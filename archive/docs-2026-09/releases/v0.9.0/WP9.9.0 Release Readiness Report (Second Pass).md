# WP 9.9.0 — Release Preparation & Product Baseline — Release Readiness Report (Second Pass)

## Purpose and Relationship to the First Pass

This is a **second, independent release-readiness verification pass**
for `v0.9.0` ("Mechanical Foundation"), commissioned by the Product
Owner after `WP9.9.0 Release Readiness Report.md`'s own first pass and
`WP 9.8B` (Platform Service Register Reconciliation), which closed that
first pass's own top standing recommendation. The controlling
instruction for this pass is identical, word for word, to the first
pass's own — a deliberate "verify, remediate, re-verify" sequence, not
a correction of anything wrong with the first pass. **The first pass's
own documents are left exactly as written, per "never silently modify
historical records"** — this is a new, additional artifact, not a
replacement.

Verification only — no new functionality, no architectural changes, no
implementation changes beyond what a genuine release-blocking defect or
stale governance discovered during verification would require. Exactly
one implementation-adjacent action was taken under that exception: a
newly-observed test flake was formally registered in the Technical Debt
Register (see §3, below) — a governance-documentation action, not a
code change.

## 1. Repository Verification

**Working tree not clean — disclosed, unchanged in kind since the first
pass.** `git status` at the start of this pass showed 95 entries (14
modified tracked files, 81 untracked new files/directories); expanded,
154 files new or modified (140 new + 14 modified), all uncommitted — up
from the first pass's own 139, reflecting `WP 9.8B`'s own six new files
(five `WP9.8B`-prefixed release documents plus one Academy Retrospective)
and its own four governance-document edits. Diffed against the `v0.8.0`
merge commit (`28e41e8`) directly: **144 files changed, +14,546/−294
lines** (up from the first pass's own 143 files, +14,055/−268 lines).

`git branch -a` continues to show only `main` — no
`feature/v0.9.0-calculations-workspace` branch exists, unchanged from
the first pass's own disclosed finding, reconfirmed here rather than
assumed still true.

## 2. Build Verification

| Configuration | Projects | Warnings | Errors |
|---|---|---|---|
| Debug (clean rebuild, `bin`/`obj` fully removed) | 4/4 | 0 | 0 |
| Release (clean rebuild, `bin`/`obj` fully removed) | 4/4 | 0 | 0 |
| Release, per-project (`Tempest.App.csproj`, `Tempest.Samples.csproj`, `--no-incremental`) | 2/2 | 0 | 0 |

Identical result to the first pass — expected, since zero `src/`/`tests/`
files have changed since it ran (`WP 9.8B` was documentation-only).

## 3. Test Verification — A Genuine Finding This Pass

| Run | Configuration | Total | Passed | Failed | Skipped |
|---|---|---|---|---|---|
| 1 | Debug (clean rebuild) | 2026 | 2026 | 0 | 0 |
| 2 | Debug | 2026 | 2026 | 0 | 0 |
| 3 | Release (clean rebuild) | 2026 | **2025** | **1** | 0 |
| 4 | Release (re-run) | 2026 | 2026 | 0 | 0 |
| 5 | Release (`scripts/new-release.ps1` invocation) | 2026 | 2026 | 0 | 0 |

**Run 3 failed one test: `Tempest.Core.Tests.Logging
.CompositeLogSinkTests.Write_AllSinksThrow_ExceptionNeverPropagatesToTheCaller`.**
This is the first time in this project's own documented history that a
full-suite run has actually captured an instance of the
`Console.Out`/`Console.Error`-capture flake first informally disclosed
at `WP 6.3` and referenced by name in every subsequent release-closing
review's own dedicated flake-check — every one of which, until now,
reported zero instances observed across its own runs. **Root cause,
confirmed by direct source inspection**: `CompositeLogSink.Write`
(`src/Tempest.Core/Logging/CompositeLogSink.cs:84`) reports a child
sink's own failure via `Console.Error.WriteLine` directly; xUnit
parallelises test classes by default, and any concurrently-running
`[Collection("Console output capture")]`-tagged test (every Workspace
integration test file, `WP 9.0A` onward) temporarily redirects the
same, process-wide, static `Console.Out`/`Console.Error` streams via
`Console.SetOut`/`SetError` — a genuine, if narrow, cross-test race, not
a defect in `CompositeLogSink`'s own actual behaviour.

**Confirmed non-reproducible in isolation**: 5 further runs scoped to
`CompositeLogSinkTests` alone, 5/5 passed, 0 failures. **Confirmed
resolved on re-run**: the immediately-following full-suite Release run
(Run 4) passed 2026/2026 clean. Across all 5 full-suite runs this pass
performed, the observed failure rate is 1-in-5 — broadly consistent
with the "roughly one run in several" characterisation `WP8.9.0`'s own
retrospective already gave this flake by name, now empirically
confirmed rather than only asserted.

**Formally registered for the first time**: `TD-34`, added to
`docs/governance/Quality/Technical Debt Register.md` during this pass —
a disclosed, genuine gap in the register's own completeness (six years'
worth, in this project's own compressed timeline, of narrative
disclosure across many release documents, never once formalised as its
own tracked item) found only because this pass happened to catch a live
instance to cite as evidence. **Not Release Blocking** — the underlying
`CompositeLogSink.Write` behaviour is correct and already proven by the
same test's own repeated, isolated passes; only the test's own
incidental dependency on the shared, static `Console.Error` stream is
at risk under full-suite parallel execution, a pre-existing
characteristic of the test suite's own structure, not of any `v0.9.0`
Work Package's own code.

