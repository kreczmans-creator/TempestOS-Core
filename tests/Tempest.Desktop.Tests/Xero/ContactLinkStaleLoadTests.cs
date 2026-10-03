using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.Xero.ContactLinkViewTests;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` C1 U2 verifier fixes: an organisation look-up superseded while it
/// is still awaiting (its code suggestion, its contacts) writes nothing more,
/// so the form never shows one organisation's details under another's
/// heading; New supersedes a pending look-up too.
/// </summary>
public sealed class ContactLinkStaleLoadTests
{
    // The verifier's probe: BRUNL (no customer code, Bristol) is held in its code suggestion while CARLO (Leeds) loads.
    [AvaloniaFact]
    public async Task ALookUpSupersededDuringItsCodeSuggestion_NeverWritesItsDetailsOverTheLaterOrganisation()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await RegisterBrunelAndCarlowAsync(kit);
        var (organisations, control) = InterceptingProxy<IOrganisationCatalog>.Wrap(kit.Organisations);
        var (window, view) = await ShowAsync(kit, organisations, kit.Contacts);
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => view.EditingRecordId == "ACME1" && !view.IsXeroBusy);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = false;
        control.Intercept = (method, args) =>
        {
            if (method.Name != nameof(IOrganisationCatalog.SuggestCustomerCodeAsync) || !Equals(args![0], "Brunel Fabrication Ltd") || entered)
                return null;
            entered = true;
            return HeldSuggestionAsync(args);
        };

        await view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => entered);
        await view.SelectAsync("CARLO");
        await kit.WaitAsync(() => view.EditingRecordId == "CARLO" && Box(view, "Town").Text == "Leeds" && !view.IsXeroBusy);

        gate.SetResult();
        await Task.Delay(50);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("CARLO", view.EditingRecordId);
        AssertCarlowOnTheForm(view);
        control.Intercept = null;
        window.Close();

        async Task<string> HeldSuggestionAsync(object?[] args)
        {
            await gate.Task.ConfigureAwait(true);
            return await kit.Organisations.SuggestCustomerCodeAsync((string)args[0]!, (string?)args[1]).ConfigureAwait(true);
        }
    }

    // The same, with BRUNL's contacts look-up held instead.
    [AvaloniaFact]
    public async Task ALookUpSupersededDuringItsContactsLookUp_NeverWritesItsContactsOverTheLaterOrganisation()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await RegisterBrunelAndCarlowAsync(kit);
        var (contacts, control) = InterceptingProxy<IContactCatalog>.Wrap(kit.Contacts);
        var (window, view) = await ShowAsync(kit, kit.Organisations, contacts);
        await view.SelectAsync("ACME1");
        await kit.WaitAsync(() => view.EditingRecordId == "ACME1" && !view.IsXeroBusy);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = false;
        control.Intercept = (method, args) =>
        {
            if (method.Name != nameof(IContactCatalog.FindForOrganisationAsync) || !Equals(args![0], "BRUNL") || entered)
                return null;
            entered = true;
            return HeldContactsAsync();
        };

        await view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => entered);
        await view.SelectAsync("CARLO");
        await kit.WaitAsync(() => view.EditingRecordId == "CARLO" && ContactNames(view).Contains("Carla Carlow") && !view.IsXeroBusy);

        gate.SetResult();
        await Task.Delay(50);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("CARLO", view.EditingRecordId);
        AssertCarlowOnTheForm(view);
        Assert.Equal(["Carla Carlow"], ContactNames(view));
        control.Intercept = null;
        window.Close();

        async Task<IReadOnlyList<IReferenceRecord<Contact>>> HeldContactsAsync()
        {
            await gate.Task.ConfigureAwait(true);
            return await kit.Contacts.FindForOrganisationAsync("BRUNL").ConfigureAwait(true);
        }
    }

    // Defect 4: New pressed while an organisation's look-up is pending keeps the blank form.
    [AvaloniaFact]
    public async Task NewPressedWhileALookUpIsPending_KeepsTheBlankNewOrganisationForm()
    {
        await using var kit = await ContactLinkTestKit.CreateAsync();
        await RegisterBrunelAndCarlowAsync(kit);
        var (organisations, control) = InterceptingProxy<IOrganisationCatalog>.Wrap(kit.Organisations);
        var (window, view) = await ShowAsync(kit, organisations, kit.Contacts);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = false;
        control.Intercept = (method, args) =>
        {
            if (method.Name != nameof(IOrganisationCatalog.FindAsync) || !Equals(args![0], "BRUNL") || entered)
                return null;
            entered = true;
            return HeldFindAsync();
        };

        var opening = view.SelectAsync("BRUNL");
        await kit.WaitAsync(() => entered);
        Click(view, "New organisation");

        gate.SetResult();
        await opening;
        await Task.Delay(50);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Null(view.EditingRecordId);
        Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "New organisation");
        Assert.DoesNotContain(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Edit Brunel Fabrication Ltd");
        Assert.True(string.IsNullOrEmpty(Box(view, "Legal name").Text));
        Assert.True(string.IsNullOrEmpty(Box(view, "Town").Text));
        Assert.False(Button(view, CustomersSuppliersView.LinkToXeroName).IsEffectivelyVisible);
        Assert.False(view.IsXeroBusy);
        control.Intercept = null;
        window.Close();

        async Task<IReferenceRecord<Organisation>?> HeldFindAsync()
        {
            await gate.Task.ConfigureAwait(true);
            return await kit.Organisations.FindAsync("BRUNL").ConfigureAwait(true);
        }
    }

    private static async Task RegisterBrunelAndCarlowAsync(ContactLinkTestKit kit)
    {
        await kit.AddOrganisationAsync();
        await RegisterAsync(kit, new Organisation
        {
            Reference = "BRUNL",
            Name = "Brunel Fabrication Ltd",
            CustomerCode = null,
            Address = new PostalAddress("1 Dock Road", Town: "Bristol", Postcode: "BS1 1AA", CountryCode: "GB"),
            TaxRegistration = "GB111111111",
            TelephoneNumber = "0117 000 0000",
            EmailAddress = "accounts@brunel.example",
            Website = "https://brunel.example",
            Status = RelationshipStatus.Active,
        });
        await RegisterAsync(kit, new Organisation
        {
            Reference = "CARLO",
            Name = "Carlow Castings Ltd",
            CustomerCode = "CARLO",
            Address = new PostalAddress("2 Mill Lane", Town: "Leeds", Postcode: "LS1 1AA", CountryCode: "GB"),
            TaxRegistration = "GB222222222",
            TelephoneNumber = "0113 000 0000",
            EmailAddress = "accounts@carlow.example",
            Website = "https://carlow.example",
            Status = RelationshipStatus.Active,
        });
        await kit.Contacts.RegisterAsync("BRUNL-C1", new Contact { Reference = "BRUNL-C1", OrganisationReference = "BRUNL", Name = "Bea Brunel", IsPrimaryContact = true }, CustomersSuppliersView.Provenance);
        await kit.Contacts.RegisterAsync("CARLO-C1", new Contact { Reference = "CARLO-C1", OrganisationReference = "CARLO", Name = "Carla Carlow", IsPrimaryContact = true }, CustomersSuppliersView.Provenance);
    }

    private static async Task RegisterAsync(ContactLinkTestKit kit, Organisation organisation)
    {
        organisation = organisation with { Roles = organisation.RolesFor(OrganisationTradingType.Customer) };
        await kit.Organisations.RegisterAsync(organisation.Reference, organisation, CustomersSuppliersView.Provenance);
    }

    private static void AssertCarlowOnTheForm(CustomersSuppliersView view)
    {
        Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Edit Carlow Castings Ltd");
        Assert.Equal("Carlow Castings Ltd", Box(view, "Legal name").Text);
        Assert.Equal("CARLO", Box(view, "Customer code").Text);
        Assert.Equal("2 Mill Lane", Box(view, "Address line 1").Text);
        Assert.Equal("Leeds", Box(view, "Town").Text);
        Assert.Equal("LS1 1AA", Box(view, "Postcode").Text);
        Assert.Equal("GB222222222", Box(view, "VAT number").Text);
        Assert.Equal("0113 000 0000", Box(view, "Phone").Text);
        Assert.Equal("accounts@carlow.example", Box(view, "Email").Text);
        Assert.Equal("https://carlow.example", Box(view, "Website").Text);
    }

    private static TextBox Box(Control root, string name) =>
        root.GetLogicalDescendants().OfType<TextBox>().Single(b => AutomationProperties.GetName(b) == name);

    private static List<string> ContactNames(CustomersSuppliersView view) =>
        [.. (List(view, "Contacts").ItemsSource?.OfType<ListBoxItem>() ?? []).Select(i => (i.Content as string ?? string.Empty).Split(" · ")[0])];

    private static async Task<(Window Window, CustomersSuppliersView View)> ShowAsync(ContactLinkTestKit kit, IOrganisationCatalog organisations, IContactCatalog contacts)
    {
        var view = new CustomersSuppliersView(organisations, contacts) { XeroContacts = kit.Linker };
        var window = new Window { Content = view, Width = 1200, Height = 900 };
        window.Show();
        await view.RefreshAsync();
        return (window, view);
    }
}
