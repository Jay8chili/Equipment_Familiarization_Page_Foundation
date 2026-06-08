using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Freezes finger joint poses on grab, restores live tracking on release.
///
/// ORDER OF OPERATIONS (LockPose)
/// ───────────────────────────────
/// 1. Disable additional drivers FIRST — stops Animators / other scripts
///    from overwriting bones the instant we set them.
/// 2. Write recorded joint local poses.
/// 3. Swap driver's joint list to empty — wrist root keeps tracking,
///    finger bones stay frozen.
///
/// WHY NOT DISABLE THE DRIVER
/// ───────────────────────────
/// XRHandSkeletonDriver.OnDisable() kills poseUpdated too — the wrist stops.
/// GrabInteraction tracks against rootTransform, so the root MUST keep moving.
///
/// LAZY INIT
/// ──────────
/// Skeleton driver may not be populated at Awake (subsystem hasn't fired yet).
/// TryInitialize re-checks at LockPose time.
/// </summary>
public class HandPoseLock : MonoBehaviour
{
    [Tooltip("XRHandSkeletonDriver driving finger joints. Auto-found in children if empty.\n" +
             "Also used by GrabPinchDetector to obtain the wrist tracking root.")]
    public XRHandSkeletonDriver skeletonDriver;

    [Tooltip("Additional Behaviours that write to joints (e.g. Animator). " +
             "Disabled BEFORE joint writes on lock so they can't overwrite the pose.")]
    public Behaviour[] additionalJointDrivers;

    private readonly Dictionary<XRHandJointID, Transform> _jointMap
        = new Dictionary<XRHandJointID, Transform>();

    private List<JointToTransformReference> _savedReferences;
    private List<JointToTransformReference> _restoreList;
    private readonly List<JointToTransformReference> _emptyList
        = new List<JointToTransformReference>(0);

    private bool _isLocked;
    public bool isLocked => _isLocked;

    private bool _initialized;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (skeletonDriver == null)
            skeletonDriver = GetComponentInChildren<XRHandSkeletonDriver>();

        if (skeletonDriver == null)
        {
            Debug.LogError("HandPoseLock: no XRHandSkeletonDriver found.", this);
            return;
        }

        TryInitialize();
    }

    private bool TryInitialize()
    {
        if (_initialized) return true;
        if (skeletonDriver == null) return false;

        var refs = skeletonDriver.jointTransformReferences;
        if (refs == null || refs.Count == 0) return false;

        int capacity = refs.Count;
        _savedReferences = new List<JointToTransformReference>(capacity);
        _restoreList = new List<JointToTransformReference>(capacity);

        BuildJointMap();
        _initialized = true;
        return true;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void BuildJointMap()
    {
        _jointMap.Clear();
        if (skeletonDriver == null) return;

        foreach (var item in skeletonDriver.jointTransformReferences)
            if (item.jointTransform != null)
                _jointMap[item.xrHandJointID] = item.jointTransform;
    }

    /// <summary>
    /// Freeze the hand at the recorded pose.
    ///
    /// 1) Disable additional drivers so nothing fights the pose.
    /// 2) Write recorded local transforms to each joint bone.
    /// 3) Swap the driver's joint list to empty so it stops updating fingers
    ///    but keeps updating the wrist root.
    /// </summary>
    public void LockPose(RecordedPose pose)
    {
        if (_isLocked)
        {
            Debug.LogWarning("HandPoseLock: already locked.", this);
            return;
        }
        if (pose == null)
        {
            Debug.LogError("HandPoseLock: pose is null.", this);
            return;
        }
        if (skeletonDriver == null)
        {
            Debug.LogError("HandPoseLock: skeletonDriver is null.", this);
            return;
        }

        if (!TryInitialize())
        {
            Debug.LogError("HandPoseLock: driver has no joint references yet. " +
                           "Has the XR hand subsystem delivered a hand update?", this);
            return;
        }

        if (_jointMap.Count == 0)
            BuildJointMap();

        if (_jointMap.Count == 0)
        {
            Debug.LogError("HandPoseLock: joint map is empty after rebuild.", this);
            return;
        }

        // ── STEP 1: Kill additional drivers FIRST ────────────────────────
        // This stops Animators, IK solvers, or any other scripts from
        // overwriting bone transforms the instant we set them below.
        SetAdditionalDrivers(false);

        // ── STEP 2: Write recorded joint poses ──────────────────────────
        var snaps = pose.jointSnapshots;
        int applied = 0;
        for (int i = 0; i < snaps.Length; i++)
        {
            if (_jointMap.TryGetValue(snaps[i].jointID, out Transform t))
            {
                t.localPosition = snaps[i].localPosition;
                t.localRotation = snaps[i].localRotation;
                applied++;
            }
        }

        if (applied == 0)
        {
            Debug.LogWarning($"HandPoseLock: 0 of {snaps.Length} joints matched. " +
                             "Wrong hand?", this);
        }

        // ── STEP 3: Swap joint list to empty ────────────────────────────
        // Driver stops pushing finger updates but wrist root keeps tracking.
        _savedReferences.Clear();
        _savedReferences.AddRange(skeletonDriver.jointTransformReferences);
        skeletonDriver.jointTransformReferences = _emptyList;

        _isLocked = true;

        Debug.Log($"HandPoseLock: locked — {applied}/{snaps.Length} joints applied.", this);
    }

    /// <summary>
    /// Restore live finger tracking.
    /// </summary>
    public void UnlockPose()
    {
        if (!_isLocked)
        {
            Debug.LogWarning("HandPoseLock: not locked.", this);
            return;
        }

        if (skeletonDriver != null && _savedReferences != null && _savedReferences.Count > 0)
        {
            _restoreList.Clear();
            _restoreList.AddRange(_savedReferences);
            _savedReferences.Clear();
            skeletonDriver.jointTransformReferences = _restoreList;
        }
        else
        {
            Debug.LogWarning("HandPoseLock: could not restore joint references.", this);
        }

        // Re-enable additional drivers AFTER restoring the joint list
        // so they see live data immediately.
        SetAdditionalDrivers(true);
        _isLocked = false;

        Debug.Log("HandPoseLock: unlocked — live tracking resumed.", this);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetAdditionalDrivers(bool state)
    {
        if (additionalJointDrivers == null) return;
        for (int i = 0; i < additionalJointDrivers.Length; i++)
            if (additionalJointDrivers[i] != null)
                additionalJointDrivers[i].enabled = state;
    }
}