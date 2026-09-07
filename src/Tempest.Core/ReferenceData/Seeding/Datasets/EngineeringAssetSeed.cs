using Tempest.Core.EngineeringAssets;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.DesignReviews;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.EngineeringAssets.TechnicalDocumentation;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.Materials;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// One of each kind of engineering asset, built around the material data
/// this corpus actually holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Five assets, not a corpus.</b> The point is to prove the asset
/// structures can carry real engineering work that reaches back into the
/// reference libraries — not to stock a document management system. One
/// template, one calculation pack, one verification artefact, one design
/// review and one technical document, all about the same bracket, so that
/// the links between them are real links and not five unrelated examples.
/// </para>
/// <para>
/// <b>Every one of these is AUTHORED, and every one is unfinished on
/// purpose.</b> Tempest Design Engineering wrote them; no external source
/// says any of it. The calculation pack has inputs pinned to real material
/// revisions but records no execution, the verification artefact is
/// <see cref="VerificationStanding.NotPerformed"/>, and the design review
/// is <see cref="ReviewOutcome.NotConcluded"/> with no participants — all
/// because none of those things has happened. Filling them in with
/// plausible results would turn a demonstration of structure into a
/// fabricated engineering record, which is the one thing an engineering
/// platform must never contain.
/// </para>
/// <para>
/// <b>The pins are resolved, not hardcoded.</b> A calculation that says it
/// used 6082-T6 has to say <em>which revision</em> of the 6082-T6 record,
/// or the reproducibility claim is empty. That number is a fact about the
/// catalogue at seeding time, so this dataset is built from the registered
/// records rather than written as a literal — see
/// <see cref="CreateAsync"/>.
/// </para>
/// </remarks>
public sealed class EngineeringAssetSeed
{
    /// <summary>The identity of the calculation record sheet template.</summary>
    public const string TemplateRecordId = "tpl-calculation-record-sheet";

    /// <summary>The identity of the bracket calculation pack.</summary>
    public const string CalculationPackRecordId = "cpk-bracket-stress-check";

    /// <summary>The identity of the bracket verification artefact.</summary>
    public const string VerificationRecordId = "ver-bracket-yield-margin";

    /// <summary>The identity of the bracket design review pack.</summary>
    public const string DesignReviewRecordId = "drp-bracket-preliminary";

    /// <summary>The identity of the bracket material selection note.</summary>
    public const string TechnicalDocumentRecordId = "doc-bracket-material-selection";

    private const string TemplateReference = "TDE-TPL-CALC-001";

    private const string CalculationPackReference = "TDE-CPK-001";

    private const string VerificationReference = "TDE-VER-001";

    private const string Subject = "Mounting bracket, machined from bar";

    /// <summary>
    /// The template dataset, which stands alone because everything else
    /// pins it: a calculation pack cites the revision of the template it
    /// was recorded on, so the template must already be registered before
    /// the rest of the assets can be built.
    /// </summary>
    public static IReferenceSeed<EngineeringTemplate> Templates { get; } =
        new Seed<EngineeringTemplate>("Calculation record sheet template", [BuildTemplate()]);

    private EngineeringAssetSeed(
        IReferenceSeed<CalculationPack> calculationPacks,
        IReferenceSeed<VerificationArtefact> verificationArtefacts,
        IReferenceSeed<DesignReviewPack> designReviews,
        IReferenceSeed<TechnicalDocument> technicalDocuments)
    {
        CalculationPacks = calculationPacks;
        VerificationArtefacts = verificationArtefacts;
        DesignReviews = designReviews;
        TechnicalDocuments = technicalDocuments;
    }

    /// <summary>The calculation pack dataset.</summary>
    public IReferenceSeed<CalculationPack> CalculationPacks { get; }

    /// <summary>The verification artefact dataset.</summary>
    public IReferenceSeed<VerificationArtefact> VerificationArtefacts { get; }

    /// <summary>The design review dataset.</summary>
    public IReferenceSeed<DesignReviewPack> DesignReviews { get; }

    /// <summary>The technical document dataset.</summary>
    public IReferenceSeed<TechnicalDocument> TechnicalDocuments { get; }

