# WP 4.2A — Runtime Platform Version Infrastructure

## 1. Introduction

WP 4.2A implements the single missing piece WP 4.2's design pass found but
could not fix within its own scope: TempestOS had no way to answer "what
version of the platform is actually running" from inside a running
process. This work package closes that gap directly, ahead of WP 4.2
(Plugin Manifest) implementation, which depends on it.

## 2. Purpose

To give every current and future platform service a single, authoritative,
build-derived platform version, queryable via ordinary constructor
injection — and, specifically, to unblock the one prerequisite `Plugin
Manifest Architecture.md` named explicitly: a `MinimumPlatformVersion`
check needs something real to compare against.

## 3. Background

`Plugin Manifest Architecture.md`'s Versioning Strategy section found, while
designing how a plugin declares compatibility, that no project in the
solution set `<Version>`, `<AssemblyVersion>`, `<FileVersion>`, or
`<InformationalVersion>` — every compiled assembly carried the .NET SDK's
own default (`1.0.0.0`), completely disconnected from the repository's real
`VERSION` file. That retrospective named this a blocking prerequisite and
recommended it be resolved as its own, separate, Runtime-Foundation-level
decision — not folded silently into Plugin Manifest implementation. This
work package is that decision.

## 4. The Problem

1. **Where should the platform's version actually come from** — a
   hand-typed constant, or something derived from the same file the
   release process already treats as authoritative?
2. **How should it be exposed** so that any platform service, present or
   future, can query it without depending on anything "above" it?
3. **Does any project already specify version metadata** that this work
   package would be duplicating? (Investigated directly — see Section 5.)
4. **What happens if the metadata is genuinely missing** — should
   resolution throw, or degrade gracefully?

## 5. The Design

**Repository investigation, first.** No project (`Tempest.Core.csproj`,
`Tempest.App.csproj`, `Tempest.Core.Tests.csproj`) or `Directory.Build.props`
set any version property before this work package. Confirmed directly by
inspecting each file, not assumed.

**Origin.** `Directory.Build.props` now reads the repository's root
`VERSION` file once, at build time, into MSBuild's `$(Version)` property,
guarded by `Condition="Exists(...)"` so a missing file leaves `<Version>`
unset (SDK default) rather than failing the build. No version number is
hand-typed anywhere; the existing `VERSION` file (Engineering Governance
§7's own release artefact) is the only source. The .NET SDK derives
`AssemblyVersion`/`AssemblyFileVersion`/`AssemblyInformationalVersion`
automatically from `<Version>` — no further project configuration needed.

**Exposure.** Three new types in `Tempest.Core.Versioning` (a new
capability namespace, per ADR-0024's packaging convention):
`IPlatformVersionProvider` (the Platform API), `PlatformVersionProvider`
(the Platform Service, resolving `Version` once in its constructor from
the executing assembly's own metadata), and `PlatformVersion` (an
immutable value: `SemanticVersion`, `AssemblyVersion`,
`InformationalVersion`).

**Lifetime and ownership.** `TempestHost` constructs
`PlatformVersionProvider` directly, alongside Configuration and Logging,
during the already-existing Platform Services Registered phase, and
registers it via `AddInstance` (ADR-0009) — no new `Host Lifecycle.md`
phase, no phase-table change, unlike Plugin Manifest's own anticipated
need for one.

**Dependency direction.** `PlatformVersionProvider` depends on nothing —
not Configuration, not any other platform service — so that ADR-0023's
downward-only layering holds: any service may depend on it; it can never
depend on them.

## 6. Alternatives Considered

**Registering `PlatformVersionProvider` as an ordinary, container-constructed
singleton** rather than via `AddInstance`. Genuinely possible — its
constructor needs nothing external. Rejected in favour of `AddInstance`
specifically so the Host resolves and logs the version eagerly, every
run, matching Configuration and Logging's own "always happens, every
startup" guarantee, rather than a lazy resolution that might never occur
if nothing happens to ask.

**An `IPlatformVersionSource` abstraction**, generalising where version
data could come from. Not proposed — no second source is in view, and the
same reasoning already recorded as RD-0008 (rejecting an
`IPluginManifestSource` abstraction for an identical reason) applies here
without needing a new entry.

**Throwing when version metadata is absent.** Rejected — a platform unable
to determine its own version is a diagnostic fact worth reporting (via the
documented fallback chain), not a startup-blocking failure. Consistent
with Fail Fast's own spirit applied where it actually helps, not
mechanically everywhere.

## 7. Why This Solution Was Chosen

Every decision traces back to one of two things already established
elsewhere: ADR-0009's Composition Root pattern (reused a third time,
exactly as its own Future Considerations anticipated) and ADR-0023's
layering rule (applied here in its purest form yet — a service with
literally zero dependencies, that everything else may depend on). Nothing
in this work package required inventing a new pattern.

## 8. Architectural Principles

- **Platform Layering** (ADR-0023) — the clearest possible instance in the
  platform so far: zero dependencies in, unlimited dependents out.
- **Composition Root** (ADR-0009) — reused, not re-derived.
- **Fail Fast, applied proportionately** — missing metadata degrades
  gracefully rather than throwing, because the failure mode is genuinely
  informational, not a broken precondition.
- **Reuse Before Invention** — the internal `Assembly`-accepting
  constructor mirrors `ReflectionFrameworkDiscoveryService`'s own
  established test-seam pattern exactly.

## 9. Benefits

- Every current and future platform service can now answer "what version
  of TempestOS am I running inside" without any duplicated constant to
  keep in sync.
- The Plugin Manifest's one named blocking prerequisite is resolved —
  `MinimumPlatformVersion` now has something real to compare against.
- Proven, not merely asserted: `PlatformVersionDependencyInjectionTests`
  demonstrates a plain service resolving `IPlatformVersionProvider` via
  constructor injection, exactly the same proof
  `ConfigurationDependencyInjectionTests` already established for
  `IConfigurationProvider`.

## 10. Trade-offs

- `PlatformVersionProvider`'s eager, Host-constructed lifetime means its
  resolution always happens during startup, even for a run where nothing
  ever asks for the version — a small, fixed cost, accepted for the same
  reason Configuration and Logging accept it: guaranteed availability
  matters more than avoiding one cheap, side-effect-free reflection call.
- `SemanticVersion`'s fallback format (`Major.Minor.Build` from
  `AssemblyVersion`) is a reasonable default, not a validated SemVer
  parser — sufficient for this platform's own versioning scheme (always
  three-part, no prerelease tags in use), not a general-purpose SemVer
  library.

