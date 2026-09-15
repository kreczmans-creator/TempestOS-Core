using System.Collections;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.UnitsAndQuantities;
using Tempest.Desktop.Views;
using Tempest.Desktop.Views.EngineeringAssets;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 21.2B` — the Engineering Assets area, driven end to end through the
/// real <see cref="MainWindow"/>: <b>enter Engineering → Modules →
/// Engineering Assets → fill the bracket verification form → Check → Record
/// → the artefact lists under Verification artefacts, Passed → open the
/// calculation pack's own Trace tab and see this run's own provenance.</b>
/// Closes `TD-165` ("the bracket verification artefact cannot be filled in
/// from the Desktop") and half of `TD-160` ("CalculationTrace... rendered
/// nowhere").
/// </summary>
/// <remarks>
/// A button "click" here is a synthetic routed event and typing is
/// <c>TextBox.Text = …</c>, the same limits every journey test in this
/// suite discloses (see <see cref="EngineeringCalculationJourneyTests"/>'s
/// own remarks) — this proves a handler runs and produces the right
/// result, not that a real pointer or keyboard reaches it.
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class EngineeringAssetsJourneyTests
{
    private const string EngineerId = "engineering-assets-journey-engineer";
    private const string MaterialRecordId = "mat-journey-6082";
    private const string PackRecordId = "cpk-journey-bracket";
    private const string ArtefactRecordId = "ver-journey-bracket";

    [AvaloniaFact]
    public async Task EnteringEngineeringAssets_ListsEveryRegisteredRecord_ByAutomationName()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            SignIn(host);
            await SeedAsync(host);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single().SelectNode("Engineering Assets");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<EngineeringAssetsView>().Any());
            LayOut(window);

            var assetsView = window.GetLogicalDescendants().OfType<EngineeringAssetsView>().Single();

            var tabItems = assetsView.GetLogicalDescendants().OfType<TabItem>().ToList();
            foreach (var name in new[] { "Calculation packs", "Templates", "Verification artefacts", "Engineering evidence", "Bracket verification" })
                Assert.Contains(tabItems, t => Equals(AutomationProperties.GetName(t), name));

            // The seeded calculation pack and verification artefact each
            // list, with a real Open button of their own.
            Assert.Contains(
                assetsView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains(PackRecordId, StringComparison.Ordinal));
            Assert.Contains(
                assetsView.GetLogicalDescendants().OfType<Button>(),
                b => (AutomationProperties.GetName(b) ?? string.Empty) == $"Open {PackRecordId}");

            assetsView.SelectTab("Verification artefacts");
            LayOut(window);
            Assert.Contains(
                assetsView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains(ArtefactRecordId, StringComparison.Ordinal));
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheBracketVerificationJourney_ChecksAndRecords_TheArtefactThenListsAndTraces()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            SignIn(host);
            await SeedAsync(host);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single().SelectNode("Engineering Assets");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<EngineeringAssetsView>().Any());
            LayOut(window);

            var assetsView = window.GetLogicalDescendants().OfType<EngineeringAssetsView>().Single();
            assetsView.SelectTab("Bracket verification");
            LayOut(window);

            // `GetLogicalDescendants` can revisit the same control more than
            // once for a `TabControl`'s own selected content (the same
            // quirk `EngineeringCalculationJourneyTests` already works
            // around) — distinct by reference, the same discipline
            // `AutomationNameCoverageTests` already establishes.
            List<ComboBox> Combos() => assetsView.GetLogicalDescendants().OfType<ComboBox>().Distinct().ToList();
            List<TextBox> Boxes() => assetsView.GetLogicalDescendants().OfType<TextBox>().Distinct().ToList();

            // --- Fill the check inputs, every one a quantity with a unit picker ---
            await RenderUntilAsync(window, () => Combos().Any(c => Equals(AutomationProperties.GetName(c), "Material") && HasItems(c)));
            SelectByPrefix(Combos().Single(c => Equals(AutomationProperties.GetName(c), "Material")), MaterialRecordId);

            // Every quantity field defaults its own unit picker to the
            // dimension's base SI unit (newtons, square metres, metres,
            // kilograms) — matching the Core demonstration's own figures
            // (`BracketEngineeringDemonstrationTests`) needs the same units
            // that test states them in.
            SetText(Boxes(), "Applied load", "12");
            SelectUnit(Combos(), "Applied load", "kN");
            SetText(Boxes(), "Section area", "60");
            SelectUnit(Combos(), "Section area", "mm²");
            SetText(Boxes(), "Member length", "150");
            SelectUnit(Combos(), "Member length", "mm");
            SetText(Boxes(), "Mass limit", "50");
            SelectUnit(Combos(), "Mass limit", "g");

            // --- Check ---
            var checkButton = assetsView.GetLogicalDescendants().OfType<Button>().Distinct().Single(b => Equals(AutomationProperties.GetName(b), "Check"));
            checkButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () =>
                assetsView.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("Outcome:", StringComparison.Ordinal)));
            LayOut(window);

            Assert.Contains(
                assetsView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains("Meets criteria", StringComparison.Ordinal));

            // --- Fill the independent check and select the Record target ---
            SelectByPrefix(Combos().Single(c => Equals(AutomationProperties.GetName(c), "Calculation pack")), PackRecordId);
            SelectByPrefix(Combos().Single(c => Equals(AutomationProperties.GetName(c), "Verification artefact")), ArtefactRecordId);

            SetText(Boxes(), "Independent basis",
                "Hand calculation from first principles: sigma = 12000 N / 6.0e-5 m2 = 200 MPa; margin = 260/200 - 1 = 0.30; "
                + "mass = 2700 kg/m3 x 6.0e-5 m2 x 0.15 m = 0.0243 kg.");
            SetText(Boxes(), "Independent margin", "0.30");
            SetText(Boxes(), "Independent mass", "0.0243");
            SetText(Boxes(), "Verifier", EngineerId);
            SetText(Boxes(), "Performed on", "2026-09-15");

            // --- Record ---
            var recordButton = assetsView.GetLogicalDescendants().OfType<Button>().Distinct().Single(b => Equals(AutomationProperties.GetName(b), "Record verification artefact"));
            recordButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () =>
                assetsView.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).StartsWith("Recorded.", StringComparison.Ordinal)));
            LayOut(window);

            Assert.Contains(
                assetsView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).StartsWith("Recorded.", StringComparison.Ordinal)
                    && (t.Text ?? string.Empty).Contains("Passed", StringComparison.Ordinal));

            // --- The artefact appears under Verification artefacts, Passed ---
            assetsView.SelectTab("Verification artefacts");
            LayOut(window);
            await RenderUntilAsync(window, () => assetsView.GetLogicalDescendants().OfType<TextBlock>()
                .Any(t => (t.Text ?? string.Empty).Contains(ArtefactRecordId, StringComparison.Ordinal)
                    && (t.Text ?? string.Empty).Contains("Passed", StringComparison.Ordinal)));

            Assert.Contains(
                assetsView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains(ArtefactRecordId, StringComparison.Ordinal)
                    && (t.Text ?? string.Empty).Contains("Passed", StringComparison.Ordinal));

            // --- The trace tab shows this run's own provenance (TD-160) ---
            assetsView.SelectTab("Calculation packs");
            LayOut(window);

            var openPackButton = assetsView.GetLogicalDescendants().OfType<Button>()
                .First(b => (AutomationProperties.GetName(b) ?? string.Empty) == $"Open {PackRecordId}");
            openPackButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () =>
                assetsView.GetLogicalDescendants().OfType<TabItem>().Any(t => Equals(AutomationProperties.GetName(t), "Trace")));
            LayOut(window);

            var traceTab = assetsView.GetLogicalDescendants().OfType<TabItem>().Distinct().Single(t => Equals(AutomationProperties.GetName(t), "Trace"));
            traceTab.IsSelected = true;
            LayOut(window);

            await RenderUntilAsync(window, () =>
                assetsView.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("IN-MATERIAL", StringComparison.Ordinal)));

            Assert.Contains(
                assetsView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains(MaterialRecordId, StringComparison.Ordinal));
            Assert.Contains(
                assetsView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains("OUT-MARGIN", StringComparison.Ordinal));
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    private static async Task SeedAsync(WorkspaceHost host)
    {
        var materialDefinition = new MaterialDefinition
        {
            Name = "Journey Test Alloy",
            Family = MaterialFamily.Aluminium,
            Designation = "6082-T6",
            Properties = new Dictionary<string, ReferenceQuantityValue>
            {
                [MaterialPropertyNames.YieldStrength] = new(
                    new Quantity<Pressure>(260, PressureUnits.Megapascal), ReferenceValueOrigin.EngineeringReference, "Journey test fixture."),
                [MaterialPropertyNames.Density] = new(
                    new Quantity<MassDensity>(2700, MassDensityUnits.KilogramPerCubicMetre), ReferenceValueOrigin.EngineeringReference, "Journey test fixture."),
            },
        };
        await host.Materials!.RegisterAsync(
            MaterialRecordId, materialDefinition,
            new ReferenceProvenance(SourceOrganisation: "Test Handbook Publisher", SourceDocument: "Test Handbook"));
        await host.ReferenceReview!.VerifyAsync(host.Materials!, MaterialRecordId, new ReferenceReviewStatement("Test datasheet"));
        await host.ReferenceReview!.ReleaseAsync(host.Materials!, MaterialRecordId, "Released for the Engineering Assets journey test.");

        var pack = new CalculationPack
        {
            Reference = "JOURNEY-CPK-001",
            Title = "Journey bracket check",
            Purpose = "Proves the Desktop can record a bracket check into an existing calculation pack.",
            Method = new CalculationMethod(CalculationMethodKind.ClosedForm, "sigma = F / A; margin = (Rp0.2 / sigma) - 1."),
        };
        await host.CalculationPacks!.RegisterAsync(PackRecordId, pack, new ReferenceProvenance(SourceOrganisation: "Test"));

        var artefact = new VerificationArtefact
        {
            Reference = "JOURNEY-VER-001",
            Requirement = new VerifiedRequirement(Guid.NewGuid()),
            Subject = "Journey test mounting bracket",
            Method = VerificationMethod.Analysis,
        };
        await host.VerificationArtefacts!.RegisterAsync(ArtefactRecordId, artefact, new ReferenceProvenance(SourceOrganisation: "Test"));
    }

    private static void SignIn(WorkspaceHost host)
    {
        var principalSession = (PrincipalSession)host.Services!.GetService(typeof(PrincipalSession));
        principalSession.Establish(
            new PlatformPrincipal(new PlatformIdentity(EngineerId, EngineerId), ApplicationPermissions.LocalSession));
    }

    private static bool HasItems(ComboBox combo) => combo.ItemsSource is { } source && source.Cast<object>().Any();

    private static void SelectByPrefix(ComboBox combo, string prefix)
    {
        var items = ((IEnumerable)combo.ItemsSource!).Cast<string>().ToList();
        var matches = items.Where(s => s.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException($"Expected exactly one match for '{prefix}' among [{string.Join(" | ", items)}], found {matches.Count}.");
        combo.SelectedItem = matches[0];
    }

    private static void SetText(IReadOnlyList<TextBox> boxes, string automationName, string text) =>
        boxes.Single(b => Equals(AutomationProperties.GetName(b), automationName)).Text = text;

    private static void SelectUnit(IReadOnlyList<ComboBox> combos, string fieldLabel, string unitSymbol)
    {
        var combo = combos.Single(c => Equals(AutomationProperties.GetName(c), $"{fieldLabel} unit"));
        combo.SelectedItem = ((IEnumerable)combo.ItemsSource!).Cast<string>().Single(s => s == unitSymbol);
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
