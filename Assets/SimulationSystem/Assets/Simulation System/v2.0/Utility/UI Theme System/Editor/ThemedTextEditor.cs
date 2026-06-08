// ThemedTextEditor.cs
// Must live inside an Editor folder.

using UnityEditor;
using UnityEngine;

namespace UIThemeSystem.Editor
{
    [CustomEditor(typeof(ThemedText))]
    public class ThemedTextEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var comp = (ThemedText)target;

            EditorGUILayout.Space(6);

            if (comp.theme != null)
            {
                Color resolved = comp.theme.Resolve(comp.role);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Resolved Color");
                    EditorGUILayout.ColorField(
                        GUIContent.none, resolved,
                        showEyedropper: false,
                        showAlpha: true,
                        hdr: false,
                        GUILayout.Width(60));
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Assign a UIThemeAsset to preview the resolved color.", MessageType.None);
            }

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(comp.theme == null))
            {
                if (GUILayout.Button("Apply Now"))
                {
                    comp.ApplyTheme();
                }
            }
        }
    }
}
