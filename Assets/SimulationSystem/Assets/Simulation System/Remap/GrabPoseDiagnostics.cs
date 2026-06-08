using UnityEngine;

/// <summary>
/// Drop this on a GrabInteraction object to validate its pose setup at runtime.
/// Logs detailed diagnostics when poses fail to apply. Remove for shipping builds.
///
/// Also provides a manual test button: call TestPose() from a custom inspector
/// or UnityEvent to lock/unlock a specific pose slot without needing to grab.
/// </summary>
public class GrabPoseDiagnostics : MonoBehaviour
{
    [Header("Test Target (optional)")]
    [Tooltip("The GrabPinchDetector to pull lock components from. " +
             "If empty, searches the scene.")]
    public GrabPinchDetector testDetector;

    [Header("Test Parameters")]
    public InputMode testInputMode = InputMode.HandTracking;
    public Handedness testHandedness = Handedness.Right;

    private GrabInteraction _grab;
    private bool _testLocked;

    private void Awake()
    {
        _grab = GetComponent<GrabInteraction>();
        if (_grab == null)
            Debug.LogError("GrabPoseDiagnostics: no GrabInteraction found on this object.", this);
    }

    private void Start()
    {
        ValidateSetup();
    }

    // ── Validation ───────────────────────────────────────────────────────────

    /// <summary>
    /// Checks every pose slot and its associated lock components.
    /// Call at Start or any time you want a full diagnostic dump.
    /// </summary>
    public void ValidateSetup()
    {
        if (_grab == null) return;

        Debug.Log($"=== GrabPoseDiagnostics: '{gameObject.name}' ===", this);

        ValidateSlot("Hand Left",        _grab.poseHandLeft,        PoseType.HandTracking);
        ValidateSlot("Hand Right",       _grab.poseHandRight,       PoseType.HandTracking);
        ValidateSlot("Controller Left",  _grab.poseControllerLeft,  PoseType.Controller);
        ValidateSlot("Controller Right", _grab.poseControllerRight, PoseType.Controller);

        // Check that detectors in the scene can reach the right lock components.
        var detectors = FindObjectsByType<GrabPinchDetector>(FindObjectsSortMode.None);
        foreach (var d in detectors)
        {
            if (d.inputMode == InputMode.HandTracking)
            {
                if (d.handPoseLock == null)
                    Debug.LogWarning($"  Detector '{d.name}' (HandTracking) has no HandPoseLock!", d);
                else if (d.handPoseLock.skeletonDriver == null)
                    Debug.LogWarning($"  Detector '{d.name}' → HandPoseLock has no skeletonDriver!", d);
                else if (d.handPoseLock.skeletonDriver.rootTransform == null)
                    Debug.LogWarning($"  Detector '{d.name}' → skeletonDriver.rootTransform is null!", d);
            }
            else
            {
                if (d.controllerPoseLock == null)
                    Debug.LogWarning($"  Detector '{d.name}' (Controller) has no ControllerPoseLock!", d);
                else if (d.controllerPoseLock.handAnimator == null)
                    Debug.LogWarning($"  Detector '{d.name}' → ControllerPoseLock has no Animator!", d);

                if (d.controllerRoot == null)
                    Debug.LogWarning($"  Detector '{d.name}' (Controller) has no controllerRoot!", d);
            }
        }

        Debug.Log($"=== End Diagnostics ===", this);
    }

    private void ValidateSlot(string slotName, RecordedPose pose, PoseType expectedType)
    {
        if (pose == null)
        {
            Debug.Log($"  [{slotName}] Empty (free grab)");
            return;
        }

        if (pose.poseType != expectedType)
        {
            Debug.LogWarning($"  [{slotName}] TYPE MISMATCH — slot expects {expectedType} " +
                             $"but pose is {pose.poseType}!", this);
        }

        if (expectedType == PoseType.HandTracking)
        {
            if (pose.jointSnapshots == null || pose.jointSnapshots.Length == 0)
                Debug.LogWarning($"  [{slotName}] Pose has NO joint snapshots!", this);
            else
                Debug.Log($"  [{slotName}] OK — {pose.jointSnapshots.Length} joints, " +
                          $"anchor={pose.hasAnchorData}");
        }
        else
        {
            Debug.Log($"  [{slotName}] OK — Trigger={pose.triggerValue:F2}, " +
                      $"Grip={pose.gripValue:F2}, anchor={pose.hasAnchorData}");
        }

        if (!pose.hasAnchorData)
        {
            Debug.LogWarning($"  [{slotName}] No anchor data — object won't snap to " +
                             "recorded grip position. Re-record with a target GrabInteraction.", this);
        }
    }

    // ── Manual test ──────────────────────────────────────────────────────────

    /// <summary>
    /// Toggle-test a pose lock without actually grabbing. Useful for verifying
    /// that the hand mesh freezes correctly.
    /// </summary>
    [ContextMenu("Toggle Test Pose")]
    public void TestPose()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("GrabPoseDiagnostics: test only works in Play Mode.", this);
            return;
        }

        if (_grab == null) return;

        if (_testLocked)
        {
            UnlockTestPose();
        }
        else
        {
            LockTestPose();
        }
    }

    private void LockTestPose()
    {
        var detector = FindDetector();
        if (detector == null) return;

        RecordedPose pose = ResolvePose();
        if (pose == null)
        {
            Debug.LogWarning($"GrabPoseDiagnostics: no pose in slot " +
                             $"{testInputMode}/{testHandedness}.", this);
            return;
        }

        if (testInputMode == InputMode.HandTracking)
        {
            var hLock = detector.handPoseLock;
            if (hLock == null)
            {
                Debug.LogError("GrabPoseDiagnostics: detector has no HandPoseLock.", this);
                return;
            }
            hLock.LockPose(pose);
            Debug.Log("GrabPoseDiagnostics: hand tracking pose LOCKED.", this);
        }
        else
        {
            var cLock = detector.controllerPoseLock;
            if (cLock == null)
            {
                Debug.LogError("GrabPoseDiagnostics: detector has no ControllerPoseLock.", this);
                return;
            }
            cLock.LockPose(pose);
            Debug.Log("GrabPoseDiagnostics: controller pose LOCKED.", this);
        }

        _testLocked = true;
    }

    private void UnlockTestPose()
    {
        var detector = FindDetector();
        if (detector == null) return;

        if (testInputMode == InputMode.HandTracking)
            detector.handPoseLock?.UnlockPose();
        else
            detector.controllerPoseLock?.UnlockPose();

        _testLocked = false;
        Debug.Log("GrabPoseDiagnostics: pose UNLOCKED.", this);
    }

    private RecordedPose ResolvePose()
    {
        if (_grab == null) return null;

        if (testInputMode == InputMode.HandTracking)
            return testHandedness == Handedness.Left ? _grab.poseHandLeft : _grab.poseHandRight;
        else
            return testHandedness == Handedness.Left ? _grab.poseControllerLeft : _grab.poseControllerRight;
    }

    private GrabPinchDetector FindDetector()
    {
        if (testDetector != null) return testDetector;

        var detectors = FindObjectsByType<GrabPinchDetector>(FindObjectsSortMode.None);
        foreach (var d in detectors)
        {
            if (d.inputMode == testInputMode && d.handedness == testHandedness)
            {
                testDetector = d;
                return d;
            }
        }

        Debug.LogError($"GrabPoseDiagnostics: no GrabPinchDetector found for " +
                       $"{testInputMode}/{testHandedness}.", this);
        return null;
    }
}
