using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.Bearings;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Components;
using Tempest.Core.Constants;
using Tempest.Core.Fasteners;
using Tempest.Core.Manufacturing;
using Tempest.Core.Materials;
using Tempest.Core.People;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.Standards;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Engineering;
using Tempest.Workspace.Evidence;

namespace Tempest.Desktop.Views;

/// <summary>
/// One reference-data record, as the Evidence workspace's own Libraries
/// tab and Citation picker both need it — record id, name, revision,
/// validation state and its structured source citation (`WP 18.2A`, §5).
/// </summary>
public sealed record EvidenceLibraryRow(
    string Library, string RecordId, string DisplayName, int RevisionNumber,
    ReferenceValidationState ValidationState, SourceCitation? Source)
{
    /// <summary>Whether evidence may cite this record — released, and only released (`ADR-0148`).</summary>
    public bool IsCitable => ValidationState == ReferenceValidationState.Released;
}

/// <summary>
/// The Evidence workspace's own <em>Libraries</em> tab (`WP 18.2A`, §5;
/// master/detail and the three remaining libraries added `WP 19.6A`):
/// every governed reference library, each record shown with its source
/// citation, opened in a real record view (<see cref="ReferenceRecordView"/>)
/// beside the list, released and governed through the one existing review
/// flow (<see cref="ReferenceReviewService"/>) — never a second one.
/// </summary>
public sealed class LibrariesView : UserControl
{
    private readonly IMaterialCatalog _materials;
    private readonly IFastenerCatalog _fasteners;
    private readonly IBearingCatalog _bearings;
    private readonly IStandardCatalog _standards;
    private readonly IConstantCatalog _constants;
    private readonly IProcessCatalog _manufacturing;
    private readonly IComponentCatalog _components;
    private readonly IRateCardCatalog _businessRateCards;
    private readonly IPersonCatalog _persons;
    private readonly ReferenceLibraryCatalogues _catalogues;
    private readonly ReferenceReviewService _review;
    private readonly BracketCalculationWorkbench _bracketCalculations;

