using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `v0.25.3` PO request: system settings sit at the foot of the rail; the
/// header's account chip keeps only the person's own settings (job role,
/// appearance, notifications, timesheets); organisation identity and bank
/// details live in Business → Organisation.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SettingsSplitTests
{
    private static readonly string[] AccountSections = ["Job role", "Appearance", "Notifications", "Timesheets"];
    private static readonly string[] SystemSections = ["Persistence root", "Sign-off", "Evidence", "VAT", "Workflow"];

    [AvaloniaFact]
    public async Task TheRailFoot_OpensSystemSettings_TheAccountChip_OpensAccountSettings_AndOrganisationIsInBusiness()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;
            await window.RenderCurrentModuleAsync();

            // The rail's own Settings entry, apart from the five modules.
            var rail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Single();
            var settingsButton = rail.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Settings");
            settingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(() => navigator.Current.Area == ShellArea.SystemSettings);
            await window.RenderCurrentModuleAsync();

            var view = window.GetLogicalDescendants().OfType<SettingsView>().Single();
            Assert.Equal(SettingsPage.System, view.Page);
            Assert.Equal(ShellArea.SystemSettings, rail.HighlightedArea);
            foreach (var section in SystemSections)
                Assert.True(SectionShown(view, section), $"System settings should show {section}.");
            foreach (var section in AccountSections)
                Assert.False(SectionShown(view, section), $"System settings should not show {section}.");
            Assert.False(SectionShown(view, "Organisation identity"));

            // The account chip: the person's own settings only.
            await navigator.GoToModuleAsync(ShellArea.Settings);
            await window.RenderCurrentModuleAsync();
            Assert.Equal(SettingsPage.Account, view.Page);
            foreach (var section in AccountSections)
                Assert.True(SectionShown(view, section), $"Account settings should show {section}.");
            foreach (var section in SystemSections)
                Assert.False(SectionShown(view, section), $"Account settings should not show {section}.");

            // Organisation identity and bank details: Business → Organisation.
            await navigator.GoToModuleAsync(ShellArea.Business);
            await window.RenderCurrentModuleAsync();
            var business = window.GetLogicalDescendants().OfType<BusinessAreaView>().Single();
            business.SelectNode("Organisation");
            await WaitUntilAsync(() => business.GetLogicalDescendants().OfType<OrganisationView>().Any());
            var organisation = business.GetLogicalDescendants().OfType<OrganisationView>().Single();
            Assert.True(SectionShown(organisation, "Organisation identity"));
            Assert.True(SectionShown(organisation, "Bank details (invoice payment details)"));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static bool SectionShown(Control root, string title) =>
        root.GetLogicalDescendants().OfType<TextBlock>()
            .Where(t => t.Text == title)
            .Any(t => t.GetLogicalAncestors().OfType<Control>().TakeWhile(c => !ReferenceEquals(c, root)).All(c => c.IsVisible));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DesktopTestHelpers.Deadline(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition(), "The condition never became true.");
    }
}
