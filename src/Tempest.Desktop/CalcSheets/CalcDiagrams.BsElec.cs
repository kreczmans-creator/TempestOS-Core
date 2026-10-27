using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Core.Calculations.Modules.Diagrams;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// The reference diagrams of the building-services and electrical templates
/// Addenda A and B added (DI-08, DC-21): the pipe expansion loop, the room
/// heat loss, the sealed heating system, and the six electrical circuits.
/// </summary>
/// <remarks>
/// Same rules as <see cref="CalcDiagrams"/>: inputs only, never a result,
/// never to scale; every shape bound to the worksheet input it stands for;
/// a row list drawn representatively and labelled with its row count; a
/// choice that changes the geometry drawn as one variant per member.
/// </remarks>
public static partial class CalcDiagrams
{
    // ---- Building services (sheet 400 x 240, y downward, not to scale) ----

    private static CalculationDiagramSpec PipeExpansion()
    {
        // The pipe section (OD, t_wall) at the top left, and the conditions and material as labels across the top.
        static List<DiagramElement> Common() =>
        [
            new DiagramCircle("section", new(70, 58), 22, Filled: true, "OD", "OD", new(70, 20)),
            new DiagramCircle("bore", new(70, 58), 15, Filled: false, "t_wall", "t_wall", new(70, 94)),
            new DiagramLabel("install-temperature", new(200, 24), "T_install", "T_install"),
            new DiagramLabel("operating-temperature", new(200, 48), "T_operate", "T_operate"),
            new DiagramLabel("temperature-change", new(200, 72), "dT_in", "ΔT"),
            new DiagramLabel("modulus", new(330, 24), "E", "E"),
            new DiagramLabel("expansion", new(330, 48), "alpha", "α"),
            new DiagramLabel("allowable", new(330, 72), "Sa", "Sa"),
        ];

        // An offset leg: the run L from its anchor, the leg H off its end; a Z-bend adds the second run L2 to the far anchor.
        static DiagramVariant OffsetLeg(string caption, IReadOnlyDictionary<string, string> when, bool zBend)
        {
            var elements = Common();
            elements.AddRange(
            [
                new DiagramSupport("anchor-1", new(40, 130), DiagramSupportKind.Fixed, DiagramDirection.Left, Symbol: "anchor"),
                new DiagramMember("run", new(40, 130), new(250, 130), 8, "L", "L", new(130, 112)),
                new DiagramPointLoad("growth", new(236, 112), DiagramDirection.Right, 36, "alpha"),
                new DiagramSupport("slide", new(130, 130), DiagramSupportKind.Roller, DiagramDirection.Down, "mu", "μ", new(130, 172)),
                new DiagramMember("leg", new(250, 130), new(250, zBend ? 190 : 200), 8, "H", "H", new(214, 162)),
            ]);

            if (zBend)
            {
                elements.Add(new DiagramMember("run-2", new(250, 190), new(370, 190), 8, "L2", "L2", new(310, 172)));
                elements.Add(new DiagramSupport("anchor-2", new(370, 190), DiagramSupportKind.Fixed, DiagramDirection.Right, Symbol: "anchor", LabelAt: new(370, 224)));
            }
            else
            {
                elements.Add(new DiagramSupport("anchor-2", new(250, 200), DiagramSupportKind.Fixed, DiagramDirection.Down, Symbol: "anchor", LabelAt: new(250, 228)));
            }

            return new DiagramVariant(caption, when, elements);
        }

        var loop = Common();
        loop.AddRange(
        [
            new DiagramSupport("anchor-1", new(40, 130), DiagramSupportKind.Fixed, DiagramDirection.Left, Symbol: "anchor"),
            new DiagramMember("run", new(40, 130), new(150, 130), 8, "L", "L", new(95, 112)),
            new DiagramPointLoad("growth", new(346, 112), DiagramDirection.Right, 36, "alpha"),
            new DiagramSupport("slide", new(95, 130), DiagramSupportKind.Roller, DiagramDirection.Down, "mu", "μ", new(90, 196)),
            new DiagramMember("leg-1", new(150, 130), new(150, 200), 8, "H", "H", new(190, 166)),
            new DiagramMember("loop-base", new(150, 200), new(250, 200), 8, "H"),
            new DiagramMember("leg-2", new(250, 200), new(250, 130), 8, "H"),
            new DiagramMember("run-2", new(250, 130), new(360, 130), 8, "L"),
            new DiagramSupport("anchor-2", new(360, 130), DiagramSupportKind.Fixed, DiagramDirection.Right, Symbol: "anchor"),
            new DiagramLabel("loop-note", new(200, 224), Symbol: "U-loop"),
        ]);

        var bellows = Common();
        bellows.AddRange(
        [
            new DiagramSupport("anchor-1", new(40, 130), DiagramSupportKind.Fixed, DiagramDirection.Left, Symbol: "anchor", LabelAt: new(72, 164)),
            new DiagramMember("run", new(40, 130), new(170, 130), 8, "L"),
            new DiagramPointLoad("growth", new(156, 112), DiagramDirection.Right, 36, "alpha"),
            new DiagramSpring("bellows", new(170, 130), new(230, 130), "k_b", "k_b", new(200, 156)),
            new DiagramMember("run-2", new(230, 130), new(360, 130), 8, "L"),
            new DiagramSupport("anchor-2", new(360, 130), DiagramSupportKind.Fixed, DiagramDirection.Right, Symbol: "anchor", LabelAt: new(328, 164)),
            new DiagramDimension("spacing", new(40, 130), new(360, 130), 44, "L", "L"),
            new DiagramLabel("pressure", new(90, 206), "P_b", "P_b"),
            new DiagramLabel("effective-area", new(240, 206), "A_eff", "A_eff"),
            new DiagramLabel("rated-movement", new(110, 230), "dL_rated", "ΔL_rated"),
            new DiagramLabel("friction", new(300, 230), "mu", "μ"),
        ]);

        return new(PipeExpansionCalculator.Id,
        [
            OffsetLeg("An offset leg (L-bend): the run L grows from its anchor and bends the leg H at its end; the pipe section beside it", new Dictionary<string, string> { ["layout"] = PipeExpansionCalculator.OffsetLeg, ["L2"] = string.Empty }, zBend: false),
            OffsetLeg("An offset leg (Z-bend): the run L, the leg H, and the second run L2 to the far anchor; the pipe section beside it", When("layout", PipeExpansionCalculator.OffsetLeg), zBend: true),
            new DiagramVariant("A U-loop between two anchors: each leg H takes half the growth of the run L; the pipe section beside it", When("layout", PipeExpansionCalculator.UpLoop), loop),
            new DiagramVariant("An axial bellows in the run between two anchors, anchor spacing L: the bellows absorbs the growth; the pipe section beside it", When("layout", PipeExpansionCalculator.AxialBellows), bellows),
        ]);
    }

