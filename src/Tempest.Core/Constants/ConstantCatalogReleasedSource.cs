using Tempest.Core.ReferenceData;

namespace Tempest.Core.Constants;

/// <summary>
/// Presents the registered <see cref="IConstantCatalog"/> under the narrow
/// <see cref="IReleasedConstantSource"/> seam a calculation consumes.
/// </summary>
/// <remarks>
/// Adds no behaviour, and exists for the same reason
/// <see cref="Standards.StandardCatalogResolver"/> does: registering one
/// implementation type under two service types would construct two
/// catalogues over the same store, each with its own write locks, quietly
/// losing the check-then-write atomicity
/// <see cref="ReferenceDataCatalog{TDefinition}"/> depends on.
/// </remarks>
public sealed class ConstantCatalogReleasedSource : IReleasedConstantSource
{
    private readonly IConstantCatalog _catalog;

    /// <summary>Initialises a new instance of the <see cref="ConstantCatalogReleasedSource"/> class.</summary>
    /// <param name="catalog">The registered Engineering Constants Library this source reads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is <see langword="null"/>.</exception>
    public ConstantCatalogReleasedSource(IConstantCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
    }

    /// <inheritdoc />
    public Task<ReleasedConstant?> FindReleasedAsync(string symbol, CancellationToken cancellationToken = default) =>
        _catalog.FindReleasedAsync(symbol, cancellationToken);
}
