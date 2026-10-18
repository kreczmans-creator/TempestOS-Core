using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;
using Tempest.Core.Quotations;
using static Tempest.Core.Tests.Invoicing.Xero.Sync.Engine.OrganisationSwitchKit;
using Legacy = Tempest.Core.Invoicing.Xero.Sync.XeroInvoiceLinkImporter.LegacyInvoiceLink;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;

/// <summary>
/// `v0.24.0` F1 (review board M1, the go-live path): the Product Owner tests
/// on the Demo Company, then connects a second organisation. End to end
/// through the simulator: records whose content did not change are not
/// silently lost there (they read <em>Not sent</em>, noting they went to
/// another organisation, and <em>Send to Xero</em> sends them); nothing from
/// the Demo testing floods the second organisation, not even a write that
/// was still waiting when the organisation changed; and no Demo Company
/// invoice id is imported as a link.
/// </summary>
public sealed class XeroOrganisationSwitchTests
{
    [Fact]
    public async Task DemoThenSecondOrganisation_NothingFloodsIn_NothingIsSilentlyLost_NoDemoInvoiceIsImported()
    {
        using var kit = await OrganisationSwitchKit.CreateAsync();

        // Invoices sent before v0.24.0: one to the Demo Company, one to the
        // organisation connected later. Neither recorded where it went.
        var startedAt = kit.Clock.GetUtcNow();
        var preDemoInvoice = await KeyInvoiceAsync(kit.DemoXero, "INV-0900");
        var preSecondInvoice = await KeyInvoiceAsync(kit.SecondXero, "INV-0901");
        var sentToDemoBefore = Guid.NewGuid();
        var sentToSecondBefore = Guid.NewGuid();
        kit.InvoiceRequests.Add(new Legacy(sentToDemoBefore, "Xero", preDemoInvoice, "INV-0900", "DRAFT", startedAt.AddDays(-30)));
        kit.InvoiceRequests.Add(new Legacy(sentToSecondBefore, "Xero", preSecondInvoice, "INV-0901", "DRAFT", startedAt.AddDays(-20)));
        var secondSeedWrites = WritesTo(kit.SecondXero).Count;

        // ---- Demo Company testing ----
        await kit.Engine.StartAsync();
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var demoQuote = kit.ExportQuote("P0012-Q-001");
        var demoOrder = kit.IssueOrder("PO-2026-001");
        var demoExpense = kit.RecordExpense();
        var waitingExpense = kit.RecordExpense(LateSupplierReference, "Fixings from Newco");
        await kit.SettleAsync();

        Assert.Single(QuotesIn(kit.DemoXero));
        Assert.Single(OrdersIn(kit.DemoXero));
        Assert.Single(BillsIn(kit.DemoXero));
        var waiting = await kit.Engine.GetDocumentStatusAsync(ExpenseRef(waitingExpense));
        Assert.Equal(XeroSyncBadge.Failed, waiting.Status.Badge); // Newco is not linked in the Demo Company

        // A v0.24.0 send to the Demo Company (its link written by X4 there).
        kit.Clock.Advance(TimeSpan.FromMinutes(5));
        var v024DemoInvoice = await KeyInvoiceAsync(kit.DemoXero, "INV-0902");
        var sentToDemoThroughSync = Guid.NewGuid();
        kit.InvoiceRequests.Add(new Legacy(sentToDemoThroughSync, "Xero", v024DemoInvoice, "INV-0902", "DRAFT", kit.Clock.GetUtcNow()));

        // The import into the Demo Company: only what it holds.
        var demoInvoiceLinks = await kit.Links.ListAsync(DemoTenant, XeroDocumentKind.Invoice);
        Assert.Equal([sentToDemoBefore.ToString("D")], demoInvoiceLinks.Select(l => l.Document.TempestKey));
        var demoWrites = WritesTo(kit.DemoXero).Count;

        // ---- Go-live the next day: the second organisation is connected ----
        kit.Clock.Advance(TimeSpan.FromDays(1));
        await kit.ConnectSecondOrganisationAsync();
        kit.Saved(Tempest.Core.Expenses.ProjectExpense.CanonicalKind, waitingExpense); // its supplier is linked now
        await kit.SettleAsync();

        // Nothing from the Demo testing reached the second organisation.
        Assert.Empty(QuotesIn(kit.SecondXero));
        Assert.Empty(OrdersIn(kit.SecondXero));
        Assert.Empty(BillsIn(kit.SecondXero));
        Assert.Equal(secondSeedWrites, WritesTo(kit.SecondXero).Count);

        // The waiting write was for the Demo Company: superseded, never sent here.
        var superseded = Assert.Single(await kit.Outbox.ListForDocumentAsync(ExpenseRef(waitingExpense)), e => e.Operation == XeroOperation.PushExpenseBill);
        Assert.Equal(XeroOutboxState.Superseded, superseded.State);
        Assert.Equal(XeroSyncService.OrganisationChangedReason, superseded.LastError);
        var changed = Assert.Single(kit.Audit.Rows, r => r.Action == XeroSyncService.AuditOrganisationChanged).Detail!;
        Assert.Equal(DemoTenant, changed["from"]);
        Assert.Equal(SecondTenant, changed["to"]);

        // Not silently lost: each Demo-era record reads Not sent, saying where it went.
        foreach (var document in new[] { QuoteRef(demoQuote), OrderRef(demoOrder), ExpenseRef(demoExpense) })
        {
            var status = (await kit.Engine.GetDocumentStatusAsync(document)).Status;
            Assert.Equal(XeroSyncBadge.NotSent, status.Badge);
            Assert.Equal(XeroSyncService.SentToAnotherOrganisationNote, status.Reason);
        }

        Assert.False(await kit.QuotePlanner.IsAutomaticAsync(kit.Quotes[demoQuote]));
        Assert.Equal(XeroSyncBadge.NotSent, (await kit.Engine.GetDocumentStatusAsync(ExpenseRef(waitingExpense))).Status.Badge);

        // No Demo Company invoice id became a link here; the one this organisation holds did.
        var secondInvoiceLinks = await kit.Links.ListAsync(SecondTenant, XeroDocumentKind.Invoice);
        var imported = Assert.Single(secondInvoiceLinks);
        Assert.Equal(sentToSecondBefore.ToString("D"), imported.Document.TempestKey);
        Assert.Equal(preSecondInvoice, imported.XeroId);
        Assert.DoesNotContain(secondInvoiceLinks, l => l.XeroId == preDemoInvoice || l.XeroId == v024DemoInvoice);

        // The Demo Company was not written to after the switch.
        Assert.Equal(demoWrites, WritesTo(kit.DemoXero).Count);

        // ---- Work in the second organisation ----
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var liveQuote = kit.ExportQuote("P0013-Q-001");
        await kit.SettleAsync();
        Assert.Equal(["P0013-Q-001"], QuotesIn(kit.SecondXero).Select(q => q.Number));

        // Send to Xero on the unchanged Demo-era records sends them here — the
        // Demo Company's succeeded writes no longer stand in for this organisation's.
        Assert.True((await kit.QuotePlanner.SendToXeroAsync(demoQuote)).Queued);
        Assert.True((await kit.OrderPlanner.SendToXeroAsync(demoOrder)).Queued);
        Assert.True((await kit.ExpensePlanner.SendToXeroAsync(demoExpense)).Queued);
        foreach (var document in new[] { QuoteRef(demoQuote), OrderRef(demoOrder), ExpenseRef(demoExpense) })
            Assert.Equal(XeroSyncBadge.Queued, (await kit.Engine.GetDocumentStatusAsync(document)).Status.Badge);

        await kit.SettleAsync();

        Assert.Equal(["P0012-Q-001", "P0013-Q-001"], QuotesIn(kit.SecondXero).Select(q => q.Number).Order(StringComparer.Ordinal));
        Assert.Single(OrdersIn(kit.SecondXero));
        Assert.Single(BillsIn(kit.SecondXero));
        foreach (var document in new[] { QuoteRef(demoQuote), QuoteRef(liveQuote), OrderRef(demoOrder), ExpenseRef(demoExpense) })
        {
            var status = (await kit.Engine.GetDocumentStatusAsync(document)).Status;
            Assert.Equal(XeroSyncBadge.InXeroDraft, status.Badge);
            Assert.NotNull(await kit.Links.FindAsync(SecondTenant, document));
        }

        // The Demo-era expense that was waiting still never reached the second organisation.
        Assert.Null(await kit.Links.FindAsync(SecondTenant, ExpenseRef(waitingExpense)));
        Assert.Equal(demoWrites, WritesTo(kit.DemoXero).Count);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task SwitchedWhileTempestOSWasClosed_ALostDemoWriteIsNeverReplayedIntoTheSecondOrganisation()
    {
        using var kit = await OrganisationSwitchKit.CreateAsync();
        await kit.Engine.StartAsync();
        kit.Clock.Advance(TimeSpan.FromMinutes(1));

        // Demo testing: one quote's create was in flight when TempestOS stopped,
        // another quote was queued but never sent.
        var lost = kit.ExportQuote("P0012-Q-001");
        var queued = kit.ExportQuote("P0012-Q-002");
        await kit.Engine.ScanAsync();
        var inFlight = await kit.Outbox.ClaimNextDueForTenantAsync([XeroOutboxState.Pending], DemoTenant);
        Assert.NotNull(inFlight);
        Assert.Equal(QuoteRef(lost), inFlight.Document);
        Assert.Equal(DemoTenant, inFlight.TenantId);
        kit.Engine.StopListening();

        // While closed, the Product Owner re-authorised into the second organisation.
        kit.Clock.Advance(TimeSpan.FromDays(1));
        await kit.Secrets.SetAsync(Tempest.Core.Invoicing.Xero.Contacts.XeroContactLinker.TenantIdSecretKey, SecondTenant);
        await kit.LinkAsync(kit.SecondXero, ClientReference, "Acme Engineering Ltd");

        // Restart: the in-flight create becomes Unknown — and is recovered
        // first — but it was the Demo Company's, so it is superseded instead.
        kit.Restart();
        await kit.Engine.StartAsync();
        await kit.SettleAsync();

        Assert.Empty(QuotesIn(kit.SecondXero));
        Assert.Empty(WritesTo(kit.SecondXero));
        foreach (var id in new[] { lost, queued })
        {
            Assert.All(await kit.Outbox.ListForDocumentAsync(QuoteRef(id)), e => Assert.Equal(XeroOutboxState.Superseded, e.State));
            Assert.Equal(XeroSyncBadge.NotSent, (await kit.Engine.GetDocumentStatusAsync(QuoteRef(id))).Status.Badge);
        }

        // Send to Xero still sends one there, once.
        Assert.True((await kit.QuotePlanner.SendToXeroAsync(lost)).Queued);
        await kit.SettleAsync();
        Assert.Equal(["P0012-Q-001"], QuotesIn(kit.SecondXero).Select(q => q.Number));
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task ReconnectingTheSameOrganisation_ChangesNothing()
    {
        using var kit = await OrganisationSwitchKit.CreateAsync();
        await kit.Engine.StartAsync();
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var quote = kit.ExportQuote("P0012-Q-001");
        await kit.SettleAsync();
        var began = await kit.QuotePlanner.AutomaticFromAsync();

        kit.Clock.Advance(TimeSpan.FromDays(1));
        await kit.Engine.NotifyAuthorisedAsync(); // re-authorised, same organisation
        kit.Restart();
        await kit.Engine.StartAsync();
        await kit.SettleAsync();

        Assert.Single(QuotesIn(kit.DemoXero));
        Assert.Equal(began, await kit.QuotePlanner.AutomaticFromAsync());
        Assert.DoesNotContain(kit.Audit.Rows, r => r.Action == XeroSyncService.AuditOrganisationChanged);
        Assert.Equal(XeroSyncBadge.InXeroDraft, (await kit.Engine.GetDocumentStatusAsync(QuoteRef(quote))).Status.Badge);
        kit.AssertNoViolations();
    }

    [Fact]
    public async Task AtStartUp_FinishedWritesPastTheRetention_ArePruned_AndNothingIsSentAgain()
    {
        using var kit = await OrganisationSwitchKit.CreateAsync();
        await kit.Engine.StartAsync();
        kit.Clock.Advance(TimeSpan.FromMinutes(1));
        var expense = kit.RecordExpense();
        await kit.SettleAsync();

        // Amended twice while still a draft bill: three pushes of the bill.
        foreach (var net in new[] { 110m, 120m })
        {
            kit.Expenses[expense] = kit.Expenses[expense] with { NetAmount = net };
            kit.Saved(Tempest.Core.Expenses.ProjectExpense.CanonicalKind, expense);
            await kit.SettleAsync();
        }

        var pushes = (await kit.Outbox.ListForDocumentAsync(ExpenseRef(expense))).Where(e => e.Operation == XeroOperation.PushExpenseBill).ToList();
        Assert.Equal(3, pushes.Count);
        var writes = WritesTo(kit.DemoXero).Count;

        kit.Clock.Advance(TimeSpan.FromDays(31));
        kit.Restart();
        await kit.Engine.StartAsync();
        await kit.SettleAsync();

        var left = (await kit.Outbox.ListForDocumentAsync(ExpenseRef(expense))).Where(e => e.Operation == XeroOperation.PushExpenseBill).ToList();
        Assert.Equal([pushes[^1].Id], left.Select(e => e.Id));
        Assert.Equal(XeroSyncBadge.InXeroDraft, (await kit.Engine.GetDocumentStatusAsync(ExpenseRef(expense))).Status.Badge);
        Assert.Equal(writes, WritesTo(kit.DemoXero).Count);
        kit.AssertNoViolations();
    }
}
