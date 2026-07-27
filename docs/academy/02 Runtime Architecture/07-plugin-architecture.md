# Plugin Architecture

## 1. Introduction

`src/Plugins/` sat empty in TempestOS's repository from WP 2.1 onward — a
gap named explicitly, repeatedly, and left deliberately unaddressed until
`WP 4.2` had the design experience (a queryable platform version, a
classified failure model, a settled place in the Host's own sequence) to
close it properly. This document explains, for a reader who has never
designed a plugin system before, what problem a plugin manifest actually
solves, why TempestOS's answer looks the way it does, and how it manages to
add "load code from disk at runtime" — normally a significant source of new
complexity — without changing a single line of Module Discovery,
Registration, or Lifecycle.

## 2. Purpose

To explain the governing idea behind TempestOS's plugin system — **the
Manifest describes; the Runtime decides** — and to walk through exactly how
a plugin's assembly goes from "a folder on disk" to "a running module,
indistinguishable from one that was compiled directly into the process,"
without any existing platform service needing to know plugins exist.

## 3. Background

A "plugin" for TempestOS is nothing more than an ordinary `IModule`
implementation that the platform did not already have compiled in — the
challenge is never "how do modules run," which the module pipeline
(Discovery → Registration → Lifecycle → Dependency Injection) already
solved completely by WP 2.4. The challenge is specifically: **how does a
module's compiled assembly get into the process at all**, before Discovery
runs its existing, unmodified `AppDomain.CurrentDomain.GetAssemblies()`
scan? Answering that one question — and answering it *before* touching any
assembly — is the entire scope of the plugin system.

## 4. The Problem

1. **What has to be known about a plugin before its assembly is ever
   loaded** — since loading an assembly is not free, and (per ADR-0015) not
   reversible without a full process restart?
2. **What happens when a plugin is broken** — a malformed file, an
   incompatible version, a missing dependency — and does one bad plugin
   get to take down the whole platform?
3. **Where, in the Host's own fixed startup sequence, does reading and
   loading plugins actually belong**, given it has to finish before Module
   Discovery runs, but needs some of the same platform services (logging,
   a way to check version compatibility) that don't exist until partway
   through startup themselves?
4. **Does Module Discovery — already fully built, tested, and stable since
   WP 2.1 — need to change at all** to see a plugin's module once its
   assembly is loaded?

## 5. The Design

**A manifest is a pre-discovery artifact.** `PluginManifest`
(`Tempest.Core.Plugins`) describes a plugin — `Id`, `Name`, `Version`,
`MinimumPlatformVersion`, `AssemblyFileName` — read from a
`plugin.manifest.json` file sitting in a folder, *before* its assembly is
ever loaded, let alone reflected over. This is the single most important
distinction the whole design rests on: `ModuleDescriptor` (WP 2.1) describes
something *already loaded and reflectable*; `PluginManifest` describes
something *not yet touched at all*. Confusing the two — trying to make one
type serve both purposes — was considered and explicitly rejected (see
Alternatives Considered, below).

**Two new, Host-owned phases, not one.** Plugin Discovery (reads and
validates every manifest; loads no assembly) and Plugin Loading (loads each
validated manifest's declared assembly) are kept deliberately separate,
mirroring Module Discovery/Module Registration's own existing split between
a side-effect-free step and a harder-to-reverse one. Both sit immediately
before Module Discovery in the Host's own fixed sequence (`3.1`/`3.2`,
decimal-numbered so none of the existing thirteen phases needed
renumbering — ADR-0026).

**Failure is classified exhaustively, and almost always isolated.** Eleven
named failure categories (a malformed manifest, a duplicate plugin identity,
an incompatible platform version, a missing or corrupt assembly, and so on)
collapse to three outcomes: not-a-failure, isolated (the one plugin excluded,
everything else proceeds), or Host-fatal (reserved for a genuine defect in
Plugin Discovery/Loading's *own* orchestration, not attributable to any
specific plugin) — ADR-0025. **Fail one plugin, not the platform** is this
design's version of ADR-0013's own platform-service/module boundary, applied
to a third category that is neither quite a platform service nor quite a
module.

**Module Discovery needs zero code changes.** This is not merely a design
goal — it is a provable fact about `AppDomain.CurrentDomain.GetAssemblies()`:
it already returns every assembly loaded into the process, by any means,
including one Plugin Loading loads via `Assembly.LoadFrom`. Nothing about
*how* an assembly arrived is Discovery's concern, and this design does not
ask it to become one — proven directly, in WP 4.2's own implementation, by
dynamically building a genuinely loadable assembly at test time and handing
it to the real, unmodified `ReflectionFrameworkDiscoveryService`.

## 6. Alternatives Considered

**An `IPluginManifestSource` abstraction**, generalising where a manifest
could come from (disk, a database, a remote registry). Rejected (RD-0008) —
no second source was in view, and the same "no consumer today" test that
governed WP 4.0's own contract scope applied here.

**A maximum, "tested up to," platform version field.** Rejected (RD-0009) —
`MinimumPlatformVersion` alone answers the only question that actually
matters today (can this plugin run on what's installed); a ceiling adds a
second comparison with no current consumer.

**An explicit "entry point type" field** on the manifest, naming which type
in the loaded assembly is the module. Rejected — it would have duplicated
Module Discovery's own type-scanning logic in a second place, for no
benefit: Discovery's existing scan already finds every `IModule`
implementation in whatever assembly it's given, plugin-sourced or not.

**A per-plugin `IsCritical` opt-in**, mirroring `ICriticalBackgroundService`
(ADR-0021). Rejected (RD-0011) — examined *why* that pattern works for a
background service (a live, running component capable of self-assessment)
and found the precondition does not hold for a manifest read before any
plugin code has executed at all; the pattern does not obviously transfer
just because the two concepts are both "optional, pluggable, might fail."

## 7. Why This Solution Was Chosen

Every non-obvious decision traces back to the same source: "the Manifest
describes; the Runtime decides." A manifest carries no behaviour and makes
no decisions — every decision (accept, isolate, load) belongs to the
Host's own services. This kept the manifest itself simple (five required
fields, each individually justified against a real consumer) and kept every
consequential decision (failure classification, sequence placement) in the
same place the platform already makes equivalent decisions for ordinary
modules.

## 8. Architectural Principles

- **The Manifest describes; the Runtime decides** — this design's own
  organising principle, restated at every responsibility boundary.
- **Reuse Before Invention** — `PluginManifest` reuses `ModuleDescriptor`'s
  own immutable-snapshot shape; `Plugin Discovery`/`Loading`'s failure model
  reuses ADR-0013's isolated/Host-fatal split rather than inventing a third
  category from scratch.
- **Fail Fast** — validation happens at Plugin Discovery time, before any
  assembly is loaded, not discovered awkwardly later via a failed
  `Assembly.LoadFrom` call.
- **Deterministic Startup** — candidate folders are sorted ordinally by
  name before any processing, so duplicate-identity resolution ("first
  encountered wins") means the same thing on every operating system and
  file system, not whatever order the filesystem happens to enumerate in.

## 9. Benefits

- A plugin author's broken manifest, incompatible version declaration, or
  corrupt assembly file affects only that one plugin — proven directly,
  including a dedicated test proving a genuine, unattributable orchestration
  defect *is* still Host-fatal, so the isolation boundary is exercised in
  both directions, not merely asserted for the common case.
- Zero code changes anywhere in Module Discovery, Registration, or
  Lifecycle — the clearest possible evidence that this release's own
  discipline ("reuse everything that already exists; do not redesign
  completed architecture") held for a genuinely new kind of capability, not
  only for incremental extensions of existing ones.
- `WP 4.2A`'s platform-version infrastructure — itself found as a blocking
  prerequisite *during* this design's own planning — is a direct, concrete
  example of an architecture-first pass finding a real gap before
  implementation had to discover it the hard way.

## 10. Trade-offs

- No assembly-unloading support — once loaded, a plugin's assembly stays
  loaded for the process's entire life (consistent with, not worse than,
  ADR-0015's restart policy).
- The plugins root directory and manifest file name are fixed conventions,
  not configurable, in this release — a deliberate, disclosed limitation,
  not an oversight; either can be made configurable later, purely
  additively.

## 11. Common Mistakes

The mistake most worth naming: treating "plugins can fail" as license to
reach reflexively for a critical/non-critical opt-in the moment a plugin
*looks* similar to a background service. The two are shaped differently at
the moment they can fail — a background service is a live, running,
self-assessing component; a plugin manifest is read before any plugin code
has ever executed — and recognising that difference, rather than assuming
similarity implies the same mechanism, is what correctly ruled the opt-in
out (RD-0011).

## 12. Future Evolution

- **A real, non-synthetic plugin** — every current test proves the pipeline
  against a genuinely loadable, dynamically-built assembly, but no
  hand-authored, shipped example plugin exists yet; `WP 4.3`'s own sample
  module remains available to be packaged this way later (RD-0015), once a
  real need for a shipped example exists.
- **Configurable plugin root/manifest name** — available additively,
  without needing to revisit anything decided here.
- **A future diagnostics capability** (`WP 4.8`) should be able to surface
  "which plugins failed, and why" from whatever structure this system
  produces — anticipated by ADR-0025's own Future Considerations, not
  designed here.

## 13. Key Takeaways

1. "The Manifest describes; the Runtime decides" is a small enough sentence
   to remember, and precise enough to resolve almost every
   responsibility-boundary question a plugin system raises.
2. Proving "the existing system needs zero changes" is only as strong as
   what the proof actually exercises — a real, dynamically-built,
   genuinely loadable assembly handed to the real, unmodified Discovery
   service is what proves this claim; a test that never loads anything
   real would not have.
3. A capability that resembles an existing pattern on the surface (plugin
   failure vs. background-service failure) still deserves its own,
   independent check of whether the *reason* the existing pattern works
   actually applies here — not an assumption that resemblance implies the
   same mechanism transfers.
