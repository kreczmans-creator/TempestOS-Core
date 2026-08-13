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

**Extended, `WP 13.0A`.** Everything above answered "how does a plugin's
code get into the process." It deliberately did not answer "what is a
plugin's code, once loaded, actually trusted to do" — `Plugin Manifest
Architecture.md`'s own Risks section named that gap explicitly ("Security
is an accepted, named gap, not a solved problem… sandboxing, signing, and
permissions are all explicit non-goals, not omissions"), and three
Technical Debt Register items (`TD-09`, `TD-10`, `TD-11`) carried the
identical revisit trigger — "the first Work Package with a genuine reason
to build an authorization concept" (`TD-09`/`TD-10`/`TD-11`) — since
`v0.5.0`/`v0.6.0`. `v0.13.0` — "Trust & Deployment Hardening" — is that
Work Package: the Product Owner's confirmed commitment to third-party
plugin support (`FCR-0001`) fired the trigger `Security Roadmap.md` items
1, 2, and 10 each named as their own precondition. `WP 13.0A` answered it
in two composed architecture documents — `Plugin Platform Architecture.md`
(dependency graph resolution, the Plugin Registry, lifecycle, DI
boundaries — `ADR-0107`–`ADR-0109`) and `Plugin Trust & Isolation
Architecture.md` (trust tiers, the capability model, signing, the
isolation-boundary decision — `ADR-0110`–`ADR-0112`) — summarised in §5,
below, and covered in full by `WP13.0A-plugin-and-registration-trust-isolation-architecture.md`.

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

**Extended, `WP 13.0A`** — four further questions, deliberately left
unanswered until a real trigger existed to answer them responsibly:

5. **What is a loaded plugin actually trusted to do**, given `TD-09`
   already names it as indistinguishable, today, from a first-party
   module the instant its assembly loads?
6. **How does the platform know a plugin's declared identity (its
   `Publisher`) is real**, without inventing a new runtime dependency or a
   full PKI the platform's own local-only, non-networked deployment model
   does not need?
7. **Does an `AssemblyLoadContext` per plugin — the mechanism most
   engineers reach for first — actually solve the trust problem**, or
   only a different problem (assembly identity/unload) that merely looks
   similar?
8. **How does `NavigationService.Unregister` (`TD-10`) and Command/
   Navigation registration-order squatting (`TD-11`/`CMD-1`) get closed**
   without redesigning either already-shipped, approved public contract?

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

**The current, implemented pipeline** (Plugin Discovery → Plugin Loading →
Module Discovery), exactly as described above:

```mermaid
sequenceDiagram
    autonumber
    participant Host as TempestHost
    participant PD as PluginManifestDiscoveryService<br/>(Phase 3.1 — Plugin Discovery)
    participant PV as IPlatformVersionProvider
    participant PL as PluginAssemblyLoader<br/>(Phase 3.2 — Plugin Loading)
    participant AD as AppDomain.CurrentDomain
    participant MD as IFrameworkDiscoveryService<br/>(Phase 4 — Module Discovery, unchanged)

    Note over Host: Logging Built (Phase 3) has completed.<br/>PlatformVersionProvider constructed early (ADR-0026)<br/>so MinimumPlatformVersion is checkable.

    Host->>PD: Discover plugin manifests
    PD->>PD: Scan Plugins/ for plugin.manifest.json<br/>(candidate folders sorted ordinally by name)
    loop each candidate folder
        PD->>PD: Parse + validate manifest<br/>(Id, Name, Version, MinimumPlatformVersion, AssemblyFileName)
        alt malformed JSON or missing/blank required field
            PD-->>PD: InvalidPluginManifestException<br/>isolated (ADR-0025) — excluded, logged
        else duplicate Id (first encountered wins)
            PD-->>PD: DuplicatePluginIdException<br/>isolated (ADR-0025) — excluded, logged
        else well-formed
            PD->>PV: Compare MinimumPlatformVersion
            alt exceeds running platform version
                PD-->>PD: IncompatiblePluginVersionException<br/>isolated (ADR-0025) — excluded, logged
            else compatible
                PD->>PD: Resolve AssemblyPath; accept manifest
            end
        end
    end
    PD-->>Host: IReadOnlyList&lt;PluginManifest&gt;<br/>(validated, version-compatible, possibly empty)

    Host->>PL: Load plugin assemblies
    loop each eligible manifest, in Discovery's deterministic order
        PL->>AD: Assembly.LoadFrom(AssemblyPath)
        alt assembly file missing
            PL-->>PL: PluginAssemblyNotFoundException<br/>isolated (ADR-0025) — excluded, logged
        else load failure (corrupt/bad image/IO)
            PL-->>PL: PluginAssemblyLoadException<br/>isolated (ADR-0025) — excluded, logged
        else load succeeds
            AD-->>PL: Assembly now resident in the process
        end
    end
    PL-->>Host: Loading complete<br/>(every eligible plugin either loaded or isolated)

    Note over Host,MD: A genuine defect in Plugin Discovery/Loading's<br/>own orchestration (not attributable to any one plugin)<br/>is Host-fatal — Faulted (ADR-0025), not shown above.

    Host->>MD: DiscoverModules() — Phase 4, entirely unchanged
    MD->>AD: GetAssemblies()
    AD-->>MD: Every loaded assembly —<br/>first-party and plugin-loaded, indistinguishable
    MD->>MD: Reflect for IModule implementations
    MD-->>Host: IReadOnlyList&lt;ModuleDescriptor&gt;

    Note over MD: Module Discovery required zero code change for plugin<br/>support — proven directly by WP 4.2's own test suite against<br/>a real, dynamically-built, genuinely loadable assembly.
