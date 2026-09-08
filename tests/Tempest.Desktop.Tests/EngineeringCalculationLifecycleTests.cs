using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Workspace.Engineering;
using Tempest.Workspace.Shell;
using Tempest.Core.Calculations;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// What an engineer can do to a calculation once it exists — name it, find
/// it among others, rename it, take it out of the active list, and still
/// find it afterwards — driven entirely through the real shell.
/// </summary>
/// <remarks>
/// <para>
/// The third file in this set, after
/// <see cref="EngineeringCalculationJourneyTests"/> (the calculation
/// itself) and <see cref="EngineeringCalculationWorkspaceTests"/> (the
/// surrounding workspace). It applies the same rule as both: <b>every
/// control is checked visible, enabled and laid out at a real size before
/// it is used</b>, because a synthetic routed event reaches a hidden or
/// disabled control just as happily as a usable one, and a test that
/// activates something a person cannot reach proves nothing.
/// </para>
/// <para>
/// <b>Nothing here asserts what only the test process is true of.</b> This
/// assembly references <c>Tempest.Samples</c>, whose modules execute
/// calculations of their own while initialising, and the shipped Desktop
/// does not. So no test here counts the list, asserts it starts empty, or
/// assumes the calculation it ran is the only one — each finds its own
/// record by Id and asserts about that.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class EngineeringCalculationLifecycleTests
{
    private const string EngineerId = "lifecycle-test-engineer";
    private const string RailEntry = "Engineering Calculations";

    [AvaloniaFact]
    public async Task TwoCalculationsAreBothListed_EachUnderTheNameTheEngineerGaveIt()
    {
        await InWorkspaceAsync(async (host, window, view) =>
        {
            var first = await RunNamedCheckAsync(window, view, "Bracket A — lower lug", area: "60");
            var second = await RunNamedCheckAsync(window, view, "Bracket B — upper lug", area: "40");

            Assert.NotEqual(first.CalculationRecordId, second.CalculationRecordId);

            view = SurfaceOf(window);

            var a = view.Calculations.Single(c => c.RecordId == first.CalculationRecordId);
            var b = view.Calculations.Single(c => c.RecordId == second.CalculationRecordId);

            // Named, and named what the engineer typed — not a hex fragment.
            Assert.Equal("Bracket A — lower lug", a.Title);
            Assert.Equal("Bracket B — upper lug", b.Title);
            Assert.True(a.IsNamed);
            Assert.True(b.IsNamed);
            Assert.NotEqual(a.ObjectId, b.ObjectId);

            // Each carries its own evidence, not the other's.
            Assert.NotEqual(a.ResultSummary, b.ResultSummary);
            Assert.Equal(MaterialSeed.Aluminium6082T6, a.MaterialRecordId);
            Assert.Equal(MaterialSeed.Aluminium6082T6, b.MaterialRecordId);

            // And both are on screen, distinguishable without opening either.
            AssertRendered(window, view, a.Label);
            AssertRendered(window, view, b.Label);
        });
    }

    [AvaloniaFact]
    public async Task RenamingChangesTheNameOnly_AndLeavesIdentityRecordAndReferencesAlone()
    {
        await InWorkspaceAsync(async (host, window, view) =>
        {
            var executed = await RunNamedCheckAsync(window, view, "Bracket check — draft name", area: "60");
            view = SurfaceOf(window);

            var before = view.Calculations.Single(c => c.RecordId == executed.CalculationRecordId);
            Assert.True(before.IsNamed);

            var objectId = before.ObjectId!.Value;
            var domain = DomainOf(host);
            var target = await domain.Repository.FindAsync(objectId);
            var revisionsBefore = ((IEngineeringObject)target!).CurrentRevisionNumber;
            var linksBefore = await ((IHasRelationships)target!).GetRelationshipsAsync();
            var statusBefore = ((IHasLifecycle)target!).Status;

            // --- rename, through the surface ---------------------------
            SelectAsync(window, view, before);
            view = SurfaceOf(window);

            var renameBox = TextBoxOf(view, "New name for the selected calculation");
            AssertUsable(window, renameBox, "the rename box");

            // It is pre-filled with the current name, so a rename starts
            // from what the calculation is actually called.
            Assert.Equal(before.Title, renameBox.Text);

            renameBox.Text = "Bracket check — issued for review";

            // A refresh that is not a change of selection must not discard
            // what the engineer is halfway through typing.
            CheckBoxOf(view, EngineeringCalculationView.ShowRetiredCaption).IsChecked = true;
            await RenderUntilAsync(window, () => SurfaceOf(window).StatusMessage.Contains("Showing retired", StringComparison.Ordinal));
            Assert.Equal("Bracket check — issued for review", TextBoxOf(SurfaceOf(window), "New name for the selected calculation").Text);

            view = SurfaceOf(window);
            await ClickAsync(window, view, EngineeringCalculationView.RenameCaption);
            await RenderUntilAsync(window, () =>
                SurfaceOf(window).Calculations.Any(c => c.RecordId == executed.CalculationRecordId && c.Title == "Bracket check — issued for review"));

            view = SurfaceOf(window);
            var after = view.Calculations.Single(c => c.RecordId == executed.CalculationRecordId);

            // The name changed.
            Assert.Equal("Bracket check — issued for review", after.Title);

            // Nothing else did.
            Assert.Equal(objectId, after.ObjectId);
            Assert.Equal(before.RecordId, after.RecordId);
            Assert.Equal(before.PinnedRevision, after.PinnedRevision);
            Assert.Equal(before.MaterialRecordId, after.MaterialRecordId);
            Assert.Equal(before.ResultSummary, after.ResultSummary);
            Assert.Equal(before.ExecutedAt, after.ExecutedAt);
            Assert.Equal(before.ExecutedByPrincipalId, after.ExecutedByPrincipalId);
            Assert.False(after.IsRetired);

            // Not just the read model — the governed object itself keeps
            // its identity, its revision history, its lifecycle status and
            // its links to the record it produced.
            var renamed = await domain.Repository.FindAsync(objectId);
            Assert.Equal(objectId, renamed!.Id);
            Assert.Equal(revisionsBefore, renamed.CurrentRevisionNumber);
            Assert.Equal(statusBefore, ((IHasLifecycle)renamed).Status);
            Assert.Equal("Bracket check — issued for review", ((IHasBusinessIdentifier)renamed).DisplayName);

            var linksAfter = await ((IHasRelationships)renamed).GetRelationshipsAsync();
            Assert.Equal(linksBefore.Count, linksAfter.Count);
            Assert.Contains(linksAfter, l => l.TargetId == executed.CalculationRecordId);

            // And the record itself is untouched and still readable.
            var engine = (ICalculationEngine)host.Services!.GetService(typeof(ICalculationEngine));
            var record = await engine.FindRecordAsync<BracketSectionCheckResult>(executed.CalculationRecordId);
            Assert.NotNull(record);
            Assert.Equal(executed.PinnedRevision, record!.Result.MaterialPin.RevisionNumber);
        });
    }

    [AvaloniaFact]
    public async Task ABlankRenameIsRefused_AndChangesNothing()
    {
        await InWorkspaceAsync(async (host, window, view) =>
        {
            var executed = await RunNamedCheckAsync(window, view, "Bracket check — keep this name", area: "60");
            view = SurfaceOf(window);

            var before = view.Calculations.Single(c => c.RecordId == executed.CalculationRecordId);
            SelectAsync(window, view, before);
            view = SurfaceOf(window);

            TextBoxOf(view, "New name for the selected calculation").Text = "   ";
            await ClickAsync(window, view, EngineeringCalculationView.RenameCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).StatusMessage.Contains("needs a name", StringComparison.Ordinal));

            view = SurfaceOf(window);

            // The refusal is said out loud, and the name is what it was.
            Assert.Contains("needs a name", view.StatusMessage, StringComparison.Ordinal);
            Assert.Equal("Bracket check — keep this name", view.Calculations.Single(c => c.RecordId == executed.CalculationRecordId).Title);
        });
    }

    [AvaloniaFact]
    public async Task RetiringRemovesItFromTheActiveListWithoutDeletingIt_AndItCanStillBeFoundAndOpened()
    {
        await InWorkspaceAsync(async (host, window, view) =>
        {
            var executed = await RunNamedCheckAsync(window, view, "Bracket check — superseded by rev B", area: "60");
            view = SurfaceOf(window);

            var listed = view.Calculations.Single(c => c.RecordId == executed.CalculationRecordId);
            var objectId = listed.ObjectId!.Value;

            SelectAsync(window, view, listed);
            view = SurfaceOf(window);

            var retire = ButtonOf(view, EngineeringCalculationView.RetireCaption);
            AssertUsable(window, retire, "the retire button");

            // --- the first press says what it will do, and does nothing --
            await ClickAsync(window, view, EngineeringCalculationView.RetireCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).StatusMessage.Contains("terminal state", StringComparison.Ordinal));

            view = SurfaceOf(window);

            // It names the state it will reach, says it cannot be undone,
            // and says nothing is deleted — before anything happens.
            Assert.Contains("terminal state", view.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("cannot be moved back", view.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("Nothing is deleted", view.StatusMessage, StringComparison.Ordinal);

            // And the calculation is still exactly where it was.
            Assert.Contains(view.Calculations, c => c.RecordId == executed.CalculationRecordId && !c.IsRetired);
            Assert.False(EngineeringCalculationRegister.IsRetired(
                ((IHasLifecycle)(await DomainOf(host).Repository.FindAsync(objectId))!).Status));

            // --- the second press does it ------------------------------
            await ClickAsync(window, view, EngineeringCalculationView.RetireCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).Calculations.All(c => c.RecordId != executed.CalculationRecordId));

            view = SurfaceOf(window);

            // Out of the active list ...
            Assert.DoesNotContain(view.Calculations, c => c.RecordId == executed.CalculationRecordId);

            // ... and the surface says plainly that nothing was deleted.
            Assert.Contains("Nothing was deleted", view.StatusMessage, StringComparison.Ordinal);

            // The governed object is still held, in a retained terminal
            // lifecycle state — never soft-deleted, which the platform has
            // no undo for.
            var domain = DomainOf(host);
            var retired = await domain.Repository.FindAsync(objectId);
            Assert.NotNull(retired);
            Assert.False(((IDeletable)retired!).IsDeleted);
            Assert.True(EngineeringCalculationRegister.IsRetired(((IHasLifecycle)retired!).Status));

            // The record it produced is untouched.
            var engine = (ICalculationEngine)host.Services!.GetService(typeof(ICalculationEngine));
            Assert.NotNull(await engine.FindRecordAsync<BracketSectionCheckResult>(executed.CalculationRecordId));

            // --- and the engineer can still get to it -------------------
            var showRetired = CheckBoxOf(view, EngineeringCalculationView.ShowRetiredCaption);
            AssertUsable(window, showRetired, "the show-retired box");

            showRetired.IsChecked = true;
            await RenderUntilAsync(window, () => SurfaceOf(window).Calculations.Any(c => c.RecordId == executed.CalculationRecordId));

            view = SurfaceOf(window);
            var back = view.Calculations.Single(c => c.RecordId == executed.CalculationRecordId);
            Assert.True(back.IsRetired);
            Assert.Contains("retired", back.Label, StringComparison.OrdinalIgnoreCase);

            // Opening it still works, and is still read-only.
            SelectAsync(window, view, back);
            await ClickAsync(window, SurfaceOf(window), EngineeringCalculationView.OpenCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).IsReadOnly);

            view = SurfaceOf(window);
            Assert.True(view.IsReadOnly);
            Assert.Equal(executed.CalculationRecordId, view.DisplayedOutcome!.CalculationRecordId);
            Assert.False(ButtonOf(view, EngineeringCalculationView.CalculateCaption).IsEnabled);

            // Retiring it twice is not offered at all.
            SelectAsync(window, SurfaceOf(window), back);
            Assert.False(ButtonOf(SurfaceOf(window), EngineeringCalculationView.RetireCaption).IsEnabled);
        });
    }

    [AvaloniaFact]
    public async Task ARecordNobodyNamedOffersNoRenameOrRetire_AndSaysWhy()
    {
        await InWorkspaceAsync(async (host, window, view) =>
        {
            // The sample modules in this test assembly execute calculations
            // while initialising, and nothing names those. That is exactly
            // the shape of every record recorded before naming existed, so
            // it is the honest fixture for this behaviour — and if this
            // assembly ever stops carrying such a record, the test says so
            // rather than passing vacuously.
            view = SurfaceOf(window);
            var unnamed = view.Calculations.FirstOrDefault(c => !c.IsNamed);
            Assert.True(unnamed is not null, "Expected at least one recorded calculation with no governed object behind it.");

            SelectAsync(window, view, unnamed!);
            view = SurfaceOf(window);

            // Offered nothing it cannot do ...
            Assert.False(ButtonOf(view, EngineeringCalculationView.RenameCaption).IsEnabled);
            Assert.False(ButtonOf(view, EngineeringCalculationView.RetireCaption).IsEnabled);
            Assert.False(TextBoxOf(view, "New name for the selected calculation").IsEnabled);

            // ... and told why, on screen.
            AssertRendered(window, view, "there is no name to change");

            // Opening it is still offered, because opening it still works.
            Assert.True(ButtonOf(view, EngineeringCalculationView.OpenCaption).IsEnabled);
        });
    }

    [AvaloniaFact]
    public async Task ACalculationCreatedInsideAProjectBelongsToIt_ThroughTheParentEdgeEveryDisciplineUses()
    {
        await InWorkspaceAsync(async (host, window, view) =>
        {
            var project = await host.ProjectDirectory!.CreateAsync("P-0091", "Lug Bracket Redesign", "Follow-on usability fixture.");
            await host.ProjectContext!.OpenAsync(project.Id);

            var executed = await RunNamedCheckAsync(window, view, "Lug bracket — section check", area: "60");
            view = SurfaceOf(window);

            var listed = view.Calculations.Single(c => c.RecordId == executed.CalculationRecordId);

            // The list says where it lives, without a folder anywhere.
            Assert.Equal("Lug Bracket Redesign", listed.ProjectLabel);
            Assert.Contains("Lug Bracket Redesign", listed.Label, StringComparison.Ordinal);

            // And it is the platform's own parent edge that says so — the
            // same one the project directory reads for every other kind of
            // object, not a membership list of this surface's invention.
            var contents = await host.ProjectDirectory!.ListProjectContentsAsync(project.Id);
            Assert.Contains(listed.ObjectId!.Value, contents);
        });
    }

    [AvaloniaFact]
    public async Task ANamedCalculationSurvivesARestart_WithItsNameAndItsEvidenceIntact()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid recordId;
        Guid objectId;

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            SignIn(first);

            var window = new MainWindow(first) { Width = 1600, Height = 1000 };
            window.Show();
            await OpenFromTheRailAsync(first, window);

            var executed = await RunNamedCheckAsync(window, SurfaceOf(window), "Bracket check — survives a restart", area: "60");
            var listed = SurfaceOf(window).Calculations.Single(c => c.RecordId == executed.CalculationRecordId);

            recordId = listed.RecordId;
            objectId = listed.ObjectId!.Value;

            await first.ShutdownAsync();
        }
        finally
        {
            await first.DisposeAsync();
        }

        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            SignIn(second);

            var window = new MainWindow(second) { Width = 1600, Height = 1000 };
            window.Show();
            await OpenFromTheRailAsync(second, window);

            var view = SurfaceOf(window);
            var listed = view.Calculations.Single(c => c.RecordId == recordId);

            // The name came back, on the same governed object, still
            // pointing at the same record.
            Assert.Equal("Bracket check — survives a restart", listed.Title);
            Assert.Equal(objectId, listed.ObjectId);
            Assert.True(listed.IsNamed);
            Assert.False(listed.IsRetired);

            // And renaming it after the restart still works, which is the
            // proof that what came back is a live governed object and not
            // a read-only echo of one.
            SelectAsync(window, view, listed);
            TextBoxOf(SurfaceOf(window), "New name for the selected calculation").Text = "Bracket check — renamed after restart";
            await ClickAsync(window, SurfaceOf(window), EngineeringCalculationView.RenameCaption);
            await RenderUntilAsync(window, () =>
                SurfaceOf(window).Calculations.Any(c => c.RecordId == recordId && c.Title == "Bracket check — renamed after restart"));

            Assert.Equal(
                "Bracket check — renamed after restart",
                SurfaceOf(window).Calculations.Single(c => c.RecordId == recordId).Title);

            await second.ShutdownAsync();
        }
        finally
        {
            await second.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ARetiredCalculationRecoveredOnRelaunch_SaysItIsRetired_RatherThanPresentingItAsCurrent()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            SignIn(first);

            var window = new MainWindow(first) { Width = 1600, Height = 1000 };
            window.Show();
            await OpenFromTheRailAsync(first, window);

            var executed = await RunNamedCheckAsync(window, SurfaceOf(window), "Bracket check — retired before relaunch", area: "60");
            var listed = SurfaceOf(window).Calculations.Single(c => c.RecordId == executed.CalculationRecordId);

            SelectAsync(window, SurfaceOf(window), listed);

            // Two presses: the first describes, the second acts.
            await ClickAsync(window, SurfaceOf(window), EngineeringCalculationView.RetireCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).StatusMessage.Contains("terminal state", StringComparison.Ordinal));
            await ClickAsync(window, SurfaceOf(window), EngineeringCalculationView.RetireCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).Calculations.All(c => c.RecordId != executed.CalculationRecordId));

            await first.ShutdownAsync();
        }
        finally
        {
            await first.DisposeAsync();
        }

        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            SignIn(second);

            var window = new MainWindow(second) { Width = 1600, Height = 1000 };
            window.Show();
            await OpenFromTheRailAsync(second, window);

            // The last calculation is still recovered — the record is the
            // authority and nothing was deleted — but the surface says
            // plainly that it has been retired, rather than presenting it
            // as the calculation currently being worked on while the list
            // beside it does not contain it.
            await RenderUntilAsync(window, () => SurfaceOf(window).StatusMessage.Contains("Recovered the calculation", StringComparison.Ordinal));

            var view = SurfaceOf(window);

            Assert.True(view.IsReadOnly);
            Assert.Contains("Recovered the calculation", view.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("has since been retired", view.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("nothing was deleted", view.StatusMessage, StringComparison.Ordinal);
            Assert.DoesNotContain(view.Calculations, c => c.RecordId == view.DisplayedOutcome!.CalculationRecordId);

            // And it is shown under its own name. The list is filtered, so
            // a retired calculation is not in it — reading the name from
            // there alone would leave the box blank for a calculation that
            // has one.
            Assert.Equal("Bracket check — retired before relaunch", TextBoxOf(view, "Calculation name").Text);

            await second.ShutdownAsync();
        }
        finally
        {
            await second.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ArmingARetirementAndThenDoingSomethingElse_DisarmsIt_SoTheNextSinglePressCannotRetire()
    {
        await InWorkspaceAsync(async (host, window, view) =>
        {
            var executed = await RunNamedCheckAsync(window, view, "Bracket check — armed then abandoned", area: "60");
            view = SurfaceOf(window);

            var listed = view.Calculations.Single(c => c.RecordId == executed.CalculationRecordId);
            var objectId = listed.ObjectId!.Value;

            SelectAsync(window, view, listed);

            // Arm the confirmation.
            await ClickAsync(window, SurfaceOf(window), EngineeringCalculationView.RetireCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).StatusMessage.Contains("terminal state", StringComparison.Ordinal));

            // Now do something else. The warning leaves the screen — the
            // status line is the whole warning, there is no dialog — so the
            // confirmation must leave with it.
            await ClickAsync(window, SurfaceOf(window), EngineeringCalculationView.OpenCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).StatusMessage.Contains("Opened the calculation", StringComparison.Ordinal));

            Assert.DoesNotContain("terminal state", SurfaceOf(window).StatusMessage, StringComparison.Ordinal);

            // The next single press must describe again, not retire.
            await ClickAsync(window, SurfaceOf(window), EngineeringCalculationView.RetireCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).StatusMessage.Contains("terminal state", StringComparison.Ordinal));

            Assert.Contains(SurfaceOf(window).Calculations, c => c.RecordId == executed.CalculationRecordId && !c.IsRetired);
            Assert.False(EngineeringCalculationRegister.IsRetired(
                ((IHasLifecycle)(await DomainOf(host).Repository.FindAsync(objectId))!).Status));
        });
    }

    // ---- harness ----

    private static async Task InWorkspaceAsync(Func<WorkspaceHost, MainWindow, EngineeringCalculationView, Task> body)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            SignIn(host);

            var window = new MainWindow(host) { Width = 1600, Height = 1000 };
            window.Show();

            await OpenFromTheRailAsync(host, window);
            await body(host, window, SurfaceOf(window));
            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    private static void SignIn(WorkspaceHost host)
    {
        var principals = (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
        ((CurrentPrincipalAccessor)principals).SetCurrent(new PlatformPrincipal(new PlatformIdentity(EngineerId, EngineerId), []));
    }

    private static EngineeringDomainContext DomainOf(WorkspaceHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

    /// <summary>
    /// Runs the one check this workspace can drive, under a name the
    /// engineer chose, releasing the reference material first if it is not
    /// already released.
    /// </summary>
    private static async Task<BracketCalculationOutcome> RunNamedCheckAsync(
        MainWindow window, EngineeringCalculationView view, string name, string area)
    {
        view = SurfaceOf(window);

        if (!view.Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6))
        {
            await ClickAsync(window, view, EngineeringCalculationView.PopulateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6));
            view = SurfaceOf(window);
        }

        if (!view.Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6 && m.IsUsableForEngineering))
        {
            var picker = PickerOf(view);
            AssertUsable(window, picker, "the reference library list");
            picker.SelectedItem = view.Materials.Single(m => m.RecordId == MaterialSeed.Aluminium6082T6);

            foreach (var (automationName, text) in new[]
            {
                ("Source consulted", "Aalco 6082-T6 extrusions datasheet"),
                ("Release rationale", "Required for the bracket section check."),
            })
            {
                var box = TextBoxOf(view, automationName);
                AssertUsable(window, box, $"the '{automationName}' box");
                box.Text = text;
            }

            await ClickAsync(window, view, EngineeringCalculationView.ReleaseCaption);
            await RenderUntilAsync(window, () =>
                SurfaceOf(window).Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6 && m.IsUsableForEngineering));
            view = SurfaceOf(window);
        }

        // The single canonical New Calculation path — the same one an
        // engineer takes, never a second entry point of the test's own.
        await ClickAsync(window, view, EngineeringCalculationView.NewCalculationCaption);
        await RenderUntilAsync(window, () => !SurfaceOf(window).IsReadOnly);
        view = SurfaceOf(window);

        var materials = PickerOf(view);
        AssertUsable(window, materials, "the reference library list");
        materials.SelectedItem = view.Materials.Single(m => m.RecordId == MaterialSeed.Aluminium6082T6);

        var nameBox = TextBoxOf(view, "Calculation name");
        AssertUsable(window, nameBox, "the calculation name box");
        nameBox.Text = name;

        // Every input is checked usable before it is driven — Avalonia will
        // happily accept .Text on a disabled TextBox, so a regression that
        // left these disabled after a read-only view would otherwise be
        // driven successfully and never caught.
        foreach (var (automationName, text) in new[]
        {
            ("Axial load in kilonewtons", "12"),
            ("Section area in square millimetres", area),
            ("Member length in millimetres", "150"),
            ("Mass limit in grams", "50"),
        })
        {
            var box = TextBoxOf(view, automationName);
            AssertUsable(window, box, $"the '{automationName}' box");
            box.Text = text;
        }

        // Which record was on screen before this run — so the wait below
        // is for THIS calculation's record and not satisfied instantly by
        // the previous one still being displayed.
        var previous = view.DisplayedOutcome?.CalculationRecordId;

        await ClickAsync(window, view, EngineeringCalculationView.CalculateCaption);
        await RenderUntilAsync(window, () =>
            SurfaceOf(window).DisplayedOutcome is { Performed: true } shown && shown.CalculationRecordId != previous);

        var outcome = SurfaceOf(window).DisplayedOutcome!;

        Assert.True(outcome.Performed, $"The calculation did not run. {string.Join(" ", outcome.Problems)} {outcome.RefusalReason}");
        Assert.NotEqual(previous, outcome.CalculationRecordId);

        // The list is not read until the calculation it must contain is in
        // it — a bounded wait on real state, never a fixed delay.
        await RenderUntilAsync(window, () =>
            SurfaceOf(window).Calculations.Any(c => c.RecordId == outcome.CalculationRecordId && c.IsNamed));

        return outcome;
    }

    private static void SelectAsync(MainWindow window, EngineeringCalculationView view, CalculationListEntry entry)
    {
        var list = CalculationListOf(view);
        AssertUsable(window, list, "the calculations list");

        list.SelectedItem = view.Calculations.Single(c => c.RecordId == entry.RecordId);
        Dispatcher.UIThread.RunJobs();
        LayOut(window);
    }

    private static async Task OpenFromTheRailAsync(WorkspaceHost host, MainWindow window)
    {
        LayOut(window);

        var rail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Distinct().Single();
        var entry = rail.GetLogicalDescendants().OfType<Button>().Distinct()
            .Single(b => string.Equals(AutomationProperties.GetName(b), RailEntry, StringComparison.Ordinal));

        AssertUsable(window, entry, $"the '{RailEntry}' rail entry");
        entry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var deadline = DesktopTestHelpers.Deadline(5);
        while (host.ShellNavigator!.Current.Area != ShellArea.EngineeringCalculation && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(ShellArea.EngineeringCalculation, host.ShellNavigator!.Current.Area);
        await window.RenderCurrentModuleAsync();
        LayOut(window);
    }

    private static EngineeringCalculationView SurfaceOf(MainWindow window) =>
        window.GetLogicalDescendants().OfType<EngineeringCalculationView>().Distinct().Single();

    private static ListBox PickerOf(EngineeringCalculationView view) =>
        view.GetLogicalDescendants().OfType<ListBox>().Distinct()
            .Single(l => string.Equals(AutomationProperties.GetName(l), "Reference library", StringComparison.Ordinal));

    private static ListBox CalculationListOf(EngineeringCalculationView view) =>
        view.GetLogicalDescendants().OfType<ListBox>().Distinct()
            .Single(l => string.Equals(AutomationProperties.GetName(l), "Existing calculations", StringComparison.Ordinal));

    private static Button ButtonOf(EngineeringCalculationView view, string caption) =>
        view.GetLogicalDescendants().OfType<Button>().Distinct()
            .Single(b => string.Equals(b.Content?.ToString(), caption, StringComparison.Ordinal));

    private static CheckBox CheckBoxOf(EngineeringCalculationView view, string caption) =>
        view.GetLogicalDescendants().OfType<CheckBox>().Distinct()
            .Single(b => string.Equals(b.Content?.ToString(), caption, StringComparison.Ordinal));

    private static TextBox TextBoxOf(EngineeringCalculationView view, string automationName) =>
        view.GetLogicalDescendants().OfType<TextBox>().Distinct()
            .Single(b => string.Equals(AutomationProperties.GetName(b), automationName, StringComparison.Ordinal));

    /// <summary>Asserts a control is something a person could actually use.</summary>
    private static void AssertUsable(MainWindow window, Control control, string what)
    {
        LayOut(window);
        Assert.True(control.IsVisible, $"{what} exists but IsVisible is false.");
        Assert.True(control.IsEnabled, $"{what} is visible but disabled.");
        DesktopTestHelpers.AssertPlaced(control, what);

        // And the column it lives in is placed, too: a usable control inside
        // a column drawn over another column is not usable (`WP 17.0A`).
        foreach (var column in control.GetLogicalAncestors().OfType<ScrollViewer>())
            DesktopTestHelpers.AssertNoSiblingOverlap(column, $"the column holding {what}");
    }

    private static async Task ClickAsync(MainWindow window, Control surface, string caption)
    {
        LayOut(window);

        var button = surface.GetLogicalDescendants().OfType<Button>().Distinct()
            .FirstOrDefault(b => string.Equals(b.Content?.ToString(), caption, StringComparison.Ordinal));

        Assert.True(button is not null, $"No '{caption}' button. Present: {string.Join(", ", surface.GetLogicalDescendants().OfType<Button>().Select(b => b.Content?.ToString()))}");
        AssertUsable(window, button!, $"the '{caption}' button");

        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
    }

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = DesktopTestHelpers.Deadline(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        LayOut(window);
    }

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Avalonia.Size(1600, 1000));
            window.Arrange(new Avalonia.Rect(0, 0, 1600, 1000));
        }
    }

    /// <summary>Asserts <paramref name="fragment"/> is on screen, in a control a person can see.</summary>
    private static void AssertRendered(MainWindow window, Control surface, string fragment)
    {
        LayOut(window);

        var shown = surface.GetLogicalDescendants().OfType<Control>()
            .Where(c => c.IsVisible && c.Bounds.Width > 0 && c.Bounds.Height > 0)
            .Select(c => c switch
            {
                TextBlock text => text.Text,
                ContentControl content => content.Content?.ToString(),
                _ => null,
            })
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        Assert.True(
            shown.Any(t => t!.Contains(fragment, StringComparison.Ordinal)),
            $"'{fragment}' is not shown anywhere a person could see it. On screen: {string.Join(" | ", shown)}");
    }
}
