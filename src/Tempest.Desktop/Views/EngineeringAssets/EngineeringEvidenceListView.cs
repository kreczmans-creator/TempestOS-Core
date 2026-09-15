using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views.EngineeringAssets;

/// <summary>
/// Engineering Assets → Engineering evidence (`WP 21.2B`, scope item 1):
/// every <c>EngineeringEvidence</c> item cited by any calculation pack,
/// template or verification artefact's own governance facts, flattened
/// into one filterable list. Nothing here is a second store — every row
/// is read fresh out of the three libraries' own records, and Open
/// navigates to the record that actually holds it.
/// </summary>
public sealed class EngineeringEvidenceListView : UserControl
{
    private readonly ICalculationPackCatalog _packs;
    private readonly ITemplateCatalog _templates;
    private readonly IVerificationArtefactCatalog _artefacts;
    private readonly Action<string, string> _openOwner;

    private readonly TextBox _filter = new() { Watermark = "Filter", MinHeight = DesignTokens.MinControlSize };
    private readonly StackPanel _rows = new() { Spacing = DesignTokens.SpaceXs };

    private IReadOnlyList<EngineeringEvidenceRow> _all = [];

    /// <summary>Initialises a new instance of the <see cref="EngineeringEvidenceListView"/> class.</summary>
    /// <param name="packs">The calculation pack library.</param>
    /// <param name="templates">The template library.</param>
    /// <param name="artefacts">The verification artefact library.</param>
    /// <param name="openOwner">Opens the owning record — <c>("Calculation pack" | "Template" | "Verification artefact", recordId)</c>.</param>
    public EngineeringEvidenceListView(
        ICalculationPackCatalog packs, ITemplateCatalog templates, IVerificationArtefactCatalog artefacts, Action<string, string> openOwner)
    {
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(artefacts);
        ArgumentNullException.ThrowIfNull(openOwner);

        _packs = packs;
        _templates = templates;
        _artefacts = artefacts;
        _openOwner = openOwner;

        AutomationProperties.SetName(_filter, "Filter engineering evidence");
        _filter.TextChanged += (_, _) => Render();

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(new TextBlock { Text = "Engineering evidence", FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeTitle, FontWeight = DesignTokens.WeightHeading });
        body.Children.Add(new TextBlock
        {
            Text = "Every piece of supporting material any calculation pack, template or verification artefact cites.",
            FontSize = DesignTokens.FontSizeCaption, Opacity = 0.75,
        });
        body.Children.Add(_filter);
        body.Children.Add(_rows);

        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Reloads every evidence item across all three libraries.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<EngineeringEvidenceRow>();

        foreach (var pack in await _packs.ListAsync(cancellationToken).ConfigureAwait(true))
            rows.AddRange(EngineeringAssetFormatting.RowsFor("Calculation pack", pack.Id, pack.Definition.Reference, pack.Definition.Governance));

        foreach (var template in await _templates.ListAsync(cancellationToken).ConfigureAwait(true))
            rows.AddRange(EngineeringAssetFormatting.RowsFor("Template", template.Id, template.Definition.Reference, template.Definition.Governance));

        foreach (var artefact in await _artefacts.ListAsync(cancellationToken).ConfigureAwait(true))
        {
            rows.AddRange(EngineeringAssetFormatting.RowsFor("Verification artefact", artefact.Id, artefact.Definition.Reference, artefact.Definition.Governance));

            // `VerificationArtefact.Evidence` is a second, dedicated field
            // (distinct from `Governance.Evidence`) — the evidence
            // supporting the verification result itself.
            rows.AddRange(artefact.Definition.Evidence.Select(e => new EngineeringEvidenceRow(
                e.Kind, e.Description, e.IsLocatable, e.IsIndependent,
                "Verification artefact", artefact.Id, artefact.Definition.Reference)));
        }

        _all = rows;
        Render();
    }

    private void Render()
    {
        var rows = _all.Select((row, index) => new AssetListRow($"{row.CitedByRecordId}#{index}", row.Label));

        // The list identity carries an index because one record can cite
        // several evidence items — Open still resolves to the owning
        // record, read back out of `_all` by position.
        EngineeringAssetListBuilder.Render(_rows, rows, _filter.Text, syntheticId =>
        {
            var index = int.Parse(syntheticId[(syntheticId.IndexOf('#') + 1)..], System.Globalization.CultureInfo.InvariantCulture);
            var row = _all[index];
            _openOwner(row.CitedByKind, row.CitedByRecordId);
        });
    }
}
