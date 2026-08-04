namespace Tempest.Core.EngineeringDomain;

/// <summary>Owned by <c>Tempest.Core.Materials</c> (<c>MaterialSpecification</c>); not given a competing concrete realisation here (WP 8.2C).</summary>
public interface IMaterial : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasRevisions
{
    IReadOnlyDictionary<string, MaterialPropertyValue> Properties { get; }
}

public sealed record MaterialPropertyValue(object Value, string ConfidenceLevel);
