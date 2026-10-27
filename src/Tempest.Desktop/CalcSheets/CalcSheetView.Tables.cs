using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Shot-fix A, item 12: table and vector inputs on the worksheet form. Before, they arrived only by Import inputs, and a new sheet of a template with
/// a table opened blocked ("enter at least one row of ...") with nowhere to enter one. Each table now has an editor: its rows with their columns (from
/// <see cref="CalcColumnSpec"/>), Add row up to <see cref="CalcInputSpec.MaxRows"/>, Delete per row, a unit picker per quantity column, a picker per
/// choice column, a tick per yes/no column, and one source for the table. A vector is one line of numbers with its unit and source. The run reads them
/// with the rest of the form; validation names the row and column as for an import. A table or vector the user has not touched is read back exactly
/// as it was given (its rows, units and per-cell sources and links), so an imported or saved table is never rewritten by being shown. Read-only on an
/// issued form.
/// </summary>
public sealed partial class CalcSheetView
{
    /// <summary>The caption of a table's Add row button.</summary>
    public const string AddRowCaption = "Add row";

    /// <summary>The caption of a table row's Delete button.</summary>
    public const string DeleteRowCaption = "Delete";

    /// <summary>The automation name of a table input's editor.</summary>
    public static string TableEditorName(string symbol) => $"{symbol} table";

    /// <summary>The most characters a vector's line of numbers may hold on the form.</summary>
    public const int MaxVectorTextLength = 200_000;

    private sealed class CellEditor(CalcColumnSpec column, CalcCell? seed)
    {
        public CalcColumnSpec Column { get; } = column;

        public CalcCell? Seed { get; } = seed;

        public TextBox? Text { get; set; }

        public ComboBox? Choice { get; set; }

        public CheckBox? Flag { get; set; }

        public string InitialText { get; set; } = string.Empty;

        public string? InitialChoice { get; set; }

        public bool InitialFlag { get; set; }

        public bool IsEdited => Text is not null ? !string.Equals(Text.Text ?? string.Empty, InitialText, StringComparison.Ordinal)
            : Choice is not null ? !string.Equals(Choice.SelectedItem as string, InitialChoice, StringComparison.Ordinal)
            : Flag is not null && (Flag.IsChecked == true) != InitialFlag;
    }

    private sealed class RowEditor(CalcRow? seed)
    {
        public CalcRow? Seed { get; } = seed;

        public List<CellEditor> Cells { get; } = [];

        public Grid Row { get; } = new();

        public Button Delete { get; } = new() { Content = DeleteRowCaption, MinHeight = DesignTokens.MinControlSize };
    }

    private sealed class TableEditor(CalcInputSpec spec, CalcInputValue? seed)
    {
        public CalcInputSpec Spec { get; } = spec;

        public CalcInputValue? Seed { get; } = seed;

        /// <summary>Whether a row was added or deleted or a column's unit changed since the table was shown (a typed cell is seen by <see cref="IsChanged"/>).</summary>
        public bool Dirty { get; set; }

        public string InitialSource { get; set; } = string.Empty;

        public string InitialReference { get; set; } = string.Empty;

        /// <summary>
        /// Whether anything in the table or vector differs from how it was shown: until then it reads back as its seed, untouched. Read from the
        /// controls themselves, so a value typed is seen whether or not its change event has been raised yet.
        /// </summary>
        public bool IsChanged => Dirty
            || Rows.Any(r => r.Cells.Any(c => c.IsEdited))
            || Vector is not null && !string.Equals(Vector.Text ?? string.Empty, VectorInitial, StringComparison.Ordinal)
            || !string.Equals(Provenance.SelectedItem as string ?? string.Empty, InitialSource, StringComparison.Ordinal)
            || !string.Equals(ProvenanceRef.Text ?? string.Empty, InitialReference, StringComparison.Ordinal);

        public List<RowEditor> Rows { get; } = [];

        /// <summary>Given rows beyond the table's cap: not shown, but kept, so validation keeps blocking until they are removed on purpose.</summary>
        public List<CalcRow> Overflow { get; } = [];

        public TextBlock OverflowNote { get; } = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, IsVisible = false };

        public Button RemoveOverflow { get; } = new() { MinHeight = DesignTokens.MinControlSize, IsVisible = false };

        public StackPanel RowHost { get; } = new() { Spacing = 2 };

        public Dictionary<string, ComboBox> ColumnUnits { get; } = new(StringComparer.Ordinal);

        public string ColumnWidths { get; set; } = string.Empty;

        public Button Add { get; } = new() { Content = AddRowCaption, MinHeight = DesignTokens.MinControlSize };

