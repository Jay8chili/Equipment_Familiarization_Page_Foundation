// ════════════════════════════════════════════════════════════════════════════
//  PartsIdentificationManager.cs
// ════════════════════════════════════════════════════════════════════════════

using SimulationSystem.V02.Assistant;
using SimulationSystem.V02.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// ── Station Group data ────────────────────────────────────────────────────

[System.Serializable]
public class StationGroup
{
    [SerializeField]
    [Tooltip("Display name — used only for Inspector clarity and debug logs.")]
    public string stationName;

    [SerializeField]
    [Tooltip("Station ID matching the StationID field on PartStepData (e.g. S01).")]
    public string stationId;

    [SerializeField]
    [Tooltip("Index of the Station intro SimulationState in SimulationManager.states.")]
    public int stationIntroStateIndex;

    [SerializeField]
    [Tooltip("Ordered indices of all Part SimulationStates in SimulationManager.states " +
             "that belong to this station.")]
    public List<int> partStateIndices = new List<int>();

    [SerializeField]
    [Tooltip("The StationIButton in the scene that the user ray-clicks to trigger this station " +
             "in Free Roam mode.")]
    public StationIButton iButton;
}

// ═════════════════════════════════════════════════════════════════════════════

public class PartsIdentificationManager : MonoBehaviour
{
    public static PartsIdentificationManager Instance { get; private set; }

    // ── References ────────────────────────────────────────────────────────
    [Header("References")]
    public SimulationManager simulationManager;

    // ── Familiarization Settings ──────────────────────────────────────────
    [Header("Familiarization Settings")]
    [Tooltip("Drag the AssistantManager botPromptPanel here. Its scale is forced " +
             "to zero on every familiarization step so it never appears.")]
    public GameObject botPromptPanel;

    // ── Free Roam ─────────────────────────────────────────────────────────
    [Header("Free Roam")]
    public int freeRoamStateIndex = 1;

    // ── Station Groups ────────────────────────────────────────────────────
    [Header("Station Groups")]
    public List<StationGroup> stationGroups = new List<StationGroup>();

    // ── Highlight Events ──────────────────────────────────────────────────
    [Header("Highlight Events")]
    [Tooltip("Fired when a step becomes active. Passes the GameObject to highlight.")]
    public UnityEvent<GameObject> OnPartBecameActive;

    [Tooltip("Fired when the active step ends. Passes the GameObject to unhighlight.")]
    public UnityEvent<GameObject> OnPartBecameInactive;

    // ── Skip Button ───────────────────────────────────────────────────────
    [Header("Skip Button")]
    [Tooltip("Assign the skip button GO here. Shown only in Guided mode. " +
             "CustomButton is found automatically from its children at runtime.")]
    public GameObject skipButtonGO;

    [Tooltip("Seconds the skip button stays disabled after being pressed. " +
             "Set to 0 to re-enable immediately after the next state starts.")]
    public float skipCooldownDuration = 3f;

    // ── Internal ──────────────────────────────────────────────────────────
    private StationGroup _activeGroup;
    private int _activeGroupEndIdx = -1;
    private GameObject _activeHighlight;
    private FamiliarizationUIPanel _activePanel;
    private SimulationState _lastKnownState;
    private SimulationState _pendingHighlightState; // waiting for teleport before showing highlight
    private Coroutine _skipCooldownCoroutine;  // re-enables skip button after cooldown
    private Coroutine _videoCoroutine;          // coroutine waiting for video/audio to finish
    private bool _videoIntercepting;       // true when we are controlling advancement for video

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (simulationManager == null)
        {
            Debug.LogError("[PartsIdentificationManager] No SimulationManager assigned.");
            return;
        }

        simulationManager.SimulationStart.AddListener(OnSimulationStart);
        simulationManager.SimulationEnd.AddListener(OnSimulationEnd);
        AssistantManager.PromptCompleted += OnPromptCompleted;

        SetAllIButtonsVisible(false);
        TeleportManager.TeleportCompleted += OnTeleportCompletedForHighlight;

