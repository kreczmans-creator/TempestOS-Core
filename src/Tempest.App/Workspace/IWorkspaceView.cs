namespace Tempest.App.Workspace;

/// <summary>
/// Renders exactly one engineering object — never a relationship list or a
/// composed digital-thread read, both of which are the Property Inspector's
/// own concern (`WP8.0A UI Architecture.md` §3.1, `ADR-0065`).
/// </summary>
public interface IWorkspaceView
{
    /// <summary>Gets this view's own unique identifier.</summary>
    Guid Id { get; }

    /// <summary>Gets this view's own display title.</summary>
    string Title { get; }

    /// <summary>Gets the object this view presents.</summary>
    Guid ObjectId { get; }

    /// <summary>Gets the <c>Kind</c> of <see cref="ObjectId"/> — for example <c>"Requirement"</c>.</summary>
    string ObjectKind { get; }

    /// <summary>Gets a value indicating whether this view holds local edits not yet committed via a Command.</summary>
    bool IsDirty { get; }

    /// <summary>Re-reads <see cref="ObjectId"/> from its owning service and refreshes this view's own display. Never uses a cached copy.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests this view close. Returns <see langword="false"/> if the
    /// caller should prompt the user about unsaved edits
    /// (<see cref="IsDirty"/>) before proceeding — this method itself never
    /// prompts, since prompting is a concrete rendering concern, not a
    /// contract-level one.
    /// </summary>
    Task<bool> CloseAsync(CancellationToken cancellationToken = default);
}
