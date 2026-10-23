# M11 — Thin cylindrical shell under internal pressure (`mech.thin-vessel@1.0.0`)

**In plain terms.** Pressure inside a cylinder pulls its wall apart in two directions: around the circumference and along the length. The circumferential pull is twice the longitudinal one, so it usually sets the wall thickness. The code formula adds a small correction for the wall's own thickness and divides by the weld joint efficiency, because a welded seam may be weaker than the plate; a corrosion allowance is added on top so the shell is still thick enough at the end of its life.

**Scope.** Cylindrical shell away from heads and openings, internal pressure. Ported from the legacy `calc.pressure-vessel-wall-thickness` (ADR-0167), keeping its design factor. Clause numbers only (ASME VIII-1 UG-27(c)(1), (c)(2)). Thick walls are `mech.thick-cylinder`.

**Method.** `t_c = PR/(SE − 0.6P)`; `t_l = PR/(2SE + 0.4P)`; `t_req = n·max(t_c, t_l) + c`; `σ_θ = P(R + 0.6t)/(Et)` at `t = t_p − c`.

**Inputs.** `P`, `R`, `S`, `E_j`, `n`, `c`, `t_p`. **Outputs.** `t_c`, `t_l`, `t_req`, `sigma_h`, `u`. **Checks.** provided against required thickness.

**Limitations.** P > 0.385·S·E or t_c > R/2 blocks; no external pressure, heads, nozzles or supports.

**Test cases.** A UG-27 hand calculation (`ka-ug27-hand`); the legacy module had property tests only.
