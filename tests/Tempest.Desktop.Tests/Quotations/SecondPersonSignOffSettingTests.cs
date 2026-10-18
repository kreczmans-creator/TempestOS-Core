using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Governance;
using Tempest.Core.Quotations;
using Tempest.Core.Settings;
using Tempest.Desktop.Quotations;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests.Quotations;

/// <summary>
/// `ADR-0161` (Product Owner decision 2026-10-01), headless: Settings →
/// Sign-off's own "Second-person sign-off" switch is off by default; the
/// Quote tab says so ("Self-approval allowed"); saving it on makes the
/// author's own approval refused, and saving it off again lets the author
/// approve their own quote — recorded as self-approved — with both changes
/// audited.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SecondPersonSignOffSettingTests
{
    [AvaloniaFact]
    public async Task TheSettingsSwitch_IsOffByDefault_AndChangesWhetherTheAuthorMayApproveTheirOwnQuote()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        Window? settingsWindow = null;
        Window? quoteWindow = null;
        try
        {
            await host.StartAsync();
            var policy = Resolve<ISignOffPolicy>(host);
            var settingsProvider = Resolve<ISettingsProvider>(host);
            var settingsView = new SettingsView(
                new ThemeService(settingsProvider), new UserSettings(settingsProvider), settingsProvider,
                Resolve<IConfigurationProvider>(host), "(test)", signOffPolicy: policy);
            settingsWindow = new Window { Width = 1200, Height = 900, Content = settingsView };
            settingsWindow.Show();
            await settingsView.RefreshAsync();

            // ---- Off by default, with the PO's own wording and an automation name ----
            var signOffSwitch = settingsView.GetLogicalDescendants().OfType<CheckBox>()
                .Single(c => AutomationProperties.GetName(c) == SettingsView.SecondPersonSignOffName);
            Assert.False(signOffSwitch.IsChecked);
            Assert.Contains("Off for a one-person consultancy", ((TextBlock)signOffSwitch.Content!).Text, StringComparison.Ordinal);
            Assert.False(await policy.IsSecondPersonRequiredAsync());

            // ---- A quote in review, submitted by whoever is signed in ----
            var project = await host.ProjectDirectory!.CreateAsync("P-SIGNOFF", "Sign-off Project");
            var quotations = Resolve<IQuotationService>(host);
            var created = await quotations.CreateAsync(project.Id);
            Assert.True(created.Succeeded, created.Reason);
            var quoteId = created.Quotation!.Id;
            Assert.True((await quotations.AddLineAsync(quoteId, "Survey", null, null, new Money(500m, CurrencyCode.Gbp))).Succeeded);
            Assert.True((await quotations.SubmitForReviewAsync(quoteId)).Succeeded);

            var domain = Resolve<EngineeringDomainContext>(host);
            var quoteView = new ProjectQuoteView(
                domain, Resolve<ICommandDispatcher>(host), Resolve<ICommandRegistry>(host), () => project.Id,
                Resolve<IOrganisationCatalog>(host), (_, _) => { }, new StubFilePicker(), new QuotationSheetRenderer(),
                () => "Issuer", () => "TempestOS test")
            {
                SignOffPolicy = policy,
            };
            quoteWindow = new Window { Width = 1400, Height = 900, Content = quoteView };
            quoteWindow.Show();
            await quoteView.SelectQuoteAsync(quoteId);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(ProjectQuoteView.SelfApprovalAllowedText, SignOffState(quoteView));

            // ---- Settings: switch it on, Save — the author is now refused ----
            signOffSwitch.IsChecked = true;
            Click(settingsView, "Save");
            await UntilAsync(() => policy.IsSecondPersonRequiredAsync().GetAwaiter().GetResult());

            await quoteView.RefreshAsync();
            Assert.Equal(ProjectQuoteView.SecondPersonRequiredText, SignOffState(quoteView));
            Click(quoteView, "Approve");
            await UntilAsync(() => StatusLine(quoteView).Contains("second person", StringComparison.Ordinal));
            Assert.Equal(QuotationStatus.InReview, Quote(domain, quoteId).Status);

            // ---- Settings: switch it off, Save — the author approves their own quote ----
            await settingsView.RefreshAsync();
            Assert.True(signOffSwitch.IsChecked);
            signOffSwitch.IsChecked = false;
            Click(settingsView, "Save");
            await UntilAsync(() => !policy.IsSecondPersonRequiredAsync().GetAwaiter().GetResult());

            await quoteView.RefreshAsync();
            Assert.Equal(ProjectQuoteView.SelfApprovalAllowedText, SignOffState(quoteView));
            Click(quoteView, "Approve");
            await UntilAsync(() => Quote(domain, quoteId).Status == QuotationStatus.Approved);
            Assert.Equal("R1", Quote(domain, quoteId).RevisionLabel);
            Assert.True(Quote(domain, quoteId).Review.Revisions[^1].SelfApproved);
            await UntilAsync(() => ReviewState(quoteView).Contains("(self-approved)", StringComparison.Ordinal));

            // ---- Both changes of the switch are on the audit trail, old → new ----
            var audit = await Resolve<IAuditQuery>(host).QueryAsync(new AuditQueryCriteria(action: SignOffPolicy.ChangedActionName));
            Assert.Equal(
                ["Off→On", "On→Off"],
                audit.OrderBy(r => r.OccurredAt).Select(r => $"{r.Detail["OldValue"]}→{r.Detail["NewValue"]}").ToArray());
        }
        finally
        {
            try
            {
                quoteWindow?.Close();
                settingsWindow?.Close();
                Dispatcher.UIThread.RunJobs();
            }
            finally
            {
                await host.ShutdownAsync();
                await host.DisposeAsync();
            }
        }
    }

    private static Quotation Quote(EngineeringDomainContext domain, Guid quoteId) =>
        (Quotation)domain.Repository.FindAsync(quoteId).GetAwaiter().GetResult()!;

    private static string SignOffState(Control view) =>
        view.GetLogicalDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == ProjectQuoteView.SignOffStateName).Text ?? string.Empty;

    private static string ReviewState(Control view) =>
        view.GetLogicalDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == "Quote review state").Text ?? string.Empty;

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

    private static T Resolve<T>(WorkspaceHost host) where T : class => (T)host.Services!.GetService(typeof(T));
}
