namespace Tempest.Core.Macros;

/// <summary>
/// A user-authored, named, ordered sequence of existing registered
/// <see cref="Commands.CommandDescriptor"/> Ids — the "foundation" macro
/// capability (`WP 10.6A`).
/// </summary>
/// <remarks>
/// Deliberately not a scripting language: a macro carries no branching,
/// looping, or parameterisation of its own — it is nothing more than a
/// named, ordered list of Ids, each dispatched exactly as
/// <see cref="Commands.ICommandRegistry.InvokeAsync"/> already dispatches
/// any other command by Id (`RunMacroCommand`, same namespace). A step
/// whose own descriptor has no <see cref="Commands.CommandDescriptor.CreateDefault"/>
/// factory cannot be invoked by Id at all — the identical, pre-existing
/// platform-wide limitation <c>CommandPaletteOverlay</c>'s own remarks
/// already document, not a restriction this type introduces.
/// </remarks>
public interface ICommandMacro
{
    /// <summary>Gets the macro's own unique, stable Id.</summary>
    Guid Id { get; }

    /// <summary>Gets the macro's own human-readable display name.</summary>
    string Name { get; }

    /// <summary>
    /// Gets the ordered <see cref="Commands.CommandDescriptor.Id"/>s this
    /// macro invokes, in sequence, when run.
    /// </summary>
    IReadOnlyList<string> StepCommandIds { get; }
}
