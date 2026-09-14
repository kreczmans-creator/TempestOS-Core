using Tempest.Core.Evidence;
using Tempest.Core.Invoicing;
using Tempest.Core.Quotations;

namespace Tempest.Workspace.Tasks;

/// <summary>
/// One live <c>Deliverable</c>'s own task-relevant fields, read directly
/// off the durable object-state collection by
/// <see cref="TasksReadModelService"/> (`WP 19.5C`) — never the rehydrated
/// domain object itself, so <see cref="TaskEquations"/> stays a pure
/// function of plain data, mirroring <c>Tempest.Workspace.Kpi.KpiFacts</c>.
/// </summary>
public sealed record DeliverableFact(Guid DeliverableId, string Title, Guid MilestoneId);

/// <summary>One live <c>Milestone</c>'s own task-relevant fields.</summary>
public sealed record MilestoneFact(Guid MilestoneId, string Title, Guid ProjectId, DateOnly TargetDate);

/// <summary>One live piece of Evidence's own task-relevant fields — whether it is genuinely ready to check (Draft, with a subject and a file) or awaiting issue (Checked).</summary>
public sealed record EvidenceReviewFact(Guid EvidenceId, string Title, Guid? ProjectId, EvidenceStatus Status, bool HasSubjectAndFile);

/// <summary>One live <c>InvoiceRequest</c>'s own task-relevant fields — the "to chase" figures.</summary>
public sealed record InvoiceChaseFact(Guid RequestId, string Title, Guid? ProjectId, InvoiceRequestStatus Status, DateTimeOffset? SentAtUtc, DateOnly? PaidDate);

/// <summary>One live <c>Quotation</c>'s own task-relevant fields — the "to chase" figures.</summary>
public sealed record QuotationChaseFact(Guid QuotationId, string Title, Guid? ProjectId, QuotationStatus Status, DateOnly? SentOn);

/// <summary>One live manual task's own fields.</summary>
public sealed record ManualTaskFact(Guid TaskId, string Title, Guid? ProjectId, DateOnly? DueDate, bool Done);
