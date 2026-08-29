using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Viewing;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Viewing;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `TD-80`'s own Definition of Done, driven through the real
/// <see cref="MainWindow"/> across real <see cref="WorkspaceHost"/>
/// lifetimes: <b>a user can attach a drawing, close TempestOS, relaunch
/// it, open that drawing, zoom and page through it, open a second one
/// beside it, and close them without losing the project they were in.</b>
/// </summary>
/// <remarks>
/// Documents are opened through <see cref="MainWindow.AttachmentViewers"/>
/// — the same launcher the object editor's Open button calls — so a pass
/// means the wired-up application does this, not that a viewer control
/// could if something constructed one.
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class DocumentViewerAcceptanceTests
{
    private static EngineeringDomainContext DomainOf(WorkspaceHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

    private static ICommandDispatcher DispatcherOf(WorkspaceHost host) =>
        (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

    private static async Task<(Guid DocumentId, Guid AttachmentId)> CreateDocumentWithAttachmentAsync(
        WorkspaceHost host, string identifier, string fileName, string contentType, byte[] content)
    {
        var dispatcher = DispatcherOf(host);
        var domain = DomainOf(host);

        var created = await dispatcher.DispatchAsync(new CreateDocumentObjectCommand(
            DocumentObjectFactoryRegistry.Document, $"Document {identifier}", identifier: identifier,
            initialContent: "Owning engineering object.",
            classification: DocumentObjectFactoryRegistry.Specification), CancellationToken.None);
        Assert.True(created.Succeeded, created.Message);

        var document = (await domain.Repository.ListByKindAsync(DocumentObjectFactoryRegistry.Document))
            .Single(o => ((IHasBusinessIdentifier)o).Identifier == identifier);

        var attached = await dispatcher.DispatchAsync(new AttachDocumentCommand(
            document.Id, DocumentObjectFactoryRegistry.Document, fileName, contentType, content), CancellationToken.None);
        Assert.True(attached.Succeeded, attached.Message);

        var attachment = (await ((IHasAttachments)document).GetAttachmentsAsync()).Single(a => a.FileName == fileName);
        return (document.Id, attachment.Id);
    }

    private static async Task<(IHasAttachments Owner, IAttachment Attachment)> ResolveAsync(
        WorkspaceHost host, Guid documentId, Guid attachmentId)
    {
        var document = await DomainOf(host).Repository.FindAsync(documentId);
        var owner = Assert.IsAssignableFrom<IHasAttachments>(document!);
        var attachment = (await owner.GetAttachmentsAsync()).Single(a => a.Id == attachmentId);
        return (owner, attachment);
    }

    [AvaloniaFact]
    public async Task Journey_AttachADrawing_Relaunch_ThenOpenZoomAndPageThroughIt()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var drawing = DocumentPageSourceTests.MultiPagePdf();

        Guid documentId;
        Guid attachmentId;

        // ---- FIRST LAUNCH: attach a real multi-page drawing -----------
        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                first, "DWG-100", "pump-head.pdf", "application/pdf", drawing);
        }
        finally
        {
            await first.ShutdownAsync();
            await first.DisposeAsync();
        }

        // ---- SECOND LAUNCH: open it in the real viewer ----------------
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var window = new MainWindow(second);

            var (owner, attachment) = await ResolveAsync(second, documentId, attachmentId);
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            // --- It opened, on page 1, fitted -------------------------
            var session = viewer.Session!;
            Assert.Equal(DocumentViewStatus.Ready, session.Status);
            Assert.Equal(ViewableDocumentFormat.Pdf, session.Format);
            Assert.Equal(3, session.PageCount);
            Assert.Equal(1, session.CurrentPage);
            Assert.True(session.Viewport.IsFitted);
            Assert.False(viewer.IsShowingUnavailableState);
            Assert.Equal("Page 1 of 3", viewer.PageIndicatorText);

            // --- And it really rendered -------------------------------
            Assert.NotNull(viewer.RenderedPage);
            Assert.True(viewer.RenderedPage!.PixelSize.Width > 0);

            // --- Page navigation --------------------------------------
            viewer.NextPage();
            Assert.Equal(2, viewer.Session!.CurrentPage);
            Assert.Equal("Page 2 of 3", viewer.PageIndicatorText);

            viewer.GoToPage(99);
            Assert.Equal(3, viewer.Session!.CurrentPage);

            viewer.PreviousPage();
            Assert.Equal(2, viewer.Session!.CurrentPage);

            // --- Zoom -------------------------------------------------
            var fittedZoom = viewer.Session!.Viewport.Zoom;
            viewer.ZoomIn();
            Assert.True(viewer.Session!.Viewport.Zoom > fittedZoom);

            viewer.ActualSize();
            Assert.Equal(1.0, viewer.Session!.Viewport.Zoom, 0.0001);

            // --- Pan, which only means anything once zoomed in --------
            // Zoomed well past fit first, so both axes genuinely overflow:
            // at actual size this landscape page is narrower than the view,
            // so its X offset is centre-clamped and a horizontal pan is
            // correctly a no-op. Panning an axis with nowhere to go proves
            // nothing about panning.
            viewer.ZoomIn();
            viewer.ZoomIn();
            viewer.ZoomIn();
            viewer.ZoomIn();

            var panned = viewer.Session!.Viewport;
            Assert.True(panned.RenderedWidth > panned.ViewportWidth);
            Assert.True(panned.RenderedHeight > panned.ViewportHeight);

            var beforeX = panned.OffsetX;
            var beforeY = panned.OffsetY;
            viewer.PanBy(40, 40);

            Assert.NotEqual(beforeX, viewer.Session!.Viewport.OffsetX);
            Assert.NotEqual(beforeY, viewer.Session!.Viewport.OffsetY);

            // --- Fit puts the whole page back -------------------------
            viewer.FitToView();
            Assert.True(viewer.Session!.Viewport.IsFitted);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task MultipleDocuments_OpenSideBySide_AsOrdinaryWorkspacePanels()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (firstDocument, firstAttachment) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-201", "first.pdf", "application/pdf", DocumentPageSourceTests.MultiPagePdf());
            var (secondDocument, secondAttachment) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-202", "second.pdf", "application/pdf", DocumentPageSourceTests.MultiPagePdf());

            var (firstOwner, first) = await ResolveAsync(host, firstDocument, firstAttachment);
            var (secondOwner, second) = await ResolveAsync(host, secondDocument, secondAttachment);

            var firstViewer = await window.AttachmentViewers.OpenAsync(firstOwner, first, 800, 600);
            var secondViewer = await window.AttachmentViewers.OpenAsync(secondOwner, second, 800, 600);

            // Two documents, two panels, both live — no fixed number of
            // slots to run out of (`TD-72`).
            Assert.NotSame(firstViewer, secondViewer);
            Assert.Equal(2, window.AttachmentViewers.OpenAttachmentIds.Count);

            var firstPanel = window.AttachmentViewers.PanelFor(firstAttachment);
            var secondPanel = window.AttachmentViewers.PanelFor(secondAttachment);
            Assert.NotNull(firstPanel);
            Assert.NotNull(secondPanel);
            Assert.NotEqual(firstPanel, secondPanel);

            // Both are in the real layout tree the workspace renders.
            Assert.True(window.WorkspaceLayout.IsPanelVisible(firstPanel!.Value));
            Assert.True(window.WorkspaceLayout.IsPanelVisible(secondPanel!.Value));

            // Each keeps its own place: paging one does not page the other.
            firstViewer.NextPage();
            Assert.Equal(2, firstViewer.Session!.CurrentPage);
            Assert.Equal(1, secondViewer.Session!.CurrentPage);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task OpeningTheSameDocumentTwice_BringsTheOpenTabForward_RatherThanDuplicatingIt()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-300", "once.pdf", "application/pdf", DocumentPageSourceTests.MultiPagePdf());
            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);

            var opened = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);
            opened.NextPage();

            var reopened = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            Assert.Same(opened, reopened);
            Assert.Single(window.AttachmentViewers.OpenAttachmentIds);

            // And it kept the user's place rather than resetting to page 1.
            Assert.Equal(2, reopened.Session!.CurrentPage);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ClosingAViewer_LeavesTheProjectAndEngineeringContextExactlyWhereItWas()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("Pump Programme", "P-9001");
            await host.ShellNavigator!.OpenProjectAsync(project.Id);
            await host.ShellNavigator!.GoToEngineeringAsync();

            var locationBefore = host.ShellNavigator!.Current;
            var projectBefore = host.ProjectContext!.Current!.Id;

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-400", "context.pdf", "application/pdf", DocumentPageSourceTests.MultiPagePdf());
            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);

            await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            // Opening a document is not navigation.
            Assert.Equal(locationBefore, host.ShellNavigator!.Current);
            Assert.Equal(projectBefore, host.ProjectContext!.Current!.Id);

            var panelId = window.AttachmentViewers.PanelFor(attachmentId)!.Value;
            window.AttachmentViewers.Close(attachmentId);

            // Closing it is not navigation either, and the tab is gone.
            Assert.False(window.WorkspaceLayout.IsPanelVisible(panelId));
            Assert.Empty(window.AttachmentViewers.OpenAttachmentIds);
            Assert.Equal(locationBefore, host.ShellNavigator!.Current);
            Assert.Equal(projectBefore, host.ProjectContext!.Current!.Id);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AnAttachmentWithNoStoredContent_SaysSo_RatherThanShowingAnEmptyPage()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var dispatcher = DispatcherOf(host);
            var domain = DomainOf(host);

            var created = await dispatcher.DispatchAsync(new CreateDocumentObjectCommand(
                DocumentObjectFactoryRegistry.Document, "External Report", identifier: "DOC-500",
                initialContent: "Held in the client's own system.",
                classification: DocumentObjectFactoryRegistry.ExternalReference), CancellationToken.None);
            Assert.True(created.Succeeded, created.Message);

            var document = (await domain.Repository.ListByKindAsync(DocumentObjectFactoryRegistry.Document))
                .Single(o => ((IHasBusinessIdentifier)o).Identifier == "DOC-500");

            // The metadata-only overload: an attachment that names a file
            // this platform does not hold.
            await dispatcher.DispatchAsync(new AttachDocumentCommand(
                document.Id, DocumentObjectFactoryRegistry.Document, "client.pdf", "application/pdf", 5_000_000L), CancellationToken.None);

            var owner = (IHasAttachments)document;
            var attachment = (await owner.GetAttachmentsAsync()).Single();

            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            Assert.Equal(DocumentViewStatus.Missing, viewer.Session!.Status);
            Assert.True(viewer.IsShowingUnavailableState);
            Assert.Contains("No content stored", viewer.UnavailableHeadline);
            Assert.Null(viewer.RenderedPage);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AnAttachmentWhoseContentIsDamaged_SaysDamaged_AndNeverShowsTheBytes()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-600", "damaged.pdf", "application/pdf", DocumentPageSourceTests.MultiPagePdf());

            // Corrupt the stored bytes behind the platform's back, exactly
            // as a bad disk block or a half-copied store directory would.
            var contentStore = (IAttachmentContentStore)host.Services!.GetService(typeof(IAttachmentContentStore));
            await contentStore.SaveAsync(attachmentId, "this is not the file that was attached"u8.ToArray());

            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            Assert.Equal(DocumentViewStatus.Corrupt, viewer.Session!.Status);
            Assert.True(viewer.IsShowingUnavailableState);
            Assert.Contains("damaged", viewer.UnavailableHeadline, StringComparison.OrdinalIgnoreCase);
            Assert.Null(viewer.RenderedPage);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AnIntactFileInAFormatWithNoViewer_SaysThat_RatherThanCallingItDamaged()
    {
        // The distinction that matters to a user: their file is fine, and
        // we cannot draw it. Calling it damaged would be an accusation
        // about their data that is simply untrue.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            byte[] zipContainer = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00, 0xFF, 0xFE, 0x00, 0x1A];
            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DOC-700", "report.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", zipContainer);

            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            Assert.Equal(DocumentViewStatus.Unsupported, viewer.Session!.Status);
            Assert.True(viewer.IsShowingUnavailableState);
            Assert.Contains("cannot be displayed", viewer.UnavailableHeadline);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ATextDatasheet_OpensAndPaginates()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var csv = System.Text.Encoding.UTF8.GetBytes(
                string.Join('\n', Enumerable.Range(0, 120).Select(i => $"Property {i},{i * 10},MPa")));

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DAT-800", "datasheet.csv", "text/csv", csv);

            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            Assert.Equal(DocumentViewStatus.Ready, viewer.Session!.Status);
            Assert.Equal(ViewableDocumentFormat.Text, viewer.Session!.Format);
            Assert.True(viewer.Session!.PageCount > 1);
            Assert.NotNull(viewer.RenderedPage);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ClosingTheTabFromTheLayoutItself_ThenOpeningTheSameDrawingAgain_BringsItBack()
    {
        // The close a user actually performs. `AttachmentViewers.Close` is
        // the launcher's own door; the tab strip's own close button is
        // `TD-72`'s, and it removes the panel from the layout tree without
        // telling the launcher anything. Found by the `TD-80` visual audit:
        // after that close, re-opening the same attachment selected a panel
        // that was no longer there, did nothing at all, and left the
        // drawing unreachable for the rest of the session.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-900", "reopened.pdf", "application/pdf", DocumentPageSourceTests.MultiPagePdf());
            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);

            var opened = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);
            var firstPanel = window.AttachmentViewers.PanelFor(attachmentId)!.Value;
            Assert.True(window.WorkspaceLayout.IsPanelVisible(firstPanel));

            // Exactly what LayoutTabGroupView's own close button raises.
            window.WorkspaceLayout.Apply(tree => tree.Remove(firstPanel));
            Assert.False(window.WorkspaceLayout.Tree.Contains(firstPanel));

            var reopened = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);
            var secondPanel = window.AttachmentViewers.PanelFor(attachmentId)!.Value;

            Assert.NotEqual(firstPanel, secondPanel);
            Assert.True(window.WorkspaceLayout.Tree.Contains(secondPanel));
            Assert.True(window.WorkspaceLayout.IsPanelVisible(secondPanel));
            Assert.Equal(DocumentViewStatus.Ready, reopened.Session!.Status);
            Assert.NotNull(reopened.RenderedPage);
            Assert.Single(window.AttachmentViewers.OpenAttachmentIds);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ADocumentThatCannotBeShown_ShowsNoPageOrZoomControls_WhileOneThatCanDoes()
    {
        // A row of page arrows, a zoom stepper, Fit and 100% over "No
        // content stored" is chrome for a document that is not there: it
        // reads as a working viewer whose right button the user has not
        // found yet, when in fact none of them can do anything. Found by
        // the `TD-80` visual audit.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (readyDocument, readyAttachment) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-901", "shown.pdf", "application/pdf", DocumentPageSourceTests.MultiPagePdf());
            var (readyOwner, ready) = await ResolveAsync(host, readyDocument, readyAttachment);
            var readyViewer = await window.AttachmentViewers.OpenAsync(readyOwner, ready, 800, 600);

            Assert.True(readyViewer.AreViewControlsVisible);
            Assert.Equal("Page 1 of 3", readyViewer.PageIndicatorText);

            byte[] zipContainer = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00, 0xFF, 0xFE, 0x00, 0x1A];
            var (unsupportedDocument, unsupportedAttachment) = await CreateDocumentWithAttachmentAsync(
                host, "DOC-902", "notes.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", zipContainer);
            var (unsupportedOwner, unsupported) = await ResolveAsync(host, unsupportedDocument, unsupportedAttachment);
            var unsupportedViewer = await window.AttachmentViewers.OpenAsync(unsupportedOwner, unsupported, 800, 600);

            Assert.True(unsupportedViewer.IsShowingUnavailableState);
            Assert.False(unsupportedViewer.AreViewControlsVisible);
            Assert.Equal(string.Empty, unsupportedViewer.PageIndicatorText);
            Assert.Equal(string.Empty, unsupportedViewer.ZoomIndicatorText);

            // A viewer that has never been opened is in the same position.
            Assert.False(new DocumentViewerView().AreViewControlsVisible);

            // And the one that can be shown is unaffected by any of it.
            Assert.True(readyViewer.AreViewControlsVisible);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheOpenButtonOnAnAttachmentRow_ExistsInARealEditor_AndOpensTheViewer()
    {
        // The user's actual entry point, and the one nothing tested. The
        // `TD-80` visual audit rendered the real editor and found no Open
        // button on it at all: TryCreate populates the attachment rows
        // before it returns, the rows only carry an Open button when
        // something can handle it, and the shell subscribes afterwards. The
        // viewer was unreachable from the UI on a freshly opened object —
        // every headless test passed, because every one of them called the
        // launcher directly.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-903", "from-the-button.pdf", "application/pdf", DocumentPageSourceTests.MultiPagePdf());

            // Exactly what activating the object in the Explorer does.
            await window.NavigateToObjectAsync(documentId, DocumentObjectFactoryRegistry.Document);

            var editor = window.GetLogicalDescendants().OfType<ObjectEditorView>()
                .Single(e => e.GetLogicalDescendants().OfType<TextBlock>()
                    .Any(t => t.Text?.Contains("from-the-button.pdf", StringComparison.Ordinal) == true));

            var open = editor.GetLogicalDescendants().OfType<Button>()
                .Single(b => b.Content as string == "Open");

            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // Fire-and-forget by design, so wait for the open to land.
            for (var attempt = 0; attempt < 200 && window.AttachmentViewers.OpenAttachmentIds.Count == 0; attempt++)
                await Task.Delay(10);

            Assert.Equal([attachmentId], window.AttachmentViewers.OpenAttachmentIds);

            var panelId = window.AttachmentViewers.PanelFor(attachmentId)!.Value;
            Assert.True(window.WorkspaceLayout.IsPanelVisible(panelId));

            var viewer = window.GetLogicalDescendants().OfType<DocumentViewerView>().Single();
            Assert.Equal(DocumentViewStatus.Ready, viewer.Session!.Status);
            Assert.Equal("from-the-button.pdf", viewer.Session!.FileName);
            Assert.NotNull(viewer.RenderedPage);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TurningToAPageOfAnotherSize_ReFitsToThatPage_RatherThanStretchingItIntoTheLastOnesShape()
    {
        // The multi-page fixture was built with three deliberately
        // different page sizes — A4 portrait, landscape, small square —
        // and nothing at the view level ever asserted what happened on the
        // turn. The `TD-80` visual audit rendered page 2 and found a
        // landscape sheet drawn into the portrait page's rectangle,
        // squashed to roughly half its height: the viewport still carried
        // page 1's content size, which decides both the fit zoom and the
        // rendered width and height the page is drawn at.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-904", "mixed-sheets.pdf", "application/pdf", DocumentPageSourceTests.MultiPagePdf());
            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);

            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            // Page 1 is A4 portrait.
            Assert.Equal(595, viewer.Session!.Viewport.ContentWidth, 0.5);
            Assert.Equal(842, viewer.Session!.Viewport.ContentHeight, 0.5);
            Assert.True(viewer.Session!.Viewport.IsFitted);

            // Page 2 is landscape, and the viewport must say so.
            viewer.NextPage();
            Assert.Equal(842, viewer.Session!.Viewport.ContentWidth, 0.5);
            Assert.Equal(595, viewer.Session!.Viewport.ContentHeight, 0.5);
            Assert.True(viewer.Session!.Viewport.IsFitted);
            Assert.True(viewer.Session!.Viewport.RenderedWidth > viewer.Session!.Viewport.RenderedHeight);

            // Page 3 is a small square, and so is what gets drawn.
            viewer.NextPage();
            Assert.Equal(200, viewer.Session!.Viewport.ContentWidth, 0.5);
            Assert.Equal(200, viewer.Session!.Viewport.ContentHeight, 0.5);
            Assert.Equal(
                viewer.Session!.Viewport.RenderedWidth,
                viewer.Session!.Viewport.RenderedHeight,
                0.5);

            // And the bitmap the control is showing has the page's own
            // shape, not the previous page's — the squash, measured.
            var rendered = viewer.RenderedPage!;
            Assert.Equal(rendered.PixelSize.Width, rendered.PixelSize.Height);

            // Back to page 1, re-fitted to portrait again.
            viewer.GoToPage(1);
            Assert.Equal(595, viewer.Session!.Viewport.ContentWidth, 0.5);
            Assert.Equal(842, viewer.Session!.Viewport.ContentHeight, 0.5);
            Assert.True(viewer.Session!.Viewport.IsFitted);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
