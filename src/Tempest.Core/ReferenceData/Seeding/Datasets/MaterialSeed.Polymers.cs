using Tempest.Core.Materials;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// Engineering and general thermoplastics, from the semi-finished-product
/// manufacturers' own data (Ensinger, Röchling, Röhm).
/// </summary>
/// <remarks>
/// <para>
/// <b>Every value is one manufacturer's stock shape.</b> A polymer's
/// properties depend on grade, processing and moisture as much as on its
/// chemistry; these records name the product they describe (TECAFORM AH
/// natural, Trovidur PVC-U and so on) and are typical values at 23 degC,
/// dry, not specification limits.
/// </para>
/// <para>
/// <b>Stress calculators read yield; not every polymer publishes one.</b>
/// PTFE and cast PMMA records carry no yield strength because their
/// manufacturers publish none (PTFE creeps rather than yields; cast PMMA
/// fails brittle). The records say so, and the calculators will refuse them
/// rather than borrow a figure.
/// </para>
/// </remarks>
public sealed partial class MaterialSeed
{
    /// <summary>The identity of the POM-C record.</summary>
    public const string PolymerPomC = "mat-pom-c";

    /// <summary>The identity of the PA 6 record.</summary>
    public const string PolymerPa6 = "mat-pa6";

    /// <summary>The identity of the PA 66 record.</summary>
    public const string PolymerPa66 = "mat-pa66";

    /// <summary>The identity of the PA 66 GF30 record.</summary>
    public const string PolymerPa66Gf30 = "mat-pa66-gf30";

    /// <summary>The identity of the PEEK record.</summary>
    public const string PolymerPeek = "mat-peek";

    /// <summary>The identity of the polycarbonate record.</summary>
    public const string PolymerPc = "mat-pc";

    /// <summary>The identity of the PEI record.</summary>
    public const string PolymerPei = "mat-pei";

    /// <summary>The identity of the PPS record.</summary>
    public const string PolymerPps = "mat-pps";

    /// <summary>The identity of the ABS record.</summary>
    public const string PolymerAbs = "mat-abs";

    /// <summary>The identity of the PTFE record.</summary>
    public const string PolymerPtfe = "mat-ptfe";

    /// <summary>The identity of the UHMW-PE record.</summary>
    public const string PolymerUhmwPe = "mat-pe-uhmw";

    /// <summary>The identity of the HDPE record.</summary>
    public const string PolymerHdpe = "mat-pe-hd";

    /// <summary>The identity of the polypropylene record.</summary>
    public const string PolymerPp = "mat-pp";

    /// <summary>The identity of the PVC-U record.</summary>
    public const string PolymerPvcU = "mat-pvc-u";

    /// <summary>The identity of the cast PMMA record.</summary>
    public const string PolymerPmma = "mat-pmma-cast";

    private const string EnsingerShapes = "https://www.ensingerplastics.com/en/shapes/";

    private const string EnsingerShapesGb = "https://www.ensingerplastics.com/en-gb/shapes/";

