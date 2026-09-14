using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.Bearings;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Components;
using Tempest.Core.Constants;
using Tempest.Core.Evidence;
using Tempest.Core.Fasteners;
using Tempest.Core.Manufacturing;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.Standards;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Evidence;

namespace Tempest.Desktop.Views;

/// <summary>Every governed reference-data catalogue a record view or the Libraries list can reach — the seam map's own eight libraries (`WP 19.6A`, §2).</summary>
internal sealed record ReferenceLibraryCatalogues(
    IMaterialCatalog Materials,
    IFastenerCatalog Fasteners,
    IBearingCatalog Bearings,
    IStandardCatalog Standards,
    IConstantCatalog Constants,
    IProcessCatalog Manufacturing,
    IComponentCatalog Components,
    IRateCardCatalog BusinessRateCards);

/// <summary>One reference record, read generically across whichever of the eight libraries it belongs to — the shape <see cref="ReferenceRecordView"/> renders and <see cref="LibrariesView"/>'s own detail pane opens.</summary>
internal sealed record ReferenceRecordSnapshot(
    string Library,
    string RecordId,
    string DisplayName,
    object Definition,
    ReferenceProvenance Provenance,
    SourceCitation? Source,
    ReferenceValidationState ValidationState,
    int RevisionNumber);

/// <summary>One revision in a record's own history, as the Revision history section shows it.</summary>
internal sealed record ReferenceRecordRevisionRow(
    int RevisionNumber, ReferenceValidationState ValidationState, DateTimeOffset RecordedAt, string AuthorPrincipalId, string? ChangeSummary);

/// <summary>
/// The one per-library switch a reference-record editor needs — every
/// other operation is already Kind-agnostic
/// (<see cref="IReferenceDataCatalog{TDefinition}"/>,
/// <see cref="ReferenceReviewService"/>); only picking which catalogue a
/// library name means does not generalise. <see cref="LibrariesView"/> and
/// <see cref="ReferenceRecordView"/> both call through here rather than
/// each keeping its own switch (`WP 19.6A`).
/// </summary>
internal static class ReferenceLibraryAccess
{
    private static readonly JsonSerializerOptions ReviseJsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    /// <summary>
    /// The name a person reads for <paramref name="library"/>'s own
    /// routing key (`WP 19.10P`, D15) — identical to the key for every
    /// library except the rate-card one, whose own
    /// <see cref="IReferenceDataCatalog{TDefinition}.LibraryName"/> stays
    /// the literal <c>"BusinessRateCards"</c> every switch above routes
    /// on; only what reaches the screen changes.
    /// </summary>
    public static string DisplayNameFor(string library) => library == "BusinessRateCards" ? "Rate cards" : library;

    /// <summary>Reads one record generically, projected to <see cref="ReferenceRecordSnapshot"/> — <see langword="null"/> if no such record is registered.</summary>
    public static Task<ReferenceRecordSnapshot?> FindAsync(ReferenceLibraryCatalogues c, string library, string recordId, CancellationToken cancellationToken = default) =>
        library switch
        {
            "Materials" => ProjectAsync(c.Materials, recordId, d => d.Name, cancellationToken),
            "Fasteners" => ProjectAsync(c.Fasteners, recordId, d => d.Designation, cancellationToken),
            "Bearings" => ProjectAsync(c.Bearings, recordId, d => $"{d.Identity.Manufacturer} {d.Identity.ManufacturerPartNumber}", cancellationToken),
            "Standards" => ProjectAsync(c.Standards, recordId, d => d.FullDesignation, cancellationToken),
            "Constants" => ProjectAsync(c.Constants, recordId, d => $"{d.Symbol} — {d.Name}", cancellationToken),
            "Manufacturing" => ProjectAsync(c.Manufacturing, recordId, d => d.Name, cancellationToken),
            "Components" => ProjectAsync(c.Components, recordId, d => d.Designation, cancellationToken),
            "BusinessRateCards" => ProjectAsync(c.BusinessRateCards, recordId, d => d.Name, cancellationToken),
            _ => Task.FromResult<ReferenceRecordSnapshot?>(null),
        };

