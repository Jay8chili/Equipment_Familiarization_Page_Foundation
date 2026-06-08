using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

// ─────────────────────────────────────────────────────────────────────────────
// Data
// ─────────────────────────────────────────────────────────────────────────────

public enum PoseType
{
    HandTracking,
    Controller
}

public enum Handedness
{
    Left,
    Right
}

[System.Serializable]
public struct JointPoseSnapshot
{
    public XRHandJointID jointID;
    public Vector3 localPosition;
    public Quaternion localRotation;
}

/// <summary>
/// Stores a hand-pose snapshot and everything needed to re-place the object
/// at grab time so the recorded grip point lands exactly at the live finger.
///
/// TWO RECORDING MODES
/// ────────────────────
///   HandTracking — JointPoseSnapshot[] keyed by XRHandJointID.
///                  Applied by HandPoseLock via skeleton driver list swap.
///
///   Controller   — Two floats: triggerValue and gripValue.
///                  These are the Blend Tree parameters that fully define the
///                  hand pose. At lock time ControllerPoseLock sets these on
///                  the Animator, forces one evaluation so the bones settle,
///                  then disables the Animator. The blend tree IS the pose —
///                  no bone snapshots needed.
///
/// Anchor data (handToObjectRotOffset, objToContactLS) is identical for both
/// modes — always recorded relative to the wrist / controller root.
/// </summary>
[CreateAssetMenu(menuName = "XR Hands/Recorded Pose", fileName = "NewRecordedPose")]
public class RecordedPose : ScriptableObject
{
    [Header("Identity")]
    public PoseType poseType;
    public Handedness handedness;

    // ── Hand-tracking joints ─────────────────────────────────────────────────

    [Header("Hand Tracking Data")]
    [Tooltip("Joint local-pose snapshots (HandTracking mode). Applied by HandPoseLock.")]
    public JointPoseSnapshot[] jointSnapshots = System.Array.Empty<JointPoseSnapshot>();

    // ── Controller blend tree values ─────────────────────────────────────────

    [Header("Controller Data")]
    [Tooltip("Blend tree 'Trigger' parameter value at record time (0–1).")]
    [Range(0f, 1f)]
    public float triggerValue;

    [Tooltip("Blend tree 'Grip' parameter value at record time (0–1).")]
    [Range(0f, 1f)]
    public float gripValue;

    // ── Anchor data (shared) ─────────────────────────────────────────────────

    [Header("Anchor Data")]
    [Tooltip("True when anchor data was recorded against a GrabInteraction.")]
    public bool hasAnchorData;

    [Tooltip("Inverse(trackingRoot.rotation) * object.rotation at record time.\n" +
             "For hands: trackingRoot = wrist (skeletonDriver.rootTransform).\n" +
             "For controllers: trackingRoot = controllerRoot.\n" +
             "At grab time: desiredRot = liveRoot.rotation * handToObjectRotOffset")]
    public Quaternion handToObjectRotOffset;

    [Tooltip("(pinchPoint.position - object.position) in object rotation frame.\n" +
             "Scale-immune. Identical formula for both input modes.")]
    public Vector3 objToContactLS;

    [Header("Recorded Object Transform")]
    [Tooltip("World rotation of the object at record time.\n" +
             "Used at grab time: desiredRot = liveRoot.rotation * handToObjectRotOffset")]
    public Quaternion recordedObjectWorldRotation = Quaternion.identity;

    [Tooltip("Distance from pinch point to object center at record time.\n" +
             "Used at grab time to position the object at the exact recorded distance.")]
    public float recordedPinchToObjectDistance;

    [Tooltip("Direction from pinch point to object center in tracking-root-local space.\n" +
             "At grab time: objectPos = pinchPos + liveRoot.rotation * recordedPinchToObjectDirLS * recordedPinchToObjectDistance")]
    public Vector3 recordedPinchToObjectDirLS;

    // ── Editor: Hand-Tracking Capture ────────────────────────────────────────
#if UNITY_EDITOR

