using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public enum InputMode
{
    HandTracking,
    Controller
}

/// <summary>
/// Detects a pinch/grip and initiates an offset-based grab on the nearest GrabInteraction.
///
/// INPUT MODES
/// ───────────
/// HandTracking : Requires HandPoseLock → supplies wrist root + freezes joints.
/// Controller   : Requires ControllerPoseLock → supplies Animator freeze.
///                Uses controllerRoot as the stable tracking reference.
///
/// HANDEDNESS
/// ──────────
/// Set in the inspector. Forwarded to GrabInteraction so it can pick the
/// correct RecordedPose from its four slots.
///
/// PROXIMITY GRAB SUPPORT
/// ───────────────────────
/// When a GrabInteraction in GrabWithoutPinch mode auto-grabs via proximity,
/// it calls NotifyProximityGrab to register itself as _currentGrab. This lets
/// the detector's existing pinch release path (OnPerformed / OnCanceled)
/// release the proximity-grabbed object cleanly.
/// </summary>
public class GrabPinchDetector : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Identity")]
    public InputMode inputMode = InputMode.HandTracking;
    public Handedness handedness = Handedness.Right;

    [Header("Input Actions")]
    public InputActionReference pinchAction;

    [Header("Grab Detection")]
    public float grabRadius = 0.15f;
    public LayerMask grabLayerMask = ~0;

    [Header("Transforms")]
    [Tooltip("Contact point sampled at grab time. Fingertip for hands, grip point for controllers.")]
    public Transform pinchPoint;

    [Header("Hand Tracking Setup")]
    [Tooltip("Required for HandTracking. Provides skeletonDriver.rootTransform + finger freeze.")]
    public HandPoseLock handPoseLock;

    [Header("Controller Setup")]
    [Tooltip("Required for Controller. Stable tracking reference (like the wrist).")]
    public Transform controllerRoot;

    [Tooltip("Required for Controller with pose locking. Freezes Animator-driven hand mesh.")]
    public ControllerPoseLock controllerPoseLock;

    [Header("Events")]
    public UnityEvent onPinchStarted;
    public UnityEvent onPinchPerformed;
    public UnityEvent onPinchCancelled;

    // ── State ────────────────────────────────────────────────────────────────

    [Header("State — read only")]
    [SerializeField] private bool _isPinching;
    [SerializeField] private bool _hasGrabbedObject;
    [SerializeField] public GrabInteraction _currentGrab;

    public bool isPinching => _isPinching;
    public bool hasGrabbedObject => _hasGrabbedObject;
    public GrabInteraction currentGrab => _currentGrab;

    // ── Internal ─────────────────────────────────────────────────────────────

    private readonly Collider[] _overlapBuffer = new Collider[32];
    private readonly List<Collider> _colliderBuffer = new List<Collider>(16);

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (pinchAction?.action == null)
        {
            Debug.LogError("GrabPinchDetector: pinchAction not assigned.", this);
            return;
        }
        pinchAction.action.Enable();
        pinchAction.action.started += OnStarted;
        pinchAction.action.performed += OnPerformed;
        pinchAction.action.canceled += OnCanceled;
    }

    private void OnDisable()
    {
        if (pinchAction?.action == null) return;
        pinchAction.action.started -= OnStarted;
        pinchAction.action.performed -= OnPerformed;
        pinchAction.action.canceled -= OnCanceled;
        pinchAction.action.Disable();
    }

    // ── Input ────────────────────────────────────────────────────────────────

    private void OnStarted(InputAction.CallbackContext ctx) => onPinchStarted?.Invoke();

    private void OnPerformed(InputAction.CallbackContext ctx)
    {
        _isPinching = true;
        if (_hasGrabbedObject) return;   // already holding — must release (OnCanceled) before grabbing again
        TryGrab();
        onPinchPerformed?.Invoke();
    }

    private void OnCanceled(InputAction.CallbackContext ctx)
    {
        _isPinching = false;
        if (_currentGrab != null) { _currentGrab.OnPinchCanceled(); ClearState(); }
        onPinchCancelled?.Invoke();
    }

    // ── Grab ─────────────────────────────────────────────────────────────────

    private void TryGrab()
    {
        if (pinchPoint == null)
        {
            Debug.LogError("GrabPinchDetector: pinchPoint not assigned.", this);
            return;
        }

        Transform trackingRoot = GetTrackingRoot();
        if (trackingRoot == null)
        {
            Debug.LogError("GrabPinchDetector: cannot resolve tracking root. " +
                           "Check controllerRoot (Controller) or handPoseLock.skeletonDriver (Hand).", this);
            return;
        }

        Vector3 origin = pinchPoint.position;

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin, grabRadius, _overlapBuffer, grabLayerMask, QueryTriggerInteraction.Collide);

        GrabInteraction best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var grab = _overlapBuffer[i].GetComponentInParent<GrabInteraction>();
            if (grab == null) continue;
            float d = ClosestDistanceSq(grab, origin);
            if (d < bestDistSq) { bestDistSq = d; best = grab; }
        }

        if (best != null)
        {
            HandPoseLock hLock = inputMode == InputMode.HandTracking ? handPoseLock : null;
            ControllerPoseLock cLock = inputMode == InputMode.Controller ? controllerPoseLock : null;

            // Pass the Transform so TryGrabFrom can lock the pose first
            // and then read the (now-locked) pinch position.
            best.TryGrabFrom(pinchPoint, trackingRoot, inputMode, handedness, hLock, cLock);
            _currentGrab = best;
            _hasGrabbedObject = true;
        }
        else
        {
            Debug.Log($"GrabPinchDetector: nothing in range (r={grabRadius}, hits={hitCount}).");
        }
    }

    // ── Public helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// The stable tracking base for the current input mode.
    /// Public so GrabInteraction can read it for proximity grabs.
    /// </summary>
    public Transform TrackingRoot => GetTrackingRoot();

    /// <summary>
    /// Called by GrabInteraction after a proximity (GrabWithoutPinch) grab fires.
    /// Registers the grab on this detector so the existing pinch-release path
    /// (OnPerformed / OnCanceled → OnPinchCanceled) can release it.
    /// </summary>
    public void NotifyProximityGrab(GrabInteraction grab)
    {
        // If the detector was already holding something (shouldn't happen — the
        // proximity coroutine checks hasGrabbedObject), release the old one first.
        if (_currentGrab != null && _currentGrab != grab)
        {
            _currentGrab.OnPinchCanceled();
        }

        _currentGrab = grab;
        _hasGrabbedObject = true;
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private Transform GetTrackingRoot()
    {
        if (inputMode == InputMode.Controller)
        {
            return controllerRoot;
        }
        else
        {
            if (handPoseLock == null || handPoseLock.skeletonDriver == null) return null;
            return handPoseLock.skeletonDriver.rootTransform;
        }
    }

    private float ClosestDistanceSq(GrabInteraction grab, Vector3 point)
    {
        grab.GetComponentsInChildren(_colliderBuffer);
        if (_colliderBuffer.Count == 0)
            return (grab.transform.position - point).sqrMagnitude;

        float best = float.MaxValue;
        for (int i = 0; i < _colliderBuffer.Count; i++)
        {
            Collider col = _colliderBuffer[i];
            if (col == null) continue;
            // Skip colliders owned by a nested child GrabInteraction, not this one
            if (col.GetComponentInParent<GrabInteraction>() != grab) continue;
            float d = (col.ClosestPoint(point) - point).sqrMagnitude;
            if (d < best) best = d;
        }
        return best == float.MaxValue ? (grab.transform.position - point).sqrMagnitude : best;
    }

    private void ClearState() { _currentGrab = null; _hasGrabbedObject = false; }

    /// <summary>Called by GrabInteraction when a proximity-grabbed object auto-releases (hand left zone).</summary>
    public void ClearCurrentGrab() => ClearState();
}