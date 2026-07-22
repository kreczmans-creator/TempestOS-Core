using System.Collections.Concurrent;
using Tempest.Core.Logging;

namespace Tempest.Core.Tests.Logging;

/// <summary>
/// A test-only <see cref="ILogger"/> that records every message it receives,
/// used across the test suite wherever a test needs to prove something was
/// logged (rather than merely that logging didn't throw). Backed by a
/// <see cref="ConcurrentQueue{T}"/> so it is itself safe to use from the
/// thread-safety tests without being a source of races in the test fixture.
/// </summary>
internal sealed class RecordingLogger : ILogger
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages.ToList();

    public void Trace(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        _messages.Enqueue(message);

    public void Debug(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        _messages.Enqueue(message);

    public void Information(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        _messages.Enqueue(message);

    public void Warning(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        _messages.Enqueue(message);

    public void Error(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        _messages.Enqueue(message);

    public void Critical(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        _messages.Enqueue(message);
}
