# WP 9.0A — Mechanical Product Structure — Technical Debt Assessment

## Purpose

Reviews the Technical Debt Register for items this Work Package's own
implementation created, extended, or should have created and did not.

## New Item

### `TD-26` — Runtime Host Module-Initialisation Timing Is Not Awaited Before `WorkspaceManager.StartAsync` Returns

**What.** `WorkspaceManager.StartAsync`'s own `WaitForServicesAsync`
returns as soon as `ITempestHost.Services` becomes non-null.
`TempestHost.cs`'s own phase order sets `Services` (`"Dependency
Injection Built"`) *before* running `ModuleLifecycleManager.InitialiseAllAsync`
(`"Module Initialisation"`) — so a Workspace read taken immediately after
`StartAsync` returns can occur before a module's own `InitialiseAsync`
(a `NavigationItem` registration, seeded Engineering Domain data) has
run.

**How it was found.** Manual console verification of this Work Package's
own Mechanical area: piped, non-interactive input against `Program.cs`
(and directly against the built `Tempest.App.dll`, ruling out a `dotnet
run` wrapper artefact) repeatedly showed `Areas: 0`/`No Mechanical
Project yet` even several real seconds after startup — inconsistent with
a simple "wait a little longer" race.

**Confirmed pre-existing, not introduced by this Work Package.** Rebuilt
and ran the identical scenario against a disposable `git worktree`
checked out at the unmodified `v0.8.0` tag (no `WP 9.0A` code present):
the identical `Areas: 0` behaviour reproduced. This is a latent Runtime
Host/`WorkspaceManager` characteristic, not a defect in Mechanical
Product Structure's own code.

**Why automated tests are unaffected.** Every `WP 9.0A` test that needs
seeded data explicitly polls the seeded module's own `HasRegistered`
flag before proceeding (`MechanicalWorkspaceIntegrationTests`) — a
deterministic, disclosed test-level fix, not a production code change.
All 1695 tests pass, four consecutive clean runs.

**Why this is debt, not merely a limitation.** A real interactive user,
who takes real seconds to read a screen before typing, is unlikely to
notice; a scripted or fast-typing session might see a stale first render.
No data is lost or corrupted — only a render is transiently stale, and
every subsequent render is correct once background initialisation
completes (confirmed complete, per Runtime Host logs, well under one
second after `Services` becomes non-null in this Work Package's own
scenario).

**Revisit trigger.** Any future Work Package building a scripted,
automation-driven, or latency-sensitive Workspace consumer, where a
stale first read would be user-visible or consequential.

**Disposition.** Accepted, disclosed, not fixed by this Work Package — a
Runtime Host/`WorkspaceManager` concern spanning every module, not
specific to Mechanical Product Structure; fixing it correctly (for
example, awaiting `ModuleLifecycleManager.InitialiseAllAsync` before
`WaitForServicesAsync` returns) is a genuine platform-level change
requiring its own review, out of this Work Package's own "no
architectural redesign" scope.

## Existing Items Reviewed for Extension or Change

- **`TD-25`** (no concurrency-conflict detection on `ReviseAsync`/
  `SetStatusAsync`) — extended in spirit by `RenameAsync`/`MoveAsync`/
  `DeleteAsync`, which carry the identical, disclosed lack of
  compare-and-swap protection. Not separately re-registered; see `WP9.0A
  Security Review Report.md`.
- **`TD-22`/`TD-24`** (no bound on recorded volume, linear-scan reads) —
  the same pattern recurs in `MechanicalProductStructureNodeProvider`'s
  own `ListAllAsync`-and-filter approach and `DeleteAsync`'s own
  has-children check. Not separately re-registered.
- **`WP8.9.0`'s own disclosed "zero dedicated Security Reviews this
  release" finding** — closed by `WP9.0A Security Review Report.md`,
  the first dedicated Security Review since.

## Items Considered and Not Raised

- **Drag-and-drop / multi-selection** — not Technical Debt: consciously,
  explicitly deferred per the Work Package's own "if supported by
  current UI technology" clause, not a defect. Recorded in the Future
  Capability Register instead.
- **`createDefault` omitted from all six Mechanical `CommandDescriptor`s**
  — not Technical Debt: a disclosed, reasoned scope boundary (no
  meaningful parameterless default exists), not an oversight. Recorded
  in the Future Capability Register.
- **`IAssembly.ChildIds`/`ISubAssembly.ParentAssemblyId` staleness after
  a `Move`** — not separately raised as Technical Debt here: already
  fully disclosed and reasoned in `ADR-0081` itself, which is the more
  precise, permanent record for a deliberate, ADR-ratified design
  trade-off rather than an open-ended debt item.

## Verdict

**One new item (`TD-26`), formally registered.** Two existing items'
own dispositions extended, not worsened. One prior release's own
disclosed gap (dedicated Security Review) closed.

## Related Documents

`docs/governance/Quality/Technical Debt Register.md`; `WP7.3A Security
Review Report.md` (`TD-25`); `WP8.9.0 Release Readiness Report.md`;
`ADR-0081`; `WP9.0A Future Capability Assessment.md`.
