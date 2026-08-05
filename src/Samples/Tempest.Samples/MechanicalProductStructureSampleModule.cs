using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Modules;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module demonstrating `WP 9.0A`'s
/// Mechanical Product Structure and `WP 9.0B`'s Product Configuration &amp;
/// BOM Management — real, representative engineering data for the
/// Engineering Workspace's own Mechanical area to present, per both Work
/// Packages' own explicit "meaningful engineering data rather than
/// placeholders" requirement.
/// </summary>
/// <remarks>
/// <para>
/// Builds one Project, two top-level Assemblies, a three-level-deep
/// Sub-Assembly chain, several Parts (each carrying a real
/// <see cref="IHasBomLine"/> — Quantity/Unit of Measure/Find Number/Item
/// Number, `WP 9.0B`), and one Component referenced (not parented —
/// <see cref="IHasParent.ParentId"/> is a single-parent tree edge, never a
/// DAG) from two different Parts across two different Assemblies — the
/// "shared component / cross reference" scenario, realised through the
/// existing, reused Relationship framework
/// (<see cref="IHasRelationships.LinkAsync"/>), never a second parent
/// pointer. The Component also carries a Reference Designator
/// (<c>"B1-B8"</c>), the one BOM field no Part needs.
/// </para>
/// <para>
/// Configuration Management (`WP 9.0B`): a plain, working
/// <see cref="Configuration"/> (left at Draft); a <see cref="Baseline"/>
/// frozen and taken to Approved; a later, larger-membership
/// <see cref="Release"/> taken all the way to <see cref="LifecycleState.Released"/>
/// — the Release's own member set differs from the Baseline's in both an
/// added member (a Part created after the Baseline was frozen) and a
/// revision-changed member (a Part revised after the Baseline was frozen),
/// so comparing the two (<c>CompareBaselinesCommand</c>) shows a real,
/// non-trivial diff — "multiple configurations"/"baseline
/// comparisons"/"revision examples", all real, none placeholder. Display/
/// baseline-awareness only, no configuration management workflow, exactly
/// as both Work Packages' own controlling instructions require.
/// </para>
/// <para>
/// Exercises every `WP 9.0A`/`WP 9.0B` additive facet directly against the
/// shared Domain services — <see cref="IRenamable.RenameAsync"/>,
/// <see cref="IHasParent.MoveAsync"/>, <see cref="IDeletable.DeleteAsync"/>,
/// <see cref="IHasBomLine.SetBomLineAsync"/>, and (via the revised Part
/// above) <see cref="IHasRevisions.ReviseAsync"/> itself, proving `WP 9.0B`'s
/// own <c>EngineeringObjectBase.ReviseAsync</c> structural-state-copy fix
/// directly, not just in a unit test — so the representative data itself
/// proves every new capability works, mirroring
/// <see cref="EngineeringDomainSampleModule"/>'s own identical "exercise
/// real behaviour, not just construct data" precedent. Registers all five
/// new `WP 9.0B` <see cref="IValidationRule"/>s against the shared
/// <see cref="ValidationRuleSet"/> — the same extension point
/// `EngineeringDomainSampleModule`'s own sibling Work Package left unused.
/// <see cref="CopyMechanicalObjectCommand"/>/<see cref="DuplicateMechanicalObjectCommand"/>
/// are deliberately not exercised here — both live in <c>Tempest.App</c>,
/// which this project (<c>Tempest.Samples</c>) is never referenced by
/// (`Tempest.App` depends on `Tempest.Samples`, never the reverse); they are
/// covered instead by dedicated Workspace command tests.
/// </para>
/// <para>
/// Carries <see cref="ModuleMetadataAttribute"/> so Discovery can read its
/// identity without instantiating it (ADR-0027). Builds its own
/// <see cref="EngineeringObjectFactory{T}"/> instances directly, in its own
/// composition root — never through a registry, per `WP8.2B Dependency
/// Rules.md` §8 — mirroring <see cref="EngineeringDomainSampleModule"/>'s
/// own identical, disclosed precedent (ADR-0071). Establishes its own
/// principal (<see cref="SampleIdentityId"/>), never depending on another
/// sample module having already run.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.mechanicalproductstructure", "Mechanical Product Structure Sample", "1.0.0")]
public sealed class MechanicalProductStructureSampleModule : ModuleLifecycleBase
{
    /// <summary>The identity id this module establishes as current during its own initialisation.</summary>
    public const string SampleIdentityId = "sample.mechanicalproductstructure-user";

