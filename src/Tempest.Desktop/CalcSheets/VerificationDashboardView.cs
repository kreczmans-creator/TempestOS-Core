using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Calc.Library.Dashboard;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Addendum C, C-4: Calculations → Verification. One read-only screen listing every worksheet calculator and every chain with its version,
/// known-answer results, Unconfirmed and Secondary counts, Track P re-derivation result, record kinds not entered, open defects and manual-test
/// status, filtered by discipline and status and exported to markdown and PDF. All behaviour is in <see cref="VerificationDashboardViewModel"/>;
/// this class only draws it. It has no button that changes a calculation, an Evidence status or a record.
/// </summary>
public sealed class VerificationDashboardView : UserControl
{
    public const string Heading = "Verification dashboard";
    public const string ListName = "Verification dashboard rows";
    public const string DisciplineFilterName = "Discipline filter";
    public const string StatusFilterName = "Status filter";
    public const string RefreshCaption = "Refresh";
    public const string ExportMarkdownCaption = "Export markdown…";
    public const string ExportPdfCaption = "Export PDF…";

    private readonly VerificationDashboardViewModel _model;
    private readonly ComboBox _discipline = new() { MinWidth = 200 };
    private readonly ComboBox _status = new() { MinWidth = 220 };
    private readonly Button _refresh = new() { Content = RefreshCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _markdown = new() { Content = ExportMarkdownCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _pdf = new() { Content = ExportPdfCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _summary = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _statusLine = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 };
    private readonly StackPanel _rows = new() { Spacing = DesignTokens.SpaceXs };
    private bool _rendering;

    public VerificationDashboardView(VerificationDashboardViewModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));

        AutomationProperties.SetName(this, Heading);
        AutomationProperties.SetName(_discipline, DisciplineFilterName);
        AutomationProperties.SetName(_status, StatusFilterName);
        AutomationProperties.SetName(_refresh, RefreshCaption);
        AutomationProperties.SetName(_markdown, ExportMarkdownCaption);
        AutomationProperties.SetName(_pdf, ExportPdfCaption);
        AutomationProperties.SetName(_summary, $"{Heading} summary");
        AutomationProperties.SetName(_statusLine, $"{Heading} status");
        AutomationProperties.SetName(_rows, ListName);

        foreach (var (filter, label) in VerificationDashboardViewModel.StatusChoices)
            _status.Items.Add(new ComboBoxItem { Content = label, Tag = filter });

        _status.SelectedIndex = 0;

        _discipline.SelectionChanged += (_, _) =>
        {
            if (!_rendering && _discipline.SelectedItem is string chosen)
                _model.SetDiscipline(chosen);
        };
        _status.SelectionChanged += (_, _) =>
        {
            if (!_rendering && _status.SelectedItem is ComboBoxItem { Tag: VerificationStatusFilter filter })
                _model.SetStatus(filter);
        };
        _refresh.Click += (_, _) => _ = _model.RefreshAsync();
        _markdown.Click += (_, _) => _ = _model.ExportMarkdownAsync();
        _pdf.Click += (_, _) => _ = _model.ExportPdfAsync();

        var title = new TextBlock { Text = Heading, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeTitle };
        var note = new TextBlock
        {
            Text = "Read-only. It lists what the tests, the Track P verification documents and the records in this database say about each calculator and chain; it changes none of them.",
            FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Opacity = 0.8,
        };
        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };

        foreach (var control in new Control[] { new TextBlock { Text = "Discipline", VerticalAlignment = VerticalAlignment.Center }, _discipline, new TextBlock { Text = "Status", VerticalAlignment = VerticalAlignment.Center }, _status, _refresh, _markdown, _pdf })
            filters.Children.Add(control);

