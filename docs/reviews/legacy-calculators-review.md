# Legacy calculation modules (v0.21.0–v0.25.4) against the v0.26.0 `Tempest.Calc` standard

Requested by the owner on 2026-10-23 ("review all of the currently built work … if they're not as high quality replace it; if higher, raise the new work"). Reviewed: the sixteen modules under `src/Tempest.Core/Calculations/` (five original definitions in `EngineeringCalculationDefinitions.cs`; eleven `WP 21.7A` modules; tolerance stack and two thermal modules recovered from v0.16.0), their specs under `docs/engineering/calculations/`, their tests under `tests/Tempest.Core.Tests/Calculations/`, and the Desktop surface `CalculationModulesView` (Engineering → Modules → Calculators, Re-run/Compare). Compared against the §5 Definition of Done the sixteen v0.26.0 templates were built to.

## Quality matrix

| Dimension (§5 DoD) | Legacy modules | v0.26.0 templates | Higher |
|---|---|---|---|
| Transparent working: formula → substitution → result on a sheet | No. `CalculationContext.RecordIntermediate` records named values only; no formula text, no substitution. | Yes, every step, with clause per step; "Show working" in the form; text and PDF sheets. | **New** |
| Evidence lifecycle (Draft → Checked → Issued → Superseded, checker independence, immutable issue) | No. Runs are `CalculationRecord`s in the calculation engine (audited, retained, re-runnable, comparable) but never Evidence; nothing is checked or issued. | Yes: one-transaction Issue, register link, packs, revisions. | **New** |
| Provenance per input (drawing / spec / assumption / evidence / reference) | Partly: reference-library pins for materials, fasteners and bearings (released records only, with refusals for unreleased or incomplete records). No provenance on hand-entered values. | Yes on every input; Manual cannot issue. No reference-library fill in v0.26.0. | Split: **Legacy** for library sourcing, **New** for everything else |
| Standards cited by clause number and title | No clause references in the modules; specs name Shigley / VDI 2230 / Roark by chapter. | Clause number + title per template and per step. | **New** |
| Applicability rules, refusals instead of exceptions | Method limits are refusals (`OutsideMethodLimits`); invalid inputs **throw** `CalculationInputInvalidException`. | Hard limits block, soft limits warn, never throws (`CalcRunner`). | **New** |
| Units | Compile-time `Quantity<T>` types; imperial restatements in vectors; CsCheck unit-invariance property test per module. | Runtime SI dimension vector; unit pickers; CsCheck properties per calculator family. | Even |
| Specification document | 100-line teaching-register spec per module: "In plain terms" paragraph, method, inputs, formulae, outputs, limits, hand-worked examples to five significant figures, edge cases. | 13-line table spec per template (D-04 lean-Markdown rule): scope, method, inputs, outputs, references, limitations, test cases. | **Legacy** (prose and worked arithmetic in the spec) |
| Known-answer tests | 3–4 hand-derived vectors per module as data, plus invalid-input and refusal vectors (≈ 60 vectors across 14 modules). | 47 cases, 219 expected values, recomputed independently (Green), printed in every sheet's footer, 24+ worked examples with files; Red proof-of-concept and Black black-box passes. | **New** |
| Worked examples a user can open | In the spec text only. | Folder per example: README, `inputs.json`, `sheet.txt`, `sheet.pdf`; index README. | **New** |
| Diagrams | Yes, per module (`CalculationDiagrams`, 588 lines), with readings. | `DiagramKey` on 17 templates; chart series (Monte Carlo, system/pump curves, psychrometric). | Even |
| Security and resource caps | Closed-form; no caps needed; no hostile-input review. | Caps on everything numeric; Red, Blue, Purple, Black reviews. | **New** |
| Five original definitions (bolt shear capacity, beam bending stress, bearing load capacity, pressure-vessel wall thickness, material selection margin) | Simplistic (allowable = ultimate / safety factor; no code basis), two intermediates each, no spec document, one test each. | — | **New** by a wide margin |

**Verdict.** The v0.26.0 standard is higher on every dimension that carries the product's thesis (D-028: evidence is the product, calculation is where the engineer does it): working shown, issued through Evidence, provenance, clauses, refusals, worked examples, reviews. The legacy modules are higher on two narrower dimensions — the teaching-register spec with hand arithmetic, and reference-library sourcing of material, fastener and bearing properties — and even on units and diagrams.

