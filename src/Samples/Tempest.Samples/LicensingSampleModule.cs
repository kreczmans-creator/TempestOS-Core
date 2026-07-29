using Tempest.Core.Api;
using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Identity;
using Tempest.Core.Licensing;
using Tempest.Core.Modules;
using Tempest.Core.Notifications;
using Tempest.Core.Settings;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates the
/// Licensing Framework: it registers a sample setting and a command
/// (<see cref="CheckSampleCapabilityCommand"/>) whose handler is
/// permission-gated, checks a sample capability through
/// <see cref="ILicenseProvider"/>, records the outcome through Audit,
/// and publishes a completion notice through Notifications — then maps
/// that same command to an HTTP route, demonstrating the REST API
/// exposing a Licensing-gated capability with zero business logic of
/// its own, mirroring <see cref="ApiSampleModule"/>'s own precedent.
/// </summary>
/// <remarks>
/// <para>
/// The living reference module `WP 6.6` validates the Licensing
/// Framework against — mirrors <see cref="ReportingSampleModule"/>'s own
/// role for Reporting. Carries <see cref="ModuleMetadataAttribute"/> so
/// Discovery can read its identity without instantiating it (ADR-0027),
/// freeing its constructor to request <see cref="IIdentityService"/>,
/// <see cref="ISettingsProvider"/>, <see cref="ICurrentPrincipalAccessor"/>,
/// <see cref="IPermissionEvaluator"/>, <see cref="ILicenseProvider"/>,
/// <see cref="IAuditRecorder"/>, <see cref="INotificationDispatcher"/>,
/// <see cref="IApiEndpointRegistry"/>, <see cref="ICommandDispatcher"/>,
/// and <see cref="ICommandRegistry"/> — all DI-public platform services —
/// via ordinary constructor injection.
/// </para>
/// <para>
/// Deliberately establishes its own principal (<see cref="SampleIdentityId"/>),
/// rather than depending on <see cref="IdentitySampleModule"/> having
/// already run — every sample module remains independently usable,
/// exactly as <see cref="AuditSampleModule"/>'s own precedent. With no
/// <c>Identity:Roles:*:Permissions</c> configuration supplied,
/// <see cref="CapabilityCheckPermissionKey"/> is not granted — the
/// fail-closed default <see cref="CheckSampleCapabilityCommandHandler"/>
/// reports honestly, exactly as <see cref="AuditSampleModule"/>'s own
/// query command does for its own permission. Independently, with no
/// license file present, <see cref="SampleCapabilityKey"/> is not
/// enabled either — an unlicensed installation reports the capability
/// as unavailable, not as an error, exactly as <c>ADR-0050</c> intends.
/// </para>
/// <para>
/// This module deliberately does not depend on
/// <see cref="Tempest.Core.Persistence.IPersistenceStore"/> or
/// <see cref="Tempest.Core.Reporting.IReportingService"/> — Licensing's
/// own approved contract states "Persistence Requirements: None," and no
/// commercial reporting need exists for this sample, disclosed
/// explicitly rather than wired in speculatively. See this Work
/// Package's own Platform Integration Demonstration for the full,
/// per-service account.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.licensing", "Licensing Sample", "1.0.0")]
public sealed class LicensingSampleModule : ModuleLifecycleBase
{
    /// <summary>
    /// The identity id this module establishes as current during its own
    /// initialisation.
    /// </summary>
    public const string SampleIdentityId = "sample.licensing-user";

    /// <summary>
    /// The permission key <see cref="CheckSampleCapabilityCommandHandler"/>
    /// checks for before evaluating the sample capability.
    /// </summary>
    public const string CapabilityCheckPermissionKey = "licensing.check-capability";

    /// <summary>
    /// The capability key <see cref="CheckSampleCapabilityCommandHandler"/>
    /// checks via <see cref="ILicenseProvider.HasCapability"/>.
    /// </summary>
    public const string SampleCapabilityKey = "sample.premium-feature";

    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module
    /// registers for <see cref="CheckSampleCapabilityCommand"/>.
    /// </summary>
    public const string CheckCapabilityCommandId = "sample.licensing-check-capability";

    /// <summary>
    /// The action <see cref="CheckSampleCapabilityCommandHandler"/>
    /// records through <see cref="IAuditRecorder"/> when the sample
    /// capability is enabled.
    /// </summary>
    public const string CapabilityGrantedActionName = "licensing.capability-granted";

    /// <summary>
    /// The action <see cref="CheckSampleCapabilityCommandHandler"/>
    /// records through <see cref="IAuditRecorder"/> when the sample
    /// capability is not enabled.
    /// </summary>
    public const string CapabilityDeniedActionName = "licensing.capability-denied";

