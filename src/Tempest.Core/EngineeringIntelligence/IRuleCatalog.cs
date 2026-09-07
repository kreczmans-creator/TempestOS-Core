using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// The authoritative library of engineering rules (`P02`).
/// </summary>
/// <remarks>
/// <para>
/// Register, retrieve, revise, govern, read history and supersede all come
/// from <see cref="IReferenceDataCatalog{TDefinition}"/>, shared with the
/// seven `P01` reference libraries. That is the substance of `ADR-0128`: a
/// rule is an authored, sourced, reviewed, revisioned record, which is
/// exactly what that catalogue governs, so `P02` grows no lifecycle of its
/// own.
/// </para>
/// <para>
/// <b>What this contract deliberately does not offer.</b> No execution —
/// <see cref="RuleEngine"/> is a pure function and takes a rule, not a
/// catalogue. No approval, no certification, no autonomous judgement. And
/// no general search: the query below filters a rule library by the
/// dimensions an engineer actually looks along.
/// </para>
/// </remarks>
public interface IRuleCatalog : IReferenceDataCatalog<RuleDefinition>
{
    /// <summary>Returns the rule registered under <paramref name="code"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="code"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<RuleDefinition>?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Every registered rule matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<RuleDefinition>>> SearchAsync(RuleQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every <b>released</b> rule that applies to <paramref name="subject"/>,
    /// in ascending record-Id order — the set an assessment actually runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Released only, and deliberately. A Draft or Checked rule is
    /// engineering guidance nobody has finished reviewing, and running it
    /// as part of an assessment would let unvalidated guidance reach an
    /// engineering conclusion wearing the same clothes as validated
    /// guidance. This mirrors `A6`'s released-constant seam exactly.
    /// </para>
    /// <para>
    /// A rule whose applicability cannot be decided for the subject is
    /// <b>included</b>: it will report
    /// <see cref="AssessmentOutcome.Indeterminate"/> when evaluated, which
    /// is the honest result. Excluding it here would make it vanish from
    /// the assessment precisely when the subject's data is weakest.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<RuleDefinition>>> FindReleasedApplicableAsync(
        IAssessmentSubject subject,
        CancellationToken cancellationToken = default);
}
