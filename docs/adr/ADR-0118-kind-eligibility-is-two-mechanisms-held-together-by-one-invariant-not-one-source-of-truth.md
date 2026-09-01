# ADR-0118: Kind Eligibility Is Two Mechanisms Held Together by One Invariant, Not One Source of Truth

## Status

Accepted — `WP-B2` (Unify Kind Eligibility), 2026-09-01. Closes `TD-107` (audit finding `F-03`). Builds on `ADR-0070` (an unavailable command is disabled with its reason, not hidden), `ADR-0096`/`ADR-0097` (rename and revise dispatch through `IWorkspaceManager`), `ADR-0023` (dependencies flow downward) and `WP-B1`, which installed the invariant this decision makes permanent.

**This ADR authorises no code change.** It records why the duplication `F-03` names is not a defect, and closes the finding as decided rather than deferred.

## Context

Per-Kind eligibility is encoded in two places.

`WorkspaceManager` holds three dictionaries — `_renameFactories`, `_reviseFactories`, `_deleteFactories` — keyed by Kind, each holding the delegate that constructs that Kind's own command. `CanRename`/`CanRevise`/`CanDelete` are `ContainsKey` over them, and the interface says so in as many words: *"Gets whether a delete factory is registered."*

`CommandBinding.AppliesToKinds` records the Kinds a command acts on, and is one term of what `ICommandRegistry.Evaluate` computes.

The two agree today: rename 17 Kinds, revise 18, delete 20, on both sides. `F-03` read that agreement as a single fact stated twice and proposed unifying it. `WP-B1` installed a directional invariant test instead, and `WP-B2` was deferred pending this decision.

The audit that preceded this ADR traced every consumer of both mechanisms and found that the agreement is real but the identity is not.

## Decision

**1. The two mechanisms encode different concepts, and the difference is not cosmetic.**

Three questions are involved, and only two of them have a representation in the code:

- *"Is a factory registered for this Kind?"* — `CanRename`/`CanRevise`/`CanDelete`. A construction check. One term.
- *"Is this command available for this Kind?"* — `Evaluate`, of which `AppliesToKinds` is one of five terms, beside `CommandContextRequirement`, `CommandBinding.IsInvocable`, declared `Parameters` and the descriptor's own `CanExecute`.
- *"Does this Kind support this user operation?"* — **nothing represents this.** It is an inference a reader makes from the first. Naming it would be a third mechanism, not a unification of two.

A single source of truth requires one question. There are two, and they are asked by different callers for different purposes.

**2. The overlap is 19 commands out of 74, and unification would explain only those.**

Fifty-three binding sites pass a Kind list. Eighteen of them are the routed rename/edit/delete `appliesToKinds:` arguments that have a factory counterpart. The remaining thirty-five — every status transition, every create, copy, duplicate, bulk operation and `mechanical.set-bom-line` — carry `AppliesToKinds` with no factory behind them and no manager question to ask. A mechanism unifying the eighteen would leave the thirty-five exactly as they are, having added a concept rather than removed one.

**3. The Manufacturing asymmetry is load-bearing, and unification would erase it.**

`ManufacturingWorkspaceRegistration` registers *Documents'* rename/delete/revise commands for its own `WorkInstruction` Kind, and *Verification's* for `Inspection` — disclosed cross-Work-Package reuse (`WP 9.5A`). So the manager's rename map is genuinely **wider** than any single descriptor's `AppliesToKinds`, and correctly so:

- `documents.rename` does **not** claim `WorkInstruction`. A Documents ribbon button must not offer to rename a manufacturing work instruction.
- `manufacturing.rename` **does** claim it, which is what makes the Ribbon offer it in the right place.
- The manager renames it either way, because the Project Explorer's context menu is discipline-agnostic and asks only whether the object can be renamed at all.

Deriving one side from the other forces a choice between widening `documents.rename` — wrong, it would put the command on the wrong tab — and narrowing the manager, which would stop the Explorer renaming a `WorkInstruction` at all. **The asymmetry is the correct answer to two different questions, not drift between two answers to one.**

