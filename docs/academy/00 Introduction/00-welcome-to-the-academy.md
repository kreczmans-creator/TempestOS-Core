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
Engineering Governance describes the *process* that produced all of them, and
that every future one is expected to follow.

## Who This Is For

Anyone joining TempestOS, at any level of seniority. A graduate engineer reading
these documents should come away understanding not just what a Registry pattern
or a state machine is, in the abstract, but specifically why TempestOS's runtime
module pipeline uses both, and what would go wrong if it didn't. A senior
architect reviewing TempestOS for the first time should be able to find, for any
design choice that looks unusual on first read, a document explaining exactly
what alternatives were weighed and why this one won.

## How the Academy Is Organised

- **00 Introduction** — this section. Orientation, not architecture.
- **01 Engineering Principles** — general software engineering principles (SOLID,
  Immutability, Dependency Injection, and others), each explained on its own
  terms first, then connected explicitly to how TempestOS actually applies it.
  Read these if you want to understand the *vocabulary* the rest of the Academy
  uses.
- **02 Runtime Architecture** — how TempestOS's runtime is actually put together,
  holistically: the whole module pipeline, how its stages relate, and why the
  boundaries between them sit where they do.
- **03 Work Packages** — a detailed retrospective for each major piece of work
  (currently WP 2.1 through WP 2.5), following a consistent thirteen-section
  template: introduction, purpose, background, the problem, the design,
  alternatives considered, why this solution was chosen, architectural
  principles, benefits, trade-offs, common mistakes, future evolution, and key
  takeaways. These are the deepest, most detailed documents in the Academy —
  read the work package retrospective for whatever you're about to change, before
  you change it.
- **04 Design Patterns** — recurring structural patterns TempestOS actually uses
  (not a generic patterns catalogue), explained in terms of the real code that
  uses them.
- **05 Case Studies** — narrative deep-dives into specific, individually
  significant decisions, including at least one preserved, real architectural
  review exchange — the original problem, the alternatives, the reasoning, the
  decision, and the outcome. Shorter and more focused than a work package
  retrospective; longer and more narrative than an ADR.
- **06 Engineering Standards** — the conventions TempestOS holds itself to
  consistently across every work package: exception design, logging, testing
  strategy, documentation style.

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

## A Note on Honesty

Several documents in this Academy describe mistakes, not just successes — an
immutability decision that required a second, structurally duplicated type; an
asymmetric state-machine rule that needed defending under direct challenge; a
real bug, introduced by one work package and only surfaced by the next, that had
to be found and fixed rather than merely documented. This is deliberate. An
Academy that only records the decisions that turned out well, and omits the ones
that had to be corrected or defended, teaches confidence without teaching
judgment. TempestOS's Academy aims for the latter.
