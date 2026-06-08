using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attach this component to any GameObject that should belong to one or more locations.
/// The LocationManager reads these tags at runtime to activate/deactivate objects.
/// </summary>
public class LocationTag : MonoBehaviour
{
    [Tooltip("If true, this object appears in ALL locations and is never deactivated.")]
    public bool allLocations = false;

    [Tooltip("Indices into the LocationData.locations list that this object belongs to.")]
    public List<int> selectedLocationIndices = new List<int>();

    /// <summary>
    /// Returns true if this object should be active for the given location name.
    /// </summary>
    public bool BelongsToLocation(string locationName, LocationData data)
    {
        if (allLocations) return true;
        if (data == null) return false;

        foreach (int index in selectedLocationIndices)
        {
            if (index >= 0 && index < data.locations.Count && data.locations[index] == locationName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if this object should be active for any of the given location names.
    /// </summary>
    public bool BelongsToAnyLocation(List<string> locationNames, LocationData data)
    {
        if (allLocations) return true;
        if (data == null || locationNames == null) return false;

        foreach (string loc in locationNames)
        {
            if (BelongsToLocation(loc, data))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns a list of location name strings this object belongs to.
    /// </summary>
    public List<string> GetLocationNames(LocationData data)
    {
        List<string> names = new List<string>();
        if (allLocations)
        {
            if (data != null) names.AddRange(data.locations);
            return names;
        }

        if (data == null) return names;

        foreach (int index in selectedLocationIndices)
        {
            if (index >= 0 && index < data.locations.Count)
                names.Add(data.locations[index]);
        }

        return names;
    }
}
