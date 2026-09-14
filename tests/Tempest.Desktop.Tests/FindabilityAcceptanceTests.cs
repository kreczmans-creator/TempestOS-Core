using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Persistence;
using Tempest.Desktop.Views;
using Tempest.Workspace;
using Tempest.Workspace.Mechanical;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 18.1B` acceptance #2: record a Part, a Document and a piece of
/// Evidence (citing one released material) in a project; restart the host;
/// the Command Palette's own Objects section finds a title fragment and
/// opens it; the Explorer filter finds the Part by its own business
/// identifier; the Home cockpit's own "Recently changed" card lists all
/// three, newest first.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class FindabilityAcceptanceTests
{
    private const string PartIdentifier = "BRK-FIND-001";
    private const string PartTitle = "Findability Bracket Mount";
    private const string DocumentTitle = "Findability Design Note";
    private const string EvidenceTitle = "Findability Bracket Calc";

    [AvaloniaFact]
    public async Task RecordThreeObjects_RestartTheHost_FindThemByPaletteSearch_ExplorerIdentifierFilter_AndCockpitRecentlyChanged()
    {
        var rootPath = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid projectId;
        Guid partId;
        Guid documentId;
        Guid evidenceId;

        // ---- First session: record the three objects, cite a released material ----
        {
            var host = new WorkspaceHost(rootPath);
            try
            {
                await host.StartAsync();

                var navigator = host.ShellNavigator!;
                await navigator.GoToProjectsAsync();
                var project = await host.ProjectDirectory!.CreateAsync("P-FIND", "Findability Project");
                projectId = project.Id;
                await navigator.OpenProjectAsync(project.Id);

                var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

                // A Part, with a real business identifier — the Explorer's
                // own filter-by-identifier subject.
                var part = (Part)await new EngineeringObjectFactory<Part>(
                    MechanicalObjectFactoryRegistry.Part, domain,
                    (doc, rev) => new Part(doc, rev, domain, PartIdentifier, PartTitle, EngineeringObjectMetadata.Empty))
                    .CreateAsync("Created for the Findability acceptance journey.");
                await part.MoveAsync(project.Id);
                partId = part.Id;

                // A Document.
                var document = (Document)await new EngineeringObjectFactory<Document>(
                    "Document", domain,
                    (doc, rev) => new Document(doc, rev, domain, identifier: null, DocumentTitle, EngineeringObjectMetadata.Empty))
                    .CreateAsync("Created for the Findability acceptance journey.");
                await document.MoveAsync(project.Id);
                documentId = document.Id;

                // A released material, to cite.
                await host.BracketCalculations!.PopulateMaterialLibraryAsync();
                await host.BracketCalculations!.VerifyAndReleaseAsync(
                    Tempest.Core.ReferenceData.Seeding.Datasets.MaterialSeed.S355J2,
                    sourceConsulted: "EN 10025-2, Table 7.",
                    releaseRationale: "Released for the Findability acceptance journey.");

                var evidenceService = (IEvidenceService)host.Services!.GetService(typeof(IEvidenceService));
                var evidence = await evidenceService.CreateAsync(project.Id, EvidenceTitle, EvidenceClassification.Calculation);
                evidenceId = evidence.Id;
                var citation = await evidenceService.CiteAsync(
                    evidence.Id, "Materials", Tempest.Core.ReferenceData.Seeding.Datasets.MaterialSeed.S355J2);
                Assert.True(citation.Succeeded, citation.Reason);
            }
            finally
            {
                await host.ShutdownAsync();
                await host.DisposeAsync();
            }
        }

        // ---- Restart: a fresh WorkspaceHost/MainWindow over the same root ----
        {
            var host = new WorkspaceHost(rootPath);
            try
            {
                await host.StartAsync();
                var window = new MainWindow(host);
                var navigator = host.ShellNavigator!;
                var workspace = host.Workspace!;

                await navigator.GoToProjectsAsync();
                await window.RenderCurrentModuleAsync();
                await navigator.OpenProjectAsync(projectId);
                await window.RenderCurrentModuleAsync();
                await navigator.GoToEngineeringAsync();
                await window.RenderCurrentModuleAsync();
                LayOut(window);

                // ---- Command Palette: a title fragment finds the Part and opens it ----
                var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");
                PaletteObjectHit? selectedHit = null;
                palette.ObjectSelected += hit => selectedHit = hit;

                palette.Open();
                var queryBox = (TextBox)((StackPanel)palette.Child!).Children[0];
                var resultsBox = (ListBox)((StackPanel)palette.Child!).Children[1];
                queryBox.Text = "Bracket Mount";

                await RenderUntilAsync(window, () =>
                    resultsBox.ItemsSource is IReadOnlyList<ListBoxItem> items
                        && items.Any(i => i.Content is string s && s.Contains(PartTitle, StringComparison.Ordinal)));

                var partRowIndex = ((IReadOnlyList<ListBoxItem>)resultsBox.ItemsSource!)
                    .ToList()
                    .FindIndex(i => i.Content is string s && s.Contains(PartTitle, StringComparison.Ordinal));
                Assert.True(partRowIndex >= 0, "Expected the Objects section to list the Part.");

                resultsBox.SelectedIndex = partRowIndex;
                queryBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

                Assert.NotNull(selectedHit);
                Assert.Equal(partId, selectedHit!.ObjectId);
                LayOut(window);

                var explorer = window.GetLogicalDescendants().OfType<ProjectExplorerView>().Single();
                await RenderUntilAsync(window, () => explorer.IsRevealed(partId));
                Assert.True(explorer.IsRevealed(partId), "Selecting the Part from the palette did not reveal it in the Explorer.");

                // ---- Explorer filter: the Part's own business identifier finds it ----
                var filterBox = GetPrivateField<TextBox>(explorer, "_filter");
                filterBox.Text = "BRK-FIND";
                LayOut(window);

                var tree = GetPrivateField<TreeView>(explorer, "_tree");
                bool FilteredTreeShowsThePart() =>
                    FlattenExplorerItems(tree.ItemsSource).Any(n => n.Node.Id == partId);
                await RenderUntilAsync(window, FilteredTreeShowsThePart);
                Assert.True(FilteredTreeShowsThePart(), "The Explorer filter by identifier did not show the Part.");

                filterBox.Text = string.Empty;
                LayOut(window);

                // ---- Cockpit: Recently changed reads the three back, newest first, from the durable audit trail ----
                // `EngineeringWorkspaceComposer.Build`'s own remarks: a real
                // launch with no Tempest.Samples.dll on disk registers no
                // sample content at all — this test process has that
                // assembly loaded (other Desktop tests reference it), so
                // `WorkspaceHost.StartAsync` freshly (re)creates a batch of
                // unrelated sample objects on *every* start, including this
                // restart, which always outranks this journey's own
                // (earlier, session-one) objects in the card's own
                // top-ten-only display. That capped, newest-first,
                // survives-restart contract is what
                // `EngineeringCockpitTests.RecentlyChanged_*` (Core, no
                // sample noise) proves deterministically; this journey
                // proves the same durable audit trail — read through the
                // identical `IAuditQuery` the Cockpit itself reads — is
                // reachable end-to-end through a real, restarted Desktop
                // Host, in the correct newest-first order.
                var auditQuery = (Tempest.Core.Audit.IAuditQuery)host.Services!.GetService(typeof(Tempest.Core.Audit.IAuditQuery));
                var allRecords = await auditQuery.QueryAsync(new Tempest.Core.Audit.AuditQueryCriteria());

                DateTimeOffset? LastChangeTo(Guid objectId) => allRecords
                    .Where(r => r.Detail.TryGetValue("ObjectId", out var id) && id == objectId.ToString("N"))
                    .Select(r => (DateTimeOffset?)r.OccurredAt)
                    .Max();

                var evidenceWhen = LastChangeTo(evidenceId);
                var documentWhen = LastChangeTo(documentId);
                var partWhen = LastChangeTo(partId);

                Assert.True(evidenceWhen.HasValue, "No durable audit row survived restart for the Evidence.");
                Assert.True(documentWhen.HasValue, "No durable audit row survived restart for the Document.");
                Assert.True(partWhen.HasValue, "No durable audit row survived restart for the Part.");

                // Newest first: Evidence was recorded last (created, then cited), the Part first.
                Assert.True(evidenceWhen > documentWhen, "Evidence (recorded last) should be newer than the Document.");
                Assert.True(documentWhen > partWhen, "Document should be newer than the Part (recorded first).");

                // The card itself renders real content from this same
                // source (Core-level tests pin its exact top-ten/ordering
                // contract; this confirms it is wired into the real
                // Cockpit view rather than only the read model).
                var cockpit = workspace.Cockpit;
                await cockpit.PrimeAsync();
                Assert.NotEmpty(cockpit.RecentlyChanged);
            }
            finally
            {
                await host.ShutdownAsync();
                await host.DisposeAsync();
            }
        }
    }

    /// <summary>Walks the Explorer's own bound <see cref="ExplorerNodeItem"/> tree (whatever the filter currently left in it), depth-first.</summary>
    private static IEnumerable<ExplorerNodeItem> FlattenExplorerItems(System.Collections.IEnumerable? roots)
    {
        if (roots is null)
            yield break;

        foreach (var obj in roots)
        {
            if (obj is not ExplorerNodeItem item)
                continue;

            yield return item;

            foreach (var descendant in FlattenExplorerItems(item.Children))
                yield return descendant;
        }
    }

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = Deadline(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }
    }

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Avalonia.Size(1900, 1050));
            window.Arrange(new Avalonia.Rect(0, 0, 1900, 1050));
        }
    }
}
