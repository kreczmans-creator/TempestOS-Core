using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Commands;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Projects;
using Tempest.Core.Timesheets;
using Tempest.Workspace.Timesheets;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// Runbook G1 (Product Owner: "task should be drop down from project
/// deliverables"): time recorded against one of the project's own
/// deliverables stores that deliverable's id and its "identifier — title"
/// label, survives a restart, and refuses a deliverable of any other
/// project; a free-text entry recorded the old way still carries no
/// deliverable and reads exactly as before.
/// </summary>
public sealed class TimesheetDeliverableTaskTests
{
    private const string Grade = "Senior";
    private static readonly DateOnly Monday = new(2026, 3, 2);

    [Fact]
    public async Task RecordAgainstDeliverable_StoresTheDeliverableAndItsLabel_AndSurvivesARestart()
    {
        using var temp = new TempDirectory();
        Guid entryId;
        Guid deliverableId;
        string expectedLabel;

        {
            var (host, manager) = await ProjectCommercialTestHost.StartAsync(temp.Path);
            ProjectCommercialTestHost.SignIn(host);
            var projectId = await CreatePricedProjectAsync(host, "TS-DEL-A");

            var deliverable = await ProjectCommercialTestHost.Deliverables(host).AddDeliverableAsync(projectId, "Structural calcs");
            deliverableId = deliverable.Id;
            expectedLabel = TimesheetService.DeliverableTaskLabel(deliverable);
            Assert.EndsWith("Structural calcs", expectedLabel, StringComparison.Ordinal);

            var result = await ProjectCommercialTestHost.Timesheets(host)
                .RecordAgainstDeliverableAsync(projectId, deliverableId, Monday, 3m, billable: true, Grade);

            Assert.True(result.Succeeded, result.Reason);
            entryId = result.Entry!.Id;
            Assert.Equal(deliverableId, result.Entry.DeliverableId);
            Assert.Equal(expectedLabel, result.Entry.TaskDescription);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }

        {
            var (host, manager) = await ProjectCommercialTestHost.StartAsync(temp.Path);
            ProjectCommercialTestHost.SignIn(host);
            var rehydration = await Tempest.Workspace.Composition.EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);
            Assert.True(rehydration.IsComplete, "Expected a clean rehydration.");

            var reread = (TimesheetEntry)(await ProjectCommercialTestHost.Domain(host).Repository.FindAsync(entryId))!;
            Assert.Equal(deliverableId, reread.DeliverableId);
            Assert.Equal(expectedLabel, reread.TaskDescription);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task RecordAgainstDeliverable_OfAnotherProject_OrUnknown_IsRefused()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ProjectCommercialTestHost.StartAsync(temp.Path);
        try
        {
            ProjectCommercialTestHost.SignIn(host);
            var projectId = await CreatePricedProjectAsync(host, "TS-DEL-B");
            var otherProjectId = await ProjectCommercialTestHost.CreateProjectAsync(host, "COM-PRJ-OTHER", "Other project");
            var otherDeliverable = await ProjectCommercialTestHost.Deliverables(host).AddDeliverableAsync(otherProjectId, "Someone else's work");
            var timesheets = ProjectCommercialTestHost.Timesheets(host);

            var foreign = await timesheets.RecordAgainstDeliverableAsync(projectId, otherDeliverable.Id, Monday, 1m, billable: true, Grade);
            Assert.Equal(TimesheetRefusal.DeliverableNotOnProject, foreign.Refusal);

            var unknown = await timesheets.RecordAgainstDeliverableAsync(projectId, Guid.NewGuid(), Monday, 1m, billable: true, Grade);
            Assert.Equal(TimesheetRefusal.DeliverableNotOnProject, unknown.Refusal);

            var deleted = await ProjectCommercialTestHost.Deliverables(host).AddDeliverableAsync(projectId, "Withdrawn");
            await deleted.DeleteAsync();
            var refusedDeleted = await timesheets.RecordAgainstDeliverableAsync(projectId, deleted.Id, Monday, 1m, billable: true, Grade);
            Assert.Equal(TimesheetRefusal.DeliverableNotOnProject, refusedDeleted.Refusal);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task FreeTextEntry_RecordedTheOldWay_CarriesNoDeliverable_AndReadsAsBefore()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ProjectCommercialTestHost.StartAsync(temp.Path);
        try
        {
            ProjectCommercialTestHost.SignIn(host);
            var projectId = await CreatePricedProjectAsync(host, "TS-DEL-C");

            var result = await ProjectCommercialTestHost.Timesheets(host).RecordAsync(projectId, Monday, 2m, billable: true, Grade, "Legacy free text");

            Assert.True(result.Succeeded, result.Reason);
            Assert.Null(result.Entry!.DeliverableId);
            Assert.Equal("Legacy free text", result.Entry.TaskDescription);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task RecordCommand_WithADeliverable_DispatchesThroughTheDeliverablePath()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ProjectCommercialTestHost.StartAsync(temp.Path);
        try
        {
            ProjectCommercialTestHost.SignIn(host);
            var projectId = await CreatePricedProjectAsync(host, "TS-DEL-D");
            var deliverable = await ProjectCommercialTestHost.Deliverables(host).AddDeliverableAsync(projectId, "Drawings pack");
            var dispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

            var result = await dispatcher.DispatchAsync(
                new RecordTimesheetCommand(projectId, Monday, 1.5m, billable: true, Grade, "whatever the caller displayed", deliverable.Id),
                CancellationToken.None);

            Assert.True(result.Succeeded, result.Message);
            var entry = (TimesheetEntry)(await ProjectCommercialTestHost.Domain(host).Repository.FindAsync(result.SubjectId!.Value))!;
            Assert.Equal(deliverable.Id, entry.DeliverableId);
            Assert.Equal(TimesheetService.DeliverableTaskLabel(deliverable), entry.TaskDescription);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task<Guid> CreatePricedProjectAsync(Core.Runtime.ITempestHost host, string cardId)
    {
        var projectId = await ProjectCommercialTestHost.CreateProjectAsync(host);
        var rateCards = ProjectCommercialTestHost.RateCards(host);
        var card = BusinessGovernanceFixtures.Card(cardId) with
        {
            Entries =
            [
                new RateCardEntry("ENG-1", "Senior engineering", PricingBasis.Hourly, new Money(100m, CurrencyCode.Gbp), Grade: Grade),
            ],
        };
        await rateCards.RegisterAsync(cardId, card, BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, cardId);
        Assert.True((await ProjectCommercialTestHost.ProjectCommercial(host).PinRateCardAsync(projectId, cardId)).Succeeded);
        return projectId;
    }
}
