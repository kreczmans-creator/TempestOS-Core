using Tempest.Core.Constants;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// A seed set of fundamental physical constants, transcribed from the 2022
/// CODATA adjustment as NIST publishes it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these twelve and not the whole table.</b> The CODATA listing
/// holds several hundred entries, most of which carry dimensions this
/// platform's unit system does not model — molar quantities, magnetic
/// moments, quantities per mole or per tesla. Recording those would mean
/// either extending the dimension system to satisfy a dataset (the tail
/// wagging the dog) or storing them dimensionlessly, which would be a lie
/// about what they are. So the seed holds exactly the constants that can
/// be represented honestly and that engineering work actually reaches for.
/// </para>
/// <para>
/// <b>Exact is recorded as exact.</b> Five of these are exact by
/// definition since the 2019 SI redefinition, and
/// <see cref="ConstantUncertaintyKind.Exact"/> says so — distinct both
/// from an uncertainty of zero and from nobody having recorded one.
/// </para>
/// <para>
/// <b>Every value stays in the unit NIST quoted it in.</b> No conversion
/// is applied on the way in, so the stored record is a faithful
/// transcription and any conversion a caller wants is the caller's, made
/// visibly, at the point of use.
/// </para>
/// </remarks>
public sealed class ConstantSeed : IReferenceSeed<ConstantDefinition>
{
    /// <summary>The single instance of this dataset.</summary>
    public static ConstantSeed Instance { get; } = new();

    private ConstantSeed()
    {
    }

    /// <inheritdoc />
    public string DatasetName => "NIST CODATA 2022 — representable fundamental constants";

    /// <inheritdoc />
    public int DatasetRevision => 1;

    /// <inheritdoc />
    public IReadOnlyList<ReferenceSeedRecord<ConstantDefinition>> Records { get; } =
    [
        Exact("const-speed-of-light", "c", "Speed of light in vacuum",
            new Quantity<Velocity>(299_792_458.0, VelocityUnits.MetrePerSecond),
            ConstantCategory.Universal,
            "Fixed by the 2019 SI redefinition, which defines the metre in terms of this value.",
            "speed of light in vacuum"),

        Exact("const-standard-gravity", "g_n", "Standard acceleration of gravity",
            new Quantity<Acceleration>(9.806_65, AccelerationUnits.MetrePerSecondSquared),
            ConstantCategory.ConventionalReference,
            "A conventional reference value adopted by the CGPM, not a measurement of local gravity. "
            + "Local acceleration due to gravity varies with latitude and altitude and is not this number.",
            "standard acceleration of gravity"),

        Exact("const-standard-atmosphere", "atm", "Standard atmosphere",
            new Quantity<Pressure>(101_325.0, PressureUnits.Pascal),
            ConstantCategory.ConventionalReference,
            "A defined reference pressure, not a measurement of any actual atmosphere.",
            "standard atmosphere"),

        Exact("const-electron-volt", "eV", "Electron volt",
            new Quantity<Energy>(1.602_176_634e-19, EnergyUnits.Joule),
            ConstantCategory.ConversionFactor,
            "Exact since the 2019 SI redefinition fixed the elementary charge.",
            "electron volt"),

        Measured("const-electron-mass", "m_e", "Electron mass",
            new Quantity<Mass>(9.109_383_7139e-31, MassUnits.Kilogram),
            new Quantity<Mass>(0.000_000_0028e-31, MassUnits.Kilogram),
            ConstantCategory.AtomicAndNuclear,
            "electron mass"),

        Measured("const-proton-mass", "m_p", "Proton mass",
            new Quantity<Mass>(1.672_621_925_95e-27, MassUnits.Kilogram),
            new Quantity<Mass>(0.000_000_000_52e-27, MassUnits.Kilogram),
            ConstantCategory.AtomicAndNuclear,
            "proton mass"),

        Measured("const-atomic-mass-constant", "m_u", "Atomic mass constant",
            new Quantity<Mass>(1.660_539_068_92e-27, MassUnits.Kilogram),
            new Quantity<Mass>(0.000_000_000_52e-27, MassUnits.Kilogram),
            ConstantCategory.AtomicAndNuclear,
            "atomic mass constant"),

        Measured("const-bohr-radius", "a_0", "Bohr radius",
            new Quantity<Length>(5.291_772_105_44e-11, LengthUnits.Metre),
            new Quantity<Length>(0.000_000_000_82e-11, LengthUnits.Metre),
            ConstantCategory.AtomicAndNuclear,
            "Bohr radius"),

        Measured("const-compton-wavelength", "lambda_C", "Compton wavelength",
            new Quantity<Length>(2.426_310_235_38e-12, LengthUnits.Metre),
            new Quantity<Length>(0.000_000_000_76e-12, LengthUnits.Metre),
            ConstantCategory.AtomicAndNuclear,
            "Compton wavelength"),

        Measured("const-classical-electron-radius", "r_e", "Classical electron radius",
            new Quantity<Length>(2.817_940_3205e-15, LengthUnits.Metre),
            new Quantity<Length>(0.000_000_0013e-15, LengthUnits.Metre),
            ConstantCategory.AtomicAndNuclear,
            "classical electron radius"),

        Measured("const-fine-structure-constant", "alpha", "Fine-structure constant",
            new Quantity<Dimensionless>(7.297_352_5643e-3, DimensionlessUnits.One),
            new Quantity<Dimensionless>(0.000_000_0011e-3, DimensionlessUnits.One),
            ConstantCategory.Electromagnetic,
            "fine-structure constant"),

        Measured("const-proton-electron-mass-ratio", "m_p/m_e", "Proton-electron mass ratio",
            new Quantity<Dimensionless>(1836.152_673_426, DimensionlessUnits.One),
            new Quantity<Dimensionless>(0.000_000_032, DimensionlessUnits.One),
            ConstantCategory.AtomicAndNuclear,
            "proton-electron mass ratio"),
    ];

