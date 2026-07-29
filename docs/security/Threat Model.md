# TempestOS Threat Model

## Purpose

This document describes what TempestOS is being built to protect, who or
what it must be protected against, and where the trust boundaries in the
platform currently sit. It is the reference frame `Platform Security
Review v0.5.0.md` was audited against, and the reference frame every
future Work Package's own security review (per the Security Baseline
Statement in that review) should be compared to.

A threat model is not a list of vulnerabilities — it is a description of
*what could go wrong, for whom, and why it would matter*, built before
looking at the code, so that a security review has a yardstick rather
than just an impression. This document is deliberately written to
describe TempestOS as it will eventually need to behave, not only as it
behaves today (2026-07-28, `v0.4.0` released, `v0.5.0` in progress) —
several of the assets and actors below do not exist yet in the running
system, and are marked as such.

## Assumptions Governing This Model

These are the assumptions TempestOS's eventual mission requires this
model to plan around, as given for `WP 5.0S`:

1. TempestOS will eventually manage **engineering intellectual property**
   (CAD, requirements, analysis, verification records).
2. TempestOS will eventually store **commercially sensitive customer
   information**.
3. TempestOS will eventually manage **financial information**.
4. TempestOS will eventually **support multiple users**.
5. TempestOS will eventually **support authentication**.
6. TempestOS will eventually **support licensing**.
7. TempestOS will eventually **support plugins written by third parties**.
8. TempestOS will eventually **support cloud synchronisation**.
9. TempestOS will eventually **expose APIs**.
10. TempestOS may eventually **operate within defence organisations**.

None of items 4–9 exist in the codebase today. Item 1–3's only concrete
trace in the codebase is the bootstrap-era, currently-unreferenced
`ProjectModel` (`src/Tempest.Core/Models/ProjectModel.cs`), which already
carries `Classification`, `SecurityLevel` (defaulting to `"BPSS"` — the
UK Baseline Personnel Security Standard), `ExportControlled`, and
`Customer`/`ContractNumber` fields — a strong, concrete signal of intent,
even though this code is currently dead (see `Platform Security Review
v0.5.0.md`, File System section).

## Assets

What TempestOS protects, or will need to protect, ranked by what would
hurt most if compromised:

| Asset | Exists Today? | Sensitivity |
|---|---|---|
| Engineering project data (requirements, CAD, analysis, verification, deliverables) | No — modelled only in dead code (`ProjectModel`) | Potentially export-controlled / classified |
| Customer/commercial information (`Customer`, `ContractNumber`) | No — dead code only | Commercially sensitive |
| Financial information | No | Sensitive |
| Plugin/module code running inside the host process | Yes | High — full process trust today |
| Platform configuration (`IConfigurationProvider` values) | Yes | Low today (no secrets exist); will rise once credentials/connection strings are configured |
| Log output (console today) | Yes | Low today; may carry stack traces, file paths |
| The Host's own runtime state (module/hosted-service lifecycle) | Yes | Availability-relevant, not confidentiality-relevant |
| User identity / session | No | N/A yet — no authentication exists |
| API surface | No | N/A yet — no network-facing API exists |

## Actors

| Actor | Trust Level Today | Notes |
|---|---|---|
| The person running the process | Fully trusted | Single-user, local process; whoever can run `Tempest.App.exe` already has whatever access the OS account has |
| A first-party module (`Tempest.Samples`, or a future in-tree module) | Fully trusted | Runs in-process, full DI container visibility once discovered |
| A plugin (`src/Tempest.Core/Plugins/*`) | **Fully trusted, in practice** | ADR-0025/ADR-0026 isolate a plugin's *discovery/loading* failures, but a plugin whose assembly loads successfully is indistinguishable, from that point on, from a first-party module — see Threat Scenario 1, below |
| A future third-party plugin author | Not yet a real actor | The threat model this platform must eventually defend against; today's plugin infrastructure was built assuming today's actor (a trusted, in-house module), not this one |
| A future authenticated end user | Does not exist | No authentication exists; "the user" and "the process" are currently the same trust domain |
| A future second, less-trusted user on a shared/multi-user deployment | Does not exist | No concept of "another user's data" exists to be protected from |
| A future network caller (API/cloud sync) | Does not exist | No network-facing surface exists at all today |

## Trust Boundaries

TempestOS's four-layer platform model (Modules → Platform APIs → Platform
Services → Runtime Host, ADR-0023) is a **layering** boundary, not a
**trust** boundary — every layer runs in the same process, under the same
OS identity, with no privilege separation between them. The one trust
boundary the codebase has deliberately, explicitly drawn is:

