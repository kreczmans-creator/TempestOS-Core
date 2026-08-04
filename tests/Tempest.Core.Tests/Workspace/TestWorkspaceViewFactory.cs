using Tempest.App.Workspace;

namespace Tempest.Core.Tests.Workspace;

// A real, minimal IWorkspaceViewFactory constructing TestWorkspaceView
// instances — proves WP8.0B's own ADR-0067 extensibility mechanism end to
// end without any Engineering Core dependency.
public sealed class TestWorkspaceViewFactory(string kind) : IWorkspaceViewFactory
{
    public string Kind { get; } = kind;

    public int CreateCallCount { get; private set; }

    public IWorkspaceView Create(Guid objectId, IWorkspaceContext context)
    {
        CreateCallCount++;
        return new TestWorkspaceView(objectId, Kind, $"{Kind} {objectId}");
    }
}
