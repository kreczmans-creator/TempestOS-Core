using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringAssets.CalculationPacks;

/// <summary>A deterministic filter over the calculation-pack library.</summary>
public sealed record CalculationPackQuery
{
    /// <summary>Matches any pack whose reference, title or purpose contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these method kinds. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<CalculationMethodKind> MethodKinds { get; init; } = [];

    /// <summary>Matches packs applying to this enquiry. <see langword="null"/> to leave every dimension open.</summary>
    public AssetEnquiry? Enquiry { get; init; }

    /// <summary>Matches only packs somebody could carry out again. <see langword="null"/> to match any.</summary>
    public bool? IsReproducible { get; init; }

    /// <summary>Matches only packs citing this record, at any revision. <see langword="null"/> to match any.</summary>
    public ReferencePin? CitesPin { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The calculation-pack library.</summary>
public interface ICalculationPackCatalog : IReferenceDataCatalog<CalculationPack>
{
    /// <summary>Returns the pack registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<CalculationPack>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered pack matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<CalculationPack>>> SearchAsync(CalculationPackQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every pack that rests on <paramref name="pin"/>'s record, at any
    /// revision.
    /// </summary>
    /// <remarks>
    /// The impact question asked backwards: a material property has been
    /// revised, so which calculations relied on the old one? Those packs
    /// do not change — that is what pinning is for — but somebody may
    /// need to know they are now behind the library.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="pin"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<CalculationPack>>> FindCitingAsync(ReferencePin pin, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ICalculationPackCatalog"/> implementation.</summary>
public sealed class CalculationPackCatalog : ReferenceDataCatalog<CalculationPack>, ICalculationPackCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every pack's own backing document carries.</summary>
    public const string CalculationPackDocumentKind = "EngineeringCalculationPack";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string CalculationPackLibraryName = "EngineeringCalculationPacks";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>packId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "EngineeringCalculationPacks.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each pack reference to the <c>packId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "EngineeringCalculationPacks.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="CalculationPackCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own packs are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public CalculationPackCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => CalculationPackLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => CalculationPackDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<CalculationPack>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(CalculationPack.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<CalculationPack>>> SearchAsync(
        CalculationPackQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<CalculationPack>>> FindCitingAsync(
        ReferencePin pin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pin);

        return FilterAsync(
            record => record.Definition.AllPins.Any(
                p => string.Equals(p.Library, pin.Library, StringComparison.Ordinal)
                     && string.Equals(p.RecordId, pin.RecordId, StringComparison.Ordinal)),
            cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(CalculationPack definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(CalculationPack definition) => $"Calculation pack reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<CalculationPack> record, CalculationPackQuery query)
    {
        var pack = record.Definition;

        if (query.TextContains is { } text
            && !pack.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !pack.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !pack.Purpose.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.MethodKinds.Count > 0 && !query.MethodKinds.Contains(pack.Method.Kind))
            return false;

        if (query.Enquiry is { } enquiry && !pack.Applicability.AppliesTo(enquiry))
            return false;

        if (query.IsReproducible is { } reproducible && pack.IsReproducible != reproducible)
            return false;

        if (query.CitesPin is { } pin
            && !pack.AllPins.Any(p => string.Equals(p.Library, pin.Library, StringComparison.Ordinal)
                                      && string.Equals(p.RecordId, pin.RecordId, StringComparison.Ordinal)))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes E2's validation service reports.</summary>
public static class CalculationPackValidationRules
{
    /// <summary>The pack takes no inputs, so there is nothing to reproduce.</summary>
    public const string PackHasNoInputs = "TEMPEST-EAC-001";

    /// <summary>The pack produces no outputs, so it answers nothing.</summary>
    public const string PackHasNoOutputs = "TEMPEST-EAC-002";

    /// <summary>Two inputs, outputs or assumptions share one reference.</summary>
    public const string DuplicatePackReference = "TEMPEST-EAC-003";

    /// <summary>The pack does not say how the calculation was carried out.</summary>
    public const string MethodKindNotStated = "TEMPEST-EAC-004";

    /// <summary>An input does not say where its value came from.</summary>
    public const string InputHasNoSource = "TEMPEST-EAC-005";

    /// <summary>An input's value cannot be traced to a governed record.</summary>
    public const string InputIsUntraceable = "TEMPEST-EAC-006";

    /// <summary>Software was used but the pack does not name it, or does not name its version.</summary>
    public const string ToolNotIdentified = "TEMPEST-EAC-007";

    /// <summary>The method comes from a standard but the pack cites none.</summary>
    public const string StandardMethodCitesNoStandard = "TEMPEST-EAC-008";

    /// <summary>An assumption states no justification.</summary>
    public const string AssumptionIsUnjustified = "TEMPEST-EAC-009";

    /// <summary>No output states what it has to satisfy, so the pack reaches no conclusion.</summary>
    public const string NoAcceptanceCriteria = "TEMPEST-EAC-010";

    /// <summary>The pack states no limitations, which is rarely true of a real calculation.</summary>
    public const string NoLimitationsStated = "TEMPEST-EAC-011";

    /// <summary>The pack cannot be carried out again from what it records.</summary>
    public const string PackIsNotReproducible = "TEMPEST-EAC-012";

    /// <summary>A record the pack pins has since been superseded.</summary>
    public const string PinnedSourceSuperseded = "TEMPEST-EAC-013";

    /// <summary>The pack names a TempestOS calculation definition but links no execution of it.</summary>
    public const string PlatformCalculationHasNoExecution = "TEMPEST-EAC-014";

    /// <summary>The pack has run past its own effective period.</summary>
    public const string PackHasExpired = "TEMPEST-EAC-015";
}

/// <summary>Governance of the calculation-pack library itself.</summary>
public interface ICalculationPackValidationService : IReferenceValidationService<CalculationPack>
{
}

/// <summary>The concrete <see cref="ICalculationPackValidationService"/> implementation.</summary>
/// <remarks>
/// The findings are about whether the calculation survives the person who
/// did it. An unsourced input, an unnamed solver version, an
/// unjustified assumption — each leaves a pack that reads convincingly
/// and cannot be checked. None of them is a wrong number, and the
/// service never corrects one.
/// </remarks>
public sealed class CalculationPackValidationService
    : ReferenceValidationService<CalculationPack>, ICalculationPackValidationService
{
    private readonly IReadOnlyDictionary<string, IReferencePinResolver> _pinResolvers;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="CalculationPackValidationService"/> class.</summary>
    /// <param name="catalog">The pack library whose records this service validates.</param>
    /// <param name="pinResolvers">Resolvers for the libraries a pack may pin, keyed by library name. Optional; pins into libraries with no resolver are not checked.</param>
    /// <param name="timeProvider">The clock staleness checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public CalculationPackValidationService(
        ICalculationPackCatalog catalog,
        IEnumerable<IReferencePinResolver>? pinResolvers = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _pinResolvers = (pinResolvers ?? []).ToDictionary(r => r.LibraryName, StringComparer.Ordinal);
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        CalculationPack definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Calculation pack '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (definition.Inputs.Count == 0)
            errors.Add(AssetGovernanceValidation.Diagnostic(
                CalculationPackValidationRules.PackHasNoInputs,
                $"{subject} takes no inputs, so there is nothing to reproduce."));

        if (definition.Outputs.Count == 0)
            errors.Add(AssetGovernanceValidation.Diagnostic(
                CalculationPackValidationRules.PackHasNoOutputs,
                $"{subject} produces no outputs, so it answers nothing."));

        AssetGovernanceValidation.EvaluateDuplicateReferences(
            definition.Inputs.Select(i => i.Reference),
            $"{subject} has two inputs sharing the reference",
            errors);

        AssetGovernanceValidation.EvaluateDuplicateReferences(
            definition.Outputs.Select(o => o.Reference),
            $"{subject} has two outputs sharing the reference",
            errors);

        AssetGovernanceValidation.EvaluateDuplicateReferences(
            definition.Assumptions.Select(a => a.Reference),
            $"{subject} has two assumptions sharing the reference",
            errors);

        if (definition.Method.Kind == CalculationMethodKind.Unspecified)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                CalculationPackValidationRules.MethodKindNotStated,
                $"{subject} does not say how the calculation was carried out."));

        foreach (var input in definition.UnsourcedInputs)
            errors.Add(AssetGovernanceValidation.Diagnostic(
                CalculationPackValidationRules.InputHasNoSource,
                $"{subject} input '{input.Reference}' carries the value {input.Value} and says nowhere it came from."));

        foreach (var input in definition.UntraceableInputs.Where(i => i.HasStatedSource))
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                CalculationPackValidationRules.InputIsUntraceable,
                $"{subject} input '{input.Reference}' names its source in words but pins no governed record, so the "
                + "value cannot be resolved back to a revision."));

        if (!definition.Method.IsToolIdentified)
            errors.Add(AssetGovernanceValidation.Diagnostic(
                CalculationPackValidationRules.ToolNotIdentified,
                $"{subject} was produced with software but does not name the tool and its version. A numerical result "
                + "from an unnamed solver at an unnamed version cannot be reproduced."));

        if (definition.Method.Kind == CalculationMethodKind.StandardMethod && definition.Method.StandardReferences.Count == 0)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                CalculationPackValidationRules.StandardMethodCitesNoStandard,
                $"{subject} says its method comes from a standard but cites none."));

