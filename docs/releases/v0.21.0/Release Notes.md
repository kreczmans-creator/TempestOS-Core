# TempestOS v0.21.0 — Release Notes

**Status: in preparation on `release/v0.21.0`, cut from the `v0.20.0`
candidate (`88311649`) on 2026-09-15 at the Product Owner's instruction
to close every technical weakness named in the lead's assessment of that
afternoon.** Nothing in this document is certification. `v0.20.0` stays
under the Product Owner's manual test as the candidate, receiving the
`WP 20.10A`–`20.10G` fixes; every one of those is merged here too.

## Summary

**v0.21.0 is the recovery tranche** — the docking rewrite to `ADR-0153`,
Undo across commands, the editor split, documents from every template,
the Engineering Assets surfaces, typed calculation results with retained
inputs, the commercial edges, the viewer's remaining formats and markup,
the installer with upgrade, backup and restore, lazy rehydration, a
real-shell run in CI, and the mutation threshold met. See
`Execution Plan.md` for the packages and their waves.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 21.5B` `TD-88` — lazy, project-scoped materialisation over the index, closed | `EngineeringObjectRehydrationService.RehydrateAsync` no longer reconstructs the estate unconditionally: for every state with a known Kind and an existing document (the one document read that stays eager — a lightweight existence check, no revision content, needed for orphan detection), it registers the object lazily (`IEngineeringObjectRepository.RegisterLazy`), deferring revision-content reads and the rehydrator's own type-specific parsing to first access. `FindAsync` is the single-flight materialising loader — an id materialises once no matter how many callers ask concurrently, and joins the identity map permanently (no eviction shipped). `ListAllAsync`/`ListByKindAsync`/`ListChildrenAsync` answer `EngineeringObjectIndexEntry` rows from the index alone, computed live for a materialised object so a rename or delete is never stale. Every one of `WP 20.1C2`'s own named ~seventy/eighty callers across `Tempest.Core`/`Tempest.Workspace`/`Tempest.Desktop`/`Tempest.Samples` now either reads the index type directly (the compiler proves it needs nothing else) or materialises explicitly through the new `EngineeringObjectRepositoryExtensions.MaterialiseAsync<T>` seam before touching a type-specific field — no caller keeps an untyped "list then cast". Opening a project materialises its own subtree eagerly (`IEngineeringObjectRepository.MaterialiseSubtreeAsync`, wired at `ProjectContext.OpenAsync`/`LoadAsync`); closing one releases nothing. **Kill switch** (named, bounded): `WP 20.1A2`'s business-identifier index rebuild still materialises every enforced-Kind object eagerly inside `RehydrateAsync` — `BusinessIdentifier` is a computed projection absent from the index row, and `BusinessIdentifierScope.ResolveProjectId`'s own synchronous repository read (outside this Work Package's files, the business-identifier index's contract not to be touched) depends on an already-materialised ancestor chain — bounded to the enforced-Kind set, never the unenforced majority a large estate is mostly made of. See the `TD-88` row (`BACKLOG.md`, "Closed") for the full measured figures and their own disclosed caveat: `IndexBuilt`/project-open/first-read all improved (a 10,000-object estate: ~387 ms/~55 ms (300-object subtree)/~1.4 ms respectively); `RehydrateAsync` returning as a whole measured slower in raw total than `WP 20.1C2`'s own ~190 ms/1000 figure, dominated by relationship rebuilding this Work Package left unchanged and did not benchmark past 1,000 objects before. Three new tests (single-flight, identity-map preservation, a rehydrated revision chain read from its newest end) plus a benchmark; `tests/Tempest.Core.Tests` (4,673) and `tests/Tempest.Desktop.Tests` (660) green in Debug; both configurations build clean, warnings as errors; governance 5/5. | *(pending merge)* |
| `WP 21.3A` Typed calculation results with retained inputs — `TD-22`, `TD-29`, `TD-30` | `TD-22`: `CalculationIntermediateResult` carries its own declared `ValueTypeName`; a typed read-back (`As<TValue>()`) returns the declared type or throws `CalculationReadbackException` (naming the key and both types) — never an `InvalidCastException` — whether the value is still the in-process CLR object or a `JsonElement` read back after persistence. `CalculationContext` takes a configured count/total-size bound (defaults 200 intermediates / 1 MiB, disclosed as guesses); exceeding either throws `CalculationBoundExceededException` naming the definition, refusing the record rather than recording without limit. `TD-29`: `CalculationRecord<TResult>` retains the input it ran with (`Input`/`InputTypeName`, the same informal nullable-trailing-field precedent `ResultTypeName` already set — no export-schema migration needed, confirmed against `WP 20.3A`'s own, separate export/import scope). `ICalculationEngine` gains `ReRunAsync` (identical retained input, or a supplied changed input) and `CompareAsync`, producing a typed `CalculationComparison` — which input and result fields changed, old and new, with units; a record with no retained input is reported in the diff rather than thrown for. Surfaced as two new commands, `calculations.rerun` (`Mutates = true`) and `calculations.compare-with-previous` (read-only), invocable through the Command Palette and the Ribbon's own data-driven Calculations tab — no new view. `TD-30`: `ICalculationResult`/`IVerificationResult`/`IApprovalGate` stay declared (still referenced structurally by `EvidenceComposer`/`IEvidence`/`ISimulation`) but are retired — each interface's own remark now says plainly why no implementation exists or is planned: the first two would require turning an immutable evidentiary snapshot into a live, addressable `IEngineeringObject` with its own revision/relationship service dependencies (`FCR-0051`, "a real Domain design question, not a mechanical add"); the third is a workflow-gate concept the product's own "no workflow engine" guard and `ADR-0087`/`ADR-0090` already rule out. The six built-in calculation definitions and their unit-invariance property proofs are unchanged. |
| `WP 21.5E` | Security review of every surface added since the `v0.5.0` baseline, widened mid-review (Product Owner) to the whole live `src/` tree: **1 RED, 1 AMBER, 11 GREEN** findings (`docs/security/Security Posture.md`; RED filed as `TD-184`, owned by `WP 21.4A`'s own files and applied at its merge; AMBER filed as `TD-185`). `docs/security/Security Posture.md` (`WP RC.0C`, brought forward) replaces `Threat Model.md`/`Security Roadmap.md` for `v1.0` (both kept, pointer added). Dependency vulnerability scanning is now a required `ci.yml` check (`dependency-scan`, parsed by `scripts/check-vulnerable-packages.ps1`); `.github/dependabot.yml` added; `THIRD-PARTY-NOTICES.md` added (20 direct packages, all MIT or Apache-2.0). Proved, with a test, that the frozen REST API/plugin-loading/licensing layers (`src/Frozen/`) are unreachable in a default build and configuration. | *(pending)* |
| `WP 21.5A` (`WP RC.0A`, brought forward) | Velopack-packaged Windows installer (`TempestOS-<tag>-Setup.exe`) with in-place update, off by default until enabled in Settings → Updates; an installed run's default persistence root (`%LOCALAPPDATA%\TempestOS\persistence-data`), a first-run location dialog, and `--persistence-root`/`Persistence:RootPath` overrides, closing `TD-36` for the installed case; a pre-migration backup (`BackupService`, the online SQLite backup API) fired automatically when a launch finds an older schema version, and Settings → Data's own "Back up now…"/"Restore from backup…"; the support matrix in `PHYSICAL_REVIEW.md` §2a. Adds **Velopack 1.2.0 (MIT)** — see `THIRD-PARTY-NOTICES.md` — referenced only by `Tempest.Desktop`. | *(pending)* |

## Figures

*(re-derived at `WP 21.9.0`)*

## Warnings

- **The `TD-22` intermediate-result bound's defaults are guesses**
  (`WP 21.3A`): 200 intermediates / 1 MiB total per execution, sized off
  the six built-in definitions (at most five intermediates each) with two
  orders of magnitude of headroom, not measured against any real
  step-heavy calculation the product does not yet have. `CalculationContext`'s
  own constructor lets a caller override either; the lead should confirm
  the defaults before a calculation with a genuinely large intermediate
  set ships against them.
- **Rehydration's own kill switch, `WP 21.5B`**: `WP 20.1A2`'s
  business-identifier index rebuild still materialises every enforced-Kind
  object (Part, Calculation/CalculationSet, Document/Drawing/CadModel, the
  three Manufacturing Kinds, VerificationActivity, Evidence) eagerly inside
  `RehydrateAsync`, because `BusinessIdentifier` is a computed projection
  not on the index row and `BusinessIdentifierScope.ResolveProjectId`'s own
  synchronous repository read (outside this Work Package's files) needs an
  already-materialised ancestor chain. Bounded to that Kind set, not the
  unenforced majority a large estate is mostly made of. Separately, the
  benchmark's own `RehydrateAsync`-returning figure (~514 ms/1000 objects)
  reads slower in raw total than `WP 20.1C2`'s own ~190 ms/1000 — disclosed
  rather than hidden: it is dominated by relationship rebuilding, which
  this Work Package left fully eager and unchanged (id-keyed, not
  materialisation), and which was never benchmarked past a 1,000-object
  estate before now.

## Related

- `docs/releases/v0.21.0/Execution Plan.md`
- `docs/releases/v0.20.0/Release Notes.md`
- `docs/adr/ADR-0153-tear-out-and-dock-everywhere-across-monitors.md`
