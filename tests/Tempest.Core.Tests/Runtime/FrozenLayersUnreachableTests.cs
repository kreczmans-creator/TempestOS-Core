using System.Net.Sockets;
using System.Reflection;
using System.Xml.Linq;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.Runtime;

/// <summary>
/// `WP 21.5E`'s own scope, widened by the Product Owner mid-review: for
/// every layer frozen out of the `v1.0` build (`ADR-0146`,
/// <c>src/Frozen/README.md</c>) — the inbound REST API
/// (<c>Tempest.Core.Api</c>), plugin *loading* (<c>Tempest.Core.Plugins</c>'s
/// signing/trust/assembly-load half — plugin *manifest discovery* stays
/// live and is not this file's concern) and Licensing
/// (<c>Tempest.Core.Licensing</c>) — prove, with a test, that each is
/// unreachable in a default build and configuration, rather than resting on
/// the freeze README's own word alone.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two different properties, both asserted, because they answer two
/// different questions.</b> The build-graph tests below answer "can this
/// code even run" — <c>src/Frozen/</c> has no project file and is
/// referenced by nothing, so the frozen types do not exist in any assembly
/// this solution produces, which is a stronger guarantee than any runtime
/// configuration flag could be (compare <see cref="DependencyDirectionTests"/>'s
/// own reasoning for testing the built graph, not only the declared one).
/// The live-host test answers "even granting that, does starting this
/// platform normally put anything on the wire" — it starts a real
/// <see cref="ITempestHost"/> with default configuration (no
/// <c>Runtime:RestApi:Enabled</c> set) and confirms nothing answers on the
/// frozen REST API's own documented default port.
/// </para>
/// <para>
/// <b>Not proven here, on purpose.</b> This file does not re-audit
/// <c>src/Frozen/</c>'s own code — the brief this Work Package executes
/// under is explicit that the frozen layers are out of scope for a code
/// review, only for an unreachability proof. It does not exercise Plugin
/// *Discovery* either (that stays live and is covered by
/// <c>PluginManifestDiscoveryServiceTests</c> elsewhere) — only that
/// nothing after discovery can ever load what it finds, which the
/// reflection sweep below proves directly: <c>PluginAssemblyLoader</c>
/// does not exist in any assembly this solution builds, so no manifest,
/// however crafted, can result in code execution.
/// </para>
/// </remarks>
public sealed class FrozenLayersUnreachableTests
{
    /// <summary>
    /// <c>RestApiHostedService.DefaultPort</c>'s own value, as named in
    /// <c>src/Frozen/Tempest.Core.Api/RestApiHostedService.cs</c> — copied
    /// as a literal because that type is not part of any compiled
    /// assembly this test could reference.
    /// </summary>
    private const int FrozenRestApiDefaultPort = 5080;

    /// <summary>
    /// Every type name this file knows to belong exclusively to a frozen
    /// layer (drawn directly from the file names under
    /// <c>src/Frozen/Tempest.Core.Api</c>, <c>Tempest.Core.Plugins</c> and
    /// <c>Tempest.Core.Licensing</c>) — checked against
    /// <see cref="Type.Name"/> (not a full namespace-qualified name),
    /// deliberately: a live type accidentally reusing one of these exact,
    /// distinctive names anywhere in this platform would be at least as
    /// worth failing this test over as the frozen type itself reappearing.
    /// </summary>
    private static readonly string[] FrozenTypeNames =
    [
        // Tempest.Core.Api
        "RestApiHostedService", "ApiRequestHandler",
        // Tempest.Core.Plugins (the loading/trust half; manifest discovery
        // — PluginManifestDiscoveryService, PluginRegistry, PluginManifest
        // — is deliberately absent from this list: it stays live).
        "PluginAssemblyLoader", "PluginAssemblyLoadException", "PluginAssemblyNotFoundException",
        "IPluginAssemblyLoader", "PluginSignatureVerifier", "PluginSignatureEnvelope",
        "PluginSignatureVerificationFailedException", "PluginTrustStore", "IPluginTrustStore",
        "PluginTrustDeniedException", "PluginTrustPermission", "PluginTrustTier", "PluginCapability",
        "PluginComponentPrincipalRegistry", "IPluginComponentPrincipalRegistry", "IPluginComponentPrincipalRecorder",
        "PluginDeniedTypeRegistry", "IPluginDeniedTypeRegistry", "IPluginDeniedTypeRecorder",
        "PluginUnsignedLoadNotAllowedException",
        // Tempest.Core.Licensing
        "ILicense", "ILicenseProvider", "ILicenseValidator", "License", "LicenseDto", "LicenseProvider",
        "LicenseValidationException", "LicenseValidationResult", "LicenseValidator", "LicensingException",
    ];

