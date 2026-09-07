using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringAssets.Verification;

/// <summary>A deterministic filter over the verification-artefact library.</summary>
public sealed record VerificationArtefactQuery
{
    /// <summary>Matches any artefact whose reference or subject contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches only artefacts verifying this requirement. <see langword="null"/> to match any.</summary>
    public Guid? RequirementId { get; init; }

    /// <summary>Matches any of these standings. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<VerificationStanding> Standings { get; init; } = [];

    /// <summary>Matches any of these methods. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<VerificationMethod> Methods { get; init; } = [];

    /// <summary>Matches only artefacts with, or without, locatable evidence. <see langword="null"/> to match any.</summary>
    public bool? IsEvidenced { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The verification-artefact library.</summary>
public interface IVerificationArtefactCatalog : IReferenceDataCatalog<VerificationArtefact>
{
    /// <summary>Returns the artefact registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<VerificationArtefact>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered artefact matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<VerificationArtefact>>> SearchAsync(VerificationArtefactQuery query, CancellationToken cancellationToken = default);

    /// <summary>Every artefact verifying <paramref name="requirementId"/>. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IReferenceRecord<VerificationArtefact>>> FindForRequirementAsync(Guid requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every artefact that still leaves work for somebody, weakest
    /// standing first.
    /// </summary>
    /// <remarks>
    /// Failures ahead of not-yet-started, because a failed verification
    /// is a live engineering problem and an unstarted one is a plan.
    /// </remarks>
    Task<IReadOnlyList<IReferenceRecord<VerificationArtefact>>> FindOutstandingAsync(CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IVerificationArtefactCatalog"/> implementation.</summary>
public sealed class VerificationArtefactCatalog : ReferenceDataCatalog<VerificationArtefact>, IVerificationArtefactCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every artefact's own backing document carries.</summary>
    public const string VerificationArtefactDocumentKind = "EngineeringVerificationArtefact";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string VerificationArtefactLibraryName = "EngineeringVerificationArtefacts";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>artefactId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "EngineeringVerificationArtefacts.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each artefact reference to the <c>artefactId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "EngineeringVerificationArtefacts.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="VerificationArtefactCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own artefacts are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public VerificationArtefactCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => VerificationArtefactLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => VerificationArtefactDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<VerificationArtefact>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(VerificationArtefact.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<VerificationArtefact>>> SearchAsync(
        VerificationArtefactQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<VerificationArtefact>>> FindForRequirementAsync(
        Guid requirementId,
        CancellationToken cancellationToken = default) =>
        FilterAsync(record => record.Definition.Requirement.RequirementId == requirementId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<VerificationArtefact>>> FindOutstandingAsync(CancellationToken cancellationToken = default)
    {
        var outstanding = await FilterAsync(record => record.Definition.IsOutstanding, cancellationToken).ConfigureAwait(false);

        return outstanding
            .OrderBy(r => VerificationStandings.Rank(r.Definition.Standing))
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(VerificationArtefact definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(VerificationArtefact definition) => $"Verification artefact reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<VerificationArtefact> record, VerificationArtefactQuery query)
    {
        var artefact = record.Definition;

        if (query.TextContains is { } text
            && !artefact.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !artefact.Subject.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.RequirementId is { } requirementId && artefact.Requirement.RequirementId != requirementId)
            return false;

        if (query.Standings.Count > 0 && !query.Standings.Contains(artefact.Standing))
            return false;

        if (query.Methods.Count > 0 && !query.Methods.Contains(artefact.Method))
            return false;

        if (query.IsEvidenced is { } evidenced && artefact.IsEvidenced != evidenced)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes E3's validation service reports.</summary>
public static class VerificationValidationRules
{
    /// <summary>The artefact records a pass with nothing anybody can go and look at.</summary>
    /// <remarks>
    /// The heaviest finding in `E3`, and an error rather than a warning.
    /// An unevidenced pass is the exact way missing evidence turns into a
    /// claim of compliance.
    /// </remarks>
    public const string PassIsUnevidenced = "TEMPEST-EAV-001";

    /// <summary>The artefact records a result but nobody is named as having performed it, or no date is given.</summary>
    public const string ResultIsNotAttributable = "TEMPEST-EAV-002";

    /// <summary>The artefact does not say how verification is to be done.</summary>
    public const string MethodNotStated = "TEMPEST-EAV-003";

    /// <summary>The artefact states nothing the requirement must satisfy.</summary>
    public const string NoAcceptanceCriteria = "TEMPEST-EAV-004";

    /// <summary>The artefact says the requirement does not apply but does not say why.</summary>
    public const string NotApplicableWithoutReason = "TEMPEST-EAV-005";

    /// <summary>The artefact did not record what the requirement said when it was verified.</summary>
    public const string RequirementRevisionNotPinned = "TEMPEST-EAV-006";

    /// <summary>The requirement named is not one the platform holds.</summary>
    public const string RequirementMustResolve = "TEMPEST-EAV-007";

    /// <summary>Verification was by analysis but no calculation pack is named.</summary>
    public const string AnalysisCitesNoCalculation = "TEMPEST-EAV-008";

    /// <summary>The named calculation pack is not one the library holds.</summary>
    public const string CalculationPackMustResolve = "TEMPEST-EAV-009";

    /// <summary>A failed verification has nothing recorded about what happens next.</summary>
    public const string FailureHasNoCommentary = "TEMPEST-EAV-010";

    /// <summary>The result rests entirely on the asserting party's own material.</summary>
    public const string NoIndependentEvidence = "TEMPEST-EAV-011";

    /// <summary>Two artefacts verify the same requirement by the same method.</summary>
    public const string DuplicateVerification = "TEMPEST-EAV-012";

    /// <summary>The artefact pins a record that has since been superseded.</summary>
    public const string PinnedSourceSuperseded = "TEMPEST-EAV-013";
}

/// <summary>Governance of the verification-artefact library itself.</summary>
public interface IVerificationArtefactValidationService : IReferenceValidationService<VerificationArtefact>
{
}

/// <summary>The concrete <see cref="IVerificationArtefactValidationService"/> implementation.</summary>
/// <remarks>
/// One finding here is an error where the rest are warnings: a recorded
/// pass with nothing behind it. Everything else `E3` reports is a gap
/// somebody may legitimately accept; an unevidenced pass is a claim the
/// records cannot support, and it is the specific thing §11 forbids.
/// </remarks>
public sealed class VerificationArtefactValidationService
    : ReferenceValidationService<VerificationArtefact>, IVerificationArtefactValidationService
{
    private readonly IVerificationArtefactCatalog _artefacts;
    private readonly CalculationPacks.ICalculationPackCatalog? _calculationPacks;
    private readonly IReadOnlyDictionary<string, IReferencePinResolver> _pinResolvers;

    /// <summary>Initialises a new instance of the <see cref="VerificationArtefactValidationService"/> class.</summary>
    /// <param name="catalog">The artefact library whose records this service validates.</param>
    /// <param name="calculationPacks">The `E2` library, for confirming a named analysis pack exists. Optional.</param>
    /// <param name="pinResolvers">Resolvers for the libraries an artefact may pin. Optional.</param>
    public VerificationArtefactValidationService(
        IVerificationArtefactCatalog catalog,
        CalculationPacks.ICalculationPackCatalog? calculationPacks = null,
        IEnumerable<IReferencePinResolver>? pinResolvers = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _artefacts = catalog;
        _calculationPacks = calculationPacks;
        _pinResolvers = (pinResolvers ?? []).ToDictionary(r => r.LibraryName, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        VerificationArtefact definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Verification artefact '{definition.Reference}'";

        if (definition.IsUnsupportedPass)
            errors.Add(AssetGovernanceValidation.Diagnostic(
                VerificationValidationRules.PassIsUnevidenced,
                $"{subject} records the requirement as met and offers nothing anybody can go and look at. "
                + "Missing evidence is not a pass."));

        if (definition.Result is { } result)
        {
            if (!result.IsAttributable)
                errors.Add(AssetGovernanceValidation.Diagnostic(
                    VerificationValidationRules.ResultIsNotAttributable,
                    $"{subject} records a {result.Standing} result but names nobody who performed it, no date, or "
                    + "neither. An unattributable result is not evidence."));

            if (result.Standing == VerificationStanding.Failed && string.IsNullOrWhiteSpace(definition.Notes))
                warnings.Add(AssetGovernanceValidation.Diagnostic(
                    VerificationValidationRules.FailureHasNoCommentary,
                    $"{subject} records a failure and says nothing about what follows from it."));
        }

        if (definition.Method == VerificationMethod.Unspecified)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                VerificationValidationRules.MethodNotStated,
                $"{subject} does not say how the requirement is to be verified."));

        if (definition.AcceptanceCriteria.Count == 0)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                VerificationValidationRules.NoAcceptanceCriteria,
                $"{subject} states nothing the requirement must satisfy, so whether it passed is a matter of opinion."));

        if (definition.Standing == VerificationStanding.NotApplicable && string.IsNullOrWhiteSpace(definition.NotApplicableReason))
            errors.Add(AssetGovernanceValidation.Diagnostic(
                VerificationValidationRules.NotApplicableWithoutReason,
                $"{subject} declares the requirement inapplicable without saying why. That is how a requirement gets "
                + "quietly dropped."));

        if (!definition.Requirement.IsPinnedToRevision)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                VerificationValidationRules.RequirementRevisionNotPinned,
                $"{subject} did not record which revision of the requirement it verified. If the requirement is "
                + "reworded, this evidence will appear to address wording it never saw."));

        if (definition.Method == VerificationMethod.Analysis && definition.Result?.CalculationPackReference is null)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                VerificationValidationRules.AnalysisCitesNoCalculation,
                $"{subject} verifies by analysis but names no calculation pack holding it."));

        if (definition.IsDemonstrated && definition.IsEvidenced && !definition.HasIndependentEvidence)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                VerificationValidationRules.NoIndependentEvidence,
                $"{subject} passes on internal records and judgement alone, with no test, inspection or certificate "
                + "behind it."));

