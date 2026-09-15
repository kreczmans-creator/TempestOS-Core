using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 20.1B` (`TD-180`): the Commercial section's client editor —
/// <see cref="OrganisationPicker"/> — shows and changes a selected
/// organisation's own <see cref="Organisation.PaymentTerms"/>, and the
/// change survives a close/reopen of the picker.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class OrganisationPickerPaymentTermsTests
{
    [AvaloniaFact]
    public async Task SelectingAnOrganisation_ShowsItsOwnTerms_ChangingAndSaving_PersistsAcrossAReopen()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var organisations = Resolve<IOrganisationCatalog>(host);
            await organisations.RegisterAsync(
                "ORG-TERMS-PICKER", new Organisation { Reference = "ORG-TERMS-PICKER", Name = "Terms Picker Fixture Ltd" },
                ReferenceProvenance.Unknown);

            var picker = new OrganisationPicker(organisations);
            LayOutStandalone(picker);

            var pickTask = picker.PickAsync();
            await RenderUntilStandaloneAsync(() => picker.GetLogicalDescendants().OfType<ListBoxItem>().Any());

            var list = picker.GetLogicalDescendants().OfType<ListBox>().Single();
            list.SelectedItem = list.ItemsSource!.Cast<ListBoxItem>().Single(i => ((string)i.Content!).Contains("ORG-TERMS-PICKER", StringComparison.Ordinal));

            var termsCombo = picker.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Payment terms");
            Assert.True(termsCombo.IsEnabled);
            Assert.Equal(PaymentTerms.UpFront, ((ComboBoxItem)termsCombo.SelectedItem!).Tag);

            termsCombo.SelectedItem = termsCombo.Items.OfType<ComboBoxItem>().Single(i => Equals(i.Tag, PaymentTerms.Days60));

            var saveButton = picker.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Save payment terms");
            saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilStandaloneAsync(() =>
                organisations.FindByReferenceAsync("ORG-TERMS-PICKER").GetAwaiter().GetResult()!.Definition.PaymentTerms == PaymentTerms.Days60);

            // Cancel this picker instance and open a fresh one — the
            // persisted value, not anything the still-open control
            // happens to remember in memory.
            picker.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Null(await pickTask);

            var reopened = picker.PickAsync();
            await RenderUntilStandaloneAsync(() => picker.GetLogicalDescendants().OfType<ListBoxItem>().Any());

            var listAgain = picker.GetLogicalDescendants().OfType<ListBox>().Single();
            listAgain.SelectedItem = listAgain.ItemsSource!.Cast<ListBoxItem>().Single(i => ((string)i.Content!).Contains("ORG-TERMS-PICKER", StringComparison.Ordinal));

            var termsComboAgain = picker.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Payment terms");
            Assert.Equal(PaymentTerms.Days60, ((ComboBoxItem)termsComboAgain.SelectedItem!).Tag);

            picker.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Null(await reopened);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static T Resolve<T>(WorkspaceHost host) where T : class => (T)host.Services!.GetService(typeof(T));

    private static void LayOutStandalone(Control control)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            control.Measure(new Avalonia.Size(1200, 900));
            control.Arrange(new Avalonia.Rect(0, 0, 1200, 900));
        }
    }

    private static async Task RenderUntilStandaloneAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Dispatcher.UIThread.RunJobs();
    }
}
