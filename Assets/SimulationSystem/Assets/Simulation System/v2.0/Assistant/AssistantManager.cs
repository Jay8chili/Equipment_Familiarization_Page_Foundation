using SimulationSystem.V02.Simulation.Managers;
using SimulationSystem.V02.StateInteractions;
using SimulationSystem.V02.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

namespace SimulationSystem.V02.Assistant
{
    public class AssistantManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // SINGLETON
        // ─────────────────────────────────────────────

        public static AssistantManager Instance { get; private set; }

        public static Action PromptCompleted;

        // ─────────────────────────────────────────────
        // PANEL STATE MACHINE — TYPES & FIELDS
        // ─────────────────────────────────────────────

        // NOTE: BotFollowMe and Contamination are full members of this enum and participate
        // in the interrupt/stash flow exactly like Help, BotUI, etc.
        // SpeedAlert is intentionally kept separate — it overlays independently and never
        // interrupts or stashes the current panel.
        private enum ActivePanel { None, Prompt, BotUI, Help, BotFollowMe, Contamination, Assist }

        /// <summary>The panel currently popped in and visible. Only one panel is active at a time.</summary>
        private ActivePanel _currentPanel = ActivePanel.None;

        /// <summary>Panel that was interrupted by a higher-priority panel; restored when the interrupter closes.</summary>
        private ActivePanel _stashedPanel = ActivePanel.None;

        /// <summary>True when this manager paused the simulation to show a panel; used to auto-resume on dismiss.</summary>
        private bool _wasSimPausedByPanel = false;

        // One CTS per panel to cancel in-flight DoScale tweens
        private CancellationTokenSource _promptCts;
        private CancellationTokenSource _botUICts;
        private CancellationTokenSource _helpCts;
        private CancellationTokenSource _progressCts;
        private CancellationTokenSource _assistCts;
        private CancellationTokenSource _botFollowMeCts;
        private CancellationTokenSource _contaminationCts;
        private CancellationTokenSource _guideUnavailableCts;
        private Coroutine _guideUnavailableCoroutine;

        // Cached original scene scales — panels may be world-space canvases
        // with tiny scales like 0.001. We pop to these, not to Vector3.one.
        private Dictionary<GameObject, Vector3> _panelOriginalScales = new Dictionary<GameObject, Vector3>();

        // ─────────────────────────────────────────────
        // RUNTIME STATE
        // ─────────────────────────────────────────────

        /// <summary>True while the prompt panel is animating or waiting for its audio to finish.</summary>
        private bool _promptActive = false;

        /// <summary>Reference to the active HidePromptAfter coroutine; stopped early if a new state starts.</summary>
        private Coroutine _promptCoroutine = null;

        /// <summary>BotUI text queued while the prompt is still active; flushed when the prompt closes.</summary>
        private string _pendingUIContent = null;

        /// <summary>Callback queued alongside <see cref="_pendingUIContent"/>; invoked when the deferred BotUI is dismissed.</summary>
        private UnityAction _pendingOnComplete = null;

        /// <summary>Callback for the currently displayed BotUI panel; invoked when the dismiss button is pressed.</summary>
        private UnityAction _uiOnComplete = null;

        /// <summary>Tracks whether the help panel is currently visible.</summary>
        private bool _isHelpVisible = false;

        /// <summary>Tracks whether the settings panel is currently visible.</summary>
        private bool _isSettingsVisible = false;

        // ─────────────────────────────────────────────
        // SERIALIZED REFERENCES — BOT CORE
        // ─────────────────────────────────────────────

        [Header("Bot Controller")]
        [SerializeField] private BotController botController;

        // ─────────────────────────────────────────────
        // SERIALIZED REFERENCES — PANELS
        // ─────────────────────────────────────────────

        [Header("Bot Prompt Panel (shown on state start)")]
        [Tooltip("Root GameObject of the prompt panel shown at the start of each simulation state.")]
        [SerializeField] private GameObject botPromptPanel;
        [Tooltip("Text component that displays the prompt message inside the prompt panel.")]
        [SerializeField] private TMP_Text botPromptText;

        [Header("Bot UI Panel (shown per-interaction, not at state start)")]
        [Tooltip("Root GameObject of the BotUI panel shown during individual interactions.")]
        [SerializeField] private GameObject botUIPanel;
        [Tooltip("Text component that displays the interaction message inside the BotUI panel.")]
        [SerializeField] private TMP_Text botUIText;

        [Header("Bot Button (UI panel dismiss only)")]
        [Tooltip("Button shown on the BotUI panel; pressing it dismisses the panel and fires the onComplete callback.")]
        [SerializeField] private CustomButton botButton;

        [Header("Bot Progress Panel")]
        [Tooltip("Root GameObject of the progress panel that displays simulation completion.")]
        [SerializeField] private GameObject botProgressPanel;
        [Tooltip("All Progress components to update simultaneously. Each manages its own slider and text.")]
        [SerializeField] private Progress[] progressTrackers;
        [Tooltip("Text component that displays the progress percentage alongside the slider.")]
        [SerializeField] private TMP_Text botProgressText;
        [Tooltip("Duration in seconds for the slider to animate from its current value to the target progress.")]
        [SerializeField] private float progressAnimDuration = 0.6f;

        [Header("Bot Follow Me Panel")]
        [Tooltip("Root GameObject of the follow-me panel shown when the bot is guiding the user. " +
                 "Participates in full interrupt/stash flow identical to Help and BotUI.")]
        [SerializeField] private GameObject botFollowMePanel;

        [Header("Settings Panel")]
        [Tooltip("Root GameObject of the settings panel; toggled via OpenSettings / CloseSettings.")]
        [SerializeField] private GameObject settingsPanel;

        // ─────────────────────────────────────────────
        // SERIALIZED REFERENCES — POP ANIMATION
        // ─────────────────────────────────────────────

        [Header("Panel Pop Animation")]
        [Tooltip("Duration of pop-in / pop-out scale animation (seconds).")]
        [SerializeField] private float popDuration = 0.2f;

        [Tooltip("Ease for pop-in (scale 0→1). OutBack gives a bouncy overshoot.")]
        [SerializeField] private Ease popInEase = Ease.OutBack;

        [Tooltip("Ease for pop-out (scale 1→0). InBack gives a tuck-in feel.")]
        [SerializeField] private Ease popOutEase = Ease.InBack;

        // ─────────────────────────────────────────────
        // SERIALIZED REFERENCES — HELP PANEL
        // ─────────────────────────────────────────────

        [Header("[HELP PANEL]")]
        [Header("Refrences")]
        [Tooltip("Root GameObject of the help panel shown when the user requests assistance.")]
        [SerializeField] private GameObject helpPanel;
        [Tooltip("Button that opens the help panel; also closes the assist panel automatically.")]
        [SerializeField] private CustomButton helpButton;
        [Tooltip("Button inside the help panel that closes it and re-opens the assist panel.")]
        [SerializeField] private CustomButton helpCloseButton;

        // ─────────────────────────────────────────────
        // SERIALIZED REFERENCES — ASSIST PANEL
        // ─────────────────────────────────────────────

        [Header("[ ASSIST PANEL ]")]
        [Header("Refrences")]
        [Tooltip("Root GameObject of the assist panel toggled by the Y button.")]
        [SerializeField] private GameObject assistPanel;
        [Tooltip("Input action reference for the controller Y button that toggles the assist panel.")]
        [SerializeField] private InputActionReference buttonYAction;
        public static Action HelpEnabled;
        public static Action HelpDisabled;

        /// <summary>Tracks whether the assist panel is currently popped in.</summary>
        private bool isAssistantPanelVisible = false;

        // ─────────────────────────────────────────────
        // SERIALIZED REFERENCES — PATHFINDING
        // ─────────────────────────────────────────────

        [Header(" [ PATHFINDING ] ")]
        [Header("Refrences")]
        [Tooltip("Button that triggers bot guidance toward the current interaction target.")]
        [SerializeField] CustomButton pathfindingButton;

        [Tooltip("Panel shown when the guide button is pressed but the current interaction does not support guidance.")]
        [SerializeField] private GameObject guideUnavailablePanel;

        [Tooltip("How long (seconds) the guide-unavailable panel stays visible before auto-dismissing.")]
        [SerializeField] private float guideUnavailableDuration = 3f;

        // ─────────────────────────────────────────────
        // SERIALIZED REFERENCES — BOT DISCARD
        // ─────────────────────────────────────────────

