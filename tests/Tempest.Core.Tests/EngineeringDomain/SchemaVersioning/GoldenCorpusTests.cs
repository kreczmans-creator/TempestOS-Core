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

        // `v0.16.0` review board: asserting only `Id` and `SchemaVersion`
        // let a mutation that dropped or corrupted every *other* field pass
        // this test silently — which defeats the corpus's own stated
        // purpose, since a real data-transforming migration is exactly
        // where such a loss would happen. Every field-bearing branch of
        // the fixture is now compared against what the read path handed
        // back.
        AssertEveryFieldSurvived(json, state);
    }

    /// <summary>
    /// Compares every field the fixture actually declares against the
    /// record the real read path produced. Fixtures differ in shape (a
    /// deleted object, a part with attachments, a bare object), so each
    /// branch is compared only when the fixture carries it — an absent
    /// property is not a failure, but a present one that came back
    /// different is.
    /// </summary>
    private static void AssertEveryFieldSurvived(string json, EngineeringObjectState state)
    {
        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal(root.GetProperty("Kind").GetString(), state.Kind);
        Assert.Equal(root.GetProperty("DisplayName").GetString(), state.DisplayName);

        if (root.TryGetProperty("Identifier", out var identifier))
            Assert.Equal(identifier.ValueKind == JsonValueKind.Null ? null : identifier.GetString(), state.Identifier);

        if (root.TryGetProperty("IsDeleted", out var isDeleted))
            Assert.Equal(isDeleted.GetBoolean(), state.IsDeleted);

        if (root.TryGetProperty("ParentId", out var parentId))
        {
            Assert.Equal(
                parentId.ValueKind == JsonValueKind.Null ? null : parentId.GetGuid(),
                state.ParentId);
        }

        // The fixtures are pre-`v0.16.0` records, so `Status` is a numeric
        // ordinal. That it still lands on the same `LifecycleState` member
        // after `ADR-0120` Decision 4 made enums serialise as strings is
        // the single most important thing this corpus proves — `TD-87`'s
        // named failure was exactly a status silently reinterpreted.
        if (root.TryGetProperty("Status", out var status) && status.ValueKind == JsonValueKind.Number)
            Assert.Equal(status.GetInt32(), (int)state.Status);

        // `Metadata` — every fixture carries this facet, and two of them
        // (`assembly-with-parent.json`) carry real, non-null values for
        // it. Left unasserted, a read path that silently wiped the whole
        // facet to `EngineeringObjectMetadata.Empty` passed this corpus
        // (`WP16.4A-R1`'s own mutation proof).
        if (root.TryGetProperty("Metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
        {
            static string? OptionalString(JsonElement parent, string name) =>
                parent.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;

            Assert.Equal(OptionalString(metadata, "Category"), state.Metadata.Category);
            Assert.Equal(OptionalString(metadata, "Discipline"), state.Metadata.Discipline);
            Assert.Equal(OptionalString(metadata, "Owner"), state.Metadata.Owner);
            Assert.Equal(OptionalString(metadata, "Classification"), state.Metadata.Classification);
            Assert.Equal(OptionalString(metadata, "Notes"), state.Metadata.Notes);

            if (metadata.TryGetProperty("Tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                Assert.Equal(tags.EnumerateArray().Select(t => t.GetString()), state.Metadata.TagsOrEmpty);
            else
                Assert.Empty(state.Metadata.TagsOrEmpty);
        }

        if (root.TryGetProperty("BomLine", out var bomLine) && bomLine.ValueKind == JsonValueKind.Object)
        {
            Assert.Equal(bomLine.GetProperty("Quantity").GetDecimal(), state.BomLine.Quantity);

            static string? OptionalString(JsonElement parent, string name) =>
                parent.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;

            Assert.Equal(OptionalString(bomLine, "UnitOfMeasure"), state.BomLine.UnitOfMeasure);
            Assert.Equal(OptionalString(bomLine, "FindNumber"), state.BomLine.FindNumber);
            Assert.Equal(OptionalString(bomLine, "ItemNumber"), state.BomLine.ItemNumber);
            Assert.Equal(OptionalString(bomLine, "ReferenceDesignator"), state.BomLine.ReferenceDesignator);
        }

        if (root.TryGetProperty("History", out var history) && history.ValueKind == JsonValueKind.Array)
        {
            Assert.Equal(history.GetArrayLength(), state.History.Count);

            var index = 0;
            foreach (var transition in history.EnumerateArray())
            {
                var loaded = state.History[index++];

                Assert.Equal(transition.GetProperty("From").GetInt32(), (int)loaded.From);
                Assert.Equal(transition.GetProperty("To").GetInt32(), (int)loaded.To);
                Assert.Equal(transition.GetProperty("ActorPrincipalId").GetString(), loaded.ActorPrincipalId);
                Assert.Equal(transition.GetProperty("OccurredAt").GetDateTimeOffset(), loaded.OccurredAt);

                var approvalId = transition.GetProperty("ApprovalId");
                Assert.Equal(approvalId.ValueKind == JsonValueKind.Null ? null : approvalId.GetGuid(), loaded.ApprovalId);
            }
        }

        if (root.TryGetProperty("Attachments", out var attachments) && attachments.ValueKind == JsonValueKind.Array)
        {
            Assert.Equal(attachments.GetArrayLength(), state.Attachments.Count);

            var index = 0;
            foreach (var attachment in attachments.EnumerateArray())
            {
                var loaded = state.Attachments[index++];

                Assert.Equal(attachment.GetProperty("Id").GetGuid(), loaded.Id);
                Assert.Equal(attachment.GetProperty("FileName").GetString(), loaded.FileName);
                Assert.Equal(attachment.GetProperty("ContentType").GetString(), loaded.ContentType);
                Assert.Equal(attachment.GetProperty("SizeInBytes").GetInt64(), loaded.SizeInBytes);
                Assert.Equal(attachment.GetProperty("ContentHash").GetString(), loaded.ContentHash);
            }
        }

        if (root.TryGetProperty("TypeState", out var typeState) && typeState.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in typeState.EnumerateObject())
            {
                Assert.True(state.TypeState.ContainsKey(property.Name), $"TypeState lost '{property.Name}'.");
                Assert.Equal(
                    property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString(),
                    state.TypeState[property.Name]);
            }
        }
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
