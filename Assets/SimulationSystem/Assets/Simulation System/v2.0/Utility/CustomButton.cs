using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Touch-based VR button driven by trigger collision.
///
/// SETUP REQUIREMENTS
/// ───────────────────
/// • Canvas render mode: World Space (required for VR).
/// • The Image GameObject needs a BoxCollider set as isTrigger = true.
/// • No Rigidbody needed on this GameObject.
/// • The hand / finger collider must have a Rigidbody (kinematic is fine)
///   and must be tagged "Hand" for detection to work.
/// • Assign the fingertip Transform in the Inspector for accurate ripple UV.
///   If left unassigned, ripple defaults to button center (0.5, 0.5).
/// </summary>
[RequireComponent(typeof(Collider))]
public class CustomButton : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("How long the hand must stay in contact before the button fires.")]
    public float holdTime = 1f;

    [Header("Events")]
    [Tooltip("Fired once when the hold timer completes successfully.")]
    public UnityEvent OnButtonClicked;

    [Tooltip("Fired when the hand first makes contact and hold begins.")]
    public UnityEvent OnStartHolding;

    [Tooltip("Fired when the hand leaves before the hold timer completes.")]
    public UnityEvent OnSuspendHolding;

    [Header("Ripple")]
    [Tooltip("Auto-found on this GameObject if not assigned.")]
    public RippleEffect rippleEffect;

    [Tooltip("Fingertip transform used to calculate ripple origin UV. " +
             "If unassigned, ripple defaults to button center (0.5, 0.5).")]
    public Transform fingertipTransform;

    // ── Private State ────────────────────────────────────────────────────────

    [SerializeField] private bool isHolding;
    [SerializeField] private bool isPressed;
    [SerializeField] private float elapsedTime;

    private Timer holdTimer;
    private RectTransform _rectTransform;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Force the collider to be a trigger.
        // No Rigidbody is needed on this object — the hand's Rigidbody
        // (kinematic or not) is sufficient to receive trigger events.
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        holdTimer = new Timer(holdTime);
        holdTimer.OnTimerEnd += ButtonClicked;
        holdTimer.OnTimerRunning += GetElapsedTimeValue;

        if (rippleEffect == null)
            rippleEffect = GetComponent<RippleEffect>();

        _rectTransform = GetComponent<RectTransform>();
    }

    private void OnDestroy()
    {
        holdTimer.OnTimerEnd -= ButtonClicked;
        holdTimer.OnTimerRunning -= GetElapsedTimeValue;
    }

    // ── Trigger Detection ────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!IsHand(other)) return;

        Debug.Log("hintbuttonclicked");
        Vector2 uv = FingertipToUV();
        StartHolding(uv);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsHand(other)) return;
        Debug.Log("hintbuttonexited");

        StopHolding();
    }

    // ── Hold Logic ───────────────────────────────────────────────────────────

    private void StartHolding(Vector2 touchUV)
    {
        // Ignore if already holding or button is in post-click cooldown.
        if (isPressed || isHolding) return;

        isHolding = true;
        holdTimer.StartTimer();
        OnStartHolding?.Invoke();

        rippleEffect?.TriggerRipple(touchUV);
    }

    private void StopHolding()
    {
        if (!isHolding) return;

        isHolding = false;
        holdTimer.StopTimer(true);
        OnSuspendHolding?.Invoke();

        rippleEffect?.CancelRipple();
    }

    public void ButtonClicked()
    {
        Debug.Log($"[CustomButton] ButtonClicked on '{gameObject.name}'");
        isPressed = true;

        if (isPressed)
        {
            OnButtonClicked?.Invoke();
            isHolding = false;
            isPressed = false;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the collider belongs to the player's hand.
    /// Detection is tag-based — ensure the hand collider is tagged "Hand".
    /// </summary>
    private bool IsHand(Collider col)
    {
        return col.CompareTag("IndexFinger");
    }

    /// <summary>
    /// Projects the fingertip world position onto the RectTransform and
    /// remaps to 0–1 UV for the ripple shader.
    ///
    /// RectTransform path (Canvas UI):
    ///   World point → local point via InverseTransformPoint.
    ///   Normalize by rect width/height, offset by pivot so (0,0) = bottom-left.
    ///
    /// Falls back to (0.5, 0.5) if fingertipTransform is not assigned.
    /// </summary>
    private Vector2 FingertipToUV()
    {
        // Fallback: no fingertip assigned — use button center.
        if (fingertipTransform == null)
        {
            Debug.LogWarning($"[CustomButton] No fingertip Transform assigned on '{gameObject.name}'. " +
                              "Ripple defaulting to button center (0.5, 0.5).");
            return new Vector2(0.5f, 0.5f);
        }

        Vector3 worldPoint = fingertipTransform.position;

        // ── RectTransform path (Canvas UI) ───────────────────────────────
        if (_rectTransform != null)
        {
            // Convert fingertip world position to button's local space.
            Vector3 local = _rectTransform.InverseTransformPoint(worldPoint);

            Rect rect = _rectTransform.rect;
            Vector2 pivot = _rectTransform.pivot;

            // local (0,0) sits at the pivot. Shift so (0,0) = bottom-left.
            float u = (local.x + pivot.x * rect.width) / rect.width;
            float v = (local.y + pivot.y * rect.height) / rect.height;

            return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        }

        // ── Fallback: no RectTransform found ─────────────────────────────
        return new Vector2(0.5f, 0.5f);
    }

    private void GetElapsedTimeValue(float timerValue)
    {
        elapsedTime = timerValue;
    }
}