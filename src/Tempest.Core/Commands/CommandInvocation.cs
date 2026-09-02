namespace Tempest.Core.Commands;

/// <summary>What happened when a command was invoked by Id with a context.</summary>
public enum CommandOutcome
{
    /// <summary>The command ran; <see cref="CommandInvocation.Result"/> says whether it succeeded.</summary>
    Executed,

    /// <summary>A person was asked for something and declined. Nothing ran, and nothing changed.</summary>
    Cancelled,

    /// <summary>The command could not be invoked; <see cref="CommandInvocation.Reason"/> says why.</summary>
    Unavailable,
}

/// <summary>
/// The outcome of <see cref="ICommandRegistry.InvokeAsync(string, CommandContext, CommandParameterPrompt?, CancellationToken)"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three outcomes, because there genuinely are three.</b>
/// <see cref="CommandResult"/> answers "did it succeed", which is the right
/// question once a command has run — but a person closing a prompt has not
/// failed at anything, and a command that needs a selection nobody made has
/// not failed either. Reporting either as a <see cref="CommandResult.Failure"/>
/// would put an error in front of a user who did nothing wrong, so
/// <see cref="CommandResult"/> is left to mean exactly what it already
/// means and this type carries the distinction instead.
/// </para>
/// <para>
/// No new exception type accompanies this: an unavailable command and a
/// declined prompt are both expected outcomes, not defects, and
/// <c>ADR-0038</c> reserves exceptions for defects.
/// </para>
/// </remarks>
public sealed class CommandInvocation
{
    private CommandInvocation(CommandOutcome outcome, CommandResult? result, string? reason)
    {
        Outcome = outcome;
        Result = result;
        Reason = reason;
    }

    /// <summary>The command ran and returned <paramref name="result"/>.</summary>
    /// <param name="result">The result its handler returned.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public static CommandInvocation Executed(CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new CommandInvocation(CommandOutcome.Executed, result, null);
    }

    /// <summary>A person declined. Nothing ran; report nothing.</summary>
    public static CommandInvocation Cancelled { get; } = new(CommandOutcome.Cancelled, null, null);

    /// <summary>The command could not be invoked, for the stated reason.</summary>
    /// <param name="reason">A user-facing explanation of what is missing.</param>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static CommandInvocation Unavailable(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new CommandInvocation(CommandOutcome.Unavailable, null, reason);
    }

    /// <summary>Gets what happened.</summary>
    public CommandOutcome Outcome { get; }

    /// <summary>
    /// Gets the handler's own result. Non-<see langword="null"/> exactly
    /// when <see cref="Outcome"/> is <see cref="CommandOutcome.Executed"/>.
    /// </summary>
    public CommandResult? Result { get; }

    /// <summary>
    /// Gets why the command could not be invoked. Non-<see langword="null"/>
    /// exactly when <see cref="Outcome"/> is
    /// <see cref="CommandOutcome.Unavailable"/>.
    /// </summary>
    public string? Reason { get; }
}
