# TempestOS v0.13.0 — Work Packages

## Status

**In progress.** `v0.13.0` — "Trust & Deployment Hardening" — branch
`feature/v0.13.0`, cut from the released `v0.12.0` tag (`13a6ce3`), not
from any working branch, per this release's own explicit `WP 13.0.0`
Branch Establishment instruction — confirmed directly, `git merge-base
feature/v0.13.0 v0.12.0` resolves to `13a6ce3` exactly, the identical
commit `v0.12.0` itself points to. This document is created now, per
this project's own established convention ("each release's own
`WorkPackages.md` is created when that release's branch is cut,"
`WP11.4B Release Process Correction Report.md` §10), seeded from
`WP11.0B Architecture Roadmap.md` §3's own predicted `v0.13.0` table.

**This release is conditional, and the condition is now confirmed
triggered.** `WP11.0B Architecture Roadmap.md` §3 is explicit:
`v0.13.0` "only enters the plan if `WP 11.2A` / the Product Owner
commits `v1.0` scope to third-party plugins and/or REST deployment
beyond a trusted local network. Otherwise… this release is skipped
entirely — the plan collapses to `v0.11.0` → `v0.12.0` → `v1.0.0`."
Directly confirmed, at this Work Package's own commissioning: neither
trigger had fired as of `v0.12.0`'s own close (`src/Plugins/` remains
empty — `WP 12.9.1`'s own tracked marker `README.md` — and no
`v0.12.0` Work Package touched REST API scope). The Product Owner has
now explicitly made that commitment, confirmed directly, commissioning
this branch — recorded here as the trigger event itself, not assumed
or inferred from the branch's mere existence.

**Predicted scope**, per `WP11.0B Architecture Roadmap.md` §3: Work
Package, Scope, and Type columns reproduced verbatim; Status updated
from that table's own `Conditional` to `Not started` now that the
condition has fired — no `v0.13.0` Work Package has yet begun:

| Work Package | Scope | Type | Status |
|---|---|---|---|
| `WP 13.0A` | Plugin & Registration Trust Isolation Architecture (`A-3`/`FCR-0001`) | Architecture | **Complete** |
| `WP 13.0B` | ~~Plugin & Registration Trust Isolation Implementation~~ — **actually commissioned as** Plugin & Trust Isolation Architecture Review and Baseline (independent audit of `WP 13.0A`, not the implementation this table originally predicted — disclosed, not silently reconciled; see below) | ~~Implementation~~ Governance/Process | **Complete** |
| `WP 13.1A` | REST API Authentication & TLS Architecture (`A-4`/`FCR-0003`/`FCR-0004`) | Architecture | Not started |
| `WP 13.1B` | REST API Authentication & TLS Implementation | Implementation | Not started |
| `WP 13.9.0` | `v0.13.0` Release Preparation & Engineering Sign-Off | Verification only | Not started |

**Not re-derived or re-scoped here** — this document records the
roadmap's own predicted plan at the moment this branch was cut; the
first substantive Work Package (`WP 13.0A`, expected) is where a real,
independent architectural investigation of `A-3`/`FCR-0001` belongs,
mirroring exactly how `WP 12.0A` opened `v0.12.0`'s own first roadmap-
predicted item rather than re-litigating it here.

**Disclosed divergence from this table's own prediction, found when
`WP 13.0B` was actually commissioned:** the roadmap predicted `WP 13.0B`
would be the Trust Isolation *Implementation* Work Package, directly
following `WP 13.0A`'s architecture. What was actually commissioned
under that exact label was instead an independent **Architecture Review
and Baseline** — auditing `WP 13.0A`'s own design before it becomes a
commit, not implementing it. This is recorded plainly, not silently
reconciled with the original prediction, mirroring `WP 12.2A`'s own
precedent of disclosing exactly this class of roadmap-vs-reality
divergence rather than quietly renumbering around it. **The real Trust
Isolation Implementation Work Package the roadmap originally predicted
remains not yet commissioned, and does not yet have a number** — it is
not `WP 13.0B` (that label is now taken by the review Work Package
above, already Complete) and this document does not invent one
speculatively; the next Work Package to take up that scope names its own
number when it is actually commissioned, per this project's own
established "do not pre-number a Work Package that has not yet been
briefed" discipline.

