# Independent Finding-Closure Verification — 2026-08-28

## Provenance, stated plainly

This pass was commissioned as a falsification review of a previous
"close every finding" pass against a 16-item independent review. **Neither
the previous closure report nor the original 16-item finding list exists
anywhere in this repository** — not on any branch, in any commit on any
remote ref, or in any pull request. The prior working sessions' outputs
were evidently never committed or pushed, and are unrecoverable. Three of
the original findings survived in the commissioning instruction itself,
with their working numbers and full descriptions (`TD-58` redundant
rebuilds, `TD-59` reserved device-name uniqueness, `TD-60` malformed
index values); the remaining thirteen could not be enumerated.

Consequence, applied honestly rather than papered over:

- **No prior closure claim was trusted or carried forward.** Every
  closure verified here was verified against source, from scratch.
- **The un-enumerable findings were re-derived** by a fresh, full-breadth
  independent review (below): any original finding whose defect still
  exists in source should have been rediscovered by it; anything it found
  is dispositioned in this report — fixed, or registered as Open with its
  number, never re-worded into closure.
- A parallel Companion work stream (branch
  `claude/tempestos-companion-mobile-ubznt3`) has independently allocated
  `TD-57`/`TD-58` on its own copy of the Technical Debt Register with
  different meanings. The collision is recorded in this branch's `TD-57`
  row for reconciliation at merge.

## Method

Eight fresh, read-only review agents ran in parallel, one per requested
discipline — closure verification, persistence/data integrity,
UI/threading/lifecycle, plugin/security boundaries, accessibility/UI
semantics, test/mutation quality, architecture/dependency, and
governance/source reconciliation — with every load-bearing claim
re-verified centrally against real source before anything was changed.
No competing implementations were run. Baseline before any change:
Core 2339/2341, Desktop 221/221 (the two failures are the documented
pre-existing Linux-environment cases: file-lock semantics and
case-insensitive-filename behaviour). After all changes:
Core 2412/2414 (same two), Desktop 228/228 — 80 new tests.

Closure discipline applied throughout: "registered", "documented",
"future", "currently unreachable", "third-party disabled", and "not
currently exposed" were **not** accepted as closure anywhere in this
report; each such disposition remains an Open register row.

---

## Part 1 — The three named findings

### TD-58 — Redundant rebuilds: **CLOSED**

