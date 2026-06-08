using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom inspector for LocationManager.
/// Shows a dropdown for selecting the active location from the LocationData asset.
/// </summary>
[CustomEditor(typeof(LocationManager))]
public class LocationManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LocationManager manager = (LocationManager)target;

        serializedObject.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Location Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        // LocationData field
        SerializedProperty dataProp = serializedObject.FindProperty("locationData");
        EditorGUILayout.PropertyField(dataProp, new GUIContent("Location Data"));

        LocationData data = (LocationData)dataProp.objectReferenceValue;

        if (data == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a LocationData asset.\nCreate one via Assets > Create > Location System > Location Data.",
                MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        if (data.locations.Count == 0)
        {
            EditorGUILayout.HelpBox("The LocationData asset has no locations defined.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // Active location dropdown
        SerializedProperty indexProp = serializedObject.FindProperty("activeLocationIndex");
        string[] names = data.locations.ToArray();
        int current = Mathf.Clamp(indexProp.intValue, 0, names.Length - 1);

        EditorGUILayout.Space(4);
        int newIndex = EditorGUILayout.Popup("Active Location", current, names);
        indexProp.intValue = newIndex;

        EditorGUILayout.Space(8);

        // Convenience button to apply in Play mode
        if (Application.isPlaying)
        {
            if (GUILayout.Button("Apply Location Now"))
            {
                manager.SetLocation(newIndex);
            }
        }

        // Button to open the browser window
        EditorGUILayout.Space(4);
        if (GUILayout.Button("Open Location Browser"))
        {
            LocationBrowserWindow.Open();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