One further, dedicated scoped run against this release's own seven
Workspace namespaces plus `EngineeringCockpit`: **516/516 passing**,
unchanged from the first pass (zero new tests added by `WP 9.8B`).

**Zero regression against `v0.8.0`'s own 1631 tests, and zero
regression against the first pass's own 2026-test baseline** — the one
observed failure is a flake in a `WP 5.2`-era test, not a new defect in
any `v0.9.0` Work Package's own code.

## 4. Version Verification

Unchanged from the first pass. `VERSION` correctly still reads `0.8.0`
— confirmed directly, not assumed. No action taken, per this Work
Package's own explicit "Do NOT change VERSION" constraint.

## 5. Documentation Completeness

- `docs/releases/v0.9.0/ReleaseNotes.md` — updated in place for this
  pass (see Deliverables) to disclose `WP 9.8B`'s own closure of the
  Platform Service gap and this pass's own `TD-34` finding. Not
  recreated from scratch — the first pass's own population of this
  document remains the base this pass builds on.
- `docs/releases/v0.9.0/Retrospective.md` — likewise updated in place,
  not recreated.
- Five new `WP9.9.0`-prefixed deliverables produced under
  `docs/releases/v0.9.0/`, each suffixed "(Second Pass)" to keep the
  first pass's own five identically-named files intact and
  unambiguous — both sets coexist, per "never silently modify
  historical records."

## 6. Academy Completeness

