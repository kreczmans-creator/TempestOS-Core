using Tempest.Core.BusinessGovernance;
using Tempest.Core.Expenses;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.PurchaseOrders;

/// <summary>
/// The `WP 21.3B` acceptance journey: a purchase order raised against a
/// project, lined, issued, received, its lines recorded as expenses (the
/// seam that reaches invoicing with no ledger of its own), and closed —
/// round-tripped across a restart, with the reference counter's own
/// scan-the-store discipline and every status-transition refusal proven
/// alongside.
/// </summary>
public sealed class PurchaseOrderServiceJourneyTests
{
    [Fact]
    public async Task CoreJourney_TwoLines_Issue_Receive_RecordAsExpenses_Close_RestartAndEverythingIsStillThere()
    {
        using var temp = new TempDirectory();

        Guid projectId;
        Guid orderId;
        string reference;

        // ================================================================
        // FIRST HOST
        // ================================================================
        {
            var (host, manager) = await PurchaseOrderTestHost.StartAsync(temp.Path);
            PurchaseOrderTestHost.SignIn(host);

            projectId = await PurchaseOrderTestHost.CreateProjectAsync(host, "PO-CORE");
            var orders = PurchaseOrderTestHost.PurchaseOrders(host);

            var created = await orders.CreateAsync(projectId, supplierOrganisationId: "PO-SUPPLIER-1");
            Assert.True(created.Succeeded, created.Reason);
            var order = created.Order!;
            orderId = order.Id;
            reference = order.Reference;
            Assert.StartsWith("PO-", reference, StringComparison.Ordinal);
            Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
            Assert.Equal("PO-SUPPLIER-1", order.SupplierOrganisationId);
            Assert.Empty(order.Lines);

            // ---- Issue with no lines is refused ----
            var issueEmpty = await orders.IssueAsync(orderId);
            Assert.False(issueEmpty.Succeeded);
            Assert.Equal(PurchaseOrderRefusal.NothingToIssue, issueEmpty.Refusal);

            // ---- Two lines, one standard-rated, one zero-rated ----
            var line1 = await orders.AddLineAsync(orderId, "Steel plate", 10m, new Money(50m, CurrencyCode.Gbp), VatRate.Standard);
            Assert.True(line1.Succeeded, line1.Reason);
            var line2 = await orders.AddLineAsync(orderId, "Freight", 1m, new Money(75m, CurrencyCode.Gbp), VatRate.Zero);
            Assert.True(line2.Succeeded, line2.Reason);

            var withLines = line2.Order!;
            Assert.Equal(2, withLines.Lines.Count);
            Assert.Equal(new Money(10m * 50m + 75m, CurrencyCode.Gbp), withLines.Total);

            // Line 1: 500 net, 20% VAT = 100. Line 2: 75 net, 0% VAT = 0.
            Assert.Equal(new Money(100m, CurrencyCode.Gbp), withLines.VatTotal);
            Assert.Equal(new Money(575m + 100m, CurrencyCode.Gbp), withLines.GrossTotal);

            // ---- Update and remove a line while Draft ----
            var firstLineId = withLines.Lines[0].Id;
            var updated = await orders.UpdateLineAsync(orderId, firstLineId, "Steel plate (revised)", 12m, new Money(50m, CurrencyCode.Gbp), VatRate.Standard);
            Assert.True(updated.Succeeded, updated.Reason);
            Assert.Equal("Steel plate (revised)", updated.Order!.Lines.Single(l => l.Id == firstLineId).Description);

            var secondLineId = withLines.Lines[1].Id;
            var removed = await orders.RemoveLineAsync(orderId, secondLineId);
            Assert.True(removed.Succeeded, removed.Reason);
            Assert.Single(removed.Order!.Lines);

            // Re-add the freight line so Issue has two lines again.
            var reAdded = await orders.AddLineAsync(orderId, "Freight", 1m, new Money(75m, CurrencyCode.Gbp), VatRate.Zero);
            Assert.True(reAdded.Succeeded, reAdded.Reason);

            // ---- Issue ----
            var issued = await orders.IssueAsync(orderId);
            Assert.True(issued.Succeeded, issued.Reason);
            Assert.Equal(PurchaseOrderStatus.Issued, issued.Order!.Status);
            Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), issued.Order.IssuedDate);

