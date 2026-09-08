# WP 6.1 — Permissions & Identity — Engineering Review Report

## Purpose

A self-review of `WP 6.1`'s own implementation against the constraints
its own brief imposed and this project's standing Engineering Governance,
performed before declaring the Work Package complete — the same
discipline every prior implementation Work Package has applied to
itself.

## Constraint Checklist

| Constraint | Result | Evidence |
|---|---|---|
| Follow every approved architecture document | **Met** | Every approved interface (`IIdentity`, `IPrincipal`, `ICurrentPrincipalAccessor`, `IPermissionEvaluator`, `Permission`) implemented with zero signature deviation from `Public Interface Catalogue.md`. Registration matches `Service Registration Matrix.md` and `Service Lifecycle.md` exactly — Phase 6, no new Host Lifecycle phase. |
| Do not redesign the architecture | **Met** | No change to `Host Lifecycle.md`'s phase table, `Runtime State Machine.md`, or any existing platform service's own registered shape. |
| Do not change approved public interfaces absent a genuine defect | **Met, with one disclosed departure requiring justification** | `CurrentPrincipalAccessor`'s own *internal* storage mechanism departs from `Platform Service Contracts.md`'s tentative `AsyncLocal<T>` suggestion — but `ICurrentPrincipalAccessor` itself (the public interface) is unchanged. This is not a public-interface change; it is documented anyway, in `ADR-0044`, because a future reader could otherwise assume `AsyncLocal<T>` was used. |
| If a change is genuinely required, document it, produce an ADR, explain why | **Applied to the one case where it was warranted** | See `ADR-0044`'s own reasoning and regression test for the `CurrentPrincipalAccessor` finding. No approved public interface signature was actually changed, so no ADR was required on those grounds — the ADR exists because the *concrete implementation choice* deserved a recorded justification, not because a contract broke. |
| Produce only ADRs genuinely required by implementation | **Met** | Exactly `ADR-0043` and `ADR-0044` — the two `Required ADRs.md` named as originating from `WP 6.1`. No other reserved `v0.6.0` ADR number (`ADR-0040`–`ADR-0042`, `ADR-0045`–`ADR-0051`) was touched, since none originates from this Work Package. |
| Comprehensive testing across every named category | **Met** | 91 new tests across unit, failure-injection, permission-evaluation, configuration-validation, registration-validation, and integration categories — see `WP6.1 Implementation Report.md`. |
| Clean build, complete test suite, static analysis, documentation validation, self-review | **Met** | 0 warnings/0 errors, both Debug and Release, from a clean rebuild; 643/643 tests passing, both configurations; this report is the self-review. |
| Stop after WP 6.1; do not begin WP 6.4 | **Met** | No file under any other Work Package's own scope was created or modified. |

## Four-Layer / Governance Confirmation (Re-Verified Against Real Code)

The Contract Review's own `Governance Confirmation.md` checked these four
properties against the *proposed* design. This section re-checks them
against what was *actually shipped*, per this project's own
re-derivation discipline:

1. **Four-layer dependency rules.** `Tempest.Core.Identity` depends only
   on Dependency Injection and (for `RoleProvider`/`IdentityService`)
   `IConfigurationProvider` — both existing Platform Services/DI. No
   dependency on any Module. Confirmed by direct inspection of every
   `using` directive in `src/Tempest.Core/Identity/`.
2. **No circular dependencies.** `IdentitySampleModule` depends on
   Identity types; no Identity type depends back on
   `Tempest.Samples`. Confirmed directly — `Tempest.Core.Identity` has
   no reference to `Tempest.Samples` anywhere.
3. **No public interface overlap.** `ILicenseProvider`/
   `IPermissionEvaluator`'s own anticipated surface-similarity (flagged
   in the Contract Review) does not yet exist in code (`WP 6.6` has not
   begun) — no overlap to check yet. Within what was actually shipped:
   `IPermissionEvaluator` and `ICurrentPrincipalAccessor` have entirely
   distinct method sets; no overlap found.
4. **No duplicated responsibilities.** Identity & Permissions is the
   only service in the shipped codebase with any authorization concept
   — confirmed directly: `grep -r "Permission" src/Tempest.Core/`
   outside the new `Identity/` folder returns exactly one match,
   `TempestHost.cs`'s own registration line
   (`services.Singleton<IPermissionEvaluator, PermissionEvaluator>();`),
   not a second, independent authorization mechanism.

## Findings Requiring Disclosure

1. **`TD-09`/`TD-10`/`TD-11` remain Open.** Stated plainly in
   `ADR-0044`, the retrospective, and the updated Technical Debt
   Register entries — not overclaimed as resolved anywhere in this
   Work Package's own documentation.
2. **`CurrentPrincipalAccessor` departs from the Contract Review's own
   tentative `AsyncLocal<T>` suggestion.** Documented in `ADR-0044`
   with a regression test proving the departure was necessary, not
   arbitrary.
3. **Two pre-existing governance drifts found and corrected**, unrelated
   to this Work Package's own scope: the Namespace Register's file-count
   total had gone stale since `WP 5.2` (missed `WP 5.3`'s own
   `TempestSampleModule.cs`); `ClockModuleDiscoveryTests.cs` required
   its own now-standard update for an eighth sample module — both
   corrected in this Work Package's own commit.

## Verdict

`WP 6.1` meets every constraint its own brief imposed. Nothing approved
was redesigned; the one internal-implementation departure from the
Contract Review's own tentative language is disclosed with a concrete,
verifying test, not asserted without evidence; and every governance
figure this Work Package touched was re-derived directly, not
incremented from a prior claim.

## Related Documents

`WP6.1 Implementation Report.md`; `WP6.1 Lessons Learned.md`; `WP6.1
Technical Debt Assessment.md`; `WP6.1 Future Capability
Recommendations.md`; `ADR-0043`; `ADR-0044`; `docs/releases/v0.6.0/
Governance Confirmation.md` (the Contract Review's own design-time
check this report re-verifies against shipped code).
