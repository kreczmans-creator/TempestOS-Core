namespace Tempest.Core.Calculations.Modules.Diagrams;

/// <summary>
/// The reference diagram of every product calculation that has one
/// (`PO-2`, work packages A and D1): a declarative drawing per calculation,
/// every shape bound to the input it stands for, drawn by one Desktop
/// control and never to scale.
/// </summary>
/// <remarks>
/// <para>
/// <b>Inputs only.</b> A diagram shows what the engineer states — the
/// span, the load, the supports — and never a result: no deflected shape,
/// no stress marker. The Product Owner's own decision (2026-10-01).
/// </para>
/// <para>
/// <b>Kept honest by a test.</b> Every calculation in
/// <see cref="CalculationModuleDescriptors.All"/> either has a diagram here
/// or is named in <see cref="NoDiagramYet"/>, a list that may only shrink;
/// every input a shape is bound to, or a variant is selected by, is an
/// input its descriptor really has, and a choice condition names one of
/// the choice's own members.
/// </para>
/// </remarks>
public static class CalculationDiagrams
{
    private static readonly IReadOnlyDictionary<string, string> Always = new Dictionary<string, string>();

    /// <summary>
    /// The calculations drawn as "No diagram yet": the harder geometry
    /// (`PO-2` D2) and the calculations better shown as a chart (D3). This
    /// list only shrinks: a calculation leaves it when its diagram is added.
    /// </summary>
    public static IReadOnlyList<string> NoDiagramYet { get; } =
    [
        MaterialSelectionMarginCalculationDefinition.Id,
        BearingRatingLifeCalculationDefinition.Id,
        FatigueMinerCalculationDefinition.Id,
    ];

    /// <summary>Every diagram, one per calculation that has one.</summary>
    public static IReadOnlyList<CalculationDiagramSpec> All { get; } =
    [
        BoltShear(),
        BeamBendingStress(),
        BearingAtAHole(),
        PressureVessel(),
        BeamDeflection(),
        ColumnBuckling(),
        Shaft(),
        ThermalExpansion(),
        PlaneWall(),
        ThermalResistanceChain(),
        ToleranceStack(),
        BoltGroupEccentricShear(),
        FilletWeldThroatStress(),
        LiftingLugPinJoint(),
        ThickWalledCylinder(),
        BoltedJointPreload(),
    ];

    /// <summary>The diagram of <paramref name="calculationId"/>, or <see langword="null"/> where it has none yet.</summary>
    public static CalculationDiagramSpec? For(string calculationId) =>
        All.FirstOrDefault(d => string.Equals(d.CalculationId, calculationId, StringComparison.Ordinal));

    // ---- The diagrams (sheet 400 x 240, y downward, not to scale) ----

    private static CalculationDiagramSpec BoltShear()
    {
        static DiagramVariant Variant(string caption, IReadOnlyDictionary<string, string> when, bool doubleShear)
        {
            var top = doubleShear ? 74.0 : 86.0;
            var elements = new List<DiagramElement>
            {
                new DiagramPlate("plate-a", 40, top, 220, 24),
                new DiagramPlate("plate-b", 140, top + 24, 220, 24),
            };
            if (doubleShear)
                elements.Add(new DiagramPlate("plate-c", 40, top + 48, 220, 24));

            var plates = doubleShear ? 3 : 2;
            elements.AddRange(
            [
                new DiagramPlate("bolt", 190, top - 14, 20, 24 * plates + 28, "Diameter"),
                new DiagramDimension("bolt-diameter", new(190, top - 14), new(210, top - 14), -14, "Diameter", "d", new(200, top - 42)),
                new DiagramPointLoad("force-b", new(366, top + 36), DiagramDirection.Right, 30, Symbol: "F"),
                new DiagramPointLoad("force-a", new(34, top + 12), DiagramDirection.Left, 30, Symbol: "F"),
            ]);
            if (doubleShear)
                elements.Add(new DiagramPointLoad("force-c", new(34, top + 60), DiagramDirection.Left, 30));

            elements.AddRange(
            [
                new DiagramLabel("shear-planes", new(200, 186), "ShearPlanes", "n"),
                new DiagramLabel("shear-strength", new(110, 212), "UltimateShearStrength", "τ_u"),
                new DiagramLabel("safety-factor", new(290, 212), "SafetyFactor", "SF"),
            ]);
            return new DiagramVariant(caption, when, elements);
        }

        return new(BoltShearCapacityCalculationDefinition.Id,
        [
            Variant("Double shear: the bolt through three plates, two shear planes", new Dictionary<string, string> { ["ShearPlanes"] = "2" }, doubleShear: true),
            Variant("Single shear: the bolt through two plates, one shear plane", Always, doubleShear: false),
        ]);
    }