    private static CalculationDiagramSpec RoomHeatLoss() =>
        new(RoomHeatLossCalculator.Id,
        [
            new DiagramVariant("A room in section: the elements as listed bound it (a layered wall, a roof, a floor and a window drawn), the outside air beyond, ventilation air entering", Always,
            [
                new DiagramPlate("layer-1", 72, 68, 8, 128, "layers", "layers", new(80, 20)),
                new DiagramPlate("layer-2", 80, 68, 8, 128, "layers"),
                new DiagramPlate("layer-3", 88, 68, 8, 128, "layers"),
                new DiagramPlate("roof", 72, 56, 190, 12, "elements", "elements", new(230, 20)),
                new DiagramPlate("floor", 72, 196, 190, 12, "elements"),
                new DiagramPlate("wall-right", 250, 68, 12, 128, "elements"),
                new DiagramPlate("window", 246, 96, 20, 44, "elements"),
                new DiagramLabel("target-u", new(120, 42), "U_target", "U_target"),
                new DiagramLabel("addition", new(300, 42), "f_add", "f_add"),
                new DiagramLabel("volume", new(160, 96), "V", "V"),
                new DiagramLabel("inside", new(160, 122), "T_int", "T_int"),
                new DiagramPointLoad("ventilation", new(236, 120), DiagramDirection.Left, 36, "n", "n", new(160, 150)),
                new DiagramLabel("outside", new(330, 80), "theta_e", "θ_e"),
                new DiagramLabel("bridges", new(330, 108), "y_tb", "y_tb"),
                new DiagramLabel("infiltration", new(330, 136), "n_inf", "n_inf"),
                new DiagramDimension("floor-area", new(72, 208), new(262, 208), 6, "A_floor", "A_floor", new(76, 228)),
                new DiagramLabel("supply", new(260, 228), "T_supply", "T_supply"),
            ]),
        ]);

