using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// `v0.24.0` U2 (Xero Technical Design §5, §11): the <b>Link to Xero…</b>
/// prompt. Asks Xero for the contacts that may be one TempestOS customer or
/// supplier (<see cref="IXeroContactLinker.FindCandidatesAsync"/>, ranked VAT
/// number, then customer code as Xero's <c>ContactNumber</c>, then the same
/// name, then a similar name), lists them for the Product Owner, and links
/// only the one they confirm (<see cref="IXeroContactLinker.LinkExistingAsync"/>)
/// — or, when none fits, creates the contact in Xero
/// (<see cref="IXeroContactLinker.CreateAsync"/>, which looks the customer
/// code up first so it never makes two).
/// </summary>
/// <remarks>
/// <para>
/// <b>Never by name alone.</b> Nothing is linked until the Product Owner
/// chooses a contact and confirms; a document for an organisation that is
/// not linked stays Blocked. <b>Create in Xero</b> is offered only once the
/// search has answered, so the matches are seen before a new contact is made.
/// </para>
/// <para>
/// <b>Never blocks the UI thread.</b> Every Xero call is awaited; while one
/// is in flight the prompt says so and its buttons are disabled. A search may
/// be cancelled; a link or create may not (its answer decides whether a
/// contact exists), so Cancel waits for it.
/// </para>
/// <para>
/// <b>Reasons, not codes.</b> A refusal, an unreachable Xero, a lost answer
/// or a needed re-authorisation is shown as a sentence saying what happened
/// and what to do; the prompt stays open so the Product Owner can try again.
/// </para>
/// <para>
/// Hosted as an overlay by <see cref="CustomersSuppliersView"/>; any other
/// view (for example a Blocked badge) can host its own instance and call
/// <see cref="PromptAsync"/>.
/// </para>
/// </remarks>
public sealed class XeroContactLinkPrompt : Border
{
    /// <summary>The automation name of the list of Xero contacts offered.</summary>
    public const string CandidateListName = "Xero contact matches";

    /// <summary>The automation name of the line that reports progress and failures.</summary>
    public const string StatusName = "Xero link status";

    /// <summary>The automation name (and caption) of the confirm button.</summary>
    public const string LinkButtonName = "Link to selected contact";

    /// <summary>The automation name (and caption) of the create button.</summary>
    public const string CreateButtonName = "Create in Xero";

    /// <summary>The automation name (and caption) of the search-again button.</summary>
    public const string SearchAgainButtonName = "Search Xero again";

    /// <summary>The automation name (and caption) of the cancel button.</summary>
    public const string CancelButtonName = "Cancel";

    /// <summary>The automation name of the line shown when Xero has no matching contact.</summary>
    public const string NoMatchesName = "No Xero matches";

