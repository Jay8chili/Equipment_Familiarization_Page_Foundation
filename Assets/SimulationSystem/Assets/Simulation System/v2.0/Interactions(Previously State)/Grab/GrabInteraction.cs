using System;
using UnityEngine;
using System.Threading;
using System.Collections;
using UnityEngine.Events;
using System.Threading.Tasks;
using System.Collections.Generic;
using SimulationSystem.V02.Utility;
using SimulationSystem.V02.Simulation.Managers;

/// <summary>
/// Grab via offset math in LateUpdate. Zero hierarchy changes.
///
/// TWO GRAB PATHS
/// ───────────────
/// GrabWithPinch (default):
///   GrabPinchDetector detects pinch → calls TryGrabFrom directly. Same as before.
///
/// GrabWithoutPinch (proximity):
///   OnTriggerEnter detects the hand collider entering this object's trigger.
///   A coroutine waits grabThisObjectIn seconds, re-validates all conditions,
///   then calls TryGrabFrom and notifies the detector so its release flow works.
///   Hand exiting cancels the timer. canInteract going false cancels the timer.
///
/// FOUR POSE SLOTS
/// ────────────────
///   poseHandLeft / poseHandRight / poseControllerLeft / poseControllerRight
///   Resolved automatically by InputMode + Handedness.
///
/// OFFSET MATH — unchanged. See LateUpdate.
/// </summary>
public class GrabInteraction : Interactions, IGrab
{
    public bool DontRelaseOnStateEnd;
    // ── Inspector — Behaviour ────────────────────────────────────────────────

    [Header("Grab Behaviour")]
    [Tooltip("Default = follow SimulationManager's global setting.\n" +
             "Override per-object to force pinch or no-pinch regardless of the global.")]
    public GrabBehaviour grabBehaviourOverride = GrabBehaviour.Default;

    [Header("Proximity Grab (GrabWithoutPinch)")]
    [Tooltip("Seconds the hand must remain inside the collider before the grab fires.\n" +
             "0 = grab on the same frame the hand enters.")]
    [Min(0f)]
    public float grabThisObjectIn = 0.5f;
    public bool resetTimer;

    public bool TeleportThisObject;

    [Header("Radial Progress UI")]
    public RadialInteractionUI radialUI;
    protected override RadialInteractionUI RadialUI => radialUI;

    // ── Inspector — Proximity Grab UI ────────────────────────────────────────

    [Header("Proximity Grab UI")]
    [Tooltip("Parent GameObject of the ProximityGrabUI canvas. Set its localScale " +
             "in the scene to whatever size your canvas needs (e.g. 0.0005). " +
             "At runtime we cache that scale and pop between 0 and that value.")]
    public GameObject proximityUIHolder;

    [Tooltip("Duration of the pop-in and pop-out bounce animations (seconds).")]
    [Min(0f)]
    public float proximityUIPopDuration = 0.2f;

    // ── Inspector — Poses ────────────────────────────────────────────────────

    [Header("Poses — Hand Tracking")]
    [Tooltip("Pose applied when grabbed by hand-tracking LEFT hand. Null = free grab.")]
    public RecordedPose poseHandLeft;

    [Tooltip("Pose applied when grabbed by hand-tracking RIGHT hand. Null = free grab.")]
    public RecordedPose poseHandRight;

    [Header("Poses — Controller")]
    [Tooltip("Pose applied when grabbed by controller LEFT hand. Null = free grab.")]
    public RecordedPose poseControllerLeft;

    [Tooltip("Pose applied when grabbed by controller RIGHT hand. Null = free grab.")]
    public RecordedPose poseControllerRight;

    [Tooltip("World-space position nudge applied on top of pose placement.")]
    public Vector3 posePositionOffset;

    [Header("Reset")]
    public bool resetOnRelease;
    [Min(0f)]
    public float resetDelay = 2f;
    [Min(0.01f)]
    public float resetDuration = 0.5f;
    public Ease resetEase = Ease.OutCubic;

    [Header("Events")]
    public UnityEvent onGrabbed;
    public UnityEvent onReleased;
    public UnityEvent onResetComplete;

    public GrabHighlightController grabHighlightController;
    // ── Outline ──────────────────────────────────────────────────────────────


