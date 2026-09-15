using System.Globalization;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>
/// The unit catalogues a generated calculation form offers, keyed by the
/// dimension name a <see cref="CalculationInputDescriptor"/> states, and
/// the one way a typed quantity is built from what an engineer typed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists (`WP 21.7B`).</b> A form generated from descriptors
/// knows an input's dimension only by name. The typed catalogues
/// (<see cref="LengthUnits"/>, <see cref="PressureUnits"/> and the rest)
/// are what a unit picker must offer and what <see cref="Quantity{TDimension}.Parse"/>
/// needs; this registry is the bridge from the name to both, so no view
/// ever restates a conversion factor or names a unit of its own.
/// </para>
/// <para>
/// <b>Deliberately a closed list.</b> Only the dimensions the module
/// descriptors use are registered; asking for another is an error, not a
/// silent empty picker, because a descriptor naming an unregistered
/// dimension is a defect in the descriptor.
/// </para>
/// </remarks>
public static class CalculationInputUnits
{
    private sealed record Registration(Type DimensionType, IReadOnlyList<string> Symbols, Func<string, string, object> Parse, Func<object, string, double> ValueIn);

    /// <summary>The value of the boxed quantity <paramref name="quantity"/> expressed in the unit <paramref name="unitSymbol"/> of <paramref name="dimensionName"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="dimensionName"/> is not registered, <paramref name="quantity"/> is not a quantity of it, or the symbol is not in its catalogue.</exception>
    public static double ValueIn(string dimensionName, object quantity, string unitSymbol)
    {
        ArgumentNullException.ThrowIfNull(quantity);
        var registration = Find(dimensionName);

        if (!registration.Symbols.Contains(unitSymbol, StringComparer.Ordinal))
            throw new ArgumentException($"'{unitSymbol}' is not a unit of {dimensionName}.", nameof(unitSymbol));

        return registration.ValueIn(quantity, unitSymbol);
    }

    private static readonly IReadOnlyDictionary<string, Registration> Registrations = new Dictionary<string, Registration>(StringComparer.Ordinal)
    {
        [nameof(Length)] = Register(LengthUnits.All),
        [nameof(Force)] = Register(ForceUnits.All),
        [nameof(Pressure)] = Register(PressureUnits.All),
        [nameof(Area)] = Register(AreaUnits.All),
        [nameof(SecondMomentOfArea)] = Register(SecondMomentOfAreaUnits.All),
        [nameof(Torque)] = Register(TorqueUnits.All),
        [nameof(Stiffness)] = Register(StiffnessUnits.All),
        [nameof(RotationalSpeed)] = Register(RotationalSpeedUnits.All),
        [nameof(Duration)] = Register(DurationUnits.All),
        [nameof(ThermalExpansion)] = Register(ThermalExpansionUnits.All),
        [nameof(TemperatureDelta)] = Register(TemperatureDeltaUnits.All),
        [nameof(MassDensity)] = Register(MassDensityUnits.All),
        [nameof(Mass)] = Register(MassUnits.All),
    };

    /// <summary>Every dimension name this registry knows.</summary>
    public static IReadOnlyList<string> DimensionNames { get; } = Registrations.Keys.Order(StringComparer.Ordinal).ToList();

    /// <summary>Whether <paramref name="dimensionName"/> is registered.</summary>
    public static bool IsKnown(string dimensionName) => Registrations.ContainsKey(dimensionName);

    /// <summary>The unit symbols of <paramref name="dimensionName"/>'s catalogue, in catalogue order.</summary>
    /// <exception cref="ArgumentException"><paramref name="dimensionName"/> is not registered.</exception>
    public static IReadOnlyList<string> SymbolsOf(string dimensionName) => Find(dimensionName).Symbols;

    /// <summary>The <see cref="IDimension"/> marker type <paramref name="dimensionName"/> names.</summary>
    /// <exception cref="ArgumentException"><paramref name="dimensionName"/> is not registered.</exception>
    public static Type DimensionTypeOf(string dimensionName) => Find(dimensionName).DimensionType;

    /// <summary>
    /// Builds the boxed <see cref="Quantity{TDimension}"/> of
    /// <paramref name="dimensionName"/> from a typed number and a chosen
    /// unit symbol, or explains why it could not.
    /// </summary>
    /// <returns>The quantity, or <see langword="null"/> with <paramref name="problem"/> set.</returns>
    /// <exception cref="ArgumentException"><paramref name="dimensionName"/> is not registered.</exception>
    public static object? TryParse(string dimensionName, string? valueText, string? unitSymbol, out string? problem)
    {
        var registration = Find(dimensionName);
        problem = null;

        if (string.IsNullOrWhiteSpace(valueText))
        {
            problem = "no value was entered";
            return null;
        }

        if (!double.TryParse(valueText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
        {
            problem = $"'{valueText.Trim()}' is not a number";
            return null;
        }

        if (string.IsNullOrWhiteSpace(unitSymbol) || !registration.Symbols.Contains(unitSymbol, StringComparer.Ordinal))
        {
            problem = $"'{unitSymbol}' is not a unit of {dimensionName} (one of {string.Join(", ", registration.Symbols)})";
            return null;
        }

        return registration.Parse(value.ToString("R", CultureInfo.InvariantCulture), unitSymbol);
    }

    /// <summary>
    /// Builds the boxed quantity from one "&lt;number&gt; &lt;symbol&gt;" text,
    /// the form a row of a list input is typed in.
    /// </summary>
    /// <returns>The quantity, or <see langword="null"/> with <paramref name="problem"/> set.</returns>
    public static object? TryParseWithUnit(string dimensionName, string? text, out string? problem)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        var separator = trimmed.LastIndexOf(' ');

        if (separator < 0)
        {
            problem = $"'{trimmed}' needs a unit, as in '75 mm'";
            return null;
        }

        return TryParse(dimensionName, trimmed[..separator], trimmed[(separator + 1)..].Trim(), out problem);
    }

    private static Registration Find(string dimensionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionName);

        return Registrations.TryGetValue(dimensionName, out var registration)
            ? registration
            : throw new ArgumentException($"No unit catalogue is registered for the dimension '{dimensionName}'. Registered: {string.Join(", ", DimensionNames)}.", nameof(dimensionName));
    }

    private static Registration Register<TDimension>(IReadOnlyList<Unit<TDimension>> catalogue)
        where TDimension : IDimension =>
        new(
            typeof(TDimension),
            catalogue.Select(u => u.Symbol).ToList(),
            (valueText, symbol) => Quantity<TDimension>.Parse($"{valueText} {symbol}", catalogue),
            (quantity, symbol) => quantity is Quantity<TDimension> typed
                ? typed.ConvertTo(catalogue.Single(u => string.Equals(u.Symbol, symbol, StringComparison.Ordinal))).Value
                : throw new ArgumentException($"The value is a {quantity.GetType().Name}, not a quantity of {typeof(TDimension).Name}.", nameof(quantity)));
}