    private static CalculationDiagramSpec BeamBendingStress() =>
        new(BeamBendingStressCalculationDefinition.Id,
        [
            new DiagramVariant("Cantilever with a point load at the free end; rectangular section b by h", Always,
            [
                new DiagramSupport("wall", new(40, 110), DiagramSupportKind.Fixed, DiagramDirection.Left),
                new DiagramMember("beam", new(40, 110), new(250, 110), 10),
                new DiagramPointLoad("load", new(250, 104), DiagramDirection.Down, 44, "AppliedLoad", "P"),
                new DiagramDimension("length", new(40, 110), new(250, 110), 40, "CantileverLength", "L"),
                new DiagramLabel("section-note", new(315, 46), Symbol: "Section"),
                new DiagramPlate("section", 300, 60, 30, 60),
                new DiagramDimension("width", new(300, 120), new(330, 120), 16, "SectionWidth", "b"),
                new DiagramDimension("height", new(330, 60), new(330, 120), 12, "SectionHeight", "h", new(360, 90)),
                new DiagramLabel("allowable", new(145, 212), "AllowableBendingStress", "σ_allow"),
            ]),
        ]);

    private static CalculationDiagramSpec BearingAtAHole() =>
        new(BearingLoadCapacityCalculationDefinition.Id,
        [
            new DiagramVariant("A plate with a hole, front and side views", Always,
            [
                new DiagramLabel("front-note", new(160, 30), Symbol: "Front"),
                new DiagramPlate("plate", 60, 50, 200, 140),
                new DiagramCircle("hole", new(160, 120), 28, Filled: false, "HoleDiameter"),
                new DiagramDimension("hole-diameter", new(132, 120), new(188, 120), -48, "HoleDiameter", "d"),
                new DiagramLabel("side-note", new(320, 30), Symbol: "Side"),
                new DiagramPlate("plate-side", 310, 50, 20, 140, "PlateThickness"),
                new DiagramDimension("thickness", new(310, 190), new(330, 190), 12, "PlateThickness", "t"),
                new DiagramLabel("strength", new(110, 224), "BearingStrength", "F_br"),
                new DiagramLabel("safety-factor", new(225, 224), "SafetyFactor", "SF"),
            ]),
        ]);

    private static CalculationDiagramSpec PressureVessel() =>
        new(PressureVesselWallThicknessCalculationDefinition.Id,
        [
            new DiagramVariant("A thin-walled cylinder in section under internal pressure", Always,
            [
                new DiagramLabel("section-note", new(130, 20), Symbol: "Shell section"),
                new DiagramCircle("shell", new(130, 120), 84, Filled: false),
                new DiagramPointLoad("pressure-up", new(130, 52), DiagramDirection.Up, 34, "InternalPressure", "P", new(158, 70)),
                new DiagramPointLoad("pressure-left", new(62, 120), DiagramDirection.Left, 34, "InternalPressure"),
                new DiagramPointLoad("pressure-right", new(198, 120), DiagramDirection.Right, 34, "InternalPressure"),
                new DiagramDimension("radius", new(130, 120), new(130, 204), 0, "InnerRadius", "R", new(176, 166)),
                new DiagramLabel("allowable", new(310, 90), "AllowableStress", "S"),
                new DiagramLabel("joint-efficiency", new(310, 120), "JointEfficiency", "E"),
                new DiagramLabel("safety-factor", new(310, 150), "SafetyFactor", "SF"),
            ]),
        ]);

