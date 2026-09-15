using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Expenses;

/// <summary>
/// A closed vocabulary of what a <see cref="ProjectExpense"/> was for
/// (`WP 21.3B`) — the Product Owner's own "not ERP, not PLM" guard: no
/// supplier, cost-centre or general-ledger account, only the categories a
/// consultancy's own invoice request actually needs to describe an
/// expense by.
/// </summary>
public enum ExpenseCategory
{
    /// <summary>Travel — fares, mileage, parking.</summary>
    Travel,

    /// <summary>Subsistence — meals and accommodation while working away.</summary>
    Subsistence,

    /// <summary>Materials bought for the project.</summary>
    Materials,

    /// <summary>Work subcontracted out.</summary>
    Subcontract,

    /// <summary>Anything the four categories above do not name.</summary>
    Other,
}

/// <summary>
/// A canonical Kind recording one out-of-pocket cost against a project —
/// travel, subsistence, materials, a subcontract invoice, or anything else
/// a receipt names (`WP 21.3B`). Follows
/// <c>Tempest.Core.Timesheets.TimesheetEntry</c>'s own shape exactly: an
/// <c>EngineeringObjectBase</c> subtype carrying its own state through
/// <c>CaptureTypeState</c>/<c>ApplyTypeState</c>/<see cref="IRehydratable{ProjectExpense}.Rehydrate"/>,
/// parented to the project it was incurred on.
/// </summary>
/// <remarks>
/// <para>
/// <b>TempestOS holds no ledger (Product Owner guard, "Not ERP, not
/// PLM").</b> <see cref="NetAmount"/> and <see cref="VatAmount"/> are
/// entered by the consultant, from the receipt, exactly as they are — this
/// class computes no tax liability, posts nothing to Xero, and reconciles
/// nothing; <see cref="GrossAmount"/> is the one figure it derives, by
/// addition alone.
/// </para>
/// <para>
/// <b>The receipt is an ordinary attachment.</b> <c>EngineeringObjectBase</c>
/// already implements <c>IHasAttachments</c> for every canonical Kind, so a
/// receipt is <see cref="Tempest.Core.EngineeringDomain.Implementation.EngineeringObjectBase.AttachFileAsync"/>,
/// not a bespoke field this class invents.
/// </para>
/// <para>
/// <b><see cref="InvoicedBy"/> is set once and never cleared</b> — exactly
/// <see cref="TimesheetEntry.InvoicedBy"/>'s own once-only shape, so a
/// billable expense can be billed on at most one live invoice request.
/// </para>
/// </remarks>
public sealed class ProjectExpense : EngineeringObjectBase, IRehydratable<ProjectExpense>
{
    /// <summary>The <see cref="IEngineeringObject.Kind"/> every project expense's own backing document carries (`ADR-0105`).</summary>
    public const string CanonicalKind = "ProjectExpense";

    private readonly Guid _projectId;
    private readonly DateOnly _date;
    private string _description;
    private ExpenseCategory _category;
    private Money _netAmount;
    private Money _vatAmount;
    private bool _billable;
    private Guid? _invoicedBy;

    /// <summary>Initialises a new instance of the <see cref="ProjectExpense"/> class.</summary>
    public ProjectExpense(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        Guid projectId, DateOnly date, string description, ExpenseCategory category, Money netAmount, Money vatAmount,
        bool billable, Guid? invoicedBy = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        _projectId = projectId;
        _date = date;
        _description = description;
        _category = category;
        _netAmount = netAmount;
        _vatAmount = vatAmount;
        _billable = billable;
        _invoicedBy = invoicedBy;
    }

    /// <summary>The project this expense was incurred on. Always this expense's own <see cref="IHasParent.ParentId"/> too.</summary>
    public Guid ProjectId => _projectId;

    /// <summary>The day the expense was incurred.</summary>
    public DateOnly Date => _date;

    /// <summary>What the expense was.</summary>
    public string Description => _description;

