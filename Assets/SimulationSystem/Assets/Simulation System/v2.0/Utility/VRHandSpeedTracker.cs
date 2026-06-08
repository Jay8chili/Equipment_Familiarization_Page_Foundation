using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class VRHandSpeedTracker : MonoBehaviour
{
    [Header("Hand Transforms")]
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;

    [Header("Speed Settings")]
    [Tooltip("Hysteresis buffer (m/s) to prevent rapid event toggling.")]
    [SerializeField, Range(0f, 1f)] private float hysteresis = 0.15f;

    [Tooltip("Smoothing factor for speed calculation (0 = no smoothing).")]
    [SerializeField, Range(0f, 0.95f)] private float smoothing = 0.3f;

    [Header("Timing")]
    [Tooltip("How long the speed violation UI stays visible before auto-resolving.")]
    public float resolveDuration = 3f;

    [Header("Events")]
    public UnityEvent OnSpeedViolationDetected;
    public UnityEvent OnSpeedViolationResolved;

    // ── public read-only ─────────────────────────────────────────────
    public float LeftHandSpeed => _left.smoothedSpeed;
    public float RightHandSpeed => _right.smoothedSpeed;
    public bool IsLeftAbove => _left.isAbove;
    public bool IsRightAbove => _right.isAbove;

    // ── internal ─────────────────────────────────────────────────────
    private struct HandState
    {
        public Vector3 prevPosition;
        public float smoothedSpeed;
        public bool isAbove;
        public bool initialized;
    }

    private HandState _left;
    private HandState _right;
    private float _currentSpeedThreshold;
    private bool _isActive;
    private bool _isTriggered;
    private bool _trackLeft;
    private bool _trackRight;
    private Coroutine _resolveRoutine;
    private float _invFixedDt;

    // ── lifecycle ────────────────────────────────────────────────────

    private void OnEnable()
    {
        SpeedTrackingArea.OnHandEnteredArea += HandleHandEnteredArea;
        SpeedTrackingArea.OnHandExitedArea += HandleHandExitedArea;
        SpeedTrackingArea.OnTrackingDisabled += StopTracking;
    }

    private void OnDisable()
    {
        SpeedTrackingArea.OnHandEnteredArea -= HandleHandEnteredArea;
        SpeedTrackingArea.OnHandExitedArea -= HandleHandExitedArea;
        SpeedTrackingArea.OnTrackingDisabled -= StopTracking;
    }

    private void FixedUpdate()
    {
        if (!_isActive || _isTriggered) return;

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) return;
        _invFixedDt = 1f / dt;

        if (leftHand != null && _trackLeft) ProcessHand(ref _left, leftHand, 0);
        if (rightHand != null && _trackRight) ProcessHand(ref _right, rightHand, 1);
    }

    // ── area events ──────────────────────────────────────────────────

    private void HandleHandEnteredArea(SpeedTrackingArea area, Collider handCollider)
    {
        _currentSpeedThreshold = area.SpeedThreshold;
        _isActive = true;

        // Determine which hand entered and enable its tracking
        if (leftHand != null && handCollider.transform.IsChildOf(leftHand) || handCollider.transform == leftHand)
        {
            _trackLeft = true;
            _left = default;
        }
        else if (rightHand != null && (handCollider.transform.IsChildOf(rightHand) || handCollider.transform == rightHand))
        {
            _trackRight = true;
            _right = default;
        }
    }

    private void HandleHandExitedArea(SpeedTrackingArea area, Collider handCollider)
    {
        // Stop tracking the hand that exited
        if (leftHand != null && (handCollider.transform.IsChildOf(leftHand) || handCollider.transform == leftHand))
        {
            _trackLeft = false;
            _left = default;
        }
        else if (rightHand != null && (handCollider.transform.IsChildOf(rightHand) || handCollider.transform == rightHand))
        {
            _trackRight = false;
            _right = default;
        }

        // Only deactivate if no hands remain in the zone
        if (!_trackLeft && !_trackRight)
        {
            _isActive = false;
            if (!_isTriggered)
                ResetTracking();
        }
    }

    // ── core ─────────────────────────────────────────────────────────

    private void ProcessHand(ref HandState state, Transform hand, int index)
    {
        Vector3 pos = hand.position;

        if (!state.initialized)
        {
            state.prevPosition = pos;
            state.initialized = true;
            return;
        }

        float rawSpeed = (pos - state.prevPosition).magnitude * _invFixedDt;
        state.prevPosition = pos;

        state.smoothedSpeed = Mathf.Lerp(rawSpeed, state.smoothedSpeed, smoothing);

        float speed = state.smoothedSpeed;

        if (!state.isAbove)
        {
            if (speed > _currentSpeedThreshold)
            {
                state.isAbove = true;
                HandleViolation();
            }
        }
        else
        {
            if (speed < _currentSpeedThreshold - hysteresis)
                state.isAbove = false;
        }
    }

    private void HandleViolation()
    {
        if (_isTriggered) return;

        _isTriggered = true;
        OnSpeedViolationDetected?.Invoke();
        Debug.Log("[vrhandspeedtracker]" + "speed violationdetected invoked");


        if (_resolveRoutine != null) StopCoroutine(_resolveRoutine);
        _resolveRoutine = StartCoroutine(ResolveAfterDelay());
    }

    private IEnumerator ResolveAfterDelay()
    {
        yield return new WaitForSeconds(resolveDuration);

        _isTriggered = false;
        _resolveRoutine = null;
        OnSpeedViolationResolved?.Invoke();
        Debug.Log("[vrhandspeedtracker]" + "resolveafterdelay invoked");
    }

    // ── public API ───────────────────────────────────────────────────

    public void StopTracking()
    {
        _isActive = false;
        _trackLeft = false;
        _trackRight = false;
        _left = default;
        _right = default;
    }

    public void SetThreshold(float newThreshold)
    {
        _currentSpeedThreshold = Mathf.Max(0f, newThreshold);
    }

    public void ResetTracking()
    {
        _left = default;
        _right = default;
        _trackLeft = false;
        _trackRight = false;

        if (_isTriggered) return;

        if (_resolveRoutine != null)
        {
            StopCoroutine(_resolveRoutine);
            _resolveRoutine = null;
        }

        _isTriggered = false;
    }

    // ── gizmos ───────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !_isActive) return;
        DrawHandGizmo(leftHand, _left);
        DrawHandGizmo(rightHand, _right);
    }

    private void DrawHandGizmo(Transform hand, HandState state)
    {
        if (hand == null) return;

        float speed = state.smoothedSpeed;
        if (speed > _currentSpeedThreshold) Gizmos.color = Color.red;
        else if (speed > _currentSpeedThreshold - hysteresis) Gizmos.color = Color.yellow;
        else Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(hand.position, 0.05f);
        UnityEditor.Handles.Label(
            hand.position + Vector3.up * 0.08f,
            $"{speed:F2} m/s");
    }
#endif
}