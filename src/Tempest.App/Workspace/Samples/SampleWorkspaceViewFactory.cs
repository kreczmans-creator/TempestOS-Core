namespace Tempest.App.Workspace.Samples;

/// <summary>
/// Constructs a <see cref="SampleWorkspaceView"/> for one sample-object
/// <c>Kind</c> (<see cref="SampleExplorerContent.AssemblyKind"/> or
/// <see cref="SampleExplorerContent.ComponentKind"/>), looking the object's
/// own title up from <see cref="SampleExplorerContent"/> — the living
/// reference <see cref="IWorkspaceViewFactory"/> proving the Kind-keyed view
/// extension point (`ADR-0067`) against real, running content.
/// </summary>
public sealed class SampleWorkspaceViewFactory : IWorkspaceViewFactory
{
    /// <summary>Initialises a new instance of the <see cref="SampleWorkspaceViewFactory"/> class.</summary>
    /// <param name="kind">The single sample-object <c>Kind</c> this factory constructs a view for.</param>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    public SampleWorkspaceViewFactory(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        Kind = kind;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known sample object.</exception>
    public IWorkspaceView Create(Guid objectId, IWorkspaceContext context)
    {
        var node = SampleExplorerContent.Find(objectId)
            ?? throw new ArgumentException($"'{objectId}' is not a known sample object.", nameof(objectId));

        return new SampleWorkspaceView(objectId, Kind, node.Title);
    }
}
