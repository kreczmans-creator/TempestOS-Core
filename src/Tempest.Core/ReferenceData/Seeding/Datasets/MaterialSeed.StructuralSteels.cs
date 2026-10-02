using Tempest.Core.Materials;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// Hot-rolled structural steels to EN 10025-2/-3/-4/-6 and EN 10210-1.
/// </summary>
/// <remarks>
/// <para>
/// Mechanical limits are each grade's own, as the SteelNumber grade page
/// (or, for S235JR, the Siderticino datasheet) restates the standard, for
/// the thinnest band, with the whole thickness ladder in the conditions.
/// </para>
/// <para>
/// <b>Physical properties are the Eurocode design values, cited as such.</b>
/// EN 10025 sets no physical property at all. The values a structural
/// calculation uses — E = 210 GPa, G = 81 GPa, alpha = 12e-6 /K, density
/// 7850 kg/m3 — are the EN 1993-1-1 and EN 1993-1-2 design values, which
/// apply to every grade in this file. They are taken from the Siderticino
/// S355 datasheet's own table, which cites those clauses, and every one is
/// marked as a supplementary value naming that page.
/// </para>
/// </remarks>
public sealed partial class MaterialSeed
{
    /// <summary>The identity of the S235JR structural steel record.</summary>
    public const string S235JR = "mat-s235jr";

    /// <summary>The identity of the S275JR structural steel record.</summary>
    public const string S275JR = "mat-s275jr";

    /// <summary>The identity of the S355JR structural steel record.</summary>
    public const string S355JR = "mat-s355jr";

    /// <summary>The identity of the S355J0 structural steel record.</summary>
    public const string S355J0 = "mat-s355j0";

    /// <summary>The identity of the S355K2 structural steel record.</summary>
    public const string S355K2 = "mat-s355k2";

    /// <summary>The identity of the S460N structural steel record.</summary>
    public const string S460N = "mat-s460n";

    /// <summary>The identity of the S460M structural steel record.</summary>
    public const string S460M = "mat-s460m";

    /// <summary>The identity of the S460Q structural steel record.</summary>
    public const string S460Q = "mat-s460q";

    /// <summary>The identity of the S355J2H hollow-section steel record.</summary>
    public const string S355J2H = "mat-s355j2h";

    private const string SiderticinoS355Page =
        "Siderticino SA, S355 steel datasheet (https://siderticino.it/en/steel-datasheets/s355j2/), physical "
        + "properties table";

    private const string SiderticinoS235Page =
        "Siderticino SA, S235JR steel datasheet (https://siderticino.it/en/steel-datasheets/s235jr/)";

    private static (string, ReferenceQuantityValue)[] EurocodeStructuralSteelPhysicals(bool includeDensity = true, bool includePoisson = true)
    {
        var values = new List<(string, ReferenceQuantityValue)>
        {
            (Modulus, SupplementaryValue(GPa(210), SiderticinoS355Page + ": E = 210 GPa, citing EN 1993-1-1, which "
                + "applies this value to all structural steels in its scope.", ReferenceValueOrigin.EngineeringReference)),
            (Shear, SupplementaryValue(GPa(81), SiderticinoS355Page + ": G = 81 GPa, citing EN 1993-1-1.",
                ReferenceValueOrigin.EngineeringReference)),
            (Cte, SupplementaryValue(MicroPerK(12), SiderticinoS355Page + ": alpha = 12e-6 /K, citing EN 1993-1-1.",
                ReferenceValueOrigin.EngineeringReference)),
            (Conductivity, SupplementaryValue(WPerMK(53.3), SiderticinoS355Page + ": 53.3 W/(m.K) at 20 degC, "
                + "citing EN 1993-1-2. Falls with temperature.", ReferenceValueOrigin.EngineeringReference)),
            (HeatCapacity, SupplementaryValue(JPerKgK(440), SiderticinoS355Page + ": 440 J/(kg.K) at 20 degC, "
                + "citing EN 1993-1-2.", ReferenceValueOrigin.EngineeringReference)),
        };

        if (includeDensity)
            values.Add((Density, SupplementaryValue(GramPerCc(7.85), SiderticinoS355Page + ": 7.85 g/cm3, citing "
                + "EN 1993-1-2.", ReferenceValueOrigin.EngineeringReference)));

        if (includePoisson)
            values.Add((Poisson, SupplementaryValue(Ratio(0.3), SiderticinoS235Page + ", which gives nu ~ 0.3 for "
                + "hot-rolled non-alloy structural steels.", ReferenceValueOrigin.EngineeringReference)));

        return [.. values];
    }

