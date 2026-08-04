# v0.8.0 Retrospective — "Engineering Workspace"

## What This Document Is

Like `WP 5.4`'s, `WP 6.8`'s, and `WP 7.4.0`'s own prior release
retrospectives, this document does not design or implement a platform
capability — it verifies, closes, and prepares an entire release for
Product Approval. Shaped around the same five questions that kind of
Work Package actually raises, not forced into the standard 13-section
feature template. Written by `WP 8.9.0` (Release Preparation & Product
Baseline), the release's own closing Work Package.

## 1. Introduction

`v0.8.0` ("Engineering Workspace") is the fifth release TempestOS has
shipped, and the first to give the platform a genuine user-facing
product surface — somewhere a person actually looks when they open
TempestOS — alongside a second, equally significant achievement: the
Engineering Domain, the platform's own first compiled, running,
shared vocabulary for what an engineering object *is*, independent of
any one discipline. Where `v0.7.0` proved the platform could understand
requirements, materials, calculations, and verified claims, `v0.8.0`
proves it can (a) show a user their own engineering work, and (b)
generalise "what an engineering object is" into one reusable shape
every future discipline inherits rather than reinvents. Nine Work
Packages across two independent tracks, zero architectural rework, zero
Release Blocking findings.

## 2. What Was Achieved

**Track one — Engineering Workspace (`WP 8.0A`–`WP 8.1C`):** a complete
architecture across twelve named areas (`WP 8.0A`), complete public
contracts for all twelve Workspace interfaces (`WP 8.0B`), a real,
compiled shell now `Tempest.App`'s own default launch target
(`WP 8.1A`, `ADR-0068`), a complete target UX specification across 28
scope areas written *after* the shell already existed — a genuine,
disclosed sequencing departure from this project's usual order,
handled transparently rather than silently absorbed (`WP 8.0C`), a real
Navigation system and Project Explorer (`WP 8.1B`), and the Engineering
Cockpit — the Workspace's own default landing screen, answering four
questions on every visit (`WP 8.1C`).

**Track two — Engineering Domain (`WP 8.2A`–`WP 8.2C`):** a complete
canonical Engineering Object catalogue — ~49 objects across 13 families,
20 relationship kinds, a canonical eight-state lifecycle vocabulary —
grounded in, and reconciled against, four already-shipped Engineering
Core frameworks that had independently converged on the same shape
without coordination (`WP 8.2A`); complete public contracts, 83
interface/enum/record types, composed from ten facet interfaces rather
than one monolith (`WP 8.2B`); and a real, compiled, tested
implementation — 38 concrete object classes over one shared
`EngineeringObjectBase`, a new in-memory repository layer, two generic
factory types, and a sixteen-object representative sample graph
(`WP 8.2C`).

225 new tests (1406 → 1631). 18 new ADRs (61 → 79), all Accepted, zero
gaps. 12 new Academy articles (104 → 116). 83 new public interfaces (80
→ 163) — the largest single-release interface addition this project has
recorded. 2 new production modules (20 → 22). Zero breaking changes to
any prior release's own contract.

## 3. Architectural Lessons

**A genuine contract-vs-prior-decision tension recurred at a second
stage of the same three-Work-Package sequence, and was resolved the
same way both times.** `ADR-0076` (contract stage, `WP 8.2B`) and
`ADR-0077` (implementation stage, `WP 8.2C`) each faced the identical
shape of conflict — a literal reading of the controlling brief
appearing to require something a prior, binding decision (`ADR-0073`,
then `ADR-0072`) already forbade — and each resolved it the same way:
distinguishing what the instruction actually needed from what its most
literal reading would produce. This is no longer a one-off; it is a
repeatable pattern for this project's own two-and-three-stage design
processes.

**A shared concrete base class for implementation reuse is not in
tension with "composition over inheritance"**, provided the two are
kept conceptually separate from the start. `WP 8.2C`'s own
`EngineeringObjectBase` implements every facet interface
unconditionally — ordinary implementation reuse — while `ADR-0075`'s
own composition rule continues to govern only the *interfaces* those
classes implement. Naming this distinction explicitly, in both the ADR
and the Academy concept guide, prevented what could otherwise have
looked like a contradiction of a decision made one Work Package
earlier.

**Presentation-layer and shared-vocabulary-layer work each independently
rediscovered the "reuse what already exists" pattern this platform's own
Engineering Core already proved six times.** The Workspace introduces
zero new Platform Service (`ADR-0062`); the Engineering Domain
introduces zero new persistence mechanism (`ADR-0077`). Neither
decision was copied from precedent — each was independently justified
on its own merits — and both landed on the identical conclusion the six
Engineering Foundation/Systems Engineering frameworks already reached.

## 4. Implementation Lessons

**A contract can name a type it never defines, and only implementing
the contract catches it.** `WP8.2B Interface Catalogue.md` referenced
`IRevisionRecord` from `IHasRevisions.GetRevisionHistoryAsync` but never
itself defined the interface — invisible at the contract-review stage,
unavoidable the moment `WP 8.2C` tried to compile against it. Closed by
defining it, mirroring the already-shipped `IDocumentRevision`'s own
shape, and disclosed as a genuine, implementation-time contract gap
rather than silently patched.

**A count claimed in a Work Package's own closing documentation is not
guaranteed correct simply because the Work Package itself succeeded.**
`WP 8.2C` claimed 39 concrete canonical object classes; this release's
own closing review (`WP 8.9.0`) found 38 by direct `grep` against the
compiled source — a simple arithmetic slip in summary prose, with zero
effect on the actual, correct, tested implementation. The lesson is
procedural, not architectural: a release-closing review's own value
includes re-deriving even a just-completed Work Package's own headline
numbers directly, not only carrying forward its own prior claim.

