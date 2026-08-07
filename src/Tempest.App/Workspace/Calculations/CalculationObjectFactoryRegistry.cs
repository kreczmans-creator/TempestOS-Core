using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Builds a new Calculation Domain object — <c>"Calculation"</c> or
/// <c>"CalculationSet"</c> — a small, Workspace-layer composition helper
/// wrapping <see cref="EngineeringObjectFactory{T}"/> instances, mirroring
/// <see cref="Mechanical.MechanicalObjectFactoryRegistry"/>'s own identical
/// shape (`WP 9.0A`) exactly. Never a Domain-layer registry contract — this
/// type lives entirely in <c>Tempest.App</c>.
/// </summary>
public sealed class CalculationObjectFactoryRegistry
{
    /// <summary>The Kinds this registry can construct.</summary>
    public static readonly IReadOnlyList<string> SupportedKinds = ["Calculation", "CalculationSet"];

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="CalculationObjectFactoryRegistry"/> class.</summary>
    public CalculationObjectFactoryRegistry(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <summary>Creates a new object of <paramref name="kind"/>, moving it under <paramref name="parentId"/> if one is given.</summary>
    /// <param name="memberCalculationIds">Only meaningful for <c>"CalculationSet"</c> — a Calculation Set's own members are frozen at construction, mirroring <c>Configuration.MemberRevisions</c>'s own identical `WP 9.0B` shape (no mutator exists there either).</param>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is not one of <see cref="SupportedKinds"/>, or <paramref name="displayName"/> is null/empty/whitespace.</exception>
    public async Task<IEngineeringObject> CreateAsync(
        string kind, string? identifier, string displayName, string initialContent, Guid? parentId,
        IReadOnlyList<Guid>? memberCalculationIds = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(initialContent);

        IEngineeringObject created = kind switch
        {
            "Calculation" => await new EngineeringObjectFactory<Calculation>(
                "Calculation", _context, (doc, rev) => new Calculation(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            "CalculationSet" => await new EngineeringObjectFactory<CalculationSet>(
                "CalculationSet", _context, (doc, rev) => new CalculationSet(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty, memberCalculationIds))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            _ => throw new ArgumentException($"'{kind}' is not a supported Calculation Kind — expected one of: {string.Join(", ", SupportedKinds)}.", nameof(kind)),
        };

        if (parentId is { } pid && created is IHasParent hasParent)
            await hasParent.MoveAsync(pid, cancellationToken).ConfigureAwait(false);

        return created;
    }
}
