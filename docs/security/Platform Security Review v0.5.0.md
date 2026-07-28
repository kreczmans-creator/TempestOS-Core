# Platform Security Review v0.5.0

## Register Metadata

| Field | Value |
|---|---|
| **Document Name** | Platform Security Review v0.5.0 |
| **Purpose** | The first comprehensive security audit of the entire TempestOS platform — every production project and supporting infrastructure implemented through `WP 5.0D`. Establishes the **v0.5.0 Security Baseline**: from this point forward, every Work Package's Definition of Done includes a check against this baseline (see Security Baseline Statement, below). |
| **Scope** | `Tempest.Core`, `Tempest.App` (including `Tempest.App.Shell`), `Tempest.Samples`, and every supporting subsystem: Dependency Injection, Runtime Host, Module Discovery/Registration/Lifecycle, Plugin Discovery/Loading/Manifest, the Reflection Framework, Event Bus, Navigation, Hosted Services, Logging, Configuration, Versioning, the Exception hierarchy, and bootstrap-era pre-module-pipeline code still compiled into the solution. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | This document; `Threat Model.md`; `Security Principles.md`; `Security Roadmap.md`. |
| **Review Frequency** | This baseline is re-validated by every future Work Package's own Definition of Done (see Security Baseline Statement); a full re-audit of this depth is expected at major release boundaries or when a new threat-model assumption (`Threat Model.md`) goes live. |
| **Last Reviewed** | 2026-07-28 (`WP 5.0S`, Platform Security Baseline Audit). |
| **Related Documents** | `Threat Model.md`; `Security Principles.md`; `Security Roadmap.md`; `Technical Debt Register.md`; `Decision Register.md`. |
| **Related ADRs** | ADR-0017, ADR-0021, ADR-0023, ADR-0025, ADR-0026, ADR-0028, ADR-0029, ADR-0032, ADR-0034. |
| **Related Academy Articles** | `docs/academy/03 Work Packages/WP5.0S-platform-security-baseline-audit.md`. |
| **Coverage Status** | Complete — every audit area named in this Work Package's brief was reviewed; every area either carries a finding below or an explicit no-findings statement. |

---

## Executive Summary

This audit reviewed every production source file across `Tempest.Core`,
`Tempest.App`, and `Tempest.Samples` (119 files at the start of this Work
Package), plus every supporting test and governance artifact, against the
threat model in `Threat Model.md`. **No Critical or High severity
vulnerability was found.** Seven findings were identified, all Medium,
Low, or Informational severity. One (PL-1) was fixed within this Work
Package's own isolated-fix rule; the remainder are genuine future
security debt, correctly deferred to a future, separately-scoped
Work Package because remediation would require an architectural decision
this Work Package's own brief prohibits it from making unilaterally.

The platform's current low risk profile is not an accident of omission —
it is the direct, honest consequence of what TempestOS does not yet do:
no authentication, no networking, no multi-user support, no persistent
handling of sensitive data outside of dead code. The findings below are
concentrated almost entirely in **Future Readiness**: architectural
decisions that are reasonable, even correct, for what the platform is
today, but that will need deliberate attention before the threat model's
own assumptions 4–9 (`Threat Model.md`) go live.

## Scope Reviewed

Every file identified by `find src -name "*.cs"` at the start of this
Work Package, organised by subsystem: Configuration (10 files), Logging
(10 files), Plugins (13 files), Versioning (3 files), Dependency
Injection (all files, including `ResolutionChainFormatter.cs` and
`ServiceProviderExtensions.cs`, not previously read in depth), Modules,
Events, Navigation, BackgroundServices, Runtime, `Tempest.App.Shell`, and
the bootstrap-era `Bootstrap/`, `Hosting/`, `Projects/`, `Repositories/`,
`Models/` directories. Test projects were reviewed to confirm existing
coverage and to add regression tests for the one remediation applied.

## Methodology

1. Re-established current repository state (`PROJECT_STATUS.md`: 446
   tests, `WP 5.0D` most recently landed).
2. Read every previously-unreviewed production source file with a
   security lens (see Scope Reviewed).
3. Re-read, with a security lens specifically, the core subsystems most
   central to trust-boundary questions: `TempestServiceProvider`,
   `ServiceCollection`, `ReflectionFrameworkDiscoveryService`, `EventBus`,
   `HostedServiceManager`, `TempestHost`, `NavigationService`,
   `RuntimeModuleManager`.
4. Ran targeted searches across `src/` for security-relevant patterns:
   `Assembly.Load`/`Activator.CreateInstance`, file/path operations,
   JSON deserialization, hard-coded secrets/credentials, and
   `Console`/exception-disclosure patterns. No hard-coded credential,
   secret, or API key was found anywhere in the codebase.
5. Evaluated every finding against `Threat Model.md`'s actors and assets,
   and classified each per the format below.
6. Applied the one fix judged clearly isolated, non-breaking, and free of
   architectural redesign (PL-1); added targeted regression tests.
7. Re-ran the full build and test suite to confirm no regression.

## Findings by Severity

Each finding states: Severity; Description; Why it matters; Exploit
scenario; Impact; Recommendation; whether it was introduced by this Work
Package or is pre-existing; whether it was fixed, and if not, why not.

### PL-1 — Plugin manifest `AssemblyFileName` permitted path traversal outside its own folder (Medium) — **FIXED**

- **Description.** `PluginManifestDiscoveryService.ParseAndValidate`
  resolved a manifest's declared `AssemblyFileName` via
  `Path.GetFullPath(Path.Combine(folder, dto.AssemblyFileName))`, with no
  check that the result stayed within `folder`. `Path.Combine` discards
  its first argument entirely when the second is rooted (an absolute
  path), and a relative `../../` sequence resolves upward through the
  filesystem exactly as it would in a shell.
- **Why it matters.** A plugin manifest is untrusted, external input —
  parsed from a JSON file discovered on disk, not authored by the
  platform. The manifest's own implicit contract ("this plugin's
  assembly lives in its own folder") was not enforced by code, only by
  convention.
- **Exploit scenario.** A manifest declaring
  `"AssemblyFileName": "../../../SomeOtherPlugin/Payload.dll"`, or an
  absolute path such as `"C:\\Windows\\Temp\\Payload.dll"`, would resolve
  and load successfully — the loader would load whatever assembly the
  manifest pointed at, anywhere on disk the process could read, not only
  the plugin's own declared folder.
- **Impact.** Given `Threat Model.md` Scenario 1 (plugins already run
  with full process trust once loaded), this was not, by itself, a
  privilege-escalation path today — a plugin can already do anything the
  process can do once it loads at all. Its impact is in what it
  *doesn't* prevent for the future: any later manifest-scoped permission
  or signing scheme would need this containment to already hold, and it
  did not.
- **Recommendation.** Reject a manifest whose resolved `AssemblyPath`
  falls outside its own candidate folder, at discovery time, the same way
  every other malformed-manifest case is already isolated (ADR-0025).
- **Introduced by this Work Package or pre-existing.** Pre-existing,
  since `WP 4.2`/`ADR-0026` first implemented `PluginManifestDiscoveryService`.
  Not previously identified — this audit is the first security-focused
  review of this code.
- **Fixed.** Yes, in this Work Package. `ParseAndValidate` now computes
  the candidate folder's own normalised form and rejects (via the
  existing `InvalidPluginManifestException`, isolated and logged at
  `Warning` exactly like every other malformed-manifest case) any
  `AssemblyFileName` whose resolved path does not fall within it. Two
  regression tests were added: one for a relative `../` escape, one for
  an absolute path outside the folder. This is an isolated, non-breaking
  change — it tightens validation of already-untrusted input using the
  same isolation mechanism every other manifest validation failure
  already uses; it introduces no new architecture, no new trust boundary,
  and no new public surface.

### SEC-01 — No isolation boundary between a loaded plugin and a first-party module (Medium) — **NOT FIXED (architectural; deferred)**

