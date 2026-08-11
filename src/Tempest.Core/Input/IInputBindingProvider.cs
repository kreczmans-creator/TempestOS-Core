namespace Tempest.Core.Input;

/// <summary>
/// A source of physical or virtual user input that can request a
/// registered <see cref="Commands.CommandDescriptor.Id"/> be invoked — the
/// platform abstraction (`WP 10.6A`, `ADR-0100`) that lets every command
/// this platform registers be bound to a keyboard shortcut, a user macro,
/// a Stream Deck button, a programmable keypad, a mouse button, a game
/// controller, or a MIDI device, all identically, without the Command
/// Framework itself ever needing to know which.
/// </summary>
/// <remarks>
/// Deliberately minimal: a provider's only obligation is to raise
/// <see cref="CommandRequested"/> with a Command Id when its own input
/// occurs — <em>how</em> it decides which Id (a fixed map, a
/// user-configurable one, a physical device's own SDK callback) is
/// entirely its own concern, invisible to <see cref="IInputBindingRegistry"/>
/// and to <see cref="Commands.ICommandRegistry"/> alike.
/// </remarks>
public interface IInputBindingProvider
{
    /// <summary>Gets this provider's own human-readable source name — for example <c>"Keyboard"</c> or <c>"Stream Deck"</c>.</summary>
    string SourceName { get; }

    /// <summary>Raised when this provider's own input requests a command be invoked, carrying the requested <see cref="Commands.CommandDescriptor.Id"/>.</summary>
    event Action<string>? CommandRequested;
}
