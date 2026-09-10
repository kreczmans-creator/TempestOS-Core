using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.Bearings;
using Tempest.Core.Constants;
using Tempest.Core.Fasteners;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.Standards;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Engineering;

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
/// The Evidence workspace's own <em>Libraries</em> tab (`WP 18.2A`, §5): the
/// five governed reference libraries evidence cites (Materials, Fasteners,
/// Bearings, Standards, Constants — deliberately not the sixth,
/// Manufacturing Processes, which nothing in this Work Package's own scope
/// cites), each record shown with its source citation, released and
/// governed through the one existing review flow
/// (<see cref="ReferenceReviewService"/>) — never a second one.
/// </summary>
public sealed class LibrariesView : UserControl
{
    private readonly IMaterialCatalog _materials;
    private readonly IFastenerCatalog _fasteners;
    private readonly IBearingCatalog _bearings;
    private readonly IStandardCatalog _standards;
    private readonly IConstantCatalog _constants;
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
    /// discipline for the Object Editor's own pickers.
    /// </summary>
    public Func<string, string, SourceCitation?, CancellationToken, Task<ReviseReferenceRecordInput?>>? ReviseRecordPrompt { get; set; }

    /// <summary>Initialises a new instance of the <see cref="LibrariesView"/> class.</summary>
    public LibrariesView(
        IMaterialCatalog materials, IFastenerCatalog fasteners, IBearingCatalog bearings,
        IStandardCatalog standards, IConstantCatalog constants,
        ReferenceReviewService review, BracketCalculationWorkbench bracketCalculations)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(fasteners);
        ArgumentNullException.ThrowIfNull(bearings);
        ArgumentNullException.ThrowIfNull(standards);
        ArgumentNullException.ThrowIfNull(constants);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(bracketCalculations);

