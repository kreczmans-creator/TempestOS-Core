namespace Tempest.Core.Calculations.Modules;

/// <summary>
/// The three things every `WP 21.7A` module does the same way: reject a
/// malformed input, refuse an input outside the method's limits, and
/// compare a utilisation against one with the framework's slack.
/// </summary>
/// <remarks>
/// Internal and static: these are conventions shared by the modules in
/// this folder, not a framework contract. The slack is
/// <see cref="BracketSectionCheckCalculationDefinition.AcceptanceRelativeTolerance"/>,
/// reused rather than restated, for the reason set out on that constant.
/// </remarks>
internal static class ModuleGuards
{
    /// <summary>The relative slack every module's acceptance comparison allows — one part in a billion.</summary>
    public const double AcceptanceRelativeTolerance = BracketSectionCheckCalculationDefinition.AcceptanceRelativeTolerance;

    /// <summary>
    /// Records <paramref name="constraint"/> as a constraint check and, when
    /// it does not hold, rejects the input with
    /// <see cref="CalculationInputInvalidException"/>.
    /// </summary>
    /// <exception cref="CalculationInputInvalidException"><paramref name="satisfied"/> is <see langword="false"/>.</exception>
    public static void Require(CalculationContext context, string constraint, bool satisfied, string actual)
    {
        context.RecordConstraintCheck(constraint, satisfied, $"Received {actual}.");

        if (!satisfied)
            throw new CalculationInputInvalidException($"{constraint} Received {actual}.");
    }

    /// <summary>
    /// Records a method limit as an unsatisfied constraint check and returns
    /// the refusal reason for the result to carry. Never throws: see
    /// <see cref="EngineeringCheckOutcome.OutsideMethodLimits"/>.
    /// </summary>
    public static string Refuse(CalculationContext context, string limit, string reason)
    {
        context.RecordConstraintCheck(limit, false, reason);
        return reason;
    }

    /// <summary>Whether a utilisation (demand over capacity) is at or below one, with the framework's slack.</summary>
    public static bool IsMet(double utilisation) => utilisation <= 1.0 + AcceptanceRelativeTolerance;

    /// <summary>The outcome for a set of criteria that were all met or not.</summary>
    public static EngineeringCheckOutcome OutcomeOf(bool everyCriterionMet) =>
        everyCriterionMet ? EngineeringCheckOutcome.MeetsCriteria : EngineeringCheckOutcome.DoesNotMeetCriteria;
}
