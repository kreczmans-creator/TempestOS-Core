using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>Collects one issue — the issue reference, revision and client (`WP 18.2B`, §2).</summary>
/// <param name="IssueReference">The issue's own reference, as the consultancy names it.</param>
/// <param name="Revision">The revision issued.</param>
/// <param name="Client">The client the evidence is issued to.</param>
public sealed record IssueEntryInput(string IssueReference, string Revision, string Client);

/// <summary>
/// The Evidence workspace's own issue entry dialog (`WP 18.2B`, §2): issue
/// reference, revision and client. Initially hidden, shares the Dialog
/// Framework's own established panel styling and real modal behaviour
/// (mirrors <see cref="CheckEntry"/>/<see cref="DeclaredFigureEntry"/>).
/// </summary>
public sealed class IssueEntry : Border
{
    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBox _issueReference = new() { Watermark = "Issue reference (e.g. ISS-001)", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBox _revision = new() { Watermark = "Revision (e.g. A)", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBox _client = new() { Watermark = "Client", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBlock _validation = new() { FontSize = DesignTokens.FontSizeCaption, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0), IsVisible = false };
    private readonly Button _issueButton = new() { Content = "Issue", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private TaskCompletionSource<IssueEntryInput?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="IssueEntry"/> class, initially hidden.</summary>
    public IssueEntry()
    {
        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 360;
        MaxWidth = 440;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _title.Text = "Issue to the client";

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_issueButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_issueReference);
        body.Children.Add(_revision);
        body.Children.Add(_client);
        body.Children.Add(_validation);
        body.Children.Add(buttons);
        Child = body;

        _issueButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_issueReference, "Issue reference");
        AutomationProperties.SetName(_revision, "Revision");
        AutomationProperties.SetName(_client, "Client");
        AutomationProperties.SetName(_issueButton, "Issue");
        AutomationProperties.SetName(_cancelButton, "Cancel");
        ToolTip.SetTip(_issueButton, "Issue");
        ToolTip.SetTip(_cancelButton, "Cancel");

        _issueButton.Click += (_, _) => TryComplete();
        _cancelButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>Shows this dialog and returns the issue entered, or <see langword="null"/> if the user cancelled.</summary>
    public Task<IssueEntryInput?> PromptAsync(CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _issueReference.Text = string.Empty;
        _revision.Text = string.Empty;
        _client.Text = string.Empty;
        _validation.IsVisible = false;
        IsVisible = true;
        _issueReference.Focus();

        _pending = new TaskCompletionSource<IssueEntryInput?>();
        return _pending.Task;
    }

    private void TryComplete()
    {
        var issueReference = _issueReference.Text?.Trim();
        if (string.IsNullOrWhiteSpace(issueReference))
        {
            ShowValidationError("An issue reference is required.");
            return;
        }

        var revision = _revision.Text?.Trim();
        if (string.IsNullOrWhiteSpace(revision))
        {
            ShowValidationError("A revision is required.");
            return;
        }

        var client = _client.Text?.Trim();
        if (string.IsNullOrWhiteSpace(client))
        {
            ShowValidationError("A client is required.");
            return;
        }

        Complete(new IssueEntryInput(issueReference, revision, client));
    }

    private void ShowValidationError(string message)
    {
        _validation.Text = message;
        _validation.IsVisible = true;
    }

    private void Complete(IssueEntryInput? input)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(input);
    }
}
