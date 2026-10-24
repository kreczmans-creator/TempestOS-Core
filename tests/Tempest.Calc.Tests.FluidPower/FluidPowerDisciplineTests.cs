using Tempest.Calc.FluidPower;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.FluidPower;

/// <summary>ADR-0170: the Fluid Power discipline as a calculation discipline — the enum, the assembly and the definition-of-done checks over its calculators.</summary>
public sealed class FluidPowerDisciplineTests
{
    [Fact]
    public void Every_Shipped_Fluid_Power_Calculator_Meets_The_Common_Definition_Of_Done()
    {
        // A theory over an empty list fails ("no data"); this passes vacuously now and covers F1–F3 the moment they are added,
        // reporting every failed check of every calculator together.
        CommonChecks.RunAll(FluidPowerCalculators.All);
    }

    [Fact]
    public void The_Harness_Fixture_Meets_The_Same_Checks()
    {
        CommonChecks.Run(new HarnessFixtureCalculator());
    }

    [Fact]
    public void Every_Shipped_Calculator_Is_A_Fluid_Power_One_With_A_Unique_Id()
    {
        Assert.All(FluidPowerCalculators.All, c => Assert.Equal(CalcDiscipline.FluidPower, c.Template.Discipline));
        Assert.Equal(FluidPowerCalculators.All.Count, FluidPowerCalculators.All.Select(c => c.Template.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Adding_The_Discipline_Appended_It_And_Left_The_Existing_Names_Alone()
    {
        // The template hash writes the discipline by NAME, so appending a member cannot change any existing template's hash;
        // this pins both facts (a rename or reorder of the first four would).
        Assert.Equal(["Mechanical", "Structural", "Electrical", "BuildingServices", "FluidPower"], Enum.GetNames<CalcDiscipline>());
        Assert.Contains("discipline=FluidPower\n", CalcTemplateHasher.CanonicalText(new HarnessFixtureCalculator().Template, []), StringComparison.Ordinal);
    }

    [Fact]
    public void The_Fixture_Sheet_Names_Its_Discipline_And_Computes_Pascal()
    {
        var calculator = new HarnessFixtureCalculator();
        var result = CalcRunner.Run(calculator, CalcTemplateHasher.DefaultInputs(calculator.Template));
        Assert.False(result.IsBlocked);
        Assert.Equal(10_000.0, result.OutputSi("F"), 6);
    }

    [Fact]
    public void A_Fluid_Power_Template_Hash_Differs_From_The_Same_Template_Filed_Under_Another_Discipline()
    {
        var fixture = new HarnessFixtureCalculator();
        var elsewhere = new HarnessFixtureCalculator().Template with { Discipline = CalcDiscipline.Mechanical };
        Assert.NotEqual(CalcTemplateHasher.CanonicalText(fixture.Template, []), CalcTemplateHasher.CanonicalText(elsewhere, []));
    }

    [Fact]
    public void The_Common_Checks_Report_Every_Failure_Not_Just_The_First()
    {
        // B-0 review, LOW 8: a calculator that is in the wrong discipline AND gets its known answers wrong says both.
        var broken = new BrokenFixtureCalculator();
        var failures = CommonChecks.Collect([broken]);

        Assert.Contains(failures, f => f.Contains("/ discipline:", StringComparison.Ordinal));
        Assert.Contains(failures, f => f.Contains("/ known answers:", StringComparison.Ordinal));
        Assert.True(failures.Count >= 2);
        Assert.Empty(CommonChecks.Collect([new HarnessFixtureCalculator()]));

        var thrown = Assert.ThrowsAny<Exception>(() => CommonChecks.RunAll([broken]));
        Assert.Contains("/ discipline:", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("/ known answers:", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Common_Checks_Pass_Vacuously_On_An_Empty_List_And_Refuse_A_Null_One()
    {
        Assert.Empty(CommonChecks.Collect([]));
        Assert.Throws<ArgumentNullException>(() => CommonChecks.Collect(null!));
    }
}

/// <summary>The harness fixture filed under the wrong discipline with known answers that are wrong: two checks fail at once.</summary>
internal sealed class BrokenFixtureCalculator : ICalcCalculator
{
    private readonly HarnessFixtureCalculator _inner = new();

    public CalcTemplate Template => _inner.Template with
    {
        Discipline = CalcDiscipline.Mechanical,
        TestCases = [.. _inner.Template.TestCases!.Select(c => c with { Expected = [new CalcExpectedValue("F", 1, "kN", 1e-9)] })],
    };

    public void Run(CalcRunContext c) => _inner.Run(c);
}
