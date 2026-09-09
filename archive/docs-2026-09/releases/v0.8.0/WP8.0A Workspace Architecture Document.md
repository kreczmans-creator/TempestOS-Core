# WP 8.0A — Engineering Workspace Architecture Document

## Purpose

The complete architecture for the TempestOS Engineering Workspace — the
user-facing surface an engineer actually works in. Architecture and
design only; **no implementation, no code written**. This document is
the master reference; `UI Architecture.md`, `Navigation
Specification.md`, `Object Relationship Diagrams.md`, and `User
Workflow Diagrams.md` each go deeper into one area named below.

## 0. Where This Sits in the Platform

Every capability TempestOS has shipped through `v0.7.0` is either
Runtime Foundation, a Platform Service, or Engineering Core/Systems
Engineering Foundation capability (`VISION.md`). None of it is a
user-facing product surface beyond `TempestShell` — a minimum-viable,
text-console Shell (`WP 5.0D`) presenting a Navigation Region, a
Content Region, and a reserved Status Bar, reading numbered menu
selections from a `TextReader` and writing to a `TextWriter`
(`src/Tempest.App/Shell/TempestShell.cs`). It renders `PlaceholderPage`
stubs; no Engineering Core object (a Requirement, a Material, a
Calculation Record, a Verification Record) has ever been displayed to a
user.

**The Engineering Workspace is the first real, user-facing engineering
product surface TempestOS designs.** It is not a new platform layer —
it is `Tempest.App`'s own composition root (`ADR-0033`), evolved from a
console shell into a graphical, multi-panel desktop application, built
entirely on Platform Services and Engineering Core services that
already exist and are already certified. `INavigationProvider` has
explicitly anticipated this since `WP 5.0A`: `NavigationItem`'s own
`Icon` is "a symbolic key only... resolving what, if anything, an `Id`
or `Icon` actually looks like on screen is entirely `Tempest.App`'s (or
any future UI shell's) own responsibility." This architecture is that
future UI shell, designed now that real Engineering Core capability
exists for it to actually present.

## 1. Workspace Philosophy

1. **The Workspace is additive, not a replacement of the platform's own
   architecture.** It introduces zero new Platform Service, zero new
   storage mechanism, and zero new authorization model. Every
   capability it presents already exists: Navigation
   (`INavigationProvider`), commands (`ICommandDispatcher`/
   `ICommandRegistry`), Diagnostics (`IDiagnosticsProvider`), Settings
   (`ISettingsProvider`), and the Engineering Core/Systems Engineering
   Foundation services (`IEngineeringDocumentStore`,
   `IMaterialCatalog`, `ICalculationEngine`, `IVerificationService`,
   `IRequirementsService`). The Workspace's own job is composition and
   presentation, not new capability.
2. **Every engineering object is explorable in place — no forced
   navigation away from context.** An engineer inspecting a Requirement
   sees its lifecycle status, revision history, and every linked
   relationship without leaving the Requirement; following a
   relationship opens a new context alongside the first, never
   replacing it outright (see §7, Digital Thread Visualisation).
3. **Reads are direct; writes are commands.** Every action that changes
   engineering data — revising a requirement, changing its status,
   recording a relationship — dispatches through the existing Command
   Framework (`ICommandDispatcher`), never a View calling
   `IRequirementsService.ReviseAsync` (or any sibling mutator) directly.
   Reads (`FindAsync`, `ListAsync`, `GetRelationshipsAsync`,
   `GetEvidenceAsync`) may be called directly from a View — they change
   nothing, and gating them behind a Command would only add ceremony
   with no benefit (see `ADR-0063`).
4. **The Workspace does not know what an Engineering Discipline Module
   is.** It presents whatever `INavigationProvider` and
   `ICommandRegistry` already expose, plus a reserved extension point
   for a module to contribute its own object views (§10) — it never
   hard-codes knowledge of Requirements, Materials, Calculations, or
   Verification specifically at the composition-root level, mirroring
   how `TempestShell` today has zero compiled-in knowledge of
   `ClockModule`'s own behaviour.
5. **Disclose, don't invent.** Where this document names an open
   question rather than a decision, it says so explicitly (§11), rather
   than presenting an implementation-phase judgment call as if already
   settled — the same discipline `VISION.md`'s own Product Principle 2
   already requires of every other document in this governance suite.

