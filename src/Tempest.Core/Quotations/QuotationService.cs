using System.Globalization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;
using Tempest.Core.Requirements;
using Tempest.Core.Settings;

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
/// <para>
/// <b>A change order (`WP 20.10E`, PO finding D18, `ADR-0152` addendum) is
/// the identical <see cref="Quotation"/> shape</b>, distinguished only by
/// <see cref="QuotationKind.ChangeOrder"/> and by carrying an existing
/// deliverable's own id on each line from the moment it is added
/// (<see cref="AddLineAsync"/>'s own <c>carriedDeliverableId</c>) rather
/// than waiting for <see cref="AcceptAsync"/> to mint one. This is what
/// lets <c>Tempest.Core.Projects.ProjectLifecycleService.SignOffAsync</c>
/// treat a carried deliverable as covered without inventing a second
/// mechanism: <see cref="AcceptAsync"/> already branches per line on
/// whether <see cref="QuotationLine.DeliverableId"/> is already set.
/// </para>
/// </remarks>
public sealed class QuotationService : IQuotationService
{
    /// <summary>The Kind string for a Milestone — <c>Tempest.Workspace.CanonicalObjectKinds.Milestone</c>'s own value, repeated here because <c>Tempest.Core</c> cannot reference <c>Tempest.Workspace</c> (see this class's own remarks).</summary>
    private const string MilestoneKind = "Milestone";

    /// <summary>The Kind string for a Deliverable — <c>Tempest.Workspace.CanonicalObjectKinds.Deliverable</c>'s own value, repeated for the identical reason as <see cref="MilestoneKind"/>.</summary>
    private const string DeliverableKind = "Deliverable";

    /// <summary>
    /// The Settings key the consultant's own default VAT rate for a new
    /// quotation line is stored under (`WP 21.3B`, Settings → Organisation
    /// identity) — read, and defaulted to
    /// <see cref="Core.BusinessGovernance.VatRate.OutOfScope"/>, whenever
    /// <see cref="AddLineAsync"/>/<see cref="UpdateLineAsync"/> are called
    /// with no explicit <c>vatRate</c>.
    /// </summary>
    public const string DefaultVatRateSettingKey = "Invoicing.DefaultVatRate";

    private readonly EngineeringDomainContext _context;
    private readonly IRateCardCatalog _rateCards;
    private readonly IRequirementsService _requirements;
    private readonly ISettingsProvider? _settings;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="QuotationService"/> class.</summary>
    /// <param name="settings">
    /// Where <see cref="DefaultVatRateSettingKey"/> is registered and read
    /// (`WP 21.3B`). <see langword="null"/> — honoured, not required —
    /// leaves every line defaulting to
    /// <see cref="Core.BusinessGovernance.VatRate.OutOfScope"/> outright,
    /// for a caller (an older test host) with no settings provider composed.
    /// </param>
    public QuotationService(
        EngineeringDomainContext context, IRateCardCatalog rateCards, IRequirementsService requirements, TimeProvider? timeProvider = null,
        ISettingsProvider? settings = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rateCards);
        ArgumentNullException.ThrowIfNull(requirements);

        _context = context;
        _rateCards = rateCards;
        _requirements = requirements;
        _time = timeProvider ?? TimeProvider.System;
        _settings = settings;

