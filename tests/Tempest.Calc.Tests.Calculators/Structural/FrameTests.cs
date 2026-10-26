using CsCheck;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Calc.Structural.Frame;

namespace Tempest.Calc.Tests.Calculators.Structural;

public sealed class FrameTests
{
    private static readonly FrameCalculator Calc = new();

    private static CalcRunResult Run(params CalcInputValue[] values) => CalcRunner.Run(Calc, new CalcInputSet(values));

    private static FrameModel Beam(double l, double w, bool releaseJ = false) => new(
        [new FrameNode(1, 0, 0, true, true, false), new FrameNode(2, l, 0, false, true, false)],
        [new FrameElement(1, 1, 2, 210e9, 1e-2, 8e-5, false, releaseJ)],
        [new FrameLoad(1, FrameLoadKind.Udl, 1, 0, l, w, w, 0, 0, 0, false)],
        []);

    [Fact]
    public void Simply_Supported_Udl_Matches_Closed_Form_At_Every_Station()
    {
        Gen.Select(Gen.Double[1, 20], Gen.Double[-50e3, 50e3]).Sample((l, w) =>
        {
            var r = FrameSolver.Solve(Beam(l, w))[0];
            var m = r.Members[0];
            var ei = 210e9 * 8e-5;

            foreach (var x in m.Stations(10))
            {
                var s = m.At(x);
                // w positive towards local +y (upward): the reactions act downward, so M = −w x (L − x)/2 and V = w (x − L/2).
                Assert.Equal(-w * x * (l - x) / 2, s.M, Math.Abs(w) * l * l * 1e-9 + 1e-9);
                Assert.Equal(w * (x - l / 2), s.V, Math.Abs(w) * l * 1e-9 + 1e-9);
                Assert.Equal(w * x * (l * l * l - 2 * l * x * x + x * x * x) / (24 * ei), s.Vy, Math.Abs(w) * Math.Pow(l, 4) / ei * 1e-9 + 1e-15);
            }
        }, iter: 100);
    }

    [Fact]
    public void Reactions_Balance_The_Applied_Loads_On_Random_Frames()
    {
        var gen = Gen.Select(Gen.Int[2, 8], Gen.Double[2, 6], Gen.Double[2, 5], Gen.Double[-30e3, 30e3], Gen.Double[-20e3, 20e3], Gen.Double[-15e3, 15e3]);
        gen.Sample((bays, span, height, w, p, h) =>
        {
            var nodes = new List<FrameNode>();
            var elements = new List<FrameElement>();

            for (var i = 0; i <= bays; i++)
            {
                nodes.Add(new FrameNode(2 * i + 1, i * span, 0, true, true, i % 2 == 0));
                nodes.Add(new FrameNode(2 * i + 2, i * span, height, false, false, false));
                elements.Add(new FrameElement(100 + i, 2 * i + 1, 2 * i + 2, 210e9, 5e-3, 3e-5, false, false));
                if (i > 0) elements.Add(new FrameElement(200 + i, 2 * i, 2 * i + 2, 210e9, 5e-3, 3e-5, i == 1, false));
            }

            var loads = new List<FrameLoad>
            {
                new(1, FrameLoadKind.Nodal, 2, 0, 0, 0, 0, h, 0, 0, false),
                new(1, FrameLoadKind.Point, 100, height / 3, 0, 0, 0, p, p / 2, 0, false),
                new(1, FrameLoadKind.Moment, 100, height / 2, 0, 0, 0, 0, 0, 5e3, false),
            };

            for (var i = 1; i <= bays; i++)
                loads.Add(new FrameLoad(1, FrameLoadKind.Trapezoid, 200 + i, 0, span, w, w / 3, 0, 0, 0, i % 2 == 0));

            var r = FrameSolver.Solve(new FrameModel(nodes, elements, loads, []))[0];
            var rx = r.Reactions.Values.Sum(v => v.Rx);
            var ry = r.Reactions.Values.Sum(v => v.Ry);
            var appliedX = h + p;
            var appliedY = p / 2 + bays * span * (w + w / 3) / 2 * -1 * 0; // trapezoids act in local y (horizontal members → global y) except the gravity ones (global −y)

            foreach (var i in Enumerable.Range(1, bays))
                appliedY += (i % 2 == 0 ? -1 : 1) * span * (w + w / 3) / 2;

            Assert.Equal(-appliedX, rx, Math.Abs(appliedX) * 1e-8 + 1e-6);
            Assert.Equal(-appliedY, ry, Math.Abs(appliedY) * 1e-8 + 1e-6);

            // Each member's end forces balance its own loads.
            foreach (var m in r.Members)
            {
                var end = m.At(m.Length);
                Assert.Equal(-m.EndForces[4], end.V, Math.Abs(m.EndForces[4]) * 1e-8 + 1e-6);
                Assert.Equal(m.EndForces[5], end.M, Math.Abs(m.EndForces[5]) * 1e-8 + 1e-6);
                Assert.Equal(m.EndForces[3], end.N, Math.Abs(m.EndForces[3]) * 1e-8 + 1e-6);
            }
        }, iter: 60);
    }

