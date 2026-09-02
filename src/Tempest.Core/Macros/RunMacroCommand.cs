using Tempest.Core.Commands;

namespace Tempest.Core.Macros;

/// <summary>Runs one <see cref="ICommandMacro"/> — the one command type every macro's own <see cref="CommandDescriptor"/> dispatches (`ADR-0098`), whichever macro it is; <see cref="MacroId"/> carries which.</summary>
public sealed class RunMacroCommand : ICommand
{
    /// <summary>Initialises a new instance of the <see cref="RunMacroCommand"/> class.</summary>
    /// <param name="macroId">The macro to run.</param>
    /// <param name="context">
    /// The context captured when the macro was started, replayed for every
    /// step. <see langword="null"/> — the default, and what the
    /// parameterless <see cref="CommandDescriptor.CreateDefault"/> factory
    /// still produces — means no selection was captured, and every step
    /// needing one reports that rather than acting on nothing.
    /// </param>
    public RunMacroCommand(Guid macroId, CommandContext? context = null)
    {
        MacroId = macroId;
        Context = context;
    }

    /// <summary>Gets the Id of the <see cref="ICommandMacro"/> to run.</summary>
    public Guid MacroId { get; }

    /// <summary>
    /// Gets the context captured at macro start, or <see langword="null"/>
    /// if none was.
    /// </summary>
    /// <remarks>
    /// Captured once, at the start, and replayed unchanged for every step —
    /// never re-read per step. A macro is an ordered list of Ids
    /// (<c>ADR-0098</c>): if step one changed the selection, step two would
    /// otherwise act on something the person never chose.
    /// </remarks>
    public CommandContext? Context { get; }
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

        // TD-77 Stage 5: steps run through the context-aware path, so a
        // bound discipline command reaches its real handler. The context is
        // whatever was captured at macro start, replayed for every step.
        //
        // No prompt is passed, deliberately and permanently. A macro is
        // unattended by definition (ADR-0098: an ordered list of Ids, no
        // branching, no looping, no parameters), so a step that declares
        // values or a confirmation is reported as unavailable and stops the
        // run — it is never allowed to interrupt with a dialog. That is
        // what a legacy macro holding a parameterised step now does.
        var context = command.Context ?? CommandContext.Empty;
        var stepsRun = 0;

        foreach (var stepId in macro.StepCommandIds)
        {
            var invocation = await _commandRegistry
                .InvokeAsync(stepId, context, prompt: null, cancellationToken)
                .ConfigureAwait(false);
            stepsRun++;

            var failure = invocation.Outcome switch
            {
                CommandOutcome.Executed when invocation.Result!.Succeeded => null,
                CommandOutcome.Executed => invocation.Result!.Message ?? "failed",
                _ => invocation.Reason ?? "could not be run",
            };

            if (failure is not null)
            {
                return CommandResult.Failure(
                    $"Macro '{macro.Name}' stopped at step {stepsRun}/{macro.StepCommandIds.Count} ('{stepId}'): {failure}.");
            }
        }

        return CommandResult.Success($"Macro '{macro.Name}' completed all {stepsRun} step(s).");
    }
}
