using System.Text.Json;
using CsCheck;
using Tempest.Core.Bearings;
using Tempest.Core.Constants;
using Tempest.Core.EngineeringData;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Standards;

namespace Tempest.Core.Tests.ReferenceData;

/// <summary>
/// Property-based tests (CsCheck) of the supersession invariant every
/// Group A library shares (`ADR-0149`, `WP 18.0B` acceptance test 2):
/// after any number of <see cref="IReferenceDataCatalog{TDefinition}.ReviseAsync(string,TDefinition,ReferenceProvenance,string?,CancellationToken)"/>
/// calls and a <see cref="IReferenceDataCatalog{TDefinition}.SupersedeAsync"/>,
/// every pin ever minted still resolves to the exact content it pinned via
/// <see cref="IReferenceDataCatalog{TDefinition}.GetRevisionAsync"/>, and a
/// superseded record still reports itself — as Superseded — through
/// <see cref="IReferenceDataCatalog{TDefinition}.FindAsync"/> rather than
/// vanishing.
/// </summary>
/// <remarks>
/// <para>
/// <b>One property, run against all five real libraries</b> — Materials,
/// Fasteners, Bearings, Standards, Constants — not a fake domain, because
/// the invariant this Work Package is proving belongs to
/// <see cref="ReferenceDataCatalog{TDefinition}"/> and its interaction with
/// each library's own real definitions, secondary keys included.
/// </para>
/// <para>
/// <b>What is random.</b> A sequence of up to ten step codes, each meaning
/// "revise", "advance one lifecycle state", "retreat one lifecycle state" or
/// "supersede", applied in order against a single record, skipping any step
/// not currently valid (a revise while Released, a supersede before
/// Released) rather than the generator having to know the state machine
/// itself. Each of the up to hundred generated sequences runs against a
/// fresh in-memory store, so iterations never interfere with one another.
/// </para>
/// <para>
/// <b>Why definitions compare by their own JSON, not by <c>Equals</c>.</b>
/// Every one of these five definition types is a <c>record</c>, but several
/// carry a <c>Dictionary</c>-typed property (<see cref="MaterialDefinition.Properties"/>,
/// <see cref="BearingDefinition.ManufacturerAttributes"/>), and a record's
/// compiler-generated <c>Equals</c> compares a <c>Dictionary</c> field by
/// reference, not by content — so a definition read back after a JSON round
/// trip (a fresh <c>Dictionary</c> instance, identical content) would
/// compare unequal to the one that was written even though nothing was
/// lost. Serialising both sides with the exact
/// <see cref="ReferenceSerialisation.Options"/> the catalogue itself writes
/// with, and comparing the resulting strings, is what the catalogue's own
/// on-disk contract actually promises.
/// </para>
/// </remarks>
public class ReferenceSupersessionPropertyTests
{
    private static readonly Gen<int[]> Steps = Gen.Int[0, 3].Array[1, 10];

    [Fact]
    public Task Materials_SupersessionInvariant_HoldsOverRandomSequences() =>
        RunAsync(
            (docs, store) => new MaterialCatalog(docs, store),
            Pool(MaterialSeed.Instance.Records));

    [Fact]
    public Task Fasteners_SupersessionInvariant_HoldsOverRandomSequences() =>
        RunAsync(
            (docs, store) => new FastenerCatalog(docs, store),
            Pool(FastenerSeed.Instance.Records));

    [Fact]
    public Task Bearings_SupersessionInvariant_HoldsOverRandomSequences() =>
        RunAsync(
            (docs, store) => new BearingCatalog(docs, store),
            Pool(BearingSeed.Instance.Records));

    [Fact]
    public Task Standards_SupersessionInvariant_HoldsOverRandomSequences() =>
        RunAsync(
            (docs, store) => new StandardCatalog(docs, store),
            Pool(StandardSeed.Instance.Records));

    [Fact]
    public Task Constants_SupersessionInvariant_HoldsOverRandomSequences() =>
        RunAsync(
            (docs, store) => new ConstantCatalog(docs, store),
            Pool(ConstantSeed.Instance.Records));

    private static IReadOnlyList<(TDefinition Definition, ReferenceProvenance Provenance)> Pool<TDefinition>(
        IReadOnlyList<ReferenceSeedRecord<TDefinition>> records)
        where TDefinition : class =>
        records.Select(r => (r.Definition, Verified(r.Provenance))).ToList();