    private readonly IIdentityService _identityService;
    private readonly EngineeringDomainContext _context;

    public MechanicalProductStructureSampleModule(IIdentityService identityService, EngineeringDomainContext context)
        : base("tempest.samples.mechanicalproductstructure", "Mechanical Product Structure Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(context);

        _identityService = identityService;
        _context = context;
    }

    public Guid? ProjectId { get; private set; }
    public Guid? WingAssemblyId { get; private set; }
    public Guid? EmpennageAssemblyId { get; private set; }
    public Guid? SharedFastenerComponentId { get; private set; }
    public Guid? DeletedPartId { get; private set; }
    public Guid? SparWebPlateId { get; private set; }
    public Guid? SparCapPartId { get; private set; }
    public Guid? WorkingConfigurationId { get; private set; }
    public Guid? BaselineId { get; private set; }
    public Guid? ReleaseId { get; private set; }
    public IReadOnlyList<Guid> AllSampleObjectIds { get; private set; } = Array.Empty<Guid>();
    public bool HasRegistered { get; private set; }

    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _identityService.EstablishCurrentPrincipal(SampleIdentityId);

        var objectIds = new List<Guid>();

        // ---- Project (the root of this Work Package's own scope) ----
        var projectFactory = new EngineeringObjectFactory<Project>(
            "Project", _context, (doc, rev) => new Project(doc, rev, _context, "MECH-PROJ-001", "Falcon Structural Assembly Project", EngineeringObjectMetadata.Empty));
        var project = (Project)await projectFactory.CreateAsync("A fictional airframe structural product — for demonstration only.", cancellationToken).ConfigureAwait(false);
        ProjectId = project.Id;
        objectIds.Add(project.Id);

        // ---- Wing Assembly branch: Assembly -> SubAssembly -> SubAssembly -> Part (deep hierarchy) ----
        var wingAssemblyFactory = new EngineeringObjectFactory<Assembly>(
            "Assembly", _context, (doc, rev) => new Assembly(doc, rev, _context, "MECH-ASM-001", "Wing Assembly", EngineeringObjectMetadata.Empty));
        var wingAssembly = (Assembly)await wingAssemblyFactory.CreateAsync("Fictional wing structural assembly — for demonstration only.", cancellationToken).ConfigureAwait(false);
        WingAssemblyId = wingAssembly.Id;
        objectIds.Add(wingAssembly.Id);
        await wingAssembly.MoveAsync(project.Id, cancellationToken).ConfigureAwait(false);

        // Real-data lifecycle: Draft -> InReview -> Approved -> Released, demonstrating "Released state display".
        await wingAssembly.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        await wingAssembly.TransitionAsync(LifecycleState.Approved, cancellationToken).ConfigureAwait(false);
        await wingAssembly.TransitionAsync(LifecycleState.Released, cancellationToken).ConfigureAwait(false);

