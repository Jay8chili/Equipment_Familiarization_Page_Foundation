using UnityEngine;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using SimulationSystem.V02.Utility;
using SimulationSystem.V02.Assistant;

public class DetectInteraction : Interactions, IDetect
{
    public bool DontUngrabOnStateEnd;
    [Space(15f)]
    [Tooltip("Objects to be detected simultaneously or separately.")]
    [Header("Detection")]
    public bool TeleportObjectToDetect = false;
    public List<GameObject> ObjectsToBeDetectedList;

    public enum DetectType { Normal, Wrong }
    [HideInInspector] public DetectType detectType;

    public bool resetTimer;
    public Timer timer;
    public bool detectSeparately;

    [SerializeField] private int countOfDetectsEntered;

    [Header("Discard Object")]
    [Tooltip("When enabled, the object plays its dissolve effect when discarded.")]
    public bool dissolve = false;
    public enum DiscardMode { DontDoAnything, OriginalPosition, GivenPosition }
    [Tooltip("Where to send the detected objects on interaction complete.")]
    public DiscardMode discardMode = DiscardMode.OriginalPosition;
    [Tooltip("Destination transform. Only used when Discard Mode is Given Position.")]
    public Transform discardTarget;

    [Header("Discard Animation")]
    [Tooltip("Duration of the smooth move to discard position (ignored when dissolve is true).")]
    [Min(0.01f)]
    public float discardMoveDuration = 0.35f;

    [Tooltip("Easing curve for the discard move. OutCubic = fast start, slow arrival.")]
    public Ease discardMoveEase = Ease.OutCubic;

    [Header("Radial Progress UI")]
    public RadialInteractionUI radialUI;
    protected override RadialInteractionUI RadialUI => radialUI;

    // Stores the world-space starting position for each object at Start time.
    // Keyed by the object's instance ID so we can look up the correct origin
    // even if the list order changes.
    private Dictionary<GrabInteraction, MyTransform> _originalPositions = new();

    // Cancellation for any in-flight discard tweens.
    private CancellationTokenSource _discardCts;

    private HashSet<GameObject> _wotdObjects = new();
    public System.Action<GameObject> OnWOTDEntered;

    // ─────────────────────────────────────────────────────────────────────────

    public void SetupWrongObjects(List<GameObject> wotd)
    {
        _wotdObjects.Clear();
        if (wotd == null) return;
        foreach (var obj in wotd)
            if (obj != null) _wotdObjects.Add(obj);
    }

    public void ClearWrongObjects()
    {
        _wotdObjects.Clear();
        OnWOTDEntered = null;
    }

    public override void Awake()
    {
        base.Awake();
        CaptureOriginalPositions();
    }

    public override void Start()
    {
        countOfDetectsEntered = 0;
        ChangeDetectType(DetectType.Normal);

        // Capture original world positions as Vector3 values so they don't
        // move when the objects get grabbed and carried away.

    }

    private void CaptureOriginalPositions()
    {
        _originalPositions.Clear();
        if (ObjectsToBeDetectedList == null) return;
        _originalPositions = new();
        foreach (var obj in ObjectsToBeDetectedList)
        {
            if (obj.GetComponent<GrabInteraction>() == null) continue;
            _originalPositions[obj.GetComponent<GrabInteraction>()] = obj.GetComponent<GrabInteraction>().InitialPositionRotation;
            Debug.Log($"[DetectInteraction] Captured original pos for '{obj.name}': {obj.transform.position}");
        }
    }

    /// <summary>
    /// Look up the stored original position for a given object.
    /// Falls back to its current position if not found.
    /// </summary>
    private MyTransform GetOriginalTransform(GameObject obj)
    {
        if (obj != null && obj.GetComponent<GrabInteraction>())
        {

            return obj.GetComponent<GrabInteraction>().InitialPositionRotation;
        }
        else
        {
            return new MyTransform(this.transform);

        }

        //Fallback to Detect Position If not found in Dict

    }



    // ─────────────────────────────────────────────────────────────────────────
    // INTERACTION LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    /*public override void StartInteraction()
    {
        base.StartInteraction();

        if (TeleportObjectToDetect)
        {
            var movHelper = ObjectsToBeDetectedList[0].GetComponent<ObjectMovementHelper>();
            if (movHelper != null)
            {
                TeleportManager.Instance.TeleportObject(ObjectsToBeDetectedList[0]);
            }
        }

        // Enable grab on any GrabInteraction components in the detect list
        // so the player can pick them up and bring them to this detector.
        EnableGrabOnDetectObjects();
    }*/

    public override void StartInteraction()
    {
        base.StartInteraction();

        if (TeleportObjectToDetect)
        {
            canInteract = false;
            var movHelper = ObjectsToBeDetectedList[0].GetComponent<MoveToHelper>();
            if (movHelper != null)
            {
                movHelper.TeleportThisObject();
            }
        }

        // Enable grab on any GrabInteraction components in the detect list
        // so the player can pick them up and bring them to this detector.
        EnableGrabOnDetectObjects();
    }

