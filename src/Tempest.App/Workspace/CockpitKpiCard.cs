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
internal sealed record CockpitKpiCard(string Label, string Value, bool IsPlaceholder);