        if (_settings is not null)
        {
            try
            {
                _settings.RegisterDefinition(new SettingDefinition(
                    DefaultVatRateSettingKey, "Invoicing — default VAT rate for a new line", VatRate.OutOfScope.ToString()));
            }
            catch (DuplicateSettingDefinitionException)
            {
                // Registered already — by an earlier QuotationService this
                // process constructed (a test host restarting the same
                // in-memory settings provider, `SettingsView`'s own lazy
                // re-registration precedent).
            }
        }
    }

    /// <summary>The reference prefix an ordinary quotation is generated under.</summary>
    private const string QuotationReferencePrefix = "Q-";

    /// <summary>The reference prefix a change order is generated under (`WP 20.10E`).</summary>
    private const string ChangeOrderReferencePrefix = "CO-";

    /// <inheritdoc />
    public async Task<QuotationResult> CreateAsync(
        Guid projectId, string? reference = null, string? clientOrganisationId = null, QuotationKind kind = QuotationKind.Quotation,
        CancellationToken cancellationToken = default)
    {
        if (await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project || !IsLive(project))
            return new QuotationResult(QuotationRefusal.ProjectNotFound, $"No project '{projectId}' is registered.", null);

        if (ProjectArchival.IsArchived(project, _time.GetUtcNow()))
        {
            return new QuotationResult(
                QuotationRefusal.ProjectArchived, $"Project '{projectId}' is archived (closed {project.ClosedOn:O}); no new quotation can be opened with it.", null);
        }

        var quoteDate = Today();
        var prefix = kind == QuotationKind.ChangeOrder ? ChangeOrderReferencePrefix : QuotationReferencePrefix;

        var resolvedReference = string.IsNullOrWhiteSpace(reference)
            ? await NextReferenceAsync(quoteDate.Year, prefix, cancellationToken).ConfigureAwait(false)
            : reference.Trim();

        var resolvedClient = clientOrganisationId ?? project.ClientOrganisationId;
        var currency = await ResolveCurrencyAsync(project, cancellationToken).ConfigureAwait(false);
        var displayName = kind == QuotationKind.ChangeOrder ? $"Change order — {resolvedReference}" : $"Quotation — {resolvedReference}";

        var created = await new EngineeringObjectFactory<Quotation>(
            Quotation.CanonicalKind,
            _context,
            (doc, rev) => new Quotation(
                doc, rev, _context, identifier: null, displayName,
                EngineeringObjectMetadata.Empty, resolvedReference, quoteDate, resolvedClient, currency,
                validityDays: 30, terms: null, lines: [], kind: kind))
            .CreateAsync($"Quotation '{resolvedReference}' opened with project '{projectId}'.", cancellationToken)
            .ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, cancellationToken).ConfigureAwait(false);

        return new QuotationResult(QuotationRefusal.None, null, (Quotation)created);
    }

    /// <inheritdoc />
    public async Task<QuotationResult> AddLineAsync(
        Guid quotationId, string description, decimal? hours, Money? rate, Money? fixedPrice, Guid? carriedDeliverableId = null,
        VatRate? vatRate = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var quote = await FindQuotationAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (quote is null)
            return NotFound(quotationId);

        if (quote.Status != QuotationStatus.Draft)
            return NotDraft(quote, quotationId, "added");

        if (await ArchivedAsync(quote, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        if (carriedDeliverableId is { } carriedId)
        {
            if (await ValidateCarriedDeliverableAsync(quote, carriedId, cancellationToken).ConfigureAwait(false) is { } carriedRefusal)
                return carriedRefusal;
        }

        var resolvedVatRate = await ResolveVatRateAsync(vatRate, cancellationToken).ConfigureAwait(false);
        var (line, refusal, reason) = BuildLine(quote, Guid.NewGuid(), description, hours, rate, fixedPrice, resolvedVatRate);
        if (line is null)
            return new QuotationResult(refusal, reason, quote);

        if (carriedDeliverableId is { } carried)
            line = line with { DeliverableId = carried };

        await quote.AddLineAsync(line, cancellationToken).ConfigureAwait(false);

        return new QuotationResult(QuotationRefusal.None, null, quote);
    }

    /// <inheritdoc />
    public async Task<QuotationResult> UpdateLineAsync(
        Guid quotationId, Guid lineId, string description, decimal? hours, Money? rate, Money? fixedPrice,
        VatRate? vatRate = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var quote = await FindQuotationAsync(quotationId, cancellationToken).ConfigureAwait(false);
        if (quote is null)
            return NotFound(quotationId);

        if (quote.Status != QuotationStatus.Draft)
            return NotDraft(quote, quotationId, "changed");

        var existingLine = quote.Lines.FirstOrDefault(l => l.Id == lineId);
        if (existingLine is null)
            return new QuotationResult(QuotationRefusal.LineNotFound, $"No line '{lineId}' on quotation '{quotationId}'.", quote);

        if (await ArchivedAsync(quote, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        // An update with no explicit VAT rate keeps the line's own current
        // rate rather than resetting it to the consultant's own default —
        // only a genuinely new line (AddLineAsync) resolves the default.
        var resolvedVatRate = vatRate ?? existingLine.VatRate;
        var (line, refusal, reason) = BuildLine(quote, lineId, description, hours, rate, fixedPrice, resolvedVatRate);
        if (line is null)
            return new QuotationResult(refusal, reason, quote);

        // A carried deliverable id (`WP 20.10E`) is set once, at
        // AddLineAsync, and is not itself an editable field — carried
        // forward here so an ordinary edit (description/hours/rate) on a
        // change order line never loses what it carries.
        if (existingLine.DeliverableId is { } carried)
            line = line with { DeliverableId = carried };

        await quote.UpdateLineAsync(line, cancellationToken).ConfigureAwait(false);

        return new QuotationResult(QuotationRefusal.None, null, quote);
    }

    /// <summary>Resolves <paramref name="vatRate"/> — given, verbatim, or the consultant's own configured default (Settings → Organisation identity), or <see cref="Core.BusinessGovernance.VatRate.OutOfScope"/> when no settings provider is composed at all (`WP 21.3B`).</summary>
    private async Task<VatRate> ResolveVatRateAsync(VatRate? vatRate, CancellationToken cancellationToken)
    {
        if (vatRate is { } explicitRate)
            return explicitRate;

        if (_settings is null)
            return VatRate.OutOfScope;

        var stored = await _settings.GetValueAsync(DefaultVatRateSettingKey, cancellationToken).ConfigureAwait(false);
        return Enum.TryParse<VatRate>(stored, out var configured) ? configured : VatRate.OutOfScope;
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

        if (await ArchivedAsync(quote, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

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

        if (await ArchivedAsync(quote, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

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

        if (await ArchivedAsync(quote, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        // `WP 20.10E`: a change-order line already carries an existing
        // deliverable's own id (set at AddLineAsync) — no new Deliverable
        // for that line, and the reference milestone is found-or-created
        // lazily, only the first time a line actually needs one, so a
        // change order whose every line is carried creates no milestone at
        // all.
        Guid? milestoneId = null;

        var fulfilledLines = new List<QuotationLine>(quote.Lines.Count);
        var sequence = 1;

        foreach (var line in quote.Lines)
        {
            Guid deliverableId;
            if (line.DeliverableId is { } carriedDeliverableId)
            {
                deliverableId = carriedDeliverableId;
            }
            else
            {
                milestoneId ??= await FindOrCreateReferenceMilestoneAsync(quote, projectId, cancellationToken).ConfigureAwait(false);
                var deliverable = await CreateDeliverableAsync(milestoneId.Value, line.Description, cancellationToken).ConfigureAwait(false);
                deliverableId = deliverable.Id;
            }

            var requirement = await _requirements
                .CreateAsync($"{quote.Reference}-{sequence}", line.Description, category: "Quotation", cancellationToken)
                .ConfigureAwait(false);
            await _requirements
                .LinkAsync(requirement.Id, quote.Id, RequirementRelationshipKinds.AllocatedTo, cancellationToken)
                .ConfigureAwait(false);

            fulfilledLines.Add(line with { DeliverableId = deliverableId, RequirementId = requirement.Id });
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

        if (await ArchivedAsync(quote, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await quote.MarkDeclinedAsync(Today(), cancellationToken).ConfigureAwait(false);

        return new QuotationResult(QuotationRefusal.None, null, quote);
    }

    /// <summary>Builds a validated line, or a refusal naming what is wrong with it — shared by <see cref="AddLineAsync"/> and <see cref="UpdateLineAsync"/>.</summary>
    private static (QuotationLine? Line, QuotationRefusal Refusal, string? Reason) BuildLine(
        Quotation quote, Guid lineId, string description, decimal? hours, Money? rate, Money? fixedPrice, VatRate vatRate)
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

            return (new QuotationLine(lineId, trimmedDescription, null, null, price, price, QuotationLineBasis.FixedPrice, VatRate: vatRate), QuotationRefusal.None, null);
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

            return (new QuotationLine(lineId, trimmedDescription, h, r, null, r * h, QuotationLineBasis.Hourly, VatRate: vatRate), QuotationRefusal.None, null);
        }

        return (null, QuotationRefusal.InvalidLine, "A line needs either hours and a rate, or a fixed price.");
    }

    /// <summary>
    /// The guard on <see cref="AddLineAsync"/>'s own <c>carriedDeliverableId</c>
    /// (`WP 20.10E`): only a change order carries an existing deliverable,
    /// and only a live one.
    /// </summary>
    private async Task<QuotationResult?> ValidateCarriedDeliverableAsync(Quotation quote, Guid carriedDeliverableId, CancellationToken cancellationToken)
    {
        if (quote.QuotationKind != QuotationKind.ChangeOrder)
        {
            return new QuotationResult(
                QuotationRefusal.InvalidLine, "Only a change order can carry an existing deliverable on a line.", quote);
        }

        if (await _context.Repository.FindAsync(carriedDeliverableId, cancellationToken).ConfigureAwait(false) is not Deliverable deliverable
            || !IsLive(deliverable))
        {
            return new QuotationResult(
                QuotationRefusal.DeliverableNotFound, $"No live deliverable '{carriedDeliverableId}' to carry.", quote);
        }

        return null;
    }

    /// <summary>The archived-project guard (`WP 19.5C`): every mutating command on an archived project's objects is refused, here, before its own mutator ever runs.</summary>
    private async Task<QuotationResult?> ArchivedAsync(Quotation quote, CancellationToken cancellationToken)
    {
        if (quote.ParentId is not { } projectId
            || await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project)
        {
            return null;
        }

        return ProjectArchival.IsArchived(project, _time.GetUtcNow())
            ? new QuotationResult(QuotationRefusal.ProjectArchived, $"Project '{projectId}' is archived (closed {project.ClosedOn:O}); this quotation is read-only.", quote)
            : null;
    }

    private async Task<Guid> FindOrCreateReferenceMilestoneAsync(Quotation quote, Guid projectId, CancellationToken cancellationToken)
    {
        // `TD-88`/`WP 21.5B`: Kind, liveness and display name are all on the
        // index row, so finding the existing milestone (if any) never needs
        // to materialise a single candidate.
        var children = await _context.Repository.ListChildrenAsync(projectId, cancellationToken).ConfigureAwait(false);
        var existing = children.FirstOrDefault(entry =>
            string.Equals(entry.Kind, MilestoneKind, StringComparison.Ordinal) &&
            !entry.IsDeleted &&
            string.Equals(entry.DisplayName, quote.Reference, StringComparison.Ordinal));

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

    /// <summary>
    /// The next <c>&lt;prefix&gt;&lt;year&gt;-&lt;nnn&gt;</c> reference — one
    /// past the highest existing suffix already used for <paramref name="year"/>
    /// under that same prefix, among every quotation this store holds (live
    /// or not: a reference, once used, is never reissued). Shared by both
    /// prefixes this service generates (<see cref="QuotationReferencePrefix"/>
    /// for an ordinary quotation, <see cref="ChangeOrderReferencePrefix"/>
    /// for a change order — `WP 20.10E`): the two vocabularies never
    /// collide, so one scan of every live-or-not <see cref="Quotation"/>
    /// serves either.
    /// </summary>
    private async Task<string> NextReferenceAsync(int year, string prefix, CancellationToken cancellationToken)
    {
        // `TD-88`/`WP 21.5B`: `Reference` is a `Quotation`-own field, not on
        // the index row.
        var existingEntries = await _context.Repository.ListByKindAsync(Quotation.CanonicalKind, cancellationToken).ConfigureAwait(false);
        var existing = await _context.Repository.MaterialiseAsync<Quotation>(existingEntries, cancellationToken).ConfigureAwait(false);
        var fullPrefix = $"{prefix}{year.ToString(CultureInfo.InvariantCulture)}-";

        var max = 0;
        foreach (var candidate in existing)
        {
            if (candidate.Reference.StartsWith(fullPrefix, StringComparison.Ordinal)
                && int.TryParse(candidate.Reference.AsSpan(fullPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                && n > max)
            {
                max = n;
            }
        }

        return $"{fullPrefix}{(max + 1).ToString("000", CultureInfo.InvariantCulture)}";
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
