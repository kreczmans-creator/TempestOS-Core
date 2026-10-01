using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.People;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Evidence;

namespace Tempest.Desktop.Views;

/// <summary>
/// Business → Staff (Product Owner runbook B1, 2026-10-01: "This isn't
/// engineering reference data, this is business reference data … Should be
/// Business/Staff."): the consultancy's own people — the
/// <see cref="IPersonCatalog"/> records requirement owners, timesheets and
/// rate-card grades pick from — listed one compact row each (name and
/// release status, runbook F1), with an <em>Add a person</em> form that adds
/// and releases in one act, and the record opened beside the list in the
/// same <see cref="ReferenceRecordView"/> every reference library uses.
/// </summary>
/// <remarks>
/// Moved, not duplicated: People no longer appears under Engineering →
/// Reference data (<see cref="LibrariesView"/>). A client's people are not
/// staff — they are contacts of an organisation, under Customers &amp;
/// Suppliers.
/// </remarks>
public sealed class StaffView : UserControl
{
    private readonly IPersonCatalog _persons;
    private readonly ReferenceReviewService _review;

    private readonly StackPanel _rows = new() { Spacing = 0 };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly TextBox _displayName = new() { Watermark = "Display name", MinHeight = DesignTokens.MinControlSize, MinWidth = 180 };
    private readonly TextBox _role = new() { Watermark = "Role", MinHeight = DesignTokens.MinControlSize, MinWidth = 140 };
    private readonly TextBox _email = new() { Watermark = "Email", MinHeight = DesignTokens.MinControlSize, MinWidth = 180 };
    private readonly TextBox _phone = new() { Watermark = "Phone", MinHeight = DesignTokens.MinControlSize, MinWidth = 140 };
    private readonly Button _addButton = new() { Content = "Add & Release", MinHeight = DesignTokens.MinControlSize };

    private readonly DockPanel _container = new();
    private readonly ScrollViewer _listScroll;
    private readonly ScrollViewer _detailScroll;
    private readonly ReferenceRecordView _detail;
    private string? _openRecordId;

    /// <summary>Raised after an action completes — every Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>The open record's own Revise prompt (see <see cref="LibrariesView.ReviseRecordPrompt"/>); <see langword="null"/> leaves Revise unavailable.</summary>
    public Func<string, string, SourceCitation?, CancellationToken, Task<ReviseReferenceRecordInput?>>? ReviseRecordPrompt
    {
        get => _detail.ReviseRecordPrompt;
        set => _detail.ReviseRecordPrompt = value;
    }

