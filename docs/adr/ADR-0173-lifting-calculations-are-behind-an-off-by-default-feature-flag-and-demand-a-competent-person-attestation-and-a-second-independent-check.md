# ADR-0173: Lifting Calculations Are Behind an Off-by-Default Feature Flag, and Calculations That Demand It Need a Competent-Person Attestation and a Second Independent Check

## Status

Accepted (v0.26.0, Addendum B, B-5). Extends `ADR-0165` (the worksheet engine), `ADR-0166` (a checked calculation is never a dead end), `ADR-0168` (linking and staleness) and the independent-check rule of Addendum A §A3.

## Context

Addendum B §B6 S8 adds lifting lugs and spreader/lifting beams. A lifting appliance is regulated work (LOLER and PUWER in the UK): a design calculation is one input to a thorough examination by a competent person, never a substitute for it. §B5 therefore asks for three things the worksheet engine did not have:

1. **A feature flag, off by default.** The calculator is built, tested and composed in, but no one can create, find or issue a calculation with it until the owner has completed the review checklist against the standards (§B10 item 6).
2. **A competent-person attestation** at Issue.
3. **A second Checked signature** at Issue, from a checker independent of the first.

Two things were missing underneath. There was no place in the product that gates an optional capability: the nearest thing, `src/Frozen/` (guarded by `FrozenCapabilitiesUnreachableTests`), parks code outside the build altogether, which would leave the calculator unbuildable and untestable, and `UserSettings` is a per-user interface preference, which a regulatory gate must not be. And the single check record that Core's Evidence holds ("a second check is refused", R-19) cannot carry two signatures.

## Decision

### 1. The feature flag is a catalogue property, fixed at composition

- `CalcTemplate.FeatureFlag` names the feature (`CalcFeatures.Lifting` = `"lifting"` for S8). `CalcFeatureFlags` is the set of features an installation has switched on. It is read **only from the `appsettings.json` in the folder beside the executable** (`CalcFeatureFlags.FromInstallFile`): `{ "Calc": { "Features": { "Lifting": true } } }`, the JSON value `true` (or the string `true`, any case) and nothing else (`1`, `yes`, `on`, a typo, a file that does not parse, a file over 1 MB: off). That folder is the installation's, which an ordinary user cannot write. **The environment, the command line and the current directory are never read for a feature flag**, so an engineer cannot self-enable an unreviewed calculator by starting the application differently (review M7; an earlier draft read the shared configuration provider, which does read those). It is not a user setting, a command or an interface toggle. `CalcFeatureFlags` has no setter and the catalogue's copy is fixed at construction, so nothing running can switch a flag on. When a feature is enabled the host logs a warning at startup naming the feature and the file it came from, and every issued snapshot of a gated calculator records "lifting feature enabled (source)" so that the sheet shows how the feature was switched on.
- `CalcCatalogue` validates and keeps a calculator whose flag is off, but in a **withheld** set: it is absent from `All()`, `Current()`, `ByDiscipline()`, `Find()` and `Get()`. Everything that lists calculators (the Worksheets tree, the Ctrl+K "Open worksheet" entries, which are built from `Current()`) and everything that resolves a template reference therefore cannot see it. `Get()` throws `CalcFeatureDisabledException` (a `KeyNotFoundException`) whose message says the feature is switched off, names the configuration key and says why; `FeatureRefusal()` gives the same sentence to the paths that return a refusal.
- **Every service path refuses a withheld template**, before any other reason: `CreateAsync`, `Preview`, `OutputPorts`, `UpdateInputsAsync`, `AttestAsync`, `CheckAsync`, `CountersignAsync`, `IssueAsync`, `ReviseAsync`, `BindAsync`, `BindCellAsync`, `RunAsync`, `CreatePackAsync`, `CheckPackAsync`, `CountersignPackAsync`, `IssuePackAsync`, and pack-template validation and instantiation (`ValidatePackTemplate`, `InstantiatePackTemplateAsync`). An instance that already exists (made while the flag was on, or written into the store directly) stays in the project and in search by its name, but cannot be edited, checked, issued or revised while the flag is off; nothing about it is deleted.
- JSON and CSV import parse any template they are handed and create nothing; the values they produce reach a calculation only through `CreateAsync`, which refuses. The Desktop import acts on the selected catalogue entry, which cannot be a withheld one.
- The calculator stays in `StructuralCalculators.All`, so the cross-discipline definition-of-done tests (known answers, hostile numbers, golden sheet, worked example) cover it without the flag; only the catalogue withholds it.
- The flag is read in `WorkspaceHost` when the catalogue is composed. `CalcCatalogue` has no way to change the flag afterwards, so enabling it means editing the installation's `appsettings.json` and restarting.
- **The older lifting-lug and pin-joint module (`calc.lifting-lug-pin-joint`, Engineering Calculators) is gated by the same flag** (review M8): `CalculationModuleDescriptors.FeatureOf` names the feature, `CalculationModuleService` refuses it with `ModuleSwitchedOff` while the feature is off, and the Engineering Calculators workbench (`AvailableCatalogue`) does not offer it. Every other module is unaffected.
- **Links and external evidence** respect the flag (review Lows 10, 11): a link whose upstream calculation is switched off is refused (`PortProblem`), and `AttachExternalEvidenceAsync` refuses a switched-off template like every other path.

