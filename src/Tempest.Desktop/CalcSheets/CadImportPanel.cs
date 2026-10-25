using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Library;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Addendum B, W1: the panel behind "Import CAD parameters…" on the worksheet form. It follows how "Import inputs (CSV/JSON)" is
/// wired (the form raises a request, the coordinator opens it) but shows a preview first: choose a file, a format and a mapping
/// profile; see which names are mapped, refused, unmapped, read-only expressions or missing, and what changed since the previous
/// import; then apply. All behaviour is in <see cref="CadImportViewModel"/>; this class only draws it and hosts the file pickers.
/// </summary>
public sealed class CadImportPanel : Border
{
    public const string PanelName = "Import CAD parameters";
    public const string ChooseFileCaption = "Choose CAD file…";
    public const string LoadProfileCaption = "Load profile (JSON)…";
    public const string ApplyCaption = "Apply import";
    public const string CloseCaption = "Close import";

    private readonly CadImportViewModel _model;
    private readonly TextBlock _heading = new() { FontSize = DesignTokens.FontSizeBody + 2, FontWeight = DesignTokens.WeightHeading, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _file = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap };
    private readonly Button _chooseFile = new() { Content = ChooseFileCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _loadProfile = new() { Content = LoadProfileCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly ComboBox _format = new() { MinWidth = 220, PlaceholderText = "File format" };
    private readonly ComboBox _profile = new() { MinWidth = 260, PlaceholderText = "Mapping profile" };
    private readonly CheckBox _decimalComma = new() { Content = "Numbers use a decimal comma" };
    private readonly TextBox _version = new() { MinWidth = 140, Watermark = "CAD version (optional)", MaxLength = 40 };
    private readonly TextBox _part = new() { MinWidth = 160, Watermark = "Part name (optional)", MaxLength = 80 };
    private readonly TextBox _revision = new() { MinWidth = 120, Watermark = "Revision (optional)", MaxLength = 20 };
    private readonly TextBox _configuration = new() { MinWidth = 140, Watermark = "Configuration (design table)", MaxLength = 80 };
    private readonly TextBox _sheet = new() { MinWidth = 140, Watermark = "Sheet (workbook)", MaxLength = 80 };
    private readonly StackPanel _lines = new() { Spacing = 2 };
    private readonly StackPanel _changes = new() { Spacing = 2 };
    private readonly TextBlock _summary = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, MaxWidth = 900 };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, MaxWidth = 900, Opacity = 0.85 };
    private readonly Button _apply = new() { Content = ApplyCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Button _close = new() { Content = CloseCaption, MinHeight = DesignTokens.MinControlSize };
    private bool _rendering;

    public CadImportPanel(CadImportViewModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        IsVisible = false;
        BorderThickness = new Thickness(1);
        BorderBrush = Brushes.Gray;
        Padding = new Thickness(DesignTokens.SpaceSm);
        Margin = DesignTokens.SectionMargin;

        AutomationProperties.SetName(this, PanelName);
        AutomationProperties.SetName(_chooseFile, ChooseFileCaption);
        AutomationProperties.SetName(_loadProfile, LoadProfileCaption);
        AutomationProperties.SetName(_apply, ApplyCaption);
        AutomationProperties.SetName(_close, CloseCaption);
        AutomationProperties.SetName(_format, "CAD file format");
        AutomationProperties.SetName(_profile, "Mapping profile");
        AutomationProperties.SetName(_decimalComma, "Numbers use a decimal comma");
        AutomationProperties.SetName(_lines, "Import preview");
        AutomationProperties.SetName(_changes, "Changes since the previous import");
        AutomationProperties.SetName(_status, "Import status");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);

        var files = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        files.Children.Add(_chooseFile);
        files.Children.Add(_loadProfile);
        files.Children.Add(_format);
        files.Children.Add(_profile);

        var facts = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var control in new Control[] { _decimalComma, _version, _part, _revision, _configuration, _sheet })
        {
            control.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceXs);
            facts.Children.Add(control);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        buttons.Children.Add(_apply);
        buttons.Children.Add(_close);

        var stack = new StackPanel { Spacing = DesignTokens.SpaceSm };
        stack.Children.Add(_heading);
        stack.Children.Add(files);
        stack.Children.Add(_file);
        stack.Children.Add(facts);
        stack.Children.Add(_summary);
        stack.Children.Add(_lines);
        stack.Children.Add(_changes);
        stack.Children.Add(buttons);
        stack.Children.Add(_status);
        Child = stack;

        _chooseFile.Click += async (_, _) => await GuardedAsync(PickFileAsync).ConfigureAwait(true);
        _loadProfile.Click += async (_, _) => await GuardedAsync(PickProfileAsync).ConfigureAwait(true);
        _apply.Click += async (_, _) => await GuardedAsync(ApplyAsync).ConfigureAwait(true);
        _close.Click += (_, _) => _model.Close();
        _format.SelectionChanged += async (_, _) => await GuardedAsync(OnFormatAsync).ConfigureAwait(true);
        _profile.SelectionChanged += async (_, _) => await GuardedAsync(OnProfileAsync).ConfigureAwait(true);
        _decimalComma.IsCheckedChanged += async (_, _) => await GuardedAsync(OnSettingsAsync).ConfigureAwait(true);

