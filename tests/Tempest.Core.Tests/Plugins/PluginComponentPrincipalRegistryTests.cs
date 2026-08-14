using Tempest.Core.Identity;
using Tempest.Core.Plugins;

namespace Tempest.Core.Tests.Plugins;

// ADR-0111: the small, Host-owned registry mapping a discovered IModule Type
// back to the plugin's own component principal that owns it - its read
// (IPluginComponentPrincipalRegistry) and write (IPluginComponentPrincipalRecorder)
// sides, both implemented by the one PluginComponentPrincipalRegistry type.
public class PluginComponentPrincipalRegistryTests
{
    private static readonly Type SampleModuleTypeA = typeof(SampleModuleA);
    private static readonly Type SampleModuleTypeB = typeof(SampleModuleB);

    [Fact]
    public void GetPrincipalFor_NothingRecorded_ReturnsNull()
    {
        var registry = new PluginComponentPrincipalRegistry();

        Assert.Null(registry.GetPrincipalFor(SampleModuleTypeA));
    }

    [Fact]
    public void Record_ThenGetPrincipalFor_ReturnsTheRecordedPrincipal()
    {
        var registry = new PluginComponentPrincipalRegistry();
        var principal = CreatePrincipal("plugin.a");

        registry.Record(SampleModuleTypeA, principal);

        Assert.Same(principal, registry.GetPrincipalFor(SampleModuleTypeA));
    }

    [Fact]
    public void Record_DistinctModuleTypes_EachResolvesItsOwnPrincipal()
    {
        var registry = new PluginComponentPrincipalRegistry();
        var principalA = CreatePrincipal("plugin.a");
        var principalB = CreatePrincipal("plugin.b");

        registry.Record(SampleModuleTypeA, principalA);
        registry.Record(SampleModuleTypeB, principalB);

        Assert.Same(principalA, registry.GetPrincipalFor(SampleModuleTypeA));
        Assert.Same(principalB, registry.GetPrincipalFor(SampleModuleTypeB));
    }

    [Fact]
    public void Record_SameModuleTypeTwice_LatestRecordingWins()
    {
        var registry = new PluginComponentPrincipalRegistry();
        var first = CreatePrincipal("plugin.first");
        var second = CreatePrincipal("plugin.second");

        registry.Record(SampleModuleTypeA, first);
        registry.Record(SampleModuleTypeA, second);

        Assert.Same(second, registry.GetPrincipalFor(SampleModuleTypeA));
    }

    [Fact]
    public void GetPrincipalFor_UnrecordedType_AmongOthersRecorded_ReturnsNull()
    {
        var registry = new PluginComponentPrincipalRegistry();
        registry.Record(SampleModuleTypeA, CreatePrincipal("plugin.a"));

        Assert.Null(registry.GetPrincipalFor(SampleModuleTypeB));
    }

    [Fact]
    public void Record_NullModuleType_ThrowsArgumentNullException()
    {
        var registry = new PluginComponentPrincipalRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Record(null!, CreatePrincipal("plugin.a")));
    }

    [Fact]
    public void Record_NullPrincipal_ThrowsArgumentNullException()
    {
        var registry = new PluginComponentPrincipalRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Record(SampleModuleTypeA, null!));
    }

    [Fact]
    public void GetPrincipalFor_NullModuleType_ThrowsArgumentNullException()
    {
        var registry = new PluginComponentPrincipalRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.GetPrincipalFor(null!));
    }

    // ------------------------------------------------------------------
    // Interface segregation: read/write sides are genuinely distinct
    // capabilities on the one concrete type (ADR-0111).
    // ------------------------------------------------------------------

    [Fact]
    public void ConcreteType_ImplementsBothReadAndWriteInterfaces()
    {
        var registry = new PluginComponentPrincipalRegistry();

        Assert.IsAssignableFrom<IPluginComponentPrincipalRegistry>(registry);
        Assert.IsAssignableFrom<IPluginComponentPrincipalRecorder>(registry);
    }

    [Fact]
    public void WriteThroughRecorderInterface_IsVisibleThroughReadInterface()
    {
        var registry = new PluginComponentPrincipalRegistry();
        IPluginComponentPrincipalRecorder recorder = registry;
        IPluginComponentPrincipalRegistry reader = registry;
        var principal = CreatePrincipal("plugin.a");

        recorder.Record(SampleModuleTypeA, principal);

        Assert.Same(principal, reader.GetPrincipalFor(SampleModuleTypeA));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static PlatformPrincipal CreatePrincipal(string id) =>
        new(new PlatformIdentity(id, id), []);

    private sealed class SampleModuleA : Tempest.Core.Modules.IModule
    {
        public string Id => "sample.a";
        public string Name => "Sample A";
        public string Version => "1.0.0";
    }

    private sealed class SampleModuleB : Tempest.Core.Modules.IModule
    {
        public string Id => "sample.b";
        public string Name => "Sample B";
        public string Version => "1.0.0";
    }
}
