using Tempest.Core.Logging;

namespace Tempest.Core.Tests.ExportImport;

/// <summary>
/// A test-only <see cref="ILogger"/> that records each message alongside
/// the severity it was logged at, mirroring
/// <see cref="Tempest.Core.Tests.Events.RecordingLevelLogger"/>. Used here
/// to prove <c>ExportService</c>/<c>ImportService</c>'s own Logging
/// Requirements.
/// </summary>
internal sealed class RecordingLevelLogger : ILogger
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries;

    public bool HasEntryAt(LogLevel level, string messageSubstring) =>
        _entries.Any(entry => entry.Level == level && entry.Message.Contains(messageSubstring, StringComparison.Ordinal));

    public void Trace(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        _entries.Add((LogLevel.Trace, message));

    public void Debug(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        _entries.Add((LogLevel.Debug, message));

    public void Information(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        _entries.Add((LogLevel.Information, message));

    public void Warning(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        _entries.Add((LogLevel.Warning, message));

    public void Error(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        _entries.Add((LogLevel.Error, message));

    public void Critical(string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) =>
        _entries.Add((LogLevel.Critical, message));
}
