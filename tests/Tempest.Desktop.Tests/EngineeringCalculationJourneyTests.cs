using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.App.Engineering;
using Tempest.App.Shell;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The engineering calculation journey, driven end to end through the real
/// <see cref="MainWindow"/>: <b>launch → navigate to the calculation surface
/// → populate the governed library → try to calculate on unreleased data and
/// be refused → verify and release it → calculate → read the result and its
/// traceability → relaunch → recover the persisted result → revise the
/// reference and watch the historical result stay put.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here calls the workbench directly for the journey.</b> Every
/// step goes through a control a person actually uses — the rail the shell
/// renders, the picker they choose from, the boxes they type into, the
/// button they press. That is the whole point: the sibling
/// <see cref="BracketCalculationJourneyTests"/> proves the host composition
/// reaches the calculation and says so in its own header ("No view is added
/// and no navigation is changed"), and it would have passed against a
/// product with no calculation surface at all — which is exactly what
/// `TD-160` recorded.
/// </para>
/// <para>
/// <b>What this cannot prove.</b> A button "click" here is a synthetic
/// routed event, the only mechanism this suite has: it bypasses
/// hit-testing, z-order and overlays, so these tests show that a handler
/// runs and produces the right result, not that the button is reachable by
/// a real pointer at a real coordinate. Typing is
/// <c>TextBox.Text = …</c> rather than keystrokes. And this is headless
/// Linux, not Windows. Human verification on Windows remains the only thing
/// that proves the last of those.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class EngineeringCalculationJourneyTests
{
    /// <summary>The rail entry an engineer looks for. Deliberately the words, not the enum name.</summary>
    private const string CalculationsRailEntry = "Engineering Calculations";

    private const string EngineerId = "desktop-journey-engineer";
    private const string SourceConsulted = "Aalco 6082-T6 extrusions datasheet, mechanical and physical property tables";
    private const string ReleaseRationale = "Required for the bracket section check.";

    [AvaloniaFact]
    public async Task Journey_Navigate_Populate_Refused_Release_Calculate_Inspect_Relaunch_Recover()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid recordId;

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            SignIn(first);
            var window = OpenWindow(first);

            // --- 1. Discover and open it from the rail, as a person does -
            await OpenCalculationsFromTheRailAsync(first, window);
            var view = SurfaceOf(window);
            Assert.NotNull(view);

            // A real surface, not the "not yet implemented" card every
            // Declared module gets.
            Assert.Empty(window.GetLogicalDescendants().OfType<DeclaredCapabilityView>());

            // --- 2. Real reference data loads ---------------------------
            // NOT "the library is empty": this test process references
            // Tempest.Samples, whose MaterialsSampleModule registers its own
            // demonstration alloys at start-up, and the shipped Desktop does
            // not. Asserting emptiness here would assert the harness rather
            // than the product. What IS true of both compositions is that
            // the shipped seed corpus is absent until somebody asks for it.
            Assert.DoesNotContain(view.Materials, m => m.RecordId == MaterialSeed.Aluminium6082T6);

            await ClickAsync(window, view, EngineeringCalculationView.PopulateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6));

            view = SurfaceOf(window);
            Assert.Contains(view.Materials, m => m.RecordId == MaterialSeed.Aluminium6082T6);
            Assert.All(view.Materials, m => Assert.Equal(ReferenceValidationState.Draft, m.ValidationState));

            // --- 3. Selecting a reference changes the governed input -----
            var picker = PickerOf(view);
            var aluminium = view.Materials.Single(m => m.RecordId == MaterialSeed.Aluminium6082T6);
            picker.SelectedItem = aluminium;

            Assert.Equal(MaterialSeed.Aluminium6082T6, view.SelectedMaterial!.RecordId);
            Assert.Equal(MaterialSeed.Aluminium6082T6, view.CurrentInputs.MaterialRecordId);

            // --- 4. An unreleased reference is refused, visibly ----------
            EnterInputs(view, load: "12", area: "60", length: "150", massLimit: "50");
            await ClickAsync(window, view, EngineeringCalculationView.CalculateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).DisplayedOutcome is not null);

            view = SurfaceOf(window);
            var refused = view.DisplayedOutcome!;
            Assert.False(refused.Performed);
            Assert.NotNull(refused.RefusalReason);
            Assert.Contains("not Released", refused.RefusalReason!, StringComparison.Ordinal);

            // The refusal is on screen, not just in a property.
            AssertRenderedContains(window, view, "not Released");

            // --- 5. The governed review, in the engineer's own words -----
            EnterText(view, "Source consulted", SourceConsulted);
            EnterText(view, "Release rationale", ReleaseRationale);
            await ClickAsync(window, view, EngineeringCalculationView.ReleaseCaption);
            await RenderUntilAsync(window, () =>
                SurfaceOf(window).Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6 && m.IsUsableForEngineering));

            view = SurfaceOf(window);
            var released = view.Materials.Single(m => m.RecordId == MaterialSeed.Aluminium6082T6);
            Assert.Equal(ReferenceValidationState.Released, released.ValidationState);

            // Attributed to whoever was signed in, and to nobody else.
            Assert.Equal(EngineerId, released.ReviewerPrincipalId);
            Assert.NotNull(released.VerificationDate);

            // --- 6. The calculation, through the real button ------------
            EnterInputs(view, load: "12", area: "60", length: "150", massLimit: "50");
            await ClickAsync(window, view, EngineeringCalculationView.CalculateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).DisplayedOutcome is { Performed: true });

            view = SurfaceOf(window);
            var outcome = view.DisplayedOutcome!;
            recordId = outcome.CalculationRecordId;

            // --- 7. The numbers, and pass/fail --------------------------
            Assert.Equal("200 MPa", outcome.AppliedStress);
            Assert.Equal("260 MPa", outcome.AllowableStress);
            Assert.Equal("0.3", outcome.StressMargin);
            Assert.Equal("24.3 g", outcome.EstimatedMass);
            Assert.Equal("50 g", outcome.MassLimit);
            Assert.True(outcome.StressCriterionMet);
            Assert.True(outcome.MassCriterionMet);
            Assert.True(outcome.MeetsCriteria);
            Assert.Equal("Meets criteria", outcome.OutcomeLabel);

            // --- 8. Traceability is visible -----------------------------
            Assert.Equal(MaterialSeed.Aluminium6082T6, outcome.MaterialRecordId);
            Assert.Equal("Materials", outcome.MaterialLibrary);
            Assert.Equal(released.RevisionNumber, outcome.PinnedRevision);
            Assert.Equal(EngineerId, outcome.ReviewerPrincipalId);
            Assert.Contains("Aalco", outcome.Provenance, StringComparison.Ordinal);
            Assert.NotEqual(Guid.Empty, outcome.CalculationRecordId);

            // And on screen, at a real laid-out size — not merely present
            // in the logical tree.
            AssertRenderedContains(window, view, "200 MPa");
            AssertRenderedContains(window, view, "24.3 g");
            AssertRenderedContains(window, view, "Meets criteria");
            AssertRenderedContains(window, view, MaterialSeed.Aluminium6082T6);

            await first.ShutdownAsync();
        }
        finally
        {
            await first.DisposeAsync();
        }

        // --- 9. Relaunch, and recover the persisted result --------------
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            SignIn(second);
            var window = OpenWindow(second);

            await OpenCalculationsFromTheRailAsync(second, window);
            await RenderUntilAsync(window, () => SurfaceOf(window).DisplayedOutcome is { Performed: true });

            var view = SurfaceOf(window);
            var recovered = view.DisplayedOutcome!;

            Assert.Equal(recordId, recovered.CalculationRecordId);
            Assert.Equal("200 MPa", recovered.AppliedStress);
            Assert.Equal("0.3", recovered.StressMargin);
            Assert.Equal("24.3 g", recovered.EstimatedMass);
            Assert.True(recovered.MeetsCriteria);

            // The figures the engineer typed are back in the boxes too.
            Assert.Equal("12", view.CurrentInputs.LoadKilonewtons);
            Assert.Equal("150", view.CurrentInputs.MemberLengthMillimetres);

            await second.ShutdownAsync();
        }
        finally
        {
            await second.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RevisingTheReferenceAfterwards_DoesNotAlterTheHistoricalResult()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            SignIn(host);
            var window = OpenWindow(host);

            var outcome = await RunTheKnownCheckAsync(host, window);
            var pinnedRevision = outcome.PinnedRevision;
            Assert.Equal("260 MPa", outcome.AllowableStress);

            // Move the reference on behind the calculation's back — through
            // the only route the lifecycle permits for a Released record.
            // Revising one is refused outright ("Register the corrected
            // record and supersede this one instead"), which is the
            // governance working, so this supersedes it the way an engineer
            // correcting a datasheet reading would have to.
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog));
            var record = (await materials.FindAsync(MaterialSeed.Aluminium6082T6))!;
            var corrected = record.Definition.Properties.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            corrected[MaterialPropertyNames.YieldStrength] = new ReferenceQuantityValue(
                new Quantity<Pressure>(200.0, PressureUnits.Megapascal),
                ReferenceValueOrigin.EngineeringReference,
                "FICTIONAL TEST FIXTURE: a later, lower allowable stress.");

            // A distinct designation, because the superseded record still
            // holds its own secondary key — recorded as `TD-156`.
            await materials.RegisterAsync(
                "mat-6082-t6-rev-b",
                record.Definition with { Designation = "6082 (rev B)", Properties = corrected },
                record.Provenance with
                {
                    VerificationStatus = ReferenceVerificationStatus.NotVerified,
                    ReviewerPrincipalId = null,
                    VerificationDate = null,
                });

            await materials.SupersedeAsync(
                MaterialSeed.Aluminium6082T6, "mat-6082-t6-rev-b", "Allowable stress corrected.");

            // Re-enter the surface and recover the stored calculation.
            await host.ShellNavigator!.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            await host.ShellNavigator.GoToModuleAsync(ShellArea.EngineeringCalculation);
            await window.RenderCurrentModuleAsync();

            var view = SurfaceOf(window);
            var afterwards = view.DisplayedOutcome!;

            // The historical result is untouched: it stands on the revision
            // it was pinned to, not on today's data.
            Assert.Equal(outcome.CalculationRecordId, afterwards.CalculationRecordId);
            Assert.Equal("260 MPa", afterwards.AllowableStress);
            Assert.Equal("0.3", afterwards.StressMargin);
            Assert.Equal(pinnedRevision, afterwards.PinnedRevision);

            // And the surface says so, rather than letting the reader
            // assume the result is current: the reference has moved on, and
            // it now reads Superseded.
            Assert.True(afterwards.MaterialHasMovedOn);
            Assert.NotEqual(pinnedRevision, afterwards.CurrentRevision);
            Assert.Equal(ReferenceValidationState.Superseded.ToString(), afterwards.MaterialStateNow);

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task IncompleteAndInvalidInput_IsRejectedVisibly_AndNothingIsCalculated()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            SignIn(host);
            var window = OpenWindow(host);

            await OpenCalculationsFromTheRailAsync(host, window);

            var view = SurfaceOf(window);
            await ClickAsync(window, view, EngineeringCalculationView.PopulateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6));
            view = SurfaceOf(window);

            EnterInputs(view, load: "not a number", area: string.Empty, length: "-5", massLimit: "0");
            await ClickAsync(window, view, EngineeringCalculationView.CalculateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).DisplayedOutcome is not null);

            view = SurfaceOf(window);
            var outcome = view.DisplayedOutcome!;

            Assert.False(outcome.Performed);
            Assert.Equal(Guid.Empty, outcome.CalculationRecordId);

            // Every problem, not just the first — an engineer correcting
            // four fields one round-trip at a time is doing the form's work.
            Assert.Equal(4, outcome.Problems.Count);
            Assert.Contains(outcome.Problems, p => p.Contains("Load must be a number", StringComparison.Ordinal));
            Assert.Contains(outcome.Problems, p => p.Contains("Section area is required", StringComparison.Ordinal));
            Assert.Contains(outcome.Problems, p => p.Contains("Member length must be greater than zero", StringComparison.Ordinal));
            Assert.Contains(outcome.Problems, p => p.Contains("Mass limit must be greater than zero", StringComparison.Ordinal));

            AssertRenderedContains(window, view, "Load must be a number, in kN. 'not a number' is not.");

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AReleaseAttemptedWithNobodySignedIn_IsRefused_AndTheRecordStaysDraft()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = OpenWindow(host);

            await OpenCalculationsFromTheRailAsync(host, window);

            var view = SurfaceOf(window);
            await ClickAsync(window, view, EngineeringCalculationView.PopulateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6));
            view = SurfaceOf(window);

            PickerOf(view).SelectedItem = view.Materials.Single(m => m.RecordId == MaterialSeed.Aluminium6082T6);
            EnterText(view, "Source consulted", SourceConsulted);
            EnterText(view, "Release rationale", ReleaseRationale);

            // Nobody signed in — the application refuses to attribute a
            // review to an unknown principal.
            var principals = (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
            ((CurrentPrincipalAccessor)principals).SetCurrent(null);

            await ClickAsync(window, view, EngineeringCalculationView.ReleaseCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).StatusMessage.Length > 0);

            view = SurfaceOf(window);
            Assert.Contains("no principal is signed in", view.StatusMessage, StringComparison.Ordinal);

            var materials = (IMaterialCatalog)host.Services.GetService(typeof(IMaterialCatalog));
            var record = await materials.FindAsync(MaterialSeed.Aluminium6082T6);
            Assert.Equal(ReferenceValidationState.Draft, record!.ValidationState);

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ThePopulateAction_IsVisibleAtNonZeroBounds_WheneverTheSurfaceIsOpen()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            SignIn(host);
            var window = OpenWindow(host);

            await OpenCalculationsFromTheRailAsync(host, window);

            var view = SurfaceOf(window);
            var button = view.GetLogicalDescendants().OfType<Button>().Distinct()
                .FirstOrDefault(b => string.Equals(b.Content?.ToString(), EngineeringCalculationView.PopulateCaption, StringComparison.Ordinal));

            Assert.True(button is not null, "There is no Populate button at all.");
            Assert.True(button!.IsVisible, $"The Populate button exists but IsVisible=false. Materials in library at entry: {view.Materials.Count}.");

            for (var pass = 0; pass < 2; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.Measure(new Avalonia.Size(1400, 900));
                window.Arrange(new Avalonia.Rect(0, 0, 1400, 900));
            }

            Assert.True(button.Bounds.Width > 0 && button.Bounds.Height > 0, $"The Populate button rendered at {button.Bounds}.");

            // And it stays visible after the library is no longer empty:
            // hiding it once it had been used is the defect this test was
            // written to reproduce.
            await ClickAsync(window, view, EngineeringCalculationView.PopulateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6));

            LayOut(window);
            view = SurfaceOf(window);
            var again = view.GetLogicalDescendants().OfType<Button>().Distinct()
                .First(b => string.Equals(b.Content?.ToString(), EngineeringCalculationView.PopulateCaption, StringComparison.Ordinal));

            Assert.True(again.IsVisible, "The Populate button disappeared once the library held records.");
            Assert.True(again.Bounds.Width > 0 && again.Bounds.Height > 0, $"The Populate button rendered at {again.Bounds} after populating.");

            // The real catalogue, not the view's copy: pressing the button
            // ran ReferenceSeedService against IMaterialCatalog, and the
            // records are Draft, which is the only state seeding produces.
            var materials = (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog));
            var seeded = await materials.FindAsync(MaterialSeed.Aluminium6082T6);

            Assert.NotNull(seeded);
            Assert.Equal(ReferenceValidationState.Draft, seeded!.ValidationState);
            Assert.False(seeded.Provenance.IsVerified);
            Assert.Null(seeded.Provenance.ReviewerPrincipalId);

            var everySeededRecord = await materials.ListAsync();
            Assert.All(
                MaterialSeed.Instance.Records.Select(r => r.RecordId),
                id => Assert.Contains(everySeededRecord, m => m.Id == id));

            // The engineer is told what happened, on screen.
            Assert.Contains("Added", view.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("Draft", view.StatusMessage, StringComparison.Ordinal);
            AssertRenderedContains(window, view, "Added");

            // And pressing it again is harmless and says so, rather than
            // duplicating records or overwriting one somebody corrected.
            await ClickAsync(window, view, EngineeringCalculationView.PopulateCaption);
            await RenderUntilAsync(window, () => SurfaceOf(window).StatusMessage.Contains("already held", StringComparison.Ordinal));

            view = SurfaceOf(window);
            Assert.Contains("already held", view.StatusMessage, StringComparison.Ordinal);
            Assert.Equal(everySeededRecord.Count, (await materials.ListAsync()).Count);

            // The populated material is then selectable by the calculation
            // workflow — the point of populating at all.
            PickerOf(view).SelectedItem = view.Materials.Single(m => m.RecordId == MaterialSeed.Aluminium6082T6);
            Assert.Equal(MaterialSeed.Aluminium6082T6, view.CurrentInputs.MaterialRecordId);

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    // ---- the journey, reused by the tests that need a result to exist ----

    private static async Task<BracketCalculationOutcome> RunTheKnownCheckAsync(WorkspaceHost host, MainWindow window)
    {
        await OpenCalculationsFromTheRailAsync(host, window);

        var view = SurfaceOf(window);
        await ClickAsync(window, view, EngineeringCalculationView.PopulateCaption);
        await RenderUntilAsync(window, () => SurfaceOf(window).Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6));

        view = SurfaceOf(window);
        PickerOf(view).SelectedItem = view.Materials.Single(m => m.RecordId == MaterialSeed.Aluminium6082T6);
        EnterText(view, "Source consulted", SourceConsulted);
        EnterText(view, "Release rationale", ReleaseRationale);

        await ClickAsync(window, view, EngineeringCalculationView.ReleaseCaption);
        await RenderUntilAsync(window, () =>
            SurfaceOf(window).Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6 && m.IsUsableForEngineering));

        view = SurfaceOf(window);
        EnterInputs(view, load: "12", area: "60", length: "150", massLimit: "50");
        await ClickAsync(window, view, EngineeringCalculationView.CalculateCaption);
        await RenderUntilAsync(window, () => SurfaceOf(window).DisplayedOutcome is { Performed: true });

        return SurfaceOf(window).DisplayedOutcome!;
    }

    // ---- helpers, copied in shape from ProjectTaskAcceptanceTests ----

    /// <remarks>
    /// Deduplicated by identity: a shown window materialises content through
    /// a presenter as well, so one surface can appear twice in the logical
    /// tree. See <c>ProjectTaskAcceptanceTests</c>, which records the same.
    /// </remarks>
    private static EngineeringCalculationView SurfaceOf(MainWindow window) =>
        window.GetLogicalDescendants().OfType<EngineeringCalculationView>().Distinct().Single();

    /// <summary>The Reference Library list, located by the name a screen reader announces.</summary>
    private static ListBox PickerOf(EngineeringCalculationView view) =>
        view.GetLogicalDescendants().OfType<ListBox>().Distinct()
            .Single(l => string.Equals(AutomationProperties.GetName(l), "Reference library", StringComparison.Ordinal));

    /// <summary>The Calculations list, located the same way.</summary>
    private static ListBox CalculationListOf(EngineeringCalculationView view) =>
        view.GetLogicalDescendants().OfType<ListBox>().Distinct()
            .Single(l => string.Equals(AutomationProperties.GetName(l), "Existing calculations", StringComparison.Ordinal));

    private static void SignIn(WorkspaceHost host)
    {
        // A real launch signs in the OS account through
        // LocalSessionPrincipalSource; this pins a deterministic id so the
        // reviewer attribution can be asserted by name.
        var principals = (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
        ((CurrentPrincipalAccessor)principals).SetCurrent(
            new PlatformPrincipal(new PlatformIdentity(EngineerId, EngineerId), []));
    }

    private static void EnterInputs(EngineeringCalculationView view, string load, string area, string length, string massLimit)
    {
        EnterText(view, "Axial load in kilonewtons", load);
        EnterText(view, "Section area in square millimetres", area);
        EnterText(view, "Member length in millimetres", length);
        EnterText(view, "Mass limit in grams", massLimit);
    }

    /// <summary>Types into the box a person would, located by the name a screen reader would announce.</summary>
    private static void EnterText(EngineeringCalculationView view, string automationName, string text)
    {
        var box = view.GetLogicalDescendants().OfType<TextBox>().Distinct()
            .FirstOrDefault(b => string.Equals(AutomationProperties.GetName(b), automationName, StringComparison.Ordinal));

        Assert.True(box is not null, $"No box named '{automationName}'. Present: {string.Join(", ", view.GetLogicalDescendants().OfType<TextBox>().Select(AutomationProperties.GetName))}");
        box!.Text = text;
    }

    /// <summary>
    /// Clicks the button with <paramref name="caption"/>, exactly as a user
    /// would — and refuses to click one a user could not.
    /// </summary>
    /// <remarks>
    /// <b>The visibility and bounds assertions are the point.</b> A
    /// synthetic <c>Button.ClickEvent</c> reaches a control that is
    /// <c>IsVisible=false</c> or laid out at zero just as happily as one on
    /// screen, so a test using a bare click can pass against a product
    /// whose button nobody can find. That is not hypothetical: the first
    /// version of these tests clicked "Populate Material Library" while it
    /// was hidden, and the first manual review on Windows could not find
    /// that button at all. Every click in this file now goes through here.
    /// </remarks>
    private static async Task ClickAsync(MainWindow window, Control surface, string caption)
    {
        LayOut(window);

        var button = surface.GetLogicalDescendants().OfType<Button>().Distinct()
            .FirstOrDefault(b => string.Equals(b.Content?.ToString(), caption, StringComparison.Ordinal));

        Assert.True(button is not null, $"No '{caption}' button on this surface. Present: {string.Join(", ", surface.GetLogicalDescendants().OfType<Button>().Select(b => b.Content?.ToString()))}");
        Assert.True(button!.IsVisible, $"The '{caption}' button exists but IsVisible is false — a user cannot click it.");
        Assert.True(
            button.Bounds.Width > 0 && button.Bounds.Height > 0,
            $"The '{caption}' button rendered at {button.Bounds} — a user cannot click it.");

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
    }

    /// <summary>Runs a real layout pass, twice, so content added during the render is measured too.</summary>
    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Avalonia.Size(1400, 900));
            window.Arrange(new Avalonia.Rect(0, 0, 1400, 900));
        }
    }

    /// <summary>Opens the shell the way a launch does, at a real size so layout is real.</summary>
    private static MainWindow OpenWindow(WorkspaceHost host)
    {
        var window = new MainWindow(host) { Width = 1400, Height = 900 };
        window.Show();
        return window;
    }

    /// <summary>
    /// Reaches Engineering Calculations the way a person does: by finding
    /// the entry in the global navigation rail and clicking it.
    /// </summary>
    /// <remarks>
    /// Located by the name a screen reader announces, which
    /// <c>GlobalNavigationRail</c> sets from the module's own title — so
    /// this test fails if the rail entry is renamed to something an
    /// engineer would not look for.
    /// </remarks>
    private static async Task OpenCalculationsFromTheRailAsync(WorkspaceHost host, MainWindow window)
    {
        LayOut(window);

        var rail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Distinct().Single();
        var entry = rail.GetLogicalDescendants().OfType<Button>().Distinct()
            .FirstOrDefault(b => string.Equals(AutomationProperties.GetName(b), CalculationsRailEntry, StringComparison.Ordinal));

        Assert.True(entry is not null, $"The rail has no '{CalculationsRailEntry}' entry. Present: {string.Join(", ", rail.GetLogicalDescendants().OfType<Button>().Select(AutomationProperties.GetName))}");
        Assert.True(entry!.IsVisible, $"The '{CalculationsRailEntry}' rail entry exists but IsVisible is false.");
        Assert.True(entry.Bounds.Width > 0 && entry.Bounds.Height > 0, $"The '{CalculationsRailEntry}' rail entry rendered at {entry.Bounds}.");

        entry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // `TD-119`: the rail navigates on an asynchronous continuation.
        var deadline = DesktopTestHelpers.Deadline(5);
        while (host.ShellNavigator!.Current.Area != ShellArea.EngineeringCalculation && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(ShellArea.EngineeringCalculation, host.ShellNavigator!.Current.Area);
        await window.RenderCurrentModuleAsync();
    }

    /// <summary>Re-renders until <paramref name="condition"/> holds, or a deadline expires. `TD-119`: no fixed wait.</summary>
    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = DesktopTestHelpers.Deadline(5);
        while (true)
        {
            if (condition())
                return;

            if (DateTime.UtcNow >= deadline)
                return;

            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Asserts the text is genuinely on screen at a real laid-out size, not merely in the logical tree.</summary>
    private static void AssertRenderedContains(MainWindow window, Control surface, string fragment)
    {
        // A window that was never shown lays out at zero, so every bounds
        // assertion would pass or fail for the wrong reason. Shown once;
        // repeated calls are harmless.
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Avalonia.Size(1400, 900));
            window.Arrange(new Avalonia.Rect(0, 0, 1400, 900));
        }

        var block = surface.GetLogicalDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => (t.Text ?? string.Empty).Contains(fragment, StringComparison.Ordinal));

        Assert.True(block is not null, $"'{fragment}' is not on screen at all.");
        Assert.True(block!.Bounds.Width > 0 && block.Bounds.Height > 0, $"'{fragment}' rendered at {block.Bounds}.");
    }
}
