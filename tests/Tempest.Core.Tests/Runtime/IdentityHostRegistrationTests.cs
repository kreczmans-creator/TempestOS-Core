using Tempest.Core.DependencyInjection;
using Tempest.Core.Identity;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves Identity & Permissions is wired into the
// real, unmodified TempestHost exactly as Service Registration Matrix.md
// specifies - ordinary singleton semantics for IPermissionEvaluator, and
// (since `WP 21.6A`, OSA-12/OSA-14) the principal seam's own narrowed
// registration - ICurrentPrincipalAccessor stays broadly resolvable and
// read-only; the concrete CurrentPrincipalAccessor type is no longer
// registered under its own key at all (its own SetCurrent is internal to
// this assembly regardless); PrincipalSession, resolvable under its own
// concrete type, is the one seam that can establish a principal - see
// CurrentPrincipalAccessor's and PrincipalSession's own remarks for the
// finding and the fix.
//
// `WP 17.2A` (ADR-0146): IRoleProvider/RoleProvider and
// IIdentityService/IdentityService are deleted along with this file's own
// former tests for them - the Host no longer registers either. Identity
// collapses to one session principal, established through PrincipalSession
// by the presentation layer (WorkspaceHost) rather than resolved through a
// Host-registered identity service; see SessionPrincipalSourceTests for
// that boundary's own coverage.
public class IdentityHostRegistrationTests
{
    private static async Task RunAgainstRunningHostAsync(Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).WithIsolatedPersistenceRoot().Build();

        var runTask = host.RunAsync();

        await RunningHostFixture.WaitUntilRunningAsync(host);

        await body(host);

        await host.StopAsync();
        await runTask;
    }

    [Fact]
    public Task Host_RegistersIPermissionEvaluator_Resolvable() =>
        RunAgainstRunningHostAsync(host =>
        {
            var evaluator = host.Services!.GetService(typeof(IPermissionEvaluator));

            Assert.IsType<PermissionEvaluator>(evaluator);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_RegistersICurrentPrincipalAccessor_Resolvable() =>
        RunAgainstRunningHostAsync(host =>
        {
            var accessor = host.Services!.GetService(typeof(ICurrentPrincipalAccessor));

            Assert.IsType<CurrentPrincipalAccessor>(accessor);

            return Task.CompletedTask;
        });

    // ----------------------------------------------------------------
    // `WP 21.6A`, OSA-12: the concrete CurrentPrincipalAccessor type is no
    // longer reachable via ordinary DI resolution at all - a component
    // holding only the container (every module's own constructor-injection
    // surface) cannot ask for it by concrete type the way
    // `Tempest.Samples.SamplePrincipalFactory` used to, demonstrated as a
    // real reach before this fix (`WP 21.5F`'s own OSA-12 finding).
    // ----------------------------------------------------------------

    [Fact]
    public Task Host_ConcreteCurrentPrincipalAccessorType_IsNotResolvable() =>
        RunAgainstRunningHostAsync(host =>
        {
            Assert.Throws<ServiceNotRegisteredException>(() => host.Services!.GetService(typeof(CurrentPrincipalAccessor)));

            return Task.CompletedTask;
        });

    // ----------------------------------------------------------------
    // The seam: PrincipalSession is the one resolvable type that can
    // establish a principal, and doing so through it is visible through
    // ICurrentPrincipalAccessor.Current - the same one-instance sharing
    // the old dual-AddInstance registration gave the (now unreachable)
    // concrete type, carried forward onto the narrower seam instead.
    // ----------------------------------------------------------------

    [Fact]
    public Task Host_RegistersPrincipalSession_Resolvable() =>
        RunAgainstRunningHostAsync(host =>
        {
            var session = host.Services!.GetService(typeof(PrincipalSession));

            Assert.IsType<PrincipalSession>(session);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_EstablishingCurrentPrincipalThroughPrincipalSession_IsVisibleThroughTheInterface() =>
        RunAgainstRunningHostAsync(host =>
        {
            var session = (PrincipalSession)host.Services!.GetService(typeof(PrincipalSession));
            var accessor = (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));

            var principal = new PlatformPrincipal(new PlatformIdentity("registration-test-user", "registration-test-user"), []);
            session.Establish(principal);

            Assert.NotNull(accessor.Current);
            Assert.Equal("registration-test-user", accessor.Current!.Identity.Id);

            return Task.CompletedTask;
        });
}
