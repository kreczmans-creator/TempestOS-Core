namespace Tempest.Core.Input;

/// <summary>
/// Tracks every currently registered <see cref="IInputBindingProvider"/>
/// and routes each one's own <see cref="IInputBindingProvider.CommandRequested"/>
/// event to the shared <see cref="Commands.ICommandRegistry"/>, through the
/// canonical <c>Evaluate</c> then <c>InvokeAsync(id, context, prompt, ct)</c>
/// path every other command surface uses (`WP-A2`).
/// </summary>
/// <remarks>
/// A Platform Service (ADR-0036), DI-public like <see cref="Commands.ICommandRegistry"/>.
/// See <see cref="InputBindingRouter"/> for the one concrete implementation.
/// </remarks>
public interface IInputBindingRegistry
{
    /// <summary>Gets every currently registered provider.</summary>
    IReadOnlyList<IInputBindingProvider> Providers { get; }

    /// <summary>
    /// Reads the application's own current <see cref="Commands.CommandContext"/>
    /// when a bound gesture fires, or <see langword="null"/> when the composing
    /// application has no selection to offer (`WP-A2`).
    /// </summary>
    /// <remarks>
    /// Set once, by whatever composes the shell. Left unset, a command needing
    /// a selected object is refused with its own declared reason rather than
    /// invoked against a fabricated context.
    /// </remarks>
    Func<Commands.CommandContext>? ContextSource { get; set; }

    /// <summary>
    /// Collects the values and confirmations a bound command's own binding
    /// declares, or <see langword="null"/> when nothing can ask (`WP-A2`).
    /// </summary>
    /// <remarks>
    /// A person is present when a key is pressed, so a real prompt is honest
    /// here. Left unset, a parameterised or confirmation-gated command reports
    /// that it needs input rather than running without asking.
    /// </remarks>
    Commands.CommandParameterPrompt? ParameterPrompt { get; set; }

    /// <summary>Registers <paramref name="provider"/> — its own <see cref="IInputBindingProvider.CommandRequested"/> event is subscribed to immediately, routing every future request through to <see cref="Commands.ICommandRegistry.InvokeAsync"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    void Register(IInputBindingProvider provider);

    /// <summary>Unregisters <paramref name="provider"/>, if currently registered — a no-op otherwise.</summary>
    void Unregister(IInputBindingProvider provider);
}
