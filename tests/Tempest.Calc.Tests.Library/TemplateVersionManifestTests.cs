using System.Runtime.CompilerServices;
using System.Text.Json;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// Merge review finding 1: two different templates both printed <c>struct.en1993-member@1.0.1</c> on sheets, because two lines of
/// development each bumped 1.0.0 to 1.0.1 and the merge of the two matched neither. A reference <c>id@version</c> must name one
/// content for ever. <c>TemplateVersionManifest.json</c>, committed beside this file, records the content hash (the full
/// <see cref="CalcTemplateHasher"/> hash, the one whose first 16 characters a sheet prints) of every shipped template at its version.
/// A template whose content changed under an unchanged version fails here; a new template or a new version fails until its hash is
/// recorded, which is a reviewed diff.
///
/// To add a new template or a bumped version: change the <c>Version</c> in the calculator, then run
/// <c>TEMPEST_UPDATE_TEMPLATE_MANIFEST=1 dotnet test tests/Tempest.Calc.Tests.Library --filter TemplateVersionManifestTests</c>
/// and commit the added line. The update only ever ADDS lines: it never changes the hash recorded for an existing
/// <c>id@version</c> (that is the failure this test exists to catch), so a changed template needs a new version, never an edited manifest.
/// Lines for versions that are no longer in the code are kept as the history of what each reference once meant. When merging two lines
/// that both bumped a template, take the manifest lines of both and give the merged template a version of its own.
/// </summary>
public sealed class TemplateVersionManifestTests
{
    public const string UpdateVariable = "TEMPEST_UPDATE_TEMPLATE_MANIFEST";

    private static string ManifestPath([CallerFilePath] string callerFilePath = "") =>
        Path.Combine(Path.GetDirectoryName(callerFilePath) ?? AppContext.BaseDirectory, "TemplateVersionManifest.json");

    private static SortedDictionary<string, string> Current() =>
        new(ShippedCalculators.All.ToDictionary(c => $"{c.Template.Id}@{c.Template.Version}", c => CalcTemplateHasher.Hash(c), StringComparer.Ordinal), StringComparer.Ordinal);

    private static SortedDictionary<string, string> Load(string path) =>
        File.Exists(path)
            ? new(JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [], StringComparer.Ordinal)
            : new(StringComparer.Ordinal);

    /// <summary>The problems between a manifest and the shipped templates: content changed under a version, or a version not yet recorded.</summary>
    internal static IReadOnlyList<string> Problems(IReadOnlyDictionary<string, string> manifest, IReadOnlyDictionary<string, string> shipped)
    {
        var problems = new List<string>();

        foreach (var (reference, hash) in shipped)
        {
            if (!manifest.TryGetValue(reference, out var recorded))
                problems.Add($"{reference} is not in the manifest (a new template or a new version). Record it with {UpdateVariable}=1.");
            else if (!string.Equals(recorded, hash, StringComparison.Ordinal))
                problems.Add($"{reference} has changed content (hash {recorded[..16]} recorded, {hash[..16]} now) under the same version. Bump the template's Version; do not edit the manifest.");
        }

        return problems;
    }

    [Fact]
    public void Every_Shipped_Template_Version_Has_The_Content_Recorded_In_The_Manifest()
    {
        var path = ManifestPath();
        var shipped = Current();
        var manifest = Load(path);

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            // Add only: an id@version already recorded keeps its hash, so a changed template still fails below.
            var added = false;

            foreach (var (reference, hash) in shipped)
            {
                if (manifest.TryAdd(reference, hash))
                    added = true;
            }

            if (added || !File.Exists(path))
                File.WriteAllText(path, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        Assert.True(File.Exists(path), $"{path} is missing; run with {UpdateVariable}=1 to create it.");
        var problems = Problems(manifest, shipped);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void The_Manifest_Is_Well_Formed()
    {
        var manifest = Load(ManifestPath());
        Assert.NotEmpty(manifest);

        foreach (var (reference, hash) in manifest)
        {
            var at = reference.LastIndexOf('@');
            Assert.True(at > 0 && at < reference.Length - 1, $"'{reference}' is not id@version.");
            Assert.Matches("^[0-9a-f]{64}$", hash);
        }

        // Every shipped template id appears, so a template cannot be dropped from the manifest silently.
        var recordedIds = manifest.Keys.Select(k => k[..k.LastIndexOf('@')]).ToHashSet(StringComparer.Ordinal);
        Assert.All(ShippedCalculators.All, c => Assert.Contains(c.Template.Id, recordedIds));
    }

    [Fact]
    public void A_Changed_Template_Under_An_Unchanged_Version_Is_Reported_And_A_Bump_Is_Not_Silently_Accepted()
    {
        var hashA = new string('a', 64);
        var hashB = new string('b', 64);
        var manifest = new Dictionary<string, string> { ["x.t@1.0.1"] = hashA };

        Assert.Empty(Problems(manifest, new Dictionary<string, string> { ["x.t@1.0.1"] = hashA }));

        var changed = Assert.Single(Problems(manifest, new Dictionary<string, string> { ["x.t@1.0.1"] = hashB }));
        Assert.Contains("Bump the template's Version", changed);

        var bumped = Assert.Single(Problems(manifest, new Dictionary<string, string> { ["x.t@1.1.0"] = hashB }));
        Assert.Contains("not in the manifest", bumped);
    }
}
