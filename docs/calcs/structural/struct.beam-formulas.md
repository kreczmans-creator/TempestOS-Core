# S2b — Beam formulas, single span (`struct.beam-formulas@1.0.0`)

**Scope.** Closed-form quick checks: reactions, maximum and minimum moment with position, maximum shear, maximum deflection with position for eight standard cases (simply supported UDL / point at `a` / end couple; cantilever UDL / point at `a`; fixed-ended UDL / central point; propped cantilever UDL).

**Method.** Roark Table 8.1 in own notation, every step an engine formula (formula → substitution → result). Simply supported point load deflection uses the shorter segment `c` with `δ = P c √((L² − c²)³) / (9√3 L EI)`; a load left of midspan mirrors the beam (warning).

**Inputs.** `case`, `L`, `E`, `I`, and `w` / `P` / `M_0` / `a` as the case needs. **Outputs.** `R_A`, `R_B`, `M_max`, `x_M`, `M_min`, `V_max`, `delta_max`, `x_delta`.

**Test cases.** SS UDL, SS off-centre point, cantilever tip, fixed UDL — analytic, Confirmed; the propped-cantilever worked example cross-checks the frame solver.
