using Tempest.Core.Events;
using Tempest.Core.Requirements;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 20.1A1` (`TD-28` closure) — end-to-end proof, through the real
/// production composition root a docked view resolves its own collaborators
/// from (<see cref="WorkspaceHost"/>, not a hand-built <c>TempestHostBuilder</c>),
/// that a Requirements write now reaches <see cref="IWorkspaceChanges"/>. A
/// docked Requirements Explorer or Cockpit resolves <see cref="IWorkspaceChanges"/>
/// once and subscribes to <see cref="IWorkspaceChanges.Changed"/> for the
/// life of the view; this test stands in for that subscriber with a fake
/// listener (the alternative the brief itself names, alongside the real
/// <c>RequirementsWorkspaceExplorerModule</c> node provider) and asserts it
/// is notified from the commit alone — no re-entry, no explicit reload call
/// anywhere in this test, mirroring <see cref="WorkspaceChangeFeedJourneyTests"/>'s
/// own "reload from the feed alone" proof for Mechanical.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class RequirementsChangeBusIntegrationTests
{
    [Fact]
    public async Task SetStatusAsync_DockedStyleSubscriberSeesTheChange_WithNoReEntry()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
            var workspaceChanges = (IWorkspaceChanges)host.Services!.GetService(typeof(IWorkspaceChanges));

            var requirement = await requirementsService.CreateAsync("REQ-CB-001", "The system shall publish its own writes.");

            // A docked-style subscriber: resolves IWorkspaceChanges once,
            // subscribes once, and reacts to whatever Changed reports — it
            // never re-queries or re-enters anything to notice a change.
            var received = new List<WorkspaceChange>();
            workspaceChanges.Changed += received.Add;

            await requirementsService.SetStatusAsync(requirement.Id, RequirementStatus.Reviewed);

            var change = Assert.Single(received);
            var entry = Assert.Single(change.Entries, e => e.ObjectId == requirement.Id);
            Assert.Equal(RequirementsService.RequirementDocumentKind, entry.Kind);
            Assert.Equal(WorkspaceChangeType.StatusChanged, entry.ChangeType);
            Assert.True(change.Sequence > 0, "A committed write must report a real, advanced sequence number.");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