- **Description.** Once `PluginAssemblyLoader.LoadPlugins` loads a
  plugin's assembly via `Assembly.LoadFrom`, that assembly is
  indistinguishable, from `ReflectionFrameworkDiscoveryService` onward,
  from a first-party module assembly. Plugin loading runs before module
  discovery in `TempestHost.ExecuteStartupPhasesAsync`, so any `IModule`
  the plugin assembly declares is discovered, registered, and given
  identical DI-container access to every first-party module: the same
  `IEventBus`, `INavigationProvider`, `IConfigurationProvider`, `ILogger`,
  and every other first-party module's own concrete type.
- **Why it matters.** ADR-0025/ADR-0026 isolate a plugin's *discovery and
  loading* failures from crashing the Host — a reliability guarantee.
  They do not, and were never intended to, isolate a *successfully
  loaded* plugin's runtime behaviour from the rest of the process. There
  is currently no capability model, no manifest-declared permission
  scope, no code signing, and no process/AppDomain-level isolation.
- **Exploit scenario.** A third-party plugin (once assumption 7 in
  `Threat Model.md` becomes real) could read or exfiltrate any data any
  other module can reach, subscribe to any event any other module
  publishes, register navigation entries indistinguishable from
  first-party ones, or resolve and interact with any other module's
  concrete type directly, all without violating a single existing
  contract in the codebase — because no contract currently forbids it.
- **Impact.** None today — every module and plugin in the current
  codebase is first-party and trusted; there is no untrusted plugin
  author yet. This is entirely prospective debt against assumption 7.
- **Recommendation.** Before third-party plugins are supported in
  practice, a dedicated Architecture Work Package should design a real
  isolation boundary — candidates include a separate
  `AssemblyLoadContext` per plugin, a manifest-declared, enforced
  capability/permission scope, and code-signing verification before
  load. This is squarely an architectural redesign and is explicitly out
  of this Work Package's scope to design or implement.
- **Introduced by this Work Package or pre-existing.** Pre-existing —
  an accepted consequence of `ADR-0025`/`ADR-0026`'s own scope decision
  (see `Technical Debt Register.md` AT-06). This audit is the first to
  name it explicitly as a security concern rather than only a
  reliability/isolation one.
- **Fixed.** No. Per this Work Package's own brief: "Do not redesign
  plugin security. Identify future security debt." Recorded as
  `Technical Debt Register.md` TD-09 and as the first entry in `Security
  Roadmap.md`.

### NAV-1 — `NavigationService.Unregister` performs no ownership check (Low) — **NOT FIXED (architectural; deferred)**

- **Description.** `NavigationService.Unregister(string id)` removes
  whatever `NavigationItem` is registered under `id`, with no check that
  the caller is the same component that registered it.
- **Why it matters.** Any component holding an `INavigationProvider`
  reference — which, per SEC-01, includes any loaded plugin — can
  unregister *any other* component's navigation entry by ID, whether or
  not it registered that entry itself.
- **Exploit scenario.** A misbehaving or malicious module/plugin calls
  `Unregister("clock.overview")` (or any other well-known navigation ID)
  to silently remove a sibling module's menu entry, disrupting the
  application without ever touching that module's own code.
- **Impact.** Low today: single-user, all-trusted, first-party modules
  only; the worst case is a first-party module accidentally colliding
  with or removing another's entry, already partially guarded against by
  `Register`'s own duplicate-ID rejection. Becomes materially more
  relevant once SEC-01's plugin trust boundary is addressed, since an
  ownership check on `Unregister` is the natural companion fix.
- **Recommendation.** When `SEC-01`/`TD-09` is addressed by a future
  Architecture Work Package, extend the same design to give
  `NavigationService` (and any similarly shaped shared registry) an
  ownership or capability check on removal, not only on initial
  registration.
- **Introduced by this Work Package or pre-existing.** Pre-existing,
  since `WP 5.0B`/`ADR-0032` implemented `NavigationService`.
- **Fixed.** No — an ownership/capability model is an architectural
  addition, not an isolated fix. Recorded as `Technical Debt Register.md`
  TD-10 and in `Security Roadmap.md`.

### SEC-02 — No secrets-redaction convention exists in the logging framework (Low) — **NOT FIXED (premature; no live need)**

