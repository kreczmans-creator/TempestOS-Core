using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Invoices;

/// <summary>
/// `v0.24.0` X4 after the send (design §4.2): the status read back from
/// Xero (DRAFT, AUTHORISED as awaiting payment, PAID, VOIDED, DELETED);
/// revising a sent invoice changes Xero's draft only while it is still a
/// draft, otherwise it is refused with the reason; voiding deletes the Xero
/// draft only. TempestOS never moves an invoice past DRAFT itself.
/// </summary>
public sealed class XeroInvoiceLifecycleTests
{
    [Fact]
    public async Task ReadBack_Draft_StaysSent_WithNoIssuedDate()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("RB1");

        var read = await kit.Service.ReconcileAsync(request.Id);

        Assert.Equal(InvoiceRequestStatus.Sent, read.Request!.Status);
        Assert.Equal("DRAFT", read.Request.ExternalStatus);
        Assert.Null(read.Request.IssuedDate);
        Assert.Null(read.Request.PaidDate);
        kit.AssertSafe();
    }

    [Fact]
    public async Task ReadBack_ApprovedInXero_IsAccepted_AwaitingPayment_IssuedOnItsDate_ThenPaid_WithThePaidDateFromXeroAlone()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("RB2");

        kit.Simulator.ApproveInXero(request.ExternalId!);
        var approved = await kit.Service.ReconcileAsync(request.Id);

        Assert.Equal(InvoiceRequestStatus.Accepted, approved.Request!.Status);
        Assert.Equal("AUTHORISED", approved.Request.ExternalStatus);
        Assert.Equal(new DateOnly(2026, 10, 2), approved.Request.IssuedDate);
        Assert.Null(approved.Request.PaidDate);
        Assert.Equal("AUTHORISED", (await kit.LinkAsync(request.Id))!.LastKnownXeroStatus);

        kit.Simulator.PayInXero(request.ExternalId!, new DateOnly(2026, 10, 20));
        var paid = await kit.Service.ReconcileAsync(request.Id);

        Assert.Equal(InvoiceRequestStatus.Accepted, paid.Request!.Status);
        Assert.Equal("PAID", paid.Request.ExternalStatus);
        Assert.Equal(new DateOnly(2026, 10, 20), paid.Request.PaidDate);
        kit.AssertSafe();
    }

    [Fact]
    public async Task ReadBack_VoidedInXero_IsVoided()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("RB3");
        kit.Simulator.ApproveInXero(request.ExternalId!);
        kit.Simulator.VoidInXero(request.ExternalId!);

        var read = await kit.Service.ReconcileAsync(request.Id);

        Assert.Equal(InvoiceRequestStatus.Voided, read.Request!.Status);
        Assert.Equal("VOIDED", read.Request.ExternalStatus);
        kit.AssertSafe();
    }

    [Fact]
    public async Task ReadBack_DeletedInXero_IsVoided_WithTheNote_AndFreesItsLinesForANewRequest()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("RB4");
        kit.Simulator.DeleteInXero("Invoices", request.ExternalId!);

        var read = await kit.Service.ReconcileAsync(request.Id);

        Assert.Equal(InvoiceRequestStatus.Voided, read.Request!.Status);
        Assert.Equal("DELETED", read.Request.ExternalStatus);
        Assert.Equal("Deleted in Xero.", read.Request.LastError);
        Assert.Equal("DELETED", (await kit.LinkAsync(request.Id))!.LastKnownXeroStatus);
        kit.AssertSafe();
    }

    [Theory]
    [InlineData("DRAFT", InvoiceRequestStatus.Sent)]
    [InlineData("SUBMITTED", InvoiceRequestStatus.Sent)]
    [InlineData("AUTHORISED", InvoiceRequestStatus.Accepted)]
    [InlineData("PAID", InvoiceRequestStatus.Accepted)]
    [InlineData("VOIDED", InvoiceRequestStatus.Voided)]
    [InlineData("DELETED", InvoiceRequestStatus.Voided)]
    [InlineData("SOMETHING-NEW", InvoiceRequestStatus.Sent)]
    public void InterpretStatus_MapsEveryXeroInvoiceStatus(string xeroStatus, InvoiceRequestStatus expected) =>
        Assert.Equal(expected, InvoicingService.InterpretStatus(xeroStatus, InvoiceRequestStatus.Sent));

    [Fact]
    public async Task Revise_WhileXeroHoldsADraft_UpdatesTheXeroDraft_ThenTheRequest()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("RV1");
        var hashBefore = (await kit.LinkAsync(request.Id))!.LastPushedContentHash;
        var line = Assert.Single(request.Lines);

        var revised = await kit.Service.ReviseLinesAsync(
            request.Id, [new InvoiceRequestLineRevision(line.SourceId, "Bridge study — stage 1", 2m, new Money(700m, CurrencyCode.Gbp), VatRate.Standard)]);

        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Equal(InvoiceRequestStatus.Sent, revised.Request!.Status);
        var after = Assert.Single((await kit.ReloadAsync(request.Id)).Lines);
        Assert.Equal("Bridge study — stage 1", after.Description);
        Assert.Equal(new Money(1400m, CurrencyCode.Gbp), after.Amount);
        Assert.Equal(new Money(1400m, CurrencyCode.Gbp), (await kit.ReloadAsync(request.Id)).Total);

        var invoice = kit.Invoice(request.ExternalId!);
        Assert.Equal("DRAFT", invoice.Status);
        var xeroLine = Assert.Single(invoice.Body["LineItems"]!.AsArray());
        Assert.Equal("Bridge study — stage 1", (string?)xeroLine!["Description"]);
        Assert.Equal("OUTPUT2", (string?)xeroLine["TaxType"]);
        Assert.Equal(2m, (decimal?)xeroLine["Quantity"]);
        Assert.Equal("ACME1-BRIDG1-INV-001", invoice.Number);

        Assert.NotEqual(hashBefore, (await kit.LinkAsync(request.Id))!.LastPushedContentHash);
        Assert.Single(kit.SalesInvoices);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Revise_OnceApprovedInXero_IsRefusedWithTheReason_AndNeitherSideChanges()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("RV2");
        kit.Simulator.ApproveInXero(request.ExternalId!);
        var line = Assert.Single(request.Lines);
        var mark = kit.Mark;

        var revised = await kit.Service.ReviseLinesAsync(
            request.Id, [new InvoiceRequestLineRevision(line.SourceId, "Changed", 1m, new Money(1m, CurrencyCode.Gbp), VatRate.Standard)]);

        Assert.False(revised.Succeeded);
        Assert.Equal("Xero holds it as AUTHORISED; change it in Xero.", revised.Reason);
        Assert.Equal(line, Assert.Single((await kit.ReloadAsync(request.Id)).Lines));
        Assert.DoesNotContain(kit.RequestsSince(mark), r => r.Method != HttpMethod.Get);
        Assert.Equal(1200m, (decimal?)Assert.Single(kit.Invoice(request.ExternalId!).Body["LineItems"]!.AsArray())!["UnitAmount"]);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Revise_XeroUnreachable_IsRefused_TheRequestUnchanged()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("RV3");
        var line = Assert.Single(request.Lines);
        kit.Simulator.Inject(new Simulator.XeroFault(Simulator.XeroFaultKind.TransportFailure, PathContains: "Invoices"));

        var revised = await kit.Service.ReviseLinesAsync(
            request.Id, [new InvoiceRequestLineRevision(line.SourceId, "Changed", 1m, new Money(1m, CurrencyCode.Gbp), VatRate.Standard)]);

        Assert.False(revised.Succeeded);
        Assert.Contains("did not take the change", revised.Reason, StringComparison.Ordinal);
        Assert.Equal(line, Assert.Single((await kit.ReloadAsync(request.Id)).Lines));
        kit.AssertSafe();
    }

    [Fact]
    public async Task Revise_ADraftRequest_IsLocalOnly_AndABadRevisionIsRefused()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, _) = await kit.AddProjectAsync("RV4");
        var request = await kit.RaiseAsync(projectId, "RV4");
        var line = Assert.Single(request.Lines);
        var mark = kit.Mark;

        var revised = await kit.Service.ReviseLinesAsync(
            request.Id, [new InvoiceRequestLineRevision(line.SourceId, "Reworded", 1m, new Money(900m, CurrencyCode.Gbp), VatRate.Zero)]);

        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Equal(new Money(900m, CurrencyCode.Gbp), (await kit.ReloadAsync(request.Id)).Total);
        Assert.Empty(kit.RequestsSince(mark));

        var unknownSource = await kit.Service.ReviseLinesAsync(
            request.Id, [new InvoiceRequestLineRevision(Guid.NewGuid(), "x", 1m, new Money(1m, CurrencyCode.Gbp), VatRate.Zero)]);
        Assert.False(unknownSource.Succeeded);
        Assert.Contains("never adds a line", unknownSource.Reason, StringComparison.Ordinal);

        var zeroQuantity = await kit.Service.ReviseLinesAsync(
            request.Id, [new InvoiceRequestLineRevision(line.SourceId, "x", 0m, new Money(1m, CurrencyCode.Gbp), VatRate.Zero)]);
        Assert.False(zeroQuantity.Succeeded);

        var otherCurrency = await kit.Service.ReviseLinesAsync(
            request.Id, [new InvoiceRequestLineRevision(line.SourceId, "x", 1m, new Money(1m, new CurrencyCode("EUR")), VatRate.Zero)]);
        Assert.False(otherCurrency.Succeeded);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Void_WhileXeroHoldsADraft_DeletesTheXeroDraftOnly_AndVoidsTheRequest()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("VD1");

        var voided = await kit.Service.VoidAsync(request.Id);

        Assert.True(voided.Succeeded, voided.Reason);
        Assert.Equal(InvoiceRequestStatus.Voided, voided.Request!.Status);
        Assert.Equal("DELETED", voided.Request.ExternalStatus);
        Assert.Equal("DELETED", kit.Invoice(request.ExternalId!).Status);
        Assert.Equal("DELETED", (await kit.LinkAsync(request.Id))!.LastKnownXeroStatus);

        var delete = Assert.Single(kit.Simulator.Requests, r => r.Method == HttpMethod.Post && r.Path == $"Invoices/{request.ExternalId}");
        var body = Assert.Single(delete.JsonBody!["Invoices"]!.AsArray())!.AsObject();
        Assert.Equal("DELETED", (string?)body["Status"]);
        Assert.Null(body["LineItems"]);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Void_OnceApprovedInXero_IsRefusedWithTheReason_TheRequestStaysSent()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("VD2");
        kit.Simulator.ApproveInXero(request.ExternalId!);

        var voided = await kit.Service.VoidAsync(request.Id);

        Assert.False(voided.Succeeded);
        Assert.Equal("Xero holds it as AUTHORISED; void it in Xero — TempestOS reads it back.", voided.Reason);
        Assert.Equal(InvoiceRequestStatus.Sent, (await kit.ReloadAsync(request.Id)).Status);
        Assert.Equal("AUTHORISED", kit.Invoice(request.ExternalId!).Status);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Void_AlreadyDeletedInXero_VoidsTheRequestWithoutWriting()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("VD3");
        kit.Simulator.DeleteInXero("Invoices", request.ExternalId!);
        var mark = kit.Mark;

        var voided = await kit.Service.VoidAsync(request.Id);

        Assert.True(voided.Succeeded, voided.Reason);
        Assert.Equal(InvoiceRequestStatus.Voided, voided.Request!.Status);
        Assert.DoesNotContain(kit.RequestsSince(mark), r => r.Method != HttpMethod.Get);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Void_ADraftWhoseSendAnswerWasLost_DeletesTheXeroDraftFirst_SoRaisingAgainLeavesOneLiveInvoice()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("LV1");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "LV1");
        var completionId = Assert.Single(request.Lines).SourceId;
        kit.Loss.LoseInvoiceCreates = 1;
        kit.Loss.Loss = AnswerLoss.Dropped;

        var sent = await kit.Service.SendAsync(request.Id);
        Assert.Equal(InvoiceRequestStatus.Draft, sent.Request!.Status); // Xero committed it; the answer was lost
        var lost = Assert.Single(kit.SalesInvoices);
        var queued = Assert.Single(await kit.Outbox.ListForDocumentAsync(XeroInvoiceDrafts.DocumentFor(request.Id)));

        var voided = await kit.Service.VoidAsync(request.Id);

        Assert.True(voided.Succeeded, voided.Reason);
        Assert.Equal(InvoiceRequestStatus.Voided, voided.Request!.Status);
        Assert.Equal("DELETED", kit.Invoice(lost.Id).Status);
        Assert.Equal("DELETED", (await kit.LinkAsync(request.Id))!.LastKnownXeroStatus);

        // The queued send has nothing left to do; the freed work is raised and sent again.
        var pushed = await new XeroInvoicePushHandler(kit.Service, kit.Drafts).PushAsync(InvoiceExportKit.TenantId, queued);
        Assert.Equal(XeroPushOutcome.NothingToDo, pushed.Outcome);

        var again = await kit.Service.RaiseFromCompletionAsync(completionId);
        Assert.True(again.Succeeded, again.Reason);
        Assert.Equal(InvoiceRequestStatus.Sent, (await kit.Service.SendAsync(again.Request!.Id)).Request!.Status);

        var live = Assert.Single(kit.LiveSalesInvoices);
        Assert.Equal("ACME1-BRIDG1-INV-002", live.Number);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Void_ADraftWhoseSendAnswerWasLost_XeroUnreachable_IsRefused_TheRequestStaysDraft()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("LV2");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "LV2");
        kit.Loss.LoseInvoiceCreates = 1;
        await kit.Service.SendAsync(request.Id);
        var lost = Assert.Single(kit.SalesInvoices);
        kit.Simulator.Inject(new Simulator.XeroFault(Simulator.XeroFaultKind.TransportFailure, PathContains: "Invoices"));

        var voided = await kit.Service.VoidAsync(request.Id);

        Assert.False(voided.Succeeded);
        Assert.Contains("try again", voided.Reason, StringComparison.Ordinal);
        Assert.Equal(InvoiceRequestStatus.Draft, (await kit.ReloadAsync(request.Id)).Status);
        Assert.Equal("DRAFT", kit.Invoice(lost.Id).Status);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Void_ADraftWhoseSendAnswerWasLost_AlreadyApprovedInXero_IsRefused_AndTheInvoiceIsTrackedAsSent()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("LV3");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "LV3");
        kit.Loss.LoseInvoiceCreates = 1;
        await kit.Service.SendAsync(request.Id);
        var lost = Assert.Single(kit.SalesInvoices);
        kit.Simulator.ApproveInXero(lost.Id);

        var voided = await kit.Service.VoidAsync(request.Id);

        Assert.False(voided.Succeeded);
        Assert.Contains("void it in Xero", voided.Reason, StringComparison.Ordinal);
        var after = await kit.ReloadAsync(request.Id);
        Assert.Equal(InvoiceRequestStatus.Sent, after.Status);
        Assert.Equal(lost.Id, after.ExternalId);
        Assert.Equal("AUTHORISED", kit.Invoice(lost.Id).Status);
        Assert.Equal(InvoiceRequestStatus.Accepted, (await kit.Service.ReconcileAsync(request.Id)).Request!.Status);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Void_ADraftWhoseSendNeverReachedXero_IsLocal_AfterOneLookUp()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("LV4");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "LV4");
        kit.Simulator.Inject(new Simulator.XeroFault(Simulator.XeroFaultKind.TransportFailure, PathContains: "Invoices"));
        await kit.Service.SendAsync(request.Id);
        Assert.Empty(kit.SalesInvoices);
        var mark = kit.Mark;

        var voided = await kit.Service.VoidAsync(request.Id);

        Assert.True(voided.Succeeded, voided.Reason);
        Assert.Equal(InvoiceRequestStatus.Voided, voided.Request!.Status);
        Assert.Contains(kit.RequestsSince(mark), r => r.Method == HttpMethod.Get && r.Query.ContainsKey("InvoiceNumbers"));
        Assert.DoesNotContain(kit.RequestsSince(mark), r => r.Method != HttpMethod.Get);
        Assert.Empty(kit.SalesInvoices);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Retry_AfterALostAnswer_FindsItsOwnInvoice_EvenThoughTheDeliverableWasRenamedMeanwhile()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("RN1");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "RN1");
        kit.Loss.LoseInvoiceCreates = 1;
        await kit.Service.SendAsync(request.Id);
        var lost = Assert.Single(kit.SalesInvoices);
        Assert.Equal("ACME1-BRIDG1 · Deliverable RN1", (string?)lost.Body["Reference"]);
        var queued = Assert.Single(await kit.Outbox.ListForDocumentAsync(XeroInvoiceDrafts.DocumentFor(request.Id)));

        await kit.RenameDeliverableAsync(request, "Bridge study, renamed");
        var pushed = await new XeroInvoicePushHandler(kit.Service, kit.Drafts).PushAsync(InvoiceExportKit.TenantId, queued);

        Assert.Equal(XeroPushOutcome.Succeeded, pushed.Outcome);
        var after = await kit.ReloadAsync(request.Id);
        Assert.Equal(InvoiceRequestStatus.Sent, after.Status);
        Assert.Equal(lost.Id, after.ExternalId);
        Assert.Equal(XeroInvoiceDrafts.LinkedByReconciled, (await kit.LinkAsync(request.Id))!.LinkedBy);
        Assert.Single(kit.SalesInvoices);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Reconcile_AnUnknownSend_FindsItsOwnInvoice_EvenThoughTheDeliverableWasRenamedMeanwhile()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (projectId, organisationId) = await kit.AddProjectAsync("RN2");
        await kit.LinkClientAsync(organisationId);
        var request = await kit.RaiseAsync(projectId, "RN2");
        kit.Loss.LoseInvoiceCreates = 1;
        kit.Loss.Loss = AnswerLoss.EmptyBody;
        Assert.Equal(InvoiceRequestStatus.Unknown, (await kit.Service.SendAsync(request.Id)).Request!.Status);
        var lost = Assert.Single(kit.SalesInvoices);

        await kit.RenameDeliverableAsync(request, "Bridge study, renamed");
        var reconciled = await kit.Service.ReconcileAsync(request.Id);

        Assert.Equal(InvoiceRequestStatus.Sent, reconciled.Request!.Status);
        Assert.Equal(lost.Id, reconciled.Request.ExternalId);
        Assert.Single(kit.SalesInvoices);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Revise_TheContentUpdateStatesDraft_SoAnApprovalRacingItIsRefusedByXero_NotOverwritten()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("RC1");
        var line = Assert.Single(request.Lines);

        var revised = await kit.Service.ReviseLinesAsync(
            request.Id, [new InvoiceRequestLineRevision(line.SourceId, "Stage 1", 1m, new Money(1000m, CurrencyCode.Gbp), VatRate.Standard)]);
        Assert.True(revised.Succeeded, revised.Reason);
        var update = Assert.Single(kit.Simulator.Requests, r => r.Method == HttpMethod.Post && r.Path == $"Invoices/{request.ExternalId}");
        Assert.Equal("DRAFT", (string?)Assert.Single(update.JsonBody!["Invoices"]!.AsArray())!["Status"]);

        // The Product Owner approves in Xero between TempestOS's status read and its content write.
        kit.Loss.BeforeSend = message =>
        {
            if (message.Method == HttpMethod.Post && message.RequestUri!.AbsolutePath.EndsWith($"/Invoices/{request.ExternalId}", StringComparison.Ordinal))
                kit.Simulator.ApproveInXero(request.ExternalId!);
        };

        var raced = await kit.Service.ReviseLinesAsync(
            request.Id, [new InvoiceRequestLineRevision(line.SourceId, "Stage 2", 1m, new Money(1m, CurrencyCode.Gbp), VatRate.Standard)]);

        Assert.False(raced.Succeeded);
        var invoice = kit.Invoice(request.ExternalId!);
        Assert.Equal("AUTHORISED", invoice.Status);
        Assert.Equal("Stage 1", (string?)Assert.Single(invoice.Body["LineItems"]!.AsArray())!["Description"]);
        Assert.Equal("Stage 1", Assert.Single((await kit.ReloadAsync(request.Id)).Lines).Description);

        // Xero's own refusal of AUTHORISED → DRAFT is what stopped the write
        // (the simulator logs it as a contract refusal); nothing else broke a rule.
        Assert.All(kit.Simulator.Violations, v => Assert.Contains(v.Rule, new[] { Simulator.XeroSimulatorRules.InvoiceTransition, Simulator.XeroSimulatorRules.NotEditable }));
        Assert.Contains(kit.Simulator.Violations, v => v.Rule == Simulator.XeroSimulatorRules.InvoiceTransition);
        Assert.DoesNotContain(kit.Simulator.Requests, r => r.Path.EndsWith("/Email", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(kit.SafetyAudit.Rows);
    }
}
