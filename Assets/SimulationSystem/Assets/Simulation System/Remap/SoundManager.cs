using System; // HapticManager
using UnityEngine;
using System.Threading;
using System.Collections;
using System.Threading.Tasks;

namespace SimulationSystem.V02.Simulation.Managers
{
    /// <summary>
    /// Centralized service responsible for all audio playback.
    /// Owns AudioSources, AudioClips, and audio lifecycle.
    ///
    /// USAGE — call static methods directly from anywhere, no reference needed:
    ///   await SoundManager.PlaySimulationStart();
    ///   SoundManager.PlayBotSummon();
    ///   SoundManager.PlayInteractionOngoing();
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        #region Singleton

        /// <summary>Global instance — accessible from anywhere.</summary>
        public static SoundManager Instance { get; private set; }

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Static API — call from anywhere, no reference needed

        // ── Prompt ───────────────────────────────────────────────────────
        public static Task PlayPromptAudio(AudioClip clip, CancellationToken ct = default)
                                => Instance._PlayPromptAudioAsync(clip, ct);
        public static void StopPromptAudio()
                                => Instance._StopPromptAudio();

        // ── Simulation ───────────────────────────────────────────────────
        public static Task PlaySimulationStart(CancellationToken ct = default)
                                => Instance._PlaySimulationStartAsync(ct);
        public static Task PlaySimulationEnd(CancellationToken ct = default)
                                => Instance._PlaySimulationEndAsync(ct);

        // ── Intro ────────────────────────────────────────────────────────
        /// <summary>
        /// Coroutine-friendly version — yield return this from a MonoBehaviour coroutine.
        /// Plays clip on simulationSource, waits for it to finish, then clears the clip.
        /// </summary>
        public static IEnumerator PlayIntroCoroutine(AudioClip clip)
                                => Instance._PlayIntroCoroutine(clip);

        // ── Interaction ──────────────────────────────────────────────────
        public static Task PlayInteractionStart(CancellationToken ct = default)
                                => Instance._PlayInteractionStartAsync(ct);
        public static Task PlayInteractionSuspend(CancellationToken ct = default)
                                => Instance._PlayInteractionSuspendAsync(ct);
        public static Task PlayInteractionComplete(CancellationToken ct = default)
                                => Instance._PlayInteractionCompleteAsync(ct);
        public static void PlayInteractionOngoing()
                                => Instance._PlayInteractionOngoing();
        public static void StopInteractionOngoing()
                                => Instance._StopInteractionOngoing();

        // Grab
        public static void PlayOnGrab(CancellationToken ct = default)
                                => Instance._PlayGrabStartAsync(ct);

        // ── Bot ──────────────────────────────────────────────────────────
        public static Task PlayBotSummon(CancellationToken ct = default)
                                => Instance._PlayBotSummonAsync(ct);
        public static Task PlayBotDismiss(CancellationToken ct = default)
                                => Instance._PlayBotDismissAsync(ct);
        public static Task PlayBotDiscard(CancellationToken ct = default)
                                => Instance._PlayBotDiscardAsync(ct);
        public static Task PlayBotUIOpen(CancellationToken ct = default)
                                => Instance._PlayBotUIOpenAsync(ct);
        public static Task PlayBotUIClose(CancellationToken ct = default)
                                => Instance._PlayBotUICloseAsync(ct);
        public static Task PlayBotHelpCalled(CancellationToken ct = default)
                                => Instance._PlayBotHelpCalledAsync(ct);
        public static Task PlayBotPromptShown(CancellationToken ct = default)
                                => Instance._PlayBotPromptShownAsync(ct);

        // ── UI & Assessment ──────────────────────────────────────────────
        public static void PlayUIClick() => Instance._PlayUIClick();
        public static void PlayWrongGrab() => Instance._PlayWrongGrab();
        public static void PlayWrongHand() => Instance._PlayWrongHand();
        public static void PlayWrongOption() => Instance._PlayWrongOption();
        public static void PlayWrongDetect() => Instance._PlayWrongDetect();
        public static void PlayFail() => Instance._PlayFail();

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Audio Sources

        [Header("Audio Sources")]
        [Tooltip("Narration / prompt voice-over.")]
        public AudioSource promptSource;

        [Tooltip("Short UI and assessment feedback stabs.")]
        public AudioSource effectsSource;

        [Tooltip("Simulation-level event stings (start / end).")]
        public AudioSource simulationSource;

        [Tooltip("Interaction-level event stings + ongoing loop.")]
        public AudioSource interactionSource;

        [Tooltip("Bot state and UI event stings.")]
        public AudioSource botSource;

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Audio Clips

        [Header("UI Sounds")]
        [SerializeField] private AudioClip uiClick;

        [Header("Assessment Sounds")]
        [SerializeField] private AudioClip wrongGrab;
        [SerializeField] private AudioClip wrongHand;
        [SerializeField] private AudioClip wrongOption;
        [SerializeField] private AudioClip wrongDetect;
        [SerializeField] private AudioClip fail;

