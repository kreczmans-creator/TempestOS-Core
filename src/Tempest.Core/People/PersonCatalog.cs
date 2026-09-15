using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.People;

/// <summary>The consultancy's own reference library of people (Product Owner finding D8).</summary>
public interface IPersonCatalog : IReferenceDataCatalog<Person>
{
    /// <summary>Returns the person registered under <paramref name="displayName"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="displayName"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<Person>?> FindByDisplayNameAsync(string displayName, CancellationToken cancellationToken = default);

    /// <summary>Every Released, active person — the list a requirement Owner picker, or any future person picker, may offer. Never <see langword="null"/>; ordered by display name.</summary>
    Task<IReadOnlyList<IReferenceRecord<Person>>> FindSelectableAsync(CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IPersonCatalog"/> implementation — the same thin, typed index over <see cref="IEngineeringDocumentStore"/> every other Group A/P04 library is (`ADR-0126`).</summary>
public sealed class PersonCatalog : ReferenceDataCatalog<Person>, IPersonCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every person's own backing document carries.</summary>
    public const string PersonDocumentKind = "Person";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> this library is registered and shown under.</summary>
    public const string PersonLibraryName = "People";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>personId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "People.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each display-name key to the <c>personId</c> holding it.</summary>
    public const string DisplayNameIndexCollection = "People.DisplayNameIndex";

    /// <summary>Initialises a new instance of the <see cref="PersonCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own person records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public PersonCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => PersonLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => PersonDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => DisplayNameIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<Person>?> FindByDisplayNameAsync(string displayName, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(Person.DisplayNameKeyFor(displayName), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<Person>>> FindSelectableAsync(CancellationToken cancellationToken = default)
    {
        var selectable = await FilterAsync(
            record => record.ValidationState == ReferenceValidationState.Released && record.Definition.IsActive,
            cancellationToken).ConfigureAwait(false);

        return [.. selectable.OrderBy(r => r.Definition.DisplayName, StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(Person definition) => definition.DisplayNameKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(Person definition) => $"Person '{definition.DisplayName}'";
}

/// <summary>
/// The one fixed <see cref="ReferenceProvenance"/> every person added by
/// hand is registered with (`WP 20.10F`) — shared by the Libraries view's
/// own "Add a person" form and the requirement Owner picker's own "Add
/// person…" affordance, so the two add paths can never drift.
/// </summary>
/// <remarks>
/// A person is not transcribed from a datasheet, a standard or a
/// manufacturer's catalogue the way every other Group A record is —
/// nothing outside TempestOS itself could honestly be named as the source
/// of somebody's own name. This still names <em>something</em>
/// (<see cref="ReferenceProvenance.IdentifiesASource"/>), which is what
/// lets a person record leave <see cref="ReferenceValidationState.Draft"/>
/// at all — the same "provenance-earns-status" rule every sibling library
/// enforces, applied honestly rather than worked around.
/// </remarks>
public static class PersonProvenance
{
    /// <summary>The fixed provenance every hand-added person is registered with.</summary>
    public static ReferenceProvenance Default { get; } = new(
        SourceOrganisation: "TempestOS",
        SourceDocument: "Entered directly in the People library.",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Added by hand; not verified against any external source until reviewed.");
}
