using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Structural;
using Tempest.Core.Calculations.Modules.Diagrams;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// The reference diagram of every worksheet template (ADR-0158, Engineering
/// Principle 33, raised into the worksheet form by D-41): a declarative,
/// not-to-scale drawing per template, every shape bound to the worksheet
/// input (its symbol) it stands for, drawn by the same
/// <see cref="Views.CalculationDiagramView"/> the legacy calculators used.
/// </summary>
/// <remarks>
/// Inputs only, never a result. Templates ported from a legacy module keep
/// that module's drawing, rebound to the worksheet's symbols; the others are
/// drawn here for the first time. A coverage test requires exactly one
/// diagram per registered template and every bound name to be one of its
/// inputs.
/// </remarks>
public static partial class CalcDiagrams
{
    private static readonly IReadOnlyDictionary<string, string> Always = new Dictionary<string, string>();

    private static Dictionary<string, string> When(string input, string value) => new() { [input] = value };

    /// <summary>Every diagram, exactly one per worksheet template.</summary>
    public static IReadOnlyList<CalculationDiagramSpec> All { get; } =
    [
        ToleranceStack(), Iso286Fit(), PressFit(), BoltedJoint(), BoltGroup(), ThickCylinder(), LiftingLug(), BearingLife(),
        FatigueMiner(), ShaftStress(), ThermalExpansion(), ThinVessel(), MaterialMargin(),
        SectionProperties(), Frame2D(), BeamFormulas(), MemberChecks(), ColumnBuckling(), FilletWeld(),
        PipePump(), Psychrometrics(), DuctSizing(), PlaneWall(),
        MotorSizing(), EnclosureHeat(), VoltageDrop(), ThermalChain(),
        // ---- MechFp ----
        BearingLifeIso281(),
        Mechanism(),
        Shaft(),
        SnapFit(),
        Spring(),
        WeldGroup(),
        Accumulator(),
        Cylinder(),
        ORingGland(),
    ];

    /// <summary>The diagram of the template <paramref name="templateId"/>, or <see langword="null"/>.</summary>
    public static CalculationDiagramSpec? For(string templateId) =>
        All.FirstOrDefault(d => string.Equals(d.CalculationId, templateId, StringComparison.Ordinal));

    // ---- Mechanical (sheet 400 x 240, y downward, not to scale) ----

    private static CalculationDiagramSpec ToleranceStack() =>
        new(ToleranceStackCalculator.Id,
        [
            new DiagramVariant("A linear stack (representative; the contributors as listed): the bore adds, the parts subtract, the gap closes the loop", Always,
            [
                new DiagramPlate("base", 40, 140, 300, 16),
                new DiagramPlate("wall-left", 40, 70, 20, 70),
                new DiagramPlate("wall-right", 320, 70, 20, 70),
                new DiagramPlate("part-a", 60, 100, 120, 40, "contributors"),
                new DiagramPlate("part-b", 180, 100, 110, 40, "contributors"),
                new DiagramDimension("bore", new(60, 70), new(320, 70), -16, "contributors"),
                new DiagramLabel("bore-note", new(190, 40), Symbol: "+ bore ± t"),
                new DiagramDimension("part-a-length", new(60, 100), new(180, 100), -12, "contributors"),
                new DiagramLabel("part-a-note", new(120, 76), Symbol: "− A ± t"),
                new DiagramDimension("part-b-length", new(180, 100), new(290, 100), -12, "contributors"),
                new DiagramLabel("part-b-note", new(235, 76), Symbol: "− B ± t"),
                new DiagramDimension("gap", new(290, 120), new(320, 120), 0, "minimum"),
                new DiagramLabel("gap-note", new(305, 170), Symbol: "gap"),
                new DiagramLabel("contributors", new(110, 192), "contributors", "contributors"),
                new DiagramLabel("sigma", new(110, 216), "k", "k (σ per ½ band)"),
                new DiagramLabel("minimum", new(290, 192), "minimum", "min"),
                new DiagramLabel("maximum", new(290, 216), "maximum", "max"),
            ]),
        ]);

    private static CalculationDiagramSpec Iso286Fit() =>
        new(Iso286FitCalculator.Id,
        [
            new DiagramVariant("A hole and its shaft of one nominal size D, and their tolerance zones about the zero line (hole above, shaft below as drawn; the letters place them)", Always,
            [
                new DiagramLabel("part-note", new(100, 22), Symbol: "Hole and shaft"),
                new DiagramPlate("hub-top", 30, 44, 140, 30),
                new DiagramPlate("hub-bottom", 30, 146, 140, 30),
                new DiagramPlate("shaft", 10, 80, 180, 60),
                new DiagramDimension("size", new(100, 80), new(100, 140), -70, "D", "D", new(100, 198)),
                new DiagramLabel("zone-note", new(300, 22), Symbol: "Zones"),
                new DiagramPolyline("zero-line", [new(220, 120), new(390, 120)], Dashed: true, Thickness: 1, Symbol: "0", LabelAt: new(384, 108)),
                new DiagramPlate("hole-zone", 240, 70, 50, 44, "hole_grade"),
                new DiagramPlate("shaft-zone", 310, 128, 50, 40, "shaft_grade"),
                new DiagramLabel("hole-letter", new(250, 52), "hole_letter", "hole"),
                new DiagramLabel("hole-grade", new(250, 196), "hole_grade", "IT hole"),
                new DiagramLabel("shaft-letter", new(350, 52), "shaft_letter", "shaft"),
                new DiagramLabel("shaft-grade", new(350, 220), "shaft_grade", "IT shaft"),
            ]),
        ]);

    private static CalculationDiagramSpec PressFit() =>
        new(PressFitCalculator.Id,
        [
            new DiagramVariant("A hub shrunk onto a shaft: section across the interface, and the engagement length beside it", Always,
            [
                new DiagramCircle("hub", new(110, 120), 90, Filled: true, "d_o"),
                new DiagramCircle("shaft", new(110, 120), 50, Filled: true, "d", Solid: false),
                new DiagramCircle("bore", new(110, 120), 18, Filled: false, "d_i"),
                new DiagramDimension("outer", new(110, 120), new(46.4, 183.6), 0, "d_o", "d_o", new(70, 222)),
                new DiagramDimension("interface", new(110, 120), new(160, 120), 0, "d", "d", new(170, 142)),
                new DiagramDimension("bore-d", new(110, 120), new(110, 102), 0, "d_i", "d_i", new(110, 86)),
                new DiagramLabel("interference", new(110, 18), "delta", "δ"),
                new DiagramLabel("side-note", new(300, 16), Symbol: "Side"),
                new DiagramPlate("hub-side", 250, 60, 100, 120, "L"),
                new DiagramPlate("shaft-side", 220, 100, 160, 40),
                new DiagramDimension("length", new(250, 180), new(350, 180), 14, "L", "L"),
                new DiagramLabel("hub-material", new(300, 222), "E_h", "E_h"),
                new DiagramLabel("friction", new(240, 40), "mu", "μ"),
                new DiagramLabel("shaft-material", new(345, 40), "E_s", "E_s"),
            ]),
        ]);

