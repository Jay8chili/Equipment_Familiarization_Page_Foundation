using SimulationSystem.V02.Simulation.Managers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SimulationManager : MonoBehaviour
{

    [Tooltip("Assign state GameObjects (in order). Each state GameObject should have a State component.")]
    public List<GameObject> states;

    private int currentIndex = -1;
    public SimulationState currentState { get; private set; }

    public static SimulationManager Instance;

    public GameObject setModeUI;
    public GameObject simulationCompletedPanel;
    [HideInInspector] public SimulationMode simulationMode { get; private set; }

    [Header("Grab Settings")]
    [Tooltip("Global grab behaviour. Individual GrabInteraction objects can override this.")]
    public GrabBehaviour grabBehaviour = GrabBehaviour.GrabWithPinch;

    // ── Intro ─────────────────────────────────────────────────────────────
    [Header("Intro Settings")]
    [Tooltip("Panel to show during the intro. Starts inactive; hidden automatically when audio ends.")]
    public GameObject introPanel;

    [Tooltip("Audio clip to play during the intro. If null, intro phase is skipped.")]
    public AudioClip introClip;

    [Tooltip("None = full flow (intro → setMode → simulation).\n" +
             "SkipIntro = skip intro, show setModeUI directly.\n" +
             "SkipAll = skip intro and setMode, start simulation immediately.")]
    public StartupOverride startupOverride = StartupOverride.None;

    [Header("Simulation Events")]
    public UnityEvent SimulationIntroStarted;
    public UnityEvent SimulationIntroCompleted;
    public UnityEvent SimulationStart;
    public UnityEvent SimulationEnd;
    public UnityEvent SimulationPaused;
    public UnityEvent SimulationResumed;

    // ── Pause State ───────────────────────────────────────────────────────
    private bool _isPaused;
    public bool isPaused => _isPaused;

    private Dictionary<Interactions, bool> _pausedInteractFlags = new Dictionary<Interactions, bool>();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
        }
        Instance = this;
    }

    private void Start()
    {
        states.Clear();

        for (int i = 0; i < gameObject.transform.childCount; i++)
        {
            if (transform.GetChild(i).gameObject.activeInHierarchy &&
                transform.GetChild(i).gameObject.GetComponent<SimulationState>() != null)
            {
                states.Add(transform.GetChild(i).gameObject);
            }
        }

        switch (startupOverride)
        {
            case StartupOverride.SkipAll:
                StartSimulation();
                break;

            case StartupOverride.SkipIntro:
                setModeUI.SetActive(true);
                break;

            case StartupOverride.None:
            default:
                StartCoroutine(IntroRoutine());
                break;
        }
    }

    // ─────────────────────────────────────────────
    // INTRO
    // ─────────────────────────────────────────────

    private IEnumerator IntroRoutine()
    {
        if (introClip != null)
        {
            if (introPanel != null)
                introPanel.SetActive(true);

            SimulationIntroStarted?.Invoke();

            // yield return on the IEnumerator directly — Unity waits frame-by-frame
            // until the audio finishes and SoundManager clears the clip
            yield return StartCoroutine(SoundManager.PlayIntroCoroutine(introClip));

            if (introPanel != null)
                introPanel.SetActive(false);

            SimulationIntroCompleted?.Invoke();
        }

        // Intro done (or skipped) — now show the mode selection
        setModeUI.SetActive(true);
    }

    // ─────────────────────────────────────────────
    // PAUSE / RESUME / PLAY
    // ─────────────────────────────────────────────

    /// <summary>
    /// Pause the entire simulation. All interactions in the current state
    /// have canInteract set to false. Flags are snapshot so ResumeSimulation
    /// can restore them exactly.
    /// </summary>
    public void PauseSimulation()
    {
        if (_isPaused) return;
        _isPaused = true;

        _pausedInteractFlags.Clear();

        if (currentState != null)
        {
            foreach (var interaction in currentState.listOfInteractions)
            {
                if (interaction == null) continue;
                _pausedInteractFlags[interaction] = interaction.canInteract;
                interaction.canInteract = false;
            }
        }

        SimulationPaused?.Invoke();
        Debug.Log("[SimulationManager] Simulation paused.");
    }

    /// <summary>
    /// Resume from a paused state. Restores canInteract flags to exactly
    /// what they were before PauseSimulation was called.
    /// </summary>
    public void ResumeSimulation()
    {
        if (!_isPaused) return;
        _isPaused = false;

        if (currentState != null)
        {
            foreach (var interaction in currentState.listOfInteractions)
            {
                if (interaction == null) continue;
                if (_pausedInteractFlags.TryGetValue(interaction, out bool wasActive))
                    interaction.canInteract = wasActive;
            }
        }

        _pausedInteractFlags.Clear();

        SimulationResumed?.Invoke();
        Debug.Log("[SimulationManager] Simulation resumed.");
    }
    public void AssignStepSessionID(List<Step> steps)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            states[i].GetComponent<SimulationState>().sessionLogID = steps[i].log_id;
        }
    }

    /// <summary>
    /// Convenience alias — resumes from pause if paused, otherwise no-op.
    /// Wire to a "Play" button.
    /// </summary>
    public void PlaySimulation()
    {
        ResumeSimulation();
    }

    /// <summary>
    /// Toggle between paused and playing.
    /// Wire to a single pause/play toggle button.
    /// </summary>
    public void TogglePause()
    {
        if (_isPaused)
            ResumeSimulation();
        else
            PauseSimulation();
    }

    // ─────────────────────────────────────────────
    // EXISTING API
    // ─────────────────────────────────────────────

    public void PauseCurrentState()
    {
        foreach (var interaction in currentState.listOfInteractions)
        {
            interaction.canInteract = false;
        }
    }

    public void RestartCurrentState()
    {
        _isPaused = false;
        _pausedInteractFlags.Clear();
        MoveToState(currentIndex);
    }

    public void MoveToNextState()
    {
        _isPaused = false;
        _pausedInteractFlags.Clear();
        MoveToState(currentIndex + 1);
    }

    public void MoveToState(int index)
    {



        StepUpdate data = new StepUpdate(currentState.sessionLogID, "Done", SessionManager.Instance.GetElapsedTime(), (int)AssessmentManager.Instance._currentStateRecord.stateFinalScore, "This Step Is Assessed");
        SessionManager.Instance.UpdateSession(data);

        //Before Current State Changes To Next State, Log the Current State's Score and Time Spent to SessionManager



        if (states == null || states.Count == 0)
            return;

        _isPaused = false;
        _pausedInteractFlags.Clear();

        StopCurrent();
        if (index >= states.Count)
        {
            SoundManager.PlaySimulationEnd();
            Debug.LogError("Simulation Completed!");

            AssessmentManager.Instance?.CloseSession(); // if AssessmentManager exists in the scene, close the session
            SessionManager.Instance.EndSession();

            SimulationEnd?.Invoke();
            simulationCompletedPanel.SetActive(true);
        }
        if (index < 0 || index >= states.Count)
        {
            currentIndex = index;
            currentState = null;
            return;
        }

        GameObject go = states[index];
        if (go == null)
            return;

        currentIndex = index;
        go.SetActive(true);

        currentState = go.GetComponent<SimulationState>();
        if (currentState != null)
        {
            Debug.LogError("Started State : " + index + " " + currentState.promptText);
            currentState.StartState(this);
            SessionManager.Instance.ResetElapsedTime();

        }
    }


    private void StopCurrent()
    {
        if (currentState != null)
        {
            currentState.StopState();
            if (currentState.gameObject != null)
                currentState.gameObject.SetActive(false);

            currentState = null;
        }
    }

    public void NotifyStateComplete(SimulationState state)
    {
        if (state != currentState)
            return;


        // Allow PartsIdentificationManager to intercept in Free Roam
        if (PartsIdentificationManager.Instance != null &&
            PartsIdentificationManager.Instance.TryInterceptStateComplete(currentIndex))
            return;

        MoveToNextState();
    }

    public void SetMode(int value)
    {
        if (value == 0)
            SetSimulationType(SimulationMode.Guided);
        else if (value == 1)
            SetSimulationType(SimulationMode.Assessment);
        else if (value == 2)
            SetSimulationType(SimulationMode.FreeRoam); ;

        setModeUI.SetActive(false);

        StartSimulation();
    }

    private void StartSimulation()
    {
        currentState = states[0].GetComponent<SimulationState>();
        MoveToState(0);

        AssessmentManager.Instance?.BeginSession(); // if AssessmentManager exists in the scene, start the session

        //Sfx
        SoundManager.PlaySimulationStart();

        //event
        SimulationStart?.Invoke();
    }

    private void SetSimulationType(SimulationMode mode)
    {
        simulationMode = mode;
        switch (simulationMode)
        {
            case SimulationMode.Guided:
                Debug.Log("Simulation Mode set to Guided");
                break;

            case SimulationMode.Assessment:
                Debug.Log("Simulation Mode set to Assessment");
                break;

            case SimulationMode.FreeRoam:
                Debug.Log("Simulation Mode set to Free Roam");
                break;
        }
    }
}

public enum SimulationMode
{
    Guided,
    Assessment,
    FreeRoam
}

public enum GrabBehaviour
{
    Default,
    GrabWithPinch,
    GrabWithoutPinch
}

public enum StartupOverride
{
    None,
    SkipIntro,
    SkipAll
}