#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runtime + Editor visualization for GridPathfinder.
/// 
/// Features:
/// - Draws the last computed smooth path
/// - Works in Edit Mode AND Play Mode
/// - Read-only (does not control pathfinding)
/// - Auto-repaints during runtime
/// </summary>
[CustomEditor(typeof(GridPathfinder))]
public class GridPathfinderEditor : Editor
{
    private GridPathfinder Path => (GridPathfinder)target;

    private void OnEnable()
    {
        // Ensure scene keeps repainting during Play Mode
        EditorApplication.update += RuntimeRepaint;
    }

    private void OnDisable()
    {
        EditorApplication.update -= RuntimeRepaint;
    }

    private void RuntimeRepaint()
    {
        if (Application.isPlaying)
            SceneView.RepaintAll();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Path Debugging", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? "Runtime mode: displaying live path computed by the bot."
                : "Editor mode: displaying the last computed path (if any).",
            MessageType.Info
        );

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear Debug Path"))
            {
                Undo.RecordObject(Path, "Clear Debug Path");
                Path.lastSmoothPath?.Clear();
                EditorUtility.SetDirty(Path);
                SceneView.RepaintAll();
            }
        }
    }

    private void OnSceneGUI()
    {
        DrawPathGizmos();
    }

    private void DrawPathGizmos()
    {
        if (Path.lastSmoothPath == null || Path.lastSmoothPath.Count < 2)
            return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        Handles.color = Color.yellow;

        // Draw polyline
        for (int i = 1; i < Path.lastSmoothPath.Count; i++)
        {
            Handles.DrawLine(
                Path.lastSmoothPath[i - 1],
                Path.lastSmoothPath[i]
            );
        }

        // Draw direction arrows (optional but very useful)
        for (int i = 0; i < Path.lastSmoothPath.Count; i += 6)
        {
            Vector3 p = Path.lastSmoothPath[i];
            Vector3 dir = Vector3.zero;

            if (i + 1 < Path.lastSmoothPath.Count)
                dir = (Path.lastSmoothPath[i + 1] - p).normalized;

            if (dir.sqrMagnitude > 0.001f)
                Handles.ArrowHandleCap(
                    0,
                    p,
                    Quaternion.LookRotation(dir),
                    0.25f,
                    EventType.Repaint
                );
        }
    }
}
#endif