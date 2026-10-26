# M3a — Bolted joint, simplified VDI 2230 (`mech.bolted-joint@1.2.0`)

**In plain terms.** Tightening a bolt stretches it and squeezes the parts it clamps, like a soft spring pulling against a stiffer one. When a working load then pulls the joint apart, most of it relaxes the squeeze on the parts and only a fraction, set by the two stiffnesses, adds to the bolt. The joint is checked five ways: the parts must not separate, enough clamp must remain, the bolt must not yield under tension plus thread torsion, the alternating stress must stay below the endurance limit, and the thread must not strip. Torque gives preload only roughly, which is why the scatter factor is applied.

**Scope.** One concentrically clamped and loaded bolt: preload from torque (nut factor or friction) with scatter, bolt and member stiffness (two 30° frusta), load factor, embedding loss, residual clamp and separation, yield under preload + working load (50 % thread torsion), fatigue amplitude against a user endurance limit, thread engagement against stripping. Clause numbers only (VDI 2230 Part 1 R0–R13, ISO 898-1 §9.1.6, Shigley §8-5/§8-7).

**Method.** `A_s = π/4·((d_2 + d_3)/2)²`. Friction: `F_M = M_A/(0.16P + 0.58 d_2 μ_G + 0.5 D_Km μ_K)`; nut factor: `F_M = M_A/(K d)`. `F_M,min = F_M/α_A`. `δ_S = 0.8d/(E A_N) + l_k/(E A_s) + 0.5d/(E A_d3)`. `k_f = 0.5774πE_P d / ln[((1.155t + d_w − d_h)(d_w + d_h))/((1.155t + d_w + d_h)(d_w − d_h))]`, `k_P = k_f/2`, `Φ = n k_S/(k_S + k_P)`. `F_Z = f_Z k_S k_P/(k_S + k_P)`. Given preload: `F_M = F_V`, with `k_S`, `k_P` and `A_s` optionally given (measured, FE, a fastener record). `F_KR = max(F_M,min − (1 − Φ)F_A − F_Z, 0)`. `F_S,max = max(F_M + Φ F_A, F_A)`: once the joint opens the bolt carries the whole working load. Yield load factor `n_L = min((F_proof − F_M)/Φ, F_proof)/F_A` (separation included, D-42). `σ_red = √(σ_z² + 3(0.5τ_M)²)`. `σ_a = (F_S,max − F_M)/(2A_s)`. `m_min = S F_S,max/(0.5π d τ_B)`.

**Inputs.** Bolt `d`, `P`, `R_p02`, `E_S`, `sigma_A`; joint `l_k`, `d_h`, `d_w`, `E_P`, `n`, `f_Z`; tightening `method` (nut factor, friction, given preload), `M_A`, `K`, `mu_G`, `mu_K`, `nu_perm` (1.2.0: the permitted assembly utilisation ν, default 1.0, the limit at which the bolt yields in tightening; 0.9 is the usual VDI 2230 target), `alpha_A`, `F_V`; given properties `A_s_given` (fill from a released fastener record), `k_S_given`, `k_P_given`; loads `F_A`, `F_KR_req`; thread `tau_B`, `m_eff`, `S_strip`.

**Outputs.** `A_s`, `F_M`, `F_M_min`, `sigma_M`, `nu_M`, `u_assembly` (`nu_M/nu_perm`), `k_S`, `k_P`, `Phi`, `F_Z`, `F_SA`, `F_KR`, `F_A_sep` (working load at separation), `F_S_max`, `sigma_red_B`, `sigma_a`, `m_min`; with a working load, the joint-diagram load factors `n_0 = F_A,sep/F_A` (against separation) and `n_L = (R_p02·A_s − F_M)/F_SA` (against axial yield, torsion excluded), carried from the legacy `calc.bolted-joint-preload` (ADR-0167). **Checks.** equivalent stress at assembly within the permitted ν of the proof strength (1.2.0: M12 8.8 friction method at 95 N·m reads `nu_M` 1.150, σ_red,M 736 MPa against R_p0.2 640 MPa, and all five checks passed before; the warning `preload-yield` above 90 % stays and says when the bolt yields in tightening); no separation; residual clamp vs required; yield; fatigue; stripping.

**Limitations.** Concentric only; frusta with the hole diameter inside; torsion kept at 50 % in service; stripping by a half-pitch shear area; no thermal or transverse-slip checks.

**Test cases.** `ka-m12-88-nut-factor` (A_s 84.27 mm², F_M 38.75 kN — analytic, Confirmed); `ka-shigley-stiffness` (k_P 1.908e6 kN/m, Φ 0.0720, F_M 40.69 kN — re-derived from Shigley eqs 8-17/8-20, Confirmed); the legacy module's three vectors `ka-legacy-given-1`, `-2` and `-given-separates` (given preload and stiffnesses; vector 2's yield factor corrected to 3.2578 because the joint separates first, D-42).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** M12 class 8.8 flange bolt, 80 N·m, 10 kN working load ([sheet](../../worked-examples/mechanical/mech.bolted-joint/m12-88-flange/sheet.txt)).

```text
d_2 = 12 mm − 0.6495 × 1.75 mm = 10.863 mm
d_3 = 12 mm − 1.2269 × 1.75 mm = 9.8529 mm
A_s = π / 4 × ((10.863 mm + 9.8529 mm) / 2)^2 = 84.266 mm²
A_N = π / 4 × (12 mm)^2 = 113.1 mm²
A_d3 = π / 4 × (9.8529 mm)^2 = 76.247 mm²
D_Km = (18 mm + 13 mm) / 2 = 15.5 mm
F_M = 80 N·m / (0.16 × 1.75 mm + 0.58 × 10.863 mm × 0.12 + 0.5 × 15.5 mm × 0.12) = 40.69 kN
F_M_min = 40.69 kN / 1.6 = 25.431 kN
sigma_M = 40.69 kN / 84.266 mm² = 482.87 MPa
M_G = 40.69 kN × (0.16 × 1.75 mm + 0.58 × 10.863 mm × 0.12) = 42.158 N·m
W_p = π / 16 × (9.8529 mm)^3 = 187.81 mm³
tau_M = 42.158 N·m / 187.81 mm³ = 224.47 MPa
sigma_red_M = sqrt((482.87 MPa)^2 + 3 × (224.47 MPa)^2) = 619.94 MPa
nu_M = 619.94 MPa / 640 MPa = 0.96866
… 25 more step(s) on the sheet
Governing: PASS  Equivalent stress at assembly within the permitted ν of the proof strength: u_assembly = 0.96866 vs u_assembly ≤ 1  utilisation 0.969  « VDI 2230 R7
```
<!-- hand-arithmetic:end -->
