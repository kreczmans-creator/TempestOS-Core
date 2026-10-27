# Column buckling (Perry–Robertson) (`struct.column-buckling@1.1.0`)

**In plain terms.** A slender strut fails not by crushing but by bowing sideways at Euler's critical load, which falls with the square of its slenderness (length over radius of gyration). A real strut is never perfectly straight, so it bows a little from the start and reaches yield on its concave face before the Euler load. Perry's formula puts that initial bow into the yield condition and Robertson sized the bow to fit tests, giving the strut curves: a stocky strut reaches its design strength, a slender one tends to Euler's value, and the curve bridges the two. The strut curve, chosen by section shape, sets the Robertson constant.

## Scope

Calculates the compressive strength and resistance of a pin-ended strut by the Perry–Robertson formula, and its utilisation against an applied axial load. The Robertson constant comes from the strut curve.

## Method

- Radius of gyration and slenderness: r = √(I/A); λ = L_E/r.
- Euler buckling stress and load: p_E = π²E/λ²; F_E = p_E·A.
- Limiting slenderness, below which the strut is not reduced for buckling: λ_0 = 0.2·√(π²E/p_y).
- Perry factor (Robertson constant a = 2.0, 3.5, 5.5, 8.0 for curves a–d): η = max(0.001·a·(λ − λ_0), 0).
- Compressive strength by the Perry–Robertson formula: φ = (p_y + (η + 1)·p_E)/2; p_c = p_E·p_y/(φ + √(φ² − p_E·p_y)).
- Compression resistance and utilisation: F_c_Rd = p_c·A; u = F/F_c_Rd.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group | Old symbols |
|---|---|---|---|---|---|---|
| `curve` | Strut curve (selects the Robertson constant) | — | c | a, b, c, d | Setup | — |
| `L_E` | Effective length | m | 3 | 0.1 to 50 | Geometry | — |
| `A` | Cross-section area | mm² | 2000 | 10 to 1000000 | Geometry | — |
| `I` | Second moment of area about the weaker axis | cm⁴ | 200 | 0.01 to 10000000 | Geometry | — |
| `E` | Young's modulus | GPa | 205 | 50 to 450 | Material | — |
| `p_y` | Design strength (yield) | MPa | 275 | 100 to 1000 | Material | — |
| `F` | Applied axial load | kN | 150 | 0 to 100000 | Loads | `P` |

## Assumptions

1. The strut is pin-ended and prismatic, under axial load only. Basis: Effective length carries the end restraint.
2. Buckling is about the axis of the given second moment. Basis: The user gives the weaker axis.
3. Initial imperfection represented by the Perry factor η = 0.001·a·(λ − λ_0), with a from the strut curve. Basis: Robertson's fit to strut tests.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `r` | Radius of gyration | mm |
| `lambda` | Slenderness | — |
| `p_E` | Euler stress | MPa |
| `F_E` | Euler critical load | kN |
| `lambda_0` | Limiting slenderness | — |
| `eta` | Perry factor | — |
| `p_c` | Compressive strength | MPa |
| `F_c_Rd` | Compression resistance | kN |
| `u` | Utilisation | — |

## Ports

| Port | Output symbol | Description | Unit | Old symbols |
|---|---|---|---|---|
| `column.resistance_compression` | `F_c_Rd` | Compression resistance | kN | `P_c` |
| `column.load_euler` | `F_E` | Euler critical load | kN | `P_E` |
| `column.strength_compressive` | `p_c` | Compressive strength | MPa | — |
| `column.slenderness` | `lambda` | Slenderness | — | — |
| `column.utilisation` | `u` | Utilisation | — | — |

## Checks

Checks the sheet makes:

- Axial load against compression resistance

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `curve` | Block | Conservative | The strut curve must be a, b, c or d. |
| `slenderness` | Block | Conservative | Slenderness above 180 is outside the method (members resisting loads other than wind). |

## Limitations

Flexural buckling only (no torsional or flexural–torsional modes); no local buckling (class 4) reduction; slenderness above 180 blocks (4.7.3.2). For EN 1993-1-1 design use `struct.en1993-member`.

### Changes by version

**1.1.0 (C-2 consistency).** The axial load is `F` (old symbol `P`, kept as an alias), the Euler load is `F_E` (old `P_E`) and the compression resistance is `F_c_Rd` (old `P_c`), so that no two symbols differ only by case (`p_E` and `P_E`, `p_c` and `P_c`) and a bare `P` is not a force. The old output symbols stay as aliases of the ports `column.load_euler` and `column.resistance_compression`; the sheet declares five ports. The inputs are grouped Setup, Geometry, Material and Loads. The check shows the load and the resistance with units. The rule `curve` (an unknown strut curve blocks) is now declared. The scope, assumptions and clause locators follow the sheet standard. No calculated value changes.

## References

- BS 5950-1:2000, Annex C: Compressive strength (Perry strut formula).
- BS 5950-1:2000, cl. 4.7.3.2: Maximum slenderness.
- BS 5950-1:2000, Table 23: Allocation of strut curve.

## Test cases

The legacy module's three hand calculations (`ka-legacy-curve-c`, `ka-legacy-slender-b`, `ka-legacy-stocky-c`), every intermediate to five significant figures.

- `ka-legacy-curve-c`: Legacy example 1: curve c, intermediate slenderness. Source: TempestOS calc.column-buckling (v0.21.0) hand calculation.
- `ka-legacy-slender-b`: Legacy example 2: slender, curve b, S355 (fails). Source: TempestOS calc.column-buckling (v0.21.0) hand calculation.
- `ka-legacy-stocky-c`: Legacy example 3: stocky, curve c. Source: TempestOS calc.column-buckling (v0.21.0) hand calculation.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Pin-ended column, curve c, intermediate slenderness ([sheet](../../worked-examples/structural/struct.column-buckling/rhs-column-curve-c/sheet.txt)).

```text
a_R = 5.5
r = sqrt(200 cm⁴ / 2000 mm²) = 31.623 mm
lambda = 3 m / 31.623 mm = 94.868
p_E = π^2 × 205 GPa / 94.868^2 = 224.81 MPa
F_E = 224.81 MPa × 2000 mm² = 449.62 kN
lambda_0 = 0.2 × sqrt(π^2 × 205 GPa / 275 MPa) = 17.155
eta = max(0.001 × 5.5 × (94.868 − 17.155), 0) = 0.42742
phi = (275 MPa + (0.42742 + 1) × 224.81 MPa) / 2 = 297.95 MPa
p_c = 224.81 MPa × 275 MPa / (297.95 MPa + sqrt((297.95 MPa)^2 − 224.81 MPa × 275 MPa)) = 133.78 MPa
F_c_Rd = 133.78 MPa × 2000 mm² = 267.56 kN
u = 150 kN / 267.56 kN = 0.56062
Governing: PASS  Axial load against compression resistance: 150 kN vs 267.56 kN  utilisation 0.56  « BS 5950-1 Annex C
```
<!-- hand-arithmetic:end -->
