using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// One value read from a subject, together with why it is absent when it
/// is.
/// </summary>
/// <remarks>
/// The whole reason P02 can tell "this material has no yield point" from
/// "nobody recorded a yield strength". Reuses `P01`'s own
/// <see cref="ReferencePropertyAvailability"/> rather than declaring a
/// third enum meaning the same three things, and the availability itself
/// comes from `P01`'s family-traits tables — the subject adapter asks
/// <c>MaterialFamilyTraits</c>, <c>FastenerFamilyTraits</c> and their
/// siblings, so applicability is decided once, in the library that owns
/// the taxonomy.
/// </remarks>
/// <param name="Availability">Whether a value is present, and if not, why not.</param>
/// <param name="Value">The value. <see langword="null"/> unless <paramref name="Availability"/> is <see cref="ReferencePropertyAvailability.Recorded"/>.</param>
public sealed record SubjectQuantity(ReferencePropertyAvailability Availability, ReferenceQuantityValue? Value = null)
{
    /// <summary>The property applies to this subject but nobody has recorded a value.</summary>
    public static SubjectQuantity NotRecorded { get; } = new(ReferencePropertyAvailability.NotRecorded);

    /// <summary>The property does not apply to this subject at all. Nothing is missing.</summary>
    public static SubjectQuantity NotApplicable { get; } = new(ReferencePropertyAvailability.NotApplicable);

    /// <summary>A recorded value.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static SubjectQuantity Recorded(ReferenceQuantityValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new SubjectQuantity(ReferencePropertyAvailability.Recorded, value);
    }

    /// <summary>The assessment outcome an absent value produces. Never <see cref="AssessmentOutcome.Pass"/>.</summary>
    /// <exception cref="InvalidOperationException">The value is recorded, so no absence outcome exists — evaluate it instead.</exception>
    public AssessmentOutcome AbsenceOutcome => Availability switch
    {
        ReferencePropertyAvailability.NotRecorded => AssessmentOutcome.NotRecorded,
        ReferencePropertyAvailability.NotApplicable => AssessmentOutcome.NotApplicable,
        _ => throw new InvalidOperationException("The value is recorded; there is no absence to report."),
    };
}

/// <summary>One text or classification value read from a subject.</summary>
/// <param name="Availability">Whether a value is present, and if not, why not.</param>
/// <param name="Value">The value. <see langword="null"/> unless <paramref name="Availability"/> is <see cref="ReferencePropertyAvailability.Recorded"/>.</param>
public sealed record SubjectText(ReferencePropertyAvailability Availability, string? Value = null)
{
    /// <summary>The attribute applies to this subject but nobody has recorded a value.</summary>
    public static SubjectText NotRecorded { get; } = new(ReferencePropertyAvailability.NotRecorded);

    /// <summary>The attribute does not apply to this subject at all.</summary>
    public static SubjectText NotApplicable { get; } = new(ReferencePropertyAvailability.NotApplicable);

    /// <summary>A recorded value, or <see cref="NotRecorded"/> where <paramref name="value"/> is absent or blank.</summary>
    public static SubjectText Recorded(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NotRecorded : new SubjectText(ReferencePropertyAvailability.Recorded, value);

    /// <summary>The assessment outcome an absent value produces. Never <see cref="AssessmentOutcome.Pass"/>.</summary>
    /// <exception cref="InvalidOperationException">The value is recorded, so no absence outcome exists.</exception>
    public AssessmentOutcome AbsenceOutcome => Availability switch
    {
        ReferencePropertyAvailability.NotRecorded => AssessmentOutcome.NotRecorded,
        ReferencePropertyAvailability.NotApplicable => AssessmentOutcome.NotApplicable,
        _ => throw new InvalidOperationException("The value is recorded; there is no absence to report."),
    };
}

/// <summary>
/// The thing a rule or criterion is evaluated against — a material, a
/// fastener, a process, a component, presented in the one shape a rule
/// engine can read.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a string bag, and not the primary API.</b> Every P02 capability
/// takes typed reference records at its own boundary
/// (<c>IReferenceRecord&lt;MaterialDefinition&gt;</c>, and so on) and
/// constructs a subject from them. This interface exists so that one
/// deterministic rule engine can serve seven reference libraries without
/// each library growing its own engine, and it is deliberately narrow:
/// two typed accessors, an identity, a pin and a family.
/// </para>
/// <para>
/// <b>The adapter owns the applicability question.</b> An implementation
/// answers <see cref="GetQuantity"/> with
/// <see cref="ReferencePropertyAvailability.NotApplicable"/> only when its
/// own library's traits table says the property cannot exist for this
/// subject's family, and with
/// <see cref="ReferencePropertyAvailability.NotRecorded"/> otherwise. An
/// implementation that returns <c>NotApplicable</c> for a property nobody
/// happened to record would make a rule silently skip a real gap, which
/// is the failure mode this whole model exists to prevent.
/// </para>
/// </remarks>
public interface IAssessmentSubject
{
    /// <summary>What kind of thing this is, as the owning library names it (e.g. <c>"Material"</c>, <c>"Fastener"</c>). Used for rule applicability.</summary>
    string SubjectKind { get; }

    /// <summary>The subject's own identity, for the record.</summary>
    string SubjectId { get; }

    /// <summary>A human-readable name for the subject, for explanations.</summary>
    string DisplayName { get; }

    /// <summary>
    /// The subject's own family within its library, where it has one —
    /// the value rule applicability is matched against.
    /// <see langword="null"/> where the library has no family concept or
    /// none is recorded.
    /// </summary>
    string? Family { get; }

    /// <summary>
    /// Whether the subject's own library can say which properties apply to
    /// this family. <see langword="false"/> for an unclassified family, in
    /// which case a <c>NotApplicable</c> answer must never be produced —
    /// "not known to apply" is not "known not to apply".
    /// </summary>
    bool IsApplicabilityKnown { get; }

    /// <summary>The pinned reference-data record this subject was read from, where it is one. <see langword="null"/> for a subject constructed from something other than a reference record.</summary>
    ReferencePin? Pin { get; }

    /// <summary>Reads one dimensioned property, reporting why it is absent when it is.</summary>
    /// <exception cref="ArgumentException"><paramref name="propertyName"/> is null, empty, or whitespace.</exception>
    SubjectQuantity GetQuantity(string propertyName);

    /// <summary>Reads one text or classification attribute, reporting why it is absent when it is.</summary>
    /// <exception cref="ArgumentException"><paramref name="attributeName"/> is null, empty, or whitespace.</exception>
    SubjectText GetText(string attributeName);
}
