# WP 6.1 — Permissions & Identity Implementation

## 1. Introduction

WP 6.1 delivers the Identity & Permissions Framework: the platform's
first authorization concept, and the first Work Package of the
Platform Services phase (`v0.6.0`) to ship real code. Unlike the
Academy Plan's own anticipation (`docs/releases/v0.6.0/Academy Plan.md`
named this Work Package as likely needing its own architecture/
implementation split, mirroring `WP 5.0A`/`WP 5.0B`), this Work Package
was implemented in a single pass, directly against the already-approved
`v0.6.0` architecture package (`Release Architecture.md` and seven
companions) and Contract Review package (`Platform Service Contracts.md`
and four companions) — no separate architecture phase was run, per
direct instruction to implement.

## 2. Purpose

To build `Tempest.Core.Identity` exactly as the approved architecture
specified — `IIdentity`, `IPrincipal`, `ICurrentPrincipalAccessor`,
`IPermissionEvaluator`, `Permission` — plus the Role model and identity-
resolution service the architecture package explicitly deferred to this
Work Package's own implementation phase; to wire it into the real,
unmodified `TempestHost`; and to do so without redesigning any approved
architecture or changing any approved public interface absent a genuine
implementation defect.

## 3. Background

This is the first implementation Work Package in the Platform Services
phase, following two entirely documentation-only phases: the Release
Architecture Package (nine services architected, no code) and the
Contract Review Package (per-service contracts, dependency ordering, a
registration matrix, and a testing strategy — still no code). Both
packages were read in full before any file was written. `Platform
Service Implementation Order.md` (from the Contract Review package)
recommended `WP 6.1` start first among the nine Work Packages —
alongside `WP 6.6` (Licensing) — specifically because Identity &
Permissions carries the release's highest risk (`Risk Register.md` `R1`)
and is the most-depended-on new service (`WP 6.3`, `WP 6.5` both require
it). Implementing `WP 6.1` first, ahead of `WP 6.0`'s own lower nominal
number, is therefore consistent with the architecture phase's own
recommendation, not a deviation from it.

## 4. The Problem

Three things needed to exist, none of which this platform has ever had:

1. **An identity concept** — nothing in this codebase represents "who
   is performing this action" before this Work Package.
2. **A permission-checking mechanism** — three tracked debt items,
   `TD-09` (plugin isolation), `TD-10` (Navigation ownership), and
   `TD-11` (registration-order squatting), have each carried the
   identical revisit trigger since their own disclosure: "the first
   Work Package with a genuine reason to build an authorization
   concept."
3. **A way to populate that identity with real permissions** — the
   architecture package's own `Public Interface Catalogue.md` drafted
   `IPermissionEvaluator` and `IPrincipal` but explicitly left "the
   mechanism a future... login flow... must still define" as an open
   question for this Work Package to resolve.

## 5. The Design

**Implemented exactly as drafted, zero signature deviation:** `IIdentity`
(`Id`, `DisplayName`), `IPrincipal` (`Identity`, `Permissions`),
`ICurrentPrincipalAccessor` (`Current`, read-only), `IPermissionEvaluator`
(`HasPermission`, `RequirePermission`), `Permission` (a validated,
value-equal record keyed by `Key`).

**Additive elaboration, not a change to anything approved:** `IRole`/
`Role` (a named permission grouping) and `IRoleProvider`/`RoleProvider`
(config-sourced role definitions, `Identity:Roles:{RoleName}:Permissions`)
plus `IIdentityService`/`IdentityService` (resolves a principal from an
identity id, flattening its configured roles —
`Identity:Principals:{IdentityId}:Roles` — into permissions, and
establishes it as current). None of these four types appeared in the
original `Public Interface Catalogue.md` draft; all four fill a gap that
document explicitly left for this Work Package to close (`ADR-0043`).

**Fail-closed by default:** `IIdentityService.GetPrincipal` never throws
for an identity id nothing has configured — it resolves to a principal
holding zero permissions. A principal referencing an undefined role
(`RoleNotFoundException`) is a different, genuine configuration defect
and is reported loudly.

