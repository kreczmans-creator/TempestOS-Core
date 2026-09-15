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
    private CommandResult(
        bool succeeded, string? message, Guid? subjectId = null, string? subjectKind = null,
        CommandCompensation? compensation = null, string? undoUnavailableReason = null)
    {
        Succeeded = succeeded;
        Message = message;
        SubjectId = subjectId;
        SubjectKind = subjectKind;
        Compensation = compensation;
        UndoUnavailableReason = undoUnavailableReason;
    }

    /// <summary>
    /// Creates a <see cref="CommandResult"/> reporting success.
    /// </summary>
    /// <param name="message">An optional message describing the outcome.</param>
    /// <param name="subjectId">The object the command made or acted on, when there is one (`WP 17.9.4`).</param>
    /// <param name="subjectKind">That object's Kind.</param>
    /// <param name="compensation">
    /// How to reverse this outcome, when the handler that produced it knows
    /// how (`WP 21.1A`) — <see langword="null"/> for a result nothing can
    /// undo, exactly the behaviour before this Work Package.
    /// </param>
    /// <param name="undoUnavailableReason">
    /// Why this outcome, though it succeeded, cannot be undone — set only by
    /// a handler whose own family is sometimes compensable and sometimes not
    /// (a status transition the lifecycle table will not permit reversing).
    /// Never set together with <paramref name="compensation"/>.
    /// </param>
    public static CommandResult Success(
        string? message = null, Guid? subjectId = null, string? subjectKind = null,
        CommandCompensation? compensation = null, string? undoUnavailableReason = null) =>
        new(succeeded: true, message, subjectId, subjectKind, compensation, undoUnavailableReason);

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

    /// <summary>
    /// How to reverse this outcome, or <see langword="null"/> when nothing
    /// reverses it — either because this command's family carries no
    /// compensation at all, or (see <see cref="UndoUnavailableReason"/>)
    /// because this particular outcome cannot be (`WP 21.1A`).
    /// </summary>
    public CommandCompensation? Compensation { get; }

    /// <summary>
    /// Why this outcome cannot be undone, when it succeeded but its own
    /// family sometimes can be and sometimes cannot — non-<see langword="null"/>
    /// only when <see cref="Compensation"/> is <see langword="null"/> and a
    /// handler wants that fact surfaced (Command History says "cannot be
    /// undone: {reason}.") rather than silently offering nothing (`WP 21.1A`).
    /// </summary>
    public string? UndoUnavailableReason { get; }
}
