namespace Tempest.Workspace;

/// <summary>
/// One KPI card on the Engineering Cockpit (`WP8.0C Engineering Cockpit
/// Specification.md` §2, Engineering Health Summary) or the Home cockpit's
/// own five `WP 19.1B` cards (`ADR-0150`). <see cref="IsPlaceholder"/> is
/// <see langword="false"/> for every KPI shipped today — Requirements,
/// Verification, Calculations, Documentation and Manufacturing each
/// carry a real per-discipline read model (`WP 12.0B` onward), and
/// utilisation/margin/work-in-progress/days-sales-outstanding/calc
/// throughput are each a real read over `ADR-0150`'s own equations, an
/// honestly-empty state ("no time recorded", "unavailable") rendered as
/// a real, non-placeholder value rather than as this flag — carried on
/// the type itself so a genuine future placeholder (a KPI with no read
/// model behind it yet) can still be told apart from a live one without
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
public sealed record CockpitKpiCard(string Label, string Value, bool IsPlaceholder, int? PercentValue = null);
