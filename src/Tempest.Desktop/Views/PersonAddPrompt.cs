using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Core.People;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The requirement Owner picker's own <b>Add person…</b> affordance
/// (`WP 20.10F`, Product Owner finding D8): a small modal that registers a
/// new <see cref="Person"/> record, and — in the same action, since there is
/// no reason to make an engineer visit Reference data separately just to
/// make their own new colleague pickable — verifies and releases it right
/// away, mirroring <see cref="Views.LibrariesView"/>'s own established
/// "Release from Draft verifies first, as one action" idiom
/// (<c>LibrariesView.OnReleaseAsync</c>). Returns the released record's own
/// id and display name, so the Owner drop-down that opened this can select
/// them immediately.
/// </summary>
public sealed class PersonAddPrompt : Border
{
    private readonly IPersonCatalog _persons;
    private readonly ReferenceReviewService _review;

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBox _displayName = new() { Watermark = "Display name", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBox _role = new() { Watermark = "Role", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBox _email = new() { Watermark = "Email", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly Button _addButton = new() { Content = "Add & Release", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private TaskCompletionSource<(string RecordId, string DisplayName)?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="PersonAddPrompt"/> class, initially hidden.</summary>
    public PersonAddPrompt(IPersonCatalog persons, ReferenceReviewService review)
    {
        ArgumentNullException.ThrowIfNull(persons);
        ArgumentNullException.ThrowIfNull(review);
        _persons = persons;
        _review = review;

        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 360;
        MaxWidth = 460;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _title.Text = "Add a person";

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceMd, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_addButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_displayName);
        body.Children.Add(_role);
        body.Children.Add(_email);
        body.Children.Add(_status);
        body.Children.Add(buttons);
        Child = body;

        _addButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_displayName, "Display name");
        AutomationProperties.SetName(_role, "Role");
        AutomationProperties.SetName(_email, "Email");
        AutomationProperties.SetName(_addButton, "Add & Release");
        AutomationProperties.SetName(_cancelButton, "Cancel");
        ToolTip.SetTip(_addButton, "Add & Release");
        ToolTip.SetTip(_cancelButton, "Cancel");

        _addButton.Click += async (_, _) => await OnAddAsync().ConfigureAwait(true);
        _cancelButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>Shows this prompt and returns the newly-released person's own record id and display name, or <see langword="null"/> if the user cancelled.</summary>
    public async Task<(string RecordId, string DisplayName)?> PromptAsync(CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _displayName.Text = string.Empty;
        _role.Text = string.Empty;
        _email.Text = string.Empty;
        _status.Text = string.Empty;
        IsVisible = true;

        _displayName.Focus();

        _pending = new TaskCompletionSource<(string RecordId, string DisplayName)?>();
        return await _pending.Task.ConfigureAwait(true);
    }

    private async Task OnAddAsync()
    {
        var displayName = _displayName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            _status.Text = "A display name is required.";
            return;
        }

        var recordId = "person-" + new string(displayName.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        var person = new Person
        {
            DisplayName = displayName,
            Role = string.IsNullOrWhiteSpace(_role.Text) ? null : _role.Text.Trim(),
            Email = string.IsNullOrWhiteSpace(_email.Text) ? null : _email.Text.Trim(),
        };

        try
        {
            await _persons.RegisterAsync(recordId, person, PersonProvenance.Default).ConfigureAwait(true);

            // Release from Draft verifies first, as one action — the
            // identical idiom `LibrariesView.OnReleaseAsync` already
            // establishes for every other library's own row.
            var statement = new ReferenceReviewStatement(SourceConsulted: "the record's own recorded provenance");
            await _review.VerifyAsync(_persons, recordId, statement).ConfigureAwait(true);
            await _review.ReleaseAsync(_persons, recordId, "Released via the requirement Owner picker's own Add person… affordance.").ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is ArgumentException or DuplicateReferenceRecordException or DuplicateReferenceKeyException or ReferenceReviewException)
        {
            _status.Text = ex.Message;
            return;
        }

        Complete((recordId, displayName));
    }

    private void Complete((string RecordId, string DisplayName)? result)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(result);
    }
}
