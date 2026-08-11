using Tempest.Core.Input;

namespace Tempest.Core.Tests.Input;

/// <summary>
/// A test-only <see cref="IExternalControllerProvider"/> double, simulating
/// a Stream-Deck-shaped physical device — proves
/// <see cref="IInputBindingRegistry"/> already routes a real device-shaped
/// provider's own events with zero Command Framework changes (`WP 10.6A`,
/// `ADR-0100`). Explicitly, permanently a test double — never a real
/// vendor SDK integration, matching this Work Package's own Out-of-Scope
/// (no Stream Deck plugin, no hardware integration).
/// </summary>
public sealed class StubExternalControllerProvider : IExternalControllerProvider
{
    /// <inheritdoc />
    public string SourceName => "Stub Stream Deck";

    /// <inheritdoc />
    public bool IsConnected { get; set; } = true;

    /// <inheritdoc />
    public event Action<string>? CommandRequested;

    /// <summary>Simulates a physical button press bound to <paramref name="commandId"/>.</summary>
    public void SimulatePress(string commandId) => CommandRequested?.Invoke(commandId);
}