    private static CalculationDiagramSpec BeamDeflection()
    {
        const double Left = 50, Right = 330, Y = 100;

        static DiagramVariant Variant(string caption, string support, string loading)
        {
            var elements = new List<DiagramElement>();
            if (support == nameof(BeamSupport.Cantilever))
            {
                elements.Add(new DiagramSupport("support-fixed", new(Left, Y), DiagramSupportKind.Fixed, DiagramDirection.Left, "Support"));
            }
            else
            {
                elements.Add(new DiagramSupport("support-pin", new(Left, Y + 6), DiagramSupportKind.Pinned, DiagramDirection.Down, "Support"));
                elements.Add(new DiagramSupport("support-roller", new(Right, Y + 6), DiagramSupportKind.Roller, DiagramDirection.Down, "Support"));
            }

            elements.Add(new DiagramMember("beam", new(Left, Y), new(Right, Y), 10));

            if (loading == nameof(BeamLoading.PointLoad))
            {
                var x = support == nameof(BeamSupport.Cantilever) ? Right : (Left + Right) / 2;
                elements.Add(new DiagramPointLoad("load", new(x, Y - 6), DiagramDirection.Down, 44, "Load", "W"));
            }
            else
            {
                elements.Add(new DiagramDistributedLoad("load", Left, Right, Y - 6, 30, "Load", "W (total)"));
            }

            elements.AddRange(
            [
                new DiagramDimension("span", new(Left, Y), new(Right, Y), 50, "Span", "L"),
                new DiagramLabel("modulus", new(80, 196), "YoungsModulus", "E"),
                new DiagramLabel("second-moment", new(200, 196), "SecondMomentOfArea", "I"),
                new DiagramLabel("fibre", new(320, 196), "ExtremeFibreDistance", "c"),
                new DiagramLabel("allowable", new(120, 222), "AllowableBendingStress", "σ_allow"),
                new DiagramLabel("deflection-limit", new(290, 222), "DeflectionLimit", "δ_limit"),
            ]);

            return new DiagramVariant(caption, new Dictionary<string, string> { ["Support"] = support, ["Loading"] = loading }, elements);
        }

        return new(BeamDeflectionCalculationDefinition.Id,
        [
            Variant("Simply supported, point load at mid-span", nameof(BeamSupport.SimplySupported), nameof(BeamLoading.PointLoad)),
            Variant("Simply supported, uniformly distributed load", nameof(BeamSupport.SimplySupported), nameof(BeamLoading.UniformlyDistributed)),
            Variant("Cantilever, point load at the free end", nameof(BeamSupport.Cantilever), nameof(BeamLoading.PointLoad)),
            Variant("Cantilever, uniformly distributed load", nameof(BeamSupport.Cantilever), nameof(BeamLoading.UniformlyDistributed)),
        ]);
    }

    private static CalculationDiagramSpec ColumnBuckling() =>
        new(ColumnBucklingCalculationDefinition.Id,
        [
            new DiagramVariant("A pin-ended strut under axial compression, effective length L_E", Always,
            [
                new DiagramMember("column", new(200, 62), new(200, 196), 10),
                new DiagramSupport("base", new(200, 202), DiagramSupportKind.Pinned, DiagramDirection.Down),
                new DiagramSupport("head", new(206, 62), DiagramSupportKind.Roller, DiagramDirection.Right),
                new DiagramPointLoad("load", new(200, 56), DiagramDirection.Down, 34, "AppliedLoad", "P"),
                new DiagramDimension("effective-length", new(200, 62), new(200, 196), -60, "EffectiveLength", "L_E", new(90, 129)),
                new DiagramLabel("area", new(320, 80), "Area", "A"),
                new DiagramLabel("second-moment", new(320, 104), "SecondMomentOfArea", "I"),
                new DiagramLabel("modulus", new(320, 128), "YoungsModulus", "E"),
                new DiagramLabel("yield", new(320, 152), "YieldStrength", "p_y"),
                new DiagramLabel("robertson", new(320, 176), "RobertsonConstant", "a"),
            ]),
        ]);

