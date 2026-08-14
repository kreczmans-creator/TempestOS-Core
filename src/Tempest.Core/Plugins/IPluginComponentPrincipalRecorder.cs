namespace Tempest.Core.Plugins;

/// <summary>
/// The write side of the small, Host-owned registry mapping a discovered
/// <see cref="Modules.IModule"/> <see cref="Type"/> back to the plugin's own
/// component principal that owns it.
/// </summary>
/// <remarks>
/// Used only by <see cref="PluginAssemblyLoader"/>, once, for every
/// <see cref="Modules.IModule"/> type found in a plugin whose two static
/// trust checks (capability eligibility, constructor conformance) both
/// passed (ADR-0111). Kept separate from <see cref="IPluginComponentPrincipalRegistry"/>
/// (the read side) so that nothing outside <c>Tempest.Core.Plugins</c> is
/// ever handed a reference capable of mutating this registry — mirroring
/// <see cref="IPluginRegistryRecorder"/>'s own identical rationale.
/// </remarks>
public interface IPluginComponentPrincipalRecorder
{
    /// <summary>
    /// Records <paramref name="principal"/> as the owning component
    /// principal for <paramref name="moduleType"/>.
    /// </summary>
    /// <param name="moduleType">The discovered module type to record an owner for.</param>
    /// <param name="principal">The owning plugin's own component principal.</param>
    void Record(Type moduleType, Identity.IPrincipal principal);
}
