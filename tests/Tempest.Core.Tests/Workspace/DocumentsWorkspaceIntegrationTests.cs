using Tempest.App.Workspace;
using Tempest.App.Workspace.Documents;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;
using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Requirements;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// End-to-end `WP 9.4A` Workspace integration test — the same composition
/// <c>Program.cs</c> itself performs (<see cref="DocumentsWorkspaceRegistration.Register"/>,
/// called after <see cref="WorkspaceManager.StartAsync"/> exactly as
/// `Program.cs` calls it), against the real, running <see cref="ITempestHost"/>,
/// the real <see cref="EngineeringDocumentsWorkspaceSampleModule"/>'s own
/// representative graph (which itself depends on the real
/// <see cref="EngineeringDomainSampleModule"/>'s (for the base sample's own
/// live Risk), <see cref="MechanicalProductStructureSampleModule"/>'s,
/// <see cref="RequirementsWorkspaceSampleModule"/>'s, and
/// <see cref="EngineeringCalculationsWorkspaceSampleModule"/>'s own graphs
/// for cross-discipline Digital Thread links), and the real Command
/// Framework — mirroring <c>CalculationsWorkspaceIntegrationTests</c>'s own
/// identical shape.
/// </summary>
[Collection("Console output capture")]
public class DocumentsWorkspaceIntegrationTests
{
    private static async Task<(IWorkspace Workspace, WorkspaceManager Manager, ITempestHost Host)> StartAsync(string rootPath)
    {
        var host = new TempestHostBuilder(
        [
            typeof(EngineeringDomainSampleModule),
            typeof(MechanicalProductStructureSampleModule),
            typeof(RequirementsWorkspaceExplorerModule),
            typeof(RequirementsWorkspaceSampleModule),
            typeof(CalculationsWorkspaceExplorerModule),
            typeof(EngineeringCalculationsWorkspaceSampleModule),
            typeof(DocumentsWorkspaceExplorerModule),
            typeof(EngineeringDocumentsWorkspaceSampleModule),
        ])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        var originalOut = Console.Out;
        IWorkspace workspace;
        try
        {
            Console.SetOut(new StringWriter());
            workspace = await manager.StartAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Same disclosed, pre-existing platform timing characteristic
        // CalculationsWorkspaceIntegrationTests's own StartAsync already
        // works around — waiting on the seeded module's own HasRegistered
        // flag (the last module in ordinal Id order: EngineeringDomain,
        // then Mechanical, then Requirements, then Calculations, then this
        // one).
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (GetSeededModule(host) is not { HasRegistered: true } && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        var services = host.Services!;
        var domainContext = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        var commandDispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));
        var commandRegistry = (ICommandRegistry)services.GetService(typeof(ICommandRegistry));

        DocumentsWorkspaceRegistration.Register(manager, domainContext, commandDispatcher, commandRegistry);

        return (workspace, manager, host);
    }

    private static EngineeringDocumentsWorkspaceSampleModule GetSeededModule(ITempestHost host) =>
        (EngineeringDocumentsWorkspaceSampleModule)host.Services!.GetService(typeof(EngineeringDocumentsWorkspaceSampleModule));

