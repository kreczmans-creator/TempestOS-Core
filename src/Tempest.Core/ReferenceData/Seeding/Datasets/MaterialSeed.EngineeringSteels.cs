using Tempest.Core.Materials;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// Engineering (machinery) steels: EN 10083/10277 grades and the AISI/SAE
/// low-alloy grades the knowledge-foundation archive named.
/// </summary>
/// <remarks>
/// <para>
/// <b>A quenched-and-tempered steel has no single strength.</b> Its limits
/// fall with ruling section. Each record holds the 16 to 40 mm band — the
/// commonest bar size for shafts and pins — and states every other band in
/// the value's own conditions, so a part outside that band is visibly
/// outside the record.
/// </para>
/// <para>
/// <b>British Standard names are carried as mappings, not as records.</b>
/// EN8, EN19 and EN24 (BS 970 080M40, 709M40, 817M40) are the names the
/// knowledge-foundation archive and UK practice use; the archive's own
/// mapping file marks them "commonly compared, medium confidence". They are
/// recorded on the EN record they compare with, not as separate materials,
/// because a separate record would claim a separate set of limits nobody
/// has published.
/// </para>
/// </remarks>
public sealed partial class MaterialSeed
{
    /// <summary>The identity of the C45 (EN8-comparable) record.</summary>
    public const string C45 = "mat-c45";

    /// <summary>The identity of the 42CrMo4 +QT (EN19-comparable) record.</summary>
    public const string Steel42CrMo4 = "mat-42crmo4-qt";

    /// <summary>The identity of the 34CrNiMo6 +QT (EN24-comparable) record.</summary>
    public const string Steel34CrNiMo6 = "mat-34crnimo6-qt";

    /// <summary>The identity of the 51CrV4 +QT record.</summary>
    public const string Steel51CrV4 = "mat-51crv4-qt";

    /// <summary>The identity of the 11SMn30 +C free-cutting steel record.</summary>
    public const string Steel11SMn30 = "mat-11smn30-c";

    /// <summary>The identity of the AISI 4130 record.</summary>
    public const string Aisi4130 = "mat-aisi-4130";

    /// <summary>The identity of the AISI 4140 record.</summary>
    public const string Aisi4140 = "mat-aisi-4140";

    /// <summary>The identity of the AISI 4340 annealed record.</summary>
    public const string Aisi4340 = "mat-aisi-4340-annealed";

    /// <summary>The identity of the AISI 8620 annealed record.</summary>
    public const string Aisi8620 = "mat-aisi-8620-annealed";

    private static (string, ReferenceQuantityValue)[] OvakoPhysicals(string grade, string slug, string revised) =>
    [
        (Modulus, SupplementaryValue(GPa(210), OvakoRef(grade, slug, revised) + ": Young's modulus 210 GPa.",
            ReferenceValueOrigin.ManufacturerCatalogue)),
        (Poisson, SupplementaryValue(Ratio(0.3), OvakoRef(grade, slug, revised) + ": Poisson's ratio 0.3.",
            ReferenceValueOrigin.ManufacturerCatalogue)),
        (Shear, SupplementaryValue(GPa(80), OvakoRef(grade, slug, revised) + ": shear modulus 80 GPa.",
            ReferenceValueOrigin.ManufacturerCatalogue)),
        (Density, SupplementaryValue(KgPerM3(7800), OvakoRef(grade, slug, revised) + ": density 7800 kg/m3.",
            ReferenceValueOrigin.ManufacturerCatalogue)),
        (Cte, SupplementaryValue(MicroPerK(12), OvakoRef(grade, slug, revised) + ": average CTE 20-300 degC, "
            + "12 um/(m.K).", ReferenceValueOrigin.ManufacturerCatalogue)),
        (Conductivity, SupplementaryValue(WPerMK(40), OvakoRef(grade, slug, revised) + ": thermal conductivity at "
            + "ambient temperature 40-45 W/(m.K); lower end recorded.", ReferenceValueOrigin.ManufacturerCatalogue)),
        (HeatCapacity, SupplementaryValue(JPerKgK(460), OvakoRef(grade, slug, revised) + ": specific heat capacity "
            + "50/100 degC 460-480 J/(kg.K); lower end recorded.", ReferenceValueOrigin.ManufacturerCatalogue)),
    ];

