using Tempest.Core.Fasteners;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// ISO metric coarse-thread fasteners by property class: ISO 898-1 classes
/// 4.6, 5.6, 8.8, 10.9 and 12.9 from M3 to M36, and ISO 3506-1 A2-70, A4-70
/// and A4-80 from M3 to M24 — the records the bolted-joint calculator can
/// read a grade, stress area and proof strength from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Source.</b> Würth Industrie's technical handbook reproduces the ISO
/// standards' own tables as extracts (DIN EN ISO 898-1 Table 2 and the test-
/// force tables; DIN EN ISO 3506-1). Every value here is read from those
/// extracts; no value was computed. The proof load is the handbook's own
/// tabulated test force (As,nom x Sp), not a product this dataset formed.
/// </para>
/// <para>
/// <b>One record per size and class.</b> A class is a set of limits and a
/// size is a stress area; the bolted-joint calculation needs both, so the
/// record is the pair. Records name no head or length — they qualify the
/// threaded fastener, not a catalogue part.
/// </para>
/// <para>
/// <b>Stainless fasteners carry no proof stress.</b> ISO 3506-1 specifies
/// 0.2% proof stress and tensile strength for screws, not a proof stress, so
/// the A2/A4 records leave <see cref="FastenerMechanicalProperties.ProofStrength"/>
/// empty and the calculator asks for it by hand rather than reading a
/// borrowed figure.
/// </para>
/// </remarks>
public sealed partial class FastenerSeed
{
    private const string WuerthChapter1 =
        "https://www.wuerth-industrie.com/web/media/en/pictures/wuerthindustrie/technikportal/dinokapitel/Kapitel_01_DINO_techn_Teil.pdf";

    private const string WuerthChapter2 =
        "https://www.wuerth-industrie.com/web/media/en/pictures/wuerthindustrie/technikportal/dinokapitel/Kapitel_02_DINO_techn_Teil.pdf";

    // Nominal diameter, coarse pitch and nominal stress area As,nom, as the
    // handbook's own test-force tables (Tab. 3 and Tab. 6) give them.
    private static (int D, double Pitch, double StressArea)[] Sizes =>
    [
        (3, 0.5, 5.03), (4, 0.7, 8.78), (5, 0.8, 14.2), (6, 1.0, 20.1), (8, 1.25, 36.6), (10, 1.5, 58.0),
        (12, 1.75, 84.3), (14, 2.0, 115), (16, 2.0, 157), (18, 2.5, 192), (20, 2.5, 245), (22, 2.5, 303),
        (24, 3.0, 353), (27, 3.0, 459), (30, 3.5, 561), (33, 3.5, 694), (36, 4.0, 817),
    ];

    // Test force Fp (N) from Tab. 3, by size (same order as Sizes) for
    // classes 4.6, 5.6, 8.8, 10.9 and 12.9.
    private static double[][] TestForces =>
    [
        [1_130, 1_410, 2_920, 4_180, 4_880],
        [1_980, 2_460, 5_100, 7_290, 8_520],
        [3_200, 3_980, 8_230, 11_800, 13_800],
        [4_520, 5_630, 11_600, 16_700, 19_500],
        [8_240, 10_200, 21_200, 30_400, 35_500],
        [13_000, 16_200, 33_700, 48_100, 56_300],
        [19_000, 23_600, 48_900, 70_000, 81_800],
        [25_900, 32_200, 66_700, 95_500, 112_000],
        [35_300, 44_000, 91_000, 130_000, 152_000],
        [43_200, 53_800, 115_000, 159_000, 186_000],
        [55_100, 68_600, 147_000, 203_000, 238_000],
        [68_200, 84_800, 182_000, 252_000, 294_000],
        [79_400, 98_800, 212_000, 293_000, 342_000],
        [103_000, 128_000, 275_000, 381_000, 445_000],
        [126_000, 157_000, 337_000, 466_000, 544_000],
        [156_000, 194_000, 416_000, 576_000, 673_000],
        [184_000, 229_000, 490_000, 678_000, 792_000],
    ];

    private sealed record SteelClass(
        string Name,
        double RmMin,
        double YieldMin,
        string YieldSymbol,
        double ProofStress,
        double ElongationMin,
        double HvMin,
        double? HvMax);

    // Tab. 2 (extract from DIN EN ISO 898-1). 8.8 changes at d = 16 mm.
    private static SteelClass ClassFor(string name, int d) => name switch
    {
        "4.6" => new("4.6", 400, 240, "ReL", 225, 22, 120, null),
        "5.6" => new("5.6", 500, 300, "ReL", 280, 20, 155, null),
        "8.8" when d <= 16 => new("8.8", 800, 640, "Rp0.2", 580, 12, 250, 320),
        "8.8" => new("8.8", 830, 660, "Rp0.2", 600, 12, 255, 335),
        "10.9" => new("10.9", 1040, 940, "Rp0.2", 830, 9, 320, 380),
        "12.9" => new("12.9", 1220, 1100, "Rp0.2", 970, 8, 385, 435),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Not a seeded property class."),
    };

