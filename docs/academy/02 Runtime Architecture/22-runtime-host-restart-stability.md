# Runtime Host — Restart Stability & Readiness Signalling

## 1. Introduction

This is the Academy's own concept guide for `WP 10.1B` — the Work
Package that investigated and resolved `TD-26` and `TD-37`, two
long-disclosed Technical Debt items concerning, respectively, the
Runtime Host's own readiness signal and the durability of sample-data
seeding across a genuine application restart.

## 2. Purpose

To explain why two seemingly different symptoms — a Workspace consumer
occasionally reading stale data, and four sample modules occasionally
failing to initialise — turned out to share a family resemblance
without sharing a root cause, and why finding that out required reading
real source, not re-mitigating a second time.

## 3. Background

`TD-26` was first disclosed `WP 9.0A`: `WorkspaceManager.StartAsync`
returns as soon as `ITempestHost.Services` is resolvable, not once
every module has finished initialising. Two later Work Packages
(`WP 10.0B`, `WP 10.1A`) each hit its real consequences directly and
each mitigated it the same way — one layer up, in `Tempest.Desktop`,
polling `IDiagnosticsProvider.HostState` before proceeding — without
ever touching `WorkspaceManager` itself. `TD-37` was disclosed
`WP 10.1A`: four `Tempest.Samples` modules failed their own
`InitialiseAsync` against their own literal identifiers, root cause
explicitly left undiagnosed pending a future, dedicated Work Package.
`WP 10.1B` is that Work Package.

## 4. The Problem

Two named items, one controlling instruction: "Determine the true root
cause(s)." The register's own prior entries had already ruled out
several explanations for `TD-37` (cross-test contamination, a retry
mechanism) but left the true mechanism open. Re-mitigating a third time,
in a third location, was explicitly not what this Work Package was
asked to do — it was asked to find the actual, underlying cause and fix
it there.

## 5. The Design

**`TD-26`:** re-reading `TempestHost.ExecuteStartupPhasesAsync` directly
confirmed the exact phase gap the register already described —
`Services` is set at Phase 6, Module Initialisation/Start is Phase 7.
The fix: wait for the *authoritative* signal
(`IDiagnosticsProvider.HostState == HostState.Running`, set only once
Phase 7 completes) at `WorkspaceManager`'s own source, not a second time
one layer up.

**`TD-37`:** the register's own remaining candidate — double-invocation
— was directly disproved by re-reading `ModuleLifecycleManager`: no
retry, no re-entry, a `TrackedModule.State` guard preventing exactly
that. The real mechanism required looking one layer *down*, not
sideways: `IMaterialCatalog`/`IRequirementsService` both durably index
their own uniqueness constraint via `IPersistenceStore`, which is
real, file-backed, and — by design (`ADR-0041`) — shared across every
process launched from the same working directory. Direct file-system
evidence (`SAMPLE-MAT-001` already present in the test project's own
build output before any second launch was deliberately triggered)
confirmed it beyond doubt: a first successful launch durably writes
these identifiers; every later launch's own re-seeding then collides
with its own prior work.

## 6. Alternatives Considered

- **Re-mitigate `TD-26` a third time, in a fourth location:** rejected
  — every prior mitigation already proved the correct signal; the only
  question left was where to apply it permanently.
- **Change `PersistenceStore`'s own default root path for `TD-37`:** a
  production behavioural change (moving durable data to a
  per-user-profile location) with a far larger blast radius than this
  Work Package's own scope justified, and orthogonal to the actual
  defect (fixed-identifier sample seeding assuming an always-empty
  store).
- **Fix `TD-37` by adding uniqueness enforcement to
  `EngineeringObjectFactory<T>`:** would have required an Engineering
  Domain change, explicitly forbidden by this Work Package's own
  scope — disclosed instead, as new item `TD-38`.

## 7. Why This Solution Was Chosen

