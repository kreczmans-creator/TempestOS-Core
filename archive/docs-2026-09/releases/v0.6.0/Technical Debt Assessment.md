# TempestOS v0.6.0 — Technical Debt Assessment

## Purpose

Assesses this release's proposed architecture against
`docs/governance/Quality/Technical Debt Register.md`'s existing 11
tracked debt items and 7 disclosed trade-offs, in both directions: which
existing items this release is positioned to resolve, and what new debt
or disclosed trade-offs its own proposed design is expected to introduce.
This is an assessment accompanying an architecture-only review — no
debt item is actually resolved or introduced until the owning Work
Package implements it; every claim below is "positioned to," not
"has."

## Existing Debt This Release Is Positioned to Resolve

### TD-09, TD-10, TD-11 — via `WP 6.1` (Permissions & Identity)

All three trace to the same underlying gap: this platform has never had
an authorization concept, so nothing prevents a plugin from acting with
identical trust to a first-party module (`TD-09`), nothing checks
ownership before `NavigationService.Unregister` acts (`TD-10`), and
nothing establishes that the first registrant of a well-known Id was its
*intended* owner rather than merely the fastest (`TD-11`). Each was
explicitly left open with the same revisit trigger: "the first Work
Package with a genuine reason to build an authorization concept."
`WP 6.1` is that Work Package.

**How this release positions each for resolution**, per `ADR-0044`
(Authorization Enforcement Point, see `Required ADRs.md`):
`IPermissionEvaluator.RequirePermission` becomes the single, uniform
check every other service calls. Concretely: `NavigationService.
Unregister` (`TD-10`) can require the caller hold a permission scoped to
the specific navigation item's own owning module, closing the ownership
gap directly; command/navigation registration (`TD-11`) can require a
reserved-Id permission grant rather than relying on registration-order
alone; and a plugin-loaded module's own effective permission set
(`TD-09`) can be scoped narrower than a first-party module's by
default, rather than granted implicitly and identically. **This
release's architecture does not itself implement any of these three
fixes** — it identifies `WP 6.1` as the load-bearing Work Package
capable of closing all three together, for the first time since they
were disclosed, and names the specific mechanism (`ADR-0044`) each would
use. Actually closing them remains `WP 6.1`'s own implementation-phase
responsibility, verified by `WP 6.8`.

**Risk of non-resolution.** See `Risk Register.md` `R1`/`R2` — if
`WP 6.1`'s own architecture phase does not produce a sufficiently
complete `ADR-0044`, or if `WP 6.3` (REST API) begins before `WP 6.1`
lands, these three items could remain open through another entire
release, for the third consecutive time.

### AT-07 — via `WP 6.3` (REST API)

