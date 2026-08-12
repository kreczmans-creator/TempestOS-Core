using System.Reflection;
using System.Text.RegularExpressions;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Component 3 (`ADR-0105`) — the additive, non-blocking consistency
/// check realising `WP11.0A Platform Architecture Review.md` Finding
/// `A-6`'s own second recommendation ("a build-time check validating
/// classification/relationship strings against the live registries").
/// Never validates a Kind/`Classification`/`RelationshipKind` value at
/// write time — an ordinary xUnit test, not a build gate, not a new
/// tool, discovered and run exactly like every other test in this
/// platform's own suite.
/// </summary>
/// <remarks>
/// **Lives in `Tempest.Desktop.Tests`, not `Tempest.Core.Tests`
/// (`WP 12.1A`'s own architecture review Finding 2, closed here).**
/// `Tempest.Core.Tests` references `Tempest.Core`/`Tempest.Samples`/
/// `Tempest.App`/`Tempest.Validation` — never `Tempest.Desktop` — so a
/// consistency check placed there could never reflect over
/// `Tempest.Desktop.DigitalThread.DigitalThreadGraphModel`, the exact
/// class whose own confirmed cross-layer duplicate motivated this
/// entire mechanism. `Tempest.Desktop.Tests` already references all
/// three layers this platform has, and is therefore the only test
/// project able to reflect across every layer a vocabulary value's own
/// declaration or duplicate might live in.
/// </remarks>
/// <remarks>
/// Reflects only the classes and fields the Engineering Vocabulary
/// Register itself names — never every `public const string` field on
/// a registered class indiscriminately. Several registered classes
/// (`RequirementsService`, `VerificationService`, `CalculationEngine`)
/// also declare real, legitimate, non-vocabulary constants of their own
/// (persistence collection names, a default "unknown" principal Id —
/// `"unknown"` alone is independently declared by three different
/// classes, a real, confirmed, but entirely unrelated collision) — a
/// blind, unscoped reflection scan would misreport these as vocabulary
/// duplicates. The Engineering Vocabulary Register is this test's own
/// source of truth for which fields are Kind/`Classification`/
/// `RelationshipKind` vocabulary in the first place.
/// </remarks>
public sealed class EngineeringVocabularyConsistencyTests
{
    /// <summary>
    /// `references` is declared, independently and intentionally, by
    /// both <c>RequirementRelationshipKinds</c> and
    /// <c>VerificationService</c> — a disclosed, pre-existing exception
    /// to the "one value, one owner" rule, not a defect (see the
    /// register's own † footnote and `ADR-0073`'s own already-accepted
    /// "vocabulary drift" risk). Named here explicitly, not silently
    /// excluded, so a reader of this test can see exactly what it does
    /// and does not enforce.
    /// </summary>
    private const string DisclosedDualOwnerValue = "references";

    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string RegisterPath = Path.Combine(RepositoryRoot, "docs", "governance", "Engineering", "Engineering Vocabulary Register.md");

    private readonly record struct VocabularyEntry(string Vocabulary, string Value, string TypeName, string FieldName);

    [Fact]
    public void EveryRegisteredValue_MatchesItsRealDeclaredConstant()
    {
        var entries = ParseRegister();
        Assert.NotEmpty(entries);

        var mismatches = new List<string>();
        foreach (var entry in entries)
        {
            var field = ResolveField(entry.TypeName, entry.FieldName);
            if (field is null)
            {
                mismatches.Add(
                    $"{entry.TypeName}.{entry.FieldName} (registered as {entry.Vocabulary} '{entry.Value}') " +
                    "does not exist as a public const string — the Engineering Vocabulary Register has drifted from code.");
                continue;
            }

            var actualValue = (string)field.GetRawConstantValue()!;
            if (!string.Equals(actualValue, entry.Value, StringComparison.Ordinal))
            {
                mismatches.Add(
                    $"{entry.TypeName}.{entry.FieldName} is declared as '{actualValue}', but the Engineering " +
                    $"Vocabulary Register records it as '{entry.Value}'.");
            }
        }

        Assert.True(mismatches.Count == 0, "Engineering Vocabulary Register drift found:\n" + string.Join("\n", mismatches));
    }

    [Fact]
    public void NoTwoDeclaringClasses_ShareTheIdenticalValue_ExceptTheOneDisclosedException()
    {
        var entries = ParseRegister();

        var duplicates = entries
            .GroupBy(e => (e.Vocabulary, e.Value))
            .Where(g => g.Key.Value != DisclosedDualOwnerValue)
            .Where(g => g.Select(e => $"{e.TypeName}.{e.FieldName}").Distinct().Count() > 1)
            .ToList();

        Assert.True(duplicates.Count == 0, "Duplicate vocabulary declarations found, across different owners:\n" + string.Join("\n", duplicates.Select(g =>
            $"'{g.Key.Value}' ({g.Key.Vocabulary}) is declared as a named constant by more than one class: " +
            string.Join(", ", g.Select(e => $"{e.TypeName}.{e.FieldName}").Distinct()))));
    }

