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
    private CommandResult(bool succeeded, string? message, Guid? subjectId = null, string? subjectKind = null)
    {
        Succeeded = succeeded;
        Message = message;
        SubjectId = subjectId;
        SubjectKind = subjectKind;
    }

    /// <summary>
    /// Creates a <see cref="CommandResult"/> reporting success.
    /// </summary>
    /// <param name="message">An optional message describing the outcome.</param>
    /// <param name="subjectId">The object the command made or acted on, when there is one (`WP 17.9.4`).</param>
    /// <param name="subjectKind">That object's Kind.</param>
    public static CommandResult Success(string? message = null, Guid? subjectId = null, string? subjectKind = null) =>
        new(succeeded: true, message, subjectId, subjectKind);

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

    /// <summary>
    /// The object the command made or acted on, when the handler says so
    /// (`WP 17.9.4`). A create handler always says so, which is what lets
    /// the shell open the new object right up rather than announce it.
    /// </summary>
    public Guid? SubjectId { get; }

    /// <summary>The Kind of <see cref="SubjectId"/>, when set.</summary>
    public string? SubjectKind { get; }
}
