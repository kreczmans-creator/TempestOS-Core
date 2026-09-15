using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;

namespace Tempest.Core.PurchaseOrders;

/// <summary>
/// One line of a <see cref="PurchaseOrder"/> — a described quantity of
/// goods or service, its own unit price, net amount and VAT treatment
/// (`WP 21.3B`).
/// </summary>
/// <param name="Id">This line's own identity — stable across an <see cref="PurchaseOrderService.UpdateLineAsync"/>, what <see cref="PurchaseOrderService.RemoveLineAsync"/> addresses.</param>
/// <param name="Description">What the line is.</param>
/// <param name="Quantity">How many units.</param>
/// <param name="UnitPrice">The price of one unit.</param>
/// <param name="Net"><see cref="Quantity"/> times <see cref="UnitPrice"/>, carried alongside rather than recomputed, exactly as <c>Tempest.Core.Quotations.QuotationLine.Amount</c> is.</param>
/// <param name="VatRate">This line's own VAT treatment — the identical closed vocabulary <c>QuotationLine.VatRate</c> carries.</param>
public sealed record PurchaseOrderLine(
    Guid Id,
    string Description,
    decimal Quantity,
    Money UnitPrice,
    Money Net,
    VatRate VatRate = VatRate.OutOfScope)
{
    /// <summary>This line's own VAT amount — <see cref="Net"/> times <see cref="VatRate"/>'s own percentage, rounded to two decimal places.</summary>
    public Money VatAmount => (Net * VatRate.Percentage()).RoundTo(2);

    /// <summary>This line's own amount inclusive of VAT — <see cref="Net"/> plus <see cref="VatAmount"/>.</summary>
    public Money GrossAmount => Net + VatAmount;
}

/// <summary>
/// A canonical Kind recording one purchase order raised against a
/// supplier: a reference, the supplier, lines and their prices, a
/// lifecycle from Draft through to Closed or Cancelled, and the act of
/// turning a received order's own lines into project expenses (`WP
/// 21.3B`). Follows <c>Tempest.Core.Quotations.Quotation</c>'s own shape
/// exactly: an <c>EngineeringObjectBase</c> subtype carrying its own state
/// through <c>CaptureTypeState</c>/<c>ApplyTypeState</c>/<see cref="IRehydratable{PurchaseOrder}.Rehydrate"/>,
/// parented to the project it was raised on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not ERP, not PLM (Product Owner guard).</b> This Kind carries no
/// supplier catalogue, no stock, no goods-received-note reconciliation and
/// no procurement workflow — a reference, a supplier tag, lines and a
/// status, exactly what the invoice seam and a printed purchase-order
/// document (`WP 21.2A`) need.
/// </para>
/// <para>
/// <b>Whether an act is permitted is decided by <see cref="PurchaseOrderService"/>,
/// before any mutator below ever runs</b> — this class only ever persists
/// what it is told to, exactly as <c>Quotation</c>'s own mutators do for
/// <c>QuotationService</c>.
/// </para>
/// </remarks>
public sealed class PurchaseOrder : EngineeringObjectBase, IRehydratable<PurchaseOrder>
{
    /// <summary>The <see cref="IEngineeringObject.Kind"/> every purchase order's own backing document carries (`ADR-0105`).</summary>
    public const string CanonicalKind = "PurchaseOrder";

    private readonly string _reference;
    private readonly string? _supplierOrganisationId;
    private readonly CurrencyCode _currency;
    private readonly List<PurchaseOrderLine> _lines;
    private PurchaseOrderStatus _status;
    private DateOnly? _issuedDate;
    private DateOnly? _expectedDelivery;
    private string? _notes;
    private bool _expensesRecorded;

    /// <summary>Initialises a new instance of the <see cref="PurchaseOrder"/> class.</summary>
    public PurchaseOrder(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        string reference, string? supplierOrganisationId, CurrencyCode currency, IReadOnlyList<PurchaseOrderLine> lines,
        PurchaseOrderStatus status = PurchaseOrderStatus.Draft, DateOnly? issuedDate = null, DateOnly? expectedDelivery = null,
        string? notes = null, bool expensesRecorded = false)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentNullException.ThrowIfNull(lines);

