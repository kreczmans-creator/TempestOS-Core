using Tempest.Calc.BuildingServices;
using Tempest.Calc.Structural;
using Tempest.Core.Calculations.Modules.Diagrams;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// The reference diagrams of the Addendum A and B structural and
/// building-services templates (DI-08, DC-21): the base plate, the EN 1993-1-8
/// connection, the lifting lugs, the load combinations, the LTB interaction,
/// the plate in bending, the Glaser wall, the heat exchanger and the
/// insulation. Same rules as the first 27: inputs only, not to scale, one
/// variant per choice that changes the geometry.
/// </summary>
public static partial class CalcDiagrams
{
    /// <summary>A variant condition on two choices at once (a shape and its load, an arrangement and its mode).</summary>
    private static Dictionary<string, string> StructBsWhen(string input1, string value1, string input2, string value2) =>
        new() { [input1] = value1, [input2] = value2 };

    // ---- Structural (Addendum A and B) ----

    private static CalculationDiagramSpec BasePlate()
    {
        static DiagramVariant Variant(string caption, string column, IReadOnlyList<DiagramElement> footprint, IReadOnlyList<DiagramElement> sectionLabels)
        {
            List<DiagramElement> elements =
            [
                // Plan: the plate, the column footprint on it, and one bolt row each side of the column axis.
                new DiagramPlate("plate", 36, 66, 140, 100, "f_y"),
                new DiagramDimension("bolt-row", new(106, 66), new(146, 66), -16, "x_b", "x_b", new(126, 36)),
                new DiagramDimension("length", new(36, 166), new(176, 166), 10, "L", "L"),
                new DiagramDimension("width", new(36, 66), new(36, 166), -14, "B", "B", new(44, 206)),
                new DiagramCircle("bolt-1", new(66, 90), 5, Filled: true, "n_t", "n_t", new(56, 116), Solid: true),
                new DiagramCircle("bolt-2", new(66, 142), 5, Filled: true, "n_t", Solid: true),
                new DiagramCircle("bolt-3", new(146, 90), 5, Filled: true, "n_t", Solid: true),
                new DiagramCircle("bolt-4", new(146, 142), 5, Filled: true, "n_t", Solid: true),
                new DiagramLabel("bolts-total", new(156, 116), "n_b", "n_b"),
                .. footprint,
                // Elevation: the column stub on the plate, the grout and the concrete, under the axial force, the moment and the shear.
                new DiagramPlate("column", 316, 56, 28, 60),
                new DiagramPointLoad("axial", new(330, 56), DiagramDirection.Down, 32, "N_Ed", "N_Ed", new(330, 14)),
                new DiagramMoment("moment", new(330, 86), 14, Clockwise: true, "M_Ed", "M_Ed", new(250, 76)),
                new DiagramPointLoad("shear", new(316, 104), DiagramDirection.Right, 40, "V_Ed", "V_Ed", new(262, 96)),
                new DiagramPlate("plate-section", 280, 116, 110, 10, "t_p", "t_p", new(232, 122)),
                new DiagramPlate("grout", 280, 126, 110, 8, "t_g"),
                new DiagramPlate("concrete", 260, 134, 140, 30, "f_ck"),
                .. sectionLabels,
                new DiagramLabel("concrete-strength", new(346, 206), "f_ck", "f_ck"),
                new DiagramLabel("prying", new(44, 228), "prying", "prying"),
                new DiagramLabel("grout-thickness", new(236, 228), "t_g", "t_g"),
                new DiagramLabel("plate-yield", new(346, 228), "f_y", "f_y"),
            ];
            return new DiagramVariant(caption, When("column", column), elements);
        }

        return new(BasePlateCalculator.Id,
        [
            Variant("A base plate B by L in plan under an I/H column, one holding-down bolt row each side at x_b from the axis; in elevation the column on the plate, grout and concrete under N_Ed, M_Ed and V_Ed", BasePlateCalculator.ColumnIH,
            [
                new DiagramPlate("flange-left", 88, 96, 6, 40, "h"),
                new DiagramPlate("flange-right", 118, 96, 6, 40, "h"),
                new DiagramPlate("web", 94, 113, 24, 6, "t_w"),
                new DiagramLabel("depth", new(106, 156), "h", "h"),
            ],
            [
                new DiagramLabel("column-width", new(135, 206), "b", "b"),
                new DiagramLabel("flange-thickness", new(240, 206), "t_f", "t_f"),
                new DiagramLabel("web-thickness", new(135, 228), "t_w", "t_w"),
            ]),
            Variant("A base plate B by L in plan under an RHS column, one holding-down bolt row each side at x_b from the axis; in elevation the column on the plate, grout and concrete under N_Ed, M_Ed and V_Ed", BasePlateCalculator.ColumnRhs,
            [
                new DiagramPlate("rhs-outer", 86, 96, 40, 40, "h"),
                new DiagramPlate("rhs-inner", 92, 102, 28, 28, "t_wall"),
                new DiagramLabel("depth", new(106, 156), "h", "h"),
            ],
            [
                new DiagramLabel("column-width", new(135, 206), "b", "b"),
                new DiagramLabel("wall-thickness", new(240, 206), "t_wall", "t_wall"),
            ]),
            Variant("A base plate B by L in plan under a CHS column of diameter D, one holding-down bolt row each side at x_b from the axis; in elevation the column on the plate, grout and concrete under N_Ed, M_Ed and V_Ed", BasePlateCalculator.ColumnChs,
            [
                new DiagramCircle("chs-outer", new(106, 116), 20, Filled: true, "D"),
                new DiagramCircle("chs-inner", new(106, 116), 14, Filled: false, "t_wall"),
                new DiagramLabel("diameter", new(106, 156), "D", "D"),
            ],
            [
                new DiagramLabel("wall-thickness", new(240, 206), "t_wall", "t_wall"),
            ]),
        ]);
    }

