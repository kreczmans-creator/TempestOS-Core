# B3 — Duct sizing (`bs.duct-sizing@1.0.1`)

**In plain terms.** Ducts are sized so that air does not lose too much pressure per metre or move fast enough to be noisy. The equal-friction method picks each size to give the same pressure loss per metre; the velocity method picks it to give a target speed. A rectangular duct is compared with the round duct that has the same friction, and the losses along the longest run, straight ducts plus fittings, give the pressure the fan must supply.

**Scope.** Section-by-section sizing by equal friction (target Pa/m) or velocity (target m/s); round, rectangular (fixed side a, b solved) and flat-oval ducts via equivalent diameters; sizes rounded up to a user step; velocity, Re, Colebrook f, friction rate, friction loss, fitting loss (ΣC × velocity pressure), equipment loss; index-run and all-section totals; velocity and pressure-drop limit checks.

**Method.** Dry air ρ = p/(287.055 T), Sutherland viscosity; Darcy–Weisbach Δp/L = f ρ v²/(2D_e) with Colebrook (laminar/transition warned); equal friction solves D for the target rate by bisection; D_e,rect = 1.30 (ab)^0.625/(a + b)^0.25, D_e,oval = 1.55 A^0.625/P^0.25 (ASHRAE Ch. 21 relations, cited); loss coefficients C are user inputs (D-19).

**Inputs.** `method`, `dp_target`, `v_target`, `T`, `p_atm`, `eps`, `sections` (name, Q, L, shape, a, D/b, ΣC, dp_eq, index), `v_limit`, `dp_limit`, `round_to`. **Outputs.** per section `D_s<i>` or `a_s<i>`, `b_s<i>`, `De_s<i>`; `v_s<i>`, `Re_s<i>`, `f_s<i>`, `dpL_s<i>`, `dp_f_s<i>`, `dp_fit_s<i>`, `dp_s<i>` (≤ 50 sections); `dp_index`, `dp_all`, `v_peak`. **Checks.** index-run Δp ≤ limit; velocity ≤ limit.

**Test cases.** 400 mm round at 1 m³/s against the ASHRAE friction-chart point (Colebrook, within 0.5 %); 600 × 300 rectangular equivalent diameter (ASHRAE relation); velocity-method rounding (analytic).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Office supply ductwork by equal friction at 1 Pa/m ([sheet](../../worked-examples/building-services/bs.duct-sizing/office-supply-equal-friction/sheet.txt)).

```text
rho = 101320 Pa / (287.055 × 293.15 K) = 1.2041 kg/m³
mu = T = 293.15 K = 18.133 uPa·s
a_s1 = as given = 800 mm
b_s1 = from the target equivalent diameter 573.91 mm = 400 mm
De_s1 = 1.30 (ab)^0.625/(a + b)^0.25 = 609.35 mm
v_s1 = 2 / 0.32 = 6.25 m/s
Re_s1 = 1.2041 × 6.8581 × 0.60935 / 0.000018133 = 277500
f_s1 = Colebrook–White = 0.015999
dpL_s1 = 0.015999 × 1.2041 × 6.8581² / (2 × 0.60935) = 0.74347 Pa/m
dp_f_s1 = 0.74347 × 15 = 11.152 Pa
dp_fit_s1 = 0.8 × 23.518 = 18.814 Pa
dp_s1 = 11.152 + 18.814 + 0 = 29.966 Pa
D_s2 = target 573.91 mm rounded up to 50 mm = 600 mm
v_s2 = 2 / 0.28274 = 7.0736 m/s
… 37 more step(s) on the sheet
Governing: PASS  Velocity ≤ limit: u_v = 0.88419 vs u_v ≤ 1  utilisation 0.884
```
<!-- hand-arithmetic:end -->
