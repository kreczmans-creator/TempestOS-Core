using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Modules;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module demonstrating the Engineering Domain implementation
/// (WP 8.2C) — the "representative engineering object graph" the Work Package's own Definition of
/// Done names. During initialisation it builds a fictional, cross-family graph entirely through the
/// generic <see cref="EngineeringObjectFactory{T}"/>/<see cref="EngineeringObjectBase"/> machinery:
/// a Programme Hierarchy (Portfolio → Programme → Project), a Physical &amp; Configuration composition
/// (Assembly → Sub-Assembly → Part), a Part referencing a real, cross-framework <see cref="IMaterialSpecification"/>,
/// a Document (Drawing) documenting the Assembly, a Supply Chain pair (Supplier → Purchase Item), a
/// Governance &amp; Risk object (Risk) related to the Project, a Process &amp; Approval pair (Task → Milestone
/// → Deliverable), a Change &amp; Release pair (Change Request → Engineering Change), and an External
/// System Link — sixteen objects across eight of the eleven families this Work Package gives concrete
/// classes to. Exercises lifecycle
/// transition, revision, relationship, traceability, and dependency-traversal behaviour directly against
/// the shared Domain services, never against any of its own private state.
/// </summary>
/// <remarks>
/// Carries <see cref="ModuleMetadataAttribute"/> so Discovery can read its identity without instantiating
/// it (ADR-0027). Builds its own <see cref="EngineeringObjectFactory{T}"/> instances directly, in its own
/// composition root — never through a registry, per WP8.2B Dependency Rules.md §8 (no such registry
/// contract exists) — mirroring <c>Program.cs</c>'s own identical, disclosed precedent (ADR-0071).
/// Establishes its own principal (<see cref="SampleIdentityId"/>), never depending on another sample
/// module having already run, mirroring <see cref="RequirementsSampleModule"/>'s own identical precedent.
/// </remarks>
[ModuleMetadata("tempest.samples.engineeringdomain", "Engineering Domain Sample", "1.0.0")]
public sealed class EngineeringDomainSampleModule : ModuleLifecycleBase
{
    /// <summary>The identity id this module establishes as current during its own initialisation.</summary>
    public const string SampleIdentityId = "sample.engineeringdomain-user";

    /// <summary>The <see cref="CommandDescriptor.Id"/> this module registers for <see cref="GetSampleEngineeringDomainGraphSummaryCommand"/>.</summary>
    public const string GetGraphSummaryCommandId = "sample.engineeringdomain-graph-summary";

    private readonly IIdentityService _identityService;
    private readonly EngineeringDomainContext _context;
    private readonly IMaterialCatalog _materialCatalog;
    private readonly IDependencyTraversal _dependencyTraversal;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;