    private static CalculationDiagramSpec ConnectionEc3()
    {
        static DiagramVariant Variant(string caption, string holeType, Func<string, double, DiagramElement> hole)
        {
            List<DiagramElement> elements =
            [
                // Plan: the plate end with two bolts in the load direction, the end, edge and pitch distances, the shear per bolt pulling the plate.
                new DiagramPlate("plate", 40, 60, 200, 110, "steel"),
                hole("hole-1", 100),
                hole("hole-2", 170),
                new DiagramCircle("bolt-1", new(100, 115), 7, Filled: true, "size", Solid: true),
                new DiagramCircle("bolt-2", new(170, 115), 7, Filled: true, "size", Solid: true),
                new DiagramDimension("end-distance", new(40, 170), new(100, 170), 14, "e_1", "e_1"),
                new DiagramDimension("edge-distance", new(170, 60), new(170, 115), 50, "e_2", "e_2"),
                new DiagramDimension("pitch", new(100, 60), new(170, 60), -14, "p_1", "p_1", new(135, 36)),
                new DiagramPointLoad("shear", new(280, 115), DiagramDirection.Right, 40, "F_v_Ed", "F_v,Ed", new(262, 136)),
                // Section: the two plies with the bolt through them, the tension per bolt pulling it out.
                new DiagramPlate("ply-1", 310, 112, 70, 10, "t_p", "t_p", new(340, 180)),
                new DiagramPlate("ply-2", 310, 122, 70, 10, "t_2"),
                new DiagramMember("shank", new(345, 92), new(345, 150), 8, "size", "size", new(310, 196)),
                new DiagramPointLoad("tension", new(345, 68), DiagramDirection.Up, 24, "F_t_Ed", "F_t,Ed", new(340, 52)),
                new DiagramLabel("other-ply", new(338, 160), "t_2", "t_2"),
                new DiagramLabel("shear-planes", new(34, 36), "n_s", "n_s"),
                new DiagramLabel("threads", new(262, 36), "threads", "threads"),
                new DiagramLabel("plate-yield", new(190, 196), "f_y", "f_y"),
                new DiagramLabel("plate-steel", new(70, 220), "steel", "steel"),
                new DiagramLabel("plate-ultimate", new(190, 220), "f_u", "f_u"),
                new DiagramLabel("bolt-grade", new(310, 220), "grade", "grade"),
            ];
            return new DiagramVariant(caption, When("hole_type", holeType), elements);
        }

        static DiagramElement Round(string id, double x, double radius) => new DiagramCircle(id, new(x, 115), radius, Filled: false, "hole_type");

        static DiagramElement Slot(string id, double x) => new DiagramPolyline(id,
            [new(x - 10, 99), new(x + 10, 99), new(x + 16, 105), new(x + 16, 125), new(x + 10, 131), new(x - 10, 131), new(x - 16, 125), new(x - 16, 105), new(x - 10, 99)],
            Thickness: 1.5, InputName: "hole_type");

        return new(ConnectionCalculator.Id,
        [
            Variant("Two bolts in normal clearance holes at a plate end, e_1 to the end, e_2 to the edge and p_1 apart; in section the plies t_p and t_2 with the bolt through them", ConnectionCalculator.HoleNormal, (id, x) => Round(id, x, 10)),
            Variant("Two bolts in oversize holes at a plate end, e_1 to the end, e_2 to the edge and p_1 apart; in section the plies t_p and t_2 with the bolt through them", ConnectionCalculator.HoleOversize, (id, x) => Round(id, x, 13)),
            Variant("Two bolts in slots across the load at a plate end, e_1 to the end, e_2 to the edge and p_1 apart; in section the plies t_p and t_2 with the bolt through them", ConnectionCalculator.HoleSlot, Slot),
        ]);
    }

