# ADR-0025: Plugin Failure Classification

## Status

Accepted — v0.4.0, WP 4.2B, 2026-07-23. Resolves the first of the two ADRs
`Plugin Manifest Architecture.md` named as required before Plugin Manifest
(`WP 4.2`) implementation may begin. The second — where Plugin Discovery
and Plugin Loading sit in `Host Lifecycle.md`'s phase table — remains
outstanding and is not addressed here.

## Context

`Plugin Manifest Architecture.md` designed a manifest that describes a
module before it is loaded, and named, without deciding, the question this
ADR settles: when something goes wrong while discovering or loading a
plugin, is that failure Host-fatal (like a platform-service failure,
ADR-0013) or isolated (like an individual module failure, ADR-0013's other
half)? That document's own reasoning already leaned toward isolated; this
ADR makes that lean an explicit, complete decision, covering every failure
category the brief for this work package named.

**Scope boundary, stated up front.** This ADR governs only failures
occurring in the *new* Plugin Discovery and Plugin Loading steps — reading
and validating manifests, and loading a plugin's declared assembly file
into the process, before that assembly's own module is ever handed to the
*existing*, unchanged module pipeline. Once a plugin's assembly has been
loaded, whatever module it contains flows through Module Discovery,
Registration, and Lifecycle exactly as any other module does today —
governed entirely by ADR-0013, already decided, not reopened here. Where a
failure category below happens to land after that handoff, this ADR says
so explicitly and defers to the existing, unchanged rule rather than
restating it.

## Decision

