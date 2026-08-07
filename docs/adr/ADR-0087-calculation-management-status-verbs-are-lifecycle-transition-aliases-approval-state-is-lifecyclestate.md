# ADR-0087: Calculation Management's Lock/Unlock/Review/Approve/Archive Verbs Are `CommandDescriptor` Aliases Over `IHasLifecycle.TransitionAsync` — Approval State Is `LifecycleState` Alone

## Status

Accepted — `v0.9.0` "Mechanical Foundation", `WP 9.2A` (Engineering Calculations Workspace), 2026-08-05.

## Context

`WP 9.2A`'s own controlling instruction names Lock/Unlock/Review/Approve/Archive as distinct Calculation Management capabilities, and "Calculation Approval State" as a distinct, first-class Engineering Object concept alongside Calculation Status. Its own explicit constraints match every prior real-discipline Work Package's own: "No architectural redesign. No contract redesign. No duplicate framework."

`Contracts/Lifecycle.cs` (`WP 8.2B`) declares `IApprovalGate`, `IApproval`, `IReview`, `IReviewGate` — a real, designed contract family for exactly this concept. A direct, whole-repository search performed during this Work Package's own implementation confirms zero concrete implementations of any of the four exist anywhere in this platform, across every release to date — not a gap this Work Package introduces, but one it is the first to need to work around directly (`TD-30`). `Calculation`/`CalculationSet` do, however, already carry a real, working `IHasLifecycle` (via `EngineeringObjectBase`'s own unconditional facet implementation, `ADR-0075`) — `Status`, `History`, and `TransitionAsync`, governed by the existing, unmodified, platform-wide `LifecycleTransitionTable`.

## Decision

**Every Calculation Management status verb dispatches through one real command, `SetCalculationStatusCommand`/`SetCalculationStatusCommandHandler`, calling `IHasLifecycle.TransitionAsync` — never five separate mechanisms:**

- **"Lock"** and **"Approve"** both transition to `LifecycleState.Approved`.
- **"Unlock"** transitions back to `LifecycleState.Draft` (a permitted transition from `Approved`, per the existing, unmodified `LifecycleTransitionTable`).
- **"Request Review"** transitions to `LifecycleState.InReview`.
- **"Archive"** transitions to the terminal `LifecycleState.Archived`.

Five separate, descriptive `CommandDescriptor`s (`calculations.lock`, `calculations.unlock`, `calculations.request-review`, `calculations.approve`, `calculations.archive`) are registered with `ICommandRegistry` for Command Palette discoverability, each documenting in its own `description` exactly which `SetCalculationStatusCommand` invocation it corresponds to — discoverable and named as the task-oriented verbs this Work Package's own scope asks for, dispatched through the one real mechanism underneath, mirroring `MechanicalWorkspaceRegistration`'s own precedent of registering descriptors with no `createDefault` where no meaningful parameterless invocation exists outside a real selection context.

**"Calculation Approval State" is read from `IHasLifecycle.Status` alone** — `CalculationsPropertyFacetProvider`'s own "Approved" facet reports `"Yes"` when `Status` is `Approved` or `Released`, `"No"` otherwise — exactly mirroring `MechanicalPropertyFacetProvider`'s own already-shipped "Released" facet (`WP 9.0A`), which reads the identical `LifecycleState` in the identical way. No `IApprovalGate`/`IApproval` implementation is built to serve this Work Package alone.

## Consequences

**Positive:**

- Every impermissible transition (e.g. Draft directly to Released) is rejected identically regardless of which of the five Command Palette entries a caller reaches it through, since all five defer entirely to the one, already-correct `LifecycleTransitionTable` — no alias can bypass it.
- Zero new Domain state, zero new Domain contract, zero risk to `IHasLifecycle`'s own existing, platform-wide-shared implementation.
- `TD-30` formally registers the underlying `IApprovalGate`/`IApproval` absence in the Technical Debt Register for the first time — a genuine, pre-existing gap made visible for future planning, not merely worked around silently.

**Negative:**

- "Approval State" as delivered is a status reading, not a governed, queryable record of who approved what, when, against what evidence — a real semantic gap between what "Approval State" might reasonably be expected to mean and what this Work Package delivers. Disclosed directly in the Implementation Report, Engineering Review, and `FCR-0052`, not left for a future reader to discover by surprise.
- "Lock" and "Approve" being the identical transition means the Command Palette shows two differently-named entries with identical effect — judged acceptable, since both are genuinely meaningful, distinct user intents ("stop this from being edited further" vs. "this calculation is formally approved") that happen to share the one lifecycle state this Domain's own closed vocabulary offers for "no longer open to casual edits."

## Alternatives Considered

**Build a real, concrete `IApprovalGate`/`IApproval` implementation to serve this Work Package's own Approval State scope item.** Considered and rejected; a governed approval record's own shape (single approver? review panel? evidence bundle required? re-approval on revision?) is a genuine, open design question this Work Package's own narrow scope cannot answer well by itself, and building one unilaterally, to serve one Work Package's own display need, risks shipping the wrong shape for the two other Engineering disciplines (Verification, and any future one) that would also want to consume it. Deferred to `FCR-0052`, for a future Work Package with a real, demonstrated consumer.

**Add a new `IsLocked`/`ApprovalRecord` facet to `EngineeringObjectBase`, following the `ADR-0080` additive-facet precedent.** Considered and rejected; `ADR-0080`'s own precedent Work Packages (`WP 9.0A`/`WP 9.0B`) were themselves building out the Mechanical discipline's own foundation, a materially different situation from `WP 9.2A`'s own explicitly integration-only mandate ("Consume the existing Engineering Domain, Calculation Framework and Workspace services exclusively"). Adding a new Domain facet here would blur that boundary for a concept (`LifecycleState`) the Domain already has a real, working answer to.

## Related Documents

`ADR-0075`; `ADR-0080`; `Contracts/Lifecycle.cs`; `WP9.2A Implementation Report.md`; `WP9.2A Technical Debt Assessment.md` (`TD-30`); `WP9.2A Future Capability Assessment.md` (`FCR-0052`); `src/Tempest.App/Workspace/Calculations/SetCalculationStatusCommand.cs`; `src/Tempest.App/Workspace/Requirements/SetRequirementStatusCommand.cs`.
