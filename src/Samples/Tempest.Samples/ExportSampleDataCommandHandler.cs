using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.ExportImport;
using Tempest.Core.Identity;
using Tempest.Core.Notifications;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="ExportSampleDataCommand"/> by exporting both of
/// <see cref="ExportImportSampleModule"/>'s own sample settings, as a
/// single, multi-source artifact, through <see cref="IExportService"/> —
/// after checking Identity's own permission gate — then recording the
/// action through <see cref="IAuditRecorder"/>, publishing a completion
/// notice through <see cref="INotificationDispatcher"/>, and storing the
/// artifact in <see cref="SampleExportArtifactStore"/> for a later
/// <see cref="ImportSampleDataCommand"/> to read back.
/// </summary>
/// <remarks>
/// <see cref="IExportService.ExportAsync"/> does not itself check
/// permissions (`Platform Service Contracts.md`'s own Security
/// Considerations: "the enforcement point is the caller, not this
/// service") — this handler is that enforcement point, mirroring
/// <see cref="GenerateSampleReportCommandHandler"/>'s own convention.
/// </remarks>
public sealed class ExportSampleDataCommandHandler : ICommandHandler<ExportSampleDataCommand>
{
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly IExportService _exportService;
    private readonly IAuditRecorder _auditRecorder;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly SampleExportArtifactStore _artifactStore;
    private readonly IReadOnlyList<IExportable> _sources;

    /// <summary>
    /// Initialises a new instance of the <see cref="ExportSampleDataCommandHandler"/> class.
    /// </summary>
    /// <param name="currentPrincipalAccessor">The service this handler reads the current principal from.</param>
    /// <param name="permissionEvaluator">The service this handler checks the export permission against.</param>
    /// <param name="exportService">The Export/Import service this handler exports through.</param>
    /// <param name="auditRecorder">The Audit service this handler records the export through.</param>
    /// <param name="notificationDispatcher">The Notification service this handler publishes a completion notice through.</param>
    /// <param name="artifactStore">The demo-only holder this handler stores the exported artifact in.</param>
    /// <param name="sources">The sources this handler exports, in order, on every invocation.</param>
    public ExportSampleDataCommandHandler(
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IPermissionEvaluator permissionEvaluator,
        IExportService exportService,
        IAuditRecorder auditRecorder,
        INotificationDispatcher notificationDispatcher,
        SampleExportArtifactStore artifactStore,
        IReadOnlyList<IExportable> sources)
    {
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(auditRecorder);
        ArgumentNullException.ThrowIfNull(notificationDispatcher);
        ArgumentNullException.ThrowIfNull(artifactStore);
        ArgumentNullException.ThrowIfNull(sources);

        _currentPrincipalAccessor = currentPrincipalAccessor;
        _permissionEvaluator = permissionEvaluator;
        _exportService = exportService;
        _auditRecorder = auditRecorder;
        _notificationDispatcher = notificationDispatcher;
        _artifactStore = artifactStore;
        _sources = sources;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(ExportSampleDataCommand command, CancellationToken cancellationToken)
    {
        var principal = _currentPrincipalAccessor.Current;

        if (principal is null)
            return CommandResult.Failure("No current principal is established.");

        var permission = new Permission(ExportImportSampleModule.ExportPermissionKey);

        if (!_permissionEvaluator.HasPermission(principal, permission))
            return CommandResult.Failure($"Principal '{principal.Identity.Id}' does not hold '{permission.Key}'.");

        using var destination = new MemoryStream();

        await _exportService.ExportAsync(destination, _sources, cancellationToken).ConfigureAwait(false);

        _artifactStore.Artifact = destination.ToArray();

        await _auditRecorder.RecordAsync(
            ExportImportSampleModule.ExportedActionName,
            new Dictionary<string, string> { ["SourceCount"] = _sources.Count.ToString(), ["ByteLength"] = _artifactStore.Artifact.Length.ToString() },
            cancellationToken).ConfigureAwait(false);

        await _notificationDispatcher.PublishAsync<IPlatformNotification>(
            new PlatformNotification(
                ExportImportSampleModule.NotificationCategory,
                NotificationSeverity.Success,
                "Sample data exported."),
            cancellationToken).ConfigureAwait(false);

        return CommandResult.Success($"Exported {_sources.Count} source(s) ({_artifactStore.Artifact.Length} bytes).");
    }
}
