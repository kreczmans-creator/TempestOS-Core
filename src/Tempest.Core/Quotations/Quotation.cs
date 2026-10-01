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
/// What a <see cref="Quotation"/> is for: an ordinary quotation (the
/// default — unchanged behaviour), or a change order raised against an
/// already-accepted quotation's own project so that work still open
/// against it can be carried forward rather than blocking sign-off
/// (`WP 20.10E`, PO finding D18, `ADR-0152` addendum). A change order is
/// itself an ordinary <see cref="Quotation"/> — same Draft → Sent →
/// Accepted | Declined lifecycle, same reference/currency/lines shape —
/// distinguished only by this closed vocabulary and by carrying, on each
/// of its own lines, the id of a deliverable that already exists
/// (<see cref="QuotationLine.DeliverableId"/> set at
/// <see cref="QuotationService.AddLineAsync"/> time rather than left for
/// <see cref="QuotationService.AcceptAsync"/> to fill in).
/// </summary>
public enum QuotationKind
{
    /// <summary>An ordinary quotation — defines new deliverables and requirements once accepted. The default.</summary>
    Quotation,

    /// <summary>
    /// A change order — every line carries an existing, still-open
    /// deliverable rather than defining a new one; accepting it creates no
    /// deliverable for a carried line, only records the carried id
    /// (`ADR-0152` addendum).
    /// </summary>
    ChangeOrder,
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
/// <param name="DeliverableId">The Deliverable created from this line on Accept. <see langword="null"/> until then — except on a <see cref="QuotationKind.ChangeOrder"/> line, which already carries an existing deliverable's own id from the moment it is added (`WP 20.10E`).</param>
/// <param name="RequirementId">The Requirement created from this line on Accept. <see langword="null"/> until then.</param>
/// <param name="VatRate">
/// This line's own VAT treatment (`WP 21.3B`) — a closed vocabulary, the
/// consultant's own default picked in Settings → Organisation identity.
/// Defaults to <see cref="Core.BusinessGovernance.VatRate.OutOfScope"/>,
/// the enum's own first-declared value, so a line persisted before this
/// Work Package — which carries no VAT rate in its stored state at all —
/// reads back at the identical <see cref="Amount"/> it always has, VAT
/// added at nothing.
/// </param>
/// <param name="RateCardServiceCode">
/// The <c>RateCardEntry.ServiceCode</c> of the project's own pinned rate
/// card this hourly line's <see cref="Rate"/> was taken from (runbook C3:
/// "make the hourly rate a drop down from whatever rate card is applied
/// to the project"). <see langword="null"/> for a fixed-price line, and
/// for an hourly line whose rate was given directly — every line written
/// before runbook C3 reads back this way.
/// </param>
public sealed record QuotationLine(
    Guid Id,
    string Description,
    decimal? Hours,
    Money? Rate,
    Money? FixedPrice,
    Money Amount,
    QuotationLineBasis Basis,
    Guid? DeliverableId = null,
    Guid? RequirementId = null,
    VatRate VatRate = VatRate.OutOfScope,
    string? RateCardServiceCode = null)
{
    /// <summary>This line's own VAT amount — <see cref="Amount"/> (the net figure) times <see cref="VatRate"/>'s own percentage, rounded to two decimal places (`WP 21.3B`).</summary>
    public Money VatAmount => (Amount * VatRate.Percentage()).RoundTo(2);

    /// <summary>This line's own amount inclusive of VAT — <see cref="Amount"/> plus <see cref="VatAmount"/>.</summary>
    public Money GrossAmount => Amount + VatAmount;
}

/// <summary>
/// One approved revision of a <see cref="Quotation"/> — what an export or
/// a send prints as <c>R&lt;n&gt;</c> (runbook C3).
/// </summary>
/// <param name="Number">The revision number — 1 for <c>R1</c>, 2 for <c>R2</c>, ….</param>
/// <param name="SubmittedBy">The identity id of whoever submitted the draft for review.</param>
/// <param name="ApprovedBy">The identity id of the second person who approved it — never the same as <paramref name="SubmittedBy"/>, nor as the quotation's own author.</param>
/// <param name="ApprovedAt">When it was approved.</param>
/// <param name="LinesHash">
/// <see cref="Quotation.ComputeLinesHash"/> of the lines this revision
/// approved (colour review board M18) — what lets an exported
/// <c>R&lt;n&gt;</c> sheet be matched to the stored content it printed
/// rather than only by timestamp. <see langword="null"/> for a revision
/// approved before it was recorded; such a revision still reads back.
/// </param>
public sealed record QuotationRevision(int Number, string SubmittedBy, string ApprovedBy, DateTimeOffset ApprovedAt, string? LinesHash = null)
{
    /// <summary>The revision as printed: <c>R1</c>, <c>R2</c>, ….</summary>
    public string Label => QuotationReview.LabelFor(Number);
}

/// <summary>
/// A <see cref="Quotation"/>'s own draft-and-review state (runbook C3, PO:
/// "save the quote without exporting or sending, save it as a draft and
/// then review by second person, then export becomes R1") — persisted as
/// one JSON value beside the quotation's own <see cref="Quotation.Status"/>.
/// </summary>
/// <param name="RevisionNumber">The latest approved revision's own number; 0 while no revision has ever been approved.</param>
/// <param name="Revisions">Every approved revision, oldest first. Never <see langword="null"/> once read.</param>
/// <param name="DraftSavedAt">When the draft was last saved — every line change saves it, and so does an explicit Save draft.</param>
/// <param name="SubmittedBy">Who submitted the current review. <see langword="null"/> while not in review and never submitted.</param>
/// <param name="SubmittedAt">When the current review was submitted.</param>
/// <param name="ReturnedBy">Who last returned this quotation to draft.</param>
/// <param name="ReturnComment">The reviewer's own comment when they last returned it to draft — cleared on the next submission.</param>
/// <param name="LineEditors">
/// The identity id of everyone who added, changed or removed a line since
/// the last approval (colour review board B1) — none of them may approve
/// the next revision. Cleared by an approval. Never <see langword="null"/>
/// once read; a review stored before it was recorded reads as empty.
/// </param>
public sealed record QuotationReview(
    int RevisionNumber = 0,
    IReadOnlyList<QuotationRevision>? Revisions = null,
    DateTimeOffset? DraftSavedAt = null,
    string? SubmittedBy = null,
    DateTimeOffset? SubmittedAt = null,
    string? ReturnedBy = null,
    string? ReturnComment = null,
    IReadOnlyList<string>? LineEditors = null)
{
    /// <summary>Every approved revision, oldest first.</summary>
    public IReadOnlyList<QuotationRevision> Revisions { get; init; } = Revisions ?? [];

    /// <summary>Everyone who changed a line since the last approval, in the order they first did.</summary>
    public IReadOnlyList<string> LineEditors { get; init; } = LineEditors ?? [];

    /// <summary>The printed label for revision <paramref name="number"/>: <c>R1</c>, <c>R2</c>, ….</summary>
    public static string LabelFor(int number) => $"R{number.ToString(CultureInfo.InvariantCulture)}";
}

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
    private readonly QuotationKind _kind;
    private readonly List<QuotationLine> _lines;
    private QuotationStatus _status;
    private DateOnly? _sentOn;
    private DateOnly? _decidedOn;
    private readonly string? _authorIdentityId;
    private QuotationReview _review;

