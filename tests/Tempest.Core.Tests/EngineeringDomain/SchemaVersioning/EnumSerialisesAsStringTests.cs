using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Tests.EngineeringDomain.SchemaVersioning;

/// <summary>
/// `TD-87`/`ADR-0120` Decision 4 — going forward, every enum reachable
/// from <see cref="EngineeringObjectState"/> serialises as its member
/// name, not its ordinal, so a future re-ordering of
/// <see cref="LifecycleState"/> can no longer silently reinterpret a
/// persisted status. Reading stays backward-compatible: a record still
/// holding the old numeric form reads back identically.
/// </summary>
public class EnumSerialisesAsStringTests
{
    private static EngineeringObjectState Part(Guid id) => new(
        EngineeringObjectStateStore.CurrentSchemaVersion, id, "Part", "PN-1", "Bracket",
        EngineeringObjectMetadata.Empty, LifecycleState.Released, null, false,
        EngineeringObjectBomLineState.Default, [], [], new Dictionary<string, string?>());

    [Fact]
    public async Task SaveAsync_WritesTheEnumAsItsMemberName_NotADigit()
    {
        var persistence = new InMemoryPersistenceStore();
        var store = new EngineeringObjectStateStore(persistence);
        var id = Guid.NewGuid();

        await store.SaveAsync(Part(id));

        // Bypasses the store's own Deserialise — the raw bytes on disk are
        // what this asserts against.
        var raw = await persistence.ReadAsync(EngineeringObjectStateStore.StateCollectionName, id.ToString("N"));

        Assert.NotNull(raw);
        Assert.Contains("\"Status\":\"Released\"", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Status\":3", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARecordStillHoldingTheOldNumericEnum_ReadsBackIdentically()
    {
        var id = Guid.NewGuid();
        var json =
            $$"""
            {"SchemaVersion":1,"Id":"{{id}}","Kind":"Part","Identifier":"PN-1","DisplayName":"Bracket",
            "Metadata":{},"Status":3,"ParentId":null,"IsDeleted":false,
            "BomLine":{"Quantity":1,"UnitOfMeasure":null,"FindNumber":null,"ItemNumber":null,"ReferenceDesignator":null},
            "History":[],"Attachments":[],"TypeState":{} }
            """;

        var persistence = new InMemoryPersistenceStore();
        await persistence.WriteAsync(EngineeringObjectStateStore.StateCollectionName, id.ToString("N"), json);

        var state = await new EngineeringObjectStateStore(persistence).FindAsync(id);

        Assert.NotNull(state);
        Assert.Equal(LifecycleState.Released, state!.Status);
    }

    [Fact]
    public async Task HistoryTransitions_AlsoSerialiseTheirEnumsAsNames()
    {
        var persistence = new InMemoryPersistenceStore();
        var store = new EngineeringObjectStateStore(persistence);
        var id = Guid.NewGuid();
        var withHistory = Part(id) with
        {
            History = [new EngineeringObjectTransitionState(LifecycleState.Draft, LifecycleState.InReview, "ada", DateTimeOffset.UtcNow, null)],
        };

        await store.SaveAsync(withHistory);

        var raw = await persistence.ReadAsync(EngineeringObjectStateStore.StateCollectionName, id.ToString("N"));

        Assert.NotNull(raw);
        Assert.Contains("\"From\":\"Draft\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"To\":\"InReview\"", raw, StringComparison.Ordinal);
    }
}
