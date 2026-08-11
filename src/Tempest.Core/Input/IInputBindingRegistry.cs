namespace Tempest.Core.Input;

/// <summary>
/// Tracks every currently registered <see cref="IInputBindingProvider"/>
/// and routes each one's own <see cref="IInputBindingProvider.CommandRequested"/>
/// event to the shared <see cref="Commands.ICommandRegistry"/>.
/// </summary>
/// <remarks>
/// A Platform Service (ADR-0036), DI-public like <see cref="Commands.ICommandRegistry"/>.
/// See <see cref="InputBindingRouter"/> for the one concrete implementation.
/// </remarks>
public interface IInputBindingRegistry
{
    /// <summary>Gets every currently registered provider.</summary>
    IReadOnlyList<IInputBindingProvider> Providers { get; }

    /// <summary>Registers <paramref name="provider"/> — its own <see cref="IInputBindingProvider.CommandRequested"/> event is subscribed to immediately, routing every future request through to <see cref="Commands.ICommandRegistry.InvokeAsync"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    void Register(IInputBindingProvider provider);

    /// <summary>Unregisters <paramref name="provider"/>, if currently registered — a no-op otherwise.</summary>
    void Unregister(IInputBindingProvider provider);
}
