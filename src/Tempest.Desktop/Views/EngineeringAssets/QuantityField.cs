using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.UnitsAndQuantities;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views.EngineeringAssets;

/// <summary>
/// One dimensioned input — a value box and a unit picker drawn from
/// <typeparamref name="TDimension"/>'s own unit catalogue.
/// </summary>
/// <remarks>
/// The bracket verification form's own building block (`WP 21.2B`,
/// `TD-165`, scope item 2: "every input a quantity with a unit picker") —
/// one instance per dimension, each offering every unit that dimension's
/// own catalogue (<c>ForceUnits.All</c>, <c>AreaUnits.All</c>, and so on)
/// declares, unlike <c>BracketCalculationInputs</c> (the unchanged
/// Engineering Calculations surface's own contract), whose four fields
/// are typed text in one fixed unit apiece. Not a <see cref="Control"/>
/// itself — a small builder over two controls, the same shape
/// <c>DeclaredFigureEntry</c> already establishes for a single dialog's
/// worth of value-plus-unit, reused here per field on a form rather than
/// once per dialog.
/// </remarks>
public sealed class QuantityField<TDimension>
    where TDimension : IDimension
{
    private readonly string _label;
    private readonly IReadOnlyList<Unit<TDimension>> _units;
    private readonly TextBox _value = new() { MinHeight = DesignTokens.ControlSizeMedium };
    private readonly ComboBox _unit = new() { MinHeight = DesignTokens.ControlSizeMedium, Width = 90 };

    /// <summary>Initialises a new instance of the <see cref="QuantityField{TDimension}"/> class.</summary>
    /// <param name="label">What the field asks for, e.g. "Applied load".</param>
    /// <param name="units">Every unit this field may be entered in.</param>
    /// <exception cref="ArgumentException"><paramref name="label"/> is null, empty or whitespace, or <paramref name="units"/> is empty.</exception>
    public QuantityField(string label, IReadOnlyList<Unit<TDimension>> units)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(units);
        if (units.Count == 0)
            throw new ArgumentException("A quantity field needs at least one unit to offer.", nameof(units));

        _label = label;
        _units = units;

        _value.Watermark = label;
        _unit.ItemsSource = units.Select(u => u.Symbol).ToList();
        _unit.SelectedItem = units[0].Symbol;

        AutomationProperties.SetName(_value, label);
        AutomationProperties.SetName(_unit, $"{label} unit");
    }

    /// <summary>Builds this field's own row — a caption, the value box and the unit picker.</summary>
    public Control BuildRow()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 0, 0, DesignTokens.SpaceSm) };

        var caption = new TextBlock
        {
            Text = _label, VerticalAlignment = VerticalAlignment.Center, Width = 190, FontSize = DesignTokens.FontSizeBody,
        };
        _value.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, 0);

        Grid.SetColumn(caption, 0);
        Grid.SetColumn(_value, 1);
        Grid.SetColumn(_unit, 2);
        row.Children.Add(caption);
        row.Children.Add(_value);
        row.Children.Add(_unit);
        return row;
    }

    /// <summary>Clears the value, leaving the unit at its first offered choice.</summary>
    public void Reset()
    {
        _value.Text = string.Empty;
        _unit.SelectedItem = _units[0].Symbol;
    }

    /// <summary>Parses the field, refusing a blank, non-numeric or non-positive value.</summary>
    /// <param name="quantity">The parsed quantity, where parsing succeeded.</param>
    /// <param name="problem">Why it did not, where it did not.</param>
    /// <returns><see langword="true"/> where a quantity was parsed.</returns>
    public bool TryGetValue(out Quantity<TDimension> quantity, out string? problem)
    {
        quantity = default;

        if (string.IsNullOrWhiteSpace(_value.Text))
        {
            problem = $"{_label} is required.";
            return false;
        }

        if (!double.TryParse(_value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw)
            || double.IsNaN(raw) || double.IsInfinity(raw))
        {
            problem = $"{_label} must be a number.";
            return false;
        }

        if (raw <= 0)
        {
            problem = $"{_label} must be greater than zero.";
            return false;
        }

        var symbol = _unit.SelectedItem as string;
        var unit = _units.FirstOrDefault(u => string.Equals(u.Symbol, symbol, StringComparison.Ordinal), _units[0]);
        quantity = new Quantity<TDimension>(raw, unit);
        problem = null;
        return true;
    }
}
