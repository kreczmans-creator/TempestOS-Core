namespace Tempest.Core.Logging;

/// <summary>
/// Creates named <see cref="ILogger"/> instances.
/// </summary>
/// <remarks>
/// The category name a logger is created with (for example, "Discovery",
/// "Configuration", "Runtime") identifies which part of the system a message
/// came from; it carries no other meaning and is never parsed or interpreted.
/// </remarks>
public interface ILoggerFactory
{
    /// <summary>
    /// Creates a logger for the given category.
    /// </summary>
    /// <param name="category">The category to create a logger for.</param>
    /// <returns>An <see cref="ILogger"/> for <paramref name="category"/>.</returns>
    ILogger CreateLogger(string category);
}
