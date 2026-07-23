using Tempest.Core.Logging;

namespace Tempest.Core.Tests.Logging;

/// <summary>
/// A test-only <see cref="ILogSink"/> that always throws from <see cref="Write"/>,
/// used to verify that <see cref="Logger"/> isolates sink failures rather than
/// letting them propagate (see Failure Behaviour.md, "Logging failure").
/// </summary>
internal sealed class ThrowingLogSink : ILogSink
{
    private readonly Exception _exception;

    public ThrowingLogSink(Exception? exception = null)
    {
        _exception = exception ?? new InvalidOperationException("Simulated sink failure.");
    }

    public int WriteAttempts { get; private set; }

    public void Write(LogEntry entry)
    {
        WriteAttempts++;
        throw _exception;
    }
}