## 11. Common Mistakes

The mistake most worth naming here is one avoided: treating "derive from
build metadata" as license to also embed a git commit hash or other build
provenance into `InformationalVersion` speculatively, since that capability
exists elsewhere in the .NET ecosystem (SourceLink-style deterministic
builds). This work package did not add it — no current consumer asked for
build provenance, only a comparable version number — consistent with this
release's now well-established discipline of building only what today's
understanding justifies.

## 12. Future Evolution

- **Plugin Manifest (WP 4.2)** is now unblocked with respect to this one
  prerequisite; its own two required ADRs remain outstanding and are not
  addressed by this work package.
- **A future `MinimumPlatformVersion` comparison** will consume
  `IPlatformVersionProvider.Version.AssemblyVersion` directly — no further
  version-infrastructure work is anticipated to support it.
- **Build provenance** (git hash, build date) could be added to
  `InformationalVersion` later, purely additively, if a real consumer
  ever needs it — not anticipated now.

## 13. Key Takeaways

1. A gap found during an architecture-only work package (WP 4.2) does not
   have to be fixed *inside* that work package's own scope — naming it
   precisely and handing it to its own, focused follow-up (this one) kept
   both pieces of work clean and independently reviewable.
2. Not every new platform service needs Composition Root treatment just
   because Configuration and Logging both use it — `PlatformVersionProvider`
   could have been container-constructed; `AddInstance` was chosen for a
   specific, stated reason (eager resolution), not by default imitation.
3. The simplest possible dependency graph — zero in, unlimited out — is
   also the clearest possible demonstration of ADR-0023's layering rule in
   practice.

---

## Architectural Debt Assessment

**No new debt introduced.** The gap this work package closes was existing,
newly-discovered debt from before WP 4.0; it is now resolved, not carried
forward. No other debt item on record changes as a result of this work
package.

## Observations

- **Files changed**: `Directory.Build.props` (modified); 3 new production
  files (`PlatformVersion.cs`, `IPlatformVersionProvider.cs`,
  `PlatformVersionProvider.cs`); `TempestHost.cs` (modified — one new
  registration, no phase-table change); 3 new test files (17 new tests);
  `TempestHostTests.cs` (modified — one new assertion); this retrospective;
  `Platform Version.md`; Platform Service Map updates.
- **Tests added**: 17 — successful retrieval (real assembly), preferring
  `InformationalVersion` when present, three missing-metadata fallback
  scenarios (no informational version; no version metadata at all; a
  blank informational version treated as absent), constructor validation
  (both `PlatformVersionProvider` and `PlatformVersion`), caching (same
  instance returned on repeated access; two independent providers over the
  same assembly resolve equal but distinct values), thread safety (64
  concurrent reads observe one consistent value), and DI resolution (both
  direct and via constructor injection into a consuming service).
- **Test results**: 215 of 215 passing (198 pre-existing + 17 new), 0
  failures, verified stable across five consecutive full-suite runs.
- **Build results**: 0 warnings, 0 errors. Compiled `Tempest.Core.dll`
  verified directly to carry `AssemblyVersion 0.3.0.0`, matching the
  repository's own `VERSION` file exactly.
- **Remaining blockers before WP 4.2 (Plugin Manifest) implementation**:
  at the time of this work package, the two ADRs `Plugin Manifest
  Architecture.md` already named (plugin failure classification; `Host
  Lifecycle.md` phase-table placement) — unaffected by this work package.
  **Update, WP 4.2B**: plugin failure classification is now resolved
  (ADR-0025). **Update, WP 4.2C**: phase-table placement is now also
  resolved (ADR-0026). No blocker remains.
- **Readiness assessment**: WP 4.2A is complete. The platform-version
  prerequisite is fully resolved. Both remaining ADRs (ADR-0025, ADR-0026)
  were subsequently resolved, and **Plugin Manifest implementation is now
  complete (WP 4.2)** — including ADR-0026's own move of this work
  package's `PlatformVersionProvider` construction to immediately follow
  Logging Built (its DI registration unchanged from this work package's
  own placement). See the WP 4.2 implementation retrospective.
