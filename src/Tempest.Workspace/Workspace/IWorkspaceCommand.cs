using Tempest.Core.Commands;

namespace Tempest.App.Workspace;

/// <summary>
/// Extends the existing <see cref="ICommand"/> — never a second, parallel
/// command contract. Every mutating Workspace action still dispatches
/// through the existing <see cref="ICommandDispatcher"/>; this interface
/// adds only the one piece of metadata generic Workspace infrastructure
/// needs to react uniformly after any Workspace-originated command
/// succeeds (calling <see cref="IWorkspaceView.RefreshAsync"/> on whatever
/// open view matches <see cref="TargetObjectId"/>).
/// </summary>
/// <remarks>
/// No concrete <see cref="IWorkspaceCommand"/> is implemented by this Work
/// Package — no engineering functionality exists yet for a command to act
/// on. This interface exists so a future implementation Work Package's own
/// commands have a contract to implement against.
/// </remarks>
public interface IWorkspaceCommand : ICommand
{
    /// <summary>Gets the engineering object this command acts on.</summary>
    Guid TargetObjectId { get; }

    /// <summary>Gets the <c>Kind</c> of <see cref="TargetObjectId"/> — for example <c>"Requirement"</c>.</summary>
    string TargetKind { get; }
}