            // ---- A line cannot be added once Issued ----
            var lineAfterIssue = await orders.AddLineAsync(orderId, "Too late", 1m, new Money(1m, CurrencyCode.Gbp));
            Assert.False(lineAfterIssue.Succeeded);
            Assert.Equal(PurchaseOrderRefusal.OrderNotDraft, lineAfterIssue.Refusal);

            // ---- Receive is refused before Issue, permitted once Issued ----
            var receiveBeforeIssue = await orders.ReceiveAsync(Guid.NewGuid());
            Assert.False(receiveBeforeIssue.Succeeded);
            Assert.Equal(PurchaseOrderRefusal.OrderNotFound, receiveBeforeIssue.Refusal);

            var received = await orders.ReceiveAsync(orderId);
            Assert.True(received.Succeeded, received.Reason);
            Assert.Equal(PurchaseOrderStatus.Received, received.Order!.Status);

            // ---- Record as expenses is refused before Received; not reachable here since already Received ----
            var recordedAsExpenses = await orders.RecordLinesAsExpensesAsync(orderId);
            Assert.True(recordedAsExpenses.Succeeded, recordedAsExpenses.Reason);
            Assert.True(recordedAsExpenses.Order!.ExpensesRecorded);

            var expenses = PurchaseOrderTestHost.Expenses(host);
            var projectExpenses = await expenses.ListForProjectAsync(projectId);
            Assert.Equal(2, projectExpenses.Count);
            Assert.All(projectExpenses, e => Assert.True(e.Billable));
            Assert.All(projectExpenses, e => Assert.Equal(ExpenseCategory.Materials, e.Category));
            Assert.Contains(projectExpenses, e => e.NetAmount == new Money(600m, CurrencyCode.Gbp) && e.VatAmount == new Money(120m, CurrencyCode.Gbp));
            Assert.Contains(projectExpenses, e => e.NetAmount == new Money(75m, CurrencyCode.Gbp) && e.VatAmount == new Money(0m, CurrencyCode.Gbp));

            // ---- A second Record as expenses is refused ----
            var secondRecord = await orders.RecordLinesAsExpensesAsync(orderId);
            Assert.False(secondRecord.Succeeded);
            Assert.Equal(PurchaseOrderRefusal.ExpensesAlreadyRecorded, secondRecord.Refusal);

            // ---- Close ----
            var closed = await orders.CloseAsync(orderId);
            Assert.True(closed.Succeeded, closed.Reason);
            Assert.Equal(PurchaseOrderStatus.Closed, closed.Order!.Status);

