# ADR-0165: Tempest Computes Through a Transparent Worksheet Engine, and Evidence Governs Everything

## Status

Accepted. Product Owner instruction of 2026-10-23 for `v0.26.0` (unattended build). Supersedes the "nothing computes in Tempest" half of D-028 (`docs/releases/v1.0.0/D-028 ...md`); keeps "Evidence is the product".

## Context

D-028 placed calculation outside the platform: an engineer computed in Mathcad, Excel or FEA and the platform held the Evidence. Nineteen generated-form calculators later grew inside Core anyway (`CalculationModuleDescriptors`), each an opaque C# function whose working is a list of named intermediates. They have no shared unit algebra beyond typed `Quantity<T>`, no formula-to-substitution-to-result trail a checker can read, and no known-answer registry.

## Decision

1. **A transparent worksheet engine.** `Tempest.Calc.Engine` evaluates unit-aware expressions over a runtime SI dimension vector, with dimension checking, deterministic evaluation, resource caps, and a step log that records for every assignment the symbolic formula, the substituted values with units, and the result. The decision between embedding CalcpadCE and writing this engine is D-01 in `docs/decisions/v0.26.0-DECISIONS.md`.
2. **Every calculator renders one sheet shape** (`Tempest.Calc.Sheets`): header, scope and method with clause numbers only, inputs with provenance, numbered assumptions, working, checks, applicability warnings and blocks, and a verification footer naming template id and version, content hash, engine version and known-answer cases passed.
3. **Evidence governs.** A calculation instance is issued only through the Evidence lifecycle (Draft → Checked → Issued → Superseded). Issue snapshots inputs, outputs, the rendered sheet, the template text, the engine version and the test results into the immutable Evidence revision in one transaction. Input provenance is mandatory to issue; "assumption" is a valid provenance and appears in the assumptions register. Every issued instance is linked into its project's Calculation register.
4. **Standards are cited, never reproduced.** Clause numbers and titles only; tabulated standard data lives in user-editable reference records with provenance, seeded only with values derivable from published formulae or cited from public textbooks.
5. **External calculations remain first-class Evidence.** Nothing here removes the Mathcad/Excel/FEA path.
6. **Seams.** Only `Tempest.Calc.Library` depends on Core (Evidence, reference data, persistence, Disciplines). `Tempest.Calc.Sheets` depends on Engine only. Calculator assemblies depend on Engine and Sheets.

## Consequences

- The nineteen existing calculators keep working. The 1D tolerance stack and the thick-walled cylinder are re-implemented on the engine in `v0.26.0`; their old tests stay and pass.
- A sheet's content hash and engine version are part of the issued record; a changed template version or upstream input marks dependent instances stale.
- Licence rule: in-process code is MIT, MIT-0, BSD or Apache-2.0; no copyleft in process.
