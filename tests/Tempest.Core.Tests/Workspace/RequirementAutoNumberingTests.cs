using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Tests.Persistence;
using Tempest.Core.Verification;
using Tempest.Workspace.Requirements;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `v0.25.3` independent test E2: a requirement created with no identifier
/// takes the next one in its project's own sequence, so adding one is only
/// a matter of its detail.
/// </summary>
public class RequirementAutoNumberingTests
{
    private static readonly Guid TestyProject = Guid.NewGuid();
    private static readonly Guid OldStyleProject = Guid.NewGuid();

    private static (IRequirementsService Requirements, ICommandDispatcher Dispatcher) Build()
    {
        var store = new InMemoryQueryablePersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        principalAccessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("test-user", "test-user"), []));
        var documentStore = new EngineeringDocumentStore(store, principalAccessor);
        var verificationService = new VerificationService(
            documentStore, principalAccessor, new PermissionEvaluator(), store, new InMemoryEngineeringRelationshipRepository());
        var requirements = new RequirementsService(documentStore, store, principalAccessor, verificationService);

        var dispatcher = new CommandDispatcher(new CommandHandlerTable());
        dispatcher.RegisterHandler<CreateRequirementCommand>(new CreateRequirementCommandHandler(
            requirements, dispatcher,
            (id, _) => Task.FromResult<string?>(id == TestyProject ? "TESTY-TRIALX" : id == OldStyleProject ? "P-0001" : null)));
        return (requirements, dispatcher);
    }

    private static async Task<string> CreateAsync(ICommandDispatcher dispatcher, IRequirementsService requirements, string? identifier, Guid? project)
    {
        var result = await dispatcher.DispatchAsync(new CreateRequirementCommand(identifier, "The bracket shall carry 5 kN.", projectId: project), CancellationToken.None);
        Assert.True(result.Succeeded, result.Message);
        return (await requirements.FindAsync(result.SubjectId!.Value))!.Identifier;
    }

    [Fact]
    public async Task ABlankIdentifier_TakesTheNextInTheProjectsSequence()
    {
        var (requirements, dispatcher) = Build();

        Assert.Equal("TESTY-TRIALX-REQ-001", await CreateAsync(dispatcher, requirements, null, TestyProject));
        Assert.Equal("TESTY-TRIALX-REQ-002", await CreateAsync(dispatcher, requirements, "  ", TestyProject));

        // A typed identifier still stands, and the sequence carries on past the highest used.
        Assert.Equal("TESTY-TRIALX-REQ-010", await CreateAsync(dispatcher, requirements, "TESTY-TRIALX-REQ-010", TestyProject));
        Assert.Equal("TESTY-TRIALX-REQ-011", await CreateAsync(dispatcher, requirements, null, TestyProject));
    }

    [Fact]
    public async Task WithNoCustomerCodedProject_TheSequenceIsPlainReq()
    {
        var (requirements, dispatcher) = Build();

        Assert.Equal("REQ-001", await CreateAsync(dispatcher, requirements, null, null));
        Assert.Equal("REQ-002", await CreateAsync(dispatcher, requirements, null, OldStyleProject));
    }
}
