using Tempest.Core.Commands;

namespace Tempest.App.Workspace;

/// <summary>
/// Creates and owns the lifecycle of the one running <see cref="IWorkspace"/>
/// instance — the Workspace's own equivalent of
/// <see cref="Tempest.Core.Runtime.ITempestHostBuilder"/>/
/// <see cref="Tempest.Core.Runtime.ITempestHost"/>, and the Workspace's own
/// registration point for the extensibility mechanisms that resolve
/// `ADR-0067`.
/// </summary>
public interface IWorkspaceManager
{
    /// <summary>Gets the current running Workspace, or <see langword="null"/> before <see cref="StartAsync"/>.</summary>
    IWorkspace? Current { get; }

    /// <summary>Assembles and starts the Workspace — the composition-root entry point.</summary>
    Task<IWorkspace> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists current state (<see cref="IWorkspaceState.SaveAsync"/>) and shuts the Workspace down.</summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers the one <see cref="IWorkspaceViewFactory"/> responsible for
    /// presenting an engineering object of <paramref name="kind"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateWorkspaceRegistrationException">A factory is already registered for <paramref name="kind"/>.</exception>
    void RegisterView(string kind, IWorkspaceViewFactory factory);

    /// <summary>
    /// Registers the one <see cref="IProjectExplorerNodeProvider"/> responsible
    /// for populating the Project Explorer's own tree for <paramref name="kind"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateWorkspaceRegistrationException">A provider is already registered for <paramref name="kind"/>.</exception>
    void RegisterExplorerArea(string kind, IProjectExplorerNodeProvider provider);

    /// <summary>
    /// Registers the one <see cref="IPropertyFacetProvider"/> responsible for
    /// supplying the Property Inspector's own real facets for <paramref name="kind"/>.
    /// A genuine, disclosed `WP 9.0A` addition to this frozen `WP8.0B`
    /// contract — additive only (a new member, no existing member changed),
    /// applying the same Kind-keyed registration principle
    /// <see cref="RegisterView"/>/<see cref="RegisterExplorerArea"/> already
    /// establish (`ADR-0067`) a third time.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateWorkspaceRegistrationException">A provider is already registered for <paramref name="kind"/>.</exception>
    void RegisterFacetProvider(string kind, IPropertyFacetProvider provider);

    /// <summary>
    /// Registers the factory building the real, discipline-specific rename
    /// command for objects of Kind <paramref name="kind"/> — a genuine,
    /// disclosed `WP 10.2A` addition to this frozen `WP8.0B` contract
    /// (`ADR-0096`), additive only. Realises the "future context-menu
    /// action" every discipline's own <c>Rename*Command</c> was already
    /// built, and already dispatcher-registered, to serve
    /// (`MechanicalWorkspaceRegistration`'s own remarks, `WP 9.0A`) — the
    /// Project Explorer's own inline rename and the Property Inspector's
    /// own editable Name field (`WP 10.2A`) are its first two real callers.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateWorkspaceRegistrationException">A rename factory is already registered for <paramref name="kind"/>.</exception>
    void RegisterRenameFactory(string kind, Func<Guid, string, string, IWorkspaceCommand> factory);

    /// <summary>
    /// Registers the factory building the real, discipline-specific delete
    /// command for objects of Kind <paramref name="kind"/> — the delete
    /// counterpart of <see cref="RegisterRenameFactory"/>, identical
    /// rationale (`WP 10.2A`, `ADR-0096`).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateWorkspaceRegistrationException">A delete factory is already registered for <paramref name="kind"/>.</exception>
    void RegisterDeleteFactory(string kind, Func<Guid, string, IWorkspaceCommand> factory);

    /// <summary>
    /// Registers the factory building the real, discipline-specific content-
    /// revision command for objects of Kind <paramref name="kind"/> — a
    /// genuine, disclosed `WP 10.3A` addition to this frozen `WP8.0B`
    /// contract (`ADR-0097`), additive only, mirroring
    /// <see cref="RegisterRenameFactory"/>'s own `ADR-0096` shape exactly a
    /// second time. Every discipline's own already-existing
    /// <c>Revise*Command</c> (built for the console/Command Palette,
    /// `WP 9.x`) is its own real factory here — the Object Editor
    /// Framework's own Content field is this member's first real caller.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateWorkspaceRegistrationException">A revise factory is already registered for <paramref name="kind"/>.</exception>
    void RegisterReviseFactory(string kind, Func<Guid, string, string, IWorkspaceCommand> factory);

    /// <summary>Gets whether a rename factory is registered for <paramref name="kind"/> — the honest, pre-check surface a menu/inline-edit UI uses to decide whether to offer renaming at all, never guessing or always-enabling.</summary>
    bool CanRename(string kind);

    /// <summary>Gets whether a delete factory is registered for <paramref name="kind"/> — the delete counterpart of <see cref="CanRename"/>.</summary>
    bool CanDelete(string kind);

    /// <summary>Gets whether a revise factory is registered for <paramref name="kind"/> — the revise counterpart of <see cref="CanRename"/>.</summary>
    bool CanRevise(string kind);

    /// <summary>
    /// Renames <paramref name="id"/>/<paramref name="kind"/> to
    /// <paramref name="newDisplayName"/> by building the registered
    /// rename command (<see cref="RegisterRenameFactory"/>) and dispatching
    /// it through the real, already-registered handler for its own
    /// concrete type. Returns a <see cref="CommandResult.Failure(string)"/>,
    /// never throws, if no rename factory is registered for
    /// <paramref name="kind"/> — the identical "foreseeable failure, not a
    /// defect" discipline every command handler in this platform already
    /// follows (ADR-0038).
    /// </summary>
    Task<CommandResult> RenameObjectAsync(Guid id, string kind, string newDisplayName, CancellationToken cancellationToken = default);

    /// <summary>The delete counterpart of <see cref="RenameObjectAsync"/>.</summary>
    Task<CommandResult> DeleteObjectAsync(Guid id, string kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revises <paramref name="id"/>/<paramref name="kind"/>'s own content
    /// to <paramref name="newContent"/> by building the registered revise
    /// command (<see cref="RegisterReviseFactory"/>) and dispatching it
    /// through the real, already-registered handler for its own concrete
    /// type — the revise counterpart of <see cref="RenameObjectAsync"/>.
    /// Returns a <see cref="CommandResult.Failure(string)"/>, never throws,
    /// if no revise factory is registered for <paramref name="kind"/>.
    /// </summary>
    Task<CommandResult> ReviseObjectAsync(Guid id, string kind, string newContent, CancellationToken cancellationToken = default);
}