        foreach (var assumption in definition.Assumptions.Where(a => !a.IsJustified))
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                CalculationPackValidationRules.AssumptionIsUnjustified,
                $"{subject} assumption '{assumption.Reference}' states no justification."));

        if (definition.Outputs.Count > 0 && !definition.Outputs.Any(o => o.HasAcceptanceCriterion))
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                CalculationPackValidationRules.NoAcceptanceCriteria,
                $"{subject} states no acceptance criterion on any output, so it produces numbers rather than a conclusion."));

        if (definition.Limitations.Count == 0)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                CalculationPackValidationRules.NoLimitationsStated,
                $"{subject} states no limitations. Few real calculations have none."));

        if (!definition.IsReproducible)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                CalculationPackValidationRules.PackIsNotReproducible,
                $"{subject} does not record enough for somebody else to carry the calculation out again."));

        if (definition.Method.IsPlatformCalculation && definition.ExecutionRecordIds.Count == 0)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                CalculationPackValidationRules.PlatformCalculationHasNoExecution,
                $"{subject} names TempestOS calculation definition '{definition.Method.CalculationDefinitionId}' but links "
                + "no execution of it, so the platform's own record of the arithmetic cannot be found."));

        if (definition.IsStaleAt(today))
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                CalculationPackValidationRules.PackHasExpired,
                $"{subject} ran past its own effective period on {definition.Applicability.Validity!.To:O}."));

        AssetGovernanceValidation.Evaluate(definition.Governance, subject, errors, warnings);

        await EvaluatePinsAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    private async Task EvaluatePinsAsync(
        CalculationPack definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        foreach (var pin in definition.AllPins)
        {
            if (!_pinResolvers.TryGetValue(pin.Library, out var resolver))
                continue;

            var state = await resolver.ResolveAsync(pin, cancellationToken).ConfigureAwait(false);

            if (state == ReferenceValidationState.Superseded)
                warnings.Add(AssetGovernanceValidation.Diagnostic(
                    CalculationPackValidationRules.PinnedSourceSuperseded,
                    $"{subject} rests on {pin.Library} record '{pin.RecordId}' revision {pin.RevisionNumber}, which has "
                    + "since been superseded. The pack itself is unchanged and remains an accurate record of the "
                    + "calculation as performed."));
        }
    }
}
