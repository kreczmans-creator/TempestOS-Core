# Backlog

The live technical-debt list. Replaces the 468 KB `Technical Debt
Register.md`, frozen as history at
[`archive/docs-2026-09/governance/Quality/Technical Debt Register.md`](archive/docs-2026-09/governance/Quality/Technical%20Debt%20Register.md).

**Triage basis (`WP 17.0B`, 2026-09-08):** every register row whose
final Status cell led with `Open`, `Partially resolved` or `Deferred` —
101 rows (170 total rows in the register; 93 of the 101 are `Open`
alone, which is the figure `docs/releases/v1.0.0/WorkPackages.md`
names). The disposition rule is the one that document states under
"Disposition of the current Technical Debt Register": a row closed by
a substrate Work Package (`WP 17.1A`, `17.1B`, `17.2A`, `17.3A`,
`18.0A`) or a surface Work Package (`WP 18.1A`, `18.2A`, `19.2A`,
`19.2B`) is **Owned by programme**, below, mapped from each Work
Package's own "Closes" column; a row about plugins, REST, licensing or
a frozen P02–P07 layer is **Archived with the layer**; everything
else a user could still notice is the **Live Backlog**, capped at 30.

Nine rows close by this Work Package itself rather than by a future
one: `TD-45` (branch protection, now configured — see
`CONTRIBUTING.md`), `TD-123`–`TD-125`, `TD-127`, `TD-151`–`TD-153` and
`TD-164` (all governance-register drift or precision complaints —
moot once the registers they complain about are archived and the
health check that watched them is reduced), and `TD-43` (the health
check's generic exception handler already names the failing check in
its `Fail` result; carried forward unchanged into the reduced script).
None of these nine appear below.

## Live Backlog (28 of 30 cap)

`TD-147` is listed first and marked Release Blocking on purpose: it is
mechanically a `WP 17.1B` closure like the other transactional-store
rows below it, but it is also the first thing `v0.17.0`'s physical
review checks, so it stays visible here rather than only in the table
beneath.

| ID | Title | Owner |
|---|---|---|
| `TD-147` | **Release Blocking.** An object creation whose initial durable write fails still registers the object in memory; its next successful write makes a reported failure real | `WP 17.1B` |
| `TD-05` | Module discovery still requires a parameterless constructor outside the `[ModuleMetadata]` lift | unowned |
| `TD-17` | Document revision content is an opaque string with no structured payload support | `WP 18.0B` |
| `TD-24` | `VerificationContext` has no bound on criteria, evidence or links recorded | unowned |
| `TD-25` | `RequirementsService` has no compare-and-swap; concurrent edits can silently clobber | `WP 18.2B` |
| `TD-27` | `InMemoryEngineeringObjectRepository` iteration order is unguaranteed | `WP 17.1B` (judgement — see note) |
| `TD-28` | Bulk requirement commands don't auto-refresh an already-open view | `WP 18.1A` (judgement — see note) |
| `TD-33` | `EngineeringCockpit.FormatCoverage` returns a hardcoded, wrong-discipline empty-state string | `WP 19.1B` |
| `TD-38` | `EngineeringObjectFactory` enforces no business-identifier uniqueness | `WP 18.2B` |
| `TD-41` | `ObjectEditorView` never resolves a real Requirement; always falls back to the generic body | unowned |
| `TD-42` | `new-release.ps1`'s `git tag`/`git push` calls never check `$LASTEXITCODE` | unowned |
| `TD-63` | `TD-40`'s dirty-tab-close fix is not pinned on its production path | unowned |
| `TD-76` | No project context anywhere in the running application | `WP 19.0A` |
| `TD-78` | Brand design system (colours, fonts) is absent from the Desktop | unowned |
| `TD-84` | Grouping row: `TD-74`/`76`/`79`/`81` are one Product Spine deficiency, not four | unowned |
| `TD-91` | `IWorkspaceLayout` cannot express a tabbed or floating panel | unowned |
| `TD-92` | Drag-to-dock has no live preview adorner | unowned |
| `TD-93` | `Tempest.Samples` redeclares 13 canonical vocabulary strings; can't reference the owner | unowned |
| `TD-98` | Document viewer has no markup, annotation or rotation | `WP 18.3A` (partial) |
| `TD-99` | DWG and SVG attachments report `Unsupported` in the viewer | unowned |
| `TD-101` | A page rasterises at full size even when only part of it is visible | unowned |
| `TD-131` | Focus-ring contrast test can't see any `Flat`-treatment state | unowned |
| `TD-134` | `SettingsDocument<TDocument>` has no per-consumer notion of "current version" | unowned |
| `TD-150` | `PersistenceStore`'s post-commit failure window: 0 of 3,330 tests would notice a revert | `WP 17.0C` |
| `TD-154` | CI's `linux-launch-smoke` marker now fires before the composition root runs | unowned |
| `TD-155` | Materials library reuses an incompatible payload shape under the old document Kind | `WP 18.0B` |
| `TD-157` | "Pinned source superseded" warning can never fire; the resolver is never wired up | `WP 18.0B` |
| `TD-163` | 79 seeded reference records never reach the shipped product | `WP 18.0B` |

