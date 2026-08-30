namespace Tempest.Core.Commands;

/// <summary>
/// Whether a command can be invoked against a given
/// <see cref="CommandContext"/> right now, and — when it cannot — a reason
/// a person can act on.
/// </summary>
/// <remarks>
/// The reason is not optional decoration. <c>ADR-0070</c> requires an
/// unavailable command to be shown <i>disabled with its own reason</i>
/// rather than hidden, so a surface needs somewhere to read that reason
/// from; this is it.
/// </remarks>
/// <param name="IsAvailable">Whether the command can be invoked.</param>
/// <param name="Reason">Why it cannot, or <see langword="null"/> when it can.</param>
public sealed record CommandAvailability(bool IsAvailable, string? Reason)
{
    /// <summary>The command can be invoked.</summary>
    public static CommandAvailability Available { get; } = new(true, null);

    /// <summary>The command cannot be invoked, for the stated reason.</summary>
    /// <param name="reason">A user-facing explanation of what is missing.</param>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static CommandAvailability Blocked(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new CommandAvailability(false, reason);
    }
}
