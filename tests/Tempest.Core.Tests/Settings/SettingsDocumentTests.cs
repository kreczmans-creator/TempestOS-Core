using Tempest.Core.Events;
using Tempest.Core.Settings;
using Tempest.Core.Tests.Logging;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.Settings;

/// <summary>
/// WP-D2 (`TD-112`) — the one settings-document helper nine stores now share.
/// </summary>
/// <remarks>
/// The point of these tests is the distinction the old code conflated:
/// degrading safely and degrading silently are not the same thing. `TD-60`'s
/// recovery contract is asserted unchanged, and the new observability is
/// asserted separately, so a future change cannot trade one for the other
/// without a test failing.
/// </remarks>
public class SettingsDocumentTests
{
    private sealed record Sample(string Name, int Count);

    private static SettingsProvider Provider() => new(new InMemoryPersistenceStore(), new EventBus());

    private static SettingsDocument<Sample> Document(SettingsProvider provider, RecordingLogger? logger = null) =>
        new(provider, "test.document", "Test Document", logger);

    // ==================================================================
    // TD-60's recovery contract — unchanged
    // ==================================================================

    [Fact]
    public async Task NothingStored_LoadsAsNull_SoTheCallerUsesItsOwnDefaults()
    {
        Assert.Null(await Document(Provider()).LoadAsync());
    }

    [Fact]
    public async Task AStoredDocument_RoundTrips()
    {
        var provider = Provider();
        var document = Document(provider);

        await document.SaveAsync(new Sample("bracket", 3));

        Assert.Equal(new Sample("bracket", 3), await document.LoadAsync());
    }

    [Fact]
    public async Task ACorruptStoredValue_LoadsAsNull_AndDoesNotThrow()
    {
        var provider = Provider();
        var document = Document(provider);
        await provider.SetValueAsync("test.document", "{ this is not json");

        // The whole of TD-60: a torn write degrades to the caller's own
        // documented defaults rather than failing the load.
        Assert.Null(await document.LoadAsync());
    }

    [Fact]
    public async Task ACorruptStoredValue_LeavesTheStoreUsable()
    {
        var provider = Provider();
        var document = Document(provider);
        await provider.SetValueAsync("test.document", "]not json[");

        Assert.Null(await document.LoadAsync());

        // Recovery is not one-shot: writing again works, and reads clean.
        await document.SaveAsync(new Sample("recovered", 1));
        Assert.Equal(new Sample("recovered", 1), await document.LoadAsync());
    }

    // ==================================================================
    // The new half — corruption is audible
    // ==================================================================

    [Fact]
    public async Task ACorruptStoredValue_IsLogged_NamingTheKey()
    {
        var logger = new RecordingLogger();
        var provider = Provider();
        var document = Document(provider, logger);
        await provider.SetValueAsync("test.document", "{ truncated");

        Assert.Null(await document.LoadAsync());

        var message = Assert.Single(logger.Messages);
        Assert.Contains("test.document", message, StringComparison.Ordinal);
        Assert.Contains("discarded", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("defaults", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AHealthyLoad_LogsNothing()
    {
        var logger = new RecordingLogger();
        var provider = Provider();
        var document = Document(provider, logger);

        await document.SaveAsync(new Sample("fine", 1));
        Assert.NotNull(await document.LoadAsync());
        Assert.Null(await Document(Provider(), logger).LoadAsync()); // and neither does a first run

        Assert.Empty(logger.Messages);
    }

    [Fact]
    public async Task WithNoLogger_TheBehaviourIsExactlyWhatItWasBefore()
    {
        var provider = Provider();
        var document = Document(provider);
        await provider.SetValueAsync("test.document", "not json at all");

        // The nine stores' previous behaviour, reproduced verbatim for any
        // caller that supplies no logger: silent, safe, never an exception.
        Assert.Null(await document.LoadAsync());
    }

    // ==================================================================
    // The registration half of the duplication
    // ==================================================================

    [Fact]
    public void ASecondDocumentOnTheSameKey_IsIdempotent_NotAnError()
    {
        var provider = Provider();

        _ = Document(provider);

        // A restart constructs a second instance against the same provider.
        // Nine stores each wrote this try/catch out by hand.
        Assert.Null(Record.Exception(() => Document(provider)));
    }

    [Fact]
    public void TheDocumentExposesItsOwnKey()
    {
        Assert.Equal("test.document", Document(Provider()).Key);
    }
}