    private static CalculationDiagramSpec Shaft() =>
        new(ShaftCombinedStressCalculationDefinition.Id,
        [
            new DiagramVariant("A solid round shaft under bending and torsion at the section", Always,
            [
                new DiagramMember("shaft", new(60, 110), new(340, 110), 24, "Diameter"),
                new DiagramDimension("diameter", new(60, 98), new(60, 122), -24, "Diameter", "d", new(60, 146)),
                new DiagramMoment("bending", new(140, 110), 30, Clockwise: true, "BendingMoment", "M", new(140, 64)),
                new DiagramMoment("torque", new(270, 110), 30, Clockwise: false, "Torque", "T", new(270, 64)),
                new DiagramLabel("yield", new(90, 196), "YieldStrength", "S_y"),
                new DiagramLabel("kt", new(205, 196), "BendingStressConcentrationFactor", "K_t"),
                new DiagramLabel("kts", new(315, 196), "TorsionalStressConcentrationFactor", "K_ts"),
                new DiagramLabel("required-factor", new(200, 222), "RequiredSafetyFactor", "n_req"),
            ]),
        ]);

    private static CalculationDiagramSpec ThermalExpansion() =>
        new(ThermalExpansionStressCalculationDefinition.Id,
        [
            new DiagramVariant("A bar fixed at one end, a gap, then an elastic restraint", Always,
            [
                new DiagramSupport("wall", new(40, 110), DiagramSupportKind.Fixed, DiagramDirection.Left),
                new DiagramMember("bar", new(40, 110), new(260, 110), 14, "Area", "A", new(150, 86)),
                new DiagramLabel("temperature", new(150, 50), "TemperatureChange", "ΔT"),
                new DiagramDimension("length", new(40, 110), new(260, 110), 40, "Length", "L"),
                new DiagramDimension("gap", new(260, 110), new(290, 110), 40, "Gap", "g", new(285, 162)),
                new DiagramSpring("restraint", new(290, 110), new(350, 110), "RestraintStiffness", "k_s", new(312, 76)),
                new DiagramSupport("restraint-wall", new(350, 110), DiagramSupportKind.Fixed, DiagramDirection.Right),
                new DiagramLabel("modulus", new(75, 206), "YoungsModulus", "E"),
                new DiagramLabel("expansion", new(200, 206), "ExpansionCoefficient", "α"),
                new DiagramLabel("allowable", new(325, 206), "AllowableStress", "σ_allow"),
            ]),
        ]);

    private static CalculationDiagramSpec PlaneWall() =>
        new(PlaneWallHeatTransferCalculationDefinition.Id,
        [
            new DiagramVariant("A composite plane wall, hot side left: a fluid film, the layers as listed (three drawn), a film on the cold side", Always,
            [
                new DiagramLabel("layers-note", new(200, 22), Symbol: "t, k per layer"),
                new DiagramDimension("thickness", new(150, 50), new(250, 50), -14, "Layers"),
                new DiagramPlate("layer-1", 150, 50, 34, 130, "Layers", "Layers", new(200, 198)),
                new DiagramPlate("layer-2", 184, 50, 33, 130, "Layers"),
                new DiagramPlate("layer-3", 217, 50, 33, 130, "Layers"),
                new DiagramLabel("hot-temperature", new(75, 70), "HotSideTemperature", "T_1"),
                new DiagramLabel("hot-film", new(75, 104), "HotSideFilmCoefficient", "h_1"),
                new DiagramPointLoad("hot-convection", new(146, 140), DiagramDirection.Right, 40, "HotSideFilmCoefficient"),
                new DiagramLabel("cold-temperature", new(325, 70), "ColdSideTemperature", "T_2"),
                new DiagramLabel("cold-film", new(325, 104), "ColdSideFilmCoefficient", "h_2"),
                new DiagramPointLoad("cold-convection", new(294, 140), DiagramDirection.Right, 40, "ColdSideFilmCoefficient"),
                new DiagramLabel("area", new(200, 222), "Area", "A"),
            ]),
        ]);

