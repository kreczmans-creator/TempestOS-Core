using Tempest.Workspace;
using Tempest.Workspace.Composition;
using Tempest.Workspace.Manufacturing;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Verification;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `WP 18.1B` §5: placement and open-right-up as rules of the store and the
/// declaration, not of each screen. One table-driven test over every
/// discipline's own create command — Mechanical, Documents, Calculations,
/// Requirements, Manufacturing, Verification, Evidence. Four
/// (Mechanical/Documents/Calculations/Evidence) resolve their parent
/// through <see cref="CreationPlacement.ParentFor"/>; three
/// (Requirements/Manufacturing/Verification) have their own, different,
/// already-correct placement rule with no Project/<c>ParentId</c> concept
/// at all — each documented at its own test method rather than routed,
/// after an initial attempt at routing Manufacturing and Verification
/// through <c>CreationPlacement</c> broke a real, existing acceptance test
/// (caught by the gate) by nesting a created object where its own explorer
/// tree can never find it again. Every row also confirms
/// <c>CommandResult.Success(message, id, kind)</c> so the shell can reveal
/// and open what was just made.
/// </summary>
public sealed class CreationPlacementAndOpenRightUpTests : IAsyncLifetime
{
    private TempDirectory _temp = null!;
    private ITempestHost _host = null!;
    private WorkspaceManager _manager = null!;
    private ICommandRegistry _registry = null!;
    private EngineeringDomainContext _domain = null!;

    public async Task InitializeAsync()
    {
        _temp = new TempDirectory();

        _host = new TempestHostBuilder([typeof(MechanicalWorkspaceExplorerModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, _temp.Path),
            ]))
            .Build();
        _manager = new WorkspaceManager(_host);

        await _manager.StartAsync();

        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(_manager, _host);

