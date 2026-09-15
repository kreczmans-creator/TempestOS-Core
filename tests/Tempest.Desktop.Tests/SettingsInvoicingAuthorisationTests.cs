using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Secrets;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;
using Tempest.Workspace;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 21.6P` (the overnight acceptance campaign, 2026-09-15). Three
/// defects stood between the Settings area and the first live Xero
/// authorisation the programme owes (`WP 21.6`): the client id typed here
/// was stored under <c>Invoicing:ClientId</c>, a key the authoriser never
/// reads (it reads <c>Invoicing:&lt;Provider&gt;:ClientId</c>, `ADR-0151`);
/// <em>Authorise</em> only re-read the stored state and never ran the
/// browser sign-in; and the connector chosen here never reached the host
/// (proven on the Core side by <c>ConnectorSelectionTests</c>). These
/// facts drive the real <see cref="SettingsView"/> through its own
/// controls and read the outcome back through the real
/// <see cref="OAuthAuthoriser"/> and a connector double that records
/// whether the sign-in was actually asked for.
/// </summary>
public sealed class SettingsInvoicingAuthorisationTests
{
    [AvaloniaFact]
    public async Task Save_WithXeroSelected_StoresTheClientIdWhereTheAuthoriserReadsIt()
    {
        var (host, view, store, _) = await BuildAsync(new RecordingAuthorisableConnector("Xero"));
        try
        {
            SelectConnector(view, "Xero");
            TextBox(view, "Invoicing client id").Text = "client-from-settings";
            Click(view, "Save");
            await UntilAsync(async () => await store.GetAsync(SettingsView.ClientIdSecretKeyFor("Xero")) is not null);

            Assert.Equal("client-from-settings", await store.GetAsync("Invoicing:Xero:ClientId"));
            Assert.Null(await store.GetAsync(SettingsView.InvoicingClientIdSecretKey));

            // The proof that matters: the real authoriser, over the same
            // secret store and with nothing configured, now finds the id —
            // "not authorised yet" rather than "not configured".
            var authoriser = new OAuthAuthoriser(
                new OAuthProviderProfile("Xero", new Uri("https://login.example.test/authorize"), new Uri("https://identity.example.test/token"), ["scope"]),
                new ConfigurationBuilder().Build(), store, new NoBrowser(), new HttpClient());
            var access = await authoriser.EnsureAccessTokenAsync();
            Assert.Equal(AccessTokenOutcome.NotAuthorised, access.Outcome);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ALegacyProviderlessClientId_IsShown_AndMigratedOntoTheProvidersKeyOnSave()
    {
        var store = new InMemorySecretStore();
        await store.SetAsync(SettingsView.InvoicingClientIdSecretKey, "legacy-id");
        await store.SetAsync(SettingsView.InvoicingClientSecretSecretKey, "legacy-secret");
        var (host, view, _, _) = await BuildAsync(new RecordingAuthorisableConnector("Xero"), store, connectorSetting: "Xero");
        try
        {
            Assert.Equal("legacy-id", TextBox(view, "Invoicing client id").Text);
            Assert.Equal("(unchanged)", TextBox(view, "Invoicing client secret").Watermark);

            Click(view, "Save");
            await UntilAsync(async () => await store.GetAsync(SettingsView.InvoicingClientIdSecretKey) is null);

            Assert.Equal("legacy-id", await store.GetAsync("Invoicing:Xero:ClientId"));
            Assert.Equal("legacy-secret", await store.GetAsync("Invoicing:Xero:ClientSecret"));
            Assert.Null(await store.GetAsync(SettingsView.InvoicingClientSecretSecretKey));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Authorise_OnARealProviderThatIsNotAuthorised_RunsTheSignIn_AndThenShowsAuthorised()
    {
        var connector = new RecordingAuthorisableConnector("Xero");
        var (host, view, _, _) = await BuildAsync(connector, connectorSetting: "Xero");
        try
        {
            Click(view, "Authorise");
            await UntilAsync(() => Task.FromResult(StatusText(view) == "Authorised."));

            Assert.Equal(1, connector.AuthoriseCalls);
            Assert.Equal("Authorised.", StatusText(view));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Authorise_OnAnAlreadyAuthorisedProvider_OnlyRefreshes_NeverReopensTheBrowser()
    {
        var connector = new RecordingAuthorisableConnector("Xero") { Authorised = true };
        var (host, view, _, _) = await BuildAsync(connector, connectorSetting: "Xero");
        try
        {
            Click(view, "Authorise");
            await UntilAsync(() => Task.FromResult(StatusText(view) == "Authorised."));

            Assert.Equal(0, connector.AuthoriseCalls);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Authorise_WhenTheProviderRefuses_ShowsTheReason_AndStaysNotAuthorised()
    {
        var connector = new RecordingAuthorisableConnector("Xero") { Refusal = "The provider reported: access_denied." };
        var (host, view, _, _) = await BuildAsync(connector, connectorSetting: "Xero");
        try
        {
            Click(view, "Authorise");
            await UntilAsync(() => Task.FromResult(StatusText(view).StartsWith("Authorisation failed.", StringComparison.Ordinal)));

            Assert.Equal(1, connector.AuthoriseCalls);
            Assert.Contains("access_denied", StatusText(view), StringComparison.Ordinal);
            Assert.True(Button(view, "Authorise").IsEnabled);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Authorise_WhenTheSelectedConnectorIsNotTheRunningOne_SaysRestart_AndDoesNotSignIn()
    {
        // This process is running the Fake connector (it is what a fresh
        // install runs); the operator has just chosen Xero.
        var connector = new RecordingAuthorisableConnector("Fake");
        var (host, view, _, _) = await BuildAsync(connector);
        try
        {
            SelectConnector(view, "Xero");
            Click(view, "Authorise");
            await UntilAsync(() => Task.FromResult(StatusText(view).StartsWith("Saved. Restart TempestOS to use Xero", StringComparison.Ordinal)));

            Assert.Equal(0, connector.AuthoriseCalls);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Authorise_WithTheRealFakeConnectorRunning_AndXeroChosen_SaysRestart_NotAuthorised()
    {
        // Found by driving the real application on 2026-09-15: a fresh
        // install runs the Fake connector, which always answers
        // "Authorised."; with Xero freshly chosen the refresh overwrote the
        // restart wording and the status read "Authorised." for a provider
        // that had never been signed in to.
        var (host, view, _, _) = await BuildAsync(new FakeInvoicingConnector());
        try
        {
            SelectConnector(view, "Xero");
            TextBox(view, "Invoicing client id").Text = "client-abc";
            Click(view, "Authorise");
            await UntilAsync(() => Task.FromResult(StatusText(view).StartsWith("Saved. Restart TempestOS to use Xero", StringComparison.Ordinal)));

            await Task.Delay(200);
            Dispatcher.UIThread.RunJobs();
            Assert.StartsWith("Saved. Restart TempestOS to use Xero", StatusText(view), StringComparison.Ordinal);
            Assert.DoesNotContain("Authorised.", StatusText(view), StringComparison.Ordinal);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------

    private static async Task<(WorkspaceHost Host, SettingsView View, InMemorySecretStore Store, IInvoicingConnector Connector)> BuildAsync(
        IInvoicingConnector connector, InMemorySecretStore? store = null, string? connectorSetting = null)
    {
        store ??= new InMemorySecretStore();
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var settingsProvider = (Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));
        var configuration = (IConfigurationProvider)host.Services!.GetService(typeof(IConfigurationProvider));

        if (connectorSetting is not null)
        {
            settingsProvider.RegisterDefinition(new Tempest.Core.Settings.SettingDefinition(InvoicingService.ConnectorConfigurationKey, "Invoicing — connector", "Fake"));
            await settingsProvider.SetValueAsync(InvoicingService.ConnectorConfigurationKey, connectorSetting);
        }

        var theme = new ThemeService(settingsProvider);
        var settings = new UserSettings(settingsProvider);
        var view = new SettingsView(theme, settings, settingsProvider, configuration, "(test)", invoicingConnector: connector, secretStore: store);
        await view.RefreshAsync();
        Dispatcher.UIThread.RunJobs();

        return (host, view, store, connector);
    }

    private static void SelectConnector(SettingsView view, string tag)
    {
        var combo = view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Invoicing connector");
        combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().Single(i => Equals(i.Tag, tag));
        Dispatcher.UIThread.RunJobs();
    }

    private static TextBox TextBox(SettingsView view, string automationName) =>
        view.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == automationName);

    private static Button Button(SettingsView view, string content) =>
        view.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, content));

    private static void Click(SettingsView view, string content)
    {
        Button(view, content).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The authorisation status line sits beside the Authorise button in the same row.</summary>
    private static string StatusText(SettingsView view)
    {
        var row = (StackPanel)Button(view, "Authorise").GetLogicalParent()!;
        return row.Children.OfType<TextBlock>().Single().Text ?? string.Empty;
    }

    private static async Task UntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (await condition())
                return;
            await Task.Delay(10);
        }

        Assert.Fail("The condition was not met within ten seconds.");
    }

    private sealed class NoBrowser : IBrowserLauncher
    {
        public void Open(Uri url)
        {
        }
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }

    /// <summary>A connector double that records whether the interactive sign-in was asked for, and answers the state the way a real provider would after it.</summary>
    private sealed class RecordingAuthorisableConnector(string name) : IInvoicingConnector, IAuthorisableConnector
    {
        public int AuthoriseCalls { get; private set; }

        public bool Authorised { get; set; }

        public string? Refusal { get; set; }

        public string Name => name;

        public Task<OAuthResult> AuthoriseAsync(CancellationToken cancellationToken = default)
        {
            AuthoriseCalls++;
            if (Refusal is not null)
                return Task.FromResult(OAuthResult.Failed(Refusal));

            Authorised = true;
            return Task.FromResult(OAuthResult.Ok());
        }

        public Task<ConnectorAuthorisationState> AuthorisationStateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectorAuthorisationState(Authorised ? ConnectorAuthorisation.Authorised : ConnectorAuthorisation.NotAuthorised));

        public Task<ConnectorResult<CreatedInvoice>> CreateDraftInvoiceAsync(InvoiceRequestSnapshot request, string idempotencyKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ConnectorResult<InvoiceStatusReading>> ReadStatusAsync(string externalId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ConnectorResult<CreatedInvoice?>> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ConnectorResult<IReadOnlyList<ConnectorContact>>> ListContactsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
