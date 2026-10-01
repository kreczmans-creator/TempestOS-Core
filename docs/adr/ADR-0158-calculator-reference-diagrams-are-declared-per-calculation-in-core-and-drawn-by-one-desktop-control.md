# ADR-0158: Calculator Reference Diagrams Are Declared per Calculation in Core and Drawn by One Desktop Control

## Status

Accepted — Product Owner feedback `PO-2` (2026-10-01), work packages A, B,
C and D1 of the scope recorded in
`docs/reviews/RC PO Feedback Actions (2026-10-01).md`.

## Context

The Engineering Calculators (`WP 21.7B`) generate every form from
`CalculationModuleDescriptors`; nothing was drawn. The Product Owner
asked for a reference diagram per calculator and answered the scope's
questions: a **fixed panel beside the inputs**, **not to scale**,
**inputs only** (no deflected shape, no stress marker), and a field and
its shape highlighted together.

## Decision

**1. A diagram is data, in Core.** `CalculationDiagramSpec`
(`Tempest.Core.Calculations.Modules.Diagrams`) lists a calculation's
variants; each variant is a list of shapes on a 400 × 240 sheet —
members, plates, circles, supports (pinned, roller, fixed), point and
distributed loads, moments, springs, dimensions and labels — each bound
by name to the input it stands for. A variant is selected by the form's
own values (`Support`/`Loading` for the beam, `ShearPlanes = 2` for the
bolt). `CalculationDiagrams.All` holds the diagrams.

**2. A label reads the form as typed.** `CalculationDiagramReader` labels
a bound shape `L = 2000 mm`, or `L = ?` when the value cannot be read
(blank, not a number, a unit the dimension does not have), using the
same unit parser the calculation uses. It never guesses and never
computes a result. The same reading gives a screen-reader summary.

**3. One Desktop control draws every diagram.** `CalculationDiagramView`
builds plain Avalonia shapes from the spec, coloured from `BrandPalette`
and `ApplicationPalette` tokens (focus highlight is the focus-ring
token), marks every drawing "Not to scale · inputs only", and shows "No
diagram yet" where there is none. `CalculationModulesView` places it
beside the inputs, redraws it on every field change, highlights a
field's shapes on focus, and marks or focuses a field when its shape is
pointed at or clicked.

**4. Coverage is enforced, and only shrinks.** A Core test requires every
calculation in `CalculationModuleDescriptors.All` to have a diagram or
sit on `CalculationDiagrams.NoDiagramYet`, every bound input to exist on
its descriptor, every choice condition to name a member, and every
choice combination to select a variant. The list starts with the eight
D2/D3 calculations (bolt group, lug, fillet weld, thick cylinder,
bolted-joint preload, bearing life, Miner, material margin); it may not
grow.

## Consequences

- Eleven calculations are drawn (both beams, column, bolt shear, bearing
  at a hole, thermal expansion, shaft, thin-wall vessel, plane wall,
  thermal resistance chain, tolerance stack).
- A row-list input (layers, stages, contributors) is drawn
  representatively — three layers, three stages, two parts — and its
  label says how many rows the form lists (`Layers = 2 rows`).
- A new calculation must either ship a diagram or join the list — the
  test names it either way.
- Showing the diagram on the recorded run and the calc-sheet export (WP
  E) is not done; the spec is in Core so that surface can reuse it.

## Alternatives Considered

**Images per calculation.** Rejected: cannot label live values or
highlight a shape, and drift from the inputs silently.

**Drawing code per calculation in Desktop.** Rejected: untestable
without a window, and no coverage check against the descriptors.

## Related Documents

`docs/reviews/RC PO Feedback Actions (2026-10-01).md` (PO-2);
`src/Tempest.Core/Calculations/Modules/Diagrams/`;
`src/Tempest.Desktop/Views/CalculationDiagramView.cs`.