    private static CalculationDiagramSpec BoltedJoint() =>
        new(BoltedJointCalculator.Id,
        [
            new DiagramVariant("A preloaded bolt clamping two members, the working load pulling them apart; beside it, bolt and members as two springs in parallel", Always,
            [
                new DiagramPlate("member-top", 30, 80, 160, 30, "E_P"),
                new DiagramPlate("member-bottom", 30, 110, 160, 30, "E_P"),
                new DiagramPlate("shank", 100, 66, 20, 96, "d"),
                new DiagramPlate("head", 86, 66, 48, 14, "d_w"),
                new DiagramPlate("nut", 86, 140, 48, 14),
                new DiagramMoment("torque", new(110, 46), 14, Clockwise: true, "M_A", "M_A", new(175, 22)),
                new DiagramDimension("clamp", new(30, 80), new(30, 140), -14, "l_k"),
                new DiagramLabel("clamp-length", new(200, 196), "l_k", "l_k"),
                new DiagramPointLoad("external-top-left", new(45, 64), DiagramDirection.Up, 26, "F_A"),
                new DiagramPointLoad("external-bottom-left", new(45, 156), DiagramDirection.Down, 26, "F_A", "F_A", new(45, 196)),
                new DiagramPointLoad("external-top-right", new(175, 64), DiagramDirection.Up, 26, "F_A"),
                new DiagramPointLoad("external-bottom-right", new(175, 156), DiagramDirection.Down, 26, "F_A"),
                new DiagramMember("model-top", new(232, 70), new(372, 70), 4),
                new DiagramMember("model-bottom", new(232, 160), new(372, 160), 4),
                new DiagramSpring("bolt-spring", new(256, 70), new(256, 160), "E_S", "bolt", new(300, 52)),
                new DiagramSpring("member-spring", new(346, 70), new(346, 160), "E_P", "members", new(330, 180)),
                new DiagramLabel("proof", new(110, 222), "R_p02", "R_p0.2"),
                new DiagramLabel("residual", new(290, 222), "F_KR_req", "F_KR,req"),
            ]),
        ]);

    private static CalculationDiagramSpec BoltGroup() =>
        new(BoltGroupCalculator.Id,
        [
            new DiagramVariant("A bolt group (six drawn; the bolts as listed) under an in-plane load at (e_x, e_y), x and y from one origin, + as drawn", Always,
            [
                new DiagramMember("x-axis", new(40, 200), new(372, 200), 1),
                new DiagramLabel("x-note", new(384, 200), Symbol: "x"),
                new DiagramMember("y-axis", new(40, 200), new(40, 40), 1),
                new DiagramLabel("y-note", new(40, 30), Symbol: "y"),
                new DiagramPlate("plate", 70, 40, 130, 132),
                new DiagramCircle("bolt-1", new(100, 62), 8, Filled: true, "bolts", Solid: true),
                new DiagramCircle("bolt-2", new(170, 62), 8, Filled: true, "bolts", Solid: true),
                new DiagramCircle("bolt-3", new(100, 106), 8, Filled: true, "bolts", Solid: true),
                new DiagramCircle("bolt-4", new(170, 106), 8, Filled: true, "bolts", "bolts", new(135, 26), Solid: true),
                new DiagramCircle("bolt-5", new(100, 150), 8, Filled: true, "bolts", Solid: true),
                new DiagramCircle("bolt-6", new(170, 150), 8, Filled: true, "bolts", Solid: true),
                new DiagramMoment("torque", new(135, 106), 14, Clockwise: false, "M_z", "M_z", new(135, 186)),
                new DiagramDimension("load-point-y", new(290, 200), new(290, 80), 0, "e_y", "e_y", new(340, 150)),
                new DiagramDimension("load-point-x", new(40, 200), new(290, 200), 18, "e_x", "e_x"),
                new DiagramCircle("load-point", new(290, 80), 4, Filled: true, "e_x", Solid: true),
                new DiagramPointLoad("load-y", new(290, 36), DiagramDirection.Up, 44, "F_y", "F_y", new(290, 24)),
                new DiagramPointLoad("load-x", new(342, 80), DiagramDirection.Right, 52, "F_x", "F_x", new(344, 96)),
                new DiagramLabel("shear-resistance", new(60, 230), "F_v_Rd", "F_v_Rd"),
                new DiagramLabel("tension-resistance", new(300, 230), "F_t_Rd", "F_t_Rd"),
            ]),
        ]);

    private static CalculationDiagramSpec ThickCylinder()
    {
        static DiagramVariant Variant(string caption, IReadOnlyDictionary<string, string> when, bool closedEnds)
        {
            var elements = new List<DiagramElement>
            {
                new DiagramCircle("wall", new(130, 120), 90, Filled: true, "b"),
                new DiagramCircle("bore", new(130, 120), 60, Filled: false, "a"),
                new DiagramPointLoad("internal-up", new(130, 60), DiagramDirection.Up, 24, "p_i", "p_i", new(130, 98)),
                new DiagramPointLoad("internal-left", new(70, 120), DiagramDirection.Left, 24, "p_i"),
                new DiagramPointLoad("external-top", new(130, 30), DiagramDirection.Down, 26, "p_o", "p_o", new(205, 20)),
                new DiagramPointLoad("external-left", new(40, 120), DiagramDirection.Right, 28, "p_o"),
                new DiagramPointLoad("external-right", new(220, 120), DiagramDirection.Left, 28, "p_o"),
                new DiagramDimension("inner-radius", new(130, 120), new(190, 120), 0, "a", "a", new(158, 136)),
                new DiagramDimension("outer-radius", new(130, 120), new(66.4, 183.6), 0, "b", "b", new(46, 222)),
                new DiagramLabel("side-note", new(330, 50), Symbol: "Side"),
                new DiagramPlate("side-wall-top", 290, 70, 80, 12),
                new DiagramPlate("side-wall-bottom", 290, 158, 80, 12),
            };

            if (closedEnds)
            {
                elements.Add(new DiagramPlate("end-left", 282, 70, 8, 100, "closed"));
                elements.Add(new DiagramPlate("end-right", 370, 70, 8, 100, "closed"));
            }

            elements.Add(new DiagramLabel("closed-ends", new(330, 196), "closed", "closed ends"));
            elements.Add(new DiagramLabel("allowable", new(330, 222), "sigma_allow", "σ_allow"));
            return new DiagramVariant(caption, when, elements);
        }

        return new(ThickCylinderCalculator.Id,
        [
            Variant("A thick-walled cylinder in section, bore radius a, outer radius b, pressure inside and out; closed ends carry the pressures axially", When("closed", CalcDiagramReader.BooleanTrue), closedEnds: true),
            Variant("A thick-walled cylinder in section, bore radius a, outer radius b, pressure inside and out; open ends, no axial load", Always, closedEnds: false),
        ]);
    }

    private static CalculationDiagramSpec LiftingLug() =>
        new(Tempest.Calc.Mechanical.LiftingLugCalculator.Id,
        [
            new DiagramVariant("A lug and its pin, front view and section through the pin: the lug between two cheek plates, the load through the pin", Always,
            [
                new DiagramPlate("lug", 40, 44, 90, 120, "W"),
                new DiagramCircle("hole", new(85, 86), 20, Filled: false, "d_h"),
                new DiagramCircle("pin", new(85, 86), 16, Filled: true, "d_p", Solid: true),
                new DiagramPointLoad("load", new(85, 12), DiagramDirection.Up, 30, "F_lug", "F_lug", new(130, 22)),
                new DiagramDimension("edge-distance", new(85, 44), new(85, 66), 60, "a", "a"),
                new DiagramDimension("hole-diameter", new(65, 86), new(105, 86), 30, "d_h", "d_h"),
                new DiagramDimension("width", new(40, 164), new(130, 164), 12, "W", "W"),
                new DiagramPlate("cheek-left", 240, 30, 10, 80, "t_s"),
                new DiagramPlate("cheek-right", 296, 30, 10, 80, "t_s"),
                new DiagramPlate("lug-section", 260, 64, 26, 100, "t_lug"),
                new DiagramMember("pin-section", new(230, 86), new(316, 86), 16, "d_p"),
                new DiagramDimension("thickness", new(260, 164), new(286, 164), 12, "t_lug", "t_lug"),
                new DiagramDimension("cheek-thickness", new(296, 30), new(306, 30), -10, "t_s", "t_s", new(330, 8)),
                new DiagramDimension("clearance", new(250, 110), new(260, 110), 14, "g", "g", new(204, 124)),
                new DiagramDimension("pin-diameter", new(316, 78), new(316, 94), 14, "d_p", "d_p", new(346, 124)),
                new DiagramLabel("allowable-tension", new(70, 210), "f_t", "f_t"),
                new DiagramLabel("allowable-bearing", new(200, 210), "f_br", "f_br"),
                new DiagramLabel("allowable-shear", new(330, 210), "f_v", "f_v"),
                new DiagramLabel("pin-allowable-bending", new(120, 228), "f_b_pin", "f_b,pin"),
                new DiagramLabel("pin-allowable-shear", new(280, 228), "f_v_pin", "f_v,pin"),
            ]),
        ]);

