using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.Calc.Library.Dashboard;

/// <summary>Where one finding stands in <c>docs/testing/v0.26.0-known-defects.md</c> (Addendum C, C-4).</summary>
public enum VerificationDefectStatus
{
    /// <summary>The finding is fixed (by Track S, or closed by Track P's own re-check).</summary>
    Fixed,

    /// <summary>The finding is still open.</summary>
    StillOpen,

    /// <summary>The finding waits for the owner's decision against a licensed standard.</summary>
    OwnerDecision,

    /// <summary>The triage found it is not a defect.</summary>
    NotADefect,
}

/// <summary>The last real run of one template's known-answer cases and worked examples (<c>docs/testing/kat-results.json</c>).</summary>
public sealed record SnapshotKat(string Version, int Kats, int KatsPassed, IReadOnlyList<string> FailedKats, int ExamplesPassed, IReadOnlyList<string> FailedExamples);

/// <summary>One Track P finding of a template: its id, its severity (Critical, High, Medium, Low, Info, Rejected) and its defect status where the known-defects list or Track P's re-check gives one.</summary>
public sealed record SnapshotFinding(string Id, string Severity, VerificationDefectStatus? Defect);

/// <summary>Track P's re-derivation report of one template or chain: the result code, the verdict line and the findings.</summary>
public sealed record SnapshotRederivation(string Result, string Verdict, IReadOnlyList<SnapshotFinding> Findings);

/// <summary>Everything the documents say about one template (by id) or chain (by pack template id).</summary>
public sealed record SnapshotItem(int WorkedExamples, int ExampleChecks, int ExampleChecksPassed, SnapshotKat? Kat, SnapshotRederivation? Rederivation);

/// <summary>
/// Addendum C, C-4: the document-derived data the verification dashboard shows, read once by <c>scripts/generate-verification-snapshot.py</c>
/// from the Track P re-derivation reports, the known-defects list, the KAT results file and the worked examples, and embedded in this
/// assembly. The Desktop may run where <c>docs/</c> is not present (an installed build), so the dashboard never reads the repository at run
/// time. <see cref="Sources"/> holds a SHA-256 of each group of input documents; <c>VerificationSnapshotStaleTests</c> recomputes them and
/// fails when the snapshot was made from older ones.
/// </summary>
public sealed class VerificationSnapshot
{
    /// <summary>The embedded resource's name.</summary>
    public const string ResourceName = "Tempest.Calc.Library.Dashboard.verification-snapshot.json";

    /// <summary>The snapshot format this code reads.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The largest snapshot text read (characters). The shipped file is about 65 000.</summary>
    public const int MaxJsonChars = 4_000_000;

    /// <summary>The most items (templates and chains) read.</summary>
    public const int MaxItems = 1000;

    /// <summary>The most findings read for one item.</summary>
    public const int MaxFindingsPerItem = 500;

