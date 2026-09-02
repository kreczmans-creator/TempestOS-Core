using Avalonia.Headless.XUnit;
using Tempest.App.Workspace.Documents;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `TD-31`'s own Definition of Done, driven through the real production
/// composition over real <see cref="WorkspaceHost"/> lifetimes sharing one
/// persistence root: <b>a user can attach a real file to engineering work,
/// close TempestOS, relaunch it, and open that same file.</b>
/// </summary>
/// <remarks>
/// <para>
/// Nothing here inspects the store on disk, and nothing constructs a
/// content store by hand. Attachments are made through the same
/// <see cref="AttachDocumentCommand"/> the Documents workspace dispatches,
/// and read back through the rehydrated object the running application
/// hands out — so a pass means the wired-up application does this, not
/// that a class in isolation could.
/// </para>
/// <para>
/// The content is realistic on purpose. A store that decoded bytes as
/// text, stopped at a NUL or normalised a line ending would round-trip an
/// empty array perfectly and lose every real document; the files here
/// carry the byte patterns that catch it.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class AttachmentContentAcceptanceTests
{
    private static EngineeringDomainContext DomainOf(WorkspaceHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

    private static ICommandDispatcher DispatcherOf(WorkspaceHost host) =>
        (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

    /// <summary>A structurally real PDF, including the high-byte comment line a real writer emits.</summary>
    private static byte[] SpecificationPdf()
    {
        var text =
            "%PDF-1.7\n" +
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n" +
            "trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n0\n%%EOF\n";

        var bytes = new List<byte>(System.Text.Encoding.ASCII.GetBytes(text));
        bytes.InsertRange(9, new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A });
        return [.. bytes];
    }

    /// <summary>Every byte value twice, in both directions — the exhaustive fidelity payload.</summary>
    private static byte[] EveryByteValue()
    {
        var bytes = new byte[512];
        for (var i = 0; i < 256; i++)
        {
            bytes[i] = (byte)i;
            bytes[511 - i] = (byte)i;
        }

        return bytes;
    }

    [AvaloniaFact]
    public async Task Journey_AttachRealFiles_Relaunch_AndOpenTheSameFilesBack()
    {
        // One persistence root stands for one machine across two launches.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var pdf = SpecificationPdf();
        var blob = EveryByteValue();

        Guid documentId;
        Guid pdfAttachmentId;
        Guid blobAttachmentId;
        string pdfHash;

        // ============================================================
        // FIRST LAUNCH — create a Document and attach real files to it
        // ============================================================
        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            var dispatcher = DispatcherOf(first);
            var domain = DomainOf(first);

            var created = await dispatcher.DispatchAsync(new CreateDocumentObjectCommand(
                DocumentObjectFactoryRegistry.Document,
                "Pump Head Specification",
                identifier: "DOC-9100",
                initialContent: "The specification this attachment belongs to.",
                classification: DocumentObjectFactoryRegistry.Specification), CancellationToken.None);
            Assert.True(created.Succeeded, created.Message);

            var document = (await domain.Repository.ListByKindAsync(DocumentObjectFactoryRegistry.Document))
                .Single(o => ((IHasBusinessIdentifier)o).Identifier == "DOC-9100");
            documentId = document.Id;

            // --- Attach through the real workspace command --------------
            var attachedPdf = await dispatcher.DispatchAsync(new AttachDocumentCommand(
                documentId, DocumentObjectFactoryRegistry.Document, "specification.pdf", "application/pdf", pdf), CancellationToken.None);
            Assert.True(attachedPdf.Succeeded, attachedPdf.Message);

            var attachedBlob = await dispatcher.DispatchAsync(new AttachDocumentCommand(
                documentId, DocumentObjectFactoryRegistry.Document, "every-byte.bin", "application/octet-stream", blob), CancellationToken.None);
            Assert.True(attachedBlob.Succeeded, attachedBlob.Message);

            var attachments = await ((IHasAttachments)document).GetAttachmentsAsync();
            Assert.Equal(2, attachments.Count);

            var pdfAttachment = attachments.Single(a => a.FileName == "specification.pdf");
            var blobAttachment = attachments.Single(a => a.FileName == "every-byte.bin");
            pdfAttachmentId = pdfAttachment.Id;
            blobAttachmentId = blobAttachment.Id;
            pdfHash = pdfAttachment.ContentHash!;

            // The metadata describes the bytes actually stored, because it
            // was derived from them rather than asserted alongside them.
            Assert.Equal(pdf.LongLength, pdfAttachment.SizeInBytes);
            Assert.NotNull(pdfAttachment.ContentHash);
            Assert.Equal(blob.LongLength, blobAttachment.SizeInBytes);
            Assert.NotNull(blobAttachment.ContentHash);

            // Readable in the session that wrote it, before any restart.
            var live = await ((IHasAttachments)document).ReadAttachmentContentAsync(pdfAttachmentId);
            Assert.Equal(AttachmentContentStatus.Available, live.Status);
            Assert.Equal(pdf, live.Bytes);
        }
        finally
        {
            await first.ShutdownAsync();
            await first.DisposeAsync();
        }

        // ============================================================
        // SECOND LAUNCH — a new process shape over the same root
        // ============================================================
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var domain = DomainOf(second);

            // The document came back through real rehydration, not through
            // anything this test kept alive.
            var document = await domain.Repository.FindAsync(documentId);
            Assert.NotNull(document);

            var attachable = Assert.IsAssignableFrom<IHasAttachments>(document);
            var attachments = await attachable.GetAttachmentsAsync();

            // --- Metadata survived, with its identities intact ----------
            Assert.Equal(2, attachments.Count);
            var pdfAttachment = attachments.Single(a => a.Id == pdfAttachmentId);
            var blobAttachment = attachments.Single(a => a.Id == blobAttachmentId);

            Assert.Equal("specification.pdf", pdfAttachment.FileName);
            Assert.Equal("application/pdf", pdfAttachment.ContentType);
            Assert.Equal(pdf.LongLength, pdfAttachment.SizeInBytes);
            Assert.Equal(pdfHash, pdfAttachment.ContentHash);

            // --- And so did the bytes -----------------------------------
            var recoveredPdf = await attachable.ReadAttachmentContentAsync(pdfAttachmentId);
            Assert.Equal(AttachmentContentStatus.Available, recoveredPdf.Status);
            Assert.Equal(pdf, recoveredPdf.Bytes);

            var recoveredBlob = await attachable.ReadAttachmentContentAsync(blobAttachmentId);
            Assert.Equal(AttachmentContentStatus.Available, recoveredBlob.Status);
            Assert.Equal(blob, recoveredBlob.Bytes);
            Assert.Equal(blobAttachment.SizeInBytes, recoveredBlob.Bytes.LongLength);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AfterARelaunch_AskingForAnAttachmentThatDoesNotExist_ReportsMissing_NotAnError()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid documentId;

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            var dispatcher = DispatcherOf(first);
            var domain = DomainOf(first);

            var created = await dispatcher.DispatchAsync(new CreateDocumentObjectCommand(
                DocumentObjectFactoryRegistry.Document, "Empty Document", identifier: "DOC-9200",
                initialContent: "No attachments here."), CancellationToken.None);
            Assert.True(created.Succeeded, created.Message);

            documentId = (await domain.Repository.ListByKindAsync(DocumentObjectFactoryRegistry.Document))
                .Single(o => ((IHasBusinessIdentifier)o).Identifier == "DOC-9200").Id;
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
            var document = await DomainOf(second).Repository.FindAsync(documentId);
            var attachable = Assert.IsAssignableFrom<IHasAttachments>(document!);

            var result = await attachable.ReadAttachmentContentAsync(Guid.NewGuid());

            Assert.Equal(AttachmentContentStatus.Missing, result.Status);
            Assert.Empty(result.Bytes);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AMetadataOnlyAttachment_StillWorks_AndReportsItsContentMissing()
    {
        // The pre-`TD-31` shape has to keep working: an attachment may name
        // a file this platform does not hold, and must say so rather than
        // failing or pretending.
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid documentId;
        Guid attachmentId;

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            var dispatcher = DispatcherOf(first);
            var domain = DomainOf(first);

            var created = await dispatcher.DispatchAsync(new CreateDocumentObjectCommand(
                DocumentObjectFactoryRegistry.Document, "Externally Held Report", identifier: "DOC-9300",
                initialContent: "Held in the client's own system.",
                classification: DocumentObjectFactoryRegistry.ExternalReference), CancellationToken.None);
            Assert.True(created.Succeeded, created.Message);

            documentId = (await domain.Repository.ListByKindAsync(DocumentObjectFactoryRegistry.Document))
                .Single(o => ((IHasBusinessIdentifier)o).Identifier == "DOC-9300").Id;

            // The metadata-only overload, unchanged since `WP 9.4A`.
            var attached = await dispatcher.DispatchAsync(new AttachDocumentCommand(
                documentId, DocumentObjectFactoryRegistry.Document, "client-report.pdf", "application/pdf", 4_200_000L), CancellationToken.None);
            Assert.True(attached.Succeeded, attached.Message);

            var document = await domain.Repository.FindAsync(documentId);
            var attachment = (await ((IHasAttachments)document!).GetAttachmentsAsync()).Single();
            attachmentId = attachment.Id;

            Assert.Null(attachment.ContentHash);
            Assert.Equal(4_200_000L, attachment.SizeInBytes);
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
            var document = await DomainOf(second).Repository.FindAsync(documentId);
            var attachable = Assert.IsAssignableFrom<IHasAttachments>(document!);

            var attachment = (await attachable.GetAttachmentsAsync()).Single(a => a.Id == attachmentId);
            Assert.Null(attachment.ContentHash);
            Assert.Equal(4_200_000L, attachment.SizeInBytes);

            var result = await attachable.ReadAttachmentContentAsync(attachmentId);
            Assert.Equal(AttachmentContentStatus.Missing, result.Status);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AttachmentContentSurvivesTwoRelaunches_AndFurtherWorkInBetween()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var firstFile = SpecificationPdf();
        var secondFile = EveryByteValue();

        Guid documentId;
        Guid firstAttachmentId;

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            var dispatcher = DispatcherOf(first);
            var domain = DomainOf(first);

            var created = await dispatcher.DispatchAsync(new CreateDocumentObjectCommand(
                DocumentObjectFactoryRegistry.Document, "Procedure", identifier: "DOC-9400",
                initialContent: "Step one.",
                classification: DocumentObjectFactoryRegistry.Procedure), CancellationToken.None);
            Assert.True(created.Succeeded, created.Message);

            documentId = (await domain.Repository.ListByKindAsync(DocumentObjectFactoryRegistry.Document))
                .Single(o => ((IHasBusinessIdentifier)o).Identifier == "DOC-9400").Id;

            await dispatcher.DispatchAsync(new AttachDocumentCommand(
                documentId, DocumentObjectFactoryRegistry.Document, "procedure.pdf", "application/pdf", firstFile), CancellationToken.None);

            var document = await domain.Repository.FindAsync(documentId);
            firstAttachmentId = (await ((IHasAttachments)document!).GetAttachmentsAsync()).Single().Id;
        }
        finally
        {
            await first.ShutdownAsync();
            await first.DisposeAsync();
        }

        // --- Second lifetime: attach a second file to the recovered object
        Guid secondAttachmentId;
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var dispatcher = DispatcherOf(second);

            await dispatcher.DispatchAsync(new AttachDocumentCommand(
                documentId, DocumentObjectFactoryRegistry.Document, "appendix.bin", "application/octet-stream", secondFile), CancellationToken.None);

            var document = await DomainOf(second).Repository.FindAsync(documentId);
            var attachments = await ((IHasAttachments)document!).GetAttachmentsAsync();
            Assert.Equal(2, attachments.Count);
            secondAttachmentId = attachments.Single(a => a.FileName == "appendix.bin").Id;
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }

        // --- Third lifetime: both files are still there, still intact ----
        var third = new WorkspaceHost(root);
        try
        {
            await third.StartAsync();
            var document = await DomainOf(third).Repository.FindAsync(documentId);
            var attachable = Assert.IsAssignableFrom<IHasAttachments>(document!);

            var recoveredFirst = await attachable.ReadAttachmentContentAsync(firstAttachmentId);
            Assert.Equal(AttachmentContentStatus.Available, recoveredFirst.Status);
            Assert.Equal(firstFile, recoveredFirst.Bytes);

            var recoveredSecond = await attachable.ReadAttachmentContentAsync(secondAttachmentId);
            Assert.Equal(AttachmentContentStatus.Available, recoveredSecond.Status);
            Assert.Equal(secondFile, recoveredSecond.Bytes);
        }
        finally
        {
            await third.ShutdownAsync();
            await third.DisposeAsync();
        }
    }
}
