# TempestOS v0.6.0 — Testing Strategy

## Baseline

This release extends, and does not replace, the existing Engineering
Standard (`docs/academy/06 Engineering Standards/02-testing-strategy.md`)
and `docs/releases/v0.4.0/Testing.md`'s own precedent for a release-scoped
testing document. Every convention established across `v0.2.0`–`v0.5.0`
applies unchanged: the internal-test-seam pattern for ambient/broad
contracts, minimal and clearly-separated test fixtures, explicit
test-category coverage matched to each Work Package's own brief,
regression tests named for the scenario they reproduce, deterministic
coordination (never fixed sleeps or timing windows) for any concurrency-
sensitive test, and a full build/test run from a clean, committed tree
before any Work Package is reported done.

Starting point: **552 tests passing, 0 warnings, 0 errors** (the
`v0.5.0` baseline, confirmed at release). Every Work Package in this
release must leave that number undiminished and unbroken before its own
new tests are even considered.

## Test Category Definitions

Six categories are used consistently across every Work Package below —
defined once here rather than redefined per row:

- **Unit Tests** — a single component's own behaviour, in isolation,
  with collaborators faked/stubbed where needed — the large majority of
  this platform's existing 552 tests.
- **Integration Tests** — two or more real, unmodified components
  working together (e.g., against the real, unmodified `TempestHost`,
  mirroring `TempestHostTests`' own established philosophy) — no fakes
  standing in for a platform service under test.
- **Failure Injection Tests** — a deliberately-forced failure path
  (a thrown exception from a dependency, a simulated I/O failure, an
  unauthorized caller) proving the documented failure behaviour in
  `Platform Service Contracts.md` actually holds, not merely the happy
  path.
- **Regression Tests** — a test written specifically because a named,
  historical gap or bug (a `TD-`/`AT-` item, a prior release's own
  finding) must not recur — named for the scenario it reproduces, per
  the existing Engineering Standard.
- **Performance Tests** — where a stated performance expectation exists
  in `Platform Service Contracts.md`, a test (or documented manual
  benchmark, if automating is disproportionate) confirming it is met.
  Not every Work Package has one — see each row.
- **Documentation Validation** — any code example published in Academy
  material or a public interface's own XML doc actually compiles and
  behaves as documented, mirroring the existing standard's own
  "documentation examples actually compile and run" requirement
  (`docs/releases/v0.4.0/Testing.md`).

## Per-Work-Package Test Plan

### `WP 6.0` — Reporting Framework

- **Unit Tests.** Definition/renderer registration (success, duplicate-Id
  rejection); `GenerateAsync` dispatch to the correct renderer;
  `RegisteredDefinitions` enumeration correctness.
- **Integration Tests.** A real report definition/renderer registered by
  a sample module, generated end-to-end through the real
  `IReportingService`; optional Command Framework invocation path
  (`GenerateReportCommand` dispatching through `ICommandDispatcher`).
- **Failure Injection Tests.** `GenerateAsync` for an unregistered Id
  throws `ReportDefinitionNotFoundException`; a renderer that throws
  propagates the original exception unmodified to the caller.
- **Regression Tests.** None anticipated at architecture time — this is
  a new service with no prior history to regress against.
- **Performance Tests.** Not applicable — rendering time is
  renderer-specific and explicitly outside this service's own control
  (`Platform Service Contracts.md`).
- **Documentation Validation.** Every `Public Interface Catalogue.md`
  example for `Tempest.Core.Reporting` compiles; the Academy material
  produced for this Work Package (see `Academy Plan.md`) includes a
  worked, buildable example.

### `WP 6.1` — Permissions & Identity

- **Unit Tests.** `HasPermission`/`RequirePermission` correctness for
  every grant/deny combination; `ICurrentPrincipalAccessor.Current`
  resolution correctness in a single-threaded context.
- **Integration Tests.** A real module performing an authorization check
  against a real, registered principal and permission set; the specific
  `TD-09` (plugin isolation), `TD-10` (Navigation ownership), and
  `TD-11` (registration-order squatting) scenarios each re-tested as a
  named regression case (see Regression Tests, below) using the new
  enforcement point.
- **Failure Injection Tests.** `RequirePermission` throws
  `PermissionDeniedException` for a principal lacking the required
  permission; an unauthenticated/absent principal is handled per
  whatever `WP 6.1`'s own architecture phase decides (denied, not a
  null-reference fault).
- **Regression Tests.** `TD-09`, `TD-10`, `TD-11` — each named explicitly
  as a required regression test once `WP 6.1`'s own implementation
  claims to resolve it, proving the specific historical scenario each
  item describes no longer succeeds where it once would have
  (`Technical Debt Assessment.md`).
- **Performance Tests.** `HasPermission`/`RequirePermission` measured
  against the O(1)/O(log n) expectation in `Platform Service
  Contracts.md`, given this is an anticipated hot path (every REST
  request, once `WP 6.3` exists).
- **Documentation Validation.** A worked "how to check a permission from
  your own module" example, compiling and passing.

