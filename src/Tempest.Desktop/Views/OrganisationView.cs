using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// Business → Organisation (`v0.25.3` PO request): the organisation's own
/// identity and bank details, moved out of Settings because they belong to
/// the business, not to an account or the system. Documents read them at
/// render time (<see cref="OrganisationIdentitySettings.ToIdentity"/>), so
/// a save here shows on the next export.
/// </summary>
public sealed class OrganisationView : UserControl
{
    private readonly OrganisationIdentitySettings _identity;

    private readonly TextBox _legalName = Box(260, "Organisation legal name");
    private readonly TextBox _companyNumber = Box(160, "Organisation company number");
    private readonly TextBox _website = Box(220, "Organisation website");
    private readonly TextBox _addressLine1 = Box(260, "Organisation address line 1");
    private readonly TextBox _addressLine2 = Box(260, "Organisation address line 2");
    private readonly TextBox _email = Box(220, "Organisation email");
    private readonly TextBox _phone = Box(180, "Organisation phone");
    private readonly TextBox _bankSortCode = Box(120, "Organisation bank sort code");
    private readonly TextBox _bankAccountNumber = Box(160, "Organisation bank account number");
    private readonly TextBox _bankAccountName = Box(220, "Organisation bank account name");
    private readonly TextBox _bankIban = Box(220, "Organisation bank IBAN");
    private readonly TextBlock _xeroNote = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly Button _saveButton = new() { Content = "Save", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _savedStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Raised after Save, as every other view's own <c>ActionCompleted</c>.</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Initialises a new instance of the <see cref="OrganisationView"/> class.</summary>
    public OrganisationView(OrganisationIdentitySettings identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        _identity = identity;

        var identityStack = new StackPanel { Spacing = DesignTokens.SpaceSm };
        identityStack.Children.Add(Row("Legal name", _legalName));
        identityStack.Children.Add(Row("Company number", _companyNumber));
        identityStack.Children.Add(Row("Website", _website));
        identityStack.Children.Add(Row("Address line 1", _addressLine1));
        identityStack.Children.Add(Row("Address line 2", _addressLine2));
        identityStack.Children.Add(Row("Email", _email));
        identityStack.Children.Add(Row("Phone", _phone));
        identityStack.Children.Add(_xeroNote);
        AutomationProperties.SetName(_xeroNote, "Organisation identity source");

        // A bank detail is never part of a document's footer, only the
        // invoice's own "Payment details", so it keeps its own section.
        var bankStack = new StackPanel { Spacing = DesignTokens.SpaceSm };
        bankStack.Children.Add(Row("Sort code", _bankSortCode));
        bankStack.Children.Add(Row("Account number", _bankAccountNumber));
        bankStack.Children.Add(Row("Account name", _bankAccountName));
        bankStack.Children.Add(Row("IBAN", _bankIban));

        _saveButton.Classes.Add(ChromeStyles.Primary);
        AutomationProperties.SetName(_saveButton, "Save organisation details");
        _saveButton.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        var saveRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        saveRow.Children.Add(_saveButton);
        saveRow.Children.Add(_savedStatus);

        var body = new StackPanel { Margin = DesignTokens.PagePadding, Spacing = DesignTokens.SpaceLg, MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        body.Children.Add(PageHeading.Label("BUSINESS · ORGANISATION"));
        body.Children.Add(PageHeading.Title("Organisation"));
        body.Children.Add(PageHeading.Lead("The business's own details, printed on every document, and the bank details printed on invoices."));
        body.Children.Add(Section("Organisation identity", identityStack));
        body.Children.Add(Section("Bank details (invoice payment details)", bankStack));
        body.Children.Add(saveRow);

        AutomationProperties.SetName(this, "Organisation");
        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Re-reads the stored values, and whether Xero supplies the printed details.</summary>
    public Task RefreshAsync()
    {
        _legalName.Text = _identity.LegalName;
        _companyNumber.Text = _identity.CompanyNumber;
        _website.Text = _identity.Website;
        _addressLine1.Text = _identity.AddressLine1;
        _addressLine2.Text = _identity.AddressLine2;
        _email.Text = _identity.Email;
        _phone.Text = _identity.Phone;
        _bankSortCode.Text = _identity.BankSortCode;
        _bankAccountNumber.Text = _identity.BankAccountNumber;
        _bankAccountName.Text = _identity.BankAccountName;
        _bankIban.Text = _identity.BankIban;
        DescribeSource();
        return Task.CompletedTask;
    }

    private void DescribeSource()
    {
        if (_identity.XeroCompanyDetails is { } xero)
        {
            _xeroNote.Text = $"Documents print the company details from Xero ({xero.SourceNote()}) — see Settings → Xero. The values here are used when there is no reading of Xero.";
            _xeroNote.IsVisible = true;
        }
        else
        {
            _xeroNote.Text = string.Empty;
            _xeroNote.IsVisible = false;
        }
    }

    private async Task SaveAsync()
    {
        _identity.LegalName = _legalName.Text ?? string.Empty;
        _identity.CompanyNumber = _companyNumber.Text ?? string.Empty;
        _identity.Website = _website.Text ?? string.Empty;
        _identity.AddressLine1 = _addressLine1.Text ?? string.Empty;
        _identity.AddressLine2 = _addressLine2.Text ?? string.Empty;
        _identity.Email = _email.Text ?? string.Empty;
        _identity.Phone = _phone.Text ?? string.Empty;
        _identity.BankSortCode = _bankSortCode.Text ?? string.Empty;
        _identity.BankAccountNumber = _bankAccountNumber.Text ?? string.Empty;
        _identity.BankAccountName = _bankAccountName.Text ?? string.Empty;
        _identity.BankIban = _bankIban.Text ?? string.Empty;
        await _identity.SaveAsync().ConfigureAwait(true);

        _savedStatus.Text = $"Saved at {DateTime.Now:HH:mm:ss}.";
        ActionCompleted?.Invoke("Organisation details saved.", ActionOutcome.Changed);
    }

    private static TextBox Box(double minWidth, string name)
    {
        var box = new TextBox { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = minWidth };
        AutomationProperties.SetName(box, name);
        return box;
    }

    private static Control Row(string label, Control value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = DesignTokens.FontSizeBody };
        Grid.SetColumn(value, 1);
        row.Children.Add(text);
        row.Children.Add(value);
        return row;
    }

    private static Control Section(string title, Control content)
    {
        var stack = new StackPanel { Spacing = DesignTokens.SpaceXs };
        stack.Children.Add(new TextBlock { Text = title, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading });
        stack.Children.Add(content);

        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = stack,
        };
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
    }
}