    [Tooltip("Renderer to show the outline on. If empty, uses the Renderer on this GameObject.")]
    public List<Renderer> AllRenderersOfThisGrab;

    public Dictionary<Renderer, List<Material>> CachedMaterialDictonary;
    public Dictionary<Renderer, List<Material>> MainDictonary;
    [Tooltip("Material using Bot/InteractionOutline shader. Auto-created if left empty.")]


    private static readonly int ID_Visible = Shader.PropertyToID("_Visible");
    private MaterialPropertyBlock _outlineBlock;
    private Renderer _outlineRenderer;
    private bool _outlineReady = false;



    // ── State ────────────────────────────────────────────────────────────────

    [Header("Debug — read only")]
    [SerializeField] private bool _isGrabbed;
    public bool isGrabbed => _isGrabbed;

    /// <summary>
    /// Resolved grab behaviour for this object.
    /// If override is Default, reads from SimulationManager. Otherwise uses the override.
    /// </summary>
    public GrabBehaviour ResolvedGrabBehaviour
    {
        get
        {
            if (grabBehaviourOverride != GrabBehaviour.Default)
                return grabBehaviourOverride;

            if (SimulationManager.Instance != null)
                return SimulationManager.Instance.grabBehaviour;

            return GrabBehaviour.GrabWithPinch;
        }
    }

    // ── Grab state ───────────────────────────────────────────────────────────

    private Transform _handRoot;

    private HandPoseLock _handLock;
    private ControllerPoseLock _controllerLock;

    private RecordedPose _activePose;

    // Scale-immune offsets
    private Vector3 _rootToContactLS;
    private Vector3 _objToContactLS;
    private Quaternion _rootToObjRot;

    // ── Proximity grab state ─────────────────────────────────────────────────

    // Only one hand can be pending at a time. Second hand entering is ignored.
    private Coroutine _proximityCoroutine;
    private GrabPinchDetector _pendingDetector;

    // Set true by ProximityGrabRoutine before calling TryGrabFrom so the
    // GrabWithoutPinch guard lets it through. Cleared immediately after.
    private bool _isPinchBypassedByProximity;

    // Tracks which detector proximity-grabbed this object so LateUpdate can
    // auto-release when the hand root leaves the trigger zone.
    private GrabPinchDetector _proximityGrabbedByDetector;

    // Tracks whether the UI pop-in has finished so we know it's safe to pop-out.
    private bool _uiPopInComplete;

    // Cached at Awake from proximityUIHolder
    private ProximityGrabUI _proxUI;
    private Vector3 _proxUITargetScale;
    private CancellationTokenSource _proxUICts;

    // ── Proximity overlap check ──────────────────────────────────────────────

    // Reusable buffer for the overlap check in CheckForHandAlreadyInside.
    private readonly Collider[] _overlapBuffer = new Collider[16];

    // ── Reset ────────────────────────────────────────────────────────────────

    private MyTransform _spawnTransform;
    private bool _spawnCaptured;
    private CancellationTokenSource _resetCts;

    [HideInInspector] public MyTransform InitialPositionRotation;


    // ── Lifecycle ────────────────────────────────────────────────────────────

    public override void Awake()
    {
        InitialPositionRotation = new MyTransform(this.transform);
        Debug.Log("Captured" + InitialPositionRotation.GetThisTransform().Position);

        grabHighlightController = transform.GetComponent<GrabHighlightController>();


        grabHighlightController.HideHighlight();
        GetAllRenderers();
        base.Awake();
        CaptureSpawnTransform();

        onGrabbed.AddListener(OnInteractionStart);
        onReleased.AddListener(OnInteractionSuspend);

        // Cache the ProximityGrabUI from the holder's children and its scene scale
        if (proximityUIHolder != null)
        {
            _proxUI = proximityUIHolder.GetComponentInChildren<ProximityGrabUI>(true);
            _proxUITargetScale = proximityUIHolder.transform.localScale;

            if (_proxUITargetScale.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning($"[GrabInteraction] proximityUIHolder on '{gameObject.name}' has " +
                                 "zero scale in scene — defaulting to Vector3.one. Set the holder's " +
                                 "scale in the Inspector to the desired canvas size.", this);
                _proxUITargetScale = Vector3.one;
            }

            proximityUIHolder.transform.localScale = Vector3.zero;
            proximityUIHolder.SetActive(false);

            if (_proxUI == null)
                Debug.LogWarning($"[GrabInteraction] proximityUIHolder on '{gameObject.name}' " +
                                 "has no ProximityGrabUI in children.", this);
        }


        // this.onGrabbed.AddListener(ResetHighlightedShader);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        CancelProximityTimer();

        _resetCts?.Cancel();
        _proxUICts?.Cancel();
        onGrabbed.RemoveListener(OnInteractionStart);
        onReleased.RemoveListener(OnInteractionSuspend);
        if (_isGrabbed) ReleaseInternal();
    }


