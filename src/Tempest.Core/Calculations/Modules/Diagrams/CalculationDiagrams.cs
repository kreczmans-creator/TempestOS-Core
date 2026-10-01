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
        BoltedJointPreloadCalculationDefinition.Id,
        BoltGroupEccentricShearCalculationDefinition.Id,
        FilletWeldThroatStressCalculationDefinition.Id,
        LiftingLugPinJointCalculationDefinition.Id,
        BearingRatingLifeCalculationDefinition.Id,
        ThickWalledCylinderCalculationDefinition.Id,
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
}