        AssetGovernanceValidation.Evaluate(definition.Governance, subject, errors, warnings);

        await EvaluateCalculationPackAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
        await EvaluateDuplicatesAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
        await EvaluatePinsAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    private async Task EvaluateCalculationPackAsync(
        VerificationArtefact definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (_calculationPacks is null || definition.Result?.CalculationPackReference is not { } reference)
            return;

        var pack = await _calculationPacks.FindByReferenceAsync(reference, cancellationToken).ConfigureAwait(false);

        if (pack is null)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                VerificationValidationRules.CalculationPackMustResolve,
                $"{subject} cites calculation pack '{reference}', which the library does not hold."));
    }

    private async Task EvaluateDuplicatesAsync(
        VerificationArtefact definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var siblings = await _artefacts
            .FindForRequirementAsync(definition.Requirement.RequirementId, cancellationToken)
            .ConfigureAwait(false);

        var duplicates = siblings
            .Where(s => s.Definition.Method == definition.Method
                        && !string.Equals(s.Definition.Reference, definition.Reference, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var duplicate in duplicates)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                VerificationValidationRules.DuplicateVerification,
                $"{subject} verifies the same requirement by the same method as '{duplicate.Definition.Reference}'. "
                + "Legitimate where the subjects differ; worth checking where they do not."));
    }

    private async Task EvaluatePinsAsync(
        VerificationArtefact definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        foreach (var pin in definition.SourcePins)
        {
            if (!_pinResolvers.TryGetValue(pin.Library, out var resolver))
                continue;

            var state = await resolver.ResolveAsync(pin, cancellationToken).ConfigureAwait(false);

            if (state == ReferenceValidationState.Superseded)
                warnings.Add(AssetGovernanceValidation.Diagnostic(
                    VerificationValidationRules.PinnedSourceSuperseded,
                    $"{subject} rests on {pin.Library} record '{pin.RecordId}' revision {pin.RevisionNumber}, which has "
                    + "since been superseded. The evidence is unchanged and still records what was verified."));
        }
    }
}
