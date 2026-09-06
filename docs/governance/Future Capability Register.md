# Future Capability Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Future Capability Register |
| **Purpose** | The single, permanent, authoritative list of every identified future TempestOS capability — replacing every informal "Car Park" discussion, scattered Technical Debt Register trade-off, and per-Work-Package "Future Capability Recommendations" document with one register future roadmap planning and Work Package selection is drawn from. |
| **Scope** | Every future capability identified from a release retrospective, a Future Capability Recommendations document, a Technical Debt Register entry, a governance review, an architecture discussion, or existing project documentation, as of `WP 7.0A`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | This document, from `WP 7.0A` onward. Prior to this Work Package, the same information existed only in fragments — see each entry's own "Sourced From" field for where it previously lived. |
| **Review Frequency** | Updated whenever a new future capability is identified (by any Work Package, retrospective, or review), or whenever an existing capability's Status changes (a Work Package begins, completes, or a capability is formally deferred/rejected). |
| **Last Reviewed** | 2026-09-06 (`Group C`, P07 Business Governance & Scale) — **two entries added, `FCR-0096`–`FCR-0097`**, in a new **Business Governance** category: Business Template Integration (blocked on access — the Claude Design project named as mandatory source material by the `P07` commissioning instruction returned HTTP 403, and no equivalent content exists in the repository) and Authoritative Business Data Population (deliberately not done, mirroring `FCR-0089`/`FCR-0093` for reference data). The register's own range is now `FCR-0001`–`FCR-0097`; 94 → 96 entries, `FCR-0092` still deliberately skipped, preserving the `WP 16.0B` disclosure below. No existing entry's Status changed. Previously reviewed 2026-09-06 (`Group A`, P01 Engineering Reference Data) — **one entry resolved and three added.** `FCR-0034` (Affine Unit Conversion) marked **Implemented**: `ADR-0125` added an optional `ToBaseUnitOffset` to `Unit<TDimension>` and refused arithmetic on affine quantities; the revisit trigger this entry named fired three times at once (A1, A5 and A7 each record a temperature). The register's own range is now `FCR-0001`–`FCR-0095`. **`FCR-0093`–`FCR-0095` added**: Authoritative Reference-Dataset Population for the six libraries beyond A4 (the sibling of `FCR-0089`, deliberately not a rewrite of it); A Shared Reference-Data Import Pipeline (generalising `FCR-0090`, which stays open as the bearing-specific case); A Temperature-Difference Dimension (sourced from `ADR-0125`'s own disclosed boundary). 91 → 94 entries. `FCR-0092` remains deliberately skipped, preserving the `WP 16.0B` disclosure below. No other entry's Status changed. Previously reviewed 2026-09-05 (`A4`, Bearing Library) — **three new entries added, `FCR-0089`–`FCR-0091`** (Authoritative Bearing Dataset Population; Bearing Reference-Data Import Pipeline and Mapping Report; Schema Versioning for Reference-Data Catalogue Documents), each sourced directly from a deferral `A4` disclosed rather than silently absorbed — see `docs/architecture/A4 Bearing Library.md` §14/§16 and `ADR-0124`'s own Negative consequences; 88 → 91 entries. **`FCR-0092` is deliberately skipped**, preserving the `WP 16.0B` disclosure below that the number belongs to the `Tempest.Companion` branch's own sequence and is to be translated into this register's numbering when that branch merges. No existing entry's Status changed. Previously reviewed 2026-09-04 (`WP 16.0B`, Integrate off-`main` work) — resolves the `FCR-0092` disclosure below per decision record `D-022` (Proposed when `WP 16.0B` wrote this; **ratified by the Product Owner 2026-09-05**, `docs/releases/v0.16.0/WP16.0A Product Owner Ratification — D-021 to D-026.md`): `FCR-0092`, cited by `WP 15.0A`'s commit message and `docs/design/Tempest Engineering Design System Reference.md`, is recorded as a **Companion-scoped number** belonging to the `Tempest.Companion` branch's own capability sequence (`claude/tempestos-companion-mobile-ubznt3`, deferred to v1.1), outside this register's `FCR-0001`–`FCR-0091` range (`FCR-0001`–`FCR-0088` when `WP 16.0B` wrote this; `A4` added `FCR-0089`–`FCR-0091` and deliberately skipped `0092` to keep this translation unambiguous); it is **not** added as an entry here and is to be translated into this register's numbering when the Companion branch is merged. No entry's Status changed; 88 entries unchanged. Previously reviewed 2026-09-04 (`WP 15.1A`, `v0.15.0` Release Preparation & Governance Closure) — the first review since `v0.13.0`; this register had gone unreviewed through all of `v0.13.1`, `v0.14.0`, and the undocumented `v0.14.0..main` commit range `WP 15.1A` itself formalises. **A disclosed, narrow-scope pass, not a full line-by-line re-verification of all 88 entries against current `main`**: that full audit is out of this Work Package's own scope (release/governance closure, not a Future Capability review in its own right) and is named here as a real, current gap, not silently claimed complete. What was checked directly: **`FCR-0092`, cited by `WP 15.0A`'s own commit message and by `docs/design/Tempest Engineering Design System Reference.md` as closed by the Desktop brand recovery, does not exist in this register** — `FCR-0001`–`FCR-0088` is the real, confirmed range (`ls`-equivalent count re-derived directly). The same design-system document describes `Tempest.Companion` as living "on its own branch"; the most likely explanation (**Inferred**, not confirmed — the Companion branch was not reviewed here) is that `FCR-0092` belongs to a separate, Companion-scoped capability sequence, cited without translation into this register's own numbering. Not fixed — this register cannot register a capability it cannot independently verify from a branch outside this Work Package's own scope; recorded here as the disclosure this register's own currency obligation requires. No entry's own Status was reviewed or changed by this pass; 88 entries unchanged. Previously reviewed 2026-08-17 (`WP 13.11A`, Plugin Platform Final Hardening Architecture Review) — `FCR-0001`/`FCR-0002` found stale by this Work Package's own Governance/Documentation reviewer: `FCR-0001`'s `Status`/`Proposed Target Release`/`Related Work Packages` fields had drifted since `WP 13.0A`, still reading "implementation pending `WP 13.0B`" — never corrected by `WP 13.1A`, `WP 13.2A`, or `WP 13.3A` (which reconfirmed **Implemented** in this same field's own narrative without ever fixing the table cell it contradicted) — corrected in place to `WP 13.2A` as the real implementer, `Status: Implemented`; `FCR-0002`'s own `Dependencies` field carried the identical stale citation, corrected identically. Zero new entries added; zero entries' actual disposition changed (both were already substantively correct in narrative, only their own table cells were wrong). 88 entries unchanged. Previously reviewed 2026-08-14 (`WP 13.3A`, Plugin Platform Integration & End-to-End Validation) — the plugin platform's own closing integration/validation pass for `v0.13.0`. `FCR-0001` reconfirmed **Implemented** in substance (design `WP 13.0A`, implementation `WP 13.1A`/`WP 13.2A`, independently re-verified against real code by this Work Package — see `Technical Debt Register.md`'s own identical `TD-09`/`TD-10`/`TD-11` re-confirmation); `FCR-0002` unchanged (`src/Plugins/` remains empty). **Four new entries added, `FCR-0085`–`FCR-0088`** — certificate-chain validation and revocation checking, per-plugin collectible-`AssemblyLoadContext` hot/live reload and in-process unload, process-separated isolation for an open/unvetted marketplace, and a per-plugin `AllowUnsignedLoad` allow-list — each explicitly named as out of scope by `ADR-0108`/`ADR-0110`/`ADR-0112` and by `WP 13.2A`'s own Future Evolution section, but never before given a roadmap-facing `FCR` entry of their own. 84 → 88 entries. Full detail: `docs/academy/03 Work Packages/WP13.3A-plugin-platform-integration-and-end-to-end-validation.md`. Previously reviewed 2026-08-13 (`WP 13.1A`, Plugin Runtime & Composition Root Implementation) — `FCR-0010` updated to **Implemented**: `Runtime:Plugins:RootDirectory`/`ManifestFileName`/`Disabled` are now real, tested configuration keys (`TempestHost.cs`), closing the gap `WP 13.0A` had only designed. `FCR-0001` reconfirmed unchanged, deliberately — this Work Package implements only the mechanical, non-trust half of `WP 13.0A`'s design (`ADR-0107`–`ADR-0109`); `FCR-0001`'s own trust-retrofit scope (`ADR-0110`–`ADR-0112`) remains a genuinely separate, still-unassigned future Work Package's task, still **Design complete**, not Implemented. `FCR-0002` untouched (`src/Plugins/` remains empty, reconfirmed directly). Zero new entries added. 84 entries unchanged. Full detail: `docs/academy/03 Work Packages/WP13.1A-plugin-runtime-and-composition-root-implementation.md`. Previously reviewed 2026-08-13 (`WP 13.0A`, Plugin & Registration Trust Isolation Architecture) — `FCR-0001` updated to **Design complete**, its own gating trigger (the Product Owner's confirmed third-party plugin commitment) now fired and its dependency on `FCR-0002`'s own schedule removed, both landing together as `v0.13.0`; `FCR-0010` updated to **Architecture designed**; `FCR-0002` updated in place, its own precondition (`src/Plugins/` still empty) directly reconfirmed unmet. Zero new entries added — every capability this Work Package's own six new ADRs (`ADR-0107`–`ADR-0112`) address was already tracked. 84 entries unchanged. Architecture only; zero `src/`/`tests/` files touched. Full detail: `docs/academy/03 Work Packages/WP13.0A-plugin-and-registration-trust-isolation-architecture.md`. Previously reviewed 2026-08-12 (`WP 12.4B` Architecture Review Follow-Up, Desktop Command & Event Wiring Implementation) — `FCR-0084` added (A Typed Callback Interface for `WorkspaceViewCoordinator`'s Three Bundled Callbacks) — **Identified**, sourced directly from the `WP 12.4B` architecture/code review's own Finding 2 (a genuine future opportunity — `WorkspaceViewCoordinator`'s callback count reached `ADR-0104`'s own 3-callback threshold when `WP 12.4B` added `refreshCockpit`, but introducing the interface was deliberately deferred rather than applied speculatively — that had existed only as narrative prose, not a trackable register entry, until this follow-up); 83 → 84 total. Previously reviewed 2026-08-11 (`WP 11.9.0`, `v0.11.0` Release Preparation & Engineering Sign-Off) — reviewed as part of the release's own closing sign-off; one entry corrected, not merely reconfirmed: `FCR-0005` (Governance Register Health-Check Tooling) found, on direct independent re-verification, still reading "Identified, not started" despite `WP 11.2A` having shipped `scripts/governance-healthcheck.ps1` one Work Package earlier — a factual error, not a stale date, corrected in place (see its own updated entry). This is itself the identical drift pattern `FCR-0005` exists to prevent, recurring an **eighth** time — caught here by a human sign-off review, not by the tool, since the tool does not yet audit this register. No other entries changed; 83 entries unchanged in count. `v0.11.0`'s own eighth and closing Work Package. Previously reviewed 2026-08-11 (`WP 10.8A`, Desktop Feature Completion & Existing Capability Exposure) — reviewed, zero new entries added: this Work Package's own scope was confirming `WP 10.7A`'s five already-Implemented items remained intact at runtime and closing the two genuinely incomplete ones its own controlling instruction named directly — `FCR-0075`'s own Notes updated in place (Manufacturing's own "Record Inspection Result" wired, the one remaining genuinely-unwired verb this register's own audit found; the Ribbon's own honest-fallback wording corrected, a defect in messaging, not a missing capability, so tracked in the Technical Debt Register's own narrative, not a new entry here). The Property Inspector's real Validation section is a new capability closed this Work Package but was never itself a tracked `FCR` — `23-workspace-modernisation.md`'s own "Validation, an honest placeholder" line was a `WP 10.2A` Academy disclosure, not a registered Future Capability, so its closure updates that article directly (§Future Evolution) rather than marking an entry Implemented here. 83 entries unchanged. `v0.10.0`'s own fifteenth Work Package by completion order. Previously reviewed 2026-08-10 (`WP 10.7A`, Feature Completion) — three entries marked **Implemented**: `FCR-0066` (Uniform `Move*Command` Shape, Enabling Real Drag/Drop Reparenting — implemented via a lighter, non-`IWorkspaceManager`-extending route than this entry's own original proposal, see its own updated remarks), `FCR-0068` (Discipline-Specific Object Editor Enhancements — all five named sections built, one genuine pre-existing gap found and disclosed as `TD-41` in the process), `FCR-0075` (Uniform Create/Duplicate Wiring Across All Six Disciplines — Create/Duplicate/status-transition dispatch now real for all five disciplines beyond Mechanical; Copy remains unwired, `FCR-0073`'s own named destination-picker dialog gap unchanged). This Work Package closes every WP10.6D-audited placeholder judged achievable without new architecture — see `WP10.7A Implementation Report.md`. No new entries added (every capability closed this Work Package was already tracked). `v0.10.0`'s own fourteenth Work Package by completion order. Previously reviewed 2026-08-10 (`WP 10.5C`, Commercial User Experience & Application Completion) — reviewed, zero new entries added: this Work Package's own required-first runtime audit found no genuinely unreachable feature (`WP10.5C Runtime UX Traceability Matrix.md`), and its own two real findings (both hardcoded, wrong-in-one-theme colours) were fixed in place, never reaching Future Capability status. The one disclosed, lasting scope reduction — Object Editor discipline-aware layouts — is `FCR-0068`'s own already-tracked scope, reconfirmed still Identified, not a new entry. 83 entries unchanged. `v0.10.0`'s own thirteenth Work Package by completion order (commissioned and completed after `WP 10.6A` despite its own earlier number — recorded plainly, not reordered, mirroring `WP 9.3A`'s own identical precedent). Previously reviewed 2026-08-10 (`WP 10.6A`, Command Execution & Productivity Experience) — `FCR-0078`–`FCR-0083` added (Undo/Redo Coverage Beyond Rename and Favourite Toggle; Background Task Percentage Progress Reporting; Macro Steps Eligible Beyond `CreateDefault`-Invokable Commands; Command History as a Real `ICommandDispatcher` Interception; Persisted Cross-Session Undo/Redo and Command History; Keyboard Remapping UI and a Real External Controller Integration) — all **Identified**, sourced directly from this Work Package's own six disclosed scope reductions (`WP10.6A Implementation Report.md` §8, each also tracked as `AT-18`–`AT-23` in the Technical Debt Register); 77 → 83 total. `v0.10.0`'s own twelfth Work Package. Previously reviewed 2026-08-10 (`WP 10.5B`, Desktop Workflow & Professional Interaction) — `FCR-0073`–`FCR-0077` added (Copy/Move Destination-Picker Dialog & Wired Dispatch; Export/Import Commands & Dialog Wiring; Uniform Create/Duplicate Wiring Across All Six Disciplines; Startup Splash Screen; Customisable Keyboard Shortcuts, Ribbon & Toolbar Preferences) — all **Identified**, sourced directly from this Work Package's own five disclosed scope reductions; 72 → 77 total. `v0.10.0`'s own eleventh Work Package. Previously reviewed 2026-08-10 (`WP 10.5A`, Workspace Visual Polish & Engineering User Experience) — `FCR-0071` added (A Comprehensive, Hand-Authored Vector Icon Library) and `FCR-0072` added (Split/Tiled Document View) — both **Identified**, sourced directly from this Work Package's own disclosed scope reductions; `FCR-0067` (Theme-Variant-Aware Overlay Backgrounds) marked **Implemented** — `ApplicationPalette`/`ThemeReactiveBrush`, closing `TD-39`; 70 → 72 total. `v0.10.0`'s own tenth Work Package. Previously reviewed 2026-08-09 (`WP 10.4A`, Digital Thread Visualisation) — `FCR-0070` added (Digital Thread Graph Clustering/Pruning for Dense Objects) — **Identified**, sourced directly from `ADR-0093`'s own already-disclosed, already-accepted first-iteration limitation, now attached to a real implementation for the first time; 69 → 70 total. `v0.10.0`'s own ninth Work Package. Previously reviewed 2026-08-09 (`WP 10.3B`, Ribbon, Toolbar & Command Experience) — `FCR-0069` added (Real, Authored Per-Command Icons) — **Identified**, sourced directly from this Work Package's own confirmed-by-`grep` finding that `CommandDescriptor.Icon` is unpopulated everywhere; 68 → 69 total. `v0.10.0`'s own eighth Work Package. Previously reviewed 2026-08-09 (`WP 10.3A`, Engineering Object Editors) — `FCR-0068` added (Discipline-Specific Object Editor Enhancements) — **Identified**, sourced directly from this Work Package's own disclosed scope decision (one generic engine, real per-discipline enhancements deferred); 67 → 68 total. `v0.10.0`'s own seventh Work Package. Previously reviewed 2026-08-09 (`WP 10.2B`, Docking & Workspace Layouts) — `FCR-0067` added (Theme-Variant-Aware Overlay Backgrounds) — **Identified**, sourced directly from this Work Package's own disclosed `TD-39` finding; 66 → 67 total. `FCR-0005` (Governance Register Health-Check Tooling) reconfirmed still Identified, reinforced by a fresh instance of its own exact pattern found this Work Package — the Academy Register's own `## 03 Work Packages` table found missing eleven real, already-shipped rows (see `Academy Register.md`'s own "Last Reviewed" entry) — named there, not fixed here, out of this Work Package's own scope. Previously reviewed 2026-08-07 (`WP 10.2A`, Workspace Modernisation) — `FCR-0066` added (Uniform `Move*Command` Shape, Enabling Real Drag/Drop Reparenting) — **Identified**, sourced directly from this Work Package's own disclosed drag/drop-preparation-not-implementation trade-off; 65 → 66 total. Previously reviewed 2026-08-07 (`WP 10.1B`, Runtime Host & Module Discovery Hardening) — reviewed, zero new entries added: this Work Package's own two named subjects (`TD-26`, `TD-37`) were both genuinely resolved, not merely disclosed as future capability candidates, and its one new finding (`TD-38`, `EngineeringObjectFactory<T>`'s own lack of business-identifier uniqueness enforcement) is tracked in the Technical Debt Register, not this one — mirroring `WP 10.1A`'s own identical "significant finding belongs in the Technical Debt Register" precedent. 65 entries unchanged. Previously reviewed 2026-08-07 (`WP 10.1A`, Engineering Cockpit Implementation) — `FCR-0056` (Governance & Risk Workspace) updated, not resolved: `EngineeringCockpit` now reads this exact Domain family for the first time (Open Decisions, Risk Summary, Upcoming Milestones), strengthening rather than closing this capability's own case, since a full Explorer/commands/Property Inspector presence still does not exist. No new entry added — the one significant new finding this Work Package made (a sample-module registration defect) is tracked as `TD-37` in the Technical Debt Register, not this register. 65 entries unchanged. Previously reviewed 2026-08-07 (`WP 10.0B`, Desktop Application Framework) — `FCR-0063` (Concrete Cross-Platform .NET Desktop UI Framework Selection) marked **Implemented** — Avalonia 11.2.3, `ADR-0094`, the first entry in the "Workspace" category `WP 10.0A` itself added to leave Identified status. No new entry added; `FCR-0064`/`FCR-0065` reconfirmed still Identified, both outside this Work Package's own "no contract redesign"/"no new Platform Service" scope. **Disclosed, found gap, not fixed retroactively**: this field's own immediately-prior entry was `WP 9.9.0` Second Pass's (below) — `WP 10.0A` itself, despite adding `FCR-0063`–`FCR-0065`, never updated this field — the identical class of drift this register's own `WP 9.3A`/`WP 9.4A` gap already disclosed once before, recorded here plainly rather than silently backfilled. Previously reviewed 2026-08-07 (`WP 10.0A`, User Experience Architecture) — `FCR-0063`–`FCR-0065` added (Concrete Desktop UI Framework Selection, Floating/Multi-Monitor Panel Contract Extension, Notification Framework Workspace Integration) — all **Identified**, sourced directly from `WP10.0A UX Architecture Document.md` and companion reviews, the first entries in this register raised by an architecture-only Work Package; 62 → 65 total. Previously reviewed 2026-08-07 (`WP 9.9.0`, Release Preparation & Product Baseline — Second Pass) — reviewed, zero new entries added: a second, independent verification pass, commissioned after `WP 9.8B` closed the first pass's own top standing recommendation. All 62 entries re-verified directly a second time, 62 total unchanged. `FCR-0005` (Governance Register Health-Check Tooling) reconfirmed still Identified, now carrying its strongest evidentiary case yet — both `WP 9.8B`'s own existence and this pass's own newly-registered `TD-34` finding are direct, first-hand evidence of the manual-effort cost automation would eliminate. Previously reviewed 2026-08-07 (`WP 9.9.0`, Release Preparation & Product Baseline — First Pass) — reviewed, zero new entries added: verification-only Work Package. All 62 entries (`FCR-0001`–`FCR-0062`) re-verified directly against this register's own section headings, 62 total unchanged. `FCR-0005` (Governance Register Health-Check Tooling) reconfirmed still Identified, now disclosed as recurring across a seventh consecutive release-adjacent review — see `WP9.9.0 Release Readiness Report.md` §16 (Future Capability Review) and `WP9.9.0 Product Approval Report.md`'s own standing recommendations. Previously reviewed 2026-08-07 (`WP 9.5A`, Manufacturing Workspace) — `FCR-0060`–`FCR-0062` added (A Genuine `Routing`/`SupplierOperation` Domain Kind, Parameterising `EngineeringCockpit.FormatCoverage`'s Own Empty-State Message, Extending `VerificationService.RecordAsync`'s Own `IHasRelationships` Linking to Cover Inspection Subjects) — all **Identified**, sourced directly from `WP9.5A Future Capability Assessment.md`; 59 → 62 total. **Disclosed, found gap, not fixed retroactively:** this field's own immediately-prior entry was `WP 9.2A`'s (2026-08-05) — neither `WP 9.3A` nor `WP 9.4A` updated this "Last Reviewed" field despite each adding three real entries of their own (`FCR-0054`–`FCR-0056`, `FCR-0057`–`FCR-0059`, confirmed present in the Coverage Note below and in the table itself) — a genuine drift between this field and this register's own actual content, found while adding this Work Package's own entries, recorded here plainly rather than silently backfilled to look as though it was continuously current. Previously reviewed 2026-08-05 (`WP 9.2A`, Engineering Calculations Workspace) — `FCR-0051`–`FCR-0053` added (Concrete `ICalculationResult`/`IVerificationResult` Implementations, Concrete Approval/Review Workflow, Recalculate Resuming From a Previously-Executed Input) — all **Identified**, sourced directly from `WP9.2A Future Capability Assessment.md`; 50 → 53 total. Previously reviewed 2026-08-05 (`WP 9.1A`, Requirements Management Workspace) — `FCR-0048`–`FCR-0050` added (Requirement Collection Membership Removal, Domain-Level Search Generalised Beyond `IEngineeringObject`, Multi-Target Workspace View Refresh) — all **Identified**, sourced directly from `WP9.1A Future Capability Assessment.md`; `FCR-0039` (Multi-Selection) marked **Resolved/Implemented** — `ADR-0085`, the first entry in the "Workspace" category to leave Identified status; 47 → 50 total. Previously reviewed 2026-08-05 (`WP 9.0B`, Product Configuration & BOM Management) — `FCR-0044`–`FCR-0047` added (Product Variant Resolution, Unit of Measure Canonicalisation, Cost Roll-Up Over the BOM Hierarchy, Configuration Management Workflow) — all **Identified**, sourced directly from `WP9.0B Future Capability Assessment.md`; 43 → 47 total. Previously reviewed 2026-08-05 (`WP 9.0A`, Mechanical Product Structure) — `FCR-0039`–`FCR-0043` added, the first entries in a new "Workspace" category (multi-selection, drag-and-drop, real invoke-by-Id command execution, a second Engineering Discipline Module reusing this Work Package's own provider categories, and structural mutation for further object families) — all **Identified**, sourced directly from `WP9.0A Future Capability Assessment.md`; 38 → 43 total. Previously reviewed 2026-07-30 (`WP 7.2C`, Requirements & Verification Platform Contract Review) — `FCR-0027`'s own complete public contracts defined (thirteen domain concepts, four reserved ADRs, twelve completion deliverables) — still **Identified**, not **Implemented**; a contract review is not an implementation, and this Work Package wrote no production code. Previously reviewed 2026-07-30 (`WP 7.2B`, Requirements & Verification Platform Architecture) — `FCR-0027`'s own complete architecture designed (twelve domain concepts, three reserved ADRs, eleven completion deliverables) — still **Identified**, not **Implemented**; an architecture phase is not an implementation, and this Work Package wrote no production code. Previously reviewed 2026-07-30 (`WP 7.2A`, Strategic Roadmap Selection & Programme Architecture) — all 36 entries reviewed against seven candidate next-programme options; no new entry added, no status changed. `FCR-0027` (Requirements Engine) recommended as the next implementation programme's own scope (`WP7.2A Recommended Programme.md`) — still **Identified**, not yet approved; recommendation is not approval. Previously reviewed 2026-07-30 (`WP 7.1F`, Engineering Core Integration Review & Certification) — confirmed `FCR-0029`–`FCR-0033` all **Implemented**; reviewed `FCR-0034`/`FCR-0035`/`FCR-0036`, all remain **Deferred** (no scheduled release); `FCR-0005`'s own priority raised Medium → High after a third, independent recurrence of the governance-register-drift pattern it exists to prevent (see `WP7.1F Engineering Core Architecture Conformance Report.md` §7). No new capability identified. Previously reviewed 2026-07-30 (`WP 7.1E`, Verification Framework) — `FCR-0033` marked **Implemented**, completing the Engineering Foundation programme (all five, `FCR-0029`–`FCR-0033`, now Implemented); `FCR-0036` added (Transactional Multi-Document Operations, found during this Work Package's own required Security Review). Previously reviewed 2026-07-30 (`WP 7.1D`, Engineering Calculation Framework) — `FCR-0032` marked **Implemented**; `FCR-0035` added (Calculation Execution Timeout & Cancellation Support, found during this Work Package's own required Security Review). Previously reviewed 2026-07-30 (`WP 7.1C`, Materials Framework) — `FCR-0031` marked **Implemented**. Previously reviewed 2026-07-30 (`WP 7.1B`, Units & Quantities Framework) — `FCR-0030` marked **Implemented**; `FCR-0034` added (Affine Unit Conversion / Temperature, found during implementation, not anticipated by prior planning). Previously reviewed 2026-07-30 (`WP 7.1A`, Engineering Data Model) — `FCR-0029` marked **Implemented**, the first entry in this register to leave "Identified" status. Previously reviewed 2026-07-30 (`WP 7.0B`, Engineering Foundation Planning & Capability Architecture) — added `FCR-0029` through `FCR-0033`, the five cross-cutting Engineering Foundation frameworks this Work Package's own dependency analysis identified as architecturally necessary before any discipline-specific Engineering Module can begin (see `WP7.0B Engineering Foundation Architecture.md`). Each is marked **Inferred**, not Verified — architectural necessity reasoning, not a capability named in a prior document, per the same discipline `FCR-0026` already applied. Previously reviewed 2026-07-30 (`WP 7.0A`, established). |
| **Related Documents** | `Capability Categories.md`; `Product Roadmap.md`; `VISION.md`; `docs/governance/Quality/Technical Debt Register.md`; `docs/security/Security Roadmap.md`; `docs/security/Threat Model.md`; `docs/releases/v0.7.0/WorkPackages.md`; the eight `WP6.x Future Capability Recommendations.md` documents under `docs/releases/v0.6.0/`. |
| **Related ADRs** | ADR-0013, ADR-0040, ADR-0044, ADR-0045, ADR-0046, ADR-0049, ADR-0050, ADR-0052, ADR-0053, ADR-0054, ADR-0055, ADR-0056, ADR-0057 — see individual entries. |
| **Related Academy Articles** | `WP6.8-platform-services-integration-review.md` §6 (the direct source of `FCR-0001`, `FCR-0003`, `FCR-0004`, `FCR-0005`, `FCR-0006`); `WP7.0B-engineering-foundation-planning-and-capability-architecture.md`. |
| **Coverage Status** | Complete for every future capability traceable to an existing, real document, plus five architecturally-inferred Engineering Foundation entries (`WP 7.0B`). **Not** claimed complete for the five Engineering Discipline categories with no identified candidate — see Coverage Note, below, and `Capability Categories.md`'s own identical disclosure. |