## 2. User Journeys

Five representative journeys, each exercising a different part of this
architecture. Full step-by-step diagrams: `WP8.0A User Workflow
Diagrams.md`.

1. **Browse and inspect a requirement.** An engineer opens the
   Workspace, expands the Requirements node in the Project Explorer,
   selects a requirement, and reads its statement, status, and revision
   history in the Properties panel — zero writes, zero commands.
2. **Trace the digital thread.** From an open requirement, the engineer
   opens its Digital Thread panel, sees its verification history and
   linked references composed in one place (`GetEvidenceAsync`), and
   follows a link to the calculation record that justified a
   verification outcome — opening it in a new tab alongside the
   requirement, not replacing it.
3. **Revise a requirement.** The engineer edits a requirement's
   statement in its editor tab and invokes the "Revise Requirement"
   command (Command Framework); the Workspace does not call
   `IRequirementsService.ReviseAsync` directly.
4. **Change lifecycle status.** The engineer selects "Approve" from a
   context menu populated from `ICommandRegistry`, filtered to commands
   applicable to a Requirement in `Draft`/`Reviewed` status; an invalid
   transition is rejected by `RequirementStatusTransitions` exactly as
   it is today, surfaced to the engineer as a command failure, not a
   silent no-op.
5. **Discover what a module contributes.** A future Engineering
   Discipline Module registers its own navigation items, commands, and
   (once the reserved extension point is answered, see §11) its own
   object views — the Workspace presents them exactly as it presents
   every Engineering Core capability, with zero code change to the
   Workspace itself.

## 3. Main Window Layout

Full detail: `WP8.0A UI Architecture.md` §1.

The Workspace replaces `TempestShell`'s own three-region model
(Navigation Region, Content Region, Status Bar) with five regions:
**Command Bar** (top), **Project Explorer** (left dock), **Document
Area** (centre, tabbed), **Properties/Digital Thread panel** (right
dock), **Status Bar** (bottom) — a direct, one-for-one evolution: the
Navigation Region becomes the Command Bar's own top-level menu plus the
Project Explorer's own top-level nodes; the Content Region becomes the
tabbed Document Area; the Status Bar is finally populated, no longer
reserved.

## 4. Navigation Model

Full detail: `WP8.0A Navigation Specification.md` §1-2.

