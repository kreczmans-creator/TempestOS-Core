namespace Tempest.Core.Calculations.Modules;

/// <summary>
/// What one of the `WP 21.7A` calculation modules found: its criteria met,
/// not met, or the method not applicable to the input at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>Meeting the criteria is not approval.</b> As with
/// <see cref="BracketCheckOutcome"/>, there is no <c>Approved</c> member:
/// approving a design is a governed act by a person, and this enumeration
/// reports only what the arithmetic found.
/// </para>
/// <para>
/// <b><see cref="OutsideMethodLimits"/> is a refusal, not a failure.</b> A
/// hand-calculation method has limits of applicability — a slenderness
/// beyond which a strut curve is not defined, a weld too short to be
/// designed to carry load, a bolt-group with nothing to resist its moment.
/// Inside those limits a result is computed and compared; outside them the
/// module declines to compute, says why in the result's own refusal
/// reason, and leaves every figure it did not compute as <see langword="null"/>.
/// That is deliberately not an exception: the input is legitimate, the
/// method is simply the wrong one for it, and an engineer needs to read
/// that as a finding rather than a fault. A zero or negative input, by
/// contrast, is malformed and is rejected with
/// <see cref="CalculationInputInvalidException"/>, as every existing
/// definition does.
/// </para>
/// </remarks>
public enum EngineeringCheckOutcome
{
    /// <summary>Every stated criterion is met. An engineering finding, not an approval.</summary>
    MeetsCriteria,

    /// <summary>At least one stated criterion is not met.</summary>
    DoesNotMeetCriteria,

    /// <summary>The input lies outside the method's limits of applicability; nothing was computed. The result's refusal reason says why.</summary>
    OutsideMethodLimits,
}