    private static ReferenceSeedRecord<ConstantDefinition> Exact(
        string recordId,
        string symbol,
        string name,
        object value,
        ConstantCategory category,
        string applicability,
        string row) =>
        new(recordId,
            new ConstantDefinition
            {
                Symbol = symbol,
                Name = name,
                Value = new ReferenceQuantityValue(value, ReferenceValueOrigin.EngineeringReference, sourceDesignation: row),
                Uncertainty = new ConstantUncertainty(
                    ConstantUncertaintyKind.Exact,
                    Notes: "The CODATA listing marks this value '(exact)'."),
                Category = category,
                Applicability = applicability,
                SourceClassification = "CODATA fundamental physical constant",
            },
            SeedSources.NistCodata2022(row),
            new SourceCitation(
                "National Institute of Standards and Technology (NIST)",
                "CODATA Internationally Recommended Values of the Fundamental Physical Constants — Complete Listing (allascii.txt)",
                Edition: "2022 CODATA adjustment",
                RowOrEntry: row));

    private static ReferenceSeedRecord<ConstantDefinition> Measured(
        string recordId,
        string symbol,
        string name,
        object value,
        object standardUncertainty,
        ConstantCategory category,
        string row) =>
        new(recordId,
            new ConstantDefinition
            {
                Symbol = symbol,
                Name = name,
                Value = new ReferenceQuantityValue(value, ReferenceValueOrigin.EngineeringReference, sourceDesignation: row),
                Uncertainty = new ConstantUncertainty(
                    ConstantUncertaintyKind.Standard,
                    Absolute: new ReferenceQuantityValue(standardUncertainty, ReferenceValueOrigin.EngineeringReference),
                    Notes: "The standard uncertainty as the CODATA listing states it, at coverage factor one. "
                        + "The relative uncertainty is deliberately not recorded: CODATA publishes it separately "
                        + "and computing it here would invent a figure this source did not supply in this table."),
                Category = category,
                SourceClassification = "CODATA fundamental physical constant",
            },
            SeedSources.NistCodata2022(row),
            new SourceCitation(
                "National Institute of Standards and Technology (NIST)",
                "CODATA Internationally Recommended Values of the Fundamental Physical Constants — Complete Listing (allascii.txt)",
                Edition: "2022 CODATA adjustment",
                RowOrEntry: row));
}
