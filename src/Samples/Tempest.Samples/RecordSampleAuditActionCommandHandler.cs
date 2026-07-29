using Tempest.Core.Audit;
using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="RecordSampleAuditActionCommand"/> by recording
/// <see cref="AuditSampleModule.ManualActionName"/> through
/// <see cref="IAuditRecorder"/>.
/// </summary>
public sealed class RecordSampleAuditActionCommandHandler : ICommandHandler<RecordSampleAuditActionCommand>
{
    private readonly IAuditRecorder _auditRecorder;

    /// <summary>
    /// Initialises a new instance of the <see cref="RecordSampleAuditActionCommandHandler"/> class.
    /// </summary>
    /// <param name="auditRecorder">The Audit service this handler records through.</param>
    public RecordSampleAuditActionCommandHandler(IAuditRecorder auditRecorder)
    {
        ArgumentNullException.ThrowIfNull(auditRecorder);

        _auditRecorder = auditRecorder;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(RecordSampleAuditActionCommand command, CancellationToken cancellationToken)
    {
        await _auditRecorder.RecordAsync(AuditSampleModule.ManualActionName, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return CommandResult.Success($"Recorded action '{AuditSampleModule.ManualActionName}'.");
    }
}
