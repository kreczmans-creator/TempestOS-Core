using Tempest.Core.Materials;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// Copper alloys: Aalco datasheets for the EN grades, Copper Development
/// Association alloy data for the UNS grades the archive named.
/// </summary>
/// <remarks>
/// The CDA publishes in US customary units; those values are recorded in
/// those units (ksi as psi, lb/in3, uin/(in.degF), BTU/(h.ft.degF)) and the
/// platform converts on read. Nothing here was converted by hand.
/// </remarks>
public sealed partial class MaterialSeed
{
    /// <summary>The identity of the CW614N leaded brass record.</summary>
    public const string CopperCw614N = "mat-cw614n";

    /// <summary>The identity of the CW451K phosphor bronze record.</summary>
    public const string CopperCw451K = "mat-cw451k";

    /// <summary>The identity of the C63000 nickel-aluminium bronze record.</summary>
    public const string CopperC63000 = "mat-c63000-m30";

    /// <summary>The identity of the C95400 cast aluminium bronze record.</summary>
    public const string CopperC95400 = "mat-c95400-m01";

    /// <summary>The identity of the C10100 oxygen-free copper record.</summary>
    public const string CopperC10100 = "mat-c10100-h04";

    private static IEnumerable<ReferenceSeedRecord<MaterialDefinition>> CopperAlloys() =>
    [
        Mat(CopperCw614N,
            new MaterialDefinition
            {
                Name = "CW614N (CZ121) leaded free-machining brass rod",
                Family = MaterialFamily.CopperAlloy,
                Designation = "CW614N",
                Grade = "CZ121 / CuZn39Pb3",
                SourceClassification = "Copper and Copper Alloys - Brass",
                Standards = [new StandardReference("EN 12164", StandardSeed.En12164, "CEN", "2011",
                    "Rod for free machining purposes; limits as the datasheet restates them")],
                Properties = Props(
                [
                    (Yield, Lim(MPa(230), "Lowest proof stress of the 230-350 MPa range published for bar 6-80 mm "
                        + "diameter / 5-60 mm across flats; the range spans the delivered tempers.", "Proof Stress")),
                    (Uts, Lim(MPa(360), "Lowest of the 360-500 MPa range, same band.", "Tensile Strength")),
                    (Elongation, Lim(Pct(5), "The published range runs 20% to 5%; the limiting (hard) end is recorded.",
                        "Elongation A")),
                    (BrinellHardness, Lim(Ratio(90), "Lowest of the 90-160 HB range published.", "HB")),
                    (Density, Typ(GramPerCc(8.47), "As the datasheet states it.")),
                    (Melting, Typ(DegC(875), "As the datasheet states it.")),
                    (Cte, Typ(MicroPerK(20.9), "As the datasheet states it.")),
                    (Modulus, Typ(GPa(97), "As the datasheet states it.")),
                    (Conductivity, Typ(WPerMK(123), "As the datasheet states it.")),
                ]),
                Notes = "Designations the source lists as corresponding but possibly not direct equivalents: UNS C38500, "
                    + "CuZn39Pb3. Electrical resistivity 0.062e-6 ohm.m published but not modelled.",
            },
            SeedSources.AalcoDatasheet("Copper and Copper Alloys - CW614N Brass Rod",
                "Copper-and-Copper-Alloys-CW614N-Brass-Rod_31", "Physical properties; mechanical properties (bar 6-80 mm)"),
            "Aalco Metals Limited", "Copper and Copper Alloys - CW614N Brass Rod", "Mechanical properties table (bar)"),

        Mat(CopperCw451K,
            new MaterialDefinition
            {
                Name = "CW451K (PB102) phosphor bronze sheet",
                Family = MaterialFamily.CopperAlloy,
                Designation = "CW451K",
                Grade = "PB102 / CuSn5",
                SourceClassification = "Copper and Copper Alloys - Phosphor & Leaded Bronze",
                Standards = [new StandardReference("EN 1652", StandardSeed.En1652, "CEN", "1998",
                    "Sheet, as the datasheet states it")],
                Properties = Props(
                [
                    (Yield, Lim(MPa(240), "Lowest proof stress of the 240-670 MPa range published for sheet 0.1-5 mm; "
                        + "the range spans tempers from annealed to spring hard.", "Proof Stress")),
                    (Uts, Lim(MPa(400), "Lowest of the 400-720 MPa range, same band.", "Tensile Strength")),
                    (Elongation, Lim(Pct(2), "The published range is 2-45%; the limiting (hard) end is recorded.",
                        "Elongation A50mm")),
                    (VickersHardness, Lim(Ratio(75), "Lowest of the 75-230 HV range published.", "HV")),
                    (Density, Typ(GramPerCc(8.85), "As the datasheet states it.")),
                    (Melting, Typ(DegC(930), "As the datasheet states it.")),
                    (Modulus, Typ(GPa(121), "As the datasheet states it.")),
                    (Conductivity, Typ(WPerMK(63), "As the datasheet states it.")),
                    (Cte, SupplementaryValue(MicroPerF(9.9), "Copper Development Association, alloy C51000 data "
                        + "(https://alloys.copper.org/alloy/C51000), coefficient of thermal expansion 68-572 degF, "
                        + "9.9e-6 per degF. The Aalco CW451K datasheet publishes no expansion coefficient; it lists UNS "
                        + "C51000 as a corresponding (not necessarily direct) equivalent.",
                        ReferenceValueOrigin.EngineeringReference)),
                ]),
                ProcessingNotes = "The published figures span the full temper range; specify a temper and check its own "
                    + "limits before relying on strength.",
                Notes = "The PO's list named CW453K (CuSn8); the stockholder read publishes CW451K (CuSn5), which is "
                    + "seeded instead. CW453K remains a gap. Electrical resistivity 0.096e-6 ohm.m published but not "
                    + "modelled.",
            },
            SeedSources.AalcoDatasheet("Copper and Copper Alloys - CW451K Sheet and Bar",
                "Copper-and-Copper-Alloys-CW451K--Sheet-and-Bar_120", "Physical properties; mechanical properties (sheet 0.1-5 mm)"),
            "Aalco Metals Limited", "Copper and Copper Alloys - CW451K Sheet and Bar", "Mechanical properties table (sheet)"),

        CdaAlloy(CopperC63000, "C63000 nickel-aluminium bronze rod, as hot extruded", "C63000", "M30 (as hot extruded)",
            "Copper Alloys - Aluminium Bronze", "Mechanical properties (rod, M30, 4 in. section) and physical properties",
            utsKsi: 100, ysKsi: 60, elongation: 15, fatigueKsi: 36, densityLbIn3: 0.274, cteF: 9.0, modulusKsi: 17500,
            conductivityBtu: 22.6, typical: true,
            notes: "Typical values for rod as hot extruded, 4 in. section. Yield is the 0.5% extension-under-load value the "
                + "CDA tabulates. Fatigue strength at 100x10^6 cycles. CTE 68-572 degF. Knowledge-foundation archive item "
                + "MAT-CU-C63000 (CuAl10Ni5Fe4 is the commonly compared EN composition; not claimed as an equivalent)."),

        CdaAlloy(CopperC95400, "C95400 aluminium bronze, sand cast", "C95400", "M01 (as sand cast)",
            "Copper Alloys - Cast Aluminium Bronze", "Mechanical properties (as sand cast, M01) and physical properties",
            utsKsi: 75, ysKsi: 30, elongation: 12, fatigueKsi: null, densityLbIn3: 0.269, cteF: 9.0, modulusKsi: 15500,
            conductivityBtu: 33.9, typical: false,
            notes: "Minimum values 'for standard' as the CDA tabulates them for sand castings; yield is at 0.5% extension "
                + "under load. The heat-treated TQ50 temper is stronger (90 ksi min). The CDA fatigue column for this "
                + "alloy could not be unambiguously aligned with its row and is not recorded. Knowledge-foundation archive "
                + "item MAT-CU-C95400."),

        CdaAlloy(CopperC10100, "C10100 oxygen-free electronic copper rod, hard", "C10100", "H04 (hard, 35% cold work)",
            "Copper - Oxygen-Free Electronic (OFE)", "Mechanical properties (rod, H04, 1 in. section) and physical properties",
            utsKsi: 48, ysKsi: 44, elongation: 16, fatigueKsi: 17, densityLbIn3: 0.323, cteF: 9.4, modulusKsi: 17000,
            conductivityBtu: 226, typical: true,
            notes: "Typical values for hard (H04) rod, 1 in. section, 35% cold work; annealed copper is far weaker "
                + "(yield about 10 ksi). CTE 68-212 degF. Electrical conductivity 101% IACS minimum published but not "
                + "modelled. Knowledge-foundation archive item 'C10100 OFHC copper'."),
    ];

