# The Product Owner's Test as the Source of Record

**Release:** `v0.19.0` → `v0.19.1` (release candidates, `release/v0.19.1`) →
`v0.20.0` (release candidate, `release/v0.20.0`) → `v0.21.0` (execution plan
only, `release/v0.21.0`) · **Work Package(s):** `WP 19.9.1` (`a400792`,
`10a23ae`, `fecea02`, `3ba512d`), `WP 19.10A` (`a6eb65e`), `WP 20.10`, `WP
20.10A`, `WP 20.10D`, `WP 20.10E`, the `v0.21.0` recovery tranche ·
**Decision:** `ADR-0152` (and addendum), `ADR-0153` (Proposed) · **Code:**
`docs/releases/v0.19.1/Product Owner Comments.md`, `Product Owner Decisions
2026-09-15.md`, `Rehearsal.md`, `PHYSICAL_REVIEW.md` §7c/§7d, `docs/design/`
(the records this chapter is about, rather than code)

**In plain terms.** A test suite checks that the code agrees with itself. It
cannot tell anyone whether the finished product actually helps the person who
will run a business on it. So twice in two days, the Product Owner — who
is not a software engineer — sat down with a working build and used it as a
real consultancy would, then said what was wrong, and two further releases
were shaped by what those sessions found. Every session is written
down word for word before anyone touches the code, so nobody's memory of what
was said quietly drifts. This chapter is about that record, and the loop it
drives.

## The loop, once stated, run across four releases

Every release here follows the same shape: the Product Owner tests a
**candidate** — a build offered for judgement, not for use — and says what is
wrong; the lead **transcribes** every comment as it arrives, unedited, into a
dated file; that file becomes the **source of record**, meaning the next stage
is built *from* it, not a summary of it; the source of record becomes **Work
Packages**; before the Product Owner sees the result, the team **rehearses**
the exact script it will ask them to follow, against a real running build;
only then does a new candidate go back for another **test**.

`v0.19.0` was tested on 2026-09-14 and superseded by `v0.19.1` the same day.
`v0.19.1` was rehearsed before the Product Owner ever opened it, gated three
times on CI, and stands as today's fallback candidate. `v0.20.0` — built
overnight while the Product Owner slept, then tested by them directly the next
afternoon — is the release candidate now under that manual test, with
`v0.21.0` opened as the plan for what that afternoon still leaves to do. None
of the four has reached `main`; `v0.18.0` is the only released version this
chapter refers to.

## `WP 19.9.1`: the comments as they arrived

`docs/releases/v0.19.1/Product Owner Comments.md` names itself in its first
line: "the eight comments the Product Owner raised on 2026-09-14 while testing
the `v0.19.0` candidate `947c50d`, transcribed by the lead as they arrived,
including the nine information-architecture sketches. This is the source of
record for what `v0.19.1` builds." A comment reads "remove this taskbar, lots
of generic bits in here that aren't offering anything or even relevant,"
quoted before the document turns it into scope.

Several comments close with a hypothesis about the cause, marked as something
to check, not assumed true. Comment 1 is explicit — "Likely cause (to verify
at review time): `ProjectWorkspaceView.SetEngineeringSurface` (`WP 19.2B`)
places the surface with negative margins … the layout walk passed because it
checks bounds against parents, not sibling overlap." Comments 3, 4, 5 and 8
carry the same discipline as "Scope at review" — a proposed shape, not a
decision. **Writing "likely cause" instead of "cause" tells the next reader
this is a guess worth verifying, not a fact to build on.** Comment 4 — no
quotation anywhere — is marked **"a huge priority"** by the Product Owner,
recorded in the same breath. Comment 9, "Answers," records two decisions taken
on the spot: a quote export ships this release, and Xero is the accounting
package.