        foreach (var box in new[] { _version, _part, _revision, _configuration, _sheet })
            box.LostFocus += async (_, _) => await GuardedAsync(OnSettingsAsync).ConfigureAwait(true);

        _model.Changed += Render;
    }

    public CadImportViewModel Model => _model;

    // ------------------------------------------------------------------

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
            Title = "Choose a CAD parameter file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CAD parameters") { Patterns = ["*.exp", "*.xlsx", "*.csv", "*.json", "*.txt"] },
            ],
        }).ConfigureAwait(true);

        if (picked.Count == 0)
            return;

        // The picker's stream is read through the bounded reader: the 5 MB cap and the time limit hold whatever the file claims.
        await using var stream = await picked[0].OpenReadAsync().ConfigureAwait(true);
        await _model.LoadFileAsync(picked[0].Name, stream).ConfigureAwait(true);
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
            Title = "Load a mapping profile",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Mapping profile (JSON)") { Patterns = ["*.json"] }],
        }).ConfigureAwait(true);

        if (picked.Count == 0)
            return;

        await using var stream = await picked[0].OpenReadAsync().ConfigureAwait(true);

        if (stream.CanSeek && stream.Length > CadMappingProfileJson.MaxBytes)
        {
            _status.Text = $"A mapping profile is at most {CadMappingProfileJson.MaxBytes / 1000} KB.";
            return;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer).ConfigureAwait(true);

        if (buffer.Length > CadMappingProfileJson.MaxBytes)
        {
            _status.Text = $"A mapping profile is at most {CadMappingProfileJson.MaxBytes / 1000} KB.";
            return;
        }

        await _model.SaveProfileJsonAsync(System.Text.Encoding.UTF8.GetString(buffer.ToArray())).ConfigureAwait(true);
    }

    private async Task ApplyAsync()
    {
        await _model.ApplyAsync().ConfigureAwait(true);
    }

    private async Task OnFormatAsync()
    {
        if (_rendering || _format.SelectedIndex < 0 || _format.SelectedIndex >= _model.Formats.Count)
            return;

        await _model.SelectFormatAsync(_model.Formats[_format.SelectedIndex]).ConfigureAwait(true);
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

        await _model.SetSettingsAsync(_decimalComma.IsChecked == true, _version.Text, _part.Text, _revision.Text, _configuration.Text, _sheet.Text).ConfigureAwait(true);
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
            _heading.Text = $"Import CAD parameters into “{_model.InstanceName}”";
            _file.Text = _model.FileName is null ? "No file chosen." : $"{_model.FileName} — SHA-256 {_model.FileHash}";
            _format.ItemsSource = _model.Formats.Select(FormatLabel).ToList();
            _format.SelectedIndex = _model.Format is { } format ? _model.Formats.ToList().IndexOf(format) : -1;
            _profile.ItemsSource = _model.Profiles.Select(p => $"{p.Title} ({p.Label})").ToList();
            _profile.SelectedIndex = _model.Profile is { } profile ? _model.Profiles.ToList().FindIndex(p => p.Id == profile.Id && p.Version == profile.Version) : -1;
            _summary.Text = _model.Summary;
            _status.Text = _model.Status;
            _apply.IsEnabled = _model.CanApply;

            _lines.Children.Clear();

            foreach (var line in _model.Lines)
            {
                var block = new TextBlock { Text = line.Text, FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeCaption + 1, TextWrapping = TextWrapping.Wrap, MaxWidth = 900 };

                block.Foreground = line.Status switch
                {
                    CadRowStatus.Refused or CadRowStatus.Missing => Brushes.IndianRed,
                    CadRowStatus.Mapped => Brushes.SeaGreen,
                    _ => Brushes.Gray,
                };
                _lines.Children.Add(block);
            }

            _changes.Children.Clear();

            foreach (var text in _model.ChangeLines.Concat(_model.AfterLines))
                _changes.Children.Add(new TextBlock { Text = text, FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeCaption + 1, TextWrapping = TextWrapping.Wrap, MaxWidth = 900, Foreground = Brushes.DarkOrange });
        }
        finally
        {
            _rendering = false;
        }
    }

    private static string FormatLabel(CadSourceFormat format) => format switch
    {
        CadSourceFormat.NxExpressions => "NX expressions (.exp)",
        CadSourceFormat.NxXlsx => "NX expressions (.xlsx)",
        CadSourceFormat.CreoRelations => "Creo relations and parameters (text)",
        CadSourceFormat.SolidWorksEquations => "SolidWorks equations (text)",
        CadSourceFormat.SolidWorksDesignTable => "SolidWorks design table (.xlsx)",
        CadSourceFormat.OnshapeCsv => "Onshape variables (CSV)",
        CadSourceFormat.OnshapeJson => "Onshape variables (JSON)",
        CadSourceFormat.GenericCsv => "Generic parameters (CSV)",
        _ => "Generic parameters (JSON)",
    };
}