Both fixes were placed at the actual owning layer of the actual defect:
`WorkspaceManager` owns the readiness contract every consumer depends
on; `Tempest.Samples` owns the literal identifiers that collide. Neither
fix required touching the Engineering Domain, the Workspace contracts,
or the desktop UX — matching the controlling instruction's own explicit
constraints precisely.

## 8. Architectural Principles

- **A mitigated symptom is not a resolved cause.** Two prior Work
  Packages each proved the correct signal existed and worked — applying
  it at the true source, once, was strictly better than a third
  mitigation layer.
- **Durable-by-design persistence has real consequences for anything
  that assumes an empty store.** `ADR-0041`'s own deliberate choice
  (real, cross-launch durability) is correct and unchanged; the defect
  was sample data written *as if* that choice hadn't been made.
- **Idempotency, not fragility, is the correct contract for seed data.**
  A module that seeds fixed, demonstrative data into a real, durable
  store must expect to run more than once against it — check first,
  don't assume empty.
- **Scope boundaries survive even a visible, nearby, cheaper-looking
  fix.** `TD-38` was found in the same investigation as `TD-37` but
  requires an Engineering Domain change — disclosed, not attempted.

## 9. Benefits

- Every `IWorkspaceManager` consumer — not just `Tempest.Desktop` —
  now receives a genuinely correct readiness guarantee.
- A real, repeated application restart from the same working directory
  no longer crashes sample-data seeding — a genuine product-facing
  stability fix, not only a test-infrastructure one.
- `Tempest.Desktop.Tests` gained the same test-isolation discipline
  `Tempest.Core.Tests` has held since `WP 7.3A`, eliminating an entire
  class of run-to-run test pollution as a side effect.
- Three previously honest-empty Cockpit assertions now demonstrate real,
  stable, live data — proof the underlying fix works, not a Cockpit
  code change.

## 10. Trade-offs

- On the rare idempotent-skip branch (a genuine, real second launch
  against already-seeded data), a sample module's own `Sample*Id`
  properties are left unset rather than recovered — no
  lookup-by-business-identifier capability exists to recover them, and
  building one was judged disproportionate to a demonstration module's
  own needs.
- `TD-38` remains open, by design — a deliberate scope boundary, not an
  oversight, and named explicitly as a future Work Package's own
  starting point.

## 11. Common Mistakes

- Treating a working mitigation as evidence the underlying cause is
  understood — `TD-26` was mitigated twice before anyone traced it to
  its actual source.
- Assuming a "module fails initialisation" symptom must live inside the
  module pipeline itself — the actual defect here lived one layer
  below, in how a Platform Service's own durable uniqueness constraint
  interacted with sample data that assumed it wouldn't exist yet.
- Fixing a nearby, visible, related defect (`TD-38`) just because it
  was found during the same investigation, without checking whether it
  falls inside the current Work Package's own stated scope.

## 12. Future Evolution

A dedicated Engineering Domain Work Package could resolve `TD-38`
directly — either real business-identifier uniqueness enforcement on
`EngineeringObjectFactory<T>`, or a durable-store rehydration capability
for `IEngineeringObjectRepository`, restoring a sample/demonstration
module's own object graph across a genuine restart rather than merely
declining to duplicate it.

## 13. Key Takeaways

A controlling instruction that says "determine the true root cause" is
asking for exactly that — not a third mitigation dressed up as a fix.
Reading real source, ruling candidate explanations out with direct
evidence rather than plausibility, and placing each fix at its actual
owning layer are what separate a genuine resolution from another
disclosed workaround.

## Related Documents

- `docs/governance/Quality/Technical Debt Register.md` — `TD-26`,
  `TD-37` (Resolved), `TD-38` (new, Open)
- `docs/releases/v0.10.0/WP10.1B Root Cause Analysis.md`
- `docs/releases/v0.10.0/WP10.1B Implementation Report.md`
- `docs/academy/03 Work Packages/WP10.1B-runtime-host-and-module-discovery-hardening.md`
