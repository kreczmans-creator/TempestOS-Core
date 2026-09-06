namespace Tempest.Core.EngineeringIntelligence.TradeStudies;

/// <summary>
/// One of the things being compared.
/// </summary>
/// <remarks>
/// <para>
/// An option is not required to be a reference-data record. Comparing two
/// architectures, two suppliers or two programme approaches is a real
/// trade study, and the framework must not force those into a catalogue
/// they do not belong in. Where an option <i>is</i> a record — a material,
/// a bearing, a process — <see cref="SubjectId"/> ties it to that record
/// and the assessment pins the revision it read.
/// </para>
/// <para>
/// The option set is recorded with the study result rather than with the
/// study definition, because the same question is legitimately re-asked
/// against a different option set later, and doing so must not rewrite
/// the original.
/// </para>
/// </remarks>
/// <param name="Code">The study-local identifier a judgement and a decision refer to. Required.</param>
/// <param name="Name">What the option is called. Required.</param>
/// <param name="Description">What the option is, in enough detail for a later reader. <see langword="null"/> if the name suffices.</param>
/// <param name="SubjectId">The reference-data record this option is, where it is one. <see langword="null"/> where the option is described rather than catalogued.</param>
/// <param name="IsIncumbent">Whether this is the current or baseline solution the others are being weighed against.</param>
public sealed record TradeStudyOption(
    string Code,
    string Name,
    string? Description = null,
    string? SubjectId = null,
    bool IsIncumbent = false)
{
    /// <summary>The study-local identifier a judgement and a decision refer to.</summary>
    public string Code { get; } = string.IsNullOrWhiteSpace(Code)
        ? throw new ArgumentException("A trade-study option must have a code a judgement can refer to.", nameof(Code))
        : Code.Trim();

    /// <summary>What the option is called.</summary>
    public string Name { get; } = string.IsNullOrWhiteSpace(Name)
        ? throw new ArgumentException("A trade-study option must have a name.", nameof(Name))
        : Name.Trim();

    /// <summary>Whether the option is a reference-data record the framework can assess for itself.</summary>
    public bool IsCatalogued => !string.IsNullOrWhiteSpace(SubjectId);
}

/// <summary>
/// An option paired with the reference-data subject standing behind it, if
/// there is one.
/// </summary>
/// <remarks>
/// The pairing is supplied at assessment time rather than held on the
/// option, so that <see cref="TradeStudyOption"/> stays plain recorded data
/// and the live subject — which carries a revision and a pin — is read
/// once, at a known moment, by the service that records what it read.
/// </remarks>
/// <param name="Option">The option being offered. Required.</param>
/// <param name="Subject">The record the option is, for the considerations the framework can assess itself. <see langword="null"/> where the option is not catalogued, or where every consideration needs a person.</param>
public sealed record TradeStudyCandidate(TradeStudyOption Option, IAssessmentSubject? Subject = null)
{
    /// <summary>The option being offered.</summary>
    public TradeStudyOption Option { get; } = Option ?? throw new ArgumentNullException(nameof(Option));
}
