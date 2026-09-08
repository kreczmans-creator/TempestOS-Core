using Tempest.Core.Commands;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Executes a registered Calculation Template against a target Domain
/// object — the Calculation Management scope's own "Execute" capability.
/// One non-generic command serves every registered Template
/// (<see cref="CalculationTemplateRegistry"/>'s own type-erasure adapter);
/// <see cref="InputJson"/> is deserialized to that Template's own real
/// <c>TInput</c> type only inside the registry, never here.
/// </summary>
public sealed class ExecuteCalculationCommand : IWorkspaceCommand
{
    public ExecuteCalculationCommand(Guid targetObjectId, string targetKind, string calculationTemplateId, string inputJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(calculationTemplateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputJson);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        CalculationTemplateId = calculationTemplateId;
        InputJson = inputJson;
    }

    /// <inheritdoc />
    /// <remarks>The Domain object the resulting <c>CalculationRecord</c> is linked to via <see cref="CalculationTemplateRegistry.CalculatedByRelationshipKind"/> — typically a <c>"Calculation"</c>, but any <see cref="Tempest.Core.EngineeringDomain.IHasRelationships"/>-composing object is accepted.</remarks>
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the Template's own <see cref="Tempest.Core.Calculations.ICalculationDefinition{TInput, TResult}.CalculationId"/> to execute.</summary>
    public string CalculationTemplateId { get; }

    /// <summary>Gets the Template's own input, JSON-serialized.</summary>
    public string InputJson { get; }
}

/// <summary>Handles <see cref="ExecuteCalculationCommand"/>.</summary>
public sealed class ExecuteCalculationCommandHandler : ICommandHandler<ExecuteCalculationCommand>
{
    private readonly CalculationTemplateRegistry _templateRegistry;

    public ExecuteCalculationCommandHandler(CalculationTemplateRegistry templateRegistry)
    {
        ArgumentNullException.ThrowIfNull(templateRegistry);

        _templateRegistry = templateRegistry;
    }

    public async Task<CommandResult> HandleAsync(ExecuteCalculationCommand command, CancellationToken cancellationToken)
    {
        CalculationExecutionSummary summary;

        try
        {
            summary = await _templateRegistry.ExecuteAsync(
                command.CalculationTemplateId, command.TargetObjectId, command.InputJson, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Tempest.Core.Calculations.CalculationException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success(
            $"Executed '{command.CalculationTemplateId}', produced record '{summary.RecordId}' ({summary.Outcome}): {summary.ResultJson}.");
    }
}
