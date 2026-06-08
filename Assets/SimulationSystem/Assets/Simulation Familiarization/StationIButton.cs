// ════════════════════════════════════════════════════════════════════════════
//  StationIButton.cs
//  Place on each world-space I-button GameObject.
//  Requires a Collider for raycast detection.
//  IButtonRaycaster calls OnClick() when trigger is pressed while hovering.
// ════════════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.Events;

public class StationIButton : MonoBehaviour
{
    [Tooltip("The StationGroup this button belongs to.")]
    public StationGroup stationGroup;

    [Tooltip("Optional child GO used as the visual. If empty the whole GO is toggled.")]
    public GameObject visualRoot;

    public UnityEvent OnButtonShown;
    public UnityEvent OnButtonHidden;
    public UnityEvent OnButtonClicked;

    private bool _isVisible = false;
    private Collider _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        _isVisible = visible;

        GameObject target = visualRoot != null ? visualRoot : gameObject;
        target.SetActive(visible);

        if (_collider != null) _collider.enabled = visible;

        if (visible) OnButtonShown?.Invoke();
        else OnButtonHidden?.Invoke();
    }

    public void OnClick()
    {
        if (!_isVisible) return;
        Debug.Log($"[StationIButton] Clicked: '{stationGroup?.stationName}'");
        OnButtonClicked?.Invoke();
        PartsIdentificationManager.Instance?.OnStationSelected(stationGroup);
    }

    public bool IsVisible => _isVisible;
}