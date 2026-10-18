using Tempest.Core.Materials;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// Stainless steels: the Aalco bar-and-section datasheets (EN 10088-3
/// limits restated) and, for grades Aalco publishes without usable physical
/// data, the AZoM grade articles.
/// </summary>
public sealed partial class MaterialSeed
{
    /// <summary>The identity of the 1.4307 (304L) record.</summary>
    public const string Stainless1Point4307 = "mat-1-4307";

    /// <summary>The identity of the 1.4401 (316) record.</summary>
    public const string Stainless1Point4401 = "mat-1-4401";

    /// <summary>The identity of the 1.4462 (2205 duplex) record.</summary>
    public const string Stainless1Point4462 = "mat-1-4462";

    /// <summary>The identity of the 1.4021 (420) record.</summary>
    public const string Stainless1Point4021 = "mat-1-4021";

    /// <summary>The identity of the 1.4305 (303) record.</summary>
    public const string Stainless1Point4305 = "mat-1-4305";

    /// <summary>The identity of the 1.4016 (430) record.</summary>
    public const string Stainless1Point4016 = "mat-1-4016";

    /// <summary>The identity of the 1.4542 (17-4 PH) record.</summary>
    public const string Stainless1Point4542 = "mat-1-4542-p930";

    /// <summary>The identity of the grade 431 (1.4057) annealed record.</summary>
    public const string Stainless431 = "mat-431-annealed";

    /// <summary>The identity of the grade 410 annealed record.</summary>
    public const string Stainless410 = "mat-410-annealed";

    /// <summary>The identity of the grade 440C annealed record.</summary>
    public const string Stainless440C = "mat-440c-annealed";

    /// <summary>The identity of the grade 321 record.</summary>
    public const string Stainless321 = "mat-321";

    private static StandardReference En10088Part3At2005 =>
        new("EN 10088-3", StandardSeed.En10088Part3, "CEN", "2005", "Mechanical property limits for bar, as the datasheet restates them");