    private static CalculationDiagramSpec ThermalResistanceChain() =>
        new(ThermalResistanceChainCalculationDefinition.Id,
        [
            new DiagramVariant("Thermal resistances in series from the source to ambient, hottest first (three drawn; the stages as listed)", Always,
            [
                new DiagramPointLoad("power", new(60, 94), DiagramDirection.Down, 40, "PowerDissipation", "P"),
                new DiagramSpring("stage-1", new(60, 100), new(140, 100), "Stages"),
                new DiagramSpring("stage-2", new(140, 100), new(220, 100), "Stages", "R stages", new(180, 70)),
                new DiagramSpring("stage-3", new(220, 100), new(300, 100), "Stages"),
                new DiagramCircle("source", new(60, 100), 6, Filled: true, "MaximumSourceTemperature", "T_max", new(60, 128)),
                new DiagramCircle("node-1", new(140, 100), 4, Filled: true),
                new DiagramCircle("node-2", new(220, 100), 4, Filled: true),
                new DiagramCircle("ambient", new(300, 100), 6, Filled: true, "AmbientTemperature", "T_a", new(300, 128)),
                new DiagramLabel("source-note", new(60, 152), Symbol: "source"),
                new DiagramLabel("ambient-note", new(300, 152), Symbol: "ambient"),
            ]),
        ]);

    private static CalculationDiagramSpec ToleranceStack() =>
        new(ToleranceStackCalculationDefinition.Id,
        [
            new DiagramVariant("A linear stack (representative; the contributors as listed): the bore adds, the parts subtract, the gap closes the loop", Always,
            [
                new DiagramPlate("base", 40, 140, 300, 16),
                new DiagramPlate("wall-left", 40, 70, 20, 70),
                new DiagramPlate("wall-right", 320, 70, 20, 70),
                new DiagramPlate("part-a", 60, 100, 120, 40, "Contributors"),
                new DiagramPlate("part-b", 180, 100, 110, 40, "Contributors"),
                new DiagramDimension("bore", new(60, 70), new(320, 70), -16, "Contributors"),
                new DiagramLabel("bore-note", new(190, 40), Symbol: "+ bore ± t"),
                new DiagramDimension("part-a-length", new(60, 100), new(180, 100), -12, "Contributors"),
                new DiagramLabel("part-a-note", new(120, 76), Symbol: "− A ± t"),
                new DiagramDimension("part-b-length", new(180, 100), new(290, 100), -12, "Contributors"),
                new DiagramLabel("part-b-note", new(235, 76), Symbol: "− B ± t"),
                new DiagramDimension("gap", new(290, 120), new(320, 120), 0, "MinimumResult"),
                new DiagramLabel("gap-note", new(305, 170), Symbol: "gap"),
                new DiagramLabel("contributors", new(110, 192), "Contributors", "Contributors"),
                new DiagramLabel("sigma", new(110, 216), "SigmaPerTolerance", "σ per ½ tol"),
                new DiagramLabel("minimum", new(290, 192), "MinimumResult", "min gap"),
                new DiagramLabel("maximum", new(290, 216), "MaximumResult", "max gap"),
            ]),
        ]);