## Branch Discipline

Per this release's own explicit `WP 13.0.0` instruction — stricter
than every prior release's own convention (`v0.11.0`/`v0.12.0` each
used multiple parallel `feature/vX.Y.0-*` branches, one per Work
Package or Work Package pair):

- `feature/v0.13.0` is the **sole** integration branch for every
  `v0.13.0` Work Package. No additional feature branches without
  explicit, separate authorisation.
- Every Work Package commits **directly** to `feature/v0.13.0`.
- **Never rebase. Never squash.** History stays linear through
  sequential commits on this one branch.
- Merge commits only when integrating `feature/v0.13.0` back to `main`
  at this release's own close (mirroring Engineering Governance §7's
  own merge-commit-only rule, applied here one level earlier, to the
  Work-Package-to-branch relationship as well as the branch-to-`main`
  one).

## Work Packages

Two process/governance Work Packages completed so far, outside the
roadmap-predicted table above (mirroring how `v0.12.0`'s own `WP
12.9.1`/`WP 12.9.2`/`WP 12.9.3B` were real, additive Work Packages
never named in that release's own original roadmap prediction either).
`WP 13.0A` (the roadmap's own first predicted item) has not yet begun.

| Work Package | Scope | Type | Status |
|---|---|---|---|
| `WP 13.0.0` | `v0.13.0` Branch Establishment — creates `feature/v0.13.0` directly from the released `v0.12.0` tag (`13a6ce3`), confirmed the exact branch point six independent ways (Architecture Agent). Two real conflicts resolved with the Product Owner before proceeding, not silently applied: `VERSION → 0.13.0-dev` would have broken `governance-healthcheck.ps1`'s own version-token parsing (`[version]"0.13.0-dev"` throws) and has no precedent in this project's documented versioning policy — `VERSION` kept at `0.12.0`, mirroring how it stayed `0.11.0` throughout all of `v0.12.0`'s own development; `v0.13.0`'s own roadmap-conditional scope confirmed now explicitly triggered by the Product Owner. Establishes stricter branch discipline than any prior release: `feature/v0.13.0` is the sole integration branch, commit directly to it, never rebase, never squash. Three parallel verification agents (Architecture/Repository/Governance) all Pass; Governance Agent's one minor finding (imprecise "verbatim" wording) fixed same session, second sequential commit. See `WP13.0.0-v0.13.0-branch-establishment.md` (Academy retrospective). | Governance/Process | **Complete** |
| `WP 13.0.0A` | Release Register Reconciliation — closes the one finding `WP 13.0.0` disclosed rather than fixed: `docs/governance/Delivery/Release Register.md` had no row for the `v0.12.0` tag. Independently confirmed pre-existing, dating to `v0.12.0`'s own close (`git show 13a6ce3:...` — already missing at the exact commit the tag points to), not caused by any `v0.13.0` Work Package. `v0.12.0` row added, matching the established evidence/density convention; all eleven real tags (`v0.3.0`–`v0.12.0`) independently re-verified present in the Entries table exactly once each. `governance-healthcheck.ps1` re-confirmed clean: 7 passed, 1 warned, 0 failed, exit 0. Governance/documentation only; zero `src/`/`tests/` files touched, zero architecture/implementation/release behaviour changed. See `WP13.0.0A-release-register-reconciliation.md` (Academy retrospective). | Governance/Process | **Complete** |
| `WP 13.0A` | Plugin & Registration Trust Isolation Architecture — this release's own first roadmap-predicted Work Package (`A-3`/`FCR-0001`). Four parallel architecture sub-agents (Plugin Architecture; Security & Trust; Governance & ADR Review; Documentation & Academy), each writing new, non-overlapping files, reconciled into one coherent design by a single integrating pass rather than each editing the project's own shared registers directly. **Plugin Architecture** (`docs/architecture/Plugin Platform Architecture.md`): manifest v2 (`Dependencies`, plus shape-only `RequestedCapabilities`/`Publisher`/`Signature` fields whose semantics the Security & Trust document owns), dependency-graph resolution inside the existing Phase 3.1 (no new Host Lifecycle phase), a new Host-owned `IPluginRegistry` projected read-only via `IDiagnosticsProvider.Plugins`, a configurable plugins root/manifest convention (closes `FCR-0010`/`TD-06`), and an explicit, defended decision that live in-process plugin unload remains a non-goal for `v0.13.0` (`ADR-0015` reaffirmed) while reserving an unused lifecycle seam contingent on a future isolation-mechanism change. `ADR-0107`–`ADR-0109`. **Security & Trust** (`docs/security/Plugin Trust & Isolation Architecture.md`): a four-tier trust model; a capability model extending `IPermissionEvaluator` (`ADR-0044`) via a new, `AsyncLocal<T>`-backed `ICurrentComponentAccessor` distinct from the existing ambient `ICurrentPrincipalAccessor`; a detached SHA-256 manifest+assembly signature verified entirely at Plugin Discovery, no new NuGet dependency; and the central isolation-boundary decision — capability-scoped, in-process enforcement, **not** a separate `AssemblyLoadContext` (confirmed, on direct technical grounds, not a security boundary in modern .NET) and **not** process separation (disproportionate to the disclosed threat: vetted, signed, commercial plugins, not an open marketplace). This design directly closes the architecture gap behind `TD-09`/`TD-10`/`TD-11` — a trust-tier-ordered registration rule replaces unconditional "first registration wins" for Navigation/Command Id ownership, closing `Security Roadmap.md` items 1, 2, and 10 together, as that roadmap's own item 10 recommended. `ADR-0110`–`ADR-0112`. Both documents were reconciled directly against each other during drafting (matching manifest field shapes; failure-classification categories numbered 12–14 and 15–18 to avoid collision; the Security & Trust document's own isolation decision confirmed against, and consciously leaves unused, the Plugin Architecture document's reserved lifecycle seam) — one integration gap both sides explicitly deferred (`PluginRegistryState.TrustDenied`, a sixth enum value referenced but not literally declared) found and closed directly in the same file both agents already owned. Nineteen new Rejected Designs entries (`RD-0046`–`RD-0064`) and one addendum (`RD-0009`, reaffirmed not reversed). **Governance & ADR Review** independently re-derived the full governance landscape before either architecture agent's output existed, confirmed a `governance-healthcheck.ps1` clean baseline (7 passed, 1 warned, 0 failed), and — critically — found that this Work Package's own true scope (per its own title, "Plugin **& Registration** Trust Isolation Architecture") is broader than `FCR-0001`'s own narrow three-call-site framing alone suggests; verified in a second pass, after integration, that none of the six new ADRs contradicts an existing Accepted ADR and that the numbering/register updates are internally consistent, re-running the health check against the fully integrated state. **Documentation & Academy** extended `docs/academy/02 Runtime Architecture/07-plugin-architecture.md` in place (not a new article, per Engineering Governance §6's own "fit an existing document first" discipline), wrote this Work Package's own 13-section Academy retrospective, and updated `WP6.1-permissions-and-identity-implementation.md`'s own Future Evolution section to connect its prediction to what this Work Package actually did. **Zero `src/`/`tests/` files touched, zero code written, zero implementation introduced** — confirmed directly at every stage; a real, still-unnumbered implementation Work Package remains the roadmap's own next step (see `WP 13.0B`'s own row, below, for the disclosed divergence — `WP 13.0B` itself became an independent architecture review, not the implementation this row originally anticipated). See `WP13.0A-plugin-and-registration-trust-isolation-architecture.md` (Academy retrospective). | Architecture | **Complete** |
| `WP 13.0B` | Plugin & Trust Isolation Architecture Review and Baseline — an independent review of `WP 13.0A`, not the implementation the roadmap-predicted table originally named this number for (disclosed divergence, see the note above that table). Three parallel, genuinely independent audit sub-agents — Architecture Audit, Governance Audit, Documentation Audit — none of which authored any `WP 13.0A` material, each strictly read-only, each reporting file-and-line findings rather than editing anything themselves. **Architecture Audit** (PASS): all six new ADRs re-checked against Engineering Governance §5 criteria; a full-corpus sweep of all 111 ADRs for contradiction (wider than any single prior pass); all nineteen Rejected Designs entries spot-checked against their cited sources. One blocking fix (a copy-paste failure-category range in `Plugin Trust & Isolation Architecture.md`'s Logging & Telemetry section, "12–15" corrected to "15–18") and one citation-completeness fix (`ADR-0111` now explicitly acknowledges and cites `ADR-0032`/`ADR-0037`, whose already-Accepted registration behaviour its trust-ordered rule additively revises). **Governance Audit** (FAIL → fixed): re-derived every count independently (ADR total, Rejected Designs total, Academy/Documentation Register counts) and re-ran `governance-healthcheck.ps1` — found `ADR Register.md`'s own separate `**Total: 104 ADRs**` paragraph had drifted stale independently of the `Last Reviewed`/`Related ADRs` fields `WP 13.0A` had already corrected, an internal self-contradiction within the same register; found `WP13.0A Governance Landscape Brief.md`'s own closing verdict still read "cannot yet be called complete" after its underlying findings had in fact already been fixed. Both corrected — the Brief via an appended addendum, not a rewritten verdict, per this project's own "disclose a finding's history, don't edit it away" discipline. A double-encoded `&amp;amp;` typo in this very document's own `WP 13.0A` row also found and fixed. **Documentation Audit** (FAIL → fixed): confirmed the retrospective's all-thirteen-section completeness and fact-checked it against the architecture source documents; found its own §12 Future Evolution falsely claimed `Host Lifecycle.md`/`Failure Behaviour.md` were untouched by `WP 13.0A`, when `git diff` shows both carry substantial, already-merged "Extended, `WP 13.0A`" content — corrected to state the true documentation-vs-code distinction (the *prose* extension is done; the *code* it describes is `WP 13.0B`'s — now this Work Package's own — remaining, still-unassigned successor's task). The retrospective's own incomplete "Files changed" list was also corrected. A wrong section citation in the `WP6.1` retrospective's own `WP 13.0A` addition (§7 → §10/§12) was fixed. **Every fix applied was a defect `WP 13.0A` itself introduced — nothing pre-existing was touched or re-flagged** (`Rejected Designs Register.md`/`Governance Index.md`'s own long-stale counts, already disclosed by `WP 13.0A` as out of scope, confirmed still untouched). `governance-healthcheck.ps1` re-run clean after every fix: 7 passed, 1 warned, 0 failed, exit 0 — identical shape to `WP 13.0A`'s own closing baseline. No new ADR, no new architecture, no broadened scope. Zero `src/`/`tests/` files touched throughout. `WP 13.0A`'s and this Work Package's combined material committed as a single architecture baseline immediately following this Work Package's own close. See `WP13.0B-plugin-and-trust-isolation-architecture-review-and-baseline.md` (Academy retrospective). | Governance/Process | **Complete** |

## Related Documents

`docs/releases/v0.11.0/WP11.0B Architecture Roadmap.md` §3 (this
release's own originally-predicted scope, now confirmed triggered);
`docs/releases/v0.12.0/WorkPackages.md` (the immediately preceding
release, and this document's own format precedent); `PROJECT_STATUS.md`;
`docs/governance/Future Capability Register.md` (`FCR-0001`–`FCR-0004`).
