namespace Tempest.Core.Versioning;

/// <summary>
/// Provides the single, authoritative version of the running platform,
/// queryable from anywhere in the application.
/// </summary>
/// <remarks>
/// A Platform API (ADR-0023) — the contract; <see cref="PlatformVersionProvider"/>
/// is the Platform Service that implements it. DI-public, resolved via
/// ordinary constructor injection like <see cref="Configuration.IConfigurationProvider"/>
/// and <see cref="Logging.ILogger"/> — never a Host-owned collaborator, since
/// it orchestrates nothing and carries no authority over any other
/// component.
/// </remarks>
public interface IPlatformVersionProvider
{
    /// <summary>
    /// Gets the running platform's own version, resolved once and cached
    /// for the life of this provider instance.
    /// </summary>
    PlatformVersion Version { get; }
}
