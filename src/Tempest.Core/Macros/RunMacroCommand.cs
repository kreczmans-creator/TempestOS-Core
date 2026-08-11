using Tempest.Core.Commands;

namespace Tempest.Core.Macros;

/// <summary>Runs one <see cref="ICommandMacro"/> — the one command type every macro's own <see cref="CommandDescriptor"/> dispatches (`ADR-0098`), whichever macro it is; <see cref="MacroId"/> carries which.</summary>
public sealed class RunMacroCommand : ICommand
{
    /// <summary>Initialises a new instance of the <see cref="RunMacroCommand"/> class.</summary>
    public RunMacroCommand(Guid macroId)
    {
        MacroId = macroId;
    }

    /// <summary>Gets the Id of the <see cref="ICommandMacro"/> to run.</summary>
    public Guid MacroId { get; }
}

/// <summary>Handles <see cref="RunMacroCommand"/> — resolves the macro, then invokes each of its own <see cref="ICommandMacro.StepCommandIds"/> in sequence via <see cref="ICommandRegistry.InvokeAsync"/>, stopping at the first failure.</summary>
public sealed class RunMacroCommandHandler : ICommandHandler<RunMacroCommand>
{
    private readonly IMacroManager _macroManager;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>Initialises a new instance of the <see cref="RunMacroCommandHandler"/> class.</summary>
    public RunMacroCommandHandler(IMacroManager macroManager, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(macroManager);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _macroManager = macroManager;
        _commandRegistry = commandRegistry;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(RunMacroCommand command, CancellationToken cancellationToken)
    {
        var macro = await _macroManager.FindAsync(command.MacroId, cancellationToken).ConfigureAwait(false);
        if (macro is null)
        {
            // The honest "stale descriptor" outcome IMacroManager.DeleteAsync's
            // own remarks disclose — a deleted macro's own CommandDescriptor
            // stays registered (ICommandRegistry exposes no removal method),
            // so a caller can still reach here; this is the deliberate,
            // graceful failure that replaces it.
            return CommandResult.Failure($"Macro '{command.MacroId}' no longer exists.");
        }

        var stepsRun = 0;
        foreach (var stepId in macro.StepCommandIds)
        {
            var result = await _commandRegistry.InvokeAsync(stepId, cancellationToken).ConfigureAwait(false);
            stepsRun++;

            if (!result.Succeeded)
            {
                return CommandResult.Failure(
                    $"Macro '{macro.Name}' stopped at step {stepsRun}/{macro.StepCommandIds.Count} ('{stepId}'): {result.Message ?? "failed"}.");
            }
        }

        return CommandResult.Success($"Macro '{macro.Name}' completed all {stepsRun} step(s).");
    }
}
