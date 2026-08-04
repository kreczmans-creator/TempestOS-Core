namespace Tempest.App.Workspace;

/// <summary>
/// One entry in the Engineering Cockpit's own "Open Actions" region
/// (`WP8.0C Engineering Cockpit Specification.md` §2) — today, always
/// fixed, representative placeholder content, since no service tracking
/// real open actions is wired to the Workspace yet.
/// </summary>
/// <param name="Title">A short, one-line description of the action.</param>
/// <param name="Owner">Who the action is assigned to.</param>
internal sealed record CockpitActionItem(string Title, string Owner);
