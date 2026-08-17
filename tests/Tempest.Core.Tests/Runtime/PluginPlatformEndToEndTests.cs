using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.Diagnostics;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Core.Plugins;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// WP 13.3A: end-to-end validation of the COMPLETE plugin lifecycle through a
// real TempestHostBuilder/TempestHost - not PluginManifestDiscoveryService or
// PluginAssemblyLoader in isolation (already covered by
// PluginTrustTierAssignmentTests.cs / PluginAssemblyLoaderEnforceTrustTests.cs)
// and not simple single-stage Host-level checks (already covered by
// TempestHostPluginLifecycleTests.cs / TempestHostPluginTrustTests.cs). Every
// test here drives real, on-disk plugin candidates - real signed manifests
// (PluginSigningTestHelper's own real X509Certificate2/RSA-PSS machinery,
// reusing production PluginSignatureVerifier), real loadable assemblies
// (DynamicPluginAssemblyBuilder's PersistedAssemblyBuilder-based IL
// emission) - through TempestHost's own real, unmodified composition root:
// real Plugin Discovery, real Plugin Loading/trust enforcement, real Module
// Discovery/Registration/Lifecycle, and real capability-gated
// Navigation/Command registration via the Host's actual
// componentScopeProvider/ICurrentComponentAccessor wiring.
//
// A small number of tests here write real certificates into the actual,
// non-overridable TrustedPublishers/ folder relative to
// AppContext.BaseDirectory - the one path TempestHost's own
// `new PluginTrustStore(logger)` construction reads from (no test seam
// exists on TempestHost/TempestHostBuilder for this, unlike the plugins
// root itself). This is safe here specifically because every test in this
// file is tagged [Collection("Console output capture")] - the same,
// already-established collection every other real-Host test in this
// assembly uses, serialising all of them against each other - and because
// RealTrustedPublishersFixture deletes only the exact files it wrote.
[Collection("Console output capture")]
public class PluginPlatformEndToEndTests
{
    // ------------------------------------------------------------------
    // Scenario 1: full pipeline, one plugin (with a real dependency),
    // every stage's own effect independently observable.
    // ------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_SignedFirstPartyPluginWithDependency_EveryPipelineStageIndependentlyObservable()
    {
        using var trust = new RealTrustedPublishersFixture();
        using var firstPartyCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=TempestOS");
        trust.WriteFirstParty(firstPartyCertificate);

        using var temp = new TempDirectory();

        const string baseId = "wp133e2e.a1-base";
        const string dependentId = "wp133e2e.a1-dependent";
        const string commandId = "wp133e2e.a1-command";

        var baseFolder = CreateFolder(temp.Path, "a-a1-base");
        var baseAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            baseFolder, "Base.dll", baseId, "A1 Base Plugin", "1.0.0");
        WriteSignedManifest(baseFolder, firstPartyCertificate, baseId, Path.GetFileName(baseAssemblyPath), name: "A1 Base Plugin");

        var dependentFolder = CreateFolder(temp.Path, "b-a1-dependent");
        var dependentAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithCommandModule(
            dependentFolder, "Dependent.dll", dependentId, "A1 Dependent Plugin", "2.5.0", commandId, "A1 Dependent Command");
        WriteSignedManifest(
            dependentFolder, firstPartyCertificate, dependentId, Path.GetFileName(dependentAssemblyPath),
            name: "A1 Dependent Plugin", version: "2.5.0",
            requestedCapabilities:
            [
                PluginCapability.ServiceResolve(typeof(ICommandDispatcher).FullName!),
                PluginCapability.ServiceResolve(typeof(ICommandRegistry).FullName!),
            ],
            dependencies: [new PluginDependencyDto { Id = baseId, MinimumVersion = "1.0.0" }]);

        var dependentModuleType = LoadPluginModuleType(dependentAssemblyPath);

        var builder = new TempestHostBuilder([dependentModuleType], temp.Path);
        var host = builder.Build();

        var originalOut = Console.Out;
        var writer = new StringWriter();
        Task runTask;
        try
        {
            Console.SetOut(writer);
            runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HostState.Running, host.State);
        var output = writer.ToString();

        // Stage 1: the manifest was discovered (Plugin Discovery's own log line).
        Assert.Contains($"Plugin manifest accepted: '{baseId}'", output, StringComparison.Ordinal);
        Assert.Contains($"Plugin manifest accepted: '{dependentId}'", output, StringComparison.Ordinal);

        // Stage 2: the dependency was resolved, in the correct order - the
        // dependency's own assembly loads strictly before its dependent's,
        // proving genuine dependency-topological ordering, not mere
        // candidate-processing order (both happen to coincide here; a
        // reverse-order proof lives in the mixed-tier chain test below).
        var baseLoadedIndex = output.IndexOf($"Plugin assembly loaded: '{baseId}'", StringComparison.Ordinal);
        var dependentLoadedIndex = output.IndexOf($"Plugin assembly loaded: '{dependentId}'", StringComparison.Ordinal);
        Assert.True(baseLoadedIndex >= 0, "Base plugin was never loaded.");
        Assert.True(dependentLoadedIndex > baseLoadedIndex, "Dependent plugin did not load after its own dependency.");

