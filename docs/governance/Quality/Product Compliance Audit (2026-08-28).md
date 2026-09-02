# TempestOS — Product / UX / Functional Compliance Audit

**Date:** 2026-08-28 · **Branch:** `claude/finding-closure-verification-tbcd50` ·
**Baseline commit:** `ec81bf6` · **Version:** `0.13.1`

## Executive Summary — the honest answer

**PARTIALLY — and the shortfall is structural, not cosmetic.**

TempestOS today is an **excellent engineering *platform*** with a
**modest single-window desktop shell** in front of it. The platform layer
(runtime host, DI, modules, plugin trust boundary, engineering domain,
units/quantities, materials, calculations, requirements, verification,
persistence, audit) is genuinely strong, deeply tested (2,414 Core
tests), and matches its architecture documents.

The product shown in the five mock-ups is **not** the product that is
implemented. The mock-ups depict a project-centric, multi-module,
fully-dockable engineering environment with a companion mobile app. The
implementation is a **discipline-centric, single-window, fixed-three-dock
object browser/editor** with no project context, no mobile application on
this branch, and no drawing viewer.

The gap is not "polish outstanding". Five of the mock-ups' central
organising concepts — **Projects as a workspace, Tasks, Commercial,
a Drawing/Document viewer, and the Companion** — have **no implementation
at all** in the audited tree. This is the answer to the question the
brief poses ("would I open this in six months and say *this isn't the
system we designed*?"): **yes, on information architecture and on the
mobile/field half of the product.**

Two things are worth stating in the platform's favour, because they are
unusual and they matter: the codebase **does not fake anything** — there
are zero `TODO`/`FIXME`/`NotImplementedException`s, and placeholders are
explicitly *labelled as placeholders in the UI itself*; and the
governance registers largely **already disclose** these gaps as
`FCR`/`TD` entries rather than claiming false completeness. The product
is behind the design brief; the documentation is largely honest about it.

## Overall Compliance

| Measure | Count |
|---|---|
| Requirement areas audited (brief §3–§27) | 25 |
| Fully compliant | 6 |
| Partially compliant | 9 |
| Failed / not implemented | 10 |
| Intentionally superseded | 0 |
| New gap-register entries raised | 14 (`TD-70`–`TD-83`) |
| Fixed in this pass | 2 (`TD-70` responsive workspace, `TD-71` drag preference) |

## Method and evidence standard

Repository-wide reconstruction of the requirement set (README, ADRs 1–115,
architecture documents, all governance registers, 142 Academy Work Package
retrospectives, release notes, the full `src/` and `tests/` trees, and Git
history), then adversarial verification against **running code**. Where a
claim could be settled empirically it was: a headless probe booted the real
`MainWindow` over a real `WorkspaceHost` and measured the actual shell
composition and layout behaviour at seven window sizes. No finding below
rests on documentation alone.

---

## 1. Critical UX findings

### 1.1 Docking is a fixed three-dock grid, not a docking system — **PARTIAL (P1)**

`DockingGrid` is a 5-column × 3-row `Grid` with three `GridSplitter`s.
Panels are assigned to a dock **once**, at composition time
(`SetLeftPanel`/`SetRightPanel`/`SetBottomPanel`), and can never move.

| Capability (brief §3.1) | Status | Evidence |
|---|---|---|
| Resizable panels | **PASS** | `GridSplitter` per dock; `DockingGridTests` |
| Collapsing panes | **PASS** | `SetLeftCollapsed` → 32 px strip |
| Reopening closed panes | **PASS** | View menu; width preserved |
| Auto-hide / flyout "peek" | **PASS** | `ShowFlyout`, tested |
| Layout persistence across restart | **PASS** | `DesktopPanelUiState` + `WorkspaceState` |
| Workspace presets | **PASS** | 3 presets (Engineering/Review/Documentation) |
| **Movable panels (drag to re-dock)** | **NOT IMPLEMENTED** | no drag-dock code anywhere |
| **Tabbed panel groups** | **NOT IMPLEMENTED** | one panel per dock, fixed |
| **Floating panels/windows** | **NOT IMPLEMENTED** | `WorkspaceDockPosition` has no `Floating` member — deliberate (`ADR-0095`) |
| **Panel splitting / new dock regions** | **NOT IMPLEMENTED** | grid geometry is a compile-time constant |
| **Panel reordering** | **NOT IMPLEMENTED** | — |

**Verdict: it is a real layout system, honestly built and tested, but it
is not a docking framework.** The project already tracks the missing half
as **`FCR-0064`** (floating/multi-monitor placement contract extension,
*Identified, Unscheduled*). Raised here as **`TD-72`** so it has a
severity, not only a roadmap slot.

### 1.2 Responsive/space-efficient UI — was **FAIL**, now **PARTIAL (fixed this pass)**

Measured on the real window before the fix: **no `SizeChanged` handler,
no `Bounds` observation, no breakpoint, no compact mode anywhere in
`Tempest.Desktop`.** Both side docks were `ColumnDefinition(240, Pixel)`
— fixed — so at the application's own `MinWidth` of 960 px they consumed
**50% of the window**. The ribbon was a fixed 145 px tall with no
minimise affordance.

**Fixed in this pass (`TD-70`):**
- `DockingGrid.ApplyResponsiveLayout` observes `Bounds` and squeezes side
  and bottom docks so the Document Area never falls below 420 × 220 px;
  below a readable width a dock collapses to its strip rather than
  becoming an unusable sliver. **The user's preferred sizes are never
  overwritten** — they are restored in full when the window grows back.
- `RibbonView.SetCollapsed`/`ToggleCollapsed` minimises the ribbon to its
  tab strip (double-click a tab, or **View ▸ Minimise Ribbon**),
  persisted across restarts. Every tab header stays reachable, so no
  command is lost.
- 12 new tests; three mutations run and killed (clamp disabled → 5
  failures; ribbon collapse no-op → 2; drag-preference removal → 1).

**Still outstanding (`TD-73`):** the navigation/ribbon do **not** switch to
compact/icon-only presentations, and `MinWidth`/`MinHeight` of 960 × 600
still bars genuinely small displays. The ribbon degrades by *scrolling*,
not by *compacting*.

### 1.3 No global navigation architecture — **FAIL (P0)**

The mock-ups' left rail (Home · Projects · Tasks · Engineering ·
Documents · Commercial · Resources · Reports · Knowledge ·
Administration) **does not exist**. There is no navigation rail control in
the codebase.

