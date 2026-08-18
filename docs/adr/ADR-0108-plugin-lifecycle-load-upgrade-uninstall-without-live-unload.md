# ADR-0108: Plugin Lifecycle Covers Load, Upgrade, and Uninstall — Live, In-Process Unload Remains a Named, Cited Non-Goal

## Status

Accepted — `v0.13.0`, `WP 13.0A` (Plugin Platform Architecture), 2026-08-13.
Architecture only; no code changes accompany this decision — implementation
is `WP 13.0B`'s own, separately-scoped task. Reaffirms and extends, rather
than reopens, `Plugin Manifest Architecture.md`'s own "no assembly
unloading support" Risk, and applies ADR-0015 (*Runtime Hosts Are Not
Restartable*) to a second, plugin-specific case.

## Context

`Plugin Manifest Architecture.md`'s Risks section already named "no
assembly unloading support" as an explicit non-goal, "consistent with, and
no worse than, ADR-0015's existing 'no restart' decision." That was stated
as a `v0.4.0` non-goal, without the rigour of its own decision record —
this ADR is that decision record, revisited now that this release's own
trigger (a real, confirmed third-party plugin commitment, `FCR-0001`)
makes "what happens when a plugin needs fixing, upgrading, or removing"
a genuine, near-term operational question rather than a hypothetical one.

Two facts from already-Accepted architecture bound this decision tightly:

- **ADR-0015** decided a `TempestHost` instance is single-use:
  `Created → Running → Stopped → Disposed` (or `→ Faulted → Disposed`),
  never restarting in place. Its own reasoning is structural, not
  incidental: `RuntimeModuleManager` has no deregistration API,
  `TempestServiceProvider`'s singleton cache has no invalidation
  mechanism, and `ModuleLifecycleManager`'s state machine treats
  `Disposed` as terminal per module. None of these were built with reset
  semantics in mind, and a single plugin's assembly is loaded into the
  same process, through the same `AppDomain.CurrentDomain.GetAssemblies()`
  surface Module Discovery already depends on (`Plugin Manifest
  Architecture.md`) — there is no mechanism today by which a loaded
  assembly could be removed from a running process at all, let alone one
  whose module has already been registered, initialised, and started.
- **The isolation mechanism a real per-plugin unload would require —
  a dedicated `AssemblyLoadContext` (or equivalent) per plugin, collectible
  and disposable independently of the process's default load context — is
  explicitly the sibling Trust & Isolation Architecture's own decision to
  make, not this document's.** `Assembly.LoadFrom` (today's mechanism,
  `PluginAssemblyLoader`) loads into the default, non-collectible
  `AssemblyLoadContext` — an assembly loaded this way can never be
  unloaded, by any means, for the life of the process, regardless of
  anything this ADR could decide. Real unload is therefore not merely
  undesigned; it is mechanically unavailable under today's loading
  mechanism, full stop.

## Decision

**Live, in-process plugin unload remains a named, cited non-goal for
`v0.13.0`, exactly as `Plugin Manifest Architecture.md` already disclosed
it for `v0.4.0` — reaffirmed, not silently carried forward.** This
release's Plugin Lifecycle covers three real, designed operations —
**Load**, **Upgrade**, and **Uninstall** — none of which requires removing
an already-loaded assembly from a running process.

### The lifecycle state machine (per plugin, per process run)

```
Discovered → Validated → Loading → Loaded ──▶ (its IModule flows through
                │            │                  the existing, unchanged
                │            │                  Module pipeline)
                ▼            ▼
             Failed      Failed
                │
                ▼
          (Incompatible / DependencyUnmet are Validated-stage
           terminal outcomes, not Failed — see Plugin Platform
           Architecture.md, Plugin Registry)

          Disabled  (a separate, orthogonal terminal outcome — an
                     operator-configured skip, checked immediately
                     after a manifest's Id is known, before further
                     validation; see Plugin Platform Architecture.md)
```

Every state above is **reached at most once per process run** and is
**never re-entered** — the direct, plugin-scoped application of ADR-0015's
own "constructed once, torn down once" model, not a new model invented for
plugins. `Loaded` has no outgoing transition within this state machine
at all: once a plugin's assembly is loaded, its own `IModule` is owned
entirely by the existing, unmodified Module pipeline (Discovery,
Registration, Lifecycle) for the rest of the process's life, exactly as
`Plugin Manifest Architecture.md` already established.

### Load

Unchanged from `Plugin Manifest Architecture.md`/ADR-0026: Plugin Discovery
(3.1) validates, Plugin Loading (3.2) loads, in dependency-topological
order (ADR-0107). No behavioural change to the load path itself.

### Upgrade

**An upgrade is a file-system operation performed while the process is
not running, taking effect on the next process start — never a live,
in-process replacement.** Replacing a plugin's manifest and assembly files
under its own existing plugin folder with a newer `Version` is
indistinguishable, from Plugin Discovery's own point of view, from
discovering that plugin for the first time on a fresh process run — Plugin
Discovery carries **no memory across runs** (a direct consequence of
ADR-0015: a second run is always a new `TempestHostBuilder`/`TempestHost`,
with no state inherited from the run before it). No monotonic-version
check is imposed — Discovery does not compare a freshly-discovered
manifest's `Version` against any prior run's value, because no prior run's
value is available to compare against, by design.

### Uninstall