    /// <summary>
    /// The <see cref="IPlatformNotification.Category"/>
    /// <see cref="CheckSampleCapabilityCommandHandler"/> publishes under.
    /// </summary>
    public const string NotificationCategory = "Licensing";

    /// <summary>The setting key this module's own command reads its premium message from.</summary>
    public const string PremiumMessageSettingKey = "sample.licensing.premiummessage";

    /// <summary><see cref="PremiumMessageSettingKey"/>'s own default value.</summary>
    public const string PremiumMessageSettingDefaultValue = "Premium Feature Unlocked";

    /// <summary>The HTTP method <see cref="CheckCapabilityRoutePath"/> is mapped under.</summary>
    public const string CheckCapabilityRouteMethod = "POST";

    /// <summary>The route path mapped to <see cref="CheckCapabilityCommandId"/>.</summary>
    public const string CheckCapabilityRoutePath = "/api/v1/sample-capability";

    private readonly IIdentityService _identityService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly ILicenseProvider _licenseProvider;
    private readonly IAuditRecorder _auditRecorder;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IApiEndpointRegistry _endpointRegistry;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>
    /// Initialises a new instance of the <see cref="LicensingSampleModule"/> class.
    /// </summary>
    /// <param name="identityService">The Identity &amp; Permissions service this module establishes a principal through.</param>
    /// <param name="settingsProvider">The Settings service this module registers its premium message setting through.</param>
    /// <param name="currentPrincipalAccessor">The service this module's registered command reads the current principal from.</param>
    /// <param name="permissionEvaluator">The service this module's registered command checks permissions against.</param>
    /// <param name="licenseProvider">The Licensing service this module's registered command checks the sample capability through.</param>
    /// <param name="auditRecorder">The Audit service this module's registered command records through.</param>
    /// <param name="notificationDispatcher">The Notification service this module's registered command publishes through.</param>
    /// <param name="endpointRegistry">The REST API service this module maps its route through.</param>
    /// <param name="commandDispatcher">The Command Framework's dispatch-side surface this module registers its handler through.</param>
    /// <param name="commandRegistry">The Command Framework's discovery-side surface this module registers its descriptor through.</param>
    public LicensingSampleModule(
        IIdentityService identityService,
        ISettingsProvider settingsProvider,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IPermissionEvaluator permissionEvaluator,
        ILicenseProvider licenseProvider,
        IAuditRecorder auditRecorder,
        INotificationDispatcher notificationDispatcher,
        IApiEndpointRegistry endpointRegistry,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.licensing", "Licensing Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(licenseProvider);
        ArgumentNullException.ThrowIfNull(auditRecorder);
        ArgumentNullException.ThrowIfNull(notificationDispatcher);
        ArgumentNullException.ThrowIfNull(endpointRegistry);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _identityService = identityService;
        _settingsProvider = settingsProvider;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _permissionEvaluator = permissionEvaluator;
        _licenseProvider = licenseProvider;
        _auditRecorder = auditRecorder;
        _notificationDispatcher = notificationDispatcher;
        _endpointRegistry = endpointRegistry;
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
    /// registered this module's setting, command, and route.
    /// </summary>
    public bool HasRegistered { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Establishes <see cref="SampleIdentityId"/> as the current
    /// principal, registers the sample premium-message setting, registers
    /// <see cref="CheckSampleCapabilityCommand"/>'s handler and
    /// descriptor, then maps <see cref="CheckCapabilityRouteMethod"/> +
    /// <see cref="CheckCapabilityRoutePath"/> to it.
    /// </remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        EstablishedPrincipal = _identityService.EstablishCurrentPrincipal(SampleIdentityId);

        _settingsProvider.RegisterDefinition(new SettingDefinition(
            PremiumMessageSettingKey, "Sample Licensing Premium Message", PremiumMessageSettingDefaultValue));

        _commandDispatcher.RegisterHandler<CheckSampleCapabilityCommand>(
            new CheckSampleCapabilityCommandHandler(
                _currentPrincipalAccessor, _permissionEvaluator, _licenseProvider, _settingsProvider, _auditRecorder, _notificationDispatcher));

        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CheckCapabilityCommandId,
            displayName: "Check Sample Capability",
            category: "Sample",
            description: "Checks whether the current license enables the sample capability, demonstrating Identity, Licensing, Settings, Audit, and Notifications integration.",
            createDefault: () => new CheckSampleCapabilityCommand()));

        _endpointRegistry.MapCommand(
            CheckCapabilityRouteMethod,
            CheckCapabilityRoutePath,
            CheckCapabilityCommandId,
            new Permission(CapabilityCheckPermissionKey));

        HasRegistered = true;

        return Task.CompletedTask;
    }
}
