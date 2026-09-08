namespace Tempest.App.Workspace;

/// <summary>
/// Shared, stateless coverage-formatting helpers used by more than one
/// Engineering Cockpit per-discipline read-model collaborator
/// (<c>RequirementsCockpitReadModel</c>/<c>CalculationsCockpitReadModel</c>/
/// <c>VerificationCockpitReadModel</c>) — extracted, `WP 12.0B`
/// (`ADR-0103`), from <see cref="EngineeringCockpit"/>'s own previously
/// `private static` <c>FormatCoverage</c>/<c>PercentOf</c> pair, so each
/// collaborator can reuse the identical, unmodified formatting rather
/// than duplicating it three times.
/// </summary>
/// <remarks>
/// Not itself an <c>ADR-0103</c> "collaborator" — it holds no state, is
/// never constructed, and belongs to no one composition root; a plain,
/// stateless static utility.
/// </remarks>
internal static class CockpitFormatting
{
    /// <summary>
    /// Formats a coverage fraction as display text — <c>"— (no
    /// requirements yet)"</c> for a zero denominator (a fixed string,
    /// unchanged from <see cref="EngineeringCockpit"/>'s own original
    /// text, even where reused by a non-Requirements discipline — see
    /// <see cref="EngineeringCockpit"/>'s own prior remarks on this exact,
    /// pre-existing, disclosed minor inaccuracy, not introduced or
    /// corrected by this move), else <c>"{percent}% ({numerator}/{denominator})"</c>.
    /// </summary>
    public static string FormatCoverage(int numerator, int denominator) =>
        denominator == 0 ? "— (no requirements yet)" : $"{numerator * 100 / denominator}% ({numerator}/{denominator})";

    /// <summary>
    /// The numeric twin of <see cref="FormatCoverage"/> — the identical
    /// numerator/denominator as a real <c>0</c>-<c>100</c> percentage, or
    /// <see langword="null"/> for the identical zero-denominator case.
    /// </summary>
    public static int? PercentOf(int numerator, int denominator) =>
        denominator == 0 ? null : numerator * 100 / denominator;
}
