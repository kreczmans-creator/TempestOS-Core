namespace Tempest.Core.Requirements;

/// <summary>
/// The permitted <see cref="RequirementStatus"/> transition table,
/// exactly as <c>WP7.2C Requirement Lifecycle Model.md</c> defines it —
/// the sole enforcement point <see cref="IRequirementsService.SetStatusAsync"/>
/// checks against. This is a contractual state model, not workflow
/// automation: nothing here decides *when* a transition should happen,
/// only whether a requested one is permitted.
/// </summary>
internal static class RequirementStatusTransitions
{
    private static readonly IReadOnlyDictionary<RequirementStatus, IReadOnlySet<RequirementStatus>> Permitted =
        new Dictionary<RequirementStatus, IReadOnlySet<RequirementStatus>>
        {
            [RequirementStatus.Draft] = new HashSet<RequirementStatus> { RequirementStatus.Reviewed, RequirementStatus.Obsolete },
            [RequirementStatus.Reviewed] = new HashSet<RequirementStatus> { RequirementStatus.Draft, RequirementStatus.Approved, RequirementStatus.Obsolete },
            [RequirementStatus.Approved] = new HashSet<RequirementStatus> { RequirementStatus.Draft, RequirementStatus.Allocated, RequirementStatus.Obsolete },
            [RequirementStatus.Allocated] = new HashSet<RequirementStatus> { RequirementStatus.Approved, RequirementStatus.Verified, RequirementStatus.Obsolete },
            [RequirementStatus.Verified] = new HashSet<RequirementStatus> { RequirementStatus.Allocated, RequirementStatus.Satisfied, RequirementStatus.Obsolete },
            [RequirementStatus.Satisfied] = new HashSet<RequirementStatus> { RequirementStatus.Verified, RequirementStatus.Obsolete },
            [RequirementStatus.Obsolete] = new HashSet<RequirementStatus>(),
        };

    /// <summary>
    /// Whether transitioning from <paramref name="from"/> to <paramref name="to"/>
    /// is permitted. A same-to-same request is deliberately **not** special-cased —
    /// it is permitted only where the table itself lists it (nowhere, today),
    /// keeping this method a literal match to <c>WP7.2C Requirement Lifecycle
    /// Model.md</c>'s own table, with no undocumented exception.
    /// </summary>
    public static bool IsPermitted(RequirementStatus from, RequirementStatus to) =>
        Permitted[from].Contains(to);
}