        [Header("Simulation Sounds")]
        [Tooltip("Played once when a simulation session begins.")]
        [SerializeField] private AudioClip simulationStart;

        [Tooltip("Played once when a simulation session ends.")]
        [SerializeField] private AudioClip simulationEnd;

        [Header("Interaction Sounds")]
        [Tooltip("Played once when an interaction step begins.")]
        [SerializeField] private AudioClip interactionStart;

        [Tooltip("Played once when an interaction step is suspended / paused.")]
        [SerializeField] private AudioClip interactionSuspend;

        [Tooltip("Played once when an interaction step is successfully completed.")]
        [SerializeField] private AudioClip interactionComplete;

        [Tooltip("Looped while an interaction step is actively in progress.")]
        [SerializeField] private AudioClip interactionOngoing;

        [SerializeField] private AudioClip onGrabbed;

        [Header("Bot Sounds")]
        [Tooltip("Played when the bot is summoned into the scene.")]
        [SerializeField] private AudioClip botSummon;

        [Tooltip("Played when the bot is dismissed from the scene.")]
        [SerializeField] private AudioClip botDismiss;

        [Tooltip("Played when the bot action or input is discarded.")]
        [SerializeField] private AudioClip botDiscard;

        [Tooltip("Played when the bot UI panel opens.")]
        [SerializeField] private AudioClip botUIOpen;

        [Tooltip("Played when the bot UI panel closes.")]
        [SerializeField] private AudioClip botUIClose;

        [Tooltip("Played when the user calls for help via the bot.")]
        [SerializeField] private AudioClip botHelpCalled;

        [Tooltip("Played when the bot displays a new prompt to the user.")]
        [SerializeField] private AudioClip botPromptShown;

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Internal State

        private Coroutine _promptRoutine;
        private Coroutine _simulationRoutine;
        private Coroutine _interactionRoutine;
        private Coroutine _botRoutine;
        private Coroutine _introRoutine; // separate handle — never stomps _simulationRoutine

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                //DontDestroyOnLoad(gameObject);
                InitializeSources();
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
        #region Initialization

        /// <summary>Ensures all AudioSources exist and are 2-D.</summary>
        private void InitializeSources()
        {
            promptSource = EnsureSource(promptSource);
            effectsSource = EnsureSource(effectsSource);
            simulationSource = EnsureSource(simulationSource);
            interactionSource = EnsureSource(interactionSource);
            botSource = EnsureSource(botSource);
        }