    internal StaffView(ReferenceLibraryCatalogues catalogues, ReferenceReviewService review, IReferenceCitationIndex citationIndex, Action<Guid, string> openObjectRightUp)
    {
        ArgumentNullException.ThrowIfNull(catalogues);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(citationIndex);
        ArgumentNullException.ThrowIfNull(openObjectRightUp);

        _persons = catalogues.People;
        _review = review;

        _detail = new ReferenceRecordView(catalogues, review, citationIndex, openObjectRightUp);
        _detail.ActionCompleted += (message, outcome) =>
        {
            _status.Text = message;
            ActionCompleted?.Invoke(message, outcome);
        };
        _detail.RecordChanged += () => _ = RefreshAsync();

        AutomationProperties.SetName(_displayName, "Display name");
        AutomationProperties.SetName(_role, "Role");
        AutomationProperties.SetName(_email, "Email");
        AutomationProperties.SetName(_phone, "Phone");
        AutomationProperties.SetName(_addButton, "Add & Release");
        ToolTip.SetTip(_addButton, "Add the person and release them, ready to be picked");
        _addButton.Classes.Add(ChromeStyles.Primary);
        _addButton.Click += async (_, _) => await OnAddAsync().ConfigureAwait(true);

        var form = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var field in new Control[] { _displayName, _role, _email, _phone, _addButton })
        {
            field.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceSm);
            form.Children.Add(field);
        }

        var addSection = new StackPanel { Spacing = DesignTokens.SpaceXs, Margin = new Thickness(0, 0, 0, DesignTokens.SpaceLg) };
        addSection.Children.Add(new TextBlock { Text = "Add a person", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading });
        addSection.Children.Add(form);

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(new TextBlock { Text = "Staff", FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeTitle, FontWeight = DesignTokens.WeightHeading });
        body.Children.Add(new TextBlock
        {
            Text = "The consultancy's own people — picked as requirement owners, on timesheets and against rate-card grades. A client's or supplier's people are contacts under Customers & Suppliers.",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(_status);
        body.Children.Add(addSection);
        body.Children.Add(_rows);
        _listScroll = new ScrollViewer { Content = body };

        var detailBody = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        detailBody.Children.Add(_detail);
        _detailScroll = new ScrollViewer { Content = detailBody, IsVisible = false };

        DockPanel.SetDock(_listScroll, Dock.Left);
        _container.Children.Add(_listScroll);
        _container.Children.Add(_detailScroll);
        Content = _container;

        AutomationProperties.SetName(this, "Staff");
    }

    /// <summary>The record id a person's display name is registered under — the same rule <see cref="PersonAddPrompt"/> uses.</summary>
    public static string RecordIdFor(string displayName) =>
        "person-" + new string(displayName.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

    /// <summary>Re-reads the people list.</summary>
    public async Task RefreshAsync()
    {
        var records = await _persons.ListAsync().ConfigureAwait(true);

        _rows.Children.Clear();
        _rows.Children.Add(new TextBlock
        {
            Text = $"People ({records.Count})",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeHeading,
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXs),
        });

        if (records.Count == 0)
        {
            _rows.Children.Add(new TextBlock { Text = "No staff yet — add the first person above.", Opacity = 0.7 });
            return;
        }

        foreach (var record in records.OrderBy(r => r.Definition.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var recordId = record.Id;
            var state = record.ValidationState;
            _rows.Children.Add(ReferenceRecordListBuilder.BuildRow(
                recordId, Describe(record.Definition), state,
                () => OpenRecordAsync(recordId),
                () => OnReleaseAsync(recordId, state)));
        }
    }

    private static string Describe(Person person) =>
        person.Role is { Length: > 0 } role ? $"{person.DisplayName} ({role})" : person.DisplayName;

    private async Task OpenRecordAsync(string recordId)
    {
        _openRecordId = recordId;
        _listScroll.Width = 460;
        _detailScroll.IsVisible = true;
        await _detail.LoadAsync(_persons.LibraryName, recordId).ConfigureAwait(true);
    }

    /// <summary>Gets the record id currently open beside the list, if any.</summary>
    internal string? OpenRecordId => _openRecordId;

    private async Task OnReleaseAsync(string recordId, ReferenceValidationState state)
    {
        try
        {
            if (state == ReferenceValidationState.Draft)
                await _review.VerifyAsync(_persons, recordId, new ReferenceReviewStatement(SourceConsulted: "the record's own recorded provenance")).ConfigureAwait(true);

            await _review.ReleaseAsync(_persons, recordId, "Released from Business → Staff.").ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            Report($"Released '{recordId}'.", succeeded: true);
        }
        catch (ReferenceReviewException ex)
        {
            await RefreshAsync().ConfigureAwait(true);
            Report(ex.Message, succeeded: false);
        }
    }

    /// <summary>
    /// Adds the person and releases them in one act, so they can be picked
    /// at once — Verify then Release, both real, separately audited acts of
    /// <see cref="ReferenceReviewService"/> (the identical idiom
    /// <see cref="PersonAddPrompt"/> uses). A person is not transcribed from
    /// a datasheet: <see cref="PersonProvenance.Default"/> names TempestOS
    /// itself as the source, which is what lets the record leave Draft.
    /// </summary>
    private async Task OnAddAsync()
    {
        var displayName = _displayName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            Report("Enter a display name before adding a person.", succeeded: false);
            return;
        }

        var recordId = RecordIdFor(displayName);
        var person = new Person
        {
            DisplayName = displayName,
            Role = NullIfEmpty(_role.Text),
            Email = NullIfEmpty(_email.Text),
            Phone = NullIfEmpty(_phone.Text),
        };

        try
        {
            await _persons.RegisterAsync(recordId, person, PersonProvenance.Default).ConfigureAwait(true);
            await _review.VerifyAsync(_persons, recordId, new ReferenceReviewStatement(SourceConsulted: "the record's own recorded provenance")).ConfigureAwait(true);
            await _review.ReleaseAsync(_persons, recordId, "Added and released from Business → Staff.").ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is ArgumentException or DuplicateReferenceRecordException or DuplicateReferenceKeyException or ReferenceReviewException)
        {
            await RefreshAsync().ConfigureAwait(true);
            Report(ex.Message, succeeded: false);
            return;
        }

        _displayName.Text = string.Empty;
        _role.Text = string.Empty;
        _email.Text = string.Empty;
        _phone.Text = string.Empty;

        await RefreshAsync().ConfigureAwait(true);
        Report($"Added and released '{displayName}'.", succeeded: true);

        // The Product Owner guard (`po-comments.md` item 5): Add opens the
        // new record right up.
        await OpenRecordAsync(recordId).ConfigureAwait(true);
    }

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private void Report(string message, bool succeeded)
    {
        _status.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(succeeded));
    }
}
