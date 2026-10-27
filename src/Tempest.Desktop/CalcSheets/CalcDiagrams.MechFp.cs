using Tempest.Calc.FluidPower;
using Tempest.Calc.Mechanical;
using Tempest.Core.Calculations.Modules.Diagrams;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// The reference diagrams of the mechanical and fluid-power templates drawn
/// in DI-08 (v0.26.0): bearing life (ISO 281), mechanism, shaft, snap-fit,
/// spring, weld group, accumulator, cylinder and O-ring gland. Same rules as
/// the first 27: inputs only, never a result, not to scale, every shape
/// bound to the worksheet input it stands for.
/// </summary>
public static partial class CalcDiagrams
{
    // ---- Mechanical (DI-08; sheet 400 x 240, y downward, not to scale) ----

    private static CalculationDiagramSpec BearingLifeIso281()
    {
        static DiagramVariant Variant(string caption, string type)
        {
            var elements = new List<DiagramElement>
            {
                new DiagramPlate("shaft", 40, 110, 180, 30),
                new DiagramPlate("outer-ring-top", 125, 54, 50, 16, "C"),
                new DiagramPlate("inner-ring-top", 125, 94, 50, 16, "C"),
                new DiagramPlate("inner-ring-bottom", 125, 140, 50, 16, "C"),
                new DiagramPlate("outer-ring-bottom", 125, 180, 50, 16, "C", "C", new(50, 214)),
            };

            if (type == BearingLifeCalculator.Roller)
            {
                elements.Add(new DiagramPlate("element-top", 138, 70, 24, 24, "type", "type", new(180, 214)));
                elements.Add(new DiagramPlate("element-bottom", 138, 156, 24, 24, "type"));
            }
            else
            {
                elements.Add(new DiagramCircle("element-top", new(150, 82), 12, Filled: true, "type", "type", new(180, 214), Solid: true));
                elements.Add(new DiagramCircle("element-bottom", new(150, 168), 12, Filled: true, "type", Solid: true));
            }

            elements.AddRange(
            [
                new DiagramPointLoad("radial-load", new(150, 52), DiagramDirection.Down, 30, "F_r", "F_r"),
                new DiagramPointLoad("axial-load", new(50, 125), DiagramDirection.Right, 36, "F_a", "F_a", new(62, 96)),
                new DiagramMoment("speed", new(246, 125), 16, Clockwise: true, "n", "n", new(240, 160)),
                new DiagramLabel("load-ratio-limit", new(335, 36), "e_lim", "e_lim"),
                new DiagramLabel("radial-factor", new(335, 60), "X", "X"),
                new DiagramLabel("axial-factor", new(335, 84), "Y", "Y"),
                new DiagramLabel("reliability", new(335, 108), "R_rel", "R_rel"),
                new DiagramLabel("life-factor", new(335, 132), "a_iso", "a_ISO"),
                new DiagramLabel("required-life", new(335, 190), "L_req", "L_req"),
                new DiagramLabel("required-static-safety", new(335, 214), "s_0_req", "s_0_req"),
                new DiagramLabel("static-rating", new(120, 232), "C_0", "C_0"),
                new DiagramLabel("duty-cycle", new(290, 232), "duty", "duty"),
            ]);
            return new DiagramVariant(caption, When("type", type), elements);
        }

        return new(BearingLifeCalculator.Id,
        [
            Variant("A ball bearing on its shaft in section: radial load F_r, axial load F_a, speed n, and the ratings and factors of the records", BearingLifeCalculator.Ball),
            Variant("A roller bearing on its shaft in section: radial load F_r, axial load F_a, speed n, and the ratings and factors of the records", BearingLifeCalculator.Roller),
        ]);
    }

