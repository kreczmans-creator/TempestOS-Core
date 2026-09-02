# WP-G — The Project CRUD Leaves MainWindow, Verbatim

## 1. Introduction

`WP-G` (`0a9e49b`) moved nineteen CRUD methods out of `MainWindow` into two
new collaborators, byte-for-byte. `MainWindow` went from 1,577 to 1,042
lines. `TD-109` stays **Open, partially resolved** — deliberately — and
`TD-112` was recorded as completed after this work package's own audit
found its row stale.

## 2. Purpose

To address the one thing `TD-109` actually named: the complete CRUD
interaction logic for projects, tasks, milestones, deliverables, risks,
issues and decisions, living inside the composition root.

## 3. Background

`MainWindow` was a composition root, an event-wiring hub, *and* the
implementation of every project-domain CRUD interaction. `WP 12.0B`
(`ADR-0103`) had already extracted eighteen collaborators from it and from
`EngineeringCockpit`; this family had not moved.

## 4. The Problem

CRUD interaction logic in a composition root is not merely untidy. It means
the file that decides *what the application is made of* also decides what
happens when a user clicks "Create Task" — two responsibilities with
different reasons to change, and 533 lines of the second buried among the
first.

## 5. The Design

Two collaborators, split along the **domain service** rather than by size:

- `ProjectDeliveryCoordinator` — tasks, milestones, deliverables
  (`IProjectTaskService`, `IProjectMilestoneService`)
- `ProjectGovernanceCoordinator` — risks, issues, decisions
  (`IProjectGovernanceService`)

The seam was already there: **not one of the 19 methods touched both
sides.** A single 533-line collaborator would have been the largest file in
`Composition/` and would have relocated the god object rather than
decomposed it.

**Moved verbatim, and checked rather than asserted.** All 19 method bodies
were diffed against `HEAD` and are byte-for-byte identical after the two
permitted edits: field access becoming constructor-injected dependencies,
and `RecordHistory(...)` becoming the injected `Action<string>` the three
existing collaborators already take (4 sites in delivery, 6 in governance,
matching the audit's count of 10). Prompts, messages, identifier schemes,
`try`/`catch` blocks, refreshes, async shape and ordering are untouched.

The 21 `_projectWorkspace.*Requested` wiring lambdas stay in the
composition root, which is where deciding what a surface event runs
belongs.

**Both seams were mutation-checked, because "it compiles" is not evidence
the wiring still reaches the code.** Unwiring `CreateTaskRequested` fails
four `ProjectTaskAcceptanceTests` journeys; unwiring `CreateRiskRequested`
fails two `ProjectGovernanceAcceptanceTests` journeys. Those tests drive
the real buttons and dialogs through the real window, so they cannot be
satisfied by wiring that merely compiles.

## 6. Alternatives Considered

**One collaborator for all nineteen methods.** Rejected: 533 lines, the
largest file in `Composition/`, and a relocation rather than a
decomposition.

**Split by size or by CRUD verb.** Rejected in favour of the domain-service
seam, which already existed in the code and required no method to be
divided.

**Refactor the methods while moving them.** Explicitly rejected. A move
that also improves is a move whose behaviour cannot be verified by
comparison.

**Route the 41 CRUD toast sites through `ActionOutcomeReporter`.**
Explicitly not done — that family carries no `ActionOutcome` and refreshes
no dependent surfaces, which is `TD-111`'s recorded reason for leaving it
alone. Doing it here would have been a behaviour change smuggled into a
move.

## 7. Why This Solution Was Chosen

Because verbatim is verifiable. Diffing nineteen method bodies against
`HEAD` and finding zero differences beyond two mechanical edits is a
stronger statement about behaviour preservation than any test suite, and it
is only available if the move refuses to improve anything on the way.

## 8. Architectural Principles

`ADR-0103` — a collaborator is constructed once by the composition root,
declares only the dependencies it needs, is never DI-registered, and never
references the composition root or a sibling collaborator back. Both new
coordinators follow it exactly, and the event wiring stays where `ADR-0104`
puts it.

## 9. Benefits

`MainWindow` is 34% shorter and no longer implements project CRUD. The two
coordinators are testable through their own acceptance journeys. The seam
chosen matches a real domain boundary rather than a line count.

`TD-112`'s stale row was found and corrected here: `WP-D2` shipped in
Gate 0 with all nine sites migrated and zero `JsonException` swallows
remaining, and its row had never been updated.

## 10. Trade-offs

**`TD-109` stays Open.** `MainWindow` is still 1,042 lines and still the
largest file in the repository. What remains is the composition root proper
plus the shell services — `RenderCurrentModuleAsync`, `RefreshStatusBar`,
`RecordHistory`, layout save/restore, rehydration reporting — which are
genuinely the shell's own and are a different question from this row's
stated defect. Closing the row would have overstated what one extraction
achieved.

The row's stale 1,557-line figure was corrected in passing; it had grown by
20 lines by `WP-D1`. (`WP-Z1` later corrected it again to 1,052, after
`WP-A2` added to the file.)

## 11. Common Mistakes

**Improving during a move.** It destroys the one verification a move
uniquely admits.

**Trusting the compiler to prove wiring.** Event wiring that compiles can
still be disconnected. Unwiring each seam and watching real acceptance
journeys fail is the check that matters.

**Closing a debt row because it got better.** Partially resolved is a
status, and it is the honest one here.

## 12. Future Evolution

`TD-109`'s remainder — the shell services — is a separate question about
what a composition root should own, not a continuation of this extraction.
`TD-112` is closed. The 41 CRUD toast sites remain unrouted for
`TD-111`'s stated reason.

## 13. Key Takeaways

- A verbatim move is verifiable by diff. Give that up only for a reason.
- Split along a seam the domain already has, not along a line count.
- Mutation-check the *wiring*, not just the moved code: a coordinator
  nothing reaches is worse than the god object it came from.
- Correct the debt row's figures while you are in it, and correct them
  again when a later work package moves them.

## Related Documents

- `ADR-0103` — composition roots own collaborators
- `ADR-0104` — Desktop cross-collaborator communication
- `docs/governance/Quality/Technical Debt Register.md` — `TD-109`, `TD-111`,
  `TD-112`
- Commit `0a9e49b`
