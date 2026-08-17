namespace Tempest.Core.Plugins;

/// <summary>
/// The read side of the small, Host-owned registry recording every
/// discovered <see cref="Modules.IModule"/> or <see cref="BackgroundServices.IHostedService"/>
/// <see cref="Type"/> that belonged to a plugin
/// <see cref="PluginAssemblyLoader.LoadPlugins"/> denied trust.
/// </summary>
/// <remarks>
/// Exists so <c>TempestHost</c> can filter both Module Discovery's and
/// Hosted Service Discovery's own returned types, before Module
/// Registration or Hosted Service Registration ever run, excluding every
/// type this registry recorded — the WP 13.9.4 execution boundary. Mirrors
/// <see cref="IPluginComponentPrincipalRegistry"/>'s own read/write split
/// exactly: callers within <c>Tempest.Core.Plugins</c>
/// (<see cref="PluginAssemblyLoader"/>) record entries through
/// <see cref="IPluginDeniedTypeRecorder"/>; everything else — in practice,
/// only <c>TempestHost</c>'s own Module Registration and Hosted Service
/// Registration filters — observes through this interface. Host-owned,
/// never added to the <c>ServiceCollection</c>, never resolvable by a
/// module or plugin (ADR-0017).
/// </remarks>
public interface IPluginDeniedTypeRegistry
{
    /// <summary>
    /// Reports whether <paramref name="type"/> was recorded as belonging to
    /// a plugin denied trust — <see langword="true"/> means it must never
    /// reach Module Registration or Hosted Service Registration.
    /// </summary>
    /// <param name="type">The discovered type to check.</param>
    bool IsDenied(Type type);
}
