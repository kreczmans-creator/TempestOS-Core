using System.Globalization;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural.Geometry;

namespace Tempest.Calc.Structural;

/// <summary>
/// S1 — section properties of parametric shapes (rectangle, circle, tube,
/// hollow rectangle, I, channel, tee, angle), an arbitrary polygon with
/// holes, or a composite of placed parts. Area, centroid, second moments
/// including the product, principal axes, elastic and plastic moduli,
/// radii of gyration, the torsion constant (exact for solid rectangles,
/// circles and tubes; thin-walled for the rest, flagged) and the warping
/// constant for I-sections.
/// </summary>
public sealed class SectionPropertiesCalculator : ICalcCalculator
{
    public const string Id = "struct.section-properties";

    public const string ShapeRectangle = "rectangle";
    public const string ShapeCircle = "circle";
    public const string ShapeTube = "tube";
    public const string ShapeHollowRectangle = "hollow rectangle";
    public const string ShapeI = "I-section";
    public const string ShapeChannel = "channel";
    public const string ShapeTee = "tee";
    public const string ShapeAngle = "angle";
    public const string ShapePolygon = "polygon";
    public const string ShapeComposite = "composite";

    private static readonly string[] Primitives = [ShapeRectangle, ShapeCircle, ShapeTube, ShapeHollowRectangle, ShapeI, ShapeChannel, ShapeTee, ShapeAngle];

    private static readonly IReadOnlyList<CalcColumnSpec> VertexColumns =
    [
        new("x", "x", CalcInputKind.Quantity, "mm", HardMin: -1e5, HardMax: 1e5),
        new("y", "y", CalcInputKind.Quantity, "mm", HardMin: -1e5, HardMax: 1e5),
        new("loop", "Loop (1 = outline, 2+ = holes)", CalcInputKind.Number, HardMin: 1, HardMax: PolygonSection.MaxLoops, IsOptional: true),
    ];

