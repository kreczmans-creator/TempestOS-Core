# Bolts and fillet welds (EN 1993-1-8) (`struct.connection-ec3@1.9.0`)

**In plain terms.** A bolted joint can fail by the bolt shearing, the plate tearing or crushing at the hole, or the bolt pulling through or stretching, and a fillet weld can fail along its throat. The worksheet takes the bolt, plate and weld you describe and the loads on them, works out how much each failure mode can carry under Eurocode 3, and divides the load by the smallest capacity. A ratio under 1 passes, and the mode with the highest ratio governs.

## Scope

Calculates the design resistance of one bolt in a plate, block tearing of the bolt group, the spacing limits, and a fillet weld by the simplified and directional methods, each against its design force. Loading is static.

## Method

- Bolt strengths: f_ub = class × 100 MPa; A_s = π/4·((d₂ + d₃)/2)², d₂ = d − 0.6495P, d₃ = d − 1.2269P
- Shear (cl. 3.6, Table 3.4): F_v,Rd = k_v12·α_v·f_ub·A·n_s/γ_M2; α_v = 0.6 (4.6, 5.6, 8.8 or shank in the plane), 0.5 (4.8, 5.8, 6.8, 10.9 with threads in the plane); k_v12 = 0.85 for an M12 bolt of class 4.8, 5.8, 6.8, 8.8 or 10.9 in its normal clearance hole d₀ = d + 2 (Table 3.4, note on M12 bolts), else 1
- Bolt bearing resistance: F_b,Rd = k_h·min(k₁·α_b·f_u·d·t/γ_M2, [single lap, one bolt row: 1.5·f_u·d·t/γ_M2]); α_b = min(α_d, f_ub/f_u, 1), α_d = e₁/3d₀ (end) or p₁/3d₀ − 1/4 (inner); k₁ = min(2.8e₂/d₀ − 1.7, 1.4p₂/d₀ − 1.7, 2.5); k_h = 1 (normal holes), 0.8 (oversize), 0.6 (slot across the load)
- Bolt tension resistance: F_t,Rd = k₂·f_ub·A_s/γ_M2, k₂ = 0.9; punching B_p,Rd = 0.6π·d_m·t·f_u/γ_M2, d_m = (s + e)/2 from the across-flats s and the across-corners e of the smaller of head and nut, or d_m = s when e is not entered (conservative); combined F_v/F_v,Rd + F_t/(1.4F_t,Rd) ≤ 1
- Block tearing (cl. 3.10.2): V_eff,1,Rd = f_u·A_nt/γ_M2 + f_y·A_nv/(√3·γ_M0) for a group loaded concentrically; V_eff,2,Rd = 0.5·f_u·A_nt/γ_M2 + f_y·A_nv/(√3·γ_M0) for an eccentrically loaded group
- Weld, simplified (cl. 4.5.3.3): f_vw,d = f_u/(√3·β_w·γ_M2), F_w,Rd = f_vw,d·a; directional (cl. 4.5.3.2): √(σ⊥² + 3(τ⊥² + τ∥²)) ≤ f_u/(β_w·γ_M2), σ⊥ ≤ 0.9f_u/γ_M2

