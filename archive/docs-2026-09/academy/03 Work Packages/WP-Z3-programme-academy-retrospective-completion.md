# WP-Z3 — Programme Academy Retrospective Completion

## 1. Introduction

`WP-Z3` created the fifteen Work Package retrospectives the remediation
programme owed under Engineering Governance §6, registered them, and
updated the Academy metrics that had recorded them as outstanding. It
changed **zero** `src/` and `tests/` files. This article is one of the
fifteen: §6 applies to every work package, including this one.

## 2. Purpose

To close the Definition-of-Done breach the programme's pre-release audit
found: fifteen work packages complete, none with an Academy retrospective.

## 3. Background

Engineering Governance §3 lists among the conditions for Done that
*"Relevant Academy documentation (§6) has been created or updated."* §6 is
unconditional: *"A Work Package retrospective (`03 Work Packages/`) is
created for every future work package … following the 13-section
template."*

The precedent is direct. `WP 13.12.0` existed solely to close this as
*"the single Release Blocking finding"* for `v0.13.0` — sixteen of
twenty-five work packages had shipped with no retrospective — and it found
that a claimed exemption in `PROJECT_STATUS.md` *"cite[d] a rule that does
not exist"*. `WP 13.12.2` then added retrospectives for `WP 13.12.0`,
`13.12.1` and itself, establishing that governance work packages are not
exempt and that a work package may write its own.

## 4. The Problem

Two problems, and the second is the reason the first matters.

**The gap.** Verified positively rather than inferred:
`git diff --name-only 00f7f39..HEAD -- "docs/academy/03 Work Packages/"`
returns empty. Not one retrospective was added anywhere on the branch.

**The count was wrong twice.** The pre-release audit called it "eleven",
counting only the remediation work packages and omitting **`WP-REVIEW`**,
which produced `PHYSICAL_REVIEW.md` and found `TD-116`. The Stage 2 brief
then listed thirteen — the eleven plus `WP-Z1` and `WP-Z2` — still omitting
`WP-REVIEW`, and not accounting for this work package's own obligation. The
true figure, derived from `git log`, is **fifteen**: `WP-C` (two commits,
one work package), `WP-B1`, `WP-D2`, `WP-A1`, `WP-H`, `WP-REVIEW`, `WP-D1`,
`WP-F`, `WP-B2`, `WP-G`, `WP-A2`, `WP-E`, `WP-Z1`, `WP-Z2`, `WP-Z3`.

## 5. The Design

One retrospective per work package, all fifteen in one commit with every
index, register and status update alongside — because an unindexed article
under `docs/academy/` is an orphan, and `governance-healthcheck.ps1`'s
Academy check fails on orphans. Splitting the work would have left the
health check red at every intermediate commit.

**Naming.** Engineering Governance §12 prescribes
`WPX.Y[-Letter]-kebab-case-title.md`, *"matching the Work Package number
exactly as `WorkPackages.md` itself names it."* These work packages have no
`X.Y` number — they were commissioned as `WP-C`, `WP-A1`, `WP-Z1`. The
rule's principle is to match the real identifier, and every reference in
the repository — fifteen commits, Technical Debt Register rows,
`ADR-0118`, `ADR-0119`, test doc comments — uses those names. Files are
therefore `WP-<id>-<kebab-title>.md`. **No `WP14.x` identifiers were
invented**, which would have orphaned every existing reference.

**Evidence.** Written from committed evidence only: commit messages (196 to
730 words each, carrying problem, design, rejected alternatives,
deviations, discoveries, mutation results and test counts), Technical Debt
Register rows, `ADR-0118`/`ADR-0119`, `PHYSICAL_REVIEW.md`, the Test and
ADR Registers, and the source and test files themselves.

## 6. Alternatives Considered

**Fifteen commits, one per retrospective.** Rejected: every intermediate
commit would fail the governance health check on orphaned articles.

**Assign `WP14.x` numbers.** Rejected — it would invent identifiers
appearing nowhere else and orphan every existing cross-reference.

