using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.Evidence;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Views;
using Tempest.Workspace.Mechanical;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The first Windows run of <c>v0.18.0</c> reported "no parts or
/// assemblies" when retagging evidence, after an assembly and a part had
/// been created from the ribbon. The earlier journey skipped the choice
/// ("No subject"), so what the picker lists was never asserted. This test
/// creates both through the real <c>mechanical.create</c> command, opens
/// the picker from the Evidence editor's own button, and expects to see
/// and choose them.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SubjectPickerListsStructureTests
{
    [AvaloniaFact]
    public async Task ChangeSubject_ListsTheAssemblyAndPartCreatedFromTheShell_AndTagsTheChosenOne()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-SUBJ-1", "Subject Picker Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            await host.Workspace!.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            await host.Workspace.Selection.ClearAsync();

            var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            var assembly = await registry.InvokeAsync(
                "mechanical.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Assembly", ["displayName"] = "assy 1" }));
            Assert.True(assembly.Result!.Succeeded, assembly.Result.Message);

            await host.Workspace.Selection.ClearAsync();
            var part = await registry.InvokeAsync(
                "mechanical.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = "level 1" }));
            Assert.True(part.Result!.Succeeded, part.Result.Message);
            var partId = part.Result.SubjectId!.Value;

            var evidence = await host.EvidenceService!.CreateAsync(project.Id, "Bracket calculation", EvidenceClassification.Calculation);
            var editor = await OpenEvidenceEditorAsync(window, evidence.Id);

            var subjectPicker = GetPrivateField<SubjectPicker>(window, "_subjectPicker");
            var changeSubject = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Change Subject"));
            changeSubject.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => subjectPicker.IsVisible);

            var list = GetPrivateField<ListBox>(subjectPicker, "_list");
            var items = ((IEnumerable<ListBoxItem>)list.ItemsSource!).ToList();
            var labels = items.Select(i => i.Content?.ToString() ?? string.Empty).ToList();
            Assert.Contains("Assembly — assy 1", labels);
            Assert.Contains("Part — level 1", labels);

            list.SelectedItem = items.Single(i => Equals(i.Tag, partId));
            var choose = subjectPicker.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Choose"));
            choose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !subjectPicker.IsVisible);

            await RenderUntilAsync(window, () => evidence.SubjectId == partId);
            Assert.Equal(partId, evidence.SubjectId);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    private static async Task<ObjectEditorView> OpenEvidenceEditorAsync(MainWindow window, Guid id)
    {
        var openEvidenceRecord = GetPrivateMethod(window, "OpenEvidenceRecordAsync");
        await (Task)openEvidenceRecord.Invoke(window, [id, Core.Evidence.Evidence.CanonicalKind])!;
        LayOut(window);

        var editor = window.GetLogicalDescendants().OfType<ObjectEditorView>().FirstOrDefault();
        Assert.NotNull(editor);
        return editor!;
    }

    private static System.Reflection.MethodInfo GetPrivateMethod(object target, string name) =>
        target.GetType().GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(name);

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