**The enforcement point:** `IPermissionEvaluator.RequirePermission`
throws `PermissionDeniedException`; `HasPermission` is the non-throwing
form. This is now the single, uniform mechanism any future consumer
should call — but this Work Package did not itself retrofit a call into
`NavigationService`, Command/Navigation registration, or plugin loading
(`ADR-0044`; see Section 10, Trade-offs).

**Registration:** `IRoleProvider`, `IPermissionEvaluator`, and
`IIdentityService` are ordinary, container-constructed singletons,
registered in `TempestHost`'s existing Platform Services Registered
block (Phase 6) — no new Host Lifecycle phase.
`CurrentPrincipalAccessor` is constructed directly (`new`, zero
constructor dependencies) and registered via `AddInstance` under *both*
`ICurrentPrincipalAccessor` and its own concrete type — one object,
two service-type keys, so `IdentityService` (which needs write access
via the concrete type) and every ordinary consumer (the interface) share
state rather than each getting an independently-constructed instance.

**`IdentitySampleModule`** (`Tempest.Samples`, the eighth production
sample module) establishes a default local principal
(`sample.local-user`) during its own `InitialiseAsync` and registers
`CheckSamplePermissionCommand`, whose handler reports whether the
current principal holds a sample permission — demonstrating both the
fail-closed-denied default and the granted path (with configuration
supplied) against the same, unmodified module.

## 6. Alternatives Considered

See `ADR-0043` and `ADR-0044` for the complete reasoning. In summary:
building external identity-provider federation now was rejected as
speculative scope no named `v0.6.0` Work Package requires; throwing for
an unrecognised identity id was rejected as a worse default than a
harmless, zero-permission principal; retrofitting `RequirePermission`
calls into `NavigationService`/Command registration/plugin loading as
part of this Work Package was rejected because none of those three was
named in this Work Package's own brief, and each is approved, shipped
architecture from a prior release; and implementing
`CurrentPrincipalAccessor` with `AsyncLocal<T>`, as the architecture
package's own Contract Review tentatively suggested, was rejected after
direct verification that it does not fit this release's actual,
local-only, single-ambient-principal need.

## 7. Why This Solution Was Chosen

Every approved interface is implemented with zero deviation, so nothing
downstream (a future `WP 6.3`/`WP 6.5` consumer) needs to plan around an
unexpected shape change. The four additive types close a gap the
architecture package itself flagged as open, using patterns this
codebase has already proven elsewhere (config-sourced definitions,
mirroring `LoggerFactory`'s own `Runtime:Logging:MinimumLevel` reading;
imperative registration, mirroring the Command Framework's own
`RegisterHandler`). The ambient `CurrentPrincipalAccessor` was chosen
over `AsyncLocal<T>` because it is the only one of the two that actually
satisfies this release's real requirement, proven by a regression test,
not merely asserted.

## 8. Architectural Principles

- **Fail Closed, Not Fail Open** — an unrecognised identity gets zero
  permissions, never broad access by default.
- **Single Enforcement Point** — `IPermissionEvaluator` is the one place
  every future authorization check should go through, rather than each
  consumer inventing its own check.
- **Reuse Before Invention** — config-sourced grants reuse
  `IConfigurationProvider` exactly as it already exists; imperative
  registration mirrors the Command Framework's own proven pattern;
  Composition-Root dual-registration mirrors, in spirit, the existing
  `AddInstance` mechanism already used for Configuration/Logging/
  Diagnostics.
- **Verify, Don't Assume, Especially About Concurrency** — the
  `AsyncLocal<T>` question was not resolved by reasoning alone; a
  concrete test proved the ambient design was necessary before it was
  adopted.
- **Do Not Redesign Approved Architecture Absent a Genuine Defect** —
  this Work Package changed no existing platform service's behaviour
  (`NavigationService`, Command registration, plugin loading) to close
  `TD-09`/`TD-10`/`TD-11`, because none of those changes was in its own
  brief.

## 9. Files Added

