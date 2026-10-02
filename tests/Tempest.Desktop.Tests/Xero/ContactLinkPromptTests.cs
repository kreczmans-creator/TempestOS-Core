using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.Xero.ContactLinkViewTests;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` U2: <see cref="XeroContactLinkPrompt"/> on its own, end to end
/// through the simulator — a failure's reason is shown and the prompt stays
/// open to try again; Xero calls never block the UI thread; a search can be
/// cancelled, a write cannot.
/// </summary>
public sealed class ContactLinkPromptTests
{
    [AvaloniaFact]
    public async Task CreateRefusedByTheLiveOrganisationGuard_ShowsTheReason_StaysOpen_LinksNothing()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync(isDemoCompany: false);
        await kit.AddOrganisationAsync();
        var (window, prompt) = Show(kit);

        var answer = prompt.PromptAsync("ACME1", "Acme Engineering Ltd");
        await kit.WaitAsync(() => !prompt.IsBusy && Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled);
        var mark = kit.Mark;

        Click(prompt, XeroContactLinkPrompt.CreateButtonName);
        await kit.WaitAsync(() => !prompt.IsBusy && Text(prompt, XeroContactLinkPrompt.StatusName).StartsWith("Could not create", StringComparison.Ordinal));

        var status = Text(prompt, XeroContactLinkPrompt.StatusName);
        Assert.StartsWith("Could not create the contact in Xero: ", status, StringComparison.Ordinal);
        Assert.Contains(XeroWriteSafetyHandler.RuleLiveOrganisation, status, StringComparison.Ordinal);
        Assert.True(prompt.IsVisible);
        Assert.False(answer.IsCompleted);
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.Empty(kit.WritesSince(mark));
        Assert.Empty(kit.LiveContacts);

        Click(prompt, XeroContactLinkPrompt.CancelButtonName);
        Assert.Null(await answer);
        Assert.False(prompt.IsVisible);
        kit.AssertNoViolations();
        window.Close();
    }

    [AvaloniaFact]
    public async Task SearchWhileXeroIsUnreachable_SaysSo_ThenSearchAgainFindsTheContact()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.TransportFailure, PathContains: "Contacts"));
        var (window, prompt) = Show(kit);

        var answer = prompt.PromptAsync("ACME1", "Acme Engineering Ltd");
        await kit.WaitAsync(() => !prompt.IsBusy && Text(prompt, XeroContactLinkPrompt.StatusName).StartsWith("Could not search Xero", StringComparison.Ordinal));
        Assert.Contains("Xero could not be reached", Text(prompt, XeroContactLinkPrompt.StatusName), StringComparison.Ordinal);
        Assert.Contains("Nothing was changed", Text(prompt, XeroContactLinkPrompt.StatusName), StringComparison.Ordinal);
        Assert.Empty(prompt.Candidates);

        Click(prompt, XeroContactLinkPrompt.SearchAgainButtonName);
        await kit.WaitAsync(() => !prompt.IsBusy && prompt.Candidates.Count == 1);
        Assert.Equal(contactId, prompt.Candidates[0].ContactId);
        Assert.Equal(XeroContactMatcher.MatchedOnExactName, prompt.Candidates[0].MatchedOn);

        Assert.True(prompt.Select(contactId));
        Click(prompt, XeroContactLinkPrompt.LinkButtonName);
        var link = await answer;

        Assert.NotNull(link);
        Assert.Equal(contactId, link.XeroId);
        Assert.Equal(contactId, (await kit.Linker.FindLinkAsync("ACME1"))!.XeroId);
        kit.AssertNoViolations();
        window.Close();
    }

    [AvaloniaFact]
    public async Task WhileXeroIsAnswering_TheUiStaysResponsive_ButtonsWait_AndCancelStopsTheSearch()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        kit.Simulator.SeedContact("Acme Engineering Ltd");
        var (window, prompt) = Show(kit);

        Task<Core.Invoicing.Xero.Sync.XeroLink?> answer;
        using (kit.Simulator.HoldRequests())
        {
            answer = prompt.PromptAsync("ACME1", "Acme Engineering Ltd");

            // PromptAsync returned at once; the dispatcher keeps running while
            // the search waits on Xero.
            await kit.WaitAsync(() => kit.Simulator.InFlight == 1);
            Assert.True(prompt.IsBusy);
            Assert.Equal("Searching Xero for matching contacts…", Text(prompt, XeroContactLinkPrompt.StatusName));
            Assert.False(Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled);
            Assert.False(Button(prompt, XeroContactLinkPrompt.LinkButtonName).IsEnabled);
            Assert.True(Button(prompt, XeroContactLinkPrompt.CancelButtonName).IsEnabled);

            Click(prompt, XeroContactLinkPrompt.CancelButtonName);
            Assert.Null(await answer);
            Assert.False(prompt.IsVisible);
        }

        await kit.WaitAsync(() => kit.Simulator.InFlight == 0);
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.Empty(kit.WritesSince(0));
        kit.AssertNoViolations();
        window.Close();
    }

    [AvaloniaFact]
    public async Task CancelDuringACreate_WaitsForXerosAnswer_SoTheOutcomeIsKnown()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var (window, prompt) = Show(kit);

        var answer = prompt.PromptAsync("ACME1", "Acme Engineering Ltd");
        await kit.WaitAsync(() => !prompt.IsBusy && Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled);

        var hold = kit.Simulator.HoldRequests();
        try
        {
            Click(prompt, XeroContactLinkPrompt.CreateButtonName);
            await kit.WaitAsync(() => kit.Simulator.InFlight == 1);
            Assert.Equal("Creating 'Acme Engineering Ltd' in Xero…", Text(prompt, XeroContactLinkPrompt.StatusName));
            Assert.False(Button(prompt, XeroContactLinkPrompt.CancelButtonName).IsEnabled);
            Assert.False(Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled);

            Click(prompt, XeroContactLinkPrompt.CancelButtonName);
            Assert.True(prompt.IsVisible);
        }
        finally
        {
            hold.Dispose();
        }

        var link = await WaitForAsync(kit, answer);
        Assert.NotNull(link);
        Assert.Equal(XeroContactLinker.LinkedByCreated, link.LinkedBy);
        Assert.Single(kit.LiveContacts);
        kit.AssertNoViolations();
        window.Close();
    }

    [Fact]
    public void Failures_AreDescribedAsWhatHappenedAndWhatToDo()
    {
        Assert.Equal("Could not link the contact: Xero has no contact X.", XeroContactLinkPrompt.DescribeFailure(ConnectorResult<int>.Rejected("Xero has no contact X."), "link the contact"));
        Assert.StartsWith("Could not search Xero: Xero needs to be re-authorised", XeroContactLinkPrompt.DescribeFailure(ConnectorResult<int>.Reauthorise(), "search Xero"), StringComparison.Ordinal);
        Assert.Contains("never makes a second contact", XeroContactLinkPrompt.DescribeFailure(ConnectorResult<int>.Unknown(), "create the contact in Xero"), StringComparison.Ordinal);
        Assert.Equal("30 days after the invoice date", CustomersSuppliersView.DescribePaymentTerms(30, "DAYSAFTERBILLDATE"));
        Assert.Equal("a day of the following month (see Xero for the day)", CustomersSuppliersView.DescribePaymentTerms(null, "OFFOLLOWINGMONTH"));
        Assert.Equal("none set in Xero", CustomersSuppliersView.DescribePaymentTerms(null, null));
        Assert.Equal(
            "Old Co — Similar name · contact number OLD01 · supplier in Xero · archived in Xero — restore it there to link it",
            XeroContactLinkPrompt.DescribeCandidate(new XeroContactCandidate("c-1", "Old Co", null, "OLD01", null, false, true, "ARCHIVED", XeroContactMatcher.MatchedOnSimilarName)));
    }

    private static (Window Window, XeroContactLinkPrompt Prompt) Show(ContactLinkTestKit kit)
    {
        var prompt = new XeroContactLinkPrompt(kit.Linker);
        var window = new Window { Content = new Panel { Children = { prompt } }, Width = 900, Height = 700 };
        window.Show();
        return (window, prompt);
    }

    private static async Task<T> WaitForAsync<T>(ContactLinkTestKit kit, Task<T> task)
    {
        await kit.WaitAsync(() => task.IsCompleted);
        return await task;
    }
}
