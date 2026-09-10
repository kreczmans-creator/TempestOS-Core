using System.Globalization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Timesheets;

/// <summary>
/// A canonical Kind recording one principal's time against a project, at a
/// rate frozen the moment it was recorded (`WP 19.0A`, `ADR-0150`).
/// Follows <c>Tempest.Core.Evidence.Evidence</c>'s own shape exactly: an
/// <c>EngineeringObjectBase</c> subtype carrying its own state through
/// <c>CaptureTypeState</c>/<c>ApplyTypeState</c>/<see cref="IRehydratable{TimesheetEntry}.Rehydrate"/>,
/// inheriting identity, revisions, audit and lifecycle from the base.
/// </summary>
/// <remarks>
/// <para>
/// <b>Billing and cost rate are resolved once, at record time, and frozen
/// thereafter.</b> <see cref="TimesheetService.RecordAsync"/> resolves
/// both from the project's own pinned rate card and the grade given; a
/// later revision of that card — even a later card pinned to the same
/// project — never reaches an entry already recorded. This is what makes
/// margin (`WP 19.1B`) a fact about the day the work was done, not about
/// whatever the card happens to say when the report runs.
/// </para>
/// <para>
/// <b><see cref="InvoicedBy"/> is set once and never cleared</b> —
/// <see cref="TimesheetEntry.MarkInvoicedAsync"/> refuses a second set,
/// exactly as <c>Evidence.RecordIssueAsync</c>'s own once-only shape. Not
/// ERP, not PLM (`D-028`): this class never raises an invoice, computes a
/// total, or talks to an accounting system — <c>WP 19.1A</c> does that,
/// reading this link, never writing it a second way.
/// </para>
/// </remarks>
public sealed class TimesheetEntry : EngineeringObjectBase, IRehydratable<TimesheetEntry>
{
    /// <summary>The <see cref="IEngineeringObject.Kind"/> every timesheet entry's own backing document carries (`ADR-0105`).</summary>
    public const string CanonicalKind = "TimesheetEntry";

    private readonly string _principalIdentityId;
    private readonly Guid _projectId;
    private string _taskDescription;
    private readonly DateOnly _date;
    private decimal _hours;
    private bool _billable;
    private readonly string _grade;
    private readonly Money _billingRate;
    private readonly Money? _costRate;
    private Guid? _invoicedBy;

    /// <summary>Initialises a new instance of the <see cref="TimesheetEntry"/> class.</summary>
    public TimesheetEntry(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        string principalIdentityId, Guid projectId, string taskDescription, DateOnly date, decimal hours, bool billable,
        string grade, Money billingRate, Money? costRate = null, Guid? invoicedBy = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalIdentityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(grade);

        _principalIdentityId = principalIdentityId;
        _projectId = projectId;
        _taskDescription = taskDescription;
        _date = date;
        _hours = ValidateHours(hours);
        _billable = billable;
        _grade = grade;
        _billingRate = billingRate;
        _costRate = costRate;
        _invoicedBy = invoicedBy;
    }

    /// <summary>The principal whose time this is.</summary>
    public string PrincipalIdentityId => _principalIdentityId;

    /// <summary>The project this time was worked on. Always this entry's own <see cref="IHasParent.ParentId"/> too.</summary>
    public Guid ProjectId => _projectId;

    /// <summary>What the work was.</summary>
    public string TaskDescription => _taskDescription;

    /// <summary>The day the work was done.</summary>
    public DateOnly Date => _date;

    /// <summary>How many hours, 0 exclusive to 24 inclusive, to the nearest quarter hour.</summary>
    public decimal Hours => _hours;

    /// <summary>Whether this time is billable to the client.</summary>
    public bool Billable => _billable;

    /// <summary>The grade this time was recorded at, resolved against the project's pinned rate card.</summary>
    public string Grade => _grade;

    /// <summary>The billing rate this entry froze at record time.</summary>
    public Money BillingRate => _billingRate;