- **Description.** `ILogger`/`ILogSink`/`ConsoleLogSink` write every
  message, exception, and structured property to the console verbatim.
  There is no mechanism to mark a value as sensitive so it is
  automatically redacted before reaching a sink.
- **Why it matters.** No secret, credential, or connection string exists
  anywhere in the codebase today (confirmed by direct search), so nothing
  is at risk right now. But nothing in the logging framework would
  prevent a future component from logging one in plaintext by accident,
  and there is no convention yet to reach for that would stop it.
- **Exploit scenario.** Once assumption 5 (authentication) or a future
  cloud-sync credential is introduced, a call such as
  `logger.Information($"Connecting with token {token}")` would work today
  exactly as written — nothing catches it.
- **Impact.** None today (no secret-bearing data exists to leak).
  Entirely prospective.
- **Recommendation.** Before credentials, tokens, or connection strings
  are introduced anywhere in the platform, design a redaction convention
  (a marker attribute, a `SensitiveValue` wrapper type, or an
  `ILogSink`-level filter) and require its use at that point — not
  before, and not after.
- **Introduced by this Work Package or pre-existing.** Pre-existing —
  the logging framework (`WP 2.6`) was never designed with secrets in
  mind because none existed to design for.
- **Fixed.** No. Building a redaction convention today, with nothing yet
  to redact, would be exactly the kind of speculative improvement this
  Work Package's brief prohibits ("do not implement speculative
  improvements"). Recorded in `Security Roadmap.md`, sequenced to trigger
  before assumption 5 or 8 (`Threat Model.md`) is implemented.

### FS-1 — Bootstrap-era dead code models sensitive project data with no security controls (Informational) — **NOT FIXED (dead code; not reachable)**

- **Description.** `JsonProjectRepository`/`ProjectModel`
  (`src/Tempest.Core/Repositories/`, `src/Tempest.Core/Models/`) already
  model `Classification`, `SecurityLevel` (defaulting to `"BPSS"`),
  `ExportControlled`, `Customer`, and `ContractNumber` fields, persisted
  as plain, unencrypted JSON via `File.WriteAllText`/`JsonSerializer`,
  with no access control and no audit trail. This code has been fully
  unreferenced by `Program.cs` since `WP 5.0D` (already disclosed in
  `Technical Debt Register.md`).
- **Why it matters.** This is the clearest concrete evidence in the
  repository of where `Threat Model.md` assumptions 1–3 are headed. It
  currently poses no live risk because nothing calls it, but it must not
  simply be "switched back on" unchanged whenever project data storage is
  revived.
- **Exploit scenario.** N/A today — the code is unreachable from any
  entry point in the running application.
- **Impact.** None today.
- **Recommendation.** When a real project-data subsystem is designed
  (whether reviving this code or replacing it), encryption at rest,
  access control, and audit logging for classified/export-controlled
  fields must be designed in from the start, not retrofitted. See
  `Security Roadmap.md`.
- **Introduced by this Work Package or pre-existing.** Pre-existing,
  unauthored-origin (predates Claude-developed history).
- **Fixed.** No — not applicable; there is nothing to fix in code that is
  not executed. Recorded in `Security Roadmap.md` as a design
  prerequisite for the eventual project-data subsystem.

### FS-2 — Hard-coded, Windows-only default workspace root (Informational) — **NOT FIXED (dead code path; low priority)**

- **Description.** `ApplicationConfiguration.WorkspaceRoot` defaults to
  the literal `@"C:\Tempest"`. `HostingService.Initialise` creates this
  directory (and its subdirectories) unconditionally.
- **Why it matters.** A fixed, drive-rooted path outside any user
  profile is a portability and least-privilege concern (it may require
  elevated permissions, and collides across users on a shared machine) —
  more relevant once assumption 4 (multi-user) or non-Windows deployment
  is real.
- **Exploit scenario.** N/A — this is a configuration/portability
  concern, not an exploitable vulnerability.
- **Impact.** None today; this code path is unreferenced by `Program.cs`
  since `WP 5.0D`, identically to FS-1.
