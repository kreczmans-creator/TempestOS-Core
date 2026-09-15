using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Workspace;
using Tempest.Workspace.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.People;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.Requirements;
using Tempest.Desktop.Editors;
using Tempest.Samples;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Verification;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates the Object Editor Framework (`WP 10.3A`'s own "Demonstrate"
/// list — Editable properties, Dirty-state tracking, Save/Cancel workflow,
/// Read-only mode, Relationship summary, Lifecycle display, Navigation
/// between related objects) directly against <see cref="ObjectEditorView"/>,
/// over a real, running <see cref="WorkspaceHost"/> and real Mechanical
/// sample data — never a mock or a fake domain object.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ObjectEditorViewTests
{
    [AvaloniaFact]
    public async Task TryCreate_NonExistentObjectId_ReturnsNull()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

            var editor = ObjectEditorView.TryCreate(Guid.NewGuid(), "Component", domainContext, host.Manager!, (_, _) => { }, commandDispatcher);

            Assert.Null(editor);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TryCreate_RealMechanicalObject_ReturnsARealEditor_NeverNull()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target, commandDispatcher) = await GetRealMechanicalObjectAsync(host);

            var editor = ObjectEditorView.TryCreate(target.Id, target.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher);

            Assert.NotNull(editor);
            Assert.False(editor!.IsDirty);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task EditingTheNameField_MarksDirty_RaisesDirtyChanged()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target, commandDispatcher) = await GetRealMechanicalObjectAsync(host);
            var editor = ObjectEditorView.TryCreate(target.Id, target.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher)!;

            var dirtyEvents = new List<bool>();
            editor.DirtyChanged += dirtyEvents.Add;

            var nameBox = FindByLabel<TextBox>(editor, "Name");
            nameBox.Text = "A Genuinely Different Name";

            Assert.True(editor.IsDirty);
            Assert.Contains(true, dirtyEvents);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Cancel_RevertsBufferedEdits_ClearsDirty()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target, commandDispatcher) = await GetRealMechanicalObjectAsync(host);
            var originalName = ((IHasBusinessIdentifier)target).DisplayName;
            var editor = ObjectEditorView.TryCreate(target.Id, target.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher)!;

            var nameBox = FindByLabel<TextBox>(editor, "Name");
            nameBox.Text = "Some Unsaved Edit";
            Assert.True(editor.IsDirty);

            var cancelButton = FindButtonByContent(editor, "Cancel");
            cancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.False(editor.IsDirty);
            Assert.Equal(originalName, nameBox.Text);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Save_RenamedName_ActuallyRenamesTheRealObject_ThenClearsDirty()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target, commandDispatcher) = await GetRealMechanicalObjectAsync(host);
            var editor = ObjectEditorView.TryCreate(target.Id, target.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher)!;

            var nameBox = FindByLabel<TextBox>(editor, "Name");
            nameBox.Text = "Renamed By WP10.3A Test";

            var saveButton = FindButtonByContent(editor, "Save");
            saveButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // `TD-119`: the Save/Attach click runs an `async void` handler over
            // real disk I/O. Bounded poll re-reading the real state each
            // iteration — the same remedy as `TD-46`/`WP 13.12.9` further up
            // this file. The assertions below are unchanged and still fail if
            // the write genuinely never lands.
            var reread = await domainContext.Repository.FindAsync(target.Id);
            var renameDeadline = DesktopTestHelpers.Deadline(2);
            while ((reread is null || ((IHasBusinessIdentifier)reread).DisplayName != "Renamed By WP10.3A Test" || editor.IsDirty) && DateTime.UtcNow < renameDeadline)
            {
                await Task.Delay(10);
                reread = await domainContext.Repository.FindAsync(target.Id);
            }

            Assert.Equal("Renamed By WP10.3A Test", ((IHasBusinessIdentifier)reread!).DisplayName);
            Assert.False(editor.IsDirty);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Save_RevisedContent_ActuallyAdvancesTheRealRevisionNumber()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target, commandDispatcher) = await GetRealMechanicalObjectAsync(host);
            var originalRevision = target.CurrentRevisionNumber;
            var editor = ObjectEditorView.TryCreate(target.Id, target.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher)!;

            var contentBox = FindContentBox(editor);
            contentBox.Text = "Revised content, WP 10.3A test.";

            var saveButton = FindButtonByContent(editor, "Save");
            saveButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // Poll the repository for the revised object — re-reading every
            // iteration, not once after a guess — until the revision number
            // actually advances or a generous deadline elapses. The Save
            // handler is an `async void` click subscriber whose task the test
            // cannot observe, and the revise path performs real disk I/O
            // (`EngineeringDocumentStore.ReviseAsync` writes both a revision
            // file and the document, each behind an `AsyncKeyedLock`), so a
            // fixed `Task.Delay` is a race, not a wait. This is the same
            // failure mode `TD-46` records for
            // `VerificationResultSection_RecordPass_...` further down this
            // file, and the same bounded-poll remedy: found flaky under CI
            // load, passing on a quiet machine. It bit here for real —
            // `WP 13.12.7`'s tag-triggered `release.yml` run failed this exact
            // assertion on the `v0.13.0` commit while the concurrent `ci.yml`
            // run on the identical SHA passed. This still fails, just as
            // before, if the revision genuinely never advances — it no longer
            // fails because the write merely took longer than an arbitrary
            // guess.
            IEngineeringObject? reread = null;
            var deadline = DesktopTestHelpers.Deadline(2);
            while (DateTime.UtcNow < deadline)
            {
                reread = await domainContext.Repository.FindAsync(target.Id);
                if (reread is not null && reread.CurrentRevisionNumber > originalRevision)
                    break;
                await Task.Delay(10);
            }

            Assert.True(reread!.CurrentRevisionNumber > originalRevision);
            Assert.Equal("Revised content, WP 10.3A test.", ((IHasRevisions)reread).Content);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ReadOnlyToggle_DisablesTheNameAndContentFields()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target, commandDispatcher) = await GetRealMechanicalObjectAsync(host);
            var editor = ObjectEditorView.TryCreate(target.Id, target.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher)!;

            var nameBox = FindByLabel<TextBox>(editor, "Name");
            Assert.True(nameBox.IsEnabled); // editable by default, real CanRename

            var toggle = FindReadOnlyToggle(editor);
            toggle.IsChecked = true;

            Assert.False(nameBox.IsEnabled);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ReadOnlyToggle_TurnedOnWhileDirty_DiscardsThePendingEdit()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target, commandDispatcher) = await GetRealMechanicalObjectAsync(host);
            var originalName = ((IHasBusinessIdentifier)target).DisplayName;
            var editor = ObjectEditorView.TryCreate(target.Id, target.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher)!;

            var nameBox = FindByLabel<TextBox>(editor, "Name");
            nameBox.Text = "Unsaved Edit Before Read-Only";
            Assert.True(editor.IsDirty);

            FindReadOnlyToggle(editor).IsChecked = true;

            Assert.False(editor.IsDirty);
            Assert.Equal(originalName, nameBox.Text);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task NavigateToObject_ClickedFromARelationshipRow_InvokesTheInjectedCallback()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(RequirementsWorkspaceExplorerModule.NavigationItemId);
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

            // The base sample data links a real Requirement to a real
            // Mechanical object (allocation) — found via the Requirements
            // Explorer tree, giving this test a real, live relationship row
            // to click, not a fabricated one.
            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var requirementNode = await FindFirstObjectNodeAsync(workspace.ProjectExplorer, roots);
            if (requirementNode is null)
                return; // no requirement in this sample set — nothing to prove here, honestly skipped.

            var navigated = new List<(Guid Id, string Kind)>();
            var editor = ObjectEditorView.TryCreate(requirementNode.Id, requirementNode.Kind!, domainContext, host.Manager!, (id, kind) => navigated.Add((id, kind)), commandDispatcher);
            if (editor is null)
                return; // this Kind has no real Engineering Domain object behind it — nothing to prove here.

            var openButtons = editor.GetLogicalDescendants().OfType<Button>().Where(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Open").ToList();
            if (openButtons.Count == 0)
                return; // this particular object has no relationships recorded — honestly nothing to click.

            openButtons[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.Single(navigated);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------
    // WP 10.7A — Feature Completion: the five new, real, discipline-
    // specific sections (Mechanical BOM, Requirements Owner/Priority,
    // Calculations Execute, Verification Record Result, Documents
    // Attachments) — each proven against a real sample object of its own
    // Kind, dispatching a real, already-registered command, verified by
    // re-reading the real, durable state afterward, never merely "the
    // click didn't throw."
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task BomSection_SaveOnARealPart_ActuallyPersistsTheBomLine()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var target = await FindFirstObjectSatisfyingAsync(workspace.ProjectExplorer, domainContext, roots, o => o is IHasBomLine && o.Kind is not null && ObjectEditorView.BomKinds.Contains(o.Kind));
            if (target is null)
                return; // no real BOM-eligible object in this sample set — honestly nothing to prove here.

            var editor = ObjectEditorView.TryCreate(target.Id, target.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher)!;
            Assert.NotNull(editor);

            var bomExpander = editor.GetLogicalDescendants().OfType<Expander>().Single(e => Equals(e.Header, "Bill of Materials"));
            Assert.True(bomExpander.IsVisible);

            var quantityBox = FindByLabelWithin<TextBox>(bomExpander, "Quantity");
            quantityBox.Text = "42";

            var saveButton = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save BOM Line"));
            saveButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // `TD-119`: the Save/Attach click runs an `async void` handler over
            // real disk I/O. Bounded poll re-reading the real state each
            // iteration — the same remedy as `TD-46`/`WP 13.12.9` further up
            // this file. The assertions below are unchanged and still fail if
            // the write genuinely never lands.
            var reread = await domainContext.Repository.FindAsync(target.Id);
            var bomDeadline = DesktopTestHelpers.Deadline(2);
            while ((reread is null || ((IHasBomLine)reread).Quantity != 42m) && DateTime.UtcNow < bomDeadline)
            {
                await Task.Delay(10);
                reread = await domainContext.Repository.FindAsync(target.Id);
            }

            Assert.Equal(42m, ((IHasBomLine)reread!).Quantity);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// <b>`TD-41`, closed by `WP 19.10I`.</b> <see cref="ObjectEditorView.TryCreate"/>
    /// used to gate unconditionally on <c>EngineeringDomainContext.Repository.FindAsync</c>
    /// resolving a real <see cref="IEngineeringObject"/> — and that call
    /// always returns <see langword="null"/> for a Requirement
    /// (Requirements are real <c>IEngineeringDocument</c>s, `ADR-0058`,
    /// but were never wired into the general
    /// <c>IEngineeringObjectRepository</c>'s own Kind-to-object
    /// materialisation — only reachable through
    /// <see cref="IRequirementsService"/> directly, a genuinely different
    /// read path). <c>TryCreate</c> now falls back to
    /// <see cref="IRequirementsService"/> when the repository has nothing
    /// for a Requirement's own Kind, so this section's own code — real
    /// since `WP 10.7A`, but unreachable through the Object Editor until
    /// now — is exercised here exactly as the Ribbon already exercised it
    /// (<c>FeatureCompletionTests</c>). The statement is asserted too,
    /// alongside Owner/Priority, since both are now real through this one
    /// editor.
    /// </summary>
    [AvaloniaFact]
    public async Task RequirementSection_SaveOwnerAndPriority_ActuallyPersistsThem()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(RequirementsWorkspaceExplorerModule.NavigationItemId);
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
            var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));

            // `WP 20.10F` (Product Owner finding D8): the Owner control is
            // now a drop-down of Released people — a Released person,
            // registered directly through the catalogue, is what this
            // test's own Save path now picks. The People library's own UI
            // (Add a person, Verify, Release) is proven separately
            // (`PersonLibraryJourneyTests`); this test's own focus stays the
            // Save path once a person is pickable.
            var personCatalog = (IPersonCatalog)host.Services!.GetService(typeof(IPersonCatalog));
            const string personId = "person-wp2010f-test-owner";
            const string personDisplayName = "WP 20.10F Test Owner";
            await personCatalog.RegisterAsync(personId, new Person { DisplayName = personDisplayName }, PersonProvenance.Default);
            var statement = new ReferenceReviewStatement("Test fixture — registered directly, not through the People library's own UI.");
            await host.ReferenceReview!.VerifyAsync(personCatalog, personId, statement);
            await host.ReferenceReview!.ReleaseAsync(personCatalog, personId, "Test fixture.");
            var ownerSupport = new RequirementOwnerEditorSupport(personCatalog, _ => Task.FromResult<(string RecordId, string DisplayName)?>(null));

            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var target = await FindFirstObjectNodeOfKindAsync(workspace.ProjectExplorer, roots, RequirementsService.RequirementDocumentKind);
            if (target is null)
                return; // no real Requirement in this sample set — honestly nothing to prove here.

            var editor = ObjectEditorView.TryCreate(
                target.Id, target.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher, requirementsService,
                ownerSupport: ownerSupport);
            Assert.NotNull(editor);

            var sampleRequirement = await requirementsService.FindAsync(target.Id);
            Assert.NotNull(sampleRequirement);
            var contentBox = FindContentBox(editor!);
            Assert.Equal(sampleRequirement!.Statement, contentBox.Text);

            var requirementExpander = editor!.GetLogicalDescendants().OfType<Expander>().Single(e => Equals(e.Header, "Owner / Priority"));
            Assert.True(requirementExpander.IsVisible);

            // The Owner section's own options are read through
            // IPersonCatalog in the background (`PopulateRequirementInBackground`)
            // — bounded poll for the option to appear, the same remedy this
            // file's own Save-path polls already use for a real disk write.
            var ownerBox = FindByLabelWithin<ComboBox>(requirementExpander, "Owner");
            ComboBoxItem? ownerOption = null;
            var optionDeadline = DesktopTestHelpers.Deadline(2);
            while (ownerOption is null && DateTime.UtcNow < optionDeadline)
            {
                ownerOption = (ownerBox.ItemsSource as IEnumerable<ComboBoxItem>)?.FirstOrDefault(i => Equals(i.Content, personDisplayName));
                if (ownerOption is null)
                    await Task.Delay(10);
            }

            Assert.NotNull(ownerOption);
            ownerBox.SelectedItem = ownerOption;

            var priorityBox = FindByLabelWithin<ComboBox>(requirementExpander, "Priority");
            priorityBox.SelectedItem = "High";

            var saveButton = editor!.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save Owner/Priority"));
            saveButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // `TD-119`: the Save/Attach click runs an `async void` handler over
            // real disk I/O. Bounded poll re-reading the real state each
            // iteration — the same remedy as `TD-46`/`WP 13.12.9` further up
            // this file. The assertions below are unchanged and still fail if
            // the write genuinely never lands.
            var reread = await requirementsService.FindAsync(target.Id);
            var ownerDeadline = DesktopTestHelpers.Deadline(2);
            while ((reread is null || reread.Owner != personDisplayName) && DateTime.UtcNow < ownerDeadline)
            {
                await Task.Delay(10);
                reread = await requirementsService.FindAsync(target.Id);
            }

            Assert.Equal(personDisplayName, reread!.Owner);
            Assert.Equal(personId, reread.OwnerPersonId);
            Assert.Equal(RequirementPriority.High, reread.Priority);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task VerificationResultSection_RecordPass_ActuallyRecordsARealVerificationRecord()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(VerificationWorkspaceExplorerModule.NavigationItemId);
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var target = await FindFirstObjectNodeOfKindAsync(workspace.ProjectExplorer, roots, "VerificationActivity");
            if (target is null)
                return; // no real Verification Activity in this sample set — honestly nothing to prove here.

            var editor = ObjectEditorView.TryCreate(target.Id, target.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher)!;
            Assert.NotNull(editor);

            var resultExpander = editor.GetLogicalDescendants().OfType<Expander>().Single(e => Equals(e.Header, "Record Result"));
            Assert.True(resultExpander.IsVisible);

            var passButton = resultExpander.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Pass"));
            passButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // Deterministic synchronisation, not a fixed delay: the "Record
            // Pass" click dispatches an async command whose completion (and
            // consequent status-text update) has no other observable signal
            // this test can await, so it polls the real, current status text
            // — re-read from the live visual tree every iteration, not a
            // reference captured before the command completes — until it
            // contains "recorded" or a generous deadline elapses. A fixed
            // `Task.Delay` here was found flaky under CI load (WP 11.9.0
            // Release Publication Report, Finding 1): the assertion could
            // read the TextBlock before the async update landed. This still
            // fails, just as before, if the operation genuinely never
            // completes — it no longer fails because it merely ran slower
            // than an arbitrary guess.
            string? statusText = null;
            var deadline = DesktopTestHelpers.Deadline(2);
            while (DateTime.UtcNow < deadline)
            {
                statusText = resultExpander.GetLogicalDescendants().OfType<TextBlock>().Last().Text;
                if (statusText is not null && statusText.Contains("recorded", StringComparison.OrdinalIgnoreCase))
                    break;
                await Task.Delay(10);
            }

            Assert.Contains("recorded", statusText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AttachmentsSection_AttachOnARealDocument_ActuallyPersistsIt()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(DocumentsWorkspaceExplorerModule.NavigationItemId);
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var target = await FindFirstObjectSatisfyingAsync(workspace.ProjectExplorer, domainContext, roots, o => o is IHasAttachments);
            if (target is null)
                return; // no real attachable object in this sample set — honestly nothing to prove here.

            var editor = ObjectEditorView.TryCreate(target.Id, target.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher)!;
            Assert.NotNull(editor);

            var attachmentsExpander = editor.GetLogicalDescendants().OfType<Expander>().Single(e => Equals(e.Header, "Attachments"));
            Assert.True(attachmentsExpander.IsVisible);

            var fileNameBox = FindByLabelWithin<TextBox>(attachmentsExpander, "File Name");
            fileNameBox.Text = "wp107a-test.pdf";
            var sizeBox = FindByLabelWithin<TextBox>(attachmentsExpander, "Size (bytes)");
            sizeBox.Text = "1024";

            var attachButton = attachmentsExpander.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Attach"));
            attachButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // `TD-119`: the Save/Attach click runs an `async void` handler over
            // real disk I/O. Bounded poll re-reading the real state each
            // iteration — the same remedy as `TD-46`/`WP 13.12.9` further up
            // this file. The assertions below are unchanged and still fail if
            // the write genuinely never lands.
            var reread = await domainContext.Repository.FindAsync(target.Id);
            var attachments = await ((IHasAttachments)reread!).GetAttachmentsAsync();
            var attachDeadline = DesktopTestHelpers.Deadline(2);
            while (!attachments.Any(a => a.FileName == "wp107a-test.pdf" && a.SizeInBytes == 1024) && DateTime.UtcNow < attachDeadline)
            {
                await Task.Delay(10);
                reread = await domainContext.Repository.FindAsync(target.Id);
                attachments = await ((IHasAttachments)reread!).GetAttachmentsAsync();
            }

            Assert.Contains(attachments, a => a.FileName == "wp107a-test.pdf" && a.SizeInBytes == 1024);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------
    // WP 17.9.1 — sections by Kind. The first Windows review of v0.17.0
    // opened a Project and a Calculation and was offered Quantity / Find
    // Number (every canonical object implements IHasBomLine) and, on the
    // Calculation, an "Execute" section with a raw JSON box. The BOM
    // section belongs to the mechanical Kinds; the JSON box is retired and
    // a Calculation points at the Engineering Calculations workspace.
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task ProjectEditor_ShowsNeitherBomNorExecute()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var project = await FindFirstObjectNodeOfKindAsync(workspace.ProjectExplorer, roots, MechanicalObjectFactoryRegistry.Project);
            Assert.NotNull(project);

            var editor = ObjectEditorView.TryCreate(project!.Id, project.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher)!;
            Assert.NotNull(editor);

            Assert.False(Section(editor, "Bill of Materials").IsVisible);
            Assert.False(Section(editor, "Execute").IsVisible);
            Assert.False(Section(editor, "Calculation").IsVisible);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task CalculationEditor_RetiresTheJsonExecuteBox_AndPointsAtTheCalculationWorkspace()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(CalculationsWorkspaceExplorerModule.NavigationItemId);
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

            // A Calculation made through the real production command, so the
            // editor is proven on the object a user would actually create.
            var created = await commandDispatcher.DispatchAsync(new CreateCalculationObjectCommand(
                CalculationObjectFactoryRegistry.CalculationKind, "Quick Bolt Check", "QC-1"), CancellationToken.None);
            Assert.True(created.Succeeded, created.Message);
            var calculation = (await domainContext.Repository.ListByKindAsync(CalculationObjectFactoryRegistry.CalculationKind))
                .Single(o => ((IHasBusinessIdentifier)o).Identifier == "QC-1");

            var editor = ObjectEditorView.TryCreate(calculation.Id, calculation.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher)!;
            Assert.NotNull(editor);

            Assert.False(Section(editor, "Bill of Materials").IsVisible);
            Assert.False(Section(editor, "Execute").IsVisible);
            Assert.DoesNotContain(editor.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Input (JSON):" && t.IsVisible && t.GetLogicalAncestors().OfType<Expander>().All(e => e.IsVisible));

            var pointer = Section(editor, "Calculation");
            Assert.True(pointer.IsVisible);
            Assert.Contains(pointer.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == ObjectEditorView.CalculationPointerGuidance);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static Expander Section(ObjectEditorView editor, string header) =>
        editor.GetLogicalDescendants().OfType<Expander>().Single(e => Equals(e.Header, header));

    private static async Task<IEngineeringObject?> FindFirstObjectSatisfyingAsync(
        IProjectExplorer explorer, EngineeringDomainContext domainContext, IReadOnlyList<ProjectExplorerNode> nodes, Func<IEngineeringObject, bool> predicate)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object)
            {
                var candidate = await domainContext.Repository.FindAsync(node.Id);
                if (candidate is not null && predicate(candidate))
                    return candidate;
            }

            if (node.HasChildren)
            {
                var found = await FindFirstObjectSatisfyingAsync(explorer, domainContext, await explorer.GetChildrenAsync(node.Id), predicate);
                if (found is not null)
                    return found;
            }
        }

        return null;
    }

    private static async Task<ProjectExplorerNode?> FindFirstObjectNodeOfKindAsync(IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes, string kind)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object && node.Kind == kind)
                return node;

            if (node.HasChildren)
            {
                var found = await FindFirstObjectNodeOfKindAsync(explorer, await explorer.GetChildrenAsync(node.Id), kind);
                if (found is not null)
                    return found;
            }
        }

        return null;
    }

    /// <summary>The <see cref="FindByLabel{T}"/> equivalent, scoped to search only within <paramref name="root"/> (an individual section's own Expander) rather than the whole editor — needed once multiple sections could otherwise share an ambiguous label.</summary>
    private static T FindByLabelWithin<T>(Control root, string label) where T : Control
    {
        var grid = root.GetLogicalDescendants().OfType<Grid>()
            .Single(g => g.Children.Count == 2 && g.Children[0] is TextBlock { Text: var text } && text == label);
        return (T)grid.Children[1];
    }

    private static async Task<(EngineeringDomainContext DomainContext, IEngineeringObject Target, ICommandDispatcher CommandDispatcher)> GetRealMechanicalObjectAsync(WorkspaceHost host)
    {
        var workspace = host.Workspace!;
        await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);

        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
        var objectNode = await FindFirstObjectNodeAsync(workspace.ProjectExplorer, roots);
        Assert.NotNull(objectNode);

        var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
        var target = await domainContext.Repository.FindAsync(objectNode!.Id);
        Assert.NotNull(target);

        return (domainContext, target!, commandDispatcher);
    }

    private static async Task<ProjectExplorerNode?> FindFirstObjectNodeAsync(IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object)
                return node;

            if (node.HasChildren)
            {
                var found = await FindFirstObjectNodeAsync(explorer, await explorer.GetChildrenAsync(node.Id));
                if (found is not null)
                    return found;
            }
        }

        return null;
    }

    // Direct Children/logical-tree traversal — ObjectEditorView's own
    // Identity section is a single LabeledRow per field; this helper finds
    // the value control next to a given label text, mirroring
    // PanelHostControlTests' own "direct traversal, not VisualTree
    // extensions" precedent for a control never attached to a real Window.
    private static T FindByLabel<T>(ObjectEditorView editor, string label) where T : Control
    {
        var grid = editor.GetLogicalDescendants().OfType<Grid>()
            .Single(g => g.Children.Count == 2 && g.Children[0] is TextBlock { Text: var text } && text == label);
        return (T)grid.Children[1];
    }

    private static TextBox FindContentBox(ObjectEditorView editor) =>
        editor.GetLogicalDescendants().OfType<Expander>().Single(e => Equals(e.Header, "Content")).Content as TextBox
        ?? throw new InvalidOperationException("Content section body was not a TextBox.");

    private static ToggleButton FindReadOnlyToggle(ObjectEditorView editor) =>
        editor.GetLogicalDescendants().OfType<ToggleButton>().Single();

    private static Button FindButtonByContent(ObjectEditorView editor, string content) =>
        editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, content));
}
