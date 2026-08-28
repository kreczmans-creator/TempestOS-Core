using Tempest.Core.EngineeringDomain;

namespace Tempest.Samples;

/// <summary>
/// Registers how the canonical Kinds only the sample modules create come
/// back after a restart (`TD-85`).
/// </summary>
/// <remarks>
/// <para>
/// Every Kind's own declaring class is responsible for registering it —
/// the five real discipline registries in <c>Tempest.App</c> declare
/// seventeen between them, and these twelve are declared nowhere but here,
/// by <see cref="EngineeringDomainSampleModule"/>'s own graph. Without
/// this, the sample graph would be created on first launch and then
/// silently lose two thirds of itself on the next one — the sample modules
/// are idempotent across restarts (`TD-37`), so they would not re-create
/// what rehydration failed to bring back.
/// </para>
/// <para>
/// The Kind strings are the same literals
/// <see cref="EngineeringDomainSampleModule"/> already passes to
/// <see cref="EngineeringObjectFactory{T}"/>, referenced here rather than
/// promoted to named constants: these Kinds have no production write path,
/// so declaring them as platform vocabulary would overstate what they are
/// (`ADR-0105` governs values a real discipline owns).
/// </para>
/// <para>
/// This class is also the worked example of the extension point itself: a
/// module that introduces a Kind of its own registers one line per Kind,
/// and its objects survive restart with no change to the domain, the
/// factory, or the rehydration service.
/// </para>
/// </remarks>
public static class SampleEngineeringObjectRehydrators
{
    /// <summary>Registers every sample-only Kind's own rehydrator.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    public static void RegisterAll(IEngineeringObjectRehydratorRegistry registry, EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(context);

        registry.Register<Portfolio>("Portfolio", context);
        registry.Register<Programme>("Programme", context);
        registry.Register<Risk>("Risk", context);
        registry.Register<Decision>("Decision", context);
        registry.Register<EngineeringTask>("Task", context);
        registry.Register<Milestone>("Milestone", context);
        registry.Register<Deliverable>("Deliverable", context);
        registry.Register<ChangeRequest>("ChangeRequest", context);
        registry.Register<EngineeringChange>("EngineeringChange", context);
        registry.Register<Supplier>("Supplier", context);
        registry.Register<PurchaseItem>("PurchaseItem", context);
        registry.Register<ExternalSystemLink>("ExternalSystemLink", context);
    }
}
