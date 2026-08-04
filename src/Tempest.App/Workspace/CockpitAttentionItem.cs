namespace Tempest.App.Workspace;

/// <summary>
/// One entry in the Engineering Cockpit's own "What Needs Attention"
/// region (`WP8.0C Engineering Cockpit Specification.md` §3) — today,
/// always fixed, representative placeholder content, since no
/// Requirements/Verification/Calculation service is wired to the
/// Workspace yet (`WP 8.1C`'s own explicit scope boundary).
/// </summary>
/// <param name="Title">A short, one-line summary of what needs attention.</param>
/// <param name="Detail">A longer, one-sentence description.</param>
internal sealed record CockpitAttentionItem(string Title, string Detail);