    private static CalculationDiagramSpec Mechanism()
    {
        // The sweep labels every linkage shares, with the choice labels placed per variant.
        static IEnumerable<DiagramElement> Linkage(DiagramPoint circuit, DiagramPoint steps, DiagramPoint transmission) =>
        [
            new DiagramLabel("start-angle", new(120, 30), "theta_start", "θ_start"),
            new DiagramLabel("swept-range", new(240, 30), "theta_range", "θ_range"),
            new DiagramLabel("circuit", circuit, "circuit", "circuit"),
            new DiagramLabel("steps", steps, "steps", "steps"),
            new DiagramLabel("transmission-angle", transmission, "mu_req", "μ_req"),
            new DiagramLabel("load-direction", new(200, 232), "load_dir", "load"),
        ];

        static DiagramVariant Cam(string caption, IReadOnlyDictionary<string, string> when, bool roller)
        {
            var elements = new List<DiagramElement>
            {
                new DiagramCircle("cam", new(130, 160), 46, Filled: true, "Rb", "R_b", new(54, 196)),
                new DiagramPolyline("nose", [new(92, 128), new(110, 90), new(150, 90), new(168, 128)]),
                new DiagramPolyline("follower-axis", [new(130, 160), new(130, 222)], Dashed: true, Thickness: 1),
                new DiagramDimension("offset", new(130, 216), new(160, 216), 0, "e_off", "e", new(176, 228)),
                new DiagramMoment("speed", new(130, 160), 16, Clockwise: true, "n_in", "n_in", new(230, 190)),
            };

            if (roller)
            {
                elements.Add(new DiagramCircle("roller", new(130, 82), 14, Filled: true, "R_r", "R_r", new(190, 66), Solid: true));
                elements.Add(new DiagramMember("stem", new(130, 68), new(130, 30), 8, "m_f", "m_f", new(80, 30)));
                elements.Add(new DiagramLabel("follower", new(320, 76), "follower", "follower"));
                elements.Add(new DiagramLabel("pressure-angle", new(300, 112), "phi_max", "φ_max"));
            }
            else
            {
                elements.Add(new DiagramPlate("face", 100, 88, 60, 8, "follower", "follower", new(312, 76)));
                elements.Add(new DiagramMember("stem", new(130, 88), new(130, 30), 8, "m_f", "m_f", new(80, 30)));
            }

            elements.AddRange(
            [
                new DiagramSpring("return-spring", new(170, 14), new(170, 52), "k_sp", "k_sp", new(230, 30)),
                new DiagramLabel("preload", new(230, 50), "F_pre", "F_pre"),
                new DiagramPointLoad("external-load", new(130, 30), DiagramDirection.Down, 24, "F_ext", "F_ext", new(64, 14)),
                new DiagramLabel("segments", new(300, 140), "segs", "segs"),
                new DiagramLabel("steps", new(320, 164), "steps", "steps"),
            ]);
            return new DiagramVariant(caption, when, elements);
        }

        return new(MechanismCalculator.Id,
        [
            new DiagramVariant("A four-bar linkage: ground d between O₂ and O₄, crank a turning at n_in, coupler b, rocker c carrying the load torque", When("mode", MechanismCalculator.FourBar),
            [
                new DiagramMember("ground", new(70, 170), new(290, 170), 4, "L1", "L1", new(180, 192)),
                new DiagramSupport("pivot-crank", new(70, 176), DiagramSupportKind.Pinned, DiagramDirection.Down),
                new DiagramSupport("pivot-rocker", new(290, 176), DiagramSupportKind.Pinned, DiagramDirection.Down),
                new DiagramMember("crank", new(70, 170), new(100, 118), 6, "L2", "L2", new(50, 100)),
                new DiagramMember("coupler", new(100, 118), new(262, 88), 6, "L3", "L3", new(180, 84)),
                new DiagramMember("rocker", new(262, 88), new(290, 170), 6, "L4", "L4", new(330, 120)),
                new DiagramCircle("pin-o2", new(70, 170), 4, Filled: true, Solid: true),
                new DiagramCircle("pin-a", new(100, 118), 4, Filled: true, Solid: true),
                new DiagramCircle("pin-b", new(262, 88), 4, Filled: true, Solid: true),
                new DiagramCircle("pin-o4", new(290, 170), 4, Filled: true, Solid: true),
                new DiagramMoment("speed", new(70, 170), 16, Clockwise: true, "n_in", "n_in", new(60, 218)),
                new DiagramMoment("load-torque", new(290, 170), 16, Clockwise: false, "T_load", "T_load", new(310, 218)),
                new DiagramLabel("output-inertia", new(315, 48), "J_out", "J_out"),
                .. Linkage(circuit: new(330, 72), steps: new(330, 96), transmission: new(330, 144)),
            ]),
            new DiagramVariant("A slider-crank: crank r turning at n_in, connecting rod l, the slider of mass m on a slide line offset e above the crank centre", When("mode", MechanismCalculator.SliderCrank),
            [
                new DiagramSupport("pivot-crank", new(80, 156), DiagramSupportKind.Pinned, DiagramDirection.Down),
                new DiagramPolyline("crank-centreline", [new(80, 150), new(200, 150)], Dashed: true, Thickness: 1),
                new DiagramPolyline("slide-line", [new(170, 120), new(270, 120)], Dashed: true, Thickness: 1),
                new DiagramMember("crank", new(80, 150), new(112, 112), 6, "L2", "L2", new(46, 110)),
                new DiagramMember("rod", new(112, 112), new(270, 120), 6, "L3", "L3", new(150, 90)),
                new DiagramPlate("slider", 270, 106, 40, 28, "m_slider", "m", new(240, 90)),
                new DiagramCircle("pin-o2", new(80, 150), 4, Filled: true, Solid: true),
                new DiagramCircle("pin-a", new(112, 112), 4, Filled: true, Solid: true),
                new DiagramCircle("pin-b", new(270, 120), 4, Filled: true, Solid: true),
                new DiagramDimension("offset", new(200, 150), new(200, 120), 0, "e_off", "e"),
                new DiagramPointLoad("slider-load", new(310, 120), DiagramDirection.Left, 40, "F_load", "F_load", new(340, 140)),
                new DiagramMoment("speed", new(80, 150), 16, Clockwise: true, "n_in", "n_in", new(60, 218)),
                .. Linkage(circuit: new(330, 52), steps: new(330, 76), transmission: new(330, 100)),
            ]),
            Cam("A cam turning at n_in under a flat-faced follower: base circle R_b, the follower mass, its return spring and the loads on it", new Dictionary<string, string> { ["mode"] = MechanismCalculator.CamMode, ["follower"] = MechanismCalculator.FlatFaced }, roller: false),
            Cam("A cam turning at n_in under a roller follower: base circle R_b, roller R_r offset e, the follower mass, its return spring and the loads on it", new Dictionary<string, string> { ["mode"] = MechanismCalculator.CamMode, ["follower"] = MechanismCalculator.Roller }, roller: true),
        ]);
    }