126 articles across 7 categories at the start of this pass (after
`WP 9.8B`'s own `+1`); **127 after this pass's own Academy
Retrospective** (Second Pass), re-verified by direct `find` count.
`03 Work Packages` grows from 77 to 78.

## 7. Governance Registers — Re-Audited, Reflecting `WP 9.8B`'s Own Backfill

| Register | Claimed (post-`WP 9.8B`) | Verified | Consistent? |
|---|---|---|---|
| ADR Register | 91 ADRs | 91 files, `ADR-0001`–`ADR-0091`, zero gaps | Yes, unchanged since first pass |
| Technical Debt Register | 34 tracked (post-`TD-34`), 17 trade-offs | 34 `TD-` rows, 17 `AT-` rows | Yes — `+1` this pass, disclosed in §3 |
| Future Capability Register | 62 entries | 62 `FCR-` headings | Yes, unchanged |
| Academy Register | 126 (post-`WP 9.8B`) → 127 (this pass) | Matches, re-derived directly | Yes |
| **Platform Services Register** | **30 entries (post-`WP 9.8B`, up from 27/26)** | **30, verified directly — the four-Engineering-Foundation-framework gap this Work Package's own first pass named as `v0.9.0`'s own top standing recommendation is now closed** | **Yes — first time this register has been fully consistent in this project's history** |
| Module Register | 34 production modules | 34, via `ClockModuleDiscoveryTests` | Yes, unchanged |
| Interface Register | 168 public interfaces | 168, `grep`-verified | Yes, unchanged |
| Dependency Injection Register | 44 raw / 42 named | Unchanged, re-confirmed against all seven `Program.cs` registration calls | Yes |
| Rejected Designs Register | 45 entries | 45, direct count | Yes, unchanged |

### Findings Requiring Disclosure

1. **The four-Engineering-Foundation-framework Platform Service gap —
   RESOLVED.** Closed by `WP 9.8B`, confirmed here by direct
   re-verification, not merely trusted from that Work Package's own
   claim: `docs/governance/Engineering/Platform Services Register.md`
   and `docs/architecture/Platform Service Map.md` both independently
   re-checked and found to carry complete, accurate entries for
   Engineering Data Model, Materials, Engineering Calculations, and
   Verification. **This is the first release-closing review in this
   project's history to find this gap closed rather than open.**
2. **`TD-34` — newly registered this pass**, see §3.
3. **The "32 vs. 35 governance documents" count drift remains open** —
   unchanged since `WP 9.3A` first found it; `find docs/governance
   -iname "*.md" | wc -l` still returns 35 against the register's own
   stated "32," and remained outside `WP 9.8B`'s own narrower Platform
   Service scope, and outside this pass's own scope to chase further.

No historical record was modified. `WP 9.9.0`'s own first-pass
documents, `WP 9.8B`'s own documents, and every dated Work Package
retrospective remain exactly as written.

## 8. Architecture Review

Unchanged from the first pass, re-verified directly rather than
assumed: zero circular dependencies, zero layering violations, the one
disclosed cross-framework dependency (`Tempest.Core.Requirements` →
`Tempest.Core.EngineeringDomain`, `WP 9.1A`) reconfirmed and unchanged.
`WP 9.8B`'s own four new `Platform Service Map.md` sections introduce
zero code and therefore zero architectural change of any kind —
confirmed by `git diff` showing zero `src/` changes attributable to
that Work Package.

## 9. Workspace Integration

Unchanged from the first pass: all six real Engineering Disciplines
confirmed registered in `Program.cs`, in the identical, correct
dependency order, re-verified by direct source inspection.

## 10. Engineering Lifecycle Completeness

Unchanged from the first pass — re-confirmed directly.

## 11. Digital Thread Integrity

Unchanged from the first pass — re-confirmed directly; zero new
relationship kinds introduced by `WP 9.8B` (a documentation-only Work
Package has none to introduce).

## 12. Cockpit Integration

Unchanged from the first pass — re-confirmed directly.

## 13. Work Package Traceability

All seven `v0.9.0` implementation Work Packages (`WP 9.0A` through
`WP 9.5A`) confirmed represented, unchanged from the first pass. `WP
9.9.0` (first pass) and `WP 9.8B` are both now also complete, dated
Work Packages in their own right, each with their own full deliverable
set, confirmed present by direct `find`.

## 14. Module Inventory

34 production modules, unchanged since the first pass (`WP 9.8B` added
zero modules — a documentation-only Work Package). Re-verified via
`ClockModuleDiscoveryTests`, part of the 2026/2026 passing suite
(Runs 1, 2, 4, 5).

## 15. Technical Debt Review

34 tracked items (33 + `TD-34`, this pass's own new finding), 17
disclosed trade-offs. **Zero Release Blocking** — `TD-34` is a
confirmed, non-reproducible-in-isolation test-infrastructure flake with
no data-correctness consequence, the identical disposition every
"disclosed, not fixed, no functional gap" item in this register already
carries.

## 16. Future Capability Review

62 entries, unchanged since the first pass. `FCR-0005` (Governance
Register Health-Check Tooling) reconfirmed still Identified — `WP 9.8B`'s
own existence, and this pass's own manual, 20-step re-verification, are
each further, independent evidence for it.

## 17. Engineering Review

Every one of the seven Work Packages' own Engineering Review Report,
plus `WP 9.8B`'s own, independently reconfirmed **No Release Blocking
findings** — re-verified here by direct re-read, not assumed unchanged.

## 18. Security Review

Unchanged from the first pass's own conclusion: zero Release Blocking
findings across all seven implementation Work Packages plus `WP 9.8B`
(eight dedicated Security Reviews this release now, including `WP
9.8B`'s own, which found zero new attack surface — a documentation-only
Work Package introduces none).

## 19. Systems Engineering Review

Unchanged from the first pass's own conclusion, extended by `WP 9.8B`'s
own: the Kind-keyed Workspace extension model remains sound across six
real disciplines, and the governance model itself is now demonstrably
self-correcting — a disclosed, multi-review-old gap was closed by a
Work Package created specifically for that purpose, the first such
instance in this project's history.

## 20. Known Issues

One new item this pass: `TD-34` (test-infrastructure flake, disclosed
above, not Release Blocking). One item resolved since the first pass:
the four-Engineering-Foundation-framework Platform Service gap. One
item unchanged, still open: the "32 vs. 35 governance documents" count
drift.

## Overall Verdict

**No release-blocking defect found.** This pass found the release in a
materially *more* consistent state than the first pass left it — one
standing governance recommendation closed, zero new architectural or
functional defects, and one previously-only-narratively-disclosed test
characteristic now formally captured with real evidence. See
`docs/releases/v0.9.0/WP9.9.0 Product Approval Report (Second Pass).md`
for the formal recommendation.

## Related Documents

`docs/releases/v0.9.0/WP9.9.0 Release Readiness Report.md` (first
pass); `docs/releases/v0.9.0/WP9.8B Reconciliation Report.md`;
`docs/releases/v0.9.0/ReleaseNotes.md`; `docs/releases/v0.9.0/
Retrospective.md`; `docs/releases/v0.9.0/WP9.9.0 Engineering Statistics
Report (Second Pass).md`; `docs/releases/v0.9.0/WP9.9.0 Architecture
Baseline Summary (Second Pass).md`; `docs/releases/v0.9.0/WP9.9.0
Engineering Capability Summary (Second Pass).md`; `docs/releases/v0.9.0/
WP9.9.0 Product Approval Report (Second Pass).md`;
`docs/governance/Quality/Technical Debt Register.md` (`TD-34`).
