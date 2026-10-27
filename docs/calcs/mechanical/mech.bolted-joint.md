# Bolted joint (simplified VDI 2230) (`mech.bolted-joint@1.3.0`)

**In plain terms.** Tightening a bolt stretches it and squeezes the parts it clamps, like a soft spring pulling against a stiffer one. When a working load then pulls the joint apart, most of it relaxes the squeeze on the parts and only a fraction, set by the two stiffnesses, adds to the bolt. The joint is checked five ways: the parts must not separate, enough clamp must remain, the bolt must not yield under tension plus thread torsion, the alternating stress must stay below the endurance limit, and the thread must not strip. Torque gives preload only roughly, which is why the scatter factor is applied.

## Scope

Calculates the preload from the tightening torque, the load factor, the residual clamp, separation, yield, thread stripping and fatigue amplitude of one bolt clamping members under an axial working load. It uses a simplified VDI 2230 approach.

## Method

- Tensile stress area: A_s = (π/4)·((d_2 + d_3)/2)², with d_2 = d − 0.6495 p_thread and d_3 = d − 1.2269 p_thread
- Assembly preload: F_M = M_A/(K d) by the nut factor; F_M = M_A/(0.16 p_thread + 0.58 d_2 μ_G + 0.5 D_Km μ_K) by thread and head friction; F_M = F_V when measured; F_M,min = F_M/α_A after the scatter
- Thread torsion under preload: τ_M = M_G/W_p with M_G = F_M (0.16 p_thread + 0.58 d_2 μ_G) and W_p = π/16·d_3³ at the minor diameter, which is conservative against the VDI 2230 mean diameter d_S
- Bolt resilience, the head, free shank and engaged thread as springs in series: δ_S = (0.4 d + 0.4 d)/(E_S A_N) + l_k/(E_S A_s) + 0.5 d/(E_S A_d3)
- Member stiffness, the clamped parts as two pressure frusta: k = 0.5774 π E d / ln[((1.155 t + d_w − d_h)(d_w + d_h))/((1.155 t + d_w + d_h)(d_w − d_h))]
- Load factor and embedding: Φ = n_load k_S/(k_S + k_P); F_Z = f_embed (k_S k_P)/(k_S + k_P)
- Working state: F_KR = max(F_M,min − (1 − Φ) F_A − F_Z, 0); F_S,max = max(F_M + Φ F_A, F_A); σ_red = √(σ_z² + 3 (0.5 τ)²); σ_a = (F_S,max − F_M)/(2 A_s)
- Thread engagement needed against stripping: m_min = S F_S,max/(0.5 π d τ_B)

