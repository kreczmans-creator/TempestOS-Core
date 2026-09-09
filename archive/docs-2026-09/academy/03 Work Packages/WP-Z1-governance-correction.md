# WP-Z1 — Governance Correction

## 1. Introduction

`WP-Z1` (`05a4218`) closed the findings of the programme's pre-release
audit. It corrected a figure the author had written into `TD-108` one
commit earlier, normalised eight Status cells that contradicted their own
contents, re-derived every count in the Technical Debt and Test Registers,
and brought `PROJECT_STATUS.md` up to date using that file's own retention
convention. **No production code was touched.**

## 2. Purpose

To make the governance record describe the repository that actually exists,
before any of it is merged or released.

## 3. Background

The pre-release audit of the eleven-work-package remediation programme
found the registers trailing the work they were supposed to record: review
dates predating the entire programme, totals stale by fourteen rows, a
figure that was simply wrong, and test documentation contradicting a test
in another file.

## 4. The Problem

**The wrong figure was the author's own.** `TD-108`'s per-assembly split
read "26 Desktop / 37 App". Re-derived, it is 28 Desktop / 35 App at
`7de6290` — matching neither the textual (28/41) nor the executable (28/35)
division, so it was not a rounding of either. It had gone in as part of
`WP-E`'s own correction of that row's *earlier* wrong figures, which is the
least excusable place for it.

**Eight rows ended with their own closure and began with the word "Open".**
`TD-105`, `TD-106`, `TD-107`, `TD-110`, `TD-111`, `TD-112`, `TD-113` and
`TD-114` each appended "Closed"/"Completed" at the end of a cell that led
with "Open". The last column is named **Status**, so anyone reading the
leading token — which is what that column is for — read eight finished rows
as outstanding.

**Counts were stale.** The register stated "104 tracked"; it holds 118 TD
rows and 26 AT rows. The Test Register recorded six of the eleven work
packages and stated executed cases of 3,068/366 against an actual
3,088/370.

**Two test files contradicted each other.**
`DormantKeyboardBindingTests` still described `InputBindingRouter` as
routing through the obsolete Id-only overload and being allow-listed as
DORMANT — both falsified by `WP-A2`, and `IdOnlyInvocationGuardTests`
asserts the opposite. Its failure message also instructed a contributor to
do `WP-A2`, which was done.

## 5. The Design

Five files, no `src/`.

Status cells were normalised by promoting each row's **own** closure verb to
the leading position, keeping the full body — following the precedent
`TD-31`, `TD-72` and `TD-80` already set, where a terminal row leads with
its terminal status. `TD-105`/`106`/`107` lead **Closed**;
`TD-110`–`TD-114` lead **Resolved**. No taxonomy was invented: each row
kept the word it had used.

Counts were re-derived from the file after the edits, not patched by
arithmetic: **118 tracked — 35 Resolved, 6 Closed, 74 Open, 3 Partially
resolved**, plus 26 accepted trade-offs.

The Test Register's four drifted cells were corrected, and its historical
progression table — whose last row had been labelled "**Current (WP 5.3)**"
across eight releases while the real total grew from 552 to 3,458 — was
relabelled as the historical figure it is, with a verified current row
added and the progression itself preserved.

`DormantKeyboardBindingTests`' prose and failure message were corrected.
**The assertion was untouched**, because it still guards `AT-23`: the
keyboard ships bound to nothing by product choice, which is now the whole
of what it means.

`PROJECT_STATUS.md` was updated using its own established convention — the
authoritative `Last Updated` block rewritten, the four "Current …"
preambles refreshed, and a correction preamble added to Repository Metrics
— with the stale lower sections retained, not deleted.

## 6. Alternatives Considered

**Rewrite the eight rows entirely.** Rejected: the bodies are accurate and
detailed. Only the leading token was wrong.

**Invent a status taxonomy.** Rejected — the register uses both "Closed"
and "Resolved" for delivered work, and imposing a distinction it had never
drawn would have been a change of meaning disguised as a correction.

**A full re-derivation of `PROJECT_STATUS.md`.** Rejected and escalated:
its lower sections are stale by up to eight releases, which is its own
decision and was deliberately not absorbed here.

**Quietly fix the `TD-108` figure.** Rejected. It is disclosed as an error
rather than presented as a refinement.

## 7. Why This Solution Was Chosen

Because a register that contradicts itself is worse than one that is merely
behind — a reader who trusts the Status column gets eight wrong answers,
and a reader who does not trust it has no register at all.

Disclosing the `TD-108` error rather than silently amending it matters for
the same reason: the row's entire purpose was recording that earlier
figures had been wrong.

## 8. Architectural Principles

The project's standing rule that figures are re-derived from the repository
rather than carried forward — applied to the registers themselves, and then
applied again to check every figure written.

`PROJECT_STATUS.md`'s own retention convention ("this field is stale; the
content below is retained, not deleted") was followed rather than
overridden, which is what kept an 8,902-line file from being rewritten.

## 9. Benefits

Eight finished rows read as finished. Every count in two registers matches
the repository. A false "Current" label eight releases old is gone.
Two test files agree. And the programme is recorded in the registers that
are supposed to record it.

## 10. Trade-offs

`PROJECT_STATUS.md`'s pre-existing drift is left in place. That is a
deliberate boundary, stated in the file itself, and it means the lower
sections still describe v0.5-to-v0.10-era state.

The `WP-F` correction (below) is disclosed in the Test Register rather than
only here, which makes that register longer but keeps the correction where
a reader of the figure will find it.

## 11. Common Mistakes

**Patching a count by arithmetic.** Every total here was recomputed from
the file after editing, then verified again after writing.

**Trusting your own recent figure.** The `TD-108` split was written one
commit earlier by the same author and was still wrong.

**Fixing prose and assuming the assertion follows.** The stale test
documentation was wrong while the assertion remained correct and
load-bearing; changing the assertion would have removed `AT-23`'s guard.

## 12. Future Evolution

Two items were escalated rather than absorbed: a full `PROJECT_STATUS.md`
re-derivation spanning eight releases, and the `Validation Register.md`
Test Gate row, still reading 552 — left for release preparation, since that
figure should come from the release's own verification run rather than be
copied across.

**Newly discovered:** the Test Register's Desktop attribute total had been
wrong since `WP-F` re-derived that table — 358 recorded against a true 353
— because attribute names were counted wherever they appeared, so a comment
naming `[AvaloniaFact]` and `[Fact]` together counted twice. Corrected, and
disclosed in the register.

## 13. Key Takeaways

- The Status column is read by its first word. Everything after it is
  commentary.
- Re-derive counts from the file, then verify what you wrote.
- Disclose your own errors as errors, especially in the row that exists to
  record earlier errors.
- A stale label ("Current") is a defect in its own right, independent of
  whether the figure beneath it was ever correct.

## Related Documents

- `docs/governance/Quality/Technical Debt Register.md`
- `docs/governance/Quality/Test Register.md`
- `PROJECT_STATUS.md`
- `WP-E` retrospective — where the `TD-108` figure was introduced
- Commit `05a4218`
