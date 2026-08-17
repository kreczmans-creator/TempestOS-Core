namespace Tempest.Core.Plugins;

/// <summary>
/// The one concrete denied-type registry, implementing both its read
/// (<see cref="IPluginDeniedTypeRegistry"/>) and write
/// (<see cref="IPluginDeniedTypeRecorder"/>) sides.
/// </summary>
/// <remarks>
/// <para>
/// Host-owned — constructed and held directly by <c>TempestHost</c>, never
/// added to the <c>ServiceCollection</c>, never resolvable by a module or
/// plugin (ADR-0017), mirroring <see cref="PluginComponentPrincipalRegistry"/>'s
/// own identical boundary exactly.
/// </para>
/// <para>
/// Thread-safety here is defensive, not load-bearing — every write happens
/// during the single-threaded Plugin Loading phase (3.2), strictly before
/// any read (<c>TempestHost</c>'s own Module Registration and Hosted
/// Service Registration filters, applied only once Module Discovery and
/// Hosted Service Discovery complete, after Loading finishes). A lock is
/// used anyway, mirroring <see cref="PluginComponentPrincipalRegistry"/>'s
/// own <c>_gate</c> convention, since nothing about this type's own
/// contract promises a caller it will only ever be read after every write
/// has finished.
/// </para>
/// </remarks>
public sealed class PluginDeniedTypeRegistry : IPluginDeniedTypeRegistry, IPluginDeniedTypeRecorder
{
    private readonly object _gate = new();
    private readonly HashSet<Type> _deniedTypes = [];

    /// <inheritdoc />
    public void Record(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        lock (_gate)
            _deniedTypes.Add(type);
    }

    /// <inheritdoc />
    public bool IsDenied(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        lock (_gate)
            return _deniedTypes.Contains(type);
    }
}
