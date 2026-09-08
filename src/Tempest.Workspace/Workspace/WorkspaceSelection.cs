namespace Tempest.App.Workspace;

/// <summary>The Workspace's own current selection — an engineering object's identity and Kind.</summary>
/// <param name="ObjectId">The selected object's own Id.</param>
/// <param name="Kind">The selected object's own <c>Kind</c> — for example, <c>"Requirement"</c>.</param>
public sealed record WorkspaceSelection(Guid ObjectId, string Kind);