    private static CalculationDiagramSpec BearingLife()
    {
        static DiagramVariant Variant(string caption, string type)
        {
            var elements = new List<DiagramElement>
            {
                new DiagramPlate("shaft", 50, 110, 180, 30),
                new DiagramPolyline("centreline", [new(40, 125), new(240, 125)], Dashed: true, Thickness: 1),
                new DiagramPlate("outer-ring-top", 125, 54, 50, 16, "C"),
                new DiagramPlate("inner-ring-top", 125, 94, 50, 16, "C"),
                new DiagramPlate("inner-ring-bottom", 125, 140, 50, 16, "C"),
                new DiagramPlate("outer-ring-bottom", 125, 180, 50, 16, "C", "C", new(150, 214)),
            };

            if (type == BearingLifeBasicCalculator.Roller)
            {
                elements.Add(new DiagramPlate("element-top", 138, 70, 24, 24, "type", "type", new(226, 74)));
                elements.Add(new DiagramPlate("element-bottom", 138, 156, 24, 24, "type"));
            }
            else
            {
                elements.Add(new DiagramCircle("element-top", new(150, 82), 12, Filled: true, "type", "type", new(226, 74), Solid: true));
                elements.Add(new DiagramCircle("element-bottom", new(150, 168), 12, Filled: true, "type", Solid: true));
            }

            elements.AddRange(
            [
                new DiagramPointLoad("radial-load", new(150, 52), DiagramDirection.Down, 30, "F_r", "F_r"),
                new DiagramPointLoad("axial-load", new(50, 125), DiagramDirection.Right, 36, "F_a", "F_a", new(62, 96)),
                new DiagramMoment("speed", new(246, 125), 16, Clockwise: true, "n", "n", new(246, 160)),
                new DiagramLabel("radial-factor", new(336, 60), "X", "X"),
                new DiagramLabel("axial-factor", new(336, 84), "Y", "Y"),
                new DiagramLabel("reliability", new(336, 108), "a_1", "a_1"),
                new DiagramLabel("required-life", new(336, 132), "L_req", "L_req"),
            ]);
            return new DiagramVariant(caption, When("type", type), elements);
        }

        return new(BearingLifeBasicCalculator.Id,
        [
            Variant("A ball bearing on its shaft in section, radial and axial load, running speed n", BearingLifeBasicCalculator.Ball),
            Variant("A roller bearing on its shaft in section, radial and axial load, running speed n", BearingLifeBasicCalculator.Roller),
        ]);
    }

    private static CalculationDiagramSpec FatigueMiner() =>
        new(FatigueMinerCalculator.Id,
        [
            new DiagramVariant("A single-slope S–N line on log axes through its reference point, the endurance limit as a cut-off, and the stress-range blocks as listed (three drawn, representatively)", Always,
            [
                new DiagramAxes("axes", new(60, 200), 380, 22),
                new DiagramLabel("y-title", new(96, 30), Symbol: "log S"),
                new DiagramLabel("x-title", new(362, 214), Symbol: "log N"),
                new DiagramPolyline("sn-line", [new(70, 40), new(320, 165)], InputName: "m"),
                new DiagramPolyline("slope-triangle", [new(230, 120), new(270, 120), new(270, 140)], Thickness: 1.2, InputName: "m", Symbol: "m", LabelAt: new(290, 112)),
                new DiagramPolyline("reference-range", [new(60, 105), new(200, 105)], Dashed: true, Thickness: 1, InputName: "S_ref", Symbol: "S_ref", LabelAt: new(125, 117)),
                new DiagramPolyline("reference-cycles", [new(200, 105), new(200, 200)], Dashed: true, Thickness: 1, InputName: "N_ref", Symbol: "N_ref", LabelAt: new(200, 214)),
                new DiagramCircle("reference-point", new(200, 105), 4, Filled: true, "S_ref", Solid: true),
                new DiagramPolyline("endurance-limit", [new(320, 165), new(370, 165)], Dashed: true, InputName: "S_e", Symbol: "S_e", LabelAt: new(330, 180)),
                new DiagramCircle("block-1", new(95, 148), 4, Filled: true, "blocks", Solid: true),
                new DiagramCircle("block-2", new(125, 161), 4, Filled: true, "blocks", Solid: true),
                new DiagramCircle("block-3", new(155, 174), 4, Filled: true, "blocks", "blocks", new(130, 190), Solid: true),
            ]),
        ]);

    private static CalculationDiagramSpec ShaftStress() =>
        new(ShaftStressCalculator.Id,
        [
            new DiagramVariant("A solid round shaft under bending and torsion at the section", Always,
            [
                new DiagramMember("shaft", new(60, 110), new(340, 110), 24, "d"),
                new DiagramDimension("diameter", new(60, 98), new(60, 122), -24, "d", "d", new(60, 146)),
                new DiagramMoment("bending", new(140, 110), 30, Clockwise: true, "M", "M", new(140, 64)),
                new DiagramMoment("torque", new(270, 110), 30, Clockwise: false, "T", "T", new(270, 64)),
                new DiagramLabel("yield", new(90, 196), "S_y", "S_y"),
                new DiagramLabel("kt", new(205, 196), "K_t", "K_t"),
                new DiagramLabel("kts", new(315, 196), "K_ts", "K_ts"),
                new DiagramLabel("required-factor", new(200, 222), "n_req", "n_req"),
            ]),
        ]);

    private static CalculationDiagramSpec ThermalExpansion()
    {
        static DiagramVariant Variant(string caption, IReadOnlyDictionary<string, string> when, bool elastic)
        {
            var elements = new List<DiagramElement>
            {
                new DiagramSupport("wall", new(40, 110), DiagramSupportKind.Fixed, DiagramDirection.Left),
                new DiagramMember("bar", new(40, 110), new(260, 110), 14, "A", "A", new(150, 86)),
                new DiagramLabel("temperature", new(150, 50), "dT", "ΔT"),
                new DiagramDimension("length", new(40, 110), new(260, 110), 40, "L", "L"),
                new DiagramDimension("gap", new(260, 110), new(290, 110), 40, "g", "g", new(312, 162)),
            };

            if (elastic)
            {
                elements.Add(new DiagramSpring("restraint", new(290, 110), new(350, 110), "k_s", "k_s", new(318, 76)));
                elements.Add(new DiagramSupport("restraint-wall", new(350, 110), DiagramSupportKind.Fixed, DiagramDirection.Right, "k_s"));
            }
            else
            {
                elements.Add(new DiagramSupport("restraint-wall", new(290, 110), DiagramSupportKind.Fixed, DiagramDirection.Right, "k_s", "k_s", new(318, 76)));
                elements.Add(new DiagramLabel("rigid-note", new(332, 110), Symbol: "rigid"));
            }

            elements.AddRange(
            [
                new DiagramLabel("modulus", new(75, 206), "E", "E"),
                new DiagramLabel("expansion", new(200, 206), "alpha", "α"),
                new DiagramLabel("allowable", new(325, 206), "sigma_allow", "σ_allow"),
            ]);
            return new DiagramVariant(caption, when, elements);
        }

        return new(ThermalExpansionCalculator.Id,
        [
            Variant("A bar fixed at one end, a gap, then a rigid restraint (no stiffness given)", When("k_s", string.Empty), elastic: false),
            Variant("A bar fixed at one end, a gap, then an elastic restraint of stiffness k_s", Always, elastic: true),
        ]);
    }