    /// <summary>Initialises a new instance of the <see cref="Quotation"/> class.</summary>
    public Quotation(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        string reference, DateOnly quoteDate, string? clientOrganisationId, CurrencyCode currency,
        int validityDays, string? terms, IReadOnlyList<QuotationLine> lines,
        QuotationStatus status = QuotationStatus.Draft, DateOnly? sentOn = null, DateOnly? decidedOn = null,
        QuotationKind kind = QuotationKind.Quotation, string? authorIdentityId = null, QuotationReview? review = null)
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
        _kind = kind;
        _lines = [.. lines];
        _status = status;
        _sentOn = sentOn;
        _decidedOn = decidedOn;
        _authorIdentityId = authorIdentityId;
        _review = review ?? new QuotationReview();
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

    /// <summary>Whether this is an ordinary quotation or a change order (`WP 20.10E`). Immutable once created.</summary>
    public QuotationKind QuotationKind => _kind;

    /// <summary>This quotation's own lines, in the order they were added.</summary>
    public IReadOnlyList<QuotationLine> Lines => _lines;

    /// <summary>The sum of every line's own <c>Amount</c> (net of VAT) — computed, never stored, so it can never drift from what the lines actually carry.</summary>
    public Money Total => Money.Sum(_lines.Select(l => l.Amount), _currency);

    /// <summary>The sum of every line's own <see cref="QuotationLine.VatAmount"/> (`WP 21.3B`) — computed, exactly as <see cref="Total"/> is.</summary>
    public Money VatTotal => Money.Sum(_lines.Select(l => l.VatAmount), _currency);

