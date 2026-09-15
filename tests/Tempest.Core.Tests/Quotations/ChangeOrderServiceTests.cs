using Tempest.Core.BusinessGovernance;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Quotations;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Quotations;

/// <summary>
/// The `WP 20.10E` change-order half of `IQuotationService` (Product Owner
/// finding D18, `ADR-0152` addendum): <see cref="QuotationKind.ChangeOrder"/>
/// generates its own <c>CO-&lt;yyyy&gt;-&lt;nnn&gt;</c> reference,
/// independent of the ordinary <c>Q-</c> sequence; a line can carry an
/// already-existing, live deliverable, only on a change order; Accept
/// creates no new deliverable for a carried line, only a Requirement.
/// <see cref="Projects.ProjectLifecycleServiceTests"/> covers the sign-off
/// rule itself.
/// </summary>
public sealed class ChangeOrderServiceTests
{
    [Fact]
    public async Task CreateAsync_ChangeOrder_GeneratesItsOwnCOReference_IndependentOfTheOrdinaryQSequence()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "CO-REF");
        var quotations = QuotationTestHost.Quotations(host);

        var quote = await quotations.CreateAsync(projectId);
        Assert.True(quote.Succeeded, quote.Reason);
        var year = quote.Quotation!.QuoteDate.Year;
        Assert.Equal($"Q-{year}-001", quote.Quotation.Reference);

        var firstChangeOrder = await quotations.CreateAsync(projectId, kind: QuotationKind.ChangeOrder);
        Assert.True(firstChangeOrder.Succeeded, firstChangeOrder.Reason);
        Assert.Equal(QuotationKind.ChangeOrder, firstChangeOrder.Quotation!.QuotationKind);
        Assert.Equal($"CO-{year}-001", firstChangeOrder.Quotation.Reference);

        // A second ordinary quotation still continues the Q- sequence, unaffected by the CO- one.
        var secondQuote = await quotations.CreateAsync(projectId);
        Assert.Equal($"Q-{year}-002", secondQuote.Quotation!.Reference);
        Assert.Equal(QuotationKind.Quotation, secondQuote.Quotation.QuotationKind);

        var secondChangeOrder = await quotations.CreateAsync(projectId, kind: QuotationKind.ChangeOrder);
        Assert.Equal($"CO-{year}-002", secondChangeOrder.Quotation!.Reference);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task AddLineAsync_CarriedDeliverableId_RefusedOnAnOrdinaryQuotation()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "CO-NOTCARRY");
        var quotations = QuotationTestHost.Quotations(host);
        var deliverables = QuotationTestHost.Deliverables(host);

        var deliverable = await deliverables.AddDeliverableAsync(projectId, "Unrelated deliverable");
        var quote = await quotations.CreateAsync(projectId);

        var lineAdded = await quotations.AddLineAsync(
            quote.Quotation!.Id, "Should be refused", null, null, new Money(100m, CurrencyCode.Gbp), carriedDeliverableId: deliverable.Id);

        Assert.False(lineAdded.Succeeded);
        Assert.Equal(QuotationRefusal.InvalidLine, lineAdded.Refusal);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task AddLineAsync_CarriedDeliverableId_RefusedWhenNoLiveDeliverableMatchesIt()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "CO-MISSING");
        var quotations = QuotationTestHost.Quotations(host);

        var changeOrder = await quotations.CreateAsync(projectId, kind: QuotationKind.ChangeOrder);

        var lineAdded = await quotations.AddLineAsync(
            changeOrder.Quotation!.Id, "Carries nothing real", null, null, new Money(100m, CurrencyCode.Gbp), carriedDeliverableId: Guid.NewGuid());

        Assert.False(lineAdded.Succeeded);
        Assert.Equal(QuotationRefusal.DeliverableNotFound, lineAdded.Refusal);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task AcceptAsync_ChangeOrderLineCarryingAnExistingDeliverable_CreatesNoNewDeliverable_ButCreatesARequirement()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);

        var projectId = await QuotationTestHost.CreateProjectAsync(host, "CO-ACCEPT");
        var domain = QuotationTestHost.Domain(host);
        var quotations = QuotationTestHost.Quotations(host);
        var deliverables = QuotationTestHost.Deliverables(host);

        var deliverable = await deliverables.AddDeliverableAsync(projectId, "Carried deliverable");

        // `WP 21.5B`: list results are index rows — Kind and IsDeleted are on the row.
        var deliverablesBefore = (await domain.Repository.ListByKindAsync("Deliverable"))
            .Count(d => !d.IsDeleted);

        var changeOrder = await quotations.CreateAsync(projectId, kind: QuotationKind.ChangeOrder);
        Assert.True(changeOrder.Succeeded, changeOrder.Reason);

        var lineAdded = await quotations.AddLineAsync(
            changeOrder.Quotation!.Id, "Additional scope on the carried deliverable", null, null, new Money(650m, CurrencyCode.Gbp),
            carriedDeliverableId: deliverable.Id);
        Assert.True(lineAdded.Succeeded, lineAdded.Reason);
        Assert.Equal(deliverable.Id, lineAdded.Quotation!.Lines[0].DeliverableId);

        Assert.True((await quotations.SendAsync(changeOrder.Quotation.Id)).Succeeded);
        var accepted = await quotations.AcceptAsync(changeOrder.Quotation.Id);
        Assert.True(accepted.Succeeded, accepted.Reason);

        var line = Assert.Single(accepted.Quotation!.Lines);
        Assert.Equal(deliverable.Id, line.DeliverableId);
        Assert.NotNull(line.RequirementId);

        // No new Deliverable was created — the carried line's own id is the pre-existing one.
        var deliverablesAfter = (await domain.Repository.ListByKindAsync("Deliverable"))
            .Count(d => !d.IsDeleted);
        Assert.Equal(deliverablesBefore, deliverablesAfter);

        // No milestone was created for the change order either — every one of its lines was carried.
        var milestoneNamedAfterTheChangeOrder = (await domain.Repository.ListChildrenAsync(projectId))
            .Any(m => m.Kind == "Milestone" && m.DisplayName == changeOrder.Quotation.Reference);
        Assert.False(milestoneNamedAfterTheChangeOrder);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }
}
