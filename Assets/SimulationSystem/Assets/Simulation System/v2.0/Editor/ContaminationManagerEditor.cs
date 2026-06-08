using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ContaminationManager))]
public class ContaminationManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ContaminationManager manager = (ContaminationManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Apply Threshold & Attach ContaminationTrigger to All Colliders"))
            ApplyContaminationTriggers(manager);

        if (GUILayout.Button("Apply Speed Threshold & Attach SpeedTrackingArea to All Zones"))
            ApplySpeedTrackingAreas(manager);

        if (GUILayout.Button("Run Full Setup (Both)"))
        {
            ApplyContaminationTriggers(manager);
            ApplySpeedTrackingAreas(manager);
        }
    }

    private void ApplyContaminationTriggers(ContaminationManager manager)
    {
        int added = 0, updated = 0;

        foreach (ContaminationZone zone in manager.zones)
        {
            foreach (Collider col in zone.colliders)
            {
                if (col == null) continue;

                ContaminationTrigger trigger = col.GetComponent<ContaminationTrigger>();

                if (trigger == null)
                {
                    trigger = col.gameObject.AddComponent<ContaminationTrigger>();
                    added++;
                }
                else updated++;

                trigger.contactThreshold = manager.contactThreshold;
                EditorUtility.SetDirty(trigger);
            }
        }

        Debug.Log($"[ContaminationManager] ContaminationTrigger — Added: {added}, Updated: {updated}. Threshold: {manager.contactThreshold}s.");
    }

    private void ApplySpeedTrackingAreas(ContaminationManager manager)
    {
        int added = 0, updated = 0, skipped = 0;

        foreach (ContaminationZone zone in manager.zones)
        {
            if (zone.speedTrackingArea == null)
            {
                skipped++;
                continue;
            }

            SpeedTrackingArea area = zone.speedTrackingArea.GetComponent<SpeedTrackingArea>();

            if (area == null)
            {
                area = zone.speedTrackingArea.gameObject.AddComponent<SpeedTrackingArea>();
                added++;
            }
            else updated++;

            area.speedThreshold = zone.speedThreshold;
            EditorUtility.SetDirty(area);
        }

        Debug.Log($"[ContaminationManager] SpeedTrackingArea — Added: {added}, Updated: {updated}, Skipped (no collider assigned): {skipped}.");
    }
}