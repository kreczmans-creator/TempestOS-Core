using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler queries every audit record
/// attributed to <see cref="AuditSampleModule.SampleIdentityId"/> through
/// <see cref="Tempest.Core.Audit.IAuditQuery"/> — demonstrating both the
/// permission-gated denied path (no grant configured) and the granted
/// path (with configuration supplying
/// <see cref="Tempest.Core.Audit.AuditQuery.QueryPermission"/>).
/// </summary>
/// <remarks>
/// Carries no data — see <see cref="QuerySampleAuditRecordsCommandHandler"/>.
/// </remarks>
public sealed class QuerySampleAuditRecordsCommand : ICommand
{
}
