namespace Tempest.Core.EngineeringDomain;

/// <summary>The base contract every canonical Engineering Object satisfies, mirroring <see cref="EngineeringData.IEngineeringDocument"/> exactly (ADR-0072).</summary>
public interface IEngineeringObject
{
    Guid Id { get; }
    string Kind { get; }
    int CurrentRevisionNumber { get; }
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// This Kind's own business identifier (`TD-38`) — the field its own
    /// identity section shows first: a Part's or a Calculation's name, a
    /// Document's number if it has one else its name, a Requirement's own
    /// identifier. A read-only projection over existing fields, never a
    /// second stored value, and never null or empty — every canonical
    /// object has a display name, and that is always at least a fallback.
    /// </summary>
    string BusinessIdentifier { get; }
}
