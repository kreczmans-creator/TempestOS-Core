using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Invoices;

/// <summary>
/// `v0.24.0` X4 (D3, D4; design §3, §4.2, §6.4): an invoice export creates
/// the Xero <c>ACCREC</c> invoice as <c>DRAFT</c> with TempestOS's own number,
/// the client by <c>ContactID</c>, lines through the X1 tax/account
/// resolution, the project and deliverable in <c>Reference</c> and the PDF
/// attached (<c>IncludeOnline</c> off, Q5) — end to end over the simulator,
/// which records no violation in any test.
/// </summary>
public sealed class XeroInvoiceExportTests
{
    [Fact]
    public async Task Send_CreatesOneDraft_WithTheSameNumber_TheLinkedContact_ResolvedCodes_TheReference_AndThePdfKeptInternal()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("ONE");
        var contactId = await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "ONE");
        kit.Files.Save(request.Id, "ACME1-BRIDG1-INV-001.pdf", "%PDF-1.7 invoice one");

        var sent = await kit.Service.SendAsync(request.Id);

        Assert.True(sent.Succeeded, sent.Reason);
        var after = sent.Request!;
        Assert.Equal(InvoiceRequestStatus.Sent, after.Status);
        Assert.Equal("Xero", after.Connector);
        Assert.Equal("ACME1-BRIDG1-INV-001", after.ExternalInvoiceNumber);
        Assert.Equal("DRAFT", after.ExternalStatus);
        Assert.Equal(new DateOnly(2026, 11, 1), after.DueOn);

        var invoice = Assert.Single(kit.SalesInvoices);
        Assert.Equal(after.ExternalId, invoice.Id);
        Assert.Equal("DRAFT", invoice.Status);
        Assert.Equal("ACME1-BRIDG1-INV-001", invoice.Number);
        Assert.Equal(contactId, (string?)invoice.Body["Contact"]!["ContactID"]);
        Assert.Equal("ACME1-BRIDG1 · Deliverable ONE", (string?)invoice.Body["Reference"]);
        Assert.Equal("Exclusive", (string?)invoice.Body["LineAmountTypes"]);
        Assert.False((bool?)invoice.Body["SentToContact"] ?? false);

        var line = Assert.Single(invoice.Body["LineItems"]!.AsArray());
        Assert.Equal("200", (string?)line!["AccountCode"]);
        Assert.Equal("NONE", (string?)line["TaxType"]); // the completion's fixed-price line is out of scope for VAT
        Assert.Equal(1200m, (decimal?)line["UnitAmount"]);

        var attachment = Assert.Single(invoice.Attachments);
        Assert.Equal("ACME1-BRIDG1-INV-001.pdf", attachment.FileName);
        Assert.False(attachment.IncludeOnline);

        var link = await kit.LinkAsync(request.Id);
        Assert.NotNull(link);
        Assert.Equal(invoice.Id, link!.XeroId);
        Assert.Equal(XeroInvoiceDrafts.LinkedByCreated, link.LinkedBy);
        Assert.Equal("DRAFT", link.LastKnownXeroStatus);
        Assert.Equal("ACME1-BRIDG1-INV-001.pdf", link.AttachmentFileName);
        Assert.NotNull(link.LastPushedContentHash);
        Assert.Contains(kit.Audit.Rows, r => r.Action == XeroInvoiceDrafts.AuditLinkCreated);

        kit.AssertSafe();
    }

    [Fact]
    public async Task Send_TheClientNotLinkedToAXeroContact_IsRefusedBeforeSending_AndNothingReachesXero()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, _) = await kit.AddProjectAsync("UNL");
        var request = await kit.RaiseAsync(projectId, "UNL");
        var mark = kit.Mark;

        var sent = await kit.Service.SendAsync(request.Id);

        Assert.False(sent.Succeeded);
        Assert.Equal(InvoiceRequestRefusal.TransitionNotPermitted, sent.Refusal);
        Assert.Contains("not linked to a Xero contact", sent.Reason, StringComparison.Ordinal);
        Assert.Equal(InvoiceRequestStatus.Draft, (await kit.ReloadAsync(request.Id)).Status);
        Assert.Empty(kit.RequestsSince(mark));
        Assert.Empty(kit.SalesInvoices);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Send_TheCreateAnswerIsLost_RequestBackToDraftAndQueued_TheQueuedRetryFindsItByNumber_OneInvoice()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("LOST");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "LOST");
        kit.Loss.LoseInvoiceCreates = 1;

        var sent = await kit.Service.SendAsync(request.Id);

        // Xero committed it, the answer was lost: Draft again, queued (§4.2).
        Assert.True(sent.Succeeded, sent.Reason);
        Assert.Equal(InvoiceRequestStatus.Draft, sent.Request!.Status);
        Assert.Single(kit.SalesInvoices);
        var queued = Assert.Single(await kit.Outbox.ListForDocumentAsync(XeroInvoiceDrafts.DocumentFor(request.Id)));
        Assert.Equal(XeroOperation.PushInvoiceDraft, queued.Operation);
        Assert.Null(await kit.LinkAsync(request.Id));

        // The drain sends the entry: the number look-up finds TempestOS's own
        // invoice, links it, and never creates a second.
        var handler = new XeroInvoicePushHandler(kit.Service, kit.Drafts);
        var pushed = await handler.PushAsync(InvoiceExportKit.TenantId, queued);

        Assert.Equal(XeroPushOutcome.Succeeded, pushed.Outcome);
        var invoice = Assert.Single(kit.SalesInvoices);
        var after = await kit.ReloadAsync(request.Id);
        Assert.Equal(InvoiceRequestStatus.Sent, after.Status);
        Assert.Equal(invoice.Id, after.ExternalId);
        Assert.Equal(XeroInvoiceDrafts.LinkedByReconciled, (await kit.LinkAsync(request.Id))!.LinkedBy);
        Assert.Single(kit.Simulator.Requests, r => r.Method == HttpMethod.Put && r.Path == "Invoices");
        kit.AssertSafe();
    }

    [Fact]
    public async Task Send_XeroUnreachable_RequestBackToDraft_SendQueued_ThenSentOnTheNextDrain()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("OFF");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "OFF");
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.TransportFailure, PathContains: "Invoices"));

        var sent = await kit.Service.SendAsync(request.Id);

        Assert.Equal(InvoiceRequestStatus.Draft, sent.Request!.Status);
        Assert.NotNull(sent.Request.LastError);
        Assert.Empty(kit.SalesInvoices);
        var queued = Assert.Single(await kit.Outbox.ListForDocumentAsync(XeroInvoiceDrafts.DocumentFor(request.Id)));
        Assert.Equal(XeroOperation.PushInvoiceDraft, queued.Operation);

        var planner = new XeroInvoicePlanner(kit.Service);
        var planned = Assert.Single(await planner.PlanAsync(request.Id, link: null));
        Assert.Equal(XeroOperation.PushInvoiceDraft, planned.Operation);

        var pushed = await new XeroInvoicePushHandler(kit.Service, kit.Drafts).PushAsync(InvoiceExportKit.TenantId, queued);

        Assert.Equal(XeroPushOutcome.Succeeded, pushed.Outcome);
        Assert.Equal(InvoiceRequestStatus.Sent, (await kit.ReloadAsync(request.Id)).Status);
        Assert.Single(kit.SalesInvoices);
        Assert.Single(await kit.Outbox.ListForDocumentAsync(XeroInvoiceDrafts.DocumentFor(request.Id))); // the drain's own entry; no second one queued
        kit.AssertSafe();
    }

    [Fact]
    public async Task Send_AnEmptyAnswer_IsUnknown_ReconcileFindsTheInvoiceByItsNumber_OneInvoice()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("UNK");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "UNK");
        kit.Loss.LoseInvoiceCreates = 1;
        kit.Loss.Loss = AnswerLoss.EmptyBody;

        var sent = await kit.Service.SendAsync(request.Id);
        Assert.Equal(InvoiceRequestStatus.Unknown, sent.Request!.Status);

        var mark = kit.Mark;
        var reconciled = await kit.Service.ReconcileAsync(request.Id);

        Assert.True(reconciled.Succeeded, reconciled.Reason);
        Assert.Equal(InvoiceRequestStatus.Sent, reconciled.Request!.Status);
        var invoice = Assert.Single(kit.SalesInvoices);
        Assert.Equal(invoice.Id, reconciled.Request.ExternalId);
        Assert.Contains(kit.RequestsSince(mark), r => r.Method == HttpMethod.Get && r.Query.TryGetValue("InvoiceNumbers", out var n) && n == "ACME1-BRIDG1-INV-001");
        Assert.DoesNotContain(kit.RequestsSince(mark), r => r.Method == HttpMethod.Put);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Send_TheNumberIsAlreadyUsedInXeroByAnotherInvoice_IsRejected_NeverDuplicated()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("DUP");
        var contactId = await kit.LinkClientAsync(organisationId);
        await kit.EnterInvoiceInXeroAsync(contactId, "ACME1-BRIDG1-INV-001", "Typed in by hand");
        var request = await kit.RaiseAsync(projectId, "DUP");

        var sent = await kit.Service.SendAsync(request.Id);

        Assert.Equal(InvoiceRequestStatus.Rejected, sent.Request!.Status);
        Assert.Contains("ACME1-BRIDG1-INV-001 is already used in Xero", sent.Request.LastError, StringComparison.Ordinal);
        Assert.Single(kit.SalesInvoices);
        Assert.Null(await kit.LinkAsync(request.Id));
        kit.AssertSafe();
    }

    [Fact]
    public async Task Send_ARequestRaisedOutsideAProjectCentricProject_CarriesATosNumber()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("LEG", projectIdentifier: "LEGACY-PROJECT");

        var invoice = Assert.Single(kit.SalesInvoices);
        Assert.Equal($"TOS-{request.Id:N}".ToUpperInvariant(), invoice.Number);
        Assert.Equal(invoice.Number, request.ExternalInvoiceNumber);
        Assert.Equal("LEGACY-PROJECT · Deliverable LEG", (string?)invoice.Body["Reference"]);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Send_WithIncludeOnlineSwitchedOn_ThePdfIsShownOnTheOnlineInvoice()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        XeroInvoiceDrafts.EnsureDefinitions(kit.Settings);
        await kit.Settings.SetValueAsync(XeroInvoiceDrafts.IncludeOnlineSettingKey, "true");
        var (projectId, organisationId) = await kit.AddProjectAsync("ONL");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "ONL");
        kit.Files.Save(request.Id, "ACME1-BRIDG1-INV-001.pdf", "%PDF online");

        await kit.Service.SendAsync(request.Id);

        Assert.True(Assert.Single(Assert.Single(kit.SalesInvoices).Attachments).IncludeOnline);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Send_AttachmentUploadFails_TheInvoiceIsStillSent_AndTheUploadIsQueued_ThenUploadedOnce()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("ATT");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "ATT");
        var file = kit.Files.Save(request.Id, "ACME1-BRIDG1-INV-001.pdf", "%PDF attach");
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.ServiceUnavailable, PathContains: "Attachments"));

        var sent = await kit.Service.SendAsync(request.Id);

        Assert.Equal(InvoiceRequestStatus.Sent, sent.Request!.Status);
        Assert.Empty(Assert.Single(kit.SalesInvoices).Attachments);
        var queued = Assert.Single(await kit.Outbox.ListForDocumentAsync(XeroInvoiceDrafts.DocumentFor(request.Id)));
        Assert.Equal(XeroOperation.UploadAttachment, queued.Operation);
        Assert.Equal(file.FileName, queued.Argument);

        var handler = new XeroInvoiceAttachmentPushHandler(kit.Drafts);
        Assert.Equal(XeroPushOutcome.Succeeded, (await handler.PushAsync(InvoiceExportKit.TenantId, queued)).Outcome);
        Assert.Equal(XeroPushOutcome.NothingToDo, (await handler.PushAsync(InvoiceExportKit.TenantId, queued)).Outcome);

        Assert.Single(Assert.Single(kit.SalesInvoices).Attachments);
        Assert.Equal(file.Sha256, (await kit.LinkAsync(request.Id))!.AttachmentContentHash);
        kit.AssertSafe();
    }

    [Fact]
    public async Task SendingAgain_ARequestAlreadyLinked_NeverCreatesASecondInvoice()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("TWICE");
        var document = await kit.Service.ToDraftDocumentAsync(request, kit.Clock.GetUtcNow(), CancellationToken.None);
        var mark = kit.Mark;

        var again = await kit.NewDrafts().CreateDraftAsync(document);

        Assert.Equal(ConnectorOutcome.Ok, again.Outcome);
        Assert.Equal(request.ExternalId, again.Value!.ExternalId);
        Assert.Empty(kit.RequestsSince(mark));
        Assert.Single(kit.SalesInvoices);
        kit.AssertSafe();
    }

    [Fact]
    public async Task APreV024Request_SentWithItsIdAsReference_StillReconcilesByReference()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("OLD");
        var contactId = await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "OLD");

        // v0.19–v0.23: Xero numbered it itself and carried the request id as its reference; the answer was lost.
        var xeroId = await kit.EnterInvoiceInXeroAsync(contactId, "INV-0042", request.Id.ToString());
        await request.MoveToSendingAsync("Xero");
        await request.MarkUnknownAsync("The answer was lost.");

        var reconciled = await kit.Service.ReconcileAsync(request.Id);

        Assert.True(reconciled.Succeeded, reconciled.Reason);
        Assert.Equal(InvoiceRequestStatus.Sent, reconciled.Request!.Status);
        Assert.Equal(xeroId, reconciled.Request.ExternalId);
        Assert.Equal("INV-0042", reconciled.Request.ExternalInvoiceNumber);
        Assert.Single(kit.SalesInvoices);
        kit.AssertSafe();
    }

    [Fact]
    public async Task WithoutTheDraftSeam_TheXeroConnectorRefusesTheSend_NeverAContactByName()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("NOSEAM");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "NOSEAM");
        var mark = kit.Mark;

        var sent = await kit.NewService(drafts: null).SendAsync(request.Id);

        Assert.Equal(InvoiceRequestStatus.Rejected, sent.Request!.Status);
        Assert.Contains("ContactID", sent.Request.LastError, StringComparison.Ordinal);
        Assert.Empty(kit.RequestsSince(mark));
        kit.AssertSafe();
    }
}
