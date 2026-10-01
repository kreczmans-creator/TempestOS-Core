using Tempest.Core.Materials;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// Grey and spheroidal-graphite cast irons to EN 1561 and EN 1563.
/// </summary>
/// <remarks>
/// Strength limits come from the SteelNumber grade pages (the standard's
/// separately-cast test-piece values); physical and fatigue values from a
/// foundry's restatement of the standard's informative annex, cited on each
/// value. Cast-iron properties fall with casting wall thickness — the
/// thinnest band is recorded and the rest are stated.
/// </remarks>
public sealed partial class MaterialSeed
{
    /// <summary>The identity of the EN-GJL-250 grey iron record.</summary>
    public const string CastIronGjl250 = "mat-en-gjl-250";

    /// <summary>The identity of the EN-GJS-400-15 ductile iron record.</summary>
    public const string CastIronGjs40015 = "mat-en-gjs-400-15";

    /// <summary>The identity of the EN-GJS-500-7 ductile iron record.</summary>
    public const string CastIronGjs5007 = "mat-en-gjs-500-7";

    private const string CastfastGjlRef =
        "CASTFAST GmbH, 'Material properties of cast iron — Cast iron with lamellar graphite (GJL)' "
        + "(https://www.castfast.com/wp-content/uploads/2025/07/Werkstoffeigenschaften-GJL-CASTFASTenvers.pdf), "
        + "EN-GJL-250 column";

    private const string CastfastGjsRef =
        "CASTFAST GmbH, 'Material properties of cast iron — Spheroidal graphite cast iron (GJS)' "
        + "(https://www.castfast.com/wp-content/uploads/2025/07/20240507_Werkstoffeigenschaften-GJS-CASTFASTenvers.pdf)";

    private static IEnumerable<ReferenceSeedRecord<MaterialDefinition>> CastIrons() =>
    [
        Mat(CastIronGjl250,
            new MaterialDefinition
            {
                Name = "EN-GJL-250 grey cast iron",
                Family = MaterialFamily.CastIron,
                Designation = "EN-GJL-250",
                Grade = "EN-JL1040 (formerly GG25)",
                Condition = "As cast",
                SourceClassification = "Grey cast iron (lamellar graphite)",
                Standards = [new StandardReference("EN 1561", StandardSeed.En1561, "CEN", "1997",
                    "Grey cast irons, as the SteelNumber page states it")],
                Properties = Props(
                [
                    (Uts, Lim(MPa(250), "Lower limit of the 250-350 MPa range the SteelNumber page states for the grade "
                        + "(separately cast test piece). CASTFAST tabulates 200-250 MPa against the same grade, evidently for "
                        + "thicker castings; strength falls with wall thickness.", "Rm")),
                    (Yield, SupplementaryValue(MPa(165), CastfastGjlRef + ": 0.1% proof strength Rp0.1 165-228 MPa; "
                        + "lower end recorded. Grey iron has no yield point; 0.1% proof is the conventional substitute.",
                        ReferenceValueOrigin.EngineeringReference)),
                    (Elongation, SupplementaryValue(Pct(0.3), CastfastGjlRef + ": ultimate strain A5 0.3-0.8%; lower "
                        + "end recorded. Effectively brittle.", ReferenceValueOrigin.EngineeringReference)),
                    (Modulus, SupplementaryValue(GPa(103), CastfastGjlRef + ": E 103-118 kN/mm2; lower end recorded. "
                        + "Grey iron's modulus falls with stress.", ReferenceValueOrigin.EngineeringReference)),
                    (Poisson, SupplementaryValue(Ratio(0.26), CastfastGjlRef + ": Poisson number 0.26.",
                        ReferenceValueOrigin.EngineeringReference)),
                    (Density, SupplementaryValue(GramPerCc(7.20), CastfastGjlRef + ": 7.20 g/cm3.",
                        ReferenceValueOrigin.EngineeringReference)),
                    (Cte, SupplementaryValue(MicroPerK(11.7), CastfastGjlRef + ": coefficient of linear expansion "
                        + "20-200 degC 11.7 um/(m.K) (13.0 for 20-400 degC).", ReferenceValueOrigin.EngineeringReference)),
                    (Conductivity, SupplementaryValue(WPerMK(48.5), CastfastGjlRef + ": 48.5-44.5 W/(m.K) over "
                        + "100-500 degC; the 100 degC end recorded.", ReferenceValueOrigin.EngineeringReference)),
                    (HeatCapacity, SupplementaryValue(JPerKgK(460), CastfastGjlRef + ": 460 J/(kg.K) at 20-200 degC.",
                        ReferenceValueOrigin.EngineeringReference)),
                    (Fatigue, SupplementaryValue(MPa(60), CastfastGjlRef + ": tension-compression fatigue strength "
                        + "60 N/mm2 (flexural fatigue strength 120 N/mm2).", ReferenceValueOrigin.EngineeringReference)),
                    (Compressive, SupplementaryValue(MPa(840), CastfastGjlRef + ": compressive strength 840 N/mm2.",
                        ReferenceValueOrigin.EngineeringReference)),
                ]),
                Notes = "Grey iron is three to four times stronger in compression than tension and fails without "
                    + "yielding; a tension yield check against the 0.1% proof strength is a convention, not a ductile "
                    + "margin. Hardness 160-210 HB per CASTFAST.",
            },
            SeedSources.SteelNumber("EN-GJL-250 (EN-JL1040)", 1505, "Mechanical properties of cast iron (Rm)"),
            "SteelNumber (European steel and alloy grades database)", "SteelNumber grade page — EN-GJL-250",
            "Mechanical properties of cast iron"),

        Gjs(CastIronGjs40015, "EN-GJS-400-15", "5.3106", 1520, ferritic: true,
            utsMpa: 400, utsBands: "390 MPa for 30-60 mm, 370 MPa for 60-200 mm", proofMpa: 250,
            proofBands: "250 MPa for 30-60 mm, 240 MPa for 60-200 mm", elongation: 15,
            elongationBands: "14% for 30-60 mm, 11% for 60-200 mm", hardness: "135-180 HBW",
            modulus: 169, fatigue: 110),

        Gjs(CastIronGjs5007, "EN-GJS-500-7", "5.3200", 1522, ferritic: false,
            utsMpa: 500, utsBands: "450 MPa for 30-60 mm, 420 MPa for 60-200 mm", proofMpa: 320,
            proofBands: "300 MPa for 30-60 mm, 290 MPa for 60-200 mm", elongation: 7,
            elongationBands: "7% for 30-60 mm, 5% for 60-200 mm", hardness: "150-230 HBW",
            modulus: 169, fatigue: 150),
    ];

