using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Calc.Library;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// The reference-record pickers (ADR-0167 §3). A library whose records carry a size and a grade (the fastener records:
/// "M12 x 1.75" and "8.8") is offered as two pickers (owner, 2026-10-29): the thread size, smallest first, then the grade,
/// listing only the grades that exist for that size. The pair selects the one record, so the fill, the record's pin and
/// the saved calculation are unchanged. A library whose records have no size (materials, bearings) keeps one picker.
/// </summary>
public sealed partial class CalcSheetView
{
    /// <summary>One released record as a picker offers it: its label, and its size and grade where it has them.</summary>
    public sealed record ReferenceChoice(string Label, string? Size = null, string? Grade = null);

    /// <summary>The text a grade picker shows for a record of a size that names no grade (a thread-only record).</summary>
    public const string NoGradeText = "No grade (thread only)";

    /// <summary>The size and grade pickers of a library that is split.</summary>
    private sealed class SplitPicker(ComboBox size, ComboBox grade, Control host)
    {
        public ComboBox Size { get; } = size;

        public ComboBox Grade { get; } = grade;

        public Control Host { get; } = host;

        /// <summary>The choices the pickers were last given, in the coordinator's order (a pick reports an index into it).</summary>
        public IReadOnlyList<ReferenceChoice> Choices { get; set; } = [];

        /// <summary>The sizes listed, in order.</summary>
        public List<string> Sizes { get; } = [];

        /// <summary>The indexes into <see cref="Choices"/> of the grades listed for the chosen size, in order.</summary>
        public List<int> Grades { get; } = [];
    }

    private readonly Dictionary<string, SplitPicker> _splitPickers = new(StringComparer.Ordinal);

    /// <summary>The automation name of a split library's size picker.</summary>
    public static string ReferenceSizePickerName(string library) => $"{library} size";

    /// <summary>The automation name of a split library's grade picker.</summary>
    public static string ReferenceGradePickerName(string library) => $"{library} grade";

    /// <summary>Whether <paramref name="library"/> is offered as a size picker and a grade picker.</summary>
    public bool IsReferencePickerSplit(string library) => _splitPickers.TryGetValue(library, out var split) && split.Host.IsVisible;

    /// <summary>Fills a library's picker(s) with the released records; a library whose records carry sizes is split into size and grade.</summary>
    public void SetReferenceOptions(string library, IReadOnlyList<ReferenceChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);

        if (!_referencePickers.ContainsKey(library))
            return;

