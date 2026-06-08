#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AssessmentController))]
public class AssessmentControllerEditor : UnityEditor.Editor
{
    private SerializedProperty _interactionConfigs;

    private void OnEnable()
    {
        _interactionConfigs = serializedObject.FindProperty("interactionConfigs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Assessment Controller", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        for (int i = 0; i < _interactionConfigs.arraySize; i++)
        {
            var config = _interactionConfigs.GetArrayElementAtIndex(i);
            var interactionProp = config.FindPropertyRelative("interaction");
            var maxScoreProp = config.FindPropertyRelative("maxScore");
            var hintPenaltyProp = config.FindPropertyRelative("hintPenalty");
            var wrongDetectPenProp = config.FindPropertyRelative("wrongDetectPenalty");
            var wrongDetectsProp = config.FindPropertyRelative("wrongDetects");
            var wotdProp = config.FindPropertyRelative("WOTD");
            var wrongGrabPenProp = config.FindPropertyRelative("wrongGrabPenalty");
            var wrongGrabsProp = config.FindPropertyRelative("wrongGrabs");

            bool isDetect = interactionProp.objectReferenceValue is DetectInteraction;
            bool isGrab = interactionProp.objectReferenceValue is GrabInteraction;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header + remove button
            EditorGUILayout.BeginHorizontal();
            string label = interactionProp.objectReferenceValue != null
                ? interactionProp.objectReferenceValue.name
                : $"Interaction {i}";
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                _interactionConfigs.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(interactionProp, new GUIContent("Interaction"));
            EditorGUILayout.PropertyField(maxScoreProp, new GUIContent("Max Score"));
            EditorGUILayout.PropertyField(hintPenaltyProp, new GUIContent("Hint Penalty"));

            // Only show wrong detect fields for DetectInteraction
            if (isDetect)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Wrong Detect Assessment", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(wrongDetectPenProp, new GUIContent("Wrong Detect Penalty"));
                EditorGUILayout.PropertyField(wrongDetectsProp, new GUIContent("Wrong Detects"), true);
                EditorGUILayout.PropertyField(wotdProp, new GUIContent("Wrong Objects To Detect (WOTD)"), true);
            }

            // Only show wrong grab fields for GrabInteraction
            if (isGrab)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Wrong Grab Assessment", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(wrongGrabPenProp, new GUIContent("Wrong Grab Penalty"));
                EditorGUILayout.PropertyField(wrongGrabsProp, new GUIContent("Wrong Grabs"), true);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        if (GUILayout.Button("+ Add Interaction Config"))
            _interactionConfigs.InsertArrayElementAtIndex(_interactionConfigs.arraySize);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif