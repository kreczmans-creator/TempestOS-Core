# M11 — Thin cylindrical shell under internal pressure (`mech.thin-vessel@1.0.1`)

**In plain terms.** Pressure inside a cylinder pulls its wall apart in two directions: around the circumference and along the length. The circumferential pull is twice the longitudinal one, so it usually sets the wall thickness. The code formula adds a small correction for the wall's own thickness and divides by the weld joint efficiency, because a welded seam may be weaker than the plate; a corrosion allowance is added on top so the shell is still thick enough at the end of its life.

**Scope.** Cylindrical shell away from heads and openings, internal pressure. Ported from the legacy `calc.pressure-vessel-wall-thickness` (ADR-0167), keeping its design factor. Clause numbers only (ASME VIII-1 UG-27(c)(1), (c)(2)). Thick walls are `mech.thick-cylinder`.

**Method.** `t_c = PR/(SE − 0.6P)`; `t_l = PR/(2SE + 0.4P)`; `t_req = n·max(t_c, t_l) + c`; `σ_θ = P(R + 0.6t)/(Et)` at `t = t_p − c`.

**Inputs.** `P`, `R`, `S`, `E_j`, `n`, `c`, `t_p`. **Outputs.** `t_c`, `t_l`, `t_req`, `sigma_h`, `u`. **Checks.** provided against required thickness.

**Limitations.** P > 0.385·S·E or t_c > R/2 blocks; no external pressure, heads, nozzles or supports.

**Test cases.** Two UG-27 hand calculations (`ka-ug27-hand` passes, `ka-ug27-thin-plate` fails); the legacy module had property tests only.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Air receiver shell, 2 MPa ([sheet](../../worked-examples/mechanical/mech.thin-vessel/receiver-2MPa/sheet.txt)).

```text
t_c = 2 MPa × 500 mm / (138 MPa × 0.85 − 0.6 × 2 MPa) = 8.6133 mm
t_l = 2 MPa × 500 mm / (2 × 138 MPa × 0.85 + 0.4 × 2 MPa) = 4.2481 mm
t_req = 1 × max(8.6133 mm, 4.2481 mm) + 3 mm = 11.613 mm
t_corr = 12 mm − 3 mm = 9 mm
sigma_h = 2 MPa × (500 mm + 0.6 × 9 mm) / (0.85 × 9 mm) = 132.13 MPa
u = 11.613 mm / 12 mm = 0.96777
Governing: PASS  Provided thickness against required: u = 0.96777 vs u ≤ 1  utilisation 0.968  « ASME VIII-1 UG-27
```
<!-- hand-arithmetic:end -->
