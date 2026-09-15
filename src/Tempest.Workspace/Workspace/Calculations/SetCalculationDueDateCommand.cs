using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Calculations;

/// <summary>
/// Sets, or clears, the selected <see cref="Calculation"/>'s own
/// <see cref="Calculation.DueOn"/> (`WP 20.10B`, T2) — the generic
/// editor's own row pattern, mirroring
/// <see cref="Mechanical.SetBomLineCommand"/>'s identical shape: a plain
/// field write, dispatched directly from the object editor's own Save
/// button, no independence rule and no confirmation.
/// </summary>
public sealed class SetCalculationDueDateCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="SetCalculationDueDateCommand"/> class.</summary>
    public SetCalculationDueDateCommand(Guid targetObjectId, string targetKind, DateOnly? dueOn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        DueOn = dueOn;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the new due date, or <see langword="null"/> to clear it.</summary>
    public DateOnly? DueOn { get; }
}

/// <summary>Handles <see cref="SetCalculationDueDateCommand"/> directly against <see cref="EngineeringDomainContext"/> — Calculations registers no separate service layer of its own, mirroring <see cref="CompleteCalculationCommandHandler"/>'s own identical shape.</summary>
public sealed class SetCalculationDueDateCommandHandler : ICommandHandler<SetCalculationDueDateCommand>
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="SetCalculationDueDateCommandHandler"/> class.</summary>
    public SetCalculationDueDateCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(SetCalculationDueDateCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not Calculation calculation || target is IDeletable { IsDeleted: true })
            return CommandResult.Failure($"'{command.TargetObjectId}' is not a known Calculation.");

        await calculation.SetDueOnAsync(command.DueOn, cancellationToken).ConfigureAwait(false);

        var message = command.DueOn is { } due
            ? $"Due date for '{calculation.DisplayName}' set to {due:O}."
            : $"Due date cleared for '{calculation.DisplayName}'.";

        return CommandResult.Success(message, command.TargetObjectId, command.TargetKind);
    }
}
