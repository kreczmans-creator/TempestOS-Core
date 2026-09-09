# WP-Z4 (Stages 4–14) — TD-119 Desktop Test Synchronisation Remediation

## 1. Introduction

`WP-Z4` began as v0.14.0 release preparation. Stages 1–3 produced the
release artefacts and opened PR #5. Stages 4–14 are a different piece of
work entirely, and this retrospective covers those: the investigation,
classification, remediation and verification of `TD-119`, the systemic
fixed-delay synchronisation weakness in `Tempest.Desktop.Tests`.

It is **test-only work**. Across eleven stages and four commits, zero
`src/` files changed and `DesktopTestHelpers.cs` was never touched. The
outcome is 52 fixed synchronisation waits reduced to 1, verified on both
CI events at one SHA.

## 2. Purpose

To make a green CI result mean something.

That is the whole of it. When Stage 3 took PR #5 to its first `CI Gate`,
the gate failed. It then passed on the next commit, failed on the one
after, and passed on the one after that — always with the two event runs
disagreeing at an identical SHA. A release cannot be certified against a
gate that flips, and re-running until it happened to go green would have
produced a green tick that carried no information.

## 3. Background

`TD-46` (`WP 11.4A`) had already named this mechanism at `v0.11.0`, in
`ObjectEditorViewTests`. `WP 13.12.8` diagnosed it again at `v0.13.0` on
the tag-triggered `release.yml` run, and `WP 13.12.9` fixed that one
method — while explicitly declining, in its §6, to generalise the remedy
to what it then counted as 28 remaining sites. That decision was
defensible at the time: it kept the release record's diff minimal. It also
left the mechanism in place, and it is why this Work Package exists.

## 4. The Problem

A Desktop acceptance test raises a UI event and then sleeps:

```csharp
await ClickAsync(tasks, "New Task");
await AnswerDialogAsync(window, "Overdue work");   // ends in Task.Delay(50)
await window.RenderCurrentModuleAsync();

await ClickAsync(TasksSurfaceOf(window), "Due date");
```

`RaiseEvent` returns as soon as the `async void` handler reaches its first
`await`. The command dispatch, the persistence write and the workspace
refresh all complete on a continuation the test has no task for. The
delay is a guess, not a join, and the state is sampled once, after the
guess.

**Six CI occurrences resulted, across seven distinct tests.**

| # | SHA / release | Failing test | Signature |
|---|---|---|---|
| 1 | `v0.11.0` | `ObjectEditorViewTests.VerificationResultSection_RecordPass_…` | fixed by `WP 11.4A` |
| 2 | `v0.13.0` | `ObjectEditorViewTests.Save_RevisedContent_…` | fixed by `WP 13.12.9` |
| 3 | `384e47f` | `Journey_ProposeADecision_…` (Debug) **and** `MechanicalDuplicate_…` (Release) | `collection was empty`; `Expected 13, Actual 12` |
| 4 | `b09a620` | `Ribbon_RequirementsDeleteGroup_…` | `Collection: []` |
| 5 | `a1e64d2` | `Journey_CreateATask_…` | `collection was empty` |
| 6 | `e7357b6` | `ADueDateSetThroughTheDialog_…` | `No 'Due date' button on this surface` |

Five of the seven tests fall within `WP-Z4`. Earlier reporting in this
programme said "six occurrences across five distinct tests", conflating
the two figures; six is the number of CI incidents, seven the number of
tests.

The `e7357b6` signature is worth its own note. It is not an assertion
reading empty — it is `ClickAsync`'s guard failing to find a button,
because "Due date" is a per-task-row control that does not exist until the
row has rendered. The same race, one step earlier, wearing a message that
looks like a missing feature.

## 5. The Design

The remedy is the bounded, condition-based poll `TD-46` established and
`WP 13.12.9` reused, applied at scale:

```csharp
var deadline = DateTime.UtcNow.AddSeconds(2);
while (!(condition) && DateTime.UtcNow < deadline)
    await Task.Delay(10);

<original assertion, unchanged>;
```