`AT-07` ("Zero real hosted services exist beyond the infrastructure...
Revisit trigger: The first Work Package that ships a real hosted
service") is retired the moment the REST API's own hosted-service
scaffold (`ADR-0047`) is implemented — the first genuine, non-test
consumer of `IHostedServiceManager`/`IHostedServiceDiscoveryService`
since `WP 4.5` built them. **Not retired by this architecture
document itself** — retirement is contingent on `WP 6.3`'s own
implementation actually landing, at which point `WP 6.8` should update
`Technical Debt Register.md` directly, per its own re-derivation
mandate.

### AT-06 — partially relevant, not resolved by this release

`AT-06` ("`src/Plugins/` remains empty — no real plugin built yet...
Revisit trigger: The first Work Package that ships a real plugin") is
**not** addressed by any `v0.6.0` Work Package — none of the nine
proposes shipping a real, loadable plugin (as distinct from a first-party
module). Listed here only to confirm this release does not silently
close it; it remains open, unaffected.

## New Debt/Trade-offs This Release Is Expected to Disclose

### Licensing initially local/offline-only

`WP 6.6`'s architecture (`ADR-0050`) validates a license from a local
file source at Host startup, with no network call, no remote license
server, and no online activation/deactivation flow. This is a
deliberate, disclosed initial scope — mirroring how `WP 4.2`'s Plugin
Manifest shipped with fixed, non-configurable conventions (`TD-06`) as
an accepted starting point, not an oversight. **Anticipated debt item**
(to be registered by `WP 6.6` upon implementation): *Licensing has no
remote validation, revocation, or floating/seat-based licensing model —
local file only.* Revisit trigger: a genuine multi-seat or
subscription-licensing requirement.

### REST API's initial authorization model possibly coarse-grained

`WP 6.3` depends on `WP 6.1`'s permission model, but `WP 6.1` itself may
only deliver a first-iteration, relatively coarse-grained permission
scheme (see `Risk Register.md` `R1`) — meaning the REST API's own
initial endpoint-level authorization could be coarser than an
eventual, fully mature permission model would allow (e.g., a single
"API access" permission rather than one permission per endpoint,
initially). **Anticipated debt item** (to be registered by `WP 6.3`
upon implementation, if this coarseness is actually what ships):
*REST API authorization is endpoint-group-grained, not
endpoint-specific, in its first iteration.* Revisit trigger: a genuine
need for finer-grained API access control.

### Persistence abstraction initially minimal, key-value only

`ADR-0041`'s anticipated `IPersistenceStore` shape (see `Public
Interface Catalogue.md`) is a simple string key/value store scoped to
named collections — no querying beyond key lookup and full-collection
key enumeration. `WP 6.5` (Audit)'s own `IAuditQuery` needs filtered,
potentially range-based queries (by actor, by action, by date range) —
see `Risk Register.md` `R8`. If `WP 6.4` ships the minimal shape as
designed, Audit will need to either fetch broadly and filter in memory,
or `WP 6.4`'s implementation phase will need to extend the abstraction
before `WP 6.5` can build on it cleanly. **Anticipated debt item** (to
be registered by whichever Work Package's implementation actually
encounters this): *Persistence has no native query/filter capability
beyond key lookup — any filtered read is client-side.* Revisit trigger:
a second consumer with a query need `WP 6.5` cannot satisfy by
client-side filtering at a reasonable cost.

## Assessment Summary

| Existing item | Effect of this release |
|---|---|
| `TD-09` | Positioned for resolution via `WP 6.1`/`ADR-0044` — not resolved by this document |
| `TD-10` | Positioned for resolution via `WP 6.1`/`ADR-0044` — not resolved by this document |
| `TD-11` | Positioned for resolution via `WP 6.1`/`ADR-0044` — not resolved by this document |
| `AT-07` | Positioned for retirement via `WP 6.3` — not retired by this document |
| `AT-06` | Unaffected — remains open |
| All other existing items (`TD-01`–`TD-08`) | Unaffected by this release's proposed scope |

| Anticipated new item | Originating WP | Disclosed in advance |
|---|---|---|
| Licensing local/offline-only in its first iteration | `WP 6.6` | Yes — `ADR-0050` |
| REST API authorization possibly coarse-grained initially | `WP 6.3` | Yes — contingent on `WP 6.1`'s own first-iteration scope |
| Persistence has no native query/filter capability beyond key lookup | `WP 6.4` | Yes — cross-referenced against `WP 6.5`'s own need |

`WP 6.8` (Platform Services Integration Review) is expected to
re-derive this entire assessment directly from what each Work Package
actually shipped — not to trust this document's own predictions
unverified — mirroring `WP 5.4`'s own standing-practice finding that
governance counts must be re-derived from the file system at every
release boundary, not carried forward by increment.

## Related Documents

`docs/governance/Quality/Technical Debt Register.md`; `Release
Architecture.md`; `Platform Services Overview.md`; `Required ADRs.md`;
`Risk Register.md`; `docs/releases/v0.6.0/WorkPackages.md`;
`docs/security/Security Roadmap.md`.
