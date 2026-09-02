using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Tempest.App.Workspace;
using Tempest.Core.Commands;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The handful of things every Desktop test needs and none of them should
/// own — `WP-F`.
/// </summary>
/// <remarks>
/// <para>
/// Each of these was copied between test classes rather than shared, and the
/// copying cost something real: <see cref="FindButton"/> was duplicated in a
/// simplified form that searched the whole Ribbon rather than the command's
/// own tab, and silently matched the wrong button because several
/// disciplines register commands with the same DisplayName. The version here
/// is the tab-scoped one, which is the correct one.
/// </para>
/// <para>
/// <b>Deliberately small, and deliberately test-only.</b> This is Desktop
/// test infrastructure, not an abstraction the product gained to make tests
/// convenient — it reaches for reflection and the logical tree precisely
/// because those are things a test may do and production may not. It mirrors
/// <c>Tempest.Core.Tests.Templates.RepositoryPaths</c>, which already plays
/// this role in the Core suite. Nothing is generalised beyond the call sites
/// that exist.
/// </para>
/// </remarks>
internal static class DesktopTestHelpers
{
    /// <summary>
    /// The repository root, found by walking up from the test assembly to
    /// <c>global.json</c> — the same marker <c>Directory.Build.props</c>
    /// relies on, so a source-reading test needs no hand-maintained relative
    /// path from its output directory.
    /// </summary>
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

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

    /// <summary>
    /// Reads a private field — for the shell's own collaborators, which
    /// <c>MainWindow</c> holds privately and exposes no seam for.
    /// </summary>
    /// <remarks>
    /// Retained rather than replaced by a production accessor: the private
    /// boundary is itself the architectural rule (`ADR-0103` — a collaborator
    /// is constructed by the composition root and never handed out), so
    /// widening it to suit a test would change the thing under test.
    /// </remarks>
    public static T GetPrivateField<T>(object instance, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {instance.GetType().Name}.");

        return (T)field.GetValue(instance)!;
    }

    /// <summary>
    /// The Ribbon button for <paramref name="commandId"/>, found inside that
    /// command's own discipline tab.
    /// </summary>
    /// <remarks>
    /// <b>Tab-scoped, and that is load-bearing.</b> Several disciplines
    /// register commands sharing a DisplayName ("Request Review" exists in
    /// three), so a Ribbon-wide search returns whichever the tree yields
    /// first — a test that passes while clicking the wrong button. Scoped by
    /// <see cref="CommandDescriptor.Category"/>, which is what builds the
    /// tabs.
    /// </remarks>
    public static Button FindButton(RibbonView ribbon, ICommandRegistry registry, string commandId)
    {
        ArgumentNullException.ThrowIfNull(ribbon);
        ArgumentNullException.ThrowIfNull(registry);

        var descriptor = registry.Items.Single(d => d.Id == commandId);
        var tab = ((TabControl)ribbon.Content!).Items.OfType<TabItem>().Single(t => Equals(t.Tag, descriptor.Category));

        return ((Control)tab.Content!).GetLogicalDescendants()
            .OfType<Button>()
            .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == descriptor.DisplayName));
    }

    /// <summary>Clicks the Ribbon button for <paramref name="commandId"/>, exactly as a user does.</summary>
    public static void Click(RibbonView ribbon, ICommandRegistry registry, string commandId) =>
        FindButton(ribbon, registry, commandId)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// Switches to <paramref name="areaId"/>, finds the first object of
    /// <paramref name="kind"/> anywhere in that area's tree, and selects it.
    /// </summary>
    public static async Task<ProjectExplorerNode> SelectFirstAsync(IWorkspace workspace, string areaId, string kind)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        await workspace.Navigation.SwitchAreaAsync(areaId);
        var node = await FindAsync(workspace.ProjectExplorer, await workspace.ProjectExplorer.GetRootNodesAsync(), kind);
        Assert.NotNull(node);
        await workspace.Selection.SelectAsync(node!.Id, node.Kind!);

        return node;
    }

    /// <summary>The first object node of <paramref name="kind"/> in <paramref name="nodes"/> or below it.</summary>
    public static async Task<ProjectExplorerNode?> FindAsync(
        IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes, string kind)
    {
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentNullException.ThrowIfNull(nodes);

        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object && node.Kind == kind)
                return node;

            if (node.HasChildren && await FindAsync(explorer, await explorer.GetChildrenAsync(node.Id), kind) is { } found)
                return found;
        }

        return null;
    }
}
