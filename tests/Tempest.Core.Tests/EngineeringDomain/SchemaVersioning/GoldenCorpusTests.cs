using System.Text.Json;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Tests.EngineeringDomain.SchemaVersioning;

/// <summary>
/// `TD-87`/`ADR-0120` — the standing regression check the golden corpus
/// exists for: every record `v1` of this platform ever actually produced
/// (`GoldenCorpus/v1/*.json`, committed byte-for-byte, numeric enums, no
/// <c>SchemaVersion</c> at all) still loads through the real
/// <see cref="EngineeringObjectStateStore"/> read path, and comes back
/// normalised to <see cref="EngineeringObjectStateStore.CurrentSchemaVersion"/>.
/// </summary>
/// <remarks>
/// This must keep passing after every future schema bump — that is the
/// whole point of committing the fixtures rather than asserting the same
/// claim once, by hand, at the moment a migration is written.
/// </remarks>
public class GoldenCorpusTests
{
    private static string CorpusDirectory =>
        Path.Combine(AppContext.BaseDirectory, "EngineeringDomain", "SchemaVersioning", "GoldenCorpus", "v1");

    public static IEnumerable<object[]> CorpusFiles() =>
        Directory.GetFiles(CorpusDirectory, "*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { f });

    [Fact]
    public void TheCorpusDirectory_HasAtLeastTheDocumentedFixtures()
    {
        var names = Directory.GetFiles(CorpusDirectory, "*.json").Select(Path.GetFileName).ToList();

        Assert.Contains("part-minimal.json", names);
        Assert.Contains("part-with-history.json", names);
        Assert.Contains("part-with-attachment-no-hash.json", names);
        Assert.Contains("part-with-attachment-and-hash.json", names);
        Assert.Contains("assembly-with-parent.json", names);
        Assert.Contains("project-with-typestate.json", names);
        Assert.Contains("deleted-object.json", names);
    }

    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public async Task EveryFixture_LoadsThroughTheRealReadPath_NormalisedToTheCurrentSchemaVersion(string fixturePath)
    {
        var json = await File.ReadAllTextAsync(fixturePath);
        var objectId = ReadId(json);

        var persistence = new InMemoryPersistenceStore();
        await persistence.WriteAsync(EngineeringObjectStateStore.StateCollectionName, objectId.ToString("N"), json);

        var store = new EngineeringObjectStateStore(persistence);
        var state = await store.FindAsync(objectId);

        Assert.NotNull(state);
        Assert.Equal(EngineeringObjectStateStore.CurrentSchemaVersion, state!.SchemaVersion);
        Assert.Equal(objectId, state.Id);
    }

    [Fact]
    public async Task TheWholeCorpus_AlsoComesBackTogether_ThroughListAsync()
    {
        var files = Directory.GetFiles(CorpusDirectory, "*.json");
        var persistence = new InMemoryPersistenceStore();

        foreach (var file in files)
        {
            var json = await File.ReadAllTextAsync(file);
            await persistence.WriteAsync(EngineeringObjectStateStore.StateCollectionName, ReadId(json).ToString("N"), json);
        }

        var store = new EngineeringObjectStateStore(persistence);
        var states = await store.ListAsync();

        Assert.Equal(files.Length, states.Count);
        Assert.All(states, s => Assert.Equal(EngineeringObjectStateStore.CurrentSchemaVersion, s.SchemaVersion));
    }

    private static Guid ReadId(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("Id").GetGuid();
    }
}
