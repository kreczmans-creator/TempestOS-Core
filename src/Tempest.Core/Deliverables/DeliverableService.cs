using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;

namespace Tempest.Core.Deliverables;

/// <summary>The concrete <see cref="IDeliverableService"/> implementation.</summary>
public sealed class DeliverableService : IDeliverableService
{
    /// <summary>The default milestone a directly-added deliverable is grouped under when no quotation has created one yet (`WP 19.5A`, Product Owner comment item 4).</summary>
    public const string UnquotedMilestoneTitle = "Unquoted";

    /// <summary>The Kind string for a Milestone — <c>Tempest.Workspace.CanonicalObjectKinds.Milestone</c>'s own value, repeated here because <c>Tempest.Core</c> cannot reference <c>Tempest.Workspace</c>, mirroring <c>Tempest.Core.Quotations.QuotationService</c>'s own identical disclosure.</summary>
    private const string MilestoneKind = "Milestone";

    /// <summary>The Kind string for a Deliverable — <c>Tempest.Workspace.CanonicalObjectKinds.Deliverable</c>'s own value, repeated for the identical reason as <see cref="MilestoneKind"/>.</summary>
    private const string DeliverableKind = "Deliverable";

    private readonly EngineeringDomainContext _context;
    private readonly TimeProvider _time;
    private Func<Guid, CancellationToken, Task>? _completionHook;

