// ThemedImage.cs
// Place anywhere outside an Editor folder.
//
// EDITOR-ONLY DESIGN:
//   - No Start(), no Update(), no Awake() runtime hooks.
//   - Color is written directly to Image.color in the editor.
//   - At runtime the Image just has whatever color was last applied — zero overhead.
//   - The #if UNITY_EDITOR block handles auto-apply on value change in the Inspector.

using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UIThemeSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public class ThemedImage : MonoBehaviour
    {
        [Tooltip("The theme asset that owns the color palette.")]
        public UIThemeAsset theme;

        [Tooltip("Which semantic color slot this image should use.")]
        public ColorRole role = ColorRole.Primary;

        /// <summary>
        /// Reads the resolved color from the asset and writes it directly to
        /// the Image component. Call this from editor tooling only.
        /// </summary>
        public void ApplyTheme()
        {
            if (theme == null) return;

            var img = GetComponent<Image>();
            if (img == null)   return;

#if UNITY_EDITOR
            Undo.RecordObject(img, "Apply Theme to Image");
#endif
            img.color = theme.Resolve(role);

#if UNITY_EDITOR
            EditorUtility.SetDirty(img);
#endif
        }

#if UNITY_EDITOR
        // Called by Unity when any serialized field changes in the Inspector.
        private void OnValidate()
        {
            // Small guard: OnValidate fires during prefab import too,
            // so only act when we're actually editing a scene object.
            if (!gameObject.scene.IsValid()) return;

            ApplyTheme();
        }
#endif
    }
}