Four properties are load-bearing. The state is **re-read every
iteration** — the original bug was sampling once, not sleeping too
briefly. The wait is **bounded**, so a genuinely broken path fails rather
than hangs. The **assertion is untouched**, so the loop decides only
*when* to assert and a real failure still fails on its own message. And
the loop **only observes** — it never retries the action under test.

Three shapes were needed, and the third was the discovery:

- **`RenderUntilAsync`** — re-renders until a condition holds. Rendering
  is a read (`ProjectWorkspaceView.RefreshAsync` lists and shows), so it
  cannot manufacture the state it waits for.
- **`SurfaceOrNull`** — lets the *loop* observe a momentarily unresolvable
  logical tree without throwing, while every *assertion* still resolves
  the surface unguarded, so a tree that never settles fails loudly.
- **`ClickWhenPresentAsync`** — waits for a target that is legitimately
  produced asynchronously. `ClickAsync` is deliberately unchanged and
  still fails at once for a button that ought to be present already.

Three sites needed no poll at all, because the production path completes
before the raising call returns: `RibbonView` refuses an unavailable
command and raises `ActionCompleted` before its first `await`;
`ProjectExplorerView` wires `_filter.PropertyChanged` to a synchronous
`ApplyFilter`; and `LayOutAsync` now drains the dispatcher with
`Dispatcher.UIThread.RunJobs()`.

## 6. Alternatives Considered

**Re-run CI until both events agree.** Rejected. It produces a green tick
that says nothing about the next run, and it is precisely the habit this
Work Package existed to break.

**Targeted remediation of demonstrated failures only.** This was tried,
at Stage 8, and it is the most valuable negative result here — see §11.

**A shared helper in `DesktopTestHelpers.cs`.** Rejected. Nine test files
consume that class; changing it would alter unrelated tests. Each file
got its own private helpers instead, at the cost of some duplication.

**Making `ClickAsync` globally wait for any button.** Rejected. It would
convert every genuine "this button should already be here" failure into a
two-second timeout, weakening thirty-odd call sites to fix six.

## 7. Why This Solution Was Chosen

Because it removes the mechanism rather than widening the guess, and
because the repository had already proved it twice — in the same file, by
`TD-46` and `WP 13.12.9`. Nothing here is novel; it is an existing remedy
applied to the sites its authors declined to reach.

## 8. Architectural Principles

**Tests observe; they do not orchestrate.** Every condition polled is
state the product already exposes — repository re-reads, view properties,
event-collected lists, `Dispatcher.UIThread.RunJobs()`. No production type
gained a test-only hook, event or awaitable. That constraint is what kept
`src/` at zero changes, and it was the right constraint: a test seam added
to production to make a test wait is a design smell, not a fix.

**Fire-and-forget UI dispatch is correct production design.** A UI event
handler has nowhere to return a task to. `ADR-0119`/`WP-Z2` already
settled that the Desktop owns its own async lifecycle. The defect was
never in the product.

## 9. Benefits

- 52 fixed waits → 1. 59 `Task.Delay` occurrences remain, 58 legitimate.
- The first commit in the sequence where **both** CI events agree at one
  SHA: `f0fcad6`, push `33653617714` and pull_request `33653636667`, all
  four checks green in each.
- Failures now fail for the reason they name. `e7357b6`'s misleading
  "No 'Due date' button" message cannot recur as a timing artefact.
- A third legitimate pattern is documented, not just used: the
  attempt-bounded `for` loop, already present six times.

## 10. Trade-offs

**A two-second deadline absorbs a performance regression.** If a create
starts taking 1.5s instead of 50ms, these tests go green and say nothing.
Nothing in the suite measures latency. This is the accepted cost of the
pattern and it applies equally to the pre-existing polls.

**One site is deliberately retained.**
`WorkflowInteractionTests.cs:335` asserts that a declined delete changed
nothing. The assertions are negative and `CommandOutcome.Cancelled` raises
no event, so there is nothing to poll for. Waiting longer only strengthens
it, so it cannot produce a false pass — but it is debt, and `TD-119`
stays **Partially resolved** rather than Resolved because of it.