### 2. Governance is declared by the template and enforced once, in the Library

A template opts in with three properties; a template that sets none is governed exactly as before and its content hash is unchanged (the canonical text adds a line only when a property is set):

| Property | Meaning |
|---|---|
| `RequiresAttestation` | An accepting Check and Issue are refused until `AttestAsync` has recorded the attestation. It is made by the **signed-in principal** (as `CountersignAsync` is), who must be a known person and none of the calculation's authors: its creator, anyone who set or changed an input, or (at pack issue) any author of any member of the pack. The comparison is by sign-in id and by the display name the principal directory gives it (on Windows the sign-in id is a SID, which no typed name resembles), without case, punctuation or spacing. The competent person's name, qualification and statement (name and qualification at least three characters, statement at least twenty) are display text: they are written into the three inputs `attest_name`, `attest_qualification` and `attest_statement` (ordinary optional text inputs the template declares, so they print on the sheet) with the attester as their source, and stored in the instance with the principal, the time and a hash of the inputs. **Any later change to an input voids the attestation.** The first checker must not be the attester. A *rejecting* first check needs no attestation (a calculation can be rejected without being vouched for). |
| `RequiresSecondCheck` | After the first Check, `CountersignAsync` (or `CountersignPackAsync` for a pack) records a second signature from a person who is signed in, who is not the creator or an editor, who is not the attester, and who is not the first checker (by sign-in or by typed name). The statement cannot be empty (at least twenty characters: what was verified, or why it is rejected). Issue is refused without an accepted second check. It requires `IndependentCheck`, so the first checker is also independent of the authors. A rejection is recorded, stops Issue, and lets the checked calculation be revised (`ADR-0166`: never a dead end). |
| `FeatureFlag` | As above. |

The catalogue refuses to register a template that asks for governance it cannot meet (an attestation without the three inputs, a second check without an independent first check, an unknown flag).

The second signature is held on the instance (`CalcInstance.SecondCheck`) or pack (`CalcPack.SecondCheck`), beside the Evidence's own single check record, and is frozen into the issued snapshot, because Core's Evidence has one check record per revision. The first checker's sign-in is kept (`CheckerPrincipal`) only for templates that need the comparison. The issued sheet's header names both: "checker; second check: name". The attestation prints in the inputs table like any input. **An accepting Check demands the attestation** and **Issue re-demands it and the second check** against the stored instance, so a record written into the store around the service still cannot be issued.

The Desktop worksheet form offers the two acts (review M9): an **Attest** step on a draft and a **Countersign** step on a checked calculation, shown only for a template that requires them, both recorded under the person signed in. An S8 calculation can therefore be taken from draft to issued, by four different people, through the form alone.

### 3. The Evidence of a calculation is managed by its worksheet

A calculation's Evidence record is created through `IEvidenceService.CreateManagedAsync` and carries `Evidence.ManagedBy = "calc.worksheet"` (review H2). The generic Evidence acts `RecordCheckAsync`, `IssueAsync` and `ReviseAsync` **refuse managed Evidence** (`EvidenceRefusal.ManagedElsewhere`); only `CalcInstanceService`, passing the `EvidenceOwner` token through the `…InOneTransactionAsync` overloads, can check or issue it, so only after the worksheet's own governance (feature flag, attestation, independent checkers, second check) has passed. Evidence made before the marker existed is adopted (`ManageAsync`) at its next check or issue. The Evidence editor hides Check, Issue and Revise for managed Evidence and offers **Open worksheet** (which finds the owning calculation or pack and opens it). Evidence that is not owned by a worksheet behaves exactly as before. This closes the same hole for every calculator, not only S8 (it was follow-up 5 of the first version of this decision). The owner token is a routing rule inside one trusted process, not a security boundary: code in the process could construct it, as it could write the store directly.

