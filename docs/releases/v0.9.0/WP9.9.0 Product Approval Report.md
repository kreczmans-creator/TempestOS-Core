# WP 9.9.0 — Release Preparation & Product Baseline — Product Approval Report

## Purpose

The formal recommendation this Work Package's own controlling
instruction required: state whether `v0.9.0` is **APPROVED** or **NOT
READY**, supported by the evidence gathered across the complete release
readiness review (`WP9.9.0 Release Readiness Report.md`), the
engineering statistics baseline (`WP9.9.0 Engineering Statistics
Report.md`), and the architecture and capability baselines (`WP9.9.0
Architecture Baseline Summary.md`, `WP9.9.0 Engineering Capability
Summary.md`).

## Recommendation

# **APPROVED**

`v0.9.0` ("Mechanical Foundation") is recommended for Product Approval,
release, tagging, and merge to `main` by the Product Owner.

## Evidence Supporting This Recommendation

### Build and Test — Clean

- 4/4 projects build with 0 warnings, 0 errors, in both Debug and
  Release configurations, from a fully clean rebuild, plus per-project
  Release builds of `Tempest.App`/`Tempest.Samples`.
- 2026/2026 tests passing, confirmed across four consecutive full-suite
  runs (two Debug, two Release — the second Release run reproducing
  `scripts/new-release.ps1`'s own exact invocation), plus a dedicated
  516-test run scoped to this release's own seven Workspace namespaces,
  plus one targeted probe of the one previously-disclosed flaky test
  class — zero flakes, zero regressions anywhere in this release's own
  scope, and zero regression in any of the 1631 tests `v0.8.0` itself
  already shipped.

### Scope Discipline — Held

All seven Work Packages this release comprises delivered exactly what
their own controlling instruction named — six real Engineering
Disciplines wired into the Workspace, using exclusively the already-real
Engineering Domain, Workspace, and Digital Thread. **Four of seven
required zero Domain-layer changes at all** (`WP 9.2A`, `WP 9.4A`,
`WP 9.3A`, `WP 9.5A`); the remaining three (`WP 9.0A`, `WP 9.0B`,
`WP 9.1A`) each made only small, additive Domain-layer facet extensions
(`IRenamable`/`IHasParent`/`IDeletable`/`IHasBomLine`), never a
reopened, frozen `WP8.2B` contract. Zero architectural redesign
occurred at any of the seven implementation stages — confirmed directly
by this Work Package's own Architecture Review (Release Readiness
Report §8).

### Security — Fully Restored, Zero Release Blocking

**Seven dedicated Security Reviews this release — one per Work
Package**, a full recovery of `v0.8.0`'s own disclosed "zero dedicated
Security Reviews" gap, and the single most important standing
recommendation `WP8.9.0 Product Approval Report.md` itself named for
whatever followed it. Every one of the seven independently confirmed
zero Release Blocking findings; re-verified at the release level by
this Work Package's own Release Readiness Report §18 — zero permission-
gating availability defect reachable from any passive Workspace surface
across all six real disciplines, zero new deserialisation surface, and
the one disclosed cross-Work-Package reuse (`WP 9.5A`'s own facet-provider
reuse) confirmed, by dedicated tests, to introduce no new authorisation
path.

### Certification — Independently Re-Verified, Not Assumed

