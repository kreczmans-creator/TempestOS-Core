using System.Globalization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;

namespace Tempest.Core.Quotations;

/// <summary>
/// How one <see cref="QuotationLine"/> was priced: hours at a rate, or a
/// single fixed price — never both (`WP 19.5A`, `ADR-0152`).
/// </summary>
public enum QuotationLineBasis
{
    /// <summary>Priced as <see cref="QuotationLine.Hours"/> times <see cref="QuotationLine.Rate"/>.</summary>
    Hourly,

    /// <summary>Priced as <see cref="QuotationLine.FixedPrice"/> alone.</summary>
    FixedPrice,
}

/// <summary>
/// One line of a <see cref="Quotation"/> — a described piece of work, its
/// own price, and, once the quotation is accepted, the Deliverable and
/// Requirement created from it (`WP 19.5A`, `ADR-0152`).
/// </summary>
/// <param name="Id">This line's own identity — stable across an <see cref="QuotationService.UpdateLineAsync"/>, what <see cref="QuotationService.RemoveLineAsync"/> addresses.</param>
/// <param name="Description">What the line is — becomes the created Deliverable's and Requirement's own title.</param>
/// <param name="Hours">Billable hours, for an <see cref="QuotationLineBasis.Hourly"/> line. <see langword="null"/> for a fixed-price line.</param>
/// <param name="Rate">The rate one hour bills at, for an <see cref="QuotationLineBasis.Hourly"/> line. <see langword="null"/> for a fixed-price line.</param>
/// <param name="FixedPrice">The line's own fixed price, for a <see cref="QuotationLineBasis.FixedPrice"/> line. <see langword="null"/> for an hourly line.</param>
/// <param name="Amount"><see cref="Hours"/> times <see cref="Rate"/>, or <see cref="FixedPrice"/> — carried alongside rather than recomputed, exactly as <c>Tempest.Core.Invoicing.InvoiceRequestLine.Amount</c> is.</param>
/// <param name="Basis">Which of the two ways this line is priced.</param>
/// <param name="DeliverableId">The Deliverable created from this line on Accept. <see langword="null"/> until then.</param>
/// <param name="RequirementId">The Requirement created from this line on Accept. <see langword="null"/> until then.</param>
public sealed record QuotationLine(
    Guid Id,
    string Description,
    decimal? Hours,
    Money? Rate,
    Money? FixedPrice,
    Money Amount,
    QuotationLineBasis Basis,
    Guid? DeliverableId = null,
    Guid? RequirementId = null);

/// <summary>
/// A quotation raised against a project's client: a reference, a date, a
/// currency, validity, terms, and lines each priced hourly or fixed —
/// opened with the project and, once accepted, the source of the
/// project's own initial deliverables and requirements (`WP 19.5A`,
/// `ADR-0152`, Product Owner comment item 4). Follows
/// <c>Tempest.Core.Invoicing.InvoiceRequest</c>'s own shape exactly: an
/// <c>EngineeringObjectBase</c> subtype carrying its own state through
/// <c>CaptureTypeState</c>/<c>ApplyTypeState</c>/<see cref="IRehydratable{Quotation}.Rehydrate"/>,
/// parented to the project it quotes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Whether an act is permitted is decided by <see cref="QuotationService"/>,
/// before any mutator below ever runs</b> — this class only ever persists
/// what it is told to, exactly as <c>InvoiceRequest</c>'s own mutators do
/// for <c>InvoicingService</c>.
/// </para>
/// <para>
/// <b>No revision of an accepted quotation.</b> There is no mutator that
/// changes a line, or anything else, once <see cref="Status"/> reaches
/// <see cref="QuotationStatus.Accepted"/> — a change to accepted work is a
/// new quotation, never an edit to this one (`ADR-0152`, out of scope for
/// `v0.19.1`).
/// </para>
/// </remarks>
public sealed class Quotation : EngineeringObjectBase, IRehydratable<Quotation>
{
    /// <summary>The <see cref="IEngineeringObject.Kind"/> every quotation's own backing document carries (`ADR-0105`).</summary>
    public const string CanonicalKind = "Quotation";

    private readonly string _reference;
    private readonly DateOnly _quoteDate;
    private readonly string? _clientOrganisationId;
    private readonly CurrencyCode _currency;
    private readonly int _validityDays;
    private readonly string? _terms;
    private readonly List<QuotationLine> _lines;
    private QuotationStatus _status;
    private DateOnly? _sentOn;
    private DateOnly? _decidedOn;