        // Stage 3: signature verified and tier assigned FirstParty - proven
        // indirectly (PluginRegistryEntry carries no TrustTier field): the
        // dependent plugin requested two 'plugin.services.resolve:*'
        // capabilities, which are categorically ineligible for
        // UnsignedLocal (PluginAssemblyLoader's own ceiling never contains
        // a service-resolve-shaped key) - reaching Loaded, not TrustDenied,
        // is only possible if a higher tier than UnsignedLocal was assigned,
        // which only follows from successful signature verification.
        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        var baseEntry = diagnosticsProvider.Plugins.Single(e => e.Id == baseId);
        var dependentEntry = diagnosticsProvider.Plugins.Single(e => e.Id == dependentId);
        Assert.Equal(PluginRegistryState.Loaded, baseEntry.State);
        Assert.Equal(PluginRegistryState.Loaded, dependentEntry.State);
        Assert.Equal("A1 Dependent Plugin", dependentEntry.Name);
        Assert.Equal("2.5.0", dependentEntry.Version);

        // Stage 4: the capability check passed and the module actually
        // registered its command through the real Command Framework, not a
        // fake - resolved via the exact same DI-public surface a real
        // module would use.
        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
        var descriptor = commandRegistry.Items.Single(d => d.Id == commandId);
        Assert.Equal("A1 Dependent Command", descriptor.DisplayName);

        // Stage 5: activation - the module actually reached InitialiseAsync
        // (else the command above could never have been registered) AND
        // StartAsync (State is Running, not merely Initialised).
        var moduleStatus = diagnosticsProvider.Modules.Single(m => m.Descriptor.Id == dependentId);
        Assert.Equal(ModuleState.Running, moduleStatus.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }

    // ------------------------------------------------------------------
    // Scenario 2: multiple plugins, mixed trust tiers, a real dependency
    // chain, verifying load order respects the graph and each plugin's
    // tier-derived outcome is independently correct.
    // ------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_MixedTrustTierDependencyChain_LoadOrderRespectsGraph_EachReportsCorrectTierDerivedState()
    {
        using var trust = new RealTrustedPublishersFixture();
        using var firstPartyCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=TempestOS");
        using var verifiedCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Acme Plugins Ltd.");
        trust.WriteFirstParty(firstPartyCertificate);
        trust.WriteOther(verifiedCertificate, "Acme.cer");

        using var temp = new TempDirectory();

        const string idC = "wp133e2e.chain-c";
        const string idB = "wp133e2e.chain-b";
        const string idA = "wp133e2e.chain-a";
        const string idCeilingFirstParty = "wp133e2e.ceiling-firstparty";
        const string idCeilingUnsignedLocal = "wp133e2e.ceiling-unsignedlocal";

        // Folder names are deliberately in REVERSE order relative to the
        // dependency chain (A depends on B depends on C): candidate
        // PROCESSING order is folder-alphabetical, but the final returned
        // load order must still be dependency-topological regardless -
        // proving genuine dependency-graph resolution, not an accident of
        // enumeration order.
        var cFolder = CreateFolder(temp.Path, "z-chain-c");
        var cAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(cFolder, "C.dll", idC, "Chain Plugin C", "1.0.0");
        WriteUnsignedManifest(cFolder, idC, Path.GetFileName(cAssemblyPath), name: "Chain Plugin C");

        var bFolder = CreateFolder(temp.Path, "y-chain-b");
        var bAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(bFolder, "B.dll", idB, "Chain Plugin B", "1.0.0");
        WriteSignedManifest(
            bFolder, verifiedCertificate, idB, Path.GetFileName(bAssemblyPath), name: "Chain Plugin B",
            dependencies: [new PluginDependencyDto { Id = idC, MinimumVersion = "1.0.0" }]);

        var aFolder = CreateFolder(temp.Path, "x-chain-a");
        var aAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(aFolder, "A.dll", idA, "Chain Plugin A", "1.0.0");
        WriteSignedManifest(
            aFolder, firstPartyCertificate, idA, Path.GetFileName(aAssemblyPath), name: "Chain Plugin A",
            dependencies: [new PluginDependencyDto { Id = idB, MinimumVersion = "1.0.0" }]);

        // A second, independent pair - identical requested capability
        // ('plugin.di.register'), opposite trust tiers - the strongest
        // available proof that trust tier assignment genuinely differs
        // between a verifying FirstParty signature and no signature at
        // all: only the tier differs, and only the tier explains the
        // opposite outcome.
        var ceilingFirstPartyFolder = CreateFolder(temp.Path, "w-ceiling-firstparty");
        var ceilingFirstPartyAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            ceilingFirstPartyFolder, "CeilingFp.dll", idCeilingFirstParty, "Ceiling First Party Plugin", "1.0.0");
        WriteSignedManifest(
            ceilingFirstPartyFolder, firstPartyCertificate, idCeilingFirstParty, Path.GetFileName(ceilingFirstPartyAssemblyPath),
            name: "Ceiling First Party Plugin", requestedCapabilities: [PluginCapability.DiRegister]);

