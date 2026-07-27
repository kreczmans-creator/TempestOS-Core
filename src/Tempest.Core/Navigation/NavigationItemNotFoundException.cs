namespace Tempest.Core.Navigation;

/// <summary>
/// Thrown when <see cref="INavigationProvider.Navigate"/> is called with an
/// ID that has not been registered.
/// </summary>
/// <remarks>
/// This is application logic's own error to handle (a caller navigating to
/// a stale or mistyped ID), not a Host-level concern, since
/// <see cref="NavigationService"/> is never part of Host orchestration.
/// </remarks>
public sealed class NavigationItemNotFoundException : NavigationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="NavigationItemNotFoundException"/> class.
    /// </summary>
    /// <param name="id">The navigation item ID that was not found.</param>
    public NavigationItemNotFoundException(string id)
        : base($"No navigation item is registered with ID '{id}'.")
    {
        Id = id;
    }

    /// <summary>
    /// Gets the navigation item ID that was not found.
    /// </summary>
    public string Id { get; }
}
