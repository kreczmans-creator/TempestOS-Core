using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// The register of worksheet calculations in the open project (ADR-0165): one row per calculation with its template,
/// revision, status, governing check and author, newest first, each opening in the worksheet form. Shown at the top of
/// Calculations → Calculation register; the showcase journey found the node listed only the bracket-check records.
/// </summary>
public sealed class CalcRegisterView : UserControl
{
    public const string Heading = "Worksheet calculations";
    public const string ListName = "Worksheet calculation register";
    public const string OpenCaption = "Open";

    /// <summary>Rows listed; the rest are counted (§3.8 caps).</summary>
    public const int MaxListed = 200;

    private readonly TextBlock _heading = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 2 };
    private readonly TextBlock _summary = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _rows = new() { Spacing = DesignTokens.SpaceXs };
    private IReadOnlyList<CalcInstance> _shown = [];

    public CalcRegisterView()
    {
        AutomationProperties.SetName(this, Heading);
        AutomationProperties.SetName(_rows, ListName);
        AutomationProperties.SetName(_summary, $"{Heading} summary");

        var panel = new StackPanel { Spacing = DesignTokens.SpaceSm, Margin = DesignTokens.PanelPadding };
        panel.Children.Add(_heading);
        panel.Children.Add(_summary);
        panel.Children.Add(Row(Header("Calculation"), Header("Worksheet"), Header("Rev"), Header("Status"), Header("Governing check"), Header("Author · updated"), new Control()));
        panel.Children.Add(new ScrollViewer { Content = _rows, MaxHeight = 320 });
        Content = new Border { Child = panel, BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Brushes.Gray };
        ShowEntries([], null, _ => null);
    }

    /// <summary>The user asked to open a calculation in the worksheet form.</summary>
    public event Action<Guid>? OpenRequested;

    /// <summary>The calculations listed, in the order shown.</summary>
    public IReadOnlyList<CalcInstance> Shown => _shown;

    /// <summary>Lists <paramref name="instances"/> (legacy archives left out), newest first; <paramref name="templateTitle"/> names each worksheet.</summary>
    public void ShowEntries(IReadOnlyList<CalcInstance> instances, string? projectName, Func<string, string?> templateTitle)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(templateTitle);
        var listed = instances.Where(i => !i.IsLegacyArchive).OrderByDescending(i => i.UpdatedAt).ThenBy(i => i.Name, StringComparer.Ordinal).ToList();
        _shown = [.. listed.Take(MaxListed)];
        _heading.Text = projectName is null ? Heading : $"{Heading} — {projectName}";
        _summary.Text = listed.Count == 0
            ? "No worksheet calculations yet. Save one from Calculations → Worksheets and it is listed here."
            : $"{listed.Count} calculation(s): {listed.Count(i => i.Status == CalcInstanceStatus.Issued)} issued, {listed.Count(i => i.Status == CalcInstanceStatus.Checked)} checked, {listed.Count(i => i.Status == CalcInstanceStatus.Draft)} draft, {listed.Count(i => i.Status == CalcInstanceStatus.Superseded)} superseded."
              + (listed.Count > MaxListed ? $" The newest {MaxListed} are listed." : string.Empty);
        _rows.Children.Clear();

        foreach (var instance in _shown)
        {
            var open = new Button { Content = OpenCaption, MinHeight = DesignTokens.MinControlSize };
            var id = instance.Id;
            open.Click += (_, _) => OpenRequested?.Invoke(id);
            AutomationProperties.SetName(open, $"{OpenCaption} {instance.Name}");

            var governing = instance.Checks.OrderByDescending(c => c.Utilisation).FirstOrDefault();
            var status = Cell(instance.Status.ToString().ToUpperInvariant());
            status.Foreground = instance.Status switch
            {
                CalcInstanceStatus.Issued => Brushes.SeaGreen,
                CalcInstanceStatus.Superseded => Brushes.Gray,
                _ => Brushes.DarkOrange,
            };
            var check = Cell(governing is null ? "—" : $"{(governing.Passed ? "PASS" : "FAIL")} u = {CalcDisplay.StoredUtilisation(governing.Utilisation)}");

            if (governing is not null)
                check.Foreground = governing.Passed ? Brushes.SeaGreen : Brushes.IndianRed;

            var row = Row(
                Cell(instance.Name),
                Cell(templateTitle(instance.TemplateReference) ?? instance.TemplateReference),
                Cell(instance.Revision),
                status,
                check,
                Cell($"{instance.CreatedBy} · {instance.UpdatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}"),
                open);
            AutomationProperties.SetName(row, instance.Name);
            _rows.Children.Add(row);
        }
    }

    /// <summary>Opens <paramref name="name"/> as its Open button would.</summary>
    public void Open(string name) => OpenRequested?.Invoke(_shown.First(i => string.Equals(i.Name, name, StringComparison.Ordinal)).Id);

    private static Grid Row(params Control[] cells)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,2*,50,100,140,200,80") };

        for (var i = 0; i < cells.Length; i++)
        {
            Grid.SetColumn(cells[i], i);
            row.Children.Add(cells[i]);
        }

        return row;
    }

    private static TextBlock Header(string text) => new() { Text = text, FontSize = DesignTokens.FontSizeCaption, FontWeight = DesignTokens.WeightHeading, Opacity = 0.8 };

    private static TextBlock Cell(string text) => new() { Text = text, FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, DesignTokens.SpaceSm, 0) };
}
