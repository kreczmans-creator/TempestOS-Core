using System.Collections.Concurrent;
using Tempest.Core.Logging;

namespace Tempest.Core.Tests.Logging;

/// <summary>
/// A test-only <see cref="ILogSink"/> that records every <see cref="LogEntry"/> it
/// receives, backed by a <see cref="ConcurrentQueue{T}"/> so it is safe to use
/// directly from thread-safety tests.
/// </summary>
internal sealed class RecordingLogSink : ILogSink
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToList();

    public void Write(LogEntry entry) => _entries.Enqueue(entry);
}