    private static StandardReference En10025Part2At2004 =>
        new("EN 10025-2", StandardSeed.En10025Part2, "CEN", "2004",
            "Mechanical property limits, as the SteelNumber page states them against the 2004 edition");

    private static StandardReference Eurocode3 =>
        new("EN 1993-1-1", StandardSeed.En1993Part1Part1, "CEN", null,
            "Design values of E, G and alpha used for the physical properties");

    private static IEnumerable<ReferenceSeedRecord<MaterialDefinition>> StructuralSteels() =>
    [
        Mat(S235JR,
            new MaterialDefinition
            {
                Name = "S235JR non-alloy structural steel",
                Family = MaterialFamily.Steel,
                Designation = "S235JR",
                Grade = "1.0038",
                SourceClassification = "Non-alloy structural steel",
                Standards =
                [
                    new StandardReference("EN 10025-2", StandardSeed.En10025Part2, "CEN", "2019",
                        "Mechanical property limits by thickness, as the datasheet restates them"),
                    Eurocode3,
                ],
                Properties = Props(
                [
                    (Yield, Lim(MPa(235), "Minimum upper yield strength ReH for t <= 16 mm. Ladder: 225 MPa to 40 mm, "
                        + "215 MPa to 100 mm, 195 MPa to 150 mm, 185 MPa to 200 mm, 175 MPa to 250 mm.", "ReH min")),
                    (Uts, Lim(MPa(360), "Lower limit of 360-510 MPa for t <= 100 mm (350-500 MPa to 150 mm, "
                        + "340-490 MPa to 250 mm).", "Rm")),
                    (Elongation, Lim(Pct(26), "Minimum, longitudinal, t <= 40 mm (25% to 63 mm, 24% to 100 mm).", "A% min")),
                    (Impact, Lim(Joule(27), "Charpy V minimum at +20 degC — what the JR quality denotes; the datasheet "
                        + "notes the test is made only when ordered.", "KV at +20 degC")),
                    (Density, Typ(KgPerM3(7850), "Density as the datasheet's own header states it (7,850 kg/m3); its "
                        + "text gives 7800-7850 kg/m3.")),
                    (Modulus, Typ(GPa(210), "E ~ 210 GPa, as the datasheet states for elastic sizing.")),
                    (Shear, Typ(GPa(81), "G ~ 81 GPa, as the datasheet states.")),
                    (Poisson, Typ(Ratio(0.3), "nu ~ 0.3, as the datasheet states.")),
                    (Conductivity, Typ(WPerMK(53.3), "At 20 degC, which the datasheet says is computed from the "
                        + "EN 1993-1-2 formulas.")),
                    (HeatCapacity, Typ(JPerKgK(440), "At 20 degC, computed from EN 1993-1-2 per the datasheet.")),
                    (Cte, SupplementaryValue(MicroPerK(12), SiderticinoS355Page + ": alpha = 12e-6 /K citing "
                        + "EN 1993-1-1. The S235JR page describes the expansion coefficient without stating a number.",
                        ReferenceValueOrigin.EngineeringReference)),
                ]),
                EnvironmentalNotes = "No corrosion resistance; protect or allow for corrosion loss. JR quality is "
                    + "suited to service not below 0 degC per the datasheet.",
                Notes = "Delivery conditions +AR, +N or +M must be stated on the order (datasheet). Chemical limits "
                    + "stated by the source and not modelled: C <= 0.17% (t <= 40 mm), Mn <= 1.40%, P <= 0.035%, "
                    + "S <= 0.035%, Cu <= 0.55%, N <= 0.012%. Knowledge-foundation archive value corrected: the archive "
                    + "screening record gave yield 250 MPa; EN 10025-2 as restated here gives 235 MPa for t <= 16 mm.",
            },
            SeedSources.SiderticinoPage("S235JR Steel", "s235jr",
                "Mechanical properties of S235JR by thickness class (EN 10025-2) and physical characteristics"),
            "Siderticino SA", "S235JR Steel", "Mechanical properties of S235JR by thickness class (EN 10025-2)"),

        Structural(S275JR, "S275JR", "1.0044", 3, 275, "265 MPa to 40 mm, 255 MPa to 63 mm, 245 MPa to 80 mm, "
                + "235 MPa to 100 mm, 225 MPa to 150 mm, 215 MPa to 200 mm, 205 MPa to 250 mm", 410, 560,
            "430-580 MPa below 3 mm; 400-540 MPa to 150 mm; 380-540 MPa to 250 mm", 23,
            "22% to 63 mm, 21% to 100 mm", 27, "+20 degC",
            "JR quality: 27 J at +20 degC. The SteelNumber page tabulates 27 J at -20/0/+20 degC for the "
            + "JR/J0/J2 family without separating them; the temperature assigned to JR is the one the Siderticino "
            + "S235JR datasheet states the JR suffix denotes."),

        Structural(S355JR, "S355JR", "1.0045", 8, 355, "345 MPa to 40 mm, 335 MPa to 63 mm, 325 MPa to 80 mm, "
                + "315 MPa to 100 mm, 295 MPa to 150 mm, 285 MPa to 200 mm, 275 MPa to 250 mm", 470, 630,
            "510-680 MPa below 3 mm; 450-600 MPa to 250 mm", 22, "21% to 63 mm, 20% to 100 mm", 27, "+20 degC",
            "JR quality: 27 J at +20 degC (temperature per the Siderticino S235JR datasheet's statement of what JR denotes)."),

        Structural(S355J0, "S355J0", "1.0553", 2, 355, "345 MPa to 40 mm, 335 MPa to 63 mm, 325 MPa to 80 mm, "
                + "315 MPa to 100 mm, 295 MPa to 150 mm, 285 MPa to 200 mm, 275 MPa to 250 mm", 470, 630,
            "510-680 MPa below 3 mm; 450-600 MPa to 250 mm", 22, "21% to 63 mm, 20% to 100 mm", 27, "0 degC",
            "J0 quality: 27 J at 0 degC (temperature per the Siderticino S235JR datasheet's statement of what J0 denotes)."),

        Structural(S355K2, "S355K2", "1.0596", 11, 355, "345 MPa to 40 mm, 335 MPa to 63 mm, 325 MPa to 80 mm, "
                + "315 MPa to 100 mm, 295 MPa to 150 mm, 285 MPa to 200 mm, 275 MPa to 250 mm", 470, 630,
            "510-680 MPa below 3 mm; 450-600 MPa to 400 mm", 22, "21% to 63 mm, 20% to 100 mm", null, null,
            "The SteelNumber page publishes no impact requirement for K2, so none is recorded."),

        Structural(S460N, "S460N", "1.8901", 22, 460, "440 MPa to 40 mm, 430 MPa to 63 mm, 410 MPa to 80 mm, "
                + "400 MPa to 100 mm, 380 MPa to 150 mm, 370 MPa to 200 mm", 540, 720,
            "530-710 MPa for 100-200 mm", 17, "17% throughout to 200 mm", 40, "-20 degC (longitudinal)",
            "Longitudinal Charpy V minima: 55 J at +20, 47 J at 0, 43 J at -10, 40 J at -20 degC; transverse "
            + "31/27/24/20 J.", standard: new StandardReference("EN 10025-3", StandardSeed.En10025Part3, "CEN", "2004",
                "Normalised/normalised-rolled fine-grain steels; limits as the SteelNumber page states them"),
            classification: "Normalized/normalized rolled weldable fine grain structural steel"),

        Structural(S460M, "S460M", "1.8827", 30, 460, "440 MPa to 40 mm, 430 MPa to 63 mm, 410 MPa to 80 mm, "
                + "400 MPa to 100 mm, 385 MPa to 120 mm", 540, 720,
            "530-710 MPa to 63 mm, 510-690 MPa to 80 mm, 500-680 MPa to 100 mm, 490-660 MPa to 120 mm", 17,
            "17%", 40, "-20 degC (longitudinal)",
            "Longitudinal Charpy V minima: 55 J at +20, 47 J at 0, 43 J at -10, 40 J at -20 degC.",
            standard: new StandardReference("EN 10025-4", StandardSeed.En10025Part4, "CEN", "2004",
                "Thermomechanical rolled fine-grain steels; limits as the SteelNumber page states them"),
            classification: "Thermomechanical rolled weldable fine grain structural steel",
            tMaxForUts: "t <= 40 mm"),

        Structural(S460Q, "S460Q", "1.8908", 39, 460, "440 MPa for 50-100 mm, 400 MPa for 100-150 mm "
                + "(the first band is 3-50 mm)", 550, 720, "500-670 MPa for 100-150 mm", 17, "17%", 30,
            "-20 degC (longitudinal)", "Longitudinal Charpy V minima: 40 J at 0 degC, 30 J at -20 degC.",
            standard: new StandardReference("EN 10025-6", StandardSeed.En10025Part6, "CEN", "2004",
                "Quenched and tempered high-yield flat products; limits as the SteelNumber page states them"),
            classification: "High yield strength structural steel, quenched and tempered (flat products)",
            tMaxForYield: "3 mm < t <= 50 mm", tMaxForUts: "3 mm < t <= 100 mm"),

        Structural(S355J2H, "S355J2H", "1.0576", 649, 355, "345 MPa to 40 mm, 335 MPa to 63 mm, 325 MPa to 80 mm, "
                + "315 MPa to 100 mm, 295 MPa to 120 mm", 470, 630, "510-680 MPa below 3 mm; 450-600 MPa to 120 mm",
            22, "21% to 63 mm, 20% to 100 mm, 18% to 120 mm", 27, "-20 degC",
            "Charpy V minimum 27 J at -20 degC as the page states it.",
            standard: new StandardReference("EN 10210-1", StandardSeed.En10210Part1, "CEN", "2006",
                "Hot-finished structural hollow sections; limits as the SteelNumber page states them"),
            classification: "Non-alloy structural steel, hot-finished hollow sections"),
    ];