    private static ReferenceSeedRecord<MaterialDefinition> CdaAlloy(
        string recordId,
        string name,
        string designation,
        string condition,
        string classification,
        string location,
        double utsKsi,
        double ysKsi,
        double elongation,
        double? fatigueKsi,
        double densityLbIn3,
        double cteF,
        double modulusKsi,
        double conductivityBtu,
        bool typical,
        string notes)
    {
        ReferenceQuantityValue Strength(double ksi, string label) => typical
            ? Typ(Ksi(ksi), $"Typical, {condition}; published as {ksi:0.#} ksi and recorded in psi.", label)
            : Lim(Ksi(ksi), $"Minimum for standard, {condition}; published as {ksi:0.#} ksi and recorded in psi.", label);

        var properties = new List<(string, ReferenceQuantityValue)>
        {
            (Uts, Strength(utsKsi, "Tensile Strength")),
            (Yield, Strength(ysKsi, "YS-0.5% Ext")),
            (Elongation, typical
                ? Typ(Pct(elongation), $"Typical, {condition}.")
                : Lim(Pct(elongation), $"Minimum for standard, {condition}.", "Elongation")),
            (Density, Typ(LbPerIn3(densityLbIn3), "At 68 degF, as published.")),
            (Cte, Typ(MicroPerF(cteF), "As published (per degF; see notes for the temperature range).")),
            (Modulus, Typ(Ksi(modulusKsi), $"Modulus of elasticity in tension, published as {modulusKsi:0} ksi.")),
            (Conductivity, Typ(BtuPerHFtF(conductivityBtu), "At 68 degF, as published.")),
        };

        if (fatigueKsi is { } f)
            properties.Add((Fatigue, Typ(Ksi(f), $"Typical fatigue strength at 100x10^6 cycles, {condition}; "
                + $"published as {f:0.#} ksi.", "Fatigue Strength")));

        return Mat(recordId,
            new MaterialDefinition
            {
                Name = name,
                Family = MaterialFamily.CopperAlloy,
                Designation = designation,
                Grade = $"UNS {designation}",
                Condition = condition,
                SourceClassification = classification,
                Properties = Props([.. properties]),
                Notes = notes,
            },
            SeedSources.CopperDevelopmentAssociation(designation, location),
            "Copper Development Association Inc.", $"CDA alloy data — {designation}", location);
    }
}