    // ==================================================================
    // The build graph: does the frozen code exist in anything this
    // solution produces at all?
    // ==================================================================

    [Fact]
    public void TheSolution_ReferencesNoProjectUnderSrcFrozenOrTestsFrozen()
    {
        var slnxPath = Path.Combine(RepositoryPaths.RepositoryRoot, "src", "TempestOS.slnx");
        var solutionDirectory = Path.GetDirectoryName(slnxPath)!;

        var frozenSrc = Path.Combine(RepositoryPaths.RepositoryRoot, "src", "Frozen") + Path.DirectorySeparatorChar;
        var frozenTests = Path.Combine(RepositoryPaths.RepositoryRoot, "tests", "Frozen") + Path.DirectorySeparatorChar;

        var projectPaths = XDocument.Load(slnxPath)
            .Descendants("Project")
            .Select(project => Path.GetFullPath(
                Path.Combine(solutionDirectory, project.Attribute("Path")!.Value.Replace('/', Path.DirectorySeparatorChar))))
            .ToList();

        // Eight today (WP 21.5E's own baseline) - asserted as a floor, not
        // pinned exactly, so this test fails loudly if the solution file
        // itself becomes unreadable rather than silently checking zero
        // projects and passing vacuously.
        Assert.True(projectPaths.Count >= 8, $"Expected at least 8 projects in '{slnxPath}'; found {projectPaths.Count}.");

        foreach (var path in projectPaths)
        {
            Assert.False(
                path.StartsWith(frozenSrc, StringComparison.OrdinalIgnoreCase),
                $"'{path}' is referenced by src/TempestOS.slnx but lives under src/Frozen/.");
            Assert.False(
                path.StartsWith(frozenTests, StringComparison.OrdinalIgnoreCase),
                $"'{path}' is referenced by src/TempestOS.slnx but lives under tests/Frozen/.");
        }
    }

