# WP 6.6 — Licensing Framework — Platform Integration Demonstration

## Purpose

Demonstrate how Licensing integrates with Platform Services —
explicitly required by this Work Package's own brief as a distinct
deliverable, naming Identity, Settings, Audit, Notifications, and the
REST API as the five services to assess and justify. For each, this
document records: whether it was used, its purpose, the coupling
rationale, and its plausible future consumers.

## How to Read This Document

**`LicensingSampleModule` — this Work Package's own reference module —
registers one command (`CheckSampleCapabilityCommand`) whose handler
contains only a permission check, a capability check, a Settings read,
an Audit record, and a Notification publish — then maps that same
command to an HTTP route.** Every platform-service interaction below
happens inside that one handler or that one route mapping;
`Tempest.Core.Licensing` itself references none of the five services
this document assesses — confirmed by direct inspection of every
`using` directive in `src/Tempest.Core/Licensing/`.

## The One Command This Work Package Ships (Reachable Two Ways)

`CheckSampleCapabilityCommand` → checks
`LicensingSampleModule.SampleCapabilityKey`
(`"sample.premium-feature"`), requiring
`LicensingSampleModule.CapabilityCheckPermissionKey`
(`"licensing.check-capability"`). Reachable directly through the
Command Framework, and via `POST /api/v1/sample-capability` through the
REST API.

## Identity & Permissions

**Used?** Yes — inside `CheckSampleCapabilityCommandHandler`, not
inside `Tempest.Core.Licensing` itself.

**Purpose.** The handler reads the current principal via
`ICurrentPrincipalAccessor`, then checks
`CapabilityCheckPermissionKey` via `IPermissionEvaluator.HasPermission`
before ever consulting `ILicenseProvider`.

**Coupling rationale.** `Platform Service Contracts.md`'s own Licensing
contract does not state the enforcement point is the caller in so many
words, but the same convention every other Platform Service in this
release follows (Reporting, Export/Import) applies identically here —
`ILicenseProvider.HasCapability` answers a question; it does not decide
who may ask it. `Tempest.Core.Licensing` itself has zero dependency on
`Tempest.Core.Identity` — confirmed directly.

**Future consumers.** Any future Licensing-gated command follows this
identical pattern — check a permission, then check a capability, both
at the calling layer.

## Settings

**Used?** Yes — but only on the success path, entirely inside
`CheckSampleCapabilityCommandHandler`, not inside
`Tempest.Core.Licensing` itself.

**Purpose.** Once `HasCapability` confirms the sample capability is
enabled, the handler reads a customisable message via
`ISettingsProvider.GetValueAsync` (`PremiumMessageSettingKey`) and
returns it as the command's own success message — demonstrating that a
licensed capability's own behaviour can be further customised through
the ordinary Settings mechanism, with no special-casing.

**Coupling rationale.** `Tempest.Core.Licensing` itself has zero
dependency on `Tempest.Core.Settings` — confirmed directly. The same
"the core service never needs to know" pattern every other Work
Package's own cross-service integration follows.

**Future consumers.** Any future Licensing-gated feature wanting
per-installation customisable behaviour on top of a licensed capability
can read its own setting identically.

## Audit

**Used?** Yes, on both the granted and denied paths, independently.

**Purpose.** `CheckSampleCapabilityCommandHandler` records
`CapabilityGrantedActionName` (`"licensing.capability-granted"`) or
`CapabilityDeniedActionName` (`"licensing.capability-denied"`) through
the ordinary, unmodified `IAuditRecorder`, carrying the capability key
and current licensee name in `Detail`.

**Coupling rationale.** Not a core-level dependency of
`Tempest.Core.Licensing` itself — Licensing's own approved contract
states no audit requirement of its own; recording is an ordinary
calling-layer decision, matching Reporting's and Export/Import's own
established convention.

