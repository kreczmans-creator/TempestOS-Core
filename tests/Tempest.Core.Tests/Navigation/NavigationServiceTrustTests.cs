using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Navigation;
using Tempest.Core.Plugins;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Navigation;

// ADR-0111: NavigationService's own trust-ordered registration retrofit -
// ownership tracking, tier-ranked eviction on Register, and Unregister's
// ownership-mismatch denial (including its one deliberate exception to the
// "null collaborator reproduces today's behaviour" rule). None of this is
// exercised by NavigationServiceTests.cs, which only proves the pre-existing,
// unconditional "first registration wins" behaviour with no component scope
// ever pushed.
public class NavigationServiceTrustTests
{
    // ------------------------------------------------------------------
    // Register: higher-trust-tier registrant evicts a lower-tier owner
    // ------------------------------------------------------------------

    [Fact]
    public void Register_HigherTierRegistrant_EvictsLowerTierOwner_ForSameId()
    {
        var (service, accessor, _) = CreateService();
        var lowTier = CreatePrincipal("plugin.low", PluginTrustPermission.UnsignedLocal, PluginCapability.Navigation);
        var highTier = CreatePrincipal("plugin.high", PluginTrustPermission.VerifiedSigned, PluginCapability.Navigation);

        using (accessor.BeginScope(lowTier))
            service.Register(new NavigationItem("shared", "Low Tier Item"));

        using (accessor.BeginScope(highTier))
            service.Register(new NavigationItem("shared", "High Tier Item"));

        var item = Assert.Single(service.Items);
        Assert.Equal("High Tier Item", item.Title);
    }

    [Fact]
    public void Register_HigherTierRegistrant_EvictionIsLoggedAsWarning()
    {
        var (service, accessor, logger) = CreateService();
        var lowTier = CreatePrincipal("plugin.low", PluginTrustPermission.UnsignedLocal, PluginCapability.Navigation);
        var highTier = CreatePrincipal("plugin.high", PluginTrustPermission.VerifiedSigned, PluginCapability.Navigation);

        using (accessor.BeginScope(lowTier))
            service.Register(new NavigationItem("shared", "Low Tier Item"));

        using (accessor.BeginScope(highTier))
            service.Register(new NavigationItem("shared", "High Tier Item"));

        Assert.True(logger.HasEntryAt(LogLevel.Warning, "ownership override"));
    }

    [Fact]
    public void Register_FirstPartyRegistrant_EvictsAnyPluginOwner()
    {
        var (service, accessor, _) = CreateService();
        var plugin = CreatePrincipal("plugin.a", PluginTrustPermission.VerifiedSigned, PluginCapability.Navigation);

        using (accessor.BeginScope(plugin))
            service.Register(new NavigationItem("shared", "Plugin Item"));

        // No BeginScope - null current component = first-party, ranked
        // identically to a plugin that itself achieved FirstParty tier.
        service.Register(new NavigationItem("shared", "First Party Item"));

        Assert.Equal("First Party Item", Assert.Single(service.Items).Title);
    }

    // ------------------------------------------------------------------
    // Register: a genuine FirstParty-tier plugin principal (the tier marker
    // permission, achieved via signature verification - not merely a null,
    // unwired component) is exempt from the plugin.navigation.register
    // capability check entirely, exactly as ADR-0111 and this type's own
    // remarks document ("skipped entirely - not merely satisfied - when the
    // ambient component principal is null or First-Party"). WP 13.2B
    // security review finding: the guard previously read `if (registrant is
    // not null)`, so a real FirstParty-tier plugin holding only the tier
    // marker (no explicit navigation.register grant) was incorrectly denied.
    // ------------------------------------------------------------------

    [Fact]
    public void Register_FirstPartyTierPrincipal_WithNoExplicitCapabilityGrant_Succeeds()
    {
        var (service, accessor, _) = CreateService();
        var firstParty = CreatePrincipal("plugin.firstparty", PluginTrustPermission.FirstParty);

        using (accessor.BeginScope(firstParty))
            service.Register(new NavigationItem("first-party-item", "First Party Item"));

        Assert.Equal("First Party Item", Assert.Single(service.Items).Title);
    }

