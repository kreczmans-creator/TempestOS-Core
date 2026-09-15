using Tempest.Core.Calculations;
using Tempest.Core.Commands;

namespace Tempest.Workspace.Calculations;

/// <summary>
/// Re-runs the target Calculation Domain object's own most recent record
/// with its exact original input (`TD-29`), producing a new record linked
/// to the old one as its own predecessor — the Calculation Management
/// scope's own "Re-run" capability, a genuine parameterless replay, unlike
/// <see cref="RecalculateCalculationCommand"/>'s own "fresh input required
/// every time" (that command's disclosed limitation, now closed for this
/// one by <see cref="Tempest.Core.Calculations.CalculationRecord{TResult}.Input"/>).
/// </summary>
public sealed class RerunCalculationCommand : IWorkspaceCommand
{
    public RerunCalculationCommand(Guid targetObjectId, string targetKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
    }

    /// <inheritdoc />
    /// <remarks>The Domain object whose own most recent <c>CalculationRecord</c> is re-run and linked via <see cref="CalculationTemplateRegistry.CalculatedByRelationshipKind"/> — typically a <c>"Calculation"</c>.</remarks>
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }
}

/// <summary>Handles <see cref="RerunCalculationCommand"/>.</summary>
public sealed class RerunCalculationCommandHandler : ICommandHandler<RerunCalculationCommand>
{
    private readonly CalculationTemplateRegistry _templateRegistry;

    public RerunCalculationCommandHandler(CalculationTemplateRegistry templateRegistry)
    {
        ArgumentNullException.ThrowIfNull(templateRegistry);

        _templateRegistry = templateRegistry;
    }

    public async Task<CommandResult> HandleAsync(RerunCalculationCommand command, CancellationToken cancellationToken)
    {
        CalculationExecutionSummary summary;

        try
        {
            summary = await _templateRegistry.RerunAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);
        }
        catch (CalculationException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success(
            $"Re-ran '{summary.CalculationId}', produced record '{summary.RecordId}' ({summary.Outcome}): {summary.ResultJson}.");
    }
}
