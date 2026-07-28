namespace Tempest.Core.Commands;

/// <summary>
/// The outcome of dispatching a command — whether it succeeded, and an
/// optional message describing the outcome.
/// </summary>
/// <remarks>
/// A command "has an expected result" (the property that distinguishes it
/// from an event, per the Engineering Glossary) — this type is that result.
/// <see cref="Failure(string)"/> is the expected path for a handler that
/// encounters a foreseeable, nameable failure case (invalid input, a
/// business rule violation); a handler that encounters a genuine defect in
/// its own execution should throw instead, and let the exception propagate
/// per ADR-0038.
/// </remarks>
public sealed class CommandResult
{
    private CommandResult(bool succeeded, string? message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    /// <summary>
    /// Creates a <see cref="CommandResult"/> reporting success.
    /// </summary>
    /// <param name="message">An optional message describing the outcome.</param>
    public static CommandResult Success(string? message = null) => new(succeeded: true, message);

    /// <summary>
    /// Creates a <see cref="CommandResult"/> reporting a foreseeable,
    /// expected failure.
    /// </summary>
    /// <param name="message">A message describing why the command failed.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="message"/> is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public static CommandResult Failure(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message must not be null, empty, or whitespace.", nameof(message));

        return new(succeeded: false, message);
    }

    /// <summary>
    /// Gets a value indicating whether the command succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets a message describing the outcome, if one was supplied. Always
    /// non-<see langword="null"/> for a <see cref="Failure(string)"/> result;
    /// optional for a <see cref="Success"/> result.
    /// </summary>
    public string? Message { get; }
}
