using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using Tempest.Core.Configuration;
using Tempest.Core.Diagnostics;
using Tempest.Core.Logging;
using Tempest.Core.Plugins;
using Tempest.Core.Runtime;
using Tempest.Core.Versioning;
using Xunit.Abstractions;

namespace Tempest.Core.Tests.Plugins;

// WP 13.3A (Performance & Scalability sub-agent): stress/scale tests for the
// plugin platform - Plugin Discovery (ADR-0107), signature verification and
// trust tier assignment (ADR-0112), Plugin Loading (ADR-0110/ADR-0111), and a
// full TempestHost startup with a large plugin set.
//
// These are diagnostic, not a tight benchmark gate: every test reports its
// own actual Stopwatch numbers via ITestOutputHelper (visible with
// `dotnet test --logger "console;verbosity=detailed"`), and asserts only a
// generous upper bound, loose enough not to be flaky on a slower CI box -
// the assertion exists to catch a genuine regression (e.g. an accidental
// change from O(n) to O(n^2)), not to pin down an exact millisecond figure.
//
// [Collection("Console output capture")] mirrors every other TempestHost
// integration test in this suite: it serialises this class's tests against
// the rest of that collection so wall-clock measurements are not skewed by
// unrelated tests contending for the same CPU cores at the same time.
[Collection("Console output capture")]
public class PluginPlatformPerformanceTests
{
    private static readonly IPlatformVersionProvider DefaultVersionProvider =
        new FakePlatformVersionProvider(new Version(1, 0, 0));

    private readonly ITestOutputHelper _output;

    public PluginPlatformPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ------------------------------------------------------------------
    // 1. Plugin Discovery alone (ADR-0107 dependency graph resolution),
    //    against a realistic mix of dependency shapes: independent
    //    candidates, multi-link chains, and a deliberately-unsatisfiable
    //    cascading chain (missing root dependency) so the fixed-point
    //    removal loop in ResolveDependencyGraph runs more than one full
    //    pass. No assembly files are created - AssignTrustTier never reads
    //    assembly bytes for an unsigned candidate under allowUnsignedLoad,
    //    so this isolates the parse + dependency-graph algorithm cost from
    //    both assembly I/O and signature verification.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(50)]
    [InlineData(200)]
    [InlineData(1000)]
    public void DiscoverManifests_MixedDependencyShapes_ScalesAcrossSizes(int scale)
    {
        using var temp = new TempDirectory();

        var shape = BuildMixedDependencyShapeFixture(temp.Path, scale);

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, allowUnsignedLoad: true);

        var stopwatch = Stopwatch.StartNew();
        var result = service.DiscoverManifests();
        stopwatch.Stop();

        _output.WriteLine(
            $"[Discovery] scale={scale} candidates, elapsed={stopwatch.ElapsedMilliseconds}ms " +
            $"(independent={shape.IndependentCount}, chainNodes={shape.ChainNodeCount}, " +
            $"unsatisfiableCascade={shape.UnsatisfiableCount}, survivors={result.Count})");

        Assert.Equal(shape.IndependentCount + shape.ChainNodeCount, result.Count);