    private static CalculationDiagramSpec BoltGroupEccentricShear() =>
        new(BoltGroupEccentricShearCalculationDefinition.Id,
        [
            new DiagramVariant("A bolt group (six drawn; the bolts as listed) under an in-plane load at the load point, x and y from one origin, + as drawn", Always,
            [
                new DiagramMember("x-axis", new(40, 200), new(372, 200), 1),
                new DiagramLabel("x-note", new(384, 200), Symbol: "x"),
                new DiagramMember("y-axis", new(40, 200), new(40, 40), 1),
                new DiagramLabel("y-note", new(40, 30), Symbol: "y"),
                new DiagramPlate("plate", 70, 40, 130, 132),
                new DiagramCircle("bolt-1", new(100, 62), 8, Filled: true, "Bolts"),
                new DiagramCircle("bolt-2", new(170, 62), 8, Filled: true, "Bolts"),
                new DiagramCircle("bolt-3", new(100, 106), 8, Filled: true, "Bolts"),
                new DiagramCircle("bolt-4", new(170, 106), 8, Filled: true, "Bolts", "Bolts", new(135, 26)),
                new DiagramCircle("bolt-5", new(100, 150), 8, Filled: true, "Bolts"),
                new DiagramCircle("bolt-6", new(170, 150), 8, Filled: true, "Bolts"),
                new DiagramCircle("centroid", new(135, 106), 3, Filled: true, Symbol: "c.g.", LabelAt: new(135, 124)),
                new DiagramDimension("load-point-y", new(320, 200), new(320, 80), 0, "LoadPointY", "y_P", new(268, 150)),
                new DiagramDimension("load-point-x", new(40, 200), new(320, 200), 18, "LoadPointX", "x_P"),
                new DiagramCircle("load-point", new(320, 80), 4, Filled: true, "LoadPointX"),
                new DiagramPointLoad("load-y", new(320, 36), DiagramDirection.Up, 44, "LoadY", "F_y", new(320, 24)),
                new DiagramPointLoad("load-x", new(372, 80), DiagramDirection.Right, 52, "LoadX", "F_x", new(362, 98)),
                new DiagramLabel("allowable", new(135, 188), "AllowableShearPerBolt", "V_allow"),
            ]),
        ]);

    private static CalculationDiagramSpec FilletWeldThroatStress() =>
        new(FilletWeldThroatStressCalculationDefinition.Id,
        [
            new DiagramVariant("A fillet weld of effective length L: in plan, the force along it; in section, the throat a and the forces across it and normal to the plate", Always,
            [
                new DiagramLabel("plan-note", new(110, 22), Symbol: "Plan"),
                new DiagramPlate("plan-plate", 30, 40, 160, 80),
                new DiagramPlate("plan-stem", 30, 72, 160, 10),
                new DiagramMember("weld", new(40, 88), new(180, 88), 8, "EffectiveLength"),
                new DiagramDimension("length", new(40, 92), new(180, 92), 42, "EffectiveLength", "L"),
                new DiagramPointLoad("parallel", new(180, 168), DiagramDirection.Right, 80, "ParallelForce", "F_∥", new(110, 184)),
                new DiagramLabel("section-note", new(345, 40), Symbol: "Section"),
                new DiagramPlate("base", 222, 150, 166, 16),
                new DiagramPlate("stem", 270, 50, 16, 100),
                new DiagramMember("weld-leg-up", new(286, 110), new(286, 150), 2, "ThroatThickness"),
                new DiagramMember("weld-leg-along", new(286, 150), new(326, 150), 2, "ThroatThickness"),
                new DiagramMember("weld-face", new(286, 110), new(326, 150), 2, "ThroatThickness"),
                new DiagramDimension("throat", new(286, 150), new(306, 130), 0, "ThroatThickness", "a", new(352, 134)),
                new DiagramPointLoad("normal", new(278, 16), DiagramDirection.Up, 34, "NormalForce", "F_n", new(222, 28)),
                new DiagramPointLoad("transverse", new(336, 76), DiagramDirection.Right, 50, "TransverseForce", "F_⊥", new(340, 92)),
                new DiagramLabel("ultimate", new(80, 214), "UltimateStrength", "f_u"),
                new DiagramLabel("correlation", new(200, 214), "CorrelationFactor", "β_w"),
                new DiagramLabel("partial", new(320, 214), "PartialFactor", "γ_M2"),
            ]),
        ]);

