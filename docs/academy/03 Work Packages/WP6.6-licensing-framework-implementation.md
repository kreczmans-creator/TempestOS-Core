# WP 6.6 — Licensing Framework Implementation

## 1. Introduction

WP 6.6 delivers the Licensing Framework — the final production
implementation Work Package of the Platform Services phase (`v0.6.0`);
only `WP 6.8` (Platform Services Integration Review, certification)
remains after it. The sixth of eight implemented Work Packages to be
sequenced ahead of its own nominal numeric position, per `Platform
Service Implementation Order.md`'s own explicit recommendation.
Implemented in a single pass, directly against the already-approved
architecture and Contract Review packages — no separate architecture
phase, mirroring every one of its seven predecessors. Unlike any of
them, this Work Package required changing `TempestHost`'s own startup
sequence itself, not merely adding a Phase 6 registration — Licensing
is the only proposed `v0.6.0` service that runs before the DI container
exists at all.

## 2. Purpose

To build `Tempest.Core.Licensing` exactly as the approved architecture
specified — `ILicense`, `ILicenseValidator`, `ILicenseProvider` — as a
service that exposes capability only, never commercial policy; to
resolve the placement question `Required ADRs.md` named as this Work
Package's own required ADR (pre-container leaf, Host-fatal on invalid);
and to resolve `Risk Register.md`'s own `R5` residual question — whether
every category of "invalid" license (missing, expired, malformed)
genuinely warrants Host-fatal treatment, or whether some should degrade
to a reduced-capability running state instead.

## 3. Background

`WP 6.1` (Permissions & Identity), `WP 6.4` (Settings Framework), `WP
6.5` (Audit Framework), `WP 6.2` (Notification Framework), `WP 6.0`
(Reporting Framework), `WP 6.3` (REST API), and `WP 6.7`
(Export/Import) were all already implemented. `Platform Service
Implementation Order.md`'s own dependency analysis named Licensing as
"no proposed-service dependency of any kind; fully independent leaf,"
free to implement at any point — this Work Package landed last among
the eight anyway, since it was authorised last. `Risk Register.md`'s
own `R5` had been sitting open since the architecture phase, explicitly
naming this Work Package as the point where its own question needed a
real answer, not a deferred one.

## 4. The Problem

Two things needed resolving, one of which the approved contract itself
left genuinely open:

1. **A way to validate licensing state before anything else in the
   Host exists** — `ILicenseValidator` must run before the DI
   container, and even before the logger, is built, since an invalid
   license should abort startup at the earliest possible point.
2. **What "invalid" actually means, operationally** — the approved
   contract said an invalid license is Host-fatal, but never defined
   which concrete file states count as invalid. Naively treating a
   *missing* license file as invalid would have made every one of the
   24 pre-existing test files that build a real `TempestHost` — none
   of which has ever supplied one — fail immediately, along with every
   sample and development workflow this platform has ever shipped.

## 5. The Design

**Implemented exactly as drafted, zero signature deviation:**
`ILicense.LicenseeName`/`ExpiresAt`/`EnabledCapabilities`,
`ILicenseValidator.Validate()`, `LicenseValidationResult`,
`ILicenseProvider.CurrentLicense`/`HasCapability`. `LicensingException`
is a concrete, base-plus-subtype type (not abstract, mirroring
`ReportingException`'s/`ExportImportException`'s own established
convention), with `LicenseValidationException` as its one approved
subtype.

**Pre-container placement (`ADR-0050`):** `LicenseValidator` has no
constructor dependencies at all — deliberately a leaf, mirroring
`PlatformVersionProvider`'s own position. `TempestHost` constructs it
directly, immediately after `ConfigurationBuilder.Build()` returns and
before the logger/sink are built. A minimal `Console.Error.WriteLine`
records a validation failure before throwing, since no logger exists
yet; a success is logged retroactively once the logger is built,
alongside the existing "Configuration Built"/"Logging Built" lines.

**`R5`'s own resolution, the genuine architectural decision this Work
Package settled:** a missing license file is not itself invalid — it
produces a valid, unrestricted-but-uncapable default
(`LicenseValidator.UnlicensedLicenseeName`, zero enabled capabilities),
never Host-fatal. A license file that exists but is unreadable, is not
valid JSON, is missing its own required `LicenseeName` field, or has
already expired, *is* Host-fatal — someone deliberately supplied it,
and it is broken. `LicenseValidationException` propagates through
`TempestHost.ExecuteStartupPhasesAsync` to `RunAsync`'s own existing
`catch (Exception ex) { EnterFaulted(ex); throw; }` block — Host-fatal
per `ADR-0013`, with zero modification to `RunAsync` itself.

**`ILicenseProvider` is Composition-Root-constructed and `AddInstance`-registered
at Phase 6**, immediately after Identity & Permissions — exactly like
`IPlatformVersionProvider` and `IDiagnosticsProvider` before it, wrapping
the already-validated `ILicense` from before Phase 1.

**No cryptographic signature verification of the license file's own
contents:** trusted at face value, disclosed as `TD-16`, mirroring
`TD-13`'s own precedent for the REST API's undisclosed-authentication
gap.

**Cross-service integration, entirely at the sample-module layer:**
`LicensingSampleModule`'s own `CheckSampleCapabilityCommandHandler`
checks a permission (Identity), checks a sample capability
(`ILicenseProvider.HasCapability`), reads a Settings-provided message on
success (Settings), records the outcome (Audit), and publishes a
completion notice (Notifications) — then the same command is mapped to
an HTTP route (REST API), proven by a real HTTP round trip returning
`200`/`400`/`403` for licensed/unlicensed/denied respectively. Persistence
and Reporting are both deliberately not consumed.

## 6. Alternatives Considered

See `ADR-0050` for the complete reasoning. In summary: treating a
missing license file as Host-fatal was rejected — it would have broken
every existing test and workflow this platform has ever shipped, and
conflates "no one supplied a license" with "someone supplied a broken
one," two genuinely different facts. Reusing `WP 5.2`'s own `Func<T>`
lazy-accessor pattern to let Licensing depend on a container-constructed
Persistence singleton was rejected — it would recreate the exact
Composition-Root timing problem that pattern exists to work around.
Building cryptographic signature verification now was rejected — no
concrete distribution channel or tamper-threat model exists yet in this
release's own approved scope.

## 7. Why This Solution Was Chosen

Every approved interface is implemented with zero deviation, so any
future Licensing-gated consumer can depend on
`ILicense`/`ILicenseValidator`/`ILicenseProvider` with full confidence
in their shape. `R5`'s own open question is now answered precisely, not
vaguely, and proven correct by running the full pre-existing test suite
unmodified rather than merely arguing it would be fine. Reusing
`PlatformVersionProvider`'s own leaf-construction pattern and
`IPlatformVersionProvider`/`IDiagnosticsProvider`'s own `AddInstance`
registration pattern meant no new Composition Root mechanism needed
inventing for a service with genuinely novel timing constraints.

## 8. Architectural Principles

- **Verify a Behavioural Change Against the Full Existing Suite, Not
  Just Reason About It** — the "missing file is not invalid" resolution
  was proven by actually running all 24 `TempestHost`-building test
  files afterward, not by arguing they would probably still pass.
- **Reuse a Proven Construction Pattern Before Inventing a New One** —
  `LicenseValidator`'s leaf construction mirrors `PlatformVersionProvider`
  exactly; `LicenseProvider`'s registration mirrors
  `IDiagnosticsProvider` exactly.
- **Two Facts That Sound Similar Deserve Different Treatment When a
  Caller Needs to React Differently** — "no license supplied" and "a
  license was supplied and it's broken" are different operator
  situations, not two flavours of the same error.
- **Disclose a Security Limitation Loudly, Never Build It to Look More
  Secure Than It Is** — the absence of cryptographic verification is
  named directly, in the ADR, the retrospective, and the Technical Debt
  Register.

## 9. Files Added

`src/Tempest.Core/Licensing/ILicense.cs`;
`src/Tempest.Core/Licensing/License.cs`;
`src/Tempest.Core/Licensing/ILicenseValidator.cs`;
`src/Tempest.Core/Licensing/LicenseValidationResult.cs`;
`src/Tempest.Core/Licensing/ILicenseProvider.cs`;
`src/Tempest.Core/Licensing/LicensingException.cs`;
`src/Tempest.Core/Licensing/LicenseValidationException.cs`;
`src/Tempest.Core/Licensing/LicenseDto.cs`;
`src/Tempest.Core/Licensing/LicenseValidator.cs`;
`src/Tempest.Core/Licensing/LicenseProvider.cs`;
`src/Samples/Tempest.Samples/LicensingSampleModule.cs`;
`src/Samples/Tempest.Samples/CheckSampleCapabilityCommand.cs`;
`src/Samples/Tempest.Samples/CheckSampleCapabilityCommandHandler.cs`;
`tests/Tempest.Core.Tests/Licensing/LicenseValidatorTests.cs`;
`tests/Tempest.Core.Tests/Licensing/LicenseProviderTests.cs`;
`tests/Tempest.Core.Tests/Licensing/ExceptionTests.cs`;
`tests/Tempest.Core.Tests/Runtime/LicenseHostRegistrationTests.cs`;
`tests/Tempest.Core.Tests/Samples/LicensingSampleModuleIntegrationTests.cs`;
`docs/adr/ADR-0050-license-validation-is-a-host-startup-host-fatal-gate.md`;
this retrospective. **Modified:**
`src/Tempest.Core/Runtime/TempestHost.cs` (license validation gate,
Phase 6 registration); `src/Tempest.Core/Runtime/TempestHostBuilder.cs`
(license file path test-seam override, mirroring the existing
telescoping-constructor convention); `tests/Tempest.Core.Tests/Samples/ClockModuleDiscoveryTests.cs`
(sample-module count, 14 → 15).

## 10. Trade-offs

- **No cryptographic license file signature verification** (`TD-16`) —
  a disclosed, deliberate limitation; license file contents are trusted
  at face value, mirroring the REST API's own undisclosed-authentication
  precedent (`TD-13`).
- **No remote validation/activation, floating/seat-based licensing, or
  renewal/grace-period model** (`AT-13`) — all named explicitly in the
  approved contract's own Future Extension Points; a license change
  requires a full Host restart in this release.

## 11. Common Mistakes

- **Assuming a missing license file should be treated the same as a
  malformed one** — it is not; a missing file is this platform's own
  normal, unrestricted-but-uncapable default state, never Host-fatal,
  while a malformed or expired one is a genuine, actionable operator
  error.
- **Assuming `ILicenseValidator` can be resolved through the DI
  container** — it cannot, by design; it runs before the container
  exists, and attempting to resolve it via `host.Services!.GetService`
  throws `ServiceNotRegisteredException`, proven directly by a
  dedicated test.
- **Assuming `ILicenseProvider.HasCapability` enforces anything on its
  own** — it does not; it only answers a question. Permission
  enforcement, commercial policy, and what happens when a capability is
  absent are all the calling layer's own responsibility.

## 12. Future Evolution

Remote validation/activation, floating/seat-based licensing, and a
license-renewal/grace-period model once a concrete deployment scenario
names any of the three as a requirement; cryptographic signature
verification once a concrete distribution channel and tamper-threat
model exist — all named explicitly as future, separately-scoped
responsibilities, not designed now.

## 13. Key Takeaways

1. When an approved contract states a category of failure is
   Host-fatal without precisely defining every case that belongs to
   that category, resolving the ambiguity requires checking the
   platform's own existing usage first — every prior Work Package's own
   test suite and every sample workflow already assumes no license file
   exists, which is itself strong evidence for what the correct default
   must be.
2. A construction-timing constraint that looks novel ("this service
   must exist before the container does") is usually not actually
   novel — this codebase had already solved it twice
   (`PlatformVersionProvider`, `IDiagnosticsProvider`); reusing the
   established shape is safer than designing a third one.
3. Proving a behavioural change is safe by running the full existing
   test suite against it is strictly stronger evidence than reasoning
   that it "should" be fine — this Work Package's own `R5` resolution
   was verified this way, not merely argued.

## Architectural Debt Assessment

`docs/governance/Quality/Technical Debt Register.md` gained one new
tracked debt item (`TD-16`) and one new trade-off (`AT-13`); no existing
Technical Debt item required annotation — Licensing introduces no
instance of any previously-tracked gap.

## Observations

This Work Package's own repository review, re-deriving every touched
register directly, found no further stale figures beyond the
`Interface Register.md`/`Dependency Injection Register.md`/`Module
Register.md` gap `WP 6.7` had already disclosed as `Partial` (missing
every interface, DI registration, and sample module `WP 6.1` through
`WP 6.3` added) — this Work Package added only its own three new
interfaces, one new registration, and one new module to each register,
leaving the larger, six-Work-Package backfill exactly where `WP 6.7`
left it, for `WP 6.8`'s own closing audit to perform systematically.

## Related Documents

`docs/releases/v0.6.0/Release Architecture.md` and companions (the
architecture package this Work Package implemented); `Platform Service
Contracts.md` and companions (the Contract Review package); `ADR-0009`
(Composition Root pattern, confirmed to extend to a leaf validator and
a wrapped provider); `ADR-0013` (platform-service-failure
classification, applied here without modification); `ADR-0023`
(`PlatformVersionProvider`'s own "deliberately a leaf" precedent,
mirrored here); `ADR-0043` (local-only trust model, extended here to
license file contents); `ADR-0044` (the fail-closed-by-default
precedent `HasCapability`'s own default state mirrors); `ADR-0050`;
`docs/architecture/Platform Service Map.md` (Licensing entry);
`docs/governance/Quality/Technical Debt Register.md` (`TD-16`, `AT-13`);
`docs/releases/v0.6.0/Risk Register.md` (`R5`); `docs/academy/03 Work
Packages/WP6.3-rest-api-implementation.md`,
`WP6.7-export-import-implementation.md` (the precedents this Work
Package's own single-pass implementation approach and empirical-
verification discipline follow).
