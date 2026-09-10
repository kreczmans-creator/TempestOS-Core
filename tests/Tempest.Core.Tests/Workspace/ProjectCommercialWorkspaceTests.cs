using Tempest.Workspace;
using Tempest.Workspace.Composition;
using Tempest.Workspace.Mechanical;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `WP 19.0A` acceptance #3: every project commercial command resolves
/// the selected project as its own target and returns the project's own
/// subject id and Kind; pinning a Draft card is refused by name
/// (`ADR-0150`).
/// </summary>
public sealed class ProjectCommercialWorkspaceTests : IAsyncLifetime
{
    private TempDirectory _temp = null!;
    private ITempestHost _host = null!;
    private WorkspaceManager _manager = null!;
    private ICommandRegistry _registry = null!;
    private EngineeringDomainContext _domain = null!;

    public async Task InitializeAsync()
    {
        _temp = new TempDirectory();

        _host = new TempestHostBuilder([typeof(MechanicalWorkspaceExplorerModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, _temp.Path),
            ]))
            .Build();
        _manager = new WorkspaceManager(_host);

        await _manager.StartAsync();

        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(_manager, _host);

        _registry = (ICommandRegistry)_host.Services!.GetService(typeof(ICommandRegistry));
        _domain = (EngineeringDomainContext)_host.Services!.GetService(typeof(EngineeringDomainContext));
    }

    public async Task DisposeAsync()
    {
        await _manager.ShutdownAsync();
        await _host.DisposeAsync();
        _temp.Dispose();
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var factory = new EngineeringObjectFactory<Project>(
            MechanicalObjectFactoryRegistry.Project, _domain,
            (doc, rev) => new Project(doc, rev, _domain, "COM-WS-PRJ", "Commercial Workspace Test Project", EngineeringObjectMetadata.Empty));
        var project = await factory.CreateAsync("Commercial workspace test project.");
        return project.Id;
    }

    private static CommandParameterPrompt Answering(params (string Name, string Value)[] answers) =>
        (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
            answers.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal));

    [Theory]
    [InlineData("project.set-client", "organisationId", "ORG-99")]
    [InlineData("project.set-purchase-order", "reference", "PO-99")]
    [InlineData("project.set-project-manager", "identityId", "pm-99")]
    public async Task ASingleFieldCommand_ActsOnTheSelectedProject_AndReturnsItAsSubject(string commandId, string field, string value)
    {
        var projectId = await CreateProjectAsync();
        var context = CommandContext.For(projectId, MechanicalObjectFactoryRegistry.Project);

        var invocation = await _registry.InvokeAsync(commandId, context, Answering((field, value)));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);
        Assert.Equal(projectId, invocation.Result.SubjectId);
        Assert.Equal(MechanicalObjectFactoryRegistry.Project, invocation.Result.SubjectKind);
    }

    [Fact]
    public async Task SetBudget_ParsesAmountAndCurrency_AndReturnsTheProjectAsSubject()
    {
        var projectId = await CreateProjectAsync();
        var context = CommandContext.For(projectId, MechanicalObjectFactoryRegistry.Project);

        var invocation = await _registry.InvokeAsync("project.set-budget", context, Answering(("budget", "25000 GBP")));

        Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);

        var project = (Project)(await _domain.Repository.FindAsync(projectId))!;
        Assert.Equal(25000m, project.Budget!.Value.Amount);
    }

    [Fact]
    public async Task SetDates_ParsesBothDates_AndReturnsTheProjectAsSubject()
    {
        var projectId = await CreateProjectAsync();
        var context = CommandContext.For(projectId, MechanicalObjectFactoryRegistry.Project);

        var invocation = await _registry.InvokeAsync(
            "project.set-dates", context, Answering(("startDate", "2026-04-01"), ("targetDate", "2026-10-01")));

        Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);

        var project = (Project)(await _domain.Repository.FindAsync(projectId))!;
        Assert.Equal(new DateOnly(2026, 4, 1), project.StartDate);
        Assert.Equal(new DateOnly(2026, 10, 1), project.TargetDate);
    }

    [Fact]
    public async Task PinRateCard_RefusesADraftCard_ByName()
    {
        var projectId = await CreateProjectAsync();
        var context = CommandContext.For(projectId, MechanicalObjectFactoryRegistry.Project);

        var rateCards = (IRateCardCatalog)_host.Services!.GetService(typeof(IRateCardCatalog));
        await rateCards.RegisterAsync("DRAFT-CARD", BusinessGovernanceFixtures.Card("DRAFT-CARD"), BusinessGovernanceFixtures.Verified());

        var invocation = await _registry.InvokeAsync("project.pin-rate-card", context, Answering(("rateCardId", "DRAFT-CARD")));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.False(invocation.Result!.Succeeded);
        Assert.Contains("DRAFT-CARD", invocation.Result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PinRateCard_AcceptsAReleasedCard_AndReturnsTheProjectAsSubject()
    {
        var projectId = await CreateProjectAsync();
        var context = CommandContext.For(projectId, MechanicalObjectFactoryRegistry.Project);

        var rateCards = (IRateCardCatalog)_host.Services!.GetService(typeof(IRateCardCatalog));
        await rateCards.RegisterAsync("RELEASED-CARD", BusinessGovernanceFixtures.Card("RELEASED-CARD"), BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)rateCards, "RELEASED-CARD");

        var invocation = await _registry.InvokeAsync("project.pin-rate-card", context, Answering(("rateCardId", "RELEASED-CARD")));

        Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);
        Assert.Equal(projectId, invocation.Result.SubjectId);
        Assert.Equal(MechanicalObjectFactoryRegistry.Project, invocation.Result.SubjectKind);
    }
}
