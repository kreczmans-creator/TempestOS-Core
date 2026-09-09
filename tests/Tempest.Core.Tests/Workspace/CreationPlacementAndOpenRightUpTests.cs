using Tempest.Workspace;
using Tempest.Workspace.Composition;
using Tempest.Workspace.Manufacturing;
using Tempest.Workspace.Mechanical;
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
/// Requirements, Manufacturing, Verification, Evidence — proving each
/// resolves its parent through <see cref="CreationPlacement.ParentFor"/>
/// (or, for Requirements, that discipline's own documented equivalent —
/// see <see cref="Requirements_NothingSelected_IsReachableFromRoot"/>'s own
/// remarks) and returns <c>CommandResult.Success(message, id, kind)</c> so
/// the shell can reveal and open what was just made.
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

    [Fact]
    public async Task Manufacturing_APartSelected_OperationLandsUnderThatPart()
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

        await AssertCreatedAndPlacedUnderAsync(invocation, ManufacturingObjectFactoryRegistry.ManufacturingOperationKind, partId);
    }

    [Fact]
    public async Task Verification_ANonContainerSubjectSelected_LandsUnderTheOpenProject()
    {
        var projectId = await CreateProjectAsync();
        var partId = await CreatePartAsync(projectId);
        // The Part is the required subject (what is being verified) — never a
        // container, so the activity's own structural parent falls back to
        // the open project, exactly as Evidence's identical fallback does.
        var context = new CommandContext([new CommandContextObject(partId, "Part")], projectId);

        var invocation = await _registry.InvokeAsync(
            "verification.create", context,
            Answering(new Dictionary<string, string> { ["displayName"] = "Verify the pocket depth", ["method"] = "Inspection" }));

        await AssertCreatedAndPlacedUnderAsync(invocation, "VerificationActivity", projectId);
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
