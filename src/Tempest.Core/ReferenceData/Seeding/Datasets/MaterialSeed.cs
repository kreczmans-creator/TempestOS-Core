using Tempest.Core.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// The shipped materials library: 77 engineering materials across
/// structural, engineering and stainless steels, aluminium, copper, cast
/// iron, titanium, nickel and magnesium alloys and thermoplastics, every
/// value traced to a named, addressable source (PO decision 2026-10-01).
/// </summary>
/// <remarks>
/// <para>
/// <b>Usable from day one, and honest about how.</b> The Product Owner ruled
/// that the libraries must be fully usable from day one. Every record
/// therefore carries every property the calculators read — density,
/// Young's modulus, yield or 0.2% proof strength, tensile strength and
/// thermal expansion — except where no source read publishes one; those
/// gaps are named in the record's own notes and pinned by
/// <c>DayOneReferenceLibraryTests</c>, so a calculator refuses rather than
/// using a borrowed figure. The dataset is released at seed
/// (<see cref="ReleaseAtSeed"/>) through the governed review path.
/// </para>
/// <para>
/// <b>Every property carries the condition it holds under.</b> A minimum
/// proof stress for 6082-T6 is a minimum for a stated product form in a
/// stated size band, and quoting it without that band would be quoting a
/// different number than the one published. Where a source gave a range,
/// the record holds the limit that governs design and states the whole
/// range in the value's own conditions, because
/// <see cref="MaterialDefinition.Properties"/> holds points rather than
/// ranges and silently keeping only one end would hide that a range
/// existed.
/// </para>
/// <para>
/// <b>Where the value came from is recorded per value, not per record.</b>
/// A datasheet's mechanical minima are the cited standard's
/// (<see cref="ReferenceValueOrigin.Standard"/>); its density and modulus
/// are typical figures the stockholder published on its own authority
/// (<see cref="ReferenceValueOrigin.EngineeringReference"/>); a
/// manufacturer's own product figures are
/// <see cref="ReferenceValueOrigin.ManufacturerCatalogue"/>. A value taken
/// from a document other than the record's principal source begins
/// "SUPPLEMENTARY SOURCE:" and names that document and its address.
/// </para>
/// <para>
/// <b>Source defects are recorded, not repaired.</b> See
/// <c>mat-5083-o-h111</c>: its own datasheet states a density that is
/// physically impossible; the record carries the same publisher's figure
/// for the same alloy from another of its datasheets, cited on the value,
/// and says why. The Seed Data Sources Register
/// (<c>docs/governance/Data/Seed Data Sources Register.md</c>) lists every
/// source and every defect.
/// </para>
/// </remarks>
public sealed partial class MaterialSeed : IReferenceSeed<MaterialDefinition>
{
    /// <summary>The identity of the S355J2 structural steel record.</summary>
    public const string S355J2 = "mat-s355j2";

    /// <summary>The identity of the 1.4301 (304) austenitic stainless steel record.</summary>
    public const string Stainless1Point4301 = "mat-1-4301";

    /// <summary>The identity of the 1.4404 (316L) austenitic stainless steel record.</summary>
    public const string Stainless1Point4404 = "mat-1-4404";

    /// <summary>The identity of the 6082-T6 wrought aluminium record.</summary>
    public const string Aluminium6082T6 = "mat-6082-t6";

    /// <summary>The identity of the 5083-O/H111 wrought aluminium record.</summary>
    public const string Aluminium5083OH111 = "mat-5083-o-h111";

    /// <summary>The identity of the CW004A copper record.</summary>
    public const string CopperCw004A = "mat-cw004a";

    /// <summary>The single instance of this dataset.</summary>
    public static MaterialSeed Instance { get; } = new();

    private MaterialSeed()
    {
    }

    /// <inheritdoc />
    /// <remarks>Shipped reference data under the PO decision of 2026-10-01: released at seed by the host's seeder.</remarks>
    public bool ReleaseAtSeed => true;

