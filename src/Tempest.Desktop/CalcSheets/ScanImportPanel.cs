using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Addendum B, W2: the panel behind "Import scan deviations…" on the worksheet form. It follows the CAD import panel: choose one or more scan tables
/// (with the stage and serial for tables that have no column for them), a scan profile and the facts about the measurement; see each feature's
/// statistics and what is mapped, refused, unmapped or missing, what was reported in the tables and what changed since the previous import; then apply.
/// All behaviour is in <see cref="ScanImportViewModel"/>; this class only draws it and hosts the file pickers. Files and profiles are read through the
/// bounded reader; the profile is held to its own size cap.
/// </summary>
public sealed class ScanImportPanel : Border
{
    public const string PanelName = "Import scan deviations";
    public const string ChooseFileCaption = "Choose scan table…";
    public const string LoadProfileCaption = "Load scan profile (JSON)…";
    public const string ApplyCaption = "Apply import";
    public const string CancelCaption = "Cancel reading";
    public const string CloseCaption = "Close import";
    public const string ClearFilesCaption = "Remove chosen tables";

    private readonly ScanImportViewModel _model;
    private readonly TextBlock _heading = new() { FontSize = DesignTokens.FontSizeBody + 2, FontWeight = DesignTokens.WeightHeading, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _files = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
    private readonly Button _chooseFile = new() { Content = ChooseFileCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _clearFiles = new() { Content = ClearFilesCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _loadProfile = new() { Content = LoadProfileCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly ComboBox _profile = new() { MinWidth = 260, PlaceholderText = "Scan profile" };
    private readonly TextBox _stage = new() { MinWidth = 150, Watermark = "Stage of the next table (optional)", MaxLength = 40 };
    private readonly TextBox _serial = new() { MinWidth = 150, Watermark = "Serial of the next table (optional)", MaxLength = 60 };
    private readonly TextBox _software = new() { MinWidth = 150, Watermark = "Software version (optional)", MaxLength = 40 };
    private readonly TextBox _alignment = new() { MinWidth = 180, Watermark = "Alignment (RPS, best fit …)", MaxLength = 60 };
    private readonly TextBox _subgroup = new() { MinWidth = 90, Watermark = "Subgroup size", Text = "1", MaxLength = 4 };
    private readonly TextBox _minParts = new() { MinWidth = 90, Watermark = "Minimum parts", Text = "30", MaxLength = 6 };
    private readonly CheckBox _exclude = new() { Content = "Leave flagged outliers out of the statistics" };
    private readonly TextBlock _numberFormat = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 };
    private readonly StackPanel _stats = new() { Spacing = 2 };
    private readonly StackPanel _problems = new() { Spacing = 2 };
    private readonly StackPanel _lines = new() { Spacing = 2 };
    private readonly StackPanel _changes = new() { Spacing = 2 };
    private readonly TextBlock _summary = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, MaxWidth = 900 };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, MaxWidth = 900, Opacity = 0.85 };
    private readonly Button _apply = new() { Content = ApplyCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Button _cancel = new() { Content = CancelCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Button _close = new() { Content = CloseCaption, MinHeight = DesignTokens.MinControlSize };
    private bool _rendering;

    public ScanImportPanel(ScanImportViewModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        IsVisible = false;
        BorderThickness = new Thickness(1);
        BorderBrush = Brushes.Gray;
        Padding = new Thickness(DesignTokens.SpaceSm);
        Margin = DesignTokens.SectionMargin;

        AutomationProperties.SetName(this, PanelName);
        AutomationProperties.SetName(_chooseFile, ChooseFileCaption);
        AutomationProperties.SetName(_clearFiles, ClearFilesCaption);
        AutomationProperties.SetName(_loadProfile, LoadProfileCaption);
        AutomationProperties.SetName(_apply, ApplyCaption);
        AutomationProperties.SetName(_cancel, CancelCaption);
        AutomationProperties.SetName(_close, CloseCaption);
        AutomationProperties.SetName(_profile, "Scan profile");
        AutomationProperties.SetName(_exclude, "Leave flagged outliers out of the statistics");
        AutomationProperties.SetName(_stats, "Scan statistics");
        AutomationProperties.SetName(_problems, "Reported in the tables");
        AutomationProperties.SetName(_lines, "Import preview");
        AutomationProperties.SetName(_changes, "Changes since the previous import");
        AutomationProperties.SetName(_status, "Import status");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);

        var pick = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        pick.Children.Add(_chooseFile);
        pick.Children.Add(_clearFiles);
        pick.Children.Add(_loadProfile);
        pick.Children.Add(_profile);

        var facts = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var control in new Control[] { _stage, _serial, _software, _alignment, _subgroup, _minParts, _exclude })
        {
            control.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceXs);
            facts.Children.Add(control);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        buttons.Children.Add(_apply);
        buttons.Children.Add(_cancel);
        buttons.Children.Add(_close);

        var stack = new StackPanel { Spacing = DesignTokens.SpaceSm };
        stack.Children.Add(_heading);
        stack.Children.Add(pick);
        stack.Children.Add(_files);
        stack.Children.Add(facts);
        stack.Children.Add(_numberFormat);
        stack.Children.Add(_summary);
        stack.Children.Add(_stats);
        stack.Children.Add(_problems);
        stack.Children.Add(_lines);
        stack.Children.Add(_changes);
        stack.Children.Add(buttons);
        stack.Children.Add(_status);
        Child = stack;

        _chooseFile.Click += async (_, _) => await GuardedAsync(PickFilesAsync).ConfigureAwait(true);
        _clearFiles.Click += async (_, _) => await GuardedAsync(ClearFilesAsync).ConfigureAwait(true);
        _loadProfile.Click += async (_, _) => await GuardedAsync(PickProfileAsync).ConfigureAwait(true);
        _apply.Click += async (_, _) => await GuardedAsync(async () => { await _model.ApplyAsync().ConfigureAwait(true); }).ConfigureAwait(true);
        _cancel.Click += (_, _) => _model.Cancel();
        _close.Click += (_, _) => _model.Close();
        _profile.SelectionChanged += async (_, _) => await GuardedAsync(OnProfileAsync).ConfigureAwait(true);
        _exclude.IsCheckedChanged += async (_, _) => await GuardedAsync(OnSettingsAsync).ConfigureAwait(true);
        _stage.LostFocus += (_, _) => _model.NextStage = _stage.Text ?? string.Empty;
        _serial.LostFocus += (_, _) => _model.NextSerial = _serial.Text ?? string.Empty;

        foreach (var box in new[] { _software, _alignment, _subgroup, _minParts })
            box.LostFocus += async (_, _) => await GuardedAsync(OnSettingsAsync).ConfigureAwait(true);

        _model.Changed += Render;
    }

    public ScanImportViewModel Model => _model;

    // ------------------------------------------------------------------

    private async Task PickFilesAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storage is null || !storage.CanOpen)
        {
            _status.Text = "No file picker is available here.";
            return;
        }

