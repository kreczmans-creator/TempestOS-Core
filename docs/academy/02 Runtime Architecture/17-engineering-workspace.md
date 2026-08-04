# Engineering Workspace

## 1. Introduction

The Engineering Workspace (architected `WP 8.0A`, contracted `WP 8.0B`,
shell implemented `WP 8.1A`, its complete target experience specified
`WP 8.0C`, its Navigation system and Project Explorer implemented
`WP 8.1B`, `ADR-0062`–`ADR-0071`) is TempestOS's first user-facing
engineering product surface, and — since `WP 8.1A` — the platform's own
default launch target (`ADR-0068`). Running `Tempest.App` today
presents the five-region Workspace shell (Areas, Project Explorer,
Documents, Properties, Status Bar), not the original console
`TempestShell`, which remains in the repository, fully tested, simply
no longer the default. `WP 8.0C` then specified, without implementing,
the complete target experience this shell is meant to grow into — an
Engineering Cockpit landing screen (`ADR-0069`), a Command Palette
(`ADR-0070`), a Project Dashboard, an Inspector panel distinct from
Properties, and the full interaction/navigation/behaviour model around
them. `WP 8.1B` then implemented the first slice of that target
experience for real: navigation history, breadcrumbs, filtering, recent
items, and context menus, proven against a real, fixed, fictional
object tree (`Tempest.App.Workspace.Samples`) — the Project Explorer's
own first living reference content, not a test double. This document
teaches the reasoning behind the Workspace's own design, its own frozen
public contracts, its own shell implementation, the target experience
specified for it, and its own first navigation/tree implementation —
not yet any real engineering-domain content (Requirements, Materials,
Calculations) or the richer Cockpit/Command Palette UX, neither of
which any Work Package through `WP 8.1B` has built.

## 2. Purpose

To explain why the Workspace is not a new platform layer but a
graphical evolution of the same composition root `TempestShell` already
occupies, and why its own View layer is forbidden from calling a
mutating service method directly — the two decisions every other design
choice in `WP8.0A Workspace Architecture Document.md` depends on.

## 3. Background

Every capability TempestOS has shipped through `v0.7.0` — the Runtime
Foundation, every Platform Service, and the entire Engineering Core and
Systems Engineering Foundation — is infrastructure a user never
directly sees. `TempestShell` (`WP 5.0D`) is the one exception, and it
presents nothing but `PlaceholderPage` stubs: no Requirement, Material,
Calculation Record, or Verification Record has ever appeared on screen.
`NavigationItem`'s own design (`WP 5.0A`) explicitly anticipated this
gap would eventually need closing — its own `Icon` property's
documentation names "any future UI shell" as the thing that would
resolve what an item actually looks like. The Engineering Workspace is
that future shell, designed now that real engineering data exists for
it to present.

## 4. The Problem

1. **What *is* the Workspace, architecturally** — a new Platform
   Service, a new Module category, or an evolution of something that
   already exists?
2. **How does a View change engineering data without becoming a second,
   unmediated write path** alongside the Command Framework this
   platform already built specifically to be the one place a mutating
   action passes through?
3. **Where does a user's own panel arrangement live** between sessions,
   without inventing a second persistence mechanism alongside the one
   (`ISettingsProvider`) already built for exactly this class of data?
4. **How does a digital thread get presented on screen** without
   requiring a new traversal capability the Systems Engineering
   Foundation does not yet provide?
5. **What does the Workspace actually feel like to use** — what screen
   does a user land on, how do they discover an action without
   memorising it, and how does the experience stay coherent across
   every future engineering discipline — decided deliberately, before
   implementation, rather than accreted screen by screen (`WP 8.0C`)?

## 5. The Design

