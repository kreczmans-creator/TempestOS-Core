# Engineering Readiness Review Architecture

## Status

Designed — `WP 12.9.0` (Release Preparation & Engineering Sign-Off,
Architecture phase), 2026-08-12. Architecture only; no code, no test,
no release engineering performed. Required ADR: `ADR-0106`.

**Corrected, `WP 12.9.0` Architecture Review Follow-Up, same date,
documentation only.** This Work Package's own read-only architecture
review found the original §4/§6 left the relationship between a
finding's own kind, the category status it produces, and the release
verdict it produces implicit — provably contradictory in one place:
`CERTIFIED WITH ACCEPTED TECHNICAL DEBT`'s own stated condition
("every category Pass" plus "Disclosed-Non-Blocking findings exist")
could never both hold simultaneously, given the category-status rule
in force at the time, making that verdict an unreachable, empty set as
originally written. §4 now states, once, the complete and exclusive
derivation from finding kind → category status → release verdict; §2
and §6 both now cite it rather than restate it. The taxonomy's own
three kinds, the five categories, the six-discipline process, and the
Repository Investigation below are all unchanged — this correction is
confined to the decision logic connecting them. See this document's
own §4 for the corrected model in full, and the `WP 12.9.0`
Architecture Review Follow-Up Academy retrospective for the complete
before/after account.

## Objective

Design the permanent TempestOS Engineering Readiness Review (ERR) — the
one, repeatable sign-off process every future release passes through
before Product Approval may authorise a tag — replacing the ad hoc,
per-release checklist this project has used twice (`WP 10.9A`, `WP
11.9.0`) with a structural framework this document names once and
every future release cites, rather than reinvents.

