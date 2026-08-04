namespace Tempest.App.Workspace;

/// <summary>
/// Thrown when <see cref="IWorkspaceManager.RegisterView"/> or
/// <see cref="IWorkspaceManager.RegisterExplorerArea"/> is called twice for
/// the same <see cref="Kind"/>.
/// </summary>
public sealed class DuplicateWorkspaceRegistrationException : WorkspaceException
{
    /// <summary>Initialises a new instance of the <see cref="DuplicateWorkspaceRegistrationException"/> class.</summary>
    /// <param name="kind">The <c>Kind</c> already registered.</param>
    public DuplicateWorkspaceRegistrationException(string kind)
        : base($"A registration already exists for Kind '{kind}'.")
    {
        Kind = kind;
    }

    /// <summary>Gets the <c>Kind</c> already registered.</summary>
    public string Kind { get; }
}
