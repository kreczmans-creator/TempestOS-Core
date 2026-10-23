using System.Diagnostics;
using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Tests.Engine;

/// <summary>
/// Red team (offensive review): cap breaches in the expression engine
/// under the DEFAULT limits, which is what every calculator runs with.
/// Every test asserts the secure behaviour (a cap bites before the work
/// is done, or the work stays inside the 5 s budget).
/// </summary>
public sealed class Red_EngineAttacks
{
    /// <summary>The CI runners scale every deadline by <c>TEMPEST_TEST_TIMEOUT_FACTOR</c> (3 on the Windows matrix); the budgets here scale the same way.</summary>
    private static readonly double TimeoutFactor = double.TryParse(Environment.GetEnvironmentVariable("TEMPEST_TEST_TIMEOUT_FACTOR"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) && f >= 1 ? f : 1;

    private static ICalcSession Session() => CalcEngine.Instance.CreateSession();

    private static ICalcSession WithMillionVector()
    {
        var s = Session();
        s.Define("w", new VectorValue(new double[CalcLimits.Default.MaxArrayElements], Dimension.None), record: false);
        return s;
    }

    /// <summary>R-10: a variadic reducer materialises every element of every argument as an object before any cap is looked at: 64 × 1,000,000 elements is 64 M allocations, several GB, and seconds — inside one expression of 200 characters.</summary>
    [Theory]
    [InlineData("sum")]
    [InlineData("max")]
    [InlineData("vec")]
    public void Variadic_Reducers_Over_Maximal_Vectors_Stay_In_Budget(string function)
    {
        var s = WithMillionVector();
        var expression = function + "(" + string.Join(", ", Enumerable.Repeat("w", CalcLimits.Default.MaxFunctionArguments)) + ")";
        var before = GC.GetTotalAllocatedBytes(true);
        var sw = Stopwatch.StartNew();
        var ex = Record.Exception(() => s.Evaluate(expression));
        sw.Stop();
        var allocated = GC.GetTotalAllocatedBytes(true) - before;
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5 * TimeoutFactor), $"{function} over 64 maximal vectors took {sw.Elapsed.TotalSeconds:F1} s and allocated {allocated / 1_000_000} MB ({ex?.GetType().Name ?? "completed"})");
        Assert.True(allocated < 500_000_000, $"{function} over 64 maximal vectors allocated {allocated / 1_000_000} MB before any cap applied");
    }

    /// <summary>Tried, no issue: element-wise arithmetic on maximal vectors is linear and fast, and the array cap bites on vec(w, w).</summary>
    [Fact]
    public void Vector_Arithmetic_At_The_Cap_Is_Fast_And_Capped()
    {
        var s = WithMillionVector();
        var sw = Stopwatch.StartNew();
        _ = s.Evaluate("w * 2 + w / 3 - abs(w)");
        Assert.True(sw.ElapsedMilliseconds < 5_000);
        Assert.Throws<CalcLimitException>(() => s.Evaluate("vec(w, w)"));
    }

    /// <summary>Tried, no issue: the widest matrix an expression can state (bounded by the token cap) is tiny; inv/det/solve on it are instant, and the matrix order cap still bites on a defined one.</summary>
    [Fact]
    public void Matrix_Work_Is_Bounded_By_The_Token_Cap()
    {
        var s = Session();
        var row = string.Join(", ", Enumerable.Range(1, 20).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var rows = Enumerable.Range(0, 20).Select(r => string.Join(", ", Enumerable.Range(1, 20).Select(c => (r == c ? 40 : 1).ToString(System.Globalization.CultureInfo.InvariantCulture))));
        s.Assign("M", "[" + string.Join("; ", rows) + "]");
        var sw = Stopwatch.StartNew();
        _ = s.Evaluate("det(inv(M) * M)");
        _ = s.Evaluate("solve(M, [" + row + "])");
        Assert.True(sw.ElapsedMilliseconds < 5_000);

        var big = new double[CalcLimits.Default.MaxMatrixOrder + 1, 1];
        s.Define("B", new MatrixValue(big, Dimension.None), record: false);
        Assert.Throws<CalcLimitException>(() => s.Evaluate("B * B"));
    }

    /// <summary>Tried, no issue: 1,000 tokens of left-associated operators evaluate and substitute without a deep stack.</summary>
    [Fact]
    public void Long_Flat_Chains_Do_Not_Overflow()
    {
        var s = Session();
        var chain = string.Join(" + ", Enumerable.Repeat("1", 499));
        var step = s.Assign("a", chain);
        Assert.Equal(499, ((ScalarValue)step.Result).Si);
        var mul = string.Join(" * ", Enumerable.Repeat("1", 499));
        s.Assign("b", mul);
        s.Define("t", BoolValue.True, record: false);
        var cmp = string.Join(" and ", Enumerable.Repeat("t", 499));
        s.Assign("c", cmp);
    }

    /// <summary>Tried, no issue: every non-finite path becomes a block-grade exception at the step, never a NaN on the sheet.</summary>
    [Theory]
    [InlineData("10 ^ 400")]
    [InlineData("0 ^ (-1)")]
    [InlineData("(-8) ^ (1/3)")]
    [InlineData("exp(1000) - exp(1000)")]
    [InlineData("exp(1000) * 0")]
    [InlineData("sqrt(exp(1000) - exp(1000))")]
    [InlineData("mod(1, exp(1000) - exp(1000))")]
    [InlineData("1e308 * 10")]
    public void Non_Finite_Results_Are_Refused_At_The_Step(string expression)
    {
        var s = Session();
        Assert.ThrowsAny<CalcException>(() => s.Assign("x", expression));
        Assert.Null(s.Find("x"));
    }

    /// <summary>Tried, no issue: huge, negative, NaN and fractional indices and decimals never index outside or crash.</summary>
    [Theory]
    [InlineData("w[1e300]")]
    [InlineData("w[-1e300]")]
    [InlineData("w[2147483648]")]
    [InlineData("w[exp(1000) - exp(1000)]")]
    [InlineData("w[0.5]")]
    [InlineData("w[0]")]
    [InlineData("round(1.5, 1e300)")]
    [InlineData("round(1.5, -1e300)")]
    [InlineData("interp(exp(1000) - exp(1000), [1, 2], [3, 4])")]
    [InlineData("interp(1, [exp(1000) - exp(1000), 1], [3, 4])")]
    [InlineData("interp(1, [1, 1], [3, 4])")]
    public void Hostile_Indices_And_Arguments_Are_Refused_Cleanly(string expression)
    {
        var s = Session();
        s.Define("w", new VectorValue([1, 2, 3], Dimension.None), record: false);
        var ex = Record.Exception(() => s.Evaluate(expression));
        Assert.True(ex is null || ex is CalcException, $"{expression} threw {ex?.GetType().Name}");
    }

    /// <summary>Tried, no issue: the unit sub-parser is capped at 64 characters however it is reached (number suffix, 'in', vector unit).</summary>
    [Theory]
    [InlineData("2 m/(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s*(s)))))))))))))))))))))))))")]
    [InlineData("2 in m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m")]
    [InlineData("2 mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm")]
    public void Unit_Parsing_Is_Capped(string expression)
    {
        var s = Session();
        Assert.ThrowsAny<CalcException>(() => s.Evaluate(expression));
    }

    /// <summary>Tried, no issue: a literal padded with thousands of underscores is cheap, and an identifier made of exotic letters is just a symbol.</summary>
    [Fact]
    public void Lexer_Oddities_Are_Harmless()
    {
        var s = Session();
        var padded = "1" + new string('_', 3_000) + "1";
        var sw = Stopwatch.StartNew();
        Assert.Equal(11, ((ScalarValue)s.Evaluate(padded)).Si);
        Assert.ThrowsAny<CalcException>(() => s.Evaluate("𝔘𝔫𝔦𝔠𝔬𝔡𝔢 + ﬃ + Ω"));
        Assert.ThrowsAny<CalcException>(() => s.Evaluate("\"" + new string('x', 2_001) + "\""));
        Assert.True(sw.ElapsedMilliseconds < 1_000);
    }

    /// <summary>Tried, no issue: the step and variable caps hold for Record and Define as well as Assign.</summary>
    [Fact]
    public void Record_And_Define_Respect_The_Caps()
    {
        var s = CalcEngine.Instance.CreateSession(CalcLimits.Tight);
        Assert.Throws<CalcLimitException>(() => s.Record("x", new string('f', 201), "1", new ScalarValue(1)));
        Assert.Throws<CalcLimitException>(() => s.Record("x", "f", new string('s', 201), new ScalarValue(1)));
        Assert.Throws<CalcLimitException>(() => s.Define("t", new TextValue(new string('t', 101))));
        Assert.ThrowsAny<CalcException>(() => s.Record("x", "f", "s", new ScalarValue(double.NaN)));
        Assert.ThrowsAny<CalcException>(() => s.Define("if", new ScalarValue(1)));
        Assert.ThrowsAny<CalcException>(() => s.Define("a\u0000", new ScalarValue(1)));
    }
}
