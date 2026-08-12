namespace Tempest.Core.Modules;

/// <summary>
/// Marks an <see cref="IModule"/> implementation as existing solely to
/// deliberately fail, or otherwise deliberately misbehave, in order to
/// validate the platform's own failure-handling behaviour — never a
/// genuine application capability.
/// </summary>
/// <remarks>
/// <para>
/// A discovery-time classification, not an instantiation-avoidance
/// mechanism (contrast <see cref="ModuleMetadataAttribute"/>, ADR-0027,
/// which exists so Discovery never has to construct a module just to read
/// its identity). A marker interface fits this "is-a" question better
/// than a second attribute: whether a candidate type implements this
/// interface is checked with a plain <c>is</c>/<c>IsAssignableFrom</c>
/// test, requiring no reflection-attribute lookup.
/// </para>
/// <para>
/// <see cref="ReflectionFrameworkDiscoveryService"/> excludes any
/// candidate implementing this interface by default — from both its
/// full-<c>AppDomain</c> scan and its explicit-candidate-type overload
/// alike — unless constructed with <c>includeFaultInjectionModules:
/// true</c>. This is what keeps a fault-injection module out of ordinary
/// application startup even if its own assembly happens to be loaded into
/// the same process (discovery scans the whole <c>AppDomain</c>, not only
/// directly-referenced assemblies) — not referencing the assembly at all
/// is the first line of defence; this interface is the second, and the
/// one that still holds if the first is ever accidentally breached. See
/// ADR-0102 and <c>Fault Injection &amp; Validation Architecture.md</c> for
/// the complete design.
/// </para>
/// </remarks>
public interface IFaultInjectionModule : IModule
{
}
