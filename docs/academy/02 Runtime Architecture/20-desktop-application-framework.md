# Desktop Application Framework

## 1. Introduction

This is the Academy's own concept guide for `Tempest.Desktop` —
TempestOS's first graphical desktop application, and the first real
code implementing `19-user-experience-and-desktop-application.md`'s
own architecture. Written at implementation stage (`WP 10.0B`), the
same cadence `17-engineering-workspace.md` followed relative to its own
architecture-then-implementation predecessors.

## 2. Purpose

To explain how a second presentation layer was added over the exact
same Engineering Workspace six real disciplines already populate,
without changing a single Workspace contract, and what that required
in practice — not only in principle.

## 3. Background

`ADR-0092` (`WP 10.0A`) decided the paradigm; `ADR-0094` (`WP 10.0B`)
chose Avalonia to realise it. Between the two, the Workspace contract
layer (`WP 8.0B`) had already gone through one full release
(`v0.9.0`, six real disciplines) proving itself stable under real,
varied use — but never under a *second* presentation layer. `WP 10.0B`
is the first Work Package to actually test that claim.

## 4. The Problem

Two presentation layers — a console `TempestShell` and a graphical
`Tempest.Desktop` — need to load the identical Engineering Workspace,
with the identical six disciplines, in the identical order, and stay
that way as future Work Packages add more. The obvious approach —
copy `Program.cs`'s own composition sequence into a second file — would
create two independently maintained copies of the same fact,
guaranteed to drift eventually.

## 5. The Design

`EngineeringWorkspaceComposer` (`Tempest.App.Composition`) extracts
that sequence into one shared method pair, called by both `Program.cs`
(console) and `WorkspaceHost` (`Tempest.Desktop`) — the same object
code, not a copy. `WorkspaceHost` itself mirrors `TempestShell`'s own
composition-root shape exactly, owning the `ITempestHost`/
`WorkspaceManager` lifecycle with zero dependency on any UI framework
— confirmed by its own file containing no Avalonia `using` directive
at all. `MainWindow` and its constituent Views (`DockingGrid`,
`ProjectExplorerView`, `PropertyInspectorView`, `DocumentAreaView`,
`CommandPaletteOverlay`) then bind to the exact same `IWorkspace`
surface the console already consumed, unchanged.

## 6. Alternatives Considered

Duplicating the composition sequence in `Tempest.Desktop` directly was
considered and rejected — it would satisfy "loads the same
disciplines" only until the next Work Package changed one file and
forgot the other. A third-party docking library was considered and
rejected — `WorkspaceDockPosition` supports no floating panel today,
so a library's own floating-window capability would be unused surface
for a capability this platform does not yet offer.

## 7. Why This Solution Was Chosen

Because the Workspace contract layer, designed rendering-agnostic
since `WP 8.0B`, made it possible: every Workspace-facing type
`Tempest.Desktop` consumes — `IWorkspace`, `IProjectExplorer`,
`IPropertyInspector`, `INavigationService`, `ICommandRegistry` — needed
zero change to support a second, structurally different presentation
layer. This is `ADR-0092`'s own central prediction, now proven true
against real, compiled, tested code rather than only architecture-stage
reasoning.

## 8. Architectural Principles

- **A shared composition root beats two synchronised ones.**
  `EngineeringWorkspaceComposer` is this project's own first case of
  two presentation layers needing the identical assembly sequence —
  extraction, not duplication, is the correct answer whenever that
  happens again.
- **A test that models reality more faithfully finds what a test that
  models it loosely cannot.** No prior test in this project's history
  ever built two genuinely independent `WorkspaceManager` instances
  against the same persisted session state — the moment one did, it
  immediately found a real, shipped defect (`ProjectExplorer`/
  `PropertyInspector`'s own non-deterministic panel Ids) that had been
  present, undetected, since `v0.8.0`.
- **A disclosed, tracked debt item's own "revisit trigger" is not
  decoration.** `TD-26` named its own reversal condition in writing at
  `WP 9.0A`; `WP 10.0B` is the Work Package that condition predicted,
  and hitting it in practice is exactly what that discipline is for.

## 9. Benefits

Zero Workspace contract change proven necessary for a second,
structurally different presentation layer. A genuine, pre-existing
session-persistence defect found and fixed before it could ever affect
a real user, rather than after. A reusable composition root that makes
"both presentation layers stay in sync" a structural guarantee, not an
ongoing manual discipline.

## 10. Trade-offs

This platform's first-ever GUI dependency, now real rather than only
decided in principle. No `.axaml` markup anywhere in `Tempest.Desktop`
— readable today, a genuine maintainability question once the UI grows
materially richer. `TD-26`'s own root cause remains unfixed at its own
source, only mitigated one layer up.

## 11. Common Mistakes

Assuming a Workspace contract being "rendering-agnostic" on paper means
it has actually been proven so — it had not, fully, until a second
real presentation layer existed to test it against. Assuming a defect
that "must have been caught already" in a mature, 2026-test-covered
codebase necessarily was — `TD-26`'s own consequences and the
panel-Id defect both survived an entire prior release specifically
because no existing test modelled the exact failure condition (two
genuinely independent process instances) closely enough to trigger
them.

## 12. Future Evolution

Per-Kind rich Object Editors, the graph-based Object Relationship
View (`ADR-0093`), Notification Framework integration, and
floating/multi-monitor panel placement (`ADR-0095`) are all named,
disclosed future work, not designed here. `TD-26`'s own root-cause fix
remains a real, open, now-doubly-evidenced candidate for whichever
future Work Package next touches `WorkspaceManager` directly.

## 13. Key Takeaways

A contract proven stable under one presentation layer is not yet
proven stable under two — only building the second one actually tests
that. The best evidence a "no contract change needed" architecture
prediction was right is a real implementation that needed none, not a
document that merely says it should not.

## Related Documents

`ADR-0094`; `19-user-experience-and-desktop-application.md`;
`WP10.0B Implementation Report.md`;
`docs/academy/03 Work Packages/WP10.0B-desktop-application-framework.md`;
`TD-26`.