        _reference = reference;
        _supplierOrganisationId = supplierOrganisationId;
        _currency = currency;
        _lines = [.. lines];
        _status = status;
        _issuedDate = issuedDate;
        _expectedDelivery = expectedDelivery;
        _notes = notes;
        _expensesRecorded = expensesRecorded;
    }

    /// <summary>This purchase order's own reference — given at creation, or generated as <c>PO-&lt;yyyy&gt;-&lt;nnn&gt;</c> from a per-year count of existing purchase orders, the identical scan-the-store discipline <c>Quotation.Reference</c> uses.</summary>
    public string Reference => _reference;

    /// <summary>The supplier this order is raised against — an Organisation-catalogue id, a tag, never validated by this class (the identical rule <c>Quotation.ClientOrganisationId</c> follows).</summary>
    public string? SupplierOrganisationId => _supplierOrganisationId;

    /// <summary>The currency every line and total is stated in — resolved from the project's own pinned rate card at the moment this order was created, or GBP when none is pinned.</summary>
    public CurrencyCode Currency => _currency;

    /// <summary>This purchase order's own lines, in the order they were added.</summary>
    public IReadOnlyList<PurchaseOrderLine> Lines => _lines;

    /// <summary>The sum of every line's own <c>Net</c> — computed, never stored, so it can never drift from what the lines actually carry.</summary>
    public Money Total => Money.Sum(_lines.Select(l => l.Net), _currency);

    /// <summary>The sum of every line's own <see cref="PurchaseOrderLine.VatAmount"/>.</summary>
    public Money VatTotal => Money.Sum(_lines.Select(l => l.VatAmount), _currency);

    /// <summary>The sum of every line's own <see cref="PurchaseOrderLine.GrossAmount"/> — <see cref="Total"/> plus <see cref="VatTotal"/>.</summary>
    public Money GrossTotal => Total + VatTotal;

    /// <inheritdoc cref="IHasLifecycle.Status" />
    /// <remarks>Hides <c>IHasLifecycle.Status</c> (the eight-value canonical <see cref="LifecycleState"/>) with this Kind's own status vocabulary, exactly as <c>Quotation.Status</c> does.</remarks>
    public new PurchaseOrderStatus Status => _status;

    /// <summary>When this order was issued to the supplier. <see langword="null"/> while still <see cref="PurchaseOrderStatus.Draft"/>.</summary>
    public DateOnly? IssuedDate => _issuedDate;

    /// <summary>When the goods or service are expected. <see langword="null"/> if not stated.</summary>
    public DateOnly? ExpectedDelivery => _expectedDelivery;

    /// <summary>Free-text notes. <see langword="null"/> when none are recorded.</summary>
    /// <remarks>Hides <c>EngineeringObjectBase.Notes</c> (always <see langword="null"/> unless the base class's own metadata sets it, which this class never does) with this Kind's own explicitly-captured field.</remarks>
    public new string? Notes => _notes;

    /// <summary>Whether <see cref="PurchaseOrderService.RecordLinesAsExpensesAsync"/> has already run for this order — set once, never cleared, so a second attempt is refused rather than double-recording every line as a second set of expenses.</summary>
    public bool ExpensesRecorded => _expensesRecorded;

    /// <summary>Adds a new line. <see cref="PurchaseOrderService"/> decides whether this order is still Draft before this ever runs.</summary>
    internal Task AddLineAsync(PurchaseOrderLine line, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);

        return MutateTypeStateAndPersistAsync(
            () =>
            {
                var next = new List<PurchaseOrderLine>(_lines) { line };
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(Lines), next);
                return state;
            },
            static () => { },
            $"Line added: '{line.Description}' ({line.Net}).",
            cancellationToken);
    }

    /// <summary>Replaces the line whose <see cref="PurchaseOrderLine.Id"/> matches <paramref name="line"/>'s own. <see cref="PurchaseOrderService"/> has already confirmed the line exists.</summary>
    internal Task UpdateLineAsync(PurchaseOrderLine line, CancellationToken cancellationToken = default)
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
            static () => { },
            $"Line updated: '{line.Description}' ({line.Net}).",
            cancellationToken);
    }

    /// <summary>Removes the line identified by <paramref name="lineId"/>. <see cref="PurchaseOrderService"/> has already confirmed it exists.</summary>
    internal Task RemoveLineAsync(Guid lineId, string removedDescription, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () =>
            {
                var next = _lines.Where(l => l.Id != lineId).ToList();
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(Lines), next);
                return state;
            },
            static () => { },
            $"Line removed: '{removedDescription}'.",
            cancellationToken);

    /// <summary>Moves this order to <see cref="PurchaseOrderStatus.Issued"/> and records the date. <see cref="PurchaseOrderService.IssueAsync"/> checks <see cref="PurchaseOrderStatusTransitions"/> and that at least one line exists before this ever runs.</summary>
    internal Task MarkIssuedAsync(DateOnly issuedDate, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(Status)] = PurchaseOrderStatus.Issued.ToString() };
                WriteJson(state, nameof(IssuedDate), issuedDate);
                return state;
            },
            static () => { },
            $"Issued on {issuedDate:O}.",
            cancellationToken,
            WorkspaceChangeType.StatusChanged);

    /// <summary>Moves this order to <see cref="PurchaseOrderStatus.Received"/>.</summary>
    internal Task MarkReceivedAsync(CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(Status)] = PurchaseOrderStatus.Received.ToString() },
            () => _status = PurchaseOrderStatus.Received,
            "Received.",
            cancellationToken,
            WorkspaceChangeType.StatusChanged);

    /// <summary>Moves this order to <see cref="PurchaseOrderStatus.Closed"/>.</summary>
    internal Task MarkClosedAsync(CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(Status)] = PurchaseOrderStatus.Closed.ToString() },
            () => _status = PurchaseOrderStatus.Closed,
            "Closed.",
            cancellationToken,
            WorkspaceChangeType.StatusChanged);

    /// <summary>Moves this order to <see cref="PurchaseOrderStatus.Cancelled"/>.</summary>
    internal Task MarkCancelledAsync(CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(Status)] = PurchaseOrderStatus.Cancelled.ToString() },
            () => _status = PurchaseOrderStatus.Cancelled,
            "Cancelled.",
            cancellationToken,
            WorkspaceChangeType.StatusChanged);

    /// <summary>Records that <see cref="PurchaseOrderService.RecordLinesAsExpensesAsync"/> has run — set once, never cleared.</summary>
    internal Task MarkExpensesRecordedAsync(CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(ExpensesRecorded)] = bool.TrueString },
            () => _expensesRecorded = true,
            "Lines recorded as project expenses.",
            cancellationToken);

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(Reference)] = _reference;
        state[nameof(SupplierOrganisationId)] = _supplierOrganisationId;
        WriteJson(state, nameof(Currency), _currency);
        WriteJson(state, nameof(Lines), _lines);
        state[nameof(Status)] = _status.ToString();
        WriteJson(state, nameof(IssuedDate), _issuedDate);
        WriteJson(state, nameof(ExpectedDelivery), _expectedDelivery);
        state[nameof(Notes)] = _notes;
        state[nameof(ExpensesRecorded)] = _expensesRecorded.ToString();
    }

    /// <inheritdoc />
    protected override void ApplyTypeState(EngineeringObjectState state)
    {
        _lines.Clear();
        _lines.AddRange(ReadLines(state));
        _status = ReadStatus(state);
        _issuedDate = state.TypeJson<DateOnly?>(nameof(IssuedDate));
        _expectedDelivery = state.TypeJson<DateOnly?>(nameof(ExpectedDelivery));
        _notes = state.Type(nameof(Notes));
        _expensesRecorded = bool.TryParse(state.Type(nameof(ExpensesRecorded)), out var recorded) && recorded;
    }

    private static List<PurchaseOrderLine> ReadLines(EngineeringObjectState state) =>
        state.TypeJson<List<PurchaseOrderLine>>(nameof(Lines)) ?? [];

    private static PurchaseOrderStatus ReadStatus(EngineeringObjectState state) =>
        Enum.TryParse<PurchaseOrderStatus>(state.Type(nameof(Status)), out var value) ? value : PurchaseOrderStatus.Draft;

    static PurchaseOrder IRehydratable<PurchaseOrder>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.Type(nameof(Reference)) ?? string.Empty,
            state.Type(nameof(SupplierOrganisationId)),
            state.TypeJson<CurrencyCode>(nameof(Currency)),
            ReadLines(state),
            ReadStatus(state),
            state.TypeJson<DateOnly?>(nameof(IssuedDate)),
            state.TypeJson<DateOnly?>(nameof(ExpectedDelivery)),
            state.Type(nameof(Notes)),
            bool.TryParse(state.Type(nameof(ExpensesRecorded)), out var recorded) && recorded);
}