    /// <summary>
    /// Every seeded record is unverified by design (`Population/SeedDatasetTests.EverySeededRecord_NamesASourceAndClaimsNoVerification`);
    /// this property test drives real lifecycle transitions including
    /// Released, which requires verification, so it stamps one on here
    /// rather than borrowing an unrelated permission-checked service to get
    /// there.
    /// </summary>
    private static ReferenceProvenance Verified(ReferenceProvenance provenance) => provenance with
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "supersession-property-reviewer",
        VerificationDate = new DateOnly(2026, 1, 1),
    };

    private static async Task RunAsync<TDefinition>(
        Func<IEngineeringDocumentStore, IPersistenceStore, IReferenceDataCatalog<TDefinition>> makeCatalog,
        IReadOnlyList<(TDefinition Definition, ReferenceProvenance Provenance)> pool)
        where TDefinition : class
    {
        Assert.True(pool.Count >= 2, "The property needs at least two distinct records: one to revise through, one to supersede onto.");

        await Steps.SampleAsync(steps => ExerciseOnceAsync(makeCatalog, pool, steps));
    }

    private static async Task ExerciseOnceAsync<TDefinition>(
        Func<IEngineeringDocumentStore, IPersistenceStore, IReferenceDataCatalog<TDefinition>> makeCatalog,
        IReadOnlyList<(TDefinition Definition, ReferenceProvenance Provenance)> pool,
        int[] steps)
        where TDefinition : class
    {
        var persistenceStore = new InMemoryPersistenceStore();
        var documentStore = new EngineeringDocumentStore(persistenceStore, new CurrentPrincipalAccessor());
        var catalog = makeCatalog(documentStore, persistenceStore);

        var primary = await catalog.RegisterAsync("primary", pool[0].Definition, pool[0].Provenance);
        await catalog.RegisterAsync("replacement", pool[1].Definition, pool[1].Provenance);

        // "primary" only ever revises onto a definition from this list —
        // never index 1, which "replacement" already holds. A library that
        // enforces a secondary key (a designation, a symbol) would
        // otherwise refuse the revision outright, which is a real rule
        // this test must respect rather than a flake to route around.
        var reviseIndices = Enumerable.Range(0, pool.Count).Where(i => i != 1).ToList();

        // Every pin ever minted for "primary": the revision number, and the
        // exact content that revision must still read back as.
        var pins = new List<(int Revision, string ExpectedJson)> { (primary.RevisionNumber, Json(pool[0].Definition)) };
        var state = ReferenceValidationState.Draft;
        var superseded = false;
        var reviseCursor = 0;

        foreach (var step in steps)
        {
            if (superseded)
                break;

            switch (((step % 4) + 4) % 4)
            {
                case 0: // Revise, where the record is still revisable.
                    if (ReferenceValidationStates.IsRevisable(state))
                    {
                        reviseCursor = (reviseCursor + 1) % reviseIndices.Count;
                        var next = pool[reviseIndices[reviseCursor]];
                        var revised = await catalog.ReviseAsync("primary", next.Definition, next.Provenance, "Property revision.");
                        pins.Add((revised.RevisionNumber, Json(next.Definition)));
                    }
                    break;

                case 1: // Advance one lifecycle state.
                    var forward = state switch
                    {
                        ReferenceValidationState.Draft => ReferenceValidationState.Checked,
                        ReferenceValidationState.Checked => ReferenceValidationState.Validated,
                        ReferenceValidationState.Validated => ReferenceValidationState.Released,
                        _ => (ReferenceValidationState?)null,
                    };
                    if (forward is { } target)
                    {
                        await catalog.SetValidationStateAsync("primary", target, "Property transition.");
                        state = target;
                    }
                    break;

                case 2: // Retreat one lifecycle state — a permitted down-transition.
                    var backward = state switch
                    {
                        ReferenceValidationState.Checked => ReferenceValidationState.Draft,
                        ReferenceValidationState.Validated => ReferenceValidationState.Checked,
                        _ => (ReferenceValidationState?)null,
                    };
                    if (backward is { } retreat)
                    {
                        await catalog.SetValidationStateAsync("primary", retreat, "Property retreat.");
                        state = retreat;
                    }
                    break;

                case 3: // Supersede — only reachable, and only permitted, once Released.
                    if (state == ReferenceValidationState.Released)
                    {
                        await catalog.SupersedeAsync("primary", "replacement", "Property supersession.");
                        superseded = true;
                    }
                    break;
            }
        }

        // The invariant: every pin ever minted still resolves to its exact
        // content, whatever happened afterwards.
        foreach (var (revision, expectedJson) in pins)
        {
            var atRevision = await catalog.GetRevisionAsync("primary", revision);
            Assert.Equal(expectedJson, Json(atRevision.Definition));
        }

        // A superseded record reports its own state rather than vanishing.
        var found = await catalog.FindAsync("primary");
        Assert.NotNull(found);

        if (superseded)
            Assert.Equal(ReferenceValidationState.Superseded, found!.ValidationState);
    }

    private static string Json<TDefinition>(TDefinition definition) =>
        JsonSerializer.Serialize(definition, ReferenceSerialisation.Options);
}
