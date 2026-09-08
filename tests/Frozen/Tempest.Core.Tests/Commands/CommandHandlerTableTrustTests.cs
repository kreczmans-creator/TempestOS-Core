using Tempest.Core.Commands;
using Tempest.Core.Identity;
using Tempest.Core.Logging;
using Tempest.Core.Plugins;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Commands;

// ADR-0111: CommandHandlerTable's own trust-ordered registration retrofit -
// its own, separate ownership tracking (independent of CommandRegistry's)
// keyed by command Type rather than string Id. No dedicated
// CommandHandlerTableTests.cs previously existed at all - CommandRegistryTests
// and CommandDispatcherTests only exercise it indirectly, always through a
// null component accessor.
public class CommandHandlerTableTrustTests
{
    [Fact]
    public async Task Register_HigherTierRegistrant_EvictsLowerTierOwner_ForSameCommandType()
    {
        var (table, accessor, _) = CreateTable();
        var lowTier = CreatePrincipal("plugin.low", PluginTrustPermission.UnsignedLocal, PluginCapability.Commands);
        var highTier = CreatePrincipal("plugin.high", PluginTrustPermission.VerifiedSigned, PluginCapability.Commands);

        var lowHandler = new RecordingCommandHandler<RecordedCommandA>();
        var highHandler = new RecordingCommandHandler<RecordedCommandA>();

        using (accessor.BeginScope(lowTier))
            table.Register(lowHandler);

        using (accessor.BeginScope(highTier))
            table.Register(highHandler);

        await table.DispatchAsync(new RecordedCommandA(), CancellationToken.None);

        Assert.Empty(lowHandler.Received);
        Assert.Single(highHandler.Received);
    }

    [Fact]
    public void Register_HigherTierRegistrant_EvictionIsLoggedAsWarning()
    {
        var (table, accessor, logger) = CreateTable();
        var lowTier = CreatePrincipal("plugin.low", PluginTrustPermission.UnsignedLocal, PluginCapability.Commands);
        var highTier = CreatePrincipal("plugin.high", PluginTrustPermission.VerifiedSigned, PluginCapability.Commands);

        using (accessor.BeginScope(lowTier))
            table.Register(new RecordingCommandHandler<RecordedCommandA>());

        using (accessor.BeginScope(highTier))
            table.Register(new RecordingCommandHandler<RecordedCommandA>());

        Assert.True(logger.HasEntryAt(LogLevel.Warning, "ownership override"));
    }

    [Fact]
    public void Register_SameTierRegistrant_ForSameCommandType_ThrowsDuplicateCommandHandlerException()
    {
        var (table, accessor, _) = CreateTable();
        var first = CreatePrincipal("plugin.a", PluginTrustPermission.VerifiedSigned, PluginCapability.Commands);
        var second = CreatePrincipal("plugin.b", PluginTrustPermission.VerifiedSigned, PluginCapability.Commands);

        using (accessor.BeginScope(first))
            table.Register(new RecordingCommandHandler<RecordedCommandA>());

        using (accessor.BeginScope(second))
        {
            Assert.Throws<DuplicateCommandHandlerException>(() => table.Register(new RecordingCommandHandler<RecordedCommandA>()));
        }
    }

    [Fact]
    public void Register_LowerTierRegistrant_AgainstHigherTierOwner_ThrowsDuplicateCommandHandlerException()
    {
        var (table, accessor, _) = CreateTable();
        var highTier = CreatePrincipal("plugin.high", PluginTrustPermission.VerifiedSigned, PluginCapability.Commands);
        var lowTier = CreatePrincipal("plugin.low", PluginTrustPermission.UnsignedLocal, PluginCapability.Commands);

        using (accessor.BeginScope(highTier))
            table.Register(new RecordingCommandHandler<RecordedCommandA>());

        using (accessor.BeginScope(lowTier))
        {
            Assert.Throws<DuplicateCommandHandlerException>(() => table.Register(new RecordingCommandHandler<RecordedCommandA>()));
        }
    }

