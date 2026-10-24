# E2 — Enclosure heat dissipation and fan sizing (`elec.enclosure-heat@1.0.1`)

**In plain terms.** Equipment inside a closed enclosure gives off heat, and the enclosure can only lose it through its surfaces, so the air inside warms until the heat out matches the heat in. The worksheet estimates that temperature rise from the effective surface area and a heat-transfer coefficient. If the inside would get too hot, it sizes the airflow a fan must move, corrected for the thinner air at altitude, or the capacity a cooling unit needs.

**Scope.** Heat-load schedule (P × qty × duty) plus optional solar gain; effective surface from per-face exposure factors (D-20); natural temperature rise through an editable overall coefficient h; forced-ventilation airflow at the allowed rise with altitude density; fan duty point against user fan-curve points and a V² system resistance; cooling-unit capacity when the limit cannot be held.

**Method.** A_eff = Σ f_face A_face; ΔT = P/(h A_eff); V = P/(ρ c_p ΔT_allow), ρ = p(z)/(287.055 T_amb); duty by bisection of p_fan(V) − p_sys (V/V_ref)²; P_cool = max(0, P − h A_eff ΔT_allow).

**Inputs.** `loads`, `I_sun`, `A_sun`, `alpha_sun`, `W`, `H`, `D`, `f_top…f_bottom`, `h`, `T_amb`, `T_max`, `altitude`, `V_pts`, `p_pts`, `p_sys`, `V_ref`. **Outputs.** `P_loads`, `P_solar`, `P_total`, `A_eff`, `dT_nat`, `T_int`, `P_nat_max`, `P_cool`, `rho_air`, `V_req`, `V_req_net`, `V_duty`, `p_duty`, `u_fan`. **Checks.** natural dissipation holds T_max; fan delivers V_req (when a curve is given); fan duty point exists.

**Test cases.** Wall-mounted 600 × 800 × 300 at 300 W (analytic); fan duty against a linear curve (analytic).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Wall-mounted PLC panel, natural dissipation and fan ([sheet](../../worked-examples/electrical/elec.enclosure-heat/wall-mounted-plc-panel/sheet.txt)).

```text
P_loads = 2 item(s) = 300 W
P_solar = 0.5 × 0 W/m² × 0 m² = 0 W
P_total = 300 W + 0 W = 300 W
A_eff = 1 × 0.6 m × 0.3 m + 0 × 0.6 m × 0.3 m + 1 × 0.6 m × 0.8 m + 0 × 0.6 m × 0.8 m + 1 × 0.8 m × 0.3 m + 1 × 0.8 m × 0.3 m = 1.14 m²
dT_nat = 300 W / (5.5 W/(m²·K) × 1.14 m²) = 47.847 K
T_int = 35 degC + 47.847 K = 82.847 degC
dT_allow = 50 degC − 35 degC = 15 K
u_nat = 47.847 K / 15 K = 3.1898
P_nat_max = 5.5 W/(m²·K) × 1.14 m² × 15 K = 94.05 W
P_cool = max(0 W, 300 W − 94.05 W) = 205.95 W
p_site = z = 0 m = 101.32 kPa
rho_air = 101.32 kPa / (287.055 J/(kg*K) × 35 degC) = 1.1455 kg/m³
V_req = 300 W / (1.1455 kg/m³ × 1006 J/(kg*K) × 15 K) = 62.481 m³/h
V_req_net = max(0 m^3/h, (300 W − 94.05 W) / (1.1455 kg/m³ × 1006 J/(kg*K) × 15 K)) = 42.893 m³/h
… 3 more step(s) on the sheet
Governing: FAIL  Natural dissipation holds T_max: u_nat = 3.1898 vs u_nat ≤ 1  utilisation 3.19
```
<!-- hand-arithmetic:end -->