    /// <summary>What the expense was for.</summary>
    /// <remarks>Hides <c>EngineeringObjectBase.Category</c> (a generic, free-text field) with this Kind's own closed vocabulary, exactly as <c>Quotation.Status</c> hides the canonical <c>LifecycleState</c> with its own status vocabulary.</remarks>
    public new ExpenseCategory Category => _category;

    /// <summary>The amount before VAT, as the receipt states it.</summary>
    public Money NetAmount => _netAmount;

    /// <summary>The VAT amount, as the receipt states it.</summary>
    public Money VatAmount => _vatAmount;

    /// <summary><see cref="NetAmount"/> plus <see cref="VatAmount"/>.</summary>
    public Money GrossAmount => _netAmount + _vatAmount;

    /// <summary>Whether this expense is billable to the client.</summary>
    public bool Billable => _billable;

    /// <summary>The invoice request this expense was billed on, set once and never cleared. <see langword="null"/> until then.</summary>
    public Guid? InvoicedBy => _invoicedBy;

    /// <summary>Amends this expense's own description, category, amounts and billable flag — the only fields <see cref="ExpenseService.AmendAsync"/> may change, and only while <see cref="InvoicedBy"/> is unset.</summary>
    internal Task AmendAsync(
        string description, ExpenseCategory category, Money netAmount, Money vatAmount, bool billable, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [nameof(Description)] = description,
                    [nameof(Category)] = category.ToString(),
                    [nameof(Billable)] = billable.ToString(),
                };
                WriteJson(state, nameof(NetAmount), netAmount);
                WriteJson(state, nameof(VatAmount), vatAmount);
                return state;
            },
            () =>
            {
                _description = description;
                _category = category;
                _netAmount = netAmount;
                _vatAmount = vatAmount;
                _billable = billable;
            },
            $"Amended: '{description}', {category}, net {netAmount}, VAT {vatAmount}, billable {billable}.",
            cancellationToken);
    }

    /// <summary>Sets <see cref="InvoicedBy"/> to <paramref name="requestId"/>, once. <see cref="ExpenseService.MarkInvoicedAsync"/> refuses a second set before this ever runs.</summary>
    internal Task MarkInvoicedAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(InvoicedBy)] = requestId.ToString() },
            () => _invoicedBy = requestId,
            $"Invoiced by request '{requestId:N}'.",
            cancellationToken);

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(ProjectId)] = _projectId.ToString();
        WriteJson(state, nameof(Date), _date);
        state[nameof(Description)] = _description;
        state[nameof(Category)] = _category.ToString();
        WriteJson(state, nameof(NetAmount), _netAmount);
        WriteJson(state, nameof(VatAmount), _vatAmount);
        state[nameof(Billable)] = _billable.ToString();
        state[nameof(InvoicedBy)] = _invoicedBy?.ToString();
    }

    /// <inheritdoc />
    protected override void ApplyTypeState(EngineeringObjectState state)
    {
        _description = state.Type(nameof(Description)) ?? _description;
        _category = ParseCategory(state);
        _netAmount = state.TypeJson<Money>(nameof(NetAmount));
        _vatAmount = state.TypeJson<Money>(nameof(VatAmount));
        _billable = ParseBillable(state);
        _invoicedBy = state.TypeGuid(nameof(InvoicedBy));
    }

    private static ExpenseCategory ParseCategory(EngineeringObjectState state) =>
        Enum.TryParse<ExpenseCategory>(state.Type(nameof(Category)), out var value) ? value : ExpenseCategory.Other;

    private static bool ParseBillable(EngineeringObjectState state) =>
        bool.TryParse(state.Type(nameof(Billable)), out var value) && value;

    static ProjectExpense IRehydratable<ProjectExpense>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.TypeGuidOrEmpty(nameof(ProjectId)),
            state.TypeJson<DateOnly>(nameof(Date)),
            state.Type(nameof(Description)) ?? string.Empty,
            ParseCategory(state),
            state.TypeJson<Money>(nameof(NetAmount)),
            state.TypeJson<Money>(nameof(VatAmount)),
            ParseBillable(state),
            state.TypeGuid(nameof(InvoicedBy)));
}
