# Cylinder and actuator circuit (`fp.cylinder@1.5.0`)

**In plain terms.** A hydraulic cylinder turns fluid pressure into force and fluid flow into speed; because the rod takes up part of one side, the cylinder pushes harder than it pulls and moves faster in than out. The worksheet works out the areas, the force and speed on each stroke, the pump flow, shaft power and torque needed, the line velocities and an orifice, with the regenerative connection as an option.

## Scope

Calculates a hydraulic cylinder in its circuit: force, speed, swept volume, pump and motor duty, line velocities, orifice flow and a rod buckling check (screening only; ISO/TS 13725 method not implemented). It is not a design to a standard.

## Method

- Piston and rod areas: A_b = π·D²/4, A_r = π·d_r²/4, A_a = A_b − A_r, area ratio φ = A_b/A_a
- Cylinder force: F_ext = (p_sys·A_b − p_back·A_a)·η_m, F_ret = (p_sys·A_a − p_back·A_b)·η_m, regenerative F_ext = p_sys·A_r·η_m
- Speed and time: v_ext = Q·η_vc/A_b, v_ret = Q·η_vc/A_a, t = stroke/v, t_cycle = t_ext + t_ret + t_dwell, and a required speed gives Q = v_req·A_b/η_vc (regenerative A_r)
- Swept volume: ΔV_ext = A_b·stroke (regenerative A_r·stroke), ΔV_ret = A_a·stroke, ΔV = the larger
- Pump and motor: Q_p = V_g·n_p·η_vp, P = p_sys·Q_p/η_tp, T = P/ω, motor T = Δp·V_gm/(2π)·η_hm
- Line velocities: v = Q_line/A_line with Q_suc = max(Q, V_g·n_p), Q_prs = Q_line·A_b/A_a (regenerative A_b/A_r if larger), Q_rtn = Q_line·A_b/A_a, Q_line = max(Q, Q_p), d_min = √(4Q/(π·v_max))
- Orifice flow: Q = C_d·A_o·√(2Δp/ρ), d_o = √(4Q/(π·C_d·√(2Δp/ρ)))
- Rod buckling: L_eff = K_m·L_b, L_b = L_closed + stroke (blank 2·stroke + L_add), λ = L_eff/(d_r/4), λ_c = π·√(2E/f_y), F_cr by Euler π²·E·I/L_eff² above λ_c or Johnson A_r·f_y·[1 − f_y·λ²/(4π²E)], u = max(F_push, p_sys·A_b)·S_f/F_cr

### Notes on the method

