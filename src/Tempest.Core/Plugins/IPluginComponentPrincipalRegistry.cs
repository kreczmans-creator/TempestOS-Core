namespace Tempest.Core.Plugins;

/// <summary>
/// The read side of the small, Host-owned registry mapping a discovered
/// <see cref="Modules.IModule"/> <see cref="Type"/> back to the plugin's own
/// component principal that owns it.
/// </summary>
/// <remarks>
/// Exists so <c>TempestHost</c>'s own <see cref="Modules.ModuleLifecycleManager"/>
/// wiring can push the correct ambient component principal
/// (<see cref="Identity.ICurrentComponentAccessor"/>) around a module's own
/// lifecycle calls, without Module Discovery, Registration, or Lifecycle
/// ever needing to know a plugin exists — ADR-0111. Mirrors
/// <see cref="IPluginRegistry"/>'s own read/write split exactly: callers
/// within <c>Tempest.Core.Plugins</c> (<see cref="PluginAssemblyLoader"/>)
/// record entries through <see cref="IPluginComponentPrincipalRecorder"/>;
/// everything else — in practice, only <c>TempestHost</c>'s own
/// <c>componentScopeProvider</c> closure — observes through this interface.
/// Host-owned, never added to the <c>ServiceCollection</c>, never resolvable
/// by a module or plugin (ADR-0017).
/// </remarks>
public interface IPluginComponentPrincipalRegistry
{
    /// <summary>
    /// Returns the owning plugin's own component principal for
    /// <paramref name="moduleType"/>, or <see langword="null"/> if
    /// <paramref name="moduleType"/> did not come from a (successfully
    /// trust-checked) plugin — i.e., it is a genuine first-party module, or
    /// belonged to a plugin that was isolated (<see cref="PluginTrustDeniedException"/>)
    /// before reaching this point.
    /// </summary>
    /// <param name="moduleType">The discovered module type to look up.</param>
    Identity.IPrincipal? GetPrincipalFor(Type moduleType);
}
