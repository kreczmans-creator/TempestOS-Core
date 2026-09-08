# WP 6.3 — REST API — Implementation Report

## Status

**Complete.** Implemented on `feature/v0.6.0-platform-services`, directly
against the already-approved `v0.6.0` architecture package and Contract
Review package — neither package was revised during implementation.
The second of `v0.6.0`'s six implemented Work Packages to match its own
nominal numeric position in `WorkPackages.md` (after `WP 6.0`). Per this
Work Package's own closing instruction, implementation stops here,
pending engineering approval.

## Scope Delivered

| Deliverable | Status |
|---|---|
| API Host | Delivered — `RestApiHostedService`, ASP.NET Core/Kestrel via `WebApplication.CreateSlimBuilder()` (`ADR-0049`) |
| API registration | Delivered — `IApiEndpointRegistry`/`ApiEndpointRegistry`, exactly as approved |
| Versioning infrastructure | Delivered — every route conventionally prefixed `/api/v1/...`, matching `Platform Service Contracts.md`'s own Versioning Policy |
| Endpoint discovery | Delivered — `RestApiHostedService` maps every route present in `IApiEndpointRegistry.Routes` at startup |
| Authentication abstraction | Delivered, disclosed as minimal — `ApiRequestHandler.IdentityHeaderName` (`X-Identity-Id`), a bare, unverified header value extending this release's own local-only identity model over HTTP (`ADR-0052`); no credential verification of any kind (`TD-13`) |
| Authorisation integration | Delivered — `IPermissionEvaluator.HasPermission`, called explicitly per request against a per-request-resolved principal |
| OpenAPI generation | Delivered — `OpenApiDocumentGenerator`, a minimal, hand-built OpenAPI 3.0 JSON document, no third-party Swagger dependency |
| Dependency Injection registration | Delivered — `IApiEndpointRegistry` as an ordinary Phase 6 singleton |
| Hosted Service integration | Delivered — `RestApiHostedService` discovered and orchestrated identically to any other hosted service (`ADR-0047`) |
| Logging | Delivered — every request logged at Information level (method, path, status, principal Id if authorized), matching the approved contract's own Logging Requirements |
| Diagnostics | **Not delivered as an `IDiagnosticsProvider` interface change** — see "Diagnostics," below, mirroring every prior Work Package's own identical scope decision |

## Suitability for Future Consumers

`IApiEndpointRegistry` is implemented with zero deviation from `Public
Interface Catalogue.md`, so any future engineering module can map its
own route with full confidence in the shape. `ApiRequestHandler` is
Kestrel-independent and directly unit-testable, so its own
route-lookup/authorization/dispatch logic can be exercised without a
real HTTP listener.

## Diagnostics: What Was and Was Not Done

Mirroring every prior Work Package's own identical finding: extending
the approved, shipped `IDiagnosticsProvider` (`WP 5.2`, `ADR-0039`)
would be a change to an approved public interface, requiring
documentation, an ADR, and genuine necessity per this Work Package's own
instructions. No such necessity exists — the REST API's own
observability need is fully satisfiable through ordinary logging
(delivered) and the sample module's own demonstrable behaviour
(delivered).

## The ASP.NET Core/Kestrel Integration Boundary

`Risk Register.md`'s own `R3` required this boundary to be prototyped
explicitly before committing to it. Adopted via a `FrameworkReference`
to the already-installed `Microsoft.AspNetCore.App` shared framework —
no new external package. Confined entirely to `RestApiHostedService`:
every mapped route delegate closes over the exact `ApiRequestHandler`
instance received via ordinary constructor injection from TempestOS's
own container; no `Tempest.Core` service is ever resolved through
ASP.NET Core's own internal `IServiceProvider`. Verified directly, not
merely designed — see this Work Package's own Testing section.

## Identity Resolution: A Genuine, Empirically-Verified Decision

`Risk Register.md`'s own `R1` named this Work Package as the point
where `CurrentPrincipalAccessor`'s ambient design "will need real
reconsideration." An `AsyncLocal<T>`-backed implementation was built
and tested directly against the full, pre-existing 862-test suite, and
regressed 17 tests. `CurrentPrincipalAccessor` therefore remains
entirely unchanged. `ApiRequestHandler` instead resolves a per-request
`IPrincipal` via the pure, non-mutating `IIdentityService.GetPrincipal`,
passed explicitly to `IPermissionEvaluator.HasPermission` — safe for
concurrent requests by construction. See `ADR-0052` for the complete
account.

