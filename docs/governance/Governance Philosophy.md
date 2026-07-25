# TempestOS Governance Philosophy

## What This Document Is

`Governance Index.md` tells you where every register lives.
`Governance Audit Report.md` tells you what was found when this suite was
built. This document tells you *why any of it exists at all* — the
engineering philosophy behind maintaining registers, not a description of
their contents. If a future contributor is tempted to skip updating a
register "just this once," or to fill an Unknown value with a plausible
guess rather than leave it honestly blank, this is the document that
explains why that temptation should be resisted, and what it actually
costs when it isn't.

## Why TempestOS Uses Governance Registers

TempestOS already had, before this document existed, an unusually
disciplined documentation culture: 30 ADRs, 29 Rejected Designs entries,
62 Academy articles, and a Work Package retrospective for nearly every
piece of work — see `Engineering Evolution Register.md` for when each
discipline was adopted. What it did not have, until `WP 4.5A`, was a
single place that answered *aggregate* questions: how many ADRs exist and
are any of them stale? Which platform services exist and which are only
contracts? Does every completed Work Package have a retrospective, or did
one slip through? Each individual document already answered its own
narrow question well; nothing answered the questions that span all of
them at once.

A registry answers exactly one kind of question a narrative document
cannot answer efficiently: *enumeration and cross-reference*. "Read
`Platform Service Map.md`" answers "what does the Event Bus do." "Read
the Platform Services Register" answers "which of TempestOS's fifteen
platform services are actually implemented, and does every one of them
have a test, an ADR, and an Academy article — or does something exist in
isolation, undocumented, untested, or forgotten." The second kind of
question requires structured, comparable data across many subjects at
once; prose, however well-written, resists exactly that comparison.

This is not a claim that registers are more valuable than the narrative
documents they index — the opposite is explicit throughout this suite
(see "Avoid Duplication," below). A register that tried to replace
`Platform Service Map.md`'s reasoning, or `Rejected Designs.md`'s "why we
said no," would be strictly worse than either the register or the
original alone: a shallow copy competing with the deep original for a
reader's trust. The register's job is to make the *existence and
cross-reference* of that reasoning checkable at a glance, never to
restate the reasoning itself.

## Why Traceability Matters

`Traceability Matrix.md` is this suite's capstone specifically because it
answers the question every other register can only partially answer on
its own: for one real capability — the Event Bus, say — can you actually
walk, without guessing, from "why does this exist" through "what decided
its shape" through "where is that written down" through "what code
implements it" through "what proves it works" through "what teaches a
future contributor about it" through "what release does it belong to"?

If any link in that chain is missing, one of several real failures has
occurred: a capability was built without ever being decided (an
undocumented architectural choice, silently load-bearing); a capability
was decided but never built (a stale promise); a capability was built but
never tested (unverified correctness); or a capability was built and
tested but never explained (a future contributor re-deriving reasoning
that already exists, wastefully). Traceability is the mechanism that
makes each of these failures *visible* rather than merely *possible* —
not because tracing itself prevents the failure, but because a missing
link is now something a reviewer can actually see and ask about, instead
of a gap nobody had a reason to notice.

## Why Unknown Is Preferable to Invented Data

This is the single most important discipline this suite enforces, and the
one most tempting to quietly violate. A register with every field filled
in *looks* more complete, more authoritative, more finished than one with
several honest "Unknown" entries. That appearance is exactly the danger.

**A wrong answer that looks confident is worse than a missing answer that
looks incomplete**, because a missing answer invites exactly one correct
next action — go find out, or accept that it may not be knowable — while
a wrong answer invites zero further scrutiny and gets built upon by
everyone who reads it afterward, compounding the original error silently.
This project's own history already demonstrates the cost of the opposite
discipline done well: `WP 4.4F`'s Academy audit found and fixed six
genuine staleness issues specifically *because* it treated "I'm not sure
this is still accurate" as a finding worth recording, not a gap to paper
over with a plausible-sounding sentence.

Every register in this suite marks its own entries **Verified**
(established by directly reading a repository artifact — a commit, a
file, a test result), **Inferred** (a reasonable conclusion the available
evidence supports but does not directly state — always labelled as such,
never presented as Verified), or **Unknown** (evidence to establish this
does not exist, or was out of this suite's own scope to reconstruct,
recorded honestly). `docs/releases/v0.2.0/`'s empty directory, the
five-day gap in early git history, and the exact original authorship of
`Tempest.Core.Hosting`/`Bootstrap`/`Projects`/`Repositories` are all
recorded as Unknown in this suite, not guessed at — because a future
reader who needs to actually know one of these things deserves an honest
"nobody currently knows this, here is exactly what evidence exists and
what's missing," not a confident-sounding fabrication that sends them
down a wrong path.

