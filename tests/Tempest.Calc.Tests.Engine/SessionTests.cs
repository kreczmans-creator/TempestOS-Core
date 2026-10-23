using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Tests.Engine;

public sealed class SessionTests
{
    private static ICalcSession Session() => CalcEngine.Instance.CreateSession();

    [Fact]
    public void Engine_Reports_A_Version() => Assert.Equal("calc-engine/1.0.0", CalcEngine.Instance.EngineVersion);

    [Fact]
    public void Define_Records_An_Input_Step()
    {
        var s = Session();
        var step = s.Define("F", ScalarValue.Of(12, "kN"), "Applied load");
        Assert.Equal(CalcStepKind.Input, step.Kind);
        Assert.Equal("F", step.Formula);
        Assert.Equal("12 kN", step.ResultText);
        Assert.Equal("Applied load", step.Description);
        Assert.Single(s.Steps);
    }

    [Fact]
    public void Define_Without_Record_Binds_Only()
    {
        var s = Session();
        s.Define("F", ScalarValue.Of(12, "kN"), record: false);
        Assert.Empty(s.Steps);
        Assert.NotNull(s.Find("F"));
    }

    [Fact]
    public void Assign_Records_Formula_Substitution_And_Result()
    {
        var s = Session();
        s.Define("F", ScalarValue.Of(12, "kN"));
        s.Define("A", ScalarValue.Of(200, "mm^2"));
        var step = s.Assign("sigma", "F / A", "MPa", "Direct stress", "EN 1993-1-1 §6.2.3");

        Assert.Equal(3, step.Ordinal);
        Assert.Equal(CalcStepKind.Evaluated, step.Kind);
        Assert.Equal("F / A", step.Formula);
        Assert.Equal("12 kN / 200 mm²", step.Substitution);
        Assert.Equal("60 MPa", step.ResultText);
        Assert.Equal("EN 1993-1-1 §6.2.3", step.Clause);
        Assert.Equal(60e6, s.GetScalar("sigma").Si, 6);
    }

    [Fact]
    public void Substitution_Brackets_Lower_Precedence_Operands()
    {
        var s = Session();
        s.Define("a", new ScalarValue(2));
        s.Define("b", new ScalarValue(3));
        s.Define("c", new ScalarValue(4));
        Assert.Equal("(2 + 3) × 4", s.Assign("x", "(a + b) * c").Substitution);
        Assert.Equal("2 + 3 × 4", s.Assign("y", "a + b * c").Substitution);
        Assert.Equal("2 − (3 − 4)", s.Assign("z", "a - (b - c)").Substitution);
        Assert.Equal("2 / (3 × 4)", s.Assign("w", "a / (b * c)").Substitution);
    }

    [Fact]
    public void Substitution_Brackets_Negative_Values()
    {
        var s = Session();
        s.Define("a", new ScalarValue(-2));
        Assert.Equal("(-2) × 3", s.Assign("x", "a * 3").Substitution);
    }

    [Fact]
    public void Substitution_Brackets_Dimensioned_Base_Of_A_Power()
    {
        var s = Session();
        s.Define("d", ScalarValue.Of(20, "mm"));
        Assert.Equal("π × (20 mm)^2 / 4", s.Assign("A", "pi * d^2 / 4", "mm^2").Substitution);
    }

    [Fact]
    public void Substitution_Renders_Functions_And_Conversion()
    {
        var s = Session();
        s.Define("theta", ScalarValue.Of(30, "deg"));
        Assert.Equal("sin(30 deg)", s.Assign("k", "sin(theta)").Substitution);
        s.Define("P", ScalarValue.Of(1500, "N"));
        Assert.Equal("1500 N in kN", s.Assign("Pk", "P in kN").Substitution);
        Assert.Equal("1.5 kN", s.Steps[^1].ResultText);
    }

    [Fact]
    public void Record_Binds_A_Native_Step()
    {
        var s = Session();
        var step = s.Record("f", "Colebrook(Re, ε/D)", "Colebrook(50000, 0.001)", new ScalarValue(0.0233), "Friction factor");
        Assert.Equal(CalcStepKind.Native, step.Kind);
        Assert.Equal(0.0233, s.GetScalar("f").Si);
        Assert.Equal(0.0466, ((ScalarValue)s.Evaluate("2 * f")).Si, 12);
    }

    [Fact]
    public void Steps_Are_Numbered_In_Order()
    {
        var s = Session();
        s.Define("a", new ScalarValue(1));
        s.Assign("b", "a + 1");
        s.Record("c", "b", "2", new ScalarValue(2));
        Assert.Equal([1, 2, 3], s.Steps.Select(x => x.Ordinal).ToArray());
    }

