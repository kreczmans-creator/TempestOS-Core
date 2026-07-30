namespace Tempest.Core.Materials;

/// <summary>
/// Thrown when <see cref="IMaterialCatalog.RegisterAsync"/> is given a
/// <c>materialId</c> that is already registered.
/// </summary>
public sealed class DuplicateMaterialException : MaterialsException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateMaterialException"/> class.
    /// </summary>
    /// <param name="materialId">The material identity that is already registered.</param>
    public DuplicateMaterialException(string materialId)
        : base($"A material is already registered with Id '{materialId}'.")
    {
        MaterialId = materialId;
    }

    /// <summary>Gets the material identity that is already registered.</summary>
    public string MaterialId { get; }
}
