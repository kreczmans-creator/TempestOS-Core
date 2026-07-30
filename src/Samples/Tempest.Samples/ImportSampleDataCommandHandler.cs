using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.ExportImport;
using Tempest.Core.Identity;
using Tempest.Core.Notifications;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="ImportSampleDataCommand"/> by re-importing
/// <see cref="SampleExportArtifactStore"/>'s own most recently exported
/// artifact through <see cref="IImportService"/> — after checking
/// Identity's own permission gate — then recording the action through
/// <see cref="IAuditRecorder"/> and publishing a completion notice through
/// <see cref="INotificationDispatcher"/>.
/// </summary>
/// <remarks>
/// <see cref="IImportService.ImportAsync"/> does not itself check
/// permissions — this handler is that enforcement point, mirroring
/// <see cref="ExportSampleDataCommandHandler"/>'s own convention.
/// </remarks>
public sealed class ImportSampleDataCommandHandler : ICommandHandler<ImportSampleDataCommand>
{
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly IImportService _importService;
    private readonly IAuditRecorder _auditRecorder;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly SampleExportArtifactStore _artifactStore;

    /// <summary>
    /// Initialises a new instance of the <see cref="ImportSampleDataCommandHandler"/> class.
    /// </summary>
    /// <param name="currentPrincipalAccessor">The service this handler reads the current principal from.</param>
    /// <param name="permissionEvaluator">The service this handler checks the import permission against.</param>
    /// <param name="importService">The Export/Import service this handler imports through.</param>
    /// <param name="auditRecorder">The Audit service this handler records the import through.</param>
    /// <param name="notificationDispatcher">The Notification service this handler publishes a completion notice through.</param>
    /// <param name="artifactStore">The demo-only holder this handler reads the artifact to import from.</param>
    public ImportSampleDataCommandHandler(
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IPermissionEvaluator permissionEvaluator,
        IImportService importService,
        IAuditRecorder auditRecorder,
        INotificationDispatcher notificationDispatcher,
        SampleExportArtifactStore artifactStore)
    {
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(importService);
        ArgumentNullException.ThrowIfNull(auditRecorder);
        ArgumentNullException.ThrowIfNull(notificationDispatcher);
        ArgumentNullException.ThrowIfNull(artifactStore);

        _currentPrincipalAccessor = currentPrincipalAccessor;
        _permissionEvaluator = permissionEvaluator;
        _importService = importService;
        _auditRecorder = auditRecorder;
        _notificationDispatcher = notificationDispatcher;
        _artifactStore = artifactStore;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(ImportSampleDataCommand command, CancellationToken cancellationToken)
    {
        var principal = _currentPrincipalAccessor.Current;

        if (principal is null)
            return CommandResult.Failure("No current principal is established.");

        var permission = new Permission(ExportImportSampleModule.ImportPermissionKey);

        if (!_permissionEvaluator.HasPermission(principal, permission))
            return CommandResult.Failure($"Principal '{principal.Identity.Id}' does not hold '{permission.Key}'.");

        var artifact = _artifactStore.Artifact;

        if (artifact is null)
            return CommandResult.Failure("No artifact has been exported yet.");

        using var source = new MemoryStream(artifact, writable: false);

        await _importService.ImportAsync(source, cancellationToken).ConfigureAwait(false);

        await _auditRecorder.RecordAsync(
            ExportImportSampleModule.ImportedActionName,
            new Dictionary<string, string> { ["ByteLength"] = artifact.Length.ToString() },
            cancellationToken).ConfigureAwait(false);

        await _notificationDispatcher.PublishAsync<IPlatformNotification>(
            new PlatformNotification(
                ExportImportSampleModule.NotificationCategory,
                NotificationSeverity.Success,
                "Sample data imported."),
            cancellationToken).ConfigureAwait(false);

        return CommandResult.Success($"Imported artifact ({artifact.Length} bytes).");
    }
}