    private static string[] SteelClasses => ["4.6", "5.6", "8.8", "10.9", "12.9"];

    /// <summary>The record identity for a carbon or alloy steel fastener of size M<paramref name="d"/> in <paramref name="propertyClass"/>.</summary>
    /// <param name="d">The nominal diameter in millimetres.</param>
    /// <param name="propertyClass">The ISO 898-1 property class, e.g. "8.8".</param>
    /// <returns>The record identity.</returns>
    public static string PropertyClassRecordId(int d, string propertyClass) =>
        $"fst-m{d}-{propertyClass.Replace('.', '-').ToLowerInvariant()}";

    private static IEnumerable<ReferenceSeedRecord<FastenerDefinition>> CarbonSteelPropertyClasses()
    {
        for (var i = 0; i < Sizes.Length; i++)
        {
            var (d, pitch, area) = Sizes[i];

            for (var c = 0; c < SteelClasses.Length; c++)
            {
                var cls = ClassFor(SteelClasses[c], d);
                var proofLoad = TestForces[i][c];

                yield return new ReferenceSeedRecord<FastenerDefinition>(
                    PropertyClassRecordId(d, cls.Name),
                    new FastenerDefinition
                    {
                        Family = FastenerFamily.Bolt,
                        Designation = $"M{d} x {Format(pitch)} - {cls.Name}",
                        Thread = MetricThread(d, pitch, "Coarse pitch as the handbook's nut test-force table gives it (Tab. 6)."),
                        Mechanical = new FastenerMechanicalProperties(
                            PropertyClass: cls.Name,
                            ProofStrength: Mpa(cls.ProofStress, $"Nominal stress under proof load Sp,nom for class {cls.Name}"
                                + (cls.Name == "8.8" ? (d <= 16 ? ", d <= 16 mm" : ", d > 16 mm") : string.Empty)
                                + " (Tab. 2, row 5).", "Sp,nom"),
                            TensileStrength: Mpa(cls.RmMin, $"Minimum tensile strength Rm,min (Tab. 2, row 1).", "Rm,min"),
                            YieldStrength: Mpa(cls.YieldMin, cls.YieldSymbol == "ReL"
                                ? "Minimum lower yield strength ReL,min (Tab. 2, row 2)."
                                : "Minimum 0.2% offset yield strength Rp0.2,min (Tab. 2, row 3).", $"{cls.YieldSymbol},min"),
                            ProofLoad: new ReferenceValue<Force>(
                                new Quantity<Force>(proofLoad, ForceUnits.Newton),
                                ReferenceValueOrigin.Standard,
                                $"Test force Fp = As,nom x Sp for M{d} class {cls.Name}, as the handbook tabulates it (Tab. 3). "
                                + "Footnotes there reduce some M8/M10 values for hot-dip galvanised 6az threads (ISO 10684) "
                                + "and give different values for steel construction bolts M12-M16 in 8.8.",
                                "Fp"),
                            Hardness: new FastenerHardness("HV", cls.HvMin, cls.HvMax, ReferenceValueOrigin.Standard,
                                cls.HvMax is null
                                    ? "Vickers hardness minimum (Tab. 2, row 10); the maximum is tabulated across several classes and is not recorded."
                                    : "Vickers hardness range (Tab. 2, row 10)."),
                            ElongationAfterFracture: new ReferenceValue<Dimensionless>(
                                new Quantity<Dimensionless>(cls.ElongationMin, DimensionlessUnits.Percent),
                                ReferenceValueOrigin.Standard,
                                "Minimum percentage elongation after fracture of a turned-off specimen (Tab. 2, row 6).",
                                "A,min"),
                            StressArea: new ReferenceValue<Area>(
                                new Quantity<Area>(area, AreaUnits.SquareMillimetre),
                                ReferenceValueOrigin.Standard,
                                "Nominal stress area As,nom for the coarse thread (Tab. 3).",
                                "As,nom")),
                        Standards =
                        [
                            new StandardReference("ISO 898-1", StandardSeed.Iso898Part1, "ISO", null,
                                "Property-class limits and test forces, as the handbook extracts them"),
                            new StandardReference("ISO 262", StandardSeed.Iso262, "ISO", "1998", "Selected coarse pitch"),
                        ],
                        SourceClassification = "Bolts, screws and studs of carbon or alloy steel — ISO 898-1 property class",
                        Notes = $"Property class {cls.Name} at M{d}: a threaded-fastener qualification, not a catalogue part — "
                            + "no head, length or finish is implied. Valid at room temperature; the handbook tabulates "
                            + "reduced yield points at elevated temperature that are not recorded.",
                    },
                    SeedSources.Wuerth("Chapter 1, Steel fasteners for the temperature range between -50 degC and +150 degC",
                        WuerthChapter1,
                        "Tab. 2 (extract from DIN EN ISO 898-1, mechanical and physical properties of screws), Tab. 3 (test "
                        + "forces for ISO metric standard thread) and Tab. 6 (thread pitch)"),
                    new SourceCitation("Würth Industrie Service GmbH & Co. KG", "DINO technical handbook, chapter 1",
                        TableOrFigure: "Tab. 2 and Tab. 3", RowOrEntry: $"M{d}, class {cls.Name}"));
            }
        }
    }

