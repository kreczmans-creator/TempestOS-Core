namespace Tempest.App.Workspace;

/// <summary>
/// One KPI card on the Engineering Cockpit (`WP8.0C Engineering Cockpit
/// Specification.md` §2, Engineering Health Summary). <see cref="IsPlaceholder"/>
/// is always <see langword="true"/> today — no Requirements, Verification,
/// or Calculation service is wired to the Workspace yet (`WP 8.1C`'s own
/// explicit scope boundary) — carried on the type itself so a future,
/// real KPI can be told apart from today's fixed sample value without
/// inspecting the value's own text.
/// </summary>
/// <param name="Label">The KPI's own short name — for example "Requirements".</param>
/// <param name="Value">The KPI's own display value.</param>
/// <param name="IsPlaceholder">Whether this value is representative sample data, not a live read.</param>
/// <param name="PercentValue">
/// The KPI's own coverage percentage, `0`-`100`, when this KPI genuinely
/// represents a coverage ratio (`WP 10.5C`, "coloured health indicators,
/// progress bars... verification coverage, requirements coverage") —
/// <see langword="null"/> for every KPI that is not a percentage (a raw
/// count, a placeholder). A trailing, defaulted, additive parameter —
/// every pre-existing call site compiles unchanged; the three real
/// Coverage KPIs (Requirements/Verification/Calculations) were
/// individually revisited to pass the identical numerator/denominator
/// <see cref="EngineeringCockpit"/> already computes for its own
/// <c>FormatCoverage</c> display string — never a second, independent
/// computation that could drift from the text it accompanies.
/// </param>
internal sealed record CockpitKpiCard(string Label, string Value, bool IsPlaceholder, int? PercentValue = null);
