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

/// <summary>Handles <see cref="RunMacroCommand"/> — resolves the macro, then invokes each of its own <see cref="ICommandMacro.Steps"/> in sequence via <see cref="ICommandRegistry.InvokeAsync"/>, stopping at the first failure.</summary>
public sealed class RunMacroCommandHandler : ICommandHandler<RunMacroCommand>
{
    private readonly IMacroManager _macroManager;
    private readonly ICommandRegistry _commandRegistry;
    private readonly CommandParameterPrompt? _fallbackPrompt;

    /// <summary>Initialises a new instance of the <see cref="RunMacroCommandHandler"/> class.</summary>
    /// <param name="macroManager">Resolves the macro being run.</param>
    /// <param name="commandRegistry">Invokes each of the macro's own steps.</param>
    /// <param name="fallbackPrompt">
    /// Asked, for one step, only for whichever of its binding's own
    /// declared values that step's own <see cref="MacroStep.RecordedValues"/>
    /// does not carry (`WP 20.2C`) — never for a confirmation, which a
    /// recording never answers and this seam never asks for either
    /// (<c>ADR-0098</c>'s own "never runs unattended" for a destructive
    /// step, unchanged). <see langword="null"/> — the default, and every
    /// production composition today — means a step recording every value
    /// its binding needs still replays silently; one that does not is
    /// refused with the framework's own honest "no input surface was
    /// supplied", exactly as before this Work Package.
    /// </param>
    public RunMacroCommandHandler(
        IMacroManager macroManager, ICommandRegistry commandRegistry, CommandParameterPrompt? fallbackPrompt = null)
    {
        ArgumentNullException.ThrowIfNull(macroManager);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _macroManager = macroManager;
        _commandRegistry = commandRegistry;
        _fallbackPrompt = fallbackPrompt;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(RunMacroCommand command, CancellationToken cancellationToken)
    {
        var macro = await _macroManager.FindAsync(command.MacroId, cancellationToken).ConfigureAwait(false);
        if (macro is null)
        {
            // The honest "stale descriptor" outcome IMacroManager.DeleteAsync's
            // own remarks used to disclose is no longer reachable through
            // the registry's own Id path (`WP 20.2C`: DeleteAsync
            // unregisters the descriptor, so ICommandRegistry.InvokeAsync
            // now throws CommandNotFoundException for it instead) — this
            // stays as the graceful failure for the one path that can
            // still reach here: a RunMacroCommand dispatched directly,
            // bypassing the Id lookup, for a macro Id nothing ever created
            // or one already deleted.
            return CommandResult.Failure($"Macro '{command.MacroId}' no longer exists.");
        }

        // TD-77 Stage 5: steps run through the context-aware path, so a
        // bound discipline command reaches its real handler. The context is
        // whatever was captured at macro start, replayed for every step.
        var context = command.Context ?? CommandContext.Empty;
        var stepsRun = 0;
        var stepCompensations = new List<CommandCompensation>();
        string? undoUnavailableStep = null;

        foreach (var step in macro.Steps)
        {
            var descriptor = _commandRegistry.Items.FirstOrDefault(d => d.Id == step.CommandId);
            var invocation = await _commandRegistry
                .InvokeAsync(step.CommandId, context, BuildStepPrompt(descriptor, step.RecordedValues), cancellationToken)
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
                    $"Macro '{macro.Name}' stopped at step {stepsRun}/{macro.Steps.Count} ('{step.CommandId}'): {failure}.");
            }

            // `WP 21.1A`: one compound action for the whole run, not one
            // per step — a person undoes "the macro", never one of its
            // steps in isolation. Every step's own compensation is
            // collected as it runs; the first step that carries none marks
            // the whole run non-compensable, named by which step and why,
            // rather than silently offering a partial undo.
            if (undoUnavailableStep is null)
            {
                if (invocation.Result!.Compensation is { } stepCompensation)
                    stepCompensations.Add(stepCompensation);
                else
                    undoUnavailableStep = invocation.Result!.UndoUnavailableReason is { } reason
                        ? $"step {stepsRun} ('{step.CommandId}'): {reason}"
                        : $"step {stepsRun} ('{step.CommandId}') carries no compensation";
            }
        }

        var message = $"Macro '{macro.Name}' completed all {stepsRun} step(s).";

        if (undoUnavailableStep is not null || stepCompensations.Count == 0)
        {
            return CommandResult.Success(
                message,
                undoUnavailableReason: undoUnavailableStep ?? "the macro ran no compensable step");
        }