    private static CalculationDiagramSpec SealedSystem() =>
        new(SealedSystemCalculator.Id,
        [
            new DiagramVariant("A sealed heating circuit: the boiler, the flow and return pipework, the emitters of the rooms as listed (three drawn), and the expansion vessel below the static height", Always,
            [
                new DiagramPlate("boiler", 290, 160, 94, 46, "V_plant", "V_plant"),
                new DiagramMember("flow-riser", new(324, 160), new(324, 80), 6, "V_pipe"),
                new DiagramMember("flow-run", new(324, 80), new(60, 80), 6, "V_pipe", "V_pipe", new(100, 56)),
                new DiagramMember("return-run", new(60, 140), new(344, 140), 6, "V_pipe"),
                new DiagramMember("return-riser", new(344, 140), new(344, 160), 6, "V_pipe"),
                new DiagramPlate("emitter-1", 150, 90, 30, 42, "rooms"),
                new DiagramPlate("emitter-2", 200, 90, 30, 42, "rooms", "rooms", new(215, 182)),
                new DiagramPlate("emitter-3", 250, 90, 30, 42, "rooms"),
                new DiagramLabel("flow-temperature", new(240, 56), "T_flow", "T_flow"),
                new DiagramLabel("return-temperature", new(215, 158), "T_return", "T_return"),
                new DiagramLabel("room-temperature", new(215, 206), "T_room", "T_room"),
                new DiagramMember("vessel-connection", new(60, 140), new(60, 184), 3, "V_vessel"),
                new DiagramCircle("vessel", new(60, 200), 16, Filled: true, "V_vessel", "V_vessel", new(90, 230)),
                new DiagramDimension("static-height", new(20, 200), new(20, 80), 0, "h_static", "h_static", new(70, 24)),
                new DiagramLabel("fill-temperature", new(200, 24), "T_fill", "T_fill"),
                new DiagramLabel("maximum-temperature", new(330, 24), "T_max", "T_max"),
                new DiagramLabel("final-pressure", new(86, 102), "p_f", "p_f"),
                new DiagramLabel("safety-valve", new(86, 126), "p_prv", "p_prv"),
                new DiagramLabel("margin", new(290, 230), "p_margin", "p_margin"),
                new DiagramLabel("exponent", new(190, 230), "n", "n"),
            ]),
        ]);

    // ---- Electrical ----

    private static CalculationDiagramSpec ConductorSizing()
    {
        // The supply, the protective device, the conductors of circuit length L, and the load; the factors as labels below.
        static DiagramVariant Variant(string caption, string supply, int conductors)
        {
            var elements = new List<DiagramElement>
            {
                new DiagramCircle("source", new(50, 108), 22, Filled: false, "U", "U", new(50, 150)),
                new DiagramMember("source-lead", new(72, 108), new(96, 108), 3, "U"),
                new DiagramPlate("device", 96, 94, 36, 28, "device", "device", new(150, 40)),
                new DiagramPlate("load", 320, 80, 60, 56, "P", "P", new(350, 64)),
                new DiagramDimension("route", new(132, 124), new(320, 124), 40, "L", "L", new(170, 176)),
                new DiagramLabel("system", new(290, 176), "system", "system"),
                new DiagramLabel("power-factor", new(56, 204), "cos_phi", "cos φ"),
                new DiagramLabel("efficiency", new(150, 204), "eta", "η"),
                new DiagramLabel("ambient", new(250, 204), "T_amb", "T_amb"),
                new DiagramLabel("grouped", new(340, 204), "N_g", "N_g"),
                new DiagramLabel("ambient-factor", new(56, 228), "C_a", "C_a"),
                new DiagramLabel("grouping-factor", new(150, 228), "C_g", "C_g"),
                new DiagramLabel("insulation-factor", new(250, 228), "C_i", "C_i"),
                new DiagramLabel("rated-ratings", new(340, 228), "ratings", "ratings"),
            };

            for (var k = 0; k < conductors; k++)
            {
                var y = 92 + (k * 32.0 / Math.Max(1, conductors - 1));
                elements.Add(new DiagramMember($"conductor-{k + 1}", new(132, y), new(320, y), 3, "insulation", k == 0 ? "insulation" : null, k == 0 ? new DiagramPoint(210, 70) : null));
            }

            return new DiagramVariant(caption, When("supply", supply), elements);
        }

        return new(ConductorSizingCalculator.Id,
        [
            Variant("A three-phase circuit: the supply U, the protective device, three line conductors of length L, and the load P", ConductorSizingCalculator.ThreePhase, 3),
            Variant("A single-phase circuit: the supply U, the protective device, line and neutral of length L, and the load P", ConductorSizingCalculator.SinglePhase, 2),
            Variant("A DC circuit: the supply U, the protective device, the two conductors of length L, and the load P", ConductorSizingCalculator.Dc, 2),
        ]);
    }

