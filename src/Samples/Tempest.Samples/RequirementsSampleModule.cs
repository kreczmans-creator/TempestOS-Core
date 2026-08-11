using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.ExportImport;
using Tempest.Core.Identity;
using Tempest.Core.Modules;
using Tempest.Core.Reporting;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates the
/// Requirements Engine — the first implementation of the Systems
/// Engineering Foundation. During initialisation it creates a fictional
/// sample requirement, revises it, walks it through three lifecycle
/// transitions, creates a group and a collection, allocates it to a
/// fictional (non-Requirements) engineering document, and records a
/// verification against it directly through <see cref="IVerificationService"/>
/// — proving no duplicate verification mechanism exists in this
/// framework. Registers two commands demonstrating Identity, Audit, and
/// Reporting integration, and an Export/Import adapter demonstrating that
/// integration too.
/// </summary>
/// <remarks>
/// The living reference module `WP 7.3A` validates the Requirements
/// Engine against — mirrors <see cref="VerificationSampleModule"/>'s own
/// role for the Verification Framework. Carries
/// <see cref="ModuleMetadataAttribute"/> so Discovery can read its
/// identity without instantiating it (ADR-0027). Establishes its own
/// principal (<see cref="SampleIdentityId"/>), rather than depending on
/// <see cref="IdentitySampleModule"/> having already run, mirroring
/// <see cref="ExportImportSampleModule"/>'s own identical precedent —
/// every sample module remains independently usable.
/// </remarks>
[ModuleMetadata("tempest.samples.requirements", "Requirements Sample", "1.0.0")]
public sealed class RequirementsSampleModule : ModuleLifecycleBase
{
    /// <summary>The identity id this module establishes as current during its own initialisation.</summary>
    public const string SampleIdentityId = "sample.requirements-user";

    /// <summary>The <c>Kind</c> this module creates its own fictional allocation-target document under.</summary>
    public const string SampleAllocationTargetDocumentKind = "SampleComponent";

    /// <summary>The permission key <see cref="GetSampleRequirementEvidenceCommandHandler"/> checks for.</summary>
    public const string ReadPermissionKey = "requirements.read";

    /// <summary>The <see cref="CommandDescriptor.Id"/> this module registers for <see cref="GetSampleRequirementEvidenceCommand"/>.</summary>
    public const string GetSampleRequirementEvidenceCommandId = "sample.requirements-evidence";

    /// <summary>The <see cref="CommandDescriptor.Id"/> this module registers for <see cref="GenerateSampleRequirementReportCommand"/>.</summary>
    public const string GenerateSampleRequirementReportCommandId = "sample.requirements-report";

    /// <summary>The action recorded through <see cref="IAuditRecorder"/> when the sample requirement is created.</summary>
    public const string CreatedActionName = "requirements.sampleCreated";

    /// <summary>The artifact section kind <see cref="RequirementExportAdapter"/> is registered under.</summary>
    public const string ExportAdapterKind = "tempest.samples.requirements.sample";

