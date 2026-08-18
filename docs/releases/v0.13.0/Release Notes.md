# TempestOS v0.13.0 — "Trust & Deployment Hardening"

## 1. Executive Summary

`v0.13.0` builds TempestOS's plugin platform and its trust boundary. It
is a platform-capability release: no new Engineering Discipline, and no
user-visible feature. Its scope is the thing `FCR-0001` has named since
`v0.7.0` and `Security Roadmap.md` items 1, 2, and 10 have named since
`v0.6.0` — a plugin can now be discovered, dependency-ordered, signed,
trust-tiered, capability-gated, and lifecycle-run, with enforcement that
holds at every point a plugin's code can reach the platform.

Six ADRs (`ADR-0107`–`ADR-0112`) define it: dependency-graph resolution
and extended failure classification; a load/upgrade/uninstall lifecycle
with live in-process unload retained as a named non-goal; plugin service
registration that adds no new DI capability; **capability-scoped
isolation rather than `AssemblyLoadContext` or process separation**; a
trust-capability model extending `IPermissionEvaluator` via a component
principal and trust-ordered registration; and a detached
manifest-and-assembly hash signature verified at Plugin Discovery.

The release's centre of gravity is not the initial build but the four
remediation chains that followed it. `WP 13.9.0`'s readiness review
returned **NOT READY** and demonstrated, against the compiled binary,
that trust enforcement scanned only the one manifest-declared assembly.
Eleven Work Packages then closed that and three further trust-boundary
defects — each found by adversarial review, each fixed, each
independently re-verified, several by reverting the fix and confirming
the original failure returned.

**No third-party plugin ships in this release, and third-party plugin
support is neither enabled nor advertised.** `src/Plugins/` contains only
a `README.md`. See §5.

## 2. Major Capabilities Added, by Work Package

- **`WP 13.0A` — Plugin & Registration Trust Isolation Architecture.**
  Two composed architecture documents and `ADR-0107`–`ADR-0112`: a
  four-tier trust model, a capability model extending `ADR-0044`'s
  `IPermissionEvaluator` via a new `AsyncLocal<T>`-backed
  `ICurrentComponentAccessor`, a detached signature verified entirely at
  Plugin Discovery, and the central isolation decision — capability-scoped
  in-process enforcement, on the explicit technical grounds that an
  `AssemblyLoadContext` is not a security boundary in modern .NET and
  that process separation is disproportionate to the disclosed threat
  (vetted, signed, commercial plugins; not an open marketplace).
  Nineteen Rejected Designs recorded (`RD-0046`–`RD-0064`).

- **`WP 13.1A` — Plugin Runtime & Composition Root Implementation.** The
  mechanical half: manifest v2, Kahn topological dependency resolution
  inside the existing Phase 3.1, a Host-owned `IPluginRegistry` projected
  read-only through `IDiagnosticsProvider.Plugins`, and a configurable
  plugins root (closing `TD-06`/`FCR-0010`).

- **`WP 13.2A` — Plugin Trust & Capability Enforcement Implementation.**
  The trust half: real trust-tier assignment and detached SHA-256/RSA-PSS
  signature verification at Discovery; real capability enforcement at
  Loading (`PluginAssemblyLoader.EnforceTrust`); and trust-ordered
  registration across `NavigationService`, `CommandRegistry`,
  `CommandHandlerTable`, and `IEventBus`. **Closes `TD-09`, `TD-10`,
  `TD-11`.**

- **`WP 13.3A` — Plugin Platform Integration & End-to-End Validation.**
  Independent re-verification of all six ADRs against real code; a
  `TopologicalSort` O(n²) → reverse-adjacency-index optimisation.

- **`WP 13.9.3` — Multi-Assembly Trust-Boundary Remediation.** Closes the
  gap `WP 13.9.0` demonstrated: constructor-parameter `ParameterType`
  resolution is now forced inside the scan's own before/after `AppDomain`
  diff window, so an assembly reachable only through a constructor
  parameter can no longer enter the process un-vetted.

- **`WP 13.9.4` — Trust-Denial Execution Boundary Remediation.** Makes
  denial an execution boundary rather than a bookkeeping outcome: a
  Host-owned denied-type registry plus filters at Module Registration and
  Hosted Service Registration. Broadened mid-Work-Package, by its own
  adversarial review, to cover `IHostedService` as well as `IModule`.

- **`WP 13.9.6` — Module Discovery Trust Boundary Remediation.** Closes
  the residual: a denied plugin's unattributed module was still being
  *constructed* during Discovery, before either registration filter was
  consulted. Fixed with one optional, generic `Func<Type, bool>`
  predicate — `Tempest.Core.Modules` gains no reference to
  `Tempest.Core.Plugins`.