f_ub from the property class; A_s from the ISO 898-1 formula with ISO 261 pitches. `F_v,Rd = k_v12 α_v f_ub A n_s/γ_M2` (α_v 0.6 or 0.5 per Table 3.4; `k_v12` = 0.85 for an M12 bolt of class 4.8, 5.8, 6.8, 8.8 or 10.9 in its normal clearance hole, d₀ = d + 2 = 14 mm with `hole_type` Normal, by the note on M12 bolts of Table 3.4, printed as a step with its reason; 1 for every other size, class or hole, so an M12 in a 13 mm hole, in an oversize hole or of class 4.6 or 5.6 carries no factor); `F_b,Rd = k_h · min(k₁ α_b f_u d t/γ_M2, 1.5 f_u d t/γ_M2)` at the worst of end/inner and edge bolts, where the 1.5 cap applies only to a single-lap joint with one bolt row (`single_lap` on, one shear plane; cl. 3.6.1) and `k_h` is the hole factor of Table 3.4: 1 for normal holes, 0.8 for oversize holes, 0.6 for a slot across the load (`hole_type`); a `d_0` above the normal clearance (d + 2, d + 3 from M27) with the type left at Normal is treated as oversize and warns (`hole_clearance`); with one shear plane and `k₁ α_b` above 1.5 and `single_lap` off a warning (`single_lap`) states the capped value; `F_t,Rd = 0.9 f_ub A_s/γ_M2`; `B_p,Rd = 0.6π d_m t f_u/γ_M2` with `d_m = (s + e)/2` from the across-flats `s` and the across-corners `e` of the smaller of head and nut (optional input `e_head`), or `d_m = s` when `e_head` is blank (conservative: the sharp-hexagon value overstated it by 8 %); `F_v/F_v,Rd + F_t/(1.4F_t,Rd) ≤ 1` (cl. 3.6.1). Block tearing `V_eff,1,Rd = f_u A_nt/γ_M2 + f_y A_nv/(√3 γ_M0)` for a concentrically loaded group, `V_eff,2,Rd = 0.5 f_u A_nt/γ_M2 + f_y A_nv/(√3 γ_M0)` for an eccentrically loaded one, chosen with `block_load` (cl. 3.10.2; concentric is the default, so an eccentric group must be selected). Welds `f_vw,d = f_u/(√3 β_w γ_M2)` (cl. 4.5.3.3), directional `√(σ⊥² + 3(τ⊥² + τ∥²)) ≤ f_u/(β_w γ_M2)`, `σ⊥ ≤ 0.9 f_u/γ_M2` (cl. 4.5.3.2); β_w by grade (Table 4.1).

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group | Old symbols |
|---|---|---|---|---|---|---|
| `grade` | Bolt property class | — | 8.8 | 4.6, 4.8, 5.6, 5.8, 6.8, 8.8, 10.9 | Setup | — |
| `size` | Bolt size | — | M20 | M12, M16, M20, M22, M24, M27, M30, M36 | Setup | — |
| `steel` | Plate steel grade (β_w) | — | S275 | S235, S275, S355, S420, S460 | Setup | — |
| `hole_type` | Hole type: oversize holes reduce the bearing resistance by 0.8, a slot across the load by 0.6 (a d₀ above the normal clearance is treated as oversize) | — | Normal clearance | Normal clearance, Oversize, Slot across the load | Setup | — |
| `block_load` | Block tearing loading: concentric (V_eff,1) or eccentric (V_eff,2, the tension term halved) | — | Concentric loading (V_eff,1) | Concentric loading (V_eff,1), Eccentric loading (V_eff,2) | Setup | — |
| `single_lap` | Single-lap joint with only one bolt row (limits the bearing resistance to 1.5·f_u·d·t/γ_M2, cl. 3.6.1) | — | 0 | — | Setup | — |
| `threads` | Threads in the shear plane | — | 1 | — | Setup | — |
| `n_s` | Shear planes | — | 1 | 1 to 2 | Setup | — |
| `t_p` | Plate thickness | mm | 10 | 4 to 40 | Geometry | `t` |
| `e_1` | End distance (load direction) | mm | 40 | 15 to 250 | Geometry | — |
| `e_2` | Edge distance (across the load) | mm | 40 | 15 to 250 | Geometry | — |
| `d_0` | Hole diameter (optional; blank = normal clearance d + 2, or d + 3 from M27) | mm | blank | 14 to 40 | Geometry | — |
| `t_2` | Thickness of the other ply, such as the column flange behind an end plate (optional; blank = one ply only) | mm | blank | 4 to 40 | Geometry | — |
| `p_1` | Pitch in the load direction (optional; blank = one row) | mm | blank | 30 to 300 | Geometry | — |
| `p_2` | Gauge across the load (optional; blank = one line) | mm | blank | 30 to 300 | Geometry | — |
| `e_head` | Across-corners dimension of the smaller of bolt head and nut (optional; blank = punching takes the across-flats dimension, which is conservative) | mm | blank | 18 to 70 | Geometry | — |
| `A_nt` | Net area in tension (optional; blank = no block tearing check) | mm² | blank | 10 to 50000 | Geometry | — |
| `A_nv` | Net area in shear (optional; blank = no block tearing check) | mm² | blank | 10 to 50000 | Geometry | — |
| `a` | Weld throat (optional; blank = no weld check) | mm | blank | 3 to 15 | Geometry | — |
| `f_y` | Plate yield strength | MPa | 275 | 235 to 440 | Material | — |
| `f_u` | Plate ultimate tensile strength | MPa | 410 | 340 to 700 | Material | — |
| `F_v_Ed` | Shear per bolt | kN | 50 | 0 to 1000 | Loads | — |
| `F_t_Ed` | Tension per bolt | kN | 0 | 0 to 1000 | Loads | — |
| `V_Ed` | Group force for block tearing (optional; blank = no block tearing check) | kN | blank | 0 to 5000 | Loads | — |
| `F_w_Ed` | Weld force per unit length (optional; blank = no weld check) | kN/m | blank | 0 to 5000 | Loads | — |
| `sigma_perp` | Directional method: normal stress perpendicular to the throat (optional; blank = no directional check) | MPa | blank | -500 to 500 | Loads | — |
| `tau_perp` | Directional method: shear perpendicular to the weld axis (optional; blank = no directional check) | MPa | blank | -500 to 500 | Loads | — |
| `tau_par` | Directional method: shear parallel to the weld axis (optional; blank = no directional check) | MPa | blank | -500 to 500 | Loads | — |
| `gamma_M2` | Partial factor for bolt and weld resistance | — | 1.25 | 1.1 to 1.4 | Factors | — |
| `gamma_M0` | Partial factor for cross-section resistance | — | 1 | 1 to 1.1 | Factors | — |
| `k_e1` | Minimum e₁/d₀ | — | 1.2 | 1 to 2.5 | Requirements | — |
| `k_e2` | Minimum e₂/d₀ | — | 1.2 | 1 to 2.5 | Requirements | — |
| `k_p1` | Minimum p₁/d₀ | — | 2.2 | 2 to 3.5 | Requirements | — |
| `k_p2` | Minimum p₂/d₀ | — | 2.4 | 2 to 3.5 | Requirements | — |