    private static async Task<ReferenceRecordSnapshot?> ProjectAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog, string recordId, Func<TDefinition, string> displayName, CancellationToken cancellationToken)
        where TDefinition : class
    {
        var record = await catalog.FindAsync(recordId, cancellationToken).ConfigureAwait(false);
        return record is null
            ? null
            : new ReferenceRecordSnapshot(
                catalog.LibraryName, record.Id, displayName(record.Definition), record.Definition,
                record.Provenance, record.Source, record.ValidationState, record.RevisionNumber);
    }

    /// <summary>Every revision of a record's own history, oldest first, each decoded for the validation state it carried at that point.</summary>
    public static Task<IReadOnlyList<ReferenceRecordRevisionRow>> GetHistoryAsync(ReferenceLibraryCatalogues c, string library, string recordId, CancellationToken cancellationToken = default) =>
        library switch
        {
            "Materials" => HistoryAsync(c.Materials, recordId, cancellationToken),
            "Fasteners" => HistoryAsync(c.Fasteners, recordId, cancellationToken),
            "Bearings" => HistoryAsync(c.Bearings, recordId, cancellationToken),
            "Standards" => HistoryAsync(c.Standards, recordId, cancellationToken),
            "Constants" => HistoryAsync(c.Constants, recordId, cancellationToken),
            "Manufacturing" => HistoryAsync(c.Manufacturing, recordId, cancellationToken),
            "Components" => HistoryAsync(c.Components, recordId, cancellationToken),
            "BusinessRateCards" => HistoryAsync(c.BusinessRateCards, recordId, cancellationToken),
            _ => Task.FromResult<IReadOnlyList<ReferenceRecordRevisionRow>>([]),
        };

    private static async Task<IReadOnlyList<ReferenceRecordRevisionRow>> HistoryAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog, string recordId, CancellationToken cancellationToken)
        where TDefinition : class
    {
        var history = await catalog.GetHistoryAsync(recordId, cancellationToken).ConfigureAwait(false);
        var rows = new List<ReferenceRecordRevisionRow>(history.Count);

        foreach (var revision in history)
        {
            // `IDocumentRevision` carries when/who/why but not the
            // validation state the record held at that point — that lives
            // only in the decoded document content, so each revision is
            // re-read through the catalogue's own `GetRevisionAsync`
            // rather than duplicating its decode here.
            var atRevision = await catalog.GetRevisionAsync(recordId, revision.RevisionNumber, cancellationToken).ConfigureAwait(false);
            rows.Add(new ReferenceRecordRevisionRow(revision.RevisionNumber, atRevision.ValidationState, revision.CreatedAt, revision.AuthorPrincipalId, revision.ChangeSummary));
        }

        return rows;
    }

    /// <summary>Verifies a record through <see cref="ReferenceReviewService"/> — the same governed act <see cref="LibrariesView"/>'s own row buttons already call.</summary>
    public static Task VerifyAsync(ReferenceLibraryCatalogues c, ReferenceReviewService review, string library, string recordId, ReferenceReviewStatement statement) =>
        library switch
        {
            "Materials" => review.VerifyAsync(c.Materials, recordId, statement),
            "Fasteners" => review.VerifyAsync(c.Fasteners, recordId, statement),
            "Bearings" => review.VerifyAsync(c.Bearings, recordId, statement),
            "Standards" => review.VerifyAsync(c.Standards, recordId, statement),
            "Constants" => review.VerifyAsync(c.Constants, recordId, statement),
            "Manufacturing" => review.VerifyAsync(c.Manufacturing, recordId, statement),
            "Components" => review.VerifyAsync(c.Components, recordId, statement),
            "BusinessRateCards" => review.VerifyAsync(c.BusinessRateCards, recordId, statement),
            _ => Task.CompletedTask,
        };

    /// <summary>Releases a record through <see cref="ReferenceReviewService"/>.</summary>
    public static Task ReleaseAsync(ReferenceLibraryCatalogues c, ReferenceReviewService review, string library, string recordId, string rationale) =>
        library switch
        {
            "Materials" => review.ReleaseAsync(c.Materials, recordId, rationale),
            "Fasteners" => review.ReleaseAsync(c.Fasteners, recordId, rationale),
            "Bearings" => review.ReleaseAsync(c.Bearings, recordId, rationale),
            "Standards" => review.ReleaseAsync(c.Standards, recordId, rationale),
            "Constants" => review.ReleaseAsync(c.Constants, recordId, rationale),
            "Manufacturing" => review.ReleaseAsync(c.Manufacturing, recordId, rationale),
            "Components" => review.ReleaseAsync(c.Components, recordId, rationale),
            "BusinessRateCards" => review.ReleaseAsync(c.BusinessRateCards, recordId, rationale),
            _ => Task.CompletedTask,
        };

    /// <summary>The current record's own definition (as JSON), source citation and provenance — for the Revise prompt.</summary>
    public static Task<(string DefinitionJson, SourceCitation? Source, ReferenceProvenance Provenance)> ReadDefinitionJsonAsync(
        ReferenceLibraryCatalogues c, string library, string recordId, CancellationToken cancellationToken = default) =>
        library switch
        {
            "Materials" => ReadJsonAsync(c.Materials, recordId, cancellationToken),
            "Fasteners" => ReadJsonAsync(c.Fasteners, recordId, cancellationToken),
            "Bearings" => ReadJsonAsync(c.Bearings, recordId, cancellationToken),
            "Standards" => ReadJsonAsync(c.Standards, recordId, cancellationToken),
            "Constants" => ReadJsonAsync(c.Constants, recordId, cancellationToken),
            "Manufacturing" => ReadJsonAsync(c.Manufacturing, recordId, cancellationToken),
            "Components" => ReadJsonAsync(c.Components, recordId, cancellationToken),
            "BusinessRateCards" => ReadJsonAsync(c.BusinessRateCards, recordId, cancellationToken),
            _ => throw new ReferenceRecordNotFoundException(library, recordId),
        };

    private static async Task<(string, SourceCitation?, ReferenceProvenance)> ReadJsonAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog, string recordId, CancellationToken cancellationToken)
        where TDefinition : class
    {
        var record = await catalog.FindAsync(recordId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(catalog.LibraryName, recordId);
        return (JsonSerializer.Serialize(record.Definition, ReviseJsonOptions), record.Source, record.Provenance);
    }

    /// <summary>Records a new revision of a record's own definition, provenance and source citation.</summary>
    public static Task ReviseAsync(
        ReferenceLibraryCatalogues c, string library, string recordId, string definitionJson,
        ReferenceProvenance provenance, string? changeSummary, SourceCitation? source, CancellationToken cancellationToken = default) =>
        library switch
        {
            "Materials" => c.Materials.ReviseAsync(recordId, Deserialise<MaterialDefinition>(library, definitionJson), provenance, changeSummary, source, cancellationToken),
            "Fasteners" => c.Fasteners.ReviseAsync(recordId, Deserialise<FastenerDefinition>(library, definitionJson), provenance, changeSummary, source, cancellationToken),
            "Bearings" => c.Bearings.ReviseAsync(recordId, Deserialise<BearingDefinition>(library, definitionJson), provenance, changeSummary, source, cancellationToken),
            "Standards" => c.Standards.ReviseAsync(recordId, Deserialise<StandardDefinition>(library, definitionJson), provenance, changeSummary, source, cancellationToken),
            "Constants" => c.Constants.ReviseAsync(recordId, Deserialise<ConstantDefinition>(library, definitionJson), provenance, changeSummary, source, cancellationToken),
            "Manufacturing" => c.Manufacturing.ReviseAsync(recordId, Deserialise<ProcessDefinition>(library, definitionJson), provenance, changeSummary, source, cancellationToken),
            "Components" => c.Components.ReviseAsync(recordId, Deserialise<ComponentDefinition>(library, definitionJson), provenance, changeSummary, source, cancellationToken),
            "BusinessRateCards" => c.BusinessRateCards.ReviseAsync(recordId, Deserialise<RateCard>(library, definitionJson), provenance, changeSummary, source, cancellationToken),
            _ => throw new ReferenceRecordNotFoundException(library, recordId),
        };

    private static TDefinition Deserialise<TDefinition>(string library, string definitionJson) where TDefinition : class =>
        JsonSerializer.Deserialize<TDefinition>(definitionJson, ReviseJsonOptions)
            ?? throw new JsonException($"The definition for '{library}' deserialised to nothing.");
}