---

## Governing Rules

1. **Identifiers are permanent.** Once assigned, an `FCR-NNNN` identifier
   is never reused or renumbered, even if the capability it names is
   later rejected or merged into another entry — mirroring `ADR`/`RD`
   numbering discipline elsewhere in this governance suite.
2. **This register replaces informal tracking, not existing governance
   registers.** A capability already tracked as Technical Debt Register
   debt or a disclosed trade-off keeps its own `TD-NN`/`AT-NN` identifier
   there — this register adds a roadmap-facing `FCR-NNNN` entry
   cross-referencing it, it does not duplicate or supersede it.
3. **Do not invent implementation details.** Every entry below is
   sourced from an existing, cited document. Where the source material
   is a category-level ambition rather than a specific design (the nine
   Engineering Discipline categories in `Capability Categories.md`, most
   conspicuously), no entry is invented to fill the gap — see Coverage
   Note.
4. **Merge duplicates.** Several Technical Debt Register items and
   `WP6.x Future Capability Recommendations.md` documents describe the
   same underlying capability from different angles (for example,
   `TD-09`/`TD-10`/`TD-11` and Security Roadmap items 1/2/10 are one
   capability — a plugin/registration trust boundary — not three). Each
   is merged into a single `FCR` entry citing every source.

## Entries

### Platform