The Workspace is `Tempest.App`'s own composition root, evolved from
`TempestShell`'s console loop into a graphical, five-region, docking
desktop application — additive to `TempestShell`, not a replacement
(`ADR-0062`). It introduces zero new Platform Service. A View reads
directly from whichever service owns the data it presents
(`FindAsync`, `GetRelationshipsAsync`, `GetEvidenceAsync`); every
mutating action — a revision, a status change, a new relationship —
dispatches through the existing `ICommandDispatcher` instead of calling
a mutator directly (`ADR-0063`). Panel layout, open tabs, and last
selection persist through the existing `ISettingsProvider`, exactly as
any other per-user runtime-mutable value already does (`ADR-0064`).
The Digital Thread panel is a View over `GetEvidenceAsync` exactly as
it already exists — no new traversal mechanism, no multi-hop query
capability (`ADR-0065`). Its own presentation is terminal-based, not a
graphical desktop framework — this platform's first-ever GUI dependency
was considered and deliberately not taken on absent real demonstrated
need (`ADR-0066`). Twelve public contracts exist, all now compiled and running
(`Tempest.App.Workspace`, `WP 8.1A`): `IWorkspace`, `IWorkspaceManager`,
`IWorkspaceView`, `IWorkspacePanel`, `IWorkspaceLayout`,
`INavigationService`, `ISelectionService`, `IWorkspaceContext`,
`IWorkspaceState`, `IProjectExplorer`, `IPropertyInspector`,
`IWorkspaceCommand`. `WorkspaceManager` is `Tempest.App`'s own default
launch target (`ADR-0068`), assembling a real `IWorkspace` from four
existing Platform Services with zero new one. A future Engineering
Discipline Module extends the Workspace via two Kind-keyed
registrations — `IWorkspaceViewFactory` for object presentation,
`IProjectExplorerNodeProvider` for tree population — mirroring
`IReportDefinition`/`IReportRenderer<T>`'s own established pattern
(`ADR-0067`); this Work Package proves both mechanisms directly, using
real, minimal test-double registrations, though no production
Engineering Core `Kind` is registered yet (no engineering functionality
in this Work Package's own scope). See `WP8.0A Workspace Architecture
Document.md`, `WP8.0B Workspace Contracts.md`, and
`WP8.0B Workspace Contracts.md` for the complete design.

`WP 8.0C` then specified, product- and UX-only, the complete experience
this shell is meant to reach: the Engineering Cockpit — not a
placeholder Home page — is the Workspace's own default landing screen
(`ADR-0069`), a live, data-driven dashboard surfacing "what needs
attention" ahead of any drill-down; the Command Palette is a first-class
global entry point, a *view* over the existing `ICommandRegistry`
introducing no second registration mechanism, reachable from any screen
and surfacing both commands and navigation targets in one surface
(`ADR-0070`); and a Properties/Inspector split formalises a distinction
already implicit in the shipped contracts — Properties answers "what an
object is," Inspector answers "what proves or relates to it" (a
`GetEvidenceAsync`-composed read, `ADR-0065`, unchanged). Every screen
and journey this specification names is checked against the twelve
already-approved contracts and disclosed, not silently assumed, where a
gap exists: `WP8.0C UX Specification.md` §0 names `WP 8.1A`'s own shell
as having shipped before this specification existed, and §5 names,
without resolving, a real tension between this specification's own
richer ambitions (true multi-window, multi-monitor, a floating palette
overlay) and `ADR-0066`'s current terminal-based decision.

`WP 8.1B` then implemented the Navigation system and Project Explorer
`WP8.0C Navigation Maps.md`/`Screen Catalogue.md` specified, against the
`WP 8.0B` contracts that already existed. Two genuine capabilities
`WP 8.0B` never anticipated — navigation history/recent items
(`NavigationService.History`/`RecentItems`/`GoBackAsync`/`GoForwardAsync`)
and breadcrumbs/filtering (`ProjectExplorer.CurrentPath`/`EnterAsync`/
`ExitAsync`/`FilterAsync`) — were added as disclosed, same-assembly-only
extensions to the concrete classes, never to the twelve public
interfaces themselves, mirroring `WorkspaceManager.StatusBar`'s own
`WP 8.1A` precedent exactly: a real capability the approved contracts
never named, added without reopening any of them. The Project Explorer
is populated, for the first time, with real (if fictional) content — a
fixed Category → Group → Object tree
(`Tempest.App.Workspace.Samples.SampleExplorerContent`) — proving the
Kind-keyed provider architecture (`ADR-0067`) end to end. Building this
first real registration found `ADR-0067`'s own worked example
(a module calling `IWorkspaceManager.RegisterView` directly) does not
hold: a discovered module has no path to the one `WorkspaceManager`
instance wrapping its own Host from the outside. `ADR-0071` corrects
this — registration belongs to `Tempest.App`'s own composition root
(`Program.cs`), not to a module.

## 6. Alternatives Considered

**A new Platform Service for the Workspace** — considered and rejected;
see `ADR-0062`'s own reasoning, identical to `ADR-0033`'s rejection of
the same shape for the Shell three releases earlier.

**Views calling mutating service methods directly, "for now"** —
considered and rejected; see `ADR-0063`. This project's own history
already demonstrates the cost of skipping the Command Framework for
convenience: it was built specifically so mutating actions would not
need ad hoc, per-caller invocation logic.

**A dedicated multi-hop graph traversal service to power a richer
digital thread visualisation** — considered and rejected; see
`ADR-0065`. No real, demonstrated need for transitive traversal exists
yet, and building one speculatively would repeat exactly the mistake
`WP7.2B Digital Thread Architecture.md` already argued against at the
service layer.

**A full graphical desktop framework (WPF, Avalonia, MAUI)** —
considered and rejected; see `ADR-0066`. This platform has taken on
zero GUI dependency of any kind through `v0.7.0`; a terminal-based
interface satisfies every named user journey without that first-ever
commitment.

**A static Home/welcome page as the default landing screen** —
considered and rejected; see `ADR-0069`. Cheap to build, but directly
contradicts `WP 8.0C`'s own controlling instruction that the Cockpit
"should become the engineer's primary landing page," and would not
answer "what needs attention?" on the one screen where an engineer most
needs that question answered.

**Per-view search only, with no global command surface** — considered
and rejected; see `ADR-0070`. This is what `WP 8.1A`'s shipped shell
effectively has today, and it directly fails Principle 4
("everything discoverable"): a user has no way to discover a command
that exists outside whichever view they currently happen to be looking
at.

**Giving a discovered module a reference to `IWorkspaceManager`**
(a new Workspace-aware module base class, or DI-registering
`WorkspaceManager` into the Host) — considered and rejected; see
`ADR-0071`. This would directly contradict `ADR-0062`'s own decision
that the Workspace is not a Platform Service and is never resolved
through `ITempestHost.Services` — reopening a settled architectural
boundary to save one composition-root registration call.

## 7. Why This Solution Was Chosen

It is the first user-facing product surface to reach the identical
"reuse what already exists, introduce nothing new" conclusion every
Engineering Core framework independently reached before it (Materials,
Calculations, Verification, Requirements) — now extended, for the first
time, to presentation-layer concerns (layout state, mutation discipline,
thread visualisation) rather than only service-layer storage.

## 8. Architectural Principles

- **Composition Over Inheritance** — every View composes existing
  service reads; none introduces its own query, cache, or index.
- **Single Responsibility Principle** — a View renders exactly one
  object, relationship list, or composed thread read; never more than
  one concern.
- **One Reason to Change**, applied to presentation code for the first
  time: a View that reads data and a Command that changes it are two
  different reasons to change, kept as two different things.

## 9. Benefits

- Zero new Platform Service, zero new persistence mechanism, zero new
  traversal capability — the Workspace's own architecture cost is
  almost entirely presentation design, not new platform engineering.
- Every mutating Workspace action is, from its own first design,
  testable and reusable from a menu, a keyboard shortcut, or a future
  automation caller, exactly as `ICommand`'s own contract already
  anticipates.
- A future Engineering Discipline Module extends the Workspace through
  mechanisms that already exist (`INavigationProvider`,
  `ICommandRegistry`) with zero Workspace code change required.

## 10. Trade-offs

- Two presentation layers (console `TempestShell`, terminal-based
  Workspace) now coexist, a maintenance surface no prior release
  carried (`ADR-0062`).
- The Digital Thread panel shows only one hop of relationship depth per
  view; tracing a longer chain requires repeated manual navigation
  (`ADR-0065`).
- A terminal interface is a genuine capability ceiling relative to a
  full graphical framework — no custom fonts, no embedded images,
  coarser interaction fidelity — accepted since no named user journey
  requires graphical-only capability (`ADR-0066`).
- Extending the Workspace to a new engineering `Kind` requires two
  registration calls (a view factory and, optionally, an explorer node
  provider), not one — a small, disclosed ergonomic cost accepted for
  keeping the two concerns independently varying (`ADR-0067`).
- `TempestShell` is no longer directly reachable by running
  `Tempest.App` — a future contributor wanting the console Shell
  specifically must construct it manually (`ADR-0068`, `WP 8.1A`).
- `IPropertyInspector` shows only Identity facets (Id, Kind) as of
  `WP 8.1A` — Revision/Provenance/Relationship/DisciplineSpecific facets
  have no source yet, expected given "no engineering functionality" was
  this Work Package's own explicit constraint.
- A real gap remains, disclosed rather than hidden, between what
  `WP 8.1B` ships today and what `WP 8.0C` specifies as the target
  experience — the Project Explorer is now populated (with fictional
  sample content) and navigable, but there is still no Cockpit, no
  Command Palette, and no Properties/Inspector split
  (`WP8.0C UX Specification.md` §0).
- A future `IWorkspaceCommand`-adjacent thirteenth contract
  (`IDigitalThreadInspector`, named but not designed, `WP8.0C Screen
  Catalogue.md` §10) is now a disclosed, plausible future addition, not
  yet a decision — a future Contract Review must actually design it
  before an Inspector panel distinct from Properties can be built.
- True multi-window and multi-monitor placement remain named, not
  designed — `WP8.0C Workspace Behaviour Specification.md` §5-§6
  elaborates precisely why a terminal-based single window (`ADR-0066`)
  bounds this specific ambition, without resolving the tension.
- Navigation history and recent items are Workspace-global, not
  per-tab/per-project as `WP8.0C Navigation Maps.md` §4 specifies — a
  disclosed simplification, since the terminal shell has no independent
  per-tab focus model to hang per-tab history off
  (`WP8.1B Implementation Report.md`).
- The Project Explorer's own interaction vocabulary (`open <N>`, `up`,
  `filter [text]`, `back`/`forward`, `menu <N>`) is a small, discoverable
  set of terminal words, not a literal binding of `WP8.0C Interaction
  Specification.md`'s own richer keyboard-shortcut/mouse-gesture model
  — the literal bindings remain deferred to a future rendering-technology
  choice, unchanged since `ADR-0066`.

## 11. Common Mistakes

The mistake most worth naming: treating "the View already has a
reference to the service, so it's fine to call `ReviseAsync` directly
just this once" as a harmless shortcut. It is not — every mutating path
that bypasses the Command Framework is one future cross-cutting concern
(audit attribution, undo/redo, concurrency retry) that must be
retrofitted into that one View specifically, rather than gained for
free at the one dispatch point every other mutating action already
uses.

A second, `WP 8.1B`-specific mistake worth naming: assuming a Decision
document's own worked example is itself proven, rather than proving it
against real code before relying on it. `ADR-0067`'s own Decision
section described a module calling `IWorkspaceManager.RegisterView`
directly — a plausible-sounding example nobody had actually built yet.
Building the first real registration (`WP 8.1B`) is what surfaced that
this specific example contradicts `ADR-0062`'s own Host/Workspace
boundary. The lesson is not "ADRs are unreliable" — it is that a worked
example inside an ADR is still a claim, and claims get verified by
building the thing, exactly as this platform's own two-stage
architecture-then-contracts discipline already assumes for the contracts
themselves.

## 12. Future Evolution

- **The first real, production `IWorkspaceViewFactory`/
  `IProjectExplorerNodeProvider` pair for an actual Engineering Core
  `Kind`**, most naturally for Requirements — `WP 8.1B` proved the
  mechanism against fictional sample content only; a real discipline
  Kind is still the natural next proof.
- **The Engineering Cockpit and Command Palette** (`ADR-0069`/
  `ADR-0070`) — specified in full (`WP 8.0C`) but not yet implemented;
  the next Workspace-experience gap after Navigation & Project Explorer.
- **Specific TUI library selection**, if `WorkspaceShell`'s own
  hand-rolled renderer (`WP 8.1A`) ever proves insufficient — narrower
  than `ADR-0066`, needing no further ADR.
- **Multi-window support, multi-hop digital thread traversal** — both
  named as deliberately deferred, not designed, pending real
  demonstrated need, unchanged since `WP 8.0A`.
- **A Contract Review reconciling `WP 8.0B`'s twelve contracts against
  `WP 8.0C`'s richer demands** — the Engineering Cockpit's own data
  needs, the Command Palette's own reach into `ICommandRegistry`, and
  the Inspector panel's own plausible `IDigitalThreadInspector` — remains
  the explicitly recommended step before a Cockpit/Palette
  implementation Work Package builds either.
- **A possible future revisit of `ADR-0066`** — only if true
  multi-window or multi-monitor support ever becomes a real,
  demonstrated need rather than a named ambition (`WP8.0C Workspace
  Behaviour Specification.md` §5-§6).

## 13. Key Takeaways

1. A user-facing product surface can, and should, be held to the
   identical architectural discipline every backend framework already
   demonstrated — "reuse what exists" is not a service-layer-only
   principle.
2. Separating reads (direct) from writes (Command-mediated) in
   presentation code is the same "one reason to change" discipline this
   project already applies everywhere else, simply applied to a new
   kind of code for the first time.
3. An architecture-only Work Package can, and should, still name real,
   locked-in decisions as ADRs rather than deferring everything to
   implementation — the difference is knowing which decisions are
   genuine boundaries (shape, mutation discipline, persistence, thread
   composition) versus which are empirical questions implementation
   should actually answer (rendering technology, extensibility contract
   shape).
4. A UX specification is not exempt from the same discipline: `WP 8.0C`
   named two genuine architectural boundary decisions as ADRs (default
   landing screen, global command discoverability) while leaving colour
   meanings, panel proportions, and keyboard bindings as product/UX
   record rather than inflating `docs/adr/` with decisions that do not
   constrain future architecture.
5. Disclosing a sequencing gap honestly (a shell shipped before its own
   UX specification existed) is more valuable than presenting the
   specification as if it were already satisfied — the "Today vs.
   Target" pattern `WP 8.0C` uses throughout is itself a reusable
   documentation discipline, not specific to the Workspace.
6. A same-assembly-only, concrete-class extension (`NavigationService.
   History`, `ProjectExplorer.CurrentPath`) is a legitimate way to add a
   real, needed capability the frozen public contracts never named,
   without reopening those contracts — the discipline is keeping the
   extension internal until a genuine cross-boundary need (a second
   assembly, a second implementation) actually forces a public contract
   change, not adding it speculatively.
7. An Accepted ADR's own worked example is a claim like any other —
   verify it against real, built code before the first Work Package that
   actually needs it relies on it, and correct it with a new ADR,
   openly, if it turns out not to hold (`ADR-0071`), rather than quietly
   working around it.

## Related Documents

`10-shell-and-application-composition.md`; `09-navigation-architecture.md`;
`11-command-framework.md`; `16-requirements-engine.md`; `ADR-0062`–
`ADR-0071`; `docs/releases/v0.8.0/WP8.0A Workspace Architecture
Document.md` and its four companion deliverables; `docs/releases/
v0.8.0/WP8.0B Workspace Contracts.md` and its three companion
deliverables; `docs/releases/v0.8.0/WP8.1A Implementation Report.md`;
`docs/releases/v0.8.0/WP8.0C UX Specification.md` and its eight
companion deliverables; `docs/releases/v0.8.0/WP8.1B Implementation
Report.md`;
`docs/academy/03 Work Packages/WP8.1A-workspace-shell-implementation.md`;
`docs/academy/03 Work Packages/WP8.0C-engineering-workspace-ux-specification.md`;
`docs/academy/03 Work Packages/WP8.1B-navigation-and-project-explorer-implementation.md`.