What the running application actually offers (probe output):

- Menu bar: `_View · _Document · _Theme · _Commands · _Help`
- Ribbon tabs: `Calculations · Documents · Manufacturing · Mechanical ·
  Requirements · Sample · Verification` — **engineering disciplines**, not
  the mock-ups' command groups
- Explorer "areas": the same disciplines plus `Home`, `Sample Objects`,
  `Settings`

The mock-ups' three-level model (**Global module → Project → Engineering
Workspace**) is collapsed to a **single level**: swap the Project
Explorer's tree. Raised as **`TD-74`**.

Note also: `Sample Objects` and a `Sample` ribbon tab are **visible in the
shipped desktop product** — `Tempest.Samples`' 33 fictional modules ship
to end users because `Tempest.Desktop → Tempest.App → Tempest.Samples`
(**`TD-75`**).

### 1.4 There is no project context — **FAIL (P0)**

This is the single largest divergence from the mock-ups, all five of
which are project-framed.

- `Project` exists as an *engineering object Kind* inside the Mechanical
  discipline tree (`Portfolio → Programme → Project`), reachable only as
  a tree node.
- There is **no current-project concept**: no open/switch, no
  project-scoped workspace, none of the mock-up's project tabs
  (Overview · Tasks · Engineering · Documents · Requirements · Risks ·
  Timeline · Reports · Settings).
- `StatusBarView.SetProject` is called **exactly once — by its own
  constructor, with `null`.** The shipped status bar permanently reads
  **"📁 No project"**. Verified on the running window.
