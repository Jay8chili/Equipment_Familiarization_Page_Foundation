// UIThemeAsset.cs
// Place anywhere outside an Editor folder.
// This is pure data — no MonoBehaviour, no Update, no runtime dependencies.

using System.Collections.Generic;
using UnityEngine;

namespace UIThemeSystem
{
    [CreateAssetMenu(
        fileName = "NewUITheme",
        menuName  = "UI Theme System/Theme Asset",
        order     = 0)]
    public class UIThemeAsset : ScriptableObject
    {
        // ── Color palette ─────────────────────────────────────────────────────

        [Header("Core Colors")]
        public Color primary       = new Color(0.20f, 0.47f, 0.95f);
        public Color secondary     = new Color(0.35f, 0.35f, 0.40f);
        public Color accent        = new Color(0.95f, 0.60f, 0.10f);
        public Color background    = new Color(0.10f, 0.10f, 0.12f);
        public Color border        = new Color(0.30f, 0.30f, 0.34f);

        [Header("Text Colors")]
        public Color text          = new Color(0.95f, 0.95f, 0.95f);
        public Color textSecondary = new Color(0.65f, 0.65f, 0.68f);
        public Color textTertiary  = new Color(0.50f, 0.50f, 0.54f);

        [Header("State Colors")]
        public Color highlight     = new Color(0.20f, 0.85f, 0.55f);
        public Color danger        = new Color(0.90f, 0.25f, 0.25f);

        // ── Role resolver ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns the Color mapped to the given semantic role.
        /// All application logic stays in the Editor scripts;
        /// this method is just a pure lookup with no side-effects.
        /// </summary>
        public Color Resolve(ColorRole role)
        {
            return role switch
            {
                ColorRole.Primary       => primary,
                ColorRole.Secondary     => secondary,
                ColorRole.Accent        => accent,
                ColorRole.Background    => background,
                ColorRole.Border        => border,
                ColorRole.Text          => text,
                ColorRole.TextSecondary => textSecondary,
                ColorRole.TextTertiary  => textTertiary,
                ColorRole.Highlight     => highlight,
                ColorRole.Danger        => danger,
                _                       => Color.magenta   // fallback — easy to spot
            };
        }

        // ── Convenience: all roles ────────────────────────────────────────────

        public static IEnumerable<ColorRole> AllRoles()
        {
            foreach (ColorRole r in System.Enum.GetValues(typeof(ColorRole)))
                yield return r;
        }
    }
}
