namespace Tempest.Core.BackgroundServices;

/// <summary>
/// Discovers <see cref="IHostedService"/> implementations by scanning assemblies with
/// reflection.
/// </summary>
/// <remarks>
/// <para>
/// A Host-owned collaborator (ADR-0017, applied to this new component per ADR-0029) —
/// never registered into the dependency injection container. Mirrors
/// <see cref="Modules.IFrameworkDiscoveryService"/>'s own shape, but for a candidate
/// contract that carries no metadata: unlike <see cref="Modules.IModule"/>, a hosted
/// service has no <c>Id</c>/<c>Name</c>/<c>Version</c> to read, so discovery never needs
/// to instantiate a candidate at all — it only needs to identify which types implement
/// <see cref="IHostedService"/>. See ADR-0029 and <c>Background Services
/// Architecture.md</c> for the complete design.
/// </para>
/// </remarks>
public interface IHostedServiceDiscoveryService
{
    /// <summary>
    /// Discovers every concrete <see cref="IHostedService"/> implementation across the
    /// scanned assemblies, in deterministic order.
    /// </summary>
    /// <returns>
    /// The discovered hosted service types, ordered ascending, ordinal, by
    /// <see cref="Type.FullName"/> — the deterministic ordering key a hosted service
    /// has, since it carries no <c>Id</c> to sort by.
    /// </returns>
    IReadOnlyList<Type> DiscoverHostedServiceTypes();
}