        var panel = new StackPanel { Spacing = DesignTokens.SpaceSm, Margin = DesignTokens.PanelPadding };
        panel.Children.Add(title);
        panel.Children.Add(note);
        panel.Children.Add(filters);
        panel.Children.Add(_summary);
        panel.Children.Add(_statusLine);
        panel.Children.Add(Row(Header("Calculator or chain"), Header("Discipline"), Header("Version"), Header("Known answers"), Header("Unconf."), Header("Second."), Header("Track P re-derivation"), Header("Records not entered"), Header("Open defects"), Header("Manual test")));
        panel.Children.Add(_rows);

        Content = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        _model.Changed += Render;
        Render();
    }

    /// <summary>The rows are read again (the screen was opened).</summary>
    public Task RefreshAsync() => _model.RefreshAsync();

    /// <summary>The state behind the screen.</summary>
    public VerificationDashboardViewModel Model => _model;

    private void Render()
    {
        _rendering = true;

        try
        {
            _discipline.ItemsSource = _model.DisciplineChoices;
            _discipline.SelectedItem = _model.DisciplineChoices.Contains(_model.DisciplineFilter) ? _model.DisciplineFilter : VerificationDashboardViewModel.AllDisciplines;
            _status.SelectedItem = _status.Items.OfType<ComboBoxItem>().First(i => i.Tag is VerificationStatusFilter f && f == _model.StatusFilter);
            _summary.Text = _model.Summary;
            _statusLine.Text = _model.Status;
            _statusLine.IsVisible = _model.Status.Length > 0;
            _refresh.IsEnabled = !_model.IsBusy;
            _markdown.IsEnabled = !_model.IsBusy && _model.Rows.Count > 0;
            _pdf.IsEnabled = !_model.IsBusy && _model.Rows.Count > 0;
            _rows.Children.Clear();

            foreach (var r in _model.Rows)
            {
                var kat = Cell(r.KatText);
                kat.Foreground = r.KatState switch { KatRunState.Failed => Brushes.IndianRed, KatRunState.Passed => Brushes.SeaGreen, _ => Brushes.DarkOrange };

                var open = Cell(r.OpenDefectCount.ToString(System.Globalization.CultureInfo.InvariantCulture));

                if (r.OpenDefectCount > 0)
                {
                    open.Foreground = Brushes.IndianRed;
                    ToolTip.SetTip(open, string.Join("\n", r.OpenDefects.Take(30).Select(d => $"{d.Id} — {d.Severity}, {d.StatusLabel.ToLowerInvariant()}")) + (r.OpenDefectCount > 30 ? "\n…" : string.Empty));
                }

                var records = Cell(r.MissingRecordKinds.Count == 0 ? "none" : string.Join("; ", r.MissingRecordKinds));

                if (r.MissingRecordKinds.Count > 0)
                    records.Foreground = Brushes.DarkOrange;

                var row = Row(
                    Cell(r.Kind == VerificationRowKind.Chain ? $"{r.Title} (chain)" : r.Title), Cell(r.Discipline), Cell(r.Version.Length > 0 ? r.Version : "—"), kat,
                    Cell(r.Unconfirmed.ToString(System.Globalization.CultureInfo.InvariantCulture)), Cell(r.Secondary.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    Cell($"{r.RederivationResult}: {r.RederivationFindings.Text}"), records, open, Cell(r.ManualTest.Label));
                AutomationProperties.SetName(row, r.Id);

                if (r.Steps.Count > 0)
                    ToolTip.SetTip(row, "Calculators: " + string.Join(", ", r.Steps));

                _rows.Children.Add(row);
            }
        }
        finally
        {
            _rendering = false;
        }
    }

    private static Grid Row(params Control[] cells)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,110,60,150,60,60,2*,2*,80,100") };

        for (var i = 0; i < cells.Length; i++)
        {
            Grid.SetColumn(cells[i], i);
            row.Children.Add(cells[i]);
        }

        return row;
    }

    private static TextBlock Header(string text) => new() { Text = text, FontSize = DesignTokens.FontSizeCaption, FontWeight = DesignTokens.WeightHeading, Opacity = 0.8, TextWrapping = TextWrapping.Wrap };

    private static TextBlock Cell(string text) => new() { Text = text, FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, DesignTokens.SpaceSm, 0) };
}