    private static readonly IReadOnlyList<CalcColumnSpec> PartColumns =
    [
        new("shape", "Shape", CalcInputKind.Choice, Choices: Primitives),
        new("b", "Width or horizontal leg", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 1e5, IsOptional: true),
        new("h", "Depth, vertical leg, or diameter of a circle or tube", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 1e5, IsOptional: true),
        new("t_w", "Wall, web or leg thickness", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 1e4, IsOptional: true),
        new("t_f", "Flange thickness", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 1e4, IsOptional: true),
        new("r", "Root or corner radius", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 1e4, IsOptional: true),
        new("x0", "Centroid x", CalcInputKind.Quantity, "mm", HardMin: -1e5, HardMax: 1e5),
        new("y0", "Centroid y", CalcInputKind.Quantity, "mm", HardMin: -1e5, HardMax: 1e5),
        new("rot", "Rotation (° anticlockwise)", CalcInputKind.Number, HardMin: -360, HardMax: 360, IsOptional: true),
        new("sign", "Add or subtract", CalcInputKind.Choice, Choices: ["add", "subtract"], IsOptional: true),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.2.1", CalcDiscipline.Structural, "Section properties",
        "Calculates area, centroid, second moments, moduli, radii of gyration, and torsion and warping constants of a parametric, polygon or composite section. The outline has x horizontal and y vertical; properties use EN 1993 axes, y-y horizontal and z-z vertical.",
        "Area and centroid: A = ½∮(x dy − y dx); x_c = ∫x dA / A; y_c = ∫y dA / A.\nSecond moment about y-y: I_y = ∫y² dA − A y_c², by Green's theorem and the parallel-axis theorem.\nSecond moment about z-z: I_z = ∫x² dA − A x_c²; product I_yz = ∫xy dA − A x_c y_c.\nPrincipal axes: tan 2θ = −2 I_yz / (I_y − I_z).\nElastic section modulus: W_el_y = I_y / y_max.\nPlastic section modulus about the equal-area axis, found by bisection: W_pl_y = |Q_above| + |Q_below|.\nTorsion constant J: Saint-Venant series (solid rectangle), πd⁴/32 (circle), Bredt (closed thin-walled), Σbt³/3 (open).\nWarping constant of an I-section: I_w = I_z (h − t_f)²/4.",
        [
            new CalcInputSpec("shape", "Shape", CalcInputKind.Choice, Choices: [.. Primitives, ShapePolygon, ShapeComposite], DefaultChoice: ShapeI, Group: "Setup"),
            new CalcInputSpec("h", "Depth or vertical leg", CalcInputKind.Quantity, "mm", Default: 467.2, Min: 1, Max: 5000, HardMin: 1e-3, HardMax: 1e5, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("b", "Width or horizontal leg", CalcInputKind.Quantity, "mm", Default: 192.8, Min: 1, Max: 5000, HardMin: 1e-3, HardMax: 1e5, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("t_w", "Wall, web or leg thickness", CalcInputKind.Quantity, "mm", Default: 11.4, Min: 0.1, Max: 500, HardMin: 1e-3, HardMax: 1e4, IsOptional: true, Group: "Geometry", Aliases: ["t"]),
            new CalcInputSpec("t_f", "Flange thickness", CalcInputKind.Quantity, "mm", Default: 19.6, Min: 0.1, Max: 500, HardMin: 1e-3, HardMax: 1e4, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("d", "Diameter of a circle or tube", CalcInputKind.Quantity, "mm", Min: 1, Max: 5000, HardMin: 1e-3, HardMax: 1e5, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("r", "Root radius of an I-section, channel, tee or angle, or outer corner radius of a hollow rectangle (optional; blank = 0 for the rolled shapes, 1.5 t_w for a hollow rectangle)", CalcInputKind.Quantity, "mm", Default: 10.2, Min: 0, Max: 500, HardMin: 0, HardMax: 1e4, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("r_i", "Inner corner radius of a hollow rectangle (optional; blank = 1.0 t_w, at most the outer radius)", CalcInputKind.Quantity, "mm", Min: 0, Max: 500, HardMin: 0, HardMax: 1e4, IsOptional: true, Group: "Geometry"),
            new CalcInputSpec("vertices", "Polygon vertices (polygon shape)", CalcInputKind.Table, Columns: VertexColumns, IsOptional: true, MaxRows: 2000, Group: "Polygon"),
            new CalcInputSpec("parts", "Parts (composite shape)", CalcInputKind.Table, Columns: PartColumns, IsOptional: true, MaxRows: 100, Group: "Composite"),
        ],
        [
            new CalcAssumption("A polygon outline does not cross itself.", "Green's theorem integrates the signed loops as given."),
            new CalcAssumption("Holes lie inside the outline and do not overlap one another.", "Green's theorem integrates the signed loops as given."),
            new CalcAssumption("Rolled-section root fillets are quarter circles of radius r and flange tips are square.", "Published section tables use the same idealisation."),
            new CalcAssumption("Hollow-section corners are quarter circles of radius 1.5 t_w outside and 1.0 t_w inside by default.", "BS EN 10210-2 hot-finished idealisation."),
            new CalcAssumption("Thin-walled torsion constants neglect the junction and tip effects except the fillet term kept for rolled I-sections.", "Open sections: Σbt³/3; closed: Bredt."),
        ],
        [
            new CalcClauseRef("Young and Budynas, Roark's Formulas for Stress and Strain", "Table A.1 and Table 10.1, 8th ed.", "Properties of sections; torsion of solid and thin-walled sections"),
            new CalcClauseRef("Seaburg and Carter, Torsional Analysis of Structural Steel Members (AISC Design Guide 9)", "App., 1st ed.", "Torsional properties of rolled I-shapes"),
            new CalcClauseRef("BS EN 10365:2017", "—", "Hot rolled steel channels, I and H sections: dimensions and masses"),
            new CalcClauseRef("BS EN 10210-2:2019", "—", "Hot finished structural hollow sections: tolerances, dimensions and sectional properties"),
        ],
        [
            new CalcApplicabilityRule("missing-dimension", "A dimension the chosen shape needs was not given.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("geometry", "A thickness exceeds the size it sits in, or a loop encloses no area.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("thin-walled-torsion", "The torsion constant is a thin-walled approximation.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("no-torsion", "No torsion constant is computed for an arbitrary polygon or a composite.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("thick-wall", "A wall thicker than a fifth of the section size: the thin-walled torsion estimate is rough.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-ub-457x191x98", "UB 457×191×98 (SCI Blue Book)",
                new CalcReferenceSource("SCI / Steel for Life Blue Book (P363 data)", "EC3 UK NA", "UB 457×191×98: A = 125 cm², Iyy = 45,730 cm⁴, Izz = 2,347 cm⁴, iy = 19.1 cm, iz = 4.33 cm, Wel,y = 1,957 cm³, Wel,z = 243 cm³, Wpl,y = 2,232 cm³, Wpl,z = 379 cm³, IT = 121 cm⁴, Iw = 1.18 dm⁶ (Blue Book y-y and z-z are this sheet's y-y and z-z)"),
                [CalcInputValue.Choice("shape", ShapeI), CalcInputValue.Quantity("h", 467.2, "mm"), CalcInputValue.Quantity("b", 192.8, "mm"), CalcInputValue.Quantity("t_w", 11.4, "mm"), CalcInputValue.Quantity("t_f", 19.6, "mm"), CalcInputValue.Quantity("r", 10.2, "mm")],
                [new CalcExpectedValue("A", 125, "cm^2", 0.005), new CalcExpectedValue("I_y", 45730, "cm^4", 0.005), new CalcExpectedValue("I_z", 2347, "cm^4", 0.005), new CalcExpectedValue("r_g_y", 19.1, "cm", 0.005), new CalcExpectedValue("r_g_z", 4.33, "cm", 0.005), new CalcExpectedValue("W_el_y", 1957, "cm^3", 0.005), new CalcExpectedValue("W_el_z", 243, "cm^3", 0.005), new CalcExpectedValue("W_pl_y", 2232, "cm^3", 0.005), new CalcExpectedValue("W_pl_z", 379, "cm^3", 0.005), new CalcExpectedValue("J", 121, "cm^4", 0.01), new CalcExpectedValue("I_w", 1.18, "dm^6", 0.01)]),
            new CalcTestCase("ka-shs-100x100x5", "Hot-finished SHS 100×100×5 (Blue Book)",
                new CalcReferenceSource("SCI / Steel for Life Blue Book (P363 data)", "EC3 UK NA", "SHS 100×100×5.0 hot finished: A = 18.7 cm², I = 279 cm⁴, Wpl = 66.4 cm³, IT = 439 cm⁴"),
                [CalcInputValue.Choice("shape", ShapeHollowRectangle), CalcInputValue.Quantity("h", 100, "mm"), CalcInputValue.Quantity("b", 100, "mm"), CalcInputValue.Quantity("t_w", 5, "mm")],
                [new CalcExpectedValue("A", 18.7, "cm^2", 0.005), new CalcExpectedValue("I_y", 279, "cm^4", 0.005), new CalcExpectedValue("W_pl_y", 66.4, "cm^3", 0.005), new CalcExpectedValue("J", 439, "cm^4", 0.01)]),
            new CalcTestCase("ka-rectangle", "Solid rectangle 100 × 200 (analytic)",
                new CalcReferenceSource("Roark's Formulas for Stress and Strain", "8th ed.", "Table A.1 (rectangle) and Table 10.1 (torsion, a/b = 2: K = 0.229 a b³)"),
                [CalcInputValue.Choice("shape", ShapeRectangle), CalcInputValue.Quantity("b", 100, "mm"), CalcInputValue.Quantity("h", 200, "mm")],
                [new CalcExpectedValue("A", 200, "cm^2", 1e-9), new CalcExpectedValue("I_y", 6666.67, "cm^4", 1e-5), new CalcExpectedValue("I_z", 1666.67, "cm^4", 1e-5), new CalcExpectedValue("W_el_y", 666.667, "cm^3", 1e-5), new CalcExpectedValue("W_pl_y", 1000, "cm^3", 1e-6), new CalcExpectedValue("W_pl_z", 500, "cm^3", 1e-6), new CalcExpectedValue("r_g_y", 5.7735, "cm", 1e-4), new CalcExpectedValue("J", 4580, "cm^4", 0.002)]),
            new CalcTestCase("ka-tube", "Tube 200 × 10 (analytic)",
                new CalcReferenceSource("Roark's Formulas for Stress and Strain", "8th ed.", "Table A.1 (hollow circle): A = π(D² − d²)/4, I = π(D⁴ − d⁴)/64, Wpl = (D³ − d³)/6, J = 2I"),
                [CalcInputValue.Choice("shape", ShapeTube), CalcInputValue.Quantity("d", 200, "mm"), CalcInputValue.Quantity("t_w", 10, "mm")],
                [new CalcExpectedValue("A", 59.690, "cm^2", 1e-4), new CalcExpectedValue("I_y", 2701.1, "cm^4", 1e-4), new CalcExpectedValue("W_el_y", 270.11, "cm^3", 1e-4), new CalcExpectedValue("W_pl_y", 361.33, "cm^3", 1e-4), new CalcExpectedValue("J", 5402.2, "cm^4", 1e-4)]),
            new CalcTestCase("ka-angle-polygon", "Unequal angle 150 × 90 × 12, sharp corners, as parametric shape and as a polygon",
                new CalcReferenceSource("Analytic (two rectangles 12 × 150 and 78 × 12, parallel-axis theorem)", null, "A = 27.36 cm², centroid (21.395, 51.395) mm from the heel, Ixx = 631.80 cm⁴, Iyy = 174.31 cm⁴, Ixy = −191.20 cm⁴"),
                [CalcInputValue.Choice("shape", ShapePolygon), Polygon(("mm", [(0, 0), (90, 0), (90, 12), (12, 12), (12, 150), (0, 150)]))],
                [new CalcExpectedValue("A", 27.36, "cm^2", 1e-6), new CalcExpectedValue("x_c", 21.39474, "mm", 1e-5), new CalcExpectedValue("y_c", 51.39474, "mm", 1e-5), new CalcExpectedValue("I_y", 631.80, "cm^4", 2e-4), new CalcExpectedValue("I_z", 174.31, "cm^4", 2e-4), new CalcExpectedValue("I_yz", -191.20, "cm^4", 2e-4)]),
        ],
        DiagramKey: "section-properties",
        Category: "Sections",
        OutputPorts:
        [
            new CalcOutputPort("A", "cm^2", "Area of the section", "section.area"),
            new CalcOutputPort("I_y", "cm^4", "Second moment of area about the y-y axis", "section.second_moment_y", ["I_xx"]),
            new CalcOutputPort("I_z", "cm^4", "Second moment of area about the z-z axis", "section.second_moment_z", ["I_yy"]),
            new CalcOutputPort("I_yz", "cm^4", "Product of inertia about the y-y and z-z axes", "section.product_inertia", ["I_xy"]),
            new CalcOutputPort("W_el_y", "cm^3", "Elastic section modulus about the y-y axis", "section.modulus_elastic_y", ["W_el_x"], MeaningChangedAt: "1.2.0", MeaningWas: "the elastic section modulus about the vertical axis of the drawing (the minor axis of an upright I-section)", MeaningNow: "the elastic section modulus about the EN 1993 y-y axis (the major axis of an upright I-section)"),
            new CalcOutputPort("W_el_z", "cm^3", "Elastic section modulus about the z-z axis", "section.modulus_elastic_z"),
            new CalcOutputPort("W_pl_y", "cm^3", "Plastic section modulus about the y-y axis", "section.modulus_plastic_y", ["W_pl_x"], MeaningChangedAt: "1.2.0", MeaningWas: "the plastic section modulus about the vertical axis of the drawing (the minor axis of an upright I-section)", MeaningNow: "the plastic section modulus about the EN 1993 y-y axis (the major axis of an upright I-section)"),
            new CalcOutputPort("W_pl_z", "cm^3", "Plastic section modulus about the z-z axis", "section.modulus_plastic_z"),
            new CalcOutputPort("r_g_y", "mm", "Radius of gyration about the y-y axis", "section.radius_gyration_y", ["i_x"]),
            new CalcOutputPort("r_g_z", "mm", "Radius of gyration about the z-z axis", "section.radius_gyration_z", ["i_y"]),
            new CalcOutputPort("J", "cm^4", "Torsion constant", "section.constant_torsion"),
            new CalcOutputPort("I_w", "cm^6", "Warping constant of an I-section", "section.constant_warping"),
        ]);

    /// <summary>A polygon vertices table in one unit, loop 1 unless a loop number is given per group, for tests and worked examples.</summary>
    public static CalcInputValue Polygon(params (string Unit, IReadOnlyList<(double X, double Y)> Points)[] loops) =>
        CalcInputValue.Table("vertices", loops.SelectMany((l, i) => l.Points.Select(p => CalcRow.Of(
            ("x", new CalcCell(p.X, l.Unit)),
            ("y", new CalcCell(p.Y, l.Unit)),
            ("loop", new CalcCell(i + 1))))).ToList());

    /// <summary>A composite parts table (mm), for tests and worked examples.</summary>
    public static CalcInputValue Parts(params (string Shape, double B, double H, double T, double Tf, double R, double X0, double Y0, double Rot, string Sign)[] parts) =>
        CalcInputValue.Table("parts", parts.Select(p => CalcRow.Of(
            ("shape", new CalcCell(Text: p.Shape)),
            ("b", new CalcCell(p.B, "mm")),
            ("h", new CalcCell(p.H, "mm")),
            ("t_w", new CalcCell(p.T, "mm")),
            ("t_f", new CalcCell(p.Tf, "mm")),
            ("r", new CalcCell(p.R, "mm")),
            ("x0", new CalcCell(p.X0, "mm")),
            ("y0", new CalcCell(p.Y0, "mm")),
            ("rot", new CalcCell(p.Rot)),
            ("sign", new CalcCell(Text: p.Sign)))).ToList());

    public void Run(CalcRunContext c)
    {
        var shape = c.InputText("shape");
        var dims = Dims.FromInputs(c);
        IReadOnlyList<SectionLoop> loops;
        double? torsion = null;
        string torsionFormula = "";
        string torsionNote = "";
        double? warping = null;
        var mm = UnitCatalog.Parse("mm");

        try
        {
            switch (shape)
            {
                case ShapePolygon:
                    loops = PolygonLoops(c.Rows("vertices"));
                    c.Warn("no-torsion", "J: no torsion constant is computed for an arbitrary polygon.");
                    break;

                case ShapeComposite:
                    loops = CompositeLoops(c.Rows("parts"));
                    c.Warn("no-torsion", "J: no torsion constant is computed for a composite section.");
                    break;

                case ShapeCircle:
                case ShapeTube:
                    if (!TryPrimitive(shape, dims, out loops, out var missingRound, out var badRound))
                    {
                        c.Block(badRound is null ? "missing-dimension" : "geometry", badRound ?? $"{missingRound}: enter this dimension of the {shape}.");
                        return;
                    }

                    RunRound(c, shape, dims, loops);
                    return;

                default:
                    if (!TryPrimitive(shape, dims, out loops, out var missing, out var bad))
                    {
                        c.Block(bad is null ? "missing-dimension" : "geometry", bad ?? $"{missing}: enter this dimension of the {shape}.");
                        return;
                    }

                    (torsion, torsionFormula, torsionNote, warping) = PrimitiveTorsion(shape, dims, c);
                    break;
            }
        }
        catch (ArgumentException ex)
        {
            c.Block("geometry", (shape == ShapePolygon ? "vertices: " : "parts: ") + ex.Message);
            return;
        }

        var p = Snap(PolygonSection.Compute(loops));
        var n = p.VertexCount;
        var cm2 = UnitCatalog.Parse("cm^2");
        var cm3 = UnitCatalog.Parse("cm^3");
        var cm4 = UnitCatalog.Parse("cm^4");
        var cm = UnitCatalog.Parse("cm");
        var green = $"Green's theorem over {n} vertices in {loops.Count} loop(s)";

        c.Record("A", "½ ∮ (x dy − y dx)", green, ScalarValue.Of(cm2.FromSi(p.A), cm2), "Area", "Roark Table A.1");
        c.Record("x_c", "∫x dA / A", green, ScalarValue.Of(mm.FromSi(p.Xc), mm), "Centroid x from the shape origin");
        c.Record("y_c", "∫y dA / A", green, ScalarValue.Of(mm.FromSi(p.Yc), mm), "Centroid y from the shape origin");
        c.Record("I_y", "∫y² dA − A y_c²", green, ScalarValue.Of(cm4.FromSi(p.Ixx), cm4), "Second moment of area about the centroidal y-y axis");
        c.Record("I_z", "∫x² dA − A x_c²", green, ScalarValue.Of(cm4.FromSi(p.Iyy), cm4), "Second moment of area about the centroidal z-z axis");
        c.Record("I_yz", "∫xy dA − A x_c y_c", green, ScalarValue.Of(cm4.FromSi(p.Ixy), cm4), "Product of inertia about the centroidal y-y and z-z axes");
        c.Assign("I_1", "(I_y + I_z)/2 + sqrt(((I_y - I_z)/2)^2 + I_yz^2)", "cm^4", "Major principal second moment");
        c.Assign("I_2", "(I_y + I_z)/2 - sqrt(((I_y - I_z)/2)^2 + I_yz^2)", "cm^4", "Minor principal second moment");
        c.Record("theta", "½ atan2(−2 I_yz, I_y − I_z)", $"½ atan2({F(cm4.FromSi(-2 * p.Ixy))}, {F(cm4.FromSi(p.Ixx - p.Iyy))})", ScalarValue.Of(p.Theta * 180 / Math.PI, "deg"), "Angle from the y-y axis to the major principal axis, anticlockwise");
        c.Record("y_top", "y_max − y_c", green, ScalarValue.Of(mm.FromSi(p.YTop), mm), "Extreme fibre above the centroid");
        c.Record("y_bot", "y_c − y_min", green, ScalarValue.Of(mm.FromSi(p.YBottom), mm), "Extreme fibre below the centroid");
        c.Record("x_ext", "max(x_max − x_c, x_c − x_min)", green, ScalarValue.Of(mm.FromSi(Math.Max(p.XRight, p.XLeft)), mm), "Extreme fibre from the centroid, x");
        c.Assign("W_el_y", "I_y / max(y_top, y_bot)", "cm^3", "Elastic section modulus about the y-y axis, governing fibre");
        c.Assign("W_el_z", "I_z / x_ext", "cm^3", "Elastic section modulus about the z-z axis");
        c.Record("W_pl_y", "|Q_above| + |Q_below| about the equal-area axis", $"equal-area axis at y = {F(mm.FromSi(p.YPna))} mm", ScalarValue.Of(cm3.FromSi(p.WplX), cm3), "Plastic section modulus about the y-y axis");
        c.Record("W_pl_z", "|Q_right| + |Q_left| about the equal-area axis", $"equal-area axis at x = {F(mm.FromSi(p.XPna))} mm", ScalarValue.Of(cm3.FromSi(p.WplY), cm3), "Plastic section modulus about the z-z axis");
        c.Assign("r_g_y", "sqrt(I_y / A)", "mm", "Radius of gyration about the y-y axis");
        c.Assign("r_g_z", "sqrt(I_z / A)", "mm", "Radius of gyration about the z-z axis");
        c.Assign("alpha_pl_y", "W_pl_y / W_el_y", null, "Shape factor about the y-y axis");

        foreach (var (symbol, text) in new[] { ("A", "Area"), ("x_c", "Centroid x"), ("y_c", "Centroid y"), ("I_y", "Second moment of area about the y-y axis"), ("I_z", "Second moment of area about the z-z axis"), ("I_yz", "Product of inertia about the y-y and z-z axes"), ("I_1", "Major principal second moment"), ("I_2", "Minor principal second moment"), ("theta", "Principal axis angle"), ("W_el_y", "Elastic section modulus about the y-y axis"), ("W_el_z", "Elastic section modulus about the z-z axis"), ("W_pl_y", "Plastic section modulus about the y-y axis"), ("W_pl_z", "Plastic section modulus about the z-z axis"), ("r_g_y", "Radius of gyration about the y-y axis"), ("r_g_z", "Radius of gyration about the z-z axis"), ("alpha_pl_y", "Shape factor about the y-y axis") })
            c.Output(symbol, text);

        if (torsion is { } j)
        {
            c.Record("J", torsionFormula, torsionNote, ScalarValue.Of(cm4.FromSi(j), cm4), "Torsion constant");
            c.Output("J", "Torsion constant");
        }

        if (warping is { } iw)
        {
            c.Record("I_w", "I_z (h − t_f)² / 4", $"{F(cm4.FromSi(p.Iyy))} cm⁴ × ({F(dims.H * 1e3)} − {F(dims.Tf * 1e3)})² mm² / 4", new ScalarValue(iw, Dimension.L.Pow(6), UnitCatalog.Parse("cm^6")), "Warping constant", "AISC DG9");
            c.Output("I_w", "Warping constant");
        }

        c.Series(new CalcSeries("Outline", "x", "mm", "y", "mm", loops[0].Vertices.Select(v => (mm.FromSi(v.X), mm.FromSi(v.Y))).Append((mm.FromSi(loops[0].Vertices[0].X), mm.FromSi(loops[0].Vertices[0].Y))).ToList()));
    }

    /// <summary>Rounds floating-point noise (symmetric shapes give centroids of 1e-15 m) to exact zeros so sheets are reproducible across platforms.</summary>
    private static SectionProperties Snap(SectionProperties p)
    {
        var size = Math.Max(p.XRight + p.XLeft, p.YTop + p.YBottom);
        var inertia = Math.Max(p.Ixx, p.Iyy);
        return p with
        {
            Xc = Zero(p.Xc, size), Yc = Zero(p.Yc, size), XPna = Zero(p.XPna, size), YPna = Zero(p.YPna, size),
            Ixy = Zero(p.Ixy, inertia), Theta = Zero(p.Theta, 1),
        };
    }

    private static double Zero(double v, double scale) => Math.Abs(v) < 1e-9 * scale ? 0 : v;

    /// <summary>Circle and tube: every property in closed form, so the sheet shows formula → substitution → result.</summary>
    private static void RunRound(CalcRunContext c, string shape, Dims d, IReadOnlyList<SectionLoop> loops)
    {
        var tube = shape == ShapeTube;
        var mm = UnitCatalog.Parse("mm");
        c.Assign("d_i", tube ? "d - 2*t_w" : "0 mm", "mm", "Inner diameter");
        c.Assign("A", "pi * (d^2 - d_i^2) / 4", "cm^2", "Area", "Roark Table A.1");
        c.Assign("x_c", "0 mm", "mm", "Centroid x from the shape origin");
        c.Assign("y_c", "0 mm", "mm", "Centroid y from the shape origin");
        c.Assign("I_y", "pi * (d^4 - d_i^4) / 64", "cm^4", "Second moment of area about the centroidal y-y axis", "Roark Table A.1");
        c.Assign("I_z", "I_y", "cm^4", "Second moment of area about the centroidal z-z axis");
        c.Assign("I_yz", "0 cm^4", "cm^4", "Product of inertia about the centroidal y-y and z-z axes");
        c.Assign("I_1", "I_y", "cm^4", "Major principal second moment");
        c.Assign("I_2", "I_z", "cm^4", "Minor principal second moment");
        c.Assign("theta", "0 deg", "deg", "Angle from the y-y axis to the major principal axis");
        c.Assign("y_top", "d / 2", "mm", "Extreme fibre above the centroid");
        c.Assign("y_bot", "d / 2", "mm", "Extreme fibre below the centroid");
        c.Assign("x_ext", "d / 2", "mm", "Extreme fibre from the centroid, x");
        c.Assign("W_el_y", "I_y / y_top", "cm^3", "Elastic section modulus about the y-y axis");
        c.Assign("W_el_z", "I_z / x_ext", "cm^3", "Elastic section modulus about the z-z axis");
        c.Assign("W_pl_y", "(d^3 - d_i^3) / 6", "cm^3", "Plastic section modulus about the y-y axis", "Roark Table A.1");
        c.Assign("W_pl_z", "W_pl_y", "cm^3", "Plastic section modulus about the z-z axis");
        c.Assign("r_g_y", "sqrt(I_y / A)", "mm", "Radius of gyration about the y-y axis");
        c.Assign("r_g_z", "r_g_y", "mm", "Radius of gyration about the z-z axis");
        c.Assign("alpha_pl_y", "W_pl_y / W_el_y", null, "Shape factor about the y-y axis");
        c.Assign("J", "pi * (d^4 - d_i^4) / 32", "cm^4", "Torsion constant", "Roark Table 10.1");

        foreach (var (symbol, text) in new[] { ("A", "Area"), ("x_c", "Centroid x"), ("y_c", "Centroid y"), ("I_y", "Second moment of area about the y-y axis"), ("I_z", "Second moment of area about the z-z axis"), ("I_yz", "Product of inertia about the y-y and z-z axes"), ("I_1", "Major principal second moment"), ("I_2", "Minor principal second moment"), ("theta", "Principal axis angle"), ("W_el_y", "Elastic section modulus about the y-y axis"), ("W_el_z", "Elastic section modulus about the z-z axis"), ("W_pl_y", "Plastic section modulus about the y-y axis"), ("W_pl_z", "Plastic section modulus about the z-z axis"), ("r_g_y", "Radius of gyration about the y-y axis"), ("r_g_z", "Radius of gyration about the z-z axis"), ("alpha_pl_y", "Shape factor about the y-y axis"), ("J", "Torsion constant") })
            c.Output(symbol, text);

        c.Series(new CalcSeries("Outline", "x", "mm", "y", "mm", loops[0].Vertices.Select(v => (mm.FromSi(v.X), mm.FromSi(v.Y))).Append((mm.FromSi(loops[0].Vertices[0].X), mm.FromSi(loops[0].Vertices[0].Y))).ToList()));
    }

    /// <summary>The dimensions of one primitive, SI; NaN when not given.</summary>
    private sealed record Dims(double H, double B, double T, double Tf, double D, double R, double Ri)
    {
        public static Dims FromInputs(CalcRunContext c) => new(Optional(c, "h"), Optional(c, "b"), Optional(c, "t_w"), Optional(c, "t_f"), Optional(c, "d"), Optional(c, "r"), Optional(c, "r_i"));

        public static Dims FromRow(CalcRow row) => new(Optional(row, "h"), Optional(row, "b"), OptionalThickness(row), Optional(row, "t_f"), Optional(row, "h"), Optional(row, "r"), double.NaN);

        private static double Optional(CalcRunContext c, string symbol) => c.Has(symbol) ? c.InputSi(symbol) : double.NaN;

        /// <summary>The wall, web or leg thickness of a part: column <c>t_w</c>, or <c>t</c> in a table saved before the column was renamed.</summary>
        private static double OptionalThickness(CalcRow row) => row.Get("t_w") is not null ? Optional(row, "t_w") : Optional(row, "t");

        private static double Optional(CalcRow row, string key) => row.Get(key)?.Number is { } n ? UnitCatalog.Parse(string.IsNullOrEmpty(row.Get(key)!.Unit) ? "mm" : row.Get(key)!.Unit!).ToSi(n) : double.NaN;
    }

    private static bool TryPrimitive(string shape, Dims d, out IReadOnlyList<SectionLoop> loops, out string missing, out string? bad)
    {
        loops = [];
        missing = "";
        bad = null;

        switch (shape)
        {
            case ShapeRectangle:
                if (!Need(d.B, "b", ref missing) || !Need(d.H, "h", ref missing)) return false;
                loops = Shapes.Rectangle(d.B, d.H);
                return true;

            case ShapeCircle:
                if (!Need(d.D, "d", ref missing)) return false;
                loops = Shapes.Circle(d.D);
                return true;

            case ShapeTube:
                if (!Need(d.D, "d", ref missing) || !Need(d.T, "t_w", ref missing)) return false;
                if (2 * d.T >= d.D) { bad = "t_w: the wall thickness must be less than half the diameter."; return false; }
                loops = Shapes.Tube(d.D, d.T);
                return true;

            case ShapeHollowRectangle:
            {
                if (!Need(d.B, "b", ref missing) || !Need(d.H, "h", ref missing) || !Need(d.T, "t_w", ref missing)) return false;
                if (2 * d.T >= Math.Min(d.B, d.H)) { bad = "t_w: the wall thickness must be less than half the smaller side."; return false; }
                var (ro, ri) = HollowRadii(d);
                if (ri > ro) { bad = "r_i: the inner corner radius must not exceed the outer."; return false; }
                loops = Shapes.HollowRectangle(d.B, d.H, d.T, ro, ri);
                return true;
            }

            case ShapeI:
            case ShapeChannel:
            case ShapeTee:
            {
                if (!Need(d.H, "h", ref missing) || !Need(d.B, "b", ref missing) || !Need(d.T, "t_w", ref missing) || !Need(d.Tf, "t_f", ref missing)) return false;
                var flanges = shape == ShapeTee ? 1 : 2;
                if (flanges * d.Tf >= d.H) { bad = "t_f: the flanges must be thinner than the depth."; return false; }
                if (d.T >= d.B) { bad = "t_w: the web must be thinner than the width."; return false; }
                var r = double.IsNaN(d.R) ? 0 : d.R;
                if (shape != ShapeChannel && 2 * r + d.T > d.B) { bad = "r: the root radii do not fit inside the flange width."; return false; }
                if (2 * r + flanges * d.Tf > d.H) { bad = "r: the root radii do not fit inside the depth."; return false; }
                loops = shape switch
                {
                    ShapeI => Shapes.ISection(d.H, d.B, d.T, d.Tf, r),
                    ShapeChannel => Shapes.Channel(d.H, d.B, d.T, d.Tf, r),
                    _ => Shapes.Tee(d.H, d.B, d.T, d.Tf, r),
                };
                return true;
            }

            case ShapeAngle:
            {
                if (!Need(d.H, "h", ref missing) || !Need(d.B, "b", ref missing) || !Need(d.T, "t_w", ref missing)) return false;
                if (d.T >= Math.Min(d.H, d.B)) { bad = "t_w: the leg thickness must be less than the shorter leg."; return false; }
                var r = double.IsNaN(d.R) ? 0 : d.R;
                if (d.T + r > Math.Min(d.H, d.B)) { bad = "r: the root radius does not fit inside the legs."; return false; }
                loops = Shapes.Angle(d.H, d.B, d.T, r);
                return true;
            }

            default:
                bad = $"shape: '{shape}' is not a shape this sheet knows.";
                return false;
        }
    }

    /// <summary>Hollow-rectangle corner radii: as given, else the EN 10210-2 hot-finished idealisation r_o = 1.5 t, r_i = 1.0 t.</summary>
    private static (double Ro, double Ri) HollowRadii(Dims d) => (double.IsNaN(d.R) ? 1.5 * d.T : d.R, double.IsNaN(d.Ri) ? Math.Min(d.T, double.IsNaN(d.R) ? 1.5 * d.T : d.R) : d.Ri);

    private static bool Need(double value, string symbol, ref string missing)
    {
        if (double.IsNaN(value) || value <= 0)
        {
            missing = symbol;
            return false;
        }

        return true;
    }

    private static (double? J, string Formula, string Note, double? Iw) PrimitiveTorsion(string shape, Dims d, CalcRunContext c)
    {
        switch (shape)
        {
            case ShapeRectangle:
                return (Torsion.Rectangle(d.B, d.H), "a b³ [1/3 − (192/π⁵)(b/a) Σ tanh(nπa/2b)/n⁵]", $"a = {F(Math.Max(d.B, d.H) * 1e3)} mm, b = {F(Math.Min(d.B, d.H) * 1e3)} mm, 25 odd terms", null);

            case ShapeCircle:
                return (Torsion.Circle(d.D), "π d⁴ / 32", $"d = {F(d.D * 1e3)} mm", null);

            case ShapeTube:
                return (Torsion.Tube(d.D, d.T), "π (d⁴ − (d − 2t)⁴) / 32", $"d = {F(d.D * 1e3)} mm, t = {F(d.T * 1e3)} mm", null);

            case ShapeHollowRectangle:
            {
                var (ro, ri) = HollowRadii(d);
                var rm = (ro + ri) / 2;
                var bm = d.B - d.T;
                var hm = d.H - d.T;
                var areaMid = bm * hm - (4 - Math.PI) * rm * rm;
                var perimeterMid = 2 * (bm + hm) - (8 - 2 * Math.PI) * rm;
                c.Warn("thin-walled-torsion", "J: Bredt's thin-walled formula on the wall mid-line is used.");
                if (d.T > Math.Min(d.B, d.H) / 5) c.Warn("thick-wall", "t_w: the wall is thicker than a fifth of the smaller side. Bredt's estimate is rough.");
                return (Torsion.ClosedThinWalled(areaMid, perimeterMid, d.T), "4 A_m² t / p_m + p_m t³ / 3", $"A_m = {F(areaMid * 1e6)} mm², p_m = {F(perimeterMid * 1e3)} mm, t = {F(d.T * 1e3)} mm", null);
            }

            case ShapeI:
            {
                var r = double.IsNaN(d.R) ? 0 : d.R;
                c.Warn("thin-walled-torsion", "J: the open thin-walled sum with the rolled-section fillet term is used.");
                var iw = PolygonSection.Compute(Shapes.ISection(d.H, d.B, d.T, d.Tf, r)).Iyy * (d.H - d.Tf) * (d.H - d.Tf) / 4;
                return (Torsion.ISection(d.H, d.B, d.T, d.Tf, r), r > 0 ? "(2 b t_f³ + (h − 2 t_f) t_w³)/3 + 2 α D⁴ − 0.42 t_f⁴" : "(2 b t_f³ + (h − 2 t_f) t_w³)/3", $"b = {F(d.B * 1e3)}, t_f = {F(d.Tf * 1e3)}, h = {F(d.H * 1e3)}, t_w = {F(d.T * 1e3)}, r = {F(r * 1e3)} mm", iw);
            }

            case ShapeChannel:
            case ShapeTee:
            {
                var flanges = shape == ShapeTee ? 1 : 2;
                c.Warn("thin-walled-torsion", shape == ShapeTee
                    ? "J: the sum is about 2.8 % high for a sharp tee. A root fillet, which is ignored, raises the true J."
                    : "J: omitting the root fillet makes J about 4.3 % low for a 200 × 75 channel. Take J from the section tables where it matters.");
                return (Torsion.OpenThinWalled([(flanges * d.B, d.Tf), (d.H - flanges * d.Tf, d.T)]), "Σ b t³ / 3", $"flanges {flanges} × {F(d.B * 1e3)} × {F(d.Tf * 1e3)}³, web {F((d.H - flanges * d.Tf) * 1e3)} × {F(d.T * 1e3)}³ mm", null);
            }

            case ShapeAngle:
                c.Warn("thin-walled-torsion", "J: omitting the root fillet makes J about 8.9 % low for a 100 × 75 angle. Take J from the section tables where it matters.");
                return (Torsion.OpenThinWalled([(d.H, d.T), (d.B - d.T, d.T)]), "Σ b t³ / 3", $"legs {F(d.H * 1e3)} and {F((d.B - d.T) * 1e3)} × {F(d.T * 1e3)}³ mm", null);

            default:
                return (null, "", "", null);
        }
    }

    private static IReadOnlyList<SectionLoop> PolygonLoops(IReadOnlyList<CalcRow> rows)
    {
        if (rows.Count < 3)
            throw new ArgumentException("enter at least three points for the polygon outline.");

        var groups = rows.GroupBy(r => (int)Math.Round(r.Get("loop")?.Number ?? 1)).OrderBy(g => g.Key).ToList();

        if (groups.Count > PolygonSection.MaxLoops)
            throw new ArgumentException($"use at most {PolygonSection.MaxLoops} loops.");

        var loops = new List<SectionLoop>();

        foreach (var g in groups)
        {
            var pts = g.Select(r => new Point2(Cell(r, "x"), Cell(r, "y"))).ToList();

            if (pts.Count < 3)
                throw new ArgumentException($"loop {g.Key} has fewer than three points. Enter at least three.");

            loops.Add(new SectionLoop(pts, g.Key != groups[0].Key));
        }

        CheckLoops(groups.Select(g => g.Key).ToList(), loops);
        return loops;
    }

    /// <summary>
    /// A polygon's loops must each be simple (no edge crossing another of the same loop), must not cross one another, and every hole must lie
    /// inside the outline and outside the other holes; otherwise Green's theorem integrates a meaningless region (P1-struct.section-properties-04).
    /// </summary>
    private static void CheckLoops(IReadOnlyList<int> numbers, IReadOnlyList<SectionLoop> loops)
    {
        for (var i = 0; i < loops.Count; i++)
        {
            var v = loops[i].Vertices;

            for (var a = 0; a < v.Count; a++)
            {
                for (var b = a + 2; b < v.Count; b++)
                {
                    if (a == 0 && b == v.Count - 1)
                        continue;

                    if (Crosses(v[a], v[(a + 1) % v.Count], v[b], v[(b + 1) % v.Count]))
                        throw new ArgumentException($"loop {numbers[i]} crosses itself, as edge {a + 1} crosses edge {b + 1}. Redraw it as a simple polygon.");
                }
            }
        }

        for (var i = 0; i < loops.Count; i++)
        {
            for (var j = i + 1; j < loops.Count; j++)
            {
                var u = loops[i].Vertices;
                var w = loops[j].Vertices;

                for (var a = 0; a < u.Count; a++)
                {
                    for (var b = 0; b < w.Count; b++)
                    {
                        if (Crosses(u[a], u[(a + 1) % u.Count], w[b], w[(b + 1) % w.Count]))
                            throw new ArgumentException($"loop {numbers[j]} crosses loop {numbers[i]}. A hole must lie inside the outline and clear of the other holes.");
                    }
                }
            }
        }

        for (var j = 1; j < loops.Count; j++)
        {
            if (!Inside(loops[0].Vertices, loops[j].Vertices[0]))
                throw new ArgumentException($"loop {numbers[j]} lies outside the outline (loop {numbers[0]}). A hole must lie wholly inside it.");

            for (var k = 1; k < loops.Count; k++)
            {
                if (k != j && Inside(loops[k].Vertices, loops[j].Vertices[0]))
                    throw new ArgumentException($"loop {numbers[j]} lies inside loop {numbers[k]}, which is itself a hole. Give an island as a separate part of a composite.");
            }
        }
    }

    /// <summary>Whether two segments cross properly (their interiors intersect at one point, not merely touch).</summary>
    private static bool Crosses(Point2 p1, Point2 p2, Point2 p3, Point2 p4)
    {
        var d1 = Orientation(p3, p4, p1);
        var d2 = Orientation(p3, p4, p2);
        var d3 = Orientation(p1, p2, p3);
        var d4 = Orientation(p1, p2, p4);
        return d1 * d2 < 0 && d3 * d4 < 0;
    }

    private static double Orientation(Point2 a, Point2 b, Point2 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

    /// <summary>Even-odd point-in-polygon test.</summary>
    private static bool Inside(IReadOnlyList<Point2> polygon, Point2 p)
    {
        var inside = false;

        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];

            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }

        return inside;
    }

    private static IReadOnlyList<SectionLoop> CompositeLoops(IReadOnlyList<CalcRow> rows)
    {
        if (rows.Count == 0)
            throw new ArgumentException("enter at least one part.");

        var loops = new List<SectionLoop>();

        foreach (var (row, index) in rows.Select((r, i) => (r, i + 1)))
        {
            var shape = row.Get("shape")?.Text ?? "";
            var d = Dims.FromRow(row);
            var subtract = string.Equals(row.Get("sign")?.Text, "subtract", StringComparison.OrdinalIgnoreCase);

            if (!TryPrimitive(shape, d, out var partLoops, out var missing, out var bad))
                throw new ArgumentException($"part {index} ({shape}): " + (bad ?? $"enter {missing}."));

            var p = PolygonSection.Compute(partLoops);
            var rot = (row.Get("rot")?.Number ?? 0) * Math.PI / 180;
            var (s, co) = Math.SinCos(rot);
            var x0 = Cell(row, "x0");
            var y0 = Cell(row, "y0");

            foreach (var l in partLoops)
            {
                var moved = l.Transform(x0 - (co * p.Xc - s * p.Yc), y0 - (s * p.Xc + co * p.Yc), rot);
                // A subtracted part turns every solid loop into a hole and every hole of the part (a tube's bore) into solid; an added part keeps its loops as they are.
                loops.Add(subtract ? new SectionLoop(moved.Vertices, !l.IsHole) : moved);
            }
        }

        if (loops.Count > PolygonSection.MaxLoops)
            throw new ArgumentException($"use at most {PolygonSection.MaxLoops} loops.");

        return loops;
    }

    private static double Cell(CalcRow row, string key)
    {
        var cell = row.Get(key);

        if (cell?.Number is not { } n)
            throw new ArgumentException($"column '{key}': enter a number.");

        return UnitCatalog.Parse(string.IsNullOrEmpty(cell.Unit) ? "mm" : cell.Unit).ToSi(n);
    }

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
