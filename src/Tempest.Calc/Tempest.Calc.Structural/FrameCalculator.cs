using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural.Frame;

namespace Tempest.Calc.Structural;

/// <summary>
/// S2 — beam and 2D frame analysis by the direct stiffness method:
/// Euler–Bernoulli members with end releases, pin / roller / fixed /
/// spring supports, point, distributed (uniform, trapezoidal), couple
/// and nodal loads in cases and user combinations. Reactions, member
/// end forces, N/V/M and deflection along every member with extremes,
/// and an envelope over the combinations.
/// </summary>
public sealed class FrameCalculator : ICalcCalculator
{
    public const string Id = "struct.frame-2d";

    public const string SupportFree = "free";
    public const string SupportPin = "pin";
    public const string SupportRollerY = "roller (vertical)";
    public const string SupportRollerX = "roller (horizontal)";
    public const string SupportFixed = "fixed";
    public const string SupportSpring = "spring";

    private const int MaxDetailedMembers = 50;
    private const int MaxDetailedSupports = 50;

    private static readonly IReadOnlyList<CalcColumnSpec> NodeColumns =
    [
        new("id", "Node", CalcInputKind.Number, HardMin: 1, HardMax: 1e6),
        new("x", "x", CalcInputKind.Quantity, "m", HardMin: -1e5, HardMax: 1e5),
        new("y", "y", CalcInputKind.Quantity, "m", HardMin: -1e5, HardMax: 1e5),
        new("support", "Support", CalcInputKind.Choice, Choices: [SupportFree, SupportPin, SupportRollerY, SupportRollerX, SupportFixed, SupportSpring], IsOptional: true),
        new("k_x", "Spring k_x", CalcInputKind.Quantity, "kN/m", HardMin: 0, HardMax: 1e12, IsOptional: true),
        new("k_y", "Spring k_y", CalcInputKind.Quantity, "kN/m", HardMin: 0, HardMax: 1e12, IsOptional: true),
        new("k_r", "Spring k_r (per radian)", CalcInputKind.Quantity, "kN*m", HardMin: 0, HardMax: 1e12, IsOptional: true),
    ];

    private static readonly IReadOnlyList<CalcColumnSpec> ElementColumns =
    [
        new("id", "Element", CalcInputKind.Number, HardMin: 1, HardMax: 1e6),
        new("i", "Node i", CalcInputKind.Number, HardMin: 1, HardMax: 1e6),
        new("j", "Node j", CalcInputKind.Number, HardMin: 1, HardMax: 1e6),
        new("A", "Area A", CalcInputKind.Quantity, "cm^2", HardMin: 1e-6, HardMax: 1e6),
        new("I", "Second moment of area about the section y-y axis", CalcInputKind.Quantity, "cm^4", HardMin: 1e-9, HardMax: 1e12),
        new("E", "Young's modulus (blank = sheet default)", CalcInputKind.Quantity, "GPa", HardMin: 1e-6, HardMax: 1e4, IsOptional: true),
        new("rel_i", "Moment release at i", CalcInputKind.Boolean, IsOptional: true),
        new("rel_j", "Moment release at j", CalcInputKind.Boolean, IsOptional: true),
    ];

    private static readonly IReadOnlyList<CalcColumnSpec> LoadColumns =
    [
        new("case", "Load case", CalcInputKind.Number, HardMin: 1, HardMax: 1e6),
        new("kind", "Kind", CalcInputKind.Choice, Choices: ["nodal", "point", "udl", "trapezoid", "moment"]),
        new("target", "Node (nodal) or element", CalcInputKind.Number, HardMin: 1, HardMax: 1e6),
        new("a", "a from end i", CalcInputKind.Quantity, "m", HardMin: 0, HardMax: 1e5, IsOptional: true),
        new("b", "b from end i (distributed)", CalcInputKind.Quantity, "m", HardMin: 0, HardMax: 1e5, IsOptional: true),
        new("w1", "w at a (+ towards local y, i.e. left of i→j; gravity rows: + downward)", CalcInputKind.Quantity, "kN/m", HardMin: -1e6, HardMax: 1e6, IsOptional: true),
        new("w2", "w at b (trapezoid)", CalcInputKind.Quantity, "kN/m", HardMin: -1e6, HardMax: 1e6, IsOptional: true),
        new("F_x", "F_x (global)", CalcInputKind.Quantity, "kN", HardMin: -1e7, HardMax: 1e7, IsOptional: true),
        new("F_y", "F_y (global)", CalcInputKind.Quantity, "kN", HardMin: -1e7, HardMax: 1e7, IsOptional: true),
        new("M", "Couple M (anticlockwise)", CalcInputKind.Quantity, "kN*m", HardMin: -1e8, HardMax: 1e8, IsOptional: true),
        new("gravity", "Gravity load: w positive downward, per metre of member", CalcInputKind.Boolean, IsOptional: true),
    ];

