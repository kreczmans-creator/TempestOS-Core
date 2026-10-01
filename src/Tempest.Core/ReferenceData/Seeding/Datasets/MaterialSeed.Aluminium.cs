using Tempest.Core.Materials;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// Wrought aluminium alloys: EN grades from the Aalco datasheets (EN 755-2
/// and EN 485-2 minima restated) and the aerospace 2xxx/7xxx alloys from
/// Kaiser Aluminum's own technical data (typical values, with the R.R.
/// Moore fatigue endurance limit the calculators can use).
/// </summary>
/// <remarks>
/// New aluminium records carry the temper in their designation
/// ("6061-T6"), because the designation key must be unique and the same
/// alloy in two tempers is two materials. The two first-acquisition records
/// (6082 and 5083) keep their alloy-only designations so existing
/// installations are not disturbed.
/// </remarks>
public sealed partial class MaterialSeed
{
    /// <summary>The identity of the 6061-T6 record.</summary>
    public const string Aluminium6061T6 = "mat-6061-t6";

    /// <summary>The identity of the 6063-T6 record.</summary>
    public const string Aluminium6063T6 = "mat-6063-t6";

    /// <summary>The identity of the 6082-T651 plate record.</summary>
    public const string Aluminium6082T651 = "mat-6082-t651";

    /// <summary>The identity of the 5754-H22 record.</summary>
    public const string Aluminium5754H22 = "mat-5754-h22";

    /// <summary>The identity of the 1050A-H14 record.</summary>
    public const string Aluminium1050AH14 = "mat-1050a-h14";

    /// <summary>The identity of the 2014A-T6511 record.</summary>
    public const string Aluminium2014AT6511 = "mat-2014a-t6511";

    /// <summary>The identity of the 7075-T6/T651 record.</summary>
    public const string Aluminium7075T6 = "mat-7075-t6";

    /// <summary>The identity of the 2024-T351 record.</summary>
    public const string Aluminium2024T351 = "mat-2024-t351";

    /// <summary>The identity of the 7050-T7451 record.</summary>
    public const string Aluminium7050T7451 = "mat-7050-t7451";

    private static StandardReference En573Part3At2009 =>
        new("EN 573-3", StandardSeed.En573Part3, "CEN", "2009", "Alloy designation and composition");

    private static StandardReference En755Part2At2008 =>
        new("EN 755-2", StandardSeed.En755Part2, "CEN", "2008", "Mechanical property minima for extruded products");

    private static StandardReference En485Part2At2008 =>
        new("EN 485-2", StandardSeed.En485Part2, "CEN", "2008", "Mechanical property limits for sheet, strip and plate");

