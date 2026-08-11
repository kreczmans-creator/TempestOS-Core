namespace Tempest.Core.Input;

/// <summary>
/// An <see cref="IInputBindingProvider"/> backed by a physical, external
/// device — a Stream Deck, a programmable macro keypad, a MIDI
/// controller, a game controller — distinct from <see cref="IsConnected"/>
/// -less software-only providers (keyboard, mouse) already built into the
/// running machine.
/// </summary>
/// <remarks>
/// A contract only, deliberately never implemented against a real vendor
/// SDK by this platform (`WP 10.6A`'s own explicit Out-of-Scope: no
/// Stream Deck plugin, no hardware integration). The one implementation
/// this Work Package ships is a test-only double
/// (<c>StubExternalControllerProvider</c>, <c>Tempest.Core.Tests</c>)
/// proving <see cref="IInputBindingRegistry"/> already routes a real
/// device-shaped provider's own events with zero Command Framework
/// changes — a future Work Package with an actual vendor SDK dependency
/// implements this interface for real, unchanged.
/// </remarks>
public interface IExternalControllerProvider : IInputBindingProvider
{
    /// <summary>Gets whether this controller currently reports itself connected.</summary>
    bool IsConnected { get; }
}
