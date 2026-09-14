
namespace Tempest.Core.Knowledge.Academy;

/// <summary>Where a node sits in the Academy's hierarchy.</summary>
/// <remarks>
/// <para>
/// One node type with a kind, rather than five types, because the
/// hierarchy is genuinely recursive: a module contains lessons, a lesson
/// contains concepts, and the containment rule is the same at every
/// level. The kinds order strictly, and
/// <see cref="AcademyNodeKinds.CanContain"/> is what stops a subject
/// appearing inside a lesson.
/// </para>
/// <para>
/// This is the exception `ADR-0141` records to the "distinct types for
/// distinct concepts" rule: these five are not distinct concepts, they
/// are one concept at five depths.
/// </para>
/// </remarks>
public enum AcademyNodeKind
{
    /// <summary>A broad field — mechanical engineering, materials.</summary>
    Subject,

    /// <summary>A discipline within a subject.</summary>
    Discipline,

    /// <summary>A unit of study.</summary>
    Module,

    /// <summary>A single sitting's worth.</summary>
    Lesson,

    /// <summary>One idea.</summary>
    Concept
}

/// <summary>What the Academy hierarchy permits.</summary>
public static class AcademyNodeKinds
{
    /// <summary>The kinds, broadest first.</summary>
    public static IReadOnlyList<AcademyNodeKind> BroadestFirst { get; } =
    [
        AcademyNodeKind.Subject,
        AcademyNodeKind.Discipline,
        AcademyNodeKind.Module,
        AcademyNodeKind.Lesson,
        AcademyNodeKind.Concept,
    ];

    /// <summary>How deep a kind sits. Lower is broader.</summary>
    public static int Depth(AcademyNodeKind kind) => kind switch
    {
        AcademyNodeKind.Subject => 0,
        AcademyNodeKind.Discipline => 1,
        AcademyNodeKind.Module => 2,
        AcademyNodeKind.Lesson => 3,
        AcademyNodeKind.Concept => 4,
        _ => 0
    };

    /// <summary>
    /// Whether <paramref name="parent"/> may contain
    /// <paramref name="child"/>.
    /// </summary>
    /// <remarks>
    /// A node may contain anything strictly narrower than itself, not
    /// merely the next level down. A module holding concepts directly,
    /// without an intervening lesson, is a reasonable curriculum and the
    /// model does not forbid it. What it forbids is a lesson containing a
    /// subject.
    /// </remarks>
    public static bool CanContain(AcademyNodeKind parent, AcademyNodeKind child) => Depth(child) > Depth(parent);

    /// <summary>Whether the kind is one a learner actually sits down to.</summary>
    public static bool IsDeliverable(AcademyNodeKind kind) =>
        kind is AcademyNodeKind.Lesson or AcademyNodeKind.Concept;
}

/// <summary>What a learner should be able to do afterwards.</summary>
/// <remarks>
/// A learning outcome is a statement about the <em>learner</em>, not
/// about the content. "Covers beam bending" is a syllabus line;
/// "can calculate the maximum bending stress in a simply supported beam"
/// is an outcome, and only the second can be assessed.
/// </remarks>
/// <param name="Reference">The outcome's own identifier within the node. Required.</param>
/// <param name="Statement">What the learner should be able to do. Required.</param>
/// <param name="AssessedBy">The exercise or assessment references that test it. Never <see langword="null"/>.</param>
public sealed record LearningOutcome(string Reference, string Statement, IReadOnlyList<string>? AssessedBy = null)
{
    /// <summary>The outcome's own identifier within the node.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A learning outcome must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What the learner should be able to do.</summary>
    public string Statement { get; } = string.IsNullOrWhiteSpace(Statement)
        ? throw new ArgumentException("A learning outcome must say what the learner should be able to do.", nameof(Statement))
        : Statement.Trim();

    /// <summary>The exercises or assessments that test it.</summary>
    public IReadOnlyList<string> AssessedBy { get; init; } = AssessedBy ?? [];

    /// <summary>Whether anything actually tests the outcome.</summary>
    public bool IsAssessed => AssessedBy.Count > 0;
}

/// <summary>What sort of activity an exercise is.</summary>
public enum AcademyActivityKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Something to read.</summary>
    Reading,

    /// <summary>A problem to work through.</summary>
    Problem,

    /// <summary>A worked example to follow.</summary>
    WorkedExample,

    /// <summary>A calculation to perform.</summary>
    Calculation,

    /// <summary>A design task.</summary>
    DesignTask,

    /// <summary>A discussion or "what if".</summary>
    Discussion,

    /// <summary>An assessment of what has been learned.</summary>
    Assessment
}

