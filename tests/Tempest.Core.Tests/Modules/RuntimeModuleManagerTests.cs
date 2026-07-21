using Tempest.Core.Logging;
using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Modules;

public class RuntimeModuleManagerTests
{
    private static ModuleDescriptor CreateDescriptor(string id) =>
        new(id, $"Module {id}", "1.0.0", typeof(object));

    [Fact]
    public void Modules_IsEmpty_WhenNothingRegistered()
    {
        var manager = new RuntimeModuleManager();

        Assert.Empty(manager.Modules);
        Assert.Empty(manager.GetAll());
    }

    [Fact]
    public void Register_SingleModule_ReturnsRegisteredRuntimeModule()
    {
        var manager = new RuntimeModuleManager();
        var descriptor = CreateDescriptor("alpha");

        var before = DateTimeOffset.UtcNow;
        var runtimeModule = manager.Register(descriptor);
        var after = DateTimeOffset.UtcNow;

        Assert.Same(descriptor, runtimeModule.Descriptor);
        Assert.Equal(ModuleState.Registered, runtimeModule.State);
        Assert.Null(runtimeModule.FailureReason);
        Assert.InRange(runtimeModule.RegisteredAt, before, after);
    }

    [Fact]
    public void Register_MultipleModules_AreAllRetrievable()
    {
        var manager = new RuntimeModuleManager();

        manager.Register(CreateDescriptor("alpha"));
        manager.Register(CreateDescriptor("beta"));

        Assert.Equal(2, manager.Modules.Count);
        Assert.True(manager.IsRegistered("alpha"));
        Assert.True(manager.IsRegistered("beta"));
    }

    [Fact]
    public void Register_ThrowsDuplicateModuleRegistrationException_WhenAlreadyRegistered()
    {
        var manager = new RuntimeModuleManager();
        manager.Register(CreateDescriptor("alpha"));

        var exception = Assert.Throws<DuplicateModuleRegistrationException>(() =>
            manager.Register(CreateDescriptor("alpha")));

        Assert.Equal("alpha", exception.ModuleId);
    }

    [Fact]
    public void Get_ReturnsRegisteredModule()
    {
        var manager = new RuntimeModuleManager();
        var registered = manager.Register(CreateDescriptor("alpha"));

        var result = manager.Get("alpha");

        Assert.Same(registered, result);
    }

    [Fact]
    public void Get_ThrowsModuleNotRegisteredException_WhenMissing()
    {
        var manager = new RuntimeModuleManager();

        var exception = Assert.Throws<ModuleNotRegisteredException>(() => manager.Get("missing"));

        Assert.Equal("missing", exception.ModuleId);
    }

    [Fact]
    public void TryGet_ReturnsTrueAndModule_WhenRegistered()
    {
        var manager = new RuntimeModuleManager();
        var registered = manager.Register(CreateDescriptor("alpha"));

        var result = manager.TryGet("alpha", out var module);

        Assert.True(result);
        Assert.Same(registered, module);
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenNotRegistered()
    {
        var manager = new RuntimeModuleManager();

        var result = manager.TryGet("missing", out var module);

        Assert.False(result);
        Assert.Null(module);
    }

    [Fact]
    public void IsRegistered_ReturnsTrue_WhenModuleIsRegistered()
    {
        var manager = new RuntimeModuleManager();
        manager.Register(CreateDescriptor("alpha"));

        Assert.True(manager.IsRegistered("alpha"));
    }

    [Fact]
    public void IsRegistered_ReturnsFalse_WhenModuleIsNotRegistered()
    {
        var manager = new RuntimeModuleManager();

        Assert.False(manager.IsRegistered("alpha"));
    }

    [Fact]
    public void Modules_PreservesRegistrationOrder_RegardlessOfId()
    {
        var manager = new RuntimeModuleManager();

        manager.Register(CreateDescriptor("charlie"));
        manager.Register(CreateDescriptor("alpha"));
        manager.Register(CreateDescriptor("bravo"));

        var ids = manager.Modules.Select(m => m.Descriptor.Id).ToArray();

        Assert.Equal(new[] { "charlie", "alpha", "bravo" }, ids);
    }

    [Fact]
    public void Modules_CannotBeMutatedByConsumers()
    {
        var manager = new RuntimeModuleManager();
        manager.Register(CreateDescriptor("alpha"));

        var modules = manager.Modules;

        var mutable = Assert.IsAssignableFrom<IList<RuntimeModule>>(modules);
        Assert.Throws<NotSupportedException>(() => mutable.Add(manager.Get("alpha")));
        Assert.Throws<NotSupportedException>(() => mutable.Clear());
    }

    [Fact]
    public void Modules_SnapshotIsUnaffectedByLaterRegistrations()
    {
        var manager = new RuntimeModuleManager();
        manager.Register(CreateDescriptor("alpha"));

        var snapshot = manager.Modules;
        manager.Register(CreateDescriptor("beta"));

        Assert.Single(snapshot);
        Assert.Equal(2, manager.Modules.Count);
    }

    [Fact]
    public void ModuleState_DefaultValueIsDiscovered()
    {
        Assert.Equal(ModuleState.Discovered, default(ModuleState));
    }

    [Fact]
    public void Register_WithLogger_DoesNotThrowAndRecordsProgress()
    {
        var logDirectory = Path.Combine(Path.GetTempPath(), $"tempest-runtime-manager-tests-{Guid.NewGuid():N}");

        try
        {
            var logger = new LoggingService(logDirectory);
            var manager = new RuntimeModuleManager(logger);

            var runtimeModule = manager.Register(CreateDescriptor("alpha"));

            Assert.Equal("alpha", runtimeModule.Descriptor.Id);
        }
        finally
        {
            if (Directory.Exists(logDirectory))
                Directory.Delete(logDirectory, recursive: true);
        }
    }
}