**Batch the retrospectives into release preparation.** Rejected on the
governing text and the precedent: §6 is part of §3's Definition of Done,
and `Engineering Readiness Review Architecture.md` §2.4 makes a work
package that *shipped* without one a blocking finding. Correcting before
merge keeps it from ever becoming that.

**Also backfill the older gaps this work package's audit found.**
Explicitly out of scope, and deliberately so — see §12.

## 7. Why This Solution Was Chosen

Because the alternative to writing them now is writing them later with less
evidence. The Stage 1 audit demonstrated the risk: `WP 13.9.1` had already
been forced to reconstruct `WP 13.3B`'s retrospective from a commit message
because the article was never written. Every day between the work and its
record makes the record worse.

## 8. Architectural Principles

Engineering Governance §6 treats the Academy as *"a maintained asset, not a
one-time deliverable"*. The §9 decision-authority boundary was also
respected: the classification of this gap is mechanical and reported;
whether the work merges or releases is Product Approval's call, and was
sought rather than assumed.

## 9. Benefits

Fifteen work packages have a durable record of their reasoning, their
rejected alternatives, their deviations and their discoveries — the parts
that do not survive in a diff. The programme is compliant with §6 before
merge rather than at a release gate. And the count discrepancy is resolved
against the repository rather than carried forward.

## 10. Trade-offs

**A stated evidence limitation.** Each work package's Stage 1 audit report
existed only in the commissioning conversation, not in the repository.
Where a retrospective would otherwise cite audit findings, it cites what is
committed — the Technical Debt Register row, the commit message, the ADR —
rather than reproducing recollections as though they were records. Fine
detail from those audits is genuinely absent and is not reconstructed.

These are also retrospectives written after the fact rather than alongside
the work, which is what §6 asks for. That is the cost of the gap, and it is
not fully recoverable by closing it.

## 11. Common Mistakes

**Trusting a count that was stated rather than derived.** "Eleven" and
"thirteen" were both wrong, in different ways, and both came from
summaries rather than from `git log`.

**Treating governance work as exempt.** `WP 13.12.2`'s precedent is
explicit, and this article exists because of it.

**Writing an article and not indexing it.** The health check's orphan
detection is the real gate, and it is the reason this is one commit.

**Filling in a section you have no evidence for.** Where the evidence is
absent, the limitation is stated.

## 12. Future Evolution

The Stage 1 audit found two **larger, older gaps deliberately left
untouched**, recorded here as context rather than actioned:

- The ~34 pre-programme commits on this branch — `TD-59`, `TD-58`,
  `TD-70`/`TD-71`, `TD-84`, `TD-85`, `TD-89`, `TD-72`, `TD-93`, `TD-31`,
  `TD-80`, `TD-102`, `TD-75` phases 1–2, Project Tasks, Project Risks,
  Project Timeline, `TD-77` Stages 2–5 and others — also have no
  retrospectives.
- `docs/academy/03 Work Packages/` contains no `WP11.*` file at all:
  `v0.11.0`'s ten work packages (`WP 11.0A`–`WP 11.4B`) shipped with none.

Both will surface at the release gate exactly as this did. Neither is
`WP-Z3`'s to close.

## 13. Key Takeaways

- Derive the scope from the repository. Two prior statements of this work
  package's own size were both wrong.
- Governance work packages are not exempt from governance, and a work
  package can write its own retrospective.
- Index in the same commit as the article; the orphan check makes that
  structural rather than optional.
- Write the retrospective while the evidence is fresh. Closing the gap
  later recovers the facts, not the reasoning.

## Related Documents

- `docs/academy/06 Engineering Standards/Engineering Governance.md` — §3, §6, §9, §12
- `docs/architecture/Engineering Readiness Review Architecture.md` — §2.4
- `WP 13.12.0` retrospective — the precedent for backfilling
- `docs/academy/Academy Index.md`, `docs/governance/Documentation/Academy Register.md`
