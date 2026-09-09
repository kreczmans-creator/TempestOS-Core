namespace Tempest.Core.Licensing;

/// <summary>
/// The read-only, post-validation view of the current license — DI-
/// public, registered via <c>AddInstance</c> once validation succeeds.
/// </summary>
/// <remarks>
/// Exposes capability only — it answers "is this capability enabled,"
/// nothing more. Deciding what a consuming module does with that answer,
/// including any commercial policy behind why a capability is or is not
/// enabled, is deliberately outside this interface's own responsibility.
/// </remarks>
public interface ILicenseProvider
{
    /// <summary>The current, already-validated license.</summary>
    ILicense CurrentLicense { get; }

    /// <summary>
    /// Gets whether <paramref name="capability"/> is enabled by the
    /// current license. Safe for concurrent calls — the underlying
    /// <see cref="ILicense"/> is immutable for the life of the running
    /// process.
    /// </summary>
    /// <param name="capability">The capability key to check.</param>
    /// <exception cref="ArgumentException"><paramref name="capability"/> is <see langword="null"/>, empty, or whitespace.</exception>
    bool HasCapability(string capability);
}
