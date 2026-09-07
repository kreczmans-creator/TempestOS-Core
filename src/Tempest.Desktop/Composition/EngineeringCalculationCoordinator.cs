using Tempest.App.Engineering;
using Tempest.Core.ReferenceData.Review;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Performs what the Engineering Calculation surface asks for: populate the
/// material library, verify and release a material, run the check, and
/// recover the last one on relaunch.
/// </summary>
/// <remarks>
/// <para>
/// A collaborator under `ADR-0103`: constructed once by <c>MainWindow</c>,
/// declaring only what it needs, never DI-registered, never referencing
/// <c>MainWindow</c> or a sibling back — the same shape
/// <see cref="ProjectDeliveryCoordinator"/> and
/// <see cref="ProjectGovernanceCoordinator"/> already use.
/// </para>
/// <para>
/// <b>It holds no engineering rule and no lifecycle rule.</b> Every call
/// below forwards to <see cref="BracketCalculationWorkbench"/> and renders
/// what comes back. Its only judgement is which sentence to put on the
/// status line.
/// </para>
/// </remarks>
internal sealed class EngineeringCalculationCoordinator
{
    private readonly BracketCalculationWorkbench _workbench;
    private readonly EngineeringCalculationView _view;

    /// <summary>
    /// Whether retired calculations are being listed alongside the active
    /// ones. Held here rather than read from the view at each call so that
    /// every refresh — a calculation, a release, a rename — honours the
    /// engineer's own choice instead of quietly reverting it.
    /// </summary>
    private bool _includeRetired;

    /// <summary>Initialises a new instance of the <see cref="EngineeringCalculationCoordinator"/> class.</summary>
    /// <param name="workbench">The application-side answer behind the surface.</param>
    /// <param name="view">The surface itself.</param>
    public EngineeringCalculationCoordinator(BracketCalculationWorkbench workbench, EngineeringCalculationView view)
    {
        ArgumentNullException.ThrowIfNull(workbench);
        ArgumentNullException.ThrowIfNull(view);

        _workbench = workbench;
        _view = view;
    }

    /// <summary>
    /// Loads the material library into the surface and re-reads the stored
    /// calculation from its own record.
    /// </summary>
    /// <remarks>
    /// <b>The stored calculation is re-read on every entry, not only the
    /// first.</b> The result itself cannot change — it lives in an immutable
    /// record pinned to the reference revision it stood on — but what the
    /// library holds <i>now</i> can, and the traceability panel reports
    /// both. Describing it once and leaving it would mean a surface that
    /// still called a reference current after somebody superseded it, which
    /// is precisely the claim this panel exists to prevent.
    /// </remarks>
    /// <param name="restoreInputs">Whether to put the remembered figures back into the boxes — done on the first entry of a session, not on every return, so a half-typed input is not discarded by navigating away and back.</param>
    public async Task RefreshAsync(bool restoreInputs = false)
    {
        _view.ShowCatalogue(EngineeringCalculationCatalogue.All());
        _view.ShowMaterials(await _workbench.ListMaterialsAsync().ConfigureAwait(true));
        _view.ShowCalculations(await _workbench.ListCalculationsAsync(_includeRetired).ConfigureAwait(true));
        _view.ShowVerification(await _workbench.ReadVerificationAsync().ConfigureAwait(true));

        var (outcome, inputs) = await _workbench.RecoverLastAsync().ConfigureAwait(true);

        if (restoreInputs && inputs is not null)
        {
            _view.RestoreInputs(inputs);
            _view.ShowMaterials(_view.Materials, inputs.MaterialRecordId);
        }

        if (outcome is null)
            return;

        // Recovered from a record, so it is a recorded calculation being
        // viewed, not one being entered.
        _view.ShowOutcome(outcome, readOnly: true);

        if (restoreInputs)
            _view.ShowStatus($"Recovered the calculation of {outcome.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC from its persisted record. It is read-only.");
    }

    /// <summary>Starts a new, editable calculation.</summary>
    public void BeginNewCalculation() => _view.BeginNewCalculation();

    /// <summary>Opens one persisted calculation, read-only, exactly as recorded.</summary>
    /// <param name="recordId">The record to open.</param>
    public async Task OpenAsync(Guid recordId)
    {
        var outcome = await _workbench.OpenAsync(recordId).ConfigureAwait(true);

        if (outcome is null)
        {
            _view.ShowStatus("That calculation is recorded, but this workspace cannot display its result type yet.");
            return;
        }

        _view.ShowOutcome(outcome, readOnly: true);
        _view.ShowVerification(await _workbench.ReadVerificationAsync().ConfigureAwait(true));
        _view.ShowStatus($"Opened the calculation of {outcome.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC, read-only. Nothing was recalculated.");
    }

