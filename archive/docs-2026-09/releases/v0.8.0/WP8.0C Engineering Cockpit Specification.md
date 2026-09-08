# WP 8.0C — Engineering Workspace UX Specification — Engineering Cockpit Specification

## Purpose

The full definition of the Engineering Cockpit — the Workspace's own
default landing screen (`ADR-0069`) and, per every persona's own
journey (`User Journey Maps.md`, "Cross-Journey Observations"), the
one screen every workflow in the product returns to. This document is
the authoritative specification the controlling instruction's own
"Fully define the Engineering Cockpit discussed during product
planning" line calls for.

## 1. Purpose and Placement

The Cockpit answers, at the *programme* level, the same three questions
every screen must answer (Principle 9): what am I looking at (this
project's own current state, as a whole), what needs attention
(surfaced first, above the fold), what should I do next (a clear path
into any area). It is not a Home page in the sense of a static welcome
screen — it is a live, data-driven dashboard, reflecting current
Engineering Core state on every visit, not a cached snapshot.

**Today:** does not exist — WP8.1A's shell has no Cockpit; start-up
shows an empty Project Explorer only.

**Target:** the Cockpit is the first screen shown after start-up
(`Screen Catalogue.md` §1-§2, `ADR-0069`) and the destination of every
breadcrumb "home" segment (`Navigation Maps.md` §3).

## 2. Layout Regions

Restating `Wireframe Sketches.md` §1 in specification form, seven
regions, top to bottom:

1. **Project header** — project name, overall health status (the
   closed four-value vocabulary from `UX Specification.md` §3:
   `Healthy`/`Attention`/`Blocked`/`Unknown`), overall progress.
2. **What Needs Attention** (the Attention Centre's own home,
   `Screen Catalogue.md` §17) — a ranked list of items requiring
   action: blocked requirements, failing calculations, overdue
   verifications, open risks past a threshold. Each item opens directly
   to the relevant object in a new tab (the same universal jump
   pattern, unchanged).
3. **Engineering Health Summary** — aggregate counts across the four
   tracked disciplines named in the controlling instruction:
   Requirements status breakdown, Verification status breakdown,
   Calculation status breakdown, open Risk count.
4. **Digital Thread Summary** — aggregate link count and a specific,
   actionable callout for orphaned objects (requirements with no
   linked evidence, calculations with no linked requirement) — chosen
   because an orphan is exactly the kind of gap a summary count alone
   would hide.
5. **Project Progress** — a single overall completion measure,
   composed from existing status data (not a new tracked field) —
   definition left to the owning Engineering Core capability
   (Requirements/Verification), the Cockpit only displays it.
6. **Upcoming Milestones** — the nearest few dated items, if the
   project tracks any; absent gracefully (empty state, not an error)
   for a project that does not yet define milestones.
7. **Recent Activity** — a short, reverse-chronological feed of recent
   state changes across all tracked disciplines, each entry a jump
   target like every other cross-reference in the Workspace.

## 3. "What Needs Attention" — Ranking Rule

Since this region is the Cockpit's own most load-bearing part (it is
what every Project Manager journey opens the Cockpit specifically to
read, `User Journey Maps.md` §2), its own ranking rule is stated
explicitly rather than left implicit: **blocking items rank above
non-blocking items; within the same blocking tier, most-recently-changed
ranks first.** "Blocking" is derived from existing domain state (a
`Blocked` requirement status, a failing calculation validation, an
overdue verification) — the Cockpit introduces no new "attention"
flag of its own; it reads and ranks state that already exists.

## 4. Attention Centre vs. Notifications, Restated

`Screen Catalogue.md` §17 already draws this line; restated here since
the Cockpit is where it matters most: the Attention Centre region on
the Cockpit shows what is *still true right now* (a persistent query
over current state, re-evaluated on every visit) — it is not a feed of
past events, which is Recent Activity's own job (§2, above) and
Notifications' own separate, ephemeral surface (`Screen Catalogue.md`
§16, not part of the Cockpit's own layout).

## 5. Empty and New-Project States

A newly created project's own Cockpit is not a blank screen: each
region shows its own honest empty state (`Wireframe Sketches.md` §6) —
"No requirements yet — [+ New Requirement]," "No risks logged," etc. —
so the very first screen a new project shows already demonstrates,
without requiring the user to have entered data yet, what a healthy
Cockpit will eventually contain (Principle 5, progressive disclosure,
applied to a project's own lifecycle rather than only a single user's
learning curve).

## 6. Multi-Project Context

The Cockpit is always scoped to one project at a time (`Navigation
Maps.md` §5) — it is not a cross-project rollup. A user managing
several projects reaches each one's own Cockpit via project switching;
a cross-project view is explicitly out of scope for this specification
(no evidence from the named personas, `UX Specification.md` §2,
requires one — VISION.md's own target user is an individual engineer
or small practice, not a portfolio manager).

## 7. Relationship to the Project Dashboard

Distinguished explicitly, since both are dashboards and the distinction
is easy to blur: the **Engineering Cockpit** is the Workspace's own
single default landing screen, always the same shape, always for the
currently active project. The **Project Dashboard** (`Screen
Catalogue.md` §4) is a drill-down a Project Manager reaches deliberately
from the Cockpit (`User Journey Maps.md` §2) for a deeper, single-project
view — the Cockpit is the summary; the Dashboard is the detail.

## Related Documents

`WP8.0C UX Specification.md`; `WP8.0C Screen Catalogue.md` §2, §4,
§17; `WP8.0C User Journey Maps.md` §2; `WP8.0C Wireframe Sketches.md`
§1; `WP8.0C Navigation Maps.md` §2, §5; `ADR-0069`.
