# WP 6.3 — REST API — Platform Impact Assessment

## Purpose

A dedicated assessment of whether `WP 6.3`'s own implementation
confirms, extends, or exposes a weakness in the platform architecture
established by prior Work Packages — distinct from its Implementation
Report, Engineering Review Report, and Platform Integration
Demonstration.

## Does This Work Package Confirm Earlier Platform Architecture?

**Yes, on four separate points, each independently verified rather
than assumed:**

1. **The Composition Root / ordinary-singleton registration pattern
   (`ADR-0009`) continues to scale cleanly to a ninth new service**
   (`IApiEndpointRegistry`), registered in the same Phase 6 block as
   every other DI-public Platform Service since `WP 4.4`.
2. **`ADR-0029`/`ADR-0030` (hosted-service discovery/orchestration and
   lifecycle placement) require zero adaptation for a real, substantial
   consumer.** `RestApiHostedService` is discovered by the same
   reflection-based `IHostedServiceDiscoveryService`, started at Phase
   8.1 and stopped at Phase 10.1, with no new Host Lifecycle phase —
   confirming this design genuinely generalises beyond the sample
   fixtures that originally exercised it.
3. **`ADR-0038`'s own Command dispatch failure model (propagate, don't
   isolate) generalises to a fourth consumer.** `ApiRequestHandler`
   maps a dispatched command's own exception to an HTTP 500 without
   ever wrapping or swallowing it internally, exactly mirroring the
   philosophy `ADR-0038` established for command handlers themselves.
4. **`IPermissionEvaluator`'s own explicit-parameter shape (`ADR-0044`)
   is exactly what a genuinely concurrent consumer needs.** Because
   `HasPermission`/`RequirePermission` take the principal as an
   ordinary parameter rather than reading ambient state, the REST API
   could adopt this platform's own single existing authorization
   mechanism without any modification — strong evidence that
   `ADR-0044`'s own design was already correctly shaped for a
   concurrent consumer, years before one existed.

## Does This Work Package Extend Earlier Platform Architecture?

**Yes, in two specific, disclosed ways:**

1. **This platform's first substantial dependency on a pre-built
   framework component beyond the bare .NET SDK** (`ADR-0049`) —
   `ADR-0005`'s own custom-DI-container reasoning is confirmed to have
   never been about HTTP hosting, and the adopted boundary (a single
   `FrameworkReference`, confined to one type) is verified, not merely
   designed, to leave every other platform service untouched.
2. **This platform's first genuinely concurrent, per-request scenario**
   — every prior platform service operated under an implicit
   single-ambient-principal, effectively single-caller-at-a-time model.
   The REST API resolves this without modifying the existing model at
   all, by avoiding shared mutable state entirely rather than making it
   safe under concurrency — a genuinely new pattern this codebase can
   reuse for any future concurrent consumer.

## Does This Work Package Expose Any Architectural Weakness?

**One, directly confirmed rather than merely anticipated:**
`Risk Register.md`'s own `R1` predicted `CurrentPrincipalAccessor`'s
ambient design would need "real reconsideration" once genuine request
concurrency arrived — this Work Package confirms that prediction was
half right and half wrong: reconsideration was genuinely required (a
real design question had to be answered), but the answer was "resolve
it by not touching the ambient state at all," not "change the ambient
state's own implementation." A future platform service with a genuine
need to *establish* an ambient principal per-request (rather than
merely read one) would still face the identical tension this Work
Package found — that residual risk is now understood precisely,
not vaguely, and is named explicitly in `TD-15`.

**A second, disclosed observation, not a weakness in the platform
architecture itself:** `docs/governance/Engineering/Hosted Services
Register.md` had gone stale since `WP 4.5A`, never updated when `WP
6.2` shipped this codebase's first real hosted service. This is a
disclosed weakness in this project's own governance-maintenance
discipline, not the platform's own technical architecture — corrected
in the same commit as the finding.

## Explicit Assessment: Interactions With Identity, Settings, Audit, Notifications, and Reporting

**Recorded per this Work Package's own explicit instruction — see
`WP6.3 Platform Integration Demonstration.md` for the complete,
per-service account.** In summary:

- **Identity & Permissions.** Used — a genuine, approved, core-level
  dependency of `ApiRequestHandler` itself, required by the approved
  contract's own Responsibilities dimension.
- **Audit.** Used, twice, independently — a genuine, approved,
  core-level dependency of `ApiRequestHandler` itself (its own
  `api.request` entry), plus whatever the invoked command's own handler
  separately records.
- **Settings, Notifications, Reporting.** All three used, but entirely
  inside `ReportingSampleModule`'s own already-existing command handler
  (`WP 6.0`) — `Tempest.Core.Api` itself has zero direct dependency on
  any of the three, confirmed by direct inspection.

**Summary: the REST API has two genuine, justified, core-level platform
dependencies (Identity, Audit) and three arm's-length dependencies
(Settings, Notifications, Reporting) that exist only because the one
command it happens to expose uses them — never because the REST API
itself needs them.** This is architecturally the cleanest possible
outcome for a "thin transport layer": the two dependencies it does have
are exactly the two the approved contract names as required
responsibilities, and nothing more.

## Related Documents

`WP6.3 Implementation Report.md`; `WP6.3 Engineering Review Report.md`;
`WP6.3 Platform Integration Demonstration.md`; `WP6.3 Lessons
Learned.md`; `WP6.3 Technical Debt Assessment.md`; `WP6.3 Future
Capability Recommendations.md`; `ADR-0005`; `ADR-0038`; `ADR-0044`;
`ADR-0047`; `ADR-0049`; `ADR-0052`; `docs/releases/v0.6.0/Risk
Register.md` (`R1`, `R2`, `R3`); `docs/governance/Quality/Technical
Debt Register.md` (`TD-15`); `docs/governance/Engineering/Hosted
Services Register.md`.