    /// <summary>Populates the material library from the shipped seed corpus.</summary>
    public async Task PopulateAsync()
    {
        var added = await _workbench.PopulateMaterialLibraryAsync().ConfigureAwait(true);

        await RefreshAsync().ConfigureAwait(true);
        _view.ShowStatus(added == 0
            ? "The library already held every record in the shipped corpus. Nothing was overwritten."
            : $"Added {added} reference records, all Draft. Nothing is released, and nothing is approved, by populating.");
    }

    /// <summary>Verifies and releases the selected material, in the engineer's own words.</summary>
    public async Task VerifyAndReleaseAsync()
    {
        var selected = _view.SelectedMaterial;

        if (selected is null)
        {
            _view.ShowStatus("Select a material first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_view.SourceConsulted))
        {
            _view.ShowStatus("Say what you checked this record against. A verification that names no source is not a verification.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_view.ReleaseRationale))
        {
            _view.ShowStatus("Say why it is being released. A release with no stated reason is not reviewable.");
            return;
        }

        try
        {
            var released = await _workbench
                .VerifyAndReleaseAsync(selected.RecordId, _view.SourceConsulted, _view.ReleaseRationale)
                .ConfigureAwait(true);

            await RefreshAsync().ConfigureAwait(true);
            _view.ShowMaterials(_view.Materials, released.RecordId);
            _view.ShowStatus($"{released.Designation} is Released, verified by {released.ReviewerPrincipalId} on {released.VerificationDate:yyyy-MM-dd}.");
        }
        catch (ReferenceReviewException refusal)
        {
            // The platform's own refusal, verbatim. Nobody signed in, or
            // already verified, or provenance naming no source — each is a
            // governance answer the engineer should read, not an error to
            // paraphrase.
            _view.ShowStatus(refusal.Message);
        }
    }

    /// <summary>Runs the bracket section check against what the engineer entered.</summary>
    public async Task CalculateAsync()
    {
        var outcome = await _workbench.RunAsync(_view.CurrentInputs, _view.CalculationName).ConfigureAwait(true);

        _view.ShowOutcome(outcome);

        if (outcome.Performed)
        {
            // The list is re-read so the calculation just recorded appears
            // where the engineer will look for it next.
            _view.ShowCalculations(await _workbench.ListCalculationsAsync(_includeRetired).ConfigureAwait(true));
            _view.ShowVerification(await _workbench.ReadVerificationAsync().ConfigureAwait(true));
            var recorded = $"{outcome.OutcomeLabel}. Recorded as {outcome.CalculationRecordId}, pinned to {outcome.MaterialLibrary}/{outcome.MaterialRecordId} revision {outcome.PinnedRevision}.";

            _view.ShowStatus(outcome.NamingProblem is null ? recorded : $"{recorded} {outcome.NamingProblem}");
            return;
        }

        _view.ShowStatus(outcome.RefusalReason is not null
            ? "The calculation was refused. The reason is shown above."
            : "The inputs are not complete. What is wrong is shown above.");
    }

    /// <summary>Changes what one calculation is called, and nothing else.</summary>
    /// <param name="calculationObjectId">The governed calculation object to rename.</param>
    /// <param name="newName">Its new display name.</param>
    public async Task RenameAsync(Guid calculationObjectId, string newName)
    {
        var outcome = await _workbench.RenameAsync(calculationObjectId, newName).ConfigureAwait(true);

        if (outcome.Succeeded)
            _view.ShowCalculations(await _workbench.ListCalculationsAsync(_includeRetired).ConfigureAwait(true));

        _view.ShowStatus(outcome.Message);
    }

    /// <summary>
    /// Takes one calculation out of the active list. Nothing is deleted —
    /// see <c>EngineeringCalculationRegister</c>'s own remarks and
    /// <c>TD-169</c> for why this is a lifecycle transition and not a
    /// delete.
    /// </summary>
    /// <param name="calculationObjectId">The governed calculation object to retire.</param>
    public async Task RetireAsync(Guid calculationObjectId)
    {
        var outcome = await _workbench.RetireAsync(calculationObjectId).ConfigureAwait(true);

        if (outcome.Succeeded)
            _view.ShowCalculations(await _workbench.ListCalculationsAsync(_includeRetired).ConfigureAwait(true));

        _view.ShowStatus(outcome.Message);
    }

    /// <summary>Shows, or stops showing, retired calculations alongside the active ones.</summary>
    /// <param name="includeRetired">Whether retired calculations should be listed.</param>
    public async Task SetShowRetiredAsync(bool includeRetired)
    {
        _includeRetired = includeRetired;

        _view.ShowCalculations(await _workbench.ListCalculationsAsync(_includeRetired).ConfigureAwait(true));
        _view.ShowStatus(includeRetired
            ? "Showing retired calculations as well as active ones. A retired calculation is still held and can still be opened."
            : "Showing active calculations only. Retired ones are still held — tick the box to see them.");
    }
}
