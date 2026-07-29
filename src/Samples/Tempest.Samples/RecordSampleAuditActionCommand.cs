using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler records an action through
/// <see cref="Tempest.Core.Audit.IAuditRecorder"/>, attributed to
/// whatever principal is currently established.
/// </summary>
/// <remarks>
/// Carries no data — see <see cref="RecordSampleAuditActionCommandHandler"/>.
/// </remarks>
public sealed class RecordSampleAuditActionCommand : ICommand
{
}