    private readonly IXeroContactLinker _linker;

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _lead = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly ListBox _candidates = new() { MaxHeight = 260, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBlock _noMatches = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, IsVisible = false, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly Button _linkButton = new() { Content = LinkButtonName, MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _createButton = new() { Content = CreateButtonName, MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _searchAgainButton = new() { Content = SearchAgainButtonName, MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = CancelButtonName, MinHeight = DesignTokens.ControlSizeMedium };

    private TaskCompletionSource<XeroLink?>? _pending;
    private CancellationTokenRegistration _pendingRegistration;
    private CancellationTokenSource? _searchCancellation;
    private string? _organisationReference;
    private string _organisationName = string.Empty;
    private bool _searching;
    private bool _writing;
    private bool _searched;

    /// <summary>Initialises a new instance of the <see cref="XeroContactLinkPrompt"/> class, initially hidden.</summary>
    /// <param name="linker">The X2 contact linker every search, link and create goes through.</param>
    public XeroContactLinkPrompt(IXeroContactLinker linker)
    {
        ArgumentNullException.ThrowIfNull(linker);
        _linker = linker;

        IsVisible = false;
        MinWidth = 420;
        MaxWidth = 560;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _lead.Text = "Choose the Xero contact that is this organisation. Nothing is linked until you confirm; if none of them is right, create it in Xero.";
        _noMatches.Text = "Xero has no contact that looks like this organisation. Create it in Xero, or search again after adding it there.";

        foreach (var (control, name) in new (Control, string)[]
                 {
                     (_candidates, CandidateListName), (_noMatches, NoMatchesName), (_status, StatusName),
                     (_linkButton, LinkButtonName), (_createButton, CreateButtonName),
                     (_searchAgainButton, SearchAgainButtonName), (_cancelButton, CancelButtonName),
                 })
            AutomationProperties.SetName(control, name);

        AutomationProperties.SetName(this, "Link to Xero");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);

        foreach (var button in new[] { _linkButton, _createButton, _searchAgainButton, _cancelButton })
            ToolTip.SetTip(button, AutomationProperties.GetName(button));

        _linkButton.Classes.Add(ChromeStyles.Primary);
        _createButton.Classes.Add(ChromeStyles.Subtle);
        _searchAgainButton.Classes.Add(ChromeStyles.Subtle);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);

        var buttons = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceMd, 0, 0) };
        foreach (var button in new[] { _cancelButton, _searchAgainButton, _createButton, _linkButton })
        {
            button.Margin = new Thickness(DesignTokens.SpaceSm, DesignTokens.SpaceSm, 0, 0);
            buttons.Children.Add(button);
        }

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_lead);
        body.Children.Add(_candidates);
        body.Children.Add(_noMatches);
        body.Children.Add(_status);
        body.Children.Add(buttons);
        Child = body;

        _candidates.SelectionChanged += (_, _) => UpdateButtons();
        _linkButton.Click += async (_, _) => await LinkSelectedAsync().ConfigureAwait(true);
        _createButton.Click += async (_, _) => await CreateAsync().ConfigureAwait(true);
        _searchAgainButton.Click += async (_, _) => await SearchAsync().ConfigureAwait(true);
        _cancelButton.Click += (_, _) => Cancel();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Cancel();
        };

        DialogModality.Install(this);
        UpdateButtons();
    }

    /// <summary>The contacts Xero offered in the last search, strongest first (empty until it answers).</summary>
    internal IReadOnlyList<XeroContactCandidate> Candidates { get; private set; } = [];

    /// <summary>Whether a Xero call is in flight.</summary>
    internal bool IsBusy => _searching || _writing;

    /// <summary>The progress or failure line currently shown.</summary>
    internal string? StatusText => _status.Text;

    /// <summary>
    /// Shows the prompt for the organisation <paramref name="organisationReference"/>,
    /// starts the search, and completes with the link the Product Owner made
    /// (an existing contact confirmed, or one created in Xero), or
    /// <see langword="null"/> when they cancelled.
    /// </summary>
    /// <param name="organisationReference">The organisation's reference (its link key).</param>
    /// <param name="organisationName">Its name, for the title.</param>
    /// <param name="cancellationToken">Closes the prompt as cancelled.</param>
    /// <remarks>
    /// While a link or create is in flight the prompt stays on that
    /// organisation — the write's answer belongs to it alone — so a second
    /// call completes at once with <see langword="null"/> and leaves the
    /// prompt (and the write) as they are.
    /// </remarks>
    public Task<XeroLink?> PromptAsync(string organisationReference, string organisationName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisationReference);

        if (_writing)
            return Task.FromResult<XeroLink?>(null);

        Complete(null);

        _organisationReference = organisationReference.Trim();
        _organisationName = string.IsNullOrWhiteSpace(organisationName) ? _organisationReference : organisationName.Trim();
        _title.Text = $"Link {_organisationName} to Xero";
        _status.Text = string.Empty;
        _noMatches.IsVisible = false;
        _searched = false;
        ShowCandidates([]);
        IsVisible = true;

        var pending = new TaskCompletionSource<XeroLink?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = pending;
        if (cancellationToken.CanBeCanceled)
            _pendingRegistration = cancellationToken.Register(() => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (ReferenceEquals(_pending, pending)) Cancel(); }));

        _ = SearchAsync();
        _cancelButton.Focus();
        return pending.Task;
    }

    /// <summary>"Matched on VAT number", …: why Xero's contact was offered, in words.</summary>
    /// <param name="matchedOn">The candidate's <see cref="XeroContactCandidate.MatchedOn"/>.</param>
    internal static string DescribeMatch(string matchedOn) => matchedOn switch
    {
        XeroContactMatcher.MatchedOnVatNumber => "Same VAT number",
        XeroContactMatcher.MatchedOnContactNumber => "Xero's contact number is this customer code",
        XeroContactMatcher.MatchedOnExactName => "Same name",
        XeroContactMatcher.MatchedOnSimilarName => "Similar name",
        _ => matchedOn,
    };

    /// <summary>One line per candidate: its name, why it was offered, and what identifies it.</summary>
    /// <param name="candidate">The candidate.</param>
    internal static string DescribeCandidate(XeroContactCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var parts = new List<string> { DescribeMatch(candidate.MatchedOn) };
        if (!string.IsNullOrWhiteSpace(candidate.TaxNumber))
            parts.Add($"VAT {candidate.TaxNumber.Trim()}");
        if (!string.IsNullOrWhiteSpace(candidate.ContactNumber))
            parts.Add($"contact number {candidate.ContactNumber.Trim()}");
        if (!string.IsNullOrWhiteSpace(candidate.EmailAddress))
            parts.Add(candidate.EmailAddress.Trim());
        if (candidate.IsCustomer && candidate.IsSupplier)
            parts.Add("customer and supplier in Xero");
        else if (candidate.IsCustomer)
            parts.Add("customer in Xero");
        else if (candidate.IsSupplier)
            parts.Add("supplier in Xero");
        if (!IsActive(candidate))
            parts.Add($"{candidate.ContactStatus.ToLowerInvariant()} in Xero — restore it there to link it");

        return $"{candidate.Name} — {string.Join(" · ", parts)}";
    }

    /// <summary>What a Xero call that did not succeed means for the Product Owner, as one sentence with what to do next.</summary>
    /// <typeparam name="T">The call's answer type.</typeparam>
    /// <param name="result">The call's result.</param>
    /// <param name="action">What was being done ("search Xero", "link the contact", "create the contact").</param>
    internal static string DescribeFailure<T>(ConnectorResult<T> result, string action)
    {
        ArgumentNullException.ThrowIfNull(result);

        var reason = string.IsNullOrWhiteSpace(result.Reason) ? null : result.Reason.Trim();
        return result.Outcome switch
        {
            ConnectorOutcome.Rejected => $"Could not {action}: {reason ?? "Xero refused it."}",
            ConnectorOutcome.Reauthorise => $"Could not {action}: Xero needs to be re-authorised (Settings → Invoicing → Re-authorise).{(reason is null ? string.Empty : " " + reason)}",
            ConnectorOutcome.Unavailable => $"Could not {action}: Xero could not be reached{(reason is null ? "." : $" ({reason}).")} Nothing was changed; try again when it is back.",
            ConnectorOutcome.Unknown => $"Could not {action}: Xero's answer was lost{(reason is null ? "." : $" ({reason}).")} Try again — TempestOS checks Xero first, so it never makes a second contact.",
            _ => $"Could not {action}.",
        };
    }

    private static bool IsActive(XeroContactCandidate candidate) =>
        string.Equals(candidate.ContactStatus, XeroContactMatcher.ActiveStatus, StringComparison.OrdinalIgnoreCase);

    private async Task SearchAsync()
    {
        if (_organisationReference is not { } reference || _writing)
            return;

        _searchCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;

        // Create in Xero waits for this search's answer: the matches are
        // seen before a new contact is made.
        _searched = false;
        _searching = true;
        _noMatches.IsVisible = false;
        _status.Text = "Searching Xero for matching contacts…";
        UpdateButtons();

        ConnectorResult<IReadOnlyList<XeroContactCandidate>>? result = null;
        string? failure = null;
        try
        {
            result = await _linker.FindCandidatesAsync(reference, cancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Cancelled or superseded: a newer search or the prompt closing owns the state now.
        }
        catch (Exception ex)
        {
            // The UI boundary: any fault (a store defect included) is shown, never left to the dispatcher.
            failure = $"Could not search Xero: {ex.Message}";
        }

        // A newer search or the prompt closing owns the state now; this
        // search's token source is disposed on the way out and must not stay
        // reachable.
        if (!ReferenceEquals(_searchCancellation, cancellation))
            return;

        _searchCancellation = null;
        _searching = false;
        if (!IsVisible)
            return;

        if (result is { Outcome: ConnectorOutcome.Ok, Value: { } found })
        {
            _searched = true;
            ShowCandidates(found);
            _noMatches.IsVisible = found.Count == 0;
            _status.Text = found.Count switch
            {
                0 => string.Empty,
                1 => "Xero has 1 contact that may be this organisation. Select it and confirm, or create a new one.",
                _ => $"Xero has {found.Count} contacts that may be this organisation, strongest match first. Select one and confirm, or create a new one.",
            };
        }
        else if (result is not null)
        {
            _status.Text = DescribeFailure(result, "search Xero");
        }
        else if (failure is not null)
        {
            _status.Text = failure;
        }

        UpdateButtons();
    }

    private void ShowCandidates(IReadOnlyList<XeroContactCandidate> candidates)
    {
        Candidates = candidates;
        _candidates.ItemsSource = candidates
            .Select(c =>
            {
                var item = new ListBoxItem
                {
                    Content = new TextBlock { Text = DescribeCandidate(c), TextWrapping = TextWrapping.Wrap },
                    Tag = c,
                    IsEnabled = IsActive(c),
                };
                AutomationProperties.SetName(item, DescribeCandidate(c));
                return item;
            })
            .ToList();
        _candidates.SelectedItem = null;
        _candidates.IsVisible = candidates.Count > 0;
    }

    /// <summary>Selects the offered contact <paramref name="contactId"/> (as a click on its row would).</summary>
    internal bool Select(string contactId)
    {
        if (_candidates.ItemsSource is not IEnumerable<ListBoxItem> items
            || items.FirstOrDefault(i => i.Tag is XeroContactCandidate c && string.Equals(c.ContactId, contactId, StringComparison.OrdinalIgnoreCase)) is not { } item)
            return false;

        _candidates.SelectedItem = item;
        return true;
    }

    private XeroContactCandidate? SelectedCandidate => _candidates.SelectedItem is ListBoxItem { Tag: XeroContactCandidate candidate } ? candidate : null;

    private async Task LinkSelectedAsync()
    {
        if (_organisationReference is not { } reference || IsBusy)
            return;

        if (SelectedCandidate is not { } candidate)
        {
            _status.Text = "Select the Xero contact that is this organisation first.";
            return;
        }

        if (!IsActive(candidate))
        {
            _status.Text = $"'{candidate.Name}' is {candidate.ContactStatus.ToLowerInvariant()} in Xero; only an active contact can be linked. Restore it in Xero first.";
            return;
        }

        await WriteAsync(
            $"Linking to '{candidate.Name}' in Xero…",
            "link the contact",
            ct => _linker.LinkExistingAsync(reference, candidate.ContactId, ct)).ConfigureAwait(true);
    }

    private async Task CreateAsync()
    {
        if (_organisationReference is not { } reference || IsBusy)
            return;

        await WriteAsync(
            $"Creating '{_organisationName}' in Xero…",
            "create the contact in Xero",
            ct => _linker.CreateAsync(reference, ct)).ConfigureAwait(true);
    }

    private async Task WriteAsync(string progress, string action, Func<CancellationToken, Task<ConnectorResult<XeroLink>>> write)
    {
        // The write's answer belongs to the prompt it was started from: the
        // prompt cannot be re-pointed while it runs (see PromptAsync), and
        // only this pending answer is completed with its link.
        var pending = _pending;
        _writing = true;
        _status.Text = progress;
        UpdateButtons();

        ConnectorResult<XeroLink>? result = null;
        try
        {
            // Never cancelled: whether Xero made the contact is only known
            // from the answer, so a write always runs to it.
            result = await write(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // The UI boundary: any fault (a store defect included) is shown, never left to the dispatcher.
            _status.Text = $"Could not {action}: {ex.Message}";
        }
        finally
        {
            _writing = false;
        }

        if (result is { Outcome: ConnectorOutcome.Ok, Value: { } link })
        {
            if (ReferenceEquals(_pending, pending))
                Complete(link);
            else
                pending?.TrySetResult(link);
            return;
        }

        if (result is not null && ReferenceEquals(_pending, pending))
            _status.Text = DescribeFailure(result, action);

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var busy = IsBusy;
        _linkButton.IsEnabled = !busy && SelectedCandidate is { } selected && IsActive(selected);
        _createButton.IsEnabled = !busy && _searched;
        _searchAgainButton.IsEnabled = !busy;
        _cancelButton.IsEnabled = !_writing;
        _candidates.IsEnabled = !_writing;
    }

    private void Cancel()
    {
        if (_writing)
            return;

        Complete(null);
    }

    private void Complete(XeroLink? link)
    {
        _searchCancellation?.Cancel();
        _searchCancellation = null;
        _searching = false;

        _pendingRegistration.Dispose();
        _pendingRegistration = default;

        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(link);
    }
}
