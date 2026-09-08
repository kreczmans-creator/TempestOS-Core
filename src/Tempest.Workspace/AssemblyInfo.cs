using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tempest.Core.Tests")]

// WP 10.0B: lets Tempest.Desktop reuse WorkspaceManager's own internal
// StatusBar (WorkspaceStatusBar) directly, rather than re-implementing its
// identical WorkspaceSelectionChangedEvent-driven status text logic a
// second time in a second assembly. A build-visibility grant only — no
// public interface signature changes, per WP 10.0B's own explicit "no
// Workspace contract redesign" constraint.
[assembly: InternalsVisibleTo("Tempest.Desktop")]

// WP 10.1A: lets tests directly verify EngineeringCockpit's own real-data
// properties (the graphical Cockpit's own data source) against the real,
// running Workspace — the identical build-visibility-only grant pattern,
// applied to the test project this Work Package adds.
[assembly: InternalsVisibleTo("Tempest.Desktop.Tests")]
