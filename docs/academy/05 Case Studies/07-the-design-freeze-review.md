# Case Study: The Design-Freeze Review — Keep the Substrate, Remediate the Surface, Buy the Option to Pivot

*Companion to `docs/reviews/design-freeze-2026-09/Design Freeze Review.md`.
8 September 2026 (commits `24e6043`, `3e35779`, `c39c0ef`); its
consequence, `D-028`, is dated the following day, 9 September 2026.*

**In plain terms.** After the first substrates of `v0.17.0` were built
and the Product Owner had tried the product on a real Windows machine
for the first time, the Product Owner faced a decision no user manual
could answer: keep going, or throw the surface away and start again?
Instead of guessing, the chief engineer produced a report in which every
number could be checked against the actual code, and which said plainly
where it was uncertain. That is what let a non-engineer make a decision
usually reserved for engineers — not because it became easy, but because
the evidence for it was laid out where anyone could inspect it.

## The question

By 8 September 2026, `v0.17.0` had replaced the persistence layer with a
transactional store, made units a proper runtime concept, frozen the
platform-era plugin and licensing machinery outside the build, and
collapsed identity to one signed-in user. The Product Owner then used
the product on Windows for the first time. It did not go well: "the
engineering workspace doesn't open properly in any windows, it's a
really hard to navigate place that doesn't actually contain half of the
things it claims to have."

That is the moment a **pivot** decision gets asked. A pivot means
stopping work on the current product and starting again on the same
principles but a different shape — not a small correction, and not
throwing everything away. The Product Owner's instruction was exact:
*"architectural structural columns to remain the same, but general
layout can be updated within reason."* Two terms carry the question:

- **Architectural columns** are the load-bearing decisions no
  implementation could do without and stay the same product: one durable
  store with one transaction boundary, one authorisation point, reads
  that go direct while every change is a command, units that never
  silently convert, governed reference data, the object model's shape,
  one shipped desktop surface, and the rule that the tool reports while
  a person decides. Change a column and you have built a different
  product.
- **Layout** is everything built on the columns that a user actually
  sees and clicks: which entry sits in the navigation rail, how a screen
  is composed, how a new object finds its way onto a tree. Layout can be
  redrawn without touching a column underneath it.

So: is what went wrong a column problem, which no amount of remediation
fixes, or a layout problem, which can be fixed without discarding what
already works?

## The evidence gathered

