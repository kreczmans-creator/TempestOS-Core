using System.Xml.Linq;
using Tempest.App.Workspace;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.Architecture;

/// <summary>
/// WP-H — dependencies flow downward: <c>Desktop → App → Core</c>, and no
/// presentation framework reaches the two layers below the shell
/// (<c>ADR-0023</c>, <c>ADR-0101</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The decision this protects.</b> <c>ADR-0023</c> makes the layering a
/// one-way graph, and <c>ADR-0092</c>/<c>ADR-0101</c> make <c>Tempest.Desktop</c>
/// one presentation layer over an App layer that must remain capable of
/// carrying another. The moment <c>Tempest.Core</c> can see
/// <c>Tempest.App</c>, or <c>Tempest.App</c> can see Avalonia, that promise
/// is gone — not gradually, but at the first type that takes the shortcut.
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
    private const string App = "Tempest.App";
    private const string Desktop = "Tempest.Desktop";

    private static readonly IReadOnlyDictionary<string, string> ProjectFiles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Core] = Path.Combine("src", "Tempest.Core", "Tempest.Core.csproj"),
            [App] = Path.Combine("src", "Tempest.App", "Tempest.App.csproj"),
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
        // Not "does not depend on App/Desktop" — Core is the bottom of the
        // graph, so the honest assertion is that it depends on no project at
        // all. Anything added here is by definition upward.
        Assert.Empty(ReferencedProjects(Core));
    }

    [Fact]
    public void App_DependsOnCoreAlone()
    {
        Assert.Equal([Core], ReferencedProjects(App));
    }

    [Fact]
    public void Desktop_DependsOnApp_AndReachesCoreThroughIt()
    {
        // Desktop names App only; Core arrives transitively, which is what
        // keeps the layering a chain rather than a fan.
        Assert.Equal([App], ReferencedProjects(Desktop));
    }

    // ==================================================================
    // The presentation framework stays in the presentation layer
    // ==================================================================

    [Fact]
    public void NoAvaloniaPackage_ReachesCoreOrApp()
    {
        Assert.DoesNotContain(ReferencedPackages(Core), package => package.StartsWith("Avalonia", StringComparison.Ordinal));
        Assert.DoesNotContain(ReferencedPackages(App), package => package.StartsWith("Avalonia", StringComparison.Ordinal));

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
        var app = typeof(WorkspaceShell).Assembly;

        Assert.Equal(Core, core.GetName().Name);
        Assert.Equal(App, app.GetName().Name);

        foreach (var forbidden in Forbidden(core, App, Desktop, "Avalonia"))
            Assert.Fail(forbidden);

        foreach (var forbidden in Forbidden(app, Desktop, "Avalonia"))
            Assert.Fail(forbidden);
    }

    private static IEnumerable<string> Forbidden(System.Reflection.Assembly assembly, params string[] prefixes) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .Select(name => $"{assembly.GetName().Name} references '{name}', which is above it or is a presentation framework.");
}