The member stiffness is `k_P = k_f/2` with `k_f` from the frusta formula. Given preload: `F_M = F_V`, with `k_S`, `k_P` and `A_s` optionally given (measured, FE, a fastener record). The yield load factor is `n_L = min((F_proof − F_M)/Φ, F_proof)/F_A` (separation included, D-42). Once the joint opens, the bolt carries the whole working load. The factors `n_0 = F_A,sep/F_A` and `n_L` come from the legacy `calc.bolted-joint-preload` (ADR-0167). The permitted assembly utilisation `nu_perm` is the equivalent stress at assembly over the proof strength. Its default 1.0 is the limit at which the bolt yields in tightening, and 0.9 is the usual VDI 2230 target. The warning `preload-yield` above 90 % stays.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `method` | Preload method |  | friction |  | Setup |
| `d` | Nominal thread diameter | mm | 12 | 3 to 64 | Bolt |
| `p_thread` (was `P`) | Thread pitch | mm | 1.75 | 0.35 to 6 | Bolt |
| `R_p02` | Bolt 0.2 % proof (yield) strength | MPa | 640 | 200 to 1200 | Bolt |
| `E_S` | Bolt Young's modulus | GPa | 205 | 60 to 250 | Bolt |
| `sigma_a_limit` (was `sigma_A`) | Bolt endurance limit (stress amplitude) | MPa | 50 | 10 to 200 | Bolt |
| `l_k` | Clamp length | mm | 40 | 1 to 2000 | Joint |
| `d_h` | Hole diameter | mm | 13 | 1 to 100 | Joint |
| `d_w` | Head / washer bearing diameter | mm | 18 | 2 to 200 | Joint |
| `E_P` | Clamped members' Young's modulus | GPa | 205 | 5 to 250 | Joint |
| `n_load` (was `n`) | Load introduction factor (0 = at the clamped faces, 1 = under the head) |  | 0.5 | 0 to 1 | Joint |
| `f_embed` (was `f_Z`) | Embedding (settling) loss | µm | 11 | 0 to 100 | Joint |
| `M_A` | Tightening torque | kN·m | 0.08 | 0.0001 to 10 | Tightening |
| `K` | Nut factor (nut-factor method); F_M is the maximum preload, so enter the smallest value the joint can have |  | 0.2 | 0.1 to 0.35 | Tightening |
| `mu_G` | Thread friction coefficient; F_M is the maximum preload, so enter the smallest value the joint can have |  | 0.12 | 0.06 to 0.25 | Tightening |
| `mu_K` | Head / nut bearing friction coefficient; F_M is the maximum preload, so enter the smallest value the joint can have |  | 0.12 | 0.06 to 0.25 | Tightening |
| `nu_perm` | Permitted assembly utilisation ν: the equivalent stress at assembly over the proof strength (VDI 2230 R7; 0.9 is the usual target, 1.0 is the limit at which the bolt yields in tightening) |  | 1 | 0.5 to 1 | Tightening |
| `alpha_A` | Tightening scatter factor (F_M,max / F_M,min) |  | 1.6 | 1 to 4 | Tightening |
| `F_V` | Preload (optional; blank = the preload comes from the tightening torque; the given-preload method needs it) | kN | optional | 0.1 to 5000 | Tightening |
| `F_A` | Axial working load (per bolt) | kN | 10 | 0 to 5000 | Loads |
| `F_KR_req` | Required residual clamp force | kN | 5 | 0 to 5000 | Loads |
| `tau_B` | Nut / internal-thread material shear strength | MPa | 400 | 50 to 1200 | Thread |
| `m_eff` | Thread engagement length provided | mm | 12 | 1 to 200 | Thread |
| `S_strip` | Safety factor on stripping |  | 1.25 | 1 to 3 | Thread |
| `A_s_given` | Tensile stress area, from a fastener record (optional; blank = from d and p_thread) | mm² | optional | 1 to 5000 | Given properties |
| `k_S_given` | Bolt stiffness, measured or from an FE model (optional; blank = from the geometry) | kN/m | optional | 1000 to 100000000 | Given properties |
| `k_P_given` | Clamped members' stiffness, measured or from an FE model (optional; blank = two frusta) | kN/m | optional | 1000 to 1000000000 | Given properties |

## Assumptions

1. The joint is concentrically clamped and loaded, and the members behave as two 30° frusta in series. Basis: Eccentric clamping and loading (VDI 2230 full procedure) are outside this sheet.
2. Thread torsion is taken at 50 % in the equivalent stress for the working state. Basis: Common VDI 2230 practice (k_τ = 0.5).
3. The thread torsion modulus is taken at the minor diameter d_3, which is conservative. Basis: With the VDI 2230 mean diameter the torsion stress is about 14 % lower and ν_M about 5 % lower at the defaults.
4. F_M is the maximum preload, from the smallest friction and nut factor, and F_M,min is the minimum after scatter. Basis: VDI 2230 R2: the maximum preload comes from the minimum friction coefficients.
5. The working load pulsates from 0 to F_A, so the fatigue amplitude is Φ F_A / (2 A_s). Basis: Compare it with the bolt's endurance limit as entered.
6. Thread stripping uses a shear area of half the pitch per unit length on the nominal diameter. Basis: A conservative simplification of ISO 898-2 / VDI 2230 §5.5.5.

## Outputs

| Symbol | Description |
|---|---|
| `n_0` | Load factor against separation |
| `n_L` | Load factor against axial yield |
| `A_s` | Tensile stress area |
| `F_M` | Assembly preload (maximum) |
| `F_M_min` | Minimum preload |
| `sigma_M` | Preload stress |
| `nu_M` | Assembly utilisation of proof strength |
| `u_assembly` | Assembly utilisation over the permitted ν |
| `k_S` | Bolt stiffness |
| `k_P` | Members' stiffness |
| `Phi_K` | Load factor |
| `F_Z` | Embedding loss |
| `F_SA` | Additional bolt load |
| `F_KR` | Residual clamp |
| `F_A_sep` | Separation load |
| `F_S_max` | Maximum bolt load |
| `sigma_z_max` | Maximum bolt tensile stress |
| `sigma_red_B` | Working equivalent stress |
| `sigma_a` | Fatigue amplitude |
| `m_min` | Engagement needed |

The load factor `Φ` is now `Phi_K`, and the port `joint.factor_load` keeps `Phi` as an alias. The other outputs are `A_s`, `F_M`, `F_M_min`, `sigma_M`, `nu_M`, `u_assembly` (`nu_M/nu_perm`), `k_S`, `k_P`, `F_Z`, `F_SA`, `F_KR`, `F_A_sep` (the working load at separation), `F_S_max`, `sigma_red_B`, `sigma_a`, `m_min`, and the joint-diagram load factors `n_0` and `n_L`.