## Production Code

9 files under `src/Tempest.Core/Api/`; 1 file under
`src/Samples/Tempest.Samples/`; 2 files modified
(`src/Tempest.Core/Tempest.Core.csproj`, adding a `FrameworkReference`;
`src/Tempest.Core/Runtime/TempestHost.cs`, registration only). See the
retrospective's own "Files Added" section for the complete list,
including the experimental `CurrentPrincipalAccessor` change that was
built, tested, found to regress 17 tests, and reverted — no net change
shipped to that file.

## Testing

45 new tests (914 total, up from the `WP 6.0` baseline of 862), across
every category the implementation brief named:

| Category | Delivered |
|---|---|
| Unit tests | `ApiEndpointRegistryTests`, `ApiRequestHandlerTests`, `OpenApiDocumentGeneratorTests`, `ExceptionTests` |
| Integration tests | `ApiSampleModuleIntegrationTests` — genuine, real-HTTP round trips (via `HttpClient`) against a real, running `TempestHost`, not an in-process simulation |
| Endpoint tests | `PostToMappedRoute_*`, `GetUnmappedPath_Returns404`, `GetOpenApiDocument_*` |
| Authentication tests | `PostToMappedRoute_NoIdentityHeader_Returns401`, `PostToMappedRoute_PermissionNotGranted_Returns403` |
| Failure injection tests | `HandleAsync_CommandThrows_Returns500_WithNoLeakedExceptionDetail`; `RestApiHostedServiceTests.StartAsync_PortAlreadyInUse_ThrowsRatherThanSilentlyListeningElsewhere` |
| Hosted Service tests | `RestApiHostedServiceTests` (bind/listen, stop, port-conflict isolation, non-critical marker); `RunAsync_ConfiguredPortAlreadyInUse_HostStillReachesRunning_FailureIsolated` |
| Regression tests | `ClockModuleDiscoveryTests` updated for the thirteenth sample module |
| Concurrency tests | `ConcurrentRequests_FromDifferentPrincipals_AreEachHandledIndependently` — ten concurrent, differently-permissioned requests, each authorized correctly and independently |

## Validation Performed

- **Clean build.** `dotnet build tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj`
  from a fully removed `bin`/`obj` tree, both Debug and Release
  configurations: 0 warnings, 0 errors, both times.
- **Complete automated test suite.** `dotnet test` in both Debug and
  Release configurations: 914/914 passing, both times; each
  configuration re-run three consecutive times to confirm stability,
  with no port-collision flake observed once (every test configures
  port `0`, an OS-assigned ephemeral port).
- **Static analysis.** 0 compiler warnings (`Nullable` enabled
  project-wide) in both configurations.
- **Documentation validation.** Every code example in `Public Interface
  Catalogue.md` referenced by this Work Package's own implementation was
  cross-checked against the real, compiled signatures — no drift found.
- **Dependency validation.** Confirmed directly: `Tempest.Core.Api`
  depends only on `Tempest.Core.Audit`, `Tempest.Core.BackgroundServices`,
  `Tempest.Core.Commands`, `Tempest.Core.Configuration`,
  `Tempest.Core.Identity`, `Tempest.Core.Logging` (all existing Platform
  Services/DI), plus ASP.NET Core (`ADR-0049`) and `System.Text.Json`
  (BCL) — no dependency on any Module, no circular reference. No
  dependency on Settings, Notifications, or Reporting — those three are
  consumed only at the sample-module calling layer.
- **Engineering self-review.** See `WP6.3 Engineering Review Report.md`.

## A Genuine, Empirically-Verified Architectural Finding

This Work Package's own implementation phase built and tested a
genuine alternative (`AsyncLocal<T>`-backed `CurrentPrincipalAccessor`)
directly against the full pre-existing suite, found it regressed 17
tests, and reverted it — see this report's own Identity Resolution
section and `ADR-0052` for the complete account.

## Related Documents

`docs/academy/03 Work Packages/WP6.3-rest-api-
implementation.md` (the full retrospective); `ADR-0047`; `ADR-0048`;
`ADR-0049`; `ADR-0052`; `WP6.3 Engineering Review Report.md`; `WP6.3
Platform Integration Demonstration.md`; `WP6.3 Platform Impact
Assessment.md`; `WP6.3 Lessons Learned.md`; `WP6.3 Technical Debt
Assessment.md`; `WP6.3 Future Capability Recommendations.md`.
