// ThemedText.cs
// Place anywhere outside an Editor folder.
//
// Unity 6 ships TextMeshPro as a core package — TMPro is always available,
// so there are no compile guards around it. The legacy Text fallback is kept
// for completeness but TMP is tried first.
//
// EDITOR-ONLY DESIGN: identical to ThemedImage — color is baked in, no runtime hooks.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UIThemeSystem
{
    [DisallowMultipleComponent]
    public class ThemedText : MonoBehaviour
    {
        [Tooltip("The theme asset that owns the color palette.")]
        public UIThemeAsset theme;

        [Tooltip("Which semantic color slot this text should use.")]
        public ColorRole role = ColorRole.Text;

        /// <summary>
        /// Resolves the color and writes it to whichever text component is present.
        /// TMP_Text (covers TextMeshProUGUI and TextMeshPro) takes priority over legacy Text.
        /// </summary>
        public void ApplyTheme()
        {
            if (theme == null) return;

            Color resolved = theme.Resolve(role);

            // TMP_Text is the base class for both TextMeshProUGUI and TextMeshPro (world space)
            var tmp = GetComponent<TMP_Text>();
            if (tmp != null)
            {
#if UNITY_EDITOR
                Undo.RecordObject(tmp, "Apply Theme to Text");
#endif
                tmp.color = resolved;
#if UNITY_EDITOR
                EditorUtility.SetDirty(tmp);
#endif
                return;
            }

            // Fallback: legacy UnityEngine.UI.Text
            var legacyText = GetComponent<Text>();
            if (legacyText != null)
            {
#if UNITY_EDITOR
                Undo.RecordObject(legacyText, "Apply Theme to Text");
#endif
                legacyText.color = resolved;
#if UNITY_EDITOR
                EditorUtility.SetDirty(legacyText);
#endif
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!gameObject.scene.IsValid()) return;

            ApplyTheme();
        }
#endif
    }
}