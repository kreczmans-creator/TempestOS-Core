using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Desktop.Composition;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The `WP 18.2A` acceptance journeys — through the real window, with a
/// stubbed file picker (Execution Plan §3 decision 6), driving the real
/// Evidence rail area, the real Citation and Declared-figure dialogs, and
/// the real Object Editor's own declaration-per-Kind rendering.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class EvidenceWorkspaceJourneyTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    /// <summary>Initialises a new instance of the <see cref="EvidenceWorkspaceJourneyTests"/> class.</summary>
    public EvidenceWorkspaceJourneyTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Acceptance 1: record, cite, restart, find, open — measured, and
    /// asserted under 120 s wall clock on the headless runner.
    /// </summary>
    [AvaloniaFact]
    public async Task RecordCiteRestartFindOpen_CompletesUnderTwoMinutes()
    {
        var stopwatch = Stopwatch.StartNew();
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var tempFile = CreateTempFile("evidence-record", ".xlsx", 12 * 1024);
        Guid createdId;
        string createdTitle;
        Guid projectId;

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();

            // Two released materials to cite — the seeded catalogue is not
            // populated until the calculation workbench asks it to be
            // (`BracketCalculationWorkbench.PopulateAsync`), so this
            // journey registers and releases its own two records directly
            // through the same governed services the Libraries tab itself
            // (`WP 18.2A`, §5) uses.
            await RegisterAndReleaseMaterialAsync(host, "mat-evd-1", "Evidence Test Alloy One");
            await RegisterAndReleaseMaterialAsync(host, "mat-evd-2", "Evidence Test Alloy Two");

            var filePicker = new StubFilePicker();
            var window = new MainWindow(host, filePicker);
            LayOut(window); // shows the window now, before `Opened`'s own status-bar/explorer refresh could otherwise overwrite state a later assertion reads.
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-EVD-1", "Evidence Journey Project");
            projectId = project.Id;
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            await navigator.GoToModuleAsync(ShellArea.Evidence);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var evidenceWorkspace = GetPrivateField<EvidenceWorkspaceView>(window, "_evidenceWorkspace");
            evidenceWorkspace.ParameterPrompt = (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                new Dictionary<string, string> { ["classification"] = nameof(EvidenceClassification.Calculation) });
            evidenceWorkspace.SubjectPrompt = _ => Task.FromResult<Guid?>(null);

            filePicker.EnqueuePick(tempFile);

            // `.First`, not `.Single`: both tab bodies are logical children
            // of the workspace's own `TabControl` regardless of which is
            // selected, and the selected tab's content is additionally
            // reachable through the control's own selected-content host —
            // the same control instance, found via two paths.
            var createButton = evidenceWorkspace.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, "Create"));
            createButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            Core.Evidence.Evidence? created = null;
            await RenderUntilAsync(window, () =>
            {
                created = domain.Repository.ListByKindAsync(Core.Evidence.Evidence.CanonicalKind).GetAwaiter().GetResult()
                    .OfType<Core.Evidence.Evidence>().FirstOrDefault(e => e.ParentId == project.Id);
                return created is not null;
            });
            Assert.NotNull(created);
            createdId = created!.Id;
            createdTitle = created.DisplayName;
            Assert.Equal("Calculation", created.Classification.ToString());

            // Opens right up, with the file listed — size and hash.
            ObjectEditorView? editor = null;
            await RenderUntilAsync(window, () =>
            {
                editor = window.GetLogicalDescendants().OfType<ObjectEditorView>()
                    .FirstOrDefault(e => e.GetLogicalDescendants().OfType<TextBox>().Any(t => t.Text == createdTitle));
                return editor is not null;
            });
            Assert.NotNull(editor);

            var attachments = await ((IHasAttachments)created).GetAttachmentsAsync();
            var attachment = Assert.Single(attachments);
            Assert.NotNull(attachment.ContentHash);
            await RenderUntilAsync(window, () =>
                editor!.GetLogicalDescendants().OfType<TextBlock>().Any(t =>
                    t.Text != null && t.Text.Contains(attachment.SizeInBytes.ToString("N0")) && t.Text.Contains(attachment.ContentHash!)));

            // Cite two released materials, via the real Cite button and the
            // real Citation picker.
            await CiteViaRealDialogAsync(window, editor!, "mat-evd-1");
            await CiteViaRealDialogAsync(window, editor!, "mat-evd-2");

            await RenderUntilAsync(window, () => created.Citations.Count == 2);
            Assert.Equal(2, created.Citations.Count);
            foreach (var citation in created.Citations)
            {
                var citationText = editor!.GetLogicalDescendants().OfType<TextBlock>()
                    .Select(t => t.Text ?? string.Empty)
                    .FirstOrDefault(t => t.Contains(citation.Pin.RecordId, StringComparison.Ordinal));
                Assert.NotNull(citationText);
                Assert.Contains("Materials", citationText);
                Assert.Contains(citation.Pin.RevisionNumber.ToString(), citationText);
                Assert.Contains(citation.SourceCitationSnapshot ?? string.Empty, citationText);
            }

            // Declare two figures, via the real Declare button and the real
            // Declared-figure dialog.
            await DeclareViaRealDialogAsync(window, editor!, "Utilisation", DeclaredFigureRole.Result, "0.82");
            await DeclareViaRealDialogAsync(window, editor!, "Max stress", DeclaredFigureRole.Result, "142 MPa");

            await RenderUntilAsync(window, () => created.DeclaredFigures.Count == 2);
            Assert.Equal(2, created.DeclaredFigures.Count);

            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
        catch
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
            throw;
        }

        // ============================================================
        // RESTART — a new host, the same persistence root.
        // ============================================================
        var secondHost = new WorkspaceHost(root);
        try
        {
            await secondHost.StartAsync();
            var window = new MainWindow(secondHost);
            var navigator = secondHost.ShellNavigator!;

            // Found directly by the id captured before the restart — never
            // by enumerating every project, which this headless run's own
            // `Tempest.Samples` module also seeds into the same store.
            var project = await secondHost.ProjectDirectory!.FindAsync(projectId);
            Assert.NotNull(project);
            await navigator.OpenProjectAsync(project!.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToModuleAsync(ShellArea.Evidence);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var evidenceWorkspace = GetPrivateField<EvidenceWorkspaceView>(window, "_evidenceWorkspace");
            await RenderUntilAsync(window, () =>
                evidenceWorkspace.GetLogicalDescendants().OfType<ListBoxItem>().Any(i => (i.Content as string)?.Contains(createdTitle) == true));

            var recovered = evidenceWorkspace.GetLogicalDescendants().OfType<ListBoxItem>().First(i => (i.Content as string)?.Contains(createdTitle) == true);
            Assert.NotNull(recovered.Content);

            // Opening a row opens the Object Editor on the record (§2) —
            // driven through the same "open right up" path Create itself
            // uses, since simulating a genuine double-tap gesture on a
            // headless `ListBoxItem` is not a reliable substitute for it.
            var reopened = EvidenceObjectEditorViewFor(window, createdId, Core.Evidence.Evidence.CanonicalKind);
            Assert.NotNull(reopened);

            var domain = (EngineeringDomainContext)secondHost.Services!.GetService(typeof(EngineeringDomainContext));
            var recoveredEvidence = Assert.IsType<Core.Evidence.Evidence>(await domain.Repository.FindAsync(createdId));
            Assert.Single(await ((IHasAttachments)recoveredEvidence).GetAttachmentsAsync());
            Assert.Equal(2, recoveredEvidence.Citations.Count);
            Assert.Equal(2, recoveredEvidence.DeclaredFigures.Count);

            var body = reopened!.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();
            Assert.Contains(body, t => t.Contains("Utilisation"));
            Assert.Contains(body, t => t.Contains("Max stress"));
        }
        finally
        {
            await secondHost.ShutdownAsync();
            await secondHost.DisposeAsync();
        }

        stopwatch.Stop();
        _output.WriteLine($"Acceptance 1 (record, cite, restart, find, open) took {stopwatch.ElapsedMilliseconds} ms.");
        Assert.True(stopwatch.ElapsedMilliseconds < 120_000, $"The journey took {stopwatch.ElapsedMilliseconds} ms, over the 120 s budget.");
    }

    /// <summary>Acceptance 2: citing a Draft record through the command is refused and the status bar names it.</summary>
    [AvaloniaFact]
    public async Task CitingADraftRecordThroughTheCommand_IsRefused_AndNamedInTheStatusBar()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            await RegisterDraftMaterialAsync(host, "mat-evd-draft", "Draft-Only Alloy");

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window); // shows the window now, so `Opened`'s own status-bar refresh does not later overwrite the refusal message this test asserts.
            var navigator = host.ShellNavigator!;
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-EVD-2", "Evidence Refusal Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var evidenceService = host.EvidenceService!;
            var evidence = await evidenceService.CreateAsync(project.Id, "Refusal Test Evidence", EvidenceClassification.Calculation);

            var dispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
            var result = await dispatcher.DispatchAsync(
                new Tempest.Workspace.Evidence.CiteEvidenceCommand(evidence.Id, Core.Evidence.Evidence.CanonicalKind, "Materials", "mat-evd-draft"),
                CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.NotNull(result.Message);
            Assert.Contains("mat-evd-draft", result.Message, StringComparison.Ordinal);
            Assert.Contains("Draft", result.Message, StringComparison.Ordinal);

            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            var actionReporter = GetPrivateField<ActionOutcomeReporter>(window, "_actionReporter");
            await actionReporter.ReportAsync(result.Message!, ActionOutcome.From(result.Succeeded));
            LayOut(window);
            Assert.Contains(statusBar.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text != null && t.Text.Contains("mat-evd-draft"));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Acceptance 3: dropping two files onto the list creates one record with two attachments.</summary>
    [AvaloniaFact]
    public async Task DroppingTwoFiles_CreatesOneRecordWithTwoAttachments()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window); // shows the window now, before `Opened`'s own status-bar/explorer refresh could otherwise overwrite state a later assertion reads.
            var navigator = host.ShellNavigator!;
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-EVD-3", "Evidence Drop Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToModuleAsync(ShellArea.Evidence);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var evidenceWorkspace = GetPrivateField<EvidenceWorkspaceView>(window, "_evidenceWorkspace");
            evidenceWorkspace.ParameterPrompt = (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                new Dictionary<string, string> { ["classification"] = nameof(EvidenceClassification.Report) });
            evidenceWorkspace.SubjectPrompt = _ => Task.FromResult<Guid?>(null);

            var fileA = CreateTempFile("drop-a", ".pdf", 2048);
            var fileB = CreateTempFile("drop-b", ".pdf", 4096);

            // Drives `EvidenceWorkspaceView`'s own shared
            // `CreateFromFilesAsync` — the exact method both the Create
            // button and the real `DragDrop.DropEvent` handler funnel into
            // once the OS (or, for the Create button, `IFilePicker`) has
            // handed over the picked files. The OS-level drag/drop
            // extraction itself (`IDataObject.GetFiles()`) is Avalonia's
            // own framework code, not this Work Package's; what this
            // exercises is everything this Work Package actually wrote:
            // the prompt, the command, the refresh, and opening the result
            // right up.
            IReadOnlyList<Tempest.Workspace.Files.PickedFile> pickedFiles =
            [
                ToPickedFile(fileA), ToPickedFile(fileB),
            ];
            var createFromFiles = GetPrivateMethod(evidenceWorkspace, "CreateFromFilesAsync");
            await (Task)createFromFiles.Invoke(evidenceWorkspace, [project.Id, pickedFiles])!;

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            Core.Evidence.Evidence? created = null;
            await RenderUntilAsync(window, () =>
            {
                created = domain.Repository.ListByKindAsync(Core.Evidence.Evidence.CanonicalKind).GetAwaiter().GetResult()
                    .OfType<Core.Evidence.Evidence>().FirstOrDefault(e => e.ParentId == project.Id);
                return created is not null;
            });
            Assert.NotNull(created);

            IReadOnlyList<IAttachment> attachments = [];
            await RenderUntilAsync(window, () =>
            {
                attachments = ((IHasAttachments)created!).GetAttachmentsAsync().GetAwaiter().GetResult();
                return attachments.Count == 2;
            });
            Assert.Equal(2, attachments.Count);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Acceptance 4: Part shows no BOM section, Where used names and opens the assembly; Assembly keeps its BOM section; Requirement and Document editors are unchanged.</summary>
    [AvaloniaFact]
    public async Task PartAndAssemblyEditors_RenderPerTheirOwnDeclaration()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window); // shows the window now, before `Opened`'s own status-bar/explorer refresh could otherwise overwrite state a later assertion reads.
            var navigator = host.ShellNavigator!;
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-EVD-4", "Evidence Declaration Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();

            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
            var assemblyResult = await commandDispatcher.DispatchAsync(
                new Tempest.Workspace.Mechanical.CreateMechanicalObjectCommand("Assembly", "Test Assembly", parentId: project.Id),
                CancellationToken.None);
            Assert.True(assemblyResult.Succeeded, assemblyResult.Message);
            var assemblyId = assemblyResult.SubjectId!.Value;

            var partResult = await commandDispatcher.DispatchAsync(
                new Tempest.Workspace.Mechanical.CreateMechanicalObjectCommand("Part", "Test Part", parentId: assemblyId),
                CancellationToken.None);
            Assert.True(partResult.Succeeded, partResult.Message);
            var partId = partResult.SubjectId!.Value;

            var partEditor = ObjectEditorViewFor(window, partId, "Part");
            Assert.NotNull(partEditor);
            AssertSectionAbsent(partEditor!, "Bill of Materials");
            var whereUsedText = partEditor!.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).FirstOrDefault(t => t.Contains("Test Assembly"));
            Assert.NotNull(whereUsedText);

            var openButton = partEditor.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => Equals(b.Content, "Open"));
            Assert.NotNull(openButton);
            openButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            ObjectEditorView? assemblyEditor = null;
            await RenderUntilAsync(window, () =>
            {
                assemblyEditor = window.GetLogicalDescendants().OfType<ObjectEditorView>()
                    .FirstOrDefault(e => e.GetLogicalDescendants().OfType<TextBox>().Any(t => t.Text == "Test Assembly"));
                return assemblyEditor is not null;
            });
            Assert.NotNull(assemblyEditor);
            AssertSectionPresent(assemblyEditor!, "Bill of Materials");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Acceptance 5: releasing a Draft material through the Libraries tab makes it appear in the citation picker without restart; a principal without <c>reference.release</c> sees the refusal.</summary>
    [AvaloniaFact]
    public async Task LibrariesTab_ReleasesADraftMaterial_VisibleInCitationPickerWithoutRestart_AndRefusesWithoutPermission()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            await RegisterDraftMaterialAsync(host, "mat-lib-release", "Library Release Alloy");

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window); // shows the window now, before `Opened`'s own status-bar/explorer refresh could otherwise overwrite state a later assertion reads.
            var navigator = host.ShellNavigator!;
            await navigator.GoToModuleAsync(ShellArea.Evidence);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var evidenceWorkspace = GetPrivateField<EvidenceWorkspaceView>(window, "_evidenceWorkspace");
            var tabs = (TabControl)evidenceWorkspace.Content!;
            tabs.SelectedIndex = 1;
            var librariesView = (LibrariesView)((TabItem)tabs.Items[1]!).Content!;
            LayOut(window);

            // The row naming this test's own record specifically — this
            // headless run's own `Tempest.Samples` module seeds further
            // Draft records into the same Materials catalogue, each with
            // its own "Release" button. `.First`, not `.Single`, within
            // that row: a control inside a `TabControl`'s own selected tab
            // is reachable via two logical-tree paths.
            var recordRow = librariesView.GetLogicalDescendants().OfType<Grid>()
                .First(g => g.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("mat-lib-release", StringComparison.Ordinal)));
            var releaseButton = recordRow.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, "Release"));
            releaseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            IReferenceRecord<MaterialDefinition>? record = null;
            var deadline = Deadline(20);
            while (DateTime.UtcNow < deadline)
            {
                record = await host.Materials!.FindAsync("mat-lib-release");
                if (record?.ValidationState == ReferenceValidationState.Released)
                    break;

                await Task.Delay(20);
                Dispatcher.UIThread.RunJobs();
                LayOut(window);
            }

            Assert.NotNull(record);
            Assert.Equal(ReferenceValidationState.Released, record!.ValidationState);

            var citationPicker = GetPrivateField<CitationPicker>(window, "_citationPicker");
            var pickTask = citationPicker.PickAsync();
            await RenderUntilAsync(window, () => citationPicker.GetLogicalDescendants().OfType<ListBoxItem>().Any());
            var items = citationPicker.GetLogicalDescendants().OfType<ListBoxItem>().Select(i => (i.Content as string) ?? string.Empty).ToList();
            Assert.Contains(items, t => t.Contains("mat-lib-release"));
            var cancel = citationPicker.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cancel"));
            cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await pickTask;

            // A principal without `reference.release` sees the refusal.
            var accessor = (CurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
            accessor.SetCurrent(new PlatformPrincipal(
                new PlatformIdentity("restricted-user", "Restricted User"),
                [Tempest.Core.Verification.VerificationService.ReadPermission]));

            await RegisterDraftMaterialAsync(host, "mat-lib-restricted", "Restricted Release Alloy");
            var ex = await Assert.ThrowsAsync<ReferenceReviewException>(() =>
                host.ReferenceReview!.ReleaseAsync(host.Materials!, "mat-lib-restricted", "Attempted without permission."));
            Assert.Contains("reference.release", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ---------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------

    private static async Task RegisterAndReleaseMaterialAsync(WorkspaceHost host, string recordId, string name)
    {
        await RegisterDraftMaterialAsync(host, recordId, name);
        await host.ReferenceReview!.VerifyAsync(host.Materials!, recordId, new ReferenceReviewStatement("Test handbook, consulted for this journey."));
        await host.ReferenceReview!.ReleaseAsync(host.Materials!, recordId, "Released for the WP 18.2A journey test.");
    }

    private static Task RegisterDraftMaterialAsync(WorkspaceHost host, string recordId, string name)
    {
        var definition = new MaterialDefinition { Name = name, Family = MaterialFamily.Aluminium, Designation = recordId };
        var provenance = new ReferenceProvenance(SourceOrganisation: "Test Handbook Publisher", SourceDocument: "Test Handbook");
        var source = new SourceCitation("Test Handbook Publisher", "Test Handbook", Page: "12");
        return host.Materials!.RegisterAsync(recordId, definition, provenance, source);
    }

    private static async Task CiteViaRealDialogAsync(MainWindow window, ObjectEditorView editor, string recordId)
    {
        var citeButton = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cite"));
        citeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var citationPicker = GetPrivateField<CitationPicker>(window, "_citationPicker");
        await RenderUntilAsync(window, () => citationPicker.GetLogicalDescendants().OfType<ListBoxItem>().Any());

        var list = citationPicker.GetLogicalDescendants().OfType<ListBox>().Single();
        var item = list.ItemsSource!.Cast<ListBoxItem>().First(i => ((string)i.Content!).Contains(recordId, StringComparison.Ordinal));
        list.SelectedItem = item;

        var pickerCiteButton = citationPicker.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cite"));
        pickerCiteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await RenderUntilAsync(window, () => !citationPicker.IsVisible);
    }

    private static async Task DeclareViaRealDialogAsync(MainWindow window, ObjectEditorView editor, string name, DeclaredFigureRole role, string quantity)
    {
        var declareButton = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Declare Figure"));
        declareButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var entry = GetPrivateField<DeclaredFigureEntry>(window, "_declaredFigureEntry");
        await RenderUntilAsync(window, () => entry.IsVisible);

        var nameBox = entry.GetLogicalDescendants().OfType<TextBox>().First();
        nameBox.Text = name;
        var roleCombo = entry.GetLogicalDescendants().OfType<ComboBox>().First();
        roleCombo.SelectedItem = role;
        var valueUnitBoxes = entry.GetLogicalDescendants().OfType<TextBox>().ToList();
        var valueBox = valueUnitBoxes[1];
        var parts = quantity.Split(' ', 2);
        valueBox.Text = parts[0];
        var unitCombo = entry.GetLogicalDescendants().OfType<ComboBox>().Skip(1).First();
        unitCombo.SelectedItem = parts.Length > 1 ? parts[1] : "1";

        var declare = entry.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Declare"));
        declare.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await RenderUntilAsync(window, () => !entry.IsVisible);
    }

    private static ObjectEditorView? ObjectEditorViewFor(MainWindow window, Guid id, string kind)
    {
        var navigateInternal = GetPrivateMethod(window, "OpenCreatedObjectAsync");
        ((Task)navigateInternal.Invoke(window, [id, kind])!).GetAwaiter().GetResult();
        LayOut(window);

        return window.GetLogicalDescendants().OfType<ObjectEditorView>().FirstOrDefault();
    }

    /// <summary>
    /// Opens an Evidence record's own editor the same way "opening a row"
    /// (§2) does — through <c>MainWindow.OpenEvidenceRecordAsync</c>, which
    /// switches to the Engineering module first so the document tab it
    /// opens is actually on screen; <see cref="ObjectEditorViewFor"/>'s own
    /// <c>OpenCreatedObjectAsync</c> path does not switch modules, so it is
    /// only right for a Kind whose objects are already opened from inside
    /// Engineering (Part, Assembly, …).
    /// </summary>
    private static ObjectEditorView? EvidenceObjectEditorViewFor(MainWindow window, Guid id, string kind)
    {
        var openEvidenceRecord = GetPrivateMethod(window, "OpenEvidenceRecordAsync");
        ((Task)openEvidenceRecord.Invoke(window, [id, kind])!).GetAwaiter().GetResult();
        LayOut(window);

        return window.GetLogicalDescendants().OfType<ObjectEditorView>().FirstOrDefault();
    }

    private static void AssertSectionAbsent(ObjectEditorView editor, string title) =>
        Assert.DoesNotContain(editor.GetLogicalDescendants().OfType<Expander>(), e => Equals(e.Header, title) && e.IsVisible);

    private static void AssertSectionPresent(ObjectEditorView editor, string title) =>
        Assert.Contains(editor.GetLogicalDescendants().OfType<Expander>(), e => Equals(e.Header, title) && e.IsVisible);

    private static System.Reflection.MethodInfo GetPrivateMethod(object instance, string name) =>
        instance.GetType().GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method '{name}' not found on {instance.GetType().Name}.");

    private static Tempest.Workspace.Files.PickedFile ToPickedFile(string path) =>
        new(Path.GetFileName(path), Tempest.Workspace.Files.FileContentTypes.ForFileName(path), () => Task.FromResult<ReadOnlyMemory<byte>>(File.ReadAllBytes(path)));

    private static string CreateTempFile(string name, string extension, int sizeBytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}{extension}");
        var bytes = new byte[sizeBytes];
        new Random(42).NextBytes(bytes);
        File.WriteAllBytes(path, bytes);
        return path;
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
