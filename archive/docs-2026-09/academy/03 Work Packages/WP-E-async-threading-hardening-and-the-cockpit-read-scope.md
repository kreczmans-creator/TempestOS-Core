# WP-E — Async/Threading Hardening and the Cockpit Read Scope

## 1. Introduction

`WP-E` (`e4bc3ee`) removed two of the blocking calls `TD-108` tracks — on
their merits, rather than by sweeping the file — and left sixty-one in
place with the register corrected to say accurately why. Its larger
outcome was that the Cockpit's repeated persistence reads were eliminated:
one Requirements refresh fell from **1,140 reads to 104**. It also
surfaced a pre-existing defect that became `TD-117`.

## 2. Purpose

To triage every blocking `.GetAwaiter().GetResult()` call by execution
context and architectural necessity, remove UI-thread blocking where
removal was warranted, and preserve synchronous semantics where they are
genuinely required.

## 3. Background

`TD-108` recorded "77 blocking `.GetAwaiter().GetResult()` calls in
`Tempest.Desktop`". Re-derived, the figures were wrong in three ways: 69
textual occurrences of which 63 executable, not 77; 28 Desktop / 35 App /
0 Core, not "in `Tempest.Desktop`"; and its cited line numbers predated
`WP-G`. The register now records all of this. (`WP-Z1` subsequently
corrected the per-assembly split again, which this work package had
recorded incorrectly.)

The audit also established what the register had not: **36 of the 63 block
on an already-completed `Task`** — `Task.FromResult` from the in-memory
object and relationship repositories, the only implementations there are —
and therefore do not wait at all. `Tempest.Core` and `Tempest.App` use
`ConfigureAwait(false)` on every awaited statement but one, and neither
references Avalonia, so **no awaited operation can require the UI
dispatcher and no blocking call can deadlock.** The problem was latency,
not correctness.

## 4. The Problem

Two genuine defects among the sixty-three.

