using Tempest.Core.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// The small vocabulary every day-one material record is written in.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each value states its origin and its conditions.</b> A limit a
/// standard sets (and the source restates) is
/// <see cref="ReferenceValueOrigin.Standard"/>; a figure a manufacturer
/// publishes for its own product is
/// <see cref="ReferenceValueOrigin.ManufacturerCatalogue"/>; a typical value
/// a stockholder, database or handbook publishes on its own authority is
/// <see cref="ReferenceValueOrigin.EngineeringReference"/>. The helpers
/// below exist so that the origin is chosen once per value and never
/// defaulted.
/// </para>
/// <para>
/// <b>Units are the source's own.</b> A value published in ksi is recorded
/// in psi (a decimal prefix, not a conversion), a density published in
/// lb/in3 is recorded in lb/in3, a CTE published per degree Fahrenheit is
/// recorded per degree Fahrenheit. The platform's unit system converts on
/// read; transcription never does.
/// </para>
/// </remarks>
public sealed partial class MaterialSeed
{
    private static ReferenceQuantityValue Val(object quantity, ReferenceValueOrigin origin, string conditions, string? designation = null) =>
        new(quantity, origin, conditions, designation);

    /// <summary>A limit the cited standard sets, as the source restates it.</summary>
    private static ReferenceQuantityValue Lim(object quantity, string conditions, string designation) =>
        new(quantity, ReferenceValueOrigin.Standard, conditions, designation);

    /// <summary>A typical value the source publishes on its own authority.</summary>
    private static ReferenceQuantityValue Typ(object quantity, string conditions, string? designation = null) =>
        new(quantity, ReferenceValueOrigin.EngineeringReference, conditions, designation);

    /// <summary>A value a manufacturer publishes for its own product.</summary>
    private static ReferenceQuantityValue Mfr(object quantity, string conditions, string? designation = null) =>
        new(quantity, ReferenceValueOrigin.ManufacturerCatalogue, conditions, designation);

    /// <summary>A design value the source tabulates from a design code (Eurocode).</summary>
    private static ReferenceQuantityValue Design(object quantity, string conditions) =>
        new(quantity, ReferenceValueOrigin.EngineeringReference, conditions);

    /// <summary>A value taken from a document other than the record's own principal source — that document is named in the conditions.</summary>
    private static ReferenceQuantityValue SupplementaryValue(object quantity, string sourceAndConditions, ReferenceValueOrigin origin) =>
        new(quantity, origin, "SUPPLEMENTARY SOURCE: " + sourceAndConditions);

    private static Quantity<Pressure> MPa(double value) => new(value, PressureUnits.Megapascal);

    private static Quantity<Pressure> GPa(double value) => new(value, PressureUnits.Gigapascal);

    /// <summary>A value published in ksi, recorded in psi (ksi x 1000).</summary>
    private static Quantity<Pressure> Ksi(double value) => new(value * 1000.0, PressureUnits.Psi);

    private static Quantity<MassDensity> GramPerCc(double value) => new(value, MassDensityUnits.GramPerCubicCentimetre);

    private static Quantity<MassDensity> KgPerM3(double value) => new(value, MassDensityUnits.KilogramPerCubicMetre);

    private static Quantity<MassDensity> LbPerIn3(double value) => new(value, MassDensityUnits.PoundPerCubicInch);

    private static Quantity<ThermalExpansion> MicroPerK(double value) => new(value, ThermalExpansionUnits.MicrometrePerMetreKelvin);

    private static Quantity<ThermalExpansion> MicroPerF(double value) => new(value, ThermalExpansionUnits.MicroinchPerInchDegreeFahrenheit);

    private static Quantity<ThermalConductivity> WPerMK(double value) => new(value, ThermalConductivityUnits.WattPerMetreKelvin);

    private static Quantity<ThermalConductivity> BtuPerHFtF(double value) => new(value, ThermalConductivityUnits.BtuPerHourFootDegreeFahrenheit);

    private static Quantity<SpecificHeatCapacity> JPerKgK(double value) => new(value, SpecificHeatCapacityUnits.JoulePerKilogramKelvin);

    private static Quantity<Dimensionless> Pct(double value) => new(value, DimensionlessUnits.Percent);

    private static Quantity<Dimensionless> Ratio(double value) => new(value, DimensionlessUnits.One);

    private static Quantity<Temperature> DegC(double value) => new(value, TemperatureUnits.DegreeCelsius);

    private static Quantity<Energy> Joule(double value) => new(value, EnergyUnits.Joule);

    private static ReferenceSeedRecord<MaterialDefinition> Mat(
        string recordId,
        MaterialDefinition definition,
        ReferenceProvenance provenance,
        string publisher,
        string work,
        string table) =>
        new(recordId, definition, provenance, new SourceCitation(publisher, work, TableOrFigure: table));

    private static Dictionary<string, ReferenceQuantityValue> Props(params (string Name, ReferenceQuantityValue Value)[] values)
    {
        var properties = new Dictionary<string, ReferenceQuantityValue>(StringComparer.Ordinal);
        foreach (var (name, value) in values)
            properties.Add(name, value);

        return properties;
    }

    private const string Density = MaterialPropertyNames.Density;
    private const string Modulus = MaterialPropertyNames.YoungsModulus;
    private const string Shear = MaterialPropertyNames.ShearModulus;
    private const string Poisson = MaterialPropertyNames.PoissonsRatio;
    private const string Yield = MaterialPropertyNames.YieldStrength;
    private const string Uts = MaterialPropertyNames.UltimateTensileStrength;
    private const string Elongation = MaterialPropertyNames.ElongationAtBreak;
    private const string Impact = MaterialPropertyNames.ImpactEnergy;
    private const string Cte = MaterialPropertyNames.ThermalExpansionCoefficient;
    private const string Conductivity = MaterialPropertyNames.ThermalConductivity;
    private const string HeatCapacity = MaterialPropertyNames.SpecificHeatCapacity;
    private const string Fatigue = MaterialPropertyNames.FatigueStrength;
    private const string Melting = MaterialPropertyNames.MeltingPoint;
    private const string MaxService = MaterialPropertyNames.MaximumServiceTemperature;
    private const string MinService = MaterialPropertyNames.MinimumServiceTemperature;
    private const string Compressive = MaterialPropertyNames.CompressiveStrength;

    /// <summary>The property name a Shore D hardness number is recorded under. See <see cref="BrinellHardness"/>.</summary>
    public const string ShoreDHardness = "ShoreDHardness";

    /// <summary>The property name a Rockwell C hardness number is recorded under. See <see cref="BrinellHardness"/>.</summary>
    public const string RockwellCHardness = "RockwellCHardness";
}