    [Theory]
    [InlineData(3.0, 15.0, -15.0)]
    [InlineData(3.3, 16.5, -13.5)]
    [InlineData(6.0, 30.0, 0.0)]
    [InlineData(0.0, 0.0, -30.0)]
    public void A_Concentrated_Couple_Reports_The_Moment_On_Both_Sides_Of_The_Jump(double a, double expectedMax, double expectedMin)
    {
        // P1-struct.frame-2d-01. Simply supported 6 m span, 30 kN*m couple at a: R = 30/6 = 5 kN, so M(a-) = 5a and M(a+) = 5a - 30 (python3:
        // a = 3 -> +15 / -15; a = 3.3 -> 16.5 / -13.5; a = 6 -> 30 / 0; a = 0 -> 0 / -30). The product reported only the value after the jump
        // at a station (13.5 against 15 at a = 3, 28.5 against 30 at a = 6).
        var r = Run(CalcInputValue.Quantity("E", 210, "GPa"), FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 6, 0, FrameCalculator.SupportRollerY)),
            FrameCalculator.Elements((1, 1, 2, 100, 8000)), FrameCalculator.Loads((1, "moment", 1, a, 0, 0, 0, 0, 0, 30)), CalcInputValue.Of("n_div", 20));
        Assert.Equal(expectedMax, r.OutputSi("M_max") / 1e3, 6);
        Assert.Equal(expectedMin, r.OutputSi("M_min") / 1e3, 6);
    }

    [Theory]
    [InlineData("point")]
    [InlineData("moment")]
    public void A_Point_Or_Couple_Load_With_A_Blank_Position_Is_Blocked_Instead_Of_Landing_On_The_Member_Start(string kind)
    {
        // P1-struct.frame-2d-02. SS 6 m, point load F_y -60 with 'a' blank gave R1_y 60, R2_y 0 and M_max 0 with no notice.
        var blank = CalcRow.Of(("case", new CalcCell(1)), ("kind", new CalcCell(Text: kind)), ("target", new CalcCell(1)), ("F_y", new CalcCell(-60, "kN")), ("M", new CalcCell(30, "kN*m")));
        var r = Run(CalcInputValue.Quantity("E", 210, "GPa"), FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 6, 0, FrameCalculator.SupportRollerY)),
            FrameCalculator.Elements((1, 1, 2, 100, 8000)), CalcInputValue.Table("loads", [blank]), CalcInputValue.Of("n_div", 20));
        Assert.True(r.IsBlocked);
        var block = Assert.Single(r.Blocks);
        Assert.Equal("model", block.RuleId);
        Assert.StartsWith("loads:", block.Message, StringComparison.Ordinal);
        Assert.Contains("enter a", block.Message, StringComparison.Ordinal);

        // The same load with a = 0 typed is accepted: the start of the member is then a stated position.
        var zero = CalcRow.Of(("case", new CalcCell(1)), ("kind", new CalcCell(Text: kind)), ("target", new CalcCell(1)), ("a", new CalcCell(0, "m")), ("F_y", new CalcCell(-60, "kN")), ("M", new CalcCell(30, "kN*m")));
        Assert.False(Run(CalcInputValue.Quantity("E", 210, "GPa"), FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 6, 0, FrameCalculator.SupportRollerY)),
            FrameCalculator.Elements((1, 1, 2, 100, 8000)), CalcInputValue.Table("loads", [zero]), CalcInputValue.Of("n_div", 20)).IsBlocked);
    }

    [Theory]
    [InlineData("udl", "w1", 0)]
    [InlineData("trapezoid", "w2", 1)]
    [InlineData("trapezoid", "w1", 2)]
    [InlineData("point", "F_y", 3)]
    [InlineData("moment", "M", 4)]
    [InlineData("nodal", "F_y", 5)]
    public void A_Load_Row_With_A_Blank_Magnitude_Is_Blocked_Instead_Of_Running_As_Zero(string kind, string blankSymbol, int index)
    {
        // struct.frame-2d-R1: SS 6 m, trapezoid a 0, b 6, w1 -10, w2 blank gave SUM R_y 30 kN and M_max 23.09 kN.m (a triangle) where the uniform intent gives 60 kN and 45 kN.m;
        // a udl row with only F_y filled carried nothing. A blank magnitude is not zero: it is blocked, and an explicit 0 is accepted.
        _ = index;
        var cells = new Dictionary<string, CalcCell>
        {
            ["case"] = new(1), ["kind"] = new(Text: kind), ["target"] = new(kind == "nodal" ? 2 : 1), ["a"] = new(kind is "point" or "moment" ? 3 : 0, "m"), ["b"] = new(kind is "udl" or "trapezoid" ? 6 : 0, "m"),
            ["w1"] = new(-10, "kN/m"), ["w2"] = new(-10, "kN/m"), ["F_x"] = new(0, "kN"), ["F_y"] = new(-60, "kN"), ["M"] = new(30, "kN*m"),
        };
        CalcRunResult WithRow(IEnumerable<string> blank, double? explicitValue = null)
        {
            var row = CalcRow.Of(cells.Where(kv => !blank.Contains(kv.Key)).Select(kv => (kv.Key, kv.Value)).ToArray());
            return Run(CalcInputValue.Quantity("E", 210, "GPa"), FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 6, 0, FrameCalculator.SupportRollerY)),
                FrameCalculator.Elements((1, 1, 2, 100, 8000)), CalcInputValue.Table("loads", [row]), CalcInputValue.Of("n_div", 20));
        }

        // Blank in the field that matters (a point row is blank when F_x and F_y both are, a nodal row when F_x, F_y and M all are).
        var blanks = kind == "nodal" ? new[] { "F_x", "F_y", "M" } : kind == "point" ? ["F_x", "F_y"] : [blankSymbol];
        var r = WithRow(blanks);
        Assert.True(r.IsBlocked);
        var block = Assert.Single(r.Blocks);
        Assert.Equal("model", block.RuleId);
        Assert.StartsWith("loads:", block.Message, StringComparison.Ordinal);
        Assert.Contains(blanks[0], block.Message, StringComparison.Ordinal);

        Assert.False(WithRow([]).IsBlocked);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void A_Couple_At_A_Member_End_Reports_The_Full_Jump_In_The_Greatest_Moment_Port(double a)
    {
        // struct.frame-2d-D1: SS 6 m, 30 kN.m couple at the member end: M_abs_e1 was 28.5 kN.m, the correct value is 30 (the stations stop one step short of the jump).
        var r = Run(CalcInputValue.Quantity("E", 210, "GPa"), FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 6, 0, FrameCalculator.SupportRollerY)),
            FrameCalculator.Elements((1, 1, 2, 100, 8000)), FrameCalculator.Loads((1, "moment", 1, a, 0, 0, 0, 0, 0, 30)), CalcInputValue.Of("n_div", 20));
        Assert.Equal(30, r.OutputSi("M_abs_e1") / 1e3, 6);
    }

    [Fact]
    public void A_Long_Beam_Cut_Into_Short_Elements_Does_Not_Warn_Large_Deflection_Against_Each_Elements_Own_Length()
    {
        // struct.frame-2d-03: 10 m simply supported beam in 20 elements of 0.5 m, midspan deflection 27.2 mm (span/368): 16 warnings "deflects ... more than L/50" (L = 0.5 m, 10 mm).
        // python3: EI = 210 GPa x 22800 cm4 = 47880 kN.m2; 5 w L^4/(384 EI) with w 10 kN/m = 27.2 mm.
        const int n = 20;
        var nodes = Enumerable.Range(0, n + 1).Select(i => (i + 1, 10.0 * i / n, 0.0, i == 0 ? FrameCalculator.SupportPin : i == n ? FrameCalculator.SupportRollerY : FrameCalculator.SupportFree)).ToArray();
        var elements = Enumerable.Range(0, n).Select(i => (i + 1, i + 1, i + 2, 100.0, 22800.0)).ToArray();
        var loads = Enumerable.Range(0, n).Select(i => (1, "udl", i + 1, 0.0, 0.0, -10.0, 0.0, 0.0, 0.0, 0.0)).ToArray();
        var r = Run(CalcInputValue.Quantity("E", 210, "GPa"), FrameCalculator.Nodes(nodes), FrameCalculator.Elements(elements), FrameCalculator.Loads(loads), CalcInputValue.Of("n_div", 8));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(27.2, r.OutputSi("d_max") * 1e3, 1);
        Assert.DoesNotContain(r.Notices, nt => nt.RuleId == "large-deflection");

        // A single 10 m member deflecting more than L/50 = 200 mm still warns (EI 1/10: 272 mm).
        var single = Run(CalcInputValue.Quantity("E", 210, "GPa"), FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 10, 0, FrameCalculator.SupportRollerY)),
            FrameCalculator.Elements((1, 1, 2, 100, 2280)), FrameCalculator.Loads((1, "udl", 1, 0, 0, -10, 0, 0, 0, 0)), CalcInputValue.Of("n_div", 8));
        Assert.Contains(single.Notices, nt => nt.RuleId == "large-deflection");
    }

    [Fact]
    public void Released_End_Carries_No_Moment()
    {
        var r = FrameSolver.Solve(Beam(6, -10e3, releaseJ: true))[0];
        var m = r.Members[0];
        Assert.Equal(0, m.EndForces[5], 6);
        Assert.Equal(0, m.At(6).M, 6);
        Assert.Equal(45e3, m.MomentExtremes(20).Max, 3);
    }

    [Fact]
    public void Hinge_Mechanism_Is_Reported_Not_Thrown()
    {
        var r = Run(FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 3, 0, FrameCalculator.SupportFree), (3, 6, 0, FrameCalculator.SupportRollerY)),
            FrameCalculator.Elements((1, 1, 2, 10, 1000, false, true), (2, 2, 3, 10, 1000, false, false)),
            FrameCalculator.Loads((1, "nodal", 2, 0, 0, 0, 0, 0, -10, 0)));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.RuleId == "mechanism");
    }

    [Fact]
    public void Unsupported_Frame_Is_A_Mechanism()
    {
        var r = Run(FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportRollerY), (2, 3, 0, FrameCalculator.SupportRollerY)),
            FrameCalculator.Elements((1, 1, 2, 10, 1000)),
            FrameCalculator.Loads((1, "nodal", 2, 0, 0, 0, 0, 5, 0, 0)));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.RuleId == "mechanism");
    }

    [Fact]
    public void Bad_References_Block_With_Plain_Messages()
    {
        var r = Run(FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 3, 0, FrameCalculator.SupportRollerY)),
            FrameCalculator.Elements((1, 1, 9, 10, 1000)),
            FrameCalculator.Loads((1, "nodal", 2, 0, 0, 0, 0, 0, -10, 0)));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.Message.Contains("does not exist", StringComparison.Ordinal));

        var off = Run(FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 3, 0, FrameCalculator.SupportRollerY)),
            FrameCalculator.Elements((1, 1, 2, 10, 1000)),
            FrameCalculator.Loads((1, "point", 1, 5, 0, 0, 0, 0, -10, 0)));
        Assert.True(off.IsBlocked);
        Assert.Contains(off.Blocks, b => b.Message.Contains("outside the member", StringComparison.Ordinal));
    }

    [Fact]
    public void Caps_Are_Enforced()
    {
        var nodes = Enumerable.Range(1, FrameLimits.MaxNodes + 1).Select(i => new FrameNode(i, i, 0, i == 1, true, false)).ToList();
        var elements = Enumerable.Range(1, FrameLimits.MaxNodes).Select(i => new FrameElement(i, i, i + 1, 210e9, 1e-3, 1e-5, false, false)).ToList();
        Assert.Throws<FrameModelException>(() => new FrameModel(nodes, elements, [new FrameLoad(1, FrameLoadKind.Nodal, 2, 0, 0, 0, 0, 0, -1, 0, false)], []));

        var cases = Enumerable.Range(1, FrameLimits.MaxCases + 1).Select(i => new FrameLoad(i, FrameLoadKind.Nodal, 2, 0, 0, 0, 0, 0, -1, 0, false)).ToList();
        Assert.Throws<FrameModelException>(() => new FrameModel(nodes.Take(3).ToList(), elements.Take(2).ToList(), cases, []));
    }

    [Fact]
    public void A_Long_Continuous_Beam_Solves_Within_The_Caps()
    {
        var n = 1_001;
        var nodes = Enumerable.Range(1, n).Select(i => new FrameNode(i, (i - 1) * 0.5, 0, i == 1, i % 10 == 1, false)).ToList();
        var elements = Enumerable.Range(1, n - 1).Select(i => new FrameElement(i, i, i + 1, 210e9, 1e-3, 1e-5, false, false)).ToList();
        var loads = elements.Select(e => new FrameLoad(1, FrameLoadKind.Udl, e.Id, 0, 0.5, -5e3, -5e3, 0, 0, 0, false)).ToList();
        var r = FrameSolver.Solve(new FrameModel(nodes, elements, loads, []))[0];
        Assert.Equal(5e3 * 500, r.Reactions.Values.Sum(v => v.Ry), 1e-3);
    }

    [Fact]
    public void Gravity_Load_On_An_Inclined_Member_Resolves_Into_Axial_And_Transverse()
    {
        // A 3-4-5 rafter pinned at both ends under 10 kN/m gravity per metre: vertical reactions 25 kN each.
        var r = Run(FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 4, 3, FrameCalculator.SupportRollerY)),
            FrameCalculator.Elements((1, 1, 2, 50, 5000)),
            CalcInputValue.Table("loads", [CalcRow.Of(("case", new CalcCell(1)), ("kind", new CalcCell(Text: "udl")), ("target", new CalcCell(1)), ("w1", new CalcCell(10, "kN/m")), ("gravity", new CalcCell(Flag: true)))]));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(25, r.OutputSi("R1_y") / 1e3, 6);
        Assert.Equal(25, r.OutputSi("R2_y") / 1e3, 6);
        Assert.Equal(0, r.OutputSi("R1_x") / 1e3, 6);
    }

    [Fact]
    public void Spring_Support_Deflects_By_Force_Over_Stiffness()
    {
        var r = Run(CalcInputValue.Table("nodes",
            [
                CalcRow.Of(("id", new CalcCell(1)), ("x", new CalcCell(0, "m")), ("y", new CalcCell(0, "m")), ("support", new CalcCell(Text: FrameCalculator.SupportPin))),
                CalcRow.Of(("id", new CalcCell(2)), ("x", new CalcCell(4, "m")), ("y", new CalcCell(0, "m")), ("support", new CalcCell(Text: FrameCalculator.SupportSpring)), ("k_y", new CalcCell(1000, "kN/m")), ("k_x", new CalcCell(1e9, "kN/m"))),
            ]),
            FrameCalculator.Elements((1, 1, 2, 50, 5000)),
            FrameCalculator.Loads((1, "nodal", 2, 0, 0, 0, 0, 0, -10, 0)));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(10, r.OutputSi("R2_y") / 1e3, 6);
        Assert.Equal(10, r.OutputSi("d_max") * 1e3, 3);
    }

    [Fact]
    public void Combinations_Scale_And_Envelope()
    {
        var r = Run(FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 6, 0, FrameCalculator.SupportRollerY)),
            FrameCalculator.Elements((1, 1, 2, 100, 8000)),
            FrameCalculator.Loads((1, "udl", 1, 0, 6, -10, 0, 0, 0, 0), (2, "udl", 1, 0, 6, -4, 0, 0, 0, 0)),
            FrameCalculator.Combinations(("ULS", 1, 1.35), ("ULS", 2, 1.5), ("SLS", 1, 1.0), ("SLS", 2, 1.0)),
            CalcInputValue.Choice("report", "SLS"));
        Assert.False(r.IsBlocked);
        Assert.Equal(14 * 36 / 8.0, r.OutputSi("M_max") / 1e3, 6);
        Assert.Equal((13.5 + 6) * 36 / 8.0, r.OutputSi("M_max_env") / 1e3, 6);
    }

    [Fact]
    public void Reaction_And_Moment_Magnitudes_Are_Ports_Whatever_The_Sign()
    {
        // Cantilever, 20 kN down at the 3 m tip: R_y = +20, the base moment reaction is +60 kN·m, no horizontal reaction.
        var cantilever = CalcRunner.Run(Calc, new CalcInputSet(Calc.Template.TestCases[1].Inputs));
        Assert.Equal(60, cantilever.OutputSi("R1_m") / 1e3, 6);
        Assert.Equal(60, cantilever.OutputSi("R1_m_abs") / 1e3, 6);
        Assert.Equal(0, cantilever.OutputSi("R1_x_abs"), 6);
        Assert.Equal(60, cantilever.OutputSi("M_abs_e1") / 1e3, 6);

        // The same cantilever pushed the other way round (load up): the signed reaction flips, its magnitude does not.
        var up = CalcRunner.Run(Calc, new CalcInputSet(Calc.Template.TestCases[1].Inputs.Select(v => v.Symbol == "loads" ? FrameCalculator.Loads((1, "nodal", 2, 0, 0, 0, 0, 0, 20, 0)) : v)));
        Assert.Equal(-60, up.OutputSi("R1_m") / 1e3, 6);
        Assert.Equal(60, up.OutputSi("R1_m_abs") / 1e3, 6);
        Assert.Equal(-20, up.OutputSi("R1_y") / 1e3, 6);

        // Pinned portal under a 30 kN lateral load: each base takes -15 kN horizontally; the magnitude port is positive.
        var portal = CalcRunner.Run(Calc, new CalcInputSet(Calc.Template.TestCases[3].Inputs));
        Assert.Equal(-15, portal.OutputSi("R1_x") / 1e3, 5);
        Assert.Equal(15, portal.OutputSi("R1_x_abs") / 1e3, 5);
        Assert.Equal(15, portal.OutputSi("R4_x_abs") / 1e3, 5);

        // A simply supported beam hogs nowhere: |M| is the sagging moment wL²/8 = 45 kN·m.
        var beam = CalcRunner.Run(Calc, new CalcInputSet(Calc.Template.TestCases[0].Inputs));
        Assert.Equal(45, beam.OutputSi("M_abs_e1") / 1e3, 6);

        // Every declared port of the frame template is produced for a model with four nodes and three members.
        Assert.All(Calc.Template.OutputPorts!, p => Assert.NotNull(portal.FindOutput(p.Symbol)));
        Assert.Contains(Calc.Template.OutputPorts!, p => p is { Symbol: "R1_m_abs", Unit: "kN*m" });
    }

    [Fact]
    public void Solver_Noise_On_An_Unloaded_Support_Is_Snapped_To_Zero_In_The_Magnitude_Outputs()
    {
        // The pinned portal's pinned bases carry no moment; the magnitude outputs print exactly zero, not 1e-14.
        var portal = CalcRunner.Run(Calc, new CalcInputSet(Calc.Template.TestCases[3].Inputs));
        Assert.Equal(0, portal.OutputSi("R1_m_abs"));
        Assert.Equal(0, portal.OutputSi("R4_m_abs"));
    }
}
