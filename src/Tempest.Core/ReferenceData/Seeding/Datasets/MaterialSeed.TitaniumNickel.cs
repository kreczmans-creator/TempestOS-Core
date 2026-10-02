using Tempest.Core.Materials;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// Titanium (AZoM grade articles) and nickel alloys (Special Metals'
/// own technical bulletins).
/// </summary>
public sealed partial class MaterialSeed
{
    /// <summary>The identity of the titanium Grade 2 record.</summary>
    public const string TitaniumGrade2 = "mat-ti-grade-2";

    /// <summary>The identity of the titanium Grade 5 (Ti-6Al-4V) record.</summary>
    public const string TitaniumGrade5 = "mat-ti-grade-5";

    /// <summary>The identity of the Inconel 600 annealed record.</summary>
    public const string Inconel600 = "mat-inconel-600-annealed";

    /// <summary>The identity of the Inconel 625 annealed record.</summary>
    public const string Inconel625 = "mat-inconel-625-annealed";

    /// <summary>The identity of the Inconel 718 aged record.</summary>
    public const string Inconel718 = "mat-inconel-718-aged";

    private static IEnumerable<ReferenceSeedRecord<MaterialDefinition>> TitaniumAndNickelAlloys() =>
    [
        Mat(TitaniumGrade2,
            new MaterialDefinition
            {
                Name = "Titanium Grade 2 (commercially pure)",
                Family = MaterialFamily.Titanium,
                Designation = "Titanium Grade 2",
                Grade = "UNS R50400",
                SourceClassification = "Unalloyed titanium",
                Properties = Props(
                [
                    (Uts, Typ(MPa(485), "As the article tabulates it.", "Tensile strength")),
                    (Yield, Typ(MPa(345), "As the article tabulates it.", "Yield strength")),
                    (Elongation, Typ(Pct(28), "As the article tabulates it.")),
                    (Density, Typ(GramPerCc(4.51), "As the article states it.")),
                    (Melting, Typ(DegC(1660), "As the article states it.")),
                    (Modulus, Typ(GPa(105), "The article gives 105-120 GPa; lower end recorded.")),
                    (Poisson, Typ(Ratio(0.34), "The article gives 0.34-0.40; lower end recorded.")),
                    (Cte, Typ(MicroPerK(8.6), "0-100 degC.")),
                    (Conductivity, Typ(WPerMK(21.97), "As the article states it.")),
                    (VickersHardness, Typ(Ratio(160), "The article gives 160-200 HV; lower end recorded.")),
                ]),
                EnvironmentalNotes = "Excellent general corrosion resistance; non-magnetic.",
                Notes = "Typical values; the article does not state a product form or condition. ASTM Grade 2 minima "
                    + "(not read) are lower than these typical values — confirm against the material certificate. "
                    + "Knowledge-foundation archive item MAT-TI-CP-GR2.",
            },
            SeedSources.Azom("Grade 2 Unalloyed Ti (\"Pure\") 50A (UNS R50400)", 9413, "Physical, mechanical and thermal tables"),
            "AZoM (AZO Materials)", "Grade 2 Unalloyed Ti (\"Pure\") 50A (UNS R50400)", "Mechanical properties table"),

        Mat(TitaniumGrade5,
            new MaterialDefinition
            {
                Name = "Titanium Grade 5 (Ti-6Al-4V)",
                Family = MaterialFamily.Titanium,
                Designation = "Ti-6Al-4V Grade 5",
                Grade = "UNS R56200 (as the article's title states it)",
                SourceClassification = "Alpha-beta titanium alloy",
                Properties = Props(
                [
                    (Uts, Lim(MPa(895), "The article tabulates it as a minimum (>= 895 MPa).", "Tensile strength")),
                    (Yield, Lim(MPa(828), "The article tabulates it as a minimum (>= 828 MPa).", "Yield strength")),
                    (Elongation, Lim(Pct(10), "The article tabulates it as a minimum (>= 10%).", "Elongation at break")),
                    (Density, Typ(GramPerCc(4.43), "As the article states it.")),
                    (Melting, Typ(DegC(1674), "As the article states it.")),
                    (Modulus, Typ(GPa(105), "The article gives 105-120 GPa; lower end recorded.")),
                    (Shear, Typ(GPa(41), "The article gives 41-45 GPa; lower end recorded.")),
                    (Poisson, Typ(Ratio(0.31), "As the article states it.")),
                    (Cte, Typ(MicroPerK(9), "0-100 degC.")),
                    (Conductivity, Typ(WPerMK(6.6), "As the article states it.")),
                ]),
                EnvironmentalNotes = "Highly resistant to general corrosion in sea water, per the article.",
                Notes = "The UNS number is recorded as the article prints it; confirm it against the ASTM product "
                    + "specification before issue. Minima are as the article states them; it names no specification. "
                    + "Knowledge-foundation archive item MAT-TI-6AL4V (archive screening yield 830 MPa vs the 828 MPa "
                    + "minimum here).",
            },
            SeedSources.Azom("Grade 5 Ti-6Al-4V Alloy (UNS R56200)", 9299, "Physical, mechanical and thermal tables"),
            "AZoM (AZO Materials)", "Grade 5 Ti-6Al-4V Alloy (UNS R56200)", "Mechanical properties table"),

        Mat(Inconel600,
            new MaterialDefinition
            {
                Name = "INCONEL alloy 600, hot-finished and annealed rod and bar",
                Family = MaterialFamily.NickelAlloy,
                Designation = "INCONEL alloy 600",
                Grade = "UNS N06600",
                Condition = "Hot-finished, annealed (rod and bar)",
                SourceClassification = "Nickel-chromium-iron alloy",
                Supplier = "Special Metals Corporation",
                SupplierDesignation = "INCONEL alloy 600",
                Properties = Props(
                [
                    (Uts, Mfr(MPa(550), "Lower end of the 550-690 MPa nominal range for hot-finished annealed rod and bar "
                        + "(Table 6). The bulletin states these ranges are composites, not for specification.", "Tensile Strength")),
                    (Yield, Mfr(MPa(205), "Lower end of the 205-345 MPa 0.2% offset range, same form (Table 6).",
                        "Yield Strength (0.2% Offset)")),
                    (Elongation, Mfr(Pct(35), "Lower end of the 55-35% range, same form.")),
                    (Density, Mfr(GramPerCc(8.47), "Table 2, 8.47 Mg/m3.")),
                    (Modulus, Mfr(GPa(214), "Young's modulus at 22 degC (Table 4).")),
                    (Shear, Mfr(GPa(80.8), "Shear modulus at 22 degC (Table 4).")),
                    (Poisson, Mfr(Ratio(0.324), "At 22 degC (Table 4).")),
                    (Cte, Mfr(MicroPerK(13.3), "Mean coefficient of linear expansion from 21 degC to 100 degC (Table 3).")),
                    (Conductivity, Mfr(WPerMK(14.9), "At 20 degC (Table 3).")),
                    (HeatCapacity, Mfr(JPerKgK(444), "Table 2.")),
                    (Melting, Mfr(DegC(1354), "Solidus of the 1354-1413 degC melting range (Table 2).")),
                ]),
                Notes = "Knowledge-foundation archive item MAT-NI-INCONEL600.",
            },
            SeedSources.SpecialMetals("INCONEL alloy 600",
                "https://www.specialmetals.com/documents/technical-bulletins/inconel/inconel-alloy-600.pdf",
                "Table 2 Physical Constants, Table 3 Thermal Properties, Table 4 Modulus of Elasticity, Table 6 Typical "
                + "Mechanical-Property Ranges"),
            "Special Metals Corporation", "INCONEL alloy 600 technical bulletin", "Tables 2, 3, 4 and 6"),

        Mat(Inconel625,
            new MaterialDefinition
            {
                Name = "INCONEL alloy 625, annealed rod, bar and plate",
                Family = MaterialFamily.NickelAlloy,
                Designation = "INCONEL alloy 625",
                Grade = "UNS N06625",
                Condition = "Annealed (rod, bar, plate)",
                SourceClassification = "Nickel-chromium-molybdenum alloy",
                Supplier = "Special Metals Corporation",
                SupplierDesignation = "INCONEL alloy 625",
                Properties = Props(
                [
                    (Uts, Mfr(MPa(827), "Lower end of the 827-1034 MPa nominal range, annealed rod/bar/plate (Table 5); "
                        + "composites for sizes up to 4 in., not for specification.", "Tensile Strength")),
                    (Yield, Mfr(MPa(414), "Lower end of the 414-655 MPa 0.2% offset range (Table 5).",
                        "Yield Strength (0.2% Offset)")),
                    (Elongation, Mfr(Pct(30), "Lower end of the 60-30% range (Table 5).")),
                    (Density, Mfr(GramPerCc(8.44), "Table 2.")),
                    (Modulus, Mfr(GPa(207.5), "Tension modulus at 21 degC, annealed (Table 4).")),
                    (Shear, Mfr(GPa(81.4), "Shear modulus at 21 degC, annealed (Table 4).")),
                    (Poisson, Mfr(Ratio(0.278), "At 21 degC, annealed (Table 4).")),
                    (Cte, Mfr(MicroPerK(12.8), "Mean linear expansion to 93 degC (Table 3).")),
                    (Conductivity, Mfr(WPerMK(9.8), "At 21 degC (Table 3).")),
                    (HeatCapacity, Mfr(JPerKgK(410), "At 21 degC (Table 2).")),
                    (Melting, Mfr(DegC(1290), "Solidus of the 1290-1350 degC melting range (Table 2).")),
                ]),
                Notes = "Knowledge-foundation archive item MAT-NI-INCONEL625.",
            },
            SeedSources.SpecialMetals("INCONEL alloy 625",
                "https://www.specialmetals.com/documents/technical-bulletins/inconel/inconel-alloy-625.pdf",
                "Table 2 Physical Constants, Table 3 Thermal and Electrical Properties, Table 4 Modulus, Table 5 Nominal "
                + "Room-Temperature Mechanical Properties"),
            "Special Metals Corporation", "INCONEL alloy 625 technical bulletin", "Tables 2, 3, 4 and 5"),

        Mat(Inconel718,
            new MaterialDefinition
            {
                Name = "INCONEL alloy 718, solution annealed and aged",
                Family = MaterialFamily.NickelAlloy,
                Designation = "INCONEL alloy 718",
                Grade = "UNS N07718 / W.Nr. 2.4668",
                Condition = "Solution annealed and aged (oil-tool specification, Table 6)",
                SourceClassification = "Precipitation-hardenable nickel-chromium alloy",
                Supplier = "Special Metals Corporation",
                SupplierDesignation = "INCONEL alloy 718",
                Properties = Props(
                [
                    (Uts, Mfr(Ksi(150), "Minimum, aged material for oil tool applications, 0.5-10 in. diameter (Table 6); "
                        + "published as 150 ksi and recorded in psi.", "Tensile Strength min")),
                    (Yield, Mfr(Ksi(120), "Minimum 0.2% offset, same (Table 6; maximum 140 ksi); published as 120 ksi.",
                        "Yield Strength (0.2% Offset) min")),
                    (Elongation, Mfr(Pct(20), "Minimum, in 2 in. or 4D (Table 6).")),
                    (Density, Mfr(LbPerIn3(0.297), "Annealed and aged, as published in lb/in3 (Table 2).")),
                    (Modulus, Mfr(Ksi(29000), "Young's modulus at 70 degF, cold-rolled sheet heat-treated to AMS 5596B "
                        + "(Table 3); published as 29.0 x 10^3 ksi.")),
                    (Poisson, Mfr(Ratio(0.29), "At 70 degF (Table 3).")),
                    (Cte, Mfr(MicroPerF(7.31), "Mean linear expansion from 70 degF to 200 degF (Table 5), published as "
                        + "7.31 x 10^-6 in/in/degF.")),
                    (Melting, Mfr(DegC(1260), "Solidus of the 1260-1336 degC melting range (Table 2).")),
                ]),
                Notes = "Strength depends heavily on heat treatment; the Table 6 oil-tool minima are recorded because they "
                    + "are the only guaranteed (minimum) values the bulletin tabulates. Thermal conductivity is published "
                    + "in BTU.in/(ft2.h.degF), a unit this platform does not model, and is not recorded. Knowledge-"
                    + "foundation archive item MAT-NI-INCONEL718.",
            },
            SeedSources.SpecialMetals("INCONEL alloy 718",
                "https://www.specialmetals.com/documents/technical-bulletins/inconel/inconel-alloy-718.pdf",
                "Table 2 Physical Constants, Table 3 Modulus of Elasticity, Table 5 Thermal Properties, Table 6 Mechanical "
                + "Properties Aged Material for Oil Tool Applications"),
            "Special Metals Corporation", "INCONEL alloy 718 technical bulletin", "Tables 2, 3, 5 and 6"),
    ];
}