    private void OnDestroy() => _resetCts?.Cancel();

    public override void Update() { }

    private void LateUpdate()
    {
        if (!_isGrabbed || _handRoot == null) return;

        Vector3 contactWorld = _handRoot.position + _handRoot.rotation * _rootToContactLS;
        Quaternion objRot = _handRoot.rotation * _rootToObjRot;
        Vector3 objPos = contactWorld - objRot * _objToContactLS;

        transform.SetPositionAndRotation(objPos, objRot);
    }

    // ── Interaction overrides ────────────────────────────────────────────────
    public override void StartInteraction()
    {
        base.StartInteraction();

        OnInteractionStarted();
    }

    // This is also used by Detect Interaction 
    public void OnInteractionStarted()
    {
        canInteract = true;
        Debug.Log("override Hello 1234");
        if (TeleportThisObject)
        {
            var movHelper = gameObject.GetComponent<MoveToHelper>();
            if (movHelper != null)
            {
                movHelper.TeleportThisObject();
            }
        }

        // FIX: After canInteract becomes true, check if a hand is already
        // sitting inside our trigger collider.
        if (ResolvedGrabBehaviour == GrabBehaviour.GrabWithoutPinch)
        {
            CheckForHandAlreadyInside();
        }

        Debug.Log("Hello 1234");

        // Highlight — hidden in assessment mode until hint taken
        if (SimulationManager.Instance?.simulationMode != SimulationMode.Assessment ||
            AssessmentManager.Instance?.IsHintTaken == true)
        {
            grabHighlightController.ShowHighlight();
            RadialUI?.Show();
        }

        /*grabHighlightController.ShowHighlight();
        //SetHighlightShader();
        RadialUI?.Show();*/
    }
    protected override IEnumerator ExecuteInteraction() { yield break; }

    /// <summary>
    /// Called by SimulationState when this interaction should stop being active.
    /// Cancels any running proximity timer and force-releases the object.
    /// </summary>
    public override void StopInteraction()
    {
        base.StopInteraction();          // sets canInteract = false
        CancelProximityTimer();
        if (_isGrabbed) ReleaseInternal();
    }

    public override void ResetInteraction()
    {
        CancelProximityTimer();
        if (_isGrabbed) ReleaseInternal();
        base.ResetInteraction();
    }

    public override void OnInteractionComplete()
    {
        base.OnInteractionComplete();


    }


    public override void OnInteractionStart()
    {
        base.OnInteractionStart();
        InteractionTimer.StartTimer();
        grabHighlightController.HideHighlight();
        //ResetHighlightedShader();
    }

