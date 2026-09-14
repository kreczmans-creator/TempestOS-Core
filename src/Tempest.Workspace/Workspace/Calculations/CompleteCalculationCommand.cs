using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Calculations;

/// <summary>
/// Marks the selected <see cref="Calculation"/> complete (`TD-181`,
/// Product Owner decision 2026-09-15 §2) — the calculation-task signal a
/// project's own Tasks read model leaves the Calculations bucket on,
/// alongside issued evidence citing it, whichever comes first. Mirrors
/// <c>Tempest.Workspace.Tasks.CompleteTaskCommand</c>'s own shape exactly:
/// no independence rule (the same person may complete it, per the Product
/// Owner's own words), one audit row.
/// </summary>
public sealed class CompleteCalculationCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="CompleteCalculationCommand"/> class.</summary>
    public CompleteCalculationCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="CompleteCalculationCommand"/> directly against <see cref="EngineeringDomainContext"/> — mirrors <see cref="SetCalculationStatusCommandHandler"/>'s own shape; Calculations registers no separate service layer of its own.</summary>
public sealed class CompleteCalculationCommandHandler : ICommandHandler<CompleteCalculationCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="CompleteCalculationCommandHandler"/> class.</summary>
    /// <param name="timeProvider">The clock "today" is read from. <see langword="null"/> — the default — is <see cref="TimeProvider.System"/>; a test supplies a controllable one.</param>
    public CompleteCalculationCommandHandler(EngineeringDomainContext context, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(CompleteCalculationCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not Calculation calculation || target is IDeletable { IsDeleted: true })
            return CommandResult.Failure($"'{command.TargetObjectId}' is not a known Calculation.");

        if (calculation.Completed)
            return CommandResult.Failure($"'{calculation.DisplayName}' is already complete ({calculation.CompletedOn:O}).");

        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        await calculation.MarkCompletedAsync(today, cancellationToken).ConfigureAwait(false);

        return CommandResult.Success($"'{calculation.DisplayName}' marked complete.", command.TargetObjectId, command.TargetKind);
    }
}
