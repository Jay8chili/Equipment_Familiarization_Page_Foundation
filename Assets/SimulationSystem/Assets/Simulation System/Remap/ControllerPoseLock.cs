using System.Collections;
using UnityEngine;

/// <summary>
/// Freezes an Animator-driven hand mesh on grab, restores live animation on release.
///
/// APPROACH
/// ────────
/// 1. LockPose: set the recorded Trigger/Grip float values on the Animator,
///    force evaluation over two frames so the blend tree fully settles,
///    then disable the Animator and input manager.
///
/// 2. UnlockPose: re-enable everything so live input resumes.
///
/// FIX: TWO-FRAME SETTLE
/// ──────────────────────
/// A single Animator.Update(0) can leave bones one frame behind if the blend
/// tree uses transitions or if the Animator hasn't run its first full graph
/// evaluation yet. We now force TWO evaluations with a frame gap between them
/// before disabling the Animator. This guarantees the bones are in the final
/// pose even for complex blend trees.
///
/// ROLE IN GrabPinchDetector
/// ──────────────────────────
/// When inputMode == Controller, GrabPinchDetector passes this component to
/// GrabInteraction so the grab can lock/unlock the hand mesh.
/// </summary>
public class ControllerPoseLock : MonoBehaviour
{
    [Tooltip("The Animator driving hand animations via the blend tree.")]
    public Animator handAnimator;

    [Tooltip("The input manager feeding Trigger/Grip values. Disabled on lock so input " +
             "callbacks don't fight with the frozen pose.")]
    public ControllerHandAnimationManager animationManager;

    [Tooltip("Additional Behaviours that write to bones. Disabled on lock.")]
    public Behaviour[] additionalDrivers;


    public bool isLocked; 

    private Coroutine _lockCoroutine;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (handAnimator == null)
            handAnimator = GetComponentInChildren<Animator>();

        if (animationManager == null)
            animationManager = GetComponentInChildren<ControllerHandAnimationManager>();

        if (handAnimator == null)
            Debug.LogError("ControllerPoseLock: no Animator found.", this);
    }

    private void OnDisable()
    {
        // If we're mid-lock-coroutine and get disabled, clean up.
        if (_lockCoroutine != null)
        {
            StopCoroutine(_lockCoroutine);
            _lockCoroutine = null;
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Set the blend tree parameters to the recorded values, force evaluation
    /// over two frames so bones fully settle, then disable the Animator.
    /// </summary>
    public void LockPose(RecordedPose pose)
    {
      
        if (pose == null)
        {
            Debug.LogError("ControllerPoseLock: pose is null.", this);
            return;
        }
        if (handAnimator == null)
        {
            Debug.LogError("ControllerPoseLock: handAnimator is null.", this);
            return;
        }

        // Cancel any in-progress lock sequence.
        if (_lockCoroutine != null)
            StopCoroutine(_lockCoroutine);

        _lockCoroutine = StartCoroutine(LockCoroutine(pose));
    }

    /// <summary>
    /// Re-enable the Animator and input manager so the blend tree resumes
    /// responding to live Trigger/Grip input.
    /// </summary>
    public void UnlockPose()
    {
      

        // Cancel a pending lock if unlock arrives before it finishes.
        if (_lockCoroutine != null)
        {
            StopCoroutine(_lockCoroutine);
            _lockCoroutine = null;
        }

        if (handAnimator != null)
            handAnimator.enabled = true;

        if (animationManager != null)
            animationManager.enabled = true;

        SetAdditionalDrivers(true);
        isLocked = false;

        Debug.Log("ControllerPoseLock: unlocked — live animation resumed.", this);
    }

    // ── Lock coroutine ──────────────────────────────────────────────────────

    private IEnumerator LockCoroutine(RecordedPose pose)
    {
        // 1. Stop input immediately so callbacks don't race with us.
        if (animationManager != null)
            animationManager.enabled = false;

        SetAdditionalDrivers(false);

        // 2. Ensure the Animator is enabled so it can evaluate.
        handAnimator.enabled = true;

        // 3. Set blend tree parameters.
        handAnimator.SetFloat("Trigger", pose.triggerValue);
        handAnimator.SetFloat("Grip", pose.gripValue);

        // 4. Force first evaluation — pushes parameters into the blend tree.
        handAnimator.Update(0f);

        // 5. Wait one frame so the transform hierarchy absorbs the update.
        yield return null;

        // 6. Second evaluation — catches any blend tree transitions or
        //    one-frame-delayed state machine changes.
        handAnimator.SetFloat("Trigger", pose.triggerValue);
        handAnimator.SetFloat("Grip", pose.gripValue);
        handAnimator.Update(0f);

        // 7. Now freeze — bones are in their final positions.
        handAnimator.enabled = false;

        isLocked = true;
        _lockCoroutine = null;

        Debug.Log($"ControllerPoseLock: locked — Trigger={pose.triggerValue:F3}, " +
                  $"Grip={pose.gripValue:F3}", this);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetAdditionalDrivers(bool state)
    {
        if (additionalDrivers == null) return;
        for (int i = 0; i < additionalDrivers.Length; i++)
            if (additionalDrivers[i] != null)
                additionalDrivers[i].enabled = state;
    }
}