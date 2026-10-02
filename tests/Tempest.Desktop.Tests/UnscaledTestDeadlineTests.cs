using System.Text.RegularExpressions;

namespace Tempest.Desktop.Tests;

/// <summary>
/// No Desktop test computes its own wall-clock deadline (`v0.23.0` CI board
/// G-13). Every wait goes through <see cref="DesktopTestHelpers.Deadline"/>,
/// which scales by <c>TEMPEST_TEST_TIMEOUT_FACTOR</c> (CI sets 3); eighteen
/// <c>UtcNow.AddSeconds</c> deadlines had drifted back in and ignored it,
/// contrary to the test-determinism standard. A source-text scan on the
/// <see cref="NoBlockingPersistenceCallsTests"/> precedent: a deadline that
/// happens to be long enough today has no failing behaviour to observe.
/// </summary>
public sealed class UnscaledTestDeadlineTests
{
    private static readonly Regex UnscaledDeadline = new(@"\bDate(Time|TimeOffset)\.UtcNow\.AddSeconds\(", RegexOptions.Compiled);

    [Fact]
    public void NoDesktopTest_BuildsAnUnscaledUtcNowAddSecondsDeadline()
    {
        var root = Path.Combine(DesktopTestHelpers.RepositoryRoot, "tests", "Tempest.Desktop.Tests");
        var helper = Path.Combine(root, "DesktopTestHelpers.cs");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (string.Equals(file, helper, StringComparison.OrdinalIgnoreCase)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("///", StringComparison.Ordinal))
                    continue;
                if (UnscaledDeadline.IsMatch(lines[i]))
                    violations.Add($"{Path.GetRelativePath(root, file)}:{i + 1}: {trimmed}");
            }
        }

        Assert.True(violations.Count == 0,
            "Unscaled deadline(s) - use DesktopTestHelpers.Deadline(seconds) instead:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }
}
