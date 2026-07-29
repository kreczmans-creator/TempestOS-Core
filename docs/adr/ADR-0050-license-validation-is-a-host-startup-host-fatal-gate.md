# ADR-0050: License Validation Is a Host-Startup, Host-Fatal Gate — Except a Missing License File, Which Is a Valid, Unrestricted Default

## Status

Accepted — `WP 6.6` (Licensing Framework), 2026-07-29.

## Context

`v0.6.0`'s own architecture package anticipated this decision but
deliberately left it unratified pending `WP 6.6`'s own implementation
phase. `Required ADRs.md` named the core placement question (pre-
container leaf, Host-fatal on invalid) as this Work Package's own
required ADR. Implementation surfaced a genuine, previously-open
question the anticipated decision did not itself resolve: `Risk
Register.md`'s own `R5` named the exact tension —

> "License validation being too aggressively Host-fatal... Licensing is
> a new *kind* of failure (a business/entitlement condition, not a
> technical fault), and an overly strict interpretation could make the
> platform impossible to run at all in a degraded-but-useful state...
> `WP 6.6`'s own architecture phase should explicitly define what
> 'invalid' means (missing vs. expired vs. malformed) and confirm
> whether every one of those categories genuinely warrants Host-fatal
> treatment."

This is not a hypothetical concern: `Tempest.Core.Runtime.TempestHostBuilder`
is exercised by every one of the 24 existing test files that build a
real `TempestHost` and call `RunAsync`, none of which has ever supplied
a license file, and `Tempest.App`'s own real entry point has never
shipped one either. Treating a missing license file as Host-fatal would
have made this platform — including its own entire existing test suite
and every sample/development workflow — unable to start at all, a
regression this Work Package's brief could not possibly have intended
("expose licensing capability... shall not implement commercial
policy... shall remain independent of billing, subscriptions").

`Platform Service Contracts.md`'s own Security Considerations also
named a second, undecided question: whether license validation includes
a cryptographic signature check or trusts the license file's own
contents at face value, "disclosing whichever it chooses as a named
trade-off if the answer is the latter."

## Decision

**`ILicense`, `ILicenseValidator`, `LicenseValidationResult`,
`ILicenseProvider`, `LicensingException`, and `LicenseValidationException`
are implemented exactly as `Public Interface Catalogue.md` drafted** —
zero signature deviation. `LicensingException` is a concrete,
base-plus-subtype type (not abstract, despite the catalogue's own
pseudo-code shorthand), mirroring `ReportingException`/`ApiException`/
`ExportImportException`'s own established real-codebase convention.

**`ILicenseValidator` is constructed directly by `TempestHost`, before
the DI container exists, with no constructor dependencies at all** —
deliberately a leaf, mirroring `PlatformVersionProvider`'s own position.
Runs immediately after `ConfigurationBuilder.Build()` returns and before
the logger/sink are constructed — functionally indistinguishable from
running before Configuration is built, since `LicenseValidator` never
reads `IConfigurationProvider` at all (a fixed, documented
`license.json` convention path, mirroring Plugin Manifest's own fixed
`Plugins/`/`plugin.manifest.json` convention). `Service Lifecycle.md`'s
own "immediately after Configuration is built" phrasing and `Platform
Service Contracts.md`'s own "Configuration itself is not yet built at
the point Licensing validates" phrasing describe the same actual
placement from two different angles — resolved here by direct
implementation rather than left as an unexamined inconsistency between
two approved documents, since the two readings produce zero observable
difference in behaviour.

**A missing license file is a valid, unrestricted-but-uncapable
default — resolving `Risk Register.md`'s own `R5` explicitly, not left
open.** `LicenseValidator.Validate()` returns `IsValid: true` with a
`License` under `LicenseValidator.UnlicensedLicenseeName` (`"Unlicensed"`),
never expiring, with zero enabled capabilities, whenever the
conventional file is absent. This is this platform's own normal,
open-source-friendly default state — every capability-gated command
reports its own capability unavailable via `ILicenseProvider.HasCapability`,
exactly the fail-closed-by-default pattern `IPermissionEvaluator`
already established for authorization (`ADR-0044`) — not an operator
error, and never Host-fatal. **A license file that exists but is
unreadable, is not valid JSON, is missing its own required
`LicenseeName` field, or has already expired, is Host-fatal** — someone
deliberately supplied it, and it is broken, a genuinely different,
actionable condition from "no one supplied one." This directly answers
`R5`'s own question: "missing" does not warrant Host-fatal treatment;
"expired" and "malformed" do.

**An invalid result throws `LicenseValidationException`, propagating
through `TempestHost.ExecuteStartupPhasesAsync` to `RunAsync`'s own
existing `catch (Exception ex) { EnterFaulted(ex); throw; }` block** —
Host-fatal, per `ADR-0013`'s existing platform-service-failure
classification, applied to Licensing with zero modification to
`RunAsync` itself. A minimal, direct `Console.Error.WriteLine` records
the failure reason immediately before throwing, since no logger exists
yet at this point in startup — mirroring how Configuration itself has
no logger available at its own construction point. On success, the
license's own licensee name and capability count are logged
retroactively once the logger is built, alongside the existing
"Configuration Built"/"Logging Built" lines.

**`ILicenseProvider` is Composition-Root-constructed from the
already-validated `ILicense` and registered via `AddInstance` at Phase
6** — immediately after Identity & Permissions, matching `Service
Registration Matrix.md`'s own recommended order — exactly like
`IPlatformVersionProvider` and `IDiagnosticsProvider` before it. Never
container-resolved from a license file itself; that is
`ILicenseValidator`'s own, separate, pre-container responsibility.

**No cryptographic signature verification of the license file's own
contents — trusted at face value.** Disclosed as `TD-16`, mirroring
`TD-13`'s own precedent for the REST API's undisclosed-authentication
gap: building a signature scheme now, with no concrete distribution or
tamper-threat model named by this release's own approved scope, would
be exactly the kind of premature capability this project's own
conventions warn against. Remote validation/activation, floating/seat-
based licensing, and a renewal/grace-period model are all explicitly
deferred future scope (`AT-13`), matching the approved contract's own
Future Extension Points precisely.

**Cross-service integration is demonstrated at the sample-module layer,
never inside `ILicenseValidator`/`ILicenseProvider` themselves.**
`LicensingSampleModule`'s own `CheckSampleCapabilityCommandHandler`
checks `IPermissionEvaluator.HasPermission` (Identity) before checking
`ILicenseProvider.HasCapability`, reads a Settings-provided message on
success (Settings), records the outcome through `IAuditRecorder`
(Audit), and publishes a completion notice through
`INotificationDispatcher` (Notifications) — none of which
`ILicenseProvider` itself references. The same command is also mapped
to an HTTP route via `IApiEndpointRegistry` (REST API), proven by a
real HTTP round trip, mirroring `ApiSampleModule`'s own "any module can
map its own route" precedent. Persistence and Reporting are deliberately
**not** consumed anywhere — Licensing's own approved contract states
"Persistence Requirements: None," and no sample component was built to
use either speculatively. See this Work Package's own Platform
Integration Demonstration for the complete, per-service account.

## Consequences

**Positive:**

- Every approved interface is implemented with zero deviation, so any
  future consumer (Licensing-gated feature, engineering module) can
  depend on `ILicense`/`ILicenseValidator`/`ILicenseProvider` with full
  confidence in their shape.
- `R5`'s own open question is now answered precisely, not vaguely: the
  exact boundary between "not Host-fatal" (missing) and "Host-fatal"
  (malformed, expired) is written down and enforced by a dedicated test
  for each of the four documented outcomes.
- Every existing test that builds a real `TempestHost` — all 24 files,
  spanning every prior Work Package — continues to pass completely
  unmodified, proven directly by running the full pre-`WP 6.6` suite
  after this change, not merely assumed safe.
- The cross-service integration pattern (permission check, capability
  check, Settings-customised success message, Audit record,
  Notifications publish, REST route mapping, all at the calling layer)
  is now a concrete, tested precedent any future Licensing-gated
  consumer can copy directly.

**Negative:**

- A license file's own contents are trusted outright, with no
  cryptographic verification of any kind — a genuine, disclosed
  security limitation (`TD-16`), extending this release's own
  local-trust posture (already established for Identity, `ADR-0043`) to
  a second surface.
- A license change requires a full Host restart — `ILicense` is
  immutable for the life of the running process, with no "license
  changed" event or hot-reload mechanism, matching the approved
  contract's own Event Publication Rules exactly ("no plausible 'license
  changed' event once the process is running").

## Alternatives Considered

**Treating a missing license file as Host-fatal, matching a literal
reading of "an invalid license aborts startup."** Rejected — this
would have broken every existing test in this repository that builds a
real `TempestHost`, and every sample/development workflow, directly
contradicting `Risk Register.md`'s own `R5` warning about "an overly
strict interpretation." Also rejected because "missing" and "broken"
are not the same fact: the former is this platform's own normal,
unrestricted-but-uncapable state; the latter is a genuine, actionable
operator error.

**Reusing `WP 5.2`'s own `Func<T>` lazy-accessor pattern to let
Licensing depend on a container-constructed `Persistence` singleton
for its own license storage.** Rejected per `ADR-0050`'s own originally
anticipated reasoning — this would recreate the exact Composition-Root
timing dependency that pattern exists to work around, when reading the
license file directly, with no constructor dependencies at all, avoids
the problem entirely.

**Building cryptographic signature verification for the license file
now.** Rejected — no concrete distribution channel or tamper-threat
model exists yet in this release's own approved scope; disclosed
explicitly as `TD-16` rather than silently trusted without
acknowledgment.

**Adding a `LicenseChangedEvent` published through the Event Bus once a
license file is detected to have changed on disk (file-watching).**
Rejected — no Event Bus exists yet at Licensing's own validation-time
construction point, and no concrete requirement for a running process
to observe a license change without restarting exists in this release's
own approved scope; named explicitly in Future Extension Points as
future, not current, work.

## Related Documents

`docs/releases/v0.6.0/Required ADRs.md` (this decision's own anticipated
form); `docs/releases/v0.6.0/Risk Register.md` (`R5`, whose own open
question this ADR resolves); `Platform Service Contracts.md` (Licensing's
own 15-dimension contract this ADR implements); `ADR-0009` (Composition
Root pattern, confirmed to extend to a leaf validator and a wrapped
provider); `ADR-0013` (platform-service-failure classification, applied
here without modification); `ADR-0023` (`PlatformVersionProvider`'s own
"deliberately a leaf" precedent, mirrored here); `ADR-0043` (local-only
trust model, extended here to license file contents); `ADR-0044`
(the fail-closed-by-default precedent `HasCapability`'s own default
state mirrors); `WP6.6 Implementation Report.md`; `WP6.6 Engineering
Review Report.md`; `WP6.6 Platform Integration Demonstration.md`;
`docs/governance/Quality/Technical Debt Register.md` (`TD-16`, `AT-13`);
`docs/academy/03 Work Packages/WP6.6-licensing-framework-implementation.md`.
