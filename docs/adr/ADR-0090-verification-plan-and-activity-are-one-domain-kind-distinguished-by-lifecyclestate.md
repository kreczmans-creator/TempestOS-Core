# ADR-0090: "Verification Plan" and "Verification Activity" Are One Domain Kind (`VerificationActivity`), Distinguished Only by `LifecycleState`; Review/Approve/Archive Are `CommandDescriptor` Aliases Over `TransitionAsync`

## Status

Accepted — `v0.9.0` "Mechanical Foundation", `WP 9.3A` (Verification Management Workspace), 2026-08-07.

## Context

`WP 9.3A`'s own controlling instruction names "Verification Plans" and "Verification Activities" as two separate scope items, and "Review"/"Approve"/"Archive" as three of Verification Management's twelve named verbs, alongside "Verification Approval State" as a named Engineering Behaviour item. Its own explicit constraints match every prior real-discipline Work Package's own: "No architectural redesign. No contract redesign. No duplicate framework."

`Contracts/RequirementsVerification.cs`/`Implementation/RequirementsVerification.cs` (`WP 8.2B`/`WP 8.2C`, confirmed by direct read) declare and compile exactly one concrete Verification-family Domain Kind with any real fields: `VerificationActivity` (`SubjectId`, `Method`, `IHasLifecycle`). No `VerificationPlan` Kind exists, declared or implemented, anywhere. `VerificationActivity` already carries a real, working `IHasLifecycle` (via `EngineeringObjectBase`'s own unconditional facet implementation) — `Status`, `History`, `TransitionAsync`, governed by the existing, unmodified, platform-wide `LifecycleTransitionTable` — the identical starting shape `ADR-0087` (`WP 9.2A`, Calculation Management's own Lock/Unlock/Review/Approve/Archive) and `ADR-0090`'s own sibling decision this Work Package makes for Document classification (`ADR-0088`, `WP 9.4A`) both already faced.

## Decision

**A `VerificationActivity` in `LifecycleState.Draft` is a Verification Plan; the same object once `InReview` (or later) is a Verification Activity under way — one Domain Kind, read through `IHasLifecycle.Status` alone, never two Kinds or a dedicated "plan/activity" flag.** No `VerificationPlan` Domain class is declared or added.

**Every Verification Management status verb dispatches through one real command, `SetVerificationActivityStatusCommand`/`SetVerificationActivityStatusCommandHandler`, calling `IHasLifecycle.TransitionAsync` — never three separate mechanisms:**

- **"Request Review"** transitions to `LifecycleState.InReview` — also this Work Package's own realisation of "the Plan has become an Activity under way."
- **"Approve"** transitions to `LifecycleState.Approved`.
- **"Archive"** transitions to the terminal `LifecycleState.Archived`.

Three separate, descriptive `CommandDescriptor`s (`verification.request-review`, `verification.approve`, `verification.archive`) are registered with `ICommandRegistry` for Command Palette discoverability, mirroring `ADR-0087`'s own identical precedent exactly.

**"Verification Approval State" is read from `IHasLifecycle.Status` alone** — `VerificationActivityPropertyFacetProvider`'s own "Approved" facet reports `"Yes"` when `Status` is `Approved` or `Released`, `"No"` otherwise — the identical facet `MechanicalPropertyFacetProvider`/`CalculationsPropertyFacetProvider`/`DocumentsPropertyFacetProvider` already establish. No `IApprovalGate`/`IApproval` implementation is built to serve this Work Package alone (`TD-30`, confirmed still open).

## Consequences

**Positive:**

- Zero new Domain state, zero new Domain contract — `VerificationActivity`'s own already-compiled shape (`WP 8.2C`) is completely sufficient for both named scope items.
- Every impermissible transition is rejected identically regardless of entry point, since all three Command Palette entries defer entirely to the one, already-correct `LifecycleTransitionTable`.
- A Verification Activity's own "Plan → Activity" progression is externally visible and queryable through the exact same `Status`/`History` facet every other discipline's own Property Inspector already shows — no separate "is this still a plan" query is needed.

**Negative:**

- A Verification Plan and a Verification Activity under way are, at the Domain layer, the identical concrete type — there is no compiler-enforced guarantee a caller cannot construct a `VerificationActivity` already `InReview` and call it a "Plan" by display convention alone. Accepted for the identical reason `ADR-0088`'s own `Classification` mapping was accepted: a disclosed, precedent-following convenience over the existing vocabulary, not a new Domain guarantee.
- "Request Review" doing double duty as both "begin formal review" (the name every other discipline's own identical descriptor already carries) and "the Plan has become an Activity" (this Work Package's own added meaning) means the one transition now carries two distinct intents. Judged acceptable — both intents genuinely coincide at the identical lifecycle point (work has started, is no longer merely planned) for every real-discipline Work Package's own established `InReview` meaning so far.

## Alternatives Considered

**A new `VerificationPlan` Domain Kind, promoted to `VerificationActivity` on some explicit "start" command.** Considered and rejected; this is precisely the "contract redesign" this Work Package's own controlling instruction forbids, and would require `WP 8.2A`/`WP 8.2B`/`WP 8.2C` (all `Complete`, all already `Engineering Review APPROVED`) to be reopened to add a Kind those Work Packages explicitly did not include.

**A `Category`/`Classification`-style metadata tag distinguishing "Plan" from "Activity," mirroring `ADR-0088`'s own mechanism exactly.** Considered and rejected as unnecessary duplication; `LifecycleState` already carries this exact distinction for free (Draft = not yet started), and `IHasMetadata.Classification` would then be tracking a fact `IHasLifecycle.Status` already tracks — two fields for one distinction, never reconciled against each other, a genuine source of drift `ADR-0088`'s own single-field design does not risk.

## Related Documents

`ADR-0087`; `ADR-0088`; `ADR-0089`; `Contracts/RequirementsVerification.cs`; `WP9.3A Implementation Report.md`; `WP9.3A Technical Debt Assessment.md` (`TD-30`); `src/Tempest.App/Workspace/Verification/SetVerificationActivityStatusCommand.cs`; `src/Tempest.App/Workspace/Calculations/SetCalculationStatusCommand.cs`.
