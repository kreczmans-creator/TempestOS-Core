using System.Security.Cryptography;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Desktop.Quotations;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` U3 verifier round 1: <see cref="XeroIssuedPdf"/> decides whether
/// a re-export is the same document by what it says, not by its raw bytes —
/// every render stamps the moment it was made, so with a real (moving) clock
/// no two exports are byte-identical; an attach the content store fails is
/// reported, never thrown; and an invoice keeps its PDF only once it was
/// actually sent.
/// </summary>
public sealed class BadgeIssuedPdfTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 2, 9, 0, 12, 345, TimeSpan.Zero);

    private static QuotationSheetModel Sheet(DateTimeOffset generatedAt, string total = "£500.00") => new(
        IssuerName: "Issuer", ProjectCode: "P-1", ProjectName: "Project", Client: "Client Ltd", Reference: "Q-2026-001",
        QuoteDate: new DateOnly(2026, 10, 1), ValidityDays: 30, Currency: "Gbp",
        Lines: [new QuotationSheetLineRow("Survey", null, null, total)], Total: total, Terms: null, Status: "Approved",
        GeneratedAtUtc: generatedAt, ApplicationVersionText: "TempestOS test", Revision: "R1");

    [Fact]
    public async Task AReExportOfTheSameDocumentLater_KeepsNothingMore_ButAChangedOneIsKept()
    {
        var renderer = new QuotationSheetRenderer();
        var record = new InMemoryAttachments();

        Func<DateTimeOffset, ReadOnlyMemory<byte>> RenderAt(string total) => at => renderer.Render(Sheet(at, total));

        // First export: kept.
        var first = renderer.Render(Sheet(Start)).ToArray();
        Assert.True(await XeroIssuedPdf.AttachAsync(record, "Q-2026-001-R1-quote.pdf", first, RenderAt("£500.00")));
        Assert.Single(record.Files);

        // The same quotation exported 3 min 17.4 s later (a moving clock, as
        // in the shell): different bytes — the footer and creation date move
        // — but the same document, so nothing more is kept or uploaded.
        var later = renderer.Render(Sheet(Start.AddMinutes(3).AddSeconds(17.4))).ToArray();
        Assert.NotEqual(Hash(first), Hash(later));
        Assert.True(await XeroIssuedPdf.AttachAsync(record, "Q-2026-001-R1-quote.pdf", later, RenderAt("£500.00")));
        Assert.Single(record.Files);

        // A changed document (another total) is kept, as the newest.
        var changed = renderer.Render(Sheet(Start.AddMinutes(9), "£650.00")).ToArray();
        Assert.True(await XeroIssuedPdf.AttachAsync(record, "Q-2026-001-R1-quote.pdf", changed, RenderAt("£650.00")));
        Assert.Equal(2, record.Files.Count);
        Assert.Equal(changed, record.Files[^1].Bytes);

        // Without a way to re-render, only identical bytes count as the same.
        var again = renderer.Render(Sheet(Start.AddMinutes(20), "£650.00")).ToArray();
        Assert.True(await XeroIssuedPdf.AttachAsync(record, "Q-2026-001-R1-quote.pdf", again, renderAt: null));
        Assert.Equal(3, record.Files.Count);
    }

    [Fact]
    public void GeneratedAtOf_ReadsTheRenderersOwnCreationDate_ToTheSecond()
    {
        var bytes = new QuotationSheetRenderer().Render(Sheet(Start));

        Assert.Equal(new DateTimeOffset(2026, 10, 2, 9, 0, 12, TimeSpan.Zero), XeroIssuedPdf.GeneratedAtOf(bytes.Span));
        Assert.Null(XeroIssuedPdf.GeneratedAtOf("%PDF-1.4 no info"u8));
        Assert.Null(XeroIssuedPdf.GeneratedAtOf("/CreationDate (D:2026AB)"u8));
    }

    [Fact]
    public async Task AContentStoreThatFailsToWrite_IsReported_NeverThrownOutOfTheExport()
    {
        var record = new InMemoryAttachments { FailWith = new IOException("No space left on device") };

        Assert.False(await XeroIssuedPdf.AttachAsync(record, "Q-1.pdf", new byte[] { 1, 2, 3 }, renderAt: null));

        record.FailWith = new UnauthorizedAccessException("Access denied");
        Assert.False(await XeroIssuedPdf.AttachAsync(record, "Q-1.pdf", new byte[] { 1, 2, 3 }, renderAt: null));
        Assert.Empty(record.Files);
    }

    [Theory]
    [InlineData(InvoiceRequestStatus.Sent, true)]
    [InlineData(InvoiceRequestStatus.Accepted, true)]
    [InlineData(InvoiceRequestStatus.Unknown, true)] // sent, its answer lost: it may well be in Xero
    [InlineData(InvoiceRequestStatus.Draft, false)]
    [InlineData(InvoiceRequestStatus.Sending, false)]
    [InlineData(InvoiceRequestStatus.Reauthorise, false)] // never reached Xero
    [InlineData(InvoiceRequestStatus.Rejected, false)]
    [InlineData(InvoiceRequestStatus.Voided, false)]
    [InlineData(InvoiceRequestStatus.Unavailable, false)]
    public void AnInvoiceKeepsItsPdf_OnlyOnceSent(InvoiceRequestStatus status, bool kept) =>
        Assert.Equal(kept, XeroIssuedPdf.IsSent(status));

    [Fact]
    public void EveryInvoiceStatus_IsDecided() =>
        Assert.Equal(
            [InvoiceRequestStatus.Sent, InvoiceRequestStatus.Accepted, InvoiceRequestStatus.Unknown],
            Enum.GetValues<InvoiceRequestStatus>().Where(XeroIssuedPdf.IsSent).ToArray());

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>A record whose attachments live in memory; <see cref="FailWith"/> makes the content store fail.</summary>
    private sealed class InMemoryAttachments : IHasAttachments
    {
        public List<(Attachment Attachment, byte[] Bytes)> Files { get; } = [];

        public Exception? FailWith { get; set; }

        public Task AttachAsync(IAttachment attachment, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<IAttachment>> GetAttachmentsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IAttachment>>([.. Files.Select(f => (IAttachment)f.Attachment)]);

        public Task<IAttachment> AttachContentAsync(string fileName, string contentType, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            if (FailWith is { } failure)
                throw failure;

            var bytes = content.ToArray();
            var attachment = new Attachment(fileName, contentType, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
            Files.Add((attachment, bytes));
            return Task.FromResult<IAttachment>(attachment);
        }

        public Task<AttachmentContentResult> ReadAttachmentContentAsync(Guid attachmentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Files.FirstOrDefault(f => f.Attachment.Id == attachmentId) is { Bytes: { } bytes }
                ? AttachmentContentResult.Available(bytes)
                : AttachmentContentResult.Missing());
    }
}
