using System.Runtime.CompilerServices;

// WP 10.1A: lets tests directly construct/verify CockpitView and
// HealthColors (both internal — the Cockpit's own graphical dashboard is a
// Tempest.Desktop-local presentation concern, not a public API) — the
// identical build-visibility-only grant pattern Tempest.App already uses
// for Tempest.Core.Tests/Tempest.Desktop.
[assembly: InternalsVisibleTo("Tempest.Desktop.Tests")]