## Assumptions

1. Loading is static. Basis: Scope of this sheet.
2. Bolts are category A in shear (bearing type) and category D in tension (non-preloaded). Basis: Scope of this sheet.
3. The joint has no slip-resistant bolts and no moment end plates. Basis: Scope of this sheet.
4. Bearing is taken at the worst of an end bolt and an inner bolt. Basis: Conservative for every bolt in the group.
5. Bearing across the load is taken at an edge bolt, with k₁ from e₂ and p₂. Basis: Conservative for every bolt in the group.
6. Oversize holes reduce the bearing resistance by 0.8, and a slot across the load by 0.6. Basis: BS EN 1993-1-8:2005 cl. 3.6.1, Table 3.4.
7. A hole above the normal clearance, with the hole type left at normal, is treated as oversize. Basis: BS EN 1993-1-8:2005 cl. 3.6.1, Table 3.4.
8. A 12 mm bolt in its normal clearance hole takes 0.85 on its shear resistance, except classes 4.6 and 5.6. Basis: BS EN 1993-1-8:2005 Table 3.4, the note on 12 mm and 14 mm bolts in 2 mm clearance holes.
9. The single-lap cap on the bearing resistance applies only when the single-lap switch is on. Basis: BS EN 1993-1-8:2005 cl. 3.6.1; a warning says when it would bind.
10. Spacing minima and γ_M factors are entered from a National Annex record. Basis: A small set of factors only; no table from a standard is shipped.
11. The defaults for the spacing minima and γ_M factors are the recommended values of BS EN 1993-1-8. Basis: A small set of factors only; no table from a standard is shipped.
12. Group forces and weld forces per length come from the bolt group and weld group sheets by link. Basis: Linked from the bolt-group and weld-group calculations.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `f_ub` | Bolt ultimate tensile strength | MPa |
| `f_yb` | Bolt yield strength | MPa |
| `A_s` | Stress area | mm² |
| `alpha_v` | Shear factor | — |
| `k_v12` | Shear factor for the 2 mm clearance hole (an M12 bolt in its normal clearance hole only) | — |
| `F_v_Rd` | Shear resistance | kN |
| `alpha_b` | Bearing factor | — |
| `k_1` | Bearing factor k₁ | — |
| `F_b_Rd` | Bearing resistance | kN |
| `F_t_Rd` | Tension resistance | kN |
| `B_p_Rd` | Punching resistance | kN |
| `u_v` | Shear utilisation | — |
| `u_b` | Bearing utilisation | — |
| `u_t` | Tension utilisation | — |
| `u_vt` | Combined utilisation | — |
| `governing` | Governing bolt mode | — |

