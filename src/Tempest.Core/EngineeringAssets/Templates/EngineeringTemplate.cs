using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringAssets.Templates;

/// <summary>What kind of engineering work a template structures.</summary>
/// <remarks>
/// The kind decides what a caller can do with the template, so it is a
/// closed vocabulary rather than a label. A calculation template and a
/// review template are not interchangeable even though both are
/// sections and fields.
/// </remarks>
public enum TemplateKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>The structure of a calculation — its inputs, method and outputs.</summary>
    Calculation,

    /// <summary>The structure of a design activity.</summary>
    Design,

    /// <summary>The structure of an engineering report.</summary>
    Report,

    /// <summary>The structure of a requirements set.</summary>
    Requirements,

    /// <summary>The structure of a design review.</summary>
    Review,

    /// <summary>The structure of a verification activity.</summary>
    Verification,

    /// <summary>The structure of a test plan or procedure.</summary>
    TestPlan,

    /// <summary>The structure of a specification.</summary>
    Specification,

    /// <summary>Something else.</summary>
    Other
}

/// <summary>What a template section expects to be filled in with.</summary>
public enum TemplateFieldKind
{
    /// <summary>Free text.</summary>
    Text,

    /// <summary>A number with no dimension.</summary>
    Number,

    /// <summary>A dimensioned engineering quantity — a value and a unit.</summary>
    Quantity,

    /// <summary>A date.</summary>
    Date,

    /// <summary>Yes or no.</summary>
    Boolean,

    /// <summary>A choice from a stated list.</summary>
    Choice,

    /// <summary>A reference to a governed record.</summary>
    RecordReference,

    /// <summary>A reference to a document.</summary>
    DocumentReference,

    /// <summary>A principal — an engineer, a reviewer.</summary>
    Principal
}

/// <summary>
/// One thing a template asks to be filled in.
/// </summary>
/// <remarks>
/// A template field says what is wanted and never holds an answer. The
/// answer lives in whatever the template was used to produce, which is
/// why <see cref="EngineeringTemplate"/> is reference data and the thing
/// made from it is not.
/// </remarks>
/// <param name="Reference">The field's own identifier within the template. Required.</param>
/// <param name="Label">What to call it. Required.</param>
/// <param name="Kind">What sort of answer is expected.</param>
/// <param name="IsRequired">Whether the template treats an unanswered field as incomplete.</param>
/// <param name="Guidance">What the author should put here. <see langword="null"/> where nothing was written.</param>
/// <param name="Choices">The permitted answers, where <paramref name="Kind"/> is <see cref="TemplateFieldKind.Choice"/>. Never <see langword="null"/>.</param>
/// <param name="ExpectedDimension">The physical dimension expected, where the kind is <see cref="TemplateFieldKind.Quantity"/>. <see langword="null"/> otherwise.</param>
public sealed record TemplateField(
    string Reference,
    string Label,
    TemplateFieldKind Kind = TemplateFieldKind.Text,
    bool IsRequired = false,
    string? Guidance = null,
    IReadOnlyList<string>? Choices = null,
    string? ExpectedDimension = null)
{
    /// <summary>The field's own identifier within the template.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A template field must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What to call it.</summary>
    public string Label { get; } = string.IsNullOrWhiteSpace(Label)
        ? throw new ArgumentException("A template field must have a label.", nameof(Label))
        : Label.Trim();

    /// <summary>The permitted answers.</summary>
    public IReadOnlyList<string> Choices { get; init; } = Choices ?? [];

    /// <summary>Whether the field offers a closed set of answers but names none.</summary>
    public bool IsUnusableChoice => Kind == TemplateFieldKind.Choice && Choices.Count == 0;
}

/// <summary>One part of a template's structure.</summary>
/// <param name="Reference">The section's own identifier within the template. Required.</param>
/// <param name="Title">What to call it. Required.</param>
/// <param name="Purpose">What belongs in it. <see langword="null"/> where nothing was written.</param>
/// <param name="IsMandatory">Whether the template treats an omitted section as incomplete.</param>
/// <param name="Fields">What the section asks for. Never <see langword="null"/>.</param>
/// <param name="Subsections">Nested structure. Never <see langword="null"/>.</param>
public sealed record TemplateSection(
    string Reference,
    string Title,
    string? Purpose = null,
    bool IsMandatory = false,
    IReadOnlyList<TemplateField>? Fields = null,
    IReadOnlyList<TemplateSection>? Subsections = null)
{
    /// <summary>The section's own identifier within the template.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A template section must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What to call it.</summary>
    public string Title { get; } = string.IsNullOrWhiteSpace(Title)
        ? throw new ArgumentException("A template section must have a title.", nameof(Title))
        : Title.Trim();

    /// <summary>What the section asks for.</summary>
    public IReadOnlyList<TemplateField> Fields { get; init; } = Fields ?? [];

    /// <summary>Nested structure.</summary>
    public IReadOnlyList<TemplateSection> Subsections { get; init; } = Subsections ?? [];

    /// <summary>This section and every section beneath it, depth first.</summary>
    public IEnumerable<TemplateSection> SelfAndDescendants()
    {
        yield return this;

        foreach (var descendant in Subsections.SelectMany(s => s.SelfAndDescendants()))
            yield return descendant;
    }

    /// <summary>Whether the section asks for nothing and contains nothing.</summary>
    public bool IsEmpty => Fields.Count == 0 && Subsections.Count == 0;
}

