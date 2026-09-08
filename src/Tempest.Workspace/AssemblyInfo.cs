using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tempest.Core.Tests")]

// WP 17.2B: Tempest.Harness (WorkspaceShell.cs, Program.cs) used to be
// same-assembly with the rest of the Workspace domain layer, reaching
// ProjectExplorer.ProjectExplorerConcrete/NavigationService.
// NavigationServiceConcrete's own internal members (EnterAsync/ExitAsync/
// FilterAsync, GoBackAsync/GoForwardAsync/History) for free — deliberately
// not part of the twelve `WP8.0B Workspace Contracts.md` interfaces, per
// EngineeringCockpit's own identical precedent. Splitting the harness into
// its own project needs the identical access restated as an explicit
// grant, not a new one.
[assembly: InternalsVisibleTo("Tempest.Harness")]

// WP 10.1A: lets tests directly verify EngineeringCockpit's own real-data
// properties (the graphical Cockpit's own data source) against the real,
// running Workspace — the identical build-visibility-only grant pattern,
// applied to the test project this Work Package adds.
[assembly: InternalsVisibleTo("Tempest.Desktop.Tests")]
