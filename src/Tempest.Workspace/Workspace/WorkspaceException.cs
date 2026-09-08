namespace Tempest.App.Workspace;

/// <summary>The abstract base of every exception this namespace throws.</summary>
public abstract class WorkspaceException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="WorkspaceException"/> class.</summary>
    /// <param name="message">The exception message.</param>
    protected WorkspaceException(string message) : base(message)
    {
    }
}
