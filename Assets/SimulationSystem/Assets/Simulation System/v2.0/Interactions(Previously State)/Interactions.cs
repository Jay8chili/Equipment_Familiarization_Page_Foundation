using System;
using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using SimulationSystem.V02.Simulation.Managers;


public abstract class Interactions : MonoBehaviour
{
    #region properties
    public float Time;
    protected Timer InteractionTimer;
    public float elapsedTime;

    protected bool IsStarted;
    protected bool IsOngoing;
    protected bool IsSuspended;
    protected bool IsCompleted;

    protected Coroutine activeCoroutine;

    public bool canInteract = false;
    #endregion

    #region Radial UI
    [Header("Radial Progress UI")]
    // Base returns null — subclasses override to expose their radialUI reference
    protected virtual RadialInteractionUI RadialUI => null;
    #endregion

    #region Highlight

    [Header("Interaction Highlight")]
    [Tooltip("Show a progress fill + outline effect while this interaction is active.")]
    public bool enableHighlight = false;

    [Tooltip("Renderer to highlight. If empty, uses the Renderer on this GameObject.")]
    public Renderer highlightTarget;

    [Tooltip("Material using Bot/Interaction shader. Auto-created if left empty.")]
    public Material highlightMaterial;

    private static readonly int ID_Progress = Shader.PropertyToID("_Progress");
    private static readonly int ID_Visible = Shader.PropertyToID("_Visible");

    private Renderer _highlightRenderer;
    private MaterialPropertyBlock _highlightBlock;
    private Material[] _originalMaterials;
    private Material[] _highlightMaterials;
    private bool _highlightReady = false;

    private void SetupHighlight()
    {
        if (!enableHighlight) return;

        _highlightRenderer = highlightTarget != null
            ? highlightTarget
            : GetComponent<Renderer>();

        if (_highlightRenderer == null)
        {
            Debug.LogWarning($"[{GetType().Name}] enableHighlight is true but no Renderer found on " +
                             $"'{gameObject.name}'. Assign a Highlight Target in the Inspector.");
            enableHighlight = false;
            return;
        }

        if (highlightMaterial == null)
        {
            var shader = Shader.Find("Bot/InteractionForTransparentMat");
            if (shader == null)
            {
                Debug.LogError($"[{GetType().Name}] Shader 'Bot/Interaction' not found. Highlight disabled.");
                enableHighlight = false;
                return;
            }
            highlightMaterial = new Material(shader);
        }

        _highlightBlock = new MaterialPropertyBlock();
        _originalMaterials = _highlightRenderer.sharedMaterials;

        // One extra slot — the single combined material handles both outline and progress.
        _highlightMaterials = new Material[_originalMaterials.Length + 1];
        _originalMaterials.CopyTo(_highlightMaterials, 0);
        _highlightMaterials[_highlightMaterials.Length - 1] = highlightMaterial;

        // Add the slot permanently so HideHighlight/ShowHighlight only toggle
        // _Visible and _Progress — the slot itself never gets removed.
        _highlightRenderer.materials = _highlightMaterials;

        _highlightReady = true;

        // Start fully hidden
        SetProgressAndVisibility(0f, false);
    }

    protected virtual void OnRendererReady(Renderer renderer) { }

    private void SetProgressAndVisibility(float progress, bool outlineVisible)
    {
        if (!_highlightReady || _highlightRenderer == null) return;
        _highlightRenderer.GetPropertyBlock(_highlightBlock);
        _highlightBlock.SetFloat(ID_Progress, Mathf.Clamp01(progress));
        _highlightBlock.SetFloat(ID_Visible, outlineVisible ? 1f : 0f);
        _highlightRenderer.SetPropertyBlock(_highlightBlock);
    }

    private void SetHighlightProgress(float progress)
    {
        if (!enableHighlight || !_highlightReady || _highlightRenderer == null) return;
        _highlightRenderer.GetPropertyBlock(_highlightBlock);
        _highlightBlock.SetFloat(ID_Progress, Mathf.Clamp01(progress));
        _highlightRenderer.SetPropertyBlock(_highlightBlock);
    }

