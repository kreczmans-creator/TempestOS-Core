using Tempest.App.Workspace;

namespace Tempest.Core.Tests.Workspace.Samples;

/// <summary>
/// Renders exactly one fictional sample engineering object — never dirty,
/// never editable, since it exists to prove <see cref="INavigationService"/>/
/// <see cref="IProjectExplorer"/> end to end, not to demonstrate real
/// engineering-object editing (out of `WP 8.1B`'s own explicit scope).
/// </summary>
public sealed class SampleWorkspaceView : IWorkspaceView
{
    /// <summary>Initialises a new instance of the <see cref="SampleWorkspaceView"/> class.</summary>
    public SampleWorkspaceView(Guid objectId, string objectKind, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        ObjectId = objectId;
        ObjectKind = objectKind;
        Title = title;
    }

    /// <inheritdoc />
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public string Title { get; }

    /// <inheritdoc />
    public Guid ObjectId { get; }

    /// <inheritdoc />
    public string ObjectKind { get; }

    /// <inheritdoc />
    public bool IsDirty => false;

    /// <inheritdoc />
    /// <remarks>A no-op — the fixed sample dataset never changes underneath an open view.</remarks>
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>Always returns <see langword="true"/> — never dirty, so no unsaved-edit prompt is ever needed.</remarks>
    public Task<bool> CloseAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}
