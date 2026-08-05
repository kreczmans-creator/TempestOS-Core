# WP 9.1B — Development Baseline Consolidation — Commit Summary

## Status

**One consolidation commit created**, not three per-Work-Package
commits. This document explains why a three-way split was assessed and
rejected, in favour of the single-commit fallback this Work Package's
own controlling instruction explicitly allows.

## The Commit

```
commit 71b49ea
Author: (session author)
Branch: feature/v0.9.0-mechanical-foundation

    WP 9.0A-9.1A: Mechanical Foundation consolidation (v0.9.0, development baseline)

    138 files changed, 12114 insertions(+), 263 deletions(-)
```

Full message body itemises each Work Package's own scope, test-count
progression (1631 → 1695 → 1738 → 1808), and the two-pre-commit-defect
disclosure each Work Package's own Implementation Report already
records — reproduced in full by `git show 71b49ea` / `git log -1
--format=%B 71b49ea`.

## Why a Single Commit, Not Three

The controlling instruction's own escape hatch applies directly: *"If
work from multiple packages is already interleaved and cannot
reasonably be separated, explain why and recommend a single
consolidation commit instead."*

**No intermediate commit exists to split at.** All three Work Packages
were implemented in one continuous session with zero commits between
them, per this project's own "commit only when asked" discipline (never
invoked until this Work Package). Git has no record of "the tree as it
stood right after `WP 9.0A`" or "right after `WP 9.0B`" — those
intermediate states exist only in this session's own conversation
history, not as buildable, testable commits.

**A large, load-bearing share of the changed surface is genuinely
multi-Work-Package at the single-file, often single-line, level:**

- **Every governance register touched** (`PROJECT_STATUS.md`, `ADR
  Register.md`, `Academy Register.md`, `Documentation Register.md`,
  `Dependency Injection Register.md`, `Exception Register.md`,
  `Interface Register.md`, `Module Register.md`, `Platform Services
  Register.md`, `Future Capability Register.md`, `Technical Debt
  Register.md`, `Test Register.md`) carries its own "Last Reviewed"
  field as **one single line/table-cell**, each Work Package's own
  contribution prepended ahead of the previous one's. Splitting these
  into three commits would require manually rewriting each cell three
  times to reconstruct an intermediate state that never existed as a
  real file on disk — original re-authorship, not a git operation.
- **`PROJECT_STATUS.md`** (238 insertions/86 deletions in one file) has
  its own "Current Work Package" section fully rewritten at each Work
  Package boundary (the previous Work Package's own full write-up
  demoted to a condensed "Summary (for reference)" section, the new one
  taking its place) — the same reconstruction problem, at greater
  scale.
- **Several source files were created by `WP 9.0A` and then directly
  extended by `WP 9.0B`**, as new (never-committed, so history-free)
  files: `MechanicalProductStructureNodeProvider.cs`,
  `MechanicalPropertyFacetProvider.cs`,
  `MechanicalObjectFactoryRegistry.cs`,
  `MechanicalWorkspaceRegistration.cs`,
  `CreateMechanicalObjectCommand.cs`, `CopyMechanicalObjectCommand.cs`,
  `MechanicalProductStructureSampleModule.cs`,
  `StructuralMutationTests.cs` — each a single file mixing both Work
  Packages' own additions, again with no commit boundary to split at.
- **`Program.cs`, `WorkspaceManager.cs`** are each touched by all three
  Work Packages in turn (registration wiring added incrementally as
  each discipline was wired in).
- **`EngineeringObjectBase.cs`, `Validation.cs`,
  `ReferenceIntegrityChecker.cs`, `EngineeringDomainException.cs`,
  `PhysicalConfiguration.cs`** are shared between `WP 9.0A` and
  `WP 9.0B` specifically (the `ReviseAsync` structural-state-copy fix
  `WP 9.0A` introduced, then the BOM-field extension of the same fix by
  `WP 9.0B`).

**What *was* cleanly separable, confirmed by direct inspection, and
deliberately not split out on its own regardless:** the Requirements
Domain layer (`src/Tempest.Core/Requirements/*.cs`) and the entire
`Tempest.App.Workspace.Requirements` namespace are exclusively
`WP 9.1A` — no `WP 9.0A`/`WP 9.0B` file touches either. Splitting only
this genuinely-separable portion into its own commit while leaving the
other two Work Packages combined would not produce three meaningful
per-Work-Package commits — it would produce one partial commit and one
still-combined one, which is more confusing than one clearly-itemised
consolidation commit, not less.

## Assessed Alternative: `git add -p` Hunk-Level Splitting

Considered directly. For files with real, distinct per-Work-Package
hunks (a minority — mostly `Program.cs`-shaped additive blocks), this
would have worked. For the governance registers and `PROJECT_STATUS.md`
— the majority of the non-source-code diff volume — it would not: a
single-line cell replacing itself three times has exactly one hunk, not
three, and `git add -p` cannot split within a line. Attempting a
mixed strategy (hunk-split where possible, manual reconstruction
where not) was assessed as materially higher-risk than one honest
consolidation commit, for no corresponding benefit — the three Work
Packages' own individual scopes remain fully and separately documented
in `WP9.0A`/`WP9.0B`/`WP9.1A Implementation Report.md` regardless of how
many commits carry the code.

## Verification

Post-commit: working tree clean except `docs/First_run/` (deliberately
excluded, see `WP9.1B Development Baseline Report.md`); `dotnet build
src/TempestOS.slnx -c Debug` — 0 warnings, 0 errors.

## Related Documents

`WP9.1B Development Baseline Report.md`; `WP9.1B Merge Readiness
Report.md`; `WP9.0A Implementation Report.md`; `WP9.0B Implementation
Report.md`; `WP9.1A Implementation Report.md`.
