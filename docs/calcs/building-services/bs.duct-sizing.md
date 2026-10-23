# B3 — Duct sizing (`bs.duct-sizing@1.0.0`)

**Scope.** Section-by-section sizing by equal friction (target Pa/m) or velocity (target m/s); round, rectangular (fixed side a, b solved) and flat-oval ducts via equivalent diameters; sizes rounded up to a user step; velocity, Re, Colebrook f, friction rate, friction loss, fitting loss (ΣC × velocity pressure), equipment loss; index-run and all-section totals; velocity and pressure-drop limit checks.

**Method.** Dry air ρ = p/(287.055 T), Sutherland viscosity; Darcy–Weisbach Δp/L = f ρ v²/(2D_e) with Colebrook (laminar/transition warned); equal friction solves D for the target rate by bisection; D_e,rect = 1.30 (ab)^0.625/(a + b)^0.25, D_e,oval = 1.55 A^0.625/P^0.25 (ASHRAE Ch. 21 relations, cited); loss coefficients C are user inputs (D-19).

**Inputs.** `method`, `dp_target`, `v_target`, `T`, `p_atm`, `eps`, `sections` (name, Q, L, shape, a, D/b, ΣC, dp_eq, index), `v_limit`, `dp_limit`, `round_to`. **Outputs.** per section `D_s<i>` or `a_s<i>`, `b_s<i>`, `De_s<i>`; `v_s<i>`, `Re_s<i>`, `f_s<i>`, `dpL_s<i>`, `dp_f_s<i>`, `dp_fit_s<i>`, `dp_s<i>` (≤ 50 sections); `dp_index`, `dp_all`, `v_peak`. **Checks.** index-run Δp ≤ limit; velocity ≤ limit.

**Test cases.** 400 mm round at 1 m³/s against the ASHRAE friction-chart point (Colebrook, within 0.5 %); 600 × 300 rectangular equivalent diameter (ASHRAE relation); velocity-method rounding (analytic).