    [Fact]
    public void NoReferencedCsproj_HasAnItemPathMentioningFrozen()
    {
        // Checks every Include/Remove/Path attribute value specifically
        // (Compile, ProjectReference, Reference, and any other MSBuild
        // item), not the file's raw text - Tempest.Core.Tests.csproj's own
        // explanatory comment about ADR-0146 legitimately says "Frozen" in
        // prose, and a plain substring search over the whole file would
        // false-positive on exactly that. An explicit
        // `<Compile Include="../Frozen/...` or
        // `<ProjectReference Include="...Frozen...` would show up as a real
        // match here, which is the realistic way frozen code could sneak
        // back into the build - implicit SDK-style globbing is already
        // directory-scoped and cannot reach src/Frozen/ from any of these
        // projects' own directories, so this specifically catches a
        // hand-added explicit reference, not merely restating that geometry.
        var slnxPath = Path.Combine(RepositoryPaths.RepositoryRoot, "src", "TempestOS.slnx");
        var solutionDirectory = Path.GetDirectoryName(slnxPath)!;

        var projectPaths = XDocument.Load(slnxPath)
            .Descendants("Project")
            .Select(project => Path.GetFullPath(
                Path.Combine(solutionDirectory, project.Attribute("Path")!.Value.Replace('/', Path.DirectorySeparatorChar))))
            .ToList();

        string[] pathAttributeNames = ["Include", "Remove", "Exclude"];

        foreach (var path in projectPaths)
        {
            var offending = XDocument.Load(path)
                .Descendants()
                .SelectMany(element => element.Attributes())
                .Where(attribute => pathAttributeNames.Contains(attribute.Name.LocalName, StringComparer.Ordinal))
                .Where(attribute => attribute.Value.Contains("Frozen", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.True(offending.Count == 0,
                $"'{path}' has an item whose path mentions 'Frozen': " +
                string.Join(", ", offending.Select(a => $"{a.Parent!.Name.LocalName}[{a.Name.LocalName}='{a.Value}']")));
        }
    }

    [Fact]
    public void NoLiveAssembly_DeclaresAnyFrozenPluginLicensingOrRestApiType()
    {
        // One anchor type per src/ assembly Tempest.Core.Tests can
        // reference (Tempest.Desktop is Avalonia-only and is covered
        // separately, in Tempest.Desktop.Tests) - the same "typeof(...).Assembly"
        // pattern DependencyDirectionTests and ModuleMetadataCoverageTests
        // already use, so this reads what these assemblies actually
        // contain, not what their own project files merely declare.
        var assemblies = new[]
        {
            typeof(ITempestHost).Assembly,
            typeof(Tempest.Samples.AuditSampleModule).Assembly,
            typeof(Tempest.Workspace.Calculations.CalculationsWorkspaceExplorerModule).Assembly,
            typeof(Tempest.Validation.FaultInjection.DuplicateNavigationModule).Assembly,
            typeof(Tempest.Harness.WorkspaceShell).Assembly,
        }.Distinct().ToList();

        var found = new List<string>();

        foreach (var assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }

            foreach (var type in types)
            {
                if (Array.IndexOf(FrozenTypeNames, type.Name) >= 0)
                    found.Add($"{assembly.GetName().Name}: {type.FullName}");
            }
        }

        Assert.True(found.Count == 0,
            "Expected no frozen plugin-loading/licensing/REST-API type in any live assembly. Found: "
            + string.Join(", ", found));
    }

    // ==================================================================
    // The live host: does starting TempestOS normally put the frozen
    // REST API's own default port on the wire?
    // ==================================================================

    [Fact]
    public async Task ADefaultlyConfiguredHost_NeverBindsTheFrozenRestApiDefaultPort()
    {
        // Type.EmptyTypes + WithIsolatedPersistenceRoot(): the identical
        // minimal, default-configuration construction TempestHostTests
        // itself uses for "what does this platform do with nothing special
        // configured" - no Runtime:RestApi:Enabled entry anywhere in play,
        // which is the only thing that could ever make a REST listener
        // bind even if RestApiHostedService existed to be discovered.
        var host = new TempestHostBuilder(Type.EmptyTypes).WithIsolatedPersistenceRoot().Build();

        var runTask = host.RunAsync();
        try
        {
            await RunningHostFixture.WaitUntilRunningAsync(host);
            Assert.Equal(HostState.Running, host.State);

            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            var refused = false;
            try
            {
                await client.ConnectAsync(System.Net.IPAddress.Loopback, FrozenRestApiDefaultPort, cts.Token).ConfigureAwait(true);
            }
            catch (SocketException)
            {
                refused = true;
            }
            catch (OperationCanceledException)
            {
                // No response at all within the bound - equally proves
                // nothing is listening; a bound port answers immediately.
                refused = true;
            }

            Assert.True(refused,
                $"A running default-configuration host answered a connection on 127.0.0.1:{FrozenRestApiDefaultPort} " +
                "- the frozen REST API's own documented default port. Nothing in this build should ever bind it.");
        }
        finally
        {
            await host.StopAsync();
            await runTask;
        }
    }
}
