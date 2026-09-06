namespace Tempest.Core.EngineeringAssets.Verification;

/// <summary>
/// How one requirement stands across every verification anybody has
/// planned or performed against it.
/// </summary>
/// <param name="RequirementId">The requirement.</param>
/// <param name="RequirementIdentifier">Its human identifier, where any artefact recorded one.</param>
/// <param name="Standing">Where the requirement stands overall.</param>
/// <param name="Artefacts">The artefacts verifying it, weakest standing first. Never <see langword="null"/>.</param>
/// <param name="Concerns">What is wrong or missing, in plain words. Never <see langword="null"/>.</param>
public sealed record RequirementVerificationTrace(
    Guid RequirementId,
    string? RequirementIdentifier,
    VerificationStanding Standing,
    IReadOnlyList<VerificationArtefact> Artefacts,
    IReadOnlyList<string> Concerns)
{
    /// <summary>The artefacts verifying the requirement.</summary>
    public IReadOnlyList<VerificationArtefact> Artefacts { get; } = Artefacts ?? [];

    /// <summary>What is wrong or missing.</summary>
    public IReadOnlyList<string> Concerns { get; } = Concerns ?? [];

    /// <summary>Whether the requirement has actually been shown to be met.</summary>
    public bool IsDemonstrated => VerificationStandings.IsDemonstrated(Standing);

    /// <summary>Whether anybody has planned any verification at all.</summary>
    public bool IsPlanned => Artefacts.Count > 0;

    /// <summary>Whether the trace found nothing to worry about.</summary>
    /// <remarks>
    /// Not the same as <see cref="IsDemonstrated"/>, and deliberately so.
    /// A requirement nobody has verified raises a concern; one verified
    /// and failed raises a different one; and neither is "clean".
    /// </remarks>
    public bool IsClean => Concerns.Count == 0;
}

/// <summary>Answers "is this requirement verified, and how do we know?".</summary>
public interface IVerificationTraceService
{
    /// <summary>
    /// Traces <paramref name="requirementId"/> through every artefact
    /// verifying it.
    /// </summary>
    /// <remarks>
    /// A requirement with no artefacts comes back
    /// <see cref="VerificationStanding.NotPerformed"/> with a concern
    /// saying so — never "passed" and never an empty clean result.
    /// </remarks>
    /// <param name="requirementId">The requirement to trace.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="ArgumentException"><paramref name="requirementId"/> is empty.</exception>
    Task<RequirementVerificationTrace> TraceAsync(Guid requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Traces every requirement in <paramref name="requirementIds"/>,
    /// least-verified first.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="requirementIds"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<RequirementVerificationTrace>> TraceAllAsync(
        IEnumerable<Guid> requirementIds,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IVerificationTraceService"/> implementation.</summary>
/// <remarks>
/// <para>
/// Read-only and deterministic. It reports what the artefacts say and
/// changes nothing — in particular it never marks a requirement verified,
/// which is what recording an evidenced result does.
/// </para>
/// <para>
/// The concerns are the useful output. "Verified" with no concerns and
/// "verified, but on the asserting party's own material, against a
/// requirement revision nobody pinned" are different situations, and only
/// the second is common.
/// </para>
/// </remarks>
public sealed class VerificationTraceService : IVerificationTraceService
{
    private readonly IVerificationArtefactCatalog _artefacts;

    /// <summary>Initialises a new instance of the <see cref="VerificationTraceService"/> class.</summary>
    /// <param name="artefacts">The artefact library to trace through.</param>
    /// <exception cref="ArgumentNullException"><paramref name="artefacts"/> is <see langword="null"/>.</exception>
    public VerificationTraceService(IVerificationArtefactCatalog artefacts)
    {
        ArgumentNullException.ThrowIfNull(artefacts);

        _artefacts = artefacts;
    }

    /// <inheritdoc />
    public async Task<RequirementVerificationTrace> TraceAsync(Guid requirementId, CancellationToken cancellationToken = default)
    {
        if (requirementId == Guid.Empty)
            throw new ArgumentException("A trace must name the requirement it traces.", nameof(requirementId));

        var records = await _artefacts.FindForRequirementAsync(requirementId, cancellationToken).ConfigureAwait(false);

        var artefacts = records
            .Select(r => r.Definition)
            .OrderBy(a => VerificationStandings.Rank(a.Standing))
            .ThenBy(a => a.Reference, StringComparer.Ordinal)
            .ToList();

        var standing = VerificationStandings.Weakest(artefacts.Select(a => a.Standing));

        return new RequirementVerificationTrace(
            requirementId,
            artefacts.Select(a => a.Requirement.RequirementIdentifier).FirstOrDefault(i => i is not null),
            standing,
            artefacts,
            Concerns(requirementId, artefacts, standing));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RequirementVerificationTrace>> TraceAllAsync(
        IEnumerable<Guid> requirementIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirementIds);

        var traces = new List<RequirementVerificationTrace>();

        foreach (var requirementId in requirementIds.Distinct())
            traces.Add(await TraceAsync(requirementId, cancellationToken).ConfigureAwait(false));

        return traces
            .OrderBy(t => VerificationStandings.Rank(t.Standing))
            .ThenBy(t => t.RequirementIdentifier ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(t => t.RequirementId)
            .ToList();
    }

    private static IReadOnlyList<string> Concerns(
        Guid requirementId,
        IReadOnlyList<VerificationArtefact> artefacts,
        VerificationStanding standing)
    {
        var concerns = new List<string>();

        if (artefacts.Count == 0)
        {
            concerns.Add($"No verification of any kind is planned against requirement '{requirementId}'.");
            return concerns;
        }

        foreach (var artefact in artefacts.Where(a => a.IsUnsupportedPass))
            concerns.Add($"'{artefact.Reference}' records a pass with no locatable evidence behind it.");

        foreach (var artefact in artefacts.Where(a => a.Standing == VerificationStanding.Failed))
            concerns.Add($"'{artefact.Reference}' failed: {artefact.Result!.Summary}");

        foreach (var artefact in artefacts.Where(a => a.Standing == VerificationStanding.Inconclusive))
            concerns.Add($"'{artefact.Reference}' was performed and did not settle the question.");

        foreach (var artefact in artefacts.Where(a => a.Standing == VerificationStanding.NotPerformed))
            concerns.Add($"'{artefact.Reference}' is planned and has not been performed.");

        foreach (var artefact in artefacts.Where(a => a.IsDemonstrated && !a.HasIndependentEvidence))
            concerns.Add($"'{artefact.Reference}' passes on internal material alone, with nothing independent behind it.");

        foreach (var artefact in artefacts.Where(a => !a.Requirement.IsPinnedToRevision))
            concerns.Add($"'{artefact.Reference}' did not record which revision of the requirement it verified.");

        if (standing == VerificationStanding.NotApplicable && artefacts.All(a => a.Standing == VerificationStanding.NotApplicable))
            concerns.Add(
                $"Every artefact against requirement '{requirementId}' declares it inapplicable. "
                + "Worth confirming the requirement should be there at all.");

        return concerns;
    }
}
