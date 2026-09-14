using System.Globalization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Requirements;

namespace Tempest.Core.Quotations;

/// <summary>The concrete <see cref="IQuotationService"/> implementation (`WP 19.5A`, `ADR-0152`).</summary>
/// <remarks>
/// <para>
/// <b><see cref="AcceptAsync"/> is not one atomic transaction.</b> It
/// creates a Milestone (if none named after the quotation's own reference
/// exists yet), then one Deliverable and one Requirement per line, each
/// its own transaction and audit row — <see cref="EngineeringDomainContext.ExecuteWriteAsync"/>
/// is internal to <c>Tempest.Core.EngineeringDomain</c> and commits one
/// object's own state at a time (`ADR-0145`); widening it to a
/// multi-object primitive is a substrate change outside this Work
/// Package's own files. Only once every line's own objects exist does
/// <see cref="Quotation.MarkAcceptedAsync"/> run, recording every created
/// id and moving <see cref="Quotation.Status"/> to
/// <see cref="QuotationStatus.Accepted"/> together, in one final
/// transaction — disclosed here exactly as `ADR-0151` §5 discloses the
/// identical shape for <c>InvoicingService.SendAsync</c>. A crash between
/// two of these transactions leaves live Deliverables/Requirements whose
/// quotation still reads Sent; retrying Accept in that state creates a
/// second set for lines already fulfilled and, since Requirements are
/// identified by <c>"&lt;Reference&gt;-&lt;n&gt;"</c>, throws
/// <see cref="DuplicateRequirementIdentifierException"/> for the
/// requirement half — a real, disclosed gap, not a hidden one, and the
/// PO's own kill switch for this Work Package does not ask it closed.
/// </para>
/// <para>
/// <b>A Requirement is associated to the project by allocation, not by
/// parenting.</b> <c>Requirement</c> is a plain DTO over
/// <c>IEngineeringDocumentStore</c>, not an <c>EngineeringObjectBase</c>,
/// so it has no <c>IHasParent</c>/<c>MoveAsync</c> to call. Instead, every
/// created Requirement is linked to the quotation itself
/// (<see cref="RequirementRelationshipKinds.AllocatedTo"/>) — the
/// quotation is already parented to the project
/// (<see cref="CreateAsync"/>), so <c>ProjectMembership.ListProjectMembersAsync</c>
/// already resolves it as a project member, and
/// <c>ProjectRequirementRegister.ListAsync</c> already treats a
/// requirement allocated to any project member as belonging to that
/// project. No change to <c>IRequirementsService</c> was needed.
/// </para>
/// <para>
/// <b>Milestone/Deliverable are constructed directly, never through
/// <c>Tempest.Workspace.Projects.IProjectMilestoneService</c>.</b> That
/// service lives in <c>Tempest.Workspace</c>, which <c>Tempest.Core</c>
/// cannot reference (the dependency runs the other way); it is also,
/// today, wired only on the Desktop-only <c>WorkspaceHost.ProjectMilestoneWorkflow</c>
/// property, unreachable from a Core-only host such as the one this
/// service's own journey tests run against. <c>Milestone</c>/
/// <c>Deliverable</c> are themselves plain <c>Tempest.Core.EngineeringDomain</c>
/// types, so this class builds them the same way
/// <c>ProjectMilestoneService.CreateMilestoneAsync</c>/<c>CreateDeliverableAsync</c>
/// do — through <see cref="EngineeringObjectFactory{T}"/> directly — rather
/// than duplicating a cross-project service reference that cannot compile.
/// The Kind strings ("Milestone"/"Deliverable") are repeated as literals
/// here for the same reason <c>Tempest.Core.Deliverables.DeliverableService.AddDeliverableAsync</c>
/// repeats them: <c>Tempest.Workspace.CanonicalObjectKinds</c>, the
/// constants' own canonical owner, lives in the Workspace project too.
/// </para>
/// </remarks>
public sealed class QuotationService : IQuotationService
{
    /// <summary>The Kind string for a Milestone — <c>Tempest.Workspace.CanonicalObjectKinds.Milestone</c>'s own value, repeated here because <c>Tempest.Core</c> cannot reference <c>Tempest.Workspace</c> (see this class's own remarks).</summary>
    private const string MilestoneKind = "Milestone";

    /// <summary>The Kind string for a Deliverable — <c>Tempest.Workspace.CanonicalObjectKinds.Deliverable</c>'s own value, repeated for the identical reason as <see cref="MilestoneKind"/>.</summary>
    private const string DeliverableKind = "Deliverable";

