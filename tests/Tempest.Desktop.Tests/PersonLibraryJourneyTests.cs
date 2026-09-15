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
/// window: a person is added and released in the People library — the
/// ninth governed library under Engineering → Reference data, the
/// identical editor pattern (create, edit fields, Verify/Release) every
/// sibling library already has — and then picked as a requirement's own
/// Owner from the drop-down that replaces free-typing it, surviving a
/// relaunch on the same persistence root.
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

            await first.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            // Engineering → Reference data — the same tree node every
            // sibling library's own journey test reaches it through
            // (`LibrariesTabLoadsOnEntryTests`, `ReferenceRecordViewTests`).
            window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single().SelectNode("Reference data");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<LibrariesView>().Any());
            LayOut(window);

            var librariesView = window.GetLogicalDescendants().OfType<LibrariesView>().Single();

            // Add a person, through the real "Add a person" form.
            var textBoxes = librariesView.GetLogicalDescendants().OfType<TextBox>().ToList();
            textBoxes.First(t => t.Watermark == "Display name").Text = PersonDisplayName;
            textBoxes.First(t => t.Watermark == "Role").Text = PersonRole;
            textBoxes.First(t => t.Watermark == "Email").Text = "journey.person@example.com";

            var addButton = librariesView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Add Person"));
            addButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // Add opens the new record right up (the Product Owner guard,
            // `po-comments.md` item 5 — the identical discipline
            // `LibrariesTabLoadsOnEntryTests.AddingAMaterial_OpensItInTheDetailPaneRightUp`
            // already proves for Materials).
            await RenderUntilAsync(window, () =>
                librariesView.GetLogicalDescendants().OfType<ReferenceRecordView>().FirstOrDefault() is { } d
                && d.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains(PersonDisplayName, StringComparison.Ordinal)));
            LayOut(window);

            var detail = librariesView.GetLogicalDescendants().OfType<ReferenceRecordView>().First();

            // Verify, then Release — the same governed acts every other
            // library's own record already offers, unchanged.
            var verifyButton = detail.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Verify"));
            verifyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => detail.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("Checked", StringComparison.Ordinal)));
            LayOut(window);

            var releaseButton = detail.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Release"));
            releaseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => detail.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("Released", StringComparison.Ordinal)));
            LayOut(window);

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

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = Deadline(20);
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