    private static IEnumerable<ReferenceSeedRecord<MaterialDefinition>> AluminiumAlloys() =>
    [
        Mat(Aluminium6061T6,
            new MaterialDefinition
            {
                Name = "6061-T6 wrought aluminium alloy, extruded",
                Family = MaterialFamily.Aluminium,
                Designation = "6061-T6",
                Grade = "EN AW-6061",
                Condition = "T6 (solution heat treated and artificially aged)",
                SourceClassification = "Aluminium Alloy - Commercial Alloy",
                Standards = [En573Part3At2009, En755Part2At2008],
                Properties = Props(
                [
                    (Yield, Lim(MPa(240), "Minimum proof stress, extrusions up to 200 mm diameter/across flats and 5 mm "
                        + "wall for tube and profile.", "Proof Stress min")),
                    (Uts, Lim(MPa(260), "Minimum, same band.", "Tensile Strength min")),
                    (BrinellHardness, Typ(Ratio(95), "Brinell hardness number, quoted without a limit qualifier.")),
                    (Density, Typ(GramPerCc(2.70), "As the datasheet states it.")),
                    (Melting, Typ(DegC(650), "As the datasheet states it.")),
                    (Cte, Typ(MicroPerK(23.4), "As the datasheet states it.")),
                    (Modulus, Typ(GPa(70), "As the datasheet states it.")),
                    (Conductivity, Typ(WPerMK(166), "As the datasheet states it.")),
                    (Fatigue, SupplementaryValue(MPa(97), "Kaiser Aluminum, 'Rod & Bar Alloy 6061 Technical Data' "
                        + "(https://online.kaiseraluminum.com/depot/PublicProductInformation/Document/1025/"
                        + "Kaiser_Aluminum_6061_Rod_and_Bar.pdf), typical fatigue endurance limit for T6/T651, R.R. Moore "
                        + "rotating beam, 5x10^8 cycles of reversed stress (14 ksi / 97 MPa). Kaiser's own product, typical "
                        + "not minimum.", ReferenceValueOrigin.ManufacturerCatalogue)),
                ]),
                Notes = "The datasheet publishes no elongation for this band. Electrical resistivity 0.040e-6 ohm.m "
                    + "published but not modelled. Knowledge-foundation archive item MAT-AL-6061-T6 (archive screening "
                    + "yield 276 MPa is a typical value; the EN 755-2 minimum restated here is 240 MPa).",
            },
            SeedSources.AalcoDatasheet("Aluminium Alloy - Commercial Alloy - 6061 - T6 Extrusions",
                "Aluminium-Alloy-6061-T6-Extrusions_145", "Physical properties; mechanical properties (extrusions)"),
            "Aalco Metals Limited", "Aluminium Alloy - Commercial Alloy - 6061 - T6 Extrusions",
            "Mechanical properties table (extrusions up to 200 mm)"),

        Mat(Aluminium6063T6,
            new MaterialDefinition
            {
                Name = "6063-T6 wrought aluminium alloy, extruded",
                Family = MaterialFamily.Aluminium,
                Designation = "6063-T6",
                Grade = "EN AW-6063",
                Condition = "T6 (solution heat treated and artificially aged)",
                SourceClassification = "Aluminium Alloy - Commercial Alloy",
                Standards = [En573Part3At2009, En755Part2At2008],
                Properties = Props(
                [
                    (Yield, Lim(MPa(170), "Minimum proof stress, rod and bar up to 150 mm (160 MPa for 150-200 mm; "
                        + "profiles 10-25 mm wall 160 MPa).", "Proof Stress min")),
                    (Uts, Lim(MPa(215), "Minimum, rod and bar up to 150 mm (195 MPa for 150-200 mm).", "Tensile Strength min")),
                    (Elongation, Lim(Pct(10), "Minimum A, rod and bar up to 150 mm (A50mm 8%).", "Elongation A min")),
                    (BrinellHardness, Typ(Ratio(75), "Brinell hardness number, quoted without a limit qualifier.")),
                    (Density, Typ(GramPerCc(2.70), "As the datasheet states it.")),
                    (Melting, Typ(DegC(655), "As the datasheet states it.")),
                    (Cte, Typ(MicroPerK(23.5), "As the datasheet states it.")),
                    (Modulus, Typ(GPa(69.5), "As the datasheet states it.")),
                    (Conductivity, Typ(WPerMK(201), "As the datasheet states it.")),
                ]),
                Notes = "The architectural extrusion alloy. Electrical resistivity 0.033e-6 ohm.m (52% IACS) published "
                    + "but not modelled.",
            },
            SeedSources.AalcoDatasheet("Aluminium Alloy - Commercial Alloy - 6063 - T6 Extrusions",
                "Aluminium-Alloy-6063-T6-Extrusions_158", "Physical properties; mechanical properties (rod and bar to 150 mm)"),
            "Aalco Metals Limited", "Aluminium Alloy - Commercial Alloy - 6063 - T6 Extrusions",
            "Mechanical properties table (rod and bar up to 150 mm)"),

        Mat(Aluminium6082T651,
            new MaterialDefinition
            {
                Name = "6082-T651 wrought aluminium alloy plate",
                Family = MaterialFamily.Aluminium,
                Designation = "6082-T651",
                Grade = "EN AW-6082",
                Condition = "T651 (solution heat treated, stress relieved by stretching, artificially aged)",
                SourceClassification = "Aluminium Alloy - Commercial Alloy",
                Standards = [En573Part3At2009, En485Part2At2008],
                Properties = Props(
                [
                    (Yield, Lim(MPa(240), "Minimum proof stress, plate 12.5-100 mm (255 MPa for 6-12.5 mm; 240 MPa for "
                        + "100-150 mm).", "Proof Stress min")),
                    (Uts, Lim(MPa(295), "Minimum, plate 12.5-100 mm (300 MPa for 6-12.5 mm; 275 MPa for 100-150 mm).",
                        "Tensile Strength min")),
                    (BrinellHardness, Typ(Ratio(89), "Brinell hardness number for 12.5-100 mm plate, quoted without a "
                        + "limit qualifier.")),
                    (Density, Typ(GramPerCc(2.70), "As the datasheet states it.")),
                    (Melting, Typ(DegC(555), "As the datasheet states it.")),
                    (Cte, Typ(MicroPerK(24), "As the datasheet states it.")),
                    (Modulus, Typ(GPa(70), "As the datasheet states it.")),
                    (Conductivity, Typ(WPerMK(180), "As the datasheet states it.")),
                ]),
                Notes = "Archive mapping MAP-6082-T6-T651 (same alloy, different condition — properties must not be "
                    + "merged): this is the plate record; extruded T6 is mat-6082-t6. The source gives no elongation for "
                    + "the 12.5-100 mm band (9% A50mm for 6-12.5 mm).",
            },
            SeedSources.AalcoDatasheet("Aluminium Alloy - Commercial Alloy - 6082 - T6/T651 Plate",
                "Aluminium-Alloy-6082-T6T651-Plate_148", "Physical properties; mechanical properties (plate bands)"),
            "Aalco Metals Limited", "Aluminium Alloy - Commercial Alloy - 6082 - T6/T651 Plate",
            "Mechanical properties table (plate 12.5 mm to 100 mm)"),

        Mat(Aluminium5754H22,
            new MaterialDefinition
            {
                Name = "5754-H22 wrought aluminium alloy sheet and plate",
                Family = MaterialFamily.Aluminium,
                Designation = "5754-H22",
                Grade = "EN AW-5754",
                Condition = "H22 (strain hardened and partially annealed, quarter hard)",
                SourceClassification = "Aluminium Alloy - Commercial Alloy",
                Standards = [En573Part3At2009, En485Part2At2008],
                Properties = Props(
                [
                    (Yield, Lim(MPa(130), "Minimum proof stress, sheet and plate 0.2-40 mm.", "Proof Stress min")),
                    (Uts, Lim(MPa(220), "Lower limit of 220-270 MPa, sheet and plate 0.2-40 mm.", "Tensile Strength")),
                    (Elongation, Lim(Pct(7), "Minimum A50mm.", "Elongation A50mm min")),
                    (BrinellHardness, Typ(Ratio(63), "Brinell hardness number, quoted without a limit qualifier.")),
                    (Density, Typ(GramPerCc(2.66), "As the datasheet states it.")),
                    (Melting, Typ(DegC(600), "As the datasheet states it.")),
                    (Cte, Typ(MicroPerK(24), "As the datasheet states it.")),
                    (Modulus, Typ(GPa(68), "As the datasheet states it.")),
                    (Conductivity, Typ(WPerMK(147), "As the datasheet states it.")),
                ]),
                EnvironmentalNotes = "Magnesium-bearing, non-heat-treatable; chosen for corrosion resistance and formability.",
                Notes = "Knowledge-foundation archive item MAT-AL-5754-H22.",
            },
            SeedSources.AalcoDatasheet("Aluminium Alloy - Commercial Alloy - 5754 - H22 Sheet and Plate",
                "Aluminium-Alloy-5754-H22-Sheet-and-Plate_153", "Physical properties; mechanical properties (0.2-40 mm)"),
            "Aalco Metals Limited", "Aluminium Alloy - Commercial Alloy - 5754 - H22 Sheet and Plate",
            "Mechanical properties table (sheet and plate 0.2 mm to 40 mm)"),

        Mat(Aluminium1050AH14,
            new MaterialDefinition
            {
                Name = "1050A-H14 commercially pure aluminium sheet",
                Family = MaterialFamily.Aluminium,
                Designation = "1050A-H14",
                Grade = "EN AW-1050A",
                Condition = "H14 (strain hardened, half hard)",
                SourceClassification = "Aluminium Alloy - Commercial Alloy",
                Standards = [En573Part3At2009, En485Part2At2008],
                Properties = Props(
                [
                    (Yield, Lim(MPa(85), "Minimum proof stress, sheet 0.2-6 mm.", "Proof Stress min")),
                    (Uts, Lim(MPa(105), "Lower limit of 105-145 MPa, sheet 0.2-6 mm.", "Tensile Strength")),
                    (Elongation, Lim(Pct(12), "Minimum A.", "Elongation A min")),
                    (BrinellHardness, Typ(Ratio(34), "Brinell hardness number, quoted without a limit qualifier.")),
                    (Density, Typ(GramPerCc(2.71), "As the datasheet states it.")),
                    (Melting, Typ(DegC(650), "As the datasheet states it.")),
                    (Cte, Typ(MicroPerK(24), "As the datasheet states it.")),
                    (Modulus, Typ(GPa(71), "As the datasheet states it.")),
                    (Conductivity, Typ(WPerMK(222), "As the datasheet states it.")),
                ]),
                Notes = "Knowledge-foundation archive item MAT-AL-1050A-H14. Electrical resistivity 0.0282e-6 ohm.m "
                    + "published but not modelled.",
            },
            SeedSources.AalcoDatasheet("Aluminium Alloy - Commercial Alloy - 1050A - H14 Sheet",
                "Aluminium-Alloy-1050A-H14-Sheet_57", "Physical properties; mechanical properties (sheet 0.2-6 mm)"),
            "Aalco Metals Limited", "Aluminium Alloy - Commercial Alloy - 1050A - H14 Sheet",
            "Mechanical properties table (sheet 0.2 mm to 6 mm)"),

        Mat(Aluminium2014AT6511,
            new MaterialDefinition
            {
                Name = "2014A-T6511 wrought aluminium-copper alloy, extruded",
                Family = MaterialFamily.Aluminium,
                Designation = "2014A-T6511",
                Grade = "EN AW-2014A",
                Condition = "T6511 (solution heat treated, stress relieved by stretching, artificially aged, minor straightening)",
                SourceClassification = "Aluminium Alloy - Commercial Alloy",
                Standards = [En573Part3At2009, En755Part2At2008],
                Properties = Props(
                [
                    (Yield, Lim(MPa(415), "Minimum proof stress, bar 25-75 mm (370 MPa to 25 mm, 420 MPa for 75-150 mm, "
                        + "350 MPa for 150-200 mm).", "Proof Stress min")),
                    (Uts, Lim(MPa(460), "Minimum, bar 25-75 mm (415 MPa to 25 mm, 465 MPa for 75-150 mm, 430 MPa for "
                        + "150-200 mm).", "Tensile Strength min")),
                    (Elongation, Lim(Pct(7), "Minimum A, bar 25-75 mm.", "Elongation A min")),
                    (BrinellHardness, Typ(Ratio(140), "Brinell hardness number, quoted without a limit qualifier.")),
                    (Density, Typ(GramPerCc(2.82), "As the datasheet states it.")),
                    (Melting, Typ(DegC(535), "As the datasheet states it.")),
                    (Cte, Typ(MicroPerK(23), "As the datasheet states it.")),
                    (Modulus, Typ(GPa(71), "As the datasheet states it.")),
                    (Conductivity, Typ(WPerMK(138), "As the datasheet states it.")),
                    (Fatigue, SupplementaryValue(MPa(125), "Kaiser Aluminum, 'Rod & Bar Alloy 2014 Technical Data' "
                        + "(https://online.kaiseraluminum.com/depot/PublicProductInformation/Document/1029/"
                        + "Kaiser_Aluminum_2014_Rod_and_Bar.pdf), typical fatigue endurance limit for 2014-T6/T651, R.R. "
                        + "Moore, 5x10^8 cycles (18 ksi / 125 MPa). Kaiser's figure is for AA 2014, the close relative of "
                        + "EN AW-2014A; typical, not minimum.", ReferenceValueOrigin.ManufacturerCatalogue)),
                ]),
                Notes = "The datasheet's chemical composition heading cites alloy 2014 under EN 573-3. Knowledge-foundation "
                    + "archive item MAT-AL-2014-T6.",
            },
            SeedSources.AalcoDatasheet("Aluminium Alloy - Commercial Alloy - 2014A - T6511 Extrusion",
                "Aluminium-Alloy-2014A-T6511-Extrusion_342", "Physical properties; mechanical properties (bar 25-75 mm)"),
            "Aalco Metals Limited", "Aluminium Alloy - Commercial Alloy - 2014A - T6511 Extrusion",
            "Mechanical properties table (bar 25 mm to 75 mm)"),

        KaiserAluminium(Aluminium7075T6, "7075-T6 wrought aluminium-zinc alloy", "7075-T6", "AA 7075 / UNS A97075",
            "T6 / T651 (solution heat treated and artificially aged)", "Rod & Bar Alloy 7075 Technical Data",
            "1028/Kaiser_Aluminum_7075_Rod_and_Bar.pdf", "Typical mechanical properties and typical physical properties tables",
            ultimate: 572, yieldMpa: 503, elongation: 11, brinell: 150, fatigue: 158, modulus: 71.0, density: 2.80,
            cte: 23.4, conductivity: 130, heat: 960,
            notes: "Typical values for T6/T651, 0.500 in. diameter specimen; Kaiser's own product. Fatigue is the R.R. "
                + "Moore endurance limit at 5x10^8 cycles. Corrosion: rated C (general and stress corrosion) by the "
                + "sheet — protect faying surfaces. Not commonly weldable. Knowledge-foundation archive items "
                + "MAT-AL-7075-T6 / MAT-AL-7075-T651."),

        KaiserAluminium(Aluminium2024T351, "2024-T351 wrought aluminium-copper alloy", "2024-T351", "AA 2024 / UNS A92024",
            "T4 / T351 (solution heat treated, stress relieved by stretching, naturally aged)",
            "Rod & Bar Alloy 2024 Technical Data", "1022/Kaiser_Aluminum_2024_Rod_and_Bar.pdf",
            "Typical mechanical properties and typical physical properties tables",
            ultimate: 469, yieldMpa: 324, elongation: 19, brinell: 120, fatigue: 138, modulus: 73.1, density: 2.77,
            cte: 22.9, conductivity: 120, heat: 875,
            notes: "Typical values for T4/T351 rod and bar; Kaiser's own product. Fatigue is the R.R. Moore endurance "
                + "limit at 5x10^8 cycles. Knowledge-foundation archive items MAT-AL-2024-T3 / MAT-AL-2024-T351 (the "
                + "T3 sheet temper is not covered by this rod-and-bar sheet)."),

        KaiserAluminium(Aluminium7050T7451, "7050-T7451 wrought aluminium-zinc alloy plate", "7050-T7451", "AA 7050 / UNS A97050",
            "T7451 (solution heat treated, stress relieved by stretching, overaged)", "Sheet Coil & Plate Alloy 7050 Technical Data",
            "1016/Kaiser_Aluminum_7050_Sheet_Coil_and_Plate.pdf",
            "Typical mechanical properties (longitudinal) and typical physical properties tables",
            ultimate: 524, yieldMpa: 469, elongation: 11, brinell: 140, fatigue: null, modulus: 70.3, density: 2.83,
            cte: 23.5, conductivity: 157, heat: 860,
            notes: "Typical longitudinal values for T7451 plate; Kaiser's own product. The sheet publishes no fatigue "
                + "endurance limit for 7050. Better exfoliation and stress-corrosion resistance than 7075-T6. Knowledge-"
                + "foundation archive item MAT-AL-7050-T7451."),
    ];