    public override void OnInteractionSuspend()
    {
        if (IsCompleted) return;

        base.OnInteractionSuspend();
        InteractionTimer.StopTimer(resetTimer);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PROXIMITY GRAB UI HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Enable the holder, scale from 0 → cached target scale with OutBack bounce.
    /// Sets _uiPopInComplete when the tween finishes.
    /// </summary>
    private void ShowProximityUI()
    {
        _proxUI.Initialize("0");

        if (proximityUIHolder == null) return;

        _uiPopInComplete = false;

        _proxUICts?.Cancel();
        _proxUICts = new CancellationTokenSource();

        if (_proxUI != null) _proxUI.SetProgress(0f);

        proximityUIHolder.transform.localScale = Vector3.zero;
        proximityUIHolder.SetActive(true);

        _ = proximityUIHolder.transform.DoScale(_proxUITargetScale, proximityUIPopDuration,
            Ease.OutBack, _proxUICts.Token,
            onComplete: () => { _uiPopInComplete = true; });
    }

    /// <summary>
    /// Scale holder from current → 0 with InBack, then disable it.
    /// If pop-in never finished, snap-hides instead.
    /// </summary>
    private void HideProximityUI()
    {
        if (proximityUIHolder == null) return;

        if (!_uiPopInComplete)
        {
            KillProximityUI();
            return;
        }

        _proxUICts?.Cancel();
        _proxUICts = new CancellationTokenSource();

        _ = proximityUIHolder.transform.DoScale(Vector3.zero, proximityUIPopDuration,
            Ease.InBack, _proxUICts.Token,
            onComplete: () =>
            {
                if (proximityUIHolder != null) proximityUIHolder.SetActive(false);
            });
    }

    /// <summary>Hard-kill: cancel tween, snap scale to 0, disable. No animation.</summary>
    private void KillProximityUI()
    {
        if (proximityUIHolder == null) return;

        _proxUICts?.Cancel();
        _proxUICts = null;
        proximityUIHolder.transform.localScale = Vector3.zero;
        proximityUIHolder.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PROXIMITY GRAB (GrabWithoutPinch)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Manual overlap check run once at the end of StartInteraction.
    /// </summary>
    private void CheckForHandAlreadyInside()
    {
        if (_isGrabbed) return;
        if (_proximityCoroutine != null) return;

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null) return;

        // Use the collider's actual orientation — Quaternion.identity is wrong for rotated triggers
        Vector3 center, halfExtents;
        Quaternion orientation;

        if (triggerCollider is BoxCollider box)
        {
            center = transform.TransformPoint(box.center);
            halfExtents = Vector3.Scale(box.size * 0.5f, transform.lossyScale);
            orientation = transform.rotation;
        }
        else
        {
            Bounds bounds = triggerCollider.bounds;
            center = bounds.center;
            halfExtents = bounds.extents;
            orientation = Quaternion.identity;
        }

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            _overlapBuffer,
            orientation,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider other = _overlapBuffer[i];
            if (other == null) continue;
            if (other.transform.IsChildOf(transform)) continue;

            var detector = other.GetComponentInParent<GrabPinchDetector>();
            if (detector == null) continue;
            if (detector.hasGrabbedObject) continue;
            Debug.Log($"[GrabInteraction] Hand already inside '{gameObject.name}' " +
                      $"on StartInteraction — starting proximity grab.");
            _pendingDetector = detector;
            _proximityCoroutine = StartCoroutine(ProximityGrabRoutine(detector));
            return;
        }
    }

    /// <summary>
    /// Fires when any collider enters this object's trigger.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (ResolvedGrabBehaviour != GrabBehaviour.GrabWithoutPinch) return;
        if (!canInteract) return;
        if (_isGrabbed) return;
        if (_proximityCoroutine != null) return;

        var detector = other.GetComponentInParent<GrabPinchDetector>();
        if (detector == null) return;
        if (detector.hasGrabbedObject) return;