**Judgement calls, not named in any Work Package's "Closes" column:**
`TD-27` and `TD-150` sit squarely in the persistence/object-store
mechanism `WP 17.1A`/`WP 17.1B` replace, but neither row is literally
listed; `TD-28` sits in the refresh/notification mechanism `WP 18.1A`
replaces, same caveat. Owners other than "unowned" that are not one of
the nine programme Work Packages (`WP 18.0B`, `18.2B`, `18.3A`,
`19.0A`, `19.1B`, `17.0C`) are real, named in that WP's own "Closes"
column in `WorkPackages.md`, but fall outside the specific
"substrate"/"surface" set that rule defines — they are kept here,
with their real owner shown, rather than mislabelled "unowned."

## Owned by Programme

Every remaining row closed by one of the nine substrate/surface Work
Packages, per that Work Package's own "Closes" column. These do not
need a backlog entry of their own — they close when the Work Package
lands and its tests pass, not by triage.

| ID | Title | Owner |
|---|---|---|
| `TD-01` | Two logging mechanisms coexist (`ILogger` vs. legacy `LoggingService`) | `WP 17.2A` |
| `TD-03` | No disposal tracking for reflection-constructed singletons | `WP 17.2A` |
| `TD-04` | `IHostedService` name clashes with `Microsoft.Extensions.Hosting.IHostedService` | `WP 17.2A` |
| `TD-12` | `IPersistenceStore` has no native query or filter capability | `WP 17.1A` |
| `TD-18` | `LinkAsync` concurrency under many simultaneous calls is untested | `WP 17.1A` |
| `TD-20` | `MaterialCatalog` reads a full revision history for a latest-only lookup | `WP 17.1A` |
| `TD-21` | `ICalculationDefinition.Calculate` carries no `CancellationToken` | `WP 18.0A` |
| `TD-22` | `CalculationContext` has no result bound; intermediate values aren't type-safe on read-back | `WP 18.0A` |
| `TD-23` | `VerificationService.RecordAsync`'s multi-step link sequence is not transactional | `WP 17.1B` |
| `TD-29` | `CalculationRecord` never retains its input, blocking a parameterless re-run | `WP 17.3A` / `WP 18.0A` |
| `TD-30` | `ICalculationResult`/`IVerificationResult`/`IApprovalGate` have zero implementations | `WP 18.0A` |
| `TD-32` | Verification's `verifiedBy` link is invisible to `RelationshipRepository` | `WP 17.1B` |
| `TD-36` | `PersistenceStore.DefaultRootPath` resolves relative to the process CWD | `WP 17.1A` |
| `TD-67` | Crash-window write ordering can strand an invisible orphan document | `WP 17.1A` |
| `TD-86` | Engineering object mutation writes are per-object and unbatched | `WP 17.1B` |
| `TD-88` | Startup rehydration is eager and linear, never lazy or project-scoped | `WP 17.1A` |
| `TD-95` | Attachment bytes are stored per attachment, never deduplicated by content | `WP 17.1B` |
| `TD-96` | `IBinaryPersistenceStore` materialises whole file content in memory | `WP 17.1B` |
| `TD-130` | Reconciliation services (one of which deletes data) have no authorization seam | `WP 17.2A` |
| `TD-137` | `PersistenceStore`'s atomic writes are crash-safe but not `fsync`'d | `WP 17.1A` |
| `TD-141` | Two durable relationship-write paths carry no supersession guard | `WP 17.1B` |
| `TD-142` | The refuse-after-mutate defect recurs on twelve concrete-Kind mutators | `WP 17.1B` |
| `TD-144` | A failed move's durable link write can leave a durable partial reparent | `WP 17.1B` |
| `TD-145` | Two concurrent moves can form an undetected parent cycle | `WP 17.1B` |
| `TD-146` | A delete can commit while a concurrent move gives the object a live child | `WP 17.1B` |
| `TD-148` | `DeleteAsync` can report failure after the soft delete already committed | `WP 17.1B` |
| `TD-149` | A deleted legacy-encoded record can resurrect as live on delete failure | `WP 17.1A` |
| `TD-156` | A superseded reference record keeps its secondary index entry | `WP 17.1A` |
| `TD-158` | `ReferenceDataCatalog` composes durable writes with no all-or-nothing semantics | `WP 17.1B` |
| `TD-169` | The canonical lifecycle permits no `Draft` → `Archived` transition | `WP 18.0A` |
| `TD-170` | Naming an executed calculation is create-then-link with no compensation | `WP 17.1B` |
| `TD-65` | Systemic Desktop accessibility gaps: dialogs, focus, `AutomationProperties` | `WP 19.2B` |
| `TD-66` | Refresh-architecture debt beyond `TD-58`: Cockpit, Explorer, open tabs | `WP 18.1A` |
| `TD-73` | Rail and ribbon never compact; `MinWidth` bars small displays | `WP 19.2B` |
| `TD-74` | No global navigation architecture; three-level mock-up model collapsed to one | `WP 19.2B` |
| `TD-77` | Command Palette is not contextual; most real commands are unavailable there | `WP 19.2B` |
| `TD-79` | Engineering Workspace has deep domain support and almost no dedicated UI | `WP 18.2A` |
| `TD-81` | Whole mock-up modules unimplemented: Tasks, Commercial, Resources, Knowledge, Admin | `WP 19.2B` |
| `TD-90` | A docking re-render does not restore keyboard focus | `WP 18.1A` |
| `TD-108` | Blocking `.GetAwaiter().GetResult()` calls, several on the UI thread | `WP 18.1A` |
| `TD-109` | `MainWindow` is a 1,577-line god object | `WP 19.2A` |
| `TD-115` | Three registered commands have no production construction path | `WP 19.2A` |
| `TD-118` | The Engineering Cockpit's read surface is synchronous by shape | `WP 18.1A` |
| `TD-128` | Digital Thread graph edges are keyboard-unreachable | `WP 19.2B` |
| `TD-132` | Every relationship row's "Open" button shares one accessible name | `WP 19.2B` |
| `TD-133` | Docking-panel repositioning and tab reordering are mouse-only | `WP 19.2B` |
| `TD-160` | The whole merged engineering capability has no Desktop UI surface | `WP 18.2A` |
| `TD-165` | The bracket verification artefact cannot be filled in from the Desktop | `WP 18.2A` |