        _materials = materials;
        _fasteners = fasteners;
        _bearings = bearings;
        _standards = standards;
        _constants = constants;
        _review = review;
        _bracketCalculations = bracketCalculations;

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
            field.Margin = new Avalonia.Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceSm);
            addMaterialForm.Children.Add(field);
        }

        var addMaterialSection = new StackPanel { Spacing = DesignTokens.SpaceXs, Margin = new Avalonia.Thickness(0, 0, 0, DesignTokens.SpaceLg) };
        addMaterialSection.Children.Add(new TextBlock { Text = "Add a material", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading });
        addMaterialSection.Children.Add(addMaterialForm);

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(new TextBlock { Text = "Libraries", FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeTitle, FontWeight = DesignTokens.WeightHeading });
        body.Children.Add(_status);
        body.Children.Add(addMaterialSection);
        body.Children.Add(_rows);

        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Reloads every one of the five libraries' own records.</summary>
    public async Task RefreshAsync()
    {
        var all = await ReadAllAsync(_materials, _fasteners, _bearings, _standards, _constants).ConfigureAwait(true);

        _rows.Children.Clear();

        if (all.Count == 0)
        {
            _rows.Children.Add(new TextBlock { Text = "No reference records are seeded.", Opacity = 0.7 });
            return;
        }

        foreach (var library in all.GroupBy(r => r.Library).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            _rows.Children.Add(new TextBlock
            {
                Text = $"{library.Key} ({library.Count()})",
                FontWeight = DesignTokens.WeightHeading,
                FontSize = DesignTokens.FontSizeHeading,
                Margin = new Avalonia.Thickness(0, DesignTokens.SpaceMd, 0, DesignTokens.SpaceXs),
            });

            foreach (var row in library.OrderBy(r => r.RecordId, StringComparer.Ordinal))
                _rows.Children.Add(BuildRow(row));
        }
    }

    /// <summary>Every record across the five governed libraries evidence may cite — read fresh, never cached, so a release recorded anywhere is seen the next time this is called (`WP 18.2A`, no-restart requirement).</summary>
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

    private static async Task<IReadOnlyList<EvidenceLibraryRow>> ReadLibraryAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog, Func<TDefinition, string> displayName, CancellationToken cancellationToken)
        where TDefinition : class
    {
        var records = await catalog.ListAsync(cancellationToken).ConfigureAwait(false);

        return [.. records.Select(r => new EvidenceLibraryRow(catalog.LibraryName, r.Id, displayName(r.Definition), r.RevisionNumber, r.ValidationState, r.Source))];
    }

    private Control BuildRow(EvidenceLibraryRow row)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto"), Margin = new Avalonia.Thickness(0, DesignTokens.SpaceXs) };

        var text = new TextBlock
        {
            Text = $"{row.RecordId} — {row.DisplayName}  •  rev {row.RevisionNumber}  •  {row.ValidationState}  •  {row.Source?.ToString() ?? "(no source citation)"}",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var verify = new Button { Content = "Verify", Padding = new Avalonia.Thickness(10, 2), IsVisible = row.ValidationState == ReferenceValidationState.Draft };
        verify.Classes.Add(ChromeStyles.Subtle);
        verify.Click += async (_, _) => await OnVerifyAsync(row).ConfigureAwait(true);
        AutomationProperties.SetName(verify, $"Verify {row.RecordId}");
        Grid.SetColumn(verify, 1);
        grid.Children.Add(verify);

        var release = new Button { Content = "Release", Padding = new Avalonia.Thickness(10, 2), IsVisible = row.ValidationState is ReferenceValidationState.Draft or ReferenceValidationState.Checked or ReferenceValidationState.Validated };
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
            Padding = new Avalonia.Thickness(10, 2),
            IsVisible = ReviseRecordPrompt is not null && ReferenceValidationStates.IsRevisable(row.ValidationState),
        };
        revise.Classes.Add(ChromeStyles.Subtle);
        revise.Click += async (_, _) => await OnReviseAsync(row).ConfigureAwait(true);
        AutomationProperties.SetName(revise, $"Revise {row.RecordId}");
        Grid.SetColumn(revise, 3);
        grid.Children.Add(revise);

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
            var current = await ReadCurrentDefinitionJsonAsync(row).ConfigureAwait(true);

            var input = await ReviseRecordPrompt($"{row.Library} — {row.RecordId}", current.DefinitionJson, current.Source, CancellationToken.None).ConfigureAwait(true);
            if (input is null)
            {
                Report("Revise was cancelled.", succeeded: true);
                return;
            }

            await ReviseAsync(row, input, current.Provenance).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            Report($"Revised '{row.RecordId}'.", succeeded: true);
        }
        catch (JsonException ex)
        {
            Report($"The definition is not valid JSON for {row.Library}: {ex.Message}", succeeded: false);
        }
        catch (ReferenceDataException ex)
        {
            await RefreshAsync().ConfigureAwait(true);
            Report(ex.Message, succeeded: false);
        }
    }

    private static readonly JsonSerializerOptions ReviseJsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    /// <summary>The current record's own definition (as JSON), source citation and provenance — read once, so <see cref="ReviseAsync(EvidenceLibraryRow, ReviseReferenceRecordInput, ReferenceProvenance)"/> never re-reads (a blocking re-read on the Desktop thread is exactly what `NoBlockingPersistenceCallsTests` forbids) just to carry the provenance forward unchanged.</summary>
    private async Task<(string DefinitionJson, SourceCitation? Source, ReferenceProvenance Provenance)> ReadCurrentDefinitionJsonAsync(EvidenceLibraryRow row)
    {
        switch (row.Library)
        {
            case "Materials":
            {
                var record = await _materials.FindAsync(row.RecordId).ConfigureAwait(false) ?? throw new ReferenceRecordNotFoundException(row.Library, row.RecordId);
                return (JsonSerializer.Serialize(record.Definition, ReviseJsonOptions), record.Source, record.Provenance);
            }
            case "Fasteners":
            {
                var record = await _fasteners.FindAsync(row.RecordId).ConfigureAwait(false) ?? throw new ReferenceRecordNotFoundException(row.Library, row.RecordId);
                return (JsonSerializer.Serialize(record.Definition, ReviseJsonOptions), record.Source, record.Provenance);
            }
            case "Bearings":
            {
                var record = await _bearings.FindAsync(row.RecordId).ConfigureAwait(false) ?? throw new ReferenceRecordNotFoundException(row.Library, row.RecordId);
                return (JsonSerializer.Serialize(record.Definition, ReviseJsonOptions), record.Source, record.Provenance);
            }
            case "Standards":
            {
                var record = await _standards.FindAsync(row.RecordId).ConfigureAwait(false) ?? throw new ReferenceRecordNotFoundException(row.Library, row.RecordId);
                return (JsonSerializer.Serialize(record.Definition, ReviseJsonOptions), record.Source, record.Provenance);
            }
            case "Constants":
            {
                var record = await _constants.FindAsync(row.RecordId).ConfigureAwait(false) ?? throw new ReferenceRecordNotFoundException(row.Library, row.RecordId);
                return (JsonSerializer.Serialize(record.Definition, ReviseJsonOptions), record.Source, record.Provenance);
            }
            default:
                throw new ReferenceRecordNotFoundException(row.Library, row.RecordId);
        }
    }

    private Task ReviseAsync(EvidenceLibraryRow row, ReviseReferenceRecordInput input, ReferenceProvenance provenance)
    {
        return row.Library switch
        {
            "Materials" => _materials.ReviseAsync(
                row.RecordId, Deserialise<MaterialDefinition>(row.Library, input.DefinitionJson), provenance, input.ChangeSummary, input.Source),
            "Fasteners" => _fasteners.ReviseAsync(
                row.RecordId, Deserialise<FastenerDefinition>(row.Library, input.DefinitionJson), provenance, input.ChangeSummary, input.Source),
            "Bearings" => _bearings.ReviseAsync(
                row.RecordId, Deserialise<BearingDefinition>(row.Library, input.DefinitionJson), provenance, input.ChangeSummary, input.Source),
            "Standards" => _standards.ReviseAsync(
                row.RecordId, Deserialise<StandardDefinition>(row.Library, input.DefinitionJson), provenance, input.ChangeSummary, input.Source),
            "Constants" => _constants.ReviseAsync(
                row.RecordId, Deserialise<ConstantDefinition>(row.Library, input.DefinitionJson), provenance, input.ChangeSummary, input.Source),
            _ => throw new ReferenceRecordNotFoundException(row.Library, row.RecordId),
        };
    }

    private static TDefinition Deserialise<TDefinition>(string library, string definitionJson) where TDefinition : class =>
        JsonSerializer.Deserialize<TDefinition>(definitionJson, ReviseJsonOptions)
            ?? throw new JsonException($"The definition for '{library}' deserialised to nothing.");

    private Task VerifyAsync(EvidenceLibraryRow row)
    {
        var statement = new ReferenceReviewStatement(
            SourceConsulted: row.Source?.ToString() ?? "the record's own recorded provenance");

        return row.Library switch
        {
            "Materials" => _review.VerifyAsync(_materials, row.RecordId, statement),
            "Fasteners" => _review.VerifyAsync(_fasteners, row.RecordId, statement),
            "Bearings" => _review.VerifyAsync(_bearings, row.RecordId, statement),
            "Standards" => _review.VerifyAsync(_standards, row.RecordId, statement),
            "Constants" => _review.VerifyAsync(_constants, row.RecordId, statement),
            _ => Task.CompletedTask,
        };
    }

    private Task ReleaseAsync(EvidenceLibraryRow row)
    {
        const string rationale = "Released via the Evidence workspace's own Libraries tab.";

        return row.Library switch
        {
            "Materials" => _review.ReleaseAsync(_materials, row.RecordId, rationale),
            "Fasteners" => _review.ReleaseAsync(_fasteners, row.RecordId, rationale),
            "Bearings" => _review.ReleaseAsync(_bearings, row.RecordId, rationale),
            "Standards" => _review.ReleaseAsync(_standards, row.RecordId, rationale),
            "Constants" => _review.ReleaseAsync(_constants, row.RecordId, rationale),
            _ => Task.CompletedTask,
        };
    }

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
        }
        catch (ArgumentException ex)
        {
            Report(ex.Message, succeeded: false);
        }
    }

    private void Report(string message, bool succeeded)
    {
        _status.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(succeeded));
    }
}
