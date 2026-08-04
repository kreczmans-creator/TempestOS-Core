namespace Tempest.Core.EngineeringDomain;

/// <summary>The base contract every canonical Engineering Object satisfies, mirroring <see cref="EngineeringData.IEngineeringDocument"/> exactly (ADR-0072).</summary>
public interface IEngineeringObject
{
    Guid Id { get; }
    string Kind { get; }
    int CurrentRevisionNumber { get; }
    DateTimeOffset CreatedAt { get; }
}
