using System.Diagnostics;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Calc.Structural.Frame;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>§8 Yellow budgets: sheet render under 1 s for every template, a 10,000-sample Monte Carlo under 2 s, a 500-element frame under 1 s. Generous CI multipliers keep these from flaking on slow runners.</summary>
public sealed class PerformanceBudgetTests
{
    private const double CiMultiplier = 3.0;

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Sheet_Renders_Under_One_Second(string id)
    {
        var calculator = AllCalculators.Get(id);
        var inputs = CalcTemplateHasher.DefaultInputs(calculator.Template);
        CalcRunner.Run(calculator, inputs); // warm up
        var sw = Stopwatch.StartNew();
        var result = CalcRunner.Run(calculator, inputs);
        var sheet = CalcSheetTextRenderer.Render(CalcSheetBuilder.Build(calculator.Template, inputs, result,
            new CalcSheetHeader("Perf", "P", calculator.Template.Title, "A", CalcSheetStatus.Draft, "TempestOS", null, null, null, null),
            GoldenFiles.FixedFooter(calculator.Template, calculator.Template.TestCases.Count, calculator.Template.TestCases.Count)));
        sw.Stop();
        Assert.True(sheet.Length > 0);
        Assert.True(sw.Elapsed.TotalSeconds < 1.0 * CiMultiplier, $"{id}: {sw.Elapsed.TotalMilliseconds:0} ms");
    }

    [Fact]
    public void Ten_Thousand_Sample_Monte_Carlo_Under_Two_Seconds()
    {
        var calculator = new ToleranceStackCalculator();
        var inputs = CalcTemplateHasher.DefaultInputs(calculator.Template).With(CalcInputValue.Of("n_mc", 10_000));
        CalcRunner.Run(calculator, inputs);
        var sw = Stopwatch.StartNew();
        var result = CalcRunner.Run(calculator, inputs);
        sw.Stop();
        Assert.False(result.IsBlocked);
        Assert.True(sw.Elapsed.TotalSeconds < 2.0 * CiMultiplier, $"{sw.Elapsed.TotalMilliseconds:0} ms");
    }

    [Fact]
    public void Five_Hundred_Element_Frame_Under_One_Second()
    {
        // A 25-bay, 10-storey grid: 286 nodes, 260 columns + 250 beams = 510 elements, three load cases.
        var nodes = new List<FrameNode>();
        var elements = new List<FrameElement>();
        var id = 1;

        for (var storey = 0; storey <= 10; storey++)
            for (var bay = 0; bay <= 25; bay++)
                nodes.Add(new FrameNode(storey * 100 + bay + 1, bay * 4.0, storey * 3.5, storey == 0, storey == 0, storey == 0));

        for (var storey = 1; storey <= 10; storey++)
            for (var bay = 0; bay <= 25; bay++)
            {
                elements.Add(new FrameElement(id++, (storey - 1) * 100 + bay + 1, storey * 100 + bay + 1, 210e9, 1e-2, 2e-4, false, false));
                if (bay > 0) elements.Add(new FrameElement(id++, storey * 100 + bay, storey * 100 + bay + 1, 210e9, 1e-2, 2e-4, false, false));
            }

        var loads = new List<FrameLoad>();
        foreach (var e in elements.Where(e => nodes.First(n => n.Id == e.I).Y == nodes.First(n => n.Id == e.J).Y))
            loads.Add(new FrameLoad(1, FrameLoadKind.Udl, e.Id, 0, 0, -20e3, -20e3, 0, 0, 0, false));
        for (var storey = 1; storey <= 10; storey++)
            loads.Add(new FrameLoad(2, FrameLoadKind.Nodal, storey * 100 + 1, 0, 0, 0, 0, 15e3, 0, 0, false));
        loads.Add(new FrameLoad(3, FrameLoadKind.Nodal, 1001 + 12, 0, 0, 0, 0, 0, -50e3, 0, false));

        var model = new FrameModel(nodes, elements, loads, [new FrameCombination("ULS", [(1, 1.35), (2, 1.5), (3, 1.5)])]);
        FrameSolver.Solve(model);
        var sw = Stopwatch.StartNew();
        var results = FrameSolver.Solve(model);
        foreach (var m in results[0].Members) m.MomentExtremes(20);
        sw.Stop();
        Assert.Equal(510, results[0].Members.Count);
        Assert.True(sw.Elapsed.TotalSeconds < 1.0 * CiMultiplier, $"{sw.Elapsed.TotalMilliseconds:0} ms");
    }
}
