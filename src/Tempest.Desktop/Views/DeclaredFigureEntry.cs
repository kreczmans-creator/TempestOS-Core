using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Core.Evidence;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>Collects one declared figure — name, role, value and unit (`WP 18.2A`, §4).</summary>
/// <param name="Name">The figure's own name (e.g. "Utilisation", "Max stress").</param>
/// <param name="Role">Whether this figure was fed into the work, or came out of it.</param>
/// <param name="Quantity">The value, as <c>"&lt;value&gt; &lt;unit symbol&gt;"</c> text — <see cref="EvidenceUnitCatalog"/>'s own accepted form.</param>
public sealed record DeclaredFigureInput(string Name, DeclaredFigureRole Role, string Quantity);

/// <summary>
/// The Evidence workspace's own declared-figure entry dialog (`WP 18.2A`,
/// §4): name, role (Input/Result) and a typed value with a unit picker
/// drawn from every unit this platform's own dimension catalogues declare
/// (<see cref="EvidenceUnitCatalog.KnownUnits"/>). Initially hidden, shares
/// the Dialog Framework's own established panel styling and real modal
/// behaviour (mirrors <see cref="InputDialog"/>).
/// </summary>
public sealed class DeclaredFigureEntry : Border
{
    private static readonly IReadOnlyList<string> UnitSymbols =
        [.. EvidenceUnitCatalog.KnownUnits.Select(u => u.Symbol).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal)];

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBox _name = new() { Watermark = "e.g. Utilisation", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly ComboBox _role = new() { MinHeight = DesignTokens.ControlSizeMedium, ItemsSource = Enum.GetValues<DeclaredFigureRole>(), Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBox _value = new() { Watermark = "e.g. 0.82", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly ComboBox _unit = new() { MinHeight = DesignTokens.ControlSizeMedium, ItemsSource = UnitSymbols, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBlock _validation = new() { FontSize = DesignTokens.FontSizeCaption, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0), IsVisible = false };
    private readonly Button _declareButton = new() { Content = "Declare", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private TaskCompletionSource<DeclaredFigureInput?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="DeclaredFigureEntry"/> class, initially hidden.</summary>
    public DeclaredFigureEntry()
    {
        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 360;
        MaxWidth = 440;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _title.Text = "Declare a figure";

        var valueRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
        Grid.SetColumn(_value, 0);
        Grid.SetColumn(_unit, 1);
        _value.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, 0);
        _unit.Width = 110;
        valueRow.Children.Add(_value);
        valueRow.Children.Add(_unit);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_declareButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_name);
        body.Children.Add(_role);
        body.Children.Add(valueRow);
        body.Children.Add(_validation);
        body.Children.Add(buttons);
        Child = body;

        _declareButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);

        _declareButton.Click += (_, _) => TryComplete();
        _cancelButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>Shows this dialog and returns the figure entered, or <see langword="null"/> if the user cancelled.</summary>
    public Task<DeclaredFigureInput?> PromptAsync(CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _name.Text = string.Empty;
        _role.SelectedItem = DeclaredFigureRole.Result;
        _value.Text = string.Empty;
        _unit.SelectedItem = UnitSymbols.FirstOrDefault(s => s == "1") ?? UnitSymbols.FirstOrDefault();
        _validation.IsVisible = false;
        IsVisible = true;
        _name.Focus();

        _pending = new TaskCompletionSource<DeclaredFigureInput?>();
        return _pending.Task;
    }

    private void TryComplete()
    {
        var name = _name.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowValidationError("A name is required.");
            return;
        }

        if (_role.SelectedItem is not DeclaredFigureRole role)
        {
            ShowValidationError("A role is required.");
            return;
        }

        if (!double.TryParse(_value.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            ShowValidationError("Value must be a number.");
            return;
        }

        if (_unit.SelectedItem is not string unit || string.IsNullOrWhiteSpace(unit))
        {
            ShowValidationError("A unit is required.");
            return;
        }

        var quantity = $"{_value.Text!.Trim()} {unit}";
        Complete(new DeclaredFigureInput(name, role, quantity));
    }

    private void ShowValidationError(string message)
    {
        _validation.Text = message;
        _validation.IsVisible = true;
    }

    private void Complete(DeclaredFigureInput? input)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(input);
    }
}
