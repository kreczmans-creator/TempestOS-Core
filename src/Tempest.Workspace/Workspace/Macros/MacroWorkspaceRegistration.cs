using Tempest.Core.Commands;
using Tempest.Core.Macros;

namespace Tempest.App.Workspace.Macros;

/// <summary>
/// The composition-root entry point wiring the User Command Macro
/// foundation (`WP 10.6A`) into a running Workspace — registers
/// <see cref="RunMacroCommandHandler"/> once, then loads every previously
/// persisted macro (re-registering each one's own <see cref="CommandDescriptor"/>).
/// </summary>
/// <remarks>
/// Called from <c>EngineeringWorkspaceComposer.RegisterEngineeringDisciplines</c>
/// alongside the six real Engineering Disciplines, purely for
/// host-lifecycle convenience (both need the Host already started) — not
/// itself a seventh discipline: Macros are a cross-cutting productivity
/// capability over whatever commands already exist, not a new Engineering
/// Domain area of their own.
/// </remarks>
public static class MacroWorkspaceRegistration
{
    /// <summary>Registers the Macro foundation's own command handler and loads every persisted macro.</summary>
    public static void Register(ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry, IMacroManager macroManager)
    {
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(macroManager);

        commandDispatcher.RegisterHandler<RunMacroCommand>(new RunMacroCommandHandler(macroManager, commandRegistry));

        macroManager.LoadAsync().GetAwaiter().GetResult();
    }
}