> **Discovery, Registration, and Lifecycle machinery is never reachable
> through the dependency injection container** (ADR-0017, reaffirmed by
> `ITempestHost.Services`'s design, ADR-0034). A module or plugin can
> resolve any DI-*public* platform service, but can never obtain a
> reference to `IRuntimeModuleManager`, `IModuleLifecycleManager`, or
> `IFrameworkDiscoveryService` — the machinery that orchestrates it has no
> path back to modules being orchestrated.

This boundary is real, verified (WP 5.0D added a regression test proving
it), and worth being proud of. It is also the *only* internal trust
boundary that exists. Everything else — every module, every hosted
service, and (critically) every loaded plugin — shares one undivided
trust domain: the process itself. See `Platform Security Review
v0.5.0.md`'s Architecture and Plugin Infrastructure sections for the full
implications.

## Threat Scenarios

Each scenario below is written against a specific asset/actor pair from
the tables above, and is cross-referenced to the `Platform Security
Review v0.5.0.md` finding that examines it, where one exists.

### 1. A malicious or compromised plugin runs with full platform trust

**Actor:** a future third-party plugin author (or a legitimate plugin
whose supply chain is compromised). **Asset:** every other asset in the
process — the plugin can read or write anything the host process can.

Today, `PluginAssemblyLoader.LoadPlugins` loads a plugin's declared
assembly via `Assembly.LoadFrom` into the default `AppDomain`.
`ReflectionFrameworkDiscoveryService`'s default constructor scans
`AppDomain.CurrentDomain.GetAssemblies()` — which, because plugin loading
runs *before* module discovery in `TempestHost.ExecuteStartupPhasesAsync`,
already includes the plugin's own assembly. Any `IModule` the plugin
assembly declares is discovered, registered, and given constructor-
injected access to every DI-public platform service (`IEventBus`,
`INavigationProvider`, `IConfigurationProvider`, `ILogger`, and every
other first-party module's own concrete type), identically to a
first-party module. Nothing distinguishes "a plugin's module" from "a
first-party module" once the assembly has loaded successfully.

For today's actor (an in-house, trusted module built by the same team),
this is fine — arguably correct, even, since ADR-0023's four layers were
never meant to be a security boundary between first-party components.
For assumption 7 (third-party plugin authors), this is the platform's
single largest piece of future security debt: there is no capability
model, no code signing, no manifest-declared permission scope, and no
process/AppDomain isolation between "a plugin" and "the platform itself."
See `Platform Security Review v0.5.0.md`, Plugin Infrastructure section,
and `Security Roadmap.md`'s first entry.

### 2. A plugin manifest declares an assembly path outside its own folder

**Actor:** a plugin manifest author (malicious or careless). **Asset:**
which assembly gets loaded from disk.

`PluginManifestDiscoveryService` resolves a manifest's declared
`AssemblyFileName` via `Path.GetFullPath(Path.Combine(folder,
dto.AssemblyFileName))`. Before this Work Package, an absolute path or a
`../` escape in `AssemblyFileName` would resolve outside the plugin's own
candidate folder — `Path.Combine` discards its first argument entirely
when the second is rooted. Given Scenario 1 (plugins are already fully
trusted once loaded), this was not a privilege-escalation path, but it
did mean the manifest's own declared scope ("this plugin's files live in
its own folder") was unenforced. **This Work Package closed this gap** —
see `Platform Security Review v0.5.0.md`, Finding PL-1.

### 3. A misbehaving module disrupts another module's navigation entry

**Actor:** any first-party module or plugin holding an
`INavigationProvider` reference. **Asset:** another module's registered
`NavigationItem`.

`NavigationService.Unregister(string id)` takes a bare ID string with no
ownership check — any caller that knows (or guesses) another module's
navigation ID can remove it. Low impact today (single-user, all-trusted
modules); becomes real once assumption 7 (third-party plugins) arrives
and two independently-authored components share one `INavigationProvider`.
See `Platform Security Review v0.5.0.md`, Finding NAV-1.

### 4. Sensitive project data is stored unauthenticated and unencrypted

**Actor:** anyone with filesystem access to the process's data directory.
**Asset:** engineering IP, customer data, export-controlled metadata
(assumptions 1–3).

`JsonProjectRepository`/`ProjectModel` (bootstrap-era, currently dead
code, unreferenced since `WP 5.0D`) already model exactly this data —
plain JSON, no encryption at rest, no access control, no audit trail.
Because this code is not wired into the active pipeline today, there is
no live exposure. But it is the clearest concrete evidence in the
repository of where assumptions 1–3 are headed, and it must not be
revived as-is once that happens. See `Platform Security Review v0.5.0.md`,
File System section, and `Security Roadmap.md`.

### 5. A future authenticated, multi-user, or networked TempestOS has no scope to build on

**Actor:** N/A — this is an absence, not an active threat. **Asset:**
every future capability in assumptions 4–9.

The DI container has exactly two lifetimes (Singleton, Transient) and no
concept of a per-user or per-request scope; `EventBus`, `NavigationService`,
and every platform service are process-wide singletons with no tenant
isolation. There is no authentication concept, no session concept, no
network-facing surface, and no secrets-handling convention anywhere in
the logging or configuration frameworks. None of this is a vulnerability
in the code that exists today — there is nothing yet to authenticate,
scope, or protect over a network. It is, however, exactly the kind of
"architectural decision likely to become a future security risk" this
Work Package's brief asked to have identified. See `Security
Roadmap.md` for the full list, sequenced against the assumption that
would first require each one.

## What This Model Deliberately Does Not Cover

This model does not attempt to threat-model capabilities that do not
exist yet in any form (authentication schemes, licensing schemes, API
authorization models, cloud sync protocols) — doing so before a single
design decision has been made about *how* any of them will work would be
speculation, not threat modelling. Each is instead named, at the level of
"this will need a threat model of its own before it is built," in
`Security Roadmap.md`.

## Related Documents

`Platform Security Review v0.5.0.md` (the audit this model frames);
`Security Principles.md` (the standing principles the review judged the
codebase against); `Security Roadmap.md` (prioritised future work); `docs/architecture/Platform Service Map.md`; ADR-0017, ADR-0023, ADR-0025,
ADR-0026, ADR-0034.