**Sequencing two independent tracks in parallel, rather than serially,
worked cleanly here — but left a real integration gap disclosed, not
hidden.** The Engineering Workspace and the Engineering Domain shipped
in the same release without ever being wired to each other — the
Cockpit renders no Engineering Domain object, and the Project Explorer's
own content remains entirely fictional. Neither track's own Work
Packages silently pretended otherwise; both name the gap explicitly as
deferred work, not completed integration.

## 5. Repository Maturity

**The four-Engineering-Foundation-framework Platform Service Map/
Register gap, first found by `WP 7.3A` and confirmed still open by
`WP 7.4.0`, is now confirmed open a second consecutive release-closing
review.** This is a genuinely different pattern from the
`Interface`/`DI`/`Module` Register drift `WP 7.1F` and `WP 6.8` each
found and *closed* — this specific gap has now survived two entire
release cycles' worth of dedicated closing review, each of which
correctly declined to fix it as out of scope, but neither of which
escalated it beyond "recommended, not required." **This is the
strongest evidence yet that a documentation gap explicitly deferred
twice in a row needs either a dedicated Work Package of its own, or a
firm decision that it will never be worth fixing** — recommended
explicitly, below, rather than deferred a third time without comment.

**Every count this Work Package independently re-derived from the
repository directly matched the register that claimed it, with one
disclosed exception.** ADRs (79), Rejected Designs (45), Technical Debt
Register items (25), Future Capability Register entries (38), Academy
articles (116), public interfaces (163), DI registrations (41 named, 43
raw), production modules (22) — all independently verified via direct
`grep`/`find` against source. The one exception — `WP 8.2C`'s own
39-vs-38 concrete class count — is disclosed in full above and in
`WP8.9.0 Release Readiness Report.md`. Test suite stability was
re-confirmed across five full-suite-equivalent runs (two Debug, two
Release, plus one via the actual release script's own solution-file
path), zero flakes, zero regressions, matching every prior release's
own closing-review standard.

**`FCR-0005` (Governance Register Health-Check Tooling) remains
Identified, not built, for a sixth consecutive release-adjacent
review to recommend it.** Combined with the Platform Service gap's own
new escalation (above), this release's own experience makes the
strongest case yet that a manual, periodic sweep — however thorough —
will keep finding the identical classes of drift indefinitely; the
tooling itself, not another manual pass, is what would actually close
the loop.

**A dedicated Security Review was skipped for an entire release cycle
for the first time since the practice began.** `v0.7.0` performed three;
`v0.8.0` performed zero. Disclosed prominently, weighed explicitly in
the Product Approval Report rather than silently normalised, and named
as this release's own single most important carry-forward
recommendation.

## 6. Recommendations for What Comes Next

1. **Build `FCR-0005` (Governance Register Health-Check Tooling) before,
   or alongside, whatever Work Package comes next** — six independent
   recurrences across four releases is no longer a pattern worth
   re-discovering a seventh time manually.
2. **Make a firm decision about the four-Engineering-Foundation-framework
   Platform Service Map/Register gap** — either schedule a dedicated
   backfill Work Package, or formally accept the gap as permanent and
   stop re-disclosing it as "recommended" every release without
   consequence. Two consecutive closing reviews finding the identical,
   unfixed gap is itself now a process finding.
3. **Perform a dedicated Security Review as part of, or immediately
   before, the first implementation Work Package of Programme 9** —
   closing this release's own one genuine process gap rather than
   carrying it forward a second time.
4. **Build a real Physical/Configuration Engineering Discipline Module**
   against `WP 8.2C`'s own compiled classes — the first proof that the
   Engineering Domain's own "consumed by every future discipline"
   Definition of Done actually holds under a real consumer, not only a
   representative sample module.
5. **Decide Programme F (Platform Hardening) vs. a further Systems
   Engineering capability vs. the first discipline-specific engineering
   module** as the next programme — all three remain open, unscheduled
   candidates.

## Key Takeaways

1. Two independent tracks, shipped in the same release without being
   integrated with each other, is a legitimate and disclosed choice —
   provided the gap it leaves is named as deferred work, not hidden as
   if integration had happened.
2. The identical contract-vs-prior-decision tension, resolved the same
   way at two different design stages (`ADR-0076`, `ADR-0077`), is
   strong evidence the resolution technique itself — distinguish what
   is needed from what a literal reading produces — is a reusable skill
   for this project, not a one-off insight.
3. A release-closing review's own distinct value, now confirmed a
   fourth time, is re-deriving every claim directly from the repository
   — including a Work Package's own headline numbers from the release
   currently closing, not only older ones.
4. A documentation gap disclosed and deferred twice in a row, across two
   entire release cycles, has stopped being "recommended for a future
   Work Package" and started being a standing process question this
   project has not yet decided how to answer.

## Related Documents

`docs/releases/v0.8.0/ReleaseNotes.md`; `docs/releases/v0.8.0/WP8.9.0
Release Readiness Report.md`; `docs/releases/v0.8.0/WP8.9.0 Product
Approval Report.md`; `docs/releases/v0.8.0/WP8.9.0 Engineering
Statistics Report.md`; `docs/releases/v0.8.0/WP8.9.0 Architecture
Baseline Summary.md`; `docs/academy/03 Work Packages/
WP7.4.0-release-preparation-and-product-baseline.md`;
`docs/governance/Future Capability Register.md` (`FCR-0005`);
`docs/governance/Engineering/Platform Services Register.md`.
