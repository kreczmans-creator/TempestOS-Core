# WP-B1 — Pin the Two Encodings of Per-Kind Command Eligibility

## 1. Introduction

`WP-B1` (`25de7a3`) added one test class, `KindEligibilityInvariantTests`,
to hold two independently-maintained encodings of per-Kind command
eligibility consistent where they overlap. Test-only: 250 insertions in a
single file, no production change. It addressed `TD-107` (audit finding
`F-03`) without deciding whether the duplication should be unified — that
question was deferred to `WP-B2`, which later decided it should not be.

## 2. Purpose

To detect drift between two encodings that agreed only by coincidence, and
to do so before the drift caused a user-visible failure rather than after.

## 3. Background

The platform states per-Kind eligibility twice.

`CommandBinding.AppliesToKinds` is one term of what `ICommandRegistry.Evaluate`
computes, and so drives the Ribbon and the Command Palette.
`WorkspaceManager`'s `Register{Rename,Delete,Revise}Factory` Kind maps drive
`CanRename`/`CanRevise`/`CanDelete`, and so drive the Project Explorer's
context menu and inline rename, the Property Inspector's name field, and the
Object Editor's fields and save path.

`TD-77` Stage 5 moved the Ribbon from the second mechanism to the first,
which is what split them. At the time of this work package they agreed, and
nothing anywhere detected drift.

## 4. The Problem

Agreement by coincidence is not agreement. Two concrete failures were
available the moment the two sides diverged:

- A Kind a routed command claims, with no matching factory registered:
  `Evaluate` enables the button, and the dispatch then fails.
- A Kind the manager supports that no command claims: the Explorer offers
  an operation the Ribbon shows disabled.

Both are surface-level inconsistencies a contract test on either mechanism
alone would pass cleanly.

## 5. The Design

**The invariant is directional, not equality, and getting that right was
most of the work.**

`ManufacturingWorkspaceRegistration` registers *Documents'* rename command
for its own `WorkInstruction` Kind and *Verification's* for `Inspection` —
disclosed cross-work-package reuse from `WP 9.5A`. The manager's map is
therefore deliberately **wider** than any single descriptor's
`AppliesToKinds`, and a symmetric assertion would fail on correct code.

Two directions are asserted instead:

- every Kind a routed command claims has the matching factory registered;
- every Kind the manager supports is claimed by at least one command.

A third test pins the Manufacturing asymmetry itself, so a future reader
meets it as a decision rather than as an inconsistency to "correct".

Two anti-drift properties are built into the test: its own verb map is
asserted equal to `SurfaceCommandPolicy`'s sets, so it cannot silently
diverge from the policy it describes; and **no command Id suffix is parsed
anywhere**. Reading a command's meaning from its trailing word is precisely
the defect `TD-77` Stage 5 removed, and a test is not the place to
reintroduce it.

## 6. Alternatives Considered

**Assert set equality between the two encodings.** The obvious test, and
wrong: it fails on the correct Manufacturing configuration.

**Unify the two mechanisms in production.** Considered and explicitly
deferred — "`WorkspaceManager` is not redesigned and `WP-B2` is not
implemented; that unification is deferred and needs its own ADR." `WP-B2`
subsequently examined it and rejected unification, making
`KindEligibilityInvariantTests` the permanent control rather than a
stopgap.

**Derive the test's verb map from command Id suffixes.** Rejected on the
grounds that it re-creates a defect the platform had already removed.

## 7. Why This Solution Was Chosen

Because it was the cheapest thing that could actually fail. A directional
invariant costs one file, changes no production behaviour, and turns a
class of silent surface inconsistency into a build failure that names the
offending Kind. Committing to a production unification before understanding
whether the two mechanisms encode the same concept would have been the
larger and less reversible move — and `WP-B2` later established that they
do not.

## 8. Architectural Principles

The invariant sits beside the decision it enforces rather than in a general
"architecture tests" bucket. It documents the Manufacturing asymmetry as a
decision, in the test that would otherwise appear to contradict it — the
same principle `WP-H` later applied across five more invariants.

## 9. Benefits

Drift in either direction now fails the build and names the Kind. The
Manufacturing asymmetry is recorded where a future contributor will meet
it. And the question of unification was left genuinely open for `WP-B2`
rather than foreclosed by a test that assumed the answer.

## 10. Trade-offs

An invariant test is a consistency control, not a single source of truth: a
contributor adding a discipline must still remember both encodings. `WP-B2`
accepted that cost explicitly and bounded it — the test fails at build time
in either direction, and a runtime divergence degrades to an honest
`CommandResult.Failure`, never a crash.

## 11. Common Mistakes

**Reaching for equality when the domain is directional.** The symmetric
assertion is shorter, reads better, and is wrong.

**Parsing meaning out of identifiers in a test.** Tests are production
code's peer, and a shortcut forbidden in one is forbidden in the other.

**Letting a test's own model drift from the thing it models.** Asserting
the test's verb map equal to `SurfaceCommandPolicy`'s sets is what stops
that.

## 12. Future Evolution

`WP-B2` (`ADR-0118`) decided the underlying question: the two mechanisms
answer different questions, unification is rejected, and this test is the
permanent consistency control. What would reopen it is a third consumer of
per-Kind eligibility needing to ask both questions at once, or a discipline
needing the two sides to disagree in a way the invariant forbids.

## 13. Key Takeaways

- Before unifying two encodings, test that they agree. The test is cheap,
  and it may reveal that they should not be unified at all.
- A deliberate asymmetry belongs *inside* the test that would otherwise
  appear to contradict it.
- Two mutations, two killed — a binding claiming a factory-less Kind, and a
  factory registered for a Kind no command claims. An invariant test that
  has not been made to fail is not yet known to work.

## Related Documents

- `ADR-0118` — Kind eligibility is two mechanisms held together by one invariant
- `docs/governance/Quality/Technical Debt Register.md` — `TD-107`
- `WP-B2` retrospective — the decision this test's premise was left open for
- Commit `25de7a3`
