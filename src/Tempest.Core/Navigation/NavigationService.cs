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
/// </remarks>
public sealed class NavigationService : INavigationProvider
{
    private readonly object _gate = new();
    private readonly Dictionary<string, NavigationItem> _itemsById = new(StringComparer.Ordinal);
    private readonly IEventBus _eventBus;
    private readonly ILogger? _logger;

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
    /// <exception cref="ArgumentNullException"><paramref name="eventBus"/> is <see langword="null"/>.</exception>
    public NavigationService(IEventBus eventBus, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(eventBus);

        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Register(NavigationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_gate)
        {
            if (_itemsById.ContainsKey(item.Id))
                throw new DuplicateNavigationItemException(item.Id);

            _itemsById.Add(item.Id, item);
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
            removed = _itemsById.Remove(id);
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
