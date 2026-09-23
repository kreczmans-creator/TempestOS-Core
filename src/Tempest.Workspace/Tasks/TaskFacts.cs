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

/// <summary>One live piece of Evidence's own task-relevant fields — whether it is genuinely ready to check (Draft, with a subject and a file), awaiting issue (Checked), or (Issued, with a subject) closing whatever it cites.</summary>
public sealed record EvidenceReviewFact(Guid EvidenceId, string Title, Guid? ProjectId, EvidenceStatus Status, bool HasSubjectAndFile, Guid? SubjectId);

/// <summary>One live <c>InvoiceRequest</c>'s own task-relevant fields — the "to chase" figures.</summary>
/// <param name="DueOn">This request's own due date (`TD-180`) — <see langword="null"/> while still Draft.</param>
public sealed record InvoiceChaseFact(Guid RequestId, string Title, Guid? ProjectId, InvoiceRequestStatus Status, DateTimeOffset? SentAtUtc, DateOnly? PaidDate, DateOnly? DueOn);

/// <summary>One live <c>Calculation</c>'s own task-relevant fields (`TD-181`, and `DueOn` from `WP 20.10B`/T2).</summary>
/// <param name="ProjectId">The project this calculation ultimately sits under, resolved by walking its own parent chain. A calculation with no project ancestor at all is not a fact this record is ever built for — <see cref="TasksReadModelService"/>'s own remarks.</param>
/// <param name="Completed">Whether <c>calculations.complete</c> has been run against it.</param>
/// <param name="DueOn">This calculation's own due date — set at creation for every calculation created under a project; <see langword="null"/> for one created before `WP 20.10B`, or created standalone and later moved under a project.</param>
public sealed record CalculationChaseFact(Guid CalculationId, string Title, Guid ProjectId, bool Completed, DateOnly? DueOn);

/// <summary>One live <c>Quotation</c>'s own task-relevant fields — the "to chase" figures.</summary>
public sealed record QuotationChaseFact(Guid QuotationId, string Title, Guid? ProjectId, QuotationStatus Status, DateOnly? SentOn);

/// <summary>One live manual task's own fields.</summary>
public sealed record ManualTaskFact(Guid TaskId, string Title, Guid? ProjectId, DateOnly? DueDate, bool Done);