/// <summary>
/// One library record, opened as a real editor (`WP 19.6A`, `po-comments.md`
/// item 5): identity, every field of its own definition (reflected
/// generically — no per-library editor), revision history, source
/// citation and "cited by", with Verify/Release/Revise on it through the
/// same governed acts <see cref="LibrariesView"/>'s own rows already call.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Guid bridge.</b> A reference record's <c>Id</c> is a
/// caller-assigned string, outside the engineering object model
/// (`IReferenceRecord{T}.Id`, never a Guid `ObjectEditorView.TryCreate`
/// could open) — this view is the record's own home, addressed by
/// <c>(Library, RecordId)</c> rather than folded into the engineering
/// document tabs.
/// </para>
/// <para>
/// <b>The definition's fields, reflected.</b> Each of the eight libraries'
/// own definition type is rendered by walking its public, non-<see cref="JsonIgnoreAttribute"/>
/// properties: a quantity-like value (<see cref="ReferenceQuantityValue"/>,
/// <see cref="ReferenceValue{TDimension}"/>, <see cref="ReferenceRange{TDimension}"/>,
/// or a bare <c>Quantity&lt;TDimension&gt;</c>, which already shows its
/// own unit through its own <c>ToString()</c>) shows its unit; a nested
/// record renders as an indented group; a list or dictionary renders as a
/// small table where every item is itself flat, or as one indented group
/// per item otherwise. Every value type (an enum, <c>DateOnly</c>, a
/// <c>Money</c>, a <c>CurrencyCode</c>) falls back to its own
/// <c>ToString()</c> — the kill switch this Work Package's own brief
/// allows for a property type with no sensible generic display.
/// </para>
/// </remarks>
public sealed class ReferenceRecordView : UserControl
{
    private const double IndentStep = DesignTokens.SpaceXl;