        var ceilingUnsignedFolder = CreateFolder(temp.Path, "v-ceiling-unsignedlocal");
        var ceilingUnsignedAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            ceilingUnsignedFolder, "CeilingUl.dll", idCeilingUnsignedLocal, "Ceiling Unsigned Local Plugin", "1.0.0");
        WriteUnsignedManifest(
            ceilingUnsignedFolder, idCeilingUnsignedLocal, Path.GetFileName(ceilingUnsignedAssemblyPath),
            name: "Ceiling Unsigned Local Plugin", requestedCapabilities: [PluginCapability.DiRegister]);

        var builder = new TempestHostBuilder(Type.EmptyTypes, temp.Path);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));
        var host = builder.Build();

        var originalOut = Console.Out;
        var writer = new StringWriter();
        Task runTask;
        try
        {
            Console.SetOut(writer);
            runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HostState.Running, host.State);
        var output = writer.ToString();

        var indexC = output.IndexOf($"Plugin assembly loaded: '{idC}'", StringComparison.Ordinal);
        var indexB = output.IndexOf($"Plugin assembly loaded: '{idB}'", StringComparison.Ordinal);
        var indexA = output.IndexOf($"Plugin assembly loaded: '{idA}'", StringComparison.Ordinal);

        Assert.True(indexC >= 0, "C (the innermost dependency) was never loaded.");
        Assert.True(indexB > indexC, "B did not load after its own dependency, C.");
        Assert.True(indexA > indexB, "A did not load after its own dependency, B.");

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));

        foreach (var id in new[] { idC, idB, idA, idCeilingFirstParty })
        {
            var entry = diagnosticsProvider.Plugins.Single(e => e.Id == id);
            Assert.Equal(PluginRegistryState.Loaded, entry.State);
        }

        var ceilingUnsignedEntry = diagnosticsProvider.Plugins.Single(e => e.Id == idCeilingUnsignedLocal);
        Assert.Equal(PluginRegistryState.TrustDenied, ceilingUnsignedEntry.State);
        Assert.Contains(PluginCapability.DiRegister, ceilingUnsignedEntry.Detail!, StringComparison.Ordinal);

        await host.StopAsync();
        await runTask;
    }

    // ------------------------------------------------------------------
    // Scenario 3: every PluginRegistryState value, reachable through one
    // real, multi-plugin TempestHost run, with genuinely distinguishable
    // Detail text between the different reasons a plugin can land in
    // Failed/TrustDenied.
    // ------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_EveryPluginRegistryState_ReachableThroughRealMultiPluginRun_DiagnosticsAccurateAndDistinguishable()
    {
        using var temp = new TempDirectory();

        const string idLoaded = "wp133e2e.c-loaded";
        const string idMissingAssembly = "wp133e2e.c-missing-assembly";
        const string idIncompatible = "wp133e2e.c-incompatible";
        const string idDependencyUnmet = "wp133e2e.c-dependency-unmet";
        const string idDisabled = "wp133e2e.c-disabled";
        const string idTrustDeniedCtor = "wp133e2e.c-trust-denied-ctor";
        const string idTrustDeniedCap = "wp133e2e.c-trust-denied-cap";

        var loadedFolder = CreateFolder(temp.Path, "1-loaded");
        var loadedAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(loadedFolder, "Loaded.dll", idLoaded, "Loaded Plugin", "1.0.0");
        WriteUnsignedManifest(loadedFolder, idLoaded, Path.GetFileName(loadedAssemblyPath), name: "Loaded Plugin");

        var malformedFolder = CreateFolder(temp.Path, "2-malformed-json");
        File.WriteAllText(Path.Combine(malformedFolder, PluginManifestDiscoveryService.ManifestFileName), "{ this is not valid JSON");

        var missingAssemblyFolder = CreateFolder(temp.Path, "3-missing-assembly");
        WriteUnsignedManifest(missingAssemblyFolder, idMissingAssembly, "DoesNotExist.dll", name: "Missing Assembly Plugin");

        var incompatibleFolder = CreateFolder(temp.Path, "4-incompatible");
        var incompatibleAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            incompatibleFolder, "Incompatible.dll", idIncompatible, "Incompatible Plugin", "1.0.0");
        WriteUnsignedManifest(
            incompatibleFolder, idIncompatible, Path.GetFileName(incompatibleAssemblyPath),
            name: "Incompatible Plugin", minimumPlatformVersion: "9.9.9");

        var dependencyUnmetFolder = CreateFolder(temp.Path, "5-dependency-unmet");
        var dependencyUnmetAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            dependencyUnmetFolder, "DepUnmet.dll", idDependencyUnmet, "Dependency Unmet Plugin", "1.0.0");
        WriteUnsignedManifest(
            dependencyUnmetFolder, idDependencyUnmet, Path.GetFileName(dependencyUnmetAssemblyPath), name: "Dependency Unmet Plugin",
            dependencies: [new PluginDependencyDto { Id = "wp133e2e.c-does-not-exist", MinimumVersion = "1.0.0" }]);

        var disabledFolder = CreateFolder(temp.Path, "6-disabled");
        var disabledAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(disabledFolder, "Disabled.dll", idDisabled, "Disabled Plugin", "1.0.0");
        WriteUnsignedManifest(disabledFolder, idDisabled, Path.GetFileName(disabledAssemblyPath), name: "Disabled Plugin");

        var trustDeniedCtorFolder = CreateFolder(temp.Path, "7-trust-denied-ctor");
        var trustDeniedCtorAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithCommandModule(
            trustDeniedCtorFolder, "TrustDeniedCtor.dll", idTrustDeniedCtor, "Trust Denied Ctor Plugin", "1.0.0",
            "wp133e2e.c-trust-denied-ctor-command", "Should Never Register");
        WriteUnsignedManifest(trustDeniedCtorFolder, idTrustDeniedCtor, Path.GetFileName(trustDeniedCtorAssemblyPath), name: "Trust Denied Ctor Plugin");

        var trustDeniedCapFolder = CreateFolder(temp.Path, "8-trust-denied-cap");
        var trustDeniedCapAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            trustDeniedCapFolder, "TrustDeniedCap.dll", idTrustDeniedCap, "Trust Denied Cap Plugin", "1.0.0");
        WriteUnsignedManifest(
            trustDeniedCapFolder, idTrustDeniedCap, Path.GetFileName(trustDeniedCapAssemblyPath), name: "Trust Denied Cap Plugin",
            requestedCapabilities: [PluginCapability.DiRegister]);

        var builder = new TempestHostBuilder(Type.EmptyTypes, temp.Path);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
            new KeyValuePair<string, string>("Runtime:Plugins:Disabled", idDisabled),
        ]));
        var host = builder.Build();

        var runTask = host.RunAsync();
        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        var entries = diagnosticsProvider.Plugins;
        Assert.Equal(8, entries.Count);

        var loadedEntry = entries.Single(e => e.Id == idLoaded);
        Assert.Equal(PluginRegistryState.Loaded, loadedEntry.State);
        Assert.Null(loadedEntry.Detail);

        var missingAssemblyEntry = entries.Single(e => e.Id == idMissingAssembly);
        Assert.Equal(PluginRegistryState.Failed, missingAssemblyEntry.State);
        Assert.NotNull(missingAssemblyEntry.Detail);
        Assert.Contains("does not exist", missingAssemblyEntry.Detail!, StringComparison.Ordinal);

        // The malformed-JSON candidate never parses far enough to obtain
        // its own declared Id - it is recorded under its candidate folder
        // path instead (PluginFailureLogging's own documented fallback) -
        // located by exclusion instead.
        var malformedJsonEntry = entries.Single(e => e.State == PluginRegistryState.Failed && e.Id != missingAssemblyEntry.Id);
        Assert.NotNull(malformedJsonEntry.Detail);
        Assert.Contains("JSON", malformedJsonEntry.Detail!, StringComparison.Ordinal);
        Assert.DoesNotContain("does not exist", malformedJsonEntry.Detail!, StringComparison.Ordinal);

        var incompatibleEntry = entries.Single(e => e.Id == idIncompatible);
        Assert.Equal(PluginRegistryState.Incompatible, incompatibleEntry.State);
        Assert.NotNull(incompatibleEntry.Detail);
        Assert.Contains("requires platform version", incompatibleEntry.Detail!, StringComparison.Ordinal);

        var dependencyUnmetEntry = entries.Single(e => e.Id == idDependencyUnmet);
        Assert.Equal(PluginRegistryState.DependencyUnmet, dependencyUnmetEntry.State);
        Assert.NotNull(dependencyUnmetEntry.Detail);
        Assert.Contains("depends on", dependencyUnmetEntry.Detail!, StringComparison.Ordinal);

        var disabledEntry = entries.Single(e => e.Id == idDisabled);
        Assert.Equal(PluginRegistryState.Disabled, disabledEntry.State);
        Assert.NotNull(disabledEntry.Detail);
        Assert.Contains("Disabled", disabledEntry.Detail!, StringComparison.Ordinal);

        var trustDeniedCtorEntry = entries.Single(e => e.Id == idTrustDeniedCtor);
        Assert.Equal(PluginRegistryState.TrustDenied, trustDeniedCtorEntry.State);
        Assert.NotNull(trustDeniedCtorEntry.Detail);
        Assert.Contains("constructor", trustDeniedCtorEntry.Detail!, StringComparison.OrdinalIgnoreCase);

        var trustDeniedCapEntry = entries.Single(e => e.Id == idTrustDeniedCap);
        Assert.Equal(PluginRegistryState.TrustDenied, trustDeniedCapEntry.State);
        Assert.NotNull(trustDeniedCapEntry.Detail);
        Assert.Contains(PluginCapability.DiRegister, trustDeniedCapEntry.Detail!, StringComparison.Ordinal);
        Assert.DoesNotContain("constructor", trustDeniedCapEntry.Detail!, StringComparison.OrdinalIgnoreCase);

        await host.StopAsync();
        await runTask;
    }

    // ------------------------------------------------------------------
    // Scenario 4 (+ activation, scenario 5): capability-gated Navigation
    // registration through the real Host's actual componentScopeProvider/
    // ICurrentComponentAccessor wiring - trust-ordered eviction/ownership
    // against a real first-party, built-in registration, AND across two
    // real plugins of different trust tiers.
    // ------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_TrustOrderedNavigationOwnership_AgainstFirstPartyBuiltIn_AndAcrossPlugins_ThroughRealHostWiring()
    {
        using var trust = new RealTrustedPublishersFixture();
        using var firstPartyCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=TempestOS");
        using var verifiedCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Acme Plugins Ltd.");
        trust.WriteFirstParty(firstPartyCertificate);
        trust.WriteOther(verifiedCertificate, "Acme.cer");

        using var temp = new TempDirectory();

        const string bbbId = "bbb.wp133e2e.verified-attempt-on-x";
        const string cccId = "ccc.wp133e2e.verified-owner-of-y";
        const string dddId = "ddd.wp133e2e.firstparty-evictor-of-y";
        const string itemYId = "wp133e2e.item-y";
        const string evictorTitle = "First Party Evictor Title (should win)";

        var navigationCapabilities = new[] { PluginCapability.Navigation, PluginCapability.ServiceResolve(typeof(INavigationProvider).FullName!) };

        // bbb (VerifiedSigned): sorts AFTER the built-in first-party owner
        // (module lifecycle order is ordinal by module Id) - attempts to
        // register the SAME Id the built-in already owns. Rank(VerifiedSigned)
        // = 2 <= Rank(null/first-party) = 3, so this must be REJECTED, not
        // an eviction.
        var bbbFolder = CreateFolder(temp.Path, "bbb-plugin");
        var bbbAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithNavigationModule(
            bbbFolder, "Bbb.dll", bbbId, "Verified Attempt On X", "1.0.0",
            FirstPartyNavigationOwnerFixtureModule.ItemXId, "Verified Attempt Title (must not win)");
        WriteSignedManifest(
            bbbFolder, verifiedCertificate, bbbId, Path.GetFileName(bbbAssemblyPath), name: "Verified Attempt On X",
            requestedCapabilities: navigationCapabilities);
        var bbbType = LoadPluginModuleType(bbbAssemblyPath);

        // ccc (VerifiedSigned): registers its OWN, uncontested Id first.
        var cccFolder = CreateFolder(temp.Path, "ccc-plugin");
        var cccAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithNavigationModule(
            cccFolder, "Ccc.dll", cccId, "Verified Owner Of Y", "1.0.0", itemYId, "Verified Owner Title (should be evicted)");
        WriteSignedManifest(
            cccFolder, verifiedCertificate, cccId, Path.GetFileName(cccAssemblyPath), name: "Verified Owner Of Y",
            requestedCapabilities: navigationCapabilities);
        var cccType = LoadPluginModuleType(cccAssemblyPath);

        // ddd (FirstParty): sorts AFTER ccc - re-registers the SAME Id ccc
        // already owns. Rank(FirstParty) = 3 > Rank(VerifiedSigned) = 2, so
        // this must EVICT successfully, logged loudly, no exception for
        // either module. FirstParty is unrestricted (IsFirstParty bypasses
        // RequirePermission entirely) so 'plugin.navigation.register'
        // itself is deliberately omitted here - only the constructor-
        // conformance grant is requested.
        var dddFolder = CreateFolder(temp.Path, "ddd-plugin");
        var dddAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithNavigationModule(
            dddFolder, "Ddd.dll", dddId, "First Party Evictor Of Y", "1.0.0", itemYId, evictorTitle);
        WriteSignedManifest(
            dddFolder, firstPartyCertificate, dddId, Path.GetFileName(dddAssemblyPath), name: "First Party Evictor Of Y",
            requestedCapabilities: [PluginCapability.ServiceResolve(typeof(INavigationProvider).FullName!)]);
        var dddType = LoadPluginModuleType(dddAssemblyPath);

        var builder = new TempestHostBuilder(
            [typeof(FirstPartyNavigationOwnerFixtureModule), bbbType, cccType, dddType], temp.Path);
        var host = builder.Build();

        var originalOut = Console.Out;
        var writer = new StringWriter();
        Task runTask;
        try
        {
            Console.SetOut(writer);
            runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HostState.Running, host.State);
        var output = writer.ToString();

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));

        var builtinStatus = diagnosticsProvider.Modules.Single(m => m.Descriptor.Id == FirstPartyNavigationOwnerFixtureModule.ModuleId);
        Assert.Equal(ModuleState.Running, builtinStatus.State);

        var bbbStatus = diagnosticsProvider.Modules.Single(m => m.Descriptor.Id == bbbId);
        Assert.Equal(ModuleState.Failed, bbbStatus.State);
        Assert.IsType<DuplicateNavigationItemException>(bbbStatus.FailureReason);
        Assert.Contains(FirstPartyNavigationOwnerFixtureModule.ItemXId, bbbStatus.FailureReason!.Message, StringComparison.Ordinal);

        var cccStatus = diagnosticsProvider.Modules.Single(m => m.Descriptor.Id == cccId);
        Assert.Equal(ModuleState.Running, cccStatus.State);

        var dddStatus = diagnosticsProvider.Modules.Single(m => m.Descriptor.Id == dddId);
        Assert.Equal(ModuleState.Running, dddStatus.State);

        var navigationProvider = (INavigationProvider)host.Services!.GetService(typeof(INavigationProvider));

        // The built-in's own item is unchanged - the rejected registration
        // never overwrote it.
        var itemX = navigationProvider.Items.Single(i => i.Id == FirstPartyNavigationOwnerFixtureModule.ItemXId);
        Assert.Equal(FirstPartyNavigationOwnerFixtureModule.ItemXTitle, itemX.Title);

        // The FirstParty evictor's item won.
        var itemY = navigationProvider.Items.Single(i => i.Id == itemYId);
        Assert.Equal(evictorTitle, itemY.Title);

        Assert.Contains("ownership override", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(itemYId, output, StringComparison.Ordinal);

        await host.StopAsync();
        await runTask;
    }

    // ------------------------------------------------------------------
    // Scenario 6 (WP 13.10B): multi-module-per-plugin coverage - a single
    // plugin whose one assembly contains TWO separate, legitimate IModule
    // types (DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithTwoModules),
    // driven through a real TempestHostBuilder/TempestHost.RunAsync() end to
    // end. Every prior multi-module test in this suite ("one succeeds, one
    // fails, sibling unaffected") uses two separate PLUGINS, each with
    // exactly one module - this proves the same shape genuinely holds
    // WITHIN one plugin's own multiple modules too.
    // ------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_SinglePluginWithTwoModules_BothModulesIndependentlyReachRunning_PluginRecordedLoaded()
    {
        using var temp = new TempDirectory();

        const string pluginId = "wp1310b.twomodule-positive";
        const string module1Id = "wp1310b.twomodule-positive.one";
        const string module2Id = "wp1310b.twomodule-positive.two";

        var folder = CreateFolder(temp.Path, "twomodule-positive");
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithTwoModules(
            folder, "TwoModulesPositive.dll",
            module1Id, "Two Module Plugin - Module One", "1.0.0",
            module2Id, "Two Module Plugin - Module Two", "1.0.0",
            module2ThrowsOnInitialise: false);
        WriteUnsignedManifest(folder, pluginId, Path.GetFileName(assemblyPath), name: "Two Module Plugin");

        var moduleTypes = LoadPluginModuleTypes(assemblyPath);
        Assert.Equal(2, moduleTypes.Count);

        var builder = new TempestHostBuilder(moduleTypes, temp.Path);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));
        var host = builder.Build();

        var runTask = host.RunAsync();
        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));

        var pluginEntry = diagnosticsProvider.Plugins.Single(e => e.Id == pluginId);
        Assert.Equal(PluginRegistryState.Loaded, pluginEntry.State);

        var module1Status = diagnosticsProvider.Modules.Single(m => m.Descriptor.Id == module1Id);
        Assert.Equal(ModuleState.Running, module1Status.State);
        Assert.Null(module1Status.FailureReason);

        var module2Status = diagnosticsProvider.Modules.Single(m => m.Descriptor.Id == module2Id);
        Assert.Equal(ModuleState.Running, module2Status.State);
        Assert.Null(module2Status.FailureReason);

        await host.StopAsync();
        await runTask;
    }

    // ------------------------------------------------------------------
    // Scenario 7 (WP 13.10B): failure isolation WITHIN one plugin's own
    // multiple modules - module 2's own InitialiseAsync deliberately
    // throws; module 1, its own SIBLING in the identical plugin (same
    // assembly, same manifest, same trust decision), must still
    // independently reach Running, and the Host itself must still reach
    // Running overall - one module's own failure never cascades to a
    // sibling module belonging to the same plugin, nor crashes the Host.
    // ------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_SinglePluginWithTwoModules_OneModuleInitialiseAsyncFails_SiblingModuleWithinSamePluginStillReachesRunning_HostStaysRunning()
    {
        using var temp = new TempDirectory();

        const string pluginId = "wp1310b.twomodule-isolation";
        const string module1Id = "wp1310b.twomodule-isolation.survivor";
        const string module2Id = "wp1310b.twomodule-isolation.failing";

        var folder = CreateFolder(temp.Path, "twomodule-isolation");
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithTwoModules(
            folder, "TwoModulesIsolation.dll",
            module1Id, "Two Module Plugin - Survivor", "1.0.0",
            module2Id, "Two Module Plugin - Failing", "1.0.0",
            module2ThrowsOnInitialise: true);
        WriteUnsignedManifest(folder, pluginId, Path.GetFileName(assemblyPath), name: "Two Module Failure Plugin");

        var moduleTypes = LoadPluginModuleTypes(assemblyPath);
        Assert.Equal(2, moduleTypes.Count);

        var builder = new TempestHostBuilder(moduleTypes, temp.Path);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));
        var host = builder.Build();

        var runTask = host.RunAsync();
        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        // The Host itself is unaffected - one plugin's own module-level
        // activation failure is isolated, never Host-fatal.
        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));

        // The plugin itself still reaches Loaded - Plugin Loading's own
        // constructor-conformance and capability checks ran, and passed,
        // strictly before either module's own InitialiseAsync was ever
        // called; a lifecycle-time failure in one module cannot retroactively
        // change the plugin's own already-recorded Loading outcome.
        var pluginEntry = diagnosticsProvider.Plugins.Single(e => e.Id == pluginId);
        Assert.Equal(PluginRegistryState.Loaded, pluginEntry.State);

        // Module 1 - the survivor, and the SAME plugin's own sibling of the
        // module that fails below - still independently reaches Running.
        var survivorStatus = diagnosticsProvider.Modules.Single(m => m.Descriptor.Id == module1Id);
        Assert.Equal(ModuleState.Running, survivorStatus.State);
        Assert.Null(survivorStatus.FailureReason);

        // Module 2 - the one whose own InitialiseAsync deliberately throws -
        // isolated as Failed, with the distinctive marker preserved
        // (thrown directly from IL-emitted code via a virtual override call,
        // never through reflection Invoke, so it is never wrapped in a
        // TargetInvocationException - mirrors this suite's own established
        // pattern, e.g. RunAsync_TrustOrderedNavigationOwnership...'s own
        // bbbStatus.FailureReason assertion above).
        var failingStatus = diagnosticsProvider.Modules.Single(m => m.Descriptor.Id == module2Id);
        Assert.Equal(ModuleState.Failed, failingStatus.State);
        Assert.NotNull(failingStatus.FailureReason);
        Assert.IsType<InvalidOperationException>(failingStatus.FailureReason);
        Assert.Contains("WP1310B-DELIBERATE-INITIALISE-FAILURE", failingStatus.FailureReason!.Message, StringComparison.Ordinal);

        await host.StopAsync();
        await runTask;
    }

    // ------------------------------------------------------------------
    // Suspected production defect, documented as a passing, precise
    // repro rather than fixed here (per this Work Package's own
    // instructions - see final report).
    // ------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_UnsignedLocalPlugin_RequestingNavigationServiceResolve_IsTrustDenied_DespiteNavigationRegisterBeingWithinCeiling()
    {
        using var temp = new TempDirectory();
        var folder = CreateFolder(temp.Path, "unsigned-nav-plugin");

        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssemblyWithNavigationModule(
            folder, "UnsignedNav.dll", "wp133e2e.unsignedlocal-nav-attempt", "Unsigned Local Nav Attempt", "1.0.0",
            "wp133e2e.unsignedlocal-item", "Should Never Register");

        WriteUnsignedManifest(
            folder, "wp133e2e.unsignedlocal-nav-attempt", Path.GetFileName(assemblyPath), name: "Unsigned Local Nav Attempt",
            requestedCapabilities: [PluginCapability.Navigation, PluginCapability.ServiceResolve(typeof(INavigationProvider).FullName!)]);

        var builder = new TempestHostBuilder(Type.EmptyTypes, temp.Path);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));
        var host = builder.Build();

        var runTask = host.RunAsync();
        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        var diagnosticsProvider = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));
        var entry = Assert.Single(diagnosticsProvider.Plugins);

        // Documents current, real behaviour: 'plugin.navigation.register'
        // alone is within UnsignedLocal's own ceiling, but a module cannot
        // actually call NavigationService.Register without
        // INavigationProvider constructor-injected first, which requires a
        // SEPARATE 'plugin.services.resolve:...INavigationProvider' grant -
        // and that capability shape can never be eligible for UnsignedLocal
        // (PluginAssemblyLoader.UnsignedLocalCapabilityCeiling only ever
        // contains the two exact keys Navigation/Commands, never any
        // service-resolve-prefixed key). See this test's own final report
        // write-up.
        Assert.Equal(PluginRegistryState.TrustDenied, entry.State);
        Assert.NotNull(entry.Detail);
        Assert.Contains("plugin.services.resolve", entry.Detail!, StringComparison.Ordinal);

        await host.StopAsync();
        await runTask;
    }

    // ------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------

    private static string CreateFolder(string root, string name)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Loads the already-built plugin assembly at <paramref name="assemblyPath"/>
    /// into this test process (via the exact same <see cref="Assembly.LoadFrom(string)"/>
    /// call <see cref="PluginAssemblyLoader"/> itself will later make against
    /// the identical path - the default load context caches by assembly
    /// identity, so both calls resolve to the same, single loaded assembly
    /// and Type) purely to obtain its one discovered <see cref="IModule"/>
    /// type's <see cref="Type"/> handle - needed to pass as an explicit
    /// Module Discovery candidate (a real AppDomain-wide scan is never used
    /// in this test assembly; see the established precedent and its own
    /// stated hazard in <c>TempestHostPluginLifecycleTests.cs</c>), so a
    /// plugin's own module can be proven to reach real Module Discovery/
    /// Registration/Lifecycle without also scanning in every unrelated
    /// fixture module scattered across this test assembly.
    /// </summary>
    private static Type LoadPluginModuleType(string assemblyPath) =>
        Assembly.LoadFrom(assemblyPath).GetTypes()
            .Single(type => typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

    /// <summary>
    /// The multi-module counterpart of <see cref="LoadPluginModuleType"/> -
    /// returns every discovered <see cref="IModule"/> type in the assembly at
    /// <paramref name="assemblyPath"/> (WP 13.10B: a plugin whose one
    /// assembly declares more than one legitimate module type), rather than
    /// requiring exactly one.
    /// </summary>
    private static IReadOnlyList<Type> LoadPluginModuleTypes(string assemblyPath) =>
        Assembly.LoadFrom(assemblyPath).GetTypes()
            .Where(type => typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
            .ToList();

    private static void WriteSignedManifest(
        string folder,
        X509Certificate2 signingCertificate,
        string id,
        string assemblyFileName,
        string name = "Signed Plugin",
        string version = "1.0.0",
        string minimumPlatformVersion = "0.1.0",
        IReadOnlyList<string>? requestedCapabilities = null,
        IReadOnlyList<PluginDependencyDto>? dependencies = null)
    {
        var assemblyPath = Path.Combine(folder, assemblyFileName);
        var dto = PluginSigningTestHelper.BuildDto(id, assemblyFileName, name, version, minimumPlatformVersion, requestedCapabilities);

        if (dependencies is not null)
            dto.Dependencies = dependencies;

        dto.Signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(dto, assemblyPath, signingCertificate);

        File.WriteAllText(
            Path.Combine(folder, PluginManifestDiscoveryService.ManifestFileName),
            PluginSigningTestHelper.ToManifestJson(dto));
    }

    private static void WriteUnsignedManifest(
        string folder,
        string id,
        string assemblyFileName,
        string name = "Unsigned Plugin",
        string version = "1.0.0",
        string minimumPlatformVersion = "0.1.0",
        IReadOnlyList<string>? requestedCapabilities = null,
        IReadOnlyList<PluginDependencyDto>? dependencies = null)
    {
        var dto = PluginSigningTestHelper.BuildDto(id, assemblyFileName, name, version, minimumPlatformVersion, requestedCapabilities);

        if (dependencies is not null)
            dto.Dependencies = dependencies;

        File.WriteAllText(
            Path.Combine(folder, PluginManifestDiscoveryService.ManifestFileName),
            PluginSigningTestHelper.ToManifestJson(dto));
    }

    /// <summary>
    /// Writes real certificates into the actual, conventional
    /// <c>TrustedPublishers/</c> folder relative to <see cref="AppContext.BaseDirectory"/> -
    /// the one, fixed, non-overridable path <see cref="TempestHost"/>'s own
    /// real <c>new PluginTrustStore(logger)</c> construction reads from (no
    /// test seam exists for it on <see cref="TempestHostBuilder"/>, unlike
    /// the plugins root itself). Deletes only the specific files it wrote,
    /// on <see cref="Dispose"/>.
    /// </summary>
    private sealed class RealTrustedPublishersFixture : IDisposable
    {
        private readonly List<string> _writtenFilePaths = [];

        public string FolderPath { get; } = Path.Combine(AppContext.BaseDirectory, PluginTrustStore.TrustedPublishersFolderName);

        public X509Certificate2 WriteFirstParty(X509Certificate2 certificate) =>
            Write(certificate, PluginTrustStore.FirstPartyCertificateFileName);

        public X509Certificate2 WriteOther(X509Certificate2 certificate, string fileName) =>
            Write(certificate, fileName);

        private X509Certificate2 Write(X509Certificate2 certificate, string fileName)
        {
            PluginSigningTestHelper.WriteToTrustStore(FolderPath, certificate, fileName);
            _writtenFilePaths.Add(Path.Combine(FolderPath, fileName));
            return certificate;
        }

        public void Dispose()
        {
            foreach (var path in _writtenFilePaths)
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}

/// <summary>
/// A genuine first-party fixture module (constructed directly via an
/// explicit Module Discovery candidate list - never plugin-loaded, never
/// trust-checked) that registers one <see cref="NavigationItem"/> during
/// <see cref="InitialiseAsync"/> with no ambient component scope (registrant
/// is <see langword="null"/> - the real "genuinely first-party" case
/// <see cref="Plugins.PluginTrustPermission.Rank"/> itself documents),
/// exactly mirroring a real, built-in, first-party module's own shape.
/// Decorated with <see cref="ModuleMetadataAttribute"/> so Module Discovery
/// never needs a parameterless constructor to read its identity.
/// </summary>
[ModuleMetadata(ModuleId, "First-Party Navigation Owner Fixture", "1.0.0")]
internal sealed class FirstPartyNavigationOwnerFixtureModule : IModule, IModuleLifecycle
{
    public const string ModuleId = "aaa.wp133e2e.firstparty-owner";
    public const string ItemXId = "wp133e2e.item-x";
    public const string ItemXTitle = "First Party Owned Item X";

    private readonly INavigationProvider _navigationProvider;

    public FirstPartyNavigationOwnerFixtureModule(INavigationProvider navigationProvider)
    {
        _navigationProvider = navigationProvider;
    }

    public string Id => ModuleId;

    public string Name => "First-Party Navigation Owner Fixture";

    public string Version => "1.0.0";

    public Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Register(new NavigationItem(ItemXId, ItemXTitle));
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisposeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
