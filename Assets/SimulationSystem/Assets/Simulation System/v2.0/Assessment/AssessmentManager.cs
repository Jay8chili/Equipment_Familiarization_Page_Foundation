using SimulationSystem.V02.Assistant;
using SimulationSystem.V02.StateInteractions;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that owns all assessment logic.
/// Only active when SimulationManager.simulationMode == Assessment.
/// Observes the simulation via SimulationState events — never blocks the flow.
/// </summary>
public class AssessmentManager : MonoBehaviour
{
    public static AssessmentManager Instance { get; private set; }

    [Header("Hint Button")]
    [Tooltip("The GO that holds the CustomButton for hint. Enabled/disabled per interaction.")]
    [SerializeField] private GameObject hintButtonGO;

    // ── Session Data ─────────────────────────────────────────────────────────

    private AssessmentSession _session;
    private StateAssessmentRecord _currentStateRecord;
    private InteractionAssessmentRecord _currentInteractionRecord;

    // ── Runtime State ────────────────────────────────────────────────────────

    private SimulationState _currentSimState;
    private Interactions _currentInteraction;

    // Tracks active wrong detect zones for cleanup on interaction complete
    private List<WrongDetectRuntimeEntry> _activeWrongDetects = new();
    private List<WrongGrabRuntimeEntry> _activeWrongGrabs = new();

    private bool _suppressWOTDPenalty;

    private bool _isAssessmentMode => SimulationManager.Instance != null &&
                                      SimulationManager.Instance.simulationMode == SimulationMode.Assessment;