        _pendingDetector = detector;
        _proximityCoroutine = StartCoroutine(ProximityGrabRoutine(detector));
    }

    /// <summary>
    /// Fires when a collider exits. Only cancels if it belongs to the pending detector.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (_proximityCoroutine == null) return;
        if (_pendingDetector == null) return;

        var detector = other.GetComponentInParent<GrabPinchDetector>();
        if (detector != _pendingDetector) return;

        CancelProximityTimer();
    }

    /// <summary>
    /// Wait grabThisObjectIn seconds while driving the proximity UI,
    /// then re-validate and fire the grab.
    ///
    /// UI lifecycle:
    ///   1. PopIn (bounce up)  — at coroutine start
    ///   2. SetProgress(0..1)  — every frame during the wait
    ///   3. PopOut (bounce down, then disable) — on grab fire or cancel
    /// </summary>
    private IEnumerator ProximityGrabRoutine(GrabPinchDetector detector)
    {
        // ── Show UI with bounce-in ───────────────────────────────────────────
        ShowProximityUI();

        // ── Wait phase — manual timer so we can drive fill progress ──────────
        if (grabThisObjectIn > 0f)
        {
            float elapsed = 0f;
            while (elapsed < grabThisObjectIn)
            {
                if (!canInteract || _isGrabbed || detector == null
                    || detector.hasGrabbedObject
                    || ResolvedGrabBehaviour != GrabBehaviour.GrabWithoutPinch)
                {
                    // Early abort — hide UI and bail
                    HideProximityUI();
                    ClearProximityState();
                    yield break;
                }

                elapsed += UnityEngine.Time.deltaTime;

                HapticManager.OnProximityGrab(HapticHand.Right);


                HapticManager.OnProximityGrab(HapticHand.Left);


                // Drive the radial fill
                if (_proxUI != null)
                {
                    _proxUI.SetProgress(Mathf.Clamp01(elapsed / grabThisObjectIn));
                    _proxUI.SetText(((int)(grabThisObjectIn - elapsed)).ToString());
                    if (elapsed >= grabThisObjectIn)
                    {
                        _proxUI.SetText("0");
                    }


                }
                yield return null;
            }
        }

        // Snap fill to 100%
        if (_proxUI != null)
            _proxUI.SetProgress(1f);

        // ── Re-validate after wait ───────────────────────────────────────────
        if (!canInteract
            || _isGrabbed
            || detector == null
            || detector.hasGrabbedObject
            || ResolvedGrabBehaviour != GrabBehaviour.GrabWithoutPinch)
        {
            HideProximityUI();
            ClearProximityState();
            yield break;
        }

        Transform trackingRoot = detector.TrackingRoot;
        if (trackingRoot == null)
        {
            Debug.LogWarning("GrabInteraction: proximity grab aborted — detector has no tracking root.", this);
            HideProximityUI();
            ClearProximityState();
            yield break;
        }

        // ── Pop UI out before firing the grab ────────────────────────────────
        HideProximityUI();

        // ── Fire the grab ────────────────────────────────────────────────────
        Transform pinchTransform = detector.pinchPoint != null
            ? detector.pinchPoint
            : trackingRoot;

        HandPoseLock hLock = detector.inputMode == InputMode.HandTracking ? detector.handPoseLock : null;
        ControllerPoseLock cLock = detector.inputMode == InputMode.Controller ? detector.controllerPoseLock : null;

        _isPinchBypassedByProximity = true;
        TryGrabFrom(pinchTransform, trackingRoot, detector.inputMode, detector.handedness, hLock, cLock);
        _isPinchBypassedByProximity = false;

        _proximityGrabbedByDetector = detector;   // enables LateUpdate auto-release on hand exit
        detector.NotifyProximityGrab(this);

        ClearProximityState();
    }

    private void CancelProximityTimer()
    {
        if (_proximityCoroutine != null)
        {
            StopCoroutine(_proximityCoroutine);
            _proximityCoroutine = null;
        }
        _pendingDetector = null;

        // Hide the UI — use animated pop-out if pop-in finished, otherwise hard-kill
        HideProximityUI();
    }

    /// <summary>Null out state without stopping the coroutine (it finished naturally).</summary>
    private void ClearProximityState()
    {
        _proximityCoroutine = null;
        _pendingDetector = null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PINCH GRAB (existing path — called by GrabPinchDetector)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Begin a grab.
    ///
    /// OPERATION ORDER (critical for hand tracking):
    ///   1. Lock hand pose → fingers snap to recorded positions.
    ///   2. Read pinchTransform.position → now reflects recorded finger pose.
    ///   3. Set object rotation from recorded handToObjectRotOffset.
    ///   4. Set object position at recorded distance from pinch point.
    ///   5. Compute follow offsets against the final placed state.
    ///   6. Mark grabbed → LateUpdate follow begins.
    /// </summary>
    public void TryGrabFrom(
        Transform pinchTransform,
        Transform handRoot,
        InputMode inputMode,
        Handedness handedness,
        HandPoseLock handPoseLock = null,
        ControllerPoseLock controllerPoseLock = null)
    {
        if (handRoot == null)
        {
            Debug.LogError("GrabInteraction.TryGrabFrom: handRoot is null.", this);
            return;
        }

        // ── GUARD: interaction must be active ────────────────────────────
        if (!canInteract)
        {
            Debug.Log($"GrabInteraction: grab rejected on '{gameObject.name}' — canInteract is false.", this);
            return;
        }

        // ── GUARD: pinch grab blocked when set to GrabWithoutPinch ──────
        if (ResolvedGrabBehaviour == GrabBehaviour.GrabWithoutPinch && !_isPinchBypassedByProximity)
        {
            Debug.Log($"GrabInteraction: pinch grab rejected on '{gameObject.name}' — " +
                      "behaviour is GrabWithoutPinch, use proximity grab.", this);
            return;
        }

        if (_isGrabbed) ReleaseInternal();

        _resetCts?.Cancel();

        _handRoot = handRoot;
        _handLock = handPoseLock;
        _controllerLock = controllerPoseLock;

        _activePose = ResolvePose(inputMode, handedness);

        // ── STEP 1: LOCK HAND POSE ──────────────────────────────────────
        if (_activePose != null)
        {
            if (inputMode == InputMode.HandTracking)
                _handLock?.LockPose(_activePose);
            else
                _controllerLock?.LockPose(_activePose);
        }

        // ── STEP 2: READ PINCH POSITION AFTER LOCK ─────────────────────
        Vector3 pinchPos = pinchTransform != null
            ? pinchTransform.position
            : handRoot.position;

        // ── STEP 3 & 4: PLACE OBJECT ────────────────────────────────────
        if (_activePose != null && _activePose.hasAnchorData)
            PlaceForPose(pinchPos);

        // ── STEP 5: COMPUTE FOLLOW OFFSETS ──────────────────────────────
        ComputeOffsets(pinchPos);

        // ── STEP 6: MARK GRABBED ────────────────────────────────────────
        _isGrabbed = true;

        SoundManager.PlayOnGrab();
        onGrabbed?.Invoke();
    }

    public void OnPinchCanceled()
    {
        if (_isGrabbed)
        {
            _isGrabbed = false;
            _handRoot = null;

            _handLock?.UnlockPose();
            _handLock = null;

            _controllerLock?.UnlockPose();
            _controllerLock = null;

            _activePose = null;

            // Clear the detector's held-object state for proximity grabs so
            // it doesn't think it's still holding the object after release.
            _proximityGrabbedByDetector?.ClearCurrentGrab();
            _proximityGrabbedByDetector = null;

            bool doReset = resetOnRelease && !_suppressNextReset;
            _suppressNextReset = false;

            onReleased?.Invoke();
            if (doReset) BeginReset();
        }
    }

    // ── Pose resolution ──────────────────────────────────────────────────────

    private RecordedPose ResolvePose(InputMode mode, Handedness hand)
    {
        if (mode == InputMode.HandTracking)
        {

            return hand == Handedness.Left ? poseHandLeft : poseHandRight;
        }
        else
        {
            return hand == Handedness.Left ? poseControllerLeft : poseControllerRight;
        }
    }

    // ── Pose placement ───────────────────────────────────────────────────────

    private void PlaceForPose(Vector3 pinchPos)
    {
        Quaternion desiredRot = _handRoot.rotation * _activePose.handToObjectRotOffset;

        Vector3 desiredPos;

        if (_activePose.recordedPinchToObjectDistance > 0.0001f)
        {
            Vector3 worldDir = _handRoot.rotation * _activePose.recordedPinchToObjectDirLS;
            desiredPos = pinchPos + worldDir * _activePose.recordedPinchToObjectDistance;
        }
        else
        {
            desiredPos = pinchPos;
        }

        transform.SetPositionAndRotation(desiredPos + posePositionOffset, desiredRot);
    }

    // ── Offsets ──────────────────────────────────────────────────────────────

    private void ComputeOffsets(Vector3 contactPointWorld)
    {
        _rootToContactLS = Quaternion.Inverse(_handRoot.rotation)
                           * (contactPointWorld - _handRoot.position);

        _objToContactLS = Quaternion.Inverse(transform.rotation)
                           * (contactPointWorld - transform.position);

        _rootToObjRot = Quaternion.Inverse(_handRoot.rotation) * transform.rotation;
    }

    // ── Release ────────────────────────────────────────────────────────────
    public void ForceUngrab()
    {
        ResetHighlightedShader();
        _isGrabbed = false;
        _handRoot = null;

        _handLock?.UnlockPose();
        _handLock = null;

        _controllerLock?.UnlockPose();
        _controllerLock = null;

        _activePose = null;

        // Clear the detector's held-object state for proximity grabs so
        // it doesn't think it's still holding the object after release.
        _proximityGrabbedByDetector?.ClearCurrentGrab();
        _proximityGrabbedByDetector = null;

        bool doReset = resetOnRelease && !_suppressNextReset;
        _suppressNextReset = false;

        onReleased?.Invoke();
        if (doReset) BeginReset();

    }



    private void ReleaseInternal()
    {
        if (!DontRelaseOnStateEnd)
        {

            _isGrabbed = false;
            _handRoot = null;

            _handLock?.UnlockPose();
            _handLock = null;

            _controllerLock?.UnlockPose();
            _controllerLock = null;

            _activePose = null;

            // Clear the detector's held-object state for proximity grabs so
            // it doesn't think it's still holding the object after release.
            _proximityGrabbedByDetector?.ClearCurrentGrab();
            _proximityGrabbedByDetector = null;

            bool doReset = resetOnRelease && !_suppressNextReset;
            _suppressNextReset = false;

            onReleased?.Invoke();
            if (doReset) BeginReset();
        }
    }


    // ── Release ── helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Release the grab but skip BeginReset for this one release.
    /// Use this when an external system (e.g. DetectInteraction) will handle
    /// moving the object itself — calling OnPinchCanceled directly would start
    /// BeginReset which fights the external move.
    /// </summary>
    public void ForceReleaseSuppressReset()
    {
        _suppressNextReset = true;
        OnPinchCanceled();
    }

    // ── Reset ────────────────────────────────────────────────────────────────

    private bool _suppressNextReset;

    private void CaptureSpawnTransform()
    {
        if (_spawnCaptured) return;
        _spawnTransform = new MyTransform(transform);
        _spawnCaptured = true;
    }

    private async void BeginReset()
    {
        _resetCts?.Cancel();
        _resetCts = new CancellationTokenSource();
        CancellationToken token = _resetCts.Token;
        try
        {
            if (resetDelay > 0f)
                await Task.Delay(TimeSpan.FromSeconds(resetDelay), token);

            var spawnData = _spawnTransform.GetThisTransform();
            await Task.WhenAll(
                transform.DoMove(spawnData.Position, resetDuration, resetEase, token),
                transform.DoRotateQuaternion(spawnData.Rotation, resetDuration, resetEase, token)
            );

            if (!token.IsCancellationRequested)
                onResetComplete?.Invoke();
        }
        catch (OperationCanceledException) { }
    }



    #region  Grab Highlight  =════════════════════════════════════════════════════════════════

    [ContextMenu("Getrenderers")]
    public void GetAllRenderers()
    {
        AllRenderersOfThisGrab = new List<Renderer>();
        CachedMaterialDictonary = new Dictionary<Renderer, List<Material>>();

        // Collect every Renderer in the entire child hierarchy (any depth)
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer rend in allRenderers)
        {
            if (rend == null) continue;

            AllRenderersOfThisGrab.Add(rend);

            List<Material> materialList = (List<Material>)new(rend.materials);
            CachedMaterialDictonary.Add(rend, materialList);
        }

        Debug.Log($"[GrabInteraction] GetAllRenderers: cached {AllRenderersOfThisGrab.Count} renderer(s) on '{gameObject.name}'.", this);
    }
    public void ResetObjectAfterDetect(MyTransform destination, float duration)
    {
        StartCoroutine(MoveObjectToDiscard(transform, destination, duration));
    }
    private IEnumerator MoveObjectToDiscard(Transform obj, MyTransform destination, float duration)
    {
      
        float timeInThisRoutine = 0;
        while (true)
        {
            timeInThisRoutine += UnityEngine.Time.deltaTime;
            obj.position = Vector3.Lerp(obj.position, destination.GetThisTransform().Position, timeInThisRoutine / duration);
            obj.rotation = destination.GetThisTransform().Rotation;
            obj.localScale = destination.GetThisTransform().localScale;
            yield return null;
            if (timeInThisRoutine > duration)
            {
                ResetHighlightedShader();
                obj.position = destination.GetThisTransform().Position;
                obj.rotation = destination.GetThisTransform().Rotation;
                obj.localScale = destination.GetThisTransform().localScale;
                yield break;
            }
        }
    }


    public void ResetHighlightedShader()
    { 
        grabHighlightController.HideHighlight();
    }
    #endregion
}