        [Header("Bot Discard")]
        [Tooltip("Total time in seconds for the bot to dissolve-out, teleport, and dissolve-in a discarded object.")]
        [SerializeField] private float discardMoveDuration = 0.8f;

        [Tooltip("Zone GameObject that is enabled while a discard operation is in progress.")]
        [SerializeField] private GameObject discardZone;

        /// <summary>Queue of objects waiting to be moved to their discard destination; processed one at a time.</summary>
        private readonly Queue<(GameObject obj, Transform destination)> _discardQueue
            = new Queue<(GameObject, Transform)>();

        /// <summary>True while RunDiscardQueue is processing; prevents multiple coroutines running in parallel.</summary>
        private bool _discardRunning = false;

        // ─────────────────────────────────────────────
        // SERIALIZED REFERENCES — CONTAMINATION
        // ─────────────────────────────────────────────

        [Header("[ CONTAMINATION ]")]
        [Header("Refrences")]
        [Tooltip("Reference to the ContaminationManager that fires contamination triggered/resolved events.")]
        [SerializeField] private ContaminationManager contaminationManager;
        [Tooltip("Panel shown to the user when a contamination event is active. " +
                 "Participates in full interrupt/stash flow identical to Help and BotUI.")]
        [SerializeField] private GameObject contaminationPanel;

        // ─────────────────────────────────────────────
        // SERIALIZED REFERENCES — HAND SPEED TRACKING
        // ─────────────────────────────────────────────
        //
        // DUAL-MODE SPEED ALERT — fully independent of the ActivePanel state machine:
        //
        //   Mode A (no active panel):
        //     speedAlertPanel pops in at its default local position. Stays there until resolved.
        //
        //   Mode B (another panel already active):
        //     The existing panel is left completely untouched — no stash, no interrupt.
        //     speedAlertPanel is activated, moved to speedAlertAnchor's local position,
        //     then slides to its default local position in parallel with the pop-in animation.
        //     Stays at default until resolved.
        //
        // In both modes, resolve = PopOut from the default local position.
        // VRHandSpeedTracker owns WHEN violations start and end.

        [Header("[ HANDSPEEDTRACKING ]")]
        [Header("Refrences")]
        [Tooltip("Reference to the VRHandSpeedTracker that fires speed violation detected/resolved events.")]
        [SerializeField] private VRHandSpeedTracker vrHandSpeedTracker;

        [Tooltip("The speed alert panel. In-flow at its default position when no other panel is active; " +
                 "moves to speedAlertAnchor when another panel is active.")]
        [SerializeField] private GameObject speedAlertPanel;

        [Tooltip("Empty GameObject placed as a sibling of speedAlertPanel under the same parent. " +
                 "Its localPosition is used as the anchor offset when another panel is active (Mode B).")]
        [SerializeField] private Transform speedAlertAnchor;

        [Tooltip("Reserved: minimum seconds the speed alert panel remains visible before it can be auto-hidden.")]
        [SerializeField] private float speedPanelVisiblityTiming;

        [Tooltip("Duration in seconds for the speedAlertPanel to smoothly move between its default position and the anchor offset.")]
        [SerializeField] private float speedAlertMoveDuration = 0.3f;

        // ─────────────────────────────────────────────
        // SERIALIZED REFERENCES — LEGACY PROMPT
        // ─────────────────────────────────────────────

        [Header("Scene Prompt (legacy / non-state use)")]
        [Tooltip("Legacy prompt component used outside the state machine; prefer EvaluateStateInteractions for state-driven prompts.")]
        [SerializeField] private PromptInteraction prompt;

        // ─────────────────────────────────────────────
        // PROGRESS — PRIVATE STATE
        // ─────────────────────────────────────────────

        /// <summary>The progress value (0–1) the slider is currently animating toward.</summary>
        private float _progressTarget = 0f;

        /// <summary>Reference to the active AnimateProgress coroutine; stopped before starting a new animation.</summary>
        private Coroutine _progressCoroutine = null;

        // ─────────────────────────────────────────────
        // SPEED ALERT — PRIVATE STATE
        // ─────────────────────────────────────────────
        // SpeedAlert is fully independent of the ActivePanel state machine.
        // It never sets _currentPanel, never stashes anything, and never interrupts
        // any other panel. It simply overlays on top of whatever is currently showing.

        /// <summary>Cached default local position of the speedAlertPanel as placed in the scene. Never changes after Awake.</summary>
        private Vector3 _speedAlertDefaultLocalPosition;

        /// <summary>CTS for the speedAlertPanel's pop-in / pop-out scale tween. Independent of all other panel CTS fields.</summary>
        private CancellationTokenSource _speedAlertCts;

        /// <summary>
        /// Reference to the anchor→default slide coroutine so it can be cancelled
        /// if a new violation fires before the previous slide finishes.
        /// </summary>
        private Coroutine _speedAlertSlideCoroutine;


        // ═════════════════════════════════════════════
        // UNITY LIFECYCLE
        // ═════════════════════════════════════════════

        #region Unity Methods

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Cache each panel's original scene scale (may be tiny for world-space canvases),
            // then zero it out and disable. PopIn will scale to the cached value.
            CacheAndHidePanel(botPromptPanel);
            CacheAndHidePanel(botUIPanel);
            CacheAndHidePanel(helpPanel);
            CacheAndHidePanel(assistPanel);
            CacheAndHidePanel(botFollowMePanel);
            CacheAndHidePanel(contaminationPanel);
            CacheAndHidePanel(guideUnavailablePanel);

            // Cache local position before hiding so we always know where to return the panel.
            // Local position is parent-relative and stays correct regardless of world transform.
            if (speedAlertPanel != null)
                _speedAlertDefaultLocalPosition = speedAlertPanel.transform.localPosition;
            CacheAndHidePanel(speedAlertPanel);

            if (settingsPanel)
            {
                settingsPanel.SetActive(false);
            }
            if (discardZone)
            {
                discardZone?.SetActive(false);
            }
            //botButton?.gameObject.SetActive(false);

            if (progressTrackers != null)
                foreach (var t in progressTrackers)
                    if (t != null) t.SetSliderValue(0f);

            // Warm up TMP font atlases so text dimensions are correct on first show.
            // See WarmUpTMPAtlases for full explanation.
            WarmUpTMPAtlases();

