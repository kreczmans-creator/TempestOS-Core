using Tempest.Core.Commands;

namespace Tempest.Desktop.Composition;

/// <summary>
/// `WP 19.4A`: one generic <see cref="ICommand"/> wrapping a Desktop-tier
/// shell action that carries no engineering-object data of its own — the
/// Command Palette's only route to Reset Layout, Theme, Macros and View
/// Relationships now that the menu bar and Quick Access Toolbar are gone
/// from the engineering surface (`po-comments.md` #3,
/// <c>MainWindowComposer.Layout</c>'s own remarks).
/// </summary>
/// <remarks>
/// One command type, one handler (<see cref="ShellActionCommandHandler"/>),
/// registered once — every shell action is then just another
/// <see cref="CommandDescriptor"/> whose <see cref="CommandDescriptor.CreateDefault"/>
/// builds one of these around the same delegate the removed menu/toolbar
/// button used to call directly, never a new domain command type per
/// action: none of these touch the persistence store or need an audit row
/// (`brief-common.md`'s "every write is one transaction" guard is about
/// engineering-domain writes, and none of Reset Layout, Theme, Macros or
/// View Relationships is one — they read or rearrange the window's own
/// session-only state).
/// </remarks>
internal sealed class ShellActionCommand : ICommand
{
    /// <summary>The action this command runs when dispatched.</summary>
    public required Func<Task<CommandResult>> Action { get; init; }
}

/// <summary>The one handler for every <see cref="ShellActionCommand"/> — just runs the action it carries.</summary>
internal sealed class ShellActionCommandHandler : ICommandHandler<ShellActionCommand>
{
    /// <inheritdoc />
    public Task<CommandResult> HandleAsync(ShellActionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Action();
    }
}