    private static CalculationDiagramSpec ThinVessel() =>
        new(ThinVesselCalculator.Id,
        [
            new DiagramVariant("A thin-walled cylinder in section under internal pressure: inside radius R_in, shell thickness t_p including the corrosion allowance c", Always,
            [
                new DiagramLabel("section-note", new(120, 22), Symbol: "Shell section"),
                new DiagramCircle("shell", new(120, 124), 86, Filled: true, "t_p"),
                new DiagramCircle("bore", new(120, 124), 76, Filled: false, "R_in"),
                new DiagramPointLoad("pressure-up", new(120, 54), DiagramDirection.Up, 30, "p_design", "p_design", new(120, 100)),
                new DiagramPointLoad("pressure-left", new(50, 124), DiagramDirection.Left, 30, "p_design"),
                new DiagramPointLoad("pressure-right", new(190, 124), DiagramDirection.Right, 30, "p_design"),
                new DiagramDimension("radius", new(120, 124), new(66.3, 177.7), 0, "R_in", "R_in", new(146, 162)),
                new DiagramDimension("thickness", new(196, 124), new(206, 124), -24, "t_p", "t_p", new(252, 96)),
                new DiagramLabel("allowable", new(320, 86), "S", "S"),
                new DiagramLabel("joint-efficiency", new(320, 112), "E_j", "E"),
                new DiagramLabel("corrosion", new(320, 138), "c", "c"),
                new DiagramLabel("design-factor", new(320, 164), "n_design", "n_design"),
            ]),
        ]);

    private static CalculationDiagramSpec MaterialMargin() =>
        new(MaterialMarginCalculator.Id,
        [
            new DiagramVariant("A piece of the material under the applied stress, and the stresses as bars (drawn representatively, not compared)", Always,
            [
                new DiagramPlate("specimen", 40, 96, 110, 36),
                new DiagramPointLoad("stress-left", new(8, 114), DiagramDirection.Left, 28, "sigma"),
                new DiagramPointLoad("stress-right", new(182, 114), DiagramDirection.Right, 28, "sigma", "σ", new(95, 152)),
                new DiagramAxes("axes", new(200, 190), 390, 30),
                new DiagramLabel("x-title", new(386, 204), Symbol: "σ"),
                new DiagramPlate("ultimate-bar", 200, 44, 170, 22, "f_u", "f_u", new(285, 34)),
                new DiagramPlate("yield-bar", 200, 92, 140, 22, "f_y", "f_y", new(270, 82)),
                new DiagramPlate("applied-bar", 200, 140, 80, 22, "sigma", "σ", new(240, 130)),
                new DiagramLabel("required", new(95, 196), "n_req", "n_req"),
                new DiagramLabel("ultimate-ratio", new(95, 220), "k_u", "k_u"),
            ]),
        ]);

    // ---- Structural ----

    private static CalculationDiagramSpec SectionProperties()
    {
        static DiagramVariant Shape(string caption, string shape, IReadOnlyList<DiagramElement> body) =>
            new(caption, When("shape", shape), [.. body, new DiagramLabel("shape", new(320, 222), "shape", "shape")]);

        return new(SectionPropertiesCalculator.Id,
        [
            Shape("A solid rectangle b by h, axes through the centroid", SectionPropertiesCalculator.ShapeRectangle,
            [
                new DiagramPlate("section", 140, 50, 100, 140, "h"),
                new DiagramDimension("width", new(140, 190), new(240, 190), 16, "b", "b"),
                new DiagramDimension("depth", new(240, 50), new(240, 190), 16, "h", "h", new(276, 120)),
            ]),
            Shape("A solid circle of diameter d", SectionPropertiesCalculator.ShapeCircle,
            [
                new DiagramCircle("section", new(190, 120), 70, Filled: true, "d"),
                new DiagramDimension("diameter", new(120, 120), new(260, 120), 84, "d", "d"),
            ]),
            Shape("A circular tube, outside diameter d, wall t", SectionPropertiesCalculator.ShapeTube,
            [
                new DiagramCircle("outer", new(190, 120), 70, Filled: true, "d"),
                new DiagramCircle("inner", new(190, 120), 56, Filled: false, "t_w"),
                new DiagramDimension("diameter", new(120, 120), new(260, 120), 84, "d", "d"),
                new DiagramDimension("wall", new(246, 120), new(260, 120), -16, "t_w", "t_w", new(286, 96)),
            ]),
            Shape("A hollow rectangle b by h, wall t, corner radii r and r_i", SectionPropertiesCalculator.ShapeHollowRectangle,
            [
                new DiagramPlate("outer", 130, 46, 120, 150, "h"),
                new DiagramPlate("inner", 144, 60, 92, 122, "t_w"),
                new DiagramDimension("width", new(130, 196), new(250, 196), 14, "b", "b"),
                new DiagramDimension("depth", new(250, 46), new(250, 196), 16, "h", "h", new(286, 120)),
                new DiagramDimension("wall", new(130, 120), new(144, 120), -20, "t_w", "t_w", new(80, 100)),
                new DiagramLabel("radii", new(70, 40), "r", "r"),
            ]),
            Shape("A doubly symmetric I-section b by h, web t, flanges t_f, root radius r", SectionPropertiesCalculator.ShapeI,
            [
                new DiagramPlate("top-flange", 120, 40, 140, 16, "t_f"),
                new DiagramPlate("web", 182, 56, 16, 128, "t_w"),
                new DiagramPlate("bottom-flange", 120, 184, 140, 16, "t_f"),
                new DiagramDimension("width", new(120, 200), new(260, 200), 14, "b", "b"),
                new DiagramDimension("depth", new(260, 40), new(260, 200), 18, "h", "h", new(298, 120)),
                new DiagramDimension("web-t", new(182, 120), new(198, 120), 0, "t_w", "t_w", new(244, 156)),
                new DiagramDimension("flange-t", new(120, 40), new(120, 56), -14, "t_f", "t_f", new(62, 48)),
                new DiagramLabel("root", new(70, 120), "r", "r"),
            ]),
            Shape("A channel b by h, web t, flanges t_f, root radius r", SectionPropertiesCalculator.ShapeChannel,
            [
                new DiagramPlate("top-flange", 150, 40, 90, 16, "t_f"),
                new DiagramPlate("web", 150, 56, 16, 128, "t_w"),
                new DiagramPlate("bottom-flange", 150, 184, 90, 16, "t_f"),
                new DiagramDimension("width", new(150, 200), new(240, 200), 14, "b", "b"),
                new DiagramDimension("depth", new(240, 40), new(240, 200), 18, "h", "h", new(278, 120)),
                new DiagramLabel("root", new(90, 120), "r", "r"),
            ]),
            Shape("A tee b by h, stem t, flange t_f, root radius r", SectionPropertiesCalculator.ShapeTee,
            [
                new DiagramPlate("flange", 120, 40, 140, 16, "t_f"),
                new DiagramPlate("stem", 182, 56, 16, 144, "t_w"),
                new DiagramDimension("width", new(120, 40), new(260, 40), -14, "b", "b", new(190, 18)),
                new DiagramDimension("depth", new(260, 40), new(260, 200), 18, "h", "h", new(298, 120)),
                new DiagramLabel("root", new(90, 120), "r", "r"),
            ]),
            Shape("An angle, legs h and b, thickness t, root radius r", SectionPropertiesCalculator.ShapeAngle,
            [
                new DiagramPlate("vertical-leg", 150, 40, 16, 160, "h"),
                new DiagramPlate("horizontal-leg", 150, 184, 110, 16, "b"),
                new DiagramDimension("vertical", new(150, 40), new(150, 200), -18, "h", "h", new(90, 80)),
                new DiagramDimension("horizontal", new(150, 200), new(260, 200), 14, "b", "b"),
                new DiagramDimension("thickness", new(150, 120), new(166, 120), 0, "t_w", "t_w", new(218, 120)),
                new DiagramLabel("root", new(320, 80), "r", "r"),
            ]),
            Shape("A polygon through the vertices as listed (drawn representatively)", SectionPropertiesCalculator.ShapePolygon,
            [
                new DiagramPolyline("outline", [new(120, 190), new(260, 190), new(260, 120), new(200, 50), new(120, 80), new(120, 190)], InputName: "vertices", Symbol: "vertices", LabelAt: new(190, 214)),
                new DiagramCircle("vertex-1", new(120, 190), 4, Filled: true, "vertices", Solid: true),
                new DiagramCircle("vertex-2", new(260, 190), 4, Filled: true, "vertices", Solid: true),
                new DiagramCircle("vertex-3", new(260, 120), 4, Filled: true, "vertices", Solid: true),
                new DiagramCircle("vertex-4", new(200, 50), 4, Filled: true, "vertices", Solid: true),
                new DiagramCircle("vertex-5", new(120, 80), 4, Filled: true, "vertices", Solid: true),
            ]),
            Shape("A composite of the parts as listed, each added or subtracted (drawn representatively)", SectionPropertiesCalculator.ShapeComposite,
            [
                new DiagramPlate("part-1", 120, 50, 140, 20, "parts"),
                new DiagramPlate("part-2", 180, 70, 20, 110, "parts"),
                new DiagramPlate("part-3", 140, 180, 100, 20, "parts"),
                new DiagramLabel("parts", new(190, 22), "parts", "parts"),
            ]),
        ]);
    }

