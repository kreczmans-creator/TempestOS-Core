# WP-D1 — One Implementation of the Desktop Report-Then-Refresh Tail

## 1. Introduction

`WP-D1` (`0ced2c5`) consolidated seven hand-written copies of the same
four-step tail — status bar, toast, command history, gated refresh — into
one collaborator, `Composition/ActionOutcomeReporter.cs`. It closed
`TD-111`, corrected that row's own wording while doing so, and preserved
two behaviours a naive consolidation would have broken.

## 2. Purpose

To give `ActionOutcome` — a type introduced to carry exactly the two facts
this tail needs — a single implementation of the tail itself.

## 3. Background

Seven places wrote the same four steps by hand: set the status bar, raise a
toast whose severity derives from success, record in Command History, and
refresh dependent surfaces, gated so a refused action does not rebuild the
world.

**`TD-111`'s own wording was wrong, and is corrected in closing it.**
`ActionOutcome` did not lack a consumer — six views raise it and five
subscribers read it. What had no single implementation was the *tail*, and
that is what was consolidated. The type is untouched.

## 4. The Problem

Seven copies of a four-step sequence is seven opportunities for one of the
steps to be omitted, reordered, or gated on the wrong condition — and,
critically, no test can see the difference. Seven copies and one shared
implementation behave identically, which is what made audit finding `F-08`
invisible for as long as it was.

## 5. The Design

`ActionOutcomeReporter`, built once by `MainWindow`. Six call sites, seven
tails migrated: Ribbon, Explorer, Inspector and Object Editor
`ActionCompleted`; drag-and-drop Move; Undo and Redo.

**Two things the naive version would have broken, both preserved
deliberately:**

*The refresh set is per-caller, not common.* The four `ActionCompleted`
sites refresh Inspector+Cockpit, Explorer+Cockpit,
Explorer+Inspector+Cockpit and Explorer+Cockpit respectively, and the
differences are correct — the Explorer does not reload itself after its own
action, having just done so. A reporter that picked one set would be a
behaviour change wearing a cleanup's clothes. The refresh therefore arrives
as a delegate, and the reporter decides only *whether* to run it.

*The gate reads `WorkspaceChanged`, never `Succeeded`.*
`ObjectEditorView`'s Owner/Priority save reports a failure that did change
the workspace: the Owner half commits before the Priority half is refused.
A success-gated reporter leaves the Explorer, Inspector and Cockpit showing
values that are no longer true.

History semantics are unchanged. Drag-and-drop Move still records nothing —
carried as an explicit no-history entry point rather than normalised away,
because uniformity is not a reason to change what the history contains.
`RecordHistory`'s own success heuristic (`AT-21`) is untouched, and the
reporter decides no history policy of its own.

## 6. Alternatives Considered

**A common refresh set.** Rejected: see above. The differences between the
four sets are correct.

**Gate on `Succeeded`.** Rejected on a concrete counter-example, not on
principle.

**Give the reporter a "no toast" mode for the Command Palette.** Rejected —
the Palette was approved as a deliberate unmigrated survivor rather than
have the shared implementation grow a mode for one caller.

**Normalise drag-and-drop Move to record history.** Rejected: it would
change what the command history contains, for consistency's sake.

## 7. Why This Solution Was Chosen

Because the delegate-shaped refresh is what makes the consolidation
behaviour-preserving. The reporter owns the *sequence* and the *gate*, which
is what was duplicated; it owns none of the per-caller *content*, which was
never duplicated in the first place.

The deliberate survivors matter equally. Four call sites stayed unmigrated,
each for a stated reason: the Command Palette reports through
`RefreshStatusBar` and raises no toast; the Digital Thread graph sets the
status bar alone; `CommandUnavailable` reports a third severity; and
`MainWindow`'s project/task/risk CRUD is a different shape entirely and
belongs to `TD-109`.

## 8. Architectural Principles

`ADR-0103`'s collaborator pattern — constructed once by the composition
root, declaring only what it needs, never referencing a sibling. `TD-58`'s
outcome-gated refresh architecture is the rule the gate implements, and this
work package is what gave it one implementation instead of seven.

## 9. Benefits

One tail, correct once. The `WorkspaceChanged` gate is now enforced rather
than repeated, so the Owner/Priority case cannot regress in one of seven
places. `TD-111`'s misstatement about `ActionOutcome` lacking a consumer is
corrected in the register.

## 10. Trade-offs

A source-level test was needed, because "this logic exists in one place" is
not observable at runtime. Four unmigrated survivors remain, each named — a
consolidation that reaches six of ten call sites and says which four it did
not, and why, is more honest than one that forces all ten into a shape that
fits six.

## 11. Common Mistakes

**Unifying the variable part along with the duplicated part.** The refresh
sets differ *correctly*; folding them into one would have been a regression
disguised as cleanup.

**Gating a refresh on success.** A failed action can still have changed the
workspace. The Owner/Priority save is the proof.

**Making the shared implementation absorb every caller.** The Palette
needed a mode nobody else wanted; it stayed a survivor instead.

## 12. Future Evolution

`MainWindow`'s 41 CRUD toast sites were left for `TD-109`, and `WP-G`
subsequently moved that CRUD out of `MainWindow` while explicitly *not*
routing it through the reporter — that family carries no `ActionOutcome`
and refreshes no dependent surfaces, which is `TD-111`'s recorded reason
for leaving it alone.

**Discovered, not fixed:** the `failureFallback` argument these call sites
have always passed is unreachable — `CommandResult.Failure` rejects a blank
message, so `Message` is never null on a failure. Carried forward unchanged.

## 13. Key Takeaways

- Consolidate the sequence, not the content. A delegate parameter is often
  the line between them.
- A failed action that changed state is the case that distinguishes a
  correct gate from a plausible one.
- Name the callers you did not migrate, with reasons. Four survivors with
  stated reasons beat ten callers forced into one shape.
- Four mutations, four killed: gating on `Succeeded`, recording history on
  the no-history path, inverting toast severity, and re-adding an inline tail.

## Related Documents

- `ADR-0103` — composition roots own collaborators
- `docs/governance/Quality/Technical Debt Register.md` — `TD-111`, `TD-58`,
  `AT-21`, `TD-109`
- `WP-G` retrospective — the CRUD family, moved but deliberately not routed
- Commit `0ced2c5`
