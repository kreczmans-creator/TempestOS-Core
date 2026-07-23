using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Tempest.Core.Versioning;

namespace Tempest.Core.Tests.Versioning;

public class PlatformVersionProviderTests
{
    // ----------------------------------------------------------------
    // Successful version retrieval
    // ----------------------------------------------------------------

    [Fact]
    public void Version_ResolvedFromTheRealPlatformAssembly_HasASemanticVersionShapedString()
    {
        var provider = new PlatformVersionProvider();

        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+"), provider.Version.SemanticVersion);
    }

    [Fact]
    public void Version_ResolvedFromTheRealPlatformAssembly_HasANonNegativeAssemblyVersion()
    {
        var provider = new PlatformVersionProvider();

        Assert.True(provider.Version.AssemblyVersion.Major >= 0);
        Assert.True(provider.Version.AssemblyVersion.Minor >= 0);
    }

    [Fact]
    public void Version_ResolvedFromTheRealPlatformAssembly_HasAnInformationalVersion()
    {
        // Tempest.Core is built with <Version> set (Directory.Build.props,
        // sourced from the repository's own VERSION file), so the .NET SDK
        // generates an AssemblyInformationalVersionAttribute automatically.
        var provider = new PlatformVersionProvider();

        Assert.False(string.IsNullOrWhiteSpace(provider.Version.InformationalVersion));
    }

    [Fact]
    public void Version_PreferInformationalVersion_WhenPresent_ForSemanticVersion()
    {
        var assembly = BuildDynamicAssembly(version: new Version(2, 5, 0, 0), informationalVersion: "2.5.0-preview");

        var provider = new PlatformVersionProvider(assembly);

        Assert.Equal("2.5.0-preview", provider.Version.SemanticVersion);
        Assert.Equal("2.5.0-preview", provider.Version.InformationalVersion);
        Assert.Equal(new Version(2, 5, 0, 0), provider.Version.AssemblyVersion);
    }

    // ----------------------------------------------------------------
    // Missing metadata behaviour
    // ----------------------------------------------------------------

    [Fact]
    public void Version_AssemblyWithNoInformationalVersionAttribute_FallsBackToAssemblyVersion()
    {
        var assembly = BuildDynamicAssembly(version: new Version(1, 4, 2, 0), informationalVersion: null);

        var provider = new PlatformVersionProvider(assembly);

        Assert.Null(provider.Version.InformationalVersion);
        Assert.Equal("1.4.2", provider.Version.SemanticVersion);
        Assert.Equal(new Version(1, 4, 2, 0), provider.Version.AssemblyVersion);
    }

    [Fact]
    public void Version_AssemblyWithNoVersionMetadataAtAll_FallsBackToZeroVersion()
    {
        var assembly = BuildDynamicAssembly(version: null, informationalVersion: null);

        var provider = new PlatformVersionProvider(assembly);

        Assert.Equal(new Version(0, 0, 0, 0), provider.Version.AssemblyVersion);
        Assert.Equal("0.0.0", provider.Version.SemanticVersion);
        Assert.Null(provider.Version.InformationalVersion);
    }

    [Fact]
    public void Version_AssemblyWithBlankInformationalVersionAttribute_TreatsItAsAbsent()
    {
        var assembly = BuildDynamicAssembly(version: new Version(1, 0, 0, 0), informationalVersion: "   ");

        var provider = new PlatformVersionProvider(assembly);

        Assert.Null(provider.Version.InformationalVersion);
        Assert.Equal("1.0.0", provider.Version.SemanticVersion);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenAssemblyIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new PlatformVersionProvider((Assembly)null!));
    }

    // ----------------------------------------------------------------
    // Caching behaviour
    // ----------------------------------------------------------------

    [Fact]
    public void Version_IsResolvedOnce_AndTheSameInstanceIsReturnedOnEveryAccess()
    {
        var provider = new PlatformVersionProvider();

        var first = provider.Version;
        var second = provider.Version;

        Assert.Same(first, second);
    }

    [Fact]
    public void Version_TwoProvidersOverTheSameAssembly_ResolveEqualButIndependentValues()
    {
        var assembly = BuildDynamicAssembly(version: new Version(3, 1, 4, 0), informationalVersion: "3.1.4");

        var first = new PlatformVersionProvider(assembly);
        var second = new PlatformVersionProvider(assembly);

        Assert.NotSame(first.Version, second.Version);
        Assert.Equal(first.Version.SemanticVersion, second.Version.SemanticVersion);
        Assert.Equal(first.Version.AssemblyVersion, second.Version.AssemblyVersion);
    }

    // ----------------------------------------------------------------
    // Immutability
    // ----------------------------------------------------------------

    [Fact]
    public void PlatformVersion_Constructor_ThrowsArgumentException_WhenSemanticVersionIsNullEmptyOrWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new PlatformVersion("", new Version(1, 0, 0, 0), null));
    }

    [Fact]
    public void PlatformVersion_Constructor_ThrowsArgumentNullException_WhenAssemblyVersionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new PlatformVersion("1.0.0", null!, null));
    }

    [Fact]
    public void PlatformVersion_PropertiesReflectExactlyWhatWasConstructedWith()
    {
        var version = new PlatformVersion("9.9.9", new Version(9, 9, 9, 9), "9.9.9+build");

        Assert.Equal("9.9.9", version.SemanticVersion);
        Assert.Equal(new Version(9, 9, 9, 9), version.AssemblyVersion);
        Assert.Equal("9.9.9+build", version.InformationalVersion);
    }

    // ----------------------------------------------------------------
    // Thread safety
    // ----------------------------------------------------------------

    [Fact]
    public void Version_ReadFromMultipleThreadsConcurrently_AlwaysObservesTheSameValue()
    {
        var provider = new PlatformVersionProvider();
        var results = new PlatformVersion[64];

        Parallel.For(0, results.Length, i => results[i] = provider.Version);

        Assert.All(results, result => Assert.Same(provider.Version, result));
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static Assembly BuildDynamicAssembly(Version? version, string? informationalVersion)
    {
        var assemblyName = new AssemblyName("Tempest.Core.Tests.DynamicVersionFixture")
        {
            Version = version,
        };

        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

        if (informationalVersion is not null)
        {
            var constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            var attributeBuilder = new CustomAttributeBuilder(constructor, [informationalVersion]);
            assemblyBuilder.SetCustomAttribute(attributeBuilder);
        }

        return assemblyBuilder;
    }
}