    public bool IsHintTaken => _currentInteractionRecord != null && _currentInteractionRecord.hintTaken;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (hintButtonGO != null)
        {
            hintButtonGO.SetActive(false);
            var button = hintButtonGO.GetComponentInChildren<CustomButton>();
            if (button != null)
                button.OnButtonClicked.AddListener(OnHintClicked);
        }
    }

    private void OnDestroy()
    {
        if (hintButtonGO != null)
        {
            var button = hintButtonGO.GetComponentInChildren<CustomButton>();
            if (button != null)
                button.OnButtonClicked.RemoveListener(OnHintClicked);
        }
    }

    // ─────────────────────────────────────────────
    // SESSION
    // ─────────────────────────────────────────────

    public void BeginSession()
    {
        if (!_isAssessmentMode) return;
        _session = new AssessmentSession();
        Debug.Log("[AssessmentManager] Session started.");
    }

    public void CloseSession()
    {
        if (_session == null) return;

        _session.totalMaxScore = 0f;
        _session.totalFinalScore = 0f;
        foreach (var s in _session.states)
        {
            _session.totalMaxScore += s.stateMaxScore;
            _session.totalFinalScore += s.stateFinalScore;
        }

        Debug.Log($"[AssessmentManager] ══ SESSION COMPLETE ══\n" +
                  $"Total Score: {_session.totalFinalScore:F1} / {_session.totalMaxScore:F1}");
        foreach (var s in _session.states)
            LogStateRecord(s);
    }

    // ─────────────────────────────────────────────
    // STATE
    // ─────────────────────────────────────────────

    public void BeginState(SimulationState state)
    {
        if (!_isAssessmentMode) return;

        _currentSimState = state;
        _currentStateRecord = new StateAssessmentRecord { stateName = state.name };

        var controller = state.GetComponent<AssessmentController>();
        if (controller != null)
        {
            foreach (var config in controller.interactionConfigs)
            {
                if (config.interaction == null) continue;
                _currentStateRecord.stateMaxScore += config.maxScore;
                _currentStateRecord.interactions.Add(new InteractionAssessmentRecord
                {
                    interactionName = config.interaction.name,
                    maxScore = config.maxScore,
                    scoreAwarded = config.maxScore,
                    hintPenalty = config.hintPenalty
                });
            }
        }

        _currentStateRecord.stateFinalScore = _currentStateRecord.stateMaxScore;
        Debug.Log($"[AssessmentManager] State started: '{state.name}' | MaxScore: {_currentStateRecord.stateMaxScore:F1}");
    }

    public void CloseState()
    {
        if (!_isAssessmentMode || _currentStateRecord == null) return;
        _session?.states.Add(_currentStateRecord);
        LogStateRecord(_currentStateRecord);
        _currentStateRecord = null;
    }

    // ─────────────────────────────────────────────
    // INTERACTION
    // ─────────────────────────────────────────────

    public void OnInteractionStarted(Interactions interaction)
    {
        if (!_isAssessmentMode) return;

        _currentInteraction = interaction;
        _currentInteractionRecord = FindInteractionRecord(interaction);

        bool isUI = interaction is UIInteraction;
        bool isIdle = interaction is IdleInteraction;
        bool isMoveToNextStep = _currentSimState != null && _currentSimState.MoveToNextStepAfterAudio;
        bool hintEligible = !isUI && !isIdle && !isMoveToNextStep;

        if (hintButtonGO != null)
            hintButtonGO.SetActive(hintEligible);

        DisableVisualsForInteraction(interaction);

        // Setup wrong detects if this interaction is a DetectInteraction
        SetupWrongDetects(interaction);

        // Setup wrong grabs if this interaction is a GrabInteraction
        SetupWrongGrabs(interaction);

        Debug.Log($"[AssessmentManager] Interaction started: '{interaction.name}' | HintEligible: {hintEligible}");
    }

    public void OnInteractionCompleted(Interactions interaction)
    {
        if (!_isAssessmentMode) return;

        if (hintButtonGO != null)
            hintButtonGO.SetActive(false);

        // Cleanup wrong detects — remove WOTD from correct detect and wrong zones
        CleanupWrongDetects(interaction);

        // Cleanup wrong grabs
        CleanupWrongGrabs();

        _currentInteraction = null;
        _currentInteractionRecord = null;

        Debug.Log($"[AssessmentManager] Interaction completed: '{interaction.name}'");
    }

    // ─────────────────────────────────────────────
    // WRONG DETECT SETUP & CLEANUP
    // ─────────────────────────────────────────────

    /// <summary>
    /// On interaction start:
    /// 1. Snapshot wrong detect zone's current state (activeSelf, OTD, canInteract)
    /// 2. Add WOTD to correct detect's OTD
    /// 3. Add WOTD + correct OTD to wrong detect zone's OTD
    /// 4. Activate wrong detect zone and enable canInteract
    /// 5. Subscribe to wrong detect zone's OnInteractionStartedEvent for penalty
    /// </summary>
    private void SetupWrongDetects(Interactions interaction)
    {
        _activeWrongDetects.Clear();

        if (_currentSimState == null) return;
        var controller = _currentSimState.GetComponent<AssessmentController>();
        if (controller == null) return;

        // Only process for DetectInteraction
        if (!(interaction is DetectInteraction correctDetect)) return;

        // Find matching config
        var config = controller.interactionConfigs.Find(c => c.interaction == interaction);
        if (config == null || config.wrongDetects == null || config.wrongDetects.Count == 0) return;

        List<GameObject> correctOTD = correctDetect.ObjectsToBeDetectedList;

        // Tell correct detect which objects are WOTD so it can fire penalty but skip completion
        correctDetect.SetupWrongObjects(config.WOTD);
        float correctWotdPenalty = config.wrongDetectPenalty;
        var correctPenalized = new HashSet<GameObject>();
        correctDetect.OnWOTDEntered = (obj) => RecordWrongDetect(correctDetect.name, correctWotdPenalty, obj, correctPenalized);

        // ── Add WOTD to correct detect's OTD ─────────────────────────────────
        if (config.WOTD != null)
            foreach (var obj in config.WOTD)
                if (obj != null && !correctOTD.Contains(obj))
                    correctOTD.Add(obj);

        // ── Setup each wrong detect zone ──────────────────────────────────────
        foreach (var wrongDetect in config.wrongDetects)
        {
            if (wrongDetect == null) continue;

            // Snapshot
            var snapshot = new WrongDetectSnapshot
            {
                wasActive = wrongDetect.gameObject.activeSelf,
                canInteract = wrongDetect.canInteract,
                originalOTD = new List<GameObject>(wrongDetect.ObjectsToBeDetectedList)
            };

            // Feed WOTD + correct OTD into wrong detect's OTD
            if (config.WOTD != null)
                foreach (var obj in config.WOTD)
                    if (obj != null && !wrongDetect.ObjectsToBeDetectedList.Contains(obj))
                        wrongDetect.ObjectsToBeDetectedList.Add(obj);

            foreach (var obj in correctOTD)
                if (obj != null && !wrongDetect.ObjectsToBeDetectedList.Contains(obj))
                    wrongDetect.ObjectsToBeDetectedList.Add(obj);

            // Activate
            wrongDetect.gameObject.SetActive(true);
            wrongDetect.canInteract = true;

            // Setup combined WOTD on wrong detect zone — includes both WOTD and correct OTD
            // so ALL objects entering the wrong zone fire penalty and skip interaction start
            var combinedWOTD = new List<GameObject>(config.WOTD ?? new List<GameObject>());
            foreach (var obj in correctOTD)
                if (obj != null && !combinedWOTD.Contains(obj))
                    combinedWOTD.Add(obj);

            wrongDetect.SetupWrongObjects(combinedWOTD);
            float penalty = config.wrongDetectPenalty;
            var entry = new WrongDetectRuntimeEntry
            {
                wrongDetectZone = wrongDetect,
                snapshot = snapshot,
                correctOTD = correctOTD,
                addedWOTD = config.WOTD != null ? new List<GameObject>(config.WOTD) : new List<GameObject>()
            };
            wrongDetect.OnWOTDEntered = (obj) => RecordWrongDetect(wrongDetect.name, penalty, obj, entry.penalizedObjects);
            _activeWrongDetects.Add(entry);

            Debug.Log($"[AssessmentManager] Wrong detect '{wrongDetect.name}' set up | Penalty: {penalty:F1}");
        }
    }

    /// <summary>
    /// On interaction complete — fully restores each wrong detect zone to its snapshot state:
    /// 1. Unsubscribe penalty listener
    /// 2. Remove WOTD from correct detect's OTD
    /// 3. Restore wrong detect zone's OTD to snapshot
    /// 4. Restore canInteract and activeSelf to snapshot values
    /// </summary>
    private void CleanupWrongDetects(Interactions interaction)
    {
        if (_activeWrongDetects.Count == 0) return;

        // Clear WOTD from correct detect
        if (interaction is DetectInteraction correctDetect)
            correctDetect.ClearWrongObjects();

        // Remove WOTD from correct OTD once (shared across all wrong detects)
        var first = _activeWrongDetects[0];
        if (first.correctOTD != null)
            foreach (var obj in first.addedWOTD)
                first.correctOTD.Remove(obj);

        foreach (var entry in _activeWrongDetects)
        {
            if (entry.wrongDetectZone == null) continue;

            // Clear WOTD and unsubscribe penalty from wrong detect zone
            entry.wrongDetectZone.ClearWrongObjects();

            // Restore OTD, canInteract, activeSelf to snapshot
            entry.wrongDetectZone.ObjectsToBeDetectedList.Clear();
            entry.wrongDetectZone.ObjectsToBeDetectedList.AddRange(entry.snapshot.originalOTD);
            entry.wrongDetectZone.canInteract = entry.snapshot.canInteract;
            entry.wrongDetectZone.gameObject.SetActive(entry.snapshot.wasActive);

            Debug.Log($"[AssessmentManager] Wrong detect '{entry.wrongDetectZone.name}' restored.");
        }

        _activeWrongDetects.Clear();
    }

    // ─────────────────────────────────────────────
    // WRONG DETECT RECORD
    // ─────────────────────────────────────────────

    private System.Collections.IEnumerator ClearSuppressNextFrame()
    {
        yield return null;
        _suppressWOTDPenalty = false;
    }

    private void RecordWrongDetect(string zoneName, float penalty, GameObject wotdObject, HashSet<GameObject> penalizedSet)
    {
        if (_currentInteractionRecord == null) return;
        if (_suppressWOTDPenalty) return;

        // Only penalize once per unique object per zone
        if (penalizedSet.Contains(wotdObject)) return;
        penalizedSet.Add(wotdObject);

        _currentInteractionRecord.wrongDetectCount++;
        _currentInteractionRecord.scoreAwarded = Mathf.Max(0f, _currentInteractionRecord.scoreAwarded - penalty);
        RecalculateStateFinalScore();

        Debug.Log($"[AssessmentManager] Wrong detect — '{wotdObject.name}' in '{zoneName}' | " +
                  $"Penalty: {penalty:F1} | Score now: {_currentInteractionRecord.scoreAwarded:F1}");
    }

    // ─────────────────────────────────────────────
    // HINT
    // ─────────────────────────────────────────────

    public void OnHintClicked()
    {
        if (!_isAssessmentMode) return;
        if (_currentSimState == null) return;

        if (hintButtonGO != null)
            hintButtonGO.SetActive(false);

        // Suppress WOTD penalty for one frame — finger pressing hint button
        // may physically overlap detect zones simultaneously
        _suppressWOTDPenalty = true;
        StartCoroutine(ClearSuppressNextFrame());

        // Disable all wrong detect zones for the rest of this interaction
        // Snapshot already stores their original canInteract — restored on cleanup
        foreach (var entry in _activeWrongDetects)
            if (entry.wrongDetectZone != null)
                entry.wrongDetectZone.canInteract = false;

        if (_currentInteractionRecord != null)
        {
            float penalty = _currentInteractionRecord.hintPenalty;
            _currentInteractionRecord.hintTaken = true;
            _currentInteractionRecord.scoreAwarded = Mathf.Max(0f, _currentInteractionRecord.scoreAwarded - penalty);
            RecalculateStateFinalScore();
            Debug.Log($"[AssessmentManager] Hint taken on '{_currentInteractionRecord.interactionName}' | " +
                      $"Penalty: {penalty:F1} | Score now: {_currentInteractionRecord.scoreAwarded:F1}");
        }

        AssistantManager.Instance?.ShowHintPrompt(_currentSimState);
        EnableVisualsForInteraction(_currentInteraction);

        // Reset wrong grabs on hint — disable and force ungrab
        ResetWrongGrabs();
    }

    // ─────────────────────────────────────────────
    // WRONG GRAB SETUP & CLEANUP
    // ─────────────────────────────────────────────

    private void SetupWrongGrabs(Interactions interaction)
    {
        _activeWrongGrabs.Clear();

        if (_currentSimState == null) return;
        var controller = _currentSimState.GetComponent<AssessmentController>();
        if (controller == null) return;

        if (!(interaction is GrabInteraction)) return;

        var config = controller.interactionConfigs.Find(c => c.interaction == interaction);
        if (config == null || config.wrongGrabs == null || config.wrongGrabs.Count == 0) return;

        var penalizedSet = new HashSet<GrabInteraction>();

        foreach (var wrongGrab in config.wrongGrabs)
        {
            if (wrongGrab == null) continue;

            bool snapshotCanInteract = wrongGrab.canInteract;
            bool snapshotResetOnRelease = wrongGrab.resetOnRelease;

            wrongGrab.canInteract = true;
            wrongGrab.resetOnRelease = true;

            float penalty = config.wrongGrabPenalty;
            UnityEngine.Events.UnityAction grabAction = () => RecordWrongGrab(wrongGrab, penalty, penalizedSet);
            wrongGrab.onGrabbed.AddListener(grabAction);

            _activeWrongGrabs.Add(new WrongGrabRuntimeEntry
            {
                wrongGrab = wrongGrab,
                snapshotCanInteract = snapshotCanInteract,
                snapshotResetOnRelease = snapshotResetOnRelease,
                grabAction = grabAction,
                penalizedSet = penalizedSet
            });

            Debug.Log($"[AssessmentManager] Wrong grab '{wrongGrab.name}' set up | Penalty: {penalty:F1}");
        }
    }

    private void CleanupWrongGrabs()
    {
        foreach (var entry in _activeWrongGrabs)
        {
            if (entry.wrongGrab == null) continue;

            entry.wrongGrab.onGrabbed.RemoveListener(entry.grabAction);

            var canvasGroup = entry.wrongGrab.GetComponentInChildren<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = 0f;

            entry.wrongGrab.ForceUngrab();

            entry.wrongGrab.canInteract = entry.snapshotCanInteract;
            entry.wrongGrab.resetOnRelease = entry.snapshotResetOnRelease;

            Debug.Log($"[AssessmentManager] Wrong grab '{entry.wrongGrab.name}' restored.");
        }

        _activeWrongGrabs.Clear();
    }

    private void ResetWrongGrabs()
    {
        foreach (var entry in _activeWrongGrabs)
        {
            if (entry.wrongGrab == null) continue;

            entry.wrongGrab.canInteract = false;

            var canvasGroup = entry.wrongGrab.GetComponentInChildren<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = 0f;

            entry.wrongGrab.ForceUngrab();

            Debug.Log($"[AssessmentManager] Wrong grab '{entry.wrongGrab.name}' reset on hint.");
        }
    }

    private void RecordWrongGrab(GrabInteraction wrongGrab, float penalty, HashSet<GrabInteraction> penalizedSet)
    {
        if (_currentInteractionRecord == null) return;

        if (penalizedSet.Contains(wrongGrab)) return;
        penalizedSet.Add(wrongGrab);

        _currentInteractionRecord.wrongGrabCount++;
        _currentInteractionRecord.scoreAwarded = Mathf.Max(0f, _currentInteractionRecord.scoreAwarded - penalty);
        RecalculateStateFinalScore();

        Debug.Log($"[AssessmentManager] Wrong grab — '{wrongGrab.name}' | " +
                  $"Penalty: {penalty:F1} | Score now: {_currentInteractionRecord.scoreAwarded:F1}");
    }

    // ─────────────────────────────────────────────
    // VISUAL HELPERS
    // ─────────────────────────────────────────────

    public void DisableVisualsAfterStart(Interactions interaction)
    {
        if (!_isAssessmentMode) return;
        DisableVisualsForInteraction(interaction);
    }

    private void DisableVisualsForInteraction(Interactions interaction)
    {
        if (interaction == null) return;

        if (interaction is GrabInteraction grab)
        {
            grab.grabHighlightController.HideHighlight();
            interaction.SetRadialUIVisible(false);
        }
        else if (interaction is DetectInteraction detect)
        {
            var renderer = interaction.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;

            foreach (var obj in detect.ObjectsToBeDetectedList)
            {
                if (obj == null) continue;
                var grabComp = obj.GetComponent<GrabInteraction>();
                if (grabComp != null)
                    grabComp.grabHighlightController.HideHighlight();
            }

            interaction.SetRadialUIVisible(false);
        }
        else if (interaction is GazeInteraction)
        {
            var renderer = interaction.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
            interaction.SetRadialUIVisible(false);
        }
    }

    private void EnableVisualsForInteraction(Interactions interaction)
    {
        if (interaction == null) return;

        if (interaction is GrabInteraction grab)
        {
            grab.grabHighlightController.ShowHighlight();
            interaction.SetRadialUIVisible(true);
        }
        else if (interaction is DetectInteraction detect)
        {
            var renderer = interaction.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = true;

            foreach (var obj in detect.ObjectsToBeDetectedList)
            {
                if (obj == null) continue;
                var grabComp = obj.GetComponent<GrabInteraction>();
                if (grabComp != null)
                {
                    grabComp.grabHighlightController.ShowHighlight();
                    grabComp.SetRadialUIVisible(true);
                }
            }

            interaction.SetRadialUIVisible(true);
        }
        else if (interaction is GazeInteraction)
        {
            var renderer = interaction.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = true;
            interaction.SetRadialUIVisible(true);
        }
    }

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────

    private InteractionAssessmentRecord FindInteractionRecord(Interactions interaction)
    {
        if (_currentStateRecord == null || interaction == null) return null;
        return _currentStateRecord.interactions.Find(r => r.interactionName == interaction.name);
    }

    private void RecalculateStateFinalScore()
    {
        if (_currentStateRecord == null) return;
        float total = 0f;
        foreach (var r in _currentStateRecord.interactions)
            total += r.scoreAwarded;
        _currentStateRecord.stateFinalScore = Mathf.Max(0f, total);
    }

    private void LogStateRecord(StateAssessmentRecord s)
    {
        Debug.Log($"[AssessmentManager] State '{s.stateName}': " +
                  $"{s.stateFinalScore:F1} / {s.stateMaxScore:F1} | HintTaken: {s.hintTaken}");
        foreach (var i in s.interactions)
            Debug.Log($"  └ '{i.interactionName}': {i.scoreAwarded:F1}/{i.maxScore:F1} | " +
                      $"HintTaken: {i.hintTaken} | WrongDetects: {i.wrongDetectCount} | WrongGrabs: {i.wrongGrabCount}");
    }
}

