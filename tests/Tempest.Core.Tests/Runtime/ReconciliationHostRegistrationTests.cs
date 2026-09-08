using Tempest.Core.Materials;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Runtime;

// Registration validation for the TD-67/TD-97 reconciliation services
// (`WP 16.4B`). TD-67's own complaint was that no reconcile or repair path
// existed at all; a service that exists in the assembly but that nothing
// can reach would not close it, so these prove each one is resolvable from
// the real, unmodified TempestHost with ordinary singleton semantics - the
// same shape every other HostRegistrationTests file in this folder asserts
// for its own Platform Service.
//
// There were three. `WP 17.1B` deleted the attachment one
// (`IAttachmentContentReconciliationService`/
// `AttachmentContentReconciliationService`, and its registration in
// TempestHost) along with the write-intent store it swept against, because
// the failure it compensated for cannot occur any more: attachment content
// is now a BLOB written in the same transaction as the row that names it
// (`ADR-0145`), so there is no window in which bytes exist on disk that no
// committed record references. A sweep for orphaned attachment bytes would
// now be a sweep for a state the write path cannot produce. The two
// surviving services are unchanged and still registered - they reconcile
// data this platform genuinely can leave inconsistent.
//
// What is deliberately NOT asserted here: that any sweep runs. Neither of
// the two is invoked by the startup phase table or by anything else - each
// is explicit DetectAsync/SweepAsync only, because this platform does not
// repair a user's data behind their back. Their behaviour is tested
// directly in the Materials/Requirements test folders; this file only
// proves they are wired.
public class ReconciliationHostRegistrationTests
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
    public Task Host_RegistersIRequirementsReconciliationService_Resolvable() =>
        RunAgainstRunningHostAsync(host =>
        {
            Assert.IsType<RequirementsReconciliationService>(
                host.Services!.GetService(typeof(IRequirementsReconciliationService)));

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_RegistersIMaterialCatalogReconciliationService_Resolvable() =>
        RunAgainstRunningHostAsync(host =>
        {
            Assert.IsType<MaterialCatalogReconciliationService>(
                host.Services!.GetService(typeof(IMaterialCatalogReconciliationService)));

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_ResolvingEachReconciliationServiceTwice_ReturnsTheSameInstance() =>
        RunAgainstRunningHostAsync(host =>
        {
            foreach (var contract in new[]
                     {
                         typeof(IRequirementsReconciliationService),
                         typeof(IMaterialCatalogReconciliationService),
                     })
            {
                Assert.Same(host.Services!.GetService(contract), host.Services!.GetService(contract));
            }

            return Task.CompletedTask;
        });
}
