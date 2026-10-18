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
    private static XeroDocumentSyncStatus Status(
        XeroSyncBadge badge, string? reason = null, string? xeroStatus = null, bool canSendAgain = false, Guid? retry = null, bool cannotTell = false) =>
        new(new XeroSyncStatus(badge, reason, "N-1", xeroStatus, null, retry), XeroSyncService.LabelFor(badge), retry is not null, canSendAgain) { CannotTell = cannotTell };

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
        var shown = XeroBadgeText.Describe(XeroDocumentKind.PurchaseOrder, Status(XeroSyncBadge.Failed, reason, retry: Guid.NewGuid(), cannotTell: true));

        Assert.Equal("Can't tell", shown.Text);
        Assert.Equal(reason, shown.Detail);
    }

    // Backlog U3 open item: X6's status now carries the verdict typed, so the badge reads it and never the reason's words.
    [Fact]
    public void CannotTell_IsReadFromX6sTypedVerdict_NotFromTheReasonsWords()
    {
        var reason = XeroPurchasingOwnership.CannotTell("Purchase order", "order", "PO-2026-001", "key expired", sourceGone: false).Reason;

        Assert.Equal(XeroBadgeText.Failed, XeroBadgeText.Describe(XeroDocumentKind.PurchaseOrder, Status(XeroSyncBadge.Failed, reason, retry: Guid.NewGuid())).Text);
        Assert.Equal(
            XeroBadgeText.CannotTell,
            XeroBadgeText.Describe(XeroDocumentKind.PurchaseOrder, Status(XeroSyncBadge.Failed, "Reworded by a later X5.", retry: Guid.NewGuid(), cannotTell: true)).Text);
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
    public void CannotTell_ForABill_IsItsOwnBadge_FromX5sOwnReason()
    {
        var reason = XeroPurchasingOwnership.CannotTell("Bill", "expense", "SUP-778", null, sourceGone: true).Reason;

        Assert.Equal(XeroBadgeText.CannotTell, XeroBadgeText.Describe(XeroDocumentKind.ExpenseBill, Status(XeroSyncBadge.Failed, reason, retry: Guid.NewGuid(), cannotTell: true)).Text);
    }

    [Theory]
    [InlineData(XeroDocumentKind.PurchaseOrder)]
    [InlineData(XeroDocumentKind.ExpenseBill)]
    [InlineData(XeroDocumentKind.Invoice)]
    public void AFailedReasonThatMerelyMentionsTheWords_IsNeverReadAsCannotTellOrDeleted(XeroDocumentKind kind)
    {
        // Verifier round 1 (defect 6): the badge state comes from typed facts
        // and X5's own producer, never from searching a reason's text.
        var cannotTellWords = XeroBadgeText.Describe(kind, Status(XeroSyncBadge.Failed, "Xero refused: the tax engine cannot tell which rate applies.", retry: Guid.NewGuid()));
        Assert.Equal(XeroBadgeText.Failed, cannotTellWords.Text);

        var deletedWords = XeroBadgeText.Describe(kind, Status(XeroSyncBadge.Failed, "Deleted in Xero, or never there: Xero answered 404 for this contact.", retry: Guid.NewGuid()));
        Assert.Equal(XeroBadgeText.Failed, deletedWords.Text);

        var voidedWithDeletedWords = XeroBadgeText.Describe(kind, Status(XeroSyncBadge.Voided, "Deleted in Xero (as a note).", "VOIDED"));
        Assert.Equal(XeroBadgeText.Voided, voidedWithDeletedWords.Text);
    }

    [Fact]
    public void APurchasingPushRefusedBecauseItsRecordWasDeletedInXero_ReadsDeleted_FromX6sSendAgain()
    {
        // X5's DeletedInXero refusal leaves a tombstone, which is exactly when X6 offers Send again.
        var reason = XeroPurchasingOwnership.DeletedInXero("Bill", "expense", "EXP-1", "DELETED", "Send it again to make a new draft.", sourceGone: false).Reason;

        var shown = XeroBadgeText.Describe(XeroDocumentKind.ExpenseBill, Status(XeroSyncBadge.Failed, reason, canSendAgain: true, retry: Guid.NewGuid()));

        Assert.Equal(XeroBadgeText.Deleted, shown.Text);
        Assert.Equal(XeroBadgeTone.Attention, shown.Tone);
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
