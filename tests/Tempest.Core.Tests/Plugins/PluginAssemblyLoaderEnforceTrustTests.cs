using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.Diagnostics;
using Tempest.Core.Logging;
using Tempest.Core.Plugins;

namespace Tempest.Core.Tests.Plugins;

// ADR-0111: PluginAssemblyLoader.EnforceTrust's own two static checks -
// capability-eligibility (UnsignedLocal's fixed, two-key ceiling; an
// unrecognised capability shape denied for every tier) and
// constructor-conformance (the fixed always-allowed baseline;
// plugin.services.resolve:* eligibility) - neither of which
// PluginAssemblyLoaderTests.cs exercises at all (it only proves LoadPlugins'
// own file-loading success/failure paths, always with an empty
// RequestedCapabilities list and no module type requiring anything beyond
// the baseline).
public class PluginAssemblyLoaderEnforceTrustTests
{
    // ------------------------------------------------------------------
    // Capability eligibility ceiling: UnsignedLocal
    // ------------------------------------------------------------------

    [Fact]
    public void EnforceTrust_UnsignedLocal_BothCeilingCapabilities_IsAllowed()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Plugin.dll", "test.ceiling-ok", "Ceiling OK", "1.0.0");

        var manifest = CreateManifest(
            "test.ceiling-ok", assemblyPath, PluginTrustTier.UnsignedLocal,
            [PluginCapability.Navigation, PluginCapability.Commands]);

        var loader = new PluginAssemblyLoader();

        var result = loader.LoadPlugins([manifest]);