    private static CalculationDiagramSpec LiftingLugPinJoint() =>
        new(LiftingLugPinJointCalculationDefinition.Id,
        [
            new DiagramVariant("A lug and its pin, front view and section through the pin: the lug between two cheek plates, the load through the pin", Always,
            [
                new DiagramPlate("lug", 55, 44, 90, 120, "LugWidth"),
                new DiagramCircle("hole", new(100, 86), 20, Filled: false, "HoleDiameter"),
                new DiagramCircle("pin", new(100, 86), 17, Filled: true, "PinDiameter"),
                new DiagramPointLoad("load", new(100, 12), DiagramDirection.Up, 30, "Load", "P", new(48, 24)),
                new DiagramDimension("edge-distance", new(100, 44), new(100, 66), 60, "EdgeDistance", "a"),
                new DiagramDimension("hole-diameter", new(80, 86), new(120, 86), 30, "HoleDiameter", "d_h"),
                new DiagramDimension("width", new(55, 164), new(145, 164), 12, "LugWidth", "W"),
                new DiagramPlate("cheek-left", 250, 30, 10, 80, "CheekPlateThickness"),
                new DiagramPlate("cheek-right", 306, 30, 10, 80, "CheekPlateThickness"),
                new DiagramPlate("lug-section", 270, 64, 26, 100, "LugThickness"),
                new DiagramMember("pin-section", new(240, 86), new(326, 86), 16, "PinDiameter"),
                new DiagramDimension("thickness", new(270, 164), new(296, 164), 12, "LugThickness", "t"),
                new DiagramDimension("cheek-thickness", new(306, 30), new(316, 30), -10, "CheekPlateThickness", "t_s", new(360, 20)),
                new DiagramDimension("clearance", new(260, 110), new(270, 110), 14, "Clearance", "g", new(226, 124)),
                new DiagramDimension("pin-diameter", new(326, 78), new(326, 94), 14, "PinDiameter", "d_p", new(358, 110)),
                new DiagramLabel("allowable-tension", new(70, 210), "AllowableTensileStress", "S_t"),
                new DiagramLabel("allowable-bearing", new(200, 210), "AllowableBearingStress", "S_br"),
                new DiagramLabel("allowable-shear", new(330, 210), "AllowableShearStress", "S_v"),
                new DiagramLabel("pin-allowable-bending", new(120, 228), "PinAllowableBendingStress", "S_b,pin"),
                new DiagramLabel("pin-allowable-shear", new(280, 228), "PinAllowableShearStress", "S_v,pin"),
            ]),
        ]);