    private static (string Name, string Group, double Rm, double Rp)[] StainlessClasses =>
    [
        ("A2-70", "A2", 700, 450),
        ("A4-70", "A4", 700, 450),
        ("A4-80", "A4", 800, 600),
    ];

    private static IEnumerable<ReferenceSeedRecord<FastenerDefinition>> StainlessPropertyClasses()
    {
        foreach (var (d, pitch, area) in Sizes.Where(s => s.D <= 24))
        {
            foreach (var (name, group, rm, rp) in StainlessClasses)
            {
                yield return new ReferenceSeedRecord<FastenerDefinition>(
                    $"fst-m{d}-{name.ToLowerInvariant()}",
                    new FastenerDefinition
                    {
                        Family = FastenerFamily.Bolt,
                        Designation = $"M{d} x {Format(pitch)} - {name}",
                        Thread = MetricThread(d, pitch, "Coarse pitch as the handbook's chapter 1 nut test-force table gives it."),
                        Mechanical = new FastenerMechanicalProperties(
                            PropertyClass: name,
                            TensileStrength: Mpa(rm, $"Minimum tensile strength Rm for strength class {name[^2..]} "
                                + $"(Tab. 16, extract from DIN EN ISO 3506-1); d <= 24 mm.", "Rm,min"),
                            YieldStrength: Mpa(rp, "Minimum 0.2% offset yield point Rp0.2 (Tab. 16); determined on whole "
                                + "screws because the strength comes partly from cold forming.", "Rp0.2,min"),
                            StressArea: new ReferenceValue<Area>(
                                new Quantity<Area>(area, AreaUnits.SquareMillimetre),
                                ReferenceValueOrigin.Standard,
                                "Nominal stress area As,nom for the coarse thread, from the handbook's chapter 1 test-force "
                                + "table; Tab. 16 states the tensile stress is calculated on this tension cross-section.",
                                "As,nom")),
                        Standards =
                        [
                            new StandardReference("ISO 3506-1", StandardSeed.Iso3506Part1, "ISO", null,
                                "Strength-class limits, as the handbook extracts them"),
                            new StandardReference("ISO 262", StandardSeed.Iso262, "ISO", "1998", "Selected coarse pitch"),
                        ],
                        SourceClassification = $"Austenitic stainless steel bolts, screws and studs — steel grade {group}, "
                            + $"strength class {name[^2..]} (ISO 3506-1)",
                        Notes = "NO PROOF STRESS RECORDED: ISO 3506-1 specifies a 0.2% proof stress and tensile strength for "
                            + "stainless screws, not a proof stress, so the bolted-joint calculator asks for one by hand. "
                            + "Elongation at fracture is specified as a length (0.4 d for class 70, 0.3 d for class 80) and "
                            + "is not recorded. Properties above M24 must be agreed between user and manufacturer (Tab. 16 "
                            + "note 3), which is why the dataset stops at M24.",
                    },
                    SeedSources.Wuerth("Chapter 2, Rust and acid-resistant fasteners", WuerthChapter2,
                        "Tab. 16 (extract from DIN EN ISO 3506-1, mechanical properties of screws in the austenitic "
                        + "steel groups); thread pitch and stress area from chapter 1 Tab. 3 and Tab. 6 (" + WuerthChapter1 + ")"),
                    new SourceCitation("Würth Industrie Service GmbH & Co. KG", "DINO technical handbook, chapter 2",
                        TableOrFigure: "Tab. 16", RowOrEntry: $"M{d}, {name}"));
            }
        }
    }

    private static ThreadSpecification MetricThread(int d, double pitch, string pitchConditions)
    {
        var designation = $"M{d} x {Format(pitch)}";

        return new ThreadSpecification(
            designation,
            ThreadSystem.MetricCoarse,
            NominalDiameter: new ReferenceValue<Length>(
                new Quantity<Length>(d, LengthUnits.Millimetre),
                ReferenceValueOrigin.Standard,
                "Nominal (major) diameter of the coarse-thread size.",
                "d"),
            Pitch: new ReferenceValue<Length>(
                new Quantity<Length>(pitch, LengthUnits.Millimetre),
                ReferenceValueOrigin.Standard,
                pitchConditions,
                "P"),
            Handedness: ThreadHandedness.RightHand);
    }

    private static ReferenceValue<Pressure> Mpa(double value, string conditions, string designation) =>
        new(new Quantity<Pressure>(value, PressureUnits.Megapascal), ReferenceValueOrigin.Standard, conditions, designation);
}
