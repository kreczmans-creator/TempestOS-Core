using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Identity;

// ADR-0111: CurrentComponentAccessor's own AsyncLocal<ImmutableStack<IPrincipal>>-
// backed scope stack - nested BeginScope push/pop restores the correct prior
// value, and concurrent/unrelated async call chains never observe each
// other's pushed principal (real AsyncLocal isolation, proven via Task.Run
// with a synchronisation barrier, not merely asserted by reading the docs).
public class CurrentComponentAccessorTests
{
    // ------------------------------------------------------------------
    // Baseline: no scope pushed
    // ------------------------------------------------------------------

    [Fact]
    public void Current_NoScopePushed_IsNull()
    {
        var accessor = new CurrentComponentAccessor();

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void BeginScope_NullPrincipal_ThrowsArgumentNullException()
    {
        var accessor = new CurrentComponentAccessor();

        Assert.Throws<ArgumentNullException>(() => accessor.BeginScope(null!));
    }

    // ------------------------------------------------------------------
    // Single push/pop
    // ------------------------------------------------------------------

    [Fact]
    public void BeginScope_PushesPrincipal_CurrentReturnsIt()
    {
        var accessor = new CurrentComponentAccessor();
        var principal = CreatePrincipal("component.a");

        using (accessor.BeginScope(principal))
        {
            Assert.Same(principal, accessor.Current);
        }
    }

    [Fact]
    public void Dispose_RestoresNullWhenNoPriorScope()
    {
        var accessor = new CurrentComponentAccessor();
        var principal = CreatePrincipal("component.a");

        var scope = accessor.BeginScope(principal);
        scope.Dispose();

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Dispose_CalledTwice_IsIdempotent_DoesNotDoubleRestore()
    {
        var accessor = new CurrentComponentAccessor();
        var outer = CreatePrincipal("component.outer");
        var inner = CreatePrincipal("component.inner");

        using (accessor.BeginScope(outer))
        {
            var innerScope = accessor.BeginScope(inner);
            innerScope.Dispose();
            innerScope.Dispose(); // second dispose must not pop past "outer"

            Assert.Same(outer, accessor.Current);
        }
    }

    // ------------------------------------------------------------------
    // Nested scopes: correct restore ordering
    // ------------------------------------------------------------------

    [Fact]
    public void NestedBeginScope_InnerDisposed_RestoresOuter()
    {
        var accessor = new CurrentComponentAccessor();
        var outer = CreatePrincipal("component.outer");
        var inner = CreatePrincipal("component.inner");

        using (accessor.BeginScope(outer))
        {
            using (accessor.BeginScope(inner))
            {
                Assert.Same(inner, accessor.Current);
            }

            Assert.Same(outer, accessor.Current);
        }

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void TriplyNestedBeginScope_EachDisposeRestoresExactlyOneLevel()
    {
        var accessor = new CurrentComponentAccessor();
        var a = CreatePrincipal("component.a");
        var b = CreatePrincipal("component.b");
        var c = CreatePrincipal("component.c");

        var scopeA = accessor.BeginScope(a);
        Assert.Same(a, accessor.Current);

        var scopeB = accessor.BeginScope(b);
        Assert.Same(b, accessor.Current);

        var scopeC = accessor.BeginScope(c);
        Assert.Same(c, accessor.Current);

        scopeC.Dispose();
        Assert.Same(b, accessor.Current);

        scopeB.Dispose();
        Assert.Same(a, accessor.Current);

        scopeA.Dispose();
        Assert.Null(accessor.Current);
    }

    [Fact]
    public void ReenteringSameComponent_NestedScope_RestoresCorrectlyOnExit()
    {
        var accessor = new CurrentComponentAccessor();
        var principal = CreatePrincipal("component.a");

        using (accessor.BeginScope(principal))
        {
            using (accessor.BeginScope(principal))
            {
                Assert.Same(principal, accessor.Current);
            }

            Assert.Same(principal, accessor.Current);
        }

        Assert.Null(accessor.Current);
    }

    // ------------------------------------------------------------------
    // Cross-component nesting: control returning to first-party code is
    // never mistakenly attributed to a plugin.
    // ------------------------------------------------------------------

    [Fact]
    public void CrossComponentCall_ReturningToFirstParty_CurrentRevertsToNull()
    {
        var accessor = new CurrentComponentAccessor();
        var plugin = CreatePrincipal("plugin.a");

        Assert.Null(accessor.Current); // first-party, no scope

        using (accessor.BeginScope(plugin))
        {
            Assert.Same(plugin, accessor.Current);
        }

        // Control has returned to first-party code - must revert, not leak.
        Assert.Null(accessor.Current);
    }

    // ------------------------------------------------------------------
    // Exception safety: Dispose() restores the prior value via `using`'s
    // own implicit finally, even when the scoped code itself throws - a
    // scope must never be left "stuck" pushed after an exception unwinds
    // through it.
    // ------------------------------------------------------------------

    [Fact]
    public void BeginScope_ExceptionThrownInsideUsing_StillRestoresPriorScope()
    {
        var accessor = new CurrentComponentAccessor();
        var principal = CreatePrincipal("component.a");

        Action act = () =>
        {
            using (accessor.BeginScope(principal))
            {
                Assert.Same(principal, accessor.Current);
                throw new InvalidOperationException("boom");
            }
        };

        Assert.Throws<InvalidOperationException>(act);
        Assert.Null(accessor.Current);
    }

    [Fact]
    public void NestedBeginScope_InnerThrows_RestoresOuter_NotNull()
    {
        var accessor = new CurrentComponentAccessor();
        var outer = CreatePrincipal("component.outer");
        var inner = CreatePrincipal("component.inner");

        using (accessor.BeginScope(outer))
        {
            Action act = () =>
            {
                using (accessor.BeginScope(inner))
                {
                    Assert.Same(inner, accessor.Current);
                    throw new InvalidOperationException("boom");
                }
            };

            Assert.Throws<InvalidOperationException>(act);

            // The inner scope's own Dispose still ran (via `using`'s
            // implicit finally) despite the exception - the outer scope,
            // not null, must be current here.
            Assert.Same(outer, accessor.Current);
        }

        Assert.Null(accessor.Current);
    }

    // ------------------------------------------------------------------
    // Async continuation: the scope flows correctly across an await.
    // ------------------------------------------------------------------

    [Fact]
    public async Task BeginScope_SurvivesAcrossAwait_WithinTheSameLogicalCallChain()
    {
        var accessor = new CurrentComponentAccessor();
        var principal = CreatePrincipal("component.async");

        using (accessor.BeginScope(principal))
        {
            await Task.Delay(1);
            Assert.Same(principal, accessor.Current);

            await Task.Yield();
            Assert.Same(principal, accessor.Current);
        }

        Assert.Null(accessor.Current);
    }

    // ------------------------------------------------------------------
    // AsyncLocal isolation: concurrent, unrelated async call chains do not
    // observe each other's pushed principal - the load-bearing guarantee
    // this type exists to provide (ADR-0111).
    // ------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentUnrelatedCallChains_EachObservesOnlyItsOwnPushedPrincipal()
    {
        var accessor = new CurrentComponentAccessor();
        var principalA = CreatePrincipal("component.a");
        var principalB = CreatePrincipal("component.b");

        // A two-party barrier: both tasks push their own principal, then
        // wait for the other to reach the same point before observing
        // Current - if AsyncLocal isolation were broken (e.g. a shared
        // mutable Stack<T> instead of a copy-on-write ImmutableStack<T>),
        // one task's push could clobber or be visible to the other here.
        using var barrierA = new SemaphoreSlim(0, 1);
        using var barrierB = new SemaphoreSlim(0, 1);

        Tempest.Core.Identity.IPrincipal? observedByA = null;
        Tempest.Core.Identity.IPrincipal? observedByB = null;

        var taskA = Task.Run(async () =>
        {
            using (accessor.BeginScope(principalA))
            {
                barrierA.Release();
                await barrierB.WaitAsync();

                observedByA = accessor.Current;
            }
        });

        var taskB = Task.Run(async () =>
        {
            using (accessor.BeginScope(principalB))
            {
                barrierB.Release();
                await barrierA.WaitAsync();

                observedByB = accessor.Current;
            }
        });

        await Task.WhenAll(taskA, taskB);

        Assert.Same(principalA, observedByA);
        Assert.Same(principalB, observedByB);
    }

    [Fact]
    public async Task ConcurrentUnrelatedCallChains_MainThreadNeverObservesEitherChildsPushedPrincipal()
    {
        var accessor = new CurrentComponentAccessor();
        var principal = CreatePrincipal("component.child");

        using var childPushed = new SemaphoreSlim(0, 1);
        using var mainChecked = new SemaphoreSlim(0, 1);

        var child = Task.Run(async () =>
        {
            using (accessor.BeginScope(principal))
            {
                childPushed.Release();
                await mainChecked.WaitAsync();
            }
        });

        await childPushed.WaitAsync();

        // The main (test) async context never had BeginScope called on it -
        // AsyncLocal<T>'s copy-on-write semantics must keep this context's
        // own value at null regardless of what the child Task.Run pushed.
        Assert.Null(accessor.Current);

        mainChecked.Release();
        await child;

        Assert.Null(accessor.Current);
    }

    [Fact]
    public async Task ManyConcurrentCallChains_EachObservesOnlyItsOwnPrincipal()
    {
        var accessor = new CurrentComponentAccessor();
        const int chainCount = 20;

        var tasks = Enumerable.Range(0, chainCount).Select(i => Task.Run(async () =>
        {
            var principal = CreatePrincipal($"component.{i}");

            using (accessor.BeginScope(principal))
            {
                // Yield repeatedly to encourage interleaving with the other
                // concurrently-running chains.
                for (var iteration = 0; iteration < 10; iteration++)
                {
                    await Task.Yield();
                    Assert.Same(principal, accessor.Current);
                }

                return accessor.Current;
            }
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        for (var i = 0; i < chainCount; i++)
            Assert.Equal($"component.{i}", results[i]!.Identity.Id);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static PlatformPrincipal CreatePrincipal(string id) =>
        new(new PlatformIdentity(id, id), []);
}
