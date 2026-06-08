using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(LineRenderer))]
public class IButtonRaycaster : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference triggerAction;

    [Header("Ray Settings")]
    public float rayLength = 5f;
    public LayerMask layerMask = ~0;

    [Header("Visual")]
    public Color defaultColor = new Color(1f, 1f, 1f, 0.4f);
    public Color hoverColor = Color.cyan;
    public float lineWidth = 0.004f;

    [Header("Visibility")]
    [Tooltip("Only show the ray when Free Roam I-buttons are visible.")]
    public bool onlyShowInFreeRoam = true;

    private LineRenderer _line;
    private StationIButton _hoveredButton;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        SetupLineRenderer();
    }

    private void OnEnable()
    {
        if (triggerAction != null)
            triggerAction.action.Enable();
    }

    private void OnDisable()
    {
        if (triggerAction != null)
            triggerAction.action.Disable();

        // Hide the ray visually but do NOT disable the LineRenderer component
        SetLineVisible(false);
    }

    private void Update()
    {
        // ── Visibility ────────────────────────────────────────────────────
        bool shouldShow = !onlyShowInFreeRoam ||
                          (PartsIdentificationManager.Instance != null &&
                           IsAnyIButtonVisible());

        SetLineVisible(shouldShow);
        if (!shouldShow) return;

        // ── Raycast ───────────────────────────────────────────────────────
        Ray ray = new Ray(transform.position, transform.forward);
        bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, rayLength, layerMask);

        StationIButton button = hit
            ? hitInfo.collider.GetComponent<StationIButton>()
            : null;

        if (button != null && !button.IsVisible) button = null;
        _hoveredButton = button;

        // ── Update line ───────────────────────────────────────────────────
        Vector3 endPoint = hit ? hitInfo.point : transform.position + transform.forward * rayLength;
        _line.SetPosition(0, transform.position);
        _line.SetPosition(1, endPoint);

        Color c = _hoveredButton != null ? hoverColor : defaultColor;
        _line.startColor = c;
        _line.endColor = new Color(c.r, c.g, c.b, 0f);

        // ── Trigger ───────────────────────────────────────────────────────
        if (triggerAction != null &&
            triggerAction.action.WasPressedThisFrame() &&
            _hoveredButton != null)
        {
            _hoveredButton.OnClick();
        }
    }

    private bool IsAnyIButtonVisible()
    {
        if (PartsIdentificationManager.Instance == null) return false;
        foreach (var group in PartsIdentificationManager.Instance.stationGroups)
            if (group.iButton != null && group.iButton.IsVisible) return true;
        return false;
    }

    private void SetLineVisible(bool visible)
    {
        // Set alpha to 0 rather than disabling the component
        Color c = visible ? defaultColor : Color.clear;
        _line.startColor = c;
        _line.endColor = Color.clear;
        _line.enabled = true; // always keep enabled
    }

    private void SetupLineRenderer()
    {
        _line.positionCount = 2;
        _line.startWidth = lineWidth;
        _line.endWidth = lineWidth * 0.1f;
        _line.useWorldSpace = true;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;

        var mat = new Material(Shader.Find("Sprites/Default"));
        _line.material = mat;
        _line.startColor = defaultColor;
        _line.endColor = Color.clear;
    }
}