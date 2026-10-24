using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Analysis.ToleranceStack;
using Tempest.Calc.Engine.Physics;

namespace Tempest.Calc.Tests.Engine;

/// <summary>B-0 review, LOW 2 and LOW 3: the tolerance-stack engine lives in the engine assembly and checks what it is given.</summary>
public sealed class ToleranceStackEngineGuardTests
{
    private static Contributor Good(string name = "a") => Contributor.Size(name, 1, 10, 0.1, -0.1);

    [Fact]
    public void The_Stack_Engine_Is_In_The_Engine_Assembly_Beside_The_Gas_Law_Kernel()
    {
        // ADR-0170 §3: Fluid Power depends on Engine and Sheets only, so F3 can call the engine without a reference to Mechanical.
        Assert.Equal(typeof(GasLaw).Assembly, typeof(ToleranceStackEngine).Assembly);
        Assert.Equal("Tempest.Calc.Engine.Analysis.ToleranceStack", typeof(ToleranceStackEngine).Namespace);
        Assert.Equal("Tempest.Calc.Engine", typeof(Contributor).Assembly.GetName().Name);
    }

    [Theory]
    [InlineData(2.0, 0.1, 0.0)]                    // sign
    [InlineData(0.0, 0.1, 0.0)]
    [InlineData(1.0, -0.1, 0.0)]                   // negative half-band
    [InlineData(1.0, double.NaN, 0.0)]
    [InlineData(1.0, double.PositiveInfinity, 0.0)]
    [InlineData(1.0, 0.1, -1.0)]                   // negative sigma
    [InlineData(1.0, 0.1, double.NaN)]
    public void A_Contributor_Built_With_The_Positional_Constructor_Is_Checked_Like_One_From_The_Factories(double sign, double half, double sigma)
    {
        var bad = new Contributor("bad", sign, 10, half, 10, ContributorDistribution.Normal, sigma);
        Assert.NotNull(bad.Problem());

        var analyse = Assert.Throws<CalcEvaluationException>(() => ToleranceStackEngine.Analyse([Good(), bad]));
        Assert.Contains("Contributor 2 (bad)", analyse.Message, StringComparison.Ordinal);
        Assert.Throws<CalcEvaluationException>(() => ToleranceStackEngine.Simulate([Good(), bad], 100, 1));
    }

    [Fact]
    public void A_Non_Finite_Nominal_Or_Mean_Or_An_Undefined_Distribution_Or_A_Missing_Entry_Is_Refused()
    {
        Assert.Throws<CalcEvaluationException>(() => ToleranceStackEngine.Analyse([Good() with { Nominal = double.NaN }]));
        Assert.Throws<CalcEvaluationException>(() => ToleranceStackEngine.Analyse([Good() with { Mid = double.PositiveInfinity }]));
        Assert.Throws<CalcEvaluationException>(() => ToleranceStackEngine.Analyse([Good() with { Distribution = (ContributorDistribution)99 }]));
        Assert.Throws<CalcEvaluationException>(() => ToleranceStackEngine.Analyse([null!]));
        Assert.Null(Good().Problem());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void The_Modified_Rss_Factor_Must_Be_Positive_And_Finite(double factor)
    {
        var ex = Assert.Throws<CalcEvaluationException>(() => ToleranceStackEngine.Analyse([Good()], null, factor));
        Assert.Contains("modified-RSS factor", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1.5 * 0.1, ToleranceStackEngine.Analyse([Good()], null, 1.5).ModifiedRss, 12);
    }

    [Fact]
    public void Simulate_Checks_The_Contributor_Cap_And_The_Requirement_Like_Analyse()
    {
        var many = Enumerable.Range(0, ToleranceStackEngine.MaxContributors + 1).Select(i => Good("c" + i)).ToList();
        Assert.Throws<CalcLimitException>(() => ToleranceStackEngine.Simulate(many, 10, 1));

        var inverted = Assert.Throws<CalcEvaluationException>(() => ToleranceStackEngine.Simulate([Good()], 10, 1, new StackRequirement(5, 1)));
        Assert.Contains("stated minimum is above the stated maximum", inverted.Message, StringComparison.Ordinal);
        Assert.Throws<CalcEvaluationException>(() => ToleranceStackEngine.Simulate([Good()], 10, 1, new StackRequirement(double.NaN, 1)));
        Assert.Throws<CalcEvaluationException>(() => ToleranceStackEngine.Analyse([Good()], new StackRequirement(null, double.PositiveInfinity)));
    }
}