## Ports

| Port | Output symbol | Description | Unit | Old symbols |
|---|---|---|---|---|
| `bolt.resistance_shear` | `F_v_Rd` | Shear resistance per bolt | kN | — |
| `bolt.resistance_bearing` | `F_b_Rd` | Bearing resistance per bolt (→ base-plate F_1_vb_Rd) | kN | — |
| `bolt.resistance_tension` | `F_t_Rd` | Tension resistance per bolt (→ base-plate F_t_Rd) | kN | — |
| `bolt.area_stress` | `A_s` | Bolt tensile stress area (→ base-plate A_s) | mm² | — |
| `bolt.strength_ultimate` | `f_ub` | Bolt ultimate tensile strength (→ base-plate f_ub) | MPa | — |
| `bolt.strength_yield` | `f_yb` | Bolt yield strength (→ base-plate f_yb) | MPa | — |
| `weld.strength_shear_design` | `f_vw_d` | Weld design shear strength (→ weld-group f_w) | MPa | — |

## Checks

Checks the sheet makes:

- Shear force against bolt shear resistance
- Shear force against bolt bearing resistance
- Tension force against bolt tension or punching resistance
- Combined shear and tension against its limit
- Spacing and edge distances against the Table 3.3 minima
- Weld force against simplified weld resistance

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `hole` | Block | Conservative | The hole must be larger than the bolt; an oversize or slotted hole type needs its hole size d_0 entered. |
| `hole_clearance` | Warning | Conservative | A d₀ above the normal clearance with the hole type left at normal is treated as oversize (bearing × 0.8). |
| `single_lap` | Warning | Conservative | With one shear plane and k₁·α_b above 1.5 the bearing resistance is capped for a single-lap joint with one bolt row; the sheet warns until the joint type is stated. |
| `spacing` | Block | Conservative | The edge distance e₂, the gauge p₂ and the pitch p₁ must leave a positive bearing factor (k₁ and α_b above 0). |
| `e_head` | Block | Conservative | The across-corners dimension cannot be less than the across-flats dimension of the nut. |
| `block` | Warning | Unconservative | Block tearing needs the group force and both net areas. |
| `weld` | Warning | Unconservative | The weld check needs a throat and a force per length. |

## Limitations

A spacing that leaves no bearing resistance (`e_2` or `p_2` so small that `k_1 ≤ 0`, or `p_1` so small that `α_d ≤ 0`) blocks naming the input and the Table 3.3 minimum, instead of failing with a division by zero. The 0.85 factor on `F_v,Rd` of the Table 3.4 note is applied only where the sheet knows the bolt is M12 and the hole is its normal clearance hole (`d_0` blank or 14 mm, `hole_type` Normal); an M14 is not a size of this sheet, and a countersunk bolt is outside it. Static; categories A and D only; no slip-resistant joints, no moment end plates, no countersunk or long joints.

### Changes by version

**1.9.0 (DC-03 S4, owner decision DC-21).** An M12 bolt of class 4.8, 5.8, 6.8, 8.8 or 10.9 in its normal clearance hole (d₀ = d + 2 = 14 mm, `hole_type` Normal) takes the factor 0.85 on `F_v,Rd` from the note on M12 bolts of BS EN 1993-1-8:2005 Table 3.4; the sheet prints the factor and its reason as the step `k_v12` and multiplies it into the `F_v,Rd` step. Nothing else changes: an M12 in a 13 mm hole, in an oversize or slotted hole, or of class 4.6 or 5.6, and every other size, keep their values. New known-answer case `ka-m12-88-clearance-shear`: M12 8.8 at 30 kN shear, F_v,Rd 32.358 → 27.505 kN, u_v 0.927 (passed) → 1.091 (fails); python3: `0.85*0.6*800*84.2665/1.25/1e3 = 27.5046`, `30/27.5046 = 1.0907`. C-1 finding `struct.connection-ec3-R1`.

