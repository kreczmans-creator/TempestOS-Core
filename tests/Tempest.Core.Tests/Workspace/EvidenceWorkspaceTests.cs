using Tempest.Workspace;
using Tempest.Workspace.Composition;
using Tempest.Workspace.Evidence;
using Tempest.Workspace.Mechanical;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `WP 18.0A` acceptance #3: <c>evidence.create</c> lands under the open
/// project whether nothing or a non-container object is selected and
/// returns the subject id and Kind; the node provider lists the result;
/// the facet provider names the subject, not a bare <see cref="Guid"/>.
/// </summary>
public sealed class EvidenceWorkspaceTests : IAsyncLifetime
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
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, _temp.Path),
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

    private static CommandParameterPrompt Answering(string title, EvidenceClassification classification) =>
        (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string>
        {
            ["title"] = title,
            ["classification"] = classification.ToString(),
        });

    private async Task<Guid> CreateProjectAsync()
    {
        var factory = new EngineeringObjectFactory<Project>(
            MechanicalObjectFactoryRegistry.Project, _domain,
            (doc, rev) => new Project(doc, rev, _domain, "EVD-WS-PRJ", "Evidence Workspace Test Project", EngineeringObjectMetadata.Empty));
        var project = await factory.CreateAsync("Evidence workspace test project.");
        return project.Id;
    }

    [Fact]
    public async Task Create_WithNothingSelected_LandsUnderTheOpenProject_AndReturnsSubjectIdAndKind()
    {
        var projectId = await CreateProjectAsync();
        var context = new CommandContext([], projectId);

        var invocation = await _registry.InvokeAsync("evidence.create", context, Answering("Standalone calc", EvidenceClassification.Calculation));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.True(invocation.Result!.Succeeded);
        Assert.NotNull(invocation.Result.SubjectId);
        Assert.Equal(Core.Evidence.Evidence.CanonicalKind, invocation.Result.SubjectKind);

        var created = await _domain.Repository.FindAsync(invocation.Result.SubjectId!.Value) as Core.Evidence.Evidence;
        Assert.NotNull(created);
        Assert.Equal(projectId, created!.ParentId);
    }

    [Fact]
    public async Task Create_WithANonContainerSelected_StillLandsUnderTheOpenProject()
    {
        var projectId = await CreateProjectAsync();

        var partFactory = new EngineeringObjectFactory<Part>(
            MechanicalObjectFactoryRegistry.Part, _domain,
            (doc, rev) => new Part(doc, rev, _domain, "EVD-WS-PART", "A Part", EngineeringObjectMetadata.Empty));
        var part = await partFactory.CreateAsync("A part, not a container for evidence.");

        var context = new CommandContext([new CommandContextObject(part.Id, MechanicalObjectFactoryRegistry.Part)], projectId);

        var invocation = await _registry.InvokeAsync("evidence.create", context, Answering("Evidence with a Part selected", EvidenceClassification.Report));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.True(invocation.Result!.Succeeded);

        var created = await _domain.Repository.FindAsync(invocation.Result.SubjectId!.Value) as Core.Evidence.Evidence;
        Assert.NotNull(created);
        Assert.Equal(projectId, created!.ParentId);
    }

    [Fact]
    public async Task TheNodeProvider_ListsANewlyCreatedRecord_UnderItsOwnProjectAndClassification()
    {
        var projectId = await CreateProjectAsync();
        var service = (IEvidenceService)_host.Services!.GetService(typeof(IEvidenceService));
        var evidence = await service.CreateAsync(projectId, "Listed evidence", EvidenceClassification.Drawing);

        var provider = new EvidenceNodeProvider(EvidenceWorkspaceRegistration.ExplorerAreaId, _domain);

        var roots = await provider.GetRootNodesAsync();
        var projectNode = Assert.Single(roots, n => n.Id == projectId);
        Assert.True(projectNode.HasChildren);

        var groups = await provider.GetChildrenAsync(projectId);
        var drawingGroup = Assert.Single(groups, g => g.Title == nameof(EvidenceClassification.Drawing));
        Assert.True(drawingGroup.HasChildren);

        var members = await provider.GetChildrenAsync(drawingGroup.Id);
        var memberNode = Assert.Single(members, m => m.Id == evidence.Id);
        Assert.Contains("Listed evidence", memberNode.Title, StringComparison.Ordinal);
        Assert.Contains(EvidenceStatus.Draft.ToString(), memberNode.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFacetProvider_NamesTheSubject_NotABareGuid()
    {
        var projectId = await CreateProjectAsync();

        var partFactory = new EngineeringObjectFactory<Part>(
            MechanicalObjectFactoryRegistry.Part, _domain,
            (doc, rev) => new Part(doc, rev, _domain, "EVD-WS-SUBJECT", "The Subject Part", EngineeringObjectMetadata.Empty));
        var subjectPart = await partFactory.CreateAsync("The part this evidence is about.");

        var service = (IEvidenceService)_host.Services!.GetService(typeof(IEvidenceService));
        var evidence = await service.CreateAsync(projectId, "Evidence about a Part", EvidenceClassification.Calculation, subjectPart.Id);

        var provider = new EvidencePropertyFacetProvider(Core.Evidence.Evidence.CanonicalKind, _domain);
        var facets = await provider.GetFacetsAsync(evidence.Id);

        var subjectFacet = Assert.Single(facets, f => f.Name == "Subject");
        Assert.DoesNotContain(subjectPart.Id.ToString(), subjectFacet.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The Subject Part", subjectFacet.Value, StringComparison.Ordinal);
        Assert.Equal(PropertyFacetKind.ObjectReference, subjectFacet.FacetKind);
    }
}
