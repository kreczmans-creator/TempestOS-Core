namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// An object of one of the engineering disciplines that share a single,
/// attribute-carrying shape (`ADR-0163`, `ADR-0170`): Structural,
/// Electrical, Building Services / HVAC and Fluid Power. Each Kind (a member, a circuit, a zone…)
/// declares the attributes it carries; the values are free text, recorded
/// exactly as the engineer typed them, with no design-code logic applied.
/// </summary>
public interface IDisciplineObject : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    /// <summary>Every attribute recorded on this object, by name.</summary>
    IReadOnlyDictionary<string, string> Attributes { get; }

    /// <summary>Records <paramref name="value"/> against <paramref name="name"/>, or clears it when <paramref name="value"/> is blank.</summary>
    Task SetAttributeAsync(string name, string? value, CancellationToken cancellationToken = default);
}
