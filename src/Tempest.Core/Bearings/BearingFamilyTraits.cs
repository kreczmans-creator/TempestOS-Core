namespace Tempest.Core.Bearings;

/// <summary>
/// Which engineering properties are meaningful for a given
/// <see cref="BearingFamily"/> — this library's own type-aware modelling
/// rule, and the single place it is stated.
/// </summary>
/// <remarks>
/// <para>
/// Applicability is a property of the bearing family, not of any one
/// record: a deep-groove ball bearing has no nominal contact angle to
/// record, and a plain bush has no rolling elements or internal clearance
/// class. Reading applicability from here lets a missing value be reported
/// as <see cref="BearingPropertyAvailability.NotApplicable"/> — genuinely
/// distinct from <see cref="BearingPropertyAvailability.NotRecorded"/>,
/// which is a data gap.
/// </para>
/// <para>
/// This table states what a property <em>means</em> for a family. It never
/// states whether a bearing is suitable for an application — that is
/// engineering judgement, and belongs to a future selection capability,
/// not to this reference library (see `docs/architecture/A4 Bearing
/// Library.md`, "Selection boundary").
/// </para>
/// </remarks>
public static class BearingFamilyTraits
{
    /// <summary>
    /// Whether a nominal contact angle is a defining characteristic of
    /// <paramref name="family"/>. False for <see cref="BearingFamily.Other"/>
    /// and <see cref="BearingFamily.Unspecified"/> is deliberately *not*
    /// asserted — see <see cref="IsApplicabilityKnown"/>.
    /// </summary>
    public static bool HasContactAngle(BearingFamily family) => family switch
    {
        BearingFamily.AngularContactBall => true,
        BearingFamily.TaperedRoller => true,
        BearingFamily.SphericalRoller => true,
        BearingFamily.ThrustRoller => true,
        _ => false
    };

    /// <summary>Whether <paramref name="family"/> carries load on rolling elements (as opposed to a sliding surface).</summary>
    public static bool HasRollingElements(BearingFamily family) => family switch
    {
        BearingFamily.Plain => false,
        _ => true
    };

    /// <summary>Whether an internal clearance or preload class is a meaningful property of <paramref name="family"/>.</summary>
    public static bool HasInternalClearance(BearingFamily family) => HasRollingElements(family);

    /// <summary>Whether a row configuration (single-row, double-row, and so on) is a meaningful property of <paramref name="family"/>.</summary>
    public static bool HasRowConfiguration(BearingFamily family) => HasRollingElements(family);

    /// <summary>
    /// Whether <paramref name="family"/> is primarily an axial-load
    /// (thrust) bearing, for which an axial load rating is the principal
    /// rating and a radial one is not ordinarily quoted.
    /// </summary>
    public static bool IsThrustBearing(BearingFamily family) => family switch
    {
        BearingFamily.ThrustBall => true,
        BearingFamily.ThrustRoller => true,
        _ => false
    };

    /// <summary>Whether a cage (separator) is a meaningful property of <paramref name="family"/>.</summary>
    public static bool HasCage(BearingFamily family) => HasRollingElements(family);

    /// <summary>
    /// Whether this table can speak for <paramref name="family"/> at all.
    /// <see cref="BearingFamily.Unspecified"/> and
    /// <see cref="BearingFamily.Other"/> are unclassified by construction:
    /// every applicability answer above returns a conservative
    /// <see langword="false"/> for them, which must be read as "not known
    /// to apply", never as "known not to apply". Callers reporting
    /// applicability to a reader must consult this first.
    /// </summary>
    public static bool IsApplicabilityKnown(BearingFamily family) =>
        family is not (BearingFamily.Unspecified or BearingFamily.Other);
}
