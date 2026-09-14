using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Evidence;

/// <summary>
/// Every unit this platform's own dimension catalogues declare, flattened
/// into one list — what a declared figure's unit is checked against
/// (`ADR-0148`).
/// </summary>
/// <remarks>
/// A declared figure's own dimension is not known in advance (an engineer
/// might declare a stress, a length, a dimensionless ratio, anything), so
/// parsing it needs every dimension's own catalogue at once rather than one
/// chosen ahead of time — exactly the situation
/// <see cref="Quantity"/> (the runtime-dimension facade, `ADR-0147`) exists
/// for. This is additive only: a unit added to any one dimension's own
/// catalogue reaches this list automatically the next time it is built.
/// </remarks>
public static class EvidenceUnitCatalog
{
    /// <summary>Every known <see cref="UnitDefinition"/>, across every dimension this platform declares.</summary>
    public static IReadOnlyList<UnitDefinition> KnownUnits { get; } = Build();

    private static IReadOnlyList<UnitDefinition> Build()
    {
        var units = new List<UnitDefinition>();

        void Add<TDimension>(IReadOnlyList<Unit<TDimension>> catalogue) where TDimension : IDimension =>
            units.AddRange(catalogue.Select(u => u.UnitDefinition));

        Add(AccelerationUnits.All);
        Add(AreaUnits.All);
        Add(DimensionlessUnits.All);
        Add(DurationUnits.All);
        Add(ElectricChargeUnits.All);
        Add(ElectricCurrentUnits.All);
        Add(EnergyUnits.All);
        Add(ForceUnits.All);
        Add(FrequencyUnits.All);
        Add(LengthUnits.All);
        Add(MassDensityUnits.All);
        Add(MassUnits.All);
        Add(PlaneAngleUnits.All);
        Add(PowerUnits.All);
        Add(PressureUnits.All);
        Add(ResistanceUnits.All);
        Add(RotationalSpeedUnits.All);
        Add(SecondMomentOfAreaUnits.All);
        Add(SectionModulusUnits.All);
        Add(SpecificHeatCapacityUnits.All);
        Add(StiffnessUnits.All);
        Add(TemperatureUnits.All);
        Add(TemperatureDeltaUnits.All);
        Add(ThermalConductivityUnits.All);
        Add(ThermalExpansionUnits.All);
        Add(TorqueUnits.All);
        Add(TorsionalStiffnessUnits.All);
        Add(VelocityUnits.All);
        Add(VoltageUnits.All);
        Add(VolumeUnits.All);

        return units;
    }

    /// <summary>Parses <paramref name="input"/> ("&lt;value&gt; &lt;symbol&gt;") against <see cref="KnownUnits"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="input"/> is not a recognised quantity for a known unit.</exception>
    public static Quantity Parse(string input)
    {
        if (!Quantity.TryParse(input, KnownUnits, out var result))
            throw new ArgumentException($"'{input}' is not a recognised quantity — its unit is not one this platform knows. Declare it as \"<value> <symbol>\" (e.g. \"142 MPa\", \"0.82 1\").", nameof(input));

        return result;
    }
}