    private static string OvakoRef(string grade, string slug, string revised) =>
        $"Ovako AB, Steel Navigator material data sheet {grade} (https://steelnavigator.ovako.com/steel-grades/{slug}/pdf, "
        + $"{revised}), physical properties block";

    private static IEnumerable<ReferenceSeedRecord<MaterialDefinition>> EngineeringSteels() =>
    [
        Mat(C45,
            new MaterialDefinition
            {
                Name = "C45 non-alloy engineering steel, normalised",
                Family = MaterialFamily.Steel,
                Designation = "C45",
                Grade = "1.0503",
                Condition = "+N (normalised)",
                SourceClassification = "Non-alloy steel for general engineering / quenching and tempering",
                Standards = [new StandardReference("EN 10277-2", StandardSeed.En10277Part2, "CEN", "2008",
                    "The standard the SteelNumber page states the grade against")],
                Properties = Props(
                [
                    (Yield, Lim(MPa(305), "Minimum upper yield strength, normalised (+N), for thickness to 100 mm (the "
                        + "page's +N yield bands are: to 100 mm 305, 100-250 mm 275, 250-500 mm 240, 500-1000 mm 230 MPa). "
                        + "Cold-drawn (+C) bar is far stronger and band-dependent: Rp0.2 565 MPa at 5-10 mm falling to "
                        + "310 MPa at 63-100 mm.", "Re (+N)")),
                    (Uts, Lim(MPa(580), "Minimum tensile strength, normalised (+N), 16-100 mm (620 MPa to 16 mm, "
                        + "560 MPa to 250 mm).", "Rm (+N)")),
                    (Elongation, Lim(Pct(16), "Minimum A, normalised (+N), 16-100 mm (14% to 16 mm).", "A (+N)")),
                    .. OvakoPhysicals("C45", "c45", "Last revised: Thu, 30 Jan 2025"),
                ]),
                ProcessingNotes = "Normalised properties recorded. Quenched and tempered, cold drawn and annealed "
                    + "conditions have different limits on the same page and must be checked against it.",
                Notes = "Designation mapping carried over from the knowledge-foundation archive "
                    + "(MAP-EN8-C45, legacy designation comparison, medium confidence): BS 970 080M40 (EN8) is commonly "
                    + "compared with C45 — verify product, standard and condition before treating them as the same. "
                    + "Ovako's own similar-designation list for C45 includes SAE 1045 and 080A42. Physical properties are "
                    + "Ovako's published block for its C45 variants.",
            },
            SeedSources.SteelNumber("C45 (1.0503)", 152, "Mechanical properties table (+N, +C, +A)"),
            "SteelNumber (European steel and alloy grades database)", "SteelNumber grade page — C45 (1.0503)",
            "Mechanical properties table"),

        Mat(Steel42CrMo4,
            new MaterialDefinition
            {
                Name = "42CrMo4 low-alloy steel, quenched and tempered",
                Family = MaterialFamily.Steel,
                Designation = "42CrMo4",
                Grade = "1.7225",
                Condition = "+QT (quenched and tempered)",
                SourceClassification = "Alloy steel for quenching and tempering",
                Standards = [new StandardReference("EN 10083-3", StandardSeed.En10083Part3, "CEN", "2006",
                    "Quenched-and-tempered limits by ruling section, as the SteelNumber page states them")],
                Properties = Props(
                [
                    (Yield, Lim(MPa(750), "Minimum Re/Rp0.2, +QT, ruling diameter 16-40 mm. Other bands on the page: "
                        + "900 MPa to 16 mm, 650 MPa for 40-100 mm, 500-550 MPa for 100-160 mm, 460-500 MPa for "
                        + "160-330 mm, 390 MPa for 330-660 mm.", "Rp0.2 (+QT)")),
                    (Uts, Lim(MPa(1000), "Lower limit of 1000-1200 MPa, +QT, 16-40 mm (1100-1300 MPa to 16 mm, "
                        + "900-1100 MPa for 40-100 mm, 800-950 MPa for 100-160 mm, 750-900 MPa for 160-250 mm).", "Rm (+QT)")),
                    (Elongation, Lim(Pct(11), "Minimum A, +QT, round products 16-40 mm (10% to 16 mm, 12% to 100 mm).",
                        "A (+QT)")),
                    .. OvakoPhysicals("42CrMo4", "42crmo4", "Last revised: Tue, 03 Feb 2026"),
                ]),
                ProcessingNotes = "Through-hardens in oil to about 60 mm per the Ovako sheet. Soft-annealed (+A) "
                    + "material is much weaker (Rm 620 MPa, Rp0.2 480 MPa for 0.3-3 mm strip on the same page).",
                Notes = "Designation mappings carried over from the knowledge-foundation archive: MAP-42CRMO4-4140 "
                    + "(AISI/SAE 4140, commonly compared, medium confidence — not automatic certification equivalence). "
                    + "The archive's EN19 record (BS 970 709M40) is commonly compared with this grade; Ovako's own similar "
                    + "list names 708M40, AISI 4140 and 42CrMoS4. Physical properties are Ovako's published block.",
            },
            SeedSources.SteelNumber("42CrMo4 (1.7225)", 335, "Mechanical properties table (+QT by ruling diameter)"),
            "SteelNumber (European steel and alloy grades database)", "SteelNumber grade page — 42CrMo4 (1.7225)",
            "Mechanical properties table"),

        Mat(Steel34CrNiMo6,
            new MaterialDefinition
            {
                Name = "34CrNiMo6 low-alloy steel, quenched and tempered",
                Family = MaterialFamily.Steel,
                Designation = "34CrNiMo6",
                Grade = "1.6582",
                Condition = "+QT (quenched and tempered)",
                SourceClassification = "Quenched and tempered steel",
                Standards = [new StandardReference("EN 10083-3", StandardSeed.En10083Part3, "CEN", null,
                    "The sheet states its +QT table is according to DIN EN 10083-3")],
                Properties = Props(
                [
                    (Yield, Lim(MPa(900), "Minimum yield strength, +QT, 16 < d <= 40 mm (1000 MPa to 16 mm, 800 MPa to "
                        + "100 mm, 700 MPa to 160 mm, 600 MPa to 250 mm).", "Yield strength (+QT)")),
                    (Uts, Lim(MPa(1100), "Lower limit of 1100-1300 MPa, +QT, 16 < d <= 40 mm (1200-1400 MPa to 16 mm, "
                        + "1000-1200 MPa to 100 mm, 900-1100 MPa to 160 mm, 800-950 MPa to 250 mm).", "Tensile strength (+QT)")),
                    (Elongation, Lim(Pct(10), "Minimum, L0 = 5 d0, +QT, 16 < d <= 40 mm.", "Elongation")),
                    (Impact, Lim(Joule(45), "ISO-V minimum, +QT, 16 < d <= 250 mm (no requirement stated to 16 mm).",
                        "Notch impact energy ISO-V")),
                    (Density, Mfr(KgPerM3(7730), "7.73 kg/dm3, as the sheet states it.")),
                    (Modulus, Mfr(GPa(210), "Young's modulus as the sheet states it.")),
                    (Conductivity, Mfr(WPerMK(42.6), "At 20 degC.")),
                    (HeatCapacity, Mfr(JPerKgK(470), "At 20 degC.")),
                    (Cte, Mfr(MicroPerK(11.1), "Mean 20-100 degC in the soft-annealed condition (12.1 to 200 degC, "
                        + "12.9 to 300 degC, 13.5 to 400 degC).")),
                    (Poisson, SupplementaryValue(Ratio(0.3), OvakoRef("34CrNiMo6", "34crnimo6",
                        "Last revised: Thu, 05 Feb 2026") + ": Poisson's ratio 0.3. The Swiss Steel sheet states none.",
                        ReferenceValueOrigin.ManufacturerCatalogue)),
                ]),
                ProcessingNotes = "Difficult to weld; the sheet advises against welded construction.",
                Notes = "Designations listed by the sheet: BS 816M40 / 817M40, AFNOR 35NCD6, JIS SNCM447, AISI/SAE "
                    + "4337 / 4340. Mapping carried over from the knowledge-foundation archive: MAP-EN24-4340 (legacy "
                    + "designation comparison, medium confidence); EN24 is BS 970 817M40, which the sheet lists.",
            },
            SeedSources.SwissSteel("Firmodur 6582 34CrNiMo6 1.6582",
                "https://swisssteel-group.com/content-media/documents/Data-Sheets/Engineering-Steel/1.6582_en.pdf",
                "Physical properties and mechanical properties in quenched and tempered condition tables",
                "19/12/2018 2018-0017"),
            "Swiss Steel Group (Deutsche Edelstahlwerke)", "Technical data sheet Firmodur 6582 34CrNiMo6 1.6582",
            "Mechanical properties in quenched and tempered condition (+QT)"),

        Mat(Steel51CrV4,
            new MaterialDefinition
            {
                Name = "51CrV4 chromium-vanadium steel, quenched and tempered",
                Family = MaterialFamily.Steel,
                Designation = "51CrV4",
                Grade = "1.8159",
                Condition = "+QT (quenched and tempered)",
                SourceClassification = "Steel for quenching and tempering according to DIN EN 10083",
                Properties = Props(
                [
                    (Yield, Mfr(MPa(800), "Minimum 0.2% proof stress, +QT, 16 < d <= 40 mm (900 MPa below 16 mm, 700 MPa "
                        + "to 100 mm, 650 MPa to 160 mm, 600 MPa to 250 mm).", "Rp0.2 min")),
                    (Uts, Mfr(MPa(1000), "Lower limit of 1000-1200 MPa, +QT, 16 < d <= 40 mm.", "Rm")),
                    (Elongation, Mfr(Pct(10), "Minimum A5, +QT, 16 < d <= 40 mm.", "A5 min")),
                    (Impact, Mfr(Joule(30), "ISO-V minimum, +QT, all bands.", "ISO-V min")),
                    .. OvakoPhysicals("51CrV4", "51crv4-en100892002", "Last revised: Thu, 30 Jan 2025"),
                ]),
                Notes = "International grades listed by the sheet: BS 735A51/735M50/735H51, AFNOR 50CrV4/51CV4, SAE 6150. "
                    + "Spring and heat-treatable steel; soft annealed (+A) max 248 HB.",
            },
            SeedSources.Saarstahl("51CrV4 (50CrV4) Material No. 1.8159",
                "https://en.saarstahl.com/app/uploads/2024/03/20160323093138-51CrV4-50CrV4.pdf",
                "Mechanical properties, quenched and tempered +QT"),
            "Saarstahl AG", "Material specification sheet 51CrV4 (50CrV4) 1.8159",
            "Mechanical properties, quenched and tempered (+QT)"),

        Mat(Steel11SMn30,
            new MaterialDefinition
            {
                Name = "11SMn30 free-cutting steel, cold drawn",
                Family = MaterialFamily.Steel,
                Designation = "11SMn30",
                Grade = "1.0715",
                Condition = "+C (cold drawn)",
                SourceClassification = "Free-cutting steel",
                Standards = [new StandardReference("EN 10277-3", StandardSeed.En10277Part3, "CEN", "2008",
                    "Bright free-cutting steels; limits as the SteelNumber page states them")],
                Properties = Props(
                [
                    (Yield, Lim(MPa(375), "Minimum Rp0.2, cold drawn (+C), 16-40 mm (440 MPa at 5-10 mm, 410 MPa to 16 mm, "
                        + "305 MPa to 63 mm, 245 MPa to 100 mm).", "Rp0.2 (+C)")),
                    (Uts, Lim(MPa(460), "Lower limit of 460-710 MPa, +C, 16-40 mm.", "Rm (+C)")),
                    (Elongation, Lim(Pct(8), "Minimum A, +C, 16-40 mm.", "A (+C)")),
                    (Density, SupplementaryValue(GramPerCc(7.85), "Siderticino SA, 11SMnPb30/37 steel datasheet "
                        + "(https://siderticino.it/en/steel-datasheets/11smnpb30-37/), physical properties table: 7.85 g/cm3 "
                        + "for the leaded sister grade, stated as indicative. The 11SMn30 page publishes no density.",
                        ReferenceValueOrigin.EngineeringReference)),
                    (Modulus, SupplementaryValue(GPa(205), "Siderticino SA, 11SMnPb30/37 datasheet "
                        + "(https://siderticino.it/en/steel-datasheets/11smnpb30-37/): E 205-210 GPa "
                        + "(indicative, leaded sister grade); lower end recorded.", ReferenceValueOrigin.EngineeringReference)),
                ]),
                ProcessingNotes = "Not intended for heat treatment. Excellent machinability; poor weldability.",
                Notes = "NO THERMAL EXPANSION COEFFICIENT RECORDED: none of the sources read for this grade publishes "
                    + "one, so the thermal-expansion calculator will refuse this record rather than use a borrowed figure. "
                    + "BS 970 230M07 is the British grade the PO named for this slot; the sources read do not state a "
                    + "230M07 equivalence (Saarstahl lists ~SAE 1213/1215 and JIS SUM 22/23), so none is claimed.",
            },
            SeedSources.SteelNumber("11SMn30 (1.0715)", 155, "Mechanical properties table (+C by thickness)"),
            "SteelNumber (European steel and alloy grades database)", "SteelNumber grade page — 11SMn30 (1.0715)",
            "Mechanical properties table"),

        Mat(Aisi4130,
            new MaterialDefinition
            {
                Name = "AISI 4130 chromium-molybdenum alloy steel",
                Family = MaterialFamily.Steel,
                Designation = "AISI 4130",
                Grade = "UNS G41300",
                SourceClassification = "Low-alloy steel",
                Properties = Props(
                [
                    (Yield, Typ(MPa(460), "Typical, as the article tabulates it. The article does not state the heat "
                        + "treatment condition of its table.", "Tensile strength, yield")),
                    (Uts, Typ(MPa(560), "Typical, condition not stated by the article.", "Tensile strength, ultimate")),
                    (Elongation, Typ(Pct(21.5), "In 50 mm, typical.")),
                    (Density, Typ(GramPerCc(7.85), "As the article states it.")),
                    (Modulus, Typ(GPa(190), "The article gives 190-210 GPa; lower end recorded.")),
                    (Shear, Typ(GPa(80), "Typical for steel, per the article.")),
                    (Poisson, Typ(Ratio(0.27), "The article gives 0.27-0.30; lower end recorded.")),
                    (Conductivity, Typ(WPerMK(42.7), "At 100 degC.")),
                    (BrinellHardness, Typ(Ratio(217), "Brinell hardness number as tabulated.")),
                    (Cte, SupplementaryValue(MicroPerK(12), OvakoRef("25CrMo4", "25crmo4", "Last revised: Fri, 17 Jan 2025")
                        + ": average CTE 20-300 degC 12 um/(m.K). Ovako lists 4130 as a similar designation of 25CrMo4. "
                        + "The AZoM article states no expansion coefficient.", ReferenceValueOrigin.ManufacturerCatalogue)),
                ]),
                Notes = "CONDITION NOT STATED by the source table; the hardness (217 HB) suggests annealed or normalised "
                    + "bar but the article does not say so. Treat the strengths as indicative and confirm against the "
                    + "material certificate. Knowledge-foundation archive item MAT-STEEL-4130.",
            },
            SeedSources.Azom("AISI 4130 Alloy Steel (UNS G41300)", 6742, "Physical, mechanical and thermal properties tables"),
            "AZoM (AZO Materials)", "AISI 4130 Alloy Steel (UNS G41300)", "Mechanical properties table"),

        Mat(Aisi4140,
            new MaterialDefinition
            {
                Name = "AISI 4140 chromium-molybdenum alloy steel",
                Family = MaterialFamily.Steel,
                Designation = "AISI 4140",
                Grade = "UNS G41400",
                SourceClassification = "Low-alloy steel",
                Properties = Props(
                [
                    (Yield, Typ(MPa(415), "Typical, as the article tabulates it; condition not stated by the table.",
                        "Yield strength")),
                    (Uts, Typ(MPa(655), "Typical; condition not stated by the table.", "Tensile strength")),
                    (Elongation, Typ(Pct(25.7), "In 50 mm, typical.")),
                    (Density, Typ(GramPerCc(7.85), "As the article states it.")),
                    (Modulus, Typ(GPa(190), "The article gives 190-210 GPa; lower end recorded.")),
                    (Shear, Typ(GPa(80), "Typical for steel, per the article.")),
                    (Poisson, Typ(Ratio(0.27), "The article gives 0.27-0.30; lower end recorded.")),
                    (Cte, Typ(MicroPerK(12.2), "0-100 degC.")),
                    (Conductivity, Typ(WPerMK(42.6), "At 100 degC.")),
                    (BrinellHardness, Typ(Ratio(197), "Brinell hardness number as tabulated.")),
                ]),
                Notes = "CONDITION NOT STATED by the source table (197 HB suggests annealed). For quenched-and-tempered "
                    + "bar use the 42CrMo4 +QT record, which carries EN 10083-3 minima by ruling section. Knowledge-"
                    + "foundation archive item MAT-STEEL-4140; archive mapping MAP-42CRMO4-4140 (medium confidence).",
            },
            SeedSources.Azom("AISI 4140 Alloy Steel (UNS G41400)", 6769, "Physical, mechanical and thermal properties tables"),
            "AZoM (AZO Materials)", "AISI 4140 Alloy Steel (UNS G41400)", "Mechanical properties table"),

        Mat(Aisi4340,
            new MaterialDefinition
            {
                Name = "AISI 4340 nickel-chromium-molybdenum alloy steel, annealed",
                Family = MaterialFamily.Steel,
                Designation = "AISI 4340",
                Grade = "UNS G43400",
                Condition = "Annealed",
                SourceClassification = "Low-alloy steel",
                Properties = Props(
                [
                    (Yield, Typ(MPa(470), "Typical, annealed, as the article states its table.", "Yield strength")),
                    (Uts, Typ(MPa(745), "Typical, annealed.", "Tensile strength")),
                    (Elongation, Typ(Pct(22), "Typical, annealed.")),
                    (Density, Typ(GramPerCc(7.85), "As the article states it.")),
                    (Modulus, Typ(GPa(190), "The article gives 190-210 GPa; lower end recorded.")),
                    (Shear, Typ(GPa(80), "Typical for steel, per the article.")),
                    (Poisson, Typ(Ratio(0.27), "The article gives 0.27-0.30; lower end recorded.")),
                    (Cte, Typ(MicroPerK(12.3), "At 20 degC, specimen oil hardened and tempered at 600 degC, as the "
                        + "article states it.")),
                    (Conductivity, Typ(WPerMK(44.5), "Typical steel, per the article.")),
                    (BrinellHardness, Typ(Ratio(217), "Brinell hardness number, annealed.")),
                ]),
                Notes = "Annealed values only. Knowledge-foundation archive item MAT-STEEL-4340; archive mapping "
                    + "MAP-EN24-4340 (legacy comparison, medium confidence). For hardened and tempered bar use the "
                    + "34CrNiMo6 +QT record.",
            },
            SeedSources.Azom("AISI 4340 Alloy Steel (UNS G43400)", 6772, "Physical, mechanical (annealed) and thermal tables"),
            "AZoM (AZO Materials)", "AISI 4340 Alloy Steel (UNS G43400)", "Mechanical properties of annealed AISI 4340"),

        Mat(Aisi8620,
            new MaterialDefinition
            {
                Name = "AISI 8620 carburising alloy steel, annealed",
                Family = MaterialFamily.Steel,
                Designation = "AISI 8620",
                Grade = "UNS G86200",
                Condition = "Annealed",
                SourceClassification = "Carburising low-alloy steel",
                Properties = Props(
                [
                    (Yield, Typ(MPa(385), "Typical, annealed, as the article states its table.", "Yield strength")),
                    (Uts, Typ(MPa(530), "Typical, annealed.", "Tensile strength")),
                    (Density, Typ(GramPerCc(7.85), "As the article states it.")),
                    (Modulus, Typ(GPa(190), "The article gives 190-210 GPa; lower end recorded.")),
                    (Shear, Typ(GPa(80), "Typical for steel, per the article.")),
                    (Poisson, Typ(Ratio(0.27), "The article gives 0.27-0.30; lower end recorded.")),
                    (Conductivity, Typ(WPerMK(46.6), "As the article states it.")),
                    (BrinellHardness, Typ(Ratio(149), "Brinell hardness number, annealed.")),
                    (Cte, SupplementaryValue(MicroPerK(12), OvakoRef("20NiCrMo2-2", "20nicrmo2-2",
                        "Last revised: Thu, 10 Sep 2026") + ": average CTE 20-300 degC 12 um/(m.K). Ovako lists 8620 as a "
                        + "similar designation. The AZoM article states no expansion coefficient.",
                        ReferenceValueOrigin.ManufacturerCatalogue)),
                ]),
                Notes = "Annealed core values only; carburised case properties are not modelled. The article gives no "
                    + "elongation. Knowledge-foundation archive item MAT-STEEL-8620.",
            },
            SeedSources.Azom("AISI 8620 Alloy Steel (UNS G86200)", 6754, "Physical, mechanical (annealed) and thermal tables"),
            "AZoM (AZO Materials)", "AISI 8620 Alloy Steel (UNS G86200)", "Mechanical properties of annealed AISI 8620"),
    ];
}
