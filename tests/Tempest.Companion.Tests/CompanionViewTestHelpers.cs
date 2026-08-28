using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Tempest.Companion.Services;
using Tempest.Companion.Offline;

namespace Tempest.Companion.Tests;

/// <summary>Shared helpers for the headless view tests.</summary>
internal static class CompanionViewTestHelpers
{
    /// <summary>Builds a data service over a fake client and an isolated cache directory.</summary>
    public static CompanionDataService BuildDataService(FakeCompanionApiClient client, TempDirectory temp) =>
        new(client, new SnapshotCache(temp.Path));

    /// <summary>
    /// Shows <paramref name="content"/> inside a fixed phone-sized
    /// container in a headless window and completes a layout pass — the
    /// container, not the window, constrains the viewport, because the
    /// headless windowing platform ignores a requested window size.
    /// </summary>
    public static Window ShowInPhoneWindow(Control content, double width = 393, double height = 852)
    {
        var window = new Window
        {
            Content = new Border
            {
                Width = width,
                Height = height,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                Child = content,
            },
        };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    /// <summary>Collects every control of type <typeparamref name="T"/> in <paramref name="root"/>'s visual tree.</summary>
    public static IReadOnlyList<T> FindControls<T>(Visual root)
        where T : Visual =>
        root.GetVisualDescendants().OfType<T>().ToList();

    /// <summary>Collects every rendered <see cref="TextBlock"/> text in <paramref name="root"/>'s visual tree.</summary>
    public static IReadOnlyList<string> CollectTexts(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();

    /// <summary>Asserts some rendered text contains <paramref name="fragment"/> (ordinal, case-insensitive).</summary>
    public static void AssertShowsText(Visual root, string fragment)
    {
        var texts = CollectTexts(root);
        Assert.True(
            texts.Any(t => t.Contains(fragment, StringComparison.OrdinalIgnoreCase)),
            $"Expected some rendered text to contain '{fragment}'. Rendered: {string.Join(" | ", texts.Where(t => t.Length > 0).Take(40))}");
    }
}