        public TextBlock Count { get; } = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center };

        public TextBox? Vector { get; set; }

        public ScrollViewer? Scroller { get; set; }

        public string VectorInitial { get; set; } = string.Empty;

        public ComboBox? VectorUnit { get; set; }

        public ComboBox Provenance { get; } = new() { MinHeight = DesignTokens.MinControlSize, MinWidth = 120 };

        public TextBox ProvenanceRef { get; } = Input(string.Empty, "reference / revision");

        public TextBlock Problem { get; } = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, IsVisible = false, Foreground = Brushes.DarkOrange };
    }

    private readonly Dictionary<string, TableEditor> _tables = new(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="spec"/> gets an editor on the form: a vector, or a table with columns.</summary>
    private static bool HasEditor(CalcInputSpec spec) => spec.Kind == CalcInputKind.Vector || spec.Kind == CalcInputKind.Table && spec.Columns is { Count: > 0 };

    /// <summary>The number of rows a table input shows, or -1 when it has no editor.</summary>
    public int TableRowCount(string symbol) => _tables.TryGetValue(symbol, out var t) && t.Spec.Kind == CalcInputKind.Table ? t.Rows.Count : -1;

    /// <summary>Whether a table input's Add row is offered (an editable form, under the row cap).</summary>
    public bool CanAddTableRow(string symbol) => _tables.TryGetValue(symbol, out var t) && t.Add.IsEnabled;

    /// <summary>Adds an empty row to a table input, as Add row does.</summary>
    public void AddTableRow(string symbol)
    {
        if (_tables.TryGetValue(symbol, out var t) && t.Add.IsEnabled)
            AddRow(t, null, raise: true);
    }

    /// <summary>Deletes a row (0-based) of a table input, as its Delete does.</summary>
    public void DeleteTableRow(string symbol, int row)
    {
        if (_tables.TryGetValue(symbol, out var t) && row >= 0 && row < t.Rows.Count && t.Rows[row].Delete.IsEnabled)
            DeleteRow(t, t.Rows[row]);
    }

    /// <summary>Sets one cell of a table input (row 0-based, column by key) as typing, picking or ticking would; a quantity cell can take its column's unit too.</summary>
    public void SetTableCell(string symbol, int row, string column, string? text = null, string? choice = null, bool? flag = null)
    {
        if (!_tables.TryGetValue(symbol, out var t) || row < 0 || row >= t.Rows.Count || IsReadOnly)
            return;

        var cell = t.Rows[row].Cells.FirstOrDefault(c => c.Column.Key == column);
        _loadingForm = true;

        if (cell?.Text is not null && text is not null)
            cell.Text.Text = text;

        if (cell?.Choice is not null && choice is not null)
            cell.Choice.SelectedItem = choice;

        if (cell?.Flag is not null && flag is not null)
            cell.Flag.IsChecked = flag;

        _loadingForm = false;
        TableChanged(t);
    }

    /// <summary>Picks the unit of a quantity column of a table input; the column's numbers are converted, as for a scalar.</summary>
    public void SetTableColumnUnit(string symbol, string column, string unit)
    {
        if (_tables.TryGetValue(symbol, out var t) && t.ColumnUnits.TryGetValue(column, out var picker) && !IsReadOnly)
            picker.SelectedItem = unit;
    }

    /// <summary>The text a table cell shows (row 0-based, column by key), or <see langword="null"/>.</summary>
    public string? TableCellText(string symbol, int row, string column)
    {
        if (!_tables.TryGetValue(symbol, out var t) || row < 0 || row >= t.Rows.Count)
            return null;

        var cell = t.Rows[row].Cells.FirstOrDefault(c => c.Column.Key == column);
        return cell?.Text?.Text ?? cell?.Choice?.SelectedItem as string ?? (cell?.Flag is { } f ? (f.IsChecked == true ? "yes" : "no") : null);
    }

    /// <summary>Types a vector input's line of numbers (comma or space separated), optionally with its unit.</summary>
    public void SetVector(string symbol, string text, string? unit = null)
    {
        if (!_tables.TryGetValue(symbol, out var t) || t.Vector is null || IsReadOnly)
            return;

        _loadingForm = true;

        if (unit is not null && t.VectorUnit is not null)
        {
            t.VectorUnit.SelectedItem = unit;
            t.VectorUnit.Tag = unit;
        }

        t.Vector.Text = text;
        _loadingForm = false;
        TableChanged(t);
    }

    /// <summary>Whether a table or vector input's editor can be changed (false on a read-only form).</summary>
    public bool IsTableEditable(string symbol) => _tables.TryGetValue(symbol, out var t) && t.Provenance.IsEnabled;

    /// <summary>The problem a table or vector input's editor shows, or <see langword="null"/>.</summary>
    public string? TableProblem(string symbol) => _tables.TryGetValue(symbol, out var t) && t.Problem.IsVisible ? t.Problem.Text : null;

    private Control BuildTableEditor(CalcInputSpec spec, CalcInputValue? seed)
    {
        var t = new TableEditor(spec, seed);
        var block = new StackPanel { Spacing = DesignTokens.SpaceXs, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, DesignTokens.SpaceSm) };
        AutomationProperties.SetName(block, TableEditorName(spec.Symbol));
        var title = Typeset(new TextBlock { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap }, $"{spec.Symbol} — {spec.Description}{(spec.IsOptional ? " (optional)" : string.Empty)}");
        ToolTip.SetTip(title, $"{spec.Symbol}: {spec.Description}");
        block.Children.Add(title);

        if (spec.Kind == CalcInputKind.Vector)
            BuildVector(t, block);
        else
            BuildTable(t, block);

        // One source for the whole table or vector, as for a scalar input.
        t.Provenance.ItemsSource = ProvenanceKinds;
        t.Provenance.SelectedItem = ProvenanceLabel(seed?.Provenance?.Kind ?? CalcProvenanceKind.Manual);
        t.ProvenanceRef.Text = seed?.Provenance?.Reference ?? string.Empty;
        t.InitialSource = t.Provenance.SelectedItem as string ?? string.Empty;
        t.InitialReference = t.ProvenanceRef.Text;
        t.ProvenanceRef.MinWidth = 220;
        AutomationProperties.SetName(t.Provenance, $"{spec.Symbol} source");
        AutomationProperties.SetName(t.ProvenanceRef, $"{spec.Symbol} source reference");
        ToolTip.SetTip(t.Provenance, "Where these values come from. A source other than 'manual' is needed to issue.");
        t.Provenance.SelectionChanged += (_, _) => TableChanged(t);
        t.ProvenanceRef.TextChanged += (_, _) => TableChanged(t);
        var source = new WrapPanel { Orientation = Orientation.Horizontal };

        if (spec.Kind == CalcInputKind.Table)
        {
            t.Add.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, 0);
            AutomationProperties.SetName(t.Add, $"{AddRowCaption} to {spec.Symbol}");
            t.Add.Click += (_, _) => AddRow(t, null, raise: true);
            source.Children.Add(t.Add);
            t.Count.Margin = new Thickness(0, 0, DesignTokens.SpaceLg, 0);
            source.Children.Add(t.Count);
        }

        source.Children.Add(new TextBlock { Text = "Source", VerticalAlignment = VerticalAlignment.Center, FontSize = DesignTokens.FontSizeBody, Margin = new Thickness(0, 0, DesignTokens.SpaceSm, 0) });
        t.Provenance.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, 0);
        source.Children.Add(t.Provenance);
        source.Children.Add(t.ProvenanceRef);
        block.Children.Add(source);
        AutomationProperties.SetName(t.Problem, $"{spec.Symbol} problem");
        block.Children.Add(t.Problem);

        _tables[spec.Symbol] = t;
        SetTableEnabled(t, !IsReadOnly);
        return block;
    }

    private void BuildVector(TableEditor t, StackPanel block)
    {
        var spec = t.Spec;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,90") };
        var unit = t.Seed?.Unit is { Length: > 0 } u ? u : spec.DefaultUnit;
        t.VectorInitial = t.Seed?.Vector is { } values ? string.Join(", ", values.Select(x => x.ToString("R", CultureInfo.InvariantCulture))) : string.Empty;
        t.Vector = Input(t.VectorInitial, "numbers, separated by spaces, commas and a space, or semicolons");
        t.Vector.MaxLength = MaxVectorTextLength;
        t.Vector.TextWrapping = TextWrapping.Wrap;
        AutomationProperties.SetName(t.Vector, spec.Symbol);
        t.Vector.TextChanged += (_, _) => TableChanged(t);
        row.Children.Add(t.Vector);

        if (spec.DefaultUnit is { Length: > 0 } && spec.DefaultUnit != "1")
        {
            var choices = UnitChoices(new CalcInputSpec(spec.Symbol, spec.Description, CalcInputKind.Quantity, spec.DefaultUnit), unit).ToList();

            if (!choices.Contains(unit))
                choices.Add(unit);

            t.VectorUnit = new ComboBox { MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = choices, SelectedItem = unit, Tag = unit, Margin = new Thickness(DesignTokens.SpaceSm, 0, 0, 0) };
            AutomationProperties.SetName(t.VectorUnit, $"{spec.Symbol} unit");
            t.VectorUnit.SelectionChanged += (_, _) => ConvertVector(t);
            Grid.SetColumn(t.VectorUnit, 1);
            row.Children.Add(t.VectorUnit);
        }

        block.Children.Add(row);
    }

    private void BuildTable(TableEditor t, StackPanel block)
    {
        var columns = t.Spec.Columns!;
        // Delete comes first, so it stays in view when a wide table scrolls sideways.
        t.ColumnWidths = string.Join(",", columns.Select(ColumnWidth).Prepend(80));
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions(t.ColumnWidths) };
        var units = new Grid { ColumnDefinitions = new ColumnDefinitions(t.ColumnWidths) };
        var anyUnit = false;

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            // At most two lines, set on the bottom of the header row, so a long label neither stands the header far above the rows nor hides
            // the others (the re-shoot found bs.glaser's five-line Element header); the whole label is in the tooltip.
            var label = new TextBlock { Text = column.Label, FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0), Opacity = column.IsOptional ? 0.75 : 1 };
            ToolTip.SetTip(label, $"{column.Key}: {column.Label}{(column.IsOptional ? " (optional)" : string.Empty)}{RangeText(column)}");
            Grid.SetColumn(label, i + 1);
            head.Children.Add(label);

            if (column.Kind != CalcInputKind.Quantity)
                continue;

            // The column's unit: the unit its first given cell is in, else the template's.
            var seedUnit = t.Seed?.Rows?.Select(r => CellOf(r, column)?.Unit).FirstOrDefault(x => x is { Length: > 0 }) ?? column.DefaultUnit;
            var choices = UnitChoices(new CalcInputSpec(column.Key, column.Label, CalcInputKind.Quantity, column.DefaultUnit), seedUnit).ToList();

            if (!choices.Contains(seedUnit))
                choices.Add(seedUnit);

            var picker = new ComboBox { MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = choices, SelectedItem = seedUnit, Tag = seedUnit, Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0) };
            AutomationProperties.SetName(picker, $"{t.Spec.Symbol} {column.Key} unit");
            var key = column.Key;
            picker.SelectionChanged += (_, _) => ConvertColumn(t, key);
            Grid.SetColumn(picker, i + 1);
            units.Children.Add(picker);
            t.ColumnUnits[column.Key] = picker;
            anyUnit = true;
        }

        var grid = new StackPanel { Spacing = 2 };
        grid.Children.Add(head);

        if (anyUnit)
            grid.Children.Add(units);

        grid.Children.Add(t.RowHost);
        // A table wider than the panel scrolls sideways, and its scroll bar is drawn at rest, so the columns past the edge are seen to be there
        // (the re-shoot found bs.duct-sizing cut at "Fixed side a" with nothing to say more columns followed).
        var scroller = new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, AllowAutoHide = false };
        AutomationProperties.SetName(scroller, $"{t.Spec.Symbol} columns");
        t.Scroller = scroller;
        block.Children.Add(scroller);

        foreach (var row in (t.Seed?.Rows ?? []).Take(t.Spec.MaxRows))
            AddRow(t, row, raise: false);

        // Rows past the cap are kept, never dropped silently (review item 6): the run refuses the table until the user removes them.
        t.Overflow.AddRange((t.Seed?.Rows ?? []).Skip(t.Spec.MaxRows));

        if (t.Overflow.Count > 0)
        {
            var given = t.Spec.MaxRows + t.Overflow.Count;
            t.OverflowNote.Text = $"{given} rows were given; this table takes at most {t.Spec.MaxRows}. The form shows rows 1 to {t.Spec.MaxRows}; rows {t.Spec.MaxRows + 1} to {given} are kept but not shown, and the calculation does not run until they are removed.";
            t.OverflowNote.IsVisible = true;
            t.RemoveOverflow.Content = $"Remove rows {t.Spec.MaxRows + 1} to {given}";
            t.RemoveOverflow.IsVisible = true;
            AutomationProperties.SetName(t.RemoveOverflow, $"Remove the rows of {t.Spec.Symbol} beyond {t.Spec.MaxRows}");
            t.RemoveOverflow.Click += (_, _) => RemoveOverflowRows(t);
            block.Children.Add(t.OverflowNote);
            block.Children.Add(t.RemoveOverflow);
        }

        UpdateRowCount(t);
    }

    /// <summary>A cell's tooltip: where it is, then its whole text (a long text is cut in the cell; the re-shoot found it with no way to read it).</summary>
    private static string CellTip(string head, string? text) => string.IsNullOrEmpty(text) ? head : $"{head}\n{text}";

    /// <summary>Whether a table input's sideways scroll bar is drawn at rest.</summary>
    public bool IsTableScrollBarAlwaysShown(string symbol) => _tables.TryGetValue(symbol, out var t) && t.Scroller is { AllowAutoHide: false, HorizontalScrollBarVisibility: not ScrollBarVisibility.Disabled and not ScrollBarVisibility.Hidden };

    /// <summary>The tooltip of a table cell (row 0-based, column by key), or <see langword="null"/>.</summary>
    public string? TableCellTip(string symbol, int row, string column) =>
        _tables.TryGetValue(symbol, out var t) && row >= 0 && row < t.Rows.Count && t.Rows[row].Row.Children.OfType<Control>().FirstOrDefault(c => AutomationProperties.GetName(c) == $"{symbol} row {row + 1} {column}") is { } cell
            ? ToolTip.GetTip(cell) as string
            : null;

    /// <summary>The placeholder a table cell shows when empty (row 0-based, column by key), or <see langword="null"/>.</summary>
    public string? TableCellPlaceholder(string symbol, int row, string column) =>
        _tables.TryGetValue(symbol, out var t) && row >= 0 && row < t.Rows.Count ? t.Rows[row].Cells.FirstOrDefault(c => c.Column.Key == column)?.Text?.Watermark : null;

    private static string RangeText(CalcColumnSpec c)
    {
        var unit = c.DefaultUnit is { Length: > 0 } u ? " " + CalcUnitText.Pretty(u) : string.Empty;
        var low = c.HardMin is { } lo ? CalcValue.FormatNumber(lo, 6) : null;
        var high = c.HardMax is { } hi ? CalcValue.FormatNumber(hi, 6) : null;
        return low is null && high is null ? string.Empty : $"\nAllowed: {low ?? "…"} to {high ?? "…"}{unit}";
    }

    private static int ColumnWidth(CalcColumnSpec c) => c.Kind switch
    {
        CalcInputKind.Boolean => 70,
        CalcInputKind.Choice => 170,
        CalcInputKind.Text => 150,
        _ => 110,
    };

    private static CalcCell? CellOf(CalcRow? row, CalcColumnSpec column) =>
        row is null ? null : row.Get(column.Key) ?? (column.Aliases is { Count: > 0 } aliases ? aliases.Select(row.Get).FirstOrDefault(c => c is not null) : null);

    private void AddRow(TableEditor t, CalcRow? seed, bool raise)
    {
        if (t.Rows.Count >= t.Spec.MaxRows)
            return;

        var loading = _loadingForm;
        _loadingForm = true;
        var editor = new RowEditor(seed);
        editor.Row.ColumnDefinitions = new ColumnDefinitions(t.ColumnWidths);
        var columns = t.Spec.Columns!;
        var index = t.Rows.Count + 1;

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var seedCell = CellOf(seed, column);
            var cell = new CellEditor(column, seedCell);
            Control control;

            switch (column.Kind)
            {
                case CalcInputKind.Choice:
                    cell.InitialChoice = seedCell?.Text;
                    cell.Choice = new ComboBox { MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = column.Choices ?? [], SelectedItem = seedCell?.Text, ItemTemplate = WrappingChoice, PlaceholderText = column.IsOptional ? "(none)" : "choose" };
                    cell.Choice.SelectionChanged += (_, _) => TableChanged(t);
                    control = cell.Choice;
                    break;

                case CalcInputKind.Boolean:
                    cell.InitialFlag = seedCell?.Flag == true;
                    cell.Flag = new CheckBox { IsChecked = cell.InitialFlag, MinHeight = DesignTokens.MinControlSize };
                    cell.Flag.IsCheckedChanged += (_, _) => TableChanged(t);
                    control = cell.Flag;
                    break;

                default:
                    // No placeholder: an empty optional column read "text" in every row (bs.glaser); the column's header names it.
                    cell.InitialText = CellText(t, column, seedCell);
                    cell.Text = Input(cell.InitialText);
                    cell.Text.MaxLength = 400;
                    var box = cell.Text;
                    var tipHead = $"{t.Spec.Symbol}, row {index}: {column.Label}";
                    box.TextChanged += (_, _) => TableChanged(t);

                    // The tooltip follows the text as it is set (TextChanged is raised later, after the edit settles).
                    box.PropertyChanged += (_, e) =>
                    {
                        if (e.Property == TextBox.TextProperty)
                            ToolTip.SetTip(box, CellTip(tipHead, box.Text));
                    };
                    control = cell.Text;
                    break;
            }

            control.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
            AutomationProperties.SetName(control, $"{t.Spec.Symbol} row {index} {column.Key}");
            ToolTip.SetTip(control, CellTip($"{t.Spec.Symbol}, row {index}: {column.Label}", cell.Text?.Text ?? cell.Choice?.SelectedItem as string));
            Grid.SetColumn(control, i + 1);
            editor.Row.Children.Add(control);
            editor.Cells.Add(cell);
        }

        AutomationProperties.SetName(editor.Delete, $"{DeleteRowCaption} row {index} of {t.Spec.Symbol}");
        editor.Delete.Click += (_, _) => DeleteRow(t, editor);
        editor.Delete.Margin = new Thickness(0, 0, DesignTokens.SpaceXs, 0);
        editor.Row.Children.Add(editor.Delete);
        t.Rows.Add(editor);
        t.RowHost.Children.Add(editor.Row);
        SetRowEnabled(editor, !IsReadOnly);
        _loadingForm = loading;
        UpdateRowCount(t);

        if (raise)
        {
            t.Dirty = true;
            RaiseInputsChanged();
        }
    }

    /// <summary>A seeded cell's text: its number in the column's unit (converted when the cell was given in another unit of the same kind), or its text.</summary>
    private static string CellText(TableEditor t, CalcColumnSpec column, CalcCell? cell)
    {
        if (cell is null)
            return string.Empty;

        if (cell.Number is not { } n)
            return cell.Text ?? string.Empty;

        if (column.Kind == CalcInputKind.Quantity && t.ColumnUnits.TryGetValue(column.Key, out var picker) && picker.SelectedItem is string shown && cell.Unit is { Length: > 0 } given && given != shown
            && Tempest.Calc.Engine.Units.UnitCatalog.TryParse(given, out var from, out _) && Tempest.Calc.Engine.Units.UnitCatalog.TryParse(shown, out var to, out _) && from.Dimension == to.Dimension)
        {
            return CalcValue.FormatNumber(to.FromSi(from.ToSi(n)), 10);
        }

        return n.ToString("R", CultureInfo.InvariantCulture);
    }

    private void DeleteRow(TableEditor t, RowEditor row)
    {
        if (IsReadOnly || !t.Rows.Remove(row))
            return;

        t.RowHost.Children.Remove(row.Row);

        for (var i = 0; i < t.Rows.Count; i++)
            AutomationProperties.SetName(t.Rows[i].Delete, $"{DeleteRowCaption} row {i + 1} of {t.Spec.Symbol}");

        UpdateRowCount(t);
        t.Dirty = true;
        RaiseInputsChanged();
    }

    /// <summary>The number of given rows of a table input kept beyond its cap (not shown on the form).</summary>
    public int TableOverflowCount(string symbol) => _tables.TryGetValue(symbol, out var t) ? t.Overflow.Count : 0;

    /// <summary>Removes a table input's rows beyond its cap, as its Remove button does.</summary>
    public void RemoveTableOverflow(string symbol)
    {
        if (_tables.TryGetValue(symbol, out var t))
            RemoveOverflowRows(t);
    }

    private void RemoveOverflowRows(TableEditor t)
    {
        if (IsReadOnly || t.Overflow.Count == 0)
            return;

        t.Overflow.Clear();
        t.OverflowNote.IsVisible = false;
        t.RemoveOverflow.IsVisible = false;
        t.Dirty = true;
        RaiseInputsChanged();
    }

    private void UpdateRowCount(TableEditor t)
    {
        t.Count.Text = string.Create(CultureInfo.InvariantCulture, $"{t.Rows.Count} of at most {t.Spec.MaxRows} rows");
        t.Add.IsEnabled = !IsReadOnly && t.Rows.Count < t.Spec.MaxRows;
    }

    private void TableChanged(TableEditor t)
    {
        if (_loadingForm)
            return;

        RaiseInputsChanged();
    }

    /// <summary>A column's unit changed: its numbers are converted so the quantities are unchanged.</summary>
    private void ConvertColumn(TableEditor t, string key)
    {
        if (_loadingForm || !t.ColumnUnits.TryGetValue(key, out var picker))
            return;

        var previous = picker.Tag as string;
        var next = picker.SelectedItem as string;

        if (previous is not null && next is not null && previous != next
            && Tempest.Calc.Engine.Units.UnitCatalog.TryParse(previous, out var from, out _) && Tempest.Calc.Engine.Units.UnitCatalog.TryParse(next, out var to, out _) && from.Dimension == to.Dimension)
        {
            var cells = t.Rows.SelectMany((r, i) => r.Cells.Where(c => c.Column.Key == key && c.Text is not null).Select(c => (Row: i + 1, Cell: c))).ToList();

            // An entry that is not a number cannot be converted: the unit change is refused, or the entry would later be read in the new unit.
            if (cells.FirstOrDefault(c => c.Cell.Text!.Text is { Length: > 0 } text && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) is { Cell: not null } bad)
            {
                RefuseUnitChange(t, picker, previous, $"{t.Spec.Symbol}: correct row {bad.Row}, {bad.Cell.Column.Label} ('{bad.Cell.Text!.Text}'), which is not a number, before changing the unit of the column.");
                return;
            }

            _loadingForm = true;

            foreach (var (_, cell) in cells)
            {
                if (!double.TryParse(cell.Text!.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    continue;

                // A cell the user had not changed is still its given value, shown in the new unit: it keeps its own number, unit and source
                // (a link or a scan import), so converting a column never strips them (review item 3).
                var untouched = !cell.IsEdited;
                cell.Text.Text = CalcValue.FormatNumber(to.FromSi(from.ToSi(value)), 10);

                if (untouched)
                    cell.InitialText = cell.Text.Text;
            }

            _loadingForm = false;
        }

        picker.Tag = next;
        TableChanged(t);
    }

    /// <summary>Puts a unit picker back to the unit it had and says why the change was refused.</summary>
    private void RefuseUnitChange(TableEditor t, ComboBox picker, string previous, string reason)
    {
        _loadingForm = true;
        picker.SelectedItem = previous;
        _loadingForm = false;
        t.Problem.Text = reason;
        t.Problem.Foreground = Brushes.IndianRed;
        t.Problem.IsVisible = true;
    }

    private void ConvertVector(TableEditor t)
    {
        if (_loadingForm || t.VectorUnit is null || t.Vector is null)
            return;

        var previous = t.VectorUnit.Tag as string;
        var next = t.VectorUnit.SelectedItem as string;

        if (previous is not null && next is not null && previous != next
            && Tempest.Calc.Engine.Units.UnitCatalog.TryParse(previous, out var from, out _) && Tempest.Calc.Engine.Units.UnitCatalog.TryParse(next, out var to, out _) && from.Dimension == to.Dimension
            && ParseVector(t.Vector.Text) is { } values)
        {
            // An entry that is not a number cannot be converted: the unit change is refused, or the good entries would change unit around it (review item 4).
            if (values.FindIndex(x => !double.IsFinite(x)) is >= 0 and var bad)
            {
                RefuseUnitChange(t, t.VectorUnit, previous, $"{t.Spec.Symbol}: correct value {bad + 1}, which is not a number, before changing the unit.");
                return;
            }

            var untouched = string.Equals(t.Vector.Text ?? string.Empty, t.VectorInitial, StringComparison.Ordinal);
            _loadingForm = true;
            t.Vector.Text = string.Join(", ", values.Select(x => CalcValue.FormatNumber(to.FromSi(from.ToSi(x)), 10)));
            _loadingForm = false;

            // The given values, only shown in another unit, are still the given values.
            if (untouched)
                t.VectorInitial = t.Vector.Text;
        }

        t.VectorUnit.Tag = next;
        TableChanged(t);
    }

    /// <summary>
    /// A vector's numbers: separated by semicolons or white space, a comma allowed only as the end of an entry ("1, 2, 3"). A comma inside an entry
    /// ("1,5", a decimal comma) makes it not a number, as it does in a single-value field, so it is refused rather than read as two values.
    /// </summary>
    internal static List<double>? ParseVector(string? text)
    {
        var parts = (text ?? string.Empty).Split([';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.EndsWith(',') ? p[..^1] : p)
            .Where(p => p.Length > 0)
            .ToList();
        return parts.Count == 0 ? null : [.. parts.Select(p => !p.Contains(',', StringComparison.Ordinal) && double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : double.NaN)];
    }

    private void SetTableEnabled(TableEditor t, bool enabled)
    {
        foreach (var row in t.Rows)
            SetRowEnabled(row, enabled);

        foreach (var picker in t.ColumnUnits.Values)
            picker.IsEnabled = enabled;

        if (t.Vector is not null)
            t.Vector.IsReadOnly = !enabled;

        if (t.VectorUnit is not null)
            t.VectorUnit.IsEnabled = enabled;

        t.Provenance.IsEnabled = enabled;
        t.ProvenanceRef.IsReadOnly = !enabled;
        t.RemoveOverflow.IsEnabled = enabled;
        t.Add.IsEnabled = enabled && t.Rows.Count < t.Spec.MaxRows;
    }

    private static void SetRowEnabled(RowEditor row, bool enabled)
    {
        foreach (var cell in row.Cells)
        {
            if (cell.Text is not null)
                cell.Text.IsReadOnly = !enabled;

            if (cell.Choice is not null)
                cell.Choice.IsEnabled = enabled;

            if (cell.Flag is not null)
                cell.Flag.IsEnabled = enabled;
        }

        row.Delete.IsEnabled = enabled;
    }

    /// <summary>
    /// The value a table or vector editor holds: its seed, exactly, until something in it is changed; then the rows as shown (a row with no cell
    /// changed is kept as it was given, cell sources and links included) with the table's source. An empty table or vector is not given.
    /// </summary>
    private CalcInputValue? ReadTable(TableEditor t)
    {
        if (!t.IsChanged)
            return t.Seed;

        var provenance = ReadTableProvenance(t);

        if (t.Spec.Kind == CalcInputKind.Vector)
        {
            var values = ParseVector(t.Vector?.Text);

            if (values is null)
                return null;

            if (t.Seed is { Vector: not null } seed && string.Equals(t.Vector?.Text ?? string.Empty, t.VectorInitial, StringComparison.Ordinal) && string.Equals(t.VectorUnit?.SelectedItem as string ?? seed.Unit, seed.Unit, StringComparison.Ordinal))
                return seed with { Provenance = provenance };

            return new CalcInputValue(t.Spec.Symbol, Unit: t.VectorUnit?.SelectedItem as string ?? (t.Spec.DefaultUnit is { Length: > 0 } u ? u : null), Vector: values, Provenance: provenance);
        }

        if (t.Rows.Count == 0 && t.Overflow.Count == 0)
            return null;

        return new CalcInputValue(t.Spec.Symbol, Rows: [.. t.Rows.Select(r => ReadRow(t, r)), .. t.Overflow], Provenance: provenance);
    }

    private static CalcRow ReadRow(TableEditor t, RowEditor row)
    {
        if (row.Seed is { } seed && !row.Cells.Any(c => c.IsEdited))
            return seed;

        var cells = row.Seed is { } given ? new Dictionary<string, CalcCell>(given.Cells, StringComparer.Ordinal) : new Dictionary<string, CalcCell>(StringComparer.Ordinal);

        foreach (var cell in row.Cells)
        {
            if (row.Seed is not null && !cell.IsEdited)
                continue;

            // The cell is written under its column's key; one given under an older key of the column is replaced.
            foreach (var alias in cell.Column.Aliases ?? [])
                cells.Remove(alias);

            var value = ReadCell(t, cell);

            if (value is null)
                cells.Remove(cell.Column.Key);
            else
                cells[cell.Column.Key] = value;
        }

        return new CalcRow(cells);
    }

    private static CalcCell? ReadCell(TableEditor t, CellEditor cell)
    {
        switch (cell.Column.Kind)
        {
            case CalcInputKind.Choice:
                return cell.Choice?.SelectedItem is string choice ? new CalcCell(Text: choice) : null;

            case CalcInputKind.Boolean:
                return new CalcCell(Flag: cell.Flag?.IsChecked == true);

            case CalcInputKind.Text:
                return cell.Text?.Text is { Length: > 0 } typed ? new CalcCell(Text: typed) : null;

            default:
            {
                var text = cell.Text?.Text?.Trim() ?? string.Empty;

                if (text.Length == 0)
                    return null;

                var number = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : double.NaN;
                var unit = cell.Column.Kind == CalcInputKind.Quantity && t.ColumnUnits.TryGetValue(cell.Column.Key, out var picker) ? picker.SelectedItem as string : null;
                return new CalcCell(number, unit);
            }
        }
    }

    private static CalcProvenance ReadTableProvenance(TableEditor t)
    {
        var kind = (t.Provenance.SelectedItem as string) switch
        {
            "drawing" => CalcProvenanceKind.Drawing,
            "spec" => CalcProvenanceKind.Specification,
            "assumption" => CalcProvenanceKind.Assumption,
            "evidence" => CalcProvenanceKind.Evidence,
            "reference" => CalcProvenanceKind.Reference,
            "link" => CalcProvenanceKind.Link,
            _ => CalcProvenanceKind.Manual,
        };

        var reference = t.ProvenanceRef.Text?.Trim();

        // The seed's own source (a pinned link, a revision) is kept while the picker and reference still say what it said.
        if (t.Seed?.Provenance is { } seed && seed.Kind == kind && string.Equals(seed.Reference ?? string.Empty, reference ?? string.Empty, StringComparison.Ordinal))
            return seed;

        return new CalcProvenance(kind, string.IsNullOrEmpty(reference) ? null : reference);
    }

    /// <summary>Shows a run's problem on a table or vector input's editor (every problem of the input, one per line).</summary>
    private bool ShowTableProblem(string key, CalcNotice notice)
    {
        if (!_tables.TryGetValue(key, out var t))
            return false;

        t.Problem.Text = t.Problem.IsVisible && t.Problem.Text is { Length: > 0 } earlier ? earlier + Environment.NewLine + notice.Message : notice.Message;
        t.Problem.Foreground = notice.Severity == CalcNoticeSeverity.Block || t.Problem.Foreground == Brushes.IndianRed ? Brushes.IndianRed : Brushes.DarkOrange;
        t.Problem.IsVisible = true;
        return true;
    }
}
