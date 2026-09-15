# Welcome to the TempestOS Academy

## What This Is

The TempestOS Academy is not API documentation. The source code, its XML
documentation, and the test suites already explain *how* TempestOS works — what
each class does, what each method's contract is, what each test proves. The
Academy exists to explain something the source code structurally cannot: *why*
it works this way instead of one of the other ways it could have.

Every non-trivial design decision in TempestOS has a story: a problem that needed
solving, at least one alternative that was seriously considered and rejected, a
piece of reasoning that connected the problem to the chosen solution, and — very
often — a cost that was accepted knowingly, in exchange for a benefit judged to
matter more. The Academy's job is to preserve that story before it lives only in
the heads of whoever was in the room when the decision was made, and is lost the
moment they move to a different project.

## Read This First: Engineering Governance

Before anything else in this Academy, read
[`06 Engineering Standards/Engineering Governance.md`](../06%20Engineering%20Standards/Engineering%20Governance.md).
It is the project's constitution: how a work package moves from brief to
merge, what "Done" actually requires, when an ADR is mandatory, who has
authority to decide what, and who has authority to approve a release. Every
other document in this Academy describes *a* decision or *a* principle;
Engineering Governance describes the *process* that produced all of them.
Since `WP 17.0B` (2026-09-08) the day-to-day process is the shorter one in
`CONTRIBUTING.md` at the repository root — a Work Package is a branch and a
pull request, the pull request description is the retrospective, and a
pull request may not add more Markdown lines than code lines. Read
Engineering Governance for the principles and for how everything up to
`v0.16.0` was produced; read `CONTRIBUTING.md` for what is required today.
Where the two differ, `CONTRIBUTING.md` governs.

## Who This Is For

Two readers, deliberately.

**The Product Owner, who is not a software engineer.** The Academy is the
place to learn what building this product actually involved, stage by
stage — what was decided, why, what it cost, what went wrong and how it
was found — without reading code. Start with
[`01-how-tempestos-gets-built.md`](01-how-tempestos-gets-built.md), which
walks the stages of software work using this repository's own artefacts,
and keep [`02-plain-language-glossary.md`](02-plain-language-glossary.md)
open beside every chapter. Every chapter from `42` onward in
`02 Runtime Architecture` opens with an **In plain terms** paragraph
written for this reader and closes with **What to take away**.

**Anyone joining TempestOS as an engineer, at any level of seniority.** A
graduate engineer reading these documents should come away understanding
not just what a Registry pattern or a state machine is, in the abstract,
but specifically why TempestOS's runtime module pipeline uses both, and
what would go wrong if it didn't. A senior architect reviewing TempestOS
for the first time should be able to find, for any design choice that
looks unusual on first read, a document explaining exactly what
alternatives were weighed and why this one won.

## How the Academy Is Organised

- **00 Introduction** — this section. Orientation, not architecture:
  this page, the stage-by-stage guide for the non-engineer reader
  (`01-how-tempestos-gets-built.md`), and the plain-language glossary
  (`02-plain-language-glossary.md`).
- **02 Runtime Architecture** — seventy-six chapters, in the order the
  platform and then the product were built: how the runtime is put
  together (chapters `01`–`12`), the engineering foundation and workspace
  (`13`–`18`), the desktop application (`19`–`32`), the Product
  Convergence programme that made it a real application (`33`–`42`), the
  remediation and hygiene releases `v0.14.0`–`v0.16.0` (`43`–`49`), the
  `v1.0.0` programme's first two releases, `v0.17.0` Reset and
  Substrates and `v0.18.0` Evidence and Check (`50`–`64`), and the three
  release candidates that followed in the week of 2026-09-10 — `v0.19.0`
  Consultancy Seam and Desktop (`65`–`69`), `v0.19.1` the Product Owner's
  first pass (`70`–`73`) and `v0.20.0` the debt tranche (`74`–`76`, which
  also covers the `v0.21.0` plan). The folder's name is historical; from
  chapter `13` onward the subject is the product as much as the runtime.
  See `Academy Index.md` for the list with one line per chapter.
- **04 Design Patterns** — recurring structural patterns TempestOS actually
  uses (not a generic patterns catalogue), explained in terms of the real
  code that uses them.
- **05 Case Studies** — narrative deep-dives into specific, individually
  significant decisions, including at least one preserved, real
  architectural review exchange — the original problem, the alternatives,
  the reasoning, the decision, and the outcome. Shorter and more focused
  than a chapter; longer and more narrative than an ADR. Two were added
  in September 2026: the attachment sweep that could delete live content,
  and the design-freeze review.
- **06 Engineering Standards** — the conventions TempestOS holds itself to
  consistently across every work package: exception design, testing
  strategy, continuous integration, release engineering, and — added in
  September 2026 — test determinism, the physical review and release
  gate, and the governance reset that changed how a release is now run.

Two sections that used to live here were archived by `WP 17.0B`
(2026-09-08) and are still worth reading:

- **01 Engineering Principles** — general software engineering principles
  (SOLID, Immutability, Dependency Injection, Deterministic Systems, State
  Machines, the Atomic Phase Principle, and others), each explained on its
  own terms first, then connected to how TempestOS applies it. Now at
  `archive/docs-2026-09/academy/01 Engineering Principles/`. Read these if
  you want the *vocabulary* the older chapters use.
