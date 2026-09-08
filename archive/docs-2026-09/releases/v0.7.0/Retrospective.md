# v0.7.0 Retrospective — "Engineering Foundation"

## What This Document Is

Like `WP 5.4`'s own `v0.5.0` retrospective, this document does not
design or implement a platform capability — it verifies, closes, and
prepares an entire release for Product Approval. Shaped around the same
five questions that kind of Work Package actually raises, not forced
into the standard 13-section feature template. Written by `WP 7.4.0`
(Release Preparation & Product Baseline), the release's own closing
Work Package.

## 1. Introduction

`v0.7.0` ("Engineering Foundation") is the fourth release TempestOS has
shipped, and the first to give the platform genuine engineering-domain
understanding rather than only application-platform infrastructure.
Where `v0.6.0` proved the platform could support independent, mutually
unaware services (Reporting, Identity, Settings, Audit, and five more),
`v0.7.0` proves it can support **engineering** — a shared notion of a
requirement, a material, a calculation, and a verified claim, each
consumed identically by whatever discipline-specific module eventually
builds on it. Twelve Work Packages across two sequential programmes,
zero architectural rework, zero Release Blocking findings.

## 2. What Was Achieved

**Programme one — Engineering Foundation (`WP 7.0A`–`WP 7.1F`):** a
complete vision and capability register (`WP 7.0A`), a full dependency
graph and six-programme grouping (`WP 7.0B`), public contracts for five
frameworks (`WP 7.0C`), and five real implementations — Engineering Data
Model (`WP 7.1A`), Units & Quantities (`WP 7.1B`), Materials (`WP 7.1C`),
Calculations (`WP 7.1D`), Verification (`WP 7.1E`) — each consuming only
what the layer beneath it already proved, none inventing a second
storage or query mechanism. `WP 7.1F` closed the programme with an
independent certification: **ENGINEERING CORE CERTIFIED WITH ACCEPTED
TECHNICAL DEBT**.

**Programme two — Systems Engineering Foundation (`WP 7.2A`–`WP 7.3A`):**
a genuinely evidence-based strategic selection among seven candidate
programmes (`WP 7.2A`, scoring Requirements & Verification 46/55, the
highest), a complete architecture (`WP 7.2B`), complete contracts
(`WP 7.2C`), and the first real implementation — the Requirements Engine
(`WP 7.3A`), the first Systems Engineering Foundation capability to
exist as running, tested code.

390 new tests (1016 → 1406). 9 new ADRs (52 → 61), all Accepted, zero
gaps. 18 new Academy articles (86 → 104). 16 new public interfaces
(64 → 80). 5 new production modules (15 → 20). Zero breaking changes to
any Platform Foundation, Developer Experience, or Platform Services
contract.

## 3. Architectural Lessons

**The reuse-of-existing-mechanism pattern now holds across six
independent frameworks, not four.** Materials, Calculations,
Verification, and now Requirements each reached the identical
conclusion — build the new concept as an `IEngineeringDocument`, express
every relationship as a `DocumentReference`, introduce zero new storage
or traversal mechanism — independently, at four different Work
Packages, each arriving at the answer on its own merits rather than
copying a prior decision. This is no longer a pattern worth
re-justifying each time; it is the default hypothesis a future
Engineering Discipline module should start from.

**A digital thread does not require a dedicated traversal mechanism —
this release proved the claim, not merely argued it.** `WP7.2B Digital
Thread Architecture.md` predicted this at the architecture stage;
`WP 7.3A`'s own `GetEvidenceAsync` — composing
`IVerificationService.GetVerificationHistoryAsync` with
`IEngineeringDocumentStore.GetReferencesAsync` into a single read — is
the first place in the entire codebase that actually builds the
composed, multi-source view the architecture predicted, closing the
loop between two Work Packages three stages apart.

**Two full architecture-then-contract-then-implementation cycles, zero
rework, is the strongest evidence yet that this project's own
architecture-first discipline (`FOUNDATION.md` §1) earns its own cost.**
Every one of the five Engineering Foundation frameworks and the
Requirements Engine implemented its own approved contract exactly, with
the sole disclosed narrowing (Requirement Allocation's Guid-only
targets) traced to a decision made *during* the contract-review stage
itself, not an implementation-phase surprise.

## 4. Implementation Lessons