- The `ProjectModel`/`ProjectService`/`IProjectRepository`/
  `JsonProjectRepository` cluster is **dead code** — referenced by nothing
  in `Tempest.App` or `Tempest.Desktop` (part of the long-deferred
  `TD-01` bootstrap cluster).

Raised as **`TD-76`** (P0).

### 1.5 Command Palette is a search box, not a command framework — **PARTIAL (P2)**

96 command descriptors are registered; **only 22 set `CreateDefault`**, and
all 22 are in `Tempest.Samples`. For the other ~74 — every real discipline
command — the palette raises `CommandUnavailable` and reports that the
command "needs a selected object or additional input".

It is honest (that honesty was a deliberate `WP 10.3B` fix) but it is not
the global contextual command framework the brief describes: there are no
contextual commands for the current object, no recent-commands list, and
no command can be *completed* from the palette. **`TD-77`**.

### 1.6 Multi-monitor / multi-window — **NOT IMPLEMENTED (P1, ADR-deferred)**

Exactly one `Window` subclass exists (`MainWindow`); there are no
secondary windows, no `ShowDialog`, no floating hosts. The `Screens` API
is used solely to validate that a restored window position is on-screen.

This is a **documented deferral**, not an accident: `WP8.0A Workspace
Architecture Document.md` §"Deliberately Out of Scope" states "**No
multi-window support — deferred, not designed**", and `FCR-0064` tracks
the contract extension. It nonetheless directly contradicts brief §5.
**`TD-72`** (with §1.1) — needs an explicit product decision.

### 1.7 Accessibility — **FAIL (P1)**

`AutomationProperties` appears **zero times** in `Tempest.Desktop`. Every
icon-only button (📌 ✕ ◀ ▶ 🕐 ⭐) and every watermark-only text box is
unnamed to a screen reader. The five `Border`-based dialogs are not
modal, never move or trap focus, and mostly lack Escape/Enter. All
asynchronous feedback (status bar, toasts, every editor status line) is
visual-only. The Digital Thread graph is pointer-only. The declared
`FocusRing` design tokens are consumed by **no style**. Already registered
in the previous pass as **`TD-65`**; re-confirmed here, unchanged.

### 1.8 Brand design system is absent from the Desktop — **FAIL (P2)**

None of Royal Blue `#1E2F97`, Electric Blue `#00AEEF`, or Purple
`#6C2BD9` appears anywhere in `src/`. No `FontFamily` is set at all — no
Chakra Petch, Inter, or Space Mono. The palette is Fluent default
(`#F5F5F5`/`#2A2A2E`/`#0067C0`).

The brand system **does exist** — it was built for the Companion on the
unmerged `claude/tempestos-companion-mobile-ubznt3` branch
(`BrandPalette.cs`, `Assets/Fonts/ChakraPetch-*.ttf`,
`SpaceMono-*.ttf`). It was never applied to the Desktop. **`TD-78`**.

---

## 2. Functional findings

### 2.1 Engineering Workspace (mock-up 2) — **PARTIAL (P1)**

The strongest platform area has the weakest dedicated UI. Domain services
exist in depth; **dedicated desktop views do not**:

| Mock-up surface | Core/App files | Desktop view |
|---|---|---|
| Calculations | 19 | **0** |
| Materials | 17 | **0** |
| Validation | 11 | **0** |
| Units | 11 | **0** |
| Standards / Reference Data / Profiles / Loads / Environments | 0–6 | **0** |
| Compare / Optimization / Sensitivity / Math Tools | 0 | **0** |
| Output console · Messages/Warnings/Errors tabs | — | **0** |

What exists instead is one generic `ObjectEditorView` with per-Kind
`Expander` sections (Identity, Content, Lifecycle, Relationships,
Validation, BOM, Owner/Priority, Execute, Record Result, Attachments)
plus an Output panel. That is a real, working engineering editor — but it
is not the multi-pane engineering cockpit of the mock-up. **`TD-79`**.

### 2.2 No drawing or document viewer — **NOT IMPLEMENTED (P0)**