    private static CalculationDiagramSpec Frame2D() =>
        new(FrameCalculator.Id,
        [
            new DiagramVariant("A plane frame (a portal drawn representatively): the nodes, elements, supports and loads as listed", Always,
            [
                new DiagramMember("column-left", new(90, 196), new(90, 70), 8, "elements"),
                new DiagramMember("beam", new(90, 70), new(310, 70), 8, "elements", "elements", new(200, 96)),
                new DiagramMember("column-right", new(310, 70), new(310, 196), 8, "elements"),
                new DiagramCircle("node-1", new(90, 196), 4, Filled: true, "nodes", Solid: true),
                new DiagramCircle("node-2", new(90, 70), 4, Filled: true, "nodes", "nodes", new(160, 220), Solid: true),
                new DiagramCircle("node-3", new(310, 70), 4, Filled: true, "nodes", Solid: true),
                new DiagramCircle("node-4", new(310, 196), 4, Filled: true, "nodes", Solid: true),
                new DiagramSupport("support-left", new(90, 202), DiagramSupportKind.Pinned, DiagramDirection.Down, "nodes"),
                new DiagramSupport("support-right", new(310, 202), DiagramSupportKind.Fixed, DiagramDirection.Down, "nodes"),
                new DiagramDistributedLoad("udl", 90, 310, 64, 24, "loads", "loads", new(200, 26)),
                new DiagramPointLoad("lateral", new(84, 70), DiagramDirection.Right, 50, "loads"),
                new DiagramLabel("modulus", new(200, 140), "E", "E"),
                new DiagramLabel("combinations", new(200, 164), "combinations", "combinations"),
            ]),
        ]);

    private static CalculationDiagramSpec BeamFormulas()
    {
        const double Left = 50, Right = 330, Y = 100;

        static DiagramVariant Variant(string caseName, string caption, string leftSupport, string? rightSupport, string load)
        {
            var elements = new List<DiagramElement>();
            elements.Add(leftSupport == "fixed"
                ? new DiagramSupport("support-left", new(Left, Y), DiagramSupportKind.Fixed, DiagramDirection.Left, "case")
                : new DiagramSupport("support-left", new(Left, Y + 6), DiagramSupportKind.Pinned, DiagramDirection.Down, "case"));

            if (rightSupport == "fixed")
                elements.Add(new DiagramSupport("support-right", new(Right, Y), DiagramSupportKind.Fixed, DiagramDirection.Right, "case"));
            else if (rightSupport == "roller")
                elements.Add(new DiagramSupport("support-right", new(Right, Y + 6), DiagramSupportKind.Roller, DiagramDirection.Down, "case"));

            elements.Add(new DiagramMember("beam", new(Left, Y), new(Right, Y), 10, "I"));

            switch (load)
            {
                case "udl":
                    elements.Add(new DiagramDistributedLoad("load", Left, Right, Y - 6, 30, "w", "w"));
                    break;
                case "point-at-a":
                    elements.Add(new DiagramPointLoad("load", new(230, Y - 6), DiagramDirection.Down, 44, "F", "F"));
                    elements.Add(new DiagramDimension("position", new(Left, Y), new(230, Y), -64, "a", "a", new(140, 26)));
                    break;
                case "point-centre":
                    elements.Add(new DiagramPointLoad("load", new((Left + Right) / 2, Y - 6), DiagramDirection.Down, 44, "F", "F"));
                    break;
                default:
                    elements.Add(new DiagramMoment("couple", new(Left + 22, Y), 18, Clockwise: true, "M_0", "M_0", new(Left + 22, Y - 38)));
                    break;
            }

            elements.AddRange(
            [
                new DiagramDimension("span", new(Left, Y), new(Right, Y), 50, "L", "L"),
                new DiagramLabel("modulus", new(70, 196), "E", "E"),
                new DiagramLabel("second-moment", new(165, 196), "I", "I"),
                new DiagramLabel("fibre", new(275, 196), "y_c", "y_c"),
                new DiagramLabel("allowable", new(120, 222), "sigma_allow", "σ_allow"),
                new DiagramLabel("deflection-limit", new(290, 222), "delta_limit", "δ_limit"),
            ]);
            return new DiagramVariant(caption, When("case", caseName), elements);
        }

        return new(BeamFormulasCalculator.Id,
        [
            Variant(BeamFormulasCalculator.SsUdl, "Simply supported, uniformly distributed load w", "pin", "roller", "udl"),
            Variant(BeamFormulasCalculator.SsPoint, "Simply supported, point load F at a from the left end", "pin", "roller", "point-at-a"),
            Variant(BeamFormulasCalculator.SsMomentEnd, "Simply supported, couple M_0 at the left end", "pin", "roller", "couple"),
            Variant(BeamFormulasCalculator.CantUdl, "Cantilever fixed at the left, uniformly distributed load w", "fixed", null, "udl"),
            Variant(BeamFormulasCalculator.CantPoint, "Cantilever fixed at the left, point load F at a from the fixed end", "fixed", null, "point-at-a"),
            Variant(BeamFormulasCalculator.FixedUdl, "Fixed at both ends, uniformly distributed load w", "fixed", "fixed", "udl"),
            Variant(BeamFormulasCalculator.FixedPointCentre, "Fixed at both ends, point load F at midspan", "fixed", "fixed", "point-centre"),
            Variant(BeamFormulasCalculator.ProppedUdl, "Propped cantilever, fixed left and pinned right, uniformly distributed load w", "fixed", "roller", "udl"),
        ]);
    }

