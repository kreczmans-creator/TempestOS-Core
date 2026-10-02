using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Projects;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Business tree's own <b>Customers &amp; Suppliers</b> node (Product
/// Owner decision 2026-10-01 §§1–2): one list of organisations — customers
/// and suppliers share the same model and the same level of detail, told
/// apart only by a type (Customer / Supplier / Both) — with an add/edit
/// form for each organisation's company details and, beneath it, its own
/// list of contacts.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two lists, two levels of detail (§1).</b> The consultancy's own
/// people (Libraries → People, <c>Tempest.Core.People.Person</c>) keep
/// contact details only: name, role/grade, e-mail, phone. A client or a
/// supplier is an <see cref="Organisation"/> — legal name, customer code,
/// address, company number, VAT number, phone, e-mail, website — plus
/// any number of <see cref="Contact"/> records (name, role, e-mail,
/// phone) held in the existing <see cref="IContactCatalog"/> by
/// <see cref="Contact.OrganisationReference"/>. Both catalogues already
/// existed (`WP04.1`, `ADR-0142`); this view is the first editor over
/// them — no parallel organisation model is introduced.
/// </para>
/// <para>
/// <b>Customer code (§3, `ADR-0156`).</b> Every organisation added here
/// gets a unique five-character code, suggested from its name by
/// <see cref="IOrganisationCatalog.SuggestCustomerCodeAsync"/> as the name
/// is typed (until the code box is edited by hand), upper-cased and
/// refused unless it is exactly five characters (letters A–Z or digits
/// 0–9) and held by no other
/// organisation. A new organisation's record id and
/// <see cref="Organisation.Reference"/> are its code at the moment it is
/// added; the code itself stays editable afterwards, and changing it
/// never renumbers an existing project.
/// </para>
/// <para>
/// <b>Where the list is used.</b> New Project → Client
/// (<see cref="NewProjectPrompt"/>) and the project editor's own client
/// picker (<see cref="OrganisationPicker"/>) both read this same
/// <see cref="IOrganisationCatalog"/>, so an organisation added here is in
/// their drop-downs immediately, and one added through their "Add
/// organisation…" shortcut appears here.
/// </para>
/// <para>
/// <b>Xero (`v0.24.0` U2, Xero Technical Design §5, §11).</b> When Xero is
/// the connector (<see cref="XeroContacts"/> set), each organisation shows
/// its Xero link — <i>Not linked</i>, or <i>Linked to</i> the Xero contact's
/// name — with <b>Link to Xero…</b> (<see cref="XeroContactLinkPrompt"/>:
/// the ranked matches to confirm, or <b>Create in Xero</b>),
/// <b>Refresh from Xero</b> and <b>Unlink</b>. The billing address, VAT
/// number and payment terms Xero holds are shown read-only with a
/// <i>from Xero, read at …</i> note: Xero is their source of truth and
/// TempestOS never pushes them. Every Xero call is awaited off the UI
/// thread; an answer for an organisation no longer shown is dropped.
/// </para>
/// </remarks>
public sealed class CustomersSuppliersView : UserControl
{
    /// <summary>The provenance every organisation and contact added here is stamped with — entered by hand, unverified.</summary>
    public static ReferenceProvenance Provenance { get; } = new(
        SourceOrganisation: "TempestOS",
        SourceDocument: "Entered directly in Business → Customers & Suppliers.",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Business contact details, added by hand.");

    private readonly IOrganisationCatalog _organisations;
    private readonly IContactCatalog _contacts;

    private readonly ComboBox _typeFilter = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 160 };
    private readonly ListBox _list = new() { MaxHeight = 260 };
    private readonly Button _newButton = new() { Content = "New organisation", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _emptyList = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = Avalonia.Media.TextWrapping.Wrap, IsVisible = false };

