using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Identity;
using Tempest.Core.Modules;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates the Audit
/// Framework: it establishes its own local principal, records an action
/// during its own initialisation, and registers two commands (record/
/// query) demonstrating both the recording path and the permission-gated
/// query path — denied by default, granted with configuration.
/// </summary>
/// <remarks>
/// <para>
/// The living reference module <c>WP 6.5</c> validates the Audit
/// Framework against — mirrors <see cref="IdentitySampleModule"/>'s own
/// role for Identity &amp; Permissions and
/// <see cref="SettingsSampleModule"/>'s own role for Settings. Carries
/// <see cref="ModuleMetadataAttribute"/> so Discovery can read its
/// identity without instantiating it (ADR-0027), freeing its constructor
/// to request <see cref="IIdentityService"/>, <see cref="IAuditRecorder"/>,
/// <see cref="IAuditQuery"/>, <see cref="ICommandDispatcher"/>, and
/// <see cref="ICommandRegistry"/> — all DI-public platform services — via
/// ordinary constructor injection.
/// </para>
/// <para>
/// Deliberately establishes its own principal
/// (<see cref="SampleIdentityId"/>), rather than depending on
/// <see cref="IdentitySampleModule"/> having already run — every sample
/// module remains independently usable, exactly as
/// <see cref="SettingsSampleModule"/> does not depend on either of the
/// other two.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.audit", "Audit Sample", "1.0.0")]
public sealed class AuditSampleModule : ModuleLifecycleBase
{
    /// <summary>
    /// The identity id this module establishes as current during its own
    /// initialisation.
    /// </summary>
    public const string SampleIdentityId = "sample.audit-user";

    /// <summary>
    /// The action recorded automatically during this module's own
    /// <see cref="InitialiseAsync"/>.
    /// </summary>
    public const string InitialisedActionName = "sample.module.initialised";

    /// <summary>
    /// The action <see cref="RecordSampleAuditActionCommandHandler"/>
    /// records.
    /// </summary>
    public const string ManualActionName = "sample.audit.manual-action";

    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="RecordSampleAuditActionCommand"/>.
    /// </summary>
    public const string RecordSampleAuditActionCommandId = "sample.audit-record";

    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="QuerySampleAuditRecordsCommand"/>.
    /// </summary>
    public const string QuerySampleAuditRecordsCommandId = "sample.audit-query";

    private readonly IIdentityService _identityService;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IAuditQuery _auditQuery;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>
    /// Initialises a new instance of the <see cref="AuditSampleModule"/> class.
    /// </summary>
    /// <param name="identityService">
    /// The Identity &amp; Permissions service this module establishes a
    /// principal through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="auditRecorder">
    /// The Audit service this module records actions through, resolved
    /// via ordinary constructor injection.
    /// </param>
    /// <param name="auditQuery">
    /// The Audit service this module's registered command queries
    /// through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="commandDispatcher">
    /// The Command Framework's dispatch-side surface this module registers
    /// its handlers through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="commandRegistry">
    /// The Command Framework's discovery-side surface this module
    /// registers its descriptors through, resolved via ordinary constructor
    /// injection.
    /// </param>
    public AuditSampleModule(
        IIdentityService identityService,
        IAuditRecorder auditRecorder,
        IAuditQuery auditQuery,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.audit", "Audit Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(auditRecorder);
        ArgumentNullException.ThrowIfNull(auditQuery);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _identityService = identityService;
        _auditRecorder = auditRecorder;
        _auditQuery = auditQuery;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="InitialiseAsync"/> has
    /// registered this module's commands.
    /// </summary>
    public bool HasRegistered { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Establishes <see cref="SampleIdentityId"/> as the current
    /// principal, records <see cref="InitialisedActionName"/>, then
    /// registers <see cref="RecordSampleAuditActionCommand"/> and
    /// <see cref="QuerySampleAuditRecordsCommand"/>'s handlers and
    /// descriptors.
    /// </remarks>
    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _identityService.EstablishCurrentPrincipal(SampleIdentityId);

        await _auditRecorder.RecordAsync(InitialisedActionName, cancellationToken: cancellationToken).ConfigureAwait(false);

        _commandDispatcher.RegisterHandler<RecordSampleAuditActionCommand>(
            new RecordSampleAuditActionCommandHandler(_auditRecorder));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: RecordSampleAuditActionCommandId,
            displayName: "Record Sample Audit Action",
            category: "Sample",
            description: "Records a manual audit action for the sample principal.",
            createDefault: () => new RecordSampleAuditActionCommand()));

        _commandDispatcher.RegisterHandler<QuerySampleAuditRecordsCommand>(
            new QuerySampleAuditRecordsCommandHandler(_auditQuery));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: QuerySampleAuditRecordsCommandId,
            displayName: "Query Sample Audit Records",
            category: "Sample",
            description: "Queries every audit record for the sample principal.",
            createDefault: () => new QuerySampleAuditRecordsCommand()));

        HasRegistered = true;
    }
}