Mock-ups 2 and 3 both centre on a drawing viewer with zoom/pan/rotate/
markup/annotation. Search across `Tempest.Desktop` for image, PDF, DWG,
SVG or bitmap rendering returns **nothing**. `ZoomBy`/`PanBy` exist **only**
in the Digital Thread *graph*. Attachments are metadata-only records
(filename, content type, size — no bytes, `TD-31`). A user cannot open a
drawing in TempestOS. **`TD-80`** (P0).

### 2.3 Whole mock-up modules absent — **NOT IMPLEMENTED (P0/P1)**

Search across `Tempest.Core` + `Tempest.App` + `Tempest.Desktop`:

| Concept | Implementation |
|---|---|
| Tasks (engineering/project tasks) | **none** — only `BackgroundTaskRunner` (background *jobs*) |
| Commercial (invoices, quotes, cashflow, budget) | **none** |
| Resources / workload | **none** |
| Knowledge / Academy in-app | **none** |
| Administration | **none** |
| Kanban board | **none** |
| Timeline / Gantt | **none** |
| Milestones as a managed surface | domain only, cockpit text lines |
| Risks / Issues / Decisions | domain objects only, **no UI surface** (`FCR-0056`) |

**`TD-81`** (P0 for Tasks; P1 for the rest).

### 2.4 Companion / mobile — **NOT IMPLEMENTED on this branch (P0)**

Mock-ups 3 (tablet SOP/drawing/checklist, offline) and 4 (phone home)
have **zero implementation** in the audited tree: no `Companion`,
`Mobile`, `Android` or `iOS` files in `src/` or `tests/`.

They exist only on the **unmerged** branch
`claude/tempestos-companion-mobile-ubznt3` (`WP 14.0A`/`14.1A`/`14.2A`),
which also carries the offline snapshot cache, the Companion REST surface,
and the brand pack. Nothing of that is on `main` or on this branch.

Consequently **Journey E (site engineer) cannot be traced at all here**,
and the desktop/mobile responsibility split (brief §15) cannot be
assessed on this branch. **`TD-82`** (P0) — plus a **numbering collision**:
that branch independently allocated `TD-57`/`TD-58` for different items
(already recorded in `TD-57`).

### 2.5 Cockpit / Home — **PASS with caveats (P3)**

The genuine bright spot. 40 card controls render over 15 real card types
(Welcome/Continue, Recent Projects, Favourite Projects, What Needs
Attention, Open Decisions, Blocked Items, Overdue Actions, Project Health,
KPI cards, Digital Thread, Risk Summary, Upcoming Milestones, Recent
Activity, Workspace Status, Quick Actions) — closely matching mock-up 1's
*intent*, driven by real domain reads, with placeholders explicitly
labelled "(placeholder)" rather than faked. Missing vs mock-up:
Commercial Snapshot, Tempest AI panel, global search, notification/message
centre. Cards remain expensive to rebuild (`TD-66`).

---

## 3. Mock-up compliance report

| Area | Mock-up intent | Current implementation | Status |
|---|---|---|---|
| **1. Home cockpit** | Attention cards, health donut, milestones, tasks, activity, system status, AI, jump-in | 15 card types over real domain data | **PARTIAL** |
| Global nav rail (10 modules) | Persistent left rail | none | **NOT IMPLEMENTED** |
| Global search (⌘K) | Header search | Command Palette only (Ctrl+K) | **PARTIAL** |
| Commercial snapshot | Invoices/quotes/cashflow | none | **NOT IMPLEMENTED** |
| Tempest AI panel | Suggestions, prompt | none | **NOT IMPLEMENTED** |
| Notifications/messages/help | Header cluster | status-bar segment only | **PARTIAL** |
| **2. Engineering workspace** | Multi-pane calc cockpit | generic object editor + output panel | **PARTIAL** |
| Ribbon (New/Open/Save/Run/Validate/Calculate/Solve/Batch/Export/Report) | Command groups | 7 **discipline** tabs, derived glyphs | **PARTIAL** |
| Calculation editor/results/validation | Code editor + results grid | none | **NOT IMPLEMENTED** |
| Materials/Standards panels | Tables | none (domain exists) | **NOT IMPLEMENTED** |
| Drawing viewer | Zoom/pan/markup | none | **NOT IMPLEMENTED** |
| Output console + Messages/Warnings/Errors | Tabbed console | Output panel (diagnostics/tasks/history) | **PARTIAL** |
| Pinned items | Sidebar pins | none | **NOT IMPLEMENTED** |
| **3. Tablet field app** | SOP/checklist, drawing markup, photos, offline | **nothing on this branch** | **NOT IMPLEMENTED** |
| **4. Phone home** | Tasks, projects, activity | **nothing on this branch** | **NOT IMPLEMENTED** |
| **5. Project workspace** | Project header, tabs, progress, milestones, health, risks, budget, Kanban, workload, Gantt, documents, approvals | **no project workspace exists** | **NOT IMPLEMENTED** |
| Docking/panels (all mock-ups) | Movable, tabbed, floating | fixed 3 docks, resize/collapse/auto-hide | **PARTIAL** |
| Visual language (all mock-ups) | Navy/electric-blue, Chakra Petch | Fluent defaults, no brand tokens | **FAIL** |

