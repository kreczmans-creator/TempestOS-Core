# WP 8.0C — Engineering Workspace UX Specification — Wireframe Sketches

## Purpose

Conceptual, technology-neutral sketches of the primary screens named in
`Screen Catalogue.md`. These are box-diagrams of *regions and their
relative proportions*, not pixel layouts, font choices, or a commitment
to any rendering technology — a box below is honoured equally by a
terminal region or a graphical panel. Per this Work Package's own
constraint, nothing here selects implementation technology.

## 1. Engineering Cockpit

```
+----------------------------------------------------------------+
| Command Bar: TempestOS  |  [Project: Bridge-7]  |  Search/Palette |
+----------------------------------------------------------------+
| Project header: Bridge-7   Health: Attention   Progress: 62%    |
+----------------------------------+-----------------------------+
| What Needs Attention             | Upcoming Milestones          |
|  - REQ-0042 blocked (2d)         |  - Design Review   Aug 12    |
|  - CALC-0018 failing validation  |  - Verification #3 Aug 20    |
+-----------------------------------------------------------------+
| Engineering Health Summary                                      |
|  Requirements: 120 (8 Draft, 4 Blocked)   Verification: 74%     |
|  Calculations: 34 (1 failing)             Risks: 3 open         |
+-----------------------------------------------------------------+
| Recent Activity                          | Recent Projects       |
|  - REQ-0042 revised (2h ago)             |  - Bridge-7 (now)     |
|  - CALC-0018 executed (5h ago)           |  - Turbine-2          |
+-------------------------------------------+---------------------+
| Status Bar: Ready | 3 Areas | Bridge-7                          |
+----------------------------------------------------------------+
```

## 2. Workspace Shell (General Screen)

```
+----------------------------------------------------------------+
| Command Bar                                                    |
+----------------+-----------------------------------+-----------+
| Project        | Document Area                      | Properties|
| Explorer       |  [Tab: REQ-0014] [Tab: CALC-0018]   |  / Inspec-|
|  Requirements  | ---------------------------------- |  tor      |
|   Group: Struct|  REQ-0014                           |           |
|    REQ-0014    |  Statement: The structure shall...  | Identifier|
|    REQ-0015    |  Status: Draft                       | REQ-0014  |
|  Materials     |                                      | Status    |
|  Calculations  |                                      |  Draft    |
+----------------+-----------------------------------+-----------+
| Status Bar: Ready | Requirements area | Bridge-7                |
+----------------------------------------------------------------+
```

**Panel proportions** (relative, not absolute): Project Explorer and
Properties/Inspector are narrow, fixed-minimum-width secondary panels;
Document Area consumes remaining width — restates `WP8.0A UI
Architecture.md` §1's own five-region layout with Properties/Inspector
merged into one dockable region that switches content by tab
(§Screen Catalogue §10).

## 3. Project Dashboard

```
+----------------------------------------------------------------+
| Breadcrumb: Cockpit > Bridge-7 Dashboard                        |
+----------------------------------------------------------------+
| Bridge-7   Owner: J. Reyes   Started: 2026-02-01                |
+-----------------------------------+-----------------------------+
| Requirements Status                | Verification Status         |
|  [bar: Draft/Reviewed/Verified]    |  [bar: Passed/Pending/Failed]|
+-----------------------------------+-----------------------------+
| Risks (3 open)                     | Open Actions (5)            |
+-----------------------------------+-----------------------------+
| Digital Thread Summary: 340 links, 12 orphaned requirements     |
+----------------------------------------------------------------+
```

## 4. Command Palette (Overlay)

```
        +----------------------------------------------+
        | > revi_                                       |
        +----------------------------------------------+
        | Commands                                       |
        |   Revise Requirement            (Requirements)  |
        |   Review Calculation  [disabled: no selection]  |
        | Navigate                                        |
        |   REQ-0014 - The structure shall...             |
        +----------------------------------------------+
```

Drawn as an overlay (floating above, not replacing, the current
screen) to make explicit the one rendering-feasibility question this
specification does not resolve (`UX Specification.md` §5): a terminal
can approximate an overlay (drawn last, restored on close) but does not
have a graphical framework's own native floating-window primitive —
named here, not decided.

## 5. Properties vs. Inspector Panel (Tabbed Region)

```
+---------------------------+
| [Properties] [Inspector]  |
+---------------------------+
| Identifier:  REQ-0014     |
| Status:      Draft        |
| Owner:       J. Reyes     |
| Created:     2026-01-15   |
+---------------------------+
```

```
+---------------------------+
| [Properties] [Inspector]  |
+---------------------------+
| Verified by: CALC-0018    |
| Derived from: REQ-0002    |
| Evidence: 3 documents     |
|  > jump to CALC-0018      |
+---------------------------+
```

## 6. Empty / Loading / Error States

```
Empty:                      Loading:                   Error:
+-------------------+       +-------------------+       +-------------------+
| No items yet.     |       | Loading...        |       | Could not load.   |
| [+ New Requirement]|       | [in-progress bar] |       | REQ-0014 evidence |
+-------------------+       +-------------------+       | fetch failed.      |
                                                          | [Retry]            |
                                                          +-------------------+
```

Restates `Screen Catalogue.md` §23-§25's own three states as sketches:
every empty state names its own primary action (never a bare "no
data"); every error state offers a retry or a clear next step, never a
dead end (Principle 9).

## Related Documents

`WP8.0C UX Specification.md`; `WP8.0C Screen Catalogue.md`;
`WP8.0A UI Architecture.md`; `WP8.0C Engineering Cockpit
Specification.md`.
