using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.ExportImport;
using Tempest.Core.Identity;
using Tempest.Core.Modules;
using Tempest.Core.Notifications;
using Tempest.Core.Settings;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates the
/// Export/Import Framework: it registers two sample settings and a
/// <see cref="SettingExportImportAdapter"/> for each during initialisation,
/// then registers two commands (<see cref="ExportSampleDataCommand"/>,
/// <see cref="ImportSampleDataCommand"/>) whose handlers export both
/// adapters as a single, multi-source artifact and re-import it, each
/// gated by its own permission, recorded through Audit, and announced
/// through Notifications.
/// </summary>
/// <remarks>
/// <para>
/// The living reference module `WP 6.7` validates the Export/Import
/// Framework against — mirrors <see cref="ReportingSampleModule"/>'s own
/// role for Reporting and <see cref="SettingsSampleModule"/>'s own role
/// for Settings. Carries <see cref="ModuleMetadataAttribute"/> so Discovery
/// can read its identity without instantiating it (ADR-0027), freeing its
/// constructor to request <see cref="IIdentityService"/>,
/// <see cref="ISettingsProvider"/>, <see cref="ICurrentPrincipalAccessor"/>,
/// <see cref="IPermissionEvaluator"/>, <see cref="IAuditRecorder"/>,
/// <see cref="INotificationDispatcher"/>, <see cref="IExportService"/>,
/// the concrete <see cref="ImportService"/> (needed for
/// <see cref="ImportService.RegisterImportable"/> — see its own remarks),
/// <see cref="ICommandDispatcher"/>, and <see cref="ICommandRegistry"/> —
/// all DI-public platform services — via ordinary constructor injection.
/// </para>
/// <para>
/// Deliberately establishes its own principal (<see cref="SampleIdentityId"/>),
/// rather than depending on <see cref="IdentitySampleModule"/> having
/// already run — every sample module remains independently usable, exactly
/// as <see cref="AuditSampleModule"/>'s own precedent. With no
/// <c>Identity:Roles:*:Permissions</c> configuration supplied,
/// <see cref="ExportPermissionKey"/>/<see cref="ImportPermissionKey"/> are
/// not granted — the fail-closed default handlers report honestly, exactly
/// as <see cref="AuditSampleModule"/>'s own query command does for its own
/// permission.
/// </para>
/// <para>
/// This module deliberately does not depend on
/// <see cref="Tempest.Core.Persistence.IPersistenceStore"/> — Export/
/// Import's own approved contract states "Persistence Requirements: None,"
/// disclosed explicitly rather than wired in speculatively, per `ADR-0051`.
/// See this Work Package's own Platform Integration Demonstration for the
/// full, per-service account.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.exportimport", "Export/Import Sample", "1.0.0")]
public sealed class ExportImportSampleModule : ModuleLifecycleBase
{
    /// <summary>
    /// The identity id this module establishes as current during its own
    /// initialisation.
    /// </summary>
    public const string SampleIdentityId = "sample.exportimport-user";

    /// <summary>The permission key <see cref="ExportSampleDataCommandHandler"/> checks for before exporting.</summary>
    public const string ExportPermissionKey = "exportimport.export";

    /// <summary>The permission key <see cref="ImportSampleDataCommandHandler"/> checks for before importing.</summary>
    public const string ImportPermissionKey = "exportimport.import";

    /// <summary>The <see cref="CommandDescriptor.Id"/> this module registers for <see cref="ExportSampleDataCommand"/>.</summary>
    public const string ExportCommandId = "sample.exportimport-export";

    /// <summary>The <see cref="CommandDescriptor.Id"/> this module registers for <see cref="ImportSampleDataCommand"/>.</summary>
    public const string ImportCommandId = "sample.exportimport-import";

    /// <summary>The action <see cref="ExportSampleDataCommandHandler"/> records through <see cref="IAuditRecorder"/> on a successful export.</summary>
    public const string ExportedActionName = "exportimport.exported";

    /// <summary>The action <see cref="ImportSampleDataCommandHandler"/> records through <see cref="IAuditRecorder"/> on a successful import.</summary>
    public const string ImportedActionName = "exportimport.imported";

    /// <summary>The <see cref="IPlatformNotification.Category"/> both handlers publish under on success.</summary>
    public const string NotificationCategory = "ExportImport";

    /// <summary>The setting key <see cref="GreetingAdapterKind"/> round-trips.</summary>
    public const string GreetingSettingKey = "sample.exportimport.greeting";

    /// <summary><see cref="GreetingSettingKey"/>'s own default value.</summary>
    public const string GreetingSettingDefaultValue = "Sample Export/Import Greeting";

    /// <summary>The artifact section kind the greeting adapter is registered under.</summary>
    public const string GreetingAdapterKind = "tempest.samples.exportimport.greeting";

    /// <summary>The setting key <see cref="SubtitleAdapterKind"/> round-trips.</summary>
    public const string SubtitleSettingKey = "sample.exportimport.subtitle";

    /// <summary><see cref="SubtitleSettingKey"/>'s own default value.</summary>
    public const string SubtitleSettingDefaultValue = "Sample Export/Import Subtitle";

    /// <summary>The artifact section kind the subtitle adapter is registered under.</summary>
    public const string SubtitleAdapterKind = "tempest.samples.exportimport.subtitle";

