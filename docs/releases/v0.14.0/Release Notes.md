# TempestOS v0.14.0 — Release Notes

**Status: in preparation.** Not yet merged to `main`, not tagged, not
published. This document is written at release-preparation time; the
Release Register carries the authoritative state.

---

## Summary

**v0.14.0 makes TempestOS durable, reviewable and internally consistent.**

Two things happened in this release, in that order. First, the platform
gained the capabilities that make it a real engineering application rather
than a demonstration of one: engineering objects and their attachments now
survive a restart, the workspace layout is a data-driven tree the user
arranges and keeps, documents and drawings can be viewed, projects carry
tasks, milestones, risks, issues and decisions, and the product no longer
ships inside its own demo harness.

Second — and this is the release's centre of gravity — a twelve-Work-Package
remediation programme closed the gap between what the platform had *decided*
and what it could *detect*. Command invocation had four parallel mechanisms;
it now has one canonical path and a build-time guard against a fifth
appearing. Architectural invariants that existed only as prose now fail the
build. And a pre-release audit found that the governance record had fallen
behind the repository it describes, which three further Work Packages
corrected.

This is the first release prepared specifically for a **physical review on
Windows**. `PHYSICAL_REVIEW.md` is its entry point.

## Headline changes

**Your work survives a restart.** Engineering object state is durable, and
each canonical type rehydrates itself through a Kind-keyed registry
(`ADR-0113`). Attachment content is durable bytes in the same store,
addressed by attachment Id and verified on read (`ADR-0114`).

**The workspace is yours to arrange.** The compile-time five-column docking
grid is replaced by a data-driven tree of splits, tab groups and floating
windows (`ADR-0095`), with responsive behaviour, ribbon minimisation, and
drag treated as a durable preference.

**Documents and drawings open.** A document viewer that rasterises through
a format-keyed page source, as an ordinary workspace panel (`ADR-0115`).

**Projects became project management.** Tasks with their own work state and
project membership by the parent chain (`ADR-0117`), plus milestones,
deliverables, risks, issues and decisions.

**One way to run a command.** The `TD-77` binding contract became canonical
across five staged changes, and the remediation programme retired what it
replaced: every live surface — Ribbon, Palette, Macro Manager, Cockpit, and
now the keyboard — invokes through `Evaluate(id, context)` then
`InvokeAsync(id, context, prompt, ct)`.

**The Cockpit stopped re-reading itself.** One Requirements refresh fell
from **1,140 persistence reads to 104**, and the per-requirement validation
pass now runs once per refresh instead of about eight times.

## Improvements

- The product no longer ships inside its own demo harness (`TD-75`); the
  harness is now deletable.
- Nine hand-copied settings stores became one `SettingsDocument<TDocument>`;
  corrupt persisted state now logs a warning naming the key instead of
  vanishing silently.
- Seven hand-written copies of the Desktop's status-bar/toast/history/refresh
  tail became one implementation, with the refresh gate reading
  `WorkspaceChanged` rather than `Succeeded`.
- `MainWindow` shrank from 1,577 to 1,052 lines as nineteen CRUD methods
  moved into two coordinators, verbatim.
- The pre-`TempestHost` v0.1 architecture — nine unreferenced types — was
  deleted, closing `TD-01`, open since `WP 2.6`.

## Bug fixes

- **Undo after a favourite toggle no longer half-completes.** `UndoRedoStack`
  raised its `Changed` event on whatever thread the undone action resumed
  on, so the toolbar refresh touched Avalonia state from the thread pool.
  The data change landed while the toast, status bar, history entry and
  refreshes never ran. Present in every release from `v0.10.0`; fixed here
  (`TD-117`, `ADR-0119`).
- **The Macro Manager's "Run" ran every step against no context**, so each
  object-scoped step reported "needs a selected object" however the
  workspace was selected. It did not throw, which is why it survived
  (`WP-A1`).
- **The keyboard input-binding path would have produced a dead key** for any
  real command bound to it. Migrated to the canonical path before any
  binding shipped (`WP-A2`).
- A `ReviseAsync` that carried half an object, a vocabulary check that
  scanned whatever happened to be loaded, and a Windows delete-while-locked
  test that read before releasing its lock.

## Architecture and governance