    /// <summary>Initialises a new instance of the <see cref="DeliverableService"/> class.</summary>
    /// <param name="timeProvider">The clock the archived-project guard reads "now" from (`WP 19.5C`). <see langword="null"/> — the default — is <see cref="TimeProvider.System"/>.</param>
    public DeliverableService(EngineeringDomainContext context, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Registers a hook run, best-effort, after a deliverable is
    /// completed — this is how completing a deliverable raises an invoice
    /// request (`WP 19.1A`, `Tempest.Core.Invoicing.InvoicingService.RaiseFromCompletionAsync`),
    /// through this service rather than by the UI, with no compile-time
    /// dependency from <c>Tempest.Core.Deliverables</c> onto
    /// <c>Tempest.Core.Invoicing</c> (which itself depends on
    /// <see cref="IDeliverableService"/>, so a direct reference the other
    /// way would be circular). The composition root
    /// (<c>Tempest.Workspace.Composition.EngineeringWorkspaceComposer.RegisterEngineeringDisciplines</c>)
    /// wires this once, after both services exist. <see langword="null"/>
    /// (the default, and every composition root this Work Package does not
    /// touch) means completing a deliverable raises nothing.
    /// </summary>
    /// <remarks>
    /// Any exception the hook itself throws, or any refusal
    /// <see cref="Tempest.Core.Invoicing.InvoicingService.RaiseFromCompletionAsync"/>
    /// reports (no client recorded, nothing to bill, and so on), is
    /// swallowed here rather than failing the completion that triggered
    /// it: a deliverable is completed the moment
    /// <see cref="CompleteAsync"/>'s own write commits, regardless of
    /// whether raising an invoice for it succeeds.
    /// </remarks>
    public void SetCompletionHook(Func<Guid, CancellationToken, Task>? hook) => _completionHook = hook;

    /// <inheritdoc />
    public async Task<DeliverableCompletionResult> CompleteAsync(
        Guid deliverableId, Guid projectId, DateOnly completedOn,
        IReadOnlyList<Guid>? issuedEvidenceIds = null, IReadOnlyList<Guid>? documentIds = null, Money? fixedPriceValue = null,
        CancellationToken cancellationToken = default)
    {
        if (await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project)
            throw new ArgumentException($"'{projectId}' does not identify a live project.", nameof(projectId));

        if (await _context.Repository.FindAsync(deliverableId, cancellationToken).ConfigureAwait(false) is not Deliverable)
            throw new ArgumentException($"'{deliverableId}' does not identify a live deliverable.", nameof(deliverableId));

        if (Archived(project) is { } archived)
            return archived;

        var existing = await FindExistingCompletionAsync(deliverableId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return new DeliverableCompletionResult(
                DeliverableCompletionRefusal.AlreadyCompleted,
                $"Deliverable '{deliverableId}' was already completed on {existing.CompletedOn:O} (completion '{existing.Id}'). A deliverable can be completed once.",
                existing);
        }

        var evidenceIds = issuedEvidenceIds ?? [];

        foreach (var evidenceId in evidenceIds)
        {
            if (await _context.Repository.FindAsync(evidenceId, cancellationToken).ConfigureAwait(false) is not Core.Evidence.Evidence evidence
                || evidence.Status != Core.Evidence.EvidenceStatus.Issued)
            {
                return new DeliverableCompletionResult(
                    DeliverableCompletionRefusal.EvidenceNotIssued,
                    $"Evidence '{evidenceId}' is not Issued; a deliverable completion may only cite issued evidence.",
                    null);
            }
        }

        var principalId = _context.ResolveCurrentPrincipalId();

        var created = await new EngineeringObjectFactory<DeliverableCompletion>(
            DeliverableCompletion.CanonicalKind,
            _context,
            (doc, rev) => new DeliverableCompletion(
                doc, rev, _context, identifier: null, $"Deliverable completed — {completedOn:yyyy-MM-dd}", EngineeringObjectMetadata.Empty,
                deliverableId, completedOn, principalId, evidenceIds, documentIds ?? [], fixedPriceValue))
            .CreateAsync($"Deliverable '{deliverableId}' completed.", cancellationToken)
            .ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, cancellationToken).ConfigureAwait(false);

        if (_completionHook is { } hook)
        {
            try
            {
                await hook(created.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Best-effort (this method's own remarks): completing a
                // deliverable succeeded the moment the write above
                // committed, and stays succeeded regardless of whether
                // raising an invoice for it did.
            }
        }

        return new DeliverableCompletionResult(DeliverableCompletionRefusal.None, null, (DeliverableCompletion)created);
    }

    /// <inheritdoc />
    public async Task<DeliverableCompletionResult> MarkInvoicedAsync(Guid completionId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var completion = await _context.Repository.FindAsync(completionId, cancellationToken).ConfigureAwait(false) as DeliverableCompletion;
        if (completion is null)
        {
            return new DeliverableCompletionResult(
                DeliverableCompletionRefusal.CompletionNotFound, $"No deliverable completion '{completionId}' is registered.", null);
        }

        if (completion.InvoicedBy is not null)
        {
            return new DeliverableCompletionResult(
                DeliverableCompletionRefusal.CompletionInvoiced,
                $"Deliverable completion '{completionId}' is already invoiced (request '{completion.InvoicedBy:N}').",
                completion);
        }

        if (await ArchivedAsync(completion, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await completion.MarkInvoicedAsync(requestId, cancellationToken).ConfigureAwait(false);

        return new DeliverableCompletionResult(DeliverableCompletionRefusal.None, null, completion);
    }

    /// <inheritdoc />
    public async Task<Deliverable> AddDeliverableAsync(
        Guid projectId, string title, DateOnly? targetDate = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project)
            throw new ArgumentException($"'{projectId}' does not identify a live project.", nameof(projectId));

        if (ProjectArchival.IsArchived(project, _time.GetUtcNow()))
        {
            throw new InvalidOperationException(
                $"Project '{projectId}' is archived (closed {project.ClosedOn:O}); no new deliverable can be added to it.");
        }

        var milestoneId = await FindOrCreateUnquotedMilestoneAsync(projectId, targetDate, cancellationToken).ConfigureAwait(false);
        var trimmedTitle = title.Trim();

        var created = await new EngineeringObjectFactory<Deliverable>(
            DeliverableKind,
            _context,
            (doc, rev) => new Deliverable(doc, rev, _context, identifier: null, trimmedTitle, EngineeringObjectMetadata.Empty, milestoneId))
            .CreateAsync($"Deliverable '{trimmedTitle}' added directly to project '{projectId}', with no quotation.", cancellationToken)
            .ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(milestoneId, cancellationToken).ConfigureAwait(false);

        return (Deliverable)created;
    }

    private async Task<Guid> FindOrCreateUnquotedMilestoneAsync(Guid projectId, DateOnly? targetDate, CancellationToken cancellationToken)
    {
        // `TD-88`/`WP 21.5B`: Kind, liveness and display name are all on the
        // index row, so finding the existing milestone (if any) never needs
        // to materialise a single candidate.
        var children = await _context.Repository.ListChildrenAsync(projectId, cancellationToken).ConfigureAwait(false);
        var existing = children.FirstOrDefault(entry =>
            string.Equals(entry.Kind, MilestoneKind, StringComparison.Ordinal) &&
            !entry.IsDeleted &&
            string.Equals(entry.DisplayName, UnquotedMilestoneTitle, StringComparison.Ordinal));

        if (existing is not null)
            return existing.Id;

        var resolvedTargetDate = (targetDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(90))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var created = await new EngineeringObjectFactory<Milestone>(
            MilestoneKind,
            _context,
            (doc, rev) => new Milestone(doc, rev, _context, identifier: null, UnquotedMilestoneTitle, EngineeringObjectMetadata.Empty, resolvedTargetDate))
            .CreateAsync("Default milestone created for a deliverable added directly, with no quotation.", cancellationToken)
            .ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, cancellationToken).ConfigureAwait(false);

        return created.Id;
    }

    private async Task<DeliverableCompletion?> FindExistingCompletionAsync(Guid deliverableId, CancellationToken cancellationToken)
    {
        // `TD-88`/`WP 21.5B`: `DeliverableId` is a `DeliverableCompletion`-own
        // field, not on the index row, so every live completion of this
        // Kind still has to be materialised to check it.
        var entries = await _context.Repository.ListByKindAsync(DeliverableCompletion.CanonicalKind, cancellationToken).ConfigureAwait(false);
        var liveEntries = entries.Where(entry => !entry.IsDeleted).ToList();
        var all = await _context.Repository.MaterialiseAsync<DeliverableCompletion>(liveEntries, cancellationToken).ConfigureAwait(false);

        return all.FirstOrDefault(c => c.DeliverableId == deliverableId);
    }

    /// <summary>The archived-project guard (`WP 19.5C`): every mutating command on an archived project's objects is refused, here, before its own mutator ever runs.</summary>
    private DeliverableCompletionResult? Archived(Project project) =>
        ProjectArchival.IsArchived(project, _time.GetUtcNow())
            ? new DeliverableCompletionResult(DeliverableCompletionRefusal.ProjectArchived, $"Project '{project.Id}' is archived (closed {project.ClosedOn:O}); it is read-only.", null)
            : null;

    /// <summary>The archived-project guard, resolved from a completion's own parent project.</summary>
    private async Task<DeliverableCompletionResult?> ArchivedAsync(DeliverableCompletion completion, CancellationToken cancellationToken)
    {
        if (completion.ParentId is not { } projectId
            || await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project)
        {
            return null;
        }

        return ProjectArchival.IsArchived(project, _time.GetUtcNow())
            ? new DeliverableCompletionResult(DeliverableCompletionRefusal.ProjectArchived, $"Project '{projectId}' is archived (closed {project.ClosedOn:O}); this completion is read-only.", completion)
            : null;
    }

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