    private readonly IIdentityService _identityService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly IAuditRecorder _auditRecorder;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IExportService _exportService;
    private readonly ImportService _importService;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>
    /// Initialises a new instance of the <see cref="ExportImportSampleModule"/> class.
    /// </summary>
    /// <param name="identityService">The Identity &amp; Permissions service this module establishes a principal through.</param>
    /// <param name="settingsProvider">The Settings service this module registers its sample settings through, and its adapters read from/write to.</param>
    /// <param name="currentPrincipalAccessor">The service this module's registered commands read the current principal from.</param>
    /// <param name="permissionEvaluator">The service this module's registered commands check permissions against.</param>
    /// <param name="auditRecorder">The Audit service this module's registered commands record through.</param>
    /// <param name="notificationDispatcher">The Notification service this module's registered commands publish through.</param>
    /// <param name="exportService">The Export/Import service this module's export command exports through.</param>
    /// <param name="importService">The concrete Export/Import import service this module registers its adapters with, and its import command imports through.</param>
    /// <param name="commandDispatcher">The Command Framework's dispatch-side surface this module registers its handlers through.</param>
    /// <param name="commandRegistry">The Command Framework's discovery-side surface this module registers its descriptors through.</param>
    public ExportImportSampleModule(
        IIdentityService identityService,
        ISettingsProvider settingsProvider,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IPermissionEvaluator permissionEvaluator,
        IAuditRecorder auditRecorder,
        INotificationDispatcher notificationDispatcher,
        IExportService exportService,
        ImportService importService,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.exportimport", "Export/Import Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(auditRecorder);
        ArgumentNullException.ThrowIfNull(notificationDispatcher);
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(importService);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _identityService = identityService;
        _settingsProvider = settingsProvider;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _permissionEvaluator = permissionEvaluator;
        _auditRecorder = auditRecorder;
        _notificationDispatcher = notificationDispatcher;
        _exportService = exportService;
        _importService = importService;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    /// <summary>
    /// Gets the principal this module established during its own
    /// <see cref="InitialiseAsync"/>, or <see langword="null"/> before that
    /// has run.
    /// </summary>
    public IPrincipal? EstablishedPrincipal { get; private set; }

    /// <summary>
    /// Gets a value indicating whether <see cref="InitialiseAsync"/> has
    /// registered this module's settings, adapters, and commands.
    /// </summary>
    public bool HasRegistered { get; private set; }

    /// <summary>
    /// Gets the demo-only holder <see cref="ExportSampleDataCommandHandler"/>
    /// and <see cref="ImportSampleDataCommandHandler"/> share, once
    /// <see cref="InitialiseAsync"/> has run.
    /// </summary>
    public SampleExportArtifactStore? ArtifactStore { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Establishes <see cref="SampleIdentityId"/> as the current principal,
    /// registers the two sample settings and a
    /// <see cref="SettingExportImportAdapter"/> for each, registers both
    /// adapters with <see cref="ImportService"/>, then registers
    /// <see cref="ExportSampleDataCommand"/>'s and
    /// <see cref="ImportSampleDataCommand"/>'s handlers and descriptors.
    /// </remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        EstablishedPrincipal = _identityService.EstablishCurrentPrincipal(SampleIdentityId);

        _settingsProvider.RegisterDefinition(new SettingDefinition(GreetingSettingKey, "Sample Export/Import Greeting", GreetingSettingDefaultValue));
        _settingsProvider.RegisterDefinition(new SettingDefinition(SubtitleSettingKey, "Sample Export/Import Subtitle", SubtitleSettingDefaultValue));

        var serializer = new JsonExportPayloadSerializer();

        var greetingAdapter = new SettingExportImportAdapter(_settingsProvider, serializer, GreetingSettingKey, GreetingAdapterKind);
        var subtitleAdapter = new SettingExportImportAdapter(_settingsProvider, serializer, SubtitleSettingKey, SubtitleAdapterKind);

        _importService.RegisterImportable(greetingAdapter);
        _importService.RegisterImportable(subtitleAdapter);

        ArtifactStore = new SampleExportArtifactStore();

        var sources = new IExportable[] { greetingAdapter, subtitleAdapter };

        _commandDispatcher.RegisterHandler<ExportSampleDataCommand>(
            new ExportSampleDataCommandHandler(
                _currentPrincipalAccessor, _permissionEvaluator, _exportService, _auditRecorder, _notificationDispatcher, ArtifactStore, sources));

        _commandDispatcher.RegisterHandler<ImportSampleDataCommand>(
            new ImportSampleDataCommandHandler(
                _currentPrincipalAccessor, _permissionEvaluator, _importService, _auditRecorder, _notificationDispatcher, ArtifactStore));

        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: ExportCommandId,
            displayName: "Export Sample Data",
            category: "Sample",
            description: "Exports both sample settings as a single, multi-source artifact, demonstrating Identity, Settings, Audit, and Notifications integration.",
            createDefault: () => new ExportSampleDataCommand()));

        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: ImportCommandId,
            displayName: "Import Sample Data",
            category: "Sample",
            description: "Re-imports the most recently exported sample artifact, demonstrating Identity, Settings, Audit, and Notifications integration.",
            createDefault: () => new ImportSampleDataCommand()));

        HasRegistered = true;

        return Task.CompletedTask;
    }
}