`fecea02`'s own message states the result: "ten Work Packages in four waves,
27 days." Two defects the first packages found in each other added a day; by
the time the overnight tranche closed, nineteen Work Packages across five
waves stood at "Effort: 30 days" — the extra wave answering the rehearsal
below and a backlog audit, not new Product Owner scope. That audit reconciled
the backlog row by row ("nine open rows re-verified unchanged, six raised from
the packages' own disclosures, one raised and closed in the same pass, the
live list at exactly 30") and checked "the nine Product Owner comments against
the code (five answered, four answered with a disclosed limit, none
unanswered)" — "answered with a disclosed limit" being a real, honest category
between done and not done. Throughout, the plan's own method line records the
day-to-day discipline: **"half-hourly status to the Product Owner."**

## `WP 19.10A`: rehearsing the script before the reviewer does

`Rehearsal.md` proves something most projects never check: that the manual
test document is accurate, against the running build, *before* the person it
is written for opens it. `WP 19.10A` walked every step of `PHYSICAL_REVIEW.md`
§7c — D1 through D19 — inside the headless test host, through the real window
and dialogs, checking "every visible text §7c names … verbatim against what
the host actually rendered."

It found one blocker and four differences, all closed the same day by `WP
19.10P`. The blocker: Open deliverable did nothing, though its click handler
was "structurally identical" to the working Open requirement button one row
over — no view factory had ever been registered for the Deliverable kind at
all. The differences were smaller but real: no currency symbol on displayed
money; the Invoices line mis-punctuated from a dead fallback and a doubled
period; an empty reference library showing no heading rather than one with
zero records; and one library's internal name, "BusinessRateCards", leaking
onto the screen.

The document is trustworthy for a specific reason: it **records its own
mistakes**. Two first readings were wrong, and it says so rather than quietly
correcting them out of sight — a date comparison against the wrong format
wrongly flagged D10 as a miss, and a text search wrongly flagged part of D15
by matching an unrelated label. Both are corrected from the actual captured
strings, the false reading left visible. **A rehearsal that hides its own
false positives is less trustworthy for looking clean, because nobody can tell
which remaining finding was checked as carefully.** It states its harness
limits too: sample projects seeded by `Tempest.Samples`, and a polling
helper's own `RenderUntilAsync` timeout — the source of the two false readings
above, not a real product defect.

## `Product Owner Decisions 2026-09-15`: seven questions, some asleep

The technical-debt rationalisation of 2026-09-14 left seven questions only the
Product Owner could answer — payment terms, when a calculation counts as a
task, three dropped menu entries, QuickBooks Online, DWG preview, the
Engineering Assets surfaces, and the design-system templates. `Product Owner
Decisions 2026-09-15.md` records each one twice: **as said** — "initially up
front; there needs to be a per-client basis; a drop-down with *Up front / 30
days / 60 days*," verbatim — and as the **engineering consequence**:
`Organisation` gains that vocabulary, an `InvoiceRequest` freezes the client's
terms when raised so a later change never moves an issued due date. All seven
are answered this way, including two with no consequence yet: QuickBooks stays
deferred to `v1.0.0`, Engineering Assets waits for the Product Owner to be at
the computer.

These answers were given "while the overnight tranche … was running," meaning
some packages needed a decision before dawn. `v0.20.0`'s own Execution Plan §3
states the rule this produced: **"The Product Owner is asleep; every decision
the packages needed was taken from `Product Owner Decisions 2026-09-15.md` or
disclosed as the lead's default in the brief."** One default surfaces as a
Warning, not a silent choice: "Payment terms default to Up front for a client
with none set … the lead's default from the Product Owner's decision." **A
decision made in someone's absence is not wrong for being made without them —
it is wrong only if it is not labelled as theirs to override.**

## `v0.20.0`'s own findings become Work Packages the same day

`v0.20.0` was built overnight, then handed to the Product Owner the same
afternoon to test §7c again, then its own new §7d. Six findings came back —
T1, T4, D1, D2, D12, D18 — and three Work Packages closed all six before the
day was out.

`WP 20.10A` answers D1, D2, D12 and T1 together, read as one gap rather than
four: a project had no way to set its own commercial identity at creation, or
see it again afterwards — T1 was blunt, "Commercial doesnt exist at all …
cannot navigate to it anywhere." The fix gives every project a **Details**
tab, first in its strip, over the same commercial commands the generic editor
already used, rather than forcing that editor into a role it was not built
for; New Project now takes a client, a rate card and a PO reference at
creation (D2); Timesheets' Record dialog lists every open project and states
plainly when no rate card is pinned (D12: "project drop down doesnt populate …
Doesn't allow recording of time at all").

`WP 20.10D` answers T4: a docking panel a person could drag away and never
find, because a one-pixel miss in a 4px splitter gutter was indistinguishable
from a genuine tear-out. It now tears a panel out only past the workspace's
own edge, clamps every floating window to a real screen, redocks a closed
window's content, and adds a "Show Panel" Palette entry for every registered
panel.

`WP 20.10E` answers D18: nothing stopped a project signing off while real work
against its quote was still open. `SignOffAsync` now refuses to close a
project carrying a live, uncompleted deliverable, task or calculation unless a
change order's line already carries it, naming every open item, with one
**Raise change order…** action carrying all of them at once.

The same afternoon, `WP 20.10` brought the Product Owner's own Tempest
Engineering design system into the repository, supplied complete — every
document template under `docs/design/templates/` and the system itself
(tokens, components, brand assets, fonts, a site export) under
`docs/design/system/` — "as the Product Owner supplied it on 2026-09-15." It
is a supply, not a finding, but it is what `v0.21.0`'s own document templates
are built from.

## `v0.21.0`: the recovery tranche

The lead's own assessment of that afternoon's test — not a new Product Owner
comment, but the lead's judgement of what the candidate still lacked — opened
`release/v0.21.0` the same day, cut from the `v0.20.0` candidate. Its
Execution Plan names itself "the recovery tranche": eighteen Work Packages
across about 79.5 developer-days, closing "every technical weakness named in
the lead's assessment of that afternoon" so that what follows is the actual
`v1.0.0` candidate rather than another recovery. It covers the docking model
properly (`WP 21.0A`–`21.0C`, to `ADR-0153`), Undo beyond Rename and
Favourite, documents rendered from every template `WP 20.10` brought in, and
the mutation-testing threshold.

Two packages were added on the Product Owner's own instruction after the first
wave was already running: `WP 21.5E`, a security review and posture statement
— nothing of the kind had run since the `v0.5.0` baseline — and `WP 21.5F`, a
full **offensive audit**: "break every local surface with proof-of-concept
exploits, then fully close, strengthen and solidify every finding in this
tranche (nothing filed and left)." The plan is explicit these are not one job:
`21.5F` is "the red team to `21.5E`'s defensive review."

The plan's waves are keyed to the other release still in flight: the seven `WP
20.10A`–`20.10G` packages land on `release/v0.20.0` first and merge into
`release/v0.21.0` the same hour, so `v0.21.0`'s own wave 2 bases its work on
the fixed tree. `v0.20.0` is not superseded by this — `release/v0.20.0` "stays
the candidate under the Product Owner's test until this branch is green,"
carrying every `20.10` fix as it arrives. `v0.21.0` has no code of its own
yet, only the plan.

## Standing rules for a contributor

- **Transcribe a comment as it was said**, not as a ticket already reshaped —
  the reshaping is the next document's job, not this one's.
- **A "likely cause" is a hypothesis to verify at review, never a fact to
  build on** — say so in those words.
- **Rehearse the exact script you will ask the reviewer to run**, against a
  real build, before they see it — and leave a rehearsal's own correction
  visible rather than fixing it quietly.
- **A decision taken in the Product Owner's absence is disclosed as a
  default**, in the Release Notes, never presented as though already agreed.
- **One candidate is under test at a time, and one candidate is the gated
  fallback** — never two the Product Owner might confuse, and never a fallback
  that has not itself passed the gate.

## What to take away

- **The Product Owner's own words, written down before anyone acts on them,
  are the source of record** — a summary written afterwards is already
  somebody's interpretation.
- **Rehearsing the reviewer's own script finds what only a person's hands
  would find, one day earlier and at no cost to their day.**
- **A record that shows its own mistakes is more trustworthy than one that
  shows none**, because a clean record gives no evidence it was checked as
  hard as this one was.