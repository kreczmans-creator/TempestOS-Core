using Tempest.Core.Identity;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves Identity & Permissions is wired into the
// real, unmodified TempestHost exactly as Service Registration Matrix.md
// specifies - ordinary singleton semantics for IPermissionEvaluator, and the
// deliberate dual-AddInstance registration for CurrentPrincipalAccessor
// actually sharing one instance between ICurrentPrincipalAccessor and its
// own concrete type (see CurrentPrincipalAccessor's own remarks for why this
// matters).
//
// `WP 17.2A` (ADR-0146): IRoleProvider/RoleProvider and
// IIdentityService/IdentityService are deleted along with this file's own
// former tests for them - the Host no longer registers either. Identity
// collapses to one session principal, established directly on
// CurrentPrincipalAccessor by the presentation layer (WorkspaceHost) rather
// than resolved through a Host-registered identity service; see
// SessionPrincipalSourceTests for that boundary's own coverage.
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
    // The dual-registration proof: ICurrentPrincipalAccessor and the
    // concrete CurrentPrincipalAccessor type must resolve to the exact
    // same object, or a principal established via the concrete type (as
    // WorkspaceHost's own SessionPrincipalSource boundary does) would be
    // invisible to every ordinary consumer resolving only the interface -
    // the entire reason this design uses two AddInstance calls over the
    // same object rather than two independent Singleton<> registrations.
    // ----------------------------------------------------------------

    [Fact]
    public Task Host_ICurrentPrincipalAccessorAndConcreteType_ResolveToTheSameInstance() =>
        RunAgainstRunningHostAsync(host =>
        {
            var viaInterface = host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
            var viaConcreteType = host.Services!.GetService(typeof(CurrentPrincipalAccessor));

            Assert.Same(viaInterface, viaConcreteType);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_EstablishingCurrentPrincipalDirectlyOnTheConcreteType_IsVisibleThroughTheInterface() =>
        RunAgainstRunningHostAsync(host =>
        {
            var concrete = (CurrentPrincipalAccessor)host.Services!.GetService(typeof(CurrentPrincipalAccessor));
            var accessor = (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));

            var principal = new PlatformPrincipal(new PlatformIdentity("registration-test-user", "registration-test-user"), []);
            concrete.SetCurrent(principal);

            Assert.NotNull(accessor.Current);
            Assert.Equal("registration-test-user", accessor.Current!.Identity.Id);

            return Task.CompletedTask;
        });
}
