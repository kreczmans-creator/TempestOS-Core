using Tempest.App.Composition;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.EngineeringDomain.SchemaVersioning;

/// <summary>
/// `TD-87`/`ADR-0120` — the architecture document's own acceptance
/// criterion, driven end to end through a real host rather than only
/// through <see cref="EngineeringObjectStateStore"/> directly (which
/// <see cref="GoldenCorpusTests"/> already proves as a standing,
/// per-record regression check): <b>a record written in the exact
/// pre-`ADR-0120` byte shape — a numeric enum, no <c>SchemaVersion</c>
/// property at all — loads identically after a real relaunch.</b>
/// </summary>
/// <remarks>
/// Uses the same real-host convention
/// <c>Workspace.CommandDescriptorBindingTests</c> already establishes for
/// this assembly (an explicit module list via <see cref="TempestHostBuilder"/>,
/// never <see cref="EngineeringWorkspaceComposer.Build"/>'s own reflective
/// discovery, plus an isolated <see cref="TempDirectory"/> persistence
/// root) — <see cref="WorkspaceManager"/> is `Tempest.App`-layer, already
/// referenced and already used this way throughout
/// `Tempest.Core.Tests/Workspace/`, so this stays in `Tempest.Core.Tests`
/// rather than moving to `Tempest.Desktop.Tests`.
/// </remarks>
// `TD-34` (`WP 16.4A`): this class redirects the process-wide
// `Console.Out` in `StartHostAsync`, so it must run inside the
// collection that serialises every class doing so — otherwise xUnit
// runs it in parallel with the other 51 and the redirection races,
// which is the exact defect `TD-34` closed. Added at the `v0.16.0`
// review board, which found this the only redirecting class outside
// the collection: the file landed on a parallel branch hours after
// `WP 16.4A` joined the last stragglers to it, so its author never
// saw the freshly-reinforced convention.
[Collection("Console output capture")]
public sealed class RestartProofTests
{
    [Fact]
    public async Task ARecordWrittenInTheExactPreAdr0120ByteShape_RehydratesThroughARealHost_WithIdenticalStatus()
    {
        using var temp = new TempDirectory();
        Guid partId;
        IPersistenceStore firstPersistence;

        // ============================================================
        // FIRST HOST — create a real Part and move its Status, through
        // the ordinary production path (factory + lifecycle transitions),
        // exactly as `EngineeringObjectBase.CaptureState()` and
        // `EngineeringObjectStateStore.SaveAsync` write it today.
        // ============================================================
        {
            var (host, manager) = await StartHostAsync(temp.Path);

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            firstPersistence = (IPersistenceStore)host.Services!.GetService(typeof(IPersistenceStore));

            var factory = new EngineeringObjectFactory<Part>(
                MechanicalObjectFactoryRegistry.Part, domain,
                (doc, rev) => new Part(doc, rev, domain, "PN-RESTART", "Restart Proof Part", EngineeringObjectMetadata.Empty));
            var part = (Part)await factory.CreateAsync("Restart proof part.");
            partId = part.Id;

            await ((IHasLifecycle)part).TransitionAsync(LifecycleState.InReview);
            await ((IHasLifecycle)part).TransitionAsync(LifecycleState.Approved);
            await ((IHasLifecycle)part).TransitionAsync(LifecycleState.Released);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }

        // ============================================================
        // Overwrite the just-written state record with the exact
        // pre-`ADR-0120` byte shape: numeric `Status`, no `SchemaVersion`
        // property at all — the identical shape
        // `GoldenCorpus/v1/part-minimal.json` was captured in, with this
        // real object's own Id/Identifier/Status substituted in. This is
        // not a hand-invented shape: it is what `v0.15.0`'s own
        // unmodified `JsonSerializer.Serialize(state)` (no options)
        // actually produced.
        // ============================================================
        var oldFormatJson =
            $$"""
            {"Id":"{{partId}}","Kind":"Part","Identifier":"PN-RESTART","DisplayName":"Restart Proof Part",
            "Metadata":{"Category":null,"Discipline":null,"Owner":null,"Tags":null,"Classification":null,"Notes":null},
            "Status":3,"ParentId":null,"IsDeleted":false,
            "BomLine":{"Quantity":1,"UnitOfMeasure":null,"FindNumber":null,"ItemNumber":null,"ReferenceDesignator":null},
            "History":[],"Attachments":[],"TypeState":{} }
            """;
        await firstPersistence.WriteAsync(EngineeringObjectStateStore.StateCollectionName, partId.ToString("N"), oldFormatJson);

        // ============================================================
        // SECOND HOST — a genuinely new process shape over the same disk,
        // rehydrating through the real production path.
        // ============================================================
        {
            var (host, manager) = await StartHostAsync(temp.Path);

            var result = await EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);
            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var rehydrated = await domain.Repository.FindAsync(partId);

            Assert.True(result.IsComplete, "Expected a clean rehydration of the v0 record.");
            Assert.NotNull(rehydrated);
            Assert.Equal(LifecycleState.Released, ((IHasLifecycle)rehydrated!).Status);
            Assert.Equal("PN-RESTART", ((IHasBusinessIdentifier)rehydrated).Identifier);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// Builds and starts a real <see cref="ITempestHost"/>/<see cref="WorkspaceManager"/>
    /// pair over an isolated persistence root, with Mechanical Engineering
    /// Disciplines registered — the minimum a Part needs — exactly the
    /// pattern <c>Workspace.CommandDescriptorBindingTests</c> already
    /// establishes for this assembly.
    /// </summary>
    private static async Task<(ITempestHost Host, WorkspaceManager Manager)> StartHostAsync(string persistenceRoot)
    {
        var host = new TempestHostBuilder([typeof(MechanicalWorkspaceExplorerModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, persistenceRoot),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        var originalOut = Console.Out;
        try
        {
            Console.SetOut(new StringWriter());
            await manager.StartAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(manager, host);

        return (host, manager);
    }
}