    /// <summary>The sum of every line's own <see cref="QuotationLine.GrossAmount"/> — <see cref="Total"/> plus <see cref="VatTotal"/> (`WP 21.3B`).</summary>
    public Money GrossTotal => Total + VatTotal;

    /// <inheritdoc cref="IHasLifecycle.Status" />
    /// <remarks>Hides <c>IHasLifecycle.Status</c> (the eight-value canonical <see cref="LifecycleState"/>) with this Kind's own status vocabulary (`ADR-0152`), exactly as <c>Tempest.Core.Invoicing.InvoiceRequest.Status</c> does.</remarks>
    public new QuotationStatus Status => _status;

    /// <summary>When this quotation was sent. <see langword="null"/> while still <see cref="QuotationStatus.Draft"/>.</summary>
    public DateOnly? SentOn => _sentOn;

    /// <summary>When this quotation was accepted or declined. <see langword="null"/> while still <see cref="QuotationStatus.Draft"/> or <see cref="QuotationStatus.Sent"/>.</summary>
    public DateOnly? DecidedOn => _decidedOn;

    /// <summary>The identity id of whoever opened this quotation (runbook C3) — the person a second-person approval must differ from. <see langword="null"/> for a quotation opened before runbook C3 (a quotation can no longer be opened with nobody signed in, colour review board B1).</summary>
    public string? AuthorIdentityId => _authorIdentityId;

    /// <summary>This quotation's own draft-and-review state (runbook C3).</summary>
    public QuotationReview Review => _review;

    /// <summary>The latest approved revision's own number — 0 while none has been approved (runbook C3).</summary>
    public int RevisionNumber => _review.RevisionNumber;

    /// <summary>
    /// Whether this quotation, as it stands, is an approved revision —
    /// <see cref="QuotationStatus.Approved"/>, or already sent and
    /// answered. A <see cref="QuotationStatus.Draft"/> or
    /// <see cref="QuotationStatus.InReview"/> one is not, even after an
    /// earlier revision was approved: its lines may differ from it.
    /// </summary>
    public bool IsApprovedRevision =>
        _review.RevisionNumber > 0
        && _status is QuotationStatus.Approved or QuotationStatus.Sent or QuotationStatus.Accepted or QuotationStatus.Declined;

    /// <summary>The revision this quotation prints as — <c>R1</c>, <c>R2</c>, … — or <see langword="null"/> while it is a draft (see <see cref="IsApprovedRevision"/>).</summary>
    public string? RevisionLabel => IsApprovedRevision ? QuotationReview.LabelFor(_review.RevisionNumber) : null;

    /// <summary>
    /// A stable fingerprint of <paramref name="lines"/> — the SHA-256 of
    /// their stored JSON, as <c>sha256:&lt;hex&gt;</c> (colour review board
    /// M18). Recorded on every <see cref="QuotationRevision"/> at approval.
    /// </summary>
    public static string ComputeLinesHash(IEnumerable<QuotationLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(lines.ToList());
        return "sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(json));
    }

    /// <summary><see cref="ComputeLinesHash"/> of this quotation's own lines as they stand.</summary>
    public string LinesHash => ComputeLinesHash(_lines);

    /// <summary>Adds a new line. <see cref="QuotationService"/> decides whether this quotation's lines may change before this ever runs; an <see cref="QuotationStatus.Approved"/> one reopens as a new draft.</summary>
    internal Task AddLineAsync(QuotationLine line, string editedBy, DateTimeOffset savedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);

