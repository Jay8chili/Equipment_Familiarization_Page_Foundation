using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the active location for the simulation.
/// Place on a single GameObject in the scene. At Start(), it finds every
/// LocationTag in the scene and deactivates objects that don't belong
/// to the selected location.
/// </summary>
public class LocationManager : MonoBehaviour
{
    public static LocationManager Instance { get; private set; }

    [Tooltip("Reference to the LocationData asset that holds all location names.")]
    public LocationData locationData;

    [Tooltip("Index of the active location in the LocationData list (set at start).")]
    public int activeLocationIndex = 0;

    /// <summary>
    /// The currently active location name (read-only convenience property).
    /// </summary>
    public string ActiveLocationName
    {
        get
        {
            if (locationData != null && activeLocationIndex >= 0 && activeLocationIndex < locationData.locations.Count)
                return locationData.locations[activeLocationIndex];
            return string.Empty;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[LocationManager] Duplicate instance destroyed on " + gameObject.name);
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        ApplyLocation();
    }

    /// <summary>
    /// Finds every LocationTag in the scene (including inactive objects) and
    /// enables/disables GameObjects based on the active location.
    /// </summary>
    public void ApplyLocation()
    {
        if (locationData == null)
        {
            Debug.LogError("[LocationManager] LocationData is not assigned!");
            return;
        }

        string activeName = ActiveLocationName;
        if (string.IsNullOrEmpty(activeName))
        {
            Debug.LogError("[LocationManager] Active location index is out of range.");
            return;
        }

        Debug.Log($"[LocationManager] Setting active location to: {activeName}");

        // Find ALL LocationTags, including those on currently-inactive objects.
        LocationTag[] allTags = Resources.FindObjectsOfTypeAll<LocationTag>();

        foreach (LocationTag tag in allTags)
        {
            // Skip prefabs / assets that aren't in the scene.
            if (!tag.gameObject.scene.isLoaded) continue;
            // Don't deactivate the LocationManager itself.
            if (tag.gameObject == this.gameObject) continue;

            bool shouldBeActive = tag.BelongsToLocation(activeName, locationData);
            tag.gameObject.SetActive(shouldBeActive);
        }
    }

    /// <summary>
    /// Change the active location at runtime and reapply visibility.
    /// </summary>
    public void SetLocation(int index)
    {
        activeLocationIndex = index;
        ApplyLocation();
    }

    /// <summary>
    /// Change the active location by name at runtime.
    /// </summary>
    public void SetLocation(string locationName)
    {
        if (locationData == null) return;

        int index = locationData.locations.IndexOf(locationName);
        if (index >= 0)
            SetLocation(index);
        else
            Debug.LogWarning($"[LocationManager] Location '{locationName}' not found in LocationData.");
    }
}
