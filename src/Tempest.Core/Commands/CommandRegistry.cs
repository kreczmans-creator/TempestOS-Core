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

        var command = descriptor.CreateDefault();

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
