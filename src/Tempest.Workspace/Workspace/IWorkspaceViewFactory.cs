namespace Tempest.App.Workspace;

/// <summary>
/// Constructs an <see cref="IWorkspaceView"/> for one specific engineering
/// object <c>Kind</c> — a future Engineering Discipline Module's own answer
/// to "how does one of my objects get presented" (`ADR-0067`).
/// </summary>
public interface IWorkspaceViewFactory
{
    /// <summary>Gets the single <see cref="IWorkspaceView.ObjectKind"/> this factory constructs a view for.</summary>
    string Kind { get; }

    /// <summary>Constructs a new view for <paramref name="objectId"/>.</summary>
    /// <param name="objectId">The object to present.</param>
    /// <param name="context">The Workspace's own ambient, read-only current state.</param>
    IWorkspaceView Create(Guid objectId, IWorkspaceContext context);
}
