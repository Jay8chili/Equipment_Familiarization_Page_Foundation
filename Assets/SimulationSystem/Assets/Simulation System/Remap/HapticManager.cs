using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace SimulationSystem.V02.Simulation.Managers
{
    /// <summary>
    /// Identifies which controller(s) should receive haptic feedback.
    /// </summary>
    public enum HapticHand
    {
        Left,
        Right,
        Both
    }

    /// <summary>
    /// Centralized service for playing XR haptic feedback.
    /// Use this instead of calling XR haptics APIs directly.
    ///
    /// USAGE — call static methods directly from anywhere, no reference needed:
    ///   await HapticManager.Grab(HapticHand.Right);
    ///   HapticManager.UIClick(HapticHand.Left);
    ///   await HapticManager.DoubleBuzz(HapticHand.Both);
    /// </summary>
    public class HapticManager : MonoBehaviour
    {
        #region Singleton

        public static HapticManager Instance { get; private set; }

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Static API — call from anywhere, no reference needed

        // ── Assessment ───────────────────────────────────────────────────
        public static Task UIClick(HapticHand hand, CancellationToken ct = default)
                             => Instance._UIClick(hand, ct);
        public static Task Grab(HapticHand hand, CancellationToken ct = default)
                             => Instance._Grab(hand, ct);
        public static Task WrongGrab(HapticHand hand, CancellationToken ct = default)
                             => Instance._WrongGrab(hand, ct);
        public static Task Detecting(HapticHand hand, CancellationToken ct = default)
                             => Instance._Detecting(hand, ct);
        public static Task DetectionComplete(CancellationToken ct = default)
                             => Instance._DetectionComplete(ct);
        public static Task Error(CancellationToken ct = default)
                             => Instance._Error(ct);

        // ── Interaction lifecycle ─────────────────────────────────────────
        public static Task InteractionStart(HapticHand hand, CancellationToken ct = default)
                             => Instance._InteractionStart(hand, ct);
        public static Task InteractionEnd(HapticHand hand, CancellationToken ct = default)
                             => Instance._InteractionEnd(hand, ct);
        public static Task InteractionSuspend(HapticHand hand, CancellationToken ct = default)
                             => Instance._InteractionSuspend(hand, ct);

        // Grab
        public static Task OnGrabbed(HapticHand hand, CancellationToken ct = default)
                             => Instance._OnGrabbed(hand, ct);
        public static Task OnProximityGrab(HapticHand hand, CancellationToken ct = default)
                             => Instance._OnProximityGrabb(hand, ct);


        // ── Special ──────────────────────────────────────────────────────
        /// <summary>
        /// Two buzzes in sequence — first buzz plays, then a short gap, then the second.
        /// </summary>
        public static Task DoubleBuzz(HapticHand hand, CancellationToken ct = default)
                             => Instance._DoubleBuzz(hand, ct);

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Internal State

        private Coroutine _hapticRoutine;

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                //DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Instance Implementation — Presets

        private Task _UIClick(HapticHand hand, CancellationToken ct) => PlayHapticAsync(0.2f, 0.05f, hand, ct: ct);
        private Task _Grab(HapticHand hand, CancellationToken ct) => PlayHapticAsync(0.4f, 0.10f, hand, ct: ct);
        private Task _WrongGrab(HapticHand hand, CancellationToken ct) => PlayHapticAsync(0.8f, 0.30f, hand, ct: ct);
        private Task _Detecting(HapticHand hand, CancellationToken ct) => PlayHapticAsync(0.15f, 0.20f, hand, ct: ct);
        private Task _DetectionComplete(CancellationToken ct) => PlayHapticAsync(0.6f, 0.20f, HapticHand.Both, ct: ct);
        private Task _Error(CancellationToken ct) => PlayHapticAsync(1.0f, 0.40f, HapticHand.Both, ct: ct);

        // ── Interaction lifecycle ─────────────────────────────────────────
        // Start  — firm double-length pulse so the user knows something began
        private Task _InteractionStart(HapticHand hand, CancellationToken ct) => PlayHapticAsync(0.5f, 0.20f, hand, ct: ct);
        // End    — clean short confirm pulse
        private Task _InteractionEnd(HapticHand hand, CancellationToken ct) => PlayHapticAsync(0.6f, 0.15f, hand, ct: ct);
        // Suspend — soft low-amplitude nudge to signal pause
        private Task _InteractionSuspend(HapticHand hand, CancellationToken ct) => PlayHapticAsync(0.25f, 0.10f, hand, ct: ct);

        //Grab
        private Task _OnGrabbed(HapticHand hand, CancellationToken ct) => PlayHapticAsync(0.25f, 0.10f, hand, ct: ct);
        private Task _OnProximityGrabb(HapticHand hand, CancellationToken ct) => PlayHapticAsync(0.25f, 0.10f, hand, ct: ct);


        // ── Double buzz ───────────────────────────────────────────────────
        private async Task _DoubleBuzz(HapticHand hand, CancellationToken ct)
        {
            await PlayHapticAsync(0.6f, 0.10f, hand, ct: ct);

            if (ct.IsCancellationRequested) return;

            // Short silent gap between the two buzzes
            await Task.Delay(80, ct);

            if (ct.IsCancellationRequested) return;

            await PlayHapticAsync(0.6f, 0.10f, hand, ct: ct);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Core Async Haptic Engine

        /// <summary>
        /// Sends a haptic impulse and returns a Task that completes once
        /// <paramref name="duration"/> seconds have elapsed.
        /// Cancelling the token resolves the Task early but does NOT cut
        /// the hardware impulse short (XR haptics have no stop API).
        /// </summary>
        private Task PlayHapticAsync(
            float amplitude,
            float duration,
            HapticHand hand,
            float frequency = 0f,
            CancellationToken ct = default)
        {
            amplitude = Mathf.Clamp01(amplitude);

#if UNITY_EDITOR
            Debug.Log($"[HAPTICS] Hand: {hand} | Amp: {amplitude} | Dur: {duration}s");
#endif

            var controller = hand switch
            {
                HapticHand.Left => HapticsUtility.Controller.Left,
                HapticHand.Right => HapticsUtility.Controller.Right,
                _ => HapticsUtility.Controller.Both
            };

            HapticsUtility.SendHapticImpulse(amplitude, duration, controller, frequency, 0);

            var tcs = new TaskCompletionSource<bool>();

            if (_hapticRoutine != null)
                StopCoroutine(_hapticRoutine);

            _hapticRoutine = StartCoroutine(HapticWaitRoutine(duration, ct, tcs));

            return tcs.Task;
        }

        private IEnumerator HapticWaitRoutine(
            float duration,
            CancellationToken ct,
            TaskCompletionSource<bool> tcs)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (ct.IsCancellationRequested)
                {
                    tcs.TrySetCanceled();
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            tcs.TrySetResult(true);
        }

        #endregion
    }
}