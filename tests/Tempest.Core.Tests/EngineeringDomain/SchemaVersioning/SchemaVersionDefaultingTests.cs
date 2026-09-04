using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Tests.EngineeringDomain.SchemaVersioning;

/// <summary>
/// `TD-87`/`ADR-0120` Decision 1 — the explicit normalisation:
/// <c>SchemaVersion &lt;= 0</c> becomes <c>1</c>, whether the property is
/// entirely absent (the CLR default for a missing constructor argument) or
/// written out as <c>0</c> or a negative number by some future bug. Never
/// relied on a serialiser default doing this correctly on its own.
/// </summary>
public class SchemaVersionDefaultingTests
{
    private const string Id = "cccccccc-cccc-cccc-cccc-cccccccccccc";

    private static readonly string RecordBody =
        $$"""
        "Id":"{{Id}}","Kind":"Part","Identifier":"PN-1","DisplayName":"Bracket",
        "Metadata":{},"Status":0,"ParentId":null,"IsDeleted":false,
        "BomLine":{"Quantity":1,"UnitOfMeasure":null,"FindNumber":null,"ItemNumber":null,"ReferenceDesignator":null},
        "History":[],"Attachments":[],"TypeState":{}
        """;

    private static string RecordJson(string? schemaVersionProperty) => "{" + schemaVersionProperty + RecordBody + "}";

    [Fact]
    public async Task ARecordWithNoSchemaVersionPropertyAtAll_LoadsAsVersion1()
    {
        var persistence = new InMemoryPersistenceStore();
        var objectId = Guid.Parse(Id);
        await persistence.WriteAsync(EngineeringObjectStateStore.StateCollectionName, objectId.ToString("N"), RecordJson(null));

        var state = await new EngineeringObjectStateStore(persistence).FindAsync(objectId);

        Assert.NotNull(state);
        Assert.Equal(1, state!.SchemaVersion);
    }

    [Fact]
    public async Task ARecordWithSchemaVersionWrittenAsZero_LoadsAsVersion1_Identically()
    {
        var persistence = new InMemoryPersistenceStore();
        var objectId = Guid.Parse(Id);
        await persistence.WriteAsync(EngineeringObjectStateStore.StateCollectionName, objectId.ToString("N"), RecordJson("\"SchemaVersion\":0,"));

        var state = await new EngineeringObjectStateStore(persistence).FindAsync(objectId);

        Assert.NotNull(state);
        Assert.Equal(1, state!.SchemaVersion);
    }

    [Fact]
    public async Task ARecordWithSchemaVersionWrittenAsOneExplicitly_LoadsAsVersion1_Identically()
    {
        var persistence = new InMemoryPersistenceStore();
        var objectId = Guid.Parse(Id);
        await persistence.WriteAsync(EngineeringObjectStateStore.StateCollectionName, objectId.ToString("N"), RecordJson("\"SchemaVersion\":1,"));

        var state = await new EngineeringObjectStateStore(persistence).FindAsync(objectId);

        Assert.NotNull(state);
        Assert.Equal(1, state!.SchemaVersion);
    }
}
