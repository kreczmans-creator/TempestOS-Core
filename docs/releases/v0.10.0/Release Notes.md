# TempestOS v0.10.0 — "User Experience & Desktop Application"

**Status:** Prepared under `WP 10.9A` (`v0.10.0` Release Candidate &
Engineering Sign-Off) — see `WP10.9A Engineering Release Report.md` for
the authoritative go/no-go decision, gate-by-gate evidence, and full
engineering justification. This document is the release's own narrative
record; the Engineering Release Report is the release's own decision
record. If the two ever disagree, the Engineering Release Report
governs.

---

## 1. Executive Summary

`v0.10.0` turns `v0.9.0`'s own terminal-based Engineering Workspace —
six real Engineering Disciplines, functionally complete but console-
only — into a full, professional, graphical desktop application.
`Tempest.Desktop` (Avalonia 11.2.3, `ADR-0094`) is a new, fifth project
in the solution: a Ribbon, a filterable multi-select Project Explorer
with real drag-and-drop reparenting, a sectioned Property Inspector, a
generic Object Editor Framework with five discipline-specific
enhancement sections, a graphical Digital Thread relationship graph, a
dockable panel framework, an Engineering Cockpit dashboard as the
default landing screen, a global Command Palette, Undo/Redo, a Macro
system, and a two-Work-Package closing pass (`WP 10.7A`/`WP 10.8A`) that
replaced every placeholder this release's own audits found with real
behaviour wherever the underlying platform capability already existed.

Sixteen Work Packages (`WP 10.0A` through this closing `WP 10.9A`)
delivered this release without reopening a single one of the twelve
frozen `WP 8.0B` Workspace contracts, without adding a second Command
dispatch mechanism, and without any View ever bypassing the Command
Framework for a mutation — confirmed, not assumed, by this Work
Package's own independent, from-source re-verification (§4 of the
Engineering Release Report).

## 2. Major Capabilities Added

- **A real graphical desktop application** (`Tempest.Desktop`) replacing
  the `v0.9.0` terminal presentation — `ADR-0092` (superseding
  `ADR-0066`) realised.
- **A professional Engineering Ribbon** — one generic engine over
  `ICommandRegistry`, grouped by discipline and verb, with real
  dispatch for Create/Duplicate/status-transitions across all six
  disciplines (`WP 10.7A`) and honest, non-misleading messaging for the
  one verb (Copy) still genuinely unwired (`WP 10.8A`).
- **A generic Object Editor Framework**, one engine across all six
  disciplines, with five real discipline-specific sections added in
  this release's closing pass: Mechanical BOM, Requirements Owner/
  Priority, Calculations Execute/Recalculate, Verification Record
  Result, Documents Attachments.
- **A graphical Digital Thread relationship graph** (`DigitalThreadGraphView`,
  `ADR-0093` superseding `ADR-0065`) — a progressively-expandable
  node-link graph over already-existing relationship reads, zero new
  traversal mechanism.
- **Real drag-and-drop object reparenting** in the Project Explorer,
  closing `FCR-0066` via a lighter route than originally proposed (a
  plain `ObjectMoveRequested` event, not a fourth `IWorkspaceManager`
  member).
- **A real, live-data Engineering Cockpit dashboard** as the default
  landing screen (`ADR-0069` finally realised graphically) — twenty
  named regions, six upgraded from placeholder to real reads in `WP
  10.1A`, two more (Risks, Review) closed in this release's own closing
  pass.
- **Undo/Redo** (`ADR-0098`, a Desktop-local delegate stack, not a new
  Command contract), **Macros** (`ADR-0099`, a macro is an ordinary
  registered Command), and an **External Controller abstraction**
  (`ADR-0100`, one real Keyboard provider, zero vendor SDKs).
- **A global Command Palette** (`ADR-0070` realised graphically), a
  dockable panel framework with Auto-Hide/Collapse/predefined layouts,
  theme persistence, and a real Property Inspector Validation section
  reading genuine `IValidatable` results (`WP 10.8A`, this release's own
  final placeholder closure).

## 3. Architecture Improvements