    private static IEnumerable<ReferenceSeedRecord<MaterialDefinition>> Polymers() =>
    [
        Ensinger(PolymerPomC, "POM-C acetal copolymer (TECAFORM AH natural)", "POM-C", "TECAFORM AH natural",
            EnsingerShapes + "acetal-tecaform-ah-natural", density: 1.41, modulus: 2800, uts: 67, yieldMpa: 67,
            elongation: 32, cte: 130, conductivity: 0.39, heat: 1.4, melting: 166, serviceLong: 100, shoreD: 82,
            archive: "MAT-POM / MAT-POLY-POM (POM unfilled)"),

        Ensinger(PolymerPa6, "PA 6 polyamide (TECAMID 6 natural)", "PA 6", "TECAMID 6 natural",
            EnsingerShapes + "pa6-tecamid-6-natural", density: 1.14, modulus: 3300, uts: 79, yieldMpa: 78,
            elongation: 130, cte: 120, conductivity: 0.37, heat: 1.6, melting: 221, serviceLong: 100, shoreD: 79,
            archive: "MAT-PA6", moisture: true),

        Ensinger(PolymerPa66, "PA 66 polyamide (TECAMID 66 natural)", "PA 66", "TECAMID 66 natural",
            EnsingerShapes + "pa66-tecamid-66-natural", density: 1.15, modulus: 3500, uts: 85, yieldMpa: 84,
            elongation: 70, cte: 110, conductivity: 0.36, heat: 1.5, melting: 258, serviceLong: 100, shoreD: 82,
            archive: null, moisture: true),

        Ensinger(PolymerPa66Gf30, "PA 66 GF30 glass-filled polyamide (TECAMID 66 GF30 black)", "PA 66 GF30",
            "TECAMID 66 GF30 black", EnsingerShapes + "pa66-tecamid-66-gf30-black", density: 1.34, modulus: 5500, uts: 91,
            yieldMpa: 91, elongation: 14, cte: 50, conductivity: 0.39, heat: 1.2, melting: 254, serviceLong: 110,
            shoreD: 86, archive: "MAT-PA66-GF30 / MAT-POLY-PA66-GF30", moisture: true, anisotropic: true),

        Ensinger(PolymerPeek, "PEEK polyetheretherketone (TECAPEEK natural)", "PEEK", "TECAPEEK natural",
            EnsingerShapes + "peek-tecapeek-natural", density: 1.31, modulus: 4200, uts: 116, yieldMpa: 116,
            elongation: 15, cte: 50, conductivity: 0.27, heat: 1.1, melting: 341, serviceLong: 260, shoreD: 89,
            archive: "MAT-PEEK / MAT-POLY-PEEK (PEEK unfilled)"),

        Ensinger(PolymerPc, "PC polycarbonate (TECANAT natural)", "PC", "TECANAT natural",
            EnsingerShapes + "polycarbonate-tecanat-natural", density: 1.19, modulus: 2200, uts: 69, yieldMpa: 69,
            elongation: 90, cte: 80, conductivity: 0.25, heat: 1.3, melting: null, serviceLong: 120, shoreD: 82,
            archive: "MAT-POLY-PC"),

        Ensinger(PolymerPei, "PEI polyetherimide (TECAPEI natural)", "PEI", "TECAPEI natural",
            EnsingerShapes + "tecapei-natural", density: 1.28, modulus: 3200, uts: 127, yieldMpa: 127,
            elongation: 35, cte: 50, conductivity: 0.21, heat: 1.2, melting: null, serviceLong: 170, shoreD: 88,
            archive: "MAT-POLY-PEI (PEI / Ultem)"),

        Ensinger(PolymerPps, "PPS polyphenylene sulphide (TECATRON SX natural)", "PPS", "TECATRON SX natural",
            EnsingerShapes + "pps-tecatron-sx-natural", density: 1.36, modulus: 4000, uts: 102, yieldMpa: 100,
            elongation: 11, cte: 60, conductivity: 0.25, heat: 1.0, melting: 281, serviceLong: 230, shoreD: 87,
            archive: "MAT-POLY-PPS (PPS unfilled)", testSpeed: "5 mm/min"),

        Ensinger(PolymerAbs, "ABS acrylonitrile-butadiene-styrene (TECARAN ABS grey)", "ABS", "TECARAN ABS grey",
            EnsingerShapes + "tecaran-abs-grey", density: 1.04, modulus: 1700, uts: 32, yieldMpa: 32,
            elongation: 49, cte: null, conductivity: null, heat: null, melting: null, serviceLong: 75, shoreD: 75,
            archive: "MAT-ABS / MAT-POLY-ABS"),

        Mat(PolymerPtfe,
            new MaterialDefinition
            {
                Name = "PTFE polytetrafluoroethylene (TECAFLON PTFE natural)",
                Family = MaterialFamily.Thermoplastic,
                Designation = "PTFE",
                SourceClassification = "PTFE (Polytetrafluorethylene)",
                Supplier = "Ensinger",
                SupplierDesignation = "TECAFLON PTFE natural",
                Properties = Props(
                [
                    (Density, Mfr(GramPerCc(2.15), "As the product page states it.")),
                    (Uts, Mfr(MPa(22), "Tensile strength per ASTM D 4894.", "Tensile strength")),
                    (Elongation, Mfr(Pct(220), "Elongation at break per ASTM D 4894.")),
                    (Cte, Mfr(MicroPerK(130), "23-100 degC longitudinal, ASTM D 696 (published as 13 x 10^-5 /K).")),
                    (Conductivity, Mfr(WPerMK(0.20), "ASTM C 177.")),
                    (MaxService, Mfr(DegC(260), "Long-term service temperature.")),
                    (ShoreDHardness, Mfr(Ratio(59), "Shore D, DIN EN ISO 868 (55 by ASTM D 2240).")),
                ]),
                Notes = "NO YOUNG'S MODULUS AND NO YIELD STRENGTH RECORDED: the product page publishes neither (PTFE "
                    + "creeps under sustained load rather than showing a usable yield point), and the Chemours Teflon PTFE "
                    + "handbook read alongside gives only a flexural modulus range of 345-620 MPa, which is a different "
                    + "quantity and is not substituted. Stress and stiffness calculators therefore refuse this record. "
                    + "Compression strength 5 MPa at 1% strain (ASTM D 695) per the page.",
            },
            SeedSources.Ensinger("TECAFLON PTFE natural", EnsingerShapes + "tecaflon-ptfe-natural"),
            "Ensinger GmbH", "TECAFLON PTFE natural", "Technical details: mechanical and thermal properties"),

        Mat(PolymerUhmwPe,
            new MaterialDefinition
            {
                Name = "PE-UHMW ultra-high-molecular-weight polyethylene (TECAFINE PE 1000 natural)",
                Family = MaterialFamily.Thermoplastic,
                Designation = "PE-UHMW",
                SourceClassification = "PE-UHMW (Polyethylene - ultra high molecular weight)",
                Supplier = "Ensinger",
                SupplierDesignation = "TECAFINE PE 1000 natural",
                Properties = Props(
                [
                    (Density, Mfr(GramPerCc(0.93), "As the product page states it.")),
                    (Modulus, Mfr(MPa(700), "Modulus of elasticity, tensile test, DIN EN ISO 527-1.")),
                    (Yield, Mfr(MPa(19), "Tensile strength at yield, DIN EN ISO 527-1.", "Tensile strength at yield")),
                    (Cte, Mfr(MicroPerK(180), "Published as 18 x 10^-5 /K, DIN EN ISO 11359-1/2.")),
                    (MaxService, Mfr(DegC(80), "Upper end of the -260 to +80 degC service range.")),
                    (MinService, Mfr(DegC(-260), "Lower end of the -260 to +80 degC service range.")),
                    (ShoreDHardness, Mfr(Ratio(60), "Shore D, DIN EN ISO 868.")),
                ]),
                Notes = "The page publishes no ultimate tensile strength (no break in the test) and no thermal conductivity. "
                    + "Elongation at yield 11%. Read from the en-gb product page.",
            },
            SeedSources.Ensinger("TECAFINE PE 1000 natural", EnsingerShapesGb + "polyethylene-tecafine-pe-1000-natural"),
            "Ensinger GmbH", "TECAFINE PE 1000 natural", "Technical details: mechanical and thermal properties"),

        Mat(PolymerHdpe,
            new MaterialDefinition
            {
                Name = "PE-HD high-density polyethylene (TECAFINE PE 300 natural)",
                Family = MaterialFamily.Thermoplastic,
                Designation = "PE-HD",
                SourceClassification = "PE-HD (Polyethylene)",
                Supplier = "Ensinger",
                SupplierDesignation = "TECAFINE PE 300 natural",
                Properties = Props(
                [
                    (Density, Mfr(GramPerCc(0.96), "As the product page states it.")),
                    (Modulus, Mfr(MPa(1100), "Modulus of elasticity, tensile test, DIN EN ISO 527-1.")),
                    (Yield, Mfr(MPa(23), "Tensile strength at yield, DIN EN ISO 527-1.", "Tensile strength at yield")),
                    (Cte, Mfr(MicroPerK(180), "Published as 18 x 10^-5 /K, DIN EN ISO 11359-1/2.")),
                    (MaxService, Mfr(DegC(80), "Upper end of the -50 to +80 degC service range.")),
                    (MinService, Mfr(DegC(-50), "Lower end of the -50 to +80 degC service range.")),
                    (ShoreDHardness, Mfr(Ratio(64), "Shore D, DIN EN ISO 868.")),
                ]),
                Notes = "No ultimate tensile strength or thermal conductivity published. Elongation at yield 9%. "
                    + "Knowledge-foundation archive items MAT-PE-HD / MAT-POLY-HDPE. Read from the en-gb product page.",
            },
            SeedSources.Ensinger("TECAFINE PE 300 natural", EnsingerShapesGb + "polyethylene-tecafine-pe-300-natural"),
            "Ensinger GmbH", "TECAFINE PE 300 natural", "Technical details: mechanical and thermal properties"),

        Mat(PolymerPp,
            new MaterialDefinition
            {
                Name = "PP polypropylene (TECAFINE PP natural)",
                Family = MaterialFamily.Thermoplastic,
                Designation = "PP",
                SourceClassification = "PP (Polypropylene)",
                Supplier = "Ensinger",
                SupplierDesignation = "TECAFINE PP natural",
                Properties = Props(
                [
                    (Density, Mfr(GramPerCc(0.9), "As the product page states it.")),
                    (Modulus, Mfr(MPa(1400), "Modulus of elasticity, tensile test, DIN EN ISO 527-1.")),
                    (Yield, Mfr(MPa(32), "Tensile strength at yield, DIN EN ISO 527-1.", "Tensile strength at yield")),
                    (Cte, Mfr(MicroPerK(160), "Published as 16 x 10^-5 /K, DIN EN ISO 11359-1/2.")),
                    (MaxService, Mfr(DegC(100), "Upper end of the 0 to +100 degC service range.")),
                    (MinService, Mfr(DegC(0), "Lower end of the 0 to +100 degC service range.")),
                    (ShoreDHardness, Mfr(Ratio(70), "Shore D, DIN EN ISO 868.")),
                ]),
                Notes = "No ultimate tensile strength or thermal conductivity published. Elongation at yield 8%. "
                    + "Knowledge-foundation archive items MAT-PP / MAT-POLY-PP. Read from the en-gb product page.",
            },
            SeedSources.Ensinger("TECAFINE PP natural", EnsingerShapesGb + "polypropylene-tecafine-pp-natural"),
            "Ensinger GmbH", "TECAFINE PP natural", "Technical details: mechanical and thermal properties"),

        Mat(PolymerPvcU,
            new MaterialDefinition
            {
                Name = "PVC-U unplasticised polyvinyl chloride (Trovidur PVC-U black)",
                Family = MaterialFamily.Thermoplastic,
                Designation = "PVC-U",
                SourceClassification = "PVC-U",
                Supplier = "Röchling",
                SupplierDesignation = "Trovidur PVC-U black",
                Properties = Props(
                [
                    (Density, Mfr(GramPerCc(1.45), "Guideline value, DIN EN ISO 1183-1.")),
                    (Yield, Mfr(MPa(45), "Yield stress, DIN EN ISO 527.", "Yield stress")),
                    (Elongation, Mfr(Pct(15), "Elongation at break, DIN EN ISO 527.")),
                    (Modulus, Mfr(MPa(3000), "Tensile modulus of elasticity, DIN EN ISO 527.")),
                    (Conductivity, Mfr(WPerMK(0.2), "DIN 52612-1.")),
                    (Cte, Mfr(MicroPerK(80), "DIN 53752.")),
                    (MaxService, Mfr(DegC(60), "Upper end of the long-term 0-60 degC service range.")),
                    (MinService, Mfr(DegC(0), "Lower end of the long-term 0-60 degC service range.")),
                    (ShoreDHardness, Mfr(Ratio(79), "Shore D, DIN EN ISO 868.")),
                ]),
                Notes = "Flame-retardant (UL 94 V0) black sheet grade. No ultimate tensile strength published. "
                    + "Knowledge-foundation archive item MAT-POLY-PVC-U. Data sheet release 20/09/2023.",
            },
            SeedSources.Roechling("Trovidur PVC-U black",
                "https://www.roechling.com/fileadmin/assets/Technical%20Data%20Sheet%20Trovidur%C2%AE%20PVC-U%20black%20591321%20EN.pdf"),
            "Röchling Industrial SE & Co. KG", "Technical Data Sheet Trovidur PVC-U black", "Properties tables"),

        Mat(PolymerPmma,
            new MaterialDefinition
            {
                Name = "PMMA cast acrylic sheet (PLEXIGLAS GS)",
                Family = MaterialFamily.Thermoplastic,
                Designation = "PMMA (cast)",
                SourceClassification = "Cast acrylic (polymethylmethacrylate)",
                Supplier = "Röhm",
                SupplierDesignation = "PLEXIGLAS GS",
                Properties = Props(
                [
                    (Density, Mfr(GramPerCc(1.19), "ISO 1183.")),
                    (Uts, Mfr(MPa(60), "Tensile strength, published as >= 60 MPa, ISO 527.", "Tensile strength")),
                    (Elongation, Mfr(Pct(5), "Elongation at break, published as >= 5%, ISO 527.")),
                    (Modulus, Mfr(MPa(2500), "Elastic modulus, published as >= 2500 MPa, ISO 527.")),
                    (Cte, Mfr(MicroPerK(70), "Published as 7 x 10^-5 1/K, DIN 53752-A.")),
                    (Conductivity, Mfr(WPerMK(0.19), "DIN 52612.")),
                ]),
                Notes = "NO YIELD STRENGTH RECORDED: cast PMMA fails brittle and the sheet publishes none, so stress "
                    + "calculators refuse this record. Values are from the UV-transmitting PLEXIGLAS GS sheet (Clear 2458); "
                    + "the sheet refers to the general GS/XT technical information for further data. Knowledge-foundation "
                    + "archive item MAT-POLY-PMMA.",
            },
            SeedSources.Plexiglas("PLEXIGLAS GS, UV transmitting (222-6)",
                "https://www.plexiglas.de/files/plexiglas-content/pdf/technische-informationen/222-6-PLEXIGLAS-GS-UV-transmitting_Clear_2458_and_SC_EN.pdf"),
            "Röhm GmbH (PLEXIGLAS)", "PLEXIGLAS GS, UV transmitting — technical information 222-6", "Physical properties table"),
    ];

