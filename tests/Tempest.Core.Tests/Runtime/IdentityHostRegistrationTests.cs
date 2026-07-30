using Tempest.Core.Identity;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves Identity & Permissions is wired into the
// real, unmodified TempestHost exactly as Service Registration Matrix.md
// specifies - every service resolvable, ordinary singleton semantics for
// IRoleProvider/IPermissionEvaluator/IIdentityService, and the deliberate
// dual-AddInstance registration for CurrentPrincipalAccessor actually
// sharing one instance between ICurrentPrincipalAccessor and its own
// concrete type (see CurrentPrincipalAccessor's own remarks for why this
// matters).
[Collection("Console output capture")]
public class IdentityHostRegistrationTests
{
    private static async Task RunAgainstRunningHostAsync(Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            await body(host);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public Task Host_RegistersIRoleProvider_Resolvable() =>
        RunAgainstRunningHostAsync(host =>
        {
            var roleProvider = host.Services!.GetService(typeof(IRoleProvider));

            Assert.IsType<RoleProvider>(roleProvider);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_RegistersIPermissionEvaluator_Resolvable() =>
        RunAgainstRunningHostAsync(host =>
        {
            var evaluator = host.Services!.GetService(typeof(IPermissionEvaluator));

            Assert.IsType<PermissionEvaluator>(evaluator);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_RegistersIIdentityService_Resolvable() =>
        RunAgainstRunningHostAsync(host =>
        {
            var identityService = host.Services!.GetService(typeof(IIdentityService));

            Assert.IsType<IdentityService>(identityService);

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
    // Singleton semantics
    // ----------------------------------------------------------------

    [Fact]
    public Task Host_ResolvingIRoleProviderTwice_ReturnsTheSameInstance() =>
        RunAgainstRunningHostAsync(host =>
        {
            var first = host.Services!.GetService(typeof(IRoleProvider));
            var second = host.Services!.GetService(typeof(IRoleProvider));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_ResolvingIIdentityServiceTwice_ReturnsTheSameInstance() =>
        RunAgainstRunningHostAsync(host =>
        {
            var first = host.Services!.GetService(typeof(IIdentityService));
            var second = host.Services!.GetService(typeof(IIdentityService));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });

    // ----------------------------------------------------------------
    // The dual-registration proof: ICurrentPrincipalAccessor and the
    // concrete CurrentPrincipalAccessor type must resolve to the exact
    // same object, or IdentityService's own writes (via the concrete
    // type) would be invisible to every ordinary consumer (via the
    // interface) - the entire reason this design uses two AddInstance
    // calls over the same object rather than two independent Singleton<>
    // registrations.
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
    public Task Host_EstablishingCurrentPrincipalThroughIdentityService_IsVisibleThroughTheInterface() =>
        RunAgainstRunningHostAsync(host =>
        {
            var identityService = (IIdentityService)host.Services!.GetService(typeof(IIdentityService));
            var accessor = (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));

            identityService.EstablishCurrentPrincipal("registration-test-user");

            Assert.NotNull(accessor.Current);
            Assert.Equal("registration-test-user", accessor.Current!.Identity.Id);

            return Task.CompletedTask;
        });
}
