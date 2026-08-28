using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Projects;
using Tempest.App.Shell;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `TD-85`'s own Definition of Done, driven end to end through the real
/// <see cref="MainWindow"/> over two real <see cref="WorkspaceHost"/>
/// lifetimes sharing one persistence root:
/// <b>a user can create engineering work, close TempestOS, relaunch it,
/// and carry on working on that same engineering work.</b>
/// </summary>
/// <remarks>
/// <para>
/// This proves behaviour, not file layout. Nothing here inspects the store
/// on disk; every assertion is made against the live objects the running
/// application hands back after the relaunch — their identity, Kind,
/// lifecycle state, revision history, relationships, structural parent and
/// business data — and against the shell surfaces that render them.
/// </para>
/// <para>
/// The second lifetime is a genuinely new process shape: a new
/// <see cref="WorkspaceHost"/>, a new Host, a new in-memory object
/// repository and relationship index, a new <see cref="MainWindow"/>. The
/// only thing carried across is the persistence root — which is exactly
/// what a real relaunch carries across.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ObjectRehydrationAcceptanceTests
{
    [AvaloniaFact]
    public async Task Journey_CreateEngineeringWork_Relaunch_AndKeepWorkingOnTheSameObjects()
    {
        // One persistence root stands for one machine across two launches.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        Guid projectId;
        Guid assemblyId;
        Guid partId;

        // ============================================================
        // FIRST LAUNCH — create the engineering work
        // ============================================================
        var first = new WorkspaceHost(root);
        try
        {
            // --- 1. Launch TempestOS ---------------------------------
            await first.StartAsync();
            var window = new MainWindow(first);
            var navigator = first.ShellNavigator!;
            var directory = first.ProjectDirectory!;

            Assert.Equal(ShellArea.Home, navigator.Current.Area);

            // --- 2. Navigate to Projects through the real shell -------
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            Assert.NotNull(window.GetLogicalDescendants().OfType<ProjectBrowserView>().SingleOrDefault());

            // --- 3. Create a project through the production surface ---
            var project = await directory.CreateAsync("P-0027", "Apollo Pump Redesign");
            projectId = project.Id;

            // --- 4. Open it ------------------------------------------
            await navigator.OpenProjectAsync(projectId);
            await window.RenderCurrentModuleAsync();

            // --- 5. The current project is a real IProject ------------
            var domain = DomainOf(first);
            Assert.IsAssignableFrom<IProject>(await domain.Repository.FindAsync(projectId));

            // --- 6. Enter Engineering from that project ---------------
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            Assert.Equal(ShellArea.Engineering, navigator.Current.Area);
            Assert.NotNull(window.GetLogicalDescendants().OfType<RibbonView>().SingleOrDefault());

            // --- 7. Create engineering objects through the real
            //        production command path the ribbon dispatches ----
            var dispatcher = (ICommandDispatcher)first.Services!.GetService(typeof(ICommandDispatcher));

            var assemblyResult = await dispatcher.DispatchAsync(new CreateMechanicalObjectCommand(
                MechanicalObjectFactoryRegistry.Assembly, "Pump Head Assembly", "ASM-100", parentId: projectId), CancellationToken.None);
            Assert.True(assemblyResult.Succeeded, assemblyResult.Message);

            var assembly = (await domain.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Assembly))
                .Single(o => o.Id != projectId && ((IHasBusinessIdentifier)o).Identifier == "ASM-100");
            assemblyId = assembly.Id;

            var partResult = await dispatcher.DispatchAsync(new CreateMechanicalObjectCommand(
                MechanicalObjectFactoryRegistry.Part, "Impeller", "PN-1001", parentId: assemblyId), CancellationToken.None);
            Assert.True(partResult.Succeeded, partResult.Message);

            var part = (await domain.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Part))
                .Single(o => ((IHasBusinessIdentifier)o).Identifier == "PN-1001");
            partId = part.Id;

            // --- 8. Modify them: lifecycle, rename, BOM line, an
            //        explicit relationship, and a new revision --------
            await ((IHasLifecycle)part).TransitionAsync(LifecycleState.InReview);
            await ((IRenamable)part).RenameAsync("Impeller (Rev B geometry)");
            await ((IHasBomLine)part).SetBomLineAsync(4m, "ea", findNumber: "FN-07", itemNumber: "IT-3", referenceDesignator: "RD-9");
            await ((IHasRelationships)part).LinkAsync(assemblyId, "dependsOn");
            await ((IHasRevisions)part).ReviseAsync("Impeller — revised blade profile.", "Rev B");

            // --- 9. It is all persisted through the real architecture:
            //        no explicit save step exists, and none is used ---
            Assert.Equal(LifecycleState.InReview, ((IHasLifecycle)part).Status);
        }
        finally
        {
            // --- 10. Close TempestOS ---------------------------------
            await first.ShutdownAsync();
            await first.DisposeAsync();
        }

        // ============================================================
        // SECOND LAUNCH — a brand new process shape, same disk
        // ============================================================
        var second = new WorkspaceHost(root);
        try
        {
            // --- 11. Relaunch, which rehydrates ----------------------
            await second.StartAsync();
            var window = new MainWindow(second);
            var navigator = second.ShellNavigator!;
            var directory = second.ProjectDirectory!;
            var domain = DomainOf(second);

            Assert.NotNull(second.RehydrationResult);
            Assert.True(second.RehydrationResult!.ObjectCount >= 3,
                $"Expected the project, assembly and part back; got {second.RehydrationResult.ObjectCount}.");

            // --- 12. The project is recovered ------------------------
            var recovered = await directory.FindAsync(projectId);
            Assert.NotNull(recovered);
            Assert.Equal("P-0027", recovered!.Identifier);
            Assert.Equal("Apollo Pump Redesign", recovered.DisplayName);

            // --- 13. Same identity, not a look-alike -----------------
            var projectObject = await domain.Repository.FindAsync(projectId);
            var typedProject = Assert.IsAssignableFrom<IProject>(projectObject);
            Assert.Equal(projectId, typedProject.Id);
            Assert.Equal(MechanicalObjectFactoryRegistry.Project, typedProject.Kind);

            // --- 14. The engineering objects are present, and are the
            //         right canonical types ------------------------
            var rehydratedAssembly = await domain.Repository.FindAsync(assemblyId);
            var rehydratedPart = await domain.Repository.FindAsync(partId);

            Assert.IsAssignableFrom<IAssembly>(rehydratedAssembly);
            var typedPart = Assert.IsType<Tempest.Core.EngineeringDomain.Part>(rehydratedPart);

            // --- 15. Relationships, lifecycle, revision, structure
            //         and business data all came back intact --------
            Assert.Equal(LifecycleState.InReview, typedPart.Status);
            Assert.Single(typedPart.History);
            Assert.Equal(LifecycleState.Draft, typedPart.History[0].From);
            Assert.Equal(LifecycleState.InReview, typedPart.History[0].To);

            Assert.Equal("Impeller (Rev B geometry)", typedPart.DisplayName);
            Assert.Equal("PN-1001", typedPart.Identifier);
            Assert.Equal(assemblyId, typedPart.ParentId);

            Assert.Equal(4m, typedPart.Quantity);
            Assert.Equal("ea", typedPart.UnitOfMeasure);
            Assert.Equal("FN-07", typedPart.FindNumber);
            Assert.Equal("IT-3", typedPart.ItemNumber);
            Assert.Equal("RD-9", typedPart.ReferenceDesignator);

            var relationships = await typedPart.GetRelationshipsAsync();
            Assert.Contains(relationships, r => r.TargetId == assemblyId && r.RelationshipKind == "dependsOn");
            Assert.All(relationships, r => Assert.False(string.IsNullOrWhiteSpace(r.CreatedByPrincipalId)));

            var revisions = await typedPart.GetRevisionHistoryAsync();
            Assert.Equal(2, revisions.Count);
            Assert.Equal("Rev B", revisions[^1].ChangeSummary);

            // The assembly still belongs to the project, so the project
            // still knows its own contents.
            var contents = await directory.ListProjectContentsAsync(projectId);
            Assert.Contains(assemblyId, contents);

            // --- 16. The shell can open the recovered project through
            //         the real production surface ------------------
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();

            var browser = window.GetLogicalDescendants().OfType<ProjectBrowserView>().Single();
            var list = browser.GetLogicalDescendants().OfType<ListBox>().Single();
            var openButton = browser.GetLogicalDescendants().OfType<Button>()
                .Single(b => (b.Content as string) == "Open Project");

            // The catalogue holds whatever else this installation has
            // (the sample modules ship real objects too), so select the
            // recovered project by identity rather than by position.
            var catalogue = await directory.ListAsync();
            var index = catalogue.ToList().FindIndex(p => p.Id == projectId);
            Assert.True(index >= 0, "The recovered project was not listed by the real project browser's own source.");
            list.SelectedIndex = index;
            openButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);
            await window.RenderCurrentModuleAsync();

            // --- 17. Shell navigation and project context are coherent
            Assert.Equal(ShellArea.ProjectWorkspace, navigator.Current.Area);
            Assert.Equal(projectId, navigator.Current.ProjectId);
            Assert.True(second.ProjectContext!.HasProject);
            Assert.Equal(projectId, second.ProjectContext.Current!.Id);
            Assert.NotNull(window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().SingleOrDefault());

            // --- 18. Continue operating on the rehydrated objects ----
            // A rehydrated object is not a read-only snapshot: it takes
            // the next lifecycle transition, the next revision and the
            // next relationship exactly as a freshly created one does.
            await typedPart.TransitionAsync(LifecycleState.Approved);
            Assert.Equal(LifecycleState.Approved, typedPart.Status);
            Assert.Equal(2, typedPart.History.Count);

            var furtherRevision = Assert.IsType<Tempest.Core.EngineeringDomain.Part>(
                await typedPart.ReviseAsync("Impeller — production release.", "Rev C"));
            Assert.Equal(3, furtherRevision.CurrentRevisionNumber);

            var newPartResult = await ((ICommandDispatcher)second.Services!.GetService(typeof(ICommandDispatcher)))
                .DispatchAsync(new CreateMechanicalObjectCommand(
                    MechanicalObjectFactoryRegistry.Part, "Wear Ring", "PN-1002", parentId: assemblyId), CancellationToken.None);
            Assert.True(newPartResult.Succeeded, newPartResult.Message);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }

        // ============================================================
        // THIRD LAUNCH — the work continued after the relaunch is
        // itself durable, so recovery is not a one-shot trick.
        // ============================================================
        var third = new WorkspaceHost(root);
        try
        {
            await third.StartAsync();
            var domain = DomainOf(third);

            var part = Assert.IsType<Tempest.Core.EngineeringDomain.Part>(await domain.Repository.FindAsync(partId));
            Assert.Equal(LifecycleState.Approved, part.Status);
            Assert.Equal(2, part.History.Count);

            var parts = await domain.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Part);
            Assert.Contains(parts, p => ((IHasBusinessIdentifier)p).Identifier == "PN-1002");
        }
        finally
        {
            await third.ShutdownAsync();
            await third.DisposeAsync();
        }
    }

    /// <summary>
    /// `TD-85` rebuilds the relationship index from every durable
    /// `DocumentReference`, including the Activity→Record `"verifiedBy"`
    /// links `VerificationService.RecordAsync` writes straight through the
    /// document store rather than through a Domain object (`WP9.3A`). The
    /// index is therefore slightly <em>more</em> complete after a relaunch
    /// than before one, and a Verification record becomes reachable as an
    /// ordinary neighbour for the first time. It must still render as a
    /// non-expandable result leaf: what a node <em>is</em> cannot depend on
    /// which read happened to reach it first.
    /// </summary>
    [AvaloniaFact]
    public async Task AfterRelaunch_AVerificationRecord_IsStillALeaf_NotAnExpandableNeighbour()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
        }
        finally
        {
            await first.ShutdownAsync();
            await first.DisposeAsync();
        }

        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var domain = DomainOf(second);

            // The sample graph is idempotent across restarts, so everything
            // present in this second lifetime arrived through rehydration.
            var activities = await domain.Repository.ListByKindAsync("VerificationActivity");
            Assert.NotEmpty(activities);

            Guid recordId = default;
            Tempest.Core.EngineeringDomain.IEngineeringObject? verified = null;
            foreach (var activity in activities)
            {
                var records = await Tempest.App.Workspace.Verification.VerificationRecordReader
                    .GetResultHistoryAsync(domain, activity.Id);
                if (records.Count > 0)
                {
                    verified = activity;
                    recordId = records[0].RecordId;
                    break;
                }
            }

            if (verified is null)
                return; // no sample Activity carries a recorded result in this build.

            // The link really is in the rebuilt index now — this is the
            // condition that makes the assertion below meaningful rather
            // than vacuous.
            var incoming = await domain.RelationshipRepository.GetIncomingAsync(recordId);
            Assert.Contains(incoming, r => r.SourceId == verified.Id && r.RelationshipKind == "verifiedBy");

            var model = new DigitalThread.DigitalThreadGraphModel(domain);
            model.Recentre(verified.Id, verified.Kind!);

            var node = model.Nodes.Single(n => n.ObjectId == recordId);
            Assert.True(node.IsRecord, "A Verification record must stay a result leaf after a relaunch re-indexes its link.");
            Assert.False(model.ExpandNode(recordId));
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    private static EngineeringDomainContext DomainOf(WorkspaceHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
}
