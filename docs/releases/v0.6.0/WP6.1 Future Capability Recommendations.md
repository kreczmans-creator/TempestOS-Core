# WP 6.1 — Permissions & Identity — Future Capability Recommendations

## Purpose

Recommendations for future Work Packages, arising directly from what
`WP 6.1`'s own implementation found — not speculative feature requests,
and not a repeat of the architecture phase's own already-recorded
Future Extension Points (`Platform Services Overview.md`).

## Recommendation 1 — Retrofit `TD-09`/`TD-10`/`TD-11` as Their Own, Small, Explicitly-Scoped Work Package

**What.** A small Work Package (or a named sub-scope of `WP 6.8`) whose
entire brief is: insert `IPermissionEvaluator.RequirePermission` calls
into `NavigationService.Unregister`, the Command/Navigation registration
path, and the plugin-loading boundary.

**Why now, not later.** The enforcement point exists and is tested;
the remaining work is three small, mechanical insertions, not a design
problem. Deferring this indefinitely risks the same fate as `TD-01`
(re-scoped forward repeatedly) — except unlike `TD-01`, this debt has
genuine security relevance (`Security Roadmap.md` items 1, 2, 10).

**Estimated complexity.** Small — the hard part (deciding what the
enforcement point looks like) is already done.

## Recommendation 2 — `WP 6.3`'s Own Architecture Phase Must Resolve Identity's Trust Boundary Before Exposing It Over the Network

**What.** Before any REST endpoint can call
`IIdentityService.EstablishCurrentPrincipal` or resolve a principal from
an inbound request, `WP 6.3` must decide how an HTTP request
establishes *which* identity id it is entitled to claim — this Work
Package deliberately left that question open (`ADR-0043`), since no
untrusted caller existed yet to force the decision.

**Why this matters.** `IIdentityService.GetPrincipal` currently trusts
its caller completely. Wiring this directly into an HTTP request handler
without an intermediate authentication step would let any network caller
claim to be any principal.

## Recommendation 3 — Revisit `CurrentPrincipalAccessor` When `WP 6.3` Introduces Genuine Concurrency

**What.** Once the REST API can receive multiple simultaneous requests,
each potentially authenticated as a different principal, the current
ambient, `lock`-protected `CurrentPrincipalAccessor` will no longer be
sufficient — a request-scoped mechanism will be needed.

**Why not build it now.** No concurrent-request scenario exists yet to
test against; building isolation now would be speculative and, per this
Work Package's own finding, would break the current, real,
single-ambient-principal use case `IdentitySampleModule` demonstrates.
The right time to build it is when `WP 6.3` can prove it works against
a real concurrent workload.

**Suggested shape, not a commitment.** `WP 6.3`'s own architecture phase
should consider whether the REST API introduces its own request-scoped
accessor layered *on top of* the existing ambient one (falling back to
it outside a request context), rather than replacing
`CurrentPrincipalAccessor` outright — preserving the existing sample
module's own behaviour.

## Recommendation 4 — `WP 6.4` (Settings) Should Reuse the Config-Sourced-Definition Pattern `RoleProvider` Established

**What.** `RoleProvider`'s own approach — parse `IConfigurationProvider.
GetAll()` for a known key prefix/suffix shape, once, at construction —
is a clean, working pattern for any future service needing definitions
sourced from flat configuration. `WP 6.4`'s own `ISettingsProvider` (if
it ever needs to seed default setting definitions from configuration
rather than purely imperative registration) could reuse this exact
approach rather than inventing a parallel one.

**Why this is worth naming.** Not because it is required, but because
this project's own convention (`Reuse Before Invention`) is best served
by naming the reusable pattern explicitly here, while its reasoning is
fresh, rather than leaving `WP 6.4` to rediscover it independently.

## Not Recommended

- **Building a runtime-mutable role/principal administration surface
  now.** No named `v0.6.0` Work Package provides a safe, authorized
  surface to expose such administration (that would itself need
  Settings and its own authorization gate) — premature before `WP 6.4`
  and a REST-exposed admin capability exist.
- **Adding external identity-provider federation.** No named `v0.6.0`
  Work Package has a genuine need for it (`ADR-0043`); revisit only if
  one is actually named in a future release's own planning.

## Related Documents

`WP6.1 Implementation Report.md`; `WP6.1 Engineering Review Report.md`;
`WP6.1 Lessons Learned.md`; `WP6.1 Technical Debt Assessment.md`;
`ADR-0043`; `ADR-0044`; `docs/releases/v0.6.0/WorkPackages.md` (`WP
6.3`, `WP 6.4`, `WP 6.8`); `docs/security/Security Roadmap.md`.