    // ShowHighlight / HideHighlight now just toggle _Visible and reset _Progress.
    // The material slot stays in the array permanently.
    private void ShowHighlight()
    {
        if (!enableHighlight || !_highlightReady) return;
        SetProgressAndVisibility(0f, true);    // progress starts at 0, overlay visible
    }

    private void HideHighlight()
    {
        if (!_highlightReady) return;
        SetProgressAndVisibility(0f, false);   // both off
    }

    #endregion

    // Outline is now handled by the combined shader via _Visible.
    // Subclasses no longer need a separate material slot — this base
    // implementation drives _Visible on the shared highlight material.
    protected virtual void SetOutlineVisible(bool visible)
    {
        if (!enableHighlight || !_highlightReady || _highlightRenderer == null) return;
        _highlightRenderer.GetPropertyBlock(_highlightBlock);
        _highlightBlock.SetFloat(ID_Visible, visible ? 1f : 0f);
        _highlightRenderer.SetPropertyBlock(_highlightBlock);
    }




    // ── Activation Events ─────────────────────────────────────────────────────
    // Called externally to activate / deactivate this interaction.
    // Distinct from the lifecycle events below which fire during the interaction itself.

    [Header("Activation")]
    [Tooltip("Invoke to activate this interaction (calls StartInteraction).")]
    public UnityEvent OnActivateInteraction;

    [Tooltip("Invoke to deactivate this interaction (calls StopInteraction).")]
    public UnityEvent OnDeactivateInteraction;

    // OnActivateInteraction / OnDeactivateInteraction are for external Inspector hooks only.
    // SimulationState calls StartInteraction() / StopInteraction() directly via virtual dispatch,
    // so no runtime listener is needed here. Adding one would cause a double-call.
    private void SetupActivationEvents() { }

    // ── Lifecycle Events (fired by the interaction itself) ────────────────────
    #region UnityEvents
    public UnityEvent OnInteractionStartedEvent;
    public UnityEvent<float> OnInteractionOngoingEvent;
    public UnityEvent OnInteractionSuspendedEvent;
    public UnityEvent OnInteractionCompletedEvent;

    protected Action OnInteractionTimerStartedEvent;
    protected Action OnInteractionTimerEndedEvent;
    /*
 * make Wrong Ineraction events
    public UnityEvent OnInteractionStartedEvent;
    public UnityEvent<float> OnInteractionOngoingEvent;
    public UnityEvent OnInteractionSuspendedEvent;
    public UnityEvent OnInteractionCompletedEvent;*/
    #endregion

    #region Override Methods

    public virtual void OnEnable()
    {
        InteractionTimer.OnTimerStart += OnInteractionTimerStart;
        InteractionTimer.OnTimerEnd += OnInteractionTimerEnd;
        InteractionTimer.OnTimerRunning += GetElapsedTimeValue;
    }

    public virtual void OnDisable()
    {
        InteractionTimer.OnTimerStart -= OnInteractionTimerStart;
        InteractionTimer.OnTimerEnd -= OnInteractionTimerEnd;
        InteractionTimer.OnTimerRunning -= GetElapsedTimeValue;
    }

    public virtual void Awake()
    {
        InteractionTimer = new(Time);
        SetupHighlight();
        SetupActivationEvents();
    }

    public virtual void Start() { }
    public virtual void Update() { }

    public virtual void StartInteraction()
    {
        Debug.Log("base up Hello 234");
        Debug.Log($"[{GetType().Name}] StartInteraction called — frame {UnityEngine.Time.frameCount}");
        canInteract = true;
        SetOutlineVisible(true);

        RadialUI?.Show();   // Shows the radial UI if this interaction has one assigned
    }

    public virtual void StopInteraction()
    {
        canInteract = false;
        RadialUI?.Hide();   // Hides the radial UI if this interaction has one assigned
    }

    public virtual void StartTimer()
    {
        InteractionTimer.StartTimer();
    }

    public virtual void StopTimer(bool resetTimer)
    {
        InteractionTimer.StopTimer(resetTimer);
    }

