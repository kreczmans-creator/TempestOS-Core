# v0.14.0 — Work Packages

## Scope of this document

Every Work Package and standalone change in the `v0.13.1..HEAD` range,
**derived from `git log`**, not from any prior summary. The range held
**52 commits** between the `v0.13.1` tag (`6eca5fde`) and `44c4701`, the
head at which this inventory was derived; **`WP-Z4`'s own
release-preparation commit makes 53**. Every figure below is measured at
`44c4701` unless stated otherwise.

Two facts this document exists to state plainly:

1. **The fifteen-Work-Package remediation programme is a minority of this
   release.** Roughly two-thirds of the range is the engineering body of
   work that preceded it — durable object state, attachments, the document
   viewer, the workspace layout tree, the Product Spine, project
   management surfaces, and the five-stage `TD-77` command-contract
   migration.
2. **`WP 13.13.2` belongs here.** It closed `v0.13.1`, but was merged to
   `main` *after* the `v0.13.1` tag, so it ships first under this release
   and is accounted for below.

Status vocabulary follows `WP 12.2A`'s precedent: **Complete**,
**Delivered by** (a disclosed disposition), or **Not started — out of this
release's scope**. No Work Package in the range is in an undocumented
state.

---

## A. Carried from the `v0.13.x` train

| Work Package | Commits | Status | Notes |
|---|---|---|---|
| WP 13.13.2 — `v0.13.1` Release Closure | `194b067`, merge `00f7f39` | **Complete** | Documentation-only. Corrected the Release Register's `v0.13.1` row from "In preparation" to Released/published, with tag, merge, CI, workflow and asset evidence; recorded the `v0.13.x` train as CLOSED. Retrospective written by `WP-Z4` — see §D. |

## B. Engineering body of work (pre-programme, this release)

Delivered as topic-scoped changes rather than numbered Work Packages.
Each row names the debt it closed or the capability it added.

| Change | Commits | Status | Notes |
|---|---|---|---|
| `TD-59` + `TD-60` — reserved-name-safe persistence boundary; controlled malformed-value reads | `dc46210`, `159d862` | **Complete** | The recovery contract `WP-D2` later consolidated. |
| `TD-58` — outcome-gated refresh architecture | `6135c7f` | **Complete** | The rule `WP-D1`'s `ActionOutcomeReporter` later gave one implementation. |
| Governance — independent finding-closure verification, register closure, false-claim corrections | `ec81bf6` | **Complete** | |
| `TD-70` + `TD-71` — responsive workspace, ribbon minimisation, drag as a durable preference | `e40a3d6` | **Complete** | |
| `TD-84` — the Product Spine: Module → Project → Workspace as persisted state | `37788a0` | **Complete** | |
| `TD-85` — durable engineering object state, per-type rehydration, removal of `Projects.Index` | `e752368`, `fa01c63` | **Complete** | `ADR-0113`. Closure audit corrected `ReviseAsync` to carry the whole object. |
| `TD-89` — project-centric convergence: one definition of project membership | `cd26b8f` | **Complete** | |
| `TD-72` — data-driven workspace layout replacing the compile-time docking grid | `76a520b`, `6e38948` | **Complete** | `ADR-0095`. Verified against the running application. |
| CI — close the build errors and governance-index gaps that made the branch red | `7b53ce6` | **Complete** | `.github/workflows/ci.yml`, the only CI change in this release. |
| `TD-93` — vocabulary consistency check scanned whatever happened to be loaded | `f2600d9` | **Complete** | |
| CI — make a red run name the tests that failed | `416f2c8` | **Complete** | |
| Windows delete-while-locked test fix | `44648ed` | **Complete** | Test-only. |
| `TD-31` — attachment content is durable bytes this platform holds | `3715aa8` | **Complete** | `ADR-0114`. |
| `TD-80` — the document and drawing viewer | `0faf6ab`, `3f68376` | **Complete** | `ADR-0115`. Second commit records what the tests could not see. |
| `TD-102` — the two project areas that claimed to be implemented now are | `4bd6140` | **Complete** | |
| Production rehydration and the principal boundary | `671a18b` | **Complete** | `ADR-0116`. |
| Project Tasks — the first real project-management surface | `007aec2` | **Complete** | `ADR-0117`. |
| Product Gap Reconciliation audit | `187180a` | **Complete** | Findings and standing evidence. |
| `TD-75` — the product no longer ships inside its own demo harness | `a5795bd`, `fdd2a2a` | **Complete** | Two phases; the harness is now deletable. |
| Academy guide — sample explorer content's new home | `41a171a` | **Complete** | |
| Project Risks, Issues & Decisions — the governance families get a surface | `45d8a99` | **Complete** | |
| `PROJECT_STATUS.md` dangling-path fix | `e57f1aa` | **Complete** | |
| xUnit2029 build failure and the vacuous assertion behind it | `562a563` | **Complete** | |
| Project Timeline — milestones, deliverables, and the work behind each date | `f9fd9e0` | **Complete** | |
| `TD-77` Stage 2 — Core command context and binding contract | `6e3d6d5` | **Complete** | |
| `TD-77` Stage 3 — production command descriptors are bound | `bb22983` | **Complete** | |
| `TD-77` Stage 4 — Core invocation contract, proven against real bindings | `1c38cb4` | **Complete** | |
| `TD-77` Stage 5 — three surfaces consume the binding contract | `e72b933` | **Complete** | The migration `WP-A1`/`WP-A2` later finished. |
| Governance — the architecture audit becomes tracked debt; `TD-01`'s trigger fires | `b796b9d` | **Complete** | Raised `TD-105`–`TD-115`, commissioning the remediation programme. |

