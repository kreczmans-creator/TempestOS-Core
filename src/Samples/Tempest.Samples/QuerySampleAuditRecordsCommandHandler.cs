using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Identity;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="QuerySampleAuditRecordsCommand"/> by querying every
/// audit record attributed to <see cref="AuditSampleModule.SampleIdentityId"/>
/// through <see cref="IAuditQuery"/>.
/// </summary>
/// <remarks>
/// Reports the denial explicitly, as an ordinary
/// <see cref="CommandResult.Failure(string)"/>, when the current
/// principal does not hold <see cref="AuditQuery.QueryPermission"/> —
/// this handler's own choice, not a Command Framework or Audit
/// requirement, mirroring
/// <see cref="Tempest.Samples.CheckSamplePermissionCommandHandler"/>'s
/// own convention.
/// </remarks>
public sealed class QuerySampleAuditRecordsCommandHandler : ICommandHandler<QuerySampleAuditRecordsCommand>
{
    private readonly IAuditQuery _auditQuery;

    /// <summary>
    /// Initialises a new instance of the <see cref="QuerySampleAuditRecordsCommandHandler"/> class.
    /// </summary>
    /// <param name="auditQuery">The Audit service this handler queries through.</param>
    public QuerySampleAuditRecordsCommandHandler(IAuditQuery auditQuery)
    {
        ArgumentNullException.ThrowIfNull(auditQuery);

        _auditQuery = auditQuery;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(QuerySampleAuditRecordsCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var records = await _auditQuery.QueryAsync(
                new AuditQueryCriteria(actorId: AuditSampleModule.SampleIdentityId),
                cancellationToken).ConfigureAwait(false);

            return CommandResult.Success($"Found {records.Count} record(s) for '{AuditSampleModule.SampleIdentityId}'.");
        }
        catch (PermissionDeniedException)
        {
            return CommandResult.Failure("Denied: current principal does not hold the audit-query permission.");
        }
    }
}
