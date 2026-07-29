namespace Tempest.Core.Navigation;

/// <summary>
/// Thrown when <see cref="INavigationProvider.Register"/> is called with an
/// item whose <see cref="NavigationItem.Id"/> is already registered.
/// </summary>
/// <remarks>
/// Because registration happens inside a module's own
/// <c>InitialiseAsync</c>/<c>StartAsync</c>, this exception is caught and
/// isolated by <see cref="Modules.ModuleLifecycleManager"/>'s existing,
/// unmodified per-module isolation (ADR-0013) — no new Host-level failure
/// policy is needed for Navigation at all (ADR-0032).
/// </remarks>
public sealed class DuplicateNavigationItemException : NavigationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateNavigationItemException"/> class.
    /// </summary>
    /// <param name="id">The navigation item ID that was already registered.</param>
    public DuplicateNavigationItemException(string id)
        : base($"A navigation item with ID '{id}' is already registered.")
    {
        Id = id;
    }

    /// <summary>
    /// Gets the navigation item ID that was already registered.
    /// </summary>
    public string Id { get; }
}
