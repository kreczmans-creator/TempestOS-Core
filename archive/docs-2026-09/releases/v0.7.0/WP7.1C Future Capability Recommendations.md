# WP 7.1C — Materials Framework — Future Capability Recommendations

## Purpose

Recommendations for future Work Packages, arising directly from what
this Work Package's own implementation found — mirroring `WP7.1A`/
`WP7.1B Future Capability Recommendations.md`'s own format.

## Recommendation 1 — Candidate `F` (Calculation) Should Read a Material's Own Properties Directly Through `IMaterialCatalog.FindAsync`, Never Duplicate Them

**What.** When Candidate `F` begins, a calculation needing a material
property (e.g. yield strength for a structural check, once a Structural
capability exists) should call `IMaterialCatalog.FindAsync` and read the
relevant `MaterialProperty.Value` directly, rather than the calling
module copying the value into its own configuration or code.

**Why this matters.** This Work Package's own implementation proves the
full round trip works correctly (`RegisterAsync`/`FindAsync`/property
codec all tested exhaustively) — Calculation can rely on it directly.

## Recommendation 2 — `IEngineeringDocumentStore` Should Consider a Dedicated "Latest Revision Only" Lookup, Once a Second Consumer Needs It

**What.** If a future consumer (beyond Materials) also needs "just the
current content, not the full history," this is worth adding to
`IEngineeringDocumentStore` itself as a new method
(e.g. `GetCurrentRevisionAsync`), rather than each consumer
re-implementing `GetRevisionHistoryAsync(id)[^1]` independently.

**Why not build it now.** Only one consumer (`MaterialCatalog`) has this
need today (`TD-20`); building a new Data Model method for a single
consumer would be speculative. Revisit once a second, independent need
appears.

## Recommendation 3 — `FCR-0034` (Affine Unit Conversion) Should Confirm Whether Materials Needs a Temperature-Dependent Property Before Its Own Design Is Finalised

**What.** A future Work Package resolving `FCR-0034` should check
whether any real Materials property (e.g. a temperature-dependent
yield strength) is the concrete trigger justifying the work, rather than
resolving it in isolation from every potential consumer.

**Why not build it now.** No real Materials property currently needs
Temperature; `MaterialPropertyValueCodec`'s own seven-dimension bound is
sufficient for this Work Package's own scope.

## Recommendation 4 — A Future Materials-Discipline or Manufacturing Capability Should Extend `Category`'s Own Usage Conventions, Not Redesign the Field

**What.** If a future capability needs a more structured classification
than the open `Category` string this Work Package provides, it should
document a convention for the string's own contents (e.g. a
slash-delimited hierarchy) rather than changing `IMaterialSpecification.Category`'s
own type — preserving every existing registered material's own
already-set category value.

**Why not build it now.** No real discipline requirement has yet named
a concrete classification scheme to design against — closing this now
would repeat exactly the invention `WP 7.0A`/`WP 7.0B` both declined.

## Not Recommended

- **Adding permission-gating to `IMaterialCatalog` itself.** The
  approved contract names no such requirement as mandatory; if a future
  consumer needs access control over specific materials, that belongs
  at the calling layer (`AT-15`), mirroring every non-Audit-like
  Platform Service's own established pattern.
- **Extending `IEngineeringDocumentStore` with native query support to
  serve Materials specifically.** `TD-12`/`FCR-0007` already name this
  as a cross-cutting, unscheduled capability — `MaterialCatalog`'s own
  direct `IPersistenceStore` index (`ADR-0055`) already avoids needing
  it for this framework's own access pattern.

## Related Documents

`WP7.1C Implementation Report.md`; `ADR-0055`; `docs/releases/v0.7.0/
WP7.0C Engineering Foundation Contracts.md`; `docs/governance/Quality/
Technical Debt Register.md` (`TD-20`, `AT-15`).
