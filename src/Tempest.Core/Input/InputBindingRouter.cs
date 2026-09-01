using Tempest.Core.Commands;
using Tempest.Core.Logging;

namespace Tempest.Core.Input;

/// <summary>The concrete <see cref="IInputBindingRegistry"/> implementation.</summary>
/// <remarks>
/// Isolates a throwing provider or a failed invocation — logged, never
/// propagated back into the provider that raised it — mirroring
/// <see cref="Events.IEventBus"/>'s own established subscriber-isolation
/// precedent (`ADR-0028`): one misbehaving controller must not be able to
/// crash every other bound input source.
/// </remarks>
public sealed class InputBindingRouter : IInputBindingRegistry
{
    private readonly ICommandRegistry _commandRegistry;
    private readonly ILogger? _logger;
    private readonly object _gate = new();
    private readonly List<IInputBindingProvider> _providers = [];
    private readonly Dictionary<IInputBindingProvider, Action<string>> _handlersByProvider = new();

    /// <summary>
    /// Reads the application's own current <see cref="CommandContext"/> at
    /// the moment a gesture fires, or <see langword="null"/> when whatever
    /// composed this router has no selection to offer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Supplied, never fabricated (`WP-A2`).</b> A gesture is pressed in a
    /// shell that knows what is selected, and this is how that reaches Core
    /// without Core knowing the shell exists: <see cref="CommandContext"/> is
    /// a Core type, so a <see cref="Func{TResult}"/> over it crosses no
    /// layer. <c>MainWindow</c> supplies the same
    /// <c>WorkspaceCommandContext.From(workspace.Selection)</c> the Command
    /// Palette already uses. Left unset, every command needing a selected
    /// object is refused with its own declared reason rather than run
    /// against an invented one.
    /// </para>
    /// </remarks>
    public Func<CommandContext>? ContextSource { get; set; }

    /// <summary>
    /// Collects the values and confirmations a command's own binding
    /// declares, or <see langword="null"/> when nothing can ask.
    /// </summary>
    /// <remarks>
    /// A person is present when a key is pressed, so passing the shell's own
    /// prompt is honest here — unlike an unattended macro or an HTTP
    /// request, which deliberately pass none (`ADR-0098`, `AT-10`). Left
    /// unset, a parameterised or confirmation-gated command reports that it
    /// needs input rather than running without asking.
    /// </remarks>
    public CommandParameterPrompt? ParameterPrompt { get; set; }

    /// <summary>Initialises a new instance of the <see cref="InputBindingRouter"/> class.</summary>
    public InputBindingRouter(ICommandRegistry commandRegistry, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _commandRegistry = commandRegistry;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<IInputBindingProvider> Providers
    {
        get
        {
            lock (_gate)
                return _providers.ToList();
        }
    }

    /// <inheritdoc />
    public void Register(IInputBindingProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        void Handler(string commandId) => RouteAsync(provider, commandId);

        lock (_gate)
        {
            if (_handlersByProvider.ContainsKey(provider))
                return;

            _handlersByProvider[provider] = Handler;
            _providers.Add(provider);
            provider.CommandRequested += Handler;
        }

        _logger?.Information($"Input binding provider registered: '{provider.SourceName}'.");
    }

    /// <inheritdoc />
    public void Unregister(IInputBindingProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lock (_gate)
        {
            if (!_handlersByProvider.Remove(provider, out var handler))
                return;

            provider.CommandRequested -= handler;
            _providers.Remove(provider);
        }

        _logger?.Information($"Input binding provider unregistered: '{provider.SourceName}'.");
    }

    /// <summary>
    /// Invokes <paramref name="commandId"/> through the canonical
    /// surface path — <see cref="ICommandRegistry.Evaluate"/> then
    /// <see cref="ICommandRegistry.InvokeAsync(string, CommandContext, CommandParameterPrompt?, CancellationToken)"/>
    /// — fire-and-forget from <paramref name="provider"/>'s own synchronous
    /// event.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>`WP-A2`: this used the obsolete Id-only overload.</b> That overload
    /// throws <see cref="CommandException"/> for every descriptor without a
    /// <c>CreateDefault</c> — all 74 production discipline commands — and the
    /// throw landed in the <see langword="catch"/> below, where it became a
    /// log line. The key appeared to do nothing. The path was allow-listed as
    /// dormant only because nothing was ever bound to it; binding anything
    /// would have made a real defect visible. It now asks the same question
    /// the Ribbon and the Palette ask, and reports the same answers.
    /// </para>
    /// <para>
    /// A command that is unavailable, or that needs input nobody can supply,
    /// or that the user cancels, is <i>logged as what it is</i> — never
    /// thrown, never silently dropped, and never run against a fabricated
    /// context. The <see langword="catch"/> stays: a handler that throws is a
    /// defect (`ADR-0038`), and one misbehaving provider must not crash every
    /// other bound input source (`ADR-0028`).
    /// </para>
    /// </remarks>
    private async void RouteAsync(IInputBindingProvider provider, string commandId)
    {
        try
        {
            var context = ContextSource?.Invoke() ?? CommandContext.Empty;
            var availability = _commandRegistry.Evaluate(commandId, context);

            if (!availability.IsAvailable)
            {
                _logger?.Information(
                    $"Input binding provider '{provider.SourceName}' requested command '{commandId}', which is not available: {availability.Reason}");
                return;
            }

            var invocation = await _commandRegistry
                .InvokeAsync(commandId, context, ParameterPrompt)
                .ConfigureAwait(false);

            switch (invocation.Outcome)
            {
                case CommandOutcome.Executed:
                    _logger?.Information(
                        $"Input binding provider '{provider.SourceName}' invoked command '{commandId}': "
                        + (invocation.Result!.Succeeded ? "succeeded." : $"failed. {invocation.Result.Message}"));
                    break;

                case CommandOutcome.Cancelled:
                    _logger?.Information(
                        $"Input binding provider '{provider.SourceName}' requested command '{commandId}', which was cancelled.");
                    break;

                default:
                    _logger?.Information(
                        $"Input binding provider '{provider.SourceName}' requested command '{commandId}', which is not available: {invocation.Reason}");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"Input binding provider '{provider.SourceName}' requested command '{commandId}', which failed.", ex);
        }
    }
}