        _registry = (ICommandRegistry)_host.Services!.GetService(typeof(ICommandRegistry));
        _domain = (EngineeringDomainContext)_host.Services!.GetService(typeof(EngineeringDomainContext));
    }

    public async Task DisposeAsync()
    {
        await _manager.ShutdownAsync();
        await _host.DisposeAsync();
        _temp.Dispose();
    }

    private static CommandParameterPrompt Answering(IReadOnlyDictionary<string, string> values) =>
        (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(values);

    private async Task<Guid> CreateProjectAsync()
    {
        var factory = new EngineeringObjectFactory<Core.EngineeringDomain.Project>(
            MechanicalObjectFactoryRegistry.Project, _domain,
            (doc, rev) => new Core.EngineeringDomain.Project(doc, rev, _domain, "PLACE-PRJ", "Placement Test Project", EngineeringObjectMetadata.Empty));
        var project = await factory.CreateAsync("Creation placement test project.");
        return project.Id;
    }

    /// <summary>Creates a live Part under <paramref name="projectId"/>, for the two disciplines (Manufacturing, Verification) whose own create binding requires a selected subject.</summary>
    private async Task<Guid> CreatePartAsync(Guid projectId)
    {
        var context = new CommandContext([], projectId);
        var invocation = await _registry.InvokeAsync(
            "mechanical.create", context,
            Answering(new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = "Placement Subject Part" }));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        return invocation.Result!.SubjectId!.Value;
    }

    /// <summary>Asserts <paramref name="invocation"/> succeeded, named its subject's id and Kind, and that the created object's own structural parent is <paramref name="expectedParentId"/>.</summary>
    private async Task AssertCreatedAndPlacedUnderAsync(CommandInvocation invocation, string expectedKind, Guid expectedParentId)
    {
        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.True(invocation.Result!.Succeeded);
        Assert.NotNull(invocation.Result.SubjectId);
        Assert.Equal(expectedKind, invocation.Result.SubjectKind);

        var created = await _domain.Repository.FindAsync(invocation.Result.SubjectId!.Value);
        Assert.NotNull(created);
        Assert.Equal(expectedParentId, (created as IHasParent)?.ParentId);
    }

    // ----------------------------------------------------------------
    // The table: one row per discipline's own create command.
    // ----------------------------------------------------------------

    [Fact]
    public async Task Mechanical_NothingSelected_LandsUnderTheOpenProject()
    {
        var projectId = await CreateProjectAsync();
        var context = new CommandContext([], projectId);

        var invocation = await _registry.InvokeAsync(
            "mechanical.create", context,
            Answering(new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = "Bracket" }));

        await AssertCreatedAndPlacedUnderAsync(invocation, "Part", projectId);
    }

    [Fact]
    public async Task Documents_NothingSelected_LandsUnderTheOpenProject()
    {
        var projectId = await CreateProjectAsync();
        var context = new CommandContext([], projectId);

        var invocation = await _registry.InvokeAsync(
            "documents.create", context,
            Answering(new Dictionary<string, string> { ["kind"] = "Document", ["displayName"] = "Design Note" }));

        await AssertCreatedAndPlacedUnderAsync(invocation, "Document", projectId);
    }

    [Fact]
    public async Task Calculations_NothingSelected_LandsUnderTheOpenProject()
    {
        var projectId = await CreateProjectAsync();
        var context = new CommandContext([], projectId);

        var invocation = await _registry.InvokeAsync(
            "calculations.create", context,
            Answering(new Dictionary<string, string> { ["kind"] = "Calculation", ["displayName"] = "Bracket Check" }));

        await AssertCreatedAndPlacedUnderAsync(invocation, "Calculation", projectId);
    }

    /// <summary>
    /// Manufacturing does not use <see cref="CreationPlacement.ParentFor"/>
    /// either — verified in code (<c>ManufacturingNodeProvider</c>'s own
    /// remarks and its <c>GetRootNodesAsync</c>/<c>GetChildrenAsync</c>
    /// bodies), not assumed, after an initial attempt at routing this
    /// discipline through it broke a real, existing acceptance test
    /// (<c>FeatureCompletionTests.VerificationCreate_*</c>'s own sibling
    /// concern, caught by the gate) by nesting a created object under a
    /// Part/Project it can never be "drilled into" from. Every
    /// Manufacturing object is a category-pooled root
    /// (Routings/Operations/Supplier Operations/Work Instructions/
    /// Inspections, by <c>Classification</c> or Kind) when un-parented —
    /// <c>ParentId == null</c> is that discipline's own correct, complete
    /// placement rule, exactly like Requirements' <c>GroupId</c>. A
    /// created Operation is reachable here by drilling into its own
    /// "Operations" category, never by <c>CreationPlacement</c>.
    /// </summary>
    [Fact]
    public async Task Manufacturing_APartSelected_OperationIsUnparented_AndReachableUnderItsCategory()
    {
        var projectId = await CreateProjectAsync();
        var partId = await CreatePartAsync(projectId);
        var context = new CommandContext([new CommandContextObject(partId, "Part")], projectId);

        var invocation = await _registry.InvokeAsync(
            "manufacturing.create", context,
            Answering(new Dictionary<string, string>
            {
                ["kind"] = ManufacturingObjectFactoryRegistry.ManufacturingOperationKind,
                ["displayName"] = "Mill the pocket",
                ["method"] = "Inspection",
            }));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.True(invocation.Result!.Succeeded);
        var createdId = invocation.Result.SubjectId!.Value;
        Assert.Equal(ManufacturingObjectFactoryRegistry.ManufacturingOperationKind, invocation.Result.SubjectKind);

        var created = await _domain.Repository.FindAsync(createdId);
        Assert.Null((created as IHasParent)?.ParentId);

        var provider = new ManufacturingNodeProvider("manufacturing", _domain);
        var operationsCategory = (await provider.GetRootNodesAsync()).Single(n => n.Title == "Operations");
        var members = await provider.GetChildrenAsync(operationsCategory.Id);
        Assert.Contains(members, n => n.Id == createdId);
    }

    /// <summary>
    /// Verification does not use <see cref="CreationPlacement.ParentFor"/>
    /// either — the identical shape as Manufacturing's own finding, verified
    /// in code: <c>VerificationActivityNodeProvider</c> pools every
    /// un-parented activity under its own method-category root
    /// (Inspection/Analysis/Test/Demonstration/Other); <c>ParentId</c> stays
    /// <see langword="null"/> by design, and this Work Package's own
    /// original attempt at routing it broke
    /// <c>FeatureCompletionTests.VerificationCreate_UsesTheCurrentSelectionAsSubject_ActuallyCreatesARealActivity</c>
    /// by nesting the created activity under the Project selected as its
    /// own container fallback — invisible ever after, since that tree never
    /// drills into a Project. Reverted; documented here instead of routed.
    /// </summary>
    [Fact]
    public async Task Verification_ANonContainerSubjectSelected_IsUnparented_AndReachableUnderItsCategory()
    {
        var projectId = await CreateProjectAsync();
        var partId = await CreatePartAsync(projectId);
        // The Part is the required subject (what is being verified) — never
        // a placement concern for this discipline.
        var context = new CommandContext([new CommandContextObject(partId, "Part")], projectId);

        var invocation = await _registry.InvokeAsync(
            "verification.create", context,
            Answering(new Dictionary<string, string> { ["displayName"] = "Verify the pocket depth", ["method"] = "Inspection" }));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.True(invocation.Result!.Succeeded);
        var createdId = invocation.Result.SubjectId!.Value;
        Assert.Equal("VerificationActivity", invocation.Result.SubjectKind);

        var created = await _domain.Repository.FindAsync(createdId);
        Assert.Null((created as IHasParent)?.ParentId);

        var provider = new VerificationActivityNodeProvider("verification", _domain);
        var inspectionCategory = (await provider.GetRootNodesAsync()).Single(n => n.Title == "Inspection");
        var members = await provider.GetChildrenAsync(inspectionCategory.Id);
        Assert.Contains(members, n => n.Id == createdId);
    }

    [Fact]
    public async Task Evidence_NothingSelected_LandsUnderTheOpenProject()
    {
        var projectId = await CreateProjectAsync();
        var context = new CommandContext([], projectId);

        var invocation = await _registry.InvokeAsync(
            "evidence.create", context,
            Answering(new Dictionary<string, string> { ["title"] = "Bracket calc", ["classification"] = EvidenceClassification.Calculation.ToString() }));

        await AssertCreatedAndPlacedUnderAsync(invocation, Core.Evidence.Evidence.CanonicalKind, projectId);
    }

    /// <summary>
    /// Requirements does not use <see cref="CreationPlacement.ParentFor"/> —
    /// verified in code, not assumed: <c>Requirement</c> predates
    /// <c>EngineeringObjectBase</c>/<c>IHasParent</c> and is placed by
    /// <c>GroupId</c> alone, a structural model with no Project concept at
    /// all (a Requirement is global, not project-scoped). This is not a
    /// gap `WP 18.1B` §5 must route: a requirement created with no group
    /// selected is placed exactly where <c>WP 17.9.3</c> (`TD-172`) fixed
    /// it to be — reachable from the Explorer's own root via the fixed
    /// "Ungrouped" bucket, never orphaned — which is Requirements' own
    /// complete, documented placement rule.
    /// </summary>
    [Fact]
    public async Task Requirements_NothingSelected_IsReachableFromRoot()
    {
        var context = new CommandContext([]);

        var invocation = await _registry.InvokeAsync(
            "requirements.create", context,
            Answering(new Dictionary<string, string> { ["identifier"] = "REQ-PLACE-001", ["statement"] = "The bracket shall not yield." }));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.True(invocation.Result!.Succeeded);
        Assert.NotNull(invocation.Result.SubjectId);
        Assert.Equal(RequirementsService.RequirementDocumentKind, invocation.Result.SubjectKind);

        var requirementsService = (IRequirementsService)_host.Services!.GetService(typeof(IRequirementsService));
        var created = await requirementsService.FindAsync(invocation.Result.SubjectId!.Value);
        Assert.NotNull(created);
        Assert.Null(created!.GroupId);
    }
}