        return CommandResult.Success(message, compensation: CompoundCompensation(macro.Name, stepCompensations));
    }

    /// <summary>
    /// Builds the one compound compensation a fully-compensable macro run
    /// produces (`WP 21.1A`): undo runs every step's own <see cref="CommandCompensation.Undo"/>
    /// in reverse order — the last thing the macro did is the first thing
    /// undone — and redo runs every step's own <see cref="CommandCompensation.Redo"/>
    /// back in the original, forward order. Stops at the first step whose
    /// own reversal is refused or fails, reporting which — a partial undo
    /// is disclosed, not silently completed nor silently discarded, the
    /// identical honesty <see cref="HandleAsync"/> itself already gives a
    /// macro run that stops partway through.
    /// </summary>
    private static CommandCompensation CompoundCompensation(string macroName, IReadOnlyList<CommandCompensation> stepCompensations) =>
        new(
            $"Macro '{macroName}'",
            undo: async ct =>
            {
                for (var i = stepCompensations.Count - 1; i >= 0; i--)
                {
                    var result = await stepCompensations[i].Undo(ct).ConfigureAwait(false);
                    if (!result.Succeeded)
                    {
                        return CommandResult.Failure(
                            $"Macro '{macroName}' undo stopped at step {i + 1}/{stepCompensations.Count}: {result.Message ?? "failed"}.");
                    }
                }

                return CommandResult.Success($"Macro '{macroName}' undone.");
            },
            redo: async ct =>
            {
                for (var i = 0; i < stepCompensations.Count; i++)
                {
                    var result = await stepCompensations[i].Redo(ct).ConfigureAwait(false);
                    if (!result.Succeeded)
                    {
                        return CommandResult.Failure(
                            $"Macro '{macroName}' redo stopped at step {i + 1}/{stepCompensations.Count}: {result.Message ?? "failed"}.");
                    }
                }

                return CommandResult.Success($"Macro '{macroName}' redone.");
            });

    /// <summary>
    /// The prompt one step is replayed with (`WP 20.2C`) — or
    /// <see langword="null"/> when none is needed (the step declares
    /// neither values nor a confirmation, so the registry never asks) or
    /// none can help (something is missing that <paramref name="recordedValues"/>
    /// does not carry, and <see cref="_fallbackPrompt"/> was not supplied).
    /// </summary>
    /// <remarks>
    /// The last case is deliberate, not an oversight: passing no prompt at
    /// all lets <see cref="ICommandRegistry.InvokeAsync(string, CommandContext, CommandParameterPrompt?, CancellationToken)"/>
    /// report its own honest "needs additional input, and no input surface
    /// was supplied" — the identical message this handler reported for
    /// every parameterised step before this Work Package
    /// (<c>MacroBindingEligibilityTests.AMacroStep_ThatNeedsAPerson_FailsHonestly_AndNeverPrompts</c>),
    /// rather than this method inventing a second, slightly different one.
    /// </remarks>
    private CommandParameterPrompt? BuildStepPrompt(CommandDescriptor? descriptor, IReadOnlyDictionary<string, string> recordedValues)
    {
        var binding = descriptor?.Binding;

        if (binding is null || !binding.RequiresPrompt)
            return null;

        var missing = binding.Parameters.Where(p => !recordedValues.ContainsKey(p.Name)).ToList();
        var needsMore = missing.Count > 0 || binding.ConfirmationMessage is not null;

        if (!needsMore)
        {
            // Every value this step's binding declares was already
            // recorded, and it declares no confirmation to ask — answered
            // silently from the recording alone, whether or not a
            // fallback was ever supplied.
            return (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(recordedValues);
        }

        if (_fallbackPrompt is null)
            return null;

        // Asked only for what recording left out. A confirmation is still
        // never answered from the recording, but the fallback itself may
        // supply one — a live person, asked once, exactly as any other
        // prompt-bearing invocation already asks.
        return async (promptDescriptor, parameters, confirmationMessage, ct) =>
        {
            var stillMissing = parameters.Where(p => !recordedValues.ContainsKey(p.Name)).ToList();
            var asked = await _fallbackPrompt(promptDescriptor, stillMissing, confirmationMessage, ct)
                .ConfigureAwait(false);

            if (asked is null)
                return null;

            var combined = new Dictionary<string, string>(recordedValues, StringComparer.Ordinal);
            foreach (var (key, value) in asked)
                combined[key] = value;

            return combined;
        };
    }
}