- **Three precedented, additive extensions to `IWorkspaceManager`**
  (`ADR-0096` Rename/Delete, `ADR-0097` Revise) — the only member
  categories ever added to that frozen `WP 8.0B` contract this release.
  Independently re-confirmed by `WP 10.9A`: no fourth generic-dispatch
  member exists anywhere in the shipped interface.
- **Zero changes to `ICommandDispatcher`/`ICommandRegistry`** — `WP
  10.9A` confirmed this directly (`git diff` against both files: no
  output, i.e. byte-for-byte identical to the pre-`v0.10.0` baseline).
- **`ADR-0063` honoured throughout** — every Workspace View reads
  directly; every mutation, without exception, dispatches through a
  Command handler via `ICommandDispatcher`. Independently spot-verified
  by `WP 10.9A` across the newest, highest-risk surfaces (the `WP
  10.7A` drag-and-drop Move handler, the Macro CRUD path — the latter
  correctly outside `ADR-0063`'s own explicit scope, since macros are a
  Desktop/Platform-Service concern, not Engineering Domain data,
  `ADR-0099`).
- **A genuine, six-Work-Package-latent Command Palette defect found and
  fixed** (`WP 10.3B`): pressing Enter on any real discipline command
  (every one lacks `CreateDefault`) previously closed the palette and
  did nothing, silently, since `WP 8.1A`. Fixed via a new,
  honestly-reported `CommandUnavailable` event.
- **Two further real defects found and fixed at their own source**,
  each before its own Work Package's commit: `RibbonView.Rebuild()`
  missing its own initial `RefreshEnablement()` call (`WP 10.3B`); every
  new Object Editor section's own success message being immediately
  overwritten by the `Refresh()` call that reported it (`WP 10.7A`).

## 4. Desktop Improvements

A Menu, Ribbon, Quick Access Toolbar, filterable/breadcrumbed Project
Explorer with real context menu and inline rename, a collapsible
sectioned Property Inspector, pinned/highlighted Document Area tabs, a
six-segment Status Bar, five keyboard shortcuts, a shared `DesignTokens`
spacing/typography system, theme-reactive brushes closing every
disclosed `TD-39`-class fixed-colour defect found across three separate
Work Packages, and a consistent, professional visual language applied
throughout — all confirmed rendering correctly against the real, running
application during this release's own closing verification passes
(`WP 10.7A`, `WP 10.8A`, `WP 10.9A`).

## 5. Engineering Workspace Completion

Every one of the six Engineering Disciplines shipped in `v0.9.0`
(Mechanical, Requirements, Calculations, Documents, Verification,
Manufacturing) now has real Ribbon-driven Create/Duplicate/status-
transition dispatch, real drag-and-drop reparenting, and — for the five
disciplines whose own discipline-specific enhancement was named as
future work since `WP 10.3A` (`FCR-0068`) — a real, working Object
Editor section. One honestly disclosed, pre-existing exception remains:
Requirements never resolve through `EngineeringDomainContext.Repository`
(`TD-41`), so the Object Editor and Property Inspector's own Validation
section both correctly report "not available for this object here"
rather than fabricate a result, for that one discipline only. Every
other read/write for Requirements remains fully correct via
`IRequirementsService` and the Ribbon's own dedicated handlers.

## 6. Command Framework Improvements

No change to the Command Framework's own two public contracts. The real
improvement this release delivers is *exposure*: the Ribbon and Object
Editor together reach far more of the already-real, already-registered
command surface than any prior Work Package's own UI ever did, using
only `ADR-0096`/`ADR-0097`'s own three Kind-keyed verbs plus direct
`ICommandDispatcher.DispatchAsync` calls exactly where `ADR-0063`
permits them. The Ribbon's own honest failure-reporting discipline
(`WP 10.3B`, refined `WP 10.8A`) also, as a direct side effect, exposed
and closed the Command Palette's own long-latent silent no-op defect
(§3, above).

## 7. Object Editor Completion

