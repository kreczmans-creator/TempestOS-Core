# WP 8.9.0 — Release Preparation & Product Baseline — Workspace Baseline Summary

## Purpose

A snapshot of the Engineering Workspace exactly as it ships in `v0.8.0`
— what exists, what a user can actually do with it today, and what
remains disclosed placeholder — verified directly against the running
Host, not assumed from any prior Work Package's own claim. New
deliverable type for this release; no `v0.7.0` precedent, since the
Workspace did not exist before `WP 8.0A`.

## What Ships

`Tempest.App`'s own default launch target (`ADR-0068`) is the
Workspace, not console `TempestShell` — running TempestOS presents a
five-region shell (Areas, Project Explorer, Documents, Properties,
Status Bar) opening directly onto the **Engineering Cockpit**, answering
four questions on every visit: where am I, what needs attention, is the
project healthy, what should I do next.

| Capability | Status | Originating Work Package |
|---|---|---|
| Five-region Workspace shell, compiled from all twelve `WP 8.0B` contracts | Real, running | `WP 8.1A` |
| Navigation (history, breadcrumbs, recent items) | Real, running | `WP 8.1B` |
| Project Explorer, Kind-keyed node providers, filtering, search | Real, running, against representative sample content only | `WP 8.1B` |
| Engineering Cockpit (default landing screen) | Real, running | `WP 8.1C` |
| Command Palette integration (Cockpit-scoped) | Real, running | `WP 8.1C`, `ADR-0070` (partial realisation — disclosed, not screen-independent yet) |
| Project Health Dashboard, Risk Summary, Open Decisions, Blocked Items, Quick Actions | Fixed, disclosed placeholder content | `WP 8.1C` |
| Continue Where I Left Off, Recent/Favourite Projects | Real where a Workspace service backs them; placeholder otherwise | `WP 8.1C` |
| Properties/Inspector panel | Not yet built | Named, not designed (`WP8.0C` gap) |
| Project Dashboard (distinct from the Cockpit) | Not yet built | Open |

## Verified Against the Running Host

- `RunAsync_WithEngineeringDomainSampleModule_RunsThroughTheRealHost`-class
  tests (and their Workspace equivalents) confirm the shell starts,
  reaches `HostState.Running`, and stops cleanly through the real,
  unmodified `TempestHostBuilder` — not a hand-assembled test pipeline.
- Every card on the Cockpit either delegates to a real Workspace service
  (`NavigationService`, `ICommandRegistry`) or renders fixed, explicitly
  disclosed placeholder text (e.g. `"— (not yet available)"`) — no
  fabricated data anywhere, verified directly against
  `EngineeringCockpitTests.cs` and `WorkspaceShellTests.cs` (91 tests
  across the two).
- The Project Explorer's own content remains a fixed, fictional
  Category → Group → Object tree (`Tempest.App.Workspace.Samples`,
  `WP 8.1B`) — the mechanism (`IProjectExplorerNodeProvider`,
  `IWorkspaceViewFactory`) is proven end to end, but no real Engineering
  Core `Kind` (Requirement, Material, and so on) is yet wired into it.

## Known Limitations (Disclosed, Not Blocking)

1. **Command Palette is reachable from the Cockpit only**, not from
   every screen — a disclosed, partial realisation of `ADR-0070`'s own
   "any screen" language, terminal-appropriate given `ADR-0066`.
2. **Properties/Inspector panel does not exist** — named as a plausible
   future `IDigitalThreadInspector` interface by `WP8.0C`, never
   designed.
3. **Project Explorer content is entirely fictional** — the first real,
   production `IWorkspaceViewFactory`/`IProjectExplorerNodeProvider`
   pair for an actual Engineering Core `Kind` remains unbuilt.
4. **The Engineering Cockpit does not render any Engineering Domain
   object** — `WP 8.2A`–`WP 8.2C` and the Workspace (`WP 8.0A`–`WP 8.1C`)
   shipped as two parallel, non-integrated tracks this release; wiring
   the Cockpit's own Project Health Dashboard to real Engineering Domain
   data is unscheduled.

None of the four rises to release-blocking — each is a named,
disclosed scope boundary the controlling Work Package itself set, not a
defect found during this review.

## Verdict

The Workspace, as shipped, matches every claim its own four
implementation Work Packages (`WP 8.1A`–`WP 8.1C`) made about it —
verified directly against the running Host and the real test suite, not
re-read from a prior retrospective's own account. Ready for Product
Approval as a genuine, if intentionally partial, first release of the
Engineering Workspace product surface.

## Related Documents

`docs/releases/v0.8.0/WP8.0A Workspace Architecture Document.md`;
`docs/releases/v0.8.0/WP8.0B Workspace Contracts.md`; `docs/releases/v0.8.0/
WP8.0C UX Specification.md`; `docs/releases/v0.8.0/WP8.1A Implementation
Report.md`; `docs/releases/v0.8.0/WP8.1B Implementation Report.md`;
`docs/releases/v0.8.0/WP8.1C Implementation Report.md`; `ADR-0062`–`ADR-0071`;
`docs/academy/02 Runtime Architecture/17-engineering-workspace.md`.