`src/Tempest.Core/Identity/IIdentity.cs`;
`src/Tempest.Core/Identity/IPrincipal.cs`;
`src/Tempest.Core/Identity/Permission.cs`;
`src/Tempest.Core/Identity/IRole.cs`;
`src/Tempest.Core/Identity/Role.cs`;
`src/Tempest.Core/Identity/IRoleProvider.cs`;
`src/Tempest.Core/Identity/RoleProvider.cs`;
`src/Tempest.Core/Identity/ICurrentPrincipalAccessor.cs`;
`src/Tempest.Core/Identity/CurrentPrincipalAccessor.cs`;
`src/Tempest.Core/Identity/IPermissionEvaluator.cs`;
`src/Tempest.Core/Identity/PermissionEvaluator.cs`;
`src/Tempest.Core/Identity/IIdentityService.cs`;
`src/Tempest.Core/Identity/IdentityService.cs`;
`src/Tempest.Core/Identity/PlatformIdentity.cs`;
`src/Tempest.Core/Identity/PlatformPrincipal.cs`;
`src/Tempest.Core/Identity/IdentityException.cs`;
`src/Tempest.Core/Identity/PermissionDeniedException.cs`;
`src/Tempest.Core/Identity/RoleNotFoundException.cs`;
`src/Samples/Tempest.Samples/IdentitySampleModule.cs`;
`src/Samples/Tempest.Samples/CheckSamplePermissionCommand.cs`;
`src/Samples/Tempest.Samples/CheckSamplePermissionCommandHandler.cs`;
`tests/Tempest.Core.Tests/Identity/PermissionTests.cs`;
`tests/Tempest.Core.Tests/Identity/RoleTests.cs`;
`tests/Tempest.Core.Tests/Identity/PlatformIdentityAndPrincipalTests.cs`;
`tests/Tempest.Core.Tests/Identity/RoleProviderTests.cs`;
`tests/Tempest.Core.Tests/Identity/CurrentPrincipalAccessorTests.cs`;
`tests/Tempest.Core.Tests/Identity/PermissionEvaluatorTests.cs`;
`tests/Tempest.Core.Tests/Identity/IdentityServiceTests.cs`;
`tests/Tempest.Core.Tests/Identity/ExceptionTests.cs`;
`tests/Tempest.Core.Tests/Runtime/IdentityHostRegistrationTests.cs`;
`tests/Tempest.Core.Tests/Samples/IdentitySampleModuleIntegrationTests.cs`;
`docs/adr/ADR-0043-identity-model-scope-local-only-extensible.md`;
`docs/adr/ADR-0044-authorization-enforcement-point.md`;
this retrospective. **Modified:**
`src/Tempest.Core/Runtime/TempestHost.cs` (registration);
`tests/Tempest.Core.Tests/Samples/ClockModuleDiscoveryTests.cs`
(sample-module count, 7 → 8).

## 10. Trade-offs

- **`TD-09`/`TD-10`/`TD-11` remain Open.** This Work Package built the
  enforcement point; it did not retrofit a `RequirePermission` call into
  `NavigationService.Unregister`, Command/Navigation registration, or
  plugin loading. Each is now resolvable by a small, targeted follow-on
  change rather than requiring its own mechanism — but none is resolved
  by this Work Package, and this is stated plainly rather than
  overclaimed.
- **No authentication of any kind.** `IIdentityService.GetPrincipal`
  trusts its caller completely (`ADR-0043`). Acceptable only because no
  untrusted caller exists yet; `WP 6.3`'s own architecture phase must
  revisit this directly before exposing identity establishment to a
  network caller.
- **Role/principal configuration requires a restart to change**, since
  `IConfigurationProvider` is immutable once built. No runtime
  administration surface exists in this release.
