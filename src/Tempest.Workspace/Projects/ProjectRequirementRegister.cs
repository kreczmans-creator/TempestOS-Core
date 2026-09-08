using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;

namespace Tempest.App.Projects;

/// <summary>Where a requirement stands against verification, as a single value.</summary>
/// <remarks>
/// Deliberately distinct from <see cref="RequirementStatus"/>. The status
/// is what someone declared about the requirement; this is what the
/// verification history actually records. A requirement marked
/// <see cref="RequirementStatus.Verified"/> with no verification record
/// behind it is a real and important thing to be able to see, and one
/// field cannot say both.
/// </remarks>
public enum RequirementVerificationState
{
    /// <summary>No verification has been recorded against this requirement.</summary>
    NotVerified,

    /// <summary>The most recent verification passed.</summary>
    Passed,

    /// <summary>The most recent verification failed.</summary>
    Failed,

    /// <summary>The most recent verification passed subject to conditions.</summary>
    Conditional,

    /// <summary>
    /// This principal is not permitted to read verification history, so
    /// what the requirement's verification says is genuinely unknown here.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="NotVerified"/> on purpose. "Nothing was
    /// recorded" and "you are not allowed to see what was recorded" are
    /// different facts, and collapsing the second into the first would
    /// have the surface state something false about the engineering data
    /// on the strength of a permission check.
    /// </remarks>
    Unknown,
}

/// <summary>
/// One requirement belonging to a project, with what is known about its
/// verification.
/// </summary>
/// <param name="RequirementId">The requirement itself.</param>
/// <param name="Identifier">Its business identifier.</param>
/// <param name="Statement">What it requires.</param>
/// <param name="Status">Its declared lifecycle status.</param>
/// <param name="Verification">What its verification history actually says.</param>
/// <param name="VerificationCount">How many verification records exist against it.</param>
/// <param name="LinkedObjectIds">The project's engineering objects this requirement is linked to.</param>
public sealed record ProjectRequirementEntry(
    Guid RequirementId,
    string Identifier,
    string Statement,
    RequirementStatus Status,
    RequirementVerificationState Verification,
    int VerificationCount,
    IReadOnlyList<Guid> LinkedObjectIds)
{
    /// <summary>
    /// Whether the declared status claims verification the verification
    /// history does not support.
    /// </summary>
    /// <remarks>
    /// Surfaced rather than silently reconciled: this is exactly the
    /// discrepancy a reviewer needs to see, and neither field is wrong to
    /// report — one is a claim, the other is a record.
    /// </remarks>
    public bool ClaimsUnrecordedVerification =>
        Status is RequirementStatus.Verified or RequirementStatus.Satisfied &&
        Verification is RequirementVerificationState.NotVerified;
}

