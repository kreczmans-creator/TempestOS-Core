using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.Xero.ContactLinkViewTests;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` U2 verifier fixes, round 5: while any organisation look-up is
/// pending, the Xero section of the organisation the form still holds is never
/// rendered again (not by a look-up of a missing record ending, nor by the
/// store-fault re-show); a look-up that faults is shown and leaves the section
/// neither withdrawn nor busy.
/// </summary>
public sealed class ContactLinkLoadRaceTests
{
    [AvaloniaFact]
    public async Task AMissingRecordLookUpEndingWhileAnotherIsPending_DoesNotReRenderThePreviousOrganisation()
    {
        // The verifier's probe, step for step.
        await using var kit = await ContactLinkTestKit.CreateAsync();
        var (_, control, held) = await ShowUnlinkedAcmeAsync(kit);
        var (window, view) = (held.Window, held.View);

        // 2. BRUNL is selected with its FindAsync held.
        var gate = HoldBrunel(kit, control, out var entered);
        var opening = view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => entered());

        // 3. GHOST (not registered) is looked up and ends first.
        await view.SelectAsync("GHOST");
        Assert.Equal("'GHOST' is no longer registered.", view.StatusText);

        // 4-5. ACME1's section is not rendered again: Link to Xero… cannot open its prompt.
        Assert.False(Button(view, CustomersSuppliersView.LinkToXeroName).IsEffectivelyVisible);
        Click(view, CustomersSuppliersView.LinkToXeroName);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(view.XeroLinkPrompt!.IsVisible);

        // 6. BRUNL loads; no ACME1 prompt over its form, and its own section is rendered.
        gate.SetResult();
        await opening;
        await kit.WaitAsync(() => view.EditingRecordId == "BRUNL" && Button(view, CustomersSuppliersView.LinkToXeroName).IsEffectivelyVisible && !view.IsXeroBusy);
        Assert.False(view.XeroLinkPrompt.IsVisible);
        control.Intercept = null;
        window.Close();
    }

    [AvaloniaFact]
    public async Task AFaultingLookUpEndingWhileAnotherIsPending_DoesNotReRenderThePreviousOrganisation()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        var (_, control, held) = await ShowUnlinkedAcmeAsync(kit);
        var (window, view) = (held.Window, held.View);

        var gate = HoldBrunel(kit, control, out var entered, faultFor: "GHOST");
        var opening = view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => entered());

        await view.SelectAsync("GHOST");
        Assert.Equal("Could not open 'GHOST': The organisation store is unreadable.", view.StatusText);
        Assert.False(Button(view, CustomersSuppliersView.LinkToXeroName).IsEffectivelyVisible);
        Click(view, CustomersSuppliersView.LinkToXeroName);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(view.XeroLinkPrompt!.IsVisible);

        gate.SetResult();
        await opening;
        await kit.WaitAsync(() => view.EditingRecordId == "BRUNL" && Button(view, CustomersSuppliersView.LinkToXeroName).IsEffectivelyVisible && !view.IsXeroBusy);
        Assert.False(view.XeroLinkPrompt.IsVisible);
        control.Intercept = null;
        window.Close();
    }

    [AvaloniaFact]
    public async Task TheStoreFaultReShow_WhileAnotherOrganisationIsBeingLookedUp_DoesNotShowThePreviousOrganisationsSection()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        await kit.AddOrganisationAsync(reference: "BRUNL", name: "Brunel Fabrication Ltd", customerCode: "BRUNL");
        await kit.Linker.LinkExistingAsync("ACME1", kit.Simulator.SeedContact("Acme Engineering Ltd", contactNumber: "ACME1"));
        await kit.Linker.LinkExistingAsync("BRUNL", kit.Simulator.SeedContact("Brunel Fabrication Ltd", contactNumber: "BRUNL"));
        var (organisations, control) = InterceptingProxy<IOrganisationCatalog>.Wrap(kit.Organisations);
        var (window, view) = await ShowAsync(kit, organisations);
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to Acme Engineering Ltd in Xero", StringComparison.Ordinal) && !view.IsXeroBusy);

        // BRUNL's look-up is held; a refresh of the form's organisation (still ACME1) then faults.
        var gate = HoldBrunel(kit, control, out var entered, faultFor: "ACME1");
        var opening = view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => entered());
        Assert.Equal("ACME1", view.EditingRecordId);

        await view.RefreshXeroAsync(readDetails: true);

        // The section stays withdrawn: no Refresh from Xero for ACME1 under BRUNL's look-up.
        Assert.False(Button(view, CustomersSuppliersView.RefreshFromXeroName).IsEffectivelyVisible);
        Assert.False(Control(view, CustomersSuppliersView.XeroStatusName).IsEffectivelyVisible);

        gate.SetResult();
        await opening;
        await kit.WaitAsync(() => view.EditingRecordId == "BRUNL" && Text(view, CustomersSuppliersView.XeroLinkStateName).StartsWith("Linked to Brunel Fabrication Ltd in Xero", StringComparison.Ordinal) && !view.IsXeroBusy);
        control.Intercept = null;
        window.Close();
    }

    [AvaloniaFact]
    public async Task ALookUpThatFaults_IsShown_AndThePreviousSectionIsRenderedAgain_NotLeftWithdrawnOrBusy()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        var (_, control, held) = await ShowUnlinkedAcmeAsync(kit);
        var (window, view) = (held.Window, held.View);

        control.Intercept = (method, args) =>
            method.Name == nameof(IOrganisationCatalog.FindAsync) && Equals(args![0], "BRUNL")
                ? Task.FromException<IReferenceRecord<Organisation>?>(new IOException("The organisation store is unreadable."))
                : null;

        // Through the list, as a user would (the async-void SelectionChanged path).
        await view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => view.StatusText == "Could not open 'BRUNL': The organisation store is unreadable.");
        await kit.WaitAsync(() => Button(view, CustomersSuppliersView.LinkToXeroName).IsEffectivelyVisible && !view.IsXeroBusy);
        Assert.Equal("ACME1", view.EditingRecordId);

        control.Intercept = null;
        window.Close();
    }

    // Backlog U2: a found record's look-up that ends after a later one was put on the form is stale and never shown.
    [AvaloniaFact]
    public async Task AFoundRecordLookUpEndingAfterALaterOne_NeverReplacesTheLaterOrganisationOnTheForm()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await kit.AddOrganisationAsync();
        await kit.AddOrganisationAsync(reference: "BRUNL", name: "Brunel Fabrication Ltd", customerCode: "BRUNL");
        await kit.AddOrganisationAsync(reference: "CARLO", name: "Carlow Castings Ltd", customerCode: "CARLO");
        var (organisations, control) = InterceptingProxy<IOrganisationCatalog>.Wrap(kit.Organisations);
        var (window, view) = await ShowAsync(kit, organisations);
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => Button(view, CustomersSuppliersView.LinkToXeroName).IsEffectivelyVisible && !view.IsXeroBusy);

        // BRUNL is opened with its look-up held; CARLO is opened and loads first.
        var gate = HoldBrunel(kit, control, out var entered);
        await view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => entered());
        await view.SelectAsync("CARLO");
        await kit.WaitAsync(() => view.EditingRecordId == "CARLO");

        // BRUNL's answer arrives last: the form keeps CARLO, as the list shows, and CARLO's section is rendered.
        gate.SetResult();
        await kit.WaitAsync(() => Button(view, CustomersSuppliersView.LinkToXeroName).IsEffectivelyVisible && !view.IsXeroBusy);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("CARLO", view.EditingRecordId);
        var list = view.GetLogicalDescendants().OfType<Avalonia.Controls.ListBox>().First(l => l.Items.OfType<Avalonia.Controls.ListBoxItem>().Any(i => Equals(i.Tag, "BRUNL")));
        Assert.Equal("CARLO", (list.SelectedItem as Avalonia.Controls.ListBoxItem)?.Tag);
        Assert.Contains(view.GetLogicalDescendants().OfType<Avalonia.Controls.TextBlock>(), t => t.Text == "Edit Carlow Castings Ltd");
        Assert.DoesNotContain(view.GetLogicalDescendants().OfType<Avalonia.Controls.TextBlock>(), t => t.Text == "Edit Brunel Fabrication Ltd");
        control.Intercept = null;
        window.Close();
    }

    private static async Task<(IOrganisationCatalog Organisations, InterceptingProxy<IOrganisationCatalog> Control, (Avalonia.Controls.Window Window, CustomersSuppliersView View) Held)> ShowUnlinkedAcmeAsync(ContactLinkTestKit kit)
    {
        // 1. ACME1 is shown and unlinked.
        await kit.AddOrganisationAsync();
        await kit.AddOrganisationAsync(reference: "BRUNL", name: "Brunel Fabrication Ltd", customerCode: "BRUNL");
        var (organisations, control) = InterceptingProxy<IOrganisationCatalog>.Wrap(kit.Organisations);
        var shown = await ShowAsync(kit, organisations);
        await shown.View.SelectAsync("ACME1");
        await kit.WaitAsync(() => Button(shown.View, CustomersSuppliersView.LinkToXeroName).IsEffectivelyVisible && !shown.View.IsXeroBusy);
        return (organisations, control, shown);
    }

    /// <summary>Holds the first FindAsync("BRUNL") until the returned gate is set; FindAsync(<paramref name="faultFor"/>) faults.</summary>
    private static TaskCompletionSource HoldBrunel(ContactLinkTestKit kit, InterceptingProxy<IOrganisationCatalog> control, out Func<bool> entered, string? faultFor = null)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hasEntered = false;
        entered = () => hasEntered;
        control.Intercept = (method, args) =>
        {
            if (method.Name != nameof(IOrganisationCatalog.FindAsync))
                return null;
            if (Equals(args![0], "BRUNL") && !hasEntered)
                return HeldFindAsync();
            return faultFor is not null && Equals(args[0], faultFor)
                ? Task.FromException<IReferenceRecord<Organisation>?>(new IOException("The organisation store is unreadable."))
                : null;
        };
        return gate;

        async Task<IReferenceRecord<Organisation>?> HeldFindAsync()
        {
            hasEntered = true;
            await gate.Task.ConfigureAwait(true);
            return await kit.Organisations.FindAsync("BRUNL").ConfigureAwait(true);
        }
    }

    private static async Task<(Avalonia.Controls.Window Window, CustomersSuppliersView View)> ShowAsync(ContactLinkTestKit kit, IOrganisationCatalog organisations)
    {
        var view = new CustomersSuppliersView(organisations, kit.Contacts) { XeroContacts = kit.Linker };
        var window = new Avalonia.Controls.Window { Content = view, Width = 1200, Height = 900 };
        window.Show();
        await view.RefreshAsync();
        return (window, view);
    }
}