    private readonly TextBlock _formHeading = SectionHeading("New organisation");
    private readonly TextBox _name = Field("Legal name");
    private readonly TextBox _code = CodeField();
    private readonly ComboBox _type = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 160 };
    private readonly TextBox _addressLine1 = Field("Address line 1");
    private readonly TextBox _addressLine2 = Field("Address line 2");
    private readonly TextBox _town = Field("Town");
    private readonly TextBox _postcode = Field("Postcode");
    private readonly TextBox _country = Field("Country code");
    private readonly TextBox _companyNumber = Field("Company number");
    private readonly TextBox _vatNumber = Field("VAT number");
    private readonly TextBox _phone = Field("Phone");
    private readonly TextBox _email = Field("Email");
    private readonly TextBox _website = Field("Website");
    private readonly Button _saveButton = new() { Content = "Save organisation", MinHeight = DesignTokens.ControlSizeMedium };

    private readonly StackPanel _contactsSection = new() { Spacing = DesignTokens.SpaceSm, IsVisible = false };
    private readonly ListBox _contactList = new() { MaxHeight = 200 };
    private readonly TextBox _contactName = Field("Contact name");
    private readonly TextBox _contactRole = Field("Contact role");
    private readonly TextBox _contactEmail = Field("Contact email");
    private readonly TextBox _contactPhone = Field("Contact phone");
    private readonly Button _saveContactButton = new() { Content = "Add contact", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _newContactButton = new() { Content = "New contact", MinHeight = DesignTokens.ControlSizeMedium };

    // v0.24.0 U2: the organisation's Xero link, shown only when Xero is the connector.
    private readonly StackPanel _xeroSection = new() { Spacing = DesignTokens.SpaceSm, IsVisible = false };
    private readonly TextBlock _xeroState = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly StackPanel _xeroDetails = new() { Spacing = 2, IsVisible = false };
    private readonly TextBlock _xeroAddress = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _xeroVat = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _xeroTerms = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _xeroNote = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _xeroStatus = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _xeroLinkButton = new() { Content = LinkToXeroName, MinHeight = DesignTokens.ControlSizeMedium, IsVisible = false };
    private readonly Button _xeroRefreshButton = new() { Content = RefreshFromXeroName, MinHeight = DesignTokens.ControlSizeMedium, IsVisible = false };
    private readonly Button _xeroUnlinkButton = new() { Content = "Unlink", MinHeight = DesignTokens.ControlSizeMedium, IsVisible = false };
    private readonly Panel _overlay = new();
    private readonly ScrollViewer _body = new();

    // The Link to Xero… prompt is modal within the view: while it is open a
    // full-size scrim takes every pointer hit and the body behind it is
    // disabled, so the user cannot change the organisation shown under it.
    // The prompt can only be opened from a section rendered for the
    // organisation shown (LoadAsync withdraws it before looking up another),
    // and its outcome is written for, and shown on, that organisation alone.
    private readonly Border _xeroScrim = new() { IsVisible = false, Opacity = 0.7 };
    private XeroContactLinker? _xero;
    private XeroContactLinkPrompt? _xeroPrompt;
    private int _xeroGeneration;
    private bool _xeroBusy;

    /// <summary>The record Id and organisation the Xero section was last rendered for; Link and Unlink act on these alone.</summary>
    private (string RecordId, Organisation Organisation)? _xeroShown;

    /// <summary>
    /// How many <see cref="LoadAsync"/> look-ups are still awaiting their
    /// <c>FindAsync</c>. While any is pending the Xero section is never
    /// rendered (or re-shown) for the organisation the form still holds: the
    /// last look-up to end renders it, for whatever the form then shows.
    /// </summary>
    private int _pendingLoads;

    private IReadOnlyList<IReferenceRecord<Organisation>> _all = [];
    private string? _editingRecordId;
    private string? _editingContactId;
    private bool _codeEditedByHand;
    private bool _settingCode;
    private bool _suppressListSelection;

    /// <summary>Initialises a new instance of the <see cref="CustomersSuppliersView"/> class.</summary>
    public CustomersSuppliersView(IOrganisationCatalog organisations, IContactCatalog contacts)
    {
        ArgumentNullException.ThrowIfNull(organisations);
        ArgumentNullException.ThrowIfNull(contacts);
        _organisations = organisations;
        _contacts = contacts;

        _typeFilter.ItemsSource = new[]
        {
            new ComboBoxItem { Content = "All", Tag = null },
            new ComboBoxItem { Content = "Customers", Tag = OrganisationTradingType.Customer },
            new ComboBoxItem { Content = "Suppliers", Tag = OrganisationTradingType.Supplier },
        };
        _typeFilter.SelectedIndex = 0;

        _type.ItemsSource = new[]
        {
            new ComboBoxItem { Content = "Customer", Tag = OrganisationTradingType.Customer },
            new ComboBoxItem { Content = "Supplier", Tag = OrganisationTradingType.Supplier },
            new ComboBoxItem { Content = "Both", Tag = OrganisationTradingType.Both },
        };
        _type.SelectedIndex = 0;

        foreach (var (control, name) in new (Control, string)[]
                 {
                     (_typeFilter, "Show"), (_list, "Organisations"), (_newButton, "New organisation"), (_type, "Type"),
                     (_saveButton, "Save organisation"), (_contactList, "Contacts"), (_saveContactButton, "Add contact"),
                     (_newContactButton, "New contact"), (_emptyList, "No matching organisations"),
                 })
            AutomationProperties.SetName(control, name);

        _saveButton.Classes.Add(ChromeStyles.Primary);
        _newButton.Classes.Add(ChromeStyles.Subtle);
        _saveContactButton.Classes.Add(ChromeStyles.Primary);
        _newContactButton.Classes.Add(ChromeStyles.Subtle);

        _typeFilter.SelectionChanged += (_, _) => ApplyFilter();
        _list.SelectionChanged += async (_, _) => await OnListSelectionChangedAsync().ConfigureAwait(true);
        _newButton.Click += (_, _) => BeginNew();
        _saveButton.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        _name.PropertyChanged += async (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                await SuggestCodeAsync().ConfigureAwait(true);
        };
        _code.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && !_settingCode)
                _codeEditedByHand = !string.IsNullOrWhiteSpace(_code.Text);
        };
        _contactList.SelectionChanged += async (_, _) => await OnContactSelectionChangedAsync().ConfigureAwait(true);
        _saveContactButton.Click += async (_, _) => await SaveContactAsync().ConfigureAwait(true);
        _newContactButton.Click += (_, _) => BeginNewContact();

        var listHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        listHeader.Children.Add(_typeFilter);
        listHeader.Children.Add(_newButton);

        var form = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var field in new Control[]
                 {
                     _name, _code, _type, _addressLine1, _addressLine2, _town, _postcode, _country,
                     _companyNumber, _vatNumber, _phone, _email, _website,
                 })
            form.Children.Add(Labelled(field));

        var contactForm = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var field in new Control[] { _contactName, _contactRole, _contactEmail, _contactPhone })
            contactForm.Children.Add(Labelled(field));
        foreach (var button in new Control[] { _saveContactButton, _newContactButton })
        {
            button.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceSm);
            button.VerticalAlignment = VerticalAlignment.Bottom;
            contactForm.Children.Add(button);
        }

        foreach (var (control, name) in new (Control, string)[]
                 {
                     (_xeroState, XeroLinkStateName), (_xeroAddress, "Xero billing address"), (_xeroVat, "Xero VAT number"),
                     (_xeroTerms, "Xero payment terms"), (_xeroNote, XeroDetailsNoteName), (_xeroStatus, XeroStatusName),
                     (_xeroLinkButton, LinkToXeroName), (_xeroRefreshButton, RefreshFromXeroName), (_xeroUnlinkButton, UnlinkFromXeroName),
                 })
            AutomationProperties.SetName(control, name);
        AutomationProperties.SetLiveSetting(_xeroStatus, AutomationLiveSetting.Polite);
        ToolTip.SetTip(_xeroUnlinkButton, "Remove the link to the Xero contact. Nothing is changed in Xero.");
        _xeroLinkButton.Classes.Add(ChromeStyles.Primary);
        _xeroRefreshButton.Classes.Add(ChromeStyles.Subtle);
        _xeroUnlinkButton.Classes.Add(ChromeStyles.Subtle);
        _xeroLinkButton.Click += async (_, _) => await LinkToXeroAsync().ConfigureAwait(true);
        _xeroRefreshButton.Click += async (_, _) => await RefreshXeroAsync(readDetails: true).ConfigureAwait(true);
        _xeroUnlinkButton.Click += async (_, _) => await UnlinkFromXeroAsync().ConfigureAwait(true);

        _xeroDetails.Children.Add(_xeroAddress);
        _xeroDetails.Children.Add(_xeroVat);
        _xeroDetails.Children.Add(_xeroTerms);
        _xeroDetails.Children.Add(_xeroNote);
        var xeroButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        xeroButtons.Children.Add(_xeroLinkButton);
        xeroButtons.Children.Add(_xeroRefreshButton);
        xeroButtons.Children.Add(_xeroUnlinkButton);
        _xeroSection.Children.Add(SectionHeading("Xero"));
        _xeroSection.Children.Add(_xeroState);
        _xeroSection.Children.Add(_xeroDetails);
        _xeroSection.Children.Add(xeroButtons);
        _xeroSection.Children.Add(_xeroStatus);

        _contactsSection.Children.Add(SectionHeading("Contacts"));
        _contactsSection.Children.Add(_contactList);
        _contactsSection.Children.Add(contactForm);

        var body = new StackPanel { Margin = DesignTokens.PagePadding, Spacing = DesignTokens.SpaceMd, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        body.Children.Add(PageHeading.Label("BUSINESS · CUSTOMERS & SUPPLIERS"));
        body.Children.Add(PageHeading.Title("Customers & Suppliers"));
        body.Children.Add(PageHeading.Lead("Every organisation the business sells to or buys from — company details, a unique five-character customer code (letters and digits) used in project and document numbers, and the people to contact there."));
        body.Children.Add(listHeader);
        body.Children.Add(_list);
        body.Children.Add(_emptyList);
        body.Children.Add(_status);
        body.Children.Add(_formHeading);
        body.Children.Add(form);
        body.Children.Add(_saveButton);
        body.Children.Add(_xeroSection);
        body.Children.Add(_contactsSection);

        AutomationProperties.SetName(this, "Customers & Suppliers");
        _body.Content = body;
        _overlay.Children.Add(_body);
        Content = _overlay;
    }

    /// <summary>The automation name of the line saying whether the organisation is linked to a Xero contact.</summary>
    public const string XeroLinkStateName = "Xero link";

    /// <summary>The automation name of the note saying the details shown are Xero's, and when they were read.</summary>
    public const string XeroDetailsNoteName = "Xero details note";

    /// <summary>The automation name of the line reporting a Xero action's outcome or failure.</summary>
    public const string XeroStatusName = "Xero status";

    /// <summary>The automation name (and caption) of the button that opens <see cref="XeroContactLinkPrompt"/>.</summary>
    public const string LinkToXeroName = "Link to Xero…";

    /// <summary>The automation name (and caption) of the button that reads the linked contact's details again.</summary>
    public const string RefreshFromXeroName = "Refresh from Xero";

    /// <summary>The automation name of the Unlink button.</summary>
    public const string UnlinkFromXeroName = "Unlink from Xero";

    /// <summary>The automation name of the scrim that covers the view while the Link to Xero… prompt is open.</summary>
    public const string XeroPromptScrimName = "Xero link prompt backdrop";

    /// <summary>
    /// The X2 contact linker (`v0.24.0` U2): set when Xero is the connector,
    /// which shows each organisation's Xero link and the Link / Refresh /
    /// Unlink actions; <see langword="null"/> (the default) shows no Xero
    /// section at all.
    /// </summary>
    public XeroContactLinker? XeroContacts
    {
        get => _xero;
        init
        {
            _xero = value;
            if (value is null)
                return;

            _xeroPrompt = new XeroContactLinkPrompt(value);
            ThemeReactiveBrush.Bind(_xeroScrim, Border.BackgroundProperty, ApplicationPalette.OverlayBackgroundBrushKey);
            AutomationProperties.SetName(_xeroScrim, XeroPromptScrimName);
            _overlay.Children.Add(_xeroScrim);
            _overlay.Children.Add(_xeroPrompt);
            _xeroPrompt.PropertyChanged += (_, e) =>
            {
                if (e.Property != IsVisibleProperty)
                    return;

                var open = _xeroPrompt.IsVisible;
                _xeroScrim.IsVisible = open;
                _body.IsEnabled = !open;
            };
        }
    }

    /// <summary>The prompt <b>Link to Xero…</b> opens; <see langword="null"/> without <see cref="XeroContacts"/>.</summary>
    internal XeroContactLinkPrompt? XeroLinkPrompt => _xeroPrompt;

    /// <summary>Whether a Xero read or unlink for the organisation shown is in flight.</summary>
    internal bool IsXeroBusy => _xeroBusy;

    /// <summary>Whether the body behind the Link to Xero… prompt (the list and the form) takes input.</summary>
    internal bool IsBodyEnabled => _body.IsEnabled;

    /// <summary>The record id of the organisation the form is editing, or <see langword="null"/> while it holds a new one.</summary>
    internal string? EditingRecordId => _editingRecordId;

    /// <summary>The form's status line (a look-up's outcome or fault).</summary>
    internal string StatusText => _status.Text ?? string.Empty;

    /// <summary>Re-reads every organisation, keeping the one being edited selected.</summary>
    public async Task RefreshAsync()
    {
        _all = await _organisations.ListAsync().ConfigureAwait(true);
        ApplyFilter();

        if (_editingRecordId is null)
            await SuggestCodeAsync().ConfigureAwait(true);
    }

    /// <summary>Selects <paramref name="recordId"/> in the list and loads it into the form.</summary>
    internal async Task SelectAsync(string recordId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        if (_list.ItemsSource is IEnumerable<ListBoxItem> items && items.FirstOrDefault(i => Equals(i.Tag, recordId)) is { } item)
            _list.SelectedItem = item;
        else
            await LoadAsync(recordId).ConfigureAwait(true);
    }

    private void ApplyFilter()
    {
        var filter = _typeFilter.SelectedItem is ComboBoxItem { Tag: OrganisationTradingType wanted } ? wanted : (OrganisationTradingType?)null;

        var rows = _all
            .Where(r => filter is null
                        || r.Definition.TradingType == OrganisationTradingType.Both
                        || r.Definition.TradingType == filter)
            .OrderBy(r => r.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new ListBoxItem { Content = Describe(r.Definition), Tag = r.Id })
            .ToList();

        _suppressListSelection = true;
        try
        {
            _list.ItemsSource = rows;
            _list.SelectedItem = _editingRecordId is null ? null : rows.FirstOrDefault(i => Equals(i.Tag, _editingRecordId));
        }
        finally
        {
            _suppressListSelection = false;
        }

        if (_all.Count == 0)
            _status.Text = "No organisations yet — add the first one below.";

        // A filter that matches nothing says so, rather than showing an
        // empty box that reads as "nothing is registered".
        _emptyList.IsVisible = _all.Count > 0 && rows.Count == 0;
        _emptyList.Text = filter switch
        {
            OrganisationTradingType.Customer => "No customers registered — choose Show: All to see every organisation.",
            OrganisationTradingType.Supplier => "No suppliers registered — choose Show: All to see every organisation.",
            _ => string.Empty,
        };
    }

    /// <summary>"Acme Engineering Ltd — ACMEE — Customer": the one-line form both this list and <see cref="NewProjectPrompt"/>'s own drop-down use.</summary>
    internal static string Describe(Organisation organisation)
    {
        ArgumentNullException.ThrowIfNull(organisation);

        var code = string.IsNullOrWhiteSpace(organisation.CustomerCode) ? "no code" : organisation.CustomerCode;
        return $"{organisation.Name} — {code} — {organisation.TradingType}";
    }

    private async Task OnListSelectionChangedAsync()
    {
        if (_suppressListSelection || _list.SelectedItem is not ListBoxItem { Tag: string recordId })
            return;

        await LoadAsync(recordId).ConfigureAwait(true);
    }

    private async Task LoadAsync(string recordId)
    {
        // Another organisation: the Xero section (still showing the previous
        // one) is withdrawn before the first await, so neither Unlink nor
        // Link to Xero… can act on it while this one is looked up, and no
        // answer for the previous one lands here.
        if (!string.Equals(_editingRecordId, recordId, StringComparison.Ordinal))
        {
            HideXero();
            SetXeroBusy(true);
        }

        IReferenceRecord<Organisation>? record;
        Exception? fault = null;
        _pendingLoads++;
        try
        {
            record = await _organisations.FindAsync(recordId).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The UI boundary: a store fault is shown, never left to the dispatcher.
            record = null;
            fault = ex;
        }
        finally
        {
            _pendingLoads--;
        }

        if (record is null)
        {
            _status.Text = fault is null
                ? $"'{recordId}' is no longer registered."
                : $"Could not open '{recordId}': {fault.Message}";

            // The organisation still shown keeps its (re-rendered) Xero
            // section — but only once no other look-up is pending; until then
            // the section stays withdrawn and the last look-up to end renders
            // it (a refresh started meanwhile renders nothing, so this one
            // always does, which also ends any busy state it left).
            if (_pendingLoads == 0)
            {
                if (_editingRecordId is not null)
                    await RefreshXeroAsync(readDetails: true).ConfigureAwait(true);
                else
                    HideXero();
            }

            return;
        }

        var o = record.Definition;

        // The organisation shown may have changed during the look-up (a
        // concurrent load): withdraw its section too.
        if (!string.Equals(_editingRecordId, recordId, StringComparison.Ordinal))
        {
            HideXero();
            SetXeroBusy(true);
        }

        _editingRecordId = recordId;
        _formHeading.Text = $"Edit {o.Name}";
        _saveButton.Content = "Save organisation";

        _name.Text = o.Name;
        SetCode(o.CustomerCode);

        // An organisation recorded before customer codes existed is offered
        // one, suggested from its name, the moment it is opened.
        _codeEditedByHand = !string.IsNullOrWhiteSpace(o.CustomerCode);
        if (!_codeEditedByHand)
            await SuggestCodeAsync().ConfigureAwait(true);
        _type.SelectedItem = _type.Items.OfType<ComboBoxItem>().First(i => Equals(i.Tag, o.TradingType));
        _addressLine1.Text = o.Address?.Line1;
        _addressLine2.Text = o.Address?.Line2;
        _town.Text = o.Address?.Town;
        _postcode.Text = o.Address?.Postcode;
        _country.Text = o.Address?.CountryCode;
        _companyNumber.Text = o.RegistrationNumber;
        _vatNumber.Text = o.TaxRegistration;
        _phone.Text = o.TelephoneNumber;
        _email.Text = o.EmailAddress;
        _website.Text = o.Website;
        _status.Text = string.Empty;

        _contactsSection.IsVisible = true;
        BeginNewContact();
        await ReloadContactsAsync(o.Reference).ConfigureAwait(true);
        await RefreshXeroAsync(readDetails: true).ConfigureAwait(true);
    }

    private void BeginNew()
    {
        _editingRecordId = null;
        _formHeading.Text = "New organisation";
        foreach (var box in new[] { _name, _addressLine1, _addressLine2, _town, _postcode, _country, _companyNumber, _vatNumber, _phone, _email, _website })
            box.Text = string.Empty;
        SetCode(null);
        _codeEditedByHand = false;
        _type.SelectedIndex = 0;
        _contactsSection.IsVisible = false;
        HideXero();
        _list.SelectedItem = null;
        _status.Text = string.Empty;
        _name.Focus();
    }

    private void SetCode(string? code)
    {
        _settingCode = true;
        try
        {
            _code.Text = code ?? string.Empty;
        }
        finally
        {
            _settingCode = false;
        }
    }

    /// <summary>Suggests a free customer code from the name while the code box has not been edited by hand (§3: "suggest one derived from the name, editable").</summary>
    private async Task SuggestCodeAsync()
    {
        if (_codeEditedByHand)
            return;

        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            SetCode(null);
            return;
        }

        var suggestion = await _organisations.SuggestCustomerCodeAsync(_name.Text, _editingRecordId).ConfigureAwait(true);
        if (!_codeEditedByHand)
            SetCode(suggestion);
    }

    private async Task SaveAsync()
    {
        var name = _name.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _status.Text = "A legal name is required.";
            return;
        }

        var all = await _organisations.ListAsync().ConfigureAwait(true);
        var code = ProjectNumbering.Normalise(_code.Text);
        // A new organisation is registered under its code as both record id
        // and reference, so a code freed by another record's edit (whose id
        // and reference are still the old code) is taken as well.
        var takenElsewhere = all
            .Where(r => !string.Equals(r.Id, _editingRecordId, StringComparison.Ordinal))
            .SelectMany(r => new[] { r.Definition.CustomerCode, r.Id, r.Definition.Reference });
        if (ProjectNumbering.ValidateCustomerCode(code, takenElsewhere) is { } refusal)
        {
            _status.Text = refusal;
            return;
        }

        var type = _type.SelectedItem is ComboBoxItem { Tag: OrganisationTradingType chosen } ? chosen : OrganisationTradingType.Customer;
        var line1 = NullIfEmpty(_addressLine1.Text);
        var address = line1 is null
            ? null
            : new PostalAddress(line1, NullIfEmpty(_town.Text), NullIfEmpty(_postcode.Text), NullIfEmpty(_country.Text), NullIfEmpty(_addressLine2.Text));

        try
        {
            if (_editingRecordId is null)
            {
                var created = new Organisation { Reference = code!, Name = name, Status = RelationshipStatus.Active };
                created = Apply(created with { Roles = created.RolesFor(type) }, code!, address);
                await _organisations.RegisterAsync(code!, created, Provenance).ConfigureAwait(true);
                _editingRecordId = code;
                _status.Text = $"Added '{name}' ({code}).";
            }
            else
            {
                var current = await _organisations.FindAsync(_editingRecordId).ConfigureAwait(true);
                if (current is null)
                {
                    _status.Text = $"'{_editingRecordId}' is no longer registered.";
                    return;
                }

                var revised = Apply(current.Definition with { Name = name, Roles = current.Definition.RolesFor(type) }, code!, address);
                await _organisations.ReviseAsync(_editingRecordId, revised, current.Provenance, "Edited in Business → Customers & Suppliers.").ConfigureAwait(true);
                _status.Text = $"Saved '{name}' ({code}).";
            }
        }
        catch (Exception ex) when (ex is ReferenceDataException or ArgumentException)
        {
            _status.Text = ex.Message;
            return;
        }

        var savedId = _editingRecordId;
        await RefreshAsync().ConfigureAwait(true);
        await LoadAsync(savedId!).ConfigureAwait(true);
    }

    private Organisation Apply(Organisation organisation, string code, PostalAddress? address) => organisation with
    {
        CustomerCode = code,
        Address = address,
        RegistrationNumber = NullIfEmpty(_companyNumber.Text),
        TaxRegistration = NullIfEmpty(_vatNumber.Text),
        TelephoneNumber = NullIfEmpty(_phone.Text),
        EmailAddress = NullIfEmpty(_email.Text),
        Website = NullIfEmpty(_website.Text),
    };

    private async Task<Organisation?> EditingOrganisationAsync() =>
        _editingRecordId is null ? null : (await _organisations.FindAsync(_editingRecordId).ConfigureAwait(true))?.Definition;

    private async Task ReloadContactsAsync(string organisationReference)
    {
        var contacts = await _contacts.FindForOrganisationAsync(organisationReference).ConfigureAwait(true);
        _contactList.ItemsSource = contacts
            .Select(c => new ListBoxItem { Content = DescribeContact(c.Definition), Tag = c.Id })
            .ToList();
    }

    private static string DescribeContact(Contact contact)
    {
        var parts = new[] { contact.Name, contact.JobTitle, contact.EmailAddress, contact.TelephoneNumber }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" · ", parts);
    }

    private async Task OnContactSelectionChangedAsync()
    {
        if (_contactList.SelectedItem is not ListBoxItem { Tag: string contactId })
            return;

        var record = await _contacts.FindAsync(contactId).ConfigureAwait(true);
        if (record is null)
            return;

        _editingContactId = contactId;
        _contactName.Text = record.Definition.Name;
        _contactRole.Text = record.Definition.JobTitle;
        _contactEmail.Text = record.Definition.EmailAddress;
        _contactPhone.Text = record.Definition.TelephoneNumber;
        _saveContactButton.Content = "Save contact";
        AutomationProperties.SetName(_saveContactButton, "Save contact");
    }

    private void BeginNewContact()
    {
        _editingContactId = null;
        foreach (var box in new[] { _contactName, _contactRole, _contactEmail, _contactPhone })
            box.Text = string.Empty;
        _contactList.SelectedItem = null;
        _saveContactButton.Content = "Add contact";
        AutomationProperties.SetName(_saveContactButton, "Add contact");
    }

    private async Task SaveContactAsync()
    {
        var organisation = await EditingOrganisationAsync().ConfigureAwait(true);
        if (organisation is null)
        {
            _status.Text = "Save the organisation before adding contacts.";
            return;
        }

        var name = _contactName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _status.Text = "A contact name is required.";
            return;
        }

        try
        {
            if (_editingContactId is null)
            {
                var existing = await _contacts.FindForOrganisationAsync(organisation.Reference).ConfigureAwait(true);
                var reference = ProjectNumbering.NextNumber($"{organisation.Reference}-C", existing.Select(c => c.Definition.Reference));
                var contact = new Contact
                {
                    Reference = reference,
                    OrganisationReference = organisation.Reference,
                    Name = name,
                    JobTitle = NullIfEmpty(_contactRole.Text),
                    EmailAddress = NullIfEmpty(_contactEmail.Text),
                    TelephoneNumber = NullIfEmpty(_contactPhone.Text),
                    IsPrimaryContact = existing.Count == 0,
                };
                await _contacts.RegisterAsync(reference, contact, Provenance).ConfigureAwait(true);
                _status.Text = $"Added contact '{name}'.";
            }
            else
            {
                var current = await _contacts.FindAsync(_editingContactId).ConfigureAwait(true);
                if (current is null)
                {
                    _status.Text = $"Contact '{_editingContactId}' is no longer registered.";
                    return;
                }

                var revised = current.Definition with
                {
                    Name = name,
                    JobTitle = NullIfEmpty(_contactRole.Text),
                    EmailAddress = NullIfEmpty(_contactEmail.Text),
                    TelephoneNumber = NullIfEmpty(_contactPhone.Text),
                };
                await _contacts.ReviseAsync(_editingContactId, revised, current.Provenance, "Edited in Business → Customers & Suppliers.").ConfigureAwait(true);
                _status.Text = $"Saved contact '{name}'.";
            }
        }
        catch (Exception ex) when (ex is ReferenceDataException or ArgumentException)
        {
            _status.Text = ex.Message;
            return;
        }

        BeginNewContact();
        await ReloadContactsAsync(organisation.Reference).ConfigureAwait(true);
    }

    // ------------------------------------------------------------------
    // v0.24.0 U2: Xero contact link.
    // ------------------------------------------------------------------

    private void HideXero()
    {
        _xeroGeneration++;
        _xeroShown = null;
        SetXeroBusy(false);
        _xeroSection.IsVisible = false;
        _xeroDetails.IsVisible = false;
        _xeroStatus.Text = string.Empty;
    }

    /// <summary>
    /// Shows the edited organisation's Xero link from the link store (no
    /// network), then — when linked and <paramref name="readDetails"/> — reads
    /// the contact's billing details from Xero. An answer arriving after
    /// another organisation was opened is dropped.
    /// </summary>
    internal async Task RefreshXeroAsync(bool readDetails)
    {
        var generation = ++_xeroGeneration;
        var recordId = _editingRecordId;
        Organisation? organisation;
        try
        {
            organisation = _xero is null ? null : await EditingOrganisationAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The UI boundary: any fault (a store defect included) is shown, never left to the dispatcher.
            // A look-up of another organisation is pending: the section stays
            // withdrawn; that look-up renders it when it ends.
            if (generation == _xeroGeneration && _pendingLoads == 0)
            {
                // The section may have been withdrawn (another organisation was
                // just opened): show it, offering only Refresh from Xero, so the
                // fault is seen and can be retried.
                if (!_xeroSection.IsVisible && recordId is not null && IsStillShowing(recordId))
                {
                    _xeroShown = null;
                    _xeroState.Text = string.Empty;
                    _xeroDetails.IsVisible = false;
                    SetXeroButtons(linked: null);
                    _xeroRefreshButton.IsVisible = true;
                    _xeroSection.IsVisible = true;
                }

                _xeroStatus.Text = $"Could not read the Xero link: {ex.Message}";
                SetXeroBusy(false);
            }

            return;
        }

        // A look-up of another organisation is pending: never render the one
        // the form still holds meanwhile; that look-up renders the section.
        if (_pendingLoads > 0)
            return;

        if (_xero is null || organisation is null || recordId is null || generation != _xeroGeneration || !IsStillShowing(recordId))
        {
            if (generation == _xeroGeneration)
            {
                _xeroShown = null;
                _xeroSection.IsVisible = false;
                SetXeroBusy(false);
            }

            return;
        }

        // From here the section is rendered for this organisation: Link and Unlink act on it.
        _xeroShown = (recordId, organisation);
        _xeroSection.IsVisible = true;
        _xeroDetails.IsVisible = false;
        _xeroStatus.Text = string.Empty;
        SetXeroButtons(linked: null);

        SetXeroBusy(true);
        try
        {
            var tenantId = await _xero.ReadTenantIdAsync().ConfigureAwait(true);
            if (generation != _xeroGeneration)
                return;

            if (tenantId is null)
            {
                _xeroState.Text = "Xero is not connected. Connect it in Settings → Invoicing to link this organisation to a Xero contact.";
                return;
            }

            var link = await _xero.FindLinkAsync(tenantId, organisation.Reference).ConfigureAwait(true);
            if (generation != _xeroGeneration)
                return;

            if (link is null)
            {
                _xeroState.Text = "Not linked to a Xero contact. Documents for this organisation wait until it is linked.";
                SetXeroButtons(linked: false);
                return;
            }

            if (PersistenceXeroLinkStore.IsFromNewerVersion(link))
            {
                _xeroState.Text = $"Linked to Xero contact {link.XeroId}, but the link was {PersistenceXeroLinkStore.NewerVersionNote}; this TempestOS leaves it alone.";
                return;
            }

            _xeroState.Text = DescribeLink(link, contactName: null);
            SetXeroButtons(linked: true);
            if (!readDetails)
                return;

            _xeroStatus.Text = "Reading the contact's details from Xero…";
            var details = await _xero.ReadDetailsAsync(organisation.Reference).ConfigureAwait(true);
            if (generation != _xeroGeneration)
                return;

            if (details is { Outcome: ConnectorOutcome.Ok, Value: { } read })
            {
                // The read may have refreshed the link's status (archived in Xero).
                var current = await _xero.FindLinkAsync(tenantId, organisation.Reference).ConfigureAwait(true) ?? link;
                if (generation != _xeroGeneration)
                    return;

                _xeroState.Text = DescribeLink(current, read.Name);
                ShowXeroDetails(read);
                _xeroStatus.Text = string.Empty;
            }
            else
            {
                _xeroStatus.Text = XeroContactLinkPrompt.DescribeFailure(details, "read the contact's details from Xero");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The UI boundary: any fault (a store defect included) is shown, never left to the dispatcher.
            if (generation == _xeroGeneration)
                _xeroStatus.Text = $"Could not read the Xero link: {ex.Message}";
        }
        finally
        {
            if (generation == _xeroGeneration)
                SetXeroBusy(false);
        }
    }

    /// <summary>
    /// Marks a Xero read or unlink as in flight (or not): while one is,
    /// Link to Xero…, Refresh and Unlink are disabled, so neither is silently ignored nor
    /// races the other's bookkeeping.
    /// </summary>
    private void SetXeroBusy(bool busy)
    {
        _xeroBusy = busy;
        _xeroLinkButton.IsEnabled = !busy;
        _xeroRefreshButton.IsEnabled = !busy;
        _xeroUnlinkButton.IsEnabled = !busy;
    }

    private void SetXeroButtons(bool? linked)
    {
        _xeroLinkButton.IsVisible = linked == false;
        _xeroRefreshButton.IsVisible = linked == true;
        _xeroUnlinkButton.IsVisible = linked == true;
    }

    private void ShowXeroDetails(XeroContactDetails details)
    {
        _xeroAddress.Text = "Billing address: " + (details.BillingAddress.Count == 0 ? "none in Xero" : string.Join(", ", details.BillingAddress));
        _xeroVat.Text = "VAT number: " + (string.IsNullOrWhiteSpace(details.TaxNumber) ? "none in Xero" : details.TaxNumber.Trim());
        _xeroTerms.Text = "Payment terms: " + DescribePaymentTerms(details.SalesPaymentTermsDays, details.SalesPaymentTermsType);
        _xeroNote.Text = $"From Xero, read at {FormatReadAt(details.ReadAtUtc)}. Change these in Xero; TempestOS never sends them.";
        _xeroDetails.IsVisible = true;
    }

    /// <summary>"Linked to Acme Ltd in Xero (you chose it)": the link line, naming the contact once its details were read.</summary>
    internal static string DescribeLink(XeroLink link, string? contactName)
    {
        ArgumentNullException.ThrowIfNull(link);

        var who = string.IsNullOrWhiteSpace(contactName) ? $"Xero contact {link.XeroId}" : $"{contactName.Trim()} in Xero";
        var how = link.LinkedBy switch
        {
            XeroContactLinker.LinkedByLinked => " (an existing contact you confirmed)",
            XeroContactLinker.LinkedByCreated => " (created in Xero by TempestOS)",
            XeroContactLinker.LinkedByReconciled => " (found in Xero by its customer code)",
            _ => string.Empty,
        };
        var status = link.LastKnownXeroStatus is { } word && !string.Equals(word, XeroContactMatcher.ActiveStatus, StringComparison.OrdinalIgnoreCase)
            ? $" It is {word.ToLowerInvariant()} in Xero, so documents for it are blocked: restore it in Xero, or unlink it and link another."
            : string.Empty;
        return $"Linked to {who}{how}.{status}";
    }

    /// <summary>Xero's sales payment terms in words: "30 days after the invoice date", …; "none set in Xero" when it has none.</summary>
    internal static string DescribePaymentTerms(int? days, string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return "none set in Xero";

        return type.Trim().ToUpperInvariant() switch
        {
            "DAYSAFTERBILLDATE" => days is { } d ? $"{d.ToString(CultureInfo.InvariantCulture)} days after the invoice date" : "days after the invoice date",
            "DAYSAFTERBILLMONTH" => "days after the end of the invoice month (see Xero for the number)",
            "OFCURRENTMONTH" => "a day of the current month (see Xero for the day)",
            "OFFOLLOWINGMONTH" => "a day of the following month (see Xero for the day)",
            _ => $"Xero terms '{type.Trim()}'",
        };
    }

    /// <summary>"2 Oct 2026 14:05" in the user's local time.</summary>
    internal static string FormatReadAt(DateTimeOffset readAtUtc) =>
        readAtUtc.ToLocalTime().ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    private async Task LinkToXeroAsync()
    {
        try
        {
            // The organisation the Xero section was rendered for — never a fresh look-up of the form's.
            if (_xeroPrompt is null || _xeroBusy || _xeroShown is not var (recordId, organisation) || !IsStillShowing(recordId))
                return;

            var link = await _xeroPrompt.PromptAsync(organisation.Reference, organisation.Name).ConfigureAwait(true);
            if (link is null || !IsStillShowing(recordId))
                return;

            await ReportAfterRefreshAsync(
                recordId,
                link.LinkedBy == XeroContactLinker.LinkedByCreated
                    ? $"Created '{organisation.Name}' in Xero and linked it."
                    : $"Linked '{organisation.Name}' to its Xero contact.",
                readDetails: true,
                onlyIfQuiet: true).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The UI boundary: any fault (a store defect included) is shown, never left to the dispatcher.
            _xeroStatus.Text = $"Could not link to Xero: {ex.Message}";
        }
    }

    private async Task UnlinkFromXeroAsync()
    {
        // The organisation the Xero section was rendered for — never a fresh
        // look-up of the form's, which may already be another organisation.
        if (_xero is null || _xeroBusy || _xeroShown is not var (recordId, organisation) || !IsStillShowing(recordId))
            return;

        // Busy from the click: Refresh and Unlink wait for this unlink.
        var generation = ++_xeroGeneration;
        SetXeroBusy(true);
        bool removed;
        try
        {
            removed = await _xero.UnlinkAsync(organisation.Reference).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The UI boundary: any fault (a store defect included) is shown, never left to the dispatcher.
            if (generation == _xeroGeneration)
                _xeroStatus.Text = $"Could not unlink: {ex.Message}";
            return;
        }
        finally
        {
            if (generation == _xeroGeneration)
                SetXeroBusy(false);
        }

        if (!IsStillShowing(recordId))
            return;

        await ReportAfterRefreshAsync(
            recordId,
            removed
                ? $"Unlinked '{organisation.Name}' from its Xero contact. Nothing was changed in Xero; link it again before its next document goes to Xero."
                : "There was no Xero link to remove.",
            readDetails: false,
            onlyIfQuiet: false).ConfigureAwait(true);
    }

    /// <summary>Whether the form still shows the organisation <paramref name="recordId"/>.</summary>
    private bool IsStillShowing(string recordId) => string.Equals(_editingRecordId, recordId, StringComparison.Ordinal);

    /// <summary>
    /// Refreshes the Xero section, then writes <paramref name="message"/>
    /// into its status line — but only if the organisation shown is still
    /// <paramref name="recordId"/> and no other refresh started meanwhile
    /// (the same generation check <see cref="RefreshXeroAsync"/> uses), so an
    /// outcome never lands on another organisation.
    /// </summary>
    private async Task ReportAfterRefreshAsync(string recordId, string message, bool readDetails, bool onlyIfQuiet)
    {
        try
        {
            var refresh = RefreshXeroAsync(readDetails);
            var generation = _xeroGeneration;
            await refresh.ConfigureAwait(true);
            if (generation != _xeroGeneration || !IsStillShowing(recordId))
                return;

            if (!onlyIfQuiet || string.IsNullOrEmpty(_xeroStatus.Text))
                _xeroStatus.Text = message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The UI boundary: any fault (a store defect included) is shown, never left to the dispatcher.
            if (IsStillShowing(recordId))
                _xeroStatus.Text = $"Could not refresh the Xero link: {ex.Message}";
        }
    }

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static TextBox CodeField()
    {
        var box = Field("Customer code");
        box.Watermark = "Customer code — five characters, e.g. ACME1";
        box.MaxLength = ProjectNumbering.CustomerCodeLength;
        return box;
    }

    private static TextBox Field(string label)
    {
        var box = new TextBox { Watermark = label, MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 180 };
        AutomationProperties.SetName(box, label);
        return box;
    }

    /// <summary>
    /// <paramref name="field"/> under a visible caption — its accessible
    /// name — so the label stays on screen once the field is filled in (a
    /// watermark alone vanishes with the first character).
    /// </summary>
    private static StackPanel Labelled(Control field)
    {
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceSm) };
        panel.Children.Add(new TextBlock { Text = AutomationProperties.GetName(field), FontSize = DesignTokens.FontSizeCaption, Classes = { FieldLabelClass } });
        panel.Children.Add(field);
        return panel;
    }

    /// <summary>The style class every visible field caption carries.</summary>
    internal const string FieldLabelClass = "field-label";

    private static TextBlock SectionHeading(string text) => new()
    {
        Text = text,
        FontFamily = DesignTokens.TitleFont,
        FontWeight = DesignTokens.WeightHeading,
        FontSize = DesignTokens.FontSizeBody + 2,
    };
}
