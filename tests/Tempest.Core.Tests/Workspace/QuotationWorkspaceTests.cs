using Tempest.Workspace;
using Tempest.Workspace.Quotations;
using Tempest.Core.Commands;
using Tempest.Core.Quotations;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Quotations;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `WP 19.5A` acceptance: <c>quotation.create</c> reads the shell's own
/// open project from <c>CommandContext.ProjectId</c> and returns the new
/// quotation's own subject id and Kind; <c>quotation.add-line</c>/
/// <c>quotation.send</c>/<c>quotation.accept</c>/<c>quotation.decline</c>
/// each act on the selected quotation; the node provider lists it under
/// its own project and status group; the facet provider names the
/// reference and total, not a bare id (`ADR-0152`).
/// </summary>
public sealed class QuotationWorkspaceTests : IAsyncLifetime
{
    private TempDirectory _temp = null!;
    private ITempestHost _host = null!;
    private WorkspaceManager _manager = null!;

    public async Task InitializeAsync()
    {
        _temp = new TempDirectory();
        (_host, _manager) = await QuotationTestHost.StartAsync(_temp.Path);
        QuotationTestHost.SignIn(_host);
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

    [Fact]
    public async Task Create_ReadsTheShellsOwnOpenProject_AndReturnsTheQuotationAsSubject()
    {
        var projectId = await QuotationTestHost.CreateProjectAsync(_host);
        var registry = (ICommandRegistry)_host.Services!.GetService(typeof(ICommandRegistry));
        var context = new CommandContext([], projectId);

        var invocation = await registry.InvokeAsync(QuotationCommandIds.Create, context, Answering(("reference", string.Empty)));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);
        Assert.NotNull(invocation.Result.SubjectId);
        Assert.Equal(Quotation.CanonicalKind, invocation.Result.SubjectKind);

        var domain = QuotationTestHost.Domain(_host);
        var created = (Quotation)(await domain.Repository.FindAsync(invocation.Result.SubjectId!.Value))!;
        Assert.Equal(projectId, created.ParentId);
    }

    [Fact]
    public async Task Create_WithNoProjectOpen_IsRefused_NotThrown()
    {
        var registry = (ICommandRegistry)_host.Services!.GetService(typeof(ICommandRegistry));
        var context = new CommandContext([], projectId: null);

        var invocation = await registry.InvokeAsync(QuotationCommandIds.Create, context, Answering(("reference", string.Empty)));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.False(invocation.Result!.Succeeded);
    }

    [Fact]
    public async Task AddLineSendAcceptDecline_EachActOnTheSelectedQuotation()
    {
        var projectId = await QuotationTestHost.CreateProjectAsync(_host);
        var quotations = QuotationTestHost.Quotations(_host);
        var created = await quotations.CreateAsync(projectId);
        Assert.True(created.Succeeded);
        var quotationId = created.Quotation!.Id;

        var registry = (ICommandRegistry)_host.Services!.GetService(typeof(ICommandRegistry));
        var context = CommandContext.For(quotationId, Quotation.CanonicalKind);

        var addLine = await registry.InvokeAsync(
            QuotationCommandIds.AddLine, context,
            Answering(("description", "Design review"), ("hours", "4"), ("rate", "150 GBP"), ("fixedPrice", string.Empty)));
        Assert.Equal(CommandOutcome.Executed, addLine.Outcome);
        Assert.True(addLine.Result!.Succeeded, addLine.Result.Message);

        var send = await registry.InvokeAsync(QuotationCommandIds.Send, context, Answering());
        Assert.Equal(CommandOutcome.Executed, send.Outcome);
        Assert.True(send.Result!.Succeeded, send.Result.Message);

        var accept = await registry.InvokeAsync(QuotationCommandIds.Accept, context, Answering());
        Assert.Equal(CommandOutcome.Executed, accept.Outcome);
        Assert.True(accept.Result!.Succeeded, accept.Result.Message);

        var domain = QuotationTestHost.Domain(_host);
        var quotation = (Quotation)(await domain.Repository.FindAsync(quotationId))!;
        Assert.Equal(QuotationStatus.Accepted, quotation.Status);
        Assert.Single(quotation.Lines);
        Assert.NotNull(quotation.Lines[0].DeliverableId);

        // A second quotation, declined instead.
        var declineCreated = await quotations.CreateAsync(projectId);
        await quotations.AddLineAsync(declineCreated.Quotation!.Id, "Another line", 1m, new Core.BusinessGovernance.Money(1m, Core.BusinessGovernance.CurrencyCode.Gbp), null);
        await quotations.SendAsync(declineCreated.Quotation.Id);
        var declineContext = CommandContext.For(declineCreated.Quotation.Id, Quotation.CanonicalKind);

        var decline = await registry.InvokeAsync(QuotationCommandIds.Decline, declineContext, Answering());
        Assert.Equal(CommandOutcome.Executed, decline.Outcome);
        Assert.True(decline.Result!.Succeeded, decline.Result.Message);

        var declinedQuotation = (Quotation)(await domain.Repository.FindAsync(declineCreated.Quotation.Id))!;
        Assert.Equal(QuotationStatus.Declined, declinedQuotation.Status);
    }

    [Fact]
    public async Task TheNodeProvider_ListsANewQuotation_UnderItsOwnProjectAndStatusGroup()
    {
        var projectId = await QuotationTestHost.CreateProjectAsync(_host);
        var quotations = QuotationTestHost.Quotations(_host);
        var created = await quotations.CreateAsync(projectId, reference: "Q-WS-1");
        Assert.True(created.Succeeded);

        var domain = QuotationTestHost.Domain(_host);
        var provider = new QuotationNodeProvider(QuotationWorkspaceRegistration.ExplorerAreaId, domain);

        var roots = await provider.GetRootNodesAsync();
        var projectNode = Assert.Single(roots, n => n.Id == projectId);
        Assert.True(projectNode.HasChildren);

        var statusGroups = await provider.GetChildrenAsync(projectId);
        var draftGroup = Assert.Single(statusGroups, g => g.Title == "Draft");
        Assert.True(draftGroup.HasChildren);

        var members = await provider.GetChildrenAsync(draftGroup.Id);
        var memberNode = Assert.Single(members, m => m.Id == created.Quotation!.Id);
        Assert.Contains("Q-WS-1", memberNode.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFacetProvider_NamesTheReferenceAndTotal_NotABareId()
    {
        var projectId = await QuotationTestHost.CreateProjectAsync(_host);
        var quotations = QuotationTestHost.Quotations(_host);
        var created = await quotations.CreateAsync(projectId, reference: "Q-WS-2");
        Assert.True(created.Succeeded);
        var lineAdded = await quotations.AddLineAsync(
            created.Quotation!.Id, "Line one", null, null, new Core.BusinessGovernance.Money(1_000m, Core.BusinessGovernance.CurrencyCode.Gbp));
        Assert.True(lineAdded.Succeeded);

        var domain = QuotationTestHost.Domain(_host);
        var provider = new QuotationPropertyFacetProvider(Quotation.CanonicalKind, domain);

        var facets = await provider.GetFacetsAsync(created.Quotation.Id);

        var referenceFacet = Assert.Single(facets, f => f.Name == "Reference");
        Assert.Equal("Q-WS-2", referenceFacet.Value);

        var totalFacet = Assert.Single(facets, f => f.Name == "Total");
        Assert.Contains("1000", totalFacet.Value, StringComparison.Ordinal);

        var statusFacet = Assert.Single(facets, f => f.Name == "Status");
        Assert.Equal("Draft", statusFacet.Value);
    }
}