**Future consumers.** Any future Licensing-gated module can record its
own grant/denial decisions the same way, with no interface change
required — a licensing-decision audit trail is immediately available to
any consumer that wants one.

## Notifications

**Used?** Yes, on both the granted and denied paths, independently.

**Purpose.** The handler publishes an `IPlatformNotification` under
`NotificationCategory` (`"Licensing"`) — `NotificationSeverity.Success`
when the capability is enabled, `NotificationSeverity.Warning` when it
is not — a fixed, non-identifying message in both cases, never leaking
license details to an unauthorized subscriber.

**Coupling rationale.** `Tempest.Core.Licensing` itself has zero
dependency on `Tempest.Core.Notifications` — confirmed directly. The
`Warning`-for-denial choice (rather than `Error`) reflects that "this
capability isn't licensed" is an everyday, expected outcome for an
unlicensed installation, not a fault.

**Future consumers.** Any future Licensing-gated module can publish its
own grant/denial notice identically; a future UI Shell could subscribe
to `"Licensing"` notifications to surface an upsell prompt on a
`Warning`-severity denial, without Licensing itself ever needing to
know that happens.

## REST API

**Used?** Yes — the same command is also mapped to an HTTP route,
demonstrating the REST API exposing a Licensing-gated capability.

**Purpose.** `LicensingSampleModule.InitialiseAsync` calls
`IApiEndpointRegistry.MapCommand` for `POST /api/v1/sample-capability`,
requiring `CapabilityCheckPermissionKey` — exactly the same
`IApiEndpointRegistry` any module can inject, mirroring
`ApiSampleModule`'s own "any module can map its own route" precedent
from `WP 6.3`. Proven directly by a real HTTP round trip: `200` for a
licensed capability with a granted permission, `400` for an unlicensed
one, `403` for a denied permission.

**Coupling rationale.** `Tempest.Core.Licensing` itself has zero
dependency on `Tempest.Core.Api` — confirmed directly. The REST layer
never needs to know a capability check is involved; it only knows it is
invoking an already-registered command.

**Future consumers.** Any future Licensing-gated command can be exposed
over HTTP with a single `MapCommand` call and no other code, exactly
like any other command in this codebase.

## Summary Table

| Service | Used? | Where | Coupling Rationale | Future Consumers |
|---|---|---|---|---|
| Identity & Permissions | Yes | Inside the command handler, not `Tempest.Core.Licensing` | Enforcement point is the caller, mirroring every other Work Package's own convention | Every future Licensing-gated command |
| Settings | Yes | Inside the command handler, success path only | A licensed capability's own behaviour may be further customised via Settings, with no special-casing | Any future Licensing-gated feature wanting customisable behaviour |
| Audit | Yes, both paths | Inside the command handler, independently | Ordinary calling-layer recording, mirroring Reporting's/Export-Import's own convention | Any future Licensing-gated module |
| Notifications | Yes, both paths | Inside the command handler, independently | The core service never needs to know; fixed, non-identifying message; `Warning` (not `Error`) for denial | Any future module; a future UI Shell upsell prompt |
| REST API | Yes | The same command also mapped to an HTTP route | `Tempest.Core.Licensing` has zero dependency on `Tempest.Core.Api`; the REST layer never knows a capability check is involved | Any future Licensing-gated command exposed over HTTP |

## Related Documents

`WP6.6 Implementation Report.md`; `WP6.6 Engineering Review Report.md`;
`WP6.6 Platform Impact Assessment.md`; `WP6.6 Lessons Learned.md`;
`WP6.6 Technical Debt Assessment.md`; `WP6.6 Future Capability
Recommendations.md`; `ADR-0050`; `docs/releases/v0.6.0/Platform Service
Contracts.md` (Licensing's own contract); `WP6.0 Platform Integration
Demonstration.md`, `WP6.3 Platform Integration Demonstration.md`, `WP6.7
Platform Integration Demonstration.md` (the precedents this document's
own format follows).