    [Fact]
    public async Task ProjectExplorer_DocumentsArea_RootsIncludeOneCategoryNodePerLabel()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);

        await workspace.Navigation.SwitchAreaAsync(DocumentsWorkspaceExplorerModule.NavigationItemId);
        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();

        Assert.Equal(DocumentsNodeProvider.CategoryLabels.Count, roots.Count);
        Assert.Contains(roots, n => n.Title == "Drawings" && n.HasChildren);
        Assert.Contains(roots, n => n.Title == "Specifications" && n.HasChildren);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ProjectExplorer_DrillIntoDrawingsCategory_FindsTheGaDrawingAndTheBaseSampleDrawing()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(DocumentsWorkspaceExplorerModule.NavigationItemId);
        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
        var drawingsNode = roots.Single(n => n.Title == "Drawings");
        var children = await workspace.ProjectExplorer.GetChildrenAsync(drawingsNode.Id);

        // The Detail Drawing is real-parented under the GA Drawing (this
        // module's own explorer nesting demonstration), so it is not a
        // top-level category member. The base EngineeringDomainSampleModule
        // (WP 8.2C) already seeds one further, un-parented "Sample Drawing"
        // — a genuine, disclosed cross-sample-module interaction, not a
        // defect.
        Assert.Equal(2, children.Count);
        var gaDrawingNode = children.Single(n => n.Id == seeded.GeneralArrangementDrawingId);
        Assert.True(gaDrawingNode.HasChildren);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ProjectExplorer_DrillIntoGaDrawing_FindsTheDetailDrawing()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(DocumentsWorkspaceExplorerModule.NavigationItemId);
        var children = await workspace.ProjectExplorer.GetChildrenAsync(seeded.GeneralArrangementDrawingId!.Value);

        var node = Assert.Single(children);
        Assert.Equal(seeded.DetailDrawingId, node.Id);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_GaDrawing_PropertyInspectorShowsApprovedStatusAndDrawingNumber()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Selection.SelectAsync(seeded.GeneralArrangementDrawingId!.Value, "Drawing");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Status" && f.Value == "Approved");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Approved" && f.Value == "Yes");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Drawing Number" && f.Value == "GA-1000");

        var mechanical = (MechanicalProductStructureSampleModule)host.Services!.GetService(typeof(MechanicalProductStructureSampleModule));
        Assert.Contains(
            workspace.PropertyInspector.CurrentFacets,
            f => f.Name == "Documents (Digital Thread)" && f.Value.Contains(mechanical.WingAssemblyId!.Value.ToString()));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_TestReport_PropertyInspectorShowsAttachmentAndReferencesLink()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Selection.SelectAsync(seeded.TestReportId!.Value, "Document");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Attachments" && f.Value == "static-test-report.pdf");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "References (Digital Thread)");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task CommandRegistry_ListsAllElevenDocumentsCommands()
    {
        using var temp = new TempDirectory();
        var (_, manager, host) = await StartAsync(temp.Path);
        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

        var documentsCommands = commandRegistry.Items.Where(d => d.Category == "Documents").ToList();

        Assert.Equal(11, documentsCommands.Count);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task FullLifecycle_CreateAttachSetStatusDelete_AllSucceed()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
        var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

        await workspace.Navigation.SwitchAreaAsync(DocumentsWorkspaceExplorerModule.NavigationItemId);

        // Create.
        var createResult = await commandDispatcher.DispatchAsync(
            new CreateDocumentObjectCommand("Document", "Integration Test Document", "DOC-IT-001", classification: DocumentObjectFactoryRegistry.Report), default);
        Assert.True(createResult.Succeeded);
        var created = (await domainContext.Repository.ListByKindAsync("Document"))
            .Single(d => ((IHasBusinessIdentifier)d).Identifier == "DOC-IT-001");

        // Attach.
        var attachResult = await commandDispatcher.DispatchAsync(
            new AttachDocumentCommand(created.Id, "Document", "integration-test.pdf", "application/pdf", 512), default);
        Assert.True(attachResult.Succeeded);
        Assert.Single(await ((IHasAttachments)created).GetAttachmentsAsync());

        // Set Status (Request Review -> Approve -> Release, all via the one SetDocumentStatusCommand the Command Palette entries dispatch through).
        var reviewResult = await commandDispatcher.DispatchAsync(
            new SetDocumentStatusCommand(created.Id, "Document", LifecycleState.InReview), default);
        Assert.True(reviewResult.Succeeded);
        var approveResult = await commandDispatcher.DispatchAsync(
            new SetDocumentStatusCommand(created.Id, "Document", LifecycleState.Approved), default);
        Assert.True(approveResult.Succeeded);
        var releaseResult = await commandDispatcher.DispatchAsync(
            new SetDocumentStatusCommand(created.Id, "Document", LifecycleState.Released), default);
        Assert.True(releaseResult.Succeeded);

        // Delete.
        var deleteResult = await commandDispatcher.DispatchAsync(new DeleteDocumentObjectCommand(created.Id, "Document"), default);
        Assert.True(deleteResult.Succeeded);

        await manager.ShutdownAsync();
    }

    // ---- Engineering Cockpit KPIs (WP 9.4A), against the real seeded graph ----

    [Fact]
    public async Task Cockpit_DocumentsKpiCards_ReflectTheRealSeededGraph()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        var cards = cockpit.DocumentsKpiCards.ToDictionary(c => c.Label, c => c.Value);

        // Ten live Documents: this module's own nine — GA Drawing
        // (Approved), Detail Drawing (InReview), Specification (Draft),
        // Test Report (Approved), Design Report (Released), Datasheet
        // (Approved), Procedure (InReview), Standard (Approved), External
        // Reference (Draft, deliberately unlinked — the sole "Missing
        // Evidence" example) — plus the base EngineeringDomainSampleModule's
        // (WP 8.2C) own pre-existing "Sample Drawing" (Draft,
        // documentedBy-linked, so not itself Missing Evidence) — a genuine,
        // disclosed cross-sample-module interaction, not a defect.
        Assert.Equal("10", cards["Total Documents"]);
        Assert.Equal("3", cards["Draft"]);
        Assert.Equal("2", cards["Review"]);
        Assert.Equal("4", cards["Approved"]);
        Assert.Equal("1", cards["Released"]);
        Assert.Equal("2", cards["Outstanding Reviews"]);
        Assert.Equal("1", cards["Missing Evidence"]);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Cockpit_AttentionItems_ReportsDocumentsAreLive()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Contains(cockpit.AttentionItems, item => item.Title == "Documents are live");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Cockpit_DocumentationStatus_IsAttentionBecauseOfOutstandingReviewsAndMissingEvidence()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Equal(EngineeringHealthStatus.Attention, cockpit.DocumentationStatus);

        await manager.ShutdownAsync();
    }
}
