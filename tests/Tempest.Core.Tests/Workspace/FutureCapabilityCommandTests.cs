using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// WP-H (`TD-115`, audit finding `F-07`) — the three commands that are
/// fully implemented and registered, and that nothing in the product can
/// construct, pending <c>FCR-0073</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The decision this protects.</b> Each of these needs a second object
/// the user must choose, and this platform has no object picker
/// (<c>FCR-0073</c>). Rather than delete them or fake a surface, they were
/// kept implemented, kept registered with a working handler, and given a
/// <c>CommandBinding.Unavailable</c> reason that says what is missing
/// (<c>ADR-0070</c>). The approved disposition of <c>TD-115</c> is that they
/// are future capability, not dead code.
/// </para>
/// <para>
/// <b>The failure this catches.</b> Two, opposite. A cleanup pass reads
/// "registered but unreachable" as "dead" and deletes the command, the
/// handler, or both — losing implemented, tested behaviour that a later Work
/// Package is meant to surface. Or a construction path appears without
/// <c>FCR-0073</c>: a hand-built <c>new …Command(…)</c> somewhere in a
/// surface, guessing at the second object instead of asking for it, which is
/// exactly the shortcut the unavailable reason exists to prevent.
/// </para>
/// <para>
/// <b>Why a behavioural test would not catch it.</b> The behaviour is
/// already covered and stays green either way:
/// <c>RequirementsWorkspaceIntegrationTests</c> and
/// <c>MechanicalWorkspaceIntegrationTests</c> dispatch all three through the
/// real <see cref="Tempest.Core.Commands.ICommandDispatcher"/> and prove they
/// work, and those tests would still pass with a production construction
/// path bolted on — while deleting a command breaks compilation of the test
/// rather than reporting the architectural fact. What is unenforced is the
/// absence: that no production code builds one. An absence has to be
/// asserted against the source, because there is no execution in which it
/// can be observed.
/// </para>
/// <para>
/// <b>This does not block <c>FCR-0073</c>.</b> When the picker lands and
/// these gain real bindings, this test fails — and that failure is the
/// prompt to retire the <c>TD-115</c> row, not an obstacle to the work.
/// </para>
/// </remarks>
public sealed class FutureCapabilityCommandTests
{
    /// <summary>
    /// The three commands, and the descriptor Id each is registered under.
    /// Written as a set so that a fourth joining them, or one leaving, is a
    /// deliberate edit here.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> PendingObjectPicker =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LinkRequirementCommand"] = "requirements.link",
            ["AddRequirementToCollectionCommand"] = "requirements.add-to-collection",
            ["CompareBaselinesCommand"] = "mechanical.compare-baselines",
        };

    private static IEnumerable<(string RelativePath, string[] Lines)> ProductionSources()
    {
        var root = Path.Combine(RepositoryPaths.RepositoryRoot, "src");

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file)
                .Select(line => line.Trim())
                .Where(line => !line.StartsWith("//", StringComparison.Ordinal)
                               && !line.StartsWith("///", StringComparison.Ordinal)
                               && !line.StartsWith('*'))
                .ToArray();

            yield return (
                Path.GetRelativePath(RepositoryPaths.RepositoryRoot, file).Replace(Path.DirectorySeparatorChar, '/'),
                lines);
        }
    }

    [Fact]
    public void NoProductionCode_ConstructsACommandThatIsWaitingOnTheObjectPicker()
    {
        var constructions = new List<string>();

        foreach (var (relativePath, lines) in ProductionSources())
        {
            foreach (var line in lines)
            {
                foreach (var command in PendingObjectPicker.Keys)
                {
                    if (line.Contains($"new {command}(", StringComparison.Ordinal))
                        constructions.Add($"{relativePath}: {line}");
                }
            }
        }

        Assert.True(
            constructions.Count == 0,
            "A command that declares itself unavailable pending FCR-0073 is being constructed in production code.\n"
            + "Its binding says a second object must be chosen and that no picker exists; building one anyway\n"
            + "means guessing at that object. If FCR-0073 has landed, give the command a real binding and\n"
            + "retire its TD-115 entry rather than adding a bespoke construction site.\n\n"
            + string.Join("\n", constructions));
    }

    [Fact]
    public void EachCommandAndItsHandler_StillExist_RatherThanHavingBeenSweptUpAsDeadCode()
    {
        // Asserted from the source rather than from typeof(...): a compile-time
        // reference would make deletion a build error in this file, which
        // reports the wrong thing — the point is that the type is still there,
        // named, with a handler beside it.
        var declarations = ProductionSources()
            .SelectMany(source => source.Lines)
            .ToList();

        foreach (var command in PendingObjectPicker.Keys)
        {
            Assert.Contains(declarations, line => line.Contains($"class {command} ", StringComparison.Ordinal)
                                                  || line.Contains($"class {command}:", StringComparison.Ordinal));

            Assert.Contains(declarations, line => line.Contains($"class {command}Handler", StringComparison.Ordinal));

            // And the handler is registered, not merely written.
            Assert.Contains(declarations, line => line.Contains($"RegisterHandler<{command}>", StringComparison.Ordinal));
        }
    }
}
