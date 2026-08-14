# TempestOS — Project Status

**Last Updated:** 2026-08-13 (`WP 13.2A`, Plugin Trust & Capability
Enforcement Implementation). Implements `ADR-0110`/`ADR-0111`/`ADR-0112`
exactly as `WP 13.0A` designed and `WP 13.0B` independently audited them —
the trust half of the plugin platform, directly following `WP 13.1A`/
`WP 13.1B`'s own mechanical, non-trust half. Trust tier assignment
(`PluginManifestDiscoveryService.AssignTrustTier`) and detached
SHA-256/RSA-PSS signature verification (`PluginSignatureVerifier`,
`PluginSignatureEnvelope`) run entirely within the existing Plugin
Discovery phase, from file bytes, before any `Assembly.LoadFrom` call;
`Plugins:AllowUnsignedLoad` (new configuration key) governs whether an
unsigned candidate loads at all, defaulting to `false` — fail-closed, per
`ADR-0112`'s own named safe default — isolating it at category 16
otherwise, never silently. Capability enforcement
(`PluginAssemblyLoader.EnforceTrust`) checks a plugin's
`RequestedCapabilities` against its assigned tier's ceiling and reflects
over every discovered `IModule` implementer's constructor for conformance
against a fixed always-allowed baseline plus its own granted
`plugin.services.resolve:*` grants, denying at category 17
(`PluginTrustDeniedException`, recorded `PluginRegistryState.TrustDenied`,
the sixth registry state) before the plugin is ever `Loaded`. A new,
`AsyncLocal<T>`-backed `ICurrentComponentAccessor`/`CurrentComponentAccessor`
(mirroring `ICurrentPrincipalAccessor`'s exact shape, a second, independent
identity axis per `ADR-0111`) is pushed around each module lifecycle call
by `ModuleLifecycleManager`'s new, plugin-unaware `componentScopeProvider`
hook, closed over by `TempestHost`. `NavigationService`, `CommandRegistry`,
`CommandHandlerTable`, and `IEventBus` each gained a trust-ordered
registration/capability-gated-publish retrofit: a non-null registrant must
hold the matching `plugin.*` capability permission; a contested Id is
resolved by trust-tier rank (a higher tier always evicts and replaces a
lower one, logged loudly, never silently); `NavigationService.Unregister`
now performs a real ownership check, closing `TD-10`; every check is
skipped, not merely satisfied, for a `null`/First-Party actor, so every
registrant that exists today observes zero behavioural change. **Directly
closes `TD-09`, `TD-10`, and `TD-11`** (Technical Debt Register) — moved
from "design resolved, retrofit remains open" to **Resolved** — and
updates `Security Roadmap.md` items 1, 2, and 10 from "resolved at the
architecture level, not yet implemented" to implemented. 15 new + 11
modified production files (`src/Tempest.Core/Plugins/`,
`src/Tempest.Core/Identity/`, plus `ModuleLifecycleManager.cs`/
`TempestHost.cs`/`NavigationService.cs`/`CommandRegistry.cs`/
`CommandHandlerTable.cs`/`EventBus.cs`); 2 new test files
(`PluginSignatureVerifierTests.cs`, 15 test methods;
`PluginSigningTestHelper.cs`, an RSA test-key helper) plus 31 modified
test files — 23 pre-existing fixtures needed a two-line
`ICurrentComponentAccessor`/`IPermissionEvaluator` dual-registration
update, the direct, foreseeable consequence of this platform's DI
container requiring every constructor parameter type to be independently
resolvable, once those four types gained new, optional trust-related
constructor parameters. One genuine found-and-corrected test-fixture
finding, not a production defect: `PluginManifestV2FieldsTests.cs` had,
since `WP 13.0A`/`WP 13.1A`, asserted a placeholder `Signature` string
round-tripped verbatim — correct before this Work Package, since
`Signature` was "read, not interpreted"; now that it is actively parsed
and cryptographically verified, the placeholder correctly fails envelope
parsing, so the fixture was corrected to omit the field and pass
`allowUnsignedLoad: true` instead, taking the deliberate unsigned path its
own remaining assertions actually concern. Confirmed absent from this Work
Package's own diff, matching its explicit out-of-scope list: certificate
chains, online revocation, a marketplace, remote trust services, a REST
API, authentication, authorisation beyond plugin capabilities, dynamic
trust changes, live plugin reload. No new ADR — `ADR-0110`–`ADR-0112`
implemented exactly as ratified, zero architectural deviation, confirmed
directly by this Work Package's own Governance & Documentation review; the
`ADR Register.md`'s own Originating Work Package field for all three
updated to cite this Work Package's implementation alongside `WP 13.0A`'s
own design, mirroring `ADR-0105`'s own established
design/implementation-citation precedent. Full detail: `docs/academy/03
Work Packages/WP13.2A-plugin-trust-and-capability-enforcement-implementation.md`.
**`WP 13.1B`'s own status line, below this point, is this field's prior
content — retained, not deleted:**

**Previously updated** 2026-08-13 (`WP 13.1B`, Plugin Runtime & Composition
Root Implementation Review). An independent review of `WP 13.1A` before
its work becomes a commit — mirroring `WP 13.0B`'s own role one Work
Package earlier, applied to real production code for the first time
this release. **Assigned this label directly, colliding with — and
repurposing — this table's own roadmap-predicted `WP 13.1B` row ("REST
API Authentication & TLS Implementation"), the second such collision
this release (`WP 13.1A`'s own row was the first); disclosed plainly in
`WorkPackages.md`, not silently reconciled.** Four parallel, genuinely
independent audit sub-agents — none of which authored any `WP 13.1A`
material, each instructed to assume defects exist until personally
disproven — found and closed **two genuine, empirically-confirmed
production bugs**, the first real code defects any review in this
project's `v0.13.0` history has found: (1) `PluginManifestDiscoveryService.TopologicalSort`
silently dropped a plugin declaring a duplicate dependency entry from
the load order, with zero isolation, log line, or registry entry —
fixed by keying its remaining-dependency count on distinct targets, not
a raw count including duplicates; (2) `TempestHost.cs`'s configuration
precedence chains treated a blank (empty/whitespace) configured
`Runtime:Plugins:RootDirectory`/`ManifestFileName` value as present
rather than absent, faulting the **entire Host** rather than falling
back to default — fixed at both precedence chains. Architecture
Compliance audit: PASS, zero blocking findings, `ADR-0107`–`ADR-0109`
and the `ADR-0017`/`ADR-0109` DI-registration boundary all confirmed
clean. Governance & Documentation audit: PASS, zero defects, twenty
claims fact-checked. Verification & Test Audit independently converged
on the same two bugs via test-completeness judgment and correctly
diagnosed a genuinely pre-existing, unrelated `CompositeLogSinkTests`
flake (confirmed non-reproducible on two clean reruns, not a `WP 13.1A`
regression) — disclosed, not fixed. Eight new regression tests added
closing every real gap found. `governance-healthcheck.ps1` re-run
clean: **7 passed, 1 warned, 0 failed, exit 0**. Debug and Release
builds both **0 Warnings, 0 Errors**, independently re-run after every
fix; full regression, both configurations, independently re-run:
**2,313 / 2,313 passing** (2,092 `Tempest.Core.Tests` + 221
`Tempest.Desktop.Tests` — 2,305 from `WP 13.1A`'s own baseline plus
these 8 new tests). No new Technical Debt introduced; no ADR changes
required or made. This Work Package's own review also found and fixed a
genuine gap `WP 13.1A` itself left: `Academy Register.md`'s own file
counts had never been incremented for `WP 13.1A`'s own retrospective at
all — corrected by independent recount, a two-file jump, not the
one-file jump a naive edit would have produced. `WP 13.1A`'s and this
Work Package's combined material is committed as a single reviewed
implementation baseline immediately following — **not pushed**, per
this Work Package's own explicit instruction; see this repository's own
git log for the exact commit. Full detail: `docs/academy/03 Work
Packages/WP13.1B-plugin-runtime-and-composition-root-implementation-review.md`.
**`WP 13.1A`'s own status line, below this point, is this field's prior
content — retained, not deleted:**

**Previously updated** 2026-08-13 (`WP 13.1A`, Plugin Runtime & Composition
Root Implementation). Builds the mechanical, non-trust half of `WP
13.0A`'s ratified design — `ADR-0107`–`ADR-0109` — leaving the sibling
Trust & Isolation retrofit (`ADR-0110`–`ADR-0112`, `TD-09`/`TD-10`/
`TD-11`) untouched, exactly as scoped, not implemented here. **Assigned
this label directly by the Product Owner, colliding with — and
repurposing — this table's own roadmap-predicted `WP 13.1A` row ("REST
API Authentication & TLS Architecture"), a real, different, still
genuinely uncommissioned Work Package that will need its own number
later; disclosed plainly in `WorkPackages.md`, not silently
reconciled.** Four parallel workstreams: **Runtime Implementation**
built the fixed-point dependency-graph reduction (missing/incompatible-
version removal, three-colour cycle detection, repeat-until-stable, then
Kahn's-algorithm topological sort — `ADR-0107`) and three new isolated
failure categories (12–14); **Dependency Injection & Composition** wired
`Runtime:Plugins:RootDirectory`/`ManifestFileName`/`Disabled`
(`TempestHost.cs`, no Host Lifecycle phase change) and extended
`IDiagnosticsProvider` with a `Plugins` property backed by a direct
`IPluginRegistry` reference; **Verification & Testing** added 45 new
test methods across six new test files plus two extended existing ones —
Debug and Release builds both **0 Warnings, 0 Errors**
(`-p:TreatWarningsAsErrors=true`); full regression Debug **2,305/2,305
passing** (2,084 `Tempest.Core.Tests` + 221 `Tempest.Desktop.Tests`),
Release confirmed to match; **zero genuine production-code defects
found** (two bugs found and fixed in the testing workstream's own new
test assertions, never in production code); **Governance & Documentation**
confirmed no new ADR was warranted, closed `TD-06`/`FCR-0010` (both now
Resolved/Implemented), and updated `docs/academy/02 Runtime
Architecture/07-plugin-architecture.md`'s Status line and two Future
Evolution bullets to reflect real, built code. 10 new + 8 modified
production files under `src/Tempest.Core/Plugins/`,
`src/Tempest.Core/Runtime/TempestHost.cs`,
`src/Tempest.Core/Diagnostics/` — independently re-verified directly via
`git status`/`git diff --stat`, one file more than the orchestrator's own
initial count, disclosed plainly rather than silently reconciled. Full
detail: `docs/academy/03 Work Packages/WP13.1A-plugin-runtime-and-composition-root-implementation.md`.
**`WP 13.0B`'s own status line, below this point, is this field's prior
content — retained, not deleted:**

**Previously updated** 2026-08-13 (`WP 13.0B`, Plugin & Trust Isolation
Architecture Review and Baseline). An independent review of `WP 13.0A`
before its work becomes a commit — **not** the Trust Isolation
implementation the roadmap-predicted table originally named `WP 13.0B`
for; that divergence is disclosed plainly in `WorkPackages.md`, not
silently reconciled. Three parallel, genuinely independent audit
sub-agents (Architecture Audit; Governance Audit; Documentation Audit)
— none of which authored any `WP 13.0A` material — each strictly
read-only, found and (via this Work Package's own triage/fix pass, not
the auditors themselves) closed a small set of real defects `WP 13.0A`
introduced: a stale `ADR Register.md` paragraph internally
contradicting that same register's own already-corrected fields; a
mis-cited failure-category range in `Plugin Trust & Isolation
Architecture.md`; a false "untouched" claim in the `WP 13.0A`
retrospective about two architecture documents it had, in fact, already
edited; a governance Brief left asserting incompleteness after its own
underlying findings were already resolved; two missing ADR citations
(`ADR-0032`/`ADR-0037`) where `ADR-0111` additively revises their
already-Accepted registration behaviour; and two smaller
citation/wording corrections. **Nothing pre-existing was touched or
re-flagged** — `Rejected Designs Register.md`/`Governance Index.md`'s
own long-stale counts, already disclosed by `WP 13.0A` as out of scope,
remain untouched. `governance-healthcheck.ps1` re-run clean after every
fix: **7 passed, 1 warned, 0 failed, exit 0** — identical shape to `WP
13.0A`'s own closing baseline. No new ADR, no new architecture, no
broadened scope. Zero `src/`/`tests/` files touched throughout, at every
stage, by any of the three audit sub-agents or by this Work Package's
own fix pass. `WP 13.0A`'s and this Work Package's combined material is
committed as a single architecture baseline immediately following —
**not pushed**, per this Work Package's own explicit instruction; see
this repository's own git log for the exact commit. Full detail:
`docs/academy/03 Work Packages/WP13.0B-plugin-and-trust-isolation-architecture-review-and-baseline.md`.
**`WP 13.0A`'s own status line, below this point, is this field's prior
content — retained, not deleted:**

**Previously updated** 2026-08-13 (`WP 13.0A`, Plugin & Registration
Trust Isolation Architecture). This release's own first roadmap-predicted
Work Package (`A-3`/`FCR-0001`), commissioned once the Product Owner's
third-party plugin commitment fired at `WP 13.0.0`'s own branch
establishment. Four parallel sub-agents (Plugin Architecture; Security &
Trust; Governance & ADR Review; Documentation & Academy), each confined
to new, non-overlapping files, with every shared governance file
reconciled and applied by a single integrating pass rather than by each
agent independently — avoiding the exact class of drift this project's
own governance suite has repeatedly found and disclosed when a shared
register is touched by more than one uncoordinated change at once.

**Two new architecture documents.** `docs/architecture/Plugin Platform
Architecture.md` — manifest v2 (dependency declarations; a configurable
plugins root/manifest convention, closing `FCR-0010`/`TD-06`; shape-only
`RequestedCapabilities`/`Publisher`/`Signature` fields whose semantics
the sibling document owns), dependency-graph resolution inside the
existing Phase 3.1 (no new Host Lifecycle phase), a new Host-owned
`IPluginRegistry` projected read-only via `IDiagnosticsProvider.Plugins`
(extending `ADR-0039`, not a new service), and an explicit, defended
decision that live in-process plugin unload remains a non-goal for
`v0.13.0` — `ADR-0015`'s no-restart decision reaffirmed, not reversed —
while reserving an unused lifecycle seam contingent on a future
isolation-mechanism change. `docs/security/Plugin Trust & Isolation
Architecture.md` — a four-tier trust model (First-Party/Verified-Signed/
Unsigned-Local/Untrusted); a capability model extending
`IPermissionEvaluator` (`ADR-0044`) via a new, `AsyncLocal<T>`-backed
`ICurrentComponentAccessor`, distinct from the existing ambient
`ICurrentPrincipalAccessor`; a detached SHA-256 manifest+assembly
signature verified entirely at Plugin Discovery, zero new NuGet
dependency; and the central isolation-boundary decision — capability-
scoped, in-process enforcement, confirmed directly **not** a separate
`AssemblyLoadContext` (not a genuine security boundary in modern .NET —
CAS/AppDomain sandboxing was removed from .NET Core) and **not** process
separation (disproportionate to the actual, disclosed threat: vetted,
signed, commercial plugins, not an open, adversarial marketplace). This
directly closes the architecture gap behind three long-open Technical
Debt items — `TD-09` (plugin/module trust isolation), `TD-10`
(`NavigationService.Unregister` ownership), `TD-11` (Command/Navigation
Id registration-order squatting) — via one trust-tier-ordered
registration rule replacing unconditional "first registration wins,"
closing `Security Roadmap.md` items 1, 2, and 10 together, exactly as
that roadmap's own item 10 recommended designing them. **All six new
ADRs are Accepted** (`ADR-0107`–`ADR-0112`); **nineteen new Rejected
Designs entries** (`RD-0046`–`RD-0064`) plus one addendum (`RD-0009`,
reaffirmed, not reversed); **105 → 111 ADR total** (`ADR-0095` remains
permanently reserved, never written — 105 files existed at 106 numbers
before this Work Package, not 106 files; independently confirmed by
`governance-healthcheck.ps1`: "111 files, 111 register rows"). `TD-06`/`TD-09`/
`TD-10`/`TD-11` and `FCR-0001`/`FCR-0010` all updated in the governance
registers to reflect "design resolved" — **none moved to Resolved**,
since this Work Package is architecture only and introduced zero
implementation. A genuine, disclosed cross-document integration gap
(both sibling architecture documents referenced a sixth
`PluginRegistryState.TrustDenied` enum value neither had literally
declared) was found and closed directly, in the one file that already
owned the enum, during integration — not left as a silent inconsistency
between two otherwise-excellent, independently-reconciled documents. An
independent Governance & ADR Review workstream confirmed a
`governance-healthcheck.ps1` clean baseline before either architecture
document existed, and re-confirmed no conflict with any existing
Accepted ADR after integration. **Zero `src/`/`tests/` files touched,
zero code written, zero implementation introduced** — confirmed directly
at every stage of every sub-agent's own work and this integration pass;
implementation is `WP 13.0B`'s own, separately-scoped task, not begun
here. On `feature/v0.13.0`, per its own branch discipline; not committed,
per this Work Package's own explicit "stop before commit" instruction —
pending review before it becomes a real commit. Full detail:
`docs/academy/03 Work Packages/WP13.0A-plugin-and-registration-trust-isolation-architecture.md`.
**`WP 13.0.0A`'s own status line, below this point, is this field's
prior content — retained, not deleted:**

**Previously updated** 2026-08-13 (`WP 13.0.0A`, Release Register
Reconciliation). Closes the one finding `WP 13.0.0` disclosed rather
than fixed: `docs/governance/Delivery/Release Register.md` had no row
for the `v0.12.0` tag. Independently confirmed pre-existing, dating to
`v0.12.0`'s own close (`git show 13a6ce3:"docs/governance/Delivery/Release
Register.md"` — already missing at the exact commit the tag points to,
not caused by any `v0.13.0` Work Package). `v0.12.0` row added,
matching this register's own established evidence/density convention;
all eleven real tags (`v0.3.0`–`v0.12.0`) independently re-verified
present in the Entries table exactly once each, cross-checked directly
against `git tag -l "v*"` and each tag's own resolved commit/date, not
carried forward from any prior figure. Total corrected 11 → 12 versions
referenced (10 → 11 confirmed released). `governance-healthcheck.ps1`
re-confirmed clean: **7 passed, 1 warned, 0 failed, exit code 0** —
Release Register now `[PASS]`. Governance/documentation only; zero
`src/`/`tests/` files touched, zero architecture, implementation, or
release behaviour changed. On `feature/v0.13.0`, per its own branch
discipline (commit directly, never rebase, never squash); not pushed.
Full detail: `docs/academy/03 Work Packages/WP13.0.0A-release-register-reconciliation.md`.
**`WP 13.0.0`'s own status line, below this point, is this field's
prior content — retained, not deleted:**

**Previously updated** 2026-08-13 (`WP 13.0.0`, `v0.13.0` Branch
Establishment). `feature/v0.13.0` cut directly from the released
`v0.12.0` tag (`13a6ce3`) — confirmed, not assumed: `git merge-base
feature/v0.13.0 v0.12.0` resolves to that exact commit. This branch is
the **sole** integration branch for every `v0.13.0` Work Package, per
this Work Package's own explicit instruction (stricter than every
prior release's multiple-parallel-branch convention) — no additional
feature branches without separate authorisation; every Work Package
commits directly to it; never rebase, never squash; merge commits only
when integrating back to `main` at this release's own close. `VERSION`
deliberately left at `0.12.0` (not bumped to a "dev" placeholder) —
this project's own documented versioning policy (`WP11.1B Engineering
Workflow.md` §7) recognises only `MAJOR.MINOR.PATCH` and a `-rc.N`
suffix; a `-dev` suffix was considered and rejected, confirmed directly
to break `governance-healthcheck.ps1`'s own version-token parsing
(`[version]"0.13.0-dev"` throws) — `VERSION` reflects the last
*released* version until the moment of the next tag, exactly as it did
throughout `v0.12.0`'s entire development (`0.11.0`, unchanged, until
the final pre-tag commit). `docs/releases/v0.13.0/WorkPackages.md`
created, seeded from `WP11.0B Architecture Roadmap.md` §3's own
predicted `v0.13.0` table — "Trust & Deployment Hardening," conditional
on the Product Owner committing `v1.0` scope to third-party plugins
and/or REST deployment beyond a trusted local network. That trigger had
not fired as of `v0.12.0`'s own close (`src/Plugins/` still empty; no
`v0.12.0` Work Package touched REST API scope) — the Product Owner has
now made that commitment explicitly, confirmed directly as this Work
Package's own commissioning, not inferred from the branch's mere
existence. **`v0.12.0`'s own release, below this point, is this
field's prior content — retained, not deleted; its own final
completion (tag, GitHub Release) is recorded here since no prior entry
did:**

**`v0.12.0` released**, 2026-08-13 — tag `v0.12.0` (annotated, object
`f430f6f`), commit `13a6ce3`, confirmed directly to point to the exact
commit `main`'s own tip resolved to at release time. GitHub Release
published automatically by `.github/workflows/release.yml`
(tag-triggered, independently re-built and re-tested Release
configuration against the tagged commit, real GitHub Actions-confirmed
success) — https://github.com/kreczmans-creator/TempestOS-Core/releases/tag/v0.12.0.
**Final Engineering Readiness verdict: `CERTIFIED WITH ACCEPTED
TECHNICAL DEBT`** (`ADR-0106` §4, mechanically derived — zero Release
Blocking finding, zero Disclosed Non-Blocking finding, `TD-42`/`TD-43`/
`TD-45` the operative Pre-Existing, Unaffected finding, §4's own row 3;
full evidence `docs/releases/v0.12.0/WP12.9.4 Engineering Release
Report.md`). Thirteen Work Packages (`WP 12.3A`–`WP 12.9.3B`); five new
ADRs (`ADR-0102`–`ADR-0106`); 2,255/2,255 tests passing both
configurations throughout. Previously updated 2026-08-13 (`v0.12.0`
Release Preparation — `WP 12.9.3B`, Governance Health Check
Consistency Remediation). Pushing `main` for the first time in this
release (`WP 12.9.3`)
surfaced a real GitHub Actions `Governance Health Check` `[FAIL]`
invisible to every local run: `Test-ProjectStatusReferences` and
`Test-ReleaseRegisterMatchesTags` both consume `Get-RepoTags`, but only
the latter treats a shallow checkout's empty tag list as a disclosed
environmental limitation (`Warn`) — the former had no equivalent guard,
producing a false `Fail` against every real historical version token
(`v0.3.0`–`v0.10.0`) whenever tags happened to be unavailable.
`WP 12.9.3A` (architecture/investigation only, no file changed)
confirmed this is a checker-architecture inconsistency, not a
repository defect — this document's own content was always accurate.
`WP 12.9.3B` implements the approved fix, scoped to exactly the
affected sub-check: version-token validation degrades to `Warn`,
disclosed, when zero tags are available; path-reference validation,
independent of tag availability, continues unconditionally and can
still `Fail` the check on its own. `Get-RepoTags` and the Release
Register check both left unmodified. Four behavioural cases verified
against isolated fixtures; full Debug+Release regression re-run,
2,255/2,255 passing both, 0 Warnings/0 Errors both. Reviewed and
integrated into `main` by `WP 12.9.3C` (Final Verification and
Integration) via the normal Engineering Governance merge process, after
an independent architecture/code review confirmed the implementation
matches `WP 12.9.3A`'s own approved recommendation exactly and a full,
independent re-verification (Governance Health Check, both builds,
both test suites) passed clean. Full detail: `docs/academy/03 Work
Packages/WP12.9.3B-governance-health-check-consistency-remediation.md`.
**`WP 12.9.2`'s own status line, below this point, is this field's
prior content — retained, not deleted:**

**Previously updated** 2026-08-13 (`v0.12.0` Release Preparation —
`WP 12.9.2`, Engineering Readiness Review Re-Execution). Re-runs the
complete ERR (`ADR-0106`) after `WP 12.9.1`'s governance remediation,
independently re-deriving every Phase A item from source, not relying
on `WP 12.9.0`'s own report. Found `WP 12.9.1`'s own changes had never
been committed — corrected within this same Work Package (commit
`955badb`, local only, no push; Engineering Governance §7.5), since
"the current repository state after `WP 12.9.1`" could not otherwise
be assessed as a real, tag-able commit. `governance-healthcheck.ps1`
re-run clean: **7 passed, 1 warned, 0 failed, exit code 0** — both
Disclosed, Non-Blocking findings `WP 12.9.0`'s own execution raised
(Academy Index gap; Engineering Vocabulary Register's missing link)
are now closed. Debug+Release builds and full regression freshly
re-run, 2,255/2,255 passing both, 0 Warnings/0 Errors both — identical
to `WP 12.9.0`'s own figures (zero `src/`/`tests/` files changed besides
one documentation marker). Six independent discipline reviews: Architecture/
Implementation/Governance/Release readiness all **Pass** (Governance
readiness improves from `WP 12.9.0`'s own "Pass, with observations" to
a clean Pass); Verification readiness **Not Ready** — the identical
class of finding `WP 12.9.0`'s own first execution raised, now against
`955badb` (committed but not pushed) rather than `fb76ce8`: real,
GitHub-hosted CI has not run against the commit this release would tag.
**Overall verdict: `NOT READY`, pending exactly one action** — pushing
`main`, then confirming real CI passes against `955badb` — expected to
become `CERTIFIED` on that evidence alone, since this execution finds
zero findings of any kind beyond the CI-evidence gap itself. Full
detail: `docs/releases/v0.12.0/WP12.9.2 Engineering Release Report.md`.
No tag, GitHub release, or push created or proposed — outside this
Work Package's own authorised scope and independently prohibited by
the `NOT READY` verdict itself. **`WP 12.9.0`'s own status line, below
this point, is this field's prior content — retained, not deleted:**

**Previously updated** 2026-08-12 (`WP 12.9.0` — Release Preparation &
Engineering Sign-Off, Architecture phase. The final roadmap-defined
`v0.12.0` Work Package, on its own new branch (`feature/v0.12.0-release-preparation-and-signoff`).
Designs the permanent TempestOS Engineering Readiness Review (ERR):
five readiness categories (Architecture/Implementation/Verification/
Governance/Release), each with defined required evidence, pass
criteria, and blocking conditions; a written blocking taxonomy (Release
Blocking / Disclosed Non-Blocking / Pre-Existing Unaffected — no prior
sign-off ever wrote this down); a fixed, four-value verdict vocabulary
(`CERTIFIED`; `CERTIFIED WITH ACCEPTED TECHNICAL DEBT`; `ACCEPT WITH
OBSERVATIONS`; `NOT READY`); executed via the six-discipline Programme
Review `WP 10.9A`/`WP 11.9.0` each already, independently, converged
on. **`ADR-0106` produced** — three alternatives evaluated and
rejected (continue the ad hoc checklist; a single-reviewer sign-off; a
purely automated gate), each on this project's own concrete evidence,
most directly `WP 11.9.0`'s own independently-found `TD-42`/`TD-43` as
the case against collapsing multi-discipline review. **Phase A
repository assessment**, performed directly, not carried forward:
`main`'s own merge state confirmed via `git log` (`WP 12.0A`/`12.0B`,
`12.1A`/`12.1B`, `12.3A`/`12.3B`, `12.4A`/`12.4B` all merged; `WP
12.2A`'s own disposition complete but not yet committed — its branch
tip is byte-identical to `main`'s own, its changes existing only as an
uncommitted stash); Debug+Release builds and full regression freshly
re-run (0 Warnings/0 Errors both, 2,255/2,255 passing both);
`scripts/governance-healthcheck.ps1` run directly (3 passed, 1 warned,
4 failed of 8 — one genuine, previously-undisclosed, `v0.12.0`-caused
gap found: `Engineering Vocabulary Register.md` never linked from
`Governance Index.md`; the remaining failures are the Academy Index's
own large, pre-existing, already-disclosed gap and mostly tool-level
path-check false positives); Technical Debt (48 entries) and Future
Capability (84 entries) registers both independently re-derived by
direct count, matching their own claimed totals exactly, nothing
newly blocking. **Found and fixed, this Work Package's own
Governance-readiness review**: `Architecture Document Register.md`'s
own `Classification & Relationship Vocabulary Safety Net Architecture.md`
row was still marked Designed despite `WP 12.1B` having implemented
`ADR-0105` and merged it into `main` — corrected directly, plus that
register's own stale `Related ADRs` field. **This is explicitly not
release engineering** — no verdict is rendered for `v0.12.0` itself;
the actual `v0.12.0` Engineering Readiness Review execution (the six
independent reviews, the reconciled Programme Review, Product
Approval's own verdict) remains a distinct, later, separately-commissioned
Work Package, gated on `WP 12.2A`'s own disposition being committed
and merged first, among other preconditions named directly in
`docs/architecture/Engineering Readiness Review Architecture.md`'s own
"Execution boundary" section. **Architecture review and follow-up,
same day, before any commit**: a read-only review found two blocking
findings — the ADR Register's own Entries table missing `ADR-0106`'s
row (`governance-healthcheck.ps1`: 105 files, 104 register rows), and
an internally contradictory relationship between category status, the
blocking taxonomy, and the verdict vocabulary that made `CERTIFIED
WITH ACCEPTED TECHNICAL DEBT` unreachable as originally written. Both
closed: the Entries table row added (`governance-healthcheck.ps1` now
`[PASS]`, 105/105); the architecture document's §4 rewritten as the
single, explicit, priority-ordered derivation from finding kind →
category status → release verdict, with §2/§6 and `ADR-0106`'s own
Decision section corrected to cite it rather than each restate their
own version — the minimum change making all four verdicts
simultaneously reachable, mutually exclusive, and objectively
derivable. No other part of the model touched. Architecture only; zero
`src/`/`tests/` files touched, confirmed directly; no commits, merge,
tag, or push performed at either pass. See `docs/academy/03 Work
Packages/WP12.9.0-release-preparation-and-engineering-sign-off-architecture.md`
(§8 Addendum for the full account),
`docs/architecture/Engineering Readiness Review Architecture.md`,
`ADR-0106`. **`WP 12.2A`'s own status line, below this point, is this
field's prior content — retained, not deleted:**

**Previously updated** 2026-08-12 (`WP 12.2A` — Presentation Strategy
Execution Architecture. The roadmap-defined `v0.12.0` Work Package
(`WP11.0B Architecture Roadmap.md` §3), on its own new branch
(`feature/v0.12.0-presentation-strategy-execution`), scoped as
"Presentation Strategy Execution (realises `WP 11.2A`'s decision)."
Discovery (Phase A) finds `WP 11.2A` itself was never commissioned as
predicted — its label was reused for Governance Health-Check Automation
instead — and the real Desktop & Console Presentation Strategy Decision
(`WP11.0A` finding `A-2`) was ratified and executed under a different
pair, `WP 11.3A` (review, `Complete`) and `WP 11.3B` (implementation,
`Complete`), both in `v0.11.0`, before `v0.12.0` even branched. Every
claim independently re-verified directly against live source, not
carried forward: `TempestShell`/`IPage`/`PlaceholderPage` confirmed
retired (zero references anywhere in `src/`/`tests/`); `ADR-0101`
confirmed Accepted and unmodified, formally classifying `Tempest.App`/
`WorkspaceShell` as TempestOS's Internal Engineering Harness, not a
shipped product; `README.md`/`Contributor Learning Path.md` confirmed
corrected; `ci.yml`/`release.yml` confirmed packaging `Tempest.Desktop`
and `Tempest.App` as two separate, clearly-labelled artifacts;
`Shell & Composition Framework Architecture.md` confirmed already
reflecting the final state via its own `WP 11.3B` addendum. `WP11.3A`'s
own five-stage roadmap's Stage 5 (further `WorkspaceShell` test/feature
trimming) was deliberately not executed by `WP 11.3B` — confirmed, not
assumed, to be a formal, disclosed deferral (`WP11.9.0 Engineering
Release Report.md`'s own "Product Debt... deferred by design" entry),
gated on "a real, demonstrated cost problem" that a direct search of
both the Technical Debt Register and the Future Capability Register
confirms has still not occurred. Phase B (Architectural Analysis)
evaluates four disposition alternatives (execute Stage 5 speculatively;
re-run the investigation from scratch; formally close as delivered,
adopted; leave the Status silently stale) and concludes, honestly,
neither a new ADR nor a new/updated architecture document is justified
— Engineering Governance §5's own ADR-creation criteria is not met (no
new either/or choice exists to record), and `Shell & Composition
Framework Architecture.md` already documents the current state
correctly. `docs/releases/v0.12.0/WorkPackages.md`'s own `WP 12.2A` row
updated from "Not started — scope set by `WP 11.2A`" to "Delivered by
`WP 11.3A`/`WP 11.3B` — no remaining scope," mirroring this project's
own identical, already-established disposition pattern for the sibling
`WP 11.1B`/`WP 11.2A` renumbering. Architecture only; zero `src/`/
`tests/` files touched, confirmed directly; no ADR produced; no build
or regression run required or performed. `docs/governance/Documentation/
Academy Register.md` and `Documentation Register.md` updated to
disclose this Work Package's own new Academy retrospective; `ADR
Register.md` and `Architecture Document Register.md` are **not**
modified — genuinely unaffected. See
`docs/academy/03 Work Packages/WP12.2A-presentation-strategy-execution-architecture.md`,
`docs/releases/v0.11.0/WP11.3A Presentation Strategy Review.md`,
`ADR-0101`. **`WP 12.1B` Architecture Review Follow-Up's own status
line, below this point, is this field's prior content — retained, not
deleted:**

**Previously updated** 2026-08-12 (`WP 12.1B` Architecture Review Follow-Up —
documentation only, closing the two findings from `WP 12.1B`'s own
read-only architecture/code review. **Finding 1**: the Engineering
Vocabulary Register's own narrative claimed a declaring-class count of "12";
re-derived directly from the register's three Entries tables (manual
enumeration, cross-checked by an automated extraction), the true count
is **11** — the original figure over-counted `VerificationService`,
which appears in both the Kind and RelationshipKind tables but is one
class. All 46 individual values and their declaring classes were
themselves already correct; corrected the propagated "12" in all seven
affected documents (`Engineering Vocabulary Register.md`, this file,
`docs/releases/v0.12.0/WorkPackages.md`, `Documentation Register.md`,
`Academy Register.md`, `ADR Register.md`, the `WP 12.1B` Academy
retrospective) and confirmed by repository-wide search that no
occurrence remains. **Finding 2**: `Classification & Relationship
Vocabulary Safety Net Architecture.md`'s own Component 3 description
named only two of the four detection checks the implemented
`EngineeringVocabularyConsistencyTests` actually provides; updated to
describe all four (register drift, duplicate canonical-owner detection,
register self-consistency, rogue duplicate vocabulary scan across
assemblies) so documentation matches implementation. `ADR-0105` not
reopened; zero `src/`/`tests/` files touched, confirmed directly; no
build or regression run required or performed — no production code
changed. See the `WP 12.1B` architecture/code review findings (read-only,
not itself a separate document) and `docs/governance/Engineering/Engineering
Vocabulary Register.md`'s own corrected "Last Reviewed" entry.
**`WP 12.1B`'s own status line, below this point, is this field's prior
content — retained, not deleted:**

**Previously updated** 2026-08-12 (`WP 12.1B` — Classification & Relationship
Vocabulary Safety Net Implementation. Realises `ADR-0105` exactly, on
the same branch as `WP 12.1A`; implements the architecture only, does
not revisit or extend the decision. Every `FactoryRegistry`
retrofitted: `MechanicalObjectFactoryRegistry` (the largest confirmed
gap) gains all eight Kind constants (`Project`/`Assembly`/
`SubAssembly`/`Part`/`Component`/`Configuration`/`Baseline`/`Release`),
used internally throughout its own `SupportedKinds`/switch/factory-
argument sites; `DocumentObjectFactoryRegistry`/
`ManufacturingObjectFactoryRegistry` gain their own base Kind constants
alongside their already-disciplined `Classification` sub-values;
`CalculationObjectFactoryRegistry` gains `CalculationKind`/
`CalculationSetKind`. `DigitalThreadGraphModel`'s own confirmed
duplicate closed — found, while directly reading the file, to be
**three** local constants, not one (`VerificationActivityKind`/
`VerificationRecordKind`/`VerifiedByRelationshipKind`), all now
referencing their real owning constants
(`VerificationActivityFactoryRegistry.SupportedKind`;
`VerificationService.VerificationRecordDocumentKind`/
`VerifiedByRelationshipKind`) instead. New Engineering Vocabulary
Register (`docs/governance/Engineering/Engineering Vocabulary
Register.md`) populated from a full, direct repository scan — 46
declared values (23 Kind, 12 `Classification`, 11 `RelationshipKind`)
across 11 declaring classes, plus 7 further conventional
`RelationshipKind` values honestly marked **Undeclared** rather than
omitted. **One genuine, pre-existing dual-ownership case found and
disclosed, not treated as a defect**: `references` is independently,
legitimately declared by both `RequirementRelationshipKinds` and
`VerificationService` for a genuinely shared, conventional meaning
neither discipline exclusively owns — mirrors `ADR-0073`'s own
already-accepted "vocabulary drift" risk, recorded explicitly with its
own register footnote and named by exception in the consistency test.
New `EngineeringVocabularyConsistencyTests` (`Tempest.Desktop.Tests`,
per the corrected `WP 12.1A` architecture review) — four tests
covering register/code drift, cross-owner duplication, register
self-consistency, and a rogue-duplicate scan across all four layer
assemblies (`Tempest.Core`/`Tempest.App`/`Tempest.Samples`/
`Tempest.Desktop`, public and private `const string` fields, since the
confirmed `DigitalThreadGraphModel` duplicates were `private`).
**Verified to actually work, not merely assumed**: a rogue duplicate
was deliberately, temporarily reintroduced into `DigitalThreadGraphModel`
mid-implementation — the consistency test failed immediately, with a
precise, actionable message naming both the offending class and the
real owner; reverted immediately after confirming the failure. Three
characterization tests added before refactoring
(`MechanicalCommandsTests.Create_SupportedKindWithoutParent_Succeeds`
gained `Configuration`/`Baseline`/`Release` `InlineData` cases,
closing a real, confirmed-by-direct-search gap `WP 12.1A`'s own
literal-string grep had missed entirely, since the existing coverage
used `[Theory]`/`[InlineData]` theory parameters, invisible to that
grep pattern — re-derived directly, method by method, before touching
any production code). **No write-time validation, no runtime registry
or lookup service, no enum, no strongly-typed value object introduced
anywhere** — the platform's own open-vocabulary philosophy fully
preserved; every prior ADR's own "never validated" guarantee
(`ADR-0073`/`ADR-0076`/`ADR-0088`/`ADR-0090`/`ADR-0091`) left
completely intact. Full Debug+Release regression: 2,255/2,255 passing
(2,034 `Tempest.Core.Tests` + 221 `Tempest.Desktop.Tests`, net +7 new
tests — 3 characterization, 4 consistency — zero regressions), 0
Warnings/0 Errors, both configurations. Behaviour confirmed unchanged,
not merely asserted — every refactored `FactoryRegistry`'s own
constants hold the identical values the literals they replace already
held; every pre-existing test passes with unchanged assertions against
the refactored code. No new ADR — `ADR-0105` unmodified, implemented as
designed. No commit, merge, tag, or push performed. See
`docs/governance/Engineering/Engineering Vocabulary Register.md`,
`tests/Tempest.Desktop.Tests/EngineeringVocabularyConsistencyTests.cs`,
`docs/academy/03 Work
Packages/WP12.1B-classification-and-relationship-vocabulary-safety-net-implementation.md`,
`docs/releases/v0.12.0/WorkPackages.md`. **`WP 12.1A`'s own Architecture
Review Follow-Up status line, below this point, is this field's prior
content — retained, not deleted:** (`WP 12.1A` Architecture Review
Follow-Up — closes all findings from the read-only architecture review
of `WP 12.1A`, documentation only, no production code or tests. (1)
`Architecture Document Register.md`'s own Total and Coverage sections
fully re-derived, not carried forward: the Entries table has 26 real
rows (24 under `docs/architecture/`, 2 under `docs/releases/`),
independently re-counted directly (`grep -c`, `ls`) rather than
assumed — the prior "24 documents (22 under `docs/architecture/`...)"
Total line and the Coverage table's own stale "Implemented: 18"
(17-item list, three Implemented documents missing from it) are both
corrected; all five Coverage buckets (20 + 1 + 3 + 1 + 1 = 26) now sum
to the Entries table's own row count exactly. (2) `Classification &
Relationship Vocabulary Safety Net Architecture.md`'s own Component 3
consistency-check proposed home moved from `Tempest.Core.Tests` to
`Tempest.Desktop.Tests` — confirmed directly that `Tempest.Core.Tests.csproj`
has no `ProjectReference` to `Tempest.Desktop`, so a test placed there
could never reflect over `DigitalThreadGraphModel` and could never
have caught the confirmed, motivating `VerifiedByRelationshipKind`
cross-layer duplicate; `Tempest.Desktop.Tests.csproj` already
references all three layers (`Tempest.Core`/`Tempest.App`/
`Tempest.Desktop`) and is the only structurally correct home. (3)
`ADR-0105` clarified in two places, its own Decision unchanged: states
explicitly that vocabulary ownership is determined by which component
*writes* a value (calls `CreateAsync`/`LinkAsync`), never by which
project compiles the underlying type — grounded in a real example
(`VerificationActivity`, compiled in `Tempest.Core.EngineeringDomain`
but written only by the Workspace-layer `VerificationActivityFactoryRegistry`,
confirmed by direct search finding zero writers in `Tempest.Core`) and
`ADR-0078`'s own "one Kind, one owner" precedent; and explains why
every declared constant is `public`, not `internal` as `WP11.0A`
Finding `A-6`'s own illustrative suggestion named — cross-layer reuse
(the entire point of this ADR) requires it, and every already-shipped
precedent this ADR generalises was already `public`. No other
governance document was found to cite the corrected figures — checked
directly, `docs/governance/Documentation/Documentation Register.md`'s
own "24 standing architecture documents" line describes the physical
`docs/architecture/` folder only (unaffected, still accurate) and
required no change. Documentation only; zero `src/`/`tests/` files
touched, confirmed directly; no commit, merge, tag, or push performed;
`WP 12.1B` not implemented. See `docs/governance/Architecture/Architecture
Document Register.md`, `docs/architecture/Classification & Relationship
Vocabulary Safety Net Architecture.md`, `ADR-0105`. **`WP 12.1A`'s own
status line, below this point, is this field's prior content —
retained, not deleted:** (`WP 12.1A` — Classification &
Relationship Vocabulary Safety Net Architecture. The roadmap-defined
`v0.12.0` Work Package, realising `WP11.0A Platform Architecture
Review.md` Finding `A-6` ("Domain classification/relationship
vocabulary is entirely stringly-typed, by deliberate design, with no
compile-time safety net"), on its own new branch
(`feature/v0.12.0-classification-relationship-vocabulary-safety-net`).
Audits every Kind/`Classification`/`RelationshipKind` vocabulary
across `Tempest.Core`/`Tempest.App`/`Tempest.Desktop`. Confirms five
prior ADRs (`ADR-0072`/`ADR-0073`/`ADR-0076`/`ADR-0088`/`ADR-0091`)
already, independently, settle the open-string-vs-closed-type question
— none reopened — while `ADR-0090` provides direct evidence that
`Classification`-tagging is not universally the right sub-typing axis
(Verification deliberately reuses `LifecycleState` instead, "rather
than... two fields for one distinction, never reconciled against each
other"). Finds the confirmed gap is precisely bounded, not
platform-wide: four `Tempest.Core` frameworks
(Requirements/Verification/Calculations/Materials) and two Workspace
disciplines (Documents/Manufacturing, `ADR-0088`/`ADR-0091`) already
declare their own vocabulary values as named `public const string`
constants; Mechanical declares none, across any of its own eight
Kinds. **Quantified, not estimated**: the literal Kind string `"Part"`
is written out at 14 separate sites across 5 different `src/` files
spanning three layers, zero referencing a shared constant; a confirmed
cross-layer duplicate — `Tempest.Desktop.DigitalThread.DigitalThreadGraphModel`
independently redeclares `Tempest.Core.Verification.VerificationService`'s
own `VerifiedByRelationshipKind` constant, identical value, identical
name. Finds and generalises three already-shipped, independently-proven
instances of the eventual safety-net shape (`RequirementRelationshipKinds`,
`RelationshipKindCategoryMap`, `DocumentCategory.Of`/
`ManufacturingCategory.Of`) rather than inventing a new mechanism.
**`ADR-0105` produced** — every live Kind/`Classification`/
`RelationshipKind` value declared exactly once, as a named constant,
on its owning class; a new Engineering Vocabulary Register
(`docs/governance/Engineering/`) tracks every value platform-wide; one
additive xUnit test (not a new tool, not a build gate) catches
exact-value collisions and register/code drift — none of this
validates a value at write time, every prior ADR's own "never
validated" guarantee left completely intact. Four alternatives
evaluated and rejected on real evidence: a closed enum (already
rejected five times, independently, by five prior ADRs); a
strongly-typed value object (no material benefit over a plain
constant, real adoption cost across nine-and-growing disciplines); a
runtime, DI-resolved registry service (the identical "misclassifies a
coordination concern as a Platform Service" reasoning `ADR-0104` just
applied to Desktop wiring); a fully free-form metadata model (a
genuine `IHasMetadata` contract redesign, widening rather than
narrowing the coordination problem). Digital Thread roadmap
compatibility confirmed low-risk directly —
`DigitalThreadGraphModel` is fully Kind-agnostic except for its own
two hand-typed local duplicates, the confirmed defect above.
Architecture only; zero `src/`/`tests/` files touched, confirmed
directly; no commit, merge, tag, or push performed. `WP 12.1B`'s own
minimum scope named directly, not left merely implied: populate the
Engineering Vocabulary Register, add the consistency test, retrofit
the two confirmed defects by name. See `docs/architecture/Classification
& Relationship Vocabulary Safety Net Architecture.md`, `ADR-0105`,
`docs/academy/03 Work Packages/WP12.1A-classification-and-relationship-vocabulary-safety-net-architecture.md`,
`docs/releases/v0.12.0/WorkPackages.md`. **`WP 12.4B`'s own status
line, below this point, is this field's prior content — retained, not
deleted:** (`WP 12.4B` — Desktop Command & Event
Wiring Implementation. A third directly-commissioned `v0.12.0` Work
Package, on the same branch as `WP 12.4A`
(`feature/v0.12.0-desktop-command-event-wiring`). Realises `ADR-0104`
exactly as approved. Three characterization tests added before any
refactor (`MechanicalCreate_OnARealPart_ActuallyCreatesARealObject`,
`MechanicalDuplicate_OnARealPart_ActuallyCreatesARealCopy`,
`DocumentsApprove_OnAFreshDraftDocument_IsRejectedByRealValidation_NeverThrows`),
closing two real, confirmed-by-direct-search gaps: the entire Mechanical
discipline had zero direct Ribbon-handler test coverage anywhere, and
the shared `statusHandler` factory's own failure branch had never been
exercised by any existing test. **Two `ADR-0104`-compliant changes, both
direct delegates, neither a typed callback interface.** Before `WP
12.4B`, `WorkspaceViewCoordinator`'s own genuine, delegate-shaped
callback-parameter count was 2 (`refreshStatusBar`, `recordHistory`) —
below `ADR-0104`'s own 3-callback threshold. `WP 12.4B` introduces a
third, `refreshCockpit`, bringing the count to 3 and reaching that
threshold. Despite reaching it, introducing a typed callback interface
in the same change that first crosses the threshold was assessed and
deferred as an explicit, disclosed engineering judgement — not applied
speculatively — even though `WorkspaceViewCoordinator` is `ADR-0104`'s
own named motivating example. (1) `RibbonObjectActionHandlers`'s own 16
duplicated "report via StatusBar/Toast, then refresh Explorer/Cockpit"
tails (`WP 12.4A`'s own quantified finding) consolidated into one local
function, `ReportAsync`, defined inside the existing constructor — pure
extract-method, zero new abstraction, zero constructor-signature change;
every handler's own success/failure message text preserved unchanged,
and `mechanical.create`/`mechanical.duplicate`'s own previously-double-
computed message ternary normalised to compute once, matching the other
14 sites' own existing convention. (2) `WP 12.0B`'s own architecture
review Finding 5 closed: `UndoRedoCoordinator` and
`WorkspaceViewCoordinator` both previously carried a two-phase-
constructed `CockpitView` object reference (a nullable field plus an
`Attach`/`AttachCockpitView` method) purely to call its own `Refresh()`
— replaced in both with a plain `Action refreshCockpit` constructor
parameter, wired from `MainWindow` via the identical field-closure
lazy-capture pattern already established there for `DocumentAreaView`.
`WorkspaceViewCoordinator`'s own two-phase `Attach` now takes only
`DocumentAreaView` — the one genuine remaining construction-order cycle;
`UndoRedoCoordinator`'s own `AttachCockpitView` method removed entirely.
**Zero Desktop mediator, Desktop-local command dispatcher, or
Desktop-local event dispatcher introduced; the platform Command/Event
Framework left untouched** — all four exactly as instructed.
`ADR-0103`'s ownership rules preserved throughout: every collaborator
remains `new`-constructed by `MainWindow`, never DI-registered, never
referencing a sibling collaborator directly. All public APIs preserved;
behaviour verified identical, not merely asserted unchanged — the three
new characterization tests (plus the full pre-existing suite) pass
against both the pre-refactor and post-refactor code, identical
assertions. Full Debug+Release regression: 2,248/2,248 passing (2,031
`Tempest.Core.Tests` + 217 `Tempest.Desktop.Tests`, net +3 from the new
characterization tests, zero regressions), 0 Warnings/0 Errors, both
configurations. No new ADR — `ADR-0104` unmodified, implemented as
designed with no factual correction exposed; `Desktop Command & Event
Wiring Architecture.md`'s own Status flipped Designed → Implemented.
No commit, merge, tag, or push performed. See `docs/academy/03 Work
Packages/WP12.4B-desktop-command-and-event-wiring-implementation.md`,
`docs/architecture/Desktop Command & Event Wiring Architecture.md`,
`ADR-0104`, `docs/releases/v0.12.0/WorkPackages.md`. **`WP 12.4A`'s own
status line, below this point, is this field's prior content — retained,
not deleted:** (`WP 12.4A` — Desktop Command & Event
Wiring Architecture. A second directly-commissioned `v0.12.0` Work
Package (`WP 12.3A`/`WP 12.3B`'s own precedent), on its own branch
`feature/v0.12.0-desktop-command-event-wiring` (cut from `main` after
`WP 12.0A`/`WP 12.0B` merged). Reviews the Desktop layer's own command,
event, and UI-wiring architecture after `WP 12.0B`'s decomposition.
Catalogues every event in `Tempest.Desktop` directly (25, all plain
`Action`/`Action<T>` delegates; zero unsubscription, zero `IDisposable`
anywhere — found safe today given exactly one `MainWindow` is ever
constructed per process, `ADR-0103`'s own "Ownership and lifetime"
section already establishing why). Documents four distinct,
already-individually-ADR-governed command mechanisms
(`ICommandRegistry`/`ICommandDispatcher`, `RibbonView.ObjectCreationHandlers`,
`KeyboardShortcuts`, `KeyboardCommandBindingProvider`) now composing
together for the first time in one visible place, confirming none is
redundant. Finds and quantifies real duplication — a four-statement
"report via StatusBar/Toast, then refresh Explorer/Cockpit" tail
repeated 42/25/25/19 times respectively across six files, concentrated
in `RibbonObjectActionHandlers`/`WorkspaceViewCoordinator` — named as a
real implementation-hygiene opportunity for a future `WP 12.4B`,
deliberately not designed in full here. Re-confirms
`WorkspaceViewCoordinator` as the strongest remaining decomposition
candidate (`WP 12.0B`'s own architecture review Finding 4, unchanged);
confirms neither it, `RibbonObjectActionHandlers`, nor `MainWindow`'s
own constructor meets this project's own "God Method" bar
(`FOUNDATION.md` non-negotiable 2) today. Evaluates six candidate
cross-collaborator communication mechanisms in full (advantages/
disadvantages/layering/ownership/lifetime/testing each): direct
delegates, typed callback interfaces, a Desktop-local Mediator, a
Desktop-local Command Dispatcher, a Desktop-local Event Dispatcher, and
reuse of the existing platform Command/Event Framework. **`ADR-0104`
produced** — direct delegates remain the default; typed callback
interfaces sanctioned narrowly, at three or more bundled callbacks
(directly answering `WorkspaceViewCoordinator`'s own 18-parameter
constructor); the Mediator, a second Command Dispatcher, and a second
Event Dispatcher are each explicitly rejected as Desktop composition
mechanisms, on mechanism-specific grounds, not merely inferred from
`ADR-0103`'s more general text — meets Engineering Governance §5's own
ADR-creation criteria, applying `ADR-0103`'s own principles more
specifically rather than replacing or narrowing them (this Work
Package's own first-pass conclusion of "zero new ADR" was revised in
the same Work Package once its own fuller brief's evaluation was
carried out — disclosed directly here, not silently corrected).
**Disclosed naming correction**: initially proposed under the label
`WP 12.1A`, already the roadmap's own name for an unrelated item
(Classification & Relationship Vocabulary Safety Net); corrected before
any document was written. Architecture only; zero `src/`/`tests/`
files touched, confirmed directly; no commit, merge, tag, or push
performed. See `docs/architecture/Desktop Command & Event Wiring
Architecture.md`, `ADR-0104`, `docs/academy/03 Work
Packages/WP12.4A-desktop-command-and-event-wiring-architecture.md`,
`docs/releases/v0.12.0/WorkPackages.md`. **`WP 12.0B`'s own status
line, below this point, is this field's prior content — retained, not
deleted:** (`WP 12.0B` — Desktop Composition Root
Decomposition Implementation. Closes `WP11.0A Platform Architecture
Review.md` Finding `A-1` in full, on branch
`feature/v0.12.0-desktop-composition-domain-vocabulary-hardening`.
Implements `ADR-0103` exactly, per `WP 12.0A`'s own design — refactor
only: zero public API changes, zero behavioural changes, zero new
Platform Services, zero new DI registrations, no `partial class`.
`MainWindow.cs` (1,556 → 544 lines; ~1,000 → ~370-line constructor)
decomposes into nine `Tempest.Desktop.Composition` collaborators
(`DesktopCompositionRoot`, `DesktopSessionState`,
`WorkspaceDockingComposer`, `RibbonObjectActionHandlers` — the
~450-line, ~29%-of-file per-discipline handler population, moved
verbatim — `WorkspaceViewCoordinator`, `UndoRedoCoordinator` — now
fully reactive via `IUndoRedoStack.Changed` — `MainMenuFactory`/
`QuickAccessToolbarFactory` — stateless static build functions —
`WorkspaceLayoutPresetCoordinator`). `EngineeringCockpit.cs` (1,398 →
575 lines) decomposes into six per-discipline read-model collaborators,
one per existing `Tempest.App.Workspace.{Discipline}` namespace;
genuinely cross-discipline aggregation and the Governance & Risk family
stay on the composition root, per `ADR-0103`'s own Composition-root
Responsibility #4. Two genuine construction-order cycles found and
resolved (`DocumentAreaView`/`CockpitView` needing
`WorkspaceViewCoordinator`'s own deferred delegates, which in turn need
them back) via the identical two-phase "construct, then attach"
sequencing the pre-decomposition source already used at the field
level, now one collaborator boundary out — not a new pattern. 12 new
characterization tests added before their own respective extractions (5
`EngineeringCockpit`, 7 `MainWindow` Menu System/Quick Access Toolbar),
both real, confirmed-by-direct-search coverage gaps. **Full Debug+Release
regression: 2,245/2,245 passing, 0 Warnings/0 Errors, both
configurations** (2,031 `Tempest.Core.Tests` + 214
`Tempest.Desktop.Tests`, up from `WP 12.3B`'s own 2,233 baseline — 12
net new tests, zero regressions). No new ADR; `ADR-0103` unmodified — no
factual correction was exposed by implementation. See
`docs/architecture/Desktop Composition Architecture.md`, `ADR-0103`,
`docs/academy/03 Work Packages/WP12.0B-desktop-composition-root-decomposition-implementation.md`,
`docs/releases/v0.12.0/WorkPackages.md`. **`WP 12.0A`'s own status
line, below this point, is this field's prior content — retained, not
deleted:** (`WP 12.0A` — Desktop Composition Root
Decomposition Architecture. `v0.12.0`'s own roadmap-predicted first Work
Package to complete (`WP11.0B Architecture Roadmap.md` §3), on branch
`feature/v0.12.0-desktop-composition-domain-vocabulary-hardening`
(re-checked-out from `main` after `WP 12.3A`/`WP 12.3B` merged — new
work continues on the feature branch, not on `main`). Realises
`WP11.0A Platform Architecture Review.md` Finding `A-1`
("Composition-root God Objects in the Desktop layer"), re-verified
directly, unchanged: `MainWindow.cs` (1,556 lines, a ~1,000-line
constructor) and `EngineeringCockpit.cs` (1,398 lines, a flat
six-Engineering-Discipline read-model). Designs `ADR-0103` —
"Composition Roots Own Collaborators": a general, platform-wide pattern
(explicitly not scoped to either motivating file) extending `ADR-0009`'s
own composition-root principle one layer down — full composition-root/
collaborator responsibilities, ownership/lifetime, construction/
dependency rules, and an explicit prohibition on ever DI-registering an
extracted collaborator (reusing `ADR-0013`'s own classification test
verbatim); rejects a `partial class` split (no structural boundary,
`ADR-0017`'s own precedent) and a declarative/reflective composition
framework (no genuine extensibility trigger, `ADR-0032`'s own precedent)
as alternatives. `docs/architecture/Desktop Composition Architecture.md`
records the pattern, with `MainWindow`/`EngineeringCockpit` as
motivating, explicitly non-binding illustrative evidence — the real,
final collaborator boundaries are `WP 12.0B`'s (not yet started) own
Implementation Report. **Architecture only — zero `src/`/`tests/` files
touched, confirmed directly; no code, no test run required or
performed.** See `docs/architecture/Desktop Composition Architecture.md`,
`ADR-0103`, `docs/academy/03 Work
Packages/WP12.0A-desktop-composition-root-decomposition-architecture.md`,
`docs/releases/v0.12.0/WorkPackages.md`. **`WP 12.3B`'s own status
line, below this point, is this field's prior content — retained, not
deleted:** (`WP 12.3B` — Fault Injection & Validation
Framework Implementation. `v0.12.0`'s own first completed Work Package
— a directly-commissioned pair, `WP 12.3A`/`WP 12.3B`, not named in
`WP11.0B Architecture Roadmap.md`'s own predicted `12.0`–`12.2` slots
(the identical pattern `WP 11.3A`/`B` and `WP 11.4A`/`B` each already
established for `v0.11.0`), on a new branch,
`feature/v0.12.0-desktop-composition-domain-vocabulary-hardening`, cut
from `main`. **Closes a genuine, previously-undisclosed defect**:
`Tempest.Samples.DuplicateNavigationSampleModule` — a deliberately-
always-failing module proving `ModuleLifecycleManager`'s per-module
isolation (ADR-0013) — was discovered and initialised by every real
`Tempest.App`/`Tempest.Desktop` launch (both compose through an
unrestricted `TempestHostBuilder()`, a genuine full-`AppDomain` scan),
permanently leaving one module `ModuleState.Failed` in real use;
confirmed directly against `ModuleLifecycleStabilityTests.cs`'s own
pre-existing special-case exclusion for exactly this module's Id. Fixed
via `ADR-0102`: the module moved to a new project, `Tempest.Validation`
(namespace `Tempest.Validation.FaultInjection`, renamed
`DuplicateNavigationModule`) that `Tempest.App`/`Tempest.Desktop` never
reference, *and* a new, default-excluded discovery-time marker
(`IFaultInjectionModule`, a defaulted `includeFaultInjectionModules`
parameter on `ReflectionFrameworkDiscoveryService`, a new
`ITempestHostBuilder.EnableFaultInjectionModules()` method) — project
isolation alone was assessed and found insufficient on its own, since
reflection-based Discovery scans the whole process's `AppDomain`, not
only directly-referenced assemblies. **The fix verified directly, not
merely asserted**: `ModuleLifecycleStabilityTests.cs`'s special-case
exclusion is deleted, not updated — a real `WorkspaceHost`, composed
through the identical path `Tempest.App` itself uses, now genuinely
reaches `Running` with zero modules `Failed`. Zero behavioural change
for any existing, unmodified caller (every new parameter defaults to
today's exact behaviour). Full regression re-verified directly, both
configurations, both before this Work Package's own documentation phase
and again as its closing gate: 2,233/2,233 passing (2,031
`Tempest.Core.Tests` + 202 `Tempest.Desktop.Tests`, net +5 new tests, 0
removed), 0 Warnings/0 Errors, Debug and Release both. No merge to
`main`, no tag, no release closure performed or sought by this Work
Package — `docs/releases/v0.12.0/WorkPackages.md`'s own remaining,
roadmap-predicted Work Packages (`WP 12.0A` through `WP 12.9.0`) remain
Not started. See `docs/architecture/Fault Injection & Validation
Architecture.md`, `ADR-0102`,
`docs/academy/03 Work Packages/WP12.3A-fault-injection-validation-framework-architecture.md`,
`WP12.3B-fault-injection-validation-framework-implementation.md`,
`docs/releases/v0.12.0/WorkPackages.md`. **`WP 11.4B`'s own status
line, below this point, is this field's prior content — retained, not
deleted:** (`WP 11.4B` — Merge to `main` & Release
Process Correction. `v0.11.0`'s own tenth and final Work Package.
**`main` is now the authoritative development baseline.** Before
merging, discovered — and, with explicit Product Owner approval,
corrected — that `v0.11.0`'s own tag repeated, verbatim, the `v0.10.0`
tag-position defect `WP 11.1B` itself disclosed and warned against: cut
from the feature branch (`fda4172`), never an ancestor of `main`.
Unlike `v0.10.0` (found after its branch had long closed, left as
permanent history), this was found *before* `v0.11.0`'s branch closed —
the distinction a new, narrowly-scoped governance exception (Governance
§7 item 4, amended this Work Package) now exists specifically to cover:
published release tags remain immutable *except* a documented
release-process defect (not a defect in the release's own content)
found and Product-Owner-approved for correction before the release
branch closes, with a Release Process Correction Report and full
traceability — `v0.10.0`'s own already-closed case is explicitly not
reopened. Corrective sequence: the git tag (never the commit, never any
release content) was deleted and recreated on the merge commit, reusing
its original annotation verbatim; `feature/v0.11.0-v1-architecture`
merged into `main` as a non-fast-forward merge commit (`4b1fb16`);
Build Gate/Test Gate re-verified **on `main` itself**, immediately
before tagging, genuinely satisfying Governance §7.3 as literally
written for the first time this release (2228/2228 both configurations,
0 Warnings/0 Errors). Independently re-verified, not assumed: the tag
now resolves to the merge commit exactly; the merge commit is
`main`'s own `HEAD`; the tag is a real ancestor of `main`; `Build &
Test`/`CI Gate` pass on real infrastructure for both the `main` push
and the corrected tag push (four consecutive real CI runs now show the
identical pattern); `release.yml` independently rebuilt, retested, and
republished a fresh, correctly-tagged, non-draft GitHub Release.
**One disclosed, unresolved residual**: deleting the original tag
orphaned its GitHub Release into a draft (`id 368605775`,
`untagged-9246469d05862713c1cc`) — still present, not deleted, outside
this Work Package's own explicitly authorised scope; the live release
(`id 368784327`) is unaffected and correct. Full account:
`docs/releases/v0.11.0/WP11.4B Release Process Correction Report.md`.
**`WP 11.4A`'s own status line, below this point, is this field's
prior content — retained, not deleted:** (`WP 11.4A` — Release
Engineering Corrections. `v0.11.0`'s own ninth Work Package — resolves the three
release-engineering defects `WP 11.9.0`'s own Release Publication
Report diagnosed when `v0.11.0` was tagged, pushed, and published: a
flaky test (`ObjectEditorViewTests.cs`, a fixed `Task.Delay` racing an
async UI update — replaced with a bounded, condition-driven poll, no
production/Domain/architecture change);
`scripts/governance-healthcheck.ps1`'s `-SummaryPath` resolution
(PowerShell's `Join-Path` doubling an already-absolute CI-runner path
into `RepoRoot` — fixed with an `IsPathRooted` guard, relative-path
behaviour and the genuine safety check both re-verified unchanged);
`.github/workflows/ci.yml`'s `governance-health-check` job's
tag-triggered checkout (`fetch-tags:
true` colliding with an unpinned ref — fixed by pinning `ref: ${{
github.sha }}`). None was a regression in product code; none was
discoverable before this release's own first real GitHub-hosted CI
execution. All three independently re-verified on real infrastructure
via two separate live runs (a branch push and a disposable, deleted-
after-use tag push): `Build & Test` (Debug/Release) and `CI Gate` both
now genuinely pass on real infrastructure for the first time in this
release's history. `Governance Health Check` still shows red on both
real runs — confirmed benign (a clean, correct non-zero exit from
genuine, pre-existing, already-tracked governance debt — Academy
Index/Documentation Register gaps — not a crash), explicitly outside
this Work Package's own three-item scope. Technical Debt Register:
`TD-44` moved Open → Resolved; `TD-46`–`TD-48` added and immediately
resolved (each distinct from the still-open `TD-42`/`TD-43` in the same
two files). See `docs/releases/v0.11.0/WP11.4A Release Engineering
Corrections.md` and `Release Publication Report.md`. **`WP 11.9.0`'s
own status line, below this point, is this field's prior content —
retained, not deleted:** (`WP 11.9.0` — `v0.11.0` Release
Preparation & Engineering Sign-Off. `v0.11.0`'s own eighth and closing
Work Package — executed as a six-discipline TempestOS Engineering
Programme (Chief Architect, Principal Engineer, Workflow Engineer, QA
Lead, Technical Author, Product Manager), each performing an
independent review, reconciled into one Programme Review. **Recommendation:
Accept with Observations.** Every hard gate independently re-verified
from a clean build, not carried forward from any prior claim: Debug/
Release both 0 Warnings/0 Errors; combined test suite 2228/2228 both
configurations (the 2266→2228 drop from `v0.10.0` exactly reconciled
against `WP 11.3B`'s own two deleted test files, not a regression);
`ADR-0101` honoured, dependency direction and all three named public
contracts unmodified; zero executable-code changes anywhere in the
release outside the disclosed `TempestShell` deletion. `WorkspaceShell`
and `Tempest.Desktop` both verified to launch and run cleanly; no
interactive, mouse-driven UI click-through was performed this Work
Package (no mouse-automation tool available this session) — disclosed
explicitly as a depth gap against `WP10.9A`'s own prior precedent for
`v0.10.0`, not silently narrowed to look equivalent. Five governance
registers found independently drifted stale relative to this release's
own seven prior Work Packages and corrected within this Work Package —
`Academy Register.md`, `Documentation Register.md`, `Release
Register.md`, `Technical Debt Register.md`, `Future Capability
Register.md` (the last carrying an outright factual error, not merely a
stale date: `FCR-0005` still read "Identified, not started" despite
`WP 11.2A` having shipped it — corrected). Four new technical debt items
registered (`TD-42`–`TD-45`): two genuine, previously-undisclosed
defects found in this release's own new release-engineering tooling by
independent QA re-verification (`scripts/new-release.ps1`'s `git tag`/
`git push` steps silently swallow a failure; `scripts/
governance-healthcheck.ps1`'s generic exception handler loses the
failing check's identity on malformed input); two already-disclosed,
still-open release-engineering gaps formally consolidated (CI never
executed on real GitHub-hosted infrastructure; branch protection
documented, not configured). Academy retrospective coverage found
absent for four of this release's own seven prior Work Packages —
assessed non-blocking per this project's own `v0.10.0`-era precedent
for a comparable finding, recommended as an immediate `v0.12.0`
fast-follow. `docs/releases/v0.11.0/Release Notes.md` created. **No git
commit or tag created by this Work Package** — per this project's own
standing discipline, tagging `v0.11.0` is a Product Approval action
sought explicitly, not assumed from a Work Package's own completion.
See `docs/releases/v0.11.0/WP11.9.0 Engineering Release Report.md`
(the release decision, full gate-by-gate evidence, all six discipline
reports, the reconciled Definition of Done) and `Release Notes.md` (the
narrative record). **`WP 11.3B`'s own status line, below this point, is
this field's prior content — retained, not deleted:** (`WP 11.3B` —
Presentation Strategy
Implementation. `v0.11.0`'s own seventh Work Package — executes
`WP 11.3A`'s own approved, low-risk recommendation (Stages 1–4 of its
five-stage roadmap; Stage 5 remains deferred). `TempestShell`/`IPage`/
`PlaceholderPage` retired as production code (`git rm`, history
preserved) — proven dead since `ADR-0068` (`WP 8.1A`, `v0.8.0`), zero
live references anywhere, confirmed by direct sweep before and after
removal. `ADR-0101` authored, formally classifying `Tempest.App`/
`WorkspaceShell` as TempestOS's Internal Engineering Harness, not a
shipped product — cites `ADR-0068`/`ADR-0092` directly, neither
modified. `README.md` and `Contributor Learning Path.md` corrected
(both had described an architecture roughly six releases stale, per
`WP11.3A` finding F2); `docs/architecture/Platform Service Map.md` and
`Shell & Composition Framework Architecture.md` corrected; three
governance registers (Namespace, Test, Architectural Dependency)
narrowly corrected, each disclosed as a targeted fix against an
already-independently-stale register, not a full re-derivation.
`.github/workflows/ci.yml`/`release.yml` now package `Tempest.Desktop`
and `Tempest.App` as two separate, clearly-labelled artifacts, never
bundled as co-equal deliverables (`WP11.3A` finding F4, corrected). The
unverifiable `WP 10.0B`-attributed comment `WP11.3A` found (finding F3)
is corrected, not repeated. **Verified directly, not assumed:**
`Tempest.Desktop` launched and ran cleanly (Host, all six disciplines,
every Desktop service, keyboard bindings, and its own default
navigation all fired with zero exception) until deliberately timed out;
`WorkspaceShell` ran a full start-to-clean-shutdown cycle via piped
stdin, exit code 0; full regression, both configurations, both **2,228
/ 2,228 passing**, 0 Warnings/0 Errors; `scripts/governance-healthcheck.ps1`
re-run post-change, ADR Register check now correctly reports 100/100
with no tool change required, no new Fail introduced. No behavioural
change to `Tempest.Desktop`; no architectural redesign; no change
outside `WP11.3A`'s own approved plan. See `docs/releases/v0.11.0/
WP11.3B Presentation Strategy Implementation.md`. **`WP 11.3A`'s own
status line, below this point, is this field's prior content —
retained, not deleted:** (`WP 11.3A` — Presentation Strategy Review
& Platform Consolidation. `v0.11.0`'s own sixth Work Package — the
Desktop & Console Presentation Strategy Decision (`WP11.0A` finding
`A-2`) finally commissioned, after being predicted for both `WP 11.2A`
and, implicitly, the slot after it, without landing in either. Review
only — no code, ADR, or architecture modified. Finds the question was
narrower than `WP11.0A` framed it: `Tempest.App` bundles a genuinely
shared Workspace domain layer (which `Tempest.Desktop` depends on) with
a console *presentation* layer `Tempest.Desktop` has zero code
dependency on; within that presentation layer, `TempestShell` has been
**provably unreachable for three releases** (`ADR-0068`, `WP 8.1A`,
`v0.8.0` — confirmed by direct grep, every reference outside its own
file is a doc-comment citation), while `WorkspaceShell` remains
`Tempest.App`'s real, live entry point today. A claim that `WP 10.0B`
formally decided "Desktop is primary, console is retained for
diagnostics/testing" exists only as a `Tempest.Desktop/Program.cs` code
comment — searched directly against all six `WP 10.0B` documents and the
rest of the repository, found nowhere else, reported as **Unverified**,
not assumed true or false. Separately, and independently of that
question: `README.md` — the repository's own front door — was found to
describe an architecture roughly six releases stale (still calling
`Tempest.App` "the legacy bootstrap/project-management app... does not
yet run through `TempestHost`," omitting `Tempest.Desktop` entirely from
its own architecture diagram and its only run instruction), and
`Contributor Learning Path.md` shows the identical pattern. `ci.yml`/
`release.yml` (this project's own `WP 11.1A`/`WP 11.1B`) were found to
package `Tempest.App` and `Tempest.Desktop` as symmetric, co-equal
release artifacts, contradicting the informal Desktop-primary
positioning. **Recommendation:** retire `TempestShell` (zero risk, dead
code); formally ratify `Tempest.App`/`WorkspaceShell` as an Internal
Engineering Harness (not a shipped product) via a new ADR; correct
`README.md`/`Contributor Learning Path.md`/release packaging to match.
A five-stage, minimum-disruption implementation roadmap is proposed for
a future Work Package — none of it executed here. See `docs/releases/
v0.11.0/WP11.3A Presentation Strategy Review.md`. **`WP 11.2A`'s own
status line, below this point, is this field's prior content — retained,
not deleted:** (`WP 11.2A` — Governance Health-Check
Automation. `v0.11.0`'s own fifth Work Package — delivers the first
Governance Health-Check Tool identified as `FCR-0005`, reprioritised
into this release by `WP11.0B Architecture Roadmap.md` §4 after
`WP 11.0A`'s own Governance Index audit became that finding's seventh
independent recurrence. `scripts/governance-healthcheck.ps1` — a
standalone, read-only PowerShell script with no production-code
dependency — runs eight checks (ADR Register vs. `docs/adr/`, Academy
Index vs. Academy articles, Release Register vs. git tags, Documentation
Register path references, `PROJECT_STATUS.md` references, `VERSION` vs.
planned release folder, release-folder mandatory documentation,
Governance Index orphans), reporting Pass/Warn/Fail per check and
exiting non-zero on any Fail. Three genuine defects found and fixed in
the tool itself during development (a multi-line path-extraction crash,
a git-unavailable crash, a PowerShell single-item-count sharp edge),
each found by actually running the tool, not by inspection. The fixed
tool's first live run against this repository found two genuine, real,
previously-undisclosed governance findings — `Academy Index.md`'s "Work
Package Walkthroughs" section silently stops at `WP 7.3A`, missing
roughly fifty real retrospectives since; and `docs/roadmap/`,
`docs/diagrams/`, `docs/releases/v0.2.0.md` (then `docs/releases/v0.2.0`,
no extension, misnamed and tracked, not an untracked empty directory —
that part of this finding corrected `WP 12.9.1`), and `src/Plugins/` are
described as existing but are not tracked by git at all (git cannot
track an empty directory) — both disclosed in full, neither fixed within
this Work Package's own scope. Wired into `.github/workflows/ci.yml` as
a new `governance-health-check` job, running after the build/test matrix
and publishing its report to every run's Job Summary — deliberately
**not** yet part of the required `CI Gate` job, since doing so today
would fail every future push on a pre-existing backlog this Work Package
did not clear; named explicitly as the natural next step once that
backlog closes. Failure-detection verified against an isolated, wholly
temporary fixture (never the real repository) via a new `-RepoRoot`
parameter. No production code, ADR, or architecture modified; no GitHub
repository setting configured. See `docs/releases/v0.11.0/WP11.2A
Governance Health-Check Tool.md`. **`WP 11.1B`'s own status line, below
this point, is this field's prior content — retained, not deleted:**
(`WP 11.1B` — Branch Protection &
Engineering Workflow Hardening. `v0.11.0`'s own fourth Work Package —
defines the complete engineering workflow surrounding `WP 11.1A`'s CI
pipeline: branching strategy, pull request expectations, merge and
release requirements, required CI status checks (recommended for branch
protection, not configured — `.github/CODEOWNERS`, `.github/
pull_request_template.md`, `.github/ISSUE_TEMPLATE/` added), the
version-tagging workflow (a new `.github/workflows/release.yml`
independently re-verifies and publishes a durable GitHub Release on
every tag), a first-ever hotfix workflow and rollback procedure, and a
release-candidate process. Two genuine, pre-existing defects found and
fixed in place: `scripts/new-release.ps1`'s release-notes check read a
path convention abandoned since `v0.6.0`, and lacked the
`-p:TreatWarningsAsErrors=true` parity `WP 11.1A`'s own CI pipeline
already established — both corrected. One genuine, previously-
undisclosed governance deviation found and disclosed, not silently
fixed: the `v0.10.0` tag itself points to the feature branch's own
pre-merge commit, not to `main`, contradicting Engineering Governance §7
as literally written — the tag is not moved (§7.4), the rule is now
stated explicitly in the new workflow documents instead. Engineering
Governance §2 and §7 updated with pointers to the new standard
(`05-release-engineering.md`). No production code, ADR, or architecture
modified; no GitHub repository setting configured (recommendations
documented for a repository administrator, per this Work Package's own
constraint). See `docs/releases/v0.11.0/WP11.1B Engineering
Workflow.md`. **`WP 11.1A`'s own status line, below this point, is this
field's prior content — retained, not deleted:** (`WP 11.1A` — Continuous Integration & Build
Verification. `v0.11.0`'s own third Work Package — TempestOS's first CI
pipeline, `.github/workflows/ci.yml`, closing `WP 11.0A` finding `R-1`
(no build/test automation existed; every "0 Warnings, N/N tests passing"
claim was self-reported, never machine-verified). Builds Debug and
Release (both with warnings promoted to errors, verified safe against
the current tree before being adopted) and runs the complete test suite
against both, on every push, pull request, and manual dispatch, with
build/test summaries and downloadable artifacts (build logs, TRX
results, and the Release build output). Local reproduction of every
command the workflow issues confirmed 0 Warnings/0 Errors in both
configurations and 2,266/2,266 tests passing (2,064 `Tempest.Core.Tests`
+ 202 `Tempest.Desktop.Tests`, the latter real Avalonia headless UI, not
mocked) — matching this project's own most recent retrospective counts
exactly. Engineering Governance §2 (Build Gate, Test Gate) updated to
record machine-verification from this Work Package onward. Actual
execution on a GitHub-hosted runner is disclosed as **Unknown, not
Verified** — no GitHub Actions runner was available in this environment
and pushing the branch was judged outside this Work Package's own
"await review" scope; every command was instead reproduced directly,
locally, with the exact flags the workflow uses. No production code,
ADR, or architecture modified. See `docs/releases/v0.11.0/WP11.1A
Implementation Report.md` for full detail. **`WP 11.0B`'s own status
line, below this point, is this field's prior content — retained, not
deleted:** (`WP 11.0B` — v1.0 Architecture Roadmap &
Release Planning. `v0.11.0`'s own second Work Package — a planning-only
exercise, no code/ADR/architecture modified — that categorises
`WP 11.0A`'s ten findings into Must Complete Before v1.0 / Strongly
Recommended Before v1.0 / Post-v1.0 Technical Debt / Future Capability,
scopes the resulting Work Packages in this project's own WBS format
(`WP 11.1A`–`WP 11.2A` this release; `WP 12.0A`–`WP 12.2A` next; a
conditional `v0.13.0`; `WP RC.0A` closing `v1.0.0`), and reconciles that
scope against `WP7.2A Recommended Programme.md`'s own previously-deferred
"Programme F" rather than duplicating it — one reprioritisation
recommended (`FCR-0005`, now independently found a seventh time), no
others. See `docs/releases/v0.11.0/WP11.0B Architecture Roadmap.md` for
the complete roadmap. Preceded, same release, by `WP 11.0A` (Platform
Architecture & Code Quality Review — an independent review of the
complete codebase as of `v0.10.0`; ten findings, zero Critical, zero ADR
violations found in the areas sampled; no code modified). Both Work
Packages run on `feature/v0.11.0-v1-architecture`, cut from `main` at the
`v0.10.0` tag. **`v0.10.0`'s own status line, below this point, is this
field's prior content — retained, not deleted, per this project's own
convention, and still the accurate record of the currently-shipped
release.** (`WP 10.8A` — Desktop Feature Completion & Existing Capability
Exposure. `v0.10.0`'s fifteenth Work Package by completion order — a
direct, narrowly-scoped re-audit of `WP 10.7A`'s own seven priority items
against the real, running application. Five were confirmed already
genuinely implemented and intact; two were genuinely incomplete and
closed this Work Package (honest Ribbon fallback messaging — the
fallback previously claimed a genuinely-unwired command was reachable via
the Command Palette or Project Explorer context menu, confirmed false for
every command reaching that branch, corrected to name no false
alternative; real Property Inspector Validation — `EngineeringDomainContext`
threaded in as a third optional constructor parameter, reusing
`ObjectEditorView`'s own `IValidatable` read and `BuildSeverityRow` helper
directly, one disclosed exception for Requirements, `TD-41`, a second
consumer of an already-known gap); one further genuinely-unwired existing
command was found and wired alongside them (Manufacturing's "Record
Inspection Result," reusing the identical `RecordVerificationResultCommand`
the Object Editor's own Verification section already dispatches). Zero
Command Framework or Workspace-contract changes; zero new commands.
Combined test suite: 2266/2266 passing, Debug and Release both clean.)

This is the primary status dashboard for TempestOS. Read this first for
"where does the project stand right now" — for "why is it built this
way," read `docs/releases/FOUNDATION.md`; for "how do I get productive,"
read `docs/academy/Contributor Learning Path.md`.

---

## Current Repository Phase

**Developer Experience — complete and released as `v0.5.0`.** The
Foundation phase is complete and closed — Platform Formation, Academy
Formation, and Governance Formation are all done (see `docs/releases/
Platform Foundation Completion Report.md`), and `v0.4.0` ("Platform
Foundation") shipped exactly that scope. TempestOS then built *on* the
foundation — Navigation, a Command Framework, Diagnostics, and Developer
Experience tooling itself — through the **Developer Experience** phase.
`WP 5.0A` through `WP 5.0D` were this phase's first four completed Work
Packages — Navigation and the Shell are both fully implemented, and
`Tempest.App` now runs the real platform for the first time in this
project's history. `WP 5.0S` (Platform Security Baseline Audit) followed
as a dedicated, formal engineering audit — not a feature Work Package —
establishing the v0.5.0 Security Baseline every future Work Package's
Definition of Done is checked against. `WP 5.1A`/`WP 5.1B` (Command
Framework) followed — `ICommand`'s own contract (`WP 4.0`) now has a real
handler contract and dispatcher. `WP 5.2` (Diagnostics Improvements)
followed — a composite `ILogSink` closes a long-named debt (`TD-02`), and
a new `IDiagnosticsProvider` gives any DI-resolving consumer a read-only
view of the Host's own current lifecycle state. `WP 5.3` (Developer
Experience Improvements) closed the phase out — a `dotnet new` module
template, and a previously-only-documented Discovery pitfall now closed
with a clear, actionable error message. `WP 5.4` (v0.5.0 Release
Candidate & Engineering Sign-Off) then verified the entire release
directly — every ADR, every Work Package, every governance register —
before Product Approval authorised the release itself. **`v0.5.0` is now
released.** TempestOS is now in the **Platform Services** phase
(`v0.6.0`). The complete architecture package (`docs/releases/v0.6.0/
Release Architecture.md` and seven companion documents) and Contract
Review package (`Platform Service Contracts.md` and four companion
documents) were both produced and approved ahead of any implementation.
`WP 6.1` (Permissions & Identity), `WP 6.4` (Settings Framework), `WP
6.5` (Audit Framework), `WP 6.2` (Notification Framework), `WP 6.0`
(Reporting Framework), `WP 6.3` (REST API), `WP 6.7` (Export/Import),
and now `WP 6.6` (Licensing Framework) are all implemented — every
Work Package that ships real code this release is now complete, with
only `WP 6.8` (Platform Services Integration Review) remaining. `WP
6.0` and `WP 6.3` remain the only two of these eight to actually match
their own nominal position in `WorkPackages.md`'s own numbering, the
other six having each been implemented ahead of their own nominal
numeric order, per `Platform Service Implementation Order.md`'s own
explicit recommendation. `WP 6.5` reuses the Persistence abstraction
`WP 6.4` established, exactly as that recommendation anticipated,
rather than introducing a second storage mechanism. `WP 6.2` is built
on top of the existing Event Bus's own proven dispatch model, exactly
as `Required ADRs.md` anticipated for `ADR-0046`, rather than a second,
parallel publish/subscribe implementation. `WP 6.0` is explicitly
orthogonal to `WP 6.7` (Export/Import), per `ADR-0040`, and
demonstrates real, working integration with four already-completed
platform services (Identity, Settings, Audit, Notifications) entirely
at its own sample module's calling layer, never inside
`IReportingService` itself. `WP 6.3` adopts ASP.NET Core/Kestrel for
HTTP hosting (`ADR-0049`) — this platform's first substantial
dependency on a pre-built framework component beyond the bare .NET SDK
— confined entirely to one hosted-service type, and resolves this
platform's first genuinely concurrent, per-request scenario without
touching `CurrentPrincipalAccessor`'s own already-shipped ambient
design (`ADR-0052`), a decision verified empirically, not merely
reasoned about. `WP 6.7` completes the orthogonality `ADR-0040`
anticipated (`ADR-0051`): Export/Import is a user-facing, `Stream`-based,
portable-artifact I/O layer, explicitly distinct from the internal
Persistence abstraction, round-tripping two Settings values as a
single, multi-source artifact through a Kind-routed `IImportable`
registration mechanism dual-registered exactly as `ADR-0044`'s own
`CurrentPrincipalAccessor` precedent. `WP 6.6` resolves this release's
own last open architectural question (`Risk Register.md`'s own `R5`):
license validation is a pre-container, Host-startup gate, Host-fatal
for a broken license file but not for a missing one, which resolves to
a valid, unrestricted-but-uncapable default (`ADR-0050`) — proven not
to regress any of the 24 pre-existing tests that build a real
`TempestHost`. `WP 6.8` closes the phase: a certification review, not
an implementation Work Package, confirming the platform's own
architecture, integration, testing, documentation, and governance all
hold up under direct, independent re-verification, and recommending
**CERTIFIED WITH ACCEPTED TECHNICAL DEBT**. Product Approval was then
granted and `v0.6.0` was released in full: merged to `main`
(non-fast-forward, `99ed285`), tagged `v0.6.0`, and pushed. TempestOS
then completed the **Engineering Foundation** phase (`v0.7.0`) in full —
thirteen Work Packages (`WP 7.0A`–`WP 7.4.0`) across two sequential
programmes, closed by `WP 7.4.0`'s own release-readiness review
recommending **APPROVED**. Product Approval was granted and `v0.7.0`
was released in full: merged to `main` (non-fast-forward, `61fb2db`),
tagged `v0.7.0`, and pushed. TempestOS then completed the **Engineering
Workspace** phase (`v0.8.0`) in full — ten Work Packages
(`WP 8.0A`–`WP 8.9.0`) closed by `WP 8.9.0`'s own release-readiness
review recommending **APPROVED**. Product Approval was granted and
`v0.8.0` was released in full: merged to `main` (non-fast-forward,
`28e41e8`), tagged `v0.8.0`, and pushed. TempestOS then completed the
**Mechanical Foundation** phase (`v0.9.0`) in full — ten Work Packages
(`WP 9.0A`–`WP 9.9.0` Second Pass, plus reconciliation `WP 9.8B`, plus
release-execution `WP 9.9.1`) closed by `WP 9.9.0`'s own two
independent release-readiness passes, both recommending **APPROVED**.
Product Approval was granted and `v0.9.0` was released in full: a
direct commit to `main` (no merge commit — no feature branch ever
existed in the real repository, despite this document's own prior
historical narrative describing one; disclosed repeatedly through the
release, most recently by `WP 9.9.1`, never silently fabricated),
`9f258f1`, tagged `v0.9.0`, and pushed — confirmed by direct `git
fetch` showing `origin/main` matching local `HEAD` exactly. TempestOS
is now in the **User Experience & Desktop Application** phase
(`v0.10.0`), Programme 10, on a real branch this time —
`feature/v0.10.0-user-experience`, confirmed to actually exist by
`git branch -a`, the first Programme 10 feature branch in this
project's history where the branch narrated here and the branch `git`
itself reports are the same branch from the moment it was created. See
Current Work Package, below.

## Current Development Branch

**`main`** — `feature/v0.11.0-v1-architecture` (`WP 11.0A` through
`WP 11.4B`, ten Work Packages) has been merged into `main`
(non-fast-forward merge commit `4b1fb16`, `WP 11.4B`), tagged
`v0.11.0` (recreated on the merge commit after a Product-Owner-approved,
one-time release-process correction — see `WP11.4B Release Process
Correction Report.md`), pushed, and is retained. `main` is the
authoritative development baseline going forward, until `v0.12.0`'s own
feature branch is cut.

**`feature/v0.11.0-v1-architecture`'s own record, retained below,
unchanged — the branch this document narrates as `v0.11.0`'s development
branch is no longer the *current* one, but every word below remained
true of it at the time it was written:** created and checked out
directly by `WP 11.0B`'s own controlling instruction, cut from `main` at
the `v0.10.0` tag. `WP 11.0A` (Platform Architecture & Code Quality
Review) and `WP 11.0B` (v1.0 Architecture Roadmap & Release Planning)
are this branch's first two Work Packages — both review/planning-only,
no production code, ADR, or architecture modified by either. See
`docs/releases/v0.11.0/WorkPackages.md` and `docs/releases/v0.11.0/
WP11.0B Architecture Roadmap.md`.

**`feature/v0.10.0-user-experience`'s own record, retained below,
unchanged — the branch this document narrates as `v0.10.0`'s development
branch is no longer the *current* one, but every word below remained true
of it at the time it was written:** created and checked out directly
by `WP 10.0A`, cut from `main` at the point `v0.9.0` was pushed
(`9f64700`). **Confirmed to actually exist** by `git branch -a` — the
first Programme 10 feature branch in this project's history where that
is true from creation, unlike every `v0.9.0`-era branch name this
document narrated (see below). `WP 10.0A` (User Experience
Architecture) is this branch's first Work Package.

**`v0.9.0`'s own branch history, closed out plainly, not silently
tidied away:** every `WP 9.0A`–`WP 9.5A`/`WP 9.8B`/`WP 9.9.0`
(both passes) narrative in this document described work as happening
on `feature/v0.9.0-calculations-workspace`; `git branch -a` never once
showed that branch actually existing, confirmed repeatedly through the
release's own close (`WP 9.5A`, `WP 9.9.0` both passes, `WP 9.8B`,
`WP 9.9.1`). The release itself closed accordingly: `WP 9.9.1` created
a single direct commit to `main` (`9f258f1`, no merge commit, since
there was never a real branch to merge from), tagged `v0.9.0`, and the
Product Owner pushed both — confirmed by `git fetch` showing
`origin/main` matching local `HEAD` exactly, plus one further pushed
documentation commit (`9f64700`). This is recorded here as the release
closing honestly on the terms `git` itself actually offered, not on
the terms this document's own prior narrative assumed it would.

`feature/v0.8.0-engineering-workspace` (`WP 8.0A` through `WP 8.9.0`,
all ten Work Packages of the Engineering Workspace phase) has been
merged into `main` (non-fast-forward, `28e41e8`), tagged `v0.8.0`,
pushed, and is retained; `feature/v0.7.0-engineering-foundation`
(`WP 7.0A` through `WP 7.4.0`, all thirteen Work Packages of the
Engineering Foundation phase) has
been merged into `main` (non-fast-forward, `61fb2db`), tagged `v0.7.0`,
pushed, and is retained; `feature/v0.6.0-platform-services`
(`WP 6.0` through `WP 6.8`) has been merged into `main`
(non-fast-forward, `99ed285`) and is retained; `feature/v0.5.0-developer-experience`
(`WP 5.0A` through `WP 5.4`) remains merged and retained as well —
unmerged and merged feature branches are both never deleted per this
project's own convention.

## Current Release

**`v0.11.0` ("Release Engineering & Architecture Governance") is
released, published, and merged to `main`** — all ten Work Packages
(`WP 11.0A` through `WP 11.4B`) complete: TempestOS's first CI
pipeline, its full surrounding engineering workflow, its first
automated governance health check, and the Desktop & Console
Presentation Strategy Review **and its own approved implementation**
all now exist — `Tempest.Desktop` is formally TempestOS's shipped
application, `Tempest.App`/`WorkspaceShell` is formally its Internal
Engineering Harness (`ADR-0101`), and `TempestShell` is retired. Tag
`v0.11.0` resolves to `main`'s own merge commit (`4b1fb16`), corrected
under a one-time, Product-Owner-approved governance exception after
being found to repeat `v0.10.0`'s own disclosed tag-position defect —
see `WP11.4B Release Process Correction Report.md`. See Current Work
Package and `docs/releases/v0.11.0/WP11.0B Architecture Roadmap.md` for
the full scope and sequence through `v1.0.0`.
**`v0.10.0`** ("User Experience & Desktop Application") remains the most
recently shipped release, and is the release this document's own
`WP 10.9A` (Release Candidate & Engineering Sign-Off) has independently,
from-source re-verified and released —
2026-08-11, tag `v0.10.0`, commit reference recorded in `WP10.9A
Engineering Release Report.md` §Git alongside the tag's own message.
Root `VERSION` correctly reads `0.10.0`. Full engineering justification,
every gate's own evidence, and the release decision itself: `docs/releases/v0.10.0/WP10.9A
Engineering Release Report.md`; the release's own narrative record:
`docs/releases/v0.10.0/Release Notes.md`. **v0.9.0** ("Mechanical
Foundation") is the release before `v0.10.0` — released 2026-08-07,
tagged `v0.9.0` (direct commit `9f258f1`, no merge commit), recommended
**APPROVED** by both of `WP 9.9.0`'s own independent release-readiness
passes, prepared for execution by `WP 9.9.1`, and pushed by the Product
Owner. **v0.8.0** ("Engineering Workspace") before that (merged
`28e41e8`, tagged `v0.8.0`, **APPROVED**); `v0.7.0` ("Engineering
Foundation") before that (tagged `v0.7.0`, `61fb2db`, **APPROVED**);
`v0.6.0` ("Platform Services") before that (tagged `v0.6.0`, `99ed285`,
`CERTIFIED WITH ACCEPTED TECHNICAL DEBT`); `v0.5.0` ("Developer
Experience") before that; `v0.4.0` ("Platform Foundation") before that.

## Current Work Package

**`WP 11.3B` — Presentation Strategy Implementation.** `v0.11.0`'s own
seventh Work Package. Executes `WP 11.3A`'s own approved recommendation
(Stages 1–4 of its five-stage roadmap): `TempestShell`/`IPage`/
`PlaceholderPage` retired as production code (`git rm`, history
preserved — proven dead since `ADR-0068`, `WP 8.1A`, `v0.8.0`);
`ADR-0101` authored, formally classifying `Tempest.App`/`WorkspaceShell`
as TempestOS's Internal Engineering Harness (cites `ADR-0068`/`ADR-0092`,
neither modified); `README.md`/`Contributor Learning Path.md`/
`Platform Service Map.md`/`Shell & Composition Framework Architecture.md`
corrected; three governance registers narrowly corrected (each already
independently stale for unrelated reasons, disclosed as such, not fully
re-derived); `ci.yml`/`release.yml` now package `Tempest.Desktop` and
`Tempest.App` as two separate, clearly-labelled artifacts, never
bundled. Verified directly: `Tempest.Desktop` launched and ran cleanly
(Host, all six disciplines, every Desktop service, its own default
navigation all fired, zero exception) until deliberately timed out;
`WorkspaceShell` ran a full start-to-clean-shutdown cycle via piped
stdin, exit code 0; full regression both configurations, both 2,228/2,228
passing, 0 Warnings/0 Errors; `scripts/governance-healthcheck.ps1`
re-run post-change, ADR Register check now correctly reports 100/100. No
behavioural change to `Tempest.Desktop`; no architectural redesign; no
change outside `WP11.3A`'s own approved plan. See `docs/releases/
v0.11.0/WP11.3B Presentation Strategy Implementation.md`.

**Preceded, same release, by `WP 11.3A` — Presentation Strategy Review &
Platform Consolidation.**
`v0.11.0`'s own sixth Work Package. A complete, evidence-based
engineering review of the presentation layer (`Tempest.App`,
`Tempest.Desktop`, startup flow, dependency graph, duplication,
documentation assumptions, release-process implications) — review only,
no code/ADR/architecture modified, no repository restructuring.
Reframes `WP11.0A` finding `A-2`: the real duplication is confined to
the console-rendering slice of `Tempest.App` (`WorkspaceShell`,
`TempestShell`), not the shared Workspace domain layer, which
`Tempest.Desktop` genuinely depends on and which is provably
duplication-free (`EngineeringWorkspaceComposer`). `TempestShell` found
provably dead — unreachable from any running entry point since
`ADR-0068` (`WP 8.1A`, `v0.8.0`), three releases ago, zero live
references anywhere. A `Tempest.Desktop/Program.cs` comment's claim that
`WP 10.0B` formally decided "Desktop primary, console diagnostic" could
not be found in any of `WP 10.0B`'s own six documents — reported
Unverified, not assumed. Independently: `README.md` and `Contributor
Learning Path.md` found describing an architecture roughly six releases
stale, omitting `Tempest.Desktop` entirely; `ci.yml`/`release.yml`
(this project's own `WP 11.1A`/`WP 11.1B`) found packaging both
presentation layers as symmetric release artifacts. Recommends (for a
future Work Package, not executed here): retire `TempestShell`; ratify
`Tempest.App`/`WorkspaceShell` as an Internal Engineering Harness via a
new ADR; correct the documentation and release-packaging
inconsistencies. See `docs/releases/v0.11.0/WP11.3A Presentation
Strategy Review.md`.

**Preceded, same release, by `WP 11.2A` — Governance Health-Check
Automation.** `v0.11.0`'s own
fifth Work Package. Delivers `scripts/governance-healthcheck.ps1`, the
first Governance Health-Check Tool (`FCR-0005`) — a standalone,
read-only PowerShell script with no production-code dependency, running
eight checks (ADR Register, Academy Index, Release Register,
Documentation Register, `PROJECT_STATUS.md`, `VERSION`, release-folder
mandatory documentation, Governance Index orphans) and exiting non-zero
on any Fail. Three genuine defects found and fixed in the tool itself
during its own development; its fixed first live run then found two
genuine, real, previously-undisclosed governance findings (`Academy
Index.md`'s "Work Package Walkthroughs" section stops at `WP 7.3A`,
missing ~50 real retrospectives since; `docs/roadmap/`, `docs/diagrams/`,
`docs/releases/v0.2.0.md` (then `docs/releases/v0.2.0`, tracked but
misnamed, not an untracked empty directory — corrected `WP 12.9.1`), and
`src/Plugins/` are described as existing but are not tracked by git — an
empty-directory limitation, not a typo)
— both disclosed, neither fixed within this Work Package's own scope.
Wired into `.github/workflows/ci.yml` as a new job running after the
build/test matrix; deliberately not yet part of the required `CI Gate`
job, since the disclosed backlog above would otherwise fail every future
push. No production code, ADR, or architecture modified; no GitHub
repository setting configured. See `docs/releases/v0.11.0/WP11.2A
Governance Health-Check Tool.md`.

**Preceded, same release, by `WP 11.1B` — Branch Protection &
Engineering Workflow Hardening.** `v0.11.0`'s own fourth Work Package. Defines the complete engineering
workflow surrounding `WP 11.1A`'s CI pipeline: branching strategy
(one `feature/vX.Y.0-*` branch per release, confirmed against real
history), pull request expectations (`.github/pull_request_template.md`,
`.github/CODEOWNERS` — every merge into `main` now goes through a PR,
formalising what was a direct merge/push through `v0.10.0`), merge and
release requirements, required CI status checks (`CI Gate` — documented
for a repository administrator to enable via branch protection, not
configured directly, per this Work Package's own constraint), the
version-tagging workflow (a new `.github/workflows/release.yml`
independently re-verifies Build/Test Gate against the tagged commit and
publishes a durable GitHub Release with the build attached), a
first-ever hotfix workflow and rollback procedure, and a
release-candidate process (`-rc.N` tags, pre-release GitHub Releases).
Two genuine pre-existing defects found and fixed in place:
`scripts/new-release.ps1`'s release-notes path check was stale (abandoned
since `v0.6.0`) and lacked `WP 11.1A`'s own `-p:TreatWarningsAsErrors=true`
parity — both corrected. One genuine governance deviation found and
disclosed, not silently fixed: the `v0.10.0` tag points to the feature
branch's own pre-merge commit, not to `main`, contradicting Engineering
Governance §7 as written — the tag itself was not moved (§7.4 forbids
it); the rule is now stated explicitly in the new workflow documents.
No production code, ADR, or architecture modified; no GitHub repository
setting configured. See `docs/releases/v0.11.0/WP11.1B Engineering
Workflow.md`.

**Preceded, same release, by `WP 11.1A` — Continuous Integration & Build
Verification.** `v0.11.0`'s own third Work Package. Implements TempestOS's first CI
pipeline (`.github/workflows/ci.yml`, GitHub Actions), closing
`WP 11.0A` finding `R-1`. Builds Debug and Release (warnings promoted to
errors at the CI step only — `Directory.Build.props` unchanged) and runs
the complete test suite against both, on every push, pull request, and
manual dispatch; publishes build/test summaries and downloadable
artifacts. Verified locally, command-for-command, before being finalised:
0 Warnings/0 Errors in both configurations, 2,266/2,266 tests passing.
Actual GitHub-hosted execution is disclosed as unverified (no runner
available in this environment; pushing was judged outside this Work
Package's own scope) — named plainly, not assumed. Engineering
Governance §2 updated to record machine-verification of the Build/Test
Gates from this Work Package onward. No production code, ADR, or
architecture modified. Full detail: `docs/releases/v0.11.0/WP11.1A
Implementation Report.md`.

**Preceded, same release, by `WP 11.0B` — v1.0 Architecture Roadmap &
Release Planning.**
`v0.11.0`'s own second Work Package, on `feature/v0.11.0-v1-architecture`
(cut from `main` at the `v0.10.0` tag). Planning only — no production
code, ADR, or architecture modified. Takes `WP 11.0A`'s ten findings
(`A-1`–`A-9`, `R-1`; zero Critical) and: categorises each into Must
Complete Before v1.0 / Strongly Recommended Before v1.0 / Post-v1.0
Technical Debt / Future Capability; scopes every Must/Strongly-
Recommended item into a Work Package in this project's own WBS format,
with effort, engineering risk, architectural impact, documentation
impact, testing impact, and dependencies for each; produces a recommended
release sequence (`v0.11.0` → `v0.12.0` → [conditional `v0.13.0`] →
`v1.0.0`); reconciles the new scope against `WP7.2A Recommended
Programme.md`'s own previously-deferred "Programme F" rather than
duplicating it (one reprioritisation recommended — `FCR-0005`, Governance
Register Health-Check Tooling, independently found stale a **seventh**
time by `WP 11.0A`'s own Governance Index audit; every other Programme F
item, `FCR-0001`/`FCR-0003`/`FCR-0004`, correctly stays gated on its own
undemonstrated trigger, per Security Principle 7); and recommends a
realistic `v1.0` definition (a single-user/trusted-team, locally-trusted
desktop engineering platform covering the six disciplines already shipped
through `v0.10.0`, with third-party plugins and non-local REST deployment
explicitly out of scope unless the Product Owner commits otherwise). Full
detail: `docs/releases/v0.11.0/WP11.0B Architecture Roadmap.md`.

**Preceded, same release, by `WP 11.0A` — Platform Architecture & Code
Quality Review.** An independent review of the complete codebase as of
the `v0.10.0` tag — layering, assembly boundaries, dependency direction
(empirically verified, not merely read from documentation), SOLID
adherence, ADR compliance (spot-checked, zero violations found in the
areas sampled), the Command/Event/Runtime Host/Plugin/Desktop
architectures, code and test quality, technical debt, and release
readiness for `v1.0`. Ten findings (zero Critical; three High — no
CI/CD pipeline, Desktop composition-root God Objects, an undecided dual-
presentation-stack strategy; four Medium; two Low), each with evidence,
impact, recommendation, and a before-v1.0-or-defer disposition. No code
modified. Full detail: `docs/releases/v0.11.0/WP11.0A Platform
Architecture Review.md`.

---

**`v0.10.0`'s own prior Current Work Package content, below this point,
is retained unchanged — the accurate record of that release's own last
Work Package, not this release's current one:**

**`WP 10.8A` — Desktop Feature Completion & Existing Capability
Exposure.** `v0.10.0`'s fifteenth Work Package — a direct, narrowly-
scoped follow-through of `WP 10.7A`, re-auditing its seven named
priority items against the real, running application. Five (Cockpit
Risks/Review KPIs, Object Editor BOM/Record Result/Attachments) were
confirmed already genuinely implemented and intact. Two were genuinely
incomplete and closed this Work Package, plus one further gap this
Work Package's own audit found and closed alongside them — all reusing
only already-existing, already-registered commands and services; zero
new `ICommand`/`IWorkspaceCommand`, zero new Domain contract, zero new
member on `IWorkspaceManager`/`ICommandDispatcher`/`ICommandRegistry`.

**Honest Ribbon fallback messaging.** `RibbonView`'s own final fallback
message, reached by every genuinely-unwired verb (including Copy),
previously claimed a command was reachable via "the Command Palette
(Ctrl+K)" or "Project Explorer's own context menu" — confirmed false
for every command still reaching that branch: `CommandDescriptor.
CreateDefault` is `null` on every real command this platform has ever
registered (`WP 10.3B`'s own finding, re-confirmed still true), so the
Palette cannot invoke any real discipline command by Id at all, and the
Project Explorer's own context menu offers only Open/Rename/Delete/
Favourite. Corrected to name no false alternative.

**Real Property Inspector Validation.** `PropertyInspectorView`'s own
Validation section previously showed a fixed "No automated validation
is available for this object type yet" message unconditionally, since
this class only ever saw `PropertyFacet`s, never the real object's own
`IValidatable`. `EngineeringDomainContext` is now a third, optional,
additive constructor parameter (mirroring exactly how `ObjectEditorView`
gained `ICommandDispatcher` the Work Package prior); when present, the
section performs the identical `IValidatable.ValidateAsync()` read
`ObjectEditorView`'s own Validation section already does, reusing its
`BuildSeverityRow` helper directly (made `internal`, not duplicated).
One disclosed, honest exception: a Requirement never resolves through
`EngineeringDomainContext.Repository` (`TD-41`, a second, previously-
undisclosed consumer of that same pre-existing gap), so this section
reports "Real validation is not available for this object here" for
that one discipline rather than fabricating a result.

**One further existing command found unwired and wired: Manufacturing's
"Record Inspection Result."** Dispatches the identical, already-
registered `RecordVerificationResultCommand` the Object Editor's own
Verification section already uses — a disclosed cross-Work-Package
reuse, not a new command.

**Verification**: `src/TempestOS.slnx`, Debug and Release, both 0
Warnings/0 Errors. Combined test suite: **2266/2266 passing**. Interactive
runtime verification against the real, running application (mouse-only,
per the Product Owner's own standing instruction for this pass —
direct mouse interaction, window controls, native UI element clicks,
screenshots, and source inspection only, no keyboard-injection
automation of any kind) directly confirmed: the real Validation section
rendering "No issues found." for a real, selected Calculation; the
corrected, honest Ribbon fallback message on a genuinely-unwired verb
(Copy Calculation); the BOM/Execute Object Editor sections (`WP 10.7A`)
still rendering correctly against live data. Governance registers
(Technical Debt, Future Capability) and four Academy articles updated
narratively — no new tracked item added, since every change either
completes an already-tracked capability or is a found-and-fixed defect,
not a new gap. See `WP10.8A Implementation Report.md` for the full
disclosure.

**No commit — matches this session's own established pattern. Stops
here per this Work Package's own instruction.**

### `WP 10.7A` Summary (for reference)

**`WP 10.7A` — Feature Completion.** `v0.10.0`'s fourteenth Work
Package — the direct implementation follow-through of a three-stage
audit chain (`WP 10.6B` real-application reachability audit → `WP
10.6C` wiring recovery, which found the one apparent defect was an
audit-automation timing artifact, not a real bug → `WP 10.6D` a further
audit for genuinely incomplete-but-reachable placeholder behaviour).
Closes every `WP 10.6D`-audited item achievable using only already-
existing, already-registered services and commands — no new
`ICommand`/`IWorkspaceCommand`, no new Domain contract, no new member on
`IWorkspaceManager`/`ICommandDispatcher`/`ICommandRegistry`.

**Real Ribbon dispatch for every discipline, not just Mechanical.**
Status transitions (Approve/Archive/Lock/Unlock/Request-Review/Release)
for Calculations/Documents/Verification/Manufacturing (a shared
`Set{X}StatusCommand(Guid, string, LifecycleState)` shape) and
Requirements (its own `RequirementStatus` shape); Duplicate for all six
disciplines; Create for all six disciplines, including Requirements'
own three distinct Create commands (Requirement/Group/Collection) and
Verification's own "create against the current selection as subject."
Every one of these previously fell through to the honest-but-permanent
"needs additional input this ribbon does not yet collect" message for
every discipline but Mechanical — now real, dispatching the identical,
already-registered command every equivalent context-menu/Command
Palette path already used.

**Real drag-and-drop reparenting in the Project Explorer.**
`ProjectExplorerView.OnTreeDrop` — previously a documented two-line
no-op ("preparation architecture," `WP 10.2A`) — now raises a new
`ObjectMoveRequested` event (mirroring its own existing `ObjectOpened`/
`ToggleFavouriteRequested` shape); `MainWindow` maps the dropped
object's own Kind to the correct discipline's own already-registered
`Move*Command` and dispatches it directly, guarding against a drop onto
self or a descendant. `IWorkspaceManager` was not extended.

**Five new, real, discipline-specific Object Editor sections**
(`ObjectEditorView`, closing `FCR-0068`, open since `WP 10.3A`):
Mechanical BOM fields (`SetBomLineCommand`), Requirements Owner/Priority
(`SetRequirementOwnerCommand`/`SetRequirementPriorityCommand`),
Calculations Execute/Recalculate (`ExecuteCalculationCommand`/
`RecalculateCalculationCommand` — required plumbing: `CalculationTemplateRegistry`,
previously discarded by `EngineeringWorkspaceComposer.RegisterEngineeringDisciplines`,
now captured and threaded through), Verification Record Result
(`RecordVerificationResultCommand`), Documents Attachments
(`AttachDocumentCommand`, metadata only — `TD-31` unchanged). Each
section is a Kind-gated `Expander`, collapsed-and-hidden until the real
target matches, mirroring the identical `is`-type-check idiom every
existing section already uses.

**Two real Cockpit KPI cards, one real Cockpit card.**
`EngineeringCockpit.KpiCards`'s own "Risks" (now `LiveRisks.Count`) and
"Review" (now a real sum of each discipline's own already-computed
in-review count) entries, both previously hardcoded
`IsPlaceholder: true` regardless of live data. `CockpitView`'s own
"Favourite Projects" card now reads the real, already-shipped
Desktop-layer `FavouriteObjectsState` (`WP 10.6A`), filtered to Kind
`"Project"` — `EngineeringCockpit.FavouriteProjects` itself (the
App-layer member) is unchanged, still genuinely empty, a distinct
concept from the Desktop-local "any object" favourites list.

**One genuine, disclosed, pre-existing gap found, not caused by this
Work Package — `TD-41` (new).** `ObjectEditorView.TryCreate` never
resolves a real Requirement via `EngineeringDomainContext.Repository` —
confirmed by direct test, a gap since `WP 10.3A`, previously silently
defended against by an existing test's own early return, never before
formally registered. The new Requirements Owner/Priority section is
correctly implemented but reachable only via the Ribbon for that one
discipline, not the Object Editor. A second, genuine implementation-
time bug (every new section's own success message immediately
overwritten by the `Refresh()` call that reported it) was found and
fixed in place before any commit, never reaching Technical Debt status.

**Explicitly out of scope, disclosed**: the Command Palette/Macro
`CreateDefault` gap (needs a genuine `CommandDescriptor`/
`ICommandRegistry` contract extension); Cockpit "Overdue Actions" (no
due-date field anywhere in the Domain); Ribbon Copy as a button (needs
a destination-parent picker dialog that does not exist, `FCR-0073`
unchanged); the Materials discipline (never reachable at all — a new
discipline, not a completion of an existing placeholder).

**Verification**: `src/TempestOS.slnx`, Debug and Release, both 0
Warnings/0 Errors. Combined test suite: **2263/2263 passing** (2064
`Tempest.Core.Tests`; 199 `Tempest.Desktop.Tests` — 184 existing + 15
new), including real end-to-end proofs (a chained Draft→InReview→Approved
Calculations transition, a real Mechanical reparent via the new drag/
drop event, real BOM/Attachments persistence). Interactive runtime
verification against the real, running application (mouse-only, per
the Product Owner's own explicit instruction for this pass) directly
confirmed: the real Ribbon status-transition success message and its
own "Recently Used" entry; the real Cockpit "Review"/"Risks" KPI
values; the real "Favourite Projects" empty-state message; the real
BOM and Attachments sections rendering on a real, opened Document; a
real Document's own Lifecycle history showing genuine transition
records. See `WP10.7A Implementation Report.md` for the full
disclosure.

**No commit — matches this session's own established pattern. Stops
here per this Work Package's own instruction.**

### `WP 10.5C` Summary (for reference)

**`WP 10.5C` — Commercial User Experience & Application Completion.**
`v0.10.0`'s thirteenth Work Package by completion order (commissioned
and completed after `WP 10.6A` despite its own earlier number — a
disclosed numbering note, §"Disclosed Numbering Note" below, mirroring
`WP 9.3A`'s own identical precedent) — a required-first runtime UX
audit of every `WP 10.0B`–`WP 10.5B` claimed feature, two genuine
theme-reactivity defects found and fixed as a direct result, and a
real, cohesive "engineering colour language" plus richer KPI/lifecycle
presentation across the Cockpit, Project Explorer, Property Inspector,
and Ribbon — while changing zero Engineering Domain, Runtime, Command
Framework, or Workspace-contract behaviour.

**Audit first, implement second — taken literally.** The controlling
instruction required launching the application and comparing its own
real, running behaviour against every `WP 10.0B`–`WP 10.5B` claim
*before* any implementation change — a discipline no prior Work Package
this session had been asked to follow this literally. Honestly
disclosed methodology: this environment has no screenshot tool, no
accessibility-tree inspector, no way to render or inspect real pixels —
the audit instead combined a real `dotnet run` process launch (an
8-second observation window, every Runtime Host phase completing, zero
exceptions) with direct source-level reachability tracing of every
claimed feature. **Found: no genuinely unreachable feature anywhere**
— every named capability traces to a real control, actually added to
the real shown `Window`'s own visual tree, actually wired to a real
handler. See `WP10.5C Runtime UX Traceability Matrix.md` in full.

**Two genuine, real `TD-39`-class defects found instead — the fourth
and fifth instance of this exact class this project has now found and
closed.** A fresh sweep of every remaining `Brushes.*` literal in
`Tempest.Desktop/Views/` (the audit's own methodology required it)
found `CockpitCardControl`'s own neutral border (fixed `Brushes.Gray`
since `WP 10.1A`) and `RibbonView.BuildSectionWithLabel`'s own group
divider (fixed `Brushes.Gray` since `WP 10.3B`) — both fixed via the
identical, already-proven `ThemeReactiveBrush.Bind`/
`ApplicationPalette.PanelBorderBrushKey` fix `TD-39` itself
established. Neither reaches a new Technical Debt entry — both fixed
in place, before commit.

**`DisciplineColors` — a real "engineering colour language," the
platform's own fifth colour-mapping class.** New, `Tempest.Desktop.Theming`,
keyed on the real `CommandDescriptor.Category` string, confirmed
exhaustive against all six real registered disciplines
(Mechanical/Requirements/Calculations/Verification/Documents/
Manufacturing) by direct `grep`. Applied to real, coloured Ribbon tab
accents — the same real distinction Visual Studio's/Creo's own "one
colour per category" conventions make.

**The existing `LifecycleColors` mapping (`WP 10.4A`), newly applied to
two further surfaces.** `ProjectExplorerNode` and `CockpitKpiCard`
(both `Tempest.App.Workspace`) each gain one additive, defaulted,
trailing parameter (`Lifecycle`, `PercentValue`) — populated at seven
real call sites across five discipline node providers and four Cockpit
Coverage KPIs, each reading data already held locally, zero extra
reads. `ProjectExplorerView`/`PropertyInspectorView` both now render a
real, small, coloured lifecycle-status dot, reusing `ObjectEditorView`'s
own already-established badge shape verbatim, for cross-surface visual
consistency. `CockpitCardControl.AddKpiRow` (new) renders a real
`ProgressBar` for every genuine coverage KPI, coloured by the same
Healthy/Attention/Blocked thresholds `HealthColors` already
established — never a fabricated bar for a raw count or a genuine
zero-denominator case.

**Disclosed, deliberately not attempted.** Object Editor discipline-
aware layouts (`FCR-0068`, reconfirmed still open — a Kind→Discipline
colour mapping was considered and rejected, a real risk of encoding a
wrong mapping with no way to visually verify it here); Requirements'
own tree nodes receive no lifecycle dot (`RequirementStatus` is a
genuinely distinct taxonomy from `LifecycleState`, not force-mapped);
Digital Thread received no code changes (reviewed directly, confirmed
already real and complete since `WP 10.4A`/`WP 10.5A`, disclosed as
"reviewed, zero changes needed" rather than manufactured busywork).

**Zero changes to `Tempest.Core.Commands`, every frozen `WP8.0B`
Workspace contract, and `src/Tempest.Core/` itself**, confirmed by
direct diff — every new capability is additive.

**Verification**: `src/TempestOS.slnx`, Debug and Release, both 0
Warnings/0 Errors. Combined test suite: **2248/2248 passing** (2064
`Tempest.Core.Tests` — 2062 existing + 2 new; 184
`Tempest.Desktop.Tests` — 176 existing + 8 new), zero regressions, zero
flakes confirmed across a targeted repeat re-run.

One Implementation Report, plus the required-first Runtime UX
Traceability Matrix, plus eight required reviews (Engineering,
Architecture, Security, Systems Engineering, UX, Accessibility,
Performance, Technical Debt), one Academy Concept Guide
(`31-commercial-user-experience-and-application-completion.md`), one
Academy Retrospective
(`WP10.5C-commercial-user-experience-and-application-completion.md`) —
all under `docs/releases/v0.10.0/`/`docs/academy/`. Governance
registers updated: ADR Register (reviewed, zero new ADRs, 99
unchanged), Documentation Register, Academy Register (30 → 31 Runtime
Architecture, 90 → 91 Work Packages), Technical Debt Register
(reviewed, zero new items — both genuine findings fixed in place, 40
unchanged), Future Capability Register (reviewed, zero new entries,
`FCR-0068` reconfirmed still open, 83 unchanged), Interface/Module/
Dependency Injection/Platform Services Registers and `docs/architecture/Platform
Service Map.md` (each reviewed, confirmed unaffected).

**Recommendation: sound, complete — the audit's own honest, positive
finding (reachability already strong) is reported plainly, not softened
to manufacture a larger "Runtime Integration" section than the evidence
supports (`WP10.5C Runtime UX Traceability Matrix.md` §6); both genuine
theme-reactivity findings correctly diagnosed and fixed at their own
true source (`WP10.5C Technical Debt Review.md`); every "no genuinely
unreachable feature" claim independently re-traced, not merely restated
(`WP10.5C Engineering Review.md`).**

**Disclosed Numbering Note**: this Work Package's own number (`WP
10.5C`) sorts before `WP 10.6A` (below) despite being commissioned and
completed after it — recorded here plainly, not reordered, mirroring
`WP 9.3A`'s own identical precedent (completed after `WP 9.4A` despite
its own earlier number).

**Stops here. Await Product Owner instruction before `WP 10.6B`.**

### `WP 10.6A` Summary (for reference)

**`WP 10.6A` — Command Execution & Productivity Experience.**
`v0.10.0`'s twelfth Work Package — the professional engineering
workflow layer over the existing, unmodified Command Framework and
frozen `WP8.0B` Workspace contracts: a real Undo/Redo architecture, a
Background Task Framework, Command History, Recent/Favourite Objects, a
User Command Macro foundation, and an External Controller/Input Binding
abstraction — while changing zero Command Framework or Workspace-
contract behaviour.

**Six independently large capabilities, each real, working, and wired
to a genuine consumer — consistent with every prior Work Package this
session.** The controlling instruction named command execution/progress
reporting, a background task framework, Undo/Redo, keyboard
productivity, command history, recent objects, favourite objects, a
macro foundation, and an explicit "important scope addition": a Macro &
Controller Abstraction with an explicit instruction not to integrate
any real vendor SDK. Rather than a shallow pass across every named
bullet, each of the six was built as a real, working, tested capability
with at least one genuine consumer wired end to end, disclosing
directly — in the Implementation Report §8, three new ADRs, and six new
Future Capability/Accepted Trade-off entries — exactly what received
narrower treatment.

**Undo/Redo — a Desktop-local delegate stack, not a new Command
contract (`ADR-0098`).** `UndoableAction`/`IUndoRedoStack` (new,
`Tempest.App.Workspace`) record a Do/Undo delegate pair after an action
already succeeded once — a plain data class a UI call site builds from
data it already has, never a new `ICommand`/`IUndoableCommand` contract
(no real command in this platform carries the "old state" its own
inversion would need, confirmed by direct inspection of every Rename/
Set-Status command's own constructor). Wired real and broad:
`ObjectEditorView`'s own one shared Rename commit path records a real
Undo/Redo entry across **all six disciplines**, reusing
`RenameObjectAsync`'s own already-Kind-agnostic dispatch (`ADR-0096`).
Wired real, second example: the new Favourite/Un-favourite toggle,
trivially self-inverting. `Ctrl+Z`/`Ctrl+Y` plus two new Quick Access
Toolbar buttons, enablement/tooltip live-bound.

**A Macro is a registered Command, not a second execution path
(`ADR-0099`).** `ICommandMacro`/`IMacroManager`/`RunMacroCommand` (new,
`Tempest.Core.Macros`) register one real `CommandDescriptor` per macro
— the Command Palette invokes it through the identical, unmodified
`ICommandRegistry.InvokeAsync` path every other command already uses,
zero macro-specific branching anywhere. A direct repository-wide `grep`
for `createDefault:` confirmed only `Tempest.Samples` commands are
Id-invokable today — the real, disclosed bound on which commands a
macro can sequence (`FCR-0080`). `MacroManagerDialog` (new) is the
real, minimal "foundation" authoring UI the brief asked for.

**External Controller integration — no vendor SDK, one real Keyboard
provider, one test-only stub (`ADR-0100`).**
`IInputBindingProvider`/`IExternalControllerProvider`/
`IInputBindingRegistry`/`InputBindingRouter` (new, `Tempest.Core.Input`)
mirror `IEventBus`'s own established subscriber-isolation shape.
`KeyboardCommandBindingProvider` (new, `Tempest.Desktop.Input`) is the
one real implementation; `StubExternalControllerProvider` (test-only)
proves the identical router drives a Stream-Deck-shaped provider with
zero Command Framework changes — explicitly never a real vendor
integration, per this Work Package's own Out-of-Scope.

**Recent/Favourite Objects, Command History, Background Tasks —
Desktop-local state, reusing existing real estate.**
`RecentObjectsState`/`FavouriteObjectsState` (new, mirroring
`UserSettings`'s own persistence shape) surface as two new
`ProjectExplorerView` flyout sections — deliberately distinct from the
still-unbuilt `EngineeringCockpit.FavouriteProjects`. `CommandHistoryLog`
(new) records every existing `ActionCompleted` UI surface's own
outcome; `IBackgroundTaskRunner` (new, coarse state only, never a
percentage — no `ICommandHandler<TCommand>` carries an `IProgress<T>`
parameter) is wired real to Macro invocation. Both surface as two new
`OutputPanelView` sections — no new dock panel.

**Zero changes to `Tempest.Core.Commands` and every frozen `WP8.0B`
Workspace contract**, confirmed by direct diff — every new capability
is additive. **One genuine, pre-existing governance-register drift
found and fixed**: `ADR Register.md`'s own Entries table was missing
its own `ADR-0097` row (the file existed, the narrative already named
it, no table row had ever been added) — added, with the Register's own
stale `Total`/`Related ADRs` fields corrected too; the identical class
of drift independently found a second time in the Dependency Injection
Register's own raw-call-site count, also corrected.

**Verification**: `src/TempestOS.slnx`, Debug and Release, both 0
Warnings/0 Errors. Combined test suite: **2238/2238 passing** (2062
`Tempest.Core.Tests` — 2048 existing + 14 new; 176
`Tempest.Desktop.Tests` — 153 existing + 23 new), zero regressions.

One Implementation Report, plus seven required reviews (Engineering,
Architecture, Security, Systems Engineering, UX, Performance, Technical
Debt), one Academy Concept Guide
(`30-command-execution-and-productivity-experience.md`), one Academy
Retrospective
(`WP10.6A-command-execution-and-productivity-experience.md`) — all
under `docs/releases/v0.10.0/`/`docs/academy/`. Governance registers
updated: ADR Register (`ADR-0098`–`ADR-0100` added, Accepted; 96 → 99,
plus the found-and-fixed `ADR-0097` row), Documentation Register,
Academy Register (29 → 30 Runtime Architecture, 89 → 90 Work Packages),
Technical Debt Register (zero new `TD` items; `AT-18`–`AT-23` added,
17 → 23 disclosed trade-offs), Future Capability Register
(`FCR-0078`–`FCR-0083` added, Identified; 77 → 83 total),
Interface Register (5 new interfaces, 168 → 173), Dependency Injection
Register (2 new registrations, 42 → 44), Platform Services Register (2
new rows, 30 → 32), Module Register (reviewed, zero new module).

**Recommendation: sound, complete — three genuine architectural
decisions each independently assessed against Engineering Governance
§5 and confirmed to meet it (`WP10.6A Architecture Review.md`); every
"only Sample commands are `CreateDefault`-eligible"/"Rename Undo/Redo
genuinely mutates the real object"/"a deleted macro's own stale
descriptor fails gracefully" claim independently re-verified against
the running code (`WP10.6A Engineering Review.md`); zero new trust
boundary, zero new injection surface (`WP10.6A Security Review.md`).**

Closed with "await Product Owner instruction before `WP 10.6B`" — the
Product Owner's own next instruction commissioned `WP 10.5C` (above)
instead, an out-of-sequence number commissioned and completed after
this Work Package despite sorting before it — recorded plainly, not a
`WP 10.6B`.

### `WP 10.5B` Summary (for reference)

**`WP 10.5B` — Desktop Workflow & Professional Interaction.**
`v0.10.0`'s eleventh Work Package — professional workflow interactions
across `Tempest.Desktop`: a unified, four-dialog Dialog Framework, real
window-geometry persistence with a graceful-shutdown gate, a real
end-to-end object-creation workflow, the Notification Framework's own
first real Desktop consumer, professional error handling, and real
user-facing settings — while changing zero Engineering Domain, Runtime,
Calculation, Verification, Mechanical, Requirements, Manufacturing,
Documents, or Workspace-contract behaviour.

**An enormous instruction, honestly, deliberately scoped — consistent
with every prior Work Package this session.** The controlling
instruction named fourteen dialog kinds and dozens of individual
window/workflow/notification/settings/recent-activity/error-handling
patterns across essentially every Desktop surface. Rather than a
shallow pass across every named bullet, this Work Package built one
genuinely unified Dialog Framework and one real, complete, end-to-end
object-creation workflow, disclosing directly — in the Implementation
Report §8 and five new Future Capability entries — exactly what
received lighter-touch or no treatment.

**The Dialog Framework — four real, shared controls, no shared base
class.** `InputDialog` and `MessageDialog` (both new) join
`ConfirmationDialog` (`WP 10.5A`) and the new `SettingsDialog`,
together covering all fourteen named dialog kinds either directly or
through an honestly-documented alternative (Rename reuses the existing
Object Editor tab; Copy/Move/Export/Import are disclosed, not wired).
A shared base class was considered and rejected — each dialog's own
layout differs enough that a common base would cost more than it
saves; consistency instead comes from sharing `DesignTokens`/
`ApplicationPalette` tokens directly.

**Delete Confirmation, wired once, consumed three times.** A single
`Func<string, Task<bool>>?` delegate (`ConfirmDeleteAsync`), added to
both `ProjectExplorerView` and `RibbonView`, set once by `MainWindow`,
covers the Ribbon Delete button, the Project Explorer context menu, and
the `Delete` key — gated by the new `UserSettings.ConfirmBeforeDelete`
(default `true`). Unwired (any test constructing these views directly)
preserves the exact pre-`WP 10.5B` immediate-delete behaviour.

**Real window persistence, a real multi-monitor safety net, one
consolidated graceful-shutdown gate.** `WindowUiState` (new, mirrors
`DesktopPanelUiState`'s own shape) persists X/Y/Width/Height/
`WindowState`, applied before the first frame renders.
`ClampToVisibleScreen` recentres a restored window on the primary
screen if its persisted position no longer falls on any
currently-connected screen. `MainWindow.Closing` checks
`DocumentAreaView.HasAnyDirtyTab` (new), prompts once via
`ConfirmationDialog` if anything is unsaved, then saves both
`WindowUiState` and `DesktopPanelUiState` before allowing the real
close — replacing `App.cs`'s own previous unconditional,
no-confirmation save. Uses the standard async-`Closing` pattern
(`e.Cancel = true` synchronously first, since Avalonia does not await
an async `Closing` handler).

**A real object creation/duplicate workflow — honestly scoped to
Mechanical.** `RibbonView.ObjectCreationHandlers` (new), a
`CommandDescriptor.Id`-keyed dictionary of real dispatch flows, wires
`mechanical.create` (`InputDialog` name prompt →
`ICommandDispatcher.DispatchAsync(new CreateMechanicalObjectCommand(...))`)
and `mechanical.duplicate` (`ConfirmationDialog` only). Every other
discipline's own Create/Duplicate command still falls through to the
pre-existing honest "needs additional input" message — the other five
disciplines have genuinely different constructor shapes (Requirements
alone has three), disclosed as real future work (`FCR-0075`).

**The Notification Framework's own first real Desktop consumer.**
`PlatformNotificationToastBridge` (new) implements
`IEventHandler<IPlatformNotification>`, subscribed via the
already-existing `IEventBus`, forwarding every publication to
`ToastHost`. Before this class, `IPlatformNotification` was published
but never consumed anywhere in `Tempest.Desktop` — confirmed directly
by a whole-repository search.

**Professional error handling and real user settings.**
`MainWindow.ShowUnexpectedErrorAsync` shows a real `MessageDialog`
(Error severity) for a genuinely unexpected exception, wired from
`App.cs`'s own `TaskScheduler.UnobservedTaskException` handler —
deliberately not wired to the already-fatal
`AppDomain.UnhandledException`. `UserSettings` (new, mirrors
`DesktopPanelUiState`'s shape) persists `ToastDurationSeconds`,
`ConfirmBeforeDelete`, `RecentSearchCapacity`, under its own Settings
key, completely separate from Engineering data; `SettingsDialog` is the
real, working editor (Theme, Toast duration, Confirm-before-delete).

**Two genuine, disclosed implementation-time findings, both fixed
before commit.** (1) `ProjectExplorerView`'s own filter `TextBox`'s
`TextChanged` routed event does not reliably fire for a purely
programmatic `.Text =` assignment — the identical, already-documented
finding `ObjectEditorView` (`WP 10.3A`) established for its own fields;
found while building and testing Recent Searches, fixed at the real
source (`PropertyChanged`, filtered to `TextBox.TextProperty`) — a
genuine reliability improvement to already-shipped filtering code, not
merely a test workaround. (2) This Work Package's own new
Delete-Confirmation tests initially asserted a deleted object becomes
unreachable via `Repository.FindAsync` — incorrect:
`IDeletable.DeleteAsync` (unchanged, pre-existing) is a deliberate,
correct, audit-preserving soft delete (`IsDeleted` flag only). A
related, `TD-27`-class non-determinism risk in the generic "first
object node found" test helper (which could non-deterministically land
on the sample data's own root Project object, which genuinely has
children) was fixed by adding a dedicated leaf-node finder for the
Delete-specific tests. Production code was always correct in both
cases — only the tests were wrong.

**"No ADR required" — independently assessed, not assumed.** Every real
decision this Work Package made (four dialogs sharing tokens rather
than a base class, `WindowUiState`/`UserSettings` mirroring
`DesktopPanelUiState`'s established shape, wiring one discipline's
Create/Duplicate flow) was checked directly against Engineering
Governance §5's own ADR-creation criteria and found not to meet it
(`WP10.5B Architecture Review.md` §1-§2).

**Zero changes to any of the twelve frozen `WP8.0B` Workspace
contracts.** Zero files touched under `src/Tempest.Core/` or
`src/Tempest.App/Workspace/` at all, confirmed by direct review. Zero
Engineering Domain, Runtime, Calculation, Verification, Mechanical,
Requirements, Manufacturing, or Documents behaviour changed — all
confirmed independently.

**Verification**: `src/TempestOS.slnx`, Debug and Release, both 0
Warnings/0 Errors. Combined test suite: **2201/2201 passing** (2048
`Tempest.Core.Tests`, unchanged — zero Core files touched; 153
`Tempest.Desktop.Tests` — 130 existing + 23 new), re-run twice in full
after the two genuine findings were fixed, zero flakes both times.

One Implementation Report, plus eight required reviews (Engineering,
Security, Systems Engineering, Architecture, UX, Accessibility,
Performance, Technical Debt), one Academy Concept Guide
(`29-desktop-workflow-and-professional-interaction.md`), one Academy
Retrospective
(`WP10.5B-desktop-workflow-and-professional-interaction.md`) — all
under `docs/releases/v0.10.0/`/`docs/academy/`. Governance registers
updated: ADR Register (reviewed, zero new ADRs, 96 unchanged),
Documentation Register, Academy Register (28 → 29 Runtime Architecture,
88 → 89 Work Packages), Technical Debt Register (reviewed, zero new
items — both genuine findings fixed in place, 40 unchanged, 31 Open
unchanged), Future Capability Register (`FCR-0073`–`FCR-0077` added,
Identified; 72 → 77 total), Interface/Module/Dependency
Injection/Platform Services Registers and `docs/architecture/Platform
Service Map.md` (each reviewed, confirmed unaffected).

**Recommendation: sound, complete — every named dialog kind covered
either directly or through an honestly-documented alternative, "no ADR
required" independently assessed correct, not assumed (`WP10.5B
Architecture Review.md` §1); both genuine implementation-time findings
correctly diagnosed and correctly fixed at their own true source, not
merely patched around (`WP10.5B Technical Debt Review.md` §1).**

Closed with "await Product Owner instruction before `WP 10.6A`" — `WP
10.6A` (above) is that direct continuation.

### `WP 10.5A` Summary (for reference)

**`WP 10.5A` — Workspace Visual Polish & Engineering User Experience.**
`v0.10.0`'s tenth Work Package — a professional visual/UX polish pass
across the entire `Tempest.Desktop` application: a real theme-reactive
brush infrastructure, a consistent icon vocabulary, four new reusable
feedback/empty-state controls, and targeted Object Editor/Project
Explorer/Output Panel improvements — while changing zero Engineering
Domain, Runtime, Calculation, Verification, Mechanical, Requirements,
Manufacturing, Documents, or Workspace-contract behaviour.

**An exceptionally broad instruction, honestly, deliberately scoped —
consistent with every prior Work Package this session.** The
controlling instruction named an almost whole-application surface
(every panel, every interaction state, every theme, every named
feedback mechanism). Rather than spreading thin, unverified effort
across every named bullet, this Work Package built a real, cohesive
foundation and applied it to the highest-value, most-repeated surfaces
first, disclosing directly — in the Implementation Report §8 and two
new Future Capability entries — exactly what received lighter-touch or
no treatment.

**`ApplicationPalette`/`ThemeReactiveBrush` — the platform's own first
genuinely theme-reactive custom brushes.** Five explicit
`ResourceDictionary.ThemeDictionaries` keys, resolved by a small,
shared helper that repaints on both attachment and theme change. Before
this Work Package, zero controls anywhere in `Tempest.Desktop` used
`DynamicResource`/theme-reactive binding — confirmed by direct `grep`.

**`TD-39`, closed.** `PanelHostControl`'s own Auto-Hide flyout
background and `CommandPaletteOverlay`'s own background/border — both
previously fixed `Brushes.White`/`Brushes.Black`/`Brushes.Gray` — now
bind through `ThemeReactiveBrush`, correct in both theme variants for
the first time.

**A same-session regression, found and fixed.** This Work Package's own
theme audit found that `DigitalThreadGraphView` (`WP 10.4A`, the
immediately preceding Work Package) hardcodes its own node-fill colours
and mini-map background — genuinely wrong in Light theme. Fixed the
same way, disclosed directly as this session's own regression, not
silently folded into general polish.

**Icon consistency, two real tiers.** `IconRegistry`'s own ~20 Kind
glyphs move from a mixed full-colour-emoji/monochrome-symbol set to a
uniform, monochrome, theme-reactive Unicode vocabulary (Geometric
Shapes/Mathematical Operators blocks, text-default presentation per
Unicode UTR#51). `IconGeometry` (new) is the platform's first real,
hand-authored vector icon set — four glyphs (Close, Check,
ChevronRight, ChevronDown), used by the new controls' own interactive
chrome — a deliberately curated starting set, not a comprehensive
replacement (`FCR-0071`, new).

**Four new, real, reused controls.** `ToastHost`/`ToastNotification` —
a transient, auto-dismissing notification stack, four severities
(`SeverityColors`, new — a fourth colour-language mapping alongside
`HealthColors`/`CategoryColors`/`LifecycleColors`), wired to every
existing `ActionCompleted` event this platform already raises.
`BusyOverlay` — a real, semi-transparent, message-bearing overlay,
wired into the Ribbon's own area-switch (the one navigation action
substantial enough to warrant it). `ConfirmationDialog` — a real Yes/No
modal-style overlay; its first, real consumer closes `TD-40` directly.
`EmptyStateView` — icon, heading, guidance, optional recommended
action; its first consumer replaces `ProjectExplorerView`'s own
plain-text placeholder with two genuinely distinct, engineering-
specific messages.

**`TD-40`, closed.** `DocumentAreaView.IsMarkedDirty(Guid)` (new) +
`MainWindow.CloseDocumentAsync`'s own new `ConfirmationDialog` gate: a
dirty Object Editor tab now prompts "Discard unsaved changes?" before
closing; cancelling leaves the tab, and its edits, untouched. Reached
from both the tab's own close button and the keyboard-shortcut close
path.

**`ObjectEditorView` polish.** A coloured lifecycle status badge
(reusing `LifecycleColors`, `WP 10.4A`, rather than inventing a fifth
status-colour scheme) and severity-coded validation rows
(`SeverityColors`), replacing plain emoji and locally-hardcoded
`Brushes.IndianRed`/`Brushes.DarkOrange`.

**Four genuine, disclosed implementation-time findings, all fixed
before commit.** (1) The conventional `GetResourceObservable`/`Bind`
code-behind pattern does not reliably re-push a value once a control
that subscribed while unattached is later attached to a real, shown
`Window` — confirmed directly by a failing test; fixed via
`ThemeReactiveBrush`, resolving directly against `Application.Current`
instead. (2) A process-wide `static bool _registered` guard on
`ApplicationPalette.Register` intermittently left a *later* headless
test's own fresh `Application` instance completely unregistered, since
Avalonia's own test host does not guarantee one instance per process —
found by a flaking `VisualPolishTests` run (reproduced deterministically
before the fix, zero failures across five re-runs after); fixed by
removing the guard, since re-registration is harmless. (3) A new test
touching `StreamGeometry.Parse` was initially a plain `[Fact]`, not
`[AvaloniaFact]` — the resulting `TypeInitializationException`
permanently poisoned `IconGeometry` for the rest of the test process,
silently breaking unrelated tests later in the same run; fixed by
correcting the attribute. (4) `WP 10.4A`'s own
`DigitalThreadGraphModelTests.Recentre_VerificationActivityWithARecordedResult_AddsTheResultAsAVisibleLeafNode`
carried an over-broad assertion (matching *any* `"verifiedBy"`-labelled
edge, not the specific `Activity→Record` edge `TD-32` names) that could
intermittently fail depending on `InMemoryEngineeringObjectRepository`'s
own unspecified iteration order (`TD-27`'s identical risk class) —
found only by this Work Package's own repeated "check for flakes"
re-runs, fixed by narrowing the assertion; `WP10.4A`'s own already-
published documents are deliberately left untouched — this finding is
disclosed in `WP10.5A`'s own documents instead, per this Work Package's
own explicit "do not silently correct historical documentation"
instruction.

**"No ADR required" — independently assessed, not assumed.** Every real
decision this Work Package made (the theme-reactive brush helper, four
new controls, two closed debt items) was checked directly against
Engineering Governance §5's own ADR-creation criteria and found not to
meet it — Desktop-local, additive, reversible, the same category as
`RibbonView`/`ObjectEditorView`/`DigitalThreadGraphView`, none of which
received an ADR for their own introduction either.

**Zero changes to any of the twelve frozen `WP8.0B` Workspace
contracts.** Zero files touched under `src/Tempest.Core/` or
`src/Tempest.App/Workspace/` at all, confirmed by direct review — matching
`WP 10.3B`/`WP 10.4A`'s own cleanest layering-compatibility result
again. Zero Engineering Domain, Runtime, Calculation, Verification,
Mechanical, Requirements, Manufacturing, or Documents behaviour
changed — all confirmed independently.

**Verification**: `src/TempestOS.slnx`, Debug and Release, both 0
Warnings/0 Errors. Combined test suite: **2178/2178 passing** (2048
`Tempest.Core.Tests`, unchanged — zero Core files touched; 130
`Tempest.Desktop.Tests` — 105 existing + 25 new), re-run twice in full
after the four genuine findings were fixed, zero flakes both times.

One Implementation Report, plus eight required reviews (Engineering,
Security, Systems Engineering, Architecture, UX, Accessibility,
Performance, Technical Debt — this platform's own first dedicated
Accessibility Review and Technical Debt Review), one Academy Concept
Guide (`28-workspace-visual-polish.md`), one Academy Retrospective
(`WP10.5A-workspace-visual-polish.md`) — all under
`docs/releases/v0.10.0/`/`docs/academy/`. Governance registers updated:
ADR Register (reviewed, zero new ADRs, 96 unchanged), Documentation
Register, Academy Register (27 → 28 Runtime Architecture, 87 → 88 Work
Packages), Technical Debt Register (`TD-39`/`TD-40` both Resolved, 40
unchanged, 33 → 31 Open), Future Capability Register (`FCR-0071`/
`FCR-0072` added, Identified; `FCR-0067` marked Implemented; 70 → 72
total), Interface/Module/Dependency Injection/Platform Services
Registers and `docs/architecture/Platform Service Map.md` (each
reviewed, confirmed unaffected).

**Recommendation: sound, complete — every named theme/icon/feedback
decision independently re-verified against the shipped code, not
merely restated (`WP10.5A Engineering Review.md`); "no ADR required"
independently assessed correct, not assumed (`WP10.5A Architecture
Review.md` §1); both closed Technical Debt items proven by dedicated,
repeatedly re-run tests (`WP10.5A Technical Debt Review.md`); every one
of the four genuine implementation-time findings correctly diagnosed
and correctly fixed at its own true source, not merely patched around.**

Closed with "await Product Owner instruction before `WP 10.5B`" — `WP
10.5B` (above) is that direct continuation.

### `WP 10.4A` Summary (for reference)

**`WP 10.4A` — Digital Thread Visualisation.** `v0.10.0`'s ninth Work
Package — a graphical Digital Thread viewer over the existing
Engineering Domain: an interactive, progressively-expandable node-link
graph, realising `ADR-0093` and `WP10.0A Digital Thread & Relationship
Visualisation.md` as real, working code for the first time, built
entirely as a presentation layer over already-existing, already-
permitted reads.

**A genuine, disclosed reversal of direction, with a governing ADR
already in place — unlike the Ribbon's own reversal one release
earlier.** Every prior `v0.10.0` Work Package through `WP 10.3B`
explicitly excluded a Digital Thread graph ("No Digital Thread graph").
This Work Package's own controlling instruction reverses that directly
and names `ADR-0093` explicitly ("Honour `ADR-0093`") — unlike the
Ribbon's own reversal (`WP 10.3B`), no ADR tension needed resolving
here: `ADR-0093` already existed, authored during `WP 10.0A`
specifically to authorise exactly this graph once built.

**The composed read — a deliberate, disclosed choice over
`IEvidenceComposer`, independently assessed superior.**
`DigitalThreadGraphModel.LoadRelationships` reuses the identical
bidirectional pair `ObjectEditorView`'s own Relationship summary
already established (`WP 10.3A`): `IHasRelationships.GetRelationshipsAsync`
(outgoing) + `EngineeringDomainContext.RelationshipRepository.GetIncomingAsync`
(incoming), generic over every Kind, zero per-discipline special-
casing. `IEvidenceComposer`/`IEvidence` was considered and rejected —
outgoing-only, and its own Verification/Calculation-result enrichment
resolves structurally empty for every object today (`TD-30`), strictly
less complete than the pair reused here (`WP10.4A Architecture
Review.md` §2).

**Progressive, client-side, on-demand expansion — never a precomputed
or cached transitive traversal, exactly as `ADR-0093` requires.** The
selected object becomes the graph's own centre node; its direct
relationships become collapsed neighbour nodes. Expanding a node issues
a fresh, live read of its own direct relationships every time.
Collapsing a node removes exactly what that node's own expansion
added, unless another still-expanded path keeps a shared node
reachable — implemented as a general fixpoint reachability sweep from
the centre, not fragile "who added this" bookkeeping. Nothing is ever
cached, indexed, or persisted; the entire graph is discarded when its
owning tab closes.

**`TD-32`, closed for this view — exactly as `WP10.0A`'s own doc §4
asked.** A Verification Activity's own `"verifiedBy"` link to its
recorded result has been invisible to `RelationshipRepository` since
`WP 9.3A` — durable, correctly readable via the raw store, but never
through the relationship graph. Expanding a `"VerificationActivity"`
node additionally calls `VerificationRecordReader.GetResultHistoryAsync`
and renders each result as a real, visible, non-expandable, non-
editable leaf node — the first place in the Workspace/Desktop layer
this link is shown graphically. **A second, previously-undisclosed
consumer of the identical gap was found in the process**: independently
re-proving the merge required confirming that `ObjectEditorView`'s own
Relationship summary (`WP 10.3A`) shares the same blind spot — a real,
newly-disclosed fact about already-shipped code, not introduced by this
Work Package. The Technical Debt Register's own `TD-32` entry is
revisited accordingly; disposition remains Open, unchanged.

**Every named scope item realised as real, working code**: interactive
relationship graph, node-link visualisation, expand/collapse (with
real reachability preservation), relationship filtering (hides edges,
never nodes — a disclosed default), relationship highlighting,
selected object centring, zoom, pan, a real click-to-pan mini-map,
relationship categories (`CategoryColors`, a new, deterministic
palette), object icons (`IconRegistry`, reused), object status
indicators (`LifecycleColors`, new), a combined legend/filter panel,
object search, click-to-open object editor (single click), double-click
navigation (re-centres the graph itself), a Relationship Inspector, a
breadcrumb path, and three real, deterministic layouts (Hierarchical,
Force-Directed — a seeded 80-iteration spring simulation, Engineering —
concentric rings by hop-depth).

**Document tab, not a new dock slot — a deliberate, disclosed
architectural choice.** `WP10.0A`'s own doc §3 describes "two
independently dockable panels," architecture-stage language predating
`WP 10.2B`'s own concrete `DockingGrid`, which has exactly three
physical dock slots, all already occupied. A fourth slot would itself
be a Workspace docking change — out of this Work Package's own "No
Workspace redesign" scope. `DigitalThreadGraphView` implements
`IWorkspaceView` directly instead, reusing 100% already-built Document
Area tab infrastructure; `MainWindow.BuildDocumentContent` gained one
new, generic first branch (`if (view is Control alreadyBuilt) return
alreadyBuilt;`), not a Digital-Thread-specific special case. Opened via
a new "🕸 View Relationships" Quick Access Toolbar button, deduplicated
per root object.

**One genuine, disclosed defect found and fixed before commit.**
`DigitalThreadGraphModel.JumpToBreadcrumb`'s first implementation
cleared the target breadcrumb entries, then called the same `Recentre`
path forward navigation uses — which unconditionally re-pushed the
centre being navigated *away* from, so jumping back to an earlier
centre never actually emptied the trail. Caught directly by this Work
Package's own test suite before any commit; fixed by splitting the
shared rebuild logic into a private `BuildGraphAround` helper with an
explicit `pushCurrentCentreToBreadcrumb` flag.

**Zero changes to any of the twelve frozen `WP8.0B` Workspace
contracts — the cleanest layering-compatibility result of any Work
Package this release, matching `WP 10.3B`'s own result exactly.** Zero
files touched under `src/Tempest.Core/` or `src/Tempest.App/Workspace/`
at all, confirmed by direct `git diff --stat`. Zero Engineering Domain
changes, zero Runtime redesign, zero Workspace redesign, zero floating
windows, zero multi-monitor, zero automation framework, zero new
traversal mechanism — all confirmed independently.

**Verification**: `src/TempestOS.slnx`, Debug and Release, both 0
Warnings/0 Errors. Combined test suite: **2153/2153 passing** (2048
`Tempest.Core.Tests`, unchanged — zero Core files touched; 105
`Tempest.Desktop.Tests` — 81 existing + 24 new), re-run across both
configurations, zero flakes observed.

One Implementation Report, plus six required reviews (Engineering,
Security, Systems Engineering, Architecture, UX, Performance), one
Academy Concept Guide (`27-digital-thread-visualisation.md`), one
Academy Retrospective (`WP10.4A-digital-thread-visualisation.md`) —
all under `docs/releases/v0.10.0/`/`docs/academy/`. Governance
registers updated: ADR Register (reviewed, zero new ADRs, 96
unchanged), Documentation Register, Academy Register (26 → 27 Runtime
Architecture, 86 → 87 Work Packages), Technical Debt Register
(`TD-32`'s existing entry revisited and re-disclosed, disposition
unchanged — zero new items, 40 unchanged, 33 Open), Future Capability
Register (`FCR-0070` added, Identified — dense-graph clustering/
pruning, already disclosed and accepted by `ADR-0093` itself),
Interface/Module/Dependency Injection/Platform Services Registers and
`docs/architecture/Platform Service Map.md` (each reviewed, confirmed
unaffected).

**Recommendation: sound, complete — every one of the nineteen named
scope items independently traced to a specific implementation member
and a specific passing test (`WP10.4A Systems Engineering Review.md`
§1); the bidirectional-read choice over `IEvidenceComposer`
independently assessed as the more complete design, not merely the
more convenient one (`WP10.4A Architecture Review.md` §2); `ADR-0093`
independently re-verified honoured throughout, not merely claimed
(`WP10.4A Engineering Review.md` §1, `WP10.4A Systems Engineering
Review.md` §4); the one genuine defect found correctly diagnosed and
correctly fixed at its own true source (`WP10.4A Engineering Review.md`
§6).**

Closed with "await Product Owner instruction before `WP 10.4B`" — `WP
10.5A` (above) is that direct continuation (the Product Owner's own
next instruction, `WP 10.5A`, not a `WP 10.4B`).

### `WP 10.3B` Summary (for reference)

**`WP 10.3B` — Ribbon, Toolbar & Command Experience.** `v0.10.0`'s
eighth Work Package — a professional command system for
`Tempest.Desktop`: a real, tabbed Engineering Ribbon, a Quick Access
Toolbar, and command discoverability/context-awareness features,
integrated directly with the existing `ICommandRegistry` and Command
Palette, without duplicating command registration, bypassing
`ADR-0070`, or building a second command framework.

**A genuine, disclosed reversal of direction, not a contradiction
silently resolved.** Every prior `v0.10.0` Work Package through `WP
10.3A` explicitly excluded a ribbon ("No ribbon"). This Work Package's
own controlling instruction reverses that directly, naming "Engineering
ribbon" as its own first scope item — the Product Owner's own explicit
new instruction. No ADR ever formally forbade a ribbon (`ADR-0092`/
`ADR-0094` decided the desktop paradigm and framework, neither commits
to or against any specific chrome style), so no ADR supersession was
needed — each prior "No ribbon" was a disclosed, per-Work-Package
scope boundary, not a standing architectural decision.

**One generic engine, over `ICommandRegistry.Items` — never a second
registration mechanism.** `RibbonView` groups every real
`CommandDescriptor` by `Category` into one tab per discipline — six
tabs, matching the six real Engineering Disciplines exactly, zero
per-discipline Desktop code, mirroring `ObjectEditorView`'s own "one
engine, six disciplines" precedent (`WP 10.3A`) a second time.
"Context-sensitive ribbon tabs" matches the active Navigation area's
own title against each tab's own Category by substring, wired from the
one existing consolidation point (`MainWindow.SetCurrentArea`) already
called from every area-switch path.

**A genuine, load-bearing discovery reshaped the entire dispatch
design.** A direct `grep` across this platform's entire history found
zero `CommandDescriptor` registrations, anywhere, ever, set
`CreateDefault` — `ICommandRegistry.InvokeAsync` cannot invoke a single
real command by Id alone. Rather than generalising the Command
Framework (explicitly excluded — "No new command framework"), the
Ribbon reuses the three already-Kind-keyed dispatch verbs
(`ADR-0096`/`ADR-0097`) `WP 10.2A`/`WP 10.3A` already built for exactly
this problem: **Delete** dispatches immediately
(`IWorkspaceManager.DeleteObjectAsync`, needs no input beyond the
current selection); **Rename**/**Edit** (Revise) route to the real
editing surface that already collects the required text input — the
Object Editor tab (`WP 10.3A`) — rather than duplicating a text box
inside the ribbon. Every other command (Create, Move, Copy, Duplicate,
Execute, ...) is shown, per `ADR-0070`'s own "disabled, not hidden"
principle, but honestly reports it needs more input than a button
click supplies, never silently doing nothing.

**Command grouping and icons are both derived, not authored.** No
descriptor has ever set `CommandDescriptor.Icon` either (the identical,
confirmed-by-`grep` finding) — real per-command icons remain disclosed
future work (`FCR-0069`). Every command Id is classified by its own
well-known verb suffix into one of four groups (Create/Organize/
Lifecycle/Actions), each with its own deterministic glyph — real,
disclosed, rendering-time heuristics, never fabricated per-command
choices.

**Two genuine, disclosed defects found and fixed before commit.**
Building the Ribbon's own honest "cannot dispatch by Id alone" case
surfaced a pre-existing, six-Work-Package-latent defect already in
`CommandPaletteOverlay.InvokeSelectedAsync`: pressing `Enter` on any
real command (every one, since `WP 8.1A`) previously closed the
palette and did nothing else, no error, no feedback, silently. Fixed
at its own source — a new `CommandUnavailable` event, wired to a real
Status Bar message. Separately, this Work Package's own new
`RibbonView.Rebuild()` was found, by its own test suite, to never call
`RefreshEnablement()` after construction — every selection-aware
button defaulted to Avalonia's own `Button.IsEnabled = true`
regardless of whether a real selection existed yet. Fixed at its own
source too, before any commit.

**Quick Access Toolbar and Ribbon/Navigation Framework — consolidated,
disclosed, zero capability lost.** The old, minimal two-button Toolbar
(`WP 10.0B`) is subsumed into a new Quick Access Toolbar (Command
Palette, Theme, plus Reset Layout, `WP 10.2B`, previously reachable
only via the `_Layout` menu). The old Navigation Framework's own
standalone area-switch button row is retired entirely — a Ribbon tab
click now both switches the Navigation area and shows that
discipline's own commands, so the two concerns need one control, not
two. Both consolidations independently verified to lose zero existing
capability.

**Status-bar command hints and Recently-Used commands, both real.**
`StatusBarView` gained a seventh segment, Hint, driven by real
`PointerEntered`/`PointerExited` events on every Ribbon button, never
scripted. A bounded (5), in-memory, per-tab Recently-Used list, updated
on every real dispatch.

**Zero changes to any of the twelve frozen `WP8.0B` Workspace
contracts, `ADR-0096`, or `ADR-0097` — the cleanest layering-
compatibility result of any Work Package this release.** Zero files
touched under `src/Tempest.Core/` or `src/Tempest.App/Workspace/` at
all, confirmed by direct `git diff --stat` — stronger than `WP 10.2B`'s
own "zero of twelve contracts" (this Work Package touches neither
layer, not merely zero contract members). Zero Engineering Domain
changes, zero Runtime redesign, zero Workspace redesign, zero floating
windows, zero Digital Thread graph, zero new command framework — all
confirmed independently.

**Verification**: `src/TempestOS.slnx`, Debug and Release, both 0
Warnings/0 Errors. Combined test suite: **2129/2129 passing** (2048
`Tempest.Core.Tests`, unchanged — zero Core files touched; 81
`Tempest.Desktop.Tests` — 71 existing + 10 new), re-run three times
(Desktop) / twice (Core) across both configurations, zero flakes
observed.

One Implementation Report, plus six required reviews (Engineering,
Security, Systems Engineering, Architecture, UX, Performance), one
Academy Concept Guide (`26-ribbon-and-command-experience.md`), one
Academy Retrospective (`WP10.3B-ribbon-and-command-experience.md`) —
all under `docs/releases/v0.10.0/`/`docs/academy/`. Governance
registers updated: ADR Register (reviewed, zero new ADRs, 96
unchanged), Documentation Register, Academy Register (25 → 26 Runtime
Architecture, 85 → 86 Work Packages), Technical Debt Register
(reviewed, zero new items — both defects found were fixed before
commit, never reaching TD-entry status), Future Capability Register
(`FCR-0069` added, Identified), Interface/Module/Dependency Injection/
Platform Services Registers and `docs/architecture/Platform Service
Map.md` (each reviewed, confirmed unaffected).

**Recommendation: sound, complete — every one of the fifteen named
scope items independently traced to a specific implementation member
and a specific passing test (`WP10.3B Systems Engineering Review.md`
§1); "no new command framework" independently assessed as correctly
honoured by reusing existing Kind-keyed dispatch rather than
generalising it (`WP10.3B Architecture Review.md` §2); both genuine,
disclosed defects correctly diagnosed and correctly fixed at their own
true source, not merely patched around (`WP10.3B Engineering
Review.md` §2–3).**

Closed with "await Product Owner instruction before `WP 10.4A`" — `WP
10.4A` (above) is that direct continuation.

### `WP 10.3A` Summary (for reference)

`v0.10.0`'s seventh Work Package — professional graphical editors for Engineering Objects
across all six disciplines, built as one generic Object Editor
Framework rather than six independently hand-built editors,
integrated with the existing Workspace, Cockpit, and Property
Inspector, without touching any of the twelve frozen `WP8.0B`
Workspace contracts beyond one precedented, additive `IWorkspaceManager`
extension.

**The real gap this Work Package found and closed.**
`DocumentAreaView.BuildBody` (`WP 10.0B`) rendered exactly three
read-only lines — Title, Kind, Id — for every open document tab,
across every one of `WP 10.1A`–`WP 10.2B`, by its own explicit
disclosure ("never a bespoke, per-Kind rich editor... those remain
disclosed future work"). This Work Package replaces that placeholder
with `ObjectEditorView`, a real, working editor.

**One generic engine, not six.** Every real Engineering Object across
all six disciplines already composes the identical
`EngineeringObjectBase` facet set (`ADR-0075`) — `ObjectEditorView`
reads `Content`/`Status`/`History`/relationships/validation directly
from whichever real object a tab presents (`ADR-0063` permits direct
reads), rendering Identity/Content/Lifecycle/Relationships/Validation
sections identically regardless of discipline. `ObjectEditorView.TryCreate`
resolves the real object via `EngineeringDomainContext.Repository`,
falling back to the original generic body only for a synthetic,
non-repository Kind (Calculations' own `"CalculationTemplate"`) or the
Sample Explorer's own fixed content — zero per-Kind registration code
anywhere in `Tempest.Desktop`.

**Editable properties — Name and Content, both real, both dispatched
through Commands.** Name uses the existing `RenameObjectAsync`
(`ADR-0096`). Content uses a **new**, third `IWorkspaceManager`
extension — `RegisterReviseFactory`/`CanRevise`/`ReviseObjectAsync`
(`ADR-0097`), mirroring `ADR-0096`'s own shape exactly a third time.
Every discipline's own already-existing `Revise*Command` becomes a
factory; **Mechanical, the one discipline of six with no Revise
command at all**, gets a new one (`ReviseMechanicalObjectCommand`),
closing a genuine, pre-existing asymmetry — the underlying Domain
capability (`IHasRevisions`) already existed identically for it since
`ADR-0075`, only the Workspace-layer command wrapper was missing.

**Validation feedback, real, for the first time.** `IValidatable.ValidateAsync()`
already existed at the Domain layer (`ADR-0075`) but was never
reachable from any Workspace/Desktop surface —
`PropertyInspectorView`'s own "Validation" section (`WP 10.2A`)
remains the disclosed placeholder it always was, still accurate for
the Property Facet layer specifically; `ObjectEditorView` holds the
real object directly, so it calls the real method. Informational
only — deliberately never blocks Save, since this platform's
`IValidationRuleSet` has no notion of "which errors this specific edit
caused."

**Dirty-state tracking, Save/Cancel, Read-only mode.**
`ObjectEditorView.IsDirty` is a new, genuinely buffered, Desktop-local
concept — distinct from and never redefining `IWorkspaceView.IsDirty`,
which remains permanently `false`, by design, unchanged since `WP
8.1A` (every concrete View still never buffers a local edit; this
Work Package's own editor is the first thing in this platform that
does). `DocumentAreaView` gained an injectable content-builder
constructor parameter (defaulting to the original generic body,
renamed `BuildDefaultBody`, fully backward compatible) and a new
`MarkDirty(Guid, bool)` method reflecting the buffered dirty state in
the tab header's own `" *"` suffix, alongside — never instead of —
`view.IsDirty` itself.

**Relationship summary and navigation between related objects.** A
real, flat list, both directions (`IHasRelationships.GetRelationshipsAsync`
for outgoing, `EngineeringDomainContext.RelationshipRepository.GetIncomingAsync`
for incoming) — deliberately flat, never a node-link graph
(`ADR-0093`'s Digital Thread graph remains explicitly out of scope).
Each row's own "Open →" button reuses the existing
`INavigationService.OpenAsync` + `DocumentAreaView.ShowTab` — no new
navigation mechanism.

**Integrated with the Workspace, Cockpit, and Property Inspector.**
Every write dispatches through the real, unmodified
`IWorkspaceManager`/`CommandHandlerTable`; `ObjectEditorView.ActionCompleted`
triggers `_cockpitView.Refresh()` and `_inspectorView.Refresh()`, the
identical pattern `_explorerView.ActionCompleted`/`_inspectorView.ActionCompleted`
already establish since `WP 10.0B`–`WP 10.2A`.

**Zero changes to any of the twelve frozen `WP8.0B` Workspace
contracts beyond one precedented, additive `IWorkspaceManager`
extension — independently re-confirmed.** `ADR-0097` adds three new
members only; every existing member of all twelve contracts,
including `ADR-0096`'s own five, is untouched. Zero Engineering
Domain changes, zero Runtime redesign, zero forbidden-scope items
(floating windows, ribbon, Digital Thread graph) — confirmed
independently by direct `git diff --stat` and per-contract review.

**One genuine, disclosed test-arithmetic finding.**
`MechanicalWorkspaceIntegrationTests.CommandRegistry_ListsAllNineMechanicalCommands`
failed on the first full-suite run after this Work Package's own
change (`Expected: 9, Actual: 10`) — a direct, correct consequence of
the new `"mechanical.edit"` `CommandDescriptor`, not a defect. Fixed
in place (renamed `...ListsAllTenMechanicalCommands`, count
corrected), re-run, confirmed passing; the other five disciplines' own
identical hardcoded-count tests independently re-checked, confirmed
unaffected (none needed a new descriptor — each already had its own
`"...edit"`/`"...revise"` descriptor from its own original Work
Package).

**Two genuine, disclosed findings registered, neither release-blocking.**
`TD-40` — closing an editor tab with unsaved, buffered edits does not
prompt for confirmation (`IWorkspaceView.CloseAsync` always returns
`true` today, since every concrete View's own `IsDirty` predates this
Work Package's own genuine buffering) — a deliberate scope decision
for a first implementation, not an oversight. `FCR-0068` — real,
disclosed future work for discipline-specific editor enhancements (BOM
fields, Owner/Priority, Execute, Record Result, Attachments), each
dispatchable through an already-existing Command, deferred here in
favour of the one-generic-engine design.

**Verification**: `src/TempestOS.slnx`, Debug and Release, both 0
Warnings/0 Errors. Combined test suite: **2119/2119 passing** (2048
`Tempest.Core.Tests` — 2040 existing + 8 new; 71
`Tempest.Desktop.Tests` — 57 existing + 14 new), re-run four times
(Desktop) / twice (Core) across both configurations, zero flakes
observed.

One Implementation Report, plus six required reviews (Engineering,
Security, Systems Engineering, Architecture, UX, Performance), one
Academy Concept Guide (`25-engineering-object-editors.md`), one
Academy Retrospective (`WP10.3A-engineering-object-editors.md`) — all
under `docs/releases/v0.10.0/`/`docs/academy/`. Governance registers
updated: ADR Register (`ADR-0097` added, 95 → 96), Documentation
Register, Academy Register (24 → 25 Runtime Architecture, 84 → 85 Work
Packages), Technical Debt Register (`TD-40` added, 39 → 40, 33 Open),
Future Capability Register (`FCR-0068` added, Identified), Interface/
Module/Dependency Injection/Platform Services Registers and
`docs/architecture/Platform Service Map.md` (each reviewed, confirmed
unaffected).

**Recommendation: sound, complete — every one of the sixteen named
scope items independently traced to a specific implementation member
and a specific passing test (`WP10.3A Systems Engineering Review.md`
§1); "one generic engine, six disciplines" independently assessed as
the architecturally correct design, not merely the cheaper one
(`WP10.3A Architecture Review.md` §3); `ADR-0063` independently
re-verified honoured throughout, not merely claimed (`WP10.3A
Engineering Review.md` §1).**

Closed with "await Product Owner instruction before `WP 10.3B`" — `WP
10.3B` (above) is that direct continuation.

### `WP 10.2B` Summary (for reference)

`v0.10.0`'s sixth Work Package — a professional dockable workspace over the existing
`Tempest.Desktop` shell (Resizable panels, Docking framework,
Collapsible panels, Auto-hide architecture, Saved/restored/reset
layouts, three predefined presets, dockable Project Explorer/Property
Inspector/Status-Output panels, improved document hosting, persistent
panel visibility/splitter positions), built entirely without touching
any of the twelve frozen `WP8.0B` Workspace contracts.

**`WorkspaceDockPosition.Bottom`, realised for the first time.** A
real enum member since `WP 8.0B`, never wired to any actual dock
surface across four subsequent Work Packages (`WP 10.0B`, `WP 10.1A`,
`WP 10.1B`, `WP 10.2A`). `DockingGrid` gained a third row (mirroring
its own existing Left/Right column pattern exactly) hosting a new,
fourth `IWorkspacePanel` implementer, `OutputPanel` — real,
live `IDiagnosticsProvider` module/hosted-service status (Runtime Host
State plus every tracked module's/hosted service's own lifecycle
state), honestly disclosed as a live stream, never a captured
log-history feed, since `ILogSink` carries no read-back API anywhere
in this platform and adding one would be a Runtime change, explicitly
out of this Work Package's own scope.

**Collapse and Auto-Hide — one shared visual affordance, two
genuinely distinct behaviours.** `PanelHostControl` gained two new
header buttons. Collapse shrinks a panel to a fixed thin strip *in
place*, inside its own normal dock slot — clicking the strip expands
it back instantly, no overlay. Auto-Hide (unpinning) additionally
removes the panel from the reserved dock layout entirely, handing that
space back to the Document Area, leaving the same thin strip as a
reachable edge tab; clicking it instead opens a temporary flyout
overlay (`DockingGrid.ShowFlyout`) by repositioning the *same*,
already-docked `PanelHostControl` instance via `Grid` attached
properties and `ZIndex` — never a second, duplicate control, and never
a second, floating OS window. Deliberate interaction model, disclosed:
click-to-reveal, click-the-Document-Area-away or `Escape`-to-dismiss —
not hover-to-peek with a dwell timer, chosen for determinism, keyboard
parity, and headless testability, not because hover was attempted and
found difficult (`WP10.2B UX Review.md` §3).

**Three predefined layouts and Reset Layout — thin callers over
already-legal state.** `PredefinedLayouts` defines Engineering/Review/
Documentation, each a fixed combination of `WorkspacePanelPlacement`
values plus this Work Package's own Desktop-local Output/pin state;
applying one calls `IWorkspaceLayout.SetPlacement` — the same member
every ordinary manual resize already calls. Reset Layout, a real menu
affordance for the first time, calls `IWorkspaceLayout.ResetToDefault()`
— itself already a real `WP 8.0B` contract member, never previously
reachable from the UI.

**Saved layouts, restore-on-startup, and persistent panel visibility/
splitter positions were found already fully real since `WP 8.1A`**
(`WorkspaceState`/`ADR-0064`), confirmed by direct re-read rather than
assumed or reimplemented. This Work Package's own genuinely new
Desktop-local state (Collapse/Auto-Hide/Output visibility and size)
persists through a second, sibling `ISettingsProvider` key
(`DesktopPanelUiState`, `"Workspace.Desktop.PanelUiState"`) — the
identical established pattern (`ADR-0064`) applied a second time to a
second, independent concern, saved from a new
`MainWindow.SaveDesktopUiStateAsync()`, called alongside (never
inside) `WorkspaceHost.ShutdownAsync` from `App.cs`'s own
`ShutdownRequested` handler.

**Zero changes to any of the twelve frozen `WP8.0B` Workspace
contracts — independently re-confirmed, a stronger compatibility
result than `WP 10.2A`'s own one disclosed additive `IWorkspaceManager`
extension.** `IWorkspacePanel` gained a fourth implementer
(`OutputPanel`), never a new member — the contract's own already-
documented extensibility, not a change to it. Zero new ADRs this Work
Package, independently assessed as correct against Engineering
Governance §5's own ADR-creation criteria, not a missed decision
(`WP10.2B Architecture Review.md` §4).

**One genuine, disclosed, cosmetic finding: `TD-39`.**
`PanelHostControl`'s own opaque overlay background (required once a
panel can be reparented as an Auto-Hide flyout directly over the
Document Area) is a fixed `Brushes.White`, not theme-variant-aware —
found to be the identical, already-shipped, **previously unregistered**
limitation `CommandPaletteOverlay` (`WP 10.0B`, `Brushes.Black`) already
carries for this platform's only other overlay control. Both surfaced
and registered together for the first time (`TD-39`; `FCR-0067`).
Cosmetic only — no functional defect.

**One genuine, disclosed, out-of-scope governance finding.** While
updating the Academy Register, a direct row-count of its own `## 03
Work Packages` table (72 rows) against the real, on-disk retrospective
file count (84) found eleven real, already-shipped files
(`WP9.3A`–`WP10.2A`, both `WP9.9.0` passes) each named once in that
field's own historical narrative but never added as their own table
row — the identical class of gap `WP 9.8B` was commissioned
specifically to close in a sibling register. This Work Package's own
row was added; backfilling the other eleven is named as a candidate
for a future dedicated governance Work Package (reinforcing `FCR-0005`),
not attempted mid-implementation here.

**Zero Engineering Domain changes, zero Runtime changes, zero
forbidden-scope items — confirmed independently, not merely claimed.**
No floating OS windows (the Auto-Hide flyout is an in-window `Grid`-
attached-property overlay on the same control, never a second
`Avalonia.Controls.Window`), no multi-monitor code, no ribbon (the
existing Menu/Toolbar/Navigation Framework triad extended, never
replaced), no Digital Thread graph code — confirmed by direct `git
diff --stat` and independent per-contract review.

**Verification**: `src/TempestOS.slnx`, Debug and Release, both 0
Warnings/0 Errors. Combined test suite: **2097/2097 passing** (2040
`Tempest.Core.Tests`, unchanged — zero Domain/Runtime files touched;
57 `Tempest.Desktop.Tests` — 29 existing + 28 new across five test
files), `Tempest.Desktop.Tests` re-run three times total after the new
tests existed, `Tempest.Core.Tests` run twice (Debug, Release), zero
failures and zero flakes observed across every run.

One Implementation Report, plus six required reviews (Engineering,
Security, Systems Engineering, Architecture, UX, Performance), one
Academy Concept Guide (`24-docking-and-workspace-layouts.md`), one
Academy Retrospective (`WP10.2B-docking-and-workspace-layouts.md`) —
all under `docs/releases/v0.10.0/`/`docs/academy/`. Governance
registers updated: ADR Register (reviewed, zero new ADRs, 95
unchanged; a stale "Related ADRs: All 94" field also found and
corrected to 95), Documentation Register, Academy Register (23 → 24
Runtime Architecture, 83 → 84 Work Packages, plus the table-gap
finding above), Technical Debt Register (`TD-39` added, 38 → 39, 32
Open), Future Capability Register (`FCR-0067` added, Identified;
`FCR-0005` reconfirmed, reinforced), Interface/Module/Dependency
Injection/Platform Services Registers and `docs/architecture/Platform
Service Map.md` (each reviewed, confirmed unaffected).

**Recommendation: sound, complete — every one of the fourteen named
scope items independently traced to a specific implementation member
and a specific passing test (`WP10.2B Systems Engineering Review.md`
§1); zero-contract-change independently re-verified at the per-
contract level, not merely asserted (`WP10.2B Architecture Review.md`
§2, `WP10.2B Engineering Review.md` §6); the one genuine cosmetic
finding correctly diagnosed and correctly scoped, not overstated
(`WP10.2B Engineering Review.md` §4).**

Closed with "await Product Owner instruction before `WP 10.3A`" — `WP
10.3A` (above) is that direct continuation.

### `WP 10.2A` Summary (for reference)

`v0.10.0`'s fifth Work Package — the first UI-focused implementation pass since `WP 10.1A`,
transforming `Tempest.Desktop`'s own working-but-minimal shell into a
modern, professional engineering application UI across six named areas
(Project Explorer, Property Inspector, Document Area, Status Bar,
Navigation, Visual Design), plus Accessibility and Performance, while
preserving every existing Engineering capability unchanged.

**A genuine, `WP 9.0A`-disclosed platform gap, found and closed.**
Reading every discipline's own composition-root registration file
directly surfaced it: every discipline already had a real, tested
`Rename*Command`/`Delete*Command` pair — but no interactive surface,
console or desktop, had ever dispatched one against an arbitrary
selected object. `MechanicalWorkspaceRegistration`'s own `WP 9.0A`
remarks named the exact missing caller explicitly: "a future
context-menu action." This Work Package's own "inline rename"/"editable
controls where appropriate" requirements are exactly that caller.

**`ADR-0096` — the one genuine, disclosed contract extension.**
`IWorkspaceManager` gains five new, additive members
(`RegisterRenameFactory`/`RegisterDeleteFactory`/`CanRename`/`CanDelete`/
`RenameObjectAsync`/`DeleteObjectAsync`), mirroring `RegisterFacetProvider`'s
own `ADR-0082` shape and precedent exactly. Dispatch reuses the
existing, unmodified `CommandHandlerTable.DispatchAsync(ICommand, ...)`
primitive — the same runtime-type-keyed lookup `ICommandRegistry.InvokeAsync`
already uses internally; no new command class was written anywhere.
All six discipline registration files were updated to register their
own real factories; two disciplines are honestly incomplete, disclosed:
Requirements' three Kinds register Delete only (no `Rename*Command`
exists for this discipline by design), and Calculations' synthetic
`"CalculationTemplate"` Kind registers neither (never a real Domain
object).

**Project Explorer**: real context menus (Open/Rename/Delete, honestly
enabled per Kind), multi-select, inline rename (`F2` or menu), text
filtering (client-side, ancestor-preserving), a clickable breadcrumb
bar, and drag/drop preparation architecture (a real drag begins with
real payload and real visual feedback; `Drop` is a documented,
deliberate no-op — reparenting needs a second, larger, not-yet-uniform
`Move*Command` decision, named as `FCR-0066`).

**Property Inspector**: collapsible `Expander` sections per facet
group; a real, working editable Name field (`ADR-0096`) gated honestly
by `CanRename`; a Lifecycle summary derived from existing status-shaped
facets (never duplicated — see below); an honest Validation-summary
placeholder (no per-object validation read exists anywhere in the
Workspace layer yet).

**Document Area**: pinned tabs (sort before unpinned, lose their close
glyph while pinned) and active-tab highlighting. "Empty workspace
experience" and "loading placeholders" are both disclosed, not
separately built — the former is already realised by the permanent
Cockpit Home tab (`WP 10.1A`, `ADR-0069`); the latter has no code path
that would ever show one today.

**Status Bar**: replaced entirely — six real segments (Current
Project, Selected Object, Active Workspace, Host State, Diagnostics,
Notifications), two of them honest, disclosed placeholders (no
"current project"/notification-count concept exists yet), four backed
by live reads.

**One genuine defect found and fixed during this Work Package's own
Engineering Review, before any commit.** The Property Inspector's
first Lifecycle-extraction implementation identified status-shaped
facets correctly but never removed them from their own original facet
group — every such fact appeared twice. Fixed in place (`Refresh()` now
excludes the extracted set via `Except`), proven by a new, dedicated
regression test that walks the real rendered visual tree, not merely
the model.

**Zero Engineering Domain changes, zero Runtime changes, zero
forbidden-scope items — confirmed, not merely claimed.** No floating
windows, no docking framework beyond the existing `DockingGrid`, no
multi-monitor support, no ribbon, no Digital Thread graph anywhere in
this Work Package's own diff, confirmed by direct `git diff --stat`
and manual review.

**Verification**: `src/TempestOS.slnx`, Debug and Release, both 0
Warnings/0 Errors. Combined test suite: **2069/2069 passing** (2040
`Tempest.Core.Tests` — 2029 existing + 11 new `WorkspaceManagerTests`;
29 `Tempest.Desktop.Tests` — 22 existing + 7 new
`WorkspaceModernisationTests`), re-run at least twice per
configuration, zero flakes observed.

One Implementation Report, plus six required reviews (Engineering,
Security, Systems Engineering, Architecture, UX, Performance), one
Academy Concept Guide (`23-workspace-modernisation.md`), one Academy
Retrospective (`WP10.2A-workspace-modernisation.md`) — all under
`docs/releases/v0.10.0/`/`docs/academy/`. Governance registers updated:
ADR Register (`ADR-0096` added, 94 → 95; `ADR-0095` remains reserved),
Documentation Register, Academy Register (22 → 23 Runtime Architecture,
82 → 83 Work Packages), Future Capability Register (`FCR-0066` added,
Identified — the disclosed drag/drop-reparenting future work), Technical
Debt Register (reviewed, zero new items — the one defect found was
fixed before commit, never reaching TD-entry status), Module/Interface/
DI/Platform Services Registers and `docs/architecture/Platform Service
Map.md` (each reviewed, confirmed unaffected).

**Recommendation: sound, complete — every named requirement
independently traced to a specific implementation member and, where
applicable, a specific test (`WP10.2A Systems Engineering Review.md`
§1); the one genuine contract extension correctly justified, minimally
scoped, and precedented (`WP10.2A Architecture Review.md` §2–3); the
one genuine implementation defect found and fixed before sign-off, not
merely disclosed (`WP10.2A Engineering Review.md` §3).**

Closed with "await Product Owner instruction before `WP 10.2B`" — `WP
10.2B` (above) is that direct continuation.

### `WP 10.1B` Summary (for reference)

`v0.10.0`'s fourth Work Package, and its first pure hardening/root-cause
pass. Investigated and genuinely resolved `TD-26` and `TD-37`, both
previously disclosed, both previously only mitigated one layer up,
never fixed at their own true source. `TD-26`: `WorkspaceManager.StartAsync`
now waits for `IDiagnosticsProvider.HostState == HostState.Running`,
fixed at its own source for the first time since `WP 9.0A`.
`TD-37`: fully root-caused via direct file-system evidence — a durable,
cross-launch `IPersistenceStore`-backed uniqueness index (`ADR-0041`)
colliding with fixed, literal sample-module identifiers on a second
real launch, never a double-invocation as previously speculated — fixed
via idempotent seeding in all four affected `Tempest.Samples` modules,
plus genuine per-test persistence isolation in `Tempest.Desktop.Tests`.
`EngineeringCockpitTests`' own three previously honest-empty assertions
began reading real, stable, live data as a direct consequence. One new,
disclosed, deliberately-unfixed finding: `TD-38`
(`EngineeringObjectFactory<T>` enforces no business-identifier
uniqueness of its own — an Engineering Domain change, out of scope,
named for a future Work Package). Zero Engineering Domain changes, zero
Workspace contract redesign, zero UX redesign, zero new ADRs.
Verification: 2051/2051 combined tests passing, both Debug and Release
clean. Recommendation: sound, complete. Closed with "await Product
Owner instruction before `WP 10.2A`" — `WP 10.2A` (above) is that direct
continuation.

### `WP 10.1A` Summary (for reference)

`v0.10.0`'s third Work Package, and its second real implementation.
Built the complete graphical Engineering Cockpit over `Tempest.Desktop`
(`WP 10.0B`), auditing every disclosed Cockpit placeholder since
`WP 8.1C` and upgrading six to real data
(`OpenDecisions`/`BlockedItems`/`Health`/`HealthScoreDisplay`/
`RiskSummary`/`DigitalThreadSummary`/`UpcomingMilestones`) by reading a
Domain family (`IDecision`/`IRisk`/`IHazard`/`IMilestone`/`ITask`/
`IAction`, `WP 8.2C`) no Workspace surface had ever read before. Two
placeholders (`FavouriteProjects`, `OverdueActions`) reconfirmed
genuinely honest — no platform capability backs either; a new, real,
clearly-distinguished `OpenTaskCount` substitute was added alongside the
second.

**`ADR-0069` realised literally for the first time.**
`DocumentAreaView.SetHomeTab` gives the Document Host a permanent,
non-closable first tab — the Engineering Cockpit, exactly as that ADR
named it, "the Workspace's own default landing screen."
`CockpitView`/`CockpitCardControl` present all twenty named regions as a
responsive, card-based dashboard; `HealthColors` is the Engineering
Colour Language's own first concrete instantiation.

**Significant new finding: a genuine, pre-existing, platform-wide
sample-module registration defect (`TD-37`).** Four `Tempest.Samples`
modules each genuinely failed their own `InitialiseAsync`, colliding
against their own literal Id — confirmed reproducible, confirmed not
caused by this Work Package's own code, root cause not fully diagnosed
at the time (deliberately, outside this Work Package's own
Cockpit-focused scope — fully root-caused and fixed by `WP 10.1B`,
above). Direct, disclosed consequence at the time: `RiskSummary`/
`UpcomingMilestones`/`OpenTaskCount` correctly, honestly reported
empty/zero.

Verification: 2045/2045 combined tests passing, both Debug and Release
clean. Recommendation: sound, complete. Closed with "await Product
Owner instruction before `WP 10.1B`" — `WP 10.1B` (above) is that
direct continuation.

### `WP 10.0B` Summary (for reference)

`v0.10.0`'s second Work Package, and the first this release to write
real implementation code. Built `Tempest.Desktop` — TempestOS's first
graphical desktop application — over the unchanged `WP 10.0A`
architecture and the unchanged six-discipline Engineering Workspace.

**`ADR-0094` resolved `WP 10.0A`'s own first reserved question.**
Avalonia 11.2.3 selected and justified — cross-platform, MIT-licensed,
a real headless testing mode letting every "Demonstrate" requirement
become an actual passing test. One transitive vulnerability
(`Tmds.DBus.Protocol` 0.20.0, `GHSA-xrw6-gwf8-vvr9`) found and
remediated by a direct version pin.

**Zero engineering functionality change, zero Workspace contract
redesign — confirmed.** `EngineeringWorkspaceComposer` — extracted from
`Tempest.App`'s own console `Program.cs` — is the single, shared
composition sequence both the console and the graphical presentation
layer call, structurally guaranteeing all six real disciplines load
identically in both.

**Two small, disclosed, non-contract changes made in
`Tempest.App.Workspace`:** an `InternalsVisibleTo("Tempest.Desktop")`
grant; and a genuine, pre-existing defect found and fixed —
`ProjectExplorer.Id`/`PropertyInspector.Id` were `Guid.NewGuid()` per
instance, meaning `ADR-0064`'s own session persistence could never
correctly restore either panel across a real process restart. Found by
this Work Package's own first-of-its-kind test; fixed via a fixed,
well-known `Guid` constant; registered and immediately closed as
`TD-35`.

**`TD-26` was directly, reproducibly hit for the first time.**
Mitigated one layer up (`WorkspaceHost`'s own bounded poll for
Navigation readiness — later strengthened by `WP 10.1A`, above), not
fixed at `WorkspaceManager`'s own source.

**All eight "Demonstrate" requirements proven as real, passing tests.**
Verification: 2036/2036 combined tests passing, both Debug and Release
clean. Recommendation: sound, complete. Closed with "await Product
Owner instruction before `WP 10.1A`" — `WP 10.1A` (above) is that
direct continuation.

### `WP 10.0A` Summary (for reference)

Programme 10's own opening Work Package, `v0.10.0`'s first —
architecture and specification only, per its own explicit "No
implementation. No code changes. No contract changes" constraint,
confirmed by direct `git status` check: zero `src/`/`tests/` files
touched.

**This project's first two ADR supersessions**, across what was then
93 ADRs. `ADR-0092` superseded `ADR-0066` (`WP 8.0B`) — the Engineering
Workspace's presentation moves from a Terminal User Interface to a
graphical desktop application, resolving `ADR-0066`'s own named
reversal condition directly against the Product Owner's own Programme
10 commissioning. `ADR-0093` superseded `ADR-0065` (`WP 8.0A`) —
Digital Thread/Object Relationship visualisation moves from a flat
list to a progressively-expandable node-link graph, while explicitly
carrying forward `ADR-0065`'s own core finding (no new platform
traversal capability needed).

**Zero Workspace contract change required for either decision** —
independently re-verified by direct read of `IWorkspaceLayout`,
`WorkspacePanelPlacement`, `WorkspaceDockPosition`, and every
Kind-keyed registration contract: every one was already
rendering-agnostic since `WP 8.0B`. Two gaps named and reserved, not
designed speculatively: `ADR-0094` (concrete desktop UI framework —
resolved by `WP 10.0B`, above) and `ADR-0095` (floating/multi-monitor
panel contract extension — remains reserved).

**All thirty UX topics the controlling instruction named were
addressed**, 30/30 coverage independently verified. Seven UX documents,
two new ADRs, four independent reviews, one Academy Concept Guide, one
Academy Retrospective — eleven deliverables against the controlling
instruction's own eleven named items. Recommendation: sound, complete,
both supersessions independently re-justified rather than accepted on
the issuing ADRs' own say-so. Closed with "await Product Owner
instruction before `WP 10.0B`" — `WP 10.0B` (above) is that direct
continuation.

### `WP 9.9.1` Summary (for reference)

**`WP 9.9.1` — Product Owner Release Execution.** `v0.9.0`'s own
closing Work Package — release mechanics only, no implementation, no
architecture, no governance changes (per its own explicit scope, this
document was not touched by `WP 9.9.1` itself). `VERSION` updated
`0.8.0` → `0.9.0`; working tree inspected (101 pending changes, all
confirmed expected release content) then staged in full (161 files:
146 added, 15 modified); a single release commit created (`Release:
TempestOS v0.9.0`) — a genuine identity mismatch was caught and
corrected before proceeding (the first commit attempt auto-detected
`Steven Kreczman <stevenk@tempest-engineering.co.uk>`, inconsistent with
every prior commit's `kreczmans-creator <kreczmans@gmail.com>`;
corrected via `git commit --amend --reset-author`, changing the final
hash from `2128bba` to `9f258f1`). **Merge into `main`: assessed and
found not required** — no feature branch ever existed in the real
repository (see Current Development Branch, above), disclosed plainly
rather than fabricating a merge step. Annotated tag `v0.9.0` created,
verified pointing at the release commit. **Not pushed, no GitHub
Release published, no branches deleted**, per this Work Package's own
explicit exclusions — prepared everything, stopped immediately before
any remote operation. Three deliverables produced under
`docs/releases/v0.9.0/`, prefixed `WP9.9.1` (Product Owner Release
Summary, Git Command Transcript, Final Release Checklist).

**Disclosed, observed after this Work Package's own close, not
performed by it:** the Product Owner subsequently executed the
remaining steps directly — committing this Work Package's own three
deliverables (`9f64700`, `Docs: Add WP 9.9.1 release execution
deliverables`), moving the `v0.9.0` tag to that commit, and pushing
both `main` and the tag to `origin` — confirmed by `git fetch` showing
`origin/main` matching local `HEAD` exactly and the remote tag present.
This is recorded here as an observation made at the start of `WP
10.0A`, not as part of `WP 9.9.1`'s own work, since it happened outside
any Work Package's own tool-call record.

### `WP 9.9.0` (Second Pass) Summary (for reference)

A second, independent release-readiness verification pass for `v0.9.0`,
commissioned by the Product Owner with the identical controlling
instruction as the first pass, after `WP 9.8B` closed that first pass's
own top standing recommendation — a deliberate "verify, remediate,
re-verify" sequence, the first this project performed in full.
**Verification only.** The first pass's own five deliverables and `WP
9.8B`'s own five deliverables were left exactly as written, per "never
silently modify historical records" — this pass's own deliverables are
new, distinctly-named artifacts alongside them (suffixed "(Second
Pass)"), except `ReleaseNotes.md`/`Retrospective.md`, both living
release-level documents updated in place.

**Resolved, re-confirmed independently:** the four-Engineering-Foundation-framework
Platform Service Register/Map gap. This pass did not trust `WP 9.8B`'s
own claim of closure — it independently re-derived the same
five-document consistency check and reached the identical conclusion by
its own separate route. **The first release-closing review in this
project's history to find this gap closed rather than open.**

**New finding this pass: `TD-34`.** A fresh, full test-suite
verification (5 runs, one more than the first pass performed) caught a
live instance of a test flake informally disclosed by name since
`WP 6.3` (`CompositeLogSinkTests`'s own intermittent, cross-test-class
`Console.Error`-capture race) but never once actually observed by any
of the four prior release-closing reviews that named it, nor by this
Work Package's own first pass. Root-caused directly (a race between
`CompositeLogSink.Write`'s own `Console.Error.WriteLine` failure report
and any concurrently-running `[Collection("Console output
capture")]`-tagged test's own console redirection), confirmed
non-reproducible in isolation (5/5 further isolated runs passed), and
formally registered for the first time. **Not Release Blocking.**

**Build**: 4/4 projects, 0 warnings, 0 errors, both Debug and Release.
**Tests**: 2026/2026 passing in 4 of 5 full-suite runs this pass (the
one exception being the `TD-34` instance, resolved on immediate
re-run); zero regression against the first pass's own 2026-test
baseline.

Zero new ADRs. One new Technical Debt item (`TD-34`). Zero new Future
Capability entries — `FCR-0005` reconfirmed still Identified. Five
completion deliverables produced under `docs/releases/v0.9.0/`, each
prefixed `WP9.9.0` and suffixed "(Second Pass)", plus
`ReleaseNotes.md`/`Retrospective.md` updated in place, plus one Academy
Retrospective. **Recommendation, reconfirmed: `v0.9.0` APPROVED.**
Closed with "await Product Owner release" — `WP 9.9.1` (above) is that
direct continuation.

### `WP 9.8B` Summary (for reference)

A dedicated, narrow-scope governance Work Package — five documents,
four frameworks, zero code — closing the single most persistent
disclosed governance finding in this project's history: the
four-Engineering-Foundation-framework Platform Service Register/Map
gap, first found by `WP 7.3A` (2026-07-30), confirmed still open by
`WP 7.4.0` and `WP 8.9.0`, and confirmed open a third consecutive
release-closing review by `WP 9.9.0`'s own first pass, whose own
Product Approval Report named making a firm decision about it as
`v0.9.0`'s own top standing recommendation. **Verification and
documentation only.**

**Disclosed sequencing.** Commissioned **after** `WP 9.9.0`'s own first
pass despite carrying an earlier number (`9.8B`, inside the `WP
9.6A`–`WP 9.8A` range `WP 9.5A`'s own controlling instruction
explicitly skipped) — not an error: this Work Package addressed that
first pass's own top standing recommendation before the release was
finalised. `WP 9.9.0`'s own second pass (above) independently
re-confirmed this Work Package's own claimed closure.

**Finding: the disclosed gap was real, but narrower than three
successive reviews' own repeated description implied** — confined to
exactly two of five reviewed governance documents (`Platform Services
Register.md`, `Platform Service Map.md`); `Dependency Injection
Register.md`, `Module Register.md`, and `Interface Register.md` had
each already been correctly backfilled by `WP 7.1F`, long before the
gap was first named.

**Backfill performed — four rows, four complete sections, zero
invention.** Engineering Data Model, Materials, Engineering
Calculations, Verification, all Implemented, `WP 7.1A`/`WP 7.1C`/
`WP 7.1D`/`WP 7.1E`, `ADR-0053`/`ADR-0055`/`ADR-0056`/`ADR-0057`. Every
fact independently re-verified against real source. Two further,
previously-undisclosed findings surfaced and corrected in the same
pass: a distinct arithmetic error in the Platform Services Register's
own headline total (claimed "27," true row count 26, now 30); two
stale "Depended on by" entries in `Platform Service Map.md`.

**Build/Test**: unaffected — `git status` confirmed zero `src/`/`tests/`
file touched. Five completion deliverables produced under
`docs/releases/v0.9.0/`, prefixed `WP9.8B`, plus one Academy
Retrospective. **Stops here** — `WP 9.9.0`'s own second pass (above) is
the direct continuation.

### `WP 9.9.0` (First Pass) Summary (for reference)

`v0.9.0`'s own eighth Work Package by completion order (and by intended
number, before `WP 9.8B`'s own disclosed out-of-order commissioning) —
the complete release-readiness review this project's own standing
practice performs before every tagged release (mirroring `WP 5.4`/
`WP 6.8`/`WP 7.4.0`/`WP 8.9.0`), covering all seven `v0.9.0`
implementation Work Packages (`WP 9.0A` through `WP 9.5A`).
**Verification only** — no new functionality, no architectural changes.

**Recommendation: `v0.9.0` APPROVED** for Product Approval, release,
tagging, and merge to `main` by the Product Owner — see `WP9.9.0
Product Approval Report.md`. Not yet acted on by the Product Owner as of
either `WP 9.8B` or `WP 9.9.0`'s own second pass (both above).

**Repository verified clean of release-blocking defects.** 4/4 projects
build with 0 warnings/0 errors in both Debug and Release, from a fully
clean rebuild, plus per-project Release builds. 2026/2026 tests passing
across four full-suite runs (two Debug, two Release — the second
reproducing `scripts/new-release.ps1`'s own exact invocation), plus a
dedicated 516-test scoped run and a flake-check run — zero failures,
zero flakes, zero regressions against `v0.8.0`'s own 1631 tests. Full
arithmetic re-derivation of the test-count and ADR-count chains, zero
drift found. Architecture, Workspace integration, Engineering lifecycle,
Digital Thread, and Cockpit integration all independently re-verified
sound.

**Two governance-completeness findings reconfirmed open at the time —
the first (the four-Engineering-Foundation-framework Platform Service
gap) subsequently closed by `WP 9.8B`, above; the second (the "32 vs.
35 governance documents" count drift) remains open.** Seven dedicated
Security Reviews performed this release — a full recovery from
`WP 8.9.0`'s own disclosed "zero dedicated Security Reviews" gap.

Zero new ADRs. Zero new Technical Debt items. Zero new Future
Capability entries. Five completion deliverables produced under
`docs/releases/v0.9.0/`, prefixed `WP9.9.0`, plus two newly-created
release documents (`ReleaseNotes.md`, `Retrospective.md`), plus one
Academy Retrospective. Closed with "await Product Owner release" — `WP
9.8B`, and subsequently `WP 9.9.0`'s own second pass (both above), are
the direct continuation, the latter finding the release approved a
second time, independently.

### `WP 9.5A` Summary (for reference)

`v0.9.0`'s own seventh Work Package by completion order (fifth by
intended number), and the sixth to wire a real Engineering Discipline
into the real Engineering Workspace — the complete Manufacturing
Workspace experience, using exclusively the already-real Engineering
Domain, Workspace, and Digital Thread
(`ManufacturingOperation`/`WorkInstruction`/`Inspection`, `WP 8.2C`),
integrated into the Workspace, Engineering Cockpit, and Digital Thread.

**Disclosed sequencing.** This Work Package's own controlling
instruction closed with "await Product Owner instruction before
`WP 9.9.0` Release Preparation" — skipping `WP 9.6A` through `WP 9.8A`
entirely, none of which was named or reserved anywhere in this
repository. `WP 9.9.0` (above) is that next Work Package, now itself
the Current Work Package.

The fourth real-discipline Work Package to require **zero Domain-layer
(`Tempest.Core`) changes**, after `WP 9.2A`, `WP 9.4A`, and `WP 9.3A`.
Nine of thirteen named scope items needed zero new Domain representation
— Manufacturing BOM already worked before this Work Package wrote a
single line of code (`Mechanical.SetBomLineCommand`, `WP 9.0B`, already
Kind-agnostic, confirmed by direct read and by a dedicated test
dispatching it against a live `"ManufacturingOperation"`); Manufacturing
Assemblies/Parts reuse the existing Mechanical Kinds directly;
Manufacturing Resources/Tooling/Fixtures are `Classification`-tagged
`"Document"` objects, extending `ADR-0088`'s own taxonomy; Supplier
Operations are `Classification`-tagged `ManufacturingOperation` objects
linked `"manufacturedBy"` a real Supplier. Routings needed one genuine,
disclosed design decision: a `Classification`-tagged
`ManufacturingOperation` used as a structural container, its own real
`IHasParent` children sequenced via the existing `IHasBomLine.ItemNumber`
field (`ADR-0091`).

**A disclosed, deliberate first for this project — genuine
cross-Work-Package read-side reuse.** `"WorkInstruction"`/`"Inspection"`
register `Documents`'/`Verification`'s own already-shipped Property
Facet Provider and Workspace View types **directly**, constructed with a
different Kind string — both already generic over their own Kind
parameter, confirmed by direct read; zero new facet/view code was
written for either, verified correct by dedicated tests. Recording an
Inspection result reuses `Verification.RecordVerificationResultCommand`
directly — already Kind-agnostic. Commands remain this Work Package's
own, never reused, for Command Palette category clarity.

New `Tempest.App.Workspace.Manufacturing` namespace:
`ManufacturingNodeProvider` (five synthetic Explorer categories —
Routings/Operations/Supplier Operations/Work Instructions/Inspections —
over all three Manufacturing Kinds together)/
`ManufacturingWorkspaceView(Factory)`/
`ManufacturingOperationPropertyFacetProvider`, eight commands (Create/
Rename/Edit/Delete/Move/Copy/Duplicate/SetStatus — "Release"/"Archive"
already match `LifecycleState` 1:1, no aliasing needed). Real Engineering
Cockpit Manufacturing KPIs (Manufacturing Objects/Manufacturing
Readiness/Released Items/Open Operations/Supplier Status/Inspection
Status/Production Health) are purely additive — `ManufacturingStatus` is
a genuinely new Cockpit member, since `WP 8.1C` never named a
Manufacturing placeholder slot to reuse.

Representative data (`EngineeringManufacturingWorkspaceSampleModule`,
one real Routing with three sequenced Operation steps — Wing Assembly
`InReview`, Spar Web Plate `Released` and referencing the real Beam
Bending Stress Calculation, Shared Fastener Component left `Draft`, the
honest "Open" baseline — one Supplier Operation `manufacturedBy` the
base sample's own real Supplier, one Tooling and one Fixture Document,
one Work Instruction, one Inspection with a real recorded `Pass` result
referencing the Documents sample's own Test Report): real Digital Thread
links spanning all six named nodes this Work Package's own scope lists,
all via already-mapped relationship kinds — the fifth sample module to
depend on four other sample modules' own instances at once, plus one
further, disclosed query-not-inject edge, verified safe by four clean
test runs with zero flakes.

**Build**: 4/4 projects, 0 warnings, 0 errors. **Tests**: 2026/2026
passing (1972 → 2026, 54 new), four full-suite runs, zero failures.

One new ADR (`ADR-0091`). One new Technical Debt item (`TD-33`:
`EngineeringCockpit.FormatCoverage`'s own zero-denominator text is
hardcoded Requirements-specific, already inaccurately reused twice,
worked around locally rather than fixed at the shared source);
`TD-30`/`TD-31`/`TD-32` confirmed still open. Three new Future
Capability candidates recorded (`FCR-0060`–`FCR-0062`). Eight completion
deliverables produced under `docs/releases/v0.9.0/`, prefixed `WP9.5A`,
plus one combined Academy file
(`docs/academy/03 Work Packages/WP9.5A-manufacturing-workspace.md`).
Closed with "await instruction before `WP 9.9.0`" — `WP 9.9.0` (above)
is that next Work Package.

### `WP 9.3A` Summary (for reference)

`v0.9.0`'s own sixth Work Package by completion order (fourth by
intended number), and the fifth to wire a real Engineering Discipline
into the real Engineering Workspace — the complete Verification
Management experience, using the already-real Verification Framework
(`IVerificationService`, `WP 7.1E`) and its Engineering Domain
counterpart (`IVerificationActivity`/`VerificationActivity`, `WP 8.2C`),
integrated into the Workspace, Engineering Cockpit, and Digital Thread.

**Disclosed sequencing.** This Work Package closed the numbering gap
`WP 9.2A` left open ("no `WP 9.3A` begins until the Product Owner gives
further instruction") and `WP 9.4A` recommended filling next
(`FCR-0055`). It was commissioned, completed, and documented **after**
`WP 9.4A` in this repository's own real history, despite carrying the
earlier number `9.3A` — recorded here plainly, not silently reordered
(see Near-Term Roadmap, below, for the full completion-order record). A
further, disclosed inconsistency: this Work Package's own controlling
instruction closed with "Await Product Owner instruction before
`WP 9.4A`" — already complete before that instruction was issued, and in
fact what recommended this very Work Package — a disclosed copy-paste
artifact from the template `WP 9.4A`'s own instruction used, not
silently corrected and not treated as a request to redo `WP 9.4A`.

The third real-discipline Work Package to require **zero Domain-layer
(`Tempest.Core`) changes**, after `WP 9.2A` and `WP 9.4A` — and the
first to also need **zero `Tempest.Core.Verification` changes**:
`VerificationActivity` is an ordinary `EngineeringObjectBase`-derived
object. `IVerificationService.RecordAsync` is a single, caller-driven
action — unlike Calculations' generic-per-Template `ICalculationEngine`,
it needed no `CalculationTemplateRegistry`-equivalent adapter at all:
"Execute," "Record Result," and "Attach Evidence" are realised together
by one command, `RecordVerificationResultCommand` (`ADR-0089`).
"Verification Plan" and "Verification Activity" are one Domain Kind,
distinguished only by `LifecycleState` (`ADR-0090`).

New `Tempest.App.Workspace.Verification` namespace: `VerificationActivityNodeProvider`
(five synthetic Explorer categories by verification Method), nine
commands (Create/Rename/Edit/Delete/Move/Copy/Duplicate/SetStatus/RecordResult).
Real Engineering Cockpit Verification KPIs replace the placeholder card
— `VerificationStatus` (a fixed `Unknown` placeholder since `WP 8.1C`)
is now a real, derived read.

**A genuine, disclosed implementation-time finding, caught by nine
failing tests before any commit:** `VerificationService.RecordAsync`
links its own subject to a new record via the raw document store only,
never through `EngineeringDomainContext.RelationshipRepository`.
`VerificationRecordReader` was corrected to read
`IEngineeringDocumentStore.GetReferencesAsync` directly instead — the
same raw data the Framework's own (permission-gated)
`GetVerificationHistoryAsync` reads internally, un-gated — introducing
no duplication and no functional gap (`TD-32`).

Representative data (`EngineeringVerificationWorkspaceSampleModule`,
four real activities — Inspection `InReview`/no result; Analysis
recorded `Pass`, linking the real Beam Bending Stress Calculation
record and referencing the real sample Material; Test recorded `Fail` —
a genuine, disclosed honest failure demonstration; Demonstration left
`Draft`): real Digital Thread links spanning all eight named nodes this
Work Package's own scope lists — the fourth sample module to depend on
four other sample modules' own instances at once, verified safe by four
clean test runs with zero flakes.

**Build**: 4/4 projects, 0 warnings, 0 errors, both Debug and Release.
**Tests**: 1972/1972 passing (1922 → 1972, 50 new), four full-suite runs,
zero failures.

Two new ADRs (`ADR-0089`, `ADR-0090`). One new Technical Debt item
(`TD-32`); `TD-30`/`TD-31` confirmed still open. Three new Future
Capability candidates recorded (`FCR-0057`–`FCR-0059`). Eight completion
deliverables produced under `docs/releases/v0.9.0/`, prefixed `WP9.3A`,
plus one combined Academy file
(`docs/academy/03 Work Packages/WP9.3A-verification-management-workspace.md`).
Closed with "await instruction before `WP 9.5A`" — `WP 9.5A` (Manufacturing
Workspace) is that next Work Package, now itself the Current Work
Package, above.

### `WP 9.4A` Summary (for reference)

`v0.9.0`'s own fifth Work Package, and the fourth to wire a real
Engineering Discipline into the real Engineering Workspace — the
complete Engineering Documents experience, using the already-real
Documentation & Design Domain family (`IDocument`/`Document`,
`IDrawing`/`Drawing`, `ICadModel`/`CadModel`, `WP 8.2C`), integrated into
the Workspace, Engineering Cockpit, and Digital Thread.

**Disclosed numbering gap (as recorded by `WP 9.4A` itself).** `WP 9.2A`
closed with "no `WP 9.3A` begins until the Product Owner gives further
instruction"; no `WP 9.3A` deliverable existed anywhere in this
repository at the time. The Product Owner's own instruction
commissioning `WP 9.4A` named it directly, skipping `9.3A` — recorded
plainly, not silently renumbered or backfilled. That gap is now closed
by `WP 9.3A` itself (see Current Work Package, above, and Near-Term
Roadmap, below).

The second real-discipline Work Package to require **zero Domain-layer
(`Tempest.Core`) changes**, after `WP 9.2A` — `Document`/`Drawing`/
`CadModel` are ordinary `EngineeringObjectBase`-derived objects, so every
Document Management verb is realised entirely through the same casts
(`IRenamable`/`IHasParent`/`IDeletable`/`IHasRevisions`/`IHasLifecycle`/
`IHasAttachments`) `CalculationsWorkspaceRegistration` already
established. Six of this Work Package's own eight named Document types
(Specification/Report/Procedure/Standard/Datasheet/External Reference)
have no dedicated Domain Kind anywhere in the platform — realised as
plain `"Document"` objects distinguished by the existing
`IHasMetadata.Classification` facet (`ADR-0088`), never five new
concrete Domain classes. This Work Package's own named statuses (Draft/
Review/Approved/Released) map one-for-one onto `LifecycleState`'s own
existing values — unlike Calculations' five-verb aliasing (`ADR-0087`),
no descriptive alias command was needed.

New `Tempest.App.Workspace.Documents` namespace mirrors `.Calculations`'s
own shape, one level simpler (no Template/Execute concept):
`DocumentsNodeProvider` (nine synthetic Explorer categories over three
real Domain Kinds — Drawings/CAD Models/Specifications/Reports/
Procedures/Standards/Datasheets/External References/Uncategorized) /
`DocumentsWorkspaceView(Factory)`/`DocumentsPropertyFacetProvider`, nine
commands (Create/Rename/Edit/Delete/Move/Copy/Duplicate/SetStatus, plus
one genuinely new command, `AttachDocumentCommand`, wrapping the
already-existing `IHasAttachments.AttachAsync` for the first time).
Search needed zero new code — `ProjectExplorer.FilterAsync` (`WP8.1B`)
already generic over whatever provider is registered. Real Engineering
Cockpit Documents KPIs (Total Documents/Draft/Review/Approved/Released/
Outstanding Reviews/Missing Evidence/Documentation Health) replace the
placeholder card — `DocumentationStatus` (a fixed `Unknown` placeholder
since `WP 8.1C`) is now a real, derived read; "Missing Evidence" is a
disclosed heuristic (zero Attachments and zero Digital Thread links),
all disclosed explicitly.

Representative data (`EngineeringDocumentsWorkspaceSampleModule`, nine
real documents: a General Arrangement Drawing and a Detail Drawing —
real `DrawingNumber`s, the Detail Drawing structurally nested under the
GA Drawing — a Specification, a Test Report (carries a real
`Attachment`), a Design Report, a Material Datasheet, a Procedure, a
Standard, and an External Reference): real Digital Thread links to the
Mechanical sample data's own Wing Assembly/Spar Web Plate, the
Requirements sample's own Requirements, the Calculations sample's own
Beam Bending Stress Calculation, the base sample's own already-live
Risk, and one newly-created `Decision` — the third sample module to
depend on other sample modules' own instances, this time three at once
plus one further, disclosed query-not-inject edge (the base sample's
own live Risk, queried by Kind rather than constructor-injected),
verified safe by four clean test runs with zero flakes. The External
Reference document is deliberately left with zero Attachments and zero
relationships — the Cockpit's own real "Missing Evidence" KPI's sole,
honest example.

**Build**: 4/4 projects, 0 warnings, 0 errors, both Debug and Release,
clean rebuild, verified both per-project (`Tempest.App`/`Tempest.Samples`
Release builds) and via `src/TempestOS.slnx`. **Tests**: 1922/1922
passing (1865 → 1922, 57 new), four full-suite runs across this Work
Package's own verification (two Debug, two Release), zero failures.
**Version**: `VERSION` correctly still reads `0.8.0`, not yet bumped,
per the established "bump after tag" precedent.

One new ADR (`ADR-0088`). One new Technical Debt item (`TD-31`: no
file/URL attachment storage service exists anywhere in the platform —
`Attachment`/External Reference are metadata/placeholder only,
disclosed, not fixed); `TD-30` confirmed still open, its consequences
extended to Documents↔Verification traceability. Zero genuine
implementation defects found (a second consecutive real-discipline Work
Package with none, after `WP 9.2A`). Three new Future Capability
candidates recorded (`FCR-0054`–`FCR-0056`, including `FCR-0055` —
Verification Workspace, this Work Package's own explicit recommendation
for what should follow it). Eight completion deliverables produced under
`docs/releases/v0.9.0/`, prefixed `WP9.4A`, plus one combined Academy
file (`docs/academy/03 Work Packages/WP9.4A-engineering-documents-workspace.md`).
**Stops here — no `WP 9.5A` begins until the Product Owner gives further
instruction.** *(Historical note: `WP 9.3A` was, in real time, completed
after this Work Package, closing the numbering gap this section itself
disclosed — see `WP 9.3A`'s own summary above.)*

### `WP 9.2A` Summary (for reference)

`v0.9.0`'s own fourth Work Package, and the third to wire a real
Engineering Discipline into the real Engineering Workspace — the
complete Engineering Calculations experience, using the already-real Calculation Framework
(`WP 7.1D`) and its Engineering Domain counterpart
(`ICalculation`/`ICalculationSet`, `WP 8.2C`), integrated into the
Workspace, Engineering Cockpit, and Digital Thread. The first of the
three real-discipline Work Packages to require **zero Domain-layer
(`Tempest.Core`) changes** — `Calculation`/`CalculationSet` are ordinary
`EngineeringObjectBase`-derived objects, architecturally much closer to
Mechanical than to Requirements, so every Calculation Management verb is
realised entirely through the same casts (`IRenamable`/`IHasParent`/
`IDeletable`/`IHasRevisions`/`IHasLifecycle`) `MechanicalWorkspaceRegistration`
already established.

A new Workspace-layer type, `CalculationTemplateRegistry` (`ADR-0086`),
is the one genuinely new mechanism: a JSON-marshalling type-erasure
adapter connecting `ICalculationEngine` (generic per Template) to one
non-generic Execute/Recalculate command, without changing the Calculation
Framework itself. Lock/Unlock/Request Review/Approve/Archive are five
descriptive `CommandDescriptor`s over one real `SetCalculationStatusCommand`/
`IHasLifecycle.TransitionAsync` (`ADR-0087`), since no `IApprovalGate`/
`IApproval` implementation exists anywhere in the platform — a
pre-existing gap, now formally registered (`TD-30`).

New `Tempest.App.Workspace.Calculations` namespace mirrors `.Mechanical`'s
own shape, extended by one synthetic, non-Domain `"CalculationTemplate"`
Kind (a read-only Explorer/Property-Inspector catalogue of every
registered Template, addressed by a registry-local Id — Templates have
no Domain identity of their own): `CalculationsNodeProvider`/
`CalculationsWorkspaceView(Factory)`/`CalculationsPropertyFacetProvider`,
ten commands (Create/Rename/Edit/Delete/Move/Copy/Duplicate/SetStatus/
Execute/Recalculate). Search needed zero new code —
`ProjectExplorer.FilterAsync` (`WP8.1B`) already generic over whatever
provider is registered. Real Engineering Cockpit Calculations KPIs
(Total/Draft/Review/Approved/Failed/Out-of-date/Verification Coverage/
Calculation Health) replace the placeholder card — "Failed" maps to a
`Conditional` validation outcome (the Framework's own closed vocabulary
has no literal "Failed"), "Out-of-date" is a disclosed revision-vs-
execution-timestamp heuristic, "Verification Coverage" reports the share
of Calculations executed at least once, all disclosed explicitly.

Representative data (`EngineeringCalculationsWorkspaceSampleModule`, five
new real — if simplified — engineering calculations: Bolt Shear
Capacity, Beam Bending Stress, Bearing Load Capacity, Pressure Vessel
Wall Thickness, Material Selection Margin): one Calculation Set grouping
two of them, a `"basedOnCalculation"` dependency chain, a mix of
lifecycle statuses, one genuine `Conditional` outcome (applied stress
exceeds allowable — a real, honest "Failed" KPI demonstration), one
Calculation revised after execution (a real "Out-of-date" demonstration),
and real Digital Thread links to the Mechanical sample data's own Wing
Assembly/Spar Web Plate and one Requirements sample requirement — the
second sample module ever to depend on other sample modules' own
instances, this time two at once, disclosed directly and verified safe
(identical DI-singleton-plus-ordinal-ordering mechanism `WP 9.1A`
established).

**Build**: 4/4 projects, 0 warnings, 0 errors, both Debug and Release,
clean rebuild, verified both per-project (`Tempest.App`/`Tempest.Samples`
Release builds) and via `src/TempestOS.slnx`. **Tests**: 1865/1865
passing (1808 → 1865, 57 new), four full-suite runs across this Work
Package's own verification (two Debug, two Release), zero failures.
**Version**: `VERSION` correctly still reads `0.8.0`, not yet bumped,
per the established "bump after tag" precedent.

Two new ADRs (`ADR-0086`, `ADR-0087`). Two new Technical Debt items
(`TD-29`: Recalculate cannot resume from a previously-executed input,
since `CalculationRecord` never retained one; `TD-30`: `ICalculationResult`/
`IVerificationResult`/`IApprovalGate` family — declared `WP8.2B` Domain
contracts with zero concrete implementations anywhere in the platform, a
pre-existing gap across two releases, now formally registered for the
first time) — both disclosed limitations, not correctness defects; zero
genuine implementation defects found (the first real-discipline Work
Package with none, consistent with its own zero-Domain-layer-file-touched
footprint). Three new Future Capability candidates recorded
(`FCR-0051`–`FCR-0053`). Eight completion deliverables produced under
`docs/releases/v0.9.0/`, prefixed `WP9.2A`, plus one combined Academy
file (`docs/academy/03 Work Packages/WP9.2A-engineering-calculations-workspace.md`).
Also disclosed: the current development branch's own real name has
silently diverged from what `WP 9.1B`'s own committed documents record
(see Current Development Branch, above) — not this Work Package's own
change, corrected here for the first time. **Stops here — no `WP 9.3A`
begins until the Product Owner gives further instruction.**

### `WP 9.1A` Summary (for reference)

`v0.9.0`'s own third Work Package, and the second to wire a real
Engineering Discipline into the real Engineering Workspace — the
complete Requirements Management experience, using the already-real
Requirements Framework (`WP 7.3A`), integrated into the Workspace,
Engineering Cockpit, and Digital Thread.

Seven new additive `IRequirementsService` methods (SetOwner/SetPriority/
Delete/MoveToGroup/MoveGroup/DeleteGroup/DeleteCollection) plus
`ListCollectionsAsync`/`ListGroupsAsync` (added mid-implementation once
the Explorer tree's own real need for them surfaced) and a
`RequirementGroupDto` parent-resolution fix (`ADR-0084`) — Requirements'
own genuinely different, immutable-snapshot architecture meant `ADR-0080`'s
own facet-composition *mechanism* did not fit, so every new capability
follows `SetStatusAsync`'s own already-proven mutation shape instead. A
new `IRequirementValidationService` reuses `IValidationResult`/
`IValidationDiagnostic`'s own generic result shape — never
`IValidationRule` itself, structurally incompatible with `IRequirement`.
`ISelectionService`/`IWorkspaceContext` gain multi-selection
(`SelectedItems`/`ToggleSelectionAsync`), additively, resolving
`FCR-0039` now that Bulk editing is a real consumer (`ADR-0085`).

New `Tempest.App.Workspace.Requirements` namespace mirrors `.Mechanical`'s
own shape exactly: `RequirementsNodeProvider`/`RequirementsWorkspaceView(Factory)`/
`RequirementsPropertyFacetProvider`, 18 commands (Create/Revise/SetStatus/
SetOwner/SetPriority/Delete/Move/Duplicate/Link, per-Group and
per-Collection Create/Move/Delete, AddToCollection, three Bulk commands).
Search needed zero new code — `ProjectExplorer.FilterAsync` (`WP8.1B`)
already generic over whatever provider is registered. Real Engineering
Cockpit Requirements KPIs (Total/Draft/Review/Approved/Released/
Verification Coverage/Allocation Coverage/Requirement Health/Outstanding
Actions) replace every placeholder card — "Released" maps to the closed
`RequirementStatus` enum's own `Satisfied` value, disclosed, since no
`"Released"` status exists in that platform-wide vocabulary.
`RequirementCollectionExportAdapter` (`Tempest.Samples`, not `Tempest.App`
— required by the project-reference direction) scales `WP 7.3A`'s own
single-requirement Import/Export demonstration to Requirement Set
granularity.

Representative data (`RequirementsWorkspaceSampleModule`): a three-level
Group hierarchy (one root-created-then-moved, proving the storage fix
directly), ten Requirements across six lifecycle statuses, real
allocations to the Mechanical sample data's own Wing Assembly/Spar Web
Plate — the first sample module ever to depend on another sample
module's own instance, disclosed directly and verified safe (DI
singleton registration plus ordinal module-Id initialisation ordering).

**Two genuine implementation defects, found in this same, not-yet-
committed session's own earlier work, fixed in place with regression
coverage — not registered as Technical Debt, since neither was ever part
of a commit or tagged release:** `IRequirementsService.GetEvidenceAsync`
is transitively permission-gated (`ADR-0061`, correct and unchanged);
`RequirementValidationService`, `RequirementsPropertyFacetProvider`, and
`EngineeringCockpit` all originally called it, so a Property Inspector
selection, a validation read, or a Cockpit KPI read could throw for any
principal lacking a narrower capability than "can view this at all" —
fixed in all three call sites by reading `GetRelationshipsAsync` for the
`verifiedBy` link directly instead, found via the Workspace integration
suite against real seeded data, not any Domain unit test. And the
`RequirementGroupDto` parent-resolution ambiguity itself, above.

**Build**: 4/4 projects, 0 warnings, 0 errors, both Debug and Release,
clean rebuild, verified both per-project and via `src/TempestOS.slnx`.
**Tests**: 1808/1808 passing (1738 → 1808, 70 new), four full-suite runs
across this Work Package's own verification (two Debug, two Release),
zero failures. **Version**: `VERSION` correctly still reads `0.8.0`, not
yet bumped, per the established "bump after tag" precedent.

Two new ADRs (`ADR-0084`, `ADR-0085`). One new Technical Debt item
(`TD-28`): Bulk Requirements commands are deliberately plain `ICommand`
(many targets, none more "the" target than another), so no open view
for any touched requirement is automatically refreshed after a bulk
operation — disclosed, not fixed; no data-correctness issue, only a
display-freshness one. `FCR-0039` (multi-selection) resolved; three new
candidates recorded (`FCR-0048`–`FCR-0050`). Eight completion
deliverables produced under `docs/releases/v0.9.0/`, prefixed `WP9.1A`.

### `WP 9.0B` Summary (for reference)

`v0.9.0`'s own second Work Package — Bill of Materials and Configuration
Management over the Mechanical Product Structure `WP 9.0A` delivered.
One new additive Domain facet, `IHasBomLine` (Quantity/Unit of Measure/
Find Number/Item Number/Reference Designator, `SetBomLineAsync`), the
fourth in the `ADR-0080` composition family (`ADR-0083`); Unit of
Measure is a plain string, deliberately never
`Tempest.Core.UnitsAndQuantities.Quantity<TDimension>`.
`Configuration`/`Baseline`/`Release` needed zero new Domain code —
already real since `WP8.2C`. First real use of two previously-unused
`WP8.2C` extension points: `ValidationRuleSet.Register` (five new
`IValidationRule`s) and `IReferenceIntegrityChecker.CheckBaselineMembersAsync`.
Three new Workspace commands (Set BOM Line, Compare Baselines — a real
diff, not placeholder — Validate Configuration), bringing the Mechanical
discipline to nine commands total. The existing Product Structure tree
became the BOM hierarchy by extension, never a second structure.
Product Variants remain placeholder architecture only, exactly as
scoped (`FCR-0044`). Two genuine implementation defects found and fixed
in place, not registered as debt (a `TEMPEST-VAL` code collision;
`ReviseAsync` silently discarding structural/BOM state). One new
Technical Debt item (`TD-27`, a flaky-test/documentation finding,
already largely remediated). One new ADR (`ADR-0083`). Second
consecutive dedicated Security Review. 1695 → 1738 tests (43 new).
Eight completion deliverables, prefixed `WP9.0B`.

### `WP 9.0A` Summary (for reference)

`v0.9.0`'s own first Work Package, on
`feature/v0.9.0-mechanical-foundation`, cut from `main` at the `v0.8.0`
tag — the first Work Package to wire a real Engineering Discipline into
the real Engineering Workspace. `Project`/`Assembly`/`SubAssembly`/
`Part`/`Component`/`Configuration` were already real, tested `WP8.2C`
concrete classes (the Canonical Object Catalogue's own "Conceptual"
marking for all six had gone stale — corrected here, left unedited in
that dated historical artefact, per "never silently modify historical
records"); this Work Package gave them real Workspace presentation and
the one capability they still lacked: structural mutation.

Three new, additive Domain facets — `IRenamable`, `IHasParent`,
`IDeletable` — composed only into the five Product Structure Kinds,
implemented once, unconditionally, in `EngineeringObjectBase`, exactly
mirroring `ADR-0075`'s own composition model (`ADR-0080`). `MoveAsync`
records a new `"groupedUnder"` relationship link to the new parent
rather than removing the old one, so the append-only Relationship
framework keeps a full move history even though a new, separate,
live `ParentId` field is the one value anything renders from — the
frozen `IAssembly.ChildIds`/`ISubAssembly.ParentAssemblyId` stay exactly
as `WP8.2C` shipped them, honestly disclosed as construction-time
snapshots only (`ADR-0081`). The Property Inspector gains a third
Kind-keyed provider category, `IPropertyFacetProvider`, added to the
frozen `IWorkspaceManager` contract as one new, additive member
(`ADR-0082`) — the same `ADR-0067` principle a third time.

Real (non-sample) implementations of all three Kind-keyed Workspace
provider categories — `MechanicalProductStructureNodeProvider`,
`MechanicalWorkspaceViewFactory`/`MechanicalWorkspaceView`,
`MechanicalPropertyFacetProvider` — plus six new `IWorkspaceCommand`/
`ICommand` implementations (Create, Rename, Delete, Move, Copy,
Duplicate), closing `WP8.1B`'s own disclosed "no concrete
`IWorkspaceCommand` is implemented" gap. Three new ADRs
(`ADR-0080`–`ADR-0082`). First dedicated Security Review since `v0.7.0`,
closing `WP8.9.0`'s own disclosed "zero dedicated Security Reviews" gap.
One genuine, pre-existing platform finding disclosed, not fixed
(`TD-26`): `WorkspaceManager.StartAsync` does not await Runtime Host
module initialisation before returning — confirmed pre-existing on the
unmodified `v0.8.0` tag via a disposable `git worktree` comparison, not
introduced by this Work Package. 1631 → 1695 tests (64 new). Eight
completion deliverables, prefixed `WP9.0A`. Drag-and-drop and multi-
selection explicitly deferred (`FCR-0039`/`FCR-0040`), not silently
dropped.

### `WP 8.9.0` Summary (for reference)

`v0.8.0`'s own tenth and closing Work Package — a release-verification-
only pass, no new platform functionality, no architecture change, no
refactoring beyond what resolving a genuine release-blocking defect
would require (none was found). Performed a complete release readiness
review across repository, build, test, version, governance, and
architecture verification for all nine prior `v0.8.0` Work Packages
(`WP 8.0A`–`WP 8.2C`).

**Build**: 4/4 projects, 0 warnings, 0 errors, both Debug and Release,
clean rebuild, verified both per-project and via `src/TempestOS.slnx`
(the exact path `scripts/new-release.ps1` itself uses). **Tests**:
1631/1631 passing, five full-suite-equivalent runs (two Debug, two
Release, plus one via the release script's own solution-file path),
plus a dedicated 225-test scoped run and three targeted flaky-test
probes — zero failures, zero flakes anywhere. **Version**: confirmed
`VERSION` correctly still reads `0.7.0`, not yet bumped, per the
established "bump after tag" precedent.

Found and corrected one genuine arithmetic error (`WP 8.2C`'s own
claimed 39 concrete canonical object classes, corrected to the verified
38, in every living document — left exactly as originally written in
`WP 8.2C`'s own dated historical artifacts, per "never silently modify
historical records"). Disclosed, not fixed, two further findings: the
four-Engineering-Foundation-framework Platform Service Register/Map gap
(`WP 7.3A` found it, `WP 7.4.0` confirmed it open, now confirmed open a
second consecutive release cycle); `WP8.2B`'s own `IRelease : IBaseline
: IConfiguration` three-level interface specialisation, contradicting
that document's own Dependency Rules §6 (a documentation-only
inconsistency, zero functional consequence). Disclosed and weighed
explicitly: **zero dedicated Security Reviews performed this release**
— a genuine departure from `v0.7.0`'s own three-review standard,
mitigated by this release's own narrow, low-risk technical footprint
(no new external attack surface), and named as the single most
important recommendation for whatever begins Programme 9 — closed by
`WP 9.0A`'s own dedicated Security Review, above.

**Recommendation: `v0.8.0` APPROVED** — see `docs/releases/v0.8.0/
WP8.9.0 Product Approval Report.md`. Nine completion deliverables
produced under `docs/releases/v0.8.0/`, prefixed `WP8.9.0`, plus
`ReleaseNotes.md`/`Retrospective.md` fully populated (both previously
stale skeletons) and `WorkPackages.md` corrected (its own stale status
marked superseded, not silently overwritten). **Zero new ADRs.** The
Product Owner subsequently performed the merge, `VERSION` bump, tag, and
push in full — `v0.8.0` is merged to `main` and tagged (see Current
Release, above); this section is retained as `WP 8.9.0`'s own
as-written record, not edited to narrate the release after the fact.

### `WP 8.2C` Summary (for reference)

**`WP 8.2C` — Engineering Domain Implementation.** `v0.8.0`'s own ninth
Work Package, and its fourth implementation — following `WP 8.2B`
directly, the identical architecture-then-contracts-then-implementation
sequence every prior Engineering Core framework already proved out.
Implements the complete `WP 8.2B` contract set exactly as frozen, with
no discipline logic (Requirements/Verification/Calculations/
Manufacturing) written anywhere inside it, per this Work Package's own
explicit constraint.

A new namespace, `Tempest.Core.EngineeringDomain`, compiles all 21
contract files (83 interfaces/enums/records — every WP8.2B interface,
plus one disclosed gap-fill, `IRevisionRecord`, referenced by
`IHasRevisions.GetRevisionHistoryAsync` but never itself defined by
`WP 8.2B`). A shared `EngineeringObjectBase` class implements every
facet unconditionally; 38 of the ~49 canonical objects get a real,
tested concrete class over it, constructed through one of two generic
factory types (`EngineeringObjectFactory<T>`, `EngineeringRelationshipFactory`).
(Corrected 39 → 38 by `WP 8.9.0`'s own release-readiness verification —
`WP 8.2C`'s own documentation miscounted; every class that exists
compiles and is tested correctly, disclosed as an arithmetic
correction, not a functional defect.)
The five already-Implemented canonical Kinds (`Requirement`,
`RequirementCollection`/`Group`, `VerificationRecord`,
`CalculationRecord`, `MaterialSpecification`) compile as Domain
interfaces but receive no competing concrete class — their ownership
stays exactly where `WP 8.2A` already placed it. Every canonical
object's own real storage reuses the existing, shared
`IEngineeringDocumentStore` in production; a new, purely in-memory
`IEngineeringObjectRepository`/`IEngineeringRelationshipRepository`
pair is the genuinely new "in-memory repositories" deliverable.

**Three new ADRs**: `ADR-0077` (shared services reuse the existing
`IEngineeringDocumentStore` in production; the new repository layer,
not a second document store, is the in-memory deliverable — resolving
a tension against `ADR-0072`), `ADR-0078` (the five already-Implemented
Kinds are not given a competing concrete realisation), `ADR-0079`
(object/relationship factories are two generic types, instantiated once
per Kind, not dozens of hand-written ones). A new, twenty-second sample
module (`EngineeringDomainSampleModule`) builds a sixteen-object,
eight-family representative graph, including a real cross-framework
reference to a `Tempest.Core.Materials`-registered
`IMaterialSpecification`. 39 new tests (1592 → 1631), both Debug and
Release, clean rebuild. One completion deliverable, prefixed `WP8.2C`
— see `docs/releases/v0.8.0/WP8.2C Engineering Domain Implementation
Report.md`. **Stops here, awaiting further Product Owner instruction
before the next Work Package begins.**

### `WP 8.2B` Summary (for reference)

**`WP 8.2B` — Engineering Domain Contracts.** `v0.8.0`'s own eighth
Work Package, following `WP 8.2A` directly — the identical
architecture-then-contracts sequence every prior Engineering Core
framework already proved out (`WP 7.0B` → `WP 7.0C`; `WP 7.2B` →
`WP 7.2C`; `WP 8.0A` → `WP 8.0B`). Contracts only: no implementation, no
concrete classes, no repositories, no persistence, no serialization, no
storage technology, no UI. Converts `WP 8.2A`'s own canonical
Engineering Domain Architecture into the complete public contract every
current and future TempestOS module implements against.

Proposed, uncompiled C# for `IEngineeringObject` (the base contract)
plus ten facet interfaces (`IHasBusinessIdentifier`, `IHasMetadata`,
`IHasLifecycle`, `IHasRevisions`, `IHasRelationships`, `ITraceable`,
`IValidatable`, `IHasAttachments`, `ISearchable`) composed, never
inherited, per object (`ADR-0075`); all ~49 canonical object interfaces
across thirteen families, each built from `IEngineeringObject` plus its
own relevant facets; one generic `IEngineeringRelationship` interface
carrying a `RelationshipCategory` enum as descriptive metadata only,
realising all seventeen named relationship categories without
reopening `ADR-0073`'s own already-locked "never a closed enum"
decision (`ADR-0076`); the full Lifecycle, Relationship, Validation, and
Digital Thread contract sets; two factory contracts
(`IEngineeringObjectFactory`, `IEngineeringRelationshipFactory`); eight
sequence diagrams; and a complete dependency-rules/layering analysis.

**Two new ADRs**: `ADR-0075` (composition over inheritance, given a
concrete, checkable shape — small facet interfaces, at most one level
of object-to-object specialisation) and `ADR-0076` (one generic
relationship interface, not seventeen per-category types — resolving a
direct tension between this Work Package's own controlling instruction
and `ADR-0073`'s prior, binding decision). Every contract's own member
list was checked against a real, shipped equivalent before being
proposed — zero contract invented independently of existing shipped
shape. **Zero `src/`/`tests/` change of any kind** — verified directly
against `git status` before commit. Eight completion deliverables,
prefixed `WP8.2B`. **Stops here, awaiting further Product Owner
instruction before the next Work Package — implementation or
otherwise — begins.**

### `WP 8.2A` Summary (for reference)

**`WP 8.2A` — Engineering Domain Architecture.** `v0.8.0`'s own seventh
Work Package, and its first Engineering-Core-facing architecture Work
Package since `WP 7.2B` — following `WP 8.1C`'s own completion.
Architecture only: no implementation, no persistence, no interfaces, no
UI. Defines the complete canonical Engineering Domain Architecture —
every Engineering Object TempestOS recognises, across ~49 named objects
in thirteen families (Programme Hierarchy; Physical & Configuration;
Requirements & Verification; Calculations; Materials; Documentation &
Design; Test & Manufacturing; Supply Chain; Governance & Risk; Process
& Approval; Change & Release; Evidence & Reference; Classification &
Extensibility) — its own identity, lifecycle, relationships,
traceability, governance, and validation rules.

The Core Principle — "everything inside TempestOS is an Engineering
Object" — is not a new claim: it formalises a pattern the four
already-shipped Engineering Core frameworks
(`Tempest.Core.Requirements`/`Verification`/`Materials`/`Calculations`)
had each independently converged on without coordination (a `Kind`-backed
identity over the shared `IEngineeringDocumentStore`; open-string,
unvalidated relationships; a closed, caller-driven lifecycle table).
Five catalogue entries are reconciled as already-`Implemented`
(`Requirement`, `RequirementCollection`/`RequirementGroup`,
`VerificationRecord`, `CalculationRecord`, `MaterialSpecification`);
the remaining forty-plus are honestly marked `Conceptual`.

**Three new ADRs**, each formalising an existing convergence as
binding platform-wide architecture: `ADR-0072` (every canonical object
is an `IEngineeringDocumentStore`-backed `Kind`, never a new storage
hierarchy), `ADR-0073` (relationships are open-string
`DocumentReference`s platform-wide, never a closed enum), `ADR-0074`
(lifecycle status is a common canonical vocabulary, specialised per
object family, never one rigid global enum). `RequirementStatus`
remains exactly as shipped, reconciled as a valid specialisation, no
code change implied. **Zero `src/`/`tests/` change of any kind** —
verified directly against `git status` before commit. Nine completion
deliverables, prefixed `WP8.2A`. See `docs/releases/v0.8.0/WP8.2A
Engineering Domain Architecture.md`.

### `WP 8.1C` Summary (for reference)

**`WP 8.1C` — Engineering Cockpit.** `v0.8.0`'s own sixth Work Package,
and its third implementation. Implements the Engineering Cockpit as the
Workspace's own default landing experience (`ADR-0069`), answering four
questions on every visit: where am I, what needs attention, is the
project healthy, what should I do next. Consumes existing Workspace
services only — `NavigationService` and the existing `ICommandRegistry`
Platform Service for Command Palette integration (`ADR-0070`).
Controlling instruction expanded mid-Work-Package to name many more
cards than `WP8.0C Engineering Cockpit Specification.md`'s own seven
regions — followed in full, disclosed as a product-scope expansion.
Every card with a real Workspace-service backing is a live read; every
other card is fixed, disclosed placeholder content. **Zero new ADRs.**
5 new production files, 3 modified, 40 new tests (1552 → 1592). Zero
new Technical Debt. See `docs/releases/v0.8.0/WP8.1C Implementation
Report.md`.

### `WP 8.1B` Summary (for reference)

**`WP 8.1B` — Navigation & Project Explorer.** `v0.8.0`'s own fifth
Work Package, and its second implementation. Implements the Workspace
Navigation system and Project Explorer exactly as specified across
`WP 8.0A`/`WP 8.0B`/`WP 8.0C`: Navigation Service, navigation history,
breadcrumbs, an Areas panel, the Project Explorer, Kind-keyed node
providers, selection synchronisation, context menus, filtering, search,
and recent items — against representative engineering objects only.
`NavigationService`/`ProjectExplorer` (`WP 8.1A`) extended with
`History`/`RecentItems`/`GoBackAsync`/`GoForwardAsync` and
`CurrentPath`/`EnterAsync`/`ExitAsync`/`FilterAsync`, added as
same-assembly-only extensions, never to the twelve public interfaces.
The Project Explorer populated for the first time with real content (a
fixed, fictional Category → Group → Object tree,
`Tempest.App.Workspace.Samples`), proving `ADR-0067`'s own Kind-keyed
extensibility mechanism end to end. One new ADR (`ADR-0071`), correcting
a worked example inside `ADR-0067` that a discovered module cannot
actually reach `IWorkspaceManager` directly. 7 new production files, 5
modified, 55 new tests (1497 → 1552). Zero new Technical Debt. See
`docs/releases/v0.8.0/WP8.1B Implementation Report.md`.

### `WP 8.0C` Summary (for reference)

**`WP 8.0C` — Engineering Workspace UX Specification.** `v0.8.0`'s own
fourth Work Package — a product-and-UX-design exercise only, no
implementation, no code. Fully specified the complete target Workspace
experience across all 28 named scope areas, as nine deliverables under
`docs/releases/v0.8.0/` (`UX Specification.md` and eight companions).
Two new ADRs: `ADR-0069` (the Engineering Cockpit is the Workspace's
own default landing screen) and `ADR-0070` (the Command Palette is a
first-class, global entry point). A genuine, disclosed gap named for a
future Contract Review: the Properties/Inspector panel split names a
plausible future thirteenth interface (`IDigitalThreadInspector`)
without designing it. **Zero `src/`/`tests/` change of any kind.** Nine
completion deliverables, prefixed `WP8.0C` — see `docs/releases/v0.8.0/
WP8.0C UX Specification.md`.

### `WP 8.1A` Summary (for reference)

**`WP 8.1A` — Workspace Shell.** `v0.8.0`'s own first implementation
Work Package, and its third overall — the shell only, no engineering
functionality, per this Work Package's own explicit constraint.
Implements all twelve `WP 8.0B`-approved contracts, compiled exactly as
specified in a new `Tempest.App.Workspace` namespace, with zero
signature change. **`WorkspaceManager`/`WorkspaceShell` are now
`Tempest.App`'s own default launch target** (`ADR-0068`) — running
TempestOS presents the five-region Workspace shell (Areas, Project
Explorer, Documents, Properties, Status Bar); console `TempestShell`
remains in the repository, fully intact, fully tested, simply no longer
the default. Two disclosed implementation-phase findings, neither
requiring an architectural revisit: `ISettingsProvider` is
`string`-only, not the generic contract `WP 8.0B` proposed;
`ITempestHost` is explicitly single-use, not restart-tolerant as
`WP8.0B Lifecycle Definitions.md` assumed. 27 new production files, 91
new tests (1406 → 1497). One new ADR (`ADR-0068`). Zero new Technical
Debt. One completion deliverable, prefixed `WP8.1A` — see
`docs/releases/v0.8.0/WP8.1A Implementation Report.md`.

### `WP 8.0B` Summary (for reference)

**`WP 8.0B` — Workspace Contracts.** `v0.8.0`'s own second Work Package
— contract review only, no implementation. Defined the complete public
contract for all twelve named Workspace interfaces, each fully
specified in proposed C#, plus the supporting types they genuinely
need. Both ADRs `WP 8.0A` reserved resolved: `ADR-0066` (terminal-based
presentation, not a graphical desktop framework) and `ADR-0067`
(Kind-keyed registration for both object views and Project Explorer
nodes, mirroring `IReportDefinition`/`IReportRenderer<T>`). Four
completion deliverables, prefixed `WP8.0B` — see `docs/releases/v0.8.0/
WP8.0B Workspace Contracts.md`.

### `WP 8.0A` Summary (for reference)

**`WP 8.0A` — Engineering Workspace Architecture.** `v0.8.0`'s own first
Work Package — architecture and design only, no implementation.
Designed the complete Engineering Workspace across all twelve named
areas (workspace philosophy, user journeys, main window layout,
navigation model, Project Explorer, engineering object hierarchy,
docking strategy, view architecture, digital thread visualisation,
workspace state management, extensibility model, interaction patterns)
— a multi-panel evolution of `Tempest.App`'s own composition root,
additive to console `TempestShell`, introducing zero new Platform
Service (`ADR-0062`). Views read Engineering Core/Platform services
directly; mutations dispatch through the existing Command Framework
(`ADR-0063`); layout/session state persists via the existing
`ISettingsProvider` (`ADR-0064`); Digital Thread visualisation composes
existing reads (`ADR-0065`). `ADR-0066`/`ADR-0067` reserved, resolved
by `WP 8.0B` (above). Five completion deliverables, prefixed `WP8.0A` —
see `docs/releases/v0.8.0/WP8.0A Workspace Architecture Document.md`.

### `WP 7.4.0` Summary (for reference)

**`WP 7.4.0` — Release Preparation & Product Baseline.** A release
preparation exercise only — no new platform functionality, architecture
change, bug fix, refactoring, or feature development was performed,
per this Work Package's own explicit constraint. Approved to begin
after `WP 7.3A`'s own completion, per its own closing instruction
awaiting Product Owner instruction for whatever comes next.

Performed a complete release readiness review across seventeen named
areas (repository health, build, test, documentation, Academy,
governance registers, ADR consistency, Work Package traceability,
version consistency, dependency consistency, module/platform-service/
interface/DI inventories, Technical Debt Register, Future Capability
Register, Known Issues, Release Notes). **Build**: 5/5 projects, 0
warnings, 0 errors, both Debug and Release, clean rebuild. **Tests**:
1406/1406 passing, four consecutive full-suite runs, zero flakes.
**Version**: confirmed the root `VERSION` file correctly still reads
`0.6.0` — not a defect, matching this project's own established
precedent that `VERSION` bumps to a new tag only *after* that tag is
cut, a Product-Owner-executed step this Work Package correctly did not
pre-empt.

Found and corrected five governance/documentation staleness findings:
`Documentation Register.md`'s own Directory Map (four stale counts,
`docs/adr/` alone reading 39 against an actual 61); `Governance
Register.md`'s own Compliance Matrix (stale since `WP 6.8`, missing all
twelve `v0.7.0` Work Packages, now backfilled). Disclosed, but did not
fix (outside this Work Package's own scope): `Platform Services
Register.md`/`Platform Service Map.md` still missing rows for the four
Engineering Foundation frameworks (found by `WP 7.3A`, reconfirmed
still open). Populated two previously-stale release-document skeletons
in full: `docs/releases/v0.7.0/ReleaseNotes.md` and `Retrospective.md`.
Corrected `docs/releases/v0.7.0/WorkPackages.md`'s own long-stale
"not started" status, marking the original candidate list superseded
by what `v0.7.0` actually delivered, not deleted.

**Recommendation: `v0.7.0` APPROVED** — see `docs/releases/v0.7.0/
WP7.4.0 Product Approval Report.md`. Five completion deliverables
produced under `docs/releases/v0.7.0/`, prefixed `WP7.4.0`. **Product
Approval was subsequently granted; `v0.7.0` was merged to `main`
(non-fast-forward, `61fb2db`), tagged, and pushed by the Product
Owner.**

### `WP 7.3A` Summary (for reference)

**`WP 7.3A` — Requirements Engine.** The first implementation Work
Package of the Systems Engineering Foundation phase — approved to begin
after Engineering Review APPROVED `WP 7.2C`'s own complete public
contracts.

Implemented `Tempest.Core.Requirements` exactly as `WP7.2C Requirements
Platform Contracts.md` approved, with zero architectural deviation.
Every requirement, collection, and group is an `IEngineeringDocument`;
every relationship (grouping, collection membership, allocation,
traceability) is a `DocumentReference` via `LinkAsync`/
`GetReferencesAsync` — zero new storage or traversal mechanism
introduced anywhere. `RequirementStatusTransitions` enforces the
approved seven-state lifecycle's own exact permitted-transition table,
with zero code path connecting it to `VerificationOutcome` — the
Status/Verification-Outcome separation is now demonstrated in running,
tested code. `GetEvidenceAsync` composes
`IVerificationService.GetVerificationHistoryAsync` with
`GetReferencesAsync` into one read, proving `WP7.2B Digital Thread
Architecture.md`'s own central claim (no new mechanism required) in
code for the first time.

Ratified all four reserved ADRs as its own first act: **`ADR-0058`**
(Platform Service classification, Engineering Data Model reuse),
**`ADR-0059`** (independent representation decisions for status,
identifier, category), **`ADR-0060`** (no compare-and-swap concurrency
mechanism, accepted as new Technical Debt `TD-25`), **`ADR-0061`** (no
internal permission gating, mirroring Materials'/Calculations' own
precedent, articulating a reusable "evidentiary vs. ordinary
operational content" deciding test). 20 new production files, 131 new
tests (1275 → 1406), a new sample module
(`RequirementsSampleModule`, the platform's twentieth). Extended
`docs/engineering/Engineering Principles.md` with four further
principles (29-32) and added a new Academy concept guide
(`16-requirements-engine.md`). Third Work Package overall to include a
dedicated Security Review — zero Release Blocking findings.

One disclosed finding, not a deviation: `WP7.2B`'s own broader
architectural vision for Allocation targets (either a document reference
or an open string) was never carried into `WP7.2C`'s own approved
`LinkAsync` contract (Guid-only) — implemented exactly as `WP7.2C`
approved, with the gap disclosed as two new Future Capability candidates
(`FCR-0037`, `FCR-0038`) rather than silently absorbed. `FCR-0027`
(Requirements Engine) is now **Implemented**.

Eight completion deliverables produced under `docs/releases/v0.7.0/`,
prefixed `WP7.3A`. See `docs/releases/v0.7.0/WP7.3A Implementation
Report.md` for the complete file-by-file account. **Does not begin
WP 7.3B. Stops here, per this Work Package's own explicit closing
instruction, awaiting Product Approval for what comes next.**

### `WP 7.2C` Summary (for reference)

**`WP 7.2C` — Requirements & Verification Platform Contract Review.**
Contract review only — no production code was written. Approved to
begin after Engineering Review APPROVED `WP 7.2B`'s own complete
architecture.

Defined the complete public contracts for the Requirements &
Verification Platform — full proposed C# interfaces for all thirteen
named domain concepts (`IRequirementsService`, `IRequirement`,
`IRequirementCollection`, `IRequirementGroup`, the relationship
mechanism, `IRequirementEvidence`, `RequirementStatus`, and the
remaining simpler concepts), each answering the same seventeen
questions this Work Package's own controlling instruction named.
Defined a full seven-state **Requirement Lifecycle Model**, confirming
`RequirementStatus` is never automatically derived from a
`VerificationRecord`'s own `Outcome` — the two remain separate,
caller-driven actions. Reviewed all seven named relationship kinds
(**Relationship Model**), confirming six belong in the initial
implementation and the seventh — "Verified By" — already exists,
unmodified, inside `Tempest.Core.Verification`. Confirmed all five
traceability dimensions reuse existing Engineering Core capability with
zero new mechanism (**Traceability Contract**), while disclosing one
genuine, structural limitation: reverse allocation traceability does not
resolve when an allocation target is an open string rather than a real
document. Re-confirmed `ADR-0057`'s own circular-dependency avoidance
holds unmodified at the contract level (**Verification Integration
Contract**).

**Security Review** found zero new issues beyond `WP 7.2B`'s own
architecture-level review, but one new open question — whether
`IRequirementsService` should gate any of its own methods internally,
mirroring `IVerificationService.GetVerificationHistoryAsync`, or remain
calling-layer-enforced throughout, mirroring `IReportingService` — now
reserved as `ADR-0061`. **Standards Architecture** confirmed the
proposed contracts can support all seven named standard families
without redesign. **Engineering Principles review** again found no
extension warranted — contracts, not implementation.

Reserved `ADR-0058`–`ADR-0060` carried forward unchanged from `WP 7.2B`;
`ADR-0061` newly reserved. Twelve completion deliverables produced under
`docs/releases/v0.7.0/`, prefixed `WP7.2C`. See `docs/releases/v0.7.0/
WP7.2C Requirements Platform Contracts.md` for the complete design.
**Engineering Review APPROVED**, authorising `WP 7.3A`.

### `WP 7.2B` Summary (for reference)

**`WP 7.2B` — Requirements & Verification Platform Architecture.**
Architecture and planning only — no production code was written.
Designed the complete architecture for the Requirements & Verification
Platform — twelve domain concepts, a three-layer **Engineering Core →
Systems Engineering Foundation → Engineering Discipline Modules** model,
a digital thread design, and an eleven-service dependency analysis
finding zero new platform capability required. Security Architecture
found one new Technical Debt item (no concurrency-conflict detection on
`ReviseAsync`); Standards Mapping reviewed seven illustrative standard
families industry-neutrally. Reserved `ADR-0058`–`ADR-0060`. Eleven
completion deliverables produced, prefixed `WP7.2B`. **Engineering
Review APPROVED**, authorising `WP 7.2C`.

### `WP 7.2A` Summary (for reference)

**`WP 7.2A` — Strategic Roadmap Selection & Programme Architecture.**
Architecture, governance, and roadmap planning only — no production code
was written. Evaluated seven candidate programmes against eleven
criteria using repository evidence exclusively and recommended Programme
A — Requirements & Verification Platform (`FCR-0027`), scoring 46 of 55,
the highest of all seven candidates. Programme F (Platform Hardening)
scored second (36/55), recommended next at `v0.9.0`, not rejected.
Programmes B, C, D, E (Mechanical, Building Services/HVAC, Structural,
Electrical) each scored 14/55 — no identified capability in any of the
four. Programme G (AI & Engineering Intelligence) scored 19/55 — its own
capability already works structurally. Ten completion deliverables
produced, prefixed `WP7.2A`. **Engineering Review and Product Approval
accepted this recommendation**, authorising `WP 7.2B`.

### `WP 7.1F` Summary (for reference)

**`WP 7.1F` — Engineering Core Integration Review & Certification.** The
Engineering Foundation phase's (`v0.7.0`) ninth and closing activity — a
certification review, not an implementation Work Package, mirroring
`WP 6.8`'s own identical role for `v0.6.0`. Approved to begin after
Engineering Review of `WP 7.0A` through `WP 7.1E` all passed. No
production code was written; the two findings requiring a fix were each
a documentation or governance-register correction.

**Certification outcome: ENGINEERING CORE CERTIFIED WITH ACCEPTED
TECHNICAL DEBT.** Architecture Review: zero circular dependencies within
the Engineering Core or between it and any Platform Service, zero
`Service → Module`/`Module → Module`/`Runtime → Feature` violations —
confirmed directly against real code, not assumed. Integration Review:
every one of the five frameworks has at least one real, tested consumer;
Engineering Data Model is consumed by all four siblings, the broadest
consumption of any of the five. Security Review: zero Release Blocking
findings across both dedicated Security Reviews (`WP 7.1D`, `WP 7.1E`)
plus a cross-framework check this Work Package performed itself, which
found the identical unvalidated-material-reference design (`AT-16`,
`AT-17`) independently reached by two frameworks — corroborating
evidence the boundary is principled, not an oversight. Testing Review:
1275 tests, 0 failures, confirmed across four full-suite runs (Debug and
Release, from a clean rebuild) plus a dedicated 224-test run scoped to
the five Engineering Core namespaces. Definition of Done Audit: all
eight Engineering Foundation Work Packages satisfy every criterion, with
exactly one disclosed shortfall, now closed (below).

**Two genuine, non-blocking findings, found and closed in this same
Work Package.** First: a **repeat of `WP 6.8`'s own exact
governance-drift pattern** — `Interface Register.md` (64 → 75),
`Dependency Injection Register.md` (26 → 30 named registrations), and
`Module Register.md` (15 → 19) had each gone stale since `WP 6.8` itself,
undetected across all five Engineering Foundation Work Packages (11
interfaces, 4 registrations, 4 sample modules unrecorded). `FCR-0005`
(Governance Register Health-Check Tooling)'s own priority is raised
Medium → High as a result — this is now a confirmed, third recurrence of
the identical failure mode. Second: `WP7.0C Academy Plan.md`'s own
required Engineering Data Model concept guide, named as this programme's
"highest-priority new Academy content," was never written by `WP 7.1A`
and never disclosed as missing by any of `WP 7.1B`–`WP 7.1E` — written
here (`docs/academy/02 Runtime Architecture/15-engineering-data-model.md`).

Ten completion deliverables produced (`WP7.1F Engineering Core
Certification Report.md` and nine companions) plus
`ENGINEERING_CORE_COMPLETION_REPORT.md` (repository root), the
programme's own permanent historical milestone document. See
`docs/releases/v0.7.0/WP7.1F Engineering Core Certification Report.md`
for the complete decision and evidence.

### `WP 7.1E` Summary (for reference)

**`WP 7.1E` — Verification Framework.** The Engineering Foundation
phase's eighth activity, and its fifth and final implementation Work
Package — approved to begin after Engineering Review of `WP 7.0A`,
`WP 7.0B`, `WP 7.0C`, and `WP 7.1A` through `WP 7.1D` all passed.
**Completed the entire Engineering Foundation implementation programme**
— all five frameworks (`FCR-0029`–`FCR-0033`) became Implemented.
Implemented `Tempest.Core.Verification` (`IVerificationService`,
`IVerificationRecord`, `VerificationOutcome`) exactly as `WP7.0C
Engineering Foundation Contracts.md` proposed, extended with a
structured `VerificationContext` (explicit criteria, evidence, linked
documents, linked calculation records, referenced materials) resolving
`ADR-0057`'s own two reserved questions (Audit orthogonality confirmed;
`method` remains open) plus one genuine implementation finding:
verification history is queried entirely through the Engineering Data
Model's own existing `LinkAsync`/`GetReferencesAsync` mechanism, needing
no new index and no direct `IPersistenceStore` dependency at all — the
simplest dependency shape of any Engineering Foundation framework. Zero
new exception types — `EngineeringDocumentNotFoundException` is reused
directly. 9 new production files (the smallest of the five frameworks);
a new sample module (`VerificationSampleModule`, the platform's
nineteenth). 49 new tests (1275/1275 passing, both Debug and Release,
clean rebuild). Extended `docs/engineering/Engineering Principles.md`
with five further principles (24-28) and added a new Academy concept
guide (`14-verification-framework.md`). Second consecutive Work Package
to include a dedicated Security Review — two new disclosed debt items
(`TD-23`, `TD-24`) and one new accepted trade-off (`AT-17`), neither
Release Blocking, plus `FCR-0036`.

### `WP 7.1D` Summary (for reference)

**`WP 7.1D` — Engineering Calculation Framework.** The Engineering
Foundation phase's fourth implementation Work Package. Implemented
`Tempest.Core.Calculations` (`ICalculationDefinition<TInput, TResult>`,
`ICalculationEngine`) exactly as `WP7.0C Engineering Foundation
Contracts.md` proposed, substantially extended with `CalculationMetadata`
(assumptions, constraints), `CalculationContext` (intermediate results,
constraint checks, material references), and an expanded
`CalculationRecord<TResult>` (stable identity, assumptions, validation
outcome, revision number) — resolving `ADR-0056`'s own two reserved
questions (convention-only purity enforcement, confirmed; mandatory
Engineering Data Model integration) plus the `Calculate`-signature
extension this Work Package's own "engineering evidence, not merely a
numerical answer" requirement demanded. Every execution is durably
recorded as an `IEngineeringDocument` of `Kind = "CalculationRecord"`;
no direct `IPersistenceStore` dependency needed. 17 new production
files; a new sample module (`CalculationSampleModule`, the platform's
eighteenth). 52 new tests. Extended `docs/engineering/Engineering
Principles.md` with seven further principles (17-23) and added a new
Academy concept guide (`13-calculation-framework.md`). First Engineering
Foundation Work Package to include a dedicated Security Review — two
new disclosed debt items (`TD-21`, `TD-22`) and one new accepted
trade-off (`AT-16`), neither Release Blocking, plus `FCR-0035`.

### `WP 7.1C` Summary (for reference)

**`WP 7.1C` — Materials Framework.** The Engineering Foundation phase's
third implementation Work Package. Implemented `Tempest.Core.Materials`
(`IMaterialCatalog`, `IMaterialSpecification`) exactly as `WP7.0C
Engineering Foundation Contracts.md` proposed, extended with a
structured, provenance-carrying `MaterialProperty` value type
(replacing the contract's own bare `object` property value) resolving
`ADR-0055`'s own reserved property-typing question, plus `ReviseAsync`
(new — revision support). Every engineering property carries mandatory
`MaterialPropertyProvenance` (source reference, revision, validation
status, confidence level, applicable conditions, notes) — never
omissible by construction. Consumes both the Engineering Data Model and
Units & Quantities. One genuine implementation finding (`ADR-0055`):
`MaterialCatalog` depends directly, not only indirectly, on
`IPersistenceStore`, for its own `materialId` index. 14 new production
files; a new sample module (`MaterialsSampleModule`, the platform's
seventeenth, using only clearly-fictional, explicitly-disclosed test
data). 55 new tests. Extended `docs/engineering/Engineering
Principles.md` with four further principles (13-16); no new Academy
concept guide. One new disclosed debt item (`TD-20`) and one new
accepted trade-off (`AT-15`), neither Release Blocking.

### `WP 7.1B` Summary (for reference)

**`WP 7.1B` — Units & Quantities Framework.** The Engineering Foundation
phase's second implementation Work Package. Implemented `Tempest.Core.
UnitsAndQuantities` (`Quantity<TDimension>`, `Unit<TDimension>`,
`IUnitConverter`) exactly as `WP7.0C Engineering Foundation
Contracts.md` proposed, extended (not changed) with arithmetic,
comparison, formatting, parsing, and JSON serialization support,
resolving `ADR-0054`: `double`-backed, no DI registration,
`IUnitConverter` built as a stateless, non-DI-registered wrapper. Seven
starting dimensions (Length, Mass, Duration, Force, Pressure, Area,
Volume), each purely multiplicative; Temperature (an affine dimension)
deliberately deferred (`TD-19`, `FCR-0034`). 20 new production files;
zero modified files — the first Engineering Foundation Work Package to
touch neither `TempestHost.cs` nor any other existing file. 67 new
tests. Extended `docs/engineering/Engineering Principles.md` with six
further principles (7-12) and added a new Design Patterns concept guide
(`Phantom-Type Dimension Safety`). One new disclosed debt item
(`TD-19`) and one new accepted trade-off (`AT-14`), neither Release
Blocking.

### `WP 7.1A` Summary (for reference)

**`WP 7.1A` — Engineering Data Model.** The Engineering Foundation
phase's first implementation Work Package. Implemented `Tempest.Core.
EngineeringData` (`IEngineeringDocumentStore`, `IEngineeringDocument`,
`IDocumentRevision`, `DocumentReference`) exactly as `WP7.0C Engineering
Foundation Contracts.md` proposed, resolving `ADR-0053`: built directly
on `IPersistenceStore` (`WP 6.4`), no new storage abstraction. One
disclosed, minor deviation (the exception base class's modifier,
corrected to match universal existing convention) — see `WP7.1A
Implementation Report.md`. 13 new production files; a new sample
module (`EngineeringDataSampleModule`, the platform's sixteenth); 36 new
tests. Established `docs/engineering/Engineering Principles.md` — a
new, permanent, top-level document. Two new, disclosed debt items
(`TD-17`, `TD-18`), neither Release Blocking.

### `WP 7.0A`/`WP 7.0B`/`WP 7.0C` Summary (for reference)

`WP 7.0A` — Future Capability Register & Product Vision — established
`VISION.md`, `docs/governance/Future Capability Register.md` (28
entries as of that Work Package), `Capability Categories.md`, and
`Product Roadmap.md`. `WP 7.0B` — Engineering Foundation Planning &
Capability Architecture — added `FCR-0029`–`FCR-0033` (33 entries
total), a full dependency graph and six-programme grouping, an honest
conclusion that five of nine Engineering Discipline categories cannot
yet be sequenced from existing evidence, a non-binding release-number
recommendation, and ten candidate Work Packages (`A`–`J`). `WP 7.0C` —
Engineering Foundation Contract Review — proposed public C# contracts
for all five Engineering Foundation frameworks and reserved
`ADR-0053`–`ADR-0057`. All three passed Engineering Review before `WP
7.1A` began. See each Work Package's own deliverables under
`docs/releases/v0.7.0/`.

**`PROJECT_STATUS.md`'s own "Next Planned Work Package" section below
now defers to `docs/governance/Future Capability Register.md` as the
authoritative source for what TempestOS builds next** — this dashboard
still names the current Work Package, but no longer re-derives roadmap
reasoning that register now owns.

### `v0.6.0` Closing Summary (for reference)

`WP 6.8` — Platform Services Integration Review & Release
Certification — certified. The closing Work Package of the Platform
Services phase (`v0.6.0`) — a certification review, not an
implementation exercise; no production code was written. Reviewed all
eleven in-scope services (Runtime Foundation, Host, Identity &
Permissions, Settings, Persistence, Audit, Notifications, Reporting,
REST API, Export/Import, Licensing) directly against the shipped
repository, re-verifying rather than re-reading each prior Work
Package's own claim.

**Certification outcome: CERTIFIED WITH ACCEPTED TECHNICAL DEBT.**
Zero findings rise to release-blocking. Architecture Review: zero
`Service → Module` violations, zero `Module → Module` violations beyond
one disclosed, constant-only exception (`ApiSampleModule`), zero
`Runtime → Feature` violations — confirmed by direct `grep`, not
assumed. One genuine, narrow, non-blocking architectural finding
disclosed for the first time: `Tempest.Core.Diagnostics` imports
`Tempest.Core.Runtime` for a single enum type (`HostState`), a mutual
namespace reference a literal reading of `ADR-0023` would flag —
shipped safely since `WP 5.2`, recommended for formal resolution in a
future release. Integration Review: every one of the eleven services
has at least one verified, real consumer (the REST API now has two
independent ones — `ApiSampleModule` and `LicensingSampleModule` — the
strongest evidence yet that `IApiEndpointRegistry`'s own design
generalises). Testing Review: 1016 tests, 0 failures, confirmed across
six full-suite runs (Debug and Release, from a clean rebuild), zero
instances of the known, disclosed `Console.Out`-capture flake.

**The largest single finding: three governance registers, stale since
`WP 5.2`, were fully backfilled.** `Interface Register.md`
(64 interfaces), `Dependency Injection Register.md` (26 named
registrations), and `Module Register.md` (all 15 production modules)
had each gone six Work Packages without an update — `WP 6.7` first
disclosed this as `Partial`, `WP 6.6` correctly added only its own new
entries, and `WP 6.8` performed the full backfill, closing the gap
completely. Two release-level risks (`R2`, `R3`) that had sat "Open"
despite being substantively resolved by their own owning Work Packages
were formally closed here with fresh, independent evidence (`git log`'s
own commit order for `R2`; a fresh `grep` of `RestApiHostedService.cs`
for `R3`). All eight risks in `Risk Register.md` are now Closed or
Mitigated, save one (`R8`) Remaining by deliberate, disclosed design
choice. Sixteen tracked debt items and thirteen disclosed trade-offs
were each classified Resolved, Accepted, or Deferred — zero Release
Blocking. See `WP6.8 Platform Certification Report.md` for the complete
decision and evidence, and its eight companion deliverables
(`Platform Architecture Conformance Report.md`, `Platform Consumption
Matrix.md`, `Definition of Done Audit.md`, `Technical Debt
Disposition.md`, `Risk Register Disposition.md`, `Release Readiness
Report.md`, `Executive Summary.md`) plus its own retrospective:
`docs/academy/03 Work Packages/WP6.8-platform-services-integration-review.md`.

## Next Planned Work Package

**`v0.11.0` is closed — merged to `main`, released, and published;
`main` is the authoritative baseline.** All ten `v0.11.0` Work Packages
(`WP 11.0A`–`WP 11.4B`) are now complete. `WP 11.4B` merged
`feature/v0.11.0-v1-architecture` into `main` (`4b1fb16`) and, with
explicit Product Owner approval, corrected a genuine release-process
defect found before the release branch closed — `v0.11.0`'s own tag had
repeated the `v0.10.0` tag-position defect `WP 11.1B` already disclosed
— under a new, narrowly-scoped governance exception (see `WP11.4B
Release Process Correction Report.md`). `Build & Test`/`CI Gate` now
genuinely pass on real GitHub-hosted infrastructure for `main` itself,
not only a feature branch, confirmed on four consecutive real CI runs.
See `docs/releases/v0.11.0/WP11.9.0 Engineering Release Report.md` (the
Programme Review), `WP11.4A Release Engineering Corrections.md` (the
CI-infrastructure fixes), and `WP11.4B Release Process Correction
Report.md` (the tag correction and merge). `WP11.0B Architecture
Roadmap.md` §6 names two concrete next candidates, cross-checked
directly against its own §5 estimates table and not superseded by
anything found since: **(1)** close `TD-45` — configure the already-
documented branch-protection rule in GitHub on `main` (required `CI
Gate` check, required CODEOWNERS review) now that `CI Gate` has
demonstrated it passes correctly on real infrastructure, not merely
designed to — the single action that converts this release's own
designed-but-advisory process into mechanically enforced process,
recommended by both the Workflow Engineer's and Product Manager's own
independent `WP 11.9.0` reviews; **(2)** `WP 12.0A` — Desktop
Composition Root Decomposition
Architecture, the roadmap's own next-scheduled item, explicitly gated
on this release's own CI pipeline existing first ("want CI in place
before the large refactor it authorises," `WP11.0B` §5). A secondary,
lower-cost fast-follow also disclosed by `WP 11.9.0`, not blocking
either candidate above: write the four missing `docs/academy/03 Work
Packages/` retrospective articles (`WP 11.0A`, `WP 11.0B`, `WP 11.3A`,
`WP 11.3B`) and correct `Contributor Learning Path.md`'s own stale
`v0.5.0` closing-section pointer (a `WP 11.9.0` Workflow Engineer
finding, six releases stale, missed by `WP 11.3B`'s own edit pass to
that same file).

**`WP 11.9.0`'s own status line, below this point, is this field's
prior content — retained, not deleted:**

**Disclosed divergence from `WP11.0B Architecture Roadmap.md`'s own
prediction, not silently reconciled — now twice, in the same release:**
that roadmap's §3 scoped `WP 11.1B` as "Governance Currency Pass &
Health-Check Tooling" and `WP 11.2A` as "Desktop & Console Presentation
Strategy Decision." The Product Owner instead commissioned `WP 11.1B` as
**Branch Protection & Engineering Workflow Hardening** and `WP 11.2A` as
**Governance Health-Check Automation** — both now complete, see Current
Work Package, above; the latter is, in substance, the roadmap's own
originally-predicted `WP 11.1B` scope, delivered one Work Package later
and under a different number than predicted. The roadmap's own text
called itself "a recommendation, not an approval" throughout (§7); this
is that distinction exercised a second time, not a defect in the roadmap
or in either Work Package actually run.

**Resolved — the Desktop & Console Presentation Strategy Decision
(`WP11.0A` finding `A-2`) is now fully closed, reviewed by `WP 11.3A`
and implemented by `WP 11.3B`.** After being predicted for two Work
Package slots without landing in either, it was commissioned directly as
its own two-Work-Package pair: `WP 11.3A` reviewed and recommended;
`WP 11.3B` executed the approved, low-risk portion of that recommendation
— `TempestShell` retired, `ADR-0101` ratifying `Tempest.App`/
`WorkspaceShell` as the Internal Engineering Harness, documentation and
release-packaging corrected. See `docs/releases/v0.11.0/WP11.3A
Presentation Strategy Review.md` and `WP11.3B Presentation Strategy
Implementation.md`.

**What remains before `v0.11.0` can close:** only `WP 11.9.0` (Release
Preparation & Engineering Sign-Off) — every other item this release's
own roadmap named is now complete. The release can cite a real CI run
(`WP 11.1A`), a fully-defined merge/release process (`WP 11.1B`), an
automated governance check (`WP 11.2A`), and a resolved, implemented
presentation strategy (`WP 11.3A`/`WP 11.3B`) for its own gate evidence
— the first release in this project's history able to cite all four.
See `docs/releases/v0.11.0/WP11.0B Architecture Roadmap.md` for full
effort/risk/impact estimates and the complete `v0.12.0`/
conditional-`v0.13.0`/`v1.0.0` sequence beyond this release.

### Superseded — `v0.10.0`'s Own Prior "Next Planned Work Package" Content (Retained for Reference)

The paragraph below was this field's own content immediately before
`WP 11.0A`/`WP 11.0B` began, and remains an accurate record of what
`v0.10.0`'s own closing review found — several of its named candidates
(`TD-38`, `TD-41`, `ADR-0095`/`FCR-0064`, the Command Palette/Macro
`CreateDefault` gap, `FCR-0073`, `FCR-0056`) are real and still open, but
none traces to a `WP 11.0A` finding, so `WP11.0B`'s own roadmap
deliberately does not absorb them — see that roadmap's §4 for why, and
for the one item it does reprioritise (`FCR-0005`, folded into
`WP 11.1B` above). They remain available for the Product Owner to
interleave with the Work Packages above at their own discretion.

**None yet approved for `WP 11.0`.** `v0.10.0`'s own sixteenth and final
Work Package, `WP 10.9A` (`v0.10.0` Release Candidate & Engineering
Sign-Off), has completed a full, independently re-verified release
audit — see `docs/releases/v0.10.0/WP10.9A Engineering Release
Report.md` for the authoritative go/no-go decision, gate-by-gate
evidence, remaining Technical Debt, and remaining Future Capability
candidates. **Per this project's own standing discipline
(`FOUNDATION.md` §1), no `WP 11.0` Work Package begins until the Product
Owner gives further instruction.**

Real, disclosed candidates for `WP 11.0`'s own scoping, carried forward
from `v0.10.0`'s own accumulated findings (not newly invented here —
see the Release Report for the full, current list): `TD-38`
(`EngineeringObjectFactory<T>` enforces no business-identifier
uniqueness of its own, open since `WP 10.1B`); `TD-41`
(Requirements never resolve via `EngineeringDomainContext.Repository`,
open since `WP 10.3A`, now affecting two Desktop presentation surfaces);
`ADR-0095` (a `WorkspaceDockPosition`/`WorkspacePanelPlacement` contract
extension for floating/undocked panels and multi-monitor placement,
`FCR-0064`) — reserved, still unwritten; the Command Palette/Macro
`CreateDefault` gap (no real discipline command has ever set it, closing
requires a genuine `CommandDescriptor`/`ICommandRegistry` extension);
Ribbon Copy/Move-as-a-button (`FCR-0073`, needs a destination-parent
picker dialog); a dedicated Governance & Risk Workspace (`FCR-0056`);
`FCR-0005` (Governance Register Health-Check Tooling — this release's
own audit found the Release Register, ADR Register, and Academy
Register had each independently drifted stale across multiple Work
Packages before `WP 10.9A` corrected them, the same recurring pattern
`FCR-0005` exists to prevent, now with a fourth-through-sixth
independent instance as evidence).

### Superseded Planning Note (Retained for Reference)

The paragraph below was this field's own content as of `WP 10.2A`
(2026-08-07) and was left unrevised for eight further Work Packages —
a real, disclosed drift found and corrected by `WP 10.9A`'s own release
audit, not silently rewritten to hide that it happened. Retained
verbatim for the historical record; every item within it that remained
genuinely open has already been re-stated, current, above.

> None yet approved for `WP 10.2B`. `WP 10.0A` (architecture), `WP
> 10.0B` (framework implementation), `WP 10.1A` (Engineering Cockpit),
> `WP 10.1B` (Runtime Host & Module Discovery Hardening), and `WP 10.2A`
> (Workspace Modernisation) have all closed — `Tempest.Desktop` is now a
> real, running, tested, modern graphical desktop application with real
> object Rename/Delete dispatch (`ADR-0096`), a real, live-data
> Engineering Cockpit as its own default landing screen (`ADR-0069`
> realised), `ADR-0094` resolved, `FCR-0063` Implemented, and `TD-26`/
> `TD-37` both genuinely resolved at their own true source. Two
> highest-priority next candidates, named directly by `WP 10.2A`'s own
> disclosed findings: (1) a dedicated Engineering Domain Work Package to
> resolve `TD-38`; (2) a uniform `Move*Command` shape across all six
> disciplines (`FCR-0066`) — **subsequently implemented, `WP 10.7A`**,
> via a lighter route than originally proposed here (a plain
> `ObjectMoveRequested` event, not a fourth `IWorkspaceManager` member).

**`v0.9.0`'s own remaining Future Capability candidates, unscheduled and
still valid, carried forward:** `FCR-0056` (a dedicated Governance &
Risk Workspace for `Risk`/`Issue`/`Decision`/`Hazard`/`Assumption`) —
`WP 10.1A` strengthened, not closed, this candidate: the Cockpit now
reads this Domain family directly for the first time, but no dedicated
Explorer/Commands/Views layer exists yet, still the most concrete,
ready-to-start next Engineering Discipline candidate; a genuine
`Routing`/`SupplierOperation` Domain Kind with structured fields
(`FCR-0060`); parameterising `EngineeringCockpit.FormatCoverage`'s own
empty-state message (`FCR-0061`); `VerificationService.RecordAsync`
additionally linking through `IHasRelationships` (`FCR-0057`/`FCR-0062`);
a governed Approval/Review workflow (`FCR-0052`/`FCR-0058`); a real
file/URL attachment storage service (`FCR-0054`); a dedicated `Witness`
field on `VerificationEvidenceEntry` (`FCR-0059`); concrete
`ICalculationResult`/`IVerificationResult` implementations (`FCR-0051`);
Recalculate resuming from a stored input (`FCR-0053`); Domain-level
cross-discipline Search (`FCR-0049`); Requirement Collection membership
removal (`FCR-0048`); multi-target Workspace view refresh (`FCR-0050`);
`TD-27` (the disclosed repository-ordering characteristic); `TD-36`
(the Settings persistence store's own working-directory-relative
default location, `WP 10.0B`); and `TD-34`
(the `CompositeLogSinkTests` flake, `WP 9.9.0`'s own second pass,
reconfirmed present but non-blocking a further time by `WP 10.2A`'s own
full-suite runs) — either serialising it against the
`[Collection("Console output capture")]` tests, or removing
`CompositeLogSink`'s own direct `Console.Error` dependency, both
low-cost candidates for whatever Work Package next touches
`Tempest.Core.Logging`. `FCR-0005` (Governance Register Health-Check
Tooling) — carrying its strongest evidentiary case yet, between
`WP 9.8B`'s own existence and `TD-34`'s own six-release-cycle-old
informal disclosure only now formally tracked. Longer-standing Future
Capability candidates remain open and unscheduled:
`FCR-0037`/`FCR-0038` (`WP 7.3A`), `FCR-0041`/`FCR-0045`–`FCR-0047`
(`WP 9.0A`/`WP 9.0B`).

## Foundation Status

**Complete.**

| Milestone | Status |
|---|---|
| Platform Formation (Runtime Host, six platform services, Plugins, Event Bus, Background Services) | Complete |
| Academy Formation (63 articles across 7 categories, formal maintenance obligation) | Complete |
| Governance Formation (27 registers, full traceability, zero outstanding debt) | Complete |

See `docs/releases/Platform Foundation Completion Report.md` for the full
closeout narrative and `docs/releases/v0.4.0/Release Notes.md` for the
release this milestone shipped as.

## Platform Summary

TempestOS is a modular runtime platform. The Runtime Host
(`TempestHost`/`TempestHostBuilder`) assembles and orchestrates: Discovery,
Registration, Lifecycle, Dependency Injection, Configuration, and Logging
(the original Runtime Foundation, v0.3.0); Plugin Manifest discovery and
loading; the Event Bus (publish/subscribe between modules); and
Background Services (Host-orchestrated hosted work with isolated/critical
failure classification); and Navigation (a DI-public registry of
navigable destinations, notified via the Event Bus). Five real modules
(`ClockModule`, `ClockLifecycleObserverModule`, `NavigationSampleModule`,
`SecondaryNavigationSampleModule`, `DuplicateNavigationSampleModule`)
exercise the complete pipeline end to end. `Tempest.App` now runs the
real platform for the first time in this project's history: its entry
point builds a `TempestHostBuilder`, constructs the Shell
(`TempestShell`, `Tempest.App.Shell`), and runs it — the Shell resolves
`INavigationProvider`/`IEventBus` through `ITempestHost.Services`
(`ADR-0033`–`ADR-0035`, implemented `WP 5.0D`) and presents a real,
interactive Navigation/Content region. The bootstrap-era
`BootstrapService`/`HostingService`/`ProjectService` code remains in the
repository, untouched and unmigrated, simply no longer referenced by
`Program.cs`. The Command Framework (`ICommand`, `v0.4.0`; `ICommandDispatcher`/
`ICommandRegistry`, `ADR-0036`–`ADR-0038`, `WP 5.1A`/`WP 5.1B`) is now
fully implemented — a type-keyed dispatcher and an Id-keyed registry,
both DI-public, mirroring the Event Bus and Navigation — proven by a
real reference module (`CommandSampleModule`) that also realises
ADR-0022's own Navigation-integration illustration for the first time.
Wiring the Shell's own input handling (keyboard shortcuts, a menu) to
the Command Framework is deferred to a later Work Package. Diagnostics
(`IDiagnosticsProvider`/`DiagnosticsProvider`, `Tempest.Core.Diagnostics`)
is now implemented — a read-only projection over the Host's own current
lifecycle state, constructed directly by `TempestHost` and registered via
`AddInstance` with `Func<T>` accessors (`ADR-0039`), proven by
`DiagnosticsSampleModule` and its `GetDiagnosticsSummaryCommand`. Logging
also gained `CompositeLogSink`, closing `TD-02`. A `dotnet new
tempest-module` template (`src/Templates/`) now lets a new contributor
scaffold a correctly-shaped module without hand-copying an existing one,
and Discovery's own long-documented parameterless-constructor pitfall now
fails with a clear, actionable message instead of a raw runtime
exception. Every Work Package originally scoped for the Developer
Experience phase is now complete.

## Repository Metrics

| Metric | Value |
|---|---|
| Automated tests | **2069** — `Tempest.Core.Tests` **+11, `WP 10.2A`**: `WorkspaceManagerTests` gained `RegisterRenameFactory`/`RegisterDeleteFactory`/`CanRename`/`CanDelete`/`RenameObjectAsync`/`DeleteObjectAsync` coverage, `ADR-0096` (2029 → 2040); `Tempest.Desktop.Tests` **+7, `WP 10.2A`**: `WorkspaceModernisationTests` (new file) — real rename dispatch, honest `CanRename` absence, the Lifecycle-duplication fix, filtering, pinned tabs, Status Bar diagnostics (22 → 29). Full `v0.9.0`→`v0.10.0` chain: 2026 → 2069 (+43). Previously +25, `WP 10.1B` |
| ADRs | 95 (`ADR-0001`–`ADR-0094`, `ADR-0096`; no gaps other than the still-reserved `ADR-0095`) — **93 Accepted, 2 Superseded**. **+1, `WP 10.2A`**: `ADR-0096` (real object Rename/Delete dispatch, a fourth/fifth Kind-keyed `IWorkspaceManager` provider category). One ADR number remains reserved: `ADR-0095` (floating/multi-monitor panel contract extension, explicitly out of `WP 10.2A`'s own "Do NOT implement" list). Full `v0.9.0`→`v0.10.0` chain: 91 → 95 (+4). Previously unchanged, `WP 10.1B` |
| Rejected Designs | 45 (`RD-0001`–`RD-0045`) — unchanged by `WP 10.2A` |
| Academy articles | 137 (see `docs/governance/Documentation/Academy Register.md`) — **+2, `WP 10.2A`**: `23-workspace-modernisation.md` (new concept guide) and `WP10.2A-workspace-modernisation.md` (retrospective). Previously +2, `WP 10.1B`: `22-runtime-host-restart-stability.md`/`WP10.1B-runtime-host-and-module-discovery-hardening.md` |
| Governance registers | 27 (stated "32 governance documents total"), plus 4 standing security documents under `docs/security/` and 1 standing engineering document (`docs/engineering/Engineering Principles.md`) — unchanged in register *count* by any `v0.9.0` Work Package (all edited in place, no new register added). **Disclosed, unfixed observation, `WP 9.3A`, re-confirmed `WP 9.5A`, `WP 9.9.0`, and `WP 9.8B`:** a direct `find` against `docs/governance/` continues to return 35 files today, not 32 — a pre-existing drift no Work Package has chased to ground (the original 27-registers/32-total split's own exact taxonomy remains undocumented); disclosed, not fixed (outside `WP 9.8B`'s own narrower Platform Service scope), escalated as a standing recommendation in `WP9.9.0 Product Approval Report.md` |
| Architecture documents | 20 under `docs/architecture/` (22 including the two release-scoped documents) — unchanged by any `v0.9.0` Work Package |
| Platform services | 30 catalogued — unchanged by `WP 9.9.0` Second Pass; independently re-verified consistent across all five governance documents, not merely trusted from `WP 9.8B`'s own claim — **the first release-closing review in this project's history to find this gap closed rather than open.** Previously **+4, `WP 9.8B`**: Engineering Data Model (`WP 7.1A`), Materials (`WP 7.1C`), Engineering Calculations (`WP 7.1D`), Verification (`WP 7.1E`) — zero new service introduced, all four already real, running, and DI-registered since `v0.7.0`; only their own governance rows/Map sections were missing. Closed the disclosed gap `WP 7.3A` first found, confirmed open across three consecutive release-closing reviews (`WP 7.4.0`/`WP 8.9.0`/`WP 9.9.0` first pass). Also corrected this register's own stale headline total (previously "27," true prior count 26). |
| Modules (production) | 34 — unchanged by `WP 10.0B`; `Tempest.Desktop`'s own classes are a composition root/presentation layer, not `IModule` implementers, confirmed by direct read (Module Register's own scope). Previously unchanged, `WP 9.9.0` |
| Hosted services (production) | 2 — unchanged by any Work Package since `v0.9.0` |
| Plugins (production) | 0 — infrastructure fully implemented and tested; `src/Plugins/` empty by deliberate scope decision |
| Custom exception types (`src/Tempest.Core/`) | 72 (register stale since `WP 6.6`; unchanged by `WP 10.0B` — zero `Tempest.Core` files touched) |
| Public interfaces (`src/Tempest.Core/`) | 168 — unchanged by `WP 10.0B`; this register's own scope is `src/Tempest.Core/` only, confirmed zero files touched there. Previously unchanged, `WP 9.9.0` |
| DI registrations (`TempestHost.cs` Phase 6) | 44 raw call sites, 42 named registrations — unchanged by `WP 10.0B`; `Tempest.Desktop` resolves existing services via `ITempestServiceProvider.GetService`, registers nothing new. Previously unchanged, `WP 9.9.0` |
| Technical Debt Register items | 38 tracked — 6 Resolved, 1 Partially resolved, 31 Open. Unchanged by `WP 10.2A` — one genuine, low-severity implementation defect (Property Inspector Lifecycle-section facet duplication) was found by this Work Package's own Engineering Review and fixed in place, before any commit, proven by a dedicated regression test — never reaching a state warranting a Technical Debt entry. Full `v0.9.0`→`v0.10.0` chain: 34 → 38 total (+4, three immediately Resolved: `TD-35`/`TD-26`/`TD-37`). Previously +1, `WP 10.1B`: `TD-26`/`TD-37` Resolved, `TD-38` added Open |
| Commits (`v0.6.0` → `v0.7.0`) | 17 total, release complete: `v0.6.0` release-branch preparation (2 commits), merge from `main`, `WP 7.0A`–`WP 7.4.0` (14 commits), the `v0.7.0` merge to `main` (non-fast-forward, `61fb2db`) — tagged `v0.7.0`, pushed |
| Commits (`v0.7.0` → `v0.8.0`) | 11 total, release complete: `WP 8.0A`, `WP 8.0B`, `WP 8.1A`, `WP 8.0C`, `WP 8.1B`, `WP 8.1C`, `WP 8.2A`, `WP 8.2B`, `WP 8.2C`, `WP 8.9.0` (10 commits), the `v0.8.0` merge to `main` (non-fast-forward, `28e41e8`) — tagged `v0.8.0`, pushed |
| Commits (`v0.8.0` → `v0.9.0`) | 5 total, release complete: `WP 9.0A`–`WP 9.1A` consolidation, `WP 9.1B` baseline, its own follow-up fix (3 commits, all directly on `main` — no feature branch ever existed in the real repository, per the disclosed finding under Current Development Branch, above, and `WP 9.9.1`'s own reconfirmation), the `v0.9.0` release commit (`9f258f1` — no merge commit, since no feature branch existed to merge from, disclosed plainly rather than fabricated) bundling all of `WP 9.2A`/`WP 9.4A`/`WP 9.3A`/`WP 9.5A`/`WP 9.9.0` (both passes)/`WP 9.8B`'s own work (161 files, +22,205/−120 lines), and a further documentation commit (`9f64700`, adding `WP 9.9.1`'s own three release-execution deliverables) — tagged `v0.9.0`, pushed. `VERSION` correctly reads `0.9.0`. |
| Commits (`v0.9.0` → `v0.10.0`, so far) | 0 — `WP 10.0A`'s own architecture deliverables, `WP 10.0B`'s own real implementation, `WP 10.1A`'s own Engineering Cockpit implementation, `WP 10.1B`'s own Runtime Host & Module Discovery Hardening, and `WP 10.2A`'s own Workspace Modernisation (`ADR-0096`/`IWorkspaceManager`'s five new members, six discipline registration files wired, a modernised Project Explorer/Property Inspector/Document Area/Status Bar, `DesignTokens.cs`, eighteen new tests across two projects, six reviews, two Academy documents, all governance register edits) all remain uncommitted, on `feature/v0.10.0-user-experience`, pending Product Owner instruction — mirroring `v0.9.0`'s own now-closed "prepared, awaiting commit" state at the equivalent point in that release's own history |
| Contributors | 1 (repository owner; all commits co-authored by Claude) |

*(This table is generated from `docs/governance/Quality/Repository Metrics
Register.md` and `docs/releases/v0.4.0/Release Notes.md` — update all
three together.)*

## Repository Health

- **Build:** Clean — 0 warnings, 0 errors (`dotnet build
  tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj`, both Debug and
  Release configurations, from a fully-removed `bin`/`obj` tree —
  re-verified directly by `WP 7.1F`, independent of any prior Work
  Package's own claim).
- **Tests:** 1275/1275 passing, re-verified by `WP 7.1F` across **four**
  full-suite runs (two Debug, two Release, each from a clean rebuild)
  plus one further, dedicated 224-test run scoped to the five Engineering
  Core namespaces specifically. No instance of the `v0.6.0`-era,
  previously-disclosed `Console.Out`-capture flake (`WP 6.3`'s own
  finding) was observed across any run. `WP 7.1F`'s own certification
  review found no code-level regression of any kind — see `WP7.1F
  Engineering Core Certification Report.md` for the complete, per-run
  evidence table.
- **Known regressions:** None.
- **Working tree:** Clean at every Work Package boundary — see
  `docs/governance/Quality/Validation Register.md`.

## Documentation Status

Mature and cross-referenced. Every architecture document, ADR, and
Academy article is indexed in `docs/governance/` and cross-checked
against its own source. `WP 5.0A` added `Navigation Framework
Architecture.md`, `ADR-0031`/`ADR-0032`, and a new Academy concept guide;
`WP 5.0B` updated each of them in place to reflect implementation. `WP
5.0C` added `Shell & Composition Framework Architecture.md` and
`ADR-0033`–`ADR-0035`; `WP 5.0D` updated each of them in place to reflect
implementation, following the identical documentation shape Navigation's
own implementation phase established. Two small, pre-existing drifts were
also found and fixed along the way, unrelated to this Work Package's own
changes: `Documentation Register.md` had gone stale since `WP 4.5B`, and
`Technical Debt Register.md`'s own TD-07 still described Navigation's
`Tempest.Core` placement as an open question, under its old `WP 4.6A`
number, three Work Packages after `ADR-0031` had already resolved it.
`docs/releases/v0.5.0/ReleasePlan.md` and `WorkPackages.md` carry the
renumbered Developer Experience scope (`WP 4.6A`–`WP 4.9` → `WP 5.0A`–
`WP 5.3`) forward, plus the new `WP 5.0C`/`WP 5.0D` pair inserted without
renumbering anything else (`D-016`); the old `v0.4.0` entries were
annotated with redirect notes rather than deleted, per this project's
"never delete, mark superseded" convention. `WP 5.0S` added a new
top-level documentation tree, `docs/security/` (four documents: `Threat
Model.md`, `Security Principles.md`, `Platform Security Review
v0.5.0.md`, `Security Roadmap.md`), indexed from `Governance Index.md`'s
new Security section and `Documentation Register.md`'s Directory Map —
the first new top-level `docs/` tree since `docs/governance/` itself
(`WP 4.5A`). `WP 5.1A` added `Command Framework Architecture.md` and
`ADR-0036`–`ADR-0038`; the existing "Command" entries in `Platform
Service Map.md` and `Engineering Glossary.md` were updated in place,
following the identical documentation shape Navigation's and the
Shell's own design phases established. A genuine, pre-existing drift was
found and corrected along the way, unrelated to `WP 5.1A`'s own design
work: `Ownership Matrix.md` had never received a row for Navigation, at
either `WP 5.0A` or `WP 5.0B` — added then, alongside `WP 5.1A`'s own new
Command Framework row. `WP 5.1B` updated every "Command Framework" status
line from "architected" to "implemented" (`Platform Service Map.md`,
`Ownership Matrix.md`, `Engineering Glossary.md`, `Architecture Document
Register.md`) and added an Implementation Note and Security Review
Update to `Command Framework Architecture.md` itself. Further
pre-existing drift was found and corrected during `WP 5.1B`'s own
repository review: `docs/releases/v0.5.0/WorkPackages.md` had never
gained an entry for `WP 5.0S`, and several Engineering registers
(`Platform Services Register.md`, `Interface Register.md`, `Namespace
Register.md`, `Dependency Injection Register.md`, `Exception Register.md`,
`Module Register.md`) and Delivery registers (`Feature Register.md`,
`Traceability Matrix.md`) had gone stale since `WP 5.0D` — all corrected
in the same commit as the change that prompted noticing them. `WP 5.2`
added `Diagnostics Architecture.md` and `ADR-0039`; `Platform Service
Map.md`, `Ownership Matrix.md`, and `Engineering Glossary.md` each gained
a new Diagnostics entry, following the identical documentation shape
Navigation's and the Command Framework's own design phases established.
A genuine, pre-existing drift was found and corrected along the way,
unrelated to `WP 5.2`'s own scope: `Architecture Document Register.md`
still read `Command Framework Architecture.md` as "implementation
pending... not yet started," two Work Packages after `WP 5.1B` had
actually completed it — corrected here. `WP 5.3` updated `Building a
Module.md` and `Sample Module Architecture.md` to reference the new
scaffolding template, and amended `Engineering Governance.md` §11 to
document `src/Templates/`. Three further, genuine, pre-existing drifts
were found and corrected during this Work Package's own repository
review, none related to its own scope: `Rejected Designs Register.md`
had added `RD-0042`–`RD-0044` (`WP 5.2`) without the corresponding full
entries ever being written into `docs/architecture/Rejected Designs.md`
itself; `Engineering Governance.md` §11 had not been updated when
`WP 5.2` added the `Tempest.Core.Diagnostics` namespace; and
`Governance Register.md`'s own Compliance Matrix had not been updated
since `WP 5.0D`, missing four completed Work Packages entirely (see
Governance Status, below, for the full account). `WP 5.4` produced this
release's own Release Candidate documentation — `docs/releases/v0.5.0/
CHANGELOG.md`, `Release Notes.md`, `ReleaseChecklist.md`, and the
top-level `docs/releases/v0.5.0.md` — and found three further,
significant, pre-existing drifts: `docs/releases/v0.5.0/ReleasePlan.md`
had not been updated since `WP 5.0C` and still read "in progress" with
only three of nine Work Packages marked complete; `docs/academy/
Contributor Learning Path.md` — the document a *new contributor* reads
first — still pointed at `v0.4.0/WorkPackages.md`, cited a 30-ADR count,
and never mentioned Navigation, the Shell, the Command Framework,
Diagnostics, or the new module template at all; and `docs/releases/
v0.4.0/Risks.md`'s own `R5`/`R7`/`R8`/`R9` rows had each been carrying a
"residual carries forward" caveat that was, by this point, already fully
resolved by a specific, named `v0.5.0` Work Package — all corrected.

**`WP 6.1` (Permissions & Identity)** — the release's own eight-document
architecture package (`docs/releases/v0.6.0/Release Architecture.md` and
companions) and five-document Contract Review package (`Platform Service
Contracts.md` and companions) were both produced and approved before this
Work Package began; neither was revised during implementation.
`docs/architecture/Platform Service Map.md` gained a new Identity &
Permissions entry, following the identical documentation shape every
prior new platform service's own entry has used. Two genuine
implementation-phase findings not anticipated by the architecture
package are disclosed in `ADR-0043`/`ADR-0044` rather than silently
absorbed: the config-sourced Role model and `IIdentityService` (neither
drafted in the original `Public Interface Catalogue.md`), and the
`CurrentPrincipalAccessor` ambient-field departure from that document's
own tentative `AsyncLocal<T>` suggestion, proven by a dedicated
regression test. `docs/governance/Quality/Technical Debt Register.md`'s
`TD-09`/`TD-10`/`TD-11` entries were each updated in place to record that
the authorization enforcement point now exists, without claiming any of
the three is resolved — none is, since retrofitting an enforcement call
into `NavigationService`, Command/Navigation registration, or plugin
loading was explicitly out of this Work Package's own scope.

**`WP 6.4` (Settings Framework)** — implemented directly against the
same, unrevised architecture and Contract Review packages.
`docs/architecture/Platform Service Map.md` gained two new entries,
Persistence and Settings, following the identical documentation shape
every prior new platform service's own entry has used. `ADR-0041`
formally ratifies the Persistence-abstraction recommendation the
architecture phase only tentatively proposed; `ADR-0042` confirms
Settings' own distinctness from Configuration and records two
genuine implementation-phase decisions the Contract Review left open:
an in-memory cache over `IPersistenceStore` (built, per that document's
own suggestion), and a deliberate choice *not* to add a sensitive-value
flag to `ISettingDefinition` in this release (a disclosed limitation,
not a defect, since doing so would have modified an approved public
interface for a speculative need). `docs/releases/v0.6.0/Risk
Register.md`'s `R4` (Persistence reinvented ad hoc) and `R8`
(Persistence too minimal for Audit's needs) were both updated in
place — `R4` retired (the abstraction now exists, exactly as
recommended), `R8` confirmed rather than retired (the minimal shape
shipped exactly as anticipated; whether it suffices for `WP 6.5` remains
open until that Work Package actually attempts to build against it).

**`WP 6.5` (Audit Framework)** — implemented directly against the same,
unrevised architecture and Contract Review packages, and directly
against `WP 6.4`'s own shipped `IPersistenceStore` — no new persistence
mechanism was introduced. `docs/architecture/Platform Service Map.md`
gained a new Audit entry. `ADR-0045` formally settles the Logging/
Diagnostics/Audit orthogonality `Required ADRs.md` anticipated, plus
three genuine implementation-phase decisions the Contract Review left
open: `RecordAsync` propagates failures rather than being literally
fire-and-forget (the performance goal is met by keeping the write
itself minimal, not by discarding the task); `IAuditQuery.QueryAsync`
is permission-gated through the existing enforcement point
(`IPermissionEvaluator`); and correlation identifiers are carried in
`Detail` under a well-known key, requiring no interface change. This
Work Package's own explicit Persistence Validation concluded
`IPersistenceStore` is adequate for this release's own correctness
needs — `docs/releases/v0.6.0/Risk Register.md`'s own `R8` is confirmed
again, not retired, and `docs/governance/Quality/Technical Debt
Register.md` gained a new, permanent item (`TD-12`) disclosing the
same client-side-filtering performance characteristic as a
cross-release concern, not merely a release-scoped risk. This Work
Package's own repository review also found and fixed a genuine,
deterministic bug in `WP 6.1`/`WP 6.4`'s own already-committed
Host-registration tests (see Repository Health, above).

**`WP 6.2` (Notification Framework)** — implemented directly against the
same, unrevised architecture and Contract Review packages.
`docs/architecture/Platform Service Map.md` gained a new Notifications
entry, following the identical documentation shape every prior new
platform service's own entry has used. `ADR-0046` formally settles the
Event-Bus-orthogonality question `Required ADRs.md` anticipated, plus
the genuine C# generic-constraint impossibility implementation
surfaced (literal delegation from `NotificationDispatcher` to
`IEventBus` is not possible without illegally tightening a generic
constraint or resorting to reflection — resolved by mirroring
`EventBus`'s own internal shape instead), the additive
`IPlatformNotification`/`NotificationSeverity`/`Category` elaboration,
the deliberate `Warning`-vs-`Error` logging-level departure from the
Event Bus's own convention, and the exact-static-type-dispatch defect
found and fixed against this Work Package's own sample consumers (see
Repository Health, above). `docs/governance/Quality/Technical Debt
Register.md`'s `AT-03` and `AT-07` entries were each annotated in
place — `AT-03` because the same exact-type-dispatch limitation now
also applies to Notifications; `AT-07` to disclose that a real,
non-infrastructure hosted service now exists without claiming its
retirement, which remains assigned to `WP 6.3`. One new, disclosed
trade-off (`AT-08`) records the deliberate absence of a persistent
notification model this release, per the approved contract's own
Persistence Requirements ("None"). This Work Package's own re-derivation
of `Namespace Register.md`'s per-namespace file counts, directly via
`grep` rather than trusting the existing table, found the `Tempest.Samples`
row itself had drifted stale since `WP 5.2` — corrected in the same
commit as the finding.

**`WP 6.0` (Reporting Framework)** — implemented directly against the
same, unrevised architecture and Contract Review packages, and the
first of `v0.6.0`'s five implemented Work Packages to match its own
nominal numeric position. `docs/architecture/Platform Service Map.md`
gained a new Reporting entry, following the identical documentation
shape every prior new platform service's own entry has used. `ADR-0040`
formally settles the Reporting-vs-Export/Import orthogonality
`Required ADRs.md` anticipated — the last remaining reserved-ADR-number
gap (`ADR-0040`) is now filled, so `docs/adr/` runs `ADR-0001` through
`ADR-0046` with no gaps at all — plus two genuine implementation-phase
decisions the Contract Review left open: the additive
`IReportTemplate<TDefinition>`/`PlainTextReportTemplate<TDefinition>`
elaboration (filling the brief's own "Template abstraction" deliverable
without touching any approved interface), and a deliberate decision
*not* to build an "Export abstraction" despite the brief naming it as
scope — doing so would duplicate `WP 6.7`'s own future scope and
contradict this very ADR's own orthogonality decision.
`docs/governance/Quality/Technical Debt Register.md` gained one new,
disclosed trade-off (`AT-09`, no delivery-channel abstraction or
durable report history this release) — matching the approved contract's
own Future Extension Points exactly, not a newly-discovered gap. This
Work Package's own dedicated `WP6.0 Platform Integration
Demonstration.md` records, for each of Identity/Settings/Persistence/
Audit/Notifications, whether it was used, why, and what the coupling
rationale is — Persistence is the one deliberately not consumed,
matching the approved contract's own "Persistence Requirements: None."

**`WP 6.3` (REST API)** — implemented directly against the same,
unrevised architecture and Contract Review packages, and the second of
`v0.6.0`'s six implemented Work Packages to match its own nominal
numeric position. `docs/architecture/Platform Service Map.md` gained a
new REST API entry, following the identical documentation shape every
prior new platform service's own entry has used. Four new ADRs:
`ADR-0047`/`ADR-0048`/`ADR-0049` formally settle the three questions
`Required ADRs.md` anticipated (hosted-service placement, Command
Framework dispatch, ASP.NET Core/Kestrel adoption); `ADR-0052` is
genuinely implementation-driven, not anticipated by `Required ADRs.md`
at all, resolving `Risk Register.md`'s own `R1` residual mitigation
("must decide whether `CurrentPrincipalAccessor` needs to become
request-scoped") — empirically, not by reasoning alone: an
`AsyncLocal<T>`-backed implementation was built and tested directly,
regressed 17 pre-existing tests, and was rejected in favour of a
per-request, non-mutating identity resolution that never touches the
shared ambient state. `docs/governance/Engineering/Hosted Services
Register.md` gained its first two entries and its own Coverage Status
correction — a disclosed governance-documentation finding: this
register was never updated when `WP 6.2` added
`NotificationSampleHostedService`, so its own "zero production hosted
services exist" text survived, stale, through an entire Work Package
that directly contradicted it. `docs/governance/Quality/Technical Debt
Register.md` gained three new tracked debt items (`TD-13` no real
authentication, `TD-14` no TLS, `TD-15` an ambient-principal
Audit-attribution gap under REST invocation) and one new trade-off
(`AT-10`, no request-parameter binding); `AT-07` ("Zero real hosted
services exist beyond the infrastructure") is updated from disclosed-
but-not-claimed (`WP 6.2`'s own wording) to genuinely Retired — this is
the Work Package that trade-off's own revisit trigger explicitly named
in advance; `TD-04` (the `IHostedService` naming-proximity concern) is
annotated, since real usage evidence (a genuine ASP.NET Core dependency
now coexisting with this platform's own, differently-shaped
`IHostedService`) has arrived for the first time.

**`WP 6.7` (Export/Import)** — implemented directly against the same,
unrevised architecture and Contract Review packages, completing the
Reporting/Export/Import orthogonality question `ADR-0040` first raised.
`docs/architecture/Platform Service Map.md` gained a new Export/Import
entry, following the identical documentation shape every prior new
platform service's own entry has used. One new ADR, `ADR-0051`, formally
settles the orthogonality question `Required ADRs.md` anticipated and
records two further genuine implementation-phase decisions the Contract
Review left open: the additive `IExportableKind`/`IImportable`
Kind-routing mechanism (closing the approved contract's own
multi-destination-import gap without changing `IImportService`'s own
shape, via a concrete-type dual registration reusing `ADR-0044`'s own
`CurrentPrincipalAccessor` precedent), and the additive
`IExportFormat`/`IExportPayloadSerializer` pair filling the brief's own
"Format abstraction"/"Serialization abstraction" scope. `docs/governance/
Quality/Technical Debt Register.md` gained two new trade-offs (`AT-11`
no compression/encryption of exported content, `AT-12` no
schema-upgrade/migration path) — both matching the approved contract's
own Future Extension Points exactly, not newly-discovered gaps. This
Work Package's own repository review found three further, genuine,
pre-existing drifts, none related to its own scope: `Platform Service
Map.md`'s own Audit and Notifications "Consumers" entries had read
"none yet implemented" since before `WP 6.0` first shipped a real
consumer of each — corrected here; and `docs/governance/Engineering/
Interface Register.md`, `Dependency Injection Register.md`, and `Module
Register.md` had each gone stale since `WP 5.2`, missing every public
interface, DI registration, and sample module `WP 6.1` through `WP 6.3`
added — each register's own Coverage Status is corrected from
"Complete" to "Partial," disclosing the exact gap, with only this Work
Package's own new entries added and the larger, six-Work-Package
backfill explicitly left for `WP 6.8` (Platform Services Integration
Review).

**`WP 6.6` (Licensing Framework)** — implemented directly against the
same, unrevised architecture and Contract Review packages, and the
final production implementation Work Package of `v0.6.0`.
`docs/architecture/Platform Service Map.md` gained a new Licensing
entry, following the identical documentation shape every prior new
platform service's own entry has used — the last such entry this
release will add. One new ADR, `ADR-0050`, formally settles the
placement question `Required ADRs.md` anticipated (pre-container leaf,
Host-fatal on invalid) and resolves a genuine question the architecture
phase left explicitly open: `Risk Register.md`'s own `R5` asked whether
every "invalid" category (missing, expired, malformed) warrants
Host-fatal treatment. `ADR-0050` answers precisely: missing is a valid,
unrestricted-but-uncapable default, never Host-fatal; expired and
malformed are Host-fatal, per `ADR-0013`'s existing classification,
unmodified. `docs/governance/Quality/Technical Debt Register.md` gained
one new tracked debt item (`TD-16`, no cryptographic license file
signature verification, mirroring `TD-13`'s own undisclosed-
authentication precedent for the REST API) and one new trade-off
(`AT-13`, no remote validation/activation, floating/seat-based
licensing, or renewal/grace-period model — all named explicitly in the
approved contract's own Future Extension Points). This Work Package's
own repository review re-derived every touched register directly and
found no further stale figures beyond what `WP 6.7`'s own review had
already disclosed (the `Interface Register.md`/`Dependency Injection
Register.md`/`Module Register.md` gap, still `Partial`, still left for
`WP 6.8`'s own backfill — this Work Package added only its own three
new interfaces, one new registration, and one new module to each,
exactly as `WP 6.7` did).

**`WP 6.8` (Platform Services Integration Review & Release
Certification)** — a certification review, not a feature Work Package;
no architecture or Contract Review document was revised, and no
production code was written. `Interface Register.md`, `Dependency
Injection Register.md`, and `Module Register.md` were each fully
backfilled — every one of the 64 public interfaces, 26 named DI
registrations, and 15 production modules TempestOS ships is now
correctly recorded, closing the gap `WP 6.7` first disclosed and `WP
6.6` correctly left in place. `docs/releases/v0.6.0/Risk Register.md`
gained four status updates (`R2`, `R3`, `R4` fully closed with fresh
evidence; `R6` updated to reflect its own partial, not perfect,
mitigation). No new ADR was produced — this Work Package audits
decisions already made; it does not make new ones. Nine completion
deliverables were produced under `docs/releases/v0.6.0/`, prefixed
`WP6.8`, culminating in a `CERTIFIED WITH ACCEPTED TECHNICAL DEBT`
recommendation.

**`WP 7.1F` (Engineering Core Integration Review & Certification)** — a
certification review of the complete Engineering Core, not a feature
Work Package; no production code was written. Found the identical
governance-register-drift pattern `WP 6.8` itself found and closed for
`v0.6.0`, recurring a second time: `Interface Register.md` (64 → 75),
`Dependency Injection Register.md` (26 → 30 named registrations), and
`Module Register.md` (15 → 19) had each gone stale since `WP 6.8`,
undetected across all five Engineering Foundation Work Packages — now
fully backfilled a second time. Also found and wrote the Engineering
Data Model's own missing concept guide
(`docs/academy/02 Runtime Architecture/15-engineering-data-model.md`), required by
`WP7.0C Academy Plan.md` since `WP 7.1A` and never produced or
disclosed as missing. No new ADR was produced. Ten completion
deliverables were produced under `docs/releases/v0.7.0/`, prefixed
`WP7.1F`, plus `ENGINEERING_CORE_COMPLETION_REPORT.md` (repository
root, the programme's own permanent historical milestone document),
culminating in an `ENGINEERING CORE CERTIFIED WITH ACCEPTED TECHNICAL
DEBT` recommendation.

**`WP 7.3A` (Requirements Engine)** — the first implementation Work
Package of the Systems Engineering Foundation phase. `docs/architecture/
Platform Service Map.md` gained a new, fully-populated Requirements
Engine entry (replacing its own "planned, no code exists" placeholder),
following the identical documentation shape every prior new platform
service's own entry has used. Four new ADRs (`ADR-0058`–`ADR-0061`)
ratify every question `WP7.2C Required ADR Catalogue.md` reserved, with
zero further genuine implementation-phase question left unanswered.
This Work Package's own repository review found and disclosed (without
fixing, being outside its own scope) a genuine, pre-existing drift:
`docs/governance/Engineering/Platform Services Register.md` and
`Platform Service Map.md` itself had never gained rows for the four
Engineering Foundation frameworks (`WP 7.1A`–`WP 7.1E`), a gap
`WP 7.1F`'s own certification review did not check — see `Platform
Services Register.md`'s own disclosure note. `Interface Register.md`
(75 → 80), `Dependency Injection Register.md` (30 → 31 named
registrations), and `Module Register.md` (19 → 20) were each kept
current directly at implementation time, not backfilled afterward — the
first Work Package since `WP 7.1F` established the practice to actually
follow it.

**`WP 7.4.0` (Release Preparation & Product Baseline)** — a complete
release-preparation review, not a feature Work Package; no production
code written. Found and corrected `Documentation Register.md`'s own
long-disclosed stale Directory Map counts (`docs/adr/` 39 → 61,
`docs/academy/02 Runtime Architecture/` 11 → 16, `docs/academy/03 Work Packages/` 32 → 57 at time
of correction, `docs/academy/04 Design Patterns/` 4 → 5) — the full re-derivation
this register's own "Last Reviewed" field had recommended since `v0.6.0`
Release Engineering, closing that specific `FCR-0005` instance. Two
previously-stale release-document skeletons (`docs/releases/v0.7.0/
ReleaseNotes.md`, `Retrospective.md`) fully populated;
`WorkPackages.md`'s own long-stale "not started" status corrected,
marked superseded, not deleted.

**`WP 8.0A` (Engineering Workspace Architecture)** — an architecture-
only Work Package; no production code written. Added
`docs/releases/v0.8.0/` (five new deliverables: `Workspace Architecture
Document.md`, `UI Architecture.md`, `Navigation Specification.md`,
`Object Relationship Diagrams.md`, `User Workflow Diagrams.md`) and the
prepared-in-advance `v0.8.0` release skeletons (`WorkPackages.md`,
`ReleaseNotes.md`, `Retrospective.md`), mirroring `v0.6.0`→`v0.7.0`'s
own branch-preparation precedent exactly.

**`WP 8.0B` (Workspace Contracts)** — a contract-review-only Work
Package; no production code, no compiled interface. Added four further
`docs/releases/v0.8.0/` deliverables (`Workspace Contracts.md`,
`Sequence Diagrams.md`, `Lifecycle Definitions.md`, `Dependency
Rules.md`).

**`WP 8.1A` (Workspace Shell)** — the first implementation Work Package
of `v0.8.0`; 27 new production files under `src/Tempest.App/Workspace/`
plus `src/Tempest.App/AssemblyInfo.cs`, 1 modified (`Program.cs`). Added
one further `docs/releases/v0.8.0/` deliverable
(`WP8.1A Implementation Report.md`).

**`WP 8.0C` (Engineering Workspace UX Specification)** — a
product-and-UX-design-only Work Package; no production code, no
`src/`/`tests/` change of any kind. Added nine further
`docs/releases/v0.8.0/` deliverables (`UX Specification.md`, `Screen
Catalogue.md`, `User Journey Maps.md`, `Interaction Specification.md`,
`Navigation Maps.md`, `Wireframe Sketches.md`, `Workspace Behaviour
Specification.md`, `Engineering Cockpit Specification.md`) and two new
ADR files (`ADR-0069`, `ADR-0070`).

**`WP 8.1B` (Navigation & Project Explorer)** — the second
implementation Work Package of `v0.8.0`; 7 new production files (2
under `src/Tempest.App/Workspace/`, 4 under
`src/Tempest.App/Workspace/Samples/`, 1 under
`src/Samples/Tempest.Samples/`), 5 modified. Added one further
`docs/releases/v0.8.0/` deliverable (`WP8.1B Implementation Report.md`)
and one new ADR file (`ADR-0071`).

**`WP 8.1C` (Engineering Cockpit)** — the third implementation Work
Package of `v0.8.0`; 5 new production files under
`src/Tempest.App/Workspace/`, 3 modified. Added one further
`docs/releases/v0.8.0/` deliverable (`WP8.1C Implementation Report.md`)
— zero new ADR files (implements `ADR-0069`/`ADR-0070` directly).

**`WP 8.2A` (Engineering Domain Architecture)** — an architecture-only
Work Package; no production code, no `src/`/`tests/` change of any
kind. Added nine further `docs/releases/v0.8.0/` deliverables
(`Engineering Domain Architecture.md`, `Canonical Object Catalogue.md`,
`Relationship Catalogue.md`, `Lifecycle Specification.md`,
`Configuration Management Specification.md`, `Digital Thread
Specification.md`, `Metadata Specification.md`, `Validation
Specification.md`, `Engineering Object Interaction Diagrams.md`) and
three new ADR files (`ADR-0072`, `ADR-0073`, `ADR-0074`).

**`WP 8.2B` (Engineering Domain Contracts)** — a contract-review-only
Work Package; no production code, no compiled interface, no
`src/`/`tests/` change of any kind. Added eight further
`docs/releases/v0.8.0/` deliverables (`Engineering Domain Contracts.md`,
`Interface Catalogue.md`, `Lifecycle Contract Specification.md`,
`Relationship Contract Specification.md`, `Validation Contract
Specification.md`, `Digital Thread Contract Specification.md`,
`Sequence Diagrams.md`, `Dependency Rules.md`) and two new ADR files
(`ADR-0075`, `ADR-0076`).

## Academy Status

86 articles across 7 categories (Introduction, Engineering Principles,
Runtime Architecture, Work Package retrospectives, Design Patterns, Case
Studies, Engineering Standards), plus `Academy Index.md`, `Academy
Masterclass Roadmap.md`, `Academy Audit Report.md`, and `Contributor
Learning Path.md` — re-derived directly (`find docs/academy -name
"*.md"`) by `WP 6.8`, consistent with `WP 6.0`'s/`WP 6.2`'s/`WP 6.3`'s/
`WP 6.7`'s/`WP 6.6`'s own prior passes. Every completed Work Package has a matching
retrospective, including `WP 5.0A` through `WP 5.4`. Maintenance
obligation (Engineering Governance §6) verified honoured by two
independent audits (`WP 4.4F`, and the Academy Register built during
`WP 4.5A`); `WP 5.0A` updated two existing articles' (`06-platform-
layering.md`, `08-failure-isolation.md`) "Future Evolution" sections,
`WP 5.0B` confirmed those predictions against the real implementation
with no correction needed, `WP 5.0D` added a genuine, non-obvious
implementation finding (`const` fields not forcing assembly load) to the
Shell's own concept guide, `WP 5.0S` added a new "Security" category
teaching threat modelling, secure plugin architecture, trust boundaries,
and least privilege from first principles, `WP 5.1A` added a new
"Command Framework" category (a new concept guide,
`11-command-framework.md`) and updated `08-failure-isolation.md` with a
genuinely new, fifth failure-isolation case (Case 5 — Command Dispatch:
propagate, don't isolate) that document's own "Future Evolution" section
had explicitly anticipated testing, `WP 5.1B` added the matching
implementation retrospective and confirmed the concept guide's own
design against the real, working implementation, with one genuine,
non-obvious implementation finding (`CommandHandlerTable`) added to both,
`WP 5.2` added a new "Diagnostics" category (a new concept guide,
`12-diagnostics-and-composite-logging.md`) plus its own retrospective,
teaching the `Func<T>` lazy-accessor pattern and the two-named-debts
decision from first principles, `WP 5.3` updated `Building a
Module.md` in place (no new concept guide — this Work Package extends
already-covered material, not a new platform capability) plus its own
retrospective, teaching why a scoped tooling Work Package legitimately
has no preceding architecture phase, and `WP 5.4` added a `v0.5.0
Release Retrospective` — deliberately shaped around what was achieved,
architectural lessons, implementation lessons, repository maturity, and
recommendations for `v0.6.0`, rather than the standard 13-section
per-feature template, since a whole-release verification pass is not the
same kind of document as a single capability's own design retrospective
(disclosed explicitly in that document's own "What This Document Is").
`WP 6.1` added the 13-section `WP6.1-permissions-and-identity-
implementation.md` retrospective — the first Academy retrospective of
the Platform Services phase — teaching the config-sourced Role model,
the fail-closed-by-default identity resolution decision, and the
`AsyncLocal<T>`-vs-ambient-field finding from first principles. `WP 6.4`
added `WP6.4-settings-framework-implementation.md`, teaching the
shared-Persistence-abstraction decision, the per-key async-lock pattern
reused across two services, and the deliberate choice not to add a
sensitive-value flag to an approved interface for a need this release
does not yet have. `WP 6.5` added `WP6.5-audit-framework-
implementation.md`, teaching the Logging/Diagnostics/Audit
orthogonality decision, the fire-and-forget-vs-awaited recording-model
tension and its resolution, the correlation-identifier-via-`Detail`
convention, and — as a genuine, disclosed engineering-review finding,
not a planned lesson — the premature-resource-disposal bug found in
two prior Work Packages' own Host-registration tests. `WP 6.2` added
`WP6.2-notification-framework-implementation.md`, teaching the
Event-Bus-orthogonality decision, the genuine C# generic-constraint
impossibility that prevented literal delegation to `IEventBus` and its
resolution (mirror the internal shape instead), the additive
severity/category elaboration, the deliberate `Warning`-vs-`Error`
logging-level departure, and — as a genuine, disclosed engineering-review
finding, not a planned lesson — the exact-static-type-dispatch defect
found against this Work Package's own sample consumers while writing
their integration tests. `WP 6.0` added
`WP6.0-reporting-framework-implementation.md`, teaching the
Reporting-vs-Export/Import orthogonality decision, the additive
Template Strategy elaboration (data/layout/rendering separation without
touching any approved interface), the deliberate choice not to build an
"Export abstraction" despite the brief naming it as scope, and the
cross-service integration pattern (permission check, Audit record,
Notifications publish, all at the calling layer, never inside
`IReportingService` itself) as a concrete precedent any future
Reporting consumer can copy directly. `WP 6.3` added
`WP6.3-rest-api-implementation.md`, teaching the hosted-service/Command-
Framework/Kestrel-adoption decisions (`ADR-0047`/`ADR-0048`/`ADR-0049`),
the empirically-verified `CurrentPrincipalAccessor` decision
(`ADR-0052`) — including the 17-test regression the rejected
`AsyncLocal<T>` alternative actually produced, not merely a theoretical
concern — and the `Detail`-carried-caller-identity convention for
REST-originated Audit records. `WP 6.7` added
`WP6.7-export-import-implementation.md`, teaching the Kind-routing
mechanism that closes the approved contract's own multi-destination-
import gap without changing any approved interface, the reuse of
`ADR-0044`'s own dual-registration pattern for a structurally identical
problem, the two-abstraction (Format/Serialization) resolution to the
brief's own named scope, and — as a genuine, disclosed
engineering-review finding, not a planned lesson — the three
governance-documentation drifts (two Platform Service Map consumer
entries, and three registers stale since `WP 5.2`) found while
re-deriving every touched register directly. `WP 6.6` added
`WP6.6-licensing-framework-implementation.md`, teaching the
missing-file-vs-broken-file Host-fatal resolution to `Risk Register.md`'s
own `R5`, the pre-container leaf-construction pattern
`PlatformVersionProvider` already established and this Work Package
reused rather than reinvented, and — proven directly, not merely
assumed — that this design change regresses none of the 24 pre-existing
tests that build a real `TempestHost`. `WP 6.8` added
`WP6.8-platform-services-integration-review.md`, mirroring `WP 5.4`'s
own whole-release-review format (What Was Achieved, Architectural
Lessons, Implementation Lessons, Repository Maturity, Recommendations,
Key Takeaways, rather than the standard 13-section per-feature
template) — teaching that a closing, whole-release review is not a
formality, that re-verifying a risk's own claimed resolution against
fresh evidence is cheap and catches real staleness, and that a
"Certified With Accepted Technical Debt" outcome is more honest than a
bare "Certified for Release" whenever a release ships disclosed,
deliberate limitations.

**`WP 7.0A`** added `WP7.0A-future-capability-register-and-product-vision.md`,
teaching that a product-vision document benefits from the same
evidentiary discipline as an ADR, and that a future-capability
register's honesty is measured by what it refuses to invent. **`WP
7.0B`** added `WP7.0B-engineering-foundation-planning-and-capability-architecture.md`,
teaching that a dependency graph is an analytical tool, not just a
diagram — it surfaced a real asymmetry between the Requirements Engine
and Verification & Validation capabilities that a flat list had left
implicit. **`WP 7.0C`** added
`WP7.0C-engineering-foundation-contract-review.md`, teaching that
contract-level design can resolve an ambiguity a capability-level
dependency graph left open. **`WP 7.1A`** added
`WP7.1A-engineering-data-model-implementation.md` — the standard
13-section template, this phase's first, since it is this phase's first
implementation Work Package — teaching that a Contract Review's
proposed signatures are a strong default, not a guarantee (one base-
exception class modifier was corrected to match universal existing
convention), and that a design decision not specified in a contract (how
revision history is looked up) can still matter architecturally.
`docs/engineering/Engineering Principles.md`, a new top-level document,
was also established by `WP 7.1A`. **`WP 7.1B`** added
`WP7.1B-units-and-quantities-framework-implementation.md` — the second
standard 13-section implementation retrospective of this phase —
teaching that `System.Text.Json` requires an explicit `[JsonConstructor]`
attribute for a value type with a hand-written (non-positional-record)
constructor, that a framework with zero Platform Service dependency is a
genuinely different implementation experience (no `TempestHost.cs`
change at all), and that a real architectural gap (Temperature's affine
conversion) can surface during a framework's own implementation even
after contract review found no issue at the interface-signature level.
Also added a new Design Patterns concept guide,
`docs/academy/04 Design Patterns/05-phantom-type-dimension-safety.md` — this
platform's first phantom-type pattern — and extended
`docs/engineering/Engineering Principles.md` with six further
principles (7-12). **`WP 7.1C`** added
`WP7.1C-materials-framework-implementation.md` — the third standard
13-section implementation retrospective of this phase — teaching that a
provenance requirement can resolve a reserved property-typing question
more decisively than the question alone, that a "thin index over an
existing store" can still need its own storage dependency once a
lookup-by-string requirement is actually implemented, and that bounding
a heterogeneous property value to an already-established, small set
(the seven Units & Quantities dimensions) avoids both an unsafe
general-purpose polymorphic mechanism and a premature, invented
property-name taxonomy. No new concept guide — Materials is presented
as a worked example of the Data Model, per `WP7.0C Academy Plan.md`'s
own finding, not a new pattern in its own right. Further extended
`docs/engineering/Engineering Principles.md` with four further
principles (13-16). **`WP 7.1D`** added
`WP7.1D-engineering-calculation-framework-implementation.md` — the
fourth standard 13-section implementation retrospective of this
phase, and the first to include a dedicated Security Review — teaching
that an evidentiary requirement can justify a signature change a
contract review could not have anticipated, that reusing a sibling
framework's own dispatch pattern (the Command Framework's type-erased
registry) works cleanly provided the one deciding property (purity) is
kept explicit and tested, and that a calculation's own true evidentiary
value comes from what it discloses (assumptions, intermediate results,
validation outcome) rather than its final number alone. Also added a
new "02 Runtime Architecture" concept guide,
`13-calculation-framework.md`, distinguishing the Calculation Framework
from the Command Framework — the required output `WP7.0C Academy
Plan.md` itself named. Further extended `docs/engineering/Engineering
Principles.md` with seven further principles (17-23). **`WP 7.1E`**
added `WP7.1E-verification-framework-implementation.md` — the fifth and
final standard 13-section implementation retrospective of this phase,
and the second to include a dedicated Security Review — teaching that a
cross-cutting framework's own best design decision can be reusing an
existing mechanism completely rather than extending it (verification
history needed no new index at all, only the Data Model's own existing
`LinkAsync`/`GetReferencesAsync`), that validating some links while
leaving others open is legitimate when the asymmetry tracks a real
dependency difference, and that narrow, explicitly-named scope
exclusions (Validation, Requirements Management) produced the same
close-to-automatic scope discipline every prior Engineering Foundation
Work Package also reported — now a five-for-five pattern across the
entire programme. Also added a new "02 Runtime Architecture" concept
guide, `14-verification-framework.md`, distinguishing Verification from
both Audit and Calculation Record. Further extended
`docs/engineering/Engineering Principles.md` with five further
principles (24-28), completing that document's own Engineering
Foundation contribution — all five frameworks have now extended it.
**`WP 7.1F`** added
`WP7.1F-engineering-core-integration-review-and-certification.md` — a
closing certification retrospective, mirroring
`WP 6.8`'s own whole-release review format, not the standard 13-section
per-feature template. Also wrote `02 Runtime Architecture/
15-engineering-data-model.md`, the Engineering Data Model's own concept
guide — required output of `WP 7.1A`, named by `WP7.0C Academy Plan.md`
as this programme's "highest-priority new Academy content," never
written, and never disclosed as missing by `WP 7.1A` or any of
`WP 7.1B`–`WP 7.1E`. No further Engineering Principles added — this
Work Package audits, it does not implement.
**`WP 7.2A`** added `WP7.2A-strategic-roadmap-selection-and-programme-
architecture.md` — a whole-review retrospective mirroring `WP 7.0A`/
`WP 7.0B`'s own format, not the standard 13-section per-feature
template. No new concept guide (a planning/governance milestone, not a
feature); no Engineering Principles added.
**`WP 7.2B`** added `WP7.2B-requirements-and-verification-platform-
architecture.md` — a whole-review retrospective mirroring `WP7.0C
Engineering Foundation Contracts.md`'s own format, not the standard
13-section per-feature template. No new concept guide — `WP7.2B Academy
Plan.md` recommends one for the owning implementation Work Package, once
real code exists to derive worked examples from. Reviewed whether
`docs/engineering/Engineering Principles.md` requires extension:
**found no extension warranted** — this Work Package produced
architecture only, and every existing principle was derived from real,
shipped code, never asserted in advance of it.
**`WP 7.2C`** added `WP7.2C-requirements-and-verification-platform-
contract-review.md` — a whole-review retrospective mirroring the same
format, extended to a seventeen-question-per-concept contract review.
No new concept guide — `WP7.2C Academy Plan.md` confirms and extends
`WP7.2B Academy Plan.md`'s own recommendation, now naming two required
concept-guide sections (the primary Requirements pattern, and the
relationship/traceability vocabulary newly detailed at contract level)
for the owning implementation Work Package. Engineering Principles
review re-confirmed: **no extension warranted**, unchanged from
`WP 7.2B`.
**`WP 7.3A`** added `WP7.3A-requirements-engine-implementation.md` (the
standard 13-section implementation retrospective) and
`docs/academy/02 Runtime Architecture/16-requirements-engine.md` (the two
concept-guide sections `WP7.2C Academy Plan.md` named: the three-layer
Requirement-as-Document pattern, and the relationship-kind/traceability
vocabulary). `docs/engineering/Engineering Principles.md` extended with
four further principles (29-32) — the first extension since `WP 7.1E`
completed the Engineering Foundation programme's own five-Work-Package
contribution.
**`WP 7.4.0`** added `WP7.4.0-release-preparation-and-product-baseline.md`
— a whole-review retrospective mirroring `WP 5.4`/`WP 6.8`/`WP 7.1F`'s
own format, reviewing all twelve `v0.7.0` Work Packages together. No new
concept guide — release preparation only. `docs/engineering/Engineering
Principles.md` confirmed requiring no extension, unchanged from
`WP 7.3A`.
**`WP 8.0A`** added `WP8.0A-engineering-workspace-architecture.md` (a
whole-review retrospective mirroring `WP7.2B`'s own architecture-only
format) and `docs/academy/02 Runtime Architecture/17-engineering-workspace.md` (a
new concept guide, written at the architecture stage, to be updated at
implementation — mirroring `10-shell-and-application-composition.md`'s
own precedent). `docs/engineering/Engineering Principles.md` reviewed:
**no extension warranted** — this Work Package produced architecture
only, no implementation to derive a genuine engineering principle from.
**`WP 8.0B`** added `WP8.0B-workspace-contracts.md` (a whole-review
retrospective mirroring `WP7.2C`'s own contract-review-only format) and
updated `docs/academy/02 Runtime Architecture/17-engineering-workspace.md` in place
(second update — not a new file) to reflect the twelve now-frozen
contracts and both resolved ADRs. `docs/engineering/Engineering
Principles.md` reviewed: **no extension warranted**, unchanged from
`WP 8.0A` — contract review produced no implementation to derive a
genuine engineering principle from.
**`WP 8.1A`** added `WP8.1A-workspace-shell-implementation.md` (a
standard 13-section implementation retrospective) and updated
`docs/academy/02 Runtime Architecture/17-engineering-workspace.md` in place a third
time to reflect the now-compiled shell and `ADR-0068`.
`docs/engineering/Engineering Principles.md` reviewed: **no extension
warranted** — every design decision this Work Package made was already
anticipated by `WP 8.0A`/`WP 8.0B`'s own approved architecture and
contracts; no genuinely new engineering principle emerged from
implementing them.
**This paragraph itself had gone four Work Packages stale — `WP 8.0C`,
`WP 8.1B`, `WP 8.1C`, and `WP 8.2A` were each never added here, though
each was correctly reflected in `Academy Register.md` at its own
documentation time. Found and closed by `WP 8.2B`, the same class of
drift `WP 7.1F`/`WP 6.8` each found and closed once before, not
backfilled speculatively beyond what follows.**
**`WP 8.0C`** added `WP8.0C-engineering-workspace-ux-specification.md`
(a whole-review retrospective mirroring `WP 8.0A`/`WP 8.0B`'s own
format) and updated
`docs/academy/02 Runtime Architecture/17-engineering-workspace.md` in
place a fourth time to reflect the target UX across
all 28 named scope areas. `docs/engineering/Engineering Principles.md`
reviewed: **no extension warranted** — a product/UX specification only,
no implementation to derive a genuine engineering principle from.
**`WP 8.1B`** added `WP8.1B-navigation-and-project-explorer-
implementation.md` (a standard 13-section implementation retrospective)
and updated `docs/academy/02 Runtime Architecture/17-engineering-workspace.md` in
place a fifth time to reflect the now-implemented Navigation system and
Project Explorer. `docs/engineering/Engineering Principles.md`
reviewed: **no extension warranted** — every design decision was already
anticipated by `WP 8.0A`/`WP 8.0B`/`WP 8.0C`'s own approved
architecture, contracts, and UX specification.
**`WP 8.1C`** added `WP8.1C-engineering-cockpit-implementation.md` (a
standard 13-section implementation retrospective) and updated
`docs/academy/02 Runtime Architecture/17-engineering-workspace.md` in place a sixth
time to reflect the now-implemented Engineering Cockpit.
`docs/engineering/Engineering Principles.md` reviewed: **no extension
warranted**, unchanged from `WP 8.1B`.
**`WP 8.2A`** added `WP8.2A-engineering-domain-architecture.md` (a
whole-review retrospective mirroring `WP 7.2B`/`WP 8.0A`'s own
architecture-only format) and
`docs/academy/02 Runtime Architecture/18-engineering-domain-architecture.md`
(a new concept guide — the Engineering Domain's own, distinct from the
Workspace's).
`docs/engineering/Engineering Principles.md` reviewed: **no extension
warranted** — this Work Package produced architecture only, no
implementation to derive a genuine engineering principle from.
**`WP 8.2B`** added `WP8.2B-engineering-domain-contracts.md` (a
whole-review retrospective mirroring `WP 7.0C`/`WP 7.2C`/`WP 8.0B`'s own
contract-review-only format) and updated
`docs/academy/02 Runtime Architecture/18-engineering-domain-architecture.md`
in place a second time to reflect the now-frozen public contracts and
`ADR-0075`/`ADR-0076`.
`docs/engineering/Engineering Principles.md` reviewed: **no extension
warranted**, unchanged from `WP 8.2A` — contract review produced no
implementation to derive a genuine engineering principle from.
**`WP 8.2C`** added `WP8.2C-engineering-domain-implementation.md` (a
standard 13-section implementation retrospective) and updated
`docs/academy/02 Runtime Architecture/18-engineering-domain-architecture.md`
in place a third time to reflect the now-compiled implementation, `ADR-0077`,
`ADR-0078`, and `ADR-0079`. `docs/engineering/Engineering Principles.md`
reviewed: **no extension warranted** — every design decision this Work
Package made was already anticipated by `WP 8.2A`/`WP 8.2B`'s own
approved architecture and contracts; no genuinely new engineering
principle emerged from compiling and implementing them, mirroring `WP
8.1A`'s own identical finding for the Workspace Shell.
**`WP 8.9.0`** added `WP8.9.0-release-preparation-and-product-baseline.md`
(a whole-review retrospective mirroring `WP 5.4`/`WP 6.8`/`WP 7.4.0`'s
own format). No new concept guide — release preparation only.
`docs/engineering/Engineering Principles.md` confirmed requiring no
extension, unchanged from `WP 8.2C`.

## Governance Status

27 registers (32 governance documents total, including the Index,
Philosophy, Audit Report, Maturity Report, and Future Work Package
Guidelines), fully cross-referenced, re-verified during `v0.4.0` Release
Engineering and every Work Package since. Traceability for both
Navigation and the Shell is complete end to end — no Pending cells remain
(`docs/governance/Delivery/Traceability Matrix.md`). One stale, pre-
existing entry unrelated to `WP 5.0D`'s own scope was found and corrected
along the way: `Technical Debt Register.md`'s TD-07. `WP 5.0S` added two
new, disclosed debt items following its own security audit — `TD-09`
(plugin trust boundary) and `TD-10` (Navigation ownership gap) — both
Open, both requiring a future Architecture Work Package, neither a
regression of anything previously Resolved. `Decision Register.md`
gained `D-017` (conducting `WP 5.0S` as a dedicated security audit Work
Package). `WP 5.1A` added `TD-11` (command/navigation registration-order
squatting, `CMD-1`) and widened `TD-09`'s own scope to name the Command
Framework as a second affected surface; `Decision Register.md` gained
`D-018` (splitting `WP 5.1` into `WP 5.1A`/`WP 5.1B`); `Risks.md`'s R3 is
now Retired (the Event Bus/Command Framework cross-reference it required
now exists). `WP 5.1B` confirmed `TD-09`/`TD-11` present in the real
implementation exactly as designed (neither worsened, neither newly
introduced) and disclosed no new debt. Several Engineering and Delivery
registers (`Platform Services Register.md`, `Interface Register.md`,
`Namespace Register.md`, `Dependency Injection Register.md`, `Exception
Register.md`, `Module Register.md`, `Feature Register.md`, `Traceability
Matrix.md`) had drifted stale since `WP 5.0D` without any Work Package
since having touched them — found and corrected during `WP 5.1B`'s own
mandatory repository review. `WP 5.2` resolved `TD-02` (`CompositeLogSink`)
and reassessed `TD-01` (re-scoped forward again, not migrated —
`D-020`); `Decision Register.md` also gained `D-019` (the Event
Framework/Diagnostics premise redirect). ADR Register, Rejected Designs
Register, and every Engineering/Delivery register touched by this Work
Package's own new types were updated in the same commit; `Architecture
Document Register.md`'s stale Command Framework marker (see Documentation
Status, above) was corrected during this Work Package's own repository
review, unrelated to its own scope. `WP 5.3` added `RD-0045` (local-folder
vs. NuGet-packaged template distribution) and touched no Technical Debt
item (its own scope does not concern `TD-01`–`TD-11`). That Work
Package's own repository review found three further, genuine,
pre-existing drifts: (1) `RD-0042`–`RD-0044` had been added to
`Rejected Designs Register.md` during `WP 5.2` but never actually written
into `docs/architecture/Rejected Designs.md` itself — the register's own
declared Source of Truth — backfilled here, unchanged in content; (2)
`Engineering Governance.md` §11 had not been updated when `WP 5.2` added
the `Tempest.Core.Diagnostics` namespace; (3) `Governance Register.md`'s
own Compliance Matrix had not been updated since `WP 5.0D` — four
completed Work Packages (`WP 5.0S`, `WP 5.1A`, `WP 5.1B`, `WP 5.2`) were
missing entirely, and `WP 5.0D`'s own row still carried a
`*(this commit)*` placeholder never backfilled with its real hash. All
five rows backfilled, verified directly against `git log`.

**`WP 5.4` (v0.5.0 Release Candidate & Engineering Sign-Off)** performed
a full, independent repository review rather than a scope-limited one,
and found two further, genuine, silent arithmetic undercounts: the
Exception Register's own stated total (30) had never matched its own
Entries table (31, true since `WP 5.1B` first introduced the mismatch);
and the Academy Register's own "03 Work Packages" count had undercounted
its own table by one for at least two consecutive Work Packages, with
the academy-wide grand total inheriting the same undercount rather than
being independently re-derived each time. Both are now corrected, backed
by a direct file-system count rather than an incremented prior figure.
`docs/releases/v0.4.0/Risks.md`'s remaining four risks with a "residual
carries forward" caveat (`R5`, `R7`, `R8`, `R9`) were each confirmed
resolved by a specific, named `v0.5.0` Work Package and retired in
full — all ten risks in that register are now Retired. This is the
fourth Work Package in a row to find real, previously-unnoticed
governance drift during its own repository review — see `WP 5.4`'s own
retrospective, "Repository Maturity," for the standing-practice
recommendation this pattern produced.

**`WP 6.1` (Permissions & Identity)** added `ADR-0043` (Identity Model
Scope) and `ADR-0044` (Authorization Enforcement Point) — both Accepted,
both cross-referencing the `Required ADRs.md` catalogue entry each
formally authors. `Technical Debt Register.md`'s `TD-09`, `TD-10`, and
`TD-11` entries were each updated in place — none marked Resolved, since
this Work Package deliberately built only the enforcement mechanism, not
the three follow-on calls into `NavigationService`, Command/Navigation
registration, and plugin loading that would actually close them; each
entry now names `ADR-0044` as the mechanism a future, explicitly-scoped
Work Package should use. No new Technical Debt item was found stale or
drifted during this Work Package's own repository review.

**`WP 6.4` (Settings Framework)** added `ADR-0041` (Shared Persistence
Abstraction) and `ADR-0042` (Settings Distinct From Configuration) —
both Accepted, both formally authoring their own `Required ADRs.md`
catalogue entry. `docs/releases/v0.6.0/Risk Register.md`'s `R4` is now
Partially Retired (the abstraction exists; whether `WP 6.5` actually
reuses it remains open) and `R8` is confirmed, not retired (the
anticipated "minimal, key-lookup-only" limitation shipped exactly as
predicted). No new Technical Debt item was found stale or drifted
during this Work Package's own repository review.

**`WP 6.5` (Audit Framework)** added `ADR-0045` (Audit orthogonality,
recording model, permission gating, Persistence sufficiency) — Accepted,
formally authoring its own `Required ADRs.md` catalogue entry.
`docs/releases/v0.6.0/Risk Register.md`'s `R8` is confirmed a second
time, still not retired — this Work Package's own explicit Persistence
Validation judged `IPersistenceStore` adequate for correctness, with no
extension made, and named a concrete revisit trigger (a measured
performance problem or a named scale requirement) rather than leaving
the risk vague. `docs/governance/Quality/Technical Debt Register.md`
gained a new, permanent item, `TD-12`, disclosing the same
client-side-filtering characteristic as an ongoing, cross-release
concern. This Work Package's own repository review found and fixed a
genuine, previously-uncaught bug in two already-committed Work
Packages' own test files (`WP 6.4`'s `SettingsHostRegistrationTests.cs`;
`WP 6.1`'s own `IdentityHostRegistrationTests.cs` was checked and found
unaffected, since it never used a `TempDirectory`) — corrected in the
same commit as the finding, per this project's own standing practice of
fixing drift encountered along the way, not only drift a Work Package's
own brief names.

**`WP 6.2` (Notification Framework)** added `ADR-0046` (Notifications
derived from Events, not a replacement pub/sub — dispatch model,
severity/category elaboration, logging level) — Accepted, formally
authoring its own `Required ADRs.md` catalogue entry (`ADR-0040` and
`ADR-0047`–`ADR-0051` remain reserved). `docs/governance/Quality/
Technical Debt Register.md`'s `AT-03` and `AT-07` entries were each
annotated in place (see Documentation Status, above); one new trade-off,
`AT-08`, was added. This Work Package's own repository review, which
re-derived every register touched directly rather than trusting the
existing text, found and corrected three further, genuine, pre-existing
drifts unrelated to its own scope: `ADR Register.md`'s own commit count
("48") had not been re-derived since at least `WP 6.5`, undercounting
the real, `git log`-verified figure; `Namespace Register.md`'s
`Tempest.Samples` row had drifted stale at "14" since `WP 5.2`, three
intervening Work Packages having each added files without the row being
updated; and `PROJECT_STATUS.md`'s own Academy Status section had
drifted stale at "77 articles" since before `WP 6.1`, for the identical
reason. All three are corrected in this same commit, backed by direct
repository counts, not incremented prior figures.

**`WP 6.0` (Reporting Framework)** added `ADR-0040` (Reporting is
DI-public and orthogonal to Export/Import — template abstraction,
cross-service integration, scope boundaries) — Accepted, formally
authoring its own `Required ADRs.md` catalogue entry and filling the
last remaining reserved-ADR-number gap: `docs/adr/` now runs `ADR-0001`
through `ADR-0046` with no gaps. `docs/governance/Quality/Technical
Debt Register.md` gained one new, disclosed trade-off (`AT-09`) — no
delivery-channel abstraction or durable report history this release,
matching the approved contract's own Future Extension Points, not a
newly-discovered gap. No existing Technical Debt item required
annotation — Reporting introduces no instance of any previously-tracked
gap (`TD-01` through `TD-12`, `AT-01` through `AT-08`). This Work
Package's own repository review re-derived every touched register
directly and found no further stale figures beyond what `WP 6.2`'s own
review had already corrected.

**`WP 6.3` (REST API)** added `ADR-0047` (REST API is a hosted service),
`ADR-0048` (REST dispatches through the Command Framework), `ADR-0049`
(adopting ASP.NET Core/Kestrel) — all three Accepted, formally
authoring their own `Required ADRs.md` catalogue entry — and `ADR-0052`,
a fourth, genuinely implementation-driven ADR not anticipated by
`Required ADRs.md` at all, documenting the empirically-verified
`CurrentPrincipalAccessor` decision. `docs/governance/Quality/Technical
Debt Register.md` gained three new tracked debt items (`TD-13`, `TD-14`,
`TD-15`) and one new trade-off (`AT-10`); `AT-07` is updated to Retired
and `TD-04` is annotated (see Documentation Status, above).
`docs/governance/Engineering/Hosted Services Register.md` — Partial
coverage since `WP 4.5A`, and never actually updated when `WP 6.2`
shipped this codebase's first real hosted service — is corrected here,
populated with both `NotificationSampleHostedService` and
`RestApiHostedService`, and its own Coverage Status changed to Complete.
This Work Package's own repository review, re-deriving every touched
register directly, found this one further, genuine, pre-existing
governance-documentation drift beyond what `WP 6.0`'s own review had
already confirmed clean.

**`WP 6.7` (Export/Import)** added `ADR-0051` (Export/Import Is
Orthogonal to the Internal Persistence Abstraction — Kind Routing,
Format/Serialization Abstractions, and Scope Boundaries) — Accepted,
formally authoring its own `Required ADRs.md` catalogue entry:
`docs/adr/` then ran `ADR-0001` through `ADR-0049`, then
`ADR-0051`–`ADR-0052`, with only `ADR-0050` (Licensing) still reserved.
`docs/governance/Quality/Technical Debt Register.md` gained two new
trade-offs (`AT-11`, `AT-12`); no existing Technical Debt item required
annotation. This Work Package's own repository review, re-deriving
every touched register directly, found three further, genuine,
pre-existing governance-documentation drifts, none related to its own
scope: `docs/architecture/Platform Service Map.md`'s own Audit and
Notifications "Consumers" entries had read "none yet implemented" since
before `WP 6.0` first shipped a real consumer of each — corrected here.
More substantially, `docs/governance/Engineering/Interface Register.md`,
`Dependency Injection Register.md`, and `Module Register.md` had each
gone stale since `WP 5.2` — six consecutive Work Packages' worth of
interfaces (23), DI registration call sites (10), and sample modules
(6) were missing entirely from all three, each register's own "Coverage
Status: Complete" line surviving unchallenged the entire time. Each
register's own Coverage Status is corrected to "Partial," the exact gap
disclosed explicitly, with only this Work Package's own new entries
added — a full backfill is recommended as `WP 6.8` (Platform Services
Integration Review)'s own closing-audit task, exactly the kind of
accumulated, multi-Work-Package drift that Work Package exists to
catch.

**`WP 6.6` (Licensing Framework)** added `ADR-0050` (License Validation
Is a Host-Startup, Host-Fatal Gate — Except a Missing License File,
Which Is a Valid, Unrestricted Default) — Accepted, formally authoring
its own `Required ADRs.md` catalogue entry and filling the very last
remaining reserved-ADR-number gap: `docs/adr/` now runs `ADR-0001`
through `ADR-0052` with no gaps at all — every ADR `Required ADRs.md`
ever reserved a number for is now a real, Accepted file.
`docs/governance/Quality/Technical Debt Register.md` gained one new
tracked debt item (`TD-16`, no cryptographic license file signature
verification) and one new trade-off (`AT-13`, no remote validation/
activation, floating/seat-based licensing, or renewal/grace-period
model). This Work Package's own repository review, re-deriving every
touched register directly, found no further stale figures beyond the
`Interface Register.md`/`Dependency Injection Register.md`/`Module
Register.md` gap `WP 6.7` had already disclosed as `Partial` — this
Work Package added only its own three new interfaces, one new
registration, and one new module to each, leaving the larger,
six-Work-Package backfill exactly where `WP 6.7` left it, for `WP 6.8`'s
own closing audit.

**`WP 6.8` (Platform Services Integration Review & Release
Certification)** produced no new ADR — it is a certification review,
not a decision-making Work Package. `Interface Register.md`,
`Dependency Injection Register.md`, and `Module Register.md` were each
fully backfilled, closing the gap `WP 6.7` first disclosed: all 64
public interfaces, 26 named DI registrations (28 raw `Singleton`/
`AddInstance` call sites), and 15 production modules are now correctly
recorded, and each register's own Coverage Status is corrected from
`Partial` to `Complete`. A genuine, pre-existing arithmetic drift,
unrelated to the larger gap, was found and corrected in the same pass:
`Interface Register.md`'s own Classification Summary had read
"Host-owned = 6" while its own Entries table already listed 7 rows
marked Host-owned. `docs/releases/v0.6.0/Risk Register.md` gained four
updates: `R2` and `R3` fully closed with fresh, independent evidence
(`git log`'s own commit order; a fresh `grep` of
`RestApiHostedService.cs`); `R4` fully closed (Audit's own reuse of
Persistence re-confirmed directly); `R6` updated to disclose that its
own mitigation held only partially, not perfectly — exactly the
Interface/DI/Module Register drift this same risk predicted, now fully
corrected. Nine completion deliverables were produced, disposing of
every Technical Debt Register item (29 total: 16 tracked, 13 trade-offs
— zero Release Blocking) and every Risk Register entry (8 total: 5
Closed, 2 Mitigated, 1 Remaining by deliberate design) explicitly.

**`WP 7.0A`** established `docs/governance/Future Capability
Register.md` (28 entries), `Capability Categories.md`, and `Product
Roadmap.md` — no new Technical Debt or Risk item, since it identified
future capability, not present-day gaps. **`WP 7.0B`** added
`FCR-0029`–`FCR-0033` to the Future Capability Register (33 entries
total). **`WP 7.0C`** produced no new governance register entries — a
contract review audits proposed decisions, it does not itself decide
them. **`WP 7.1A`** added `ADR-0053` (Accepted, the register's 53rd),
`TD-17` and `TD-18` to `Technical Debt Register.md` (18 tracked items
total), and marked `FCR-0029` **Implemented** — the first entry in the
Future Capability Register to leave "Identified" status. `WP 7.1A` also
found and corrected a small, previously-undisclosed staleness in `ADR
Register.md`'s own "Last Reviewed" field (unchanged since `WP 6.6`
despite `WP 7.0C`'s own edit to that register's Numbering Integrity
narrative in the interim) — a minor, disclosed instance of the same
register-staleness pattern this project has found and corrected
several times before. **`WP 7.1B`** added `ADR-0054` (Accepted, the
register's 54th), `TD-19` and `AT-14` to `Technical Debt Register.md`
(19 tracked items, 14 disclosed trade-offs total), and marked `FCR-0030`
**Implemented** — the second entry in the Future Capability Register to
leave "Identified" status. `WP 7.1B` also added `FCR-0034` (Affine Unit
Conversion / Temperature) — the first Future Capability Register entry
sourced from a real implementation Work Package's own disclosed finding
rather than planning-stage inference, bringing the register to 34
entries total. **`WP 7.1C`** added `ADR-0055` (Accepted, the register's
55th), `TD-20` and `AT-15` to `Technical Debt Register.md` (20 tracked
items, 15 disclosed trade-offs total), and marked `FCR-0031`
**Implemented** — the third entry in the Future Capability Register to
leave "Identified" status. No new `FCR` entry was added this time —
`WP 7.1C`'s own genuine implementation finding (`MaterialCatalog`'s
direct `IPersistenceStore` dependency) was recorded directly in
`ADR-0055` and `TD-20`, not as a new roadmap-facing capability.
**`WP 7.1D`** added `ADR-0056` (Accepted, the register's 56th), `TD-21`,
`TD-22`, and `AT-16` to `Technical Debt Register.md` (22 tracked items,
16 disclosed trade-offs total), and marked `FCR-0032` **Implemented** —
the fourth entry in the Future Capability Register to leave
"Identified" status. `WP 7.1D` also added `FCR-0035` (Calculation
Execution Timeout & Cancellation Support) — sourced directly from this
Work Package's own required Security Review, the first Future
Capability Register entry sourced from a security review rather than an
implementation report's own disclosed finding, bringing the register to
35 entries total. **`WP 7.1E`** added `ADR-0057` (Accepted, the
register's 57th, closing the entire `ADR-0053`–`ADR-0057` reserved
range), `TD-23`, `TD-24`, and `AT-17` to `Technical Debt Register.md`
(24 tracked items, 17 disclosed trade-offs total), and marked `FCR-0033`
**Implemented** — the fifth entry in the Future Capability Register to
leave "Identified" status, **completing the entire Engineering
Foundation programme** (`FCR-0029`–`FCR-0033` all now Implemented).
`WP 7.1E` also added `FCR-0036` (Transactional Multi-Document Operations
for the Engineering Data Model) — sourced directly from this Work
Package's own required Security Review, the second such entry after
`FCR-0035`, bringing the register to 36 entries total.
**`WP 7.1F`** added no new ADR (an audit, not a decision) and no new
Technical Debt or trade-off item (`TD-18` was reassessed with a more
precise disposition, not newly disclosed). Fully backfilled `Interface
Register.md`, `Dependency Injection Register.md`, and `Module
Register.md` — each stale since `WP 6.8`, the exact drift pattern
`FCR-0005` exists to prevent, recurring a second time. `FCR-0005`'s own
priority raised Medium → High as a direct result — three recurrences of
this failure mode across three separate release phases is now a
confirmed pattern, not a single observation. The register remains at 36
entries total — no new capability was identified.
**`WP 7.2A`** added no new ADR, no new Technical Debt or trade-off item,
and no new Future Capability Register entry. Reviewed all 36 existing
entries against seven candidate next-programme options; annotated
`FCR-0027` (Requirements Engine) with this Work Package's own
recommendation (Status: Identified → recommended; Priority: Unknown →
High) — a recommendation, not an approval, per `docs/governance/Future
Capability Register.md`'s own updated entry. `docs/governance/Product
Roadmap.md` updated in place: Phase 4 (Engineering Foundation) marked
Complete, with the divergence between its own original working premise
and what was actually built disclosed explicitly rather than silently
reconciled; Phase 5 (Engineering Modules) sequencing recommended
(Systems Engineering first).
**`WP 7.2B`** added no new ADR (three reserved, not written —
`ADR-0058`–`ADR-0060`), no new Technical Debt Register entry (one new
item identified at architecture level, recommended for formal
registration once implementation begins, not registered by this
Work Package itself), and no new Future Capability Register entry.
`FCR-0027`'s own entry updated in place: Status annotated "architecture
complete, not yet Implemented," `ADR-0013` classification decided at
architecture level (Platform Service). Zero governance registers
required backfilling — this Work Package's own Dependency Analysis
confirmed every integration point it needs already exists, proven,
unmodified.
**`WP 7.2C`** added no new ADR (a fourth reserved, not written —
`ADR-0061`, alongside `ADR-0058`–`ADR-0060` carried forward unanswered),
no new Technical Debt Register entry (the one item identified at
`WP 7.2B`'s own architecture level re-confirmed unchanged at contract
level, still not formally registered), and no new Future Capability
Register entry. `FCR-0027`'s own entry updated in place: Status
annotated "complete public contracts defined, not yet Implemented."
Zero governance registers required backfilling — this Work Package's
own Security Review confirmed no new issue beyond `WP 7.2B`'s own
architecture-level review.
**`WP 7.3A`** added four new ADRs (`ADR-0058`–`ADR-0061`, all Accepted,
57 → 61, closing `WP7.2C Required ADR Catalogue.md`'s own entire
reserved range), one new Technical Debt Register entry (`TD-25`, no
concurrency-conflict detection on `ReviseAsync`/`SetStatusAsync`,
formally registered directly from `ADR-0060`'s own accepted
disposition, 24 → 25), and two new Future Capability Register entries
(`FCR-0037` string-based allocation targets, `FCR-0038` requirement
baselining, 36 → 38). `FCR-0027`'s own entry updated in place: Status
changed to **Implemented**. `Interface Register.md`, `Dependency
Injection Register.md`, and `Module Register.md` were each kept current
directly at implementation time (75 → 80 interfaces, 30 → 31 named
registrations, 19 → 20 modules) — no backfill needed, since this Work
Package recorded its own additions as it made them. `Platform Services
Register.md`'s own Requirements Engine row was updated Planned →
Implemented; a genuine, pre-existing, unrelated drift was found and
disclosed there (not fixed, being outside this Work Package's own
scope): the register and `Platform Service Map.md` had never gained
rows for the four Engineering Foundation frameworks (`WP 7.1A`–`WP
7.1E`), a gap `WP 7.1F`'s own certification review did not check.
**`WP 7.4.0`** added no new ADR, no new Technical Debt or Future
Capability Register entry (release preparation only; zero new platform
functionality). Found and fully backfilled `Governance Register.md`'s
own Compliance Matrix (stale since `WP 6.8`, missing all twelve
`v0.7.0` Work Packages plus `v0.6.0` Release Engineering) and
`Documentation Register.md`'s own Directory Map (four stale counts,
carried forward unchanged since `WP 5.3`/`v0.6.0` Release Engineering).
`FCR-0005` (Governance Register Health-Check Tooling) reconfirmed still
open, its own priority annotation updated to record a fourth and fifth
recurrence found this release. `Platform Services Register.md`'s own
Coverage Status corrected from "Complete" to "Partial," disclosing
rather than hiding the still-open four-framework gap `WP 7.3A` first
found.
**`WP 8.0A`** added four new ADRs (`ADR-0062`–`ADR-0065`, all Accepted,
61 → 65) — the first ADRs of the `v0.8.0` release, each a genuine,
locked-in architectural boundary decision rather than an implementation
detail. `ADR-0066`/`ADR-0067` newly reserved for a future Contract
Review Work Package. No new Technical Debt or Future Capability
Register entry (architecture only; no implementation to disclose a
defect or capability gap from). `Academy Register.md`, `Documentation
Register.md` (directory-map counts for `docs/adr/`,
`docs/academy/02 Runtime Architecture/`, `docs/academy/03 Work Packages/`) kept current directly
at documentation time, not backfilled.
**`WP 8.0B`** added two new ADRs (`ADR-0066`/`ADR-0067`, both Accepted,
65 → 67), resolving both ADRs `WP 8.0A` reserved — zero
reserved-but-unwritten ADR number remains outstanding anywhere in the
register. No new Technical Debt or Future Capability Register entry
(contract review only; no implementation to disclose a defect or
capability gap from). `Academy Register.md`, `Documentation Register.md`
kept current directly at documentation time, not backfilled.
**`WP 8.1A`** added one new ADR (`ADR-0068`, Accepted, 67 → 68) — a
genuinely new decision (`Tempest.App`'s own default launch target), not
a reserved number answered. Zero new Technical Debt item — every scope
limitation is either already-disclosed from `WP 8.0A` or a direct
consequence of "no engineering functionality." No new Future Capability
Register entry. `Academy Register.md`, `Documentation Register.md` kept
current directly at implementation time, not backfilled. Confirmed,
disclosed: `Interface Register.md`/`Dependency Injection Register.md`/
`Module Register.md` remain correctly unchanged — all twelve new public
interfaces and `WorkspaceManager` fall outside each register's own
explicit `Tempest.Core`/`TempestHost.cs`/discovered-module scope,
mirroring `TempestShell`'s own identical, long-standing exclusion.
**`WP 8.0C`** added two new ADRs (`ADR-0069`/`ADR-0070`, both Accepted,
68 → 70) — two genuinely new product/UX decisions (default landing
screen, global command discoverability) surfaced by writing the full
target experience, not reserved numbers answered. No new Technical
Debt item and no new Future Capability Register entry (a product/UX
specification only; no implementation to disclose a defect or
capability gap from) — the one genuine gap this Work Package surfaced
(a plausible future `IDigitalThreadInspector` interface) is disclosed
in the specification itself as *not designed*, deliberately not raised
as a Future Capability entry until a Contract Review actually designs
it. `ADR Register.md`, `Academy Register.md`, `Documentation
Register.md` kept current directly at documentation time, not
backfilled.
**`WP 8.1B`** added one new ADR (`ADR-0071`, Accepted, 70 → 71) —
correcting a worked example inside `ADR-0067` (a discovered module
calling `IWorkspaceManager.RegisterView` directly) that does not hold
against the real Host/Workspace boundary `ADR-0062` already
established; `ADR-0067`'s own core Kind-keyed-registration decision is
unaffected and remains Accepted, unmodified. No new Technical Debt
item, no new Future Capability Register entry. `Module Register.md`
gains one row (`WorkspaceExplorerSampleModule`, 20 → 21 production
modules) — confirmed directly via
`ClockModuleDiscoveryTests.DiscoverModules_ScopedToSampleAssembly_FindsEveryRealSampleModule`,
updated in the same commit. `ADR Register.md`, `Academy Register.md`,
`Documentation Register.md`, `Module Register.md` all kept current
directly at implementation time, not backfilled.
**`WP 8.1C`** added **zero** new ADRs — `ADR-0069`/`ADR-0070` already
made the decisions this Work Package only implements; the ADR Register
remains at 71, unchanged. No new Technical Debt item, no new Future
Capability Register entry, no new Module Register row (the Cockpit is a
composition-root component, not a discovered module — mirroring
`WorkspaceManager`'s own identical, long-standing exclusion). `Academy
Register.md`, `Documentation Register.md` kept current directly at
implementation time, not backfilled.
**`WP 8.2A`** added three new ADRs (`ADR-0072`/`ADR-0073`/`ADR-0074`,
all Accepted, 71 → 74) — each formalising, as binding platform-wide
architecture, a pattern the Engineering Core's own four already-shipped
frameworks (`Requirements`/`Verification`/`Materials`/`Calculations`)
had independently converged on without coordination; no existing ADR
was modified or superseded. No new Technical Debt item (the disclosed
gaps — the Verification Activity/Result split, unenforced approval
gates — are named as architecture-level observations, not defects in
shipped code); no new Future Capability Register entry raised
speculatively — the natural next Work Package (a Physical/
Configuration Engineering Discipline Module) is named directly in this
Work Package's own Recommendations instead. No new Module Register row
(architecture only, no discovered module). `ADR Register.md`, `Academy
Register.md`, `Documentation Register.md` all kept current directly at
documentation time, not backfilled.
**`WP 8.2B`** added two new ADRs (`ADR-0075`/`ADR-0076`, both Accepted,
74 → 76) — both genuinely new contract-shape decisions, not reserved
numbers answered; `ADR-0076` explicitly resolves a tension between this
Work Package's own controlling instruction (seventeen named
relationship categories) and `ADR-0073`'s own prior, binding "never a
closed enum" decision, made one Work Package earlier in the same
release — no existing ADR was modified or superseded. No new Technical
Debt item (every disclosed limitation — `RelationshipCategory`
carrying no structural guarantee against `RelationshipKind`, no
compile-time guarantee for category-specific consumers — is the same
trade-off `ADR-0073` already accepted for `RelationshipKind` alone, now
extended and disclosed, not new); no new Future Capability Register
entry raised speculatively — the natural next Work Package (a real
Physical/Configuration Engineering Discipline Module implementing these
contracts) is named directly in this Work Package's own Recommendations
instead. No new Module Register row (contracts only, no discovered
module). `ADR Register.md`, `Academy Register.md`, `Documentation
Register.md` all kept current directly at documentation time, not
backfilled.
**`WP 8.2C`** added three new ADRs (`ADR-0077`–`ADR-0079`, all
Accepted, 76 → 79) — all three genuinely new implementation-stage
decisions, not reserved numbers answered; `ADR-0077` explicitly
resolves a tension between this Work Package's own "no persistence"
constraint and `ADR-0072`'s own prior, binding decision, the same
shape of tension `ADR-0076` already resolved once at the contract
stage — no existing ADR was modified or superseded. No new Technical
Debt item (every disclosed limitation — the repository not rebuilding
from the real store on restart, the five already-Implemented Kinds
being unconstructable through this framework, `ValidationRuleSet`
enforcing zero rules by design — is an ADR consequence or a named
Future Evolution item, not a defect in shipped behaviour); no new
Future Capability Register entry raised speculatively — the natural
next Work Package (a real Physical/Configuration Engineering
Discipline Module, now buildable directly against compiled concrete
classes) is named directly in this Work Package's own Future Evolution
instead. One new Module Register row (`EngineeringDomainSampleModule`,
21 → 22 production modules) — confirmed directly via
`ClockModuleDiscoveryTests.DiscoverModules_ScopedToSampleAssembly_FindsEveryRealSampleModule`,
updated in the same commit. `Interface Register.md` gains its largest
single addition to date (83 new interfaces, one dedicated subsection);
`Dependency Injection Register.md` gains ten new named registrations.
`ADR Register.md`, `Academy Register.md`, `Documentation Register.md`,
`Module Register.md`, `Interface Register.md`, `Dependency Injection
Register.md` all kept current directly at implementation time, not
backfilled.
**`WP 8.9.0`** added **zero** new ADRs — a release-verification-only
Work Package, consistent with `WP 8.0A`/`WP 8.0C`/`WP 8.2A`'s own
identical pattern for architecture/product-only Work Packages; the ADR
Register remains at 79, unchanged, re-verified directly. No new
Technical Debt item — the one genuine process gap this Work Package
found (zero dedicated Security Reviews this release) is disclosed as a
standing recommendation, not converted into a Technical Debt Register
entry, since it describes a *process* gap for the next Work Package to
close, not a defect in any shipped component. No new Future Capability
Register entry. `Academy Register.md`, `Documentation Register.md` kept
current directly at documentation time, not backfilled. One genuine,
disclosed correction made across every living document: `WP 8.2C`'s own
"39 concrete classes" claim, corrected to the verified 38 — left
unedited in `WP 8.2C`'s own dated historical artifacts, per this Work
Package's own "never silently modify historical records" instruction.

## Known Unknowns

Recorded honestly, not guessed at — full detail in `docs/governance/
Governance Audit Report.md`:

1. `docs/releases/v0.2.0.md` (renamed `WP 12.9.1` from a misnamed stray
   file, `docs/releases/v0.2.0`, no extension, no folder) — a
   never-completed release-notes skeleton, every field blank; whether
   v0.2.0 was ever released, skipped, or reserved is unknown.
2. `docs/roadmap/`, `docs/diagrams/` (each gains a tracked marker
   `README.md`, `WP 12.9.1`, disclosing this in place — see
   `Documentation Register.md`) — intended purpose unknown; unreferenced
   by any document reviewed.
3. Exact original authorship of four pre-Claude namespaces
   (`Tempest.Core.Hosting`, `Bootstrap`, `Projects`, `Repositories`) and
   seven unnamespaced bootstrap-era files.
4. A five-day gap in earliest git history (2026-07-15 to 2026-07-21).
5. v0.1.0's full scope beyond its own commit message.
6. Intermediate historical test-count totals for `WP 4.1` and `WP 4.3`
   (each retrospective states only the tests it added, not a running
   total).

## Current Priorities

1. **`v0.6.0` is released.** Merged into `main` (non-fast-forward,
   `99ed285`), tagged `v0.6.0`, both pushed to `origin` — Product
   Approval was granted and Release Engineering executed in full. See
   `docs/releases/v0.6.0/WP6.8 Platform Certification Report.md` for
   the certification decision and evidence this release shipped under.
2. **`WP 7.0A`, `WP 7.0B`, and `WP 7.0C` are all complete — Engineering
   Review APPROVED all three.** `VISION.md`, `Future Capability
   Register.md` (33 entries), `Capability Categories.md`, `Product
   Roadmap.md`, a full dependency graph, six engineering programmes,
   proposed public C# contracts for all five Engineering Foundation
   frameworks, and `ADR-0053`–`ADR-0057` catalogued are all established
   and approved.
3. **`WP 7.1A` through `WP 7.1E` — all five Engineering Foundation
   frameworks — are complete and Engineering-Review-approved.**
   `Tempest.Core.EngineeringData` (`ADR-0053`),
   `Tempest.Core.UnitsAndQuantities` (`ADR-0054`),
   `Tempest.Core.Materials` (`ADR-0055`), `Tempest.Core.Calculations`
   (`ADR-0056`), and `Tempest.Core.Verification` (`ADR-0057`) are all
   implemented; 1275/1275 tests passing, both configurations, clean
   rebuild.
4. **`WP 7.1F` — Engineering Core Integration Review & Certification —
   is complete, Engineering Review APPROVED.** The Engineering Core is
   **CERTIFIED WITH ACCEPTED TECHNICAL DEBT** — see
   `ENGINEERING_CORE_COMPLETION_REPORT.md` and `docs/releases/v0.7.0/
   WP7.1F Engineering Core Certification Report.md`. This closed the
   entire Engineering Foundation implementation programme.
5. **`WP 7.2A` — Strategic Roadmap Selection & Programme Architecture —
   is complete, Engineering Review APPROVED.** Recommended Programme A
   (Requirements & Verification Platform, `FCR-0027`), scoring 46/55 —
   see `docs/releases/v0.7.0/WP7.2A Recommended Programme.md`.
   **Engineering Review and Product Approval accepted this
   recommendation**, authorising `WP 7.2B`.
6. **`WP 7.2B` — Requirements & Verification Platform Architecture —
   is complete, Engineering Review APPROVED.** Designed the complete
   architecture for the Requirements & Verification Platform — twelve
   domain concepts, a three-layer Engineering Core/Systems Engineering
   Foundation/Engineering Discipline Modules model, a digital thread
   design, an eleven-service dependency analysis (zero new platform
   capability required), a security classification (one new Technical
   Debt item found), and an industry-neutral standards mapping. Reserved
   `ADR-0058`–`ADR-0060` — see `docs/releases/v0.7.0/WP7.2B Requirements
   Platform Architecture.md`. **Engineering Review accepted this
   architecture**, authorising `WP 7.2C`.
7. **`WP 7.2C` — Requirements & Verification Platform Contract Review —
   is complete, Engineering Review APPROVED.** Defined full proposed C#
   contracts for all thirteen named domain concepts, a seven-state
   Requirement Lifecycle Model, a Relationship Model (six of seven
   relationship kinds confirmed for the initial implementation), a
   Traceability Contract (disclosing one genuine reverse-allocation-
   traceability limitation), and a Verification Integration Contract
   re-confirming `ADR-0057`'s own circular-dependency avoidance holds
   unmodified. Reserved `ADR-0061`, alongside `ADR-0058`–`ADR-0060`
   carried forward unanswered — see `docs/releases/v0.7.0/WP7.2C
   Requirements Platform Contracts.md`. **Engineering Review accepted
   these contracts**, authorising `WP 7.3A`.
8. **`WP 7.3A` — Requirements Engine — is complete, Engineering Review
   APPROVED.** Implemented `Tempest.Core.Requirements` exactly as
   `WP 7.2C` approved, zero architectural deviation. Ratified all four
   reserved ADRs (`ADR-0058`–`ADR-0061`); disclosed new Technical Debt
   (`TD-25`) and two new Future Capability candidates (`FCR-0037`,
   `FCR-0038`). `FCR-0027` (Requirements Engine) is now **Implemented**
   — see `docs/releases/v0.7.0/WP7.3A Implementation Report.md`. This
   Work Package's own explicit closing instruction ("Do not begin
   `WP 7.3B`. Stop after WP 7.3A has been fully implemented and
   reviewed.") is honoured — no further Work Package begins until
   Product Approval authorises what comes next.
9. **`WP 7.4.0` — Release Preparation & Product Baseline — is complete.**
   A release-preparation review only, no new platform functionality:
   5/5 projects build clean (0 warnings, 0 errors) in both
   configurations; 1406/1406 tests passing across four full-suite runs;
   `VERSION` confirmed correctly unchanged (`0.6.0`, bumped only after
   the Product Owner's own tag, per established precedent). Five
   governance/documentation staleness findings corrected
   (`Documentation Register.md`, `Governance Register.md`'s Compliance
   Matrix); one further finding disclosed, not fixed (`Platform Services
   Register.md`/`Platform Service Map.md`'s own missing four-framework
   rows). **Recommendation: `v0.7.0` APPROVED** — see
   `docs/releases/v0.7.0/WP7.4.0 Product Approval Report.md`.
10. **`v0.7.0` is released.** The Product Owner accepted `WP 7.4.0`'s own
    recommendation, merged `feature/v0.7.0-engineering-foundation` into
    `main` (non-fast-forward, `61fb2db`), tagged `v0.7.0`, and pushed
    both to `origin`. `feature/v0.8.0-engineering-workspace` was then cut
    from `main` at the `v0.7.0` tag, `VERSION` bumped to `0.7.0`,
    mirroring `v0.6.0`'s own identical precedent.
11. **`WP 8.0A` — Engineering Workspace Architecture — is complete.**
    The complete architecture for TempestOS's first user-facing
    engineering product surface, across all twelve named areas — see
    `docs/releases/v0.8.0/WP8.0A Workspace Architecture Document.md`.
    Four new ADRs (`ADR-0062`–`ADR-0065`); `ADR-0066`/`ADR-0067`
    reserved for a Contract Review Work Package. **No implementation
    was performed — zero code written.**
12. **`WP 8.0B` — Workspace Contracts — is complete.** The complete
    public contract for all twelve named Workspace interfaces — see
    `docs/releases/v0.8.0/WP8.0B Workspace Contracts.md`. Both reserved
    ADRs resolved (`ADR-0066` terminal-based presentation, `ADR-0067`
    Kind-keyed extensibility registration). **No implementation was
    performed — zero code compiled.**
13. **`WP 8.1A` — Workspace Shell — is complete.** All twelve contracts
    compiled and running in `Tempest.App.Workspace`, zero signature
    change — see Current Work Package, above, and
    `docs/releases/v0.8.0/WP8.1A Implementation Report.md`. The
    Workspace is now `Tempest.App`'s own default launch target
    (`ADR-0068`); console `TempestShell` remains, untouched, no longer
    default. 91 new tests (1406 → 1497). **No engineering functionality
    — shell only, per this Work Package's own explicit constraint.**
14. **`WP 8.0C` — Engineering Workspace UX Specification — is complete.**
    The complete target user experience for the Engineering Workspace,
    across all 28 named scope areas — nine deliverables under
    `docs/releases/v0.8.0/`, see Current Work Package, above, and
    `docs/releases/v0.8.0/WP8.0C UX Specification.md`. Two new ADRs
    (`ADR-0069` Engineering Cockpit as default landing screen,
    `ADR-0070` Command Palette as global entry point). A genuine,
    disclosed sequencing finding: this specification runs after, not
    before, `WP 8.1A`'s own shell implementation — every companion
    document discloses "Today vs. Target" accordingly. **No
    implementation was performed — zero `src/`/`tests/` change of any
    kind.**
15. **`WP 8.1B` — Navigation & Project Explorer — is complete.** The
    Workspace Navigation system and Project Explorer, implemented exactly
    as specified across `WP 8.0A`/`WP 8.0B`/`WP 8.0C` — see Current Work
    Package, above, and `docs/releases/v0.8.0/WP8.1B Implementation
    Report.md`. Navigation history, recent items, breadcrumbs, and
    filtering added as same-assembly-only extensions to the concrete
    `NavigationService`/`ProjectExplorer` classes, never to the twelve
    public interfaces. The Project Explorer is now populated and
    navigable, proven against the first real `IProjectExplorerNodeProvider`/
    `IWorkspaceViewFactory` pair (fictional sample content only). One new
    ADR (`ADR-0071`), correcting a worked example inside `ADR-0067` that
    a discovered module cannot actually reach `IWorkspaceManager`
    directly. 55 new tests (1497 → 1552). **Zero new Technical Debt.**
16. **`WP 8.1C` — Engineering Cockpit — is complete.** The Engineering
    Cockpit, implemented as the Workspace's own default landing screen
    (`ADR-0069`) — see Current Work Package, above, and
    `docs/releases/v0.8.0/WP8.1C Implementation Report.md`. Consumes
    only existing Workspace services (`NavigationService`, and the
    newly-resolved `ICommandRegistry` for Command Palette integration,
    `ADR-0070`); every card with no real backing service shows fixed,
    disclosed placeholder content. Controlling instruction expanded
    mid-Work-Package to name many more cards than `WP 8.0C`'s own seven
    regions — followed in full, disclosed as a product-scope expansion.
    **Zero new ADRs.** 40 new tests (1552 → 1592). **Zero new Technical
    Debt.**
17. **`WP 8.2A` — Engineering Domain Architecture — is complete.** The
    complete canonical Engineering Domain Architecture — ~49 named
    Engineering Objects across thirteen families, twenty relationship
    kinds, a canonical lifecycle vocabulary, configuration management,
    the full Digital Thread, common metadata, and validation rules — see
    `WP 8.2A` Summary, above, and `docs/releases/v0.8.0/WP8.2A
    Engineering Domain Architecture.md` and its eight companions. Every
    fact grounded in, and reconciled against, the four already-shipped
    Engineering Core frameworks — five catalogue entries `Implemented`,
    forty-plus honestly marked `Conceptual`. Three new ADRs
    (`ADR-0072`–`ADR-0074`), each formalising an existing convergence as
    binding platform architecture, not inventing a new one. **Zero
    `src/`/`tests/` change of any kind.**
18. **`WP 8.2B` — Engineering Domain Contracts — is complete.** The
    complete public contract for the Engineering Domain Architecture
    `WP 8.2A` established — see Current Work Package, above, and
    `docs/releases/v0.8.0/WP8.2B Engineering Domain Contracts.md` and its
    seven companions. `IEngineeringObject` plus ten facet interfaces,
    composed not inherited (`ADR-0075`); all ~49 canonical object
    interfaces built from them; one generic `IEngineeringRelationship`
    interface realising all seventeen named relationship categories as
    descriptive metadata, without reopening `ADR-0073`'s own closed-enum
    prohibition (`ADR-0076`). Two new ADRs (`ADR-0075`–`ADR-0076`), one
    explicitly resolving a tension against a prior, binding decision.
    **Zero `src/`/`tests/` change of any kind.**
19. **`WP 8.2C` — Engineering Domain Implementation — is complete.**
    Compiles every `WP 8.2B` frozen contract exactly, and gives 38 of
    the ~49 canonical objects a real, tested concrete class (corrected
    39 → 38 by `WP 8.9.0`) over a new shared `EngineeringObjectBase` and
    two generic factory types — see `WP 8.2C` Summary, above, and
    `docs/releases/v0.8.0/WP8.2C Engineering Domain Implementation
    Report.md`. Every canonical object's own real storage reuses the
    existing, shared `IEngineeringDocumentStore` in production; a new,
    purely in-memory repository layer is the "in-memory repositories"
    deliverable. The five already-Implemented canonical Kinds are
    deliberately not given a competing concrete realisation. Three new
    ADRs (`ADR-0077`–`ADR-0079`). New sample module
    (`EngineeringDomainSampleModule`, the platform's twenty-second). 39
    new tests (1592 → 1631). **Zero new Technical Debt.**
20. **`WP 8.9.0` — Release Preparation & Product Baseline — is
    complete.** A complete release readiness review across repository,
    build, test, version, governance, and architecture verification for
    all nine `v0.8.0` Work Packages — see Current Work Package, above,
    and `docs/releases/v0.8.0/WP8.9.0 Release Readiness Report.md` and
    its eight companion deliverables. 1631/1631 tests passing across
    five full-suite-equivalent runs, zero flakes; 4/4 projects build
    clean, both configurations, verified both per-project and via
    `src/TempestOS.slnx`. One genuine arithmetic correction made (39→38
    concrete classes); two further findings disclosed, not fixed (the
    four-framework Platform Service gap, now open across two
    consecutive release cycles; `WP8.2B`'s own `IRelease`
    inheritance-depth inconsistency); one process gap disclosed and
    weighed explicitly (zero dedicated Security Reviews this release).
    **Recommendation: `v0.8.0` APPROVED.** Zero new ADRs.
21. **Await Product Owner instruction for what comes next.** `v0.8.0`
    is prepared and recommended for release — the Product Owner's own
    merge/`VERSION` bump/tag/GitHub Release/push sequence is prepared,
    not executed (`WP8.9.0 Product Owner Release Checklist.md`).
    Separately, `WP 8.2A`, `WP 8.2B`, and `WP 8.2C` all recommend a real
    Physical/Configuration Engineering Discipline Module, now buildable
    directly against the already-compiled
    `IAssembly`/`ISubAssembly`/`IPart`/`IComponent` concrete classes, as
    Programme 9's own most concrete candidate; `WP8.9.0`'s own
    Retrospective additionally recommends a dedicated Security Review
    precede or accompany whatever implementation Work Package comes
    next. Per this project's own standing discipline (`FOUNDATION.md`
    §1) and `WP 8.9.0`'s own explicit closing instruction, no Programme
    9 Work Package begins until the Product Owner gives further
    instruction.
22. A GitHub Release for `v0.6.0` (and now `v0.7.0`) has not yet been
    created (`gh` CLI unavailable in this environment) — see the Release
    Summary for the exact command or manual steps to complete it. The
    same limitation applies to `v0.8.0`'s own eventual GitHub Release —
    prepared as a manual web-UI alternative in `WP8.9.0 Product Owner
    Release Checklist.md` Step 6.

## Near-Term Roadmap

Per `docs/releases/v0.5.0/WorkPackages.md`, the Developer Experience
phase is complete and released as `v0.5.0`, `WP 5.0A` through `WP 5.4`:

- `WP 5.0A` — Navigation Framework Architecture (design only). **Complete.**
- `WP 5.0B` — Navigation Framework Implementation. **Complete.**
- `WP 5.0C` — Shell & Composition Framework Architecture (design only). **Complete.**
- `WP 5.0D` — Shell & Composition Framework Implementation. **Complete.**
- `WP 5.0S` — Platform Security Baseline Audit (dedicated engineering
  audit, not a feature Work Package). **Complete.**
- `WP 5.1A` — Command Framework Architecture (design only). **Complete.**
- `WP 5.1B` — Command Framework Implementation. **Complete.**
- `WP 5.2` — Diagnostics Improvements (composite logging, `TD-01`
  reassessment, `IDiagnosticsProvider`). **Complete.**
- `WP 5.3` — Developer Experience Improvements (module template,
  clearer Discovery message). **Complete.**
- `WP 5.4` — v0.5.0 Release Candidate & Engineering Sign-Off (verification,
  not a feature Work Package). **Complete.**

Per `docs/releases/v0.6.0/WorkPackages.md`, the Platform Services phase
is under way:

- `WP 6.0` — Reporting Framework. **Complete.**
- `WP 6.1` — Permissions & Identity. **Complete.**
- `WP 6.2` — Notification Framework. **Complete.**
- `WP 6.3` — REST API. **Complete.**
- `WP 6.4` — Settings Framework. **Complete.**
- `WP 6.5` — Audit Framework. **Complete.**
- `WP 6.6` — Licensing Framework. **Complete.**
- `WP 6.7` — Export / Import. **Complete.**
- `WP 6.8` — Platform Services Integration Review & Release
  Certification (closing milestone audit, mirroring `WP 4.2D`/
  `WP 5.0S`'s own precedent). **Complete — CERTIFIED WITH ACCEPTED
  TECHNICAL DEBT.**

**The Platform Services phase is complete and released.** `v0.6.0` is
merged to `main`, tagged, and pushed. TempestOS is now in the
**Engineering Foundation** phase (`v0.7.0`). `WP 7.0A` through `WP 7.3A`
are all complete — the entire Engineering Foundation implementation
programme is certified, Programme A (Requirements & Verification
Platform) has been recommended and accepted, its complete architecture
and public contracts were defined, and the Requirements Engine itself
(`FCR-0027`) is now **Implemented** — see `docs/releases/v0.7.0/WP7.3A
Implementation Report.md` and `docs/governance/Future Capability
Register.md` for the current state pending Product Approval on what
comes next.

- `WP 7.0A` — Future Capability Register & Product Vision (architecture
  and governance only; no production code). **Complete — Engineering
  Review APPROVED.**
- `WP 7.0B` — Engineering Foundation Planning & Capability Architecture
  (architecture and planning only; no production code). **Complete —
  Engineering Review APPROVED.**
- `WP 7.0C` — Engineering Foundation Contract Review (contract review
  only; no production code, no compiled interface). **Complete —
  Engineering Review APPROVED.**
- `WP 7.1A` — Engineering Data Model (first implementation Work Package
  of this phase; `Tempest.Core.EngineeringData`, `ADR-0053`). **Complete
  — Engineering Review APPROVED.**
- `WP 7.1B` — Units & Quantities Framework (second implementation Work
  Package of this phase; `Tempest.Core.UnitsAndQuantities`, `ADR-0054`).
  **Complete — Engineering Review APPROVED.**
- `WP 7.1C` — Materials Framework (third implementation Work Package of
  this phase; `Tempest.Core.Materials`, `ADR-0055`). **Complete —
  Engineering Review APPROVED.**
- `WP 7.1D` — Engineering Calculation Framework (fourth implementation
  Work Package of this phase, and the first to include a dedicated
  Security Review; `Tempest.Core.Calculations`, `ADR-0056`). **Complete
  — Engineering Review APPROVED.**
- `WP 7.1E` — Verification Framework (fifth and final Engineering
  Foundation implementation Work Package, and the second to include a
  dedicated Security Review; `Tempest.Core.Verification`, `ADR-0057`).
  **Complete — Engineering Review APPROVED.** Closed the Engineering
  Foundation implementation programme — see `docs/governance/Future
  Capability Register.md` (`FCR-0029` through `FCR-0033`, all now
  Implemented).
- `WP 7.1F` — Engineering Core Integration Review & Certification
  (closing certification review of the complete Engineering Core,
  mirroring `WP 6.8`'s own role for `v0.6.0`; no production code).
  **Complete — Engineering Review APPROVED.** **ENGINEERING CORE
  CERTIFIED WITH ACCEPTED TECHNICAL DEBT** — see
  `ENGINEERING_CORE_COMPLETION_REPORT.md` and `docs/releases/v0.7.0/
  WP7.1F Engineering Core Certification Report.md`. Found and closed a
  repeat of `WP 6.8`'s own governance-register-drift finding and a
  missing Academy concept guide four Work Packages overdue.
- `WP 7.2A` — Strategic Roadmap Selection & Programme Architecture
  (architecture, governance, and roadmap planning only; no production
  code). **Complete — Engineering Review APPROVED.** Recommended
  Programme A (Requirements & Verification Platform, `FCR-0027`) as
  `v0.8.0`'s own scope, scoring 46/55 against seven candidates — see
  `docs/releases/v0.7.0/WP7.2A Recommended Programme.md`. Programme F
  (Platform Hardening) recommended second, at `v0.9.0`. **Engineering
  Review and Product Approval accepted this recommendation.**
- `WP 7.2B` — Requirements & Verification Platform Architecture
  (architecture and planning only; no production code). **Complete —
  Engineering Review APPROVED.** Designed the complete architecture for
  the Requirements & Verification Platform — twelve domain concepts, a
  three-layer Engineering Core/Systems Engineering Foundation/Engineering
  Discipline Modules model, a digital thread design, and an
  eleven-service dependency analysis finding zero new platform capability
  required — see `docs/releases/v0.7.0/WP7.2B Requirements Platform
  Architecture.md`. Reserved `ADR-0058`–`ADR-0060`.
- `WP 7.2C` — Requirements & Verification Platform Contract Review
  (contract review only; no production code, no compiled interface).
  **Complete — Engineering Review APPROVED.** Defined full proposed C#
  contracts for all thirteen named domain concepts, a Requirement
  Lifecycle Model, a Relationship Model, a Traceability Contract, and a
  Verification Integration Contract re-confirming `ADR-0057`'s own
  circular-dependency avoidance holds unmodified — see
  `docs/releases/v0.7.0/WP7.2C Requirements Platform Contracts.md`.
  Reserved `ADR-0061`.
- `WP 7.3A` — Requirements Engine (first implementation Work Package of
  the Systems Engineering Foundation phase; `Tempest.Core.Requirements`,
  `ADR-0058`–`ADR-0061`). **Complete — Engineering Review APPROVED.**
  Implemented exactly as `WP 7.2C` approved, zero architectural
  deviation; 20 new production files, 131 new tests (1275 → 1406),
  new Academy concept guide (`16-requirements-engine.md`). Ratified all
  four reserved ADRs; disclosed `TD-25` and two new Future Capability
  candidates (`FCR-0037`, `FCR-0038`) — see `docs/releases/v0.7.0/WP7.3A
  Implementation Report.md`. `FCR-0027` is now **Implemented**. Does not
  begin `WP 7.3B`, per this Work Package's own explicit closing
  instruction.
- `WP 7.4.0` — Release Preparation & Product Baseline (release
  preparation only; no production code, no architectural change).
  **Complete.** Full release readiness review across seventeen named
  areas: clean build (5/5 projects, 0 warnings/errors, both
  configurations), 1406/1406 tests (four full-suite runs), `VERSION`
  confirmed correct, twelve Work Packages' governance traceability
  backfilled. Five documentation/governance staleness findings
  corrected; one further finding disclosed, not fixed (outside scope) —
  see `docs/releases/v0.7.0/WP7.4.0 Release Readiness Report.md`.
  **Recommendation: `v0.7.0` APPROVED** — see `docs/releases/v0.7.0/
  WP7.4.0 Product Approval Report.md`.

**`v0.7.0` is released.** All thirteen Work Packages plus its own
closing release-preparation review are complete. The Product Owner
merged `feature/v0.7.0-engineering-foundation` into `main`
(non-fast-forward, `61fb2db`), tagged `v0.7.0`, and pushed both to
`origin`.

Per `docs/releases/v0.8.0/WorkPackages.md`, the Engineering Workspace
phase is under way, on `feature/v0.8.0-engineering-workspace` (cut from
`main` at the `v0.7.0` tag):

- `WP 8.0A` — Engineering Workspace Architecture (architecture and
  design only; no implementation, no production code). **Complete.**
  Designed the complete Engineering Workspace across all twelve named
  areas — TempestOS's first user-facing engineering product surface,
  a multi-panel evolution of `Tempest.App`'s own composition
  root, additive to console `TempestShell` (`ADR-0062`). Views read
  Engineering Core/Platform services directly; mutations dispatch
  through the existing Command Framework (`ADR-0063`); layout/session
  state persists via the existing `ISettingsProvider` (`ADR-0064`);
  Digital Thread visualisation composes existing reads, introducing no
  new traversal mechanism (`ADR-0065`) — see `docs/releases/v0.8.0/
  WP8.0A Workspace Architecture Document.md` and its four companion
  deliverables. `ADR-0066` (UI rendering technology) and `ADR-0067`
  (object-view extensibility contract) reserved for a Contract Review
  Work Package.
- `WP 8.0B` — Workspace Contracts (contract review only; no
  implementation, no compiled interface). **Complete.** Defined the
  complete public contract for all twelve named interfaces
  (`IWorkspace`, `IWorkspaceManager`, `IWorkspaceView`,
  `IWorkspacePanel`, `IWorkspaceLayout`, `INavigationService`,
  `ISelectionService`, `IWorkspaceContext`, `IWorkspaceState`,
  `IProjectExplorer`, `IPropertyInspector`, `IWorkspaceCommand`) — see
  `docs/releases/v0.8.0/WP8.0B Workspace Contracts.md` and its three
  companion deliverables. Resolved both reserved ADRs: `ADR-0066`
  (terminal-based presentation, not a graphical desktop framework —
  this platform's first-ever GUI dependency deliberately not taken on)
  and `ADR-0067` (Kind-keyed registration for both object views and
  Project Explorer nodes, mirroring `IReportDefinition`/
  `IReportRenderer<T>`'s own established pattern).
- `WP 8.1A` — Workspace Shell (implementation; shell only, no
  engineering functionality). **Complete.** All twelve contracts
  compiled and running in a new `Tempest.App.Workspace` namespace, zero
  signature change — see `docs/releases/v0.8.0/WP8.1A Implementation
  Report.md`. `WorkspaceManager`/`WorkspaceShell` are now `Tempest.App`'s
  own default launch target (`ADR-0068`); console `TempestShell`
  remains, untouched, no longer default. 27 new production files, 91
  new tests (1406 → 1497). Two disclosed implementation-phase findings
  (`ISettingsProvider`'s own `string`-only contract;
  `ITempestHost`'s own single-use constraint), neither requiring an
  architectural revisit. Zero new Technical Debt.
- `WP 8.0C` — Engineering Workspace UX Specification (product/UX design
  only; no implementation, no code). **Complete.** Fully specified the
  target Workspace experience across all 28 named scope areas — nine
  deliverables (`UX Specification.md`, `Screen Catalogue.md`, `User
  Journey Maps.md`, `Interaction Specification.md`, `Navigation
  Maps.md`, `Wireframe Sketches.md`, `Workspace Behaviour
  Specification.md`, `Engineering Cockpit Specification.md`) — see
  `docs/releases/v0.8.0/WP8.0C UX Specification.md` and its eight
  companions. Two new ADRs: `ADR-0069` (Engineering Cockpit is the
  Workspace's own default landing screen) and `ADR-0070` (Command
  Palette is a first-class, global entry point). A disclosed sequencing
  finding: this specification runs after, not before, `WP 8.1A`'s own
  shell implementation, and a Rendering Feasibility Disclosure names,
  without resolving, a tension between rich UX ambitions
  (multi-window, multi-monitor, a floating Command Palette overlay) and
  `ADR-0066`'s terminal-based decision.
- `WP 8.1B` — Navigation & Project Explorer (implementation; Navigation
  system and Project Explorer against representative sample content
  only, no Requirements/Calculations/Documents). **Complete.** Navigation
  history, recent items, breadcrumbs, and filtering — genuine
  capabilities `WP 8.0B` never anticipated — added as same-assembly-only
  extensions to the concrete `NavigationService`/`ProjectExplorer`
  classes, never to the twelve public interfaces — see
  `docs/releases/v0.8.0/WP8.1B Implementation Report.md`. The Project
  Explorer is populated and navigable for the first time, proven against
  a real, fixed, fictional Category → Group → Object tree
  (`Tempest.App.Workspace.Samples`). One new ADR (`ADR-0071`), correcting
  a worked example inside `ADR-0067` that a discovered module cannot
  actually reach `IWorkspaceManager` directly — registration now happens
  in `Program.cs`. 7 new production files, 5 modified, 55 new tests
  (1497 → 1552). Zero new Technical Debt.
- `WP 8.1C` — Engineering Cockpit (implementation; the Workspace's own
  default landing screen, consuming existing Workspace services only,
  no Requirements/Calculations/Verification/Digital-Thread-traversal
  logic). **Complete.** Implements `ADR-0069` (default landing screen)
  and a Cockpit-scoped realisation of `ADR-0070` (Command Palette
  integration via the existing `ICommandRegistry`) — see
  `docs/releases/v0.8.0/WP8.1C Implementation Report.md`. Controlling
  instruction expanded mid-Work-Package to name many more cards than
  `WP8.0C Engineering Cockpit Specification.md`'s own seven regions
  (Continue Where I Left Off, Recent/Favourite Projects, a
  five-discipline Project Health Dashboard, Risk Summary, Open
  Decisions, Blocked Items, Overdue Actions, Quick Actions) — followed
  in full, disclosed as a product-scope expansion, not an architectural
  one. Every card with a real Workspace-service backing is a live read;
  every other card is fixed, disclosed placeholder content. **Zero new
  ADRs** — the first implementation Work Package this release to need
  none. 5 new production files, 3 modified, 40 new tests (1552 → 1592).
  Zero new Technical Debt.
- `WP 8.2A` — Engineering Domain Architecture (architecture only; no
  implementation, no persistence, no interfaces, no UI). **Complete.**
  Defines the complete canonical Engineering Domain Architecture — ~49
  named Engineering Objects across thirteen families, twenty
  relationship kinds, a canonical lifecycle vocabulary, configuration
  management, the full Digital Thread, common metadata, and validation
  rules — see `docs/releases/v0.8.0/WP8.2A Engineering Domain
  Architecture.md` and its eight companions. Formalises, as three new
  ADRs (`ADR-0072`–`ADR-0074`), a pattern the Engineering Core's own
  four already-shipped frameworks (`Requirements`/`Verification`/
  `Materials`/`Calculations`) had independently converged on:
  `Kind`-backed identity over the shared `IEngineeringDocumentStore`,
  open-string relationships, and a closed, caller-driven lifecycle
  table. Five catalogue entries reconciled as already-`Implemented`;
  forty-plus honestly marked `Conceptual`. `RequirementStatus` remains
  exactly as shipped, reconciled, not redesigned. Zero `src/`/`tests/`
  change of any kind.
- `WP 8.2B` — Engineering Domain Contracts (contract review only; no
  implementation, no concrete classes, no repositories, no persistence,
  no serialization, no storage technology, no UI). **Complete.** Converts
  `WP 8.2A`'s canonical Engineering Domain Architecture into the complete
  public contract every current and future TempestOS module implements
  against — see `docs/releases/v0.8.0/WP8.2B Engineering Domain
  Contracts.md` and its seven companions. `IEngineeringObject` plus ten
  facet interfaces, composed not inherited; all ~49 canonical object
  interfaces built from them across thirteen families; one generic
  `IEngineeringRelationship` interface realising all seventeen named
  relationship categories as descriptive metadata only, never a closed
  set of per-category types. Two new ADRs (`ADR-0075`, `ADR-0076`),
  `ADR-0076` explicitly resolving a tension between this Work Package's
  own controlling instruction and `ADR-0073`'s own prior, binding
  decision. Zero `src/`/`tests/` change of any kind.
- `WP 8.2C` — Engineering Domain Implementation (implementation; no
  discipline logic — Requirements/Verification/Calculations/
  Manufacturing — written anywhere inside it). **Complete.** Compiles
  every `WP 8.2B` frozen contract exactly — see `docs/releases/v0.8.0/
  WP8.2C Engineering Domain Implementation Report.md`. A shared
  `EngineeringObjectBase` class implements every facet unconditionally;
  38 of the ~49 canonical objects get a real, tested concrete class
  (corrected 39 → 38 by `WP 8.9.0`, see Repository Metrics below),
  constructed through one of two generic factory types
  (`EngineeringObjectFactory<T>`, `EngineeringRelationshipFactory`).
  The five already-Implemented canonical Kinds are deliberately not
  given a competing concrete realisation — their ownership stays
  exactly where `WP 8.2A` already placed it. Every canonical object's
  own real storage reuses the existing, shared `IEngineeringDocumentStore`
  in production, introducing zero new persistence; a new, purely
  in-memory `IEngineeringObjectRepository`/`IEngineeringRelationshipRepository`
  pair is the genuinely new "in-memory repositories" deliverable. Three
  new ADRs (`ADR-0077`–`ADR-0079`). New sample module
  (`EngineeringDomainSampleModule`, the platform's twenty-second,
  building a sixteen-object representative graph including a real
  cross-framework Material reference). 39 new tests (1592 → 1631), both
  Debug and Release, clean rebuild.
- `WP 8.9.0` — Release Preparation & Product Baseline (release
  verification only; no new functionality, no architecture change, no
  refactoring). **Complete.** Complete release readiness review across
  repository/build/test/version/governance/architecture verification
  for all nine `v0.8.0` Work Packages — see `docs/releases/v0.8.0/
  WP8.9.0 Release Readiness Report.md` and eight companions.
  1631/1631 tests, five full-suite-equivalent runs, zero flakes; 4/4
  projects build clean both configurations, verified via both
  per-project builds and `src/TempestOS.slnx`. One arithmetic
  correction (39 → 38 concrete classes); two disclosed, unfixed
  findings (the four-framework Platform Service gap, now open across
  two release cycles; `WP8.2B`'s own `IRelease` inheritance-depth
  inconsistency); one disclosed process gap (zero dedicated Security
  Reviews this release). **Recommendation: `v0.8.0` APPROVED.** Zero
  new ADRs.

**`v0.8.0` is complete and recommended APPROVED, awaiting the Product
Owner's own release action.** Its architecture, contract, and
implementation stages for both the Workspace and the Engineering Domain
are all complete — a real, running Workspace shell with a navigable
Project Explorer and a live Engineering Cockpit landing screen exists,
and the platform's own canonical engineering vocabulary is now frozen
and, for 38 of its ~49 objects, real, tested, running code — and
`WP 8.9.0`'s own independent release-readiness review found zero
release-blocking defects. The Project Dashboard, the Properties/Inspector
split, the Command Palette's own screen-independent realisation, and a
real Physical/Configuration Engineering Discipline Module built against
the now-compiled Engineering Domain classes all remain unscheduled for
whatever Work Package begins Programme 9.

**Disclosed staleness, found and fixed by `WP 9.4A`:** this section
carried no `v0.9.0` roadmap bullet list at all — it jumped directly from
`v0.8.0`'s own closing paragraph to Long-Term Vision, silently
un-updated across all four prior `v0.9.0` Work Packages
(`WP 9.0A`/`WP 9.0B`/`WP 9.1A`/`WP 9.2A`), none of which added one.
Backfilled here, not silently — every entry below reflects this
document's own Current Work Package section and each Work Package's own
committed Implementation Report; no historical record is altered by
this backfill, only this section's own prior omission is corrected.

Per this release's own Work Package sequence (no `docs/releases/v0.9.0/
WorkPackages.md` file exists), the **Mechanical Foundation** phase is
**complete, recommended APPROVED by `WP 9.9.0`, awaiting Product Owner
release** — narrated throughout as happening on
`feature/v0.9.0-calculations-workspace`, though `git branch -a` shows
only `main` today (see Current Development Branch, above). Listed here in
**intended numeric order**; each entry also states its own **real
completion order**, since the two diverge from `WP 9.4A` onward — see
the disclosed numbering-gap account immediately below the list:

- `WP 9.0A` — Mechanical Product Structure (the first real Engineering
  Discipline wired into the real Engineering Workspace; `ADR-0080`–
  `ADR-0082`). **Complete.** (1st completed.)
- `WP 9.0B` — Product Configuration & BOM Management (extends
  `WP 9.0A`'s own Mechanical Product Structure in place; `ADR-0083`).
  **Complete.** (2nd completed.)
- `WP 9.1A` — Requirements Management Workspace (the second real
  Engineering Discipline; `ADR-0084`/`ADR-0085`). **Complete.** (3rd
  completed.)
- `WP 9.1B` — Development Baseline & Merge Readiness (verification/
  governance only). **Complete.** (4th completed.)
- `WP 9.2A` — Engineering Calculations Workspace (the third real
  Engineering Discipline, and the first requiring zero Domain-layer
  changes; `ADR-0086`/`ADR-0087`). **Complete.** (5th completed.)
- `WP 9.3A` — Verification Management Workspace (the fifth real
  Engineering Discipline, the third requiring zero Domain-layer changes,
  and the first requiring zero Verification-Framework changes;
  `ADR-0089`/`ADR-0090`). **Complete.** (**6th completed** — completed
  *after* `WP 9.4A` in real time, despite its own earlier number; closes
  the numbering gap below. See Current Work Package, above.)
- `WP 9.4A` — Engineering Documents Workspace (the fourth real
  Engineering Discipline; `ADR-0088`). **Complete.** (**5th completed**
  — completed *before* `WP 9.3A` in real time, despite its own later
  number.) See `WP 9.4A` Summary, above.
- `WP 9.5A` — Manufacturing Workspace (the sixth real Engineering
  Discipline, the fourth requiring zero Domain-layer changes, and the
  first to demonstrate genuine cross-Work-Package read-side reuse of
  another discipline's own Property Facet Provider/Workspace View
  types; `ADR-0091`). **Complete.** (7th completed — intended and real
  completion order agree for this Work Package; no numbering gap.) See
  `WP 9.5A` Summary, above.
- `WP 9.8B` — Platform Service Register Reconciliation (a dedicated
  governance-only Work Package inside the `WP 9.6A`–`WP 9.8A` range
  `WP 9.5A`'s own controlling instruction skipped — see the disclosed
  numbering skip, below — closing the four-Engineering-Foundation-framework
  Platform Service Register/Map gap `WP 7.3A` first disclosed; zero new
  ADRs). **Complete.** (**9th completed** — completed *after* `WP 9.9.0`'s
  own first pass in real time, despite its own earlier number; a direct,
  disclosed response to that pass's own top standing recommendation,
  commissioned before the Product Owner finalised the release. See
  `WP 9.8B` Summary, above.)
- `WP 9.9.0` (first pass) — Release Preparation & Product Baseline (the
  release's own closing Work Package by intended number, skipping
  `WP 9.6A`–`WP 9.8A` per `WP 9.5A`'s own controlling instruction — see
  the disclosed numbering skip, below). **Complete.** (**8th
  completed** — completed *before* `WP 9.8B` in real time, despite its
  own later number.) Recommended `v0.9.0` **APPROVED**. See `WP 9.9.0`
  (First Pass) Summary, above.
- `WP 9.9.0` (second pass) — Release Preparation & Product Baseline, a
  second, independent verification pass, commissioned with the
  identical controlling instruction after `WP 9.8B` closed the first
  pass's own top standing recommendation. **Complete.** (**10th
  completed** — the fourth disclosed numbering irregularity this
  release names: a Work Package number *reused* for a deliberate second
  pass, not a new one — see the disclosed post-hoc-reconciliation
  category, below.) Recommended `v0.9.0` **APPROVED** a second time;
  one new, non-blocking finding (`TD-34`). See `WP 9.9.0` (Second Pass)
  Summary, above.
- `WP 9.9.1` — Product Owner Release Execution (release mechanics only —
  `VERSION` bump, staging, the single release commit, tag creation; no
  merge was required, no feature branch ever existed in the real
  repository). **Complete.** (**11th completed**, `v0.9.0`'s own final
  Work Package.) Prepared everything and stopped before any remote
  operation; the Product Owner then executed the remaining push
  directly, outside any Work Package's own tool-call record. See
  `WP 9.9.1` Summary, above.

Per `docs/releases/v0.10.0/`, the **User Experience & Desktop
Application** phase (`v0.10.0`, Programme 10) is now under way, on the
new `feature/v0.10.0-user-experience` branch:

- `WP 10.0A` — User Experience Architecture (architecture and
  specification only; this project's first two ADR supersessions,
  `ADR-0092` superseding `ADR-0066` and `ADR-0093` superseding
  `ADR-0065`; 30/30 controlling-instruction UX topics covered; zero
  `src/`/`tests/` change). **Complete.** (`v0.10.0`'s own first Work
  Package.) See `WP 10.0A` Summary, above.
- `WP 10.0B` — Desktop Application Framework (`Tempest.Desktop`,
  TempestOS's first graphical desktop application; `ADR-0094` resolves
  `WP 10.0A`'s own first reserved question; zero engineering
  functionality change, zero Workspace contract redesign, both
  independently confirmed; 2036/2036 combined tests passing, 8/8
  "Demonstrate" requirements proven as real tests). **Complete.**
  (`v0.10.0`'s own second Work Package, and its first real
  implementation.) See `WP 10.0B` Summary, above.
- `WP 10.1A` — Engineering Cockpit Implementation (the complete
  graphical Engineering Cockpit over `Tempest.Desktop`; six placeholder
  Cockpit members upgraded to real data; `ADR-0069` realised literally
  via `DocumentAreaView.SetHomeTab`; zero Engineering Domain changes,
  zero Workspace contract redesign, zero new ADRs, all independently
  confirmed; significant new finding `TD-37`, disclosed and deferred;
  `TD-26`'s own mitigation strengthened; 2045/2045 combined tests
  passing). **Complete.** (`v0.10.0`'s own third Work Package, and its
  second real implementation.) See `WP 10.1A` Summary, above.
- `WP 10.1B` — Runtime Host & Module Discovery Hardening (`TD-26` and
  `TD-37` both investigated, root-caused, and fixed at their own true
  source, not merely re-mitigated; `WorkspaceManager.StartAsync` now
  waits for `IDiagnosticsProvider.HostState == HostState.Running`;
  idempotent seeding in all four `TD-37`-affected `Tempest.Samples`
  modules; genuine per-test persistence isolation in
  `Tempest.Desktop.Tests`; `EngineeringCockpitTests`' own three
  previously honest-empty assertions now read real, stable, live data;
  one new, disclosed, deliberately-unfixed finding, `TD-38`; zero
  Engineering Domain changes, zero Workspace contract redesign, zero UX
  redesign, zero new ADRs, all independently confirmed; 2051/2051
  combined tests passing). **Complete.** (`v0.10.0`'s own fourth Work
  Package, and its first pure hardening/root-cause pass.) See `WP 10.1B`
  Summary, above.
- `WP 10.2A` — Workspace Modernisation (a modern Project Explorer —
  context menus, multi-select, inline rename, filtering, breadcrumbs,
  drag/drop preparation architecture; a modernised Property Inspector —
  collapsible sections, a real editable Name field, Lifecycle/Validation
  summaries; pinned Document Area tabs; a six-segment Status Bar; five
  keyboard shortcuts; a shared `DesignTokens` visual-design system; a
  genuine, disclosed `IWorkspaceManager` extension, `ADR-0096`, closing a
  `WP 9.0A`-old platform gap — real object Rename/Delete dispatch; one
  genuine implementation defect found and fixed before commit; zero
  Engineering Domain/Runtime changes, zero forbidden-scope items, all
  independently confirmed; 2069/2069 combined tests passing).
  **Complete.** (`v0.10.0`'s own fifth Work Package.) See `WP 10.2A`
  Summary, above.

**Disclosed, found gap, not fixed retroactively (out of this Work
Package's own scope):** this numbered bullet list has not gained a new
entry since `WP 10.2A` — `WP 10.2B`, `WP 10.3A`, `WP 10.3B`, `WP 10.4A`,
`WP 10.5A`, and this Work Package (`WP 10.5B`) are each fully recorded
in their own "Summary (for reference)" section above, but none was
added here as its own bullet — the identical class of registry/list
drift this session's own governance registers have repeatedly found and
disclosed elsewhere (Academy Register's own eleven-row `## 03 Work
Packages` table gap, `WP 10.2B`; the Future Capability Register's own
stale "Last Reviewed" field, `WP 9.3A`/`WP 9.4A`). Named here plainly as
a candidate for a future dedicated governance Work Package, not
backfilled in the middle of an unrelated implementation Work Package's
own scope.

**Disclosed numbering gap: `WP 9.3A` was not built until after `WP 9.4A`.**
`WP 9.2A`'s own closing instruction was explicit — "Stops here — no
`WP 9.3A` begins until the Product Owner gives further instruction" —
and at the time `WP 9.4A` began, no `WP 9.3A` deliverable of any kind
existed anywhere in this repository. The Product Owner's own instruction
commissioning the next Work Package named `9.4A` directly, skipping that
number; `WP 9.4A` itself recorded this plainly, per "disclose all
inconsistencies... do not silently modify historical records," rather
than silently renumbering or backfilling. `WP 9.4A` itself recommended
a Verification Workspace as the natural next Work Package (`FCR-0055`)
— `Tempest.Core.Verification` (`WP 7.1E`) was, at that point, the one
already-real, Workspace-invisible framework this programme's own
established pattern (one real Engineering Discipline per Work Package)
had not yet reached — and the Product Owner's own next instruction
commissioned exactly that, correctly naming it `WP 9.3A`, its own real,
intended number, closing the gap. **This document's own numeric-order
list above and this document's own real-time narrative (Current Work
Package/`WP 9.4A` Summary, above) therefore disagree on which Work
Package is "5th" and which is "6th" — both are correct, for their own
stated ordering, and neither is silently reconciled to hide the other.**

`WP 9.3A`'s own controlling instruction itself carried a further,
disclosed inconsistency: its own closing line read "Await Product Owner
instruction before `WP 9.4A`" — a copy-paste artifact from the template
`WP 9.4A`'s own instruction used ("Await Product Owner instruction
before `WP 9.5A`"), since `WP 9.4A` was already complete, and had
already recommended `WP 9.3A`, before this instruction was issued.
Recorded here plainly, not silently corrected — see `WP9.3A
Implementation Report.md`.

**`WP 9.5A`'s own controlling instruction skips `WP 9.6A` through
`WP 9.8A` entirely, jumping directly to `WP 9.9.0` Release Preparation.**
No `WP 9.6A`, `WP 9.7A`, or `WP 9.8A` is named or reserved anywhere in
this repository's own governance history — unlike the disclosed
`WP 9.3A` numbering gap above, this is not an inconsistency: `WP 9.2A`
never committed this repository to a `9.6A`–`9.8A` range existing at
all, and the Product Owner is free to sequence Work Packages as
instructed. Recorded here as a plain observation, per the identical
"disclose plainly, do not silently modify" discipline applied throughout
this document, distinguishing a genuine numbering *gap* (`WP 9.3A`,
above — a range the Product Owner's own prior instruction implied would
be filled) from a deliberate numbering *skip* (this one — no such prior
implication exists).

**A third disclosed numbering irregularity: `WP 9.8B` was commissioned
*after* `WP 9.9.0` despite carrying an earlier number, inside the very
`WP 9.6A`–`WP 9.8A` range the paragraph above confirms was never
reserved.** Unlike the `WP 9.3A`/`WP 9.4A` gap (a range the Product
Owner's own prior instruction implied would be filled) and unlike the
`WP 9.6A`–`WP 9.8A` skip (no prior implication existed at all), `WP
9.8B` is neither — it is a **new Work Package, given a number inside a
previously-skipped range, commissioned specifically to close that same
release's own top standing recommendation, after that release's own
closing review already ran.** `WP 9.9.0`'s own "Stops here" closing
instruction was honoured — no further Work Package began until the
Product Owner gave further instruction, and that further instruction
was `WP 9.8B`. Recorded here as a fourth distinct category this
document's own numbering-disclosure discipline now names explicitly: a
**post-hoc reconciliation Work Package**, numbered to sit where it
would have belonged had it been anticipated, not appended after `WP
9.9.0` as `WP 9.9.1` or similar — a genuine, deliberate Product Owner
choice, not an error, and not silently smoothed over by renumbering
`WP 9.9.0` or `WP 9.8B` to make their own real completion order match
their own numeric order.

**A fourth disclosed numbering irregularity, distinct from the first
three: `WP 9.9.0` was commissioned a second time, with the identical
controlling instruction, after `WP 9.8B` closed.** Unlike the three
irregularities above (a gap, a skip, and a post-hoc reconciliation Work
Package each occupying their own previously-unused number), this is a
**Work Package number reused for a second, independent pass** — not a
new number, and not a correction of the first pass (which remains
correct, and whose own documents are left exactly as written). Named
here as a fifth category: a **re-verification pass**, sitting alongside
its own first pass under the identical number, distinguished only by
"(First Pass)"/"(Second Pass)" suffixes on every document either one
produced. Confirmed, by direct Product Owner instruction, to be
deliberate — the natural close of a "verify, remediate, re-verify"
cycle this project's own governance discipline has now performed in
full for the first time, not an accidental duplicate instruction.

**Disclosed gap, not silently filled: this section was never given a
`v0.10.0` entry.** `v0.10.0`'s own sixteen Work Packages (`WP 10.0A`
through `WP 10.9A`) shipped, per `Current Release`/`Current Work
Package` above and `docs/releases/v0.10.0/Release Notes.md`, but no
corresponding bullet list was ever added here — found while adding this
section's own `v0.11.0` entry below, recorded plainly rather than
reconstructed after the fact from memory, per this document's own
"disclose, do not silently backfill" convention. A future documentation-
currency Work Package (`WP 11.1B`'s own governance-currency pass, or a
successor) is the right place to close this specific gap, not this one.

Per `docs/releases/v0.11.0/WorkPackages.md`, the Release Engineering &
Architecture Governance phase (`v0.11.0`) is under way:

- `WP 11.0A` — Platform Architecture & Code Quality Review (review only;
  ten findings, zero Critical). **Complete.**
- `WP 11.0B` — v1.0 Architecture Roadmap & Release Planning (planning
  only; scopes `v0.11.0`–`v1.0.0`). **Complete.**
- `WP 11.1A` — CI/CD Pipeline Standup (`.github/workflows/ci.yml`,
  closing finding `R-1`; local verification: 0 Warnings/0 Errors both
  configurations, 2,266/2,266 tests passing; GitHub-hosted execution
  disclosed unverified pending the first real push). **Complete.**
- `WP 11.1B` — as actually commissioned: **Branch Protection &
  Engineering Workflow Hardening**, not the roadmap's own originally
  predicted "Governance Currency Pass & Health-Check Tooling" (see Next
  Planned Work Package, above, for the disclosed divergence). Branching
  strategy, pull request workflow, merge/release requirements, a new
  `.github/workflows/release.yml`, hotfix workflow, rollback procedure,
  release-candidate process. **Complete.**
- `WP 11.2A` — as actually commissioned: **Governance Health-Check
  Automation** — `scripts/governance-healthcheck.ps1`, delivering
  `FCR-0005` (the roadmap's original `WP 11.1B` scope, delivered here
  instead — see Next Planned Work Package, above). Eight checks, three
  genuine tool defects found and fixed, two genuine real governance
  findings disclosed (Academy Index "Work Package Walkthroughs" stops at
  `WP 7.3A`; four untracked empty-directory references). Wired into CI,
  not yet a required check. **Complete.**
- `WP 11.3A` — Presentation Strategy Review & Platform Consolidation —
  the Desktop & Console Presentation Strategy Decision (`WP11.0A`
  finding `A-2`), finally commissioned directly. Review only:
  `TempestShell` found provably dead (unreachable since `ADR-0068`,
  `WP 8.1A`, `v0.8.0`); `README.md`/`Contributor Learning Path.md` found
  critically stale (omit `Tempest.Desktop` entirely); `ci.yml`/
  `release.yml` found packaging both presentation layers as symmetric
  release artifacts. Recommends retirement of `TempestShell`, a new ADR
  ratifying `Tempest.App`/`WorkspaceShell` as an Internal Engineering
  Harness, and documentation/release-packaging corrections — a five-
  stage roadmap, none of it executed here. **Complete.**
- `WP 11.3B` — Presentation Strategy Implementation — executes
  `WP11.3A`'s own approved Stages 1–4: `TempestShell`/`IPage`/
  `PlaceholderPage` retired (`git rm`, proven dead, zero live references);
  `ADR-0101` authored (cites `ADR-0068`/`ADR-0092`, neither modified);
  `README.md`/`Contributor Learning Path.md`/`Platform Service Map.md`/
  `Shell & Composition Framework Architecture.md` corrected; three
  governance registers narrowly corrected; `ci.yml`/`release.yml` now
  package `Tempest.Desktop` and `Tempest.App` as two separate,
  clearly-labelled artifacts. Verified directly: `Tempest.Desktop`
  launched and ran cleanly; `WorkspaceShell` ran a full start-to-shutdown
  cycle, exit code 0; full regression both configurations, 2,228/2,228
  passing. Stage 5 remains deferred, per `WP11.3A`'s own recommendation.
  **Complete.**
- `WP 11.9.0` — `v0.11.0` Release Preparation & Engineering Sign-Off.
  **Not started — the only remaining item before `v0.11.0` can close.**

See `docs/releases/v0.11.0/WP11.0B Architecture Roadmap.md` for the full
scope of `v0.12.0`, the conditional `v0.13.0`, and `v1.0.0` beyond this
release — not repeated here, per this section's own "index, do not
duplicate" convention.

## Long-Term Vision

**`VISION.md` (repository root) is now the authoritative, complete
product vision document** — established by `WP 7.0A`, superseding the
brief paragraph this section previously held as the only vision
statement in the repository. This section is kept short deliberately;
read `VISION.md` for the full account (what TempestOS is, why it
exists, target users, engineering and architectural philosophy, product
principles, what it deliberately is not, the Platform-vs-Engineering-
Module boundary, and the vision beyond `v1.0`).

In summary: TempestOS aims to be an extensible platform other people
build on, not merely a runtime that hosts a fixed set of built-in
capabilities — see `docs/releases/v0.4.0/ReleasePlan.md`'s own "From
Runtime to Platform" theme, the origin of this ambition. The Platform
Services phase (`v0.6.0`) proved cross-service platform capability;
`VISION.md` now names what that platform is *for* — engineering-practice
capability across the nine Engineering Discipline categories
`docs/governance/Capability Categories.md` establishes, beginning with
Systems Engineering and Project Management (the "Requirements Engine"
and "Project Engine" `ADR-0013`'s own Future Considerations already
named), each still requiring its own explicit Platform-Service-vs-Module
classification before design begins. The governing constraint on all of
it remains `docs/releases/FOUNDATION.md`: every future capability is a
module or platform service running inside the one Runtime Host this
foundation established, never a second, parallel execution model — and
every future Work Package is expected to build capability against that
stable foundation rather than revisit it, absent evidence that requires
otherwise (see `docs/governance/Future Work Package Guidelines.md`).

---

## Maintaining This Document

Update this file as part of the Definition of Done for any Work Package
that changes: the current branch, release, or Work Package; Foundation
status; a Repository Metrics figure; Repository Health; or a Known
Unknown being resolved. Keep it short — this is a dashboard, not a
narrative; link to the fuller document (Governance suite, Academy,
`WorkPackages.md`) rather than inlining detail that belongs there.