    /// <summary>
    /// Builds the asset datasets against the material records currently in
    /// <paramref name="materials"/>, pinning each cited material at the
    /// revision it is actually at.
    /// </summary>
    /// <param name="materials">The material library the assets cite. Must already be seeded.</param>
    /// <param name="templates">The template library. Must already have had <see cref="Templates"/> applied to it.</param>
    /// <param name="requirement">
    /// The requirement the verification artefact verifies. Pass
    /// <see langword="null"/> where no requirement has been written: the
    /// verification dataset is then empty and the design review carries no
    /// verification reference, because a verification artefact that names
    /// no real requirement is a dangling claim. The model enforces this —
    /// <see cref="VerifiedRequirement"/> refuses an empty identity — and
    /// the seed respects the refusal rather than minting a plausible
    /// <see cref="Guid"/> to get past it.
    /// </param>
    /// <param name="cancellationToken">A token observed while reading.</param>
    /// <returns>The four remaining asset datasets.</returns>
    /// <exception cref="ReferenceRecordNotFoundException">A record the assets cite is not registered.</exception>
    public static async Task<EngineeringAssetSeed> CreateAsync(
        IReferenceDataCatalog<MaterialDefinition> materials,
        IReferenceDataCatalog<EngineeringTemplate> templates,
        VerifiedRequirement? requirement = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(templates);

        var aluminium = await RequireAsync(materials, MaterialSeed.Aluminium6082T6, cancellationToken).ConfigureAwait(false);
        var steel = await RequireAsync(materials, MaterialSeed.S355J2, cancellationToken).ConfigureAwait(false);

        var template = await templates.FindAsync(TemplateRecordId, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(templates.LibraryName, TemplateRecordId);

        var aluminiumPin = ReferencePin.For(materials.LibraryName, aluminium);
        var steelPin = ReferencePin.For(materials.LibraryName, steel);
        var templatePin = ReferencePin.For(templates.LibraryName, template);

        return new EngineeringAssetSeed(
            new Seed<CalculationPack>("Bracket stress check", [BuildCalculationPack(aluminiumPin, templatePin)]),
            new Seed<VerificationArtefact>(
                "Bracket yield margin verification",
                requirement is null ? [] : [BuildVerification(aluminiumPin, requirement)]),
            new Seed<DesignReviewPack>("Bracket preliminary design review", [BuildDesignReview(requirement)]),
            new Seed<TechnicalDocument>("Bracket material selection note", [BuildTechnicalDocument(aluminiumPin, steelPin)]));
    }

    private static async Task<IReferenceRecord<MaterialDefinition>> RequireAsync(
        IReferenceDataCatalog<MaterialDefinition> materials,
        string recordId,
        CancellationToken cancellationToken) =>
        await materials.FindAsync(recordId, cancellationToken).ConfigureAwait(false)
        ?? throw new ReferenceRecordNotFoundException(materials.LibraryName, recordId);

    private static ReferenceSeedRecord<EngineeringTemplate> BuildTemplate() =>
        new(TemplateRecordId,
            new EngineeringTemplate
            {
                Reference = TemplateReference,
                Name = "Calculation record sheet",
                Purpose = "Captures a hand calculation so that a second engineer can repeat it and reach the "
                    + "same number without asking the first engineer anything.",
                Kind = TemplateKind.Calculation,
                Sections =
                [
                    new TemplateSection("SEC-1", "Identification", IsMandatory: true, Fields:
                    [
                        new TemplateField("F-TITLE", "Calculation title", TemplateFieldKind.Text, IsRequired: true),
                        new TemplateField("F-SUBJECT", "Subject", TemplateFieldKind.Text, IsRequired: true,
                            Guidance: "The part, assembly or interface the calculation is about."),
                        new TemplateField("F-AUTHOR", "Prepared by", TemplateFieldKind.Principal, IsRequired: true),
                    ]),
                    new TemplateSection("SEC-2", "Basis", IsMandatory: true,
                        Purpose: "Everything the result depends on, stated before the result appears.",
                        Fields:
                        [
                            new TemplateField("F-METHOD", "Method", TemplateFieldKind.Text, IsRequired: true),
                            new TemplateField("F-MATERIAL", "Material record", TemplateFieldKind.RecordReference,
                                IsRequired: true,
                                Guidance: "Cite the reference-data record and its revision, not just the grade "
                                    + "name. A grade name does not tell a later reader which published figure "
                                    + "was used."),
                            new TemplateField("F-ASSUMPTIONS", "Assumptions", TemplateFieldKind.Text, IsRequired: true),
                        ]),
                    new TemplateSection("SEC-3", "Result", IsMandatory: true, Fields:
                    [
                        new TemplateField("F-RESULT", "Result", TemplateFieldKind.Quantity, IsRequired: true),
                        new TemplateField("F-CRITERION", "Acceptance criterion", TemplateFieldKind.Text, IsRequired: true),
                        new TemplateField("F-VERDICT", "Meets criterion", TemplateFieldKind.Boolean, IsRequired: true),
                    ]),
                    new TemplateSection("SEC-4", "Check", IsMandatory: true,
                        Purpose: "An unchecked calculation is a draft, whatever it says on it.",
                        Fields:
                        [
                            new TemplateField("F-CHECKER", "Checked by", TemplateFieldKind.Principal),
                            new TemplateField("F-CHECK-DATE", "Checked on", TemplateFieldKind.Date),
                        ]),
                ],
                Instructions = "Complete every mandatory section before circulating. Leave the check section "
                    + "empty rather than self-checking.",
                Notes = "AUTHORED by Tempest Design Engineering. The structure encodes one opinion: that the "
                    + "basis of a calculation must be written down before the answer, because a basis "
                    + "reconstructed afterwards tends to be the one that justifies the answer.",
            },
            SeedSources.TempestAuthored("Engineering template library",
                "The first template in the library, written to give the calculation pack something real to cite."));

    private static ReferenceSeedRecord<CalculationPack> BuildCalculationPack(ReferencePin aluminiumPin, ReferencePin templatePin) =>
        new(CalculationPackRecordId,
            new CalculationPack
            {
                Reference = CalculationPackReference,
                Title = "Mounting bracket — direct stress check against material yield",
                Purpose = "Establishes whether the bracket's minimum section carries the design load with an "
                    + "acceptable margin against the material's published proof stress.",
                Method = new CalculationMethod(
                    CalculationMethodKind.ClosedForm,
                    "Direct stress on the minimum section, compared against the material's specified minimum "
                    + "0.2% proof stress with a stated margin.",
                    GoverningEquations: ["sigma = F / A", "margin = (Rp0.2 / sigma) - 1"]),
                Inputs =
                [
                    new CalculationInput(
                        "IN-MATERIAL",
                        "Material minimum 0.2% proof stress",
                        "260 MPa",
                        SourcePin: aluminiumPin,
                        SourceDescription: "6082-T6 extruded rod and bar, 20 mm to 150 mm diameter or across "
                            + "flats, per the pinned material record. The size band matters: the same record "
                            + "notes 250 MPa below 20 mm and 200 MPa above 200 mm.",
                        Dimension: "Pressure"),
                    new CalculationInput(
                        "IN-LOAD",
                        "Design load on the bracket",
                        "not established",
                        SourceDescription: "No requirement has been written for this bracket, so there is no "
                            + "design load to state. The input is present and empty rather than absent, so "
                            + "that the gap is visible in the pack rather than invisible in its omission.",
                        Dimension: "Force"),
                    new CalculationInput(
                        "IN-AREA",
                        "Minimum cross-sectional area",
                        "not established",
                        SourceDescription: "No geometry has been fixed for this bracket.",
                        Dimension: "Area"),
                ],
                Outputs =
                [
                    new CalculationOutput(
                        "OUT-MARGIN",
                        "Margin of safety against proof stress",
                        "not computed",
                        AcceptanceCriterion: "Margin greater than zero against the specified minimum proof stress.",
                        Interpretation: "Not computed, because two of the three inputs are not established. "
                            + "The pack records the method and the material basis; it does not claim a result."),
                ],
                Assumptions =
                [
                    new PackAssumption(
                        "AS-1",
                        "Loading is static and axial on the minimum section.",
                        Justification: "The closed-form method assumes it; a bending or fatigue case needs a "
                            + "different pack.",
                        WouldInvalidate: "Any cyclic loading, or a load path that puts the section in bending."),
                    new PackAssumption(
                        "AS-2",
                        "The delivered material meets the specified minimum proof stress for the size band "
                        + "actually supplied.",
                        Justification: "The pinned record's figure is a specification minimum, not a measured "
                            + "property of a particular bar.",
                        WouldInvalidate: "Supply from a size band with a lower minimum, or from a different "
                            + "product form such as tube or profile, both of which the material record notes "
                            + "carry different figures."),
                ],
                Limitations =
                [
                    "No result. This pack is a method and a basis, not a completed calculation.",
                    "Does not consider buckling, bearing at the fixing holes, bolt loads, or fatigue.",
                    "The material record it pins is unverified reference data in Draft state.",
                ],
                TemplateUsage = new TemplateUsage(templatePin, "Recorded this calculation on the calculation record sheet."),
                Notes = "AUTHORED by Tempest Design Engineering. Deliberately incomplete: it demonstrates that "
                    + "a calculation pack can pin the exact revision of the material data it relies on, which "
                    + "is the property that makes a calculation reproducible years later.",
            },
            SeedSources.TempestAuthored("Calculation pack library",
                "Written to demonstrate a pinned material basis on real reference data."));

    private static ReferenceSeedRecord<VerificationArtefact> BuildVerification(
        ReferencePin aluminiumPin,
        VerifiedRequirement requirement) =>
        new(VerificationRecordId,
            new VerificationArtefact
            {
                Reference = VerificationReference,
                Requirement = requirement,
                Subject = Subject,
                Method = VerificationMethod.Analysis,
                MethodDescription = "By the direct stress calculation recorded in " + CalculationPackReference + ".",
                AcceptanceCriteria = ["Margin of safety greater than zero."],
                Result = null,
                SourcePins = [aluminiumPin],
                Notes = "AUTHORED by Tempest Design Engineering. Standing is NotPerformed, and stays that way: "
                    + "the calculation it depends on has no result, so there is nothing to verify against. "
                    + "The requirement it names is a real registered requirement, pinned at the revision its "
                    + "statement was read at — the model refuses an artefact that names no requirement, and "
                    + "that refusal is right.",
            },
            SeedSources.TempestAuthored("Verification artefact library",
                "Written to demonstrate a verification artefact honestly reporting that nothing has been verified."));

    private static ReferenceSeedRecord<DesignReviewPack> BuildDesignReview(VerifiedRequirement? requirement) =>
        new(DesignReviewRecordId,
            new DesignReviewPack
            {
                Reference = "TDE-DRP-001",
                Subject = Subject,
                Kind = DesignReviewKind.Preliminary,
                CalculationPackReferences = [CalculationPackReference],
                VerificationArtefactReferences = requirement is null ? [] : [VerificationReference],
                RequirementIds = requirement is null ? [] : [requirement.RequirementId],
                Outcome = ReviewOutcome.NotConcluded,
                OutcomeRationale = "The review has not been held. The pack exists to collect what would be "
                    + "reviewed, not to record a meeting that did not happen.",
                Observations =
                [
                    new ReviewObservation(
                        "OBS-1",
                        "The requirement states a margin must be positive but sets no design load, so the "
                        + "calculation cannot be completed and the verification has nothing to verify. This is "
                        + "the first thing the review would raise.",
                        ObservationSeverity.Major),
                    new ReviewObservation(
                        "OBS-2",
                        "The material record the calculation pins is unverified reference data in Draft state. "
                        + "Releasing a design on it would need that record checked against EN 755-2 first.",
                        ObservationSeverity.Minor),
                ],
                Notes = "AUTHORED by Tempest Design Engineering. No participants are listed because nobody "
                    + "attended; an empty participant list is the true record of a review that has not been "
                    + "held, and populating it with plausible names would be a fabricated meeting minute.",
            },
            SeedSources.TempestAuthored("Design review library",
                "Written to demonstrate a review pack assembling real linked assets before a review takes place."));

    private static ReferenceSeedRecord<TechnicalDocument> BuildTechnicalDocument(
        ReferencePin aluminiumPin,
        ReferencePin steelPin) =>
        new(TechnicalDocumentRecordId,
            new TechnicalDocument
            {
                Reference = "TDE-DOC-001",
                Title = "Mounting bracket — material selection note",
                Type = TechnicalDocumentType.DesignReport,
                Status = DocumentStatus.Draft,
                IssueRevision = "A",
                Notes = "AUTHORED by Tempest Design Engineering. Compares two candidate materials held in the "
                    + "reference library — 6082-T6 at 260 MPa minimum proof stress and 2.70 g/cm3, and S355J2 "
                    + "at 355 MPa and 7.85 g/cm3 — and records that the choice cannot be made until a "
                    + "requirement states whether mass or strength governs. Pinned to both material records at "
                    + "the revisions read, so the comparison can be reconstructed even after either record is "
                    + "revised. Pins: "
                    + $"{aluminiumPin.Library}/{aluminiumPin.RecordId}@r{aluminiumPin.RevisionNumber}, "
                    + $"{steelPin.Library}/{steelPin.RecordId}@r{steelPin.RevisionNumber}.",
            },
            SeedSources.TempestAuthored("Technical documentation library",
                "Written to demonstrate a technical document standing on pinned reference data."));

    private sealed class Seed<TDefinition>(string datasetName, IReadOnlyList<ReferenceSeedRecord<TDefinition>> records)
        : IReferenceSeed<TDefinition>
    {
        public string DatasetName { get; } = $"Tempest-authored engineering assets — {datasetName}";

        public int DatasetRevision => 1;

        public IReadOnlyList<ReferenceSeedRecord<TDefinition>> Records { get; } = records;
    }
}
