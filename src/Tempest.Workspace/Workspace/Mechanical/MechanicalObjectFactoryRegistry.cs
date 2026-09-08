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
/// <remarks>
/// **`WP 12.1B` (`ADR-0105`).** Every one of this class's own eight Kind
/// strings is now declared exactly once, as a named constant, on this
/// class — the discipline every consumer of a Mechanical Kind, in any
/// layer, is expected to reference rather than retype
/// (`Tempest.Desktop.Icons.IconRegistry`, sample modules, and this
/// class's own <see cref="SupportedKinds"/>/switch/factory-argument
/// sites all previously repeated each value as an inline literal — the
/// exact duplication `WP 12.1A`'s own investigation quantified). No
/// value, no behaviour, and no public signature changed — only where
/// each literal is declared.
/// </remarks>
public sealed class MechanicalObjectFactoryRegistry
{
    /// <summary>The <see cref="IEngineeringObject.Kind"/> for a Project.</summary>
    public const string Project = "Project";

    /// <summary>The <see cref="IEngineeringObject.Kind"/> for an Assembly.</summary>
    public const string Assembly = "Assembly";

    /// <summary>The <see cref="IEngineeringObject.Kind"/> for a Sub-Assembly — always nested within a parent Assembly.</summary>
    public const string SubAssembly = "SubAssembly";

    /// <summary>The <see cref="IEngineeringObject.Kind"/> for a Part.</summary>
    public const string Part = "Part";

    /// <summary>The <see cref="IEngineeringObject.Kind"/> for a Component.</summary>
    public const string Component = "Component";

    /// <summary>The <see cref="IEngineeringObject.Kind"/> for a Configuration.</summary>
    public const string Configuration = "Configuration";

    /// <summary>The <see cref="IEngineeringObject.Kind"/> for a Baseline.</summary>
    public const string Baseline = "Baseline";

    /// <summary>The <see cref="IEngineeringObject.Kind"/> for a Release.</summary>
    public const string Release = "Release";

    /// <summary>The Kinds this registry can construct.</summary>
    public static readonly IReadOnlyList<string> SupportedKinds =
        [Project, Assembly, SubAssembly, Part, Component, Configuration, Baseline, Release];

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="MechanicalObjectFactoryRegistry"/> class.</summary>
    public MechanicalObjectFactoryRegistry(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <summary>Creates a new object of <paramref name="kind"/>, moving it under <paramref name="parentId"/> if one is given.</summary>
    /// <param name="memberRevisions">Only meaningful for <see cref="Configuration"/>/<see cref="Baseline"/>/<see cref="Release"/> — ignored for every other Kind (`WP 9.0B`).</param>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is not one of <see cref="SupportedKinds"/>, <paramref name="displayName"/> is null/empty/whitespace, or <paramref name="kind"/> is <see cref="SubAssembly"/> and <paramref name="parentId"/> is <see langword="null"/> (a Sub-Assembly is, by definition, nested within a parent Assembly).</exception>
    public async Task<IEngineeringObject> CreateAsync(
        string kind, string? identifier, string displayName, string initialContent, Guid? parentId,
        IReadOnlyList<ConfigurationMember>? memberRevisions = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(initialContent);

        if (kind == SubAssembly && parentId is null)
            throw new ArgumentException("A Sub-Assembly requires a parent Assembly Id — it is, by definition, nested within one.", nameof(parentId));

        IEngineeringObject created = kind switch
        {
            Project => await new EngineeringObjectFactory<Tempest.Core.EngineeringDomain.Project>(
                Project, _context, (doc, rev) => new Tempest.Core.EngineeringDomain.Project(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            Assembly => await new EngineeringObjectFactory<Tempest.Core.EngineeringDomain.Assembly>(
                Assembly, _context, (doc, rev) => new Tempest.Core.EngineeringDomain.Assembly(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            SubAssembly => await new EngineeringObjectFactory<Tempest.Core.EngineeringDomain.SubAssembly>(
                SubAssembly, _context, (doc, rev) => new Tempest.Core.EngineeringDomain.SubAssembly(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty, parentId!.Value))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            Part => await new EngineeringObjectFactory<Tempest.Core.EngineeringDomain.Part>(
                Part, _context, (doc, rev) => new Tempest.Core.EngineeringDomain.Part(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            Component => await new EngineeringObjectFactory<Tempest.Core.EngineeringDomain.Component>(
                Component, _context, (doc, rev) => new Tempest.Core.EngineeringDomain.Component(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            Configuration => await new EngineeringObjectFactory<Tempest.Core.EngineeringDomain.Configuration>(
                Configuration, _context, (doc, rev) => new Tempest.Core.EngineeringDomain.Configuration(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty, memberRevisions))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            Baseline => await new EngineeringObjectFactory<Tempest.Core.EngineeringDomain.Baseline>(
                Baseline, _context, (doc, rev) => new Tempest.Core.EngineeringDomain.Baseline(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty, memberRevisions))
                .CreateAsync(initialContent, cancellationToken).ConfigureAwait(false),

            Release => await new EngineeringObjectFactory<Tempest.Core.EngineeringDomain.Release>(
                Release, _context, (doc, rev) => new Tempest.Core.EngineeringDomain.Release(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty, memberRevisions))
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

    /// <summary>
    /// Registers how each of this discipline's own eight Kinds comes back
    /// after a restart (`TD-85`).
    /// </summary>
    /// <remarks>
    /// The rehydrating counterpart of <see cref="CreateAsync"/>, and
    /// deliberately in the same class: this registry is the canonical owner
    /// of these eight Kind strings (`ADR-0105`), so it is also the right
    /// place to say which type each of them comes back as. The reconstruction
    /// itself belongs to each type, in <c>Tempest.Core</c> — nothing about
    /// any type's own fields is known here.
    /// </remarks>
    public static void RegisterRehydrators(IEngineeringObjectRehydratorRegistry registry, EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(context);

        registry.Register<Tempest.Core.EngineeringDomain.Project>(Project, context);
        registry.Register<Tempest.Core.EngineeringDomain.Assembly>(Assembly, context);
        registry.Register<Tempest.Core.EngineeringDomain.SubAssembly>(SubAssembly, context);
        registry.Register<Tempest.Core.EngineeringDomain.Part>(Part, context);
        registry.Register<Tempest.Core.EngineeringDomain.Component>(Component, context);
        registry.Register<Tempest.Core.EngineeringDomain.Configuration>(Configuration, context);
        registry.Register<Tempest.Core.EngineeringDomain.Baseline>(Baseline, context);
        registry.Register<Tempest.Core.EngineeringDomain.Release>(Release, context);
    }
}