    private readonly EngineeringDomainContext _context;
    private readonly IRateCardCatalog _rateCards;
    private readonly IRequirementsService _requirements;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="QuotationService"/> class.</summary>
    public QuotationService(
        EngineeringDomainContext context, IRateCardCatalog rateCards, IRequirementsService requirements, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rateCards);
        ArgumentNullException.ThrowIfNull(requirements);

        _context = context;
        _rateCards = rateCards;
        _requirements = requirements;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<QuotationResult> CreateAsync(
        Guid projectId, string? reference = null, string? clientOrganisationId = null, CancellationToken cancellationToken = default)
    {
        if (await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project || !IsLive(project))
            return new QuotationResult(QuotationRefusal.ProjectNotFound, $"No project '{projectId}' is registered.", null);

        var quoteDate = Today();

        var resolvedReference = string.IsNullOrWhiteSpace(reference)
            ? await NextReferenceAsync(quoteDate.Year, cancellationToken).ConfigureAwait(false)
            : reference.Trim();

        var resolvedClient = clientOrganisationId ?? project.ClientOrganisationId;
        var currency = await ResolveCurrencyAsync(project, cancellationToken).ConfigureAwait(false);

        var created = await new EngineeringObjectFactory<Quotation>(
            Quotation.CanonicalKind,
            _context,
            (doc, rev) => new Quotation(
                doc, rev, _context, identifier: null, $"Quotation — {resolvedReference}",
                EngineeringObjectMetadata.Empty, resolvedReference, quoteDate, resolvedClient, currency,
                validityDays: 30, terms: null, lines: []))
            .CreateAsync($"Quotation '{resolvedReference}' opened with project '{projectId}'.", cancellationToken)
            .ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, cancellationToken).ConfigureAwait(false);