    [Fact]
    public async Task Register_FirstPartyRegistrant_EvictsAnyPluginOwner()
    {
        var (table, accessor, _) = CreateTable();
        var plugin = CreatePrincipal("plugin.a", PluginTrustPermission.VerifiedSigned, PluginCapability.Commands);
        var pluginHandler = new RecordingCommandHandler<RecordedCommandA>();
        var firstPartyHandler = new RecordingCommandHandler<RecordedCommandA>();

        using (accessor.BeginScope(plugin))
            table.Register(pluginHandler);

        table.Register(firstPartyHandler); // null scope

        await table.DispatchAsync(new RecordedCommandA(), CancellationToken.None);

        Assert.Empty(pluginHandler.Received);
        Assert.Single(firstPartyHandler.Received);
    }

    [Fact]
    public void Register_ComponentWithoutCommandsCapability_ThrowsPermissionDeniedException()
    {
        var (table, accessor, _) = CreateTable();
        var noCapability = CreatePrincipal("plugin.no-cap", PluginTrustPermission.UnsignedLocal);

        using (accessor.BeginScope(noCapability))
        {
            Assert.Throws<PermissionDeniedException>(() => table.Register(new RecordingCommandHandler<RecordedCommandA>()));
        }
    }

    [Fact]
    public void Register_FirstPartyTierPrincipal_WithNoExplicitCapabilityGrant_Succeeds()
    {
        // ADR-0111 / WP 13.2B security review finding: the guard previously
        // read `if (registrant is not null)`, so a genuine FirstParty-tier
        // plugin (achieved via signature verification, holding only the
        // tier marker) was incorrectly denied instead of exempted.
        var (table, accessor, _) = CreateTable();
        var firstParty = CreatePrincipal("plugin.firstparty", PluginTrustPermission.FirstParty);

        using (accessor.BeginScope(firstParty))
        {
            var exception = Record.Exception(() => table.Register(new RecordingCommandHandler<RecordedCommandA>()));
            Assert.Null(exception);
        }
    }

    [Fact]
    public async Task DispatchAsync_PushesTheHandlersOwnRecordedOwner_AsCurrentComponent_ForTheDurationOfTheHandler()
    {
        // WP 13.2B architecture compliance finding: DispatchAsync previously
        // never pushed a component scope at all, so a plugin-owned handler's
        // own internal, capability-gated calls ran under whichever component
        // happened to be ambient *before* dispatch (typically null/first-
        // party) rather than the handler's own principal - silently
        // bypassing enforcement for that entire re-entry path. Mirrors
        // EventBus.PublishAsync's own per-subscriber scope push.
        var (table, accessor, _) = CreateTable();
        var owner = CreatePrincipal("plugin.owner", PluginTrustPermission.VerifiedSigned, PluginCapability.Commands);

        IPrincipal? observedDuringHandling = null;
        var handler = new RecordingCommandHandler<RecordedCommandA>((_, _) =>
        {
            observedDuringHandling = accessor.Current;
            return Task.FromResult(CommandResult.Success());
        });

        using (accessor.BeginScope(owner))
            table.Register(handler);

        Assert.Null(accessor.Current); // ambient before dispatch: no component in scope

        await table.DispatchAsync(new RecordedCommandA(), CancellationToken.None);

        Assert.NotNull(observedDuringHandling);
        Assert.Equal(owner.Identity.Id, observedDuringHandling!.Identity.Id);
        Assert.Null(accessor.Current); // popped correctly after dispatch returns
    }

    [Fact]
    public void Register_NullCurrentComponentAccessor_SkipsCapabilityCheck_ReproducesTodaysBehaviour()
    {
        var table = new CommandHandlerTable();

        var exception = Record.Exception(() => table.Register(new RecordingCommandHandler<RecordedCommandA>()));

        Assert.Null(exception);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static (CommandHandlerTable Table, CurrentComponentAccessor Accessor, RecordingLevelLogger Logger) CreateTable()
    {
        var accessor = new CurrentComponentAccessor();
        var evaluator = new PermissionEvaluator();
        var logger = new RecordingLevelLogger();
        var table = new CommandHandlerTable(logger, accessor, evaluator);
        return (table, accessor, logger);
    }

    private static PlatformPrincipal CreatePrincipal(string id, string tierPermissionKey, params string[] additionalPermissionKeys)
    {
        var permissions = new List<Permission> { new(tierPermissionKey) };
        permissions.AddRange(additionalPermissionKeys.Select(key => new Permission(key)));
        return new PlatformPrincipal(new PlatformIdentity(id, id), permissions);
    }
}
