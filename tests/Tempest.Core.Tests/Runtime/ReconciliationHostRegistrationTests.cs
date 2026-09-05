using Tempest.Core.EngineeringDomain;
using Tempest.Core.Materials;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Runtime;

// Registration validation for the three TD-67/TD-97 reconciliation
// services (`WP 16.4B`). TD-67's own complaint was that no reconcile or
// repair path existed at all; a service that exists in the assembly but
// that nothing can reach would not close it, so these prove each one is
// resolvable from the real, unmodified TempestHost with ordinary
// singleton semantics - the same shape every other HostRegistrationTests
// file in this folder asserts for its own Platform Service.
//
// What is deliberately NOT asserted here: that any sweep runs. None of
// the three is invoked by the startup phase table or by anything else -
// each is explicit DetectAsync/SweepAsync only, because this platform
// does not repair a user's data behind their back. Their behaviour is
// tested directly in the EngineeringDomain/Materials/Requirements test
// folders; this file only proves they are wired.
[Collection("Console output capture")]
public class ReconciliationHostRegistrationTests
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
    public Task Host_RegistersIAttachmentContentReconciliationService_Resolvable() =>
        RunAgainstRunningHostAsync(host =>
        {
            Assert.IsType<AttachmentContentReconciliationService>(
                host.Services!.GetService(typeof(IAttachmentContentReconciliationService)));

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
                         typeof(IAttachmentContentReconciliationService),
                     })
            {
                Assert.Same(host.Services!.GetService(contract), host.Services!.GetService(contract));
            }

            return Task.CompletedTask;
        });
}
