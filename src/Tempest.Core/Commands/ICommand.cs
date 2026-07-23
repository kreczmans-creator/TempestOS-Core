namespace Tempest.Core.Commands;

/// <summary>
/// A discrete, named unit of application logic a module or future caller
/// can request the platform to execute.
/// </summary>
/// <remarks>
/// <para>
/// A command is data: a concrete type implementing this interface carries
/// whatever parameters its own execution needs as ordinary properties. A
/// command is dispatched by its concrete type — the framework a later work
/// package builds resolves exactly one handler for a given command type,
/// mirroring how <see cref="Tempest.Core.Modules.IModule"/> is a plain
/// identity/metadata contract and
/// <see cref="Tempest.Core.Modules.IModuleLifecycle"/> is the separate
/// contract describing behaviour. The same split is deliberately reused
/// here: <see cref="ICommand"/> names what a command is; a handler contract
/// describing how one is handled is deliberately not defined yet — that is
/// WP 4.7's own design work, not speculated on ahead of it.
/// </para>
/// <para>
/// This is a contract only. No dispatcher exists yet — declaring this
/// interface today introduces no new runtime behaviour.
/// </para>
/// <para>
/// Never depends on, or is invoked by, navigation — see ADR-0022.
/// Commands and navigation are orthogonal; a command's own execution may,
/// as one of its effects, cause navigation to occur, but does so by
/// depending on a navigation service directly, not through this contract.
/// </para>
/// </remarks>
public interface ICommand
{
}
