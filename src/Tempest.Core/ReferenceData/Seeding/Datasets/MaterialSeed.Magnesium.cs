using Tempest.Core.Materials;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// Magnesium alloys the knowledge-foundation archive named, from the AZoM
/// temper-specific articles.
/// </summary>
/// <remarks>
/// <see cref="MaterialFamily"/> has no magnesium member; these are
/// <see cref="MaterialFamily.OtherMetal"/> with the source's own
/// classification recorded, which keeps every metal-family rule (yield
/// strength, heat-treatment condition) applicable to them.
/// </remarks>
public sealed partial class MaterialSeed
{
    /// <summary>The identity of the AZ31B-H24 record.</summary>
    public const string MagnesiumAz31bH24 = "mat-az31b-h24";

    /// <summary>The identity of the AZ61A-F record.</summary>
    public const string MagnesiumAz61aF = "mat-az61a-f";

    /// <summary>The identity of the AZ80A-T6 record.</summary>
    public const string MagnesiumAz80aT6 = "mat-az80a-t6";

    /// <summary>The identity of the AZ91D-F record.</summary>
    public const string MagnesiumAz91dF = "mat-az91d-f";

    /// <summary>The identity of the WE43A-T6 record.</summary>
    public const string MagnesiumWe43aT6 = "mat-we43a-t6";

    private static IEnumerable<ReferenceSeedRecord<MaterialDefinition>> MagnesiumAlloys() =>
    [
        Magnesium(MagnesiumAz31bH24, "AZ31B-H24", "UNS M11311", "H24 (strain hardened, partially annealed)", "Wrought",
            8621, "Magnesium AZ31B-H24 (UNS M11311) Alloy", uts: 290, ys: 220, elongation: 15, modulus: 45, poisson: 0.35,
            density: 1.77, cte: 26, conductivity: 96, brinell: 73, compressiveYield: 180,
            archive: "MAT-MG-AZ31B"),
        Magnesium(MagnesiumAz61aF, "AZ61A-F", "UNS M11610", "F (as fabricated)", "Wrought (forged/extruded)",
            8635, "Magnesium AZ61A-F (UNS M11610) Alloy", uts: 295, ys: 180, elongation: 12, modulus: 45, poisson: 0.35,
            density: 1.80, cte: 26, conductivity: 70, brinell: 55, compressiveYield: 125,
            archive: "MAT-MG-AZ61A"),
        Magnesium(MagnesiumAz80aT6, "AZ80A-T6", "UNS M11800", "T6 (solution treated and artificially aged), forged",
            "Wrought (forged)", 8657, "Magnesium AZ80A-T6 (UNS M11800) Forged Alloy", uts: 340, ys: 250, elongation: 5,
            modulus: 45, poisson: 0.35, density: 1.80, cte: 26, conductivity: 76, brinell: 72, compressiveYield: 185,
            archive: "MAT-MG-AZ80A"),
        Magnesium(MagnesiumAz91dF, "AZ91D-F", "UNS M11916", "F (as cast, die casting)", "Cast", 8670,
            "Magnesium AZ91D-F Alloy (UNS M11916)", uts: 230, ys: 150, elongation: 3, modulus: 44.8, poisson: 0.35,
            density: 1.81, cte: 26, conductivity: 72.7, brinell: 63, compressiveYield: null,
            archive: "MAT-MG-AZ91D"),
        Magnesium(MagnesiumWe43aT6, "WE43A-T6", "UNS M18430", "T6 (solution treated and artificially aged), cast", "Cast",
            8545, "Magnesium WE43A-T6 Alloy (UNS M18430)", uts: 276, ys: 207, elongation: 2, modulus: 44.2, poisson: 0.27,
            density: 1.84, cte: 26, conductivity: 51.3, brinell: 75, compressiveYield: null,
            archive: "MAT-MG-WE43"),
    ];

    private static ReferenceSeedRecord<MaterialDefinition> Magnesium(
        string recordId,
        string designation,
        string uns,
        string condition,
        string form,
        int articleId,
        string articleTitle,
        double uts,
        double ys,
        double elongation,
        double modulus,
        double poisson,
        double density,
        double cte,
        double conductivity,
        double brinell,
        double? compressiveYield,
        string archive)
    {
        var properties = new List<(string, ReferenceQuantityValue)>
        {
            (Uts, Typ(MPa(uts), "As the article tabulates it.", "Tensile strength")),
            (Yield, Typ(MPa(ys), "Tensile yield at 0.2% strain, as the article tabulates it.", "Yield strength")),
            (Elongation, Typ(Pct(elongation), "In 50 mm, as the article tabulates it.")),
            (Modulus, Typ(GPa(modulus), "As the article tabulates it.")),
            (Poisson, Typ(Ratio(poisson), "As the article tabulates it.")),
            (Density, Typ(GramPerCc(density), "As the article states it.")),
            (Cte, Typ(MicroPerK(cte), "0-100 degC.")),
            (Conductivity, Typ(WPerMK(conductivity), "As the article states it.")),
            (BrinellHardness, Typ(Ratio(brinell), "Brinell hardness, 500 kg load, 10 mm ball (lower end where a range is given).")),
        };

        if (compressiveYield is { } cy)
            properties.Add((CompressiveYield, Typ(MPa(cy), "Compressive yield strength at 0.2% offset. Magnesium alloys "
                + "yield in compression well below their tensile yield; a member in compression must be checked against "
                + "this, not the tensile yield.")));

        return Mat(recordId,
            new MaterialDefinition
            {
                Name = $"{designation} magnesium alloy",
                Family = MaterialFamily.OtherMetal,
                Designation = designation,
                Grade = uns,
                Condition = condition,
                SourceClassification = $"Magnesium alloy — {form}",
                Properties = Props([.. properties]),
                EnvironmentalNotes = "Magnesium is anodic to almost every structural metal; isolate it galvanically and "
                    + "protect it from moisture.",
                Notes = $"Typical values for the stated temper; no specification minima were read. Knowledge-foundation "
                    + $"archive item {archive}.",
            },
            SeedSources.Azom(articleTitle, articleId, "Physical, mechanical and thermal properties tables"),
            "AZoM (AZO Materials)", articleTitle, "Mechanical properties table");
    }

    /// <summary>The property name a compressive yield strength is recorded under — not one of the well-known names, which have none for it.</summary>
    public const string CompressiveYield = "CompressiveYieldStrength";
}
