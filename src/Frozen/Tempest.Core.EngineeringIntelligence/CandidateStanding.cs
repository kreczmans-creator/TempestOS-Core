namespace Tempest.Core.EngineeringIntelligence;

/// <summary>Where a candidate stands after assessment — a material against an application's requirements, a process against a part's.</summary>
/// <remarks>
/// Four states, and none of them is "recommended". `P02` reports what the
/// criteria concluded; an engineer decides what to use.
/// </remarks>
public enum CandidateStanding
{
    /// <summary>Nothing was checked, so nothing is known.</summary>
    NotAssessed,

    /// <summary>
    /// Every constraint that could be evaluated was satisfied, and none
    /// failed. <b>Not a recommendation:</b> a statement about the criteria
    /// that were checked, and silent about everything that was not.
    /// </summary>
    ConstraintsSatisfied,

    /// <summary>
    /// At least one constraint could not be concluded — missing data,
    /// missing evidence, or an undecidable comparison. The candidate is
    /// neither eliminated nor cleared.
    /// </summary>
    Unresolved,

    /// <summary>At least one constraint was evaluated and failed, or a binding rule found a defect.</summary>
    Eliminated
}
