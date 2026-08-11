# Engineering Cockpit — Graphical Implementation

## 1. Introduction

This is the Academy's own concept guide for the complete graphical
Engineering Cockpit (`WP 10.1A`) — the third implementation Work
Package of `v0.10.0`, and the first to systematically audit every
disclosed Cockpit placeholder this project has carried since `WP 8.1C`.

## 2. Purpose

To explain why auditing existing disclosed placeholders, rather than
only building new UI, was this Work Package's own central act — and
what that audit found.

## 3. Background

`EngineeringCockpit` (`Tempest.App.Workspace`, `WP 8.1C`) has grown
incrementally since `v0.8.0` — every real-discipline Work Package
(`WP 9.0A` through `WP 9.5A`) added its own real KPI cards, but the
Cockpit's own class-level XML documentation had, since `WP 8.1C`,
plainly disclosed which of its own members remained fixed placeholder
content. `WP 10.0B` gave this platform a real graphical surface to show
that Cockpit on; `WP 10.1A` is the Work Package that actually reads
every one of those disclosures and asks, for each: does the platform
now have the real data to back this?

## 4. The Problem

Twenty named Cockpit regions, several still disclosed placeholders. The
controlling instruction's own explicit rule: replace every placeholder
where real data already exists; never fabricate one where it does not.
Answering "which is which" correctly, for all twenty, was the real
engineering work — not the dashboard's own visual construction.

## 5. The Design

Six placeholders were upgraded to real reads: `OpenDecisions`,
`BlockedItems`, `Health`/`HealthScoreDisplay`, `RiskSummary`,
`DigitalThreadSummary`, `UpcomingMilestones` — each backed by Domain
classes (`IDecision`/`IRisk`/`IHazard`/`IMilestone`, `WP 8.2C`) that had
compiled, and in most cases had real sample data, since `v0.8.0`, but
had never once been read by any Workspace surface. Two were reconfirmed
correctly still placeholder — `FavouriteProjects` (no capability
exists anywhere) and `OverdueActions` (no due-date field exists on
`ITask`/`IAction`) — with a new, honestly-distinct real substitute
(`OpenTaskCount`) added alongside the second. `CockpitView`
(`Tempest.Desktop`) then presents all twenty as a responsive,
card-based dashboard over `DocumentAreaView`'s own new permanent Home
tab, finally realising `ADR-0069` ("the Engineering Cockpit is the
Workspace's own default landing screen") literally.

## 6. Alternatives Considered

Building the dashboard first and wiring real data in later was
considered and rejected — it would have meant guessing at each card's
own real-vs-placeholder status rather than deriving it from the
Cockpit's own existing, authoritative disclosures, risking exactly the
"fabricate engineering data" outcome the controlling instruction
explicitly forbids.

## 7. Why This Solution Was Chosen

Because `EngineeringCockpit`'s own six years (in this project's
compressed timeline) of disclosure discipline had already done the
hard classification work — every placeholder member said, in its own
XML documentation, exactly why it was one. Auditing that documentation
directly, rather than re-deriving placeholder status from scratch, was
both faster and more reliable.

## 8. Architectural Principles

- **A disclosed placeholder is a to-do list, not a permanent
  decision.** `WP 8.1C`'s own placeholders were never meant to stay
  placeholders forever — they were honest statements of "not yet,"
  each naming exactly what would need to become real. This Work
  Package is the payoff of that discipline: reading six "not yet"
  statements and finding four of them had quietly become "yes" without
  anyone updating the comment.
- **A stronger readiness signal replaces a narrower one when a real
  need demonstrates the gap.** `WorkspaceHost`'s own `TD-26` mitigation
  moved from a six-item proxy to the Runtime Host's own authoritative
  `HostState.Running` signal, the moment testing genuinely new sample
  data exposed the proxy's own blind spot.
- **Module failure isolation, observed working, not just declared.**
  `FOUNDATION.md`'s own principle #4 predicted exactly what this Work
  Package found: four modules can fail their own initialisation and
  the platform still reaches `Running`. This is the first time any test
  in this project's history has directly observed that guarantee hold.

## 9. Benefits

Six real Cockpit regions where none existed before, using data that
was already there. A stronger, more general Runtime Host readiness
signal, adopted platform-wide within `Tempest.Desktop`. A genuine,
previously-invisible sample-module defect found and disclosed, not
because anyone went looking for it, but because reading real data
honestly required proving it was actually there.

## 10. Trade-offs

The sample-module defect (§Technical Debt Review) is disclosed, not
fixed — three of this Work Package's own six upgraded members
currently display an honest empty state, correct but less visually
compelling than the real, populated sample data this Work Package
originally expected to show. `HealthColors`'s own concrete colour
values are a first-iteration placeholder for `ADR-0094`'s own deferred
design-system tokens.

## 11. Common Mistakes

Assuming a Domain class compiling and having a documented interface
means it has been exercised end-to-end — `IDecision`/`IRisk`/
`IMilestone` all compiled since `WP 8.2C`, real sample data existed for
most since `v0.8.0`/`v0.9.0`, and still nothing read any of it until
this Work Package. Assuming a fixed placeholder string ("no service
exists yet") is still accurate just because it was accurate when
written — three of the six placeholders this Work Package upgraded had
quietly become false between when they were written and when this Work
Package finally checked.

## 12. Future Evolution

The sample-module registration defect's own root cause (`WP10.1A
Technical Debt Review.md` §1) is the most concrete near-term follow-up.
A full Governance & Risk Workspace discipline (`FCR-0056`) remains the
natural next real-discipline candidate, now with a Cockpit already
reading its own underlying Domain data. `HealthColors`'s own concrete
values should be revisited once a dedicated visual-design Work Package
exists.

**Two placeholder KPI cards closed, `WP 10.7A`/`WP 10.8A`.** The
Engineering Health Summary's own "Risks" card was hardcoded
`IsPlaceholder: true` regardless of any live data — it now reads
`LiveRisks.Count` directly, the identical read `RiskSummary` already
used. "Review" was identically hardcoded — it now sums each
discipline's own already-computed in-review count
(`RequirementsInReviewCount`/`CalculationsInReviewCount`, two small
properties promoted from previously-inline closures, plus the already-
named `OutstandingDocumentReviews`); Verification/Manufacturing have no
equivalent single named count exposed publicly today, so they are
honestly not included rather than approximated. Neither change added a
new read — both reuse a computation this class already performed
elsewhere for a different card. The Desktop-layer `CockpitView`'s own
"Favourite Projects" card similarly now reads the real, already-shipped
`FavouriteObjectsState` (`WP 10.6A`) filtered to Kind `"Project"`,
rather than its own fixed message — `EngineeringCockpit.FavouriteProjects`
itself (the class this article describes) is unchanged, still genuinely
empty, a deliberately distinct concept from the Desktop-local "any
object" favourites list.

## 13. Key Takeaways

The most valuable audit a project can run is re-reading its own
disclosed placeholders and asking, one by one, whether they are still
true. Building the visual surface to actually show real data is what
makes it worth asking that question in the first place — a dashboard
nobody looks at would never have found this Work Package's own central
finding.

## Related Documents

`ADR-0069`; `WP10.1A Implementation Report.md`;
`docs/academy/03 Work Packages/WP10.1A-engineering-cockpit-implementation.md`;
`19-user-experience-and-desktop-application.md`;
`20-desktop-application-framework.md`; `TD-26`.
