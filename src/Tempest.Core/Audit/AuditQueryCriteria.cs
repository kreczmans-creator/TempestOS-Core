namespace Tempest.Core.Audit;

/// <summary>Filter criteria for an audit query. Immutable; every property optional.</summary>
public sealed record AuditQueryCriteria
{
    /// <summary>
    /// Initialises a new instance of the <see cref="AuditQueryCriteria"/> record.
    /// </summary>
    /// <param name="actorId">Restricts results to this actor, if supplied.</param>
    /// <param name="action">Restricts results to this action, if supplied.</param>
    /// <param name="from">Restricts results to records occurring at or after this instant, if supplied.</param>
    /// <param name="to">Restricts results to records occurring at or before this instant, if supplied.</param>
    /// <exception cref="ArgumentException">
    /// Both <paramref name="from"/> and <paramref name="to"/> are
    /// supplied and <paramref name="from"/> is later than
    /// <paramref name="to"/>.
    /// </exception>
    public AuditQueryCriteria(string? actorId = null, string? action = null, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        if (from is not null && to is not null && from > to)
            throw new ArgumentException($"'{nameof(from)}' ({from}) must not be later than '{nameof(to)}' ({to}).", nameof(from));

        ActorId = actorId;
        Action = action;
        From = from;
        To = to;
    }

    /// <summary>Gets the actor restriction, if any.</summary>
    public string? ActorId { get; }

    /// <summary>Gets the action restriction, if any.</summary>
    public string? Action { get; }

    /// <summary>Gets the lower time bound (inclusive), if any.</summary>
    public DateTimeOffset? From { get; }

    /// <summary>Gets the upper time bound (inclusive), if any.</summary>
    public DateTimeOffset? To { get; }
}
