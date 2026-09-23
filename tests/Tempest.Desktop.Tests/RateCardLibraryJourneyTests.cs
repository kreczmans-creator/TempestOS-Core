using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Views;
using Tempest.Workspace;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// DEFECT-1 of the overnight real-shell journey (2026-09-16, `WP 21.5C`
/// Linux): nothing in the shipped application could create a rate card —
/// no form, no seed, no command — so on a clean persistence root no
/// project could pin one, no timesheet entry could be priced and no
/// invoice request could be raised (`PHYSICAL_REVIEW.md` §7c C2, D10–D12
/// were unperformable on a clean machine). Through the real window: the
/// Libraries area's new "Add a rate card" form registers a Draft card with
/// one graded hourly rate, opens it right up, and the row's own Verify and
/// Release take it to Released — the state <c>RateCardPicker</c> offers
/// and <c>TimesheetService</c> prices against. The released card prices
/// the grade it was given.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class RateCardLibraryJourneyTests
{
    private const string CardName = "Consultancy standard rates 2026";
    private const string Grade = "Senior Engineer";

    [AvaloniaFact]
    public async Task AddingARateCard_ThroughTheLibrariesForm_ReleasingIt_PricesTheGradeItWasGiven()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);

            await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single().SelectNode("Reference data");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<LibrariesView>().Any());
            LayOut(window);

            var librariesView = window.GetLogicalDescendants().OfType<LibrariesView>().Single();

            var textBoxes = librariesView.GetLogicalDescendants().OfType<TextBox>().ToList();
            textBoxes.First(t => t.Watermark == "Rate card name").Text = CardName;
            textBoxes.First(t => t.Watermark == "Grade (e.g. Engineer)").Text = Grade;
            textBoxes.First(t => t.Watermark == "Hourly rate (GBP)").Text = "95.50";

            var addButton = librariesView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Add Rate Card"));
            addButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // Add opens the new record right up (the Product Owner guard,
            // `po-comments.md` item 5), the way Add Material and Add Person do.
            await RenderUntilAsync(window, () =>
                librariesView.GetLogicalDescendants().OfType<ReferenceRecordView>().FirstOrDefault() is { } d
                && d.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains(CardName, StringComparison.Ordinal)));
            LayOut(window);

            var detail = librariesView.GetLogicalDescendants().OfType<ReferenceRecordView>().First();
            Assert.Contains(detail.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? string.Empty).Contains(CardName, StringComparison.Ordinal));

            var rateCards = (IRateCardCatalog)host.Services!.GetService(typeof(IRateCardCatalog));
            var draft = await rateCards.FindByCodeAsync("ratecard-consultancy-standard-rates-2026");
            Assert.NotNull(draft);
            Assert.Equal(ReferenceValidationState.Draft, draft!.ValidationState);
            Assert.Equal(CardName, draft.Definition.Name);
            var entry = Assert.Single(draft.Definition.Entries);
            Assert.Equal(Grade, entry.Grade);
            Assert.Equal(PricingBasis.Hourly, entry.Basis);
            Assert.Equal(95.50m, entry.Rate.Amount);

            // Verify, then Release — the governed acts every other library's
            // record already offers.
            var verifyButton = detail.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Verify"));
            verifyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => detail.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("Checked", StringComparison.Ordinal)));
            LayOut(window);

            var releaseButton = detail.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Release"));
            releaseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => detail.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("Released", StringComparison.Ordinal)));

            var released = await rateCards.FindByCodeAsync("ratecard-consultancy-standard-rates-2026");
            Assert.NotNull(released);
            Assert.Equal(ReferenceValidationState.Released, released!.ValidationState);

            // The card prices the grade it was given — what `TimesheetService`
            // asks of a pinned card — and refuses a grade it does not carry.
            var priced = released.Definition.ResolveRates(Grade);
            Assert.True(priced.Succeeded, priced.Reason);
            Assert.Equal(95.50m, priced.Billing!.Value.Amount);
            Assert.False(released.Definition.ResolveRates("Apprentice").Succeeded);

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AddRateCard_WithNoNameOrANonNumericRate_IsRefusedInWords_AndRegistersNothing()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.EngineeringDepartment);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single().SelectNode("Reference data");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<LibrariesView>().Any());
            LayOut(window);

            var librariesView = window.GetLogicalDescendants().OfType<LibrariesView>().Single();
            var textBoxes = librariesView.GetLogicalDescendants().OfType<TextBox>().ToList();
            var addButton = librariesView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Add Rate Card"));

            addButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => librariesView.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).StartsWith("Enter a rate card name", StringComparison.Ordinal)));
            Assert.Contains(librariesView.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? string.Empty).StartsWith("Enter a rate card name", StringComparison.Ordinal));

            textBoxes.First(t => t.Watermark == "Rate card name").Text = "Bad rate";
            textBoxes.First(t => t.Watermark == "Hourly rate (GBP)").Text = "ninety";
            addButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => librariesView.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).StartsWith("Enter the hourly rate as a number", StringComparison.Ordinal)));
            Assert.Contains(librariesView.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? string.Empty).StartsWith("Enter the hourly rate as a number", StringComparison.Ordinal));

            var rateCards = (IRateCardCatalog)host.Services!.GetService(typeof(IRateCardCatalog));
            Assert.Null(await rateCards.FindByCodeAsync("ratecard-bad-rate"));

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }
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