**An uninstall is the same file-system operation, subtractive rather than
replacing**: removing a plugin's folder (manifest and assembly) so the
next process start's Plugin Discovery simply never finds it. The
already-running process, if any, is unaffected — its already-loaded
plugin assembly, and any module state built from it, continues exactly as
before until the process itself stops, consistent with "no unload" being
mechanically true regardless of intent. An operator wanting to stop a
misbehaving plugin **before** the next restart uses the `Runtime:Plugins:Disabled`
configuration mechanism (`Plugin Platform Architecture.md`, Configurable
Plugins Root and Manifest Conventions) — which prevents the *next* Plugin
Discovery pass from attempting that plugin at all — not a live-unload
operation on the current run, which does not exist.

### The reserved seam — why this is a defended non-goal, not an oversight

The state machine above deliberately reserves the shape a future real
unload would need, without building it: a `Loaded → Unloading → Unloaded`
path is nameable, and no state or transition this ADR defines would need
to be redesigned to add it — only extended, additively, exactly as
`Plugin Manifest Architecture.md`'s own excluded-fields philosophy already
established for manifest content ("cheap to add later... none was excluded
because it would be expensive to introduce"). **The correct trigger to
build it**: the sibling Trust & Isolation Architecture adopts a per-plugin
isolation boundary (most plausibly a collectible `AssemblyLoadContext` per
plugin) **and** a real, demonstrated need exists for hot-upgrading a
running plugin without a full process restart — both conditions, not
either alone, mirroring ADR-0015's own Future Considerations: "a
hypothetical future need for in-process restart... should be built as a
new capability layered above `TempestHost`... rather than by adding reset
semantics to `TempestHost` or any of its collaborators directly." A future
per-plugin unload capability should follow the identical discipline,
layered above (or alongside) the existing Plugin Loading mechanism, not
retrofitted into it speculatively now.

## Consequences

**Positive:**

- Zero new reset/teardown semantics are required anywhere in the platform
  — `RuntimeModuleManager`, `TempestServiceProvider`, and
  `ModuleLifecycleManager` remain exactly as unmodified as ADR-0015
  already committed to, for a second, independent reason (plugins) that
  reaches the identical conclusion ADR-0015 reached for the Host as a
  whole.
- Upgrade and Uninstall are both fully real, useful operational
  capabilities — an operator is never left without a way to update or
  remove a plugin — despite neither requiring live unload.
- The reserved `Loaded → Unloading → Unloaded` seam gives a future work
  package a concrete, already-reasoned-about path to real unload, once
  its own two preconditions are met, rather than an undesigned void.

**Negative:**

- A plugin fix, once loaded, requires a full process restart to take
  effect — identical in kind to a platform-service fix today, and
  explicitly accepted as no worse, but a real, disclosed operational cost
  for a plugin author who might reasonably expect otherwise from a
  "plugin" system's own connotation.
- "No unload" combined with "no restart" (ADR-0015) means a genuinely
  malicious or badly-behaved *already-loaded* plugin cannot be forcibly
  stopped mid-process by this architecture alone — mitigation for that
  scenario is the sibling Trust & Isolation Architecture's own
  responsibility (a permission/capability check can still deny what a
  loaded-but-unprivileged plugin is *allowed to do*, even though this ADR
  cannot make it disappear from memory).

## Alternatives Considered

**Real, live per-plugin unload for `v0.13.0`, assuming the sibling Trust
& Isolation Architecture adopts a per-plugin `AssemblyLoadContext`.**
Seriously considered, as the brief for this work package explicitly
required. Rejected for this release on two independent grounds, either
alone sufficient: first, the isolation mechanism this would depend on is
not this document's decision and is not yet made — designing a lifecycle
around a mechanism that may or may not materialise, in the shape assumed,
would be speculative in exactly the way this project's own governing
discipline (`ADR-0015`'s Future Considerations, most directly) warns
against; second, no real, demonstrated operational need for hot-upgrading
a running plugin exists yet — `src/Plugins/` remains empty
(`Plugin Register.md`), so there is no real plugin whose downtime cost
during a restart-based upgrade has ever been measured or complained about.
Recorded as **RD-0049**.

**An automatic restart/backoff policy for a plugin that fails after
loading** (mirroring `ICriticalBackgroundService`'s isolated-by-default
model, or `RD-0029`'s identical question for hosted services). Rejected
for the same reason `RD-0029` rejected it for hosted services: designing a
retry/backoff policy now, with zero real plugins to test any policy
against, would be guessing at operational requirements no real plugin has
yet demonstrated. Recorded as **RD-0050**.

## Related Documents

`Plugin Platform Architecture.md` (this decision's own full context,
including the Plugin Registry's `Disabled`/`Failed`/`DependencyUnmet`/
`Incompatible` queryable states this state machine's terminal outcomes
populate); `Plugin Manifest Architecture.md` (the original, less formal
disclosure this ADR now formalises); `ADR-0015` (*Runtime Hosts Are Not
Restartable*, whose reasoning this ADR applies a second time, at the
plugin level); `ADR-0025`/`ADR-0026`/`ADR-0107` (failure classification and
ordering, unaffected by this decision); `Plugin Register.md` (confirms
`src/Plugins/` is still empty at the time of this decision);
`docs/architecture/Rejected Designs.md` (RD-0029, RD-0049, RD-0050).
