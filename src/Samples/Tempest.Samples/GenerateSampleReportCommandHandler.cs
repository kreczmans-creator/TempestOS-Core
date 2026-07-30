using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Identity;
using Tempest.Core.Notifications;
using Tempest.Core.Reporting;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="GenerateSampleReportCommand"/> by generating
/// <see cref="SampleSummaryReportDefinition"/> through
/// <see cref="IReportingService"/> — after checking Identity's own
/// permission gate — then recording the action through
/// <see cref="IAuditRecorder"/> and publishing a completion notice
/// through <see cref="INotificationDispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IReportingService.GenerateAsync"/> does not itself check
/// permissions (`Platform Service Contracts.md`'s own Security
/// Considerations for Reporting: "the enforcement point is the caller,
/// not the service") — this handler is that enforcement point, checking
/// <see cref="ReportingSampleModule.GenerateReportPermissionKey"/> via
/// the non-throwing <see cref="IPermissionEvaluator.HasPermission"/>,
/// mirroring <see cref="CheckSamplePermissionCommandHandler"/>'s own
/// convention (a denied check is reported as an ordinary
/// <see cref="CommandResult.Failure(string)"/>, not an unhandled
/// <see cref="PermissionDeniedException"/>).
/// </para>
/// <para>
/// The published notification deliberately carries only a fixed,
/// non-identifying success message — never the report's own content or
/// byte length — per `Platform Service Contracts.md`'s own Notification
/// Framework Security Considerations ("a 'report is ready' notification
/// should not leak report content to an unauthorized subscriber; the
/// notification payload should carry only what's safe for any
/// subscriber of that type to see"). The full result (content type and
/// byte length) is returned only to this command's own caller, via
/// <see cref="CommandResult"/>, never broadcast.
/// </para>
/// </remarks>
public sealed class GenerateSampleReportCommandHandler : ICommandHandler<GenerateSampleReportCommand>
{
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly IReportingService _reportingService;
    private readonly IAuditRecorder _auditRecorder;
    private readonly INotificationDispatcher _notificationDispatcher;

    /// <summary>
    /// Initialises a new instance of the <see cref="GenerateSampleReportCommandHandler"/> class.
    /// </summary>
    /// <param name="currentPrincipalAccessor">The service this handler reads the current principal from.</param>
    /// <param name="permissionEvaluator">The service this handler checks the report-generation permission against.</param>
    /// <param name="reportingService">The Reporting service this handler generates the report through.</param>
    /// <param name="auditRecorder">The Audit service this handler records generation through.</param>
    /// <param name="notificationDispatcher">The Notification service this handler publishes a completion notice through.</param>
    public GenerateSampleReportCommandHandler(
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IPermissionEvaluator permissionEvaluator,
        IReportingService reportingService,
        IAuditRecorder auditRecorder,
        INotificationDispatcher notificationDispatcher)
    {
        ArgumentNullException.ThrowIfNull(currentPrincipalAccessor);
        ArgumentNullException.ThrowIfNull(permissionEvaluator);
        ArgumentNullException.ThrowIfNull(reportingService);
        ArgumentNullException.ThrowIfNull(auditRecorder);
        ArgumentNullException.ThrowIfNull(notificationDispatcher);

        _currentPrincipalAccessor = currentPrincipalAccessor;
        _permissionEvaluator = permissionEvaluator;
        _reportingService = reportingService;
        _auditRecorder = auditRecorder;
        _notificationDispatcher = notificationDispatcher;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(GenerateSampleReportCommand command, CancellationToken cancellationToken)
    {
        var principal = _currentPrincipalAccessor.Current;

        if (principal is null)
            return CommandResult.Failure("No current principal is established.");

        var permission = new Permission(ReportingSampleModule.GenerateReportPermissionKey);

        if (!_permissionEvaluator.HasPermission(principal, permission))
            return CommandResult.Failure($"Principal '{principal.Identity.Id}' does not hold '{permission.Key}'.");

        var result = await _reportingService.GenerateAsync(
            SampleSummaryReportDefinition.ReportId,
            new ReportRequest(new Dictionary<string, string>()),
            cancellationToken).ConfigureAwait(false);

        await _auditRecorder.RecordAsync(
            ReportingSampleModule.ReportGeneratedActionName,
            new Dictionary<string, string> { ["ContentType"] = result.ContentType },
            cancellationToken).ConfigureAwait(false);

        await _notificationDispatcher.PublishAsync<IPlatformNotification>(
            new PlatformNotification(
                ReportingSampleModule.ReportGeneratedNotificationCategory,
                NotificationSeverity.Success,
                "Sample Summary Report generated."),
            cancellationToken).ConfigureAwait(false);

        return CommandResult.Success($"Generated report ({result.ContentType}, {result.Content.Length} bytes).");
    }
}