    private static CalculationDiagramSpec MemberChecks() =>
        new(MemberChecksCalculator.Id,
        [
            new DiagramVariant("A steel member under axial force, bending and shear, its buckling lengths about both axes, and its section", Always,
            [
                new DiagramSupport("end-left", new(40, 106), DiagramSupportKind.Pinned, DiagramDirection.Down),
                new DiagramSupport("end-right", new(260, 106), DiagramSupportKind.Roller, DiagramDirection.Down),
                new DiagramMember("member", new(40, 100), new(260, 100), 12, "A"),
                new DiagramPointLoad("axial-left", new(34, 100), DiagramDirection.Right, 30, "N_Ed", "N_Ed", new(55, 76)),
                new DiagramMoment("moment", new(150, 100), 22, Clockwise: true, "M_y_Ed", "M_y,Ed", new(150, 50)),
                new DiagramPointLoad("shear", new(220, 94), DiagramDirection.Down, 30, "V_Ed", "V_Ed", new(262, 36)),
                new DiagramDimension("length-y", new(40, 100), new(260, 100), 44, "L_cr_y", "L_cr,y"),
                new DiagramDimension("length-z", new(40, 100), new(260, 100), 70, "L_cr_z", "L_cr,z"),
                new DiagramLabel("section-note", new(335, 34), Symbol: "Section"),
                new DiagramPlate("top-flange", 300, 50, 70, 10, "W_pl_y"),
                new DiagramPlate("web", 330, 60, 10, 80, "A_v"),
                new DiagramPlate("bottom-flange", 300, 140, 70, 10, "W_pl_y"),
                new DiagramLabel("class", new(335, 168), "section_class", "class"),
                new DiagramLabel("yield", new(335, 192), "f_y", "f_y"),
                new DiagramLabel("minor-moment", new(335, 216), "M_z_Ed", "M_z,Ed"),
            ]),
        ]);

    private static CalculationDiagramSpec ColumnBuckling() =>
        new(ColumnBucklingCalculator.Id,
        [
            new DiagramVariant("A pin-ended strut under axial compression, effective length L_E", Always,
            [
                new DiagramMember("column", new(200, 62), new(200, 196), 10, "A"),
                new DiagramSupport("base", new(200, 202), DiagramSupportKind.Pinned, DiagramDirection.Down),
                new DiagramSupport("head", new(206, 62), DiagramSupportKind.Roller, DiagramDirection.Right),
                new DiagramPointLoad("load", new(200, 56), DiagramDirection.Down, 34, "F", "F"),
                new DiagramDimension("effective-length", new(200, 62), new(200, 196), -60, "L_E", "L_E", new(90, 129)),
                new DiagramLabel("area", new(320, 80), "A", "A"),
                new DiagramLabel("second-moment", new(320, 104), "I", "I"),
                new DiagramLabel("modulus", new(320, 128), "E", "E"),
                new DiagramLabel("yield", new(320, 152), "p_y", "p_y"),
                new DiagramLabel("curve", new(320, 176), "curve", "curve"),
            ]),
        ]);

    private static CalculationDiagramSpec FilletWeld() =>
        new(FilletWeldCalculator.Id,
        [
            new DiagramVariant("A fillet weld of effective length L: in plan, the force along it; in section, the throat a and the forces across it and normal to the plate", Always,
            [
                new DiagramLabel("plan-note", new(110, 22), Symbol: "Plan"),
                new DiagramPlate("plan-plate", 30, 40, 160, 80),
                new DiagramPlate("plan-stem", 30, 72, 160, 10),
                new DiagramMember("weld", new(40, 88), new(180, 88), 8, "L"),
                new DiagramDimension("length", new(40, 92), new(180, 92), 42, "L", "L"),
                new DiagramPointLoad("parallel", new(180, 168), DiagramDirection.Right, 80, "F_par", "F_∥", new(110, 184)),
                new DiagramLabel("section-note", new(345, 40), Symbol: "Section"),
                new DiagramPlate("base", 222, 150, 166, 16),
                new DiagramPlate("stem", 270, 50, 16, 100),
                new DiagramMember("weld-leg-up", new(286, 110), new(286, 150), 2, "a"),
                new DiagramMember("weld-leg-along", new(286, 150), new(326, 150), 2, "a"),
                new DiagramMember("weld-face", new(286, 110), new(326, 150), 2, "a"),
                new DiagramDimension("throat", new(286, 150), new(306, 130), 0, "a", "a", new(352, 134)),
                new DiagramPointLoad("normal", new(278, 16), DiagramDirection.Up, 34, "F_n", "F_n", new(222, 28)),
                new DiagramPointLoad("transverse", new(336, 76), DiagramDirection.Right, 50, "F_tr", "F_⊥", new(340, 92)),
                new DiagramLabel("ultimate", new(80, 214), "f_u", "f_u"),
                new DiagramLabel("correlation", new(200, 214), "beta_w", "β_w"),
                new DiagramLabel("partial", new(320, 214), "gamma_M2", "γ_M2"),
            ]),
        ]);

    // ---- Building services ----

    private static CalculationDiagramSpec PipePump() =>
        new(PipeFlowCalculator.Id,
        [
            new DiagramVariant("A pump lifting from a suction level through the pipe segments as listed (three drawn) to an outlet that needs a pressure head", Always,
            [
                new DiagramPlate("tank", 20, 140, 70, 70),
                new DiagramPolyline("level", [new(20, 152), new(90, 152)], Dashed: true, Thickness: 1, InputName: "z_s"),
                new DiagramDimension("suction-head", new(100, 152), new(100, 110), 10, "z_s", "z_s", new(56, 226)),
                new DiagramMember("suction-pipe", new(60, 152), new(60, 110), 6, "segments"),
                new DiagramMember("suction-run", new(60, 110), new(130, 110), 6, "segments"),
                new DiagramCircle("pump", new(150, 110), 20, Filled: true, "Q_pump", "pump curve", new(150, 70)),
                new DiagramMember("discharge-run", new(170, 110), new(300, 110), 6, "segments", "segments", new(236, 94)),
                new DiagramMember("riser", new(300, 110), new(300, 40), 6, "segments"),
                new DiagramMember("outlet", new(300, 40), new(370, 40), 6, "segments"),
                new DiagramPointLoad("flow", new(270, 128), DiagramDirection.Right, 44, "Q", "Q", new(236, 140)),
                new DiagramLabel("outlet-head", new(335, 22), "h_out", "h_out"),
                new DiagramLabel("fluid", new(236, 176), "fluid", "fluid"),
                new DiagramLabel("temperature", new(196, 200), "T_fluid", "T_fluid"),
                new DiagramLabel("npsh", new(316, 200), "NPSH_r", "NPSH_r"),
            ]),
        ]);