        _referenceLabels[library] = choices;
        _referenceLabelsFor = SelectedTemplate?.Reference;
        ShowChoices(library, choices);
    }

    /// <summary>
    /// Whether <paramref name="choices"/> read better as a size and a grade: some record names a size, and some size has
    /// more than one record. A record whose size cannot be read is then listed under its own label, with one grade.
    /// </summary>
    public static bool ShouldSplit(IReadOnlyList<ReferenceChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        return choices.Any(c => c.Size is not null) && choices.GroupBy(SizeOf, StringComparer.Ordinal).Any(g => g.Count() > 1);
    }

    private static string SizeOf(ReferenceChoice choice) => choice.Size ?? choice.Label;

    private static string GradeText(ReferenceChoice choice) => choice.Grade ?? (choice.Size is null ? choice.Label : NoGradeText);

    private void ShowChoices(string library, IReadOnlyList<ReferenceChoice> choices)
    {
        var single = _referencePickers[library];
        var split = _splitPickers.GetValueOrDefault(library);

        if (split is null || !ShouldSplit(choices))
        {
            if (split is not null)
                split.Host.IsVisible = false;

            single.IsVisible = true;
            ShowOptions(single, choices.Select(c => c.Label).ToList());
            return;
        }

        single.IsVisible = false;
        split.Host.IsVisible = true;
        split.Choices = choices;
        split.Sizes.Clear();
        split.Sizes.AddRange(choices.Select(SizeOf).Distinct(StringComparer.Ordinal));
        split.Grades.Clear();

        var loading = _loadingForm;
        _loadingForm = true;
        var thread = CalcReferenceFill.CatalogueOf(library) == CalcReferenceFill.Fasteners ? "thread size" : "size";
        split.Size.ItemsSource = split.Sizes.ToList();
        split.Size.SelectedIndex = -1;
        split.Size.IsEnabled = !IsReadOnly;
        split.Size.PlaceholderText = $"Pick a {thread} ({split.Sizes.Count})";
        ToolTip.SetTip(split.Size, $"{split.Sizes.Count} sizes, {choices.Count} released records. Pick a size, then a grade.");
        split.Grade.ItemsSource = Array.Empty<string>();
        split.Grade.SelectedIndex = -1;
        split.Grade.IsEnabled = false;
        split.Grade.PlaceholderText = $"Pick a {thread} first";
        _loadingForm = loading;
    }

    /// <summary>The size and grade pickers of a library, in one row cell; hidden until the records show they can be split.</summary>
    private SplitPicker BuildSplitPicker(string library)
    {
        var size = new ComboBox { MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false, PlaceholderText = "Loading released records…" };
        var grade = new ComboBox { MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false, PlaceholderText = "Pick a size first" };
        AutomationProperties.SetName(size, ReferenceSizePickerName(library));
        AutomationProperties.SetName(grade, ReferenceGradePickerName(library));
        ToolTip.SetTip(grade, "The grade or property class. Only the grades released for the chosen size are listed.");
        var host = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = DesignTokens.SpaceSm, IsVisible = false };
        Grid.SetColumn(grade, 1);
        host.Children.Add(size);
        host.Children.Add(grade);
        var split = new SplitPicker(size, grade, host);

        size.SelectionChanged += (_, _) =>
        {
            if (!_loadingForm && size.SelectedIndex >= 0)
                OnSizePicked(library, split);
        };

        grade.SelectionChanged += (_, _) =>
        {
            if (!_loadingForm && grade.SelectedIndex >= 0 && grade.SelectedIndex < split.Grades.Count)
                ReferencePicked?.Invoke(library, split.Grades[grade.SelectedIndex]);
        };

        _splitPickers[library] = split;
        return split;
    }

    /// <summary>A size was picked: the grade picker lists that size's grades. The grade already chosen is kept where the size has it, and a size with one record picks it.</summary>
    private void OnSizePicked(string library, SplitPicker split)
    {
        var chosen = split.Sizes[split.Size.SelectedIndex];
        var previous = split.Grade.SelectedIndex >= 0 && split.Grade.SelectedIndex < split.Grades.Count ? GradeText(split.Choices[split.Grades[split.Grade.SelectedIndex]]) : null;
        split.Grades.Clear();

        for (var i = 0; i < split.Choices.Count; i++)
        {
            if (string.Equals(SizeOf(split.Choices[i]), chosen, StringComparison.Ordinal))
                split.Grades.Add(i);
        }

        var texts = split.Grades.Select(i => GradeText(split.Choices[i])).ToList();
        var loading = _loadingForm;
        _loadingForm = true;
        split.Grade.ItemsSource = texts;
        split.Grade.SelectedIndex = -1;
        split.Grade.IsEnabled = texts.Count > 0 && !IsReadOnly;
        split.Grade.PlaceholderText = texts.Count == 0 ? "No released record" : $"Pick a grade ({texts.Count})";
        _loadingForm = loading;

        var keep = previous is null ? -1 : texts.IndexOf(previous);

        if (keep >= 0)
            split.Grade.SelectedIndex = keep;
        else if (texts.Count == 1)
            split.Grade.SelectedIndex = 0;
    }

    /// <summary>A reference picker fills fields, so it is read-only with them: both pickers of a split library too.</summary>
    private void SetReferencePickersEnabled()
    {
        foreach (var picker in _referencePickers.Values)
            picker.IsEnabled = !IsReadOnly && picker.ItemCount > 0;

        foreach (var split in _splitPickers.Values)
        {
            split.Size.IsEnabled = !IsReadOnly && split.Size.ItemCount > 0;
            split.Grade.IsEnabled = !IsReadOnly && split.Grade.ItemCount > 0;
        }
    }
}