    /// <inheritdoc />
    public string DatasetName => "Engineering materials — day-one library (PO decision 2026-10-01)";

    /// <inheritdoc />
    /// <remarks>Revision 2 is the day-one expansion; revision 1 was the six-grade representative set.</remarks>
    public int DatasetRevision => 2;

    /// <inheritdoc />
    public IReadOnlyList<ReferenceSeedRecord<MaterialDefinition>> Records { get; } =
    [
        .. FirstAcquisition(),
        .. StructuralSteels(),
        .. EngineeringSteels(),
        .. StainlessSteels(),
        .. AluminiumAlloys(),
        .. CopperAlloys(),
        .. CastIrons(),
        .. TitaniumAndNickelAlloys(),
        .. MagnesiumAlloys(),
        .. Polymers(),
    ];

    private static IEnumerable<ReferenceSeedRecord<MaterialDefinition>> FirstAcquisition() =>
    [
        new(S355J2,
            new MaterialDefinition
            {
                Name = "S355J2 non-alloy structural steel",
                Family = MaterialFamily.Steel,
                Designation = "S355J2",
                Grade = "1.0577",
                SourceClassification = "Non-alloy structural steel",
                Standards =
                [
                    new StandardReference("EN 10025-2", StandardSeed.En10025Part2, "CEN", "2019",
                        "Mechanical property limits and impact requirements"),
                ],
                Properties = new Dictionary<string, ReferenceQuantityValue>
                {
                    [MaterialPropertyNames.YieldStrength] = Standard(
                        new Quantity<Pressure>(355.0, PressureUnits.Megapascal),
                        "Minimum upper yield strength ReH for nominal thickness t <= 16 mm. The standard bands "
                        + "this down with thickness: 345 MPa to 40 mm, 335 MPa to 63 mm, 325 MPa to 80 mm, "
                        + "315 MPa to 100 mm.",
                        "ReH min"),
                    [MaterialPropertyNames.UltimateTensileStrength] = Standard(
                        new Quantity<Pressure>(470.0, PressureUnits.Megapascal),
                        "Lower limit of the specified range 470 to 630 MPa, for nominal thickness t <= 16 mm.",
                        "Rm"),
                    [MaterialPropertyNames.ElongationAtBreak] = Standard(
                        new Quantity<Dimensionless>(22.0, DimensionlessUnits.Percent),
                        "Minimum, for nominal thickness t <= 16 mm.",
                        "A% min"),
                    [MaterialPropertyNames.ImpactEnergy] = Standard(
                        new Quantity<Energy>(27.0, EnergyUnits.Joule),
                        "Charpy V-notch minimum absorbed energy at -20 degC. This is what the J2 suffix denotes "
                        + "and is the reason to specify J2 over JR or J0.",
                        "KV at -20 degC"),
                    [MaterialPropertyNames.Density] = Typical(
                        new Quantity<MassDensity>(7.85, MassDensityUnits.GramPerCubicCentimetre)),
                    [MaterialPropertyNames.YoungsModulus] = Typical(
                        new Quantity<Pressure>(210.0, PressureUnits.Gigapascal)),
                    [MaterialPropertyNames.ThermalExpansionCoefficient] = Typical(
                        new Quantity<ThermalExpansion>(12e-6, ThermalExpansionUnits.PerKelvin)),
                    [MaterialPropertyNames.ShearModulus] = Design(
                        new Quantity<Pressure>(81.0, PressureUnits.Gigapascal),
                        "Shear modulus G as the source tabulates it, citing EN 1993-1-1."),
                    [MaterialPropertyNames.ThermalConductivity] = Design(
                        new Quantity<ThermalConductivity>(53.3, ThermalConductivityUnits.WattPerMetreKelvin),
                        "At 20 degC, as the source tabulates it, citing EN 1993-1-2. Falls with temperature."),
                    [MaterialPropertyNames.SpecificHeatCapacity] = Design(
                        new Quantity<SpecificHeatCapacity>(440.0, SpecificHeatCapacityUnits.JoulePerKilogramKelvin),
                        "At 20 degC, as the source tabulates it, citing EN 1993-1-2."),
                    [MaterialPropertyNames.PoissonsRatio] = SupplementaryValue(
                        new Quantity<Dimensionless>(0.3, DimensionlessUnits.One),
                        "Siderticino SA, S235JR steel datasheet (https://siderticino.it/en/steel-datasheets/s235jr/), "
                        + "which gives nu ~ 0.3 for hot-rolled non-alloy structural steels; the S355J2 page itself "
                        + "states no Poisson's ratio.",
                        ReferenceValueOrigin.EngineeringReference),
                    [MaterialPropertyNames.FatigueStrength] = SupplementaryValue(
                        new Quantity<Pressure>(160.0, PressureUnits.Megapascal),
                        "Siderticino SA, S355 steel datasheet (https://siderticino.it/en/steel-datasheets/s355j2/, "
                        + "retrieved 2026-10-01): base-material fatigue limit 'of about 160-180 MPa' "
                        + "for alternating stress at 2x10^6 cycles, citing EN 1993-1-9. Lower end of the stated range. "
                        + "Unwelded parent metal only: welded and notched details are governed by the EN 1993-1-9 "
                        + "detail categories, which are far lower.",
                        ReferenceValueOrigin.EngineeringReference),
                },
                EnvironmentalNotes = "No corrosion resistance. Requires protective coating or an allowance for "
                    + "corrosion loss in any exposed application. The source datasheet gives no corrosion data.",
                Notes = "Chemical composition limits stated by the source and not modelled here (the material "
                    + "record holds properties, not composition): C <= 0.22%, Mn <= 1.60%, Si <= 0.55%, "
                    + "P <= 0.025%, S <= 0.025%, CEV <= 0.45% for t <= 30 mm. Delivery condition is not stated "
                    + "by the source and is therefore not recorded. Shear modulus, thermal conductivity, specific heat, "
                    + "Poisson's ratio and fatigue limit were added from a re-read of the same publisher's pages on "
                    + "2026-10-01 (day-one acquisition, PO decision 2026-10-01); the S355 page tabulates the first three "
                    + "against EN 1993-1-1/-1-2.",
            },
            SeedSources.Siderticino(
                "S355J2 (S355) technical specifications - Non-alloy structural steels",
                "Mechanical properties by thickness table and physical properties list"),
            new SourceCitation("Siderticino SA", "S355J2 (S355) technical specifications - Non-alloy structural steels",
                TableOrFigure: "Mechanical properties by thickness table")),

        new(Stainless1Point4301,
            new MaterialDefinition
            {
                Name = "1.4301 (304) austenitic stainless steel",
                Family = MaterialFamily.StainlessSteel,
                Designation = "1.4301",
                Grade = "304",
                SourceClassification = "Stainless Steel - Austenitic",
                Standards =
                [
                    new StandardReference("EN 10088-3", StandardSeed.En10088Part3, "CEN", "2005",
                        "Mechanical property limits for bar and section"),
                ],
                Properties = new Dictionary<string, ReferenceQuantityValue>
                {
                    [MaterialPropertyNames.YieldStrength] = Standard(
                        new Quantity<Pressure>(190.0, PressureUnits.Megapascal),
                        "Minimum proof stress, bar and section up to 160 mm diameter or thickness.",
                        "Proof Stress min"),
                    [MaterialPropertyNames.UltimateTensileStrength] = Standard(
                        new Quantity<Pressure>(500.0, PressureUnits.Megapascal),
                        "Lower limit of the specified range 500 to 700 MPa, bar and section up to 160 mm.",
                        "Tensile Strength"),
                    [MaterialPropertyNames.ElongationAtBreak] = Standard(
                        new Quantity<Dimensionless>(45.0, DimensionlessUnits.Percent),
                        "Minimum A50mm, bar and section up to 160 mm.",
                        "Elongation A50mm min"),
                    [BrinellHardness] = Standard(
                        new Quantity<Dimensionless>(215.0, DimensionlessUnits.One),
                        "Maximum Brinell hardness number, bar and section up to 160 mm. Stored as the bare "
                        + "number the source quotes: this platform models no hardness dimension, and inventing "
                        + "one to hold a single value would be the dataset dictating the unit system.",
                        "HB max"),
                    [MaterialPropertyNames.Density] = Typical(
                        new Quantity<MassDensity>(8.00, MassDensityUnits.GramPerCubicCentimetre)),
                    [MaterialPropertyNames.MeltingPoint] = Typical(
                        new Quantity<Temperature>(1450.0, TemperatureUnits.DegreeCelsius)),
                    [MaterialPropertyNames.YoungsModulus] = Typical(
                        new Quantity<Pressure>(193.0, PressureUnits.Gigapascal)),
                    [MaterialPropertyNames.ThermalExpansionCoefficient] = Typical(
                        new Quantity<ThermalExpansion>(17.2e-6, ThermalExpansionUnits.PerKelvin)),
                    [MaterialPropertyNames.ThermalConductivity] = Typical(
                        new Quantity<ThermalConductivity>(16.2, ThermalConductivityUnits.WattPerMetreKelvin)),
                },
                EnvironmentalNotes = "Austenitic stainless. The source datasheet states no quantified corrosion "
                    + "data, so none is recorded; 1.4301 is not resistant to chloride pitting and 1.4404 is the "
                    + "usual answer where chlorides are present.",
                Notes = "Equivalent designations stated by the source: S30400, 304S15, 304S16, 304S31, EN58E. "
                    + "Electrical resistivity 0.72e-6 ohm.m is published by the source but not recorded: this "
                    + "platform models no electrical resistivity dimension. Delivery condition is not stated by "
                    + "the source and is therefore not recorded.",
            },
            SeedSources.Aalco(
                "Stainless Steel - Austenitic - 1.4301 (304) Bar and Section",
                "Mechanical properties (bar and section up to 160 mm) and physical properties tables"),
            new SourceCitation("Aalco Metals Limited", "Stainless Steel - Austenitic - 1.4301 (304) Bar and Section",
                TableOrFigure: "Mechanical properties table (bar and section up to 160 mm)")),

        new(Stainless1Point4404,
            new MaterialDefinition
            {
                Name = "1.4404 (316L) austenitic stainless steel",
                Family = MaterialFamily.StainlessSteel,
                Designation = "1.4404",
                Grade = "316L",
                SourceClassification = "Stainless Steel - Austenitic",
                Standards =
                [
                    new StandardReference("EN 10088-3", StandardSeed.En10088Part3, "CEN", "2005",
                        "Mechanical property limits for bar and section"),
                ],
                Properties = new Dictionary<string, ReferenceQuantityValue>
                {
                    [MaterialPropertyNames.YieldStrength] = Standard(
                        new Quantity<Pressure>(200.0, PressureUnits.Megapascal),
                        "Minimum proof stress, bar and section up to 160 mm diameter or thickness.",
                        "Proof Stress min"),
                    [MaterialPropertyNames.UltimateTensileStrength] = Standard(
                        new Quantity<Pressure>(500.0, PressureUnits.Megapascal),
                        "Lower limit of the specified range 500 to 700 MPa, bar and section up to 160 mm.",
                        "Tensile Strength"),
                    [MaterialPropertyNames.ElongationAtBreak] = Standard(
                        new Quantity<Dimensionless>(40.0, DimensionlessUnits.Percent),
                        "Minimum A50mm, bar and section up to 160 mm.",
                        "Elongation A50mm min"),
                    [BrinellHardness] = Standard(
                        new Quantity<Dimensionless>(215.0, DimensionlessUnits.One),
                        "Maximum Brinell hardness number, bar and section up to 160 mm. Stored as the bare "
                        + "number the source quotes; this platform models no hardness dimension.",
                        "HB max"),
                    [MaterialPropertyNames.Density] = Typical(
                        new Quantity<MassDensity>(8.0, MassDensityUnits.GramPerCubicCentimetre)),
                    [MaterialPropertyNames.MeltingPoint] = Typical(
                        new Quantity<Temperature>(1400.0, TemperatureUnits.DegreeCelsius)),
                    [MaterialPropertyNames.YoungsModulus] = Typical(
                        new Quantity<Pressure>(193.0, PressureUnits.Gigapascal)),
                    [MaterialPropertyNames.ThermalExpansionCoefficient] = Typical(
                        new Quantity<ThermalExpansion>(15.9e-6, ThermalExpansionUnits.PerKelvin)),
                    [MaterialPropertyNames.ThermalConductivity] = Typical(
                        new Quantity<ThermalConductivity>(16.3, ThermalConductivityUnits.WattPerMetreKelvin)),
                },
                EnvironmentalNotes = "Molybdenum-bearing low-carbon austenitic stainless. The source states no "
                    + "quantified corrosion data, so none is recorded.",
                Notes = "Equivalent designations stated by the source: UNS S31603, 316S11. Electrical "
                    + "resistivity 0.74e-6 ohm.m is published by the source but not recorded: this platform "
                    + "models no electrical resistivity dimension.",
            },
            SeedSources.Aalco(
                "Stainless Steel - Austenitic - 1.4404 (316L) Bar and Section",
                "Mechanical properties (bar and section up to 160 mm) and physical properties tables"),
            new SourceCitation("Aalco Metals Limited", "Stainless Steel - Austenitic - 1.4404 (316L) Bar and Section",
                TableOrFigure: "Mechanical properties table (bar and section up to 160 mm)")),

        new(Aluminium6082T6,
            new MaterialDefinition
            {
                Name = "6082-T6 wrought aluminium alloy",
                Family = MaterialFamily.Aluminium,
                Designation = "6082",
                Condition = "T6 (solution heat treated and artificially aged)",
                SourceClassification = "Aluminium Alloy - Commercial Alloy",
                Standards =
                [
                    new StandardReference("EN 573-3", StandardSeed.En573Part3, "CEN", "2009",
                        "Alloy designation system"),
                    new StandardReference("EN 755-2", StandardSeed.En755Part2, "CEN", "2008",
                        "Mechanical property limits for extruded rod, bar, tube and profiles"),
                ],
                Properties = new Dictionary<string, ReferenceQuantityValue>
                {
                    [MaterialPropertyNames.YieldStrength] = Standard(
                        new Quantity<Pressure>(260.0, PressureUnits.Megapascal),
                        "Minimum 0.2% proof stress, extruded rod and bar 20 mm to 150 mm diameter or across "
                        + "flats. The source bands this by size and form: 250 MPa up to 20 mm, 240 MPa for "
                        + "150 to 200 mm, 200 MPa for 200 to 250 mm.",
                        "0.2% Proof Stress min"),
                    [MaterialPropertyNames.UltimateTensileStrength] = Standard(
                        new Quantity<Pressure>(310.0, PressureUnits.Megapascal),
                        "Minimum, extruded rod and bar 20 mm to 150 mm diameter or across flats. The source "
                        + "bands this by size and form: 295 MPa up to 20 mm, 280 MPa for 150 to 200 mm, "
                        + "270 MPa for 200 to 250 mm.",
                        "Tensile Strength min"),
                    [MaterialPropertyNames.ElongationAtBreak] = Standard(
                        new Quantity<Dimensionless>(8.0, DimensionlessUnits.Percent),
                        "Minimum A, extruded rod and bar 20 mm to 150 mm diameter or across flats.",
                        "Elongation A min"),
                    [BrinellHardness] = Typical(
                        new Quantity<Dimensionless>(95.0, DimensionlessUnits.One),
                        "Brinell hardness number, quoted by the source as a single figure across every product "
                        + "form and size band without a limit qualifier. Stored as the bare number the source "
                        + "quotes; this platform models no hardness dimension."),
                    [MaterialPropertyNames.Density] = Typical(
                        new Quantity<MassDensity>(2.70, MassDensityUnits.GramPerCubicCentimetre)),
                    [MaterialPropertyNames.MeltingPoint] = Typical(
                        new Quantity<Temperature>(555.0, TemperatureUnits.DegreeCelsius)),
                    [MaterialPropertyNames.YoungsModulus] = Typical(
                        new Quantity<Pressure>(70.0, PressureUnits.Gigapascal)),
                    [MaterialPropertyNames.ThermalExpansionCoefficient] = Typical(
                        new Quantity<ThermalExpansion>(24e-6, ThermalExpansionUnits.PerKelvin)),
                    [MaterialPropertyNames.ThermalConductivity] = Typical(
                        new Quantity<ThermalConductivity>(180.0, ThermalConductivityUnits.WattPerMetreKelvin)),
                },
                ProcessingNotes = "The recorded properties are the extruded rod and bar values. The source "
                    + "publishes separate and different minima for tube and for open and hollow profiles, "
                    + "banded by wall thickness; a part made from those forms must be checked against them "
                    + "rather than against this record.",
                Notes = "Electrical resistivity 0.038e-6 ohm.m is published by the source but not recorded: "
                    + "this platform models no electrical resistivity dimension.",
            },
            SeedSources.Aalco(
                "Aluminium Alloy - Commercial Alloy - 6082 - T6 Extrusions",
                "Mechanical properties, rod and bar 20 mm to 150 mm band, and physical properties table"),
            new SourceCitation("Aalco Metals Limited", "Aluminium Alloy - Commercial Alloy - 6082 - T6 Extrusions",
                TableOrFigure: "Mechanical properties table (rod and bar 20 mm to 150 mm)")),

        new(Aluminium5083OH111,
            new MaterialDefinition
            {
                Name = "5083-O/H111 wrought aluminium alloy",
                Family = MaterialFamily.Aluminium,
                Designation = "5083",
                Condition = "O / H111 (annealed / annealed and slightly work hardened)",
                SourceClassification = "Aluminium Alloy - Commercial Alloy",
                Standards =
                [
                    new StandardReference("EN 573-3", StandardSeed.En573Part3, "CEN", "2019",
                        "Alloy designation system"),
                    new StandardReference("EN 485-2", StandardSeed.En485Part2, "CEN", "2008",
                        "Mechanical property limits for sheet and plate"),
                ],
                Properties = new Dictionary<string, ReferenceQuantityValue>
                {
                    [MaterialPropertyNames.YieldStrength] = Standard(
                        new Quantity<Pressure>(115.0, PressureUnits.Megapascal),
                        "Minimum proof stress, plate 6.3 mm to 80 mm thick. The source bands this by thickness: "
                        + "125 MPa for sheet 0.2 to 6.3 mm, 110 MPa for plate 80 to 120 mm.",
                        "Proof Stress min"),
                    [MaterialPropertyNames.UltimateTensileStrength] = Standard(
                        new Quantity<Pressure>(270.0, PressureUnits.Megapascal),
                        "Lower limit of the specified range 270 to 345 MPa, plate 6.3 mm to 80 mm thick.",
                        "Tensile Strength"),
                    [BrinellHardness] = Typical(
                        new Quantity<Dimensionless>(75.0, DimensionlessUnits.One),
                        "Brinell hardness number for sheet and plate up to 80 mm, quoted by the source without "
                        + "a limit qualifier. Stored as the bare number the source quotes."),
                    [MaterialPropertyNames.MeltingPoint] = Typical(
                        new Quantity<Temperature>(570.0, TemperatureUnits.DegreeCelsius)),
                    [MaterialPropertyNames.Density] = SupplementaryValue(
                        new Quantity<MassDensity>(2.65, MassDensityUnits.GramPerCubicCentimetre),
                        "Aalco Metals Limited, 'Aluminium Alloy - Commercial Alloy - 5083 - H32 Sheet' datasheet "
                        + "(https://www.aalco.co.uk/datasheets/Aluminium-Alloy-5083-H32-Sheet_140.ashx), physical "
                        + "properties table. Density is a property of alloy 5083, not of its temper; this record's own "
                        + "O/H111 datasheet misprints it (see Notes), so the same publisher's H32 sheet is cited instead.",
                        ReferenceValueOrigin.EngineeringReference),
                    [MaterialPropertyNames.YoungsModulus] = Typical(
                        new Quantity<Pressure>(72.0, PressureUnits.Gigapascal)),
                    [MaterialPropertyNames.ThermalExpansionCoefficient] = Typical(
                        new Quantity<ThermalExpansion>(25e-6, ThermalExpansionUnits.PerKelvin)),
                    [MaterialPropertyNames.ThermalConductivity] = Typical(
                        new Quantity<ThermalConductivity>(121.0, ThermalConductivityUnits.WattPerMetreKelvin)),
                },
                EnvironmentalNotes = "A marine-grade magnesium alloy, non-heat-treatable, chosen for seawater "
                    + "corrosion resistance and weldability rather than strength. The source states no "
                    + "quantified corrosion data, so none is recorded.",
                Notes = "DENSITY NOT TAKEN FROM THIS RECORD'S OWN DATASHEET. The O/H111 datasheet prints "
                    + "'Density 265 g/cm3', which is physically impossible — some twelve times the density of osmium — "
                    + "and is evidently a misplaced decimal point. Two independent reads of the page returned the "
                    + "same figure, so it is the source's own error and not a transcription fault. Rather than "
                    + "correcting it by assumption, the density recorded is the 2.65 g/cm3 the same publisher prints "
                    + "for the same alloy on its 5083-H32 sheet datasheet, which is cited on the value itself "
                    + "(day-one acquisition, 2026-10-01). Elongation is recorded only for the "
                    + "80 to 120 mm band by the source (12% min) and so is not recorded against this 6.3 to "
                    + "80 mm record. Electrical resistivity 0.058e-6 ohm.m is published but not recorded: this "
                    + "platform models no electrical resistivity dimension.",
            },
            SeedSources.Aalco(
                "Aluminium Alloy - Commercial Alloy - 5083 - '0' - H111 Sheet and Plate",
                "Mechanical properties, plate 6.3 mm to 80 mm band, and physical properties table"),
            new SourceCitation("Aalco Metals Limited", "Aluminium Alloy - Commercial Alloy - 5083 - '0' - H111 Sheet and Plate",
                TableOrFigure: "Mechanical properties table (plate 6.3 mm to 80 mm)")),

        new(CopperCw004A,
            new MaterialDefinition
            {
                Name = "CW004A (C101) electrolytic tough pitch copper",
                Family = MaterialFamily.CopperAlloy,
                Designation = "CW004A",
                Grade = "C101",
                SourceClassification = "Copper and Copper Alloys - Copper (Pure)",
                Standards =
                [
                    new StandardReference("EN 1652", StandardSeed.En1652, "CEN", "1997",
                        "Mechanical property limits for sheet and plate"),
                    new StandardReference("EN 13601", StandardSeed.En13601, "CEN", null,
                        "Rod, bar and section. The source cites it but publishes no values against it."),
                ],
                Properties = new Dictionary<string, ReferenceQuantityValue>
                {
                    [MaterialPropertyNames.YieldStrength] = Standard(
                        new Quantity<Pressure>(50.0, PressureUnits.Megapascal),
                        "Lowest proof stress in the range 50 to 340 MPa the source publishes for sheet 0.2 to "
                        + "15 mm. The range spans tempers from annealed to hard, so this figure describes the "
                        + "annealed condition and is not a property of the material irrespective of temper.",
                        "Proof Stress"),
                    [MaterialPropertyNames.UltimateTensileStrength] = Standard(
                        new Quantity<Pressure>(200.0, PressureUnits.Megapascal),
                        "Lowest tensile strength in the range 200 to 360 MPa the source publishes for sheet "
                        + "0.2 to 15 mm, again spanning tempers.",
                        "Tensile Strength"),
                    [MaterialPropertyNames.ElongationAtBreak] = Standard(
                        new Quantity<Dimensionless>(5.0, DimensionlessUnits.Percent),
                        "Lowest elongation A50mm in the range the source publishes (50% annealed falling to 5% "
                        + "hard). Recorded at the hard end because that is the limiting case for formability.",
                        "Elongation A50mm"),
                    [VickersHardness] = Standard(
                        new Quantity<Dimensionless>(40.0, DimensionlessUnits.One),
                        "Lowest Vickers hardness number in the range 40 to 110 HV the source publishes across "
                        + "tempers. Stored as the bare number the source quotes.",
                        "HV"),
                    [MaterialPropertyNames.Density] = Typical(
                        new Quantity<MassDensity>(8.92, MassDensityUnits.GramPerCubicCentimetre)),
                    [MaterialPropertyNames.MeltingPoint] = Typical(
                        new Quantity<Temperature>(1083.0, TemperatureUnits.DegreeCelsius)),
                    [MaterialPropertyNames.YoungsModulus] = Typical(
                        new Quantity<Pressure>(117.0, PressureUnits.Gigapascal)),
                    [MaterialPropertyNames.ThermalExpansionCoefficient] = Typical(
                        new Quantity<ThermalExpansion>(16.9e-6, ThermalExpansionUnits.PerKelvin)),
                    [MaterialPropertyNames.ThermalConductivity] = Typical(
                        new Quantity<ThermalConductivity>(391.1, ThermalConductivityUnits.WattPerMetreKelvin)),
                },
                ProcessingNotes = "The published mechanical figures span the full temper range for sheet, so a "
                    + "design relying on strength must specify a temper and be checked against that temper's "
                    + "own limits rather than against this record.",
                Notes = "Equivalent designations stated by the source: C101/CW004A HC Copper, UNS C11000, "
                    + "ISO Cu-ETP. Electrical resistivity 0.0171e-6 ohm.m (100% IACS) is published by the "
                    + "source but not recorded: this platform models no electrical resistivity dimension, and "
                    + "for this material that is the property most users would want — a real gap, recorded "
                    + "rather than worked around.",
            },
            SeedSources.Aalco(
                "Copper and Copper Alloys - Copper (Pure) - CW004A Sheet, Plate and Bar",
                "Mechanical properties (sheet 0.2 to 15 mm) and physical properties tables"),
            new SourceCitation("Aalco Metals Limited", "Copper and Copper Alloys - Copper (Pure) - CW004A Sheet, Plate and Bar",
                TableOrFigure: "Mechanical properties table (sheet 0.2 to 15 mm)")),
    ];

    /// <summary>
    /// The property name a Brinell hardness number is recorded under.
    /// </summary>
    /// <remarks>
    /// Not one of <see cref="MaterialPropertyNames"/>'s well-known names,
    /// and deliberately so: that vocabulary pairs each name with a
    /// dimension it must carry, and hardness has no dimension in this
    /// platform's unit system. Recording it under an unrecognised name is
    /// exactly what <c>ADR-0055</c> left the vocabulary open for.
    /// </remarks>
    public const string BrinellHardness = "BrinellHardness";

    /// <summary>The property name a Vickers hardness number is recorded under. See <see cref="BrinellHardness"/>.</summary>
    public const string VickersHardness = "VickersHardness";

    private static ReferenceQuantityValue Standard(object value, string conditions, string sourceDesignation) =>
        new(value, ReferenceValueOrigin.Standard, conditions, sourceDesignation);

    private static ReferenceQuantityValue Typical(object value, string? conditions = null) =>
        new(value, ReferenceValueOrigin.EngineeringReference,
            conditions ?? "A typical value published by the stockholder on its own authority, not a limit set "
                + "by any standard the datasheet cites.");
}
