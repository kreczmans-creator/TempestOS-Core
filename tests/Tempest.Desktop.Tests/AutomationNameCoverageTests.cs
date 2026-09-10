using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Core.Evidence;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 19.2B` (`TD-65` residual): every interactive control in
/// <c>src/Tempest.Desktop</c> carries a real
/// <see cref="AutomationProperties.NameProperty"/> — walked structurally,
/// over the live window's own logical tree, across every rail surface and
/// every project tab, rather than the targeted, historical, per-finding
/// spot checks <see cref="AccessibilityAutomationTests"/> already holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a structural walk, not another spot check.</b> A spot check
/// proves the specific controls a past review actually looked at are
/// named; it says nothing about a control nobody happened to look at. This
/// test instead enumerates every <see cref="Button"/>, <see cref="TextBox"/>,
/// <see cref="ComboBox"/>, <see cref="CheckBox"/>, <see cref="ListBox"/>,
/// <see cref="TabItem"/> and <see cref="GridSplitter"/> the real,
/// running window actually renders — the same seven types a screen-reader
/// user's own assistive technology treats as interactive — across the
/// whole rail and every project tab, and fails naming every one that
/// carries no accessible name, rather than trusting that the historical
/// list of fixes was exhaustive.
/// </para>
/// <para>
/// <b>Real content, not empty states.</b> Some controls (a document row's
/// own Open button, a relationship row's own Open button) only exist once
/// there is something to show — a project, an evidence record, an
/// engineering object — so the walk seeds a small amount of real data
/// first, the same "give every area something real to lay out" discipline
/// <see cref="Layout.LayoutWalkTests"/> already established.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class AutomationNameCoverageTests
{
    private static readonly Type[] NamedControlTypes =
    [
        typeof(Button), typeof(TextBox), typeof(ComboBox), typeof(CheckBox), typeof(ListBox), typeof(TabItem), typeof(GridSplitter),
    ];

    [AvaloniaFact]
    public async Task EveryInteractiveControl_OnEveryRailSurfaceAndProjectTab_HasAnAutomationName()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            var navigator = host.ShellNavigator!;

            // One project, one part, one issued piece of evidence — real
            // content for the areas that only draw interactive rows (an
            // Open button, a relationship link) once something exists.
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-AUTOMATION", "Automation Coverage");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();

            await host.Workspace!.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            await host.Workspace.Selection.ClearAsync();
            await host.EvidenceService!.CreateAsync(project.Id, "Automation Coverage Evidence", EvidenceClassification.Calculation);

            // Keyed by control instance, not area: the twelve dialog
            // overlays are permanent children of the root `Grid` (hidden,
            // never removed, per `MainWindowComposer.Layout`) and so are
            // walked again at every single area visited below — recording
            // the same offending instance under whichever area happened
            // to be current the first time it was seen would multiply one
            // real defect into a dozen misleading findings.
            var missing = new Dictionary<Control, string>(ReferenceEqualityComparer.Instance);

            void Scan(string area)
            {
                foreach (var control in window.GetLogicalDescendants().OfType<Control>())
                {
                    if (!NamedControlTypes.Contains(control.GetType()))
                        continue;

                    if (!string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)))
                        continue;

                    missing.TryAdd(control, $"{area}: {Describe(control)}");
                }
            }

            foreach (var module in ShellAreas.RailModules)
            {
                await navigator.GoToModuleAsync(module.Area);
                await window.RenderCurrentModuleAsync();
                Scan($"rail · {module.Title}");
            }

            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            foreach (var descriptor in ProjectAreas.All)
            {
                await navigator.OpenProjectAsync(project.Id, descriptor.Area);
                await window.RenderCurrentModuleAsync();
                Scan($"project tab · {descriptor.Title}");
            }

            Assert.True(
                missing.Count == 0,
                $"{missing.Count} distinct control(s) with no AutomationProperties.Name:{Environment.NewLine}{string.Join(Environment.NewLine, missing.Values.Order(StringComparer.Ordinal))}");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>A best-effort description of a control that, by definition, carries no name of its own to identify it by.</summary>
    private static string Describe(Control control)
    {
        var content = control switch
        {
            ContentControl { Content: string text } => text,
            ContentControl { Content: TextBlock { Text: { } text } } => text,
            HeaderedContentControl { Header: string header } => header,
            TextBox textBox => textBox.Watermark ?? textBox.Text,
            _ => null,
        };

        return content is { Length: > 0 }
            ? $"{control.GetType().Name} \"{content}\""
            : $"{control.GetType().Name} (no content to identify it by either)";
    }
}