    private readonly StackPanel _rows = new() { Spacing = DesignTokens.SpaceXs };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly TextBox _newMaterialName = new() { Watermark = "Name", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _newMaterialDesignation = new() { Watermark = "Designation", MinHeight = DesignTokens.MinControlSize };
    private readonly ComboBox _newMaterialFamily = new() { MinHeight = DesignTokens.MinControlSize, ItemsSource = Enum.GetValues<MaterialFamily>() };
    private readonly TextBox _newMaterialYield = new() { Watermark = "Yield strength (MPa)", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _newMaterialDensity = new() { Watermark = "Density (g/cm3)", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _newMaterialSourceOrganisation = new() { Watermark = "Source organisation", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _newMaterialSourceDocument = new() { Watermark = "Source document", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _addMaterialButton = new() { Content = "Add Material", MinHeight = DesignTokens.MinControlSize };

    // `WP 20.10F` (Product Owner finding D8): a person needs no source
    // organisation/document of their own typed in — see `OnAddPersonAsync`'s
    // own remarks for the fixed provenance every added person is stamped
    // with instead.
    private readonly TextBox _newPersonDisplayName = new() { Watermark = "Display name", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _newPersonRole = new() { Watermark = "Role", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _newPersonEmail = new() { Watermark = "Email", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _addPersonButton = new() { Content = "Add Person", MinHeight = DesignTokens.MinControlSize };

    // `WP 19.6A`: master/detail — a row's own Open action or double-tap
    // shows the record view beside the list at typical widths, or in
    // place of it (with Back) below `DesignTokens.CompactShellWidth`.
    // A `DockPanel`, deliberately not a `Grid`: a test (this file's own
    // and `EvidenceWorkspaceJourneyTests`/`EvidenceCheckIssueReviseJourneyTests`)
    // finds one row by `GetLogicalDescendants().OfType<Grid>().First(g =>
    // g ... contains the record's own text)` — if this container were
    // itself a `Grid`, that same search would match the container before
    // ever reaching an actual row, since the container's own descendants
    // transitively contain every row's text too.
    private readonly DockPanel _container = new();
    private readonly ScrollViewer _listScroll;
    private readonly ScrollViewer _detailScroll;
    private readonly Button _backButton = new() { Content = "← Back to Libraries", MinHeight = DesignTokens.MinControlSize };
    private readonly ReferenceRecordView _detail;

    private (string Library, string RecordId)? _openRecord;
    private bool _compact;
    private Func<string, string, SourceCitation?, CancellationToken, Task<ReviseReferenceRecordInput?>>? _reviseRecordPrompt;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>
    /// Prompts for a revision — the record's own label, its current
    /// definition as JSON and its current source citation in; a
    /// <see cref="ReviseReferenceRecordInput"/> (or <see langword="null"/>
    /// for cancelled) out (`WP 18.2B`, closing a gap `WP 18.2A` disclosed).
    /// <see langword="null"/> (the default — any test that constructs this
    /// view directly without it) leaves Revise honestly unavailable rather
    /// than run with no dialog on screen, mirroring
    /// <see cref="Editors.EvidenceEditorSupport"/>'s own identical
    /// discipline for the Object Editor's own pickers. Also forwarded to
    /// the detail pane's own identical prompt, so Revise behaves the same
    /// whether started from a row or from the open record itself.
    /// </summary>
    public Func<string, string, SourceCitation?, CancellationToken, Task<ReviseReferenceRecordInput?>>? ReviseRecordPrompt
    {
        get => _reviseRecordPrompt;
        set
        {
            _reviseRecordPrompt = value;
            _detail.ReviseRecordPrompt = value;
        }
    }

    /// <summary>Initialises a new instance of the <see cref="LibrariesView"/> class.</summary>
    public LibrariesView(
        IMaterialCatalog materials, IFastenerCatalog fasteners, IBearingCatalog bearings,
        IStandardCatalog standards, IConstantCatalog constants, IProcessCatalog manufacturing,
        IComponentCatalog components, IRateCardCatalog businessRateCards, IPersonCatalog persons,
        ReferenceReviewService review, BracketCalculationWorkbench bracketCalculations,
        IReferenceCitationIndex citationIndex, Action<Guid, string> openObjectRightUp)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(fasteners);
        ArgumentNullException.ThrowIfNull(bearings);
        ArgumentNullException.ThrowIfNull(standards);
        ArgumentNullException.ThrowIfNull(constants);
        ArgumentNullException.ThrowIfNull(manufacturing);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(businessRateCards);
        ArgumentNullException.ThrowIfNull(persons);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(bracketCalculations);
        ArgumentNullException.ThrowIfNull(citationIndex);
        ArgumentNullException.ThrowIfNull(openObjectRightUp);

        _materials = materials;
        _fasteners = fasteners;
        _bearings = bearings;
        _standards = standards;
        _constants = constants;
        _manufacturing = manufacturing;
        _components = components;
        _businessRateCards = businessRateCards;
        _persons = persons;
        _catalogues = new ReferenceLibraryCatalogues(materials, fasteners, bearings, standards, constants, manufacturing, components, businessRateCards, persons);
        _review = review;
        _bracketCalculations = bracketCalculations;

        _detail = new ReferenceRecordView(_catalogues, review, citationIndex, openObjectRightUp);
        _detail.ActionCompleted += (message, outcome) =>
        {
            _status.Text = message;
            ActionCompleted?.Invoke(message, outcome);
        };
        _detail.RecordChanged += () => _ = RefreshAsync();

        _addMaterialButton.Classes.Add(ChromeStyles.Primary);
        _addMaterialButton.Click += async (_, _) => await OnAddMaterialAsync().ConfigureAwait(true);

        AutomationProperties.SetName(_newMaterialName, "Name");
        AutomationProperties.SetName(_newMaterialDesignation, "Designation");
        AutomationProperties.SetName(_newMaterialFamily, "Material family");
        AutomationProperties.SetName(_newMaterialYield, "Yield strength (MPa)");
        AutomationProperties.SetName(_newMaterialDensity, "Density (g/cm3)");
        AutomationProperties.SetName(_newMaterialSourceOrganisation, "Source organisation");
        AutomationProperties.SetName(_newMaterialSourceDocument, "Source document");
        AutomationProperties.SetName(_addMaterialButton, "Add Material");
        ToolTip.SetTip(_addMaterialButton, "Add Material");

        var addMaterialForm = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var field in new Control[]
                 {
                     _newMaterialName, _newMaterialDesignation, _newMaterialFamily, _newMaterialYield,
                     _newMaterialDensity, _newMaterialSourceOrganisation, _newMaterialSourceDocument, _addMaterialButton,
                 })
        {
            field.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceSm);
            addMaterialForm.Children.Add(field);
        }

        var addMaterialSection = new StackPanel { Spacing = DesignTokens.SpaceXs, Margin = new Thickness(0, 0, 0, DesignTokens.SpaceLg) };
        addMaterialSection.Children.Add(new TextBlock { Text = "Add a material", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading });
        addMaterialSection.Children.Add(addMaterialForm);

        // `WP 20.10F` (Product Owner finding D8): the People library's own
        // "Add a person" form — the identical inline-form pattern "Add a
        // material" above already establishes, with no source
        // organisation/document fields (`OnAddPersonAsync`'s own remarks).
        _addPersonButton.Classes.Add(ChromeStyles.Primary);
        _addPersonButton.Click += async (_, _) => await OnAddPersonAsync().ConfigureAwait(true);

        AutomationProperties.SetName(_newPersonDisplayName, "Display name");
        AutomationProperties.SetName(_newPersonRole, "Role");
        AutomationProperties.SetName(_newPersonEmail, "Email");
        AutomationProperties.SetName(_addPersonButton, "Add Person");
        ToolTip.SetTip(_addPersonButton, "Add Person");

        var addPersonForm = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var field in new Control[] { _newPersonDisplayName, _newPersonRole, _newPersonEmail, _addPersonButton })
        {
            field.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceSm);
            addPersonForm.Children.Add(field);
        }

        var addPersonSection = new StackPanel { Spacing = DesignTokens.SpaceXs, Margin = new Thickness(0, 0, 0, DesignTokens.SpaceLg) };
        addPersonSection.Children.Add(new TextBlock { Text = "Add a person", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading });
        addPersonSection.Children.Add(addPersonForm);

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(new TextBlock { Text = "Libraries", FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeTitle, FontWeight = DesignTokens.WeightHeading });
        body.Children.Add(_status);
        body.Children.Add(addMaterialSection);
        body.Children.Add(addPersonSection);
        body.Children.Add(_rows);

        _listScroll = new ScrollViewer { Content = body };

        _backButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_backButton, "Back to Libraries");
        _backButton.Click += (_, _) => CloseRecord();

        var detailBody = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        detailBody.Children.Add(_backButton);
        detailBody.Children.Add(_detail);
        _detailScroll = new ScrollViewer { Content = detailBody };

        DockPanel.SetDock(_listScroll, Dock.Left);
        _container.Children.Add(_listScroll);
        _container.Children.Add(_detailScroll);
        Content = _container;
        UpdateLayoutMode();
    }

    /// <summary>Below <see cref="DesignTokens.CompactShellWidth"/> the open record replaces the list (with Back) rather than sitting beside it — the same threshold the rail, header and ribbon already fold at.</summary>
    public void SetCompact(bool compact)
    {
        if (_compact == compact)
            return;

        _compact = compact;
        UpdateLayoutMode();
    }

    private void UpdateLayoutMode()
    {
        var hasOpenRecord = _openRecord is not null;
        var sideBySide = hasOpenRecord && !_compact;
        var detailOnly = hasOpenRecord && _compact;

        _listScroll.IsVisible = !detailOnly;
        _detailScroll.IsVisible = hasOpenRecord;
        _backButton.IsVisible = detailOnly;

        // Side by side: the list keeps a fixed rail width and the open
        // record fills what remains (`_detailScroll` is the DockPanel's
        // last child, so it always fills whatever the list does not
        // take). List-only or detail-only: whichever side is visible
        // gets the whole pane back the moment the other is hidden.
        _listScroll.Width = sideBySide ? 480 : double.NaN;
    }

    private async Task OpenRecordAsync(string library, string recordId)
    {
        _openRecord = (library, recordId);
        UpdateLayoutMode();
        await _detail.LoadAsync(library, recordId).ConfigureAwait(true);
    }

    private void CloseRecord()
    {
        _openRecord = null;
        UpdateLayoutMode();
    }

    /// <summary>Reloads every governed library's own records.</summary>
    /// <remarks>
    /// `WP 19.10P` (D15): every one of the eight governed libraries gets
    /// its own heading, whether or not it currently holds a record —
    /// grouping only the records that exist (as this used to) leaves an
    /// empty library invisible rather than listed-with-zero, which is
    /// indistinguishable from "a library missing" (§7c's own named
    /// failure condition for this surface).
    /// </remarks>
    public async Task RefreshAsync()
    {
        var all = await ReadAllLibrariesAsync().ConfigureAwait(true);
        var byLibrary = all.ToLookup(r => r.Library);

        _rows.Children.Clear();

        foreach (var libraryName in AllLibraryNames.OrderBy(name => name, StringComparer.Ordinal))
        {
            var records = byLibrary[libraryName].OrderBy(r => r.RecordId, StringComparer.Ordinal).ToList();

            _rows.Children.Add(new TextBlock
            {
                // `WP 19.10P` (D15): sorted and looked up by the routing
                // key (unchanged, and shared with `EvidenceLibraryRow.Library`),
                // but shown by its own display name — "Rate cards" for the
                // one library whose key and screen text differ.
                Text = $"{ReferenceLibraryAccess.DisplayNameFor(libraryName)} ({records.Count})",
                FontWeight = DesignTokens.WeightHeading,
                FontSize = DesignTokens.FontSizeHeading,
                Margin = new Thickness(0, DesignTokens.SpaceMd, 0, DesignTokens.SpaceXs),
            });

            if (records.Count == 0)
            {
                _rows.Children.Add(new TextBlock { Text = "No records yet", Opacity = 0.7 });
                continue;
            }

            foreach (var row in records)
                _rows.Children.Add(BuildRow(row));
        }
    }

    /// <summary>The nine governed libraries' own canonical names (People added `WP 20.10F`), straight from each catalog's own <see cref="IReferenceDataCatalog{TDefinition}.LibraryName"/> — never restated as a literal here, so this list can never drift from what each catalog actually reports.</summary>
    private IEnumerable<string> AllLibraryNames =>
    [
        _materials.LibraryName, _fasteners.LibraryName, _bearings.LibraryName, _standards.LibraryName,
        _constants.LibraryName, _manufacturing.LibraryName, _components.LibraryName, _businessRateCards.LibraryName,
        _persons.LibraryName,
    ];

    /// <summary>
    /// Every record across the five governed libraries evidence may cite —
    /// read fresh, never cached, so a release recorded anywhere is seen
    /// the next time this is called (`WP 18.2A`, no-restart requirement).
    /// Deliberately still only the five (`ADR-0148`'s own citable set):
    /// Manufacturing, Components and BusinessRateCards are reviewable
    /// (`WP 19.6A`) but nothing in this Work Package's own scope extends
    /// what Evidence may cite.
    /// </summary>
    public static async Task<IReadOnlyList<EvidenceLibraryRow>> ReadAllAsync(
        IMaterialCatalog materials, IFastenerCatalog fasteners, IBearingCatalog bearings,
        IStandardCatalog standards, IConstantCatalog constants, CancellationToken cancellationToken = default)
    {
        var rows = new List<EvidenceLibraryRow>();

        rows.AddRange(await ReadLibraryAsync(materials, d => d.Name, cancellationToken).ConfigureAwait(false));
        rows.AddRange(await ReadLibraryAsync(fasteners, d => d.Designation, cancellationToken).ConfigureAwait(false));
        rows.AddRange(await ReadLibraryAsync(bearings, d => $"{d.Identity.Manufacturer} {d.Identity.ManufacturerPartNumber}", cancellationToken).ConfigureAwait(false));
        rows.AddRange(await ReadLibraryAsync(standards, d => d.FullDesignation, cancellationToken).ConfigureAwait(false));
        rows.AddRange(await ReadLibraryAsync(constants, d => $"{d.Symbol} — {d.Name}", cancellationToken).ConfigureAwait(false));

        return rows;
    }

    /// <summary>Every record across all nine governed libraries — what the Libraries tab itself lists, wider than <see cref="ReadAllAsync"/>'s citable five (`WP 19.6A`; People added `WP 20.10F` — deliberately not part of <see cref="ReadAllAsync"/>'s own citable set: a person is picked as a requirement's owner, never cited by Evidence).</summary>
    private async Task<IReadOnlyList<EvidenceLibraryRow>> ReadAllLibrariesAsync()
    {
        var rows = new List<EvidenceLibraryRow>(await ReadAllAsync(_materials, _fasteners, _bearings, _standards, _constants).ConfigureAwait(false));

        rows.AddRange(await ReadLibraryAsync(_manufacturing, d => d.Name, default).ConfigureAwait(false));
        rows.AddRange(await ReadLibraryAsync(_components, d => d.Designation, default).ConfigureAwait(false));
        rows.AddRange(await ReadLibraryAsync(_businessRateCards, d => d.Name, default).ConfigureAwait(false));
        rows.AddRange(await ReadLibraryAsync(_persons, d => d.Role is { Length: > 0 } role ? $"{d.DisplayName} ({role})" : d.DisplayName, default).ConfigureAwait(false));

        return rows;
    }

    private static async Task<IReadOnlyList<EvidenceLibraryRow>> ReadLibraryAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog, Func<TDefinition, string> displayName, CancellationToken cancellationToken)
        where TDefinition : class
    {
        var records = await catalog.ListAsync(cancellationToken).ConfigureAwait(false);

        return [.. records.Select(r => new EvidenceLibraryRow(catalog.LibraryName, r.Id, displayName(r.Definition), r.RevisionNumber, r.ValidationState, r.Source))];
    }

    private Control BuildRow(EvidenceLibraryRow row)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs) };

        var text = new TextBlock
        {
            Text = $"{row.RecordId} — {row.DisplayName}  •  rev {row.RevisionNumber}  •  {row.ValidationState}  •  {row.Source?.ToString() ?? "(no source citation)"}",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        // `WP 19.6A`: Open, and a double-tap on the row itself, show the
        // record view — the row's text stays a plain label rather than a
        // second clickable target, so the automation walk still sees
        // exactly one named control per action.
        grid.DoubleTapped += async (_, _) => await OpenRecordAsync(row.Library, row.RecordId).ConfigureAwait(true);

        var verify = new Button { Content = "Verify", Padding = new Thickness(10, 2), IsVisible = row.ValidationState == ReferenceValidationState.Draft };
        verify.Classes.Add(ChromeStyles.Subtle);
        verify.Click += async (_, _) => await OnVerifyAsync(row).ConfigureAwait(true);
        AutomationProperties.SetName(verify, $"Verify {row.RecordId}");
        Grid.SetColumn(verify, 1);
        grid.Children.Add(verify);

        var release = new Button { Content = "Release", Padding = new Thickness(10, 2), IsVisible = row.ValidationState is ReferenceValidationState.Draft or ReferenceValidationState.Checked or ReferenceValidationState.Validated };
        release.Classes.Add(ChromeStyles.Primary);
        release.Click += async (_, _) => await OnReleaseAsync(row).ConfigureAwait(true);
        AutomationProperties.SetName(release, $"Release {row.RecordId}");
        Grid.SetColumn(release, 2);
        grid.Children.Add(release);

        // `WP 18.2B`: Revise carries the definition and provenance forward
        // (only the definition, source citation and change summary are
        // collected here) — offered wherever `ReviseAsync` itself would
        // not refuse, i.e. everywhere but Released/Superseded
        // (`ReferenceValidationStates.IsRevisable`).
        var revise = new Button
        {
            Content = "Revise",
            Padding = new Thickness(10, 2),
            IsVisible = ReviseRecordPrompt is not null && ReferenceValidationStates.IsRevisable(row.ValidationState),
        };
        revise.Classes.Add(ChromeStyles.Subtle);
        revise.Click += async (_, _) => await OnReviseAsync(row).ConfigureAwait(true);
        AutomationProperties.SetName(revise, $"Revise {row.RecordId}");
        Grid.SetColumn(revise, 3);
        grid.Children.Add(revise);

        var open = new Button { Content = "Open", Padding = new Thickness(10, 2) };
        open.Classes.Add(ChromeStyles.Flat);
        open.Click += async (_, _) => await OpenRecordAsync(row.Library, row.RecordId).ConfigureAwait(true);
        AutomationProperties.SetName(open, $"Open {row.RecordId}");
        Grid.SetColumn(open, 4);
        grid.Children.Add(open);

        return grid;
    }

    private async Task OnVerifyAsync(EvidenceLibraryRow row)
    {
        try
        {
            await VerifyAsync(row).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            Report($"Verified '{row.RecordId}'.", succeeded: true);
        }
        catch (ReferenceReviewException ex)
        {
            Report(ex.Message, succeeded: false);
        }
    }

    private async Task OnReleaseAsync(EvidenceLibraryRow row)
    {
        try
        {
            // "Release a Draft record" is one action from the tab (§5): a
            // record still Draft is verified first, with a plain statement
            // naming its own already-recorded provenance, then released —
            // both real, separately audited acts of ReferenceReviewService,
            // never merged into one.
            if (row.ValidationState == ReferenceValidationState.Draft)
                await VerifyAsync(row).ConfigureAwait(true);

            await ReleaseAsync(row).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            Report($"Released '{row.RecordId}'.", succeeded: true);
        }
        catch (ReferenceReviewException ex)
        {
            await RefreshAsync().ConfigureAwait(true);
            Report(ex.Message, succeeded: false);
        }
    }

    private async Task OnReviseAsync(EvidenceLibraryRow row)
    {
        if (ReviseRecordPrompt is null)
            return;

        try
        {
            var (definitionJson, source, provenance) = await ReferenceLibraryAccess.ReadDefinitionJsonAsync(_catalogues, row.Library, row.RecordId).ConfigureAwait(true);

            var input = await ReviseRecordPrompt($"{row.Library} — {row.RecordId}", definitionJson, source, CancellationToken.None).ConfigureAwait(true);
            if (input is null)
            {
                Report("Revise was cancelled.", succeeded: true);
                return;
            }

            await ReferenceLibraryAccess.ReviseAsync(_catalogues, row.Library, row.RecordId, input.DefinitionJson, provenance, input.ChangeSummary, input.Source).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            Report($"Revised '{row.RecordId}'.", succeeded: true);

            // The Product Owner guard (`po-comments.md` item 5): Revise
            // opens the new revision right up, exactly as Add does below.
            await OpenRecordAsync(row.Library, row.RecordId).ConfigureAwait(true);
        }
        catch (System.Text.Json.JsonException ex)
        {
            Report($"The definition is not valid JSON for {row.Library}: {ex.Message}", succeeded: false);
        }
        catch (ReferenceDataException ex)
        {
            await RefreshAsync().ConfigureAwait(true);
            Report(ex.Message, succeeded: false);
        }
    }

    private Task VerifyAsync(EvidenceLibraryRow row)
    {
        var statement = new ReferenceReviewStatement(
            SourceConsulted: row.Source?.ToString() ?? "the record's own recorded provenance");

        return ReferenceLibraryAccess.VerifyAsync(_catalogues, _review, row.Library, row.RecordId, statement);
    }

    private Task ReleaseAsync(EvidenceLibraryRow row) =>
        ReferenceLibraryAccess.ReleaseAsync(_catalogues, _review, row.Library, row.RecordId, "Released via the Evidence workspace's own Libraries tab.");

    private async Task OnAddMaterialAsync()
    {
        if (string.IsNullOrWhiteSpace(_newMaterialName.Text) || string.IsNullOrWhiteSpace(_newMaterialDesignation.Text))
        {
            Report("Enter a name and a designation before adding a material.", succeeded: false);
            return;
        }

        try
        {
            var material = new NewMaterialRecord(
                _newMaterialName.Text ?? string.Empty,
                _newMaterialDesignation.Text ?? string.Empty,
                _newMaterialFamily.SelectedItem is MaterialFamily family ? family : MaterialFamily.Unspecified,
                _newMaterialYield.Text ?? string.Empty,
                _newMaterialDensity.Text ?? string.Empty,
                _newMaterialSourceOrganisation.Text ?? string.Empty,
                _newMaterialSourceDocument.Text ?? string.Empty);

            var added = await _bracketCalculations.AddMaterialAsync(material).ConfigureAwait(true);

            _newMaterialName.Text = string.Empty;
            _newMaterialDesignation.Text = string.Empty;
            _newMaterialYield.Text = string.Empty;
            _newMaterialDensity.Text = string.Empty;
            _newMaterialSourceOrganisation.Text = string.Empty;
            _newMaterialSourceDocument.Text = string.Empty;

            await RefreshAsync().ConfigureAwait(true);
            Report($"Added material '{added.Designation}'.", succeeded: true);

            // The Product Owner guard (`po-comments.md` item 5): Add opens
            // the new record right up.
            await OpenRecordAsync("Materials", added.RecordId).ConfigureAwait(true);
        }
        catch (ArgumentException ex)
        {
            Report(ex.Message, succeeded: false);
        }
    }

    /// <summary>
    /// Adds one person record of the user's own as Draft (`WP 20.10F`,
    /// Product Owner finding D8) — mirrors <see cref="OnAddMaterialAsync"/>'s
    /// own shape, minus a source organisation/document: a person is not
    /// transcribed from a datasheet or a standard, so nothing outside
    /// TempestOS itself could ever be named as the source of their own
    /// name. The fixed provenance below still names <em>something</em>
    /// (`IdentifiesASource`), which is what lets the record leave Draft at
    /// all; Verify/Release afterwards is the same governed act every other
    /// library's own row already offers, unchanged.
    /// </summary>
    private async Task OnAddPersonAsync()
    {
        if (string.IsNullOrWhiteSpace(_newPersonDisplayName.Text))
        {
            Report("Enter a display name before adding a person.", succeeded: false);
            return;
        }

        try
        {
            var displayName = _newPersonDisplayName.Text.Trim();
            var recordId = "person-" + new string(displayName.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

            var person = new Person
            {
                DisplayName = displayName,
                Role = NullIfEmpty(_newPersonRole.Text),
                Email = NullIfEmpty(_newPersonEmail.Text),
            };

            await _persons.RegisterAsync(recordId, person, PersonProvenance.Default).ConfigureAwait(true);

            _newPersonDisplayName.Text = string.Empty;
            _newPersonRole.Text = string.Empty;
            _newPersonEmail.Text = string.Empty;

            await RefreshAsync().ConfigureAwait(true);
            Report($"Added person '{displayName}'.", succeeded: true);

            // The Product Owner guard (`po-comments.md` item 5): Add opens
            // the new record right up.
            await OpenRecordAsync(_persons.LibraryName, recordId).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is ArgumentException or DuplicateReferenceRecordException or DuplicateReferenceKeyException)
        {
            Report(ex.Message, succeeded: false);
        }
    }

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private void Report(string message, bool succeeded)
    {
        _status.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(succeeded));
    }
}
