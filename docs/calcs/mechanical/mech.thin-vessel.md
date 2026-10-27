# Thin cylindrical shell under internal pressure (`mech.thin-vessel@1.1.0`)

**In plain terms.** Pressure inside a cylinder pulls its wall apart in two directions: around the circumference and along the length. The circumferential pull is twice the longitudinal one, so it usually sets the wall thickness. The code formula adds a small correction for the wall's own thickness and divides by the weld joint efficiency, because a welded seam may be weaker than the plate; a corrosion allowance is added on top so the shell is still thick enough at the end of its life.

## Scope

Calculates the minimum thickness of a thin cylindrical shell under internal pressure, for circumferential and longitudinal stress. It adds corrosion allowance and a design factor, and compares the thickness provided with the requirement.

The sheet is ported from the legacy `calc.pressure-vessel-wall-thickness` module (ADR-0167) and keeps its design factor. Thick walls are `mech.thick-cylinder`.

## Method

- Thickness needed for the circumferential stress: `t_c = p_design·R_in/(S·E_j − 0.6·p_design)`.
- Thickness needed for the longitudinal stress: `t_l = p_design·R_in/(2·S·E_j + 0.4·p_design)`.
- Required thickness, with the design factor and corrosion allowance: `t_req = n_design·max(t_c, t_l) + c`.
- Hoop stress in the plate provided, after corrosion: `σ_θ = p_design·(R_in + 0.6·t)/(E_j·t)`, at `t = t_p − c`.
- Range of validity of the thin-wall formulae: `t_c ≤ R_in/2` and `p_design ≤ 0.385·S·E_j`.

## Inputs

| Symbol | Meaning | Unit | Group |
|---|---|---|---|
| `R_in` | Inside radius (corroded) | mm | Geometry |
| `t_p` | Shell thickness provided (nominal, before corrosion) | mm | Geometry |
| `S` | Allowable stress at design temperature | MPa | Material |
| `p_design` | Internal design pressure | MPa | Loads |
| `c` | Corrosion allowance | mm | Conditions |
| `E_j` | Joint efficiency | none | Factors |
| `n_design` | Design factor on the computed thickness (legacy; 1 for none) | none | Factors |

Three symbols were renamed in 1.1.0 and the old ones still load: `P` is now `p_design`, `R` is now `R_in` and `n` is now `n_design`. A saved calculation, an inputs file, a CSV file and a pack binding written with the old symbols give the same values.

## Assumptions

- The shell is cylindrical, away from heads, nozzles and supports, and carries membrane stresses only.
- The joint efficiency reduces the allowable stress uniformly.

## Outputs

`t_c`, `t_l`, `t_req`, `sigma_h` and `u` (the thickness utilisation).

## Ports

| Port | Symbol | Unit | Meaning |
|---|---|---|---|
| `vessel.thickness_required` | `t_req` | mm | Required nominal thickness, with design factor and corrosion allowance |
| `vessel.stress_hoop` | `sigma_h` | MPa | Hoop stress at the corroded thickness provided |

## Checks

- Provided thickness against required: the required thickness `t_req` against the thickness provided `t_p`.
- Rules `thin-wall` and `corroded-through` (blocks).

## Limitations

A pressure above `0.385·S·E_j`, or a required thickness above half the radius, blocks the sheet. The sheet has no external pressure, heads, nozzles or supports.

## References

- ASME BPVC Section VIII Division 1, cl. UG-27(c)(1): thickness of shells under internal pressure, circumferential stress.
- ASME BPVC Section VIII Division 1, cl. UG-27(c)(2): thickness of shells under internal pressure, longitudinal stress.

## Test cases

Two UG-27 hand calculations: `ka-ug27-hand` passes and `ka-ug27-thin-plate` fails. The legacy module had property tests only.

**Changes in 1.1.0 (C-2).** `P`, `R` and `n` are renamed `p_design`, `R_in` and `n_design` (the old symbols are aliases); the inputs are in groups; the check shows the thicknesses; two ports are declared; messages are rewritten. No result changes.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Air receiver shell, 2 MPa ([sheet](../../worked-examples/mechanical/mech.thin-vessel/receiver-2MPa/sheet.txt)).

```text
t_c = 2 MPa × 500 mm / (138 MPa × 0.85 − 0.6 × 2 MPa) = 8.6133 mm
t_l = 2 MPa × 500 mm / (2 × 138 MPa × 0.85 + 0.4 × 2 MPa) = 4.2481 mm
t_req = 1 × max(8.6133 mm, 4.2481 mm) + 3 mm = 11.613 mm
t_corr = 12 mm − 3 mm = 9 mm
sigma_h = 2 MPa × (500 mm + 0.6 × 9 mm) / (0.85 × 9 mm) = 132.13 MPa
u = 11.613 mm / 12 mm = 0.96777
Governing: PASS  Provided thickness against required: 11.61 mm vs 12 mm  utilisation 0.97  « ASME VIII-1 UG-27
```
<!-- hand-arithmetic:end -->