- **`CurrentPrincipalAccessor` is ambient, not per-request-scoped.**
  Correct for this release's own local-only, single-process deployment;
  will need real reconsideration once `WP 6.3` introduces genuine
  concurrent requests (see `ADR-0044`'s own Revisit trigger).
- **`RoleNotFoundException` is validated lazily**, on first principal
  resolution, not eagerly at Host startup — a configuration typo is not
  discovered until something actually resolves that principal.

## 11. Common Mistakes

- **Assuming this Work Package resolved `TD-09`/`TD-10`/`TD-11`** because
  it was the Work Package those items' own revisit trigger named — it
  built the mechanism, not the retrofit. Read `ADR-0044` before assuming
  otherwise.
- **Assuming `CurrentPrincipalAccessor` should use `AsyncLocal<T>`**
  because the Contract Review tentatively suggested it — that
  suggestion was explicitly left open for this Work Package to resolve,
  and resolving it required rejecting the tentative suggestion; see
  `ADR-0044`'s own worked reasoning and regression test before
  reintroducing `AsyncLocal<T>` without re-verifying the same concern.
- **Assuming an unrecognised identity id should throw** — it resolves to
  a harmless, zero-permission principal instead; only a principal
  referencing an *undefined role* throws (`RoleNotFoundException`).
- **Assuming Role/`IIdentityService` were part of the originally
  approved `Public Interface Catalogue.md`** — they were not; they are
  this Work Package's own additive elaboration of an explicitly-left-open
  question, not a design already ratified before implementation began.

## 12. Future Evolution

External identity-provider federation (explicitly deferred, `ADR-0043`);
a runtime-mutable, administered role/principal store (would itself need
Settings, `WP 6.4`, and its own authorization gate); request-scoped
`CurrentPrincipalAccessor` once `WP 6.3` introduces genuine concurrency;
retrofitting `RequirePermission` into `NavigationService`, Command/
Navigation registration, and plugin loading to actually close `TD-09`/
`TD-10`/`TD-11` — each named explicitly as a future, separately-scoped
Work Package's own responsibility, not designed now because this Work
Package's own brief did not authorise touching those three services.

## 13. Key Takeaways

1. An architecture package's own tentative language ("likely requires
   `AsyncLocal<T>`") is not a ratified decision — it is an explicit
   invitation for the implementation phase to verify and decide, and
   verifying it directly (with a failing prototype test) found a real
   problem the tentative suggestion would have caused.
2. Building the mechanism that could close three tracked debt items is
   not the same as closing them — stating this distinction plainly, in
   the ADR and this retrospective both, is more valuable than a
   simpler-sounding but inaccurate claim of resolution.
3. An architecture package can legitimately leave a question fully open
   ("the mechanism... must still define") — this Work Package's own Role
   model and `IIdentityService` are the answer, and are additive, not a
   change to anything already approved.

## Architectural Debt Assessment

`TD-09`, `TD-10`, `TD-11` — **Open, mechanism now exists.** Each
Technical Debt Register entry was updated in place to record that
`IPermissionEvaluator`/`ADR-0044` is now available as the enforcement
point a future, explicitly-scoped Work Package should use — none is
marked Resolved, since no retrofit into `NavigationService`, Command/
Navigation registration, or plugin loading was performed. No new debt
item was found genuinely required beyond what `ADR-0043`/`ADR-0044`
themselves already disclose (local-only scope; ambient, not
request-scoped, `CurrentPrincipalAccessor`) — both anticipated in this
retrospective's own Trade-offs section, not discovered as a surprise
afterward.

## Observations

Two pre-existing, unrelated governance drifts were found and corrected
during this Work Package's own repository review: the Namespace
Register's own file-count total had gone stale at 143 since `WP 5.2`,
never updated when `WP 5.3` added `src/Templates/Tempest.Templates.Module/
TempestSampleModule.cs` — corrected here by direct re-derivation (165
files) rather than an incremented guess, per `WP 5.4`'s own
standing-practice recommendation. `ClockModuleDiscoveryTests.cs`'s own
assembly-wide discovery test required updating for the eighth sample
module, exactly as every prior sample-module-adding Work Package
(`WP 4.4E`, `WP 5.0B`, `WP 5.1B`, `WP 5.2`) has had to update the same
test in turn — a recurring, expected maintenance point, not a surprise.

## Related Documents

`docs/releases/v0.6.0/Release Architecture.md` and companions (the
architecture package this Work Package implemented); `Platform Service
Contracts.md` and companions (the Contract Review package); `ADR-0043`;
`ADR-0044`; `docs/architecture/Platform Service Map.md` (Identity &
Permissions entry); `Technical Debt Register.md` (`TD-09`, `TD-10`,
`TD-11`); `docs/releases/v0.6.0/Risk Register.md` (`R1`); `docs/security/
Platform Security Review v0.5.0.md` (Findings SEC-01, NAV-1);
`docs/architecture/Command Framework Architecture.md` (Finding CMD-1).
