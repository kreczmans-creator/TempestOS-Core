using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Viewing;
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

        var documentId = (await domain.Repository.ListByKindAsync(DocumentObjectFactoryRegistry.Document))
            .Single(entry => entry.Identifier == identifier).Id;
        var document = (await domain.Repository.FindAsync(documentId))!;

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

            var documentEntryId = (await domain.Repository.ListByKindAsync(DocumentObjectFactoryRegistry.Document))
                .Single(entry => entry.Identifier == "DOC-500").Id;
            var document = (await domain.Repository.FindAsync(documentEntryId))!;

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
    public async Task ALargeAttachment_OpensThroughTheRealViewer_ReadThroughTheStreamedPath()
    {
        // `TD-96`: the whole point of streaming a large attachment's bytes
        // is invisible from here — the user still just opens the drawing —
        // which is exactly why this journey, through the real MainWindow
        // and the real content store the composer wires into it
        // (`MainWindowComposer.Coordinators.cs`), is the acceptance test:
        // a multi-megabyte file opens and renders exactly as a small one
        // does.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var drawing = DocumentPageSourceTests.LargePdf(4 * 1024 * 1024);
        Assert.True(drawing.Length > 4 * 1024 * 1024);

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-950", "large-sheet.pdf", "application/pdf", drawing);
            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);

            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            Assert.Equal(DocumentViewStatus.Ready, viewer.Session!.Status);
            Assert.Equal(ViewableDocumentFormat.Pdf, viewer.Session!.Format);
            Assert.Equal(1, viewer.Session!.PageCount);
            Assert.False(viewer.IsShowingUnavailableState);
            Assert.NotNull(viewer.RenderedPage);
            Assert.True(viewer.RenderedPage!.PixelSize.Width > 0);
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

    [AvaloniaFact]
    public async Task ADwgAttachment_OpensExternally_RatherThanReportingUnsupported()
    {
        // TD-99, closed for DWG only tonight (Product Owner decision
        // 2026-09-15 §5): the file is stored and intact — DWG has no
        // renderer in this platform and never will without a licensed
        // SDK — so the viewer says, honestly, that it opens in its own
        // application, offers the real button, and the button really does
        // launch a materialised copy of the real bytes.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            byte[] drawing = "not a real DWG parser would ever accept, but real, intact bytes"u8.ToArray();
            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-970", "pump-housing.dwg", "application/octet-stream", drawing);

            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            Assert.Equal(DocumentViewStatus.Unsupported, viewer.Session!.Status);
            Assert.Equal(ViewableDocumentFormat.ExternalOnly, viewer.Session!.Format);
            Assert.True(viewer.IsShowingUnavailableState);
            Assert.Contains("own application", viewer.UnavailableHeadline, StringComparison.Ordinal);

            // A real copy really was written — never beside the
            // persistence root, which this path is nowhere near.
            var materialisedPath = viewer.Session!.MaterialisedPath;
            Assert.NotNull(materialisedPath);
            Assert.False(materialisedPath!.StartsWith(root, StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(materialisedPath));
            Assert.Equal(drawing, await File.ReadAllBytesAsync(materialisedPath));

            // The launcher is injected, so this proves the button's own
            // action reads that path and hands it off — with no real
            // application actually opening during a test run.
            string? launchedPath = null;
            viewer.ExternalLauncher = path => launchedPath = path;

            viewer.OpenExternally();

            Assert.Equal(materialisedPath, launchedPath);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ADxfAttachment_IsAlsoExternalOnly_ByItsExtensionAlone()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            // A generic content type, not one of DXF's own — the extension
            // has to carry the whole decision, exactly as it does for a
            // real upload path with no better guess to offer.
            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-971", "bracket.dxf", "application/octet-stream", "real bytes"u8.ToArray());

            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            Assert.Equal(ViewableDocumentFormat.ExternalOnly, viewer.Session!.Format);
            Assert.NotNull(viewer.Session!.MaterialisedPath);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AnUnsupportedFormatWithNoRenderer_AlsoOffersOpenExternally()
    {
        // Item 2's own scope: the "Open externally" action is offered for
        // any Unsupported format, not only the named ExternalOnly ones —
        // a docx TempestOS was never going to render either, and the file
        // is exactly as intact.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            byte[] zipContainer = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00, 0xFF, 0xFE, 0x00, 0x1A];
            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DOC-972", "notes.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", zipContainer);

            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            Assert.Equal(ViewableDocumentFormat.Unsupported, viewer.Session!.Format);
            Assert.NotNull(viewer.Session!.MaterialisedPath);

            string? launchedPath = null;
            viewer.ExternalLauncher = path => launchedPath = path;
            viewer.OpenExternally();

            Assert.Equal(viewer.Session!.MaterialisedPath, launchedPath);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ----------------------------------------------------------------
    // `TD-184` (WP 21.5E's defensive review): AttachmentViewerLauncher used
    // to materialise a copy under the attachment's own, completely
    // unexamined file name — so an attachment named "invoice.pdf.exe" whose
    // bytes really were an executable ran as code the instant a user
    // pressed the "Open externally" button any honestly-labelled attachment
    // already offers (Process.Start(UseShellExecute: true) trusts whatever
    // extension the written file happens to carry). Closed here: the
    // written extension is never trusted from the name alone, a dangerous
    // one is refused outright rather than materialised, and the
    // materialised copy lands in a fresh, randomly-named directory a path
    // in the name cannot escape.
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public async Task ADoubleExtensionAttachment_WhoseBytesAreARealPdf_OpensAsThatPdf_NeverReachingExternalOpenAtAll()
    {
        // Magic bytes are consulted first (DocumentFormatDetector's own
        // rule): a file merely *named* invoice.pdf.exe, whose real content
        // is a genuine PDF, opens in-app exactly as pump-head.pdf already
        // does — it never reaches "Open externally" or the materialised
        // temp copy that action depends on, so there is nothing here for a
        // disguised extension to exploit in the first place.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-980", "invoice.pdf.exe", "application/octet-stream", DocumentPageSourceTests.MultiPagePdf());

            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            Assert.Equal(DocumentViewStatus.Ready, viewer.Session!.Status);
            Assert.Equal(ViewableDocumentFormat.Pdf, viewer.Session!.Format);
            Assert.False(viewer.IsShowingUnavailableState);
            Assert.Null(viewer.Session!.MaterialisedPath);
            Assert.NotNull(viewer.RenderedPage);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AnAttachmentWithARealExecutablesBytesAndADangerousExtension_IsRefused_NeverMaterialised()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            // A real Windows PE header (`MZ`) — not a format
            // DocumentFormatDetector's own magic-byte sniff recognises, and
            // an "application/octet-stream" content type gives it nothing
            // more specific to go on either, so it reaches Unsupported
            // exactly as an honestly-named unknown format would.
            byte[] peBytes = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, .. "this is not a real loadable PE, only its header"u8.ToArray()];
            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-981", "totally-a-document.exe", "application/octet-stream", peBytes);

            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            Assert.Equal(DocumentViewStatus.Unsupported, viewer.Session!.Status);
            Assert.True(viewer.IsShowingUnavailableState);

            // Refused outright: no temp copy was ever written, so there is
            // nothing for "Open externally" to launch even if it were
            // shown, and the button is not.
            Assert.Null(viewer.Session!.MaterialisedPath);
            Assert.NotNull(viewer.Session!.ExternalOpenRefusedReason);
            Assert.Contains("run as a program", viewer.Session!.ExternalOpenRefusedReason, StringComparison.Ordinal);
            Assert.Equal("This file was not opened", viewer.UnavailableHeadline);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaTheory]
    [InlineData(".scr")]
    [InlineData(".lnk")]
    [InlineData(".url")]
    [InlineData(".js")]
    [InlineData(".ps1")]
    [InlineData(".sh")]
    public async Task EveryNamedDangerousExtension_IsAlsoRefused(string extension)
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, $"DWG-982-{extension.TrimStart('.')}", $"payload{extension}", "application/octet-stream", "not a document"u8.ToArray());

            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            Assert.Null(viewer.Session!.MaterialisedPath);
            Assert.NotNull(viewer.Session!.ExternalOpenRefusedReason);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task APathTraversalAttemptInTheFileName_CannotEscapeTheMaterialisedDirectory()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var maliciousName = "..\\..\\..\\Windows\\System32\\drivers\\etc\\hosts";
            byte[] content = "harmless content, maliciously named"u8.ToArray();
            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-983", maliciousName, "application/octet-stream", content);

            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            var materialisedPath = viewer.Session!.MaterialisedPath;
            Assert.NotNull(materialisedPath);

            // Written, and written somewhere real — but strictly inside the
            // fresh per-launch temp directory this launcher created for it,
            // never above it: the path separators in the malicious name
            // were stripped, not honoured.
            var expectedRoot = Path.Combine(Path.GetTempPath(), "TempestOS", "Viewer");
            Assert.StartsWith(Path.GetFullPath(expectedRoot), Path.GetFullPath(materialisedPath!), StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(materialisedPath));
            Assert.Equal(content, await File.ReadAllBytesAsync(materialisedPath!));

            // Exactly one path segment beneath the fresh per-launch
            // directory: <launch-guid>/<mangled-name>, nothing above it —
            // the stripped separators from the malicious name survive only
            // as ordinary characters within one harmless file name, never
            // as a second directory level the name climbed out through.
            var relative = Path.GetRelativePath(expectedRoot, materialisedPath!);
            Assert.Equal(2, relative.Split(Path.DirectorySeparatorChar).Length);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ----------------------------------------------------------------
    // TD-98: markup and annotation, driven through the real viewer opened
    // by AttachmentViewers.OpenAsync exactly as the button a user presses
    // does — draw, persist, reload into a second launcher over the same
    // host (proving the round trip goes through the real owner object, not
    // a mock), select, delete, clear.
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public async Task AnAnnotation_IsDrawnPersistedReloadedSelectedAndDeleted_ThroughTheRealViewer()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-990", "annotated.pdf", "application/pdf", DocumentPageSourceTests.MultiPagePdf());
            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);

            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);
            Assert.Equal(DocumentViewStatus.Ready, viewer.Session!.Status);
            Assert.Empty(viewer.AnnotationsOnCurrentPage);

            viewer.SetActiveColor("#12B981");
            var drawn = await viewer.DrawAnnotationAsync(
                AnnotationTool.Rectangle, [new AnnotationPoint(10, 10), new AnnotationPoint(120, 90)]);

            Assert.NotNull(drawn);
            Assert.Equal("#12B981", drawn!.ColorHex);
            Assert.Single(viewer.AnnotationsOnCurrentPage);

            // Persisted through the real owner, not merely held by the
            // view: fetched back independently, the way a second tab or a
            // relaunch would.
            var annotatable = Assert.IsAssignableFrom<IHasAttachmentAnnotations>(owner);
            var stored = Assert.Single(await annotatable.GetAttachmentAnnotationsAsync(attachmentId));
            Assert.Equal(drawn.Id, stored.Id);
            Assert.Equal(AnnotationTool.Rectangle, stored.Tool);

            // Select and delete, through the same public surface a click on
            // the rendered shape and the Delete button drive.
            viewer.SelectAnnotation(drawn.Id);
            Assert.Equal(drawn.Id, viewer.SelectedAnnotationId);

            await viewer.DeleteSelectedAnnotationAsync();

            Assert.Empty(viewer.AnnotationsOnCurrentPage);
            Assert.Null(viewer.SelectedAnnotationId);
            Assert.Empty(await annotatable.GetAttachmentAnnotationsAsync(attachmentId));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ClearPageAnnotationsAsync_RemovesEveryAnnotationOnThatPage_AfterConfirming()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-991", "markup.pdf", "application/pdf", DocumentPageSourceTests.MultiPagePdf());
            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);

            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            await viewer.DrawAnnotationAsync(AnnotationTool.Ellipse, [new AnnotationPoint(0, 0), new AnnotationPoint(30, 30)]);
            await viewer.DrawAnnotationAsync(AnnotationTool.Freehand, [new AnnotationPoint(5, 5), new AnnotationPoint(15, 15), new AnnotationPoint(25, 5)]);
            Assert.Equal(2, viewer.AnnotationsOnCurrentPage.Count);

            // Real confirmation dialog: reached and confirmed exactly as a
            // user pressing "Clear" on it would, not bypassed. The dialog
            // relabels its own confirm button to "Clear" only once
            // ConfirmAsync actually runs — which happens synchronously, up
            // to its own first await, the instant ClearPageAnnotationsAsync
            // is called — so the button is looked up only after that call
            // has started, not before.
            var clearTask = viewer.ClearPageAnnotationsAsync();

            var confirmButton = viewer.GetLogicalDescendants().OfType<Button>()
                .Single(b => AutomationProperties.GetName(b) == "Clear");
            confirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await clearTask;

            Assert.Empty(viewer.AnnotationsOnCurrentPage);

            var annotatable = Assert.IsAssignableFrom<IHasAttachmentAnnotations>(owner);
            Assert.Empty(await annotatable.GetAttachmentAnnotationsAsync(attachmentId));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ClosingTheViewer_DeletesItsOwnMaterialisedDirectory()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "DWG-984", "spec.cad", "application/octet-stream", "real, harmless, unrecognised bytes"u8.ToArray());

            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            var materialisedPath = viewer.Session!.MaterialisedPath;
            Assert.NotNull(materialisedPath);
            var materialisedDirectory = Path.GetDirectoryName(materialisedPath)!;
            Assert.True(Directory.Exists(materialisedDirectory));

            window.AttachmentViewers.Close(attachmentId);

            Assert.False(Directory.Exists(materialisedDirectory));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
    // `WP 21.5F` (Offensive Security Audit, OSA-02), ported at merge onto
    // `WP 21.4A`'s launcher: the audit's own two proofs that the viewer's
    // refusal-based fix did not carry — a fresh directory per open, and
    // bidi-override characters stripped from the written name.
    [AvaloniaFact]
    public async Task OSA02_TwoOpensOfTheSameAttachment_MaterialiseToDifferentDirectories()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "OSA-02C", "part.dwg", "application/octet-stream", "real, intact bytes"u8.ToArray());

            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);

            var firstViewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);
            var firstPath = firstViewer.Session!.MaterialisedPath;
            Assert.NotNull(firstPath);

            window.AttachmentViewers.Close(attachmentId);

            var secondViewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);
            var secondPath = secondViewer.Session!.MaterialisedPath;
            Assert.NotNull(secondPath);

            Assert.NotEqual(Path.GetDirectoryName(firstPath), Path.GetDirectoryName(secondPath));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task OSA02_AnRtloDisguisedFileName_HasItsBidiControlCharactersStripped()
    {
        // An RTLO character (U+202E) can make a name render to a human as if
        // its extension were harmless. Under `WP 21.4A`'s launcher a dangerous
        // extension is refused outright, so this proof uses a drawing (the
        // one format that is materialised for the shell) and checks the
        // written name carries no bidi control character at all.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var fileName = "part‮gwd.dwg";
            var (documentId, attachmentId) = await CreateDocumentWithAttachmentAsync(
                host, "OSA-02D", fileName, "application/octet-stream", "not really this format"u8.ToArray());

            var (owner, attachment) = await ResolveAsync(host, documentId, attachmentId);
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            var materialisedPath = viewer.Session!.MaterialisedPath;
            Assert.NotNull(materialisedPath);
            Assert.DoesNotContain('‮', Path.GetFileName(materialisedPath));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