    private readonly ReferenceLibraryCatalogues _catalogues;
    private readonly ReferenceReviewService _review;
    private readonly IReferenceCitationIndex _citationIndex;
    private readonly Action<Guid, string> _openObjectRightUp;

    private readonly TextBlock _titleText = new() { FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeTitle, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBlock _identityText = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _createdText = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.6 };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly StackPanel _definitionPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _historyPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _citationPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _citedByPanel = new() { Spacing = DesignTokens.SpaceXs };

    private readonly Button _verifyButton = new() { Content = "Verify", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _releaseButton = new() { Content = "Release", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _reviseButton = new() { Content = "Revise", MinHeight = DesignTokens.MinControlSize };

    private string? _library;
    private string? _recordId;
    private ReferenceRecordSnapshot? _current;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Raised after Verify, Release or Revise actually changes this record — <see cref="LibrariesView"/> refreshes its own list row in response.</summary>
    public event Action? RecordChanged;

    /// <summary>Prompts for a revision — the same delegate <see cref="LibrariesView.ReviseRecordPrompt"/> already is. <see langword="null"/> leaves Revise honestly unavailable rather than run with no dialog on screen.</summary>
    public Func<string, string, SourceCitation?, CancellationToken, Task<ReviseReferenceRecordInput?>>? ReviseRecordPrompt { get; set; }

    /// <summary>Initialises a new instance of the <see cref="ReferenceRecordView"/> class.</summary>
    internal ReferenceRecordView(ReferenceLibraryCatalogues catalogues, ReferenceReviewService review, IReferenceCitationIndex citationIndex, Action<Guid, string> openObjectRightUp)
    {
        ArgumentNullException.ThrowIfNull(catalogues);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(citationIndex);
        ArgumentNullException.ThrowIfNull(openObjectRightUp);

        _catalogues = catalogues;
        _review = review;
        _citationIndex = citationIndex;
        _openObjectRightUp = openObjectRightUp;

        _verifyButton.Classes.Add(ChromeStyles.Subtle);
        _releaseButton.Classes.Add(ChromeStyles.Primary);
        _reviseButton.Classes.Add(ChromeStyles.Subtle);
        _verifyButton.Click += async (_, _) => await OnVerifyAsync().ConfigureAwait(true);
        _releaseButton.Click += async (_, _) => await OnReleaseAsync().ConfigureAwait(true);
        _reviseButton.Click += async (_, _) => await OnReviseAsync().ConfigureAwait(true);
        AutomationProperties.SetName(_verifyButton, "Verify record");
        AutomationProperties.SetName(_releaseButton, "Release record");
        AutomationProperties.SetName(_reviseButton, "Revise record");

        var header = new StackPanel { Spacing = DesignTokens.SpaceXs };
        header.Children.Add(_titleText);
        header.Children.Add(_identityText);
        header.Children.Add(_createdText);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        actions.Children.Add(_verifyButton);
        actions.Children.Add(_releaseButton);
        actions.Children.Add(_reviseButton);

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(header);
        body.Children.Add(_status);
        body.Children.Add(actions);
        body.Children.Add(BuildSection("Definition", _definitionPanel));
        body.Children.Add(BuildSection("Revision history", _historyPanel));
        body.Children.Add(BuildSection("Source citation", _citationPanel));
        body.Children.Add(BuildSection("Cited by", _citedByPanel));

        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Opens <paramref name="recordId"/> from <paramref name="library"/> and loads its content.</summary>
    public Task LoadAsync(string library, string recordId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(library);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        _library = library;
        _recordId = recordId;
        return RefreshAsync(cancellationToken);
    }

    /// <summary>Re-reads the currently open record from its own catalogue — a fresh snapshot every time, never cached, so an action taken elsewhere is seen immediately.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_library is null || _recordId is null)
            return;

        var snapshot = await ReferenceLibraryAccess.FindAsync(_catalogues, _library, _recordId, cancellationToken).ConfigureAwait(true);

        if (snapshot is null)
        {
            _current = null;
            _titleText.Text = $"{ReferenceLibraryAccess.DisplayNameFor(_library)} — {_recordId}";
            _identityText.Text = "This record no longer exists.";
            _createdText.Text = string.Empty;
            _definitionPanel.Children.Clear();
            _historyPanel.Children.Clear();
            _citationPanel.Children.Clear();
            _citedByPanel.Children.Clear();
            _verifyButton.IsVisible = false;
            _releaseButton.IsVisible = false;
            _reviseButton.IsVisible = false;
            return;
        }

        _current = snapshot;
        PopulateIdentity(snapshot);
        PopulateDefinition(snapshot);
        await PopulateHistoryAsync(cancellationToken).ConfigureAwait(true);
        PopulateSourceCitation(snapshot);
        await PopulateCitedByAsync(cancellationToken).ConfigureAwait(true);
        UpdateActionVisibility(snapshot);
    }

    private void PopulateIdentity(ReferenceRecordSnapshot snapshot)
    {
        _titleText.Text = $"{ReferenceLibraryAccess.DisplayNameFor(snapshot.Library)} — {snapshot.RecordId}";
        _identityText.Text = $"{snapshot.DisplayName}  •  rev {snapshot.RevisionNumber}  •  {snapshot.ValidationState}";
    }

    private void PopulateDefinition(ReferenceRecordSnapshot snapshot)
    {
        _definitionPanel.Children.Clear();
        foreach (var row in RenderValueRows(snapshot.Definition, indent: 0))
            _definitionPanel.Children.Add(row);
    }

    private async Task PopulateHistoryAsync(CancellationToken cancellationToken)
    {
        _historyPanel.Children.Clear();

        var rows = await ReferenceLibraryAccess.GetHistoryAsync(_catalogues, _library!, _recordId!, cancellationToken).ConfigureAwait(true);

        foreach (var row in rows)
        {
            var isCurrent = _current is not null && row.RevisionNumber == _current.RevisionNumber;
            var text = $"Rev {row.RevisionNumber}{(isCurrent ? " (current)" : string.Empty)}  •  {row.ValidationState}  •  {row.RecordedAt:yyyy-MM-dd}"
                + (string.IsNullOrWhiteSpace(row.ChangeSummary) ? string.Empty : $"  •  {row.ChangeSummary}");
            _historyPanel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = DesignTokens.FontSizeBody,
                FontWeight = isCurrent ? DesignTokens.WeightHeading : DesignTokens.WeightBody,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        _createdText.Text = rows.Count == 0
            ? string.Empty
            : $"Created {rows[0].RecordedAt:yyyy-MM-dd} by {rows[0].AuthorPrincipalId}";
    }

    private void PopulateSourceCitation(ReferenceRecordSnapshot snapshot)
    {
        _citationPanel.Children.Clear();

        if (snapshot.Source is not { } source)
        {
            _citationPanel.Children.Add(new TextBlock { Text = "(no source citation)", Opacity = 0.7, FontSize = DesignTokens.FontSizeBody });
            return;
        }

        _citationPanel.Children.Add(PropertyRow("Publisher", source.Publisher, 0));
        _citationPanel.Children.Add(PropertyRow("Work", source.Work, 0));
        if (source.Edition is not null)
            _citationPanel.Children.Add(PropertyRow("Edition", source.Edition, 0));
        if (source.Page is not null)
            _citationPanel.Children.Add(PropertyRow("Page", source.Page, 0));
        if (source.TableOrFigure is not null)
            _citationPanel.Children.Add(PropertyRow("Table or figure", source.TableOrFigure, 0));
        if (source.RowOrEntry is not null)
            _citationPanel.Children.Add(PropertyRow("Row or entry", source.RowOrEntry, 0));
    }

    private async Task PopulateCitedByAsync(CancellationToken cancellationToken)
    {
        _citedByPanel.Children.Clear();

        var citations = await _citationIndex.FindCitationsAsync(_library!, _recordId!, cancellationToken).ConfigureAwait(true);

        if (citations.Count == 0)
        {
            _citedByPanel.Children.Add(new TextBlock { Text = "Not cited by any evidence.", Opacity = 0.7, FontSize = DesignTokens.FontSizeBody });
            return;
        }

        foreach (var citation in citations)
            _citedByPanel.Children.Add(BuildCitedByRow(citation));
    }

    private Control BuildCitedByRow(ReferenceCitationRecord citation)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs) };

        var text = new TextBlock
        {
            Text = $"{citation.EvidenceDisplayName}  •  {citation.ProjectLabel}  •  {citation.Status}  •  cited at rev {citation.CitedRevisionNumber}",
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var openButton = new Button { Content = "Open", Padding = new Thickness(10, 2) };
        openButton.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(openButton, $"Open {citation.EvidenceDisplayName}");
        openButton.Click += (_, _) => _openObjectRightUp(citation.EvidenceId, Evidence.CanonicalKind);
        Grid.SetColumn(openButton, 1);
        grid.Children.Add(openButton);

        return grid;
    }

    private void UpdateActionVisibility(ReferenceRecordSnapshot snapshot)
    {
        _verifyButton.IsVisible = snapshot.ValidationState == ReferenceValidationState.Draft;
        _releaseButton.IsVisible = snapshot.ValidationState is ReferenceValidationState.Draft or ReferenceValidationState.Checked or ReferenceValidationState.Validated;
        _reviseButton.IsVisible = ReviseRecordPrompt is not null && ReferenceValidationStates.IsRevisable(snapshot.ValidationState);
    }

    private async Task OnVerifyAsync()
    {
        if (_current is null)
            return;

        try
        {
            await VerifyCoreAsync().ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            Report($"Verified '{_recordId}'.", succeeded: true);
            RecordChanged?.Invoke();
        }
        catch (ReferenceReviewException ex)
        {
            Report(ex.Message, succeeded: false);
        }
    }

    private Task VerifyCoreAsync()
    {
        var statement = new ReferenceReviewStatement(SourceConsulted: _current!.Source?.ToString() ?? "the record's own recorded provenance");
        return ReferenceLibraryAccess.VerifyAsync(_catalogues, _review, _library!, _recordId!, statement);
    }

    private async Task OnReleaseAsync()
    {
        if (_current is null)
            return;

        try
        {
            // "Release" from the record view is one action, exactly as it
            // is from `LibrariesView`'s own row: a still-Draft record is
            // verified first, then released — both real, separately
            // audited acts, never merged into one.
            if (_current.ValidationState == ReferenceValidationState.Draft)
                await VerifyCoreAsync().ConfigureAwait(true);

            await ReferenceLibraryAccess.ReleaseAsync(_catalogues, _review, _library!, _recordId!, "Released via the reference record view.").ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            Report($"Released '{_recordId}'.", succeeded: true);
            RecordChanged?.Invoke();
        }
        catch (ReferenceReviewException ex)
        {
            await RefreshAsync().ConfigureAwait(true);
            Report(ex.Message, succeeded: false);
        }
    }

    private async Task OnReviseAsync()
    {
        if (_current is null || ReviseRecordPrompt is null)
            return;

        try
        {
            var (json, source, provenance) = await ReferenceLibraryAccess.ReadDefinitionJsonAsync(_catalogues, _library!, _recordId!).ConfigureAwait(true);

            var input = await ReviseRecordPrompt($"{_library} — {_recordId}", json, source, CancellationToken.None).ConfigureAwait(true);
            if (input is null)
            {
                Report("Revise was cancelled.", succeeded: true);
                return;
            }

            await ReferenceLibraryAccess.ReviseAsync(_catalogues, _library!, _recordId!, input.DefinitionJson, provenance, input.ChangeSummary, input.Source).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            Report($"Revised '{_recordId}'.", succeeded: true);
            RecordChanged?.Invoke();
        }
        catch (JsonException ex)
        {
            Report($"The definition is not valid JSON for {_library}: {ex.Message}", succeeded: false);
        }
        catch (ReferenceDataException ex)
        {
            await RefreshAsync().ConfigureAwait(true);
            Report(ex.Message, succeeded: false);
        }
    }

    private void Report(string message, bool succeeded)
    {
        _status.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(succeeded));
    }