    public static RecordedPose CaptureHandTracking(
        XRHandSkeletonDriver driver,
        Transform            handRoot,
        Handedness           handedness,
        Transform            pinchPoint      = null,
        GrabInteraction      targetGrabbable = null,
        string               assetName       = "RecordedPose",
        string               saveFolder      = null)
    {
        if (driver == null)  { Debug.LogError("RecordedPose.CaptureHandTracking: driver is null.");   return null; }
        if (handRoot == null){ Debug.LogError("RecordedPose.CaptureHandTracking: handRoot is null."); return null; }

        var refs      = driver.jointTransformReferences;
        var snapshots = new List<JointPoseSnapshot>(refs.Count);
        foreach (var item in refs)
        {
            if (item.jointTransform == null) continue;
            snapshots.Add(new JointPoseSnapshot
            {
                jointID       = item.xrHandJointID,
                localPosition = item.jointTransform.localPosition,
                localRotation = item.jointTransform.localRotation,
            });
        }

        var asset             = CreateInstance<RecordedPose>();
        asset.poseType        = PoseType.HandTracking;
        asset.handedness      = handedness;
        asset.jointSnapshots  = snapshots.ToArray();

        RecordAnchorData(asset, handRoot, pinchPoint, targetGrabbable);
        return SaveAsset(asset, assetName, saveFolder);
    }

    /// <summary>
    /// Capture a controller-driven hand pose by reading the current Animator
    /// blend tree parameters. That's it — two floats define the whole pose.
    /// </summary>
    public static RecordedPose CaptureController(
        Animator        animator,
        Transform       trackingRoot,
        Handedness      handedness,
        Transform       pinchPoint      = null,
        GrabInteraction targetGrabbable = null,
        string          assetName       = "RecordedPose",
        string          saveFolder      = null)
    {
        if (animator == null)    { Debug.LogError("RecordedPose.CaptureController: animator is null.");       return null; }
        if (trackingRoot == null){ Debug.LogError("RecordedPose.CaptureController: trackingRoot is null.");   return null; }

        var asset            = CreateInstance<RecordedPose>();
        asset.poseType       = PoseType.Controller;
        asset.handedness     = handedness;
        asset.triggerValue   = animator.GetFloat("Trigger");
        asset.gripValue      = animator.GetFloat("Grip");

        Debug.Log($"RecordedPose.CaptureController: Trigger={asset.triggerValue:F3}, Grip={asset.gripValue:F3}");

        RecordAnchorData(asset, trackingRoot, pinchPoint, targetGrabbable);
        return SaveAsset(asset, assetName, saveFolder);
    }

    // ── Shared helpers ───────────────────────────────────────────────────────

    private static void RecordAnchorData(
        RecordedPose asset, Transform trackingRoot,
        Transform pinchPoint, GrabInteraction targetGrabbable)
    {
        if (pinchPoint != null && targetGrabbable != null)
        {
            Transform obj = targetGrabbable.transform;

            // Relative rotation: root → object
            asset.handToObjectRotOffset =
                Quaternion.Inverse(trackingRoot.rotation) * obj.rotation;

            // Pinch-to-object vector in object-local space (existing field)
            asset.objToContactLS =
                Quaternion.Inverse(obj.rotation) * (pinchPoint.position - obj.position);

            // ── NEW: explicit object rotation and distance ───────────────
            asset.recordedObjectWorldRotation = obj.rotation;

            Vector3 pinchToObj = obj.position - pinchPoint.position;
            asset.recordedPinchToObjectDistance = pinchToObj.magnitude;

            // Direction from pinch to object in tracking-root-local space.
            // At grab time: objPos = pinchPos + liveRoot.rotation * dirLS * distance
            if (asset.recordedPinchToObjectDistance > 0.0001f)
            {
                asset.recordedPinchToObjectDirLS =
                    Quaternion.Inverse(trackingRoot.rotation) * pinchToObj.normalized;
            }
            else
            {
                asset.recordedPinchToObjectDirLS = Vector3.zero;
            }

            asset.hasAnchorData = true;

            Debug.Log($"RecordedPose: anchor recorded for '{targetGrabbable.name}'.\n" +
                      $"  handToObjectRotOffset = {asset.handToObjectRotOffset.eulerAngles:F1}\n" +
                      $"  objToContactLS = {asset.objToContactLS:F4}\n" +
                      $"  recordedObjectWorldRotation = {asset.recordedObjectWorldRotation.eulerAngles:F1}\n" +
                      $"  recordedPinchToObjectDistance = {asset.recordedPinchToObjectDistance:F4}\n" +
                      $"  recordedPinchToObjectDirLS = {asset.recordedPinchToObjectDirLS:F4}");
        }
        else
        {
            asset.hasAnchorData = false;
        }
    }

