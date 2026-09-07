using Tempest.Core.ReferenceData;

namespace Tempest.Core.Standards;

/// <summary>
/// Presents the registered <see cref="IStandardCatalog"/> under the narrow
/// <see cref="IStandardResolver"/> seam every citing library depends on.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="StandardCatalog"/> already implements
/// <see cref="IStandardResolver"/>, so this class adds no behaviour. It
/// exists for one reason: registering the same implementation type under
/// two service types in the container would construct <em>two</em>
/// catalogues, each with its own write locks over the same store, and the
/// check-then-write atomicity
/// <see cref="ReferenceDataCatalog{TDefinition}"/> depends on would be
/// silently lost. Forwarding to the single registered catalogue keeps one
/// instance behind both seams.
/// </para>
/// <para>
/// The same reasoning applies to
/// <see cref="Constants.ConstantCatalogReleasedSource"/>.
/// </para>
/// </remarks>
public sealed class StandardCatalogResolver : IStandardResolver
{
    private readonly IStandardCatalog _catalog;

    /// <summary>Initialises a new instance of the <see cref="StandardCatalogResolver"/> class.</summary>
    /// <param name="catalog">The registered Standards Library this resolver reads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is <see langword="null"/>.</exception>
    public StandardCatalogResolver(IStandardCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string standardId, CancellationToken cancellationToken = default) =>
        _catalog.ExistsAsync(standardId, cancellationToken);
}