    private static CalculationDiagramSpec Lifting()
    {
        static DiagramVariant Variant(int lugs, bool beam)
        {
            IReadOnlyList<double> at = lugs switch
            {
                1 => [200],
                2 => [150, 250],
                3 => [140, 200, 260],
                _ => [128, 176, 224, 272],
            };

            List<DiagramElement> elements =
            [
                // The hook lifts the rated load; one sling per lug, at θ from the vertical.
                new DiagramPointLoad("rated-load", new(200, 6), DiagramDirection.Up, 24, "WLL", "WLL", new(262, 14)),
                new DiagramCircle("hook", new(200, 30), 4, Filled: true, Solid: true),
            ];

            for (var i = 0; i < at.Count; i++)
            {
                var x = at[i];
                elements.Add(new DiagramMember($"sling-{i + 1}", new(200, 30), new(x, 96), 2, "theta"));
                elements.Add(new DiagramPlate($"lug-{i + 1}", x - 10, 94, 20, 28, "t_p"));
                elements.Add(new DiagramCircle($"hole-{i + 1}", new(x, 104), 6, Filled: false, "Dh"));
                elements.Add(new DiagramCircle($"pin-{i + 1}", new(x, 104), 4, Filled: true, "Dp", Solid: true));
            }

            if (beam)
                elements.Add(new DiagramMember("spreader", new(110, 127), new(290, 127), 10, "A_beam", "A_beam", new(200, 152)));
            else
                elements.Add(new DiagramPlate("load", 120, 122, 160, 50));

            elements.AddRange(
            [
                new DiagramLabel("lug-count", new(60, 44), "n_lugs", "n_lugs"),
                new DiagramLabel("mode", new(60, 66), "mode", "mode"),
                new DiagramLabel("on-beam", new(60, 88), "beam", "beam"),
                new DiagramLabel("sling-angle", new(340, 44), "theta", "θ"),
                new DiagramLabel("out-of-plane", new(340, 66), "beta", "β"),
                new DiagramLabel("yield", new(340, 88), "f_y", "f_y"),
                new DiagramLabel("ultimate", new(340, 110), "f_u", "f_u"),
                new DiagramLabel("cycles", new(340, 160), "n_cycles", "n_cycles"),
                new DiagramLabel("thickness", new(70, 196), "t_p", "t_p"),
                new DiagramLabel("hole", new(180, 196), "Dh", "Dh"),
                new DiagramLabel("pin", new(290, 196), "Dp", "Dp"),
                new DiagramLabel("end-radius", new(70, 220), "r_lug", "r_lug"),
                new DiagramLabel("edge", new(180, 220), "be", "be"),
                new DiagramLabel("moment-arm", new(290, 220), "h", "h"),
            ]);

            var where = beam ? "on a spreader beam" : "on the load";
            var plural = lugs == 1 ? "one lug" : $"{lugs} lugs";
            return new DiagramVariant($"A hook lifting the rated load WLL through {plural} {where}, each sling at θ from the vertical; each lug a plate t_p with a hole Dh and a pin Dp",
                StructBsWhen("n_lugs", lugs.ToString(System.Globalization.CultureInfo.InvariantCulture), "beam", beam ? CalcDiagramReader.BooleanTrue : CalcDiagramReader.BooleanFalse), elements);
        }

        return new(LiftingLugCalculator.Id,
        [
            Variant(1, beam: true), Variant(3, beam: true), Variant(4, beam: true), Variant(2, beam: true),
            Variant(1, beam: false), Variant(3, beam: false), Variant(4, beam: false), Variant(2, beam: false),
        ]);
    }