    public override void OnInteractionComplete()
    {
        base.OnInteractionComplete();

        // Cancel any previous discard tweens
        _discardCts?.Cancel();
        _discardCts = new CancellationTokenSource();

        // Force-release all grabbables in the detect list FIRST,
        // before disabling grab or starting the discard move.
        // This happens when the detect timer finishes — the player held
        // the object in the zone long enough, so we yank it from their hand.
        Debug.Log("Captured 123456789");
        if (!DontUngrabOnStateEnd)
        {

            Debug.Log("Captured 1234567");
            ForceUngrabAllDetectedObjects();
            Debug.Log("Captured 123456");
            foreach (var obj in ObjectsToBeDetectedList)
            {
                if (obj == null) continue;

                // Disable grab on the object now that detection is done —
                // the player shouldn't be able to re-grab mid-discard.

                DisableGrabOnObject(obj);
                Debug.Log("Captured 123456");

                // Determine destination
                MyTransform destination;
                switch (discardMode)

                {
                    case DiscardMode.DontDoAnything:
                        //nothing
                        destination = null;

                        break;
                    case DiscardMode.GivenPosition:
                        if (discardTarget == null)
                        {
                            Debug.LogWarning($"[DetectInteraction] '{gameObject.name}': Discard mode is GivenPosition " +
                                             "but no discard target assigned. Falling back to original position.");
                            destination = GetOriginalTransform(obj);
                        }
                        else
                        {
                            destination = new MyTransform(discardTarget.transform);
                        }

                        obj.GetComponent<GrabInteraction>().ResetObjectAfterDetect(destination, discardMoveDuration);

                        break;

                    case DiscardMode.OriginalPosition:
                    default:
                        destination = GetOriginalTransform(obj);
                        Debug.Log("Captured 12345");
                        obj.GetComponent<GrabInteraction>().ResetObjectAfterDetect(destination, discardMoveDuration);

                        break;
                }

                // Dissolve path — hand off to AssistantManager
                if (dissolve)
                {
                    // Create a temporary target transform for the dissolve system.
                    // AssistantManager.BotDiscard expects a Transform, so we use
                    // discardTarget if available, otherwise we need a position-based approach.
                    if (discardMode == DiscardMode.GivenPosition && discardTarget != null)
                    {
                        AssistantManager.Instance.BotDiscard(obj, discardTarget);
                    }
                    else
                    {
                        // For original position with dissolve, we pass a dummy or
                        // use the object's own transform positioned at origin.
                        // Since BotDiscard expects a Transform, move it there after dissolve.
                        AssistantManager.Instance.BotDiscard(obj, obj.transform);
                    }
                    continue;
                }



                // Smooth eased move — fast start, slow arrival
            }
        }
        gameObject.SetActive(false); // deactivate after interaction to prevent re-triggering.
    }

    public override void OnInteractionUpdate() => base.OnInteractionUpdate();

    public override void OnInteractionStart()
    {
        base.OnInteractionStart();
        InteractionTimer.StartTimer();
    }

    public override void OnInteractionSuspend()
    {
        if (IsCompleted) return;

        base.OnInteractionSuspend();
        InteractionTimer.StopTimer(resetTimer);
    }

    public override void StopInteraction()
    {
        // Only cancel the discard if the interaction hasn't completed yet.
        // After OnInteractionComplete, _discardCts may be driving an active
        // DoMove animation — cancelling it here would kill the movement.
        if (!IsCompleted) _discardCts?.Cancel();
        base.StopInteraction();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TRIGGER DETECTION
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!canInteract) return;
        if (ObjectsToBeDetectedList.Count == 0) return;

        if (_wotdObjects.Count > 0 && _wotdObjects.Contains(other.gameObject))
        {
            OnWOTDEntered?.Invoke(other.gameObject);
            if (other.CompareTag("IndexFinger")) return; // penalty only, no further interaction
            OnInteractionUpdate();
            return;
        }

        if (detectSeparately)
        {
            foreach (var obj in ObjectsToBeDetectedList)
                if (other == obj.GetComponent<Collider>())
                    OnInteractionStart();
        }
        else
        {
            foreach (var obj in ObjectsToBeDetectedList)
            {
                if (other == obj.GetComponent<Collider>())
                {
                    // Guard: never count higher than the list size
                    if (countOfDetectsEntered < ObjectsToBeDetectedList.Count)
                        countOfDetectsEntered++;
                    break;
                }
            }

            if (countOfDetectsEntered == ObjectsToBeDetectedList.Count)
                OnInteractionStart();
        }