    private static CalculationDiagramSpec Shaft() =>
        new(ShaftCalculator.Id,
        [
            new DiagramVariant("A stepped shaft on bearing A (pin) and bearing B (roller), positions from the left end: the segments, loads and sections as listed, the torque entering at x_T1, running at n", Always,
            [
                new DiagramPlate("segment-1", 50, 100, 90, 20, "segments"),
                new DiagramPlate("segment-2", 140, 94, 110, 32, "segments"),
                new DiagramPlate("segment-3", 250, 100, 80, 20, "segments", "segments", new(330, 46)),
                new DiagramSupport("bearing-a", new(80, 121), DiagramSupportKind.Pinned, DiagramDirection.Down, "x_A", "x_A", new(80, 160)),
                new DiagramSupport("bearing-b", new(300, 121), DiagramSupportKind.Roller, DiagramDirection.Down, "x_B", "x_B", new(310, 160)),
                new DiagramDimension("bearing-b-position", new(50, 126), new(300, 126), 54, "x_B"),
                new DiagramPointLoad("load", new(195, 94), DiagramDirection.Down, 36, "loads", "loads", new(195, 46)),
                new DiagramPolyline("section", [new(230, 84), new(230, 136)], Dashed: true, Thickness: 1, InputName: "sections", Symbol: "sections", LabelAt: new(190, 150)),
                new DiagramMoment("torque", new(110, 110), 22, Clockwise: false, "T", "T", new(108, 30)),
                new DiagramDimension("torque-in", new(50, 100), new(110, 100), -36, "x_T1", "x_T1", new(80, 52)),
                new DiagramMoment("speed", new(350, 110), 16, Clockwise: true, "n", "n", new(345, 76)),
                new DiagramLabel("required-factor", new(340, 196), "n_target", "n_target"),
                new DiagramLabel("ultimate", new(60, 214), "S_ut", "S_ut"),
                new DiagramLabel("yield", new(170, 214), "S_y", "S_y"),
                new DiagramLabel("modulus", new(270, 214), "E", "E"),
                new DiagramLabel("finish", new(200, 232), "finish", "finish"),
            ]),
        ]);