    private static RecordedPose SaveAsset(RecordedPose asset, string assetName, string saveFolder)
    {
        if (string.IsNullOrEmpty(saveFolder))
        {
            saveFolder = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(saveFolder))
                saveFolder = "Assets";
            else if (!Directory.Exists(saveFolder))
                saveFolder = Path.GetDirectoryName(saveFolder);
        }

        string path = AssetDatabase.GenerateUniqueAssetPath($"{saveFolder}/{assetName}.asset");
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;

        string info = asset.poseType == PoseType.HandTracking
            ? $"{asset.jointSnapshots.Length} joints"
            : $"Trigger={asset.triggerValue:F2}, Grip={asset.gripValue:F2}";

        Debug.Log($"RecordedPose ({asset.poseType}, {asset.handedness}): {info} → {path}");
        return asset;
    }
#endif
}

// ─────────────────────────────────────────────────────────────────────────────
// Editor recorder
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Place anywhere in the scene. In Play Mode, hold the object in the desired
/// grip, then right-click → context menu to record.
///
/// Supports BOTH input modes:
///   HandTracking  — uses handPoseLock → skeletonDriver + rootTransform
///   Controller    — reads Trigger/Grip floats from the hand Animator
/// </summary>
public class PoseRef : MonoBehaviour
{
    [Header("Mode")]
    public InputMode recordMode = InputMode.HandTracking;
    public Handedness handedness = Handedness.Right;

    [Header("Contact & Target")]
    public Transform pinchPoint;
    public GrabInteraction targetGrabbable;

    [Header("Hand Tracking (when recordMode = HandTracking)")]
    [Tooltip("Provides driver and wrist root.")]
    public HandPoseLock handPoseLock;

    [Header("Controller (when recordMode = Controller)")]
    [Tooltip("Provides Animator reference for reading blend tree values.")]
    public ControllerPoseLock controllerPoseLock;

    [Tooltip("Stable tracking reference for controller mode.")]
    public Transform controllerRoot;

#if UNITY_EDITOR
    [ContextMenu("Save Hand Tracking Pose")]
    public void RecordHandTrackingPose()
    {
        if (handPoseLock == null)
        {
            Debug.LogError("PoseRef: handPoseLock not assigned.", this);
            return;
        }

        XRHandSkeletonDriver driver = handPoseLock.skeletonDriver;
        if (driver == null)
        {
            Debug.LogError("PoseRef: handPoseLock.skeletonDriver is null.", this);
            return;
        }

        Transform handRoot = driver.rootTransform;
        if (handRoot == null)
        {
            Debug.LogError("PoseRef: driver.rootTransform is null.", this);
            return;
        }

        string label = handedness == Handedness.Right ? "RightHand" : "LeftHand";
        RecordedPose.CaptureHandTracking(
            driver, handRoot, handedness,
            pinchPoint, targetGrabbable,
            assetName: $"{gameObject.name}_{label}_HandTracking");
    }

    [ContextMenu("Save Controller Pose")]
    public void RecordControllerPose()
    {
        if (controllerPoseLock == null)
        {
            Debug.LogError("PoseRef: controllerPoseLock not assigned.", this);
            return;
        }
        if (controllerPoseLock.handAnimator == null)
        {
            Debug.LogError("PoseRef: controllerPoseLock.handAnimator is null.", this);
            return;
        }
        if (controllerRoot == null)
        {
            Debug.LogError("PoseRef: controllerRoot not assigned.", this);
            return;
        }

        string label = handedness == Handedness.Right ? "RightHand" : "LeftHand";
        RecordedPose.CaptureController(
            controllerPoseLock.handAnimator,
            controllerRoot,
            handedness,
            pinchPoint, targetGrabbable,
            assetName: $"{gameObject.name}_{label}_Controller");
    }
#endif
}