namespace Tempest.Core.ReferenceData;

/// <summary>
/// Resolves a <see cref="ReferencePin"/> into one library, so a
/// record's pinned sources can be checked without the checking code
/// taking a dependency on every library it might cite.
/// </summary>
public interface IReferencePinResolver
{
    /// <summary>The library this resolver answers for, matching <see cref="ReferencePin.Library"/>.</summary>
    string LibraryName { get; }

    /// <summary>The pinned record's current validation state, or <see langword="null"/> where the library no longer holds it.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="pin"/> is <see langword="null"/>.</exception>
    Task<ReferenceValidationState?> ResolveAsync(ReferencePin pin, CancellationToken cancellationToken = default);
}

/// <summary>An <see cref="IReferencePinResolver"/> over any reference-data catalogue.</summary>
/// <typeparam name="TDefinition">The library's own definition type.</typeparam>
public sealed class CatalogPinResolver<TDefinition> : IReferencePinResolver
    where TDefinition : class
{
    private readonly IReferenceDataCatalog<TDefinition> _catalog;

    /// <summary>Initialises a new instance of the <see cref="CatalogPinResolver{TDefinition}"/> class.</summary>
    /// <param name="catalog">The catalogue this resolver answers for.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is <see langword="null"/>.</exception>
    public CatalogPinResolver(IReferenceDataCatalog<TDefinition> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _catalog = catalog;
    }

    /// <inheritdoc />
    public string LibraryName => _catalog.LibraryName;

    /// <inheritdoc />
    public async Task<ReferenceValidationState?> ResolveAsync(ReferencePin pin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pin);

        var record = await _catalog.FindAsync(pin.RecordId, cancellationToken).ConfigureAwait(false);

        return record?.ValidationState;
    }
}