    private static IEnumerable<ReferenceSeedRecord<MaterialDefinition>> StainlessSteels() =>
    [
        AalcoStainless(Stainless1Point4307, "1.4307 (304L) austenitic stainless steel", "1.4307", "304L",
            "Stainless Steel - Austenitic", "Stainless-Steel-14307-304L-Bar-and-Section_35",
            "Stainless Steel - Austenitic - 1.4307 (304L) Bar and Section", "Bar and section up to 160 mm",
            proofMpa: 175, utsLow: 500, utsHigh: 700, elongation: 45, elongationGauge: "A50mm", hbMax: 215,
            density: 8.0, melting: 1450, modulus: 193, cte: 17.2, conductivity: 16.2,
            notes: "Low-carbon 304: resistant to sensitisation in welding. Electrical resistivity 0.72e-6 ohm.m published "
                + "but not modelled."),

        AalcoStainless(Stainless1Point4401, "1.4401 (316) austenitic stainless steel", "1.4401", "316",
            "Stainless Steel - Austenitic", "Stainless-Steel-14401-316-Bar-and-Section_37",
            "Stainless Steel - Austenitic - 1.4401 (316) Bar and Section", "Bar and section up to 160 mm",
            proofMpa: 200, utsLow: 500, utsHigh: 700, elongation: 40, elongationGauge: "A50mm", hbMax: 215,
            density: 8.0, melting: 1400, modulus: 193, cte: 15.9, conductivity: 16.3,
            notes: "Molybdenum-bearing austenitic grade. Electrical resistivity 0.74e-6 ohm.m published but not modelled."),

        AalcoStainless(Stainless1Point4462, "1.4462 (2205) duplex stainless steel", "1.4462", "2205",
            "Stainless Steel - Duplex", "Stainless-Steel-14462-2205-Bar_347",
            "Stainless Steel - Duplex - 1.4462 (2205) Bar", "Bar up to 160 mm",
            proofMpa: 450, utsLow: 650, utsHigh: 880, elongation: 25, elongationGauge: "A50mm", hbMax: 270,
            density: 7.805, melting: null, modulus: 200, cte: 13.7, conductivity: 19.0,
            notes: "Designations the source lists as similar: UNS S31803, UNS S32205, BS 318S13. Electrical resistivity "
                + "0.85e-6 ohm.m published but not modelled."),

        AalcoStainless(Stainless1Point4305, "1.4305 (303) free-machining austenitic stainless steel", "1.4305", "303",
            "Stainless Steel - Austenitic", "Stainless-Steel-14305-303-Bar_107",
            "Stainless Steel - Austenitic - 1.4305 (303) Bar", "Bar up to 160 mm",
            proofMpa: 190, utsLow: 500, utsHigh: 750, elongation: 35, elongationGauge: "A50mm", hbMax: 230,
            density: 8.03, melting: 1455, modulus: 193, cte: 17.3, conductivity: 16.3,
            notes: "Sulphur-bearing free-machining grade; lower corrosion resistance and weldability than 304."),

        AalcoStainless(Stainless1Point4016, "1.4016 (430) ferritic stainless steel", "1.4016", "430",
            "Stainless Steel - Ferritic", "Stainless-Steel-14016-430-Bar_348",
            "Stainless Steel - Ferritic - 1.4016 (430) Bar", "Bar up to 100 mm",
            proofMpa: 240, utsLow: 400, utsHigh: 630, elongation: 20, elongationGauge: "A50mm", hbMax: 200,
            density: 7.75, melting: 1425, modulus: 200, cte: 10.4, conductivity: 23.9,
            notes: "Ferritic and magnetic. The source gives a melting range 1425-1510 degC; the solidus is recorded."),

        AalcoStainless(Stainless1Point4021, "1.4021 (420) martensitic stainless steel, hardened and tempered",
            "1.4021", "420", "Stainless Steel - Martensitic", "Stainless-Steel-14021-420-Bar_311",
            "Stainless Steel - Martensitic - 1.4021 (420) Bar", "Bar up to 160 mm",
            proofMpa: 500, utsLow: 700, utsHigh: 950, elongation: 12, elongationGauge: "A", hbMax: null,
            density: 7.75, melting: null, modulus: 200, cte: 10.3, conductivity: 24.9,
            notes: "The datasheet tabulates proof stress 500-600 MPa, tensile strength 700-950 MPa and elongation "
                + "12-13% for bar to 160 mm without naming the heat-treated condition; these are the lower ends. The "
                + "range corresponds to a hardened-and-tempered bar, and annealed 420 is much weaker — confirm the "
                + "condition against the certificate.",
            condition: "Hardened and tempered (condition inferred from the strength range; not named by the source)"),

        Mat(Stainless1Point4542,
            new MaterialDefinition
            {
                Name = "1.4542 (17-4 PH) precipitation-hardening stainless steel, P930",
                Family = MaterialFamily.StainlessSteel,
                Designation = "1.4542",
                Grade = "17-4 PH / 630",
                Condition = "P930 (precipitation hardened)",
                SourceClassification = "Stainless Steel - Precipitation Hardening",
                Standards = [En10088Part3At2005],
                Properties = Props(
                [
                    (Yield, Lim(MPa(720), "Minimum proof stress, P930, bar up to 100 mm. Other conditions on the sheet: "
                        + "P800 520 MPa, P960 790 MPa, P1070 1000 MPa.", "Proof Stress min")),
                    (Uts, Lim(MPa(930), "Lower limit of 930-1100 MPa, P930, bar up to 100 mm.", "Tensile Strength")),
                    (Elongation, Lim(Pct(16), "Minimum A, P930.", "Elongation A min")),
                    (Density, Typ(GramPerCc(7.75), "As the datasheet states it.")),
                    (Modulus, Typ(GPa(196), "As the datasheet states it.")),
                    (Cte, Typ(MicroPerK(10.8), "As the datasheet states it.")),
                    (Conductivity, Typ(WPerMK(18.4), "As the datasheet states it.")),
                ]),
                ProcessingNotes = "Solution annealed bar is machinable (Rm <= 1200 MPa, <= 360 HB) and is aged to the "
                    + "chosen P condition; this record is the P930 condition only.",
                Notes = "Knowledge-foundation archive items MAT-SS-17-4PH / MAT-STEEL-17-4PH. Electrical resistivity "
                    + "0.8e-6 ohm.m published but not modelled.",
            },
            SeedSources.AalcoDatasheet("Stainless Steel - Precipitation Hardening - 1.4542 (17-4PH / 630) Bar",
                "Stainless-Steel-14542-174--630-Bar_100", "Physical properties; mechanical properties at P930, bar to 100 mm"),
            "Aalco Metals Limited", "Stainless Steel - 1.4542 (17-4PH / 630) Bar", "Mechanical properties at P930"),

        AzomStainless(Stainless431, "Grade 431 (1.4057) martensitic stainless steel, annealed", "431", "UNS S43100 / 1.4057",
            1023, "Stainless Steel - Grade 431 (UNS S43100)", yieldMpa: 655, utsMpa: 862, elongation: 20,
            density: 7800, modulus: 200, cte: 10.2, conductivity: 20.2, heat: 460,
            notes: "Annealed properties typical of ASTM A276 Condition A, as the article states. Hardened and tempered "
                + "431 reaches Rp0.2 1035-1080 MPa at low tempering temperatures; 'Condition T' bar is specified at "
                + "850-1000 MPa. The article's grade table gives EN 1.4057 / X17CrNi16-2 as the Euronorm equivalent."),

        AzomStainless(Stainless410, "Grade 410 martensitic stainless steel, annealed", "410", "UNS S41000",
            970, "Stainless Steel - Grade 410 (UNS S41000)", yieldMpa: 275, utsMpa: 480, elongation: 16,
            density: 7800, modulus: 200, cte: 9.9, conductivity: 24.9, heat: 460,
            notes: "Annealed minima as the article tabulates them (ASTM A276 Condition A). Aalco's 1.4006 datasheet "
                + "was read and not used: it prints a modulus of elasticity of 300 GPa, which no martensitic stainless "
                + "steel has; the source error is recorded in the Seed Data Sources Register.",
            limits: true),

        AzomStainless(Stainless440C, "Grade 440C martensitic stainless steel, annealed", "440C", "UNS S44004",
            1024, "Stainless Steel - Grade 440 (UNS S44000)", yieldMpa: 448, utsMpa: 758, elongation: 14,
            density: 7650, modulus: 200, cte: 10.1, conductivity: 24.2, heat: 460,
            notes: "Annealed values typical of ASTM A276 Condition A. Hardened and tempered at 204 degC the article gives "
                + "Rp0.2 1900 MPa, Rm 2030 MPa, 59 HRC; physical properties are given for 440A/B/C together."),

        Mat(Stainless321,
            new MaterialDefinition
            {
                Name = "Grade 321 titanium-stabilised austenitic stainless steel",
                Family = MaterialFamily.StainlessSteel,
                Designation = "321",
                Grade = "UNS S32100",
                Condition = "Annealed",
                SourceClassification = "Austenitic stainless steel",
                Properties = Props(
                [
                    (Yield, Lim(MPa(205), "Minimum 0.2% proof stress as the article tabulates it.", "Yield Strength 0.2% Proof min")),
                    (Uts, Lim(MPa(515), "Minimum tensile strength.", "Tensile Strength min")),
                    (Elongation, Lim(Pct(40), "Minimum, in 50 mm.", "Elongation min")),
                    (Density, Typ(KgPerM3(8027), "Annealed, as the article states it.")),
                    (Modulus, Typ(GPa(193), "Annealed.")),
                    (Cte, Typ(MicroPerK(16.6), "Mean 0-100 degC (17.2 to 315 degC, 18.6 to 538 degC).")),
                    (Conductivity, Typ(WPerMK(16.1), "At 100 degC.")),
                    (HeatCapacity, Typ(JPerKgK(500), "0-100 degC.")),
                ]),
                Notes = "Stabilised against intergranular corrosion after exposure in the 425-850 degC carbide precipitation "
                    + "range. Knowledge-foundation archive item MAT-STEEL-321.",
            },
            SeedSources.Azom("Stainless Steel - Grade 321 (UNS S32100)", 967, "Tables 2 and 3: mechanical and physical properties"),
            "AZoM (AZO Materials)", "Stainless Steel - Grade 321 (UNS S32100)", "Table 2, Table 3"),
    ];