    private static ReferenceSeedRecord<MaterialDefinition> Structural(
        string recordId,
        string grade,
        string number,
        int steelNumberId,
        double yieldMpa,
        string yieldLadder,
        double utsLowMpa,
        double utsHighMpa,
        string utsOtherBands,
        double elongationPct,
        string elongationLadder,
        double? impactJ,
        string? impactTemperature,
        string impactNote,
        StandardReference? standard = null,
        string classification = "Non-alloy structural steel",
        string tMaxForYield = "t <= 16 mm",
        string tMaxForUts = "3 mm <= t <= 100 mm")
    {
        var properties = new List<(string, ReferenceQuantityValue)>
        {
            (Yield, Lim(MPa(yieldMpa), $"Minimum upper yield strength ReH for {tMaxForYield}. Ladder: {yieldLadder}.",
                "ReH min")),
            (Uts, Lim(MPa(utsLowMpa), $"Lower limit of the {utsLowMpa:0}-{utsHighMpa:0} MPa range for {tMaxForUts}; "
                + $"{utsOtherBands}.", "Rm")),
            (Elongation, Lim(Pct(elongationPct), $"Minimum A on Lo = 5.65 sqrt(So), thinnest band above 3 mm; "
                + $"{elongationLadder}.", "A min")),
        };

        if (impactJ is { } joules)
            properties.Add((Impact, Lim(Joule(joules), $"Charpy V minimum at {impactTemperature}. {impactNote}",
                $"KV at {impactTemperature}")));

        properties.AddRange(EurocodeStructuralSteelPhysicals());

        return Mat(recordId,
            new MaterialDefinition
            {
                Name = $"{grade} structural steel",
                Family = MaterialFamily.Steel,
                Designation = grade,
                Grade = number,
                SourceClassification = classification,
                Standards = [standard ?? En10025Part2At2004, Eurocode3],
                Properties = Props([.. properties]),
                EnvironmentalNotes = "No corrosion resistance; protect or allow for corrosion loss.",
                Notes = (impactJ is null ? impactNote + " " : string.Empty)
                    + "Mechanical limits are the grade's own as the SteelNumber page restates the standard; physical "
                    + "properties are the EN 1993-1-1/-1-2 design values, each marked with the page it was read from. "
                    + "The SteelNumber page cites the standard edition shown in Standards; check the edition your "
                    + "order is placed against.",
            },
            SeedSources.SteelNumber(grade, steelNumberId,
                "Mechanical properties table (ReH, Rm and A by nominal thickness; KV)"),
            "SteelNumber (European steel and alloy grades database)", $"SteelNumber grade page — {grade}",
            "Mechanical properties table");
    }
}
