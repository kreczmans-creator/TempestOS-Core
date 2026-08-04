using Tempest.Core.Events;

namespace Tempest.App.Workspace;

/// <summary>
/// The Status Bar's own current text — the one region of `WP8.0A UI
/// Architecture.md` §1's own five-region layout with no dedicated public
/// contract among the twelve `WP8.0B Workspace Contracts.md` names, since
/// none of the twelve required it. Reacts to
/// <see cref="WorkspaceSelectionChangedEvent"/> exactly as
/// <see cref="PropertyInspector"/> does — the identical "who reacts to
/// what" wiring, applied a second time.
/// </summary>
internal sealed class WorkspaceStatusBar : IEventHandler<WorkspaceSelectionChangedEvent>
{
    /// <summary>Gets the Status Bar's own current text.</summary>
    public string StatusText { get; private set; } = "Ready.";

    /// <summary>Sets the Status Bar's own current text directly — used for area-switch and lifecycle status, which have no dedicated event of their own in this Work Package's own scope.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public void SetStatus(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        StatusText = text;
    }

    /// <inheritdoc />
    public Task HandleAsync(WorkspaceSelectionChangedEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        StatusText = @event.Current is null
            ? "Ready."
            : $"Selected: {@event.Current.Kind} {@event.Current.ObjectId}";

        return Task.CompletedTask;
    }
}
