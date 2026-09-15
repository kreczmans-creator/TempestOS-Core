using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views.EngineeringAssets;

/// <summary>One row a filterable Engineering Assets list should show.</summary>
/// <param name="Id">The record's own catalogue identity — what <c>Open</c> navigates to.</param>
/// <param name="Label">The single line the row reads — the columns this record kind carries, joined for display exactly as every other list row in this area already is (`LibrariesView.BuildRow`, `ReportsView`).</param>
internal readonly record struct AssetListRow(string Id, string Label);

/// <summary>
/// Renders one filtered, sorted list of <see cref="AssetListRow"/>s with
/// an Open button per row — the one row-building routine every list in
/// the Engineering Assets area shares (`WP 21.2B`, scope item 1: "each
/// list with the columns its record carries, a filter box, Open on each
/// row"), rather than four near-identical copies.
/// </summary>
internal static class EngineeringAssetListBuilder
{
    /// <summary>Filters <paramref name="rows"/> by <paramref name="filterText"/> (matched against <see cref="AssetListRow.Id"/> and <see cref="AssetListRow.Label"/>, case-insensitive) and renders the result into <paramref name="container"/>.</summary>
    public static void Render(StackPanel container, IEnumerable<AssetListRow> rows, string? filterText, Action<string> onOpen)
    {
        container.Children.Clear();

        var filtered = rows
            .Where(r => string.IsNullOrWhiteSpace(filterText)
                || r.Id.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                || r.Label.Contains(filterText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToList();

        if (filtered.Count == 0)
        {
            container.Children.Add(new TextBlock { Text = "No records match.", Opacity = 0.7, FontSize = DesignTokens.FontSizeBody });
            return;
        }

        foreach (var row in filtered)
            container.Children.Add(BuildRow(row, onOpen));
    }

    private static Control BuildRow(AssetListRow row, Action<string> onOpen)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs) };

        var text = new TextBlock
        {
            Text = row.Label, TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);
        grid.DoubleTapped += (_, _) => onOpen(row.Id);

        var open = new Button { Content = "Open", Padding = new Thickness(10, 2) };
        open.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(open, $"Open {row.Id}");
        open.Click += (_, _) => onOpen(row.Id);
        Grid.SetColumn(open, 1);
        grid.Children.Add(open);

        return grid;
    }
}
