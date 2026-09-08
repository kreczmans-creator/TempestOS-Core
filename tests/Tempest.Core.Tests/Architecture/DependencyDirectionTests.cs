using System.Xml.Linq;
using Tempest.Harness;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Templates;
using Tempest.Workspace;

namespace Tempest.Core.Tests.Architecture;

/// <summary>
/// WP-H — dependencies flow downward: <c>Desktop → Workspace → Core</c> and
/// <c>Harness → Workspace → Core</c>, and no presentation framework reaches
/// the two layers below the shell (<c>ADR-0023</c>, <c>ADR-0101</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The decision this protects.</b> <c>ADR-0023</c> makes the layering a
/// one-way graph, and <c>ADR-0092</c>/<c>ADR-0101</c> make <c>Tempest.Desktop</c>
/// one presentation layer, and <c>Tempest.Harness</c> a second, over a
/// Workspace layer that must remain capable of carrying either. The moment
/// <c>Tempest.Core</c> can see <c>Tempest.Workspace</c>, or
/// <c>Tempest.Workspace</c> can see Avalonia, that promise is gone — not
/// gradually, but at the first type that takes the shortcut.
/// `WP 17.2B` split the former <c>Tempest.App</c> (a class library carrying
/// both the shared Workspace domain layer and a console harness in one
/// assembly) into <c>Tempest.Workspace</c> (the library) and
/// <c>Tempest.Harness</c> (the console exe) — this test's own graph gained
/// a fourth project and a fifth invariant, not a new shape.
/// </para>
/// <para>
/// <b>The failure this catches.</b> Someone adds a
/// <c>&lt;ProjectReference&gt;</c> or a <c>&lt;PackageReference&gt;</c> to a
/// lower project to reach something convenient. Every subsequent shortcut
/// then compiles.
/// </para>
/// <para>
/// <b>Why a behavioural test would not catch it.</b> The compiler is the
/// enforcement today, and the compiler is enforcing the reference graph —
/// not the rule. Adding the reference is what removes the enforcement, and
/// it makes everything compile <i>more</i>, never less: there is no failing
/// behaviour to observe, and no runtime moment at which the layering is
/// consulted. The invariant is a property of the build graph, so it is
/// asserted against the build graph and against what the assemblies
/// actually carry — not by a dependency-analysis framework, and not by
/// scanning source text, which cannot tell a reference from a doc comment
/// naming a layer.
/// </para>
/// </remarks>
public sealed class DependencyDirectionTests
{
    private const string Core = "Tempest.Core";
    private const string Workspace = "Tempest.Workspace";
    private const string Harness = "Tempest.Harness";
    private const string Desktop = "Tempest.Desktop";

    private static readonly IReadOnlyDictionary<string, string> ProjectFiles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Core] = Path.Combine("src", "Tempest.Core", "Tempest.Core.csproj"),
            [Workspace] = Path.Combine("src", "Tempest.Workspace", "Tempest.Workspace.csproj"),
            [Harness] = Path.Combine("src", "Tempest.Harness", "Tempest.Harness.csproj"),
            [Desktop] = Path.Combine("src", "Tempest.Desktop", "Tempest.Desktop.csproj"),
        };

    private static XDocument Project(string name) =>
        XDocument.Load(Path.Combine(RepositoryPaths.RepositoryRoot, ProjectFiles[name]));

    /// <summary>The project names this project declares a <c>ProjectReference</c> to.</summary>
    private static IReadOnlyList<string> ReferencedProjects(string name) =>
    [
        .. Project(name).Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(
                reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar)))
            .OrderBy(project => project, StringComparer.Ordinal),
    ];

    /// <summary>The NuGet package ids this project declares.</summary>
    private static IReadOnlyList<string> ReferencedPackages(string name) =>
    [
        .. Project(name).Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")!.Value)
            .OrderBy(package => package, StringComparer.Ordinal),
    ];

    // ==================================================================
    // The declared graph
    // ==================================================================

    [Fact]
    public void Core_DependsOnNothingInThisRepository()
    {
        // Not "does not depend on Workspace/Harness/Desktop" — Core is the
        // bottom of the graph, so the honest assertion is that it depends
        // on no project at all. Anything added here is by definition
        // upward.
        Assert.Empty(ReferencedProjects(Core));
    }

    [Fact]
    public void Workspace_DependsOnCoreAlone()
    {
        Assert.Equal([Core], ReferencedProjects(Workspace));
    }

    [Fact]
    public void Harness_DependsOnWorkspace_AndReachesCoreThroughIt()
    {
        // Harness names Workspace only; Core arrives transitively, which is
        // what keeps the layering a chain rather than a fan.
        Assert.Equal([Workspace], ReferencedProjects(Harness));
    }

    [Fact]
    public void Desktop_DependsOnWorkspace_AndReachesCoreThroughIt()
    {
        // Desktop names Workspace only; Core arrives transitively, exactly
        // as it does for Harness — two independent presentation layers over
        // the identical shared domain layer, neither one referencing the
        // other.
        Assert.Equal([Workspace], ReferencedProjects(Desktop));
    }

    // ==================================================================
    // The presentation framework stays in the presentation layer
    // ==================================================================

    [Fact]
    public void NoAvaloniaPackage_ReachesCoreOrWorkspaceOrHarness()
    {
        Assert.DoesNotContain(ReferencedPackages(Core), package => package.StartsWith("Avalonia", StringComparison.Ordinal));
        Assert.DoesNotContain(ReferencedPackages(Workspace), package => package.StartsWith("Avalonia", StringComparison.Ordinal));
        Assert.DoesNotContain(ReferencedPackages(Harness), package => package.StartsWith("Avalonia", StringComparison.Ordinal));

        // Stated from the other side too, so this test cannot pass because
        // the shell quietly stopped using Avalonia at all.
        Assert.Contains(ReferencedPackages(Desktop), package => package.StartsWith("Avalonia", StringComparison.Ordinal));
    }

    // ==================================================================
    // What the compiled assemblies actually carry
    // ==================================================================

    /// <summary>
    /// The declared graph and the built graph are two different things: a
    /// package can arrive transitively, and a reference can be added by a
    /// props file rather than by the project. This reads what the loaded
    /// assemblies actually bind against.
    /// </summary>
    [Fact]
    public void TheBuiltAssemblies_CarryNoUpwardOrPresentationReference()
    {
        var core = typeof(ITempestHost).Assembly;
        var workspace = typeof(IWorkspace).Assembly;
        var harness = typeof(WorkspaceShell).Assembly;

        Assert.Equal(Core, core.GetName().Name);
        Assert.Equal(Workspace, workspace.GetName().Name);
        Assert.Equal(Harness, harness.GetName().Name);

        foreach (var forbidden in Forbidden(core, Workspace, Harness, Desktop, "Avalonia"))
            Assert.Fail(forbidden);

        foreach (var forbidden in Forbidden(workspace, Harness, Desktop, "Avalonia"))
            Assert.Fail(forbidden);

        foreach (var forbidden in Forbidden(harness, Desktop, "Avalonia"))
            Assert.Fail(forbidden);
    }

    private static IEnumerable<string> Forbidden(System.Reflection.Assembly assembly, params string[] prefixes) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .Select(name => $"{assembly.GetName().Name} references '{name}', which is above it or is a presentation framework.");
}