`ObjectEditorView` — one generic engine, not six per-discipline classes
(`WP 10.3A`) — gained real Name/Content dispatch (`ADR-0096`/`ADR-0097`)
in its founding Work Package, then five real, Kind-gated discipline
sections in this release's closing pass (`WP 10.7A`): Mechanical BOM
fields (`SetBomLineCommand`), Requirements Owner/Priority
(`SetRequirementOwnerCommand`/`SetRequirementPriorityCommand`),
Calculations Execute/Recalculate (`ExecuteCalculationCommand`/
`RecalculateCalculationCommand`), Verification Record Result
(`RecordVerificationResultCommand` — also reused a second time,
`WP 10.8A`, for Manufacturing's own "Record Inspection Result" Ribbon
entry), and Documents Attachments (`AttachDocumentCommand`, metadata
only — `TD-31` unchanged, no file-upload capability fabricated).

## 8. Digital Thread Completion

`DigitalThreadGraphModel`/`DigitalThreadGraphView` (`WP 10.4A`) realise
`ADR-0093` as a real, progressively-expandable node-link graph, reading
exclusively through already-existing, already-permitted reads
(`EngineeringDomainContext.Repository`/`RelationshipRepository`,
`VerificationRecordReader`) — zero new traversal mechanism. One
discoverability gap remains, disclosed by `WP 10.6B`'s own Prioritised
Defect List (P4) and not closed this release: exactly one entry point
exists (the Quick Access Toolbar's own "🕸 View Relationships" button) —
no Ribbon command or Project Explorer context-menu item reaches it yet.

## 9. Cockpit Completion

Twenty named Cockpit regions; six upgraded from disclosed placeholder to
real reads in `WP 10.1A` (`OpenDecisions`, `BlockedItems`, `Health`,
`RiskSummary`, `DigitalThreadSummary`, `UpcomingMilestones`); two more
closed in this release's own final pass (`WP 10.7A`) — "Risks"
(`LiveRisks.Count`) and "Review" (a real sum of each discipline's own
already-computed in-review count) — plus a real "Favourite Projects"
Desktop-layer card (`WP 10.7A`, reading the already-shipped
`FavouriteObjectsState`, `WP 10.6A`). Two Cockpit regions remain honest,
correctly-disclosed placeholders, not fixed this release because no
backing platform capability exists to fix them honestly: "Overdue
Actions" (no due-date field anywhere in the Domain) and
`EngineeringCockpit.FavouriteProjects` itself, the distinct App-layer
member (`Tempest.App` cannot reach the Desktop-layer favourites service
without inverting the project's own layering).

## 10. Desktop UX Improvements

Docking (Bottom dock, Collapse, Auto-Hide, three predefined layout
presets, a new Output panel), theme persistence across restarts, window
position/size persistence, a Command History log, Recently-Used Ribbon
commands, five keyboard shortcuts plus a real (if currently unpopulated
by any default binding) keyboard-remapping mechanism (`FCR-0077`), toast
notifications, and a consistent confirmation-dialog discipline for every
destructive action.

## 11. Testing Summary

Independently re-verified from a genuinely clean build by `WP 10.9A`
itself — not carried forward from any prior Work Package's own claim:

| Configuration | Build | Tests |
|---|---|---|
| Debug | 0 Warnings / 0 Errors | **2266 / 2266 passing**, 0 failed, 0 skipped |
| Release | 0 Warnings / 0 Errors | **2266 / 2266 passing**, 0 failed, 0 skipped |

Growth this release: `v0.9.0` shipped at 2026/2026 (its own second
release-readiness pass); `v0.10.0` ships at 2266/2266 — **+240 tests**,
zero regressions across either configuration.

## 12. Known Technical Debt

Carried into `v0.10.0`, none Release Blocking (full detail: `Technical
Debt Register.md`, 41 tracked items, 34 currently Open):

- **`TD-41`** — Requirements never resolve via
  `EngineeringDomainContext.Repository` (open since `WP 10.3A`; now
  known to affect two Desktop presentation surfaces, Object Editor and
  Property Inspector Validation, both degrading honestly).
- **`TD-38`** — `EngineeringObjectFactory<T>` enforces no
  business-identifier uniqueness of its own (open since `WP 10.1B`, an
  Engineering Domain change, out of every `v0.10.0` Work Package's own
  scope).
- **`TD-31`** — `Attachment`/`IAttachment` carry descriptive metadata
  only; no file/URL storage capability exists anywhere in the platform
  (open since `WP 9.4A`, unchanged by this release's own Documents
  Attachments Object Editor section, which deliberately does not
  fabricate an upload affordance).
- **`TD-34`** — `CompositeLogSinkTests`'s own non-reproducible
  `Console.Out`-capture flake (open since `v0.9.0`'s own second pass).
- Every other tracked item is disclosed in full in the Technical Debt
  Register; none was worsened by this release, confirmed directly by
  `WP 10.9A`'s own re-read of every `v0.10.0`-era entry.

## 13. Deferred Future Capabilities

Named directly, not silently dropped (full detail: `Future Capability
Register.md`, 83 entries):

- **`FCR-0073`** — Copy/Move Destination-Picker Dialog. The single
  largest remaining Ribbon gap: Copy (and a Ribbon Move *button*,
  distinct from the real drag-and-drop Move this release ships) both
  need a destination-parent picker that does not exist anywhere in this
  platform.
- **The Command Palette/Macro `CreateDefault` gap** — no real
  discipline command has ever set this delegate; closing it needs a
  genuine `CommandDescriptor`/`ICommandRegistry` extension, explicitly
  out of scope for every `v0.10.0` Work Package.
- **`FCR-0064`/`ADR-0095`** — floating/multi-monitor panel placement,
  reserved, not yet written.
- **`FCR-0065`** — Notification Framework Workspace integration; a real
  Status Bar segment exists, still honestly empty.
- **`FCR-0056`** — a dedicated Governance & Risk Workspace
  (`Risk`/`Issue`/`Decision`/`Hazard`/`Assumption`); the Cockpit now
  reads this Domain family directly, but no Explorer/Commands/Property
  Inspector presence exists yet.
- **`FCR-0069`** — Real, Authored Per-Command Icons (currently
  verb-derived heuristics).
- **`FCR-0077`** — a real keyboard-remapping UI over the already-real
  `IInputBindingProvider` mechanism.

## 14. Statistics

Independently re-derived by `WP 10.9A` directly from the repository, not
carried forward from any prior claim:

- **236 new files** this release (141 documentation, 93 C# source, 2
  project files) — **18,604 new lines**, of which 17,570 are C#.
- **46 pre-existing tracked files modified** — +3,729 / -392 lines.
- **`Tempest.Desktop`**, the release's own central deliverable, is a
  wholly new fifth project: **57 C# files, ~10,268 lines**.
- **965 total `.cs` files** now across `src/` and `tests/` combined.
- **8 new ADRs** (`ADR-0092`–`ADR-0094`, `ADR-0096`–`ADR-0100`; 91 → 99
  total), plus one reserved-not-written (`ADR-0095`). Two Superseded
  markings (`ADR-0092` supersedes `ADR-0066`; `ADR-0093` supersedes
  `ADR-0065`).
- **+240 tests** (2026 → 2266), zero regressions.
- **Sixteen Work Packages** completed (`WP 10.0A` through `WP 10.9A`).
- **Approximate codebase growth this release: ~9%** by `.cs` file count
  over the `v0.9.0` baseline (57 of 965 current files are wholly new to
  this release's own central `Tempest.Desktop` project alone; the true
  proportional growth including every modified file is higher — no
  single precise "percent grown" figure is claimed beyond what these
  counts directly support).

## 15. Final Engineering Assessment

`v0.10.0` delivers exactly what its own sixteen Work Packages set out to
build: a real, professional, graphical desktop application over an
already-real Engineering Workspace, adding no new architecture beyond
three narrowly-scoped, precedented, additive `IWorkspaceManager`
extensions across the entire release. Every placeholder this release's
own repeated audits found (`WP 10.6B`, `WP 10.6D`) was either closed
using existing platform capability (`WP 10.7A`, `WP 10.8A`) or is named
here, explicitly, as a genuine, disclosed, unclosed gap — never silently
dropped. See `WP10.9A Engineering Release Report.md` for the
independent, from-source re-verification of every claim in this
document and the release's own final go/no-go decision.

## Related Documents

`WP10.9A Engineering Release Report.md`; every `WP10.0A`–`WP10.8A`
Implementation Report under `docs/releases/v0.10.0/`; `Technical Debt
Register.md`; `Future Capability Register.md`; `PROJECT_STATUS.md`.