    private static CalculationDiagramSpec Containment()
    {
        // The pull route in plan (a straight, a bend, a straight drawn; the segments as listed) with the start tension, and the limits as labels.
        static List<DiagramElement> Route() =>
        [
            new DiagramMember("straight-1", new(240, 204), new(240, 116), 6, "route"),
            new DiagramPolyline("bend", [new(240, 116), new(244, 100), new(254, 90), new(268, 84), new(280, 82)], Thickness: 6, InputName: "route"),
            new DiagramMember("straight-2", new(280, 82), new(370, 82), 6, "route", "route", new(325, 64)),
            new DiagramPointLoad("start-tension", new(240, 206), DiagramDirection.Up, 28, "T_1", "T_1", new(290, 222)),
            new DiagramLabel("weight-correction", new(320, 120), "w_c", "w_c"),
            new DiagramLabel("grip-derating", new(320, 146), "k_n", "k_n"),
            new DiagramLabel("bend-multiple", new(320, 172), "n_bend", "n_bend"),
            new DiagramLabel("friction", new(50, 190), "mu", "μ"),
            new DiagramLabel("fill-limit", new(130, 190), "f_max", "f_max"),
            new DiagramLabel("grip-limit", new(100, 212), "T_grip", "T_grip"),
            new DiagramLabel("sidewall-limit", new(100, 234), "P_lim", "P_lim"),
        ];

        // Three cables (the cables as listed) inside the containment section.
        static IEnumerable<DiagramElement> Cables() =>
        [
            new DiagramCircle("cable-1", new(84, 92), 11, Filled: true, "cables", Solid: true),
            new DiagramCircle("cable-2", new(108, 92), 11, Filled: true, "cables", "cables", new(95, 18), Solid: true),
            new DiagramCircle("cable-3", new(96, 114), 11, Filled: true, "cables", Solid: true),
        ];

        var conduit = new List<DiagramElement>
        {
            new DiagramCircle("conduit", new(95, 104), 44, Filled: false, "D_int"),
            new DiagramDimension("bore", new(51, 104), new(139, 104), -64, "D_int", "D_int"),
        };
        conduit.AddRange(Cables());
        conduit.AddRange(Route());

        var trunking = new List<DiagramElement>
        {
            new DiagramPlate("trunking", 45, 60, 100, 88, "W_int"),
            new DiagramDimension("width", new(45, 148), new(145, 148), 8, "W_int", "W_int", new(95, 168)),
            new DiagramDimension("depth", new(145, 60), new(145, 148), 12, "H_int", "H_int", new(195, 40)),
        };
        trunking.AddRange(Cables());
        trunking.AddRange(Route());

        return new(ContainmentCalculator.Id,
        [
            new DiagramVariant("A round conduit in section, bore D_int, holding the cables as listed (three drawn); the pull route in plan beside it", When("cont_type", ContainmentCalculator.Conduit), conduit),
            new DiagramVariant("A trunking or tray in section, W_int by H_int, holding the cables as listed (three drawn); the pull route in plan beside it", When("cont_type", ContainmentCalculator.Trunking), trunking),
        ]);
    }