    private static CalculationDiagramSpec LoadCombinations()
    {
        static DiagramVariant Variant(string caption, string method, bool reduction)
        {
            List<DiagramElement> elements =
            [
                // One member under the actions as listed: permanent as a distributed load, a variable and a snow load as point loads, wind across.
                new DiagramSupport("support-left", new(60, 146), DiagramSupportKind.Pinned, DiagramDirection.Down),
                new DiagramSupport("support-right", new(340, 146), DiagramSupportKind.Roller, DiagramDirection.Down),
                new DiagramMember("member", new(60, 140), new(340, 140), 10),
                new DiagramDistributedLoad("permanent", 60, 340, 134, 24, "actions"),
                new DiagramLabel("permanent-note", new(110, 100), Symbol: "G permanent"),
                new DiagramPointLoad("variable", new(200, 134), DiagramDirection.Down, 50, "actions"),
                new DiagramLabel("variable-note", new(200, 72), Symbol: "Q variable"),
                new DiagramPointLoad("snow", new(290, 134), DiagramDirection.Down, 50, "actions"),
                new DiagramLabel("snow-note", new(290, 72), Symbol: "S snow"),
                new DiagramPointLoad("wind", new(56, 140), DiagramDirection.Right, 36, "actions"),
                new DiagramLabel("wind-note", new(30, 120), Symbol: "W wind"),
                new DiagramLabel("actions", new(90, 26), "actions", "actions"),
                new DiagramLabel("permanent-favourable", new(210, 26), "gamma_G_inf", "γ_G,inf"),
                new DiagramLabel("consequence", new(320, 26), "K_FI", "K_FI"),
                new DiagramLabel("permanent-unfavourable", new(90, 50), "gamma_G_sup", "γ_G,sup"),
                new DiagramLabel("variable-factor", new(200, 50), "gamma_Q", "γ_Q"),
                new DiagramLabel("psi", new(300, 50), "psi", "ψ"),
                new DiagramLabel("unit", new(60, 200), "unit", "unit"),
                new DiagramLabel("method", new(230, 200), "method", "method"),
            ];

            if (reduction)
                elements.Add(new DiagramLabel("reduction", new(60, 224), "xi", "ξ"));

            return new DiagramVariant(caption, When("method", method), elements);
        }

        return new(LoadCombinationsCalculator.Id,
        [
            Variant("One member under the actions as listed (permanent, variable, snow and wind drawn): expression 6.10a with the ψ_0 factors and 6.10b with ξ on the permanent actions", LoadCombinationsCalculator.Method610ab, reduction: true),
            Variant("One member under the actions as listed (permanent, variable, snow and wind drawn): expression 6.10 with γ_G, γ_Q and the ψ_0 factors", LoadCombinationsCalculator.Method610, reduction: false),
        ]);
    }

    private static CalculationDiagramSpec LtbInteraction()
    {
        static DiagramVariant Variant(string caption, string mode, DiagramElement momentLeft, DiagramElement momentRight) =>
            new(caption, When("moment_mode", mode),
            [
                // The member on fork supports under compression and its end moments; the buckling lengths below it.
                new DiagramSupport("fork-left", new(70, 106), DiagramSupportKind.Pinned, DiagramDirection.Down),
                new DiagramSupport("fork-right", new(250, 106), DiagramSupportKind.Roller, DiagramDirection.Down),
                new DiagramMember("member", new(70, 100), new(250, 100), 12, "L"),
                new DiagramPointLoad("axial", new(64, 100), DiagramDirection.Right, 36, "N_Ed", "N_Ed", new(64, 72)),
                momentLeft,
                momentRight,
                new DiagramLabel("load-height", new(166, 78), "z_g", "z_g"),
                new DiagramLabel("minor-moment", new(160, 122), "M_z_Ed", "M_z,Ed"),
                new DiagramDimension("span", new(70, 100), new(250, 100), 36, "L", "L"),
                new DiagramDimension("length-y", new(70, 100), new(250, 100), 60, "L_cr_y", "L_cr,y"),
                new DiagramDimension("length-z", new(70, 100), new(250, 100), 84, "L_cr_z", "L_cr,z"),
                // The I-section: flange width b, depth h, flange thickness t_f.
                new DiagramPlate("top-flange", 320, 60, 60, 10, "t_f", "t_f", new(268, 66)),
                new DiagramPlate("web", 345, 70, 10, 90, "section_class", "class", new(300, 200)),
                new DiagramPlate("bottom-flange", 320, 160, 60, 10, "b"),
                new DiagramDimension("flange-width", new(320, 60), new(380, 60), -12, "b", "b", new(350, 30)),
                new DiagramDimension("depth", new(380, 60), new(380, 170), 10, "h", "h", new(352, 184)),
                new DiagramLabel("yield", new(60, 222), "f_y", "f_y"),
                new DiagramLabel("modulus", new(160, 222), "E", "E"),
                new DiagramLabel("shear-modulus", new(250, 222), "G", "G"),
                new DiagramLabel("partial", new(340, 222), "gamma_M1", "γ_M1"),
            ]);

        return new(LtbInteractionCalculator.Id,
        [
            Variant("A member on fork supports under N_Ed with the end moments M_y,A and M_y,B about y-y, its buckling lengths, and its I-section", LtbInteractionCalculator.ModeEnds,
                new DiagramMoment("moment-left", new(102, 100), 14, Clockwise: true, "M_y_A", "M_y,A", new(96, 48)),
                new DiagramMoment("moment-right", new(218, 100), 14, Clockwise: false, "M_y_B", "M_y,B", new(240, 48))),
            Variant("A member on fork supports under N_Ed with the greatest moment M_y,Ed about y-y and the end-moment ratio ψ_y, its buckling lengths, and its I-section", LtbInteractionCalculator.ModePsi,
                new DiagramMoment("moment-left", new(102, 100), 14, Clockwise: true, "M_y_Ed", "M_y,Ed", new(100, 48)),
                new DiagramMoment("moment-right", new(218, 100), 14, Clockwise: false, "psi_y", "ψ_y", new(236, 48))),
        ]);
    }

