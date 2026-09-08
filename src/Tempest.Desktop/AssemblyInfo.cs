using System.Runtime.CompilerServices;

// WP 10.1A: lets tests directly construct/verify CockpitView and
// HealthColors (both internal — the Cockpit's own graphical dashboard is a
// Tempest.Desktop-local presentation concern, not a public API) — the
// identical build-visibility-only grant pattern Tempest.Workspace uses for
// Tempest.Core.Tests (WP 17.2B removed the equivalent Tempest.Workspace ->
// Tempest.Desktop grant; Desktop now reaches the Workspace only through
// its public contracts).
[assembly: InternalsVisibleTo("Tempest.Desktop.Tests")]
