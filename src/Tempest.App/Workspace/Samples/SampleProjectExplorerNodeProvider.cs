namespace Tempest.App.Workspace.Samples;

/// <summary>
/// Populates one Project Explorer area from <see cref="SampleExplorerContent"/>'s
/// own fixed, fictional tree — the living reference provider `WP 8.1B`'s own
/// controlling instruction names ("populate the tree with representative
/// engineering objects only"), proving the Kind-keyed
/// <see cref="IProjectExplorerNodeProvider"/> extension point (`ADR-0067`)
/// against real, running content without any Engineering Core dependency.
/// </summary>
public sealed class SampleProjectExplorerNodeProvider : IProjectExplorerNodeProvider
{
    /// <summary>Initialises a new instance of the <see cref="SampleProjectExplorerNodeProvider"/> class.</summary>
    /// <param name="kind">The top-level area this provider populates.</param>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    public SampleProjectExplorerNodeProvider(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        Kind = kind;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SampleExplorerContent.RootNodes);

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known sample node.</exception>
    public Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        if (!SampleExplorerContent.TryGetChildren(nodeId, out var children))
            throw new ArgumentException($"'{nodeId}' is not a known sample node.", nameof(nodeId));

        return Task.FromResult(children);
    }
}
