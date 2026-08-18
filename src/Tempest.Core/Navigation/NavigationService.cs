using Tempest.Core.Events;
using Tempest.Core.Logging;

namespace Tempest.Core.Navigation;

/// <summary>
/// The concrete <see cref="INavigationProvider"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Registered items are held in a single, lock-guarded dictionary keyed by
/// <see cref="NavigationItem.Id"/>, mirroring <see cref="Events.EventBus"/>'s
/// own <c>_gate</c> pattern. Registration and removal are imperative — a
/// module or plugin-loaded module constructor-injects
/// <see cref="INavigationProvider"/> and calls <see cref="Register"/> from
/// its own <c>InitialiseAsync</c>/<c>StartAsync</c>, exactly as it may call
/// <see cref="IEventBus.Subscribe{TEvent}"/> (ADR-0032).
/// </para>
/// <para>
/// <see cref="Navigate"/> validates the requested ID is registered, then
/// publishes a <see cref="NavigationRequestedEvent"/> through the
/// constructor-injected <see cref="IEventBus"/> — a platform-service-to-
/// platform-service dependency with direct precedent
/// (<see cref="Logging.LoggerFactory"/> → <see cref="Configuration.IConfigurationProvider"/>),
/// introducing no cycle. This service tracks no notion of "current
/// location" — that is rendering state, owned entirely by whatever is
/// rendering. See ADR-0031, ADR-0032, and <c>Navigation Framework
/// Architecture.md</c> for the complete design.
/// </para>
/// <para>
/// <b>Trust-ordered registration and ownership (ADR-0111, WP 13.2A).</b> A
/// second, lock-guarded dictionary tracks each registered item's own owning
/// component principal (<see langword="null"/> = first-party), maintained in
/// lockstep with <c>_itemsById</c> at every mutation point. <see cref="Register"/>:
/// if the registrant (<see cref="Identity.ICurrentComponentAccessor.Current"/>)
/// is non-null, it must hold <c>plugin.navigation.register</c>
/// (<see cref="Identity.IPermissionEvaluator.RequirePermission"/> — throws
/// <see cref="Identity.PermissionDeniedException"/> if not, propagating
/// uncaught, isolated by <see cref="Modules.ModuleLifecycleManager"/>'s
/// existing per-module isolation exactly as any other exception a module's
/// own <c>InitialiseAsync</c> throws already is). If the Id is new, it is
/// simply added, exactly as today. If the Id already has an owner: a
/// <i>higher</i>-trust-tier registrant (<see cref="Plugins.PluginTrustPermission.Rank"/>)
/// evicts and replaces the existing entry, logged loudly at
/// <see cref="LogLevel.Warning"/> — never silent; an <i>equal-or-lower</i>-tier
/// registrant is rejected exactly as today
/// (<see cref="DuplicateNavigationItemException"/>). This is a real,
/// acknowledged, additive revision of <c>ADR-0032</c>'s own unconditional
/// duplicate-rejection behaviour — see <c>ADR-0111</c>'s own Decision section
/// for the full acknowledgement. <see cref="Unregister"/>: a <see langword="null"/>
/// or First-Party caller (<see cref="Plugins.PluginTrustPermission.IsFirstParty"/>)
/// proceeds unconditionally, exactly as today — the check is <i>skipped</i>,
/// not merely satisfied, for every actor that exists today. A non-first-party
/// caller removing its <i>own</i> item also proceeds. A non-first-party
/// caller removing someone <i>else's</i> item requires the reserved
/// <c>navigation.unregister.any</c> permission — held by no principal this
/// Work Package's own capability-grant logic ever grants — so this always
/// denies. <b>Deliberate exception to the "null collaborator reproduces
/// today's behaviour" rule used everywhere else in this file:</b> when
/// <c>permissionEvaluator</c> is unwired (<see langword="null"/>) and this
/// mismatched-ownership branch is reached, the removal is still denied, not
/// silently allowed — today's old, unconditional-success behaviour is
/// exactly the <c>TD-10</c> gap this Work Package exists to close, so a
/// missing collaborator must not resurrect it.
/// </para>
/// </remarks>
public sealed class NavigationService : INavigationProvider
{
    private readonly object _gate = new();
    private readonly Dictionary<string, NavigationItem> _itemsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Identity.IPrincipal?> _ownerById = new(StringComparer.Ordinal);
    private readonly IEventBus _eventBus;
    private readonly ILogger? _logger;
    private readonly Identity.ICurrentComponentAccessor? _currentComponentAccessor;
    private readonly Identity.IPermissionEvaluator? _permissionEvaluator;

