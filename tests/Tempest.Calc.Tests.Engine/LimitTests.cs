using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Analysis;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Tests.Engine;

public sealed class LimitTests
{
    private static ICalcSession Tight() => CalcEngine.Instance.CreateSession(CalcLimits.Tight);

    [Fact]
    public void Expression_Length_Is_Capped()
    {
        var e = Assert.Throws<CalcLimitException>(() => Tight().Evaluate(new string('1', 201)));
        Assert.Equal(nameof(CalcLimits.MaxExpressionLength), e.Limit);
    }

    [Fact]
    public void Token_Count_Is_Capped()
    {
        var expression = string.Join("+", Enumerable.Repeat("1", 40));
        var e = Assert.Throws<CalcLimitException>(() => Tight().Evaluate(expression));
        Assert.Equal(nameof(CalcLimits.MaxTokens), e.Limit);
    }

    [Fact]
    public void Nesting_Depth_Is_Capped()
    {
        var expression = new string('(', 10) + "1" + new string(')', 10);
        var e = Assert.Throws<CalcLimitException>(() => Tight().Evaluate(expression));
        Assert.Equal(nameof(CalcLimits.MaxNestingDepth), e.Limit);
    }

    [Fact]
    public void Deep_Nesting_Of_Unary_Minus_Is_Capped()
    {
        var expression = new string('-', 10) + "1";
        Assert.Throws<CalcLimitException>(() => Tight().Evaluate(expression));
    }

    [Fact]
    public void Deep_Nesting_Of_Calls_Is_Capped()
    {
        var expression = string.Concat(Enumerable.Repeat("abs(", 10)) + "1" + new string(')', 10);
        Assert.Throws<CalcLimitException>(() => Tight().Evaluate(expression));
    }

    [Fact]
    public void Deep_Nesting_With_Default_Limits_Does_Not_Overflow_The_Stack()
    {
        var expression = new string('(', 5_000) + "1" + new string(')', 5_000);
        Assert.ThrowsAny<CalcLimitException>(() => CalcEngine.Instance.CreateSession().Evaluate(expression));
    }

    [Fact]
    public void Literal_Length_Is_Capped()
    {
        var e = Assert.Throws<CalcLimitException>(() => Tight().Evaluate("1." + new string('1', 20)));
        Assert.Equal(nameof(CalcLimits.MaxLiteralLength), e.Limit);
    }

    [Fact]
    public void Huge_Exponent_Literal_Is_Refused() => Assert.ThrowsAny<CalcException>(() => Tight().Evaluate("1e99999999999"));

    [Fact]
    public void Symbol_Length_Is_Capped()
    {
        var e = Assert.Throws<CalcLimitException>(() => Tight().Evaluate(new string('a', 20)));
        Assert.Equal(nameof(CalcLimits.MaxSymbolLength), e.Limit);
    }

    [Fact]
    public void Text_Length_Is_Capped() => Assert.Throws<CalcLimitException>(() => Tight().Evaluate("\"" + new string('x', 150) + "\""));

    [Fact]
    public void Step_Count_Is_Capped()
    {
        var s = Tight();

        for (var i = 0; i < 20; i++)
            s.Assign($"x{i}", "1");

        var e = Assert.Throws<CalcLimitException>(() => s.Assign("y", "1"));
        Assert.Equal(nameof(CalcLimits.MaxSteps), e.Limit);
    }

    [Fact]
    public void Variable_Count_Is_Capped()
    {
        var s = Tight();

        for (var i = 0; i < 30; i++)
            s.Define($"v{i}", new ScalarValue(1), record: false);

        Assert.Throws<CalcLimitException>(() => s.Define("w", new ScalarValue(1), record: false));
    }

    [Fact]
    public void Array_Literal_Size_Is_Capped()
    {
        var s = CalcEngine.Instance.CreateSession(CalcLimits.Tight with { MaxTokens = 10_000, MaxExpressionLength = 100_000 });
        var expression = "[" + string.Join(",", Enumerable.Repeat("1", 101)) + "]";
        var e = Assert.Throws<CalcLimitException>(() => s.Evaluate(expression));
        Assert.Equal(nameof(CalcLimits.MaxArrayElements), e.Limit);
    }

    [Fact]
    public void Defined_Array_Size_Is_Capped() => Assert.Throws<CalcLimitException>(() => Tight().Define("v", new VectorValue(new double[101], Tempest.Calc.Engine.Units.Dimension.None)));

    [Fact]
    public void Matrix_Order_Is_Capped()
    {
        var s = Tight();
        s.Define("M", new MatrixValue(new double[9, 9], Tempest.Calc.Engine.Units.Dimension.None), record: false);
        Assert.Throws<CalcLimitException>(() => s.Evaluate("det(M)"));
    }

    [Fact]
    public void Function_Argument_Count_Is_Capped()
    {
        var expression = "max(" + string.Join(",", Enumerable.Repeat("1", 9)) + ")";
        Assert.Throws<CalcLimitException>(() => Tight().Evaluate(expression));
    }

    [Fact]
    public void Monte_Carlo_Sample_Count_Is_Capped() =>
        Assert.Throws<CalcLimitException>(() => MonteCarlo.Run(_ => 1.0, new Dictionary<string, double>(), new Dictionary<string, Distribution>(), samples: MonteCarlo.MaxSamples + 1));

    [Fact]
    public void Monte_Carlo_Zero_Samples_Is_Refused() =>
        Assert.Throws<CalcLimitException>(() => MonteCarlo.Run(_ => 1.0, new Dictionary<string, double>(), new Dictionary<string, Distribution>(), samples: 0));

    [Fact]
    public void Monte_Carlo_Bin_Count_Is_Capped() =>
        Assert.Throws<CalcLimitException>(() => MonteCarlo.Run(_ => 1.0, new Dictionary<string, double>(), new Dictionary<string, Distribution>(), bins: MonteCarlo.MaxBins + 1));

    [Fact]
    public void Tornado_Input_Count_Is_Capped()
    {
        var inputs = Enumerable.Range(0, Sensitivity.MaxInputs + 1).Select(i => $"x{i}").ToList();
        var nominal = inputs.ToDictionary(i => i, _ => 1.0, StringComparer.Ordinal);
        Assert.Throws<CalcLimitException>(() => Sensitivity.Tornado(_ => 1.0, nominal, inputs));
    }

    [Fact]
    public void Root_Finding_Iterations_Are_Capped() =>
        Assert.Throws<CalcLimitException>(() => Tempest.Calc.Engine.Numerics.RootFinding.Bisect(x => x - 0.123456789, 0, 1, tolerance: 0, maxIterations: 5));

    [Fact]
    public void Fixed_Point_Iterations_Are_Capped() =>
        Assert.Throws<CalcLimitException>(() => Tempest.Calc.Engine.Numerics.RootFinding.FixedPoint(x => 0.999999 * x + 1, 0, relativeTolerance: 1e-15, maxIterations: 10));

    [Fact]
    public void Invalid_Limits_Are_Refused() => Assert.Throws<ArgumentOutOfRangeException>(() => CalcEngine.Instance.CreateSession(new CalcLimits(MaxSteps: 0)));

    [Fact]
    public void A_Million_Element_Vector_Within_Default_Limits_Is_Fine()
    {
        var s = CalcEngine.Instance.CreateSession();
        s.Define("v", new VectorValue(new double[1_000_000], Tempest.Calc.Engine.Units.Dimension.None), record: false);
        Assert.Equal(1_000_000.0, ((ScalarValue)s.Evaluate("len(v)")).Si);
    }
}