        // Subscribe to onStateStart on every state so PreCheckVideoIntercept
        // fires synchronously when the state starts — before HidePromptAfter runs.
        if (simulationManager != null)
        {
            foreach (var stateGO in simulationManager.states)
            {
                if (stateGO == null) continue;
                var simState = stateGO.GetComponent<SimulationState>();
                if (simState != null)
                    simState.onStateStart.AddListener(() => PreCheckVideoIntercept(simState));
            }
        }

        // Wire CustomButton.OnButtonClicked → SkipToNextStation at runtime
        if (skipButtonGO != null)
        {
            var skipBtn = skipButtonGO.GetComponentInChildren<CustomButton>();
            if (skipBtn != null)
                skipBtn.OnButtonClicked.AddListener(SkipToNextStation);
            else
                Debug.LogWarning("[PartsIdentificationManager] No CustomButton found in Skip Button children.");
        }
    }

    private void OnDestroy()
    {
        if (simulationManager != null)
        {
            simulationManager.SimulationStart.RemoveListener(OnSimulationStart);
            simulationManager.SimulationEnd.RemoveListener(OnSimulationEnd);
        }
        AssistantManager.PromptCompleted -= OnPromptCompleted;
        TeleportManager.TeleportCompleted -= OnTeleportCompletedForHighlight;

        if (skipButtonGO != null)
        {
            var skipBtn = skipButtonGO.GetComponentInChildren<CustomButton>();
            if (skipBtn != null)
                skipBtn.OnButtonClicked.RemoveListener(SkipToNextStation);
        }
    }

    // ── Update — state transition polling ─────────────────────────────────

    private void Update()
    {
        if (simulationManager == null) return;
        SimulationState current = simulationManager.currentState;
        if (current == _lastKnownState) return;
        OnStateChanged(_lastKnownState, current);
        _lastKnownState = current;
    }

    // ─────────────────────────────────────────────────────────────────────
    // STATE CHANGE HANDLER
    // ─────────────────────────────────────────────────────────────────────

    private void OnStateChanged(SimulationState previous, SimulationState next)
    {
        if (_activeHighlight != null)
        {
            OnPartBecameInactive?.Invoke(_activeHighlight);

            var highlight = _activeHighlight.GetComponent<GrabHighlightController>();
            if (highlight != null) highlight.HideHighlight();

            _activeHighlight = null;
        }

        if (next == null) return;

        int currentIndex = simulationManager.states.IndexOf(next.gameObject);

        // ── Guided mode ───────────────────────────────────────────────────
        if (simulationManager.simulationMode == SimulationMode.Guided)
        {
            ShowUIAndHighlight(next);
            return;
        }

        // ── Free Roam mode ────────────────────────────────────────────────

        if (currentIndex == freeRoamStateIndex)
        {
            _activeGroup = null;
            _activeGroupEndIdx = -1;
            SetAllIButtonsVisible(true);
            Debug.Log("[PartsIdentificationManager] Returned to Free Roam parked state — I-buttons shown.");
            return;
        }

        if (_activeGroup != null)
        {
            ShowUIAndHighlight(next);
        }
    }

    /// <summary>
    /// Sets _videoIntercepting immediately on state change — before ShowUIAndHighlight.
    /// Uses VideoClip.length directly from the asset so it works before the
    /// VideoPlayer has loaded the clip at runtime.
    /// </summary>
    private void PreCheckVideoIntercept(SimulationState state)
    {
        if (state == null) { _videoIntercepting = false; return; }

        var partData = state.GetComponent<PartStepData>();
        if (partData == null || partData.videoClip == null)
        {
            _videoIntercepting = false;
            return;
        }

        float audioLength = state.promptAudio != null ? state.promptAudio.length : 0f;
        float videoLength = (float)partData.videoClip.length;

        _videoIntercepting = videoLength > audioLength;

        Debug.Log($"[PartsIdentificationManager] Video check: audio={audioLength:F2}s " +
                  $"video={videoLength:F2}s intercept={_videoIntercepting}");
    }

    // ─────────────────────────────────────────────────────────────────────
    // UI + HIGHLIGHT
    // ─────────────────────────────────────────────────────────────────────

    private void ShowUIAndHighlight(SimulationState state)
    {
        if (state == null) return;

        var partData = state.GetComponent<PartStepData>();
        if (partData == null) return;

        HideActivePanel();

        // ── Kill bot prompt panel scale ───────────────────────────────────
        if (botPromptPanel != null)
            botPromptPanel.transform.localScale = Vector3.zero;

        // ── Teleport ──────────────────────────────────────────────────────
        // SimulationState.StartState returns early for MoveToNextStepAfterAudio
        // states and never fires UpdatePlayerPos. We fire it here instead.
        if (state.teleportOnStart &&
            state.teleportTarget != null &&
            state.MoveToNextStepAfterAudio &&
            TeleportManager.Instance != null)
        {
            if (simulationManager.simulationMode == SimulationMode.FreeRoam)
            {
                // In Free Roam skip actual teleport — invoke TeleportCompleted
                // directly so WaitForActionAsync unblocks and audio plays.
                // No player movement occurs.
                TeleportManager.TeleportCompleted?.Invoke();
            }
            else
            {
                // Guided — teleport the player and defer UI + highlight
                _pendingHighlightState = state;
                TeleportManager.Instance.UpdatePlayerPos(state.teleportTarget);
                return; // UI + highlight fired in OnTeleportCompletedForHighlight
            }
        }

        // ── Show UI and highlight immediately (no teleport) ───────────────
        ShowPanelAndHighlight(state);
    }

    private void ShowPanelAndHighlight(SimulationState state)
    {
        if (state == null) return;
        var partData = state.GetComponent<PartStepData>();
        if (partData == null) return;

        // UI panel
        if (partData.uiPanel != null)
        {
            partData.uiPanel.Show(partData.displayName, state.promptText);
            _activePanel = partData.uiPanel;
        }

        // Video — respect showVideoUI flag on uiPanel
        bool videoAllowed = partData.uiPanel == null || partData.uiPanel.showVideoUI;
        if (partData.videoPanel != null && partData.videoClip != null && videoAllowed)
        {
            partData.videoPanel.Play(partData.videoClip);

            float audioLength = state.promptAudio != null ? state.promptAudio.length : 0f;
            // Use clip.length directly — VideoPlayer loads async so VideoLength is 0 at this point
            float videoLength = (float)partData.videoClip.length;

            // If video is longer than audio, take over advancement
            if (videoLength > audioLength)
            {
                _videoIntercepting = true;

                if (_videoCoroutine != null) StopCoroutine(_videoCoroutine);
                _videoCoroutine = StartCoroutine(WaitForVideoCoroutine(
                    state, partData.videoPanel, audioLength, videoLength));
            }
            else
            {
                _videoIntercepting = false;
            }
        }
        else
        {
            // No video or video hidden — hide the video panel
            if (partData.videoPanel != null)
                partData.videoPanel.Stop();
            _videoIntercepting = false;
        }

        // Highlight
        if (partData.highlightTarget != null)
        {
            _activeHighlight = partData.highlightTarget;
            OnPartBecameActive?.Invoke(_activeHighlight);

            var highlight = _activeHighlight.GetComponent<GrabHighlightController>();
            if (highlight != null) highlight.ShowHighlight();
        }
    }

    private IEnumerator WaitForVideoCoroutine(
        SimulationState state,
        FamiliarizationVideoPanel videoPanel,
        float audioLength,
        float videoLength)
    {
        // Wait for the longer of the two
        float waitTime = Mathf.Max(audioLength, videoLength);
        yield return new WaitForSeconds(waitTime);

        _videoCoroutine = null;
        _videoIntercepting = false;

        // Only advance if this state is still active
        if (simulationManager.currentState != state) yield break;

        videoPanel.Stop();

        // Hide panel directly without going through HideActivePanel
        // to avoid stopping this coroutine while it's still running
        if (_activePanel != null)
        {
            _activePanel.Hide();
            _activePanel = null;
        }

        // Advance — handle Free Roam last-part separately to return to parked state
        int currentIndex = simulationManager.states.IndexOf(state.gameObject);

        if (simulationManager.simulationMode == SimulationMode.FreeRoam &&
            _activeGroup != null &&
            currentIndex == _activeGroupEndIdx)
        {
            // Last part of station in Free Roam — return to parked state
            _activeGroup = null;
            _activeGroupEndIdx = -1;
            simulationManager.MoveToState(freeRoamStateIndex);
        }
        else
        {
            // Guided or mid-sequence Free Roam — advance normally
            simulationManager.MoveToNextState();
        }
    }

    private void OnTeleportCompletedForHighlight()
    {
        if (_pendingHighlightState == null) return;

        SimulationState state = _pendingHighlightState;
        _pendingHighlightState = null;

        // Only show UI + highlight if this state is still active
        if (simulationManager.currentState != state) return;

        ShowPanelAndHighlight(state);
    }



    // ─────────────────────────────────────────────────────────────────────
    // PROMPT COMPLETED
    // ─────────────────────────────────────────────────────────────────────

    private void OnPromptCompleted()
    {
        // If video is still running and is longer than audio, do NOT hide yet.
        // WaitForVideoCoroutine will hide and advance when video finishes.
        if (_videoIntercepting) return;

        HideActivePanel();
    }

    private void HideActivePanel()
    {
        // Stop video coroutine only when called from outside video flow
        if (_videoCoroutine != null)
        {
            StopCoroutine(_videoCoroutine);
            _videoCoroutine = null;
            _videoIntercepting = false;
        }

        // Stop active video panel if playing
        if (_activePanel != null)
        {
            var partData = _activePanel.GetComponentInParent<PartStepData>()
                        ?? _activePanel.GetComponent<PartStepData>();
            if (partData == null)
            {
                var state = simulationManager?.currentState;
                if (state != null) partData = state.GetComponent<PartStepData>();
            }
            if (partData?.videoPanel != null)
                partData.videoPanel.Stop();

            _activePanel.Hide();
            _activePanel = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // I-BUTTON ENTRY POINT
    // ─────────────────────────────────────────────────────────────────────

    public void OnStationSelected(StationGroup group)
    {
        if (group == null) return;

        Debug.Log($"[PartsIdentificationManager] Station selected: '{group.stationName}'");

        SetAllIButtonsVisible(false);

        // Force-stop ALL coroutines on the parked state GO before MoveToState.
        if (simulationManager.currentState != null)
            simulationManager.currentState.StopAllCoroutines();

        _activeGroup = group;
        _activeGroupEndIdx = group.partStateIndices != null && group.partStateIndices.Count > 0
            ? group.partStateIndices[group.partStateIndices.Count - 1]
            : group.stationIntroStateIndex;

        simulationManager.MoveToState(group.stationIntroStateIndex);
    }

    // ─────────────────────────────────────────────────────────────────────
    // INTERCEPT — called by SimulationManager.NotifyStateComplete
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by SimulationManager.NotifyStateComplete before MoveToNextState.
    /// Returns true if we handle the transition (last part of a station in Free Roam).
    /// SimulationManager skips MoveToNextState when this returns true.
    /// </summary>
    public bool TryInterceptStateComplete(int completedIndex)
    {
        // Video intercept — applies in BOTH modes when video is longer than audio.
        // WaitForVideoCoroutine handles advancement when it finishes.
        if (_videoIntercepting)
        {
            Debug.Log("[PartsIdentificationManager] Intercepting — waiting for video to finish.");
            return true;
        }

        // Free Roam last part — return to parked state
        if (simulationManager.simulationMode == SimulationMode.FreeRoam)
        {
            if (_activeGroup == null) return false;
            if (completedIndex != _activeGroupEndIdx) return false;

            Debug.Log("[PartsIdentificationManager] Last part complete. Returning to Free Roam.");
            _activeGroup = null;
            _activeGroupEndIdx = -1;
            simulationManager.MoveToState(freeRoamStateIndex);
            return true;
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────

    private void ReturnToFreeRoam()
    {
        _activeGroup = null;
        _activeGroupEndIdx = -1;

        // Kill any orphaned coroutines on the current state before returning
        if (simulationManager.currentState != null)
            simulationManager.currentState.StopAllCoroutines();

        simulationManager.MoveToState(freeRoamStateIndex);
    }

    private void SetAllIButtonsVisible(bool visible)
    {
        foreach (var group in stationGroups)
            if (group.iButton != null)
                group.iButton.SetVisible(visible);
    }

    private void OnSimulationStart()
    {
        Debug.Log("[PartsIdentificationManager] Simulation started.");

        if (simulationManager.simulationMode == SimulationMode.FreeRoam)
        {
            // Free Roam — park on idle state, show I-buttons
            SetAllIButtonsVisible(true);
            Debug.Log("[PartsIdentificationManager] Free Roam started — I-buttons shown.");
        }
        else if (simulationManager.simulationMode == SimulationMode.Guided)
        {
            // Guided — skip the parked idle state and jump straight to first real state
            SetSkipButtonVisible(true);
            simulationManager.MoveToState(freeRoamStateIndex + 1);
            Debug.Log("[PartsIdentificationManager] Guided started — skipping parked state.");
        }
    }

    private void OnSimulationEnd()
    {
        Debug.Log("[PartsIdentificationManager] Simulation ended.");
        SetAllIButtonsVisible(false);
        SetSkipButtonVisible(false);
        if (_skipCooldownCoroutine != null)
        {
            StopCoroutine(_skipCooldownCoroutine);
            _skipCooldownCoroutine = null;
        }
        SetSkipButtonInteractable(true);
        HideActivePanel();

        if (_activeHighlight != null)
        {
            OnPartBecameInactive?.Invoke(_activeHighlight);

            var highlight = _activeHighlight.GetComponent<GrabHighlightController>();
            if (highlight != null) highlight.HideHighlight();

            _activeHighlight = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // SKIP BUTTON — Guided mode only
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Skips to the next Station intro state in Guided mode.
    /// Wired automatically to CustomButton.OnButtonClicked at runtime.
    /// </summary>
    public void SkipToNextStation()
    {
        if (simulationManager.simulationMode != SimulationMode.Guided)
            return;

        int currentIndex = simulationManager.currentState != null
            ? simulationManager.states.IndexOf(simulationManager.currentState.gameObject)
            : -1;

        if (currentIndex < 0) return;

        // Find nearest station whose intro index is ahead of current
        StationGroup nextStation = null;
        int nextIntroIndex = int.MaxValue;

        foreach (var group in stationGroups)
        {
            if (group.stationIntroStateIndex > currentIndex &&
                group.stationIntroStateIndex < nextIntroIndex)
            {
                nextStation = group;
                nextIntroIndex = group.stationIntroStateIndex;
            }
        }

        if (nextStation == null)
        {
            Debug.Log("[PartsIdentificationManager] Skip — already at or past the last station.");
            return;
        }

        Debug.Log($"[PartsIdentificationManager] Skipping to '{nextStation.stationName}' " +
                  $"at index {nextStation.stationIntroStateIndex}.");

        // Disable skip button immediately — re-enabled after cooldown
        SetSkipButtonInteractable(false);
        if (_skipCooldownCoroutine != null) StopCoroutine(_skipCooldownCoroutine);
        _skipCooldownCoroutine = StartCoroutine(SkipCooldownRoutine());

        // Stop current state coroutines — kills audio and loops immediately
        if (simulationManager.currentState != null)
            simulationManager.currentState.StopAllCoroutines();

        // Hide active UI panel and highlight
        HideActivePanel();
        if (_activeHighlight != null)
        {
            var highlight = _activeHighlight.GetComponent<GrabHighlightController>();
            if (highlight != null) highlight.HideHighlight();
            OnPartBecameInactive?.Invoke(_activeHighlight);
            _activeHighlight = null;
        }

        simulationManager.MoveToState(nextStation.stationIntroStateIndex);
    }

    private IEnumerator SkipCooldownRoutine()
    {
        yield return new WaitForSeconds(skipCooldownDuration);
        _skipCooldownCoroutine = null;
        SetSkipButtonInteractable(true);
    }

    private void SetSkipButtonInteractable(bool interactable)
    {
        if (skipButtonGO == null) return;
        skipButtonGO.SetActive(interactable);
    }

    public void SetSkipButtonVisible(bool visible)
    {
        if (skipButtonGO != null)
            skipButtonGO.SetActive(visible);
    }

    // ─────────────────────────────────────────────────────────────────────
    // PUBLIC UTILITY
    // ─────────────────────────────────────────────────────────────────────

    public StationGroup GetGroupById(string id)
    {
        foreach (var g in stationGroups)
            if (g.stationId == id) return g;
        return null;
    }

    public bool IsSequencePlaying => _activeGroup != null;
}