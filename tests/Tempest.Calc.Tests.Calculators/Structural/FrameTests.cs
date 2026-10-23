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
}
