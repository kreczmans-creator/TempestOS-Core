using System.Reflection;
using System.Text.RegularExpressions;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Every Desktop test runs in exactly one CI shard (`v0.23.0` CI board
/// G-24). <c>.github/workflows/ci.yml</c> splits this assembly into three
/// <c>dotnet test --filter</c> legs by the first letter after
/// <c>Tempest.Desktop.Tests.</c>; a test whose fully-qualified name no
/// filter matches (a class outside that root namespace, or a segment that
/// starts with a digit or underscore) would silently never run in CI, and
/// one two filters match would run twice.
/// </summary>
/// <remarks>
/// The filters are read from ci.yml itself rather than restated here, so a
/// moved boundary letter is checked against the real workflow. The match
/// mirrors VSTest's <c>FullyQualifiedName~</c> operator: a case-insensitive
/// substring test against <c>Namespace.Class.Method</c>.
/// </remarks>
public sealed class CiShardCoverageTests
{
    private static readonly Regex FilterPattern = new(@"--filter ""(FullyQualifiedName~[^""]+)""", RegexOptions.Compiled);

    private static IReadOnlyList<string[]> ShardFilters()
    {
        var workflow = File.ReadAllText(Path.Combine(DesktopTestHelpers.RepositoryRoot, ".github", "workflows", "ci.yml"));
        return FilterPattern.Matches(workflow)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Select(filter => filter.Split('|').Select(term => term["FullyQualifiedName~".Length..]).ToArray())
            .ToList();
    }

    private static IEnumerable<string> TestNames() =>
        typeof(CiShardCoverageTests).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.GetCustomAttributes(inherit: true).OfType<FactAttribute>().Any())
                .Select(m => $"{t.FullName}.{m.Name}"));

    [Fact]
    public void CiDeclaresThreeDesktopShardFilters()
    {
        var filters = ShardFilters();
        Assert.Equal(3, filters.Count);
        Assert.All(filters, f => Assert.All(f, term => Assert.StartsWith("Tempest.Desktop.Tests.", term, StringComparison.Ordinal)));
    }

    [Fact]
    public void EveryDesktopTest_IsSelectedByExactlyOneCiShard()
    {
        var filters = ShardFilters();
        var names = TestNames().ToList();
        Assert.NotEmpty(names);

        var problems = names
            .Select(name => (name, hits: filters.Count(terms => terms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))))
            .Where(x => x.hits != 1)
            .Select(x => $"{x.name}: selected by {x.hits} shard(s)")
            .ToList();

        Assert.True(problems.Count == 0,
            "Desktop tests the CI shard filters in .github/workflows/ci.yml do not select exactly once:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }
}