### `WP 6.2` — Notification Framework

- **Unit Tests.** Subscribe/unsubscribe/publish basic behaviour,
  directly mirroring `EventBus`'s own existing unit test suite in
  structure.
- **Integration Tests.** A real notification derived from a real event,
  published and received end-to-end through a real subscriber.
- **Failure Injection Tests.** One subscriber throwing does not prevent
  another subscriber from receiving the same notification, and does not
  propagate to the publisher — reusing `IEventBus`'s own existing test
  pattern for this exact scenario (`ADR-0028`).
- **Regression Tests.** None anticipated — new service.
- **Performance Tests.** Not applicable beyond what Event Bus's own
  existing performance characteristics already establish.
- **Documentation Validation.** A worked "building a notification-driven
  module" example, extending (not replacing) the existing "Building an
  Event-Driven Module" Academy guide.

### `WP 6.3` — REST API

- **Unit Tests.** Route registration (`IApiEndpointRegistry.MapCommand`,
  including duplicate-route rejection); route-to-command mapping
  correctness at the registry level, independent of any real HTTP call.
- **Integration Tests.** Real HTTP requests driven against a real,
  running `TempestHost` (mirroring `TempestHostTests`'s own "against the
  real, unmodified Host" philosophy) — authorized request succeeds and
  dispatches through the real Command Framework; unauthorized request
  is rejected; a request for an unregistered route returns 404.
- **Failure Injection Tests.** A command handler that throws maps to a
  500 response with no leaked internal exception detail
  (`Platform Service Contracts.md`'s own explicit security requirement);
  an unauthorized request maps to 403; a malformed request body is
  rejected cleanly.
- **Regression Tests.** `AT-07` ("zero real hosted services exist") —
  a regression test confirming the REST API is discovered, started at
  Phase 8.1, and stopped at Phase 10.1 exactly like `WP 4.5`'s own
  existing hosted-service tests already prove for the infrastructure
  itself, extended to a genuine, non-test consumer for the first time.
- **Performance Tests.** A concurrency test proving the thread-safety
  expectations in `Platform Service Contracts.md` hold under multiple
  simultaneous requests; a basic latency/throughput benchmark once
  `WP 6.3`'s own architecture phase sets a concrete target
  (`Risk Register.md` `R3`).
- **Documentation Validation.** A worked "adding a REST endpoint for
  your module's command" example, compiling and passing against a real,
  running test Host.

### `WP 6.4` — Settings Framework (establishes Persistence)

- **Unit Tests.** `IPersistenceStore` read/write/delete/list round-trip
  correctness; `ISettingsProvider` get/set/default-value correctness;
  not-found handling for both.
- **Integration Tests.** A setting value written, restarted (simulated
  process restart via a fresh `IPersistenceStore` instance over the same
  underlying storage), and correctly re-read — proving the durability
  requirement in `Platform Service Contracts.md`; `ISettingsChangedEvent`
  published and received by a real subscriber through the real Event
  Bus.
- **Failure Injection Tests.** A simulated storage I/O failure surfaces
  `PersistenceStoreUnavailableException` from both `IPersistenceStore`
  directly and from `ISettingsProvider` (unmasked, per its own stated
  failure behaviour); concurrent writes to the same collection/key do
  not corrupt either value.
- **Regression Tests.** None anticipated — both are new services.
- **Performance Tests.** A single read/write measured against
  `Platform Service Contracts.md`'s own low-single-digit-millisecond
  expectation under ordinary local-disk conditions.
- **Documentation Validation.** A worked "registering a runtime-mutable
  setting" example, compiling and passing.

### `WP 6.5` — Audit Framework

- **Unit Tests.** `RecordAsync`/`QueryAsync` round-trip correctness;
  `AuditQueryCriteria` filter correctness (by actor, action, date range,
  and combinations).
- **Integration Tests.** A real action recorded with a real,
  authenticated principal (via Identity & Permissions) and a real
  underlying `IPersistenceStore`, then queried back successfully.
- **Failure Injection Tests.** A storage failure during `RecordAsync`
  propagates per whatever `WP 6.5`'s own architecture phase decides
  regarding isolation from the primary audited action (`Platform Service
  Contracts.md`'s own named open tension); an unauthorized caller cannot
  successfully call `IAuditQuery`.
- **Regression Tests.** None anticipated — new service, though it should
  include a test proving Audit is genuinely distinct in behaviour from
  Logging and Diagnostics (e.g., an audit record survives a process
  restart where a log entry to a non-persistent sink would not),
  directly validating `ADR-0045`'s own orthogonality claim.
- **Performance Tests.** `RecordAsync` measured to confirm it does not
  meaningfully slow the action being recorded, per its own stated
  expectation.
- **Documentation Validation.** A worked "recording an audited action"
  example, compiling and passing.

### `WP 6.6` — Licensing Framework

- **Unit Tests.** `ILicenseValidator.Validate()` against
  valid/expired/malformed/missing-file fixtures; `ILicenseProvider.
  HasCapability` correctness against a known, valid license fixture.
- **Integration Tests.** A real `TempestHost` startup sequence, confirming
  `ILicenseProvider` is correctly registered and readable via
  `ITempestHost.Services` once startup completes with a valid license.
- **Failure Injection Tests.** An invalid license aborts Host startup
  entirely, Host-fatal, exactly mirroring the existing `ADR-0013`
  platform-service-failure test pattern applied to this new case — this
  is this Work Package's single most important test, since it proves
  the entire premise of `ADR-0050`.
- **Regression Tests.** None anticipated — new service, deliberately
  modeled on Platform Version's already-tested "deliberately a leaf"
  pattern rather than introducing a novel one.
- **Performance Tests.** Not applicable beyond confirming
  `HasCapability` is O(1), per its own stated expectation.
- **Documentation Validation.** A worked "checking a licensed capability
  from your own module" example, compiling and passing.

### `WP 6.7` — Export / Import

- **Unit Tests.** Export/import round-trip correctness for a
  representative `IExportable` fixture; `SchemaVersion` compatibility
  check correctness.
- **Integration Tests.** A real Settings export (once `WP 6.4` exists)
  written to a real `Stream`, then imported back and confirmed to
  restore the original values exactly.
- **Failure Injection Tests.** An artifact with an incompatible
  `SchemaVersion` throws `IncompatibleExportSchemaException` and is
  never partially imported; a caller-supplied `Stream` failure
  propagates as the underlying `IOException` unmodified.
- **Regression Tests.** None anticipated — new service.
- **Performance Tests.** Not applicable beyond confirming no unnecessary
  buffering/copying beyond what streaming genuinely requires, per its
  own stated expectation.
- **Documentation Validation.** A worked "making your module's data
  exportable" example, compiling and passing.

### `WP 6.8` — Platform Services Integration Review

- **Unit Tests.** Not applicable — this Work Package is a review, not a
  new service; it produces no new production code and therefore no new
  unit tests of its own.
- **Integration Tests.** A full-suite regression run confirming every
  one of the other eight Work Packages' own tests still pass together,
  in combination, on the real `TempestHost` — the release's own final
  integration checkpoint, mirroring `WP 4.2D`/`WP 5.0S`/`WP 5.4`'s own
  precedent.
- **Failure Injection Tests.** Not newly introduced by this Work Package
  — it re-runs and verifies the failure-injection suites already
  established by `WP 6.0`–`WP 6.7`, rather than inventing new ones.
- **Regression Tests.** Every `TD-`/`AT-` item this release claims to
  resolve (`TD-09`, `TD-10`, `TD-11`, `AT-07`) re-verified directly
  against the file system and the actual shipped code — not trusted from
  any prior Work Package's own retrospective claim, per `WP 5.4`'s own
  standing-practice finding.
- **Performance Tests.** Not applicable — no new service; this Work
  Package may aggregate and report the individual performance test
  results already gathered by `WP 6.0`–`WP 6.7`.
- **Documentation Validation.** Re-derives every governance count
  (Work Package count, ADR count, test count, Academy document count)
  directly from the file system, exactly as this Work Package's own
  `WorkPackages.md` entry already requires.

## New Testing Concerns This Release Introduces

- **Genuine, first-class request-level concurrency** (REST API) is new
  in kind — every prior release's concurrency concerns (module
  lifecycle, hosted service start/stop) were internal, sequential-by-
  design orchestration; the REST API is this platform's first surface
  where truly concurrent, external, unordered requests are an expected,
  routine condition rather than an edge case.
- **Security-relevant failure paths** (authorization denial, license
  invalidity) are new in kind — prior releases' failure tests concerned
  correctness (a not-found exception, a duplicate registration); this
  release's failure tests must also prove a *negative* security
  property (denied access stays denied, no internal detail leaks in an
  error response).
- **Cross-service integration spanning three or more new services**
  (Audit depends on both Persistence and Identity & Permissions; the
  REST API depends on Background Services, the Command Framework, and
  Identity & Permissions) is deeper than any single prior release's own
  cross-cutting test — `v0.4.0`'s Event Bus/Command Framework each
  touched one existing service at most.

## What Does Not Change

- No test parallelism assumptions change — tests within a class remain
  sequential by xUnit's default per-class collection behaviour.
- No new test framework, mocking library, or assertion library is
  introduced — every new test is written using this platform's existing
  xUnit-based conventions.
- The internal-test-seam pattern (used by `TempestHostBuilder` and
  similar ambient/broad contracts) is reused, not replaced, for any new
  service needing an equivalent seam (most plausibly the REST API's own
  hosted-service scaffold, to test route dispatch without a live network
  listener).

## Related Documents

`docs/academy/06 Engineering Standards/02-testing-strategy.md`;
`docs/releases/v0.4.0/Testing.md` (the format this document follows);
`Platform Service Contracts.md`; `Platform Service Implementation
Order.md`; `Technical Debt Assessment.md`; `Academy Plan.md`.