        _model.NextStage = _stage.Text ?? string.Empty;
        _model.NextSerial = _serial.Text ?? string.Empty;

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose scan deviation tables",
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("Scan tables") { Patterns = ["*.csv", "*.txt", "*.tsv", "*.xlsx"] }],
        }).ConfigureAwait(true);

        foreach (var file in picked)
        {
            // Read through the bounded reader: the 5 MB cap and the time limit hold whatever the file claims.
            await using var stream = await file.OpenReadAsync().ConfigureAwait(true);
            await _model.AddFileAsync(file.Name, stream).ConfigureAwait(true);
        }
    }

    private async Task ClearFilesAsync()
    {
        while (_model.Files.Count > 0)
            await _model.RemoveFileAsync(_model.Files.Count - 1).ConfigureAwait(true);
    }

    private async Task PickProfileAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storage is null || !storage.CanOpen)
        {
            _status.Text = "No file picker is available here.";
            return;
        }

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load a scan profile",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Scan profile (JSON)") { Patterns = ["*.json"] }],
        }).ConfigureAwait(true);

        if (picked.Count == 0)
            return;

        // Bounded read (size cap and time limit) and strict UTF-8 with the BOM dropped, like every other untrusted file.
        await using var stream = await picked[0].OpenReadAsync().ConfigureAwait(true);

        try
        {
            var file = await BoundedInputReader.ReadAsync(stream, picked[0].Name, ImportLimits.Default with { MaxBytes = ScanProfileJson.MaxBytes }).ConfigureAwait(true);
            await _model.SaveProfileJsonAsync(BoundedInputReader.DecodeText(file)).ConfigureAwait(true);
        }
        catch (ImportRefusedException e)
        {
            _status.Text = "That profile was not read: " + e.Message;
        }
    }

    private async Task OnProfileAsync()
    {
        if (_rendering || _profile.SelectedIndex < 0 || _profile.SelectedIndex >= _model.Profiles.Count)
            return;

        await _model.SelectProfileAsync(_model.Profiles[_profile.SelectedIndex]).ConfigureAwait(true);
    }

    private async Task OnSettingsAsync()
    {
        if (_rendering)
            return;

        var subgroup = int.TryParse(_subgroup.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var g) ? g : 1;
        var minParts = int.TryParse(_minParts.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var m) ? m : 30;
        await _model.SetSettingsAsync(_software.Text, _alignment.Text, subgroup, minParts, _exclude.IsChecked == true).ConfigureAwait(true);
    }

    private async Task GuardedAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            _status.Text = $"Could not import: {failure.Message}";
        }
    }

    // ------------------------------------------------------------------

    private void Render()
    {
        _rendering = true;

        try
        {
            IsVisible = _model.IsOpen;
            _heading.Text = $"Import scan deviations into “{_model.InstanceName}”";
            _files.Text = _model.Files.Count == 0
                ? "No table chosen."
                : string.Join(Environment.NewLine, _model.Files.Select(f => $"{f.Name}{(f.Input.Stage is null ? string.Empty : " — stage " + f.Input.Stage)}{(f.Input.Serial is null ? string.Empty : " — serial " + f.Input.Serial)} — SHA-256 {f.Sha256}"));
            _profile.ItemsSource = _model.Profiles.Select(p => $"{p.Title} ({p.Label})").ToList();
            _profile.SelectedIndex = _model.Profile is { } profile ? _model.Profiles.ToList().FindIndex(p => p.Id == profile.Id && p.Version == profile.Version) : -1;
            _numberFormat.Text = _model.NumberFormatText;
            _summary.Text = _model.Summary;
            _status.Text = _model.Status;
            _apply.IsEnabled = _model.CanApply;
            _cancel.IsEnabled = _model.IsBusy;

            Fill(_stats, _model.StatLines.Select(t => (t, t.StartsWith("    ", StringComparison.Ordinal) ? (IBrush)Brushes.DarkOrange : Brushes.Gray)));
            Fill(_problems, _model.ProblemLines.Select(t => (t, (IBrush)Brushes.DarkOrange)));
            Fill(_lines, _model.Lines.Select(l => (l.Text, l.Status switch
            {
                ScanRowStatus.Refused or ScanRowStatus.Missing => (IBrush)Brushes.IndianRed,
                ScanRowStatus.Mapped => Brushes.SeaGreen,
                _ => Brushes.Gray,
            })));
            Fill(_changes, _model.ChangeLines.Concat(_model.AfterLines).Select(t => (t, (IBrush)Brushes.DarkOrange)));
        }
        finally
        {
            _rendering = false;
        }
    }

    private static void Fill(StackPanel host, IEnumerable<(string Text, IBrush Brush)> lines)
    {
        host.Children.Clear();

        foreach (var (text, brush) in lines)
            host.Children.Add(new TextBlock { Text = text, FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeCaption + 1, TextWrapping = TextWrapping.Wrap, MaxWidth = 900, Foreground = brush });
    }
}