    private static CalculationDiagramSpec ThickWalledCylinder()
    {
        static DiagramVariant Variant(string caption, IReadOnlyDictionary<string, string> when, bool closedEnds)
        {
            var elements = new List<DiagramElement>
            {
                new DiagramCircle("wall", new(130, 120), 90, Filled: true, "OuterRadius"),
                new DiagramCircle("bore", new(130, 120), 56, Filled: false, "InnerRadius"),
                new DiagramPointLoad("internal-up", new(130, 64), DiagramDirection.Up, 24, "InternalPressure", "p_i", new(130, 53)),
                new DiagramPointLoad("internal-left", new(74, 120), DiagramDirection.Left, 24, "InternalPressure"),
                new DiagramPointLoad("external-top", new(130, 30), DiagramDirection.Down, 26, "ExternalPressure", "p_o", new(205, 20)),
                new DiagramPointLoad("external-left", new(40, 120), DiagramDirection.Right, 28, "ExternalPressure"),
                new DiagramPointLoad("external-right", new(220, 120), DiagramDirection.Left, 28, "ExternalPressure"),
                new DiagramDimension("inner-radius", new(130, 120), new(186, 120), 0, "InnerRadius", "a", new(130, 146)),
                new DiagramDimension("outer-radius", new(130, 120), new(66.4, 56.4), 0, "OuterRadius", "b", new(40, 36)),
                new DiagramLabel("side-note", new(330, 50), Symbol: "Side"),
                new DiagramPlate("side-wall-top", 290, 70, 80, 12),
                new DiagramPlate("side-wall-bottom", 290, 158, 80, 12),
            };
            if (closedEnds)
            {
                elements.Add(new DiagramPlate("end-left", 282, 70, 8, 100, "ClosedEnds"));
                elements.Add(new DiagramPlate("end-right", 370, 70, 8, 100, "ClosedEnds"));
            }

            elements.AddRange(
            [
                new DiagramLabel("closed-ends", new(330, 196), "ClosedEnds", "closed ends"),
                new DiagramLabel("allowable", new(330, 222), "AllowableStress", "S"),
            ]);
            return new DiagramVariant(caption, when, elements);
        }

        return new(ThickWalledCylinderCalculationDefinition.Id,
        [
            Variant("A thick-walled cylinder in section, bore radius a, outer radius b, pressure inside and out; closed ends carry the pressures axially", new Dictionary<string, string> { ["ClosedEnds"] = CalculationDiagramReader.BooleanTrue }, closedEnds: true),
            Variant("A thick-walled cylinder in section, bore radius a, outer radius b, pressure inside and out; open ends, no axial load", Always, closedEnds: false),
        ]);
    }

    private static CalculationDiagramSpec BoltedJointPreload() =>
        new(BoltedJointPreloadCalculationDefinition.Id,
        [
            new DiagramVariant("A preloaded bolt clamping two members, the external load pulling them apart; beside it, bolt and members as two springs in parallel", Always,
            [
                new DiagramPlate("member-top", 30, 80, 160, 30, "MemberStiffness"),
                new DiagramPlate("member-bottom", 30, 110, 160, 30, "MemberStiffness"),
                new DiagramPlate("shank", 100, 66, 20, 96, "BoltStiffness"),
                new DiagramPlate("head", 86, 66, 48, 14),
                new DiagramPlate("nut", 86, 140, 48, 14),
                new DiagramPointLoad("preload-head", new(110, 66), DiagramDirection.Down, 26, "Preload", "F_i", new(110, 28)),
                new DiagramPointLoad("preload-nut", new(110, 162), DiagramDirection.Up, 26, "Preload"),
                new DiagramPointLoad("external-top-left", new(45, 50), DiagramDirection.Up, 30, "ExternalLoad"),
                new DiagramPointLoad("external-bottom-left", new(45, 170), DiagramDirection.Down, 30, "ExternalLoad", "P", new(45, 184)),
                new DiagramPointLoad("external-top-right", new(175, 50), DiagramDirection.Up, 30, "ExternalLoad"),
                new DiagramPointLoad("external-bottom-right", new(175, 170), DiagramDirection.Down, 30, "ExternalLoad"),
                new DiagramMember("model-top", new(232, 70), new(372, 70), 4),
                new DiagramMember("model-bottom", new(232, 160), new(372, 160), 4),
                new DiagramSpring("bolt-spring", new(256, 70), new(256, 160), "BoltStiffness", "k_b", new(262, 52)),
                new DiagramSpring("member-spring", new(346, 70), new(346, 160), "MemberStiffness", "k_m", new(338, 180)),
                new DiagramLabel("stress-area", new(110, 214), "TensileStressArea", "A_t"),
                new DiagramLabel("proof-strength", new(290, 214), "ProofStrength", "S_p"),
            ]),
        ]);
}