---

## 4. Real user journeys traced

| Journey | Result |
|---|---|
| **A — Engineer starts work** (home → project → task → workspace → calculation → validation → save) | **BREAKS AT STEP 2.** No project to open, no tasks. Achievable instead: Home → discipline area → object → editor → validation section → Save. |
| **B — Multi-pane engineering** (calc + drawing + materials + results, rearrange, dock, restart) | **PARTIAL.** Resize/collapse/auto-hide/preset/restart-persistence all work. Cannot open a drawing, a materials pane, or a results pane; cannot re-dock or float anything. |
| **C — Project management** (tasks → Kanban → timeline → risk → document → approval) | **NOT POSSIBLE.** None of Kanban, timeline, task, or approval surfaces exist. |
| **D — Drawing review** (zoom/pan/markup → comment → approval) | **NOT POSSIBLE.** No viewer. |
| **E — Site engineer** (mobile SOP → photo → offline → sync) | **NOT ASSESSABLE** on this branch — no Companion. |
| **F — Command-driven** (shortcut → palette → locate → execute) | **PARTIAL.** Ctrl+K opens, search/arrows/Enter work; ~74 of 96 commands cannot execute from it. |
| **G — Responsive** (progressively reduce width) | **WAS FAIL, NOW PARTIAL.** Measured before fix: side docks fixed at 240 px each (50% of a 960 px window), ribbon fixed at 145 px, no adaptation. After fix: docks squeeze and then collapse to strips, Document Area floor guaranteed, ribbon minimisable. Compact/icon-only nav and sub-960 px windows still unsupported (`TD-73`). |

---

## 5. Architectural findings

1. **Sample harness ships in the product** (`TD-75`) — `Tempest.Desktop →
   Tempest.App → Tempest.Samples`; 33 fictional modules and a "Sample"
   ribbon tab are user-visible. Needs a split between the discipline
   registration layer and sample content.
2. **The docking geometry is a compile-time constant** (`TD-72`) — floating,
   re-docking and tab-groups cannot be added without extending
   `WorkspaceDockPosition`/`WorkspacePanelPlacement` (`FCR-0064`) and
   replacing the fixed `Grid`. This is the architectural correction that
   must precede any "real docking" work; patching the current grid will not
   get there.
3. **No project aggregate** (`TD-76`) — nothing in the live domain owns
   tasks/documents/risks/commercial data as a project. Every mock-up
   surface that is missing traces back to this one absence. This is the
   highest-leverage architectural decision outstanding.
4. **Dead bootstrap cluster still shipping** (`TD-01`, re-confirmed) — ten
   public types including the entire `ProjectModel` family, referenced by
   nothing.
5. **ASP.NET framework reference is assembly-wide** — the Avalonia desktop
   app links the whole web shared framework because `Tempest.Core.Api` is a
   namespace, not a project.

---

## 6. Test coverage — what is actually proven

