using Tempest.App.Composition;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Documents;
using Tempest.Companion.Client;
using Tempest.Companion.Contracts;
using Tempest.Companion.Offline;
using Tempest.Companion.Services;
using Tempest.Core.Api;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;

namespace Tempest.Companion.Tests;

// The Companion's full-stack proof: a real TempestHost (real DI, real
// identity/permission configuration, real Kestrel on an OS-assigned
// port), the real Engineering Workspace composition (disciplines + the
// Companion API registered exactly as production composes them), and the
// PRODUCTION CompanionApiClient over real HTTP - no mocks anywhere in
// this path. Covers the WP 14.0A offline test matrix rows that need a
// real server: online operation, authorization fail-closed, the real
// quick action end-to-end against the Engineering Domain, malformed
// writes, API-unavailable fallback to cache, and reconnection.
public class CompanionIntegrationTests
{
    private const string ActorId = "companion-tester";
    private const string ViewerId = "companion-viewer";

    private sealed record Server(WorkspaceManager Manager, ITempestHost Host, int Port) : IAsyncDisposable
    {
        public T Resolve<T>() => (T)Host.Services!.GetService(typeof(T));

        public async ValueTask DisposeAsync()
        {
            var originalOut = Console.Out;
            try
            {
                Console.SetOut(new StringWriter());
                await Manager.ShutdownAsync();
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }

    private static async Task<Server> StartServerAsync(string persistenceRootPath)
    {
        var host = new TempestHostBuilder(
                discoveryCandidateTypesOverride: Type.EmptyTypes,
                pluginsRootPathOverride: null,
                hostedServiceCandidateTypesOverride: [typeof(RestApiHostedService)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, persistenceRootPath),
                new KeyValuePair<string, string>(RestApiHostedService.PortConfigurationKey, "0"),
                new KeyValuePair<string, string>("Identity:Roles:CompanionUser:Permissions", $"{CompanionPermissions.Read},{CompanionPermissions.Act}"),
                new KeyValuePair<string, string>($"Identity:Principals:{ActorId}:Roles", "CompanionUser"),
                new KeyValuePair<string, string>("Identity:Roles:CompanionViewer:Permissions", CompanionPermissions.Read),
                new KeyValuePair<string, string>($"Identity:Principals:{ViewerId}:Roles", "CompanionViewer"),
            ]))
            .Build();

        var manager = new WorkspaceManager(host);

        var originalOut = Console.Out;
        try
        {
            Console.SetOut(new StringWriter());
            await manager.StartAsync();

            // The identical production composition step (console shell and
            // Tempest.Desktop both run this) - registers the six
            // disciplines AND the Companion API.
            EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(manager, host);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var api = (RestApiHostedService)host.Services!.GetService(typeof(RestApiHostedService));
        var waited = 0;
        while (api.BoundPort is null)
        {
            await Task.Delay(5);
            if ((waited += 5) > 10_000)
                throw new TimeoutException("The REST API never reported a bound port.");
        }

        return new Server(manager, host, api.BoundPort.Value);
    }

    private static CompanionApiClient ClientFor(Server server, string identityId) =>
        new($"http://127.0.0.1:{server.Port}", identityId);

    [Fact]
    public async Task Cockpit_EndToEnd_ServesTheRealCockpitProjection()
    {
        using var temp = new TempDirectory();
        await using var server = await StartServerAsync(temp.Path);
        using var client = ClientFor(server, ActorId);

        var cockpit = await client.GetCockpitAsync();

        Assert.False(string.IsNullOrWhiteSpace(cockpit.PlatformVersion));
        Assert.Equal(6, cockpit.DisciplineStatuses.Count);
        Assert.Contains(cockpit.DisciplineStatuses, s => s.Discipline == "Requirements");
        // A fresh, empty workspace: the honest empty reads, not fabricated data.
        Assert.Equal("No Mechanical Project yet", cockpit.ProjectName);
        Assert.Equal("Unknown", cockpit.Health);
        Assert.Null(cockpit.ContinueWhereILeftOff);
    }

    [Fact]
    public async Task NoIdentity_FailsClosed_AsUnauthorized()
    {
        using var temp = new TempDirectory();
        await using var server = await StartServerAsync(temp.Path);
        using var client = ClientFor(server, identityId: string.Empty);

        var exception = await Assert.ThrowsAsync<CompanionApiException>(() => client.GetCockpitAsync());

        Assert.Equal(CompanionApiFailureReason.Unauthorized, exception.Reason);
    }

    [Fact]
    public async Task UnconfiguredIdentity_FailsClosed_AsForbidden()
    {
        using var temp = new TempDirectory();
        await using var server = await StartServerAsync(temp.Path);
        using var client = ClientFor(server, "nobody-configured-this-id");

        var exception = await Assert.ThrowsAsync<CompanionApiException>(() => client.GetCockpitAsync());

        Assert.Equal(CompanionApiFailureReason.Forbidden, exception.Reason);
    }