    private static CalculationDiagramSpec FaultProtection()
    {
        // The earth-fault loop: the supply U_0 behind Z_e, the device, the line conductor to the fault, and the protective conductor back.
        static DiagramVariant Variant(string caption, string device, bool curve)
        {
            var elements = new List<DiagramElement>
            {
                new DiagramCircle("source", new(50, 110), 22, Filled: false, "U_0", "U_0", new(50, 70)),
                new DiagramMember("source-lead", new(72, 110), new(84, 110), 3, "U_0"),
                new DiagramPlate("external-impedance", 84, 98, 32, 24, "Z_e", "Z_e", new(100, 154)),
                new DiagramMember("impedance-lead", new(116, 110), new(130, 110), 3, "Z_e"),
                new DiagramPlate("device", 130, 86, 40, 34, "I_n", "I_n", new(150, 66)),
                new DiagramMember("line", new(170, 100), new(330, 100), 4, "S_line", "S_line", new(250, 116)),
                new DiagramCircle("fault", new(340, 100), 6, Filled: true, Symbol: "fault", LabelAt: new(368, 100), Solid: true),
                new DiagramPolyline("fault-path", [new(340, 100), new(340, 134)], Thickness: 2),
                new DiagramMember("protective", new(340, 134), new(50, 134), 4, "S_cpc", "S_cpc", new(250, 150)),
                new DiagramDimension("route", new(170, 134), new(330, 134), 36, "L", "L"),
                new DiagramLabel("transformer-rating", new(235, 20), "S_r", "S_r"),
                new DiagramLabel("transformer-impedance", new(235, 44), "u_k", "u_k"),
                new DiagramLabel("operating-temperature", new(60, 206), "T_op", "T_op"),
                new DiagramLabel("disconnection-time", new(190, 206), "t_req", "t_req"),
                new DiagramLabel("minimum-factor", new(320, 206), "C_min", "C_min"),
                new DiagramLabel("initial-temperature", new(60, 230), "theta_i", "θ_i"),
                new DiagramLabel("final-temperature", new(190, 230), "theta_f", "θ_f"),
                new DiagramLabel("maximum-factor", new(320, 230), "c_max", "c_max"),
            };

            if (curve)
            {
                elements.Add(new DiagramAxes("curve-axes", new(300, 64), 384, 14));
                elements.Add(new DiagramPolyline("curve", [new(306, 22), new(320, 36), new(342, 50), new(376, 58)], Thickness: 2, InputName: "curve", Symbol: "curve", LabelAt: new(330, 80)));
            }

            return new DiagramVariant(caption, When("device", device), elements);
        }

        return new(FaultProtectionCalculator.Id,
        [
            Variant("The earth-fault loop of a circuit on a type B MCB: the supply U_0 behind Z_e, the device I_n, line and protective conductors of length L to the fault", FaultProtectionCalculator.McbB, curve: false),
            Variant("The earth-fault loop of a circuit on a type C MCB: the supply U_0 behind Z_e, the device I_n, line and protective conductors of length L to the fault", FaultProtectionCalculator.McbC, curve: false),
            Variant("The earth-fault loop of a circuit on a type D MCB: the supply U_0 behind Z_e, the device I_n, line and protective conductors of length L to the fault", FaultProtectionCalculator.McbD, curve: false),
            Variant("The earth-fault loop of a circuit on a fuse or MCCB with its time–current points (drawn representatively): the supply U_0 behind Z_e, the device I_n, line and protective conductors of length L to the fault", FaultProtectionCalculator.Curve, curve: true),
        ]);
    }