    private static CalculationDiagramSpec PlateBending()
    {
        const double SectionY = 146;

        // A circle as a closed twelve-sided polyline: an outline on the plate, not a hole.
        static IReadOnlyList<DiagramPoint> Ring(double cx, double cy, double r) =>
            [.. Enumerable.Range(0, 13).Select(k => new DiagramPoint(cx + (r * Math.Cos(k * Math.PI / 6)), cy + (r * Math.Sin(k * Math.PI / 6))))];

        static DiagramVariant Variant(string shape, string support, string load, string? basis)
        {
            var circular = shape == PlateBendingCalculator.Circular;
            var clamped = support == PlateBendingCalculator.Clamped;
            var ring = load == PlateBendingCalculator.Ring;
            var loadInput = ring || basis == PlateBendingCalculator.BasisForce ? "F" : "q";
            var elements = new List<DiagramElement>();

            // Plan at the left: the plate's outline and the loaded area.
            if (circular)
            {
                elements.Add(new DiagramCircle("plan", new(110, 120), 70, Filled: true, "shape"));
                elements.Add(new DiagramDimension("radius", new(110, 120), new(159.5, 169.5), 0, "a", "a", new(110, 202)));
            }
            else
            {
                elements.Add(new DiagramPlate("plan", 40, 60, 140, 120, "shape"));
                elements.Add(new DiagramDimension("length-x", new(40, 180), new(180, 180), 14, "a", "a"));
                elements.Add(new DiagramDimension("length-y", new(180, 60), new(180, 180), 12, "b", "b", new(190, 44)));
            }

            if (ring)
            {
                elements.Add(new DiagramPolyline("ring", Ring(110, 120, 36), Dashed: true, Thickness: 1.5, InputName: "r_ring", Symbol: "r_ring", LabelAt: new(110, 70)));
            }
            else if (load == PlateBendingCalculator.Patch && circular)
            {
                elements.Add(new DiagramPolyline("patch", Ring(110, 120, 14), Thickness: 1.5, InputName: "r0", Symbol: "r0", LabelAt: new(110, 70)));
            }
            else if (load == PlateBendingCalculator.Patch)
            {
                elements.Add(new DiagramPlate("patch", 85, 100, 50, 40, "u", "u", new(110, 80)));
                elements.Add(new DiagramLabel("patch-y", new(110, 160), "v", "v"));
            }

            // Section at the right: the plate on its edge supports under the load.
            elements.Add(new DiagramPlate("section", 240, SectionY, 140, 8, "t_p", "t_p", new(310, 178)));

            if (clamped)
            {
                elements.Add(new DiagramSupport("edge-left", new(240, SectionY + 4), DiagramSupportKind.Fixed, DiagramDirection.Left, "support"));
                elements.Add(new DiagramSupport("edge-right", new(380, SectionY + 4), DiagramSupportKind.Fixed, DiagramDirection.Right, "support"));
            }
            else
            {
                elements.Add(new DiagramSupport("edge-left", new(240, SectionY + 8), DiagramSupportKind.Pinned, DiagramDirection.Down, "support"));
                elements.Add(new DiagramSupport("edge-right", new(380, SectionY + 8), DiagramSupportKind.Pinned, DiagramDirection.Down, "support"));
            }

            if (ring)
            {
                elements.Add(new DiagramPointLoad("ring-left", new(274, SectionY), DiagramDirection.Down, 30, "F"));
                elements.Add(new DiagramPointLoad("ring-right", new(346, SectionY), DiagramDirection.Down, 30, "F", "F", new(310, 104)));
            }
            else if (load == PlateBendingCalculator.Patch)
            {
                elements.Add(new DiagramDistributedLoad("load", 290, 330, SectionY, 26, loadInput, loadInput));
            }
            else
            {
                elements.Add(new DiagramDistributedLoad("load", 240, 380, SectionY, 26, loadInput, loadInput));
            }

            elements.AddRange(
            [
                new DiagramLabel("modulus", new(260, 200), "E", "E"),
                new DiagramLabel("poisson", new(350, 200), "nu", "ν"),
                new DiagramLabel("yield", new(232, 222), "f_y", "f_y"),
                new DiagramLabel("density", new(340, 222), "rho", "ρ"),
            ]);

            var loadText = ring ? "a ring load F at r_ring" : load == PlateBendingCalculator.Patch
                ? (circular ? "a central patch of radius r0" : "a central patch u by v")
                : "a uniform load";
            var basisText = ring ? string.Empty : basis == PlateBendingCalculator.BasisForce ? " given as the total force F" : " given as the pressure q";
            var shapeText = circular ? "A circular plate of radius a" : "A rectangular plate a by b";
            var when = StructBsWhen("shape", shape, "load_type", load);
            when["support"] = support;

            if (basis is not null)
                when["load_basis"] = basis;

            return new DiagramVariant($"{shapeText}, {support} all round, under {loadText}{basisText}; in section the plate t_p on its edge supports", when, elements);
        }

        var variants = new List<DiagramVariant>();

        foreach (var shape in new[] { PlateBendingCalculator.Circular, PlateBendingCalculator.Rectangular })
        {
            foreach (var support in new[] { PlateBendingCalculator.Clamped, PlateBendingCalculator.Simple })
            {
                if (shape == PlateBendingCalculator.Circular)
                    variants.Add(Variant(shape, support, PlateBendingCalculator.Ring, null));

                foreach (var load in new[] { PlateBendingCalculator.Patch, PlateBendingCalculator.Uniform })
                {
                    foreach (var basis in new[] { PlateBendingCalculator.BasisForce, PlateBendingCalculator.BasisPressure })
                        variants.Add(Variant(shape, support, load, basis));
                }
            }
        }

        // The last variant (rectangular, simply supported, uniform pressure: the defaults) is the one drawn for a ring load on a rectangle, which the sheet refuses.
        return new(PlateBendingCalculator.Id, variants);
    }