## Decision (D-38)

1. **Replace** the sixteen legacy modules with `Tempest.Calc` templates, and retire the legacy Calculators surface, in **v0.27.0** on `release/v0.27.0` (branched from `release/v0.26.0` once `v0.26.0-rc1` is tagged). Mixing the replacement into the gated v0.26.0 candidate would invalidate today's nine reviews.
2. **Raise the new work** to the legacy level where legacy is higher, in the same release: every template spec gains an "In plain terms" paragraph and the hand arithmetic of one worked example (the D-04 line budget is met by counting the examples' README lines, already present); reference-library fill (`CalcProvenance.FromReference` with a released-record pin for materials, fasteners and bearings) is wired into the worksheet form.
3. **Mapping** of the sixteen modules:

| Legacy module | Fate | v0.27.0 template |
|---|---|---|
| `calc.tolerance-stack` (v1) | Superseded | `mech.tolerance-stack` (v2, shipped) |
| `calc.thick-walled-cylinder` | Superseded | `mech.thick-cylinder` (shipped) |
| `calc.bolted-joint-preload` | Superseded | `mech.bolted-joint` (shipped; add the joint-diagram load factors n_0, n_L as outputs) |
| `calc.bolt-group-eccentric-shear` | Superseded | `mech.bolt-group` (shipped) |
| `calc.beam-deflection` | Superseded | `struct.beam-formulas` (shipped; add the legacy cases not yet in the Roark set) |
| `calc.column-buckling` | Superseded | `struct.en1993-member` flexural buckling (shipped) + Euler/Johnson as `struct.column-buckling` |
| `calc.lifting-lug-pin-joint` | Port | `mech.lifting-lug` |
| `calc.fillet-weld-throat-stress` | Port | `struct.fillet-weld` |
| `calc.bearing-rating-life` | Port | `mech.bearing-life` (ISO 281 L10, a·ISO as inputs) |
| `calc.fatigue-miner` | Port | `mech.fatigue-miner` |
| `calc.shaft-combined-stress` | Port | `mech.shaft-stress` |
| `calc.plane-wall-heat-transfer` | Port | `bs.plane-wall` |
| `calc.thermal-resistance-chain` | Port | `bs.thermal-chain` |
| `calc.thermal-expansion-stress` | Port | `mech.thermal-expansion` |
| `calc.bolt-shear-capacity` | Retire (covered) | `mech.bolt-group` single-bolt case |
| `calc.beam-bending-stress` | Retire (covered) | `struct.beam-formulas` + `struct.section-properties` |
| `calc.bearing-load-capacity` | Retire (covered) | `mech.bearing-life` |
| `calc.pressure-vessel-wall-thickness` | Port | `mech.thin-vessel` (thin-wall membrane formulae; the thick-wall case is `mech.thick-cylinder`) |
| `calc.material-selection-margin` | Port | `mech.material-margin` (yield / ultimate margins from a referenced material record) |

4. **Data.** Stored legacy `CalculationRecord`s stay readable: the legacy engine and its read-back, retention and corruption tests stay until a migration writes each record as an issued-less `CalcInstance` snapshot (v0.27.0 Phase 1); nothing is deleted (non-negotiable #3). The legacy Desktop surface is removed only after every module has a template and the migration passes the Grey upgrade test.
5. **Tests.** Legacy vectors are carried over as known-answer cases of the new templates (same figures, same five-significant-figure tolerance), so no expected value is lost; the legacy definition tests are retired with the definitions under an ADR (ADR-0167, to be written at the start of v0.27.0).

## Effort

Nine ports, six supersessions with small additions, two retirements, a record migration, the spec-prose and reference-fill raises, and the Desktop surface removal: roughly the size of v0.26.0 Phases 2–3 plus a Grey migration pass. Phases proposed for v0.27.0: 1 migration + reference fill + spec raise; 2 mechanical ports; 3 structural/thermal ports; 4 Desktop retirement; 5 reviews (Green recomputation of every carried vector, Grey upgrade, Black).
