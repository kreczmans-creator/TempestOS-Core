using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Documents;

/// <summary>
/// Builds a new Document Domain object — <c>"Document"</c>, <c>"Drawing"</c>,
/// or <c>"CadModel"</c> — a small, Workspace-layer composition helper
/// wrapping <see cref="EngineeringObjectFactory{T}"/> instances, mirroring
/// <see cref="Calculations.CalculationObjectFactoryRegistry"/>/
/// <see cref="Mechanical.MechanicalObjectFactoryRegistry"/>'s own identical
/// shape (`WP 9.4A`) exactly. Never a Domain-layer registry contract — this
/// type lives entirely in <c>Tempest.App</c>.
/// </summary>
/// <remarks>
/// <see cref="Specification"/>/<see cref="Report"/>/<see cref="Procedure"/>/
/// <see cref="Standard"/>/<see cref="Datasheet"/>/<see cref="ExternalReference"/>
/// are not separate Domain Kinds — <c>ADR-0088</c> realises them as plain
/// <c>"Document"</c> objects distinguished only by
/// <paramref name="classification"/>, since no concrete Domain class for any
/// of them exists anywhere in the platform, and adding one would be exactly
/// the "no contract redesign" this Work Package forbids.
/// <see cref="Resource"/>/<see cref="Tooling"/>/<see cref="Fixture"/>
/// (`WP 9.5A`) extend this exact taxonomy by three further values, for the
/// identical reason — Manufacturing Resources/Tooling/Fixtures have no
/// dedicated Domain Kind either, and `ADR-0088`'s own `Classification`
/// facet is already open, unvalidated, free text (never a closed
/// enumeration `WP 9.4A` itself fixed), so extending its own vocabulary is
/// simple reuse, not a new mechanism.
/// </remarks>
public sealed class DocumentObjectFactoryRegistry
{
    /// <summary>The Kinds this registry can construct.</summary>
    public static readonly IReadOnlyList<string> SupportedKinds = ["Document", "Drawing", "CadModel"];

    /// <summary>The <see cref="EngineeringObjectMetadata.Classification"/> values <c>ADR-0088</c> assigns to a plain <c>"Document"</c> to realise each named Document type this Work Package's own scope lists.</summary>
    public const string Specification = "Specification";
    public const string Report = "Report";
    public const string Procedure = "Procedure";
    public const string Standard = "Standard";
    public const string Datasheet = "Datasheet";
    public const string ExternalReference = "External Reference";

    /// <summary>Extends <c>ADR-0088</c>'s own open `Classification` taxonomy for `WP 9.5A`'s own Manufacturing Resources/Tooling/Fixtures — each realised as a plain `"Document"`, never a new Domain Kind.</summary>
    public const string Resource = "Resource";
    public const string Tooling = "Tooling";
    public const string Fixture = "Fixture";

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="DocumentObjectFactoryRegistry"/> class.</summary>
    public DocumentObjectFactoryRegistry(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <summary>Creates a new object of <paramref name="kind"/>, moving it under <paramref name="parentId"/> if one is given.</summary>
    /// <param name="classification">Meaningful for <c>"Document"</c> — one of this class's own named constants, or any other free-text classification; ignored for <c>"Drawing"</c>/<c>"CadModel"</c>, whose own Kind already is the classification.</param>
    /// <param name="drawingNumber">Only meaningful for <c>"Drawing"</c>.</param>
    /// <param name="modelFormat">Only meaningful for <c>"CadModel"</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is not one of <see cref="SupportedKinds"/>, or <paramref name="displayName"/> is null/empty/whitespace.</exception>
    public async Task<IEngineeringObject> CreateAsync(
        string kind, string? identifier, string displayName, string initialContent, Guid? parentId,
        string? classification = null, string? drawingNumber = null, string? modelFormat = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(initialContent);

        var metadata = classification is null ? EngineeringObjectMetadata.Empty : new EngineeringObjectMetadata(Classification: classification);

        IEngineeringObject created = kind switch
        {
            "Document" => await new EngineeringObjectFactory<Document>(
                "Document", _context, (doc, rev) => new Document(doc, rev, _context, identifier, displayName, metadata))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            "Drawing" => await new EngineeringObjectFactory<Drawing>(
                "Drawing", _context, (doc, rev) => new Drawing(doc, rev, _context, identifier, displayName, metadata, drawingNumber))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            "CadModel" => await new EngineeringObjectFactory<CadModel>(
                "CadModel", _context, (doc, rev) => new CadModel(doc, rev, _context, identifier, displayName, metadata, modelFormat))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            _ => throw new ArgumentException($"'{kind}' is not a supported Document Kind — expected one of: {string.Join(", ", SupportedKinds)}.", nameof(kind)),
        };

        if (parentId is { } pid && created is IHasParent hasParent)
            await hasParent.MoveAsync(pid, cancellationToken).ConfigureAwait(false);

        return created;
    }
}
