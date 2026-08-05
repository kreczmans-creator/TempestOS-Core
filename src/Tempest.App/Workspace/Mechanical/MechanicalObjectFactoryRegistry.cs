using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Mechanical;

/// <summary>
/// Builds a new Mechanical Product Structure object of one of the eight
/// Kinds this and `WP 9.0B`'s own scope cover (<c>"Project"</c>,
/// <c>"Assembly"</c>, <c>"SubAssembly"</c>, <c>"Part"</c>,
/// <c>"Component"</c>, <c>"Configuration"</c>, <c>"Baseline"</c>,
/// <c>"Release"</c>) — a small, Workspace-layer composition helper
/// wrapping <see cref="EngineeringObjectFactory{T}"/> instances,
/// mirroring <c>EngineeringDomainSampleModule</c>'s own identical inline
/// construction. Never a Domain-layer registry contract — `WP8.2B
/// Dependency Rules.md` §8 proposes none, and none is added here; this type
/// lives entirely in <c>Tempest.App</c>.
/// </summary>
public sealed class MechanicalObjectFactoryRegistry
{
    /// <summary>The Kinds this registry can construct.</summary>
    public static readonly IReadOnlyList<string> SupportedKinds =
        ["Project", "Assembly", "SubAssembly", "Part", "Component", "Configuration", "Baseline", "Release"];

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="MechanicalObjectFactoryRegistry"/> class.</summary>
    public MechanicalObjectFactoryRegistry(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <summary>Creates a new object of <paramref name="kind"/>, moving it under <paramref name="parentId"/> if one is given.</summary>
    /// <param name="memberRevisions">Only meaningful for <c>"Configuration"</c>/<c>"Baseline"</c>/<c>"Release"</c> — ignored for every other Kind (`WP 9.0B`).</param>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is not one of <see cref="SupportedKinds"/>, <paramref name="displayName"/> is null/empty/whitespace, or <paramref name="kind"/> is <c>"SubAssembly"</c> and <paramref name="parentId"/> is <see langword="null"/> (a Sub-Assembly is, by definition, nested within a parent Assembly).</exception>
    public async Task<IEngineeringObject> CreateAsync(
        string kind, string? identifier, string displayName, string initialContent, Guid? parentId,
        IReadOnlyList<ConfigurationMember>? memberRevisions = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(initialContent);

        if (kind == "SubAssembly" && parentId is null)
            throw new ArgumentException("A Sub-Assembly requires a parent Assembly Id — it is, by definition, nested within one.", nameof(parentId));

        IEngineeringObject created = kind switch
        {
            "Project" => await new EngineeringObjectFactory<Project>(
                "Project", _context, (doc, rev) => new Project(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            "Assembly" => await new EngineeringObjectFactory<Assembly>(
                "Assembly", _context, (doc, rev) => new Assembly(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            "SubAssembly" => await new EngineeringObjectFactory<SubAssembly>(
                "SubAssembly", _context, (doc, rev) => new SubAssembly(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty, parentId!.Value))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            "Part" => await new EngineeringObjectFactory<Part>(
                "Part", _context, (doc, rev) => new Part(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            "Component" => await new EngineeringObjectFactory<Component>(
                "Component", _context, (doc, rev) => new Component(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            "Configuration" => await new EngineeringObjectFactory<Configuration>(
                "Configuration", _context, (doc, rev) => new Configuration(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty, memberRevisions))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            "Baseline" => await new EngineeringObjectFactory<Baseline>(
                "Baseline", _context, (doc, rev) => new Baseline(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty, memberRevisions))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            "Release" => await new EngineeringObjectFactory<Release>(
                "Release", _context, (doc, rev) => new Release(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty, memberRevisions))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            _ => throw new ArgumentException($"'{kind}' is not a supported Mechanical Product Structure Kind — expected one of: {string.Join(", ", SupportedKinds)}.", nameof(kind)),
        };

        // SubAssembly already has its own live ParentId set implicitly? No —
        // IHasParent.ParentId only ever changes via MoveAsync, so every Kind,
        // SubAssembly included, still needs an explicit Move to record it.
        if (parentId is { } pid && created is IHasParent hasParent)
            await hasParent.MoveAsync(pid, cancellationToken).ConfigureAwait(false);

        return created;
    }
}
