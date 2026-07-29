using Tempest.Core.Events;
using Tempest.Core.Persistence;
using Tempest.Core.Settings;

namespace Tempest.Core.Tests.Settings;

public class SettingsProviderTests
{
    private static SettingDefinition BuildDefinition(string key = "sample.key", string defaultValue = "default") =>
        new(key, "Sample Setting", defaultValue);

    // ----------------------------------------------------------------
    // RegisterDefinition
    // ----------------------------------------------------------------

    [Fact]
    public void RegisterDefinition_Once_Succeeds()
    {
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());

        var exception = Record.Exception(() => provider.RegisterDefinition(BuildDefinition()));

        Assert.Null(exception);
    }

    [Fact]
    public void RegisterDefinition_DuplicateKey_ThrowsDuplicateSettingDefinitionException()
    {
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        provider.RegisterDefinition(BuildDefinition("dup.key"));

        var exception = Assert.Throws<DuplicateSettingDefinitionException>(
            () => provider.RegisterDefinition(BuildDefinition("dup.key")));

        Assert.Equal("dup.key", exception.Key);
    }

    [Fact]
    public void RegisterDefinition_Null_ThrowsArgumentNullException()
    {
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());

        Assert.Throws<ArgumentNullException>(() => provider.RegisterDefinition(null!));
    }

    // ----------------------------------------------------------------
    // GetValueAsync: default-value and read correctness
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetValueAsync_NothingWrittenYet_ReturnsDefaultValue()
    {
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        provider.RegisterDefinition(BuildDefinition(defaultValue: "the-default"));

        var value = await provider.GetValueAsync("sample.key");

        Assert.Equal("the-default", value);
    }

    [Fact]
    public async Task GetValueAsync_AfterSetValueAsync_ReturnsTheWrittenValue()
    {
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        provider.RegisterDefinition(BuildDefinition());

        await provider.SetValueAsync("sample.key", "written-value");
        var value = await provider.GetValueAsync("sample.key");

        Assert.Equal("written-value", value);
    }

    [Fact]
    public async Task GetValueAsync_UnregisteredKey_ThrowsSettingNotFoundException()
    {
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());

        var exception = await Assert.ThrowsAsync<SettingNotFoundException>(
            () => provider.GetValueAsync("nonexistent"));

        Assert.Equal("nonexistent", exception.Key);
    }

    [Fact]
    public async Task GetValueAsync_ValueAlreadyPersistedBeforeRegistration_ReturnsThePersistedValue()
    {
        var store = new InMemoryPersistenceStore();
        await store.WriteAsync(SettingsProvider.SettingsCollectionName, "sample.key", "persisted-value");
        var provider = new SettingsProvider(store, new EventBus());
        provider.RegisterDefinition(BuildDefinition(defaultValue: "the-default"));

        var value = await provider.GetValueAsync("sample.key");

        Assert.Equal("persisted-value", value);
    }

    // ----------------------------------------------------------------
    // SetValueAsync: not-found and event publication
    // ----------------------------------------------------------------

    [Fact]
    public async Task SetValueAsync_UnregisteredKey_ThrowsSettingNotFoundException()
    {
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());

        await Assert.ThrowsAsync<SettingNotFoundException>(() => provider.SetValueAsync("nonexistent", "value"));
    }

    [Fact]
    public async Task SetValueAsync_PublishesSettingsChangedEvent_WithCorrectOldAndNewValues()
    {
        var eventBus = new EventBus();
        var handler = new RecordingSettingsChangedEventHandler();
        eventBus.Subscribe(handler);
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), eventBus);
        provider.RegisterDefinition(BuildDefinition(defaultValue: "the-default"));

        await provider.SetValueAsync("sample.key", "new-value");

        var published = Assert.Single(handler.Received);
        Assert.Equal("sample.key", published.Key);
        Assert.Equal("the-default", published.OldValue);
        Assert.Equal("new-value", published.NewValue);
    }

    [Fact]
    public async Task SetValueAsync_CalledTwice_SecondEventReportsFirstValueAsOld()
    {
        var eventBus = new EventBus();
        var handler = new RecordingSettingsChangedEventHandler();
        eventBus.Subscribe(handler);
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), eventBus);
        provider.RegisterDefinition(BuildDefinition());

        await provider.SetValueAsync("sample.key", "first");
        await provider.SetValueAsync("sample.key", "second");

        Assert.Equal(2, handler.Received.Count);
        Assert.Equal("first", handler.Received[0].NewValue);
        Assert.Equal("first", handler.Received[1].OldValue);
        Assert.Equal("second", handler.Received[1].NewValue);
    }

    [Fact]
    public async Task SetValueAsync_SameValueAsCurrent_StillPublishesEvent()
    {
        var eventBus = new EventBus();
        var handler = new RecordingSettingsChangedEventHandler();
        eventBus.Subscribe(handler);
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), eventBus);
        provider.RegisterDefinition(BuildDefinition(defaultValue: "same"));

        await provider.SetValueAsync("sample.key", "same");

        var published = Assert.Single(handler.Received);
        Assert.Equal("same", published.OldValue);
        Assert.Equal("same", published.NewValue);
    }

    [Fact]
    public async Task SetValueAsync_NullValue_ThrowsArgumentNullException()
    {
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        provider.RegisterDefinition(BuildDefinition());

        await Assert.ThrowsAsync<ArgumentNullException>(() => provider.SetValueAsync("sample.key", null!));
    }

    // ----------------------------------------------------------------
    // Persistence-failure propagation
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetValueAsync_PersistenceThrows_PropagatesUnchanged()
    {
        var provider = new SettingsProvider(new FailingPersistenceStore(), new EventBus());
        provider.RegisterDefinition(BuildDefinition());

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => provider.GetValueAsync("sample.key"));
    }

    [Fact]
    public async Task SetValueAsync_PersistenceThrows_PropagatesUnchanged_AndDoesNotPublish()
    {
        var eventBus = new EventBus();
        var handler = new RecordingSettingsChangedEventHandler();
        eventBus.Subscribe(handler);
        var provider = new SettingsProvider(new FailingPersistenceStore(), eventBus);
        provider.RegisterDefinition(BuildDefinition());

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(
            () => provider.SetValueAsync("sample.key", "value"));

        Assert.Empty(handler.Received);
    }

    // ----------------------------------------------------------------
    // Concurrent read/write correctness
    // ----------------------------------------------------------------

    [Fact]
    public async Task ConcurrentGetAndSet_NeverThrowsAndFinalReadIsConsistentWithFinalWrite()
    {
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        provider.RegisterDefinition(BuildDefinition());

        var writes = Enumerable.Range(0, 20)
            .Select(i => provider.SetValueAsync("sample.key", $"value-{i}"));
        var reads = Enumerable.Range(0, 20)
            .Select(_ => provider.GetValueAsync("sample.key"));

        await Task.WhenAll(writes.Concat(reads));

        var finalValue = await provider.GetValueAsync("sample.key");
        Assert.StartsWith("value-", finalValue);
    }

    [Fact]
    public async Task ConcurrentSetsToDifferentKeys_DoNotInterfere()
    {
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        var keys = Enumerable.Range(0, 10).Select(i => $"key-{i}").ToList();

        foreach (var key in keys)
            provider.RegisterDefinition(BuildDefinition(key));

        await Task.WhenAll(keys.Select(key => provider.SetValueAsync(key, $"value-for-{key}")));

        foreach (var key in keys)
            Assert.Equal($"value-for-{key}", await provider.GetValueAsync(key));
    }

    // ----------------------------------------------------------------
    // Argument validation
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetValueAsync_NullEmptyOrWhitespaceKey_ThrowsArgumentException(string? key)
    {
        var provider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());

        await Assert.ThrowsAnyAsync<ArgumentException>(() => provider.GetValueAsync(key!));
    }

    [Fact]
    public void Constructor_NullPersistenceStore_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsProvider(null!, new EventBus()));
    }

    [Fact]
    public void Constructor_NullEventBus_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsProvider(new InMemoryPersistenceStore(), null!));
    }
}
