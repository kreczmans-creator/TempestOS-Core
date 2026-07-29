# WP 6.5 — Audit Framework — Future Capability Recommendations

## Purpose

Recommendations for future Work Packages, arising directly from what
`WP 6.5`'s own implementation and Persistence Validation found,
mirroring `WP6.1`/`WP6.4 Future Capability Recommendations.md`'s own
format.

## Recommendation 1 — Any Future Consumer (Reporting, REST API, Licensing, Export/Import) Should Decide Its Own `RecordAsync` Failure Policy Explicitly

**What.** Before any of `WP 6.0` (Reporting), `WP 6.3` (REST API), `WP
6.6` (Licensing), or `WP 6.7` (Export/Import) starts calling
`IAuditRecorder.RecordAsync` as a side effect of its own primary
operation, that Work Package's own design should explicitly state
whether an audit-write failure is allowed to abort the primary
operation or must be caught and treated as non-fatal.

**Why this matters.** `ADR-0045` deliberately left this as each
caller's own decision, since the correct answer genuinely differs (a
REST request recording a security-relevant action has different stakes
than a background report generation logging its own start). Leaving it
undecided at each Work Package's own design time risks an inconsistent,
ad hoc pattern emerging across the release.

## Recommendation 2 — Revisit `TD-12` Only When a Real Performance Problem or Scale Requirement Is Named

**What.** Do not add a native query/filter capability to
`IPersistenceStore` speculatively. Wait until a future Work Package (or
a real production deployment) actually measures a performance problem,
or until a concrete scale requirement (e.g., "must support N audit
records with sub-second query latency") is named by product or
engineering direction.

**Why not build it now.** `WP 6.5`'s own Persistence Validation
confirmed the current, minimal shape is functionally correct for every
approved query need — the only open question is performance at a scale
this release has no concrete requirement for. Building a query
capability now would be exactly the speculative capability this Work
Package's own instructions warned against.

**Suggested shape, not a commitment.** If the trigger fires, consider
whether an index-like secondary structure (e.g., a per-actor or
per-action key listing, maintained incrementally on write) would be
simpler than a general-purpose query language — matching this
project's own preference for the narrowest solution that satisfies a
real, demonstrated need.

## Recommendation 3 — `WP 6.7` (Export/Import) Should Treat Audit Records as a Natural Export Candidate

**What.** `Platform Service Contracts.md`'s own Future Extension Points
for Audit named "export of audit records... for compliance reporting"
explicitly. When `WP 6.7` begins, it should consider `IAuditQuery` as a
natural data source for a compliance-oriented export, using
`IExportable`'s own versioned, round-trip-safe contract — distinct from
Persistence's own internal, platform-owned storage, exactly as
`ADR-0051`'s own anticipated orthogonality between Export/Import and
Persistence already establishes.

**Why not build it now.** `WP 6.7` has not begun, and no concrete export
format requirement exists yet.

## Recommendation 4 — A Future REST API Endpoint Exposing `IAuditQuery` Should Reuse the Existing Permission, Not Invent a New One

**What.** `WP 6.3` (REST API), when it eventually exposes audit records
over HTTP, should require `AuditQuery.QueryPermission` (`"audit.query"`)
— the same permission this Work Package already established — rather
than inventing a REST-specific permission key. This keeps the
permission model consistent regardless of which caller (an in-process
module, a REST client) is asking.

**Why this is worth naming.** Not because it is required, but because
this project's own convention (`Reuse Before Invention`) is best served
by naming the reusable permission explicitly here, while its reasoning
is fresh, rather than leaving `WP 6.3` to rediscover or reinvent it.

## Not Recommended

- **Adding a retention/archival policy now.** No named `v0.6.0` Work
  Package has a concrete requirement for one; `Platform Service
  Contracts.md` itself names this as a plausible future requirement,
  not a current one.
- **Building a generalised "audit-aware" base class or attribute for
  automatic recording.** No real, demonstrated need for automatic,
  cross-cutting audit recording exists yet — every current recording
  call is explicit, in the calling code, exactly as `IAuditRecorder`'s
  own approved shape intends.

## Related Documents

`WP6.5 Implementation Report.md`; `WP6.5 Engineering Review Report.md`;
`WP6.5 Platform Impact Assessment.md`; `WP6.5 Lessons Learned.md`;
`WP6.5 Technical Debt Assessment.md`; `ADR-0045`; `docs/releases/v0.6.0/
WorkPackages.md` (`WP 6.0`, `WP 6.3`, `WP 6.6`, `WP 6.7`);
`docs/governance/Quality/Technical Debt Register.md` (`TD-12`).
