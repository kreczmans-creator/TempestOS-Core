using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.DependencyInjection;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Persistence;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Secrets;
using Tempest.Core.Settings;
using Tempest.Core.Tests.Expenses;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// `v0.24.0` X5 over a real workspace: a purchase order issued, received and
/// recorded as expenses through <see cref="IPurchaseOrderService"/>, and an
/// expense recorded, given its supplier and deleted through
/// <see cref="IExpenseService"/>, are read by the domain sources and
/// followed in Xero (simulator); <see cref="ProjectExpense"/>'s new optional
/// fields survive a restart and an older expense reads them as empty; and
/// the <c>RegisterPurchasing</c> hook's services resolve from a container.
/// </summary>
public sealed class XeroPurchasingDomainJourneyTests
{
    [Fact]
    public async Task AnIssuedOrder_GoesToXero_AndItsReceivedLinesRecordedAsExpenses_AreNotBilledAgain()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ExpenseTestHost.StartAsync(temp.Path);
        try
        {
            ExpenseTestHost.SignIn(host);
            var domain = ExpenseTestHost.Domain(host);
            var organisations = ExpenseTestHost.Organisations(host);
            using var kit = await PurchasingSyncTestKit.CreateAsync(
                new DomainXeroPurchaseOrderSource(domain, organisations), new DomainXeroExpenseSource(domain, organisations), organisations: organisations);

            var projectId = await ExpenseTestHost.CreateProjectAsync(host, "P0012", "Bracket programme");
            var orders = (IPurchaseOrderService)host.Services!.GetService(typeof(IPurchaseOrderService));
            var created = await orders.CreateAsync(projectId, supplierOrganisationId: PurchasingSyncTestKit.SupplierReference, expectedDelivery: new DateOnly(2026, 11, 2));
            Assert.True(created.Succeeded, created.Reason);
            var orderId = created.Order!.Id;
            Assert.True((await orders.AddLineAsync(orderId, "Steel plate", 10m, new Money(50m, CurrencyCode.Gbp), VatRate.Standard)).Succeeded);
            Assert.True((await orders.AddLineAsync(orderId, "Freight", 1m, new Money(75m, CurrencyCode.Gbp), VatRate.Zero)).Succeeded);

            // Draft: nothing.
            Assert.Empty(await kit.PlanOrderAsync(orderId));

            // Issued → a DRAFT Xero purchase order with the same number, supplier and lines.
            Assert.True((await orders.IssueAsync(orderId)).Succeeded);
            await kit.PlanOrderAsync(orderId);
            await kit.DrainAsync();
            var order = Assert.Single(kit.LiveOrders);
            Assert.Equal("DRAFT", order.Status);
            Assert.Equal(created.Order.Reference, order.Number);
            Assert.Equal("P0012", order.Body["Reference"]!.GetValue<string>());
            Assert.Equal(kit.SupplierContactId, order.Body["Contact"]!["ContactID"]!.GetValue<string>());
            Assert.Equal(2, order.Body["LineItems"]!.AsArray().Count);

            // Received, lines recorded as expenses: each expense remembers its order (Q6) and is not billed.
            Assert.True((await orders.ReceiveAsync(orderId)).Succeeded);
            Assert.Empty(await kit.PlanOrderAsync(orderId));
            Assert.True((await orders.RecordLinesAsExpensesAsync(orderId)).Succeeded);

            var expenses = await ExpenseTestHost.Expenses(host).ListForProjectAsync(projectId);
            Assert.Equal(2, expenses.Count);
            foreach (var expense in expenses)
            {
                Assert.Equal(orderId, expense.SourcePurchaseOrderId);
                Assert.Empty(await kit.PlanExpenseAsync(expense.Id));
                Assert.Contains(created.Order.Reference, await kit.ExpensePlanner.DescribeNotPushedAsync(expense.Id), StringComparison.Ordinal);
            }

            Assert.Equal(0, await kit.ExpensePlanner.ScanAsync());
            Assert.Empty(kit.Bills);
            kit.AssertNoViolations();
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task AnExpenseRecordedFromAnOrdersLine_CarriesTheOrderInItsFirstRevision_SoACrashStraightAfterNeverLeavesItBillable()
    {
        // Q6: were the order written in a later revision, a crash (or a sync
        // reader) between the two would see a PO-sourced expense with no
        // source, bill it, and double the order's Copy to bill.
        using var temp = new TempDirectory();
        var (host, manager) = await ExpenseTestHost.StartAsync(temp.Path);
        try
        {
            ExpenseTestHost.SignIn(host);
            var domain = ExpenseTestHost.Domain(host);
            var organisations = ExpenseTestHost.Organisations(host);
            using var kit = await PurchasingSyncTestKit.CreateAsync(
                new DomainXeroPurchaseOrderSource(domain, organisations), new DomainXeroExpenseSource(domain, organisations), organisations: organisations);

            var projectId = await ExpenseTestHost.CreateProjectAsync(host, "P0012", "Bracket programme");
            var orders = (IPurchaseOrderService)host.Services!.GetService(typeof(IPurchaseOrderService));
            var created = await orders.CreateAsync(projectId, supplierOrganisationId: PurchasingSyncTestKit.SupplierReference);
            var orderId = created.Order!.Id;
            Assert.True((await orders.AddLineAsync(orderId, "Steel plate", 10m, new Money(50m, CurrencyCode.Gbp), VatRate.Standard)).Succeeded);
            Assert.True((await orders.IssueAsync(orderId)).Succeeded);
            Assert.True((await orders.ReceiveAsync(orderId)).Succeeded);

            var crashing = new PurchaseOrderService(domain, ExpenseTestHost.RateCards(host), new CrashAfterRecordExpenseService(ExpenseTestHost.Expenses(host)));
            await Assert.ThrowsAsync<IOException>(() => crashing.RecordLinesAsExpensesAsync(orderId));

            var expense = Assert.Single(await ExpenseTestHost.Expenses(host).ListForProjectAsync(projectId));
            Assert.Equal(orderId, expense.SourcePurchaseOrderId);
            Assert.Empty(await kit.PlanExpenseAsync(expense.Id));
            Assert.Equal(0, await kit.ExpensePlanner.ScanAsync());
            Assert.Empty(kit.Bills);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // Backlog U3 open item: the supplier and its invoice number were saved in a follow-up commit, so the expense's
    // bill could be planned against "General expenses" as EXP-{id} in between.
    [Fact]
    public async Task AnExpenseRecordedWithItsSupplier_CarriesItInItsFirstRevision_SoItsBillIsNeverSentAsGeneralExpenses()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ExpenseTestHost.StartAsync(temp.Path);
        try
        {
            ExpenseTestHost.SignIn(host);
            var domain = ExpenseTestHost.Domain(host);
            var organisations = ExpenseTestHost.Organisations(host);
            using var kit = await PurchasingSyncTestKit.CreateAsync(
                new DomainXeroPurchaseOrderSource(domain, organisations), new DomainXeroExpenseSource(domain, organisations), organisations: organisations);
            var projectId = await ExpenseTestHost.CreateProjectAsync(host, "P0012", "Bracket programme");

            // The process dies straight after the record commits: nothing after it runs.
            var handler = new Tempest.Workspace.Expenses.RecordExpenseCommandHandler(new CrashAfterRecordExpenseService(ExpenseTestHost.Expenses(host)));
            var command = new Tempest.Workspace.Expenses.RecordExpenseCommand(
                projectId, new DateOnly(2026, 9, 28), "Steel offcuts", ExpenseCategory.Materials,
                new Money(400m, CurrencyCode.Gbp), new Money(80m, CurrencyCode.Gbp), billable: true,
                PurchasingSyncTestKit.SupplierReference, " NS-4471 ");
            await Assert.ThrowsAsync<IOException>(() => handler.HandleAsync(command, CancellationToken.None));

            var expense = Assert.Single(await ExpenseTestHost.Expenses(host).ListForProjectAsync(projectId));
            Assert.Equal(PurchasingSyncTestKit.SupplierReference, expense.SupplierOrganisationId);
            Assert.Equal("NS-4471", expense.SupplierInvoiceNumber);

            await kit.PlanExpenseAsync(expense.Id);
            await kit.DrainAsync();
            var bill = Assert.Single(kit.LiveBills);
            Assert.Equal("NS-4471", bill.Number);
            Assert.Equal(kit.SupplierContactId, bill.Body["Contact"]!["ContactID"]!.GetValue<string>());
            kit.AssertNoViolations();
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Records through the real service, then fails as a process would that died straight after the expense was committed.</summary>
    private sealed class CrashAfterRecordExpenseService(IExpenseService inner) : IExpenseService
    {
        public async Task<ExpenseResult> RecordAsync(
            Guid projectId, DateOnly date, string description, ExpenseCategory category, Money netAmount, Money vatAmount, bool billable,
            CancellationToken cancellationToken = default)
        {
            await inner.RecordAsync(projectId, date, description, category, netAmount, vatAmount, billable, cancellationToken);
            throw new IOException("Simulated crash after the expense was committed.");
        }

        public async Task<ExpenseResult> RecordAsync(
            Guid projectId, DateOnly date, string description, ExpenseCategory category, Money netAmount, Money vatAmount, bool billable,
            Guid? sourcePurchaseOrderId, CancellationToken cancellationToken = default)
        {
            await inner.RecordAsync(projectId, date, description, category, netAmount, vatAmount, billable, sourcePurchaseOrderId, cancellationToken);
            throw new IOException("Simulated crash after the expense was committed.");
        }

        public async Task<ExpenseResult> RecordAsync(
            Guid projectId, DateOnly date, string description, ExpenseCategory category, Money netAmount, Money vatAmount, bool billable,
            Guid? sourcePurchaseOrderId, string? supplierOrganisationId, string? supplierInvoiceNumber, CancellationToken cancellationToken = default)
        {
            await inner.RecordAsync(projectId, date, description, category, netAmount, vatAmount, billable, sourcePurchaseOrderId, supplierOrganisationId, supplierInvoiceNumber, cancellationToken);
            throw new IOException("Simulated crash after the expense was committed.");
        }

        public Task<ExpenseResult> AmendAsync(
            Guid expenseId, string description, ExpenseCategory category, Money netAmount, Money vatAmount, bool billable, CancellationToken cancellationToken = default) =>
            inner.AmendAsync(expenseId, description, category, netAmount, vatAmount, billable, cancellationToken);

        public Task<ExpenseResult> SetSupplierAsync(Guid expenseId, string? supplierOrganisationId, string? supplierInvoiceNumber, CancellationToken cancellationToken = default) =>
            inner.SetSupplierAsync(expenseId, supplierOrganisationId, supplierInvoiceNumber, cancellationToken);

        public Task<ExpenseResult> DeleteAsync(Guid expenseId, CancellationToken cancellationToken = default) => inner.DeleteAsync(expenseId, cancellationToken);

        public Task<ExpenseResult> MarkInvoicedAsync(Guid expenseId, Guid requestId, CancellationToken cancellationToken = default) =>
            inner.MarkInvoicedAsync(expenseId, requestId, cancellationToken);

        public Task<IReadOnlyList<ProjectExpense>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            inner.ListForProjectAsync(projectId, cancellationToken);

        public Task<IReadOnlyList<ProjectExpense>> ListUnbilledForProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            inner.ListUnbilledForProjectAsync(projectId, cancellationToken);
    }

    [Fact]
    public async Task ARecordedExpense_WithItsSupplier_IsADraftBill_StillRechargeable_AndDeletedWithIt()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ExpenseTestHost.StartAsync(temp.Path);
        try
        {
            ExpenseTestHost.SignIn(host);
            var domain = ExpenseTestHost.Domain(host);
            var organisations = ExpenseTestHost.Organisations(host);
            using var kit = await PurchasingSyncTestKit.CreateAsync(
                new DomainXeroPurchaseOrderSource(domain, organisations), new DomainXeroExpenseSource(domain, organisations), organisations: organisations);

            var projectId = await ExpenseTestHost.CreateProjectAsync(host, "P0012", "Bracket programme");
            var expenses = ExpenseTestHost.Expenses(host);
            var recorded = await expenses.RecordAsync(
                projectId, new DateOnly(2026, 9, 28), "Steel offcuts", ExpenseCategory.Materials,
                new Money(400m, CurrencyCode.Gbp), new Money(80m, CurrencyCode.Gbp), billable: true);
            Assert.True(recorded.Succeeded, recorded.Reason);
            var expenseId = recorded.Expense!.Id;

            var supplied = await expenses.SetSupplierAsync(expenseId, PurchasingSyncTestKit.SupplierReference, " NS-4471 ");
            Assert.True(supplied.Succeeded, supplied.Reason);
            Assert.Equal("NS-4471", supplied.Expense!.SupplierInvoiceNumber);

            await kit.PlanExpenseAsync(expenseId);
            await kit.DrainAsync();
            var bill = Assert.Single(kit.LiveBills);
            Assert.Equal("NS-4471", bill.Number);
            Assert.Equal("DRAFT", bill.Status);
            Assert.Equal(kit.SupplierContactId, bill.Body["Contact"]!["ContactID"]!.GetValue<string>());
            Assert.Equal("P0012 · Steel offcuts", bill.Body["LineItems"]![0]!["Description"]!.GetValue<string>());
            Assert.Equal(80m, bill.Body["LineItems"]![0]!["TaxAmount"]!.GetValue<decimal>());

            // Recharges are unchanged: the expense is still unbilled to the client.
            Assert.Contains(await expenses.ListUnbilledForProjectAsync(projectId), e => e.Id == expenseId);

            // Deleted in TempestOS → the draft bill is deleted in Xero.
            Assert.True((await expenses.DeleteAsync(expenseId)).Succeeded);
            Assert.Equal(XeroOperation.DeleteExpenseBill, Assert.Single(await kit.PlanExpenseAsync(expenseId)).Operation);
            await kit.DrainAsync();
            Assert.Equal("DELETED", Assert.Single(kit.Bills).Status);

            // A deleted expense's supplier can no longer be set.
            Assert.Equal(ExpenseRefusal.ExpenseNotFound, (await expenses.SetSupplierAsync(expenseId, null, null)).Refusal);
            Assert.Equal(ExpenseRefusal.ExpenseNotFound, (await expenses.SetSupplierAsync(Guid.NewGuid(), null, null)).Refusal);
            kit.AssertNoViolations();
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task TheExpensesNewOptionalFields_SurviveARestart_AndAnExpenseWithoutThemReadsThemEmpty()
    {
        using var temp = new TempDirectory();
        Guid plainId, suppliedId, sourceOrderId = Guid.NewGuid();

        {
            var (host, manager) = await ExpenseTestHost.StartAsync(temp.Path);
            ExpenseTestHost.SignIn(host);
            var projectId = await ExpenseTestHost.CreateProjectAsync(host, "EXP-X5");
            var expenses = ExpenseTestHost.Expenses(host);

            plainId = (await expenses.RecordAsync(
                projectId, new DateOnly(2026, 9, 1), "Parking", ExpenseCategory.Travel,
                new Money(6m, CurrencyCode.Gbp), new Money(1m, CurrencyCode.Gbp), billable: false)).Expense!.Id;

            var supplied = (await expenses.RecordAsync(
                projectId, new DateOnly(2026, 9, 2), "Fixings", ExpenseCategory.Materials,
                new Money(30m, CurrencyCode.Gbp), new Money(6m, CurrencyCode.Gbp), billable: true, sourceOrderId)).Expense!;
            suppliedId = supplied.Id;
            Assert.Equal(sourceOrderId, supplied.SourcePurchaseOrderId);
            Assert.True((await expenses.SetSupplierAsync(suppliedId, "STEEL1", "INV-9")).Succeeded);

            // Clearing the supplier keeps the source order.
            Assert.True((await expenses.SetSupplierAsync(suppliedId, "STEEL1", null)).Succeeded);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }

        {
            var (host, manager) = await ExpenseTestHost.StartAsync(temp.Path);
            try
            {
                var rehydration = await Tempest.Workspace.Composition.EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);
                Assert.True(rehydration.IsComplete, "Expected a clean rehydration.");
                var domain = ExpenseTestHost.Domain(host);

                var plain = Assert.IsType<ProjectExpense>(await domain.Repository.FindAsync(plainId));
                Assert.Null(plain.SupplierOrganisationId);
                Assert.Null(plain.SupplierInvoiceNumber);
                Assert.Null(plain.SourcePurchaseOrderId);
                Assert.Equal("Parking", plain.Description);

                var supplied = Assert.IsType<ProjectExpense>(await domain.Repository.FindAsync(suppliedId));
                Assert.Equal("STEEL1", supplied.SupplierOrganisationId);
                Assert.Null(supplied.SupplierInvoiceNumber);
                Assert.Equal(sourceOrderId, supplied.SourcePurchaseOrderId);
                Assert.Equal(new Money(30m, CurrencyCode.Gbp), supplied.NetAmount);
            }
            finally
            {
                await manager.ShutdownAsync();
                await host.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task TheRegisterPurchasingHook_RegistersEachServiceOnce_AndTheyResolve()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ExpenseTestHost.StartAsync(temp.Path);
        try
        {
            using var kit = await PurchasingSyncTestKit.CreateAsync();

            var services = new ServiceCollection();
            services.AddInstance(ExpenseTestHost.Domain(host));
            services.AddInstance(ExpenseTestHost.Organisations(host));
            services.AddInstance(kit.Api);
            services.AddInstance<IXeroLinkStore>(kit.Links);
            services.AddInstance<IXeroOutbox>(kit.Outbox);
            services.AddInstance<IPersistenceStore>(kit.Store);
            services.AddInstance<ISecretStore>(kit.Secrets);
            services.AddInstance(kit.Linker);
            services.AddInstance<IXeroSettingsReader>(kit.Settings);
            services.AddInstance<ISettingsProvider>(kit.SettingsProvider);
            services.Singleton<XeroTaxTypeResolver>();
            services.Singleton<XeroAccountCodeMap>();
            XeroServiceRegistration.AddXeroPurchasing(services);
            var provider = new TempestServiceProvider(services);

            Assert.IsType<DomainXeroPurchaseOrderSource>(provider.GetService(typeof(IXeroPurchaseOrderSource)));
            Assert.IsType<DomainXeroExpenseSource>(provider.GetService(typeof(IXeroExpenseSource)));
            Assert.Equal(XeroDocumentKind.PurchaseOrder, ((XeroPurchaseOrderPlanner)provider.GetService(typeof(XeroPurchaseOrderPlanner))).Kind);
            Assert.Equal(XeroDocumentKind.ExpenseBill, ((XeroExpenseBillPlanner)provider.GetService(typeof(XeroExpenseBillPlanner))).Kind);
            Assert.Contains(XeroOperation.PushPurchaseOrder, ((XeroPurchaseOrderPushHandler)provider.GetService(typeof(XeroPurchaseOrderPushHandler))).Operations);
            Assert.Contains(XeroOperation.PushExpenseBill, ((XeroExpenseBillPushHandler)provider.GetService(typeof(XeroExpenseBillPushHandler))).Operations);
            Assert.Equal(XeroDocumentKind.PurchaseOrder, ((XeroPurchaseOrderAttachmentHandler)provider.GetService(typeof(XeroPurchaseOrderAttachmentHandler))).DocumentKind);
            Assert.Equal(XeroDocumentKind.ExpenseBill, ((XeroExpenseBillAttachmentHandler)provider.GetService(typeof(XeroExpenseBillAttachmentHandler))).DocumentKind);
            Assert.NotNull(provider.GetService(typeof(XeroGeneralExpensesContact)));
            Assert.NotNull(provider.GetService(typeof(XeroPurchasingSyncState)));

            // A second registration of any of them is an error, not a replacement (ADR-0122).
            Assert.ThrowsAny<Exception>(() => XeroServiceRegistration.AddXeroPurchasing(services));
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
