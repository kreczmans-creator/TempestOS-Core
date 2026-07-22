namespace Tempest.Core.DependencyInjection;

/// <summary>
/// Describes how long a resolved service instance is kept alive by an
/// <see cref="ITempestServiceProvider"/>.
/// </summary>
/// <remarks>
/// Only two lifetimes are supported. There is deliberately no "Scoped" lifetime —
/// TempestOS has no notion of a request or unit-of-work scope today, and adding one
/// without a concrete consumer would be speculative.
/// </remarks>
public enum ServiceLifetime
{
    /// <summary>
    /// Exactly one instance is created; every subsequent resolution of the same
    /// service type returns that same instance.
    /// </summary>
    Singleton,

    /// <summary>
    /// A new instance is created for every resolution.
    /// </summary>
    Transient
}