    private void GetElapsedTimeValue(float TimerValue)
    {
        elapsedTime = TimerValue;

        // TimerValue is already normalized 0..1 by Timer.UpdateTimer via InverseLerp
        if (enableHighlight && _highlightReady)
            SetHighlightProgress(elapsedTime);

        RadialUI?.SetProgress(elapsedTime);     // Updates the radial UI 

        OnInteractionUpdate();
        OnInteractionOngoingEvent?.Invoke(TimerValue);
    }

    public virtual void OnInteractionTimerEnd()
    {
        OnInteractionComplete();
    }

    public virtual void OnInteractionTimerStart() { }

    #endregion


    #region Abstract Methods
    protected abstract IEnumerator ExecuteInteraction();
    #endregion


    #region Interaction Logic

    public virtual void OnInteractionStart()
    {
        if (IsStarted) return;

        IsOngoing = false;
        IsCompleted = false;
        IsSuspended = false;
        IsStarted = true;

        ShowHighlight();
        SetOutlineVisible(false);   // outline off while actively interacting

        RadialUI?.Show();    // re-assert here — counteracts any Hide() that snuck in

        SoundManager.PlayInteractionStart();

        //RadialUI?.Show();   // Shows the radial UI if this interaction has one assigned

        OnInteractionStartedEvent?.Invoke();
    }

    public virtual void OnInteractionUpdate()
    {
        IsSuspended = false;
        IsStarted = false;
        IsCompleted = false;
        IsOngoing = true;

        SoundManager.PlayInteractionOngoing();
    }

    public virtual void OnInteractionComplete()
    {
        // FIX: Guard against double-completion. If already completed,
        // don't re-fire events or re-stop coroutines.
        if (IsCompleted) return;

        IsSuspended = false;
        IsStarted = false;
        IsOngoing = false;
        IsCompleted = true;

        SetHighlightProgress(1f);
        HideHighlight();
        SetOutlineVisible(false);


        SoundManager.PlayInteractionComplete();


        OnInteractionCompletedEvent?.Invoke();

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        RadialUI?.SetProgress(1f);  // Ensure radial UI shows full progress at completion           
        RadialUI?.Hide();   // Hides the radial UI at the end of the interaction
    }

    public virtual void OnInteractionSuspend()
    {
        if (IsSuspended) return;

        // FIX: Once an interaction is completed, a late suspend (e.g. player
        // releasing the grabbed object after the timer already finished)
        // must NOT overwrite the IsCompleted flag. Without this guard the
        // WaitUntil(() => HasInteractionEnded()) in SimulationState never
        // sees true, and the state machine gets permanently stuck.
        if (IsCompleted) return;

        IsOngoing = false;
        IsStarted = false;
        IsCompleted = false;
        IsSuspended = true;

        HideHighlight();
        SetOutlineVisible(true);    // outline shows when interaction is suspended

        SoundManager.PlayInteractionSuspend();

        OnInteractionSuspendedEvent?.Invoke();
    }

    #endregion


    #region Return Current Interaction State

    public bool HasInteractionStarted() => IsStarted;
    public bool HasInteractionEnded() => IsCompleted;
    public bool HasInteractioncancelled() => IsSuspended;
    public bool IsInteractionBeingPerformed() => IsOngoing;

    #endregion


    public virtual void ResetInteraction()
    {
        IsStarted = false;
        IsOngoing = false;
        IsSuspended = false;
        IsCompleted = false;
        elapsedTime = 0f;

        HideHighlight();
        SetHighlightProgress(0f);
        SetOutlineVisible(false);

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        if (InteractionTimer != null)
            StopTimer(true);

        // Only hide radial UI if interaction is not active
        // ResetInteraction fires during state setup AFTER StartInteraction
        // which would wipe the Show() call
        if (!canInteract)
            RadialUI?.Hide();
        else
            RadialUI?.SetProgress(0f);  // just reset progress, keep it visible

        Debug.Log($"[{GetType().Name}] Reset");
        Debug.Log($"[{GetType().Name}] ResetInteraction called — frame {UnityEngine.Time.frameCount}");
    }

    public void SetRadialUIVisible(bool visible)
    {
        if (visible) RadialUI?.Show();
        else RadialUI?.Hide();
    }
}
