#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AssessmentWizard : EditorWindow
{
    private List<SimulationState> _states = new();
    private Vector2 _scroll;

    // ── Global AC Defaults ────────────────────────────────────────────
    private float _defaultHintPenalty = 5f;
    private float _defaultWrongDetectPenalty = 5f;
    private float _defaultWrongGrabPenalty = 5f;

    // ── Per State Marks ───────────────────────────────────────────────
    private float _marksPerState = 10f;

    [MenuItem("Simulation/Assessment Wizard")]
    public static void OpenWindow()
    {
        var window = GetWindow<AssessmentWizard>("Assessment Wizard");
        window.minSize = new Vector2(400, 480);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Assessment Wizard", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // ── Global Defaults ───────────────────────────────────────────
        EditorGUILayout.LabelField("Global Defaults", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _marksPerState = EditorGUILayout.FloatField("Marks Per State", _marksPerState);
        _defaultHintPenalty = EditorGUILayout.FloatField("Hint Penalty", _defaultHintPenalty);
        _defaultWrongDetectPenalty = EditorGUILayout.FloatField("Wrong Detect Penalty", _defaultWrongDetectPenalty);
        _defaultWrongGrabPenalty = EditorGUILayout.FloatField("Wrong Grab Penalty", _defaultWrongGrabPenalty);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Drag & Drop Area ──────────────────────────────────────────
        EditorGUILayout.LabelField("Drag & Drop States Here", EditorStyles.miniBoldLabel);
        Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drop SimulationState GameObjects here (multi-select supported)", EditorStyles.helpBox);
        HandleDragAndDrop(dropArea);
        EditorGUILayout.Space(4);

        // ── State List ────────────────────────────────────────────────
        if (_states.Count == 0)
        {
            EditorGUILayout.HelpBox("No states added yet.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField($"States ({_states.Count})", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(250));

            for (int i = 0; i < _states.Count; i++)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                bool hasAC = _states[i] != null && _states[i].GetComponent<AssessmentController>() != null;
                GUI.color = hasAC ? new Color(0.6f, 1f, 0.6f) : Color.white;
                EditorGUILayout.ObjectField(_states[i], typeof(SimulationState), allowSceneObjects: true, GUILayout.ExpandWidth(true));
                GUI.color = Color.white;

                if (hasAC)
                    EditorGUILayout.LabelField("✓ AC", GUILayout.Width(32));

                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    _states.RemoveAt(i);
                    GUIUtility.ExitGUI();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Clear All"))
                _states.Clear();
        }

        EditorGUILayout.Space(8);

        // ── Assign Button ─────────────────────────────────────────────
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Assign Assessment Controllers", GUILayout.Height(32)))
            AssignControllers();

        // ── Remove Button ─────────────────────────────────────────────
        GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
        if (GUILayout.Button("Remove Assessment Controllers", GUILayout.Height(32)))
            RemoveControllers();

        GUI.backgroundColor = Color.white;
    }

    // ─────────────────────────────────────────────
    // DRAG & DROP
    // ─────────────────────────────────────────────

    private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition)) return;

        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                int added = 0;

                foreach (var obj in DragAndDrop.objectReferences)
                {
                    SimulationState state = null;

                    if (obj is GameObject go)
                        state = go.GetComponent<SimulationState>();
                    else if (obj is SimulationState s)
                        state = s;

                    if (state == null) continue;
                    if (_states.Contains(state)) continue;

                    _states.Add(state);
                    added++;
                }

                if (added > 0)
                {
                    Debug.Log($"[AssessmentWizard] Added {added} state(s).");
                    Repaint();
                }
            }

            evt.Use();
        }
    }

    // ─────────────────────────────────────────────
    // ASSIGN
    // ─────────────────────────────────────────────

    private void AssignControllers()
    {
        int assigned = 0;
        int skipped = 0;

        foreach (var state in _states)
        {
            if (state == null) continue;

            if (state.GetComponent<AssessmentController>() != null)
            {
                Debug.Log($"[AssessmentWizard] Skipped '{state.name}' — AssessmentController already exists.");
                skipped++;
                continue;
            }

            // Collect eligible interactions — exclude IdleInteraction and UIInteraction
            var eligibleInteractions = new List<Interactions>();
            foreach (var interaction in state.listOfInteractions)
            {
                if (interaction == null) continue;
                if (interaction is IdleInteraction) continue;
                if (interaction is UIInteraction) continue;
                eligibleInteractions.Add(interaction);
            }

            // Calculate per-interaction max score — divide state marks equally
            float perInteractionScore = eligibleInteractions.Count > 0
                ? _marksPerState / eligibleInteractions.Count
                : 0f;

            Undo.RecordObject(state.gameObject, "Add AssessmentController");
            var ac = Undo.AddComponent<AssessmentController>(state.gameObject);

            foreach (var interaction in eligibleInteractions)
            {
                ac.interactionConfigs.Add(new InteractionAssessmentConfig
                {
                    interaction = interaction,
                    maxScore = perInteractionScore,
                    hintPenalty = _defaultHintPenalty,
                    wrongDetectPenalty = _defaultWrongDetectPenalty,
                    wrongGrabPenalty = _defaultWrongGrabPenalty
                });
            }

            EditorUtility.SetDirty(state.gameObject);
            Debug.Log($"[AssessmentWizard] AssessmentController added to '{state.name}' | " +
                      $"Interactions: {ac.interactionConfigs.Count} | " +
                      $"Score per interaction: {perInteractionScore:F2}");
            assigned++;
        }

        AssetDatabase.SaveAssets();
        Repaint();
        EditorUtility.DisplayDialog("Assessment Wizard",
            $"Done.\nAssigned: {assigned}\nSkipped (already had AC): {skipped}", "OK");
    }

    // ─────────────────────────────────────────────
    // REMOVE
    // ─────────────────────────────────────────────

    private void RemoveControllers()
    {
        int removed = 0;
        int notFound = 0;

        foreach (var state in _states)
        {
            if (state == null) continue;

            var ac = state.GetComponent<AssessmentController>();
            if (ac == null)
            {
                Debug.Log($"[AssessmentWizard] '{state.name}' has no AssessmentController — skipped.");
                notFound++;
                continue;
            }

            Undo.DestroyObjectImmediate(ac);
            EditorUtility.SetDirty(state.gameObject);
            Debug.Log($"[AssessmentWizard] AssessmentController removed from '{state.name}'.");
            removed++;
        }

        AssetDatabase.SaveAssets();
        Repaint();
        EditorUtility.DisplayDialog("Assessment Wizard",
            $"Done.\nRemoved: {removed}\nNot found: {notFound}", "OK");
    }
}
#endif