- **03 Work Packages** — a thirteen-section retrospective for each Work
  Package from `WP 2.1` (`v0.3.0`) to `WP 16.5B` (`v0.16.0`), about 240
  documents. Now at `archive/docs-2026-09/academy/03 Work Packages/`.
  They remain the deepest record of *why* for everything up to `v0.16.0`;
  the chapters in `02 Runtime Architecture` cite them where they draw on
  them.

**A note on paths.** A chapter written before 2026-09-08 may cite a
`docs/architecture/…` or `docs/governance/…` document that no longer
exists at that path. Every such document was moved, not deleted: look for
it under `archive/docs-2026-09/` at the same relative path
(`archive/docs-2026-09/README.md` explains the move). The chapters were
left as written rather than edited, because they describe the repository
as it was when their subject was built.

Alongside the Academy, `docs/adr/` holds Architecture Decision Records — short,
formal, consistently-templated records of specific decisions (Status, Context,
Decision, Consequences, Future Considerations). If the Academy is the textbook,
the ADRs are the index of individually citable rulings. Case studies and ADRs
frequently cover the same decision from different angles deliberately: the ADR is
the terse, quotable record; the case study is the fuller story behind it.

## How to Use This When You're Changing Something

Before modifying an existing runtime component, read its work package
retrospective. If your change touches a decision documented in an ADR, read that
ADR's "Future Considerations" section — it may already anticipate exactly the
change you're making, or explain exactly why an alternative you're tempted by was
already considered and rejected. If you make a new, non-trivial architectural
decision, add a new ADR and update the relevant work package documentation and
retrospective — this is not optional academic record-keeping; it is how the next
person avoids re-deriving reasoning that already exists, or worse, silently
undoing a decision that was made for a reason they can't see from the code alone.

## Where This History Begins

The Academy's own retrospectives begin at WP 2.1 (Framework Discovery), the
first work package built to the engineering discipline this document
describes. Before it, the repository already contained a small, pre-existing
`Tempest.Core`/`Tempest.App` bootstrap (configuration, hosting, logging, and
project-management services predating any notion of "modules") and a
now-retired Python prototype, archived in favour of the C# implementation as
part of a repository stabilisation pass. That stabilisation — cleaning up
the repository structure, wiring the test project into the solution, adding
standard `.gitignore`/`global.json`/`Directory.Build.props` conventions — is
real, completed work, but it is housekeeping, not an architectural work
package: it made no design decision meeting Engineering Governance §5's ADR
bar, and introduced no runtime capability of its own for an Academy
retrospective to document. It is recorded here, honestly, rather than
silently treated as if the Academy's discipline had always been in place —
consistent with the Note on Honesty below.

## Where the Academy's Own Record Changed

From `v0.3.0` to `v0.16.0`, every Work Package wrote its own Academy
retrospective as part of its Definition of Done. `WP 17.0B` (2026-09-08,
the governance reset) ended that: the pull request description became the
retrospective, the retrospective suite was archived, and no per-Work-Package
Academy writing was scoped for `v0.17.0` or `v0.18.0`. The concept-level
chapters in `02 Runtime Architecture` had already stopped at chapter `41`
(the end of August 2026, the middle of `v0.14.0`).

Chapters `42`–`64`, the two new case studies, the three new standards
chapters and the two new introduction documents were written in September
2026, after `v0.18.0` shipped, to close that gap. They were written from
the commits, the ADRs, the release notes, the archived retrospectives where
they exist, and the code at the `v0.18.0` head — not from memory. Where a
figure or a name could not be verified against one of those sources it was
left out rather than guessed; where a Work Package's own first attempt was
wrong, that is in the chapter. `06 Engineering Standards/09-the-governance-reset-and-how-a-release-is-now-run.md`
explains the reset itself and what it means for how this Academy is
maintained from here on.

Chapters `65`–`76`, case study `08` and standards chapters `10`–`11` were
added on 2026-09-15, the same week the work they describe was done. They
describe **release candidates, not releases**: `release/v0.19.0`,
`release/v0.19.1` and `release/v0.20.0` were cut between 2026-09-10 and
2026-09-15 and were still under the Product Owner's manual test when the
chapters were written, and `release/v0.21.0` held a plan and no code.
Each chapter's header names the branch and the commit it was read at.
When a candidate is tagged, or is superseded, the honest correction is a
line in that chapter's header, not a rewrite; the chapters already record
that the `v0.19.0` candidate was superseded by `v0.19.1` before it was
tagged, and that `v0.20.0` was cut from `v0.19.1` to close technical debt
before release rather than after.

## A Note on Honesty

Several documents in this Academy describe mistakes, not just successes — an
immutability decision that required a second, structurally duplicated type; an
asymmetric state-machine rule that needed defending under direct challenge; a
real bug, introduced by one work package and only surfaced by the next, that had
to be found and fixed rather than merely documented. This is deliberate. An
Academy that only records the decisions that turned out well, and omits the ones
that had to be corrected or defended, teaches confidence without teaching
judgment. TempestOS's Academy aims for the latter.