**1.8.0 (C-2 consistency).** The plate thickness `t` is now `t_p` (old symbol kept as an alias), the same name as the base plate sheet. Every check shows demand and capacity with units: the shear force against each resistance, the tension against the smaller of the tension and punching resistances (new step `F_t_lim`), the group force against block tearing, the weld force per length against the weld resistance, and the interaction and directional sums against the limit 1 (new step `u_limit`). The weld force per length is entered and shown in kN/m (1 N/mm is 1 kN/m; the value does not change). The inputs are grouped Setup, Geometry, Material, Loads, Factors and Requirements, with the optional hints in the standard form, and every numeric input has a usual range inside its hard range. The hard range of `f_y`, `f_u`, `k_e1` to `k_p2`, `gamma_M2` and `gamma_M0` is wider than before, so that the usual range (the old limits) sits inside it: a value outside the usual range now warns instead of being refused. The standard is written BS EN 1993-1-8:2005, the ultimate strength is the ultimate tensile strength, and the messages and assumptions follow the sheet standard. No calculated value changes.

**Hole type (1.5.0).** A hole type of "Oversize" or "Slot across the load" with `d_0` blank is blocked (rule `hole`): the sheet does not ship the oversize clearances of the execution standard, and the normal hole d + 2 (d + 3 from M27) in `α_d`, `k₁` and the edge checks overstated the bearing resistance (M20, e₁ = e₂ = 40 mm: 79.52 kN against 72.89 kN at the 24 mm oversize hole, 9 % unconservative). Enter `d_0` (for a slot, its width across the load).

**Two plies (1.6.0).** The optional input `t_2` is the thickness of the other ply the bolt bears on (the column flange behind an end plate); bearing is taken on the thinner of `t` and `t_2`, with this sheet's `f_u`, edge distances and spacings for both (M20 8.8, S275, e₁ = e₂ = 40, p₁ 150, p₂ 120: `F_b,Rd` 198.79 kN on a 20 mm end plate, 109.33 kN with an 11 mm flange behind it). Blank: one ply, as before. Punching still uses `t`.

**v0.25.2 reconciliation (ADR-0169).** The legacy bolt-shear module is the shank-in-plane case (gross area, α_v 0.6 for every class) and now says so; threads in the shear plane are handled here (A_s, α_v 0.5 for 4.8–6.8 and 10.9).

## References

- BS EN 1993-1-8:2005, cl. 3.5, Table 3.3: Positioning of holes for bolts.
- BS EN 1993-1-8:2005, cl. 3.6.1, Table 3.4: Design resistance of individual fasteners.
- BS EN 1993-1-8:2005, cl. 3.10.2: Block tearing.
- BS EN 1993-1-8:2005, cl. 4.5.3.2, 4.5.3.3, Table 4.1: Fillet welds: directional and simplified methods; correlation factor.
- ISO 898-1:2013, cl. 9.1: Nominal stress area (formula).

## Test cases

`ka-m20-88-shear-bearing` (F_v,Rd 94.00, F_t,Rd 141.00, F_b,Rd 99.39, B_p,Rd 185.48 kN with d_m = s), `ka-m20-punching-e-head` (e = 34 mm, illustrative: B_p,Rd 197.84 kN), `ka-m20-single-lap-cap` (e₁ = e₂ = 60, single lap: 149.09 limited to 98.40 kN), `ka-m20-oversize-hole` (d₀ 24 oversize: 91.11 × 0.8 = 72.89 kN), `ka-m12-88-clearance-shear` (M12 8.8, 14 mm hole, 30 kN: k_v12 0.85, F_v,Rd 27.505 kN, u_v 1.0907 fails; 0.927 before the factor), `ka-m20-109-threads` (97.92 kN), `ka-weld-s275` (f_vw,d 222.79 MPa) — analytic, Confirmed; `ka-p363-m20-88` (SCI P363 94.1 / 141 kN, 1 %) and `ka-p358-fin-plate` (bearing 99.4 kN pattern) — **Unconfirmed** until checked against the owner's SCI copies.

