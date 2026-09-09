using System.Collections.Concurrent;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Tests.ReferenceData;

/// <summary>
/// A hand-written, in-memory <see cref="IPersistenceStore"/> test double —
/// mirrors the convention every other test area in this suite follows,
/// duplicated here rather than shared, per this codebase's own established
/// precedent of small, test-local fakes.
/// </summary>
internal sealed class InMemoryPersistenceStore : IPersistenceStore
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    private static string MakeKey(string collection, string key) => $"{collection} {key}";

    public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_values.TryGetValue(MakeKey(collection, key), out var value) ? value : null);

    public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default)
    {
        _values[MakeKey(collection, key)] = value;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default)
    {
        _values.TryRemove(MakeKey(collection, key), out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default)
    {
        var prefix = $"{collection} ";
        IReadOnlyList<string> keys = _values.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .Select(k => k[prefix.Length..])
            .ToList();

        return Task.FromResult(keys);
    }
}

/// <summary>
/// A deliberately trivial domain, used to test the shared reference-data
/// machinery without dragging any real library's own engineering semantics
/// into the test.
/// </summary>
/// <remarks>
/// The point of a fake domain here is that a failure means the shared layer
/// is wrong, not that Bearings or Fasteners is. Every real library's own
/// tests then cover only what is genuinely theirs.
/// </remarks>
internal sealed record WidgetDefinition
{
    public required string Designation { get; init; }

    public string? Colour { get; init; }
}

internal sealed class WidgetCatalog : ReferenceDataCatalog<WidgetDefinition>
{
    public WidgetCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    public override string LibraryName => "Widgets";

    public override string DocumentKind => "WidgetReference";

    public override string IndexCollectionName => "Widgets.Index";

    protected override string? GetSecondaryKey(WidgetDefinition definition) => definition.Designation.Trim().ToUpperInvariant();

    protected override string DescribeSecondaryKey(WidgetDefinition definition) => $"Designation '{definition.Designation}'";

    public Task<IReferenceRecord<WidgetDefinition>?> FindByDesignationAsync(string designation, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(designation.Trim().ToUpperInvariant(), cancellationToken);

    public Task<IReadOnlyList<IReferenceRecord<WidgetDefinition>>> WithColourAsync(string colour, CancellationToken cancellationToken = default) =>
        FilterAsync(record => string.Equals(record.Definition.Colour, colour, StringComparison.OrdinalIgnoreCase), cancellationToken);
}

/// <summary>A catalogue with no secondary key at all, so the shared layer's own "no secondary key" path is exercised.</summary>
internal sealed class KeylessWidgetCatalog : ReferenceDataCatalog<WidgetDefinition>
{
    public KeylessWidgetCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore)
        : base(documentStore, persistenceStore)
    {
    }

    public override string LibraryName => "KeylessWidgets";

    public override string DocumentKind => "KeylessWidgetReference";

    public override string IndexCollectionName => "KeylessWidgets.Index";
}

internal static class ReferenceDataFixtures
{
    public static WidgetCatalog BuildCatalog() => BuildCatalog(out _, out _);

    public static WidgetCatalog BuildCatalog(out EngineeringDocumentStore documentStore, out InMemoryPersistenceStore persistenceStore)
    {
        persistenceStore = new InMemoryPersistenceStore();
        documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        return new WidgetCatalog(documentStore, persistenceStore);
    }

    public static WidgetDefinition Widget(string designation = "W-1", string? colour = null) =>
        new() { Designation = designation, Colour = colour };

    /// <summary>Provenance that identifies a source but has not been verified — what an honest import leaves behind.</summary>
    public static ReferenceProvenance Sourced() => new(
        SourceOrganisation: "TestFixture Publications",
        SourceDocument: "Fixture handbook (not a real publication)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Table 1",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data.");

    /// <summary>Provenance a named reviewer has verified — the only kind that can reach Released.</summary>
    public static ReferenceProvenance Verified() => Sourced() with
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

    public static async Task<IReferenceRecord<WidgetDefinition>> ReleaseAsync(WidgetCatalog catalog, string recordId)
    {
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Validated, "Rules pass.");
        return await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Released, "Released.");
    }
}