    public EngineeringDomainSampleModule(
        IIdentityService identityService,
        EngineeringDomainContext context,
        IMaterialCatalog materialCatalog,
        IDependencyTraversal dependencyTraversal,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.engineeringdomain", "Engineering Domain Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(materialCatalog);
        ArgumentNullException.ThrowIfNull(dependencyTraversal);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _identityService = identityService;
        _context = context;
        _materialCatalog = materialCatalog;
        _dependencyTraversal = dependencyTraversal;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    public Guid? SampleProjectId { get; private set; }
    public Guid? SampleAssemblyId { get; private set; }
    public Guid? SamplePartId { get; private set; }
    public IReadOnlyList<Guid> AllSampleObjectIds { get; private set; } = Array.Empty<Guid>();
    public bool HasRegistered { get; private set; }

    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _identityService.EstablishCurrentPrincipal(SampleIdentityId);

        var objectIds = new List<Guid>();

        // Programme Hierarchy
        var portfolioFactory = new EngineeringObjectFactory<Portfolio>(
            "Portfolio", _context, (doc, rev) => new Portfolio(doc, rev, _context, "SAMPLE-PORT-001", "Sample Portfolio", EngineeringObjectMetadata.Empty));
        var portfolio = (Portfolio)await portfolioFactory.CreateAsync("Fictional sample portfolio — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(portfolio.Id);

        var programmeFactory = new EngineeringObjectFactory<Programme>(
            "Programme", _context, (doc, rev) => new Programme(doc, rev, _context, "SAMPLE-PROG-001", "Sample Programme", EngineeringObjectMetadata.Empty, portfolio.Id));
        var programme = (Programme)await programmeFactory.CreateAsync("Fictional sample programme — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(programme.Id);
        await portfolio.LinkAsync(programme.Id, "groupedUnder", cancellationToken).ConfigureAwait(false);

        var projectFactory = new EngineeringObjectFactory<Project>(
            "Project", _context, (doc, rev) => new Project(doc, rev, _context, "SAMPLE-PROJ-001", "Sample Project", EngineeringObjectMetadata.Empty, programme.Id));
        var project = (Project)await projectFactory.CreateAsync("Fictional sample project — for demonstration only.", cancellationToken).ConfigureAwait(false);
        SampleProjectId = project.Id;
        objectIds.Add(project.Id);
        await programme.LinkAsync(project.Id, "groupedUnder", cancellationToken).ConfigureAwait(false);

        // Physical & Configuration
        var assemblyFactory = new EngineeringObjectFactory<Assembly>(
            "Assembly", _context, (doc, rev) => new Assembly(doc, rev, _context, "SAMPLE-ASM-001", "Sample Assembly", EngineeringObjectMetadata.Empty));
        var assembly = (Assembly)await assemblyFactory.CreateAsync("Fictional sample assembly — for demonstration only.", cancellationToken).ConfigureAwait(false);
        SampleAssemblyId = assembly.Id;
        objectIds.Add(assembly.Id);
        await project.LinkAsync(assembly.Id, "relatedTo", cancellationToken).ConfigureAwait(false);
        await assembly.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        await assembly.TransitionAsync(LifecycleState.Approved, cancellationToken).ConfigureAwait(false);

        var subAssemblyFactory = new EngineeringObjectFactory<SubAssembly>(
            "SubAssembly", _context, (doc, rev) => new SubAssembly(doc, rev, _context, "SAMPLE-SUBASM-001", "Sample Sub-Assembly", EngineeringObjectMetadata.Empty, assembly.Id));
        var subAssembly = (SubAssembly)await subAssemblyFactory.CreateAsync("Fictional sample sub-assembly — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(subAssembly.Id);
        await assembly.LinkAsync(subAssembly.Id, "groupedUnder", cancellationToken).ConfigureAwait(false);

        var materialSpecification = await _materialCatalog.RegisterAsync(
            "SAMPLE-MAT-001", "Fictional Sample Alloy", new Dictionary<string, MaterialProperty>(), category: "metal", cancellationToken)
            .ConfigureAwait(false);

        var partFactory = new EngineeringObjectFactory<Part>(
            "Part", _context, (doc, rev) => new Part(doc, rev, _context, "SAMPLE-PART-001", "Sample Part", EngineeringObjectMetadata.Empty, materialSpecification.MaterialId));
        var part = (Part)await partFactory.CreateAsync("Fictional sample part — for demonstration only.", cancellationToken).ConfigureAwait(false);
        SamplePartId = part.Id;
        objectIds.Add(part.Id);
        await subAssembly.LinkAsync(part.Id, "groupedUnder", cancellationToken).ConfigureAwait(false);
        await part.LinkAsync(materialSpecification.UnderlyingDocumentId, "references", cancellationToken).ConfigureAwait(false);
        await part.ReviseAsync("Fictional sample part, revised — for demonstration only.", "Sample revision.", cancellationToken).ConfigureAwait(false);

        // Documentation & Design
        var drawingFactory = new EngineeringObjectFactory<Drawing>(
            "Drawing", _context, (doc, rev) => new Drawing(doc, rev, _context, "SAMPLE-DWG-001", "Sample Drawing", EngineeringObjectMetadata.Empty, "DWG-001"));
        var drawing = (Drawing)await drawingFactory.CreateAsync("Fictional sample drawing — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(drawing.Id);
        await assembly.LinkAsync(drawing.Id, "documentedBy", cancellationToken).ConfigureAwait(false);

        // Supply Chain
        var supplierFactory = new EngineeringObjectFactory<Supplier>(
            "Supplier", _context, (doc, rev) => new Supplier(doc, rev, _context, "SAMPLE-SUP-001", "Sample Supplier", EngineeringObjectMetadata.Empty));
        var supplier = (Supplier)await supplierFactory.CreateAsync("Fictional sample supplier — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(supplier.Id);

        var purchaseItemFactory = new EngineeringObjectFactory<PurchaseItem>(
            "PurchaseItem", _context, (doc, rev) => new PurchaseItem(doc, rev, _context, "SAMPLE-PUR-001", "Sample Purchase Item", EngineeringObjectMetadata.Empty, supplier.Id, part.Id));
        var purchaseItem = (PurchaseItem)await purchaseItemFactory.CreateAsync("Fictional sample purchase item — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(purchaseItem.Id);
        await purchaseItem.LinkAsync(supplier.Id, "manufacturedBy", cancellationToken).ConfigureAwait(false);
        await purchaseItem.LinkAsync(part.Id, "references", cancellationToken).ConfigureAwait(false);

        // Governance & Risk
        var riskFactory = new EngineeringObjectFactory<Risk>(
            "Risk", _context, (doc, rev) => new Risk(doc, rev, _context, "SAMPLE-RISK-001", "Sample Risk", EngineeringObjectMetadata.Empty, likelihood: "Low", severity: "Medium"));
        var risk = (Risk)await riskFactory.CreateAsync("Fictional sample risk — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(risk.Id);
        await risk.LinkAsync(project.Id, "relatedTo", cancellationToken).ConfigureAwait(false);

        // Process & Approval
        var taskFactory = new EngineeringObjectFactory<EngineeringTask>(
            "Task", _context, (doc, rev) => new EngineeringTask(doc, rev, _context, "SAMPLE-TASK-001", "Sample Task", EngineeringObjectMetadata.Empty, SampleIdentityId));
        var task = (EngineeringTask)await taskFactory.CreateAsync("Fictional sample task — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(task.Id);
        await task.LinkAsync(risk.Id, "relatedTo", cancellationToken).ConfigureAwait(false);

        var milestoneFactory = new EngineeringObjectFactory<Milestone>(
            "Milestone", _context, (doc, rev) => new Milestone(doc, rev, _context, "SAMPLE-MS-001", "Sample Milestone", EngineeringObjectMetadata.Empty, DateTimeOffset.UtcNow.AddMonths(3)));
        var milestone = (Milestone)await milestoneFactory.CreateAsync("Fictional sample milestone — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(milestone.Id);

        var deliverableFactory = new EngineeringObjectFactory<Deliverable>(
            "Deliverable", _context, (doc, rev) => new Deliverable(doc, rev, _context, "SAMPLE-DEL-001", "Sample Deliverable", EngineeringObjectMetadata.Empty, milestone.Id));
        var deliverable = (Deliverable)await deliverableFactory.CreateAsync("Fictional sample deliverable — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(deliverable.Id);
        await deliverable.LinkAsync(milestone.Id, "relatedTo", cancellationToken).ConfigureAwait(false);

        // Change & Release
        var changeRequestFactory = new EngineeringObjectFactory<ChangeRequest>(
            "ChangeRequest", _context, (doc, rev) => new ChangeRequest(doc, rev, _context, "SAMPLE-CR-001", "Sample Change Request", EngineeringObjectMetadata.Empty));
        var changeRequest = (ChangeRequest)await changeRequestFactory.CreateAsync("Fictional sample change request — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(changeRequest.Id);
        await changeRequest.LinkAsync(assembly.Id, "relatedTo", cancellationToken).ConfigureAwait(false);

        var engineeringChangeFactory = new EngineeringObjectFactory<EngineeringChange>(
            "EngineeringChange", _context, (doc, rev) => new EngineeringChange(doc, rev, _context, "SAMPLE-EC-001", "Sample Engineering Change", EngineeringObjectMetadata.Empty, changeRequest.Id));
        var engineeringChange = (EngineeringChange)await engineeringChangeFactory.CreateAsync("Fictional sample engineering change — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(engineeringChange.Id);
        await engineeringChange.LinkAsync(changeRequest.Id, "derivesFrom", cancellationToken).ConfigureAwait(false);

        // Evidence & Reference
        var externalSystemLinkFactory = new EngineeringObjectFactory<ExternalSystemLink>(
            "ExternalSystemLink", _context, (doc, rev) => new ExternalSystemLink(doc, rev, _context, "Sample External System Link", EngineeringObjectMetadata.Empty, "SampleExternalPlm", "EXT-00042"));
        var externalSystemLink = (ExternalSystemLink)await externalSystemLinkFactory.CreateAsync("Fictional sample external system link — for demonstration only.", cancellationToken).ConfigureAwait(false);
        objectIds.Add(externalSystemLink.Id);
        await assembly.LinkAsync(externalSystemLink.Id, "references", cancellationToken).ConfigureAwait(false);

        AllSampleObjectIds = objectIds;

        _commandDispatcher.RegisterHandler<GetSampleEngineeringDomainGraphSummaryCommand>(
            new GetSampleEngineeringDomainGraphSummaryCommandHandler(_dependencyTraversal, this));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: GetGraphSummaryCommandId,
            displayName: "Get Sample Engineering Domain Graph Summary",
            category: "Sample",
            description: "Traverses this module's own sample Engineering Domain object graph and summarises what was found, demonstrating IDependencyTraversal end to end.",
            createDefault: () => new GetSampleEngineeringDomainGraphSummaryCommand()));

        HasRegistered = true;
    }
}
