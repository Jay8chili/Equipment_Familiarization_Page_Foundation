using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class ContaminationZone
{
    public string areaName;
    public List<Collider> colliders = new List<Collider>();

    [Tooltip("Single trigger collider defining the boundary of this zone for speed tracking.")]
    public Collider speedTrackingArea;

    [Tooltip("Max hand speed (m/s) allowed inside this zone.")]
    public float speedThreshold = 2f;
}

public class ContaminationManager : MonoBehaviour
{
    [Header("Zones")]
    public List<ContaminationZone> zones = new List<ContaminationZone>();

    [Header("Timing")]
    [Tooltip("How long a hand must stay inside a collider before contamination triggers.")]
    public float contactThreshold = 1f;

    [Tooltip("How long the contamination UI stays visible before auto-resolving.")]
    public float displayDuration = 3f;

    [Header("Events")]
    public UnityEvent<string> onContaminationTriggered;
    public UnityEvent onContaminationResolved;

    // ── private ──────────────────────────────────────────────────────
    private ContaminationZone _activeZone;
    private HashSet<Collider> _activeColliderSet = new HashSet<Collider>();
    private bool _isTriggered;
    private Coroutine _resolveRoutine;

    // ── lifecycle ────────────────────────────────────────────────────

    private void Awake()
    {
        DisableAllSpeedTrackingAreas();
    }

    private void OnEnable()
    {
        ContaminationTrigger.OnHandContactDetected += NotifyTriggerEnter;
    }

    private void OnDisable()
    {
        ContaminationTrigger.OnHandContactDetected -= NotifyTriggerEnter;
    }

    // ── public API ───────────────────────────────────────────────────

    public void SetActiveZone(int index)
    {
        if (index < 0 || index >= zones.Count)
        {
            Debug.LogWarning($"[ContaminationManager] Zone index {index} out of range.");
            return;
        }

        _activeZone = zones[index];
        RebuildHashSet();
        UpdateSpeedTrackingAreas();
    }

    /// <summary>
    /// Disables the zone at the given index — clears it as the active zone if it is currently active,
    /// disables its speed tracking area, and stops any in-progress contamination resolve.
    /// </summary>
    public void DisableZone(int index)
    {
        if (index < 0 || index >= zones.Count)
        {
            Debug.LogWarning($"[ContaminationManager] DisableZone — index {index} out of range.");
            return;
        }

        ContaminationZone zone = zones[index];

        // Disable speed tracking area for this zone
        if (zone.speedTrackingArea != null)
            zone.speedTrackingArea.enabled = false;

        // If this zone is the currently active one, clear it
        if (_activeZone == zone)
        {
            _activeZone = null;
            _activeColliderSet.Clear();

            // Stop hand speed tracking immediately via event — hands may still be inside the zone
            SpeedTrackingArea.DisableTracking();

            // Stop any in-progress resolve coroutine
            if (_resolveRoutine != null)
            {
                StopCoroutine(_resolveRoutine);
                _resolveRoutine = null;
            }

            _isTriggered = false;
        }

        Debug.Log($"[ContaminationManager] Zone '{zone.areaName}' disabled.");
    }

    // ── private ──────────────────────────────────────────────────────

    private void NotifyTriggerEnter(Collider collider)
    {
        if (_isTriggered) return;
        if (!_activeColliderSet.Contains(collider)) return;

        _isTriggered = true;

        string areaName = _activeZone != null ? _activeZone.areaName : string.Empty;
        onContaminationTriggered?.Invoke(areaName);

        if (_resolveRoutine != null) StopCoroutine(_resolveRoutine);
        _resolveRoutine = StartCoroutine(ResolveAfterDelay());
    }

    private void RebuildHashSet()
    {
        _activeColliderSet.Clear();
        if (_activeZone == null) return;

        foreach (Collider col in _activeZone.colliders)
        {
            if (col != null)
                _activeColliderSet.Add(col);
        }
    }

    private void UpdateSpeedTrackingAreas()
    {
        foreach (ContaminationZone zone in zones)
        {
            if (zone.speedTrackingArea == null) continue;
            zone.speedTrackingArea.enabled = (zone == _activeZone);
        }
    }

    private void DisableAllSpeedTrackingAreas()
    {
        foreach (ContaminationZone zone in zones)
        {
            if (zone.speedTrackingArea == null) continue;
            zone.speedTrackingArea.enabled = false;
        }
    }

    private IEnumerator ResolveAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);

        _isTriggered = false;
        _resolveRoutine = null;
        onContaminationResolved?.Invoke();
    }
}