        return new QuotationResult(QuotationRefusal.None, null, (Quotation)created);
    }

    /// <inheritdoc />
    public async Task<QuotationResult> AddLineAsync(
        Guid quotationId, string description, decimal? hours, Money? rate, Money? fixedPrice, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var quote = await FindQuotationAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (quote is null)
            return NotFound(quotationId);

        if (quote.Status != QuotationStatus.Draft)
            return NotDraft(quote, quotationId, "added");

        var (line, refusal, reason) = BuildLine(quote, Guid.NewGuid(), description, hours, rate, fixedPrice);
        if (line is null)
            return new QuotationResult(refusal, reason, quote);

        await quote.AddLineAsync(line, cancellationToken).ConfigureAwait(false);

        return new QuotationResult(QuotationRefusal.None, null, quote);
    }

    /// <inheritdoc />
    public async Task<QuotationResult> UpdateLineAsync(
        Guid quotationId, Guid lineId, string description, decimal? hours, Money? rate, Money? fixedPrice,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var quote = await FindQuotationAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (quote is null)
            return NotFound(quotationId);

        if (quote.Status != QuotationStatus.Draft)
            return NotDraft(quote, quotationId, "changed");

        if (quote.Lines.All(l => l.Id != lineId))
            return new QuotationResult(QuotationRefusal.LineNotFound, $"No line '{lineId}' on quotation '{quotationId}'.", quote);

        var (line, refusal, reason) = BuildLine(quote, lineId, description, hours, rate, fixedPrice);
        if (line is null)
            return new QuotationResult(refusal, reason, quote);

        await quote.UpdateLineAsync(line, cancellationToken).ConfigureAwait(false);

        return new QuotationResult(QuotationRefusal.None, null, quote);
    }

    /// <inheritdoc />
    public async Task<QuotationResult> RemoveLineAsync(Guid quotationId, Guid lineId, CancellationToken cancellationToken = default)
    {
        var quote = await FindQuotationAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (quote is null)
            return NotFound(quotationId);

        if (quote.Status != QuotationStatus.Draft)
            return NotDraft(quote, quotationId, "removed");

        var line = quote.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return new QuotationResult(QuotationRefusal.LineNotFound, $"No line '{lineId}' on quotation '{quotationId}'.", quote);

        await quote.RemoveLineAsync(lineId, line.Description, cancellationToken).ConfigureAwait(false);

        return new QuotationResult(QuotationRefusal.None, null, quote);
    }

    /// <inheritdoc />
    public async Task<QuotationResult> SendAsync(Guid quotationId, CancellationToken cancellationToken = default)
    {
        var quote = await FindQuotationAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (quote is null)
            return NotFound(quotationId);

        if (!QuotationStatusTransitions.IsPermitted(quote.Status, QuotationStatus.Sent))
        {
            return new QuotationResult(
                QuotationRefusal.TransitionNotPermitted, $"Quotation '{quotationId}' is {quote.Status}; it must be Draft to send.", quote);
        }

        if (quote.Lines.Count == 0)
            return new QuotationResult(QuotationRefusal.NothingToSend, $"Quotation '{quotationId}' has no lines; there is nothing to send.", quote);

        await quote.MarkSentAsync(Today(), cancellationToken).ConfigureAwait(false);

        return new QuotationResult(QuotationRefusal.None, null, quote);
    }

    /// <inheritdoc />
    public async Task<QuotationResult> AcceptAsync(Guid quotationId, CancellationToken cancellationToken = default)
    {
        var quote = await FindQuotationAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (quote is null)
            return NotFound(quotationId);

        if (!QuotationStatusTransitions.IsPermitted(quote.Status, QuotationStatus.Accepted))
        {
            return new QuotationResult(
                QuotationRefusal.TransitionNotPermitted,
                $"Quotation '{quotationId}' is {quote.Status}; only a Sent quotation can be accepted.",
                quote);
        }

        if (quote.ParentId is not { } projectId)
        {
            throw new InvalidOperationException(
                $"Quotation '{quotationId}' has no live project — every quotation is parented to the project it was created under, "
                + "so this should be unreachable.");
        }

        var milestoneId = await FindOrCreateReferenceMilestoneAsync(quote, projectId, cancellationToken).ConfigureAwait(false);

        var fulfilledLines = new List<QuotationLine>(quote.Lines.Count);
        var sequence = 1;

        foreach (var line in quote.Lines)
        {
            var deliverable = await CreateDeliverableAsync(milestoneId, line.Description, cancellationToken).ConfigureAwait(false);

            var requirement = await _requirements
                .CreateAsync($"{quote.Reference}-{sequence}", line.Description, category: "Quotation", cancellationToken)
                .ConfigureAwait(false);
            await _requirements
                .LinkAsync(requirement.Id, quote.Id, RequirementRelationshipKinds.AllocatedTo, cancellationToken)
                .ConfigureAwait(false);

            fulfilledLines.Add(line with { DeliverableId = deliverable.Id, RequirementId = requirement.Id });
            sequence++;
        }

        await quote.MarkAcceptedAsync(Today(), fulfilledLines, cancellationToken).ConfigureAwait(false);

        return new QuotationResult(QuotationRefusal.None, null, quote);
    }

    /// <inheritdoc />
    public async Task<QuotationResult> DeclineAsync(Guid quotationId, CancellationToken cancellationToken = default)
    {
        var quote = await FindQuotationAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (quote is null)
            return NotFound(quotationId);

        if (!QuotationStatusTransitions.IsPermitted(quote.Status, QuotationStatus.Declined))
        {
            return new QuotationResult(
                QuotationRefusal.TransitionNotPermitted,
                $"Quotation '{quotationId}' is {quote.Status}; only a Sent quotation can be declined.",
                quote);
        }

        await quote.MarkDeclinedAsync(Today(), cancellationToken).ConfigureAwait(false);

        return new QuotationResult(QuotationRefusal.None, null, quote);
    }

    /// <summary>Builds a validated line, or a refusal naming what is wrong with it — shared by <see cref="AddLineAsync"/> and <see cref="UpdateLineAsync"/>.</summary>
    private static (QuotationLine? Line, QuotationRefusal Refusal, string? Reason) BuildLine(
        Quotation quote, Guid lineId, string description, decimal? hours, Money? rate, Money? fixedPrice)
    {
        var trimmedDescription = description.Trim();

        if (fixedPrice is { } price)
        {
            if (hours is not null || rate is not null)
            {
                return (null, QuotationRefusal.InvalidLine,
                    "A line is either hours and a rate, or a fixed price — never both.");
            }

            if (price.Currency != quote.Currency)
            {
                return (null, QuotationRefusal.LineCurrencyMismatch,
                    $"The fixed price is in {price.Currency}; this quotation is in {quote.Currency}.");
            }

            return (new QuotationLine(lineId, trimmedDescription, null, null, price, price, QuotationLineBasis.FixedPrice), QuotationRefusal.None, null);
        }

        if (hours is { } h && rate is { } r)
        {
            if (h <= 0)
                return (null, QuotationRefusal.InvalidLine, "Hours must be greater than zero.");

            if (r.Currency != quote.Currency)
            {
                return (null, QuotationRefusal.LineCurrencyMismatch,
                    $"The rate is in {r.Currency}; this quotation is in {quote.Currency}.");
            }

            return (new QuotationLine(lineId, trimmedDescription, h, r, null, r * h, QuotationLineBasis.Hourly), QuotationRefusal.None, null);
        }

        return (null, QuotationRefusal.InvalidLine, "A line needs either hours and a rate, or a fixed price.");
    }

    private async Task<Guid> FindOrCreateReferenceMilestoneAsync(Quotation quote, Guid projectId, CancellationToken cancellationToken)
    {
        var children = await _context.Repository.ListChildrenAsync(projectId, cancellationToken).ConfigureAwait(false);
        var existing = children
            .OfType<Milestone>()
            .FirstOrDefault(m => IsLive(m) && string.Equals(m.DisplayName, quote.Reference, StringComparison.Ordinal));

        if (existing is not null)
            return existing.Id;

        var targetDate = quote.QuoteDate.AddDays(quote.ValidityDays).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var created = await new EngineeringObjectFactory<Milestone>(
            MilestoneKind,
            _context,
            (doc, rev) => new Milestone(doc, rev, _context, identifier: null, quote.Reference, EngineeringObjectMetadata.Empty, targetDate))
            .CreateAsync($"Milestone created for accepted quotation '{quote.Reference}'.", cancellationToken)
            .ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, cancellationToken).ConfigureAwait(false);

        return created.Id;
    }

    private async Task<Deliverable> CreateDeliverableAsync(Guid milestoneId, string title, CancellationToken cancellationToken)
    {
        var created = await new EngineeringObjectFactory<Deliverable>(
            DeliverableKind,
            _context,
            (doc, rev) => new Deliverable(doc, rev, _context, identifier: null, title, EngineeringObjectMetadata.Empty, milestoneId))
            .CreateAsync($"Deliverable '{title}' created from an accepted quotation.", cancellationToken)
            .ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(milestoneId, cancellationToken).ConfigureAwait(false);

        return (Deliverable)created;
    }

    private async Task<CurrencyCode> ResolveCurrencyAsync(Project project, CancellationToken cancellationToken)
    {
        if (project.RateCardPin is not { } pin)
            return CurrencyCode.Gbp;

        var card = await _rateCards.GetRevisionAsync(pin.RecordId, pin.RevisionNumber, cancellationToken).ConfigureAwait(false);
        return card.Definition.Currency;
    }

    /// <summary>The next <c>Q-&lt;year&gt;-&lt;nnn&gt;</c> reference — one past the highest existing suffix already used for <paramref name="year"/>, among every quotation this store holds (live or not: a reference, once used, is never reissued).</summary>
    private async Task<string> NextReferenceAsync(int year, CancellationToken cancellationToken)
    {
        var existing = await _context.Repository.ListByKindAsync(Quotation.CanonicalKind, cancellationToken).ConfigureAwait(false);
        var prefix = $"Q-{year.ToString(CultureInfo.InvariantCulture)}-";

        var max = 0;
        foreach (var candidate in existing.OfType<Quotation>())
        {
            if (candidate.Reference.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(candidate.Reference.AsSpan(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                && n > max)
            {
                max = n;
            }
        }

        return $"{prefix}{(max + 1).ToString("000", CultureInfo.InvariantCulture)}";
    }

    private DateOnly Today() => DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

    private async Task<Quotation?> FindQuotationAsync(Guid quotationId, CancellationToken cancellationToken)
    {
        var candidate = await _context.Repository.FindAsync(quotationId, cancellationToken).ConfigureAwait(false);
        return candidate is Quotation { } quote && IsLive(quote) ? quote : null;
    }

    private static QuotationResult NotFound(Guid quotationId) =>
        new(QuotationRefusal.QuotationNotFound, $"No quotation '{quotationId}' is registered.", null);

    private static QuotationResult NotDraft(Quotation quote, Guid quotationId, string verb) =>
        new(QuotationRefusal.QuotationNotDraft, $"Quotation '{quotationId}' is {quote.Status}; a line can only be {verb} while Draft.", quote);

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
