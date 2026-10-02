using System.Text.Json;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Tests.EngineeringDomain;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Persistence;
using Tempest.Core.Verification;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Integration.DashboardExport;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Verification;

namespace Tempest.Core.Tests.Workspace.DashboardExport;

/// <summary>
/// Proves <see cref="ReviewDecisionIntakeHostedService"/> (`ADR-0162`)
/// directly against <see cref="ReviewDecisionIntakeHostedService.IntakeOnceAsync"/> —
/// lighter than a full <see cref="Tempest.Core.Runtime.ITempestHost"/>, and
/// a fair substitute for one here: this service's own constructor takes
/// only <see cref="EngineeringDomainContext"/>, <see cref="ICommandDispatcher"/>
/// and <see cref="IConfigurationProvider"/>, exactly the three collaborators
/// this fixture builds by hand, mirroring <c>RequirementsCompensationTests</c>'s
/// own "build the real pipeline, skip the host" convention. Discovery
/// itself (the hosted service found with no manual DI registration) is
/// <c>DashboardExportHostedServiceTests</c>'s own already-proven concern —
/// this service's own discoverability needs no second proof, since it is
/// registered identically (a concrete, non-abstract <see cref="Tempest.Core.BackgroundServices.IHostedService"/>).
/// </summary>
public class ReviewDecisionIntakeHostedServiceTests
{
    private sealed record Pipeline(
        EngineeringDomainContext Domain,
        ICommandDispatcher Dispatcher,
        DocumentObjectFactoryRegistry Documents,
        CalculationObjectFactoryRegistry Calculations,
        VerificationActivityFactoryRegistry Verification,
        IRequirementsService Requirements);

