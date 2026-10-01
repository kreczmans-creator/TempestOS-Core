using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;
using Tempest.Core.Quotations;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Desktop.Quotations;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests.Quotations;

/// <summary>
/// The Quote tab's own runbook C3 surface, headless: the rate dropdown
/// (a pinned card's hourly entries fill a read-only rate and take hours;
/// Fixed enables the fixed price box; no pinned card offers Fixed alone,
/// with a hint), Save draft, and the review buttons — Submit for review,
/// Approve refused for the same person, approved as R1 by a second, Return
/// to draft with a comment — and the export file name carrying R1 (DRAFT
/// before approval).
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProjectQuoteReviewTests
{
    private const string CardId = "QUOTE-TAB-CARD";

    [AvaloniaFact]
    public async Task NoPinnedRateCard_TheDropdownOffersFixedAlone_WithAHintToPinOne()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        Window? window = null;
        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-QR-NOCARD", "No Card Project");
            (var view, window, _) = await OpenQuoteViewAsync(host, project.Id);

            var choice = RateChoice(view);
            var items = choice.Items.OfType<ComboBoxItem>().Select(i => i.Content as string).ToList();
            Assert.Equal([ProjectQuoteView.FixedChoiceLabel], items);
            Assert.Equal(ProjectQuoteView.FixedChoiceLabel, (choice.SelectedItem as ComboBoxItem)?.Content);
            Assert.True(Numeric(view, "Line fixed price").IsEnabled);
            Assert.False(Numeric(view, "Line hours").IsEnabled);

            var hint = view.GetLogicalDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == "Rate card hint");
            Assert.True(hint.IsVisible);
            Assert.Contains("Pin a rate card on the Details tab", hint.Text, StringComparison.Ordinal);
        }
        finally
        {
            await TearDownAsync(host, window);
        }
    }

    [AvaloniaFact]
    public async Task APinnedCardsHourlyEntry_FillsTheReadOnlyRate_FixedEnablesTheFixedBox_AndTheLineSavesFromTheCard()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        Window? window = null;
        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-QR-CARD", "Card Project");
            await PinCardAsync(host, project.Id);
            (var view, window, var quoteId) = await OpenQuoteViewAsync(host, project.Id);
            var domain = Resolve<EngineeringDomainContext>(host);

            var choice = RateChoice(view);
            var labels = choice.Items.OfType<ComboBoxItem>().Select(i => (string)i.Content!).ToList();
            Assert.Equal(3, labels.Count);
            Assert.StartsWith("Senior engineering", labels[0], StringComparison.Ordinal);
            Assert.StartsWith("Graduate engineering", labels[1], StringComparison.Ordinal);
            Assert.Equal(ProjectQuoteView.FixedChoiceLabel, labels[2]);
            Assert.DoesNotContain(labels, l => l.StartsWith("Site visit", StringComparison.Ordinal));

            // The first hourly entry is chosen by default: its rate, read-only; hours on; fixed off.
            var rate = Numeric(view, "Line rate");
            Assert.True(rate.IsReadOnly);
            Assert.Equal(150m, rate.Value);
            Assert.True(Numeric(view, "Line hours").IsEnabled);
            Assert.False(Numeric(view, "Line fixed price").IsEnabled);

            choice.SelectedItem = choice.Items.OfType<ComboBoxItem>().ElementAt(1);
            Assert.Equal(85m, Numeric(view, "Line rate").Value);

            Text(view, "Line description").Text = "Calculations";
            Numeric(view, "Line hours").Value = 12m;
            Click(view, "Add line");
            await UntilAsync(() => Quote(domain, quoteId).Lines.Count == 1);
            var hourly = Quote(domain, quoteId).Lines[0];
            Assert.Equal(new Money(85m, CurrencyCode.Gbp), hourly.Rate);
            Assert.Equal(new Money(1_020m, CurrencyCode.Gbp), hourly.Amount);
            Assert.Equal("ENG-2", hourly.RateCardServiceCode);

            // Fixed: the rate clears, hours go off, the fixed price box comes on.
            choice = RateChoice(view);
            choice.SelectedItem = choice.Items.OfType<ComboBoxItem>().Single(i => Equals(i.Content, ProjectQuoteView.FixedChoiceLabel));
            Assert.Null(Numeric(view, "Line rate").Value);
            Assert.False(Numeric(view, "Line hours").IsEnabled);
            Assert.True(Numeric(view, "Line fixed price").IsEnabled);

            Text(view, "Line description").Text = "Site survey";
            Numeric(view, "Line fixed price").Value = 600m;
            Click(view, "Add line");
            await UntilAsync(() => Quote(domain, quoteId).Lines.Count == 2);
            var fixedLine = Quote(domain, quoteId).Lines[1];
            Assert.Equal(QuotationLineBasis.FixedPrice, fixedLine.Basis);
            Assert.Equal(new Money(600m, CurrencyCode.Gbp), fixedLine.Amount);
        }
        finally
        {
            await TearDownAsync(host, window);
        }
    }

    [AvaloniaFact]
    public async Task SaveDraft_SubmitForReview_SamePersonRefused_SecondPersonApprovesR1_ReturnToDraftNeedsAComment_ExportNamedR1()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        Window? window = null;
        try
        {
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-QR-REVIEW", "Review Project");
            // ADR-0161: second-person sign-off is off by default (a one-person
            // consultancy); the same-person refusal below is the ON rule.
            await Resolve<Tempest.Core.Governance.ISignOffPolicy>(host).SetSecondPersonRequiredAsync(true);
            await PinCardAsync(host, project.Id);
            (var view, window, var quoteId) = await OpenQuoteViewAsync(host, project.Id);
            var domain = Resolve<EngineeringDomainContext>(host);
            var filePicker = (StubFilePicker)typeof(ProjectQuoteView)
                .GetField("_filePicker", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(view)!;

            // ---- Save draft saves a line still being typed, and says so ----
            Assert.Contains("Draft — not saved yet", ReviewState(view), StringComparison.Ordinal);
            Assert.DoesNotContain(view.GetLogicalDescendants().OfType<Button>(), b => Equals(b.Content, "Send"));
            Text(view, "Line description").Text = "Design";
            Numeric(view, "Line hours").Value = 10m;
            Click(view, "Save draft");
            await UntilAsync(() => Quote(domain, quoteId).Lines.Count == 1 && Quote(domain, quoteId).Review.DraftSavedAt is not null);
            await UntilAsync(() => ReviewState(view).Contains("Draft — saved", StringComparison.Ordinal));
            Assert.Equal(QuotationStatus.Draft, Quote(domain, quoteId).Status);

            // ---- A draft exports as DRAFT ----
            var draftPath = Path.Combine(Path.GetTempPath(), $"quote-draft-{Guid.NewGuid():N}.pdf");
            filePicker.SetNextSavePath(draftPath);
            Click(view, "Export");
            await UntilAsync(() => ExportIsComplete(draftPath));
            Assert.EndsWith("-DRAFT-quote.pdf", filePicker.SaveRequests[^1].SuggestedFileName, StringComparison.Ordinal);
            Assert.Contains("DRAFT", PdfTextExtractor.ExtractText(await File.ReadAllBytesAsync(draftPath)), StringComparison.Ordinal);

            // ---- Submit for review ----
            Click(view, "Submit for review");
            await UntilAsync(() => Quote(domain, quoteId).Status == QuotationStatus.InReview);
            await UntilAsync(() => view.GetLogicalDescendants().OfType<Button>().Any(b => Equals(b.Content, "Approve")));
            Assert.DoesNotContain(view.GetLogicalDescendants().OfType<Button>(), b => Equals(b.Content, "Add line"));

            // ---- Return to draft needs a comment ----
            Click(view, "Return to draft");
            await UntilAsync(() => StatusLine(view).Contains("comment is required", StringComparison.Ordinal));
            Assert.Equal(QuotationStatus.InReview, Quote(domain, quoteId).Status);

            Text(view, "Review comment").Text = "Add the site visit.";
            Click(view, "Return to draft");
            await UntilAsync(() => Quote(domain, quoteId).Status == QuotationStatus.Draft);
            await UntilAsync(() => ReviewState(view).Contains("Add the site visit.", StringComparison.Ordinal));
            Click(view, "Submit for review");
            await UntilAsync(() => Quote(domain, quoteId).Status == QuotationStatus.InReview);
            await UntilAsync(() => view.GetLogicalDescendants().OfType<Button>().Any(b => Equals(b.Content, "Approve")));

            // ---- The same person cannot approve it ----
            Click(view, "Approve");
            await UntilAsync(() => StatusLine(view).Contains("second person", StringComparison.Ordinal));
            Assert.Contains("switch person", StatusLine(view), StringComparison.Ordinal);
            Assert.Equal(QuotationStatus.InReview, Quote(domain, quoteId).Status);

            // ---- A second person approves it: R1 ----
            await QuotationReviewSupport.AsReviewerAsync(host, async () =>
            {
                Click(view, "Approve");
                await UntilAsync(() => Quote(domain, quoteId).Status == QuotationStatus.Approved);
                return true;
            });
            Assert.Equal("R1", Quote(domain, quoteId).RevisionLabel);
            await UntilAsync(() => view.GetLogicalDescendants().OfType<Button>().Any(b => Equals(b.Content, "Send")));
            // M4: the approver by name — the reviewer's own display name while
            // they are signed in, their readable id once they are not.
            Assert.Matches("Approved R1 by (Second Reviewer|" + QuotationReviewSupport.ReviewerId + ")", ReviewState(view));

            // ---- Export after approval: R1 on the sheet and in the file name ----
            var exportPath = Path.Combine(Path.GetTempPath(), $"quote-r1-{Guid.NewGuid():N}.pdf");
            filePicker.SetNextSavePath(exportPath);
            Click(view, "Export");
            await UntilAsync(() => ExportIsComplete(exportPath));
            var reference = Quote(domain, quoteId).Reference;
            Assert.Equal($"{reference}-R1-quote.pdf", filePicker.SaveRequests[^1].SuggestedFileName);
            var text = PdfTextExtractor.ExtractText(await File.ReadAllBytesAsync(exportPath));
            Assert.Contains("Revision: R1", text, StringComparison.Ordinal);

            TryDelete(draftPath);
            TryDelete(exportPath);
        }
        finally
        {
            await TearDownAsync(host, window);
        }
    }

    [AvaloniaFact]
    public async Task TheReviewLine_NamesPeople_NeverShowsARawWindowsSid()
    {
        // Colour review board M4.
        const string AuthorSid = "S-1-5-21-1004336348-1177238915-682003330-1001";
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        Window? window = null;
        try
        {
            await host.StartAsync();
            host.SwitchPrincipal(new Tempest.Core.Identity.SessionPrincipal(AuthorSid, "Alex Author", Tempest.Core.Identity.SessionRole.Engineer));
            var project = await host.ProjectDirectory!.CreateAsync("P-QR-NAMES", "Names Project");
            (var view, window, var quoteId) = await OpenQuoteViewAsync(host, project.Id);
            view.Principals = Resolve<Tempest.Core.Identity.IPrincipalDirectory>(host);
            var quotations = Resolve<IQuotationService>(host);

            Assert.True((await quotations.AddLineAsync(quoteId, "Survey", null, null, new Money(500m, CurrencyCode.Gbp))).Succeeded);
            Assert.True((await quotations.SubmitForReviewAsync(quoteId)).Succeeded);
            await view.RefreshAsync();
            Assert.Contains("submitted by Alex Author", ReviewState(view), StringComparison.Ordinal);

            // Seen by the reviewer: the author's SID is not theirs to name, and is still never printed.
            await QuotationReviewSupport.AsReviewerAsync(host, async () =>
            {
                await view.RefreshAsync();
                Assert.DoesNotContain("S-1-", ReviewState(view), StringComparison.Ordinal);
                Assert.Contains("submitted by " + Tempest.Desktop.Documents.Timesheets.TimesheetPrincipalLabel.Unnamed, ReviewState(view), StringComparison.Ordinal);
                Assert.True((await quotations.ApproveAsync(quoteId)).Succeeded);
                await view.RefreshAsync();
                Assert.Contains("Approved R1 by Second Reviewer", ReviewState(view), StringComparison.Ordinal);
                return true;
            });

            await view.RefreshAsync();
            Assert.DoesNotContain("S-1-", ReviewState(view), StringComparison.Ordinal);
        }
        finally
        {
            await TearDownAsync(host, window);
        }
    }

    /// <summary>
    /// Closes the window and drains its queued render work while the
    /// headless session is still whole, then stops the host — in that
    /// order, whether or not the test passed (colour review board M12; the
    /// teardown fault <c>ProjectFolderExportTests</c> documents).
    /// </summary>
    private static async Task TearDownAsync(WorkspaceHost host, Window? window)
    {
        try
        {
            window?.Close();
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task<(ProjectQuoteView View, Window Window, Guid QuoteId)> OpenQuoteViewAsync(WorkspaceHost host, Guid projectId)
    {
        var domain = Resolve<EngineeringDomainContext>(host);
        var view = new ProjectQuoteView(
            domain, Resolve<ICommandDispatcher>(host), Resolve<ICommandRegistry>(host), () => projectId,
            Resolve<IOrganisationCatalog>(host), (_, _) => { }, new StubFilePicker(), new QuotationSheetRenderer(),
            () => "Issuer", () => "TempestOS test")
        {
            RateCards = Resolve<IRateCardCatalog>(host),
        };

        // Created before the window is shown, so a refusal here leaves no
        // window for the caller's teardown to miss.
        var created = await Resolve<IQuotationService>(host).CreateAsync(projectId);
        Assert.True(created.Succeeded, created.Reason);

        var window = new Window { Width = 1400, Height = 900, Content = view };
        window.Show();
        await view.SelectQuoteAsync(created.Quotation!.Id);
        Dispatcher.UIThread.RunJobs();
        return (view, window, created.Quotation.Id);
    }

    private static async Task PinCardAsync(WorkspaceHost host, Guid projectId)
    {
        var rateCards = Resolve<IRateCardCatalog>(host);
        var card = new RateCard
        {
            Code = CardId,
            Name = "Quote tab rate card",
            EffectivePeriod = new EffectivePeriod(new DateOnly(2020, 1, 1), null),
            Currency = CurrencyCode.Gbp,
            Governance = new BusinessGovernanceFacts { Ownership = new BusinessOwnership("owner-1", "Owner") },
            Entries =
            [
                new RateCardEntry("ENG-1", "Senior engineering", PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), Grade: "Senior"),
                new RateCardEntry("ENG-2", "Graduate engineering", PricingBasis.Hourly, new Money(85m, CurrencyCode.Gbp), Grade: "Graduate"),
                new RateCardEntry("VISIT", "Site visit", PricingBasis.FixedPrice, new Money(400m, CurrencyCode.Gbp)),
            ],
        };
        await rateCards.RegisterAsync(CardId, card, new ReferenceProvenance(SourceOrganisation: "Test Org", SourceDocument: "Test Doc"));
        await host.ReferenceReview!.VerifyAsync(rateCards, CardId, new ReferenceReviewStatement("Consulted for the quote tab test."));
        await host.ReferenceReview!.ReleaseAsync(rateCards, CardId, "Released for the quote tab test.");

        var commercial = Resolve<IProjectCommercialService>(host);
        var pinned = await commercial.PinRateCardAsync(projectId, CardId);
        Assert.True(pinned.Succeeded, pinned.Reason);
    }

    private static Quotation Quote(EngineeringDomainContext domain, Guid quoteId) =>
        (Quotation)domain.Repository.FindAsync(quoteId).GetAwaiter().GetResult()!;

    private static ComboBox RateChoice(Control view) =>
        view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == ProjectQuoteView.RateChoiceName);

    private static NumericUpDown Numeric(Control view, string name) =>
        view.GetLogicalDescendants().OfType<NumericUpDown>().Single(n => AutomationProperties.GetName(n) == name);

    private static TextBox Text(Control view, string name) =>
        view.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == name);

    private static string ReviewState(Control view) =>
        view.GetLogicalDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == "Quote review state").Text ?? string.Empty;

    /// <summary>The view's own status line — the first caption under the heading, where every refusal is reported.</summary>
    private static string StatusLine(ProjectQuoteView view) =>
        ((TextBlock)typeof(ProjectQuoteView)
            .GetField("_status", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(view)!).Text ?? string.Empty;

    private static void Click(Control view, string content) =>
        view.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, content)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DesktopTestHelpers.Deadline(15);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition(), $"The condition was not met within {15 * DesktopTestHelpers.TimeoutFactor:0.#} seconds.");
    }

    /// <summary>True once the export's own write stream has closed — see <c>QuotationJourneyTests.ExportIsComplete</c>.</summary>
    private static bool ExportIsComplete(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return exclusive.Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private static T Resolve<T>(WorkspaceHost host) where T : class => (T)host.Services!.GetService(typeof(T));
}
