using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Identity;
using Tempest.Core.Modules;
using Tempest.Core.Notifications;
using Tempest.Core.Reporting;
using Tempest.Core.Settings;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates the
/// Reporting Framework: it registers
/// <see cref="SampleSummaryReportDefinition"/> and its own renderer
/// during initialisation, and registers a command
/// (<see cref="GenerateSampleReportCommand"/>) whose handler generates
/// that report, gated by a permission, then records the action through
/// Audit and publishes a completion notice through Notifications.
/// </summary>
/// <remarks>
/// <para>
/// The living reference module `WP 6.0` validates the Reporting
/// Framework against — mirrors <see cref="AuditSampleModule"/>'s own
/// role for Audit and <see cref="SettingsSampleModule"/>'s own role for
/// Settings. Carries <see cref="ModuleMetadataAttribute"/> so Discovery
/// can read its identity without instantiating it (ADR-0027), freeing
/// its constructor to request <see cref="Tempest.Core.Identity.CurrentPrincipalAccessor"/>,
/// <see cref="IReportingService"/>, <see cref="ISettingsProvider"/>,
/// <see cref="ICurrentPrincipalAccessor"/>,
/// <see cref="IPermissionEvaluator"/>, <see cref="IAuditRecorder"/>,
/// <see cref="INotificationDispatcher"/>, <see cref="ICommandDispatcher"/>,
/// and <see cref="ICommandRegistry"/> — all DI-public platform services —
/// via ordinary constructor injection.
/// </para>
/// <para>
/// Deliberately establishes its own principal
/// (<see cref="SampleIdentityId"/>), rather than depending on
/// <see cref="IdentitySampleModule"/> having already run — every sample
/// module remains independently usable, exactly as
/// <see cref="AuditSampleModule"/>'s own precedent. `WP 17.2A`
/// (ADR-0146): <see cref="GenerateReportPermissionKey"/> is part of every
/// session principal's fixed <see cref="ApplicationPermissions.LocalSession"/>
/// set, so <see cref="GenerateSampleReportCommandHandler"/> is granted
/// unconditionally — there is no longer a configuration-driven grant
/// mechanism to demonstrate a denied path against.
/// </para>
/// <para>
/// This module deliberately does not depend on
/// <see cref="Tempest.Core.Persistence.IPersistenceStore"/> — Reporting's
/// own approved contract states "Persistence Requirements: None,"
/// disclosed explicitly rather than wired in speculatively. See this
/// Work Package's own Platform Integration Demonstration for the full,
/// per-service account.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.reporting", "Reporting Sample", "1.0.0")]
public sealed class ReportingSampleModule : ModuleLifecycleBase
{
    /// <summary>
    /// The identity id this module establishes as current during its own
    /// initialisation.
    /// </summary>
    public const string SampleIdentityId = "sample.reporting-user";

    /// <summary>
    /// The permission key <see cref="GenerateSampleReportCommandHandler"/>
    /// checks for before generating a report. `WP 17.2A` (ADR-0146): reuses
    /// <c>Tempest.Core.Verification.VerificationService.ReadPermission</c>'s
    /// own key, part of every session principal's fixed
    /// <see cref="ApplicationPermissions.LocalSession"/> set — there is no
    /// longer a configuration-driven grant mechanism for a custom key.
    /// </summary>
    public const string GenerateReportPermissionKey = "verification.read";

    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="GenerateSampleReportCommand"/>.
    /// </summary>
    public const string GenerateSampleReportCommandId = "sample.reporting-generate";

    /// <summary>
    /// The action <see cref="GenerateSampleReportCommandHandler"/>
    /// records through <see cref="IAuditRecorder"/> on a successful
    /// generation.
    /// </summary>
    public const string ReportGeneratedActionName = "report.generated";

    /// <summary>
    /// The <see cref="IPlatformNotification.Category"/>
    /// <see cref="GenerateSampleReportCommandHandler"/> publishes under
    /// on a successful generation.
    /// </summary>
    public const string ReportGeneratedNotificationCategory = "Reporting";

