# Beam formulas (single span) (`struct.beam-formulas@1.2.0`)

**In plain terms.** For a single span with standard supports and loads, the reactions, the largest bending moment and the deflection have closed-form answers worked out once in the textbooks. This worksheet applies those formulae directly, which makes it the quickest way to size or check a simple beam and a useful cross-check on a frame model.

## Scope

Calculates reactions, maximum moment and shear, and maximum deflection of a single-span beam for the standard support and load cases. Left end at x = 0; loads downward; sagging moments and downward deflections positive.

## Method

- Formula source: Roark Table 8.1, in this sheet's own notation, for the chosen support and load case.
- Simply supported beam, point load at a (b = L − a), reaction and largest moment: R_A = F b / L; M_max = F a b / L.
- Simply supported beam, point load at a, largest deflection when a ≥ b: δ_max = F b (L² − b²)^(3/2) / (9√3 L EI) at x = √((L² − b²)/3).
- Propped cantilever, uniform load, reaction and moments: R_B = 3wL/8; M_A = −wL²/8; sagging M_max = 9wL²/128 at x = 5L/8.

Roark Table 8.1 in own notation, every step an engine formula (formula → substitution → result). Simply supported point load deflection uses the shorter segment `c` with `δ = P c √((L² − c²)³) / (9√3 L EI)`; a load left of midspan mirrors the beam (warning).

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group | Old symbols |
|---|---|---|---|---|---|---|
| `case` | Case | — | simply supported, UDL | simply supported, UDL, simply supported, point load at a, simply supported, couple at the left end, cantilever, UDL, cantilever, point load at a, fixed both ends, UDL, fixed both ends, point load at midspan, propped cantilever (fixed left, pinned right), UDL | Setup | — |
| `L` | Span | m | 6 | 0.1 to 100 | Geometry | — |
| `I` | Second moment of area | cm⁴ | 8000 | 1 to 100000000 | Geometry | — |
| `y_c` | Extreme-fibre distance from the neutral axis (optional; blank = no stress check) | mm | blank | 1 to 2000 | Geometry | — |
| `E` | Young's modulus | GPa | 210 | 1 to 500 | Material | — |
| `w` | Distributed load, downward (optional; blank = not used by the point-load and couple cases) | kN/m | 10 | 0 to 10000 | Loads | — |
| `F` | Point load, downward (optional; blank = not used by the distributed-load and couple cases) | kN | 50 | 0 to 100000 | Loads | `P` |
| `M_0` | Applied couple (optional; blank = not used by the other cases) | kN·m | 20 | 0 to 1000000 | Loads | — |
| `a` | Load position from the left end (optional; blank = not used by the distributed-load cases) | m | 3 | 0 to 100 | Loads | — |
| `sigma_allow` | Allowable bending stress (optional; blank = no stress check) | MPa | blank | 1 to 2000 | Requirements | — |
| `delta_limit` | Deflection limit (optional; blank = no deflection check) | mm | blank | 0.01 to 1000 | Requirements | — |

## Assumptions

1. The beam is prismatic, linear elastic and Euler–Bernoulli, with small deflections. Basis: Roark Table 8.1 conditions.
2. A cantilever is fixed at the left end (x = 0) and free at the right. Basis: Case convention.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `R_A` | Left reaction | kN |
| `R_B` | Right reaction | kN |
| `M_max` | Maximum sagging moment | kN·m |
| `x_M` | Position of M_max | m |
| `M_min` | Minimum (hogging) moment | kN·m |
| `V_max` | Maximum shear | kN |
| `delta_max` | Maximum deflection | mm |
| `x_delta` | Position of δ_max | m |

## Ports

| Port | Output symbol | Description | Unit | Old symbols |
|---|---|---|---|---|
| `beam.reaction_left` | `R_A` | Left reaction | kN | — |
| `beam.reaction_right` | `R_B` | Right reaction | kN | — |
| `beam.moment_max` | `M_max` | Maximum sagging moment | kN·m | — |
| `beam.moment_min` | `M_min` | Minimum (hogging) moment | kN·m | — |
| `beam.shear_max` | `V_max` | Maximum shear | kN | — |
| `beam.deflection_max` | `delta_max` | Maximum deflection | mm | — |

## Checks

Checks the sheet makes:

- Bending stress against allowable stress
- Shortest span of a beam that is not deep against the span
- Deflection against the deflection limit

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `position` | Block | Conservative | The load position must lie on the span. |
| `deep-beam` | Warning | Unconservative | The span is below ten times the section depth: shear deformation adds to the deflection, which this sheet leaves out, and the check on the span fails. |
| `deflection-position` | Warning | Conservative | For a simply supported point load the deflection formula assumes the load is at or right of midspan, and the beam is mirrored when it is not. |

## Limitations

The beam is prismatic, linear elastic and Euler–Bernoulli, with small deflections. Shear deformation is not included, so a deep beam (span below ten times the section depth) deflects more than the sheet reports: it warns (rule `deep-beam`) and the check on the span fails.

A simply supported point load left of midspan mirrors the beam for the deflection formula (a warning, rule `deflection-position`). A load off the span blocks (rule `position`).

### Changes by version

**1.2.0 (C-2 consistency).** The point load is `F` (the old symbol `P` stays as an alias; a bare `P` means power). The inputs are grouped Setup, Geometry, Material, Loads and Requirements, with the optional hints in the standard form. Every check shows demand and capacity with units (bending stress against allowable stress, deflection against the deflection limit). The deep-beam condition is declared (rule `deep-beam`, previously emitted but not declared) and paired with a new check, "Shortest span of a beam that is not deep against the span", which fails when L/(2·y_c) is below 10 (no shipped case is that deep, so no shipped result changes). The sheet declares six ports. The scope, method labels, assumption and reference follow the sheet standard. No calculated value changes.

## References

- Young and Budynas, Roark's Formulas for Stress and Strain, Table 8.1, 8th ed.: Shear, moment, slope and deflection formulas for elastic straight beams.

## Test cases

SS UDL, SS off-centre point, cantilever tip, fixed UDL — analytic, Confirmed; the four legacy `calc.beam-deflection` hand calculations (`ka-legacy-*`, moment, stress and deflection to five significant figures); the propped-cantilever worked example cross-checks the frame solver.

- `ka-ss-udl`: Simply supported UDL. Source: Roark's Formulas for Stress and Strain, 8th ed..
- `ka-ss-point`: Simply supported point load off-centre. Source: Roark's Formulas for Stress and Strain, 8th ed..
- `ka-cantilever-point`: Cantilever tip load. Source: Roark's Formulas for Stress and Strain, 8th ed..
- `ka-fixed-udl`: Fixed-ended UDL. Source: Roark's Formulas for Stress and Strain, 8th ed..
- `ka-legacy-ss-centre-point`: Legacy example 1: simply supported, central point load. Source: TempestOS calc.beam-deflection (v0.21.0) hand calculation.
- `ka-legacy-cantilever-udl`: Legacy example 2: cantilever, uniformly distributed load (fails both checks). Source: TempestOS calc.beam-deflection (v0.21.0) hand calculation.
- `ka-legacy-ss-udl`: Legacy example 3: simply supported, uniformly distributed load. Source: TempestOS calc.beam-deflection (v0.21.0) hand calculation.
- `ka-legacy-cantilever-point`: Legacy example 4: cantilever end load, stress exactly on the allowable. Source: TempestOS calc.beam-deflection (v0.21.0) hand calculation.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Simply supported beam, off-centre point load ([sheet](../../worked-examples/structural/struct.beam-formulas/ss-point-offcentre/sheet.txt)).

```text
EI = 210 GPa × 8503 cm⁴ = 17856 kN·m²
b = 6 m − 4 m = 2 m
R_A = 50 kN × 2 m / 6 m = 16.667 kN
R_B = 50 kN × 4 m / 6 m = 33.333 kN
M_max = 50 kN × 4 m × 2 m / 6 m = 66.667 kN·m
x_M = 4 m
M_min = 0 kN*m = 0 kN·m
V_max = max(16.667 kN, 33.333 kN) = 33.333 kN
c_far = 2 m
x_delta = sqrt(((6 m)^2 − (2 m)^2) / 3) = 3.266 m
delta_max = 50 kN × 2 m × sqrt(((6 m)^2 − (2 m)^2)^3) / (9 × sqrt(3) × 6 m × 17856 kN·m²) = 10.839 mm
```
<!-- hand-arithmetic:end -->