- **Recommendation.** When this bootstrap-era code is revisited (see
  `Technical Debt Register.md` TD-01/TD-02 migration triggers), default
  to a per-user, per-OS-appropriate location rather than a fixed,
  platform-specific root.
- **Introduced by this Work Package or pre-existing.** Pre-existing,
  unauthored-origin.
- **Fixed.** No — dead code path, low priority; recorded in `Security
  Roadmap.md`.

### FR-1 — No per-user/per-tenant scope exists in the dependency injection container (Informational) — **NOT FIXED (no live need)**

- **Description.** `TempestServiceProvider` supports exactly two
  lifetimes, Singleton and Transient — there is no Scoped lifetime.
  Every platform service (`EventBus`, `NavigationService`, every
  discovered module) is a single, process-wide instance.
- **Why it matters.** Assumption 4 (multi-user support) has no
  architectural home to land in today: there is no mechanism to isolate
  one user's event subscriptions, navigation state, or module instances
  from another's within a single running process.
- **Exploit scenario.** N/A — no multi-user capability exists to exploit.
- **Impact.** None today; TempestOS is a single-user, single-process
  application in every scenario the codebase currently supports.
- **Recommendation.** When assumption 4 becomes real, evaluate whether
  multi-user support is achieved via separate OS processes per user
  (requiring no DI change) or via a genuine Scoped lifetime and
  per-tenant isolation model (requiring a DI redesign) — this decision
  should be made deliberately, with its own ADR, not by accretion.
- **Introduced by this Work Package or pre-existing.** Pre-existing —
  a reasonable design for a single-user application, which is all
  TempestOS has been asked to be so far.
- **Fixed.** No — building multi-tenancy support with no tenant to
  support would be speculative. Recorded in `Security Roadmap.md`.

## Areas Reviewed With No Findings

**Architecture.** Reviewed. Layering (ADR-0023) and the Discovery/
Registration/Lifecycle exclusion from the DI container (ADR-0017,
regression-tested at `WP 5.0D`) both hold exactly as documented. No
Platform Service was found exposing more surface than its own interface
requires. The one architecture-level concern this audit surfaced — the
plugin trust boundary — is recorded as SEC-01 under Plugin Infrastructure,
not duplicated here. No security vulnerability or architectural security
concern was otherwise identified.

**Dependency Injection.** Reviewed. No service-locator risk exists:
`ITempestServiceProvider` itself is never registered into the container,
so no module or plugin can obtain a reference to the container that
constructed it. Constructor selection (exactly one public constructor,
or a descriptive exception) is deterministic; singleton construction is
guarded by a single lock, and .NET `lock`'s per-thread re-entrancy
confirms recursive singleton resolution on one thread cannot deadlock.
`ServiceCollection`'s last-registration-wins semantics were confirmed
safe in practice: every module and hosted service is registered under
its own concrete type (never a shared interface type), so no
registration can silently shadow a core platform service. No security
vulnerability or architectural security concern was identified beyond the
Scoped-lifetime future-readiness observation (FR-1).

**Runtime Host.** Reviewed. `TempestHost`'s state machine, shutdown
sequencing, and disposal are all lock-guarded, idempotent, and
guarantee cleanup regardless of how far startup progressed — confirmed by
direct reading of `RunAsync`, `StopAsync`, and `DisposeAsync`. A critical
hosted service's failure to stop correctly faults the Host rather than
leaving it in an ambiguous state. No security vulnerability or
architectural security concern was identified.

**Reflection & Discovery.** Reviewed.
`ReflectionFrameworkDiscoveryService` correctly handles
`ReflectionTypeLoadException` from malformed assemblies without
crashing, validates every discovered module's metadata before accepting
it, and rejects duplicate module IDs. Its reliance on `AppDomain`
assembly-load side effects for discovery is a known fragility (the
`const`-field/assembly-loading finding already disclosed at `WP 5.0D`),
not a new one. The one genuine security implication of this subsystem —
that it treats a loaded plugin assembly identically to a first-party one
— is recorded as SEC-01, not duplicated here.

