# v0.9.0 Retrospective — "Mechanical Foundation"

## What This Document Is

Like `WP 5.4`'s, `WP 6.8`'s, `WP 7.4.0`'s, and `WP 8.9.0`'s own prior
release retrospectives, this document does not design or implement a
platform capability — it verifies, closes, and prepares an entire
release for Product Approval. Shaped around the same five questions
that kind of Work Package actually raises, not forced into the standard
13-section feature template. Written by `WP 9.9.0` (Release Preparation
& Product Baseline), the release's own closing Work Package.

## 1. Introduction

`v0.9.0` ("Mechanical Foundation") is the sixth release TempestOS has
shipped, and the first to give the platform genuine engineering
*discipline* content, not merely the presentation shell (`v0.8.0`'s own
Workspace) or the shared vocabulary (`v0.8.0`'s own Engineering Domain)
that made it possible. Where `v0.8.0` proved the platform *could* show a
user their own engineering work and generalise what an engineering
object is, `v0.9.0` proves it actually *does*, six times over: Mechanical
Product Structure, Requirements Management, Engineering Calculations,
Engineering Documents, Verification Management, and Manufacturing, each
real, browsable, and cross-linked. Seven Work Packages, zero
architectural rework, zero Release Blocking findings.

## 2. What Was Achieved

**Six real Engineering Disciplines, one proven extension model.**
Mechanical Product Structure (`WP 9.0A`, extended by `WP 9.0B` with BOM/
Baseline/Configuration management) proved the Kind-keyed Workspace
extension model (`ADR-0067`) against a real Engineering Domain object
for the first time. Requirements Management (`WP 9.1A`) proved it again
against a genuinely different Domain shape (immutable-snapshot
requirements, not `EngineeringObjectBase`-derived), needing additive
service methods rather than facet casts (`ADR-0084`). Engineering
Calculations (`WP 9.2A`) proved the model needs no Domain-layer change
at all when the underlying Framework is already real, and introduced the
one adapter this release needed (`CalculationTemplateRegistry`,
`ADR-0086`) to bridge a genuinely generic-per-Template execution shape.
Engineering Documents (`WP 9.4A`) and Verification Management (`WP 9.3A`)
each proved the identical zero-Domain-layer-change pattern again, the
latter completing, in real time, *after* the former despite carrying
the earlier number — a genuine, disclosed numbering gap, not silently
reordered. Manufacturing (`WP 9.5A`), closing the discipline programme,
proved something new: that a Kind-keyed provider already generic over
its own Kind parameter can serve a *second, foreign* discipline outright
— the first genuine cross-Work-Package Workspace-layer reuse this
project has ever performed, verified correct by dedicated tests rather
than assumed.

395 new tests (1631 → 2026). 12 new ADRs (79 → 91), all Accepted, zero
gaps. 8 new Academy articles (116 → 124). 12 new production modules (22
→ 34) — the largest single-release module addition this project has
recorded. 8 new Technical Debt items, one per Work Package, every one
disclosed at the moment it was found. Zero breaking changes to any
prior release's own contract.

## 3. Architectural Lessons

**"Reuse what already exists" generalises to reuse *across* disciplines,
not only reuse *within* one.** Every prior release's own version of this
lesson (`v0.7.0`'s six Engineering Core frameworks, `v0.8.0`'s
presentation and shared-vocabulary layers) was about a single Work
Package reusing an existing *mechanism*. `WP 9.5A` extends it one step
further: reusing another Work Package's own already-shipped *instance*
of a Kind-keyed provider, for an unrelated Kind its own author never
anticipated. This is a qualitatively new kind of reuse for this project,
made possible only because six prior Work Packages, across two releases,
consistently kept every provider generic over its own `Kind` parameter
rather than hardcoding it — a discipline that paid off in a form none of
those six Work Packages could have predicted at the time they applied
it.

**An established "constructor-inject the N prior sample modules"
pattern is not self-maintaining — it must be rechecked per new module,
not assumed to continue working because it held for the last several in
a row.** `WP 9.3A` set a five-module cross-sample-dependency precedent;
`WP 9.5A`'s own initial implementation plan extended it to six
naturally, before a direct ordinal-Id check (during planning, not
coding) showed the sixth dependency would have been a genuine
`ModuleLifecycleManager` initialisation-order defect. The pattern's own
*shape* ("depend on the prior modules") transferred; its own specific
*applicability* to a sixth dependency did not, and had to be checked
freshly.

**A documented "this command is Kind-agnostic" claim is worth verifying
empirically the first time a different discipline actually needs to
rely on it, not merely trusting the prior Work Package's own prose.**
`Mechanical.SetBomLineCommand` (`WP 9.0B`) and
`Verification.RecordVerificationResultCommand` (`WP 9.3A`) had each
*documented* Kind-agnosticism since their own original Work Package; `WP
9.5A` is the first to actually dispatch either against a foreign Kind
and assert on the result, turning a plausible, unexercised claim into a
verified fact — now itself a reusable precedent for any future
discipline claiming reuse of an existing command.

## 4. Implementation Lessons

**A Workspace reader's own specific data-access mechanism does not
transfer just because its own *shape* looks identical to a prior
Work Package's own reader.** `WP 9.3A`'s first `VerificationRecordReader`
draft copied `CalculationRecordReader`'s own shape verbatim, assuming
`EngineeringDomainContext.RelationshipRepository` would hold the same
kind of link — nine tests failed outright, because
`VerificationService.RecordAsync` links its own subject to a record
through the raw document store only, never through
`IHasRelationships.LinkAsync`. Caught by a failing test, not by
inspection, and corrected before any commit (`TD-32`).

**A shared Cockpit helper reused three times can hide a discipline-specific
assumption none of its first two callers happened to expose.**
`EngineeringCockpit.FormatCoverage`'s own zero-denominator text was
written, at `WP 9.1A`, with Requirements specifically in mind — and
reused, silently correctly-shaped but inaccurately-worded, by `WP 9.2A`
and `WP 9.3A`'s own Calculations/Verification coverage cards. `WP 9.5A`
is the first Work Package to read the helper's own actual implementation
before reusing it a third time, found the inaccuracy, and chose not to
compound it — a small, disclosed finding (`TD-33`) that neither of the
two prior reuses happened to surface.

**Independent, plan-time recalculation of an ordinal-Id ordering
constraint caught a genuine defect before any code existed — cheaper
than catching it via a failing test, and much cheaper than catching it
after commit.** `WP 9.5A`'s own dropped sixth sample-module dependency
(above) is a direct example: the same class of defect `WP 9.3A`'s
`TD-32` finding shows can otherwise only be caught by execution, caught
here purely by re-deriving two literal strings' own alphabetical order
during planning.

## 5. Repository Maturity

**The four-Engineering-Foundation-framework Platform Service Map/
Register gap, first found by `WP 7.3A`, confirmed open by `WP 7.4.0`
and `WP 8.9.0`, is now confirmed open a *third* consecutive
release-closing review.** Every one of `v0.9.0`'s own seven Work
Packages individually reconfirmed it open in their own Platform Services
Register entry — the gap has now survived two entire release cycles of
dedicated closing review declining to fix it as out of scope, exactly
the pattern `v0.8.0`'s own Retrospective flagged as needing either a
dedicated Work Package or a firm decision that it will never be worth
fixing. **That recommendation was not acted on during `v0.9.0` — this
is now the single most persistent disclosed governance finding in this
project's history**, named again, more forcefully, in the Product
Approval Report.

**A second, genuinely new documentation-completeness finding surfaced
and recurred within this same release.** `WP 9.3A` found `docs/governance/`
contains 35 files against a claimed "32 governance documents total";
`WP 9.5A` reconfirmed it open; this closing review reconfirms it a
third time, still unresolved. Unlike the Platform Service gap (a genuine
content gap — rows never written), this is a stale *summary figure*
describing an undocumented historical categorisation — the underlying
27 individually-tracked registers remain accurate. A different kind of
governance debt, but the same underlying process failure: a
release-closing review keeps finding it, and keeps correctly declining
to fix it as out of scope, without anyone deciding to close it
permanently.

**Every count this Work Package independently re-derived from the
repository directly matched the register that claimed it — zero
arithmetic-correction findings this release**, unlike `v0.8.0`'s own
disclosed 39→38 concrete-class correction. ADRs (91), Rejected Designs
(45), Technical Debt Register items (33), Future Capability Register
entries (62), Academy articles (124), public interfaces (168), modules
(34) — all independently verified via direct `grep`/`find` against
source, all internally consistent, including the full seven-Work-Package
test-count and ADR-count addition chains re-summed exactly. Test suite
stability was re-confirmed across four full-suite runs (two Debug, two
Release, the second Release run reproducing the actual release script's
own solution-file path), plus a scoped 516-test run and a flake-check
run, zero flakes, zero regressions, matching every prior release's own
closing-review standard.

**A dedicated Security Review was performed for every single Work
Package this release — a full recovery from `v0.8.0`'s own
disclosed, entire-release-cycle lapse.** `v0.7.0` performed three;
`v0.8.0` performed zero; `v0.9.0` performed seven, one per Work Package,
restoring the practice `v0.8.0`'s own Retrospective named as its own
single most important carry-forward recommendation — and, unlike the
Platform Service gap above, this recommendation *was* acted on.

## 6. Recommendations for What Comes Next

*(Written by `WP 9.9.0`'s own first pass. Item 1 was acted on before
this document's own second-pass addendum, §7, was written — left
exactly as originally written, per "never silently modify historical
records"; its own resolution is recorded in §7, not retrofitted here.)*

1. **Make a firm decision about the four-Engineering-Foundation-framework
   Platform Service Map/Register gap — now, not after a fourth
   consecutive review re-discovers it.** Three consecutive closing
   reviews finding the identical, unfixed gap is no longer a
   documentation-currency question; it is a standing process failure
   this project has explicitly declined to resolve twice already.
2. **Reconstruct or formally retire the "32 governance documents"
   figure**, closing the second governance-completeness gap this
   release surfaced before it, too, becomes a multi-release pattern.
3. **Build `FCR-0005` (Governance Register Health-Check Tooling)** —
   now disclosed as recurring across a *third* consecutive
   release-closing review, the same escalation `v0.8.0`'s own
   Retrospective already gave it.
4. **Continue the now-fully-restored dedicated-Security-Review discipline**
   into whatever Work Package follows — the one `v0.8.0`-era
   recommendation this release actually closed; do not let it lapse a
   second time.
5. **Build a dedicated Governance & Risk Workspace** (`FCR-0056`) — every
   Domain class it needs is already compiled and already live in the
   base sample module, the identical starting position every one of
   this release's own six disciplines began from, and the most
   concrete, ready-to-start next Engineering Discipline candidate this
   release names.

## 7. Second Pass — `WP 9.8B` and Renewed Verification

*(Added by `WP 9.9.0`'s own second pass, commissioned by the Product
Owner after `WP 9.8B` closed §6 Recommendation 1, above. Sections 1–6
are left exactly as the first pass wrote them; this section records
what changed, appended, not interleaved, per this project's own
"disclose, do not silently modify" discipline.)*

**Recommendation 1, above, was acted on.** `WP 9.8B` (Platform Service
Register Reconciliation), commissioned after `WP 9.9.0`'s own first
pass despite carrying an earlier number, closed the
four-Engineering-Foundation-framework Platform Service gap in full —
the first standing recommendation in this project's history to be
closed by a Work Package created specifically for that purpose. This
second pass independently re-verified the closure rather than trusting
`WP 9.8B`'s own claim, and confirms it: all five governance documents
this project maintains for Platform Services are now mutually
consistent for all 30 real services, including the four that were
missing.

**A genuine, new finding this pass — the first actually-observed
instance of a long-narratively-disclosed test flake.** Five full-suite
runs this pass (rather than the first pass's own four) caught a live
instance of `CompositeLogSinkTests`'s own intermittent, cross-test-class
`Console.Error`-capture race — informally disclosed by name since `WP
6.3`, referenced by every subsequent release-closing review's own
dedicated flake-check, and, until this pass, never once actually
observed by any of them. Root-caused directly (a race between
`CompositeLogSink.Write`'s own `Console.Error.WriteLine` failure report
and any concurrently-running `[Collection("Console output
capture")]`-tagged test's own `Console.SetOut`/`SetError` redirection),
confirmed non-reproducible in isolation (5/5 further isolated runs
passed), and formally registered for the first time (`TD-34`) — closing
a second, smaller governance-completeness gap this release's own
verification work happened to surface: a real, long-standing platform
characteristic that had only ever been disclosed in prose, never
tracked.

**Recommendation 2 (the "32 vs. 35" figure) and Recommendation 3
(`FCR-0005`) remain open** — both outside `WP 9.8B`'s own narrower
scope and outside this pass's own. `FCR-0005`'s own case is now
stronger than at any prior point: `WP 9.8B`'s own existence is direct,
first-hand evidence of the manual-effort cost automation would have
eliminated.

**This second pass is itself a new data point about this project's own
release-closing discipline**: a "verify, remediate, re-verify" cycle,
performed once, in full, for the first time — proving that a disclosed
finding, once actually acted on, can be independently re-confirmed
closed rather than merely trusted closed.

## Key Takeaways

1. Reuse can generalise across Work Packages, not only within one — but
   only because six prior Work Packages, across two releases, kept every
   Kind-keyed provider genuinely generic rather than hardcoding it,
   without knowing in advance that discipline would ever pay off this
   way.
2. An established multi-Work-Package pattern (constructor-injecting the
   prior sample modules; reusing a shared Cockpit helper) must be
   rechecked on its own merits every time it is extended one step
   further, not assumed to continue holding because it held for the
   last several instances.
3. A release-closing review's own distinct value, now confirmed a fifth
   time, is re-deriving every claim directly from the repository — this
   time finding zero arithmetic errors, itself evidence that the
   discipline of disclosing and correcting them at each Work Package's
   own time (rather than only at release-close) is working.
4. A governance gap disclosed and deferred three times in a row, across
   two entire release cycles, is no longer "recommended for a future
   Work Package" — it is a standing process question this project has
   now explicitly failed to answer twice, and should not fail a third
   time. *(Added, §7: this specific gap was subsequently closed by `WP
   9.8B` — the failure pattern this takeaway names is not permanent,
   only persistent until a Work Package is actually commissioned to
   address it directly, rather than folded into unrelated scope.)*
5. **(Added, §7.)** A "verify, remediate, re-verify" cycle is a genuine,
   repeatable release-closing pattern, not merely a theoretical one — a
   second, independent verification pass can find a release in a
   materially more consistent state than the first pass left it, and
   can also surface a genuinely new finding (`TD-34`) a differently-timed
   first pass had no opportunity to catch, simply by running the same
   verification steps again rather than assuming they would return the
   identical result.

## Related Documents

`docs/releases/v0.9.0/ReleaseNotes.md`; `docs/releases/v0.9.0/WP9.9.0
Release Readiness Report.md` (first pass) and `(Second Pass).md`;
`docs/releases/v0.9.0/WP9.9.0 Product Approval Report.md` and `(Second
Pass).md`; `docs/releases/v0.9.0/WP9.9.0 Engineering Statistics
Report.md` and `(Second Pass).md`; `docs/releases/v0.9.0/WP9.9.0
Architecture Baseline Summary.md` and `(Second Pass).md`;
`docs/releases/v0.9.0/WP9.9.0 Engineering Capability Summary.md` and
`(Second Pass).md`; `docs/releases/v0.9.0/WP9.8B Reconciliation
Report.md`; `docs/academy/03 Work Packages/
WP8.9.0-release-preparation-and-product-baseline.md`;
`docs/academy/03 Work Packages/WP9.8B-platform-service-register-
reconciliation.md`; `docs/governance/Future Capability Register.md`
(`FCR-0005`); `docs/governance/Engineering/Platform Services
Register.md`; `docs/governance/Quality/Technical Debt Register.md`
(`TD-34`).
