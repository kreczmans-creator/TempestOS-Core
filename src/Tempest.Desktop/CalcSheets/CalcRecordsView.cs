using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Tempest.Calc.Library;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Addendum C, C-3: the records screen (Engineering → Modules → Calculations → Reference records). One screen per record type, picked on the left:
/// its records (never their values), then the entry of new ones from a Track P record template, either typed into a grid whose columns carry their
/// units or read from a CSV file with a preview, a column mapping, a unit check per column, row-level errors, and an all-or-nothing commit. The
/// provenance (source, edition and amendment, table or clause, licence holder) is required, "Licensed source" is on by default, and the person who
/// entered it is the signed-in one. There is no export from here. All behaviour is in <see cref="CalcRecordsViewModel"/>.
/// </summary>
public sealed class CalcRecordsView : UserControl
{
    public const string ScreenName = "Records screen";
    public const string TypesName = "Record types";
    public const string RecordsListName = "Records of this type";
    public const string TemplateName = "Record template";
    public const string GridName = "Record grid";
    public const string StatusName = "Records screen status";
    public const string ProvenanceProblemName = "Provenance problem";
    public const string GridSummaryName = "Typed rows summary";
    public const string GridIssuesName = "Typed rows check";
    public const string FileSummaryName = "File summary";
    public const string FileIssuesName = "File check";
    public const string FileNameLabel = "File read";
    public const string MappingName = "Column mapping";
    public const string NumberFormatName = "Number format";

    public const string TitleFieldName = "Record title";
    public const string SourceFieldName = "Record source title";
    public const string EditionFieldName = "Record edition";
    public const string AmendmentFieldName = "Record amendment";
    public const string TableFieldName = "Record table or clause";
    public const string HolderFieldName = "Record licence holder";
    public const string EnteredByName = "Record entered by";
    public const string NotesFieldName = "Record notes";
    public const string LicensedName = "Record licensed source (never exported)";
    public const string OriginName = "Record origin";
    public const string RevisionName = "Save as the next revision when the title exists";

    public const string AddRowCaption = "Add row";
    public const string RemoveRowCaption = "Remove last row";
    public const string ClearGridCaption = "Clear grid";
    public const string SaveGridCaption = "Save typed rows";
    public const string ChooseFileCaption = "Choose CSV file…";
    public const string ImportFileCaption = "Import file";
    public const string RemoveFileCaption = "Remove file";
    public const string LoadProvenanceCaption = "Load provenance file…";

    /// <summary>The caption of the map-to choice that leaves a file column out.</summary>
    public const string LeaveOut = "(leave out)";

    private const string PointCaption = "Decimal point (1234.5)";
    private const string CommaCaption = "Decimal comma (1234,5)";