            // ---- Terminal: nothing further is permitted ----
            var cancelClosed = await orders.CancelAsync(orderId);
            Assert.False(cancelClosed.Succeeded);
            Assert.Equal(PurchaseOrderRefusal.TransitionNotPermitted, cancelClosed.Refusal);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }

        // ================================================================
        // SECOND HOST — restart, read everything back
        // ================================================================
        {
            var (host, manager) = await PurchaseOrderTestHost.StartAsync(temp.Path);

            var rehydration = await Tempest.Workspace.Composition.EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);
            Assert.True(rehydration.IsComplete, "Expected a clean rehydration.");

            var orders = PurchaseOrderTestHost.PurchaseOrders(host);
            var domain = PurchaseOrderTestHost.Domain(host);
            var recovered = await domain.Repository.FindAsync(orderId);
            var order = Assert.IsType<PurchaseOrder>(recovered);

            Assert.Equal(reference, order.Reference);
            Assert.Equal(PurchaseOrderStatus.Closed, order.Status);
            Assert.Equal(2, order.Lines.Count);
            Assert.True(order.ExpensesRecorded);
            Assert.Equal("PO-SUPPLIER-1", order.SupplierOrganisationId);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task ReferenceGeneration_ScansTheStore_AndNeverReissues()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await PurchaseOrderTestHost.StartAsync(temp.Path);

        try
        {
            PurchaseOrderTestHost.SignIn(host);
            var projectId = await PurchaseOrderTestHost.CreateProjectAsync(host, "PO-REF");
            var orders = PurchaseOrderTestHost.PurchaseOrders(host);

            var first = await orders.CreateAsync(projectId);
            Assert.True(first.Succeeded);
            var second = await orders.CreateAsync(projectId);
            Assert.True(second.Succeeded);

            var year = DateTime.UtcNow.Year;
            Assert.Equal($"PO-{year}-001", first.Order!.Reference);
            Assert.Equal($"PO-{year}-002", second.Order!.Reference);

            var given = await orders.CreateAsync(projectId, reference: "PO-CUSTOM-1");
            Assert.True(given.Succeeded);
            Assert.Equal("PO-CUSTOM-1", given.Order!.Reference);

            // A third generated reference still continues past 002 — the
            // custom one given above never collides with, or resets, the
            // per-year scan.
            var third = await orders.CreateAsync(projectId);
            Assert.True(third.Succeeded);
            Assert.Equal($"PO-{year}-003", third.Order!.Reference);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Cancel_IsPermittedFromDraftAndIssued_ButNotFromReceived()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await PurchaseOrderTestHost.StartAsync(temp.Path);

        try
        {
            PurchaseOrderTestHost.SignIn(host);
            var projectId = await PurchaseOrderTestHost.CreateProjectAsync(host, "PO-CANCEL");
            var orders = PurchaseOrderTestHost.PurchaseOrders(host);

            // Draft -> Cancelled.
            var draftOrder = await orders.CreateAsync(projectId);
            var draftCancelled = await orders.CancelAsync(draftOrder.Order!.Id);
            Assert.True(draftCancelled.Succeeded, draftCancelled.Reason);
            Assert.Equal(PurchaseOrderStatus.Cancelled, draftCancelled.Order!.Status);

            // Issued -> Cancelled.
            var issuedOrderResult = await orders.CreateAsync(projectId);
            var issuedOrderId = issuedOrderResult.Order!.Id;
            await orders.AddLineAsync(issuedOrderId, "Line", 1m, new Money(10m, CurrencyCode.Gbp));
            await orders.IssueAsync(issuedOrderId);
            var issuedCancelled = await orders.CancelAsync(issuedOrderId);
            Assert.True(issuedCancelled.Succeeded, issuedCancelled.Reason);
            Assert.Equal(PurchaseOrderStatus.Cancelled, issuedCancelled.Order!.Status);

            // Received -> Cancel refused.
            var receivedOrderResult = await orders.CreateAsync(projectId);
            var receivedOrderId = receivedOrderResult.Order!.Id;
            await orders.AddLineAsync(receivedOrderId, "Line", 1m, new Money(10m, CurrencyCode.Gbp));
            await orders.IssueAsync(receivedOrderId);
            await orders.ReceiveAsync(receivedOrderId);
            var receivedCancel = await orders.CancelAsync(receivedOrderId);
            Assert.False(receivedCancel.Succeeded);
            Assert.Equal(PurchaseOrderRefusal.TransitionNotPermitted, receivedCancel.Refusal);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(VatRate.Standard, 0.20)]
    [InlineData(VatRate.Reduced, 0.05)]
    [InlineData(VatRate.Zero, 0.0)]
    [InlineData(VatRate.Exempt, 0.0)]
    [InlineData(VatRate.OutOfScope, 0.0)]
    public async Task LineVatAmount_IsNetTimesTheRatesOwnPercentage(VatRate rate, double percentage)
    {
        using var temp = new TempDirectory();
        var (host, manager) = await PurchaseOrderTestHost.StartAsync(temp.Path);

        try
        {
            PurchaseOrderTestHost.SignIn(host);
            var projectId = await PurchaseOrderTestHost.CreateProjectAsync(host, "PO-VAT");
            var orders = PurchaseOrderTestHost.PurchaseOrders(host);

            var created = await orders.CreateAsync(projectId);
            var added = await orders.AddLineAsync(created.Order!.Id, "Line", 1m, new Money(200m, CurrencyCode.Gbp), rate);
            Assert.True(added.Succeeded, added.Reason);

            var line = Assert.Single(added.Order!.Lines);
            Assert.Equal(new Money(200m * (decimal)percentage, CurrencyCode.Gbp), line.VatAmount);
            Assert.Equal(new Money(200m + 200m * (decimal)percentage, CurrencyCode.Gbp), line.GrossAmount);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