This is explicitly not release engineering. No verdict is rendered
here for `v0.12.0` itself; that is a separate, later action (this
architecture's own "Execution" boundary, §7). This Work Package
produces the model and the repository evidence a future execution
phase will need, not the release decision itself.

---

## Repository Investigation

### 1. What sign-off process exists today

No standing architecture document defines release sign-off as a
process; it exists only as prose. `Engineering Governance.md` §7
(Release Approval Process) states the *principle* — Build/Test Gate
must pass on `main` immediately pre-tag; an annotated tag is created
once; pushing requires per-occasion approval — and `05-release-engineering.md`
plus `docs/releases/v0.11.0/WP11.1B Engineering Workflow.md` specify
the *mechanics* (branching, PRs, `scripts/new-release.ps1`,
`release.yml`). Neither defines *what evidence a reviewer must gather,
in what categories, against what pass/fail criteria* before recommending
a tag. That gap is what every release since `v0.6.0` has closed
informally, and inconsistently, each time:

- `v0.6.0` (`WP 6.8`): "Platform Certification Report," verdict
  **CERTIFIED WITH ACCEPTED TECHNICAL DEBT.**
- `v0.7.0`–`v0.9.0`: "Release Readiness Report" / "Product Approval
  Report" pairs, no fixed verdict vocabulary.
- `v0.10.0` (`WP 10.9A`): the first **six-discipline Programme Review**
  — Chief Architect, Principal Software Engineer, Workflow Engineer,
  QA Lead, Technical Author, Product Manager, each an independent
  review, reconciled into one report and a **12-item Definition of
  Done** table. Verdict vocabulary still ad hoc.
- `v0.11.0` (`WP 11.9.0`): the identical six-discipline structure,
  explicitly citing `WP 10.9A` as "the standard applied" — the first
  time this project *reused* a prior release's own review shape rather
  than inventing a new one. Verdict: **ACCEPT WITH OBSERVATIONS.**
  Found two genuine, previously-undisclosed release-tooling defects
  (`TD-42`, `TD-43`) precisely because QA Lead's own independent
  re-verification did not defer to any other discipline's report — the
  concrete, evidenced case for keeping multi-discipline review rather
  than collapsing it to a single reviewer or a single automated check.

Two consecutive releases converging on the same six-discipline shape,
unprompted by any written requirement to do so, is itself the clearest
evidence this shape is sound and worth naming permanently — exactly
this document's own commissioning brief ("this is not a checklist
copied from previous releases... it should become the permanent model
going forward").

### 2. What is machine-verified today, and what is not

`ADR Register` matches `docs/adr/` exactly (104 files, 104 rows —
confirmed directly, `scripts/governance-healthcheck.ps1` Check 1,
`[PASS]`). Build Gate and Test Gate are both CI-machine-verified since
`WP 11.1A` (`.github/workflows/ci.yml`, Debug and Release, warnings
promoted to errors). `governance-healthcheck.ps1` (`WP 11.2A`) runs
eight checks — ADR Register, Academy Index, Release Register,
Documentation Register, `PROJECT_STATUS.md`, `VERSION`, release-folder
mandatory docs, Governance Index — as an advisory CI job, not yet a
required gate (`TD-45`'s own sibling finding). Branch protection on
`main` remains documented, not configured in GitHub (`TD-45`, Open) —
nothing today mechanically prevents a direct push to `main` or a merge
with a failing check; the entire PR/CI/CODEOWNERS apparatus is
voluntary, enforced by discipline, not tooling. `scripts/new-release.ps1`'s
own `git tag`/`git push` steps still carry no exit-code verification
(`TD-42`, Open) — a failure there would currently print `RELEASE
SUCCESSFUL` regardless. Neither gap is new; both were disclosed at
`v0.11.0`'s own sign-off and remain open, unresolved by any `v0.12.0`
Work Package, none of which had them in scope.

### 3. Direct repository assessment, `v0.12.0`, as of this Work Package

Every figure below was independently re-derived from source during
this Work Package (`2026-08-12`), on a branch cut from `main` at its
current tip — nothing is carried forward from any prior summary.

**Remaining Work Packages.** `docs/releases/v0.12.0/WorkPackages.md`'s
own table: `WP 12.0A`/`WP 12.0B`, `WP 12.1A`/`WP 12.1B`, `WP 12.3A`/
`WP 12.3B`, `WP 12.4A`/`WP 12.4B` all **Complete**, and — confirmed by
direct `git log main`, not assumed — all four pairs are merged into
`main` (`fa02db3`, `86b99f3`, `a2988dd`, `386a821`). `WP 12.2A`
(Presentation Strategy Execution) found its entire scope already
delivered by `v0.11.0`'s own `WP 11.3A`/`WP 11.3B`; its disposition
documentation is written and complete but **not yet committed or
merged** — confirmed directly: `git log feature/v0.12.0-presentation-strategy-execution
--oneline` resolves to the identical commit as `main`'s own tip, zero
commits of its own; its documentation changes exist only as an
uncommitted, stashed working-tree change on that branch, awaiting the
review this Work Package's own predecessor stopped for. `main` today
therefore does not yet reflect `WP 12.2A`'s own disposition — this is
a real, open, pre-tag item, not resolved by this Work Package (out of
its own "no implementation" scope) but named here as required evidence
for a future execution pass. `WP 12.9.0` itself (this Work Package) is
**in progress**, architecture phase only. `WP 12.2A`'s Type field
(Implementation, per the roadmap's own original wording) and its own
"Delivered by `WP 11.3A`/`WP 11.3B`" disposition remain exactly as
that Work Package's own retrospective states — not reinterpreted here.

**Future Capability Register.** 84 entries total — independently
re-derived (`grep -oE "FCR-[0-9]{4}"`, unique count), matching the
register's own last-claimed total exactly. No entry is specific to
`v0.12.0` release-blocking risk; every entry is, by this register's own
definition, deferred future work, not committed scope. `FCR-0084`
(`WP 12.4B` review follow-up) is the only entry this release itself
added — a typed-callback-interface opportunity explicitly deferred as
engineering judgement, not urgency.

**Technical Debt Register.** 48 entries total (`grep -c "^| TD-"`),
approximately 35 Open, 12 Resolved, 3 Partially Resolved (exact
per-row classification is this register's own source of truth, not
duplicated here as a second count that could drift from it). Zero
entries are marked release-blocking by this release's own work — the
register carries no formal "Release Blocking" status value at all
today (§4, below, names this as a genuine design gap this architecture
closes). The three most release-relevant Open items, all pre-existing
and already disclosed at `v0.11.0`'s own sign-off, remain open and
unaffected by any `v0.12.0` Work Package: `TD-42` (`new-release.ps1`
tag/push failures not exit-code-checked), `TD-43` (`governance-healthcheck.ps1`
generic exception handler loses the failing check's identity), `TD-45`
(branch protection documented, not configured).

**Governance register health.** `scripts/governance-healthcheck.ps1`
run directly this Work Package: **3 passed, 1 warned, 4 failed of 8
checks.** ADR Register (`[PASS]`), Release Register (`[PASS]`),
`VERSION` (`[PASS]`, still `0.11.0` — correct, unbumped, since
`v0.12.0` has not yet tagged). Release-folder mandatory docs
(`[WARN]`, informational only — two historical releases, `v0.9.0`/
`v0.10.0`, have no `WorkPackages.md`, a disclosed, pre-existing,
already-explained gap). Academy Index (`[FAIL]`) — 163 files, 163
links (the totals coincidentally match; the actual link *set* does
not), dozens of real articles and retrospectives, `WP 10.0A` onward,
never linked from `Academy Index.md` — a large, pre-existing gap first
disclosed at `WP 11.2A`, reconfirmed at `WP 11.9.0`, still entirely
unaddressed two releases later. Documentation Register and
`PROJECT_STATUS.md` path checks (`[FAIL]`, both) — the great majority
of flagged paths are the health-check tool's own known limitation
(checking a relative path fragment, such as `01 Engineering Principles/`,
without the `docs/academy/` prefix it is actually written under, in
running prose) rather than a genuine broken reference; a minority
(`docs/roadmap/`, `docs/diagrams/`) are long-standing, already-disclosed
aspirational-document gaps predating this release. Governance Index
(`[FAIL]`) — **one genuine, `v0.12.0`-caused gap, not previously
disclosed anywhere**: `docs/governance/Engineering/Engineering
Vocabulary Register.md` (created `WP 12.1B`) is not linked from
`Governance Index.md`. Named here as a real, open, `v0.12.0`-caused
governance gap; not fixed within this Work Package (correcting a
governance index entry is not itself producing the sign-off
*architecture*, and this Work Package's own brief is explicit: no
implementation).

**Documentation, architecture, and ADR coverage.** 104 ADRs (`ls
docs/adr/*.md`), matching the ADR Register exactly. 24 architecture
documents (`ls docs/architecture/*.md`) — every `v0.12.0` architectural
decision has a standing document: `Desktop Composition Architecture.md`
(`ADR-0103`), `Fault Injection & Validation Architecture.md`
(`ADR-0102`), `Desktop Command & Event Wiring Architecture.md`
(`ADR-0104`), `Classification & Relationship Vocabulary Safety Net
Architecture.md` (`ADR-0105`), and — once merged — `Shell &
Composition Framework Architecture.md`'s own `WP 11.3B` update remains
`v0.11.0`'s, not this release's, addition. 99 files under `docs/academy/03
Work Packages/` on `main` today (100 once `WP 12.2A` merges); 164
Academy files total on `main` today (165 once merged).

**Implementation status.** Four ADRs realised this release, all
confirmed implemented, not merely designed: `ADR-0102` (fault-injection
module isolation), `ADR-0103` (composition roots own collaborators),
`ADR-0104` (Desktop cross-collaborator communication), `ADR-0105`
(vocabulary declared once, non-enforcing register). Zero public API
broken across all four — independently confirmed at each Work
Package's own review, not reconfirmed a second time here since no code
changed since those reviews. Zero new Engineering Discipline; this
release is composition/hardening/governance-only, the same
"architecture-and-tooling, not feature" character `v0.11.0` had.

**Testing and build status.** Independently re-built and re-run this
Work Package, from a clean checkout of `main`'s own current tip — not
carried forward from any Work Package's own report: `dotnet build
src/TempestOS.slnx` — Debug **0 Warnings, 0 Errors**; Release **0
Warnings, 0 Errors**. `dotnet test src/TempestOS.slnx` — Debug
**2,255/2,255** passing (2,034 `Tempest.Core.Tests` + 221
`Tempest.Desktop.Tests`); Release **2,255/2,255** passing, identical
split. Zero failures, zero skips, both configurations.

**CI/CD readiness.** `.github/workflows/ci.yml` and `release.yml` both
present and unmodified since `v0.11.0`'s own hardening (`WP 11.1A`,
`WP 11.4A`). `TD-44` (CI never run on real infrastructure) — Resolved,
`WP 11.4A`, confirmed on real GitHub-hosted runs. `TD-45` (branch
protection not configured) — Open, unchanged. Release artefact
packaging (`ADR-0101`, `WP 11.3B`) — two separate, correctly-labelled
assets, `Tempest.Desktop` and `Tempest.App`, independently re-confirmed
directly against both workflow files during `WP 12.2A`.

**Known architectural exceptions, deferred decisions, and accepted
risks** — every one already disclosed at the Work Package that found
it, none newly discovered here: the `references` `RelationshipKind`
dual-ownership exception (`ADR-0105`, `references` legitimately owned
by both `RequirementRelationshipKinds` and `VerificationService`,
mirroring `ADR-0073`'s own accepted vocabulary-drift risk);
`WorkspaceShell`'s Stage 5 (further test/feature trimming), deferred by
design, `v0.11.0`'s own formal Product Debt disclosure, trigger
condition ("a real, demonstrated cost problem") still unmet; `FCR-0084`
(typed callback interface for `WorkspaceViewCoordinator`), deliberately
deferred engineering judgement, not urgency; `TD-01` (two logging
mechanisms) and `TD-04` (`IHostedService` naming proximity), both open
since the Foundation phase, revisit triggers named, neither met; the
entire `v0.13.0` release itself is a deferred, conditional decision —
it exists in the roadmap only if `WP 11.2A`/Product Approval ever
commits `v1.0` scope to third-party plugins or non-local REST
deployment, a decision not yet made.

---

## Architecture

### 1. Scope and boundary

The Engineering Readiness Review governs the question "is this
release's engineering work ready for Product Approval to consider
tagging" — nothing more. It does not decide *whether* to tag (Product
Approval's own authority, Engineering Governance §9, never delegated),
does not perform the tag or push (release engineering, mechanically
specified in `05-release-engineering.md`, unchanged by this document),
and does not replace the Build Gate/Test Gate/Technical Review Gate
(Engineering Governance §2) — it is the fourth, release-scoped gate
that consumes the outputs of the other three, plus governance and
product evidence none of the first three gates ever examined.

### 2. The five readiness categories

Every ERR evaluates exactly five categories, each independently
reviewed, each with its own required evidence, pass criteria, and
blocking conditions. Each category's own status is derived solely and
mechanically from the findings raised against it, per the three-kind
taxonomy and the exact derivation rule §4 defines — no category status
is ever assigned by narrative judgement independent of that rule. A
single **Not Ready** category is sufficient to prevent Product Approval
from proceeding, regardless of how strong the other four are (mirroring
Engineering Governance §2's own "a new work package breaking an
existing test is a Build Gate and Test Gate failure, full stop"
principle, applied one level up).

#### 2.1 Architecture readiness

*Reviewed by: Chief Architect.*

| Required evidence | Pass criteria | Blocking if |
|---|---|---|
| ADR Register vs. `docs/adr/`, direct file-count comparison | Exact match | Any mismatch not disclosed and reconciled within this review |
| Every ADR produced this release honoured by shipped code (no factual correction exposed by implementation) | Confirmed per-ADR, citing the implementing Work Package's own verification | An ADR is contradicted by shipped code, silently |
| Architecture Document Register vs. `docs/architecture/`, direct file-count comparison | Exact match, every new/updated document accounted for | Any architecture document describes a state the code no longer has |
| Ownership — every new type/module/register has exactly one clear owner (Engineering Governance non-negotiable 2, `FOUNDATION.md` §2) | Confirmed directly against each new component's own architecture document | A component with no clear single owner ships |
| Layering — dependencies flow downward only (`ADR-0023`, `FOUNDATION.md` §9) | Confirmed via direct dependency-graph check on every new/moved project or namespace | An upward or lateral dependency is introduced without a documented, ADR-justified exception |
| Dependency health — no new circular reference, no new upward reference | `dotnet build` succeeds (a circular project reference cannot compile); direct namespace-reference grep for anything not enforced at build time | A dependency direction violation exists that compiles cleanly (e.g., a namespace-level, non-project-level violation) |

#### 2.2 Implementation readiness

*Reviewed by: Principal Software Engineer.*

| Required evidence | Pass criteria | Blocking if |
|---|---|---|
| Every Work Package's own completion report vs. `git diff <prior-tag>..HEAD` | Every file the reports claim changed is actually changed; nothing claimed is missing, nothing unclaimed is present without explanation | A material, undisclosed diff exists between what was reported and what shipped |
| No partial work | Every Work Package on the release's own `WorkPackages.md` is **Complete**, **Delivered by** (a disclosed disposition, `WP 12.2A`'s own precedent), or explicitly **Not started — out of this release's scope** (never silently absent) | Any Work Package is in an undocumented or ambiguous state |
| Public API review | Every public type/member touched this release reviewed for signature changes; each either unchanged or accompanied by an ADR | An undocumented, unreviewed public API break ships |
| Behavioural preservation | Every refactor-only Work Package's own characterization tests re-run and confirmed identical pre-/post-change (the `WP 12.1B`/`WP 12.4B` precedent) | A refactor changes observable behaviour without a disclosed, justified reason |
| Implementation matches ADRs | Direct source read against each release ADR's own Decision section, not merely trusted from the implementing Work Package's own claim | A genuine, material deviation from an ADR's own Decision exists, undisclosed |

#### 2.3 Verification readiness

*Reviewed by: QA Lead (build/test evidence) and Workflow Engineer (CI/CD mechanics), jointly — the identical division `WP 10.9A`/`WP 11.9.0` already used, formalised here.*

| Required evidence | Pass criteria | Blocking if |
|---|---|---|
| Debug build | `dotnet build src/TempestOS.slnx -c Debug`, freshly run this review, not carried forward | Non-zero warnings or errors |
| Release build | `dotnet build src/TempestOS.slnx -c Release`, freshly run | Non-zero warnings or errors |
| Regression suite | `dotnet test src/TempestOS.slnx`, both configurations, freshly run | Any failure, in either configuration |
| Characterization coverage | Every refactor-only Work Package named a real, confirmed-by-direct-search coverage gap it closed before refactoring (the standing `WP 12.0B`/`WP 12.1B`/`WP 12.4B` discipline) | A refactor-only Work Package changed behaviour-adjacent code with no characterization test protecting it |
| CI requirements | `Build & Test` and `CI Gate` both genuinely pass on real, GitHub-hosted infrastructure for `main` itself, immediately pre-tag (Engineering Governance §7.3, restated, not a new rule) | Either job fails, or has not actually been run on `main` at the pre-tag commit |

#### 2.4 Governance readiness

*Reviewed by: Technical Author.*

| Required evidence | Pass criteria | Blocking if |
|---|---|---|
| Register consistency | `scripts/governance-healthcheck.ps1`, run directly this review | Any newly-introduced (this release's own) register drift, undisclosed |
| Academy completeness | Every Work Package has a retrospective under `03 Work Packages/`, per Engineering Governance §6 | A `v0.12.0` Work Package shipped with no retrospective |
| Documentation completeness | Every architecture document, ADR, and register touched this release is internally consistent with what shipped | A document describes a decision or state that contradicts another document or the code |
| Release documentation | `docs/releases/v0.12.0/WorkPackages.md`, `Release Notes.md` (produced at release time, not architecture time) | Release Notes cannot be produced accurately from the existing record |
| Project status | `PROJECT_STATUS.md` accurately reflects the release's own current state | `PROJECT_STATUS.md` is stale relative to the actual repository state |
| Roadmap completion | Every roadmap-predicted Work Package for this release is accounted for — Complete, or a disclosed disposition (`WP 12.2A`'s own precedent) | A roadmap-predicted item is silently missing, with no disposition recorded anywhere |

Pre-existing governance debt that predates this release (the Academy
Index gap, the Documentation Register path-check false positives) is
**disclosed, not blocking** — it does not become this release's own
blocking finding merely by still existing, exactly as `WP 11.9.0`
already established for the identical Academy Index gap. A **new**
gap this release itself caused (the Engineering Vocabulary Register's
missing Governance Index link) is disclosed as this release's own
finding and is a candidate for correction before tag, at Product
Approval's own discretion informed by this review — not automatically
blocking, since it is a discoverability gap, not a factual error.

#### 2.5 Release readiness

*Reviewed by: Product Manager; reconciled and decided by Product Approval.*

**Required evidence**: the reconciled output of the other four
categories; the release-level Definition of Done (§3, below); every
Technical Debt and Future Capability item touched, added, or newly
relevant this release, each individually classified per §4's blocking
taxonomy; `VERSION` and release-notes readiness (both release-time
actions, not evaluated as "ready" before the release branch is about
to close).

**Mandatory verification** (all four, every release, no exception):
Build Gate and Test Gate passing on `main` itself immediately pre-tag
(Engineering Governance §7.3); zero categories **Not Ready** (a
category may be **Pass** or **Pass, with observations** — either is a
sufficient, acceptable outcome; only **Not Ready** prevents proceeding,
per §4's own derivation table); Product Approval's own explicit,
per-occasion authorisation (Engineering Governance §9 — never assumed,
never carried forward from a prior release).

**Release-blocking conditions** — every one of these is, by
construction, an instance of a Release Blocking finding as §4 defines
it, surfacing through whichever category's own required evidence first
detects it; listed here once more for readability, not as a second,
independent rule: a Build Gate or Test Gate failure on `main` at the
pre-tag commit (surfaces via Verification readiness, §2.3); a public
API break with no ADR (Implementation readiness, §2.2); a
roadmap-predicted Work Package with no disposition of any kind
(Implementation readiness's own "No partial work" evidence, §2.2); a
Technical Debt or Future Capability item this release's own review
finds real, demonstrated evidence of harm for (Release readiness,
§2.5). Any one, anywhere, makes its own category **Not Ready** and —
per §4's row 1 — the release verdict **NOT READY**, regardless of the
other four categories' own strength.

**Release approval criteria**: Product Approval reviews the reconciled
Programme Review (§5) and issues one of exactly four verdicts (§6) —
never free-text ad hoc wording, closing the inconsistency `v0.6.0`
through `v0.11.0` each independently worded differently.

**Release artefacts** (unchanged from `05-release-engineering.md`,
restated for completeness, not redefined): `VERSION`; `docs/releases/vX.Y.Z/Release
Notes.md`; the annotated tag; two GitHub Release assets
(`Tempest.Desktop`, `Tempest.App`, `ADR-0101`); the Engineering Release
Report this document's own §5/§6 produce.

### 3. The release-level Definition of Done

Distinct from Engineering Governance §3 (a single Work Package's own
Definition of Done) — this is the release's own, evaluated once, at
sign-off, over every Work Package the release contains. Twelve items,
regrouped under the five categories above (the same twelve `WP
10.9A`/`WP 11.9.0` already used, now traceable to *why* each one
exists rather than standing as a flat, ungrouped list):

| # | Item | Category |
|---|---|---|
| 1 | Implementation complete | Implementation |
| 2 | Tests passing | Verification |
| 3 | Documentation updated | Governance |
| 4 | Academy updated | Governance |
| 5 | ADRs updated if required | Architecture |
| 6 | Architecture remains compliant | Architecture |
| 7 | Workflow / CI/CD operative | Verification |
| 8 | User experience improved (or correctly N/A) | Release |
| 9 | Engineering productivity improved | Release |
| 10 | Technical debt identified and classified (§4) | Governance |
| 11 | Product debt identified | Release |
| 12 | Commercial value demonstrated | Release |

An item genuinely not applicable to a given release (item 8, for a
governance-only release — `v0.11.0`'s own precedent) is stated as
**N/A, correctly out of scope**, directly, never silently marked
otherwise.

### 4. Blocking taxonomy, and its exact effect on category status and verdict

**Corrected, `WP 12.9.0` Architecture Review Follow-Up.** The version of
this section this Work Package's own architecture review examined left
the relationship between a finding's own kind, the category status it
produces, and the release verdict it produces implicit — provably
contradictory in one place (§6, below, carried the actual defect: two
verdicts whose own stated conditions could never both hold). This
section is now the single, authoritative source of that relationship;
§2's category-status paragraph and §6's verdict table both now cite
it rather than restate it, so the derivation exists in exactly one
place. No prior sign-off ever defined, in writing, what makes a finding
*blocking*, or how a finding's kind mechanically determines a verdict —
this remains this architecture's own genuine contribution; only the
precision of the relationship is new to this follow-up, not the
taxonomy's own three kinds, which are unchanged from the original
design.

**Every finding is classified as exactly one of three kinds** — never
zero, never more than one:

1. **Release Blocking.** A failing Build/Test Gate on `main`; an
   undocumented public API break; a Technical Debt or Future
   Capability item where this release's own review finds *real,
   demonstrated* evidence of harm (not merely existence) — the same
   evidentiary bar `WP11.3A`'s own Stage 5 deferral already established
   for a different question, generalised here. **Defined solely by a
   finding's own nature — never by a category's or a release's own
   status, which are consequences of this classification, not inputs
   to it** (the circularity the original design risked, now removed).
2. **Disclosed, Non-Blocking.** Real, tracked, does not prevent this
   release, but is newly raised, newly relevant, or newly re-assessed
   by *this* review — the overwhelming majority of this project's own
   Technical Debt Register today. Must be named in the Engineering
   Release Report; silently omitting a known item is itself a
   Governance readiness failure.
3. **Pre-Existing, Unaffected.** A gap that predates this release, that
   this release neither caused nor worsened, and that this review is
   not newly re-raising (the Academy Index gap, for `v0.12.0`).
   Disclosed for completeness — the same treatment `WP 11.9.0` already
   gave this identical Academy Index gap.

**Effect on category status** (exactly one rule applies per category,
in this fixed priority order — never narrative judgement):

| If a category carries... | Its status is |
|---|---|
| ≥ 1 Release Blocking finding | **Not Ready** |
| Zero Release Blocking findings, ≥ 1 Disclosed, Non-Blocking finding | **Pass, with observations** |
| Zero Release Blocking findings, zero Disclosed, Non-Blocking findings | **Pass** (any Pre-Existing, Unaffected findings are recorded against the category for completeness but never change this status) |

**Effect on the overall release verdict** (computed once, over every
finding raised across all five categories, in this same fixed priority
order — the sole, exhaustive derivation §6 now cites rather than
restates):

| Check, in order | If true, the verdict is | Otherwise, check the next row |
|---|---|---|
| 1. Any category is **Not Ready**? | **NOT READY** | ↓ |
| 2. Any Disclosed, Non-Blocking finding exists anywhere? | **ACCEPT WITH OBSERVATIONS** | ↓ |
| 3. Any Pre-Existing, Unaffected finding exists anywhere? | **CERTIFIED WITH ACCEPTED TECHNICAL DEBT** | ↓ |
| 4. (no findings of any kind, anywhere) | **CERTIFIED** | — |

This is a strict priority order, not an independent set of conditions
— exactly one row ever fires, because each row's own condition is
checked only after every prior row's own condition has already failed.
This is what makes all four verdicts simultaneously reachable (each
row's condition is independently satisfiable), mutually exclusive
(priority order forbids more than one from firing), and objectively
derivable (the only judgement required anywhere is classifying each
finding into one of the three kinds above, per §4's own fixed
definitions — never a second, separate judgement about the category or
the release as a whole).

A Technical Debt Register item's own status field (Open/Resolved/
Partially Resolved) remains orthogonal to this taxonomy — an Open item
can be Disclosed-Non-Blocking (the common case), Pre-Existing-Unaffected
(if not newly re-raised this review), or Release-Blocking (rare,
requires a specific, evidenced finding this release's own review
makes); a Resolved item is never any of the three, and contributes no
finding at all. This architecture does not propose adding a fourth
register-schema column to encode this taxonomy mechanically — that
would be a genuine, separate implementation decision (extending a
governance register's own schema), out of this architecture-only Work
Package's scope, named here as a real candidate for a future Work
Package if the manual classification this document specifies proves
insufficient in practice.

### 5. Process — the six-discipline Programme Review

Unchanged from the proven `WP 10.9A`/`WP 11.9.0` shape, now permanent
rather than merely repeated:

```
Chief Architect ──┐
Principal Eng. ────┤
Workflow Eng. ──────┼──► Independent reviews, each against          ──► Programme Review
QA Lead ────────────┤    its own category (§2), no discipline        (reconciliation,
Technical Author ───┤    sees another's findings until this step     cross-checked,
Product Manager ────┘                                                 Part B)
                                                                            │
                                                                            ▼
                                                          Reconciled Definition of Done (§3)
                                                                            │
                                                                            ▼
                                                          Engineering Release Report
                                                          (verdict, §6 — Product Approval's
                                                           own explicit, per-occasion decision)
```

Each of the six performs their own review **before** seeing the
others' conclusions — the deliberate, already-proven discipline that
found `TD-42`/`TD-43` at `v0.11.0`: a QA Lead who deferred to a
Workflow Engineer's own "looks fine" would not have independently
exercised the release-tooling scripts against real fixtures. Small,
genuinely narrow corrections (a stale register figure, a missing
cross-reference) found during the review may be corrected within the
same sign-off Work Package, exactly as `WP 11.9.0`'s own Part C did —
never a code, ADR, or architecture change, which returns to the
engineering tier instead.

### 6. Release Decision — fixed verdict vocabulary

Exactly four verdicts, replacing the free-text wording every prior
release independently invented, and derived **exclusively** by §4's
own four-row priority table above — this section states what each
verdict means and its own historical precedent; it does not restate,
and must never re-derive independently of, §4's own decision procedure
(this duplication was the exact defect `WP 12.9.0`'s own architecture
review found and this follow-up removes):

| Verdict | Meaning (§4's own priority row) | Precedent |
|---|---|---|
| **NOT READY** | §4 row 1: any category Not Ready | **Used.** *(Corrected `WP 16.4B-R6` round 2, 2026-09-06: this cell read "Not yet used; the case this taxonomy exists to make unambiguous", which was already false when written — `WP 12.9.0` and `WP 12.9.2` both derived `NOT READY` on Verification readiness, and `WP 12.9.4` records its own verdict moving from `NOT READY` to `ACCEPT WITH OBSERVATIONS` in place. It is now false a second way: `v0.16.0`'s Engineering Release Report recommends `NOT READY` on one open Release Blocking finding, `TD-140`. Corrected here only because leaving it would contradict a live release document; the pre-existing `v0.12.0` inaccuracy is recorded, not investigated further, as it is outside that Work Package's scope.)* |
| **ACCEPT WITH OBSERVATIONS** | §4 row 2: no category Not Ready, but ≥ 1 Disclosed, Non-Blocking finding exists anywhere | `v0.11.0`'s own wording, formalised |
| **CERTIFIED WITH ACCEPTED TECHNICAL DEBT** | §4 row 3: no Not Ready category, no Disclosed, Non-Blocking finding, but ≥ 1 Pre-Existing, Unaffected finding exists anywhere | `v0.6.0`'s own wording, formalised |
| **CERTIFIED** | §4 row 4: no finding of any of the three kinds exists, anywhere, in the entire review | Not yet used; the ideal case |

Because §4's table is a strict priority order, not four independent
conditions, exactly one of these four rows is ever true for a given
review — every verdict is therefore reachable (each row's own
precondition is independently satisfiable), mutually exclusive (only
the first matching row ever fires), and objectively derivable (the
only judgement involved anywhere is classifying each finding into one
of §4's three kinds, never a second, separate judgement about the
category or the release as a whole). Only Product Approval issues this
verdict (Engineering Governance §9); the Programme Review recommends,
it does not decide — the same tier-separation §9 already establishes,
restated for this specific gate, not weakened by it.

### 7. Execution boundary

This architecture is designed, not yet exercised against `v0.12.0`.
The actual `v0.12.0` Engineering Readiness Review — the six independent
reviews, the reconciled Programme Review, the Engineering Release
Report, and Product Approval's own verdict — is a distinct, future
action (this Work Package's own "release engineering," explicitly out
of scope here). Before that execution can begin, three preconditions
named directly by the Repository Investigation above must first be
resolved, none of them by this Work Package: `WP 12.2A`'s own
disposition committed and merged into `main`; `docs/releases/v0.12.0/Release
Notes.md` produced (a release-time document, per §2.5); the Engineering
Vocabulary Register's own missing `Governance Index.md` link addressed
or explicitly accepted as Disclosed-Non-Blocking by that future
review.

---

## Alternatives Considered

### 1. Continue the informal, per-release checklist (status quo)

Leave sign-off as an unwritten convention, reinvented narratively each
release. **Rejected** — the exact problem this Work Package was
commissioned to close; two consecutive releases already converged on
the same shape without being asked to, which is itself the argument
for finally naming it rather than trusting convergence to continue by
luck a third time.

### 2. A single-reviewer sign-off

One person (or one role) certifies the release, replacing the
six-discipline structure with one signature. **Rejected** — direct,
concrete counter-evidence exists: `WP 11.9.0`'s own QA Lead
independently found two genuine release-tooling defects (`TD-42`,
`TD-43`) specifically *because* the review was independent, not
reconciled early. A single reviewer covering all five categories at
once is the same failure mode Engineering Governance §9 already
rejects for ordinary Technical Review — one tier is not equipped to
catch what a different discipline's own scrutiny catches.

### 3. A purely automated gate

Extend `governance-healthcheck.ps1` (or a new tool) to be the sole
arbiter of release readiness — no human review at all.
**Rejected** — the tool already covers exactly one of five categories
well (Governance readiness's own register-consistency evidence) and
cannot evaluate Architecture readiness's ownership/layering judgement,
Implementation readiness's "does this actually match the ADR's own
Decision text" question, or Release readiness's product/commercial
judgement — all of which require the human judgement Engineering
Governance §9 reserves for the Architecture and Product Approval
tiers respectively. Automation remains additive evidence *within* the
process (§2.3, §2.4), never a replacement for it.

### 4. The five-category ERR, six-discipline Programme Review (recommended, adopted)

Formalises exactly what has already been proven twice, adds the one
genuine, missing piece — a written blocking taxonomy (§4) and a fixed
verdict vocabulary (§6) — and changes nothing about the underlying
Build/Test/Technical Review/Merge-Release gates Engineering Governance
§2 already specifies. Lowest-disruption option that closes the actual,
named gap (an unwritten process) without inventing a new mechanism
this project has no evidence it needs.

---

## Required ADR

**`ADR-0106`** — required. Engineering Governance §5's own criteria are
met on two independent grounds: this decision **establishes a
convention every future release is expected to follow** (the
textbook case §5's own example, `ADR-0003`, already names), and a
**genuine alternative was seriously considered and rejected** — three
of them (above), not merely one, each with a concrete reason grounded
in this project's own evidence rather than abstract preference. No
existing ADR governs this specific question: `ADR-0009`/`ADR-0017`/
`ADR-0023` govern architectural layering and composition, not release
process; the closest existing citation, Engineering Governance §7,
states the *principle* but was never itself an ADR (it is the
governance document itself, §9's own "Technical Review... may also
originate new governance-level requirements, as this very document
does"). `ADR-0106` is the first ADR whose subject is the release
sign-off process itself.

---

## Documentation Impact

- **This document** — new, `docs/architecture/Engineering Readiness
  Review Architecture.md`. Standing, living reference; the source of
  truth for every future ERR.
- **`ADR-0106`** — new (Decision Evaluation, above).
- **`Engineering Governance.md` §7** — one additive cross-reference
  sentence, naming this document as §7's own detailed specification
  for *evaluation criteria*, mirroring exactly how §2 item 4 already
  points to `05-release-engineering.md` for *mechanics*. No existing
  sentence in §7 is rewritten or removed.
- **Academy retrospective** — new, `docs/academy/03 Work
  Packages/WP12.9.0-release-preparation-and-engineering-sign-off-architecture.md`.
- **`ADR Register.md`, `Architecture Document Register.md`, `Academy
  Register.md`, `Documentation Register.md`** — each updated to record
  this Work Package's own new documents, counts re-derived directly,
  not carried forward.
- **`docs/releases/v0.12.0/WorkPackages.md`, `PROJECT_STATUS.md`** —
  updated to record this Work Package's own status and findings.
- **Not modified**: Technical Debt Register, Future Capability
  Register, Governance Index, Documentation Register's path-check
  false positives — every finding above is disclosed, not corrected,
  consistent with this Work Package's own explicit "no implementation"
  constraint; correcting them is properly a future execution/closure
  Work Package's own scope.

## Validation Against Governing Documents

- **`FOUNDATION.md`** — non-negotiable 8 ("no tier of authority
  substitutes for another... Product Approval decides whether reviewed,
  working software actually ships, sought explicitly every time") is
  the direct architectural basis for §6's own tier separation
  (Programme Review recommends, Product Approval decides) — restated,
  not weakened.
- **Engineering Governance §2** (Review Gates) — this architecture adds
  a named, structured Gate 4 specification; §2 items 1–3 are entirely
  unchanged.
- **Engineering Governance §5** (ADR Creation Rules) — applied honestly
  above; the criteria are genuinely met, not assumed.
- **Engineering Governance §7** (Release Approval Process) — every
  numbered item (1–7) remains authoritative and unmodified; this
  architecture is the evaluation-criteria layer §7 itself never
  specified, exactly as `05-release-engineering.md` is already the
  mechanics layer.
- **Engineering Governance §9** (Decision Authority) — the three-tier
  model (Architecture / Technical Review / Product Approval) is
  reflected exactly: the six-discipline Programme Review sits at the
  Technical Review tier (examine, question, recommend); Product
  Approval alone issues the verdict.
- **`ADR-0009`/`ADR-0017`/`ADR-0023`** — unrelated in subject
  (composition-root ownership, host-owned collaborators, downward
  layering); Architecture readiness's own evidence requirements (§2.1)
  cite them as what a future Chief Architect checks against, not as
  documents this decision revises.

## Related Documents

`docs/releases/FOUNDATION.md`; `docs/academy/06 Engineering
Standards/Engineering Governance.md` (§2, §5, §7, §9); `docs/academy/06
Engineering Standards/05-release-engineering.md`; `docs/releases/v0.11.0/WP11.1B
Engineering Workflow.md`; `docs/releases/v0.10.0/WP10.9A Engineering
Release Report.md`; `docs/releases/v0.11.0/WP11.9.0 Engineering Release
Report.md`; `docs/releases/v0.12.0/WorkPackages.md`; `ADR-0101`
(`Tempest.App`/`WorkspaceShell` classification, cited for release
artefact packaging); `Technical Debt Register.md`; `Future Capability
Register.md`; `Governance Index.md`.
