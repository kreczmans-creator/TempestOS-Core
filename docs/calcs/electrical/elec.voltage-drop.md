# E3 — Voltage drop and I²R loss (`elec.voltage-drop@1.0.0`)

**Scope.** DC, single-phase and three-phase circuits; conductor data from resistivity + cross-section with temperature correction, from R′/X′ per km, or from a mV/A/m figure; parallel conductors; power factor; drop in V and %; conductor power loss; conductor temperature estimate from I/I_z; user limits (BS 7671 525 / Appendix 4 cited, not embedded — D-21).

**Method.** ρ_T = ρ_20 [1 + α (T_c − 20)]; R = ρ_T L/(A n); X = X′ L/n; ΔV = 2IR (DC), 2I(R cos φ + X sin φ) (1φ), √3 I(R cos φ + X sin φ) (3φ), or (mV/A/m) I L/1000; P_loss = 2I²R or 3I²R; T_c,est = T_amb + (T_rated − T_amb)(I/I_z)².

**Inputs.** `system`, `V_n`, `I`, `L`, `cos_phi`, `n_par`, `data`, `rho_20`, `alpha`, `A_c`, `T_c`, `R_km`, `X_km`, `mVAm`, `dv_limit`, `I_z`, `T_rated`, `T_amb`. **Outputs.** `R`, `X`, `dV`, `dV_pct`, `P_loss`, `T_c_est`. **Checks.** drop ≤ limit; I ≤ I_z (when given); warnings for overload and for an estimated temperature above T_c.

**Test cases.** DC 48 V / 6 mm², three-phase 400 V / 35 mm² with reactance, mV/A/m route — all analytic (Confirmed).
