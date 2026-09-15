using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;
using static Tempest.Core.Tests.Calculations.Modules.ModuleTestSupport;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// The guard conventions every module shares (`WP 21.7A`, the offending
/// value shown in the form's own unit since `WP 21.7C`): a constraint line
/// names what was received in the unit the descriptor gives that input —
/// "Received 210 GPa.", "Received 3 mm." — never the SI base value; a
/// refusal is recorded, not thrown; the acceptance comparison carries the
/// framework's slack.
/// </summary>
public class ModuleGuardsTests
{
    /// <summary>A record with no descriptor: quantities show in their own unit, everything else as itself.</summary>
    private sealed record Probe(Quantity<Length> Span, Quantity<Pressure>? Modulus, double Factor, int Count, string Grade);

    private static BeamDeflectionInput Beam(double spanMillimetres, double limitMillimetres) => new(
        SteelPin,
        BeamSupport.SimplySupported,
        BeamLoading.PointLoad,
        new Quantity<Force>(10, ForceUnits.Kilonewton),
        new Quantity<Length>(spanMillimetres, LengthUnits.Millimetre),
        new Quantity<Pressure>(210e9, PressureUnits.Pascal),
        new Quantity<SecondMomentOfArea>(2.0e-6, SecondMomentOfAreaUnits.MetreToTheFourth),
        new Quantity<Length>(0.05, LengthUnits.Metre),
        new Quantity<Pressure>(355e6, PressureUnits.Pascal),
        new Quantity<Length>(limitMillimetres, LengthUnits.Millimetre));

    [Fact]
    public void Describe_ShowsAModuleInput_InTheDescriptorsOwnUnit_WhateverUnitItArrivedIn()
    {
        var input = Beam(2000, 8);

        // Arrived in Pa, m, m^4 and kN; the beam form shows GPa, mm, mm^4 and kN.
        Assert.Equal("210 GPa", ModuleGuards.Describe(input, nameof(input.YoungsModulus)));
        Assert.Equal("2000 mm", ModuleGuards.Describe(input, nameof(input.Span)));
        Assert.Equal("50 mm", ModuleGuards.Describe(input, nameof(input.ExtremeFibreDistance)));
        Assert.Equal("2000000 mm^4", ModuleGuards.Describe(input, nameof(input.SecondMomentOfArea)));
        Assert.Equal("355 MPa", ModuleGuards.Describe(input, nameof(input.AllowableBendingStress)));
        Assert.Equal("10 kN", ModuleGuards.Describe(input, nameof(input.Load)));
        Assert.Equal("8 mm", ModuleGuards.Describe(input, nameof(input.DeflectionLimit)));
    }

    [Fact]
    public void Describe_WithoutADescriptor_ShowsAQuantityInItsOwnUnit_AndTheRestAsThemselves()
    {
        var probe = new Probe(new Quantity<Length>(1.23456789, LengthUnits.Metre), null, 0.4960317460317461, 3, "ISO 898-1 class 8.8");

        Assert.Equal("1.23457 m", ModuleGuards.Describe(probe, nameof(probe.Span)));
        Assert.Equal("nothing", ModuleGuards.Describe(probe, nameof(probe.Modulus)));
        Assert.Equal("0.496032", ModuleGuards.Describe(probe, nameof(probe.Factor)));
        Assert.Equal("3", ModuleGuards.Describe(probe, nameof(probe.Count)));
        Assert.Equal("'ISO 898-1 class 8.8'", ModuleGuards.Describe(probe, nameof(probe.Grade)));
        Assert.Throws<ArgumentException>(() => ModuleGuards.Describe(probe, "NoSuchInput"));
    }

    [Fact]
    public void Require_RecordsTheCheckWithTheValueInTheFormsUnit_AndRejectsInThoseWords()
    {
        var context = new CalculationContext();
        var input = Beam(2000, 8);

        ModuleGuards.Require(context, "Span must be positive.", true, input, nameof(input.Span));
        var check = Assert.Single(context.ConstraintChecks);
        Assert.Equal("Span must be positive.", check.Description);
        Assert.True(check.IsSatisfied);
        Assert.Equal("Received 2000 mm.", check.Detail);

        var zero = Beam(2000, 0);
        var rejected = Assert.Throws<CalculationInputInvalidException>(() =>
            ModuleGuards.Require(context, "Deflection limit must be positive.", false, zero, nameof(zero.DeflectionLimit)));
        Assert.Equal("Deflection limit must be positive. Received 0 mm.", rejected.Message);
        Assert.Contains(context.ConstraintChecks, c => c.Description == "Deflection limit must be positive." && !c.IsSatisfied && c.Detail == "Received 0 mm.");

        // The plain-text overload is unchanged for derived and non-quantity values.
        ModuleGuards.Require(context, "At least one bolt is required.", true, "4 bolt(s)");
        Assert.Contains(context.ConstraintChecks, c => c.Description == "At least one bolt is required." && c.Detail == "Received 4 bolt(s).");
    }

    [Fact]
    public void Refuse_RecordsTheLimitAsUnsatisfied_AndHandsBackTheReason_WithoutThrowing()
    {
        var context = new CalculationContext();

        var reason = ModuleGuards.Refuse(context, "Span-to-depth ratio must be at least 10.", "Refused: the ratio is 2, below 10.");

        Assert.Equal("Refused: the ratio is 2, below 10.", reason);
        var check = Assert.Single(context.ConstraintChecks);
        Assert.False(check.IsSatisfied);
        Assert.Equal("Refused: the ratio is 2, below 10.", check.Detail);
    }

    [Fact]
    public void IsMet_CarriesTheFrameworksSlack_AndOutcomeOfNamesTheFinding()
    {
        Assert.True(ModuleGuards.IsMet(1.0));
        Assert.True(ModuleGuards.IsMet(1.0 + ModuleGuards.AcceptanceRelativeTolerance / 2));
        Assert.False(ModuleGuards.IsMet(1.0 + ModuleGuards.AcceptanceRelativeTolerance * 10));
        Assert.Equal(EngineeringCheckOutcome.MeetsCriteria, ModuleGuards.OutcomeOf(true));
        Assert.Equal(EngineeringCheckOutcome.DoesNotMeetCriteria, ModuleGuards.OutcomeOf(false));
    }
}
