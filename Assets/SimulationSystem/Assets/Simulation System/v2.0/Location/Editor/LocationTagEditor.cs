using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Custom inspector for LocationTag.
/// Displays a checkbox for each location defined in the LocationData asset,
/// plus an "All Locations" toggle at the top.
/// </summary>
[CustomEditor(typeof(LocationTag))]
public class LocationTagEditor : Editor
{
    private LocationData cachedData;

    public override void OnInspectorGUI()
    {
        LocationTag tag = (LocationTag)target;

        // Try to find a LocationData reference.
        // First look at the LocationManager in the scene, then fall back to finding the asset.
        if (cachedData == null)
        {
            LocationManager manager = Object.FindFirstObjectByType<LocationManager>();
            if (manager != null)
                cachedData = manager.locationData;

            if (cachedData == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:LocationData");
                if (guids.Length > 0)
                    cachedData = AssetDatabase.LoadAssetAtPath<LocationData>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        if (cachedData == null)
        {
            EditorGUILayout.HelpBox(
                "No LocationData asset found.\n" +
                "Create one via Assets > Create > Location System > Location Data,\n" +
                "or assign it to a LocationManager in the scene.",
                MessageType.Warning);
            return;
        }

        serializedObject.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Location Tag", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        // ── All Locations toggle ──
        SerializedProperty allProp = serializedObject.FindProperty("allLocations");
        EditorGUI.BeginChangeCheck();
        bool newAll = EditorGUILayout.Toggle("All Locations", allProp.boolValue);
        if (EditorGUI.EndChangeCheck())
        {
            allProp.boolValue = newAll;
        }

        EditorGUILayout.Space(4);

        // ── Per-location checkboxes ──
        if (!allProp.boolValue)
        {
            SerializedProperty indicesProp = serializedObject.FindProperty("selectedLocationIndices");

            // Build a hash set of currently selected indices for fast lookup.
            HashSet<int> selected = new HashSet<int>();
            for (int i = 0; i < indicesProp.arraySize; i++)
                selected.Add(indicesProp.GetArrayElementAtIndex(i).intValue);

            EditorGUILayout.LabelField("Locations", EditorStyles.miniBoldLabel);

            EditorGUI.indentLevel++;
            for (int i = 0; i < cachedData.locations.Count; i++)
            {
                bool wasSelected = selected.Contains(i);
                bool isSelected = EditorGUILayout.ToggleLeft(cachedData.locations[i], wasSelected);

                if (isSelected && !wasSelected)
                    selected.Add(i);
                else if (!isSelected && wasSelected)
                    selected.Remove(i);
            }
            EditorGUI.indentLevel--;

            // Write the updated indices back.
            indicesProp.ClearArray();
            int idx = 0;
            foreach (int val in selected)
            {
                indicesProp.InsertArrayElementAtIndex(idx);
                indicesProp.GetArrayElementAtIndex(idx).intValue = val;
                idx++;
            }
        }
        else
        {
            EditorGUILayout.HelpBox("This object will appear in every location.", MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
