using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Identity;
using Tempest.Core.Licensing;
using Tempest.Core.Notifications;
using Tempest.Core.Settings;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="CheckSampleCapabilityCommand"/> by checking whether
/// the current license enables <see cref="LicensingSampleModule.SampleCapabilityKey"/>
/// through <see cref="ILicenseProvider"/> — after checking Identity's own
/// permission gate — then recording the outcome through
/// <see cref="IAuditRecorder"/> and publishing a completion notice
/// through <see cref="INotificationDispatcher"/>.
/// </summary>
/// <remarks>
/// <see cref="ILicenseProvider.HasCapability"/> does not itself check
/// permissions or implement any commercial policy — it only answers
/// "is this capability enabled." This handler is both the permission
/// enforcement point (mirroring every other sample command handler's own
/// convention) and the place a denied capability is turned into an
/// ordinary <see cref="CommandResult.Failure(string)"/>, never an
/// unhandled exception — capability absence is an expected, everyday
/// outcome for an unlicensed installation, not a fault.
/// </remarks>
public sealed class CheckSampleCapabilityCommandHandler : ICommandHandler<CheckSampleCapabilityCommand>
{
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly ILicenseProvider _licenseProvider;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IAuditRecorder _auditRecorder;
    private readonly INotificationDispatcher _notificationDispatcher;

    /// <summary>
    /// Initialises a new instance of the <see cref="CheckSampleCapabilityCommandHandler"/> class.
    /// </summary>
    /// <param name="currentPrincipalAccessor">The service this handler reads the current principal from.</param>
    /// <param name="permissionEvaluator">The service this handler checks the capability-check permission against.</param>
    /// <param name="licenseProvider">The Licensing service this handler checks the sample capability through.</param>
    /// <param name="settingsProvider">The Settings service this handler reads its own premium message from.</param>
    /// <param name="auditRecorder">The Audit service this handler records the outcome through.</param>
    /// <param name="notificationDispatcher">The Notification service this handler publishes a completion notice through.</param>
    public CheckSampleCapabilityCommandHandler(
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IPermissionEvaluator permissionEvaluator,
        ILicenseProvider licenseProvider,
        ISettingsProvider settingsProvider,
        IAuditRecorder auditRecorder,
        INotificationDispatcher notificationDispatcher)
    {
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(licenseProvider);
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentNullException.ThrowIfNull(auditRecorder);
        ArgumentNullException.ThrowIfNull(notificationDispatcher);

        _currentPrincipalAccessor = currentPrincipalAccessor;
        _permissionEvaluator = permissionEvaluator;
        _licenseProvider = licenseProvider;
        _settingsProvider = settingsProvider;
        _auditRecorder = auditRecorder;
        _notificationDispatcher = notificationDispatcher;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(CheckSampleCapabilityCommand command, CancellationToken cancellationToken)
    {
        var principal = _currentPrincipalAccessor.Current;

        if (principal is null)
            return CommandResult.Failure("No current principal is established.");

        var permission = new Permission(LicensingSampleModule.CapabilityCheckPermissionKey);

        if (!_permissionEvaluator.HasPermission(principal, permission))
            return CommandResult.Failure($"Principal '{principal.Identity.Id}' does not hold '{permission.Key}'.");

        var hasCapability = _licenseProvider.HasCapability(LicensingSampleModule.SampleCapabilityKey);

        if (!hasCapability)
        {
            await _auditRecorder.RecordAsync(
                LicensingSampleModule.CapabilityDeniedActionName,
                new Dictionary<string, string> { ["Capability"] = LicensingSampleModule.SampleCapabilityKey, ["Licensee"] = _licenseProvider.CurrentLicense.LicenseeName },
                cancellationToken).ConfigureAwait(false);

            await _notificationDispatcher.PublishAsync<IPlatformNotification>(
                new PlatformNotification(
                    LicensingSampleModule.NotificationCategory,
                    NotificationSeverity.Warning,
                    "Sample capability is not licensed."),
                cancellationToken).ConfigureAwait(false);

            return CommandResult.Failure(
                $"Capability '{LicensingSampleModule.SampleCapabilityKey}' is not enabled by the current license " +
                $"(licensee: '{_licenseProvider.CurrentLicense.LicenseeName}').");
        }

        var premiumMessage = await _settingsProvider.GetValueAsync(
            LicensingSampleModule.PremiumMessageSettingKey, cancellationToken).ConfigureAwait(false);

        await _auditRecorder.RecordAsync(
            LicensingSampleModule.CapabilityGrantedActionName,
            new Dictionary<string, string> { ["Capability"] = LicensingSampleModule.SampleCapabilityKey, ["Licensee"] = _licenseProvider.CurrentLicense.LicenseeName },
            cancellationToken).ConfigureAwait(false);

        await _notificationDispatcher.PublishAsync<IPlatformNotification>(
            new PlatformNotification(
                LicensingSampleModule.NotificationCategory,
                NotificationSeverity.Success,
                "Sample capability check succeeded."),
            cancellationToken).ConfigureAwait(false);

        return CommandResult.Success(premiumMessage);
    }
}