/// <summary>
/// A reusable structure for engineering work.
/// </summary>
/// <remarks>
/// <para>
/// A template is <b>structure, not content</b>. It says what an
/// engineering artefact should contain and never holds an answer. The
/// platform ships the mechanism; the templates an organisation actually
/// uses are its own, and `P05` deliberately ships none (`ADR-0136`).
/// </para>
/// <para>
/// <b>Using a template pins it.</b> A caller records
/// <see cref="TemplateUsage"/> naming the template and the exact revision
/// it worked from, so revising the template to revision 4 leaves work
/// done against revision 3 saying revision 3. Nothing in `P05` rewrites a
/// historical usage, and <see cref="ITemplateCatalog.FindUsagesOfAsync"/>
/// exists so somebody can ask which work is now behind.
/// </para>
/// </remarks>
public sealed record EngineeringTemplate
{
    /// <summary>The reference the template is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What to call it. Required.</summary>
    public required string Name { get; init; }

    /// <summary>What it is for. Required.</summary>
    public required string Purpose { get; init; }

    /// <summary>What kind of engineering work it structures.</summary>
    public TemplateKind Kind { get; init; } = TemplateKind.Unspecified;

    /// <summary>The structure itself. Never <see langword="null"/>.</summary>
    public IReadOnlyList<TemplateSection> Sections { get; init; } = [];

    /// <summary>Where and when it applies.</summary>
    public AssetApplicability Applicability { get; init; } = AssetApplicability.Unrestricted;

    /// <summary>Who owns it, who wrote it, who reviewed it.</summary>
    public AssetGovernanceFacts Governance { get; init; } = new();

    /// <summary>The template this one replaces, by reference. <see langword="null"/> where it replaces none.</summary>
    public string? SupersedesReference { get; init; }

    /// <summary>Guidance for whoever fills it in. <see langword="null"/> where nothing was written.</summary>
    public string? Instructions { get; init; }

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Every section in the template, nesting flattened, depth first.</summary>
    public IEnumerable<TemplateSection> AllSections => Sections.SelectMany(s => s.SelfAndDescendants());

    /// <summary>Every field the template asks for, across every section.</summary>
    public IEnumerable<TemplateField> AllFields => AllSections.SelectMany(s => s.Fields);

    /// <summary>The fields the template treats as required.</summary>
    public IEnumerable<TemplateField> RequiredFields => AllFields.Where(f => f.IsRequired);

    /// <summary>Whether the template asks for anything at all.</summary>
    public bool IsStructured => Sections.Count > 0 && AllFields.Any();

    /// <summary>The section carrying <paramref name="reference"/>, or <see langword="null"/> where none does.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public TemplateSection? FindSection(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return AllSections.FirstOrDefault(s => string.Equals(s.Reference, reference.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The field carrying <paramref name="reference"/>, or <see langword="null"/> where none does.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public TemplateField? FindField(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return AllFields.FirstOrDefault(f => string.Equals(f.Reference, reference.Trim(), StringComparison.OrdinalIgnoreCase));
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

/// <summary>
/// A record that some piece of work was produced from a template, at a
/// stated revision.
/// </summary>
/// <remarks>
/// <b>This is what stops a template revision rewriting history.</b> The
/// usage names the revision that was actually worked from. Revising the
/// template afterwards changes what new work will use and leaves this
/// record saying exactly what it said — which is the whole point, and why
/// <see cref="TemplatePin"/> is a <see cref="ReferencePin"/> rather than
/// a bare reference.
/// </remarks>
/// <param name="TemplatePin">The template and the exact revision worked from. Required.</param>
/// <param name="UsedForDescription">What was produced from it. Required.</param>
/// <param name="UsedByPrincipalId">Who used it. <see langword="null"/> where unrecorded.</param>
/// <param name="UsedOn">When. <see langword="null"/> where unrecorded.</param>
/// <param name="ProducedDocumentId">The document produced, where one was. <see langword="null"/> otherwise.</param>
public sealed record TemplateUsage(
    ReferencePin TemplatePin,
    string UsedForDescription,
    string? UsedByPrincipalId = null,
    DateOnly? UsedOn = null,
    Guid? ProducedDocumentId = null)
{
    /// <summary>The template and the exact revision worked from.</summary>
    public ReferencePin TemplatePin { get; } = TemplatePin ?? throw new ArgumentNullException(nameof(TemplatePin));

    /// <summary>What was produced from it.</summary>
    public string UsedForDescription { get; } = string.IsNullOrWhiteSpace(UsedForDescription)
        ? throw new ArgumentException("A template usage must say what was produced from the template.", nameof(UsedForDescription))
        : UsedForDescription.Trim();

    /// <summary>
    /// Whether the usage is behind <paramref name="currentRevision"/> of
    /// the same template.
    /// </summary>
    /// <remarks>
    /// Reports; it does not migrate. Work done against an earlier
    /// revision is not thereby wrong, and deciding whether to redo it is
    /// an engineering judgement.
    /// </remarks>
    public bool IsBehind(int currentRevision) => TemplatePin.RevisionNumber < currentRevision;
}