    // ---- Building services (Addendum A and B) ----

    private static CalculationDiagramSpec Glaser()
    {
        static DiagramVariant Variant(string caption, string mode, IReadOnlyList<DiagramElement> inside) =>
            new(caption, When("humidity_mode", mode),
            [
                // The wall, inside left and outside right: three layers drawn, the layers as listed.
                new DiagramLabel("layers-note", new(200, 22), Symbol: "d, λ, μ per layer"),
                new DiagramDimension("thickness", new(150, 50), new(250, 50), -14, "layers"),
                new DiagramPlate("layer-1", 150, 50, 34, 130, "layers", "layers", new(200, 206)),
                new DiagramPlate("layer-2", 184, 50, 33, 130, "layers"),
                new DiagramPlate("layer-3", 217, 50, 33, 130, "layers"),
                new DiagramLabel("inside-note", new(75, 40), Symbol: "inside"),
                new DiagramLabel("inside-temperature", new(70, 70), "T_int", "T_int"),
                .. inside,
                new DiagramPointLoad("inside-film", new(146, 150), DiagramDirection.Right, 40, "Rsi"),
                new DiagramLabel("inside-resistance", new(74, 190), "Rsi", "R_si"),
                new DiagramLabel("outside-note", new(325, 40), Symbol: "outside"),
                new DiagramLabel("climate", new(325, 70), "climate", "climate"),
                new DiagramLabel("climate-note", new(325, 94), Symbol: "θ_e, RH_e by month"),
                new DiagramLabel("critical-humidity", new(325, 118), "RH_crit", "RH_crit"),
                new DiagramPointLoad("outside-film", new(294, 150), DiagramDirection.Right, 40, "Rse"),
                new DiagramLabel("outside-resistance", new(325, 190), "Rse", "R_se"),
                new DiagramLabel("breathable", new(110, 228), "breathable", "breathable"),
                new DiagramLabel("condensate-limit", new(280, 228), "Mc_limit", "Mc_limit"),
            ]);

        return new(GlaserCalculator.Id,
        [
            Variant("A wall of the layers as listed (three drawn), inside left and outside right, the inside vapour pressure from the moisture excess at the cold and warm points", GlaserCalculator.MoistureExcess,
            [
                new DiagramLabel("excess-cold", new(70, 94), "dv_a", "Δv_a"),
                new DiagramLabel("cold-point", new(70, 118), "theta_a", "θ_a"),
                new DiagramLabel("excess-warm", new(70, 142), "dv_b", "Δv_b"),
                new DiagramLabel("warm-point", new(70, 166), "theta_b", "θ_b"),
            ]),
            Variant("A wall of the layers as listed (three drawn), inside left and outside right, the inside at T_int and a constant relative humidity RH_int", GlaserCalculator.ConstantRh,
            [
                new DiagramLabel("inside-humidity", new(70, 94), "RH_int", "RH_int"),
            ]),
        ]);
    }

