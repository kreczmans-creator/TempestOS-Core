# ADR-0106: Release Sign-Off Is Governed by a Permanent, Five-Category Engineering Readiness Review — Never an Ad Hoc, Per-Release Checklist

## Status

Accepted — `v0.12.0`, `WP 12.9.0` (Release Preparation & Engineering
Sign-Off, Architecture phase), 2026-08-12. Architecture only; no
production code, no test, no release engineering accompanies this
decision. See `docs/architecture/Engineering Readiness Review
Architecture.md` for the full model this ADR authorises.

**Corrected, `WP 12.9.0` Architecture Review Follow-Up, same date,
before this ADR's first commit.** This Work Package's own architecture
review found the blocking taxonomy and verdict vocabulary, as
originally accepted, left the relationship between a finding's kind,
category status, and release verdict implicit — provably contradictory
for `CERTIFIED WITH ACCEPTED TECHNICAL DEBT`, whose own stated
condition could never actually hold. The Decision below now states the
corrected, single, exclusive derivation (full detail: Architecture
Document §4); the taxonomy's own three kinds and the four-value verdict
vocabulary are otherwise unchanged from what was originally accepted.

## Context

Every TempestOS release since `v0.6.0` has undergone a release-readiness
review recommending a verdict before tagging (`WP 6.8`, `WP 7.4.0`, `WP
8.9.0`, `WP 9.9.0`, `WP 10.9A`, `WP 11.9.0`) — Engineering Governance
§7 already states this as a principle, and `05-release-engineering.md`
already specifies the *mechanics* of tagging itself (branching,
pull requests, `scripts/new-release.ps1`, `release.yml`). Neither
document specifies what evidence a reviewer must gather, in what
categories, against what pass/fail criteria, before recommending that
verdict — that gap has been closed narratively, differently, at every
one of the six prior sign-offs.

Two consecutive releases, unprompted by any written requirement,
independently converged on the same shape: `WP 10.9A` introduced a
six-discipline Programme Review (Chief Architect, Principal Software
Engineer, Workflow Engineer, QA Lead, Technical Author, Product
Manager, each reviewing independently before reconciliation) and a
12-item Definition of Done; `WP 11.9.0` reused that exact shape,
explicitly citing `WP 10.9A` as "the standard applied." `WP 11.9.0`'s
own QA Lead independently found two genuine, previously-undisclosed
release-tooling defects (`TD-42`, `TD-43`) specifically because that
review was performed independently, without deferring to any other
discipline's own conclusion first — direct, concrete evidence that the
multi-discipline structure catches real defects a single reviewer, or
a purely automated check, would not.

`WP 12.9.0` was commissioned specifically to close this gap: design the
permanent TempestOS release sign-off model, not a checklist copied
from either prior release, treated as this project's first formal
Engineering Readiness Review (ERR).

## Decision

Release sign-off is governed by a permanent, five-category Engineering
Readiness Review, executed by the proven six-discipline Programme
Review structure, evaluated against a fixed blocking taxonomy and a
fixed, four-value verdict vocabulary — replacing free-text, per-release
wording with a named, reusable model every future release cites rather
than reinvents. Full specification: `docs/architecture/Engineering
Readiness Review Architecture.md`.

**The five categories** — Architecture readiness, Implementation
readiness, Verification readiness, Governance readiness, Release
readiness — each independently reviewed, each with its own required
evidence and blocking conditions (Architecture Document §2). A single
category assessed **Not Ready** is sufficient to prevent Product
Approval from proceeding, regardless of the other four's own strength.

**The blocking taxonomy** (new; no prior sign-off ever wrote this down)
— every finding is one of exactly three kinds, classified solely by its
own nature, never by a category's or the release's own status (which
are consequences of this classification, not inputs to it):
**Release Blocking** (a failing Build/Test Gate on `main`, an
undocumented public API break, or a Technical Debt/Future Capability
item with *real, demonstrated* evidence of harm); **Disclosed,
Non-Blocking** (real, tracked, does not prevent this release, newly
raised or newly relevant this review, but must be named); **Pre-Existing,
Unaffected** (a gap this release neither caused, worsened, nor newly
re-raised, disclosed for completeness).

