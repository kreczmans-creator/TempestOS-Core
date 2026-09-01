# WP-B2 — Kind Eligibility Is Two Mechanisms, One Invariant

## 1. Introduction

`WP-B2` (`464bdff`) is documentary. It produced `ADR-0118` and closed
`TD-107` by **deciding** the question rather than by changing anything: the
two encodings of per-Kind eligibility are not a duplication to unify, and
`KindEligibilityInvariantTests` — installed by `WP-B1` — is the permanent
consistency control rather than a stopgap. No production code, no test
changes.

## 2. Purpose

To determine whether `F-03`'s proposed unification of `AppliesToKinds` and
the `WorkspaceManager` factory maps was actually sound, and to record the
answer where a future contributor will meet it.

## 3. Background

`F-03` read the two encodings as a single fact stated twice and proposed
unifying them. `WP-B1` installed a directional invariant test instead and
deferred the unification question here, explicitly noting it "needs its own
ADR".

## 4. The Problem

The audit that preceded this work package concluded that **`F-03`'s premise
does not survive contact with the code.** The two encodings do not state
the same fact:

- `CanRename`/`CanRevise`/`CanDelete` are `ContainsKey` over the factory
  maps, and answer *"can a command be constructed for this Kind?"*
- `AppliesToKinds` is one of five terms `Evaluate` computes, and answers
  *"is this command available for this selection?"*

A third question — *"does this Kind support this user operation?"* — is
represented nowhere. Naming it would add a mechanism rather than remove one.

## 5. The Design

An ADR, a register amendment, and nothing else. Three findings decided it.

**The overlap is 19 commands of 74.** Fifty-three binding sites pass a Kind
list; eighteen have a factory counterpart. The other thirty-five are status
transitions, creates, copies, duplicates, bulk operations and
`mechanical.set-bom-line`, which have no manager question to ask. A
unification would explain the eighteen and leave the thirty-five untouched.

**The Manufacturing asymmetry is load-bearing.**
`ManufacturingWorkspaceRegistration` registers Documents' and Verification's
commands for `WorkInstruction` and `Inspection` (`WP 9.5A`), so the manager
map is deliberately wider than any single descriptor. `documents.rename`
correctly does *not* claim `WorkInstruction` — a Documents ribbon button
must not offer to rename a manufacturing work instruction — while the
Explorer's discipline-agnostic context menu renames it happily. Deriving
one side from the other forces a choice between putting the command on the
wrong tab and stopping the Explorer renaming a `WorkInstruction` at all.

**Registration order independently blocks the obvious derivation.** The
composer registers Documents' descriptors *before* Manufacturing's
factories, so a binding reading the manager at its own registration time
would see an incomplete map.

`CalculationTemplate` (synthetic, no domain object) and Requirements (no
rename concept — a Requirement's editable field is its Statement) are
excluded consistently on both sides.

## 6. Alternatives Considered

Three unification shapes, each rejected in `ADR-0118` for a specific
reason:

**Bindings derive from the manager.** Blocked by registration order and by
the Manufacturing asymmetry. Not blocked by layering — `AppliesToKinds` is
data supplied at construction, and construction happens in `Tempest.App`.

**The manager derives from bindings.** An inversion: `CanRename("Part")` is
asked by the Object Editor about whether a *text field* is editable, and
making that depend on whether a *Ribbon command exists* couples a field's
enablement to the command surface. The manager would also still need its
factory map to construct anything, so it would gain a consultation without
losing a source.

**A shared lower-level capability registry.** To subsume `AppliesToKinds`
it must model the thirty-five non-routed uses; to subsume the factory maps
it must carry the construction delegates. Having done both, it *is* the two
existing mechanisms under a new name.

## 7. Why This Solution Was Chosen

Because the honest answer to "should these be unified?" turned out to be
no, and recording that is worth more than a unification that would have
added a third mechanism while explaining nineteen of seventy-four commands.

`KindEligibilityInvariantTests` is recorded as the permanent control on its
merits: two mechanisms that must stay consistent where they overlap and
distinct everywhere else are exactly what an invariant test is for. Runtime
divergence degrades to an honest `CommandResult.Failure`, never a crash.

## 8. Architectural Principles

`ADR-0070` (an unavailable command is disabled with its reason),
`ADR-0096`/`ADR-0097` (rename and revise dispatch through
`IWorkspaceManager`) and `ADR-0023` (dependencies flow downward) all bear
on the analysis, and `ADR-0118` builds on each.

The governing principle the decision rests on: a single source of truth
requires a single question. There are two questions here, asked by
different callers for different purposes.

## 9. Benefits

The Explorer, Property Inspector and Object Editor keep asking one question
— *can this object be renamed* — without knowing which discipline
registered the command. The Ribbon and Palette keep asking a different one
— *is this command available for what is selected* — without knowing how
the command is constructed. Neither surface acquires a dependency on the
other's mechanism, and the Manufacturing reuse keeps working in both.

## 10. Trade-offs

The same Kind list is written twice for nineteen commands, and a
contributor adding a discipline must remember both. Bounded by the
invariant test, which fails at build time in either direction and names the
Kind; and bounded at runtime, where a disagreement produces
`CommandResult.Failure("No rename capability is registered for Kind 'X'.")`
— an honest refusal, so the worst observable outcome is a button that is
enabled and then declines.

## 11. Common Mistakes

**Reading agreement as identity.** The two encodings agreed; that did not
make them the same fact.

**Collapsing concepts because their current sets coincide.** The
distinction between "a factory is registered", "this command is available
here" and "this Kind supports this operation" is the whole finding.

**Treating an invariant test as a placeholder for a real fix.** Sometimes
it is the real fix.

## 12. Future Evolution

`ADR-0118` discloses two observations rather than actioning them: the
`CanRename`/`CanRevise`/`CanDelete` names read as capability questions
while documenting factory registration, and
`CalculationsWorkspaceRegistration.SupportedKinds` is public and wider
(three Kinds) than what any command can act on (two). Renaming the first
group to `HasRenameFactory` and siblings would be honest and is a
production API change outside a documentary work package's scope.

What would reopen the decision: a third consumer of per-Kind eligibility
needing to ask both questions at once, or a discipline needing the two
sides to disagree in a way the invariant forbids. Neither exists today.

## 13. Key Takeaways

- "These two things look the same" is a hypothesis. Trace every consumer
  before unifying.
- An asymmetry that a symmetric design would erase is usually load-bearing.
- A documentary work package that changes no code can still close a debt
  row — if it decides the question the row was open on.
- Rejected alternatives are the substance of an ADR. Three were examined
  here and each failed for a different, specific reason.

## Related Documents

- `ADR-0118` — the decision this work package produced
- `docs/governance/Quality/Technical Debt Register.md` — `TD-107`
- `WP-B1` retrospective — the invariant this makes permanent
- Commit `464bdff`