    private readonly CurrentPrincipalAccessor _principalEstablisher;
    private readonly IReportingService _reportingService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly IAuditRecorder _auditRecorder;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>
    /// Initialises a new instance of the <see cref="ReportingSampleModule"/> class.
    /// </summary>
    /// <param name="principalEstablisher">The concrete accessor this module establishes its own principal on directly (`WP 17.2A`).</param>
    /// <param name="reportingService">The Reporting service this module registers its report definition and renderer through.</param>
    /// <param name="settingsProvider">The Settings service this module registers its renderer's own greeting setting through.</param>
    /// <param name="currentPrincipalAccessor">The service this module's registered command reads the current principal from.</param>
    /// <param name="permissionEvaluator">The service this module's registered command checks permissions against.</param>
    /// <param name="auditRecorder">The Audit service this module's registered command records through.</param>
    /// <param name="notificationDispatcher">The Notification service this module's registered command publishes through.</param>
    /// <param name="commandDispatcher">The Command Framework's dispatch-side surface this module registers its handler through.</param>
    /// <param name="commandRegistry">The Command Framework's discovery-side surface this module registers its descriptor through.</param>
    public ReportingSampleModule(
        CurrentPrincipalAccessor principalEstablisher,
        IReportingService reportingService,
        ISettingsProvider settingsProvider,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IPermissionEvaluator permissionEvaluator,
        IAuditRecorder auditRecorder,
        INotificationDispatcher notificationDispatcher,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.reporting", "Reporting Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(principalEstablisher);
        ArgumentNullException.ThrowIfNull(reportingService);
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(auditRecorder);
        ArgumentNullException.ThrowIfNull(notificationDispatcher);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _principalEstablisher = principalEstablisher;
        _reportingService = reportingService;
        _settingsProvider = settingsProvider;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _permissionEvaluator = permissionEvaluator;
        _auditRecorder = auditRecorder;
        _notificationDispatcher = notificationDispatcher;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    /// <summary>
    /// Gets the principal this module established during its own
    /// <see cref="InitialiseAsync"/>, or <see langword="null"/> before
    /// that has run.
    /// </summary>
    public IPrincipal? EstablishedPrincipal { get; private set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="InitialiseAsync"/> has
    /// registered this module's report definition and command.
    /// </summary>
    public bool HasRegistered { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Establishes <see cref="SampleIdentityId"/> as the current
    /// principal, registers the sample greeting setting, registers
    /// <see cref="SampleSummaryReportDefinition"/> with its own renderer,
    /// then registers <see cref="GenerateSampleReportCommand"/>'s handler
    /// and descriptor.
    /// </remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        EstablishedPrincipal = SamplePrincipalFactory.Establish(_principalEstablisher, SampleIdentityId);

        _settingsProvider.RegisterDefinition(new SettingDefinition(
            SampleSummaryReportRenderer.GreetingSettingKey,
            "Sample Reporting Greeting",
            SampleSummaryReportRenderer.GreetingSettingDefaultValue));

        _reportingService.RegisterDefinition(
            new SampleSummaryReportDefinition(),
            new SampleSummaryReportRenderer(_settingsProvider, new PlainTextReportTemplate<SampleSummaryReportDefinition>()));

        _commandDispatcher.RegisterHandler<GenerateSampleReportCommand>(
            new GenerateSampleReportCommandHandler(
                _currentPrincipalAccessor, _permissionEvaluator, _reportingService, _auditRecorder, _notificationDispatcher));

        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: GenerateSampleReportCommandId,
            displayName: "Generate Sample Report",
            category: "Sample",
            description: "Generates the sample summary report, demonstrating Identity, Settings, Audit, and Notifications integration.",
            createDefault: () => new GenerateSampleReportCommand()));

        HasRegistered = true;

        return Task.CompletedTask;
    }
}