    private static Expander BuildSection(string title, Control content) => new()
    {
        Header = title,
        IsExpanded = true,
        Margin = DesignTokens.SectionMargin,
        Content = content,
    };

    // ------------------------------------------------------------
    // Generic definition rendering — reflects over a definition
    // record's own public properties, one row per property. A
    // quantity-like value shows its unit; a nested record renders as an
    // indented group; a list or dictionary renders as a small table where
    // every item is itself flat, or one indented group per item
    // otherwise (`WP 19.6A`, the brief's own kill switch: a property type
    // with no sensible display falls back to its own `ToString()`).
    // ------------------------------------------------------------

    private static IEnumerable<Control> RenderValueRows(object instance, int indent)
    {
        foreach (var property in GetDisplayProperties(instance.GetType()))
        {
            var value = property.GetValue(instance);
            var label = Humanize(property.Name);

            if (value is null)
            {
                yield return PropertyRow(label, "(not recorded)", indent, muted: true);
                continue;
            }

            if (TryFormatQuantityLike(value, out var quantityText))
            {
                yield return PropertyRow(label, quantityText, indent);
                continue;
            }

            if (value is string text)
            {
                yield return PropertyRow(label, text, indent);
                continue;
            }

            var valueType = value.GetType();

            if (valueType.IsValueType)
            {
                yield return PropertyRow(label, value.ToString() ?? string.Empty, indent);
                continue;
            }

            if (IsDictionary(valueType))
            {
                var entries = ((System.Collections.IEnumerable)value).Cast<object>().ToList();
                yield return HeadingRow($"{label} ({entries.Count})", indent);
                yield return entries.Count == 0
                    ? PropertyRow(string.Empty, "(none)", indent + 1, muted: true)
                    : RenderDictionaryTable(entries, indent + 1);
                continue;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                var items = enumerable.Cast<object?>().Where(o => o is not null).Select(o => o!).ToList();
                yield return HeadingRow($"{label} ({items.Count})", indent);
                yield return RenderList(items, indent + 1);
                continue;
            }

            // A nested record — walk its own public properties, indented
            // one level further.
            yield return HeadingRow(label, indent);
            foreach (var nestedRow in RenderValueRows(value, indent + 1))
                yield return nestedRow;
        }
    }

