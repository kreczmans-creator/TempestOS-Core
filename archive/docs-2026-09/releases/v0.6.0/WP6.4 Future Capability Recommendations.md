# WP 6.4 — Settings Framework — Future Capability Recommendations

## Purpose

Recommendations for future Work Packages, arising directly from what
`WP 6.4`'s own implementation found, mirroring `WP6.1 Future Capability
Recommendations.md`'s own format.

## Recommendation 1 — `WP 6.5` Should Validate `IPersistenceStore` Against `IAuditQuery`'s Own Needs Before Committing to It

**What.** Before `WP 6.5` (Audit)'s own implementation begins, its
architecture phase (if one runs) or its own opening investigation should
explicitly check whether `IAuditQuery`'s anticipated filtered/range
queries (by actor, action, date range) can be satisfied efficiently by
client-side filtering over `IPersistenceStore.ListKeysAsync`, or whether
the abstraction needs a query capability added first.

**Why now, not later.** This is exactly the risk `docs/releases/v0.6.0/
Risk Register.md`'s own `R8` names, now confirmed (not merely
anticipated) by this Work Package's own shipped, minimal shape.
Deciding this before `WP 6.5` writes real code avoids either a
mid-implementation redesign of `IPersistenceStore` (affecting Settings,
already shipped) or an awkward, inefficient client-side workaround
discovered too late to reconsider cleanly.

**Estimated complexity.** Small as an investigation; potentially medium
if `IPersistenceStore` genuinely needs a new method (an additive
change, not a breaking one, per `ADR-0041`'s own Versioning Policy).

## Recommendation 2 — Add a Sensitive-Value Flag to `ISettingDefinition` Once a Real Sensitive Setting Is Named

**What.** The first future Work Package that needs to register a
setting holding genuinely sensitive data (a credential, an API key)
should add an `IsSensitive` (or equivalently-named) property to
`ISettingDefinition`, and have `SettingsProvider`'s own logging
consult it before including a value in a log entry.

**Why not build it now.** No setting in this release holds sensitive
data — building the flag now would be a speculative interface change,
which this Work Package's own instructions were careful to avoid making
without genuine necessity.

**Suggested shape, not a commitment.** Consider whether this should be
an additive property on `ISettingDefinition` itself, or a separate,
parallel classification service — the former is simpler and more
consistent with this codebase's own preference for minimal indirection,
but the actual owning Work Package should make this call once it has a
real setting to design against.

## Recommendation 3 — Per-Principal Settings and a Strongly-Typed Abstraction Remain Legitimate, Separately-Scoped Future Work

**What.** "User settings" (per-principal, not global) and a strongly
typed settings abstraction (rather than string-valued) were both named
in this Work Package's own implementation brief but were not part of
any approved contract — neither was built. Both are already-named
Future Extension Points in `Platform Services Overview.md`
("Per-principal... settings, once Identity & Permissions exists in a
mature enough form").

**Why not build them now.** Per-principal settings depend on Identity &
Permissions being mature enough to scope a setting to a specific
principal meaningfully — `WP 6.1`'s own local-only, single-ambient-
principal model (this release's own scope) does not yet support that
distinction in a way that would make per-principal settings
meaningfully different from global ones. A strongly-typed abstraction
is a genuine, separate design question (parsing/validation per type,
a type registry, and so on) that was never scoped into this Work
Package's own approved contracts.

**Suggested next step.** If either becomes a real, named need for a
future release, it should get its own architecture-phase treatment
(mirroring `WP 5.0A`/`WP 5.0B`'s own precedent) rather than being
retrofitted into `Tempest.Core.Settings`'s own already-shipped, approved
shape without one.

## Recommendation 4 — `WP 6.3` (REST API) Should Reuse `SettingsSampleModule`'s Own Get/Set Command Pattern

**What.** `SettingsSampleModule`'s own `GetSampleSettingCommand`/
`SetSampleSettingCommand` pair, dispatched through the Command
Framework, is a direct, working template for how a future REST-exposed
settings-management endpoint (already named as a Future Extension Point
in `Platform Services Overview.md`) should be structured: a REST route
maps to a command Id, dispatched through `ICommandRegistry.InvokeAsync`
— exactly `ADR-0048`'s own anticipated design for REST endpoints
generally, not a Settings-specific mechanism.

**Why this is worth naming.** Not because it is required, but because
this project's own convention (`Reuse Before Invention`) is best served
by naming the reusable pattern explicitly here, while its reasoning is
fresh.

## Not Recommended

- **Building a query capability into `IPersistenceStore` speculatively,
  before `WP 6.5` confirms it actually needs one.** Rejected as
  premature — Recommendation 1's own investigation should happen first.
- **Retrofitting per-principal settings now, using `WP 6.1`'s own
  local-only Identity model.** Rejected — that model does not yet
  support a meaningful per-principal distinction; building on top of it
  now would likely need rework once Identity & Permissions matures.

## Related Documents

`WP6.4 Implementation Report.md`; `WP6.4 Engineering Review Report.md`;
`WP6.4 Lessons Learned.md`; `WP6.4 Technical Debt Assessment.md`;
`ADR-0041`; `ADR-0042`; `docs/releases/v0.6.0/WorkPackages.md` (`WP 6.3`,
`WP 6.5`); `docs/releases/v0.6.0/Platform Services Overview.md` (the
Future Extension Points this document elaborates on).