#### FCR-0001 — Plugin & Registration Trust Isolation Retrofit

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Insert real `IPermissionEvaluator` enforcement calls at the three call sites `WP 6.1` deliberately left unretrofitted: plugin/module load trust boundary, `NavigationService.Unregister`, and Command/Navigation registration-order ownership. The enforcement mechanism itself already exists (`ADR-0044`); this capability is applying it, not building it. |
| **Status** | **Implemented — `WP 13.2A`.** Designed `WP 13.0A` (`ADR-0110`–`ADR-0112`, `Plugin Trust & Isolation Architecture.md`); **not `WP 13.0B`**, which was in fact commissioned as an independent architecture review of that document instead (a disclosed divergence — see `WorkPackages.md`'s own `WP 13.0B` row and `Plugin Trust & Isolation Architecture.md`'s own Status header) — the real retrofit landed in `WP 13.2A`, independently reviewed `WP 13.2B`, re-verified end to end `WP 13.3A`/`WP 13.3B`, closing `TD-09`/`TD-10`/`TD-11` in full. **Corrected, `WP 13.11A`**: this field had drifted stale since `WP 13.0A` itself, still reading "implementation pending `WP 13.0B`" through `WP 13.1A`, `WP 13.2A`, `WP 13.3A` (which reconfirmed **Implemented** in this same field's own "Last Reviewed" narrative above without ever correcting this table cell), `WP 13.9.x`, and `WP 13.10A`–`WP 13.10C` — found and fixed directly by `WP 13.11A`'s own Governance/Documentation reviewer. |
| **Priority** | High — three Security Roadmap items (1, 2, 10) name this as a hard prerequisite before third-party plugins ship |
| **Business Value** | High once real third-party plugin authors exist; zero cost to defer while `src/Plugins/` remains empty |
| **Engineering Effort** | Medium — three call sites, one shared enforcement mechanism already built. `WP 13.0A` confirmed this estimate held at the architecture stage: no new mechanism was needed, only a capability model, a component-principal accessor, and a signing/trust-tier layer, all extending `IPermissionEvaluator` (`ADR-0044`) directly. |
| **Dependencies** | None technical; gated on a real trigger (a genuine third-party plugin) per Security Principle 7 (do not build security machinery ahead of real need) — **trigger fired**: the Product Owner's confirmed third-party plugin commitment, recorded at `WP 13.0.0`'s own branch establishment, is this capability's own commissioning event for `WP 13.0A`/`WP 13.0B`. |
| **Proposed Target Release** | `v0.13.0` — architecture complete (`WP 13.0A`); implemented `WP 13.2A` (**corrected, `WP 13.11A`** — not `WP 13.0B`, per Status above). No longer gated on `FCR-0002`'s own schedule; the Product Owner's commitment fired both together. |
| **Related ADRs** | ADR-0044, ADR-0110, ADR-0111, ADR-0112 |
| **Related Work Packages** | `WP 5.0S` (found), `WP 5.1A` (widened scope), `WP 6.1` (built the mechanism), `WP 6.8` (recommended for `v0.7.0`), `WP 13.0A` (designed the retrofit), `WP 13.2A` (implemented it — **corrected, `WP 13.11A`**, `WP 13.0B` was in fact repurposed to an independent architecture review, never an implementation) |
| **Academy Impact** | `07-plugin-architecture.md` extended in place (`WP 13.0A`) rather than a new `08-failure-isolation.md` case study, per that Work Package's own Governance & ADR Review finding that the trust question is a continuation of the plugin architecture article's own organising principle, not a new failure-isolation case. |
| **Notes** | Sourced from `Technical Debt Register.md` TD-09/TD-10/TD-11; `Security Roadmap.md` items 1, 2, 10; `WP6.8-platform-services-integration-review.md` §6. This is `v0.7.0` candidate `C3` in `docs/releases/v0.7.0/WorkPackages.md`, actually commissioned six releases later as `v0.13.0`'s own `WP 13.0A`/roadmap item `A-3` once its own gating trigger fired. |

#### FCR-0002 — Third-Party Plugin Ecosystem Enablement

| Field | Value |
|---|---|
| **Category** | Integrations |
| **Description** | Ship the first real, non-first-party plugin, exercising the Plugin Manifest infrastructure (`WP 4.2`) end to end with a genuine external author rather than a sample. |
| **Status** | Identified, not started — infrastructure and trust-isolation design now exist (`WP 13.0A`); no real plugin exists |
| **Priority** | Low until a concrete third-party plugin author or use case exists |
| **Business Value** | Unknown until a real plugin author's need is known |
| **Engineering Effort** | Unknown — depends entirely on the real plugin's own scope |
| **Dependencies** | `FCR-0001` — now **Implemented** (`WP 13.2A`, **corrected, `WP 13.11A`**, not `WP 13.0B` — see `FCR-0001`'s own updated Status); a real plugin can land against it today |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | ADR-0025, ADR-0026, ADR-0107, ADR-0108, ADR-0109 |
| **Related Work Packages** | `WP 4.2`, `WP 4.2B`, `WP 4.2C`, `WP 13.0A` (dependency-model, lifecycle, and DI-boundary architecture designed; still no real plugin) |
| **Academy Impact** | Would be this platform's first real "Plugin Register" entry (`Plugin Register.md`, currently empty by design, `AT-06`) |
| **Notes** | Sourced from `Technical Debt Register.md` AT-06; `Plugin Register.md`. `WP 13.0A` confirmed directly this entry's own precondition remains unmet: `src/Plugins/` is still empty, so `Plugin Register.md`'s own "Not Yet Applicable" status is correctly left untouched by that Work Package. |

#### FCR-0085 — Plugin Publisher Certificate Chain Validation & Revocation Checking (CRL/OCSP)

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Strengthen `ADR-0112`'s own detached-signature trust model beyond a flat, local `TrustedPublishers/` thumbprint match with a validity-window check: real X.509 certificate-chain validation against an issuing CA, and online or cached revocation checking (CRL/OCSP) so a compromised publisher key stops being trusted the moment it is revoked, not only once an operator manually removes its `.cer` file. |
| **Status** | Identified, not started |
| **Priority** | Low — no revocation incident, compromised-key event, or CA-chain requirement has ever occurred against this platform's plugin trust store |
| **Business Value** | Would grow substantially once a real third-party, non-self-issued publisher certificate exists — today's trust store holds only TempestOS's own first-party entry and test fixtures |
| **Engineering Effort** | Medium-High — chain validation and CRL/OCSP checking both require either a network call (a genuinely new attack/availability surface for a platform with no other network-facing runtime dependency today) or a maintained, periodically-refreshed local revocation cache; neither is a small addition to `PluginSignatureVerifier`'s current, entirely-offline design |
| **Dependencies** | `ADR-0112`'s existing signature/trust-store mechanism (shipped, `WP 13.2A`); would also benefit from `FCR-0003`'s own eventual network-facing-surface precedent if a network call is chosen |
| **Proposed Target Release** | Revisit trigger: `Security Roadmap.md` item 7 (a genuine network-facing surface) fires, or a real, demonstrated compromised-publisher-key incident occurs |
| **Related ADRs** | ADR-0112 (names both gaps directly in its own Negative consequences: "No revocation checking... a compromised publisher key remains trusted until an operator manually removes its `.cer` file") |
| **Related Work Packages** | `WP 13.0A` (designed the signing scheme this extends), `WP 13.2A` (implemented it, disclosed both gaps as accepted for this release), `WP 13.2B` (independently re-confirmed both gaps, judged out of scope for a defect-fixing review) |
| **Academy Impact** | Would warrant a new Security case study or a substantial extension to `07-plugin-architecture.md`'s own Trust Boundary section once designed |
| **Notes** | Sourced from `ADR-0112`'s own Negative consequences and Alternatives Considered ("No revocation checking (CRL/OCSP)... Accepted for this release: this platform has no network-facing surface today"); `WP13.2A-plugin-trust-and-capability-enforcement-implementation.md` §12 Future Evolution ("Certificate chains, revocation checking... all remain exactly as out of scope as `ADR-0110`–`ADR-0112`... named them"). First entry in this register for this capability — not previously tracked. |

#### FCR-0086 — Per-Plugin Collectible `AssemblyLoadContext` for Hot/Live Plugin Reload and In-Process Unload

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Adopt a collectible `AssemblyLoadContext` per plugin, enabling a plugin to be unloaded, upgraded, or reloaded without a full `TempestHost` process restart — the reserved `Loaded → Unloading → Unloaded` lifecycle seam `ADR-0108` names but deliberately leaves unbuilt, and the capability `ADR-0110` explicitly declines to unlock as a side effect of its own capability-scoped trust decision. |
| **Status** | Identified, not started |
| **Priority** | Low — both of `ADR-0108`'s own named preconditions are unmet: no per-plugin isolation mechanism has been adopted, and no real, demonstrated operational need for hot-upgrading a running plugin exists (`src/Plugins/` remains empty) |
| **Business Value** | Would grow once a real, shipped plugin's own restart-based upgrade downtime is measured and named as a genuine operational cost |
| **Engineering Effort** | High — collectible-context lifetime management and type-identity-across-context hazards, both explicitly named by `ADR-0110` as real, non-trivial complexity this release deliberately did not take on |
| **Dependencies** | Both preconditions `ADR-0108` itself names must be met together, not either alone: a per-plugin isolation mechanism (most plausibly this capability itself) **and** a real, demonstrated need for in-process plugin unload/upgrade |
| **Proposed Target Release** | Revisit trigger: a real, demonstrated need for in-process plugin unload or update-without-restart (`ADR-0108`/`ADR-0110`'s own named trigger, not yet fired) |
| **Related ADRs** | ADR-0108 (reserves the `Loaded → Unloading → Unloaded` seam, names the two-precondition trigger); ADR-0110 (explicitly declines to adopt a per-plugin `AssemblyLoadContext` for this release, on the separate grounds that it is not a genuine privilege boundary in modern .NET) |
| **Related Work Packages** | `WP 13.0A` (reserved the seam, designed the isolation-boundary decision that declines it for now), `WP 13.2A` (Future Evolution: "live plugin reload... remain[s] exactly as out of scope as `ADR-0110`–`ADR-0112`... named") |
| **Academy Impact** | Would warrant a substantial new Academy case study — this platform's first genuine in-process reset/teardown mechanism for any loaded component |
| **Notes** | Sourced from `ADR-0108`'s own "The reserved seam — why this is a defended non-goal, not an oversight" section and `ADR-0110`'s own "No per-plugin unload" Negative consequence and Alternatives Considered. First entry in this register for this capability — not previously tracked. |

#### FCR-0087 — Process-Separated Plugin Isolation for an Open, Unvetted Plugin Marketplace

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Adopt a genuine privilege boundary — a separate OS process per plugin, with IPC to the Host — for a future scenario where TempestOS runs genuinely adversarial, unvetted third-party code (an open marketplace with no publisher accountability), rather than the vetted, signed, commercial plugins `ADR-0110`'s capability-scoped enforcement model was proportioned against. |
| **Status** | Identified, not started |
| **Priority** | Low — `ADR-0110`'s own named revisit trigger ("TempestOS is asked to run genuinely adversarial, unvetted third-party code") has not fired; today's model is vetted, signed, commercial plugins only |
| **Business Value** | Would become High only if TempestOS's own commercial model shifts toward an open, unvetted plugin marketplace — unknown today |
| **Engineering Effort** | Very High — `ADR-0110` names this directly as "an order of magnitude larger than this Work Package's own brief": redesigning DI resolution, event dispatch, and every module constructor-injection point across an IPC boundary |
| **Dependencies** | A concrete open-marketplace business decision naming unvetted, non-accountable publishers as a real requirement — not a technical dependency |
| **Proposed Target Release** | Revisit trigger: a genuine, named commitment to an open, unvetted plugin marketplace (`ADR-0110`'s own explicit revisit trigger) |
| **Related ADRs** | ADR-0110 (evaluates and rejects process separation directly, for this release, naming this exact revisit trigger) |
| **Related Work Packages** | `WP 13.0A` (evaluated and rejected process separation as disproportionate to the disclosed threat), `WP 13.2A` (Future Evolution: "a marketplace... remain[s] exactly as out of scope... named") |
| **Academy Impact** | Would warrant a new Security case study on process-boundary plugin isolation, contrasting directly with `07-plugin-architecture.md`'s own capability-scoped model |
| **Notes** | Sourced from `ADR-0110`'s own "Why not process separation" Decision section and "Separate OS process per plugin, with IPC to the host" Alternatives Considered entry. First entry in this register for this capability — not previously tracked. |

#### FCR-0088 — Per-Plugin `AllowUnsignedLoad` Allow-list

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Replace `Plugins:AllowUnsignedLoad`'s current single, global switch with a per-plugin allow-list, so an operator enabling unsigned loading for one legitimately-unsigned internal tool does not implicitly permit every other unsigned candidate in the `Plugins/` folder to load under the same clamped Unsigned-Local ceiling. |
| **Status** | Identified, not started |
| **Priority** | Low — no operator has yet reported a real need for this finer granularity; `Plugins:AllowUnsignedLoad` defaults to `false` (fail-closed) regardless |
| **Business Value** | Would grow once a real deployment mixes a legitimately-unsigned internal tool with untrusted third-party unsigned candidates in the same `Plugins/` folder |
| **Engineering Effort** | Low — `ADR-0112`'s own Negative consequences names this as "purely additive future work" over the existing global-switch shape |
| **Dependencies** | `Plugins:AllowUnsignedLoad`/`PluginManifestDiscoveryService.AssignTrustTier` (shipped, `WP 13.2A`) |
| **Proposed Target Release** | Revisit trigger: a real operator need for finer per-plugin granularity |
| **Related ADRs** | ADR-0112 (names this exact gap and its own additive fix directly in its Negative consequences) |
| **Related Work Packages** | `WP 13.2A` (Future Evolution: "A per-plugin `AllowUnsignedLoad` allow-list, replacing the current single global switch, if a real operator need for finer granularity ever emerges — `ADR-0112`'s own Consequences section already names this as purely additive future work") |
| **Academy Impact** | None beyond a small update to `07-plugin-architecture.md`'s own configuration section once implemented |
| **Notes** | Sourced from `ADR-0112`'s own Negative consequences and `WP13.2A-plugin-trust-and-capability-enforcement-implementation.md` §12 Future Evolution. First entry in this register for this capability — not previously tracked. |

#### FCR-0003 — REST API Authentication Mechanism

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Design and implement a genuine authentication mechanism (API keys, OAuth/OIDC, or mutual TLS) for the REST API, replacing the current trusted, unauthenticated `X-Identity-Id` header. |
| **Status** | Identified, not started |
| **Priority** | High — this platform's first network-facing surface, currently mitigated only by binding to loopback |
| **Business Value** | High — required before any deployment beyond a trusted local network |
| **Engineering Effort** | Medium-High — a genuine architecture decision plus implementation, mirroring the rigor `WP 6.1`'s own identity model required |
| **Dependencies** | Builds on the existing Identity & Permissions model (`WP 6.1`); may need to revisit whether identity remains local-only (`ADR-0043`) once real authentication is designed |
| **Proposed Target Release** | Once a concrete deployment scenario beyond a trusted local network exists |
| **Related ADRs** | ADR-0043, ADR-0049, ADR-0052 |
| **Related Work Packages** | `WP 6.3` (disclosed the gap), `WP 6.8` (recommended for `v0.7.0`) |
| **Academy Impact** | Would warrant a new Academy concept guide or a substantial update to the REST API implementation retrospective |
| **Notes** | Sourced from `Technical Debt Register.md` TD-13; `WP6.3 Future Capability Recommendations.md` Recommendation 1; `Security Roadmap.md` item 6/7. `v0.7.0` candidate `C4` (paired with `FCR-0004`). |

#### FCR-0004 — REST API Transport Security (TLS)

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Configure TLS for the REST API's Kestrel listener. |
| **Status** | Identified, not started |
| **Priority** | High — paired with `FCR-0003`; both are named together as the same deployment-scenario trigger |
| **Business Value** | High once any deployment scenario beyond local development exists |
| **Engineering Effort** | Low-Medium — primarily configuration once a certificate/deployment story exists |
| **Dependencies** | Best designed alongside `FCR-0003`, not separately |
| **Proposed Target Release** | Once a concrete deployment scenario beyond local development exists |
| **Related ADRs** | ADR-0049 |
| **Related Work Packages** | `WP 6.3` (disclosed the gap), `WP 6.8` (recommended for `v0.7.0`) |
| **Academy Impact** | None beyond updating the REST API concept guide once resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-14; `WP6.3 Future Capability Recommendations.md` Recommendation 2. `v0.7.0` candidate `C4`. |

#### FCR-0005 — Governance Register Health-Check Tooling

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A lightweight, periodic (not only closing-review-triggered) check — a script or a documented convention — that flags a governance register whose own "Last Reviewed" Work Package predates the most recent Work Package that actually touched its subject area. |
| **Status** | **Implemented** (`WP 11.2A`, `v0.11.0` — `scripts/governance-healthcheck.ps1`) — corrected by `WP 11.9.0` after this field was found, on independent re-verification, still reading "Identified, not started" despite the tool having shipped one Work Package earlier. Eight automated checks (ADR Register, Academy Index, Release Register, Documentation Register, `PROJECT_STATUS.md`, `VERSION`, release-folder mandatory docs, Governance Index orphans), wired into CI as a non-required job. Not yet a required `CI Gate` check (deliberate, disclosed `WP 11.2A` scope limit) — so the drift pattern this entry exists to prevent is now machine-*detectable* but not yet machine-*enforced*; tracked as open technical debt, not as this entry's own remaining scope. |
| **Priority** | N/A — implemented. Historical priority context retained below for the record. Was **High** (raised from Medium by `WP 7.1F`, reconfirmed by `WP 7.4.0`) after recurring a fourth and fifth time: `Platform Services Register.md`/`Platform Service Map.md` (missing the four Engineering Foundation frameworks, found by `WP 7.3A`) and `Documentation Register.md`/`Governance Register.md` (both stale since `v0.6.0`/`WP 6.8` respectively, found by `WP 7.4.0`'s own release-readiness audit) — and recurred a **sixth and seventh** time after that (`WP 9.9.0` two passes) before finally being commissioned. |
| **Business Value** | Realised — the tool's own first live run (`WP 11.2A`) already caught two genuine, previously-undisclosed governance findings (Academy Index "Work Package Walkthroughs" stalling at `WP 7.3A`, ~50 missing retrospective links; four Documentation-Register path references to git-untrackable empty directories). `WP 11.9.0`'s own independent review found the drift pattern recurring an **eighth** time regardless — five further registers (Academy, Documentation, Release, Technical Debt, and this one) found stale relative to `v0.11.0`'s own seven Work Packages — precisely because the tool exists but is not yet a required gate and was not re-run after `WP 11.3A`/`WP 11.3B` landed. The tool's value is proven; its enforcement is not yet complete. |
| **Engineering Effort** | Delivered — see `WP11.2A Governance Health-Check Tool.md`. |
| **Dependencies** | None |
| **Proposed Target Release** | Delivered `v0.11.0` (`WP 11.2A`). Follow-on (not yet scheduled): promote to a required `CI Gate` check once its own currently-open findings are closed, so a stale register can no longer merge silently — this is the direct fix for the eighth recurrence `WP 11.9.0` found. |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 5.3` (first instance of this exact drift pattern), `WP 6.7`/`WP 6.6`/`WP 6.8` (second instance, closed), `WP 7.1A`–`WP 7.1E`/`WP 7.1F` (third instance, closed), `WP 7.3A` (fourth instance), `WP 7.4.0` (fifth instance), `WP 9.9.0` ×2 (sixth/seventh instances), `WP 11.2A` (delivered the tool), `WP 11.9.0` (eighth instance — found post-delivery, while independently correcting this very field) |
| **Academy Impact** | `06-governance-automation.md` (`WP 11.2A`) — delivered. |
| **Notes** | Sourced from `WP6.8-platform-services-integration-review.md` §6; re-confirmed and priority-raised by `WP7.1F Engineering Core Architecture Conformance Report.md` §7; further reconfirmed by `WP7.4.0 Release Readiness Report.md` and `WP9.9.0` (both passes). Delivered by `WP 11.2A`; this entry's own "Status" field was itself found stale by `WP 11.9.0` — the identical drift class, caught here by a human review rather than by the tool, since the tool does not yet audit this register. Corrected directly, not deferred, per this project's own standing practice of fixing a found-and-disclosed gap within the Work Package that found it wherever the fix is low-risk and well-bounded. |

#### FCR-0006 — `Runtime`↔`Diagnostics` Namespace Reference Resolution

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Formally resolve the one open architectural finding `WP 6.8` disclosed: `Tempest.Core.Diagnostics` imports `Tempest.Core.Runtime` for a single enum (`HostState`), a mutual namespace reference a literal reading of `ADR-0023` would flag. Either document it as an accepted, narrow `ADR-0023` exception, or relocate `HostState` to a neutral namespace. |
| **Status** | Identified, not started — shipped safely since `v0.5.0`, non-blocking |
| **Priority** | Medium — a genuine, disclosed architectural note, not urgent |
| **Business Value** | Low direct value; closes a standing architectural finding |
| **Engineering Effort** | Low — either an ADR documenting the exception, or a small, mechanical namespace move |
| **Dependencies** | None |
| **Proposed Target Release** | `v0.7.0` candidate |
| **Related ADRs** | ADR-0023 (the rule this finding is an exception to) |
| **Related Work Packages** | `WP 5.2` (introduced the reference), `WP 6.8` (found and disclosed it) |
| **Academy Impact** | Would warrant a small update to the Diagnostics concept guide once resolved |
| **Notes** | Sourced from `WP6.8-platform-services-integration-review.md` §6; `PROJECT_STATUS.md`'s own Current Work Package section. `v0.7.0` candidate `C1`. |

#### FCR-0007 — Native Query/Filter Capability for Persistence

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Extend `IPersistenceStore` (or introduce a companion abstraction) with native query/filter capability, rather than the current key-lookup-plus-full-enumeration shape `IAuditQuery` builds its own client-side filtering over today. |
| **Status** | Identified, not started |
| **Priority** | Low — deliberately not extended by `WP 6.5`; no measured performance problem exists yet |
| **Business Value** | Would grow with real data scale; currently unmeasured |
| **Engineering Effort** | Medium-High — a genuine abstraction change touching every `IPersistenceStore` consumer (Settings, Audit) |
| **Dependencies** | None technical; gated on a real, measured performance problem or a concrete scale requirement |
| **Proposed Target Release** | Not yet scheduled — explicit revisit trigger: a real, measured performance problem |
| **Related ADRs** | ADR-0041, ADR-0045 |
| **Related Work Packages** | `WP 6.4` (shipped the current shape), `WP 6.5` (confirmed the limitation via explicit Persistence Validation) |
| **Academy Impact** | Would warrant updates to the Settings/Audit concept guides once resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-12; `docs/releases/v0.6.0/Risk Register.md` R8; `WP6.5 Future Capability Recommendations.md` Recommendation 2. |

#### FCR-0008 — Legacy Logging Consolidation

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Resolve the coexistence of `ILogger` and the legacy `LoggingService` — either by migrating or deliberately deleting the legacy path. |
| **Status** | Identified, repeatedly reassessed, not started |
| **Priority** | Low — `Program.cs` has not called the legacy path since `WP 5.0D`; no behavioural benefit identified from migrating dead code |
| **Business Value** | Low unless the legacy path is genuinely revived |
| **Engineering Effort** | Low if deleted; Medium if migrated |
| **Dependencies** | None |
| **Proposed Target Release** | Revisit trigger: the legacy bootstrap code is either genuinely revived or deliberately deleted |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 2.6` (origin), `WP 5.2` (reassessed, `D-020`) |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-01. |

#### FCR-0009 — Disposal Tracking for DI-Registered Singletons

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Add disposal tracking for `AddInstance`-registered or reflection-constructed singletons implementing `IDisposable`. |
| **Status** | Identified, not started |
| **Priority** | Low — no current platform service is disposable |
| **Business Value** | Would grow if a future platform service holds an unmanaged or disposable resource |
| **Engineering Effort** | Medium — a DI container change |
| **Dependencies** | None |
| **Proposed Target Release** | Revisit trigger: a real disposable platform service is introduced |
| **Related ADRs** | ADR-0009 |
| **Related Work Packages** | `WP 2.4` |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-03. |

#### FCR-0010 — Configurable Plugin Root/Manifest Conventions

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Make the plugins root directory (`Plugins/`) and manifest file name (`plugin.manifest.json`) configurable rather than fixed conventions. |
| **Status** | **Implemented — `WP 13.1A`** (`Runtime:Plugins:RootDirectory`/`Runtime:Plugins:ManifestFileName`/`Runtime:Plugins:Disabled`, real and tested, `TempestHost.cs`) |
| **Priority** | Low — disclosed as a purely additive future enhancement |
| **Business Value** | Low until a real deployment scenario needs a different convention |
| **Engineering Effort** | Low — confirmed at implementation: both keys are optional, default to the existing fixed conventions, and are consulted at the seam `ADR-0026` already anticipated, requiring no Host Lifecycle phase change |
| **Dependencies** | None |
| **Proposed Target Release** | `v0.13.0` — architecture complete (`WP 13.0A`); implementation complete (`WP 13.1A`) |
| **Related ADRs** | None (a configuration convention, not itself ADR-worthy — see `Plugin Platform Architecture.md`, Architectural Questions Evaluated) |
| **Related Work Packages** | `WP 4.2`, `WP 13.0A`, `WP 13.1A` |
| **Academy Impact** | `docs/academy/02 Runtime Architecture/07-plugin-architecture.md` updated (`WP 13.1A`) |
| **Notes** | Sourced from `Technical Debt Register.md` TD-06, now Resolved. |

#### FCR-0011 — `IHostedService` Naming Disambiguation

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Resolve the naming proximity between `Tempest.Core.BackgroundServices.IHostedService` and `Microsoft.Extensions.Hosting.IHostedService`, now that both coexist in the same solution (`WP 6.3`'s ASP.NET Core dependency). |
| **Status** | Identified — revisit trigger arguably now met |
| **Priority** | Low-Medium — no confusion has actually been reported yet |
| **Business Value** | Low direct value; reduces a real, now-materialised naming-collision risk |
| **Engineering Effort** | Low-Medium — a rename touches every implementer |
| **Dependencies** | None |
| **Proposed Target Release** | Not yet scheduled — a judgment call for a future Work Package |
| **Related ADRs** | ADR-0024, ADR-0049 |
| **Related Work Packages** | `WP 4.0` (origin), `WP 6.3` (trigger arguably met) |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-04. |

#### FCR-0012 — Reporting Delivery-Channel, History & Scheduling Capability

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A separate consuming layer over `IReportingService.GenerateAsync` providing delivery (email/webhook/push), durable report history, generation progress/streaming for long-running renderers, and scheduled/recurring generation — explicitly not a change to `IReportingService` itself. |
| **Status** | Identified, not started |
| **Priority** | Low — no concrete delivery, history, or scheduling requirement exists yet |
| **Business Value** | Would grow with a real consuming engineering module naming a concrete need |
| **Engineering Effort** | Medium-High — likely several separable capabilities, not one |
| **Dependencies** | `IReportingService` (shipped, `WP 6.0`); likely benefits from `FCR-0013`'s own delivery-channel work if built together |
| **Proposed Target Release** | Revisit trigger: a real, demonstrated need |
| **Related ADRs** | ADR-0040 |
| **Related Work Packages** | `WP 6.0` |
| **Academy Impact** | Would warrant a new Reporting concept-guide section once any part ships |
| **Notes** | Sourced from `Technical Debt Register.md` AT-09; `WP6.0 Future Capability Recommendations.md` Recommendations 4 and 5. |

#### FCR-0013 — Notification History/Inbox & Delivery-Channel Capability

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A durable notification history/inbox (a Shell notification centre, or an audit-adjacent record of what was shown to a user), plus first-party delivery-channel handler implementations (email, webhook, push), reusing `NotificationSeverity`/`Category` rather than a parallel classification. |
| **Status** | Identified, not started |
| **Priority** | Low — an explicit, approved-contract scope exclusion for `v0.6.0`, not a current defect |
| **Business Value** | Would grow with a real UI Shell notification-centre requirement or external delivery need |
| **Engineering Effort** | Medium |
| **Dependencies** | `NotificationDispatcher` (shipped, `WP 6.2`); may integrate with `FCR-0003`/REST API for webhook/callback delivery |
| **Proposed Target Release** | Revisit trigger: a real, demonstrated need |
| **Related ADRs** | ADR-0046 |
| **Related Work Packages** | `WP 6.2` |
| **Academy Impact** | Would warrant a new Notifications concept-guide section once any part ships |
| **Notes** | Sourced from `Technical Debt Register.md` AT-08; `WP6.2 Future Capability Recommendations.md` Recommendations 2, 3, 4, 5. |

#### FCR-0014 — Advanced Settings Capability (Sensitive Values, Per-Principal, Strongly-Typed)

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Three related, separately-scoped Settings enhancements: a sensitive-value flag on `ISettingDefinition` (once a real sensitive setting is named); per-principal (user-specific) settings distinct from global ones; and a strongly-typed settings abstraction beyond the current key-lookup shape. |
| **Status** | Identified, not started |
| **Priority** | Low — each has its own named trigger, none met yet |
| **Business Value** | Would grow once a real sensitive setting, per-user preference, or type-safety need is named |
| **Engineering Effort** | Low (sensitive-value flag) to Medium (per-principal, strongly-typed) |
| **Dependencies** | `ISettingsService`/`IPersistenceStore` (shipped, `WP 6.4`); per-principal settings would depend on `WP 6.1`'s Identity model |
| **Proposed Target Release** | Revisit trigger: a real sensitive setting, per-user preference, or type-safety need is named |
| **Related ADRs** | ADR-0041, ADR-0042 |
| **Related Work Packages** | `WP 6.4` |
| **Academy Impact** | Would warrant Settings concept-guide updates once any part ships |
| **Notes** | Sourced from `WP6.4 Future Capability Recommendations.md` Recommendations 2 and 3. |

#### FCR-0015 — Export Artifact Compression & Encryption

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Compression and/or encryption of exported artifact content, currently the individual responsibility of each `IExportable` implementation with no content-level policy imposed by `IExportService`/`IImportService`. |
| **Status** | Identified, not started |
| **Priority** | Low — an explicit, approved-contract scope exclusion for `v0.6.0` |
| **Business Value** | Would grow with a concrete deployment scenario naming artifact confidentiality or size as a requirement |
| **Engineering Effort** | Medium |
| **Dependencies** | `IExportService`/`IImportService` (shipped, `WP 6.7`) |
| **Proposed Target Release** | Revisit trigger: a real, demonstrated need |
| **Related ADRs** | ADR-0051 |
| **Related Work Packages** | `WP 6.7` |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` AT-11; `WP6.7 Future Capability Recommendations.md`. |

#### FCR-0016 — Export Schema Migration/Upgrade Path

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A migration path for `IImportService.ImportAsync` to upgrade or downgrade an artifact section whose schema version does not exactly match the currently registered `IImportable`, rather than rejecting outright. |
| **Status** | Identified, not started |
| **Priority** | Low — no real, shipped schema version bump exists yet |
| **Business Value** | Would grow once a real backward-compatibility requirement exists |
| **Engineering Effort** | Medium-High |
| **Dependencies** | `IExportService`/`IImportService` (shipped, `WP 6.7`) |
| **Proposed Target Release** | Revisit trigger: a real, shipped schema version bump with a genuine backward-compatibility requirement |
| **Related ADRs** | ADR-0051 |
| **Related Work Packages** | `WP 6.7` |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` AT-12; `WP6.7 Future Capability Recommendations.md` Recommendation 3. |

#### FCR-0017 — License File Integrity Verification (Cryptographic Signature)

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Cryptographic signature or tamper-resistance verification of license file contents, currently trusted at face value. |
| **Status** | Identified, not started |
| **Priority** | Low — no concrete distribution channel or tamper/forgery threat model exists yet |
| **Business Value** | Would grow once a real license distribution channel exists |
| **Engineering Effort** | Medium |
| **Dependencies** | `ILicenseValidator`/`ILicenseProvider` (shipped, `WP 6.6`) |
| **Proposed Target Release** | Revisit trigger: a concrete license distribution scenario naming a real tamper/forgery threat model |
| **Related ADRs** | ADR-0050 |
| **Related Work Packages** | `WP 6.6` |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-16; `WP6.6 Future Capability Recommendations.md` Recommendation 2. |

#### FCR-0018 — REST Request-Parameter Binding

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Thread an inbound REST request's own body or query string into the invoked command, rather than every REST-exposed command dispatching only its own parameterless `CreateDefault` factory instance. |
| **Status** | Identified, not started |
| **Priority** | Low — an explicit, approved-contract scope exclusion for `v0.6.0` |
| **Business Value** | Would grow with a real REST-exposed command needing caller-supplied parameters |
| **Engineering Effort** | Medium |
| **Dependencies** | `IApiEndpointRegistry`/`RestApiHostedService` (shipped, `WP 6.3`) |
| **Proposed Target Release** | Revisit trigger: a real, demonstrated need |
| **Related ADRs** | ADR-0047, ADR-0048 |
| **Related Work Packages** | `WP 6.3` |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` AT-10; `WP6.3 Future Capability Recommendations.md` Recommendation 4. |

#### FCR-0019 — Explicit Actor Parameter for Cross-Boundary Audit Attribution

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | For any future command needing precise per-request Audit attribution under REST invocation, accept an explicit actor parameter rather than relying on `IAuditRecorder.RecordAsync`'s own ambient-current-principal auto-attribution, which the REST API deliberately never establishes (`ADR-0052`). |
| **Status** | Identified, not started |
| **Priority** | Low — the caller's real identity is not lost, only recorded in a different Audit entry's `Detail` field |
| **Business Value** | Would grow with a real command needing precise per-command REST-invoked Audit attribution |
| **Engineering Effort** | Low-Medium |
| **Dependencies** | `IAuditRecorder` (shipped, `WP 6.5`); `RestApiHostedService` (shipped, `WP 6.3`) |
| **Proposed Target Release** | Revisit trigger: a real, demonstrated need |
| **Related ADRs** | ADR-0045, ADR-0052 |
| **Related Work Packages** | `WP 6.3`, `WP 6.5` |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-15; `WP6.3 Future Capability Recommendations.md` Recommendation 3. |

### Infrastructure

#### FCR-0020 — Secrets-Redaction Logging Convention

| Field | Value |
|---|---|
| **Category** | Infrastructure |
| **Description** | A redaction convention (a marker attribute, a wrapper type, or an `ILogSink`-level filter) for any credential, token, or connection string entering the platform, adopted before the platform's first real secret exists rather than retrofitted after. |
| **Status** | Identified, not started |
| **Priority** | Medium — trigger is authentication (`FCR-0003`) or cloud synchronisation (`FCR-0022`), neither of which exists yet |
| **Business Value** | High once a real secret exists in the codebase; near-zero before then |
| **Engineering Effort** | Low-Medium |
| **Dependencies** | Should land before `FCR-0003` or `FCR-0022`, whichever arrives first |
| **Proposed Target Release** | Revisit trigger: authentication or cloud synchronisation introduces the platform's first real secret |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 5.0S` (found, `SEC-02`) |
| **Academy Impact** | Would warrant a new Security case study once designed |
| **Notes** | Sourced from `Security Roadmap.md` item 3. |

#### FCR-0021 — Multi-User / Tenant Isolation Architecture

| Field | Value |
|---|---|
| **Category** | Infrastructure |
| **Description** | A deliberate, ADR-backed decision on how TempestOS achieves multi-user isolation — separate OS processes per user (no DI change) versus a genuine Scoped DI lifetime and per-tenant isolation model (a DI redesign) — before multi-user support is implemented. |
| **Status** | Identified, not started |
| **Priority** | Medium-High strategic value, but explicitly gated — Security Principle 7 forbids building this ahead of real need |
| **Business Value** | High — a prerequisite for any real multi-user deployment |
| **Engineering Effort** | High — potentially a DI container redesign |
| **Dependencies** | The current DI container has no Scoped lifetime at all (`FR-1`); this decision determines whether that changes |
| **Proposed Target Release** | Not yet scheduled — trigger: multi-user support becomes a real, scheduled requirement |
| **Related ADRs** | None yet — this capability's own first deliverable is an ADR |
| **Related Work Packages** | `WP 5.0S` (found, `FR-1`) |
| **Academy Impact** | Would be a major new architectural decision warranting its own Academy case study |
| **Notes** | Sourced from `Security Roadmap.md` item 5; `Threat Model.md` assumption 4. |

#### FCR-0022 — Cloud Synchronisation

| Field | Value |
|---|---|
| **Category** | Infrastructure |
| **Description** | Synchronisation of TempestOS data with a cloud service — no design work exists yet; `Threat Model.md` explicitly names this as one of the assumptions this platform's eventual mission requires planning around. |
| **Status** | Identified, no readiness work — explicitly furthest from the current codebase |
| **Priority** | Low until a concrete design is proposed |
| **Business Value** | Unknown — no concrete scenario named yet |
| **Engineering Effort** | Unknown |
| **Dependencies** | Likely depends on `FCR-0021` (multi-user/tenant model) and `FCR-0003` (authentication) |
| **Proposed Target Release** | Not yet scheduled — `Security Principles.md` Principle 7 explicitly recommends against speculative design |
| **Related ADRs** | None |
| **Related Work Packages** | None yet |
| **Academy Impact** | None until designed |
| **Notes** | Sourced from `Threat Model.md` assumption 8; `Security Roadmap.md`'s own Explicit Non-Recommendations. |

#### FCR-0023 — Offline Synchronisation & Mobile Client Support

| Field | Value |
|---|---|
| **Category** | Infrastructure |
| **Description** | Support for non-desktop clients and offline synchronisation — explicitly named as the capability furthest from anything in the current codebase. |
| **Status** | Identified, no readiness work recommended yet |
| **Priority** | Low |
| **Business Value** | Unknown — no concrete scenario named yet |
| **Engineering Effort** | Unknown, likely High |
| **Dependencies** | Likely depends on `FCR-0022` (cloud synchronisation) |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | None |
| **Related Work Packages** | None yet |
| **Academy Impact** | None until designed |
| **Notes** | Sourced from `Security Roadmap.md` item 9. |

### AI

#### FCR-0024 — AI/Automation Command Invocation

| Field | Value |
|---|---|
| **Category** | AI |
| **Description** | A future AI service or automation script enumerating `ICommandRegistry.Items`, filtering by permission, and invoking by Id — a capability the Command Framework's own design has anticipated as a caller since its own architecture phase, requiring no AI-specific or automation-specific mode of the framework itself. |
| **Status** | Identified — the framework already supports this caller shape; no concrete AI/automation consumer exists yet |
| **Priority** | Low until a concrete AI/automation consumer is proposed |
| **Business Value** | Would depend entirely on the concrete AI/automation use case proposed |
| **Engineering Effort** | Low for the framework itself (already supports Id-based invocation); effort lies entirely in the AI/automation consumer itself, out of this register's own scope |
| **Dependencies** | `ICommandDispatcher`/`ICommandRegistry` (shipped, `WP 5.1A`/`WP 5.1B`) |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | ADR-0036, ADR-0037, ADR-0038 |
| **Related Work Packages** | `WP 5.1A`, `WP 5.1B` |
| **Academy Impact** | `11-command-framework.md` already documents this future-caller shape; a real consumer would warrant a new case study |
| **Notes** | Sourced directly from `docs/architecture/Command Framework Architecture.md`'s own repeated "future AI service" framing (its own "Future: AI Invocation, Automation, Scripting" section) and `Engineering Glossary.md`. This is the one capability in this register describing an already-supported extension point rather than a gap. |

### Commercial

#### FCR-0025 — Commercial Licensing Model (Remote Activation, Floating/Seat-Based, Renewal)

| Field | Value |
|---|---|
| **Category** | Commercial |
| **Description** | Remote validation/activation, floating/seat-based licensing, and a license-renewal/grace-period model — all deliberately out of `WP 6.6`'s own scope, which built licensing *capability* (`ILicenseProvider`) without any commercial *policy*. |
| **Status** | Identified, not started |
| **Priority** | Low — no concrete deployment scenario naming any of the three exists yet |
| **Business Value** | Would grow substantially once TempestOS has real paying customers with a concrete licensing-model requirement |
| **Engineering Effort** | Medium-High — a genuine commercial-policy layer built on top of the existing mechanism |
| **Dependencies** | `ILicenseValidator`/`ILicenseProvider` (shipped, `WP 6.6`); would also benefit from `FCR-0017`'s own integrity verification |
| **Proposed Target Release** | Revisit trigger: a real, demonstrated need for remote activation, concurrent-seat limits, or graceful expiry handling |
| **Related ADRs** | ADR-0050 |
| **Related Work Packages** | `WP 6.6` |
| **Academy Impact** | Would warrant a new Licensing concept-guide section once any part ships |
| **Notes** | Sourced from `Technical Debt Register.md` AT-13; `WP6.6 Future Capability Recommendations.md` Recommendation 4. |

#### FCR-0026 — Defence-Sector / Regulated-Environment Compliance Readiness

| Field | Value |
|---|---|
| **Category** | Commercial |
| **Description** | Compliance posture for operating TempestOS within defence or similarly regulated organisations — the bootstrap-era, currently-dead `ProjectModel` already models a `SecurityLevel` field defaulting to `"BPSS"` (the UK Baseline Personnel Security Standard), a strong, concrete signal of original intent even though the code is currently unreferenced. |
| **Status** | Identified — no active design work; the only concrete trace is dead code |
| **Priority** | Low until a real defence-sector customer or requirement is named |
| **Business Value** | Unknown — entirely dependent on whether a real defence-sector opportunity materialises |
| **Engineering Effort** | Unknown — likely substantial, spanning data classification, access control, and audit requirements well beyond this platform's current Audit Framework |
| **Dependencies** | `FCR-0029` (Project Engine / Secure Project Data Management) most directly, since classification/export-control fields already live in the same dormant subsystem |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | None — this is inferred from dormant, bootstrap-era code, not a Work Package finding |
| **Academy Impact** | None until designed |
| **Notes** | Sourced from `Threat Model.md` assumption 10 and its own note on `ProjectModel`'s `Classification`/`SecurityLevel`/`ExportControlled`/`Customer`/`ContractNumber` fields; `Platform Security Review v0.5.0.md`, File System section. Marked **Inferred**, not Verified — no document confirms this is an actual, current business objective, only that the original bootstrap-era code modelled toward it. |

### Engineering Foundation (Cross-Cutting)

Five entries below were identified by `WP 7.0B`'s own Capability
Dependency Analysis, not sourced from a prior document naming them
directly — each is marked **Inferred**: architectural necessity,
reasoned from what any Engineering Discipline module would structurally
require to be built at all, mirroring how `Capability Categories.md`
itself defines the Engineering Discipline categories. This is
architecture/planning reasoning about shared technical substrate, not
an invented business capability within a discipline (no Mechanical,
Structural, Electrical, Building Services/HVAC, or Manufacturing
capability is registered below, or anywhere in this register — see
Coverage Note).

#### FCR-0029 — Engineering Data Model & Document Management Foundation

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A shared engineering-entity data model — documents, revisions, references, and their relationships — that Requirements, Project, and every future discipline module build on rather than each inventing its own storage shape, mirroring `ADR-0041`'s own "one shared Persistence abstraction, not reinvented per service" precedent for Settings/Audit. |
| **Status** | **Implemented (`WP 7.1A`)** — `Tempest.Core.EngineeringData`, `EngineeringDocumentStore`, built on `IPersistenceStore` per `ADR-0053` |
| **Priority** | High relative to other unscheduled capabilities — almost every Engineering Foundation and Engineering Module capability depends on it |
| **Business Value** | Unknown in isolation; high as an enabler, since `FCR-0027`, `FCR-0028`, `FCR-0031`, `FCR-0033`, and every future discipline module would otherwise each invent an incompatible storage shape |
| **Engineering Effort** | Delivered — one Work Package (`WP 7.1A`), 13 new production files, 36 new tests |
| **Dependencies** | None upstream; built on `IPersistenceStore` (`WP 6.4`) per `ADR-0053` — `FCR-0007`'s own query-capability gap was avoided, not inherited, for this framework's own access pattern |
| **Proposed Target Release** | Shipped, `v0.7.0` (pending release) |
| **Related ADRs** | ADR-0013, ADR-0041, ADR-0053 |
| **Related Work Packages** | `WP 7.1A` |
| **Academy Impact** | `WP7.1A-engineering-data-model-implementation.md`; `docs/engineering/Engineering Principles.md` |
| **Notes** | Inferred from `Threat Model.md` assumption 1's own generic "CAD, requirements, analysis, verification records" framing and the shared-storage need `FCR-0027`/`FCR-0028` both independently implied in `WP 7.0A`. Not sourced from any document naming this capability directly. Implemented `WP 7.1A` exactly as `WP7.0C Engineering Foundation Contracts.md` proposed, with one disclosed, minor deviation (see `WP7.1A Implementation Report.md`). |

#### FCR-0030 — Units & Quantities Framework

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A shared representation for dimensioned physical quantities (length, force, and so on) and unit conversion between them, usable by every future Engineering Discipline module rather than each implementing its own conversion logic. |
| **Status** | **Implemented (`WP 7.1B`)** — `Tempest.Core.UnitsAndQuantities`, `Quantity<TDimension>`/`Unit<TDimension>`, per `ADR-0054`. Temperature (an affine, not purely multiplicative, dimension) deliberately deferred — see `FCR-0034`. |
| **Priority** | High relative to other unscheduled capabilities — a prerequisite for `FCR-0032` and for any Mechanical/Structural/Electrical/HVAC/Materials/Manufacturing capability, once one is identified |
| **Business Value** | Unknown in isolation; mitigates a well-known, industry-wide class of defect (unit-conversion error) that becomes possible the moment more than one discipline module performs a physical calculation |
| **Engineering Effort** | Delivered — one Work Package (`WP 7.1B`), 20 new production files, 67 new tests |
| **Dependencies** | None upstream — the only Engineering Foundation framework with zero Platform Service dependency and no DI registration of any kind, confirmed by implementation |
| **Proposed Target Release** | Shipped, `v0.7.0` (pending release) |
| **Related ADRs** | ADR-0054 |
| **Related Work Packages** | `WP 7.1B` |
| **Academy Impact** | `WP7.1B-units-and-quantities-framework-implementation.md`; `docs/engineering/Engineering Principles.md` (Principles 7-12); new concept guide (phantom-type-style dimension safety) |
| **Notes** | Inferred purely from `Capability Categories.md`'s own Engineering Discipline list — no prior document names this capability directly, the weakest-sourced entry in this register alongside `FCR-0031`/`FCR-0032` prior to implementation. Implemented `WP 7.1B` exactly as `WP7.0C Engineering Foundation Contracts.md` proposed, extended (not changed) with arithmetic, comparison, formatting, parsing, and JSON serialization support, per this Work Package's own controlling instruction. One disclosed scope boundary: Temperature deferred (`FCR-0034`), not a deviation from the approved contract. |

#### FCR-0031 — Materials Framework

| Field | Value |
|---|---|
| **Category** | Materials |
| **Description** | Material specification, provenance, and traceability capability shared across disciplines — the first identified candidate for the previously-empty `Materials` category in `Capability Categories.md`. Engineering data only — no material selection algorithm, design allowable, or calculation. |
| **Status** | **Implemented (`WP 7.1C`)** — `Tempest.Core.Materials`, `MaterialCatalog`, built on `Tempest.Core.EngineeringData` and `Tempest.Core.UnitsAndQuantities` per `ADR-0055` |
| **Priority** | Medium — enables the `Materials` and `Manufacturing` discipline categories and supports `Quality` (material traceability is a common non-conformance/inspection concern), but not a hard prerequisite for `FCR-0027`/`FCR-0028` |
| **Business Value** | Unknown in isolation; high as an enabler once a real Materials/Manufacturing discipline module needs shared material data rather than inventing its own |
| **Engineering Effort** | Delivered — one Work Package (`WP 7.1C`), 14 new production files, 55 new tests |
| **Dependencies** | `FCR-0029` (data model, implemented `WP 7.1A`), `FCR-0030` (units, implemented `WP 7.1B`) — both now real, not merely approved contracts |
| **Proposed Target Release** | Shipped, `v0.7.0` (pending release) |
| **Related ADRs** | ADR-0013, ADR-0053, ADR-0054, ADR-0055 |
| **Related Work Packages** | `WP 7.1C` |
| **Academy Impact** | `WP7.1C-materials-framework-implementation.md`; `docs/engineering/Engineering Principles.md` (Principles 13-16) |
| **Notes** | Inferred purely from the existence of the `Materials` category in `Capability Categories.md` and the structural observation that a `Manufacturing` or `Materials` discipline module cannot exist without some shared material-data capability. Not sourced from any document naming a concrete Materials capability at planning time. Implemented `WP 7.1C` exactly as `WP7.0C Engineering Foundation Contracts.md` proposed, extended (not changed) with a structured, provenance-carrying property type resolving `ADR-0055`'s own reserved question, and one disclosed implementation finding (a direct `IPersistenceStore` dependency for its own `materialId` index — see `ADR-0055` Decision 3). |

#### FCR-0032 — Engineering Calculation Framework

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A shared calculation/formula execution model usable by every future discipline module (a structural load calculation, an HVAC sizing calculation, an electrical load calculation), mirroring the Command Framework's own "one dispatch mechanism, not reinvented per consumer" precedent (`ADR-0037`/`ADR-0038`), rather than each discipline module inventing its own ad hoc computation approach. |
| **Status** | **Implemented (`WP 7.1D`)** — `Tempest.Core.Calculations`, `CalculationEngine`, built on `Tempest.Core.EngineeringData` per `ADR-0056`; consumes `Tempest.Core.UnitsAndQuantities` by convention |
| **Priority** | High relative to other unscheduled capabilities — a prerequisite for any Mechanical/Structural/Electrical/HVAC capability, once one is identified |
| **Business Value** | Unknown in isolation; prevents each future discipline module from independently reinventing calculation infrastructure |
| **Engineering Effort** | Delivered — one Work Package (`WP 7.1D`), 17 new production files, 52 new tests |
| **Dependencies** | `FCR-0030` (Units & Quantities, implemented `WP 7.1B`) — every dimensioned calculation is expected, by convention, to use `Quantity<TDimension>`; `FCR-0029` (Engineering Data Model, implemented `WP 7.1A`) — every execution is durably recorded as an `IEngineeringDocument` |
| **Proposed Target Release** | Shipped, `v0.7.0` (pending release) |
| **Related ADRs** | ADR-0013, ADR-0053, ADR-0054, ADR-0056 |
| **Related Work Packages** | `WP 7.1D` |
| **Academy Impact** | `WP7.1D-engineering-calculation-framework-implementation.md`; new concept guide (Calculation vs. Command distinction); `docs/engineering/Engineering Principles.md` (Principles 17-23) |
| **Notes** | Inferred purely from `Capability Categories.md`'s own Engineering Discipline list, the same basis as `FCR-0030`/`FCR-0031` — no prior document named this capability directly at planning time. Implemented `WP 7.1D` extended substantially beyond `WP7.0C Engineering Foundation Contracts.md`'s own illustrative shape (metadata, assumptions, constraints, a validation model, an execution context, material references) to satisfy this Work Package's own "engineering evidence, not merely a numerical answer" requirement — resolved in `ADR-0056`, not an unauthorised deviation. First Engineering Foundation Work Package to include a dedicated Security Review. |

#### FCR-0033 — Verification Framework

| Field | Value |
|---|---|
| **Category** | Quality |
| **Description** | A cross-cutting mechanism for recording a pass/fail/conditional verification against an engineering document, usable by every discipline's own quality process — the first identified candidate for the previously-empty `Quality` category in `Capability Categories.md`, and distinct from Audit (who did what), a Calculation Record (what was computed), and Reporting (presentation). Answers "has this engineering claim been demonstrated?" — deliberately excludes Validation and Requirements Management. |
| **Status** | **Implemented (`WP 7.1E`)** — `Tempest.Core.Verification`, `VerificationService`, built on `Tempest.Core.EngineeringData` per `ADR-0057`; requires no dependency on Requirements Engine (`FCR-0027`), Calculations, Units & Quantities, or Materials |
| **Priority** | Medium-High — `Threat Model.md` assumption 1 names "verification records" directly as engineering IP TempestOS will eventually manage |
| **Business Value** | Unknown in isolation; strengthens `FCR-0027` (Requirements Engine) directly, since a requirement without a verification record against it is only half the traceability chain `FCR-0027`'s own description names |
| **Engineering Effort** | Delivered — one Work Package (`WP 7.1E`), 9 new production files (the smallest of the five Engineering Foundation frameworks), 49 new tests |
| **Dependencies** | `FCR-0029` (data model, implemented `WP 7.1A`) — the only hard dependency, confirmed by implementation, not `FCR-0027` (Requirements Engine), exactly as `WP7.0C Cross-Framework Dependency Report.md`'s own clarification anticipated |
| **Proposed Target Release** | Shipped, `v0.7.0` (pending release) — **this completes the Engineering Foundation programme**, all five frameworks (`FCR-0029`–`FCR-0033`) now Implemented |
| **Related ADRs** | ADR-0013, ADR-0045, ADR-0053, ADR-0057 |
| **Related Work Packages** | `WP 7.1E` |
| **Academy Impact** | `WP7.1E-verification-framework-implementation.md`; new concept guide (Verification vs. Audit vs. Calculation Record); `docs/engineering/Engineering Principles.md` (Principles 24-28) |
| **Notes** | Inferred from `Threat Model.md` assumption 1's explicit "verification records" phrase and `FCR-0027`'s own description naming "verification" as part of the Requirements Engine's scope. Implemented `WP 7.1E` exactly as `WP7.0C Engineering Foundation Contracts.md` proposed, extended (not changed to `subjectDocumentId`/`outcome`/`method`) with a structured `VerificationContext` resolving `ADR-0057`'s own reserved question, and one disclosed implementation finding: verification history is queried via the Data Model's own existing `LinkAsync`/`GetReferencesAsync` mechanism, needing no new index or direct `IPersistenceStore` dependency at all — the simplest dependency shape of any Engineering Foundation framework. |

#### FCR-0034 — Affine Unit Conversion (Temperature and Similar Dimensions)

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Extend `Tempest.Core.UnitsAndQuantities` to support dimensions whose conversion is affine (both a scale and an offset), not purely multiplicative — Temperature (Celsius↔Fahrenheit) is the canonical example, deliberately excluded from `WP 7.1B`'s own starting catalogue because `Unit<TDimension>.ToBaseUnitFactor` supports only a single multiplicative factor. |
| **Status** | **Implemented (`Group A`, `ADR-0125`)** — `Unit<TDimension>` gained an optional `ToBaseUnitOffset` (defaulting to zero, so every existing unit is unchanged), `Quantity<TDimension>.ConvertTo` now converts through the base unit, `TemperatureUnits` holds kelvin/Celsius/Rankine/Fahrenheit, and arithmetic on an affine quantity throws rather than returning a plausible-looking wrong answer. The revisit trigger this entry named — "a real discipline module naming a Temperature requirement" — fired three times at once: A1 records service-temperature limits, A5 an operating-temperature range, A7 a process temperature band. |
| **Priority** | Medium — no discipline module currently needs Temperature; becomes High the moment one does (HVAC and Materials are the most likely first consumers) |
| **Business Value** | Would grow substantially once any Mechanical/HVAC/Materials capability is designed — nearly every thermal or material-science calculation needs Temperature |
| **Engineering Effort** | Medium — either an optional offset term on `Unit<TDimension>` (touching every existing dimension's own conversion arithmetic to confirm the offset defaults correctly to zero) or a parallel affine-unit type; a genuine design decision, not yet made |
| **Dependencies** | `FCR-0030` (Units & Quantities, shipped `WP 7.1B`) |
| **Proposed Target Release** | Not yet scheduled — revisit trigger: a real discipline module naming a Temperature requirement |
| **Related ADRs** | ADR-0054 (names this gap explicitly, "Temperature Deliberately Deferred"); **ADR-0125** (resolves it, and records why affine arithmetic is refused rather than silently converted) |
| **Related Work Packages** | `WP 7.1B` (found and disclosed, not resolved); `Group A` (resolved) |
| **Academy Impact** | Would warrant an update to the Units & Quantities concept guide, now that the design exists |
| **Notes** | Sourced from `Technical Debt Register.md` TD-19; `ADR-0054`. A genuine architectural finding discovered during implementation, not present in any prior planning document — `WP 7.0B`/`WP 7.0C` both discussed Units & Quantities only at the category/contract level, neither anticipated the multiplicative-only representation would need this exclusion until real unit catalogues were actually written. **Resolved `Group A`** by the first of the two options this entry itself named — an optional offset term on `Unit<TDimension>`, purely additive. The second design question this entry did not anticipate, and which `ADR-0125` decides: affine units break arithmetic in a way multiplicative units do not, so `+`, `-`, `*` and `/` refuse an affine operand rather than returning a number that looks right. One boundary remains open and is disclosed rather than closed: a *temperature difference* is a distinct quantity from an absolute temperature and is not modelled — a source quoting one records it in kelvin, where the magnitude is identical and the arithmetic correct. |

#### FCR-0035 — Calculation Execution Timeout & Cancellation Support

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Extend `Tempest.Core.Calculations` so a long-running or blocking `ICalculationDefinition.Calculate` can be cancelled once dispatch has begun — `Calculate` currently carries no `CancellationToken`, matching the approved contract's own signature, which had none either. |
| **Status** | Identified (`WP 7.1D`) — found during this Work Package's own required Security Review, not anticipated by prior planning |
| **Priority** | Low — no current calculation definition is long-running; calculation definitions remain trusted, first-party, in-process code, the same trust boundary the Command Framework already operates under without cancellation reaching into a handler either |
| **Business Value** | Would grow once a real, long-running calculation (or exposure to an external, network-facing caller) demonstrates a genuine need for cooperative cancellation |
| **Engineering Effort** | Medium — changing `Calculate`'s own signature a second time, after `WP 7.1D` already changed it once, would need care to avoid repeated breakage for any consumer registered by then |
| **Dependencies** | `FCR-0032` (Engineering Calculation Framework, shipped `WP 7.1D`) |
| **Proposed Target Release** | Not yet scheduled — revisit trigger: a real, demonstrated need |
| **Related ADRs** | ADR-0056 |
| **Related Work Packages** | `WP 7.1D` (found and disclosed, not resolved) |
| **Academy Impact** | Would warrant an update to the Calculation Framework's own concept guide once designed |
| **Notes** | Sourced from `Technical Debt Register.md` `TD-21`; `WP7.1D Security Review Report.md`. A genuine finding from this Work Package's own required Security Review, not present in any prior planning document — the first Future Capability Register entry sourced from a security review rather than an implementation report's own disclosed finding. |

#### FCR-0036 — Transactional Multi-Document Operations for the Engineering Data Model

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Extend `Tempest.Core.EngineeringData` (or a layer atop it) so a sequence of related writes (create a document, link it to another, link it to a third) can be performed atomically — `IEngineeringDocumentStore` currently offers no transactional multi-write primitive, so a consumer performing several related operations in sequence (as `Tempest.Core.Verification.VerificationService.RecordAsync` does) risks a partially-completed state if a later step fails. |
| **Status** | Identified (`WP 7.1E`) — found during this Work Package's own required Security Review, not anticipated by prior planning |
| **Priority** | Low — no current consumer has reported a real problem from the non-transactional sequence; disclosed proactively |
| **Business Value** | Would grow once a real, demonstrated need for atomic multi-document writes exists — Verification is the first consumer with this shape, but not the only plausible future one (Materials' own future multi-link needs, a future Requirements Engine) |
| **Engineering Effort** | Unknown, likely High — `IPersistenceStore`'s own file-backed implementation has no transaction concept to build on; this would be a genuine new capability, not a small extension |
| **Dependencies** | `FCR-0029` (Engineering Data Model, shipped `WP 7.1A`) |
| **Proposed Target Release** | Not yet scheduled — revisit trigger: a real, demonstrated need for transactional multi-document operations |
| **Related ADRs** | ADR-0053, ADR-0057 |
| **Related Work Packages** | `WP 7.1E` (found and disclosed, not resolved) |
| **Academy Impact** | Would warrant an update to the Engineering Data Model's own concept guide once designed |
| **Notes** | Sourced from `Technical Debt Register.md` `TD-23`; `WP7.1E Security Review Report.md`. A genuine finding from this Work Package's own required Security Review, mirroring `FCR-0035`'s own identical origin (`WP 7.1D`'s Security Review) — the second Future Capability Register entry sourced from a security review rather than a retrospective's own disclosed finding. |

#### FCR-0089 — Authoritative Bearing Dataset Population

| Field | Value |
|---|---|
| **Category** | Engineering Foundation (Cross-Cutting) |
| **Description** | Populate the Bearing Library (`A4`) with real bearing reference data from authoritative sources — recognised international/national standards for boundary dimensions and designation systems, manufacturer technical catalogues, manufacturer datasheets — each record carrying provenance naming the source organisation, document, revision and location, and each staying `NotVerified` until a named reviewer has actually checked it against that source. |
| **Status** | Identified (`A4`) — the library ships architecturally complete and **empty** |
| **Priority** | High — A4's own architecture is unusable for engineering work without data, and every downstream consumer (`P02` selection, `P05` calculation templates, `P06` worked examples) depends on it |
| **Business Value** | High — this is the capability A4 exists to enable; the model, rules and lifecycle are means to it |
| **Engineering Effort** | Medium-High, and mostly not engineering: the blocking cost is sourcing and licensing authoritative data and reviewing each record, not writing code |
| **Dependencies** | `A4` (shipped); `FCR-0090` if the dataset is large enough to need an import pipeline rather than manual registration |
| **Proposed Target Release** | Not yet scheduled — gated on the Product Owner confirming which sources may be used and on what licence terms |
| **Related ADRs** | ADR-0124 |
| **Related Work Packages** | `A4` (assessed the repository directly and found no bearing dataset of any kind; disclosed the gap rather than fabricating catalogue values) |
| **Academy Impact** | A worked example of the reference-data provenance lifecycle would become writable once real records exist |
| **Notes** | **This entry exists because A4 refused to invent data.** Fabricating manufacturer specifications, load ratings, speeds, dimensions or standards compliance is prohibited by A4's own charter (§34, No Fabrication) and would make the library actively dangerous — a plausible-looking catalogue value nobody can trace is worse than an empty catalogue. See `docs/architecture/A4 Bearing Library.md` §14. |

#### FCR-0090 — Bearing Reference-Data Import Pipeline and Mapping Report

| Field | Value |
|---|---|
| **Category** | Engineering Foundation (Cross-Cutting) |
| **Description** | A repeatable import path from a structured supplier or standards dataset into `IBearingCatalog`: schema mapping to the canonical `BearingDefinition`, per-record validation before any write, preservation of every source field (including ones that cannot be normalised, into `ManufacturerAttributes`), and a mapping/data-quality report naming every unmappable or incomplete field rather than silently discarding or defaulting it. |
| **Status** | Identified (`A4`) — deliberately not built |
| **Priority** | Medium — needed once `FCR-0089` supplies a dataset large enough that manual registration is impractical, and not before |
| **Business Value** | Medium — an accelerator for `FCR-0089`, not a capability in its own right |
| **Engineering Effort** | Medium — the pre-write validation seam already exists (`IBearingValidationService.ValidateDefinitionAsync`) and the catalogue-wide report an import would be judged by already exists (`ValidateCatalogueAsync`); what is missing is the reader and the mapping, both of which depend entirely on the real file shape |
| **Dependencies** | `FCR-0089` (there is nothing to import until a dataset exists) |
| **Proposed Target Release** | Not yet scheduled — revisit trigger: a real dataset in a real format |
| **Related ADRs** | ADR-0124 |
| **Related Work Packages** | `A4` (deliberately deferred: writing an importer against a hypothetical file shape, with no dataset to validate it against, is speculative work) |
| **Academy Impact** | None until built |
| **Notes** | Recorded so the deferral is trackable rather than only narrated in `docs/architecture/A4 Bearing Library.md` §14/§16. |

#### FCR-0091 — Schema Versioning for Reference-Data Catalogue Documents

| Field | Value |
|---|---|
| **Category** | Engineering Foundation (Cross-Cutting) |
| **Description** | Extend `ADR-0120`'s durable-state schema-version and read-time-migration machinery, currently scoped to `EngineeringObjectState`, to cover the reference-data catalogues' own `IEngineeringDocumentStore` documents — Materials' `MaterialSpecification`, Requirements' three Kinds, and (new, `A4`) `BearingReference`. |
| **Status** | Identified (`A4`) |
| **Priority** | Medium — no catalogue document shape has yet changed incompatibly, but each of the three catalogues stores a JSON shape with no version marker and no migration path, so the first incompatible change would be a data-loss event rather than a migration |
| **Business Value** | Medium — protects reference data a customer has already entered and reviewed, which is precisely the data that is expensive to re-create |
| **Engineering Effort** | Medium — the mechanism exists (`IStateMigration`/`IStateMigrationRegistry`, `WP 16.3B`); the work is extending it to a second document family and back-filling a version marker onto documents written before it |
| **Dependencies** | `ADR-0120`/`WP 16.3B` (shipped) |
| **Proposed Target Release** | Not yet scheduled — revisit trigger: the first genuinely incompatible change to any catalogue document shape |
| **Related ADRs** | ADR-0053, ADR-0055, ADR-0058, ADR-0120, ADR-0124 |
| **Related Work Packages** | `A4` (found and disclosed while deciding how to store bearing records; `ADR-0124`'s own Negative consequences names it directly) |
| **Academy Impact** | Would extend `State Schema Versioning Architecture.md`'s own scope section |
| **Notes** | A4 mitigates its own share of the risk by writing enums as strings, so adding or reordering an enum member can never silently reinterpret a stored record — but that is a narrow protection, not a migration mechanism, and it does nothing for the two catalogues that preceded A4. |

#### FCR-0093 — Authoritative Reference-Dataset Population for A1, A2, A3, A5, A6 and A7

| Field | Value |
|---|---|
| **Category** | Engineering Foundation (Cross-Cutting) |
| **Description** | Populate the six reference libraries `Group A` completed alongside A4 — Materials (`A1`), Standards (`A2`), Fasteners (`A3`), Mechanical Components (`A5`), Engineering Constants (`A6`) and Manufacturing Processes (`A7`) — with real data from authoritative sources, each record carrying provenance naming the source organisation, document, revision and location, and each staying `NotVerified` until a named reviewer has actually checked it against that source. `FCR-0089` is the identical entry for A4, and this one is deliberately its sibling rather than a rewrite of it. |
| **Status** | Identified (`Group A`) — all six libraries ship architecturally complete and **empty**, exactly as A4 did |
| **Priority** | High — the architectures are unusable for engineering work without data, and every downstream consumer (`P02` selection, `P05` calculation templates, `P06` worked examples) depends on them. A6 is the most urgent of the six: a calculation capability cannot cite a constant that does not exist |
| **Business Value** | High — this is the capability the six libraries exist to enable; the models, rules and lifecycles are means to it |
| **Engineering Effort** | High in total, and mostly not engineering: the blocking cost is sourcing and licensing authoritative data and reviewing each record, not writing code. A2 is additionally licence-constrained in a way the others are not — a standards index is itself a copyrighted work in many jurisdictions |
| **Dependencies** | `Group A` (shipped); `FCR-0094` where a dataset is large enough to need an import pipeline rather than manual registration |
| **Proposed Target Release** | Not yet scheduled — gated on the Product Owner confirming which sources may be used and on what licence terms, per library |
| **Related ADRs** | ADR-0126 |
| **Related Work Packages** | `Group A` (surveyed the repository directly and found no dataset of any kind for any of the six domains; disclosed the gap rather than fabricating values) |
| **Academy Impact** | A worked example of the reference-data provenance lifecycle would become writable once real records exist in any library |
| **Notes** | **This entry exists because `Group A` refused to invent data.** Every test fixture in all six libraries is explicitly fictional, says so in its own remarks, and uses an unusable "FX-" designation series with fictional source organisations, so no fixture can be mistaken for real reference data. Two libraries deserve a specific warning: a fabricated **constant** (`A6`) propagates silently into every calculation that consumes it, and a fabricated **process capability band** (`A7`) would steer a manufacturing decision. Both are more dangerous than an empty library, not less. See `docs/architecture/Group A Engineering Reference Data.md` §9. |

#### FCR-0094 — A Shared Reference-Data Import Pipeline

| Field | Value |
|---|---|
| **Category** | Engineering Foundation (Cross-Cutting) |
| **Description** | Generalise `FCR-0090`'s proposed bearing importer into one import path serving every `Group A` library: schema mapping to a library's own definition type, per-record validation before any write, preservation of every source field including ones that cannot be normalised, and a mapping/data-quality report naming every unmappable or incomplete field rather than silently discarding or defaulting it. |
| **Status** | Identified (`Group A`) — deliberately not built |
| **Priority** | Medium — needed once `FCR-0089`/`FCR-0093` supply a dataset large enough that manual registration is impractical, and not before |
| **Business Value** | Medium — an accelerator, not a capability in its own right. Its value rose with `Group A`: an importer that serves seven libraries is worth substantially more than one that serves one |
| **Engineering Effort** | Medium — the shared layer already supplies the two seams an importer needs, once each rather than seven times: `IReferenceValidationService<T>.ValidateDefinitionAsync` for pre-write validation, and `ValidateLibraryAsync` for the report an import would be judged by. What is missing is the reader and the per-library mapping, both of which depend entirely on the real file shape |
| **Dependencies** | `FCR-0089`/`FCR-0093` (there is nothing to import until a dataset exists); supersedes the scope of `FCR-0090`, which remains open as the bearing-specific case |
| **Proposed Target Release** | Not yet scheduled — revisit trigger: a real dataset in a real format |
| **Related ADRs** | ADR-0126 |
| **Related Work Packages** | `A4` (proposed the bearing-specific version as `FCR-0090`); `Group A` (deliberately deferred: writing an importer against a hypothetical file shape, with no dataset to validate it against, is speculative work — and doing so seven times more so) |
| **Academy Impact** | None until built |
| **Notes** | Recorded so the deferral is trackable rather than only narrated. The shared layer is what makes one importer plausible where seven would not have been. |

#### FCR-0095 — A Temperature-Difference Dimension

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Model a temperature *difference* as a dimension distinct from an absolute temperature. `ADR-0125` made `Temperature` representable and refused arithmetic on affine quantities; a difference of 20 °C is a different quantity from a temperature of 20 °C, and only the former can be added, subtracted or scaled. |
| **Status** | Identified (`Group A`) — deliberately not built |
| **Priority** | Low — no `Group A` library records a temperature difference, and the kelvin route is available and correct in the meantime: a source quoting a difference in degrees Celsius records it in kelvin, where the magnitude is identical and the arithmetic works |
| **Business Value** | Would grow once a real consumer records a temperature difference — a thermal-expansion or heat-transfer calculation is the most likely first one |
| **Engineering Effort** | Low — one dimension and one unit catalogue, purely additive, following the pattern `ADR-0125` and `ADR-0124` both used |
| **Dependencies** | `ADR-0125` (shipped, `Group A`) |
| **Proposed Target Release** | Not yet scheduled — revisit trigger: a real consumer recording a temperature difference |
| **Related ADRs** | ADR-0054, ADR-0125 (names this boundary explicitly in its own Negative consequences) |
| **Related Work Packages** | `Group A` (found and disclosed while resolving `FCR-0034`, not resolved) |
| **Academy Impact** | Would warrant a paragraph in the Units & Quantities concept guide alongside the affine-unit material |
| **Notes** | Adding a dimension nothing uses is the kind of unused generality this platform avoids, which is why `ADR-0125` disclosed this rather than pre-emptively building it. |

### Business Governance

#### FCR-0096 — Business Template Integration

| Field | Value |
|---|---|
| **Category** | Business Governance |
| **Description** | Inspect the organisation's existing business template documents and integrate them into `P07`: map each to its owning work package (`C1`–`C7`), extract the structured information that belongs in a governed record, preserve the rest as controlled source documents, and record every conflict between a template's own terminology and the implemented model. |
| **Status** | Identified (`Group C`) — **blocked on access, not on effort** |
| **Priority** | High — `P07`'s framework is complete and every library is empty; these templates are the organisation's own existing work and the intended first content |
| **Business Value** | High. The framework answers eight business questions and currently has nothing to answer them from. The templates are the fastest legitimate route to real content, and the only one that does not involve inventing business facts. |
| **Engineering Effort** | Low to medium for the mapping and registration; the models exist and no new code is anticipated beyond fixture-free import. |
| **Dependencies** | Access to the Claude Design project named in the `P07` commissioning instruction, which returned HTTP 403 to this session's own fetch tool. No equivalent content exists in the repository: a full search of `docs/` and `src/` found no contract template, rate card, insurance schedule, business plan or financial model, and the account's artifact listing holds three artifacts, none of them a business template. |
| **Proposed Target Release** | Not yet scheduled — revisit trigger: the template documents becoming reachable, by any route (export to the repository, a connector, or supply in a session) |
| **Related ADRs** | ADR-0129, ADR-0130 |
| **Related Work Packages** | `Group C` (framework built to receive this content; §2 of `docs/architecture/Group C Business Governance.md` records the access limitation) |
| **Academy Impact** | Would warrant a worked example per work package, using real records rather than fixtures |
| **Notes** | `P07` was designed from the repository and the engineering-consultancy domain rather than from the templates, and **claims to have implemented no template requirement it never saw**. The mapping table in the `P07` completion report is marked unverified throughout for that reason. Where the templates turn out to use different terminology from the implemented model, the conflict is to be recorded and resolved through the architecture mechanism rather than by silently renaming either side. |

#### FCR-0097 — Authoritative Business Data Population

| Field | Value |
|---|---|
| **Category** | Business Governance |
| **Description** | Populate the eleven `P07` libraries with the organisation's real contracts, templates, risks, policies, IP and data assets, rates, assumptions, scenarios, opportunities and operating model — each authored, sourced, checked and released by somebody who can stand behind it. |
| **Status** | Identified (`Group C`) — deliberately not done |
| **Priority** | High — the framework is unusable for running a business without content |
| **Business Value** | High, and it is the whole point of `P07`. Until then the libraries govern nothing. |
| **Engineering Effort** | None in code. This is authoring and review work, gated by the same lifecycle `Group A`'s own data population is (`FCR-0089`, `FCR-0093`). |
| **Dependencies** | `FCR-0096` for the template-derived portion; real commercial, legal and financial records for the rest, most of which live outside TempestOS |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | ADR-0129, ADR-0130 |
| **Related Work Packages** | `Group C` |
| **Academy Impact** | None until content exists |
| **Notes** | Recorded to make the empty state a tracked gap rather than an oversight, mirroring `FCR-0089`/`FCR-0093` for reference data. **No business, legal, insurance or financial fact may be invented to fill it**: §36 of the `P07` commissioning instruction forbids it, and a plausible-looking rate card or policy nobody wrote would be worse than an empty library, because it would be believed. |

### Systems Engineering

#### FCR-0027 — Requirements Engine

| Field | Value |
|---|---|
| **Category** | Systems Engineering |
| **Description** | A platform capability for requirements management, verification, and traceability across a real engineering programme — named in `PROJECT_STATUS.md`'s own Long-Term Vision as one of two aspirational platform services beyond the current Platform Services phase. |
| **Status** | **Implemented (`WP 7.3A`)** — `Tempest.Core.Requirements`, `RequirementsService`, built on `Tempest.Core.EngineeringData` and `Tempest.Core.Verification` per `ADR-0058`, exactly as `WP7.2C Requirements Platform Contracts.md` approved, zero deviation. `ADR-0013` classification ratified as `ADR-0058` (Platform Service), alongside `ADR-0059`–`ADR-0061`, all four now Accepted. |
| **Priority** | Realised — the first Engineering Discipline category capability to progress from Identified through Architecture, Contracts, and Implementation in sequence |
| **Business Value** | Confirmed as the first capability realising `VISION.md`'s own stated target user ("an individual engineer or a small professional engineering practice") — see `WP7.2A Commercial Assessment.md` |
| **Engineering Effort** | Realised — 20 new production files, 131 new tests (1275 → 1406), 4 ratified ADRs, zero build warnings, zero architectural rework against the approved contracts. See `WP7.3A Implementation Report.md`. |
| **Dependencies** | Both technical dependencies satisfied and consumed directly — `FCR-0029` (Engineering Data Model) and `FCR-0033` (Verification Framework), both **Implemented** and certified, both now real, hard dependencies of the shipped `RequirementsService`. |
| **Proposed Target Release** | `v0.7.0` (realised — implemented within the same release the architecture/contract phases targeted, ahead of the originally recommended `v0.8.0`) |
| **Related ADRs** | ADR-0013 (named this capability directly in its own Future Considerations); `ADR-0058`–`ADR-0061` (Accepted, `WP 7.3A`) |
| **Related Work Packages** | `WP 7.2A` (recommended this capability as the next programme); `WP 7.2B` (designed its complete architecture); `WP 7.2C` (defined its complete public contracts); `WP 7.3A` (implemented it) |
| **Academy Impact** | `16-requirements-engine.md` (two concept-guide sections, per `WP7.2C Academy Plan.md`'s own recommendation) and `WP7.3A-requirements-engine-implementation.md` retrospective, both written by `WP 7.3A` |
| **Notes** | Sourced from `PROJECT_STATUS.md`'s own Long-Term Vision section; `ADR-0013`'s own Future Considerations; `Threat Model.md` assumption 1 ("requirements, analysis, verification records"); `WP7.1E Future Capability Recommendations.md` Recommendation 1 (recommended this capability consume `IVerificationService` directly — confirmed by the shipped `GetEvidenceAsync`); `WP7.2A Strategic Roadmap Review.md`, `WP7.2A Programme Comparison Matrix.md`, `WP7.2A Recommended Programme.md`; `WP7.2B Requirements Platform Architecture.md` and its ten companion deliverables; `WP7.2C Requirements Platform Contracts.md` and its eleven companion deliverables; `WP7.3A Implementation Report.md` and its seven companion deliverables. Two new Future Capability candidates raised from this implementation's own experience: string-based allocation targets and requirement baselining — see `WP7.3A Future Capability Recommendations.md` and the new entries below. |

#### FCR-0037 — String-Based Requirement Allocation Targets

| Field | Value |
|---|---|
| **Category** | Systems Engineering |
| **Description** | `WP7.2B Requirements Domain Model.md`'s own broader architectural vision described a Requirement Allocation target as either a real `IEngineeringDocument` reference or an open, unvalidated string identifier, for allocating to a future design element that does not yet exist as a created document. `WP7.2C`'s own approved `LinkAsync` contract carried forward only the document-reference half; the open-string half was never given its own contract method, and `WP 7.3A` implements the approved contract exactly, with no string-based overload. |
| **Status** | Identified — disclosed gap between `WP7.2B`'s own architectural aspiration and `WP7.2C`'s own final, narrower approved contract; not a regression, since the contract review stage itself never committed to building it |
| **Priority** | Low — no real, demonstrated need yet; early-phase systems engineering (allocating to a still-conceptual subsystem before any concrete design document exists) is the plausible future scenario |
| **Business Value** | Unknown — depends on whether early-phase, pre-design allocation proves to be real practice for TempestOS's own future users |
| **Engineering Effort** | Low — mirrors `Tempest.Core.EngineeringData.DocumentReference`'s own existing open `RelationshipKind` string precedent; a dedicated method (e.g. `AllocateToPendingAsync`), not a redesign of `LinkAsync` |
| **Dependencies** | `Tempest.Core.Requirements` (Implemented, `WP 7.3A`) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet — would not require revisiting `ADR-0058`–`ADR-0061`, only an additive method |
| **Related Work Packages** | `WP 7.2B` (named the original broader vision); `WP 7.2C` (approved the narrower, shipped contract); `WP 7.3A` (disclosed the gap) |
| **Academy Impact** | Would extend `16-requirements-engine.md` §10/§12 once implemented |
| **Notes** | Raised directly by `WP7.3A Future Capability Recommendations.md`; revisit trigger is a real, demonstrated need to allocate a requirement to a design element that does not yet exist as a document. |

#### FCR-0038 — Requirement Baselining

| Field | Value |
|---|---|
| **Category** | Systems Engineering |
| **Description** | A capability to freeze a named, dated set of requirement revisions as a formal baseline, for later comparison against a current working set — standard systems engineering practice, distinct from the per-requirement revision history `Tempest.Core.Requirements` already provides, and explicitly not implemented by `WP 7.3A`'s own controlling instruction (which named only Requirements Engine implementation, not Compliance or Workflow). |
| **Status** | Identified — no design work performed yet |
| **Priority** | Unknown — plausible for the first engineering discipline module that consumes the Requirements Engine in earnest |
| **Business Value** | Unknown — dependent on real multi-milestone engineering programme usage |
| **Engineering Effort** | Unknown — no architecture exists yet; would need its own design phase, likely building on `IEngineeringDocumentStore`'s own existing revision history rather than a new mechanism |
| **Dependencies** | `Tempest.Core.Requirements` (Implemented, `WP 7.3A`) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 7.3A` (disclosed this candidate from its own implementation experience) |
| **Academy Impact** | Would extend `16-requirements-engine.md` §12 once implemented |
| **Notes** | Raised directly by `WP7.3A Future Capability Recommendations.md`; revisit trigger is a real, demonstrated need to compare "what the requirement set looked like at milestone X" against current state. A related, adjacent candidate (change impact analysis, layered on `GetRelationshipsAsync`) was also noted but not separately registered — see `WP7.3A Future Capability Recommendations.md`. |

### Workspace

#### FCR-0039 — Multi-Selection in the Project Explorer

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `ISelectionService` (`WP8.0B`, frozen) tracks exactly one current selection; `WP 9.0A`'s own controlling instruction named multi-selection as a Product Structure capability, conditional on current UI technology support. Not supported by the current contract. |
| **Status** | **Resolved/Implemented — `WP 9.1A`.** `ISelectionService` gains `SelectedItems`/`ToggleSelectionAsync`, additively (`ADR-0085`); `Current`/`SelectAsync`/`ClearAsync` unchanged. |
| **Priority** | Low — no real, demonstrated bulk-operation need yet |
| **Business Value** | Realised — `WP 9.1A`'s own Bulk Requirements editing (Status/Owner/Priority) is the first real consumer |
| **Engineering Effort** | Medium — required its own contract change to `ISelectionService` (additive, not a reopening), plus `WorkspaceContext`'s own backing storage |
| **Dependencies** | `Tempest.App.Workspace.Requirements` (Implemented, `WP 9.1A`), the real consumer that exercised it |
| **Proposed Target Release** | Delivered, `v0.9.0` |
| **Related ADRs** | `ADR-0085` |
| **Related Work Packages** | `WP 8.0B` (defined the original, single-selection contract); `WP 9.0A` (disclosed the gap); `WP 9.1A` (resolved it) |
| **Academy Impact** | `WP9.1A-requirements-management-workspace.md` §5/§8 |
| **Notes** | Raised directly by `WP9.0A Future Capability Assessment.md`; resolved directly by `WP9.1A Implementation Report.md`/`ADR-0085` once Bulk editing became a real, demonstrated need. |

#### FCR-0040 — Drag-and-Drop Reparenting

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | Reparenting a Product Structure object via a drag gesture, rather than the `MoveMechanicalObjectCommand` this Work Package ships. Blocked entirely on `ADR-0066`'s own commitment to a terminal-based Workspace presentation, which has no drag gesture. |
| **Status** | Identified — explicitly blocked, not merely unscheduled |
| **Priority** | Not applicable until `ADR-0066` itself is revisited |
| **Business Value** | Unknown |
| **Engineering Effort** | Unknown — depends entirely on whatever future rendering technology decision would make it possible |
| **Dependencies** | A future reversal or extension of `ADR-0066` |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | `ADR-0066` (the blocking decision) |
| **Related Work Packages** | `WP 8.0B` (`ADR-0066`); `WP 9.0A` (disclosed the dependency explicitly) |
| **Academy Impact** | None until `ADR-0066` is revisited |
| **Notes** | Raised directly by `WP9.0A Future Capability Assessment.md`; recorded so the dependency on `ADR-0066` is explicit, not to imply revisiting it is due. |

#### FCR-0041 — Real Invoke-by-Id Execution for Object-Targeted Commands

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | All six `WP 9.0A` Mechanical `CommandDescriptor`s omit `createDefault`, so none is invokable by bare Id through `ICommandRegistry.InvokeAsync` — only through `ICommandDispatcher.DispatchAsync` by a caller that already holds real target data. A context-menu-driven UI would supply that data naturally. |
| **Status** | Identified — a disclosed, reasoned scope boundary, not an oversight |
| **Priority** | Low — the only present caller (`WorkspaceShell`) has no selection-aware command invocation path yet either |
| **Business Value** | Improves Command Palette discoverability/usability once a richer interaction surface exists |
| **Engineering Effort** | Low-Medium — likely a `CommandDescriptor.CreateDefault` reading ambient `IWorkspaceContext.CurrentSelection`, once such a caller exists |
| **Dependencies** | A richer Workspace interaction surface than `WorkspaceShell`'s own current `menu <N>` listing |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 9.0A` (disclosed this candidate) |
| **Academy Impact** | Would extend `WP9.0A-mechanical-product-structure.md` once implemented |
| **Notes** | Raised directly by `WP9.0A Future Capability Assessment.md`. |

#### FCR-0042 — A Second Engineering Discipline Module Reusing the Mechanical Provider Categories

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `MechanicalProductStructureNodeProvider`/`MechanicalWorkspaceViewFactory`/`MechanicalPropertyFacetProvider` are this platform's first real (non-sample) implementations of all three Kind-keyed Workspace provider interfaces. A second discipline (Documentation & Design, or Supply Chain — both already real, `WP8.2C` Kinds with no Workspace presentation yet) would prove the pattern generalises. |
| **Status** | Identified — named for continuity, not an instruction to begin it |
| **Priority** | Medium — the natural next Engineering Discipline Module |
| **Business Value** | High, if realised — extends real Workspace presentation to a second discipline with proven patterns, minimising new design risk |
| **Engineering Effort** | Medium — the pattern (three providers, structural-mutation facets where needed, a sample module) is now proven and precedented, reducing what would otherwise be Medium-High to Medium |
| **Dependencies** | `Tempest.App.Workspace.Mechanical` (Implemented, `WP 9.0A`) as the worked precedent |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | `ADR-0067`; `ADR-0080`; `ADR-0082` |
| **Related Work Packages** | `WP 9.0A` (established the precedent) |
| **Academy Impact** | Would produce its own new academy Work Package retrospective |
| **Notes** | Raised directly by `WP9.0A Future Capability Assessment.md`. |

#### FCR-0043 — Structural Mutation for Documentation & Design / Supply Chain Kinds

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `IRenamable`/`IHasParent`/`IDeletable` (`ADR-0080`) are composed only into the five Product Structure Kinds today. A future Work Package extending Workspace presentation to Drawing/CAD Model/Supplier/Purchase Item could compose the same three facets rather than inventing new ones. |
| **Status** | Identified — contingent on `FCR-0042` |
| **Priority** | Low — contingent, not independently prioritised |
| **Business Value** | Unknown — depends on `FCR-0042` |
| **Engineering Effort** | Low, once `FCR-0042` is underway — the facets themselves already exist and are already proven; only composition into new Kind interfaces would be needed |
| **Dependencies** | `FCR-0042` |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | `ADR-0080` |
| **Related Work Packages** | `WP 9.0A` (established the reusable facets) |
| **Academy Impact** | Would extend whatever academy document `FCR-0042` produces |
| **Notes** | Raised directly by `WP9.0A Future Capability Assessment.md`. |

#### FCR-0044 — Product Variant Resolution

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `WP 9.0B`'s own controlling instruction named Product Variants as "placeholder architecture only." A future implementation would compose a named variant axis alongside `IHasBomLine`/`IConfiguration`, resolved at read time by the Workspace layer, never a second structural tree — the design note in `WP9.0B Implementation Report.md` records the intended shape. |
| **Status** | Identified — deliberately no interface, class, or test exists yet |
| **Priority** | Low — no real, demonstrated variant-specific BOM need yet |
| **Business Value** | Unknown — depends on real multi-variant product usage |
| **Engineering Effort** | Medium — a new `VariantId` concept on a richer future BOM line shape, plus Workspace-layer filtering; no Domain contract redesign anticipated |
| **Dependencies** | `IHasBomLine` (Implemented, `WP 9.0B`) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 9.0B` (recorded the placeholder design) |
| **Academy Impact** | Would extend `WP9.0B-product-configuration-and-bom-management.md` once implemented |
| **Notes** | Raised directly by `WP9.0B Implementation Report.md`/`WP9.0B Future Capability Assessment.md`. |

#### FCR-0045 — Unit of Measure Canonicalisation

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `IHasBomLine.UnitOfMeasure` is free text (`ADR-0083`) — `"EA"`/`"ea"` are different strings today. A small, closed vocabulary/lookup, scoped to BOM display units specifically (never a dependency on `Tempest.Core.UnitsAndQuantities`, the wrong tool for this per `ADR-0083`), would flag inconsistent unit strings. |
| **Status** | Identified |
| **Priority** | Low — no real multi-contributor BOM data exists yet to make inconsistency an observed problem |
| **Business Value** | Unknown |
| **Engineering Effort** | Low — a lookup table plus one new, optional validation rule following the same `IValidationRule` shape this Work Package's own five already use |
| **Dependencies** | `IHasBomLine` (Implemented, `WP 9.0B`) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | `ADR-0083` (names and rejects the alternative — dimensional typing) |
| **Related Work Packages** | `WP 9.0B` (disclosed this candidate) |
| **Academy Impact** | Would extend `WP9.0B-product-configuration-and-bom-management.md` once implemented |
| **Notes** | Raised directly by `WP9.0B Future Capability Assessment.md`. |

#### FCR-0046 — Cost Roll-Up Over the BOM Hierarchy

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | A Part's own unit cost (not tracked anywhere in the Domain today) times `IHasBomLine.Quantity`, summed recursively up the tree — natural once `PurchaseItem` (`WP8.2C`, already real) gains real Workspace presentation of its own. |
| **Status** | Identified |
| **Priority** | Low — contingent on Supply Chain Workspace presentation existing first |
| **Business Value** | Unknown |
| **Engineering Effort** | Unknown — no architecture exists yet |
| **Dependencies** | `FCR-0042` (a second Engineering Discipline Module, `WP 9.0A`) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 9.0B` (disclosed this candidate) |
| **Academy Impact** | None until designed |
| **Notes** | Raised directly by `WP9.0B Future Capability Assessment.md`; deliberately not recommended before `FCR-0042`, to avoid blurring the Product Structure/Supply Chain boundary. |

#### FCR-0047 — Configuration Management Workflow

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | A guided create → review → approve → release process over a Baseline/Release, rather than the direct `EngineeringObjectFactory<T>`/`TransitionAsync` calls both `WP 9.0A` and `WP 9.0B`'s own representative data use today. Explicitly out of scope for both Work Packages ("No configuration management workflows"). |
| **Status** | Identified |
| **Priority** | Low — today's direct creation already satisfies every named scope item |
| **Business Value** | Unknown — dependent on a real multi-approver release process being demonstrated as necessary |
| **Engineering Effort** | Unknown — no architecture exists yet |
| **Dependencies** | `Configuration`/`Baseline`/`Release` (Implemented, `WP 8.2C`) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 9.0A`, `WP 9.0B` (both explicitly excluded this from their own scope) |
| **Academy Impact** | None until designed |
| **Notes** | Raised directly by `WP9.0B Future Capability Assessment.md`. |

#### FCR-0048 — Requirement Collection Membership Removal

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `AddRequirementToCollectionCommand` has no symmetric "remove" — `IEngineeringDocumentStore` has no unlink primitive to build one on. A future `UnlinkAsync` (or an equivalent "supersede a relationship" mechanism) would let a real "remove from collection" command exist. |
| **Status** | Identified |
| **Priority** | Low — no real, demonstrated need for collection membership correction yet |
| **Business Value** | Unknown — dependent on real collection curation becoming ordinary practice |
| **Engineering Effort** | High — the underlying primitive would affect every `DocumentReference` consumer platform-wide, not only Requirements |
| **Dependencies** | `IEngineeringDocumentStore` (Implemented, `WP 7.1A`) would need its own extension first |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet — would require its own, since `IEngineeringDocumentStore` is a shared, platform-wide Domain contract |
| **Related Work Packages** | `WP 7.3A` (defined the current, no-unlink Requirements shape); `WP 9.1A` (disclosed the gap) |
| **Academy Impact** | Would extend `WP9.1A-requirements-management-workspace.md` once implemented |
| **Notes** | Raised directly by `WP9.1A Future Capability Assessment.md`; revisit trigger is a real, demonstrated need for collection membership correction. |

#### FCR-0049 — Domain-Level Search Generalised Beyond `IEngineeringObject`

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `Contracts/Search.cs`'s own `ISearchQuery`/`ISearchResult` (`WP8.2B`) remain unimplemented anywhere; `ISearchResult.Matches` is typed `IReadOnlyList<IEngineeringObject>`, which Requirements types do not implement. A real implementation would need `Matches` retyped to something Kind-agnostic, enabling a single query spanning both Mechanical and Requirements results together. |
| **Status** | Identified |
| **Priority** | Low — each discipline's own `ProjectExplorer.FilterAsync` scoped-to-current-area search already satisfies every named scope item across three consecutive Work Packages |
| **Business Value** | Unknown — dependent on a real, demonstrated cross-discipline search need |
| **Engineering Effort** | Medium-High — a genuine reopening of a frozen `WP8.2B` Domain contract |
| **Dependencies** | `Contracts/Search.cs` (Identified, `WP8.2B`, still unimplemented) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet — would require its own, since `ISearchResult` is a frozen `WP8.2B` Domain contract |
| **Related Work Packages** | `WP 8.2B` (defined the current, unimplemented contract); `WP 9.0A`/`WP 9.0B`/`WP 9.1A` (each satisfied Search at the Workspace layer instead) |
| **Academy Impact** | Would extend `WP9.1A-requirements-management-workspace.md` once implemented |
| **Notes** | Raised directly by `WP9.1A Future Capability Assessment.md`. |

#### FCR-0050 — Multi-Target Workspace View Refresh

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `IWorkspaceCommand`'s own generic post-dispatch `RefreshAsync` targets exactly one `TargetObjectId`. A `IWorkspaceCommand`-sibling contract carrying `IReadOnlyList<Guid> TargetObjectIds` (or a Workspace-side "refresh every open view matching any of these Ids" helper) would let a Bulk command refresh every touched item's own open view. |
| **Status** | Identified — disclosed directly by `TD-28` |
| **Priority** | Low — no real user report of the stale-view symptom yet; underlying data is always correct immediately |
| **Business Value** | Unknown — dependent on a real user report |
| **Engineering Effort** | Low-Medium — additive to `IWorkspaceCommand`'s own dispatch-completion handling, or a new Workspace-side helper requiring no contract change at all |
| **Dependencies** | `BulkSetRequirementStatusCommand`/`BulkSetRequirementOwnerCommand`/`BulkSetRequirementPriorityCommand` (Implemented, `WP 9.1A`), the first real consumers that would exercise it |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 9.1A` (disclosed the gap via `TD-28`) |
| **Academy Impact** | Would extend `WP9.1A-requirements-management-workspace.md` once implemented |
| **Notes** | Raised directly by `WP9.1A Future Capability Assessment.md` and `WP9.1A Technical Debt Assessment.md` (`TD-28`). |

#### FCR-0051 — Concrete `ICalculationResult`/`IVerificationResult` Implementations

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `ICalculationResult`/`IVerificationResult` (`Contracts/Calculations.cs`/`Contracts/RequirementsVerification.cs`, `WP8.2B`) have zero concrete implementations anywhere, so `EvidenceComposer`/`ITraceable.GetEvidenceAsync` resolves structurally empty for every object. A real implementation would need a genuinely addressable, non-generic `IEngineeringObject` shape wrapping `CalculationRecord<TResult>` (which is generic and has no fixed `Kind`/`SubjectId` today) — a real Domain design question, not a mechanical add. |
| **Status** | Identified |
| **Priority** | Low — direct `GetRelationshipsAsync` reads already satisfy every scope item across four consecutive Work Packages |
| **Business Value** | Unknown — dependent on a real, demonstrated need for composed, cross-discipline `IEvidence` |
| **Engineering Effort** | Medium-High — a genuine new Domain design question, not a reopening of a frozen contract's own existing shape |
| **Dependencies** | `Contracts/Calculations.cs`/`Contracts/RequirementsVerification.cs` (Identified, `WP8.2B`, still unimplemented) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 8.2B` (defined the current, unimplemented contracts); `WP 9.1A`/`WP 9.2A` (both worked around the gap via direct relationship reads) |
| **Academy Impact** | Would extend `WP9.2A-engineering-calculations-workspace.md` once implemented |
| **Notes** | Raised directly by `WP9.2A Future Capability Assessment.md` and `WP9.2A Technical Debt Assessment.md` (`TD-30`). |

#### FCR-0052 — Concrete Approval/Review Workflow

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `IApprovalGate`/`IApproval`/`IReview`/`IReviewGate` (`Contracts/Lifecycle.cs`, `WP8.2B`) have zero concrete implementations anywhere. A real implementation would give any discipline naming "Approval" in its own scope a governed, queryable sign-off record (who approved what, when, against what evidence) instead of a bare `LifecycleState` reading. |
| **Status** | Identified |
| **Priority** | Low — `LifecycleState` reading already satisfies every KPI/facet named across `WP 9.0A`–`WP 9.2A` |
| **Business Value** | Unknown — dependent on a real, demonstrated need for auditable approval provenance |
| **Engineering Effort** | Medium-High — a real governance-record design question (single approver? review panel? evidence bundle required?), not a mechanical add |
| **Dependencies** | `Contracts/Lifecycle.cs` (Identified, `WP8.2B`, still unimplemented) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | `ADR-0087` (documents the current `LifecycleState`-only workaround) |
| **Related Work Packages** | `WP 8.2B` (defined the current, unimplemented contracts); `WP 9.2A` (disclosed the gap, `ADR-0087`) |
| **Academy Impact** | Would extend `WP9.2A-engineering-calculations-workspace.md` once implemented |
| **Notes** | Raised directly by `WP9.2A Future Capability Assessment.md` and `WP9.2A Technical Debt Assessment.md` (`TD-30`). |

#### FCR-0053 — Recalculate Resuming From a Previously-Executed Input

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `RecalculateCalculationCommand` cannot offer a parameterless "run it again" gesture, since `CalculationRecordDto<TResult>` never retained the input that produced a record. A future capability could extend the Calculation Framework's own stored shape to retain a JSON-serialized input snapshot, or introduce a Workspace-layer-only, session-scoped "last input" cache requiring no Domain change at all. |
| **Status** | Identified — disclosed directly by `TD-29` |
| **Priority** | Low — no real UI consumer of the Calculations Workspace surface exists yet to demonstrate the need |
| **Business Value** | Unknown — dependent on a real UI consumer |
| **Engineering Effort** | Low (Workspace-layer cache) to Medium (`Tempest.Core.Calculations` stored-shape extension) |
| **Dependencies** | `CalculationRecordDto<TResult>` (Implemented, `WP 7.1D`) would need its own extension for the Domain-layer option |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 7.1D` (defined the current, input-free stored shape); `WP 9.2A` (disclosed the gap via `TD-29`) |
| **Academy Impact** | Would extend `WP9.2A-engineering-calculations-workspace.md` once implemented |
| **Notes** | Raised directly by `WP9.2A Future Capability Assessment.md` and `WP9.2A Technical Debt Assessment.md` (`TD-29`). |

#### FCR-0054 — Real File/URL Attachment Storage Service

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `Attachment`/`IAttachment` (`WP8.2C`) carry descriptive metadata only (`FileName`/`ContentType`/`SizeInBytes`) — no actual file bytes, no resolvable path, no URL-fetch capability exists anywhere in this platform. A real implementation would need a genuine Platform Service decision (local filesystem storage? a blob-storage abstraction? an external document-management-system integration?). |
| **Status** | **Implemented** — `ADR-0114`, 2026-08-29. The open design question is answered with the store this platform already has, in its byte shape: `IBinaryPersistenceStore` on the same `PersistenceStore` instance and root, with `IAttachmentContentStore` writing one record per attachment. No blob abstraction, no external DMS integration, and no new service to operate. `TD-31` is closed. |
| **Priority** | Low — was low while metadata-only Attachments satisfied every scope item `WP 9.4A` named; raised and delivered by `TD-80`'s need for something real to render |
| **Business Value** | Unknown — dependent on a real, demonstrated need for actual file content |
| **Engineering Effort** | Medium-High — a genuine new Platform Service design question, not a mechanical add |
| **Dependencies** | None yet identified |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | `ADR-0114` (the implementing decision); `ADR-0053` (the substrate reused); `ADR-0113` (the metadata/state split this mirrors) |
| **Related Work Packages** | `WP 8.2C` (defined the current, metadata-only `Attachment` shape); `WP 9.4A` (disclosed the gap via `TD-31`); 2026-08-29 Attachment Content Storage (implemented it) |
| **Academy Impact** | Would extend `WP9.4A-engineering-documents-workspace.md` once implemented |
| **Notes** | Raised directly by `WP9.4A Future Capability Assessment.md` and `WP9.4A Technical Debt Assessment.md` (`TD-31`). |

#### FCR-0055 — Verification Workspace

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `Tempest.Core.Verification` (`WP 7.1E`) is already real, tested, and Workspace-invisible — architecturally the closest remaining precedent to `WP 9.2A`'s own "already-real framework, never introduced to the Workspace" starting point. A Verification Workspace would also retroactively resolve `EngineeringCockpit.VerificationStatus`'s own placeholder (`Unknown` since `WP 8.1C`) and give `WP 9.4A`'s own structurally-proven-but-unpopulated Documents↔Verification Digital Thread link a real, live anchor to point at. |
| **Status** | Identified — recommended directly |
| **Priority** | Medium-High — the most natural next real-discipline Work Package; also closes the disclosed `WP 9.3A` numbering gap this release's own record carries |
| **Business Value** | High — completes the fourth of the platform's own four already-live Engineering disciplines' own most naturally-paired framework |
| **Engineering Effort** | Medium — mirrors `WP 9.2A`'s own already-proven "already-real framework, introduce to the Workspace" shape closely |
| **Dependencies** | `Tempest.Core.Verification` (Implemented, `WP 7.1E`); the Engineering Domain's own Verification-family canonical objects (`WP 8.2A`/`WP 8.2B`/`WP 8.2C`) |
| **Proposed Target Release** | `v0.9.0`, as the Product Owner's own next-instructed Work Package |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 7.1E` (Verification Framework); `WP 9.4A` (recommends this directly, disclosing the `WP 9.3A` numbering gap it stands in for) |
| **Academy Impact** | Would be a new Academy article, mirroring `WP9.2A-engineering-calculations-workspace.md`'s own shape |
| **Notes** | Raised directly by `WP9.4A Future Capability Assessment.md`. |

#### FCR-0056 — Governance & Risk Workspace (Risks, Issues, Decisions, Hazards, Assumptions)

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `Issue`/`Risk`/`Hazard`/`Decision`/`Assumption` (`Contracts/GovernanceRisk.cs`, `WP 8.2C`) are all already real, compiled, `EngineeringObjectBase`-derived concrete classes, architecturally ready for the same Kind-keyed Workspace treatment every other discipline has now received four times, but none has its own Explorer area, Property Inspector Kind registration, or dedicated commands. `WP 9.4A` creates one live `Decision` and reads one already-live `Risk` purely to satisfy its own Digital Thread scope item, proving the underlying Domain classes are Workspace-ready without any further Domain-layer work. **`WP 10.1A` update**: `EngineeringCockpit.OpenDecisions`/`RiskSummary` (plus `Milestone`/`Task` reads) are no longer placeholder — real Cockpit-level reads now exist over this exact Domain family (a read-only dashboard summary, not the Explorer/commands/Property Inspector presence this capability itself still names) — strengthening, not resolving, this capability's own case: the Cockpit now visibly surfaces exactly how little of this already-Workspace-ready Domain family has real presence anywhere else. |
| **Status** | **Implemented** (2026-08-30, `WP — Project Risks, Issues & Decisions`) — the Project Workspace's Risks area is a real surface over `Risk`, `Hazard`, `Issue` and `Decision`: raised, retitled, scored, owned, assigned, prioritised and moved through their own status vocabularies, filtered to the open project transitively through `ProjectMembership`, and durable across a restart via the production rehydration path (`TD-104`). `Assumption` is deliberately **not** surfaced — see Notes. |
| **Priority** | Resolved for the four families that had a workflow to give them; see Notes for the one deliberately left |
| **Business Value** | Unknown — dependent on a real, demonstrated need for dedicated Risk/Decision browsing and management, beyond the indirect Digital Thread reachability `WP 9.4A` already provides |
| **Engineering Effort** | Low — mirrors the now four-times-proven Kind-keyed Workspace pattern directly; no Domain-layer work is anticipated |
| **Dependencies** | `Contracts/GovernanceRisk.cs`/`Implementation/GovernanceRisk.cs` (Implemented, `WP 8.2C`) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None — the feature reuses `IFamilySpecificState` (declared by the platform, first used by the task family) and `ADR-0116`'s principal boundary, and introduced no new architectural decision |
| **Related Work Packages** | `WP 8.2C` (defined and implemented the underlying Domain classes); `WP 9.4A` (first to instantiate a live `Decision`, and to read the base sample's own live `Risk`); `WP 10.1A` (first Workspace surface — the Cockpit — to actually read this family); **`WP — Project Risks, Issues & Decisions`** (the surface itself) |
| **Academy Impact** | Would be a new Academy article, mirroring the shape of every prior discipline's own |
| **Notes** | Raised directly by `WP9.4A Future Capability Assessment.md`. **Delivered as one area rather than three**, because `ProjectAreas` has always described the Risks tab as "risks, issues and decisions for this project"; they are three switchable registers inside it. **`Assumption` is deliberately not surfaced.** The other four each have a workflow a team actually runs — a risk is scored and closed, an issue is triaged and resolved, a decision is taken or rejected — while an assumption is a statement that is either still standing or has been invalidated, and inventing a status vocabulary for it would have been designing a capability nobody asked for. It remains a real, persistable, rehydratable Kind, reachable through the Engineering Workspace like any other. The `TD-37` sample-seeding blocker named here is moot: the surface reads the user's own objects through `ProjectMembership` and depends on no sample data at all. |

#### FCR-0057 — `VerificationService.RecordAsync` Additionally Linking Through `IHasRelationships` When the Subject Is a Real Domain Object

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `VerificationService.RecordAsync` (`WP7.1E`) links its own subject to a newly-created record via the raw document store only, never visible to `EngineeringDomainContext.RelationshipRepository` (`TD-32`). A future implementation could have `RecordAsync` detect an `EngineeringObjectBase`-derived subject and additionally call its own `.LinkAsync()`. |
| **Status** | Identified — disclosed directly by `TD-32` |
| **Priority** | Low — `VerificationRecordReader`'s own existing raw-store read already serves every scope item `WP 9.3A`'s own controlling instruction names |
| **Business Value** | Unknown — dependent on a real Workspace-layer consumer needing `RelationshipRepository` to see this specific link directly |
| **Engineering Effort** | Low — a small, additive change to one already-shipped method, once justified |
| **Dependencies** | `VerificationService.RecordAsync` (Implemented, `WP 7.1E`) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 7.1E` (defined the current, raw-store-only linking shape); `WP 9.3A` (disclosed the gap via `TD-32`) |
| **Academy Impact** | Would extend `WP9.3A-verification-management-workspace.md` once implemented |
| **Notes** | Raised directly by `WP9.3A Future Capability Assessment.md` and `WP9.3A Technical Debt Assessment.md` (`TD-32`). |

#### FCR-0058 — Concrete `IApprovalGate`/`IApproval`/`IReview` Implementation, Extended to Verification

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | Extends `FCR-0052` (`WP 9.2A`) to Verification: "Verification Reviews"/"Verification Approval State" are satisfied by `LifecycleState` alone (`ADR-0090`), identically to Calculation Management's own already-disclosed treatment. A real implementation would give every discipline naming "Review"/"Approval" a genuine, queryable governance record. |
| **Status** | Identified |
| **Priority** | Low — `LifecycleState` reading already satisfies every KPI/facet named across `WP 9.0A`–`WP 9.3A` |
| **Business Value** | Unknown — dependent on a real, demonstrated need for auditable review/approval provenance |
| **Engineering Effort** | Medium-High — the identical governance-record design question `FCR-0052` already names |
| **Dependencies** | `Contracts/Lifecycle.cs` (Identified, `WP8.2B`, still unimplemented) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | `ADR-0090` (documents the current `LifecycleState`-only workaround for Verification) |
| **Related Work Packages** | `WP 8.2B` (defined the current, unimplemented contracts); `WP 9.2A` (`FCR-0052`, `ADR-0087`); `WP 9.3A` (extends `FCR-0052` to Verification, `ADR-0090`) |
| **Academy Impact** | Would extend `WP9.3A-verification-management-workspace.md` once implemented |
| **Notes** | Raised directly by `WP9.3A Future Capability Assessment.md`; extends `FCR-0052` rather than duplicating it. |

#### FCR-0059 — A Dedicated `Witness` Field on `VerificationEvidenceEntry`

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `WP 9.3A`'s own scope names "Witness information" as a distinct Engineering Behaviour item; `VerificationEvidenceEntry` (`Description`/`Reference` only, `WP 7.1E`) has no dedicated field for it — represented today as ordinary evidence text. A future capability could extend the record with a genuine `WitnessedBy` field. |
| **Status** | Identified |
| **Priority** | Low — descriptive-text representation already satisfies every scope item `WP 9.3A`'s own controlling instruction names |
| **Business Value** | Unknown — dependent on witness identity needing to be queryable/reportable independently of free-text evidence |
| **Engineering Effort** | Low — a small, additive field on an existing record, once justified |
| **Dependencies** | `VerificationEvidenceEntry` (Implemented, `WP 7.1E`) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 7.1E` (defined the current, two-field shape); `WP 9.3A` (disclosed the gap) |
| **Academy Impact** | Would extend `WP9.3A-verification-management-workspace.md` once implemented |
| **Notes** | Raised directly by `WP9.3A Future Capability Assessment.md`. |

#### FCR-0060 — A Genuine `Routing`/`SupplierOperation` Domain Kind, Each With Its Own Structured Fields

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `ADR-0091` (`WP 9.5A`) realises Routings/Supplier Operations as `Classification`-tagged `ManufacturingOperation` objects. A future implementation could introduce genuine, distinct Domain Kinds carrying their own structured fields (a real cycle-time, a real lead-time/cost). |
| **Status** | Identified |
| **Priority** | Low — every named `WP 9.5A` scope item is satisfied by the current representation |
| **Business Value** | Unknown — dependent on a real consumer needing structured fields beyond `Classification`/`PartId`/`"manufacturedBy"` |
| **Engineering Effort** | Medium — a genuine Domain-layer contract addition, reopening `WP 8.2C`'s own closed catalogue |
| **Dependencies** | `Contracts/TestManufacturing.cs` (Implemented, `WP 8.2C`) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | `ADR-0091` (documents the current `Classification`-tagged workaround) |
| **Related Work Packages** | `WP 8.2C` (defined the current, single-Kind shape); `WP 9.5A` (`ADR-0091`) |
| **Academy Impact** | Would extend `WP9.5A-manufacturing-workspace.md` once implemented |
| **Notes** | Raised directly by `WP9.5A Future Capability Assessment.md`. |

#### FCR-0061 — Parameterising `EngineeringCockpit.FormatCoverage`'s Own Empty-State Message

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `EngineeringCockpit.FormatCoverage`'s own zero-denominator text is hardcoded Requirements-specific (`TD-33`), already inaccurately reused by `CalculationsKpiCards`/`VerificationKpiCards`, and deliberately not reused by `ManufacturingKpiCards` for the same reason. A future capability could add an optional `emptyLabel` parameter and update every existing call site. |
| **Status** | Identified — disclosed directly by `TD-33` |
| **Priority** | Low — a small, low-risk, purely additive fix, but outside any one Work Package's own scope to make unprompted |
| **Business Value** | Low — a display-accuracy improvement only, no data-correctness consequence |
| **Engineering Effort** | Low — one optional parameter plus updating three existing call sites |
| **Dependencies** | `EngineeringCockpit.FormatCoverage` (Implemented, `WP 9.1A`) |
| **Proposed Target Release** | Unscheduled — recommended the next time `EngineeringCockpit.cs` is touched for any reason |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 9.1A` (defined the current, hardcoded shape); `WP 9.2A`/`WP 9.3A` (each already reuse it inaccurately); `WP 9.5A` (disclosed the gap via `TD-33`) |
| **Academy Impact** | None until implemented |
| **Notes** | Raised directly by `WP9.5A Future Capability Assessment.md` and `WP9.5A Technical Debt Assessment.md` (`TD-33`). |

#### FCR-0062 — Extending `VerificationService.RecordAsync`'s Own `IHasRelationships` Linking to Cover Inspection Subjects

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | Extends `FCR-0057` (`WP 9.3A`): `WP 9.5A`'s own Inspection recording is a direct, disclosed instance of the identical underlying gap (`TD-32`), now exercised by a second discipline (Manufacturing), strengthening the case for the future capability `FCR-0057` already names. |
| **Status** | Identified |
| **Priority** | Low — `VerificationRecordReader`'s own existing raw-store read already serves every scope item `WP 9.5A`'s own controlling instruction names |
| **Business Value** | Unknown — dependent on a real Workspace-layer consumer needing `RelationshipRepository` to see this specific link directly |
| **Engineering Effort** | Low — identical to `FCR-0057`, since it is the same underlying change |
| **Dependencies** | `VerificationService.RecordAsync` (Implemented, `WP 7.1E`) |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 7.1E` (defined the current, raw-store-only linking shape); `WP 9.3A` (`FCR-0057`, `TD-32`); `WP 9.5A` (a second, real consumer with a genuine stake) |
| **Academy Impact** | Would extend `WP9.5A-manufacturing-workspace.md` once implemented |
| **Notes** | Raised directly by `WP9.5A Future Capability Assessment.md`; extends `FCR-0057` rather than duplicating it. |

#### FCR-0063 — Concrete Cross-Platform .NET Desktop UI Framework Selection

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `ADR-0092` (`WP 10.0A`) decides the Workspace's presentation paradigm moves to a graphical desktop application, but explicitly reserves the concrete framework choice (a WPF/Avalonia/MAUI-shaped decision) as `ADR-0094`, an implementation-phase evaluation — mirroring exactly how `ADR-0066` once deferred "the specific TUI library" the same way. |
| **Status** | **Implemented, `WP 10.0B`** — Avalonia 11.2.3 selected, justified (`ADR-0094`), and integrated into a real, running, tested `Tempest.Desktop` project |
| **Priority** | N/A — resolved |
| **Business Value** | High — realised: unblocked all further graphical Workspace implementation |
| **Engineering Effort** | Medium, as estimated — an evaluation and selection Work Package, not a redesign; every existing Workspace contract confirmed rendering-agnostic, zero contract change required, confirmed empirically by `WP10.0B Engineering Review.md` §3 |
| **Dependencies** | `ADR-0092` (Accepted, `WP 10.0A`); `IWorkspaceView`/`IWorkspacePanel` (Implemented, `WP 8.0B`, unchanged) |
| **Proposed Target Release** | `v0.10.0` — realised, `WP 10.0B`, the Work Package immediately after `WP 10.0A`, exactly as recommended |
| **Related ADRs** | `ADR-0092`; `ADR-0094` (this capability's own resolution) |
| **Related Work Packages** | `WP 10.0A` (reserved `ADR-0094`); `WP 10.0B` (resolved it) |
| **Academy Impact** | Realised — `20-desktop-application-framework.md` (new concept guide, `WP 10.0B`) |
| **Notes** | Raised directly by `WP10.0A UX Architecture Document.md` §17 and `WP10.0A Systems Engineering Review.md` §5; resolved directly by `WP10.0B Implementation Report.md` §1 and `ADR-0094`. |

#### FCR-0064 — `WorkspaceDockPosition`/`WorkspacePanelPlacement` Contract Extension for Floating Panels and Multi-Monitor Placement

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `WorkspaceDockPosition` deliberately has no `Floating` value (`WP 8.0A`'s own "Deliberately Out of Scope"), and neither it nor `WorkspacePanelPlacement` carries a monitor concept. `WP10.0A UX Architecture Document.md` §4/§15 names both as required for full multi-monitor/undocked-panel support and reserves the contract question as `ADR-0095`, explicitly not designed under `WP 10.0A`'s own "no contract changes" constraint. |
| **Status** | Identified |
| **Priority** | Medium — the majority of `WP 10.0A`'s own scope (docked-panel behaviour, single-window multi-monitor spanning) does not depend on this; only true panel undocking and per-monitor placement do |
| **Business Value** | Medium — required for Journey 6 (`WP10.0A Navigation & Workflow Diagrams.md` §7) to be fully realised |
| **Engineering Effort** | Medium — an additive enum value plus a new field on `WorkspacePanelPlacement`, mirroring `ADR-0080`'s/`ADR-0083`'s own established "extend additively" pattern, never a reopened contract |
| **Dependencies** | `WorkspaceDockPosition`/`WorkspacePanelPlacement` (Implemented, `WP 8.0B`); `ADR-0092` (Accepted, `WP 10.0A`) |
| **Proposed Target Release** | `v0.10.0`, a Contract Review Work Package, mirroring `WP 8.0B`'s own relationship to `WP 8.0A` |
| **Related ADRs** | `ADR-0095` (reserved, this capability's own eventual resolution) |
| **Related Work Packages** | `WP 10.0A` (reserved `ADR-0095`) |
| **Academy Impact** | Would extend `19-user-experience-and-desktop-application.md` once resolved |
| **Notes** | Raised directly by `WP10.0A UX Architecture Document.md` §4, §15 and `WP10.0A Security Review.md` §4 (the multi-monitor physical-exposure consideration this same contract extension should account for). |

#### FCR-0065 — Notification Framework Workspace Integration

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `WP 6.2`'s own Notification Framework exists with zero Workspace-level presentation consumer. `WP10.0A UX Architecture Document.md` §10 names a Notification tray as a required Workspace surface, composing the existing framework read-only, but explicitly defers which `INotificationService` methods a future implementation Work Package actually calls. |
| **Status** | Identified |
| **Priority** | Low — no other capability in `WP 10.0A`'s own scope depends on this being resolved first |
| **Business Value** | Medium — Attention Items/Open Actions (`EngineeringCockpit`) already give engineers a derived-state view; Notifications add discrete, timestamped event awareness on top |
| **Engineering Effort** | Low-Medium — no new Platform Service; a Workspace-layer read-only consumer of the existing Notification Framework |
| **Dependencies** | `Tempest.Core` Notification Framework (Implemented, `WP 6.2`) |
| **Proposed Target Release** | Unscheduled — recommended after `FCR-0063`/`FCR-0064` |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 6.2` (defined the current framework); `WP 10.0A` (named the requirement) |
| **Academy Impact** | None until implemented |
| **Notes** | Raised directly by `WP10.0A UX Architecture Document.md` §10; permission-gating consideration flagged by `WP10.0A Security Review.md` §5 — must be honoured by whichever future Work Package implements this. |

#### FCR-0066 — Uniform `Move*Command` Shape, Enabling Real Drag/Drop Reparenting

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `WP 10.2A`'s own Project Explorer drag/drop is deliberately "preparation architecture" only (`ADR-0096`'s own precedent, applied narrowly) — a real drag begins with a real payload and `DragOver` gives real feedback, but `Drop` is a documented no-op. Each discipline's own `Move*Command` already exists but is not yet uniform in shape across all six disciplines, unlike Rename/Delete's own now-uniform `(Guid, string[, string])` shape (`ADR-0096`) — a future Work Package standardising it the same way would let the Project Explorer's own `Drop` handler dispatch a real reparent command generically, exactly mirroring how `RegisterRenameFactory`/`RegisterDeleteFactory` already work. |
| **Status** | **Implemented, `WP 10.7A`** — real, not via the `IWorkspaceManager` extension this entry originally proposed. `Drop` now raises a new `ProjectExplorerView.ObjectMoveRequested` event (mirroring the View's own existing `ObjectOpened`/`ToggleFavouriteRequested` shape) carrying (draggedId, draggedKind, newParentId); `MainWindow` maps `Kind` to the correct discipline's own already-registered `Move*Command` directly via `ICommandDispatcher`, never touching `IWorkspaceManager` — a lighter, "no new architecture" route this entry's own original Engineering Effort estimate did not anticipate. Guards against dropping onto self/a descendant. Requirements' own two Move commands (`MoveRequirementCommand`/`MoveRequirementGroupCommand`, fixed-Kind, no `targetKind` parameter) and `RequirementCollection` (no Move command at all) are handled by the same dispatch switch — the latter reports an honest "isn't supported yet" message rather than throwing. Proven both by a real reparent test (`FeatureCompletionTests.ObjectMoveRequested_ForARealMechanicalAssembly_...`) and by this Work Package's own required interactive runtime pass. |
| **Priority** | Low — no other capability depends on this; disclosed, not blocking |
| **Business Value** | Medium — drag-and-drop reparenting is a common, expected desktop-application affordance once a tree view supports multi-select and context menus, both now real (`WP 10.2A`) |
| **Engineering Effort** | Medium — requires auditing and, where needed, reshaping six disciplines' own `Move*Command` constructors to a common shape, then two new `IWorkspaceManager` members (`RegisterMoveFactory`/`MoveObjectAsync`) mirroring `ADR-0096` a third time |
| **Dependencies** | `ADR-0096` (established the Kind-keyed dispatch pattern this would reuse) |
| **Proposed Target Release** | `v0.10.0` (delivered) |
| **Related ADRs** | `ADR-0096` (the pattern this entry originally proposed extending; `WP 10.7A` took a lighter, non-contract-changing route instead) |
| **Related Work Packages** | `WP 10.2A` (named this as future work directly); `WP 10.7A` (implemented, per `WP10.6D`'s own audit) |
| **Academy Impact** | Would extend `23-workspace-modernisation.md` in place, not a new article |
| **Notes** | Raised directly by `WP10.2A Implementation Report.md` §2 and `23-workspace-modernisation.md` §12 (Future Evolution); implemented `WP10.7A Implementation Report.md` §2. |

#### FCR-0067 — Theme-Variant-Aware Overlay Backgrounds

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `Tempest.Desktop`'s own two overlay controls — `CommandPaletteOverlay` (`WP 10.0B`, `Background = Brushes.Black`) and `PanelHostControl` in its Auto-Hide flyout role (`WP 10.2B`, `Background = Brushes.White`) — both use a fixed, hardcoded brush rather than a `DynamicResource`-bound one resolved from the active `ThemeVariant`. Both remain visually correct only in one theme (dark text implied for the black palette; the white panel is simply wrong once the user is in Dark theme, `TD-39`). A real fix gives both controls' own `Background` a `DynamicResource` binding to a theme-appropriate resource key instead. |
| **Status** | **Implemented, `WP 10.5A`** |
| **Priority** | Low — cosmetic only, no functional defect; both controls remained fully usable in either theme before this fix |
| **Business Value** | Low-Medium — a small but real visual-polish gap once a user actually keeps Dark theme (`ThemeService`, `WP 10.0B`/`WP 10.2A`) as their working default, most visible for the newer Auto-Hide flyout, which sits directly over live Document Area content |
| **Engineering Effort** | Low — realised as `ApplicationPalette`/`ThemeReactiveBrush` (`WP 10.5A`), a real, shared, theme-reactive brush-binding helper — see `WP10.5A Implementation Report.md` §1-2 |
| **Dependencies** | None — `ThemeService`/`RequestedThemeVariant` (`WP 10.0B`) already existed |
| **Proposed Target Release** | `v0.10.0` (delivered) |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 10.0B` (introduced the first instance, `CommandPaletteOverlay`, never previously registered); `WP 10.2B` (introduced the second instance, found and registered both together); `WP 10.5A` (implemented) |
| **Academy Impact** | Realised in `28-workspace-visual-polish.md` |
| **Notes** | Raised directly by `WP10.2B Engineering Review.md` §4 and Technical Debt Register `TD-39` (both now Resolved, `WP 10.5A`). |

#### FCR-0068 — Discipline-Specific Object Editor Enhancements

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `ObjectEditorView` (`WP 10.3A`) is deliberately one generic engine over Name/Content/Lifecycle/Relationships/Validation, applied uniformly across all six disciplines. Real, valuable, discipline-specific enhancements remain unbuilt: a structured BOM-fields editing section for Mechanical (`SetBomLineCommand`); Owner/Priority editing for Requirements (`SetRequirementOwnerCommand`/`SetRequirementPriorityCommand`); an Execute/Recalculate action for Calculations; a Record Result action for Verification; an Attachments section for Documents (`IHasAttachments`); lifecycle-transition actions (Release/Archive/etc.) surfaced directly in the editor rather than only via the Project Explorer's own context menu. |
| **Status** | **Implemented, `WP 10.7A`** — all five sections built exactly as this entry named them, each a Kind-gated `Expander` (`IsVisible = false` until `PopulateFrom`'s own gate matches — a C# `is` type-check against the real object for BOM/Attachments/Record-Result, `_objectKind`-string for Requirements Owner/Priority and Calculations Execute, since those two need a service the object graph itself does not expose), each dispatching its own already-registered command via a newly-threaded `ICommandDispatcher` constructor parameter. **One genuine, disclosed, pre-existing gap found during implementation, not caused by this Work Package**: `ObjectEditorView.TryCreate` gates on `EngineeringDomainContext.Repository.FindAsync` resolving a real `IEngineeringObject` — confirmed by direct test that this call returns `null` for every real Requirement (Requirements are real `IEngineeringDocument`s, `ADR-0058`, but were never wired into the general repository's own Kind-to-object materialisation, only reachable via `IRequirementsService` directly) — so the Requirements Owner/Priority section, while correctly implemented, is currently unreachable specifically through the Object Editor for Requirements (the identical gap the pre-existing `NavigateToObject_ClickedFromARelationshipRow_...` test already silently defended against since `WP 10.3A`, now formally disclosed). The underlying capability remains genuinely reachable via the Ribbon's own `"requirements.set-owner"`/`"requirements.set-priority"` handlers, which dispatch the identical commands directly, independent of `ObjectEditorView`. A second, genuine bug found and fixed in place before commit: every section's own success/failure status message was being immediately overwritten by the subsequent `Refresh()` call re-running `PopulateFrom` — reordered (`Refresh()` first, status message set after) in all five sections. |
| **Priority** | Medium — each of the five named enhancements dispatches through an already-existing, already-tested Command; the missing piece is Desktop-layer UI only |
| **Business Value** | Medium-High — closes the gap between "the generic editor shows real data" and "the generic editor is the single place to do every common per-object action," reducing how often a user needs to leave the editor tab to reach the Project Explorer's own context menu |
| **Engineering Effort** | Medium — no new Command/Domain capability required for any of the five; each is a new, Kind-gated `Expander` section in `ObjectEditorView`'s own already-established layout pattern (`BuildLayout`), reusing an already-registered command directly |
| **Dependencies** | None — every underlying Command already exists and is already registered |
| **Proposed Target Release** | `v0.10.0` (delivered) |
| **Related ADRs** | `ADR-0083` (BOM line); `ADR-0089` (Verification Record Result); `ADR-0096`/`ADR-0097` (the dispatch pattern any new section would reuse) |
| **Related Work Packages** | `WP 10.3A` (named this as future work directly); `WP 10.7A` (implemented, per `WP10.6D`'s own audit) |
| **Academy Impact** | Would extend `25-engineering-object-editors.md` in place, not a new article |
| **Notes** | Raised directly by `WP10.3A Implementation Report.md` §3, `WP10.3A Architecture Review.md` §3, and `WP10.3A UX Review.md` §1/§5; implemented `WP10.7A Implementation Report.md` §3, disclosed gap `WP10.7A Implementation Report.md` §5, tracked as `TD-41`. |

#### FCR-0069 — Real, Authored Per-Command Icons

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `CommandDescriptor.Icon` (`Tempest.Core.Commands`, `WP 6.x`) has never been populated by any of this platform's own ~80 registered commands, across every discipline, since the field's own introduction — confirmed by direct `grep`, zero matches. `RibbonView` (`WP 10.3B`) stands in with a deterministic, verb-suffix-derived glyph, so every Rename button across all six disciplines currently looks identical, and every command in the same verb-group shares one icon regardless of its own more specific meaning. A real fix populates `icon:` at each of the ~80 real `RegisterDescriptor` call sites with a genuinely distinct, authored symbol. |
| **Status** | Identified |
| **Priority** | Low — cosmetic only; the current, deterministic fallback is real, working, and disclosed, not broken |
| **Business Value** | Low-Medium — a real visual-polish improvement once a user works across multiple disciplines' own Ribbon tabs regularly enough to notice the repetition |
| **Engineering Effort** | Medium — mechanical but wide: one `icon:` argument added per already-existing `CommandDescriptor` constructor call, across all six `*WorkspaceRegistration.cs` files, no new capability required |
| **Dependencies** | None — `CommandDescriptor.Icon` already exists, unmodified, since `WP 6.x` |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 10.3B` (found the gap and built the deterministic fallback that stands in for it today) |
| **Academy Impact** | Would extend `26-ribbon-and-command-experience.md` in place, not a new article |
| **Notes** | Raised directly by `WP10.3B Implementation Report.md` §1 and `WP10.3B UX Review.md` §5. |

#### FCR-0070 — Digital Thread Graph Clustering/Pruning for Dense Objects

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `DigitalThreadGraphModel` (`WP 10.4A`) implements no automatic pruning, clustering, or heuristic layout simplification for a densely-connected object (e.g. a Routing with many Operations) — exactly the first-iteration limitation `WP10.0A Digital Thread & Relationship Visualisation.md` §5 already disclosed and accepted before implementation began. After 2-3 expansions such a graph can become visually unwieldy; the engineer must manage node count manually today (collapsing nodes, filtering categories) rather than the graph offering to summarise a dense neighbourhood automatically. |
| **Status** | Identified |
| **Priority** | Low — a real, disclosed, accepted first-iteration limitation, not a defect; no user report exists yet |
| **Business Value** | Medium — would materially improve usability for the densest real objects (large Routings, heavily-referenced Standards/Specifications), but only once real usage demonstrates the current manual controls (collapse, category filter, search) are insufficient |
| **Engineering Effort** | Medium-High — a real clustering heuristic (e.g. group same-Kind, same-Category neighbours beyond a threshold into one summary node) is a genuine new algorithm, not a mechanical extension of `DigitalThreadGraphModel`'s own existing structure |
| **Dependencies** | None — purely additive to `DigitalThreadGraphModel`'s own existing node/edge model |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | `ADR-0093` (names this exact limitation as an accepted Negative Consequence) |
| **Related Work Packages** | `WP 10.0A` (first disclosed); `WP 10.4A` (implemented without it, as planned) |
| **Academy Impact** | Would extend `27-digital-thread-visualisation.md` in place, not a new article |
| **Notes** | Raised directly by `WP10.0A Digital Thread & Relationship Visualisation.md` §5, `ADR-0093`'s own Consequences (Negative), and `WP10.4A Performance Review.md` §3/§4. |

#### FCR-0071 — A Comprehensive, Hand-Authored Vector Icon Library

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `IconRegistry` (`WP 10.0B`, refreshed `WP 10.5A`) resolves every Kind to a single Unicode text glyph — now a consistent, monochrome, theme-reactive set (`WP 10.5A` Phase 2), a real improvement over the prior mixed full-colour-emoji set, but still text-glyph-based, not a true vector icon. `IconGeometry` (`WP 10.5A`, new) proves the platform can render real, hand-authored `StreamGeometry` vector icons — four exist today (Close, Check, ChevronRight, ChevronDown), used only by the newest feedback controls' own interactive chrome, not yet extended to any of `IconRegistry`'s own ~20 Kind glyphs or `FCR-0069`'s own per-command icons. |
| **Status** | Identified |
| **Priority** | Low — cosmetic; the current monochrome Unicode set is real, working, and already a disclosed improvement over the prior full-colour-emoji baseline |
| **Business Value** | Low-Medium — a further, incremental visual-polish improvement once a user compares this platform's own iconography against a mature commercial CAD/PLM tool's own hand-designed icon set |
| **Engineering Effort** | High — authoring ~20-30 genuinely well-designed vector icons (one per Kind, potentially one per command per `FCR-0069`) by hand, without a design tool in this environment, is a real, substantial effort, not a mechanical extension of `IconGeometry`'s own existing four-icon pattern |
| **Dependencies** | None — `IconGeometry`'s own pattern (a `StreamGeometry` constant, rendered via a `Path` inheriting `Foreground`) already exists and needs no extension, only more content |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 10.0B` (Phase 1, full-colour emoji); `WP 10.5A` (Phase 2, monochrome Unicode + the first four real vector icons); `FCR-0069` (the identical, still-open per-command icon gap this would also close) |
| **Academy Impact** | Would extend `28-workspace-visual-polish.md` in place, not a new article |
| **Notes** | Raised directly by `WP10.5A Implementation Report.md` §4/§8 and `WP10.5A UX Review.md` §5. |

#### FCR-0072 — Split/Tiled Document View

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `DocumentAreaView` (`WP 10.0B` onward) renders exactly one active document tab at a time — no split, tiled, or side-by-side view of two open documents exists anywhere in this platform, so comparing two Engineering Objects (or a Requirement against its own Verification Activity) always means switching tabs back and forth, never seeing both at once. Named directly by this Work Package's own controlling instruction ("split-document presentation") but not attempted — a genuine new capability, not a polish item, and a materially larger change to `DocumentAreaView`'s own tab-hosting model than this Work Package's own realistic scope. |
| **Status** | Identified |
| **Priority** | Medium — a real, named, plausible productivity feature for any engineer regularly cross-referencing two open objects |
| **Business Value** | Medium — most valuable for exactly the cross-discipline traceability scenarios `ADR-0093`'s own Digital Thread graph already partially addresses (though the graph shows relationships, not full side-by-side object content) |
| **Engineering Effort** | High — `DocumentAreaView`'s own single-`TabControl` model would need a real, new layout concept (a second pane, a splitter, tab-to-pane assignment); not a mechanical extension of any existing class |
| **Dependencies** | None identified — purely additive to `Tempest.Desktop`, no Workspace contract implicated |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 10.5A` (named directly, deliberately not attempted) |
| **Academy Impact** | Would warrant its own new concept guide once designed — a real layout-architecture decision, not an in-place extension |
| **Notes** | Raised directly by `WP10.5A Implementation Report.md` §8 and this Work Package's own controlling instruction ("Multi-document Experience... split-document presentation"). |

#### FCR-0073 — Copy/Move Destination-Picker Dialog & Wired Dispatch

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `WP 10.5B`'s own Dialog Framework wires real Create/Delete/Duplicate flows but leaves Copy and Move entirely unwired — `InputDialog`'s own single-text-field shape cannot collect a destination parent, which both operations genuinely need, and no existing dialog offers a tree-position picker. |
| **Status** | Identified |
| **Priority** | Medium — a real, named workflow gap; users can still reparent structurally via `WP 10.2A`'s own drag/drop where implemented, so this is a missing *dialog-driven* path, not the only path |
| **Business Value** | Medium — most valuable for large structural reorganisations where drag/drop across a long, scrolled Project Explorer tree is impractical |
| **Engineering Effort** | Medium — a new dialog (a filtered tree or searchable list of valid destination parents) plus wiring into `RibbonView.ObjectCreationHandlers`'s own established pattern; the dispatch side (`MoveMechanicalObjectCommand`-shaped commands) already exists for at least Mechanical |
| **Dependencies** | None new — reuses `ICommandDispatcher.DispatchAsync`, `RibbonView.ObjectCreationHandlers`'s own established shape |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 10.5B` (named directly, deliberately not attempted) |
| **Academy Impact** | Would extend `29-desktop-workflow-and-professional-interaction.md` in place, not a new article |
| **Notes** | Raised directly by `WP10.5B Implementation Report.md` §8. |

#### FCR-0074 — Export/Import Commands & Dialog Wiring

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `WP 10.5B`'s own instruction names Export/Import dialogs directly, but no underlying Workspace command to dispatch exists for either operation anywhere in this platform today — confirmed directly, no `Export*Command`/`Import*Command` exists in any discipline's own Command set. A dialog with nothing real to dispatch to would be a non-functional placeholder, deliberately not built. |
| **Status** | Identified |
| **Priority** | Low-Medium — no user-facing demand has been recorded yet; genuinely useful once any external interchange format (a real file-format ADR) is chosen |
| **Business Value** | Medium — data interchange with external CAD/PLM/requirements tools is a plausible, common enterprise need, but entirely speculative until a concrete format is scoped |
| **Engineering Effort** | High — requires its own format-selection ADR before any command or dialog work begins; not a mechanical extension of any existing pattern |
| **Dependencies** | A concrete file-interchange format decision (new ADR) must precede any implementation |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None yet — would need one |
| **Related Work Packages** | `WP 10.5B` (named directly, deliberately not attempted) |
| **Academy Impact** | Would warrant its own new concept guide once a format is chosen — a real new capability, not an in-place extension |
| **Notes** | Raised directly by `WP10.5B Implementation Report.md` §8. |

#### FCR-0075 — Uniform Create/Duplicate Wiring Across All Six Disciplines

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `WP 10.5B`'s own real object-creation workflow (`RibbonView.ObjectCreationHandlers`) is wired for Mechanical only. The other five disciplines' own Create commands have genuinely different constructor shapes (Requirements alone has three: Requirement, Requirement Collection, Requirement Group) — extending uniformly is real, substantial, disclosed future work, not a copy-paste of the Mechanical handler. |
| **Status** | **Implemented, `WP 10.7A`** — Create wired for Calculations/Documents/Manufacturing (each defaults every optional constructor parameter beyond name, mirroring Mechanical's own precedent) and Requirements (all three real Create commands — Requirement, Group, Collection — each its own Ribbon entry); Verification's own Create genuinely uses the current selection as `SubjectId`. Duplicate wired for Calculations/Documents/Verification/Manufacturing (a shared handler factory) and Requirements (its own dedicated handler — `DuplicateRequirementCommand`'s own `newIdentifier` is required, not optional, unlike every other discipline). Status transitions (Approve/Archive/Lock/Unlock/Request-Review/Release) also wired for all five status-bearing disciplines as a direct consequence of the identical `ObjectCreationHandlers` seam — see `WP10.7A Implementation Report.md` §1. Copy remains unwired (needs a destination-parent picker dialog that does not exist — disclosed, deliberately out of this Work Package's own scope, see `FCR-0073`). |
| **Priority** | Medium — every other discipline's own Ribbon Create button still falls through to the pre-existing honest "needs additional input" message, a real, disclosed, non-broken degraded state, not a crash or a lie |
| **Business Value** | High — the single most direct route to making every discipline's own Workspace as genuinely usable end-to-end as Mechanical's now is |
| **Engineering Effort** | Medium per discipline — the `InputDialog`/`ConfirmationDialog` + `ICommandDispatcher.DispatchAsync` pattern `WP 10.5B` established is real and reusable; the effort is in each discipline's own distinct command shape, not new dialog infrastructure |
| **Dependencies** | None new — reuses `WP 10.5B`'s own established Dialog Framework and `ObjectCreationHandlers` pattern directly |
| **Proposed Target Release** | `v0.10.0` (delivered, Create/Duplicate/status transitions; Copy still unscheduled) |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 10.5B` (established the pattern for Mechanical only, named this gap directly); `WP 10.7A` (implemented for the remaining five disciplines, per `WP10.6D`'s own audit); `WP 10.8A` (Manufacturing's own "Record Inspection Result" wired — the one remaining genuinely-unwired verb `WP10.6D`'s own audit had not separately named — plus a related, disclosed messaging fix, see Notes) |
| **Academy Impact** | Would extend `29-desktop-workflow-and-professional-interaction.md` in place, not a new article |
| **Notes** | Raised directly by `WP10.5B Implementation Report.md` §4/§8; implemented `WP10.7A Implementation Report.md` §1. **`WP 10.8A` update**: `"manufacturing.record-inspection-result"` wired (`RecordVerificationResultCommand`, the identical command the Object Editor's own Verification section already dispatches — a disclosed cross-Work-Package reuse, not a new command). Separately, and not itself part of this capability's own scope: the Ribbon's own final fallback message (`RibbonView.OnCommandButtonClickAsync`, reached by every verb still genuinely unwired, including Copy) previously claimed a command "not yet collected" was reachable via "the Command Palette (Ctrl+K)" or "Project Explorer's own context menu" — confirmed false for every command reaching that branch (`ADR-0070`/§11 of `26-ribbon-and-command-experience.md`: `CreateDefault` is `null` everywhere, so the Palette cannot invoke any real discipline command by Id at all). Corrected to name no false alternative — see `Technical Debt Register.md`'s own `WP 10.8A` narrative entry and `26-ribbon-and-command-experience.md` §Future Evolution. |

#### FCR-0076 — Startup Splash Screen

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `WP 10.5B`'s own controlling instruction names "startup splash behaviour" and "application loading sequence" directly; not attempted — a real risk of shipping something visually unverifiable in this environment (no way to observe a transient splash render/dismiss timing directly) outweighed its value against `WindowUiState`'s own real, verified startup-restoration work. |
| **Status** | Identified |
| **Priority** | Low — purely cosmetic; module discovery/`WorkspaceHost` startup is already fast enough in practice that a splash screen addresses perception, not a real waiting problem |
| **Business Value** | Low — a conventional desktop-application polish expectation, not a functional gap |
| **Engineering Effort** | Low-Medium — a new transient `Window` shown before `MainWindow`, dismissed once startup completes; the main risk is visual verification, not implementation complexity |
| **Dependencies** | None |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 10.5B` (named directly, deliberately not attempted) |
| **Academy Impact** | Would extend `29-desktop-workflow-and-professional-interaction.md` in place, not a new article |
| **Notes** | Raised directly by `WP10.5B Implementation Report.md` §8. |

#### FCR-0077 — Customisable Keyboard Shortcuts, Ribbon & Toolbar Preferences

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `WP 10.5B`'s own instruction names keyboard-shortcut and Ribbon/toolbar preferences directly; `UserSettings` (new this Work Package) deliberately does not persist a preference for any of these, because no underlying remapping/customisation *capability* exists anywhere in this platform to configure — shortcuts are fixed in code, the Ribbon's own layout is fixed in `RibbonView`. Persisting a setting for a capability that does not exist would be dishonest. |
| **Status** | Identified |
| **Priority** | Low — no user-facing demand recorded; the current fixed shortcuts/layout are real, working, and undisputed |
| **Business Value** | Low-Medium — a mature-desktop-application expectation, most valuable to power users with strong prior-tool muscle memory |
| **Engineering Effort** | High — requires a genuine key-binding registry/remapping engine and a Ribbon layout-customisation model, both real new subsystems, not settings-persistence extensions |
| **Dependencies** | None identified |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 10.5B` (named directly, deliberately not attempted) |
| **Academy Impact** | Would warrant its own new concept guide once designed |
| **Notes** | Raised directly by `WP10.5B Implementation Report.md` §8. |

#### FCR-0078 — Undo/Redo Coverage Beyond Rename and Favourite Toggle

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `WP 10.6A`'s own `IUndoRedoStack`/`UndoableAction` architecture (`ADR-0098`) is wired for exactly two real cases — Rename (all six disciplines, via `IWorkspaceManager.RenameObjectAsync`'s own Kind-agnostic dispatch) and the new Favourite/Un-favourite toggle. Create/Delete/Duplicate/Move, and every Set-Status/Set-Priority/Set-Owner command across all six disciplines, remain dispatched exactly as before, with no Undo entry recorded. Delete in particular has no defined inverse today — it is already a soft delete with no "restore" operation anywhere in this platform to invert into. |
| **Status** | Identified |
| **Priority** | Medium — genuine productivity gap once a user relies on Undo for one action type and expects it for others |
| **Business Value** | Medium-High — Undo/Redo is a baseline professional-desktop-application expectation; partial coverage is a real, disclosed gap |
| **Engineering Effort** | Medium — each additional command needs its own captured pre-state at its own UI call site (`UndoableAction`'s own established shape already proven); Delete specifically needs a real "restore" operation defined first |
| **Dependencies** | A real "restore a soft-deleted object" capability, not yet built anywhere in this platform, before Delete can be undone |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | ADR-0098 |
| **Related Work Packages** | `WP 10.6A` (named directly, deliberately scoped down) |
| **Academy Impact** | Would extend `30-command-execution-and-productivity-experience.md` |
| **Notes** | Raised directly by `WP10.6A Implementation Report.md` §8; tracked as `AT-18` in the Technical Debt Register. |

#### FCR-0079 — Background Task Percentage Progress Reporting

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `IBackgroundTaskRunner` (`WP 10.6A`) reports coarse state only (Running/Succeeded/Failed/Cancelled) plus elapsed time — never a percentage. No `ICommandHandler<TCommand>` anywhere in this platform reports incremental progress; that frozen Command Framework contract carries no `IProgress<T>` parameter, and adding one would be a Command Framework-wide redesign, explicitly out of this Work Package's own scope. |
| **Status** | Identified |
| **Priority** | Low — no currently-registered command handler runs long enough for percentage progress to matter in practice |
| **Business Value** | Low-Medium — mostly valuable once a genuinely long-running engineering operation exists (a large import, a batch recalculation) |
| **Engineering Effort** | High — requires extending `ICommandHandler<TCommand>.HandleAsync` (or a parallel, opt-in contract) to accept an `IProgress<T>`, touching the Command Framework itself |
| **Dependencies** | A real, demonstrated long-running command handler that would benefit — none exists yet |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 10.6A` (named directly, deliberately not attempted) |
| **Academy Impact** | Would extend `30-command-execution-and-productivity-experience.md` |
| **Notes** | Raised directly by `WP10.6A Implementation Report.md` §8; tracked as `AT-19` in the Technical Debt Register. |

#### FCR-0080 — Macro Steps Eligible Beyond `CreateDefault`-Invokable Commands

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | A macro step (`ADR-0099`) must be a `CommandDescriptor` with `CreateDefault` set — today, confirmed by repository-wide `grep`, that means only `Tempest.Samples` commands qualify. No real Engineering discipline command (Create/Rename/Revise/Delete/Set-Status, etc.) is invokable by Id alone, since each needs UI-collected context `InvokeAsync`'s parameterless contract cannot supply — the identical, pre-existing, platform-wide limitation `CommandPaletteOverlay`'s own remarks already document for the Command Palette itself. |
| **Status** | Identified |
| **Priority** | Medium — directly limits the Macro foundation's own real-world usefulness until resolved |
| **Business Value** | High — this is the single biggest lever on making the Macro foundation genuinely useful for real engineering workflows, not just Sample commands |
| **Engineering Effort** | High — needs either a per-command "collect missing parameters" UI step during macro authoring, or a broader rework of which commands can be made honestly parameterless-invokable |
| **Dependencies** | None identified beyond the design work itself |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | ADR-0099 |
| **Related Work Packages** | `WP 10.6A` (named directly, deliberately scoped down); `AT-10` (`WP 6.3`, the identical root limitation named for the REST API's own `MapCommand`) |
| **Academy Impact** | Would extend `30-command-execution-and-productivity-experience.md` |
| **Notes** | Raised directly by `WP10.6A Implementation Report.md` §8; tracked as `AT-20` in the Technical Debt Register. |

#### FCR-0081 — Command History as a Real `ICommandDispatcher` Interception, Not a UI-Surface Aggregation

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `CommandHistoryLog` (`WP 10.6A`) records only what already reaches `MainWindow`'s own existing `ActionCompleted`-shaped UI surfaces (Ribbon, Explorer, Inspector, Object Editor, Undo/Redo) — not a global interception of `ICommandDispatcher` itself, which remains completely unmodified. A command dispatched through any future path that bypasses these surfaces would not appear in the History. `succeeded` is also a disclosed heuristic (inferred from whether the recorded message contains "fail"), since these UI surfaces carry only a human-readable string, not a structured `CommandResult`. |
| **Status** | Identified |
| **Priority** | Low — every currently-real dispatch path already funnels through one of the recorded surfaces |
| **Business Value** | Low-Medium — would matter primarily for audit-grade completeness, not day-to-day productivity |
| **Engineering Effort** | Medium — either a genuine `ICommandDispatcher` decorator (touching Command Framework composition) or a broader `ActionCompleted`-signature change across five Desktop views to carry a real `CommandResult` instead of a string |
| **Dependencies** | None identified |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | ADR-0098 |
| **Related Work Packages** | `WP 10.6A` (named directly, deliberately scoped down) |
| **Academy Impact** | Would extend `30-command-execution-and-productivity-experience.md` |
| **Notes** | Raised directly by `WP10.6A Implementation Report.md` §8; tracked as `AT-21` in the Technical Debt Register. |

#### FCR-0082 — Persisted (Cross-Session) Undo/Redo and Command History

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `IUndoRedoStack`/`CommandHistoryLog` (`WP 10.6A`) are both deliberately session-only — closing and reopening TempestOS discards both, matching most desktop applications' own established convention, but a real, disclosed limitation nonetheless. |
| **Status** | Identified |
| **Priority** | Low — matches mainstream desktop-application convention; no user-facing demand recorded |
| **Business Value** | Low — genuinely optional; most desktop applications behave identically |
| **Engineering Effort** | Medium — would need a real, bounded, `ISettingsProvider`-backed persisted shape for both, mirroring `RecentObjectsState`'s own established pattern |
| **Dependencies** | None identified |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | ADR-0098 |
| **Related Work Packages** | `WP 10.6A` (named directly, deliberately not attempted) |
| **Academy Impact** | Would extend `30-command-execution-and-productivity-experience.md` |
| **Notes** | Raised directly by `WP10.6A Implementation Report.md` §8; tracked as `AT-22` in the Technical Debt Register. |

#### FCR-0083 — Keyboard Remapping UI and a Real External Controller (Stream Deck/MIDI) Integration

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `KeyboardCommandBindingProvider` (`ADR-0100`) is a real, working, tested `gesture → Command Id` mechanism, shipped with zero default bindings and no end-user UI to author them. `IExternalControllerProvider` is a real, proven contract (against a test-only `StubExternalControllerProvider`) with zero real vendor integration — no Stream Deck plugin, no MIDI device support, no hardware integration, all explicitly out of `WP 10.6A`'s own scope. |
| **Status** | Identified |
| **Priority** | Low — no user-facing demand recorded; the architecture is real and proven, only the concrete UI/hardware layers are missing |
| **Business Value** | Medium — a keyboard remapping UI is a mainstream expectation; a real Stream Deck/MIDI integration is a genuine differentiator for a professional engineering tool but a large, separate undertaking |
| **Engineering Effort** | Low (remapping UI, over the already-real `KeyboardCommandBindingProvider.Bind`/`Unbind` API) to High (a real vendor SDK integration, its own Work Package) |
| **Dependencies** | A real vendor SDK dependency for any real external controller — explicitly out of scope for this platform to add without a dedicated commissioning decision |
| **Proposed Target Release** | Unscheduled |
| **Related ADRs** | ADR-0100 |
| **Related Work Packages** | `WP 10.6A` (named directly, deliberately scoped to architecture + one real software provider + one test double) |
| **Academy Impact** | Would extend `30-command-execution-and-productivity-experience.md` |
| **Notes** | Raised directly by `WP10.6A Implementation Report.md` §8; tracked as `AT-23` in the Technical Debt Register. |

#### FCR-0084 — A Typed Callback Interface for `WorkspaceViewCoordinator`'s Three Bundled Callbacks

| Field | Value |
|---|---|
| **Category** | Workspace |
| **Description** | `WorkspaceViewCoordinator`'s own constructor now takes three genuine, delegate-shaped callback parameters (`refreshStatusBar`, `recordHistory`, `refreshCockpit`) — reaching `ADR-0104`'s own stated threshold ("three or more logically-related, delegate-shaped callback parameters") for introducing a small, purpose-named typed callback interface in place of the separate delegates. `WP 12.4B` deliberately did not introduce one in the same change that first reached the threshold, per the user's own explicit instruction not to introduce a typed callback interface speculatively — an interface added in the identical change that crosses the threshold, with no second, independent need yet demonstrated, would not have "materially reduced constructor complexity" so much as anticipated a reduction. This entry exists so that decision is tracked as a real, named future opportunity rather than left only as prose in a Work Package retrospective. |
| **Status** | Identified, not started |
| **Priority** | Low — `WorkspaceViewCoordinator`'s constructor is functionally correct and fully tested today; this is a code-quality/readability improvement, not a defect |
| **Business Value** | Low — no user-facing behaviour changes; the benefit is entirely to future maintainers reading or extending this one collaborator's own constructor |
| **Engineering Effort** | Low — `ADR-0104` already specifies the shape (a small, purpose-named interface bundling the three callbacks); no architectural design work remains, only the mechanical introduction and `MainWindow`'s own adapter/implementation |
| **Dependencies** | None technical. A fourth genuinely bundleable callback need arising for the same collaborator would strengthen, not block, the case |
| **Proposed Target Release** | Unscheduled — revisit if `WorkspaceViewCoordinator` gains a fourth genuine callback, or if a future architecture/code review judges the three-callback constructor has become materially harder to read in practice |
| **Related ADRs** | ADR-0104 (defines the three-callback threshold this entry exists to satisfy); ADR-0103 (the general composition-root/collaborator pattern `WorkspaceViewCoordinator` is extracted under) |
| **Related Work Packages** | `WP 12.4B` (Desktop Command & Event Wiring Implementation — added the third callback, `refreshCockpit`, closing `WP 12.0B`'s own architecture review Finding 5; deliberately deferred introducing the interface in the same change) |
| **Academy Impact** | Would extend `docs/academy/03 Work Packages/WP12.4B-desktop-command-and-event-wiring-implementation.md`'s own §2.4 discussion, and `Desktop Command & Event Wiring Architecture.md`'s own "Typed callback interfaces" evaluation, once implemented |
| **Notes** | Raised directly by the `WP 12.4B` architecture/code review's own Finding 2 (governance omission — this deferred decision existed only as narrative prose, not a trackable register entry, prior to this Work Package's own follow-up). Not tracked in the Technical Debt Register — this is a forward-looking design opportunity meeting this register's own criteria directly, not a defect or an accepted trade-off already shipped. |

### Project Management

#### FCR-0028 — Project Engine / Secure Project Data Management

| Field | Value |
|---|---|
| **Category** | Project Management |
| **Description** | A platform capability for programme/project-level planning, scheduling, and tracking of a real engineering effort, reviving or replacing the bootstrap-era `JsonProjectRepository`/`ProjectModel` subsystem — with encryption at rest, access control, and audit logging for classified/export-controlled fields designed in from the start, not retrofitted. |
| **Status** | Identified — aspirational; the only concrete trace is dormant, unreferenced, pre-Claude-era code |
| **Priority** | Unknown — no Work Package has yet been proposed |
| **Business Value** | Unknown — dependent entirely on TempestOS's eventual engineering-domain customer base |
| **Engineering Effort** | Unknown — requires its own Architecture Work Package; security design (encryption, access control, audit) must be part of that same design phase, not a follow-up, per `Security Roadmap.md` item 4 |
| **Dependencies** | Requires its own explicit Platform-Service-vs-Module classification decision (`ADR-0013`); benefits from `FCR-0021` (multi-user/tenant) if concurrent access is in scope; benefits from `FCR-0029` (shared data model, identified `WP 7.0B`) rather than building its own storage shape |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | ADR-0013 (names this capability directly in its own Future Considerations) |
| **Related Work Packages** | None yet — `JsonProjectRepository`/`ProjectModel` predate the Claude-developed history |
| **Academy Impact** | None until designed |
| **Notes** | Sourced from `PROJECT_STATUS.md`'s own Long-Term Vision section; `ADR-0013`'s own Future Considerations; `Security Roadmap.md` item 4 (`FS-1`, `FS-2`); `Threat Model.md` assumptions 1–3. |

## Coverage Note

**Disclosed, pre-existing drift, not introduced or fixed by this
Work Package's own follow-up**: the "77 capabilities identified" figure
and the paragraphs below it were last updated at `WP 10.5B` and were
never revised when `WP 10.6A` added `FCR-0078`–`FCR-0083` (77 → 83) or
when this Work Package's own follow-up added `FCR-0084` (83 → 84) — the
authoritative, current total is the metadata table's own "Last
Reviewed" field, above (84). Named here plainly rather than silently
carried forward as though this section were current; a full rewrite of
this narrative note is out of this Work Package's own narrow scope
(documentation/governance follow-up to the `WP 12.4B` architecture
review only).

**77 capabilities identified** (`FCR-0001` through `FCR-0077`) as of
`WP 10.5B`, the count this note's own text below was last written
against.
`FCR-0073`–`FCR-0077` (Copy/Move Destination-Picker Dialog & Wired
Dispatch; Export/Import Commands & Dialog Wiring; Uniform Create/
Duplicate Wiring Across All Six Disciplines; Startup Splash Screen;
Customisable Keyboard Shortcuts, Ribbon & Toolbar Preferences) were all
added by `WP 10.5B`, sourced directly from that Work Package's own five
disclosed scope reductions (`WP10.5B Implementation Report.md` §8) —
each a real, deliberate, honestly-named gap, none a defect; 72 → 77
total.
`FCR-0072` (Split/Tiled Document View) and `FCR-0071` (A Comprehensive,
Hand-Authored Vector Icon Library) were both added by `WP 10.5A`,
sourced directly from that Work Package's own disclosed scope
reductions; `FCR-0067` (Theme-Variant-Aware Overlay Backgrounds) was
marked **Implemented** by the same Work Package — `TD-39`'s own
resolution.
`FCR-0070` (Digital Thread Graph Clustering/Pruning for Dense Objects)
was added by `WP 10.4A`, sourced directly from `ADR-0093`'s own
already-disclosed, already-accepted first-iteration limitation, carried
forward into this register for the first time now that a real
implementation exists to attach the finding to.
`FCR-0069` (Real, Authored Per-Command Icons) was added by `WP 10.3B`,
sourced directly from the same confirmed-by-`grep` finding that
`CommandDescriptor.Icon` has never been populated anywhere.
`FCR-0068` (Discipline-Specific Object Editor Enhancements) was added
by `WP 10.3A`, sourced directly from its own disclosed "one generic
engine, no bespoke per-discipline layout yet" scope decision.
`FCR-0067` (Theme-Variant-Aware Overlay Backgrounds) was added by `WP
10.2B`, sourced directly from `TD-39` (`Technical Debt Register.md`) —
`CommandPaletteOverlay`'s own identical, previously-unregistered fixed-
brush limitation (`WP 10.0B`) and `PanelHostControl`'s own new one (`WP
10.2B`) surfaced and registered together for the first time. `FCR-0066`
was added by `WP 10.2A`, sourced directly from its own
disclosed drag/drop-preparation trade-off (`WP10.2A Implementation
Report.md` §2). `FCR-0063`–`FCR-0065` were added by `WP 10.0A`, sourced
directly from its
own UX Architecture Document and companion reviews (the concrete
desktop UI framework selection `ADR-0094` reserves, the floating/
multi-monitor panel contract extension `ADR-0095` reserves, and
Notification Framework Workspace integration), not inferred — the
first entries in this register raised by an architecture-only Work
Package rather than an implementation one.
`FCR-0060`–`FCR-0062` were added by `WP 9.5A`, sourced directly from its
own implementation-experience findings (the `Classification`-tagged
Routing/Supplier Operation representation, `TD-33`'s own
`FormatCoverage` finding, and extending `WP 9.3A`'s own `FCR-0057` to a
second, real consumer), not inferred.
`FCR-0057`–`FCR-0059` were added by `WP 9.3A`, sourced directly from its
own implementation-experience findings (`TD-32`, the disclosed
`LifecycleState`-only Approval State treatment extending `FCR-0052`, and
the undedicated Witness-information field), not inferred.
`FCR-0054`–`FCR-0056` were added by `WP 9.4A`, sourced directly from its
own implementation-experience findings (`TD-31`, the disclosed `WP 9.3A`
numbering gap, and the Governance & Risk Domain classes' own proven
Workspace-readiness), not inferred.
`FCR-0001`–`FCR-0028` were each traceable to a specific, cited,
pre-existing document, established `WP 7.0A`. `FCR-0029`–`FCR-0033`
were added by `WP 7.0B`'s own Capability Dependency Analysis — each
marked **Inferred**, architectural necessity reasoning rather than a
capability named in a prior document, and each says so explicitly in
its own Notes field. `FCR-0034` was added by `WP 7.1B`, found during
real implementation rather than planning-stage inference — the first
entry in this register sourced from an implementation Work Package's
own disclosed finding rather than a retrospective, a Technical Debt
Register entry, or architectural-necessity reasoning. `FCR-0035` was
added by `WP 7.1D`, sourced from that Work Package's own required
Security Review — the first entry in this register sourced from a
security review specifically. `FCR-0036` was added by `WP 7.1E`, sourced
from that Work Package's own required Security Review — the second such
entry, and the last added by the Engineering Foundation programme, now
complete. `FCR-0027` (Requirements Engine) progressed to **Implemented**
by `WP 7.3A` — the first Systems Engineering Foundation capability to
complete the full Identified → Architecture → Contracts → Implemented
sequence. `FCR-0037` and `FCR-0038` were added by `WP 7.3A`, sourced
directly from that Work Package's own `Future Capability
Recommendations.md` — disclosed implementation-experience findings, not
inferred. `FCR-0039`–`FCR-0043` were added by `WP 9.0A`, sourced directly
from that Work Package's own `WP9.0A Future Capability Assessment.md` —
the first entries in a new "Workspace" category, disclosed
implementation-experience findings, not inferred. `FCR-0044`–`FCR-0047`
were added by `WP 9.0B`, sourced directly from `WP9.0B Future Capability
Assessment.md` — the same "Workspace" category, disclosed
implementation-experience findings, not inferred. No entry was invented
to fill a category without disclosing that it was inferred rather than
sourced.

`Materials` and `Quality` each now have exactly one entry (`FCR-0031`,
`FCR-0033` respectively) — both cross-cutting *foundation* capabilities
identified as prerequisites for their category's own eventual discipline
modules, not a discipline-specific capability within either category.
**Five of the nine Engineering Discipline categories still have zero
entries** (Mechanical, Structural, Electrical, Building Services/HVAC,
Manufacturing) — disclosed explicitly, not papered over. `AI` and
`Academy` likewise remain at their `WP 7.0A` count (one and zero
respectively). Identifying real candidates for the five still-empty
disciplines remains recommended as its own future exercise engaging
real engineering-domain stakeholders — `WP 7.0B`'s own Capability
Dependency Analysis deliberately confined itself to cross-cutting
*foundation* reasoning (what any discipline module would structurally
need), not discipline-specific capability invention, exactly as `WP
7.0A` itself declined to do.

## Cross-Reference Check

Every `TD-NN`/`AT-NN` citation above is cross-checked directly against
`Technical Debt Register.md` — no citation references a debt item or
trade-off that does not exist there, and no `TD`/`AT` item disclosing a
genuine future capability (as opposed to a pure code-quality concern with
no roadmap relevance, such as `TD-05`, `TD-07`, `TD-08`, all already
Resolved) is missing an `FCR` entry above. Every `Security Roadmap.md`
item (1 through 10) is represented in at least one entry above, save
items 6 and 7 (authentication, API/networking), both already substantially
addressed by shipped `v0.6.0` capability (`WP 6.1`, `WP 6.3`) and
therefore not carried forward as a future capability in their original
form — item 6's remaining gap is `FCR-0003`. Every `WP6.x Future
Capability Recommendations.md` document's own recommendations are
represented above or explicitly excluded as already-resolved,
implementation-pattern guidance rather than a genuine future capability
(for example, "reuse the existing permission pattern" recommendations
are guidance for a future Work Package's own implementation discipline,
not a capability in their own right, and are not separately registered).
Every `FCR-0029`–`FCR-0033` cross-reference (to each other, and to
`FCR-0001`–`FCR-0028`) is verified consistent in both directions — see
`WP7.0B Capability Dependency Report.md` for the full dependency graph
this check is drawn from.

## Related Documents

`Capability Categories.md`; `Product Roadmap.md`; `VISION.md`;
`docs/governance/Quality/Technical Debt Register.md`;
`docs/security/Security Roadmap.md`; `docs/security/Threat Model.md`;
`ADR-0013`; `PROJECT_STATUS.md`; `docs/releases/v0.7.0/WorkPackages.md`;
`docs/releases/v0.7.0/WP7.0B Capability Dependency Report.md`;
`docs/releases/v0.7.0/WP7.1B Implementation Report.md`.