**Eight ADRs** (111 → 119): `ADR-0095`, `0113`–`0119`. Two came from the
remediation programme — `ADR-0118` (Kind eligibility is two mechanisms held
together by one invariant) and `ADR-0119` (UI-thread marshalling is the
Desktop's job; `Tempest.App` stays dispatcher-free).

**Five architectural invariants now fail the build** rather than existing
only as prose: dependency direction (`Desktop → App → Core`, no Avalonia
below the shell), the two command allow-list premises, the bounded
`Copy`-delegation set, and the dormant-command set.

**Technical debt is tracked honestly.** The register grew 56 → 118 rows
because an architecture audit converted findings into tracked debt rather
than leaving them as prose. Of those, this release closes or resolves
`TD-01`, `TD-105`, `TD-106`, `TD-107`, `TD-111`, `TD-112`, `TD-113`,
`TD-114` and `TD-117`, and narrows `TD-108`.

**Academy retrospectives**: 142 → **158**. The programme's own fifteen were
written by `WP-Z3`; `WP 13.13.2`'s was written by `WP-Z4`, which found that
Work Package had certified 38/38 retrospective completeness for `v0.13.x`
while lacking one itself.

## Validation status

| Gate | Result |
|---|---|
| Core tests, Debug / Release | **3,088 / 3,088** — 0 failures |
| Desktop tests, Debug / Release | **372 / 372** — 0 failures |
| Total | **3,460** (from 2,562 at `v0.13.1`) |
| Build, all four configurations, `TreatWarningsAsErrors=true` | 0 warnings, 0 errors |
| Governance health check | 7 passed, 1 pre-existing informational warning, 0 failed |
| ADR Register vs `docs/adr/` | 119 / 119, exact match |
| Working tree | Clean |

**Verification still outstanding at the time of writing:** the Build and
Test Gates must pass on `main` itself immediately before tagging
(Engineering Governance §7.3), and `release.yml` must succeed against the
tagged commit and publish both assets. A release is not shipped until that
second, independent verification passes.

## Accepted technical debt and known limitations

**Platform limitation — read this before reviewing.**

- **`TD-116` — the desktop application does not launch on Linux/X11.**
  `TypeLoadException` on `Tmds.DBus.Protocol.Connection` during Avalonia's
  X11 platform initialisation, before any window exists. The cause is a
  deliberate security pin (`Tmds.DBus.Protocol` 0.94.2, remediating
  `GHSA-xrw6-gwf8-vvr9`) against which `Avalonia.FreeDesktop 11.2.3` binds
  the 0.20.0 type layout. **Windows and macOS never initialise that path
  and are unaffected.** Building and the full test suite are unaffected on
  Linux, because `Avalonia.Headless` does not initialise X11 either — which
  is why nothing in the repository had noticed. Not fixed: the remedies are
  reinstating a known high-severity advisory or an Avalonia upgrade, and
  neither should be chosen implicitly.

**Accepted technical debt**

- `TD-108` — 60 executable blocking calls remain, of which 36 wait on an
  already-completed `Task`. No deadlock is possible: `Tempest.Core` and
  `Tempest.App` use `ConfigureAwait(false)` throughout and reference no
  dispatcher. A residual `O(N²)` inside `RequirementValidationService` is
  pinned by a test named for it.
- `TD-109` — `MainWindow` is 1,052 lines and still the largest file in the
  repository. What remains is the composition root and the shell services.
- `TD-115` — three commands are implemented and registered but have no
  construction path, awaiting the object picker `FCR-0073` will provide.
  Pinned both ways by test.
- `AT-26` — `IWorkspaceViewFactory.Create` is synchronous by contract; its
  implementations bridge asynchronous reads.

**Not defects — decided positions**

- `AT-10` — **REST is not activated, and no production command is reachable
  over HTTP.** Of 74 descriptors, 18 are declared unavailable and every one
  of the 56 invocable ones either needs a selected object or declares
  parameters, so the set invocable with an empty context and no prompt is
  empty. Activation needs a request-to-context contract, a parameter source
  and an authentication mechanism that does not exist.
- `AT-23` — the keyboard ships with **zero default bindings** and no
  remapping UI, by product choice. The routing behind it is now correct.
- `TD-118` — full async conversion of the Cockpit read surface, deferred;
  revisit when persistence becomes slow or remote.

**Two behaviours worth knowing before the review**

- **Where you launch from decides where your data goes.** Persistence
  resolves the relative path `persistence-data` against the process working
  directory, not the install location.
- **A loopback HTTP listener binds `127.0.0.1:5080`** at startup. Failure is
  isolated and non-fatal.

## Physical-review platform qualification

| Platform | Status |
|---|---|
| **Windows** | **Verified.** CI runs on `windows-2022`; the application is known to launch. The review platform. |
| macOS | Expected to work; not independently verified this release. |
| Linux | **Builds and tests clean; the application does not launch** (`TD-116`). |

Minimum environment: .NET SDK **10.0.302** (`rollForward: latestFeature`;
any 10.0.3xx satisfies it). No workloads, no Visual Studio, no Node, Python
or Docker, no database, no secrets, no licence, no environment variables.
Verified from a clean clone with an isolated NuGet package cache.

Full instructions, smoke test and reset procedure: **`PHYSICAL_REVIEW.md`**.

## Next milestone

The physical review itself. Its findings, together with `TD-116`'s
dependency decision and the deferred items above, set the shape of the next
release. Nothing in this release commits to that scope in advance.

---

*Work Package inventory: `docs/releases/v0.14.0/WorkPackages.md`.
Readiness assessment: `docs/releases/v0.14.0/Engineering Release Report.md`.*