    private static Pipeline BuildPipeline()
    {
        var domain = TestEngineeringDomain.NewContext();
        var documents = new DocumentObjectFactoryRegistry(domain);
        var calculations = new CalculationObjectFactoryRegistry(domain);
        var verification = new VerificationActivityFactoryRegistry(domain);

        var reqStore = new InMemoryQueryablePersistenceStore();
        var principalAccessor = new CurrentPrincipalAccessor();
        principalAccessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("test-user", "test-user"), []));
        var reqDocumentStore = new EngineeringDocumentStore(reqStore, principalAccessor);
        var reqVerificationService = new VerificationService(
            reqDocumentStore, principalAccessor, new PermissionEvaluator(), reqStore, new InMemoryEngineeringRelationshipRepository());
        var requirements = new RequirementsService(reqDocumentStore, reqStore, principalAccessor, reqVerificationService);

        var dispatcher = new CommandDispatcher(new CommandHandlerTable());
        dispatcher.RegisterHandler(new SetDocumentStatusCommandHandler(domain));
        dispatcher.RegisterHandler(new SetCalculationStatusCommandHandler(domain));
        dispatcher.RegisterHandler(new SetVerificationActivityStatusCommandHandler(domain));
        dispatcher.RegisterHandler(new SetRequirementStatusCommandHandler(requirements));

        return new Pipeline(domain, dispatcher, documents, calculations, verification, requirements);
    }

    private static ReviewDecisionIntakeHostedService BuildService(Pipeline pipeline, string intakeDirectory, bool enabled = true) =>
        new(
            pipeline.Domain,
            pipeline.Dispatcher,
            new ConfigurationBuilder().AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(ReviewDecisionIntakeOptions.EnabledConfigurationKey, enabled.ToString()),
                new KeyValuePair<string, string>(ReviewDecisionIntakeOptions.IntakeDirectoryConfigurationKey, intakeDirectory),
            ])).Build());

    private static async Task WriteIntentAsync(string directory, Guid reviewItemId, string kind, string decision, Guid? intentId = null)
    {
        Directory.CreateDirectory(directory);
        var intent = new ReviewDecisionIntent(
            ReviewDecisionIntent.CurrentSchemaVersion, intentId ?? Guid.NewGuid(), reviewItemId, kind, decision, "A. Reviewer", DateTimeOffset.UtcNow);

        await File.WriteAllTextAsync(Path.Combine(directory, $"{intent.IntentId}.json"), JsonSerializer.Serialize(intent));
    }

    private static ReviewDecisionOutcome ReadOutcome(string directory, Guid intentId)
    {
        var path = Directory.EnumerateFiles(directory, $"{intentId}*.result.json").Single();
        return JsonSerializer.Deserialize<ReviewDecisionOutcome>(File.ReadAllText(path))!;
    }

    [Fact]
    public async Task IntakeOnceAsync_Approve_Document_TransitionsToApproved_AndDeletesTheIntent()
    {
        var pipeline = BuildPipeline();
        using var temp = new TempDirectory();
        var document = await pipeline.Documents.CreateAsync(DocumentObjectFactoryRegistry.Drawing, "DWG-1", "GA Drawing", "content", parentId: null);
        await ((IHasLifecycle)document).TransitionAsync(LifecycleState.InReview);

        var intentId = Guid.NewGuid();
        await WriteIntentAsync(temp.Path, document.Id, "Drawing", ReviewDecisionIntent.DecisionApprove, intentId);
        var intentPath = Path.Combine(temp.Path, $"{intentId}.json");

        var service = BuildService(pipeline, temp.Path);
        await service.IntakeOnceAsync();

        var reloaded = await pipeline.Domain.Repository.FindAsync(document.Id);
        Assert.Equal(LifecycleState.Approved, ((IHasLifecycle)reloaded!).Status);
        Assert.False(File.Exists(intentPath));

        var outcome = ReadOutcome(temp.Path, intentId);
        Assert.True(outcome.Succeeded);
    }

    [Fact]
    public async Task IntakeOnceAsync_Reject_Calculation_ReturnsToDraft()
    {
        var pipeline = BuildPipeline();
        using var temp = new TempDirectory();
        var calculation = await pipeline.Calculations.CreateAsync(CalculationObjectFactoryRegistry.CalculationKind, "CALC-1", "Beam check", "content", parentId: null);
        await ((IHasLifecycle)calculation).TransitionAsync(LifecycleState.InReview);

        await WriteIntentAsync(temp.Path, calculation.Id, CalculationObjectFactoryRegistry.CalculationKind, ReviewDecisionIntent.DecisionReject);

        var service = BuildService(pipeline, temp.Path);
        await service.IntakeOnceAsync();

        var reloaded = await pipeline.Domain.Repository.FindAsync(calculation.Id);
        Assert.Equal(LifecycleState.Draft, ((IHasLifecycle)reloaded!).Status);
    }

    [Fact]
    public async Task IntakeOnceAsync_Approve_VerificationActivity_TransitionsToApproved()
    {
        var pipeline = BuildPipeline();
        using var temp = new TempDirectory();
        var drawing = await pipeline.Documents.CreateAsync(DocumentObjectFactoryRegistry.Drawing, "DWG-2", "GA Drawing", "content", parentId: null);
        var activity = await pipeline.Verification.CreateAsync("Load test", "content", drawing.Id, "Test", parentId: null);
        await ((IHasLifecycle)activity).TransitionAsync(LifecycleState.InReview);

        await WriteIntentAsync(temp.Path, activity.Id, VerificationActivityFactoryRegistry.SupportedKind, ReviewDecisionIntent.DecisionApprove);

        var service = BuildService(pipeline, temp.Path);
        await service.IntakeOnceAsync();

        var reloaded = await pipeline.Domain.Repository.FindAsync(activity.Id);
        Assert.Equal(LifecycleState.Approved, ((IHasLifecycle)reloaded!).Status);
    }

    [Fact]
    public async Task IntakeOnceAsync_Approve_Requirement_TransitionsToApproved()
    {
        var pipeline = BuildPipeline();
        using var temp = new TempDirectory();
        var requirement = await pipeline.Requirements.CreateAsync("The bracket shall carry 5kN.", "Structural", null, default);
        await pipeline.Requirements.SetStatusAsync(requirement.Id, RequirementStatus.Reviewed, default);

        await WriteIntentAsync(temp.Path, requirement.Id, RequirementsService.RequirementDocumentKind, ReviewDecisionIntent.DecisionApprove);

        var service = BuildService(pipeline, temp.Path);
        await service.IntakeOnceAsync();

        var reloaded = await pipeline.Requirements.FindAsync(requirement.Id, default);
        Assert.Equal(RequirementStatus.Approved, reloaded!.Status);
    }

    [Fact]
    public async Task IntakeOnceAsync_Evidence_IsRefused_WithAClearReason_NotSilentlyIgnored()
    {
        var pipeline = BuildPipeline();
        using var temp = new TempDirectory();
        var intentId = Guid.NewGuid();
        await WriteIntentAsync(temp.Path, Guid.NewGuid(), "Evidence", ReviewDecisionIntent.DecisionApprove, intentId);

        var service = BuildService(pipeline, temp.Path);
        await service.IntakeOnceAsync();

        var outcome = ReadOutcome(temp.Path, intentId);
        Assert.False(outcome.Succeeded);
        Assert.Contains("Evidence", outcome.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(temp.Path, $"{intentId}.json")), "A refused intent is still consumed, not left to be retried forever.");
    }

    [Fact]
    public async Task IntakeOnceAsync_UnknownDecisionWord_IsRefused_NeitherApprovedNorRejected()
    {
        var pipeline = BuildPipeline();
        using var temp = new TempDirectory();
        var document = await pipeline.Documents.CreateAsync(DocumentObjectFactoryRegistry.Document, "DOC-1", "Spec", "content", parentId: null);
        await ((IHasLifecycle)document).TransitionAsync(LifecycleState.InReview);
        var intentId = Guid.NewGuid();

        await WriteIntentAsync(temp.Path, document.Id, "Document", "maybe", intentId);

        var service = BuildService(pipeline, temp.Path);
        await service.IntakeOnceAsync();

        var reloaded = await pipeline.Domain.Repository.FindAsync(document.Id);
        Assert.Equal(LifecycleState.InReview, ((IHasLifecycle)reloaded!).Status);
        Assert.False(ReadOutcome(temp.Path, intentId).Succeeded);
    }

    [Fact]
    public async Task IntakeOnceAsync_InvalidTransition_FailsWithTheLifecycleTable_OwnReason_NotACrash()
    {
        var pipeline = BuildPipeline();
        using var temp = new TempDirectory();
        // Never submitted for review - Draft -> Approved is not a permitted transition.
        var document = await pipeline.Documents.CreateAsync(DocumentObjectFactoryRegistry.Document, "DOC-2", "Spec", "content", parentId: null);
        var intentId = Guid.NewGuid();

        await WriteIntentAsync(temp.Path, document.Id, "Document", ReviewDecisionIntent.DecisionApprove, intentId);

        var service = BuildService(pipeline, temp.Path);
        await service.IntakeOnceAsync();

        var outcome = ReadOutcome(temp.Path, intentId);
        Assert.False(outcome.Succeeded);

        var reloaded = await pipeline.Domain.Repository.FindAsync(document.Id);
        Assert.Equal(LifecycleState.Draft, ((IHasLifecycle)reloaded!).Status);
    }

    [Fact]
    public async Task IntakeOnceAsync_SwitchedOff_NeverReadsTheDirectory_AndNeverCreatesIt()
    {
        var pipeline = BuildPipeline();
        using var temp = new TempDirectory();
        var intakeDirectory = Path.Combine(temp.Path, "not-created");

        var service = BuildService(pipeline, intakeDirectory, enabled: false);
        await service.StartAsync(default);

        Assert.Null(service.LastIntakeAttemptedAt);
        Assert.False(Directory.Exists(intakeDirectory));

        await service.StopAsync(default);
    }

    [Fact]
    public async Task IntakeOnceAsync_OneBadIntentFile_DoesNotBlockTheOthersInTheSameTick()
    {
        var pipeline = BuildPipeline();
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Path);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, $"{Guid.NewGuid()}.json"), "{ not valid json");

        var document = await pipeline.Documents.CreateAsync(DocumentObjectFactoryRegistry.Document, "DOC-3", "Spec", "content", parentId: null);
        await ((IHasLifecycle)document).TransitionAsync(LifecycleState.InReview);
        await WriteIntentAsync(temp.Path, document.Id, "Document", ReviewDecisionIntent.DecisionApprove);

        var service = BuildService(pipeline, temp.Path);
        await service.IntakeOnceAsync();

        var reloaded = await pipeline.Domain.Repository.FindAsync(document.Id);
        Assert.Equal(LifecycleState.Approved, ((IHasLifecycle)reloaded!).Status);
    }

    [Fact]
    public async Task IntakeOnceAsync_NoIntakeDirectory_ReportsZeroProcessed_NeverThrows()
    {
        var pipeline = BuildPipeline();
        using var temp = new TempDirectory();
        var intakeDirectory = Path.Combine(temp.Path, "never-created");

        var service = BuildService(pipeline, intakeDirectory);
        await service.IntakeOnceAsync();

        Assert.Equal(0, service.LastIntakeProcessedCount);
    }
}
