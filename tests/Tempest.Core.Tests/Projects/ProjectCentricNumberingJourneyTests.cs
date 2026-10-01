using Tempest.Core.Quotations;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.PurchaseOrders;
using Tempest.Core.Tests.Quotations;

namespace Tempest.Core.Tests.Projects;

/// <summary>
/// Project-centric numbering through the real services (Product Owner
/// decision 2026-10-01 §3, `ADR-0156`): inside a project identified
/// <c>CUSTOMER-PROJECTREF</c>, every generated quotation, change order and
/// purchase order reference reads <c>CUSTOMER-PROJECTREF-DOCTYPE-NNN</c>,
/// in a sequence per project per document type starting at 001 — so the
/// first quote in every project is 001 — while a project with any other
/// identifier keeps the old <c>&lt;prefix&gt;&lt;year&gt;-&lt;nnn&gt;</c> scheme
/// unchanged, and the sequence survives a restart.
/// </summary>
public sealed class ProjectCentricNumberingJourneyTests
{
    [Fact]
    public async Task Quotations_ArePerProjectPerType_FirstQuoteInEveryProjectIs001_OldProjectsKeepTheOldScheme_AndItSurvivesARestart()
    {
        using var temp = new TempDirectory();

        Guid bridge;
        {
            var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
            QuotationTestHost.SignIn(host);
            var quotations = QuotationTestHost.Quotations(host);

            bridge = await QuotationTestHost.CreateProjectAsync(host, "ACMEE-BRIDG", "Bridge");
            var tower = await QuotationTestHost.CreateProjectAsync(host, "ACMEE-TOWER", "Tower");
            var legacy = await QuotationTestHost.CreateProjectAsync(host, "P-0001", "Legacy");

            Assert.Equal("ACMEE-BRIDG-Q-001", (await quotations.CreateAsync(bridge)).Quotation!.Reference);
            Assert.Equal("ACMEE-BRIDG-Q-002", (await quotations.CreateAsync(bridge)).Quotation!.Reference);
            Assert.Equal("ACMEE-TOWER-Q-001", (await quotations.CreateAsync(tower)).Quotation!.Reference);
            Assert.Equal("ACMEE-BRIDG-CO-001", (await quotations.CreateAsync(bridge, kind: QuotationKind.ChangeOrder)).Quotation!.Reference);

            var legacyQuote = (await quotations.CreateAsync(legacy)).Quotation!;
            Assert.Equal($"Q-{legacyQuote.QuoteDate.Year}-001", legacyQuote.Reference);

            // A given reference is still kept as given.
            Assert.Equal("CUSTOM-1", (await quotations.CreateAsync(bridge, reference: "CUSTOM-1")).Quotation!.Reference);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }

        {
            var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
            QuotationTestHost.SignIn(host);

            var rehydration = await Tempest.Workspace.Composition.EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);
            Assert.True(rehydration.IsComplete, "Expected a clean rehydration.");

            var next = await QuotationTestHost.Quotations(host).CreateAsync(bridge);
            Assert.True(next.Succeeded, next.Reason);
            Assert.Equal("ACMEE-BRIDG-Q-003", next.Quotation!.Reference);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task PurchaseOrders_ArePerProject_StartingAt001_OldProjectsKeepTheOldScheme()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await PurchaseOrderTestHost.StartAsync(temp.Path);
        PurchaseOrderTestHost.SignIn(host);
        var orders = PurchaseOrderTestHost.PurchaseOrders(host);

        var bridge = await PurchaseOrderTestHost.CreateProjectAsync(host, "ACMEE-BRIDG", "Bridge");
        var tower = await PurchaseOrderTestHost.CreateProjectAsync(host, "OTHER-TOWER", "Tower");
        var legacy = await PurchaseOrderTestHost.CreateProjectAsync(host, "P-0002", "Legacy");

        Assert.Equal("ACMEE-BRIDG-PO-001", (await orders.CreateAsync(bridge)).Order!.Reference);
        Assert.Equal("ACMEE-BRIDG-PO-002", (await orders.CreateAsync(bridge)).Order!.Reference);
        Assert.Equal("OTHER-TOWER-PO-001", (await orders.CreateAsync(tower)).Order!.Reference);

        var legacyOrder = (await orders.CreateAsync(legacy)).Order!;
        Assert.StartsWith("PO-", legacyOrder.Reference, StringComparison.Ordinal);
        Assert.EndsWith("-001", legacyOrder.Reference, StringComparison.Ordinal);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }
}