            AddListnersPathfinding();
            AddListnersForHelpButtonPanel();
        }

        private void OnEnable()
        {
            BotController.BotSummonedWithKey += OnBotSummonedWithKey;
            BotController.BotDismissedWithKey += OnBotDismissedWithKey;

            buttonYAction.action.actionMap.Enable(); // enables the whole map
            buttonYAction.action.performed += OnYButtonPressed;

            TeleportManager.TeleportStarted += OnTeleoprtStarted;
            TeleportManager.TeleportCompleted += OnTeleportCompleted;

            AddListnersContamination();
            AddListnersHandSpeedTracking();
        }

        private void OnDisable()
        {
            BotController.BotSummonedWithKey -= OnBotSummonedWithKey;
            BotController.BotDismissedWithKey -= OnBotDismissedWithKey;

            buttonYAction.action.performed -= OnYButtonPressed;

            TeleportManager.TeleportStarted -= OnTeleoprtStarted;
            TeleportManager.TeleportCompleted -= OnTeleportCompleted;

            RemoveListnersPathfinding();
            RemoveListnersContamination();
            RemoveListnersHandSpeedTracking();

            _promptCts?.Cancel();
            _botUICts?.Cancel();
            _helpCts?.Cancel();
            _progressCts?.Cancel();
            _assistCts?.Cancel();
            _botFollowMeCts?.Cancel();
            _contaminationCts?.Cancel();
            _speedAlertCts?.Cancel();
            _guideUnavailableCts?.Cancel();
        }

        #endregion


        // ═════════════════════════════════════════════
        // POP ANIMATION HELPERS
        // ═════════════════════════════════════════════
        // Panels start DISABLED. PopIn enables then scales 0→1.
        // PopOut scales 1→0 then disables. One panel visible at a time.

        /// <summary>
        /// Stores the panel's current localScale, zeroes it, and disables the GameObject.
        /// SetActive(false) is used intentionally here — it ensures the Canvas layout system
        /// runs a full rebuild cycle on the next SetActive(true), which is the only reliable
        /// way to get ContentSizeFitter and LayoutGroup to recalculate correctly in World Space.
        /// Also disables all CustomButton colliders via PanelButtonController if present.
        /// Called once in Awake for every managed panel.
        /// </summary>
        private void CacheAndHidePanel(GameObject panel)
        {
            if (panel == null) return;
            /* _panelOriginalScales[panel] = panel.transform.localScale;
             panel.transform.localScale = Vector3.zero;
             panel.SetActive(false);*/
            /* panel.SetActive(false);*/
            panel.GetComponent<CanvasGroup>().alpha = 0;
            // Disable button colliders — enabled only when this panel becomes active.
            panel.GetComponent<PanelButtonController>()?.SetCollidersEnabled(false);
        }

        /// <summary>
        /// Returns the scale cached in Awake for the given panel.
        /// Falls back to Vector3.one if the panel was never cached (should not happen in normal use).
        /// </summary>
        private Vector3 GetCachedScale(GameObject panel)
        {
            /*if (panel != null && _panelOriginalScales.TryGetValue(panel, out Vector3 scale))
                return scale;*/
            return Vector3.one; // fallback
        }

        /// <summary>
        /// Forces TMP to measure every text element across all managed panels and fully
        /// populate the font atlas before any panel is shown for the first time.
        /// Without this, TMP loads atlas textures on demand — the first time a panel shows,
        /// TMP may report zero or wrong text dimensions, causing ContentSizeFitter to
        /// calculate wrong child sizes and misalign the layout.
        /// Called once at the end of Awake. All panels are invisible at this point
        /// so ForceMeshUpdate runs with no visual side effects.
        /// </summary>
        private void WarmUpTMPAtlases()
        {
            GameObject[] panels =
            {
                botPromptPanel, botUIPanel, botProgressPanel,
                helpPanel, assistPanel, botFollowMePanel,
                contaminationPanel, speedAlertPanel
            };

            foreach (GameObject panel in panels)
            {
                if (panel == null) continue;
                foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(includeInactive: true))
                    text.ForceMeshUpdate();
            }
        }

        /// <summary>
        /// Forces an immediate synchronous layout rebuild on the Canvas inside the given panel.
        /// Called after setting dynamic text and before PopIn so ContentSizeFitter / LayoutGroup
        /// settle at the correct size before the scale tween starts — prevents mid-tween
        /// element repositioning.
        /// The panel root is a plain Transform wrapper so GetComponentInChildren reaches the Canvas.
        /// </summary>
        private void ForceRebuildLayout(GameObject panel)
        {
            if (panel == null) return;
            Canvas canvas = panel.GetComponentInChildren<Canvas>(includeInactive: true);
            if (canvas == null) return;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
        }

        /// <summary>
        /// Enables the panel, forces an immediate layout rebuild, then scales from zero
        /// to its cached original scale using the pop-in ease.
        /// SetActive(true) triggers the Canvas layout rebuild cycle that ContentSizeFitter
        /// and LayoutGroup need to recalculate correctly in World Space.
        /// ForceRebuildLayoutImmediate is called immediately after activation so the layout
        /// is fully settled before the scale tween begins — no mid-tween repositioning.
        /// Enables CustomButton colliders only after the tween completes.
        /// Cancels any in-flight tween on the same panel before starting a new one.
        /// </summary>
        private void PopIn(GameObject panel, ref CancellationTokenSource cts)
        {
            if (panel == null) return;
            cts?.Cancel();
            cts = new CancellationTokenSource();

            var cG = panel.GetComponent<CanvasGroup>();
            cG.alpha = 0f;

            // SetActive(true) triggers the full Canvas layout rebuild cycle.
            panel.SetActive(true);

            ForceRebuildLayout(panel);

            _ = cG.DoFade(1f, popDuration, popInEase, cts.Token,
                onComplete: () =>
                {
                    // Enable colliders only once fully scaled up — prevents accidental
                    // button triggers while the panel is still animating in.
                    panel?.GetComponent<PanelButtonController>()?.SetCollidersEnabled(true);
                });
            // Force an immediate synchronous rebuild right after activation so
            // ContentSizeFitter / LayoutGroup settle before the tween starts.

            /*_ = panel.transform.DoScale(targetScale, popDuration, popInEase, cts.Token,
                onComplete: () =>
                {
                    // Enable colliders only once fully scaled up — prevents accidental
                    // button triggers while the panel is still animating in.
                    panel?.GetComponent<PanelButtonController>()?.SetCollidersEnabled(true);
                });*/
        }

        /// <summary>
        /// Scales the panel to zero using the pop-out ease, then disables it.
        /// Disables CustomButton colliders immediately at tween start.
        /// SetActive(false) is called on completion so the next PopIn gets a full
        /// layout rebuild cycle via SetActive(true).
        /// If the panel is already inactive the onComplete callback fires immediately.
        /// </summary>
        private void PopOut(GameObject panel, ref CancellationTokenSource cts, Action onComplete = null)
        {
            if (panel == null) { onComplete?.Invoke(); return; }

            // Already inactive — nothing to animate.
            if (!panel.activeSelf)
            {
                onComplete?.Invoke();
                return;
            }

            // Disable colliders immediately — panel is shrinking, should not accept input.
            panel.GetComponent<PanelButtonController>()?.SetCollidersEnabled(false);

            cts?.Cancel();
            cts = new CancellationTokenSource();

            _ = panel.GetComponent<CanvasGroup>().DoFade(0, 0.1f, popOutEase, cts.Token,

                onComplete: () =>
                {
                    // Disable after tween so next PopIn triggers a full layout rebuild cycle.
                    if (panel != null) panel.SetActive(false);
                    onComplete?.Invoke();
                });
            /* _ = panel.transform.DoScale(Vector3.zero, popDuration, popOutEase, cts.Token,
                 onComplete: () =>
                 {
                     // Disable after tween so next PopIn triggers a full layout rebuild cycle.
                     if (panel != null) panel.SetActive(false);
                     onComplete?.Invoke();
                 });*/
        }

        /// <summary>
        /// Instantly cancels any tween, forces scale to zero, and disables the panel — no animation.
        /// SetActive(false) is restored so the next PopIn gets a full layout rebuild cycle.
        /// Disables CustomButton colliders immediately via PanelButtonController.
        /// Used when panels must disappear instantly (e.g. on teleport or state reset).
        /// </summary>
        private void SnapHidden(GameObject panel, ref CancellationTokenSource cts)
        {
            if (panel == null) return;
            cts?.Cancel();
            cts = new CancellationTokenSource();

            panel.GetComponent<CanvasGroup>().alpha = 0f;
            panel.SetActive(false);
            // Disable colliders immediately — panel is now inactive.

            panel.GetComponent<PanelButtonController>()?.SetCollidersEnabled(false);
        }


        // ═════════════════════════════════════════════
        // PANEL STATE MACHINE — ORCHESTRATION
        // ═════════════════════════════════════════════

        /// <summary>Maps an <see cref="ActivePanel"/> enum value to its corresponding scene GameObject.</summary>
        private GameObject GetPanelObject(ActivePanel panel)
        {
            switch (panel)
            {
                case ActivePanel.Prompt: return botPromptPanel;
                case ActivePanel.BotUI: return botUIPanel;
                case ActivePanel.Help: return helpPanel;
                case ActivePanel.BotFollowMe: return botFollowMePanel;
                case ActivePanel.Contamination: return contaminationPanel;
                case ActivePanel.Assist: return assistPanel;
                default: return null;
            }
        }

        /// <summary>Maps an <see cref="ActivePanel"/> enum value to its corresponding CancellationTokenSource by ref.</summary>
        private ref CancellationTokenSource GetPanelCts(ActivePanel panel)
        {
            switch (panel)
            {
                case ActivePanel.Prompt: return ref _promptCts;
                case ActivePanel.BotUI: return ref _botUICts;
                case ActivePanel.Help: return ref _helpCts;
                case ActivePanel.BotFollowMe: return ref _botFollowMeCts;
                case ActivePanel.Contamination: return ref _contaminationCts;
                case ActivePanel.Assist: return ref _assistCts;
                default: return ref _promptCts;
            }
        }

        /// <summary>
        /// Pops out the currently active panel and stashes it so it can be restored later.
        /// Also pauses the simulation if it was running, so it resumes when the stash is restored.
        /// Does nothing if <paramref name="newPanel"/> is already the active panel.
        /// </summary>
        private void InterruptCurrentPanel(ActivePanel newPanel)
        {
            if (_currentPanel != ActivePanel.None && _currentPanel != newPanel)
            {
                _stashedPanel = _currentPanel;

                ref var cts = ref GetPanelCts(_currentPanel);
                PopOut(GetPanelObject(_currentPanel), ref cts);

                if (SimulationManager.Instance != null && !SimulationManager.Instance.isPaused)
                {
                    SimulationManager.Instance.PauseSimulation();
                    _wasSimPausedByPanel = true;
                }
            }

            _currentPanel = newPanel;
        }

        /// <summary>
        /// Pops the stashed panel back in after the interrupting panel has been dismissed.
        /// Also resumes the simulation if this manager was the one that paused it.
        /// </summary>
        private void RestoreStashedPanel()
        {
            _currentPanel = ActivePanel.None;

            if (_stashedPanel != ActivePanel.None)
            {
                ActivePanel toRestore = _stashedPanel;
                _stashedPanel = ActivePanel.None;

                ref var cts = ref GetPanelCts(toRestore);
                PopIn(GetPanelObject(toRestore), ref cts);
                _currentPanel = toRestore;
            }
            else
            {
                // No panel was stashed — speed alert can slide back to default if it's at anchor
                SlideSpeedAlertBackIfNeeded();
            }

            if (_wasSimPausedByPanel)
            {
                _wasSimPausedByPanel = false;
                SimulationManager.Instance?.ResumeSimulation();
            }
        }

        /// <summary>
        /// Instantly hides both the active panel and any stashed panel with no animation.
        /// Also resumes the simulation if it was paused by a panel interrupt.
        /// </summary>
        private void DismissAllPanelsImmediate()
        {
            if (_currentPanel != ActivePanel.None)
            {
                ref var cts = ref GetPanelCts(_currentPanel);
                SnapHidden(GetPanelObject(_currentPanel), ref cts);
            }

            if (_stashedPanel != ActivePanel.None)
            {
                ref var cts2 = ref GetPanelCts(_stashedPanel);
                SnapHidden(GetPanelObject(_stashedPanel), ref cts2);
            }

            _currentPanel = ActivePanel.None;
            _stashedPanel = ActivePanel.None;

            if (_wasSimPausedByPanel)
            {
                _wasSimPausedByPanel = false;
                SimulationManager.Instance?.ResumeSimulation();
            }
        }

        /// <summary>
        /// Nuclear reset: stops all prompt coroutines, snap-hides all managed panels,
        /// clears all pending callbacks, and resets panel tracking state.
        /// Called on teleport start and at the beginning of every new state prompt.
        /// </summary>
        private void HideAllImmediate()
        {
            if (_promptCoroutine != null)
            {
                StopCoroutine(_promptCoroutine);
                _promptCoroutine = null;
            }

            SnapHidden(botPromptPanel, ref _promptCts);
            SnapHidden(botUIPanel, ref _botUICts);
            SnapHidden(botFollowMePanel, ref _botFollowMeCts);
            SnapHidden(contaminationPanel, ref _contaminationCts);

            // speedAlertPanel is independent of the ActivePanel flow — reset it separately.
            if (_speedAlertSlideCoroutine != null)
            {
                StopCoroutine(_speedAlertSlideCoroutine);
                _speedAlertSlideCoroutine = null;
            }
            SnapHidden(speedAlertPanel, ref _speedAlertCts);
            if (speedAlertPanel != null)
                speedAlertPanel.transform.localPosition = _speedAlertDefaultLocalPosition;

            _promptActive = false;
            _pendingUIContent = null;
            _pendingOnComplete = null;
            _uiOnComplete = null;
            _currentPanel = ActivePanel.None;
            _stashedPanel = ActivePanel.None;

            DisableBotButton();
        }


        // ═════════════════════════════════════════════
        // STATE ENTRY POINT
        // ═════════════════════════════════════════════

        /// <summary>
        /// Main entry point called by the SimulationManager when a new state begins.
        /// Extracts prompt data from the state and starts the prompt flow.
        /// </summary>
        public async Task EvaluateStateInteractions(SimulationState state)
        {
            if (state == null) return;

            if (state.teleportOnStart)
            {
                await WaitForActionAsync();
            }

            StartPrompt(state.promptText, state.promptAudio, state.MoveToNextStepAfterAudio);

        }

        private static Task WaitForActionAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            void Handler()
            {
                TeleportManager.TeleportCompleted -= Handler; // Unsubscribe after first call
                tcs.SetResult(true);
            }

            TeleportManager.TeleportCompleted += Handler;
            return tcs.Task;
        }

        // ═════════════════════════════════════════════
        // PROMPT
        // ═════════════════════════════════════════════

        #region Prompt

        /// <summary>
        /// Resets all panels, shows the prompt panel with the given text, plays the audio clip,
        /// then auto-hides after the clip duration (or immediately if no clip).
        /// </summary>
        private void StartPrompt(string text, AudioClip audio, bool moveToNextState)
        {
            HideAllImmediate();

            _promptActive = true;
            _currentPanel = ActivePanel.Prompt;

            bool isAssessment = SimulationManager.Instance != null &&
                                SimulationManager.Instance.simulationMode == SimulationMode.Assessment;

            if (!isAssessment)
            {
                if (botPromptText != null) botPromptText.text = text;
                ForceRebuildLayout(botPromptPanel);
                PopIn(botPromptPanel, ref _promptCts);

                var audioSrc = SoundManager.Instance.promptSource;
                float duration = 0f;

                botController?.Summon();
                botController?.OnPromptShown();

                if (audio != null && audioSrc != null)
                {
                    audioSrc.Stop();
                    audioSrc.clip = audio;
                    audioSrc.clip.LoadAudioData();
                    audioSrc.Play();
                    duration = audio.length;
                }

                _promptCoroutine = StartCoroutine(HidePromptAfter(duration, moveToNextState));
            }
            else
            {
                bool playAsGuided = false;
                var simState = SimulationManager.Instance?.currentState;
                if (simState != null)
                {
                    bool onlyUI = simState.listOfInteractions != null &&
                                  simState.listOfInteractions.Count == 1 &&
                                  simState.listOfInteractions[0] is UIInteraction;

                    bool isPromptOnly = simState.MoveToNextStepAfterAudio;

                    playAsGuided = onlyUI || isPromptOnly;
                }

                if (playAsGuided)
                {
                    if (botPromptText != null) botPromptText.text = text;
                    ForceRebuildLayout(botPromptPanel);
                    PopIn(botPromptPanel, ref _promptCts);

                    var audioSrc = SoundManager.Instance.promptSource;
                    float duration = 0f;

                    botController?.Summon();
                    botController?.OnPromptShown();

                    if (audio != null && audioSrc != null)
                    {
                        audioSrc.Stop();
                        audioSrc.clip = audio;
                        audioSrc.clip.LoadAudioData();
                        audioSrc.Play();
                        duration = audio.length;
                    }

                    _promptCoroutine = StartCoroutine(HidePromptAfter(duration, moveToNextState));
                }
                else
                {
                    _promptCoroutine = StartCoroutine(HidePromptAfter(0f, moveToNextState));
                }
            }
        }

        public void ShowHintPrompt(SimulationState state)
        {
            if (state == null) return;

            _promptActive = true;

            // ── Contamination: interrupt it so prompt takes over cleanly ──────
            // If contamination panel is currently active, pop it out and stash it
            // so the prompt panel doesn't overlap with it.
            if (_currentPanel == ActivePanel.Contamination)
            {
                PopOut(contaminationPanel, ref _contaminationCts);
                _currentPanel = ActivePanel.None;
            }

            // ── Speed alert: slide to anchor if visible ────────────────────────
            // speedAlertPanel is independent of the state machine — slide it to
            // the anchor offset so it doesn't overlap with the hint prompt.
            if (speedAlertPanel != null && speedAlertPanel.activeSelf)
            {
                if (_speedAlertSlideCoroutine != null)
                {
                    StopCoroutine(_speedAlertSlideCoroutine);
                    _speedAlertSlideCoroutine = null;
                }
                _speedAlertSlideCoroutine = StartCoroutine(SlideSpeedAlertToAnchor());
            }

            if (botPromptText != null) botPromptText.text = state.promptText;
            ForceRebuildLayout(botPromptPanel);
            PopIn(botPromptPanel, ref _promptCts);

            botController?.Summon();
            botController?.OnPromptShown();

            float duration = 0f;
            var audioSrc = SoundManager.Instance?.promptSource;
            if (state.promptAudio != null && audioSrc != null)
            {
                audioSrc.Stop();
                audioSrc.clip = state.promptAudio;
                audioSrc.clip.LoadAudioData();
                audioSrc.Play();
                duration = state.promptAudio.length;
            }

            StartCoroutine(HidePromptAfter(duration, false));
        }

        /// <summary>
        /// Slides the speedAlertPanel from its current position to the anchor position.
        /// Used when hint prompt needs to show without overlapping the speed alert.
        /// </summary>
        private IEnumerator SlideSpeedAlertToAnchor()
        {
            if (speedAlertPanel == null || speedAlertAnchor == null) yield break;

            Vector3 startPos = speedAlertPanel.transform.localPosition;
            Vector3 endPos = speedAlertAnchor.localPosition;
            float elapsed = 0f;

            while (elapsed < speedAlertMoveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / speedAlertMoveDuration);
                float smoothT = t * t * (3f - 2f * t);
                speedAlertPanel.transform.localPosition = Vector3.Lerp(startPos, endPos, smoothT);
                yield return null;
            }

            speedAlertPanel.transform.localPosition = endPos;
            _speedAlertSlideCoroutine = null;
        }

        /// <summary>
        /// Coroutine that waits for the prompt audio to finish, then hides the prompt panel.
        /// If <paramref name="moveToNextState"/> is true, advances the simulation automatically.
        /// Otherwise flushes any pending BotUI that was queued while the prompt was active.
        /// </summary>
        private IEnumerator HidePromptAfter(float delay, bool moveToNextState)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            else yield return null;

            Debug.Log("[AssistantManager]" + "Prompt Completed");
            PromptCompleted?.Invoke();

            _promptActive = false;
            PopOut(botPromptPanel, ref _promptCts);
            SlideSpeedAlertBackIfNeeded();
            _promptCoroutine = null;

            if (_currentPanel == ActivePanel.Prompt)
                _currentPanel = ActivePanel.None;

            if (moveToNextState)
            {
                SimulationManager.Instance?.currentState?.onStateComplete?.Invoke();
                Debug.Log("[AssistantManager]" + "MoveToNextState Invoked");

                //SimulationManager.Instance?.MoveToNextState();

                #region For Familiarization
                // AFTER:
                var simState = SimulationManager.Instance?.currentState;
                if (simState != null)
                    SimulationManager.Instance.NotifyStateComplete(simState);
                else
                    SimulationManager.Instance?.MoveToNextState(); 
                #endregion

                yield break;
            }

            if (_pendingUIContent != null)
            {
                string content = _pendingUIContent;
                UnityAction callback = _pendingOnComplete;
                _pendingUIContent = null;
                _pendingOnComplete = null;
                ShowBotUI(content, callback);
            }
        }

        /// <summary>
        /// Called when BotSummonedWithKey fires (X button press).
        /// Stashes whatever panel is currently active (if any) via InterruptCurrentPanel,
        /// then pops in the prompt panel as the new active panel.
        /// Normal state-start prompts (StartPrompt) do NOT go through this path —
        /// they call PopIn directly and are unaffected.
        /// </summary>
        private void OnBotSummonedWithKey()
        {
            // InterruptCurrentPanel pops out and stashes whatever is currently active,
            // then sets _currentPanel = Prompt. If nothing is active it is a no-op on the stash.
            InterruptCurrentPanel(ActivePanel.Prompt);
            PopIn(botPromptPanel, ref _promptCts);
        }

        /// <summary>
        /// Called when BotDismissedWithKey fires (X button press).
        /// Pops out the prompt panel, then restores whatever panel was stashed on summon.
        /// If nothing was stashed, RestoreStashedPanel is a no-op.
        /// </summary>
        private void OnBotDismissedWithKey()
        {
            PopOut(botPromptPanel, ref _promptCts);

            if (_currentPanel == ActivePanel.Prompt)
            {
                _currentPanel = ActivePanel.None;
                // Restores the panel that was active before X was pressed.
                // If nothing was stashed this does nothing.
                RestoreStashedPanel();
            }
        }

        #endregion

        // ─────────────────────────────────────────────
        // LEGACY PROMPT API
        // ─────────────────────────────────────────────

        #region Prompt API

        public void ShowPrompt(string message)
        {
            prompt?.SetPrompt(message);
            botController?.OnPromptShown();
        }

        public void HidePrompt() => prompt?.HidePrompt();

        #endregion


        // ═════════════════════════════════════════════
        // BOT UI
        // ═════════════════════════════════════════════

        #region BotUI

        /// <summary>
        /// Public entry point for showing the BotUI panel.
        /// If a prompt is still active the request is queued and flushed automatically once the prompt closes.
        /// </summary>
        public void TriggerBotUI(string text, UnityAction onComplete)
        {
            if (botController == null) return;

            if (_promptActive)
            {
                _pendingUIContent = text;
                _pendingOnComplete = onComplete;
                return;
            }

            ShowBotUI(text, onComplete);
        }

        /// <summary>Interrupts any current panel, pops in the BotUI panel, and wires the dismiss button.</summary>
        private void ShowBotUI(string text, UnityAction onComplete)
        {
            InterruptCurrentPanel(ActivePanel.BotUI);

            _uiOnComplete = onComplete;

            if (botUIText != null)
            {
                botUIPanel.SetActive(true);
                botUIText.text = text;
                botUIText.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                botUIPanel.SetActive(false);

            }
            // Force layout rebuild so ContentSizeFitter settles before the tween starts.
            ForceRebuildLayout(botUIPanel);
            PopIn(botUIPanel, ref _botUICts);
            _currentPanel = ActivePanel.BotUI;

            WireBotButton(() =>
            {
                HideBotUI();
                _uiOnComplete?.Invoke();
                _uiOnComplete = null;
            });

            botController?.Summon();
            botController?.OnUIOpen();
        }

        /// <summary>Pops out the BotUI panel and restores any previously stashed panel.</summary>
        public void HideBotUI()
        {
            PopOut(botUIPanel, ref _botUICts);
            DisableBotButton();
            botController?.OnUIClose();
            botController?.Dismiss();

            if (_currentPanel == ActivePanel.BotUI)
            {
                _currentPanel = ActivePanel.None;
                RestoreStashedPanel();
            }
        }

        #endregion

        // ═════════════════════════════════════════════
        // Simulation Progress
        // ═════════════════════════════════════════════
        // Progress values are always written to the Slider/Text components
        // even when the panel is disabled. Unity stores the values in the
        // component — when the panel is enabled + popped in, it shows
        // the correct value immediately with no flash of stale data.

        #region Simulation Progress

        public void ShowBotProgress(float progress)
        {
            _progressTarget = Mathf.Clamp01(progress);
            int stepIndex = GetCurrentStepIndex();
            Debug.Log($"[Progress] progress={_progressTarget} stepIndex={stepIndex} currentIndex={SimulationManager.Instance?.currentState?.name}");
            if (progressTrackers != null)
                foreach (var t in progressTrackers)
                    if (t != null) t.UpdateValues(_progressTarget, stepIndex);

            if (_progressCoroutine != null) StopCoroutine(_progressCoroutine);
            _progressCoroutine = StartCoroutine(AnimateProgress(_progressTarget, stepIndex));
        }

        public void UpdateProgressSilent(float progress)
        {
            _progressTarget = Mathf.Clamp01(progress);
            int stepIndex = GetCurrentStepIndex();
            UpdateProgressInternal(_progressTarget, stepIndex);
        }

        public void SetProgressFromSimulation()
        {
            if (SimulationManager.Instance == null) return;

            var states = SimulationManager.Instance.states;
            var current = SimulationManager.Instance.currentState;
            if (states == null || states.Count == 0 || current == null) return;

            int index = states.IndexOf(current.gameObject);
            if (index < 0) return;

            float progress = (float)(index + 1) / states.Count;
            ShowBotProgress(progress);
        }

        public void HideBotProgress()
        {
            if (_progressCoroutine != null)
            {
                StopCoroutine(_progressCoroutine);
                _progressCoroutine = null;
            }
        }

        private int GetCurrentStepIndex()
        {
            if (SimulationManager.Instance == null) return 1;
            var states = SimulationManager.Instance.states;
            var current = SimulationManager.Instance.currentState;
            if (states == null || states.Count == 0 || current == null) return 1;
            int index = states.IndexOf(current.gameObject);
            return index < 0 ? 1 : index + 1;
        }

        private void UpdateProgressInternal(float progress, int stepIndex)
        {
            if (progressTrackers == null) return;
            foreach (var t in progressTrackers)
                if (t != null) t.UpdateValues(progress, stepIndex);
        }

        private IEnumerator AnimateProgress(float target, int stepIndex)
        {
            if (progressTrackers == null || progressTrackers.Length == 0) yield break;

            float[] starts = new float[progressTrackers.Length];
            for (int i = 0; i < progressTrackers.Length; i++)
                starts[i] = progressTrackers[i] != null ? progressTrackers[i].GetSliderValue() : 0f;

            float elapsed = 0f;
            while (elapsed < progressAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / progressAnimDuration);
                float eased = 1f - (1f - t) * (1f - t);
                for (int i = 0; i < progressTrackers.Length; i++)
                    if (progressTrackers[i] != null)
                        progressTrackers[i].SetSliderValue(Mathf.Lerp(starts[i], target, eased));
                yield return null;
            }

            // Snap to final value and re-apply text (guards against first-step text being stale)
            for (int i = 0; i < progressTrackers.Length; i++)
                if (progressTrackers[i] != null)
                    progressTrackers[i].SnapToFinal(target);

            _progressCoroutine = null;
        }

        #endregion


        // ═════════════════════════════════════════════
        // BOT FOLLOW ME PANEL
        // ═════════════════════════════════════════════

        #region BotFollowMe

        /// <summary>
        /// Pops in the follow-me panel through the full interrupt/stash flow.
        /// Intended to be called when the bot enters guide mode to inform the user to follow it.
        /// </summary>
        public void ShowBotFollowMe()
        {
            InterruptCurrentPanel(ActivePanel.BotFollowMe);
            PopIn(botFollowMePanel, ref _botFollowMeCts);
            _currentPanel = ActivePanel.BotFollowMe;
        }

        /// <summary>Pops out the follow-me panel and restores any previously stashed panel.</summary>
        public void HideBotFollowMe()
        {
            PopOut(botFollowMePanel, ref _botFollowMeCts);

            if (_currentPanel == ActivePanel.BotFollowMe)
            {
                _currentPanel = ActivePanel.None;
                RestoreStashedPanel();
            }
        }

        #endregion


        // ═════════════════════════════════════════════
        // HELP PANEL
        // ═════════════════════════════════════════════

        #region Help Panel

        private void AddListnersForHelpButtonPanel()
        {
            if (helpButton == null)
            {
                Debug.LogError("[AssistantManager] helpButton is null — help listeners not wired.");
                return;
            }
            if (helpCloseButton == null)
            {
                Debug.LogError("[AssistantManager] helpCloseButton is null — help close listeners not wired.");
                return;
            }

            helpButton.OnButtonClicked.AddListener(() =>
            {
                Debug.Log("[AssistantManager] Help button clicked.");
                // Pop out the assist panel directly — do NOT use InterruptCurrentPanel here
                // because we don't want to overwrite _stashedPanel. The original panel
                // (e.g. BotUI) is already stashed from when Y was pressed; we preserve that.
                PopOut(assistPanel, ref _assistCts);
                _currentPanel = ActivePanel.None;

                // Pop in help as the new active panel. No stash involved — assist is gone.
                PopIn(helpPanel, ref _helpCts);
                _isHelpVisible = true;
                _currentPanel = ActivePanel.Help;
                botController.OnHelpCalled();
                HelpEnabled?.Invoke();
            });

            helpCloseButton.OnButtonClicked.AddListener(() =>
            {
                Debug.Log("[AssistantManager] Help close button clicked.");
                // Pop out help, then pop assist back in directly.
                // Again, do NOT call RestoreStashedPanel — the original stashed panel
                // (e.g. BotUI) must stay stashed until Y is pressed or guide is chosen.
                PopOut(helpPanel, ref _helpCts);
                _isHelpVisible = false;

                _currentPanel = ActivePanel.None;

                PopIn(assistPanel, ref _assistCts);
                _currentPanel = ActivePanel.Assist;
                HelpDisabled?.Invoke();
            });
        }

        public void ShowHelp()
        {
            Debug.Log($"[AssistantManager] ShowHelp — helpPanel null? {helpPanel == null}, " +
                      $"cached scale: {(helpPanel != null ? GetCachedScale(helpPanel).ToString() : "N/A")}");
            if (helpPanel == null) return;

            InterruptCurrentPanel(ActivePanel.Help);
            PopIn(helpPanel, ref _helpCts);
            _isHelpVisible = true;
            _currentPanel = ActivePanel.Help;
            botController.OnHelpCalled();
        }

        public void HideHelp()
        {
            if (helpPanel == null) return;

            PopOut(helpPanel, ref _helpCts);
            _isHelpVisible = false;

            if (_currentPanel == ActivePanel.Help)
            {
                _currentPanel = ActivePanel.None;
                RestoreStashedPanel();
            }
        }

        public void ToggleHelp() { if (_isHelpVisible) HideHelp(); else ShowHelp(); }

        #endregion


        // ═════════════════════════════════════════════
        // ASSIST PANEL — Y BUTTON
        // ═════════════════════════════════════════════

        #region Assist Panel

        /// <summary>
        /// Y button pressed.
        /// - If assist or help is currently visible: close whichever is showing,
        ///   restore any stashed panel, and fully reset the assist flow.
        /// - Otherwise: stash the current active panel (if any) and pop in the assist panel.
        /// </summary>
        private void OnYButtonPressed(InputAction.CallbackContext context)
        {
            Debug.Log("Pressed Y Button");

            if (_currentPanel == ActivePanel.Assist)
            {
                Debug.Log("[AssistantManager] Y pressed while Assist panel is active — closing Assist.");
                HelpDisabled?.Invoke();
                // Assist is open — close it and restore the stashed panel (if any).
                PopOut(assistPanel, ref _assistCts);
                _currentPanel = ActivePanel.None;
                RestoreStashedPanel();

            }
            else if (_currentPanel == ActivePanel.Help)
            {
                Debug.Log("[AssistantManager] Y pressed while Help panel is active — closing Help and Assist, restoring stashed panel.");
                // Help is open (reached from assist) — close help and restore the original
                // stashed panel. This fully resets the assist flow so Y opens fresh next time.
                PopOut(helpPanel, ref _helpCts);
                _isHelpVisible = false;
                _currentPanel = ActivePanel.None;
                RestoreStashedPanel();
                HelpDisabled?.Invoke();
            }
            else
            {
                Debug.Log("[AssistantManager] Y pressed with no Assist/Help visible — opening Assist.");
                HelpDisabled?.Invoke();
                // No assist/help visible — stash whatever is currently active and open assist.
                InterruptCurrentPanel(ActivePanel.Assist);
                PopIn(assistPanel, ref _assistCts);

            }
        }

        /// <summary>
        /// Called by the Guide button inside the assist panel.
        /// Closes the assist panel and restores the stashed panel (if any).
        /// No assist/help re-opening — guidance takes over.
        /// </summary>
        public void OnGuidButtonPressedInAssistPanel()
        {
            if (_currentPanel == ActivePanel.Assist)
            {
                PopOut(assistPanel, ref _assistCts);
                _currentPanel = ActivePanel.None;
                RestoreStashedPanel();
            }
        }

        private async void PopInAssistPanel()
        {
            /*if (assistPanel == null) return;
            isAssistantPanelVisible = true;

            _assistCts?.Cancel();
            _assistCts = new CancellationTokenSource();

            Vector3 targetScale = GetCachedScale(assistPanel);
            assistPanel.transform.localScale = Vector3.zero;

            // SetActive(true) triggers the full Canvas layout rebuild cycle.
            assistPanel.SetActive(true);

            // Force immediate rebuild so layout settles before tween starts.
            ForceRebuildLayout(assistPanel);

            await assistPanel.transform.DoScale(targetScale, popDuration, popInEase, _assistCts.Token);

            // Enable colliders only after fully scaled up.
            assistPanel.GetComponent<PanelButtonController>()?.SetCollidersEnabled(true);*/

            if (assistPanel == null) return;
            isAssistantPanelVisible = true;

            _assistCts?.Cancel();
            _assistCts = new CancellationTokenSource();

            var cG = assistPanel.GetComponent<CanvasGroup>();
            cG.alpha = 0f;

            // SetActive(true) triggers the full Canvas layout rebuild cycle.
            assistPanel.SetActive(true);

            // Force immediate rebuild so layout settles before tween starts.
            ForceRebuildLayout(assistPanel);

            await cG.DoFade(1f, popDuration, popInEase, _assistCts.Token);

            // Enable colliders only after fully scaled up.
            assistPanel.GetComponent<PanelButtonController>()?.SetCollidersEnabled(true);
        }

        private async void PopOutAssistPanel()
        {
            if (assistPanel == null) return;
            if (!assistPanel.activeSelf) { isAssistantPanelVisible = false; return; }

            isAssistantPanelVisible = false;
            // Disable colliders immediately — panel is shrinking.
            assistPanel.GetComponent<PanelButtonController>()?.SetCollidersEnabled(false);

            _assistCts?.Cancel();
            _assistCts = new CancellationTokenSource();

            await assistPanel.GetComponent<CanvasGroup>().DoFade(0, 0.1f, popOutEase, _assistCts.Token,
                onComplete: () =>
                {
                    // Disable after tween so next PopIn gets a full layout rebuild cycle.
                    if (assistPanel != null) assistPanel.SetActive(false);
                });
        }

        /// <summary>For external callers that need to set visibility directly.</summary>
        private void ToggleVisiblityOfAssistantPanel(bool isVisible)
        {
            if (isVisible)
                PopInAssistPanel();
            else
                PopOutAssistPanel();
        }

        #endregion


        // ═════════════════════════════════════════════
        // PATHFINDING
        // ═════════════════════════════════════════════

        #region Pathfinding

        private void AddListnersPathfinding()
        {
            pathfindingButton.OnButtonClicked.AddListener(() =>
            {
                StartCoroutine(WaitAndStartGuidance());
            });

            BotGuideBehaviour.GuideFinished += HideBotFollowMe;
        }

        private void RemoveListnersPathfinding()
        {
            pathfindingButton.OnButtonClicked.RemoveListener(() =>
            {
                StartCoroutine(WaitAndStartGuidance());
            });

            BotGuideBehaviour.GuideFinished -= HideBotFollowMe;
        }

        private IEnumerator WaitAndStartGuidance()
        {
            // ── GUARD: only supported interaction types can be guided ──
            if (!CanGuide())
            {
                PopOut(assistPanel, ref _assistCts);        // Dismiss assist panel before showing feedback
                _currentPanel = ActivePanel.None;

                ShowGuideUnavailablePanel();
                yield break;                 // Bot stays in Companion mode
            }

            // Close help first if open so bot exits HelpCalled state
            if (_isHelpVisible)
                HideHelp();

            // Dismiss all managed panels immediately — clean slate before guidance
            DismissAllPanelsImmediate();

            // Pop out the assist panel too
            PopOutAssistPanel();

            yield return new WaitForSeconds(1f);

            botController.SwitchMode(BotController.BotMode.Guide);

            ShowBotFollowMe();

            StartGuidance();
        }

        /// <summary>
        /// Returns true only when the current interaction is a type that supports guidance
        /// (GrabInteraction, DetectInteraction, or GazeInteraction).
        /// </summary>
        private bool CanGuide()
        {
            Interactions currentInteraction = SimulationManager.Instance?.currentState?.currentInteraction;
            return currentInteraction is GrabInteraction
                || currentInteraction is DetectInteraction
                || currentInteraction is GazeInteraction;
        }

        /// <summary>
        /// Pops in the guide-unavailable panel independently (no stash/interrupt),
        /// then auto-dismisses it after <see cref="guideUnavailableDuration"/> seconds.
        /// Re-triggering while already visible cancels the previous auto-dismiss timer
        /// and restarts it cleanly.
        /// </summary>
        private void ShowGuideUnavailablePanel()
        {
            if (guideUnavailablePanel == null) return;

            // Cancel any in-flight auto-dismiss
            if (_guideUnavailableCoroutine != null)
            {
                StopCoroutine(_guideUnavailableCoroutine);
                _guideUnavailableCoroutine = null;
            }

            PopIn(guideUnavailablePanel, ref _guideUnavailableCts);
            _guideUnavailableCoroutine = StartCoroutine(HideGuideUnavailableAfter(guideUnavailableDuration));
        }

        private IEnumerator HideGuideUnavailableAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            PopOut(guideUnavailablePanel, ref _guideUnavailableCts);
            _guideUnavailableCoroutine = null;
            RestoreStashedPanel();
        }

        private void StartGuidance()
        {
            Debug.Log("[Assistant Manager]" + "Starting guidance...");
            Interactions currentInteraction = SimulationManager.Instance.currentState.currentInteraction;
            Debug.Log("[Assistant Manager]" + $"Current interaction: {currentInteraction?.name} ({currentInteraction?.GetType().Name})");

            if (currentInteraction is GrabInteraction)
            {
                if (GrabManager.Instance.leftPinchDetector.currentGrab == null &&
                    GrabManager.Instance.rightPinchDetector.currentGrab == null)
                {
                    botController.guideBehaviour.GuideTo(currentInteraction.transform.position);
                }
            }
            else if (currentInteraction is DetectInteraction)
            {
                if (currentInteraction.GetComponent<DetectInteraction>().ObjectsToBeDetectedList[0]
                    .GetComponent<GrabInteraction>() != null)
                {
                    if (GrabManager.Instance.leftPinchDetector.currentGrab == null &&
                        GrabManager.Instance.rightPinchDetector.currentGrab == null)
                    {
                        botController.guideBehaviour.GuideTo(currentInteraction.GetComponent<DetectInteraction>().
                            ObjectsToBeDetectedList[0].transform.position);
                    }
                    else
                    {
                        botController.guideBehaviour.GuideTo(currentInteraction.transform.position);
                    }
                }
                else
                {
                    botController.guideBehaviour.GuideTo(currentInteraction.transform.position);
                }
            }
            else if (currentInteraction is GazeInteraction)
            {
                botController.guideBehaviour.GuideTo(currentInteraction.transform.position);
            }
        }

        #endregion


        // ═════════════════════════════════════════════
        // BOT DISCARD
        // ═════════════════════════════════════════════

        #region BotDiscard

        /// <summary>Enqueues an object to be dissolved-out, teleported to destination, and dissolved-in.</summary>
        public void BotDiscard(GameObject obj, Transform destination)
        {
            if (obj == null) return;
            _discardQueue.Enqueue((obj, destination));

            if (!_discardRunning)
                StartCoroutine(RunDiscardQueue());
        }

        private IEnumerator RunDiscardQueue()
        {
            _discardRunning = true;
            botController?.Summon();
            botController?.OnDiscard();

            while (_discardQueue.Count > 0)
            {
                var (obj, dest) = _discardQueue.Dequeue();
                if (obj == null) continue;
                if (dest != null)
                    yield return StartCoroutine(MoveObjectToDestination(obj, dest));
            }

            botController?.OnDiscardComplete();
            _discardRunning = false;
        }

        private IEnumerator MoveObjectToDestination(GameObject obj, Transform dest)
        {
            if (obj == null) yield break;

            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            var dissolve = obj.GetComponent<DissolveController>();
            if (dissolve == null) dissolve = obj.AddComponent<DissolveController>();

            bool dissolvedOut = false;
            dissolve.DissolveOut(discardMoveDuration * 0.5f, () => dissolvedOut = true);
            yield return new WaitUntil(() => dissolvedOut || obj == null);
            if (obj == null) yield break;

            obj.transform.position = dest.position;
            obj.transform.rotation = dest.rotation;

            bool dissolvedIn = false;
            dissolve.DissolveIn(discardMoveDuration * 0.5f, () => dissolvedIn = true);
            yield return new WaitUntil(() => dissolvedIn || obj == null);
            if (obj == null) yield break;

            if (rb != null) rb.isKinematic = false;
        }

        #endregion


        // ═════════════════════════════════════════════
        // BUTTON HELPERS
        // ═════════════════════════════════════════════

        /// <summary>Clears all listeners on the bot button, wires the given action, and enables the button.</summary>
        private void WireBotButton(UnityAction action)
        {
            if (botButton == null) return;
            botButton.OnButtonClicked.RemoveAllListeners();
            botButton.OnButtonClicked.AddListener(action);
            //botButton.gameObject.SetActive(true);
        }

        /// <summary>Clears all listeners on the bot button and disables it.</summary>
        private void DisableBotButton()
        {
            if (botButton == null) return;
            botButton.OnButtonClicked.RemoveAllListeners();
            //botButton.gameObject.SetActive(false);
        }


        // ═════════════════════════════════════════════
        // SETTINGS
        // ═════════════════════════════════════════════

        public void OpenSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
                _isSettingsVisible = true;
            }
        }

        public void CloseSettings()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
                _isSettingsVisible = false;
            }
        }


        // ═════════════════════════════════════════════
        // TELEPORT EVENTS
        // ═════════════════════════════════════════════

        #region TeleportEventFunctions

        private void OnTeleoprtStarted() => HideAllImmediate();
        private void OnTeleportCompleted()
        { }


        #endregion


        // ═════════════════════════════════════════════
        // CONTAMINATION
        // ═════════════════════════════════════════════

        #region Contamination

        private void AddListnersContamination()
        {
            contaminationManager.onContaminationTriggered.AddListener(OnContaminationTriggered);
            contaminationManager.onContaminationResolved.AddListener(OnContaminationResolved);
        }

        private void RemoveListnersContamination()
        {
            contaminationManager.onContaminationTriggered.RemoveListener(OnContaminationTriggered);
            contaminationManager.onContaminationResolved.RemoveListener(OnContaminationResolved);
        }

        /// <summary>
        /// Called by ContaminationManager when contamination is triggered.
        /// Stops bot audio, clears all panels, pauses the current state, then pops in the
        /// contamination panel through the full interrupt/stash flow.
        /// </summary>
        private void OnContaminationTriggered(String areaName)
        {
            botController.audioSource.Stop();

            // HideAllImmediate resets _currentPanel to None, so InterruptCurrentPanel
            // below starts from a clean slate and sets _currentPanel = Contamination.
            HideAllImmediate();
            SimulationManager.Instance.PauseCurrentState();

            InterruptCurrentPanel(ActivePanel.Contamination);
            PopIn(contaminationPanel, ref _contaminationCts);
            _currentPanel = ActivePanel.Contamination;
        }

        /// <summary>
        /// Called by ContaminationManager when contamination is resolved.
        /// Pops out the contamination panel, restores any stashed panel, then restarts the state.
        /// </summary>
        private void OnContaminationResolved()
        {
            PopOut(contaminationPanel, ref _contaminationCts);

            if (_currentPanel == ActivePanel.Contamination)
            {
                _currentPanel = ActivePanel.None;
                RestoreStashedPanel();
            }

            SimulationManager.Instance.RestartCurrentState();
        }

        /// <summary>Directly sets the contamination panel's visibility without animation. Kept for external use.</summary>
        private void SetContaminationPanelVisiblity(bool isVisible)
        {
            if (contaminationPanel == null) return;
            contaminationPanel.SetActive(isVisible);
            contaminationPanel.GetComponent<PanelButtonController>()?.SetCollidersEnabled(isVisible);
        }

        #endregion


        // ═════════════════════════════════════════════
        // HAND SPEED TRACKING
        // ═════════════════════════════════════════════
        //
        // speedAlertPanel is FULLY INDEPENDENT of the ActivePanel state machine.
        // It never reads or writes _currentPanel, never stashes anything, and never
        // interrupts any other panel. It simply overlays on top of whatever is showing.
        //
        // Mode A (no active panel): pop in at default local position.
        // Mode B (another panel active): activate at anchor local position, slide to default.
        // Resolve (both modes): pop out from default local position.

        #region HandSpeedTracking

        private void AddListnersHandSpeedTracking()
        {
            vrHandSpeedTracker.OnSpeedViolationDetected.AddListener(OnSpeedViolationDetected);
            vrHandSpeedTracker.OnSpeedViolationResolved.AddListener(OnSpeedViolationResolved);
        }

        private void RemoveListnersHandSpeedTracking()
        {
            vrHandSpeedTracker.OnSpeedViolationDetected.RemoveListener(OnSpeedViolationDetected);
            vrHandSpeedTracker.OnSpeedViolationResolved.RemoveListener(OnSpeedViolationResolved);
        }

        /// <summary>
        /// Called by VRHandSpeedTracker when a speed violation starts.
        /// Never touches _currentPanel — the existing panel flow is completely unaffected.
        /// Mode A: panel pops in at its default local position.
        /// Mode B: panel activates at the anchor local position and slides to default.
        /// </summary>
        private void OnSpeedViolationDetected()
        {
            if (speedAlertPanel == null) return;

            // Cancel any leftover slide from a previous violation
            if (_speedAlertSlideCoroutine != null)
            {
                StopCoroutine(_speedAlertSlideCoroutine);
                _speedAlertSlideCoroutine = null;
            }

            // Cancel any in-flight pop tween
            _speedAlertCts?.Cancel();
            _speedAlertCts = new CancellationTokenSource();

            // Always start from scale zero
            speedAlertPanel.transform.localScale = Vector3.zero;

            // Check both _currentPanel AND _promptActive since hint prompt
            // doesn't participate in the state machine but still occupies the screen
            bool anyPanelActive = _currentPanel != ActivePanel.None || _promptActive;

            if (!anyPanelActive)
            {
                // MODE A — no other panel active: show at default local position.
                speedAlertPanel.transform.localPosition = _speedAlertDefaultLocalPosition;
                speedAlertPanel.SetActive(true);
            }
            else
            {
                // MODE B — another panel is active: position at anchor.
                // The existing panel is left completely untouched.
                speedAlertPanel.transform.localPosition = speedAlertAnchor != null ? speedAlertAnchor.localPosition
                    : _speedAlertDefaultLocalPosition;

                speedAlertPanel.SetActive(true);
            }

            // Pop in — same scale animation regardless of mode
            _ = speedAlertPanel.transform.DoScale(
                    GetCachedScale(speedAlertPanel), popDuration, popInEase, _speedAlertCts.Token);
        }

        /// <summary>
        /// Called by VRHandSpeedTracker when the speed violation ends.
        /// By now the panel is always at its default local position (Mode A: never moved;
        /// Mode B: slide moved it there). Pops out — existing panel flow is unaffected.
        /// </summary>
        private void OnSpeedViolationResolved()
        {
            if (speedAlertPanel == null) return;

            // If the slide is still running (violation resolved before slide finished),
            // stop it and snap to default so the pop-out starts from the right place.
            if (_speedAlertSlideCoroutine != null)
            {
                StopCoroutine(_speedAlertSlideCoroutine);
                _speedAlertSlideCoroutine = null;
                speedAlertPanel.transform.localPosition = _speedAlertDefaultLocalPosition;
            }

            // Pop out — independent of any other panel.
            _speedAlertCts?.Cancel();
            _speedAlertCts = new CancellationTokenSource();

            _ = speedAlertPanel.transform.DoScale(Vector3.zero, popDuration, popOutEase, _speedAlertCts.Token,
                onComplete: () =>
                {
                    if (speedAlertPanel != null) speedAlertPanel.SetActive(false);
                });
        }

        /// <summary>
        /// Smoothly moves the speedAlertPanel from its current local position (the anchor)
        /// to the default local position using smooth-step easing, in parallel with pop-in.
        /// </summary>
        private IEnumerator SlideSpeedAlertToDefault()
        {
            if (speedAlertPanel == null) yield break;

            Vector3 startPos = speedAlertPanel.transform.localPosition;
            Vector3 endPos = _speedAlertDefaultLocalPosition;
            float elapsed = 0f;

            while (elapsed < speedAlertMoveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / speedAlertMoveDuration);
                float smoothT = t * t * (3f - 2f * t); // smooth-step easing

                speedAlertPanel.transform.localPosition = Vector3.Lerp(startPos, endPos, smoothT);
                yield return null;
            }

            // Snap to exact end to avoid float drift
            speedAlertPanel.transform.localPosition = endPos;
            _speedAlertSlideCoroutine = null;
        }

        /// <summary>
        /// If speedAlertPanel is active and not already at default position,
        /// slides it back to the default position. Called whenever the active
        /// panel closes and no other panel takes over.
        /// </summary>
        private void SlideSpeedAlertBackIfNeeded()
        {
            if (speedAlertPanel == null || !speedAlertPanel.activeSelf) return;

            if (_speedAlertSlideCoroutine != null)
            {
                StopCoroutine(_speedAlertSlideCoroutine);
                _speedAlertSlideCoroutine = null;
            }

            _speedAlertSlideCoroutine = StartCoroutine(SlideSpeedAlertToDefault());
        }
    }
}

        #endregion