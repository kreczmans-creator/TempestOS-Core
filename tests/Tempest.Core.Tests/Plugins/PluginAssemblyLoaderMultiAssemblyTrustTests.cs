using System.Runtime.Loader;
using Tempest.Core.BackgroundServices;
using Tempest.Core.Configuration;
using Tempest.Core.Diagnostics;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
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

    // ------------------------------------------------------------------
    // WP 13.10B (TD-51): PluginAssemblyLoader.EnforceTrust's own
    // constructor-conformance check previously consulted moduleTypes only -
    // an IHostedService-only plugin (zero discovered IModule types)
    // short-circuited straight past it, so a plugin whose sole hosted
    // service's own constructor required a forbidden, denylisted parameter
    // type would have been silently accepted. These two tests prove the fix
    // directly at the PluginAssemblyLoader level: the denial case, and the
    // positive/no-regression case (which also proves component-principal
    // recording was correctly extended to hosted-service types - before this
    // fix, this exact positive scenario would have recorded ZERO principals).
    // ------------------------------------------------------------------

    [Fact]
    public void EnforceTrust_HostedServiceOnlyPlugin_NonCompliantConstructor_IsDenied_RecordsTrustDenied()
    {
        using var temp = new TempDirectory();

        // Zero IModule types anywhere in this assembly - before WP 13.10B,
        // moduleTypes.Count == 0 meant EnforceTrust's own constructor check
        // (moduleTypes.FirstOrDefault(...)) never even looked at this type,
        // regardless of how non-compliant its own sole constructor was.
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildHostedServiceOnlyAssemblyWithConstructorParameters(
            temp.Path,
            "HostedServiceOnlyDenied.dll",
            [typeof(global::Tempest.Core.Identity.CurrentComponentAccessor)]);

        var manifest = CreateManifest("test.hosted-service-only-denied-plugin", assemblyPath, PluginTrustTier.UnsignedLocal, []);

        var registry = new PluginRegistry();
        var logger = new RecordingLevelLogger();
        var recorder = new RecordingComponentPrincipalRecorder();
        var loader = new PluginAssemblyLoader(logger, registry, recorder);

        var result = loader.LoadPlugins([manifest]);

        Assert.Empty(result);
        var entry = Assert.Single(registry.Entries);
        Assert.Equal(PluginRegistryState.TrustDenied, entry.State);
        Assert.NotNull(entry.Detail);
        Assert.Contains("no public constructor", entry.Detail!, StringComparison.OrdinalIgnoreCase);

        // Neither survives: no component principal is ever recorded for the
        // denied plugin's own hosted service type.
        Assert.Empty(recorder.Recorded);
    }

    [Fact]
    public void EnforceTrust_HostedServiceOnlyPlugin_CompliantConstructor_Loads_RecordsComponentPrincipal()
    {
        using var temp = new TempDirectory();

        var assemblyPath = DynamicPluginAssemblyBuilder.BuildCompliantHostedServiceOnlyAssembly(
            temp.Path, "HostedServiceOnlyCompliant.dll");

        var manifest = CreateManifest("test.hosted-service-only-compliant-plugin", assemblyPath, PluginTrustTier.UnsignedLocal, []);

        var recorder = new RecordingComponentPrincipalRecorder();
        var loader = new PluginAssemblyLoader(componentPrincipalRecorder: recorder);

        var result = loader.LoadPlugins([manifest]);

        Assert.Single(result);

        // Before WP 13.10B's fix, EnforceTrust's own principal-recording loop
        // ("foreach (var type in moduleTypes)") never iterated hosted-service
        // types at all - this exact, wholly compliant IHostedService-only
        // plugin would have recorded ZERO principals despite passing trust
        // cleanly.
        var recorded = Assert.Single(recorder.Recorded);
        Assert.Equal("test.hosted-service-only-compliant-plugin", recorded.Principal.Identity.Id);

        var loadedAssembly = System.Reflection.Assembly.LoadFrom(assemblyPath);
        var hostedServiceType = loadedAssembly.GetTypes()
            .Single(type => typeof(IHostedService).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
        Assert.Equal(hostedServiceType, recorded.ModuleType);
    }

    // ------------------------------------------------------------------
    // WP 13.10B cheap-hardening item: DiscoverModuleTypes's own forced
    // ParameterType resolution loop is now wrapped in a try/catch, isolating
    // ONE malformed plugin's own resolution failure into a
    // PluginTrustDeniedException rather than letting the raw
    // TypeLoadException/FileNotFoundException/FileLoadException/
    // BadImageFormatException escape LoadPlugins entirely and abort every
    // other plugin's own loading in the same call. This is the single most
    // important non-vacuousness proof in this file.
    // ------------------------------------------------------------------

    [Fact]
    public void LoadPlugins_FirstPluginHasUnresolvableConstructorParameterType_IsolatesFailure_SecondLegitimatePluginStillLoads()
    {
        using var temp = new TempDirectory();

        // The external, dependency assembly is deliberately saved to a
        // DIFFERENT directory than the primary plugin assembly below - the
        // default AssemblyLoadContext's own directory-probing for an
        // Assembly.LoadFrom-loaded assembly's own referenced dependencies
        // only ever searches the referencing assembly's own directory (the
        // exact mechanism BuildSecondaryAssemblyWithBaseTypeAndModule's own
        // remarks rely on, in every OTHER test in this file, to make a
        // secondary assembly discoverable - deliberately inverted here to
        // make this parameter type's own resolution genuinely UNresolvable),
        // so forcing it throws FileNotFoundException/TypeLoadException.
        var externalOnlyDirectory = Path.Combine(temp.Path, "external-unreachable");
        Directory.CreateDirectory(externalOnlyDirectory);

        var secondaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            externalOnlyDirectory,
            "UnresolvableSecondary",
            "SharedParameterType",
            "InertModule",
            "test.unresolvable-secondary-module",
            "Inert Secondary Module",
            "1.0.0",
            [typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider)]);

        var secondaryAssemblyName = Path.GetFileNameWithoutExtension(secondaryAssemblyPath);
        var externalParameterTypeFullName = $"{secondaryAssemblyName}.SharedParameterType";

        var unresolvablePrimaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyWithExternalConstructorParameter(
            temp.Path,
            "UnresolvableCtorParamPrimary.dll",
            secondaryAssemblyPath,
            externalParameterTypeFullName,
            moduleId: "test.unresolvable-ctor-param-module",
            moduleName: "Unresolvable Constructor Parameter Module",
            moduleVersion: "1.0.0",
            addAlternateCompliantConstructor: false);

        var legitimateAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "IsolationLegitimate.dll", "test.isolation-legitimate", "Isolation Legitimate Plugin", "1.0.0");

        var unresolvableManifest = CreateManifest(
            "test.unresolvable-ctor-param-plugin", unresolvablePrimaryAssemblyPath, PluginTrustTier.UnsignedLocal, []);
        var legitimateManifest = CreateManifest(
            "test.isolation-legitimate-plugin", legitimateAssemblyPath, PluginTrustTier.UnsignedLocal, []);

        var registry = new PluginRegistry();
        var logger = new RecordingLevelLogger();
        var loader = new PluginAssemblyLoader(logger, registry);

        // Deliberately in this order: before WP 13.10B's fix, the first
        // plugin's own unresolvable-type exception was not a PluginException,
        // so it propagated straight out of LoadPlugins, aborting every other
        // plugin's own loading in the same call - not just this one's.
        var result = loader.LoadPlugins([unresolvableManifest, legitimateManifest]);

        Assert.Single(result);
        Assert.Equal(2, registry.Entries.Count);

        var unresolvableEntry = registry.Entries.Single(e => e.Id == "test.unresolvable-ctor-param-plugin");
        Assert.Equal(PluginRegistryState.TrustDenied, unresolvableEntry.State);
        Assert.NotNull(unresolvableEntry.Detail);
        Assert.Contains("could not be resolved", unresolvableEntry.Detail!, StringComparison.OrdinalIgnoreCase);

        var legitimateEntry = registry.Entries.Single(e => e.Id == "test.isolation-legitimate-plugin");
        Assert.Equal(PluginRegistryState.Loaded, legitimateEntry.State);
    }

    /// <summary>
    /// The <see cref="BackgroundServices.IHostedService"/>-only counterpart
    /// to <see cref="LoadPlugins_FirstPluginHasUnresolvableConstructorParameterType_IsolatesFailure_SecondLegitimatePluginStillLoads"/>
    /// - WP 13.10C's own Verification/RAM-concurrency reviewer found the
    /// twice-found, twice-fixed regression (an unresolvable constructor
    /// parameter type on an <see cref="BackgroundServices.IHostedService"/>-only
    /// plugin faulting the whole Host, rather than isolating just that one
    /// plugin) was proven fixed only via throwaway, non-permanent
    /// proof-of-concept code across two independent reviewers - genuinely
    /// closed in production (<see cref="PluginAssemblyLoader.DiscoverModuleTypes"/>'s
    /// own forced-resolution loop now iterates both <c>moduleTypes</c> and
    /// <c>hostedServiceTypes</c> uniformly), but with zero permanent
    /// regression coverage protecting the specific axis two independent
    /// reviewers each had to rediscover by hand. This test closes that gap
    /// permanently, mirroring the sibling <see cref="Modules.IModule"/> test
    /// immediately above exactly, substituting
    /// <see cref="DynamicPluginAssemblyBuilder.BuildHostedServiceOnlyAssemblyWithConstructorParameters"/>
    /// for the <see cref="Modules.IModule"/>-shaped builder it uses.
    /// </summary>
    [Fact]
    public void LoadPlugins_FirstHostedServiceOnlyPluginHasUnresolvableConstructorParameterType_IsolatesFailure_SecondLegitimatePluginStillLoads()
    {
        using var temp = new TempDirectory();

        // Deliberately saved to a DIFFERENT directory than the primary
        // plugin assembly below, for the identical reason the IModule
        // sibling test does this - the default AssemblyLoadContext's own
        // directory-probing for an Assembly.LoadFrom-loaded assembly's own
        // referenced dependencies only ever searches the referencing
        // assembly's own directory, so this makes the parameter type's own
        // resolution genuinely UNresolvable.
        var externalOnlyDirectory = Path.Combine(temp.Path, "external-unreachable-hs");
        Directory.CreateDirectory(externalOnlyDirectory);

        var secondaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            externalOnlyDirectory,
            "UnresolvableSecondaryHS",
            "SharedParameterType",
            "InertModule",
            "test.unresolvable-secondary-hs-module",
            "Inert Secondary Module",
            "1.0.0",
            [typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider)]);

        var secondaryAssemblyName = Path.GetFileNameWithoutExtension(secondaryAssemblyPath);
        var externalParameterTypeFullName = $"{secondaryAssemblyName}.SharedParameterType";
        var externalParameterType = ResolveExternalType(secondaryAssemblyPath, externalParameterTypeFullName);

        var unresolvablePrimaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildHostedServiceOnlyAssemblyWithConstructorParameters(
            temp.Path, "UnresolvableCtorParamHostedServicePrimary.dll", [externalParameterType]);

        var legitimateAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "IsolationLegitimateHS.dll", "test.isolation-legitimate-hs", "Isolation Legitimate Plugin", "1.0.0");

        var unresolvableManifest = CreateManifest(
            "test.unresolvable-ctor-param-hs-plugin", unresolvablePrimaryAssemblyPath, PluginTrustTier.UnsignedLocal, []);
        var legitimateManifest = CreateManifest(
            "test.isolation-legitimate-hs-plugin", legitimateAssemblyPath, PluginTrustTier.UnsignedLocal, []);

        var registry = new PluginRegistry();
        var logger = new RecordingLevelLogger();
        var loader = new PluginAssemblyLoader(logger, registry);

        // Deliberately in this order, for the identical reason the IModule
        // sibling test orders its own two plugins this way: before the
        // WP 13.10B fix's own hostedServiceTypes extension, the first
        // plugin's own unresolvable-type exception was not a
        // PluginException, so it propagated straight out of LoadPlugins,
        // aborting every other plugin's own loading in the same call - not
        // just this one's.
        var result = loader.LoadPlugins([unresolvableManifest, legitimateManifest]);

        Assert.Single(result);
        Assert.Equal(2, registry.Entries.Count);

        var unresolvableEntry = registry.Entries.Single(e => e.Id == "test.unresolvable-ctor-param-hs-plugin");
        Assert.Equal(PluginRegistryState.TrustDenied, unresolvableEntry.State);
        Assert.NotNull(unresolvableEntry.Detail);
        Assert.Contains("could not be resolved", unresolvableEntry.Detail!, StringComparison.OrdinalIgnoreCase);

        var legitimateEntry = registry.Entries.Single(e => e.Id == "test.isolation-legitimate-hs-plugin");
        Assert.Equal(PluginRegistryState.Loaded, legitimateEntry.State);
    }

    // ------------------------------------------------------------------
    // WP 13.11B (TD-51, reopened by WP 13.11A). The two WP 13.10B/13.10C
    // tests immediately above prove ONE malformed plugin's own unresolvable
    // constructor-parameter type is isolated rather than aborting
    // LoadPlugins - but neither passes an IPluginDeniedTypeRecorder to the
    // loader at all, so neither could ever have observed what WP 13.11A's
    // own Security/Adversarial reviewer found: DiscoverModuleTypes threw
    // from inside its own forced-resolution loop, strictly BEFORE
    // EnforceTrust ever reached either RecordDenied call site, so nothing
    // whatsoever was recorded for this one denial reason. TempestHost's own
    // WP 13.9.6 Module Discovery filter (isTypeExcluded:
    // deniedTypeRegistry.IsDenied) therefore never excluded the offending
    // type, and ReflectionFrameworkDiscoveryService.CreateDescriptor's own
    // type.GetConstructor(Type.EmptyTypes) call rethrew the identical CLR
    // type-load failure uncaught, faulting the whole Host - reachable by a
    // single, otherwise-inert IModule type, any trust tier, zero requested
    // capabilities. These four tests are the permanent regression coverage
    // for that recording gap, on both discovery axes, and in both
    // directions (denied AND legitimate).
    // ------------------------------------------------------------------

    [Fact]
    public void LoadPlugins_UnresolvableConstructorParameterType_ModuleAxis_RecordsDeniedModuleType()
    {
        using var temp = new TempDirectory();

        // The identical unreachable-directory mechanism the two tests above
        // use: the default AssemblyLoadContext's own directory-probing for
        // an Assembly.LoadFrom-loaded assembly's referenced dependencies
        // only ever searches the referencing assembly's own directory, so a
        // dependency saved anywhere else is genuinely UNresolvable.
        var externalOnlyDirectory = Path.Combine(temp.Path, "td51-module-axis-unreachable");
        Directory.CreateDirectory(externalOnlyDirectory);

        var secondaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            externalOnlyDirectory,
            "Td51ModuleAxisSecondary",
            "SharedParameterType",
            "InertModule",
            "test.td51-module-axis-secondary-module",
            "Inert Secondary Module",
            "1.0.0",
            [typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider)]);

        var secondaryAssemblyName = Path.GetFileNameWithoutExtension(secondaryAssemblyPath);

        // Unattributed (DefineStringProperty only, no ModuleMetadataAttribute)
        // and with no parameterless overload - the exact shape WP 13.11A
        // reproduced, and the only shape that reaches CreateDescriptor's own
        // type.GetConstructor(Type.EmptyTypes) call at all.
        var primaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyWithExternalConstructorParameter(
            temp.Path,
            "Td51ModuleAxisPrimary.dll",
            secondaryAssemblyPath,
            $"{secondaryAssemblyName}.SharedParameterType",
            moduleId: "test.td51-module-axis-module",
            moduleName: "TD-51 Module Axis Module",
            moduleVersion: "1.0.0",
            addAlternateCompliantConstructor: false);

        var manifest = CreateManifest(
            "test.td51-module-axis-plugin", primaryAssemblyPath, PluginTrustTier.UnsignedLocal, []);

        var registry = new PluginRegistry();
        var recorder = new RecordingComponentPrincipalRecorder();
        var deniedTypeRegistry = new PluginDeniedTypeRegistry();
        var loader = new PluginAssemblyLoader(new RecordingLevelLogger(), registry, recorder, deniedTypeRegistry);

        Assert.Empty(loader.LoadPlugins([manifest]));

        var entry = Assert.Single(registry.Entries);
        Assert.Equal(PluginRegistryState.TrustDenied, entry.State);
        Assert.NotNull(entry.Detail);
        Assert.Contains("could not be resolved", entry.Detail!, StringComparison.OrdinalIgnoreCase);

        // The primary assembly is already resident - LoadPlugins loaded it
        // via Assembly.LoadFrom above, and the default AssemblyLoadContext
        // caches by path - so this yields the identical Type reference
        // PluginDeniedTypeRegistry recorded. GetTypes() itself is safe here:
        // it resolves base types and implemented interfaces, never
        // constructor signatures.
        var moduleType = System.Reflection.Assembly.LoadFrom(primaryAssemblyPath).GetTypes()
            .Single(type => typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

        // The load-bearing assertion. Before WP 13.11B, DiscoverModuleTypes
        // threw before EnforceTrust could ever reach RecordDenied, so this
        // registry was empty for this denial reason - and TempestHost's own
        // Module Discovery filter, keyed entirely off it, could not possibly
        // have excluded this type.
        Assert.True(
            deniedTypeRegistry.IsDenied(moduleType),
            "A module type whose own constructor parameter could not be resolved must be recorded denied - " +
            "the denial originates inside DiscoverModuleTypes, so recording must happen for it too, not only " +
            "at EnforceTrust's own two later RecordDenied call sites.");

        // A denied plugin is never granted a component principal, on this
        // denial path either.
        Assert.Empty(recorder.Recorded);
    }

    [Fact]
    public void LoadPlugins_UnresolvableConstructorParameterType_HostedServiceAxis_RecordsDeniedHostedServiceType()
    {
        using var temp = new TempDirectory();

        var externalOnlyDirectory = Path.Combine(temp.Path, "td51-hs-axis-unreachable");
        Directory.CreateDirectory(externalOnlyDirectory);

        var secondaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            externalOnlyDirectory,
            "Td51HostedServiceAxisSecondary",
            "SharedParameterType",
            "InertModule",
            "test.td51-hs-axis-secondary-module",
            "Inert Secondary Module",
            "1.0.0",
            [typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider)]);

        var secondaryAssemblyName = Path.GetFileNameWithoutExtension(secondaryAssemblyPath);
        var externalParameterType = ResolveExternalType(
            secondaryAssemblyPath, $"{secondaryAssemblyName}.SharedParameterType");

        // Zero IModule types anywhere in this assembly - so if the fix ever
        // regressed to recording moduleTypes only, forgetting
        // hostedServiceTypes, this is the one test that catches it.
        var primaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildHostedServiceOnlyAssemblyWithConstructorParameters(
            temp.Path, "Td51HostedServiceAxisPrimary.dll", [externalParameterType]);

        var manifest = CreateManifest(
            "test.td51-hs-axis-plugin", primaryAssemblyPath, PluginTrustTier.UnsignedLocal, []);

        var registry = new PluginRegistry();
        var recorder = new RecordingComponentPrincipalRecorder();
        var deniedTypeRegistry = new PluginDeniedTypeRegistry();
        var loader = new PluginAssemblyLoader(new RecordingLevelLogger(), registry, recorder, deniedTypeRegistry);

        Assert.Empty(loader.LoadPlugins([manifest]));

        var entry = Assert.Single(registry.Entries);
        Assert.Equal(PluginRegistryState.TrustDenied, entry.State);
        Assert.NotNull(entry.Detail);
        Assert.Contains("could not be resolved", entry.Detail!, StringComparison.OrdinalIgnoreCase);

        var hostedServiceType = System.Reflection.Assembly.LoadFrom(primaryAssemblyPath).GetTypes()
            .Single(type => typeof(IHostedService).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

        Assert.True(
            deniedTypeRegistry.IsDenied(hostedServiceType),
            "A hosted-service-only plugin's own type must be recorded denied on this path too - " +
            "TempestHost's own Hosted Service Registration filter is keyed entirely off this registry.");

        Assert.Empty(recorder.Recorded);
    }

    [Fact]
    public void LoadPlugins_UnresolvableConstructorParameterType_RecordedTypeIsExcludedByModuleDiscoveryPredicate()
    {
        using var temp = new TempDirectory();

        var externalOnlyDirectory = Path.Combine(temp.Path, "td51-boundary-unreachable");
        Directory.CreateDirectory(externalOnlyDirectory);

        var secondaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            externalOnlyDirectory,
            "Td51BoundarySecondary",
            "SharedParameterType",
            "InertModule",
            "test.td51-boundary-secondary-module",
            "Inert Secondary Module",
            "1.0.0",
            [typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider)]);

        var secondaryAssemblyName = Path.GetFileNameWithoutExtension(secondaryAssemblyPath);

        var primaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyWithExternalConstructorParameter(
            temp.Path,
            "Td51BoundaryPrimary.dll",
            secondaryAssemblyPath,
            $"{secondaryAssemblyName}.SharedParameterType",
            moduleId: "test.td51-boundary-module",
            moduleName: "TD-51 Boundary Module",
            moduleVersion: "1.0.0",
            addAlternateCompliantConstructor: false);

        var manifest = CreateManifest(
            "test.td51-boundary-plugin", primaryAssemblyPath, PluginTrustTier.UnsignedLocal, []);

        var deniedTypeRegistry = new PluginDeniedTypeRegistry();
        var loader = new PluginAssemblyLoader(deniedTypeRecorder: deniedTypeRegistry);

        Assert.Empty(loader.LoadPlugins([manifest]));

        var moduleType = System.Reflection.Assembly.LoadFrom(primaryAssemblyPath).GetTypes()
            .Single(type => typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

        Assert.True(
            deniedTypeRegistry.IsDenied(moduleType),
            "The recording half of this boundary must hold before the exclusion half can mean anything.");

        // Wired exactly as TempestHost.cs wires it
        // (isTypeExcluded: deniedTypeRegistry.IsDenied), wrapped only to
        // record which types the predicate was actually asked about - so
        // this proves the exclusion genuinely came from THIS predicate
        // returning true for THIS type, and not from IsValidModuleType
        // having filtered it out earlier, nor from the WP 13.11B discovery
        // guard silently absorbing it.
        var probedTypes = new List<Type>();
        var discovery = new ReflectionFrameworkDiscoveryService(
            isTypeExcluded: type =>
            {
                probedTypes.Add(type);
                return deniedTypeRegistry.IsDenied(type);
            });

        var exception = Record.Exception(() => discovery.DiscoverModules([moduleType]));

        // Before WP 13.11B the predicate returned false, CreateDescriptor
        // was called, and its own unguarded type.GetConstructor(Type.EmptyTypes)
        // threw the raw TypeLoadException/FileNotFoundException that faulted
        // TempestHost.RunAsync.
        Assert.Null(exception);
        Assert.Contains(moduleType, probedTypes);
        Assert.Empty(discovery.DiscoverModules([moduleType]));
    }

    [Fact]
    public void LoadPlugins_LegitimatePluginWithCompliantConstructor_IsNeverRecordedDenied_AndStillDiscoversNormally()
    {
        using var temp = new TempDirectory();

        // The "does the fix overreach?" direction. If RecordDenied were ever
        // hoisted out of the failure path, or if the discovery guard's own
        // catch filter were widened past the four CLR type-load exceptions,
        // both assertions below fail.
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Td51Legitimate.dll", "test.td51-legitimate", "TD-51 Legitimate Plugin", "1.0.0");

        var manifest = CreateManifest(
            "test.td51-legitimate-plugin", assemblyPath, PluginTrustTier.UnsignedLocal, []);

        var registry = new PluginRegistry();
        var deniedTypeRegistry = new PluginDeniedTypeRegistry();
        var loader = new PluginAssemblyLoader(null, registry, null, deniedTypeRegistry);

        Assert.Single(loader.LoadPlugins([manifest]));
        Assert.Equal(PluginRegistryState.Loaded, Assert.Single(registry.Entries).State);

        var moduleType = System.Reflection.Assembly.LoadFrom(assemblyPath).GetTypes()
            .Single(type => typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

        Assert.False(
            deniedTypeRegistry.IsDenied(moduleType),
            "A passing plugin's own module type must never be recorded denied.");

        var discovery = new ReflectionFrameworkDiscoveryService(isTypeExcluded: deniedTypeRegistry.IsDenied);

        var descriptor = Assert.Single(discovery.DiscoverModules([moduleType]));
        Assert.Equal("test.td51-legitimate", descriptor.Id);
        Assert.Equal(moduleType, descriptor.ModuleType);
    }

    /// <summary>
    /// <b>WP 13.11C.</b> The one test that makes <c>WP 13.11B</c>'s own
    /// completed-fixed-point-scan decision observable — the security-critical
    /// half of that fix which, as this Work Package's own Verification review
    /// found, had no regression coverage whatsoever: reverting it to
    /// <c>WP 13.11A</c>'s own recommended partial-list shape left all 2,561
    /// existing tests green, because every other unresolvable-parameter test
    /// deliberately places its secondary assembly somewhere the default
    /// <see cref="AssemblyLoadContext"/> can never probe, so no assembly ever
    /// becomes resident mid-scan and the partial list and the fixed-point list
    /// are byte-identical.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shape that separates them needs both transitive-load mechanisms in
    /// one plugin, and a <i>reachable</i> secondary assembly:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <c>DiscoverModuleTypes</c> takes its step-1 <c>before</c> snapshot, then
    /// calls <see cref="Assembly.GetTypes"/> on the primary — whose anchor
    /// type's own base-type-chain resolution pulls the <b>reachable</b>
    /// secondary assembly into the <c>AppDomain</c>, right there inside the
    /// step, after the snapshot.
    /// </description></item>
    /// <item><description>
    /// The primary's own module type then trips the unresolvable-constructor-
    /// parameter denial in the middle of that same step, <i>before</i> the
    /// step's own end-of-body before/after diff is ever reached.
    /// </description></item>
    /// <item><description>
    /// Under the partial-list shape the scan returns there and then: the diff
    /// never runs, the secondary assembly is never enqueued, its own
    /// <see cref="IModule"/> implementer is never scanned and never recorded
    /// denied — yet it is fully resident in the process (ADR-0015: that cannot
    /// be undone) and fully visible to Module Discovery's own deliberately
    /// plugin-unaware <c>AppDomain</c> scan (ADR-0110). Being well-formed
    /// (parameterless constructor, valid metadata) it would then be
    /// registered and lifecycle-run with a <see langword="null"/>, and
    /// therefore First-Party-treated
    /// (<see cref="PluginTrustPermission.IsFirstParty"/>), ambient component
    /// principal — a silent trust bypass in place of the Host crash
    /// <c>TD-51</c> described, which is strictly worse and not fail-closed.
    /// </description></item>
    /// <item><description>
    /// Letting the scan run on to its own fixed point is what closes it: the
    /// diff still runs, the secondary is enqueued, step 2 scans it, and
    /// <c>RecordDenied</c> covers its module type along with everything else
    /// the plugin reached.
    /// </description></item>
    /// </list>
    /// <para>
    /// Non-vacuity confirmed by construction: with <c>DiscoverModuleTypes</c>'s
    /// own <c>catch</c> reverted to record-partial-and-return, this test fails
    /// on the secondary assembly's own assertion below while every other test
    /// in the suite still passes.
    /// </para>
    /// </remarks>
    [Fact]
    public void LoadPlugins_UnresolvableConstructorParameterType_StillRecordsModuleTypesFromAssembliesLoadedEarlierInTheSameScanStep()
    {
        using var temp = new TempDirectory();

        // The UNREACHABLE tertiary assembly, supplying the constructor
        // parameter type that cannot be resolved - saved where the default
        // AssemblyLoadContext's own directory-probing will never search.
        var unreachableDirectory = Path.Combine(temp.Path, "wp1311c-unreachable");
        Directory.CreateDirectory(unreachableDirectory);

        var unreachableAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            unreachableDirectory,
            "Wp1311cUnreachableTertiary",
            "UnreachableParameterType",
            "UnreachableInertModule",
            "test.wp1311c-unreachable-module",
            "Unreachable Inert Module",
            "1.0.0",
            [typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider)]);

        var unreachableAssemblyName = Path.GetFileNameWithoutExtension(unreachableAssemblyPath);

        // The REACHABLE secondary assembly - deliberately saved ALONGSIDE the
        // primary, so probing genuinely finds it and it genuinely enters the
        // AppDomain mid-scan. Its own InertModule is entirely well-formed: a
        // fully baseline-compliant constructor and valid metadata, so nothing
        // about the module itself would ever stop it running. That is the
        // point - only being recorded denied stops it.
        var reachableAssemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
            temp.Path,
            "Wp1311cReachableSecondary",
            "ReachableBaseType",
            "ReachableInertModule",
            "test.wp1311c-reachable-secondary-module",
            "Reachable Inert Module",
            "1.0.0",
            [typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider)]);

        var reachableAssemblyName = Path.GetFileNameWithoutExtension(reachableAssemblyPath);

        var primaryAssemblyPath = DynamicPluginAssemblyBuilder
            .BuildPrimaryPluginAssemblyWithReachableBaseTypeAnchorAndUnresolvableConstructorParameter(
                temp.Path,
                "Wp1311cPrimary.dll",
                reachableAssemblyPath,
                $"{reachableAssemblyName}.ReachableBaseType",
                unreachableAssemblyPath,
                $"{unreachableAssemblyName}.UnreachableParameterType",
                moduleId: "test.wp1311c-primary-module",
                moduleName: "WP 13.11C Primary Module",
                moduleVersion: "1.0.0");

        var manifest = CreateManifest(
            "test.wp1311c-plugin", primaryAssemblyPath, PluginTrustTier.UnsignedLocal, []);

        var registry = new PluginRegistry();
        var deniedTypeRegistry = new PluginDeniedTypeRegistry();
        var loader = new PluginAssemblyLoader(new RecordingLevelLogger(), registry, null, deniedTypeRegistry);

        Assert.Empty(loader.LoadPlugins([manifest]));

        var entry = Assert.Single(registry.Entries);
        Assert.Equal(PluginRegistryState.TrustDenied, entry.State);
        Assert.Contains("could not be resolved", entry.Detail!, StringComparison.OrdinalIgnoreCase);

        // Sanity: the primary's own module type is recorded denied on any
        // shape of the fix, partial-list or fixed-point alike. This assertion
        // is deliberately NOT the one carrying this test - it passes either
        // way, and is here only to prove the denial itself really happened
        // before the assertion that matters.
        var primaryModuleType = System.Reflection.Assembly.LoadFrom(primaryAssemblyPath).GetTypes()
            .Single(type => typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
        Assert.True(deniedTypeRegistry.IsDenied(primaryModuleType));

        // Proof the reachable secondary genuinely entered the AppDomain as a
        // side effect of the scan - never via any explicit Assembly.LoadFrom
        // of it in this test, and never declared in the manifest. If this
        // fails, the test has stopped exercising its own mechanism and the
        // assertion below would pass vacuously.
        // Explicitly the DEFAULT load context's copy. The builder above also
        // reflection-loads this same assembly into a temporary collectible
        // context to emit IL against its base type; Unload() is asynchronous,
        // so that copy can still be enumerable here under the identical simple
        // name. Only the default-context copy is the one the scan actually
        // pulled in, and only its Type identities are the ones
        // deniedTypeRegistry is keyed on.
        var reachableAssembly = AssemblyLoadContext.Default.Assemblies
            .SingleOrDefault(a => string.Equals(a.GetName().Name, reachableAssemblyName, StringComparison.Ordinal));
        Assert.NotNull(reachableAssembly);

        var reachableInertModuleType = reachableAssembly!.GetTypes()
            .Single(type => typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

        // THE LOAD-BEARING ASSERTION - the only one in the suite that
        // distinguishes WP 13.11B's completed fixed-point scan from
        // WP 13.11A's own recommended partial-list shape. Under the partial
        // list this type is absent from deniedTypeRegistry, is therefore not
        // excluded by TempestHost's own WP 13.9.6 Module Discovery filter or
        // its Module Registration filter, and runs with a null - First-Party-
        // treated - ambient component principal.
        Assert.True(
            deniedTypeRegistry.IsDenied(reachableInertModuleType),
            "A denied plugin's own scan must run to its fixed point: an assembly that entered the AppDomain " +
            "earlier in the SAME scan step must still be scanned and its module types recorded denied. " +
            "Aborting the scan at the denial strands it - resident, un-vetted, and still fully visible to " +
            "Module Discovery's own plugin-unaware AppDomain scan.");
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