    private static CalculationDiagramSpec SnapFit()
    {
        static IEnumerable<DiagramElement> Material() =>
        [
            new DiagramLabel("secant-modulus", new(60, 196), "Es", "E_s"),
            new DiagramLabel("permissible-strain", new(200, 196), "eps_perm_once", "ε_perm"),
            new DiagramLabel("friction", new(320, 196), "mu", "μ"),
            new DiagramLabel("temperature", new(120, 220), "T_use", "T_use"),
            new DiagramLabel("latches", new(270, 220), "n_latch", "n_latch"),
        ];

        // The hook at the tip of a cantilever: the entry face at α, the retaining face at α′, and the deflection y that clears the undercut.
        static IEnumerable<DiagramElement> Hook() =>
        [
            new DiagramPolyline("hook", [new(196, 100), new(210, 76), new(236, 76), new(252, 100)], InputName: "y"),
            new DiagramDimension("deflection", new(300, 76), new(300, 100), 0, "y", "y"),
            new DiagramLabel("entry-angle", new(262, 62), "alpha", "α"),
            new DiagramLabel("return-angle", new(180, 62), "alpha_r", "α′"),
        ];

        static DiagramVariant Variant(string caption, string mode, IReadOnlyList<DiagramElement> body) =>
            new(caption, When("mode", mode), [.. body, .. Material()]);

        return new(SnapFitCalculator.Id,
        [
            Variant("A cantilever snap-fit of constant section: length l, root thickness h, width b, the hook deflecting y to clear the undercut", SnapFitCalculator.Constant,
            [
                new DiagramSupport("root", new(60, 110), DiagramSupportKind.Fixed, DiagramDirection.Left),
                new DiagramPlate("beam", 60, 100, 160, 20, "h"),
                .. Hook(),
                new DiagramDimension("length", new(60, 120), new(220, 120), 24, "l", "l"),
                new DiagramDimension("thickness", new(260, 100), new(260, 120), 0, "h", "h"),
                new DiagramLabel("width", new(110, 84), "b", "b"),
            ]),
            Variant("A cantilever snap-fit tapering in thickness from h at the root to h·h_ratio at the tip, width b", SnapFitCalculator.TaperedThickness,
            [
                new DiagramSupport("root", new(60, 110), DiagramSupportKind.Fixed, DiagramDirection.Left),
                new DiagramPolyline("beam", [new(60, 100), new(220, 106), new(220, 114), new(60, 120), new(60, 100)], InputName: "h"),
                .. Hook(),
                new DiagramDimension("length", new(60, 120), new(220, 120), 24, "l", "l"),
                new DiagramDimension("thickness", new(260, 100), new(260, 120), 0, "h", "h"),
                new DiagramLabel("width", new(110, 84), "b", "b"),
                new DiagramLabel("thickness-ratio", new(140, 172), "h_ratio", "h_ratio"),
            ]),
            Variant("A cantilever snap-fit in plan, tapering in width from b at the root to b·b_ratio at the tip, thickness h", SnapFitCalculator.TaperedWidth,
            [
                new DiagramSupport("root", new(60, 110), DiagramSupportKind.Fixed, DiagramDirection.Left),
                new DiagramPolyline("beam", [new(60, 96), new(220, 104), new(220, 116), new(60, 124), new(60, 96)], InputName: "b"),
                new DiagramDimension("width", new(100, 98), new(100, 122), 0, "b", "b", new(100, 80)),
                new DiagramDimension("length", new(60, 124), new(220, 124), 20, "l", "l"),
                new DiagramDimension("deflection", new(220, 104), new(220, 76), 0, "y", "y", new(258, 90)),
                new DiagramLabel("thickness", new(300, 124), "h", "h"),
                new DiagramLabel("width-ratio", new(140, 172), "b_ratio", "b_ratio"),
                new DiagramLabel("entry-angle", new(300, 60), "alpha", "α"),
                new DiagramLabel("return-angle", new(180, 60), "alpha_r", "α′"),
            ]),
            Variant("An annular snap-fit: a ring of mean diameter D_ring and wall h widened by ΔD to pass the undercut; beside it, the ring's axial length b", SnapFitCalculator.Annular,
            [
                new DiagramCircle("ring", new(130, 110), 56, Filled: true, "D_ring"),
                new DiagramCircle("bore", new(130, 110), 44, Filled: false, "h"),
                new DiagramDimension("mean-diameter", new(74, 110), new(186, 110), -64, "D_ring", "D_ring", new(130, 34)),
                new DiagramDimension("wall", new(174, 110), new(186, 110), 30, "h", "h", new(230, 152)),
                new DiagramDimension("interference", new(186, 110), new(200, 110), 0, "dD", "ΔD", new(240, 110)),
                new DiagramLabel("strain-factor", new(300, 24), "K_ann", "K_ann"),
                new DiagramPlate("ring-side", 280, 70, 40, 100, "b"),
                new DiagramDimension("axial-length", new(280, 70), new(320, 70), -12, "b", "b", new(300, 44)),
                new DiagramLabel("entry-angle", new(40, 70), "alpha", "α"),
            ]),
            Variant("A torsion-bar snap-fit: the bar of radius r_bar and length l held as chosen, the lever a_arm on it deflecting y at its end", SnapFitCalculator.Torsion,
            [
                new DiagramSupport("root", new(80, 120), DiagramSupportKind.Fixed, DiagramDirection.Left),
                new DiagramMember("bar", new(80, 120), new(240, 120), 10, "r_bar", "r_bar", new(160, 96)),
                new DiagramMember("lever", new(240, 120), new(240, 60), 6, "a_arm", "a_arm", new(296, 100)),
                new DiagramDimension("deflection", new(240, 60), new(280, 60), -16, "y", "y", new(310, 30)),
                new DiagramDimension("length", new(80, 120), new(240, 120), 30, "l", "l"),
                new DiagramLabel("support", new(100, 60), "bar_support", "held"),
                new DiagramLabel("poisson", new(330, 136), "nu", "ν"),
            ]),
        ]);
    }