## C. The remediation programme (twelve Work Packages)

| Work Package | Commits | Status | Notes |
|---|---|---|---|
| WP-C — Delete the Retired v0.1 Architecture | `dfa6ee1`, `7e28f74` | **Complete** | Nine types removed; closes `TD-01`. Second commit is `WP-C`'s completion (`ApplicationConfiguration`, `AT-20`). |
| WP-B1 — Pin the Two Encodings of Per-Kind Command Eligibility | `25de7a3` | **Complete** | Test-only; directional invariant. |
| WP-D2 — One Settings Document, and Corruption That Leaves a Trace | `171dc68` | **Complete** | Nine stores consolidated; closes `TD-112`. |
| WP-A1 — Close the Live Id-Only Command Path | `b3a6c7e` | **Complete** | Closes `TD-106`, `TD-113`. Found a fourth broken surface the audit missed. |
| WP-H — Enforce the Architectural Invariants Nothing Was Holding | `9c25223` | **Complete** | Five new invariant test classes; eight already-held left alone. |
| WP-REVIEW — Clean-Machine and Physical-Review Readiness | `a13d1c3` | **Complete** | Produced `PHYSICAL_REVIEW.md`; found `TD-116`. |
| WP-D1 — One Desktop Report-Then-Refresh Tail | `0ced2c5` | **Complete** | Closes `TD-111`. |
| WP-F — Test-Suite Hygiene | `3a9b777` | **Complete** | Closes `TD-114`; two of four findings corrected as wrong. |
| WP-B2 — Kind Eligibility Is Two Mechanisms, One Invariant | `464bdff` | **Complete** | `ADR-0118`; closes `TD-107`. Documentary. |
| WP-G — The Project CRUD Leaves MainWindow, Verbatim | `0a9e49b` | **Complete** | `TD-109` partially resolved; `TD-112` recorded. |
| WP-A2 — The Keyboard Reaches the Canonical Path | `7de6290` | **Complete** | Closes `TD-105`, `TD-106`. `AT-10` reclassified. |
| WP-E — Async/Threading Hardening and the Cockpit Read Scope | `e4bc3ee` | **Complete** | `TD-108` narrowed; raised `TD-117`, `TD-118`, `AT-26`. |

## D. Release-preparation Work Packages

| Work Package | Commits | Status | Notes |
|---|---|---|---|
| WP-Z1 — Governance Correction | `05a4218` | **Complete** | Corrected `TD-108`'s split, normalised eight Status cells, re-derived two registers. |
| WP-Z2 — Undo/Redo UI-Thread Marshalling | `121efea` | **Complete** | `ADR-0119`; resolves `TD-117`. |
| WP-Z3 — Programme Academy Retrospective Completion | `44c4701` | **Complete** | Fifteen retrospectives written, indexed and registered. |
| WP-Z4 — Release Preparation | *this commit* | **Complete** | This document, the Release Notes, the Engineering Release Report, `VERSION`, the Release Register row, `WP 13.13.2`'s retrospective, and the `PHYSICAL_REVIEW.md` count correction. |

## E. Not started — out of this release's scope

| Item | Disposition |
|---|---|
| Full async conversion of the Cockpit read surface | `TD-118` — deferred by approved decision; revisit trigger is persistence becoming slow or remote. |
| Remaining `MainWindow` shell services | `TD-109` — Open, partially resolved. A different question from the row's stated defect. |
| REST activation | `AT-10` — a decided position, not a deferral. Needs a request-to-context contract, a parameter source, and an authentication model. |
| Default keyboard bindings, binding persistence, remapping UI | `AT-23` — feature work. |
| `IWorkspaceViewFactory.Create` async conversion | `AT-26` — deliberate survivor. |
| `TD-116` Linux/X11 launch | Open, platform-conditional. See the Release Notes' known limitations. |
| Retrospectives for pre-`v0.14.0` gaps (the ~34 pre-programme commits above, and `v0.11.0`'s ten Work Packages) | **Disclosed, not addressed.** Recorded in `WP-Z3`'s retrospective §12. Out of this release's approved scope. |