**Toggle Favourite** blocked the UI thread on a real file write, on an
interactive gesture (Ctrl+D, or the Explorer's context menu).

**The Cockpit was worse, and the blocking call was not the cause.** Every
discipline read-model exposed its data as expression-bodied properties over
an uncached read, so one `CockpitView.Refresh()` re-read the same
persistence-backed leaf eight or more times — and
`RequirementValidationService.ValidateAsync`, itself `O(N)` in stored
requirements, ran once per requirement inside each of those evaluations.

## 5. The Design

**Toggle Favourite** became `ToggleFavouriteAsync` with a synchronous
fire-and-forget wrapper for the two callback shapes — the shape
`NavigateToObject` already established in the same file.
`ConfigureAwait(true)` is load-bearing: status bar, toast, history and the
Undo/Redo record all follow the save and all touch Avalonia state, so the
ordering the user sees is unchanged.

**`CockpitReadScope`** bounds a render pass. Outside a scope every cell
reads live, exactly as before — which is what keeps every existing caller
and acceptance test honest. Inside one, the first read wins and the rest of
the pass consumes it. `CockpitView.Refresh()` opens exactly one scope.
Applied to the four I/O-backed read models (Requirements, Verification,
Calculations, Manufacturing); Documents and Mechanical read only in-memory
and were deliberately excluded.

## 6. Alternatives Considered

**Async conversion of the Cockpit read surface.** Considered and rejected:
C# properties cannot be `async`, so it would mean reshaping a two-assembly
public contract — and it would not have removed a single repeated read.
Recorded as `TD-118`, deferred with its own revisit trigger.

**Blanket removal of the remaining blocking calls.** Rejected: 36 never
wait, the startup and shutdown ones sit behind Avalonia's own `void`
framework contract with no window yet constructed, and the
`IWorkspaceViewFactory.Create` survivors are a contract question (`AT-26`).

**`Task.Run`.** Rejected outright — hiding synchronous blocking behind a
thread is not removing it.

**A cache rather than a scope.** Rejected: a cache outliving the render
pass would make a live read-model a stale one, and every existing caller
relies on reading a property immediately after mutating the workspace.

## 7. Why This Solution Was Chosen

Because the audit distinguished a latency problem from a correctness one,
and the fix follows that distinction. Nothing here can deadlock, so nothing
needed to be made async for safety. What needed fixing was repetition, and
memoisation removes repetition without touching a public API.

The scope's boundary is what makes it behaviour-preserving: outside it,
nothing changed at all.

## 8. Architectural Principles

The `ConfigureAwait(false)` discipline in `Tempest.Core`/`Tempest.App` is
treated as correct and left intact — the audit's finding that it makes
deadlock impossible is the reason the fix could be so small.

`ADR-0103`'s collaborator pattern governs `ActionOutcomeReporter`'s
neighbours; `CockpitReadScope` is a `Tempest.App` primitive owned by
`EngineeringCockpit`, its composition root.

## 9. Benefits

An interactive gesture no longer blocks the UI thread on a file write. One
Cockpit refresh performs 104 persistence reads where it performed 1,140,
and the per-requirement validation pass runs once instead of about eight
times. Cards within a refresh are now internally consistent — a KPI total
and the coverage percentage beside it come from the same snapshot. And
`TD-108` states the truth about the other sixty-one calls.

## 10. Trade-offs

**A residual `O(N²)` remains and was not fixed.** One validation pass is
itself quadratic, because `RequirementValidationService.ValidateAsync`
calls `ListAsync` for its duplicate-identifier check, so validating one
requirement reads every requirement. Fixing that means changing a
`Tempest.Core` validation service — a separate decision. It is pinned by a
test named for exactly that, so it is a stated fact rather than something
to be rediscovered.

Inside a scope, a refresh raised from a background thread completes
asynchronously; nothing in the product depends on it being synchronous.

## 11. Common Mistakes

**Assuming a blocking call is a deadlock risk.** Here none of them are, and
establishing that is what kept the work package small.

**Async-ifying a repetition problem.** `await`ing a read performed eight
times still performs it eight times.

**Writing a test that asserts what you hoped rather than what is.** The
first "growth is linear" test *failed* — 36 reads at N=2, 336 at N=8 — and
was replaced by two honest ones: the repetition elimination, asserted
precisely, and the residual quadratic, pinned as a recorded fact.

## 12. Future Evolution

`TD-118` records the deferred async conversion of the Cockpit read surface,
with the revisit trigger "persistence becomes slow or remote". `AT-26`
records the synchronous `IWorkspaceViewFactory.Create` contract as a
deliberate survivor. `TD-108` remains open, narrowed.

**`TD-117` was discovered here and fixed by `WP-Z2`**:
`UndoRedoStack` raises `Changed` on whatever thread an undone action
resumed on, and the Quick Access Toolbar refresh subscribed to it touches
Avalonia state. Pre-existing and untouched by this work package — found
only because this one wrote the first test to drive `Stack.UndoAsync()`
with a coordinator attached.

## 13. Key Takeaways

- Triage before converting. Thirty-six of sixty-three calls waited on
  nothing at all.
- Identify the actual cause. The Cockpit's problem was uncached properties;
  the blocking call was a symptom.
- Bound a memoisation by an explicit scope, and every existing caller keeps
  the semantics it relied on.
- A test that fails against your own hypothesis has done its job. Rewrite
  the hypothesis, not the test.
- Eight mutations run, eight killed — after one survivor exposed genuinely
  redundant code, which was removed rather than defended.

## Related Documents

- `docs/governance/Quality/Technical Debt Register.md` — `TD-108`, `TD-117`,
  `TD-118`, `AT-26`
- `WP-Z2` retrospective — `TD-117`, fixed
- `ADR-0103` — composition roots own collaborators
- Commit `e4bc3ee`
