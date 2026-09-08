using Tempest.Core.Commands;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Re-executes a Calculation Template that has already been executed at
/// least once against <see cref="IWorkspaceCommand.TargetObjectId"/> — the
/// Calculation Management scope's own "Recalculate" capability, kept
/// discoverable in the Command Palette as a distinct, descriptive action
/// from "Execute" (a first run vs. a re-run), while composing
/// <see cref="ExecuteCalculationCommandHandler"/>'s own identical
/// mechanism underneath, mirroring
/// <see cref="Mechanical.DuplicateMechanicalObjectCommand"/>'s own
/// composition-over-a-second-implementation precedent exactly.
/// </summary>
/// <remarks>
/// A genuine, disclosed limitation: <see cref="Tempest.Core.Calculations.CalculationRecord{TResult}"/>
/// does not itself retain the input that produced it (only
/// <c>Result</c>/<c>Assumptions</c>/<c>IntermediateResults</c>/<c>Validation</c>/
/// <c>ReferencedMaterialIds</c>) — Recalculate therefore still requires a
/// fresh <see cref="InputJson"/> from the caller, exactly like Execute; it
/// is not a parameterless "run it again with the same numbers" gesture.
/// See this Work Package's own Technical Debt Assessment.
/// </remarks>
public sealed class RecalculateCalculationCommand : IWorkspaceCommand
{
    public RecalculateCalculationCommand(Guid targetObjectId, string targetKind, string calculationTemplateId, string inputJson)
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
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the Template's own <see cref="Tempest.Core.Calculations.ICalculationDefinition{TInput, TResult}.CalculationId"/> to re-execute.</summary>
    public string CalculationTemplateId { get; }

    /// <summary>Gets the Template's own (possibly updated) input, JSON-serialized.</summary>
    public string InputJson { get; }
}

/// <summary>Handles <see cref="RecalculateCalculationCommand"/> by delegating to <see cref="ExecuteCalculationCommandHandler"/>.</summary>
public sealed class RecalculateCalculationCommandHandler : ICommandHandler<RecalculateCalculationCommand>
{
    private readonly ExecuteCalculationCommandHandler _executeHandler;

    public RecalculateCalculationCommandHandler(ExecuteCalculationCommandHandler executeHandler)
    {
        ArgumentNullException.ThrowIfNull(executeHandler);

        _executeHandler = executeHandler;
    }

    public Task<CommandResult> HandleAsync(RecalculateCalculationCommand command, CancellationToken cancellationToken)
    {
        var executeCommand = new ExecuteCalculationCommand(command.TargetObjectId, command.TargetKind, command.CalculationTemplateId, command.InputJson);

        return _executeHandler.HandleAsync(executeCommand, cancellationToken);
    }
}