    private static CalculationDiagramSpec LoadBudget()
    {
        // Three buses, a load on each (the loads as listed), the additional load, the power supply, and the battery.
        static DiagramVariant Variant(string caption, string psuBus, DiagramElement? feed)
        {
            var elements = new List<DiagramElement>
            {
                new DiagramCircle("transformer", new(40, 46), 16, Filled: false, "k_T", "k_T", new(70, 18)),
                new DiagramMember("bus-3ph", new(56, 46), new(330, 46), 4, "U_3ph", "U_3ph", new(200, 30)),
                new DiagramMember("bus-1ph", new(56, 110), new(330, 110), 4, "U_1ph", "U_1ph", new(300, 96)),
                new DiagramMember("bus-dc", new(56, 186), new(330, 186), 4, "U_DC", "U_DC", new(180, 172)),
                new DiagramPlate("load-1", 70, 58, 40, 28, "loads"),
                new DiagramPlate("load-2", 70, 122, 40, 28, "loads"),
                new DiagramPlate("load-3", 70, 194, 40, 26, "loads", "loads", new(170, 208)),
                new DiagramPlate("additional-load", 150, 58, 40, 28, "P_add", "P_add", new(170, 100)),
                new DiagramPlate("psu", 230, 140, 46, 30, "eta_psu", "η_PSU", new(290, 128)),
                new DiagramMember("psu-output", new(240, 170), new(240, 186), 3, "eta_psu"),
                new DiagramPlate("battery", 284, 150, 36, 28, "U_b", "U_b", new(354, 164)),
                new DiagramMember("battery-output", new(302, 178), new(302, 186), 3, "U_b"),
                new DiagramLabel("autonomy", new(348, 210), "t_auto", "t_auto"),
                new DiagramLabel("ambient", new(60, 234), "T_amb", "T_amb"),
                new DiagramLabel("safety-factor", new(150, 234), "SF", "SF"),
                new DiagramLabel("depth-of-discharge", new(230, 234), "DoD", "DoD"),
                new DiagramLabel("ageing", new(320, 234), "k_age", "k_age"),
            };

            if (feed is not null)
                elements.Add(feed);

            return new DiagramVariant(caption, When("psu_bus", psuBus), elements);
        }

        return new(LoadBudgetCalculator.Id,
        [
            Variant("Three buses with the loads as listed (one on each drawn): the DC power supply fed from the three-phase bus, the battery on the DC bus", LoadBudgetCalculator.Ac3, new DiagramMember("psu-feed", new(240, 46), new(240, 140), 3, "psu_bus")),
            Variant("Three buses with the loads as listed (one on each drawn): the DC power supply fed from the single-phase bus, the battery on the DC bus", LoadBudgetCalculator.Ac1, new DiagramMember("psu-feed", new(240, 110), new(240, 140), 3, "psu_bus")),
            Variant("Three buses with the loads as listed (one on each drawn): the DC power supply not counted on an AC bus, the battery on the DC bus", LoadBudgetCalculator.PsuNone, null),
        ]);
    }