### 4. What the product does not do: signatures are not authenticated

**Signatures are not authenticated by TempestOS** (review H4). The author, the checkers, the attester and the second checker are the signed-in principals of the installation (a Windows account, or a "Switch person" in a single-user installation). The product records them, compares them and refuses the same person twice; it does not verify that the person is who they say, that the attester is competent, that the second checker is independent of the first in fact (two colleagues who agree, or one person with two accounts), or that anyone examined the equipment. The sheet says so, in its scope, in an assumption and as the output `sheet_notice`; the spec document carries it into the §B10 item 6 checklist; the Desktop form shows it beside the sign-off steps. It is the owner's and the company's to establish who may attest, check and countersign, and how that is evidenced, before the flag is enabled. There is no platform change in this decision.

### 5. The sheet and the scope

S8's sheet prints, in its scope statement and again as an output, that it is a **design calculation, not a thorough examination**, and the LOLER/PUWER scope notes (the thorough examination, marking, the competent person and test certificates stay outside the sheet, with the checklist as text). Nothing in the Library prints it; it is part of the calculator's template.

### 6. No copyrighted standard content

ASME BTH-1 and EN 13155 text and tables are not shipped. The BTH-1 design factor Nd by Design Category, the allowable-stress divisors, the bearing multiple, the dishing-ratio limit, the service-class factor and the proof-load factor are **user records** (record kinds, no defaults). The sheet cites clause numbers only (BTH-1 3-3.3; EN 13155; EN 13001-3-1). The published course example the equations are checked against (PDHonline S106, a secondary source) is cited, never reproduced; all six of its published values are pinned in the Secondary case `ka-pdhonline-s106` and reproduced by an independent recompute.

### 7. Questions the owner must answer before the flag is enabled

Choices the sheet makes that the standards, not the author, must settle (spec document, "Owner questions"): whether the company's procedure for attesting, checking and countersigning is adequate given unauthenticated signatures; the four-lug rule (`n_eff = min(n_lugs, 3)`: three share, a fourth is not relied on); the definition of the dishing ratio (`D_h/t` against a user-record limit); whether the pin-clearance factor `C_r` has a cut-off (it is applied with none: conservative); whether the 6° tilt is a pitch case and a roll case taken one at a time or both at once; and whether BTH-1 takes separate allowables for yield-governed and fracture-governed sections (the sheet uses `min(f_y/n_y, f_u/n_u)` for all).

## Consequences

- With the flag off (the default), S8 is invisible and inert in a shipped build. Turning it on is a deliberate act in the installation's configuration, after §B10 item 6.
- Existing calculators, instances, packs and stored records are untouched; an `Instance` and a `Pack` gain nullable fields that are absent from stored JSON until a governed template uses them.
- The second signature is not an Evidence check record. An audit of the Evidence alone shows one check; the issued sheet and the instance show both. If Core later holds several check records per Evidence, `SecondCheck` can move there without changing the rule.
- The competent-person attestation is recorded under the signed-in principal with the typed name, qualification and statement as display text. The product cannot verify that a person is competent or that a typed name is the person who signed in; the check is that the signed-in person is not the author. The owner's checklist and the thorough examination are the real controls (section 4).
- A pack that holds an S8 calculation needs its own second check (`CountersignPackAsync`), by someone who is none of the members' authors.

## Follow-ups

1. §B10 item 6: review the S8 equations and the user records against BTH-1 (2023 or later) and EN 13155+A1 before the flag is ever enabled, answer the owner questions (section 7) and record the review in the owner test pack.
2. Chain 6 (lifting beam: S8 → S7 → S4/M6 → S1) is the integrator's, with the flag on in the test host only.
3. Consider a visible "feature off" marker on an existing instance in the project tree.
4. **Residual:** the generic engine command `calculations.execute` can still run the legacy lifting-lug definition by its id, because the engine registers the product calculations when the host starts. The Engineering Calculators surface and `CalculationModuleService` are gated (section 1); the engine command line is not, and gating it belongs to the engine's registration.

Resolved since the first version of this decision: the Desktop Countersign and Attest steps (section 2) and the Evidence workspace commands (section 3).
