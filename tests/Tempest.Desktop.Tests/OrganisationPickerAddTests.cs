using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Board finding M8: <see cref="OrganisationPicker"/>'s supplier mode says
/// there are no suppliers (not "no organisations") when only customers
/// exist, and its Add row asks for a <b>customer code</b> — used as typed
/// for the record id, reference and code, refused rather than quietly
/// replaced, and named in the "Added …" line.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class OrganisationPickerAddTests
{
    [AvaloniaFact]
    public async Task SupplierMode_SaysNoSuppliers_AndAddUsesTheTypedCustomerCode_OrRefusesIt()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var organisations = (IOrganisationCatalog)host.Services!.GetService(typeof(IOrganisationCatalog));
            await organisations.RegisterAsync(
                "CUSTA", new Organisation { Reference = "CUSTA", Name = "Customer Only Ltd", CustomerCode = "CUSTA", Roles = [PartyKind.Customer] },
                ReferenceProvenance.Unknown);

            var picker = new OrganisationPicker(organisations);
            var pick = picker.PickSupplierAsync();
            await RenderUntilAsync(() => Texts(picker).Any(t => t.StartsWith("No ", StringComparison.Ordinal)));
            Assert.Contains(Texts(picker), t => t.StartsWith("No suppliers registered yet", StringComparison.Ordinal));
            Assert.DoesNotContain(Texts(picker), t => t.StartsWith("No organisations registered", StringComparison.Ordinal));

            var code = picker.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Customer code");
            var name = picker.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "New organisation name");
            var add = picker.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Add organisation");

            // ---- Typed: used as typed, for id, reference and code ----
            code.Text = "steel";
            name.Text = "Steel Supplies Ltd";
            add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(() => Texts(picker).Any(t => t.StartsWith("Added", StringComparison.Ordinal)));
            Assert.Contains("Added 'Steel Supplies Ltd' with customer code STEEL.", Texts(picker));
            var steel = (await organisations.FindAsync("STEEL"))!.Definition;
            Assert.Equal("STEEL", steel.Reference);
            Assert.Equal("STEEL", steel.CustomerCode);
            Assert.Equal(OrganisationTradingType.Supplier, steel.TradingType);

            // ---- Taken or malformed: refused, never quietly replaced ----
            code.Text = "CUSTA";
            name.Text = "Another Ltd";
            add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(() => Texts(picker).Any(t => t.Contains("already in use", StringComparison.Ordinal)));
            Assert.Contains("Customer code 'CUSTA' is already in use.", Texts(picker));

            code.Text = "AB";
            add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(() => Texts(picker).Any(t => t.Contains("exactly 5 characters", StringComparison.Ordinal)));
            Assert.Equal(2, (await organisations.ListAsync()).Count);

            // ---- Blank: suggested from the name, and named in the confirmation ----
            code.Text = string.Empty;
            add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(() => Texts(picker).Any(t => t.StartsWith("Added 'Another Ltd'", StringComparison.Ordinal)));
            var another = (await organisations.ListAsync()).Single(r => r.Definition.Name == "Another Ltd");
            Assert.Equal(another.Id, another.Definition.CustomerCode);
            Assert.Contains($"Added 'Another Ltd' with customer code {another.Id}.", Texts(picker));

            picker.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Null(await pick);
        }
        finally
        {
            Dispatcher.UIThread.RunJobs();
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static List<string> Texts(Control root) =>
        [.. root.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty)];

    private static async Task RenderUntilAsync(Func<bool> condition)
    {
        var deadline = Deadline(20);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition(), "The picker never reached the expected state.");
    }
}