        OnInteractionUpdate();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!canInteract) return;

        // WOTD objects exiting don't affect the interaction state
        if (_wotdObjects.Count > 0 && _wotdObjects.Contains(other.gameObject))
        {
            if (other.CompareTag("IndexFinger")) return;
            // for non-finger WOTD, still ignore on exit
            return;
        }

        bool wasTracked = false;
        foreach (var obj in ObjectsToBeDetectedList)
            if (obj != null && other == obj.GetComponent<Collider>())
            { wasTracked = true; break; }

        if (!wasTracked) return;

        OnInteractionSuspend();
        countOfDetectsEntered = Mathf.Max(0, countOfDetectsEntered - 1);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GRAB LIFECYCLE HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enable grab on all GrabInteraction components in the detect list
    /// so the player can pick them up and carry them to the detector.
    /// </summary>
    /*private void EnableGrabOnDetectObjects()
    {
        if (ObjectsToBeDetectedList == null) return;

        foreach (var obj in ObjectsToBeDetectedList)
        {
            if (obj == null) continue;
            var grab = obj.GetComponent<GrabInteraction>();
            if (grab == null) continue;

            // Enable grabbing — sets canInteract = true and shows outline
            if (!grab.isGrabbed)
            {
                grab.OnInteractionStarted();
            }
        }
    }*/
    private void EnableGrabOnDetectObjects()
    {
        if (ObjectsToBeDetectedList == null) return;

        foreach (var obj in ObjectsToBeDetectedList)
        {
            if (obj == null) continue;
            var grab = obj.GetComponent<GrabInteraction>();
            if (grab == null) continue;

            if (grab.isGrabbed)
            {
                // Object is still held from the previous state (DontUngrabOnStateEnd = true).
                // StopInteraction() on the old state set canInteract = false — restore it
                // directly without calling OnInteractionStarted() mid-grab (that would
                // replay sounds/outline while the player is already holding the object).
                grab.canInteract = true;
            }
            else
            {
                // Normal case: arm the grab fresh.
                grab.OnInteractionStarted();
            }
        }
    }
    /// <summary>
    /// Disable grab on a single object so it can't be re-grabbed mid-discard.
    /// </summary>
    private void DisableGrabOnObject(GameObject obj)
    {
        if (obj == null) return;
        var grab = obj.GetComponent<GrabInteraction>();
        if (grab == null) return;

        if (grab.isGrabbed)
            grab.ForceReleaseSuppressReset();

        grab.StopInteraction();
    }

    /// <summary>
    /// Force-release ALL grabbed objects in the detect list.
    /// Uses ForceReleaseSuppressReset so BeginReset does not start and conflict
    /// with the discard move that follows immediately after.
    /// </summary>
    private void ForceUngrabAllDetectedObjects()
    {
        if (ObjectsToBeDetectedList == null) return;

        foreach (var obj in ObjectsToBeDetectedList)
        {
            if (obj == null) continue;

            var grab = obj.GetComponent<GrabInteraction>();
            if (grab != null && grab.isGrabbed)
            {
                grab.ForceReleaseSuppressReset();
                Debug.Log($"[DetectInteraction] Force-released grab on '{obj.name}' — detect timer completed.");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SMOOTH DISCARD MOVE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Smoothly move an object to its discard position using DoMove from
    /// ExtensionMethods. Fast start, slow arrival (OutCubic by default).
    /// </summary>
/*    private async void MoveObjectToDiscard(Transform obj, Transform destination, CancellationToken token)
    {
        if (obj == null) return;

        try
        {
            await obj.DoMove(
                destination.position,
                discardMoveDuration,
                discardMoveEase,
                token);
            obj.rotation = destination.rotation;

        }

        catch (System.OperationCanceledException) { }
    }

*/
    private void MoveObjectToDiscard(Transform obj, MyTransform destination)
    {
        Debug.Log("Captured 123 Discarding");


        /* float timeInThisRoutine = 0;
         while (true) {
             timeInThisRoutine += UnityEngine.Time.deltaTime;
             obj.position = Vector3.Lerp(obj.position, destination.transform.position, timeInThisRoutine% discardMoveDuration);

             yield return null; 
         }*/
    }
    // ─────────────────────────────────────────────────────────────────────────

    protected override IEnumerator ExecuteInteraction() { yield return null; }

    private void AddListeners()
    {
        OnInteractionCompletedEvent.AddListener(() =>
        {
            switch (detectType)
            {
                case DetectType.Wrong:
                   
                    break;
                case DetectType.Normal:
                    if (SimulationManager.Instance.simulationMode == SimulationMode.Assessment)
                    {
                    
                    }
                    break;
            }
        });
    }

    public void SetupWrongDetects(Transform t)
    {
        if (detectType == DetectType.Wrong)
            if (SimulationManager.Instance.simulationMode == SimulationMode.Assessment)
                t.GetComponent<Interactions>();
    }

    public void ChangeDetectType(DetectType _detectType)
    {
        detectType = _detectType;
        Debug.Log(detectType == DetectType.Normal ? "Normal Detect" : "Wrong Detect");
    }
}
