# ADR-0026: Plugin Discovery and Plugin Loading Lifecycle Placement

## Status

Accepted — v0.4.0, WP 4.2C, 2026-07-23. Resolves the second, and last, of
the two ADRs `Plugin Manifest Architecture.md` named as required before
Plugin Manifest (`WP 4.2`) implementation may begin. Both prerequisite
ADRs (ADR-0025, this one) are now decided; the platform-version
prerequisite was resolved separately (WP 4.2A). **No architectural blocker
remains before Plugin Manifest implementation.**

## Context

`Runtime Host Architecture.md` named, since WP 2.7A, that plugin loading
"would need to happen before Module Discovery… so that Discovery's
`AppDomain.CurrentDomain.GetAssemblies()` default actually sees them" — but
never decided exactly where, nor worked out what that insertion actually
requires of the rest of the startup sequence. `Host Lifecycle.md`'s
13-phase table was treated as complete and frozen after WP 2.7A/B; this
ADR is the first time it is deliberately reopened, and does so with the
same rigour those original 13 phases received.

Three things this ADR's design review found, each shaping the decision:

1. **Plugin Discovery needs `ILogger`** — ADR-0025 assigned specific
   logging severities (Information through Critical) to eleven failure
   categories; none of that is possible before Logging Built.
2. **Plugin Discovery needs `IPlatformVersionProvider`** — the
   `MinimumPlatformVersion` compatibility check (ADR-0025, category 4;
   `Plugin Manifest Architecture.md`'s own Versioning Strategy) requires an
   already-resolved platform version to compare against. But
   `PlatformVersionProvider` is currently constructed inside the
   **Platform Services Registered** phase (Phase 6) — which comes *after*
   Module Discovery (Phase 4) in the existing table. Plugin Discovery must
   run *before* Module Discovery. These two facts are in direct tension
   and this ADR resolves it (see Decision).
3. **Module Discovery itself must not change at all** — its own
   `AppDomain.CurrentDomain.GetAssemblies()` default already sees any
   assembly loaded into the process by any means, including one a new
   Plugin Loading step loads via `Assembly.LoadFrom`. The entire design
   rests on this already-true fact, not on any new capability Discovery
   would need to gain.

## Decision

**Two new phases, inserted between the existing Phase 3 (Logging Built)
and Phase 4 (Module Discovery), numbered 3.1 and 3.2 — not a renumbering
of the existing thirteen.** Both occur while the Host is in the existing
`Starting` state; neither introduces a new `HostState` or a new
transition.

| # | Phase | Host State |
|---|---|---|
| 1 | Host Created | `Created` |
| 2 | Configuration Built | `Starting` |
| 3 | Logging Built | `Starting` |
| **3.1** | **Plugin Discovery** | `Starting` |
| **3.2** | **Plugin Loading** | `Starting` |
| 4 | Module Discovery | `Starting` |
| 5 | Module Registration | `Starting` |
| 6 | Platform Services Registered | `Starting` |
| 7 | Dependency Injection Built | `Starting` |
| 8 | Module Initialisation | `Starting` |
| 9 | Runtime Running | `Running` |
| 10 | Shutdown Requested | `Running` → `Stopping` |
| 11 | Module Disposal | `Stopping` |
| 12 | Service Disposal | `Stopping` |
| 13 | Host Disposed | `Disposed` |

### Two phases, not one

Plugin Discovery (read and validate manifests; decide which plugins are
eligible to load) and Plugin Loading (actually load each eligible
plugin's assembly into the process) are kept as two separate phases,
deliberately mirroring Module Discovery/Module Registration's own existing
shape: one phase finds and validates candidates without side effects, a
second phase commits to something with a real, harder-to-reverse effect
(loading an assembly cannot be undone without a process restart, exactly
as registering a module cannot be silently undone either). See
Alternatives Considered for the single-phase alternative and why it was
rejected — RD-0012.

### Resolving the `PlatformVersionProvider` ordering tension

`PlatformVersionProvider`'s own constructor requires nothing — not
Configuration, not any other platform service (`Platform Version.md`).
Nothing prevents constructing it earlier than WP 4.2A originally placed
it. **This ADR moves its construction to immediately follow Logging
Built, as an explicit entry-criterion Plugin Discovery depends on** — its
*registration* into the DI `ServiceCollection` (via `AddInstance`) remains
exactly where WP 4.2A put it, inside the unchanged Platform Services
Registered phase, since nothing needs to *resolve* it through DI before
Module Initialisation regardless. Construction and registration are
separate concerns; only construction needed to move. See Alternatives
Considered for why Plugin Discovery does not instead read version
metadata independently — RD-0014.

### Dependencies and guarantees, precisely

**Plugin Discovery's entry criteria:**
- Logging Built has completed — a working `ILogger` exists.
- `PlatformVersionProvider` has been constructed (moved earlier per the
  above) — `IPlatformVersionProvider.Version` is available.
- Configuration Built has completed. Plugin Discovery has **no hard
  dependency** on Configuration for its core function (the plugins root
  is a fixed convention, mirroring Discovery's own `AppDomain`-based
  default), but *may* optionally consult it in a future implementation
  (for example, to override the plugins directory path, or disable plugin
  loading entirely) — Configuration already exists by this point regardless
  of whether anything uses it yet.
- Module Discovery, Registration, the DI container, and every module have
  **not yet been touched** — none of them exist yet at this point, and
  none is needed. This mirrors Discovery's own existing independence from
  the DI container (ADR-0008) applied one phase earlier.

**Plugin Discovery's exit criteria / guarantees:**
- A deterministic, ordered list of valid, version-compatible plugin
  manifests exists. No assembly has been loaded yet.
- Every candidate that failed validation has been isolated per ADR-0025,
  logged at its assigned severity, and excluded from the list — this
  phase never throws for a plugin-scoped reason, only for the one
  Host-fatal category ADR-0025 names (a genuine defect in Plugin
  Discovery's own orchestration).

**Plugin Loading's entry criteria:** Plugin Discovery has completed with
its (possibly empty) list of validated manifests in hand.

**Plugin Loading's exit criteria / guarantees:**
- Every validated plugin's declared assembly has either been loaded into
  the process, or isolated per ADR-0025 (category 5, 6, or 7) and
  excluded — again, never Host-fatal for a plugin-scoped reason.
- **The guarantee Module Discovery depends on**: every successfully-loaded
  plugin assembly is now visible to
  `AppDomain.CurrentDomain.GetAssemblies()`. Module Discovery's own,
  completely unchanged code will find whatever `IModule` types those
  assemblies contain, exactly as it already finds types from any other
  loaded assembly.
- **A zero-plugins run is indistinguishable from today's behaviour.** If
  the plugins directory is absent or empty (ADR-0025, category 1 — not a
  failure), Plugin Discovery and Plugin Loading both complete
  immediately, having done nothing observable, and Module Discovery
  proceeds exactly as it does today, byte-for-byte. This is the concrete
  form of "Module Discovery remains completely unaware of plugins."

### Deterministic ordering

Plugin Discovery enumerates candidate plugin folders, **sorts them
ordinally by folder name first**, then processes them in that order. This
is deliberate and closes a precision gap in ADR-0025's own language
("first manifest encountered… wins," category 3): raw filesystem
enumeration order is not guaranteed stable across operating systems or
file systems, so duplicate-identity resolution must be pinned to
something stable and developer-controlled — folder name — not incidental
OS behaviour. Plugin Loading loads eligible plugins in that same order.
This mirrors `ReflectionFrameworkDiscoveryService`'s own commitment to
deterministic, ordinal ordering exactly, applied to a different sort key
because manifests, unlike types, have no reflectable `Id` until parsed.

### Failure handling

Governed entirely by ADR-0025, not restated here. Both new phases'
failure behaviour is: isolated for every plugin-scoped category, `Starting
→ Faulted` only for a genuine defect in the Host's own orchestration of
Plugin Discovery or Plugin Loading itself — exactly the same transition
Configuration Built, Logging Built, Module Discovery, and Module
Registration already use for their own Host-fatal failures. No new
transition, no new exception category beyond what ADR-0025 already named.

## Consequences

**Positive:**

- Resolves the last remaining architectural blocker before Plugin
  Manifest implementation — both required ADRs are now decided.
- No renumbering of the existing thirteen phases — every existing
  cross-reference in `Host Lifecycle.md`, `Runtime State Machine.md`,
  `Startup Sequence.md`, `Failure Behaviour.md`, prior ADRs, and every
  prior Academy retrospective that cites a phase by number remains
  correct, unchanged, and valid.
- No new `HostState`, no new transition — `Runtime State Machine.md`
  requires no changes at all.
- Module Discovery requires zero code changes, and this ADR shows exactly
  why: the guarantee it depends on (loaded assemblies are visible to its
  existing `AppDomain` scan) was already true before this ADR: Plugin
  Loading only needed to *use* that existing truth, not create a new one.
- Closes a real precision gap in ADR-0025 (deterministic duplicate
  resolution) as a direct consequence of designing the ordering carefully
  enough to need to state it.

**Negative:**

- `PlatformVersionProvider`'s construction now happens in a different
  place than WP 4.2A's own retrospective described (inside Platform
  Services Registered). A future implementer of `WP 4.2` must move that
  one line — a small, explicit, and now fully-documented adjustment, not
  a rediscovery.
- Two new phase numbers (`3.1`, `3.2`) are a new numbering shape
  (decimal, not sequential integer) for `Host Lifecycle.md`'s table — a
  reader needs to understand this means "between 3 and 4," not "version
  3.1 of phase 3." Judged clearer and far less invasive than renumbering
  every subsequent phase — see RD-0013.

## Alternatives Considered

**A single combined "Plugin Discovery" phase**, folding manifest
validation and assembly loading into one step. Rejected: it would blur a
side-effect-free step (reading and validating data) with a
side-effect-having, harder-to-reverse one (loading an assembly), breaking
the same distinction Module Discovery/Module Registration's own existing
two-phase split already protects. Recorded as RD-0012.

**Renumbering all thirteen existing phases** to make room for two new
sequential integers, rather than using decimal sub-numbering. Rejected:
the blast radius — every existing cross-reference across
`Host Lifecycle.md`, `Runtime State Machine.md`, `Startup Sequence.md`,
`Failure Behaviour.md`, prior ADRs, and prior Academy retrospectives that
cite a phase by number — would be entirely disproportionate to what is,
architecturally, a pure insertion. Recorded as RD-0013.

**Having Plugin Discovery read platform version metadata independently**,
rather than reusing the Host's single `PlatformVersionProvider` instance
(moved earlier). Rejected: this would directly contradict WP 4.2A's own
stated goal of "a single authoritative runtime platform version" and risk
two independent readings of the same metadata silently diverging. Moving
one constructor call is cheaper and strictly more correct than
maintaining two. Recorded as RD-0014.

## Future Considerations

If Background Services (`WP 4.5`) also need a new phase inserted before
Module Initialisation (per `docs/releases/v0.4.0/Risks.md`, R1), that
work package should follow this ADR's own precedent — decimal
sub-numbering, no renumbering of existing phases, explicit entry/exit
criteria stated with the same rigour — rather than re-deriving the
question of *how* to insert a phase from scratch.
