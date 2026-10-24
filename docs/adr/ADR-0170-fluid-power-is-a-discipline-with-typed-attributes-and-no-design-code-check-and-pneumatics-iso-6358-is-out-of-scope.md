# ADR-0170: Fluid Power Is a Discipline with Typed Attributes and No Design-Code Check, and Pneumatics (ISO 6358) Is Out of Scope

## Status

Accepted (v0.26.0, Addendum B, B-0). Extends `ADR-0163` (disciplines and shared modules) and `ADR-0165` (the worksheet engine).

## Context

Addendum B §B3 makes Fluid Power a discipline "in scope, not optional": hydraulic cylinder and actuator circuits (F1), accumulators (F2) and seal grooves (F3) are queued for B-1 and B-2, and the hydraulic power unit chain (F1 → F2 → B1 → E1 → E4) crosses Fluid Power, Building Services and Electrical.

Two registries describe a discipline, and a discipline needs both:

1. **The engineering objects** (`ADR-0163`): the Engineering tree and a project's Structure tab list Mechanical, Structural, Electrical, Building Services / HVAC and Manufacturing. Structural, Electrical and Building Services share one attribute-carrying object (`DisciplineObject`), with each Kind's attributes declared in `DisciplineCatalogue`.
2. **The calculation disciplines** (`ADR-0165`): `CalcDiscipline` (Mechanical, Structural, Electrical, BuildingServices) groups the worksheet catalogue, the Worksheets nodes, Ctrl+K entries and each calculator assembly (`Tempest.Calc.Mechanical`, `.Structural`, `.Electrical`, `.BuildingServices`).

Fluid power has no home in either. Its first calculators also need two things the existing assemblies do not offer to a second discipline: a gas-law relation (today an inline expression inside the sealed-system worksheet) and the tolerance-stack arithmetic (today private to the M1 worksheet).

## Decision

1. **Fluid Power is a discipline, the fifth calculation discipline and the sixth engineering domain.** Hydraulics first. Pneumatics follows as further Kinds and calculators under the same name; it is not a separate discipline.
2. **Core objects follow `ADR-0163` exactly.** `DisciplineCatalogue.FluidPower` declares three Kinds, each an Explorer folder with typed attributes recorded exactly as the engineer types them:

   | Kind | Folder | Attributes |
   |---|---|---|
   | `FluidPowerCircuit` | Circuits | Circuit type, Working pressure (bar), Fluid, Flow rate (L/min) |
   | `FluidPowerActuator` | Actuators | Actuator type, Bore (mm), Rod diameter (mm), Stroke (mm), Working pressure (bar) |
   | `FluidPowerPowerUnit` | Power units | Pump type, Displacement (cm³/rev), Motor power (kW), Reservoir volume (L), Relief setting (bar) |

   The shared registration wires Create, Rename, Edit, Delete (with undo), Move, Request review, Approve, Release and Set attribute under the command prefix `fluid-power`. **No design-code logic, safety factor or rating check is applied to any attribute**: a working pressure is a recorded number, not a verified rating. This keeps the `Engineering Principles.md` boundary on discipline mathematics, and the same line is held for Fluid Power as for the other three.
3. **Calculators live in their own assembly.** `Tempest.Calc.FluidPower` (depends on Engine and Sheets only, like the other discipline assemblies) exposes `FluidPowerCalculators.All` and is composed in `CalcCalculatorComposition`. `CalcDiscipline.FluidPower` is appended to the enum: the template hash writes the discipline by name, so no existing template's hash changes and no stored calculation goes stale. The Desktop label is "Fluid Power" everywhere a discipline is listed (the Worksheets tree, the Engineering tree and Structure tab, the command category of the Ctrl+K "Open worksheet" entries). `Tempest.Calc.Tests.FluidPower` runs the same definition-of-done checks as the other disciplines over every Fluid Power calculator.
4. **Two kernels are shared rather than copied.**
   - `Tempest.Calc.Engine.Physics.GasLaw`: absolute-pressure helpers and `p₁·V₁ⁿ = p₂·V₂ⁿ` solved for volume or pressure (isothermal n = 1, adiabatic n = κ, polytropic), with the vessel and accumulator displacement forms. It sits in Engine, which every discipline assembly already references, because it is dimension-free SI arithmetic like `Statistics` and `Interpolation`; a new assembly would add a project, a lock file and a CI entry for one static class. B6 (`bs.sealed-system`) uses it with its sheet and numbers unchanged; F2 uses it for accumulator sizing.
   - `Tempest.Calc.Engine.Analysis.ToleranceStack.ToleranceStackEngine`: the worst-case, RSS, capability and seeded Monte Carlo arithmetic of M1, public and pure, in Engine beside the gas-law kernel so that a discipline assembly reaches it without a reference to Mechanical. M1 uses it with its sheet and numbers unchanged; F3 and the scan-to-seal chain call it directly.
5. **Out of scope, stated.**
   - **Pneumatics flow (ISO 6358)** is not in Addendum B. No catalogue placeholder is added: the catalogue has no "coming soon" entry pattern, and a disabled entry would be a promise the build does not keep.
   - **No pressure-vessel code check** (PD 5500, EN 13445, ASME VIII) and no hydraulic-component rating check. F2 reports the bar·litre value and whether it falls below the PSSR Schedule 1 exception, and does nothing more.
   - **Licensed data.** Seal-gland and O-ring size tables (AS568, ISO 3601), extrusion charts and similar are user records under the `licensed_source` lock (Addendum B §B5), never shipped.

## Consequences

- Existing calculations, templates and stored attributes are untouched; nothing migrates. A project opened on this build shows an empty Fluid Power discipline.
- The dashboard export's `disciplines` array (schema v3) gains a fourth entry, `fluid-power`. It is additive; the schema version does not change.
- An engineer can record a 400 bar working pressure against a circuit that nothing checks. That is by design (point 2) and is the same position as a Structural member's section or an Electrical circuit's rating.
- Each later Fluid Power calculator is an addition to `FluidPowerCalculators.All` and its own tests; no further composition work.

## Follow-ups

1. F1 (cylinder and actuator circuit) and F3 (seal groove, via the stack engine) in B-2; F2 (accumulator, via `GasLaw`) in B-1.
2. Pneumatics (ISO 6358 flow, receiver sizing) as a later addendum, under the same discipline.
3. Link Fluid Power objects into the Digital Thread (a circuit to its actuators and power unit), as for the other disciplines (`ADR-0163`, follow-up 5).
