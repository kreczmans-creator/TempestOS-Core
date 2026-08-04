# ADR-0068: The Engineering Workspace Is `Tempest.App`'s Own Default Launch Target

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.1A` (Workspace Shell),
2026-07-30. Resolves a question `ADR-0062` left implicit: once both
`TempestShell` (console) and the Workspace exist side by side, which one
does `Tempest.App`'s own entry point actually run by default?

## Context

`ADR-0062` decided the Workspace is additive to console `TempestShell`,
not a replacement — both remain valid, and neither Work Package through
`WP 8.0B` needed to decide which one `Program.cs` actually invokes,
since neither had compiled yet. `WP 8.1A`'s own controlling instruction
states its Definition of Done explicitly: "TempestOS launches directly
into the Workspace with a functioning shell, navigation and panel
system ready for future engineering modules." This requires a real,
disclosed decision about `Program.cs`'s own default behaviour, not left
implicit the way `ADR-0062` could afford to.

## Decision

**`Tempest.App`'s own entry point (`Program.cs`) constructs and runs the
Engineering Workspace (`WorkspaceManager`/`WorkspaceShell`) by default.
`TempestShell` remains in the repository, fully intact and fully
tested, but is no longer `Program.cs`'s own invocation target.** Running
TempestOS now presents the five-region Workspace shell (Areas, Project
Explorer, Documents, Properties, Status Bar) rather than the original
two-region console Shell (Navigation, Content).

This is a narrow, mechanical change: `Program.cs` now reads

```csharp
var host = new TempestHostBuilder().Build();
var manager = new WorkspaceManager(host);
await using var shell = new WorkspaceShell(manager, Console.Out, Console.In);
await shell.RunAsync();
```

replacing its own prior `TempestShell`-constructing three lines exactly.
No other file changed as a consequence of this decision — every
Platform Service, every Engineering Core framework, and `TempestShell`
itself are all unmodified.

## Consequences

**Positive:**

- Satisfies `WP 8.1A`'s own explicit Definition of Done directly and
  unambiguously — a fresh `dotnet run` now presents the Workspace, not a
  placeholder-only console Shell.
- `TempestShell`'s own 280 lines and its complete, passing test suite
  (`TempestShellTests.cs`) remain entirely untouched — this decision
  costs nothing to reverse, and nothing to maintain alongside the
  Workspace, since neither depends on the other.

**Negative:**

- `TempestShell` is no longer directly reachable by running
  `Tempest.App` — a future contributor wanting the console Shell
  specifically must construct it manually (or a future Work Package
  could add a launch-mode switch, not designed here, since no real need
  for one has been demonstrated yet).
- Every Academy article, screenshot, or walkthrough describing "running
  TempestOS" prior to this Work Package implicitly described
  `TempestShell`'s own two-region output — none is retroactively
  rewritten by this ADR itself, only by `WP8.1A Implementation
  Report.md`'s own disclosure and this Work Package's Academy updates.

## Alternatives Considered

**A launch-mode command-line flag** (`--shell=console` vs.
`--shell=workspace`) — considered and rejected. This is real,
additional complexity (argument parsing, a documented default,
validation) with no real, demonstrated need behind it yet — the
identical "do not build ahead of a demonstrated need" reasoning
`ADR-0066` already applied to a bigger question (the Workspace's own
rendering paradigm). A future Work Package can add this if a genuine
need for switching at runtime, rather than at the source level,
actually emerges.

**Leaving `Program.cs` running `TempestShell`, with the Workspace only
reachable via its own separate entry point or test harness** —
considered and rejected. This directly contradicts `WP 8.1A`'s own
explicit Definition of Done ("TempestOS launches directly into the
Workspace"), which is unambiguous about the default launch behaviour
specifically, not merely about the Workspace existing and being
testable.

## Related Documents

`ADR-0062`; `WP8.0A Workspace Architecture Document.md`;
`WP8.1A Implementation Report.md`; `src/Tempest.App/Program.cs`;
`src/Tempest.App/Shell/TempestShell.cs` (unmodified, still tested,
still valid — simply no longer the default).