## Ports

A port keeps its output symbol; a link, a pack binding or an inputs file written with the symbol or an alias still resolves.

| Public name | Symbol | Unit | Description |
|---|---|---|---|
| `joint.preload_max` | `F_M` | kN | Assembly preload, the maximum after tightening |
| `joint.clamp_residual` | `F_KR` | kN | Residual clamp force after embedding and the working load |
| `joint.force_bolt_max` | `F_S_max` | kN | Maximum bolt load in the working state |
| `joint.factor_load` | `Phi_K` (also `Phi`) |  | Load factor, the share of the working load the bolt sees |
| `joint.stress_equivalent_working` | `sigma_red_B` | MPa | Equivalent stress in the bolt in the working state |

## Checks

- Residual clamp force against separation
- Required residual clamp force against the residual clamp force
- Assembly equivalent stress against the permitted stress
- Working equivalent stress against the proof strength
- Fatigue stress amplitude against the endurance limit
- Thread engagement needed against the engagement provided

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `separation` | Warning | Conservative | The residual clamp force is below zero: the joint opens under the working load. |
| `geometry` | Block | Conservative | The hole must be larger than the thread and smaller than the bearing diameter. |
| `no-preload` | Block | Conservative | The given-preload method needs F_V. |
| `preload-yield` | Warning | Conservative | Preload alone takes the bolt above 90 % of its proof strength. |

## Limitations

Concentric only. Frusta with the hole diameter inside. Torsion kept at 50 % in service. Stripping by a half-pitch shear area. No thermal or transverse-slip checks.

## Changes

**1.3.0 (C-2, phase B).** Four inputs and one output are renamed, and the old symbols stay as aliases (an inputs file, a CSV, a saved draft, a pack example and a link that use them still load): `P` is `p_thread` (the thread pitch, so that `P` can keep meaning power), `sigma_A` is `sigma_a_limit` (the endurance limit of the bolt, not a stress), `n` is `n_load` (the load introduction factor) and `f_Z` is `f_embed` (the embedding loss); the output `Phi` is `Phi_K`. The inputs are grouped (`Setup`, `Bolt`, `Joint`, `Tightening`, `Loads`, `Thread`, `Given properties`) and have usual ranges. The tightening torque `M_A` is entered in kN·m (the same value as before) and the given stiffnesses in kN/m. The sheet declares ports. The checks show a value against a value. Calculated values are unchanged.

**1.2.1 (mech.bolted-joint-01, -R2).** Statements only, no number changes. The thread torsion modulus is stated: `W_p = π/16·d_3³` at the minor diameter, which is conservative against the VDI 2230 mean diameter `d_S = (d_2 + d_3)/2` (at the m12-88 defaults `τ_M` is 16 % higher and `ν_M` 0.9687 against 0.9180, 5.5 % higher). The method text and an assumption say so. `F_M` is the maximum preload, so the nut factor `K` and the friction coefficients `μ_G`, `μ_K` are labelled as the smallest values the joint can have. A mean friction coefficient understates the maximum preload by about 23 % at α_A 1.6, and the yield and stripping checks then use too low a load. An assumption states it.

**1.2.0.** The permitted assembly utilisation `nu_perm` (default 1.0) replaced the fixed 0.9 limit: an M12 8.8 friction-method joint at 95 N·m reads `nu_M` 1.150, σ_red,M 736 MPa against R_p0.2 640 MPa, and all five checks passed before.

## References

- VDI 2230 Part 1:2015, §5.1–§5.5: Systematic calculation of highly stressed bolted joints — calculation steps R0–R13.
- ISO 898-1:2013, §9.1.6: Tensile stress area of the thread.
- Budynas and Nisbett, Shigley's Mechanical Engineering Design, §8-5, §8-7, 11th ed.: Joint stiffness (frusta); preload and torque.

## Test cases

`ka-m12-88-nut-factor` (A_s 84.27 mm², F_M 38.75 kN, analytic, Confirmed); `ka-shigley-stiffness` (k_P 1.908e6 kN/m, Φ 0.0720, F_M 40.69 kN, re-derived from Shigley eqs 8-17 and 8-20, Confirmed); the legacy module's three vectors `ka-legacy-given-1`, `-2` and `-given-separates` (given preload and stiffnesses; vector 2's yield factor corrected to 3.2578 because the joint separates first, D-42).

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
… 26 more step(s) on the sheet
Governing: PASS  Assembly equivalent stress against the permitted stress: 619.9 MPa vs 640 MPa  utilisation 0.97  « VDI 2230 R7
```
<!-- hand-arithmetic:end -->
