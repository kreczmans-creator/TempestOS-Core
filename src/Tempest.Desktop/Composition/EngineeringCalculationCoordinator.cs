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
        _view.ShowMaterials(await _workbench.ListMaterialsAsync().ConfigureAwait(true));

        var (outcome, inputs) = await _workbench.RecoverLastAsync().ConfigureAwait(true);

        if (restoreInputs && inputs is not null)
        {
            _view.RestoreInputs(inputs);
            _view.ShowMaterials(_view.Materials, inputs.MaterialRecordId);
        }

        if (outcome is null)
            return;

        _view.ShowOutcome(outcome);

        if (restoreInputs)
            _view.ShowStatus($"Recovered the calculation of {outcome.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC from its persisted record.");
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
        var outcome = await _workbench.RunAsync(_view.CurrentInputs).ConfigureAwait(true);

        _view.ShowOutcome(outcome);

        if (outcome.Performed)
        {
            _view.ShowStatus($"{outcome.OutcomeLabel}. Recorded as {outcome.CalculationRecordId}, pinned to {outcome.MaterialLibrary}/{outcome.MaterialRecordId} revision {outcome.PinnedRevision}.");
            return;
        }

        _view.ShowStatus(outcome.RefusalReason is not null
            ? "The calculation was refused. The reason is shown above."
            : "The inputs are not complete. What is wrong is shown above.");
    }
}