    // ------------------------------------------------------------------
    // Register: concurrent registrants racing for the same Id. The
    // _gate lock serialises the check-then-act eviction decision, so the
    // higher-trust-tier registrant must always end up the sole owner
    // regardless of real thread interleaving/arrival order - either it
    // registers first and the low-tier attempt is rejected, or it
    // registers second and evicts the low-tier owner. Repeated to raise
    // the odds of exposing a race if the locking around Register were
    // ever weakened.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Register_ConcurrentRegistrantsForSameId_HighestTierAlwaysEndsUpSoleOwner()
    {
        for (var iteration = 0; iteration < 25; iteration++)
        {
            var eventBus = new EventBus();
            var accessor = new CurrentComponentAccessor();
            var evaluator = new PermissionEvaluator();
            var service = new NavigationService(eventBus, currentComponentAccessor: accessor, permissionEvaluator: evaluator);

            var lowTier = CreatePrincipal("plugin.low", PluginTrustPermission.UnsignedLocal, PluginCapability.Navigation);
            var highTier = CreatePrincipal("plugin.high", PluginTrustPermission.VerifiedSigned, PluginCapability.Navigation);

            using var startGate = new SemaphoreSlim(0, 2);

            var lowTask = Task.Run(async () =>
            {
                using (accessor.BeginScope(lowTier))
                {
                    await startGate.WaitAsync();
                    try { service.Register(new NavigationItem("race", "Low Tier Item")); }
                    catch (DuplicateNavigationItemException) { }
                }
            });

            var highTask = Task.Run(async () =>
            {
                using (accessor.BeginScope(highTier))
                {
                    await startGate.WaitAsync();
                    try { service.Register(new NavigationItem("race", "High Tier Item")); }
                    catch (DuplicateNavigationItemException) { }
                }
            });

            startGate.Release(2);
            await Task.WhenAll(lowTask, highTask);

            var item = Assert.Single(service.Items);
            Assert.Equal("High Tier Item", item.Title);
        }
    }

    // ------------------------------------------------------------------
    // Register: equal-or-lower-tier registrant is rejected
    // ------------------------------------------------------------------

    [Fact]
    public void Register_SameTierRegistrant_ForSameId_ThrowsDuplicateNavigationItemException()
    {
        var (service, accessor, _) = CreateService();
        var first = CreatePrincipal("plugin.a", PluginTrustPermission.VerifiedSigned, PluginCapability.Navigation);
        var second = CreatePrincipal("plugin.b", PluginTrustPermission.VerifiedSigned, PluginCapability.Navigation);

        using (accessor.BeginScope(first))
            service.Register(new NavigationItem("shared", "First"));

        using (accessor.BeginScope(second))
        {
            Assert.Throws<DuplicateNavigationItemException>(() => service.Register(new NavigationItem("shared", "Second")));
        }

        Assert.Equal("First", Assert.Single(service.Items).Title);
    }

    [Fact]
    public void Register_LowerTierRegistrant_AgainstHigherTierOwner_ThrowsDuplicateNavigationItemException()
    {
        var (service, accessor, _) = CreateService();
        var highTier = CreatePrincipal("plugin.high", PluginTrustPermission.VerifiedSigned, PluginCapability.Navigation);
        var lowTier = CreatePrincipal("plugin.low", PluginTrustPermission.UnsignedLocal, PluginCapability.Navigation);

        using (accessor.BeginScope(highTier))
            service.Register(new NavigationItem("shared", "High"));

        using (accessor.BeginScope(lowTier))
        {
            Assert.Throws<DuplicateNavigationItemException>(() => service.Register(new NavigationItem("shared", "Low")));
        }

        Assert.Equal("High", Assert.Single(service.Items).Title);
    }

    [Fact]
    public void Register_PluginAgainstFirstPartyOwner_ThrowsDuplicateNavigationItemException()
    {
        var (service, accessor, _) = CreateService();
        service.Register(new NavigationItem("shared", "First Party")); // null scope = first-party

        var plugin = CreatePrincipal("plugin.a", PluginTrustPermission.FirstParty, PluginCapability.Navigation);
        using (accessor.BeginScope(plugin))
        {
            Assert.Throws<DuplicateNavigationItemException>(() => service.Register(new NavigationItem("shared", "Plugin")));
        }
    }

    // ------------------------------------------------------------------
    // Register: capability check
    // ------------------------------------------------------------------

    [Fact]
    public void Register_ComponentWithoutNavigationCapability_ThrowsPermissionDeniedException()
    {
        var (service, accessor, _) = CreateService();
        var noCapability = CreatePrincipal("plugin.no-cap", PluginTrustPermission.UnsignedLocal);

        using (accessor.BeginScope(noCapability))
        {
            Assert.Throws<PermissionDeniedException>(() => service.Register(new NavigationItem("x", "X")));
        }

        Assert.Empty(service.Items);
    }

    [Fact]
    public void Register_ComponentWithNavigationCapability_Succeeds()
    {
        var (service, accessor, _) = CreateService();
        var withCapability = CreatePrincipal("plugin.a", PluginTrustPermission.UnsignedLocal, PluginCapability.Navigation);

        using (accessor.BeginScope(withCapability))
        {
            service.Register(new NavigationItem("x", "X"));
        }

        Assert.Single(service.Items);
    }

    [Fact]
    public void Register_NullCurrentComponentAccessor_SkipsCapabilityCheck_ReproducesTodaysBehaviour()
    {
        var eventBus = new EventBus();
        var service = new NavigationService(eventBus); // no accessor, no evaluator

        var exception = Record.Exception(() => service.Register(new NavigationItem("x", "X")));

        Assert.Null(exception);
    }