**Event Bus.** Reviewed. `Unsubscribe` requires the caller to already
hold a reference to the specific handler instance being removed — unlike
`NavigationService.Unregister` (NAV-1), there is no string-keyed removal
a caller could spoof or guess. Publish takes a locked snapshot before
dispatching outside the lock, so a re-entrant publish or a
subscribe/unsubscribe during dispatch cannot corrupt iteration or
deadlock. Per-subscriber exceptions are isolated and logged;
`OperationCanceledException` is deliberately not isolated. No security
vulnerability or architectural security concern was identified beyond
the already-disclosed accepted trade-offs (`Technical Debt Register.md`
AT-01/AT-02/AT-03).

**Hosted Services.** Reviewed. Critical-service escalation, non-critical
isolation, deterministic start/stop ordering, and lock-guarded state
transitions were all confirmed by direct reading of
`HostedServiceManager`. No security vulnerability or architectural
security concern was identified beyond the already-disclosed accepted
trade-offs (`Technical Debt Register.md` AT-04/AT-05).

**Logging.** Reviewed. No hard-coded secret, credential, or API key
exists anywhere in the codebase (confirmed by direct search). Exception
detail (including stack traces) is written to console — reasonable for a
local diagnostic sink with no other consumer today. The one forward-
looking gap (no redaction convention for a future secret) is recorded as
SEC-02, not duplicated here.

**Exception Framework.** Reviewed. Every exception hierarchy in the
codebase (`PluginException`, `ModuleDiscoveryException`,
`ServiceResolutionException`, and others) carries only type names,
identifiers, and descriptive messages — no exception was found to embed
a secret, credential, or other sensitive runtime value. Resolution-chain
formatting (`ResolutionChainFormatter`) discloses only `Type.Name`
values, which are compiled, public information, not a security concern.
No security vulnerability or architectural security concern was
identified.

**File System.** Reviewed. Every path constructed from external or
manifest-declared input was checked for traversal risk. The one gap
found (PL-1) has been fixed. `JsonProjectRepository`'s own paths are
built only from an auto-generated project ID and a fixed subfolder list
— no externally-controlled string reaches `Path.Combine` unvalidated in
the live pipeline. FS-1 and FS-2 are recorded as informational,
forward-looking observations about currently-unreachable code, not live
vulnerabilities.

**Thread Safety.** Reviewed. Every shared, mutable platform service
(`EventBus`, `NavigationService`, `RuntimeModuleManager`,
`HostedServiceManager`, `TempestServiceProvider`, `TempestHost`) uses a
single, private `_gate` lock object, consistently applied, with no
nested cross-class locking pattern found that could deadlock.
`TempestServiceProvider`'s coarse-grained, provider-wide singleton lock
serialises all singleton construction (a scalability note, not a
correctness or security defect) but is not deadlock-prone, because
.NET's `Monitor`-based `lock` is reentrant per thread. No security
vulnerability or architectural security concern was identified.

**Resource Management.** Reviewed. `TempestHost.DisposeAsync` is
idempotent and guarantees hosted-service and module teardown regardless
of how the Host reached disposal. No new resource-management concern was
identified beyond the already-disclosed `Technical Debt Register.md`
TD-03 (no disposal tracking for `IDisposable` singletons — no current
platform service is disposable) and AT-01/AT-02 (Event Bus subscriber
lifetime).

**Input Validation.** Reviewed. Every external-data entry point examined
— `ConfigurationBuilder.ValidateEntry`, `PluginManifestDiscoveryService.
ParseAndValidate`/`RequireField`, `ReflectionFrameworkDiscoveryService.
ValidateMetadata`, `RuntimeModuleManager.Register`'s ID validation —
rejects null, empty, whitespace, and malformed values before constructing
a domain object, and does so via a descriptive, isolated exception rather
than a silent default. No security vulnerability or architectural
security concern was identified beyond PL-1, which has been fixed.

**Future Readiness.** Reviewed in full — see SEC-01, SEC-02, FS-1, FS-2,
FR-1, and NAV-1, each of which is a future-readiness finding at its
core, and `Security Roadmap.md`, which sequences all of them against the
`Threat Model.md` assumption that would first require each one to be
addressed: authentication and authorisation (assumption 5 — no readiness
work exists yet; recommend designing this before assumption 5, not
during), encryption and secrets management (SEC-02), audit logging
(FS-1), licensing (no readiness work exists yet; no licensing concept
exists in the codebase to review), APIs and networking (no surface
exists yet to review), cloud synchronisation and multi-user operation
(FR-1), offline synchronisation and mobile devices (no readiness work
exists yet — out of scope until a concrete design exists to review).

