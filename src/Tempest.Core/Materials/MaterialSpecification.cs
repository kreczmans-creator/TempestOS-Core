namespace Tempest.Core.Materials;

internal sealed class MaterialSpecification : IMaterialSpecification
{
    public MaterialSpecification(
        string materialId,
        string name,
        string? category,
        IReadOnlyDictionary<string, MaterialProperty> properties,
        Guid underlyingDocumentId,
        int revisionNumber)
    {
        MaterialId = materialId;
        Name = name;
        Category = category;
        Properties = properties;
        UnderlyingDocumentId = underlyingDocumentId;
        RevisionNumber = revisionNumber;
    }

    public string MaterialId { get; }
    public string Name { get; }
    public string? Category { get; }
    public IReadOnlyDictionary<string, MaterialProperty> Properties { get; }
    public Guid UnderlyingDocumentId { get; }
    public int RevisionNumber { get; }
}