        var sparSubAssemblyFactory = new EngineeringObjectFactory<SubAssembly>(
            "SubAssembly", _context, (doc, rev) => new SubAssembly(doc, rev, _context, "MECH-SUBASM-001", "Wing Spar Sub-Assembly", EngineeringObjectMetadata.Empty, wingAssembly.Id));
        var sparSubAssembly = (SubAssembly)await sparSubAssemblyFactory.CreateAsync("Fictional wing spar sub-assembly — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(sparSubAssembly.Id);
        await sparSubAssembly.MoveAsync(wingAssembly.Id, cancellationToken).ConfigureAwait(false);
        await sparSubAssembly.SetBomLineAsync(1m, "EA", findNumber: "1", itemNumber: "0010", cancellationToken: cancellationToken).ConfigureAwait(false);

        var rootFittingSubAssemblyFactory = new EngineeringObjectFactory<SubAssembly>(
            "SubAssembly", _context, (doc, rev) => new SubAssembly(doc, rev, _context, "MECH-SUBASM-002", "Spar Root Fitting Sub-Assembly", EngineeringObjectMetadata.Empty, sparSubAssembly.Id));
        var rootFittingSubAssembly = (SubAssembly)await rootFittingSubAssemblyFactory.CreateAsync("Fictional spar root fitting sub-assembly, nested a second level deep — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(rootFittingSubAssembly.Id);
        await rootFittingSubAssembly.MoveAsync(sparSubAssembly.Id, cancellationToken).ConfigureAwait(false);
        await rootFittingSubAssembly.SetBomLineAsync(1m, "EA", findNumber: "2", itemNumber: "0010", cancellationToken: cancellationToken).ConfigureAwait(false);

        var rootFittingLugFactory = new EngineeringObjectFactory<Part>(
            "Part", _context, (doc, rev) => new Part(doc, rev, _context, "MECH-PART-001", "Root Fitting Lug", EngineeringObjectMetadata.Empty));
        var rootFittingLug = (Part)await rootFittingLugFactory.CreateAsync("Fictional root fitting lug — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(rootFittingLug.Id);
        await rootFittingLug.MoveAsync(rootFittingSubAssembly.Id, cancellationToken).ConfigureAwait(false);
        await rootFittingLug.SetBomLineAsync(2m, "EA", findNumber: "3", itemNumber: "0010", cancellationToken: cancellationToken).ConfigureAwait(false);

        var sparWebPlateFactory = new EngineeringObjectFactory<Part>(
            "Part", _context, (doc, rev) => new Part(doc, rev, _context, "MECH-PART-002", "Spar Web Plate", EngineeringObjectMetadata.Empty));
        var sparWebPlate = (Part)await sparWebPlateFactory.CreateAsync("Fictional spar web plate — for demonstration only.", cancellationToken).ConfigureAwait(false);
        SparWebPlateId = sparWebPlate.Id;
        objectIds.Add(sparWebPlate.Id);
        await sparWebPlate.MoveAsync(sparSubAssembly.Id, cancellationToken).ConfigureAwait(false);
        await sparWebPlate.SetBomLineAsync(1m, "EA", findNumber: "4", itemNumber: "0020", cancellationToken: cancellationToken).ConfigureAwait(false);

        var wingSkinPanelFactory = new EngineeringObjectFactory<Part>(
            "Part", _context, (doc, rev) => new Part(doc, rev, _context, "MECH-PART-003", "Wing Skin Panel", EngineeringObjectMetadata.Empty));
        var wingSkinPanel = (Part)await wingSkinPanelFactory.CreateAsync("Fictional wing skin panel — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(wingSkinPanel.Id);
        await wingSkinPanel.MoveAsync(wingAssembly.Id, cancellationToken).ConfigureAwait(false);
        await wingSkinPanel.SetBomLineAsync(4m, "EA", findNumber: "5", itemNumber: "0010", cancellationToken: cancellationToken).ConfigureAwait(false);

        // IRenamable, exercised directly against real data. Also proves
        // WP 9.0B's own ReviseAsync structural-state-copy fix indirectly:
        // this object is never itself revised, so its BOM line survives by
        // construction either way — the genuinely revised object below
        // (sparWebPlate) is the real proof.
        await wingSkinPanel.RenameAsync("Wing Skin Panel — Forward", cancellationToken).ConfigureAwait(false);

        // ---- Empennage Assembly branch (second top-level Assembly — "multiple assemblies") ----
        var empennageAssemblyFactory = new EngineeringObjectFactory<Assembly>(
            "Assembly", _context, (doc, rev) => new Assembly(doc, rev, _context, "MECH-ASM-002", "Empennage Assembly", EngineeringObjectMetadata.Empty));
        var empennageAssembly = (Assembly)await empennageAssemblyFactory.CreateAsync("Fictional empennage (tail) structural assembly — for demonstration only.", cancellationToken).ConfigureAwait(false);
        EmpennageAssemblyId = empennageAssembly.Id;
        objectIds.Add(empennageAssembly.Id);
        await empennageAssembly.MoveAsync(project.Id, cancellationToken).ConfigureAwait(false);

        var stabiliserRibFactory = new EngineeringObjectFactory<Part>(
            "Part", _context, (doc, rev) => new Part(doc, rev, _context, "MECH-PART-004", "Stabiliser Rib", EngineeringObjectMetadata.Empty));
        var stabiliserRib = (Part)await stabiliserRibFactory.CreateAsync("Fictional stabiliser rib — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(stabiliserRib.Id);

        // IHasParent.MoveAsync, exercised directly: created without a parent
        // in mind yet, first placed under the wrong Assembly, then moved to
        // the correct one — real reparenting against real data, and a full
        // "groupedUnder" move history over both links (never destructively
        // overwritten).
        await stabiliserRib.MoveAsync(wingAssembly.Id, cancellationToken).ConfigureAwait(false);
        await stabiliserRib.MoveAsync(empennageAssembly.Id, cancellationToken).ConfigureAwait(false);
        await stabiliserRib.SetBomLineAsync(1m, "EA", findNumber: "6", itemNumber: "0010", cancellationToken: cancellationToken).ConfigureAwait(false);

        // ---- Shared Component, referenced (not parented) from two Parts across two Assemblies ----
        var fastenerFactory = new EngineeringObjectFactory<Component>(
            "Component", _context, (doc, rev) => new Component(doc, rev, _context, "MECH-COMP-001", "Standard Fastener — M6 Bolt", EngineeringObjectMetadata.Empty));
        var fastener = (Component)await fastenerFactory.CreateAsync("Fictional standard fastener — for demonstration only.", cancellationToken).ConfigureAwait(false);
        SharedFastenerComponentId = fastener.Id;
        objectIds.Add(fastener.Id);
        await fastener.MoveAsync(wingAssembly.Id, cancellationToken).ConfigureAwait(false);
        await fastener.SetBomLineAsync(8m, "EA", findNumber: "12", itemNumber: "0040", referenceDesignator: "B1-B8", cancellationToken: cancellationToken).ConfigureAwait(false);

        // Cross-reference: two different Parts, in two different Assemblies,
        // both reference the same Component — "Support: References, Related
        // objects", reused from the existing Relationship framework.
        await sparWebPlate.LinkAsync(fastener.Id, "references", cancellationToken).ConfigureAwait(false);
        await stabiliserRib.LinkAsync(fastener.Id, "references", cancellationToken).ConfigureAwait(false);

        // ---- IDeletable, exercised directly: a superseded Part, soft-deleted ----
        var deprecatedBracketFactory = new EngineeringObjectFactory<Part>(
            "Part", _context, (doc, rev) => new Part(doc, rev, _context, "MECH-PART-005", "Deprecated Bracket", EngineeringObjectMetadata.Empty));
        var deprecatedBracket = (Part)await deprecatedBracketFactory.CreateAsync("Fictional deprecated bracket, retained only to demonstrate soft delete — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(deprecatedBracket.Id);
        await deprecatedBracket.MoveAsync(empennageAssembly.Id, cancellationToken).ConfigureAwait(false);
        await deprecatedBracket.DeleteAsync(cancellationToken).ConfigureAwait(false);
        DeletedPartId = deprecatedBracket.Id;

        // ---- Configuration Management: working set, an Approved Baseline, and a Released Release ----
        // "Working configuration" (WP 9.0B): a plain Configuration, left at
        // Draft — display/baseline-awareness only, no workflow.
        var workingConfigurationFactory = new EngineeringObjectFactory<Configuration>(
            "Configuration", _context, (doc, rev) => new Configuration(
                doc, rev, _context, "MECH-CFG-001", "Wing Assembly Working Set", EngineeringObjectMetadata.Empty,
                new[]
                {
                    new ConfigurationMember(wingAssembly.Id, wingAssembly.CurrentRevisionNumber),
                    new ConfigurationMember(sparWebPlate.Id, sparWebPlate.CurrentRevisionNumber),
                }));
        var workingConfiguration = (Configuration)await workingConfigurationFactory.CreateAsync("Fictional working configuration — for demonstration only.", cancellationToken).ConfigureAwait(false);
        WorkingConfigurationId = workingConfiguration.Id;
        objectIds.Add(workingConfiguration.Id);

        // "Baseline" (WP 9.0B): the same member set, frozen and Approved —
        // reuses the already-real, already-tested WP8.2C Baseline : Configuration class.
        var baselineFactory = new EngineeringObjectFactory<Baseline>(
            "Baseline", _context, (doc, rev) => new Baseline(
                doc, rev, _context, "MECH-BASE-001", "Wing Assembly Baseline — Rev A", EngineeringObjectMetadata.Empty,
                new[]
                {
                    new ConfigurationMember(wingAssembly.Id, wingAssembly.CurrentRevisionNumber),
                    new ConfigurationMember(sparWebPlate.Id, sparWebPlate.CurrentRevisionNumber),
                }));
        var baseline = (Baseline)await baselineFactory.CreateAsync("Fictional baseline, Rev A — for demonstration only.", cancellationToken).ConfigureAwait(false);
        BaselineId = baseline.Id;
        objectIds.Add(baseline.Id);
        await baseline.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        await baseline.TransitionAsync(LifecycleState.Approved, cancellationToken).ConfigureAwait(false);

        // A new Part, added to the structure *after* the Baseline above was
        // frozen — deliberately absent from it, present in the Release
        // below, so comparing the two shows a real "added" member.
        var sparCapFactory = new EngineeringObjectFactory<Part>(
            "Part", _context, (doc, rev) => new Part(doc, rev, _context, "MECH-PART-006", "Spar Cap", EngineeringObjectMetadata.Empty));
        var sparCap = (Part)await sparCapFactory.CreateAsync("Fictional spar cap, added after the baseline above — for demonstration only.", cancellationToken).ConfigureAwait(false);
        SparCapPartId = sparCap.Id;
        objectIds.Add(sparCap.Id);
        await sparCap.MoveAsync(sparSubAssembly.Id, cancellationToken).ConfigureAwait(false);
        await sparCap.SetBomLineAsync(2m, "EA", findNumber: "7", itemNumber: "0030", cancellationToken: cancellationToken).ConfigureAwait(false);

        // sparWebPlate revised *after* the Baseline above was frozen —
        // "Revision examples": comparing Baseline vs. Release below shows a
        // real revision-changed member, not just an added/removed one.
        // Exercises WP 9.0B's own ReviseAsync structural-state-copy fix
        // directly: without it, the revised instance below would silently
        // lose its own BOM line (Item 0020) and current ParentId, reverting
        // to construction-time defaults.
        var revisedSparWebPlate = (Part)await sparWebPlate.ReviseAsync(
            "Revised fastener hole pattern for the updated bolt spec — for demonstration only.", "Rev B — hole pattern update.", cancellationToken)
            .ConfigureAwait(false);
        objectIds.Add(revisedSparWebPlate.Id); // Same Id as sparWebPlate — recorded once more for clarity, not a new object.

        // "Released configuration" (WP 9.0B): a later, larger member set,
        // frozen and taken all the way to Released.
        var releaseFactory = new EngineeringObjectFactory<Release>(
            "Release", _context, (doc, rev) => new Release(
                doc, rev, _context, "MECH-REL-001", "Wing Assembly Release — R1", EngineeringObjectMetadata.Empty,
                new[]
                {
                    new ConfigurationMember(wingAssembly.Id, wingAssembly.CurrentRevisionNumber),
                    new ConfigurationMember(revisedSparWebPlate.Id, revisedSparWebPlate.CurrentRevisionNumber),
                    new ConfigurationMember(sparCap.Id, sparCap.CurrentRevisionNumber),
                }));
        var release = (Release)await releaseFactory.CreateAsync("Fictional release, R1 — for demonstration only.", cancellationToken).ConfigureAwait(false);
        ReleaseId = release.Id;
        objectIds.Add(release.Id);
        await release.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        await release.TransitionAsync(LifecycleState.Approved, cancellationToken).ConfigureAwait(false);
        await release.TransitionAsync(LifecycleState.Released, cancellationToken).ConfigureAwait(false);

        // ---- WP 9.0B validation rules — the same ValidationRuleSet.Register
        // extension point EngineeringDomainSampleModule's own sibling
        // Work Package left unused; registered here, once, at composition time. ----
        var validationRuleSet = (ValidationRuleSet)_context.ValidationRuleSet;
        validationRuleSet.Register(new InvalidQuantityValidationRule());
        validationRuleSet.Register(new MissingParentValidationRule(_context.Repository));
        validationRuleSet.Register(new CircularHierarchyValidationRule(_context.Repository));
        validationRuleSet.Register(new DuplicateItemNumberValidationRule(_context.Repository));
        validationRuleSet.Register(new DuplicateFindNumberValidationRule(_context.Repository));

        AllSampleObjectIds = objectIds;
        HasRegistered = true;
    }
}