**A revision-numbering assumption recurred as a genuine, if minor,
source of friction across multiple Work Packages this release.**
`IEngineeringDocumentStore.CreateAsync` begins a document's own revision
history at 1, not 0 — an assumption several Work Packages' own tests
initially got wrong before correcting against the real source. This is
now disclosed explicitly enough (`WP 7.3A`'s own Lessons Learned) that a
future consumer of the Engineering Data Model should check the source
before writing an assertion, not assume either convention.

**A "deciding test" articulated once, in one ADR, is reusable
infrastructure for a future Work Package's own judgment calls.**
`ADR-0061`'s own explicit test for internal-vs-calling-layer permission
gating (evidentiary/audit-adjacent data gates internally; ordinary
operational content leaves it to the caller) is exactly the kind of
reusable engineering judgment this release's own governance discipline
is designed to surface and preserve, rather than requiring each future
Work Package to re-derive the same distinction from scratch.

**A contract-review-stage narrowing, uncaught for two Work Packages, is
a genuine process finding, not a one-off.** `WP7.2B`'s own broader
Requirement Allocation vision (open-string targets) was quietly narrowed
to Guid-only during `WP7.2C`'s own contract finalisation, and neither
that Work Package's own review nor `WP 7.3A`'s own early implementation
planning caught the gap until the actual `LinkAsync` signature was being
written. `WP 7.3A`'s own recommendation — that a contract review stage
should explicitly cross-check every architectural capability named in
the prior stage against the contract being finalised — is carried
forward here as a standing recommendation for any future two-stage
design process this project uses again.

## 5. Repository Maturity

**The recurring governance-drift pattern identified at `v0.5.0`
(`WP 5.4`) and confirmed at `v0.6.0` (`WP 6.8`) has now recurred a third,
fourth, and fifth time within this single release alone.**
`Interface Register.md`/`Dependency Injection Register.md`/`Module
Register.md` went stale across all five Engineering Foundation Work
Packages, undetected until `WP 7.1F`'s own closing certification review
(third recurrence). `Platform Services Register.md`/`Platform Service
Map.md` were found, during `WP 7.3A`'s own repository review, to have
never gained rows for any of the four Engineering Foundation frameworks
at all — a gap `WP 7.1F`'s own certification review did not check
(fourth recurrence). `Documentation Register.md` (stale directory-map
counts: `docs/adr/` reading 39 against an actual 61; `03 Work Packages/`
reading 32 against an actual 57) and `Governance Register.md` (its own
Compliance Matrix missing all twelve `v0.7.0` Work Packages) were both
found and fully closed by this Work Package, `WP 7.4.0` (fifth
recurrence). **This is no longer a rare failure mode — it is the
default outcome absent a dedicated, periodic, wider-than-any-single-
Work-Package review**, exactly the conclusion `WP 5.4` first reached and
every subsequent release-closing review has independently reconfirmed.

**Every count this Work Package independently re-derived from the
repository directly matched the register that claimed it, once the five
staleness findings above were corrected.** ADRs (61), Rejected Designs
(45), Technical Debt Register items (25), Future Capability Register
entries (38), Academy articles (104), public interfaces (80), DI
registrations (31 named, 33 raw), production modules (20) — all
independently verified via direct `grep`/`find` against source, not
assumed from a register's own prior claim. Test suite stability was
re-confirmed across four full clean-rebuild runs (two Debug, two
Release) — zero flakes, zero regressions, matching every prior
release's own closing-review standard.

**`FCR-0005` (Governance Register Health-Check Tooling) remains
Identified, not built, for the fifth consecutive release-adjacent
review to recommend it.** This release's own experience is the
strongest argument yet that a manual, periodic sweep — however
thorough — will keep finding the identical class of drift; the tooling
itself, not another manual pass, is what would actually close the loop.

## 6. Recommendations for What Comes Next

1. **Build `FCR-0005` (Governance Register Health-Check Tooling)
   before, or alongside, whatever Work Package comes next** — five
   independent recurrences across three releases is no longer a
   pattern worth re-discovering a sixth time manually.
2. **Backfill `Platform Services Register.md`/`Platform Service Map.md`
   for the four Engineering Foundation frameworks** (Engineering Data
   Model, Materials, Calculations, Verification) — disclosed by
   `WP 7.3A`, confirmed still open by this Work Package's own audit,
   deliberately not fixed here since it falls outside release
   preparation's own scope (a documentation backfill of this size is
   implementation-adjacent work, not verification).
3. **Decide Programme F (Platform Hardening) vs. a further Systems
   Engineering capability vs. the first discipline-specific engineering
   module** as the next programme — all three remain open, unscheduled
   candidates per `docs/governance/Future Capability Register.md`; none
   is assumed here.
4. **Apply `WP 7.3A`'s own contract-review cross-check recommendation**
   to any future architecture-then-contract two-stage design process —
   explicitly verify every capability the architecture stage named is
   either carried into the contract or explicitly, visibly deferred,
   rather than silently narrowed.
5. **Continue the three-dedicated-Security-Review standard**
   (`WP 7.1D`, `WP 7.1E`, `WP 7.3A`) for every future implementation Work
   Package, not only ones judged unusually security-sensitive in
   advance — every one of the three caught findings the architecture-
   stage review alone had not.

## Key Takeaways

1. Two full architecture-then-contract-then-implementation cycles,
   completed with zero rework, is stronger evidence that this project's
   own architecture-first discipline works than any single cycle could
   provide alone.
2. A pattern independently rediscovered by four different frameworks
   (Materials, Calculations, Verification, Requirements) without ever
   citing each other's decision is no longer a hypothesis — it is this
   platform's own default answer for "how does a new engineering concept
   get built."
3. A release-closing review's own distinct value, confirmed a third
   time this release, is re-deriving every claim directly from the
   repository — not re-reading what each Work Package's own retrospective
   already said about itself. The five governance-drift findings this
   review closed were each invisible to every narrower, single-Work-
   Package review that came before it.
4. Recommending the same tooling fix (`FCR-0005`) after a third, fourth,
   and now fifth recurrence without building it is itself a finding:
   manual discipline alone cannot close a gap that only automation
   reliably prevents.

## Related Documents

`docs/releases/v0.7.0/ReleaseNotes.md`; `docs/releases/v0.7.0/WP7.4.0
Release Readiness Report.md`; `docs/releases/v0.7.0/WP7.4.0 Product
Approval Report.md`; `docs/releases/v0.7.0/WP7.4.0 Engineering
Statistics Report.md`; `docs/releases/v0.7.0/WP7.4.0 Architecture
Baseline Summary.md`; `docs/academy/03 Work Packages/
WP5.4-v0.5.0-release-candidate-and-engineering-sign-off.md`;
`docs/academy/03 Work Packages/
WP6.8-platform-services-integration-review.md`;
`docs/governance/Future Capability Register.md` (`FCR-0005`).