**Proven:** platform/domain correctness (2,414 Core tests, strong
mutation resistance in the plugin-trust chain); docking resize/collapse/
hide/flyout/presets (26 tests); ribbon dispatch and refresh counts;
responsive clamping and ribbon minimise (12 new, 3 mutations killed);
corrupted-state recovery.

**Not proven (because not implemented):** drag-to-dock, floating, panel
tabbing, multi-monitor, compact/icon navigation, project context, drawing
viewing, offline/sync, mobile.

**Implemented but under-proven (`TD-83`):** no test drives a real window
resize end-to-end through `MainWindow` (the new tests drive `DockingGrid`
directly); dialog focus/modality untested; `TD-63`/`TD-64` closure-test
gaps from the previous pass remain open.

---

## 7. Academy / documentation

Academy coverage is **good for what was built** — dedicated articles exist
for the desktop framework, docking and layouts, ribbon and command
experience, object editors, cockpit, digital thread, and workflow
(142 Work Package retrospectives).

**Missing:** an article for the responsive workspace behaviour added in
this pass (**written here**: `docs/academy/02 Runtime Architecture/
32-responsive-workspace-and-ribbon-minimisation.md`); and — necessarily —
no article for project workspace, drawing viewing, or mobile/offline,
none of which exist.

---

## 8. Master gap register

Severity: **P0** fundamental product failure · **P1** major missing
capability · **P2** significant UX/functional deficiency · **P3** minor ·
**P4** future enhancement.

| ID | Requirement | Source | Current state | Sev | Action |
|---|---|---|---|---|---|
| `TD-70` | Workspace adapts to available space | Brief §4 | **FIXED this pass** — dock squeeze/collapse floor, ribbon minimise, persisted | P1 | Closed |
| `TD-71` | A splitter drag is a durable preference | Audit | **FIXED this pass** | P3 | Closed |
| `TD-72` | Movable/floating/tabbed docking; multi-monitor | Brief §3.1, §5; `FCR-0064` | Fixed 3-dock grid; single window | P1 | Product decision, then contract extension + host replacement |
| `TD-73` | Compact/icon-only nav + ribbon; sub-960 px windows | Brief §4 | Not implemented; ribbon scrolls, never compacts | P2 | Compact presentation pass |
| `TD-74` | Global navigation architecture (10 modules) | Mock-ups 1–2, 5 | No nav rail; single-level discipline switching | P0 | Design global IA |
| `TD-75` | Sample harness must not ship in the product | Brief §23; `ADR-0101` | 33 sample modules + "Sample" tab user-visible | P1 | Split registration from sample content |
| `TD-76` | Project as a first-class workspace concept | All 5 mock-ups | No project context; status bar permanently "No project" | P0 | Project aggregate + workspace |
| `TD-77` | Global contextual command framework | Brief §6 | 22 of 96 commands invocable; no contextual commands | P2 | Parameter-binding/context contract |
| `TD-78` | TempestOS brand design system on Desktop | Brief §22 | Zero brand colours/fonts; Fluent defaults | P2 | Port `BrandPalette`/fonts from Companion branch |
| `TD-79` | Engineering Workspace multi-pane surfaces | Mock-up 2 | Generic editor only; no calc/materials/standards/tools panes | P1 | Per-discipline workspace views |
| `TD-80` | Drawing/document viewer | Mock-ups 2, 3 | None; attachments are metadata-only | P0 | Viewer + attachment content storage |
| `TD-81` | Tasks, Commercial, Resources, Knowledge, Admin, Kanban, Timeline, Risk/Issue UI | Mock-ups 1, 5 | Domain objects only or nothing | P0/P1 | Per-module programme |
| `TD-82` | Companion (mobile/field) application | Mock-ups 3, 4 | Absent on this branch; unmerged elsewhere | P0 | Merge/reconcile `WP 14.x` (incl. `TD-57`/`TD-58` collision) |
| `TD-83` | End-to-end window-resize and dialog-modality tests | Brief §18 | Not covered | P2 | Add `MainWindow`-level layout/dialog tests |

