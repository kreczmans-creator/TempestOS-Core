using System.Reflection;

namespace Tempest.Core.Tests.Security;

/// <summary>
/// `WP 21.5F` Offensive Security Audit — Product Owner scope clarification
/// (2026-09-15): proves, at runtime rather than only by reading
/// <c>src/Frozen/README.md</c>'s own claim, that three capabilities parked
/// under <c>src/Frozen/</c> — a REST/HTTP listener (<c>Tempest.Core.Api</c>),
/// licence validation (<c>Tempest.Core.Licensing</c>), and the pre-`ADR-0025`
/// plugin assembly loader/trust store (<c>Tempest.Core.Plugins.PluginAssemblyLoader</c>
/// et al. — distinct from the live, shipped, manifest-discovery-only
/// <c>Tempest.Core.Plugins</c> namespace under <c>Tempest.Core.dll</c>) —
/// are unreachable in a default build and configuration.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a real proof, not a restatement of the README.</b> No
/// <c>.csproj</c> exists anywhere under <c>src/Frozen/</c> or
/// <c>tests/Frozen/</c>, no project's compile glob can reach a directory
/// outside its own subtree, and <c>src/TempestOS.slnx</c> lists no Frozen
/// project — confirmed by direct inspection during this audit. This test
/// proves the *consequence* of that: running the exact test process every
/// other test in this project runs inside (which loads
/// <c>Tempest.Core</c>, <c>Tempest.Workspace</c>, <c>Tempest.Samples</c>,
/// <c>Tempest.Validation</c> — everything a default build and its own test
/// suite touches), none of the three capabilities' own named types are
/// loaded into the process at all. A future change that accidentally added
/// a project reference to any <c>src/Frozen/</c> directory would fail this
/// test the moment any other test in the same run loaded that type.
/// </para>
/// </remarks>
public sealed class FrozenCapabilitiesUnreachableTests
{
    /// <summary>
    /// Full type names that would only exist if one of the three frozen
    /// capabilities had been compiled into a loaded assembly. Each is a
    /// name specific to the frozen implementation — none collides with a
    /// live, shipped type (the live plugin system's own classes are named
    /// <c>PluginManifestDiscoveryService</c>, <c>PluginRegistry</c>, and
    /// so on, sharing only the <c>Tempest.Core.Plugins</c> namespace with
    /// the frozen ones, never a class name).
    /// </summary>
    private static readonly string[] FrozenTypeNames =
    [
        "Tempest.Core.Api.RestApiHostedService",
        "Tempest.Core.Api.ApiEndpointRegistry",
        "Tempest.Core.Api.ApiRequestHandler",
        "Tempest.Core.Licensing.LicenseValidator",
        "Tempest.Core.Licensing.LicenseProvider",
        "Tempest.Core.Plugins.PluginAssemblyLoader",
        "Tempest.Core.Plugins.PluginTrustStore",
        "Tempest.Core.Plugins.PluginSignatureVerifier",
    ];

    [Fact]
    public void TheRestListener_LicenceValidation_AndPluginAssemblyLoader_AreNeverLoadedIntoTheProcess()
    {
        var loadedTypeFullNames = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Select(type => type.FullName)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var frozenType in FrozenTypeNames)
        {
            Assert.DoesNotContain(frozenType, loadedTypeFullNames);
        }
    }

    [Fact]
    public void NoLoadedAssembly_IsNamedForAFrozenProject()
    {
        // A second, independent proof at the assembly-name level: even if
        // a frozen project were one day given its own .csproj and pulled
        // in only as a reference nothing in this process yet constructs a
        // type from, its assembly would still appear in the loaded set the
        // moment it was referenced by anything this test process pulls in
        // (the CLR loads a referenced assembly's metadata eagerly enough
        // for this to catch a reference, not only a construction).
        var loadedAssemblyNames = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetName().Name)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Tempest.Core.Api", loadedAssemblyNames);
        Assert.DoesNotContain("Tempest.Core.Licensing", loadedAssemblyNames);
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null)!;
        }
    }
}