    private static CalculationDiagramSpec MotorStart()
    {
        // The source, the starter, the cable run of length L_cab and section A_c, and the motor; the source data across the top.
        static DiagramVariant Variant(string caption, string starter, IReadOnlyList<DiagramElement> starterShapes)
        {
            var elements = new List<DiagramElement>
            {
                new DiagramLabel("transformer-impedance", new(50, 22), "u_k", "u_k"),
                new DiagramLabel("fault-level", new(150, 22), "S_k", "S_k"),
                new DiagramLabel("fault-current", new(250, 22), "I_k", "I_k"),
                new DiagramLabel("source-ratio", new(345, 22), "XR", "XR"),
                new DiagramCircle("source", new(54, 96), 22, Filled: false, "S_n", "S_n", new(54, 56)),
                new DiagramMember("source-lead", new(76, 96), new(104, 96), 3, "S_n"),
                new DiagramMember("conductor-1", new(156, 86), new(304, 86), 3, "A_c", "A_c", new(228, 70)),
                new DiagramMember("conductor-2", new(156, 96), new(304, 96), 3, "A_c"),
                new DiagramMember("conductor-3", new(156, 106), new(304, 106), 3, "A_c"),
                new DiagramCircle("motor", new(330, 96), 26, Filled: true, "P", "P", new(330, 54)),
                new DiagramDimension("cable-length", new(156, 106), new(304, 106), 30, "L_cab", "L_cab", new(216, 148)),
                new DiagramLabel("starting-power-factor", new(334, 150), "cos_phi_st", "cos φ_st"),
                new DiagramLabel("voltage", new(54, 196), "U", "U"),
                new DiagramLabel("efficiency", new(140, 196), "eta", "η"),
                new DiagramLabel("power-factor", new(230, 196), "cos_phi", "cos φ"),
                new DiagramLabel("locked-rotor", new(335, 196), "I_LR_r", "I_LR/I_n"),
                new DiagramLabel("conductor-temperature", new(54, 222), "T_c", "T_c"),
                new DiagramLabel("parallel", new(140, 222), "n_par", "n_par"),
                new DiagramLabel("dip-limit", new(240, 222), "dip_limit", "dip_limit"),
            };
            elements.AddRange(starterShapes);
            return new DiagramVariant(caption, When("starter", starter), elements);
        }

        return new(MotorStartCalculator.Id,
        [
            Variant("A motor started direct on line: the source, the contactor, the cable run L_cab of section A_c, and the motor P", MotorStartCalculator.StarterDol,
                [new DiagramPlate("starter", 104, 78, 52, 36, Symbol: "DOL", LabelAt: new(118, 132))]),
            Variant("A motor started star-delta: the source, the two contactors, the cable run L_cab of section A_c, and the motor P", MotorStartCalculator.StarterStarDelta,
                [new DiagramPlate("starter", 104, 78, 24, 36, Symbol: "Y–Δ", LabelAt: new(118, 132)), new DiagramPlate("starter-2", 132, 78, 24, 36)]),
            Variant("A motor on a soft starter limited to k_soft times its rated current: the source, the starter, the cable run L_cab of section A_c, and the motor P", MotorStartCalculator.StarterSoft,
                [new DiagramPlate("starter", 104, 78, 52, 36, "k_soft", "k_soft", new(118, 132))]),
            Variant("A motor on a variable-frequency drive limited to k_vfd times its rated current: the source, the drive, the cable run L_cab of section A_c, and the motor P", MotorStartCalculator.StarterVfd,
                [new DiagramPlate("starter", 104, 78, 52, 36, "k_vfd", "k_vfd", new(118, 132)), new DiagramLabel("drive-power-factor", new(100, 170), "cos_phi_vfd", "cos φ_vfd")]),
        ]);
    }

    private static CalculationDiagramSpec PcbTrace()
    {
        // The trace in plan (width w_act, length L, current I) and the board in section with the copper on the chosen layer.
        static DiagramVariant Variant(string caption, string layer, DiagramPlate copper)
        {
            List<DiagramElement> elements =
            [
                new DiagramPlate("trace", 70, 50, 240, 20, "w_act"),
                new DiagramDimension("width", new(70, 50), new(70, 70), -14, "w_act", "w_act", new(100, 34)),
                new DiagramDimension("length", new(70, 70), new(310, 70), 16, "L", "L"),
                new DiagramPointLoad("current", new(300, 36), DiagramDirection.Right, 44, "I", "I"),
                new DiagramLabel("section-note", new(295, 118), Symbol: "section"),
                new DiagramPlate("board", 230, 150, 130, 24),
                copper,
                new DiagramLabel("copper-thickness", new(295, 196), "t_oz", "t_oz"),
                new DiagramLabel("ambient", new(60, 172), "T_amb", "T_amb"),
                new DiagramLabel("rise", new(60, 196), "dT", "ΔT"),
                new DiagramLabel("exponent-rise", new(60, 220), "n_dT", "n_dT"),
                new DiagramLabel("constant-external", new(170, 172), "k_ext", "k_ext"),
                new DiagramLabel("constant-internal", new(170, 196), "k_int", "k_int"),
                new DiagramLabel("exponent-area", new(170, 220), "n_A", "n_A"),
            ];
            return new DiagramVariant(caption, When("layer", layer), elements);
        }

        return new(PcbTraceCalculator.Id,
        [
            Variant("A trace of width w_act and length L carrying I on an external layer: the copper on the board's face in section", PcbTraceCalculator.External,
                new DiagramPlate("copper", 270, 142, 50, 8, "Cu_oz", "Cu_oz", new(295, 134))),
            Variant("A trace of width w_act and length L carrying I on an internal layer: the copper inside the board in section", PcbTraceCalculator.Internal,
                new DiagramPlate("copper", 270, 158, 50, 8, "Cu_oz", "Cu_oz", new(295, 134))),
        ]);
    }
}
