namespace Tempest.Core.Knowledge.Prompts;

/// <summary>What a prompt is for.</summary>
public enum PromptPurpose
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Explaining something to somebody learning it.</summary>
    Explanation,

    /// <summary>Reviewing or critiquing engineering work.</summary>
    Review,

    /// <summary>Producing a first draft a person will then work on.</summary>
    Drafting,

    /// <summary>Pulling structured information out of a document.</summary>
    Extraction,

    /// <summary>Condensing something long.</summary>
    Summarisation,

    /// <summary>Generating candidate options for a person to judge.</summary>
    Ideation,

    /// <summary>Checking something against stated criteria.</summary>
    Checking,

    /// <summary>Something else.</summary>
    Other
}

/// <summary>Something a prompt expects to be given, or produces.</summary>
/// <param name="Reference">The slot's own identifier within the prompt. Required.</param>
/// <param name="Description">What it is. Required.</param>
/// <param name="IsRequired">Whether the prompt is unusable without it.</param>
/// <param name="Example">An illustrative value. <see langword="null"/> where none is given.</param>
public sealed record PromptSlot(string Reference, string Description, bool IsRequired = false, string? Example = null)
{
    /// <summary>The slot's own identifier within the prompt.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A prompt slot must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What it is.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A prompt slot must say what it is.", nameof(Description))
        : Description.Trim();
}

/// <summary>A limit on what the prompt may be used for, or may produce.</summary>
/// <remarks>
/// <see cref="IsSafetyConstraint"/> separates the constraints that exist
/// to keep the output useful from the ones that exist to stop it doing
/// harm. Both are recorded; only the second makes a prompt's absence of
/// constraints worth reporting.
/// </remarks>
/// <param name="Statement">The constraint. Required.</param>
/// <param name="IsSafetyConstraint">Whether it exists to prevent harm rather than to improve quality.</param>
/// <param name="Rationale">Why it is there. <see langword="null"/> where nothing was written.</param>
public sealed record PromptConstraint(string Statement, bool IsSafetyConstraint = false, string? Rationale = null)
{
    /// <summary>The constraint.</summary>
    public string Statement { get; } = string.IsNullOrWhiteSpace(Statement)
        ? throw new ArgumentException("A prompt constraint must state what it constrains.", nameof(Statement))
        : Statement.Trim();
}

/// <summary>
/// A reusable instruction, held as a governed knowledge asset.
/// </summary>
/// <remarks>
/// <para>
/// <b>TempestOS does not run this.</b> `P06` is the knowledge layer and
/// holds prompts the way it holds lessons: as content with an origin, a
/// review state and a lifecycle. There is no executor, no agent, no model
/// binding and no provider dependency anywhere in the programme, and a
/// reflection test enforces it (`ADR-0140`).
/// </para>
/// <para>
/// A prompt is knowledge about how to ask for something well. Recording
/// it lets an organisation improve its instructions the way it improves
/// its templates — by revising a governed record, with the earlier
/// version still readable.
/// </para>
/// </remarks>
public sealed record PromptRecord
{
    /// <summary>The reference the prompt is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What to call it. Required.</summary>
    public required string Name { get; init; }

    /// <summary>The instruction itself. Required.</summary>
    public required string Instruction { get; init; }

    /// <summary>What it is for.</summary>
    public PromptPurpose Purpose { get; init; } = PromptPurpose.Unspecified;

    /// <summary>What task it is meant to accomplish, in plain words. <see langword="null"/> where nothing was written.</summary>
    public string? TaskDescription { get; init; }

    /// <summary>What it expects to be given. Never <see langword="null"/>.</summary>
    public IReadOnlyList<PromptSlot> Inputs { get; init; } = [];

    /// <summary>What it is meant to produce. Never <see langword="null"/>.</summary>
    public IReadOnlyList<PromptSlot> ExpectedOutputs { get; init; } = [];

    /// <summary>What it may not be used for, and what it may not produce. Never <see langword="null"/>.</summary>
    public IReadOnlyList<PromptConstraint> Constraints { get; init; } = [];

    /// <summary>
    /// What a person must do with the output before it is relied on.
    /// </summary>
    /// <remarks>
    /// Required in substance. Every prompt in an engineering context
    /// produces something a person must check, and a prompt record that
    /// does not say what checking looks like is an invitation to skip it.
    /// </remarks>
    public string? HumanReviewGuidance { get; init; }

    /// <summary>Known ways this prompt goes wrong. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> KnownFailureModes { get; init; } = [];

    /// <summary>Where it applies and who it is for.</summary>
    public KnowledgeApplicability Applicability { get; init; } = KnowledgeApplicability.Unrestricted;

    /// <summary>Where it came from and who has checked it.</summary>
    public KnowledgeProvenance Provenance { get; init; } = new();

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The constraints that exist to prevent harm.</summary>
    public IReadOnlyList<PromptConstraint> SafetyConstraints =>
        Constraints.Where(c => c.IsSafetyConstraint).ToList();

    /// <summary>The inputs the prompt cannot be used without.</summary>
    public IReadOnlyList<PromptSlot> RequiredInputs => Inputs.Where(i => i.IsRequired).ToList();

    /// <summary>Whether the prompt says what a person must check before relying on the output.</summary>
    public bool StatesHumanReview => !string.IsNullOrWhiteSpace(HumanReviewGuidance);

    /// <summary>Whether the prompt says what it is meant to produce.</summary>
    public bool StatesExpectedOutput => ExpectedOutputs.Count > 0;

    /// <summary>
    /// Always <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// A property rather than a constant so it reads at every call site,
    /// and unconditional because there is no prompt whose output an
    /// engineer should act on unchecked. Mirrors `P02`'s
    /// <c>MaterialAssessment.RequiresHumanDecision</c> and `P03`'s
    /// <c>SourcingComparison.RequiresHumanDecision</c>.
    /// </remarks>
    public bool OutputRequiresHumanReview => true;

    /// <summary>The case-insensitive key <see cref="Reference"/> is indexed under.</summary>
    public string ReferenceKey => ReferenceKeyFor(Reference);

    /// <summary>The case-insensitive key <paramref name="reference"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public static string ReferenceKeyFor(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return reference.Trim().ToUpperInvariant();
    }
}