- **`WP 13.10B` — Plugin Trust Hardening Implementation.** Closes
  `TD-51`/`TD-52`: constructor conformance and the never-eligible
  denylist now run against hosted-service types too, and
  `HostedServiceManager` gained the component-scope hook
  `ModuleLifecycleManager` already had.

- **`WP 13.11B` — TD-51 Trust-Denial Crash Remediation.** Closes the
  reopened `TD-51`, where an unresolvable constructor-parameter type
  crashed the entire Host. The denial is recorded before it is thrown,
  and the fixed-point scan is allowed to **complete** rather than abort —
  deliberately declining the partial-list shape that would have traded a
  Host crash for a silent trust bypass.

- **`WP 13.11C` — TD-51 Remediation Review & Trust-Boundary Verification.**
  Found that the completed-scan decision had no regression coverage at
  all, and closed that gap with a test proven non-vacuous in both
  directions.

- **`WP 13.11D` — Plugin Platform Exit Review.** Six disciplines,
  read-only, declaring the platform ready for WP14 UI/UX. Raised `TD-56`.

Seventeen further Work Packages (`WP 13.0.0`, `13.0.0A`, `13.0B`, `13.1B`,
`13.2B`, `13.3B`, `13.9.0`, `13.9.1`, `13.9.2`, `13.9.5`, `13.9.7`,
`13.10A`, `13.10C`, `13.11A`, `13.12.0`, `13.12.1`, `13.12.2`) were
branch establishment, review, remediation-of-review-findings,
integration, or governance closure, and add no capability of their own.
Every one has an Academy retrospective.

## 3. Testing Summary

| Configuration | Tempest.Core.Tests | Tempest.Desktop.Tests | Combined | Failed | Skipped |
|---|---|---|---|---|---|
| Debug | 2,341 | 221 | **2,562** | 0 | 0 |
| Release | 2,341 | 221 | **2,562** | 0 | 0 |

Both builds report **0 Warnings / 0 Errors** with
`-p:TreatWarningsAsErrors=true`, the bar CI itself applies.

`v0.12.0` closed at 2,255. The release added 307 tests net, across 57
test files (+11,465 lines), with **zero** tests deleted, skipped,
ignored, or excluded by filter — independently verified across
`v0.12.0..HEAD`.

One disclosed caveat: `TD-34` records an intermittent, parallelism-only
failure caused by a process-global `Console` redirect race. It surfaced
once during this release (`2,561/2,562`), with an immediate re-run of
identical binaries returning clean. It is a test-harness isolation issue;
no product behaviour is implicated. See §4.

## 4. Known Technical Debt

The Technical Debt Register moved from 48 to **56 tracked items — 18
Resolved, 1 Partially resolved, 37 Open.**

Eight items were added or resolved by this release:

- **`TD-51`** — trust checks applied only to the plugin's own declared
  assembly, later found to crash the Host on the `IModule` axis. Added
  `WP 13.10A`; resolved `WP 13.10B`; **reopened `WP 13.11A`**;
  **Resolved, `WP 13.11B`**, independently re-verified `WP 13.11C`.
- **`TD-52`** — `EstablishCurrentPrincipal` reachable without the
  `plugin.identity.establish` capability. **Resolved, `WP 13.10B`.**
- **`TD-49`** — TOCTOU window between Discovery-time signature
  verification and `Assembly.LoadFrom`. Open; requires filesystem write
  access to exploit.
- **`TD-50`** — the first-party publisher certificate is identified by
  filename convention, not a certificate attribute. Open.
- **`TD-53`** — `HostedServiceManager` misclassifies a construction-time
  failure as non-critical. Open; the failure is still isolated.
- **`TD-54`** — `ITempestServiceProvider`'s DI-escape closure is
  incidental rather than an explicit denylist entry. Open, latent.
- **`TD-55`** — the denied-type registry is keyed on `Type` identity
  alone, so one plugin's denial can suppress another assembly's module
  types, and an already-resident assembly is never attributed. Open;
  fail-closed in its exploitable half.
- **`TD-56`** — a plugin's **constructor** executes outside its component
  scope, so plugin code runs during construction with a `null` ambient
  principal that every capability gate treats as first-party. Open —
  **Disclosed, Non-Blocking for `v0.13.0`; mandatory precondition to
  enabling third-party plugin support.** Dormant as shipped: `src/Plugins/`
  is empty, no `TrustedPublishers/` certificate is committed, and the
  `UnsignedLocal` ceiling admits no `plugin.services.resolve:*` grant, so
  the path is unreachable below `VerifiedSigned`.

## 5. Deferred / Open Findings