    private static CalculationDiagramSpec Psychrometrics()
    {
        static DiagramVariant Variant(string caption, string process, IReadOnlyList<DiagramElement> extra)
        {
            List<DiagramElement> elements =
            [
                new DiagramAxes("axes", new(40, 210), 380, 20),
                new DiagramLabel("x-title", new(360, 226), Symbol: "dry bulb"),
                new DiagramLabel("y-title", new(384, 36), Symbol: "W"),
                new DiagramPolyline("saturation", [new(40, 200), new(120, 180), new(190, 140), new(240, 90), new(270, 40)], Thickness: 1.5, Symbol: "100 %", LabelAt: new(236, 50)),
                new DiagramCircle("state-1", new(170, 170), 5, Filled: true, "T_db", "1", new(160, 186), Solid: true),
                new DiagramLabel("dry-bulb", new(135, 30), "T_db", "T_db"),
                new DiagramLabel("known", new(135, 54), "known", "known"),
                new DiagramLabel("pressure", new(135, 78), "altitude", "altitude"),
            ];
            elements.AddRange(extra);
            return new DiagramVariant(caption, When("process", process), elements);
        }

        DiagramElement Leaving(double x, double y) => new DiagramCircle("state-2", new(x, y), 5, Filled: true, "T_2", "2", new(x + 12, y - 12), Solid: true);

        return new(PsychrometricsCalculator.Id,
        [
            Variant("A moist-air state on the psychrometric chart: dry bulb and one more property", PsychrometricsCalculator.ProcessNone, []),
            Variant("Sensible heating or cooling: the state moves along a line of constant humidity ratio to T_2", PsychrometricsCalculator.ProcessSensible,
                [new DiagramPolyline("process", [new(170, 170), new(310, 170)], InputName: "T_2"), Leaving(310, 170)]),
            Variant("Cooling with dehumidification: the state cools to T_2 at RH_2, losing moisture", PsychrometricsCalculator.ProcessCoolingDehum,
                [new DiagramPolyline("process", [new(170, 170), new(126, 182)], InputName: "T_2"), new DiagramCircle("state-2", new(126, 182), 5, Filled: true, "T_2", "2", new(100, 140), Solid: true), new DiagramLabel("leaving-rh", new(330, 110), "RH_2", "RH_2")]),
            Variant("Steam humidification: moisture added at almost constant dry bulb to W_2", PsychrometricsCalculator.ProcessSteam,
                [new DiagramPolyline("process", [new(170, 170), new(176, 120)], InputName: "W_2"), new DiagramCircle("state-2", new(176, 120), 5, Filled: true, "W_2", "2", new(130, 116), Solid: true)]),
            Variant("Adiabatic (evaporative) humidification: along a line of constant wet bulb towards saturation", PsychrometricsCalculator.ProcessEvaporative,
                [new DiagramPolyline("process", [new(170, 170), new(200, 146)], InputName: "RH_2"), new DiagramCircle("state-2", new(200, 146), 5, Filled: true, "RH_2", "2", new(250, 140), Solid: true), new DiagramLabel("efficiency", new(330, 110), "eff", "ε")]),
            Variant("Adiabatic mixing of two streams: the mixed state lies on the line between them, by mass flow", PsychrometricsCalculator.ProcessMixing,
                [
                    new DiagramCircle("state-b", new(300, 186), 5, Filled: true, "T_db_b", "b", new(312, 174), Solid: true),
                    new DiagramPolyline("mixing-line", [new(170, 170), new(300, 186)], Dashed: true, Thickness: 1),
                    new DiagramCircle("mixed", new(222, 176), 4, Filled: true, "m_da_b"),
                    new DiagramLabel("flow-a", new(330, 110), "m_da", "ṁ_a"),
                    new DiagramLabel("flow-b", new(330, 134), "m_da_b", "ṁ_b"),
                ]),
        ]);
    }

    private static CalculationDiagramSpec DuctSizing() =>
        new(DuctSizingCalculator.Id,
        [
            new DiagramVariant("A duct run from the fan through its sections as listed (a main and two branches drawn), each sized by the chosen method", Always,
            [
                new DiagramCircle("fan", new(40, 110), 18, Filled: true, Symbol: "fan", LabelAt: new(40, 146)),
                new DiagramMember("main", new(58, 110), new(240, 110), 22, "sections", "sections", new(150, 84)),
                new DiagramMember("branch-up", new(240, 110), new(240, 40), 14, "sections"),
                new DiagramMember("branch-on", new(240, 110), new(370, 110), 14, "sections"),
                new DiagramPointLoad("air", new(140, 136), DiagramDirection.Right, 40, Symbol: "air", LabelAt: new(120, 152)),
                new DiagramLabel("method", new(90, 190), "method", "method"),
                new DiagramLabel("friction", new(90, 214), "dp_target", "Δp/L"),
                new DiagramLabel("velocity", new(250, 190), "v_target", "v"),
                new DiagramLabel("limit", new(250, 214), "v_limit", "v_limit"),
                new DiagramLabel("temperature", new(330, 60), "T_air", "T_air"),
            ]),
        ]);

    private static CalculationDiagramSpec PlaneWall()
    {
        static DiagramVariant Variant(string caption, IReadOnlyDictionary<string, string> when, bool film1, bool film2)
        {
            var elements = new List<DiagramElement>
            {
                new DiagramLabel("layers-note", new(200, 22), Symbol: "t, k per layer"),
                new DiagramDimension("thickness", new(150, 50), new(250, 50), -14, "layers"),
                new DiagramPlate("layer-1", 150, 50, 34, 130, "layers", "layers", new(200, 198)),
                new DiagramPlate("layer-2", 184, 50, 33, 130, "layers"),
                new DiagramPlate("layer-3", 217, 50, 33, 130, "layers"),
                new DiagramLabel("side-1-temperature", new(75, 70), "T_1", "T_1"),
                new DiagramLabel("side-1-film", new(75, 104), "h_1", "h_1"),
                new DiagramLabel("side-2-temperature", new(325, 70), "T_2", "T_2"),
                new DiagramLabel("side-2-film", new(325, 104), "h_2", "h_2"),
                new DiagramLabel("area", new(200, 222), "A", "A"),
            };

            elements.Add(film1
                ? new DiagramPointLoad("side-1-convection", new(146, 140), DiagramDirection.Right, 40, "h_1")
                : new DiagramPolyline("side-1-face", [new(100, 86), new(146, 86)], Dashed: true, Thickness: 1, InputName: "T_1"));
            elements.Add(film2
                ? new DiagramPointLoad("side-2-convection", new(294, 140), DiagramDirection.Right, 40, "h_2")
                : new DiagramPolyline("side-2-face", [new(254, 86), new(300, 86)], Dashed: true, Thickness: 1, InputName: "T_2"));
            return new DiagramVariant(caption, when, elements);
        }

        const string Layers = "the layers as listed (three drawn)";
        return new(PlaneWallCalculator.Id,
        [
            Variant($"A composite plane wall, side 1 left: {Layers}; no films, T_1 and T_2 at the wall faces", new Dictionary<string, string> { ["h_1"] = string.Empty, ["h_2"] = string.Empty }, film1: false, film2: false),
            Variant($"A composite plane wall, side 1 left: T_1 at the face, {Layers}, a film on side 2", When("h_1", string.Empty), film1: false, film2: true),
            Variant($"A composite plane wall, side 1 left: a fluid film, {Layers}, T_2 at the face", When("h_2", string.Empty), film1: true, film2: false),
            Variant($"A composite plane wall, side 1 left: a fluid film, {Layers}, a film on side 2", Always, film1: true, film2: true),
        ]);
    }

    // ---- Electrical ----

