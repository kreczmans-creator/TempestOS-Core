using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Tempest.Desktop.Theming;

/// <summary>
/// Binds a control's own brush property to one of <see cref="ApplicationPalette"/>'s
/// own keys, re-resolving on both attachment and every subsequent theme
/// change (`WP 10.5A`).
/// </summary>
/// <remarks>
/// <para>
/// Two real, disclosed findings of this Work Package's own theme-audit
/// implementation, both confirmed directly by a failing test before this
/// class reached its final shape:
/// </para>
/// <para>
/// (1) Avalonia's own <c>GetResourceObservable</c> + <c>Bind</c>
/// combination — the conventional "DynamicResource in code-behind"
/// pattern — does not reliably re-push a value once a control that
/// subscribed while unattached is later attached to a real visual tree.
/// </para>
/// <para>
/// (2) <c>StyledElement.TryFindResource</c> (walking upward from the
/// control itself through its own resource-host chain) does not reach
/// <see cref="Application.Resources"/> in this platform's own headless
/// test topology either, even once attached to a shown
/// <see cref="Window"/> — confirmed by direct diagnostic assertion.
/// <see cref="Application.TryGetResource(object, ThemeVariant?, out object?)"/>,
/// called directly against <see cref="Application.Current"/> using the
/// control's own <see cref="StyledElement.ActualThemeVariant"/>, is the
/// robust, verified-working alternative used everywhere in this codebase
/// instead — this platform owns <see cref="ApplicationPalette"/>'s
/// resources at the <see cref="Application"/> level specifically so this
/// direct route is always correct, never dependent on intermediate
/// resource-host wiring this platform does not control.
/// </para>
/// </remarks>
internal static class ThemeReactiveBrush
{
    /// <summary>Binds <paramref name="control"/>'s own <paramref name="property"/> to <paramref name="resourceKey"/>, re-resolving on attach and on every theme change.</summary>
    public static void Bind(Control control, AvaloniaProperty property, string resourceKey)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(resourceKey);

        void Apply()
        {
            if (Application.Current?.TryGetResource(resourceKey, control.ActualThemeVariant, out var value) == true && value is IBrush)
                control.SetValue(property, value);
        }

        control.AttachedToVisualTree += (_, _) => Apply();
        control.ActualThemeVariantChanged += (_, _) => Apply();
        Apply();
    }
}
