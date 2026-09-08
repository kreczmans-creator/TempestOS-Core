namespace Tempest.App.Workspace;

/// <summary>
/// Thrown by <see cref="INavigationService.OpenAsync"/>/<see cref="INavigationService.JumpToAsync"/>
/// when no <see cref="IWorkspaceViewFactory"/> is registered for the
/// requested <c>Kind</c>.
/// </summary>
public sealed class WorkspaceViewFactoryNotFoundException : WorkspaceException
{
    /// <summary>Initialises a new instance of the <see cref="WorkspaceViewFactoryNotFoundException"/> class.</summary>
    /// <param name="kind">The <c>Kind</c> no factory is registered for.</param>
    public WorkspaceViewFactoryNotFoundException(string kind)
        : base($"No IWorkspaceViewFactory is registered for Kind '{kind}'.")
    {
        Kind = kind;
    }

    /// <summary>Gets the <c>Kind</c> no factory is registered for.</summary>
    public string Kind { get; }
}
