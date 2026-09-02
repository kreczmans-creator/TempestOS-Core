using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Tempest.App.Projects;
using Tempest.App.Shell;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Viewing;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;
using Tempest.Desktop.Viewing;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The Project Workspace's Documents and Requirements areas, driven the
/// way a user reaches them: <b>project → project area →
/// document/requirement → action</b>, through the real
/// <see cref="MainWindow"/> over a real <see cref="WorkspaceHost"/>.
/// </summary>
/// <remarks>
/// <para>
/// Both areas were declared <c>Implemented</c> and drew a
/// <see cref="DeclaredCapabilityView"/> — a glyph, a title and a paragraph
/// of prose with no content behind it. These tests exist so that cannot
/// silently return: each one asserts the real surface is present, that it
/// is <em>not</em> the declared-capability card, and that the action it
/// offers actually does something.
/// </para>
/// <para>
/// Nothing here calls a register or the viewer launcher directly. A test
/// that reached past the shell would pass over exactly the class of defect
/// the `TD-80` visual audit found — a working destination nobody could
/// reach.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProjectAreaAcceptanceTests
{
    private static ICommandDispatcher DispatcherOf(WorkspaceHost host) =>
        (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

    private static EngineeringDomainContext DomainOf(WorkspaceHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

    private static IRequirementsService RequirementsOf(WorkspaceHost host) =>
        (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));

    private static IVerificationService VerificationOf(WorkspaceHost host) =>
        (IVerificationService)host.Services!.GetService(typeof(IVerificationService));

    /// <summary>Creates a Document in <paramref name="parentId"/> and attaches a real file to it.</summary>
    private static async Task<(Guid DocumentId, Guid AttachmentId)> CreateDocumentAsync(
        WorkspaceHost host, string identifier, Guid parentId, string fileName, byte[] content)
    {
        var created = await DispatcherOf(host).DispatchAsync(new CreateDocumentObjectCommand(
            DocumentObjectFactoryRegistry.Document, $"Document {identifier}", identifier: identifier,
            initialContent: "Project document.",
            classification: DocumentObjectFactoryRegistry.Specification), CancellationToken.None);
        Assert.True(created.Succeeded, created.Message);

        var document = (await DomainOf(host).Repository.ListByKindAsync(DocumentObjectFactoryRegistry.Document))
            .Single(o => ((IHasBusinessIdentifier)o).Identifier == identifier);

        await ((IHasParent)document).MoveAsync(parentId);

        var attached = await DispatcherOf(host).DispatchAsync(new AttachDocumentCommand(
            document.Id, DocumentObjectFactoryRegistry.Document, fileName, "application/pdf", content), CancellationToken.None);
        Assert.True(attached.Succeeded, attached.Message);

        var attachment = (await ((IHasAttachments)document).GetAttachmentsAsync()).Single(a => a.FileName == fileName);
        return (document.Id, attachment.Id);
    }

    /// <summary>Navigates to a project area exactly as the tab strip does, and renders the shell.</summary>
    private static async Task GoToAreaAsync(WorkspaceHost host, MainWindow window, Guid projectId, ProjectArea area)
    {
        await host.ShellNavigator!.OpenProjectAsync(projectId, area);
        await window.RenderCurrentModuleAsync();
    }

    private static ProjectDocumentsView DocumentsSurfaceOf(MainWindow window) =>
        window.GetLogicalDescendants().OfType<ProjectDocumentsView>().Single();

    private static ProjectRequirementsView RequirementsSurfaceOf(MainWindow window) =>
        window.GetLogicalDescendants().OfType<ProjectRequirementsView>().Single();

    // ================================================================
    // Documents
    // ================================================================

    [AvaloniaFact]
    public async Task Journey_ProjectToDocumentsToOpenADrawing_InTheRealViewer()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0027", "Apollo Pump Redesign");
            var (_, attachmentId) = await CreateDocumentAsync(
                host, "DWG-1001", project.Id, "pump-head.pdf", DocumentPageSourceTests.MultiPagePdf());

            // --- project → Documents area -----------------------------
            await GoToAreaAsync(host, window, project.Id, ProjectArea.Documents);

            Assert.Equal(ProjectArea.Documents, host.ShellNavigator!.Current.ProjectArea);

            // The real surface is present, and the declared-capability
            // card is not standing in for it.
            // The Documents area's own surface is the real register, not a
            // declared-capability card. Other areas legitimately still show
            // one, so the assertion is scoped to this area's own subtree.
            var documents = DocumentsSurfaceOf(window);
            Assert.False(documents.IsShowingEmptyState);
            Assert.Empty(documents.GetLogicalDescendants().OfType<DeclaredCapabilityView>());

            var entry = Assert.Single(documents.Entries);
            Assert.Equal("DWG-1001", entry.Identifier);
            Assert.Equal("pump-head.pdf", Assert.Single(entry.Attachments).FileName);

            // --- document → open --------------------------------------
            var open = documents.GetLogicalDescendants().OfType<Button>()
                .Single(b => b.Content as string == "Open");

            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            for (var attempt = 0; attempt < 200 && window.AttachmentViewers.OpenAttachmentIds.Count == 0; attempt++)
                await Task.Delay(10);

            // --- it opened in the real `TD-80` viewer -----------------
            Assert.Equal([attachmentId], window.AttachmentViewers.OpenAttachmentIds);

            var panelId = window.AttachmentViewers.PanelFor(attachmentId)!.Value;
            Assert.True(window.WorkspaceLayout.IsPanelVisible(panelId));

            var viewer = window.AttachmentViewers.ViewerFor(attachmentId)!;
            Assert.Equal(DocumentViewStatus.Ready, viewer.Session!.Status);
            Assert.Equal("pump-head.pdf", viewer.Session!.FileName);
            Assert.NotNull(viewer.RenderedPage);

            // And the row the user pressed says where the document went,
            // rather than looking as though nothing happened.
            Assert.Contains(attachmentId, documents.OpenedAttachmentIds);
            Assert.Contains(
                documents.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty),
                t => t.Contains(ProjectDocumentsView.OpenedNote, StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task OpeningADocumentFromTheProjectArea_LeavesTheProjectAndAreaExactlyWhereTheyWere()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0027", "Apollo");
            await CreateDocumentAsync(host, "DWG-1", project.Id, "one.pdf", DocumentPageSourceTests.MultiPagePdf());

            await GoToAreaAsync(host, window, project.Id, ProjectArea.Documents);

            var locationBefore = host.ShellNavigator!.Current;
            var projectBefore = host.ProjectContext!.Current!.Id;

            var open = DocumentsSurfaceOf(window).GetLogicalDescendants().OfType<Button>()
                .Single(b => b.Content as string == "Open");
            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            for (var attempt = 0; attempt < 200 && window.AttachmentViewers.OpenAttachmentIds.Count == 0; attempt++)
                await Task.Delay(10);

            // Opening a drawing is not navigation: the module, the project
            // and the area the user was on are all untouched.
            Assert.Equal(locationBefore, host.ShellNavigator!.Current);
            Assert.Equal(projectBefore, host.ProjectContext!.Current!.Id);
            Assert.Equal(ShellArea.ProjectWorkspace, host.ShellNavigator!.Current.Area);
            Assert.Equal(ProjectArea.Documents, host.ShellNavigator!.Current.ProjectArea);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TwoDocumentsFromTheProjectArea_OpenAsTwoOrdinaryWorkspacePanels()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0027", "Apollo");
            await CreateDocumentAsync(host, "DWG-1", project.Id, "first.pdf", DocumentPageSourceTests.MultiPagePdf());
            await CreateDocumentAsync(host, "DWG-2", project.Id, "second.pdf", DocumentPageSourceTests.MultiPagePdf());

            await GoToAreaAsync(host, window, project.Id, ProjectArea.Documents);

            var buttons = DocumentsSurfaceOf(window).GetLogicalDescendants().OfType<Button>()
                .Where(b => b.Content as string == "Open").ToList();
            Assert.Equal(2, buttons.Count);

            foreach (var button in buttons)
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            for (var attempt = 0; attempt < 300 && window.AttachmentViewers.OpenAttachmentIds.Count < 2; attempt++)
                await Task.Delay(10);

            // No fixed number of viewers, because there is no fixed grid to
            // run out of (`TD-72`).
            Assert.Equal(2, window.AttachmentViewers.OpenAttachmentIds.Count);
            foreach (var attachmentId in window.AttachmentViewers.OpenAttachmentIds)
                Assert.True(window.WorkspaceLayout.IsPanelVisible(window.AttachmentViewers.PanelFor(attachmentId)!.Value));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheDocumentsArea_ShowsOnlyThisProjectsDocuments_EvenWithAnotherProjectOpenFirst()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var apollo = await host.ProjectDirectory!.CreateAsync("P-0027", "Apollo");
            var vulcan = await host.ProjectDirectory!.CreateAsync("P-0031", "Vulcan");

            await CreateDocumentAsync(host, "DWG-A", apollo.Id, "apollo.pdf", DocumentPageSourceTests.MultiPagePdf());
            await CreateDocumentAsync(host, "DWG-V", vulcan.Id, "vulcan.pdf", DocumentPageSourceTests.MultiPagePdf());

            await GoToAreaAsync(host, window, apollo.Id, ProjectArea.Documents);
            Assert.Equal(["DWG-A"], DocumentsSurfaceOf(window).Entries.Select(e => e.Identifier));

            // Switching project switches the register with it — the surface
            // is never showing the project the user just left.
            await GoToAreaAsync(host, window, vulcan.Id, ProjectArea.Documents);
            Assert.Equal(["DWG-V"], DocumentsSurfaceOf(window).Entries.Select(e => e.Identifier));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AnEmptyProjectsDocumentsArea_SaysSoPlainly_AndIsNotADeclaredCapabilityCard()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0099", "Nothing yet");
            await GoToAreaAsync(host, window, project.Id, ProjectArea.Documents);

            var documents = DocumentsSurfaceOf(window);

            Assert.True(documents.IsShowingEmptyState);
            Assert.Empty(documents.Entries);
            Assert.Contains("Nothing yet", documents.SummaryText, StringComparison.Ordinal);

            var text = documents.GetLogicalDescendants().OfType<TextBlock>()
                .Select(t => t.Text ?? string.Empty).ToList();

            Assert.Contains(text, t => t.Contains(ProjectDocumentsView.EmptyHeadline, StringComparison.Ordinal));
            Assert.DoesNotContain(text, t => t.Contains(DeclaredCapabilityView.NotImplementedBadge, StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheDocumentsArea_SurvivesARestart_AndStillOpensTheDrawing()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid projectId;

        // ---- FIRST LAUNCH -------------------------------------------
        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            var project = await first.ProjectDirectory!.CreateAsync("P-0027", "Apollo");
            projectId = project.Id;
            await CreateDocumentAsync(first, "DWG-1001", projectId, "pump-head.pdf", DocumentPageSourceTests.MultiPagePdf());
        }
        finally
        {
            await first.ShutdownAsync();
            await first.DisposeAsync();
        }

        // ---- SECOND LAUNCH ------------------------------------------
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var window = new MainWindow(second);

            await GoToAreaAsync(second, window, projectId, ProjectArea.Documents);

            var documents = DocumentsSurfaceOf(window);
            var entry = Assert.Single(documents.Entries);
            Assert.Equal("DWG-1001", entry.Identifier);

            var open = documents.GetLogicalDescendants().OfType<Button>()
                .Single(b => b.Content as string == "Open");
            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            for (var attempt = 0; attempt < 200 && window.AttachmentViewers.OpenAttachmentIds.Count == 0; attempt++)
                await Task.Delay(10);

            var viewer = window.AttachmentViewers.ViewerFor(
                window.AttachmentViewers.OpenAttachmentIds.Single())!;

            // The bytes came back too, not just the metadata (`TD-31`).
            Assert.Equal(DocumentViewStatus.Ready, viewer.Session!.Status);
            Assert.NotNull(viewer.RenderedPage);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // Requirements
    // ================================================================

    [AvaloniaFact]
    public async Task Journey_ProjectToRequirements_ShowsStatusAndWhatVerificationRecorded()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0027", "Apollo");
            var (documentId, _) = await CreateDocumentAsync(
                host, "DWG-1", project.Id, "head.pdf", DocumentPageSourceTests.MultiPagePdf());

            var requirements = RequirementsOf(host);
            var passed = await requirements.CreateAsync("REQ-100", "The head shall withstand 40 bar.");
            var unverified = await requirements.CreateAsync("REQ-200", "The head shall be paintable.");

            await requirements.LinkAsync(passed.Id, documentId, RequirementRelationshipKinds.AllocatedTo);
            await requirements.LinkAsync(unverified.Id, documentId, RequirementRelationshipKinds.AllocatedTo);

            var context = new VerificationContext();
            context.RecordEvidence("Pressure test report.");
            await VerificationOf(host).RecordAsync(passed.Id, VerificationOutcome.Pass, "Test", context);

            // Stated as a precondition rather than inherited: reading
            // verification history is permission-gated, and the desktop
            // shell itself establishes no principal — only sample modules
            // do, so what the session happens to hold depends on which of
            // them initialised last. Disclosed as a finding of this Work
            // Package; the register's own denied path is covered by
            // `ProjectAreaRegisterTests`.
            ((CurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor)))
                .SetCurrent(new PlatformPrincipal(
                    new PlatformIdentity("engineer", "engineer"),
                    [Core.Verification.VerificationService.ReadPermission]));

            // --- project → Requirements area --------------------------
            await GoToAreaAsync(host, window, project.Id, ProjectArea.Requirements);

            Assert.Equal(ProjectArea.Requirements, host.ShellNavigator!.Current.ProjectArea);

            var surface = RequirementsSurfaceOf(window);
            Assert.False(surface.IsShowingEmptyState);

            var entries = surface.Entries.ToDictionary(e => e.Identifier);
            Assert.Equal(2, entries.Count);

            Assert.Equal(RequirementVerificationState.Passed, entries["REQ-100"].Verification);
            Assert.Equal(1, entries["REQ-100"].VerificationCount);
            Assert.Equal(RequirementVerificationState.NotVerified, entries["REQ-200"].Verification);

            // Both facts reach the screen, not only the model.
            var text = surface.GetLogicalDescendants().OfType<TextBlock>()
                .Select(t => t.Text ?? string.Empty).ToList();

            Assert.Contains(text, t => t.Contains("REQ-100", StringComparison.Ordinal));
            Assert.Contains(text, t => t.Contains(
                ProjectRequirementsView.Describe(RequirementVerificationState.Passed), StringComparison.Ordinal));
            Assert.Contains(text, t => t.Contains(
                ProjectRequirementsView.Describe(RequirementVerificationState.NotVerified), StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheRequirementsArea_ShowsOnlyThisProjectsRequirements()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var apollo = await host.ProjectDirectory!.CreateAsync("P-0027", "Apollo");
            var vulcan = await host.ProjectDirectory!.CreateAsync("P-0031", "Vulcan");

            var (apolloDocument, _) = await CreateDocumentAsync(host, "DWG-A", apollo.Id, "a.pdf", DocumentPageSourceTests.MultiPagePdf());
            var (vulcanDocument, _) = await CreateDocumentAsync(host, "DWG-V", vulcan.Id, "v.pdf", DocumentPageSourceTests.MultiPagePdf());

            var requirements = RequirementsOf(host);
            var apolloRequirement = await requirements.CreateAsync("REQ-A", "Apollo requirement.");
            var vulcanRequirement = await requirements.CreateAsync("REQ-V", "Vulcan requirement.");

            await requirements.LinkAsync(apolloRequirement.Id, apolloDocument, RequirementRelationshipKinds.AllocatedTo);
            await requirements.LinkAsync(vulcanRequirement.Id, vulcanDocument, RequirementRelationshipKinds.AllocatedTo);

            await GoToAreaAsync(host, window, apollo.Id, ProjectArea.Requirements);
            Assert.Equal(["REQ-A"], RequirementsSurfaceOf(window).Entries.Select(e => e.Identifier));

            await GoToAreaAsync(host, window, vulcan.Id, ProjectArea.Requirements);
            Assert.Equal(["REQ-V"], RequirementsSurfaceOf(window).Entries.Select(e => e.Identifier));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AnEmptyProjectsRequirementsArea_SaysSoPlainly_AndIsNotADeclaredCapabilityCard()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0099", "Nothing yet");
            await GoToAreaAsync(host, window, project.Id, ProjectArea.Requirements);

            var surface = RequirementsSurfaceOf(window);

            Assert.True(surface.IsShowingEmptyState);
            Assert.Empty(surface.Entries);

            var text = surface.GetLogicalDescendants().OfType<TextBlock>()
                .Select(t => t.Text ?? string.Empty).ToList();

            Assert.Contains(text, t => t.Contains(ProjectRequirementsView.EmptyHeadline, StringComparison.Ordinal));
            Assert.DoesNotContain(text, t => t.Contains(DeclaredCapabilityView.NotImplementedBadge, StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheRequirementsArea_NavigatesIntoTheEngineeringWorkspace()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0027", "Apollo");
            await GoToAreaAsync(host, window, project.Id, ProjectArea.Requirements);

            var enter = RequirementsSurfaceOf(window).GetLogicalDescendants().OfType<Button>()
                .Single(b => b.Content as string == "Open in Engineering →");

            enter.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            for (var attempt = 0; attempt < 200 && host.ShellNavigator!.Current.Area != ShellArea.Engineering; attempt++)
                await Task.Delay(10);

            // Into the existing workflow, with the project still the scope.
            Assert.Equal(ShellArea.Engineering, host.ShellNavigator!.Current.Area);
            Assert.Equal(project.Id, host.ProjectContext!.Current!.Id);
            Assert.Equal(EngineeringScopeKind.Project, host.EngineeringScope!.Current.Kind);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ================================================================
    // Both areas, and what they must never become again
    // ================================================================

    [AvaloniaFact]
    public async Task NeitherAreaRendersADeclaredCapabilityCard_AndBothDescriptorsSayImplemented()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0027", "Apollo");

            foreach (var area in new[] { ProjectArea.Documents, ProjectArea.Requirements })
            {
                await GoToAreaAsync(host, window, project.Id, area);

                // The descriptor says Implemented, and a real surface for it
                // is genuinely on screen — the pair of facts that was untrue
                // before this Work Package.
                Assert.True(ProjectAreas.IsImplemented(area));
                Assert.Null(ProjectAreas.For(area).TrackedBy);
            }

            Assert.Single(window.GetLogicalDescendants().OfType<ProjectDocumentsView>());
            Assert.Single(window.GetLogicalDescendants().OfType<ProjectRequirementsView>());
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
