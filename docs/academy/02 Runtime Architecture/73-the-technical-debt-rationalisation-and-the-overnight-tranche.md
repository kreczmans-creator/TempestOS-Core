# The Technical-Debt Rationalisation and the Overnight Tranche

**Release:** release candidate `v0.19.1` (`release/v0.19.1`, head
`94998b9`, 2026-09-14 into 2026-09-15) · **Work Package(s):** `WP 19.10F`,
`WP 19.10G`, `WP 19.10B`–`WP 19.10R` · **Debt:** `TD-150`, `TD-157`,
`TD-176`, `TD-179` (residual), `TD-41`, `TD-156`, `TD-158`, `TD-92`;
`TD-131`, `TD-134`, `TD-154`, `TD-63`, `TD-93`; `TD-42`, `TD-78`, `TD-137`,
`TD-149`, `TD-25` (register-only findings) · **Decision:** `ADR-0145`
addendum · **Code:** `BACKLOG.md`,
`src/Tempest.Core/Persistence/SqlitePersistenceStore.cs`,
`src/Tempest.Workspace/Projects/ProjectContext.cs`,
`src/Tempest.Core/Commands/ArchivedProjectCommandGuard.cs`,
`src/Tempest.Core/ReferenceData/ReferenceDataCatalog.cs`

**In plain terms.** "Technical debt" is what engineers call a known
shortcut or gap in the software — something not quite right, written
down on purpose rather than left for someone to discover the hard way.
On the evening of 2026-09-14 the Product Owner asked for a full account
of it. What came back was not the existing list retyped: every row was
checked again against the actual code, sixteen of the worst gaps were
fixed outright overnight, and the list itself was checked for honesty —
found wrong in both directions, claiming faults already mended and
fixes that had never actually happened. Almost none of this shows on a
screen. What changes is how much the next person can trust what the
project says about itself.

## What "technical debt" means here, and why a rationalisation is not a list

`BACKLOG.md` sorts every disclosed shortcoming into three buckets:
**Owned by Programme** (a past Work Package already closed it),
**Archived with the Layer** (the code lives in `src/Frozen/`, unshipped),
or the **Live Backlog**, capped at 30 rows a user could still notice
today. `09-the-governance-reset-and-how-a-release-is-now-run.md` covers
where that file and its cap came from; this chapter is about what
happened when someone finally re-checked every row in all three
buckets at once.

A **rationalisation** is that re-check: not "does this row still sound
right", but open the named file, read the named line, and see whether
the claim holds. Every row below carries a `path:line` citation or a
named test, never a restated assumption. Each is scored twice — visible
to a user today, and how big the honest fix is (S/M/L developer-days) —
and the two combine into a priority from **P1** (a live defect, close
first) to **P4** (deliberately deferred, waiting on a decision or
trigger that has not fired). A P4 is not a lesser bug; it is one the
project has chosen, on the record, not to fix yet — `TD-174`'s missing
part-number field waits on a drawing actually needing it, by decision
`D-028`.

## Two reports, read against the exact same clock

