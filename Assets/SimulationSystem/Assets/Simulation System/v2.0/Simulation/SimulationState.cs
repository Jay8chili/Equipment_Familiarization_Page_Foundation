using SimulationSystem.V02.Assistant;
using SimulationSystem.V02.StateInteractions;
using SimulationSystem.V02.Utility;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

public class SimulationState : MonoBehaviour
{
    public int sessionLogID;

    [SerializeField] public List<Interactions> listOfInteractions = new List<Interactions>();

    [HideInInspector] public Interactions currentInteraction ;
    private int currentInteractionIndex;

    [Header("Prompt")]
    public string promptText;
    public AudioClip promptAudio;
    public bool MoveToNextStepAfterAudio;

    [Header("Teleport")]
    [Tooltip("If true, the player is teleported to Teleport Target before interactions begin.")]
    public bool teleportOnStart = false;
    [Tooltip("The transform the player will be teleported to when this state starts.")]
    public Transform teleportTarget;

    [Header("Timing")]
    [Tooltip("Seconds to wait after this state completes before moving to the next state.")]
    [Min(0f)]
    public float delayAfterState = 0f;

    [Tooltip("Seconds to wait between each interaction in the sequence.")]
    [Min(0f)]
    public float delayBetweenInteractions = 0f;

    public UnityEvent onStateStart = new UnityEvent();
    public UnityEvent onStateComplete = new UnityEvent();

    private SimulationManager simulationManager;
    private bool stateIsDone = false;
    private Coroutine sequenceCoroutine;

    // Set to true by OnTeleportDone once the teleport coroutine fully completes.
    private bool _teleportDone = false;

    // ─────────────────────────────────────────────
    // START STATE
    // ─────────────────────────────────────────────

    public void StartState(SimulationManager stateMachine)
    {
        simulationManager = stateMachine;
        stateIsDone = false;
        _teleportDone = false;
        //Checks what type/s of interaction this state has
        //This also handles Prompt Audio
        AssistantManager.Instance?.EvaluateStateInteractions(this);

        AssistantManager.Instance?.SetProgressFromSimulation(); // set the assistant progress bar based on the current state index in the SimulationManager

        AssessmentManager.Instance?.BeginState(this); // Notify the AssessmentManager that this state has begun, so it can start tracking for any assessments linked to this state

        onStateStart.Invoke();
        if (MoveToNextStepAfterAudio)
            return;     // AssistantManager.HidePromptAfter handles MoveToNextState

        if (listOfInteractions == null || listOfInteractions.Count == 0)
        {
            CompleteState();
            return;
        }

        // Reset every interaction up-front so none carry stale flags
        foreach (var i in listOfInteractions)
            if (i != null) i.ResetInteraction();

        Debug.Log("[AssistantManager] start state");

        // Wait for prompt to complete before starting sequence
        AssistantManager.PromptCompleted += OnPromptDone;

        // Kick off teleport before the sequence so RunSequence can wait on it.
        if (teleportOnStart && teleportTarget != null && TeleportManager.Instance != null)
        {
            TeleportManager.TeleportCompleted += OnTeleportDone;
            TeleportManager.Instance.UpdatePlayerPos(teleportTarget);
        }
        else
        {
            _teleportDone = true;
            //sequenceCoroutine = StartCoroutine(RunSequence());
        }
    }

    private void OnPromptDone()
    {
        AssistantManager.PromptCompleted -= OnPromptDone; // Unsubscribe immediately
        Debug.Log("[AssistantManager] Prompt completed");
        sequenceCoroutine = StartCoroutine(RunSequence());
    }

    private void OnTeleportDone()
    {
        _teleportDone = true;
        Debug.Log($"[SimulationState] Teleport complete on '{name}' — starting interactions.");
        //sequenceCoroutine = StartCoroutine(RunSequence());
        TeleportManager.TeleportCompleted -= OnTeleportDone;
    }

    // ─────────────────────────────────────────────
    // STOP STATE
    // ─────────────────────────────────────────────

    public void StopState()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        foreach (var interaction in listOfInteractions)
        {
            if (interaction == null) continue;
            interaction.StopInteraction();
            interaction.ResetInteraction();
        }

        stateIsDone = true;
    }

    // ─────────────────────────────────────────────
    // SEQUENCE COROUTINE
    // ─────────────────────────────────────────────

    private IEnumerator RunSequence()
    {
        for (int i = 0; i < listOfInteractions.Count; i++)
        {
            // ── Delay between interactions (skip before the first one) ───
            if (i > 0 && delayBetweenInteractions > 0f)
            {
                Debug.Log($"[SimulationState] Waiting {delayBetweenInteractions:F2}s between interactions on '{name}'");
                yield return new WaitForSeconds(delayBetweenInteractions);
            }
            Debug.Log($"[AssistantManager] Starting interaction [{i}] on '{name}'");
            currentInteraction = listOfInteractions[i];

            var interaction = listOfInteractions[i];
            if (interaction == null) continue;

            Debug.Log($"[SimulationState] ▶ Starting [{i}] {interaction.GetType().Name} on '{name}'");

            if (interaction is DetectInteraction || interaction is GazeInteraction)
            {
                interaction.gameObject.SetActive(true);
            }

            AssessmentManager.Instance?.OnInteractionStarted(interaction);  // Notify the AssessmentManager that an interaction has started, so it can track for any assessments linked to this interaction

            // Call StartInteraction directly (guaranteed virtual dispatch to the override).
            // Then fire the Inspector event so any external listeners also run.
            interaction.StartInteraction();
            interaction.OnActivateInteraction?.Invoke();

            AssessmentManager.Instance?.DisableVisualsAfterStart(interaction);

            int safeIndex = i;
            yield return new WaitUntil(() => listOfInteractions[safeIndex].HasInteractionEnded());

            AssessmentManager.Instance?.OnInteractionCompleted(interaction);    // Notify the AssessmentManager that an interaction has completed, so it can track for any assessments linked to this interaction


            Debug.Log($"[SimulationState] ✔ Completed [{i}] {interaction.GetType().Name} on '{name}'");

            // Same pattern on deactivate: call the method, then fire the event.
            interaction.StopInteraction();
            interaction.OnDeactivateInteraction?.Invoke();

            interaction.ResetInteraction();
        }

        CompleteState();
    }

    // ─────────────────────────────────────────────
    // COMPLETE STATE
    // ─────────────────────────────────────────────

    private void CompleteState()
    {
        if (stateIsDone) return;
        stateIsDone = true;
        onStateComplete.Invoke();

        AssessmentManager.Instance?.CloseState();   // Notify the AssessmentManager that this state has completed, so it can close out any assessments linked to this state

        //AssistantManager.Instance.SetProgressFromSimulation();

        // Wait before notifying SimulationManager to advance
        if (delayAfterState > 0f)
            StartCoroutine(DelayedNotify());
        else
            simulationManager.NotifyStateComplete(this);
    }

    private IEnumerator DelayedNotify()
    {
        Debug.Log($"[SimulationState] Waiting {delayAfterState:F2}s after state '{name}' before advancing.");
        yield return new WaitForSeconds(delayAfterState);
        simulationManager.NotifyStateComplete(this);
    }
}
