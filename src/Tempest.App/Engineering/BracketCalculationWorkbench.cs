using System.Globalization;
using Tempest.Core.Calculations;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Settings;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.App.Engineering;

/// <summary>
/// The application-side answer behind the Engineering Calculation surface:
/// which governed materials are held and whether each may be used, what a
/// bracket section check produced, and what that result stood on.
/// </summary>
/// <remarks>
/// <para>
/// <b>This decides; the view renders.</b> Every string a user reads is
/// composed here, in the same discipline
/// <see cref="Tempest.App.Projects.ProjectRequirementRegister"/> and
/// <see cref="Tempest.App.Workspace.Calculations.CalculationRecordReader"/>
/// already follow — a unit-bearing value reaches the Desktop already
/// formatted, because reconstructing a unit in a view is how two surfaces
/// come to disagree.
/// </para>
/// <para>
/// <b>It computes nothing.</b> The arithmetic is
/// <see cref="BracketSectionCheckCalculationDefinition"/>'s, reached only
/// through <see cref="GovernedBracketCheckService"/>; the lifecycle is
/// <see cref="ReferenceReviewService"/>'s; the population is
/// <see cref="ReferenceSeedService"/>'s. This type holds no rule of its
/// own and duplicates none of theirs.
/// </para>
/// <para>
/// <b>Nothing here can manufacture an approval.</b> Releasing a material
/// goes through <see cref="ReferenceReviewService"/>, which takes the
/// reviewer from the signed-in principal and the date from its own clock,
/// and refuses outright when nobody is signed in. The statement and the
/// rationale are the words a person actually typed, passed through
/// unaltered.
/// </para>
/// </remarks>
public sealed class BracketCalculationWorkbench
{
    /// <summary>The setting the last calculation is remembered under, so a relaunch can recover it.</summary>
    public const string LastCalculationSettingKey = "Engineering.BracketCalculation.Last";

    private readonly IMaterialCatalog _materials;
    private readonly ReferenceSeedService _seeder;
    private readonly ReferenceReviewService _review;
    private readonly GovernedBracketCheckService _check;
    private readonly ICalculationEngine _engine;
    private readonly ISettingsProvider _settings;

    /// <summary>Initialises a new instance of the <see cref="BracketCalculationWorkbench"/> class.</summary>
    public BracketCalculationWorkbench(
        IMaterialCatalog materials,
        ReferenceSeedService seeder,
        ReferenceReviewService review,
        GovernedBracketCheckService check,
        ICalculationEngine engine,
        ISettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(seeder);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(settings);

        _materials = materials;
        _seeder = seeder;
        _review = review;
        _check = check;
        _engine = engine;
        _settings = settings;

        _settings.RegisterDefinition(new SettingDefinition(
            LastCalculationSettingKey, "Engineering Calculation — last bracket check", string.Empty));
    }

    /// <summary>The material library, as the surface should present it.</summary>
    public async Task<IReadOnlyList<BracketMaterialOption>> ListMaterialsAsync(CancellationToken cancellationToken = default)
    {
        var records = await _materials.ListAsync(cancellationToken).ConfigureAwait(false);

        return records
            .OrderBy(r => r.Definition.Designation ?? r.Definition.Name, StringComparer.Ordinal)
            .Select(Describe)
            .ToList();
    }

    /// <summary>
    /// Populates the material library from the shipped seed corpus, and
    /// reports how many records that added.
    /// </summary>
    /// <remarks>
    /// <b>Deliberately a user action, not a start-up side effect.</b>
    /// <see cref="ReferenceSeedService"/>'s own registration in the host
    /// says why: "deciding when a library gets populated is a governance
    /// choice and not a side effect of booting." Every record lands
    /// <see cref="ReferenceValidationState.Draft"/>, and the seeding is
    /// additive and idempotent, so pressing it twice is harmless and
    /// pressing it can never overwrite a value somebody has since
    /// corrected.
    /// </remarks>
    public async Task<int> PopulateMaterialLibraryAsync(CancellationToken cancellationToken = default)
    {
        var outcome = await _seeder.ApplyAsync(_materials, MaterialSeed.Instance, cancellationToken).ConfigureAwait(false);

        return outcome.RegisteredCount;
    }

