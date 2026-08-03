namespace Tempest.Core.Materials;

/// <summary>
/// Thrown when an operation requiring an existing material (e.g.
/// <see cref="IMaterialCatalog.ReviseAsync"/>) is given a <c>materialId</c>
/// that does not exist. <see cref="IMaterialCatalog.FindAsync"/> itself
/// never throws this — a nullable return is used there instead, since "not
/// found" is an ordinary, expected outcome for a catalogue lookup.
/// </summary>
public sealed class MaterialNotFoundException : MaterialsException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="MaterialNotFoundException"/> class.
    /// </summary>
    /// <param name="materialId">The material identity that does not exist.</param>
    public MaterialNotFoundException(string materialId)
        : base($"No material is registered with Id '{materialId}'.")
    {
        MaterialId = materialId;
    }

    /// <summary>Gets the material identity that does not exist.</summary>
    public string MaterialId { get; }
}
