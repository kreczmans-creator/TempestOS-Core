using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Persistence;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>What every module test does the same way: compare a figure to a hand-calculated one, and run a definition through a bare engine.</summary>
internal static class ModuleTestSupport
{
    /// <summary>A pin to the seeded S355J2 record, as a governed caller would build it.</summary>
    public static readonly ReferencePin SteelPin = new("Materials", "mat-s355j2", 1);

    /// <summary>A second pin, for a joint of two materials.</summary>
    public static readonly ReferencePin PinSteelPin = new("Materials", "mat-1-4404", 2);

    /// <summary>The fastener grade the fastener modules name.</summary>
    public const string FastenerGrade = "ISO 898-1 class 8.8";

    /// <summary>Asserts <paramref name="actual"/> is within <paramref name="relativeTolerance"/> of <paramref name="expected"/>, comparing base values.</summary>
    public static void AssertClose<TDimension>(Quantity<TDimension>? expected, Quantity<TDimension>? actual, double relativeTolerance, string what)
        where TDimension : IDimension
    {
        Assert.True(expected.HasValue, $"{what}: the vector states no expected value.");
        Assert.True(actual.HasValue, $"{what} was not computed.");
        AssertClose(expected.Value.BaseValue, actual.Value.BaseValue, relativeTolerance, what);
    }

    /// <inheritdoc cref="AssertClose{TDimension}(Quantity{TDimension}?, Quantity{TDimension}?, double, string)"/>
    public static void AssertClose<TDimension>(Quantity<TDimension> expected, Quantity<TDimension> actual, double relativeTolerance, string what)
        where TDimension : IDimension =>
        AssertClose(expected.BaseValue, actual.BaseValue, relativeTolerance, what);

    /// <inheritdoc cref="AssertClose{TDimension}(Quantity{TDimension}?, Quantity{TDimension}?, double, string)"/>
    public static void AssertClose<TDimension>(Quantity<TDimension>? expected, Quantity<TDimension> actual, double relativeTolerance, string what)
        where TDimension : IDimension =>
        AssertClose(expected, (Quantity<TDimension>?)actual, relativeTolerance, what);

    /// <inheritdoc cref="AssertClose{TDimension}(Quantity{TDimension}?, Quantity{TDimension}?, double, string)"/>
    public static void AssertClose<TDimension>(Quantity<TDimension> expected, Quantity<TDimension>? actual, double relativeTolerance, string what)
        where TDimension : IDimension =>
        AssertClose((Quantity<TDimension>?)expected, actual, relativeTolerance, what);

    /// <summary>Asserts <paramref name="actual"/> is within <paramref name="relativeTolerance"/> of <paramref name="expected"/>; an expected zero is compared to one part in a million absolutely.</summary>
    public static void AssertClose(double? expected, double? actual, double relativeTolerance, string what)
    {
        Assert.True(expected.HasValue, $"{what}: the vector states no expected value.");
        Assert.True(actual.HasValue, $"{what} was not computed.");

        var tolerance = expected.Value == 0 ? 1e-6 : relativeTolerance * Math.Abs(expected.Value);
        Assert.True(
            Math.Abs(actual.Value - expected.Value) <= tolerance,
            $"{what}: expected {expected.Value}, got {actual.Value} (difference {Math.Abs(actual.Value - expected.Value):E}, tolerance {tolerance:E}).");
    }

    /// <summary>The outcome a vector's meets-criteria flag stands for.</summary>
    public static EngineeringCheckOutcome OutcomeFor(bool? meetsCriteria) =>
        meetsCriteria == true ? EngineeringCheckOutcome.MeetsCriteria : EngineeringCheckOutcome.DoesNotMeetCriteria;

    /// <summary>A bare engine over an in-memory store with the product catalogue registered: the composition a shipped run has.</summary>
    public static CalculationEngine BareEngine()
    {
        var principals = new CurrentPrincipalAccessor();
        var engine = new CalculationEngine(new EngineeringDocumentStore(new InMemoryQueryablePersistenceStore(), principals), principals);
        ProductCalculationCatalogue.RegisterAll(engine);
        return engine;
    }

    /// <summary>
    /// Executes <paramref name="input"/> through a bare engine and reads the
    /// record back, so the result survived serialisation. Returns both.
    /// </summary>
    public static async Task<(CalculationRecord<TResult> Executed, CalculationRecord<TResult> ReadBack)> RoundTripAsync<TInput, TResult>(string calculationId, TInput input)
    {
        var engine = BareEngine();

        var executed = await engine.ExecuteAsync<TInput, TResult>(calculationId, input);
        Assert.NotEqual(Guid.Empty, executed.Id);
        Assert.Equal(calculationId, executed.CalculationId);

        var readBack = await engine.FindRecordAsync<TResult>(executed.Id);
        Assert.NotNull(readBack);

        return (executed, readBack);
    }

    /// <summary>Asserts the record carries an intermediate of that name.</summary>
    public static void AssertHasIntermediate<TResult>(CalculationRecord<TResult> record, string name) =>
        Assert.Contains(record.IntermediateResults, i => string.Equals(i.Name, name, StringComparison.Ordinal));
}