1. **Did the defect exist in source?** Yes, in four independent shapes,
   traced hop by hop:
   - `RibbonView.RecordRecent` → `Rebuild()` tore down and rebuilt the
     entire tab tree on *every* command click (including failed ones),
     destroying keyboard focus and — on the delete path — running
     `RefreshEnablement` twice per click
     (`src/Tempest.Desktop/Views/RibbonView.cs`).
   - Every `ActionCompleted` fan-out refreshed unconditionally:
     `MainWindow`'s ribbon handler and `WorkspaceViewCoordinator`'s
     explorer/inspector/editor handlers reloaded the full Project
     Explorer and rebuilt the full Cockpit for pure refusals ("needs a
     selected object first"), failed commands, and non-mutating opens —
     always with a Success-severity toast, even for failures.
     `UndoRedoCoordinator` refreshed on failed undo/redo too. Only
     `RibbonObjectActionHandlers.ReportAsync` (ADR-0104) had the correct
     success-gated shape.
   - Duplicate paths: a Cockpit "Favourite Projects" open rebuilt all
     ~20 cards **twice** (inline `Refresh()` + the navigate path's own
     refresh).
   - Stale-UI inversions (refreshes that were missing): mutating macro
     runs and Command-Palette invocations refreshed no Explorer at all;
     a deleted object stayed selected (Delete/Rename still enabled, the
     Inspector still rendering it, a repeat delete dispatching against a
     dead Id); `BackgroundTaskRunner.Changed` had zero subscribers; and
     the platform-notification toast bridge listened only on the event
     bus, which no real producer publishes through — no platform
     notification ever reached a toast (confirmed dead wiring by
     whole-repository search).
2. **Does the execution path still exist?** Yes — same views, same
   events; the paths themselves were kept and corrected.
3. **Fix.** The Desktop `ActionCompleted` convention now carries an
   `ActionOutcome(Succeeded, WorkspaceChanged)` from every raise site
   (RibbonView, ProjectExplorerView, PropertyInspectorView,
   ObjectEditorView, DigitalThreadGraphView, CommandPaletteOverlay);
   every subscriber reports severity honestly and refreshes dependents
   only on `WorkspaceChanged`. `RecordRecent` updates the per-tab
   "Recently Used" row in place. The favourite-open duplicate refresh
   was removed. `WorkspaceManager.DeleteObjectAsync` (the point every
   delete surface converges on) clears a deleted current selection.
   Macro/palette successes reload Explorer+Cockpit.
   `BackgroundTaskRunner.Changed` drives the Output panel. The toast
   bridge is additionally subscribed on `INotificationDispatcher` with
   UI-thread marshalling. The Inspector's post-mutation refresh re-runs
   `InspectAsync` (`RefreshFromSourceAsync`) instead of re-rendering
   cached facets.
4. **Do the closure tests exercise the real production path?** Yes:
   real `WorkspaceHost`, real registry, real dispatch
   (`RibbonViewTests`, `MainWindowCompositionTests`).
5. **Measured refresh counts.**
   `CommandClicks_DoNotRebuildTabs_AvoidSpuriousEnablementPasses_AndUpdateRecentRowInPlace`
   instruments a delegating `IWorkspaceManager`: a rename click now makes
   **0** `CanDelete` calls (previously a full spurious enablement pass);
   a delete click makes exactly 1 (its own guard; the single post-delete
   pass runs against the now-cleared selection), dispatches exactly one
   delete, keeps every `TabItem` instance alive, and still updates the
   Recently Used row. Refusal/failure outcomes are asserted `Failed`
   (no dependent rebuild), successful delete `Changed`, open-for-edit
   `NoChange`.
6. **Mutation evidence.** Restoring `Rebuild()` inside `RecordRecent`
   fails the test above (verified by running the mutation). Flipping any
   outcome at a raise site fails the outcome assertions. Removing the
   dispatcher subscription for the toast bridge fails
   `PlatformNotification_PublishedThroughTheRealDispatcher_ReachesAVisibleToast`.
7. **Verified no staleness introduced by removed refreshes:** each
   removed refresh corresponded to an operation that changed nothing
   (refusals/failures/opens) or to a duplicate of a surviving refresh;
   the review's stale-UI inventory items were either fixed here or
   registered as `TD-66` — none silently dropped.

**Residual, registered, not renamed into closure:** the O(everything)
internals of `CockpitView.Refresh()` itself, full-tree Explorer reloads,
editor tabs stale under *external* mutation, graph rebuild-per-keystroke,
and the absence of a domain-mutation event → **`TD-66` (Open)**. A
by-product finding: the pre-existing
`DeleteButton_WithARealSelection_ActuallyDeletesTheRealObject` test had
been passing while the delete actually failed (its "Deleted" substring
also matches "cannot be deleted…"); it now targets a leaf object and
asserts the outcome — closure theatre found and removed.

### TD-59 — Reserved device-name uniqueness: **CLOSED**

1. **Original failure reproduced (by trace + probe).**
   `identifier → validation → persistence/index → ListAsync → lookup`:
   `MaterialCatalog.RegisterAsync("CON", …)` wrote the index entry
   through `PersistenceStore`, whose `Uri.EscapeDataString` leaves
   `CON`/`PRN`/`AUX`/`NUL`/`COM1`–`COM9`/`LPT1`–`LPT9` (any casing, with
   or without extension), trailing dots, and `"."`/`".."` untouched —
   verified empirically on this .NET 10 runtime. On Windows such writes
   are routed to devices and `File.Exists` reports them absent:
   registration **returned success**, then `FindAsync` was `null`
   forever, `ListAsync` omitted the record, and the backing document was
   orphaned — the exact "silently collapse into missing records"
   failure. Trailing-dot keys aliased their dotless siblings;
   case-variant keys shared one file on case-insensitive file systems
   while taking *different* locks (`LockKey` used the raw strings).
2. **Chosen engineering behaviour:** escape/encode safely at the
   persistence boundary (protects every consumer, not just Materials),
   with no data discarded: `EncodeSegment` percent-escapes a reserved
   device stem's first character and any terminal dot;
   `Uri.UnescapeDataString` remains the exact decoder; safe names encode
   identically to before (existing stores stay valid); legacy-encoded
   records stay readable and migrate forward on the next write.
   Case-exact matching on read/delete plus a loud refusal (typed
   `PersistenceStoreUnavailableException`) instead of a silent
   case-collision overwrite; the lock key is the encoded, case-folded
   file identity; writes are temp-file + atomic rename.
3. **Tests on the real production path** (real file-backed
   `PersistenceStore`, and end-to-end through the real
   `MaterialCatalog`/`EngineeringDocumentStore` stack and the real
   `RequirementsService`): the full commissioned matrix — NUL, CON, PRN,
   AUX, COM1, LPT1, case variants, extension variants, duplicate
   identifiers (still `DuplicateMaterialException`), valid identifiers
   adjacent to reserved names (`CONX`/`XCON`/`COM10`/`LPT` keep their
   plain encoding) — plus trailing dots, dot-names, traversal-shaped
   keys, legacy migration, and no-temp-file hygiene
   (`PersistenceStoreHostileNameTests`, `MaterialCatalogHostileDataTests`,
   `RequirementsServiceHostileDataTests`).
4. **Can the tests pass with the defect present?** No for the encoding:
   disabling the reserved-stem escape fails 6 tests (mutation run and
   verified). The store's previously wholly-untested escaping guard
   (test-review mutation #26, SURVIVES) is now killed by the traversal
   tests.
5. **Honest limits, disclosed in the register row:** the Windows device
   routing itself and runtime case-collision cannot manifest on the
   Linux CI host; the tests pin the cross-platform invariants that make
   those failures impossible (no reserved file stem is ever produced,
   exact-name matching, loud refusal). Write *atomicity* has no
   test-killable mutation (crash semantics); it is verified by review.

### TD-60 — Malformed index values: **CLOSED**

1. **Defect in source, full inventory (not just the named site):**
   `Guid.ParseExact` on index *values* (`MaterialCatalog.ReadDocumentIdAsync`,
   `RequirementsService.ReadDocumentIdAsync`) and on registry *keys*
   (`ListCollectionsAsync`/`ListGroupsAsync` — a `.DS_Store` dropped in
   the directory aborted the whole listing with `FormatException`);
   unguarded `JsonSerializer.Deserialize` leaking `JsonException` from
   `EngineeringDocumentStore` (document/revision/reference reads —
   with a literal `null` record silently misclassified as "document does
   not exist"), `AuditQuery` (one corrupt record hid *all* audit
   history), `VerificationService`, and every Desktop state loader plus
   `MacroManager.LoadAsync` — the latter class running in the
   composition root, so one torn write **bricked startup permanently**
   despite each loader's own "never an exception" contract. A poisoned
   `Materials.Index` entry additionally made its id permanently
   unregisterable (the duplicate check threw) with no self-heal path.
2. **Fix — controlled, typed, and never corruption-as-absence:**
   `Guid.TryParseExact` with corruption-naming
   `MaterialsException`/`EngineeringDataException` on index values;
   non-GUID registry keys skipped with a logged warning (a foreign file
   is not a record — nothing discarded); stale index entries (document
   missing or wrong Kind) read as absent and no longer abort
   `MaterialCatalog.ListAsync` (guard ported from its sibling, closing
   the copy-paste drift the architecture review flagged);
   `JsonException` wrapped into the owning framework's exception type at
   every store read; `null`-literal records now throw (corruption, not
   absence); Desktop loaders and `MacroManager` degrade to their
   documented first-run defaults, and one corrupt macro entry no longer
   aborts the healthy rest.
3. **Tests on the real production path:** corruption injected through
   the real stores and real service stacks
   (`MaterialCatalogHostileDataTests`,
   `RequirementsServiceHostileDataTests`,
   `EngineeringDocumentStoreCorruptionTests`, `AuditQueryCorruptionTests`,
   `MacroManagerCorruptionTests`, `CorruptedStateLoadTests` — the last
   using the torn-write shape `{"ToastDurationSeconds":4.5,"Conf`).
4. **Mutation evidence:** reverting `TryParseExact` to `ParseExact`
   fails 6 tests (mutation run and verified).

---

## Part 2 — Re-verification of every previously claimed closure

No prior claim was assumed. Per-item verdicts (full evidence at cited
locations, verified this pass):

| Item | Verdict |
|---|---|
| TD-06, TD-09, TD-10, TD-11, TD-44, TD-46, TD-47, TD-48 | **CLOSED-VERIFIED** against source at the register's cited locations. |
| TD-51 | **CLOSED-VERIFIED** — all four mechanism elements intact and pinned by real-assembly, real-host tests; two named *surviving mutations* recorded for permanent coverage (narrowing the four-exception filters to the two shapes the tests manufacture; the untested parameterless `DiscoverModules()` enumeration branch). |
| TD-52 | **SUSPECT → tracked as `TD-64`** — the gate is real and unit-tested, but a one-line composition mutation (`TempestHost.cs` registration swap) disarms it in production with the suite green; no permanent end-to-end attack-shape test; `TD-56` documents a live constructor-window bypass the Resolved row doesn't cross-reference. |
| TD-40 | **SUSPECT → tracked as `TD-63`** — fix present at `WorkspaceViewCoordinator.CloseDocumentAsync`, but deleting the `IsMarkedDirty` guard (the exact original defect) leaves the suite green. |
| Open rows TD-13/14/16/41/42/43/45/49/50/53/54/55/56 | Statuses **honest**: each defect re-confirmed live in source exactly as the register describes; none uses scoping language to conceal a reachable defect. They remain Open — verbal risk-acceptance was not accepted as closure here either. |

## Part 3 — New findings from the eight-discipline re-review

Fixed in this pass (same defect classes as the three named findings):
everything in Parts 1's fix lists, including the `.DS_Store`
listing-abort, the bricked-startup loaders, the dead toast/task-runner
wiring, the stale-selection-after-delete, and the weak delete test.

Registered as new Open debt (numbers, not renamings — see each row for
file-level detail): **TD-57** governance-register currency drift (and
the Companion-branch numbering collision), **TD-61** symlink bypass of
plugin-folder containment, **TD-62** unauthenticated OpenAPI
permission-map disclosure, **TD-63**/**TD-64** the two SUSPECT-closure
test gaps above, **TD-65** systemic accessibility gaps, **TD-66**
remaining refresh-architecture debt, **TD-67** crash-window write
ordering/orphans, **TD-68** `AsyncKeyedLock` unbounded growth,
**TD-69** DI-container silent overwrite + unhonoured optional
parameters.

Governance corrections applied directly (they were false claims, not
debt): the Risk Register's five index rows contradicting its own Source
of Truth and its false "verified … and vice versa" cross-check; the
Release Register's v0.13.1 "Not yet tagged/merged/published" residue
contradicting the row's own Released status.

## Verification summary

Build clean (0 errors). Core 2412/2414 and Desktop 228/228 (the two
failures are the pre-existing, documented Linux-environment cases,
unchanged from baseline). Three targeted mutations run and killed
(reserved-stem escape off → 6 failures; `TryParseExact` reverted → 6
failures; `RecordRecent` `Rebuild()` restored → 1 failure), then
restored and re-verified green.
