using System.Text.Json.Nodes;
using Tempest.Core.Events;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Quotations;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Engine;
using Tempest.Core.Tests.Invoicing.Xero.Sync.Invoices;

namespace Tempest.Core.Tests.Invoicing.Xero;

/// <summary>
/// `v0.24.0` design §7.3 item 3: whatever TempestOS does — every outbox
/// operation driven by the X6 engine, and the invoice send — in any order,
/// under 503s, lost answers, 429s and a lost grant, no Xero invoice, bill or
/// purchase order ever reaches <c>AUTHORISED</c> or <c>SUBMITTED</c> (or
/// <c>PAID</c>, <c>BILLED</c>, <c>VOIDED</c>) except through the simulator's
/// back-office acts, no write carries a status other than <c>DRAFT</c> or
/// <c>DELETED</c> (quotes: the D2 walk), nothing is ever emailed, and the
/// simulator records no violation (D3, D4, D7 or contract). Sequences are
/// randomised from fixed seeds, so every run is the same.
/// </summary>
public sealed class XeroNeverApprovesOrSendsTests
{
    private static readonly string[] WrittenInvoiceStatuses = ["DRAFT", "DELETED"];
    private static readonly string[] WrittenQuoteStatuses = ["DRAFT", "SENT", "ACCEPTED", "DECLINED"];

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(2026)]
    public async Task QuotesOrdersAndBills_InAnyOrderUnderFaults_NeverLeaveDraftThroughTempestOs(int seed)
    {
        using var kit = await EngineTestKit.CreateAsync();
        var random = new Random(seed);
        var approvedByHand = new HashSet<string>(StringComparer.Ordinal);
        var quotes = new List<Guid>();
        var orders = new List<Guid>();
        var expenses = new List<Guid>();

        for (var step = 0; step < 45; step++)
        {
            switch (random.Next(12))
            {
                case 0:
                    quotes.Add(kit.ExportQuote($"P0012-Q-{quotes.Count + 1:000}"));
                    break;
                case 1 when quotes.Count > 0:
                {
                    var id = quotes[random.Next(quotes.Count)];
                    var next = kit.Quotes[id].Status switch
                    {
                        QuotationStatus.Approved => QuotationStatus.Sent,
                        QuotationStatus.Sent => random.Next(2) == 0 ? QuotationStatus.Accepted : QuotationStatus.Declined,
                        var same => same,
                    };
                    kit.SetQuoteStatus(id, next);
                    break;
                }

                case 2:
                    orders.Add(kit.IssueOrder($"PO-2026-{orders.Count + 1:000}"));
                    break;
                case 3 when orders.Count > 0:
                {
                    var id = orders[random.Next(orders.Count)];
                    kit.Orders[id] = kit.Orders[id] with { Status = PurchaseOrderStatus.Cancelled };
                    kit.Saved(PurchaseOrder.CanonicalKind, id, WorkspaceChangeType.StatusChanged);
                    break;
                }

                case 4:
                    expenses.Add(kit.RecordExpense(random.Next(2) == 0 ? null : EngineTestKit.SupplierReference));
                    break;
                case 5 when expenses.Count > 0:
                {
                    var id = expenses[random.Next(expenses.Count)];
                    if (!kit.Expenses[id].IsDeleted)
                    {
                        kit.Expenses[id] = random.Next(3) == 0
                            ? kit.Expenses[id] with { IsDeleted = true }
                            : kit.Expenses[id] with { NetAmount = kit.Expenses[id].NetAmount + 10m, VatAmount = kit.Expenses[id].VatAmount + 2m };
                        kit.Saved(ProjectExpense.CanonicalKind, id);
                    }

                    break;
                }

                case 6:
                    InjectFault(kit, random);
                    break;
                case 7:
                    kit.Clock.Advance(TimeSpan.FromSeconds(random.Next(1, 120)));
                    break;
                case 8:
                {
                    // The Product Owner approves a draft in Xero — the only way past DRAFT.
                    var drafts = kit.Simulator.All("Invoices").Concat(kit.Simulator.All("PurchaseOrders")).Where(d => d.Status == "DRAFT").ToList();
                    if (drafts.Count > 0)
                    {
                        var draft = drafts[random.Next(drafts.Count)];
                        kit.Simulator.ApproveInXero(draft.Id);
                        approvedByHand.Add(draft.Id);
                    }

                    break;
                }

                case 9:
                    kit.Connection.Status = random.Next(2) == 0 ? ConnectorAuthorisation.Expired : ConnectorAuthorisation.Authorised;
                    break;
                case 10:
                    kit.Clock.Advance(kit.Options.ReadBackInterval);
                    await kit.Engine.RunCycleAsync();
                    break;
                default:
                    await kit.Engine.RunCycleAsync();
                    break;
            }
        }

        // Whatever state it was left in, the queue drains to rest (the Product Owner re-authorises whenever asked).
        kit.Connection.Status = ConnectorAuthorisation.Authorised;
        for (var i = 0; i < 120; i++)
        {
            kit.Clock.Advance(TimeSpan.FromMinutes(1));
            if ((await kit.Engine.RunCycleAsync()).Drain.PausedForAuthorisation)
                await kit.Engine.NotifyAuthorisedAsync();
        }

        AssertNeverApprovedOrSent(kit.Simulator, approvedByHand);
        Assert.Empty(kit.Simulator.Violations);

        // The run did real work, and nothing is left in flight or waiting: only what a Failed write holds back.
        Assert.NotEmpty(kit.Simulator.All("Quotes").Concat(kit.Simulator.All("PurchaseOrders")).Concat(kit.Bills));
        await AssertAtRestAsync(kit.Outbox);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(11)]
    public async Task Invoices_SentVoidedAndReadBackInAnyOrder_NeverLeaveDraftThroughTempestOs(int seed)
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var random = new Random(seed);
        var approvedByHand = new HashSet<string>(StringComparer.Ordinal);
        var parts = new XeroSyncParts(
            kit.Links, kit.Outbox, kit.SecretStore,
            invoicePlanner: new XeroInvoicePlanner(kit.Service, kit.Files),
            invoiceHandler: new XeroInvoicePushHandler(kit.Service, kit.Drafts),
            invoiceAttachments: new XeroInvoiceAttachmentPushHandler(kit.Drafts),
            domain: kit.Domain);
        var engine = new XeroSyncService(
            parts, kit.Outbox, kit.Store, kit.Reader, new FakeConnectionState(),
            readBack: new XeroReadBack(kit.Api, kit.Links, kit.Service, null, kit.Audit, kit.Clock),
            audit: kit.Audit, timeProvider: kit.Clock, options: new XeroSyncOptions { Jitter = () => 0.5 });

        var (projectId, organisationId) = await kit.AddProjectAsync($"N{seed}");
        await kit.LinkClientAsync(organisationId);
        var requests = new List<Guid>();
        for (var step = 0; step < 14; step++)
        {
            switch (random.Next(6))
            {
                case 0:
                case 1:
                {
                    var suffix = $"N{seed}x{step}";
                    var raised = await kit.RaiseAsync(projectId, suffix);
                    kit.Loss.LoseInvoiceCreates = random.Next(3) == 0 ? 1 : 0;
                    await kit.Service.SendAsync(raised.Id);
                    kit.Files.Save(raised.Id, $"{suffix}.pdf", $"sheet {suffix}");
                    await engine.PlanDocumentAsync(XeroInvoiceDrafts.DocumentFor(raised.Id));
                    requests.Add(raised.Id);
                    break;
                }

                case 2 when requests.Count > 0:
                    await kit.Service.VoidAsync(requests[random.Next(requests.Count)]);
                    await engine.ScanAsync();
                    break;
                case 3:
                {
                    var drafts = kit.LiveSalesInvoices.Where(d => d.Status == "DRAFT").ToList();
                    if (drafts.Count > 0)
                    {
                        var draft = drafts[random.Next(drafts.Count)];
                        kit.Simulator.ApproveInXero(draft.Id);
                        approvedByHand.Add(draft.Id);
                    }

                    break;
                }

                case 4:
                    kit.Clock.Advance(engine.Options.ReadBackInterval);
                    await engine.RunCycleAsync();
                    break;
                default:
                    kit.Clock.Advance(TimeSpan.FromSeconds(random.Next(1, 60)));
                    await engine.RunCycleAsync();
                    break;
            }
        }

        for (var i = 0; i < 10; i++)
        {
            kit.Clock.Advance(TimeSpan.FromMinutes(1));
            await engine.RunCycleAsync();
        }

        AssertNeverApprovedOrSent(kit.Simulator, approvedByHand);
        kit.AssertSafe();
        Assert.NotEmpty(kit.SalesInvoices);
        await AssertAtRestAsync(kit.Outbox);
    }

    /// <summary>Nothing in flight, unknown or waiting; a Pending entry only behind a Failed one for its document (per-document order).</summary>
    private static async Task AssertAtRestAsync(IXeroOutbox outbox)
    {
        var all = await outbox.ListAsync([]);
        Assert.DoesNotContain(all, e => e.State is XeroOutboxState.InFlight or XeroOutboxState.Unknown or XeroOutboxState.WaitingForAuthorisation);
        foreach (var pending in all.Where(e => e.State == XeroOutboxState.Pending))
        {
            var queue = all.Where(e => e.Document == pending.Document).ToList();
            Assert.Contains(queue.Take(queue.IndexOf(pending)), e => e.State == XeroOutboxState.Failed);
        }
    }

    private static void InjectFault(EngineTestKit kit, Random random)
    {
        switch (random.Next(4))
        {
            case 0:
                kit.Simulator.Inject(new XeroFault(XeroFaultKind.ServiceUnavailable, Times: random.Next(1, 3)));
                break;
            case 1:
                kit.Hop.LoseNewWrites = 1;
                break;
            case 2:
                kit.Simulator.Inject(new XeroFault(XeroFaultKind.RateLimitedMinute, RetryAfter: TimeSpan.FromSeconds(random.Next(1, 30))));
                break;
            default:
                kit.Simulator.Inject(new XeroFault(XeroFaultKind.Unauthorised));
                break;
        }
    }

    /// <summary>No document past DRAFT except those approved by hand (and what follows from them), no forbidden status written, nothing emailed.</summary>
    private static void AssertNeverApprovedOrSent(XeroApiSimulator simulator, IReadOnlySet<string> approvedByHand)
    {
        foreach (var document in simulator.All("Invoices").Concat(simulator.All("PurchaseOrders")))
        {
            if (document.Status is "AUTHORISED" or "SUBMITTED" or "PAID" or "BILLED" or "VOIDED")
                Assert.True(approvedByHand.Contains(document.Id), $"{document.Resource} {document.Number} is {document.Status}, but nobody approved it in Xero.");
        }

        foreach (var request in simulator.Requests)
        {
            Assert.False(request.Path.EndsWith("/Email", StringComparison.OrdinalIgnoreCase), $"TempestOS called {request.Path}.");
            if (request.Method == HttpMethod.Get || request.JsonBody is not JsonObject body)
                continue;

            foreach (var (resource, allowed) in new[] { ("Invoices", WrittenInvoiceStatuses), ("PurchaseOrders", WrittenInvoiceStatuses), ("Quotes", WrittenQuoteStatuses) })
            {
                foreach (var item in body[resource]?.AsArray() ?? [])
                {
                    var status = (string?)item!["Status"];
                    Assert.True(status is null || allowed.Contains(status), $"TempestOS wrote {resource} status {status}.");
                    Assert.Null(item["SentToContact"]);
                }
            }
        }
    }
}
