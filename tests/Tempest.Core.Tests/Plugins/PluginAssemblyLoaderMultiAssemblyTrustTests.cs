using System.Runtime.Loader;
using Tempest.Core.Configuration;
using Tempest.Core.Diagnostics;
using Tempest.Core.Logging;
using Tempest.Core.Plugins;

namespace Tempest.Core.Tests.Plugins;

// WP 13.9.1 security remediation. WP 13.9.0's Security/Trust review
// empirically demonstrated, against this project's own compiled binary,
// that PluginAssemblyLoader.EnforceTrust scanned only the one
// manifest-declared assembly's own types - but .NET only loads a
// referenced assembly lazily, the moment one of its types is resolved
// (exactly what Assembly.GetTypes's own base-type-chain resolution
// triggers), so a plugin's real code footprint is not always confined to
// its one manifest-declared file. A second, wholly undeclared assembly
// sitting in the plugin's own candidate folder - reached only because the
// primary assembly's own type inherits from a type declared there - had
// its own IModule implementers reach Module Discovery (deliberately
// plugin-unaware, ADR-0110) with zero trust checking of any kind.
//
// These tests prove, end-to-end through the real PluginAssemblyLoader,
// that the fixed-point, breadth-first scan closes this gap without
// overreaching: the exact attack scenario is now denied; a legitimate
// multi-assembly plugin still loads; component principals are recorded
// for module types discovered in a secondary assembly too.
// WP 13.9.1: see PluginAssemblyLoaderTests.cs's own comment on this same
// [Collection] attribute - real assembly loading here must not race
// against any other test class that also loads a real assembly, given
// EnforceTrust's own fixed-point AppDomain scan (the exact mechanism these
// tests exercise).
[Collection("Console output capture")]
public class PluginAssemblyLoaderMultiAssemblyTrustTests
{
    [Fact]
    public void EnforceTrust_SecondUndeclaredAssembly_EvilModuleWithForbiddenConstructor_IsDenied_RecordsTrustDenied()
    {
        using var temp = new TempDirectory();

        // The second, wholly undeclared assembly: never named in any plugin
        // manifest, sitting in the plugin's own candidate folder. Its own
        // EvilModule : IModule implementer's sole public constructor
        // requires the forbidden, denylisted CurrentComponentAccessor
        // concrete type (WP 13.2B) - non-compliant under HasCompliantConstructor
        // for every trust tier, including the lowest (UnsignedLocal), with
        // zero requested capabilities.
        var secondaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            temp.Path,
            "Secondary",
            "SecondaryBaseType",
            "EvilModule",
            "test.evil-module",
            "Evil Module",
            "1.0.0",
            [typeof(global::Tempest.Core.Identity.CurrentComponentAccessor)]);

        var secondaryAssemblyName = Path.GetFileNameWithoutExtension(secondaryAssemblyPath);
        var externalBaseTypeFullName = $"{secondaryAssemblyName}.SecondaryBaseType";

        // The primary, manifest-declared assembly: UnsignedLocal tier, zero
        // requested capabilities - the lowest possible trust posture - with
        // no IModule implementer of its own. Its only connection to the
        // second assembly is a plain inheritance relationship, an entirely
        // ordinary plugin-with-a-dependency-DLL layout.
        var primaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyDerivingFromExternalBaseType(
            temp.Path,
            "Primary.dll",
            secondaryAssemblyPath,
            externalBaseTypeFullName,
            moduleId: "test.primary-attack",
            moduleName: "Primary Attack",
            moduleVersion: "1.0.0",
            implementIModule: false);

        var manifest = CreateManifest("test.multi-asm-attack", primaryAssemblyPath, PluginTrustTier.UnsignedLocal, []);

        var registry = new PluginRegistry();
        var logger = new RecordingLevelLogger();
        var recorder = new RecordingComponentPrincipalRecorder();
        var loader = new PluginAssemblyLoader(logger, registry, recorder);

        var result = loader.LoadPlugins([manifest]);