The review's own standard: **every figure is reproducible from the
repository at commit `31bba97`**, its commands recorded in seven
evidence appendices under `docs/reviews/design-freeze-2026-09/evidence/`
(three ADR-verdict files, `metrics.md`, `substrate-audit.md`,
`surface-audit.md`, `debt-tests-plan.md`). For a non-engineer reader
that matters: the Product Owner did not have to trust the chief
engineer's judgement, only hand a figure to another engineer and check
the answer matched. It read the design as decided (all 147 Architecture
Decision Records — the project's permanent record of a choice and its
reasoning — each classified structural-column, keep, keep-but-simplify,
revisit or drop, with code cited); the code as built; the debt, test and
CI record; and the product as experienced (the Windows session, the
programme's only real end-user evidence). It did not re-run the test
suites or benchmark anything.

**What exists.** Of the 147 ADRs, 30 are structural columns, and *every
one is realised in code*. The four newest — the transactional SQLite
store, dimension-vector units, frozen platform layers, session
principal — were verified complete to the letter. The substrates most
expensive to rebuild (persistence, units, reference governance,
commands) are finished, proven by fault-injection and property tests,
and reusable **verbatim** whichever option is chosen.

**What is genuinely wrong.** Two things, both beneath the surface. Any
signed-in person, not only a designated reviewer, could release governed
reference data — the numbers a calculation cites and stands on for
years — because the review path checked that someone was signed in but
never checked *who* (hazard **H8**/**H13**; fully covered in *02 Runtime
Architecture/51-engineering-reference-data.md*). Second, three separate
ways of recording a verification result had grown up side by side. Both
cost the same to fix under either option, so neither argues for a pivot.

**What is wrong with the surface.** The report's own words: the shell
was **"composed from platform primitives rather than from the user's
questions."** A Kind-keyed explorer over parent links is a correct,
general building block; "where is the thing I just made?" is a specific
question no general building block answers on its own. Every defect the
Product Owner found was this shape; the walkthrough and its one-day fix
belong to *02 Runtime Architecture/58-where-things-land-and-open.md* —
this case study concerns the decision the review made possible.

## The options

**Option 1 — keep it, remediate the surface.** Continue `v1.0.0` on
`release/v0.17.0`, with five review-recommended amendments (a
Findability Work Package, an earlier archive of unreachable code, a
Work Package widened to fix the facet problem at its root, two half-day
substrate fixes). Cost: 106–109 developer-days, 17–18 weeks, a
reviewable release roughly every six to seven weeks.

**Option 2 — pivot: re-implement on the same columns, a different
layout.** Keep the 30 columns and their ≈22,000 lines of substrate
verbatim; archive ≈31,000 lines no screen reaches; replace the
platform-era hosting machinery with an off-the-shelf framework; generate
the explorer, object page and search from one declaration per Kind
instead of six registrations. Cost: 113 developer-days, 19 weeks — and
the first seven weeks produce **nothing the Product Owner can open**,
because the object model must be re-cut before any screen exists again.

The arithmetic gap is four to seven days — genuinely close. The review
is explicit that cost should not decide this: this programme's weakness
has been assumptions not put in front of a user, and **Option 1 puts a
release in front of one three more times before `v1.0`.**

## The recommendation, and the shadow build

The recommendation: **do not pivot.** Continue Option 1 with the five
amendments, fix the two high substrate hazards before `v0.17.0` is
verified, and — aimed squarely at removing guesswork — **spend ten
developer-days on a shadow build.**

A **shadow build** is a small, time-boxed version of the pivot built in
parallel, in an isolated worktree, alongside the ordinary programme —
not a plan for the pivot, an actual attempt at its hardest part.
Concretely: build the fresh layout's first three phases (a new solution
shell, the re-cut object model, the generated application layer) over
two weeks, and check whether it reproduces two real user journeys —
creating a Mechanical part, running a governed calculation — on the new,
generated pages. The point is to convert "we estimate a fresh layout
would work better" from judgement into fact — **"a decision on evidence
rather than on estimates,"** in the review's own words. The
recommendation also set a **kill switch** — conditions, agreed before
the fact, under which it should be reversed (for example: the next
Windows review finding three or more of the same "made it, can't find
it" defects) — so nobody argues afterwards about whether a result counts.

## The decision, and what it changed immediately

The Product Owner accepted the recommendation. Two things happened the
same night, as `WP 17.9.3`. **The ADR Register was corrected**: six ADRs
whose recorded status no longer matched the code were amended —
`ADR-0016`, `0033`, `0035` (commit `3e35779`) and `ADR-0043`, `0116`,
`0124` (commit `c39c0ef`, which also corrected the Register's narrative
from "144 ADRs" to the true 147). A review that fixes its own record's
staleness, not only the code's, is checking whether the codebase's
account of itself can be trusted — worth noticing in a report a
non-engineer is asked to rely on. **The two high substrate hazards and
surface small wins were fixed**: `WP 17.9.3` added the permission check
flagged as H8/H13, added an index so children are found without scanning
every object in the store (H4/H5), and picked up several surface defects
the audit found by reading rather than by a user finding them first.
None of it touched persistence, transactions, units or the calculation
engine — exactly what the finding about a sound substrate predicted.

## The consequence, the next day: D-028

Buried in the recommendation was a number the Product Owner used the
next day to ask a sharper question: about a third of the remaining
programme — 34 developer-days — was a calculation tool, complete with
its own frozen expression grammar, that would compete with Excel,
Mathcad and SMath on the engineer's own desk. On 9 September the Product
Owner asked directly: *"Is this the place for doing calculations? Or
should they be done in separate software... and the evidence then
loaded into Tempest as a record of that?"*

The answer is `D-028`, and it re-scoped `v0.18.0` from "Calculation as
Document" to **"Evidence and Check"**: TempestOS `v1.0` would be a system
of record that a client project's evidence is tagged to, not a place
that computes anything new. The programme's highest-risk remaining Work
Package — a calculation grammar written before its own parser — left the
plan along with the days behind it, and `v0.18.0` dropped from 42 to 37
days. The Product Owner amended the decision the same day to keep the
existing in-app calculation surfaces rather than remove them — *"I'd
rather have it in place and we can pivot and strip out later than have
to build it later"* — so nothing shipped was thrown away; only new
investment in it stopped. The full shape of that decision is *02 Runtime
Architecture/59-evidence.md*; the frozen-layer consequences are in
*56-frozen-layers.md*.

D-028 is the clearest proof the review did its job: commissioned to
answer the pivot question, its evidence was laid out plainly enough that
the Product Owner could reuse it the next day for a different, larger
decision without waiting for a second review to be written.

## What to take away

- **A pivot decision is made on reproducible evidence, not on how the
  code feels to the person reading it.** Every figure here could be
  checked against a named commit; that is what let someone who does not
  write code make the call.
- **Naming your own uncertainty is what makes a recommendation
  trustworthy.** The review said what it had not tested, set a kill
  switch for its own reversal in advance, and corrected its own
  register's stale entries in the commit that used it as evidence.
- **A shadow build turns "a rewrite would be better" into a measured
  fact, for a fraction of the rewrite's own cost.** Ten developer-days
  of real code settle an argument that could otherwise run indefinitely.
