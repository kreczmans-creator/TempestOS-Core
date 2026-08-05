using System.Text.Json;
using Tempest.Core.ExportImport;
using Tempest.Core.Requirements;

namespace Tempest.Samples;

/// <summary>
/// Exports and re-imports a whole Requirement Collection (a Requirement
/// Set) — every live member requirement's own Identifier/Statement/
/// Category/Status/Owner/Priority, plus the collection's own Name —
/// reusing the exact same three interfaces <see cref="RequirementExportAdapter"/>
/// (`WP 7.3A`) already demonstrates for a single requirement, scaled to
/// Requirement Set granularity for `WP 9.1A`'s own "Import/Export" scope
/// item. Never a new Export/Import mechanism — the same
/// <see cref="IExportable"/>/<see cref="IExportableKind"/>/
/// <see cref="IImportable"/> triad, the same <see cref="ImportService.RegisterImportable"/>
/// registration precedent (`ADR-0044`). Lives in <c>Tempest.Samples</c>,
/// not <c>Tempest.App</c> — mirroring <see cref="RequirementExportAdapter"/>'s
/// own identical placement, and required by the same project-reference
/// direction (`Tempest.App` depends on `Tempest.Samples`, never the
/// reverse), since <c>RequirementsWorkspaceSampleModule</c> (also
/// <c>Tempest.Samples</c>) is what registers it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Status is exported for informational value only, never replayed on
/// import:</b> <see cref="RequirementStatusTransitions"/> only permits
/// specific paths forward from <c>Draft</c> — blindly calling
/// <see cref="IRequirementsService.SetStatusAsync"/> with an arbitrary
/// recorded target status could throw <see cref="InvalidRequirementStatusTransitionException"/>
/// for a perfectly valid export. Every imported requirement deliberately
/// starts at <c>Draft</c>, mirroring <see cref="RequirementExportAdapter"/>'s
/// own identical, already-disclosed precedent (it does not carry Status,
/// Owner, or Priority at all).
/// </para>
/// <para>
/// <b>Import re-creates under new identifiers, into a new collection,
/// rather than overwriting the original</b> — the same deliberately
/// minimal round-trip demonstration <see cref="RequirementExportAdapter"/>
/// itself documents, not a general Requirements re-import/merge policy. A
/// requirement whose own re-created identifier collides (astronomically
/// unlikely, given the GUID suffix) is skipped, not fatal to the rest of
/// the import — mirroring <see cref="IImportService.ImportAsync"/>'s own
/// per-section (not per-item) atomicity guarantee, one level down.
/// </para>
/// </remarks>
public sealed class RequirementCollectionExportAdapter : IExportable, IExportableKind, IImportable
{
    /// <summary>The schema version this adapter's own payload shape uses.</summary>
    public const int CurrentSchemaVersion = 1;

    private readonly IRequirementsService _requirementsService;
    private readonly Guid _collectionId;

    /// <summary>Initialises a new instance of the <see cref="RequirementCollectionExportAdapter"/> class.</summary>
    public RequirementCollectionExportAdapter(IRequirementsService requirementsService, string kind, Guid collectionId)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        _requirementsService = requirementsService;
        Kind = kind;
        _collectionId = collectionId;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public int SchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var collection = await _requirementsService.FindCollectionAsync(_collectionId, cancellationToken).ConfigureAwait(false);
        var members = new List<RequirementExportEntry>();

        if (collection is not null)
        {
            foreach (var requirementId in collection.MemberRequirementIds)
            {
                var requirement = await _requirementsService.FindAsync(requirementId, cancellationToken).ConfigureAwait(false);
                if (requirement is { IsDeleted: false })
                {
                    members.Add(new RequirementExportEntry(
                        requirement.Identifier, requirement.Statement, requirement.Category, requirement.Status, requirement.Owner, requirement.Priority));
                }
            }
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(new RequirementCollectionExportPayload(collection?.Name ?? string.Empty, members));
        await destination.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ImportAsync(Stream payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var buffer = new MemoryStream();
        await payload.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        var data = JsonSerializer.Deserialize<RequirementCollectionExportPayload>(buffer.ToArray())
            ?? throw new InvalidOperationException("Requirement Collection export payload could not be deserialised.");

        var importedCollection = await _requirementsService.CreateCollectionAsync(
            $"{data.Name} (Imported {Guid.NewGuid().ToString("N")[..8]})", cancellationToken).ConfigureAwait(false);

        foreach (var member in data.Members)
        {
            IRequirement created;

            try
            {
                created = await _requirementsService.CreateAsync(
                    $"{member.Identifier}-imported-{Guid.NewGuid():N}", member.Statement, member.Category, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (DuplicateRequirementIdentifierException)
            {
                continue;
            }

            if (member.Owner is not null)
                await _requirementsService.SetOwnerAsync(created.Id, member.Owner, cancellationToken).ConfigureAwait(false);

            if (member.Priority is not null)
                await _requirementsService.SetPriorityAsync(created.Id, member.Priority, cancellationToken).ConfigureAwait(false);

            await _requirementsService.AddToCollectionAsync(importedCollection.Id, created.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record RequirementExportEntry(string Identifier, string Statement, string? Category, RequirementStatus Status, string? Owner, RequirementPriority? Priority);

    private sealed record RequirementCollectionExportPayload(string Name, IReadOnlyList<RequirementExportEntry> Members);
}
