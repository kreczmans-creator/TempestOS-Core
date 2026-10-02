using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.Xero.ContactLinkViewTests;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` U2 verifier fixes: Create in Xero only after a search that
/// answered; a write's link belongs to the organisation it was started for;
/// the prompt is modal within the view and outcomes never land on another
/// organisation; any fault is shown, never thrown into the dispatcher;
/// Refresh and Unlink wait for each other; search token sources are disposed.
/// </summary>
public sealed class ContactLinkGuardTests
{
    [AvaloniaFact]
    public async Task SearchFailed_CreateStaysDisabled_UntilASearchAnswers_SoAVatMatchIsSeenFirst()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync(vat: "GB123456789");
        var existing = await kit.SeedContactAsync(new JsonObject { ["Name"] = "Acme Engineering Limited", ["TaxNumber"] = "GB123456789" });
        kit.Simulator.Inject(new XeroFault(XeroFaultKind.TransportFailure, PathContains: "Contacts", Times: 1));
        var (window, prompt) = ShowPrompt(kit);

        _ = prompt.PromptAsync("ACME1", "Acme Engineering Ltd");
        await kit.WaitAsync(() => !prompt.IsBusy && Text(prompt, XeroContactLinkPrompt.StatusName).StartsWith("Could not search Xero", StringComparison.Ordinal));
        Assert.False(Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled);

        Click(prompt, XeroContactLinkPrompt.SearchAgainButtonName);
        await kit.WaitAsync(() => !prompt.IsBusy && prompt.Candidates.Count == 1);
        Assert.Equal(existing, prompt.Candidates[0].ContactId);
        Assert.Equal(XeroContactMatcher.MatchedOnVatNumber, prompt.Candidates[0].MatchedOn);
        Assert.True(Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled);
        Assert.Single(kit.LiveContacts);
        kit.AssertNoViolations();
        window.Close();
    }

    [AvaloniaFact]
    public async Task PromptForAnotherOrganisationWhileACreateRuns_IsRefused_AndTheCreateCompletesOnlyItsOwnPrompt()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        await kit.AddOrganisationAsync(reference: "BRUNL", name: "Brunel Fabrication Ltd", customerCode: "BRUNL");
        var (window, prompt) = ShowPrompt(kit);

        var first = prompt.PromptAsync("ACME1", "Acme Engineering Ltd");
        await kit.WaitAsync(() => !prompt.IsBusy && Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled);

        Task<XeroLink?> second;
        using (kit.Simulator.HoldRequests())
        {
            Click(prompt, XeroContactLinkPrompt.CreateButtonName);
            await kit.WaitAsync(() => kit.Simulator.InFlight == 1);

            second = prompt.PromptAsync("BRUNL", "Brunel Fabrication Ltd");
            Assert.True(second.IsCompleted);
            Assert.Null(await second);
            Assert.True(prompt.IsVisible);
            Assert.Equal("Creating 'Acme Engineering Ltd' in Xero…", Text(prompt, XeroContactLinkPrompt.StatusName));
            Assert.False(first.IsCompleted);
        }

        await kit.WaitAsync(() => first.IsCompleted);
        var link = await first;
        Assert.NotNull(link);
        Assert.Equal(link.XeroId, (await kit.Linker.FindLinkAsync("ACME1"))!.XeroId);
        Assert.Null(await kit.Linker.FindLinkAsync("BRUNL"));
        Assert.False(prompt.IsVisible);
        kit.AssertNoViolations();
        window.Close();
    }

    [AvaloniaFact]
    public async Task OtherOrganisationOpenedWhileACreateRuns_ItIsNotLinked_AndTheOutcomeIsNotShownOnIt()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        await kit.AddOrganisationAsync(reference: "BRUNL", name: "Brunel Fabrication Ltd", customerCode: "BRUNL");
        var (window, view) = await kit.ShowAsync();
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Button(view, CustomersSuppliersView.LinkToXeroName).IsVisible && !view.IsXeroBusy);
        Click(view, CustomersSuppliersView.LinkToXeroName);
        var prompt = view.XeroLinkPrompt!;
        await kit.WaitAsync(() => prompt.IsVisible && !prompt.IsBusy && Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled);

        using (kit.Simulator.HoldRequests())
        {
            Click(prompt, XeroContactLinkPrompt.CreateButtonName);
            await kit.WaitAsync(() => kit.Simulator.InFlight == 1);

            // Behind the modal prompt only programmatically; the Link to Xero… press is refused while the create runs.
            await view.SelectAsync("BRUNL");
            await kit.WaitAsync(() => Button(view, CustomersSuppliersView.LinkToXeroName).IsVisible && !view.IsXeroBusy);
            Click(view, CustomersSuppliersView.LinkToXeroName);
        }

        await kit.WaitAsync(() => kit.Simulator.InFlight == 0 && !prompt.IsBusy && !prompt.IsVisible);
        await SettleAsync(kit, view);

        Assert.Equal("BRUNL", view.EditingRecordId);
        Assert.NotNull(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.Null(await kit.Linker.FindLinkAsync("BRUNL"));
        Assert.StartsWith("Not linked", Text(view, CustomersSuppliersView.XeroLinkStateName), StringComparison.Ordinal);
        Assert.Equal(string.Empty, Text(view, CustomersSuppliersView.XeroStatusName));
        kit.AssertNoViolations();
        window.Close();
    }

    [AvaloniaFact]
    public async Task ThePromptIsModalWithinTheView_AndAnOutcomeNeverLandsOnAnotherOrganisation()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        await kit.AddOrganisationAsync(reference: "BRUNL", name: "Brunel Fabrication Ltd", customerCode: "BRUNL");
        var (window, view) = await kit.ShowAsync();
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Button(view, CustomersSuppliersView.LinkToXeroName).IsVisible && !view.IsXeroBusy);
        Assert.True(view.IsBodyEnabled);
        Assert.False(Control(view, CustomersSuppliersView.XeroPromptScrimName).IsVisible);

        Click(view, CustomersSuppliersView.LinkToXeroName);
        var prompt = view.XeroLinkPrompt!;
        await kit.WaitAsync(() => prompt.IsVisible && !prompt.IsBusy && Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled);

        // A full-size scrim takes the hits and the list and form behind it are disabled.
        Assert.False(view.IsBodyEnabled);
        var scrim = Control(view, CustomersSuppliersView.XeroPromptScrimName);
        Assert.True(scrim.IsVisible);
        Assert.True(scrim.IsHitTestVisible);

        // Even if the organisation shown changes under it (programmatically), the outcome is not written onto the new one.
        await view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => !view.IsXeroBusy);
        Click(prompt, XeroContactLinkPrompt.CreateButtonName);
        await kit.WaitAsync(() => !prompt.IsVisible);
        await SettleAsync(kit, view);

        Assert.True(view.IsBodyEnabled);
        Assert.False(scrim.IsVisible);
        Assert.Equal("BRUNL", view.EditingRecordId);
        Assert.StartsWith("Not linked", Text(view, CustomersSuppliersView.XeroLinkStateName), StringComparison.Ordinal);
        Assert.Equal(string.Empty, Text(view, CustomersSuppliersView.XeroStatusName));
        kit.AssertNoViolations();
        window.Close();
    }

    [AvaloniaFact]
    public async Task OtherOrganisationOpenedWhileAnUnlinkRuns_TheUnlinkedMessageIsNotShownOnIt()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        await kit.AddOrganisationAsync(reference: "BRUNL", name: "Brunel Fabrication Ltd", customerCode: "BRUNL");
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd", contactNumber: "ACME1");
        await kit.Linker.LinkExistingAsync("ACME1", contactId);
        var (window, view) = await kit.ShowAsync();
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to Acme Engineering Ltd in Xero", StringComparison.Ordinal) && !view.IsXeroBusy);

        var gate = kit.SecretStore.HoldNextGet();
        Click(view, CustomersSuppliersView.UnlinkFromXeroName);
        await kit.WaitAsync(() => view.IsXeroBusy);
        Assert.False(Button(view, CustomersSuppliersView.RefreshFromXeroName).IsEnabled);
        Assert.False(Button(view, CustomersSuppliersView.UnlinkFromXeroName).IsEnabled);

        await view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Not linked", StringComparison.Ordinal) && !view.IsXeroBusy);
        gate.SetResult();
        await kit.WaitAsync(() => kit.Audit.Rows.Any(r => r.Action == XeroContactLinker.AuditLinkUnlinked));
        await kit.WaitAsync(() => !view.IsXeroBusy);

        Assert.Equal("BRUNL", view.EditingRecordId);
        Assert.Null(await kit.Linker.FindLinkAsync("ACME1"));
        Assert.Equal(string.Empty, Text(view, CustomersSuppliersView.XeroStatusName));
        window.Close();
    }

    [AvaloniaFact]
    public async Task WhileADetailsReadRuns_RefreshAndUnlinkAreDisabled()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd", contactNumber: "ACME1");
        await kit.Linker.LinkExistingAsync("ACME1", contactId);
        var (window, view) = await kit.ShowAsync();

        using (kit.Simulator.HoldRequests())
        {
            await view.SelectAsync("ACME1");
            await kit.WaitAsync(() => kit.Simulator.InFlight == 1);
            Assert.True(view.IsXeroBusy);
            Assert.False(Button(view, CustomersSuppliersView.RefreshFromXeroName).IsEnabled);
            Assert.False(Button(view, CustomersSuppliersView.UnlinkFromXeroName).IsEnabled);
        }

        await kit.WaitAsync(() => !view.IsXeroBusy);
        Assert.True(Button(view, CustomersSuppliersView.RefreshFromXeroName).IsEnabled);
        Assert.True(Button(view, CustomersSuppliersView.UnlinkFromXeroName).IsEnabled);
        kit.AssertNoViolations();
        window.Close();
    }

    [AvaloniaFact]
    public async Task AStoreFault_IsShownAsAReason_NotThrownIntoTheDispatcher()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd", contactNumber: "ACME1");
        await kit.Linker.LinkExistingAsync("ACME1", contactId);
        var (window, view) = await kit.ShowAsync();
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to", StringComparison.Ordinal) && !view.IsXeroBusy);

        kit.SecretStore.Fault = new IOException("The secret store is unreadable.");
        Click(view, CustomersSuppliersView.UnlinkFromXeroName);
        await kit.WaitAsync(() => !view.IsXeroBusy && Text(view, CustomersSuppliersView.XeroStatusName).Length > 0);
        Assert.Equal("Could not unlink: The secret store is unreadable.", Text(view, CustomersSuppliersView.XeroStatusName));

        Click(view, CustomersSuppliersView.RefreshFromXeroName);
        await kit.WaitAsync(() => !view.IsXeroBusy && Text(view, CustomersSuppliersView.XeroStatusName).StartsWith("Could not read", StringComparison.Ordinal));
        Assert.Equal("Could not read the Xero link: The secret store is unreadable.", Text(view, CustomersSuppliersView.XeroStatusName));
        window.Close();
    }

    [AvaloniaFact]
    public async Task AFaultDuringASearchOrACreate_IsShown_AndThePromptIsNotLeftBusy()
    {
        var linker = new FaultingLinker();
        var (window, prompt) = ShowPrompt(linker);

        var answer = prompt.PromptAsync("ACME1", "Acme Engineering Ltd");
        await DesktopTestHelpers.WaitUntilAsync(() => !prompt.IsBusy && prompt.StatusText!.StartsWith("Could not", StringComparison.Ordinal), 20);
        Assert.Equal("Could not search Xero: store defect (search)", prompt.StatusText);
        Assert.False(Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled);

        linker.FailSearch = false;
        Click(prompt, XeroContactLinkPrompt.SearchAgainButtonName);
        await DesktopTestHelpers.WaitUntilAsync(() => !prompt.IsBusy && Button(prompt, XeroContactLinkPrompt.CreateButtonName).IsEnabled, 20);

        Click(prompt, XeroContactLinkPrompt.CreateButtonName);
        await DesktopTestHelpers.WaitUntilAsync(() => !prompt.IsBusy && prompt.StatusText!.StartsWith("Could not create", StringComparison.Ordinal), 20);
        Assert.Equal("Could not create the contact in Xero: store defect (create)", prompt.StatusText);
        Assert.True(prompt.IsVisible);
        Assert.False(answer.IsCompleted);
        Assert.True(Button(prompt, XeroContactLinkPrompt.CancelButtonName).IsEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public async Task EachSearchsTokenSource_IsDisposedOnceItsSearchEnds()
    {
        var linker = new FaultingLinker { FailSearch = false };
        var (window, prompt) = ShowPrompt(linker);

        _ = prompt.PromptAsync("ACME1", "Acme Engineering Ltd");
        await DesktopTestHelpers.WaitUntilAsync(() => !prompt.IsBusy && linker.SearchTokens.Count == 1, 20);
        Click(prompt, XeroContactLinkPrompt.SearchAgainButtonName);
        await DesktopTestHelpers.WaitUntilAsync(() => !prompt.IsBusy && linker.SearchTokens.Count == 2, 20);

        foreach (var token in linker.SearchTokens)
            Assert.Throws<ObjectDisposedException>(() => token.WaitHandle);

        Click(prompt, XeroContactLinkPrompt.CancelButtonName);
        window.Close();
    }

    /// <summary>Lets every queued continuation (a prompt's answer reaching the view, its refresh) run out.</summary>
    private static async Task SettleAsync(ContactLinkTestKit kit, CustomersSuppliersView view)
    {
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(10);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        await kit.WaitAsync(() => !view.IsXeroBusy);
    }

    private static (Window Window, XeroContactLinkPrompt Prompt) ShowPrompt(ContactLinkTestKit kit) => ShowPrompt(kit.Linker);

    private static (Window Window, XeroContactLinkPrompt Prompt) ShowPrompt(IXeroContactLinker linker)
    {
        var prompt = new XeroContactLinkPrompt(linker);
        var window = new Window { Content = new Panel { Children = { prompt } }, Width = 900, Height = 700 };
        window.Show();
        return (window, prompt);
    }

    /// <summary>A linker (a fake of the §12 interface) whose search and create fail with a store defect — not an <see cref="InvalidOperationException"/>.</summary>
    private sealed class FaultingLinker : IXeroContactLinker
    {
        public bool FailSearch { get; set; } = true;

        public List<CancellationToken> SearchTokens { get; } = [];

        public Task<XeroLink?> FindLinkAsync(string organisationReference, CancellationToken cancellationToken = default) =>
            Task.FromResult<XeroLink?>(null);

        public async Task<ConnectorResult<IReadOnlyList<XeroContactCandidate>>> FindCandidatesAsync(string organisationReference, CancellationToken cancellationToken = default)
        {
            SearchTokens.Add(cancellationToken);
            await Task.Yield();
            if (FailSearch)
                throw new IOException("store defect (search)");
            return ConnectorResult<IReadOnlyList<XeroContactCandidate>>.Ok([]);
        }

        public Task<ConnectorResult<XeroLink>> LinkExistingAsync(string organisationReference, string contactId, CancellationToken cancellationToken = default) =>
            throw new IOException("store defect (link)");

        public async Task<ConnectorResult<XeroLink>> CreateAsync(string organisationReference, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new IOException("store defect (create)");
        }

        public Task<ConnectorResult<XeroContactDetails>> ReadDetailsAsync(string organisationReference, CancellationToken cancellationToken = default) =>
            throw new IOException("store defect (details)");
    }
}