/// <summary>The requirements belonging to a project.</summary>
public interface IProjectRequirementRegister
{
    /// <summary>Every requirement linked into <paramref name="projectId"/>, with its verification state.</summary>
    Task<IReadOnlyList<ProjectRequirementEntry>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The project's own requirements register — the read model behind the
/// Project Workspace's Requirements area.
/// </summary>
/// <remarks>
/// <para>
/// <b>A requirement is not an engineering object</b>, so
/// <see cref="ProjectMembership"/> — which walks the
/// <see cref="IHasParent"/> chain over the object graph — cannot reach one.
/// Requirements live in <see cref="IRequirementsService"/> over the
/// document store, with their own identity, revisions and status.
/// </para>
/// <para>
/// <b>The join is the link the platform already records.</b> A requirement
/// belongs to a project when something it is linked to
/// (<see cref="RequirementRelationshipKinds.AllocatedTo"/> above all, but
/// any recorded reference counts) is an engineering object that
/// <see cref="ProjectMembership"/> resolves into that project. Allocation
/// is how requirements are attached to the things that satisfy them, so
/// this reads the existing edge rather than inventing a
/// <c>ProjectId</c> field on the requirements model — which this Work
/// Package was explicitly not to do, and which would have been a second,
/// competing answer to a question the platform can already answer.
/// </para>
/// <para>
/// The consequence is honest and worth stating plainly: <b>an unallocated
/// requirement belongs to no project</b>. That is not a defect of this
/// register, it is the true state of the data — a requirement nobody has
/// linked to anything is not yet part of any project's work, and the
/// surface says so rather than guessing.
/// </para>
/// <para>
/// Verification state comes from
/// <see cref="IRequirementsService.GetEvidenceAsync"/>, which composes the
/// real <see cref="IVerificationRecord"/> history — so the register
/// reports what was actually recorded, and reports it separately from what
/// the requirement's own status claims.
/// </para>
/// </remarks>
public sealed class ProjectRequirementRegister : IProjectRequirementRegister
{
    private readonly IRequirementsService _requirements;
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="ProjectRequirementRegister"/> class.</summary>
    public ProjectRequirementRegister(IRequirementsService requirements, EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(context);

        _requirements = requirements;
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectRequirementEntry>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var members = await ProjectMembership
            .ListProjectMembersAsync(_context.Repository, projectId, cancellationToken)
            .ConfigureAwait(false);

        var inProject = members.Select(m => m.Id).ToHashSet();
        if (inProject.Count == 0)
            return [];

        var all = await _requirements.ListAsync(cancellationToken).ConfigureAwait(false);
        var entries = new List<ProjectRequirementEntry>();

        foreach (var requirement in all)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (requirement.IsDeleted)
                continue;

            // Membership first, and through the ungated relationship read:
            // the project join must not depend on being allowed to read
            // verification, and composing evidence for every requirement in
            // the platform to discover that most are not in this project is
            // work for nothing.
            var references = await _requirements.GetRelationshipsAsync(requirement.Id, cancellationToken).ConfigureAwait(false);

            var linked = references
                .Select(r => r.TargetDocumentId)
                .Where(inProject.Contains)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (linked.Count == 0)
                continue;

            var (state, count) = await ReadVerificationAsync(requirement.Id, cancellationToken).ConfigureAwait(false);

            entries.Add(new ProjectRequirementEntry(
                requirement.Id,
                requirement.Identifier,
                requirement.Statement,
                requirement.Status,
                state,
                count,
                linked));
        }

        return
        [
            .. entries
                .OrderBy(e => e.Identifier, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.RequirementId)
        ];
    }

    /// <summary>
    /// What verification records against <paramref name="requirementId"/>,
    /// or <see cref="RequirementVerificationState.Unknown"/> when this
    /// principal may not look.
    /// </summary>
    /// <remarks>
    /// <see cref="IRequirementsService.GetEvidenceAsync"/> is
    /// permission-gated, transitively, through the verification history it
    /// composes. A user who may see a project's requirements but not its
    /// verification records gets the requirements — with the verification
    /// column honestly saying it cannot be read — rather than an exception
    /// that empties the whole surface.
    /// </remarks>
    private async Task<(RequirementVerificationState State, int Count)> ReadVerificationAsync(
        Guid requirementId, CancellationToken cancellationToken)
    {
        try
        {
            var evidence = await _requirements.GetEvidenceAsync(requirementId, cancellationToken).ConfigureAwait(false);
            return (StateOf(evidence.VerificationHistory), evidence.VerificationHistory.Count);
        }
        catch (PermissionDeniedException)
        {
            return (RequirementVerificationState.Unknown, 0);
        }
    }

    /// <summary>What a verification history says, reduced to its most recent outcome.</summary>
    /// <remarks>
    /// The latest record wins, because a requirement that failed and was
    /// then fixed and re-verified is verified — reporting the worst
    /// outcome ever recorded would make a passing requirement look failed
    /// forever. Ties are broken by revision, so two records written in the
    /// same instant still order deterministically.
    /// </remarks>
    private static RequirementVerificationState StateOf(IReadOnlyList<IVerificationRecord> history)
    {
        var latest = history
            .OrderByDescending(r => r.VerifiedAt)
            .ThenByDescending(r => r.RevisionNumber)
            .FirstOrDefault();

        return latest is null
            ? RequirementVerificationState.NotVerified
            : latest.Outcome switch
            {
                VerificationOutcome.Pass => RequirementVerificationState.Passed,
                VerificationOutcome.Fail => RequirementVerificationState.Failed,
                _ => RequirementVerificationState.Conditional,
            };
    }
}