    /// <summary>Initialises a new instance of the <see cref="Quotation"/> class.</summary>
    public Quotation(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        string reference, DateOnly quoteDate, string? clientOrganisationId, CurrencyCode currency,
        int validityDays, string? terms, IReadOnlyList<QuotationLine> lines,
        QuotationStatus status = QuotationStatus.Draft, DateOnly? sentOn = null, DateOnly? decidedOn = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentNullException.ThrowIfNull(lines);

        _reference = reference;
        _quoteDate = quoteDate;
        _clientOrganisationId = clientOrganisationId;
        _currency = currency;
        _validityDays = validityDays;
        _terms = terms;
        _lines = [.. lines];
        _status = status;
        _sentOn = sentOn;
        _decidedOn = decidedOn;
    }

    /// <summary>This quotation's own reference — given at creation, or generated as <c>Q-&lt;yyyy&gt;-&lt;nnn&gt;</c> from a per-year count of existing quotations.</summary>
    public string Reference => _reference;

    /// <summary>The date this quotation was raised.</summary>
    public DateOnly QuoteDate => _quoteDate;

    /// <summary>The client this quotation is raised against — an Organisation-catalogue id, defaulted from the project's own client at creation, never validated as a real record by this class.</summary>
    public string? ClientOrganisationId => _clientOrganisationId;

    /// <summary>The currency every line and <see cref="Total"/> are stated in — resolved from the project's own pinned rate card at the moment this quotation was created, or GBP when none is pinned.</summary>
    public CurrencyCode Currency => _currency;

    /// <summary>How many days from <see cref="QuoteDate"/> this quotation stays valid. Defaults to 30.</summary>
    public int ValidityDays => _validityDays;

    /// <summary>Free-text terms shown on the quote. <see langword="null"/> when none are recorded.</summary>
    public string? Terms => _terms;

    /// <summary>This quotation's own lines, in the order they were added.</summary>
    public IReadOnlyList<QuotationLine> Lines => _lines;

    /// <summary>The sum of every line's own <c>Amount</c> — computed, never stored, so it can never drift from what the lines actually carry.</summary>
    public Money Total => Money.Sum(_lines.Select(l => l.Amount), _currency);

    /// <inheritdoc cref="IHasLifecycle.Status" />
    /// <remarks>Hides <c>IHasLifecycle.Status</c> (the eight-value canonical <see cref="LifecycleState"/>) with this Kind's own status vocabulary (`ADR-0152`), exactly as <c>Tempest.Core.Invoicing.InvoiceRequest.Status</c> does.</remarks>
    public new QuotationStatus Status => _status;

    /// <summary>When this quotation was sent. <see langword="null"/> while still <see cref="QuotationStatus.Draft"/>.</summary>
    public DateOnly? SentOn => _sentOn;

    /// <summary>When this quotation was accepted or declined. <see langword="null"/> while still <see cref="QuotationStatus.Draft"/> or <see cref="QuotationStatus.Sent"/>.</summary>
    public DateOnly? DecidedOn => _decidedOn;

    /// <summary>Adds a new line. <see cref="QuotationService"/> decides whether this quotation is still Draft before this ever runs.</summary>
    internal Task AddLineAsync(QuotationLine line, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);

