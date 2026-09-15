namespace Tempest.Core.Macros;

/// <summary>
/// A user-authored, named, ordered sequence of existing registered
/// <see cref="Commands.CommandDescriptor"/> Ids — the "foundation" macro
/// capability (`WP 10.6A`).
/// </summary>
/// <remarks>
/// Deliberately not a scripting language: a macro carries no branching or
/// looping of its own — it is nothing more than a named, ordered list of
/// steps, each dispatched exactly as
/// <see cref="Commands.ICommandRegistry.InvokeAsync"/> already dispatches
/// any other command by Id (`RunMacroCommand`, same namespace). A step's
/// own recorded values (`WP 20.2C`, <see cref="MacroStep.RecordedValues"/>)
/// are the one, narrow exception to "no parameterisation": they are
/// exactly what the person supplied when the step was recorded, replayed
/// unchanged every run, never computed or branched on.
/// </remarks>
public interface ICommandMacro
{
    /// <summary>Gets the macro's own unique, stable Id.</summary>
    Guid Id { get; }

    /// <summary>Gets the macro's own human-readable display name.</summary>
    string Name { get; }

    /// <summary>
    /// Gets the ordered steps this macro invokes, in sequence, when run.
    /// </summary>
    IReadOnlyList<MacroStep> Steps { get; }
}
