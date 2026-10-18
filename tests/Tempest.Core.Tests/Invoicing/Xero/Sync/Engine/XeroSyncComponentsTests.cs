using System.Security.Cryptography;
using System.Text;
using Tempest.Core.BackgroundServices;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.DependencyInjection;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Quotations;
using Tempest.Core.Tests.Expenses;
using Tempest.Core.Tests.Invoicing.Connectors;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Quotations;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

/// <summary>
/// `v0.24.0` X6's parts on their own: the backoff schedule, the change
/// observer, the attachment file source over a real workspace, the hosted
/// service, the <c>RegisterSync</c> hook, and the badge's quieter states.
/// </summary>
public sealed class XeroSyncComponentsTests
{
    // ------------------------------------------------------------ backoff

    [Theory]
    [InlineData(1, 0.5, 30)]
    [InlineData(2, 0.5, 60)]
    [InlineData(3, 0.5, 120)]
    [InlineData(7, 0.5, 1800)]
    [InlineData(40, 0.5, 1800)]
    [InlineData(1, 0.0, 24)]
    [InlineData(1, 1.0, 36)]
    [InlineData(0, 0.5, 30)]
    public void Backoff_IsThirtySecondsDoubling_CappedAtThirtyMinutes_WithTwentyPercentJitter(int attempts, double jitter, int expectedSeconds) =>
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), XeroBackoff.Delay(attempts, jitter));

    [Fact]
    public void RecoveryBackoff_FitsSeveralAttemptsInsideTheKeyLifetime_AndA429WaitsRetryAfterPlusOneSecond()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), XeroBackoff.RecoveryDelay(1, 0.5));
        Assert.Equal(TimeSpan.FromSeconds(10), XeroBackoff.RecoveryDelay(2, 0.5));
        Assert.Equal(TimeSpan.FromSeconds(45), XeroBackoff.RecoveryDelay(10, 0.5));

        var total = Enumerable.Range(1, 5).Aggregate(TimeSpan.Zero, (sum, n) => sum + XeroBackoff.RecoveryDelay(n, 0.99));
        Assert.True(total < XeroPurchasingOwnership.IdempotencyKeyLifetime, $"Five recovery attempts take {total}.");

        Assert.Equal(TimeSpan.FromSeconds(21), XeroBackoff.RateLimitPause(TimeSpan.FromSeconds(20)));
        Assert.Equal(TimeSpan.FromSeconds(60), XeroBackoff.RateLimitPause(null));
        Assert.Equal(TimeSpan.FromSeconds(30), XeroBackoff.Delay(1, double.NaN));
    }

    [Fact]
    public void Options_ReadTheSyncAndReadBackIntervals_FromConfiguration()
    {
        var configuration = new Tempest.Core.Configuration.ConfigurationBuilder()
            .AddSource(new Tempest.Core.Configuration.MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(XeroSyncOptions.SyncSecondsConfigurationKey, "15"),
                new KeyValuePair<string, string>(XeroSyncOptions.ReadBackMinutesConfigurationKey, "nonsense"),
            ]))
            .Build();

        var options = XeroSyncOptions.FromConfiguration(configuration);
        Assert.Equal(TimeSpan.FromSeconds(15), options.SyncInterval);
        Assert.Equal(TimeSpan.FromMinutes(15), options.ReadBackInterval);
        Assert.Equal(TimeSpan.FromSeconds(60), XeroSyncOptions.FromConfiguration(null).SyncInterval);
    }

    // ------------------------------------------------------------ observer

    [Fact]
    public void TheObserver_QueuesWatchedRecordsOnce_IgnoresOthers_AndAsksForARescanWhenFull()
    {
        var feed = new WorkspaceChangeFeed();
        using var observer = new XeroChangeObserver(feed, capacity: 2);
        var raised = 0;
        observer.ChangesQueued += () => raised++;
        observer.Start([Quotation.CanonicalKind, PurchaseOrder.CanonicalKind]);

        var quote = Guid.NewGuid();
        feed.Publish(new WorkspaceChange(1, [
            new WorkspaceChangeEntry(quote, Quotation.CanonicalKind, WorkspaceChangeType.Updated),
            new WorkspaceChangeEntry(Guid.NewGuid(), "Project", WorkspaceChangeType.Updated),
        ]));
        feed.Publish(new WorkspaceChange(2, [new WorkspaceChangeEntry(quote, Quotation.CanonicalKind, WorkspaceChangeType.AttachmentAdded)]));

        var batch = observer.TakeAll();
        Assert.Equal([new XeroObservedChange(Quotation.CanonicalKind, quote)], batch.Changes);
        Assert.False(batch.RescanNeeded);
        Assert.Equal(2, raised);

        feed.Publish(new WorkspaceChange(3, [
            new WorkspaceChangeEntry(Guid.NewGuid(), PurchaseOrder.CanonicalKind, WorkspaceChangeType.Created),
            new WorkspaceChangeEntry(Guid.NewGuid(), PurchaseOrder.CanonicalKind, WorkspaceChangeType.Created),
            new WorkspaceChangeEntry(Guid.NewGuid(), PurchaseOrder.CanonicalKind, WorkspaceChangeType.Created),
        ]));
        batch = observer.TakeAll();
        Assert.Equal(2, batch.Changes.Count);
        Assert.True(batch.RescanNeeded);
        Assert.False(observer.TakeAll().RescanNeeded);

        observer.Stop();
        feed.Publish(new WorkspaceChange(4, [new WorkspaceChangeEntry(quote, Quotation.CanonicalKind, WorkspaceChangeType.Updated)]));
        Assert.Empty(observer.TakeAll().Changes);
    }

    [Fact]
    public void TheObserver_NeverFailsTheCommit_WhenItsListenerThrows()
    {
        var feed = new WorkspaceChangeFeed();
        using var observer = new XeroChangeObserver(feed);
        observer.ChangesQueued += () => throw new InvalidOperationException("listener broke");
        observer.Start([Quotation.CanonicalKind]);

        var id = Guid.NewGuid();
        feed.Publish(new WorkspaceChange(1, [new WorkspaceChangeEntry(id, Quotation.CanonicalKind, WorkspaceChangeType.Created)]));
        Assert.Single(observer.TakeAll().Changes);
    }

    // ------------------------------------------------------------ file source

    [Fact]
    public async Task TheFileSource_ReadsTheNewestIssuedPdf_NamedAfterTheRecordsNumber()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        try
        {
            QuotationTestHost.SignIn(host);
            var projectId = await QuotationTestHost.CreateProjectAsync(host, "P0012", "Bracket programme");
            var created = await QuotationTestHost.Quotations(host).CreateAsync(projectId, "P0012-Q-001", null);
            Assert.True(created.Succeeded, created.Reason);
            var domain = QuotationTestHost.Domain(host);
            var quotation = (Quotation)(await domain.Repository.FindAsync(created.Quotation!.Id))!;
            var source = new AttachmentXeroDocumentFileSource(domain);
            var reference = XeroDocumentRef.For(XeroDocumentKind.Quote, quotation.Id);

            Assert.Null(await source.FindAsync(reference));

            await quotation.AttachContentAsync("Quote R1.pdf", "application/pdf", Encoding.UTF8.GetBytes("R1 sheet"));
            var r2 = Encoding.UTF8.GetBytes("R2 sheet");
            await quotation.AttachContentAsync("Quote R2.pdf", "application/pdf", r2);
            await quotation.AttachContentAsync("client drawing.png", "image/png", Encoding.UTF8.GetBytes("not a sheet"));

            var file = await source.FindAsync(reference);
            Assert.NotNull(file);
            Assert.Equal("P0012-Q-001.pdf", file.FileName);
            Assert.Equal("application/pdf", file.ContentType);
            Assert.Equal(r2, file.Content.ToArray());
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(r2)), file.Sha256);

            Assert.Null(await source.FindAsync(new XeroDocumentRef(XeroDocumentKind.Contact, "ACME1")));
            Assert.Null(await source.FindAsync(XeroDocumentRef.For(XeroDocumentKind.Quote, Guid.NewGuid())));
            Assert.Null(await source.FindAsync(new XeroDocumentRef(XeroDocumentKind.Quote, "not-a-guid")));
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task TheFileSource_ReadsAnExpensesReceipt_UnderItsOwnName()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ExpenseTestHost.StartAsync(temp.Path);
        try
        {
            ExpenseTestHost.SignIn(host);
            var projectId = await ExpenseTestHost.CreateProjectAsync(host, "P0012", "Bracket programme");
            var recorded = await ExpenseTestHost.Expenses(host).RecordAsync(
                projectId, new DateOnly(2026, 9, 28), "Train", ExpenseCategory.Travel,
                new Money(100m, CurrencyCode.Gbp), new Money(20m, CurrencyCode.Gbp), billable: false);
            Assert.True(recorded.Succeeded, recorded.Reason);
            var domain = ExpenseTestHost.Domain(host);
            var expense = (ProjectExpense)(await domain.Repository.FindAsync(recorded.Expense!.Id))!;
            var source = new AttachmentXeroDocumentFileSource(domain);

            var receipt = Encoding.UTF8.GetBytes("jpeg bytes");
            await expense.AttachContentAsync("ticket.JPG", "application/octet-stream", receipt);
            await expense.AttachContentAsync("notes.txt", "text/plain", Encoding.UTF8.GetBytes("not a receipt"));

            var file = await source.FindAsync(XeroExpenseBillPlanner.Ref(expense.Id));
            Assert.NotNull(file);
            Assert.Equal("ticket.JPG", file.FileName);
            Assert.Equal("image/jpeg", file.ContentType);
            Assert.Equal(receipt, file.Content.ToArray());
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------ hosted service

    [Fact]
    public async Task TheHostedService_WithNoXeroEngine_StartsAndStopsDoingNothing()
    {
        var service = new XeroSyncHostedService();
        Assert.False(service.IsEnabled);
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        Assert.True(service.Loop.IsCompleted);
        Assert.Equal(0, service.CompletedCycles);
    }

    [Fact]
    public async Task TheHostedService_RunsTheEngineByItself_WakesOnASavedChange_AndStopsCleanly()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var cycles = 0;
        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        kit.Engine.CycleCompleted += _ =>
        {
            Interlocked.Increment(ref cycles);
            if (kit.LiveQuotes.Count == 1 && kit.LiveQuotes[0].Attachments.Count == 1)
                settled.TrySetResult();
        };

        var service = new XeroSyncHostedService(kit.Engine);
        Assert.True(service.IsEnabled);
        await service.StartAsync(CancellationToken.None);
        try
        {
            kit.ExportQuote();
            await settled.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        Assert.True(service.Loop.IsCompleted);
        Assert.True(cycles >= 1);
        Assert.True(kit.Engine.IsStarted);
        Assert.Equal("DRAFT", Assert.Single(kit.LiveQuotes).Status);
        kit.AssertNoViolations();
    }

    [Fact]
    public void TheHostedService_IsFoundByHostedServiceDiscovery_LikeEveryOtherBackgroundService()
    {
        var discovered = new HostedServiceDiscoveryService([typeof(XeroSyncHostedService).Assembly]).DiscoverHostedServiceTypes();
        Assert.Contains(typeof(XeroSyncHostedService), discovered);
        Assert.Contains(typeof(InvoiceReconciliationService), discovered);
        Assert.Single(typeof(XeroSyncHostedService).GetConstructors());
    }

    [Fact]
    public void AContainerWithoutXero_BuildsTheHostedServiceWithNoEngine()
    {
        var services = new ServiceCollection();
        services.AddDiscoveredHostedServices([typeof(XeroSyncHostedService)]);
        var provider = new TempestServiceProvider(services);

        var service = (XeroSyncHostedService)provider.GetService(typeof(XeroSyncHostedService));
        Assert.False(service.IsEnabled);
    }

    // ------------------------------------------------------------ registration

    [Fact]
    public void TheRegisterSyncHook_RegistersEachServiceOnce()
    {
        var services = new ServiceCollection();
        XeroServiceRegistration.AddXeroSync(services);

        Assert.Throws<DuplicateServiceRegistrationException>(() => XeroServiceRegistration.AddXeroSync(services));
        Assert.DoesNotContain(services.Descriptors, d => d.ServiceType == typeof(XeroSyncHostedService));
        foreach (var type in new[] { typeof(XeroSyncService), typeof(XeroSyncParts), typeof(XeroReadBack), typeof(XeroChangeObserver), typeof(AttachmentXeroDocumentFileSource), typeof(XeroConnectorConnectionState), typeof(XeroSyncServiceForwarder) })
            Assert.Single(type.GetConstructors());
    }

    [Fact]
    public async Task AHostWithTheXeroConnector_ResolvesTheEngine_WithEveryPlannerAndHandler()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ConnectorHostFixture.StartAsync(
            temp.Path,
            new(InvoicingService.ConnectorConfigurationKey, "Xero"),
            new("Invoicing:Xero:ClientId", "test-client-id"));

        try
        {
            var engine = Assert.IsType<XeroSyncService>(host.Services!.GetService(typeof(XeroSyncService)));
            Assert.IsType<XeroSyncServiceForwarder>(host.Services.GetService(typeof(IXeroSyncService)));
            Assert.IsType<AttachmentXeroDocumentFileSource>(host.Services.GetService(typeof(IXeroDocumentFileSource)));
            Assert.IsType<XeroConnectorConnectionState>(host.Services.GetService(typeof(IXeroConnectionState)));

            var parts = (XeroSyncParts)host.Services.GetService(typeof(XeroSyncParts));
            Assert.Equal(
                [XeroDocumentKind.Quote, XeroDocumentKind.Invoice, XeroDocumentKind.PurchaseOrder, XeroDocumentKind.ExpenseBill],
                parts.Planners.Select(p => p.Planner.Kind));
            Assert.IsType<XeroQuotePushHandler>(parts.HandlerFor(XeroOperation.SetQuoteStatus, XeroDocumentKind.Quote));
            Assert.IsType<XeroQuoteAttachmentHandler>(parts.HandlerFor(XeroOperation.UploadAttachment, XeroDocumentKind.Quote));
            Assert.IsType<XeroInvoicePushHandler>(parts.HandlerFor(XeroOperation.DeleteInvoiceDraft, XeroDocumentKind.Invoice));
            Assert.IsType<XeroInvoiceAttachmentPushHandler>(parts.HandlerFor(XeroOperation.UploadAttachment, XeroDocumentKind.Invoice));
            Assert.IsType<XeroPurchaseOrderPushHandler>(parts.HandlerFor(XeroOperation.DeletePurchaseOrder, XeroDocumentKind.PurchaseOrder));
            Assert.IsType<XeroPurchaseOrderAttachmentHandler>(parts.HandlerFor(XeroOperation.UploadAttachment, XeroDocumentKind.PurchaseOrder));
            Assert.IsType<XeroExpenseBillPushHandler>(parts.HandlerFor(XeroOperation.PushExpenseBill, XeroDocumentKind.ExpenseBill));
            Assert.IsType<XeroExpenseBillAttachmentHandler>(parts.HandlerFor(XeroOperation.UploadAttachment, XeroDocumentKind.ExpenseBill));
            Assert.Null(parts.HandlerFor(XeroOperation.PushQuote, XeroDocumentKind.Invoice));

            // Not connected to any organisation: start-up and a cycle are offline and safe.
            await engine.StartAsync();
            var report = await engine.RunCycleAsync();
            Assert.True(report.Drain.PausedForAuthorisation);
            Assert.Equal(0, report.Drain.Attempted);

            var service = new XeroSyncHostedService(engine);
            Assert.True(service.IsEnabled);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------ badge

    [Fact]
    public async Task TheBadge_IsNotSent_ForARecordNeverQueued_AndExplainsAnExpenseBilledFromItsPurchaseOrder()
    {
        using var kit = await EngineTestKit.CreateAsync();
        var draft = Guid.NewGuid();
        kit.Quotes[draft] = EngineTestKit.Quote(draft, QuotationStatus.Draft);

        var status = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.QuoteRef(draft));
        Assert.Equal(XeroSyncBadge.NotSent, status.Status.Badge);
        Assert.Equal("Not sent", status.Label);
        Assert.False(status.CanRetry);
        Assert.False(status.CanSendAgain);

        var orderId = Guid.NewGuid();
        kit.Orders[orderId] = EngineTestKit.Order(orderId, reference: "PO-2026-009");
        var expenseId = Guid.NewGuid();
        kit.Expenses[expenseId] = EngineTestKit.Expense(expenseId) with { SourcePurchaseOrderId = orderId };
        var expense = await kit.Engine.GetDocumentStatusAsync(EngineTestKit.ExpenseRef(expenseId));
        Assert.Equal(XeroSyncBadge.NotSent, expense.Status.Badge);
        Assert.Contains("PO-2026-009", expense.Status.Reason, StringComparison.Ordinal);

        Assert.Equal("Queued", XeroSyncService.LabelFor(XeroSyncBadge.Queued));
        Assert.Equal("Awaiting payment", XeroSyncService.LabelFor(XeroSyncBadge.AwaitingPayment));
        Assert.Equal("Voided", XeroSyncService.LabelFor(XeroSyncBadge.Voided));
        Assert.Empty(kit.Requests);
    }
}