**Duplication across three acceptance files.** Each carries its own
`RenderUntilAsync`, `ClickWhenPresentAsync` and `SurfaceOrNull`.

## 11. Common Mistakes

**Fixing only what failed.** Stage 8 converted the three demonstrated
races, correctly and with mutation evidence. The flake immediately
appeared at `a1e64d2` in a structurally identical test in a file Stage 8
had not touched, and again at `e7357b6` in the same file. Targeted
remediation of a systemic weakness chases it; it does not remove it. This
is the single most useful thing this Work Package learned.

**Classifying without deriving.** This programme made two classification
errors, both caught by the test suite rather than by reasoning, and both
are recorded in `TD-119` rather than rewritten away:

- The row originally stated "3 legitimate / 61 fixed waits". The true
  split was **10 / 54**. The classification had been derived from the two
  files already open and assumed across the other fourteen.
- The Stage 10 audit ruled three Command Palette sites needed no wait,
  reasoning that `_query.TextChanged` reaches a synchronous `ApplyFilter`.
  `TextBox` raises `TextChanged` on a *later dispatcher pass*, unlike
  `AvaloniaObject.PropertyChanged` — a generalisation from one Avalonia
  mechanism to a different one. Four tests failed on the first Stage 11
  run.

A third under-join was caught the same way: `Save_RenamedName_…` polled
the repository but not `editor.IsDirty`, which clears on a later
continuation — a poll condition weaker than the assertions following it,
which is exactly the defect class this work removes.

**Trusting one green run.** Four consecutive commits had a green run. All
four were failing on the other event at the same SHA.

## 12. Future Evolution

Give `RibbonDelete_ConfirmationDeclined_…` an observable signal for the
cancelled path and `TD-119` can close. `TD-120` — the suite never deleting
its isolated persistence roots — is the other open thread: it took the
verification container to 0 bytes free mid-run at Stage 13. Longer term,
the three duplicated helper sets could consolidate once the pattern has
settled, which is a decision better made after it has survived a few
releases than during one.

## 13. Key Takeaways

1. **A green CI run is evidence only if a red one was possible.** The
   standard adopted here — both `push` and `pull_request` green at one
   identical SHA — exists because a single green run on this suite had
   been demonstrated, four times over, to mean nothing.
2. **Mutation testing is what makes a wait trustworthy.** Suppressing each
   action reproduced the original CI message verbatim; forcing stale
   observations into the polls still passed; setting the deadlines to zero
   reproduced the `e7357b6` failure. Without those, a green suite proves
   only that the machine was fast.
3. **Systemic weaknesses need systemic fixes.** Stage 8 proved the method
   and Stage 9 proved the scope was wrong.
4. **Disclose the errors.** Two misclassifications and an under-join are
   recorded in the register with their causes. A remediation record that
   only lists successes teaches nothing about how the mistakes happened.

## Architectural Debt Assessment

- **`TD-119`** — moved Open → **Partially resolved**. 1 fixed wait
  remains, `WorkflowInteractionTests.cs:335`, deliberately retained.
- **`TD-120`** — **new**. The Desktop suite never deletes the isolated
  persistence roots it creates; ≈9,000 directories and ≈12 GB observed at
  Stage 13, taking the container to 0 bytes free mid-verification. Test
  cleanup debt, not a product defect, not a CI failure, not a merge
  blocker.
- No `src/` change, so no production debt was created or discharged.

## Related Documents

- `docs/governance/Quality/Technical Debt Register.md` — `TD-46`,
  `TD-119`, `TD-120`
- `docs/releases/v0.14.0/Engineering Release Report.md` §4, §6, §7
- `docs/academy/03 Work Packages/WP13.12.8-v0.13.0-release-test-failure-investigation.md`
- `docs/academy/03 Work Packages/WP13.12.9-desktop-async-test-determinism-remediation.md`
- `docs/adr/ADR-0119-ui-thread-marshalling-is-the-desktops-job-tempest-app-stays-dispatcher-free.md`