    [Fact]
    public void Reassigning_A_Symbol_Replaces_Its_Value_And_Keeps_Both_Steps()
    {
        var s = Session();
        s.Assign("x", "1");
        s.Assign("x", "x + 1");
        Assert.Equal(2.0, s.GetScalar("x").Si);
        Assert.Equal(2, s.Steps.Count);
    }

    [Fact]
    public void Evaluation_Is_Deterministic()
    {
        static IReadOnlyList<CalcStep> Run()
        {
            var s = Session();
            s.Define("F", ScalarValue.Of(12.3456, "kN"));
            s.Define("A", ScalarValue.Of(197.2, "mm^2"));
            s.Assign("sigma", "F / A", "MPa");
            s.Assign("u", "sigma / (235 MPa)");
            return s.Steps;
        }

        var a = Run();
        var b = Run();
        Assert.Equal(a.Select(x => x.Substitution + x.ResultText), b.Select(x => x.Substitution + x.ResultText));
    }

    [Theory]
    [InlineData("1x")]
    [InlineData("a b")]
    [InlineData("pi")]
    [InlineData("e")]
    [InlineData("if")]
    [InlineData("in")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a-b")]
    public void Invalid_Symbols_Are_Refused(string symbol) => Assert.ThrowsAny<Exception>(() => Session().Assign(symbol, "1"));

    [Theory]
    [InlineData("sigma_max")]
    [InlineData("F'")]
    [InlineData("σ")]
    [InlineData("d₁")]
    [InlineData("x1")]
    public void Valid_Symbols_Are_Accepted(string symbol)
    {
        var s = Session();
        s.Assign(symbol, "1");
        Assert.Contains(symbol, s.Symbols);
    }

    [Theory]
    [InlineData("10 ^ 400")]
    [InlineData("-(10 ^ 400)")]
    [InlineData("0 / 0 * 0")]
    public void Non_Finite_Results_Never_Bind(string expression)
    {
        var s = Session();
        Assert.Throws<CalcEvaluationException>(() => s.Assign("x", expression));
        Assert.Null(s.Find("x"));
        Assert.Empty(s.Steps);
    }

    [Fact]
    public void Defining_A_Nan_Scalar_Is_Allowed_But_Assigning_From_It_Is_Not()
    {
        var s = Session();
        s.Define("n", new ScalarValue(double.NaN), record: false);
        Assert.Throws<CalcEvaluationException>(() => s.Assign("y", "n + 1"));
    }

    [Fact]
    public void Get_Of_An_Unknown_Symbol_Throws() => Assert.Throws<CalcEvaluationException>(() => Session().Get("nope"));

    [Fact]
    public void GetScalar_Of_A_Vector_Throws()
    {
        var s = Session();
        s.Assign("v", "[1, 2]");
        Assert.Throws<CalcEvaluationException>(() => s.GetScalar("v"));
    }

    [Fact]
    public void Assign_With_Incompatible_Display_Unit_Throws() => Assert.Throws<CalcDimensionException>(() => Session().Assign("x", "1 m", "kg"));

    [Fact]
    public void Assign_With_Unknown_Display_Unit_Throws() => Assert.Throws<CalcUnitException>(() => Session().Assign("x", "1 m", "furlong"));

    [Fact]
    public void Significant_Figures_Govern_Result_Text()
    {
        var s = CalcEngine.Instance.CreateSession(significantFigures: 3);
        Assert.Equal("3.14", s.Assign("p", "pi").ResultText);
        var s2 = CalcEngine.Instance.CreateSession(significantFigures: 8);
        Assert.Equal("3.1415927", s2.Assign("p", "pi").ResultText);
    }

    [Fact]
    public void Validate_Accepts_Good_And_Rejects_Bad_Syntax()
    {
        CalcEngine.Instance.Validate("a * b + sin(c)");
        Assert.ThrowsAny<CalcException>(() => CalcEngine.Instance.Validate("a * * b"));
    }

    [Fact]
    public void Vectors_Define_And_Index()
    {
        var s = Session();
        s.Define("xs", VectorValue.Of([1, 2, 3], "m"));
        s.Assign("y", "xs[2]");
        Assert.Equal(2.0, s.GetScalar("y").Si, 12);
        Assert.Equal("m", s.Steps[^1].Result.DisplayUnit.Symbol);
    }

    [Fact]
    public void Text_Inputs_Drive_Conditionals()
    {
        var s = Session();
        s.Define("fit", new TextValue("H7"));
        Assert.Equal(1.0, ((ScalarValue)s.Evaluate("if(fit == \"H7\", 1, 0)")).Si);
    }

    [Fact]
    public void Boolean_Results_Bind()
    {
        var s = Session();
        s.Assign("ok", "1 < 2");
        Assert.Equal(BoolValue.True, s.Get("ok"));
        Assert.Equal("true", s.Steps[0].ResultText);
    }
}
