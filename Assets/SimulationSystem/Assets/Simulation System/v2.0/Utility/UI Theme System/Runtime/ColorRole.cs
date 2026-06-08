// ColorRole.cs
// Place anywhere outside an Editor folder — it's just data, no runtime logic.

namespace UIThemeSystem
{
    /// <summary>
    /// Semantic color slots defined by the theme.
    /// Components declare *which* role they want; the asset owns the actual color.
    /// </summary>
    public enum ColorRole
    {
        Primary,
        Secondary,
        Accent,
        Background,
        Border,
        Text,
        TextSecondary,
        TextTertiary,
        Highlight,
        Danger,
    }
}