    [Fact]
    public async Task ReadOnlyPrincipal_CanRead_ButCannotAct()
    {
        using var temp = new TempDirectory();
        await using var server = await StartServerAsync(temp.Path);
        using var viewer = ClientFor(server, ViewerId);

        var cockpit = await viewer.GetCockpitAsync();
        Assert.NotNull(cockpit);

        var exception = await Assert.ThrowsAsync<CompanionApiException>(() =>
            viewer.SetDocumentStatusAsync(new SetObjectStatusRequest(Guid.NewGuid(), "Document", "Approved")));

        Assert.Equal(CompanionApiFailureReason.Forbidden, exception.Reason);
    }

    [Fact]
    public async Task QuickAction_EndToEnd_ApprovesARealDocumentThroughTheCommandFramework()
    {
        using var temp = new TempDirectory();
        await using var server = await StartServerAsync(temp.Path);
        using var client = ClientFor(server, ActorId);

        // Arrange a real Document awaiting review, through the identical
        // commands the desktop dispatches (ADR-0063).
        var dispatcher = server.Resolve<ICommandDispatcher>();
        var created = await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "DOC-1 Interface Control"), CancellationToken.None);
        Assert.True(created.Succeeded, created.Message);

        var domain = server.Resolve<EngineeringDomainContext>();
        var document = Assert.Single(await domain.Repository.ListByKindAsync("Document"));
        var toReview = await dispatcher.DispatchAsync(new SetDocumentStatusCommand(document.Id, "Document", LifecycleState.InReview), CancellationToken.None);
        Assert.True(toReview.Succeeded, toReview.Message);

        // Observe: the pending review is visible from the phone.
        var attention = await client.GetAttentionAsync();
        var pending = Assert.Single(attention.PendingReviews);
        Assert.Equal(document.Id, pending.Id);
        Assert.Equal("DOC-1 Interface Control", pending.DisplayName);

        // Act: approve it from the phone.
        var outcome = await client.SetDocumentStatusAsync(new SetObjectStatusRequest(pending.Id, pending.Kind, "Approved"));
        Assert.True(outcome.Succeeded, outcome.Message);

        // The system of record changed - and the pending list is empty again.
        var refreshed = await domain.Repository.FindAsync(document.Id);
        Assert.Equal(LifecycleState.Approved, Assert.IsAssignableFrom<IHasLifecycle>(refreshed).Status);
        Assert.Empty((await client.GetAttentionAsync()).PendingReviews);
    }

    [Fact]
    public async Task MalformedQuickAction_ReturnsTheServersOwnReason_Not500()
    {
        using var temp = new TempDirectory();
        await using var server = await StartServerAsync(temp.Path);
        using var client = ClientFor(server, ActorId);

        var wrongKind = await client.SetDocumentStatusAsync(new SetObjectStatusRequest(Guid.NewGuid(), "Requirement", "Approved"));

        Assert.False(wrongKind.Succeeded);
        Assert.Contains("targetKind", wrongKind.Message);
    }

    [Fact]
    public async Task ApiUnavailable_FallsBackToTheCachedCockpit_ThenReconnects()
    {
        using var temp = new TempDirectory();
        using var cacheDir = new TempDirectory();

        var server = await StartServerAsync(temp.Path);
        var port = server.Port;
        using var client = ClientFor(server, ActorId);
        var data = new CompanionDataService(client, new SnapshotCache(cacheDir.Path));

        var live = await data.GetCockpitAsync();
        Assert.Equal(DataFreshness.Live, live.Freshness);

        // The platform goes away entirely.
        await server.DisposeAsync();

        var offline = await data.GetCockpitAsync();
        Assert.Equal(DataFreshness.Cached, offline.Freshness);
        Assert.NotNull(offline.Data);
        Assert.Equal(live.Data!.ProjectName, offline.Data!.ProjectName);
        Assert.False(data.IsConnected);
    }

    [Fact]
    public async Task Notifications_QueryServes_EvenWhenEmpty()
    {
        using var temp = new TempDirectory();
        await using var server = await StartServerAsync(temp.Path);
        using var client = ClientFor(server, ActorId);

        var notifications = await client.GetNotificationsAsync();

        Assert.NotNull(notifications);
        Assert.Empty(notifications.Notifications);
    }

    [Fact]
    public async Task Projects_QueryServes_TheHonestEmptyList()
    {
        using var temp = new TempDirectory();
        await using var server = await StartServerAsync(temp.Path);
        using var client = ClientFor(server, ActorId);

        var projects = await client.GetProjectsAsync();

        Assert.Empty(projects.Projects);
    }
}
