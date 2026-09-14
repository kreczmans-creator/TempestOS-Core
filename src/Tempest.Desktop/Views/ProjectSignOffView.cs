using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The project's own Sign off tab (`WP 19.7A`, `po-comments.md` item 6
/// delta (c)): a statement box and the Sign off action
/// (<see cref="IProjectLifecycleService"/>, `WP 19.5C`), showing the
/// record afterwards, with Hold/Resume while Open and Reopen while Closed
/// and still within the reopen window.
/// </summary>
/// <remarks>
/// Calls <see cref="IProjectLifecycleService"/> directly, exactly as
/// <c>MainWindow.PromptForNewProjectAsync</c> already calls
/// <c>IQuotationService.CreateAsync</c> directly — every act is refused as
/// a result, never an exception, and the underlying mutator is one
/// transaction with an audit row on its own (that service's own
/// guarantee, not this view's).
/// </remarks>
public sealed class ProjectSignOffView : UserControl
{
    private readonly IProjectLifecycleService _lifecycle;
    private readonly EngineeringDomainContext _domainContext;
    private readonly Func<Guid?> _currentProjectId;

    private readonly TextBlock _recordText = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBox _statement = new() { AcceptsReturn = true, Height = 96, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Watermark = "What is being signed off, in the engineer's own words." };
    private readonly Button _signOffButton = new() { Content = "Sign off", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _holdButton = new() { Content = "Hold", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _resumeButton = new() { Content = "Resume", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _reopenButton = new() { Content = "Reopen", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption };
    private readonly StackPanel _statementSection = new() { Spacing = DesignTokens.SpaceSm };

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention.</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Initialises a new instance of the <see cref="ProjectSignOffView"/> class.</summary>
    public ProjectSignOffView(IProjectLifecycleService lifecycle, EngineeringDomainContext domainContext, Func<Guid?> currentProjectId)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(currentProjectId);

        _lifecycle = lifecycle;
        _domainContext = domainContext;
        _currentProjectId = currentProjectId;

        AutomationProperties.SetName(_statement, "Sign-off statement");
        AutomationProperties.SetName(_signOffButton, "Sign off");
        AutomationProperties.SetName(_holdButton, "Hold");
        AutomationProperties.SetName(_resumeButton, "Resume");
        AutomationProperties.SetName(_reopenButton, "Reopen");
        _signOffButton.Classes.Add(ChromeStyles.Primary);
        _holdButton.Classes.Add(ChromeStyles.Subtle);
        _resumeButton.Classes.Add(ChromeStyles.Subtle);
        _reopenButton.Classes.Add(ChromeStyles.Subtle);

        _signOffButton.Click += async (_, _) => await SignOffAsync().ConfigureAwait(true);
        _holdButton.Click += async (_, _) => await HoldAsync().ConfigureAwait(true);
        _resumeButton.Click += async (_, _) => await ResumeAsync().ConfigureAwait(true);
        _reopenButton.Click += async (_, _) => await ReopenAsync().ConfigureAwait(true);

        _statementSection.Children.Add(new TextBlock { Text = "Statement", FontWeight = DesignTokens.WeightHeading });
        _statementSection.Children.Add(_statement);
        _statementSection.Children.Add(_signOffButton);

        var holdRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        holdRow.Children.Add(_holdButton);
        holdRow.Children.Add(_resumeButton);
        holdRow.Children.Add(_reopenButton);

        ThemeReactiveBrush.Bind(_status, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);

        var body = new StackPanel { Margin = DesignTokens.PagePadding, Spacing = DesignTokens.SpaceLg, MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        body.Children.Add(PageHeading.Label("SIGN OFF"));
        body.Children.Add(PageHeading.Title("Sign off"));
        body.Children.Add(PageHeading.Lead("A statement records what is being signed off; signing off closes the project. Reopen is available for 90 days after closing."));
        body.Children.Add(_recordText);
        body.Children.Add(holdRow);
        body.Children.Add(_statementSection);
        body.Children.Add(_status);

        AutomationProperties.SetName(this, "Sign off");
        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Re-reads the open project's own lifecycle state and sign-off record.</summary>
    public async Task RefreshAsync()
    {
        _statement.Text = string.Empty;

        if (_currentProjectId() is not { } projectId
            || await _domainContext.Repository.FindAsync(projectId).ConfigureAwait(true) is not Project project)
        {
            _recordText.Text = "No project open.";
            _statementSection.IsVisible = false;
            _holdButton.IsEnabled = false;
            _resumeButton.IsEnabled = false;
            _reopenButton.IsVisible = false;
            _status.Text = string.Empty;
            return;
        }

        var closed = project.ClosedOn is not null;
        _statementSection.IsVisible = !closed;
        _holdButton.IsVisible = !closed;
        _resumeButton.IsVisible = !closed;
        _holdButton.IsEnabled = !closed && !project.Held;
        _resumeButton.IsEnabled = !closed && project.Held;
        _reopenButton.IsVisible = closed;

        _recordText.Text = project switch
        {
            { SignOff: { } signOff, ClosedOn: { } closedOn } =>
                $"Signed off by {signOff.PrincipalId} on {signOff.SignedOn:d}, closing the project on {closedOn:d}.\n\n\"{signOff.Statement}\"",
            { Held: true } => $"On hold: {project.HoldReason}",
            _ => "Open — not yet signed off.",
        };
    }

    private async Task SignOffAsync()
    {
        if (_currentProjectId() is not { } projectId)
            return;

        var statement = _statement.Text?.Trim();
        if (string.IsNullOrWhiteSpace(statement))
        {
            _status.Text = "A statement is required before signing off.";
            return;
        }

        var result = await _lifecycle.SignOffAsync(projectId, statement).ConfigureAwait(true);
        await ReportAsync(result, "Signed off; the project is now closed.").ConfigureAwait(true);
    }

    private async Task HoldAsync()
    {
        if (_currentProjectId() is not { } projectId)
            return;

        var result = await _lifecycle.HoldAsync(projectId, "Put on hold from the Sign off tab.").ConfigureAwait(true);
        await ReportAsync(result, "On hold.").ConfigureAwait(true);
    }

    private async Task ResumeAsync()
    {
        if (_currentProjectId() is not { } projectId)
            return;

        var result = await _lifecycle.ResumeAsync(projectId).ConfigureAwait(true);
        await ReportAsync(result, "Resumed.").ConfigureAwait(true);
    }

    private async Task ReopenAsync()
    {
        if (_currentProjectId() is not { } projectId)
            return;

        var result = await _lifecycle.ReopenAsync(projectId).ConfigureAwait(true);
        await ReportAsync(result, "Reopened.").ConfigureAwait(true);
    }

    private async Task ReportAsync(ProjectLifecycleResult result, string successMessage)
    {
        if (!result.Succeeded)
        {
            _status.Text = result.Reason ?? "That was refused.";
            ActionCompleted?.Invoke(result.Reason ?? "That was refused.", ActionOutcome.Failed);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        _status.Text = successMessage;
        ActionCompleted?.Invoke(successMessage, ActionOutcome.From(true));
    }
}
