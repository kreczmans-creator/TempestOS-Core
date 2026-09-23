using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;

namespace Tempest.Workspace.Kpi;

/// <summary>
/// One live <c>TimesheetEntry</c>'s own KPI-relevant fields, read directly
/// off its durable <c>TypeState</c> by <see cref="WorkspaceSnapshotReader"/>
/// (`WP 19.1B`) — never the rehydrated domain object itself, so
/// <see cref="KpiEquations"/> stays a pure function of plain data and is
/// testable over hand-authored fixtures with no persistence store at all.
/// </summary>
public sealed record TimesheetEntryFacts(
    string PrincipalIdentityId,
    Guid ProjectId,
    DateOnly Date,
    decimal Hours,
    bool Billable,
    Money BillingRate,
    Money? CostRate,
    Guid? InvoicedBy);

/// <summary>One live <c>DeliverableCompletion</c>'s own KPI-relevant fields.</summary>
/// <param name="ProjectId">Its own structural parent — the project it was completed against.</param>
public sealed record DeliverableCompletionFacts(
    Guid ProjectId,
    DateOnly CompletedOn,
    Money? FixedPriceValue,
    Guid? InvoicedBy);

/// <summary>One live <c>InvoiceRequest</c>'s own KPI-relevant fields — the three days-sales-outstanding needs.</summary>
public sealed record InvoiceRequestFacts(
    InvoiceRequestStatus Status,
    DateOnly? IssuedDate,
    DateOnly? PaidDate);

/// <summary>One live Evidence record's own issue date — present only when it has ever reached <c>EvidenceStatus.Issued</c> (its <c>Issue</c> record is set), regardless of whether a later revision moved it on.</summary>
public sealed record EvidenceIssueFacts(DateOnly IssueDate);
