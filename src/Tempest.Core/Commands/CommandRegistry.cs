using Tempest.Core.Logging;

namespace Tempest.Core.Commands;

/// <summary>
/// The concrete <see cref="ICommandRegistry"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Registered items are held in a single, lock-guarded dictionary keyed by
/// <see cref="CommandDescriptor.Id"/>, mirroring
/// <see cref="Navigation.NavigationService"/>'s own pattern exactly.
/// Delegates dispatch to the shared <see cref="CommandHandlerTable"/> — see
/// that type's own remarks for why this is necessary.
/// </para>
/// <para>
/// <b>Trust-ordered registration (ADR-0111, WP 13.2A).</b> A second,
/// lock-guarded dictionary tracks each registered descriptor's own owning
/// component principal (<see langword="null"/> = first-party), maintained in
/// lockstep with <c>_descriptorsById</c>. <see cref="RegisterDescriptor"/>:
/// if the registrant is non-null, it must hold <c>plugin.commands.register</c>
/// (<see cref="Identity.IPermissionEvaluator.RequirePermission"/> — throws
/// <see cref="Identity.PermissionDeniedException"/> if not, propagating
/// uncaught). If the Id is new, it is simply added, exactly as today. If the
/// Id already has a descriptor: a <i>higher</i>-trust-tier registrant
/// (<see cref="Plugins.PluginTrustPermission.Rank"/>) evicts and replaces
/// the existing entry, logged loudly at <see cref="LogLevel.Warning"/> —
/// never silent; an <i>equal-or-lower</i>-tier registrant is rejected
/// exactly as today (<see cref="DuplicateCommandIdException"/>). No
/// <c>Unregister</c> exists here (ADR-0037's own "no Unregister/Deregister
/// is defined" — this Work Package does not add one). This is a real,
/// acknowledged, additive revision of <c>ADR-0037</c>'s own unconditional
/// duplicate-rejection behaviour — see <c>ADR-0111</c>'s own Decision
/// section for the full acknowledgement.
/// </para>
/// </remarks>
public sealed class CommandRegistry : ICommandRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CommandDescriptor> _descriptorsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Identity.IPrincipal?> _ownerById = new(StringComparer.Ordinal);
    private readonly CommandHandlerTable _table;
    private readonly ILogger? _logger;
    private readonly Identity.ICurrentComponentAccessor? _currentComponentAccessor;
    private readonly Identity.IPermissionEvaluator? _permissionEvaluator;

    /// <summary>
    /// Initialises a new instance of the <see cref="CommandRegistry"/> class.
    /// </summary>
    /// <param name="table">The shared handler table this registry dispatches through.</param>
    /// <param name="logger">
    /// An optional logger used to record registration and invocation
    /// activity via the logging abstraction. May be <see langword="null"/>
    /// if logging is not required.
    /// </param>
    /// <param name="currentComponentAccessor">
    /// An optional accessor resolving which loaded component's own code is
    /// currently registering a descriptor (ADR-0111). <see langword="null"/>
    /// — the default — reproduces today's exact unconditional behaviour.
    /// </param>
    /// <param name="permissionEvaluator">
    /// An optional evaluator used to enforce <c>plugin.commands.register</c>
    /// (ADR-0111). <see langword="null"/> — the default — no-ops the check.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="table"/> is <see langword="null"/>.</exception>
    public CommandRegistry(
        CommandHandlerTable table,
        ILogger? logger = null,
        Identity.ICurrentComponentAccessor? currentComponentAccessor = null,
        Identity.IPermissionEvaluator? permissionEvaluator = null)
    {
        ArgumentNullException.ThrowIfNull(table);

        _table = table;
        _logger = logger;
        _currentComponentAccessor = currentComponentAccessor;
        _permissionEvaluator = permissionEvaluator;
    }

    /// <inheritdoc />
    public void RegisterDescriptor(CommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var registrant = _currentComponentAccessor?.Current;

        if (!Plugins.PluginTrustPermission.IsFirstParty(registrant))
            _permissionEvaluator?.RequirePermission(registrant!, new Identity.Permission(Plugins.PluginCapability.Commands));

        lock (_gate)
        {
            if (!_descriptorsById.ContainsKey(descriptor.Id))
            {
                _descriptorsById.Add(descriptor.Id, descriptor);
                _ownerById.Add(descriptor.Id, registrant);
            }
            else
            {
                var existingOwner = _ownerById[descriptor.Id];

                if (Plugins.PluginTrustPermission.Rank(registrant) <= Plugins.PluginTrustPermission.Rank(existingOwner))
                    throw new DuplicateCommandIdException(descriptor.Id);

                _logger?.Warning(
                    $"Command descriptor '{descriptor.Id}' ownership override: " +
                    $"'{existingOwner?.Identity.Id ?? "(first-party)"}' -> '{registrant?.Identity.Id ?? "(first-party)"}'.");

                _descriptorsById[descriptor.Id] = descriptor;
                _ownerById[descriptor.Id] = registrant;
            }
        }

        _logger?.Information($"Command descriptor registered: '{descriptor.Id}' ({descriptor.DisplayName}).");
    }

    /// <inheritdoc />
    public IReadOnlyList<CommandDescriptor> Items
    {
        get
        {
            lock (_gate)
            {
                return _descriptorsById.Values
                    .OrderBy(descriptor => descriptor.Category, StringComparer.Ordinal)
                    .ThenBy(descriptor => descriptor.Id, StringComparer.Ordinal)
                    .ToList();
            }
        }
    }

    /// <inheritdoc />
    public async Task<CommandResult> InvokeAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        CommandDescriptor descriptor;

        lock (_gate)
        {
            if (!_descriptorsById.TryGetValue(id, out var found))
                throw new CommandNotFoundException(id);

            descriptor = found;
        }

        if (descriptor.CreateDefault is null)
        {
            throw new CommandException(
                $"Command '{id}' has no default-instance factory and cannot be invoked by Id.");
        }

        _logger?.Information($"Invoking command '{id}'.");

        return await DispatchAsync(id, descriptor.CreateDefault(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public CommandAvailability Evaluate(string id, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);

        var descriptor = Find(id);

        if (descriptor is null)
            return CommandAvailability.Blocked($"No command '{id}' is registered.");

        return Evaluate(descriptor, context);
    }

    /// <inheritdoc />
    public async Task<CommandInvocation> InvokeAsync(
        string id,
        CommandContext context,
        CommandParameterPrompt? prompt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);

        // An Id nobody registered is the one case that still throws:
        // Evaluate answers "may I offer this?", which is fair to ask about
        // anything, but invoking a command that does not exist is the same
        // programming error the Id-only overload has always reported.
        var descriptor = Find(id) ?? throw new CommandNotFoundException(id);

        // The single availability implementation, consulted rather than
        // re-derived. Everything below this line assumes only what
        // Evaluate has already established.
        var availability = Evaluate(descriptor, context);

        if (!availability.IsAvailable)
        {
            _logger?.Information($"Command '{id}' not invoked: {availability.Reason}");
            return CommandInvocation.Unavailable(availability.Reason!);
        }

        var binding = descriptor.Binding;

        // No binding, but a default-instance factory: the pre-binding path,
        // reached identically here. Genuinely valid, because a command that
        // needs no caller-supplied data needs nothing from a context either
        // - this is what keeps a macro's own steps working unchanged.
        if (binding is null)
        {
            _logger?.Information($"Invoking command '{id}'.");
            return CommandInvocation.Executed(
                await DispatchAsync(id, descriptor.CreateDefault!(), cancellationToken).ConfigureAwait(false));
        }

        IReadOnlyDictionary<string, string> values;

        if (binding.RequiresPrompt)
        {
            if (prompt is null)
            {
                // Never a silent no-op: a command needing input, invoked
                // with nothing able to ask for it, says exactly that.
                var reason =
                    $"'{descriptor.DisplayName}' needs additional input, and no input surface was supplied.";
                _logger?.Information($"Command '{id}' not invoked: {reason}");
                return CommandInvocation.Unavailable(reason);
            }

            var collected = await prompt(descriptor, binding.Parameters, binding.ConfirmationMessage, cancellationToken)
                .ConfigureAwait(false);

            // Declining is not failing. Nothing ran; nothing is reported.
            if (collected is null)
            {
                _logger?.Information($"Command '{id}' cancelled before dispatch.");
                return CommandInvocation.Cancelled;
            }

            if (CheckValues(descriptor, binding, collected) is { } invalid)
            {
                _logger?.Information($"Command '{id}' not invoked: {invalid}");
                return CommandInvocation.Unavailable(invalid);
            }

            values = collected;
        }
        else
        {
            values = EmptyValues;
        }

        // A throw out of Build is a defect in the binding - it was handed a
        // context its own declared requirements said was sufficient - so it
        // is logged and propagated, never converted into an outcome.
        ICommand command;

        try
        {
            command = binding.Build(context, values);
        }
        catch (Exception ex)
        {
            _logger?.Error($"Command '{id}' binding failed to construct its command.", ex);
            throw;
        }

        _logger?.Information($"Invoking command '{id}'.");

        return CommandInvocation.Executed(await DispatchAsync(id, command, cancellationToken).ConfigureAwait(false));
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyValues =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private CommandDescriptor? Find(string id)
    {
        lock (_gate)
        {
            return _descriptorsById.TryGetValue(id, out var found) ? found : null;
        }
    }

    /// <summary>
    /// The whole of command availability, in one place, in the order a
    /// person would want to be told about it: what does not exist, then
    /// what was never wired, then what the current selection cannot
    /// satisfy, then the command's own opinion.
    /// </summary>
    private static CommandAvailability Evaluate(CommandDescriptor descriptor, CommandContext context)
    {
        var binding = descriptor.Binding;

        // Declared unavailability wins over everything: a command that
        // cannot be built has no useful answer to "is the selection right".
        if (binding is { IsInvocable: false })
            return CommandAvailability.Blocked(binding.UnavailableReason!);

        if (binding is null && descriptor.CreateDefault is null)
        {
            return CommandAvailability.Blocked(
                $"'{descriptor.DisplayName}' has no binding and cannot be invoked by Id.");
        }

        // A CreateDefault-only descriptor declares no requirements, so
        // there is nothing about the context left to check.
        if (binding is not null)
        {
            if (binding.Requires.HasFlag(CommandContextRequirement.SelectedObject) && context.Primary is null)
                return CommandAvailability.Blocked($"'{descriptor.DisplayName}' needs a selected object.");

            if (binding.AppliesToKinds is { } kinds
                && context.Primary is { } primary
                && !kinds.Contains(primary.Kind, StringComparer.Ordinal))
            {
                return CommandAvailability.Blocked(
                    $"'{descriptor.DisplayName}' does not apply to a {primary.Kind}. " +
                    $"It applies to: {string.Join(", ", kinds)}.");
            }

            // Refused rather than silently applied to the first item only.
            if (context.Selection.Count > 1 && !binding.Requires.HasFlag(CommandContextRequirement.MultipleAllowed))
                return CommandAvailability.Blocked($"'{descriptor.DisplayName}' applies to one object at a time.");
        }

        // The command's own last word, kept as the final gate - the seam a
        // future permission model plugs into (ADR-0037's own deferral).
        if (descriptor.CanExecute is { } canExecute && !canExecute())
            return CommandAvailability.Blocked($"'{descriptor.DisplayName}' is not currently available.");

        return CommandAvailability.Available;
    }

    /// <summary>
    /// Checks the collected values against what the binding declared -
    /// a value-level question <see cref="Evaluate(CommandDescriptor, CommandContext)"/>
    /// is never given the values to answer.
    /// </summary>
    private static string? CheckValues(
        CommandDescriptor descriptor, CommandBinding binding, IReadOnlyDictionary<string, string> values)
    {
        foreach (var parameter in binding.Parameters)
        {
            if (!values.TryGetValue(parameter.Name, out var value))
                return $"'{descriptor.DisplayName}' needs a value for '{parameter.Label}'.";

            if (parameter.Check(value) is { } problem)
                return $"'{descriptor.DisplayName}': {problem}";
        }

        return null;
    }

    /// <summary>
    /// The dispatch-and-report tail both <see cref="InvokeAsync(string, CancellationToken)"/>
    /// and its context-aware overload share, extracted verbatim so the two
    /// paths cannot drift in what they log or in how a handler's own
    /// exception is treated (<c>ADR-0038</c>: logged, then rethrown
    /// uncaught).
    /// </summary>
    private async Task<CommandResult> DispatchAsync(string id, ICommand command, CancellationToken cancellationToken)
    {
        CommandResult result;

        try
        {
            result = await _table.DispatchAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.Error($"Command '{id}' handler threw.", ex);
            throw;
        }

        if (result.Succeeded)
            _logger?.Information($"Command '{id}' invoked: Succeeded.");
        else
            _logger?.Warning($"Command '{id}' invoked: Failed ({result.Message}).");

        return result;
    }
}