**A plugin's failure to discover or load never fails the platform.**
Every plugin-scoped failure is isolated to that one plugin: logged,
recorded, and skipped — the Host continues starting up, every other
plugin is still attempted, and Module Discovery proceeds exactly as if the
failed plugin had never existed. This holds even if *every* plugin fails —
a plugin-free run is exactly as valid an outcome as a plugin-free
installation with no plugins directory at all. The only exception is a
genuine defect in the Host's own plugin-loading orchestration itself (not
attributable to any specific plugin), which is Host-fatal, mirroring the
identical exception Module Initialisation's own failure behaviour already
carries (`Host Lifecycle.md`, Phase 8: "a failure in the Host's own
construction… is a Host-level bug, not a module failure, and is
Host-fatal").

### Failure Categories

| # | Category | Classification | Logging Severity | Notes |
|---|---|---|---|---|
| 1 | Manifest cannot be found | Not a failure (empty/absent plugins directory) — **or** Isolated (a candidate plugin folder is incomplete) | Information (absent) / Warning (incomplete candidate) | A plugins directory with zero plugins is a valid steady state, not an error. |
| 2 | Manifest malformed (invalid JSON, missing/blank required field) | **Isolated** | Warning | `InvalidPluginManifestException` (already named in `Plugin Manifest Architecture.md`). |
| 3 | Duplicate plugin identity (two manifests declare the same `Id`) | **Isolated** | Warning | The first manifest encountered, in Plugin Discovery's own deterministic scan order, wins; every subsequent manifest sharing that `Id` is rejected. See Alternatives Considered for why the whole batch is not rejected instead. |
| 4 | Incompatible platform version | **Isolated** | Information | Often a correct, expected outcome (an old plugin correctly declining to run on a newer platform) — not a mistake, so not a Warning. `IncompatiblePluginVersionException` (already named). |
| 5 | Missing assembly (manifest's declared `AssemblyFileName` does not exist) | **Isolated** | Error | More likely a genuine packaging/deployment mistake than categories 1–4. |
| 6 | Assembly load failure (`Assembly.LoadFrom` throws — corrupt file, missing native dependency) | **Isolated** | Error | |
| 7 | Dependency load failure (the plugin assembly loads, but a type it needs cannot be resolved) | **Isolated** | Error | May surface later than the initial load call — still attributed to, and isolated against, the originating plugin. |
| 8 | Reflection/type load failure while scanning the plugin's types | **Isolated** | Warning | **Already handled by existing, unchanged code** — `ReflectionFrameworkDiscoveryService.GetLoadableTypes` already catches `ReflectionTypeLoadException` and proceeds with whatever types did load. No new handling required; noted here so the classification table is complete. |
| 9 | Invalid module registration (duplicate module ID once a plugin's module reaches Registration) | **Out of scope — unchanged** | Unchanged | Governed entirely by existing Registration behaviour and ADR-0013 (Host-fatal), exactly as for any non-plugin module. Not reachable at the Plugin Discovery/Loading stage this ADR governs, since the module catalogue does not exist yet when a plugin's manifest is read. |
| 10 | Lifecycle exceptions during a plugin's module startup | **Out of scope — unchanged** | Unchanged | Governed entirely by WP 2.3's existing per-module isolation and ADR-0013. A plugin-sourced module is indistinguishable from any other module once it reaches Lifecycle. |
| 11 | Unexpected internal exception in the Host's own plugin-loading orchestration (not attributable to any specific plugin) | **Host-fatal** | Critical | The one exception to this ADR's isolation rule — mirrors Module Initialisation's own identical carve-out exactly. |

### What "Isolated" Guarantees, Uniformly

Every row classified **Isolated** above guarantees the same four things,
stated once here rather than repeated eleven times:

1. **Startup continues.** The Host proceeds to the next plugin candidate,
   and eventually to Module Discovery, regardless of this failure.
2. **The plugin is disabled for this run.** It does not become a module;
   no `ModuleState` ever applies to it, since that state machine begins
   only once a module actually reaches Registration. A future
   implementation should record *why* a plugin never got that far — see
   Future Considerations.
3. **Every other plugin candidate is still attempted.** One plugin's
   failure has no bearing on any other's outcome.
4. **The failure is always logged, in full** — the plugin's manifest-
   declared `Id` if the manifest was at least readable, otherwise the
   candidate's file path; the failure category; the underlying
   exception's type and message. Never silently swallowed, regardless of
   how minor or expected the category (category 4 in particular, despite
   its Information-level severity, is still always logged, every time).

### What Is Explicitly Not Introduced

- **No automatic retry.** A failed plugin stays failed for the life of
  that process run. The only way to "retry" is fixing the underlying
  problem and starting a new run — consistent with ADR-0015's existing
  no-restart decision; there is no partial, in-process retry mechanism to
  reintroduce here either.
- **No silent recovery of any kind.** Every failure category is logged
  unconditionally; there is no category, however benign, that produces no
  diagnostic trace.
- **No per-plugin "critical" opt-in.** Unlike `ICriticalBackgroundService`
  (ADR-0021), no manifest field lets a plugin declare its own failure
  Host-fatal. See Alternatives Considered for why this asymmetry with
  ADR-0021 is deliberate, not an oversight.

## Consequences

**Positive:**

- Directly satisfies this work package's own stated design principle,
  "fail one plugin, not the platform," as an unconditional guarantee, not
  a default that something can override.
- Deterministic and simple to reason about: eleven named categories
  collapse to three outcomes (not-a-failure, isolated, Host-fatal), and
  only one category is Host-fatal at all.
- Extends ADR-0013's existing boundary rather than complicating it — a
  plugin failure is treated exactly like a module failure, because a
  plugin's whole purpose is to *become* a module; there was no need to
  invent a fourth failure category the way ADR-0021 introduced a genuine
  third default for background services.
- Category 8 turned out to already be handled by existing, unchanged code
  — a concrete, useful finding this classification exercise surfaced,
  not assumed.

**Negative:**

- A future contributor reading only `Runtime Host Architecture.md`'s
  original two-category failure model (ADR-0013) will need this ADR to
  understand that plugin failures are a third, explicitly named
  application of the *isolated* half of that model — not a new category
  in their own right, but worth a citable pointer since plugins are a
  different kind of thing from modules on the surface.
- No mechanism exists yet for surfacing "which plugins failed and why" to
  anything outside the log — a real, present gap until a future
  diagnostics capability (`WP 4.8`) or the Plugin Manifest implementation
  itself records this in a queryable form. Named explicitly in Future
  Considerations rather than left implicit.

## Alternatives Considered

**Making plugin failures Host-fatal**, mirroring how Module Discovery's
own existing `DuplicateModuleIdException` is Host-fatal today. Seriously
considered — a plugin does, after all, feed into the same module catalogue
Module Discovery protects. Rejected: Module Discovery's Host-fatal
duplicate check protects the integrity of the platform's own, non-optional
module catalogue; a plugin is, by definition, optional add-on content, and
treating an optional component's failure as equivalent to a foundational
platform-service failure would contradict this work package's own explicit
design principle. Recorded as Rejected Design RD-0010.

**A per-plugin `IsCritical` manifest opt-in**, mirroring
`ICriticalBackgroundService` (ADR-0021). Seriously considered, given the
close resemblance to Background Services' own isolated-by-default,
opt-in-to-critical model. Rejected: a background service opts into
criticality as a *live, running component* making a self-assessment;
every failure category this ADR governs happens *before* a plugin's
module instance exists at all, so there is no live component available to
make that declaration meaningfully. A manifest-level `IsCritical` flag
would also be exactly the kind of speculative field `Plugin Manifest
Architecture.md` already declined to add without a real, demonstrated
need. Recorded as Rejected Design RD-0011.

**Rejecting the entire plugin batch when a duplicate identity is found**,
rather than isolating only the conflicting manifest(s). Considered for
category 3 specifically. Rejected in favour of the simpler, more
consistent rule already applied to every other category — isolate only
what actually failed — rather than introducing the one special case where
one plugin's mistake could disable every other, unrelated plugin too.

## Future Considerations

A future implementation (Plugin Manifest, `WP 4.2`, once its remaining
phase-table ADR is also settled) should record, in some queryable form,
which plugin candidates failed during a given run and why — not merely log
it. This ADR does not design that structure (a candidate shape,
`PluginLoadOutcome` or similarly named, recording the candidate's path or
declared `Id`, the failure category, and the underlying exception, is
suggested but not decided here) — only requires that whatever `WP 4.2`
builds remains readable by a future diagnostics capability (`WP 4.8`)
without this ADR needing to be revisited.