    private static CalculationDiagramSpec HeatExchanger()
    {
        static DiagramVariant Variant(string arrangement, string? constSide, string mode)
        {
            var rating = mode == HeatExchangerCalculator.Rating;
            List<DiagramElement> elements =
            [
                // The shell, the dividing wall of area A, the hot stream above it and the cold stream below.
                new DiagramPlate("shell", 120, 60, 160, 110),
                rating
                    ? new DiagramLabel("area", new(90, 24), "A", "A")
                    : new DiagramLabel("duty", new(100, 24), "Q_req", "Q_req"),
                new DiagramLabel("approach", new(300, 24), "dT_min", "ΔT_min"),
                new DiagramLabel("hot-inlet", new(64, 76), "T_h_in", "T_h,in"),
                new DiagramLabel("hot-flow", new(64, 108), "m_h", "ṁ_h"),
                new DiagramLabel("hot-cp", new(100, 206), "cp_h", "c_p,h"),
                new DiagramLabel("cold-cp", new(300, 206), "cp_c", "c_p,c"),
                new DiagramLabel("coefficient", new(100, 228), "U_clean", "U_clean"),
                new DiagramLabel("fouling", new(300, 228), "R_f", "R_f"),
            ];

            if (arrangement == HeatExchangerCalculator.ShellTube)
            {
                // Tube side (hot) in and back out at the left, the shell side (cold) in at the right bottom and out at the right top.
                elements.Add(new DiagramPolyline("tubes", [new(120, 90), new(250, 90), new(250, 135), new(120, 135)], Thickness: 3, InputName: "U_clean"));
                elements.Add(new DiagramPointLoad("hot-in", new(120, 90), DiagramDirection.Right, 36, "T_h_in"));
                elements.Add(new DiagramPointLoad("hot-out", new(84, 135), DiagramDirection.Left, 36));
                elements.Add(new DiagramPointLoad("cold-in", new(280, 160), DiagramDirection.Left, 36, "T_c_in"));
                elements.Add(new DiagramPointLoad("cold-out", new(316, 70), DiagramDirection.Right, 36));
            }
            else
            {
                elements.Add(new DiagramMember("wall", new(120, 115), new(280, 115), 4, "U_clean"));
                elements.Add(new DiagramPointLoad("hot-in", new(120, 88), DiagramDirection.Right, 36, "T_h_in"));
                elements.Add(new DiagramPointLoad("hot-out", new(316, 88), DiagramDirection.Right, 36));

                if (arrangement == HeatExchangerCalculator.Parallel)
                {
                    elements.Add(new DiagramPointLoad("cold-in", new(120, 142), DiagramDirection.Right, 36, "T_c_in"));
                    elements.Add(new DiagramPointLoad("cold-out", new(316, 142), DiagramDirection.Right, 36));
                }
                else if (arrangement == HeatExchangerCalculator.Crossflow)
                {
                    elements.Add(new DiagramPointLoad("cold-in", new(200, 170), DiagramDirection.Up, 30, "T_c_in"));
                    elements.Add(new DiagramPointLoad("cold-out", new(200, 42), DiagramDirection.Up, 18));
                }
                else
                {
                    elements.Add(new DiagramPointLoad("cold-in", new(280, 142), DiagramDirection.Left, 36, "T_c_in"));
                    elements.Add(new DiagramPointLoad("cold-out", new(84, 142), DiagramDirection.Left, 36));
                }
            }

            var coldLeft = arrangement == HeatExchangerCalculator.Parallel;
            elements.Add(new DiagramLabel("cold-inlet", new(coldLeft ? 64 : 342, coldLeft ? 130 : 104), "T_c_in", "T_c,in"));
            elements.Add(new DiagramLabel("cold-flow", new(coldLeft ? 64 : 342, coldLeft ? 154 : 128), "m_c", "ṁ_c"));

            if (constSide is not null)
                elements.Add(new DiagramLabel("constant-note", new(200, constSide == HeatExchangerCalculator.HotConstant ? 76 : 150), Symbol: "constant T"));

            var when = StructBsWhen("arrangement", arrangement, "mode", mode);

            if (constSide is not null)
                when["const_side"] = constSide;

            var flow = arrangement switch
            {
                HeatExchangerCalculator.Parallel => "Parallel flow, both streams left to right",
                HeatExchangerCalculator.ShellTube => "Shell and tube: the hot stream through the tubes and back, the cold stream through the shell",
                HeatExchangerCalculator.Crossflow => "Crossflow: the hot stream left to right, the cold stream across it",
                HeatExchangerCalculator.Isothermal => constSide == HeatExchangerCalculator.HotConstant ? "The hot stream at constant temperature over the cold stream" : "The cold stream at constant temperature under the hot stream",
                _ => "Counterflow, the hot stream left to right above the cold stream right to left",
            };
            var modeText = rating ? "rating for the given area A" : "sizing for the required duty Q_req";
            return new DiagramVariant($"{flow}; {modeText}", when, elements);
        }

        var variants = new List<DiagramVariant>();

        foreach (var mode in new[] { HeatExchangerCalculator.Sizing, HeatExchangerCalculator.Rating })
        {
            variants.Add(Variant(HeatExchangerCalculator.Isothermal, HeatExchangerCalculator.ColdConstant, mode));
            variants.Add(Variant(HeatExchangerCalculator.Isothermal, HeatExchangerCalculator.HotConstant, mode));
            variants.Add(Variant(HeatExchangerCalculator.ShellTube, null, mode));
            variants.Add(Variant(HeatExchangerCalculator.Crossflow, null, mode));
            variants.Add(Variant(HeatExchangerCalculator.Parallel, null, mode));
            variants.Add(Variant(HeatExchangerCalculator.Counterflow, null, mode));
        }

        return new(HeatExchangerCalculator.Id, variants);
    }