    private static ReferenceSeedRecord<MaterialDefinition> AalcoStainless(
        string recordId,
        string name,
        string designation,
        string grade,
        string classification,
        string ashx,
        string title,
        string band,
        double proofMpa,
        double utsLow,
        double utsHigh,
        double elongation,
        string elongationGauge,
        double? hbMax,
        double density,
        double? melting,
        double modulus,
        double cte,
        double conductivity,
        string notes,
        string? condition = null)
    {
        var properties = new List<(string, ReferenceQuantityValue)>
        {
            (Yield, Lim(MPa(proofMpa), $"Minimum proof stress, {band.ToLowerInvariant()}.", "Proof Stress min")),
            (Uts, Lim(MPa(utsLow), $"Lower limit of the {utsLow:0}-{utsHigh:0} MPa range, {band.ToLowerInvariant()}.",
                "Tensile Strength")),
            (Elongation, Lim(Pct(elongation), $"Minimum {elongationGauge}, {band.ToLowerInvariant()}.",
                $"Elongation {elongationGauge} min")),
            (Density, Typ(GramPerCc(density), "As the datasheet states it.")),
            (Modulus, Typ(GPa(modulus), "As the datasheet states it.")),
            (Cte, Typ(MicroPerK(cte), "As the datasheet states it.")),
            (Conductivity, Typ(WPerMK(conductivity), "As the datasheet states it.")),
        };

        if (hbMax is { } hb)
            properties.Add((BrinellHardness, Lim(Ratio(hb), $"Maximum Brinell hardness number, {band.ToLowerInvariant()}. "
                + "Stored as the bare number the source quotes.", "HB max")));

        if (melting is { } m)
            properties.Add((Melting, Typ(DegC(m), "As the datasheet states it.")));

        return Mat(recordId,
            new MaterialDefinition
            {
                Name = name,
                Family = MaterialFamily.StainlessSteel,
                Designation = designation,
                Grade = grade,
                Condition = condition,
                SourceClassification = classification,
                Standards = [En10088Part3At2005],
                Properties = Props([.. properties]),
                Notes = notes,
            },
            SeedSources.AalcoDatasheet(title, ashx, $"Physical properties and mechanical properties ({band}) tables"),
            "Aalco Metals Limited", title, $"Mechanical properties table ({band})");
    }