## How Contributors Maintain Governance

A register is not a one-time deliverable — it decays the moment the
system it describes changes and the register does not change with it,
exactly as `Platform Service Map.md` and the Academy already warned about
themselves (Engineering Governance §6). Concretely:

- **Update the register as part of the Work Package that changes its
  subject matter**, not as a separate, later pass. A Work Package that
  adds a platform service updates the Platform Services Register and the
  Traceability Matrix in the same commit that adds the service — the same
  discipline already required of `Platform Service Map.md` itself,
  extended to this suite.
- **Prefer marking a register Partial or Not Yet Applicable, with a
  Reason and Review Trigger, over inventing entries to make it look
  Complete.** A register that honestly says "this doesn't apply yet,
  here's what would trigger it" is more useful, not less, than one padded
  with speculative rows.
- **Cross-check before publishing.** Every register in this suite states
  its own Cross-Reference Check — what it was compared against, and that
  no discrepancy was found. A future update should re-run that same
  check, not merely append a new row and assume the rest still agrees.

## Review Expectations

Each register states its own Review Frequency and Last Reviewed date —
there is no single, fixed cadence across the whole suite, because
different registers change at genuinely different rates (the ADR
Register changes once per Work Package that meets §5's criteria; the
Repository Metrics Register is a point-in-time snapshot, explicitly not
claiming to track a trend). A reviewer should trust a register's own
"Last Reviewed" date at face value, and treat a register whose subject
matter has visibly changed since that date as due for review, regardless
of how much time has passed in absolute terms.

## Ownership Principles

Every register in this suite currently names the same Owner: the Project
Maintainer — the sole contributor of record across every commit in this
repository's history (see `Repository Metrics Register.md`). This is
recorded honestly, not aspirationally: there is no separate architecture
review board, no distinct governance team, no rotating ownership scheme.
Should TempestOS gain additional contributors, ownership should be
revisited explicitly, register by register, rather than left to default
silently to whoever happens to touch a file next — an unowned register is
exactly the kind of "worse than no register at all" outcome this suite
exists to prevent (Engineering Governance §6's own reasoning, applied
here).

## Relationship Between FOUNDATION.md, Academy, Architecture, Governance, and Testing

These five bodies of documentation answer five genuinely different
questions, and this suite's own existence depends on that distinction
staying clear:

- **`FOUNDATION.md`** answers *what must never change* — the permanent,
  cross-release constitution. It is the one document in this list that
  does not get superseded by the next release.
- **The Academy** (`docs/academy/`) answers *why TempestOS is built this
  way, taught for a future contributor's benefit* — principles, patterns,
  case studies, and retrospectives, written to teach, not merely to
  record.
- **Architecture** (`docs/architecture/`, `docs/adr/`) answers *what the
  system currently does, and what alternatives were considered and
  rejected* — the authoritative technical reference.
- **This Governance suite** answers *is everything above actually
  complete, cross-referenced, and current* — the aggregate,
  cross-cutting view that makes the other four bodies' own completeness
  checkable, without replacing any of their content.
- **Testing** (`docs/releases/v0.4.0/Testing.md`, the test suite itself)
  answers *does the system actually behave as Architecture claims it
  does* — the empirical check underneath everything else.

Governance sits *above* the other four, in the sense that it indexes and
cross-checks them, but it is not superior to them in authority — where
this suite's own register disagrees with the document it indexes (an
ADR's own text, a retrospective's own claim), **the original document
wins**; the register is expected to be corrected to match it, never the
reverse. This suite exists to serve the other four, not to govern them in
the sense of overriding them.

## A Closing Note on Cost

Maintaining this suite is not free — every register added is another
thing a future Work Package must remember to update. This cost is
accepted deliberately, on the same reasoning the Academy's own maintenance
obligation already accepted it: a suite that goes stale and is still
trusted is worse than no suite at all, but a suite that is genuinely
maintained turns "is this project's governance mature" from a question
requiring a fresh audit every time into a question this suite itself can
usually answer, immediately, correctly, and honestly — including, when
appropriate, by saying it does not yet know.

## Related Documents

`Governance Index.md`; `Governance Audit Report.md`;
`Repository Maturity Report.md`; `docs/releases/FOUNDATION.md`;
`docs/academy/06 Engineering Standards/Engineering Governance.md`.
