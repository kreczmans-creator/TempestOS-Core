# Product Spine Architecture

**Status:** Implemented (first increment), 2026-08-28 ·
**Debt:** `TD-84` (P0 — Product Spine), `TD-85` ·
**Code:** `Tempest.App.Projects`, `Tempest.App.Shell`, `Tempest.Desktop.Views`

## The product decision this realises

TempestOS is a **project-centric engineering operating environment**.
Engineering is not a standalone application that happens to sit inside
TempestOS; **projects are the organisational context engineering work
occurs within.**

```
TempestOS → Module → Project → Workspace → Engineering Object → Evidence
```

## The deficiency this closes

A compliance audit (`e40a3d6`) found four apparently separate P0 gaps: no
global navigation, no project context, no per-discipline engineering
surfaces, no project modules. They are **one architectural deficiency**:
the Product Shell / Project Context layer did not exist. The engineering
platform underneath was strong; nothing connected it into a product.

Recording them as four tickets would have invited four disconnected
features — a projects screen, a rail, a dashboard, some links — with no
relationship between them. They are therefore grouped as `TD-84`.

## What the spine is

Three services in `Tempest.App`, and the shell that renders them.

### 1. `IProjectDirectory` — projects are real engineering objects

A project **is** an `IProject` engineering object (`Portfolio → Programme
→ Project`, `WP 8.2C`), created through the same
`EngineeringObjectFactory<T>` every discipline module uses. It therefore
arrives with lifecycle, relationships, revisions, traceability, audit and
principal attribution already working.

The directory adds only what the domain does not offer: enumeration by
kind, business-identifier uniqueness (`TD-38` leaves that unenforced), and
project contents via the existing `IHasParent` edge.

**The pre-platform `ProjectModel`/`ProjectService`/`IProjectRepository`
cluster was investigated and deliberately not reused.** It news up its own
repository, writes folders straight to disk, and bypasses
`IPersistenceStore`, audit, revisions and lifecycle. Its useful *concepts*
(the `P-NNNN` identifier scheme, customer, owner) carry forward; its
implementation is retired, not wired in.

### 2. `IProjectContext` — the current project as real state

Which project the user is in is **application state with a lifecycle, an
event, and persistence** — never a caption a view sets on itself:

- `Current` / `HasProject`
- `OpenAsync` / `CloseAsync` / `RefreshAsync`
- `ProjectContextChangedEvent` on the existing `IEventBus`
- persisted through the same `ISettingsProvider` substrate `WorkspaceState`
  uses (`ADR-0064`)

Only the project's **Id** is persisted. Persisting its name or status
would create a second, drifting source of truth for data the domain owns.

### 3. `IShellNavigator` — navigation as one value

`ShellLocation(ShellArea, ProjectId?, ProjectArea?)` is a single immutable
value. Navigation state being one value rather than flags spread across
views means "where am I" has exactly one answer, the shell cannot render
two surfaces that disagree, and a test asserts a location rather than a
sequence of visibility side effects.

**The invariant the navigator exists to enforce:** a project-scoped
location and the current project can never disagree. `OpenProjectAsync`
opens the context *first* — so a failed open cannot leave the shell
pointing into a project that was never opened — and every project-scoped
verb requires an open project.

That is what makes `GoToEngineeringAsync` throw with no project open.
Engineering is reachable **only** from a project, by construction rather
than by convention.

## The shell that renders it

`MainWindow` gained one rule: **a single module host renders whatever the
navigator reports.** There is exactly one place that decides what is on
screen, and it derives that from `IShellNavigator.Current`.

- `GlobalNavigationRail` — a view over the navigator, not a second
  navigation model. Shows only modules the platform can genuinely serve;
  a rail button that opens nothing would be the fake navigation the
  programme forbids (`TD-81` tracks the rest).
- `ProjectBrowserView` — lists real projects, opens through the navigator.
- `ProjectWorkspaceView` — the project's real identity, lifecycle and
  *counted* contents, with areas as tabs and "Enter Engineering" as the
  route into the Engineering Workspace.
- The Status Bar shows the real current project. Before the spine that
  segment read "📁 No project" permanently, because nothing could set it.

The existing Engineering surface — ribbon, docking grid, explorer,
inspector, output — is **unchanged**, and is now one module inside the
shell rather than the whole application.

## The boundary the spine exposed

The Definition-of-Done journey failed at "close and reopen TempestOS"
until a real architectural boundary was crossed: **the engineering object
graph is in-memory by design** (`ADR-0077`). Documents persist; the
objects reconstructed over them do not.

`ProjectDirectory` therefore keeps its own small durable
`Projects.Index` in `IPersistenceStore` — the identical, already-approved
pattern `MaterialCatalog` uses for exactly the same reason (`ADR-0055`),
never a second storage mechanism. The live object stays authoritative
whenever it exists; the index is the fallback that makes a project
reopenable.

This is a **workaround for projects, not a fix for the boundary.** Every
other engineering object still vanishes from the object graph on restart.
Recorded honestly as `TD-85`; the real fix is rehydrating the repository
from the durable document store at startup.

## What is proven

The Definition-of-Done journey runs end to end through the real
`MainWindow` over a real `WorkspaceHost`:

launch → Home → Projects → create → open → Project Workspace → current
project visible in the shell → enter Engineering → real ribbon and
docking grid → return with context intact → close → **reopen → project
and location both recovered**.

Plus the three commissioned engineering traces (Project → Component →
Material → Calculation → Validation → Result; Project → Requirement →
Verification → Evidence; Project → Drawing/Document → Engineering Object),
each against the real `MaterialCatalog`, `RequirementsService` and
`VerificationService`.

Four mutations were run and killed: engineering reachable without a
project; a phantom project location restored; a non-existent project
opened; the durable index not written.

## What this deliberately did not do

Full docking (`TD-72`), the workspace layout abstraction, the drawing
viewer (`TD-80`), the remaining project modules (`TD-81`) and Companion
integration (`TD-82`) are later objectives in the programme's own order.
The spine had to exist first; it now does.
