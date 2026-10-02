using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Requirements;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 20.10F` acceptance (Product Owner finding D8), through the real
/// window: a person is added and released under Business → Staff (moved
/// there from Engineering → Reference data by Product Owner runbook B1,
/// 2026-10-01 — people are business reference data) with one Add &amp;
/// Release, stays revision 1 through that release (runbook B2), and is
/// then picked as a requirement's own Owner from the drop-down that
/// replaces free-typing it, surviving a relaunch on the same persistence
/// root.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class PersonLibraryJourneyTests
{
    private const string PersonDisplayName = "WP 20.10F Journey Person";
    private const string PersonRole = "Structural Engineer";
    private const string PersonLabel = $"{PersonDisplayName} ({PersonRole})";

    [AvaloniaFact]
    public async Task AddingAPerson_ReleasingThem_PickingThemAsARequirementOwner_SurvivesARelaunch()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid requirementId;
        Guid projectId;

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();

            var window = new MainWindow(first, new StubFilePicker());
            LayOut(window);

            // A Requirement opens in its own project's Structure tab
            // (`PHYSICAL_REVIEW.md` §7c D8) — a project must be open first,
            // the same ordering `CreatedObjectOpensRightUpTests`'s own
            // identical Requirement-opens-right-up journey already
            // establishes.
            await first.ShellNavigator!.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await first.ProjectDirectory!.CreateAsync("P-WP2010F", "WP 20.10F Journey Project");
            projectId = project.Id;
            await first.ShellNavigator!.OpenProjectAsync(projectId);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            await first.ShellNavigator!.GoToModuleAsync(ShellArea.Business);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            // Business → Staff (runbook B1). People is no longer listed
            // under Engineering → Reference data.
            window.GetLogicalDescendants().OfType<BusinessAreaView>().Single().SelectNode("Staff");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<StaffView>().Any());
            LayOut(window);

            var staffView = window.GetLogicalDescendants().OfType<StaffView>().Single();

            // Add a person, through the real "Add a person" form.
            var textBoxes = staffView.GetLogicalDescendants().OfType<TextBox>().ToList();
            textBoxes.First(t => t.Watermark == "Display name").Text = PersonDisplayName;
            textBoxes.First(t => t.Watermark == "Role").Text = PersonRole;
            textBoxes.First(t => t.Watermark == "Email").Text = "journey.person@example.com";
            textBoxes.First(t => t.Watermark == "Phone").Text = "01234 567890";

            var addButton = staffView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Add & Release"));
            addButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // Add & Release adds, verifies and releases in one act, and opens
            // the new record right up (the Product Owner guard,
            // `po-comments.md` item 5) — released, and still revision 1:
            // a release is not a revision of the data (runbook B2).
            await RenderUntilAsync(window, () =>
                staffView.GetLogicalDescendants().OfType<ReferenceRecordView>().FirstOrDefault() is { } d
                && d.GetLogicalDescendants().OfType<TextBlock>().Any(t =>
                    (t.Text ?? string.Empty).Contains(PersonDisplayName, StringComparison.Ordinal)
                    && (t.Text ?? string.Empty).Contains("Released", StringComparison.Ordinal)));
            LayOut(window);

            var detail = staffView.GetLogicalDescendants().OfType<ReferenceRecordView>().First();
            Assert.Contains(
                detail.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains(PersonDisplayName, StringComparison.Ordinal) && (t.Text ?? string.Empty).Contains("rev 1  •  Released", StringComparison.Ordinal));

            // The list row is the person's own title and release status only.
            Assert.Contains(staffView.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == PersonLabel);
            Assert.NotNull(staffView.GetLogicalDescendants().OfType<Button>().SingleOrDefault(b => Avalonia.Automation.AutomationProperties.GetAutomationId(b) == "Open person-wp-20-10f-journey-person"));

            // Moved, not duplicated: Engineering → Reference data no longer
            // lists People.
            await first.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single().SelectNode("Reference data");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<LibrariesView>().Any());
            LayOut(window);
            var librariesView = window.GetLogicalDescendants().OfType<LibrariesView>().Single();
            Assert.DoesNotContain(librariesView.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? string.Empty).StartsWith("People (", StringComparison.Ordinal));
            Assert.DoesNotContain(librariesView.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == PersonLabel);

            // A real Requirement, opened right up — in its own project's
            // Structure tab (`PHYSICAL_REVIEW.md` §7c D8), so the project
            // is switched back onto it before opening (`ProjectArea.Engineering`
            // is that tab's own routing value; `CreatedObjectOpensRightUpTests`'s
            // own identical Requirement-opens-right-up journey establishes
            // the same ordering).
            await first.ShellNavigator!.OpenProjectAsync(projectId, Tempest.Workspace.Shell.ProjectArea.Engineering);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var requirementsService = (IRequirementsService)first.Services!.GetService(typeof(IRequirementsService));
            var requirement = await requirementsService.CreateAsync("REQ-WP2010F", "The consultancy's people are picked from a real list, not typed.");
            requirementId = requirement.Id;

            await window.OpenCreatedObjectAsync(requirementId, RequirementsService.RequirementDocumentKind);
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<ObjectEditorView>().Any());
            LayOut(window);

            var editor = EditorFor(window, "REQ-WP2010F");
            Assert.NotNull(editor);
            var requirementExpander = editor!.GetLogicalDescendants().OfType<Expander>().Single(e => Equals(e.Header, "Owner / Priority"));
            var ownerBox = FindByLabel<ComboBox>(requirementExpander, "Owner");

            ComboBoxItem? option = null;
            await RenderUntilAsync(window, () =>
            {
                option = (ownerBox.ItemsSource as IEnumerable<ComboBoxItem>)?.FirstOrDefault(i => Equals(i.Content, PersonLabel));
                return option is not null;
            });

            Assert.NotNull(option);
            ownerBox.SelectedItem = option;

            var saveButton = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save Owner/Priority"));
            saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // `TD-119`: the Save click runs an `async void` handler over
            // real disk I/O — bounded poll re-reading the real state, the
            // same remedy `ObjectEditorViewTests`'s own Save-path tests use.
            var reread = await requirementsService.FindAsync(requirementId);
            var deadline = Deadline(2);
            while ((reread is null || reread.Owner != PersonDisplayName) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
                reread = await requirementsService.FindAsync(requirementId);
            }

            Assert.Equal(PersonDisplayName, reread!.Owner);
            Assert.NotNull(reread.OwnerPersonId);

            await first.ShutdownAsync();
        }
        finally
        {
            await first.DisposeAsync();
        }

        // Relaunch — a fresh host and window over the same persistence
        // root: the owner shows without anything re-picked.
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();

            var window = new MainWindow(second, new StubFilePicker());
            LayOut(window);
            await second.ShellNavigator!.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            await second.ShellNavigator!.OpenProjectAsync(projectId, Tempest.Workspace.Shell.ProjectArea.Engineering);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            await window.OpenCreatedObjectAsync(requirementId, RequirementsService.RequirementDocumentKind);
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<ObjectEditorView>().Any());
            LayOut(window);

            var editor = EditorFor(window, "REQ-WP2010F");
            Assert.NotNull(editor);
            var requirementExpander = editor!.GetLogicalDescendants().OfType<Expander>().Single(e => Equals(e.Header, "Owner / Priority"));
            var ownerBox = FindByLabel<ComboBox>(requirementExpander, "Owner");

            await RenderUntilAsync(window, () => ownerBox.SelectedItem is ComboBoxItem { Content: string content } && content == PersonLabel);

            Assert.Equal(PersonLabel, (ownerBox.SelectedItem as ComboBoxItem)?.Content);

            await second.ShutdownAsync();
        }
        finally
        {
            await second.DisposeAsync();
        }
    }

    /// <summary>
    /// The real window can carry more than one <see cref="ObjectEditorView"/>
    /// at once (a tab this journey opened earlier, still docked) — the same
    /// disambiguation <c>CreatedObjectOpensRightUpTests</c>'s own identical
    /// helper already needs, matching on the Requirement's own identifier
    /// shown in its Identity section.
    /// </summary>
    private static ObjectEditorView? EditorFor(MainWindow window, string nameOrIdentifier) =>
        window.GetLogicalDescendants().OfType<ObjectEditorView>().Distinct()
            .FirstOrDefault(e => e.GetLogicalDescendants().OfType<TextBox>().Any(t => t.Text == nameOrIdentifier));

    private static T FindByLabel<T>(Control root, string label) where T : Control
    {
        var grid = root.GetLogicalDescendants().OfType<Grid>()
            .Single(g => g.Children.Count == 2 && g.Children[0] is TextBlock { Text: var text } && text == label);
        return (T)grid.Children[1];
    }

    private static Task RenderUntilAsync(MainWindow window, Func<bool> condition, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string? what = null) =>
        DesktopTestHelpers.WaitUntilAsync(condition, 20, () => LayOut(window), DesktopTestHelpers.OpenPhaseOf(window), what);

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