        Assert.Single(result);
    }

    [Fact]
    public void EnforceTrust_UnsignedLocal_ThirdCapabilityOutsideCeiling_IsDenied_RecordsTrustDenied()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Plugin.dll", "test.ceiling-exceeded", "Ceiling Exceeded", "1.0.0");

        var manifest = CreateManifest(
            "test.ceiling-exceeded", assemblyPath, PluginTrustTier.UnsignedLocal,
            [PluginCapability.Navigation, PluginCapability.Commands, PluginCapability.DiRegister]);

        var registry = new PluginRegistry();
        var logger = new RecordingLevelLogger();
        var loader = new PluginAssemblyLoader(logger, registry);

        var result = loader.LoadPlugins([manifest]);

        Assert.Empty(result);
        var entry = Assert.Single(registry.Entries);
        Assert.Equal(PluginRegistryState.TrustDenied, entry.State);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("test.ceiling-exceeded", StringComparison.Ordinal));
    }

    [Fact]
    public void EnforceTrust_UnsignedLocal_EventPublishCapability_OutsideCeiling_IsDenied()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Plugin.dll", "test.event-outside-ceiling", "Event Outside Ceiling", "1.0.0");

        var manifest = CreateManifest(
            "test.event-outside-ceiling", assemblyPath, PluginTrustTier.UnsignedLocal,
            [PluginCapability.EventPublish("Some.Namespace.SomeEvent")]);

        var registry = new PluginRegistry();
        var loader = new PluginAssemblyLoader(registryRecorder: registry);

        var result = loader.LoadPlugins([manifest]);

        Assert.Empty(result);
        Assert.Equal(PluginRegistryState.TrustDenied, Assert.Single(registry.Entries).State);
    }

    // ------------------------------------------------------------------
    // WP 13.2B security review finding (CONFIRMED privilege escalation):
    // plugin.services.resolve:* naming a Host-authority, container-resolvable
    // concrete type (global::Tempest.Core.Identity.CurrentComponentAccessor / CurrentPrincipalAccessor)
    // must never be eligible, for ANY trust tier including FirstParty -
    // resolving either concrete type hands a plugin the exact same singleton
    // instance the Host itself uses, letting it forge an arbitrary component
    // principal (including a self-granted FirstParty tier marker) via
    // BeginScope, or hijack the ambient, process-wide user principal via
    // SetCurrent. Before this fix, VerifiedSigned/FirstParty's "no fixed
    // ceiling" meant nothing blocked this specific pair of types.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(PluginTrustTier.VerifiedSigned)]
    [InlineData(PluginTrustTier.FirstParty)]
    public void EnforceTrust_RequestsServiceResolve_ForCurrentComponentAccessor_IsDeniedRegardlessOfTier(PluginTrustTier tier)
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Plugin.dll", $"test.escalation-cca-{tier}", "Escalation Attempt", "1.0.0");

        var manifest = CreateManifest(
            $"test.escalation-cca-{tier}", assemblyPath, tier,
            [PluginCapability.ServiceResolve(typeof(global::Tempest.Core.Identity.CurrentComponentAccessor).FullName!)]);

        var registry = new PluginRegistry();
        var loader = new PluginAssemblyLoader(registryRecorder: registry);

        var result = loader.LoadPlugins([manifest]);

        Assert.Empty(result);
        Assert.Equal(PluginRegistryState.TrustDenied, Assert.Single(registry.Entries).State);
    }

    [Theory]
    [InlineData(PluginTrustTier.VerifiedSigned)]
    [InlineData(PluginTrustTier.FirstParty)]
    public void EnforceTrust_RequestsServiceResolve_ForCurrentPrincipalAccessor_IsDeniedRegardlessOfTier(PluginTrustTier tier)
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Plugin.dll", $"test.escalation-cpa-{tier}", "Escalation Attempt", "1.0.0");

        var manifest = CreateManifest(
            $"test.escalation-cpa-{tier}", assemblyPath, tier,
            [PluginCapability.ServiceResolve(typeof(global::Tempest.Core.Identity.CurrentPrincipalAccessor).FullName!)]);

        var registry = new PluginRegistry();
        var loader = new PluginAssemblyLoader(registryRecorder: registry);

        var result = loader.LoadPlugins([manifest]);

        Assert.Empty(result);
        Assert.Equal(PluginRegistryState.TrustDenied, Assert.Single(registry.Entries).State);
    }

    [Fact]
    public void EnforceTrust_ModuleConstructorRequestsCurrentComponentAccessor_IsDenied_EvenWhenGranted()
    {
        // Defense in depth: even if a manifest somehow carried the granted
        // capability string (bypassing FindIneligibleCapability), a module
        // constructor parameter of the exact denylisted type must still
        // never be treated as compliant.
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildPluginAssemblyWithConstructorParameters(
            temp.Path, "Plugin.dll", "test.escalation-ctor", "Escalation Ctor", "1.0.0",
            [typeof(global::Tempest.Core.Identity.CurrentComponentAccessor)]);

        var manifest = CreateManifest(
            "test.escalation-ctor", assemblyPath, PluginTrustTier.FirstParty,
            [PluginCapability.ServiceResolve(typeof(global::Tempest.Core.Identity.CurrentComponentAccessor).FullName!)]);

        var registry = new PluginRegistry();
        var loader = new PluginAssemblyLoader(registryRecorder: registry);

        var result = loader.LoadPlugins([manifest]);

        Assert.Empty(result);
        Assert.Equal(PluginRegistryState.TrustDenied, Assert.Single(registry.Entries).State);
    }

    // ------------------------------------------------------------------
    // Unrecognised capability shape: denied for EVERY tier, not only
    // UnsignedLocal.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(PluginTrustTier.UnsignedLocal)]
    [InlineData(PluginTrustTier.VerifiedSigned)]
    [InlineData(PluginTrustTier.FirstParty)]
    public void EnforceTrust_UnrecognisedCapabilityShape_IsDeniedRegardlessOfTier(PluginTrustTier tier)
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Plugin.dll", $"test.bogus-{tier}", "Bogus Capability", "1.0.0");

        var manifest = CreateManifest(
            $"test.bogus-{tier}", assemblyPath, tier, ["totally.bogus.capability.key"]);

        var registry = new PluginRegistry();
        var loader = new PluginAssemblyLoader(registryRecorder: registry);

        var result = loader.LoadPlugins([manifest]);

        Assert.Empty(result);
        Assert.Equal(PluginRegistryState.TrustDenied, Assert.Single(registry.Entries).State);
    }

    [Fact]
    public void EnforceTrust_VerifiedSignedOrFirstParty_RecognisedCapabilities_AreNotClampedToUnsignedLocalCeiling()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Plugin.dll", "test.verified-unclamped", "Verified Unclamped", "1.0.0");

        // DiRegister is outside UnsignedLocal's ceiling, but VerifiedSigned
        // is not clamped to that ceiling at all - only UnsignedLocal is.
        var manifest = CreateManifest(
            "test.verified-unclamped", assemblyPath, PluginTrustTier.VerifiedSigned,
            [PluginCapability.DiRegister]);

        var loader = new PluginAssemblyLoader();

        var result = loader.LoadPlugins([manifest]);

        Assert.Single(result);
    }

    // ------------------------------------------------------------------
    // Constructor conformance: the fixed always-allowed baseline
    // ------------------------------------------------------------------

    [Fact]
    public void EnforceTrust_ModuleConstructor_BaselineOnlyParameters_EmptyCapabilities_IsCompliant()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildPluginAssemblyWithConstructorParameters(
            temp.Path, "Plugin.dll", "test.baseline-ctor", "Baseline Ctor", "1.0.0",
            [typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider)]);

        var manifest = CreateManifest("test.baseline-ctor", assemblyPath, PluginTrustTier.UnsignedLocal, []);

        var loader = new PluginAssemblyLoader();

        var result = loader.LoadPlugins([manifest]);

        Assert.Single(result);
    }

    [Fact]
    public void EnforceTrust_ModuleConstructor_NonBaselineParameter_NoGrant_IsDenied_RecordsTrustDenied()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildPluginAssemblyWithConstructorParameters(
            temp.Path, "Plugin.dll", "test.ungranted-ctor", "Ungranted Ctor", "1.0.0",
            [typeof(ICommandDispatcher)]);

        // VerifiedSigned tier, but no plugin.services.resolve:* capability
        // granted for ICommandDispatcher at all.
        var manifest = CreateManifest("test.ungranted-ctor", assemblyPath, PluginTrustTier.VerifiedSigned, []);

        var registry = new PluginRegistry();
        var logger = new RecordingLevelLogger();
        var loader = new PluginAssemblyLoader(logger, registry);

        var result = loader.LoadPlugins([manifest]);

        Assert.Empty(result);
        Assert.Equal(PluginRegistryState.TrustDenied, Assert.Single(registry.Entries).State);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning &&
            e.Message.Contains("test.ungranted-ctor", StringComparison.Ordinal));
    }

    [Fact]
    public void EnforceTrust_ModuleConstructor_NonBaselineParameter_WithMatchingGrant_IsCompliant()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildPluginAssemblyWithConstructorParameters(
            temp.Path, "Plugin.dll", "test.granted-ctor", "Granted Ctor", "1.0.0",
            [typeof(ICommandDispatcher)]);

        var manifest = CreateManifest(
            "test.granted-ctor", assemblyPath, PluginTrustTier.VerifiedSigned,
            [PluginCapability.ServiceResolve(typeof(ICommandDispatcher).FullName!)]);

        var loader = new PluginAssemblyLoader();

        var result = loader.LoadPlugins([manifest]);

        Assert.Single(result);
    }

    [Fact]
    public void EnforceTrust_ModuleConstructor_MultipleNonBaselineParameters_RequiresGrantForEachOne()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildPluginAssemblyWithConstructorParameters(
            temp.Path, "Plugin.dll", "test.partial-grant", "Partial Grant", "1.0.0",
            [typeof(ICommandDispatcher), typeof(ICommandRegistry)]);

        // Only one of the two required grants - the other is missing.
        var manifest = CreateManifest(
            "test.partial-grant", assemblyPath, PluginTrustTier.VerifiedSigned,
            [PluginCapability.ServiceResolve(typeof(ICommandDispatcher).FullName!)]);

        var registry = new PluginRegistry();
        var loader = new PluginAssemblyLoader(registryRecorder: registry);

        var result = loader.LoadPlugins([manifest]);

        Assert.Empty(result);
        Assert.Equal(PluginRegistryState.TrustDenied, Assert.Single(registry.Entries).State);
    }

    // ------------------------------------------------------------------
    // Component principal recording (ADR-0111): a plugin that passes both
    // checks has its principal recorded per discovered module Type, with
    // the tier marker permission and every requested capability granted.
    // ------------------------------------------------------------------

    [Fact]
    public void EnforceTrust_PluginPassesBothChecks_RecordsComponentPrincipal_WithGrantedCapabilitiesAndTierMarker()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Plugin.dll", "test.principal-recorded", "Principal Recorded", "1.0.0");

        var manifest = CreateManifest(
            "test.principal-recorded", assemblyPath, PluginTrustTier.UnsignedLocal,
            [PluginCapability.Navigation]);

        var recorder = new RecordingComponentPrincipalRecorder();
        var loader = new PluginAssemblyLoader(componentPrincipalRecorder: recorder);

        var result = loader.LoadPlugins([manifest]);

        Assert.Single(result);
        var recorded = Assert.Single(recorder.Recorded);
        Assert.Equal("test.principal-recorded", recorded.Principal.Identity.Id);
        Assert.Contains(new Tempest.Core.Identity.Permission(PluginCapability.Navigation), recorded.Principal.Permissions);
        Assert.Contains(new Tempest.Core.Identity.Permission(PluginTrustPermission.UnsignedLocal), recorded.Principal.Permissions);
    }

    [Fact]
    public void EnforceTrust_PluginDenied_NeverRecordsComponentPrincipal()
    {
        using var temp = new TempDirectory();
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Plugin.dll", "test.denied-no-principal", "Denied No Principal", "1.0.0");

        var manifest = CreateManifest(
            "test.denied-no-principal", assemblyPath, PluginTrustTier.UnsignedLocal,
            [PluginCapability.DiRegister]);

        var recorder = new RecordingComponentPrincipalRecorder();
        var loader = new PluginAssemblyLoader(componentPrincipalRecorder: recorder);

        loader.LoadPlugins([manifest]);

        Assert.Empty(recorder.Recorded);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static PluginManifest CreateManifest(
        string id, string assemblyPath, PluginTrustTier trustTier, IReadOnlyList<string> requestedCapabilities) =>
        new(id, $"{id} name", "1.0.0", new Version(0, 1, 0), Path.GetFileName(assemblyPath), assemblyPath,
            [], requestedCapabilities, null, null, trustTier);

    private sealed class RecordingComponentPrincipalRecorder : IPluginComponentPrincipalRecorder
    {
        public List<(Type ModuleType, Tempest.Core.Identity.IPrincipal Principal)> Recorded { get; } = [];

        public void Record(Type moduleType, Tempest.Core.Identity.IPrincipal principal) =>
            Recorded.Add((moduleType, principal));
    }
}