    private static CalculationDiagramSpec MotorSizing()
    {
        static DiagramVariant Variant(string caption, string mechanism, IReadOnlyList<DiagramElement> drive)
        {
            List<DiagramElement> elements =
            [
                new DiagramPlate("motor", 20, 70, 60, 50, "T_rated", "T_rated", new(55, 54)),
                new DiagramMember("motor-shaft", new(80, 95), new(110, 95), 6, "J_motor"),
                new DiagramLabel("gear", new(90, 140), "i", "i"),
                new DiagramLabel("move", new(90, 196), "s", "s"),
                new DiagramLabel("time", new(200, 196), "t_move", "t_move"),
                new DiagramLabel("dwell", new(310, 196), "t_dwell", "t_dwell"),
                new DiagramLabel("profile", new(200, 222), "profile", "profile"),
            ];
            elements.AddRange(drive);
            return new DiagramVariant(caption, When("mechanism", mechanism), elements);
        }

        return new(MotorSizingCalculator.Id,
        [
            Variant("A motor driving a lead or ball screw; the carriage of mass m moves along the guide", MotorSizingCalculator.MechScrew,
            [
                new DiagramMember("screw", new(110, 95), new(370, 95), 8, "p_screw", "p", new(330, 78)),
                new DiagramPlate("carriage", 200, 70, 70, 50, "m", "m", new(235, 56)),
                new DiagramPointLoad("process-force", new(320, 140), DiagramDirection.Left, 40, "F_p", "F_p", new(330, 156)),
            ]),
            Variant("A motor driving a belt over two pulleys; the carriage of mass m rides the belt", MotorSizingCalculator.MechBelt,
            [
                new DiagramCircle("pulley-drive", new(130, 95), 20, Filled: false, "D_pulley", "D", new(170, 150)),
                new DiagramCircle("pulley-idler", new(350, 95), 20, Filled: false),
                new DiagramMember("belt-top", new(130, 75), new(350, 75), 2),
                new DiagramMember("belt-bottom", new(130, 115), new(350, 115), 2),
                new DiagramPlate("carriage", 220, 50, 60, 25, "m", "m", new(250, 38)),
                new DiagramPointLoad("process-force", new(320, 150), DiagramDirection.Left, 40, "F_p", "F_p", new(330, 166)),
            ]),
            Variant("A motor driving a pinion on a rack; the carriage of mass m carries the rack", MotorSizingCalculator.MechRack,
            [
                new DiagramCircle("pinion", new(150, 95), 20, Filled: false, "D_pulley", "D", new(190, 60)),
                new DiagramPlate("rack", 110, 115, 260, 12, "m", "m", new(300, 145)),
                new DiagramPointLoad("process-force", new(360, 160), DiagramDirection.Left, 40, "F_p", "F_p", new(330, 176)),
            ]),
            Variant("A motor through a gearbox to a rotary load of inertia J_L and torque T_L", MotorSizingCalculator.MechGearedRotary,
            [
                new DiagramPlate("gearbox", 110, 75, 50, 40, "i"),
                new DiagramMember("output-shaft", new(160, 95), new(240, 95), 6),
                new DiagramCircle("load", new(280, 95), 40, Filled: true, "J_L", "J_L", new(280, 46)),
                new DiagramMoment("load-torque", new(280, 95), 18, Clockwise: false, "T_L", "T_L", new(340, 140)),
            ]),
            Variant("A motor driving a rotary load of inertia J_L and torque T_L directly", MotorSizingCalculator.MechDirectRotary,
            [
                new DiagramMember("output-shaft", new(110, 95), new(240, 95), 6),
                new DiagramCircle("load", new(280, 95), 40, Filled: true, "J_L", "J_L", new(280, 46)),
                new DiagramMoment("load-torque", new(280, 95), 18, Clockwise: false, "T_L", "T_L", new(340, 140)),
            ]),
        ]);
    }

    private static CalculationDiagramSpec EnclosureHeat() =>
        new(EnclosureHeatCalculator.Id,
        [
            new DiagramVariant("An enclosure W by H by D holding the heat loads as listed, losing heat through its exposed faces to the ambient, with a fan and the sun on it", Always,
            [
                new DiagramPlate("enclosure", 110, 50, 150, 150, "H"),
                new DiagramPlate("load-1", 130, 80, 40, 30, "loads"),
                new DiagramPlate("load-2", 190, 80, 50, 30, "loads", "loads", new(185, 66)),
                new DiagramPlate("load-3", 130, 130, 110, 24, "loads"),
                new DiagramDimension("width", new(110, 200), new(260, 200), 16, "W", "W"),
                new DiagramDimension("height", new(260, 50), new(260, 200), 18, "H", "H", new(310, 160)),
                new DiagramLabel("depth", new(320, 186), "D", "D"),
                new DiagramLabel("inside", new(185, 180), "T_max", "T_max"),
                new DiagramLabel("outside", new(55, 130), "T_amb", "T_amb"),
                new DiagramPointLoad("sun", new(150, 44), DiagramDirection.Down, 30, "I_sun", "I_sun", new(64, 20)),
                new DiagramCircle("fan", new(300, 38), 14, Filled: false, "V_pts", "fan", new(345, 14)),
                new DiagramPointLoad("loss", new(340, 116), DiagramDirection.Right, 40, "h_conv", "h_conv", new(330, 98)),
            ]),
        ]);

    private static CalculationDiagramSpec VoltageDrop()
    {
        static DiagramVariant Variant(string caption, string system, int conductors)
        {
            var elements = new List<DiagramElement>
            {
                new DiagramCircle("source", new(50, 110), 24, Filled: false, "V_n", "V_n", new(50, 70)),
                new DiagramPlate("load", 320, 80, 60, 60, "I", "I", new(350, 64)),
                new DiagramDimension("route", new(74, 110), new(320, 110), 60, "L", "L"),
                new DiagramLabel("area", new(110, 206), "A_c", "A_c"),
                new DiagramLabel("power-factor", new(200, 206), "cos_phi", "cos φ"),
                new DiagramLabel("limit", new(300, 206), "dv_limit", "Δv limit"),
                new DiagramLabel("system", new(200, 230), "system", "system"),
            };

            for (var k = 0; k < conductors; k++)
            {
                var y = 92 + (k * 36.0 / Math.Max(1, conductors - 1));
                elements.Add(new DiagramMember($"conductor-{k + 1}", new(74, y), new(320, y), 3, "A_c"));
            }

            return new DiagramVariant(caption, When("system", system), elements);
        }

        return new(VoltageDropCalculator.Id,
        [
            Variant("A three-phase circuit: the supply, three line conductors of route length L, and the load current I", VoltageDropCalculator.SystemThree, 3),
            Variant("A single-phase circuit: the supply, line and neutral of route length L, and the load current I", VoltageDropCalculator.SystemSingle, 2),
            Variant("A DC circuit: the supply, the two conductors of route length L, and the load current I", VoltageDropCalculator.SystemDc, 2),
        ]);
    }

    private static CalculationDiagramSpec ThermalChain() =>
        new(ThermalChainCalculator.Id,
        [
            new DiagramVariant("Thermal resistances in series from the source to ambient, hottest first (three drawn; the stages as listed)", Always,
            [
                new DiagramPointLoad("power", new(76, 94), DiagramDirection.Down, 40, "P", "P"),
                new DiagramSpring("stage-1", new(76, 100), new(150, 100), "stages"),
                new DiagramSpring("stage-2", new(150, 100), new(224, 100), "stages", "stages", new(187, 70)),
                new DiagramSpring("stage-3", new(224, 100), new(298, 100), "stages"),
                new DiagramCircle("source", new(76, 100), 6, Filled: true, "T_max", "T_max", new(76, 128), Solid: true),
                new DiagramCircle("node-1", new(150, 100), 4, Filled: true, Solid: true),
                new DiagramCircle("node-2", new(224, 100), 4, Filled: true, Solid: true),
                new DiagramCircle("ambient", new(298, 100), 6, Filled: true, "T_a", "T_a", new(298, 128), Solid: true),
                new DiagramLabel("source-note", new(76, 152), Symbol: "source"),
                new DiagramLabel("ambient-note", new(298, 152), Symbol: "ambient"),
            ]),
        ]);
}