## Remediations Applied

| ID | Remediation | Files Changed |
|---|---|---|
| PL-1 | `PluginManifestDiscoveryService.ParseAndValidate` now rejects a manifest whose resolved `AssemblyFileName` falls outside its own candidate folder | `src/Tempest.Core/Plugins/PluginManifestDiscoveryService.cs`; `tests/Tempest.Core.Tests/Plugins/PluginManifestDiscoveryServiceTests.cs` (2 new regression tests) |

No other finding was remediated in this Work Package — each remaining
finding requires either an architectural decision (SEC-01, NAV-1, FR-1)
or has no live need to justify building it yet (SEC-02, FS-1, FS-2), per
this Work Package's own brief.

## Future Security Recommendations

See `Security Roadmap.md` for the complete, prioritised, sequenced list.
In summary: (1) design plugin isolation before third-party plugins ship
(SEC-01/TD-09); (2) design navigation ownership alongside it (NAV-1/
TD-10); (3) design a secrets-redaction logging convention before any
credential is introduced (SEC-02); (4) design encryption/access-control/
audit-logging for project data before the bootstrap-era data model is
revived or replaced (FS-1); (5) make a deliberate multi-user architecture
decision, with its own ADR, before assumption 4 is implemented (FR-1).

## Governance Updates

- `Technical Debt Register.md`: added TD-09 (plugin trust boundary) and
  TD-10 (Navigation ownership gap); "Last Reviewed" updated.
- `Decision Register.md`: added D-017 (conducting `WP 5.0S` as a formal,
  dedicated security audit Work Package, establishing `docs/security/`
  and the v0.5.0 Security Baseline convention).
- `Governance Index.md`: added a new "Security" section indexing
  `docs/security/`'s four documents, alongside the existing Architecture/
  Engineering/Quality/Documentation/Delivery categories.
- `Documentation Register.md`: added a `docs/security/` row to the
  Directory Map.

## Documentation Created

- `docs/security/Threat Model.md`
- `docs/security/Security Principles.md`
- `docs/security/Platform Security Review v0.5.0.md` (this document)
- `docs/security/Security Roadmap.md`

## Academy Updates

- `docs/academy/03 Work Packages/WP5.0S-platform-security-baseline-audit.md`
  (new) — teaches threat modelling, secure platform design, secure
  plugin architecture, trust boundaries, least privilege, and secure
  engineering practice, assuming no prior security background.
- `docs/academy/Academy Index.md` and `Academy Register.md` updated to
  list it.

## Security Baseline Statement

**As of 2026-07-28, TempestOS has no known Critical or High severity
security vulnerability.** Seven findings were identified during this
audit: one (PL-1) has been fixed; the remaining six are disclosed,
future-facing security debt, each requiring either a live need that does
not yet exist or an architectural decision out of this Work Package's
own scope to make unilaterally. This document, together with `Threat
Model.md`, `Security Principles.md`, and `Security Roadmap.md`,
constitutes the **v0.5.0 Security Baseline**. From this point forward,
every Work Package's Definition of Done should include: *this Work
Package shall not weaken the approved Platform Security Baseline
established by `WP 5.0S`; a review against this baseline is part of the
Definition of Done.*

## Test Totals

446 pre-existing tests (as of `WP 5.0D`) + 2 new regression tests (PL-1)
= **448 tests, 448 passing, 0 failing.**

## Build Status

`dotnet build src/TempestOS.slnx` — 0 warnings, 0 errors.
`dotnet test src/TempestOS.slnx` — 448/448 passing.

## Commit Hash

*(this commit)* — resolved to the historical commit hash in a later Work
Package, per this project's established convention for avoiding the
self-reference-hash paradox (see `WP 5.0A`–`WP 5.0D` for precedent).