    private static CalculationDiagramSpec Spring()
    {
        static IEnumerable<DiagramElement> Material(DiagramPoint corr, DiagramPoint g, DiagramPoint rm, DiagramPoint se, DiagramPoint nf, DiagramPoint fop, DiagramPoint work) =>
        [
            new DiagramLabel("correction", corr, "corr", "corr"),
            new DiagramLabel("shear-modulus", g, "G", "G"),
            new DiagramLabel("tensile", rm, "Rm", "R_m"),
            new DiagramLabel("endurance", se, "S_e", "S_e"),
            new DiagramLabel("required-factor", nf, "n_f_req", "n_f_req"),
            new DiagramLabel("frequency", fop, "f_op", "f_op"),
            new DiagramLabel("working-points", work, "work", "work"),
        ];

        return new(SpringCalculator.Id,
        [
            new DiagramVariant("A compression spring of wire d, mean coil diameter D and free length L0 between its seats, loaded at the two working points", When("mode", SpringCalculator.Compression),
            [
                new DiagramPlate("seat-top", 90, 50, 60, 10),
                new DiagramPlate("seat-bottom", 90, 180, 60, 10),
                new DiagramSpring("coil", new(120, 60), new(120, 180), "n_active", "n_active", new(60, 144)),
                new DiagramLabel("wire", new(60, 96), "d", "d"),
                new DiagramDimension("mean-diameter", new(108, 200), new(132, 200), 14, "D", "D"),
                new DiagramDimension("free-length", new(120, 50), new(120, 190), 60, "L0", "L0"),
                new DiagramPointLoad("load-1", new(104, 50), DiagramDirection.Down, 30, "F_1", "F_1", new(56, 30)),
                new DiagramPointLoad("load-2", new(136, 50), DiagramDirection.Down, 30, "F_2", "F_2", new(190, 30)),
                new DiagramLabel("ends", new(260, 214), "ends", "ends"),
                .. Material(new(320, 40), new(320, 64), new(320, 88), new(320, 112), new(320, 136), new(320, 160), new(320, 184)),
            ]),
            new DiagramVariant("An extension spring of wire d, mean coil diameter D and hooks of radii r_h1 and r_h2, under its initial tension and the two working loads", When("mode", SpringCalculator.Extension),
            [
                new DiagramSpring("coil", new(90, 110), new(250, 110), "n_active", "n_active", new(170, 160)),
                new DiagramCircle("hook-left", new(72, 110), 12, Filled: false, "r_h1", "r_h1", new(72, 142)),
                new DiagramCircle("hook-right", new(268, 110), 12, Filled: false, "r_h2", "r_h2", new(262, 142)),
                new DiagramLabel("wire", new(100, 80), "d", "d"),
                new DiagramLabel("mean-diameter", new(180, 80), "D", "D"),
                new DiagramLabel("initial-tension", new(170, 136), "F_0", "F_0"),
                new DiagramPointLoad("load-1", new(16, 110), DiagramDirection.Left, 44, "F_1", "F_1", new(40, 60)),
                new DiagramPointLoad("load-2", new(310, 110), DiagramDirection.Right, 36, "F_2", "F_2", new(250, 60)),
                .. Material(new(320, 36), new(335, 60), new(335, 84), new(335, 164), new(335, 188), new(335, 212), new(335, 232)),
            ]),
            new DiagramVariant("A torsion spring of wire d and mean coil diameter D, its two legs loaded by the torques at the working points", When("mode", SpringCalculator.Torsion),
            [
                new DiagramCircle("coil", new(150, 120), 40, Filled: false, "n_active", "n_active", new(100, 180)),
                new DiagramMember("leg-1", new(150, 80), new(260, 80), 4, "l_leg_1", "l_leg_1", new(205, 64)),
                new DiagramMember("leg-2", new(150, 160), new(260, 160), 4, "l_leg_2", "l_leg_2", new(215, 176)),
                new DiagramLabel("wire", new(60, 96), "d", "d"),
                new DiagramLabel("mean-diameter", new(60, 144), "D", "D"),
                new DiagramMoment("torque-2", new(150, 120), 18, Clockwise: true, "M_2", "M_2", new(150, 30)),
                new DiagramLabel("torque-1", new(60, 48), "M_1", "M_1"),
                new DiagramLabel("modulus", new(260, 214), "E", "E"),
                .. Material(new(320, 40), new(325, 64), new(325, 88), new(325, 112), new(325, 136), new(325, 160), new(325, 184)),
            ]),
        ]);
    }