        // The exact opposite of WP 13.9.0's own proof-of-concept outcome:
        // this plugin is now denied, not silently treated as first-party.
        Assert.Empty(result);
        var entry = Assert.Single(registry.Entries);
        Assert.Equal(PluginRegistryState.TrustDenied, entry.State);
        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("test.multi-asm-attack", StringComparison.Ordinal));

        // Neither half of the WP 13.9.0 bypass survives: no component
        // principal is ever recorded for the denied plugin, so EvilModule
        // can never be misattributed to a null (First-Party-treated)
        // ambient component principal.
        Assert.Empty(recorder.Recorded);
    }

    // WP 13.9.4 trust-denial execution boundary remediation. The test above
    // proves EnforceTrust denies this exact multi-assembly attack shape; this
    // one proves the newer, separate defect it exposed is also closed:
    // before WP 13.9.4, denial recorded NOTHING identifying which Types
    // belonged to the denied plugin - not even its primary assembly's own
    // module, and certainly not a transitively-discovered secondary
    // assembly's - so nothing downstream could ever have filtered them out
    // of Module Registration, no matter how the filter itself were built.
    // Deliberately a PluginAssemblyLoader-level test, not a full
    // TempestHost end-to-end one: proving DiscoverModuleTypes's own
    // fixed-point scan correctly attributes EvilModule to this denial and
    // PluginDeniedTypeRegistry correctly records it is precise and
    // deterministic here; TempestHostPluginTrustTests.cs's own
    // single-assembly tests separately prove the OTHER half - that
    // TempestHost's Module Registration filter correctly excludes whatever
    // this registry records - through the real Host pipeline. A genuine,
    // unrestricted AppDomain-wide Module Discovery scan is not safe to
    // drive from this shared test process (it would also discover every
    // other test class's own dynamically-built IModule fixtures, several of
    // which are deliberately malformed for unrelated scenarios), so the two
    // halves are proven independently and compose to the same guarantee.
    [Fact]
    public void EnforceTrust_SecondUndeclaredAssembly_EvilModuleWithForbiddenConstructor_IsDenied_RecordsDeniedModuleType()
    {
        using var temp = new TempDirectory();

        var secondaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            temp.Path,
            "DeniedTypeSecondary",
            "DeniedTypeSecondaryBaseType",
            "EvilModule",
            "test.denied-type-evil-module",
            "Evil Module",
            "1.0.0",
            [typeof(global::Tempest.Core.Identity.CurrentComponentAccessor)]);

        var secondaryAssemblyName = Path.GetFileNameWithoutExtension(secondaryAssemblyPath);
        var externalBaseTypeFullName = $"{secondaryAssemblyName}.DeniedTypeSecondaryBaseType";
        var evilModuleFullName = $"{secondaryAssemblyName}.EvilModule";

        var primaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyDerivingFromExternalBaseType(
            temp.Path,
            "DeniedTypePrimary.dll",
            secondaryAssemblyPath,
            externalBaseTypeFullName,
            moduleId: "test.denied-type-primary-attack",
            moduleName: "Denied Type Primary Attack",
            moduleVersion: "1.0.0",
            implementIModule: false);

        var manifest = CreateManifest("test.denied-type-multi-asm-attack", primaryAssemblyPath, PluginTrustTier.UnsignedLocal, []);

        var registry = new PluginRegistry();
        var logger = new RecordingLevelLogger();
        var recorder = new RecordingComponentPrincipalRecorder();
        var deniedTypeRegistry = new PluginDeniedTypeRegistry();
        var loader = new PluginAssemblyLoader(logger, registry, recorder, deniedTypeRegistry);

        var result = loader.LoadPlugins([manifest]);

        Assert.Empty(result);
        Assert.Equal(PluginRegistryState.TrustDenied, Assert.Single(registry.Entries).State);

        // EvilModule's own assembly was already forced to load by
        // DiscoverModuleTypes's own transitive scan, inside LoadPlugins,
        // above - Assembly.LoadFrom here returns that exact same,
        // already-resident Assembly (the default AssemblyLoadContext
        // caches by path), so this resolves the identical Type reference
        // PluginDeniedTypeRegistry itself recorded.
        var secondaryAssembly = System.Reflection.Assembly.LoadFrom(secondaryAssemblyPath);
        var evilModuleType = secondaryAssembly.GetType(evilModuleFullName, throwOnError: true)!;

        Assert.True(
            deniedTypeRegistry.IsDenied(evilModuleType),
            "EvilModule, discovered only via DiscoverModuleTypes's own transitive scan of a secondary, " +
            "undeclared assembly, must be recorded denied - not only the primary assembly's own module set.");
    }

    [Fact]
    public void EnforceTrust_LegitimateMultiAssemblyPlugin_BothCompliant_StillLoads()
    {
        using var temp = new TempDirectory();

        // A genuinely compliant secondary assembly: its own IModule type's
        // constructor uses only the fixed, always-allowed baseline - no
        // forbidden or ungranted parameter type anywhere.
        var secondaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            temp.Path,
            "CompliantSecondary",
            "SharedBaseType",
            "SecondaryModule",
            "test.secondary-module",
            "Secondary Module",
            "1.0.0",
            [typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider)]);

        var secondaryAssemblyName = Path.GetFileNameWithoutExtension(secondaryAssemblyPath);
        var externalBaseTypeFullName = $"{secondaryAssemblyName}.SharedBaseType";

        var primaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyDerivingFromExternalBaseType(
            temp.Path,
            "CompliantPrimary.dll",
            secondaryAssemblyPath,
            externalBaseTypeFullName,
            moduleId: "test.primary-module",
            moduleName: "Primary Module",
            moduleVersion: "1.0.0",
            implementIModule: true);

        var manifest = CreateManifest("test.multi-asm-legit", primaryAssemblyPath, PluginTrustTier.UnsignedLocal, []);

        var loader = new PluginAssemblyLoader();

        var result = loader.LoadPlugins([manifest]);

        // The fix does not overreach: a legitimate multi-assembly plugin,
        // both of whose module constructors comply with the fixed baseline,
        // still loads successfully.
        Assert.Single(result);
    }

    [Fact]
    public void EnforceTrust_LegitimateMultiAssemblyPlugin_RecordsComponentPrincipal_ForModuleTypesInBothAssemblies()
    {
        using var temp = new TempDirectory();

        var secondaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            temp.Path,
            "PrincipalSecondary",
            "PrincipalBaseType",
            "PrincipalSecondaryModule",
            "test.secondary-principal",
            "Secondary Principal Module",
            "1.0.0",
            [typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider)]);

        var secondaryAssemblyName = Path.GetFileNameWithoutExtension(secondaryAssemblyPath);
        var externalBaseTypeFullName = $"{secondaryAssemblyName}.PrincipalBaseType";

        var primaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyDerivingFromExternalBaseType(
            temp.Path,
            "PrincipalPrimary.dll",
            secondaryAssemblyPath,
            externalBaseTypeFullName,
            moduleId: "test.primary-principal",
            moduleName: "Primary Principal Module",
            moduleVersion: "1.0.0",
            implementIModule: true);

        var manifest = CreateManifest(
            "test.multi-asm-principal", primaryAssemblyPath, PluginTrustTier.UnsignedLocal, [PluginCapability.Navigation]);

        var recorder = new RecordingComponentPrincipalRecorder();
        var loader = new PluginAssemblyLoader(componentPrincipalRecorder: recorder);

        var result = loader.LoadPlugins([manifest]);

        Assert.Single(result);

        // A component principal is recorded once per discovered module
        // Type, across every scanned assembly - not only the primary,
        // manifest-declared one. Both recorded principals carry the exact
        // same plugin identity and granted permission set.
        Assert.Equal(2, recorder.Recorded.Count);
        Assert.All(recorder.Recorded, recorded => Assert.Equal("test.multi-asm-principal", recorded.Principal.Identity.Id));
        Assert.All(
            recorder.Recorded,
            recorded => Assert.Contains(
                new Tempest.Core.Identity.Permission(PluginCapability.Navigation), recorded.Principal.Permissions));

        var recordedAssemblies = recorder.Recorded.Select(recorded => recorded.ModuleType.Assembly).Distinct().ToList();
        Assert.Equal(2, recordedAssemblies.Count);
    }

    // ------------------------------------------------------------------
    // WP 13.9.3 security remediation. WP 13.9.2's own Security/Trust
    // re-execution found WP 13.9.1's fix above closed only the specific,
    // inheritance-triggered variant: DiscoverModuleTypes's own AppDomain
    // diff wraps Assembly.GetTypes() alone, but HasCompliantConstructor's
    // own, later, separate reflection over each discovered module type's
    // constructor parameters is an equally unavoidable CLR assembly-load
    // trigger, invisible to that earlier diff. These tests reproduce the
    // second mechanism directly - a plugin module's own constructor
    // *parameter* references a type in a second, undeclared assembly, not
    // a base-type inheritance relationship - proving the fixed-point scan
    // now closes both mechanisms, not merely the one WP 13.9.0 originally
    // demonstrated.
    // ------------------------------------------------------------------

    [Fact]
    public void EnforceTrust_NonCompliantConstructorReferencesExternalAssembly_IsDenied_SecondaryEvilModuleNeverPrincipaled()
    {
        using var temp = new TempDirectory();

        var secondaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            temp.Path,
            "CtorParamSecondary",
            "SharedParameterType",
            "EvilModule",
            "test.ctor-param-evil-module",
            "Evil Module",
            "1.0.0",
            [typeof(global::Tempest.Core.Identity.CurrentComponentAccessor)]);

        var secondaryAssemblyName = Path.GetFileNameWithoutExtension(secondaryAssemblyPath);
        var externalParameterTypeFullName = $"{secondaryAssemblyName}.SharedParameterType";

        // The primary, manifest-declared assembly: UnsignedLocal tier, zero
        // requested capabilities. Its own module's sole constructor takes a
        // plain parameter of the external, undeclared assembly's own type -
        // no inheritance relationship anywhere - which is non-compliant by
        // construction (never in the always-allowed baseline, never
        // granted).
        var primaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyWithExternalConstructorParameter(
            temp.Path,
            "CtorParamPrimary.dll",
            secondaryAssemblyPath,
            externalParameterTypeFullName,
            moduleId: "test.ctor-param-attack",
            moduleName: "Constructor Parameter Attack",
            moduleVersion: "1.0.0",
            addAlternateCompliantConstructor: false);

        var manifest = CreateManifest("test.ctor-param-attack-plugin", primaryAssemblyPath, PluginTrustTier.UnsignedLocal, []);

        var registry = new PluginRegistry();
        var logger = new RecordingLevelLogger();
        var recorder = new RecordingComponentPrincipalRecorder();
        var loader = new PluginAssemblyLoader(logger, registry, recorder);

        var result = loader.LoadPlugins([manifest]);

        Assert.Empty(result);
        var entry = Assert.Single(registry.Entries);
        Assert.Equal(PluginRegistryState.TrustDenied, entry.State);
        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("test.ctor-param-attack-plugin", StringComparison.Ordinal));

        // The secondary assembly's own EvilModule - forced into the
        // AppDomain by resolving the primary module's own non-compliant
        // constructor parameter - is discovered by the same scan and never
        // gets a principal recorded, whether or not it would itself have
        // been individually compliant.
        Assert.Empty(recorder.Recorded);
    }

    [Fact]
    public void EnforceTrust_ModuleWithAlternateCompliantConstructor_StillDenied_NotSilentlyAcceptedViaOtherOverload()
    {
        using var temp = new TempDirectory();

        var secondaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            temp.Path,
            "AltCtorSecondary",
            "SharedParameterType",
            "EvilModule",
            "test.alt-ctor-evil-module",
            "Evil Module",
            "1.0.0",
            [typeof(global::Tempest.Core.Identity.CurrentComponentAccessor)]);

        var secondaryAssemblyName = Path.GetFileNameWithoutExtension(secondaryAssemblyPath);
        var externalParameterTypeFullName = $"{secondaryAssemblyName}.SharedParameterType";

        // The primary module now ALSO has a second, entirely parameterless
        // (therefore trivially compliant) constructor. Before WP 13.9.3,
        // HasCompliantConstructor would find this second overload compliant
        // and accept the whole plugin - silently, with zero denial, zero
        // registry trace - while the first, non-compliant overload's own
        // parameter-type resolution still smuggled the secondary assembly
        // in. This is the more severe of the two WP 13.9.2 scenarios.
        var primaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyWithExternalConstructorParameter(
            temp.Path,
            "AltCtorPrimary.dll",
            secondaryAssemblyPath,
            externalParameterTypeFullName,
            moduleId: "test.alt-ctor-attack",
            moduleName: "Alternate Constructor Attack",
            moduleVersion: "1.0.0",
            addAlternateCompliantConstructor: true);

        var manifest = CreateManifest("test.alt-ctor-attack-plugin", primaryAssemblyPath, PluginTrustTier.UnsignedLocal, []);

        var registry = new PluginRegistry();
        var recorder = new RecordingComponentPrincipalRecorder();
        var loader = new PluginAssemblyLoader(registryRecorder: registry, componentPrincipalRecorder: recorder);

        var result = loader.LoadPlugins([manifest]);

        // The whole plugin is denied - not silently accepted via its own
        // alternate, compliant overload - because the secondary assembly's
        // own EvilModule is discovered by the same fixed-point scan and is
        // itself non-compliant, regardless of which of the primary
        // module's own two constructors would individually have passed.
        Assert.Empty(result);
        Assert.Equal(PluginRegistryState.TrustDenied, Assert.Single(registry.Entries).State);
        Assert.Empty(recorder.Recorded);
    }

    [Fact]
    public void EnforceTrust_ThreeAssemblyTransitiveConstructorParameterChain_EvilModuleInThirdAssembly_IsDenied()
    {
        using var temp = new TempDirectory();

        // C: the innermost, wholly undeclared assembly - never referenced
        // by the primary assembly at all, reached only via B's own
        // constructor parameter.
        var assemblyCPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            temp.Path,
            "TransitiveC",
            "SharedParameterType",
            "EvilModule",
            "test.transitive-evil-module",
            "Evil Module",
            "1.0.0",
            [typeof(global::Tempest.Core.Identity.CurrentComponentAccessor)]);
        var assemblyCName = Path.GetFileNameWithoutExtension(assemblyCPath);
        var cParameterTypeFullName = $"{assemblyCName}.SharedParameterType";

        // B: an intermediate, also-undeclared assembly, reached by A via
        // plain base-type inheritance (the WP 13.9.1-proven, already-safe
        // linking mechanism - deliberately different from the B->C hop
        // below, so this test proves the fix's own transitivity across a
        // genuinely mixed chain, not merely one repeated mechanism). B's
        // own separate module references C via ITS OWN constructor
        // parameter - the new, WP 13.9.3 mechanism - so C is reached only
        // by scanning B's own module's constructor in a later fixed-point
        // iteration, never directly by A. B's own reference to C's shared
        // type is explicitly granted below (FirstParty tier has no fixed
        // ceiling beyond the closed key-shape set), so B's own module is
        // individually compliant on its own merits - isolating the test to
        // genuinely prove multi-hop transitivity finds C, rather than
        // merely tripping on B's own, closer non-compliance.
        var assemblyBPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            temp.Path,
            "TransitiveB",
            "SharedBaseType",
            "TransitiveBModule",
            "test.transitive-b-module",
            "Transitive B Module",
            "1.0.0",
            [ResolveExternalType(assemblyCPath, cParameterTypeFullName)]);
        var assemblyBName = Path.GetFileNameWithoutExtension(assemblyBPath);
        var bBaseTypeFullName = $"{assemblyBName}.SharedBaseType";

        // A: the primary, manifest-declared assembly - its own module
        // inherits from B's own plain base type, forcing B into the
        // AppDomain; B's own separate module (referencing C) is then
        // discovered and scanned in a later fixed-point iteration, exactly
        // like any other transitively-loaded assembly.
        var assemblyAPath = DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyDerivingFromExternalBaseType(
            temp.Path,
            "TransitiveA.dll",
            assemblyBPath,
            bBaseTypeFullName,
            moduleId: "test.transitive-a-attack",
            moduleName: "Transitive A Attack",
            moduleVersion: "1.0.0",
            implementIModule: false);

        var manifest = CreateManifest(
            "test.transitive-attack-plugin",
            assemblyAPath,
            PluginTrustTier.FirstParty,
            [PluginCapability.ServiceResolve(cParameterTypeFullName)]);

        var registry = new PluginRegistry();
        var recorder = new RecordingComponentPrincipalRecorder();
        var loader = new PluginAssemblyLoader(registryRecorder: registry, componentPrincipalRecorder: recorder);

        var result = loader.LoadPlugins([manifest]);

        // The fixed-point scan is genuinely transitive, not one-hop-only:
        // A's and B's own modules are each individually compliant (their
        // own external references are explicitly granted) - only C's own
        // EvilModule, reached two hops away and never itself compliant, is
        // left to deny the whole plugin.
        Assert.Empty(result);
        Assert.Equal(PluginRegistryState.TrustDenied, Assert.Single(registry.Entries).State);
        Assert.Empty(recorder.Recorded);
    }

    [Fact]
    public void EnforceTrust_LegitimateConstructorParameterReferencesGrantedExternalType_StillLoads()
    {
        using var temp = new TempDirectory();

        // A genuinely compliant secondary assembly: no IModule implementer
        // beyond a plain shared type used only as a constructor parameter.
        var secondaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            temp.Path,
            "LegitCtorParamSecondary",
            "SharedServiceType",
            "SecondaryModule",
            "test.legit-ctor-param-secondary-module",
            "Secondary Module",
            "1.0.0",
            [typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider)]);

        var secondaryAssemblyName = Path.GetFileNameWithoutExtension(secondaryAssemblyPath);
        var externalParameterTypeFullName = $"{secondaryAssemblyName}.SharedServiceType";

        // The primary module's own constructor takes the external shared
        // type as a parameter - non-compliant unless explicitly granted via
        // plugin.services.resolve:<FullTypeName>, exactly as it is here.
        var primaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyWithExternalConstructorParameter(
            temp.Path,
            "LegitCtorParamPrimary.dll",
            secondaryAssemblyPath,
            externalParameterTypeFullName,
            moduleId: "test.legit-ctor-param-primary-module",
            moduleName: "Primary Module",
            moduleVersion: "1.0.0",
            addAlternateCompliantConstructor: false);

        var manifest = CreateManifest(
            "test.legit-ctor-param-plugin",
            primaryAssemblyPath,
            PluginTrustTier.FirstParty,
            [PluginCapability.ServiceResolve(externalParameterTypeFullName)]);

        var loader = new PluginAssemblyLoader();

        var result = loader.LoadPlugins([manifest]);

        // The fix does not overreach: a legitimate plugin whose own module
        // constructor references an external assembly's type it has been
        // explicitly, correctly granted still loads successfully.
        Assert.Single(result);
    }

    /// <summary>
    /// Resolves a plain (non-<see cref="Modules.IModule"/>-implementing)
    /// type by full name from an already-saved assembly file, via a
    /// temporary, dedicated <see cref="AssemblyLoadContext"/> - mirroring
    /// <see cref="DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyDerivingFromExternalBaseType"/>'s
    /// own established, proven-safe reflection-load pattern for a builder
    /// method (<see cref="DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule"/>)
    /// that itself expects a real <see cref="Type"/>, not a full name.
    /// </summary>
    private static Type ResolveExternalType(string assemblyPath, string typeFullName)
    {
        var reflectionLoadContext = new AssemblyLoadContext($"ReflectionOnly-{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            var assembly = reflectionLoadContext.LoadFromAssemblyPath(assemblyPath);
            return assembly.GetType(typeFullName, throwOnError: true)!;
        }
        finally
        {
            reflectionLoadContext.Unload();
        }
    }

    private static PluginManifest CreateManifest(
        string id, string assemblyPath, PluginTrustTier trustTier, IReadOnlyList<string> requestedCapabilities) =>
        new(id, $"{id} name", "1.0.0", new Version(0, 1, 0), Path.GetFileName(assemblyPath), assemblyPath,
            trustTier, requestedCapabilities: requestedCapabilities);

    private sealed class RecordingComponentPrincipalRecorder : IPluginComponentPrincipalRecorder
    {
        public List<(Type ModuleType, Tempest.Core.Identity.IPrincipal Principal)> Recorded { get; } = [];

        public void Record(Type moduleType, Tempest.Core.Identity.IPrincipal principal) =>
            Recorded.Add((moduleType, principal));
    }
}
