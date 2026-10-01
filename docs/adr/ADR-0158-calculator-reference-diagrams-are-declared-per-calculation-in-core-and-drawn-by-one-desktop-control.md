# ADR-0158: Calculator Reference Diagrams Are Declared per Calculation in Core and Drawn by One Desktop Control

## Status

Accepted — Product Owner feedback `PO-2` (2026-10-01), work packages A, B,
C, D1, D2 and D3 of the scope recorded in
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
bolt, a ticked yes-or-no as `true` — `ClosedEnds` for the thick
cylinder). `CalculationDiagrams.All` holds the diagrams.

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

**4. A calculation is complete only with its diagram.** The Product
Owner's rule (2026-10-01): "for future calculations the 2D diagram will
be a necessary item to consider it complete" (Engineering Principle 33).
A Core test requires every calculation in
`CalculationModuleDescriptors.All` to have exactly one diagram in
`CalculationDiagrams.All`, every bound input to exist on its descriptor,
every choice condition to name a member, and every choice combination to
select a variant. There is no "no diagram yet" list to join; the view's
"No diagram yet" text survives only as a defensive fallback.

## Consequences

- All nineteen calculations are drawn: eleven simple ones (both beams,
  column, bolt shear, bearing at a hole, thermal expansion, shaft,
  thin-wall vessel, plane wall, thermal resistance chain, tolerance
  stack), the five D2 geometries and the three D3 charts below.
- A row-list input (layers, stages, contributors) is drawn
  representatively — three layers, three stages, two parts — and its
  label says how many rows the form lists (`Layers = 2 rows`).
- A new calculation ships with its diagram or the build is red; the
  test's message says the calculation is not complete without it.
- The D2 geometry is drawn too: the bolt group (six bolts drawn, the
  load at its point from one origin), the fillet weld (plan and
  section), the lifting lug (front and through the pin), the thick
  cylinder (open or closed ends) and the bolted-joint preload (the joint
  and its two springs in parallel). An input the method does not take is
  not drawn as one: no grip length, weld leg, eccentricity or load angle.
- Showing the diagram on the recorded run and the calc-sheet export (WP
  E) is not done; the spec is in Core so that surface can reuse it.

## Amendment: chart-like diagrams (D3)

The bearing life, Miner fatigue and material margin calculations are
drawn with two more shapes: a polyline (solid, or dashed for a guide or
a limit line) and a pair of plain axes with arrowheads and no
graduations. A chart-like diagram is as schematic as any other: the S-N
line, the loading blocks and the stress bars are drawn representatively,
labelled with the values as typed, and never place a result. A label
too long for its box ends in an ellipsis; the screen-reader summary
keeps the full text.

## Alternatives Considered

**Images per calculation.** Rejected: cannot label live values or
highlight a shape, and drift from the inputs silently.

**Drawing code per calculation in Desktop.** Rejected: untestable
without a window, and no coverage check against the descriptors.

## Related Documents

`docs/reviews/RC PO Feedback Actions (2026-10-01).md` (PO-2);
`src/Tempest.Core/Calculations/Modules/Diagrams/`;
`src/Tempest.Desktop/Views/CalculationDiagramView.cs`.