Areas `A_b = πD²/4`, `A_r = πd_r²/4`, `A_a = A_b − A_r`, ratio `φ = A_b/A_a`. Force `F_ext = (p_sys·A_b − p_back·A_a)·η_m`, `F_ret = (p_sys·A_a − p_back·A_b)·η_m`; regenerative extend `F_ext = p_sys·A_r·η_m` (the standard values are kept as `F_std`, `v_std` for comparison). Speed `v_ext = Q·η_vc/A_b` (regenerative: `A_r`), `v_ret = Q·η_vc/A_a`; `t = stroke/v`, `t_cycle = t_ext + t_ret + t_dwell`. Required speed (since 1.3.0, optional): when `v_req` is entered the flow is `Q = v_req·A_b/η_vc` (regenerative extend: `A_r`) and replaces the entered `Q`, which should then be blank (a `Q` other than the sheet's opening 36 L/min is ignored with the warning `v-req-q`; a `Q` still at the opening value is not warned about); the derived flow must lie within the 0.1 to 2000 L/min of `Q`, else the sheet blocks (`flow`). Neither a flow nor a speed blocks. Swept volume `ΔV_ext = A_b·stroke` (regenerative: `A_r·stroke`), `ΔV_ret = A_a·stroke`, `dV` the larger. Pump `Q_p = V_g·n·η_vp`, `P = p_sys·Q_p/η_tp` (the pump delivery, so a pump that delivers more than the circuit takes is taken to pass the excess over the relief valve at `p_sys`, the conservative case for the drive; with no pump entered the circuit flow `Q` is used), `T_shaft = P/ω`, displacement needed `V_g,req = Q/(n·η_vp)`. Motor `T = Δp·V_g/(2π)·η_hm` (the displacement is entered per revolution, `cm^3/rev`, so the unit carries the 1/2π). Lines `v = Q/A_line`: the pressure and return flow base is `Q_line = max(Q, Q_p)` (no volumetric efficiency, while the speeds use `Q·η_vc`: the line flows are the conservative side of the speeds); the suction line carries the geometric delivery, `Q_suc = max(Q, V_g·n)`; the line to the cylinder cap port is judged at the larger flow of the two strokes, `Q_prs = Q_line·A_b/A_a` (the cap-side discharge on the retract stroke, the same flow as the return line `Q_rtn`; in the regenerative extend the larger of that and `Q_line·A_b/A_r`); smallest bore `d = √(4Q/(π·v_max))` and the recommended bore is the smallest listed size not below it. Orifice `Q = C_d·A_o·√(2Δp/ρ)`, bore for the circuit flow `d_o = √(4Q/(π·C_d·√(2Δp/ρ)))`. Rod buckling (screening): the buckling length is the pin-to-pin length of the cylinder at full stroke, `L_b = L_closed + stroke` (the closed pin-to-pin length plus the stroke; since 1.4.0 the cylinder body is part of it, as a cylinder buckles over its whole length and not over the rod alone; with `L_closed` blank it is estimated as `L_closed = stroke + L_add`, so `L_b = 2·stroke + L_add`, and the sheet warns `length`), `L_eff = K_m·L_b`, solid round rod `r = d_r/4`, `I = πd_r⁴/64`, `λ = L_eff/r`, `λ_c = π√(2E/f_y)`; Euler `F_cr = π²E·I/L_eff²` for `λ > λ_c`, Johnson `F_cr = A_r·f_y·[1 − f_y·λ²/(4π²E)]` for `λ ≤ λ_c` (the two meet, with equal slope, at `λ_c`); `F_allow = F_cr/S_f`, the rod load is `F_buck = max(F_push, F_stall)` with the stall thrust `F_stall = p_sys·A_b`: when the cylinder stalls at the end of its stroke, meets an obstruction or runs up to its relief setting the rod carries the full pressure force on the bore, whatever push load was entered. At stall there is no flow, so no back pressure and no friction loss: `F_stall` has no `η_m` and no `p_back` (L-a), and the regenerative extend is judged at the same standard-connection `F_stall`, because the standard connection can always be selected and its thrust is the larger (L-b). No pressure-limiting device is credited; utilisation `u_buck = F_buck/F_allow`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Hard range | Group |
|---|---|---|---|---|---|---|
| `mode` | Extend connection |  | Standard | Standard, Regenerative extend |  | Setup |
| `D` | Bore | mm | 100 | 20 to 400 | 10 to 500 | Geometry |
| `d_r` | Rod diameter | mm | 70 | 10 to 300 | 4 to 450 | Geometry |
| `stroke` | Stroke | mm | 500 | 10 to 3000 | 1 to 5000 | Geometry |
| `d_suc` | Suction line inside diameter | mm | 40 | 8 to 150 | 3 to 200 | Geometry |
| `d_prs` | Pressure line inside diameter, to the cylinder cap port and judged at the larger flow of the two strokes | mm | 16 | 8 to 150 | 3 to 200 | Geometry |
| `d_ret` | Return line inside diameter | mm | 25 | 8 to 150 | 3 to 200 | Geometry |
| `E` | Young's modulus of the rod | GPa | 210 | 60 to 220 | 50 to 250 | Material |
| `f_y` | Yield strength of the rod | MPa | 500 | 200 to 1200 | 100 to 1500 | Material |
| `F_push` | Load to push on the extend stroke, which compresses the rod | kN | 80 | 0 to 5000 | 0 to 15000 | Loads |
| `F_pull` | Load to pull on the retract stroke | kN | 40 | 0 to 5000 | 0 to 15000 | Loads |
| `p_sys` (was `p`) | Supply pressure at the cylinder and the pump, gauge | bar | 160 | 5 to 420 | 1 to 700 | Conditions |
| `p_back` | Back pressure on the discharge side of the standard connection, gauge | bar | 0 | 0 to 100 | 0 to 700 | Conditions |
| `rho` | Fluid density | kg/m³ | 870 | 750 to 1100 | 700 to 1300 | Conditions |
| `t_dwell` | Dwell time added to each cycle | s | 0 | 0 to 600 | 0 to 3600 | Conditions |
| `Q` | Flow into the cylinder (optional; blank = a required extend speed gives it) | L/min | 36 | 1 to 1000 | 0.1 to 2000 | Conditions |
| `v_req` | Required extend speed, which gives the flow and replaces Q, such as the output motion of a mechanism as a kinematic proxy (optional; blank = the flow Q sets the speed) | mm/s | optional | 1 to 2000 | 0.1 to 10000 | Conditions |
| `eta_m` | Mechanical efficiency of the cylinder, from seal and guide friction |  | 0.95 | 0.8 to 1 | 0.5 to 1 | Factors |
| `eta_vc` | Volumetric efficiency of the cylinder, from leakage past the piston |  | 0.98 | 0.9 to 1 | 0.5 to 1 | Factors |
| `K_m` | Effective-length factor K of the mounting (L_eff = K·L) |  | 1 | 0.5 to 2 | 0.5 to 2.5 | Factors |
| `S_f` | Safety factor on the critical load |  | 3.5 | 1.5 to 10 | 1 to 20 | Factors |
| `v_suc_max` | Suction line velocity limit (your record, and the default is a placeholder, not a code value) | m/s | 1.2 | 0.3 to 10 | 0.1 to 20 | Requirements |
| `v_prs_max` | Pressure line velocity limit (your record, and the default is a placeholder, not a code value) | m/s | 5 | 0.3 to 10 | 0.1 to 20 | Requirements |
| `v_ret_max` | Return line velocity limit (your record, and the default is a placeholder, not a code value) | m/s | 3 | 0.3 to 10 | 0.1 to 20 | Requirements |
| `hoses` | Available hose or pipe inside diameters from your supplier's range (optional; blank = no size is recommended) |  | table |  |  | Requirements |
| `hoses.ID` | Inside diameter (column) | mm |  | | | Requirements |
| `L_add` | Length of the closed cylinder beyond its stroke (body, end caps and mounting eyes), added to the stroke when the closed length is not entered | mm | 100 | 0 to 1000 | 0 to 5000 | Rod buckling |
| `L_closed` | Closed (retracted) pin-to-pin length of the cylinder, from the cap-end mounting to the rod-end mounting, body included (optional; blank = stroke + L_add) | mm | optional | 50 to 10000 | 1 to 20000 | Rod buckling |
| `eta_vp` | Volumetric efficiency of the pump |  | 0.95 | 0.8 to 1 | 0.5 to 1 | Pump |
| `eta_tp` | Total efficiency of the pump (volumetric × hydromechanical) |  | 0.88 | 0.7 to 1 | 0.5 to 1 | Pump |
| `V_g` | Pump displacement per revolution (optional; blank = no pump delivery check) | cm³/rev | optional | 1 to 500 | 0.1 to 1000 | Pump |
| `n_p` | Pump speed | rpm | optional | 500 to 3600 | 50 to 6000 | Pump |
| `eta_hm` | Hydromechanical efficiency of the motor |  | 0.9 | 0.7 to 1 | 0.5 to 1 | Motor |
| `V_gm` | Hydraulic motor displacement per revolution (optional; blank = no motor) | cm³/rev | optional | 1 to 500 | 0.1 to 2000 | Motor |
| `dp_m` | Pressure drop across the motor | bar | optional | 5 to 420 | 1 to 700 | Motor |
| `C_d` | Orifice discharge coefficient |  | 0.62 | 0.5 to 0.9 | 0.2 to 1 | Orifice |
| `dp_o` | Pressure drop across the orifice (optional; blank = no orifice screening) | bar | optional | 1 to 350 | 0.1 to 700 | Orifice |
| `d_o` | Orifice diameter, which gives the flow it passes | mm | optional | 0.5 to 50 | 0.1 to 100 | Orifice |

## Assumptions

1. The supply pressure p_sys is the gauge pressure at the cylinder port and at the pump outlet. Basis: F = p·A·η_m, closed form.
2. Line losses are not subtracted, because the pipe calculation gives the losses of a pipe run. Basis: F = p·A·η_m, closed form.
3. The force is the pressure on the area less the back pressure on the other side, times the mechanical efficiency. Basis: F = p·A·η_m, closed form.
4. The speed is the flow into the cylinder times the volumetric efficiency over the area. Basis: v = Q·η_vc/A.
5. The motion is steady, with no acceleration, cushioning or compressibility. Basis: v = Q·η_vc/A.
6. The cycle time is the two strokes plus the dwell. Basis: v = Q·η_vc/A.
7. The pump power is the supply pressure times the pump delivery over the total efficiency. Basis: P = p·Q_p/η_tp.
8. When the pump delivers more than the circuit takes, the excess goes over the relief valve at the supply pressure. Basis: P = p·Q_p/η_tp.
9. That is the conservative case for the drive. Basis: P = p·Q_p/η_tp.
10. The shaft torque is the power over the angular speed. Basis: P = p·Q_p/η_tp.
11. The line flows use the pump delivery when it is larger than the circuit flow. Basis: Continuity.
12. The suction line carries the geometric delivery V_g·n_p. Basis: Continuity.
13. The pressure and return lines carry the larger of the circuit flow and the pump delivery, with no volumetric efficiency. Basis: Continuity.
14. These flows are the conservative side of the speeds, which use Q·η_vc. Basis: Continuity.
15. The cap-port line is judged at the larger flow of the two strokes. Basis: Continuity.
16. On the retract stroke the cap-port line carries the cap-side discharge Q_line·A_b/A_a. Basis: Continuity.
17. In the regenerative extend it carries Q_line·A_b/A_r when that is larger. Basis: Continuity.
18. The velocity limits are your records, and the sheet defaults are placeholders, not values from a standard. Basis: Continuity.
19. The orifice relation is the sharp-edged turbulent one, Q = C_d·A·√(2Δp/ρ), with your discharge coefficient. Basis: Orifice equation, closed form.
20. It ignores viscosity and cavitation. Basis: Orifice equation, closed form.
21. It screens a flow-control orifice and is not a valve selection. Basis: Orifice equation, closed form.
22. The rod is a solid round column of its own diameter, with the effective-length factor of the mounting. Basis: Euler and Johnson column relations, closed form.
23. The tube, the piston and the rod-end stiffness are not modelled. Basis: Euler and Johnson column relations, closed form.
24. The rod diameter is taken over the whole pin-to-pin length at full stroke, closed length plus stroke. Basis: Euler and Johnson column relations, closed form.
25. A body stiffer than the rod makes this conservative. Basis: Euler and Johnson column relations, closed form.
26. The rod is judged at the larger of the push load you enter and the stall thrust p_sys·A_b. Basis: Euler and Johnson column relations, closed form.
27. A stalled, obstructed or relief-limited cylinder loads the rod with the full pressure force on the bore. Basis: Euler and Johnson column relations, closed form.
28. At stall there is no flow, so there is no back pressure, and seal friction does not relieve the rod. Basis: Euler and Johnson column relations, closed form.
29. No pressure-limiting device is credited. Basis: Euler and Johnson column relations, closed form.
30. The regenerative extend is judged at the same stall thrust, because the standard connection can always be selected. Basis: Euler and Johnson column relations, closed form.
31. The mounting factor K_m defaults to 1, pinned at both ends, and a fixed-free mounting is K_m = 2. Basis: Euler and Johnson column relations, closed form.
32. The mounting factor K_m and the safety factor S_f are your records. Basis: Euler and Johnson column relations, closed form.
33. The rod buckling check is a screening, and the ISO/TS 13725 method is not implemented. Basis: Euler and Johnson column relations, closed form.

## Outputs

| Symbol | Description |
|---|---|
| `A_b` | Bore area |
| `A_r` | Rod area |
| `A_a` | Annulus area |
| `phi` | Area ratio A_b/A_a |
| `F_ext` | Extend force |
| `F_ret` | Retract force |
| `v_ext` | Extend speed |
| `v_ret` | Retract speed |
| `t_ext` | Extend time |
| `t_ret` | Retract time |
| `t_cycle` | Cycle time |
| `dV_ext` | Extend swept volume |
| `dV_ret` | Retract swept volume |
| `dV` | Swept volume per stroke (the larger) |
| `p_push` | Pressure the push load needs at the cylinder |
| `u_push` | Push load over the extend force |
| `u_pull` | Pull load over the retract force |
| `Q` | Flow into the cylinder |
| `p_sys` | Supply pressure |
| `rho` | Fluid density |
| `d_prs` | Pressure line inside diameter |
| `Q_p` | Pump delivery |
| `Vg_req` | Pump displacement the circuit needs |
| `u_pump` | Circuit flow over the pump delivery |
| `P_pump` | Pump shaft power |
| `T_shaft` | Pump shaft torque |
| `n_shaft` | Pump shaft speed |
| `T_motor` | Hydraulic motor torque |
| `Q_line` | Flow in the pressure and return lines before the area ratio |
| `Q_suc` | Flow in the suction line |
| `Q_prs` | Flow in the line to the cap port (worst stroke) |
| `Q_rtn` | Flow in the return line |
| `v_suc` | Suction line velocity |
| `v_prs` | Pressure line velocity (cap-port line, worst stroke) |
| `v_rtn` | Return line velocity |
| `u_suc` | Suction line velocity over its limit |
| `u_prs` | Pressure line velocity over its limit |
| `u_rtn` | Return line velocity over its limit |
| `d_min_suc` | Smallest suction line bore |
| `d_min_prs` | Smallest pressure line bore |
| `d_min_rtn` | Smallest return line bore |
| `d_rec_suc` | Recommended suction line bore |
| `d_rec_prs` | Recommended pressure line bore |
| `d_rec_rtn` | Recommended return line bore |
| `d_o_req` | Orifice diameter for the circuit flow |
| `Q_o` | Flow through the entered orifice |
| `L_b` | Buckling length (pin to pin at full stroke) |
| `L_eff` | Effective length |
| `lam` | Slenderness |
| `lam_c` | Transition slenderness |
| `F_cr` | Critical load (screening) |
| `F_allow` | Allowable push load (screening) |
| `F_stall` | Stall thrust p_sys·A_b |
| `F_buck` | Rod load judged for buckling: the larger of the push load and the stall thrust |
| `u_buck` | Rod buckling utilisation (screening) |
| `buckling_note` | Rod buckling check |
| `F_std` | Extend force, standard connection (comparison) |
| `v_std` | Extend speed, standard connection (comparison) |

`A_b`, `A_r`, `A_a`, `phi`, `F_ext`, `F_ret` (`F_std` and `v_std` in the regenerative connection), `v_ext`, `v_ret`, `t_ext`, `t_ret`, `t_cycle`, `dV_ext`, `dV_ret`, `dV`, `u_push`, `u_pull`, `p_push` (the pressure the push load needs at the cylinder, `(F_push/η_m + p_back·A_a)/A_b`; regenerative `F_push/(η_m·A_r)`; gauge), `Q`, `p_sys`; pump `Q_p`, `Vg_req`, `u_pump`, `P_pump`, `T_shaft`, `n_shaft`; motor `T_motor`; lines `Q_line`, `Q_suc`, `Q_prs`, `Q_rtn`, `v_suc`, `v_prs`, `v_rtn`, `u_suc`, `u_prs`, `u_rtn`, `d_min_*`, `d_rec_*`; orifice `d_o_req`, `Q_o`; rod `L_b`, `L_eff`, `lam`, `lam_c`, `F_cr`, `F_allow`, `F_stall`, `F_buck`, `u_buck`, `buckling_note` (text). **Ports.** `Q` (`hyd.flow`), `p_sys` (`hyd.pressure`), `P_pump` (`hyd.pump_power`), `u_buck` (`hyd.buckling_util`), `F_ext` (`hyd.cylinder_force`), `dV` (swept volume of the larger stroke, to F2's emergency-stroke ΔV), `p_push` (since 1.4.0, bar gauge, to F2's `p_load`: its lowest working pressure must carry the push load), `T_shaft` and `n_shaft` (pump shaft torque and speed, to E1 as B1 does; present when a pump is entered), and since 1.2.0 `Q_prs` (`L/min`, the flow in the line to the cap port, the larger of the two strokes, to the pressure line B1), `rho` (`kg/m^3`, the fluid density) and `d_prs` (`mm`, the line bore), so a pressure-line calculation takes the line F1 judges, its density and its bore by link, and goes stale when the bore is revised. **Checks.** extend force covers the push load; retract force covers the pull load; rod buckling (screening); suction, pressure (cap-port line, larger stroke flow) and return line velocity within the limit; pump delivery covers the circuit flow (pump entered). **Warnings.** `size` when no listed size is large enough for a line; `length` when no closed length is entered and the buckling length is estimated (since 1.4.0).

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as the port's key, so a saved link, a pack binding or an inputs file that uses the symbol or an alias still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `actuator.flow` | `Q` | L/min | Flow into the cylinder circuit |
| `actuator.pressure_supply` | `p_sys` (was `p`) | bar | Supply pressure, gauge |
| `actuator.power_pump` | `P_pump` | kW | Pump shaft power |
| `actuator.utilisation_buckling` | `u_buck` |  | Rod buckling utilisation, screening |
| `actuator.force_extend` | `F_ext` | kN | Cylinder force on the extend stroke |
| `actuator.volume_swept` | `dV` | L | Swept volume of the larger stroke, for the dV input of fp.accumulator |
| `actuator.pressure_push` | `p_push` | bar | Pressure the push load needs at the cylinder, gauge, for the p_load input of fp.accumulator |
| `actuator.torque_shaft` | `T_shaft` | N·m | Pump shaft torque, for the load torque input of elec.motor-sizing |
| `actuator.speed_shaft` | `n_shaft` | rpm | Pump shaft speed, for the load speed input of elec.motor-sizing |
| `actuator.flow_pressure_line` | `Q_prs` | L/min | Flow in the line to the cylinder cap port, the larger of the two strokes, for the flow input of bs.pipe-pump |
| `actuator.density_fluid` | `rho` | kg/m³ | Fluid density, for the custom fluid density input of bs.pipe-pump |
| `actuator.diameter_pressure_line` | `d_prs` | mm | Pressure line inside diameter, for the first segment bore of bs.pipe-pump |

## Checks

Checks the sheet makes:

- Push load against the extend force
- Pull load against the retract force
- Rod load against the allowable push load, screening only; ISO/TS 13725 method not implemented (Euler / Johnson column relations)
- Suction line velocity against its limit (cylinder.line-limits record)
- Pressure line velocity against its limit, cap-port line at the larger stroke flow (cylinder.line-limits record)
- Return line velocity against its limit (cylinder.line-limits record)
- Circuit flow against the pump delivery

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `rod` | Block | Conservative | The rod must be smaller than the bore. |
| `flow` | Block | Conservative | A flow into the cylinder or a required extend speed is needed, and the flow the speed needs must lie within 0.1 to 2000 L/min. |
| `v-req-q` | Warning | Conservative | A flow other than the sheet's opening 36 L/min is entered as well as a required extend speed: the flow from the speed is used and Q ignored. |
| `length` | Warning | Conservative | The closed pin-to-pin length of the cylinder is not entered: the buckling length is estimated as twice the stroke plus L_add, the cylinder body included. |
| `force` | Block | Conservative | The back pressure must leave a net force on both strokes. |
| `pump` | Block | Conservative | The pump displacement and speed are entered together, and the total efficiency cannot exceed the volumetric efficiency. |
| `motor` | Block | Conservative | The motor displacement and pressure drop are entered together. |
| `orifice` | Block | Conservative | The orifice diameter needs the pressure drop, and a drop above the supply pressure is not possible. |
| `size` | Warning | Conservative | No listed hose or pipe size is as large as the smallest bore the velocity limit needs. |

## Limitations

Steady motion only: no acceleration, cushioning, compressibility or pressure losses (line losses come from the pipe calculation, B1; `p_sys` is the pressure at the cylinder port and the pump outlet). Mechanical and volumetric efficiencies are the user's. The pump is a fixed-displacement delivery at a stated speed; no pump curve, no relief-valve or load-sensing model. The rod is a solid round column with the mounting's effective-length factor; the tube, piston and rod-end stiffness, the side load and the stop-tube are not modelled, and the rod is judged at the larger of the push load entered and the stall thrust `p_sys·A_b` (a reduced load is not credited for a pressure-limiting device). **ISO/TS 13725 is not implemented.** The orifice is the sharp-edged turbulent relation with the user's `C_d` (no viscosity, no cavitation).

## References

- ISO/TS 13725:2016: Cylinder buckling load: method not implemented, screening only.
- Esposito, Fluid Power with Applications, Ch. on cylinders and pumps, 7th ed.: Force, speed, flow and power relations, closed form.

## Test cases

### Notes on the cases

Analytic, recomputed in python3 (all Confirmed): `ka-circuit-standard` (100/70 at 160 bar, 36 L/min, 28 cm³/rev pump at 1450 rpm, motor, orifice, hose list, Johnson rod: `F_ext` 119.3805 kN, `F_ret` 60.88407 kN, `v_ext` 74.8665 mm/s, `t_cycle` 10.0846 s, `P_pump` 11.6879 kW, `T_shaft` 76.9731 N·m, `T_motor` 36.6693 N·m, return line 2.5678 m/s, suction at the geometric delivery 0.53847 m/s, cap-port line at the retract flow 75.627 L/min 6.2690 m/s (utilisation 1.254, a FAIL on the 16 mm line; the next listed size is 20 mm), `d_o_req` 5.0694 mm, `L_eff` 1100 mm (2 × 500 + 100), `F_cr` 1465.71 kN, `u_buck` 0.30008 at the 125.66 kN stall thrust); `ka-regenerative` (`F_ext` 58.4965 kN, `v_ext` 152.789 mm/s, `Q_prs` 78.7143 L/min); `ka-euler-slender` (40 mm rod over a 4100 mm pin-to-pin length: λ = 410, Euler `F_cr` 15.4939 kN; the 15 kN push load alone reads 3.3884, but a stalled cylinder at 160 bar carries `p_sys·A_b` = 80.4248 kN (76.4035 kN with `η_m`, which does not relieve a stalled rod), above `F_cr` even without a safety factor, so `u_buck` = 80.4248·3.5/15.4939 = 18.1676, a FAIL); `ka-back-pressure-fixed-free` (10 bar back pressure, K = 2: `F_ext` 115.5753 kN, `F_ret` 53.4228 kN, `L_eff` 1400 mm, Johnson `F_cr` 1181.50 kN, `u_buck` 0.37226 at the 125.66 kN stall thrust (no back pressure at stall)); `ka-orifice` (ρ 800, Δp 10 bar, C_d 0.6: jet 30 m/s, a 4 mm orifice passes 22.6195 L/min and the bore for that flow is 4 mm); `ka-lossless` (η = 1: `F = p_sys·A`, `v = Q/A`, `t_ext` 4 s). `ka-required-speed` (since 1.3.0: the lossless cylinder driven by `v_req` = 100 mm/s with `Q` blank: `Q` = 47.1239 L/min exactly, `v_ext` 100 mm/s, `t_ext` 4 s). **Unconfirmed / Secondary:** none shipped. See *Definition of done* below.

- `ka-circuit-standard`: Standard 100/70 cylinder at 160 bar with pump, motor, lines, orifice and rod check (analytic) (Confirmed; source: Analytic (closed-form areas, force, speed, pump, line, orifice and Johnson column relations))
- `ka-regenerative`: Regenerative extend: force on the rod area, speed over the rod area, cap-side line flow Q·A_b/A_r (analytic) (Confirmed; source: Analytic (regenerative connection: F = p·A_r·η_m, v = Q·η_vc/A_r, Q_cap = Q·A_b/A_r))
- `ka-euler-slender`: Slender rod: 40 mm rod over a 4100 mm pin-to-pin length is Euler-buckled and the cylinder's thrust is above its critical load (analytic) (Confirmed; source: Analytic (Euler F_cr = π²EI/(KL)² above λ_c = π√(2E/f_y)))
- `ka-back-pressure-fixed-free`: Back pressure 10 bar and a fixed-free mounting K = 2: Johnson parabola (analytic) (Confirmed; source: Analytic (F = (p·A − p_back·A_other)·η_m; Johnson F_cr = A·f_y·[1 − f_y·λ²/(4π²E)] for λ ≤ λ_c))
- `ka-orifice`: Sharp orifice: Q = C_d·A·√(2Δp/ρ) with ρ = 800 kg/m³, Δp = 10 bar (analytic) (Confirmed; source: Analytic (orifice equation, √(2·10e5/800) = 50 m/s))
- `ka-lossless`: Lossless cylinder: F = p·A and v = Q/A exactly (analytic) (Confirmed; source: Analytic (η = 1: F_ext = p·A_b, F_ret = p·A_a, v = Q/A))
- `ka-required-speed`: Lossless cylinder driven at a required extend speed: Q = v·A_b exactly (analytic) (Confirmed; source: Analytic (η = 1: Q = v_req·A_b; the flow field is blank))

## Changes

**Changes in 1.5.0 (C-2 consistency, phase C).** Layout and wording; no calculated value changes. The supply pressure `p` is now `p_sys` (alias kept, also on the port `actuator.pressure_supply`). The inputs sit in the groups Setup, Geometry, Material, Loads, Conditions, Factors and Requirements, with Rod buckling, Pump, Motor and Orifice after them, and every numeric input has a usual range inside its hard range. The seven checks read "Push load against the extend force", "Pull load against the retract force", "Rod load against the allowable push load", the three line velocities "against its limit" and "Circuit flow against the pump delivery", and compare physical values; the utilisations are the same numbers as before. The 12 ports are named `actuator.*` and their descriptions name the target template and input instead of a legacy code. The scope is two sentences and the method has eight lines. The rod material record is titled "Rod material properties". ISO/TS 13725 is cited as ISO/TS 13725:2016; the sheet standard's designation pattern does not accept the slash of a technical specification.

**Definition of done (Addendum B §A3, §B4).** Every case above is analytic and Confirmed; no published known-answer case is shipped, and none is invented. **Unconfirmed / Secondary items to list in the owner test pack:** none (this calculator has none). **Not pinned (owner check):** the Esposito *Fluid Power with Applications* 7th ed. cylinder force/speed and pump power examples (Ch. 5–6) and the Parker industrial cylinder catalogue rod-sizing procedure: their inputs and answers cannot be stated from a source available to the build, so no case is added; check the sheet against the owner's copy of the book and the catalogue (the catalogue procedure would also be Secondary).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Press ram: 125/90 cylinder at 180 bar on a 45 cm³/rev pump, lines checked, rod screened for buckling ([sheet](../../worked-examples/fluid-power/fp.cylinder/press-ram-125-90-pump-and-lines/sheet.txt)).

```text
A_b = π × (125 mm)^2 / 4 = 122.72 cm²
A_r = π × (90 mm)^2 / 4 = 63.617 cm²
A_a = 122.72 cm² − 63.617 cm² = 59.101 cm²
phi = 122.72 cm² / 59.101 cm² = 2.0764
F_ext = (180 bar g × 122.72 cm² − 0 bar g × 59.101 cm²) × 0.95 = 209.85 kN
v_ext = 60 L/min × 0.98 / 122.72 cm² = 79.858 mm/s
dV_ext = 122.72 cm² × 800 mm = 9.8175 L
F_ret = (180 bar g × 59.101 cm² − 0 bar g × 122.72 cm²) × 0.95 = 101.06 kN
v_ret = 60 L/min × 0.98 / 59.101 cm² = 165.82 mm/s
t_ext = 800 mm / 79.858 mm/s = 10.018 s
t_ret = 800 mm / 165.82 mm/s = 4.8246 s
t_cycle = 10.018 s + 4.8246 s + 0 s = 14.842 s
dV_ret = 59.101 cm² × 800 mm = 4.7281 L
dV = max(9.8175 L, 4.7281 L) = 9.8175 L
… 38 more step(s) on the sheet
Governing: PASS  Circuit flow against the pump delivery: 60 L/min vs 61.99 L/min  utilisation 0.97
```
<!-- hand-arithmetic:end -->
