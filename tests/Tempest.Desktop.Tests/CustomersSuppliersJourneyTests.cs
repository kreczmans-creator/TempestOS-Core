using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Quotations;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Product Owner decision 2026-10-01, through the real window: Business →
/// Customers &amp; Suppliers adds an organisation (customer code suggested
/// from its name, editable, unique) and its contacts (§§1–2); New Project
/// → Client is a drop-down of that same list, and a client with a customer
/// code makes the project <c>CUSTOMER-PROJECTREF</c>, its first quotation
/// <c>CUSTOMER-PROJECTREF-Q-001</c> (§3, `ADR-0156`).
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CustomersSuppliersJourneyTests
{
    [AvaloniaFact]
    public async Task CustomersAndSuppliers_AddOrganisationWithSuggestedCode_AddContact_DuplicateCodeRefused()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var organisations = (IOrganisationCatalog)host.Services!.GetService(typeof(IOrganisationCatalog));
            var contacts = (IContactCatalog)host.Services!.GetService(typeof(IContactCatalog));

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;

            await navigator.GoToModuleAsync(ShellArea.Business);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            window.GetLogicalDescendants().OfType<BusinessAreaView>().Single().SelectNode("Customers & Suppliers");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<CustomersSuppliersView>().Any(v => v.IsVisible));

            var view = window.GetLogicalDescendants().OfType<CustomersSuppliersView>().Single();
            var name = Box(view, "Legal name");
            var code = Box(view, "Customer code");

            // ---- The code is suggested from the name ----
            name.Text = "Acme Engineering Ltd";
            await RenderUntilAsync(window, () => code.Text == "ACMEE");
            Assert.Equal("ACMEE", code.Text);

            var type = view.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Type");
            type.SelectedItem = type.Items.OfType<ComboBoxItem>().Single(i => Equals(i.Tag, OrganisationTradingType.Both));
            Box(view, "Address line 1").Text = "1 High Street";
            Box(view, "Town").Text = "Bristol";
            Box(view, "Postcode").Text = "BS1 1AA";
            Box(view, "Company number").Text = "01234567";
            Box(view, "VAT number").Text = "GB123456789";
            Box(view, "Phone").Text = "0117 000 0000";
            Box(view, "Email").Text = "office@acme.example";
            Box(view, "Website").Text = "acme.example";

            ClickButton(view, "Save organisation");
            await RenderUntilAsync(window, () => organisations.FindByCustomerCodeAsync("ACMEE").GetAwaiter().GetResult() is not null);

            var saved = (await organisations.FindByCustomerCodeAsync("ACMEE"))!.Definition;
            Assert.Equal("Acme Engineering Ltd", saved.Name);
            Assert.Equal(OrganisationTradingType.Both, saved.TradingType);
            Assert.Equal("1 High Street", saved.Address!.Line1);
            Assert.Equal("BS1 1AA", saved.Address.Postcode);
            Assert.Equal("01234567", saved.RegistrationNumber);
            Assert.Equal("GB123456789", saved.TaxRegistration);
            Assert.Equal("0117 000 0000", saved.TelephoneNumber);
            Assert.Equal("office@acme.example", saved.EmailAddress);
            Assert.Equal("acme.example", saved.Website);

            // ---- A contact at that organisation ----
            await RenderUntilAsync(window, () => view.EditingRecordId is not null);
            Box(view, "Contact name").Text = "Pat Client";
            Box(view, "Contact role").Text = "Project Manager";
            Box(view, "Contact email").Text = "pat@acme.example";
            Box(view, "Contact phone").Text = "07700 900000";
            ClickButton(view, "Add contact");
            await RenderUntilAsync(window, () => contacts.FindForOrganisationAsync(saved.Reference).GetAwaiter().GetResult().Count == 1);

            var contact = (await contacts.FindForOrganisationAsync(saved.Reference)).Single().Definition;
            Assert.Equal("Pat Client", contact.Name);
            Assert.Equal("Project Manager", contact.JobTitle);
            Assert.Equal("pat@acme.example", contact.EmailAddress);
            Assert.Equal("07700 900000", contact.TelephoneNumber);

            // ---- A second organisation may not take the same code ----
            ClickButton(view, "New organisation");
            await RenderUntilAsync(window, () => view.EditingRecordId is null);
            name.Text = "Acme Engineering Group";
            await RenderUntilAsync(window, () => code.Text == "ACMEA");
            Assert.Equal("ACMEA", code.Text);

            code.Text = "acmee";
            ClickButton(view, "Save organisation");
            await RenderUntilAsync(window, () => view.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("already in use", StringComparison.Ordinal) == true));
            Assert.Single(await organisations.ListAsync());

            code.Text = "ACM";
            ClickButton(view, "Save organisation");
            await RenderUntilAsync(window, () => view.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("exactly 5 letters", StringComparison.Ordinal) == true));
            Assert.Single(await organisations.ListAsync());
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task NewProject_ClientDropDownListsCustomers_CodedClientGivesProjectCentricIdentifier_FirstQuoteIs001()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var organisations = (IOrganisationCatalog)host.Services!.GetService(typeof(IOrganisationCatalog));
            await organisations.RegisterAsync(
                "ACMEE", new Organisation { Reference = "ACMEE", Name = "Acme Engineering Ltd", CustomerCode = "ACMEE", Roles = [PartyKind.Customer] },
                ReferenceProvenance.Unknown);
            await organisations.RegisterAsync(
                "SUPPL", new Organisation { Reference = "SUPPL", Name = "Steel Supplies Ltd", CustomerCode = "STEEL", Roles = [PartyKind.Supplier] },
                ReferenceProvenance.Unknown);
            await organisations.RegisterAsync(
                "OLD-1", new Organisation { Reference = "OLD-1", Name = "Old Client Ltd" }, ReferenceProvenance.Unknown);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            // ---- A coded client: ACMEE-BRIDG, and its first quote is 001 ----
            var browser = GetPrivateField<ProjectBrowserView>(window, "_projectBrowser");
            browser.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "New Project…"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var prompt = GetPrivateField<NewProjectPrompt>(window, "_newProjectPrompt");
            await RenderUntilAsync(window, () => prompt.IsVisible);

            var clientCombo = prompt.GetLogicalDescendants().OfType<ComboBox>().First();
            await RenderUntilAsync(window, () => clientCombo.ItemsSource is not null);
            var clientItems = clientCombo.ItemsSource!.Cast<ComboBoxItem>().ToList();
            Assert.Contains(clientItems, i => Equals(i.Tag, "ACMEE") && Equals(i.Content, "Acme Engineering Ltd (ACMEE)"));
            Assert.Contains(clientItems, i => Equals(i.Tag, "OLD-1"));
            Assert.DoesNotContain(clientItems, i => Equals(i.Tag, "SUPPL"));
            Assert.Contains(clientItems, i => Equals(i.Content, "Add organisation…"));

            prompt.GetLogicalDescendants().OfType<TextBox>().First().Text = "Bridge Strengthening";
            clientCombo.SelectedItem = clientItems.Single(i => Equals(i.Tag, "ACMEE"));

            var reference = prompt.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Project reference");
            var preview = prompt.GetLogicalDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == "Project identifier");
            await RenderUntilAsync(window, () => reference.Text == "BRIDG" && preview.Text == "Identifier: ACMEE-BRIDG");
            Assert.Equal("BRIDG", reference.Text);
            Assert.Equal("Identifier: ACMEE-BRIDG", preview.Text);

            prompt.GetLogicalDescendants().OfType<CheckBox>().Single().IsChecked = true;
            prompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !prompt.IsVisible && navigator.Current.ProjectId is not null);

            var projectId = navigator.Current.ProjectId!.Value;
            Assert.Equal("ACMEE-BRIDG", (await host.ProjectDirectory!.FindAsync(projectId))!.Identifier);

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            IReadOnlyList<Quotation> quotations = [];
            await RenderUntilAsync(window, () =>
            {
                var entries = domain.Repository.ListByKindAsync(Quotation.CanonicalKind).GetAwaiter().GetResult();
                quotations = domain.Repository.MaterialiseAsync<Quotation>(entries).GetAwaiter().GetResult();
                return quotations.Count == 1;
            });
            Assert.Equal("ACMEE-BRIDG-Q-001", quotations.Single().Reference);

            // ---- An uncoded client: the old P-NNNN scheme, a reused reference is refused first ----
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            browser.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "New Project…"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => prompt.IsVisible);

            await RenderUntilAsync(window, () => clientCombo.ItemsSource is not null && clientCombo.ItemsSource.Cast<ComboBoxItem>().Any(i => Equals(i.Tag, "ACMEE")));
            prompt.GetLogicalDescendants().OfType<TextBox>().First().Text = "Bridge Phase Two";
            clientCombo.SelectedItem = clientCombo.ItemsSource!.Cast<ComboBoxItem>().Single(i => Equals(i.Tag, "ACMEE"));
            reference.Text = "BRIDG";
            prompt.GetLogicalDescendants().OfType<CheckBox>().Single().IsChecked = false;
            prompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => prompt.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("already in use", StringComparison.Ordinal) == true));
            Assert.True(prompt.IsVisible);

            clientCombo.SelectedItem = clientCombo.ItemsSource!.Cast<ComboBoxItem>().Single(i => Equals(i.Tag, "OLD-1"));
            await RenderUntilAsync(window, () => preview.Text?.Contains("no customer code", StringComparison.Ordinal) == true);
            prompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !prompt.IsVisible && navigator.Current.ProjectId is not null && navigator.Current.ProjectId != projectId);

            var legacy = (await host.ProjectDirectory!.FindAsync(navigator.Current.ProjectId!.Value))!;
            Assert.StartsWith("P-", legacy.Identifier, StringComparison.Ordinal);
            Assert.Equal("OLD-1", legacy.ClientOrganisationId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static TextBox Box(Control root, string name) =>
        root.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == name);

    private static void ClickButton(Control root, string name) =>
        root.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