    private readonly CalcRecordsViewModel _model;
    private readonly ListBox _types = new();
    private readonly TextBlock _heading = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeTitle };
    private readonly TextBlock _usedBy = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 };
    private readonly StackPanel _records = new() { Spacing = DesignTokens.SpaceXs };
    private readonly ComboBox _template = new() { MinWidth = 360, MinHeight = DesignTokens.MinControlSize, PlaceholderText = "Record template" };
    private readonly TextBlock _templateNote = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 };
    private readonly TextBox _title = Field(TitleFieldName, "Title of the record (as it appears on sheets)");
    private readonly TextBox _source = Field(SourceFieldName, "Source title");
    private readonly TextBox _edition = Field(EditionFieldName, "Edition");
    private readonly TextBox _amendment = Field(AmendmentFieldName, "Amendment (blank if none)");
    private readonly TextBox _table = Field(TableFieldName, "Table or clause");
    private readonly TextBox _holder = Field(HolderFieldName, "Licence holder");
    private readonly TextBox _enteredBy = new() { MinWidth = 160, IsReadOnly = true, Focusable = true };
    private readonly TextBox _notes = Field(NotesFieldName, "Notes (optional)");
    private readonly CheckBox _licensed = new() { Content = "Licensed source (never exported)", IsChecked = true };
    private readonly ComboBox _origin = new() { MinWidth = 160, MinHeight = DesignTokens.MinControlSize };
    private readonly CheckBox _revision = new() { Content = "Save as the next revision when the title exists" };
    private readonly Button _loadProvenance = new() { Content = LoadProvenanceCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly ComboBox _numberFormat = new() { MinWidth = 200, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _provenanceProblem = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkOrange };

    private readonly Grid _grid = new();
    private readonly List<TextBlock> _gridStatus = [];
    private readonly Button _addRow = new() { Content = AddRowCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _removeRow = new() { Content = RemoveRowCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _clearGrid = new() { Content = ClearGridCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _saveGrid = new() { Content = SaveGridCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly TextBlock _gridSummary = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _gridIssues = new() { Spacing = 2 };

    private readonly Button _chooseFile = new() { Content = ChooseFileCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _importFile = new() { Content = ImportFileCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Button _removeFile = new() { Content = RemoveFileCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly TextBlock _fileLabel = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _mapping = new() { Spacing = DesignTokens.SpaceXs };
    private readonly TextBlock _fileSummary = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _fileIssues = new() { Spacing = 2 };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };

    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private bool _rendering;

    public CalcRecordsView(CalcRecordsViewModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        AutomationProperties.SetName(this, ScreenName);
        AutomationProperties.SetName(_types, TypesName);
        AutomationProperties.SetName(_records, RecordsListName);
        AutomationProperties.SetName(_template, TemplateName);
        AutomationProperties.SetName(_enteredBy, EnteredByName);
        AutomationProperties.SetName(_licensed, LicensedName);
        AutomationProperties.SetName(_origin, OriginName);
        AutomationProperties.SetName(_revision, RevisionName);
        AutomationProperties.SetName(_numberFormat, NumberFormatName);
        AutomationProperties.SetName(_provenanceProblem, ProvenanceProblemName);
        AutomationProperties.SetName(_grid, GridName);
        AutomationProperties.SetName(_addRow, AddRowCaption);
        AutomationProperties.SetName(_removeRow, RemoveRowCaption);
        AutomationProperties.SetName(_clearGrid, ClearGridCaption);
        AutomationProperties.SetName(_saveGrid, SaveGridCaption);
        AutomationProperties.SetName(_loadProvenance, LoadProvenanceCaption);
        AutomationProperties.SetName(_gridSummary, GridSummaryName);
        AutomationProperties.SetName(_gridIssues, GridIssuesName);
        AutomationProperties.SetName(_chooseFile, ChooseFileCaption);
        AutomationProperties.SetName(_importFile, ImportFileCaption);
        AutomationProperties.SetName(_removeFile, RemoveFileCaption);
        AutomationProperties.SetName(_fileLabel, FileNameLabel);
        AutomationProperties.SetName(_mapping, MappingName);
        AutomationProperties.SetName(_fileSummary, FileSummaryName);
        AutomationProperties.SetName(_fileIssues, FileIssuesName);
        AutomationProperties.SetName(_status, StatusName);
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        AutomationProperties.SetLiveSetting(_gridSummary, AutomationLiveSetting.Polite);
        AutomationProperties.SetLiveSetting(_fileSummary, AutomationLiveSetting.Polite);

        _numberFormat.ItemsSource = new[] { PointCaption, CommaCaption };
        _numberFormat.SelectedIndex = 0;
        _origin.ItemsSource = new[] { CalcDataOrigin.UserEntered, CalcDataOrigin.Derived, CalcDataOrigin.ManufacturerExample };
        _origin.SelectedIndex = 0;

        Content = BuildLayout();
        Wire();
        _model.Changed += Render;
        _model.PreviewChanged += RenderPreview;
        Render();
    }

    public CalcRecordsViewModel Model => _model;

    /// <summary>Raised when the person asks for the screen to be refreshed (it re-reads on entry; this is for a test).</summary>
    public Task RefreshAsync(string? kind = null) => _model.RefreshAsync(kind);

    // ------------------------------------------------------------------ layout

    private static TextBox Field(string name, string watermark)
    {
        var box = new TextBox { MinWidth = 220, MaxLength = 200, Watermark = watermark };
        AutomationProperties.SetName(box, name);
        return box;
    }

    private static TextBlock Heading(string text) => new() { Text = text, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 2, Margin = DesignTokens.SectionMargin };

    private Control BuildLayout()
    {
        // The record types scroll on their own, so the long list never pushes the entry below the fold.
        var left = new DockPanel { Margin = DesignTokens.PanelPadding };
        var leftHead = new StackPanel { Spacing = DesignTokens.SpaceSm, Margin = new Thickness(0, 0, 0, DesignTokens.SpaceSm) };
        leftHead.Children.Add(new TextBlock { Text = "Record types", FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading });
        leftHead.Children.Add(new TextBlock { Text = "One per kind of reference record the calculators draw on. A type marked ! is required by a calculator and has no record yet.", FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });
        DockPanel.SetDock(leftHead, Dock.Top);
        left.Children.Add(leftHead);
        left.Children.Add(_types);

        var right = new StackPanel { Spacing = DesignTokens.SpaceSm, Margin = DesignTokens.PanelPadding };
        right.Children.Add(_heading);
        right.Children.Add(_usedBy);
        right.Children.Add(Heading("Records of this type"));
        right.Children.Add(new TextBlock { Text = "A record is never edited: a change is a new revision. The values of a licensed record are not shown here and cannot be exported.", FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });
        right.Children.Add(_records);

        right.Children.Add(Heading("Where the values come from"));
        right.Children.Add(Labelled("Record template", _template));
        right.Children.Add(_templateNote);

        var fields = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var (label, box) in new (string, TextBox)[]
                 {
                     ("Title (required)", _title), ("Source title (required)", _source), ("Edition (required)", _edition),
                     ("Amendment (blank if none)", _amendment), ("Table or clause (required)", _table), ("Licence holder (required)", _holder),
                 })
        {
            var labelled = Labelled(label, box);
            labelled.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceXs);
            fields.Children.Add(labelled);
        }

        var more = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var control in new Control[] { Labelled("Entered by (signed in)", _enteredBy), Labelled("Origin", _origin), _licensed, _revision, _loadProvenance })
        {
            control.Margin = new Thickness(0, 0, DesignTokens.SpaceMd, DesignTokens.SpaceXs);
            more.Children.Add(control);
        }

        right.Children.Add(new TextBlock { Text = "Provenance: every field but the amendment is required. The amendment is blank if the copy has none.", FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85, TextWrapping = TextWrapping.Wrap });
        right.Children.Add(fields);
        right.Children.Add(Labelled("Notes (optional)", _notes));
        right.Children.Add(more);
        right.Children.Add(_provenanceProblem);
        right.Children.Add(Labelled("Number format of the values", _numberFormat));

        right.Children.Add(Heading("Type the rows"));
        right.Children.Add(new TextBlock { Text = "One column per column of the template. Set each column's unit in its header: a unit of another kind is refused, one of the same kind is kept as typed.", FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });
        right.Children.Add(new ScrollViewer { Content = _grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        right.Children.Add(Row(_addRow, _removeRow, _clearGrid, _saveGrid));
        right.Children.Add(_gridSummary);
        right.Children.Add(_gridIssues);

        right.Children.Add(Heading("Or import a CSV file"));
        right.Children.Add(new TextBlock { Text = "A CSV that follows the template above: UTF-8, one header row of symbol[unit]. It is checked whole; if any row is wrong nothing is saved.", FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });
        right.Children.Add(Row(_chooseFile, _removeFile, _importFile));
        right.Children.Add(_fileLabel);
        right.Children.Add(_mapping);
        right.Children.Add(_fileSummary);
        right.Children.Add(_fileIssues);
        right.Children.Add(_status);

        var split = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*") };
        split.Children.Add(left);
        var scroll = new ScrollViewer { Content = right, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(scroll, 1);
        split.Children.Add(scroll);
        return split;
    }

    private static Control Labelled(string label, Control control)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = label, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85 });
        panel.Children.Add(control);
        return panel;
    }

    private static Control Row(params Control[] controls)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var control in controls)
        {
            control.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceXs);
            row.Children.Add(control);
        }

        return row;
    }

    // ------------------------------------------------------------------ events

    private void Wire()
    {
        _types.SelectionChanged += async (_, _) => await GuardedAsync(OnTypeAsync, "show that record type").ConfigureAwait(true);
        _template.SelectionChanged += async (_, _) => await GuardedAsync(OnTemplateAsync, "show that template").ConfigureAwait(true);
        _numberFormat.SelectionChanged += async (_, _) => await GuardedAsync(async () =>
        {
            if (!_rendering)
                await _model.SetDecimalCommaAsync(_numberFormat.SelectedIndex == 1).ConfigureAwait(true);
        }, "read the file under that number format").ConfigureAwait(true);

        foreach (var box in new[] { _title, _source, _edition, _amendment, _table, _holder, _notes })
            box.TextChanged += (_, _) => OnProvenanceChanged();

        _licensed.IsCheckedChanged += (_, _) => OnProvenanceChanged();
        _revision.IsCheckedChanged += (_, _) => OnProvenanceChanged();
        _origin.SelectionChanged += (_, _) => OnProvenanceChanged();

        _addRow.Click += (_, _) => _model.AddRow();
        _removeRow.Click += async (_, _) => { _model.RemoveLastRow(); await GuardedAsync(() => _model.PreviewGridAsync(), "check the typed rows").ConfigureAwait(true); };
        _clearGrid.Click += (_, _) => _model.ClearGrid();
        _saveGrid.Click += async (_, _) => await GuardedAsync(async () => { await _model.SaveGridAsync().ConfigureAwait(true); }, "save the typed table").ConfigureAwait(true);
        _chooseFile.Click += async (_, _) => await GuardedAsync(PickFileAsync, "read the file").ConfigureAwait(true);
        _removeFile.Click += (_, _) => _model.ClearFile();
        _importFile.Click += async (_, _) => await GuardedAsync(async () => { await _model.ImportFileAsync().ConfigureAwait(true); }, "import the file").ConfigureAwait(true);
        _loadProvenance.Click += async (_, _) => await GuardedAsync(PickProvenanceAsync, "read the provenance file").ConfigureAwait(true);

        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await GuardedAsync(() => _model.RefreshPreviewsAsync(), "check the entries").ConfigureAwait(true);
        };
    }

    private void OnProvenanceChanged()
    {
        if (_rendering)
            return;

        _model.Title = _title.Text ?? string.Empty;
        _model.SourceTitle = _source.Text ?? string.Empty;
        _model.Edition = _edition.Text ?? string.Empty;
        _model.Amendment = _amendment.Text ?? string.Empty;
        _model.TableOrClause = _table.Text ?? string.Empty;
        _model.LicenceHolder = _holder.Text ?? string.Empty;
        _model.Notes = _notes.Text ?? string.Empty;
        _model.Licensed = _licensed.IsChecked == true;
        _model.AsRevision = _revision.IsChecked == true;

        if (_origin.SelectedItem is CalcDataOrigin origin)
            _model.Origin = origin;

        SchedulePreview();
    }

    private void SchedulePreview()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private async Task OnTypeAsync()
    {
        if (_rendering || _types.SelectedItem is not ListBoxItem { Tag: string kind })
            return;

        if (!string.Equals(kind, _model.SelectedKind, StringComparison.Ordinal))
            await _model.SelectKindAsync(kind).ConfigureAwait(true);
    }

    private async Task OnTemplateAsync()
    {
        if (_rendering || _template.SelectedIndex < 0 || _template.SelectedIndex >= _model.Templates.Count)
            return;

        await _model.SelectTemplateAsync(_model.Templates[_template.SelectedIndex].Id).ConfigureAwait(true);
    }

    private async Task PickFileAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storage is null || !storage.CanOpen)
        {
            _status.Text = "No file picker is available here.";
            return;
        }

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a record CSV",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Record CSV (from a record template)") { Patterns = ["*.csv", "*.txt"] }],
        }).ConfigureAwait(true);

        if (picked.Count == 0)
            return;

        // The picker's stream is read through the bounded reader: the 5 MB cap and the time limit hold whatever the file claims.
        await using var stream = await picked[0].OpenReadAsync().ConfigureAwait(true);
        await _model.LoadFileAsync(picked[0].Name, stream).ConfigureAwait(true);
    }

    private async Task PickProvenanceAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storage is null || !storage.CanOpen)
        {
            _status.Text = "No file picker is available here.";
            return;
        }

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a provenance file (_provenance.csv)",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Provenance CSV") { Patterns = ["*.csv"] }],
        }).ConfigureAwait(true);

        if (picked.Count == 0)
            return;

        await using var stream = await picked[0].OpenReadAsync().ConfigureAwait(true);
        var file = await Tempest.Calc.Import.BoundedInputReader.ReadAsync(stream, picked[0].Name).ConfigureAwait(true);
        _model.LoadProvenanceFile(file.Bytes);
    }

    /// <summary>Runs a handler; a failure is one sentence in the status line that names <paramref name="what"/> was being done (yellow-team Y-C-03).</summary>
    private async Task GuardedAsync(Func<Task> action, string what)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            _status.Text = $"Could not {what}: {failure.Message}";
        }
    }

    // ------------------------------------------------------------------ drawing

    private void Render()
    {
        _rendering = true;

        try
        {
            // The types.
            var items = new List<ListBoxItem>();

            foreach (var type in _model.Types)
            {
                var item = new ListBoxItem
                {
                    Tag = type.Kind,
                    Content = new TextBlock { Text = $"{(type.RequiredBy.Count > 0 && type.Count == 0 ? "! " : string.Empty)}{type.Title} — {type.Count}", TextWrapping = TextWrapping.Wrap },
                };
                AutomationProperties.SetName(item, $"Record type: {type.Title}");
                items.Add(item);
            }

            _types.ItemsSource = items;
            _types.SelectedItem = items.FirstOrDefault(i => (string?)i.Tag == _model.SelectedKind);

            var type1 = _model.SelectedType;
            _heading.Text = type1 is null ? "No record types" : type1.Title;
            _usedBy.Text = type1 is null
                ? string.Empty
                : $"Kind {type1.Kind}. Used by: {string.Join(", ", type1.Calculators)}."
                  + (type1.RequiredBy.Count > 0 ? $" Required by: {string.Join(", ", type1.RequiredBy)}." : string.Empty);

            // The records.
            _records.Children.Clear();

            if (_model.Records.Count == 0)
                _records.Children.Add(new TextBlock { Text = "No record of this type yet.", FontSize = DesignTokens.FontSizeBody });

            foreach (var record in _model.Records)
            {
                var line = new TextBlock
                {
                    Text = $"{record.Title} · rev {record.Revision} · {record.Edition} · {record.TableOrClause} · {record.Origin} · {(record.Licensed ? "LICENSED (never exported)" : "not licensed")} · by {record.EnteredBy}, {record.Entered}  —  holds {record.Holds}",
                    FontSize = DesignTokens.FontSizeBody,
                    TextWrapping = TextWrapping.Wrap,
                };
                AutomationProperties.SetName(line, $"Record: {record.Title} rev {record.Revision}");
                _records.Children.Add(line);
            }

            if (_model.RecordsNotListed > 0)
                _records.Children.Add(new TextBlock { Text = $"… and {_model.RecordsNotListed} more.", FontSize = DesignTokens.FontSizeCaption });

            // The template and the provenance.
            _template.ItemsSource = _model.Templates.Select(t => t.Label).ToList();
            _template.SelectedIndex = _model.Template is { } t ? _model.Templates.ToList().FindIndex(x => x.Id == t.Id) : -1;
            _templateNote.Text = _model.Template is { } tt ? $"{tt.Title}. {tt.Note}" : _model.Templates.Count == 0 ? "No record template fills this kind; its records are entered on a worksheet (Save inputs as record)." : string.Empty;
            Set(_title, _model.Title);
            Set(_source, _model.SourceTitle);
            Set(_edition, _model.Edition);
            Set(_amendment, _model.Amendment);
            Set(_table, _model.TableOrClause);
            Set(_holder, _model.LicenceHolder);
            Set(_notes, _model.Notes);
            _enteredBy.Text = _model.EnteredBy;
            _licensed.IsChecked = _model.Licensed;
            _revision.IsChecked = _model.AsRevision;
            _origin.SelectedItem = _model.Origin;
            _numberFormat.SelectedIndex = _model.DecimalComma ? 1 : 0;

            RenderGrid();
            _status.Text = _model.Status;
        }
        finally
        {
            _rendering = false;
        }

        RenderPreview();
    }

    private static void Set(TextBox box, string text)
    {
        if (!string.Equals(box.Text ?? string.Empty, text, StringComparison.Ordinal))
            box.Text = text;
    }

    private void RenderGrid()
    {
        _grid.Children.Clear();
        _grid.ColumnDefinitions.Clear();
        _grid.RowDefinitions.Clear();
        _gridStatus.Clear();
        var columns = _model.GridColumns;

        for (var c = 0; c < columns.Count; c++)
            _grid.ColumnDefinitions.Add(new ColumnDefinition(columns[c].IsText ? 170 : 120, GridUnitType.Pixel));

        // Header: the symbol, its unit (typed), and what the unit check says.
        _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var c = 0; c < columns.Count; c++)
        {
            var column = columns[c];
            var index = c;
            var head = new TextBlock { Text = column.Symbol, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody, Margin = new Thickness(2, 0) };
            Place(head, 0, c);

            Control unit;

            if (column.IsText || column.Template.Unit.Length == 0)
            {
                // A text column and a plain number take no unit.
                unit = new TextBlock { Text = column.IsText ? "text" : "no unit", FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7, Margin = new Thickness(2, 4) };
            }
            else
            {
                var box = new TextBox { Text = column.Unit, Watermark = "unit", MinHeight = DesignTokens.MinControlSize, Margin = new Thickness(2, 0), FontFamily = DesignTokens.MonoFont };
                AutomationProperties.SetName(box, $"Unit of {column.Symbol}");
                box.TextChanged += (_, _) =>
                {
                    if (_rendering)
                        return;

                    _model.SetUnit(index, box.Text ?? string.Empty);
                    SchedulePreview();
                };
                unit = box;
            }

            Place(unit, 1, c);
            var status = new TextBlock { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0), MaxWidth = columns[c].IsText ? 170 : 120 };
            AutomationProperties.SetName(status, $"Unit check of {column.Symbol}");
            _gridStatus.Add(status);
            Place(status, 2, c);
        }

        for (var r = 0; r < _model.GridRows.Count; r++)
        {
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (var c = 0; c < columns.Count; c++)
            {
                var row = r;
                var col = c;
                var box = new TextBox { Text = _model.GridRows[r][c], MinHeight = DesignTokens.MinControlSize, Margin = new Thickness(2, 1), MaxLength = 200 };
                AutomationProperties.SetName(box, $"Row {r + 1} {columns[c].Symbol}");
                box.TextChanged += (_, _) =>
                {
                    if (_rendering)
                        return;

                    _model.SetCell(row, col, box.Text ?? string.Empty);
                    SchedulePreview();
                };
                Place(box, 3 + r, c);
            }
        }
    }

    private void Place(Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        _grid.Children.Add(control);
    }

    private void RenderPreview()
    {
        _provenanceProblem.Text = _model.ProvenanceProblem is { } problem ? problem : string.Empty;
        _provenanceProblem.IsVisible = _model.ProvenanceProblem is not null;

        for (var c = 0; c < _gridStatus.Count && c < _model.GridColumns.Count; c++)
        {
            var column = _model.GridColumns[c];
            _gridStatus[c].Text = Describe(column.Check, column.ExpectedUnit);
            _gridStatus[c].Foreground = Brush(column.Check);
        }

        _gridSummary.Text = CalcRecordsViewModel.Summary(_model.GridPreview);
        FillIssues(_gridIssues, _model.GridIssueLines);
        _saveGrid.IsEnabled = _model.CanSaveGrid;
        _removeRow.IsEnabled = _model.GridRows.Count > 1;

        _fileLabel.Text = _model.FileName is null ? "No file chosen." : $"{_model.FileName} — SHA-256 {_model.FileHash}";
        _fileSummary.Text = CalcRecordsViewModel.Summary(_model.FilePreview);
        FillIssues(_fileIssues, _model.FileIssueLines);
        RenderMapping();
        _importFile.IsEnabled = _model.CanImportFile;
        _removeFile.IsEnabled = _model.FileName is not null;
        _status.Text = _model.Status;
    }

    private static string Describe(RecordUnitCheck check, string expected) => check switch
    {
        RecordUnitCheck.Match => expected.Length > 0 ? $"unit ok ({expected})" : "unit ok",
        RecordUnitCheck.Convertible => $"kept as typed; calculator takes {expected}",
        RecordUnitCheck.Missing => $"unit missing; calculator takes {expected}",
        RecordUnitCheck.Mismatch => $"WRONG unit; calculator takes {(expected.Length > 0 ? expected : "another kind")}",
        RecordUnitCheck.Unknown => "unit not understood",
        _ => string.Empty,
    };

    private static IBrush Brush(RecordUnitCheck check) => check switch
    {
        RecordUnitCheck.Mismatch or RecordUnitCheck.Missing or RecordUnitCheck.Unknown => Brushes.IndianRed,
        RecordUnitCheck.Convertible => Brushes.DarkOrange,
        _ => Brushes.Gray,
    };

    private static void FillIssues(StackPanel panel, IReadOnlyList<string> lines)
    {
        panel.Children.Clear();
        var shown = lines.Take(60).ToList();

        foreach (var line in shown)
        {
            var text = new TextBlock { Text = line, FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Foreground = line.StartsWith("Error", StringComparison.Ordinal) || line.StartsWith("Refused", StringComparison.Ordinal) ? Brushes.IndianRed : Brushes.DarkOrange };
            AutomationProperties.SetName(text, line);
            panel.Children.Add(text);
        }

        if (lines.Count > shown.Count)
            panel.Children.Add(new TextBlock { Text = $"… and {lines.Count - shown.Count} more.", FontSize = DesignTokens.FontSizeCaption });
    }

    private void RenderMapping()
    {
        _rendering = true;

        try
        {
            _mapping.Children.Clear();

            if (_model.FilePreview is not { Columns.Count: > 0 } preview)
                return;

            var choices = new List<string> { LeaveOut };
            choices.AddRange(_model.Template?.Columns.Select(c => c.Symbol) ?? []);

            foreach (var column in preview.Columns)
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("160,200,*"), Margin = new Thickness(0, 1) };
                row.Children.Add(new TextBlock { Text = column.Header, VerticalAlignment = VerticalAlignment.Center, FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeBody });
                // A column the template does not know shows no choice until one is made; "leave out" is shown only once it was chosen.
                var chosenLeaveOut = _model.ColumnMap.ContainsKey(column.Header) || _model.ColumnMap.ContainsKey(column.Symbol);
                var pick = new ComboBox { ItemsSource = choices, MinHeight = DesignTokens.MinControlSize, MinWidth = 180, PlaceholderText = "Map to a template column", SelectedItem = column.TemplateSymbol ?? (chosenLeaveOut ? LeaveOut : null) };
                AutomationProperties.SetName(pick, $"Map column {column.Symbol}");
                var header = column.Header;
                pick.SelectionChanged += async (_, _) =>
                {
                    if (_rendering || pick.SelectedItem is not string chosen)
                        return;

                    await GuardedAsync(() => _model.SetColumnMapAsync(header, chosen == LeaveOut ? null : chosen), "map that column").ConfigureAwait(true);
                };
                Grid.SetColumn(pick, 1);
                row.Children.Add(pick);
                var status = new TextBlock
                {
                    Text = column.Role == RecordColumnRole.Ignored
                        ? "left out"
                        : $"→ {column.Target}{(column.UnitMessage.Length > 0 || column.Unit != RecordUnitCheck.NotApplicable ? " · " + Describe(column.Unit, column.ExpectedUnit) : string.Empty)}",
                    FontSize = DesignTokens.FontSizeCaption,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brush(column.Unit),
                };
                AutomationProperties.SetName(status, $"Column check {column.Symbol}");
                Grid.SetColumn(status, 2);
                row.Children.Add(status);
                _mapping.Children.Add(row);
            }
        }
        finally
        {
            _rendering = false;
        }
    }
}
