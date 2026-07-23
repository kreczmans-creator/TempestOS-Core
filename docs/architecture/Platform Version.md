# Platform Version

**Status: implemented — WP 4.2A (`Tempest.Core.Versioning`).**

## Overview

TempestOS did not, until this work package, have any way to answer "what
version of the platform is actually running" from inside a running
process. No `<Version>` was set in any project, so every compiled assembly
carried the .NET SDK's own default (`1.0.0.0`), completely disconnected
from the repository's real `VERSION` file. This was found — not
introduced — during WP 4.2's Plugin Manifest design pass: a
`MinimumPlatformVersion` compatibility check is meaningless without
something authoritative to compare it against.

This document describes the fix: a single, authoritative, build-derived
platform version, queryable from anywhere in the application via ordinary
constructor injection.

## Where Version Information Originates

The repository's root `VERSION` file remains the single source of truth —
the same file `scripts/New-Release.ps1` already validates against at
release time (Engineering Governance §7). `Directory.Build.props` now
reads that file once, at build time, into the MSBuild `$(Version)`
property:

```xml
<PropertyGroup Condition="Exists('$(TempestOSVersionFilePath)')">
  <Version>$([System.IO.File]::ReadAllText('$(TempestOSVersionFilePath)').Trim())</Version>
</PropertyGroup>
```

Because this lives in `Directory.Build.props`, every project in the
solution inherits the same `<Version>` automatically — no project sets it
individually, and no version number is ever hand-typed as a duplicated
constant anywhere. The .NET SDK derives `AssemblyVersion`,
`AssemblyFileVersion`, and `AssemblyInformationalVersion` from `<Version>`
on its own; nothing else needed to be configured.

**If the `VERSION` file is ever absent**, the `Condition="Exists(...)"`
guard means `<Version>` is simply left unset and the SDK falls back to its
own default, rather than failing the build. `PlatformVersionProvider`'s own
missing-metadata fallback (below) then reports that absence honestly rather
than throwing.

## How It Is Exposed

Two types, in a new `Tempest.Core.Versioning` namespace (a new capability
namespace, per ADR-0024's established packaging convention):

- **`IPlatformVersionProvider`** — the Platform API (ADR-0023): one
  property, `Version`, of type `PlatformVersion`.
- **`PlatformVersionProvider`** — the Platform Service implementing it.
  Resolves `Version` exactly once, in its constructor, from the executing
  assembly's own build metadata — cached implicitly, since the type never
  mutates it afterward.
- **`PlatformVersion`** — an immutable value carrying `SemanticVersion`
  (string), `AssemblyVersion` (`System.Version`), and `InformationalVersion`
  (string, nullable).

## Lifetime, Ownership, and Dependency Direction

- **Lifetime**: effectively a singleton for the life of the running
  process — resolved once, during the Host's existing Platform Services
  Registered phase, and registered via `AddInstance` (ADR-0009's
  Composition Root pattern), exactly like Configuration and Logging.
- **Ownership**: `TempestHost` constructs it directly, alongside
  Configuration and Logging, and logs the resolved version immediately
  ("Platform version resolved: …") — the same discoverability every other
  startup phase already has.
- **Dependency direction**: `PlatformVersionProvider` depends on nothing —
  not Configuration, not any other platform service — deliberately, so
  that any current or future platform service may depend on it (per
  ADR-0023's downward-only layering), and it can never depend on anything
  "above" it. Its only optional input is `ILogger?` (defaulting to
  `null`), matching the same optional-diagnostics convention every other
  platform service already follows, including Configuration itself — this
  does not create a dependency in the layering sense, since it is never
  required for correct operation.

## Why No New Abstraction Was Needed

`PlatformVersionProvider`'s construction requires nothing external — no
configuration, no composition-root-only input. Unlike Configuration
(which genuinely cannot be built by ordinary reflection-based DI, per
ADR-0009), `PlatformVersionProvider` *could* have been registered as an
ordinary container-constructed singleton. It is registered via
`AddInstance` anyway, specifically so the Host can resolve and log the
version eagerly, during startup, rather than lazily on first use — the
same reasoning that makes the version "always resolved, every run" rather
than "resolved if something happens to ask."

No `IPlatformVersionSource` abstraction, no builder, no factory — a
concrete class computing a value once in its constructor was sufficient,
consistent with this release's own repeated discipline (WP 4.0, WP 4.1) of
not introducing an abstraction with no second implementation in view.

## Missing Metadata Behaviour

| Condition | `AssemblyVersion` | `InformationalVersion` | `SemanticVersion` |
|---|---|---|---|
| Normal (this repository's own build) | `0.3.0.0` *(current `VERSION`)* | `"0.3.0"` | `"0.3.0"` |
| No `AssemblyInformationalVersionAttribute` | Whatever the assembly has | `null` | Derived from `AssemblyVersion`'s `Major.Minor.Build` |
| No version metadata at all | `0.0.0.0` (fallback) | `null` | `"0.0.0"` |

A platform that cannot determine its own version reports that honestly
(via the fallback values above) rather than throwing during startup —
consistent with Fail Fast's own spirit applied to a genuinely
non-fatal diagnostic gap, not a startup precondition.

## Relationship to the Plugin Manifest's Blocking Prerequisite

`Plugin Manifest Architecture.md` named "TempestOS has no queryable
platform version at runtime" as a blocking prerequisite before Plugin
Manifest implementation could begin. This document, and the
implementation it describes, resolves that prerequisite:
`IPlatformVersionProvider.Version.AssemblyVersion` (a comparable
`System.Version`) is now exactly what a future `MinimumPlatformVersion`
check would compare a plugin manifest's declared minimum against. Plugin
Manifest implementation still requires one further decision before it may
begin — `Host Lifecycle.md` phase-table placement for the new Plugin
Discovery/Loading steps. (Its other required decision, plugin failure
classification, is resolved — see ADR-0025.) This work package removed one
blocker; it was never responsible for either of the other two.
