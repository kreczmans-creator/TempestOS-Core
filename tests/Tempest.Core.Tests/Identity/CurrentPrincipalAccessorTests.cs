using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Identity;

public class CurrentPrincipalAccessorTests
{
    private static IPrincipal BuildPrincipal(string id) =>
        new PlatformPrincipal(new PlatformIdentity(id, id), []);

    // ----------------------------------------------------------------
    // Default / basic set-and-read
    // ----------------------------------------------------------------

    [Fact]
    public void Current_BeforeAnySetCurrentCall_IsNull()
    {
        var accessor = new CurrentPrincipalAccessor();

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void SetCurrent_ThenCurrent_ReturnsTheSamePrincipal()
    {
        var accessor = new CurrentPrincipalAccessor();
        var principal = BuildPrincipal("local.user");

        accessor.SetCurrent(principal);

        Assert.Same(principal, accessor.Current);
    }

    [Fact]
    public void SetCurrent_Null_ClearsAnAlreadyEstablishedPrincipal()
    {
        var accessor = new CurrentPrincipalAccessor();
        accessor.SetCurrent(BuildPrincipal("local.user"));

        accessor.SetCurrent(null);

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void SetCurrent_CalledTwice_LatestValueWins()
    {
        var accessor = new CurrentPrincipalAccessor();
        var first = BuildPrincipal("first");
        var second = BuildPrincipal("second");

        accessor.SetCurrent(first);
        accessor.SetCurrent(second);

        Assert.Same(second, accessor.Current);
    }

    // ----------------------------------------------------------------
    // Ambient, platform-wide visibility: a value established from one
    // call site is visible to a wholly independent, later caller - unlike
    // an AsyncLocal<T>-backed accessor would be. See this type's own
    // remarks for why this shape was chosen over AsyncLocal<T>.
    // ----------------------------------------------------------------

    [Fact]
    public async Task SetCurrent_FromOneAsyncCallChain_IsVisibleToAnUnrelatedLaterCallChain()
    {
        var accessor = new CurrentPrincipalAccessor();
        var principal = BuildPrincipal("local.user");

        async Task EstablishAsync()
        {
            await Task.Yield();
            accessor.SetCurrent(principal);
        }

        await EstablishAsync();

        // A wholly separate, later async call chain - not nested inside
        // EstablishAsync's own - still observes the established principal.
        await Task.Run(() => Assert.Same(principal, accessor.Current));
    }

    // ----------------------------------------------------------------
    // Thread safety: concurrent reads/writes must never throw or corrupt
    // state (Platform Service Contracts.md's own Thread Safety
    // Expectations).
    // ----------------------------------------------------------------

    [Fact]
    public async Task ConcurrentSetAndReadCurrent_DoesNotThrowOrCorruptState()
    {
        var accessor = new CurrentPrincipalAccessor();
        var principals = Enumerable.Range(0, 20).Select(i => BuildPrincipal($"user-{i}")).ToList();

        var writers = principals.Select(p => Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
                accessor.SetCurrent(p);
        }));

        var readers = Enumerable.Range(0, 20).Select(readerIndex => Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
                _ = accessor.Current;
        }));

        await Task.WhenAll(writers.Concat(readers));

        // No assertion beyond "did not throw" - the point of this test is
        // that concurrent access is safe, not which writer's value wins.
        var finalValue = accessor.Current;
        Assert.True(finalValue is null || principals.Contains(finalValue));
    }
}
