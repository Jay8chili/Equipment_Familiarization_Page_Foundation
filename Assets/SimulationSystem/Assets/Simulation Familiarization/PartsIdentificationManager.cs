// ════════════════════════════════════════════════════════════════════════════
//  PartsIdentificationManager.cs
// ════════════════════════════════════════════════════════════════════════════

using SimulationSystem.V02.Assistant;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// ── Station Group data ────────────────────────────────────────────────────

[System.Serializable]
public class StationGroup
{
    [Tooltip("Display name — used only for Inspector clarity and debug logs.")]
    public string stationName;

    [Tooltip("Station ID matching the StationID field on PartStepData (e.g. S01).")]
    public string stationId;

    [Tooltip("Index of the Station intro SimulationState in SimulationManager.states.")]
    public int stationIntroStateIndex;

    [Tooltip("Ordered indices of all Part SimulationStates in SimulationManager.states " +
             "that belong to this station.")]
    public List<int> partStateIndices = new List<int>();

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

    // ── Internal ──────────────────────────────────────────────────────────
    private StationGroup _activeGroup;
    private int _activeGroupEndIdx = -1;
    private GameObject _activeHighlight;
    private FamiliarizationUIPanel _activePanel;
    private SimulationState _lastKnownState;

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

            // Hide GrabHighlightController directly if present
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

    // ─────────────────────────────────────────────────────────────────────
    // UI + HIGHLIGHT
    // ─────────────────────────────────────────────────────────────────────

    private void ShowUIAndHighlight(SimulationState state)
    {
        if (state == null) return;

        var partData = state.GetComponent<PartStepData>();
        if (partData == null) return;

        HideActivePanel();

        // ── Show familiarization UI panel ─────────────────────────────────
        if (partData.uiPanel != null)
        {
            partData.uiPanel.Show(partData.displayName, state.promptText);
            _activePanel = partData.uiPanel;
        }

        // ── Kill bot prompt panel scale ───────────────────────────────────
        // Force scale to zero every frame while this state is active so the
        // AssistantManager panel never becomes visible. Audio plays normally.
        if (botPromptPanel != null)
            botPromptPanel.transform.localScale = Vector3.zero;

        // ── Highlight ─────────────────────────────────────────────────────
        if (partData.highlightTarget != null)
        {
            _activeHighlight = partData.highlightTarget;
            OnPartBecameActive?.Invoke(_activeHighlight);

            // Drive GrabHighlightController directly if present
            var highlight = _activeHighlight.GetComponent<GrabHighlightController>();
            if (highlight != null) highlight.ShowHighlight();
        }
    }



    // ─────────────────────────────────────────────────────────────────────
    // PROMPT COMPLETED
    // ─────────────────────────────────────────────────────────────────────

    private void OnPromptCompleted()
    {
        HideActivePanel();
    }

    private void HideActivePanel()
    {
        if (_activePanel == null) return;
        _activePanel.Hide();
        _activePanel = null;
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
        if (simulationManager.simulationMode != SimulationMode.FreeRoam) return false;
        if (_activeGroup == null) return false;
        if (completedIndex != _activeGroupEndIdx) return false;

        Debug.Log($"[PartsIdentificationManager] Last part complete. Returning to Free Roam.");
        _activeGroup = null;
        _activeGroupEndIdx = -1;
        simulationManager.MoveToState(freeRoamStateIndex);
        return true;
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