- `ka-m20-88-shear-bearing`: M20 8.8, single shear through threads, 10 mm S275 plate. Source: Analytic (EN 1993-1-8 Table 3.4 relations), BS EN 1993-1-8:2005.
- `ka-m20-punching-e-head`: M20 punching with the across-corners of the nut entered. Source: Analytic (EN 1993-1-8 Table 3.4, d_m = (s + e)/2), BS EN 1993-1-8:2005.
- `ka-m20-single-lap-cap`: M20 8.8, 10 mm S275, e₁ = e₂ = 60 in a single-lap joint with one bolt row: the cap 1.5·f_u·d·t/γ_M2 governs. Source: Analytic (EN 1993-1-8 cl. 3.6.1 single-lap limit), BS EN 1993-1-8:2005.
- `ka-m20-oversize-hole`: M20 8.8, 10 mm S275, e₁ = e₂ = 40, an oversize hole d₀ = 24: bearing × 0.8. Source: Analytic (EN 1993-1-8 cl. 3.6.1, Table 3.4 hole factor), BS EN 1993-1-8:2005.
- `ka-m12-88-clearance-shear`: M12 8.8 at 30 kN shear in its 14 mm normal clearance hole: k_v12 = 0.85, the shear check fails at 1.09 (it passed at 0.927 before the factor). Source: Analytic (EN 1993-1-8 Table 3.4 and its note on M12 bolts in 2 mm clearance holes), BS EN 1993-1-8:2005.
- `ka-m20-109-threads`: M20 10.9 with threads in the shear plane: α_v = 0.5. Source: Analytic (EN 1993-1-8 Table 3.4), BS EN 1993-1-8:2005.
- `ka-weld-s275`: 6 mm fillet (a = 4.2 mm) in S275, simplified method. Source: Analytic (EN 1993-1-8 cl. 4.5.3.3), BS EN 1993-1-8:2005.
- `ka-p363-m20-88`: M20 8.8 resistances against the SCI Blue Book (P363) tables. Source: SCI P363 Steel building design: design data (Blue Book), 2013 edition.
- `ka-p358-fin-plate`: Fin-plate bolt bearing pattern (SCI P358). Source: SCI P358 Joints in Steel Construction: Simple Joints to Eurocode 3, 2014.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Beam web splice: M20 8.8 in double shear through 10 mm S275 plates ([sheet](../../worked-examples/structural/struct.connection-ec3/beam-splice-m20/sheet.txt)).

```text
f_ub = class 8.8 = 800 MPa
f_yb = class 8.8 = 640 MPa
A_s = d = 20, P = 2.5 mm = 244.79 mm²
d_0 = 22 mm
alpha_v = class 8.8 = 0.6
F_v_Rd = 0.6 × 800 × 244.79 × 2 / 1.25 = 188 kN
alpha_b = min(0.60606, 0.81061, 1.9512, 1) = 0.60606
k_1 = e₂ = 35, p₂ = —, d₀ = 22 = 2.5
F_b_Rd = 2.5 × 0.60606 × 410 × 20 × 10 / 1.25 = 99.394 kN
F_t_Rd = 0.9 × 800 × 244.79 / 1.25 = 141 kN
B_p_Rd = 0.6π × 30 × 10 × 410 / 1.25 = 185.48 kN
u_v = 55 kN / 188 kN = 0.29255
u_b = 55 kN / 99.394 kN = 0.55335
u_t = 0 kN / min(141 kN, 185.48 kN) = 0
… 3 more step(s) on the sheet
Governing: PASS  Shear force against bolt bearing resistance: 55 kN vs 99.39 kN  utilisation 0.55  « EN 1993-1-8 Table 3.4
```
<!-- hand-arithmetic:end -->
