using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Tempest.Core.Calculations;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.Materials;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Engineering;
using Tempest.Workspace.Files;

namespace Tempest.Desktop.Views.EngineeringAssets;

/// <summary>
/// Engineering → Modules → Engineering Assets (`WP 21.2B`; `TD-160`,
/// `TD-165`): the merged engineering capability's own area — the
/// Product Owner's "close all of those" instruction against the gap list
/// that named this surface's absence, decided at the release candidate.
/// </summary>
/// <remarks>
/// Five tabs over the capability `src/Tempest.Core/EngineeringAssets/`
/// already models and nothing under <c>src/Tempest.Desktop</c> read
/// before this Work Package: <b>Calculation packs</b> and
/// <b>Templates</b> (`E1`/`E2`), <b>Verification artefacts</b> (`E3`),
/// <b>Engineering evidence</b> (every item any of the three cites,
/// flattened), and <b>Bracket verification</b> — the one governed
/// journey this area actually performs (`TD-165`): Check runs
/// <see cref="GovernedBracketCheckService"/>, Record writes through
/// <see cref="BracketEngineeringRecordService"/> into an existing
/// calculation pack and verification artefact, so the artefact then
/// lists under the tab beside it.
/// </remarks>
public sealed class EngineeringAssetsView : UserControl
{
    private readonly CalculationPackListView _calculationPacks;
    private readonly EngineeringTemplateListView _templates;
    private readonly VerificationArtefactListView _verificationArtefacts;
    private readonly EngineeringEvidenceListView _evidence;
    private readonly BracketVerificationView _bracketVerification;

    private readonly TabItem _calculationPacksTab;
    private readonly TabItem _templatesTab;
    private readonly TabItem _verificationArtefactsTab;
    private readonly TabItem _evidenceTab;
    private readonly TabItem _bracketTab;
    private readonly TabControl _tabs = new();

    /// <summary>Raised after an action completes anywhere in this area — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Initialises a new instance of the <see cref="EngineeringAssetsView"/> class.</summary>
    public EngineeringAssetsView(
        ICalculationPackCatalog calculationPacks,
        ICalculationPackValidationService calculationPackValidation,
        ITemplateCatalog templates,
        ITemplateValidationService templateValidation,
        IVerificationArtefactCatalog verificationArtefacts,
        IVerificationArtefactValidationService verificationArtefactValidation,
        IEngineeringTraceRegister trace,
        IMaterialCatalog materials,
        GovernedBracketCheckService bracketCheck,
        BracketEngineeringRecordService bracketRecords,
        IFilePicker filePicker,
        Func<string?> sessionPrincipalId)
    {
        _calculationPacks = new CalculationPackListView(calculationPacks, calculationPackValidation, trace, filePicker);
        _templates = new EngineeringTemplateListView(templates, templateValidation);
        _verificationArtefacts = new VerificationArtefactListView(verificationArtefacts, verificationArtefactValidation);
        _evidence = new EngineeringEvidenceListView(calculationPacks, templates, verificationArtefacts, OpenOwner);
        _bracketVerification = new BracketVerificationView(
            materials, bracketCheck, bracketRecords, calculationPacks, verificationArtefacts, sessionPrincipalId, OnRecordedAsync);

        _calculationPacks.ActionCompleted += (m, o) => ActionCompleted?.Invoke(m, o);
        _templates.ActionCompleted += (m, o) => ActionCompleted?.Invoke(m, o);
        _verificationArtefacts.ActionCompleted += (m, o) => ActionCompleted?.Invoke(m, o);
        _bracketVerification.ActionCompleted += (m, o) => ActionCompleted?.Invoke(m, o);

        _calculationPacksTab = new TabItem { Header = "Calculation packs", Content = _calculationPacks };
        _templatesTab = new TabItem { Header = "Templates", Content = _templates };
        _verificationArtefactsTab = new TabItem { Header = "Verification artefacts", Content = _verificationArtefacts };
        _evidenceTab = new TabItem { Header = "Engineering evidence", Content = _evidence };
        _bracketTab = new TabItem { Header = "Bracket verification", Content = _bracketVerification };

        foreach (var (tab, name) in new[]
                 {
                     (_calculationPacksTab, "Calculation packs"), (_templatesTab, "Templates"),
                     (_verificationArtefactsTab, "Verification artefacts"), (_evidenceTab, "Engineering evidence"),
                     (_bracketTab, "Bracket verification"),
                 })
            AutomationProperties.SetName(tab, name);

        _tabs.Items.Add(_calculationPacksTab);
        _tabs.Items.Add(_templatesTab);
        _tabs.Items.Add(_verificationArtefactsTab);
        _tabs.Items.Add(_evidenceTab);
        _tabs.Items.Add(_bracketTab);

        AutomationProperties.SetName(this, "Engineering Assets");
        Content = _tabs;
    }

    /// <summary>Below <see cref="DesignTokens.CompactShellWidth"/>, every master/detail tab folds the same way the Libraries tab already does.</summary>
    public void SetCompact(bool compact)
    {
        _calculationPacks.SetCompact(compact);
        _templates.SetCompact(compact);
        _verificationArtefacts.SetCompact(compact);
    }

    /// <summary>Reloads every tab. Called on entry (`TD-58`) — no tab here reloads only when a test happens to call it by hand.</summary>
    public async Task RefreshAsync()
    {
        await _calculationPacks.RefreshAsync().ConfigureAwait(true);
        await _templates.RefreshAsync().ConfigureAwait(true);
        await _verificationArtefacts.RefreshAsync().ConfigureAwait(true);
        await _evidence.RefreshAsync().ConfigureAwait(true);
        await _bracketVerification.RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Selects the tab named <paramref name="automationName"/> — what a journey test drives the area by.</summary>
    public void SelectTab(string automationName)
    {
        var item = new[] { _calculationPacksTab, _templatesTab, _verificationArtefactsTab, _evidenceTab, _bracketTab }
            .Single(t => string.Equals(AutomationProperties.GetName(t), automationName, StringComparison.Ordinal));
        _tabs.SelectedItem = item;
    }

    private void OpenOwner(string ownerKind, string recordId)
    {
        switch (ownerKind)
        {
            case "Calculation pack":
                _tabs.SelectedItem = _calculationPacksTab;
                _ = _calculationPacks.OpenAsync(recordId);
                break;
            case "Template":
                _tabs.SelectedItem = _templatesTab;
                _ = _templates.OpenAsync(recordId);
                break;
            case "Verification artefact":
                _tabs.SelectedItem = _verificationArtefactsTab;
                _ = _verificationArtefacts.OpenAsync(recordId);
                break;
        }
    }

    private async Task OnRecordedAsync()
    {
        await _calculationPacks.RefreshAsync().ConfigureAwait(true);
        await _verificationArtefacts.RefreshAsync().ConfigureAwait(true);
        await _evidence.RefreshAsync().ConfigureAwait(true);
    }
}