**The derivation — a single, exclusive priority order, not independent
rules** (Architecture Document §4 in full): a category carrying any
Release Blocking finding is **Not Ready**; a category with none but at
least one Disclosed, Non-Blocking finding is **Pass, with observations**;
otherwise it is **Pass** (Pre-Existing, Unaffected findings never change
a category's own status). The release verdict is then computed once,
in strict priority order, over every finding raised across all five
categories: any category **Not Ready** → `NOT READY`; else any
Disclosed, Non-Blocking finding anywhere → `ACCEPT WITH OBSERVATIONS`
(`v0.11.0`'s own wording, formalised); else any Pre-Existing, Unaffected
finding anywhere → `CERTIFIED WITH ACCEPTED TECHNICAL DEBT` (`v0.6.0`'s
own wording, formalised); else, no finding of any kind anywhere →
`CERTIFIED`. Because this is a priority order and not four independent
conditions, exactly one verdict is ever reachable for a given review —
every verdict is therefore mutually exclusive, every verdict is
reachable in principle, and every verdict is objectively derivable from
nothing but each finding's own §4 classification. Only Product Approval
issues this verdict (Engineering Governance §9); the Programme Review
recommends, it never decides.

**Tier separation is unchanged, not weakened.** The Programme Review
sits at the Technical Review tier — it examines, questions, and
recommends. Product Approval alone decides whether the release ships,
exercised explicitly, per occasion, exactly as Engineering Governance
§9 and `FOUNDATION.md` non-negotiable 8 already require. This ADR adds
structure to how the Technical Review tier gathers and organises its
own evidence; it does not move, weaken, or duplicate any tier's
existing authority.

## Alternatives Considered

1. **Continue the informal, per-release checklist (status quo).**
   Rejected — the exact problem this decision exists to solve; trusting
   two-releases-of-luck to continue indefinitely is not a plan.
2. **A single-reviewer sign-off**, collapsing the six disciplines into
   one signature. Rejected — `WP 11.9.0`'s own `TD-42`/`TD-43` finding
   is direct, concrete counter-evidence: independent review by a
   distinct discipline caught what a generalist single reviewer,
   covering everything at once, plausibly would not.
3. **A purely automated gate** (extending `governance-healthcheck.ps1`
   to be the sole arbiter). Rejected — the tool evaluates one of five
   categories well (Governance readiness's own register-consistency
   evidence) and structurally cannot evaluate the architectural,
   implementation-fidelity, or product judgement the other four
   require — judgement Engineering Governance §9 reserves for the
   Architecture and Product Approval tiers specifically, never a tool.
4. **The five-category ERR, six-discipline Programme Review — adopted.**
   Formalises what is already proven twice; adds only the two genuinely
   missing pieces (a written blocking taxonomy, a fixed verdict
   vocabulary); changes nothing about the Build/Test/Technical
   Review/Merge-Release gates Engineering Governance §2 already
   specifies.

## Consequences

**Positive.** Every future release's sign-off is evaluated against the
same, named criteria — no reviewer re-derives from scratch what
"ready" means, and no release's own verdict wording is ambiguous
relative to another's. The blocking taxonomy makes explicit a judgement
every prior sign-off already made narratively, so a future reviewer can
apply it consistently rather than reinvent the standard each time. The
six-discipline structure, now named rather than merely repeated,
remains cheap to execute (it has been performed twice already, entirely
within a single Work Package each time) while preserving the exact
property (independent review before reconciliation) that has already
found real defects twice.

**Negative / cost.** A fixed model is slightly less flexible than an
ad hoc review free to shape itself around each release's own character
— mitigated by the model's own five categories being general enough
that a governance-only release (`v0.11.0`, `v0.12.0`) and a
feature-heavy release (`v0.9.0`, `v0.10.0`) both fit without
modification, evidenced directly by re-mapping both prior sign-offs'
own 12-item Definition of Done onto this ADR's five categories without
any item needing to be dropped or reworded (Architecture Document §3).

**Neutral / explicitly deferred.** This ADR does not extend the
Technical Debt Register's own schema to mechanically encode the
blocking taxonomy (a Release-Blocking/Disclosed/Pre-Existing column) —
that is a genuine, separate implementation decision, named as a future
candidate (Architecture Document §4) rather than assumed necessary
without evidence the manual classification this ADR specifies proves
insufficient in practice.

## Future Considerations

- If a future release finds the six-discipline structure genuinely
  insufficient (a real category this model does not cover, or a
  discipline whose findings are consistently redundant with another's),
  that is itself Technical Review material under Engineering Governance
  §9 — a candidate for revising this ADR directly, not a silent
  deviation from it.
- If the Technical Debt Register's manual blocking-taxonomy
  classification (§4 of the Architecture Document) proves error-prone
  or inconsistent across multiple releases, formalising it as a
  register-schema field is the named, anticipated next step.
- The first real exercise of this model — the actual `v0.12.0`
  Engineering Readiness Review — is expected to surface refinements
  this architecture-only pass could not anticipate without executing
  it; any such refinement is itself recorded against this ADR, not
  silently absorbed into practice.

## Related Documents

`docs/architecture/Engineering Readiness Review Architecture.md` (the
full model); `docs/academy/06 Engineering Standards/Engineering
Governance.md` §2, §5, §7, §9; `docs/academy/06 Engineering
Standards/05-release-engineering.md`; `docs/releases/v0.10.0/WP10.9A
Engineering Release Report.md`; `docs/releases/v0.11.0/WP11.9.0
Engineering Release Report.md`; `FOUNDATION.md` non-negotiable 8.