    private static readonly IReadOnlyList<CalcColumnSpec> CombinationColumns =
    [
        new("name", "Combination", CalcInputKind.Text),
        new("case", "Load case", CalcInputKind.Number, HardMin: 1, HardMax: 1e6),
        new("factor", "Factor", CalcInputKind.Number, HardMin: -100, HardMax: 100),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.6.0", CalcDiscipline.Structural, "Beam and 2D frame analysis",
        "Calculates reactions, member forces and deflections of a linear elastic plane frame or continuous beam by the direct stiffness method. Global x is right and y up; bending is about the section y-y axis, anticlockwise positive.",
        "Member stiffness: three freedoms per node, Euler–Bernoulli members with axial EA/L and bending EI/L³ terms.\nEnd releases: pins are taken out by static condensation; supports remove freedoms or add spring stiffness.\nSpan loads: they become equivalent nodal loads, the exact fixed-end forces.\nNodal displacements: K u = F, solved by banded LDLᵀ after a reverse Cuthill–McKee ordering; a non-positive pivot is reported as a mechanism.\nInternal forces: they follow from the member end actions and the Macaulay functions of the span loads.\nMember extremes: found at stations along each member, plus the zero-shear and zero-slope points found by bisection.",
        [
            new CalcInputSpec("nodes", "Nodes", CalcInputKind.Table, Columns: NodeColumns, MaxRows: FrameLimits.MaxNodes, Group: "Setup"),
            new CalcInputSpec("elements", "Elements", CalcInputKind.Table, Columns: ElementColumns, MaxRows: FrameLimits.MaxElements, Group: "Setup"),
            new CalcInputSpec("loads", "Loads", CalcInputKind.Table, Columns: LoadColumns, MaxRows: FrameLimits.MaxLoads, Group: "Setup"),
            new CalcInputSpec("n_div", "Stations per member", CalcInputKind.Number, Default: 20, Min: 4, Max: FrameLimits.MaxStations, HardMin: 2, HardMax: FrameLimits.MaxStations, Group: "Setup"),
            new CalcInputSpec("combinations", "Load combinations (optional; blank = each case alone)", CalcInputKind.Table, Columns: CombinationColumns, IsOptional: true, MaxRows: 1000, Group: "Setup"),
            new CalcInputSpec("report", "Combination to report in detail (optional; blank = the first)", CalcInputKind.Text, IsOptional: true, Group: "Setup"),
            new CalcInputSpec("E", "Default Young's modulus", CalcInputKind.Quantity, "GPa", ReferenceLibrary: "Materials", ReferenceProperty: "YoungsModulus", ReferenceDimension: "Pressure", Default: 210, Min: 1, Max: 500, HardMin: 1e-3, HardMax: 1e4, Group: "Material"),
        ],
        [
            new CalcAssumption("The analysis is linear elastic and first order, with small displacements.", "Euler–Bernoulli direct stiffness."),
            new CalcAssumption("Members have no shear deformation, and there are no P–Δ effects or buckling.", "Euler–Bernoulli direct stiffness."),
            new CalcAssumption("Members are prismatic between nodes and connect rigidly unless a release is stated.", "Static condensation of the released rotation."),
            new CalcAssumption("A released member end transmits no moment.", "Static condensation of the released rotation."),
            new CalcAssumption("Distributed loads act perpendicular to the member unless marked as gravity loads.", "Load table convention."),
            new CalcAssumption("Gravity loads act in global −y per metre of member length.", "Load table convention."),
        ],
        [
            new CalcClauseRef("Hibbeler, Structural Analysis", "Ch. 16, 9th ed.", "Plane frame analysis using the stiffness method"),
            new CalcClauseRef("Young and Budynas, Roark's Formulas for Stress and Strain", "Table 8.1, 8th ed.", "Shear, moment, slope and deflection formulas for elastic straight beams"),
            new CalcClauseRef("BS EN 1990:2002+A1:2005", "cl. 6.4.3", "Combinations of actions (user-entered factors)"),
        ],
        [
            new CalcApplicabilityRule("model", "The model is incomplete or inconsistent (a missing node, a zero-length member, a load off the member).", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("report-name", "The combination named in report does not exist: the first combination is reported instead.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("mechanism", "The structure is a mechanism: a non-positive stiffness pivot.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("size", "The model exceeds a cap (1,200 nodes, 2,400 elements, 2,400 load rows, 50 cases, 200 load rows per member, factorisation work); a table past its row cap is refused at entry.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("summary-only", "More than 50 members or supports: per-member and per-support lines are omitted from the sheet; the extremes and envelope remain.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("large-deflection", "A deflection exceeds 1/50 of its member length: the small-displacement assumption is doubtful.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
        ],
        [
            new CalcTestCase("ka-ss-udl", "Simply supported beam, UDL (Roark Table 8.1 case 2e)",
                new CalcReferenceSource("Roark's Formulas for Stress and Strain", "8th ed.", "Table 8.1, simple supports, uniform load: R = wL/2, M_max = wL²/8 at midspan, δ_max = 5wL⁴/(384EI)"),
                [CalcInputValue.Quantity("E", 210, "GPa"), Nodes((1, 0, 0, SupportPin), (2, 6, 0, SupportRollerY)), Elements((1, 1, 2, 100, 8000)), Loads((1, "udl", 1, 0, 6, -10, 0, 0, 0, 0)), CalcInputValue.Of("n_div", 20)],
                [new CalcExpectedValue("R1_y", 30, "kN", 1e-9), new CalcExpectedValue("R2_y", 30, "kN", 1e-9), new CalcExpectedValue("M_max", 45, "kN*m", 1e-9), new CalcExpectedValue("x_Mmax", 3, "m", 1e-6), new CalcExpectedValue("V_max", 30, "kN", 1e-9), new CalcExpectedValue("d_max", 10.0446, "mm", 1e-4)]),
            new CalcTestCase("ka-cantilever-point", "Cantilever, point load at the tip (Roark Table 8.1 case 1a)",
                new CalcReferenceSource("Roark's Formulas for Stress and Strain", "8th ed.", "Table 8.1, cantilever, end load: R = P, M_fixed = −PL, δ_tip = PL³/(3EI)"),
                [CalcInputValue.Quantity("E", 200, "GPa"), Nodes((1, 0, 0, SupportFixed), (2, 3, 0, SupportFree)), Elements((1, 1, 2, 50, 2000)), Loads((1, "nodal", 2, 0, 0, 0, 0, 0, -20, 0)), CalcInputValue.Of("n_div", 10)],
                [new CalcExpectedValue("R1_y", 20, "kN", 1e-9), new CalcExpectedValue("R1_m", 60, "kN*m", 1e-9), new CalcExpectedValue("M_min", -60, "kN*m", 1e-9), new CalcExpectedValue("d_max", 45, "mm", 1e-9)]),
            new CalcTestCase("ka-propped-cantilever", "Propped cantilever, UDL (Roark Table 8.1 case 2c)",
                new CalcReferenceSource("Roark's Formulas for Stress and Strain", "8th ed.", "Table 8.1, fixed–simple, uniform load: R_simple = 3wL/8, M_fixed = −wL²/8, M_max,sag = 9wL²/128 at 5L/8 from the fixed end... (sag at 3L/8 from the prop)"),
                [CalcInputValue.Quantity("E", 210, "GPa"), Nodes((1, 0, 0, SupportFixed), (2, 8, 0, SupportRollerY)), Elements((1, 1, 2, 100, 20000)), Loads((1, "udl", 1, 0, 8, -12, 0, 0, 0, 0)), CalcInputValue.Of("n_div", 16)],
                [new CalcExpectedValue("R2_y", 36, "kN", 1e-9), new CalcExpectedValue("R1_y", 60, "kN", 1e-9), new CalcExpectedValue("M_min", -96, "kN*m", 1e-9), new CalcExpectedValue("M_max", 54, "kN*m", 1e-9), new CalcExpectedValue("x_Mmax", 5, "m", 1e-6)]),
            new CalcTestCase("ka-portal-sway", "Pinned-base portal frame with a lateral load (Hibbeler, portal frame symmetry)",
                new CalcReferenceSource("Hibbeler, Structural Analysis", "9th ed.", "Portal frames with pinned supports, equal columns and an axially stiff beam: each column carries half the lateral load, column-top moment P h/2, vertical reactions ±P h / L (beam area set large here to make it axially rigid)"),
                [CalcInputValue.Quantity("E", 210, "GPa"), Nodes((1, 0, 0, SupportPin), (2, 0, 4, SupportFree), (3, 6, 4, SupportFree), (4, 6, 0, SupportPin)), Elements((1, 1, 2, 100, 10000), (2, 2, 3, 1e6, 10000), (3, 3, 4, 100, 10000)), Loads((1, "nodal", 2, 0, 0, 0, 0, 30, 0, 0)), CalcInputValue.Of("n_div", 8)],
                [new CalcExpectedValue("R1_x", -15, "kN", 1e-5), new CalcExpectedValue("R4_x", -15, "kN", 1e-5), new CalcExpectedValue("R1_y", -20, "kN", 1e-9), new CalcExpectedValue("R4_y", 20, "kN", 1e-9), new CalcExpectedValue("M_max", 60, "kN*m", 1e-5), new CalcExpectedValue("N_e1", 20, "kN", 1e-9)]),
            new CalcTestCase("ka-two-span-release", "Two-span beam with a hinge (release) at the interior support side, point load",
                new CalcReferenceSource("Statics (hinge makes the right span simply supported)", null, "Span 2 (4 m) with a hinge at its left end and a 40 kN load at midspan: R3 = 20 kN, M_max = 40 kN·m; span 1 (fixed–pin) carries 20 kN at its right end as a cantilever: M_fixed = −20 × 5 = −100 kN·m"),
                [CalcInputValue.Quantity("E", 210, "GPa"), Nodes((1, 0, 0, SupportFixed), (2, 5, 0, SupportRollerY), (3, 9, 0, SupportRollerY)), Elements((1, 1, 2, 80, 5000, false, false), (2, 2, 3, 80, 5000, true, false)), Loads((1, "point", 2, 2, 0, 0, 0, 0, -40, 0)), CalcInputValue.Of("n_div", 10)],
                [new CalcExpectedValue("R3_y", 20, "kN", 1e-9), new CalcExpectedValue("R2_y", 20, "kN", 1e-9), new CalcExpectedValue("R1_y", 0, "kN", 1e-9), new CalcExpectedValue("R1_m", 0, "kN*m", 1e-9), new CalcExpectedValue("M_max", 40, "kN*m", 1e-9)]),
            new CalcTestCase("ka-ss-couple", "Simply supported beam, concentrated couple at midspan (the moment diagram jumps)",
                new CalcReferenceSource("Statics (closed form)", null, "Span 6 m, couple 30 kN·m at 3 m: the reactions are ±M/L = 5 kN, so the moment is +5x up to the couple (+15 kN·m just before it) and 5x − 30 after it (−15 kN·m just after): M_max = 15, M_min = −15 kN·m, both at 3 m, one on each side of the jump"),
                [CalcInputValue.Quantity("E", 210, "GPa"), Nodes((1, 0, 0, SupportPin), (2, 6, 0, SupportRollerY)), Elements((1, 1, 2, 100, 8000)), Loads((1, "moment", 1, 3, 0, 0, 0, 0, 0, 30)), CalcInputValue.Of("n_div", 20)],
                [new CalcExpectedValue("M_max", 15, "kN*m", 1e-6), new CalcExpectedValue("M_min", -15, "kN*m", 1e-6)]),
        ],
        DiagramKey: "frame-2d",
        Category: "Analysis",
        OutputPorts:
        [
            new CalcOutputPort("R1_x", "kN", "Horizontal reaction at node 1, signed (node n follows the same pattern, R<n>_x)", "frame.reaction_x_node1"),
            new CalcOutputPort("R1_y", "kN", "Vertical reaction at node 1, up positive: compression into a column base (→ base-plate N_Ed)", "frame.reaction_y_node1"),
            new CalcOutputPort("R1_m", "kN*m", "Moment reaction at node 1, signed", "frame.reaction_moment_node1"),
            new CalcOutputPort("R1_x_abs", "kN", "Magnitude of the horizontal reaction at node 1 (→ base-plate V_Ed)", "frame.reaction_x_magnitude_node1"),
            new CalcOutputPort("R1_m_abs", "kN*m", "Magnitude of the moment reaction at node 1 (→ base-plate M_Ed)", "frame.reaction_moment_magnitude_node1"),
            new CalcOutputPort("N_e1", "kN", "Axial force of greatest magnitude in member 1, tension positive (→ en1993-member N_Ed; member e follows the same pattern, N_e<e>)", "frame.axial_force_member1"),
            new CalcOutputPort("V_e1", "kN", "Greatest shear magnitude in member 1 (→ en1993-member V_Ed, connection-ec3 V_Ed)", "frame.shear_member1"),
            new CalcOutputPort("M_abs_e1", "kN*m", "Greatest bending-moment magnitude in member 1, sagging or hogging (→ en1993-member M_y_Ed)", "frame.moment_magnitude_member1"),
            new CalcOutputPort("M_max_e1", "kN*m", "Maximum sagging moment in member 1", "frame.moment_max_member1"),
            new CalcOutputPort("M_min_e1", "kN*m", "Minimum (hogging) moment in member 1", "frame.moment_min_member1"),
            new CalcOutputPort("V_e2", "kN", "Greatest shear magnitude in member 2 (V_e<e> for any member)", "frame.shear_member2"),
            new CalcOutputPort("M_abs_e2", "kN*m", "Greatest bending-moment magnitude in member 2 (M_abs_e<e> for any member)", "frame.moment_magnitude_member2"),
            new CalcOutputPort("sum_R_y", "kN", "Total vertical reaction", "frame.reaction_y_total"),
            new CalcOutputPort("sum_R_x", "kN", "Total horizontal reaction", "frame.reaction_x_total"),
            new CalcOutputPort("M_max", "kN*m", "Maximum sagging moment, reported combination", "frame.moment_max"),
            new CalcOutputPort("M_min", "kN*m", "Minimum (hogging) moment, reported combination", "frame.moment_min"),
            new CalcOutputPort("V_max", "kN", "Greatest shear magnitude, reported combination", "frame.shear_max"),
            new CalcOutputPort("N_max", "kN", "Maximum axial tension, reported combination", "frame.axial_force_max"),
            new CalcOutputPort("N_min", "kN", "Maximum axial compression (negative), reported combination", "frame.axial_force_min"),
            new CalcOutputPort("d_max", "mm", "Maximum transverse deflection, reported combination", "frame.deflection_max"),
        ]);

    public static CalcInputValue Nodes(params (int Id, double X, double Y, string Support)[] nodes) =>
        CalcInputValue.Table("nodes", nodes.Select(n => CalcRow.Of(("id", new CalcCell(n.Id)), ("x", new CalcCell(n.X, "m")), ("y", new CalcCell(n.Y, "m")), ("support", new CalcCell(Text: n.Support)))).ToList());

    public static CalcInputValue Elements(params (int Id, int I, int J, double ACm2, double ICm4)[] elements) =>
        Elements(elements.Select(e => (e.Id, e.I, e.J, e.ACm2, e.ICm4, false, false)).ToArray());

    public static CalcInputValue Elements(params (int Id, int I, int J, double ACm2, double ICm4, bool RelI, bool RelJ)[] elements) =>
        CalcInputValue.Table("elements", elements.Select(e => CalcRow.Of(("id", new CalcCell(e.Id)), ("i", new CalcCell(e.I)), ("j", new CalcCell(e.J)), ("A", new CalcCell(e.ACm2, "cm^2")), ("I", new CalcCell(e.ICm4, "cm^4")), ("rel_i", new CalcCell(Flag: e.RelI)), ("rel_j", new CalcCell(Flag: e.RelJ)))).ToList());

    /// <summary>Loads (kN, kN/m, kN·m, m): (case, kind, target, a, b, w1, w2, F_x, F_y, M).</summary>
    public static CalcInputValue Loads(params (int Case, string Kind, int Target, double A, double B, double W1, double W2, double Fx, double Fy, double M)[] loads) =>
        CalcInputValue.Table("loads", loads.Select(l => CalcRow.Of(("case", new CalcCell(l.Case)), ("kind", new CalcCell(Text: l.Kind)), ("target", new CalcCell(l.Target)), ("a", new CalcCell(l.A, "m")), ("b", new CalcCell(l.B, "m")), ("w1", new CalcCell(l.W1, "kN/m")), ("w2", new CalcCell(l.W2, "kN/m")), ("F_x", new CalcCell(l.Fx, "kN")), ("F_y", new CalcCell(l.Fy, "kN")), ("M", new CalcCell(l.M, "kN*m")))).ToList());

    public static CalcInputValue Combinations(params (string Name, int Case, double Factor)[] rows) =>
        CalcInputValue.Table("combinations", rows.Select(r => CalcRow.Of(("name", new CalcCell(Text: r.Name)), ("case", new CalcCell(r.Case)), ("factor", new CalcCell(r.Factor)))).ToList());

    public void Run(CalcRunContext c)
    {
        var eDefault = c.InputSi("E");
        var nDiv = Math.Clamp((int)Math.Round(c.InputSi("n_div")), 2, FrameLimits.MaxStations);
        var reportName = c.Has("report") ? c.InputText("report").Trim() : "";
        FrameModel model;
        IReadOnlyList<FrameResult> results;

        try
        {
            model = Build(c, eDefault);
            results = FrameSolver.Solve(model, c.CancellationToken);
        }
        catch (FrameModelException ex)
        {
            var rule = ex.Message.Contains("mechanism", StringComparison.Ordinal) ? "mechanism" : ex.Message.Contains("at most", StringComparison.Ordinal) || ex.Message.Contains("too wide", StringComparison.Ordinal) || ex.Message.Contains("entries", StringComparison.Ordinal) ? "size" : "model";
            c.Block(rule, ex.Message);
            return;
        }

        var kN = UnitCatalog.Parse("kN");
        var kNm = UnitCatalog.Parse("kN*m");
        var mm = UnitCatalog.Parse("mm");
        var m = UnitCatalog.Parse("m");

        c.Record("n_nodes", "nodes", $"{model.Nodes.Count}", new ScalarValue(model.Nodes.Count), "Nodes in the model");
        c.Record("n_elements", "elements", $"{model.Elements.Count}", new ScalarValue(model.Elements.Count), "Members in the model");
        c.Record("n_comb", "combinations", string.Join(", ", model.Combinations.Select(x => x.Name)), new ScalarValue(model.Combinations.Count), "Load combinations analysed");

        var report = results.FirstOrDefault(r => string.Equals(r.Combination.Name, reportName, StringComparison.OrdinalIgnoreCase)) ?? results[0];

        if (reportName.Length > 0 && !string.Equals(report.Combination.Name, reportName, StringComparison.OrdinalIgnoreCase))
            c.Warn("report-name", $"report: no combination is named '{reportName}'. '{report.Combination.Name}' is reported instead.");

        var detailed = model.Elements.Count <= MaxDetailedMembers && model.Nodes.Count(n => n.IsSupported) <= MaxDetailedSupports;

        if (!detailed)
            c.Warn("summary-only", "elements: more than 50 members or supports. Per-member and per-support lines are omitted.");

        c.Record("comb", "reported combination", report.Combination.Name, new TextValue(report.Combination.Name), "The combination detailed below");

        // Reactions.
        // Solver noise (a 1e-14 moment on an unloaded support) is snapped to zero in the magnitude outputs, relative to the largest reaction.
        var reactionScale = Math.Max(1, report.Reactions.Values.Select(r => Math.Max(Math.Abs(r.Rx), Math.Max(Math.Abs(r.Ry), Math.Abs(r.Mz)))).DefaultIfEmpty(0).Max());
        double Snap(double v) => Math.Abs(v) < 1e-9 * reactionScale ? 0 : Math.Abs(v);

        foreach (var node in model.Nodes.Where(n => n.IsSupported))
        {
            var (rx, ry, mz) = report.Reactions[node.Id];

            if (detailed)
            {
                // A reaction that is zero by equilibrium (a moment on a pinned support) comes out as solver noise: shown as 0 (C2-20).
                c.MarkNoiseProne($"R{node.Id}_x", $"R{node.Id}_y", $"R{node.Id}_m");
                c.Record($"R{node.Id}_x", $"reaction at node {node.Id}, x", "Σ member end forces − applied nodal loads", ScalarValue.Of(rx / 1e3, kN), $"Horizontal reaction at node {node.Id}");
                c.Record($"R{node.Id}_y", $"reaction at node {node.Id}, y", "Σ member end forces − applied nodal loads", ScalarValue.Of(ry / 1e3, kN), $"Vertical reaction at node {node.Id}");
                c.Record($"R{node.Id}_m", $"reaction at node {node.Id}, moment", "Σ member end moments − applied nodal couple", ScalarValue.Of(mz / 1e3, kNm), $"Moment reaction at node {node.Id}");
                c.Output($"R{node.Id}_x", $"R_x node {node.Id}");
                c.Output($"R{node.Id}_y", $"R_y node {node.Id}");
                c.Output($"R{node.Id}_m", $"M node {node.Id}");

                // Magnitudes, for a design check (a base plate, a connection) that takes shear and moment unsigned.
                c.Record($"R{node.Id}_x_abs", $"|R_x| at node {node.Id}", $"|{Fmt(rx / 1e3)}| kN", ScalarValue.Of(Snap(rx) / 1e3, kN), $"Magnitude of the horizontal reaction at node {node.Id}");
                c.Record($"R{node.Id}_m_abs", $"|M| at node {node.Id}", $"|{Fmt(mz / 1e3)}| kN·m", ScalarValue.Of(Snap(mz) / 1e3, kNm), $"Magnitude of the moment reaction at node {node.Id}");
                c.Output($"R{node.Id}_x_abs", $"|R_x| node {node.Id}");
                c.Output($"R{node.Id}_m_abs", $"|M| node {node.Id}");
            }
        }

        var sumRy = report.Reactions.Values.Sum(r => r.Ry);
        var sumRx = report.Reactions.Values.Sum(r => r.Rx);
        c.Record("sum_R_y", "Σ R_y", "all supports", ScalarValue.Of(sumRy / 1e3, kN), "Total vertical reaction (equilibrium check against the applied load)");
        c.Record("sum_R_x", "Σ R_x", "all supports", ScalarValue.Of(sumRx / 1e3, kN), "Total horizontal reaction");
        c.MarkNoiseProne("sum_R_y", "sum_R_x");
        c.Output("sum_R_y", "Σ R_y");
        c.Output("sum_R_x", "Σ R_x");

        // Member extremes for the reported combination.
        var summary = Summarise(report, nDiv);

        foreach (var s in summary.Members)
        {
            if (detailed)
            {
                var e = s.Member.Element.Id;
                c.MarkNoiseProne($"M_max_e{e}", $"M_min_e{e}", $"V_e{e}", $"N_e{e}", $"d_e{e}");
                c.Record($"M_max_e{e}", $"member {e}: max sagging M", $"at x = {Fmt(s.XMmax)} m from node {s.Member.Element.I}", ScalarValue.Of(s.Mmax / 1e3, kNm), $"Member {e} maximum moment");
                c.Record($"M_min_e{e}", $"member {e}: min (hogging) M", $"at x = {Fmt(s.XMmin)} m", ScalarValue.Of(s.Mmin / 1e3, kNm), $"Member {e} minimum moment");
                c.Record($"M_abs_e{e}", $"member {e}: max |M|", $"max(|{Fmt(s.Mmax / 1e3)}|, |{Fmt(s.Mmin / 1e3)}|) kN·m", ScalarValue.Of(Math.Max(Math.Abs(s.Mmax), Math.Abs(s.Mmin)) / 1e3, kNm), $"Member {e} greatest moment magnitude (sagging or hogging)");
                c.Record($"V_e{e}", $"member {e}: max |V|", $"{Fmt(s.Vabs / 1e3)} kN", ScalarValue.Of(s.Vabs / 1e3, kN), $"Member {e} maximum shear");
                c.Record($"N_e{e}", $"member {e}: max |N| (tension +)", $"{Fmt(s.Nsigned / 1e3)} kN", ScalarValue.Of(s.Nsigned / 1e3, kN), $"Member {e} axial force of greatest magnitude");
                c.Record($"d_e{e}", $"member {e}: max |deflection|", $"at x = {Fmt(s.Xd)} m", ScalarValue.Of(s.Dabs * 1e3, mm), $"Member {e} maximum transverse deflection");
                c.Output($"M_max_e{e}", $"M max, member {e}");
                c.Output($"M_min_e{e}", $"M min, member {e}");
                c.Output($"M_abs_e{e}", $"|M| max, member {e}");
                c.Output($"V_e{e}", $"|V| max, member {e}");
                c.Output($"N_e{e}", $"N, member {e}");
                c.Output($"d_e{e}", $"δ max, member {e}");

                var stations = s.Member.Stations(nDiv);
                c.Series(new CalcSeries($"M member {e}", "x", "m", "M", "kN*m", stations.Select(x => (x, s.Member.At(x).M / 1e3)).ToList()));
                c.Series(new CalcSeries($"V member {e}", "x", "m", "V", "kN", stations.Select(x => (x, s.Member.At(x).V / 1e3)).ToList()));
                c.Series(new CalcSeries($"N member {e}", "x", "m", "N", "kN", stations.Select(x => (x, s.Member.At(x).N / 1e3)).ToList()));
                c.Series(new CalcSeries($"δ member {e}", "x", "m", "δ", "mm", stations.Select(x => (x, s.Member.At(x).Vy * 1e3)).ToList()));
            }

            // A beam cut into short elements is judged against the length of its straight chain of elements, not each element's own length.
            if (s.Dabs > s.Member.Length / 50)
            {
                var chain = ChainLength(model, s.Member.Element);

                if (s.Dabs > chain / 50)
                    c.Warn("large-deflection", chain > s.Member.Length * (1 + 1e-9)
                        ? $"elements: member {s.Member.Element.Id} deflects {Fmt(s.Dabs * 1e3)} mm, more than L/50 of the {Fmt(chain)} m straight chain of members it belongs to."
                        : $"elements: member {s.Member.Element.Id} deflects {Fmt(s.Dabs * 1e3)} mm, more than L/50.");
            }
        }

        c.Record("M_max", "max sagging moment", $"member {summary.MmaxMember} at x = {Fmt(summary.XMmax)} m", ScalarValue.Of(summary.Mmax / 1e3, kNm), "Maximum moment in the reported combination");
        c.Record("x_Mmax", "position of M_max", $"from node i of member {summary.MmaxMember}", ScalarValue.Of(summary.XMmax, m), "Position of the maximum moment");
        c.Record("M_min", "min (hogging) moment", $"member {summary.MminMember} at x = {Fmt(summary.XMmin)} m", ScalarValue.Of(summary.Mmin / 1e3, kNm), "Minimum moment in the reported combination");
        c.Record("V_max", "max |V|", $"member {summary.VMember}", ScalarValue.Of(summary.Vabs / 1e3, kN), "Maximum shear in the reported combination");
        c.Record("N_max", "max tension", $"member {summary.NmaxMember}", ScalarValue.Of(summary.Nmax / 1e3, kN), "Maximum axial tension");
        c.Record("N_min", "max compression (negative)", $"member {summary.NminMember}", ScalarValue.Of(summary.Nmin / 1e3, kN), "Maximum axial compression");
        c.Record("d_max", "max |deflection|", $"member {summary.DMember} at x = {Fmt(summary.Xd)} m", ScalarValue.Of(summary.Dabs * 1e3, mm), "Maximum transverse deflection");

        foreach (var (symbol, text) in new[] { ("M_max", "M max"), ("x_Mmax", "x of M max"), ("M_min", "M min"), ("V_max", "|V| max"), ("N_max", "N max (tension)"), ("N_min", "N min (compression)"), ("d_max", "δ max") })
            c.Output(symbol, text);

        // Envelope over every combination.
        if (results.Count > 1)
        {
            var all = results.Select(r => Summarise(r, nDiv)).ToList();
            var mMax = all.MaxBy(s => s.Mmax)!;
            var mMin = all.MinBy(s => s.Mmin)!;
            var vMax = all.MaxBy(s => s.Vabs)!;
            var dMax = all.MaxBy(s => s.Dabs)!;
            c.Record("M_max_env", "envelope max M", $"{mMax.Combination}, member {mMax.MmaxMember}", ScalarValue.Of(mMax.Mmax / 1e3, kNm), "Maximum moment over all combinations");
            c.Record("M_min_env", "envelope min M", $"{mMin.Combination}, member {mMin.MminMember}", ScalarValue.Of(mMin.Mmin / 1e3, kNm), "Minimum moment over all combinations");
            c.Record("V_max_env", "envelope max |V|", $"{vMax.Combination}, member {vMax.VMember}", ScalarValue.Of(vMax.Vabs / 1e3, kN), "Maximum shear over all combinations");
            c.Record("d_max_env", "envelope max |δ|", $"{dMax.Combination}, member {dMax.DMember}", ScalarValue.Of(dMax.Dabs * 1e3, mm), "Maximum deflection over all combinations");
            c.Output("M_max_env", "M max, envelope");
            c.Output("M_min_env", "M min, envelope");
            c.Output("V_max_env", "|V| max, envelope");
            c.Output("d_max_env", "δ max, envelope");
        }

        c.Series(new CalcSeries("Geometry", "x", "m", "y", "m", model.Elements.SelectMany(e => new[] { (model.NodeById[e.I].X, model.NodeById[e.I].Y), (model.NodeById[e.J].X, model.NodeById[e.J].Y) }).ToList()));
    }

    /// <summary>The length of the straight chain of members that <paramref name="start"/> belongs to: members joined end to end in a line through unsupported nodes that join exactly two members.</summary>
    private static double ChainLength(FrameModel model, FrameElement start)
    {
        var seen = new HashSet<int> { start.Id };
        var queue = new Queue<FrameElement>();
        queue.Enqueue(start);
        var total = 0.0;

        while (queue.Count > 0)
        {
            var e = queue.Dequeue();
            total += model.Length(e);

            foreach (var nodeId in new[] { e.I, e.J })
            {
                if (model.NodeById[nodeId].IsSupported)
                    continue;

                var attached = model.Elements.Where(x => x.I == nodeId || x.J == nodeId).ToList();

                if (attached.Count != 2)
                    continue;

                var other = attached.Single(x => x.Id != e.Id);

                if (seen.Contains(other.Id))
                    continue;

                var (ax, ay) = model.Delta(e);
                var (bx, by) = model.Delta(other);

                if (Math.Abs(ax * by - ay * bx) > 1e-6 * model.Length(e) * model.Length(other))
                    continue;

                seen.Add(other.Id);
                queue.Enqueue(other);
            }
        }

        return total;
    }

    private sealed record MemberSummary(MemberResult Member, double Mmax, double XMmax, double Mmin, double XMmin, double Vabs, double Nsigned, double Nmax, double Nmin, double Dabs, double Xd);

    private sealed record FrameSummary(string Combination, IReadOnlyList<MemberSummary> Members, double Mmax, double XMmax, int MmaxMember, double Mmin, double XMmin, int MminMember, double Vabs, int VMember, double Nmax, int NmaxMember, double Nmin, int NminMember, double Dabs, double Xd, int DMember);

    private static FrameSummary Summarise(FrameResult r, int nDiv)
    {
        var members = new List<MemberSummary>();

        foreach (var mr in r.Members)
        {
            var (mMax, xMax, mMin, xMin) = mr.MomentExtremes(nDiv);
            var (dMax, xdMax, dMin, xdMin) = mr.DeflectionExtremes(nDiv);
            var stations = mr.Stations(nDiv);
            var states = stations.SelectMany(x => new[] { mr.At(x), mr.At(Math.Min(mr.Length, x + 1e-9 * mr.Length)), mr.At(Math.Max(0, x - 1e-9 * mr.Length)) }).ToList();
            var vAbs = states.Max(s => Math.Abs(s.V));
            var nMax = states.Max(s => s.N);
            var nMin = states.Min(s => s.N);
            var nSigned = Math.Abs(nMax) >= Math.Abs(nMin) ? nMax : nMin;
            var (dAbs, xd) = Math.Abs(dMax) >= Math.Abs(dMin) ? (Math.Abs(dMax), xdMax) : (Math.Abs(dMin), xdMin);
            members.Add(new MemberSummary(mr, Clean(mMax), xMax, Clean(mMin), xMin, Clean(vAbs), Clean(nSigned), Clean(nMax), Clean(nMin), Clean(dAbs), xd));
        }

        var a = members.MaxBy(s => s.Mmax)!;
        var b = members.MinBy(s => s.Mmin)!;
        var v = members.MaxBy(s => s.Vabs)!;
        var nx = members.MaxBy(s => s.Nmax)!;
        var nn = members.MinBy(s => s.Nmin)!;
        var d = members.MaxBy(s => s.Dabs)!;
        return new FrameSummary(r.Combination.Name, members, a.Mmax, a.XMmax, a.Member.Element.Id, b.Mmin, b.XMmin, b.Member.Element.Id, v.Vabs, v.Member.Element.Id, nx.Nmax, nx.Member.Element.Id, nn.Nmin, nn.Member.Element.Id, d.Dabs, d.Xd, d.Member.Element.Id);
    }

    /// <summary>Rounds solver noise (1e-12 kN on an unloaded member) to zero.</summary>
    private static double Clean(double v) => Math.Abs(v) < 1e-9 ? 0 : v;

    private static string Fmt(double v) => CalcValue.FormatNumber(v, 5);

    private static FrameModel Build(CalcRunContext c, double eDefault)
    {
        var nodes = new List<FrameNode>();

        foreach (var row in c.Rows("nodes"))
        {
            var support = row.Get("support")?.Text ?? SupportFree;
            var (fx, fy, fr) = support switch
            {
                SupportPin => (true, true, false),
                SupportRollerY => (false, true, false),
                SupportRollerX => (true, false, false),
                SupportFixed => (true, true, true),
                _ => (false, false, false),
            };
            nodes.Add(new FrameNode(Int(row, "id"), Si(row, "x", "m"), Si(row, "y", "m"), fx, fy, fr,
                support == SupportSpring ? Si(row, "k_x", "kN/m") : 0, support == SupportSpring ? Si(row, "k_y", "kN/m") : 0, support == SupportSpring ? Si(row, "k_r", "kN*m") : 0));
        }

        var elements = c.Rows("elements").Select(row => new FrameElement(Int(row, "id"), Int(row, "i"), Int(row, "j"),
            row.Get("E")?.Number is { } ? Si(row, "E", "GPa") : eDefault, Si(row, "A", "cm^2"), Si(row, "I", "cm^4"), row.Get("rel_i")?.Flag ?? false, row.Get("rel_j")?.Flag ?? false)).ToList();

        var loads = c.Rows("loads").Select(row =>
        {
            var kind = (row.Get("kind")?.Text ?? "point") switch
            {
                "nodal" => FrameLoadKind.Nodal,
                "udl" => FrameLoadKind.Udl,
                "trapezoid" => FrameLoadKind.Trapezoid,
                "moment" => FrameLoadKind.Moment,
                _ => FrameLoadKind.Point,
            };

            if (kind is FrameLoadKind.Point or FrameLoadKind.Moment && row.Get("a")?.Number is null)
                throw new FrameModelException($"loads: the {(kind == FrameLoadKind.Moment ? "couple" : "point load")} in case {Int(row, "case")} on element {Int(row, "target")} has no position; enter a, its distance from end i (0 for the start of the member).");

            // A blank magnitude is not zero: a trapezoid with w2 blank would silently become a triangle, a udl with w1 blank would carry nothing.
            var missing = kind switch
            {
                FrameLoadKind.Udl when row.Get("w1")?.Number is null => "w1",
                FrameLoadKind.Trapezoid when row.Get("w1")?.Number is null => "w1",
                FrameLoadKind.Trapezoid when row.Get("w2")?.Number is null => "w2",
                FrameLoadKind.Moment when row.Get("M")?.Number is null => "M",
                FrameLoadKind.Point when row.Get("F_x")?.Number is null && row.Get("F_y")?.Number is null => "F_x or F_y",
                FrameLoadKind.Nodal when row.Get("F_x")?.Number is null && row.Get("F_y")?.Number is null && row.Get("M")?.Number is null => "F_x, F_y or M",
                _ => null,
            };

            if (missing is not null)
                throw new FrameModelException($"loads: the {row.Get("kind")?.Text ?? "point"} load in case {Int(row, "case")} on {(kind == FrameLoadKind.Nodal ? "node" : "element")} {Int(row, "target")} has no magnitude: {missing} is blank. A blank is not zero; enter the value (0 for none).");

            return new FrameLoad(Int(row, "case"), kind, Int(row, "target"), Si(row, "a", "m"), Si(row, "b", "m"), Si(row, "w1", "kN/m"), Si(row, "w2", "kN/m"), Si(row, "F_x", "kN"), Si(row, "F_y", "kN"), Si(row, "M", "kN*m"), row.Get("gravity")?.Flag ?? false);
        }).ToList();

        var combinations = c.Rows("combinations")
            .GroupBy(row => (row.Get("name")?.Text ?? "").Trim())
            .Where(g => g.Key.Length > 0)
            .Select(g => new FrameCombination(g.Key, g.Select(row => (Int(row, "case"), row.Get("factor")?.Number ?? 1.0)).ToList()))
            .ToList();

        return new FrameModel(nodes, elements, loads, combinations);
    }

    private static int Int(CalcRow row, string key) => (int)Math.Round(row.Get(key)?.Number ?? throw new FrameModelException($"{key}: a whole number is needed."));

    private static double Si(CalcRow row, string key, string defaultUnit)
    {
        var cell = row.Get(key);

        if (cell?.Number is not { } n)
            return 0;

        return UnitCatalog.Parse(string.IsNullOrEmpty(cell.Unit) ? defaultUnit : cell.Unit).ToSi(n);
    }
}
