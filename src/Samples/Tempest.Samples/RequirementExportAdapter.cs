using System.Text.Json;
using Tempest.Core.ExportImport;
using Tempest.Core.Requirements;

namespace Tempest.Samples;

/// <summary>
/// Exports and re-imports a single requirement's own current
/// Identifier/Statement/Category, demonstrating the Requirements Engine's
/// own Export/Import integration point — a Requirement Collection or an
/// individual requirement each author their own <see cref="IExportable"/>/
/// <see cref="IImportable"/> implementation, exactly as this adapter does,
/// per <c>WP7.2C Platform Integration Matrix.md</c>.
/// </summary>
/// <remarks>
/// Import re-creates the requirement under a new Id rather than
/// overwriting the original — this adapter's own deliberately minimal
/// round-trip demonstration, not a general Requirements re-import policy.
/// </remarks>
public sealed class RequirementExportAdapter : IExportable, IExportableKind, IImportable
{
    /// <summary>The schema version this adapter's own payload shape uses.</summary>
    public const int CurrentSchemaVersion = 1;

    private readonly IRequirementsService _requirementsService;
    private readonly Guid _requirementId;

    /// <summary>Initialises a new instance of the <see cref="RequirementExportAdapter"/> class.</summary>
    public RequirementExportAdapter(IRequirementsService requirementsService, string kind, Guid requirementId)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(kind);

        _requirementsService = requirementsService;
        Kind = kind;
        _requirementId = requirementId;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public int SchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var requirement = await _requirementsService.FindAsync(_requirementId, cancellationToken).ConfigureAwait(false);

        var payload = JsonSerializer.SerializeToUtf8Bytes(new RequirementExportPayload(
            requirement?.Identifier ?? string.Empty, requirement?.Statement ?? string.Empty, requirement?.Category));

        await destination.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ImportAsync(Stream payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var buffer = new MemoryStream();
        await payload.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        var data = JsonSerializer.Deserialize<RequirementExportPayload>(buffer.ToArray())
            ?? throw new InvalidOperationException("Requirement export payload could not be deserialised.");

        var reimportedIdentifier = $"{data.Identifier}-imported-{Guid.NewGuid():N}";
        await _requirementsService.CreateAsync(reimportedIdentifier, data.Statement, data.Category, cancellationToken).ConfigureAwait(false);
    }

    private sealed record RequirementExportPayload(string Identifier, string Statement, string? Category);
}