    /// <summary>The cost rate this entry froze at record time. <see langword="null"/> when the pinned card held none.</summary>
    public Money? CostRate => _costRate;

    /// <summary>The invoice request this entry was billed on, set once and never cleared. <see langword="null"/> until then.</summary>
    public Guid? InvoicedBy => _invoicedBy;

    /// <summary>Amends this entry's own hours, task and billable flag — the only fields <see cref="TimesheetService.AmendAsync"/> may change, and only while <see cref="InvoicedBy"/> is unset.</summary>
    internal Task AmendAsync(decimal hours, string taskDescription, bool billable, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskDescription);
        var validatedHours = ValidateHours(hours);

        return MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [nameof(Hours)] = validatedHours.ToString(CultureInfo.InvariantCulture),
                [nameof(TaskDescription)] = taskDescription,
                [nameof(Billable)] = billable.ToString(),
            },
            () =>
            {
                _hours = validatedHours;
                _taskDescription = taskDescription;
                _billable = billable;
            },
            $"Amended: {validatedHours}h, billable {billable}, '{taskDescription}'.",
            cancellationToken);
    }

    /// <summary>Sets <see cref="InvoicedBy"/> to <paramref name="requestId"/>, once. <see cref="TimesheetService.MarkInvoicedAsync"/> refuses a second set before this ever runs.</summary>
    internal Task MarkInvoicedAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(InvoicedBy)] = requestId.ToString() },
            () => _invoicedBy = requestId,
            $"Invoiced by request '{requestId:N}'.",
            cancellationToken);

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(PrincipalIdentityId)] = _principalIdentityId;
        state[nameof(ProjectId)] = _projectId.ToString();
        state[nameof(TaskDescription)] = _taskDescription;
        WriteJson(state, nameof(Date), _date);
        state[nameof(Hours)] = _hours.ToString(CultureInfo.InvariantCulture);
        state[nameof(Billable)] = _billable.ToString();
        state[nameof(Grade)] = _grade;
        WriteJson(state, nameof(BillingRate), _billingRate);
        WriteJson(state, nameof(CostRate), _costRate);
        state[nameof(InvoicedBy)] = _invoicedBy?.ToString();
    }

    /// <inheritdoc />
    protected override void ApplyTypeState(EngineeringObjectState state)
    {
        _taskDescription = state.Type(nameof(TaskDescription)) ?? _taskDescription;
        _hours = ParseHours(state);
        _billable = ParseBillable(state);
        _invoicedBy = state.TypeGuid(nameof(InvoicedBy));
    }

    private static decimal ValidateHours(decimal hours)
    {
        if (hours <= 0m || hours > 24m)
            throw new ArgumentOutOfRangeException(nameof(hours), hours, "Hours must be more than zero and at most 24 in a single day.");

        if (hours % 0.25m != 0m)
            throw new ArgumentOutOfRangeException(nameof(hours), hours, "Hours must be recorded to the nearest quarter hour.");

        return hours;
    }

    private static decimal ParseHours(EngineeringObjectState state) =>
        decimal.TryParse(state.Type(nameof(Hours)), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    private static bool ParseBillable(EngineeringObjectState state) =>
        bool.TryParse(state.Type(nameof(Billable)), out var value) && value;

    static TimesheetEntry IRehydratable<TimesheetEntry>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.Type(nameof(PrincipalIdentityId)) ?? string.Empty,
            state.TypeGuidOrEmpty(nameof(ProjectId)),
            state.Type(nameof(TaskDescription)) ?? string.Empty,
            state.TypeJson<DateOnly>(nameof(Date)),
            ParseHours(state),
            ParseBillable(state),
            state.Type(nameof(Grade)) ?? string.Empty,
            state.TypeJson<Money>(nameof(BillingRate)),
            state.TypeJson<Money?>(nameof(CostRate)),
            state.TypeGuid(nameof(InvoicedBy)));
}
