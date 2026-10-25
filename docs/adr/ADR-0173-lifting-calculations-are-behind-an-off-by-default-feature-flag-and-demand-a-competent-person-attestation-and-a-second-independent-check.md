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

- `CalcTemplate.FeatureFlag` names the feature (`CalcFeatures.Lifting` = `"lifting"` for S8). `CalcFeatureFlags` is the set of features an installation has switched on, read once from the installation's configuration (`IConfigurationProvider`, the same immutable, load-before-start source the timesheet export folder uses): **`Calc:Features:Lifting = true`**, the exact word `true` in any case, nothing else (`1`, `yes`, `on`, a typo: off). Configuration reaches the product through `appsettings.json` next to the executable, the environment (`TEMPEST_` prefix) or the command line, which is an installation-level act; it is not a user setting, a command or an interface toggle. `CalcFeatureFlags` has no setter and the catalogue's copy is fixed at construction, so nothing running can switch a flag on.
- `CalcCatalogue` validates and keeps a calculator whose flag is off, but in a **withheld** set: it is absent from `All()`, `Current()`, `ByDiscipline()`, `Find()` and `Get()`. Everything that lists calculators (the Worksheets tree, the Ctrl+K "Open worksheet" entries, which are built from `Current()`) and everything that resolves a template reference therefore cannot see it. `Get()` throws `CalcFeatureDisabledException` (a `KeyNotFoundException`) whose message says the feature is switched off, names the configuration key and says why; `FeatureRefusal()` gives the same sentence to the paths that return a refusal.
- **Every service path refuses a withheld template**, before any other reason: `CreateAsync`, `Preview`, `OutputPorts`, `UpdateInputsAsync`, `CheckAsync`, `CountersignAsync`, `IssueAsync`, `ReviseAsync`, `BindAsync`, `BindCellAsync`, `RunAsync`, `CreatePackAsync`, `CheckPackAsync`, `CountersignPackAsync`, `IssuePackAsync`, and pack-template validation and instantiation (`ValidatePackTemplate`, `InstantiatePackTemplateAsync`). An instance that already exists (made while the flag was on, or written into the store directly) stays in the project and in search by its name, but cannot be edited, checked, issued or revised while the flag is off; nothing about it is deleted.
- JSON and CSV import parse any template they are handed and create nothing; the values they produce reach a calculation only through `CreateAsync`, which refuses. The Desktop import acts on the selected catalogue entry, which cannot be a withheld one.
- The calculator stays in `StructuralCalculators.All`, so the cross-discipline definition-of-done tests (known answers, hostile numbers, golden sheet, worked example) cover it without the flag; only the catalogue withholds it.
- The flag is read in `WorkspaceHost` when the catalogue is composed. `CalcCatalogue` has no way to change the flag afterwards, so enabling it means editing the installation's configuration and restarting.

### 2. Governance is declared by the template and enforced once, in the Library

A template opts in with three properties; a template that sets none is governed exactly as before and its content hash is unchanged (the canonical text adds a line only when a property is set):

| Property | Meaning |
|---|---|
| `RequiresAttestation` | Check and Issue are refused until the inputs `attest_name`, `attest_qualification` and `attest_statement` (ordinary optional text inputs the template declares, so they print on the sheet) are non-blank (name and qualification at least three characters, statement at least twenty) and the named person is **not** the calculation's creator or anyone who set or changed its inputs. Names and sign-in ids are compared without case, punctuation or spacing, and a `DOMAIN\name` or `name@domain` sign-in also gives its bare name, so "Jane Smith" is `jane.smith`. |
| `RequiresSecondCheck` | After the first Check, `CountersignAsync` (or `CountersignPackAsync` for a pack) records a second signature from a person who is signed in, who is not the creator or an editor, and who is not the first checker (by sign-in or by typed name). Issue is refused without an accepted second check. It requires `IndependentCheck`, so the first checker is also independent of the authors. A rejection is recorded, stops Issue, and lets the checked calculation be revised (`ADR-0166`: never a dead end). |
| `FeatureFlag` | As above. |

The catalogue refuses to register a template that asks for governance it cannot meet (an attestation without the three inputs, a second check without an independent first check, an unknown flag).

The second signature is held on the instance (`CalcInstance.SecondCheck`) or pack (`CalcPack.SecondCheck`), beside the Evidence's own single check record, and is frozen into the issued snapshot, because Core's Evidence has one check record per revision. The first checker's sign-in is kept (`CheckerPrincipal`) only for templates that need the comparison. The issued sheet's header names both: "checker; second check: name". The attestation prints in the inputs table like any input. **Check demands the attestation** and **Issue re-demands it and the second check** against the stored instance, so a record written into the store around the service still cannot be issued.

### 3. The sheet and the scope

S8's sheet prints, in its scope statement and again as an output, that it is a **design calculation, not a thorough examination**, and the LOLER/PUWER scope notes (the thorough examination, marking, the competent person and test certificates stay outside the sheet, with the checklist as text). Nothing in the Library prints it; it is part of the calculator's template.

### 4. No copyrighted standard content

ASME BTH-1 and EN 13155 text and tables are not shipped. The BTH-1 design factor Nd by Design Category, the allowable-stress divisors, the bearing multiple, the dishing-ratio limit, the service-class factor and the proof-load factor are **user records** (record kinds, no defaults). The sheet cites clause numbers only (BTH-1 3-3.3; EN 13155; EN 13001-3-1).

## Consequences

- With the flag off (the default), S8 is invisible and inert in a shipped build. Turning it on is a deliberate act in the installation's configuration, after §B10 item 6.
- Existing calculators, instances, packs and stored records are untouched; an `Instance` and a `Pack` gain nullable fields that are absent from stored JSON until a governed template uses them.
- The second signature is not an Evidence check record. An audit of the Evidence alone shows one check; the issued sheet and the instance show both. If Core later holds several check records per Evidence, `SecondCheck` can move there without changing the rule.
- The competent-person attestation is a statement typed into the sheet, compared with sign-in ids by name. The product cannot verify that a person is competent or that a typed name is the person who signed in; the check is that it is not the author. The owner's checklist and the thorough examination are the real controls.
- A pack that holds an S8 calculation needs its own second check (`CountersignPackAsync`), by someone who is none of the members' authors.

## Follow-ups

1. §B10 item 6: review the S8 equations and the user records against BTH-1 (2023 or later) and EN 13155+A1 before the flag is ever enabled; record the review in the owner test pack.
2. Chain 6 (lifting beam: S8 → S7 → S4/M6 → S1) is the integrator's, with the flag on in the test host only.
3. Consider a visible "feature off" marker on an existing instance in the project tree.