        return MutateTypeStateAndPersistAsync(
            () =>
            {
                var next = new List<QuotationLine>(_lines) { line };
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(Lines), next);
                return state;
            },
            () => _lines.Add(line),
            $"Line added: '{line.Description}' ({line.Amount}).",
            cancellationToken);
    }

    /// <summary>Replaces the line whose <see cref="QuotationLine.Id"/> matches <paramref name="line"/>'s own. <see cref="QuotationService"/> has already confirmed the line exists.</summary>
    internal Task UpdateLineAsync(QuotationLine line, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);

        return MutateTypeStateAndPersistAsync(
            () =>
            {
                var next = _lines.Select(l => l.Id == line.Id ? line : l).ToList();
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(Lines), next);
                return state;
            },
            () =>
            {
                var index = _lines.FindIndex(l => l.Id == line.Id);
                if (index >= 0)
                    _lines[index] = line;
            },
            $"Line updated: '{line.Description}' ({line.Amount}).",
            cancellationToken);
    }

    /// <summary>Removes the line identified by <paramref name="lineId"/>. <see cref="QuotationService"/> has already confirmed it exists.</summary>
    internal Task RemoveLineAsync(Guid lineId, string removedDescription, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () =>
            {
                var next = _lines.Where(l => l.Id != lineId).ToList();
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(Lines), next);
                return state;
            },
            () => _lines.RemoveAll(l => l.Id == lineId),
            $"Line removed: '{removedDescription}'.",
            cancellationToken);

    /// <summary>Moves this quotation to <see cref="QuotationStatus.Sent"/> and records the date. <see cref="QuotationService.SendAsync"/> checks <see cref="QuotationStatusTransitions"/> and that at least one line exists before this ever runs.</summary>
    internal Task MarkSentAsync(DateOnly sentOn, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(Status)] = QuotationStatus.Sent.ToString() };
                WriteJson(state, nameof(SentOn), sentOn);
                return state;
            },
            () =>
            {
                _status = QuotationStatus.Sent;
                _sentOn = sentOn;
            },
            $"Sent on {sentOn:O}.",
            cancellationToken,
            WorkspaceChangeType.StatusChanged);

    /// <summary>
    /// Moves this quotation to <see cref="QuotationStatus.Accepted"/>,
    /// records the date, and records <paramref name="fulfilledLines"/> —
    /// each line carrying the <see cref="QuotationLine.DeliverableId"/>/
    /// <see cref="QuotationLine.RequirementId"/> <see cref="QuotationService.AcceptAsync"/>
    /// already created, each its own transaction, before this one runs. One
    /// transaction, one audit row, for the status move and the recorded ids
    /// together — what makes a second Accept refused as already accepted
    /// (`ADR-0152` §5, mirroring `ADR-0151` §5's identical disclosure).
    /// </summary>
    internal Task MarkAcceptedAsync(DateOnly decidedOn, IReadOnlyList<QuotationLine> fulfilledLines, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fulfilledLines);

        return MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(Status)] = QuotationStatus.Accepted.ToString() };
                WriteJson(state, nameof(DecidedOn), decidedOn);
                WriteJson(state, nameof(Lines), fulfilledLines);
                return state;
            },
            () =>
            {
                _status = QuotationStatus.Accepted;
                _decidedOn = decidedOn;
                _lines.Clear();
                _lines.AddRange(fulfilledLines);
            },
            "Accepted — a Deliverable and a Requirement created for every line.",
            cancellationToken,
            WorkspaceChangeType.StatusChanged);
    }

    /// <summary>Moves this quotation to <see cref="QuotationStatus.Declined"/> and records the date. Creates nothing.</summary>
    internal Task MarkDeclinedAsync(DateOnly decidedOn, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(Status)] = QuotationStatus.Declined.ToString() };
                WriteJson(state, nameof(DecidedOn), decidedOn);
                return state;
            },
            () =>
            {
                _status = QuotationStatus.Declined;
                _decidedOn = decidedOn;
            },
            $"Declined on {decidedOn:O}.",
            cancellationToken,
            WorkspaceChangeType.StatusChanged);

    /// <inheritdoc />
    /// <remarks>
    /// Only the fields a mutator can change after creation are written here
    /// — <see cref="Reference"/>, <see cref="QuoteDate"/>,
    /// <see cref="ClientOrganisationId"/>, <see cref="Currency"/>,
    /// <see cref="ValidityDays"/> and <see cref="Terms"/> are immutable
    /// once set, exactly as <c>InvoiceRequest.ClientOrganisationId</c>/
    /// <c>Currency</c> are — captured once, by <see cref="Rehydrate"/>,
    /// never re-applied by a running instance.
    /// </remarks>
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(Reference)] = _reference;
        WriteJson(state, nameof(QuoteDate), _quoteDate);
        state[nameof(ClientOrganisationId)] = _clientOrganisationId;
        WriteJson(state, nameof(Currency), _currency);
        state[nameof(ValidityDays)] = _validityDays.ToString(CultureInfo.InvariantCulture);
        state[nameof(Terms)] = _terms;
        WriteJson(state, nameof(Lines), _lines);
        state[nameof(Status)] = _status.ToString();
        WriteJson(state, nameof(SentOn), _sentOn);
        WriteJson(state, nameof(DecidedOn), _decidedOn);
    }

    /// <inheritdoc />
    protected override void ApplyTypeState(EngineeringObjectState state)
    {
        _lines.Clear();
        _lines.AddRange(ReadLines(state));
        _status = ReadStatus(state);
        _sentOn = state.TypeJson<DateOnly?>(nameof(SentOn));
        _decidedOn = state.TypeJson<DateOnly?>(nameof(DecidedOn));
    }

    private static List<QuotationLine> ReadLines(EngineeringObjectState state) =>
        state.TypeJson<List<QuotationLine>>(nameof(Lines)) ?? [];

    private static QuotationStatus ReadStatus(EngineeringObjectState state) =>
        Enum.TryParse<QuotationStatus>(state.Type(nameof(Status)), out var value) ? value : QuotationStatus.Draft;

    private static int ReadValidityDays(EngineeringObjectState state) =>
        int.TryParse(state.Type(nameof(ValidityDays)), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 30;

    static Quotation IRehydratable<Quotation>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.Type(nameof(Reference)) ?? string.Empty,
            state.TypeJson<DateOnly>(nameof(QuoteDate)),
            state.Type(nameof(ClientOrganisationId)),
            state.TypeJson<CurrencyCode>(nameof(Currency)),
            ReadValidityDays(state),
            state.Type(nameof(Terms)),
            ReadLines(state),
            ReadStatus(state),
            state.TypeJson<DateOnly?>(nameof(SentOn)),
            state.TypeJson<DateOnly?>(nameof(DecidedOn)));
}
