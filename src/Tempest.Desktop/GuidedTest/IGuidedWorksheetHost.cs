using Tempest.Calc.Sheets;

namespace Tempest.Desktop.GuidedTest;

/// <summary>How preparing the worksheet form for a guided step went.</summary>
public enum GuidedPrepareOutcome
{
    /// <summary>The worksheet form is open on the template with the example's inputs loaded, unsaved.</summary>
    Ready,

    /// <summary>The form holds edits that are not saved; nothing was replaced. Asking again with the discard flag replaces them.</summary>
    NeedsConfirmation,

    /// <summary>The worksheet cannot be opened (the template is not in this build, or its feature is switched off, or the inputs were refused); the message says why.</summary>
    Unavailable,
}

/// <summary>The outcome of preparing the form and a one-sentence message for the tester.</summary>
public sealed record GuidedPrepareResult(GuidedPrepareOutcome Outcome, string Message);

/// <summary>
/// What the guided test needs of the worksheet form (Addendum C, C-5): open a template as a scratch sheet with an example's inputs loaded, and read
/// the outputs the form shows. It is an interface so the view model is tested without the form; the real one is <see cref="CalcGuidedWorksheetHost"/>.
/// None of it saves, checks or issues anything.
/// </summary>
public interface IGuidedWorksheetHost
{
    /// <summary>
    /// Opens <paramref name="templateId"/> in the worksheet form as an unsaved sheet and loads <paramref name="inputsJson"/> through the form's own
    /// "Import inputs (CSV/JSON)" path. A form that holds unsaved edits is left alone unless <paramref name="discardUnsavedEdits"/> is set.
    /// </summary>
    GuidedPrepareResult Prepare(string templateId, string inputsJson, bool discardUnsavedEdits);

    /// <summary>The run the form shows for <paramref name="templateId"/> now, or <see langword="null"/> when the form is not open on that template.</summary>
    CalcRunResult? ShownRun(string templateId);

    /// <summary>The version of the template this build ships, or <see langword="null"/> when it has none.</summary>
    string? ShippedTemplateVersion(string templateId);
}
