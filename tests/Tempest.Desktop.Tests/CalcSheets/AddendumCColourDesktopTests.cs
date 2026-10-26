using System.Reflection;
using Avalonia.Headless.XUnit;
using Tempest.Calc.Library;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>The colour-team findings on Addendum C that live in the Desktop: the records screen's open path (Black B-C-05).</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class AddendumCColourDesktopTests
{
    /// <summary>Passes every call through, except a read of the whole reference-record collection, which fails as a broken store would.</summary>
    public class RecordsReadFails : DispatchProxy
    {
        public IQueryablePersistenceStore Target { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            if (targetMethod.Name == nameof(IQueryablePersistenceStore.ReadAllAsync) && args is [string collection, ..] && collection == CalcInstanceStore.RecordCollection)
                throw new IOException("The database file is locked by another program.");

            try
            {
                return targetMethod.Invoke(Target, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }
    }

    [AvaloniaFact]
    public async Task Black_B_C_05_A_Store_Failure_While_The_Records_Screen_Opens_Is_A_Sentence_On_The_Screen_Not_A_Lost_Exception()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();

        try
        {
            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var proxy = DispatchProxy.Create<IQueryablePersistenceStore, RecordsReadFails>();
            ((RecordsReadFails)(object)proxy).Target = domain.PersistenceStore;
            var service = new CalcInstanceService(host.CalcInstances!.Catalogue, new CalcInstanceStore(proxy), host.EvidenceService!, domain, renderer: null, applicationVersion: "TempestOS test");
            var model = new CalcRecordsViewModel(service, () => "test.engineer");
            var view = new CalcRecordsView(model);
            var changed = 0;
            model.Changed += () => changed++;

            // The Engineering tree opens the screen from a handler nobody awaits: before the fix this threw and the failure was lost.
            await view.RefreshAsync("cable.ampacity");

            Assert.Equal("Could not open the reference records: The database file is locked by another program.", model.Status);
            Assert.True(changed > 0);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