    /// <summary>
    /// Initialises a new instance of the <see cref="NavigationService"/> class.
    /// </summary>
    /// <param name="eventBus">
    /// The Event Bus this service publishes <see cref="NavigationRequestedEvent"/>
    /// through, resolved via ordinary constructor injection.
    /// </param>
    /// <param name="logger">
    /// An optional logger used to record registration and navigation activity
    /// via the logging abstraction. May be <see langword="null"/> if logging
    /// is not required.
    /// </param>
    /// <param name="currentComponentAccessor">
    /// An optional accessor resolving which loaded component's own code is
    /// currently registering/unregistering an item (ADR-0111). <see langword="null"/>
    /// — the default — reproduces today's exact unconditional behaviour: every
    /// check that reads it treats a <see langword="null"/> accessor identically
    /// to a <see langword="null"/> current component (first-party).
    /// </param>
    /// <param name="permissionEvaluator">
    /// An optional evaluator used to enforce <c>plugin.navigation.register</c>/
    /// the reserved <c>navigation.unregister.any</c> override permission
    /// (ADR-0111). <see langword="null"/> — the default — no-ops every check
    /// this collaborator would otherwise perform, <b>except</b> the
    /// mismatched-ownership branch of <see cref="Unregister"/> — see this
    /// type's own remarks.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="eventBus"/> is <see langword="null"/>.</exception>
    public NavigationService(
        IEventBus eventBus,
        ILogger? logger = null,
        Identity.ICurrentComponentAccessor? currentComponentAccessor = null,
        Identity.IPermissionEvaluator? permissionEvaluator = null)
    {
        ArgumentNullException.ThrowIfNull(eventBus);

        _eventBus = eventBus;
        _logger = logger;
        _currentComponentAccessor = currentComponentAccessor;
        _permissionEvaluator = permissionEvaluator;
    }

    /// <inheritdoc />
    public void Register(NavigationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var registrant = _currentComponentAccessor?.Current;

        if (!Plugins.PluginTrustPermission.IsFirstParty(registrant))
            _permissionEvaluator?.RequirePermission(registrant!, new Identity.Permission(Plugins.PluginCapability.Navigation));

        lock (_gate)
        {
            if (!_itemsById.ContainsKey(item.Id))
            {
                _itemsById.Add(item.Id, item);
                _ownerById.Add(item.Id, registrant);
            }
            else
            {
                var existingOwner = _ownerById[item.Id];

                if (Plugins.PluginTrustPermission.Rank(registrant) <= Plugins.PluginTrustPermission.Rank(existingOwner))
                    throw new DuplicateNavigationItemException(item.Id);

                _logger?.Warning(
                    $"Navigation item '{item.Id}' ownership override: " +
                    $"'{existingOwner?.Identity.Id ?? "(first-party)"}' -> '{registrant?.Identity.Id ?? "(first-party)"}'.");

                _itemsById[item.Id] = item;
                _ownerById[item.Id] = registrant;
            }
        }

        _logger?.Information($"Navigation item '{item.Id}' registered.");
    }

    /// <inheritdoc />
    public void Unregister(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        bool removed;

        lock (_gate)
        {
            if (!_itemsById.ContainsKey(id))
                return;

            var caller = _currentComponentAccessor?.Current;

            if (!Plugins.PluginTrustPermission.IsFirstParty(caller))
            {
                var storedOwner = _ownerById[id];

                if (caller!.Identity.Id != storedOwner?.Identity.Id)
                {
                    if (_permissionEvaluator is not null)
                    {
                        _permissionEvaluator.RequirePermission(caller, new Identity.Permission("navigation.unregister.any"));
                    }
                    else
                    {
                        // Deliberate exception to the "null collaborator
                        // reproduces today's behaviour" rule used everywhere
                        // else in this class - see this type's own remarks.
                        throw new Identity.PermissionDeniedException(
                            caller, new Identity.Permission("navigation.unregister.any"));
                    }
                }
            }

            removed = _itemsById.Remove(id);
            _ownerById.Remove(id);
        }

        if (removed)
            _logger?.Information($"Navigation item '{id}' unregistered.");
    }

    /// <inheritdoc />
    public IReadOnlyList<NavigationItem> Items
    {
        get
        {
            lock (_gate)
            {
                return _itemsById.Values
                    .OrderBy(item => item.Group, StringComparer.Ordinal)
                    .ThenBy(item => item.Order)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .ToList();
            }
        }
    }

    /// <inheritdoc />
    public async Task Navigate(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        NavigationItem item;

        lock (_gate)
        {
            if (!_itemsById.TryGetValue(id, out var found))
                throw new NavigationItemNotFoundException(id);

            item = found;
        }

        _logger?.Information($"Navigation requested to '{id}'.");

        await _eventBus.PublishAsync(new NavigationRequestedEvent(item), cancellationToken).ConfigureAwait(false);
    }
}