- **No plugin can contribute renderable UI.** `NavigationItem` carries no
  rendering concern by explicit design, and the real UI seams
  (`IWorkspaceViewFactory`, `IWorkspacePanel`, `IWorkspaceView`) live in
  `Tempest.App`, which plugin projects structurally cannot reference. A
  plugin can contribute Navigation items and Commands — both
  capability-gated, both proven end-to-end against real signed plugin
  assemblies. Assessed by `WP 13.11D` as WP14's own design scope, not a
  prerequisite gap.
- **No trust-tier, granted-capability, or ownership projection** exists on
  any DI-public diagnostics surface — the data a future plugin-management
  UI would need. Additive to extend.
- **`FCR-0002` (Third-Party Plugin Ecosystem Enablement) is not started.**
  No real plugin exists; `Plugin Register.md` remains "Not Yet
  Applicable". Every trust decision in this release is validated against
  synthetic fixtures only.
- **`TD-56` must be closed before third-party plugin support is enabled.**
- **Both REST-API Work Packages predicted by the roadmap
  (Authentication & TLS Architecture, and Implementation) remain
  uncommissioned and unnumbered** — their predicted labels `WP 13.1A`/
  `WP 13.1B` were reassigned to plugin work, disclosed in
  `WorkPackages.md`, not silently dropped.
- **`ADR-0110`'s Consequences bullet and `Plugin Platform Architecture.md`'s
  "Unchanged" claims** were factually stale from `WP 13.9.6` until
  corrected by `WP 13.12.2`. Both are struck and annotated; the decisions
  themselves were never contradicted.
- **Pre-existing, deliberately not closed by this release:**
  `Feature Register.md` and `Traceability Matrix.md` remain stale since
  `WP 5.3`; the Academy Register's `03 Work Packages` table carries fewer
  rows than files; `TD-42`, `TD-43`, `TD-45` remain Open; `v0.9.0` and
  `v0.10.0` still lack `WorkPackages.md`.

## 6. Statistics

- **Work Packages:** 28 delivered (`WP 13.0.0` → `WP 13.12.2`).
- **Commits:** 13 on `feature/v0.13.0`, linear, zero merge commits,
  cut directly from the `v0.12.0` tag (`13a6ce3`).
- **ADR count: 105 → 111** (`ADR-0107`–`ADR-0112`). `ADR-0111` amended
  five times in-file across the remediation chain; `ADR-0110` corrected
  once by `WP 13.12.2`.
- **Rejected Designs:** `RD-0046`–`RD-0065` (20 new).
- **Technical Debt: 48 → 56 tracked** (18 Resolved, 1 Partially, 37 Open).
- **Tests: 2,255 → 2,562**, both configurations (net +307).
- **Academy: 104 → 132 retrospectives**; 169 → 197 articles.
- **Production surface:** 45 `src/` files changed, all in
  `Tempest.Core`. **Zero** `.csproj`, `Directory.Build.props`, or
  lock-file changes; `TargetFramework` unchanged at `net10.0`; nothing
  packaged for external consumption.

## 7. Final Engineering Assessment

Debug and Release builds: **0 Warnings / 0 Errors**. Full regression:
**2,562/2,562, both configurations**. `governance-healthcheck.ps1`: **7
passed, 1 warned (pre-existing), 0 failed**. Branch: 13 commits, linear
descendant of `v0.12.0`, zero merges. Every one of the 28 delivered Work
Packages has exactly one Academy retrospective.

The authoritative readiness record for this release is
`WP13.12.2 Engineering Release Report.md`, which supersedes
`WP13.9.0 Engineering Release Report.md` and its now-historical
**NOT READY** verdict.

**`VERSION` remains `0.12.0`.** No tag has been created, no branch has
been pushed or merged, and no GitHub Release exists. The `Build & Test`
and `CI Gate` jobs have never run on `main` at a pre-tag commit — a
mandatory verification under `Engineering Readiness Review Architecture.md`
§2.3 that is structurally unsatisfiable from a feature branch, and the
single remaining blocker to release.

## Related Documents

`docs/releases/v0.13.0/WP13.12.2 Engineering Release Report.md`
(authoritative readiness record);
`docs/releases/v0.13.0/WP13.9.0 Engineering Release Report.md`
(superseded); `docs/releases/v0.13.0/WorkPackages.md`;
`docs/architecture/Plugin Platform Architecture.md`;
`docs/security/Plugin Trust & Isolation Architecture.md`;
`ADR-0107`–`ADR-0112`;
`docs/governance/Quality/Technical Debt Register.md`;
`docs/governance/Future Capability Register.md`; `PROJECT_STATUS.md`.