    private static CalculationDiagramSpec Insulation()
    {
        static DiagramVariant Variant(string geometry, string hMode)
        {
            var pipe = geometry == InsulationCalculator.Pipe;
            var given = hMode == InsulationCalculator.HGiven;
            var surface = given ? "h_se" : "eps";
            var elements = new List<DiagramElement>();

            if (pipe)
            {
                // Section through the pipe: the pipe wall (its outside D_i is the inside of the insulation) and the insulation round it.
                elements.Add(new DiagramCircle("insulation", new(110, 120), 66, Filled: true, "lambda"));
                elements.Add(new DiagramCircle("wall", new(110, 120), 34, Filled: true, "D_i"));
                elements.Add(new DiagramCircle("bore", new(110, 120), 30, Filled: false, "t_w"));
                elements.Add(new DiagramDimension("diameter", new(76, 120), new(144, 120), 0, "D_i", "D_i", new(110, 196)));
                elements.Add(new DiagramDimension("thickness", new(110, 54), new(110, 86), 0, "t_chosen", "t_chosen", new(110, 36)));
                elements.Add(new DiagramPointLoad("surface", new(212, 120), DiagramDirection.Right, 30, surface));
                elements.Add(new DiagramLabel("wall-thickness", new(110, 214), "t_w", "t_w"));
                elements.Add(new DiagramLabel("conductivity", new(110, 232), "lambda", "λ"));
            }
            else
            {
                // The flat surface: the wall, the insulation on it, and the flow length along it.
                elements.Add(new DiagramPlate("wall", 60, 60, 12, 120, "t_w"));
                elements.Add(new DiagramPlate("insulation", 72, 60, 50, 120, "lambda"));
                elements.Add(new DiagramDimension("thickness", new(72, 60), new(122, 60), -14, "t_chosen", "t_chosen", new(100, 32)));
                elements.Add(new DiagramPointLoad("surface", new(190, 96), DiagramDirection.Right, 30, surface));
                elements.Add(new DiagramLabel("wall-thickness", new(97, 204), "t_w", "t_w"));
                elements.Add(new DiagramLabel("conductivity", new(97, 226), "lambda", "λ"));

                if (!given)
                    elements.Add(new DiagramDimension("flow-length", new(122, 60), new(122, 180), 14, "L_surf", "L_surf", new(176, 52)));
            }

            // The conditions and the limits in one column at the right.
            var rows = new List<(string Input, string Symbol)> { ("T_proc", "T_proc"), ("T_amb", "T_amb"), ("RH_a", "RH_a") };

            if (given)
                rows.Add(("h_se", "h_se"));
            else
                rows.AddRange([("eps", "ε"), ("v_wind", "v_wind")]);

            rows.AddRange([("T_dew", "T_dew"), ("h_i", "h_i"), pipe ? ("q_max_m", "q_max,m") : ("q_max_A", "q_max,A"), ("theta_se_max", "θ_se,max"), ("dew_margin", "dew_margin")]);

            for (var i = 0; i < rows.Count; i++)
                elements.Add(new DiagramLabel(rows[i].Input.ToLowerInvariant().Replace('_', '-'), new(300, 30 + (21 * i)), rows[i].Input, rows[i].Symbol));

            var what = pipe ? "A pipe of outer diameter D_i in section, its wall t_w and the insulation t_chosen round it" : "A flat surface, its wall t_w and the insulation t_chosen on it";
            var how = given ? "the outer surface coefficient h_se as given" : "the outer surface coefficient from the convection and radiation correlation";
            return new DiagramVariant($"{what}, losing heat to the ambient with {how}", StructBsWhen("geometry", geometry, "h_mode", hMode), elements);
        }

        return new(InsulationCalculator.Id,
        [
            Variant(InsulationCalculator.Flat, InsulationCalculator.HCorrelation),
            Variant(InsulationCalculator.Flat, InsulationCalculator.HGiven),
            Variant(InsulationCalculator.Pipe, InsulationCalculator.HCorrelation),
            Variant(InsulationCalculator.Pipe, InsulationCalculator.HGiven),
        ]);
    }
}
