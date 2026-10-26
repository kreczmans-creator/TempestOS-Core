using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.GuidedTest;

/// <summary>
/// The worksheet form as the guided test uses it: a template opened with no calculation behind it (an unsaved sheet), seeded through the form's own
/// import of an example's <c>inputs.json</c> (the same path as "Import inputs (CSV/JSON)"), and read back through the run the form last showed. It
/// calls no save, check, issue or revise: the only things it changes are what the form displays.
/// </summary>
public sealed class CalcGuidedWorksheetHost(CalcSheetView view, CalcSheetCoordinator coordinator, CalcCatalogue catalogue) : IGuidedWorksheetHost
{
    private readonly CalcSheetView _view = view ?? throw new ArgumentNullException(nameof(view));
    private readonly CalcSheetCoordinator _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    private readonly CalcCatalogue _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));

    // What this host last loaded into the form (the template and the form's inputs as they stood straight after), so that the form still holding exactly
    // that is not mistaken for the tester's own edits when the same step is opened again.
    private string? _loadedTemplate;
    private string? _loadedForm;

    /// <inheritdoc />
    public GuidedPrepareResult Prepare(string templateId, string inputsJson, bool discardUnsavedEdits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentNullException.ThrowIfNull(inputsJson);

        var entry = _catalogue.Find(templateId);

        if (entry is null)
        {
            // A calculator the installation has switched off is withheld, not missing: say which feature and where it is enabled (ADR-0173).
            return new GuidedPrepareResult(GuidedPrepareOutcome.Unavailable, _catalogue.FeatureRefusal(templateId) ?? $"The worksheet '{templateId}' is not in this build.");
        }

        // The same two-step protection as opening a saved calculation over edits: the first press says so, the second discards them.
        if (!discardUnsavedEdits && HasEditsToProtect())
            return new GuidedPrepareResult(GuidedPrepareOutcome.NeedsConfirmation, "The worksheet form has changes that are not saved. Press the button again to discard them and load this example.");

        // Select the template in the catalogue (a no-op when it is already selected), then show it as a new sheet: no calculation, defaults, unsaved.
        _view.SelectTemplate(entry.Reference);
        _view.ShowTemplate(entry);

        _loadedTemplate = _loadedForm = null;

        if (!_coordinator.ImportInputsText(inputsJson))
            return new GuidedPrepareResult(GuidedPrepareOutcome.Unavailable, $"The example's inputs were not accepted by the {entry.Template.Title} worksheet: {_view.StatusText}");

        (_loadedTemplate, _loadedForm) = (entry.Template.Id, FormText());
        return new GuidedPrepareResult(GuidedPrepareOutcome.Ready, $"{entry.Template.Title} is open beside this panel with the example's inputs loaded. It is a scratch sheet and is not saved.");
    }

    /// <summary>Unsaved edits the guided test must not discard: any the worksheet reports, except a form that still holds exactly what this host loaded into it.</summary>
    private bool HasEditsToProtect() =>
        _view.HasUnsavedEdits()
        && !(_view.CurrentInstance is null && _view.SelectedTemplate?.Template.Id == _loadedTemplate && string.Equals(FormText(), _loadedForm, StringComparison.Ordinal));

    private string FormText() => CalcInputJson.Write(_view.ReadForm());

    /// <inheritdoc />
    public CalcRunResult? ShownRun(string templateId) =>
        _view.SelectedTemplate is { } shown && string.Equals(shown.Template.Id, templateId, StringComparison.Ordinal) ? _view.LastRun : null;

    /// <inheritdoc />
    public string? ShippedTemplateVersion(string templateId) => _catalogue.Find(templateId)?.Template.Version;
}
