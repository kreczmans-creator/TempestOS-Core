using System.Diagnostics;
using System.Globalization;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Calc.Structural.Frame;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>
/// Red team (offensive review): hostile table inputs reaching the
/// calculators and the solvers (frame sizes, load rows, stiffness ratios,
/// polygon vertex counts). Every test asserts the secure behaviour: the
/// runner returns a block instead of throwing, and a run stays inside the
/// 5 s budget.
/// </summary>
public sealed class Red_SolverAttacks
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    private static CalcInputSet WithOverride(CalcTemplate template, params CalcInputValue[] overrides)
    {
        var inputs = CalcTemplateHasher.DefaultInputs(template).Values.ToDictionary(v => v.Symbol, StringComparer.Ordinal);

        foreach (var o in overrides)
            inputs[o.Symbol] = o;

        return new CalcInputSet([.. inputs.Values]);
    }

    private static string RenderText(ICalcCalculator calculator, CalcInputSet set, CalcRunResult result) =>
        CalcSheetTextRenderer.Render(CalcSheetBuilder.Build(calculator.Template, set, result,
            new CalcSheetHeader("Red", "R", calculator.Template.Title, "A", CalcSheetStatus.Draft, "red", null, null, null, null),
            GoldenFiles.FixedFooter(calculator.Template, 0, 0)));

    /// <summary>Runs the calculator on a worker and reports whether it finished inside the budget (a calculator does not poll the cancellation token, so a timeout cannot stop it).</summary>
    private static (bool Finished, TimeSpan Elapsed, CalcRunResult? Result) RunWithBudget(ICalcCalculator calculator, CalcInputSet set)
    {
        var sw = Stopwatch.StartNew();
        var task = Task.Run(() => CalcRunner.Run(calculator, set));
        var finished = task.Wait(Budget + TimeSpan.FromSeconds(10));
        sw.Stop();
        return (finished && sw.Elapsed <= Budget, sw.Elapsed, finished ? task.Result : null);
    }

    // ------------------------------------------------------------------
    // Hostile table rows through the JSON importer
    // ------------------------------------------------------------------

    /// <summary>R-11: a table row without a Cells dictionary (JSON <c>{"Rows":[{}]}</c>, <c>[null]</c>) makes the validator dereference null, so CalcRunner.Run throws instead of blocking.</summary>
    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Table_Rows_Without_Cells_Never_Throw(string id)
    {
        var calculator = AllCalculators.Get(id);
        var tables = calculator.Template.Inputs.Where(i => i.Kind == CalcInputKind.Table).ToList();

        foreach (var spec in tables)
        {
            foreach (var rows in new[] { "[{}]", "[null]", "[{\"Cells\":null}]", "[{\"Cells\":{\"x\":null}}]", "[{\"Cells\":{}}]" })
            {
                var (parsed, notices) = CalcInputJson.Read($"[{{\"Symbol\":\"{spec.Symbol}\",\"Rows\":{rows}}}]");

                if (notices.Any(n => n.Severity == CalcNoticeSeverity.Block))
                    continue;

                var set = WithOverride(calculator.Template, parsed.Find(spec.Symbol)!);
                var ex = Record.Exception(() =>
                {
                    var result = CalcRunner.Run(calculator, set);
                    _ = RenderText(calculator, set, result);
                });
                Assert.True(ex is null, $"{id}/{spec.Symbol} with rows {rows}: {ex?.GetType().Name}: {ex?.Message}");
            }
        }
    }

    // ------------------------------------------------------------------
    // Frame solver
    // ------------------------------------------------------------------

    /// <summary>R-12: every load row adds a station to its member, and every station evaluates every load, so 20,000 point loads on one member (the row cap) cost O(n²) per combination.</summary>
    [Fact]
    public void Frame_With_Maximal_Load_Rows_On_One_Member_Finishes_In_Budget()
    {
        var frame = AllCalculators.Get(FrameCalculator.Id);
        const int count = FrameLimits.MaxLoads;
        var loads = Enumerable.Range(0, count)
            .Select(i => (1, "point", 1, 6.0 * (i + 1) / (count + 1), 0.0, 0.0, 0.0, 0.0, i % 2 == 0 ? -1.0 : 1.0, 0.0))
            .ToArray();
        var set = WithOverride(frame.Template,
            FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 6, 0, FrameCalculator.SupportRollerY)),
            FrameCalculator.Elements((1, 1, 2, 100, 8000)),
            FrameCalculator.Loads(loads),
            CalcInputValue.Of("n_div", 20));

        var (finished, elapsed, _) = RunWithBudget(frame, set);
        Assert.True(finished, $"a frame with {count} load rows on one member ran for {elapsed.TotalSeconds:F1} s (budget {Budget.TotalSeconds} s)");
    }

    /// <summary>R-13: the banded-entry cap (20 M) admits a 2,000-node random mesh whose bandwidth is O(n); the LDLᵀ factor is then O(n·b²) ≈ 10¹⁰ flops.</summary>
    [Fact]
    public void Frame_Random_Mesh_At_The_Node_Cap_Finishes_In_Budget()
    {
        var frame = AllCalculators.Get(FrameCalculator.Id);
        var random = new Random(1);
        const int n = FrameLimits.MaxNodes;
        var nodes = Enumerable.Range(1, n).Select(i => (i, Math.Round(random.NextDouble() * 100, 3), Math.Round(random.NextDouble() * 100, 3), FrameCalculator.SupportPin)).ToArray();
        var elements = new List<(int, int, int, double, double)> { (1, 1, 2, 100.0, 8000.0) };

        while (elements.Count < FrameLimits.MaxElements)
        {
            var i = random.Next(1, n + 1);
            var j = random.Next(1, n + 1);

            if (i != j)
                elements.Add((elements.Count + 1, i, j, 100.0, 8000.0));
        }

        var set = WithOverride(frame.Template,
            FrameCalculator.Nodes(nodes),
            FrameCalculator.Elements(elements.ToArray()),
            FrameCalculator.Loads((1, "nodal", 1, 0, 0, 0, 0, 0, 0, 10)),
            CalcInputValue.Of("n_div", 2));

        var (finished, elapsed, result) = RunWithBudget(frame, set);
        Assert.True(finished, $"a {n}-node random mesh ran for {elapsed.TotalSeconds:F1} s (budget {Budget.TotalSeconds} s){(result is null ? string.Empty : ", blocked: " + string.Join(" | ", result.Blocks.Select(b => b.Message)))}");
    }

    /// <summary>Tried, no issue: a mechanism beside a member 10¹² times stiffer is still reported as a mechanism, not solved as a number.</summary>
    [Fact]
    public void Mechanism_Beside_A_Super_Stiff_Member_Is_Detected()
    {
        var frame = AllCalculators.Get(FrameCalculator.Id);
        var set = WithOverride(frame.Template,
            FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 3, 0, FrameCalculator.SupportFree), (3, 10, 0, FrameCalculator.SupportFixed), (4, 10.001, 0, FrameCalculator.SupportFree)),
            CalcInputValue.Table("elements",
            [
                CalcRow.Of(("id", new CalcCell(1)), ("i", new CalcCell(1)), ("j", new CalcCell(2)), ("A", new CalcCell(1e-6, "cm^2")), ("I", new CalcCell(1e-9, "cm^4")), ("E", new CalcCell(1e-6, "GPa"))),
                CalcRow.Of(("id", new CalcCell(2)), ("i", new CalcCell(3)), ("j", new CalcCell(4)), ("A", new CalcCell(1e6, "cm^2")), ("I", new CalcCell(1e12, "cm^4")), ("E", new CalcCell(1e4, "GPa"))),
            ]),
            FrameCalculator.Loads((1, "nodal", 2, 0, 0, 0, 0, 0, -1, 0)),
            CalcInputValue.Of("n_div", 4));

        var result = CalcRunner.Run(frame, set);
        Assert.True(result.IsBlocked, "a pinned-base cantilever (a mechanism) was solved");
        Assert.Contains(result.Blocks, b => b.RuleId == "mechanism");
    }

    /// <summary>Tried, no issue: a pin-ended member chain (released both ends) carrying a transverse load is a mechanism, with or without a stiff neighbour.</summary>
    [Fact]
    public void Released_Chain_Under_Transverse_Load_Is_Detected()
    {
        var frame = AllCalculators.Get(FrameCalculator.Id);
        var set = WithOverride(frame.Template,
            FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 3, 0, FrameCalculator.SupportFree), (3, 6, 0, FrameCalculator.SupportPin)),
            FrameCalculator.Elements((1, 1, 2, 1e6, 1e12, true, true), (2, 2, 3, 1e6, 1e12, true, true)),
            FrameCalculator.Loads((1, "nodal", 2, 0, 0, 0, 0, 0, -1, 0)),
            CalcInputValue.Of("n_div", 4));
        var result = CalcRunner.Run(frame, set);
        Assert.True(result.IsBlocked, "a released chain with a transverse load was solved");
    }

    /// <summary>Tried, no issue: every property at its hard limit (E, A, I, lengths of 10⁻⁸ m and 10⁵ m, loads of 10⁷ kN) never throws; it blocks or produces finite outputs.</summary>
    [Theory]
    [InlineData(1e-8, 1e4, 1e6, 1e12, 1e7)]
    [InlineData(1e5, 1e-3, 1e-6, 1e-9, 1e7)]
    [InlineData(1e-8, 1e-3, 1e-6, 1e-9, 1e-9)]
    [InlineData(1e5, 1e4, 1e6, 1e12, -1e7)]
    public void Frame_Extreme_Values_Never_Throw(double length, double eGpa, double aCm2, double iCm4, double fKn)
    {
        var frame = AllCalculators.Get(FrameCalculator.Id);
        var set = WithOverride(frame.Template,
            CalcInputValue.Quantity("E", eGpa, "GPa"),
            FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportFixed), (2, length, 0, FrameCalculator.SupportFree)),
            FrameCalculator.Elements((1, 1, 2, aCm2, iCm4)),
            FrameCalculator.Loads((1, "nodal", 2, 0, 0, 0, 0, 0, fKn, 0), (1, "udl", 1, 0, 0, fKn, 0, 0, 0, 0)),
            CalcInputValue.Of("n_div", 100));

        var ex = Record.Exception(() =>
        {
            var result = CalcRunner.Run(frame, set);
            var text = RenderText(frame, set, result);
            Assert.DoesNotContain("undefined", text, StringComparison.Ordinal);
            Assert.True(text.Length < 400_000);
        });
        Assert.Null(ex);
    }

    // ------------------------------------------------------------------
    // Polygon sections
    // ------------------------------------------------------------------

    /// <summary>Tried, no issue: a 2,000-vertex star, a bow-tie, collinear points, duplicate points, 200 loops and hard-limit coordinates never throw and stay in budget.</summary>
    [Fact]
    public void Polygon_Pathologies_Never_Throw_And_Stay_In_Budget()
    {
        var section = AllCalculators.All.Single(c => c.Template.Inputs.Any(i => i.Symbol == "vertices"));
        var star = Enumerable.Range(0, 2000).Select(i =>
        {
            var r = i % 2 == 0 ? 100.0 : 1e-6;
            var a = 2 * Math.PI * i / 2000;
            return (r * Math.Cos(a), r * Math.Sin(a));
        }).ToList();
        var loops = Enumerable.Range(0, 200).Select(k => ("mm", (IReadOnlyList<(double, double)>)[(k * 1.0, 0.0), (k + 0.5, 0.0), (k + 0.5, 0.5)])).ToArray();

        var cases = new List<CalcInputValue>
        {
            SectionPropertiesCalculator.Polygon(("mm", star)),
            SectionPropertiesCalculator.Polygon(("mm", [(0, 0), (100, 100), (100, 0), (0, 100)])),
            SectionPropertiesCalculator.Polygon(("mm", [(0, 0), (1, 0), (2, 0)])),
            SectionPropertiesCalculator.Polygon(("mm", [(5, 5), (5, 5), (5, 5), (5, 5)])),
            SectionPropertiesCalculator.Polygon(("mm", [(-1e5, -1e5), (1e5, -1e5), (1e5, 1e5), (-1e5, 1e5)])),
            SectionPropertiesCalculator.Polygon(("mm", [(0, 0), (1e-9, 0), (1e-9, 1e-9)])),
            SectionPropertiesCalculator.Polygon(loops),
            SectionPropertiesCalculator.Polygon(("mm", [(0, 0), (100, 0), (100, 100), (0, 100)]), ("mm", [(0, 0), (100, 0), (100, 100), (0, 100)])),
        };

        foreach (var vertices in cases)
        {
            var set = WithOverride(section.Template, CalcInputValue.Choice("shape", SectionPropertiesCalculator.ShapePolygon), vertices);
            var sw = Stopwatch.StartNew();
            var ex = Record.Exception(() =>
            {
                var result = CalcRunner.Run(section, set);
                var text = RenderText(section, set, result);
                Assert.DoesNotContain("undefined", text, StringComparison.Ordinal);
            });
            Assert.True(ex is null, $"{vertices.Rows!.Count} vertices: {ex?.GetType().Name}: {ex?.Message}");
            Assert.True(sw.Elapsed < Budget, $"{vertices.Rows!.Count} vertices took {sw.Elapsed.TotalSeconds:F1} s");
        }
    }

    // ------------------------------------------------------------------
    // Every calculator: numeric table cells at the extremes
    // ------------------------------------------------------------------

    /// <summary>Tried, no issue: every numeric input and table column at its hard limits, at zero and negated, with 500 rows, never throws and never prints an undefined value.</summary>
    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Extremes_In_Every_Numeric_Input_Never_Throw(string id)
    {
        var calculator = AllCalculators.Get(id);
        var template = calculator.Template;

        foreach (var pick in new Func<double?, double?, double>[] { (lo, hi) => hi ?? 1e6, (lo, hi) => lo ?? -1e6, (_, _) => 0, (_, _) => 1e-300, (_, _) => -1e-300 })
        {
            var overrides = new List<CalcInputValue>();

            foreach (var spec in template.Inputs)
            {
                switch (spec.Kind)
                {
                    case CalcInputKind.Quantity:
                        overrides.Add(CalcInputValue.Quantity(spec.Symbol, pick(spec.HardMin, spec.HardMax), spec.DefaultUnit));
                        break;
                    case CalcInputKind.Number:
                        overrides.Add(CalcInputValue.Of(spec.Symbol, pick(spec.HardMin, spec.HardMax)));
                        break;
                    case CalcInputKind.Vector:
                        overrides.Add(CalcInputValue.VectorOf(spec.Symbol, Enumerable.Repeat(pick(null, null), 3).ToList(), spec.DefaultUnit));
                        break;
                    case CalcInputKind.Table when spec.Columns is { Count: > 0 } columns:
                    {
                        var rows = Enumerable.Range(0, Math.Min(spec.MaxRows, 500)).Select(_ => new CalcRow(columns.ToDictionary(c => c.Key, c => c.Kind switch
                        {
                            CalcInputKind.Quantity => new CalcCell(pick(c.HardMin, c.HardMax), c.DefaultUnit),
                            CalcInputKind.Number => new CalcCell(pick(c.HardMin, c.HardMax)),
                            CalcInputKind.Choice => new CalcCell(Text: c.Choices?[0]),
                            CalcInputKind.Boolean => new CalcCell(Flag: true),
                            _ => new CalcCell(Text: "x"),
                        }, StringComparer.Ordinal))).ToList();
                        overrides.Add(CalcInputValue.Table(spec.Symbol, rows));
                        break;
                    }
                }
            }

            var set = WithOverride(template, [.. overrides]);
            var sw = Stopwatch.StartNew();
            var ex = Record.Exception(() =>
            {
                var result = CalcRunner.Run(calculator, set);
                var text = RenderText(calculator, set, result);
                Assert.DoesNotContain(" = undefined", text, StringComparison.Ordinal);
                Assert.True(text.Length < 400_000, $"sheet is {text.Length} characters");
            });
            Assert.True(ex is null, $"{id}: {ex?.GetType().Name}: {ex?.Message}");
            Assert.True(sw.Elapsed < Budget, $"{id} took {sw.Elapsed.TotalSeconds:F1} s");
        }
    }
}
