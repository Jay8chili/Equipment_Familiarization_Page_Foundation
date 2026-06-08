// UIThemeAssetEditor.cs
// Must live inside an Editor folder (e.g. Assets/UIThemeSystem/Editor/).
// Adds context-menu actions directly on the UIThemeAsset inspector.

using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UIThemeSystem.Editor
{
    [CustomEditor(typeof(UIThemeAsset))]
    public class UIThemeAssetEditor : UnityEditor.Editor
    {
        // ── Inspector GUI ─────────────────────────────────────────────────────

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Scene Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply to Entire Scene", GUILayout.Height(30)))
                    ApplyThemeToScene();

                if (GUILayout.Button("Preview Selection", GUILayout.Height(30)))
                    ApplyThemeToSelection();
            }

            EditorGUILayout.Space(4);

            // Show a count of themed components for quick feedback
            int imgCount  = CountInScene<ThemedImage>();
            int textCount = CountInScene<ThemedText>();
            EditorGUILayout.HelpBox(
                $"Scene contains  {imgCount} ThemedImage  +  {textCount} ThemedText components.",
                MessageType.Info);
        }

        // ── Context menu on the asset (right-click in Project window) ─────────

        [MenuItem("Assets/UI Theme System/Apply to Entire Scene")]
        private static void ApplyFromMenu()
        {
            var asset = Selection.activeObject as UIThemeAsset;
            if (asset == null)
            {
                Debug.LogWarning("UIThemeSystem: select a UIThemeAsset first.");
                return;
            }
            ApplyThemeToScene(asset);
        }

        // Validation so the menu item is greyed out when nothing suitable is selected
        [MenuItem("Assets/UI Theme System/Apply to Entire Scene", true)]
        private static bool ApplyFromMenuValidate()
            => Selection.activeObject is UIThemeAsset;

        // ── Context-menu methods on the SO itself ─────────────────────────────

        [ContextMenu("Apply to Entire Scene")]
        private void ApplyThemeToScene() => ApplyThemeToScene((UIThemeAsset)target);

        [ContextMenu("Preview on Selected GameObjects")]
        private void ApplyThemeToSelection()
        {
            var asset = (UIThemeAsset)target;
            int applied = 0;

            foreach (var go in Selection.gameObjects)
            {
                applied += ApplyToGameObject(go, asset);
            }

            Debug.Log($"UIThemeSystem: applied theme '{asset.name}' to {applied} component(s) in selection.");
        }

        // ── Core apply logic ──────────────────────────────────────────────────

        /// <summary>
        /// Finds every ThemedImage and ThemedText in the active scene
        /// that references this asset and pushes the resolved color to them.
        /// Registers an Undo group so the whole operation is one Ctrl-Z.
        /// </summary>
        private static void ApplyThemeToScene(UIThemeAsset asset)
        {
            Undo.SetCurrentGroupName($"Apply Theme '{asset.name}'");
            int group   = Undo.GetCurrentGroup();
            int applied = 0;

            // Walk every root GameObject in the active scene
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                applied += ApplyToGameObject(root, asset, recursive: true);

            Undo.CollapseUndoOperations(group);

            // Mark scene dirty so Unity knows to save
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"UIThemeSystem: applied theme '{asset.name}' to {applied} component(s) in scene.");
        }

        /// <summary>
        /// Applies the theme to all Themed components on <paramref name="root"/>
        /// and optionally its children.
        /// Only applies to components whose <c>theme</c> field matches <paramref name="asset"/>.
        /// </summary>
        private static int ApplyToGameObject(GameObject root, UIThemeAsset asset, bool recursive = true)
        {
            int count = 0;

            var images = recursive
                ? root.GetComponentsInChildren<ThemedImage>(includeInactive: true)
                : root.GetComponents<ThemedImage>();

            foreach (var c in images)
            {
                if (c.theme == asset || c.theme == null)
                {
                    if (c.theme == null) c.theme = asset;   // auto-assign if blank
                    c.ApplyTheme();
                    count++;
                }
            }

            var texts = recursive
                ? root.GetComponentsInChildren<ThemedText>(includeInactive: true)
                : root.GetComponents<ThemedText>();

            foreach (var c in texts)
            {
                if (c.theme == asset || c.theme == null)
                {
                    if (c.theme == null) c.theme = asset;
                    c.ApplyTheme();
                    count++;
                }
            }

            return count;
        }

        private static int CountInScene<T>() where T : Component
        {
            var scene = SceneManager.GetActiveScene();
            return scene.GetRootGameObjects()
                        .SelectMany(r => r.GetComponentsInChildren<T>(true))
                        .Count();
        }
    }
}