    private static ReferenceSeedRecord<MaterialDefinition> Ensinger(
        string recordId,
        string name,
        string designation,
        string product,
        string url,
        double density,
        double modulus,
        double uts,
        double yieldMpa,
        double elongation,
        double? cte,
        double? conductivity,
        double? heat,
        double? melting,
        double serviceLong,
        double shoreD,
        string? archive,
        bool moisture = false,
        bool anisotropic = false,
        string testSpeed = "50 mm/min")
    {
        var properties = new List<(string, ReferenceQuantityValue)>
        {
            (Density, Mfr(GramPerCc(density), "As the product page states it.")),
            (Modulus, Mfr(MPa(modulus), "Modulus of elasticity, tensile test at 1 mm/min, DIN EN ISO 527-2.")),
            (Uts, Mfr(MPa(uts), $"Tensile strength at {testSpeed}, DIN EN ISO 527-2.", "Tensile strength")),
            (Yield, Mfr(MPa(yieldMpa), $"Tensile strength at yield at {testSpeed}, DIN EN ISO 527-2.", "Tensile strength at yield")),
            (Elongation, Mfr(Pct(elongation), "Elongation at break, tensile test, DIN EN ISO 527-2.")),
            (MaxService, Mfr(DegC(serviceLong), "Long-term service temperature.")),
            (ShoreDHardness, Mfr(Ratio(shoreD), "Shore D, DIN EN ISO 868.")),
        };

        if (cte is { } c)
            properties.Add((Cte, Mfr(MicroPerK(c), $"23-60 degC longitudinal, DIN EN ISO 11359-1/2 (published as {c / 10:0.#} x 10^-5 /K).")));

        if (conductivity is { } k)
            properties.Add((Conductivity, Mfr(WPerMK(k), "ISO 22007-4:2008.")));

        if (heat is { } h)
            properties.Add((HeatCapacity, Mfr(JPerKgK(h * 1000), $"ISO 22007-4:2008 (published as {h:0.0#} J/(g.K)).")));

        if (melting is { } m)
            properties.Add((Melting, Mfr(DegC(m), "Melting temperature, DIN EN ISO 11357.")));

        var notes = (cte is null
                ? "NO THERMAL EXPANSION COEFFICIENT RECORDED: the product page publishes none, so the thermal-expansion "
                    + "calculator refuses this record. "
                : string.Empty)
            + (moisture
                ? "Polyamides absorb moisture, which lowers strength and stiffness and changes dimensions; these are dry "
                    + "values. "
                : string.Empty)
            + (anisotropic ? "Glass-fibre filled: properties are direction-dependent in a moulded or extruded part. " : string.Empty)
            + "Typical values measured on test specimens, not specification limits."
            + (archive is null ? string.Empty : $" Knowledge-foundation archive item {archive}.");

        return Mat(recordId,
            new MaterialDefinition
            {
                Name = name,
                Family = MaterialFamily.Thermoplastic,
                Designation = designation,
                SourceClassification = designation,
                Supplier = "Ensinger",
                SupplierDesignation = product,
                Properties = Props([.. properties]),
                Notes = notes,
            },
            SeedSources.Ensinger(product, url),
            "Ensinger GmbH", product, "Technical details: mechanical and thermal properties");
    }
}
