using Tempest.Core.Commands;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Plugins;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Commands;

// ADR-0111: CommandRegistry's own trust-ordered registration retrofit -
// ownership tracking and tier-ranked eviction on RegisterDescriptor. No
// Unregister exists here (ADR-0037's own "no Unregister/Deregister is
// defined"). None of this is exercised by CommandRegistryTests.cs, which
// only proves the pre-existing, unconditional "first registration wins"
// behaviour with no component scope ever pushed.
public class CommandRegistryTrustTests
{
    // ------------------------------------------------------------------
    // Higher-tier registrant evicts a lower-tier owner
    // ------------------------------------------------------------------

    [Fact]
    public void RegisterDescriptor_HigherTierRegistrant_EvictsLowerTierOwner_ForSameId()
    {
        var (registry, accessor, _) = CreateRegistry();
        var lowTier = CreatePrincipal("plugin.low", PluginTrustPermission.UnsignedLocal, PluginCapability.Commands);
        var highTier = CreatePrincipal("plugin.high", PluginTrustPermission.VerifiedSigned, PluginCapability.Commands);

        using (accessor.BeginScope(lowTier))
            registry.RegisterDescriptor(new CommandDescriptor("shared", "Low Tier"));

        using (accessor.BeginScope(highTier))
            registry.RegisterDescriptor(new CommandDescriptor("shared", "High Tier"));

        Assert.Equal("High Tier", Assert.Single(registry.Items).DisplayName);
    }

    [Fact]
    public void RegisterDescriptor_HigherTierRegistrant_EvictionIsLoggedAsWarning()
    {
        var (registry, accessor, logger) = CreateRegistry();
        var lowTier = CreatePrincipal("plugin.low", PluginTrustPermission.UnsignedLocal, PluginCapability.Commands);
        var highTier = CreatePrincipal("plugin.high", PluginTrustPermission.VerifiedSigned, PluginCapability.Commands);

        using (accessor.BeginScope(lowTier))
            registry.RegisterDescriptor(new CommandDescriptor("shared", "Low Tier"));

        using (accessor.BeginScope(highTier))
            registry.RegisterDescriptor(new CommandDescriptor("shared", "High Tier"));

        Assert.True(logger.HasEntryAt(LogLevel.Warning, "ownership override"));
    }

    [Fact]
    public void RegisterDescriptor_FirstPartyRegistrant_EvictsAnyPluginOwner()
    {
        var (registry, accessor, _) = CreateRegistry();
        var plugin = CreatePrincipal("plugin.a", PluginTrustPermission.VerifiedSigned, PluginCapability.Commands);

        using (accessor.BeginScope(plugin))
            registry.RegisterDescriptor(new CommandDescriptor("shared", "Plugin"));

        registry.RegisterDescriptor(new CommandDescriptor("shared", "First Party")); // null scope

        Assert.Equal("First Party", Assert.Single(registry.Items).DisplayName);
    }

    // ------------------------------------------------------------------
    // Equal-or-lower-tier registrant is rejected
    // ------------------------------------------------------------------

    [Fact]
    public void RegisterDescriptor_SameTierRegistrant_ForSameId_ThrowsDuplicateCommandIdException()
    {
        var (registry, accessor, _) = CreateRegistry();
        var first = CreatePrincipal("plugin.a", PluginTrustPermission.VerifiedSigned, PluginCapability.Commands);
        var second = CreatePrincipal("plugin.b", PluginTrustPermission.VerifiedSigned, PluginCapability.Commands);

        using (accessor.BeginScope(first))
            registry.RegisterDescriptor(new CommandDescriptor("shared", "First"));

        using (accessor.BeginScope(second))
        {
            Assert.Throws<DuplicateCommandIdException>(() => registry.RegisterDescriptor(new CommandDescriptor("shared", "Second")));
        }

        Assert.Equal("First", Assert.Single(registry.Items).DisplayName);
    }

    [Fact]
    public void RegisterDescriptor_LowerTierRegistrant_AgainstHigherTierOwner_ThrowsDuplicateCommandIdException()
    {
        var (registry, accessor, _) = CreateRegistry();
        var highTier = CreatePrincipal("plugin.high", PluginTrustPermission.VerifiedSigned, PluginCapability.Commands);
        var lowTier = CreatePrincipal("plugin.low", PluginTrustPermission.UnsignedLocal, PluginCapability.Commands);

        using (accessor.BeginScope(highTier))
            registry.RegisterDescriptor(new CommandDescriptor("shared", "High"));

        using (accessor.BeginScope(lowTier))
        {
            Assert.Throws<DuplicateCommandIdException>(() => registry.RegisterDescriptor(new CommandDescriptor("shared", "Low")));
        }
    }

    // ------------------------------------------------------------------
    // Capability check
    // ------------------------------------------------------------------

    [Fact]
    public void RegisterDescriptor_ComponentWithoutCommandsCapability_ThrowsPermissionDeniedException()
    {
        var (registry, accessor, _) = CreateRegistry();
        var noCapability = CreatePrincipal("plugin.no-cap", PluginTrustPermission.UnsignedLocal);

        using (accessor.BeginScope(noCapability))
        {
            Assert.Throws<PermissionDeniedException>(() => registry.RegisterDescriptor(new CommandDescriptor("x", "X")));
        }

        Assert.Empty(registry.Items);
    }

    [Fact]
    public void RegisterDescriptor_ComponentWithCommandsCapability_Succeeds()
    {
        var (registry, accessor, _) = CreateRegistry();
        var withCapability = CreatePrincipal("plugin.a", PluginTrustPermission.UnsignedLocal, PluginCapability.Commands);

        using (accessor.BeginScope(withCapability))
        {
            registry.RegisterDescriptor(new CommandDescriptor("x", "X"));
        }

        Assert.Single(registry.Items);
    }

    [Fact]
    public void RegisterDescriptor_NullCurrentComponentAccessor_SkipsCapabilityCheck_ReproducesTodaysBehaviour()
    {
        var table = new CommandHandlerTable();
        var registry = new CommandRegistry(table); // no accessor, no evaluator

        var exception = Record.Exception(() => registry.RegisterDescriptor(new CommandDescriptor("x", "X")));

        Assert.Null(exception);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static (CommandRegistry Registry, CurrentComponentAccessor Accessor, RecordingLevelLogger Logger) CreateRegistry()
    {
        var table = new CommandHandlerTable();
        var accessor = new CurrentComponentAccessor();
        var evaluator = new PermissionEvaluator();
        var logger = new RecordingLevelLogger();
        var registry = new CommandRegistry(table, logger, accessor, evaluator);
        return (registry, accessor, logger);
    }

    private static PlatformPrincipal CreatePrincipal(string id, string tierPermissionKey, params string[] additionalPermissionKeys)
    {
        var permissions = new List<Permission> { new(tierPermissionKey) };
        permissions.AddRange(additionalPermissionKeys.Select(key => new Permission(key)));
        return new PlatformPrincipal(new PlatformIdentity(id, id), permissions);
    }
}