**Part 1** (`WP 19.10F`, merged `6cd9cf2`) covers the Live Backlog's 30
rows plus the debt the code and ADRs disclose that no row names,
audited by six parallel code passes plus two source audits. **Part 2**
(`WP 19.10G`, merged `f2b8ff4`) covers the Owned-by-Programme rows, the
Archived-with-the-Layer rows, and the 170-row frozen register
`BACKLOG.md` grew out of. Both were read-only, against the identical
commit (`5abeb57d`, this branch's own base) — so that sixteen Work
Packages already racing to fix things that night could not make the
audit's own count drift while it was still being written.

Part 2's headline finding is the sharper of the two: of the **57 rows**
`BACKLOG.md` listed as Owned by Programme — each meaning some past
release had claimed it done — only **26 were actually closed**, **4**
were partly closed, and **27 were not closed at all**. Nearly half of
what a prior release marked finished was not, on re-inspection.

## What the audit found about its own paperwork

A register can be wrong two ways, caught both ways the same night.
**Understating what is fixed:** `TD-42` (the release script's `git
tag`/`git push` never checking success) was already fixed at this
branch's own base commit, before the audit began — the row had simply
never been told. `TD-78` claimed no brand design system existed;
`BrandPalette.cs` turned out complete, WCAG-audited, in use across 75
files — never debt at all. `TD-137` (every write fsync'd) and `TD-149`
(a deleted legacy record cannot resurrect) were both genuinely fixed
months earlier by the same rewrite that fixed their still-open
neighbours; `ADR-0145`'s own "Closed" section had claimed `TD-150`
alongside them prematurely, and its addendum records that `BACKLOG.md`'s
row was the one that had it right, keeping `TD-150` open the whole time
against the ADR's own over-claim.

**Overstating what is fixed is worse, because nobody looks again.**
`TD-25` (no protection against two people editing a Requirement at
once) was attributed to a Work Package that never touches
`RequirementsService`; its real governing document, `ADR-0060`, is a
deliberate deferral waiting on an actual incident. `TD-157` — real,
still open — had outgrown its own "Release Blocking" label: neither
service it protects has a Desktop screen yet, so nothing shipped could
reach the warning. The severity was revised down, not the row, because
it goes live the day a screen is built — see
`60-source-citations-and-supersession.md`, where the warning was
designed in the first place.

## The overnight tranche

The request came in the evening; the rationalisation answered it
through the night, and the lead — with the Product Owner away — used
the same night to act on it. Sixteen Live Backlog rows closed, each
with its own new test: six with a Work Package each (`TD-176`
`WP 19.10B`, `TD-177` `WP 19.10C`, `TD-33`/`TD-157` both `WP 19.10E`,
`TD-41` `WP 19.10I`, `TD-150` `WP 19.10J`), seven in two combined
packages (`TD-63`, `TD-93`, `TD-131`, `TD-134`, `TD-154` under
`WP 19.10M`; `TD-27`, `TD-92` under `WP 19.10N`), one narrowed rather
than closed (`TD-179`), and two — `TD-42`, `TD-78` — needing no code at
all. The same night, `WP 19.10K`/`WP 19.10L` closed three
Programme-owned rows Part 2 had just found still open: `TD-23`, `TD-32`
and `TD-141`'s second unguarded write path.

Even as this ran, the Product Owner answered seven open questions in
the small hours (`Product Owner Decisions 2026-09-15.md`). None of the
sixteen closures needed one of those answers — they were code defects
with only one honest fix once found — but the discipline carries into
the next tranche, whose own plan states the rule plainly: "every
decision the packages needed was taken from [that document] or
disclosed as the lead's default in the brief", a pattern
`74-the-debt-tranche.md` continues the morning after.

### The other side of commit: `TD-150`

`SqlitePersistenceStore.ExecuteInTransactionAsync` closed its database
connection outside the block protecting `COMMIT;` — so a write could
land durably, and a failure closing the connection *afterwards* was
still reported as a failed write. `WP 19.10J` (`e98d77f`) gates the
close inside a `committed` flag: once `COMMIT;` returns, a close failure
is logged and swallowed, not thrown — mirroring `TD-147`, the
release-blocking defect `54-one-transaction-per-engineering-change.md`
covers, from the opposite side of the same boundary.

### A warning nothing could ever trigger: `TD-157`

The validation services that raise "pinned source superseded" already
accepted a collection of resolvers; nothing in `TempestHost.cs` ever
registered one, so the collection was always empty. `WP 19.10E`
(`7e44f25`) adds `ReferencePinResolverCollection`, registering all
eight reference libraries' own resolvers at once — the seam
`60-source-citations-and-supersession.md` designed but never wired up.

### A race that looked like a deletion: `TD-176`

`ProjectContext.RefreshAsync` closed the whole project context whenever
an overlapping refresh's own read came back "not found" — which could
happen simply because a second refresh started first. `WP 19.10B`
(`e48adcf`) adds a generation token: only the *latest* refresh started
may act on what it reads. Investigating it surfaced a second defect —
`ProjectBrowserView.CreateAsync` looked for a just-created project in a
stale, pre-filtered list and never opened it — closed the same night as
`WP 19.10Q`.

### Archived means archived, all the way to the Ribbon: `TD-179`

The sharpest single finding: an archived project's workspace showed a
passive "read only" banner while several buttons stayed enabled
underneath it. `WP 19.10H` guarded the Milestone, Task, Evidence and
manual-task services directly; the Ribbon and Command Palette still
asked no question at all, closed by `WP 19.10R`'s
`ArchivedProjectCommandGuard`, consulted wherever a binding declares
`Mutates: true` — sixty binding sites, seventy-seven commands. One
residual remains by design: `IRequirementsService.CreateAsync` takes no
project id to guard against. The mechanism, and where it sits in the
project's lifecycle, is
`70-quotations-change-orders-and-the-project-lifecycle.md`'s subject.

### A Requirement that finally opens: `TD-41`

A working Owner/Priority editor for a Requirement had existed for
releases, unreachable because `ObjectEditorView.TryCreate` only ever
asked the engineering-object repository, which never holds a
Requirement — a separate aggregate entirely. `WP 19.10I` (`c3eed27`)
falls back to `IRequirementsService`, resolved asynchronously so no
second blocking call was added to the one already disclosed elsewhere.

### And the rest of the night

Three more closures, more briefly. `ReferenceDataCatalog<T>.RegisterAsync`
and `SupersedeAsync` each composed several durable writes with no
shared transaction, so a fault partway could strand a record between
states; `WP 19.10K` (`b6d532a`, `91fde43`) wraps all three paths in one
transaction and separately fixes `TD-156` — a superseded record's own
secondary key is no longer held forever, so a correcting replacement
can legitimately reclaim it. The docking engine already computed a live
drop target while dragging a panel; nothing on screen showed it, so
every rearrangement happened blind — `WP 19.10N` (`e729533`) renders
one highlight over the real target's own bounds (`TD-92`), a capability
that existed one layer down needing only to be shown. And `WP 19.10M`
closed five small internal-quality gaps in one pass: a focus-ring
contrast test that skipped every state it was meant to measure
(`TD-131`); a settings document with no notion of "too new to read"
(`TD-134`); a CI marker firing before the Desktop shell had actually
finished composing (`TD-154`); a dirty-tab-close confirmation proven
only by calling its method directly, never a real window (`TD-63`); and
`Tempest.Samples` hand-copying thirteen vocabulary strings it could
reference instead, the same discipline
`45-deleting-dead-architecture-and-consolidating-duplicates.md`
practises elsewhere (`TD-93`). None found a live defect; each closed a
place the test suite could not previously have caught one.

## The backlog, trimmed and reconciled

The Live Backlog's own heading tells the rest: **30 of 30** before that
night, **13 of 30** by the time `v0.20.0` was cut — most of the
difference this chapter's sixteen closures. Two honest exceptions travel
with that number: `TD-42`'s row still sits in `BACKLOG.md` as if open,
because already-fixed is not the same as formally closed; and
`TD-137`/`TD-149` still carry no "Closed" note of their own, even now,
for the identical reason. Fixing the finding and fixing the file are two
separate acts, and the file has not yet caught up on these three rows.

## What was deliberately left for the morning

Not everything rated closable was closed that night. The P4 rows stayed
deferred, by the decisions that put them there; two larger product
features — tear-out and dock everywhere, and the document templates
package — waited for their own ADRs and the Product Owner at the
computer; `TD-22`/`TD-29` (the calculation engine's untyped results and
inputs it cannot retain), `TD-101` (a document page always rasterised
in full, never by viewport), and `TD-165`/`TD-160` (the merged
engineering capability with no Desktop screen at all) were named and
rated, then left for the next tranche rather than rushed.
`74-the-debt-tranche.md` covers what happened to them; this chapter
stops at the boundary the lead drew that morning.

## What to take away

- **A debt register is itself a claim, and needs re-checking as much as
  code does** — this one was wrong in both directions, and neither
  error was found by reading the register alone.
- **A rationalisation is a re-verification, not a re-listing**: every
  row here stands on a file, a line, or a named test, never a restated
  assumption from the last time someone looked.
- **Closing debt overnight and closing the book on it are different
  acts** — sixteen real fixes still left rows in the file quietly out
  of step with the code, and saying so plainly is what let this chapter
  be written at all.
