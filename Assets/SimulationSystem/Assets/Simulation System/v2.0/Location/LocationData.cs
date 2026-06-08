using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject that stores all available location names for the project.
/// Create via Assets > Create > Location System > Location Data.
/// </summary>
[CreateAssetMenu(fileName = "LocationData", menuName = "Location System/Location Data")]
public class LocationData : ScriptableObject
{
    [Tooltip("List of all location names available in the project.")]
    public List<string> locations = new List<string>();
}
