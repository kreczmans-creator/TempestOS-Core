using Tempest.Core.EngineeringIntelligence.Subjects;
using Tempest.Core.Identity;
using Tempest.Core.Manufacturing;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.EngineeringIntelligence.Decisions;

/// <summary>
/// Reasons from a part's requirements to candidate manufacturing processes
/// (`B2`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two mechanisms, deliberately separate.</b> Screening compares a
/// part's requirements against each process's published capability bands,
/// and is exhaustive: every process is assessed and reported, including
/// the ones ruled out and why. A decision tree walk answers a structured
/// engineering question whose answer is not a simple band comparison, and
/// reports the path it took. Either is useful alone; together the tree
/// narrows and the screening tests.
/// </para>
/// <para>
/// <b>What this does not do.</b> No process planning, no operation
/// sequencing, no routing, no cost or cycle-time estimate, no supplier
/// selection. A screening result is a candidate set with reasons, and
/// choosing from it is an engineering decision.
/// </para>
/// </remarks>
public interface IManufacturingDecisionService
{
    /// <summary>
    /// Screens <paramref name="candidates"/> against
    /// <paramref name="requirements"/>, reporting every candidate and why
    /// it stands where it does.
    /// </summary>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    Task<ProcessScreeningResult> ScreenAsync(
        ManufacturingRequirementSet requirements,
        IReadOnlyList<IReferenceRecord<ProcessDefinition>> candidates,
        CancellationToken cancellationToken = default);

    /// <summary>Screens every process the `A7` catalogue holds that <paramref name="requirements"/> permits as a candidate.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="requirements"/> is <see langword="null"/>.</exception>
    Task<ProcessScreeningResult> ScreenCatalogueAsync(
        ManufacturingRequirementSet requirements,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Walks the released decision tree registered under
    /// <paramref name="treeCode"/> for <paramref name="subject"/>.
    /// </summary>
    /// <remarks>
    /// Released only, for the same reason a rule must be: an unreviewed
    /// tree is engineering guidance nobody has finished checking.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="treeCode"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is <see langword="null"/>.</exception>
    /// <exception cref="ReferenceRecordNotFoundException">No tree is registered under <paramref name="treeCode"/>.</exception>
    /// <exception cref="UnreleasedDecisionTreeException">The tree exists but has not been released.</exception>
    Task<DecisionWalk> WalkAsync(string treeCode, IAssessmentSubject subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-walks a tree at the exact revision a previous walk pinned, so a
    /// historical decision can be reproduced.
    /// </summary>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="treePin"/> names a library other than the tree library.</exception>
    Task<DecisionWalk> ReproduceWalkAsync(ReferencePin treePin, IAssessmentSubject subject, CancellationToken cancellationToken = default);
}

/// <summary>Thrown when a decision tree exists but has not been released as guidance.</summary>
/// <remarks>
/// An exception rather than an empty result, deliberately: asking to walk
/// a specific tree by code and silently getting nothing back would be
/// indistinguishable from the tree concluding nothing. The caller asked
/// for a named tree and is entitled to know why it did not run.
/// </remarks>
public sealed class UnreleasedDecisionTreeException : ReferenceDataException
{
    /// <summary>Initialises a new instance of the <see cref="UnreleasedDecisionTreeException"/> class.</summary>
    /// <param name="treeCode">The tree that was asked for.</param>
    /// <param name="state">The state it is actually in.</param>
    public UnreleasedDecisionTreeException(string treeCode, ReferenceValidationState state)
        : base(
            "DecisionTrees",
            $"Decision tree '{treeCode}' is {state}, not Released. A tree nobody has finished reviewing must not "
            + "produce an engineering decision, so it is refused rather than walked.")
    {
        TreeCode = treeCode;
        State = state;
    }

    /// <summary>The tree that was asked for.</summary>
    public string TreeCode { get; }

    /// <summary>The state it is actually in.</summary>
    public ReferenceValidationState State { get; }
}

/// <summary>The concrete <see cref="IManufacturingDecisionService"/> implementation.</summary>
public sealed class ManufacturingDecisionService : IManufacturingDecisionService
{
    /// <summary>Recorded as the screener where no principal is established.</summary>
    public const string UnknownScreenerPrincipalId = "unknown";

    private readonly IProcessCatalog _processes;
    private readonly IDecisionTreeCatalog _trees;
    private readonly ICurrentPrincipalAccessor _principals;
    private readonly IReleasedConstantSource? _constants;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ManufacturingDecisionService"/> class.</summary>
    /// <param name="processes">The `A7` catalogue candidates are read from.</param>
    /// <param name="trees">The decision-tree library.</param>
    /// <param name="principals">The platform's own identity boundary, for attributing a screening.</param>
    /// <param name="constants">The released-constant seam, for tree conditions whose thresholds are constants. Optional.</param>
    /// <param name="timeProvider">The clock a screening is stamped with. Defaults to the system clock.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public ManufacturingDecisionService(
        IProcessCatalog processes,
        IDecisionTreeCatalog trees,
        ICurrentPrincipalAccessor principals,
        IReleasedConstantSource? constants = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(trees);
        ArgumentNullException.ThrowIfNull(principals);

        _processes = processes;
        _trees = trees;
        _principals = principals;
        _constants = constants;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<ProcessScreeningResult> ScreenAsync(
        ManufacturingRequirementSet requirements,
        IReadOnlyList<IReferenceRecord<ProcessDefinition>> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(candidates);

        var assessments = candidates
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .Select(candidate => Screen(requirements, candidate))
            .ToList();

        return Task.FromResult(new ProcessScreeningResult(
            requirements.PartDescription,
            requirements.StatedRequirements,
            assessments,
            Walk: null,
            _time.GetUtcNow(),
            _principals.Current?.Identity.Id ?? UnknownScreenerPrincipalId));
    }

    /// <inheritdoc />
    public async Task<ProcessScreeningResult> ScreenCatalogueAsync(
        ManufacturingRequirementSet requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        var query = new ProcessQuery
        {
            ValidationStates = requirements.RequireReleasedProcesses ? [ReferenceValidationState.Released] : [],
        };

        var candidates = await _processes.SearchAsync(query, cancellationToken).ConfigureAwait(false);

        return await ScreenAsync(requirements, candidates, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DecisionWalk> WalkAsync(string treeCode, IAssessmentSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(treeCode);
        ArgumentNullException.ThrowIfNull(subject);

        var record = await _trees.FindByCodeAsync(treeCode, cancellationToken).ConfigureAwait(false)
            ?? throw new ReferenceRecordNotFoundException(_trees.LibraryName, treeCode);

        if (record.ValidationState != ReferenceValidationState.Released)
            throw new UnreleasedDecisionTreeException(treeCode, record.ValidationState);

        return await WalkRecordAsync(record, subject, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DecisionWalk> ReproduceWalkAsync(
        ReferencePin treePin,
        IAssessmentSubject subject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(treePin);
        ArgumentNullException.ThrowIfNull(subject);

        if (!string.Equals(treePin.Library, _trees.LibraryName, StringComparison.Ordinal))
            throw new ArgumentException(
                $"Pin {treePin} names library '{treePin.Library}', and this service can only reproduce decision-tree pins.",
                nameof(treePin));

        // Read the tree as it stood, not as it is. A released tree is
        // immutable, so this differs from the current record only where
        // the tree has since been superseded — which is exactly the case
        // reproduction exists for.
        var record = await _trees
            .GetRevisionAsync(treePin.RecordId, treePin.RevisionNumber, cancellationToken)
            .ConfigureAwait(false);

        return await WalkRecordAsync(record, subject, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DecisionWalk> WalkRecordAsync(
        IReferenceRecord<DecisionTree> record,
        IAssessmentSubject subject,
        CancellationToken cancellationToken)
    {
        var constants = _constants is null
            ? ConstantResolutionSet.Empty
            : await ResolveTreeConstantsAsync(record.Definition, _constants, cancellationToken).ConfigureAwait(false);

        return DecisionTreeWalker.Walk(
            record.Definition,
            ReferencePin.For(_trees.LibraryName, record),
            subject,
            constants);
    }

    private static async Task<ConstantResolutionSet> ResolveTreeConstantsAsync(
        DecisionTree tree,
        IReleasedConstantSource constants,
        CancellationToken cancellationToken)
    {
        // Every branch condition in the tree, presented to the shared
        // resolver as rules, so constant resolution has one implementation
        // rather than two that could diverge.
        var probes = tree.Nodes
            .SelectMany(node => node.Branches)
            .Select(branch => new RuleDefinition
            {
                Code = "BRANCH",
                Name = branch.Label,
                Statement = branch.Label,
                Condition = branch.Condition,
            });

        return await ConstantResolutionSet.ResolveForAsync(probes, constants, cancellationToken).ConfigureAwait(false);
    }

    private static ProcessCandidateAssessment Screen(
        ManufacturingRequirementSet requirements,
        IReferenceRecord<ProcessDefinition> candidate)
    {
        var subject = new ProcessSubject(candidate);
        var assessments = new List<ProcessRequirementAssessment>();

        if (requirements.RequireReleasedProcesses && candidate.ValidationState != ReferenceValidationState.Released)
            assessments.Add(new ProcessRequirementAssessment(
                "The process record must be Released",
                AssessmentOutcome.Fail,
                $"{subject.DisplayName} is {candidate.ValidationState}, not Released. Its capability bands have not been verified."));

        if (requirements.MaterialFamily is { } family)
            assessments.Add(AssessMaterialFamily(subject, family));

        // A requirement is met when it falls inside the published band. The
        // band end each requirement is tested against is the one that can
        // rule the process out: a tolerance must be no tighter than the
        // finest the process holds, a part no larger than the largest it
        // handles.
        AddIfStated(assessments, requirements.RequiredTolerance, subject, SubjectPropertyNames.FinestAchievableTolerance,
            QuantityComparator.AtMost, "hold a tolerance of", tighterIsBetter: true);

        AddIfStated(assessments, requirements.RequiredSurfaceRoughness, subject, SubjectPropertyNames.FinestSurfaceRoughness,
            QuantityComparator.AtMost, "leave a surface no rougher than", tighterIsBetter: true);

        AddIfStated(assessments, requirements.ThinnestWall, subject, SubjectPropertyNames.MinimumWallThickness,
            QuantityComparator.AtMost, "produce a wall as thin as", tighterIsBetter: true);

        AddIfStated(assessments, requirements.SmallestFeature, subject, SubjectPropertyNames.MinimumFeatureSize,
            QuantityComparator.AtMost, "resolve a feature as small as", tighterIsBetter: true);

        AddIfStated(assessments, requirements.LargestDimension, subject, SubjectPropertyNames.MaximumPartSize,
            QuantityComparator.AtLeast, "handle a part as large as", tighterIsBetter: false);

        AddIfStated(assessments, requirements.PartMass, subject, SubjectPropertyNames.MaximumPartMass,
            QuantityComparator.AtLeast, "handle a part as heavy as", tighterIsBetter: false);

        if (requirements.ProductionScale is { } scale)
            assessments.Add(AssessProductionScale(candidate, subject, scale));

        if (requirements.AdditionalRequirements is { } additional)
            assessments.Add(new ProcessRequirementAssessment(
                additional,
                AssessmentOutcome.EvidenceRequired,
                "This requirement is stated in prose and cannot be tested against a published capability band. "
                + "It needs a person to confirm it against the process."));

        return new ProcessCandidateAssessment(
            candidate.Id,
            subject.DisplayName,
            candidate.Definition.Family.ToString(),
            subject.Pin,
            assessments);
    }

    private static void AddIfStated<TDimension>(
        List<ProcessRequirementAssessment> into,
        Quantity<TDimension>? required,
        ProcessSubject subject,
        string propertyName,
        QuantityComparator comparator,
        string verb,
        bool tighterIsBetter)
        where TDimension : IDimension
    {
        if (required is not { } requirement)
            return;

        var label = $"The process must {verb} {requirement}";
        var capability = subject.GetQuantity(propertyName);

        if (capability.Availability != ReferencePropertyAvailability.Recorded || capability.Value is null)
        {
            into.Add(new ProcessRequirementAssessment(
                label,
                capability.AbsenceOutcome,
                capability.Availability == ReferencePropertyAvailability.NotApplicable
                    ? $"{propertyName} does not apply to {subject.DisplayName}, so this requirement does not bear on it."
                    : $"{subject.DisplayName} publishes no {propertyName}, so whether it meets this requirement is unknown. "
                      + "An unpublished capability band is not an unlimited one."));
            return;
        }

        var published = capability.Value.CanonicalValue;
        var wanted = requirement.BaseValue;

        var satisfied = comparator == QuantityComparator.AtMost ? published <= wanted : published >= wanted;

        into.Add(new ProcessRequirementAssessment(
            label,
            satisfied ? AssessmentOutcome.Pass : AssessmentOutcome.Fail,
            $"{subject.DisplayName} publishes {propertyName} of {capability.Value.Value}"
            + (capability.Value.Conditions is { } conditions ? $" ({conditions})" : string.Empty)
            + $". The part needs {requirement}. "
            + (satisfied
                ? "The published band covers it."
                : tighterIsBetter
                    ? "The process cannot hold it that tightly."
                    : "The process cannot handle something that large.")));
    }

    private static ProcessRequirementAssessment AssessMaterialFamily(ProcessSubject subject, Materials.MaterialFamily family)
    {
        var outcome = subject.AssessMaterialFamily(family);

        return new ProcessRequirementAssessment(
            $"The process must be recorded as working on {family}",
            outcome,
            outcome switch
            {
                AssessmentOutcome.Pass => $"A source records {subject.DisplayName} as suitable for {family}.",
                AssessmentOutcome.Concern => $"A source records {subject.DisplayName} as conditionally suitable for {family}; "
                    + "the stated conditions need confirming.",
                AssessmentOutcome.Fail => $"A source explicitly records {family} as not processed by {subject.DisplayName}.",
                AssessmentOutcome.NotRecorded => $"No source records whether {subject.DisplayName} works on {family}. "
                    + "That is a gap in the process record, not a statement that it does not.",
                _ => $"A source associates {family} with {subject.DisplayName} but does not say whether the pairing works.",
            });
    }

    private static ProcessRequirementAssessment AssessProductionScale(
        IReferenceRecord<ProcessDefinition> candidate,
        ProcessSubject subject,
        Manufacturing.ProductionScale scale)
    {
        var scales = candidate.Definition.ProductionScales;
        var label = $"The process must be recorded as used at {scale} volume";

        if (scales.Count == 0)
            return new ProcessRequirementAssessment(
                label,
                AssessmentOutcome.NotRecorded,
                $"No source records a production scale for {subject.DisplayName}. That is a gap, not a statement "
                + "that the process suits no scale.");

        return new ProcessRequirementAssessment(
            label,
            scales.Contains(scale) ? AssessmentOutcome.Pass : AssessmentOutcome.Concern,
            $"Sources record {subject.DisplayName} at [{string.Join(", ", scales)}]. "
            + (scales.Contains(scale)
                ? $"{scale} is among them."
                : $"{scale} is not among them, which is a reason to look closer rather than a reason to rule the process out — "
                  + "a source describing common use is not a source stating a limit."));
    }
}
