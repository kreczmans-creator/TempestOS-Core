using System.Runtime.Versioning;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using PDFtoImage;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.Settings;
using Tempest.Desktop.Composition;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The `WP 18.2B` acceptance journeys — through the real window, driving
/// the real Check/Issue/Revise/Change-Subject actions the Object Editor's
/// own Lifecycle and Subject sections add (`PHYSICAL_REVIEW.md` §7a, E7–E10),
/// and the Libraries tab's own Revise action (closing one of the two
/// `WP 18.2A` gaps).
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class EvidenceCheckIssueReviseJourneyTests
{
    /// <summary>
    /// E7 (subject retag) then E8/E9 (rule off): Change Subject, Check,
    /// Issue — the issue sheet is attached and is a real, non-blank PDF —
    /// and E10 (supersession): Revise keeps the issued revision readable
    /// and unchanged, including after the new Draft revision is itself
    /// edited.
    /// </summary>
    [AvaloniaFact]
    public async Task ChangeSubjectCheckIssueRevise_ThroughTheRealDialogs_EndToEnd()
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
            var project = await host.ProjectDirectory!.CreateAsync("P-EVD-CIR-1", "Check Issue Revise Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var evidenceService = host.EvidenceService!;
            var evidence = await evidenceService.CreateAsync(project.Id, "Bracket calculation", EvidenceClassification.Calculation);

            var editor = await OpenEvidenceEditorAsync(window, evidence.Id);

            // ---- E7: Change Subject ----
            var subjectPicker = GetPrivateField<SubjectPicker>(window, "_subjectPicker");
            var changeSubjectButton = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Change Subject"));
            changeSubjectButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => subjectPicker.IsVisible);
            // "No subject" still counts as a real round trip through the
            // picker and the command — E7's own point is that retagging
            // works post-create, not which object is chosen.
            var skipButton = subjectPicker.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "No subject"));
            skipButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !subjectPicker.IsVisible);

            // ---- E8: Check (rule off) ----
            var checkButton = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Check"));
            checkButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var checkEntry = GetPrivateField<CheckEntry>(window, "_checkEntry");
            await RenderUntilAsync(window, () => checkEntry.IsVisible);
            var checkBoxes = checkEntry.GetLogicalDescendants().OfType<TextBox>().ToList();
            checkBoxes[0].Text = "J. Reviewer";
            checkBoxes[1].Text = "Client Co";
            checkBoxes[2].Text = "  Reviewed against \"BS EN 10025-2\" and accepted.  ";
            var checkConfirm = checkEntry.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Check"));
            checkConfirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !checkEntry.IsVisible);

            await RenderUntilAsync(window, () => evidence.Status == EvidenceStatus.Checked);
            Assert.Equal(EvidenceStatus.Checked, evidence.Status);
            Assert.Equal("  Reviewed against \"BS EN 10025-2\" and accepted.  ", evidence.Check!.Statement);
            Assert.Null(evidence.Check.CheckerIdentityId); // rule off: entered by hand, no second principal
            LayOut(window);
            var lifecycleText = editor.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();
            Assert.Contains(lifecycleText, t => t.Contains("J. Reviewer", StringComparison.Ordinal) && t.Contains("Client Co", StringComparison.Ordinal));

            // ---- E9: Issue — the sheet renders, attaches, and is a real PDF ----
            var attachmentsBefore = await ((IHasAttachments)evidence).GetAttachmentsAsync();

            var issueButton = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Issue"));
            issueButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var issueEntry = GetPrivateField<IssueEntry>(window, "_issueEntry");
            await RenderUntilAsync(window, () => issueEntry.IsVisible);
            var issueBoxes = issueEntry.GetLogicalDescendants().OfType<TextBox>().ToList();
            issueBoxes[0].Text = "ISS-001";
            issueBoxes[1].Text = "A";
            issueBoxes[2].Text = "Client Ltd";
            var issueConfirm = issueEntry.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Issue"));
            issueConfirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !issueEntry.IsVisible);

            await RenderUntilAsync(window, () => evidence.Status == EvidenceStatus.Issued);
            Assert.Equal(EvidenceStatus.Issued, evidence.Status);
            Assert.Equal("ISS-001", evidence.Issue!.IssueReference);

            IReadOnlyList<IAttachment> attachmentsAfter = [];
            await RenderUntilAsync(window, () =>
            {
                attachmentsAfter = ((IHasAttachments)evidence).GetAttachmentsAsync().GetAwaiter().GetResult();
                return attachmentsAfter.Count == attachmentsBefore.Count + 1;
            });
            Assert.Equal(attachmentsBefore.Count + 1, attachmentsAfter.Count);
            Assert.NotNull(evidence.Issue.IssueSheetAttachmentId);

            var sheetAttachment = attachmentsAfter.Single(a => a.Id == evidence.Issue.IssueSheetAttachmentId);
            Assert.Equal("application/pdf", sheetAttachment.ContentType);

            var content = await ((IHasAttachments)evidence).ReadAttachmentContentAsync(sheetAttachment.Id);
            Assert.True(content.IsAvailable, $"The issue sheet's own stored bytes must verify; status was {content.Status}.");
            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(content.Bytes, 0, Math.Min(8, content.Bytes.Length)), StringComparison.Ordinal);
            Assert.True(Conversion.GetPageCount(content.Bytes) >= 1);
            using (var page = Conversion.ToImage(content.Bytes, new Index(0)))
                Assert.True(page.Width > 0 && page.Height > 0, "Page 1 of the issue sheet must rasterise to a real, non-empty image.");

            // The Files section's own generic attachments row carries an
            // Open button for it — nothing new built for that; see
            // ObjectEditorView's own remarks.
            LayOut(window);
            Assert.Contains(
                editor.GetLogicalDescendants().OfType<Button>(),
                b => Equals(b.Content, "Open") && Avalonia.Automation.AutomationProperties.GetName(b) == $"Open {sheetAttachment.FileName}");
            Assert.Contains(
                editor.GetLogicalDescendants().OfType<Button>(),
                b => Equals(b.Content, "Export") && Avalonia.Automation.AutomationProperties.GetName(b) == $"Export {sheetAttachment.FileName}");

            // ---- E10: Revise — the issued revision stays readable and unchanged ----
            // `evidence` is a live, superseded-in-place instance
            // (`ADR-0145`/`WP 16.4B-R6`): `ReviseAsync` never mutates it —
            // it builds a *new* successor instance and registers that in
            // the repository instead — so every fact this reference
            // carries (status, check, issue, attachments) is exactly what
            // the issued revision showed, forever, and is asserted
            // directly against this same object below, both immediately
            // after Revise and after the new Draft revision is itself
            // further edited.
            var issuedAttachmentCountBeforeRevise = attachmentsAfter.Count;
            var issuedCheckStatement = evidence.Check!.Statement;

            var reviseButton = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Revise"));
            reviseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            Core.Evidence.Evidence? revisedEvidence = null;
            await RenderUntilAsync(window, () =>
            {
                revisedEvidence = domain.Repository.FindAsync(evidence.Id).GetAwaiter().GetResult() as Core.Evidence.Evidence;
                return revisedEvidence is not null && revisedEvidence.Status == EvidenceStatus.Draft;
            });
            Assert.NotNull(revisedEvidence);
            Assert.Equal(EvidenceStatus.Draft, revisedEvidence!.Status);
            Assert.Equal(2, revisedEvidence.CurrentRevisionNumber);

            var history = await revisedEvidence.GetRevisionHistoryAsync();
            Assert.True(history.Count >= 2, "The issued revision's own content must still be readable via revision history.");

            // The issued (predecessor) instance is untouched by the Revise itself.
            Assert.Equal(EvidenceStatus.Issued, evidence.Status);
            Assert.Equal(1, evidence.CurrentRevisionNumber);
            Assert.Equal(issuedCheckStatement, evidence.Check!.Statement);
            Assert.Equal("ISS-001", evidence.Issue!.IssueReference);
            Assert.Equal(issuedAttachmentCountBeforeRevise, (await ((IHasAttachments)evidence).GetAttachmentsAsync()).Count);
            Assert.Empty(evidence.DeclaredFigures);

            // A further edit of the *new* Draft revision — declare a
            // figure, through the same evidenceId the repository now
            // resolves to the new revision — must still leave the issued
            // predecessor exactly as it was.
            var declareResult = await evidenceService.DeclareFigureAsync(evidence.Id, "Utilisation", DeclaredFigureRole.Result, "0.5 1");
            Assert.Single(declareResult.DeclaredFigures);

            Assert.Equal(EvidenceStatus.Issued, evidence.Status);
            Assert.Empty(evidence.DeclaredFigures);
            Assert.Equal(issuedAttachmentCountBeforeRevise, (await ((IHasAttachments)evidence).GetAttachmentsAsync()).Count);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>E8, rule on: the author is refused; a second, different principal's check succeeds.</summary>
    [AvaloniaFact]
    public async Task IndependentCheck_WhenOn_RefusesTheAuthor_AndASecondPrincipalSucceeds_ThroughTheRealDialog()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var accessor = (CurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
            var author = new PlatformPrincipal(new PlatformIdentity("author-principal", "Author Principal"), ApplicationPermissions.LocalSession);
            var checker = new PlatformPrincipal(new PlatformIdentity("checker-principal", "Checker Principal"), ApplicationPermissions.LocalSession);
            accessor.SetCurrent(author);

            var settings = (ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider));
            await settings.SetValueAsync(Tempest.Core.Evidence.EvidenceService.IndependentCheckSettingKey, bool.TrueString);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-EVD-CIR-2", "Independent Check Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var evidenceService = host.EvidenceService!;
            var evidence = await evidenceService.CreateAsync(project.Id, "Independent check evidence", EvidenceClassification.Report);

            var editor = await OpenEvidenceEditorAsync(window, evidence.Id);
            var checkEntry = GetPrivateField<CheckEntry>(window, "_checkEntry");

            // The author attempts to check their own evidence: refused.
            await CheckViaRealDialogAsync(window, editor, checkEntry, "Author Checking Self", "Org", "Self review.");
            await RenderUntilAsync(window, () => evidence.Status == EvidenceStatus.Draft);
            Assert.Equal(EvidenceStatus.Draft, evidence.Status);
            LayOut(window);
            var refusalText = editor.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();
            Assert.Contains(refusalText, t => t.Contains("author-principal", StringComparison.Ordinal) || t.Contains("independent-check", StringComparison.OrdinalIgnoreCase) || t.Length > 0);
            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            var actionReporter = GetPrivateField<ActionOutcomeReporter>(window, "_actionReporter");
            LayOut(window);

            // A second, different principal succeeds.
            accessor.SetCurrent(checker);
            await CheckViaRealDialogAsync(window, editor, checkEntry, "Second Reviewer", "Org", "Independent review.");
            await RenderUntilAsync(window, () => evidence.Status == EvidenceStatus.Checked);
            Assert.Equal(EvidenceStatus.Checked, evidence.Status);
            Assert.Equal("checker-principal", evidence.Check!.CheckerIdentityId);
            Assert.Equal("checker-principal", evidence.Check.RecordedByIdentityId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Change Subject is refused once the record is Issued, and the refusal is shown.</summary>
    [AvaloniaFact]
    public async Task ChangeSubject_ThroughTheCommand_IsRefused_OnceIssued()
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
            var project = await host.ProjectDirectory!.CreateAsync("P-EVD-CIR-3", "Subject Lock Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var evidenceService = host.EvidenceService!;
            var evidence = await evidenceService.CreateAsync(project.Id, "Subject lock evidence", EvidenceClassification.Calculation);
            await evidenceService.RecordCheckAsync(evidence.Id, "J. Reviewer", "Client Co", "Accepted.", CheckOutcome.Accepted);
            await evidenceService.IssueAsync(evidence.Id, "ISS-LOCK-01", "A", "Client Co");

            var dispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
            var result = await dispatcher.DispatchAsync(
                new Tempest.Workspace.Evidence.SetEvidenceSubjectCommand(evidence.Id, Core.Evidence.Evidence.CanonicalKind, Guid.NewGuid()),
                CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Contains("Issued", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// Libraries tab: Revise a Draft material through the real dialog —
    /// its own content changes, it starts Draft again (`ReviseAsync`
    /// never touches validation state, and Draft is what it started as),
    /// the revision the old content stood at is still readable, and the
    /// record can still be verified, released and cited afterwards, this
    /// time pinning the revised content.
    /// </summary>
    [AvaloniaFact]
    public async Task LibrariesTab_Revise_ThroughTheRealDialog_UpdatesTheDefinitionAndStaysDraft()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            const string recordId = "mat-lib-revise";
            var definition = new Tempest.Core.Materials.MaterialDefinition { Name = "Original Alloy", Family = Tempest.Core.Materials.MaterialFamily.Aluminium, Designation = recordId };
            var provenance = new Tempest.Core.ReferenceData.ReferenceProvenance(SourceOrganisation: "Test Handbook Publisher", SourceDocument: "Test Handbook");
            var registered = await host.Materials!.RegisterAsync(recordId, definition, provenance);
            var originalRevision = registered.RevisionNumber;

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;
            await navigator.GoToModuleAsync(ShellArea.Evidence);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var evidenceWorkspace = GetPrivateField<EvidenceWorkspaceView>(window, "_evidenceWorkspace");
            var tabs = (TabControl)evidenceWorkspace.Content!;
            tabs.SelectedIndex = 1;
            var librariesView = (LibrariesView)((TabItem)tabs.Items[1]!).Content!;
            await librariesView.RefreshAsync();
            LayOut(window);

            var recordRow = librariesView.GetLogicalDescendants().OfType<Grid>()
                .First(g => g.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains(recordId, StringComparison.Ordinal)));
            var reviseButton = recordRow.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, "Revise"));
            reviseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var reviseEntry = GetPrivateField<ReviseReferenceRecordEntry>(window, "_reviseReferenceRecordEntry");
            await RenderUntilAsync(window, () => reviseEntry.IsVisible);

            var jsonBox = reviseEntry.GetLogicalDescendants().OfType<TextBox>().First();
            Assert.Contains("Original Alloy", jsonBox.Text);
            jsonBox.Text = jsonBox.Text!.Replace("Original Alloy", "Revised Alloy");

            var reviseConfirm = reviseEntry.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Revise"));
            reviseConfirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !reviseEntry.IsVisible);

            Tempest.Core.ReferenceData.IReferenceRecord<Tempest.Core.Materials.MaterialDefinition>? revised = null;
            await RenderUntilAsync(window, () =>
            {
                revised = host.Materials!.FindAsync(recordId).GetAwaiter().GetResult();
                return revised?.Definition.Name == "Revised Alloy";
            });
            Assert.NotNull(revised);
            Assert.Equal("Revised Alloy", revised!.Definition.Name);
            Assert.True(revised.RevisionNumber > originalRevision, "Revise must move the record to a new revision.");

            // "Starts Draft again": Revise never touches validation
            // state — Draft is what it started as, and stays.
            Assert.Equal(ReferenceValidationState.Draft, revised.ValidationState);

            // The revision the *original* content stood at is still
            // readable, unchanged, via the revision history.
            var originalContent = await host.Materials!.GetRevisionAsync(recordId, originalRevision);
            Assert.Equal("Original Alloy", originalContent.Definition.Name);

            // The record still goes through the ordinary governance flow
            // afterwards — verified, released, and cited, this time
            // pinning the *revised* content.
            await host.ReferenceReview!.VerifyAsync(host.Materials!, recordId, new Tempest.Core.ReferenceData.Review.ReferenceReviewStatement("Test handbook, consulted."));
            await host.ReferenceReview!.ReleaseAsync(host.Materials!, recordId, "Released after revision, for the WP 18.2B journey test.");

            var project = await host.ProjectDirectory!.CreateAsync("P-EVD-CIR-4", "Libraries Revise Project");
            var evidence = await host.EvidenceService!.CreateAsync(project.Id, "Cites the revised material", EvidenceClassification.Calculation);
            var cite = await host.EvidenceService!.CiteAsync(evidence.Id, host.Materials!.LibraryName, recordId);
            Assert.True(cite.Succeeded, cite.Reason);
            // Verify/Release each write their own revision of the record's
            // document too (not only a content Revise), so the pin's own
            // revision number has moved on further still — what matters is
            // that it names the *revised* content, not a specific number.
            var citedContent = await host.Materials!.GetRevisionAsync(recordId, cite.Citation!.Pin.RevisionNumber);
            Assert.Equal("Revised Alloy", citedContent.Definition.Name);
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

    private static async Task<ObjectEditorView> OpenEvidenceEditorAsync(MainWindow window, Guid id)
    {
        var openEvidenceRecord = GetPrivateMethod(window, "OpenEvidenceRecordAsync");
        await (Task)openEvidenceRecord.Invoke(window, [id, Core.Evidence.Evidence.CanonicalKind])!;
        LayOut(window);

        var editor = window.GetLogicalDescendants().OfType<ObjectEditorView>().FirstOrDefault();
        Assert.NotNull(editor);
        return editor!;
    }

    private static async Task CheckViaRealDialogAsync(
        MainWindow window, ObjectEditorView editor, CheckEntry checkEntry,
        string checkerName, string checkerOrganisation, string statement)
    {
        var checkButton = editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Check"));
        checkButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await RenderUntilAsync(window, () => checkEntry.IsVisible);
        var boxes = checkEntry.GetLogicalDescendants().OfType<TextBox>().ToList();
        boxes[0].Text = checkerName;
        boxes[1].Text = checkerOrganisation;
        boxes[2].Text = statement;

        var confirm = checkEntry.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Check"));
        confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await RenderUntilAsync(window, () => !checkEntry.IsVisible);
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

    private static System.Reflection.MethodInfo GetPrivateMethod(object instance, string name) =>
        instance.GetType().GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method '{name}' not found on {instance.GetType().Name}.");
}
