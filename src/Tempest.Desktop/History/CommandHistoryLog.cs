namespace Tempest.Desktop.History;

/// <summary>
/// A bounded, session-only log of completed actions (`WP 10.6A`) — the
/// "Command history" productivity feature.
/// </summary>
/// <remarks>
/// <para>
/// Honest, disclosed scope: this records only what already reaches
/// <c>MainWindow</c>'s own existing <c>ActionCompleted</c>/dispatch-result
/// surfaces (Ribbon, Project Explorer, Property Inspector, Object Editor,
/// Undo/Redo) — every one of them already produces the human-readable
/// outcome message this log stores. It is not a global interception of
/// <see cref="Tempest.Core.Commands.ICommandDispatcher"/> itself (frozen,
/// unmodified) — a command dispatched through some future path that
/// bypasses these existing UI surfaces would not appear here.
/// </para>
/// <para>
/// Session-only, like <see cref="Tempest.App.Workspace.IUndoRedoStack"/> —
/// never persisted across a restart.
/// </para>
/// </remarks>
public sealed class CommandHistoryLog
{
    /// <summary>The maximum number of entries retained — the oldest is discarded once exceeded.</summary>
    public const int Capacity = 200;

    private readonly List<CommandHistoryEntry> _entries = [];

    /// <summary>Raised after <see cref="Record"/> adds a new entry — a UI's own refresh hook.</summary>
    public event Action? Changed;

    /// <summary>Gets every recorded entry, oldest first.</summary>
    public IReadOnlyList<CommandHistoryEntry> Entries => _entries;

    /// <summary>Records <paramref name="description"/>/<paramref name="succeeded"/>, timestamped now.</summary>
    public void Record(string description, bool succeeded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        _entries.Add(new CommandHistoryEntry(DateTimeOffset.Now, description, succeeded));
        if (_entries.Count > Capacity)
            _entries.RemoveAt(0);

        Changed?.Invoke();
    }
}