    private static ReferenceSeedRecord<MaterialDefinition> AzomStainless(
        string recordId,
        string name,
        string designation,
        string grade,
        int articleId,
        string articleTitle,
        double yieldMpa,
        double utsMpa,
        double elongation,
        double density,
        double modulus,
        double cte,
        double conductivity,
        double heat,
        string notes,
        bool limits = false)
    {
        ReferenceQuantityValue Strength(double mpa, string label) => limits
            ? Lim(MPa(mpa), $"Annealed minimum ({label}) as the article tabulates it.", label)
            : Typ(MPa(mpa), $"Annealed, typical of ASTM A276 Condition A as the article states.", label);

        return Mat(recordId,
            new MaterialDefinition
            {
                Name = name,
                Family = MaterialFamily.StainlessSteel,
                Designation = designation,
                Grade = grade,
                Condition = "Annealed",
                SourceClassification = "Martensitic stainless steel",
                Standards = [new StandardReference("ASTM A276", StandardSeed.AstmA276, "ASTM", null,
                    "The specification the article names for its annealed (Condition A) values")],
                Properties = Props(
                [
                    (Yield, Strength(yieldMpa, "Yield Strength 0.2% Proof")),
                    (Uts, Strength(utsMpa, "Tensile Strength")),
                    (Elongation, limits
                        ? Lim(Pct(elongation), "Annealed minimum, in 50 mm.", "Elongation")
                        : Typ(Pct(elongation), "Annealed, in 50 mm.")),
                    (Density, Typ(KgPerM3(density), "As the article's physical properties table states it.")),
                    (Modulus, Typ(GPa(modulus), "As the article's physical properties table states it.")),
                    (Cte, Typ(MicroPerK(cte), "Mean 0-100 degC.")),
                    (Conductivity, Typ(WPerMK(conductivity), "At 100 degC.")),
                    (HeatCapacity, Typ(JPerKgK(heat), "0-100 degC.")),
                ]),
                Notes = notes,
            },
            SeedSources.Azom(articleTitle, articleId, "Mechanical properties and typical physical properties tables"),
            "AZoM (AZO Materials)", articleTitle, "Mechanical properties table; physical properties table");
    }
}