    private static Control RenderList(List<object> items, int indent)
    {
        if (items.Count == 0)
            return PropertyRow(string.Empty, "(none)", indent, muted: true);

        var first = items[0];

        // Leaf items (strings, enums, other value types, quantity-like
        // wrappers) — a one-column table.
        if (first is string || first.GetType().IsValueType || TryFormatQuantityLike(first, out _))
            return RenderTable(items, columns: null, indent);

        // Record items whose own properties are all flat — a genuine
        // small table, one column per property.
        var columns = GetDisplayProperties(first.GetType()).ToList();
        if (columns.Count > 0 && columns.All(p => FormatSingleLineValue(p.GetValue(first)) is not null))
            return RenderTable(items, columns, indent);

        // Anything deeper (a list of records that themselves nest a list,
        // dictionary or further record) — one indented group per item
        // rather than a table no grid could stay readable as.
        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        for (var i = 0; i < items.Count; i++)
        {
            panel.Children.Add(HeadingRow($"#{i + 1}", indent));
            foreach (var row in RenderValueRows(items[i], indent + 1))
                panel.Children.Add(row);
        }

        return panel;
    }

    private static Control RenderTable(List<object> items, List<PropertyInfo>? columns, int indent)
    {
        var columnCount = columns?.Count ?? 1;
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("Auto", columnCount))),
            Margin = new Thickness(indent * IndentStep, 0, 0, DesignTokens.SpaceXs),
        };
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var c = 0; c < columnCount; c++)
        {
            var header = new TextBlock
            {
                Text = columns is null ? "Value" : Humanize(columns[c].Name),
                FontWeight = DesignTokens.WeightLabel,
                FontSize = DesignTokens.FontSizeCaption,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, DesignTokens.SpaceLg, DesignTokens.SpaceXs),
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, c);
            grid.Children.Add(header);
        }

        for (var r = 0; r < items.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < columnCount; c++)
            {
                var cellValue = columns is null ? items[r] : columns[c].GetValue(items[r]);
                var text = FormatSingleLineValue(cellValue) ?? cellValue?.ToString() ?? "—";
                var cell = new TextBlock
                {
                    Text = text,
                    FontSize = DesignTokens.FontSizeBody,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = DesignTokens.MonoFont,
                    Margin = new Thickness(0, 0, DesignTokens.SpaceLg, DesignTokens.SpaceXs),
                };
                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        return grid;
    }

    private static Control RenderDictionaryTable(List<object> entries, int indent)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto"),
            Margin = new Thickness(indent * IndentStep, 0, 0, DesignTokens.SpaceXs),
        };

        for (var r = 0; r < entries.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var entryType = entries[r].GetType();
            var key = entryType.GetProperty("Key")!.GetValue(entries[r]);
            var val = entryType.GetProperty("Value")!.GetValue(entries[r]);

            var keyCell = new TextBlock
            {
                Text = key?.ToString() ?? string.Empty,
                FontSize = DesignTokens.FontSizeBody,
                Opacity = 0.8,
                Margin = new Thickness(0, 0, DesignTokens.SpaceLg, DesignTokens.SpaceXs),
            };
            var valueCell = new TextBlock
            {
                Text = FormatSingleLineValue(val) ?? val?.ToString() ?? "—",
                FontSize = DesignTokens.FontSizeBody,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = DesignTokens.MonoFont,
                Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXs),
            };
            Grid.SetRow(keyCell, r);
            Grid.SetColumn(keyCell, 0);
            Grid.SetRow(valueCell, r);
            Grid.SetColumn(valueCell, 1);
            grid.Children.Add(keyCell);
            grid.Children.Add(valueCell);
        }

        return grid;
    }

    private static Control PropertyRow(string label, string value, int indent, bool muted = false)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("200,*"),
            Margin = new Thickness(indent * IndentStep, DesignTokens.SpaceXs, 0, DesignTokens.SpaceXs),
        };
        var nameText = new TextBlock { Text = label, FontSize = DesignTokens.FontSizeBody, Opacity = 0.8, VerticalAlignment = VerticalAlignment.Top };
        var valueText = new TextBlock
        {
            Text = value,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = TextWrapping.Wrap,
            Opacity = muted ? 0.5 : 1.0,
            FontFamily = DesignTokens.MonoFont,
        };
        Grid.SetColumn(nameText, 0);
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(nameText);
        grid.Children.Add(valueText);
        return grid;
    }

    private static Control HeadingRow(string label, int indent) => new TextBlock
    {
        Text = label,
        FontWeight = DesignTokens.WeightLabel,
        FontSize = DesignTokens.FontSizeBody,
        Margin = new Thickness(indent * IndentStep, DesignTokens.SpaceSm, 0, DesignTokens.SpaceXs),
    };

    /// <summary>A value's own single-line text, or <see langword="null"/> if it is not flat enough for one (a further list, dictionary or nested record).</summary>
    private static string? FormatSingleLineValue(object? value)
    {
        if (value is null)
            return "—";

        if (TryFormatQuantityLike(value, out var quantityText))
            return quantityText;

        if (value is string s)
            return s;

        return value.GetType().IsValueType ? value.ToString() : null;
    }

    /// <summary>
    /// Formats a quantity-bearing wrapper's own value and unit as one
    /// string. A bare <c>Quantity&lt;TDimension&gt;</c> needs no entry
    /// here — its own <c>ToString()</c> already reads "value unit", and it
    /// falls into the general value-type <c>ToString()</c> path above.
    /// </summary>
    private static bool TryFormatQuantityLike(object value, out string text)
    {
        if (value is ReferenceQuantityValue quantityValue)
        {
            text = FormatMagnitude(quantityValue.EncodedValue.Value, quantityValue.EncodedValue.UnitSymbol);
            return true;
        }

        var type = value.GetType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ReferenceValue<>))
        {
            var quantity = type.GetProperty("Value")!.GetValue(value);
            text = quantity?.ToString() ?? "(not recorded)";
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ReferenceRange<>))
        {
            var minimum = type.GetProperty("Minimum")!.GetValue(value);
            var maximum = type.GetProperty("Maximum")!.GetValue(value);
            text = (minimum, maximum) switch
            {
                (null, null) => "(not recorded)",
                ({ } min, null) => $"≥ {min}",
                (null, { } max) => $"≤ {max}",
                ({ } min, { } max) => $"{min} – {max}",
            };
            return true;
        }

        text = string.Empty;
        return false;
    }

    private static string FormatMagnitude(double value, string unitSymbol)
    {
        var formatted = value.ToString("0.####", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(unitSymbol) ? formatted : $"{formatted} {unitSymbol}";
    }

    private static bool IsDictionary(Type type) =>
        typeof(System.Collections.IDictionary).IsAssignableFrom(type)
        || type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));

    private static IEnumerable<PropertyInfo> GetDisplayProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() is null);

    /// <summary>"NominalDiameter" → "Nominal Diameter" — a space before each capital that follows a lower-case letter or precedes one.</summary>
    private static string Humanize(string propertyName)
    {
        var builder = new System.Text.StringBuilder(propertyName.Length + 8);

        for (var i = 0; i < propertyName.Length; i++)
        {
            var c = propertyName[i];
            if (i > 0 && char.IsUpper(c)
                && (char.IsLower(propertyName[i - 1]) || (i + 1 < propertyName.Length && char.IsLower(propertyName[i + 1]))))
            {
                builder.Append(' ');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
