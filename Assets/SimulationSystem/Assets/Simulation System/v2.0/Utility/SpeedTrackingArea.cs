using System;
using UnityEngine;

public class SpeedTrackingArea : MonoBehaviour
{
    public static event Action<SpeedTrackingArea, Collider> OnHandEnteredArea;
    public static event Action<SpeedTrackingArea, Collider> OnHandExitedArea;
    public static event Action OnTrackingDisabled;

    [HideInInspector] public float speedThreshold = 2f;

    private int _handsInside = 0;

    public float SpeedThreshold => speedThreshold;

    public static void DisableTracking()
    {
        OnTrackingDisabled?.Invoke();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Hand")) return;
        _handsInside++;
        OnHandEnteredArea?.Invoke(this, other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Hand")) return;
        _handsInside = Mathf.Max(0, _handsInside - 1);
        OnHandExitedArea?.Invoke(this, other);
    }
}