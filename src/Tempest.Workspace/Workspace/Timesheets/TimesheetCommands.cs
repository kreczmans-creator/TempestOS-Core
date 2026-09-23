using Tempest.Core.Commands;
using Tempest.Core.Timesheets;

namespace Tempest.Workspace.Timesheets;

/// <summary>
/// Records a new <see cref="TimesheetEntry"/>. Plain <see cref="ICommand"/>,
/// not <see cref="IWorkspaceCommand"/> — there is no pre-existing target,
/// only a new object to be revealed and opened right up once created,
/// mirroring <c>Evidence.CreateEvidenceCommand</c>'s own identical
/// reasoning (`WP 19.0A`, `ADR-0150`).
/// </summary>
public sealed class RecordTimesheetCommand : ICommand
{
    /// <summary>Initialises a new instance of the <see cref="RecordTimesheetCommand"/> class.</summary>
    public RecordTimesheetCommand(Guid? projectId, DateOnly date, decimal hours, bool billable, string grade, string task)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grade);
        ArgumentException.ThrowIfNullOrWhiteSpace(task);

        ProjectId = projectId;
        Date = date;
        Hours = hours;
        Billable = billable;
        Grade = grade;
        Task = task;
    }

    /// <summary>Gets where the new entry goes — the open project, or a chosen container (`CreationPlacement`). <see langword="null"/> when neither resolves.</summary>
    public Guid? ProjectId { get; }

    /// <summary>Gets the day the work was done.</summary>
    public DateOnly Date { get; }

    /// <summary>Gets how many hours.</summary>
    public decimal Hours { get; }

    /// <summary>Gets whether the time is billable.</summary>
    public bool Billable { get; }

    /// <summary>Gets the grade this time is recorded at.</summary>
    public string Grade { get; }

    /// <summary>Gets what the work was.</summary>
    public string Task { get; }
}

/// <summary>Handles <see cref="RecordTimesheetCommand"/>.</summary>
public sealed class RecordTimesheetCommandHandler : ICommandHandler<RecordTimesheetCommand>
{
    private readonly ITimesheetService _service;

    /// <summary>Initialises a new instance of the <see cref="RecordTimesheetCommandHandler"/> class.</summary>
    public RecordTimesheetCommandHandler(ITimesheetService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(RecordTimesheetCommand command, CancellationToken cancellationToken)
    {
        if (command.ProjectId is not { } projectId)
            return CommandResult.Failure("Recording time needs an open project.");

        TimesheetResult result;

        try
        {
            result = await _service
                .RecordAsync(projectId, command.Date, command.Hours, command.Billable, command.Grade, command.Task, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return result.Succeeded
            ? CommandResult.Success($"Recorded {result.Entry!.Hours}h — '{result.Entry.TaskDescription}'.", result.Entry.Id, TimesheetEntry.CanonicalKind)
            : CommandResult.Failure(result.Reason ?? "The entry was refused.");
    }
}

/// <summary>Amends a timesheet entry's own hours, task and billable flag (`WP 19.0A`, `ADR-0150`). Refused, as a result, once the entry is invoiced.</summary>
public sealed class AmendTimesheetCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="AmendTimesheetCommand"/> class.</summary>
    public AmendTimesheetCommand(Guid targetObjectId, string targetKind, decimal hours, string task, bool billable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(task);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Hours = hours;
        Task = task;
        Billable = billable;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the amended hours.</summary>
    public decimal Hours { get; }

    /// <summary>Gets the amended task.</summary>
    public string Task { get; }

    /// <summary>Gets the amended billable flag.</summary>
    public bool Billable { get; }
}

/// <summary>Handles <see cref="AmendTimesheetCommand"/>.</summary>
public sealed class AmendTimesheetCommandHandler : ICommandHandler<AmendTimesheetCommand>
{
    private readonly ITimesheetService _service;

    /// <summary>Initialises a new instance of the <see cref="AmendTimesheetCommandHandler"/> class.</summary>
    public AmendTimesheetCommandHandler(ITimesheetService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(AmendTimesheetCommand command, CancellationToken cancellationToken)
    {
        TimesheetResult result;

        try
        {
            result = await _service.AmendAsync(command.TargetObjectId, command.Hours, command.Task, command.Billable, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return result.Succeeded
            ? CommandResult.Success("Entry amended.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The entry could not be amended.");
    }
}

/// <summary>Soft-deletes a timesheet entry (`WP 19.0A`, `ADR-0150`). Refused, as a result, once the entry is invoiced.</summary>
public sealed class DeleteTimesheetCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="DeleteTimesheetCommand"/> class.</summary>
    public DeleteTimesheetCommand(Guid targetObjectId, string targetKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }
}

/// <summary>Handles <see cref="DeleteTimesheetCommand"/>.</summary>
public sealed class DeleteTimesheetCommandHandler : ICommandHandler<DeleteTimesheetCommand>
{
    private readonly ITimesheetService _service;

    /// <summary>Initialises a new instance of the <see cref="DeleteTimesheetCommandHandler"/> class.</summary>
    public DeleteTimesheetCommandHandler(ITimesheetService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(DeleteTimesheetCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success("Entry deleted.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The entry could not be deleted.");
    }
}
