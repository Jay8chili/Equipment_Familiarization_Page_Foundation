using UnityEditor;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

public class FixMetaQuestFeature
{
    [MenuItem("Tools/Fix MetaQuest Feature Asset")]
    static void Fix()
    {
        // Use GetSettingsForBuildTargetGroup — works across all recent OpenXR versions
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(
            BuildTargetGroup.Android
        );

        if (settings == null)
        {
            Debug.LogError("OpenXR Android settings not found! " +
                "Enable OpenXR in XR Plug-in Management (Android tab) first.");
            return;
        }

        Debug.Log("OpenXR settings found. Scanning features via SerializedObject...");

        // Access features via SerializedObject to avoid API version mismatches
        var so = new SerializedObject(settings);
        var featuresProp = so.FindProperty("features");

        if (featuresProp == null)
        {
            Debug.LogWarning("Could not find 'features' property via serialization.");
        }
        else
        {
            Debug.Log($"Feature count: {featuresProp.arraySize}");

            for (int i = 0; i < featuresProp.arraySize; i++)
            {
                var element = featuresProp.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == null)
                {
                    Debug.LogWarning($"  [index {i}] NULL feature reference found!");
                }
                else
                {
                    var feature = element.objectReferenceValue as OpenXRFeature;
                    Debug.Log($"  [index {i}] {feature?.name} | enabled: {feature?.enabled}");
                }
            }
        }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Done. Check above for any NULL entries.");
    }
}