Two tiers, not one: **global navigation** (top-level areas — Home,
Requirements, Materials, Calculations, Verification, Settings —
sourced from `INavigationProvider.Items` exactly as `TempestShell`
already consumes it, zero change to that service) and the **Project
Explorer** (a richer, per-area hierarchical tree of actual engineering
objects, composed from each area's own service — `IRequirementsService.
ListAsync`/`GetRelationshipsAsync` for Requirements, and the equivalent
read methods for Materials/Calculations/Verification). Global
navigation answers "what areas exist"; the Project Explorer answers
"what objects exist within the selected area, and how are they
related."

## 5. Project Explorer

Full detail: `WP8.0A Navigation Specification.md` §3.

A tree per engineering area: for Requirements, `RequirementGroup`'s own
existing parent/child hierarchy (`CreateGroupAsync`'s own
`parentGroupId`) forms the tree's own branch structure;
`RequirementCollection` membership is a second, cross-cutting
grouping, presented as a filterable view rather than a second parallel
tree, since a requirement may belong to multiple collections but only
one group's own primary hierarchy. Materials, Calculations, and
Verification Records — none of which currently have a group/collection
concept of their own — are presented as flat, sortable lists until a
real need for their own hierarchy is identified (no capability is
invented ahead of evidence, per `VISION.md`'s own Product Principle 3).

## 6. Engineering Object Hierarchy

Full detail: `WP8.0A Object Relationship Diagrams.md`.

Every engineering object the Workspace presents is, underneath, an
`IEngineeringDocument` of some `Kind` (`"Requirement"`,
`"RequirementCollection"`, `"RequirementGroup"`, `"Material"`,
`"CalculationRecord"`, `"VerificationRecord"`, and whatever `Kind`
values a future Engineering Discipline Module introduces). The
Workspace's own presentation model reflects this directly: every object
view shares a common set of facets — **Identity** (`Id`, business
identifier where one exists), **Revision History** (every
`IDocumentRevision`), **Provenance** (who created/last revised it,
when), and **Relationships** (every `DocumentReference` with this
object as source or target) — plus whatever discipline-specific facets
that object's own `Kind` adds (a Requirement's own `RequirementStatus`;
a Calculation Record's own assumptions and constraints). This is a
presentation-layer observation, not a new abstraction: the Workspace
does not need a new "Engineering Object" interface in
`Tempest.Core` to achieve this, since every fact it needs is already
exposed by `IEngineeringDocumentStore`/each framework's own service.

## 7. Docking Strategy

Full detail: `WP8.0A UI Architecture.md` §2.

Project Explorer, Properties, and Digital Thread panels are dockable,
resizable, and independently closable/reopenable; the Document Area is
always present and always tabbed (never dockable away, since it is the
Workspace's own primary work surface). A default layout is defined;
user rearrangement is persisted per-user (§9). No panel may be
undocked into its own separate top-level window in this architecture's
own first iteration — multi-window support is an explicitly deferred,
disclosed future capability (§11), not assumed.

## 8. View Architecture

Full detail: `WP8.0A UI Architecture.md` §3.

A **View** renders exactly one engineering object, one relationship
list, or one composed digital-thread read — never more than one
concern, mirroring `FOUNDATION.md`'s own "one reason to change"
discipline applied to presentation code for the first time. A View
reads its own data directly from the owning service (`IRequirementsService`,
`IMaterialCatalog`, etc.) and renders it; a View never calls a mutating
method on any service directly (§1, Point 3) — every mutating
interaction dispatches a Command instead. This split is the Workspace's
own equivalent of `ICommandDispatcher`/`ICommandRegistry`'s already-
established split between "a caller with a concrete typed instance"
and "a caller with only an Id" — reads are direct because a View always
already knows exactly which object it is displaying; writes go through
Commands because a Command is independently testable, auditable
(through whatever audit trail a future Work Package wires the Command
Framework to), and reusable from a menu, a keyboard shortcut, or a
future automation caller, exactly as `ICommand`'s own contract already
anticipates (`FCR-0024`).

## 9. Digital Thread Visualisation

Full detail: `WP8.0A Object Relationship Diagrams.md` §3;
`WP8.0A User Workflow Diagrams.md` (Journey 2).

The Digital Thread panel renders exactly what
`IRequirementsService.GetEvidenceAsync` (and each sibling framework's
own equivalent composed read, where one exists) already composes:
verification history plus linked references, presented as a navigable
list — each entry showing its own relationship kind
(`RequirementRelationshipKinds`), target object identity, and a
"jump to" action that opens the target in a new Document Area tab.
This is a **list-based** presentation in this architecture's own first
iteration, not a graph-drawing visualisation — a full interactive graph
view is an explicitly deferred future capability (§11), since no real
need for one has been demonstrated yet and a list already presents
every fact `GetEvidenceAsync` provides without requiring any new
platform capability to build.

## 10. Extensibility Model

A future Engineering Discipline Module extends the Workspace through
three existing or reserved mechanisms, never by the Workspace adding
compiled-in knowledge of that module:

1. **Navigation** — `INavigationProvider`, already implemented,
   unchanged. A module contributing a new top-level area registers a
   `NavigationItem` exactly as `NavigationSampleModule` already does.
2. **Commands** — `ICommandRegistry`, already implemented, unchanged. A
   module contributing a mutating action registers a `CommandDescriptor`
   exactly as every `v0.6.0`/`v0.7.0` sample module already does.
3. **Object views** — **reserved, not designed here.** The Workspace
   needs a way to resolve "what View renders an `IEngineeringDocument`
   of `Kind = X`" without a compiled-in `switch` over every known
   `Kind`. The shape of this contract (an `IWorkspaceViewProvider`-style
   registration, keyed by `Kind`, mirroring `IReportDefinition`'s own
   registration pattern in Reporting) is named here as a real, necessary
   capability, but its own interface design is explicitly deferred to a
   Contract Review Work Package — reserved as `ADR-0067` (§11) rather
   than designed ahead of a concrete need to validate the shape against.

## 11. Interaction Patterns

Full detail: `WP8.0A UI Architecture.md` §4.

- **Select-to-inspect**: selecting an item in the Project Explorer
  updates the Properties panel; no navigation, no new tab.
- **Open-to-edit**: double-click (or an explicit "Open" command) opens
  the object in a new Document Area tab, or focuses its existing tab if
  already open — never a second tab for the same object.
- **Context menu**: right-click populates from `ICommandRegistry`,
  filtered to descriptors whose own `Category` and applicability match
  the selected object's own `Kind` and current state (mirroring how
  `SetStatusAsync`'s own transition table already restricts which
  status changes are valid from a given current status).
- **Jump-to-relationship**: from the Digital Thread panel, opens the
  target object in a new Document Area tab, never replacing the
  source's own tab.
- **Keyboard navigation**: Project Explorer and Document Area tabs are
  both keyboard-navigable; specific key bindings are an implementation-
  phase concern, not fixed here.

## 12. Workspace State Management

Full detail: `WP8.0A UI Architecture.md` §5.

Workspace state — panel layout (docked positions, sizes, open/closed),
open Document Area tabs, and last-selected Project Explorer node — is
**per-user, runtime-mutable state**, the exact shape `ISettingsProvider`
(`Tempest.Core.Settings`, `WP 6.4`) already exists to hold. No new
persistence mechanism is introduced (`ADR-0064`): the Workspace reads
its own layout at startup via `ISettingsProvider.GetValueAsync` and
writes it back on change via `SetValueAsync`, exactly as
`SettingsSampleModule` already demonstrates the pattern for any other
runtime-mutable value.

## ADR Summary

Four genuine, locked-in architectural decisions are written as full
ADRs (not merely reserved) because each is a boundary decision the
Workspace's own shape depends on, not an implementation detail an
empirical build would need to validate first:

| ADR | Decision |
|---|---|
| `ADR-0062` | The Engineering Workspace is a graphical, multi-panel evolution of `Tempest.App`'s own composition root, additive to (not replacing) console `TempestShell`, introducing zero new Platform Service |
| `ADR-0063` | Workspace Views read Engineering Core/Platform services directly; every mutating action dispatches through the existing Command Framework |
| `ADR-0064` | Workspace layout/session state is persisted via the existing `ISettingsProvider`; no new persistence mechanism |
| `ADR-0065` | Digital Thread visualisation composes existing relationship/evidence reads (`GetEvidenceAsync` and siblings); no new traversal or query platform service |

Two genuine open questions are named, not answered, and their ADR
numbers are reserved for a future Contract Review Work Package:

| ADR (Reserved) | Open Question |
|---|---|
| `ADR-0066` | Concrete graphical UI rendering technology (e.g. a specific .NET desktop UI framework) — an implementation-phase evaluation, not an architecture-phase decision |
| `ADR-0067` | The object-view extensibility contract shape (§10, Point 3) — a real interface design, deferred until a Contract Review Work Package can validate it against a concrete second consumer, not designed speculatively here |

## Deliberately Out of Scope

- **No implementation.** Zero code, zero interface definitions in
  `src/`, per this Work Package's own explicit constraint.
- **No concrete UI framework decision** (§11, `ADR-0066` reserved).
- **No multi-window support** — deferred, not designed (§7).
- **No full interactive graph visualisation** for the digital thread —
  a list view is this architecture's own complete answer for now (§9).
- **No object-view extensibility contract design** — named as a real
  need, not designed (§10, `ADR-0067` reserved).
- **No changes to any existing Platform Service or Engineering Core
  contract** — `INavigationProvider`, `ICommandDispatcher`/
  `ICommandRegistry`, `ISettingsProvider`, `IEngineeringDocumentStore`,
  and every framework built on it are all consumed exactly as they
  exist today, unmodified.

## Related Documents

`WP8.0A UI Architecture.md`; `WP8.0A Navigation Specification.md`;
`WP8.0A Object Relationship Diagrams.md`; `WP8.0A User Workflow
Diagrams.md`; `ADR-0062`–`ADR-0065`; `VISION.md`;
`docs/architecture/Navigation Framework Architecture.md`;
`docs/architecture/Shell & Composition Framework Architecture.md`;
`docs/architecture/Command Framework Architecture.md`;
`docs/academy/02 Runtime Architecture/17-engineering-workspace.md`.
