namespace Tempest.Core.Plugins;

/// <summary>
/// The one concrete component principal registry, implementing both its read
/// (<see cref="IPluginComponentPrincipalRegistry"/>) and write
/// (<see cref="IPluginComponentPrincipalRecorder"/>) sides.
/// </summary>
/// <remarks>
/// <para>
/// Host-owned — constructed and held directly by <c>TempestHost</c>, never
/// added to the <c>ServiceCollection</c>, never resolvable by a module or
/// plugin (ADR-0017), mirroring <see cref="PluginRegistry"/>'s own identical
/// boundary exactly.
/// </para>
/// <para>
/// Thread-safety here is defensive, not load-bearing — every write happens
/// during the single-threaded Plugin Loading phase (3.2), strictly before
/// any read (<c>TempestHost</c>'s own <c>componentScopeProvider</c> closure,
/// invoked only once Module Lifecycle begins, well after Loading
/// completes). A lock is used anyway, mirroring <see cref="PluginRegistry"/>'s
/// own <c>_gate</c> convention, since nothing about this type's own contract
/// promises a caller it will only ever be read after every write has
/// finished.
/// </para>
/// </remarks>
public sealed class PluginComponentPrincipalRegistry : IPluginComponentPrincipalRegistry, IPluginComponentPrincipalRecorder
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, Identity.IPrincipal> _principalsByModuleType = new();

    /// <inheritdoc />
    public void Record(Type moduleType, Identity.IPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(moduleType);
        ArgumentNullException.ThrowIfNull(principal);

        lock (_gate)
            _principalsByModuleType[moduleType] = principal;
    }

    /// <inheritdoc />
    public Identity.IPrincipal? GetPrincipalFor(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);

        lock (_gate)
            return _principalsByModuleType.TryGetValue(moduleType, out var principal) ? principal : null;
    }
}