        // Generous upper bound, scaled with candidate count - not a tight
        // benchmark gate. Pure in-memory dictionary/list work over a set
        // this size should complete in well under a second on any
        // reasonable machine; this bound is roughly 10x that, to absorb CI
        // noise without masking a genuine algorithmic regression.
        var maxMilliseconds = Math.Max(2_000, scale * 20);
        Assert.True(
            stopwatch.ElapsedMilliseconds < maxMilliseconds,
            $"Discovery of {scale} candidates took {stopwatch.ElapsedMilliseconds}ms, expected under {maxMilliseconds}ms.");
    }

    // WP 13.3B: 0/1-candidate boundaries. BuildMixedDependencyShapeFixture's
    // own chain/cascade arithmetic (Math.Max(1, ...) floors) means the
    // Theory above cannot represent these two boundary sizes faithfully, so
    // they get their own minimal, direct fixtures instead - still exercising
    // the exact same public DiscoverManifests entrypoint.
    [Fact]
    public void DiscoverManifests_ZeroCandidates_ReturnsEmpty_NoException()
    {
        using var temp = new TempDirectory();

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, allowUnsignedLoad: true);
        var result = service.DiscoverManifests();

        Assert.Empty(result);
    }

    [Fact]
    public void DiscoverManifests_SingleCandidate_ReturnsThatOnePluginOnly()
    {
        using var temp = new TempDirectory();

        var folder = CreateCandidateFolder(temp.Path, "only-candidate");
        WriteManifest(folder, PluginSigningTestHelper.BuildDto(
            id: "perf.boundary.single", assemblyFileName: "Plugin.dll", signature: null));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, allowUnsignedLoad: true);
        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal("perf.boundary.single", manifest.Id);
    }

    // ------------------------------------------------------------------
    // 1b. Maximal fan-in on the reverse-adjacency index itself: a single
    //    "hub" candidate with no dependencies of its own, depended upon by
    //    thousands of others - the WP13.3A dependentsByTargetId index holds
    //    exactly one key with an enormous value list, the opposite shape
    //    from the wide-independent case the WP13.3A performance sub-agent's
    //    own headline numbers used. This is a CORRECTNESS check (right
    //    order, right count, nothing silently dropped), not primarily a
    //    speed check - it stress-tests the reverse-index fix's own logic
    //    specifically, since a bug in "record each dependent once per
    //    target" would show up exactly here.
    // ------------------------------------------------------------------

    [Fact]
    public void DiscoverManifests_MaximalFanIn_SingleHubDependedOnByThousands_HubLoadsFirstAllDependentsFollow()
    {
        const int fanOut = 3_000;
        const string hubId = "perf.fanin.hub";

        using var temp = new TempDirectory();

        var hubFolder = CreateCandidateFolder(temp.Path, "0-hub");
        WriteManifest(hubFolder, PluginSigningTestHelper.BuildDto(id: hubId, assemblyFileName: "Plugin.dll", signature: null));

        for (var i = 0; i < fanOut; i++)
        {
            var folder = CreateCandidateFolder(temp.Path, $"1-dep-{i:D6}");
            var dto = PluginSigningTestHelper.BuildDto(
                id: $"perf.fanin.dep.{i:D6}", assemblyFileName: "Plugin.dll", signature: null);
            dto.Dependencies = [new PluginDependencyDto { Id = hubId, MinimumVersion = "1.0.0" }];
            WriteManifest(folder, dto);
        }

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, allowUnsignedLoad: true);

        var stopwatch = Stopwatch.StartNew();
        var result = service.DiscoverManifests();
        stopwatch.Stop();

        _output.WriteLine($"[MaximalFanIn] fanOut={fanOut}, elapsed={stopwatch.ElapsedMilliseconds}ms, count={result.Count}");

        // Nothing silently dropped - every hub dependent survived.
        Assert.Equal(fanOut + 1, result.Count);

        // The hub is the only candidate with zero dependencies at the start,
        // so it is the only possible first emission regardless of tie-break
        // order - proving the reverse index correctly attributes all 5,000
        // dependents to the one target Id, not losing or double-counting any.
        Assert.Equal(hubId, result[0].Id);
        Assert.DoesNotContain(result.Skip(1), m => m.Id == hubId);
        Assert.All(result.Skip(1), m => Assert.StartsWith("perf.fanin.dep.", m.Id, StringComparison.Ordinal));

        var maxMilliseconds = 10_000;
        Assert.True(
            stopwatch.ElapsedMilliseconds < maxMilliseconds,
            $"Maximal fan-in discovery of {fanOut + 1} candidates took {stopwatch.ElapsedMilliseconds}ms, expected under {maxMilliseconds}ms.");
    }

    // ------------------------------------------------------------------
    // 2. Signature verification cost, isolated. Compares a flat,
    //    independent set of N unsigned candidates (allowUnsignedLoad) against
    //    the identical N candidates all signed with the same real, self-signed
    //    certificate and verified against a real PluginTrustStore - the delta
    //    attributes the extra time to File.ReadAllBytes + RSA-PSS
    //    verification per plugin (PluginSignatureVerifier), separated from
    //    manifest parsing and the (here-trivial, dependency-free) graph
    //    resolution cost both runs share.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(300)]
    [InlineData(1000)]
    public void SignatureVerification_LargeSet_IsolatedCost_ScalesAcrossSizes(int scale)
    {
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Perf Test Publisher");

        using var unsignedRoot = new TempDirectory();
        BuildFlatUnsignedFixture(unsignedRoot.Path, scale, assemblyBytesSize: 20_000);

        using var signedRoot = new TempDirectory();
        using var trustRoot = new TempDirectory();
        PluginSigningTestHelper.WriteToTrustStore(trustRoot.Path, certificate, "Publisher.cer");
        BuildFlatSignedFixture(signedRoot.Path, scale, certificate, assemblyBytesSize: 20_000);

        var unsignedService = new PluginManifestDiscoveryService(
            unsignedRoot.Path, DefaultVersionProvider, allowUnsignedLoad: true);
        var unsignedStopwatch = Stopwatch.StartNew();
        var unsignedResult = unsignedService.DiscoverManifests();
        unsignedStopwatch.Stop();

        var trustStore = new PluginTrustStore(trustRoot.Path);
        var signedService = new PluginManifestDiscoveryService(
            signedRoot.Path, DefaultVersionProvider, trustStore: trustStore);
        var signedStopwatch = Stopwatch.StartNew();
        var signedResult = signedService.DiscoverManifests();
        signedStopwatch.Stop();

        var deltaMs = signedStopwatch.ElapsedMilliseconds - unsignedStopwatch.ElapsedMilliseconds;

        _output.WriteLine(
            $"[SignatureVerification] scale={scale}, unsigned={unsignedStopwatch.ElapsedMilliseconds}ms, " +
            $"signed={signedStopwatch.ElapsedMilliseconds}ms, delta(signature-only)~={deltaMs}ms, " +
            $"perPluginSignatureCost~={(scale > 0 ? deltaMs / (double)scale : 0):F3}ms");

        Assert.Equal(scale, unsignedResult.Count);
        Assert.Equal(scale, signedResult.Count);
        Assert.All(signedResult, m => Assert.Equal(PluginTrustTier.VerifiedSigned, m.TrustTier));

        // Generous upper bound on the signed run's own absolute time -
        // File.ReadAllBytes of a small fixture file plus RSA-PSS
        // verification, per plugin, should not remotely approach this.
        var maxMilliseconds = Math.Max(3_000, scale * 30);
        Assert.True(
            signedStopwatch.ElapsedMilliseconds < maxMilliseconds,
            $"Signed discovery of {scale} candidates took {signedStopwatch.ElapsedMilliseconds}ms, expected under {maxMilliseconds}ms.");
    }

    // ------------------------------------------------------------------
    // 3. PluginAssemblyLoader.LoadPlugins for a large set of real, small
    //    DynamicPluginAssemblyBuilder-built assemblies. Assembly construction
    //    happens entirely before the Stopwatch starts - only Assembly.LoadFrom
    //    + EnforceTrust's reflection pass are timed.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(100)]
    [InlineData(400)]
    public void LoadPlugins_LargeSet_ScalesAcrossSizes(int scale)
    {
        using var temp = new TempDirectory();

        var manifests = new List<PluginManifest>(scale);

        for (var i = 0; i < scale; i++)
        {
            var id = $"perf.load.{i:D5}";
            var fileName = $"Plugin{i:D5}.dll";
            var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
                temp.Path, fileName, id, $"Load Perf Plugin {i}", "1.0.0");

            manifests.Add(new PluginManifest(
                id, $"Load Perf Plugin {i}", "1.0.0", new Version(0, 1, 0),
                fileName, assemblyPath, PluginTrustTier.FirstParty));
        }

        var loader = new PluginAssemblyLoader();

        var stopwatch = Stopwatch.StartNew();
        var loaded = loader.LoadPlugins(manifests);
        stopwatch.Stop();

        _output.WriteLine($"[LoadPlugins] scale={scale}, elapsed={stopwatch.ElapsedMilliseconds}ms, loaded={loaded.Count}");

        Assert.Equal(scale, loaded.Count);

        var maxMilliseconds = Math.Max(3_000, scale * 40);
        Assert.True(
            stopwatch.ElapsedMilliseconds < maxMilliseconds,
            $"Loading {scale} plugin assemblies took {stopwatch.ElapsedMilliseconds}ms, expected under {maxMilliseconds}ms.");
    }

    // ------------------------------------------------------------------
    // 3b. WP 13.10B: the widened DiscoverModuleTypes scan's own added cost,
    //    isolated. Every fixture above (and #3's own LoadPlugins_LargeSet)
    //    builds each plugin with exactly one module, an implicit
    //    zero-parameter constructor, and no secondary/transitive assembly -
    //    so WP 13.9.1's fixed-point BFS assembly scan and WP 13.9.3's forced
    //    constructor-ParameterType resolution loop never had their own
    //    actual incremental cost measured at any scale; only plugin COUNT,
    //    the one dimension neither change added cost to, was ever scaled.
    //
    //    This isolates the ParameterType-resolution loop's own marginal
    //    cost: a flat, zero-constructor-parameter baseline set is loaded and
    //    timed first, then an identically-sized set whose every module has a
    //    moderate-but-nontrivial (8) constructor parameter count - all
    //    drawn from PluginAssemblyLoader's own AlwaysAllowedConstructorBaseline
    //    (ADR-0111), so every plugin still reaches Loaded, exactly like the
    //    zero-parameter baseline - isolating the parameter-count dimension
    //    alone, mirroring SignatureVerification_LargeSet_IsolatedCost's own
    //    delta-isolation technique above.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(50)]
    [InlineData(200)]
    public void LoadPlugins_ManyConstructorParametersPerModule_IsolatesParameterResolutionCost(int scale)
    {
        using var zeroParamRoot = new TempDirectory();
        using var manyParamRoot = new TempDirectory();

        // Eight baseline-compliant constructor parameter types - a
        // realistic, nontrivial count reached purely by repeating the three
        // types PluginAssemblyLoader.AlwaysAllowedConstructorBaseline always
        // permits (ADR-0111), so every "many-parameter" module still passes
        // HasCompliantConstructor with zero requested capabilities, exactly
        // like the zero-parameter baseline - the only varying dimension is
        // parameter COUNT, never compliance outcome.
        Type[] manyParameterTypes =
        [
            typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider),
            typeof(ILogger), typeof(IConfigurationProvider), typeof(IDiagnosticsProvider),
            typeof(ILogger), typeof(IConfigurationProvider),
        ];

        var zeroParamManifests = new List<PluginManifest>(scale);
        var manyParamManifests = new List<PluginManifest>(scale);

        for (var i = 0; i < scale; i++)
        {
            var zeroId = $"perf.ctorparams.zero.{i:D5}";
            var zeroFileName = $"ZeroParam{i:D5}.dll";
            var zeroAssemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
                zeroParamRoot.Path, zeroFileName, zeroId, $"Zero Param Plugin {i}", "1.0.0");
            zeroParamManifests.Add(new PluginManifest(
                zeroId, $"Zero Param Plugin {i}", "1.0.0", new Version(0, 1, 0),
                zeroFileName, zeroAssemblyPath, PluginTrustTier.FirstParty));

            var manyId = $"perf.ctorparams.many.{i:D5}";
            var manyFileName = $"ManyParam{i:D5}.dll";
            var manyAssemblyPath = DynamicPluginAssemblyBuilder.BuildPluginAssemblyWithConstructorParameters(
                manyParamRoot.Path, manyFileName, manyId, $"Many Param Plugin {i}", "1.0.0", manyParameterTypes);
            manyParamManifests.Add(new PluginManifest(
                manyId, $"Many Param Plugin {i}", "1.0.0", new Version(0, 1, 0),
                manyFileName, manyAssemblyPath, PluginTrustTier.FirstParty));
        }

        var zeroStopwatch = Stopwatch.StartNew();
        var zeroLoaded = new PluginAssemblyLoader().LoadPlugins(zeroParamManifests);
        zeroStopwatch.Stop();

        var manyStopwatch = Stopwatch.StartNew();
        var manyLoaded = new PluginAssemblyLoader().LoadPlugins(manyParamManifests);
        manyStopwatch.Stop();

        var deltaMs = manyStopwatch.ElapsedMilliseconds - zeroStopwatch.ElapsedMilliseconds;
        var perParameterMs = scale > 0 ? deltaMs / (double)(scale * manyParameterTypes.Length) : 0;

        _output.WriteLine(
            $"[ConstructorParameters] scale={scale}, parametersPerModule={manyParameterTypes.Length}, " +
            $"zeroParam={zeroStopwatch.ElapsedMilliseconds}ms, manyParam={manyStopwatch.ElapsedMilliseconds}ms, " +
            $"delta~={deltaMs}ms, perPluginPerParameterCost~={perParameterMs:F4}ms");

        Assert.Equal(scale, zeroLoaded.Count);
        Assert.Equal(scale, manyLoaded.Count);

        // Generous upper bound on the many-parameter run's own absolute
        // time - well under a genuine regression threshold, loose enough
        // not to be flaky on a slower CI box.
        var maxMilliseconds = Math.Max(3_000, scale * 60);
        Assert.True(
            manyStopwatch.ElapsedMilliseconds < maxMilliseconds,
            $"Loading {scale} plugins with {manyParameterTypes.Length} constructor parameters each took " +
            $"{manyStopwatch.ElapsedMilliseconds}ms, expected well under {maxMilliseconds}ms.");
    }

    // ------------------------------------------------------------------
    // 3c. WP 13.10B: the widened DiscoverModuleTypes scan's own added cost,
    //    second dimension - deep transitive assembly chains. Each plugin's
    //    primary assembly derives from a chain of secondaryAssemblyCount
    //    transitively-loaded secondary assemblies, reusing
    //    BuildPrimaryPluginAssemblyDerivingFromExternalBaseType/
    //    BuildSecondaryAssemblyWithBaseTypeAndModule's own chaining pattern
    //    exactly as PluginAssemblyLoaderMultiAssemblyTrustTests's own
    //    three-assembly EnforceTrust_ThreeAssemblyTransitiveConstructorParameterChain_...
    //    test does, generalised to an arbitrary depth and granted throughout
    //    so every plugin genuinely reaches Loaded (never TrustDenied) -
    //    isolating WP 13.9.1's own fixed-point BFS scan cost at a
    //    moderate, clearly-labelled scale, not a denial path.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(20, 2)]
    [InlineData(40, 4)]
    public void LoadPlugins_DeepTransitiveAssemblyChain_MeasuresFixedPointScanCost(int scale, int secondaryAssemblyCount)
    {
        using var temp = new TempDirectory();

        var manifests = new List<PluginManifest>(scale);

        for (var i = 0; i < scale; i++)
            manifests.Add(BuildTransitiveChainManifest(temp.Path, secondaryAssemblyCount, i));

        var loader = new PluginAssemblyLoader();

        var stopwatch = Stopwatch.StartNew();
        var loaded = loader.LoadPlugins(manifests);
        stopwatch.Stop();

        _output.WriteLine(
            $"[TransitiveChain] scale={scale}, secondaryAssembliesPerPlugin={secondaryAssemblyCount} " +
            $"(totalAssembliesPerPlugin={secondaryAssemblyCount + 1}), elapsed={stopwatch.ElapsedMilliseconds}ms, " +
            $"loaded={loaded.Count}, perPluginCost~={(scale > 0 ? stopwatch.Elapsed.TotalMilliseconds / scale : 0):F3}ms");

        Assert.Equal(scale, loaded.Count);

        // Generous upper bound, scaled with both plugin count and chain
        // depth - not a tight benchmark gate, see this class's own header
        // remarks; loose enough to absorb CI noise without masking a
        // genuine algorithmic regression in the fixed-point scan.
        var maxMilliseconds = Math.Max(5_000, scale * secondaryAssemblyCount * 100);
        Assert.True(
            stopwatch.ElapsedMilliseconds < maxMilliseconds,
            $"Loading {scale} plugins each with a {secondaryAssemblyCount + 1}-assembly transitive chain took " +
            $"{stopwatch.ElapsedMilliseconds}ms, expected well under {maxMilliseconds}ms.");
    }

    /// <summary>
    /// Builds one plugin manifest whose primary assembly derives from a
    /// chain of <paramref name="secondaryAssemblyCount"/> transitively-loaded
    /// secondary assemblies - each one's own module referencing the next,
    /// deeper assembly's own marker type via a constructor parameter
    /// (WP 13.9.3's own mechanism, granted via a matching
    /// <c>plugin.services.resolve:*</c> capability so it remains compliant),
    /// the shallowest (outermost) one's own marker type reached by the
    /// primary assembly via plain base-type inheritance (WP 13.9.1's own
    /// mechanism, which needs no capability grant) - the same two linking
    /// mechanisms <c>PluginAssemblyLoaderMultiAssemblyTrustTests</c>'s own
    /// three-assembly transitive chain test exercises, generalised here to
    /// an arbitrary depth and kept fully compliant throughout, so
    /// <see cref="PluginAssemblyLoader.LoadPlugins"/> genuinely reaches
    /// <see cref="PluginRegistryState.Loaded"/> for every plugin this
    /// builds - isolating the fixed-point scan's own traversal cost, not a
    /// denial path.
    /// </summary>
    private static PluginManifest BuildTransitiveChainManifest(string root, int secondaryAssemblyCount, int index)
    {
        var grantedCapabilities = new List<string>();

        string? deeperAssemblyPath = null;
        string? deeperMarkerTypeFullName = null;

        for (var depth = secondaryAssemblyCount - 1; depth >= 0; depth--)
        {
            var namePrefix = $"PerfChain{index}_{depth}";

            Type[] moduleConstructorParameterTypes = deeperAssemblyPath is null
                ? Type.EmptyTypes
                : [ResolveExternalType(deeperAssemblyPath, deeperMarkerTypeFullName!)];

            var assemblyPath = DynamicPluginAssemblyBuilder.BuildSecondaryAssemblyWithBaseTypeAndModule(
                root, namePrefix, "SharedMarkerType", "ChainModule",
                $"perf.chain.{index}.{depth}", $"Chain Module {index}-{depth}", "1.0.0",
                moduleConstructorParameterTypes);

            if (deeperMarkerTypeFullName is not null)
                grantedCapabilities.Add(PluginCapability.ServiceResolve(deeperMarkerTypeFullName));

            var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            deeperAssemblyPath = assemblyPath;
            deeperMarkerTypeFullName = $"{assemblyName}.SharedMarkerType";
        }

        var primaryAssemblyPath = DynamicPluginAssemblyBuilder.BuildPrimaryPluginAssemblyDerivingFromExternalBaseType(
            root, $"PerfChainPrimary{index}.dll", deeperAssemblyPath!, deeperMarkerTypeFullName!,
            moduleId: $"perf.chain.{index}.primary", moduleName: $"Chain Primary {index}", moduleVersion: "1.0.0",
            implementIModule: false);

        return new PluginManifest(
            $"perf.chain.{index}", $"Chain Plugin {index}", "1.0.0", new Version(0, 1, 0),
            Path.GetFileName(primaryAssemblyPath), primaryAssemblyPath, PluginTrustTier.FirstParty,
            requestedCapabilities: grantedCapabilities);
    }

    /// <summary>
    /// Resolves <paramref name="typeFullName"/> from the already-saved
    /// assembly at <paramref name="assemblyPath"/> via a temporary,
    /// dedicated <see cref="AssemblyLoadContext"/> - mirrors
    /// <c>PluginAssemblyLoaderMultiAssemblyTrustTests</c>'s own identically-named,
    /// identically-shaped helper exactly, duplicated here rather than shared
    /// (this file owns no production or shared-fixture code beyond its own
    /// two WP 13.10B-owned test files).
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

    // ------------------------------------------------------------------
    // 4. Full TempestHost startup with a large plugin set - wall-clock from
    //    RunAsync() to HostState.Running, covering Plugin Discovery, Plugin
    //    Loading, and Module Discovery together. Plugin folder/assembly
    //    fixture construction happens entirely before the Stopwatch starts.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(100)]
    [InlineData(300)]
    public async Task FullHostStartup_LargePluginSet_MeasuresWallClockToRunning(int scale)
    {
        using var temp = new TempDirectory();

        for (var i = 0; i < scale; i++)
        {
            var folder = Path.Combine(temp.Path, $"plugin-{i:D5}");
            Directory.CreateDirectory(folder);

            var id = $"perf.host.{i:D5}";
            var fileName = $"Plugin{i:D5}.dll";
            DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(folder, fileName, id, $"Host Perf Plugin {i}", "1.0.0");

            File.WriteAllText(
                Path.Combine(folder, PluginManifestDiscoveryService.ManifestFileName),
                $$"""
                {
                  "Id": "{{id}}",
                  "Name": "Host Perf Plugin {{i}}",
                  "Version": "1.0.0",
                  "MinimumPlatformVersion": "0.1.0",
                  "AssemblyFileName": "{{fileName}}"
                }
                """);
        }

        var builder = new TempestHostBuilder(Type.EmptyTypes, temp.Path);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Plugins:AllowUnsignedLoad", "true"),
        ]));
        var host = builder.Build();

        var stopwatch = Stopwatch.StartNew();
        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        stopwatch.Stop();

        _output.WriteLine($"[FullHostStartup] scale={scale}, elapsed={stopwatch.ElapsedMilliseconds}ms, state={host.State}");

        Assert.Equal(HostState.Running, host.State);

        await host.StopAsync();
        await runTask;

        var maxMilliseconds = Math.Max(5_000, scale * 60);
        Assert.True(
            stopwatch.ElapsedMilliseconds < maxMilliseconds,
            $"Host startup with {scale} plugins took {stopwatch.ElapsedMilliseconds}ms, expected under {maxMilliseconds}ms.");
    }

    // ------------------------------------------------------------------
    // 5. Behavioural equivalence (WP 13.3B): the production TopologicalSort
    //    - already carrying WP13.3A's own reverse-adjacency-index change,
    //    reached here via reflection since the method is private - compared
    //    against a freshly hand-written NAIVE reference that reimplements
    //    the exact PRE-WP13.3A "rescan every survivor's own Dependencies on
    //    every emission" approach (see this project's own git history for
    //    the change being reversed here). Equal output (same Ids, same
    //    order) across several genuinely different dependency shapes is the
    //    actual proof the optimisation altered only internal bookkeeping,
    //    never externally observable behaviour - stronger evidence than
    //    "no exception was thrown" or a wall-clock number alone.
    // ------------------------------------------------------------------

    [Fact]
    public void TopologicalSort_WideIndependentShape_MatchesNaiveFullRescanReference()
    {
        var (acceptedById, orderedIds) = BuildInMemoryFixture(BuildWideIndependentEdges(300));
        AssertProductionMatchesNaiveReference(acceptedById, orderedIds);
    }

    [Fact]
    public void TopologicalSort_LongChainShape_MatchesNaiveFullRescanReference()
    {
        var (acceptedById, orderedIds) = BuildInMemoryFixture(BuildChainEdges(120));
        AssertProductionMatchesNaiveReference(acceptedById, orderedIds);
    }

    [Fact]
    public void TopologicalSort_DiamondFanInShape_MatchesNaiveFullRescanReference()
    {
        var (acceptedById, orderedIds) = BuildInMemoryFixture(BuildDiamondEdges(diamondCount: 60));
        AssertProductionMatchesNaiveReference(acceptedById, orderedIds);
    }

    [Fact]
    public void TopologicalSort_DuplicateDeclaredDependencyShape_MatchesNaiveFullRescanReference()
    {
        // A handful of candidates each declare the SAME dependency Id twice
        // - the exact "one graph edge, declared redundantly" case the
        // production code's own remarks call out by name (distinct-target
        // counting, not raw Dependencies.Count).
        var edges = BuildChainEdges(40);
        var duplicated = edges.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Length > 0 ? pair.Value.Concat(pair.Value).ToArray() : pair.Value,
            StringComparer.Ordinal);

        var (acceptedById, orderedIds) = BuildInMemoryFixture(duplicated);
        AssertProductionMatchesNaiveReference(acceptedById, orderedIds);
    }

    // ------------------------------------------------------------------
    // 5b. WP 13.9.1 (Verification/Test Remediation sub-agent): a real,
    //    persisted, randomized differential test - restoring, as a genuine
    //    committed artefact, the verification value `WP13.3B`'s own commit
    //    message claimed ("20,000-trial differential fuzz harness... zero
    //    mismatches, zero nondeterminism") but that `WP13.9.0`'s
    //    Implementation and Verification disciplines both independently
    //    confirmed does not exist anywhere in this repository or its
    //    history - only the four fixed-shape equivalence tests above.
    //
    //    10,000 trials, fixed seed 20130 (chosen only for reproducibility;
    //    it carries no other significance). Each trial builds a fresh,
    //    randomly-shaped acyclic dependency graph - node count, edge
    //    density, presence of duplicate dependency declarations, and
    //    candidate ordering (via BuildInMemoryFixture's own existing
    //    folder-order scramble) all vary trial-to-trial - and asserts the
    //    real, current, production TopologicalSort (via reflection, the
    //    same technique the four tests above already use) exactly matches
    //    NaiveFullRescanTopologicalSort's independent reimplementation.
    //    Measured on this machine: ~10,000 trials complete in well under
    //    two seconds (see this test's own recorded Stopwatch output) - see
    //    this Work Package's own report for the exact figure.
    // ------------------------------------------------------------------

    [Fact]
    public void TopologicalSort_RandomizedTrials_MatchesNaiveFullRescanReference()
    {
        const int trialCount = 10_000;
        const int seed = 20130;

        var random = new Random(Seed: seed);
        var stopwatch = Stopwatch.StartNew();

        for (var trial = 0; trial < trialCount; trial++)
        {
            var nodeCount = random.Next(1, 40);
            var edgeDensity = random.NextDouble() * 0.5;
            var allowDuplicateDeclarations = random.Next(2) == 0;

            var edges = BuildRandomDependencyEdges(random, nodeCount, edgeDensity, allowDuplicateDeclarations);
            var (acceptedById, orderedIds) = BuildInMemoryFixture(edges);

            AssertProductionMatchesNaiveReference(acceptedById, orderedIds);
        }

        stopwatch.Stop();

        _output.WriteLine(
            $"[RandomizedDifferential] trials={trialCount}, seed={seed}, " +
            $"elapsed={stopwatch.ElapsedMilliseconds}ms, " +
            $"perTrial~={stopwatch.Elapsed.TotalMilliseconds / trialCount:F4}ms");
    }

    /// <summary>
    /// Builds a randomly-shaped, guaranteed-acyclic dependency edge map for
    /// <see cref="TopologicalSort_RandomizedTrials_MatchesNaiveFullRescanReference"/>:
    /// <paramref name="nodeCount"/> nodes, each node <c>i</c> only ever able
    /// to depend on an already-generated node <c>j &lt; i</c> (by
    /// construction - no cycle is ever possible, exactly as a real,
    /// dependency-resolved plugin set never contains one), each candidate
    /// edge included independently with probability <paramref name="edgeDensity"/>.
    /// When <paramref name="allowDuplicateDeclarations"/> is <see langword="true"/>,
    /// roughly a third of included edges are declared twice in the same
    /// node's own dependency array - the same "one graph edge, declared
    /// redundantly" shape
    /// <see cref="TopologicalSort_DuplicateDeclaredDependencyShape_MatchesNaiveFullRescanReference"/>
    /// already covers, now varied randomly rather than fixed.
    /// </summary>
    private static Dictionary<string, string[]> BuildRandomDependencyEdges(
        Random random, int nodeCount, double edgeDensity, bool allowDuplicateDeclarations)
    {
        var ids = Enumerable.Range(0, nodeCount).Select(i => $"rand.{i:D4}").ToArray();
        var edges = new Dictionary<string, string[]>(StringComparer.Ordinal);

        for (var i = 0; i < nodeCount; i++)
        {
            var dependencyIds = new List<string>();

            for (var j = 0; j < i; j++)
            {
                if (random.NextDouble() >= edgeDensity)
                    continue;

                dependencyIds.Add(ids[j]);

                if (allowDuplicateDeclarations && random.NextDouble() < 0.3)
                    dependencyIds.Add(ids[j]);
            }

            edges[ids[i]] = dependencyIds.ToArray();
        }

        return edges;
    }

    /// <summary>
    /// Invokes both the real, private, production <c>TopologicalSort</c>
    /// (via reflection - the exact same method every real Plugin Discovery
    /// run calls) and <see cref="NaiveFullRescanTopologicalSort"/> over the
    /// identical input, and asserts their own returned Id sequences are
    /// exactly equal - same members, same order.
    /// </summary>
    private static void AssertProductionMatchesNaiveReference(
        Dictionary<string, PluginManifest> acceptedById, List<string> orderedIds)
    {
        var productionResult = InvokeProductionTopologicalSort(acceptedById, orderedIds);
        var naiveResult = NaiveFullRescanTopologicalSort(acceptedById, orderedIds);

        Assert.Equal(naiveResult, productionResult, StringComparer.Ordinal);
    }

    private static List<string> InvokeProductionTopologicalSort(
        Dictionary<string, PluginManifest> acceptedById, List<string> orderedIds)
    {
        var method = typeof(PluginManifestDiscoveryService).GetMethod(
            "TopologicalSort", BindingFlags.Static | BindingFlags.NonPublic)!;

        var result = (List<PluginManifest>)method.Invoke(null, [acceptedById, orderedIds])!;
        return result.Select(m => m.Id).ToList();
    }

    /// <summary>
    /// A deliberately independent reimplementation of the PRE-WP13.3A
    /// <c>TopologicalSort</c> algorithm: Kahn's algorithm with the same
    /// distinct-target dependency counting, but rediscovering each just-
    /// emitted node's own dependents by rescanning every remaining
    /// survivor's <see cref="PluginManifest.Dependencies"/> directly, rather
    /// than consulting a reverse-adjacency index built up front. Used purely
    /// as this test's own equivalence oracle - never shared with, and never
    /// read by, production code.
    /// </summary>
    private static List<string> NaiveFullRescanTopologicalSort(
        Dictionary<string, PluginManifest> acceptedById, List<string> orderedIds)
    {
        var survivors = orderedIds.Where(acceptedById.ContainsKey).ToList();

        var remainingDependencyCount = survivors.ToDictionary(
            id => id,
            id => acceptedById[id].Dependencies.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count(),
            StringComparer.Ordinal);

        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(survivors.Count);

        while (emitted.Count < survivors.Count)
        {
            var next = survivors.FirstOrDefault(id => !emitted.Contains(id) && remainingDependencyCount[id] == 0);

            if (next is null)
                break;

            emitted.Add(next);
            result.Add(next);

            foreach (var id in survivors)
            {
                if (emitted.Contains(id))
                    continue;

                if (acceptedById[id].Dependencies.Any(dependency => dependency.Id == next))
                    remainingDependencyCount[id]--;
            }
        }

        return result;
    }

    private static (Dictionary<string, PluginManifest> AcceptedById, List<string> OrderedIds) BuildInMemoryFixture(
        Dictionary<string, string[]> dependencyIdsByNodeId)
    {
        var acceptedById = new Dictionary<string, PluginManifest>(StringComparer.Ordinal);

        // Folder-enumeration order deliberately scrambled relative to
        // insertion order - proving equivalence isn't an accident of both
        // implementations happening to see candidates in the same sequence.
        var orderedIds = dependencyIdsByNodeId.Keys.OrderBy(_ => _random.Next()).ToList();

        foreach (var (id, dependencyIds) in dependencyIdsByNodeId)
        {
            var dependencies = dependencyIds
                .Select(dependencyId => new PluginDependency(dependencyId, new Version(1, 0, 0), null))
                .ToList();

            acceptedById[id] = new PluginManifest(
                id, "Bench", "1.0.0", new Version(0, 1, 0), "Plugin.dll", "Plugin.dll",
                PluginTrustTier.FirstParty, dependencies);
        }

        return (acceptedById, orderedIds);
    }

    private static readonly Random _random = new(Seed: 987654);

    private static Dictionary<string, string[]> BuildWideIndependentEdges(int count) =>
        Enumerable.Range(0, count).ToDictionary(
            i => $"indep.{i:D5}", _ => Array.Empty<string>(), StringComparer.Ordinal);

    private static Dictionary<string, string[]> BuildChainEdges(int length)
    {
        var edges = new Dictionary<string, string[]>(StringComparer.Ordinal);
        string? previousId = null;

        for (var i = 0; i < length; i++)
        {
            var id = $"chain.{i:D5}";
            edges[id] = previousId is null ? [] : [previousId];
            previousId = id;
        }

        return edges;
    }

    /// <summary>
    /// Builds <paramref name="diamondCount"/> independent four-node
    /// diamonds: top depends on nothing; left and right both depend on top;
    /// bottom depends on BOTH left and right - the classic converging-fan-in
    /// shape (two distinct incoming edges into one node) exercised at scale.
    /// </summary>
    private static Dictionary<string, string[]> BuildDiamondEdges(int diamondCount)
    {
        var edges = new Dictionary<string, string[]>(StringComparer.Ordinal);

        for (var i = 0; i < diamondCount; i++)
        {
            var top = $"diamond.{i:D5}.top";
            var left = $"diamond.{i:D5}.left";
            var right = $"diamond.{i:D5}.right";
            var bottom = $"diamond.{i:D5}.bottom";

            edges[top] = [];
            edges[left] = [top];
            edges[right] = [top];
            edges[bottom] = [left, right];
        }

        return edges;
    }

    // ------------------------------------------------------------------
    // Fixture builders
    // ------------------------------------------------------------------

    private sealed record MixedShapeFixture(int IndependentCount, int ChainNodeCount, int UnsatisfiableCount);

    /// <summary>
    /// Builds a realistic mix of dependency shapes under <paramref name="root"/>:
    /// ~60% independent candidates with no dependencies, ~30% distributed
    /// across several short dependency chains (proving the topological sort
    /// and fixed-point loop handle ordinary linked dependencies at scale),
    /// and a deliberately-unsatisfiable cascading chain whose root dependency
    /// does not exist - every node in that cascade depends on the previous
    /// one, so <c>RemoveCandidatesWithUnmetDependencies</c>'s own do/while
    /// loop must run more than one internal pass to remove them all
    /// (removing the node closest to the missing root only makes the next
    /// node's own dependency newly unmet, one link at a time).
    /// </summary>
    private static MixedShapeFixture BuildMixedDependencyShapeFixture(string root, int scale)
    {
        var random = new Random(Seed: 12345);

        var unsatisfiableCount = Math.Max(6, scale / 40);
        var chainNodeTarget = (int)(scale * 0.3);
        const int chainLength = 5;
        var chainCount = Math.Max(1, chainNodeTarget / chainLength);
        var actualChainNodeCount = chainCount * chainLength;
        var independentCount = Math.Max(0, scale - actualChainNodeCount - unsatisfiableCount);

        // Build a randomised folder-name ordering so the final result order
        // proves the topological sort - not folder enumeration order -
        // decided load order, exactly as the existing, smaller-scale
        // PluginDependencyGraphResolutionTests already do deliberately.
        var slots = Enumerable.Range(0, independentCount + actualChainNodeCount + unsatisfiableCount)
            .OrderBy(_ => random.Next())
            .ToList();
        var slotIndex = 0;

        for (var i = 0; i < independentCount; i++)
        {
            var folder = CreateCandidateFolder(root, $"slot-{slots[slotIndex++]:D6}");
            WriteManifest(folder, PluginSigningTestHelper.BuildDto(
                id: $"perf.independent.{i:D6}", assemblyFileName: "Plugin.dll", signature: null));
        }

        for (var chain = 0; chain < chainCount; chain++)
        {
            string? previousId = null;

            for (var link = 0; link < chainLength; link++)
            {
                var id = $"perf.chain.{chain:D4}.{link:D2}";
                var folder = CreateCandidateFolder(root, $"slot-{slots[slotIndex++]:D6}");

                var dto = PluginSigningTestHelper.BuildDto(id: id, assemblyFileName: "Plugin.dll", signature: null);

                if (previousId is not null)
                {
                    dto.Dependencies =
                    [
                        new PluginDependencyDto { Id = previousId, MinimumVersion = "1.0.0" },
                    ];
                }

                WriteManifest(folder, dto);
                previousId = id;
            }
        }

        string? previousCascadeId = null;

        for (var i = 0; i < unsatisfiableCount; i++)
        {
            var id = $"perf.unsatisfiable.{i:D4}";
            var folder = CreateCandidateFolder(root, $"slot-{slots[slotIndex++]:D6}");

            var dto = PluginSigningTestHelper.BuildDto(id: id, assemblyFileName: "Plugin.dll", signature: null);

            // The first cascade node depends on an id that genuinely does
            // not exist anywhere in this fixture; every subsequent node
            // depends on the previous cascade node, so removing node 0
            // (missing dependency) makes node 1's own dependency newly
            // unmet on the *next* fixed-point pass, and so on down the
            // chain - forcing multiple passes rather than a single one.
            var dependsOn = previousCascadeId ?? "perf.does-not-exist.root";
            dto.Dependencies = [new PluginDependencyDto { Id = dependsOn, MinimumVersion = "1.0.0" }];

            WriteManifest(folder, dto);
            previousCascadeId = id;
        }

        return new MixedShapeFixture(independentCount, actualChainNodeCount, unsatisfiableCount);
    }

    /// <summary>
    /// Builds <paramref name="count"/> flat, independent, unsigned candidate
    /// folders (no dependencies) with a small dummy "assembly" file of
    /// <paramref name="assemblyBytesSize"/> bytes each - not a real .NET
    /// assembly, since signature verification and this fixture's own
    /// unsigned counterpart both only ever read the file's raw bytes
    /// (<see cref="File.ReadAllBytes(string)"/>), never load it.
    /// </summary>
    private static void BuildFlatUnsignedFixture(string root, int count, int assemblyBytesSize)
    {
        for (var i = 0; i < count; i++)
        {
            var folder = CreateCandidateFolder(root, $"perf-unsigned-{i:D6}");
            File.WriteAllBytes(Path.Combine(folder, "Plugin.dll"), BuildDummyAssemblyBytes(assemblyBytesSize, i));

            WriteManifest(folder, PluginSigningTestHelper.BuildDto(
                id: $"perf.signature.{i:D6}", assemblyFileName: "Plugin.dll", signature: null));
        }
    }

    /// <summary>
    /// Builds <paramref name="count"/> flat, independent candidate folders,
    /// each with a small dummy "assembly" file signed with
    /// <paramref name="certificate"/>'s own private key via a real ADR-0112
    /// signature envelope (<see cref="PluginSigningTestHelper"/>) - the exact
    /// production verification path
    /// (<see cref="PluginManifestDiscoveryService"/>'s private
    /// <c>VerifySignature</c>) runs unmodified against each one.
    /// </summary>
    private static void BuildFlatSignedFixture(string root, int count, X509Certificate2 certificate, int assemblyBytesSize)
    {
        for (var i = 0; i < count; i++)
        {
            var folder = CreateCandidateFolder(root, $"perf-signed-{i:D6}");
            var assemblyPath = Path.Combine(folder, "Plugin.dll");
            File.WriteAllBytes(assemblyPath, BuildDummyAssemblyBytes(assemblyBytesSize, i));

            var dto = PluginSigningTestHelper.BuildDto(id: $"perf.signature.{i:D6}", assemblyFileName: "Plugin.dll");
            dto.Signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(dto, assemblyPath, certificate);

            WriteManifest(folder, dto);
        }
    }

    private static byte[] BuildDummyAssemblyBytes(int size, int seed)
    {
        var bytes = new byte[size];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    private static string CreateCandidateFolder(string root, string folderName)
    {
        var path = Path.Combine(root, folderName);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteManifest(string candidateFolder, PluginManifestDto dto) =>
        File.WriteAllText(
            Path.Combine(candidateFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginSigningTestHelper.ToManifestJson(dto));
}
