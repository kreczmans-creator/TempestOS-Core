namespace Tempest.Core.Audit;

/// <summary>A single, durable, immutable record of an action taken by a principal.</summary>
public interface IAuditRecord
{
    /// <summary>Gets the identity of the principal that performed the action.</summary>
    string ActorId { get; }

    /// <summary>Gets the action that was performed.</summary>
    string Action { get; }

    /// <summary>Gets when the action occurred.</summary>
    DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// Gets additional detail describing the action. Never
    /// <see langword="null"/>; empty if none was supplied. Each calling
    /// service's own key/value content is free to evolve without
    /// changing this contract — a correlation identifier tying several
    /// related records together, for example, is carried here under the
    /// well-known key <c>AuditRecorder.CorrelationIdDetailKey</c>, not
    /// as a dedicated property.
    /// </summary>
    IReadOnlyDictionary<string, string> Detail { get; }
}