// ─────────────────────────────────────────────
// RUNTIME ENTRY — tracks active wrong detects for cleanup
// ─────────────────────────────────────────────

internal class WrongDetectSnapshot
{
    public bool wasActive;
    public bool canInteract;
    public List<GameObject> originalOTD;
}

internal class WrongDetectRuntimeEntry
{
    public DetectInteraction wrongDetectZone;
    public WrongDetectSnapshot snapshot;
    public List<GameObject> correctOTD;
    public List<GameObject> addedWOTD;
    public HashSet<GameObject> penalizedObjects = new();
}

internal class WrongGrabRuntimeEntry
{
    public GrabInteraction wrongGrab;
    public bool snapshotCanInteract;
    public bool snapshotResetOnRelease;
    public UnityEngine.Events.UnityAction grabAction;
    public HashSet<GrabInteraction> penalizedSet;
}

// ─────────────────────────────────────────────
// DATA MODELS
// ─────────────────────────────────────────────

[System.Serializable]
public class AssessmentSession
{
    public float totalMaxScore;
    public float totalFinalScore;
    public List<StateAssessmentRecord> states = new();
}

[System.Serializable]
public class StateAssessmentRecord
{
    public string stateName;
    public float stateMaxScore;
    public float stateFinalScore;
    public bool hintTaken;
    public float hintPenaltyApplied;
    public List<InteractionAssessmentRecord> interactions = new();
}

[System.Serializable]
public class InteractionAssessmentRecord
{
    public string interactionName;
    public float maxScore;
    public float scoreAwarded;
    public bool hintTaken;
    public float hintPenalty;
    public int wrongDetectCount;
    public int wrongGrabCount;
}