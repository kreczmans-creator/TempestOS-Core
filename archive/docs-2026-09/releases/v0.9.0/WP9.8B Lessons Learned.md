# WP 9.8B — Platform Service Register Reconciliation — Lessons Learned

## Purpose

Records what went well, what was harder than expected, and what a
future Work Package facing a similar governance-reconciliation task
should know going in.

## What Went Well

**A disclosed gap named across three release-closing reviews turned out
to be smaller, once actually reconciled, than its own repeated
disclosure implied.** Each of `WP 7.4.0`/`WP 8.9.0`/`WP 9.9.0` described
"the four-Engineering-Foundation-framework Platform Service gap" without
qualification, which could read as all governance coverage for these
four frameworks being absent. Direct, document-by-document verification
found the gap was real but confined to exactly two of five governance
documents — `Interface Register.md`, `Dependency Injection
Register.md`, and `Module Register.md` had each already been correctly
backfilled, by `WP 7.1F`, years (in this project's own compressed
timeline) before the gap was ever named. Worth remembering: a
repeatedly-disclosed finding's own precise scope is worth re-verifying
directly before starting a fix, rather than assuming the most alarming
plausible reading of its own prior description.

**Reusing an existing document's own established section shape exactly,
rather than inventing a lighter-weight one for "just a backfill," kept
the four new Platform Service Map sections indistinguishable in quality
from the sections written when their own frameworks first shipped.**
Every new section carries the identical Responsibility/Key types/
Dependencies/Consumers/Lifecycle/ADR references/Academy references
shape the Requirements Engine section (`WP 7.3A`) already established
as the template for a "framework backfilled after the fact" entry.

## What Was Harder Than Expected

**Distinguishing "this document is missing an entry" from "this
document's own entry is stale" required checking documents that were
not named as part of the original disclosed gap.** The four-framework
gap was always described as a missing-rows problem in exactly two
documents. Checking the *other* three documents' own upstream
dependency rows (Identity & Permissions, Persistence) — not because
either was suspected of being wrong, but because this Work Package's
own controlling instruction explicitly asked to cross-check
"Dependencies"/"Consumers… Confirm consistency across all governance
documents" — surfaced two further stale entries no prior review had
found, because no prior review's own scope included checking a
*correct* row's own downstream consequences of a *missing* row
elsewhere. Worth remembering: reconciling a documented gap sometimes
requires checking the healthy-looking neighbours of the gap, not only
the gap itself.

**A register's own headline total can be wrong independently of the
specific omission a Work Package was commissioned to fix, and is easy
to miss if a Work Package only edits the total by adding its own delta
to the previously-stated figure rather than recomputing it from the
row count directly.** The Platform Services Register's own "27 entries"
had never matched its own stated bucket arithmetic (24 + 1 + 1 = 26) —
an error that predates this Work Package, three release-closing reviews
seeing it, and nobody re-deriving it from the table itself rather than
carrying the prior figure forward. Found only because this Work
Package's own edit required recomputing the total anyway (to add four
new rows), and the discipline this project has already established
(`WP 9.3A`'s own Technical Debt Register total correction, `WP 9.5A`'s
own re-verification of the same) was applied here too, by habit, not
because the instruction specifically asked for it.

## Process Observations

This Work Package is this project's first to be commissioned
specifically, and only, to close a disclosed governance gap named
across multiple prior release-closing reviews, rather than as a
byproduct of an implementation Work Package's own scope (contrast
`WP 7.1F`, which backfilled three registers as part of a broader
Engineering Core certification review, or `WP 6.8`, similarly broad).
Its own narrow scope — five documents, four frameworks, zero code — made
it possible to close the gap completely in one pass, rather than
partially, the way several prior Work Packages correctly declined to
attempt a partial fix "while also doing something else." Worth
recording as a data point: a standing recommendation that survives
three consecutive release-closing reviews without being acted on may be
better served by a small, dedicated Work Package than by continuing to
ask each subsequent, differently-scoped Work Package to fit it in.

## Recommendation for Future Work Packages

1. **Build `FCR-0005` (Governance Register Health-Check Tooling).** This
   Work Package's own existence — a dedicated Work Package needed to
   close a gap three prior reviews each correctly found, correctly
   disclosed, and correctly declined to fix as outside their own scope —
   is itself the strongest evidence yet for automating this class of
   check. A tool comparing every governance register's own claimed
   entity list against a direct source scan (the same kind of check this
   Work Package performed by hand across five documents) would catch
   the *next* instance of this pattern immediately, rather than after
   three release cycles of manual re-disclosure.
2. **When reconciling a disclosed multi-document gap, cross-check the
   gap's own upstream/downstream neighbours, not only the documents
   named in the original disclosure** — this Work Package's own Findings
   3 and 4 (stale "Depended on by" text, a stale `Related ADRs` range)
   were found only by doing so, and would not have been caught by a
   narrower "just add the four missing rows" pass.
3. **When editing a register's own headline total for any reason,
   recompute it directly from the row count rather than adding a delta
   to the previously-stated figure** — the identical discipline `WP
   9.3A`/`WP 9.5A` already established for the Technical Debt Register,
   confirmed here to generalise to any register with a stated total.

## Related Documents

`WP9.8B Reconciliation Report.md`; `WP9.8B Engineering Review.md`;
`WP9.8B Systems Engineering Review.md`; `docs/governance/Future
Capability Register.md` (`FCR-0005`); `WP9.9.0 Product Approval
Report.md`.