Carried forward, unchanged, from the 2026-08-28 closure pass: `TD-57`,
`TD-61`–`TD-69`.

## 9. Non-negotiable compliance matrix

| Non-negotiable | Original requirement evidence | Implementation | Tests | Mock-up match | Status | Remaining |
|---|---|---|---|---|---|---|
| Fully dockable workspace | Brief §3.1; `WP8.0A` §7 | Fixed 3-dock grid; resize/collapse/auto-hide/presets/persistence | 26 | Partial | **PARTIAL** | `TD-72` |
| Responsive/space-efficient UI | Brief §4 | Dock squeeze + collapse floor; ribbon minimise (this pass) | 12 (+3 mutations) | Partial | **PARTIAL** | `TD-73` |
| Multi-monitor engineering | Brief §5; `FCR-0064` | Single window; `ADR`-deferred | 0 | No | **NOT IMPLEMENTED** | `TD-72` |
| Command Palette as a framework | Brief §6; `ADR-0070` | Search + invoke for 22/96 | 2 | Partial | **PARTIAL** | `TD-77` |
| Coherent navigation architecture | Brief §7; mock-ups | Single-level discipline switching | indirect | No | **FAIL** | `TD-74` |
| Engineering Workspace | Brief §8; mock-up 2 | Generic editor + output | 14 | Partial | **PARTIAL** | `TD-79` |
| Domain-backed UI (not disconnected screens) | Brief §9 | Genuinely domain-backed throughout | 2,414 | Yes | **VERIFIED PASS** | — |
| Project Cockpit / Home | Brief §10; mock-up 1 | 15 real card types, honest placeholders | 17 | Partial | **PARTIAL** | `TD-81` |
| Project workspace | Brief §11; mock-up 5 | None | 0 | No | **NOT IMPLEMENTED** | `TD-76` |
| Drawing/document viewer | Brief §12; mock-ups 2–3 | None | 0 | No | **NOT IMPLEMENTED** | `TD-80` |
| SOP/checklist workflow | Brief §13; mock-up 3 | None on this branch | 0 | No | **NOT IMPLEMENTED** | `TD-82` |
| Mobile/Companion | Brief §14; mock-ups 3–4 | None on this branch | 0 | No | **NOT IMPLEMENTED** | `TD-82` |
| No placeholders/fake data | Brief §23 | Zero TODO/FIXME/NotImplemented; placeholders labelled in UI | — | Yes | **VERIFIED PASS** | — |
| Accessibility & keyboard operation | Brief §19 | No `AutomationProperties`; non-modal dialogs; graph pointer-only | 0 | No | **FAIL** | `TD-65` |
| Error/empty/loading states | Brief §21 | `EmptyStateView`, toasts, dialogs, corrupted-state recovery | 22 | Yes | **VERIFIED PASS** | — |
| Visual design system | Brief §22 | No brand colours/fonts | 0 | No | **FAIL** | `TD-78` |
| Auditability/provenance | Brief §26 | Audit framework, revisions, principal attribution | strong | Yes | **VERIFIED PASS** | — |
| Academy documentation | Brief §27 | Complete for what exists; new article added this pass | — | Yes | **VERIFIED PASS** | — |

## 10. Recommended remediation order

1. **Decide the product shape** (`TD-76`, `TD-74`, `TD-82`): is TempestOS
   the project-centric multi-module system of the mock-ups, or the
   discipline-centric engineering platform that exists? Everything else
   follows. This is the smallest decision that unblocks the most work,
   and it is genuinely the Product Owner's, not an engineering call.
2. **Merge or formally reject the Companion branch** (`TD-82`) —
   including the `TD-57`/`TD-58` numbering reconciliation. It is finished
   work sitting outside the product.
3. **Drawing viewer + attachment content** (`TD-80`) — the highest-value
   single capability; two mock-ups depend on it.
4. **Docking contract extension** (`TD-72`) — architectural; must precede
   any further layout work.
5. **Accessibility pass** (`TD-65`) and **brand pass** (`TD-78`) — both
   bounded, mechanical, and high-visibility.
