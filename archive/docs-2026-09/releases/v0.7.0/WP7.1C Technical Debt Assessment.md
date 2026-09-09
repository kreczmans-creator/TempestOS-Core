# WP 7.1C — Materials Framework — Technical Debt Assessment

## Purpose

Discloses every new debt item or trade-off this Work Package's own
implementation introduces, and confirms which existing debt items (if
any) it touches — mirroring `WP7.1A`/`WP7.1B Technical Debt
Assessment.md`'s own format.

## Existing Debt: What Actually Happened

**No existing Technical Debt Register item (`TD-01` through `TD-19`) is
touched by this Work Package.** `Tempest.Core.Materials` depends only on
`IEngineeringDocumentStore` and `IPersistenceStore`, neither of which
this Work Package modifies. `TD-19`/`FCR-0034` (affine unit conversion)
is referenced, not touched — Materials' own property values simply
inherit the same seven-dimension boundary Units & Quantities already
established.

## New Debt Disclosed by This Work Package

### TD-20 — `MaterialCatalog` Reads Full Revision History on Every Lookup

**What.** `FindAsync`/`ListAsync` call
`IEngineeringDocumentStore.GetRevisionHistoryAsync` and take only the
last entry — there is no dedicated "latest revision only" lookup on
that interface.

**Why this is debt, not merely a limitation.** A material with many
revisions pays for reading all of them on every lookup, an
`O(revisions)` cost for what should be an `O(1)` operation.

**Revisit trigger.** A real, measured performance problem with a
many-revision material, or `IEngineeringDocumentStore` gaining a
dedicated latest-revision lookup of its own (which would benefit every
consumer, not only Materials).

## New Accepted Trade-off Disclosed by This Work Package

### AT-15 — No Permission-Gating Inside `IMaterialCatalog` Itself

**What.** `IMaterialCatalog` performs no authorization check of its
own — a caller registering or revising a material is expected to
enforce authorization at the calling layer, mirroring
`IReportingService`/`INavigationProvider`'s own established precedent.

**Why this is a trade-off, not debt.** This mirrors the majority
pattern already established by every non-Audit-like Platform Service in
this codebase — only recording services (Audit) embed Identity
directly, for attribution, not authorization. `WP7.0C Engineering
Foundation Contracts.md`'s own Platform Services Consumed line named
"Identity & Permissions (registration authorization)" as a plausible,
not mandatory, integration.

**Revisit trigger.** A real, demonstrated need for authorization
enforcement specifically inside `IMaterialCatalog` itself, rather than
at each caller's own calling layer.

## Summary Table

| # | Item | Status | Revisit Trigger |
|---|---|---|---|
| TD-20 | Full revision history read on every `FindAsync`/`ListAsync` call | New, Open | A real, measured performance problem, or a dedicated latest-revision lookup on `IEngineeringDocumentStore` |
| AT-15 | No framework-internal permission-gating | New, Accepted Trade-off | A real, demonstrated need for framework-internal authorization |

**Total: 1 new debt item disclosed, 1 new accepted trade-off disclosed,
0 existing items worsened.**

## Related Documents

`docs/governance/Quality/Technical Debt Register.md` (updated with
`TD-20`/`AT-15` in this same Work Package); `ADR-0055`; `WP7.1C
Implementation Report.md`.