    private readonly IIdentityService _identityService;
    private readonly IRequirementsService _requirementsService;
    private readonly IEngineeringDocumentStore _documentStore;
    private readonly IVerificationService _verificationService;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IReportingService _reportingService;
    private readonly ImportService _importService;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>Initialises a new instance of the <see cref="RequirementsSampleModule"/> class.</summary>
    public RequirementsSampleModule(
        IIdentityService identityService,
        IRequirementsService requirementsService,
        IEngineeringDocumentStore documentStore,
        IVerificationService verificationService,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IPermissionEvaluator permissionEvaluator,
        IAuditRecorder auditRecorder,
        IReportingService reportingService,
        ImportService importService,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.requirements", "Requirements Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(verificationService);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(auditRecorder);
        ArgumentNullException.ThrowIfNull(reportingService);
        ArgumentNullException.ThrowIfNull(importService);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _identityService = identityService;
        _requirementsService = requirementsService;
        _documentStore = documentStore;
        _verificationService = verificationService;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _permissionEvaluator = permissionEvaluator;
        _auditRecorder = auditRecorder;
        _reportingService = reportingService;
        _importService = importService;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    /// <summary>Gets the sample requirement's own Id, once <see cref="InitialiseAsync"/> has run.</summary>
    public Guid? SampleRequirementId { get; private set; }

    /// <summary>Gets the sample requirement group's own Id, once <see cref="InitialiseAsync"/> has run.</summary>
    public Guid? SampleGroupId { get; private set; }

    /// <summary>Gets the sample requirement collection's own Id, once <see cref="InitialiseAsync"/> has run.</summary>
    public Guid? SampleCollectionId { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="InitialiseAsync"/> has registered this module's commands.</summary>
    public bool HasRegistered { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Creates a fictional sample requirement, revises it, walks
    /// <c>Draft → Reviewed → Approved → Allocated</c>, creates a group and
    /// a collection, allocates it to a fictional non-Requirements
    /// document (proving allocation targets are discipline-neutral), and
    /// records a verification directly through
    /// <see cref="IVerificationService"/> — proving create/revise/status/
    /// relate/verify all work end to end, and that no duplicate
    /// verification mechanism exists in this framework.
    /// </para>
    /// <para>
    /// <b>Idempotent restart (`WP 10.1B`, `TD-37`):</b> <see cref="IRequirementsService"/>
    /// keeps its own durable <c>Identifier</c> index (`ADR-0058`), shared
    /// across every process launched from the same working directory
    /// (`ADR-0041`) — so a second real launch would otherwise find
    /// <c>"SAMPLE-REQ-001"</c> already created and fail loudly. This module
    /// now checks first: if already created, it reuses the existing
    /// requirement for <see cref="SampleRequirementId"/> rather than
    /// repeating the full create/revise/status/group/collection/allocate/
    /// verify sequence (each of which assumes a freshly-created requirement
    /// and would itself fail or duplicate against an already-progressed
    /// one) — <see cref="SampleGroupId"/>/<see cref="SampleCollectionId"/>
    /// are only populated on the run that actually creates them, honestly
    /// left <see langword="null"/> on a later, idempotent-skip run, since
    /// no lookup-by-name capability exists to recover them. Commands,
    /// report definition, and the export adapter are always
    /// (re-)registered — all three are in-memory only and never survive a
    /// restart on their own.
    /// </para>
    /// </remarks>
    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _identityService.EstablishCurrentPrincipal(SampleIdentityId);

        var existing = await _requirementsService.FindByIdentifierAsync("SAMPLE-REQ-001", cancellationToken).ConfigureAwait(false);
        IRequirement requirement;

        if (existing is not null)
        {
            // Already durably created by an earlier launch against this
            // same persistence store (TD-37) - reuse it rather than
            // repeating a sequence that assumes a freshly-created
            // requirement.
            requirement = existing;
            SampleRequirementId = requirement.Id;
        }
        else
        {
            requirement = await _requirementsService.CreateAsync(
                "SAMPLE-REQ-001",
                "Fictional sample requirement — for demonstration only.",
                category: "functional",
                cancellationToken)
                .ConfigureAwait(false);
            SampleRequirementId = requirement.Id;

            await _auditRecorder.RecordAsync(CreatedActionName, new Dictionary<string, string> { ["Identifier"] = requirement.Identifier }, cancellationToken)
                .ConfigureAwait(false);

            await _requirementsService.ReviseAsync(
                requirement.Id, "Fictional sample requirement, revised — for demonstration only.", "Sample revision.", cancellationToken)
                .ConfigureAwait(false);

            await _requirementsService.SetStatusAsync(requirement.Id, RequirementStatus.Reviewed, cancellationToken).ConfigureAwait(false);
            await _requirementsService.SetStatusAsync(requirement.Id, RequirementStatus.Approved, cancellationToken).ConfigureAwait(false);

            var group = await _requirementsService.CreateGroupAsync("Sample Requirements Group", cancellationToken: cancellationToken).ConfigureAwait(false);
            SampleGroupId = group.Id;
            await _requirementsService.LinkAsync(requirement.Id, group.Id, RequirementRelationshipKinds.GroupedUnder, cancellationToken).ConfigureAwait(false);

            var collection = await _requirementsService.CreateCollectionAsync("Sample Requirements Baseline", cancellationToken).ConfigureAwait(false);
            SampleCollectionId = collection.Id;
            await _requirementsService.AddToCollectionAsync(collection.Id, requirement.Id, cancellationToken).ConfigureAwait(false);

            var allocationTarget = await _documentStore.CreateAsync(
                SampleAllocationTargetDocumentKind, "Fictional sample component — for demonstration only.", cancellationToken)
                .ConfigureAwait(false);
            await _requirementsService.LinkAsync(requirement.Id, allocationTarget.Id, RequirementRelationshipKinds.AllocatedTo, cancellationToken)
                .ConfigureAwait(false);
            await _requirementsService.SetStatusAsync(requirement.Id, RequirementStatus.Allocated, cancellationToken).ConfigureAwait(false);

            var verificationContext = new VerificationContext();
            verificationContext.RecordCriterion("Sample requirement is demonstrated by fictional inspection.", isSatisfied: true);
            verificationContext.RecordEvidence("Fictional sample inspection note — not a real engineering record.");
            await _verificationService.RecordAsync(requirement.Id, VerificationOutcome.Pass, "inspection", verificationContext, cancellationToken)
                .ConfigureAwait(false);
        }

        _importService.RegisterImportable(new RequirementExportAdapter(_requirementsService, ExportAdapterKind, requirement.Id));

        _reportingService.RegisterDefinition(
            new SampleRequirementReportDefinition(),
            new SampleRequirementReportRenderer(_requirementsService));

        _commandDispatcher.RegisterHandler<GetSampleRequirementEvidenceCommand>(
            new GetSampleRequirementEvidenceCommandHandler(_currentPrincipalAccessor, _permissionEvaluator, _requirementsService, this));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: GetSampleRequirementEvidenceCommandId,
            displayName: "Get Sample Requirement Evidence",
            category: "Sample",
            description: "Retrieves the aggregated evidence for this module's own sample requirement, demonstrating Identity integration (permission-gated, denied by default).",
            createDefault: () => new GetSampleRequirementEvidenceCommand()));

        _commandDispatcher.RegisterHandler<GenerateSampleRequirementReportCommand>(
            new GenerateSampleRequirementReportCommandHandler(_reportingService, this));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: GenerateSampleRequirementReportCommandId,
            displayName: "Generate Sample Requirement Report",
            category: "Sample",
            description: "Generates a summary report for this module's own sample requirement, demonstrating Reporting integration.",
            createDefault: () => new GenerateSampleRequirementReportCommand()));

        HasRegistered = true;
    }
}
