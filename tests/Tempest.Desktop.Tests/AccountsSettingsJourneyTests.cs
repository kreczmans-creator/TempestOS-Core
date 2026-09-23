using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The `WP 19.8B` Desktop acceptance (po-comments.md item 8): through the
/// real window and the real Settings area, the Accounts reading line
/// reads "unavailable" before any refresh has ever happened, and "last at
/// &lt;time&gt; (Fake)" after a scripted Refresh now.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class AccountsSettingsJourneyTests
{
    [AvaloniaFact]
    public async Task Settings_AccountsReadingLine_UnavailableThenLastReadingTime_AfterRefreshNow()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            await RenderUntilAsync(window, () => window.Ready.IsCompleted);

            await host.ShellNavigator!.GoToModuleAsync(ShellArea.Settings);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var settingsView = GetPrivateField<SettingsView>(window, "_settingsView");
            await RenderUntilAsync(window, () => AccountsReadingLine(settingsView) is not null);

            var beforeRefresh = AccountsReadingLine(settingsView)!;
            Assert.StartsWith("Accounts reading: unavailable:", beforeRefresh.Text, StringComparison.Ordinal);

            // Script the fake connector's own accounts reads — modelled on
            // Xero's own shapes (Product Owner, 2026-09-14) — then drive
            // the real Refresh now button.
            var connector = (FakeInvoicingConnector)host.Services!.GetService(typeof(Tempest.Core.Invoicing.IInvoicingConnector));
            connector.ScriptCashPosition([new CashAccountBalance("Business Current Account", new Money(5000m, CurrencyCode.Gbp), DateOnly.FromDateTime(DateTime.UtcNow))]);
            connector.ScriptBillsDue([new BillDue("Acme Ltd", "INV-1", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5), new Money(300m, CurrencyCode.Gbp), "AUTHORISED")]);

            var refreshButton = settingsView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Refresh now"));
            refreshButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () =>
                AccountsReadingLine(settingsView) is { } line && line.Text!.StartsWith("Accounts reading: last at", StringComparison.Ordinal));

            var afterRefresh = AccountsReadingLine(settingsView)!;
            Assert.Contains("(Fake)", afterRefresh.Text, StringComparison.Ordinal);

            // The saved reading is real, on disk, through the same store
            // the read model itself reads — not just a UI-local string.
            var readModel = (IAccountsReadModel)host.Services!.GetService(typeof(IAccountsReadModel));
            var snapshot = await readModel.ReadAsync();
            Assert.True(snapshot.IsAvailable);
            Assert.Single(snapshot.Cash);
            Assert.Single(snapshot.BillsDueWithin30);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static TextBlock? AccountsReadingLine(SettingsView settingsView) =>
        settingsView.GetLogicalDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.Text != null && t.Text.StartsWith("Accounts reading:", StringComparison.Ordinal));

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
            window.Measure(new Size(1900, 1050));
            window.Arrange(new Rect(0, 0, 1900, 1050));
        }
    }
}
