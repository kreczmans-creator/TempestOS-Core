using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// WP-H (`AT-24`, audit finding `F-14`) — the five sanctioned
/// Duplicate → Copy direct handler invocations, pinned as the exception
/// they were declared to be.
/// </summary>
/// <remarks>
/// <para>
/// <b>The decision this protects.</b> Duplicate is Copy with the source's
/// own current parent, so each <c>Duplicate*CommandHandler</c> calls its
/// discipline's <c>Copy*CommandHandler.HandleAsync</c> directly rather than
/// dispatching. That was retained as a sanctioned exception by approved
/// decision, not because it is free: the inner Copy runs outside
/// <see cref="Tempest.Core.Commands.CommandHandlerTable"/>, so nothing the
/// shared table does — logging, trust checks, any future cross-cutting
/// concern — applies to it. Five sites is a bounded, reviewable cost; the
/// same shortcut copied into a sixth, seventh and eighth place is a second
/// dispatch path.
/// </para>
/// <para>
/// <b>The failure this catches, in both directions.</b> Proliferation: a new
/// discipline (or a new command in an existing one) copies the pattern, and
/// the exception silently stops being an exception. Disappearance: a site is
/// removed or refactored and <c>AT-24</c> is left claiming five, so the
/// register describes a codebase that no longer exists. Deliberately
/// refactoring the pattern away stays possible — it fails this test, which
/// is the prompt to retire <c>AT-24</c> rather than a wall.
/// </para>
/// <para>
/// <b>Why a behavioural test would not catch it.</b> Every one of these
/// handlers is already covered behaviourally, and a sixth would be too:
/// Duplicate produces a correct copy whether it dispatches or calls the
/// handler directly. The bypass is invisible from the outside — that is
/// precisely why it is a governed exception and not a bug — so the only
/// place it can be observed is where it is written.
/// </para>
/// </remarks>
public sealed class DuplicateCopyDelegationTests
{
    /// <summary>
    /// The five sanctioned sites, by the file each lives in. Set membership,
    /// not a count: the message a failure prints names which file appeared or
    /// vanished, and adding a discipline means adding a line here on purpose.
    /// </summary>
    private static readonly IReadOnlySet<string> SanctionedSites = new HashSet<string>(StringComparer.Ordinal)
    {
        "Calculations/DuplicateCalculationObjectCommand.cs",
        "Documents/DuplicateDocumentObjectCommand.cs",
        "Manufacturing/DuplicateManufacturingObjectCommand.cs",
        "Mechanical/DuplicateMechanicalObjectCommand.cs",
        "Verification/DuplicateVerificationActivityCommand.cs",
    };

    private static readonly string WorkspaceRoot =
        Path.Combine(RepositoryPaths.RepositoryRoot, "src", "Tempest.App", "Workspace");

    /// <summary>Every file under <c>Tempest.App/Workspace</c> whose code calls a Copy handler's <c>HandleAsync</c> directly.</summary>
    private static IReadOnlyList<string> DelegatingFiles()
    {
        var found = new List<string>();

        foreach (var file in Directory.EnumerateFiles(WorkspaceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var delegates = File.ReadAllLines(file)
                .Select(line => line.Trim())
                .Where(line => !line.StartsWith("//", StringComparison.Ordinal)
                               && !line.StartsWith("///", StringComparison.Ordinal))
                .Any(line => line.Contains("_copyHandler.HandleAsync(", StringComparison.Ordinal));

            if (delegates)
            {
                found.Add(Path.GetRelativePath(WorkspaceRoot, file).Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        return [.. found.OrderBy(path => path, StringComparer.Ordinal)];
    }

    [Fact]
    public void ExactlyTheSanctionedSites_InvokeACopyHandlerDirectly()
    {
        var actual = DelegatingFiles();

        Assert.Equal(SanctionedSites.OrderBy(path => path, StringComparer.Ordinal), actual);
    }

    /// <summary>
    /// Requirements is the sixth Duplicate command and does <i>not</i>
    /// delegate — it duplicates a Requirement itself. Pinned so the five
    /// above read as a bounded list rather than as "every Duplicate", and so
    /// nobody 'completes' the pattern by giving Requirements a Copy handler
    /// it has no reason to have.
    /// </summary>
    [Fact]
    public void RequirementsDuplicate_DoesNotDelegate_AndThatAsymmetryIsDeliberate()
    {
        var requirements = Path.Combine(WorkspaceRoot, "Requirements", "DuplicateRequirementCommand.cs");

        Assert.True(File.Exists(requirements), "DuplicateRequirementCommand.cs must exist.");
        Assert.DoesNotContain("Requirements/DuplicateRequirementCommand.cs", DelegatingFiles());
    }
}
