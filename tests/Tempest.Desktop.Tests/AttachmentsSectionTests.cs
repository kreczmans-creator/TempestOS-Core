using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Tempest.Workspace;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Files;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Viewing;
using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Viewing;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 19.4B` — "Real files on every Attachments section": <c>Browse…</c>
/// (formerly <c>Add File…</c>, gated to <see cref="Core.Evidence.Evidence.CanonicalKind"/>
/// alone) now shows for every <see cref="IHasAttachments"/> Kind, a drop
/// zone sits alongside it, and the typed File Name/Content Type/Size form
/// moves under a collapsed "Record a reference without the file" expander.
/// Drives <see cref="ObjectEditorView"/> directly against real Calculation,
/// Part and Document objects over a real <see cref="WorkspaceHost"/> — no
/// <c>MainWindow</c>, matching <c>AttachmentContentAcceptanceTests</c>'s own
/// "real command dispatch, real domain context, no mock" discipline.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class AttachmentsSectionTests
{
    [AvaloniaFact]
    public async Task CalculationEditor_ShowsBrowseAndTheDropZone()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = DomainOf(host);
            var commandDispatcher = DispatcherOf(host);

            var calculation = await CreateObjectAsync(commandDispatcher, domainContext, CalculationObjectFactoryRegistry.CalculationKind, "WP 19.4B calculation");

            var editor = ObjectEditorView.TryCreate(
                calculation.Id, CalculationObjectFactoryRegistry.CalculationKind, domainContext, host.Manager!, (_, _) => { },
                commandDispatcher, evidenceSupport: BuildEvidenceSupport(new StubFilePicker()))!;
            Assert.NotNull(editor);

            var attachmentsExpander = Section(editor, "Attachments");
            Assert.True(attachmentsExpander.IsVisible);

            var browseButton = attachmentsExpander.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Browse…"));
            Assert.True(browseButton.IsVisible);

            var dropZone = attachmentsExpander.GetLogicalDescendants().OfType<Border>()
                .Single(b => Equals(Avalonia.Automation.AutomationProperties.GetName(b), "Drop a file here, or Browse…"));
            Assert.True(DragDrop.GetAllowDrop(dropZone));

            // The Browse button lives inside the drop zone itself — one
            // control satisfies both halves of Scope §1/§2.
            Assert.Contains(browseButton, dropZone.GetLogicalDescendants());
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Browse_WithStubFilePicker_AttachesTheRealFile_SizeHashAuditAndMessage()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = DomainOf(host);
            var commandDispatcher = DispatcherOf(host);

            var calculation = await CreateObjectAsync(commandDispatcher, domainContext, CalculationObjectFactoryRegistry.CalculationKind, "WP 19.4B Browse calculation");

            var tempFile = CreateTempFile("wp194b-workbook", ".xlsx", 4096);
            var expectedBytes = await File.ReadAllBytesAsync(tempFile);
            var expectedHash = Convert.ToHexStringLower(SHA256.HashData(expectedBytes));

            var picker = new StubFilePicker();
            picker.EnqueuePick(tempFile);
            var editor = ObjectEditorView.TryCreate(
                calculation.Id, CalculationObjectFactoryRegistry.CalculationKind, domainContext, host.Manager!, (_, _) => { },
                commandDispatcher, evidenceSupport: BuildEvidenceSupport(picker))!;

            string? completedMessage = null;
            editor.ActionCompleted += (message, _) => completedMessage = message;

            var attachmentsExpander = Section(editor, "Attachments");
            var browseButton = attachmentsExpander.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Browse…"));
            browseButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // `TD-119`'s own remedy: an `async void` click handler over real
            // disk/domain I/O — bounded poll re-reading real state.
            IReadOnlyList<IAttachment> attachments = [];
            var deadline = DesktopTestHelpers.Deadline(2);
            while (DateTime.UtcNow < deadline)
            {
                var reread = await domainContext.Repository.FindAsync(calculation.Id);
                attachments = await ((IHasAttachments)reread!).GetAttachmentsAsync();
                if (attachments.Count > 0)
                    break;
                await Task.Delay(10);
            }

            var attachment = Assert.Single(attachments);
            Assert.Equal(Path.GetFileName(tempFile), attachment.FileName);
            Assert.Equal(expectedBytes.LongLength, attachment.SizeInBytes);
            Assert.Equal(expectedHash, attachment.ContentHash);

            var auditQuery = (IAuditQuery)host.Services!.GetService(typeof(IAuditQuery));
            var audit = await auditQuery.QueryAsync(new AuditQueryCriteria(objectId: calculation.Id));
            Assert.NotEmpty(audit);

            Assert.NotNull(completedMessage);
            Assert.Contains("attached", completedMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AttachFilesAsync_WithTwoPaths_GivesTwoAttachments()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = DomainOf(host);
            var commandDispatcher = DispatcherOf(host);

            var calculation = await CreateObjectAsync(commandDispatcher, domainContext, CalculationObjectFactoryRegistry.CalculationKind, "WP 19.4B drop calculation");

            var editor = ObjectEditorView.TryCreate(
                calculation.Id, CalculationObjectFactoryRegistry.CalculationKind, domainContext, host.Manager!, (_, _) => { }, commandDispatcher)!;
            Assert.NotNull(editor);

            var fileA = CreateTempFile("wp194b-drop-a", ".pdf", 1024);
            var fileB = CreateTempFile("wp194b-drop-b", ".bin", 2048);

            // The method the real `DragDrop.DropEvent` handler funnels
            // into — called directly, the same shape
            // `EvidenceWorkspaceJourneyTests.DroppingTwoFiles_...` already
            // established for the identical "headless Avalonia cannot raise
            // a real OS drop reliably" reason, except this one needs no
            // reflection: `AttachFilesAsync` is `internal`
            // (`InternalsVisibleTo("Tempest.Desktop.Tests")`), not
            // `private`.
            await editor.AttachFilesAsync([fileA, fileB]);

            var reread = await domainContext.Repository.FindAsync(calculation.Id);
            var attachments = await ((IHasAttachments)reread!).GetAttachmentsAsync();
            Assert.Equal(2, attachments.Count);
            Assert.Contains(attachments, a => a.FileName == Path.GetFileName(fileA));
            Assert.Contains(attachments, a => a.FileName == Path.GetFileName(fileB));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaTheory]
    [InlineData(MechanicalObjectFactoryRegistry.Part)]
    [InlineData(DocumentObjectFactoryRegistry.Document)]
    public async Task PartAndDocumentEditors_BehaveTheSameAsCalculation(string kind)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = DomainOf(host);
            var commandDispatcher = DispatcherOf(host);

            var target = await CreateObjectAsync(commandDispatcher, domainContext, kind, $"WP 19.4B {kind}");

            var editor = ObjectEditorView.TryCreate(
                target.Id, kind, domainContext, host.Manager!, (_, _) => { },
                commandDispatcher, evidenceSupport: BuildEvidenceSupport(new StubFilePicker()))!;
            Assert.NotNull(editor);

            var attachmentsExpander = Section(editor, "Attachments");
            Assert.True(attachmentsExpander.IsVisible);
            var browseButton = attachmentsExpander.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Browse…"));
            Assert.True(browseButton.IsVisible);

            var file = CreateTempFile($"wp194b-{kind}", ".txt", 512);
            await editor.AttachFilesAsync([file]);

            var reread = await domainContext.Repository.FindAsync(target.Id);
            var attachments = await ((IHasAttachments)reread!).GetAttachmentsAsync();
            Assert.Single(attachments, a => a.FileName == Path.GetFileName(file));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// Scope §4 / Kill switch: "confirm a Calculation's attachment opens" —
    /// the exact real-UI path
    /// `TheOpenButtonOnAnAttachmentRow_ExistsInARealEditor_AndOpensTheViewer`
    /// (`DocumentViewerAcceptanceTests`) already proves for a Document,
    /// driven here against a Calculation instead: attach through the real
    /// command (bytes included, no picker needed for this one), open the
    /// real editor, click the row's own Open button, and confirm the same
    /// <see cref="Tempest.Desktop.Viewing.AttachmentViewerLauncher"/> a
    /// Calculation attachment opens through — unchanged by this Work
    /// Package, exactly as the seam map predicted.
    /// </summary>
    [AvaloniaFact]
    public async Task CalculationAttachment_OpensThroughTheRealViewer_JustLikeADocumentsDoes()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = DomainOf(host);
            var commandDispatcher = DispatcherOf(host);

            var calculation = await CreateObjectAsync(commandDispatcher, domainContext, CalculationObjectFactoryRegistry.CalculationKind, "WP 19.4B viewer calculation");
            var content = DocumentPageSourceTests.MultiPagePdf();
            var attached = await commandDispatcher.DispatchAsync(
                new AttachDocumentCommand(calculation.Id, CalculationObjectFactoryRegistry.CalculationKind, "calc-attachment.pdf", "application/pdf", content),
                CancellationToken.None);
            Assert.True(attached.Succeeded, attached.Message);
            var attachmentId = (await ((IHasAttachments)calculation).GetAttachmentsAsync()).Single(a => a.FileName == "calc-attachment.pdf").Id;

            var window = new MainWindow(host);
            await window.NavigateToObjectAsync(calculation.Id, CalculationObjectFactoryRegistry.CalculationKind);

            var editor = window.GetLogicalDescendants().OfType<ObjectEditorView>()
                .Single(e => e.GetLogicalDescendants().OfType<TextBlock>()
                    .Any(t => t.Text?.Contains("calc-attachment.pdf", StringComparison.Ordinal) == true));

            var open = editor.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "Open");
            open.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            for (var attempt = 0; attempt < 200 && window.AttachmentViewers.OpenAttachmentIds.Count == 0; attempt++)
                await Task.Delay(10);

            Assert.Equal([attachmentId], window.AttachmentViewers.OpenAttachmentIds);

            var viewer = window.GetLogicalDescendants().OfType<DocumentViewerView>().Single();
            Assert.Equal(DocumentViewStatus.Ready, viewer.Session!.Status);
            Assert.Equal("calc-attachment.pdf", viewer.Session!.FileName);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ObjectThatIsNotIHasAttachments_ShowsNoAttachmentsSection()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = DomainOf(host);
            var commandDispatcher = DispatcherOf(host);
            var calculation = await CreateObjectAsync(commandDispatcher, domainContext, CalculationObjectFactoryRegistry.CalculationKind, "WP 19.4B gate probe");

            var editor = ObjectEditorView.TryCreate(
                calculation.Id, CalculationObjectFactoryRegistry.CalculationKind, domainContext, host.Manager!, (_, _) => { }, commandDispatcher)!;
            Assert.NotNull(editor);
            Assert.True(Section(editor, "Attachments").IsVisible); // sanity: a real IHasAttachments target shows it

            // `EngineeringObjectBase`'s own remarks: "the shared plumbing
            // every concrete canonical object class derives from —
            // implements every facet interface generically", `IHasAttachments`
            // included — so no live Kind reachable through the real
            // Repository can exercise the gate's negative branch (confirmed
            // by inspection: Calculation, Part, Document, Project and every
            // other canonical class derive from it). Proven instead the way
            // `PopulateAttachmentsAsync` (the one method the gate actually
            // lives in) can be driven directly, mirroring this suite's own
            // `GetPrivateMethod` reflection precedent
            // (`EvidenceWorkspaceJourneyTests.DroppingTwoFiles_...`), with a
            // bare `IEngineeringObject` that implements nothing else.
            var populateAttachments = GetPrivateMethod(editor, "PopulateAttachmentsAsync");
            await (Task)populateAttachments.Invoke(editor, [new BareEngineeringObject()])!;

            Assert.False(Section(editor, "Attachments").IsVisible);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private sealed class BareEngineeringObject : IEngineeringObject
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Kind => "WP194BNotAttachable";
        public int CurrentRevisionNumber => 1;
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public string BusinessIdentifier => Id.ToString();
    }

    private static EngineeringDomainContext DomainOf(WorkspaceHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

    private static ICommandDispatcher DispatcherOf(WorkspaceHost host) =>
        (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

    private static Expander Section(ObjectEditorView editor, string header) =>
        editor.GetLogicalDescendants().OfType<Expander>().Single(e => Equals(e.Header, header));

    private static EvidenceEditorSupport BuildEvidenceSupport(IFilePicker picker) => new(
        picker,
        _ => Task.FromResult<EvidenceLibraryRow?>(null),
        _ => Task.FromResult<DeclaredFigureInput?>(null),
        _ => Task.FromResult<Guid?>(null),
        _ => Task.FromResult<CheckEntryInput?>(null),
        _ => Task.FromResult<IssueEntryInput?>(null));

    private static async Task<IEngineeringObject> CreateObjectAsync(ICommandDispatcher dispatcher, EngineeringDomainContext domain, string kind, string displayName)
    {
        var identifier = $"WP194B-{Guid.NewGuid():N}";
        CommandResult result = kind switch
        {
            CalculationObjectFactoryRegistry.CalculationKind =>
                await dispatcher.DispatchAsync(new CreateCalculationObjectCommand(kind, displayName, identifier), CancellationToken.None),
            MechanicalObjectFactoryRegistry.Part =>
                await dispatcher.DispatchAsync(new CreateMechanicalObjectCommand(kind, displayName, identifier), CancellationToken.None),
            DocumentObjectFactoryRegistry.Document =>
                await dispatcher.DispatchAsync(new CreateDocumentObjectCommand(kind, displayName, identifier), CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported Kind for this test's own object creation helper."),
        };
        Assert.True(result.Succeeded, result.Message);

        var createdId = (await domain.Repository.ListByKindAsync(kind)).Single(entry => entry.Identifier == identifier).Id;
        return (await domain.Repository.FindAsync(createdId))!;
    }

    private static string CreateTempFile(string name, string extension, int sizeBytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}{extension}");
        var bytes = new byte[sizeBytes];
        new Random(42).NextBytes(bytes);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static System.Reflection.MethodInfo GetPrivateMethod(object instance, string name) =>
        instance.GetType().GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method '{name}' not found on {instance.GetType().Name}.");
}