## Archived with the Layer

Not debt in a product that does not ship the layer:

| ID | Title | Reason |
|---|---|---|
| `TD-161` | New application surfaces write engineering assets with no project scope | `WP 19.0B` archives the P04/asset surfaces this concerns |
| `TD-162` | `ProjectDependencyRegister` (`P04`) is unreferenced | `WP 19.0B` |
| `TD-82` | Companion (mobile/field) application has zero implementation on this branch | Companion is explicitly out of `v1.0.0` scope (`docs/releases/v1.0.0/WorkPackages.md`, "What v1.0.0 is") |
| `TD-13` | REST API identity resolution carries no real authentication | frozen by `WP 17.2A` — the inbound REST API moved to `src/Frozen/Tempest.Core.Api` (`ADR-0146`) |
| `TD-14` | No TLS on the REST API's own Kestrel listener | frozen by `WP 17.2A` |
| `TD-15` | Audit records the "unknown actor" for every REST-invoked command | frozen by `WP 17.2A` |
| `TD-16` | License file contents are trusted with no signature verification | frozen by `WP 17.2A` — Licensing moved to `src/Frozen/Tempest.Core.Licensing` (`ADR-0146`) |
| `TD-49` | TOCTOU window between plugin signature verification and load | frozen by `WP 17.2A` — the plugin trust platform moved to `src/Frozen/Tempest.Core.Plugins` (`ADR-0146`) |
| `TD-50` | First-party certificate trust is a filename convention, not a certificate attribute | frozen by `WP 17.2A` |
| `TD-53` | A hosted-service construction failure can be misclassified as non-critical | frozen by `WP 17.2A` |
| `TD-54` | `ITempestServiceProvider`'s DI non-registration is incidental, not enforced | frozen by `WP 17.2A` |
| `TD-55` | `PluginDeniedTypeRegistry` can wrongly deny an innocent shared assembly's types | frozen by `WP 17.2A` |
| `TD-56` | A plugin constructor runs with a `null` (first-party) component scope | frozen by `WP 17.2A` |
| `TD-61` | Plugin-folder containment check does not resolve symlinks | frozen by `WP 17.2A` |
| `TD-64` | `TD-52`'s gate closure has no end-to-end production-wiring test | frozen by `WP 17.2A` |
| `TD-129` | REST 404-vs-401 split lets an unauthenticated caller enumerate routes | frozen by `WP 17.2A` |

Sixteen rows land here in the currently-*open* subset — three from
earlier triage, and thirteen moved by `WP 17.2A` (`ADR-0146`): the
plugin-trust, REST and Licensing rows above, whose subject moved to
`src/Frozen/` and out of the `v1.0.0` build. The "about fifteen rows"
`WorkPackages.md` estimates for this bucket counts the full register,
most of which (the plugin-trust and REST rows, e.g. `TD-09`–`TD-11`) were
already `Resolved` and never entered this triage — this Work Package's
own move is a second, later wave the estimate did not anticipate by
number.