/// <summary>Something a learner does.</summary>
/// <remarks>
/// An activity may point at content held elsewhere in `P06` — an `F5`
/// worked example, an `F3` challenge — rather than restating it. That is
/// how a worked example becomes an Academy artefact without being copied
/// into one.
/// </remarks>
/// <param name="Reference">The activity's own identifier within the node. Required.</param>
/// <param name="Title">What to call it. Required.</param>
/// <param name="Kind">What sort of activity it is.</param>
/// <param name="OutcomeReferences">The learning outcomes it serves. Never <see langword="null"/>.</param>
/// <param name="WorkedExampleReference">The `F5` worked example it uses. <see langword="null"/> where it uses none.</param>
/// <param name="ChallengeReference">The `F3` challenge it uses. <see langword="null"/> where it uses none.</param>
/// <param name="EstimatedMinutes">Roughly how long. <see langword="null"/> where nobody said.</param>
public sealed record AcademyActivity(
    string Reference,
    string Title,
    AcademyActivityKind Kind = AcademyActivityKind.Unspecified,
    IReadOnlyList<string>? OutcomeReferences = null,
    string? WorkedExampleReference = null,
    string? ChallengeReference = null,
    int? EstimatedMinutes = null)
{
    /// <summary>The activity's own identifier within the node.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("An academy activity must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What to call it.</summary>
    public string Title { get; } = string.IsNullOrWhiteSpace(Title)
        ? throw new ArgumentException("An academy activity must have a title.", nameof(Title))
        : Title.Trim();

    /// <summary>The learning outcomes it serves.</summary>
    public IReadOnlyList<string> OutcomeReferences { get; init; } = OutcomeReferences ?? [];

    /// <summary>Whether the activity tests what was learned rather than teaching it.</summary>
    public bool IsAssessment => Kind == AcademyActivityKind.Assessment;

    /// <summary>Whether the activity draws on content held elsewhere in `P06`.</summary>
    public bool DrawsOnKnowledgeLibrary =>
        !string.IsNullOrWhiteSpace(WorkedExampleReference) || !string.IsNullOrWhiteSpace(ChallengeReference);
}

/// <summary>
/// One node of the Academy — a subject, discipline, module, lesson or
/// concept.
/// </summary>
/// <remarks>
/// <para>
/// <b>Structure, not curriculum.</b> `F2` provides the shape an
/// engineering Academy takes. It ships no lesson, no module and no
/// teaching content, and inventing engineering instruction to fill it
/// would produce material that looks authoritative and was written by
/// nobody competent (`ADR-0141`).
/// </para>
/// <para>
/// Distinct from the repository's own `docs/academy/`, which is developer
/// documentation <em>about TempestOS</em>. This is a structure for
/// teaching <em>engineering</em>, held in the platform for its users.
/// </para>
/// <para>
/// Nodes reference their parent rather than nesting, so the hierarchy is
/// a graph in the library rather than a tree in one record. A module can
/// then be revised without rewriting the subject above it.
/// </para>
/// </remarks>
public sealed record AcademyNode
{
    /// <summary>The reference the node is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What to call it. Required.</summary>
    public required string Title { get; init; }

    /// <summary>Where it sits in the hierarchy.</summary>
    public AcademyNodeKind Kind { get; init; } = AcademyNodeKind.Concept;

    /// <summary>The node above it, by reference. <see langword="null"/> at the top.</summary>
    public string? ParentReference { get; init; }

    /// <summary>What it is about. <see langword="null"/> where nothing was written.</summary>
    public string? Summary { get; init; }

    /// <summary>What a learner should be able to do afterwards. Never <see langword="null"/>.</summary>
    public IReadOnlyList<LearningOutcome> Outcomes { get; init; } = [];

    /// <summary>What a learner does. Never <see langword="null"/>.</summary>
    public IReadOnlyList<AcademyActivity> Activities { get; init; } = [];

    /// <summary>The nodes a learner should have done first, by reference. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> PrerequisiteReferences { get; init; } = [];

    /// <summary>Where to read more. Never <see langword="null"/>.</summary>
    public IReadOnlyList<KnowledgeCitation> FurtherReading { get; init; } = [];

    /// <summary>Where it applies and who it is for.</summary>
    public KnowledgeApplicability Applicability { get; init; } = KnowledgeApplicability.Unrestricted;

    /// <summary>Where it came from and who has checked it.</summary>
    public KnowledgeProvenance Provenance { get; init; } = new();

    /// <summary>Roughly how long the node takes, where its activities say. <see langword="null"/> where none do.</summary>
    public int? EstimatedMinutes =>
        Activities.Select(a => a.EstimatedMinutes).OfType<int>().DefaultIfEmpty(0).Sum() is var total && total > 0
            ? total
            : null;

    /// <summary>Whether a learner actually sits down to this node.</summary>
    public bool IsDeliverable => AcademyNodeKinds.IsDeliverable(Kind);

    /// <summary>The activities that test what was learned.</summary>
    public IReadOnlyList<AcademyActivity> Assessments => Activities.Where(a => a.IsAssessment).ToList();

    /// <summary>Outcomes nothing tests.</summary>
    /// <remarks>
    /// The gap that matters in a curriculum: an outcome nothing assesses
    /// is a promise nobody checks.
    /// </remarks>
    public IReadOnlyList<LearningOutcome> UnassessedOutcomes =>
        Outcomes.Where(o => !o.IsAssessed && !Activities.Any(a => a.OutcomeReferences.Contains(o.Reference, StringComparer.OrdinalIgnoreCase))).ToList();

    /// <summary>Whether the node may contain <paramref name="child"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="child"/> is <see langword="null"/>.</exception>
    public bool CanContain(AcademyNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        return AcademyNodeKinds.CanContain(Kind, child.Kind);
    }

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