```

### Extended, `WP 13.0A`: the Plugin Platform and the Trust Boundary

**Status: architecture only — `docs/architecture/Plugin Platform
Architecture.md` and `docs/security/Plugin Trust & Isolation
Architecture.md`, not yet implemented.** `WP 13.0B` implements what
follows; nothing below changes running behaviour today. Both documents
extend, and do not replace, everything above — every v1 field, phase, and
failure category `Plugin Manifest Architecture.md`/ADR-0025/ADR-0026 already
settled is unchanged.

**The Plugin Platform (`ADR-0107`–`ADR-0109`) answers the *shape*
questions `Plugin Manifest Architecture.md` deliberately left as
non-goals**, now that a real trigger (the confirmed third-party plugin
commitment) exists to answer them: `Dependencies` (a manifest may declare
a version-ranged dependency on another plugin; Plugin Discovery resolves
the graph and Plugin Loading loads in topological order — a missing,
incompatible, or circular dependency isolates only the affected
plugin(s), extending ADR-0025 with categories 12–14, never Host-fatal); a
Host-owned, never-DI-public `IPluginRegistry` recording every candidate's
outcome (mirroring `IRuntimeModuleManager`'s own ADR-0017 boundary),
projected read-only through a new `IDiagnosticsProvider.Plugins` property
— the identical `Func<T>`-accessor extension ADR-0039 already established
for `Modules`/`HostedServices`, not a new service; a lifecycle covering
load/upgrade/uninstall as file-system operations taking effect on the
*next* process start (`ADR-0108`) — **live, in-process unload remains a
named, defended non-goal for `v0.13.0`**, exactly as `Plugin Manifest
Architecture.md`'s own Risk already disclosed, now formalised into a
decision record rather than silently carried forward; and confirmation
that a plugin contributes capability to the rest of the platform exactly
as any module already does — through `IEventBus`/`INavigationProvider`/
`ICommandRegistry`, called from its own Module Initialisation step, never
through a plugin-specific registration API or raw `IServiceCollection`
access (`ADR-0109`). The plugins root directory and manifest file name —
fixed conventions since `WP 4.2`, `TD-06`'s own disclosed limitation —
become configurable (`Runtime:Plugins:RootDirectory`/`ManifestFileName`/
`Disabled`), exercising a seam ADR-0026 already anticipated rather than
opening a new one.

**The Trust & Isolation Architecture (`ADR-0110`–`ADR-0112`) answers the
question this article's own §3 Background named as extended scope**: what
a loaded plugin is trusted to do. Four points carry the whole design:

- **A four-tier trust model** — First-Party (compiled-in, or a
  plugin signed against TempestOS's own publisher certificate;
  unrestricted, not subject to any capability check — this design changes
  nothing observable for any actor that exists today), Verified-Signed (a
  plugin signed against any other trust-store certificate; granted
  exactly what its manifest's `RequestedCapabilities` declares),
  Unsigned-Local (no signature; loads only if
  `Plugins:AllowUnsignedLoad` is explicitly `true`, default `false` —
  fail closed, mirroring `ADR-0043`'s identical fail-closed precedent;
  clamped to a fixed, low capability ceiling regardless of what it
  requests), and Untrusted (a signature present but failing to verify;
  never loads — always worse than no signature, never silently downgraded
  to Unsigned-Local). Assigned exactly once, at Plugin Loading, immutable
  for the process run.
- **A capability model reusing `Permission`/`IPermissionEvaluator`
  directly** (`ADR-0044`, not a parallel mechanism) — a closed set of
  `plugin.*`-namespaced keys (`plugin.navigation.register`,
  `plugin.commands.register`, `plugin.di.register`,
  `plugin.events.publish:<Type>`, `plugin.services.resolve:<Type>`),
  enforced two ways: **statically**, at Plugin Loading — a capability
  eligibility check against the assigned tier's ceiling, and a
  constructor-conformance check reflecting over the plugin's own `IModule`
  types before Module Discovery ever runs, rejecting any constructor
  parameter type outside the fixed baseline plus the plugin's own granted
  `plugin.services.resolve:*` declarations (this is the concrete
  mechanism that closes the "resolve a given service" half of `TD-09`,
  with zero change to `TempestServiceProvider`, Module Discovery,
  Registration, or Lifecycle); and **dynamically**, at the call site —
  one `RequirePermission` call added to `NavigationService.Register`/
  `Unregister`, the Command Framework's registration path, and
  `IEventBus.PublishAsync`, skipped entirely when the caller is
  `null`/First-Party, so every actor that exists today observes zero
  behavioural change. A new, second identity axis —
  `ICurrentComponentAccessor` (`IPrincipal? Current { get; }`,
  `AsyncLocal<T>`-backed, scoped around every re-entry into plugin-owned
  code) — answers "which loaded component's own code is currently
  executing," distinct from `ICurrentPrincipalAccessor`'s existing
  *user*-principal axis; this is exactly the layered, request/call-scoped
  accessor `ADR-0044` itself anticipated ("more likely… \[a\] request-scoped
  accessor layered on top of this one"), not a revision of it.
- **A detached, hash-based signature** (`ADR-0112`) over a canonical
  SHA-256 hash of the manifest plus a SHA-256 hash of the declared
  assembly's bytes, verified with .NET's own `System.Security.Cryptography`
  primitives — zero new dependency — entirely at Plugin Discovery (Phase
  3.1), before any `Assembly.LoadFrom` call, against a flat-file
  `TrustedPublishers/` certificate store (the same "fixed convention now,
  purely additive to make configurable later" precedent the plugins root
  itself established). No network call, no CRL/OCSP check — a local-only
  verification model mirroring `ADR-0043`'s own local-only identity
  precedent.
- **The isolation-boundary decision: capability scoping, entirely
  in-process — not a separate `AssemblyLoadContext`, not process
  separation** (`ADR-0110`). Stated with an explicit, disclosed limit:
  this is not a hard sandbox, and does not defend against an actively
  adversarial assembly willing to use reflection to bypass its own
  declared trust (that would require OS-process isolation, out of scope,
  with its own named revisit trigger). It closes the *ordinary,
  cooperative API surface* — exactly the surface `TD-09`/`TD-10`/`TD-11`/
  `CMD-1` describe. See Alternatives Considered, below, for why
  `AssemblyLoadContext` — the mechanism most engineers reach for first —
  was rejected as the trust boundary specifically. **This decision does
  not enable per-plugin unload**; `ADR-0108`'s own reserved
  `Loaded → Unloading → Unloaded` lifecycle seam stays reserved, unused,
  under this design.

**The trust-ordered registration rule directly closes `TD-10` and
`TD-11`/`CMD-1` at the architecture level**, without an Id-namespace
convention retrofitted across every existing first-party Id: ownership is
captured out-of-band at `Register`, with `Unregister` rejecting a caller
whose component principal does not match the recorded owner (`TD-10`);
"first registration wins" becomes "first registration wins **among
registrants of the same trust tier**," with a higher-tier registration
always evicting and replacing a lower one regardless of order, logged
loudly, never silently (`TD-11`/`CMD-1`) — unchanged behaviour for every
registrant that exists today, all of which are First-Party.

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

**Extended, `WP 13.0A` — the isolation-boundary question, considered four
ways:**

**A separate `AssemblyLoadContext` (ALC) per plugin.** The Security
Roadmap's own first-named option, and a real, available .NET mechanism.
Rejected as the *trust* boundary specifically — Code Access Security and
AppDomain sandboxing were removed entirely from .NET Core; an ALC governs
assembly *identity and unload*, not *privilege*. A plugin loaded into its
own ALC still runs with the full process's own OS privileges, can still
call any public API of any type loaded anywhere in the process (the exact
fact `AppDomain.CurrentDomain.GetAssemblies()` already depends on,
ADR-0026), and can still hold and use any DI-resolved service reference it
was ever given. Its one genuine benefit — enabling unload — is explicitly
a non-goal this release does not use; building it now would be exactly
the "security theatre ahead of a real need" `Security Principles.md`
Principle 7 warns against.

**Separate OS process per plugin, with IPC to the Host.** The only
mechanism providing a *genuine* privilege boundary. Rejected as
disproportionate to the actual, disclosed threat — a Product Owner
commitment to *vetted, signed, commercial* third-party plugins, not an
open marketplace accepting anonymous, adversarial publishers. Would also
require redesigning DI resolution, event dispatch, and every module
constructor-injection point across an IPC boundary — an order of
magnitude larger than this Work Package's own brief. Revisit trigger:
TempestOS is ever asked to run genuinely adversarial, unvetted third-party
code, not signed, accountable commercial plugins.

**Code-signing alone, with no capability scoping.** Solves a real problem
(authenticity, tamper-detection) but says nothing about what a
legitimately-signed plugin may then *do* once loaded — a properly signed,
fully accountable plugin would still receive `TD-09`'s exact, unrestricted
DI-container trust. Rejected as a complete answer on its own.

**A manifest-declared capability scope alone, with no signing.** Rejected
— without a signature, "the manifest declares X capabilities" is an
unverifiable, self-asserted claim with no accountability behind it;
nothing prevents a malicious manifest from declaring every capability it
wants. Signing is what makes the declaration mean something.

**Combination — signing decides trust tier; capability scope, gated by
tier, decides runtime permission — chosen.** Proportionate to the actual,
disclosed threat; reuses `ADR-0044`'s existing enforcement point almost
unchanged; requires no new runtime dependency; honest about its own
residual limit (not a hard sandbox) rather than overclaiming one.

## 7. Why This Solution Was Chosen

Every non-obvious decision traces back to the same source: "the Manifest
describes; the Runtime decides." A manifest carries no behaviour and makes
no decisions — every decision (accept, isolate, load) belongs to the
Host's own services. This kept the manifest itself simple (five required
fields, each individually justified against a real consumer) and kept every
consequential decision (failure classification, sequence placement) in the
same place the platform already makes equivalent decisions for ordinary
modules.

**Extended, `WP 13.0A`.** The trust design extends the identical sentence
one step further: the Manifest now also describes what a plugin *wants* to
do (`RequestedCapabilities`) and *who signed it* (`Signature`) — still
inert data, still interpreted nowhere inside the manifest itself — and the
Runtime still makes every consequential decision (which tier, which
capabilities are actually granted, whether a call is permitted) through
the platform's one existing enforcement point, `IPermissionEvaluator`
(`ADR-0044`), rather than a second, parallel authorization mechanism.
Choosing capability-scoped, in-process enforcement over `AssemblyLoadContext`
or process separation is the same "Reuse Before Invention" reasoning
`Plugin Manifest Architecture.md` already applied to `ModuleDescriptor`
and `IFrameworkDiscoveryService` — apply the platform's own existing
authorization mechanism to a new principal kind, rather than build a
second, structurally different trust mechanism because the *actor*
(a plugin) is new, even though the *question* (is this action permitted)
is not.

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
- **Fail Closed** (`WP 13.0A`) — an unsigned plugin loads only if an
  operator has explicitly set `Plugins:AllowUnsignedLoad` to `true`;
  the default is `false`. Mirrors `ADR-0043`'s identical fail-closed
  precedent for an unrecognised identity: the absence of a decision
  defaults to the safer outcome, never the more permissive one.
- **Reuse Before Invention, applied to authorization** (`WP 13.0A`) — the
  capability model is `Permission`/`IPermissionEvaluator` (`ADR-0044`)
  directly, namespaced under `plugin.*`, not a second, parallel
  authorization mechanism invented because the actor is new.

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
- **`TD-09`/`TD-10`/`TD-11` are resolved at the design level** (`WP
  13.0A`) — the mechanism `ADR-0044` deliberately deferred building now
  has a complete, ADR-ratified design: a capability model gating the
  DI/resolve surface (`TD-09`), an ownership check on `Unregister`
  (`TD-10`), and a trust-ordered registration rule replacing "first wins"
  (`TD-11`/`CMD-1`). This is design resolution, not implementation —
  `WP 13.0B` still has to build it; see Trade-offs, below.
- **Zero behavioural change for every actor that exists today.** Every
  first-party module — the only kind that has ever run — is First-Party
  tier by construction, not subject to any capability check; the dynamic
  enforcement calls are skipped entirely, not merely satisfied, when the
  caller is `null`/First-Party. A reader can verify this design changes
  nothing observable about the platform as it runs today.
- **No new runtime dependency, for either the dependency graph or the
  signature.** Dependency resolution is pure computation over
  already-validated manifests; signature verification uses .NET's own
  `System.Security.Cryptography` primitives — consistent with `ADR-0005`'s
  reuse-first mandate, exactly as `Plugin Manifest Architecture.md` itself
  introduced zero new dependency for JSON parsing.

## 10. Trade-offs

- No assembly-unloading support — once loaded, a plugin's assembly stays
  loaded for the process's entire life (consistent with, not worse than,
  ADR-0015's restart policy).
- The plugins root directory and manifest file name are fixed conventions,
  not configurable, in this release — a deliberate, disclosed limitation,
  not an oversight; either can be made configurable later, purely
  additively. **Closed, `WP 13.0A`** — `Runtime:Plugins:RootDirectory`/
  `ManifestFileName`/`Disabled` are now designed (`Plugin Platform
  Architecture.md`, `FCR-0010`/`TD-06`); still awaiting `WP 13.0B`
  implementation.
- **This is not a hard sandbox** (`WP 13.0A`), disclosed plainly, not
  overclaimed. A sufficiently determined plugin could, in principle, use
  reflection in-process to reach past a `RequirePermission` call site.
  This design closes the *ordinary, cooperative* API surface — exactly
  what `TD-09`/`TD-10`/`TD-11`/`CMD-1` describe — not an actively
  adversarial assembly willing to bypass its own declared trust; that
  requires OS-process isolation, explicitly out of scope, with its own
  named revisit trigger (TempestOS being asked to run genuinely
  adversarial, unvetted third-party code).
- **`Plugins:AllowUnsignedLoad` is a single, global switch, not
  per-plugin** (`WP 13.0A`) — an operator enabling it for one legitimately
  unsigned internal tool also permits every other unsigned candidate in
  the Plugins folder to load, under the same clamped ceiling. Accepted for
  v1; a per-plugin allow-list is purely additive if finer granularity is
  ever needed.
- **Architecture only — `WP 13.0A` wrote zero implementation code.**
  `TD-09`/`TD-10`/`TD-11` move from "mechanism exists, not yet applied"
  to "design resolved, retrofit remains open" — not further than that
  until `WP 13.0B` lands. See `WP13.0A-plugin-and-registration-trust-isolation-architecture.md`
  §10 for the full, disclosed statement.

## 11. Common Mistakes

The mistake most worth naming: treating "plugins can fail" as license to
reach reflexively for a critical/non-critical opt-in the moment a plugin
*looks* similar to a background service. The two are shaped differently at
the moment they can fail — a background service is a live, running,
self-assessing component; a plugin manifest is read before any plugin code
has ever executed — and recognising that difference, rather than assuming
similarity implies the same mechanism, is what correctly ruled the opt-in
out (RD-0011).

**Extended, `WP 13.0A`.** Two further mistakes worth naming explicitly:

1. **Assuming `AssemblyLoadContext` is a security boundary because it
   sounds like isolation.** It governs assembly identity and unload in
   modern .NET, not privilege — Code Access Security and AppDomain
   sandboxing were removed from .NET Core entirely. A plugin in its own
   ALC still has the full process's own OS privileges and can still reach
   any public API of any type loaded anywhere in the process. Reaching for
   an ALC to solve `TD-09` would have closed nothing while adding real
   complexity — recognising *why* it does not fit, not merely that an
   alternative exists, is what `Plugin Trust & Isolation Architecture.md`
   spends real space demonstrating rather than asserting.
2. **Assuming this Work Package's own architecture resolves `TD-09`/
   `TD-10`/`TD-11`** because it was the Work Package their own revisit
   trigger named. It designed the resolution, ADR-ratified and ready; it
   built none of it. `WP 13.0B` is where the retrofit actually happens —
   the identical distinction `WP 6.1`'s own retrospective already drew for
   itself, and worth re-drawing here rather than assuming the trigger
   firing and the debt closing are the same event.

## 12. Future Evolution

- **A real, non-synthetic plugin** — every current test proves the pipeline
  against a genuinely loadable, dynamically-built assembly, but no
  hand-authored, shipped example plugin exists yet; `WP 4.3`'s own sample
  module remains available to be packaged this way later (RD-0015), once a
  real need for a shipped example exists. **Still open** — `WP 13.0A` did
  not close this; `src/Plugins/` remains empty, confirmed directly during
  this Work Package's own Version Compatibility analysis (`RD-0009`
  revisit-trigger check).
- ~~**Configurable plugin root/manifest name** — available additively,
  without needing to revisit anything decided here.~~ **Designed, `WP
  13.0A`** — `Runtime:Plugins:RootDirectory`/`ManifestFileName`/`Disabled`
  (`Plugin Platform Architecture.md`, closing `FCR-0010`/`TD-06` at the
  design level). Awaiting `WP 13.0B` implementation.
- ~~**A future diagnostics capability** (`WP 4.8`) should be able to
  surface "which plugins failed, and why" from whatever structure this
  system produces — anticipated by ADR-0025's own Future Considerations,
  not designed here.~~ **Designed, `WP 13.0A`** — `IDiagnosticsProvider.Plugins`
  (extending the already-shipped `WP 5.2`/ADR-0039 service, not a new
  one) is exactly the queryable form ADR-0025 anticipated, built to the
  shape that ADR named without that ADR needing revision. Awaiting `WP
  13.0B` implementation.

**Added, `WP 13.0A`** — every one of these is a named non-goal with its
own explicit revisit trigger, not an oversight:

- **A DI resolution interceptor** checking every individual `GetService`
  call against a plugin's declared capabilities, beyond the
  construction-time reflection check this design already performs.
  Revisit trigger: real evidence, from `WP 13.0B`'s own implementation or
  a real plugin, that constructor-time gating alone is insufficient.
- **Wildcard/glob capability keys** (e.g. `plugin.events.publish:*`) —
  every key names one exact type in v1. Revisit trigger: a real plugin
  needing broad, dynamic event-type coverage makes an exact-key manifest
  genuinely unworkable, not merely verbose.
- **Automated quarantine/disable-on-repeated-violation.** Revisit
  trigger: real evidence of a plugin repeatedly attempting denied
  operations in a way that itself becomes a problem.
- **`AssemblyLoadContext`-based unload/hot-reload** — entirely separate
  from the trust decision; `ADR-0108`'s own reserved `Unloading`/
  `Unloaded` lifecycle seam stays reserved, unused, under this design.
  Revisit trigger: a real, demonstrated need for in-process plugin unload
  or update-without-restart.
- **Online CA/CRL/OCSP revocation checking.** Revisit trigger: `Security
  Roadmap.md` item 7 (API and networking exposure) fires — this platform
  has no network-facing surface today.
- **A runtime-mutable trust store or capability administration UI** —
  mirrors `ADR-0043`'s identical "edit configuration/files directly, no
  administration UI" precedent. Revisit trigger: a genuine multi-user/
  administered deployment scenario.
- **`RD-0009`'s platform-version-ceiling rejection is reaffirmed, not
  reversed**, by this Work Package — a commitment to build toward
  third-party plugins is not the same fact as "real plugins and real
  version history exist." Revisit trigger, now nameable concretely for
  the first time: the first real plugin (first- or third-party) ships,
  and is upgraded at least once.
- **`WP 13.0B`** — the implementation Work Package this entire extended
  §5 anticipates, mirroring exactly how `WP 4.2` once implemented `WP
  4.2A`/`4.2B`/`4.2C`'s own architecture-only groundwork. Until it lands,
  everything in this section's `WP 13.0A` content is a ratified design,
  not running code.

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
4. **Extended, `WP 13.0A`.** "The Manifest describes; the Runtime decides"
   extends cleanly to trust without becoming a different sentence: the
   Manifest now also describes what a plugin wants to do and who signed
   it, and the Runtime still makes every consequential decision — through
   the platform's one existing enforcement point, not a second one.
5. A mechanism that sounds like isolation (`AssemblyLoadContext`) is not
   automatically a security boundary — verify what a candidate mechanism
   actually governs (assembly identity/unload) against what the problem
   actually requires (privilege) before adopting it, rather than reaching
   for the first available API with a plausible-sounding name.
6. Firing a long-disclosed revisit trigger (`TD-09`/`TD-10`/`TD-11`,
   `Security Roadmap.md` items 1/2/10) and actually closing the debt are
   two different events — this Work Package designed the resolution in
   full and ADR-ratified it; `WP 13.0B` is where the retrofit actually
   happens. Stating that distinction plainly, the way `WP 6.1`'s own
   retrospective once did for the mechanism itself, is what keeps a
   Future Evolution section honest rather than quietly overclaiming.