    private static CalculationDiagramSpec WeldGroup() =>
        new(WeldGroupCalculator.Id,
        [
            new DiagramVariant("A fillet-weld group in plan (two lines drawn; the welds as listed), x and y from one origin, under forces and moments at (e_x, e_y); beside it, the weld section with leg s", Always,
            [
                new DiagramMember("x-axis", new(40, 200), new(300, 200), 1),
                new DiagramLabel("x-note", new(312, 200), Symbol: "x"),
                new DiagramMember("y-axis", new(40, 200), new(40, 40), 1),
                new DiagramLabel("y-note", new(40, 30), Symbol: "y"),
                new DiagramMember("weld-1", new(100, 70), new(100, 160), 6, "welds"),
                new DiagramMember("weld-2", new(170, 70), new(170, 160), 6, "welds", "welds", new(135, 44)),
                new DiagramMoment("torque", new(135, 115), 14, Clockwise: false, "M_z", "M_z", new(135, 182)),
                new DiagramCircle("load-point", new(250, 80), 4, Filled: true, "e_x", Solid: true),
                new DiagramDimension("load-point-y", new(250, 200), new(250, 80), 0, "e_y", "e_y", new(296, 150)),
                new DiagramDimension("load-point-x", new(40, 200), new(250, 200), 16, "e_x", "e_x"),
                new DiagramPointLoad("load-y", new(250, 36), DiagramDirection.Up, 44, "F_y", "F_y", new(250, 24)),
                new DiagramPointLoad("load-x", new(294, 80), DiagramDirection.Right, 44, "F_x", "F_x", new(300, 96)),
                new DiagramLabel("load-z", new(300, 120), "F_z", "F_z"),
                new DiagramLabel("moment-x", new(340, 40), "M_x", "M_x"),
                new DiagramLabel("moment-y", new(340, 64), "M_y", "M_y"),
                new DiagramPlate("section-base", 316, 170, 70, 10),
                new DiagramPlate("section-stem", 346, 130, 10, 40),
                new DiagramPolyline("section-weld", [new(356, 170), new(356, 156), new(370, 170)], InputName: "s"),
                new DiagramDimension("leg", new(356, 170), new(370, 170), 12, "s", "s", new(363, 194)),
                new DiagramLabel("throat-strength", new(300, 228), "f_w", "f_w"),
            ]),
        ]);

    // ---- Fluid power (DI-08) ----

    private static CalculationDiagramSpec Accumulator() =>
        new(AccumulatorCalculator.Id,
        [
            new DiagramVariant("A bladder accumulator in section: gas pre-charged to p_0 above the bladder, the liquid ΔV delivered between p_2 and p_1 through the port", Always,
            [
                new DiagramPlate("shell", 120, 30, 100, 180),
                new DiagramPlate("gas", 128, 38, 84, 70, "p_0", "p_0", new(60, 60)),
                new DiagramPolyline("bladder", [new(128, 108), new(150, 116), new(190, 116), new(212, 108)], Dashed: true, Thickness: 1),
                new DiagramPlate("liquid", 128, 116, 84, 86, "dV"),
                new DiagramDimension("delivered-volume", new(220, 116), new(220, 160), 20, "dV", "ΔV"),
                new DiagramMember("port", new(170, 210), new(170, 232), 6),
                new DiagramLabel("lowest-pressure", new(90, 224), "p_1", "p_1"),
                new DiagramLabel("highest-pressure", new(250, 224), "p_2", "p_2"),
                new DiagramLabel("maximum-temperature", new(60, 84), "T_max", "T_max"),
                new DiagramLabel("minimum-temperature", new(60, 108), "T_min", "T_min"),
                new DiagramLabel("charging-temperature", new(60, 132), "T_c", "T_c"),
                new DiagramLabel("gas-exponent", new(60, 156), "n_gas", "n_gas"),
                new DiagramLabel("duty", new(310, 36), "mode", "mode"),
                new DiagramLabel("process", new(310, 60), "process", "process"),
                new DiagramLabel("pressure-basis", new(310, 84), "p_basis", "p_basis"),
                new DiagramLabel("precharge-fraction", new(310, 108), "r_0", "r_0"),
                new DiagramLabel("pressure-ratio", new(310, 160), "r_p", "r_p"),
                new DiagramLabel("load-pressure", new(310, 184), "p_load", "p_load"),
                new DiagramLabel("sizes", new(310, 208), "sizes", "sizes"),
            ]),
        ]);