    // ------------------------------------------------------------------
    // Unregister: ownership
    // ------------------------------------------------------------------

    [Fact]
    public void Unregister_SameOwner_RemovesTheItem()
    {
        var (service, accessor, _) = CreateService();
        var owner = CreatePrincipal("plugin.a", PluginTrustPermission.UnsignedLocal, PluginCapability.Navigation);

        using (accessor.BeginScope(owner))
        {
            service.Register(new NavigationItem("x", "X"));
            service.Unregister("x");
        }

        Assert.Empty(service.Items);
    }

    [Fact]
    public void Unregister_FirstPartyCaller_RemovesAnyItem_Unconditionally()
    {
        var (service, accessor, _) = CreateService();
        var owner = CreatePrincipal("plugin.a", PluginTrustPermission.UnsignedLocal, PluginCapability.Navigation);

        using (accessor.BeginScope(owner))
            service.Register(new NavigationItem("x", "X"));

        // No scope pushed = first-party caller.
        service.Unregister("x");

        Assert.Empty(service.Items);
    }

    [Fact]
    public void Unregister_MismatchedOwner_EvaluatorGrantsOverridePermission_Succeeds()
    {
        var eventBus = new EventBus();
        var accessor = new CurrentComponentAccessor();
        var evaluator = new PermissionEvaluator();
        var service = new NavigationService(eventBus, currentComponentAccessor: accessor, permissionEvaluator: evaluator);

        var owner = CreatePrincipal("plugin.owner", PluginTrustPermission.UnsignedLocal, PluginCapability.Navigation);
        using (accessor.BeginScope(owner))
            service.Register(new NavigationItem("x", "X"));

        var overrideCaller = CreatePrincipal(
            "plugin.override", PluginTrustPermission.VerifiedSigned, PluginCapability.Navigation, "navigation.unregister.any");

        using (accessor.BeginScope(overrideCaller))
            service.Unregister("x");

        Assert.Empty(service.Items);
    }

    [Fact]
    public void Unregister_MismatchedOwner_EvaluatorPresentButNoOverridePermission_ThrowsPermissionDeniedException()
    {
        var eventBus = new EventBus();
        var accessor = new CurrentComponentAccessor();
        var evaluator = new PermissionEvaluator();
        var service = new NavigationService(eventBus, currentComponentAccessor: accessor, permissionEvaluator: evaluator);

        var owner = CreatePrincipal("plugin.owner", PluginTrustPermission.UnsignedLocal, PluginCapability.Navigation);
        using (accessor.BeginScope(owner))
            service.Register(new NavigationItem("x", "X"));

        var otherCaller = CreatePrincipal("plugin.other", PluginTrustPermission.UnsignedLocal, PluginCapability.Navigation);

        using (accessor.BeginScope(otherCaller))
        {
            Assert.Throws<PermissionDeniedException>(() => service.Unregister("x"));
        }

        Assert.Single(service.Items); // never removed
    }

    [Fact]
    public void Unregister_MismatchedOwner_NullPermissionEvaluator_DeliberatelyDeniesRatherThanReproducingOldUnconditionalSuccess()
    {
        var eventBus = new EventBus();
        var accessor = new CurrentComponentAccessor();
        // permissionEvaluator deliberately null - this is the one documented
        // exception to "null collaborator reproduces today's behaviour."
        var service = new NavigationService(eventBus, currentComponentAccessor: accessor, permissionEvaluator: null);

        var owner = CreatePrincipal("plugin.owner", PluginTrustPermission.UnsignedLocal, PluginCapability.Navigation);
        using (accessor.BeginScope(owner))
            service.Register(new NavigationItem("x", "X"));

        var otherCaller = CreatePrincipal("plugin.other", PluginTrustPermission.UnsignedLocal, PluginCapability.Navigation);

        using (accessor.BeginScope(otherCaller))
        {
            Assert.Throws<PermissionDeniedException>(() => service.Unregister("x"));
        }

        Assert.Single(service.Items); // never removed - old unconditional-success behaviour must not resurrect
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static (NavigationService Service, CurrentComponentAccessor Accessor, RecordingLevelLogger Logger) CreateService()
    {
        var eventBus = new EventBus();
        var accessor = new CurrentComponentAccessor();
        var evaluator = new PermissionEvaluator();
        var logger = new RecordingLevelLogger();
        var service = new NavigationService(eventBus, logger, accessor, evaluator);
        return (service, accessor, logger);
    }

    private static PlatformPrincipal CreatePrincipal(string id, string tierPermissionKey, params string[] additionalPermissionKeys)
    {
        var permissions = new List<Permission> { new(tierPermissionKey) };
        permissions.AddRange(additionalPermissionKeys.Select(key => new Permission(key)));
        return new PlatformPrincipal(new PlatformIdentity(id, id), permissions);
    }
}