**4. Two further asymmetries are equally deliberate.**

`CalculationTemplate` is a synthetic Kind with no `EngineeringDomainContext` object behind it. It is excluded from the factory loop (`if (kind != "CalculationTemplate")`) and from `boundKinds`, so it appears on neither side — while remaining in the discipline's own public `SupportedKinds`, which describes what the discipline knows about, not what can be done to it.

**Requirements registers no rename factory at all**, and there is no `requirements.rename` command. A Requirement's editable field is its Statement, which is what `requirements.revise` is; its identifier is its identity. Both sides are consistently silent, and the Explorer's Rename item is correctly disabled for all three Requirement Kinds.

**5. Production unification is rejected. Specifically:**

- **Bindings deriving from the manager** is rejected on ordering and on §3. It is not blocked by layering — `AppliesToKinds` is data supplied at construction, and construction happens in `Tempest.App` — but `EngineeringWorkspaceComposer` registers Documents' descriptors before Manufacturing registers `WorkInstruction`'s factories, so a binding reading the manager at its own registration time would see an incomplete map. Fixing that needs either a lazy `AppliesToKinds` (a `Tempest.Core` contract change) or a registration order nothing else depends on.
- **The manager deriving from bindings** is rejected as an inversion. `CanRename("Part")` is asked by the Object Editor about whether a *text field* is editable; making that depend on whether a *Ribbon command exists* couples a field's enablement to the command surface. The manager would also still need its factory map to construct anything, so it would gain a consultation without losing a source.
- **A shared lower-level capability registry** is rejected as a third mechanism. To subsume `AppliesToKinds` it must model the thirty-five non-routed uses; to subsume the factory maps it must carry the construction delegates. Having done both it *is* the two existing mechanisms under a new name.

**6. `KindEligibilityInvariantTests` is the permanent consistency control.**

It asserts both directions — every Kind a routed command claims has the matching factory registered, and every Kind the manager supports is claimed by at least one command — and pins the Manufacturing asymmetry explicitly so a future reader meets it as a decision. `SurfaceCommandPolicyCompletenessTests` (`WP-A1`) guards the one place the two mechanisms meet: a Ribbon Delete is *enabled* by `AppliesToKinds` and *dispatched* through `DeleteObjectAsync`.

This is not a stopgap standing in for a fix. Two mechanisms that must stay consistent where they overlap, and must stay distinct everywhere else, are exactly what an invariant test is for.

## Consequences

**Good.** The Explorer, Property Inspector and Object Editor keep asking one question — *can this object be renamed* — without knowing which discipline registered the command or whether a Ribbon button exists for it. The Ribbon and Palette keep asking a different question — *is this command available for what is selected* — without knowing how the command is constructed. Neither surface acquires a dependency on the other's mechanism, and the Manufacturing reuse keeps working in both.

**Accepted cost.** The same Kind list is written twice for nineteen commands, and a contributor adding a discipline must remember both. That cost is bounded by the invariant test, which fails at build time in either direction and names the Kind. It is also bounded at runtime: a disagreement produces `CommandResult.Failure("No rename capability is registered for Kind 'X'.")` — an honest refusal, never a crash — so the worst observable outcome is a button that is enabled and then declines.

**Disclosed, not fixed.** `CanRename`/`CanRevise`/`CanDelete` are documented as "is a factory registered" and consumed by four surfaces as "can the user do this". The names over-promise slightly. Renaming them to `HasRenameFactory` and siblings would be honest, and is a production API change outside this documentary Work Package's scope; recorded here rather than actioned. `CalculationsWorkspaceRegistration.SupportedKinds` is likewise public and wider (three Kinds) than what any command can act on (two) — harmless today, noted so a future caller does not read it as a capability list.

**What would reopen this.** A third consumer of per-Kind eligibility that needs to ask *both* questions at once, or a discipline that needs the manager and the bindings to disagree in a way the invariant test would forbid. Neither exists today.
