namespace Tempest.Core.Plugins;

/// <summary>
/// The read side of the Plugin Registry — the queryable catalogue of every
/// plugin candidate a run attempted, and its outcome.
/// </summary>
/// <remarks>
/// Host-owned — never DI-public. Mirrors <see cref="Modules.IRuntimeModuleManager"/>'s
/// own ADR-0017 boundary exactly: a module able to reach this directly could,
/// in principle, be given write access to it later by a careless future
/// change, or could be mistaken for a legitimate place to drive plugin
/// loading rather than merely observe its outcome. This interface is exposed
/// to modules only indirectly, via <c>IDiagnosticsProvider.Plugins</c>
/// (ADR-0039) — a projection, not this interface itself.
/// </remarks>
public interface IPluginRegistry
{
    /// <summary>
    /// Gets every plugin candidate outcome recorded so far this run.
    /// </summary>
    IReadOnlyCollection<PluginRegistryEntry> Entries { get; }
}
