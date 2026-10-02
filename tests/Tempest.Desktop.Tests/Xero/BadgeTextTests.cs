using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` U3: every badge state X6 can answer reads as the words the
/// design promises — and the invoice draft says D3/D4's own sentence.
/// </summary>
public sealed class BadgeTextTests
{
    private static XeroDocumentSyncStatus Status(XeroSyncBadge badge, string? reason = null, string? xeroStatus = null, bool canSendAgain = false, Guid? retry = null) =>
        new(new XeroSyncStatus(badge, reason, "N-1", xeroStatus, null, retry), XeroSyncService.LabelFor(badge), retry is not null, canSendAgain);

    [Theory]
    [InlineData(XeroDocumentKind.Quote)]
    [InlineData(XeroDocumentKind.Invoice)]
    [InlineData(XeroDocumentKind.PurchaseOrder)]
    [InlineData(XeroDocumentKind.ExpenseBill)]
    public void NotSent_Queued_Paid_AwaitingPayment_Waiting_ReadTheSameForEveryKind(XeroDocumentKind kind)
    {
        Assert.Equal(XeroBadgeText.NotSent, XeroBadgeText.Describe(kind, Status(XeroSyncBadge.NotSent)).Text);
        Assert.Equal(XeroBadgeText.Queued, XeroBadgeText.Describe(kind, Status(XeroSyncBadge.Queued)).Text);
        Assert.Equal(XeroBadgeText.Paid, XeroBadgeText.Describe(kind, Status(XeroSyncBadge.Paid)).Text);
        Assert.Equal(XeroBadgeText.AwaitingPayment, XeroBadgeText.Describe(kind, Status(XeroSyncBadge.AwaitingPayment)).Text);
        Assert.Equal("Waiting for authorisation", XeroBadgeText.Describe(kind, Status(XeroSyncBadge.NeedsReauthorisation, "Xero needs re-authorising.")).Text);
        Assert.Equal(XeroBadgeTone.Attention, XeroBadgeText.Describe(kind, Status(XeroSyncBadge.NeedsReauthorisation)).Tone);
    }

    [Fact]
    public void InvoiceDraft_SaysReviewAndSendFromXero_D3D4()
    {
        var shown = XeroBadgeText.Describe(XeroDocumentKind.Invoice, Status(XeroSyncBadge.InXeroDraft, "Draft in Xero — review and send from Xero.", "DRAFT"));

        Assert.Equal("Draft in Xero — review and send from Xero", shown.Text);
        Assert.Null(shown.Detail); // the note repeats the text: not shown twice
    }

    [Fact]
    public void InvoiceAwaitingApprovalInXero_KeepsTheDraftWords_WithTheNote()
    {
        var shown = XeroBadgeText.Describe(XeroDocumentKind.Invoice, Status(XeroSyncBadge.InXeroDraft, "Awaiting approval in Xero.", "SUBMITTED"));

        Assert.Equal(XeroBadgeText.InvoiceDraft, shown.Text);
        Assert.Equal("Awaiting approval in Xero.", shown.Detail);
    }

    [Theory]
    [InlineData(XeroDocumentKind.Quote)]
    [InlineData(XeroDocumentKind.PurchaseOrder)]
    [InlineData(XeroDocumentKind.ExpenseBill)]
    public void OtherDrafts_ReadInXeroDraft(XeroDocumentKind kind) =>
        Assert.Equal("In Xero (draft)", XeroBadgeText.Describe(kind, Status(XeroSyncBadge.InXeroDraft, null, "DRAFT")).Text);

    [Theory]
    [InlineData("SENT", "Sent in Xero")]
    [InlineData("ACCEPTED", "Accepted in Xero")]
    [InlineData("DECLINED", "Declined in Xero")]
    [InlineData("INVOICED", "Invoiced in Xero")]
    [InlineData("SOMETHING-NEW", "In Xero")]
    public void Quote_PastDraft_SaysXerosOwnStatus(string xeroStatus, string expected) =>
        Assert.Equal(expected, XeroBadgeText.Describe(XeroDocumentKind.Quote, Status(XeroSyncBadge.InXero, null, xeroStatus)).Text);

    [Fact]
    public void Quote_InvoicedInXero_NeedsAttention_AndShowsTheDriftNote()
    {
        const string note = "Invoiced in Xero — raising it from TempestOS too would bill twice.";
        var shown = XeroBadgeText.Describe(XeroDocumentKind.Quote, Status(XeroSyncBadge.InXero, note, "INVOICED"));

        Assert.Equal(XeroBadgeTone.Attention, shown.Tone);
        Assert.Equal(note, shown.Detail);
    }

    [Fact]
    public void PurchaseOrder_PastDraft_ReadsInXero_WithXerosNote() =>
        Assert.Equal(
            new XeroBadgePresentation("In Xero", "Billed in Xero.", XeroBadgeTone.Good),
            XeroBadgeText.Describe(XeroDocumentKind.PurchaseOrder, Status(XeroSyncBadge.InXero, "Billed in Xero.", "BILLED")));

    [Fact]
    public void Failed_ShowsTheReason()
    {
        var shown = XeroBadgeText.Describe(XeroDocumentKind.Invoice, Status(XeroSyncBadge.Failed, "Xero refused: Contact is archived.", retry: Guid.NewGuid()));

        Assert.Equal("Failed", shown.Text);
        Assert.Equal("Xero refused: Contact is archived.", shown.Detail);
        Assert.Equal(XeroBadgeTone.Attention, shown.Tone);
    }

    [Fact]
    public void CannotTell_IsItsOwnBadge_FromX5sOwnReason()
    {
        var reason = XeroPurchasingOwnership.CannotTell("Purchase order", "order", "PO-2026-001", "key expired", sourceGone: false).Reason;
        var shown = XeroBadgeText.Describe(XeroDocumentKind.PurchaseOrder, Status(XeroSyncBadge.Failed, reason, retry: Guid.NewGuid()));

        Assert.Equal("Can't tell", shown.Text);
        Assert.Equal(reason, shown.Detail);
    }

    [Fact]
    public void DeletedInXero_ReadsDeleted_ForEveryShapeX6Answers()
    {
        // A quote Xero shows as DELETED (X6: Failed "Deleted in Xero.").
        Assert.Equal("Deleted in Xero", XeroBadgeText.Describe(XeroDocumentKind.Quote, Status(XeroSyncBadge.Failed, "Deleted in Xero.", "DELETED")).Text);

        // An invoice or PO read back DELETED (X6: Voided "Deleted in Xero.").
        Assert.Equal("Deleted in Xero", XeroBadgeText.Describe(XeroDocumentKind.Invoice, Status(XeroSyncBadge.Voided, "Deleted in Xero.", "DELETED")).Text);
        Assert.Equal("Deleted in Xero", XeroBadgeText.Describe(XeroDocumentKind.PurchaseOrder, Status(XeroSyncBadge.Voided, "Deleted in Xero.", "DELETED")).Text);

        // A purchase order or bill TempestOS made, deleted in Xero (X5 tombstone: Not sent + Send again).
        var tombstone = XeroBadgeText.Describe(
            XeroDocumentKind.ExpenseBill, Status(XeroSyncBadge.NotSent, "The record TempestOS made in Xero was deleted there.", canSendAgain: true));
        Assert.Equal("Deleted in Xero", tombstone.Text);
        Assert.Equal(XeroBadgeTone.Attention, tombstone.Tone);

        // Voided proper stays Voided.
        Assert.Equal("Voided", XeroBadgeText.Describe(XeroDocumentKind.Invoice, Status(XeroSyncBadge.Voided, "Voided in Xero.", "VOIDED")).Text);
    }

    [Fact]
    public void EveryBadgeX6Defines_HasWords()
    {
        foreach (var kind in Enum.GetValues<XeroDocumentKind>())
        {
            foreach (var badge in Enum.GetValues<XeroSyncBadge>())
                Assert.False(string.IsNullOrWhiteSpace(XeroBadgeText.Describe(kind, Status(badge)).Text), $"{kind}/{badge}");
        }
    }
}
