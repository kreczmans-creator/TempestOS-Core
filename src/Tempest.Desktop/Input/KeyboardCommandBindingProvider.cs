using Avalonia.Input;
using Tempest.Core.Input;

namespace Tempest.Desktop.Input;

/// <summary>
/// The one real <see cref="IInputBindingProvider"/> this Work Package
/// ships (`WP 10.6A`, `ADR-0100`) — a genuine, working <see cref="KeyGesture"/>
/// → Command Id map, proving keyboard input is just another
/// <see cref="IInputBindingProvider"/>, routed through the identical
/// <see cref="IInputBindingRegistry"/> a future Stream Deck/MIDI/game
/// controller provider would use, with zero Command Framework changes.
/// </summary>
/// <remarks>
/// Deliberately distinct from <see cref="KeyboardShortcuts"/> — that
/// class binds a small, fixed set of navigation/structural actions
/// directly (Command Palette, document switching), unchanged by this
/// Work Package. This class is a second, additive, generic mechanism:
/// any registered command's own Id can be bound to any gesture at
/// runtime via <see cref="Bind"/>. Ships with zero default bindings —
/// no remapping UI exists yet to author them (disclosed, real future
/// work); the mechanism itself is real, working, and tested.
/// </remarks>
public sealed class KeyboardCommandBindingProvider : IInputBindingProvider
{
    private readonly Dictionary<KeyGesture, string> _bindings = new();

    /// <inheritdoc />
    public string SourceName => "Keyboard";

    /// <inheritdoc />
    public event Action<string>? CommandRequested;

    /// <summary>Binds <paramref name="gesture"/> to invoke <paramref name="commandId"/> — replaces any existing binding for the same gesture.</summary>
    public void Bind(KeyGesture gesture, string commandId)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);

        _bindings[gesture] = commandId;
    }

    /// <summary>Removes any binding for <paramref name="gesture"/> — a no-op if none exists.</summary>
    public void Unbind(KeyGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        _bindings.Remove(gesture);
    }

    /// <summary>Gets every currently bound gesture and the Command Id it requests.</summary>
    public IReadOnlyDictionary<KeyGesture, string> Bindings => _bindings;

    /// <summary>
    /// Handles one <see cref="KeyEventArgs"/> from a host window's own
    /// <c>KeyDown</c> — raises <see cref="CommandRequested"/> and marks
    /// the event handled if a binding matches; a no-op otherwise (in
    /// particular, a no-op if <paramref name="e"/> is already
    /// <see cref="RoutedEventArgs.Handled"/> by
    /// <see cref="KeyboardShortcuts"/>'s own fixed bindings, which take
    /// priority).
    /// </summary>
    public void HandleKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Handled)
            return;

        var gesture = new KeyGesture(e.Key, e.KeyModifiers);
        if (!_bindings.TryGetValue(gesture, out var commandId))
            return;

        e.Handled = true;
        CommandRequested?.Invoke(commandId);
    }
}
