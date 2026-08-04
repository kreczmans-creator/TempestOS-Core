using Tempest.App.Workspace;

namespace Tempest.Core.Tests.Workspace;

// A real, minimal IWorkspaceView — mirrors PlaceholderPage's own role in
// Shell tests, not a mock standing in for a production type (this project
// does not use a mocking framework).
public sealed class TestWorkspaceView(Guid objectId, string objectKind, string title) : IWorkspaceView
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Title { get; } = title;

    public Guid ObjectId { get; } = objectId;

    public string ObjectKind { get; } = objectKind;

    public bool IsDirty { get; set; }

    public bool RefreshCalled { get; private set; }

    public bool CloseResult { get; set; } = true;

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        RefreshCalled = true;
        return Task.CompletedTask;
    }

    public Task<bool> CloseAsync(CancellationToken cancellationToken = default) => Task.FromResult(CloseResult);
}