    private static ReferenceSeedRecord<MaterialDefinition> KaiserAluminium(
        string recordId,
        string name,
        string designation,
        string grade,
        string condition,
        string title,
        string path,
        string location,
        double ultimate,
        double yieldMpa,
        double elongation,
        double brinell,
        double? fatigue,
        double modulus,
        double density,
        double cte,
        double conductivity,
        double heat,
        string notes)
    {
        var properties = new List<(string, ReferenceQuantityValue)>
        {
            (Yield, Mfr(MPa(yieldMpa), "Typical, as the manufacturer tabulates it (0.500 in. diameter specimen). "
                + "Typical, not a specification minimum.", "Tensile, Yield")),
            (Uts, Mfr(MPa(ultimate), "Typical, as the manufacturer tabulates it.", "Tensile, Ultimate")),
            (Elongation, Mfr(Pct(elongation), "Typical, elongation in 4D.")),
            (BrinellHardness, Mfr(Ratio(brinell), "Typical Brinell hardness, 500 kg load, 10 mm ball.")),
            (Modulus, Mfr(GPa(modulus), "Typical modulus of elasticity.")),
            (Density, Mfr(GramPerCc(density), "Nominal density at 20 degC (published as Mg/m3).")),
            (Cte, Mfr(MicroPerK(cte), "Linear, 20-100 degC.")),
            (Conductivity, Mfr(WPerMK(conductivity), "At 20 degC, for this temper.")),
            (HeatCapacity, Mfr(JPerKgK(heat), "At 100 degC.")),
        };

        if (fatigue is { } f)
            properties.Add((Fatigue, Mfr(MPa(f), "Typical fatigue endurance limit, R.R. Moore rotating beam, "
                + "5x10^8 cycles of reversed stress.", "Fatigue endurance limit")));

        var url = "https://online.kaiseraluminum.com/depot/PublicProductInformation/Document/" + path;

        return Mat(recordId,
            new MaterialDefinition
            {
                Name = name,
                Family = MaterialFamily.Aluminium,
                Designation = designation,
                Grade = grade,
                Condition = condition,
                SourceClassification = "Heat-treatable aerospace aluminium alloy",
                Supplier = "Kaiser Aluminum",
                SupplierDesignation = designation,
                Properties = Props([.. properties]),
                Notes = notes,
            },
            SeedSources.Kaiser(title, url, location),
            "Kaiser Aluminum", title, location);
    }
}