    [Fact]
    public void TheRegisterItself_NeverDeclaresTheSameConstant_WithTwoDifferentValues()
    {
        var entries = ParseRegister();

        var contradictions = entries
            .GroupBy(e => $"{e.TypeName}.{e.FieldName}")
            .Where(g => g.Select(e => e.Value).Distinct().Count() > 1)
            .ToList();

        Assert.True(contradictions.Count == 0, "Duplicate canonical declarations found, within the register itself:\n" + string.Join("\n", contradictions.Select(g =>
            $"{g.Key} is listed with more than one value: {string.Join(", ", g.Select(e => $"'{e.Value}'").Distinct())}")));
    }

    /// <summary>
    /// The one check here that is not driven by the register alone — it
    /// scans every type in <c>Tempest.Core</c>/<c>Tempest.App</c>/
    /// <c>Tempest.Samples</c>/<c>Tempest.Desktop</c> (public and private
    /// `const string` fields, since the confirmed, motivating
    /// <c>DigitalThreadGraphModel</c> duplicates were `private`) for any
    /// class, not already the registered owner, redeclaring a registered
    /// value as its own named constant — the exact
    /// <c>VerifiedByRelationshipKind</c> failure mode this Work Package
    /// found and fixed, now caught automatically the moment a future
    /// instance of it is introduced, in any class, not only ones the
    /// register already happens to name.
    /// </summary>
    [Fact]
    public void NoUnregisteredClass_RedeclaresARegisteredValueAsItsOwnConstant()
    {
        var entries = ParseRegister();
        var registeredOwners = entries.Select(e => e.TypeName).ToHashSet();
        var registeredValuesByVocabulary = entries
            .Where(e => e.Value != DisclosedDualOwnerValue)
            .ToLookup(e => e.Value, e => e.TypeName);

        var assemblies = new[] { "Tempest.Core", "Tempest.App", "Tempest.Samples", "Tempest.Desktop" };
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => assemblies.Contains(a.GetName().Name))
            .SelectMany(a => a.GetTypes());

        var rogueDuplicates = new List<string>();
        foreach (var type in types)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!field.IsLiteral || field.FieldType != typeof(string))
                    continue;

                var value = (string?)field.GetRawConstantValue();
                if (value is null || !registeredValuesByVocabulary.Contains(value))
                    continue;

                if (registeredOwners.Contains(type.FullName ?? type.Name))
                    continue; // the registered owner itself, or a value it also legitimately holds.

                rogueDuplicates.Add($"{type.FullName}.{field.Name} = \"{value}\" duplicates a value already canonically owned by {string.Join(" or ", registeredValuesByVocabulary[value])}.");
            }
        }

        Assert.True(rogueDuplicates.Count == 0, "Unregistered classes redeclaring a canonical value found:\n" + string.Join("\n", rogueDuplicates));
    }

    // ---- Parsing ----

    private static List<VocabularyEntry> ParseRegister()
    {
        var lines = File.ReadAllLines(RegisterPath);
        var entries = new List<VocabularyEntry>();
        string? currentVocabulary = null;

        foreach (var line in lines)
        {
            var headerMatch = Regex.Match(line, @"^## Entries — (Kind|Classification|RelationshipKind)\s*$");
            if (headerMatch.Success)
            {
                currentVocabulary = headerMatch.Groups[1].Value;
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal) && currentVocabulary is not null)
                currentVocabulary = null; // left the Entries section

            if (currentVocabulary is null || !line.StartsWith('|'))
                continue;

            var cells = line.Split('|', StringSplitOptions.TrimEntries).Where(c => c.Length > 0).ToArray();
            if (cells.Length < 2 || cells[0].StartsWith("---", StringComparison.Ordinal) || cells[0] == "Value")
                continue;

            var value = StripMarkup(cells[0]);
            var declaringClass = StripMarkup(cells[1]);

            if (declaringClass.Contains("Undeclared", StringComparison.Ordinal))
                continue; // no canonical constant to check — the register's own honest disclosure, not a gap this test covers.

            var lastDot = declaringClass.LastIndexOf('.');
            if (lastDot < 0)
                continue;

            var typeName = declaringClass[..lastDot];
            var fieldName = declaringClass[(lastDot + 1)..];

            entries.Add(new VocabularyEntry(currentVocabulary, value, typeName, fieldName));
        }

        return entries;
    }

    private static string StripMarkup(string cell) =>
        cell.Replace("`", "").Replace("†", "").Trim();

    // ---- Reflection ----

    private static FieldInfo? ResolveField(string typeName, string fieldName)
    {
        var type = ResolveType(typeName);
        return type?.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
    }

    private static Type? ResolveType(string fullName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName, throwOnError: false))
            .FirstOrDefault(t => t is not null);

    // ---- Repository root discovery (mirrors Tempest.Core.Tests.Templates.RepositoryPaths) ----

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (global.json) above '{AppContext.BaseDirectory}'.");
    }
}