    private static CalculationDiagramSpec Cylinder()
    {
        static DiagramVariant Variant(string caption, string mode, IReadOnlyList<DiagramElement> connection) =>
            new(caption, When("mode", mode),
            [
                new DiagramPlate("barrel", 50, 70, 190, 60),
                new DiagramPlate("piston", 110, 72, 14, 56),
                new DiagramMember("rod", new(124, 100), new(320, 100), 14, "d_r"),
                new DiagramDimension("bore", new(50, 70), new(50, 130), -16, "D", "D", new(44, 150)),
                new DiagramDimension("rod-diameter", new(280, 93), new(280, 107), 0, "d_r", "d_r", new(296, 126)),
                new DiagramDimension("stroke", new(124, 130), new(226, 130), 22, "stroke", "stroke"),
                new DiagramPointLoad("push-load", new(320, 100), DiagramDirection.Left, 40, "F_push", "F_push", new(336, 72)),
                new DiagramPointLoad("pull-load", new(372, 134), DiagramDirection.Right, 40, "F_pull", "F_pull", new(336, 152)),
                new DiagramPointLoad("supply", new(90, 70), DiagramDirection.Down, 20, "p_sys", "p_sys", new(90, 36)),
                new DiagramLabel("flow", new(170, 56), "Q", "Q"),
                .. connection,
                new DiagramLabel("suction-line", new(70, 180), "d_suc", "d_suc"),
                new DiagramLabel("pressure-line", new(180, 180), "d_prs", "d_prs"),
                new DiagramLabel("return-line", new(290, 180), "d_ret", "d_ret"),
                new DiagramLabel("modulus", new(60, 204), "E", "E"),
                new DiagramLabel("yield", new(160, 204), "f_y", "f_y"),
                new DiagramLabel("mounting-factor", new(250, 204), "K_m", "K_m"),
                new DiagramLabel("safety-factor", new(330, 204), "S_f", "S_f"),
                new DiagramLabel("mechanical-efficiency", new(300, 228), "eta_m", "η_m"),
            ]);

        return new(CylinderCalculator.Id,
        [
            Variant("A cylinder of bore D and rod d_r, extend connection standard: supply p_sys at the cap end, back pressure p_back at the rod end, pushing F_push out and pulling F_pull back", CylinderCalculator.Standard,
            [
                new DiagramPointLoad("back-pressure", new(228, 70), DiagramDirection.Down, 20, "p_back", "p_back", new(250, 36)),
                new DiagramLabel("connection", new(120, 228), "mode", "mode"),
            ]),
            Variant("A cylinder of bore D and rod d_r, regenerative extend: the rod end joined to the supply p_sys, pushing F_push out and pulling F_pull back", CylinderCalculator.Regenerative,
            [
                new DiagramPolyline("regenerative-link", [new(228, 70), new(228, 46), new(62, 46), new(62, 70)], Thickness: 2, InputName: "mode", Symbol: "mode", LabelAt: new(120, 228)),
            ]),
        ]);
    }

