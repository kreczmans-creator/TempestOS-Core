namespace Tempest.Core.Audit;

/// <summary>
/// The concrete, immutable <see cref="IAuditRecord"/> implementation.
/// </summary>
public sealed class AuditRecord : IAuditRecord
{
    /// <summary>
    /// Initialises a new instance of the <see cref="AuditRecord"/> class.
    /// </summary>
    /// <param name="actorId">The identity of the principal that performed the action.</param>
    /// <param name="action">The action that was performed.</param>
    /// <param name="occurredAt">When the action occurred.</param>
    /// <param name="detail">Additional detail describing the action.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="actorId"/> or <paramref name="action"/> is
    /// <see langword="null"/>, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="detail"/> is <see langword="null"/>.</exception>
    public AuditRecord(string actorId, string action, DateTimeOffset occurredAt, IReadOnlyDictionary<string, string> detail)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("Actor id must not be null, empty, or whitespace.", nameof(actorId));

        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action must not be null, empty, or whitespace.", nameof(action));

        ArgumentNullException.ThrowIfNull(detail);

        ActorId = actorId;
        Action = action;
        OccurredAt = occurredAt;
        Detail = detail;
    }

    /// <inheritdoc />
    public string ActorId { get; }

    /// <inheritdoc />
    public string Action { get; }

    /// <inheritdoc />
    public DateTimeOffset OccurredAt { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Detail { get; }
}