    /// <summary>The longest string kept (a verdict, an id).</summary>
    public const int MaxTextLength = 300;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 16,
        Converters = { new DefectStatusConverter() },
    };

    private VerificationSnapshot(IReadOnlyDictionary<string, SnapshotItem> items, IReadOnlyDictionary<string, string> sources, string? problem)
    {
        Items = items;
        Sources = sources;
        Problem = problem;
    }

    /// <summary>The data by template id or pack template id.</summary>
    public IReadOnlyDictionary<string, SnapshotItem> Items { get; }

    /// <summary>The SHA-256 of each group of input documents the snapshot was made from.</summary>
    public IReadOnlyDictionary<string, string> Sources { get; }

    /// <summary>Why the snapshot could not be read (the dashboard then shows only what the catalogue gives), or <see langword="null"/>.</summary>
    public string? Problem { get; }

    /// <summary>A snapshot with no data, and the reason.</summary>
    public static VerificationSnapshot Unavailable(string problem) => new(new Dictionary<string, SnapshotItem>(), new Dictionary<string, string>(), problem);

    /// <summary>A snapshot made from <paramref name="items"/> (a test's own data).</summary>
    public static VerificationSnapshot From(IReadOnlyDictionary<string, SnapshotItem> items) => new(items, new Dictionary<string, string>(), null);

    /// <summary>The snapshot embedded in this assembly. Never throws: an unreadable one is <see cref="Unavailable"/>.</summary>
    public static VerificationSnapshot LoadEmbedded()
    {
        try
        {
            using var stream = typeof(VerificationSnapshot).Assembly.GetManifestResourceStream(ResourceName);

            if (stream is null)
                return Unavailable("The verification data is not part of this build.");

            // Bound the read: the file is embedded by this project, but the cap holds for any stream the same way.
            using var reader = new StreamReader(stream);
            var text = new System.Text.StringBuilder();
            var chunk = new char[16_384];
            int read;

            while ((read = reader.Read(chunk, 0, chunk.Length)) > 0)
            {
                text.Append(chunk, 0, read);

                if (text.Length > MaxJsonChars)
                    return Unavailable("The verification data is larger than this program reads.");
            }

            return Parse(text.ToString());
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
        {
            return Unavailable("The verification data could not be read.");
        }
    }

    /// <summary>Reads a snapshot from its JSON text; text over <see cref="MaxJsonChars"/> or not valid is <see cref="Unavailable"/>.</summary>
    public static VerificationSnapshot Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (json.Length > MaxJsonChars)
            return Unavailable("The verification data is larger than this program reads.");

        try
        {
            var file = JsonSerializer.Deserialize<SnapshotFile>(json, Options);

            if (file is null || file.Schema != SchemaVersion)
                return Unavailable("The verification data is in a format this version does not read.");

            var items = new Dictionary<string, SnapshotItem>(StringComparer.Ordinal);

            foreach (var (id, item) in (file.Items ?? []).Take(MaxItems))
            {
                if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
                    continue;

                items[id] = Bound(item);
            }

            var sources = (file.Sources ?? []).Take(16).ToDictionary(kv => Clip(kv.Key), kv => Clip(kv.Value), StringComparer.Ordinal);
            return new VerificationSnapshot(items, sources, null);
        }
        catch (JsonException)
        {
            return Unavailable("The verification data is not valid.");
        }
    }

    private static SnapshotItem Bound(SnapshotItem item) => item with
    {
        WorkedExamples = Math.Clamp(item.WorkedExamples, 0, 10_000),
        ExampleChecks = Math.Clamp(item.ExampleChecks, 0, 100_000),
        ExampleChecksPassed = Math.Clamp(item.ExampleChecksPassed, 0, 100_000),
        Kat = item.Kat is null ? null : item.Kat with
        {
            Version = Clip(item.Kat.Version),
            Kats = Math.Clamp(item.Kat.Kats, 0, 100_000),
            KatsPassed = Math.Clamp(item.Kat.KatsPassed, 0, 100_000),
            FailedKats = [.. (item.Kat.FailedKats ?? []).Take(MaxFindingsPerItem).Select(Clip)],
            ExamplesPassed = Math.Clamp(item.Kat.ExamplesPassed, 0, 10_000),
            FailedExamples = [.. (item.Kat.FailedExamples ?? []).Take(MaxFindingsPerItem).Select(Clip)],
        },
        Rederivation = item.Rederivation is null ? null : item.Rederivation with
        {
            Result = Clip(item.Rederivation.Result),
            Verdict = Clip(item.Rederivation.Verdict),
            Findings = [.. (item.Rederivation.Findings ?? []).Take(MaxFindingsPerItem).Select(f => f with { Id = Clip(f.Id), Severity = Clip(f.Severity) })],
        },
    };

    internal static string Clip(string? text) => text is null ? string.Empty : text.Length > MaxTextLength ? text[..MaxTextLength] : text;

    private sealed record SnapshotFile(int Schema, Dictionary<string, string>? Sources, Dictionary<string, SnapshotItem>? Items);

    private sealed class DefectStatusConverter : JsonConverter<VerificationDefectStatus?>
    {
        public override VerificationDefectStatus? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? null : reader.GetString() switch
            {
                "fixed" => VerificationDefectStatus.Fixed,
                "open" => VerificationDefectStatus.StillOpen,
                "owner-decision" => VerificationDefectStatus.OwnerDecision,
                "not-a-defect" => VerificationDefectStatus.NotADefect,
                _ => throw new JsonException("An unknown defect status."),
            };

        public override void Write(Utf8JsonWriter writer, VerificationDefectStatus? value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value switch
            {
                VerificationDefectStatus.Fixed => "fixed",
                VerificationDefectStatus.StillOpen => "open",
                VerificationDefectStatus.OwnerDecision => "owner-decision",
                VerificationDefectStatus.NotADefect => "not-a-defect",
                _ => null,
            });
    }
}
