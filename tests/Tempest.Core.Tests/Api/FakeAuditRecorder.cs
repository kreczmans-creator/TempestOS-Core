using Tempest.Core.Audit;

namespace Tempest.Core.Tests.Api;

/// <summary>
/// A minimal, in-memory <see cref="IAuditRecorder"/> that records every
/// call it receives, for direct assertion — used instead of the real,
/// <c>IPersistenceStore</c>-backed <c>AuditRecorder</c> so
/// <see cref="ApiRequestHandler"/> can be unit-tested in isolation, with
/// no temp-directory or file-system dependency.
/// </summary>
internal sealed class FakeAuditRecorder : IAuditRecorder
{
    public List<(string Action, IReadOnlyDictionary<string, string>? Detail)> Recorded { get; } = [];

    public Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default)
    {
        Recorded.Add((action, detail));
        return Task.CompletedTask;
    }
}
