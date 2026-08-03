using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Modules;
using Tempest.Core.Verification;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates the
/// Verification Framework: it creates a sample subject document, records
/// a verification against it (with explicit criteria and evidence)
/// during its own initialisation, and registers a command demonstrating
/// the permission-gated history-read path — denied by default, mirroring
/// <see cref="AuditSampleModule"/>'s own identical demonstration for
/// <see cref="Tempest.Core.Audit.IAuditQuery"/>.
/// </summary>
/// <remarks>
/// The living reference module `WP 7.1E` validates the Verification
/// Framework against — mirrors <see cref="CalculationSampleModule"/>'s
/// own role for the Calculation Framework. Carries
/// <see cref="ModuleMetadataAttribute"/> so Discovery can read its
/// identity without instantiating it (ADR-0027).
/// </remarks>
[ModuleMetadata("tempest.samples.verification", "Verification Sample", "1.0.0")]
public sealed class VerificationSampleModule : ModuleLifecycleBase
{
    /// <summary>The <c>Kind</c> this module creates its own sample subject document under.</summary>
    public const string SampleSubjectDocumentKind = "SampleRequirement";

    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="GetSampleVerificationHistoryCommand"/>.
    /// </summary>
    public const string GetSampleVerificationHistoryCommandId = "sample.verification-history";

    private readonly IEngineeringDocumentStore _documentStore;
    private readonly IVerificationService _verificationService;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>
    /// Initialises a new instance of the <see cref="VerificationSampleModule"/> class.
    /// </summary>
    /// <param name="documentStore">The Engineering Data Model service this module creates its own sample subject document through.</param>
    /// <param name="verificationService">The Verification Framework service this module records and queries through.</param>
    /// <param name="commandDispatcher">The Command Framework's dispatch-side surface this module registers its handlers through.</param>
    /// <param name="commandRegistry">The Command Framework's discovery-side surface this module registers its descriptors through.</param>
    public VerificationSampleModule(
        IEngineeringDocumentStore documentStore,
        IVerificationService verificationService,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.verification", "Verification Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(verificationService);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _documentStore = documentStore;
        _verificationService = verificationService;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    /// <summary>Gets the Id of the sample subject document created during <see cref="InitialiseAsync"/>, once initialisation has completed.</summary>
    public Guid? SampleSubjectDocumentId { get; private set; }

    /// <summary>Gets the Id of the verification record produced during <see cref="InitialiseAsync"/>, once initialisation has completed.</summary>
    public Guid? SampleVerificationRecordId { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="InitialiseAsync"/> has registered this module's commands.</summary>
    public bool HasRegistered { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Creates a fictional sample subject document, then records a
    /// <see cref="VerificationOutcome.Pass"/> verification against it with
    /// one explicit criterion and one piece of evidence — proving create/
    /// record/link all work end to end against the real service — then
    /// registers <see cref="GetSampleVerificationHistoryCommand"/>'s
    /// handler and descriptor.
    /// </remarks>
    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        var subjectDocument = await _documentStore.CreateAsync(
            SampleSubjectDocumentKind, "Fictional sample requirement — for demonstration only.", cancellationToken)
            .ConfigureAwait(false);
        SampleSubjectDocumentId = subjectDocument.Id;

        var context = new VerificationContext();
        context.RecordCriterion("Sample dimension does not exceed the fictional allowable.", isSatisfied: true, detail: "Fictional test value.");
        context.RecordEvidence("Fictional sample inspection note — not a real engineering record.");

        var record = await _verificationService.RecordAsync(
            subjectDocument.Id, VerificationOutcome.Pass, "inspection", context, cancellationToken)
            .ConfigureAwait(false);
        SampleVerificationRecordId = record.Id;

        _commandDispatcher.RegisterHandler<GetSampleVerificationHistoryCommand>(
            new GetSampleVerificationHistoryCommandHandler(_verificationService, this));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: GetSampleVerificationHistoryCommandId,
            displayName: "Get Sample Verification History",
            category: "Sample",
            description: "Retrieves the verification history for this module's own sample subject document.",
            createDefault: () => new GetSampleVerificationHistoryCommand()));

        HasRegistered = true;
    }
}
