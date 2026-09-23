using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Workspace.Projects;

namespace Tempest.Workspace.Evidence;

/// <summary>
/// One piece of Evidence that cites a reference record, as a record view's
/// own <em>Cited by</em> section needs it — which project it belongs to,
/// its own lifecycle state, and the revision it actually pinned.
/// </summary>
/// <param name="EvidenceId">The citing Evidence object's own id — opened right up through the same callback every other "Open" action in the shell already uses.</param>
/// <param name="EvidenceDisplayName">The citing Evidence's own display name.</param>
/// <param name="Status">The citing Evidence's own current lifecycle state.</param>
/// <param name="ProjectId">The project the citing Evidence belongs to. <see cref="Guid.Empty"/> for evidence with no structural parent, which validation elsewhere already treats as a gap rather than this index inventing one.</param>
/// <param name="ProjectLabel">The project's own label (identifier and name), or a placeholder where the project could not be resolved.</param>
/// <param name="CitedRevisionNumber">The revision of the reference record this Evidence actually pinned at citation time (`ADR-0148`) — not necessarily the record's own current revision.</param>
public sealed record ReferenceCitationRecord(
    Guid EvidenceId,
    string EvidenceDisplayName,
    EvidenceStatus Status,
    Guid ProjectId,
    string ProjectLabel,
    int CitedRevisionNumber);

/// <summary>Finds every piece of Evidence, across every project, that cites a given reference record.</summary>
/// <remarks>
/// <see cref="IEvidenceRecord.Citations"/> is forward-only — Evidence
/// knows what it cites, but no reference record knows who cites it
/// (`brief-19.6A.md`'s own seam map, §2: "no <c>FindCitationsAsync</c>/
/// <c>CitedBy</c> method anywhere in the tree"). This index is the read
/// side of that gap, built the same way every other read-model register
/// in this namespace is (<see cref="Projects.ProjectDependencyRegister"/>):
/// over the existing domain, holding no state of its own, creating no
/// second index. A full <c>Evidence</c>-kind repository scan is an
/// accepted cost here rather than a persisted reverse index — one
/// coherent snapshot read (<see cref="IEngineeringObjectRepository.ListByKindAsync"/>),
/// exactly as <see cref="Views.EvidenceWorkspaceView"/>'s own Evidence tab
/// already reads its list, and Evidence volumes in this platform do not
/// call for anything heavier.
/// </remarks>
public interface IReferenceCitationIndex
{
    /// <summary>
    /// Every piece of Evidence, across every project, whose own citations
    /// pin <paramref name="recordId"/> in <paramref name="library"/> at
    /// any revision — deleted Evidence excluded, ordered by project label
    /// then Evidence display name. Never <see langword="null"/>.
    /// </summary>
    Task<IReadOnlyList<ReferenceCitationRecord>> FindCitationsAsync(
        string library, string recordId, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IReferenceCitationIndex"/> implementation.</summary>
public sealed class ReferenceCitationIndex : IReferenceCitationIndex
{
    private readonly EngineeringDomainContext _context;
    private readonly IProjectDirectory _projectDirectory;

    /// <summary>Initialises a new instance of the <see cref="ReferenceCitationIndex"/> class.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="projectDirectory"/> is <see langword="null"/>.</exception>
    public ReferenceCitationIndex(EngineeringDomainContext context, IProjectDirectory projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(projectDirectory);

        _context = context;
        _projectDirectory = projectDirectory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReferenceCitationRecord>> FindCitationsAsync(
        string library, string recordId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(library);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        // One coherent snapshot read, never composed from several reads
        // that could straddle a commit — the same discipline
        // `EvidenceWorkspaceView.RefreshAsync` already follows for its own
        // Evidence list.
        // `TD-88`/`WP 21.5B`: `Citations` is an `Evidence`-own field, not
        // on the index row; liveness is filtered from the index first.
        var everyEvidenceEntries = await _context.Repository.ListByKindAsync(Core.Evidence.Evidence.CanonicalKind, cancellationToken).ConfigureAwait(false);
        var liveEvidenceEntries = everyEvidenceEntries.Where(entry => !entry.IsDeleted).ToList();
        var everyEvidence = await _context.Repository.MaterialiseAsync<Core.Evidence.Evidence>(liveEvidenceEntries, cancellationToken).ConfigureAwait(false);

        var citing = everyEvidence
            .SelectMany(e => e.Citations
                .Where(c => string.Equals(c.Pin.Library, library, StringComparison.Ordinal)
                    && string.Equals(c.Pin.RecordId, recordId, StringComparison.Ordinal))
                .Select(c => (Evidence: e, Citation: c)))
            .ToList();

        var results = new List<ReferenceCitationRecord>(citing.Count);
        var projectLabels = new Dictionary<Guid, string>();

        foreach (var (evidence, citation) in citing)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projectId = evidence.ParentId ?? Guid.Empty;
            if (!projectLabels.TryGetValue(projectId, out var label))
            {
                var project = projectId == Guid.Empty
                    ? null
                    : await _projectDirectory.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
                label = project?.Label ?? "(no project)";
                projectLabels[projectId] = label;
            }

            results.Add(new ReferenceCitationRecord(
                evidence.Id, evidence.DisplayName, evidence.Status, projectId, label, citation.Pin.RevisionNumber));
        }

        return results
            .OrderBy(r => r.ProjectLabel, StringComparer.Ordinal)
            .ThenBy(r => r.EvidenceDisplayName, StringComparer.Ordinal)
            .ToList();
    }
}