This Work Package (`WP 9.9.0`) independently re-derived every headline
figure directly from the repository (`grep`, `find`, `dotnet build`,
`dotnet test`) rather than trusting any prior Work Package's own
retrospective claim — cross-checking the full `1631 → 1695 → 1738 →
1808 → 1865 → 1922 → 1972 → 2026` test-count chain arithmetically
against all seven Work Packages' own stated deltas (395 new tests total,
confirmed to sum exactly) and the `79 → 82 → 83 → 85 → 87 → 88 → 90 →
91` ADR chain (12 new ADRs, confirmed to sum exactly) — both found
internally consistent, with zero arithmetic drift this time (unlike
`WP 8.2C`'s own disclosed 39→38 correction the prior release cycle).

### Technical Debt — Fully Disclosed, Zero Release Blocking

33 tracked Technical Debt items (25 → 33, `TD-26` through `TD-33`, one
per Work Package), 17 disclosed trade-offs (unchanged). **Every genuine
limitation this release surfaced was disclosed at the Work Package that
found it, none discovered newly by this review, none Release Blocking**
— each is either a narrow data-visibility/display-accuracy
characteristic with a confirmed-correct underlying data path (`TD-29`,
`TD-32`, `TD-33`), or a documentation-completeness gap with zero
functional consequence.

### Governance — Sound, With Disclosed (Not Hidden) Gaps

Every governance register this Work Package's own controlling
instruction named was audited directly against the repository. Zero new
arithmetic-error findings were identified this review (unlike `v0.8.0`'s
own 39→38 correction); two pre-existing, already-disclosed
findings were reconfirmed still open, neither newly found: the
four-framework Platform Service Register/Map gap (now open across three
consecutive release-closing reviews), and the "32 vs. 35 governance
documents" count drift (open since `WP 9.3A`, one release-cycle old).
See Release Readiness Report §7 for the complete account. None required
modifying a historical record.

## Why the Disclosed Gaps Do Not Block Approval

Neither open finding in `WP9.9.0 Release Readiness Report.md` affects
shipped functionality, test coverage, build cleanliness, or introduces a
known, unaddressed vulnerability. The Platform Service Register/Map gap
is a documentation-currency gap in a governance index describing
capability that either predates this release entirely (the four
Engineering Foundation frameworks) or is correctly absent because none
of the six real Engineering Disciplines this release wires up was ever
a Platform Service to begin with (`ADR-0062`). The governance-document
count drift is a stale summary figure in a register whose own
underlying content (27 individually-tracked registers) is itself
accurate and current — the "32" figure describes an undocumented
historical categorisation this and two prior reviews could not
reconstruct, not a missing or incorrect governance record. Both are
named as standing recommendations, below, not waved away.

## Constraints Honoured

Per this Work Package's own explicit constraints: verification only, no
new platform functionality, no architectural changes, no implementation
changes beyond what correcting a genuine release-blocking defect or
stale governance would require (none was required — zero release-blocking
defects were found, and every governance figure this review touched was
either already accurate or corrected via a disclosed, additive edit to a
governance register, never a historical Work Package's own retrospective
or Accepted ADR). No Git merge, tag, `VERSION` change, or push was
performed by this Work Package — the `VERSION` file correctly remains
`0.8.0` (see `WP9.9.0 Release Readiness Report.md` §4).

## What Happens Next

Per this Work Package's own explicit closing instruction: **STOP. Await
Product Owner release.** This Work Package does not create the Git tag,
does not merge to `main`, does not bump `VERSION`, and does not push —
those actions belong to the Product Owner, to be performed after this
recommendation is accepted, following the identical sequence
`v0.6.0`/`v0.7.0`/`v0.8.0` each already established (merge the
development work to `main`, tag `v0.9.0`, bump `VERSION`, push). No
further `v0.9.0` Work Package, and no `v0.10.0`/`v1.0` Work Package,
begins until the Product Owner gives further instruction.

**Standing recommendations for whatever Work Package follows this
release:**

1. **Close the four-framework Platform Service Register/Map gap** —
   now open across three consecutive release-closing reviews
   (`WP 7.4.0`, `WP 8.9.0`, `WP 9.9.0`), the single most persistent
   disclosed governance finding across two full release cycles.
2. **Reconstruct or formally retire the "32 governance documents"
   figure** — either locate/re-derive the original 27-registers/32-total
   taxonomy, or replace it with the directly-verifiable 35-file count
   this and the two prior reviews have each independently confirmed.
3. **Continue the now-fully-restored dedicated-Security-Review
   discipline** into whatever Work Package follows — `v0.9.0` closed
   `v0.8.0`'s own gap in full; carrying the practice forward, rather than
   letting it lapse a second time, is the cheapest way to keep it closed.

## Related Documents

`docs/releases/v0.9.0/WP9.9.0 Release Readiness Report.md`;
`docs/releases/v0.9.0/WP9.9.0 Engineering Statistics Report.md`;
`docs/releases/v0.9.0/WP9.9.0 Architecture Baseline Summary.md`;
`docs/releases/v0.9.0/WP9.9.0 Engineering Capability Summary.md`;
`docs/releases/v0.9.0/ReleaseNotes.md`; `docs/releases/v0.9.0/
Retrospective.md`.