        private AudioSource EnsureSource(AudioSource src)
        {
            if (!src) src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2-D
            return src;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Instance Implementation — Prompt Audio

        private Task _PlayPromptAudioAsync(AudioClip clip, CancellationToken ct)
        {
            if (!clip) return Task.CompletedTask;

            StopRoutineSafe(ref _promptRoutine);
            promptSource.Stop();

            var tcs = new TaskCompletionSource<bool>();

            promptSource.clip = clip;
            promptSource.Play();

            _promptRoutine = StartCoroutine(
                WaitForSourceAsync(promptSource, ct,
                    onComplete: () => tcs.TrySetResult(true),
                    onCancel: () => { promptSource.Stop(); tcs.TrySetCanceled(); }));

            return tcs.Task;
        }

        private void _StopPromptAudio()
        {
            StopRoutineSafe(ref _promptRoutine);
            promptSource.Stop();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Instance Implementation — Simulation Audio

        private Task _PlaySimulationStartAsync(CancellationToken ct) =>
            PlayOnSourceAsync(simulationSource, simulationStart, ref _simulationRoutine, ct);

        private Task _PlaySimulationEndAsync(CancellationToken ct) =>
            PlayOnSourceAsync(simulationSource, simulationEnd, ref _simulationRoutine, ct);

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Instance Implementation — Intro Audio

        /// <summary>
        /// Coroutine that plays <paramref name="clip"/> on simulationSource,
        /// waits frame-by-frame until it finishes, then clears the clip.
        /// Yielded directly by SimulationManager's IntroRoutine coroutine.
        /// </summary>
        private IEnumerator _PlayIntroCoroutine(AudioClip clip)
        {
            if (!clip) yield break;

            StopRoutineSafe(ref _introRoutine);
            simulationSource.Stop();
            simulationSource.loop = false;
            simulationSource.clip = clip;
            simulationSource.Play();

            while (simulationSource.isPlaying)
                yield return null;

            simulationSource.clip = null; // clear so later simulation sounds are unaffected
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Instance Implementation — Interaction Audio

        private Task _PlayInteractionStartAsync(CancellationToken ct)
        {
            _ = HapticManager.InteractionStart(HapticHand.Both, ct);
            return PlayOnSourceAsync(interactionSource, interactionStart, ref _interactionRoutine, ct);
        }

        private Task _PlayGrabStartAsync(CancellationToken ct)
        {
            _ = HapticManager.InteractionStart(HapticHand.Both, ct);
            return PlayOnSourceAsync(interactionSource, onGrabbed, ref _interactionRoutine, ct);
        }

        private Task _PlayInteractionSuspendAsync(CancellationToken ct)
        {
            _ = HapticManager.InteractionSuspend(HapticHand.Both, ct);
            return PlayOnSourceAsync(interactionSource, interactionSuspend, ref _interactionRoutine, ct);
        }

        private Task _PlayInteractionCompleteAsync(CancellationToken ct)
        {
            _ = HapticManager.InteractionEnd(HapticHand.Both, ct);
            return PlayOnSourceAsync(interactionSource, interactionComplete, ref _interactionRoutine, ct);
        }

        private void _PlayInteractionOngoing()
        {
            if (!interactionOngoing) return;
            if (interactionSource.isPlaying &&
                interactionSource.clip == interactionOngoing) return;

            _ = HapticManager.Detecting(HapticHand.Both);
            StopRoutineSafe(ref _interactionRoutine);
            interactionSource.Stop();
            interactionSource.clip = interactionOngoing;
            interactionSource.loop = true;
            interactionSource.Play();
        }

        private void _StopInteractionOngoing()
        {
            if (!interactionSource.isPlaying) return;
            interactionSource.Stop();
            interactionSource.loop = false;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Instance Implementation — Bot Audio

        private Task _PlayBotSummonAsync(CancellationToken ct) => PlayOnSourceAsync(botSource, botSummon, ref _botRoutine, ct);
        private Task _PlayBotDismissAsync(CancellationToken ct) => PlayOnSourceAsync(botSource, botDismiss, ref _botRoutine, ct);
        private Task _PlayBotDiscardAsync(CancellationToken ct) => PlayOnSourceAsync(botSource, botDiscard, ref _botRoutine, ct);
        private Task _PlayBotUIOpenAsync(CancellationToken ct) => PlayOnSourceAsync(botSource, botUIOpen, ref _botRoutine, ct);
        private Task _PlayBotUICloseAsync(CancellationToken ct) => PlayOnSourceAsync(botSource, botUIClose, ref _botRoutine, ct);
        private Task _PlayBotHelpCalledAsync(CancellationToken ct) => PlayOnSourceAsync(botSource, botHelpCalled, ref _botRoutine, ct);
        private Task _PlayBotPromptShownAsync(CancellationToken ct) => PlayOnSourceAsync(botSource, botPromptShown, ref _botRoutine, ct);

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Instance Implementation — UI & Assessment

        private void _PlayUIClick() => PlayEffect(uiClick);
        private void _PlayWrongGrab() => PlayEffect(wrongGrab);
        private void _PlayWrongHand() => PlayEffect(wrongHand);
        private void _PlayWrongOption() => PlayEffect(wrongOption);
        private void _PlayWrongDetect() => PlayEffect(wrongDetect);
        private void _PlayFail() => PlayEffect(fail);

        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region Private Helpers

        private void PlayEffect(AudioClip clip)
        {
            if (!clip) return;
            effectsSource.Stop();
            effectsSource.clip = clip;
            effectsSource.Play();
        }

        /// <summary>
        /// Plays <paramref name="clip"/> on <paramref name="src"/>, cancels any
        /// in-progress routine on that source, and returns an awaitable Task.
        /// </summary>
        private Task PlayOnSourceAsync(AudioSource src,
                                        AudioClip clip,
                                        ref Coroutine routineField,
                                        CancellationToken ct)
        {
            if (!clip) return Task.CompletedTask;

            StopRoutineSafe(ref routineField);
            src.Stop();
            src.loop = false;

            var tcs = new TaskCompletionSource<bool>();
            var capturedSrc = src;

            src.clip = clip;
            src.Play();

            routineField = StartCoroutine(
                WaitForSourceAsync(capturedSrc, ct,
                    onComplete: () => tcs.TrySetResult(true),
                    onCancel: () => { capturedSrc.Stop(); tcs.TrySetCanceled(); }));

            return tcs.Task;
        }

        /// <summary>
        /// Polls <paramref name="src"/>.isPlaying each frame and invokes the
        /// appropriate callback when it stops or when cancellation is requested.
        /// </summary>
        private IEnumerator WaitForSourceAsync(AudioSource src,
                                                CancellationToken ct,
                                                Action onComplete,
                                                Action onCancel)
        {
            while (src.isPlaying)
            {
                if (ct.IsCancellationRequested)
                {
                    onCancel?.Invoke();
                    yield break;
                }
                yield return null;
            }

            if (ct.IsCancellationRequested)
                onCancel?.Invoke();
            else
                onComplete?.Invoke();
        }

        private void StopRoutineSafe(ref Coroutine routine)
        {
            if (routine == null) return;
            StopCoroutine(routine);
            routine = null;
        }

        #endregion
    }
}