        return PersistLinesAsync(
            new List<QuotationLine>(_lines) { line }, editedBy, savedAt, $"Line added: '{line.Description}' ({line.Amount}).", cancellationToken);
    }

    /// <summary>Replaces the line whose <see cref="QuotationLine.Id"/> matches <paramref name="line"/>'s own. <see cref="QuotationService"/> has already confirmed the line exists.</summary>
    internal Task UpdateLineAsync(QuotationLine line, string editedBy, DateTimeOffset savedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);

        return PersistLinesAsync(
            _lines.Select(l => l.Id == line.Id ? line : l).ToList(), editedBy, savedAt, $"Line updated: '{line.Description}' ({line.Amount}).", cancellationToken);
    }

    /// <summary>Removes the line identified by <paramref name="lineId"/>. <see cref="QuotationService"/> has already confirmed it exists.</summary>
    internal Task RemoveLineAsync(Guid lineId, string removedDescription, string editedBy, DateTimeOffset savedAt, CancellationToken cancellationToken = default) =>
        PersistLinesAsync(_lines.Where(l => l.Id != lineId).ToList(), editedBy, savedAt, $"Line removed: '{removedDescription}'.", cancellationToken);

    /// <summary>
    /// Writes <paramref name="next"/> as this quotation's lines and stamps
    /// the draft as saved — in one transaction, one audit row. An
    /// <see cref="QuotationStatus.Approved"/> quotation moves back to
    /// <see cref="QuotationStatus.Draft"/> in that same transaction: an
    /// edit after approval starts a new draft, and the next approval
    /// issues the next revision (runbook C3). <paramref name="editedBy"/>
    /// joins <see cref="QuotationReview.LineEditors"/>, so they cannot
    /// approve that next revision (colour review board B1).
    /// </summary>
    private Task PersistLinesAsync(List<QuotationLine> next, string editedBy, DateTimeOffset savedAt, string auditDetail, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(editedBy);

        var reopens = _status == QuotationStatus.Approved;
        var detail = reopens
            ? $"{auditDetail} Approved {QuotationReview.LabelFor(_review.RevisionNumber)} reopened as a new draft."
            : auditDetail;
        var editors = _review.LineEditors.Contains(editedBy, StringComparer.Ordinal)
            ? _review.LineEditors
            : [.. _review.LineEditors, editedBy];
        var review = _review with { DraftSavedAt = savedAt, LineEditors = editors };

        return MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(Lines), next);
                WriteJson(state, nameof(Review), review);
                if (reopens)
                    state[nameof(Status)] = QuotationStatus.Draft.ToString();
                return state;
            },
            // A no-op: ApplyTypeState (below) already reads the committed
            // Lines value back — MutateAndPersistAsync's own afterCommit
            // calls it before this apply callback ever runs, so adding the
            // line again here would double it (found, then fixed, by this
            // Work Package's own first test run).
            static () => { },
            detail,
            cancellationToken,
            reopens ? WorkspaceChangeType.StatusChanged : WorkspaceChangeType.Updated);
    }

    /// <summary>Records an explicit Save draft — no export, no send, only the saved-at stamp and its own audit row (runbook C3). <see cref="QuotationService.SaveDraftAsync"/> confirms this quotation is Draft first.</summary>
    internal Task SaveDraftAsync(DateTimeOffset savedAt, CancellationToken cancellationToken = default) =>
        PersistReviewAsync(
            null, _review with { DraftSavedAt = savedAt },
            $"Draft saved at {savedAt:u} with {_lines.Count} line(s), total {Total}.", WorkspaceChangeType.Updated, cancellationToken);

    /// <summary>Moves this quotation to <see cref="QuotationStatus.InReview"/>, recording who submitted it (runbook C3).</summary>
    internal Task MarkInReviewAsync(string submittedBy, DateTimeOffset submittedAt, CancellationToken cancellationToken = default) =>
        PersistReviewAsync(
            QuotationStatus.InReview,
            _review with { SubmittedBy = submittedBy, SubmittedAt = submittedAt, ReturnedBy = null, ReturnComment = null },
            $"Submitted for review by '{submittedBy}' at {submittedAt:u}.", WorkspaceChangeType.StatusChanged, cancellationToken);

    /// <summary>Returns this quotation from review to <see cref="QuotationStatus.Draft"/>, with the reviewer's own comment (runbook C3).</summary>
    internal Task MarkReturnedToDraftAsync(string returnedBy, string comment, DateTimeOffset returnedAt, CancellationToken cancellationToken = default) =>
        PersistReviewAsync(
            QuotationStatus.Draft,
            _review with { ReturnedBy = returnedBy, ReturnComment = comment, SubmittedBy = null, SubmittedAt = null, DraftSavedAt = returnedAt },
            $"Returned to draft by '{returnedBy}' at {returnedAt:u}: {comment}", WorkspaceChangeType.StatusChanged, cancellationToken);

    /// <summary>Approves this quotation's current review as the next revision — <c>R1</c>, then <c>R2</c>, … (runbook C3), recording the approved lines' own <see cref="LinesHash"/> (M18) and clearing <see cref="QuotationReview.LineEditors"/>. <see cref="QuotationService.ApproveAsync"/> has already refused an approver who is not a second person.</summary>
    internal Task MarkApprovedAsync(string approvedBy, DateTimeOffset approvedAt, CancellationToken cancellationToken = default)
    {
        var revision = new QuotationRevision(_review.RevisionNumber + 1, _review.SubmittedBy ?? string.Empty, approvedBy, approvedAt, LinesHash);

        return PersistReviewAsync(
            QuotationStatus.Approved,
            _review with { RevisionNumber = revision.Number, Revisions = [.. _review.Revisions, revision], LineEditors = [] },
            $"Approved as {revision.Label} by '{approvedBy}' at {approvedAt:u} (submitted by '{revision.SubmittedBy}').",
            WorkspaceChangeType.StatusChanged, cancellationToken);
    }

    private Task PersistReviewAsync(
        QuotationStatus? status, QuotationReview review, string auditDetail, WorkspaceChangeType changeType, CancellationToken cancellationToken) =>
        MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                if (status is { } next)
                    state[nameof(Status)] = next.ToString();
                WriteJson(state, nameof(Review), review);
                return state;
            },
            // A no-op — ApplyTypeState (below) already reads Status and
            // Review back from the committed state.
            static () => { },
            auditDetail,
            cancellationToken,
            changeType);

    /// <summary>Moves this quotation to <see cref="QuotationStatus.Sent"/> and records the date. <see cref="QuotationService.SendAsync"/> checks <see cref="QuotationStatusTransitions"/> and that at least one line exists before this ever runs.</summary>
    internal Task MarkSentAsync(DateOnly sentOn, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(Status)] = QuotationStatus.Sent.ToString() };
                WriteJson(state, nameof(SentOn), sentOn);
                return state;
            },
            // A no-op — ApplyTypeState (below) already reads Status and
            // SentOn back from the committed state.
            static () => { },
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
            // A no-op — ApplyTypeState (below) already reads Status,
            // DecidedOn and Lines (with every created id) back from the
            // committed state.
            static () => { },
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
            // A no-op — ApplyTypeState (below) already reads Status and
            // DecidedOn back from the committed state.
            static () => { },
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
        state[nameof(QuotationKind)] = _kind.ToString();
        WriteJson(state, nameof(Lines), _lines);
        state[nameof(Status)] = _status.ToString();
        WriteJson(state, nameof(SentOn), _sentOn);
        WriteJson(state, nameof(DecidedOn), _decidedOn);
        state[nameof(AuthorIdentityId)] = _authorIdentityId;
        WriteJson(state, nameof(Review), _review);
    }

    /// <inheritdoc />
    protected override void ApplyTypeState(EngineeringObjectState state)
    {
        _lines.Clear();
        _lines.AddRange(ReadLines(state));
        _status = ReadStatus(state);
        _sentOn = state.TypeJson<DateOnly?>(nameof(SentOn));
        _decidedOn = state.TypeJson<DateOnly?>(nameof(DecidedOn));
        _review = ReadReview(state, _status);
    }

    /// <summary>
    /// Reads back <see cref="Review"/> (runbook C3). A quotation persisted
    /// before runbook C3 carries none: a Draft one reads as never
    /// reviewed; one already Sent, Accepted or Declined went to its client
    /// as it stood, so it reads as its first issued revision, <c>R1</c>
    /// (with no recorded approver — none was asked for then).
    /// </summary>
    private static QuotationReview ReadReview(EngineeringObjectState state, QuotationStatus status)
    {
        if (state.TypeJson<QuotationReview>(nameof(Review)) is { } stored)
            return stored;

        return status is QuotationStatus.Sent or QuotationStatus.Accepted or QuotationStatus.Declined
            ? new QuotationReview(RevisionNumber: 1)
            : new QuotationReview();
    }

    private static List<QuotationLine> ReadLines(EngineeringObjectState state) =>
        state.TypeJson<List<QuotationLine>>(nameof(Lines)) ?? [];

    private static QuotationStatus ReadStatus(EngineeringObjectState state) =>
        Enum.TryParse<QuotationStatus>(state.Type(nameof(Status)), out var value) ? value : QuotationStatus.Draft;

    /// <summary>Reads back <see cref="QuotationKind"/> — <see cref="QuotationKind.Quotation"/> (unchanged behaviour) for a pre-`WP 20.10E` record that carries no such state at all.</summary>
    private static QuotationKind ReadQuotationKind(EngineeringObjectState state) =>
        Enum.TryParse<QuotationKind>(state.Type(nameof(QuotationKind)), out var value) ? value : QuotationKind.Quotation;

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
            state.TypeJson<DateOnly?>(nameof(DecidedOn)),
            ReadQuotationKind(state),
            state.Type(nameof(AuthorIdentityId)),
            ReadReview(state, ReadStatus(state)));
}
