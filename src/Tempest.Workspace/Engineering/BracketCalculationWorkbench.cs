using System.Globalization;
using Tempest.Core.Calculations;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Settings;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Workspace.Engineering;

/// <summary>
/// The application-side answer behind the Engineering Calculation surface:
/// which governed materials are held and whether each may be used, what a
/// bracket section check produced, and what that result stood on.
/// </summary>
/// <remarks>
/// <para>
/// <b>This decides; the view renders.</b> Every string a user reads is
/// composed here, in the same discipline
/// <see cref="Tempest.Workspace.Projects.ProjectRequirementRegister"/> and
/// <see cref="Tempest.Workspace.Calculations.CalculationRecordReader"/>
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
    private readonly IVerificationArtefactCatalog _verifications;
    private readonly EngineeringCalculationRegister _register;

    /// <summary>Initialises a new instance of the <see cref="BracketCalculationWorkbench"/> class.</summary>
    public BracketCalculationWorkbench(
        IMaterialCatalog materials,
        ReferenceSeedService seeder,
        ReferenceReviewService review,
        GovernedBracketCheckService check,
        ICalculationEngine engine,
        ISettingsProvider settings,
        IVerificationArtefactCatalog verifications,
        EngineeringCalculationRegister register)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(seeder);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(verifications);
        ArgumentNullException.ThrowIfNull(register);

        _materials = materials;
        _seeder = seeder;
        _review = review;
        _check = check;
        _engine = engine;
        _settings = settings;
        _verifications = verifications;
        _register = register;

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
    /// Adds one material record of the user's own as Draft (`WP 17.9.2`).
    /// Nothing is released by adding: the record goes through the same
    /// verify-and-release review as a seeded one, which is why the source
    /// organisation and document are required here — a record that names
    /// no source can never be released.
    /// </summary>
    /// <exception cref="ArgumentException">A required field is blank, or a number does not parse or is not positive.</exception>
    /// <exception cref="DuplicateReferenceRecordException">A record with the same designation already exists.</exception>
    public async Task<BracketMaterialOption> AddMaterialAsync(NewMaterialRecord material, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentException.ThrowIfNullOrWhiteSpace(material.Name, "Name");
        ArgumentException.ThrowIfNullOrWhiteSpace(material.Designation, "Designation");
        ArgumentException.ThrowIfNullOrWhiteSpace(material.SourceOrganisation, "Source organisation");
        ArgumentException.ThrowIfNullOrWhiteSpace(material.SourceDocument, "Source document");

        var yield = ParsePositive(material.YieldStrengthMegapascals, "Yield strength (MPa)");
        var density = ParsePositive(material.DensityGramsPerCubicCentimetre, "Density (g/cm3)");

        var recordId = "mat-" + new string(material.Designation.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

        var definition = new MaterialDefinition
        {
            Name = material.Name.Trim(),
            Family = material.Family,
            Designation = material.Designation.Trim(),
            Properties = new Dictionary<string, ReferenceQuantityValue>
            {
                [MaterialPropertyNames.YieldStrength] = new(
                    new Quantity<Pressure>(yield, PressureUnits.Megapascal),
                    ReferenceValueOrigin.EngineeringReference,
                    "Entered by the engineer from the source named in the record's provenance.",
                    "Yield strength as entered"),
                [MaterialPropertyNames.Density] = new(
                    new Quantity<MassDensity>(density, MassDensityUnits.GramPerCubicCentimetre),
                    ReferenceValueOrigin.EngineeringReference,
                    "Entered by the engineer from the source named in the record's provenance."),
            },
        };

        var provenance = new ReferenceProvenance(
            SourceOrganisation: material.SourceOrganisation.Trim(),
            SourceDocument: material.SourceDocument.Trim(),
            ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
            Notes: "Added by hand in the Engineering Calculations workspace; not verified until reviewed.");

        var record = await _materials.RegisterAsync(recordId, definition, provenance, cancellationToken).ConfigureAwait(false);

        return Describe(record);
    }

    private static double ParsePositive(string? text, string field)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentException($"{field} must be a number.", field);

        if (value <= 0)
            throw new ArgumentException($"{field} must be greater than zero.", field);

        return value;
    }

    /// <summary>
    /// Populates the material library from the shipped seed corpus, and
    /// reports how many records that added.
    /// </summary>
    /// <remarks>
    /// A manual top-up, kept working alongside the same seeding the host
    /// now performs automatically on a fresh launch (`WP 18.0B-R1`,
    /// `TD-163`): <see cref="Tempest.Workspace.Composition.EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync"/>
    /// already populates the Materials library once, at start, if it was
    /// still holding no record at all. This button therefore ordinarily
    /// has nothing left to add — pressing it reports the library already
    /// held every shipped record rather than duplicating anything — and it
    /// still matters for the case the automatic pass deliberately leaves
    /// alone: a library that already held a record of its own (a sample
    /// module's, or one a person registered by hand) before the shipped
    /// corpus ever got a chance to seed it. Every record this adds still
    /// lands <see cref="ReferenceValidationState.Draft"/>, and
    /// <see cref="ReferenceSeedService.ApplyAsync{TDefinition}"/> is still
    /// additive and idempotent per record, so pressing it twice, or after
    /// the automatic pass, is harmless and can never overwrite a value
    /// somebody has since corrected.
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

    /// <summary>
    /// Every calculation this engine has recorded, newest first, as the
    /// surface should list them.
    /// </summary>
    /// <remarks>
    /// A bracket record is opened so the list can show its outcome and the
    /// reference it stood on; a record of any other calculation is listed
    /// from its summary alone, because this workbench knows no other result
    /// type and will not guess at one.
    /// </remarks>
    public async Task<IReadOnlyList<CalculationListEntry>> ListCalculationsAsync(
        bool includeRetired = false, CancellationToken cancellationToken = default)
    {
        var summaries = await _engine.ListRecordsAsync(cancellationToken).ConfigureAwait(false);
        var entries = new List<CalculationListEntry>(summaries.Count);
        var named = await _register.ListAsync(cancellationToken).ConfigureAwait(false);

        // One name per record. A record named twice would be a defect
        // upstream of this listing, so the first — the newest, since the
        // register orders newest first — is the one shown, rather than
        // throwing here and leaving the engineer with no list at all.
        var namesByRecord = new Dictionary<Guid, NamedCalculation>();

        foreach (var name in named)
        {
            if (name.RecordId is { } recordId)
                namesByRecord.TryAdd(recordId, name);
        }

        foreach (var summary in summaries)
        {
            namesByRecord.TryGetValue(summary.Id, out var name);

            if (name is { IsRetired: true } && !includeRetired)
                continue;

            var isBracket = string.Equals(summary.CalculationId, BracketSectionCheckCalculationDefinition.Id, StringComparison.Ordinal);
            var catalogue = EngineeringCalculationCatalogue.For(summary.CalculationId);
            BracketSectionCheckResult? result = null;

            if (isBracket)
            {
                var record = await _engine
                    .FindRecordAsync<BracketSectionCheckResult>(summary.Id, cancellationToken)
                    .ConfigureAwait(false);
                result = record?.Result;
            }

            entries.Add(new CalculationListEntry(
                RecordId: summary.Id,
                Title: name?.DisplayName ?? $"{catalogue?.Name ?? summary.CalculationId} {summary.Id.ToString("N")[..8]}",
                CalculationId: summary.CalculationId,
                CalculationName: catalogue?.Name ?? summary.CalculationId,
                RevisionNumber: summary.RevisionNumber,
                ExecutedAt: summary.ExecutedAt,
                ExecutedByPrincipalId: summary.ExecutedByPrincipalId,
                MaterialRecordId: result?.MaterialPin.RecordId,
                PinnedRevision: result?.MaterialPin.RevisionNumber,
                Outcome: result is null
                    ? "Recorded"
                    : result.Outcome == BracketCheckOutcome.MeetsCriteria ? "Meets criteria" : "Does not meet criteria",
                MeetsCriteria: result is null ? null : result.Outcome == BracketCheckOutcome.MeetsCriteria,
                ResultSummary: result is null
                    ? "Open it to read the record."
                    : $"{Format(result.AppliedStress.ConvertTo(PressureUnits.Megapascal).Value, "MPa")} of {Format(result.AllowableStress.ConvertTo(PressureUnits.Megapascal).Value, "MPa")}, margin {result.StressMargin.ToString("0.####", CultureInfo.InvariantCulture)}",
                CanBeOpenedHere: isBracket,
                ObjectId: name?.ObjectId,
                Status: name?.Status,
                ProjectLabel: name?.ProjectLabel));
        }

        return entries;
    }

    /// <summary>
    /// Opens one persisted calculation, read-only, exactly as it was
    /// recorded.
    /// </summary>
    /// <remarks>
    /// <b>Nothing is recalculated and nothing is written.</b> The result
    /// comes out of the immutable record; only the "reference now" fields
    /// are read live, so the panel can say the reference has moved on
    /// without the result moving with it.
    /// </remarks>
    /// <param name="recordId">The record to open.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome, or <see langword="null"/> where no such bracket record is held.</returns>
    public async Task<BracketCalculationOutcome?> OpenAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        var record = await _engine
            .FindRecordAsync<BracketSectionCheckResult>(recordId, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await DescribeAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The verification evidence held for a calculation, or an honest
    /// account of why none is.
    /// </summary>
    /// <remarks>
    /// This reads the governed verification artefact catalogue. It never
    /// creates one: an artefact whose required requirement does not exist is
    /// reported as absent, with the reason, rather than manufactured
    /// (`TD-165`).
    /// </remarks>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task<VerificationEvidence> ReadVerificationAsync(CancellationToken cancellationToken = default)
    {
        var artefact = await _verifications
            .FindAsync(EngineeringAssetSeed.VerificationRecordId, cancellationToken)
            .ConfigureAwait(false);

        if (artefact is null)
        {
            return new VerificationEvidence(
                Exists: false,
                ArtefactRecordId: EngineeringAssetSeed.VerificationRecordId,
                Reference: null,
                Standing: null,
                Summary: null,
                PerformedByPrincipalId: null,
                PerformedOn: null,
                WhyAbsent: "No verification artefact is held. The shipped one is built only when a real requirement exists to verify against — "
                    + "the model refuses an artefact that names no requirement, and the seed respects that refusal rather than minting an "
                    + "identity to get past it. Recorded as TD-165; nothing here fabricates one.");
        }

        return new VerificationEvidence(
            Exists: true,
            ArtefactRecordId: artefact.Id,
            Reference: artefact.Definition.Reference,
            Standing: artefact.Definition.Result?.Standing.ToString() ?? "Not performed",
            Summary: artefact.Definition.Result?.Summary,
            PerformedByPrincipalId: artefact.Definition.Result?.PerformedByPrincipalId,
            PerformedOn: artefact.Definition.Result?.PerformedOn,
            WhyAbsent: null);
    }

    /// <summary>Runs the bracket section check, names and remembers it, and describes the outcome.</summary>
    /// <remarks>
    /// <para>
    /// <b>Naming happens only after the calculation has actually run.</b> A
    /// governed <c>Calculation</c> object is created for a record that
    /// exists, never for a refused or rejected attempt — a name for
    /// something that was never calculated would be an entry in the
    /// workspace with no evidence behind it.
    /// </para>
    /// <para>
    /// <b>A blank name is not a refusal.</b> The engineer is asked for one
    /// and gets a serviceable default if they do not give one, because the
    /// alternative — refusing to record a calculation that has already been
    /// performed because a text box is empty — would lose real work over a
    /// label. The default names the calculation and when it ran, and can be
    /// renamed afterwards like any other.
    /// </para>
    /// </remarks>
    /// <param name="inputs">What the engineer entered.</param>
    /// <param name="displayName">What they want it called. Blank takes the default described above.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task<BracketCalculationOutcome> RunAsync(
        BracketCalculationInputs inputs, string? displayName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (!inputs.TryBuildRequest(out var request, out var problems))
            return BracketCalculationOutcome.Rejected(problems);

        var check = await _check.CheckAsync(request!, cancellationToken).ConfigureAwait(false);

        if (!check.WasPerformed)
            return BracketCalculationOutcome.Refused(check.Reason ?? "The check was refused.");

        await RememberAsync(check.Record!.Id, inputs, cancellationToken).ConfigureAwait(false);

        string? namingProblem = null;

        try
        {
            await _register
                .NameAsync(check.Record!.Id, DefaultedName(displayName, check.Record!.ExecutedAt), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (EngineeringCalculationNamingException partial)
        {
            // The object was created and named but not linked. Its own
            // message says exactly that, and says it verbatim rather than
            // paraphrased — the two failures below leave the product in
            // different states and an engineer needs to know which.
            namingProblem = partial.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The calculation itself is already recorded and durable.
            // Reported, never swallowed and never allowed to present as a
            // failed calculation — see NamingProblem's own remarks.
            namingProblem =
                $"The calculation ran and is recorded, but it could not be named: {ex.Message} "
                + "It is listed under its own identity and can still be opened.";
        }

        var described = await DescribeAsync(check.Record!, cancellationToken).ConfigureAwait(false);

        return namingProblem is null ? described : described with { NamingProblem = namingProblem };
    }

    /// <summary>The named calculation carrying <paramref name="recordId"/>, or <see langword="null"/> where nobody has named that record.</summary>
    /// <param name="recordId">The calculation record to look up.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public Task<NamedCalculation?> FindNamedAsync(Guid recordId, CancellationToken cancellationToken = default) =>
        _register.FindByRecordAsync(recordId, cancellationToken);

    /// <summary>Changes what a named calculation is called, and nothing else.</summary>
    /// <param name="calculationObjectId">The named calculation to rename.</param>
    /// <param name="newDisplayName">Its new display name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public Task<CalculationRegisterOutcome> RenameAsync(
        Guid calculationObjectId, string? newDisplayName, CancellationToken cancellationToken = default) =>
        _register.RenameAsync(calculationObjectId, newDisplayName, cancellationToken);

    /// <summary>Takes a named calculation out of the active list without deleting anything.</summary>
    /// <param name="calculationObjectId">The named calculation to retire.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public Task<CalculationRegisterOutcome> RetireAsync(
        Guid calculationObjectId, CancellationToken cancellationToken = default) =>
        _register.RetireAsync(calculationObjectId, cancellationToken);

    /// <summary>
    /// What retiring one named calculation would actually do, in words, so
    /// a surface can say it before doing it rather than afterwards.
    /// </summary>
    /// <param name="calculationObjectId">The named calculation in question.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public Task<CalculationRegisterOutcome> DescribeRetirementAsync(
        Guid calculationObjectId, CancellationToken cancellationToken = default) =>
        _register.DescribeRetirementAsync(calculationObjectId, cancellationToken);

    private static string DefaultedName(string? displayName, DateTimeOffset executedAt)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName.Trim();

        var name = EngineeringCalculationCatalogue.For(BracketSectionCheckCalculationDefinition.Id)?.Name
            ?? BracketSectionCheckCalculationDefinition.Id;

        // Invariant, like every other formatted value this workbench hands
        // out: a default name whose time separator follows the machine's
        // culture would not match the timestamp shown beside it.
        return $"{name} {executedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC";
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