    private static ReferenceSeedRecord<MaterialDefinition> Gjs(
        string recordId,
        string grade,
        string number,
        int steelNumberId,
        bool ferritic,
        double utsMpa,
        string utsBands,
        double proofMpa,
        string proofBands,
        double elongation,
        string elongationBands,
        string hardness,
        double modulus,
        double fatigue) =>
        Mat(recordId,
            new MaterialDefinition
            {
                Name = $"{grade} spheroidal graphite (ductile) cast iron",
                Family = MaterialFamily.CastIron,
                Designation = grade,
                Grade = number,
                Condition = "As cast",
                SourceClassification = ferritic ? "Ductile iron, ferritic" : "Ductile iron, ferritic-pearlitic",
                Standards = [new StandardReference("EN 1563", StandardSeed.En1563, "CEN", "2011",
                    "Spheroidal graphite cast irons, as the SteelNumber page states it")],
                Properties = Props(
                [
                    (Uts, Lim(MPa(utsMpa), $"Minimum, relevant wall thickness to 30 mm ({utsBands}).", "Rm")),
                    (Yield, Lim(MPa(proofMpa), $"Minimum 0.2% proof strength, wall to 30 mm ({proofBands}).", "Rp0.2")),
                    (Elongation, Lim(Pct(elongation), $"Minimum, wall to 30 mm ({elongationBands}).", "A")),
                    (Modulus, SupplementaryValue(GPa(modulus), CastfastGjsRef + $", {grade} column: modulus of elasticity "
                        + $"{modulus:0} kN/mm2.", ReferenceValueOrigin.EngineeringReference)),
                    (Poisson, SupplementaryValue(Ratio(0.28), CastfastGjsRef + ": Poisson number 0.28 across the grades.",
                        ReferenceValueOrigin.EngineeringReference)),
                    (Density, SupplementaryValue(GramPerCc(7.10), CastfastGjsRef + $", {grade} column: 7.10 g/cm3 at 20 degC.",
                        ReferenceValueOrigin.EngineeringReference)),
                    (Cte, SupplementaryValue(MicroPerK(12.5), CastfastGjsRef + ": coefficient of linear expansion "
                        + "20-400 degC 12.5 um/(m.K), tabulated across the grades.", ReferenceValueOrigin.EngineeringReference)),
                    (HeatCapacity, SupplementaryValue(JPerKgK(515), CastfastGjsRef + ": 515 J/(kg.K), 20-500 degC.",
                        ReferenceValueOrigin.EngineeringReference)),
                    (Fatigue, SupplementaryValue(MPa(fatigue), CastfastGjsRef + $", {grade} column: tension-compression "
                        + $"fatigue strength +/-{fatigue:0} N/mm2.", ReferenceValueOrigin.EngineeringReference)),
                ]),
                Notes = $"Hardness {hardness} per the SteelNumber page. Thermal conductivity is tabulated by CASTFAST only "
                    + "at 300 degC with values that cannot be assigned to columns unambiguously, so none is recorded. "
                    + "CASTFAST prints its rotating-bending fatigue rows in kN/mm2, evidently meaning N/mm2; those rows "
                    + "are not used.",
            },
            SeedSources.SteelNumber($"{grade} ({number})", steelNumberId,
                "Mechanical properties of cast iron (Rm, Rp0.2, A by wall thickness; hardness)"),
            "SteelNumber (European steel and alloy grades database)", $"SteelNumber grade page — {grade}",
            "Mechanical properties of cast iron");
}
