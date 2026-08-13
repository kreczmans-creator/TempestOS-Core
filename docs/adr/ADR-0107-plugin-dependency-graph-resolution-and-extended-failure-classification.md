# ADR-0107: Plugin Dependency Graph Resolution and Extended Failure Classification

## Status

Accepted — `v0.13.0`, `WP 13.0A` (Plugin Platform Architecture), 2026-08-13.
Architecture only; no code changes accompany this decision — implementation
is `WP 13.0B`'s own, separately-scoped task. Extends, and does not reopen,
ADR-0025 (*Plugin Failure Classification*) and ADR-0026 (*Plugin Discovery
Lifecycle Placement*).

## Context

`Plugin Manifest Architecture.md`'s own Non-Goals section named "module
*dependencies* (a plugin depending on another plugin)" as explicitly not
designed for `v0.4.0`. `Plugin Platform Architecture.md` (this release,
`WP 13.0A`) is the first work package with a genuine reason to design it —
the Product Owner's confirmed commitment to third-party plugin support
(`FCR-0001`, this release's own trigger) makes inter-plugin dependency a
real, near-term need rather than a speculative one.

Two existing, Accepted decisions constrain this one directly:

- **ADR-0025** classifies eleven plugin-scoped failure categories, every
  one of them **isolated** (never Host-fatal), governed by the single
  principle "a plugin's failure to discover or load never fails the
  platform." Any new failure category this ADR introduces must either fit
  inside that principle or explain, with the same rigour ADR-0025 itself
  used, why it cannot.
- **ADR-0026** places Plugin Discovery (Phase 3.1) and Plugin Loading
  (Phase 3.2) as two side-effect-separated phases: Discovery finds and
  validates candidates without side effects; Loading commits to the one
  side effect (loading an assembly) that cannot be undone without a
  process restart (ADR-0015). `Host Lifecycle.md`'s own phase table is
  frozen, approved architecture; this ADR does not reopen it, and does not
  require it to change — see Decision, below, for why a dependency graph
  fits inside the existing Phase 3.1 boundary without a new phase.

The concrete design questions this ADR settles: how are inter-plugin
dependencies declared, in what order does a set of interdependent plugins
load, what happens when a declared dependency is missing or
version-incompatible, and what happens when two or more plugins depend on
each other in a cycle.

## Decision

### Declaration

`PluginManifest` (v2, this document's own field-shape addition, detailed
in `Plugin Platform Architecture.md`) gains an optional
`Dependencies: IReadOnlyList<PluginDependency>` field — never `null`,
possibly empty, absent in a v1-shaped manifest defaulting to empty. Each
`PluginDependency` names another plugin's `Id`, a required
`MinimumVersion`, and an optional `MaximumVersion` (`null` = unbounded
above) — deliberately the same "minimum required, maximum optional"
asymmetry `Plugin Manifest Architecture.md`'s own `MinimumPlatformVersion`
already established for platform compatibility, applied here to a
different axis (one plugin's compatibility with another, not with the
platform — see this ADR's own Alternatives Considered for why this is not
a re-litigation of RD-0009).

### Where resolution happens — no new phase

Dependency graph construction and resolution is a **pure, side-effect-free
computation over the set of manifests Plugin Discovery has already
individually validated** (ADR-0025's existing per-manifest checks:
malformed manifest, duplicate identity, incompatible platform version — all
unchanged, all still run first). It therefore belongs entirely inside the
existing Phase 3.1 (Plugin Discovery), as a sub-step following individual
validation, exactly mirroring why Discovery and Loading were kept as two
phases in the first place (RD-0012) — graph resolution has no side effect
of its own, so it stays on the validation side of that boundary, not the
loading side. **No new Host Lifecycle phase, no change to `Runtime State
Machine.md`, no renumbering.**

### Ordering

Plugin Loading (Phase 3.2) loads plugins in **dependency-topological
order** (a plugin loads only after every plugin it depends on has already
been loaded, or excluded), with ADR-0026's existing ordinal folder-name
sort retained as the deterministic tie-break between plugins that share no
ordering constraint with each other. This directly extends, rather than
replaces, ADR-0026's own deterministic-ordering rule — folder name remains
the tie-break of last resort, exactly as it is today for plugins with no
declared dependency on one another at all (the common case, unaffected by
this ADR).

### Resolution algorithm — a fixed-point reduction, not a bespoke cascade

The surviving, individually-valid candidate set is reduced to a fixed
point: any candidate whose declared dependency is not present, or is
present but outside its declared `[MinimumVersion, MaximumVersion]` range,
in the *current* surviving set is removed; this repeats until a pass
removes nothing. What remains is topologically sorted (Kahn's algorithm)
for load order; any candidate excluded during reduction never reaches
Plugin Loading at all.

**This is deliberately the entire mechanism — no separate "cascade
notification" step exists.** If plugin A depends on plugin B and B is
itself excluded (for any reason, including a dependency of its own being
unmet), A is removed in the next reduction pass automatically — a chain of
any length isolates correctly with no bespoke propagation logic, and no
plugin ever needs to know *why* an ancestor it depends on disappeared, only
that it did. See Alternatives Considered for the bespoke-cascade
alternative this rejects (RD-0047).

**A cycle (A depends on B, B depends on A, directly or transitively through
others) is detected the same way missing dependencies are**: after the
fixed-point reduction, any node still present that is not reachable by the
topological sort (Kahn's algorithm terminates with unprocessed nodes
exactly when a cycle exists among them) is excluded — **every plugin
participating in the cycle, and only those plugins**, mirroring exactly
how a duplicate-identity conflict (ADR-0025, category 3) isolates only the
conflicting manifests, not the whole batch.

### Extended failure classification

Three new categories, numbered to continue ADR-0025's own eleven-row
table without renumbering any existing row:

| # | Category | Classification | Logging Severity | Notes |
|---|---|---|---|---|
| 12 | Missing plugin dependency (a declared `Dependencies` entry's `Id` is not present among the surviving, valid, compatible candidate set) | **Isolated** | Warning | The dependent is excluded; the missing dependency's own absence (if it failed validation itself) was already logged separately, at its own category's severity, when it was excluded. |
| 13 | Incompatible plugin dependency version (the named dependency is present but its `Version` falls outside the declared `[MinimumVersion, MaximumVersion]` range) | **Isolated** | Warning | Distinguished from category 12 for diagnostic clarity — "the dependency doesn't exist" and "the dependency exists but is the wrong version" are different, actionable facts for a plugin author, mirroring ADR-0025's own category 2/4 distinction (malformed vs. merely incompatible). |
| 14 | Circular plugin dependency (two or more plugins depend on each other, directly or transitively, with no valid topological order) | **Isolated — every participating plugin** | Warning | Not Host-fatal; see Alternatives Considered (RD-0046) for why a cycle is not treated as a defect in the Host's own orchestration. |

**All three are isolated, never Host-fatal** — a direct, uncontested
extension of ADR-0025's own governing principle, not a new principle. The
existing Host-fatal carve-out (a genuine defect in Plugin Discovery's own
orchestration, not attributable to any specific plugin) is unchanged and
ungrown by this ADR.

`PluginRegistryState.DependencyUnmet` (`Plugin Platform Architecture.md`,
Plugin Registry) is the queryable state a consumer observes for categories
12–14 alike — one queryable state, three distinguishable log-level causes,
mirroring how ADR-0025's own eleven categories already collapse to three
outcomes (not-a-failure, isolated, Host-fatal) for a reader who does not
need every category's own fine detail.

## Consequences

**Positive:**

- A plugin author gets a real, if narrow, dependency mechanism —
  version-range compatibility, deterministic load ordering, and honest
  failure isolation — without any new Host Lifecycle phase, any change to
  `Runtime State Machine.md`, or any weakening of ADR-0025's own
  "fail one plugin, not the platform" guarantee, now demonstrably
  extensible to "fail one dependency cluster, not the platform."
- The fixed-point reduction algorithm handles transitive failure,
  including arbitrarily deep dependency chains and cycles, with a single,
  uniform mechanism — no special-cased propagation logic to maintain or
  reason about separately.
- Cross-references ADR-0025/ADR-0026 by extension rather than duplication;
  a reader who already understands those two ADRs needs to learn only the
  three new rows and the one new sub-step, not a parallel classification
  scheme.

**Negative:**

- A plugin whose only defect is depending on a *popular* plugin that
  itself failed for an unrelated reason is excluded too, with no
  distinction in its own queryable state between "I am broken" and "my
  dependency is broken" beyond the logged `Detail` text — accepted, since
  inventing a fourth queryable state for "excluded only because of an
  ancestor" would add complexity with no demonstrated consumer need (see
  Alternatives Considered, RD-0048, for the closely related "soft
  dependency" idea this same reasoning rejects).
- Dependency resolution is a real, if bounded (candidate count is small —
  a local `Plugins/` directory, not a package registry), computation added
  to Phase 3.1's own runtime cost — not expected to be observable at any
  realistic plugin count, not measured, disclosed as an assumption rather
  than a proven bound.

## Alternatives Considered

**Host-fatal on any detected circular dependency.** Mirrors the identical
question ADR-0025 already answered for every other plugin-scoped failure
(RD-0010) — a cycle is a mutual defect between two or more *optional*
plugins, not a defect in the Host's own, foundational orchestration.
Treating it as Host-fatal would contradict this work package's own
governing principle for the same reason RD-0010 already rejected it once.
Recorded as **RD-0046**.

**A dedicated, separate cascade-notification step**, explicitly walking
the dependency graph a second time to mark every transitive dependent of a
failed plugin, distinct from the fixed-point reduction itself. Rejected:
the fixed-point reduction already produces the identical outcome as a
natural consequence of repeated passes — a second, bespoke mechanism would
duplicate behaviour the chosen algorithm already provides, adding a
maintenance burden with no corresponding benefit. Recorded as
**RD-0047**.

**A "soft" or optional dependency declaration** (a plugin depends on
another but should still load, in a reduced-capability mode, if that
dependency is unmet). Rejected as speculative — no real plugin ecosystem
exists yet to demonstrate this is a genuine need rather than a plausible-
sounding one, mirroring RD-0011's and RD-0022's identical reasoning for
declining a speculative opt-in with no demonstrated consumer. A future
work package may revisit this once a real plugin author demonstrates the
need. Recorded as **RD-0048**.

**A maximum *platform* version ceiling, reopened under cover of this
ADR's `MaximumVersion` field.** Not what this ADR does, stated explicitly
to prevent misreading: `PluginDependency.MaximumVersion` bounds one
plugin's compatibility with *another plugin*, a different axis entirely
from RD-0009's still-rejected platform-version ceiling. This ADR does not
reopen, supersede, or weaken RD-0009 — see `Plugin Platform
Architecture.md`'s own Version Compatibility section for that separate
question's own, independent treatment.

## Related Documents

`Plugin Platform Architecture.md` (this decision's own full context,
field shapes, and Plugin Registry design); `Plugin Manifest
Architecture.md` (the v1 baseline this extends); `ADR-0025` (*Plugin
Failure Classification*, extended, not reopened); `ADR-0026` (*Plugin
Discovery Lifecycle Placement*, whose two-phase boundary this decision
fits inside without change); `ADR-0015` (*Runtime Hosts Are Not
Restartable* — the reason a cycle or missing dependency found on one run
cannot be "retried" without a new process); `docs/architecture/Rejected
Designs.md` (RD-0009, RD-0010, RD-0011, RD-0012, RD-0022, RD-0046,
RD-0047, RD-0048).