    private static CalculationDiagramSpec ORingGland()
    {
        static IEnumerable<DiagramElement> Conditions() =>
        [
            new DiagramLabel("duty", new(60, 212), "duty", "duty"),
            new DiagramLabel("minimum-temperature", new(175, 212), "T_min", "T_min"),
            new DiagramLabel("maximum-temperature", new(290, 212), "T_max", "T_max"),
            new DiagramLabel("squeeze-low", new(70, 230), "q_lo_s", "q_lo_s"),
            new DiagramLabel("squeeze-high", new(180, 230), "q_hi_s", "q_hi_s"),
            new DiagramLabel("gap-allowed", new(300, 230), "gap_allow", "gap"),
        ];

        // A groove of width b cut into a part from y, its root at y + depth (depth negative cuts upwards), with the ring of section W in it.
        static IEnumerable<DiagramElement> Groove(string suffix, double x, double y, double depth, DiagramPoint? ringLabelAt) =>
        [
            new DiagramPolyline($"groove-{suffix}", [new(x, y), new(x, y + depth), new(x + 24, y + depth), new(x + 24, y)], Thickness: 1.5, InputName: "b"),
            new DiagramCircle($"ring-{suffix}", new(x + 12, y + depth / 2), 8, Filled: true, "W", ringLabelAt is null ? null : "W", ringLabelAt),
        ];

        return new(ORingGlandCalculator.Id,
        [
            new DiagramVariant("A piston seal in section: the ring of section W in the piston groove (root D_g, width b), the piston D_c in the bore D_s, pressure p_op on one side", When("type", ORingGlandCalculator.TypePiston),
            [
                new DiagramPlate("bore-wall-top", 60, 40, 200, 12),
                new DiagramPlate("bore-wall-bottom", 60, 188, 200, 12),
                new DiagramPlate("piston", 120, 58, 100, 124, "D_c"),
                .. Groove("top", 150, 58, 16, new(330, 46)),
                .. Groove("bottom", 150, 182, -16, null),
                new DiagramDimension("sealing-surface", new(60, 52), new(60, 188), -20, "D_s", "D_s", new(80, 24)),
                new DiagramDimension("mating-part", new(220, 58), new(220, 182), 16, "D_c", "D_c", new(330, 94)),
                new DiagramDimension("groove-root", new(174, 74), new(174, 166), 84, "D_g", "D_g"),
                new DiagramDimension("groove-width", new(150, 58), new(174, 58), -30, "b", "b", new(200, 24)),
                new DiagramLabel("ring-inside-diameter", new(330, 70), "d_r", "d_r"),
                new DiagramPointLoad("pressure", new(120, 120), DiagramDirection.Right, 40, "p_op", "p_op", new(62, 104)),
                .. Conditions(),
            ]),
            new DiagramVariant("A rod seal in section: the ring of section W in the housing groove (root D_g, width b) round the rod D_s, the housing bore D_c, pressure p_op on one side", When("type", ORingGlandCalculator.TypeRod),
            [
                new DiagramPlate("rod", 40, 90, 196, 60, "D_s"),
                new DiagramPlate("housing-top", 120, 40, 100, 50, "D_c"),
                new DiagramPlate("housing-bottom", 120, 150, 100, 50, "D_c"),
                .. Groove("top", 150, 90, -16, new(330, 46)),
                .. Groove("bottom", 150, 150, 16, null),
                new DiagramDimension("sealing-surface", new(40, 90), new(40, 150), -14, "D_s", "D_s", new(70, 170)),
                new DiagramDimension("mating-part", new(220, 90), new(220, 150), 16, "D_c", "D_c", new(330, 94)),
                new DiagramDimension("groove-root", new(174, 74), new(174, 166), 84, "D_g", "D_g"),
                new DiagramDimension("groove-width", new(150, 40), new(174, 40), -20, "b", "b", new(210, 30)),
                new DiagramLabel("ring-inside-diameter", new(330, 70), "d_r", "d_r"),
                new DiagramPointLoad("pressure", new(120, 70), DiagramDirection.Right, 40, "p_op", "p_op", new(62, 54)),
                .. Conditions(),
            ]),
            new DiagramVariant("A face seal in section: the ring of section W in a groove of depth h_f and width b in the upper flange, the flanges a gap e_f apart, the ring seated on D_g", When("type", ORingGlandCalculator.TypeFace),
            [
                new DiagramPlate("flange-top", 80, 40, 200, 50),
                new DiagramPlate("flange-bottom", 80, 96, 200, 50),
                .. Groove("top", 150, 90, -16, new(330, 56)),
                new DiagramDimension("groove-depth", new(174, 74), new(174, 90), 0, "h_f", "h_f", new(230, 66)),
                new DiagramDimension("flange-gap", new(280, 90), new(280, 96), 10, "e_f", "e_f", new(340, 100)),
                new DiagramDimension("groove-width", new(150, 40), new(174, 40), -20, "b", "b", new(210, 30)),
                new DiagramDimension("seat-diameter", new(150, 146), new(250, 146), 20, "D_g", "D_g"),
                new DiagramLabel("ring-inside-diameter", new(330, 120), "d_r", "d_r"),
                new DiagramPointLoad("pressure", new(100, 93), DiagramDirection.Right, 40, "p_op", "p_op", new(60, 160)),
                .. Conditions(),
            ]),
        ]);
    }
}
