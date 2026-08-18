namespace Tempest.Core.Plugins;

/// <summary>
/// The one concrete Plugin Registry, implementing both its read
/// (<see cref="IPluginRegistry"/>) and write (<see cref="IPluginRegistryRecorder"/>)
/// sides.
/// </summary>
/// <remarks>
/// <para>
/// Host-owned — constructed and held directly by <c>TempestHost</c>, never
/// added to the <c>ServiceCollection</c>, never resolvable by a module or
/// plugin (ADR-0017, applied to a fourth Host-owned collaborator). Callers
/// within <c>Tempest.Core.Plugins</c> record outcomes through
/// <see cref="IPluginRegistryRecorder"/>; everything else observes through
/// <see cref="IPluginRegistry"/>, typically via <c>IDiagnosticsProvider.Plugins</c>.
/// </para>
/// <para>
/// Thread-safety here is defensive, not load-bearing — Plugin Discovery and
/// Plugin Loading are both synchronous, single-threaded phases (3.1/3.2), so
/// nothing ever writes to this registry concurrently with anything else. But
/// <see cref="Entries"/> may be read concurrently by a caller
/// (<c>IDiagnosticsProvider.Plugins</c>) while the Host itself has moved past
/// Discovery/Loading and is fully <c>Running</c> — so a lock is the honest
/// choice here, mirroring <c>TempestHost</c>'s own <c>_gate</c> convention for
/// its <c>State</c> property.
/// </para>
/// </remarks>
public sealed class PluginRegistry : IPluginRegistry, IPluginRegistryRecorder
{
    private readonly List<PluginRegistryEntry> _entries = new();
    private readonly object _gate = new();

    /// <inheritdoc />
    public IReadOnlyCollection<PluginRegistryEntry> Entries
    {
        get
        {
            lock (_gate)
                return _entries.ToArray();
        }
    }

    /// <inheritdoc />
    public void Record(PluginRegistryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
            _entries.Add(entry);
    }
}
