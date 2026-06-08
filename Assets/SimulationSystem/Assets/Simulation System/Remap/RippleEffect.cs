using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Custom/UI/ButtonRipple shader on a UI Image component.
///
/// WHY NOT MaterialPropertyBlock?
/// ──────────────────────────────
/// CanvasRenderer does not support MaterialPropertyBlock. Instead we create
/// a runtime material INSTANCE from the Image's shared material on Awake.
/// This instance is private to this Image — other UI elements sharing the
/// same source material are unaffected.
///
/// Call TriggerRipple(uvCenter) to start, CancelRipple() when touch lifts.
///
/// SETUP
/// ─────
/// 1. Create a Material using Custom/UI/ButtonRipple shader.
/// 2. Assign that material to the Image component's Material slot.
/// 3. Add this component to the same GameObject.
/// 4. Done — it auto-instances the material on Awake.
/// </summary>
[RequireComponent(typeof(Image))]
public class RippleEffect : MonoBehaviour
{
    [Header("Animation")]
    [Tooltip("How fast the ripple ring expands (UV units per second).")]
    public float expandSpeed = 1.2f;

    [Tooltip("Max radius the ripple can reach (UV diagonal ≈ 1.41).")]
    public float maxRadius = 1.5f;

    [Tooltip("How fast the fill fades in while holding.")]
    public float fillFadeInSpeed = 2f;

    [Tooltip("Target fill alpha when fully held.")]
    [Range(0f, 1f)]
    public float fillTargetAlpha = 0.35f;

    [Tooltip("How fast the fill fades out on release.")]
    public float fillFadeOutSpeed = 3f;

    [Header("Colors (runtime overrides)")]
    public Color rippleColor = new Color(0.72f, 0.82f, 0.96f, 0.8f);
    public Color fillColor   = new Color(0.85f, 0.78f, 0.95f, 0.4f);

    // ── State ────────────────────────────────────────────────────────────────

    private Image    _image;
    private Material _mat;          // runtime instance — safe to write to

    private bool    _isAnimating;
    private bool    _isHolding;
    private float   _radius;
    private float   _fillAlpha;
    private Vector2 _center;

    // Shader property IDs
    private static readonly int _idCenter  = Shader.PropertyToID("_RippleCenter");
    private static readonly int _idRadius  = Shader.PropertyToID("_RippleRadius");
    private static readonly int _idFill    = Shader.PropertyToID("_FillAlpha");
    private static readonly int _idRipCol  = Shader.PropertyToID("_RippleColor");
    private static readonly int _idFillCol = Shader.PropertyToID("_FillColor");

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _image = GetComponent<Image>();

        // Create a private material instance so we don't pollute the shared asset.
        // Image.material setter already handles instancing, but we do it explicitly
        // to be clear and hold our own reference.
        if (_image.material != null)
        {
            _mat = new Material(_image.material);
            _image.material = _mat;
        }
        else
        {
            Debug.LogError("RippleEffect: Image has no material assigned. " +
                           "Create a material with Custom/UI/ButtonRipple and assign it.", this);
        }

        ResetProperties();
    }

    private void OnDestroy()
    {
        // Clean up the runtime material instance
        if (_mat != null)
            Destroy(_mat);
    }

    private void Update()
    {
        if (!_isAnimating || _mat == null) return;

        // ── Expand ring ──────────────────────────────────────────────────
        _radius += expandSpeed * Time.deltaTime;

        // ── Fill alpha ───────────────────────────────────────────────────
        if (_isHolding)
            _fillAlpha = Mathf.MoveTowards(_fillAlpha, fillTargetAlpha, fillFadeInSpeed * Time.deltaTime);
        else
            _fillAlpha = Mathf.MoveTowards(_fillAlpha, 0f, fillFadeOutSpeed * Time.deltaTime);

        // ── Push to material ─────────────────────────────────────────────
        _mat.SetVector(_idCenter, new Vector4(_center.x, _center.y, 0, 0));
        _mat.SetFloat(_idRadius, _radius);
        _mat.SetFloat(_idFill, _fillAlpha);
        _mat.SetColor(_idRipCol, rippleColor);
        _mat.SetColor(_idFillCol, fillColor);

        // ── Stop condition ───────────────────────────────────────────────
        if (_radius >= maxRadius && _fillAlpha <= 0.001f)
        {
            _isAnimating = false;
            ResetProperties();
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Start a ripple from the given UV coordinate (0–1 range).
    /// </summary>
    public void TriggerRipple(Vector2 uvCenter)
    {
        _center      = uvCenter;
        _radius      = 0f;
        _fillAlpha   = 0f;
        _isHolding   = true;
        _isAnimating = true;
    }

    /// <summary>Ripple from dead center.</summary>
    public void TriggerRipple()
    {
        TriggerRipple(new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Finger lifted — fill fades out, ring finishes expanding naturally.
    /// </summary>
    public void CancelRipple()
    {
        _isHolding = false;
    }

    /// <summary>Instantly kill everything.</summary>
    public void ResetRipple()
    {
        _isAnimating = false;
        _isHolding   = false;
        _radius      = 0f;
        _fillAlpha   = 0f;
        ResetProperties();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ResetProperties()
    {
        if (_mat == null) return;
        _mat.SetFloat(_idRadius, 0f);
        _mat.SetFloat(_idFill, 0f);
    }
}
