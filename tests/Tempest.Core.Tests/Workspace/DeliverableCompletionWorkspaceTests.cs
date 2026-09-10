using Tempest.Workspace;
using Tempest.Workspace.Deliverables;
using Tempest.Workspace.Projects;
using Tempest.Core.Commands;
using Tempest.Core.Deliverables;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Projects;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `WP 19.0A` acceptance #3: <c>deliverable.complete</c> resolves the
/// selected deliverable's own project from its ancestry and returns the
/// new completion's own subject id and Kind; the node provider lists it
/// under its own project and deliverable; the facet provider names the
/// deliverable and the principal, not a bare id (`ADR-0150`).
/// </summary>
public sealed class DeliverableCompletionWorkspaceTests : IAsyncLifetime
{
    private TempDirectory _temp = null!;
    private ITempestHost _host = null!;
    private WorkspaceManager _manager = null!;

    public async Task InitializeAsync()
    {
        _temp = new TempDirectory();
        (_host, _manager) = await ProjectCommercialTestHost.StartAsync(_temp.Path);
        ProjectCommercialTestHost.SignIn(_host);
    }

    public async Task DisposeAsync()
    {
        await _manager.ShutdownAsync();
        await _host.DisposeAsync();
        _temp.Dispose();
    }

    private static CommandParameterPrompt Answering(params (string Name, string Value)[] answers) =>
        (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
            answers.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal));

    private async Task<(Guid ProjectId, Guid DeliverableId)> SetUpDeliverableAsync(string identifier = "DEL-WS-1")
    {
        var projectId = await ProjectCommercialTestHost.CreateProjectAsync(_host);
        var domain = ProjectCommercialTestHost.Domain(_host);

        var milestoneService = new ProjectMilestoneService(domain);
        var milestone = await milestoneService.CreateMilestoneAsync(projectId, $"MS-{identifier}", "Milestone", DateTimeOffset.UtcNow.AddDays(30));
        var deliverable = await milestoneService.CreateDeliverableAsync(projectId, milestone.Id, identifier, "Workspace Test Deliverable");

        return (projectId, deliverable.Id);
    }

    [Fact]
    public async Task Complete_ActsOnTheSelectedDeliverable_AndReturnsTheCompletionAsSubject()
    {
        var (projectId, deliverableId) = await SetUpDeliverableAsync();
        var registry = (ICommandRegistry)_host.Services!.GetService(typeof(ICommandRegistry));
        var context = CommandContext.For(deliverableId, Tempest.Workspace.CanonicalObjectKinds.Deliverable);

        var invocation = await registry.InvokeAsync("deliverable.complete", context, Answering(("completedOn", "2026-04-01")));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);
        Assert.NotNull(invocation.Result.SubjectId);
        Assert.Equal(DeliverableCompletion.CanonicalKind, invocation.Result.SubjectKind);

        var domain = ProjectCommercialTestHost.Domain(_host);
        var created = (DeliverableCompletion)(await domain.Repository.FindAsync(invocation.Result.SubjectId!.Value))!;
        Assert.Equal(projectId, created.ParentId);
        Assert.Equal(deliverableId, created.DeliverableId);
    }

    [Fact]
    public async Task Complete_ForAnUnknownDeliverable_IsRefused_NotThrown()
    {
        var registry = (ICommandRegistry)_host.Services!.GetService(typeof(ICommandRegistry));
        var context = CommandContext.For(Guid.NewGuid(), Tempest.Workspace.CanonicalObjectKinds.Deliverable);

        var invocation = await registry.InvokeAsync("deliverable.complete", context, Answering(("completedOn", "2026-04-01")));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.False(invocation.Result!.Succeeded);
    }

    [Fact]
    public async Task TheNodeProvider_ListsANewCompletion_UnderItsOwnProjectAndDeliverable()
    {
        var (projectId, deliverableId) = await SetUpDeliverableAsync();
        var deliverables = ProjectCommercialTestHost.Deliverables(_host);
        var completion = await deliverables.CompleteAsync(deliverableId, projectId, new DateOnly(2026, 4, 2));
        Assert.True(completion.Succeeded);

        var domain = ProjectCommercialTestHost.Domain(_host);
        var provider = new DeliverableCompletionNodeProvider(DeliverableCompletionWorkspaceRegistration.ExplorerAreaId, domain);

        var roots = await provider.GetRootNodesAsync();
        var projectNode = Assert.Single(roots, n => n.Id == projectId);
        Assert.True(projectNode.HasChildren);

        var deliverableGroups = await provider.GetChildrenAsync(projectId);
        var deliverableGroup = Assert.Single(deliverableGroups);
        Assert.Contains("Workspace Test Deliverable", deliverableGroup.Title, StringComparison.Ordinal);

        var members = await provider.GetChildrenAsync(deliverableGroup.Id);
        var memberNode = Assert.Single(members, m => m.Id == completion.Completion!.Id);
        Assert.Contains("2026-04-02", memberNode.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFacetProvider_NamesTheDeliverableAndPrincipal_NotBareIds()
    {
        var (projectId, deliverableId) = await SetUpDeliverableAsync();
        var deliverables = ProjectCommercialTestHost.Deliverables(_host);
        var completion = await deliverables.CompleteAsync(deliverableId, projectId, new DateOnly(2026, 4, 3));
        Assert.True(completion.Succeeded);

        var domain = ProjectCommercialTestHost.Domain(_host);
        var principals = (IPrincipalDirectory)_host.Services!.GetService(typeof(IPrincipalDirectory));
        var provider = new DeliverableCompletionPropertyFacetProvider(DeliverableCompletion.CanonicalKind, domain, principals);

        var facets = await provider.GetFacetsAsync(completion.Completion!.Id);

        var deliverableFacet = Assert.Single(facets, f => f.Name == "Deliverable");
        Assert.DoesNotContain(deliverableId.ToString(), deliverableFacet.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Workspace Test Deliverable", deliverableFacet.Value, StringComparison.Ordinal);

        var principalFacet = Assert.Single(facets, f => f.Name == "Principal");
        Assert.Equal(principals.Describe(ProjectCommercialTestHost.PrincipalId), principalFacet.Value);
    }
}
