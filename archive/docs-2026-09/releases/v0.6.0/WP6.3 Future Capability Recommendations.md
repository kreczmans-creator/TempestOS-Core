# WP 6.3 — REST API — Future Capability Recommendations

## Purpose

Recommendations for future Work Packages, arising directly from what
`WP 6.3`'s own implementation found, mirroring every prior Work
Package's own Future Capability Recommendations format.

## Recommendation 1 — Design a Genuine Authentication Mechanism Before Exposing This Platform Beyond a Trusted Local Boundary

**What.** Before any future deployment scenario exposes the REST API
beyond `127.0.0.1`, a real authentication mechanism (API keys,
OAuth/OIDC, mutual TLS, or another concrete scheme) must replace the
current bare, unverified `X-Identity-Id` header (`TD-13`).

**Why this matters.** `Platform Service Contracts.md`'s own Security
Considerations name the REST API as this release's highest-security-
sensitivity service; the current identity model is a disclosed,
deliberate first pass, not a production-ready credential system.

## Recommendation 2 — Configure TLS Once a Concrete Deployment Scenario Exists

**What.** Add Kestrel TLS configuration (certificate source read from
`IConfigurationProvider`, matching every other platform service's own
configuration convention) once a real deployment target beyond local
development is named (`TD-14`).

**Why not build it now.** No concrete certificate source or deployment
target exists yet in this release's own approved scope; building it
speculatively would be exactly the kind of premature capability this
project's own conventions warn against.

## Recommendation 3 — Any Future Command Needing Precise Per-Request Audit Attribution Under REST Invocation Should Accept an Explicit Actor Parameter, Not Rely on Ambient State

**What.** A future command handler that must record exactly who
invoked it via REST should either read its own caller identity from a
convention this Work Package's own `Detail[CallerIdentityId]` entry
establishes, or be redesigned to accept an explicit actor parameter,
rather than depending on `IAuditRecorder`'s own ambient-principal
auto-attribution (`TD-15`).

**Why this matters.** `ApiRequestHandler` deliberately never establishes
the ambient current principal, for good, empirically-verified reasons
(`ADR-0052`) — any future command author needs to know this before
assuming ambient attribution "just works" when their own command is
exposed over REST.

## Recommendation 4 — Add Request-Parameter Binding Only When a Concrete REST-Exposed Command Actually Needs Caller-Supplied Values

**What.** If a future report definition, or any other REST-exposed
command, genuinely needs a caller-supplied parameter (a query string
value, a JSON request body), extend `IApiEndpointRegistry.MapCommand`
or introduce a parallel mechanism at that point — not speculatively now
(`AT-10`).

**Why not build it now.** No current REST-exposed command has this
need; `Public Interface Catalogue.md`'s own approved `MapCommand`
signature carries no binding mechanism, and inventing one without a
concrete consumer would be unjustified speculative capability.

## Recommendation 5 — Any Future Engineering Module Exposing a Route Should Follow `ApiSampleModule`'s Own Zero-Business-Logic Pattern

**What.** A future module wanting to expose a route should register its
own command with the Command Framework first (with whatever business
logic that command's own handler needs), then map a route to it with a
single `MapCommand` call — never writing request-handling logic inside
the module's own `InitialiseAsync` or a bespoke endpoint delegate.

**Why this is worth naming.** `ApiSampleModule` proves this pattern
works cleanly even when the exposed command was written by a completely
different Work Package for a completely different purpose
(`ReportingSampleModule`'s own command) — naming the pattern explicitly
here reduces the chance of a future module reinventing a heavier
approach.

## Recommendation 6 — `WP 6.8` (Platform Services Integration Review) Should Re-Verify the ASP.NET Core Confinement Boundary

**What.** When `WP 6.8` performs its own closing, whole-release
verification pass, it should re-confirm directly (not merely trust this
Work Package's own claim) that no `Tempest.Core` service is ever
resolved through ASP.NET Core's own internal `IServiceProvider`,
especially if any future Work Package touches `RestApiHostedService`
itself.

**Why this is worth naming.** A confinement boundary enforced by
convention, not by the compiler, can erode silently over time if a
future contributor reaches for `HttpContext.RequestServices` out of
convenience — a periodic re-verification is cheap insurance against
that.

## Not Recommended

- **Building a full API-key or OAuth provider now.** No concrete
  deployment scenario names a specific mechanism yet; building one
  speculatively would itself become technical debt if the real,
  eventual requirement differs.
- **Migrating `CurrentPrincipalAccessor` to `AsyncLocal<T>`.**
  Empirically tested and rejected this Work Package — see `ADR-0052`.
  Revisit only if a future, different concurrent scenario emerges that
  genuinely requires establishing (not merely reading) a per-flow
  ambient principal, and even then, re-test against the full suite
  before committing.
- **Adding request/response compression or rate limiting speculatively.**
  Both are named in `Platform Service Contracts.md`'s own Future
  Extension Points as plausible, not current, requirements.

## Related Documents

`WP6.3 Implementation Report.md`; `WP6.3 Engineering Review Report.md`;
`WP6.3 Platform Integration Demonstration.md`; `WP6.3 Platform Impact
Assessment.md`; `WP6.3 Lessons Learned.md`; `WP6.3 Technical Debt
Assessment.md`; `ADR-0049`; `ADR-0052`; `docs/releases/v0.6.0/Platform
Service Contracts.md` (the REST API's own Future Extension Points);
`docs/releases/v0.6.0/WorkPackages.md` (`WP 6.8`);
`docs/governance/Quality/Technical Debt Register.md` (`TD-13`, `TD-14`,
`TD-15`, `AT-10`).