    /// <summary>
    /// Verifies a material against the source the reviewer says they
    /// consulted, then releases it — the two governed acts, in the only
    /// order the lifecycle permits.
    /// </summary>
    /// <param name="recordId">The material to review.</param>
    /// <param name="sourceConsulted">What the reviewer actually checked the record against, in their own words.</param>
    /// <param name="releaseRationale">Why it is being released, in their own words.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The released record, described as the surface should present it.</returns>
    /// <exception cref="ReferenceReviewException">Nobody is signed in, the record is already verified, or its provenance names no source.</exception>
    public async Task<BracketMaterialOption> VerifyAndReleaseAsync(
        string recordId, string sourceConsulted, string releaseRationale, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceConsulted);
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseRationale);

        await _review.VerifyAsync(
            _materials, recordId, new ReferenceReviewStatement(sourceConsulted), cancellationToken).ConfigureAwait(false);

        var released = await _review.ReleaseAsync(_materials, recordId, releaseRationale, cancellationToken).ConfigureAwait(false);

        return Describe(released);
    }

    /// <summary>Runs the bracket section check, remembers it, and describes the outcome.</summary>
    public async Task<BracketCalculationOutcome> RunAsync(
        BracketCalculationInputs inputs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (!inputs.TryBuildRequest(out var request, out var problems))
            return BracketCalculationOutcome.Rejected(problems);

        var check = await _check.CheckAsync(request!, cancellationToken).ConfigureAwait(false);

        if (!check.WasPerformed)
            return BracketCalculationOutcome.Refused(check.Reason ?? "The check was refused.");

        await RememberAsync(check.Record!.Id, inputs, cancellationToken).ConfigureAwait(false);

        return await DescribeAsync(check.Record!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Recovers the last calculation this workbench ran, reading the result
    /// back out of its own persisted record.
    /// </summary>
    /// <remarks>
    /// The <em>result</em> comes from the durable
    /// <see cref="CalculationRecord{TResult}"/>, which is why it survives a
    /// relaunch unchanged even after the material it stood on moves on —
    /// the record holds the revision it was pinned to, not a live lookup.
    /// The input figures are echoed back from the remembered entry purely
    /// so the boxes read as the engineer left them; they are a convenience,
    /// and the record is the authority.
    /// </remarks>
    public async Task<(BracketCalculationOutcome? Outcome, BracketCalculationInputs? Inputs)> RecoverLastAsync(
        CancellationToken cancellationToken = default)
    {
        var stored = await _settings.GetValueAsync(LastCalculationSettingKey, cancellationToken).ConfigureAwait(false);

        if (!RememberedCalculation.TryParse(stored, out var remembered))
            return (null, null);

        var record = await _engine
            .FindRecordAsync<BracketSectionCheckResult>(remembered!.RecordId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
            return (null, remembered.Inputs);

        return (await DescribeAsync(record, cancellationToken).ConfigureAwait(false), remembered.Inputs);
    }

    private async Task RememberAsync(Guid recordId, BracketCalculationInputs inputs, CancellationToken cancellationToken) =>
        await _settings
            .SetValueAsync(LastCalculationSettingKey, new RememberedCalculation(recordId, inputs).ToStorage(), cancellationToken)
            .ConfigureAwait(false);

    private async Task<BracketCalculationOutcome> DescribeAsync(
        CalculationRecord<BracketSectionCheckResult> record, CancellationToken cancellationToken)
    {
        var result = record.Result;
        var pin = result.MaterialPin;
        var current = await _materials.FindAsync(pin.RecordId, cancellationToken).ConfigureAwait(false);

        return new BracketCalculationOutcome(
            Performed: true,
            Problems: [],
            RefusalReason: null,
            CalculationRecordId: record.Id,
            CalculationId: record.CalculationId,
            CalculationRevision: record.RevisionNumber,
            ExecutedAt: record.ExecutedAt,
            ExecutedByPrincipalId: record.ExecutedByPrincipalId,
            AppliedStress: Format(result.AppliedStress.ConvertTo(PressureUnits.Megapascal).Value, "MPa"),
            AllowableStress: Format(result.AllowableStress.ConvertTo(PressureUnits.Megapascal).Value, "MPa"),
            Density: Format(result.Density.ConvertTo(MassDensityUnits.KilogramPerCubicMetre).Value, "kg/m³"),
            StressMargin: result.StressMargin.ToString("0.####", CultureInfo.InvariantCulture),
            EstimatedMass: Format(result.EstimatedMass.ConvertTo(MassUnits.Gram).Value, "g"),
            MassLimit: Format(result.MassLimit.ConvertTo(MassUnits.Gram).Value, "g"),
            StressCriterionMet: result.StressCriterionMet,
            MassCriterionMet: result.MassCriterionMet,
            MeetsCriteria: result.Outcome == BracketCheckOutcome.MeetsCriteria,
            OutcomeLabel: result.Outcome == BracketCheckOutcome.MeetsCriteria ? "Meets criteria" : "Does not meet criteria",
            MaterialRecordId: pin.RecordId,
            MaterialLibrary: pin.Library,
            PinnedRevision: pin.RevisionNumber,
            CurrentRevision: current?.RevisionNumber,
            MaterialHasMovedOn: current is not null && current.RevisionNumber != pin.RevisionNumber,
            MaterialStateNow: current?.ValidationState.ToString() ?? "no longer held",
            Provenance: current is null ? "The material record is no longer held in the library." : DescribeProvenance(current.Provenance),
            ReviewerPrincipalId: current?.Provenance.ReviewerPrincipalId,
            VerificationDate: current?.Provenance.VerificationDate,
            Assumptions: [.. record.Assumptions.Select(a => a.Description)]);
    }

    private static BracketMaterialOption Describe(IReferenceRecord<MaterialDefinition> record) =>
        new(
            RecordId: record.Id,
            // A material with no designation is legitimate — the field is
            // optional on the definition — so fall back to the name rather
            // than showing a picker entry with an empty first column.
            Designation: record.Definition.Designation ?? record.Definition.Name,
            Name: record.Definition.Name,
            ValidationState: record.ValidationState,
            RevisionNumber: record.RevisionNumber,
            IsUsableForEngineering: record.ValidationState == ReferenceValidationState.Released,
            StateExplanation: record.ValidationState == ReferenceValidationState.Released
                ? "Released — verified against its source and available for engineering work."
                : $"{record.ValidationState} — engineering work may not rely on reference data nobody has verified against its source.",
            Provenance: DescribeProvenance(record.Provenance),
            ReviewerPrincipalId: record.Provenance.ReviewerPrincipalId,
            VerificationDate: record.Provenance.VerificationDate);

    private static string DescribeProvenance(ReferenceProvenance provenance)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(provenance.SourceOrganisation))
            parts.Add(provenance.SourceOrganisation);

        if (!string.IsNullOrWhiteSpace(provenance.SourceDocument))
            parts.Add(provenance.SourceDocument);

        if (!string.IsNullOrWhiteSpace(provenance.SourceLocation))
            parts.Add(provenance.SourceLocation);

        parts.Add(provenance.IsVerified
            ? $"verified by {provenance.ReviewerPrincipalId} on {provenance.VerificationDate:yyyy-MM-dd}"
            : "not verified against its source by anyone");

        return string.Join(" — ", parts);
    }

    private static string Format(double value, string unit) =>
        $"{value.ToString("0.####", CultureInfo.InvariantCulture)} {unit}";
}
