# Glossary of terms and symbols

One glossary for all 54 worksheet templates (mechanical, structural, building services, electrical and fluid power). It is part of the
[sheet standard](sheet-standard.md) (Addendum C, item C-2): the terms below are the words a description uses, the quantity-type table fixes the
unit and the least decimals of each kind of number (a result shows at least four and at most five significant figures), the reserved symbols say what a bare letter means, and the last table lists every input, table
column and output symbol of every template, so an engineer who sees `F_r` or `T_amb` on one sheet reads the same thing on the next.

The tables from "Quantity types" on are generated: the contract tests write `glossary-data.json` from the live catalogue
(`TEMPEST_WRITE_GLOSSARY=1 dotnet test tests/Tempest.Calc.Tests.Calculators --filter GlossaryTests`) and `scripts/generate-glossary.py`
turns it into this file. `GlossaryTests` fails when a template adds a symbol the glossary lacks, or uses a glossary symbol for another quantity type,
until both are regenerated and committed with the change. A symbol that two or more templates use also declares what it means in each (`symbol:meaning`, in `GlossaryMeanings.json`), so two meanings of one symbol in one quantity type are listed apart. Edit this introduction by hand; never edit the generated tables.

## Terms

| Term | Means | Symbol form |
|---|---|---|
| Young's modulus | The elastic modulus of a material. Never "modulus of elasticity" or "elastic modulus". A part prefix is added when needed: "Hub Young's modulus". | `E`, `E_<part>` |
| Yield strength | The stress at which the material yields; "0.2 % proof strength" is a yield strength. A part prefix is added when needed. | `f_y`, `f_y_<part>` |
| Ultimate tensile strength | The tensile stress the material fails at. | `f_u`, `f_u_<part>` |
| Design strength | A strength a code gives (a weld throat, a bolt in shear). | `f_<part>_d` |
| Allowable stress | A limit the user chooses. Not a code value. | `sigma_allow` |
| Required X | A demand the result must meet. The input ends `_req`. | `X_req` |
| X limit | A bound that must not be crossed. The input ends `_limit`. | `X_limit` |
| Target X | An aim for sizing only; nothing fails against it. The input ends `_target`. | `X_target` |
| Safety factor | A ratio of capacity to demand, shown to at least four significant figures and rounded down, so it never reads at its target when it is below it. The word "margin" is not used. | `n_<part>`, `s_<part>` |
| Utilisation | Demand divided by capacity, written to 2 decimals (two significant figures below 0.005) and rounded up when it exceeds its limit; above 1 fails. A template declares which of its outputs are utilisations. | `u_<part>` |
| Governing | The check with the highest utilisation. The sheet prints it first and marks its result "(governing)". | |
| Headline result | A result another calculation links to: an output port. Results list these first. | |
| Port | An output offered for linking, named `<area>.<quantity>` (for example `bearing.life_modified`). Its output symbol stays as its symbol and as an alias. | |
| Alias | An old symbol that still resolves to a renamed input or port, so a saved link, a pack binding, an inputs file or a CSV keeps loading. Nothing writes an alias. | |
| Default | The value an empty input takes. It is listed under "Defaults used", apart from the warnings. | |
| Axes (members) | y-y is the major axis and z-z the minor axis in every sheet, as in EN 1993. A section sheet that draws x horizontal says so in its scope. | `I_y`, `I_z` |

## How a symbol is written

- A symbol is ASCII letters, digits and `_`. What follows the first underscore is a subscript: `T_amb`, `F_v_Rd`, `sigma_vm_max`. A Greek letter is spelled out: `sigma`, `tau`, `Phi`.
- A temperature is `T` or `T_<qualifier>` (`T_amb`, `T_max`, `T_db`); a temperature difference is `dT` or `dT_<qualifier>`. Lower-case `t` is time. `theta` is an angle and never a temperature.
- `U` is a voltage, `V` a volume, `Q` a volumetric flow, `P` a power, `F` a force, `Phi` a heat flow, `T` a torque, `n` a rotational speed; the other quantities that used these letters take a qualified symbol (`V_Ed` for a shear force, `eps_rough`, `h_conv`, `mu_visc`, `R_rel`).
- A pressure is `p_<qualifier>` (`p_atm`, `p_int`, `p_ext`, `p_pre`); a bare `p` is not used.
- Two symbols of one sheet never differ only by letter case (`E_bolt` and `E_shaft`, not `E_S` and `E_s`).
- A symbol that was renamed keeps its old spelling as an alias of the input or port, so nothing saved before the rename stops loading.

## Quantity types: units and decimals

The unit a quantity type is written in, and the decimals it is shown to (display only; the calculation keeps full precision). `CalcDisplay.Rules` is the one table of decimals and `CalcUnitStandard` the one table of units; `GlossaryTests` holds this table to them. A result is shown with at least four and at most five significant figures, so the decimals below are the least a result of that type shows (a value of 0.0034 m³ shows 0.003400); a whole number is printed exactly. A utilisation is shown to 2 decimals (2 significant figures below 0.005) and rounded up above its limit; a safety factor or margin that must be at least a target is rounded down.

| Quantity type | Symbols | Standard unit | Least decimals by unit | Exceptions |
|---|---|---|---|---|
| Acceleration | a | — | mm/s² 0; m/s² 2 | — |
| Angle | theta | — | ° 2; rev 3 | — |
| Angle (slope) | theta | — | rad 4 | — |
| Angular acceleration | alpha | — | rad/s² 2 | — |
| Apparent and reactive power | S, Q_r | kVA, MVA, kvar | VA 1; kVA 2; MVA 3; var 1; kvar 2 | — |
| Area | A | m², cm², mm² | mm² 1; cm² 2; m² 2; in² 3; mil² 0 | elec.pcb-trace: mil² (PCB practice is mil²) |
| Charge | C | — | A·h 2 | — |
| Current | I | — | mA 1; A 2; kA 3 | — |
| Density | rho | — | kg/m³ 1; g/cm³ 3 | — |
| Dynamic viscosity | mu_visc | — | Pa·s 4; mPa·s 2 | — |
| Energy | E, W | J, kJ, MJ, Wh, kWh | J 1; kJ 2; MJ 3; Wh 1; kWh 2; Btu 1 | — |
| Force | F | kN | N 1; kN 2; MN 3; lbf 1 | mech.spring: N (small spring and latch forces are below 1 kN); mech.snap-fit: N (latch forces are a few newtons); mech.mechanism: N (linkage and cam forces are small); elec.motor-sizing: N (drive sizing works in newtons and N·m); elec.containment: N (cable pulling tension is in newtons); elec.enclosure-heat: N (the door force from a pressure difference is in newtons); fp: N (hydraulic load tables are in kN or N) |
| Friction rate | dp/L | — | Pa/m 2 | — |
| Heat flow per length | q_l | — | W/m 1 | — |
| Heat flux | q | — | W/m² 1 | — |
| Heat transfer coefficient, U-value | U, h_conv | — | W/(m²·K) 2 | — |
| Length | L, d, b, h, t, delta | m, mm, µm | µm 1; mm 1; cm 1; m 2; km 3; mil 1; in 2; ft 3 | elec.pcb-trace: mil (PCB practice is mil) |
| Line force, distributed load, stiffness | w, f, k | kN/m | N/m 0; N/mm 1; kN/m 1; kN/mm 2 | mech.spring: N/mm (spring rates are in N/mm); mech.weld-group: N/mm (weld stress per unit length is in N/mm, the unit of a weld throat); elec.containment: N/m (sidewall pressure is in N/m) |
| Mass | m | — | g 1; kg 2; t 3; lb 2 | — |
| Mass flow | m_dot | — | kg/s 3; g/s 2; kg/h 1 | — |
| Mass per area | m_a | — | g/m² 1; kg/m² 3 | — |
| Mass per length | w | — | kg/m 3 | — |
| Moment of inertia (mass) | J | — | kg·m² 4; kg·cm² 2 | — |
| Moment per unit length | m | kN·m/m | N·m/m 2; kN·m/m 2 | — |
| Moment, torque | M, T | kN·m | N·mm 0; N·m 2; kN·m 2; MN·m 3 | elec.motor-sizing: N·m (drive sizing is the one place that works in N·m); mech.mechanism: N·m (drive torque in N·m, as drive sizing); fp: N·m (hydraulic drive torque in N·m); mech.spring: N·m (spring torque in N·m); mech.snap-fit: N·m (latch torque in N·m) |
| Percent quantity | RH, R, u_k | — | % 1 | — |
| Power, heat flow | P, Phi | kW, MW | mW 1; W 1; kW 2; MW 3; hp 2 | elec.pcb-trace: mW, W (PCB loss is in milliwatts) |
| Resistance, impedance | R, Z | — | mΩ 2; Ω 4; kΩ 3; MΩ 3 | — |
| Resistivity | rho_res | Ω·mm²/m | Ω·mm²/m 5; Ω·m 8 | — |
| Rotational speed, frequency | n, f | — | rpm 0; Hz 2; rad/s 2; 1/s 3 | — |
| Second moment of area | I | cm⁴, mm⁴ | mm⁴ 0; cm⁴ 1; m⁴ 6; in⁴ 2 | — |
| Specific energy | h | — | kJ/kg 1 | — |
| Specific heat | c_p | — | J/(kg·K) 0; kJ/(kg·K) 3 | — |
| Specific volume | v | — | m³/kg 4 | — |
| Stress, pressure | sigma, tau, p, E | Pa, kPa, MPa, GPa | Pa 0; kPa 1; MPa 1; GPa 0; N/mm² 1; mbar 1; bar 2; psi 1; ksi 2 | fp: bar, bar a, bar g (hydraulic practice states pressure in bar, absolute (a) or gauge (g)) |
| Temperature | T_<qualifier> | °C | °C 1; °F 1 | — |
| Temperature, temperature difference | dT | K | K 1 | — |
| Thermal conductance | UA | — | W/K 2 | — |
| Thermal conductivity | lambda | — | W/(m·K) 3 | — |
| Thermal resistance | R | — | m²·K/W 3; K/W 3; m·K/W 3 | — |
| Time | t | — | ms 1; s 2; min 1; d 1 | — |
| Time, rating life | L_h | — | h 0 | — |
| Velocity | v | — | mm/s 1; m/s 2; km/h 1 | — |
| Voltage | U | — | mV 1; V 1; kV 3 | — |
| Volume, section modulus | V, W | L, m³, cm³, mm³ | mm³ 0; cm³ 1; dm³ 1; L 1; mL 1; m³ 3; cm³/rev 2 | fp: cm³/rev (displacement per revolution is in cm³/rev) |
| Volumetric flow | Q | m³/h | m³/h 1; m³/s 4; L/s 2; L/min 1; L/h 0 | fp: L/min (hydraulic flow is in L/min) |
| Warping constant | I_w | cm⁶ | cm⁶ 0; dm⁶ 3 | — |

Plain numbers: a utilisation (declared by its template, not found by its name) is shown to 2 decimals; any other plain number to five significant figures. A percentage is a quantity in `%`, shown to 1 decimal at least.

## Reserved symbols

A bare symbol that means one kind of quantity wherever it appears. Another quantity takes a qualified symbol. `GlossaryTests` and the contract rules `C10` to `C18` hold every template to this list.

| Symbol | Means | Audit row |
|---|---|---|
| `T` | Torque or moment (a temperature is T_<qualifier>) | 12 |
| `t` | Time (a thickness is t_<part>) | 12 |
| `theta` | Angle (never a temperature) | 11 |
| `V` | Volume (a voltage is U, a shear force is V_Ed) | 14 |
| `Q` | Volumetric flow (a heat flow is Phi) | 14 |
| `P` | Power (a force is F) | 14 |
| `U` | Voltage | 14 |
| `F` | Force | 14 |
| `Phi` | Heat flow | 14 |
| `p` | Not used: a pressure is p_<qualifier> (p_atm, p_int, p_ext, p_pre) | 15 |
| `mu` | Friction coefficient (a viscosity is mu_visc) | 16 |
| `rho` | Density (a resistivity is rho_res) | 16 |
| `nu` | Poisson's ratio | 16 |
| `eta` | Efficiency (a factor is eta_<qualifier>) | 16 |
| `eps` | Effectiveness (a roughness is eps_rough) | 17 |
| `h` | Depth or height (a coefficient is h_conv) | 17 |
| `n` | Rotational speed | 17 |
| `R` | Resistance (a reliability is R_rel) | 17 |
| `D` | A diameter or a depth | 17 |
| `L` | Length | 17 |

## Symbols with more than one meaning today (95)

Each of these symbols is used for different quantities in different templates. Every use of a symbol that two or more templates share declares its meaning (`symbol:meaning`, in `GlossaryMeanings.json`), so two meanings of one quantity type (an efficiency and a Perry factor, both plain numbers) are told apart. Phases B and C resolve every one: give each quantity its own symbol (keeping the old symbol as an input or port alias), or make the templates agree. Status `RESOLVE` marks the rows in the full table below.

- `A_b`: `A_b:bearing-area` Area (lifting); `A_b:bore-area` Area (cylinder)
- `A_eff`: `A_eff:bellows-effective-area` Area (pipe-expansion); `A_eff:effective-bearing-area` Area (base-plate); `A_eff:effective-surface-area` Area (enclosure-heat)
- `C`: `C:basic-dynamic-load-rating` Force (bearing-life, bearing-life-basic); `C:fitting-loss-coefficient-sum` Plain number (duct-sizing); `C:spring-index` Plain number (spring)
- `D`: `D:diameter` Length (duct-sizing, pipe-pump, cylinder, iso286-fit, …); `D:enclosure-depth` Length (enclosure-heat)
- `F`: `F:force` Force (thermal-expansion, fillet-weld); `F:applied-axial-load` Force (column-buckling); `F:plate-load-total` Force (plate-bending); `F:point-load` Force (beam-formulas)
- `F_a`: `F_a:axial-load` Force (bearing-life, bearing-life-basic); `F_a:assembly-force` Force (press-fit)
- `F_ext`: `F_ext:extend-force` Force (cylinder); `F_ext:follower-external-load` Force (mechanism)
- `H`: `H:available-leg-length` Length (pipe-expansion); `H:enclosure-height` Length (enclosure-heat); `H:hardness` Plain number (oring-gland)
- `I`: `I:current` Current (fault-protection, pcb-trace, voltage-drop); `I:second-moment-of-area` Second moment of area (beam-formulas, column-buckling, frame-2d)
- `I_b`: `I_b:battery-discharge-current` Current (load-budget); `I_b:design-current` Current (conductor-sizing)
- `I_t`: `I_t:current-carrying-capacity` Current (conductor-sizing); `I_t:torsion-constant` Second moment of area (ltb-interaction)
- `I_z`: `I_z:second-moment-minor-axis` Second moment of area (en1993-member, ltb-interaction, section-properties); `I_z:current-carrying-capacity` Current (conductor-sizing, voltage-drop)
- `J`: `J:polar-second-moment-of-bolt-areas` Second moment of area (bolt-group); `J:torsion-constant` Second moment of area (section-properties)
- `K`: `K:fitting-loss-coefficient-sum` Plain number (pipe-pump); `K:nut-factor` Plain number (bolted-joint)
- `L2`: `L2:crank-length` Length (mechanism); `L2:second-run-length` Length (pipe-expansion)
- `L_req`: `L_req:required-life` Time, rating life (bearing-life, bearing-life-basic); `L_req:required-leg-length` Length (pipe-expansion)
- `N_Ed`: `N_Ed:axial-force-tension-positive` Force (en1993-member, ltb-interaction); `N_Ed:axial-force-compression-positive` Force (base-plate)
- `P`: `P:motor-rated-output-power` Power, heat flow (conductor-sizing, motor-start); `P:dissipated-power` Power, heat flow (thermal-chain); `P:electrical-input-power` Power, heat flow (load-budget); `P:heat-loss-per-item` Power, heat flow (enclosure-heat)
- `P_b`: `P_b:bearing-force` Force (lifting); `P_b:bellows-governing-pressure` Stress, pressure (pipe-expansion)
- `R`: `R:radius` Length (containment); `R:resistance` Resistance, impedance (voltage-drop); `R:thermal-resistance` Thermal resistance (thermal-chain)
- `R_f`: `R_f:fouling-resistance` Thermal resistance (heat-exchanger); `R_f:repetitions-to-failure` Plain number (fatigue-miner)
- `S`: `S:allowable-stress` Stress, pressure (thin-vessel); `S:stress-range` Stress, pressure (fatigue-miner)
- `SF`: `SF:sizing-safety-factor` Plain number (load-budget); `SF:torque-safety-factor` Plain number (motor-sizing)
- `S_e`: `S_e:bending-endurance-limit` Stress, pressure (fatigue-miner, shaft); `S_e:spring-shear-endurance-strength` Stress, pressure (spring)
- `T_1`: `T_1:side-1-temperature` Temperature (plane-wall, thermal-chain); `T_1:pull-start-tension` Force (containment)
- `T_2`: `T_2:side-2-temperature` Temperature (plane-wall, thermal-chain); `T_2:leaving-dry-bulb-temperature` Temperature (psychrometrics)
- `T_a`: `T_a:alternating-torque` Moment, torque (shaft); `T_a:ambient-temperature` Temperature (thermal-chain)
- `T_c`: `T_c:conductor-temperature` Temperature (motor-start, voltage-drop); `T_c:charging-temperature` Temperature (accumulator)
- `T_int`: `T_int:internal-design-temperature` Temperature (glaser, room-heat-loss); `T_int:enclosure-internal-temperature` Temperature (enclosure-heat)
- `T_m`: `T_m:mean-insulation-temperature` Temperature (insulation); `T_m:mean-water-temperature` Temperature (sealed-system)
- `T_max`: `T_max:maximum-temperature` Temperature (enclosure-heat, thermal-chain, accumulator, oring-gland); `T_max:maximum-pulling-tension` Force (containment); `T_max:maximum-system-temperature` Temperature (sealed-system)
- `T_rated`: `T_rated:rated-temperature` Temperature (voltage-drop); `T_rated:rated-torque` Moment, torque (motor-sizing)
- `U`: `U:supply-voltage` Voltage (conductor-sizing, motor-start); `U:share-of-revolutions` Plain number (bearing-life)
- `V`: `V:nominal-gas-volume` Volume, section modulus (accumulator); `V:room-volume` Volume, section modulus (room-heat-loss)
- `V_Ed`: `V_Ed:shear-force` Force (base-plate, en1993-member); `V_Ed:block-tearing-group-force` Force (connection-ec3)
- `W`: `W:width` Length (enclosure-heat, lifting-lug); `W:humidity-ratio` Plain number (psychrometrics); `W:ring-cross-section-diameter` Length (oring-gland)
- `X`: `X:radial-factor` Plain number (bearing-life, bearing-life-basic); `X:reactance` Resistance, impedance (voltage-drop)
- `a`: `a:weld-throat` Length (weld-group, connection-ec3, fillet-weld); `a:load-position` Length (beam-formulas, frame-2d); `a:duct-side` Length (duct-sizing); `a:edge-distance` Length (lifting-lug); `a:end-distance` Length (lifting); `a:inner-radius` Length (thick-cylinder); `a:plate-radius-or-side` Length (plate-bending); `a:prying-lever` Length (bolt-group)
- `alpha`: `alpha:temperature-coefficient-of-resistance` 1/K (fault-protection, motor-start, pcb-trace); `alpha:thermal-expansion-coefficient` 1/K (pipe-expansion, oring-gland, thermal-expansion); `alpha:concentration-factor` Plain number (base-plate); `alpha:entry-angle` Angle (snap-fit); `alpha:temperature-coefficient-of-resistance` Plain number (voltage-drop)
- `alpha_b`: `alpha_b:bearing-factor` Plain number (connection-ec3); `alpha_b:imperfection-factor-curve-b` Plain number (ltb-interaction)
- `b`: `b:width` Length (oring-gland, snap-fit, base-plate, en1993-member, …); `b:load-position` Length (frame-2d); `b:outer-radius` Length (thick-cylinder); `b:prying-lever` Length (bolt-group)
- `beta`: `beta:cam-angle` Angle (mechanism); `beta:reciprocal-temperature-coefficient` Temperature, temperature difference (fault-protection); `beta:sling-out-of-plane-angle` Angle (lifting)
- `c`: `c:bearing-outstand` Length (base-plate); `c:corrosion-allowance` Length (thin-vessel)
- `c_max`: `c_max:maximum-clearance` Length (iso286-fit); `c_max:voltage-factor-maximum-fault` Plain number (fault-protection)
- `d`: `d:diameter` Length (bolted-joint, press-fit, shaft, shaft-stress, …); `d:thickness` Length (glaser, room-heat-loss); `d:duty-share` Plain number (load-budget)
- `dV`: `dV:volume-change` Volume, section modulus (accumulator, cylinder); `dV:voltage-drop` Voltage (voltage-drop)
- `d_max`: `d_max:maximum-deflection` Length (frame-2d); `d_max:maximum-wire-diameter` Length (spring)
- `d_o`: `d_o:hub-outer-diameter` Length (press-fit); `d_o:orifice-diameter` Length (cylinder)
- `d_r`: `d_r:ring-inside-diameter` Length (oring-gland); `d_r:rod-diameter` Length (cylinder)
- `delta`: `delta:actual-movement` Length (thermal-expansion); `delta:diametral-interference` Length (press-fit)
- `duty`: `duty:duty-factor` Plain number (enclosure-heat); `duty:thermal-duty` Plain number (motor-sizing)
- `e_x`: `e_x:point-of-application-x` Length (bolt-group, weld-group); `e_x:expansion-coefficient` Plain number (sealed-system)
- `eps`: `eps:effectiveness` Plain number (heat-exchanger); `eps:emissivity` Plain number (insulation); `eps:roughness` Length (pipe-pump); `eps:strain` Plain number (snap-fit)
- `eta`: `eta:efficiency` Plain number (conductor-sizing, load-budget, motor-sizing, motor-start); `eta:perry-factor` Plain number (column-buckling); `eta:shear-area-factor` Plain number (en1993-member)
- `g`: `g:lug-clearance` Length (lifting-lug); `g:restraint-gap` Length (thermal-expansion)
- `h`: `h:depth` Length (base-plate, en1993-member, ltb-interaction, section-properties); `h:hole-to-weld-distance` Length (lifting); `h:key-height` Length (shaft); `h:lift` Length (mechanism); `h:root-thickness` Length (snap-fit)
- `h_1`: `h_1:side-1-film-coefficient` Heat transfer coefficient, U-value (plane-wall); `h_1:specific-enthalpy-1` Plain number (psychrometrics)
- `i`: `i:gear-ratio` Plain number (motor-sizing); `i:node-index` Plain number (frame-2d)
- `k`: `k:adiabatic-factor` Plain number (fault-protection); `k:diversity-factor` Plain number (load-budget); `k:effective-length-factor` Plain number (ltb-interaction); `k:half-band-standard-deviations` Plain number (tolerance-stack); `k:rate` Line force, distributed load, stiffness (spring); `k:thermal-conductivity` Thermal conductivity (plane-wall); `k:transmission-constant` Length (motor-sizing)
- `k_P`: `k_P:load-ratio` Plain number (bearing-life-basic); `k_P:members-stiffness` Line force, distributed load, stiffness (bolted-joint)
- `k_T`: `k_T:torque-factor-at-dipped-voltage` Plain number (motor-start); `k_T:transformer-overload-factor` Plain number (load-budget)
- `k_b`: `k_b:bearing-multiple` Plain number (lifting); `k_b:bellows-spring-rate` Line force, distributed load, stiffness (pipe-expansion)
- `k_c`: `k_c:f-modification-factor` Plain number (ltb-interaction); `k_c:stress-correction-factor` Plain number (spring)
- `lambda`: `lambda:thermal-conductivity` Thermal conductivity (glaser, insulation, room-heat-loss); `lambda:slenderness` Plain number (column-buckling)
- `m`: `m:mass` Mass (motor-sizing, shaft); `m:bolt-to-weld-toe-distance` Length (base-plate); `m:sn-slope` Plain number (fatigue-miner)
- `m_min`: `m_min:engagement-needed` Length (bolted-joint); `m_min:worst-case-margin-to-minimum` Length (tolerance-stack)
- `mu`: `mu:friction-coefficient` Plain number (pipe-expansion, containment, motor-sizing, press-fit, …); `mu:vapour-resistance-factor` Plain number (glaser)
- `n`: `n:operating-speed` Rotational speed, frequency (bearing-life, bearing-life-basic, shaft); `n:air-change-rate` Rotational speed, frequency (room-heat-loss); `n:cycles` Plain number (fatigue-miner); `n:emitter-exponent` Plain number (sealed-system)
- `n_L`: `n_L:load-factor-against-axial-yield` Plain number (bolted-joint); `n_L:load-speed-at-the-output` Rotational speed, frequency (motor-sizing)
- `n_t`: `n_t:holding-down-bolts-per-row` Plain number (base-plate); `n_t:total-coils` Plain number (spring)
- `p_1`: `p_1:lowest-working-pressure` Stress, pressure (accumulator); `p_1:pitch-load-direction` Length (connection-ec3)
- `p_2`: `p_2:gauge` Length (connection-ec3); `p_2:highest-working-pressure` Stress, pressure (accumulator)
- `p_i`: `p_i:internal-pressure` Stress, pressure (thick-cylinder); `p_i:pre-charge-pressure` Stress, pressure (sealed-system)
- `p_sys`: `p_sys:supply-pressure` Stress, pressure (cylinder); `p_sys:system-resistance` Stress, pressure (enclosure-heat)
- `phi`: `phi:area-ratio` Plain number (cylinder); `phi:shear-plane-angle` Angle (lifting)
- `phi_1`: `phi_1:angle-at-point-1` Angle (spring); `phi_1:relative-humidity-1` Percent quantity (psychrometrics)
- `phi_max`: `phi_max:maximum-pressure-angle` Angle (mechanism); `phi_max:strain-shape-maximum` Plain number (snap-fit)
- `q`: `q:heat-flux` Heat flux (plane-wall); `q:notch-sensitivity` Plain number (shaft); `q:pressure-on-loaded-area` Stress, pressure (plate-bending)
- `r`: `r:root-radius` Length (en1993-member, section-properties); `r:radius-of-gyration` Length (column-buckling)
- `rho`: `rho:material-density` Density (shaft, spring, plate-bending); `rho:fluid-density` Density (cylinder)
- `s`: `s:fillet-leg` Length (weld-group); `s:move-distance` Plain number (motor-sizing)
- `sigma`: `sigma:stress` Stress, pressure (material-margin, shaft-stress, thermal-expansion); `sigma:standard-deviation` Length (oring-gland, tolerance-stack)
- `t`: `t:thickness` Length (plane-wall); `t:time` Time (fault-protection)
- `t_req`: `t_req:required-thickness` Length (thin-vessel, base-plate); `t_req:required-disconnection-time` Time (fault-protection)
- `t_w`: `t_w:web-thickness` Length (base-plate, en1993-member, section-properties); `t_w:pipe-wall-thickness` Length (insulation)
- `tau`: `tau:throat-stress` Stress, pressure (weld-group); `tau:torsional-shear-stress` Stress, pressure (shaft-stress)
- `theta`: `theta:bend-angle` Angle (containment); `theta:incline-angle` Angle (motor-sizing); `theta:principal-axis-angle` Angle (section-properties); `theta:sling-angle` Angle (lifting)
- `u`: `u:utilisation` Plain number (thermal-chain, material-margin, thermal-expansion, thick-cylinder, …); `u:patch-size-x` Length (plate-bending)
- `u_N`: `u_N:axial-bearing-utilisation` Plain number (base-plate); `u_N:axial-utilisation` Plain number (en1993-member)
- `u_T`: `u_T:cable-tension-utilisation` Plain number (containment); `u_T:t-stub-tension-utilisation` Plain number (base-plate)
- `u_b`: `u_b:bearing-utilisation` Plain number (connection-ec3, lifting); `u_b:buckling-deflection-ratio` Plain number (spring); `u_b:pin-bending-utilisation` Plain number (lifting-lug)
- `u_f`: `u_f:goodman-utilisation` Plain number (shaft, spring); `u_f:surface-factor-utilisation` Plain number (glaser)
- `u_n`: `u_n:minimum-length-travel-utilisation` Plain number (spring); `u_n:safety-factor-utilisation` Plain number (shaft-stress)
- `w`: `w:distributed-load` Line force, distributed load, stiffness (beam-formulas); `w:key-width` Length (shaft); `w:mass-per-metre` Mass per length (containment)
- `y_c`: `y_c:centroid-y` Length (weld-group, section-properties); `y_c:extreme-fibre-distance` Length (beam-formulas)

## All symbols (1447 symbols in 54 templates)

One row per symbol, declared meaning and quantity type. Status `ok` is a symbol with one meaning; `RESOLVE` a symbol with several (phases B and C).

| Symbol | Meaning | Quantity type | Units | Descriptions in the templates | Templates | Status |
|---|---|---|---|---|---|---|
| `A` | `A:area` | Area | cm², mm², m² | Area; Area A; Cross-section area; … (6 descriptions) | bs.heat-exchanger, bs.plane-wall, mech.bolt-group, mech.thermal-expansion, struct.column-buckling, struct.en1993-member, struct.frame-2d, struct.ltb-interaction, struct.section-properties | ok |
| `AWG` | — | Plain number | — | AWG gauge number n (0 = 1/0, −1 = 2/0, −2 = 3/0, −3 = 4/0) | elec.conductor-sizing | ok |
| `A_a` | — | Area | cm² | Annulus area | fp.cylinder | ok |
| `A_b` | `A_b:bearing-area` | Area | mm² | Bearing area | struct.lifting | RESOLVE (2 meanings) |
| `A_b` | `A_b:bore-area` | Area | cm² | Bore area | fp.cylinder | RESOLVE (2 meanings) |
| `A_beam` | — | Area | cm² | Gross cross-section area of the spreader or lifting beam, for its axi… | struct.lifting | ok |
| `A_c` | `A_c:conductor-cross-section` | Area | mm² | Conductor cross-section; Conductor cross-section (links conductor-sizing CSA) | elec.motor-start, elec.voltage-drop | ok |
| `A_cab` | — | Area | mm² | Total cable cross-section | elec.containment | ok |
| `A_cover` | — | Area | m² | Area of the cover or door the pressure difference acts on, for the fo… | elec.enclosure-heat | ok |
| `A_eff` | `A_eff:bellows-effective-area` | Area | cm² | Axial bellows effective area (manufacturer's data) | bs.pipe-expansion | RESOLVE (3 meanings) |
| `A_eff` | `A_eff:effective-bearing-area` | Area | mm² | Effective bearing area | struct.base-plate | RESOLVE (3 meanings) |
| `A_eff` | `A_eff:effective-surface-area` | Area | m² | Effective surface | elec.enclosure-heat | RESOLVE (3 meanings) |
| `A_ext` | — | Area | m² | Fabric area facing outside | bs.room-heat-loss | ok |
| `A_fab` | — | Area | m² | Total fabric area | bs.room-heat-loss | ok |
| `A_flange` | — | Area | mm² | Compression-side bearing area | struct.base-plate | ok |
| `A_floor` | — | Area | m² | Floor area, for the loss per floor area (optional; blank = no loss pe… | bs.room-heat-loss | ok |
| `A_fr` | — | Area | mm² | Fracture area beyond the hole | struct.lifting | ok |
| `A_int` | — | Area | mm² | Internal cross-section | elec.containment | ok |
| `A_m` | — | Area | mm² | Metal cross-section | bs.pipe-expansion | ok |
| `A_net` | — | Area | cm² | Net area of a tension member (optional; blank = the sheet is blocked … | struct.en1993-member | ok |
| `A_nt` | — | Area | mm² | Net area in tension (optional; blank = no block tearing check) | struct.connection-ec3 | ok |
| `A_nv` | — | Area | mm² | Net area in shear (optional; blank = no block tearing check) | struct.connection-ec3 | ok |
| `A_r` | — | Area | cm² | Rod area | fp.cylinder | ok |
| `A_req` | — | Area | mil² | Minimum cross-section | elec.pcb-trace | ok |
| `A_s` | `A_s:stress-area` | Area | mm² | Anchor bolt tensile stress area (from the connection sheet); Stress area; Tensile stress area | mech.bolted-joint, struct.base-plate, struct.connection-ec3 | ok |
| `A_s_given` | — | Area | mm² | Tensile stress area, from a fastener record (optional; blank = from d… | mech.bolted-joint | ok |
| `A_sun` | — | Area | m² | Sunlit area | elec.enclosure-heat | ok |
| `A_t` | — | Area | mm² | Tension area beyond the hole | struct.lifting | ok |
| `A_tb` | — | Area | m² | Fabric area facing outside weighted by f | bs.room-heat-loss | ok |
| `A_v` | `A_v:shear-area` | Area | cm², mm² | Shear area; Shear area (optional; blank = rolled I-section from h, b, t_w, t_f an… | struct.en1993-member, struct.lifting | ok |
| `Ah` | — | Charge | A·h | Discharge capacity | elec.load-budget | ok |
| `B` | — | Length | mm | Plate width | struct.base-plate | ok |
| `B_p_Rd` | — | Force | kN | Punching resistance | struct.connection-ec3 | ok |
| `C` | `C:basic-dynamic-load-rating` | Force | kN | Basic dynamic load rating | mech.bearing-life, mech.bearing-life-basic | RESOLVE (3 meanings) |
| `C` | `C:fitting-loss-coefficient-sum` | Plain number | — | Σ fitting loss coefficients | bs.duct-sizing | RESOLVE (3 meanings) |
| `C` | `C:spring-index` | Plain number | — | Spring index | mech.spring | RESOLVE (3 meanings) |
| `CSA` | — | Area | mm² | Cross-section (metric); Selected cross-section | elec.conductor-sizing | ok |
| `C_0` | — | Force | kN | Basic static load rating | mech.bearing-life | ok |
| `C_1` | — | Plain number | — | C₁; Moment diagram factor C₁ (optional; blank = derived from ψ, valid for… | struct.ltb-interaction | ok |
| `C_2` | — | Plain number | — | Moment diagram factor C₂ (0 for end moments; enter it for a transvers… | struct.ltb-interaction | ok |
| `C_Ed` | — | Force | kN | Compression resultant | struct.base-plate | ok |
| `C_Ed_2` | — | Force | kN | Compression resultant, other side | struct.base-plate | ok |
| `C_Rd` | — | Force | kN | Compression-side resistance | struct.base-plate | ok |
| `C_a` | — | Plain number | — | Ambient correction factor (1 does not apply the ambient: enter the fa…; Ambient factor | elec.conductor-sizing | ok |
| `C_batt` | — | Charge | A·h | Selected battery capacity | elec.load-budget | ok |
| `C_c` | — | Plain number | — | Buried or ducted installation factor; Buried or ducted installation factor, at most 1 (1 does not apply it:… | elec.conductor-sizing | ok |
| `C_d` | — | Plain number | — | Orifice discharge coefficient | fp.cylinder | ok |
| `C_f` | — | Plain number | — | Semi-enclosed fuse factor | elec.conductor-sizing | ok |
| `C_fd` | — | Plain number | — | Friction coefficient, plate to grout | struct.base-plate | ok |
| `C_g` | — | Plain number | — | Grouping correction factor (at most 1) | elec.conductor-sizing | ok |
| `C_i` | — | Plain number | — | Thermal insulation correction factor (at most 1) | elec.conductor-sizing | ok |
| `C_mLT` | — | Plain number | — | Equivalent uniform moment factor for lateral-torsional buckling | struct.ltb-interaction | ok |
| `C_m_LT` | — | Plain number | — | Equivalent uniform moment factor for lateral-torsional buckling in An… | struct.ltb-interaction | ok |
| `C_m_y` | — | Plain number | — | Equivalent uniform moment factor about y-y in Annex B (optional; blan… | struct.ltb-interaction | ok |
| `C_m_z` | — | Plain number | — | Equivalent uniform moment factor about z-z in Annex B (optional; blan… | struct.ltb-interaction | ok |
| `C_min` | — | Plain number | — | Voltage factor for the minimum fault | elec.fault-protection | ok |
| `C_my` | — | Plain number | — | Equivalent uniform moment factor about y-y | struct.ltb-interaction | ok |
| `C_mz` | — | Plain number | — | Equivalent uniform moment factor about z-z | struct.ltb-interaction | ok |
| `C_r` | — | Plain number | — | Capacity ratio | bs.heat-exchanger | ok |
| `C_req` | — | Charge | A·h | Minimum battery capacity | elec.load-budget | ok |
| `Cr` | — | Plain number | — | Pin-clearance factor C_r | struct.lifting | ok |
| `Cu_oz` | — | Plain number | — | Finished copper weight in oz/ft² (0.5 to 6), not a thickness in µm: 1… | elec.pcb-trace | ok |
| `D` | `D:diameter` | Length | mm | Bore; Given diameter or side b (blank = size); Inner diameter; … (6 descriptions) | bs.duct-sizing, bs.pipe-pump, fp.cylinder, mech.iso286-fit, mech.spring, struct.base-plate | RESOLVE (2 meanings) |
| `D` | `D:enclosure-depth` | Length | m | Enclosure depth | elec.enclosure-heat | RESOLVE (2 meanings) |
| `D_bth` | — | Force | kN | Design load on each lug | struct.lifting | ok |
| `D_c` | — | Length | mm | Mating part that sets the extrusion gap: the piston outside diameter,… | fp.oring-gland | ok |
| `D_c_l` | — | Length | mm | Mating part diameter, lower deviation | fp.oring-gland | ok |
| `D_c_u` | — | Length | mm | Mating part diameter, upper deviation | fp.oring-gland | ok |
| `D_crit` | — | Length | mm | Critical diameter (at the bare-surface h_se) | bs.insulation | ok |
| `D_flex` | — | Moment, torque | kN·m | Flexural rigidity | struct.plate-bending | ok |
| `D_g` | — | Length | mm | Groove root diameter: on the piston, in the housing of a rod seal, or… | fp.oring-gland | ok |
| `D_g_l` | — | Length | mm | Groove root diameter, lower deviation | fp.oring-gland | ok |
| `D_g_u` | — | Length | mm | Groove root diameter, upper deviation | fp.oring-gland | ok |
| `D_hole_max` | — | Length | mm | Hole maximum | mech.iso286-fit | ok |
| `D_hole_min` | — | Length | mm | Hole minimum | mech.iso286-fit | ok |
| `D_i` | `D_i:inside-diameter` | Length | mm | Inside diameter; Pipe outer diameter, the inside of the insulation (pipe) | bs.insulation, bs.pipe-expansion | ok |
| `D_int` | — | Length | mm | Conduit internal diameter (conduit) | elec.containment | ok |
| `D_pulley` | — | Length | mm | Pulley or pinion pitch diameter | elec.motor-sizing | ok |
| `D_ratio` | — | Plain number | — | Impeller diameter ratio D₂/D₁ (affinity; the law is chosen above) | bs.pipe-pump | ok |
| `D_ring` | — | Length | mm | Mean diameter of the ring (annular) | mech.snap-fit | ok |
| `D_s` | — | Length | mm | Sealing-surface diameter: the bore of a piston seal, the rod of a rod… | fp.oring-gland | ok |
| `D_s1` | — | Length | mm | D, main | bs.duct-sizing | ok |
| `D_s_l` | — | Length | mm | Sealing-surface diameter, lower deviation | fp.oring-gland | ok |
| `D_s_u` | — | Length | mm | Sealing-surface diameter, upper deviation | fp.oring-gland | ok |
| `D_tot` | — | Plain number | — | Total damage | mech.fatigue-miner | ok |
| `Delta` | — | Length | mm | Movement absorbed by each leg | bs.pipe-expansion | ok |
| `Delta_run` | — | Length | mm | Leg growth absorbed by the run or runs | bs.pipe-expansion | ok |
| `Dh` | — | Length | mm | Hole diameter | struct.lifting | ok |
| `DoD` | — | Plain number | — | Allowed depth of discharge | elec.load-budget | ok |
| `Dp` | — | Length | mm | Pin diameter (not larger than the hole) | struct.lifting | ok |
| `E` | `E:youngs-modulus` | Stress, pressure | GPa | Default Young's modulus; Young's modulus; Young's modulus (blank = sheet default); … (6 descriptions) | bs.pipe-expansion, fp.cylinder, mech.shaft, mech.spring, mech.thermal-expansion, mech.thick-cylinder, struct.beam-formulas, struct.column-buckling, struct.en1993-member, struct.frame-2d, struct.ltb-interaction, struct.plate-bending | ok |
| `EI_hole` | — | Length | µm | Hole lower deviation | mech.iso286-fit | ok |
| `EI_hole_override` | — | Length | µm | Hole lower deviation override (optional; blank = the formula value) | mech.iso286-fit | ok |
| `ES_hole` | — | Length | µm | Hole upper deviation | mech.iso286-fit | ok |
| `ES_hole_override` | — | Length | µm | Hole upper deviation override (optional; blank = the formula value, o… | mech.iso286-fit | ok |
| `E_P` | — | Stress, pressure | GPa | Clamped members' Young's modulus | mech.bolted-joint | ok |
| `E_S` | — | Stress, pressure | GPa | Bolt Young's modulus | mech.bolted-joint | ok |
| `E_d_max` | — | Force | kN | ULS design effect, maximum | struct.load-combinations | ok |
| `E_d_min` | — | Force | kN | ULS design effect, minimum | struct.load-combinations | ok |
| `E_fr_max` | — | Force | kN | SLS frequent effect, maximum | struct.load-combinations | ok |
| `E_fr_min` | — | Force | kN | SLS frequent effect, minimum | struct.load-combinations | ok |
| `E_h` | — | Stress, pressure | GPa | Hub Young's modulus | mech.press-fit | ok |
| `E_hot` | — | Stress, pressure | GPa | Young's modulus at the operating temperature, for the reactions on th… | bs.pipe-expansion | ok |
| `E_j` | — | Plain number | — | Joint efficiency | mech.thin-vessel | ok |
| `E_k_max` | — | Force | kN | SLS characteristic effect, maximum | struct.load-combinations | ok |
| `E_k_min` | — | Force | kN | SLS characteristic effect, minimum | struct.load-combinations | ok |
| `E_qp_max` | — | Force | kN | SLS quasi-permanent effect, maximum | struct.load-combinations | ok |
| `E_qp_min` | — | Force | kN | SLS quasi-permanent effect, minimum | struct.load-combinations | ok |
| `E_s` | — | Stress, pressure | GPa | Shaft Young's modulus | mech.press-fit | ok |
| `Es` | — | Stress, pressure | MPa | Secant modulus; Secant modulus at the design strain and temperature (annular: at the …; Secant modulus used | mech.snap-fit | ok |
| `F` | `F:force` | Force | kN | Restraint force; Resultant force | mech.thermal-expansion, struct.fillet-weld | RESOLVE (4 meanings) |
| `F` | `F:applied-axial-load` | Force | kN | Applied axial load | struct.column-buckling | RESOLVE (4 meanings) |
| `F` | `F:plate-load-total` | Force | kN | Total force (force basis: uniform, patch and ring loads; a ring load … | struct.plate-bending | RESOLVE (4 meanings) |
| `F` | `F:point-load` | Force | kN | Point load, downward (optional; blank = not used by the distributed-l… | struct.beam-formulas | RESOLVE (4 meanings) |
| `F_0` | — | Force | N | Initial tension (extension) | mech.spring | ok |
| `F_1` | — | Force | N | Force at point 1; Force at working point 1 (loads) | mech.spring | ok |
| `F_1_vb_Rd` | — | Force | kN | Bolt bearing resistance on the plate (from the bearing resistance of … | struct.base-plate | ok |
| `F_2` | — | Force | N | Force at point 2; Force at working point 2 (loads) | mech.spring | ok |
| `F_2_vb_Rd` | — | Force | kN | Anchor shear resistance F_2,vb,Rd | struct.base-plate | ok |
| `F_A` | — | Force | kN | Axial working load (per bolt) | mech.bolted-joint | ok |
| `F_A_sep` | — | Force | kN | Separation load | mech.bolted-joint | ok |
| `F_E` | — | Force | kN | Euler critical load | struct.column-buckling | ok |
| `F_KR` | — | Force | kN | Residual clamp | mech.bolted-joint | ok |
| `F_KR_req` | — | Force | kN | Required residual clamp force | mech.bolted-joint | ok |
| `F_M` | — | Force | kN | Assembly preload (maximum) | mech.bolted-joint | ok |
| `F_M_min` | — | Force | kN | Minimum preload | mech.bolted-joint | ok |
| `F_SA` | — | Force | kN | Additional bolt load | mech.bolted-joint | ok |
| `F_S_max` | — | Force | kN | Maximum bolt load | mech.bolted-joint | ok |
| `F_V` | — | Force | kN | Preload (optional; blank = the preload comes from the tightening torq… | mech.bolted-joint | ok |
| `F_Z` | — | Force | kN | Embedding loss | mech.bolted-joint | ok |
| `F_a` | `F_a:axial-load` | Force | kN | Axial load | mech.bearing-life, mech.bearing-life-basic | RESOLVE (2 meanings) |
| `F_a` | `F_a:assembly-force` | Force | kN | Assembly force | mech.press-fit | RESOLVE (2 meanings) |
| `F_allow` | — | Force | kN | Allowable push load (screening) | fp.cylinder | ok |
| `F_along_leg` | — | Force | kN | Anchor force component along the leg | bs.pipe-expansion | ok |
| `F_along_run` | — | Force | kN | Anchor force component along the run | bs.pipe-expansion | ok |
| `F_ax_pk` | — | Force | N | Peak axial thrust on a screw's bearings | elec.motor-sizing | ok |
| `F_b_Rd` | — | Force | kN | Bearing resistance | struct.connection-ec3 | ok |
| `F_buck` | — | Force | kN | Rod load judged for buckling: the larger of the push load and the sta… | fp.cylinder | ok |
| `F_c` | — | Force | N | Force at solid | mech.spring | ok |
| `F_c_Rd` | — | Force | kN | Compression resistance | struct.column-buckling | ok |
| `F_corr` | — | Plain number | — | LMTD correction factor | bs.heat-exchanger | ok |
| `F_cr` | — | Force | kN | Critical load (screening) | fp.cylinder | ok |
| `F_defl` | — | Force | N | Deflection force | mech.snap-fit | ok |
| `F_dyn` | `F_dyn:equivalent-dynamic-load` | Force | kN | Equivalent dynamic load | mech.bearing-life, mech.bearing-life-basic | ok |
| `F_ext` | `F_ext:extend-force` | Force | kN | Extend force | fp.cylinder | RESOLVE (2 meanings) |
| `F_ext` | `F_ext:follower-external-load` | Force | N | External load on the follower, positive against the rise | mech.mechanism | RESOLVE (2 meanings) |
| `F_f_Rd` | — | Force | kN | Friction resistance | struct.base-plate | ok |
| `F_friction` | — | Force | kN | Support friction on the run | bs.pipe-expansion | ok |
| `F_ip` | — | Force | kN | In-plane component | struct.lifting | ok |
| `F_k` | — | Plain number | — | Characteristic value (signed, in the effect unit) | struct.load-combinations | ok |
| `F_leg` | — | Force | kN | Force of the leg | bs.pipe-expansion | ok |
| `F_load` | — | Force | N | External load on the slider (slider-crank). Positive acts in the −x d… | mech.mechanism | ok |
| `F_lug` | — | Force | kN | Load on the lug (along the lug) | mech.lifting-lug | ok |
| `F_n` | — | Force | kN | Force normal to the plane of the parts | struct.fillet-weld | ok |
| `F_op` | — | Force | kN | Out-of-plane component | struct.lifting | ok |
| `F_p` | — | Force | N | Process force opposing the move (linear) | elec.motor-sizing | ok |
| `F_par` | — | Force | kN | Force parallel to the weld axis | struct.fillet-weld | ok |
| `F_pin_pk` | — | Force | N | Peak force in the coupler, which is also the force at the crank pin a… | mech.mechanism | ok |
| `F_pre` | — | Force | N | Spring preload at the base circle | mech.mechanism | ok |
| `F_pull` | — | Force | kN | Load to pull on the retract stroke | fp.cylinder | ok |
| `F_push` | — | Force | kN | Load to push on the extend stroke, which compresses the rod | fp.cylinder | ok |
| `F_r` | `F_r:radial-load` | Force | kN | Radial load | mech.bearing-life, mech.bearing-life-basic | ok |
| `F_req` | — | Force | N | Force the latches must hold against, for example the pressure on a co… | mech.snap-fit | ok |
| `F_res` | — | Force | kN | Fully restrained force | bs.pipe-expansion | ok |
| `F_ret` | — | Force | kN | Retract force | fp.cylinder | ok |
| `F_run` | — | Force | kN | Force of the runs | bs.pipe-expansion | ok |
| `F_s` | — | Force | kN | Sling force on each lug | struct.lifting | ok |
| `F_stall` | — | Force | kN | Stall thrust p_sys·A_b | fp.cylinder | ok |
| `F_t_Ed` | `F_t_Ed:bolt-tension-per-bolt` | Force | kN | Bolt tension per bolt (to PROFIS); Tension per bolt | struct.base-plate, struct.connection-ec3 | ok |
| `F_t_Ed_max` | — | Force | kN | Maximum tension on any bolt | mech.bolt-group | ok |
| `F_t_Rd` | `F_t_Rd:bolt-tension-resistance` | Force | kN | Tension resistance; Tension resistance per bolt; Tension resistance per bolt (from the connection sheet) | mech.bolt-group, struct.base-plate, struct.connection-ec3 | ok |
| `F_tr` | — | Force | kN | Force transverse to the weld axis, in the plane of the parts | struct.fillet-weld | ok |
| `F_v_Ed` | — | Force | kN | Shear per bolt | struct.connection-ec3 | ok |
| `F_v_Ed_max` | — | Force | kN | Maximum shear on any bolt | mech.bolt-group | ok |
| `F_v_Rd` | `F_v_Rd:bolt-shear-resistance` | Force | kN | Shear resistance; Shear resistance per bolt | mech.bolt-group, struct.connection-ec3 | ok |
| `F_vb_Rd` | — | Force | kN | Anchor shear resistance per bolt | struct.base-plate | ok |
| `F_w_Ed` | — | Line force, distributed load, stiffness | kN/m | Weld force per unit length (optional; blank = no weld check) | struct.connection-ec3 | ok |
| `F_w_Rd` | — | Force | kN | Weld resistance | struct.fillet-weld | ok |
| `F_x` | `F_x:force-x` | Force | kN | F_x (global); In-plane force, x | mech.bolt-group, mech.weld-group, struct.frame-2d | ok |
| `F_y` | `F_y:force-y` | Force | kN | F_y (global); Force, vertical plane (up +); In-plane force, y | mech.bolt-group, mech.shaft, mech.weld-group, struct.frame-2d | ok |
| `F_z` | `F_z:force-z` | Force | kN | Force, horizontal plane; Out-of-plane force (tension positive) | mech.shaft, mech.weld-group | ok |
| `Fa` | — | Stress, pressure | MPa | Allowable stress | struct.lifting | ok |
| `G` | `G:shear-modulus` | Stress, pressure | GPa | Shear modulus | mech.spring, struct.ltb-interaction | ok |
| `H` | `H:available-leg-length` | Length | m | Available leg length (each leg of a U-loop; an offset leg: the leg of… | bs.pipe-expansion | RESOLVE (3 meanings) |
| `H` | `H:enclosure-height` | Length | m | Enclosure height | elec.enclosure-heat | RESOLVE (3 meanings) |
| `H` | `H:hardness` | Plain number | — | Hardness, Shore A | fp.oring-gland | RESOLVE (3 meanings) |
| `HLC` | — | Thermal conductance | W/K | Heat loss coefficient | bs.room-heat-loss | ok |
| `H_A` | — | Plain number | — | Ring hardness, Shore A (for the extrusion record) | fp.oring-gland | ok |
| `H_int` | — | Length | mm | Trunking or tray internal depth | elec.containment | ok |
| `H_pump` | — | Length | m | Pump curve: head points (optional; blank = no duty point) | bs.pipe-pump | ok |
| `H_rate` | — | Time, rating life | h | Hour rate of the rated capacity (Peukert) | elec.load-budget | ok |
| `H_static` | — | Length | m | Static head | bs.pipe-pump | ok |
| `H_sys` | — | Length | m | System head at Q | bs.pipe-pump | ok |
| `I` | `I:current` | Current | A | Current; Design current; Trace current (the curve-fit was drawn for up to 35 A: above it the s… | elec.fault-protection, elec.pcb-trace, elec.voltage-drop | RESOLVE (2 meanings) |
| `I` | `I:second-moment-of-area` | Second moment of area | cm⁴ | Second moment of area; Second moment of area about the section y-y axis; Second moment of area about the weaker axis | struct.beam-formulas, struct.column-buckling, struct.frame-2d | RESOLVE (2 meanings) |
| `I2` | — | Current | A | Conventional operating current; Conventional operating current I₂ (gG fuse, MCCB, MPCB: required in t… | elec.conductor-sizing | ok |
| `I2t_let` | — | s·A² | A²·s | Device let-through energy I²t at the maximum prospective fault (disco… | elec.fault-protection | ok |
| `ID` | — | Length | mm | Inside diameter | fp.cylinder | ok |
| `IT_hole` | — | Length | µm | Hole tolerance | mech.iso286-fit | ok |
| `IT_shaft` | — | Length | µm | Shaft tolerance | mech.iso286-fit | ok |
| `I_1` | — | Second moment of area | cm⁴ | Major principal second moment | struct.section-properties | ok |
| `I_1ph` | — | Current | A | Single-phase bus current | elec.load-budget | ok |
| `I_2` | — | Second moment of area | cm⁴ | Minor principal second moment | struct.section-properties | ok |
| `I_3ph` | — | Current | A | Three-phase bus current | elec.load-budget | ok |
| `I_DC` | — | Current | A | DC bus current | elec.load-budget | ok |
| `I_LR_r` | — | Plain number | — | Locked-rotor current ratio I_LR/I_n | elec.motor-start | ok |
| `I_a` | — | Current | A | Disconnection current | elec.fault-protection | ok |
| `I_b` | `I_b:battery-discharge-current` | Current | A | Battery discharge current | elec.load-budget | RESOLVE (2 meanings) |
| `I_b` | `I_b:design-current` | Current | A | Design current | elec.conductor-sizing | RESOLVE (2 meanings) |
| `I_b_in` | — | Current | A | Design current from another calculation (replaces the computed I_b wh… | elec.conductor-sizing | ok |
| `I_cap` | — | Current | A | Capacitor current | elec.motor-start | ok |
| `I_cn` | — | Current | kA | Rated breaking capacity | elec.fault-protection | ok |
| `I_dip` | — | Current | A | Starting current at the dipped voltage | elec.motor-start | ok |
| `I_f` | — | Current | A | Earth fault current | elec.fault-protection | ok |
| `I_k` | — | Current | kA | Three-phase fault current at the point of supply (links fault-protect… | elec.motor-start | ok |
| `I_n` | `I_n:rated-current` | Current | A | Device rated current; Motor rated current (when given it replaces the value from P, η and c…; Rated current; … (4 descriptions) | elec.conductor-sizing, elec.fault-protection, elec.motor-start | ok |
| `I_rated` | — | Current | A | Motor rated current | elec.motor-start | ok |
| `I_run1` | — | Current | A | Running current before correction | elec.motor-start | ok |
| `I_run2` | — | Current | A | Running current after correction | elec.motor-start | ok |
| `I_start` | — | Current | A | Starting current at rated voltage | elec.motor-start | ok |
| `I_sun` | — | Heat flux | W/m² | Solar irradiance on the enclosure (0 = indoors) | elec.enclosure-heat | ok |
| `I_t` | `I_t:current-carrying-capacity` | Current | A | Tabulated current-carrying capacity | elec.conductor-sizing | RESOLVE (2 meanings) |
| `I_t` | `I_t:torsion-constant` | Second moment of area | cm⁴ | Torsion constant | struct.ltb-interaction | RESOLVE (2 meanings) |
| `I_t_req` | — | Current | A | Required tabulated capacity | elec.conductor-sizing | ok |
| `I_ux` | — | Volume, section modulus | mm³ | Unit second moment about x | mech.weld-group | ok |
| `I_uxy` | — | Volume, section modulus | mm³ | Unit product moment | mech.weld-group | ok |
| `I_uy` | — | Volume, section modulus | mm³ | Unit second moment about y | mech.weld-group | ok |
| `I_w` | `I_w:warping-constant` | Warping constant | cm⁶ | Warping constant | struct.ltb-interaction, struct.section-properties | ok |
| `I_x` | — | Second moment of area | cm⁴ | Second moment of area | bs.pipe-expansion | ok |
| `I_y` | `I_y:second-moment-major-axis` | Second moment of area | cm⁴ | Second moment of area about the y-y axis; Second moment of area about y-y; Second moment of area about y-y (major axis) | struct.en1993-member, struct.ltb-interaction, struct.section-properties | ok |
| `I_yz` | — | Second moment of area | cm⁴ | Product of inertia about the y-y and z-z axes | struct.section-properties | ok |
| `I_z` | `I_z:second-moment-minor-axis` | Second moment of area | cm⁴ | Second moment of area about the z-z axis; Second moment of area about z-z; Second moment of area about z-z (minor axis) | struct.en1993-member, struct.ltb-interaction, struct.section-properties | RESOLVE (2 meanings) |
| `I_z` | `I_z:current-carrying-capacity` | Current | A | Current-carrying capacity; Tabulated current-carrying capacity for the installed conditions (tem… | elec.conductor-sizing, elec.voltage-drop | RESOLVE (2 meanings) |
| `J` | `J:polar-second-moment-of-bolt-areas` | Second moment of area | mm⁴ | Polar second moment of bolt areas | mech.bolt-group | RESOLVE (2 meanings) |
| `J` | `J:torsion-constant` | Second moment of area | cm⁴ | Torsion constant | struct.section-properties | RESOLVE (2 meanings) |
| `J_L` | — | Moment of inertia (mass) | kg·m² | Load inertia at the output (rotary mechanisms) | elec.motor-sizing | ok |
| `J_mech` | — | Moment of inertia (mass) | kg·m² | Transmission inertia referred to the motor shaft (screw, pulleys, gea… | elec.motor-sizing | ok |
| `J_motor` | — | Moment of inertia (mass) | kg·m² | Candidate motor rotor inertia | elec.motor-sizing | ok |
| `J_out` | — | Moment of inertia (mass) | kg·m² | Moment of inertia of the output link about its pivot (four-bar) | mech.mechanism | ok |
| `J_ratio` | — | Plain number | — | Inertia ratio | elec.motor-sizing | ok |
| `J_ratio_max` | — | Plain number | — | Inertia ratio limit (load : motor), warning | elec.motor-sizing | ok |
| `J_refl` | — | Moment of inertia (mass) | kg·m² | Reflected load inertia | elec.motor-sizing | ok |
| `J_total` | — | Moment of inertia (mass) | kg·m² | Total inertia | elec.motor-sizing | ok |
| `J_u` | — | Volume, section modulus | mm³ | Unit polar moment | mech.weld-group | ok |
| `K` | `K:fitting-loss-coefficient-sum` | Plain number | — | Σ K of fittings | bs.pipe-pump | RESOLVE (2 meanings) |
| `K` | `K:nut-factor` | Plain number | — | Nut factor (nut-factor method); F_M is the maximum preload, so enter … | mech.bolted-joint | RESOLVE (2 meanings) |
| `K_F` | — | Plain number | — | Force factor of the annular snap on the hoop force (optional; blank =… | mech.snap-fit | ok |
| `K_FI` | — | Plain number | — | Consequence-class factor | struct.load-combinations | ok |
| `K_ann` | — | Plain number | — | Strain factor of the annular snap on the nominal hoop strain ΔD/D (re… | mech.snap-fit | ok |
| `K_m` | — | Plain number | — | Effective-length factor K of the mounting (L_eff = K·L) | fp.cylinder | ok |
| `K_scr` | — | Angle | mm²/m² | Screening limit K1 for OD·Y/(L − U)², in mm²/m² (or in²/ft², …) from … | bs.pipe-expansion | ok |
| `K_t` | `K_t:bending-stress-concentration` | Plain number | — | Bending stress-concentration factor (1 for none); K_t, bending | mech.shaft, mech.shaft-stress | ok |
| `K_ts` | `K_ts:torsion-stress-concentration` | Plain number | — | K_ts, torsion; Torsional stress-concentration factor (1 for none) | mech.shaft, mech.shaft-stress | ok |
| `K_y` | — | Plain number | — | Deflection factor of the cantilever | mech.snap-fit | ok |
| `L` | `L:length` | Length | m, mm | Anchor spacing: the straight length that grows (an offset leg or Z-be…; Bar length; Circuit length; … (13 descriptions) | bs.duct-sizing, bs.pipe-expansion, bs.pipe-pump, elec.conductor-sizing, elec.containment, elec.fault-protection, elec.pcb-trace, elec.voltage-drop, mech.press-fit, mech.thermal-expansion, struct.base-plate, struct.beam-formulas, struct.fillet-weld, struct.ltb-interaction | ok |
| `L0` | — | Length | mm | Free length (compression) | mech.spring | ok |
| `L1` | — | Length | mm | Ground link d, O₂ to O₄ (four-bar) | mech.mechanism | ok |
| `L2` | `L2:crank-length` | Length | mm | Crank length a (four-bar) or r (slider-crank) | mech.mechanism | RESOLVE (2 meanings) |
| `L2` | `L2:second-run-length` | Length | m | Second run of a Z-bend, on the other side of the leg (optional; blank… | bs.pipe-expansion | RESOLVE (2 meanings) |
| `L3` | — | Length | mm | Coupler length b (four-bar) or connecting-rod length l (slider-crank) | mech.mechanism | ok |
| `L4` | — | Length | mm | Rocker length c (four-bar) | mech.mechanism | ok |
| `L_1` | — | Length | mm | Length at point 1 | mech.spring | ok |
| `L_10` | `L_10:basic-rating-life-mrev` | Plain number | — | Basic rating life in millions of revolutions; L₁₀ (millions of revolutions) | mech.bearing-life, mech.bearing-life-basic | ok |
| `L_10h` | `L_10h:basic-rating-life` | Time, rating life | h | Basic rating life; L₁₀ in hours | mech.bearing-life, mech.bearing-life-basic | ok |
| `L_2` | — | Length | mm | Length at point 2 | mech.spring | ok |
| `L_E` | — | Length | m | Effective length | struct.column-buckling | ok |
| `L_add` | — | Length | mm | Length of the closed cylinder beyond its stroke (body, end caps and m… | fp.cylinder | ok |
| `L_b` | — | Length | mm | Buckling length (pin to pin at full stroke) | fp.cylinder | ok |
| `L_cab` | — | Length | m | Cable length, source to motor (links conductor-sizing L) | elec.motor-start | ok |
| `L_closed` | — | Length | mm | Closed (retracted) pin-to-pin length of the cylinder, from the cap-en… | fp.cylinder | ok |
| `L_cr_y` | `L_cr_y:buckling-length-y` | Length | m | Buckling length about y-y | struct.en1993-member, struct.ltb-interaction | ok |
| `L_cr_z` | `L_cr_z:buckling-length-z` | Length | m | Buckling length about z-z | struct.en1993-member, struct.ltb-interaction | ok |
| `L_dev` | — | Length | m | Developed length | bs.pipe-expansion | ok |
| `L_eff` | — | Length | mm | Effective length | fp.cylinder | ok |
| `L_eq` | — | Length | m | Equivalent length of fittings | bs.pipe-pump | ok |
| `L_key` | — | Length | mm | Key length | mech.shaft | ok |
| `L_n` | — | Length | mm | Minimum spring length | mech.spring | ok |
| `L_nm` | `L_nm:modified-life-mrev` | Plain number | — | Modified life (millions of revolutions); Modified rating life in millions of revolutions | mech.bearing-life, mech.bearing-life-basic | ok |
| `L_nmh` | `L_nmh:modified-life` | Time, rating life | h | Modified life in hours; Modified rating life | mech.bearing-life, mech.bearing-life-basic | ok |
| `L_req` | `L_req:required-life` | Time, rating life | h | Required life; Required life (0 for no criterion) | mech.bearing-life, mech.bearing-life-basic | RESOLVE (2 meanings) |
| `L_req` | `L_req:required-leg-length` | Length | m | Required leg length | bs.pipe-expansion | RESOLVE (2 meanings) |
| `L_req_run` | — | Length | m | Required length of the first run | bs.pipe-expansion | ok |
| `L_s` | — | Length | mm | Solid length | mech.spring | ok |
| `L_slide` | — | Length | m | Length of pipe on sliding supports between the anchor and the loop (o… | bs.pipe-expansion | ok |
| `L_surf` | — | Length | m | Height or flow length of the flat surface (correlation, flat) | bs.insulation | ok |
| `L_tot` | — | Length | m | Length that grows | bs.pipe-expansion | ok |
| `L_w` | — | Length | mm | Weld length | mech.weld-group | ok |
| `M` | `M:moment` | Moment, torque | kN·m | Bending moment (magnitude); Couple M (anticlockwise) | mech.shaft-stress, struct.frame-2d | ok |
| `M_0` | — | Moment, torque | kN·m | Applied couple (optional; blank = not used by the other cases) | struct.beam-formulas | ok |
| `M_1` | — | Moment, torque | N·m | Torque at working point 1 (torsion, loads) | mech.spring | ok |
| `M_2` | — | Moment, torque | N·m | Torque at working point 2 (torsion, loads) | mech.spring | ok |
| `M_A` | — | Moment, torque | kN·m | Tightening torque | mech.bolted-joint | ok |
| `M_Ed` | — | Moment, torque | kN·m | Major-axis moment (uniaxial) | struct.base-plate | ok |
| `M_abs_e1` | — | Moment, torque | kN·m | /M/ max, member 1 | struct.frame-2d | ok |
| `M_c` | — | Moment per unit length | kN·m/m | Moment at the centre | struct.plate-bending | ok |
| `M_edge` | — | Moment per unit length | kN·m/m | Edge moment | struct.plate-bending | ok |
| `M_leg` | — | Moment, torque | kN·m | Moment at the leg root | bs.pipe-expansion | ok |
| `M_max` | `M_max:maximum-moment` | Moment, torque | kN·m | Largest bending moment; M max; Maximum sagging moment | mech.shaft, struct.beam-formulas, struct.frame-2d | ok |
| `M_max_e1` | — | Moment, torque | kN·m | M max, member 1 | struct.frame-2d | ok |
| `M_min` | `M_min:minimum-moment` | Moment, torque | kN·m | M min; Minimum (hogging) moment | struct.beam-formulas, struct.frame-2d | ok |
| `M_min_e1` | — | Moment, torque | kN·m | M min, member 1 | struct.frame-2d | ok |
| `M_op` | — | Moment, torque | kN·m | Out-of-plane moment | struct.lifting | ok |
| `M_p` | — | Moment, torque | kN·m | Pin bending moment | mech.lifting-lug | ok |
| `M_pl_Rd` | — | Moment, torque | kN·m | Plate plastic moment | struct.base-plate | ok |
| `M_run` | — | Moment, torque | kN·m | Moment at the first run root | bs.pipe-expansion | ok |
| `M_x` | `M_x:moment-x` | Moment, torque | kN·m | Out-of-plane moment about x (tension on +y side) | mech.bolt-group, mech.weld-group | ok |
| `M_y` | `M_y:moment-y` | Moment, torque | kN·m | Out-of-plane moment about y (tension on +x side) | mech.bolt-group, mech.weld-group | ok |
| `M_y_A` | — | Moment, torque | kN·m | Moment at end A about y-y as an ordinate of the bending-moment diagra… | struct.ltb-interaction | ok |
| `M_y_B` | — | Moment, torque | kN·m | Moment at end B about y-y, with the same convention as end A: an ordi… | struct.ltb-interaction | ok |
| `M_y_Ed` | `M_y_Ed:design-moment-major-axis` | Moment, torque | kN·m | Design moment about y-y (major axis); Greatest design moment about y-y; Greatest design moment about y-y, used in the ψ mode (optional; blank… | struct.en1993-member, struct.ltb-interaction | ok |
| `M_y_Rk` | — | Moment, torque | kN·m | M_y,Rk | struct.ltb-interaction | ok |
| `M_y_b_Rd` | — | Moment, torque | kN·m | Characteristic bending resistance about y-y over the partial factor | struct.en1993-member | ok |
| `M_y_c_Rd` | — | Moment, torque | kN·m | Bending resistance about y-y | struct.en1993-member | ok |
| `M_z` | `M_z:in-plane-torque` | Moment, torque | kN·m | Additional in-plane torque; Additional in-plane torque about the centroid | mech.bolt-group, mech.weld-group | ok |
| `M_z_Ed` | `M_z_Ed:design-moment-minor-axis` | Moment, torque | kN·m | Design moment about z-z (minor axis); Greatest design moment about z-z | struct.en1993-member, struct.ltb-interaction | ok |
| `M_z_Rk` | — | Moment, torque | kN·m | M_z,Rk | struct.ltb-interaction | ok |
| `M_z_b_Rd` | — | Moment, torque | kN·m | Characteristic bending resistance about z-z over the partial factor | struct.en1993-member | ok |
| `M_z_c_Rd` | — | Moment, torque | kN·m | Bending resistance about z-z | struct.en1993-member | ok |
| `Mb_Rd` | — | Moment, torque | kN·m | M_b,Rd | struct.ltb-interaction | ok |
| `Mc_1` | — | Mass per area | g/m² | M1: condensate on the wall | bs.glaser | ok |
| `Mc_10` | — | Mass per area | g/m² | M10: condensate on the wall | bs.glaser | ok |
| `Mc_11` | — | Mass per area | g/m² | M11: condensate on the wall | bs.glaser | ok |
| `Mc_12` | — | Mass per area | g/m² | M12: condensate on the wall | bs.glaser | ok |
| `Mc_2` | — | Mass per area | g/m² | M2: condensate on the wall | bs.glaser | ok |
| `Mc_3` | — | Mass per area | g/m² | M3: condensate on the wall | bs.glaser | ok |
| `Mc_4` | — | Mass per area | g/m² | M4: condensate on the wall | bs.glaser | ok |
| `Mc_5` | — | Mass per area | g/m² | M5: condensate on the wall | bs.glaser | ok |
| `Mc_6` | — | Mass per area | g/m² | M6: condensate on the wall | bs.glaser | ok |
| `Mc_7` | — | Mass per area | g/m² | M7: condensate on the wall | bs.glaser | ok |
| `Mc_8` | — | Mass per area | g/m² | M8: condensate on the wall | bs.glaser | ok |
| `Mc_9` | — | Mass per area | g/m² | M9: condensate on the wall | bs.glaser | ok |
| `Mc_end` | — | Mass per area | g/m² | Condensate left after 12 months | bs.glaser | ok |
| `Mc_end_fine` | — | Mass per area | g/m² | Condensate left after 12 months, each layer in 20 sub-layers | bs.glaser | ok |
| `Mc_if_1` | — | Mass per area | g/m² | Peak condensate on interface 1 | bs.glaser | ok |
| `Mc_limit` | — | Mass per area | g/m² | Limit on the condensate on any interface (optional; blank = report on… | bs.glaser | ok |
| `Mc_max` | — | Mass per area | g/m² | Most condensate on any interface | bs.glaser | ok |
| `Mc_tot` | — | Mass per area | g/m² | Most condensate on the wall | bs.glaser | ok |
| `Mc_tot_fine` | — | Mass per area | g/m² | Most condensate on the wall, each layer in 20 sub-layers | bs.glaser | ok |
| `Mcr` | — | Moment, torque | kN·m | M_cr | struct.ltb-interaction | ok |
| `N` | — | Force | kN | Axial (out-of-plane) load, tension positive | mech.bolt-group | ok |
| `NPSH_r` | — | Length | m | NPSH required by the pump, at the flow NPSH_a is evaluated at: the la… | bs.pipe-pump | ok |
| `NTU` | — | Plain number | — | Transfer units | bs.heat-exchanger | ok |
| `N_1` | — | Plain number | — | Life at block 1 | mech.fatigue-miner | ok |
| `N_2` | — | Plain number | — | Life at block 2 | mech.fatigue-miner | ok |
| `N_3` | — | Plain number | — | Life at block 3 | mech.fatigue-miner | ok |
| `N_Ed` | `N_Ed:axial-force-tension-positive` | Force | kN | Design axial force, tension positive and compression negative (a posi…; Design axial force, tension positive and compression negative, opposi… | struct.en1993-member, struct.ltb-interaction | RESOLVE (2 meanings) |
| `N_Ed` | `N_Ed:axial-force-compression-positive` | Force | kN | Axial force, compression positive and opposite in sign to the member … | struct.base-plate | RESOLVE (2 meanings) |
| `N_Rk` | — | Force | kN | Characteristic axial resistance | struct.ltb-interaction | ok |
| `N_b_Rd` | `N_b_Rd:buckling-resistance` | Force | kN | Governing buckling resistance; N_b,Rd | struct.en1993-member, struct.ltb-interaction | ok |
| `N_b_y_Rd` | `N_b_y_Rd:buckling-resistance-y` | Force | kN | Buckling resistance about y-y; N_b,y,Rd | struct.en1993-member, struct.ltb-interaction | ok |
| `N_b_z_Rd` | `N_b_z_Rd:buckling-resistance-z` | Force | kN | Buckling resistance about z-z; N_b,z,Rd | struct.en1993-member, struct.ltb-interaction | ok |
| `N_beam` | — | Force | kN | Horizontal component per lug | struct.lifting | ok |
| `N_c` | — | Plain number | — | Number of cycles the endurance strength belongs to | mech.spring | ok |
| `N_cr_y` | `N_cr_y:critical-force-y` | Force | kN | Elastic critical force about y-y; N_cr,y | struct.en1993-member, struct.ltb-interaction | ok |
| `N_cr_z` | `N_cr_z:critical-force-z` | Force | kN | Elastic critical force about z-z; N_cr,z | struct.en1993-member, struct.ltb-interaction | ok |
| `N_e1` | — | Force | kN | N, member 1 | struct.frame-2d | ok |
| `N_g` | — | Plain number | — | Grouped circuits | elec.conductor-sizing | ok |
| `N_j_Rd` | — | Force | kN | Axial bearing resistance | struct.base-plate | ok |
| `N_max` | — | Force | kN | N max (tension) | struct.frame-2d | ok |
| `N_min` | — | Force | kN | N min (compression) | struct.frame-2d | ok |
| `N_pl_Rd` | — | Force | kN | Plastic axial resistance | struct.en1993-member | ok |
| `N_ref` | — | Plain number | — | Cycles at the reference stress range | mech.fatigue-miner | ok |
| `Nd` | — | Plain number | — | Design factor of the Design Category (optional; blank = BTH-1 mode is… | struct.lifting | ok |
| `OD` | `OD:pipe-outside-diameter` | Length | mm | Outside diameter of the pipe (optional; the first segment's is offere…; Pipe outside diameter | bs.pipe-expansion, bs.pipe-pump | ok |
| `OD_max` | — | Length | mm | Largest cable diameter | elec.containment | ok |
| `P` | `P:motor-rated-output-power` | Power, heat flow | kW | Motor rated output (shaft) power, from the nameplate (motor-sizing P_…; Rated output (shaft) power; η converts it to input (DC: electrical po… | elec.conductor-sizing, elec.motor-start | RESOLVE (4 meanings) |
| `P` | `P:dissipated-power` | Power, heat flow | kW | Steady power dissipated (the heat flow through every stage) | elec.thermal-chain | RESOLVE (4 meanings) |
| `P` | `P:electrical-input-power` | Power, heat flow | kW | Electrical input power | elec.load-budget | RESOLVE (4 meanings) |
| `P` | `P:heat-loss-per-item` | Power, heat flow | kW | Heat loss per item | elec.enclosure-heat | RESOLVE (4 meanings) |
| `PS` | — | Stress, pressure | bar | Vessel maximum allowable pressure, bar gauge, from the nameplate or r… | fp.accumulator | ok |
| `P_0` | — | Force | kN | Equivalent static load | mech.bearing-life | ok |
| `P_PSU` | — | Power, heat flow | kW | Selected PSU rating | elec.load-budget | ok |
| `P_PSU_req` | — | Power, heat flow | kW | Minimum PSU rating | elec.load-budget | ok |
| `P_add` | `P_add:additional-load` | Power, heat flow | kW | Additional heat load (linked from the load-budget dissipated heat or …; Additional three-phase load (linked from the motor-sizing RMS power) | elec.enclosure-heat, elec.load-budget | ok |
| `P_b` | `P_b:bearing-force` | Force | kN | Bearing | struct.lifting | RESOLVE (2 meanings) |
| `P_b` | `P_b:bellows-governing-pressure` | Stress, pressure | kPa | Axial bellows governing pressure, gauge, at or above zero: the design… | bs.pipe-expansion | RESOLVE (2 meanings) |
| `P_brake` | — | Power, heat flow | kW | Peak braking power | elec.motor-sizing | ok |
| `P_conn` | — | Power, heat flow | kW | Connected load | elec.load-budget | ok |
| `P_cool` | — | Power, heat flow | kW | Cooling unit needed | elec.enclosure-heat | ok |
| `P_dem` | — | Power, heat flow | kW | Demand active power | elec.load-budget | ok |
| `P_hyd` | — | Power, heat flow | kW | Hydraulic power | bs.pipe-pump | ok |
| `P_in` | — | Power, heat flow | kW | Motor input power at rated load | elec.motor-start | ok |
| `P_lim` | — | Line force, distributed load, stiffness | N/m | Sidewall pressure limit, force per metre of bend (from the cable make… | elec.containment | ok |
| `P_loads` | — | Power, heat flow | kW | Internal heat load | elec.enclosure-heat | ok |
| `P_loss` | — | Power, heat flow | kW | Power loss | elec.voltage-drop | ok |
| `P_max` | — | Line force, distributed load, stiffness | N/m | Maximum sidewall pressure, recommended direction | elec.containment | ok |
| `P_max_a` | — | Line force, distributed load, stiffness | N/m | Maximum sidewall pressure, as listed | elec.containment | ok |
| `P_max_b` | — | Line force, distributed load, stiffness | N/m | Maximum sidewall pressure, reversed | elec.containment | ok |
| `P_mean` | — | Power, heat flow | kW | Mean input power over the swept range (negative: the mechanism return… | mech.mechanism | ok |
| `P_nat_max` | — | Power, heat flow | kW | Natural dissipation at the limit | elec.enclosure-heat | ok |
| `P_pk` | — | Power, heat flow | kW | Peak power | elec.motor-sizing | ok |
| `P_plus_Q` | — | Length | mm | Sum of the other two links | mech.mechanism | ok |
| `P_pump` | — | Power, heat flow | kW | Pump shaft power | fp.cylinder | ok |
| `P_rated_add` | — | Power, heat flow | kW | Rated (nameplate) shaft power of the additional machine (linked from … | elec.load-budget | ok |
| `P_rms` | — | Power, heat flow | kW | RMS power (continuous equivalent) | elec.motor-sizing | ok |
| `P_solar` | — | Power, heat flow | kW | Solar gain | elec.enclosure-heat | ok |
| `P_sp` | — | Force | kN | Single-plane fracture | struct.lifting | ok |
| `P_t` | — | Force | kN | Tension beyond the hole | struct.lifting | ok |
| `P_total` | — | Power, heat flow | kW | Total heat | elec.enclosure-heat | ok |
| `P_v` | — | Force | kN | Double-plane shear | struct.lifting | ok |
| `Phi` | `Phi:heat-flow` | Power, heat flow | kW | Heat dissipated inside the enclosure; Heat duty; Heat flow; … (4 descriptions) | bs.heat-exchanger, bs.plane-wall, bs.sealed-system, elec.load-budget | ok |
| `Phi50` | — | Power, heat flow | kW | Selected emitter output at ΔT 50 K | bs.sealed-system | ok |
| `Phi_1` | — | Power, heat flow | kW | Cavity wall: fabric loss | bs.room-heat-loss | ok |
| `Phi_2` | — | Power, heat flow | kW | Window: fabric loss | bs.room-heat-loss | ok |
| `Phi_HL` | — | Power, heat flow | kW | Room design heat loss | bs.room-heat-loss | ok |
| `Phi_K` | — | Plain number | — | Load factor | mech.bolted-joint | ok |
| `Phi_LT` | — | Plain number | — | Φ_LT | struct.ltb-interaction | ok |
| `Phi_T` | — | Power, heat flow | kW | Fabric heat loss | bs.room-heat-loss | ok |
| `Phi_V` | — | Power, heat flow | kW | Ventilation heat loss | bs.room-heat-loss | ok |
| `Phi_el` | — | Power, heat flow | kW | Element fabric loss | bs.room-heat-loss | ok |
| `Phi_load` | — | Power, heat flow | kW | Heat carried by the water (gives the design flow with ΔT; replaces Q) | bs.pipe-pump | ok |
| `Phi_tb` | — | Power, heat flow | kW | Thermal-bridge allowance | bs.room-heat-loss | ok |
| `Phi_tot` | — | Power, heat flow | kW | Total heat required | bs.sealed-system | ok |
| `Q` | `Q:volumetric-flow` | Volumetric flow | L/min, m³/h | Airflow; Design flow rate (optional; blank = the flow comes from the heat load); Flow into the cylinder; … (4 descriptions) | bs.duct-sizing, bs.pipe-pump, fp.cylinder | ok |
| `Q_c` | — | kg/(m·s²·K) | J/(K·mm³) | Volumetric heat capacity | elec.fault-protection | ok |
| `Q_design` | — | Volumetric flow | m³/h | Design flow | bs.pipe-pump | ok |
| `Q_line` | — | Volumetric flow | L/min | Flow in the pressure and return lines before the area ratio | fp.cylinder | ok |
| `Q_lmtd` | — | Power, heat flow | kW | Duty by the LMTD method | bs.heat-exchanger | ok |
| `Q_o` | — | Volumetric flow | L/min | Flow through the entered orifice | fp.cylinder | ok |
| `Q_p` | — | Volumetric flow | L/min | Pump delivery | fp.cylinder | ok |
| `Q_prs` | — | Volumetric flow | L/min | Flow in the line to the cap port (worst stroke) | fp.cylinder | ok |
| `Q_pump` | — | Volumetric flow | m³/h | Pump curve: flow points (optional; blank = no duty point) | bs.pipe-pump | ok |
| `Q_req` | — | Power, heat flow | kW | Required duty | bs.heat-exchanger | ok |
| `Q_rtn` | — | Volumetric flow | L/min | Flow in the return line | fp.cylinder | ok |
| `Q_suc` | — | Volumetric flow | L/min | Flow in the suction line | fp.cylinder | ok |
| `R` | `R:radius` | Length | mm | Radius (bend) | elec.containment | RESOLVE (3 meanings) |
| `R` | `R:resistance` | Resistance, impedance | Ω | Resistance | elec.voltage-drop | RESOLVE (3 meanings) |
| `R` | `R:thermal-resistance` | Thermal resistance | K/W | Thermal resistance | elec.thermal-chain | RESOLVE (3 meanings) |
| `R1` | — | Power, heat flow | kW | Lounge: Φ50 needed | bs.sealed-system | ok |
| `R1_m` | — | Moment, torque | kN·m | M node 1 | struct.frame-2d | ok |
| `R1_m_abs` | — | Moment, torque | kN·m | /M/ node 1 | struct.frame-2d | ok |
| `R1_x` | — | Force | kN | R_x node 1 | struct.frame-2d | ok |
| `R1_x_abs` | — | Force | kN | /R_x/ node 1 | struct.frame-2d | ok |
| `R1_y` | — | Force | kN | R_y node 1 | struct.frame-2d | ok |
| `R2_m` | — | Moment, torque | kN·m | M node 2 | struct.frame-2d | ok |
| `R2_m_abs` | — | Moment, torque | kN·m | /M/ node 2 | struct.frame-2d | ok |
| `R2_x` | — | Force | kN | R_x node 2 | struct.frame-2d | ok |
| `R2_x_abs` | — | Force | kN | /R_x/ node 2 | struct.frame-2d | ok |
| `R2_y` | — | Force | kN | R_y node 2 | struct.frame-2d | ok |
| `RH` | — | Plain number | — | Relative humidity | bs.psychrometrics | ok |
| `RH_1` | — | Plain number | — | Relative humidity, state 1, as a number from 0 to 100 | bs.psychrometrics | ok |
| `RH_2` | — | Plain number | — | Leaving relative humidity (cooling, evaporative) | bs.psychrometrics | ok |
| `RH_a` | — | Percent quantity | % | Relative humidity of the ambient air (anti-condensation) | bs.insulation | ok |
| `RH_b` | — | Plain number | — | Second stream relative humidity (mixing) | bs.psychrometrics | ok |
| `RH_crit` | — | Percent quantity | % | Critical surface relative humidity | bs.glaser | ok |
| `RH_e` | — | Percent quantity | % | Mean outside relative humidity RHe | bs.glaser | ok |
| `RH_int` | — | Percent quantity | % | Internal relative humidity (constant-humidity mode) | bs.glaser | ok |
| `R_1` | `R_1:layer-1-resistance` | Thermal resistance | K/W, m²·K/W | Cavity wall: thermal resistance; Layer 1 resistance | bs.plane-wall, bs.room-heat-loss | ok |
| `R_12` | — | Resistance, impedance | Ω | R₁ + R₂ | elec.fault-protection | ok |
| `R_A` | `R_A:reaction-a` | Force | kN | Left reaction; Reaction at A | mech.shaft, struct.beam-formulas | ok |
| `R_B` | `R_B:reaction-b` | Force | kN | Reaction at B; Right reaction | mech.shaft, struct.beam-formulas | ok |
| `R_T` | — | Thermal resistance | m²·K/W | Total thermal resistance | bs.glaser | ok |
| `R_Tf` | — | Thermal resistance | m²·K/W | Total resistance for the surface check | bs.glaser | ok |
| `R_f` | `R_f:fouling-resistance` | Thermal resistance | m²·K/W | Total fouling resistance | bs.heat-exchanger | RESOLVE (2 meanings) |
| `R_f` | `R_f:repetitions-to-failure` | Plain number | — | Repetitions to failure | mech.fatigue-miner | RESOLVE (2 meanings) |
| `R_h1` | — | Thermal resistance | K/W | Side 1 film resistance | bs.plane-wall | ok |
| `R_h2` | — | Thermal resistance | K/W | Side 2 film resistance | bs.plane-wall | ok |
| `R_in` | — | Length | mm | Inside radius (corroded) | mech.thin-vessel | ok |
| `R_km` | `R_km:resistance-per-km` | m·kg/(s³·A²) | Ω/km | Resistance per km per conductor (cable datasheet or your own table); Resistance per km per conductor (tabulated, at its stated temperature) | elec.motor-start, elec.voltage-drop | ok |
| `R_max` | — | Thermal resistance | K/W | Heat sink budget | elec.thermal-chain | ok |
| `R_min` | — | Length | mm | Minimum bend radius | elec.containment | ok |
| `R_p02` | — | Stress, pressure | MPa | Bolt 0.2 % proof (yield) strength | mech.bolted-joint | ok |
| `R_r` | — | Length | mm | Roller radius (roller follower) | mech.mechanism | ok |
| `R_rel` | `R_rel:reliability` | Percent quantity | % | Reliability | mech.bearing-life, mech.shaft | ok |
| `R_tot` | `R_tot:total-thermal-resistance` | Thermal resistance | K/W, m·K/W | Total resistance; Total thermal resistance | bs.insulation, bs.plane-wall, elec.thermal-chain | ok |
| `Rb` | — | Length | mm | Base-circle radius of the cam profile | mech.mechanism | ok |
| `Re_s1` | `Re_s1:reynolds-number` | Plain number | — | Re, main; Re, oil line | bs.duct-sizing, bs.pipe-pump | ok |
| `Rg` | — | Thermal resistance | m²·K/W | Air-gap resistance Rg | bs.room-heat-loss | ok |
| `Rm` | — | Stress, pressure | MPa | Tensile strength of the wire at this diameter, typed (no default); Wire tensile strength | mech.spring | ok |
| `Rm_A` | — | Stress, pressure | MPa | Curve coefficient A of the material record (R_m = A/d^m, A in MPa for… | mech.spring | ok |
| `Rm_m` | — | Plain number | — | Curve exponent m of the material record | mech.spring | ok |
| `Rse` | `Rse:external-surface-resistance` | Thermal resistance | m²·K/W | External surface resistance R_se; Rse (blank = 0.04 outside, the same as Rsi where it faces another spa… | bs.glaser, bs.room-heat-loss | ok |
| `Rsi` | `Rsi:internal-surface-resistance` | Thermal resistance | m²·K/W | Internal surface resistance R_si; Rsi (blank = 0.13 horizontal, 0.10 upward, 0.17 downward) | bs.glaser, bs.room-heat-loss | ok |
| `Rsi_f` | — | Thermal resistance | m²·K/W | Internal surface resistance R_si,f for the f_Rsi check (0.25 m²K/W is… | bs.glaser | ok |
| `Rz_h` | — | Length | µm | Hub bore roughness Rz (optional; blank = the smoothing is not applied) | mech.press-fit | ok |
| `Rz_s` | — | Length | µm | Shaft surface roughness Rz (optional; blank = the smoothing is not ap… | mech.press-fit | ok |
| `S` | `S:allowable-stress` | Stress, pressure | MPa | Allowable stress at design temperature | mech.thin-vessel | RESOLVE (2 meanings) |
| `S` | `S:stress-range` | Stress, pressure | MPa | Stress range | mech.fatigue-miner | RESOLVE (2 meanings) |
| `SF` | `SF:sizing-safety-factor` | Plain number | — | Sizing safety factor | elec.load-budget | RESOLVE (2 meanings) |
| `SF` | `SF:torque-safety-factor` | Plain number | — | Safety factor on torque | elec.motor-sizing | RESOLVE (2 meanings) |
| `SF_c` | — | Plain number | — | Service factor on the rated torque for the design torque (needed for … | elec.motor-sizing | ok |
| `S_T` | — | Apparent and reactive power | kVA | Selected transformer rating | elec.load-budget | ok |
| `S_T_min` | — | Apparent and reactive power | kVA | Minimum transformer rating, running and starting | elec.load-budget | ok |
| `S_T_req` | — | Apparent and reactive power | kVA | Minimum transformer rating, running load | elec.load-budget | ok |
| `S_T_req_start` | — | Apparent and reactive power | kVA | Minimum transformer rating, starting peak | elec.load-budget | ok |
| `S_a` | — | Length | mm | Minimum sum of coil clearances | mech.spring | ok |
| `S_cpc` | — | Area | mm² | Protective conductor cross-section | elec.fault-protection | ok |
| `S_d` | — | Length | m | Total diffusion-equivalent air layer thickness | bs.glaser | ok |
| `S_dem` | — | Apparent and reactive power | kVA | Demand apparent power | elec.load-budget | ok |
| `S_e` | `S_e:bending-endurance-limit` | Stress, pressure | MPa | Endurance limit; Endurance limit (ranges at or below it do no damage) | mech.fatigue-miner, mech.shaft | RESOLVE (2 meanings) |
| `S_e` | `S_e:spring-shear-endurance-strength` | Stress, pressure | MPa | Endurance strength for the stress kind (shear; bending in torsion) at… | mech.spring | RESOLVE (2 meanings) |
| `S_e_b` | — | Stress, pressure | MPa | Bending endurance strength of the hook (extension) at N_c cycles, alt… | mech.spring | ok |
| `S_f` | — | Plain number | — | Safety factor on the critical load | fp.cylinder | ok |
| `S_k` | — | Apparent and reactive power | MVA | Three-phase fault level at the point of supply (use the minimum fault… | elec.motor-start | ok |
| `S_kQ` | — | Apparent and reactive power | MVA | Upstream network fault level at the transformer primary, in series wi… | elec.motor-start | ok |
| `S_line` | — | Area | mm² | Line conductor cross-section | elec.fault-protection | ok |
| `S_min` | — | Area | mm² | Minimum cross-section | elec.fault-protection | ok |
| `S_n` | — | Apparent and reactive power | kVA | Transformer rating | elec.motor-start | ok |
| `S_peak` | — | Apparent and reactive power | kVA | Peak apparent power at start | elec.load-budget | ok |
| `S_plus_L` | — | Length | mm | Shortest plus longest link | mech.mechanism | ok |
| `S_r` | — | Apparent and reactive power | kVA | Supply transformer rating (for the fault level) | elec.fault-protection | ok |
| `S_ref` | — | Stress, pressure | MPa | Reference stress range (detail category or material fatigue strength) | mech.fatigue-miner | ok |
| `S_run` | — | Volume, section modulus | m³ | Flexibility of the runs | bs.pipe-expansion | ok |
| `S_sc` | — | Apparent and reactive power | MVA | Short-circuit apparent power at the bank | elec.motor-start | ok |
| `S_start` | — | Apparent and reactive power | kVA | Starting apparent power | elec.motor-start | ok |
| `S_strip` | — | Plain number | — | Safety factor on stripping | mech.bolted-joint | ok |
| `S_ut` | — | Stress, pressure | MPa | Ultimate tensile strength | mech.shaft | ok |
| `S_y` | `S_y:yield-strength` | Stress, pressure | MPa | Yield strength | mech.shaft, mech.shaft-stress | ok |
| `S_y_h` | — | Stress, pressure | MPa | Hub yield strength | mech.press-fit | ok |
| `S_y_hub` | — | Stress, pressure | MPa | Hub yield strength (optional; blank = the hub is not checked; the key… | mech.shaft | ok |
| `S_y_key` | — | Stress, pressure | MPa | Key yield strength | mech.shaft | ok |
| `S_y_s` | — | Stress, pressure | MPa | Shaft yield strength | mech.press-fit | ok |
| `Sa` | — | Stress, pressure | MPa | Allowable displacement-stress range for the legs and runs (your pipin… | bs.pipe-expansion | ok |
| `T` | `T:torque` | Moment, torque | kN·m | Torque (magnitude); Torque about the centroid; Torque capacity; … (4 descriptions) | mech.bolt-group, mech.press-fit, mech.shaft, mech.shaft-stress, mech.weld-group | ok |
| `T_1` | `T_1:side-1-temperature` | Temperature | °C | Side 1 temperature (the fluid's, or the surface's when no film is giv…; Source temperature | bs.plane-wall, elec.thermal-chain | RESOLVE (2 meanings) |
| `T_1` | `T_1:pull-start-tension` | Force | N | Tension at the start of the pull (pulling rope, drum or head) | elec.containment | RESOLVE (2 meanings) |
| `T_1_out` | — | Temperature | °C | Dry-bulb temperature, state 1 | bs.psychrometrics | ok |
| `T_2` | `T_2:side-2-temperature` | Temperature | °C | Hot side of stage 2; Side 2 temperature (the fluid's, or the surface's when no film is giv… | bs.plane-wall, elec.thermal-chain | RESOLVE (2 meanings) |
| `T_2` | `T_2:leaving-dry-bulb-temperature` | Temperature | °C | Leaving dry-bulb (sensible, cooling) | bs.psychrometrics | RESOLVE (2 meanings) |
| `T_3` | — | Temperature | °C | Hot side of stage 3 | elec.thermal-chain | ok |
| `T_Ed` | — | Force | kN | Tension resultant, more loaded row | struct.base-plate | ok |
| `T_Ed_2` | — | Force | kN | Tension resultant, other row | struct.base-plate | ok |
| `T_L` | — | Moment, torque | N·m | Constant load torque at the output (rotary; continuous duty needs it,… | elec.motor-sizing | ok |
| `T_Rd` | — | Force | kN | Tension T-stub resistance | struct.base-plate | ok |
| `T_Rd_1` | — | Force | kN | T-stub mode 1-2, no prying | struct.base-plate | ok |
| `T_Rd_3` | — | Force | kN | T-stub mode 3 | struct.base-plate | ok |
| `T_a` | `T_a:alternating-torque` | Moment, torque | kN·m | Alternating torque amplitude about the mean (optional; blank = 0, a s… | mech.shaft | RESOLVE (2 meanings) |
| `T_a` | `T_a:ambient-temperature` | Temperature | °C | Ambient the last stage rejects to | elec.thermal-chain | RESOLVE (2 meanings) |
| `T_acc` | — | Moment, torque | N·m | Acceleration torque | elec.motor-sizing | ok |
| `T_air` | — | Temperature | °C | Air temperature | bs.duct-sizing | ok |
| `T_alt` | — | Moment, torque | N·m | Alternating torque amplitude | elec.motor-sizing | ok |
| `T_amb` | `T_amb:ambient-temperature` | Temperature | °C | Ambient air temperature (also the radiation sink); Ambient temperature; Ambient temperature at the board | bs.insulation, elec.conductor-sizing, elec.enclosure-heat, elec.load-budget, elec.pcb-trace, elec.voltage-drop | ok |
| `T_c` | `T_c:conductor-temperature` | Temperature | °C | Conductor operating temperature for the resistance; Conductor temperature for the resistance | elec.motor-start, elec.voltage-drop | RESOLVE (2 meanings) |
| `T_c` | `T_c:charging-temperature` | Temperature | °C | Charging temperature | fp.accumulator | RESOLVE (2 meanings) |
| `T_c_in` | — | Temperature | °C | Cold-side inlet temperature | bs.heat-exchanger | ok |
| `T_c_m` | — | Temperature | °C | Cold-side mean | bs.heat-exchanger | ok |
| `T_c_out` | — | Temperature | °C | Cold outlet | bs.heat-exchanger | ok |
| `T_cab` | — | Force | N | Pulling tension limit from the cables | elec.containment | ok |
| `T_db` | — | Temperature | °C | Dry-bulb temperature | bs.psychrometrics | ok |
| `T_db_b` | — | Temperature | °C | Second stream dry-bulb (mixing) | bs.psychrometrics | ok |
| `T_dec` | — | Moment, torque | N·m | Deceleration torque | elec.motor-sizing | ok |
| `T_design` | — | Moment, torque | N·m | Design torque for downstream couplings | elec.motor-sizing | ok |
| `T_dew` | `T_dew:dew-point-temperature` | Temperature | °C | Dew point of the ambient air, if known, for example from the psychrom…; Dew point of the hot-side air, when the hot side is humid air (option… | bs.heat-exchanger, bs.insulation | ok |
| `T_dp` | — | Temperature | °C | Dew-point temperature | bs.psychrometrics | ok |
| `T_dp_1` | — | Temperature | °C | Dew point, state 1 | bs.psychrometrics | ok |
| `T_fill` | — | Temperature | °C | Fill temperature | bs.sealed-system | ok |
| `T_flow` | — | Temperature | °C | Flow temperature | bs.sealed-system | ok |
| `T_fluid` | — | Temperature | °C | Fluid temperature (mean; link the mean water temperature of the seale… | bs.pipe-pump | ok |
| `T_grip` | — | Force | N | Grip or pulling-eye limit of the whole bundle (from the pulling equip… | elec.containment | ok |
| `T_h_in` | — | Temperature | °C | Hot-side inlet temperature | bs.heat-exchanger | ok |
| `T_h_out` | — | Temperature | °C | Hot outlet | bs.heat-exchanger | ok |
| `T_hold` | — | Moment, torque | N·m | Holding torque in the dwell | elec.motor-sizing | ok |
| `T_in_0` | — | Moment, torque | N·m | Input torque at the first input angle | mech.mechanism | ok |
| `T_install` | — | Temperature | °C | Temperature at installation (when the run is anchored) | bs.pipe-expansion | ok |
| `T_int` | `T_int:internal-design-temperature` | Temperature | °C | Internal air temperature; Internal design temperature | bs.glaser, bs.room-heat-loss | RESOLVE (2 meanings) |
| `T_int` | `T_int:enclosure-internal-temperature` | Temperature | °C | Internal temperature (natural) | elec.enclosure-heat | RESOLVE (2 meanings) |
| `T_lambda` | — | Temperature | °C | Temperature the conductivity applies at, compared with the mean insul… | bs.insulation | ok |
| `T_lim` | — | Moment, torque | N·m | Protective torque limit at the motor shaft, e.g. a torque limiter or … | elec.motor-sizing | ok |
| `T_line` | — | Temperature | °C | Design temperature of the line for the insulation step: the coldest (…; Line design temperature | bs.pipe-pump | ok |
| `T_load` | `T_load:load-torque` | Moment, torque | N·m | External load torque on the output link (four-bar). Positive acts in …; Load torque at motor | elec.motor-sizing, mech.mechanism | ok |
| `T_m` | `T_m:mean-insulation-temperature` | Temperature | °C | Mean insulation temperature | bs.insulation | RESOLVE (2 meanings) |
| `T_m` | `T_m:mean-water-temperature` | Temperature | °C | Mean water temperature | bs.sealed-system | RESOLVE (2 meanings) |
| `T_max` | `T_max:maximum-temperature` | Temperature | °C | Highest operating temperature; Internal temperature limit; Maximum internal temperature; … (5 descriptions) | elec.enclosure-heat, elec.thermal-chain, fp.accumulator, fp.oring-gland | RESOLVE (3 meanings) |
| `T_max` | `T_max:maximum-pulling-tension` | Force | N | Maximum pulling tension, each | elec.containment | RESOLVE (3 meanings) |
| `T_max` | `T_max:maximum-system-temperature` | Temperature | °C | Maximum system temperature | bs.sealed-system | RESOLVE (3 meanings) |
| `T_max_a` | — | Force | N | Maximum tension, as listed | elec.containment | ok |
| `T_max_b` | — | Force | N | Maximum tension, reversed | elec.containment | ok |
| `T_min` | `T_min:minimum-temperature` | Temperature | °C | Lowest operating temperature (the ring sits at 20 °C when installed); Minimum operating temperature (the gas is coldest and delivers least … | fp.accumulator, fp.oring-gland | ok |
| `T_motor` | — | Moment, torque | N·m | Hydraulic motor torque | fp.cylinder | ok |
| `T_op` | `T_op:conductor-operating-temperature` | Temperature | °C | Conductor operating temperature for the insulation; Conductor temperature for Z_s | elec.conductor-sizing, elec.fault-protection | ok |
| `T_operate` | — | Temperature | °C | Operating temperature (hot or cold) | bs.pipe-expansion | ok |
| `T_peak` | — | Moment, torque | N·m | Candidate motor peak torque | elec.motor-sizing | ok |
| `T_pk` | `T_pk:peak-torque` | Moment, torque | N·m, kN·m | Peak torque; Peak torque for the yield check and the key (optional; blank = T + T_… | elec.motor-sizing, mech.shaft | ok |
| `T_proc` | — | Temperature | °C | Process (fluid) temperature | bs.insulation | ok |
| `T_rated` | `T_rated:rated-temperature` | Temperature | °C | Conductor rated temperature | elec.voltage-drop | RESOLVE (2 meanings) |
| `T_rated` | `T_rated:rated-torque` | Moment, torque | N·m | Candidate motor continuous (rated) torque | elec.motor-sizing | RESOLVE (2 meanings) |
| `T_ref` | — | Temperature | °C | Reference ambient temperature for the derived C_a | elec.conductor-sizing | ok |
| `T_return` | — | Temperature | °C | Return temperature | bs.sealed-system | ok |
| `T_rms` | — | Moment, torque | N·m | RMS torque | elec.motor-sizing | ok |
| `T_rms_in` | — | Moment, torque | N·m | RMS input torque over the swept range | mech.mechanism | ok |
| `T_room` | — | Temperature | °C | Room temperature | bs.sealed-system | ok |
| `T_s0` | — | Temperature | °C | Side 1 surface temperature | bs.plane-wall | ok |
| `T_s1` | — | Temperature | °C | Side 2 surface temperature | bs.plane-wall | ok |
| `T_se` | — | Temperature | °C | Outer surface temperature | bs.insulation | ok |
| `T_seal` | — | Temperature | °C | Temperature at which the enclosure was closed with no breather or ven… | elec.enclosure-heat | ok |
| `T_shaft` | — | Moment, torque | N·m | Pump shaft torque | fp.cylinder | ok |
| `T_supply` | — | Temperature | °C | MVHR supply air temperature after heat recovery (optional; blank = na… | bs.room-heat-loss | ok |
| `T_use` | — | Temperature | °C | Temperature of use. Recorded: Es and the permissible strain must be t… | mech.snap-fit | ok |
| `T_user` | — | Temperature | °C | Conductor operating temperature for 'user' insulation | elec.conductor-sizing | ok |
| `T_wb` | — | Temperature | °C | Wet-bulb temperature | bs.psychrometrics | ok |
| `T_wb_1` | — | Temperature | °C | Wet-bulb temperature, state 1 | bs.psychrometrics | ok |
| `U` | `U:supply-voltage` | Voltage | V | Supply and motor rated voltage (line-to-line); Supply voltage (line voltage for three-phase) | elec.conductor-sizing, elec.motor-start | RESOLVE (2 meanings) |
| `U` | `U:share-of-revolutions` | Plain number | — | Share of revolutions | mech.bearing-life | RESOLVE (2 meanings) |
| `UA` | — | Thermal conductance | W/K | Overall conductance U·A | bs.heat-exchanger | ok |
| `U_0` | — | Voltage | V | Nominal voltage to earth | elec.fault-protection | ok |
| `U_1` | — | Heat transfer coefficient, U-value | W/(m²·K) | Cavity wall: U-value | bs.room-heat-loss | ok |
| `U_1ph` | — | Voltage | V | Single-phase voltage | elec.load-budget | ok |
| `U_2` | — | Heat transfer coefficient, U-value | W/(m²·K) | Window: U-value | bs.room-heat-loss | ok |
| `U_3ph` | — | Voltage | V | Three-phase line voltage | elec.load-budget | ok |
| `U_DC` | — | Voltage | V | DC bus voltage | elec.load-budget | ok |
| `U_L` | — | Voltage | V | Three-phase line voltage | elec.load-budget | ok |
| `U_a` | — | Length | m | Anchor distance | bs.pipe-expansion | ok |
| `U_b` | — | Voltage | V | Battery nominal voltage | elec.load-budget | ok |
| `U_clean` | — | Heat transfer coefficient, U-value | W/(m²·K) | Overall heat transfer coefficient (clean) | bs.heat-exchanger | ok |
| `U_eff` | — | Heat transfer coefficient, U-value | W/(m²·K) | Heat-loss weighted fabric U-value (with f) | bs.room-heat-loss | ok |
| `U_g` | — | Heat transfer coefficient, U-value | W/(m²·K) | Ground floor U-value from a record, applied to each element with down… | bs.room-heat-loss | ok |
| `U_m` | — | Heat transfer coefficient, U-value | W/(m²·K) | Mean fabric U-value (area-weighted) | bs.room-heat-loss | ok |
| `U_max` | — | Heat transfer coefficient, U-value | W/(m²·K) | Target U for this element | bs.room-heat-loss | ok |
| `U_ref` | — | Heat transfer coefficient, U-value | W/(m²·K) | U-value of this wall from the room heat loss calculation, for the con… | bs.glaser | ok |
| `U_target` | — | Heat transfer coefficient, U-value | W/(m²·K) | Target U for built-up elements without their own target (optional; bl… | bs.room-heat-loss | ok |
| `U_user` | — | Heat transfer coefficient, U-value | W/(m²·K) | U override (ground floor, glazing) | bs.room-heat-loss | ok |
| `U_wall` | `U_wall:wall-u-value` | Heat transfer coefficient, U-value | W/(m²·K) | Overall heat transfer coefficient; U-value of the wall | bs.glaser, bs.plane-wall | ok |
| `V` | `V:nominal-gas-volume` | Volume, section modulus | L | Nominal gas volume | fp.accumulator | RESOLVE (2 meanings) |
| `V` | `V:room-volume` | Volume, section modulus | m³ | Room volume | bs.room-heat-loss | RESOLVE (2 meanings) |
| `V0` | — | Volume, section modulus | L | Accumulator gas volume required | fp.accumulator | ok |
| `V_Ed` | `V_Ed:shear-force` | Force | kN | Design shear force; Shear force | struct.base-plate, struct.en1993-member | RESOLVE (2 meanings) |
| `V_Ed` | `V_Ed:block-tearing-group-force` | Force | kN | Group force for block tearing (optional; blank = no block tearing che… | struct.connection-ec3 | RESOLVE (2 meanings) |
| `V_Rd` | — | Force | kN | Shear resistance | struct.base-plate | ok |
| `V_b_Ed` | — | Force | kN | Shear per anchor bolt (to PROFIS) | struct.base-plate | ok |
| `V_e` | — | Volume, section modulus | L | Minimum vessel volume | bs.sealed-system | ok |
| `V_e1` | — | Force | kN | /V/ max, member 1 | struct.frame-2d | ok |
| `V_em` | — | Volume, section modulus | L | Emitter water content | bs.sealed-system | ok |
| `V_g` | — | Volume, section modulus | cm³/rev | Pump displacement per revolution (optional; blank = no pump delivery … | fp.cylinder | ok |
| `V_gm` | — | Volume, section modulus | cm³/rev | Hydraulic motor displacement per revolution (optional; blank = no mot… | fp.cylinder | ok |
| `V_m` | — | Voltage | V | Voltage at the motor terminals while starting | elec.motor-start | ok |
| `V_max` | `V_max:maximum-shear` | Force | kN | Maximum shear; /V/ max | struct.beam-formulas, struct.frame-2d | ok |
| `V_n` | — | Voltage | V | Nominal voltage (line-to-line for three-phase) | elec.voltage-drop | ok |
| `V_pcc` | — | Voltage | V | Point of common coupling voltage while starting | elec.motor-start | ok |
| `V_pipe` | `V_pipe:pipework-water-content` | Volume, section modulus | L | Pipework water content; Water held in the pipes | bs.pipe-pump, bs.sealed-system | ok |
| `V_pl_Rd` | — | Force | kN | Plastic shear resistance | struct.en1993-member | ok |
| `V_plant` | — | Volume, section modulus | L | Plant water content (boiler, cylinder coil, buffer) | bs.sealed-system | ok |
| `V_pts` | — | Volumetric flow | m³/h | Fan curve: airflow points | elec.enclosure-heat | ok |
| `V_ref` | — | Volumetric flow | m³/h | Reference airflow for the system resistance | elec.enclosure-heat | ok |
| `V_req` | — | Volumetric flow | m³/h | Required airflow | elec.enclosure-heat | ok |
| `V_req_net` | — | Volumetric flow | m³/h | Required airflow (net) | elec.enclosure-heat | ok |
| `V_s` | — | Volume, section modulus | L | System water volume | bs.sealed-system | ok |
| `V_vessel` | `V_vessel:vessel-volume` | Volume, section modulus | L | Selected vessel volume (optional; blank = no vessel check); Vessel internal volume (optional; blank = the selected size) | bs.sealed-system, fp.accumulator | ok |
| `Vg_req` | — | Volume, section modulus | cm³/rev | Pump displacement the circuit needs | fp.cylinder | ok |
| `W` | `W:width` | Length | m, mm | Enclosure width; Lug width at the hole | elec.enclosure-heat, mech.lifting-lug | RESOLVE (3 meanings) |
| `W` | `W:humidity-ratio` | Plain number | — | Humidity ratio (g per kg dry air) | bs.psychrometrics | RESOLVE (3 meanings) |
| `W` | `W:ring-cross-section-diameter` | Length | mm | Ring cross-section diameter | fp.oring-gland | RESOLVE (3 meanings) |
| `WLL` | — | Force | kN | Rated load (working load limit) of the appliance, carried by all its … | struct.lifting | ok |
| `W_1` | — | Plain number | — | Humidity ratio, state 1, in grams per kilogram of dry air | bs.psychrometrics | ok |
| `W_2` | — | Plain number | — | Leaving humidity ratio, g/kg (steam) | bs.psychrometrics | ok |
| `W_el_y` | `W_el_y:elastic-modulus-major-axis` | Volume, section modulus | cm³ | Elastic section modulus about the y-y axis; Elastic section modulus about y-y | struct.en1993-member, struct.ltb-interaction, struct.section-properties | ok |
| `W_el_z` | `W_el_z:elastic-modulus-minor-axis` | Volume, section modulus | cm³ | Elastic section modulus about the z-z axis; Elastic section modulus about z-z | struct.en1993-member, struct.ltb-interaction, struct.section-properties | ok |
| `W_int` | — | Length | mm | Trunking or tray internal width | elec.containment | ok |
| `W_pl_y` | `W_pl_y:plastic-modulus-major-axis` | Volume, section modulus | cm³ | Plastic section modulus about the y-y axis; Plastic section modulus about y-y | struct.en1993-member, struct.ltb-interaction, struct.section-properties | ok |
| `W_pl_z` | `W_pl_z:plastic-modulus-minor-axis` | Volume, section modulus | cm³ | Plastic section modulus about the z-z axis; Plastic section modulus about z-z | struct.en1993-member, struct.ltb-interaction, struct.section-properties | ok |
| `W_tol` | — | Length | mm | Ring cross-section tolerance, plus or minus | fp.oring-gland | ok |
| `W_y` | — | Volume, section modulus | cm³ | Section modulus for bending about y-y | struct.ltb-interaction | ok |
| `X` | `X:radial-factor` | Plain number | — | Radial factor for F_a/F_r above e; Radial load factor (bearing catalogue) | mech.bearing-life, mech.bearing-life-basic | RESOLVE (2 meanings) |
| `X` | `X:reactance` | Resistance, impedance | Ω | Reactance | elec.voltage-drop | RESOLVE (2 meanings) |
| `XR` | — | Plain number | — | Source X/R ratio (of the transformer, or of the fault level) | elec.motor-start | ok |
| `XR_Q` | — | Plain number | — | Upstream network X/R ratio | elec.motor-start | ok |
| `X_0` | — | Plain number | — | Static radial factor X₀ | mech.bearing-life | ok |
| `X_1` | — | Plain number | — | Radial factor X₁ for F_a/F_r ≤ e (1 for a single-row radial ball bear… | mech.bearing-life | ok |
| `X_c` | — | Percent quantity | % | Largest stretch at the cold end | fp.oring-gland | ok |
| `X_km` | `X_km:reactance-per-km` | m·kg/(s³·A²) | Ω/km | Reactance per km per conductor | elec.motor-start, elec.voltage-drop | ok |
| `Y` | `Y:axial-factor` | Plain number | — | Axial factor for F_a/F_r above e (from the manufacturer; zero makes t…; Axial load factor (bearing catalogue) | mech.bearing-life, mech.bearing-life-basic | ok |
| `Y_0` | — | Plain number | — | Static axial factor Y₀ | mech.bearing-life | ok |
| `Y_1` | — | Plain number | — | Axial factor Y₁ for F_a/F_r ≤ e (0 for a single-row radial ball beari… | mech.bearing-life | ok |
| `Y_scr` | — | Length | mm | Resultant displacement Y | bs.pipe-expansion | ok |
| `Z_e` | — | Resistance, impedance | Ω | External earth fault loop impedance | elec.fault-protection | ok |
| `Z_m` | — | Resistance, impedance | Ω | Motor starting impedance (magnitude) | elec.motor-start | ok |
| `Z_s` | — | Resistance, impedance | Ω | Loop impedance | elec.fault-protection | ok |
| `Z_s_max` | — | Resistance, impedance | Ω | Maximum earth fault loop impedance; Maximum loop impedance | elec.fault-protection | ok |
| `Z_src` | — | Resistance, impedance | Ω | Source impedance (magnitude) | elec.motor-start | ok |
| `Z_test` | — | Resistance, impedance | Ω | Field-test maximum (rule of thumb) | elec.fault-protection | ok |
| `Z_x` | — | Volume, section modulus | cm³ | Section modulus | bs.pipe-expansion | ok |
| `a` | `a:weld-throat` | Length | mm | Throat; Throat thickness; Weld throat (optional; blank = no weld check) | mech.weld-group, struct.connection-ec3, struct.fillet-weld | RESOLVE (8 meanings) |
| `a` | `a:load-position` | Length | m | Load position from the left end (optional; blank = not used by the di…; a from end i | struct.beam-formulas, struct.frame-2d | RESOLVE (8 meanings) |
| `a` | `a:duct-side` | Length | mm | Fixed side a (rectangular/oval; blank = size) | bs.duct-sizing | RESOLVE (8 meanings) |
| `a` | `a:edge-distance` | Length | mm | Edge distance, hole edge to lug end (load direction) | mech.lifting-lug | RESOLVE (8 meanings) |
| `a` | `a:end-distance` | Length | mm | End distance from the hole edge to the plate edge along the load line… | struct.lifting | RESOLVE (8 meanings) |
| `a` | `a:inner-radius` | Length | mm | Inner radius | mech.thick-cylinder | RESOLVE (8 meanings) |
| `a` | `a:plate-radius-or-side` | Length | mm | Plate radius (circular) or length in the x direction (rectangular) | struct.plate-bending | RESOLVE (8 meanings) |
| `a` | `a:prying-lever` | Length | mm | Prying lever: bolt to flange edge (optional; blank = no prying) | mech.bolt-group | RESOLVE (8 meanings) |
| `a_1` | `a_1:reliability-factor` | Plain number | — | Life modification factor for reliability; Reliability factor | mech.bearing-life, mech.bearing-life-basic | ok |
| `a_arm` | — | Length | mm | Length of the lever on the torsion bar | mech.snap-fit | ok |
| `a_calc` | — | Length | mm | Throat needed | struct.fillet-weld | ok |
| `a_eff` | — | Length | mm | End distance along the load line | struct.lifting | ok |
| `a_iso` | — | Plain number | — | Life modification factor a_ISO (from the manufacturer's tool) | mech.bearing-life | ok |
| `a_pk` | — | Acceleration | m/s² | Peak acceleration | elec.motor-sizing | ok |
| `a_req` | — | Length | mm | Required throat | struct.fillet-weld | ok |
| `alpha` | `alpha:temperature-coefficient-of-resistance` | 1/K | 1/K | Temperature coefficient of resistance; Temperature coefficient of resistance of copper | elec.fault-protection, elec.motor-start, elec.pcb-trace | RESOLVE (5 meanings) |
| `alpha` | `alpha:thermal-expansion-coefficient` | 1/K | 1/K, um/(m·K) | Coefficient of thermal expansion; Mean coefficient of thermal expansion between the install and operati…; Ring linear thermal expansion coefficient α (an indicative elastomer … | bs.pipe-expansion, fp.oring-gland, mech.thermal-expansion | RESOLVE (5 meanings) |
| `alpha` | `alpha:concentration-factor` | Plain number | — | Concentration factor √(A_c1/A_c0) of the foundation (1 when the found… | struct.base-plate | RESOLVE (5 meanings) |
| `alpha` | `alpha:entry-angle` | Angle | ° | Entry (assembly) angle α of the inclined face to the joining direction | mech.snap-fit | RESOLVE (5 meanings) |
| `alpha` | `alpha:temperature-coefficient-of-resistance` | Plain number | — | Temperature coefficient of resistance | elec.voltage-drop | RESOLVE (5 meanings) |
| `alpha3_0` | — | Angular acceleration | rad/s² | Coupler angular acceleration at the first input angle | mech.mechanism | ok |
| `alpha3_pk` | — | Angular acceleration | rad/s² | Peak coupler angular acceleration | mech.mechanism | ok |
| `alpha4_0` | — | Angular acceleration | rad/s² | Rocker angular acceleration at the first input angle | mech.mechanism | ok |
| `alpha_A` | — | Plain number | — | Tightening scatter factor (F_M,max / F_M,min) | mech.bolted-joint | ok |
| `alpha_LT` | — | Plain number | — | α_LT | struct.ltb-interaction | ok |
| `alpha_a` | — | Plain number | — | Imperfection factor for curve a (recommended 0.21) | struct.ltb-interaction | ok |
| `alpha_a0` | — | Plain number | — | Imperfection factor for curve a0 (recommended 0.13) | struct.ltb-interaction | ok |
| `alpha_b` | `alpha_b:bearing-factor` | Plain number | — | Bearing factor | struct.connection-ec3 | RESOLVE (2 meanings) |
| `alpha_b` | `alpha_b:imperfection-factor-curve-b` | Plain number | — | Imperfection factor for curve b (recommended 0.34) | struct.ltb-interaction | RESOLVE (2 meanings) |
| `alpha_bc` | — | Plain number | — | Anchor shear factor α_bc | struct.base-plate | ok |
| `alpha_c` | — | Plain number | — | Imperfection factor for curve c (recommended 0.49) | struct.ltb-interaction | ok |
| `alpha_cc` | — | Plain number | — | Coefficient for long-term effects on the concrete strength | struct.base-plate | ok |
| `alpha_d` | — | Plain number | — | Imperfection factor for curve d (recommended 0.76) | struct.ltb-interaction | ok |
| `alpha_h` | — | 1/K | 1/K | Hub thermal expansion coefficient | mech.press-fit | ok |
| `alpha_lock` | — | Angle | ° | Self-locking return angle | mech.snap-fit | ok |
| `alpha_m` | — | 1/K | 1/K | Groove metal linear thermal expansion coefficient α_m (the part that … | fp.oring-gland | ok |
| `alpha_out_pk` | — | Angular acceleration | rad/s² | Peak output (rocker) angular acceleration | mech.mechanism | ok |
| `alpha_pk` | — | Angular acceleration | rad/s² | Motor angular acceleration | elec.motor-sizing | ok |
| `alpha_pl_y` | — | Plain number | — | Shape factor about the y-y axis | struct.section-properties | ok |
| `alpha_r` | — | Angle | ° | Return (separation) angle α′ of the retaining face (90° is a face squ… | mech.snap-fit | ok |
| `alpha_s` | — | 1/K | 1/K | Linear thermal expansion coefficient of the part that carries the sea… | fp.oring-gland | ok |
| `alpha_sun` | — | Plain number | — | Surface absorptivity | elec.enclosure-heat | ok |
| `alpha_v` | — | Plain number | — | Shear factor | struct.connection-ec3 | ok |
| `alpha_y` | `alpha_y:imperfection-factor-y` | Plain number | — | Imperfection factor about y-y (optional; blank = from the buckling cu…; α_y | struct.en1993-member, struct.ltb-interaction | ok |
| `alpha_z` | `alpha_z:imperfection-factor-z` | Plain number | — | Imperfection factor about z-z (optional; blank = from the buckling cu…; α_z | struct.en1993-member, struct.ltb-interaction | ok |
| `altitude` | `altitude:altitude` | Length | m | Altitude (used when no pressure is entered); Site altitude (air density) | bs.psychrometrics, elec.enclosure-heat | ok |
| `anchor_force` | — | Force | kN | Force on the anchors (resultant) | bs.pipe-expansion | ok |
| `anchor_moment` | — | Moment, torque | kN·m | Largest root moment (guided-cantilever model) | bs.pipe-expansion | ok |
| `area` | — | Area | m² | Area | bs.room-heat-loss | ok |
| `assembly_force` | — | Force | N | Assembly force | mech.snap-fit | ok |
| `b` | `b:width` | Length | mm | Column width, along the plate width B (I/H and RHS); Flange width; Groove width; … (7 descriptions) | fp.oring-gland, mech.snap-fit, struct.base-plate, struct.en1993-member, struct.ltb-interaction, struct.plate-bending, struct.section-properties | RESOLVE (4 meanings) |
| `b` | `b:load-position` | Length | m | b from end i (distributed) | struct.frame-2d | RESOLVE (4 meanings) |
| `b` | `b:outer-radius` | Length | mm | Outer radius | mech.thick-cylinder | RESOLVE (4 meanings) |
| `b` | `b:prying-lever` | Length | mm | Prying lever: bolt to the loaded line (optional; blank = no prying) | mech.bolt-group | RESOLVE (4 meanings) |
| `b_l` | — | Length | mm | Groove width, lower deviation | fp.oring-gland | ok |
| `b_ratio` | — | Plain number | — | Tip width / root width (tapered width) | mech.snap-fit | ok |
| `b_u` | — | Length | mm | Groove width, upper deviation | fp.oring-gland | ok |
| `be` | — | Length | mm | Edge distance from the hole edge to the plate edge on each side, acro… | struct.lifting | ok |
| `be_eff` | — | Length | mm | Effective width beyond the hole | struct.lifting | ok |
| `beta` | `beta:cam-angle` | Angle | ° | Cam angle β | mech.mechanism | RESOLVE (3 meanings) |
| `beta` | `beta:reciprocal-temperature-coefficient` | Temperature, temperature difference | K | Reciprocal temperature coefficient β | elec.fault-protection | RESOLVE (3 meanings) |
| `beta` | `beta:sling-out-of-plane-angle` | Angle | ° | Out-of-plane angle β of the sling out of the lug plane | struct.lifting | RESOLVE (3 meanings) |
| `beta_LT` | — | Plain number | — | Slenderness correction factor for cl. 6.3.2.3 (recommended 0.75) | struct.ltb-interaction | ok |
| `beta_Lw` | — | Plain number | — | Long-joint factor | struct.fillet-weld | ok |
| `beta_j` | — | Plain number | — | Joint coefficient β_j | struct.base-plate | ok |
| `beta_w` | — | Plain number | — | Correlation factor (0.8 S235, 0.85 S275, 0.9 S355, 1.0 S420 and S460) | struct.fillet-weld | ok |
| `bonus` | — | Length | mm | MMC bonus (position/profile) | mech.tolerance-stack | ok |
| `c` | `c:bearing-outstand` | Length | mm | Bearing outstand round the column footprint | struct.base-plate | RESOLVE (2 meanings) |
| `c` | `c:corrosion-allowance` | Length | mm | Corrosion allowance | mech.thin-vessel | RESOLVE (2 meanings) |
| `c_a` | — | Length | µm | Assembly clearance wanted for shrink fitting | mech.press-fit | ok |
| `c_max` | `c_max:maximum-clearance` | Length | µm | Maximum clearance | mech.iso286-fit | RESOLVE (2 meanings) |
| `c_max` | `c_max:voltage-factor-maximum-fault` | Plain number | — | Voltage factor c for the maximum fault | elec.fault-protection | RESOLVE (2 meanings) |
| `c_mean` | — | Length | µm | Mean clearance | mech.iso286-fit | ok |
| `c_min` | — | Length | µm | Minimum clearance | mech.iso286-fit | ok |
| `c_src` | — | Plain number | — | Voltage factor c for the fault-level route, linked from the fault-pro… | elec.motor-start | ok |
| `cap_bth` | — | Force | kN | Capacity, BTH-1 mode | struct.lifting | ok |
| `capacity` | — | Force | kN | Rated load the lug carries in the governing mode | struct.lifting | ok |
| `case` | — | Plain number | — | Load case | struct.frame-2d | ok |
| `chi_LT` | — | Plain number | — | χ_LT | struct.ltb-interaction | ok |
| `chi_LT_0` | — | Plain number | — | χ_LT before the f modification | struct.ltb-interaction | ok |
| `chi_y` | `chi_y:reduction-factor-y` | Plain number | — | Buckling reduction factor about y-y; χ_y | struct.en1993-member, struct.ltb-interaction | ok |
| `chi_z` | `chi_z:reduction-factor-z` | Plain number | — | Buckling reduction factor about z-z; χ_z | struct.en1993-member, struct.ltb-interaction | ok |
| `cos_phi` | `cos_phi:power-factor` | Plain number | — | Motor power factor at rated load; Power factor (AC); Power factor (not used for DC) | elec.conductor-sizing, elec.motor-start, elec.voltage-drop | ok |
| `cos_phi_st` | — | Plain number | — | Power factor at locked rotor (starting) | elec.motor-start | ok |
| `cos_phi_target` | — | Plain number | — | Target power factor after correction | elec.motor-start | ok |
| `cos_phi_vfd` | — | Plain number | — | Drive line displacement power factor while starting | elec.motor-start | ok |
| `cosphi` | — | Plain number | — | Power factor | elec.load-budget | ok |
| `cosphi_add` | — | Plain number | — | Power factor of the additional load | elec.load-budget | ok |
| `count` | — | Plain number | — | Number of cables | elec.containment | ok |
| `cp_c` | — | Specific heat | J/(kg·K) | Cold-side specific heat | bs.heat-exchanger | ok |
| `cp_h` | — | Specific heat | J/(kg·K) | Hot-side specific heat | bs.heat-exchanger | ok |
| `cp_load` | — | Specific heat | J/(kg·K) | Specific heat of the fluid in the heat-load flow (optional; blank = 4… | bs.pipe-pump | ok |
| `cpk` | — | Plain number | — | C_pk | mech.tolerance-stack | ok |
| `cpk_fill_max` | — | Plain number | — | C_pk: gland fill above the upper limit (hot end) | fp.oring-gland | ok |
| `cpk_fill_min` | — | Plain number | — | C_pk: gland fill below the lower limit (cold end) | fp.oring-gland | ok |
| `cpk_gap` | — | Plain number | — | C_pk: extrusion gap above the permitted gap | fp.oring-gland | ok |
| `cpk_req` | — | Plain number | — | Required C_pk | mech.tolerance-stack | ok |
| `cpk_sq_max` | — | Plain number | — | C_pk: squeeze above the upper limit (hot end) | fp.oring-gland | ok |
| `cpk_sq_min` | — | Plain number | — | C_pk: squeeze below the lower limit (cold end) | fp.oring-gland | ok |
| `cpk_stretch` | — | Plain number | — | C_pk: installed stretch outside its limits | fp.oring-gland | ok |
| `csa` | — | Area | mm² | Conductor cross-sectional area | elec.voltage-drop | ok |
| `d` | `d:diameter` | Length | mm | Diameter; Diameter of a circle or tube; Interface (nominal) diameter; … (6 descriptions) | mech.bolted-joint, mech.press-fit, mech.shaft, mech.shaft-stress, mech.spring, struct.section-properties | RESOLVE (3 meanings) |
| `d` | `d:thickness` | Length | mm | Thickness | bs.glaser, bs.room-heat-loss | RESOLVE (3 meanings) |
| `d` | `d:duty-share` | Plain number | — | Duty (share of time on) | elec.load-budget | RESOLVE (3 meanings) |
| `dD` | — | Length | mm | Interference on the diameter: how much the ring is widened to pass th… | mech.snap-fit | ok |
| `dD_p` | — | Length | mm | Diametral growth under the operating pressure of the bore (piston sea… | fp.oring-gland | ok |
| `dL` | — | Length | mm | Thermal movement between the anchors (magnitude) | bs.pipe-expansion | ok |
| `dL_rated` | — | Length | mm | Rated axial movement of the bellows (optional; blank = movement not c… | bs.pipe-expansion | ok |
| `dL_signed` | — | Length | mm | Thermal movement, signed | bs.pipe-expansion | ok |
| `dT` | `dT:temperature-difference` | Temperature, temperature difference | K | Allowed temperature rise above ambient; Design temperature difference; Mean temperature difference; … (6 descriptions) | bs.pipe-expansion, bs.room-heat-loss, bs.sealed-system, elec.pcb-trace, mech.press-fit, mech.thermal-expansion | ok |
| `dT_1` | — | Temperature, temperature difference | K | Rise across stage 1 | elec.thermal-chain | ok |
| `dT_2` | — | Temperature, temperature difference | K | Rise across stage 2 | elec.thermal-chain | ok |
| `dT_3` | — | Temperature, temperature difference | K | Rise across stage 3 | elec.thermal-chain | ok |
| `dT_app` | — | Temperature, temperature difference | K | Approach | bs.heat-exchanger | ok |
| `dT_c` | — | Temperature, temperature difference | K | Cold-side rise | bs.heat-exchanger | ok |
| `dT_fr` | — | Temperature, temperature difference | K | Flow-to-return temperature difference | bs.sealed-system | ok |
| `dT_in` | — | Temperature, temperature difference | K | Temperature change ΔT (negative for a cold line), when entered direct… | bs.pipe-expansion | ok |
| `dT_lm` | — | Temperature, temperature difference | K | Log-mean temperature difference | bs.heat-exchanger | ok |
| `dT_load` | — | Temperature, temperature difference | K | Flow-to-return temperature difference across the load | bs.pipe-pump | ok |
| `dT_m` | — | Temperature, temperature difference | K | Thermal margin | elec.thermal-chain | ok |
| `dT_min` | — | Temperature, temperature difference | K | Minimum approach temperature difference | bs.heat-exchanger | ok |
| `dT_nat` | — | Temperature, temperature difference | K | Natural temperature rise | elec.enclosure-heat | ok |
| `dT_tot` | — | Temperature, temperature difference | K | Total rise | elec.thermal-chain | ok |
| `dV` | `dV:volume-change` | Volume, section modulus | L | Liquid volume delivered, absorbed or needed for the emergency stroke; Swept volume per stroke (the larger) | fp.accumulator, fp.cylinder | RESOLVE (2 meanings) |
| `dV` | `dV:voltage-drop` | Voltage | V | Voltage drop | elec.voltage-drop | RESOLVE (2 meanings) |
| `dV_cab` | — | Voltage | V | Cable drop along the starting voltage | elec.motor-start | ok |
| `dV_ext` | — | Volume, section modulus | L | Extend swept volume | fp.cylinder | ok |
| `dV_pct` | — | Plain number | — | Voltage drop (%) | elec.voltage-drop | ok |
| `dV_ret` | — | Volume, section modulus | L | Retract swept volume | fp.cylinder | ok |
| `d_0` | — | Length | mm | Hole diameter (optional; blank = normal clearance d + 2, or d + 3 fro… | struct.connection-ec3 | ok |
| `d_1` | — | Plain number | — | Damage of block 1 | mech.fatigue-miner | ok |
| `d_2` | — | Plain number | — | Damage of block 2 | mech.fatigue-miner | ok |
| `d_3` | — | Plain number | — | Damage of block 3 | mech.fatigue-miner | ok |
| `d_A` | — | Length | mm | Seat diameter at A | mech.shaft | ok |
| `d_B` | — | Length | mm | Seat diameter at B | mech.shaft | ok |
| `d_e1` | — | Length | mm | δ max, member 1 | struct.frame-2d | ok |
| `d_h` | `d_h:hole-diameter` | Length | mm | Hole diameter | mech.bolted-joint, mech.lifting-lug | ok |
| `d_i` | `d_i:bore-diameter` | Length | mm | Bore (hollow); Shaft bore diameter (0 for a solid shaft) | mech.press-fit, mech.shaft | ok |
| `d_max` | `d_max:maximum-deflection` | Length | mm | δ max | struct.frame-2d | RESOLVE (2 meanings) |
| `d_max` | `d_max:maximum-wire-diameter` | Length | mm | Maximum wire diameter; Maximum wire diameter, for the EN 13906-1 solid length (optional; bla… | mech.spring | RESOLVE (2 meanings) |
| `d_min_prs` | — | Length | mm | Smallest pressure line bore | fp.cylinder | ok |
| `d_min_rtn` | — | Length | mm | Smallest return line bore | fp.cylinder | ok |
| `d_min_suc` | — | Length | mm | Smallest suction line bore | fp.cylinder | ok |
| `d_o` | `d_o:hub-outer-diameter` | Length | mm | Hub outer diameter | mech.press-fit | RESOLVE (2 meanings) |
| `d_o` | `d_o:orifice-diameter` | Length | mm | Orifice diameter, which gives the flow it passes | fp.cylinder | RESOLVE (2 meanings) |
| `d_o_req` | — | Length | mm | Orifice diameter for the circuit flow | fp.cylinder | ok |
| `d_p` | — | Length | mm | Pin diameter | mech.lifting-lug | ok |
| `d_prs` | — | Length | mm | Pressure line inside diameter; Pressure line inside diameter, to the cylinder cap port and judged at… | fp.cylinder | ok |
| `d_r` | `d_r:ring-inside-diameter` | Length | mm | Ring inside diameter d (mean of the supplier's tolerance) | fp.oring-gland | RESOLVE (2 meanings) |
| `d_r` | `d_r:rod-diameter` | Length | mm | Rod diameter | fp.cylinder | RESOLVE (2 meanings) |
| `d_r_tol` | — | Length | mm | Ring inside diameter tolerance, plus or minus | fp.oring-gland | ok |
| `d_rec_prs` | — | Length | mm | Recommended pressure line bore | fp.cylinder | ok |
| `d_rec_rtn` | — | Length | mm | Recommended return line bore | fp.cylinder | ok |
| `d_rec_suc` | — | Length | mm | Recommended suction line bore | fp.cylinder | ok |
| `d_ret` | — | Length | mm | Return line inside diameter | fp.cylinder | ok |
| `d_shaft_max` | — | Length | mm | Shaft maximum | mech.iso286-fit | ok |
| `d_shaft_min` | — | Length | mm | Shaft minimum | mech.iso286-fit | ok |
| `d_suc` | — | Length | mm | Suction line inside diameter | fp.cylinder | ok |
| `d_w` | — | Length | mm | Head / washer bearing diameter | mech.bolted-joint | ok |
| `days` | — | Time | d | Days in the month (blank = calendar days, rows in January to December… | bs.glaser | ok |
| `defl_ratio` | — | Plain number | — | Deflection limit as span/ratio (span = diameter of a circular plate, … | struct.plate-bending | ok |
| `delta` | `delta:actual-movement` | Length | mm | Actual movement | mech.thermal-expansion | RESOLVE (2 meanings) |
| `delta` | `delta:diametral-interference` | Length | µm | Diametral interference | mech.press-fit | RESOLVE (2 meanings) |
| `delta_e` | — | Length | mm | Engaged movement | mech.thermal-expansion | ok |
| `delta_eff` | — | Length | µm | Effective interference after smoothing | mech.press-fit | ok |
| `delta_free` | — | Length | mm | Free movement | mech.thermal-expansion | ok |
| `delta_limit` | — | Length | mm | Deflection limit (optional; blank = no deflection check) | struct.beam-formulas | ok |
| `delta_max` | — | Length | mm | Maximum deflection | struct.beam-formulas | ok |
| `device_code` | `device_code:protective-device-code` | Plain number | — | Device type code carried from the cable step (1 MCB type B, 2 type C,…; Protective device type code | elec.conductor-sizing, elec.fault-protection | ok |
| `dew_margin` | — | Temperature, temperature difference | K | Allowance of the surface above the dew point, for a cold surface (opt… | bs.insulation | ok |
| `diagram_month` | — | Plain number | — | Month to draw the Glaser diagram for (optional; blank = the month of … | bs.glaser | ok |
| `dip_limit` | — | Percent quantity | % | Dip limit at the motor terminals | elec.motor-start | ok |
| `dip_limit_pcc` | — | Percent quantity | % | Dip limit at the point of common coupling (when the network operator … | elec.motor-start | ok |
| `dip_pcc` | — | Percent quantity | % | Voltage dip at the point of common coupling | elec.motor-start | ok |
| `dish` | — | Plain number | — | Dishing ratio | struct.lifting | ok |
| `dish_max` | — | Plain number | — | Dishing-ratio limit on the hole diameter over the thickness (optional… | struct.lifting | ok |
| `dpL_s1` | — | Friction rate | Pa/m | Δp/L, main | bs.duct-sizing | ok |
| `dp_all` | — | Stress, pressure | Pa | Σ all sections | bs.duct-sizing | ok |
| `dp_eq` | — | Stress, pressure | Pa | Equipment / terminal loss | bs.duct-sizing | ok |
| `dp_f` | — | Plain number | — | Safety factor on the vapour-pressure difference Δp = Δv·R_v·(T_i + T_… | bs.glaser | ok |
| `dp_f_s1` | — | Stress, pressure | Pa | Friction loss, main | bs.duct-sizing | ok |
| `dp_fit_s1` | — | Stress, pressure | Pa | Fitting loss, main | bs.duct-sizing | ok |
| `dp_index` | — | Stress, pressure | Pa | Index-run Δp | bs.duct-sizing | ok |
| `dp_limit` | — | Stress, pressure | Pa | Total pressure-drop limit (check) | bs.duct-sizing | ok |
| `dp_m` | — | Stress, pressure | bar | Pressure drop across the motor | fp.cylinder | ok |
| `dp_o` | — | Stress, pressure | bar | Pressure drop across the orifice (optional; blank = no orifice screen… | fp.cylinder | ok |
| `dp_relief` | — | Stress, pressure | kPa | Cracking pressure of the breather or relief element a sealed enclosur… | elec.enclosure-heat | ok |
| `dp_s1` | — | Stress, pressure | Pa | Section loss, main | bs.duct-sizing | ok |
| `dp_sys` | — | Stress, pressure | kPa | System Δp | bs.pipe-pump | ok |
| `dp_target` | — | Friction rate | Pa/m | Target friction rate (equal friction) | bs.duct-sizing | ok |
| `dp_total` | — | Stress, pressure | kPa | Friction Δp | bs.pipe-pump | ok |
| `dpmo` | — | Plain number | — | DPMO (predicted) | mech.tolerance-stack | ok |
| `duty` | `duty:duty-factor` | Plain number | — | Duty factor (0–1) | elec.enclosure-heat | RESOLVE (2 meanings) |
| `duty` | `duty:thermal-duty` | Plain number | — | Thermal duty | elec.motor-sizing | RESOLVE (2 meanings) |
| `dv_a` | — | Density | g/m³ | Moisture excess Δv at the cold point (humidity-class record) | bs.glaser | ok |
| `dv_b` | — | Density | g/m³ | Moisture excess Δv at the warm point (humidity-class record) | bs.glaser | ok |
| `dv_limit` | — | Plain number | — | Voltage-drop limit | elec.voltage-drop | ok |
| `dz` | — | Length | m | Rise in elevation (+ up) | bs.pipe-pump | ok |
| `e_1` | — | Length | mm | End distance (load direction) | struct.connection-ec3 | ok |
| `e_2` | — | Length | mm | Edge distance (across the load) | struct.connection-ec3 | ok |
| `e_f` | — | Length | mm | Face-seal gap between the flanges (face seal only), which is the extr… | fp.oring-gland | ok |
| `e_f_l` | — | Length | mm | Face-seal gap, lower deviation | fp.oring-gland | ok |
| `e_f_u` | — | Length | mm | Face-seal gap, upper deviation | fp.oring-gland | ok |
| `e_fluid` | — | Plain number | — | Expansion coefficient from a glycol record (optional; blank = water) | bs.sealed-system | ok |
| `e_head` | — | Length | mm | Across-corners dimension of the smaller of bolt head and nut (optiona… | struct.connection-ec3 | ok |
| `e_lim` | — | Plain number | — | Load ratio limit e | mech.bearing-life | ok |
| `e_min` | — | Length | mm | Edge distance of the bolt row, used only with prying (optional; blank… | struct.base-plate | ok |
| `e_off` | — | Length | mm | Offset e of the slide line above the crank centre, or of the cam foll… | mech.mechanism | ok |
| `e_x` | `e_x:point-of-application-x` | Length | mm | Point of application, x | mech.bolt-group, mech.weld-group | RESOLVE (2 meanings) |
| `e_x` | `e_x:expansion-coefficient` | Plain number | — | Expansion coefficient | bs.sealed-system | RESOLVE (2 meanings) |
| `e_y` | `e_y:point-of-application-y` | Length | mm | Point of application, y | mech.bolt-group, mech.weld-group | ok |
| `ecc` | — | Length | mm | Gland eccentricity or run-out (one-sided offset; it deepens the gland… | fp.oring-gland | ok |
| `eff` | — | Plain number | — | Saturation efficiency for evaporative humidification (optional; blank… | bs.psychrometrics | ok |
| `ei_shaft` | — | Length | µm | Shaft lower deviation | mech.iso286-fit | ok |
| `ei_shaft_override` | — | Length | µm | Shaft lower deviation override (optional; blank = the formula value) | mech.iso286-fit | ok |
| `eps` | `eps:effectiveness` | Plain number | — | Effectiveness | bs.heat-exchanger | RESOLVE (4 meanings) |
| `eps` | `eps:emissivity` | Plain number | — | Emissivity of the outer surface (for the correlation; with h_se as gi… | bs.insulation | RESOLVE (4 meanings) |
| `eps` | `eps:roughness` | Length | mm | Roughness | bs.pipe-pump | RESOLVE (4 meanings) |
| `eps` | `eps:strain` | Plain number | — | Strain | mech.snap-fit | RESOLVE (4 meanings) |
| `eps_max` | — | Percent quantity | % | Maximum strain | mech.snap-fit | ok |
| `eps_perm` | — | Percent quantity | % | Permissible strain | mech.snap-fit | ok |
| `eps_perm_once` | — | Plain number | — | Permissible strain for one-time joining, as a fraction, for example 0… | mech.snap-fit | ok |
| `eps_perm_repeated` | — | Plain number | — | Permissible strain for repeated joining, as a fraction (no default) | mech.snap-fit | ok |
| `eps_rough` | — | Length | mm | Duct roughness | bs.duct-sizing | ok |
| `es_shaft` | — | Length | µm | Shaft upper deviation | mech.iso286-fit | ok |
| `es_shaft_override` | — | Length | µm | Shaft upper deviation override (optional; blank = the formula value) | mech.iso286-fit | ok |
| `eta` | `eta:efficiency` | Plain number | — | Efficiency; Efficiency (not used for DC); Mechanism efficiency (overall); … (4 descriptions) | elec.conductor-sizing, elec.load-budget, elec.motor-sizing, elec.motor-start | RESOLVE (3 meanings) |
| `eta` | `eta:perry-factor` | Plain number | — | Perry factor | struct.column-buckling | RESOLVE (3 meanings) |
| `eta` | `eta:shear-area-factor` | Plain number | — | Shear-area factor | struct.en1993-member | RESOLVE (3 meanings) |
| `eta_add` | — | Plain number | — | Efficiency of the additional load | elec.load-budget | ok |
| `eta_b` | — | Plain number | — | Battery discharge efficiency | elec.load-budget | ok |
| `eta_conv` | — | Plain number | — | Conversion efficiency battery → loads | elec.load-budget | ok |
| `eta_hm` | — | Plain number | — | Hydromechanical efficiency of the motor | fp.cylinder | ok |
| `eta_m` | — | Plain number | — | Mechanical efficiency of the cylinder, from seal and guide friction | fp.cylinder | ok |
| `eta_psu` | — | Plain number | — | DC power supply efficiency (power factor taken as 1) | elec.load-budget | ok |
| `eta_pump` | — | Plain number | — | Pump efficiency at duty (for shaft power) | bs.pipe-pump | ok |
| `eta_tp` | — | Plain number | — | Total efficiency of the pump (volumetric × hydromechanical) | fp.cylinder | ok |
| `eta_vc` | — | Plain number | — | Volumetric efficiency of the cylinder, from leakage past the piston | fp.cylinder | ok |
| `eta_vp` | — | Plain number | — | Volumetric efficiency of the pump | fp.cylinder | ok |
| `f` | — | Plain number | — | Temperature factor f (blank = 1) | bs.room-heat-loss | ok |
| `f1` | — | Rotational speed, frequency | Hz | Fundamental frequency | struct.plate-bending | ok |
| `fRsi` | — | Plain number | — | Temperature factor of the internal surface | bs.glaser | ok |
| `fRsi_min` | — | Plain number | — | Minimum temperature factor | bs.glaser | ok |
| `f_T` | — | Plain number | — | Gas volume scaling T_max/T_min | fp.accumulator | ok |
| `f_acc` | — | Plain number | — | Acceleration (and deceleration) fraction of the move time | elec.motor-sizing | ok |
| `f_add` | — | Percent quantity | % | Additional allowance on the room load (CIBSE DHDG method only) | bs.room-heat-loss | ok |
| `f_b_pin` | — | Stress, pressure | MPa | Allowable bending stress, pin | mech.lifting-lug | ok |
| `f_bottom` | — | Plain number | — | Bottom exposure (0 = on the floor) | elec.enclosure-heat | ok |
| `f_br` | — | Stress, pressure | MPa | Allowable bearing stress, lug | mech.lifting-lug | ok |
| `f_cd` | — | Stress, pressure | MPa | Concrete design strength | struct.base-plate | ok |
| `f_ck` | — | Stress, pressure | MPa | Concrete characteristic cylinder strength | struct.base-plate | ok |
| `f_ck_g` | — | Stress, pressure | MPa | Grout characteristic strength (optional; blank = the β_j = 2/3 condit… | struct.base-plate | ok |
| `f_dT` | — | Plain number | — | Output correction factor | bs.sealed-system | ok |
| `f_derate` | — | Plain number | — | PSU derating factor at the ambient and altitude | elec.load-budget | ok |
| `f_embed` | — | Length | µm | Embedding (settling) loss | mech.bolted-joint | ok |
| `f_front` | — | Plain number | — | Front exposure | elec.enclosure-heat | ok |
| `f_in` | — | Rotational speed, frequency | Hz | Input (crank) frequency, for a spring surge check | mech.mechanism | ok |
| `f_jd` | — | Stress, pressure | MPa | Design bearing strength of the joint | struct.base-plate | ok |
| `f_left` | — | Plain number | — | Left side exposure | elec.enclosure-heat | ok |
| `f_max` | — | Plain number | — | Space factor limit (fill limit, from your record) | elec.containment | ok |
| `f_n` | — | Rotational speed, frequency | Hz | Surge frequency | mech.spring | ok |
| `f_op` | — | Rotational speed, frequency | Hz | Operating (excitation) frequency | mech.spring | ok |
| `f_r_max` | — | Line force, distributed load, stiffness | N/mm | Maximum force per unit length | mech.weld-group | ok |
| `f_rear` | — | Plain number | — | Rear exposure (0 = wall-mounted) | elec.enclosure-heat | ok |
| `f_rg` | — | Plain number | — | Real-gas factor on V0, the real over the ideal gas volume for this pr… | fp.accumulator | ok |
| `f_right` | — | Plain number | — | Right side exposure | elec.enclosure-heat | ok |
| `f_rss` | — | Plain number | — | Modified-RSS factor (Bender-style) | mech.tolerance-stack | ok |
| `f_s1` | `f_s1:friction-factor` | Plain number | — | f, main; f, oil line | bs.duct-sizing, bs.pipe-pump | ok |
| `f_sigma` | — | Plain number | — | Permissible bending stress as a fraction of R_m (torsion, hooks; gene… | mech.spring | ok |
| `f_su` | — | Plain number | — | Ultimate shear strength as a fraction of R_m (Goodman, shear; generic… | mech.spring | ok |
| `f_t` | — | Stress, pressure | MPa | Allowable tensile stress, lug | mech.lifting-lug | ok |
| `f_tau` | — | Plain number | — | Permissible shear stress as a fraction of R_m (generic textbook defau… | mech.spring | ok |
| `f_top` | — | Plain number | — | Top exposure (1 free, 0 covered) | elec.enclosure-heat | ok |
| `f_u` | `f_u:ultimate-strength` | Stress, pressure | MPa | Plate ultimate tensile strength; Ultimate tensile strength (optional); Ultimate tensile strength (optional; blank = the sheet is blocked whe…; … (5 descriptions) | mech.material-margin, struct.connection-ec3, struct.en1993-member, struct.fillet-weld, struct.lifting | ok |
| `f_ub` | `f_ub:bolt-ultimate-strength` | Stress, pressure | MPa | Anchor bolt ultimate tensile strength; Bolt ultimate tensile strength | struct.base-plate, struct.connection-ec3 | ok |
| `f_v` | — | Stress, pressure | MPa | Allowable shear stress, lug | mech.lifting-lug | ok |
| `f_v_pin` | — | Stress, pressure | MPa | Allowable shear stress, pin | mech.lifting-lug | ok |
| `f_vw_d` | — | Stress, pressure | MPa | Design shear strength | struct.fillet-weld | ok |
| `f_w` | — | Stress, pressure | MPa | Design strength of the throat | mech.weld-group | ok |
| `f_y` | `f_y:yield-strength` | Stress, pressure | MPa | Plate yield strength; Yield strength; Yield strength for the thickness; … (5 descriptions) | fp.cylinder, mech.material-margin, struct.base-plate, struct.connection-ec3, struct.en1993-member, struct.lifting, struct.ltb-interaction, struct.plate-bending | ok |
| `f_yb` | `f_yb:bolt-yield-strength` | Stress, pressure | MPa | Anchor bolt yield strength (α_bc is valid for 235–640 MPa); Bolt yield strength | struct.base-plate, struct.connection-ec3 | ok |
| `f_yd` | — | Stress, pressure | MPa | Design yield strength | struct.plate-bending | ok |
| `factor` | — | Plain number | — | Factor | struct.frame-2d | ok |
| `fill` | — | Percent quantity | % | Fill | elec.containment | ok |
| `fill_hi` | — | Percent quantity | % | Gland fill: highest acceptable | fp.oring-gland | ok |
| `fill_lo` | — | Percent quantity | % | Gland fill: lowest acceptable | fp.oring-gland | ok |
| `fill_max` | — | Percent quantity | % | Largest gland fill | fp.oring-gland | ok |
| `fill_min` | — | Percent quantity | % | Smallest gland fill | fp.oring-gland | ok |
| `follower_force` | — | Force | N | Peak force perpendicular to the rocker at the output pin, the drive f… | mech.mechanism | ok |
| `g` | `g:lug-clearance` | Length | mm | Clearance, lug face to cheek plate (each side) | mech.lifting-lug | RESOLVE (2 meanings) |
| `g` | `g:restraint-gap` | Length | mm | Gap closed before the restraint engages | mech.thermal-expansion | RESOLVE (2 meanings) |
| `gamma_G_inf` | — | Plain number | — | γ_G,inf (favourable permanent) | struct.load-combinations | ok |
| `gamma_G_sup` | — | Plain number | — | γ_G,sup (unfavourable permanent) | struct.load-combinations | ok |
| `gamma_M0` | `gamma_M0:partial-factor-cross-section` | Plain number | — | Partial factor for cross-section resistance; Partial factor for the plate; Partial factor on the resistance of the cross-section | struct.base-plate, struct.connection-ec3, struct.en1993-member, struct.plate-bending | ok |
| `gamma_M1` | `gamma_M1:partial-factor-member-buckling` | Plain number | — | Partial factor for member buckling | struct.en1993-member, struct.ltb-interaction | ok |
| `gamma_M2` | `gamma_M2:partial-factor-fracture` | Plain number | — | Partial factor for bolt and weld resistance; Partial factor for net-section fracture; Partial factor for the anchor bolts; … (4 descriptions) | struct.base-plate, struct.connection-ec3, struct.en1993-member, struct.fillet-weld | ok |
| `gamma_Q` | — | Plain number | — | γ_Q (variable) | struct.load-combinations | ok |
| `gamma_c` | — | Plain number | — | Partial factor for concrete | struct.base-plate | ok |
| `gap` | — | Length | mm | Largest permitted gap (diametral for a radial seal, axial for a face … | fp.oring-gland | ok |
| `gap_allow` | — | Length | mm | Largest permitted extrusion gap at this pressure and hardness, if you…; Permitted extrusion gap, diametral for a radial seal | fp.oring-gland | ok |
| `gap_max` | — | Length | mm | Largest extrusion gap, diametral for a radial seal (over the temperat… | fp.oring-gland | ok |
| `gap_min` | — | Length | mm | Smallest extrusion gap | fp.oring-gland | ok |
| `h` | `h:depth` | Length | mm | Column depth, along the plate length L (I/H and RHS); Depth for the shear area (optional; blank = use A_v); Depth or vertical leg; … (5 descriptions) | struct.base-plate, struct.en1993-member, struct.ltb-interaction, struct.section-properties | RESOLVE (5 meanings) |
| `h` | `h:hole-to-weld-distance` | Length | mm | Distance from the hole centre to the weld or base, for the out-of-pla… | struct.lifting | RESOLVE (5 meanings) |
| `h` | `h:key-height` | Length | mm | Key height | mech.shaft | RESOLVE (5 meanings) |
| `h` | `h:lift` | Length | mm | Lift h (rise, return; ignored for a dwell) | mech.mechanism | RESOLVE (5 meanings) |
| `h` | `h:root-thickness` | Length | mm | Thickness at the root (cantilever); wall thickness of the ring (annul… | mech.snap-fit | RESOLVE (5 meanings) |
| `h_1` | `h_1:side-1-film-coefficient` | Heat transfer coefficient, U-value | W/(m²·K) | Side 1 film coefficient (optional; blank = no film, so T_1 is the sur… | bs.plane-wall | RESOLVE (2 meanings) |
| `h_1` | `h_1:specific-enthalpy-1` | Plain number | — | Enthalpy, state 1, in kilojoules per kilogram of dry air | bs.psychrometrics | RESOLVE (2 meanings) |
| `h_2` | — | Heat transfer coefficient, U-value | W/(m²·K) | Side 2 film coefficient (optional; blank = no film, so T_2 is the sur… | bs.plane-wall | ok |
| `h_air` | — | Plain number | — | Specific enthalpy (kJ per kg dry air) | bs.psychrometrics | ok |
| `h_conv` | — | Heat transfer coefficient, U-value | W/(m²·K) | Overall heat-transfer coefficient of the enclosure surface (editable … | elec.enclosure-heat | ok |
| `h_f` | — | Length | mm | Face-seal groove depth (face seal only), where the ring is compressed… | fp.oring-gland | ok |
| `h_f_l` | — | Length | mm | Face-seal groove depth, lower deviation | fp.oring-gland | ok |
| `h_f_s1` | — | Length | m | h_f, oil line | bs.pipe-pump | ok |
| `h_f_total` | — | Length | m | Σ h_f | bs.pipe-pump | ok |
| `h_f_u` | — | Length | mm | Face-seal groove depth, upper deviation | fp.oring-gland | ok |
| `h_i` | — | Heat transfer coefficient, U-value | W/(m²·K) | Inner film coefficient (optional; blank = the pipe wall is at the pro… | bs.insulation | ok |
| `h_out` | — | Length | m | Pressure head required at the outlet | bs.pipe-pump | ok |
| `h_ratio` | — | Plain number | — | Tip thickness / root thickness (tapered thickness) | mech.snap-fit | ok |
| `h_res` | — | Plain number | — | Parallel resonance order | elec.motor-start | ok |
| `h_se` | — | Heat transfer coefficient, U-value | W/(m²·K) | Outer surface coefficient (as given; generic default of 10 W/(m²K), r… | bs.insulation | ok |
| `h_static` | — | Length | m | Static height above the vessel | bs.sealed-system | ok |
| `h_steam` | — | Plain number | — | Steam enthalpy (steam humidification) | bs.psychrometrics | ok |
| `heat` | — | Plain number | — | Share of the input power released as heat inside the enclosure (blank… | elec.load-budget | ok |
| `heat_add` | — | Plain number | — | Share of the additional load's input power released inside the enclos… | elec.load-budget | ok |
| `hole_grade` | — | Plain number | — | Hole IT grade | mech.iso286-fit | ok |
| `i` | `i:gear-ratio` | Plain number | — | Gear ratio (motor : output, 1 = none) | elec.motor-sizing | RESOLVE (2 meanings) |
| `i` | `i:node-index` | Plain number | — | Node i | struct.frame-2d | RESOLVE (2 meanings) |
| `i_max` | — | Length | µm | Maximum interference | mech.iso286-fit | ok |
| `i_min` | — | Length | µm | Minimum interference | mech.iso286-fit | ok |
| `id` | — | Plain number | — | Element; Node | struct.frame-2d | ok |
| `input_torque` | — | Moment, torque | N·m | Peak input (crank) torque by virtual work | mech.mechanism | ok |
| `inrush` | — | Plain number | — | Inrush multiple | elec.load-budget | ok |
| `inrush_add` | — | Plain number | — | Starting multiple of the additional load | elec.load-budget | ok |
| `j` | — | Plain number | — | Node j | struct.frame-2d | ok |
| `k` | `k:adiabatic-factor` | Plain number | — | Adiabatic factor (derived) | elec.fault-protection | RESOLVE (7 meanings) |
| `k` | `k:diversity-factor` | Plain number | — | Diversity | elec.load-budget | RESOLVE (7 meanings) |
| `k` | `k:effective-length-factor` | Plain number | — | Effective length factor for end rotation about the minor axis | struct.ltb-interaction | RESOLVE (7 meanings) |
| `k` | `k:half-band-standard-deviations` | Plain number | — | Standard deviations per half-band (normal contributors) | mech.tolerance-stack | RESOLVE (7 meanings) |
| `k` | `k:rate` | Line force, distributed load, stiffness | N/mm | Rate | mech.spring | RESOLVE (7 meanings) |
| `k` | `k:thermal-conductivity` | Thermal conductivity | W/(m·K) | Thermal conductivity | bs.plane-wall | RESOLVE (7 meanings) |
| `k` | `k:transmission-constant` | Length | mm | Transmission constant | elec.motor-sizing | RESOLVE (7 meanings) |
| `k_1` | — | Plain number | — | Bearing factor k₁ | struct.connection-ec3 | ok |
| `k_P` | `k_P:load-ratio` | Plain number | — | Load ratio | mech.bearing-life-basic | RESOLVE (2 meanings) |
| `k_P` | `k_P:members-stiffness` | Line force, distributed load, stiffness | kN/m | Members' stiffness | mech.bolted-joint | RESOLVE (2 meanings) |
| `k_P_given` | — | Line force, distributed load, stiffness | kN/m | Clamped members' stiffness, measured or from an FE model (optional; b… | mech.bolted-joint | ok |
| `k_R` | — | Plain number | — | Conductor resistance factor on ρ₂₀ for R₁ + R₂ (1 = the resistivity i… | elec.fault-protection | ok |
| `k_S` | — | Line force, distributed load, stiffness | kN/m | Bolt stiffness | mech.bolted-joint | ok |
| `k_S_given` | — | Line force, distributed load, stiffness | kN/m | Bolt stiffness, measured or from an FE model (optional; blank = from … | mech.bolted-joint | ok |
| `k_T` | `k_T:torque-factor-at-dipped-voltage` | Plain number | — | Torque factor at the dipped voltage | elec.motor-start | RESOLVE (2 meanings) |
| `k_T` | `k_T:transformer-overload-factor` | Plain number | — | Transformer short-time overload allowed for inrush | elec.load-budget | RESOLVE (2 meanings) |
| `k_a` | — | Plain number | — | Surface factor override (optional; blank = a·S_ut^b, Shigley 10th ed.… | mech.shaft | ok |
| `k_age` | — | Plain number | — | Ageing (end-of-life) factor | elec.load-budget | ok |
| `k_b` | `k_b:bearing-multiple` | Plain number | — | Bearing multiple of the allowable stress on the projected pin area (o… | struct.lifting | RESOLVE (2 meanings) |
| `k_b` | `k_b:bellows-spring-rate` | Line force, distributed load, stiffness | kN/m | Axial bellows spring rate (optional; blank = no spring force; manufac… | bs.pipe-expansion | RESOLVE (2 meanings) |
| `k_bar` | — | Line force, distributed load, stiffness | kN/m | Bar stiffness | mech.thermal-expansion | ok |
| `k_c` | `k_c:f-modification-factor` | Plain number | — | Correction factor for the f modification (optional; blank = 1/(1.33 −… | struct.ltb-interaction | RESOLVE (2 meanings) |
| `k_c` | `k_c:stress-correction-factor` | Plain number | — | Stress correction factor | mech.spring | RESOLVE (2 meanings) |
| `k_d` | — | Plain number | — | Temperature factor | mech.shaft | ok |
| `k_e1` | — | Plain number | — | Minimum e₁/d₀ | struct.connection-ec3 | ok |
| `k_e2` | — | Plain number | — | Minimum e₂/d₀ | struct.connection-ec3 | ok |
| `k_ext` | — | Plain number | — | Curve-fit constant k, external layer | elec.pcb-trace | ok |
| `k_f` | — | Plain number | — | Miscellaneous factor | mech.shaft | ok |
| `k_int` | — | Plain number | — | Curve-fit constant k, internal layer | elec.pcb-trace | ok |
| `k_n` | — | Plain number | — | Derating of the cable limits for more than 3 cables on one grip (from… | elec.containment | ok |
| `k_p` | — | Plain number | — | Peukert exponent | elec.load-budget | ok |
| `k_p1` | — | Plain number | — | Minimum p₁/d₀ | struct.connection-ec3 | ok |
| `k_p2` | — | Plain number | — | Minimum p₂/d₀ | struct.connection-ec3 | ok |
| `k_proof` | — | Plain number | — | Proof-load factor on the rated load (optional; blank = no proof load … | struct.lifting | ok |
| `k_r` | — | Moment, torque | kN·m | Spring k_r (per radian) | struct.frame-2d | ok |
| `k_s` | — | Line force, distributed load, stiffness | kN/m | Restraint stiffness (optional; blank = a rigid restraint) | mech.thermal-expansion | ok |
| `k_sigma` | — | Plain number | — | Standard deviations per half-band (normal tolerances and measured nor… | fp.oring-gland | ok |
| `k_soft` | — | Plain number | — | Soft-starter current limit as a multiple of the rated current | elec.motor-start | ok |
| `k_sp` | — | Line force, distributed load, stiffness | kN/m | Return spring rate | mech.mechanism | ok |
| `k_svc` | — | Plain number | — | Service-class factor on the load (optional; blank = 1.0; user record) | struct.lifting | ok |
| `k_temp` | — | Plain number | — | Temperature capacity factor | elec.load-budget | ok |
| `k_u` | — | Plain number | — | Ratio of the ultimate to the yield safety factor required | mech.material-margin | ok |
| `k_vfd` | — | Plain number | — | Drive line-current limit as a multiple of the rated current | elec.motor-start | ok |
| `k_w` | — | Plain number | — | Effective length factor for warping | struct.ltb-interaction | ok |
| `k_wall` | — | Plain number | — | Wall ratio | mech.thick-cylinder | ok |
| `k_x` | — | Line force, distributed load, stiffness | kN/m | Spring k_x | struct.frame-2d | ok |
| `k_y` | — | Line force, distributed load, stiffness | kN/m | Spring k_y | struct.frame-2d | ok |
| `k_yy` | `k_yy:interaction-factor-yy` | Plain number | — | Interaction factor for bending about y-y in the member check; Interaction factor for major-axis bending in equation 6.61 | struct.en1993-member, struct.ltb-interaction | ok |
| `k_yz` | — | Plain number | — | Interaction factor for minor-axis bending in equation 6.61 | struct.ltb-interaction | ok |
| `k_zy` | — | Plain number | — | Interaction factor for major-axis bending in equation 6.62 | struct.ltb-interaction | ok |
| `k_zz` | `k_zz:interaction-factor-zz` | Plain number | — | Interaction factor for bending about z-z in the member check; Interaction factor for minor-axis bending in equation 6.62 | struct.en1993-member, struct.ltb-interaction | ok |
| `kappa` | — | Plain number | — | Viscosity ratio κ, 0.1 to 100, used for a_ISO (optional; blank = κ is… | mech.bearing-life | ok |
| `kvar` | — | Apparent and reactive power | kvar | Capacitor kvar to the target power factor | elec.motor-start | ok |
| `l` | — | Length | mm | Length of the beam (cantilever); of the torsion bar: its free length … | mech.snap-fit | ok |
| `l_eff` | — | Length | mm | Effective length of the tension T-stub, total for the n_t bolts (requ… | struct.base-plate | ok |
| `l_h` | — | Plain number | — | Slenderness l/h | mech.snap-fit | ok |
| `l_k` | — | Length | mm | Clamp length | mech.bolted-joint | ok |
| `l_leg_1` | — | Length | mm | Leg length 1 (torsion) | mech.spring | ok |
| `l_leg_2` | — | Length | mm | Leg length 2 (torsion) | mech.spring | ok |
| `lam` | — | Plain number | — | Slenderness | fp.cylinder | ok |
| `lam_c` | — | Plain number | — | Transition slenderness | fp.cylinder | ok |
| `lambda` | `lambda:thermal-conductivity` | Thermal conductivity | W/(m·K) | Thermal conductivity of the insulation; Thermal conductivity λ | bs.glaser, bs.insulation, bs.room-heat-loss | RESOLVE (2 meanings) |
| `lambda` | `lambda:slenderness` | Plain number | — | Slenderness | struct.column-buckling | RESOLVE (2 meanings) |
| `lambda2` | — | Plain number | — | Frequency parameter λ² | struct.plate-bending | ok |
| `lambda_0` | — | Plain number | — | Limiting slenderness | struct.column-buckling | ok |
| `lambda_LT` | — | Plain number | — | λ̄_LT | struct.ltb-interaction | ok |
| `lambda_LT0` | — | Plain number | — | Plateau slenderness for cl. 6.3.2.3 (recommended 0.4) | struct.ltb-interaction | ok |
| `lambda_w` | — | Thermal conductivity | W/(m·K) | Pipe wall conductivity (optional; blank = not included) | bs.insulation | ok |
| `lambda_y` | `lambda_y:slenderness-y` | Plain number | — | Non-dimensional slenderness about y-y; λ̄_y | struct.en1993-member, struct.ltb-interaction | ok |
| `lambda_z` | `lambda_z:slenderness-z` | Plain number | — | Non-dimensional slenderness about z-z; λ̄_z | struct.en1993-member, struct.ltb-interaction | ok |
| `loop` | — | Plain number | — | Loop (1 = outline, 2+ = holes) | struct.section-properties | ok |
| `lower` | — | Length | mm | Lower deviation | mech.tolerance-stack | ok |
| `lug_util` | — | Plain number | — | Governing lug utilisation | struct.lifting | ok |
| `m` | `m:mass` | Mass | kg | Mass carried (gear, pulley); Moved mass (linear mechanisms) | elec.motor-sizing, mech.shaft | RESOLVE (3 meanings) |
| `m` | `m:bolt-to-weld-toe-distance` | Length | mm | Bolt to flange-weld toe distance, tension T-stub (required: it follow… | struct.base-plate | RESOLVE (3 meanings) |
| `m` | `m:sn-slope` | Plain number | — | S–N slope | mech.fatigue-miner | RESOLVE (3 meanings) |
| `mVAm` | — | Plain number | — | Voltage drop figure (mV/A/m, from the cable table) | elec.voltage-drop | ok |
| `m_c` | — | Mass flow | kg/s | Cold-side mass flow | bs.heat-exchanger | ok |
| `m_da` | — | Mass flow | kg/s | Dry-air mass flow | bs.psychrometrics | ok |
| `m_da_b` | — | Mass flow | kg/s | Second stream dry-air mass flow (mixing) | bs.psychrometrics | ok |
| `m_eff` | — | Length | mm | Thread engagement length provided | mech.bolted-joint | ok |
| `m_f` | — | Mass | kg | Follower mass (all moving parts that follow the cam) | mech.mechanism | ok |
| `m_h` | — | Mass flow | kg/s | Hot-side mass flow | bs.heat-exchanger | ok |
| `m_min` | `m_min:engagement-needed` | Length | mm | Engagement needed | mech.bolted-joint | RESOLVE (2 meanings) |
| `m_min` | `m_min:worst-case-margin-to-minimum` | Length | mm | Worst-case margin to the minimum | mech.tolerance-stack | RESOLVE (2 meanings) |
| `m_slider` | — | Mass | kg | Slider mass (slider-crank) | mech.mechanism | ok |
| `mass` | — | Mass per length | kg/m | Mass per metre, each | elec.containment | ok |
| `max_tension` | — | Force | N | Maximum tension, recommended direction | elec.containment | ok |
| `maximum` | — | Length | mm | Maximum acceptable result (optional; blank = no maximum) | mech.tolerance-stack | ok |
| `mc_mean` | — | Length | mm | Monte Carlo mean | mech.tolerance-stack | ok |
| `mc_p05` | — | Length | mm | Monte Carlo 5 % quantile | mech.tolerance-stack | ok |
| `mc_p95` | — | Length | mm | Monte Carlo 95 % quantile | mech.tolerance-stack | ok |
| `mc_sigma` | — | Length | mm | Monte Carlo σ | mech.tolerance-stack | ok |
| `mc_yield` | — | Percent quantity | % | Monte Carlo yield | mech.tolerance-stack | ok |
| `mean` | — | Length | mm | Measured mean | fp.oring-gland | ok |
| `minimum` | — | Length | mm | Minimum acceptable result (optional; blank = no minimum) | mech.tolerance-stack | ok |
| `mu` | `mu:friction-coefficient` | Plain number | — | Coefficient of friction at the interface; Coefficient of friction between the two parts (record snapfit.frictio…; Coefficient of friction, cable sheath on containment (from your recor…; … (5 descriptions) | bs.pipe-expansion, elec.containment, elec.motor-sizing, mech.press-fit, mech.snap-fit | RESOLVE (2 meanings) |
| `mu` | `mu:vapour-resistance-factor` | Plain number | — | Vapour resistance factor μ | bs.glaser | RESOLVE (2 meanings) |
| `mu_0` | — | Angle | ° | Transmission angle at the first input angle | mech.mechanism | ok |
| `mu_G` | — | Plain number | — | Thread friction coefficient; F_M is the maximum preload, so enter the… | mech.bolted-joint | ok |
| `mu_K` | — | Plain number | — | Head / nut bearing friction coefficient; F_M is the maximum preload, … | mech.bolted-joint | ok |
| `mu_custom` | — | Dynamic viscosity | mPa·s | Dynamic viscosity (custom fluid) | bs.pipe-pump | ok |
| `mu_min` | — | Angle | ° | Smallest transmission angle over the sweep | mech.mechanism | ok |
| `mu_req` | — | Angle | ° | Minimum transmission angle required (linkages) | mech.mechanism | ok |
| `n` | `n:operating-speed` | Rotational speed, frequency | rpm | Operating speed; Speed | mech.bearing-life, mech.bearing-life-basic, mech.shaft | RESOLVE (4 meanings) |
| `n` | `n:air-change-rate` | Rotational speed, frequency | 1/h | Air change rate | bs.room-heat-loss | RESOLVE (4 meanings) |
| `n` | `n:cycles` | Plain number | — | Cycles | mech.fatigue-miner | RESOLVE (4 meanings) |
| `n` | `n:emitter-exponent` | Plain number | — | Emitter exponent | bs.sealed-system | RESOLVE (4 meanings) |
| `n_0` | — | Plain number | — | Load factor against separation | mech.bolted-joint | ok |
| `n_A` | — | Plain number | — | Exponent of the cross-section | elec.pcb-trace | ok |
| `n_L` | `n_L:load-factor-against-axial-yield` | Plain number | — | Load factor against axial yield | mech.bolted-joint | RESOLVE (2 meanings) |
| `n_L` | `n_L:load-speed-at-the-output` | Rotational speed, frequency | rpm | Load speed at the output (continuous duty) | elec.motor-sizing | RESOLVE (2 meanings) |
| `n_T` | — | Plain number | — | Factor of safety (Tresca) | mech.shaft-stress | ok |
| `n_VM` | — | Plain number | — | Factor of safety (von Mises) | mech.shaft-stress | ok |
| `n_active` | — | Plain number | — | Active coils (torsion: coils of the body) | mech.spring | ok |
| `n_b` | — | Plain number | — | Holding-down bolts in total (both sides) | struct.base-plate | ok |
| `n_bend` | — | Plain number | — | Minimum bend radius as a multiple of the largest cable diameter (from… | elec.containment | ok |
| `n_c` | — | Rotational speed, frequency | rpm | First critical speed (Rayleigh) | mech.shaft | ok |
| `n_c_D` | — | Rotational speed, frequency | rpm | Dunkerley lower bound | mech.shaft | ok |
| `n_cycles` | — | Plain number | — | Load cycles in the life of the lug (the 2×/3× route covers up to 16,0… | struct.lifting | ok |
| `n_dT` | — | Plain number | — | Exponent of the temperature rise | elec.pcb-trace | ok |
| `n_design` | — | Plain number | — | Design factor on the computed thickness (legacy; 1 for none) | mech.thin-vessel | ok |
| `n_diag` | — | Plain number | — | Month of the diagram | bs.glaser | ok |
| `n_div` | — | Plain number | — | Stations per member | struct.frame-2d | ok |
| `n_f` | — | Plain number | — | Goodman safety factor | mech.spring | ok |
| `n_f_min` | — | Plain number | — | Lowest fatigue safety factor | mech.shaft | ok |
| `n_f_req` | — | Plain number | — | Required Goodman safety factor | mech.spring | ok |
| `n_fcrit` | — | Plain number | — | Month governing the surface check | bs.glaser | ok |
| `n_gas` | — | Plain number | — | Gas exponent of the discharge, 1 isothermal (slow) to 1.4 adiabatic (… | fp.accumulator | ok |
| `n_gov` | — | Plain number | — | Governing factor | mech.material-margin | ok |
| `n_in` | — | Rotational speed, frequency | rpm | Input (crank) speed, passed through; Input speed (constant) | mech.mechanism | ok |
| `n_inf` | — | Rotational speed, frequency | 1/h | Infiltration air change rate at outside temperature, with MVHR only (… | bs.room-heat-loss | ok |
| `n_latch` | — | Plain number | — | Number of identical latches that share a holding load (a whole number… | mech.snap-fit | ok |
| `n_load` | — | Plain number | — | Load introduction factor (0 = at the clamped faces, 1 = under the hea… | mech.bolted-joint | ok |
| `n_max` | — | Rotational speed, frequency | rpm | Candidate motor maximum speed | elec.motor-sizing | ok |
| `n_mc` | `n_mc:monte-carlo-samples` | Plain number | — | Monte Carlo samples | fp.oring-gland, mech.tolerance-stack | ok |
| `n_mean` | — | Rotational speed, frequency | rpm | Mean motor speed over the cycle | elec.motor-sizing | ok |
| `n_min` | — | Plain number | — | Governing factor of safety | mech.shaft-stress | ok |
| `n_p` | — | Rotational speed, frequency | rpm | Pump speed | fp.cylinder | ok |
| `n_par` | `n_par:parallel-conductors` | Plain number | — | Parallel conductors per phase | elec.motor-start, elec.voltage-drop | ok |
| `n_pk` | — | Rotational speed, frequency | rpm | Peak speed | elec.motor-sizing | ok |
| `n_pump` | — | Rotational speed, frequency | rpm | Pump rated speed, the speed of the pump curve as entered, which gives… | bs.pipe-pump | ok |
| `n_rated` | — | Rotational speed, frequency | rpm | Candidate motor rated (nameplate) speed, which with the rated torque … | elec.motor-sizing | ok |
| `n_ratio` | — | Plain number | — | Speed ratio n₂/n₁ (affinity) | bs.pipe-pump | ok |
| `n_req` | `n_req:required-factor-of-safety` | Plain number | — | Required factor of safety; Required factor of safety on yield | mech.material-margin, mech.shaft-stress | ok |
| `n_s` | — | Plain number | — | Shear planes | struct.connection-ec3 | ok |
| `n_shaft` | — | Rotational speed, frequency | rpm | Pump shaft speed | fp.cylinder | ok |
| `n_start` | — | Plain number | — | First month of condensation | bs.glaser | ok |
| `n_t` | `n_t:holding-down-bolts-per-row` | Plain number | — | Holding-down bolts in each bolt row (one row each side) | struct.base-plate | RESOLVE (2 meanings) |
| `n_t` | `n_t:total-coils` | Plain number | — | Total coils | mech.spring | RESOLVE (2 meanings) |
| `n_target` | — | Plain number | — | Target safety factor | mech.shaft | ok |
| `n_u` | `n_u:factor-on-ultimate` | Plain number | — | Divisor on the ultimate tensile strength in the allowable stress (opt…; Factor on ultimate | mech.material-margin, struct.lifting | ok |
| `n_y` | `n_y:factor-on-yield` | Plain number | — | Divisor on the yield strength in the allowable stress (optional; blan…; Factor on yield | mech.material-margin, struct.lifting | ok |
| `n_y_min` | — | Plain number | — | Lowest yield safety factor | mech.shaft | ok |
| `nominal` | — | Length | mm | Nominal | mech.tolerance-stack | ok |
| `nu` | `nu:poissons-ratio` | Plain number | — | Poisson ratio of the ring, for the linear relation; Poisson's ratio; Poisson's ratio (the elastic collapse pressure under external pressur…; … (4 descriptions) | fp.oring-gland, mech.snap-fit, mech.thick-cylinder, struct.plate-bending | ok |
| `nu_M` | — | Plain number | — | Assembly utilisation of proof strength | mech.bolted-joint | ok |
| `nu_h` | — | Plain number | — | Hub Poisson's ratio | mech.press-fit | ok |
| `nu_perm` | — | Plain number | — | Permitted assembly utilisation ν: the equivalent stress at assembly o… | mech.bolted-joint | ok |
| `nu_s` | — | Plain number | — | Shaft Poisson's ratio | mech.press-fit | ok |
| `nu_seat` | — | Plain number | — | End-support coefficient ν (0.5 both ends fixed, 0.7 one fixed one pin… | mech.spring | ok |
| `od` | — | Length | mm | Overall diameter | elec.containment | ok |
| `od_hi` | — | Percent quantity | % | Ring outside-diameter compression: highest acceptable, in a rod-seal … | fp.oring-gland | ok |
| `omega3_0` | — | Rotational speed, frequency | rad/s | Coupler angular velocity at the first input angle | mech.mechanism | ok |
| `omega3_pk` | — | Rotational speed, frequency | rad/s | Peak coupler angular velocity | mech.mechanism | ok |
| `omega4_0` | — | Rotational speed, frequency | rad/s | Rocker angular velocity at the first input angle | mech.mechanism | ok |
| `omega_out_pk` | — | Rotational speed, frequency | rad/s | Peak output (rocker) angular velocity | mech.mechanism | ok |
| `output_motion` | — | Velocity | m/s | Peak speed of the output pin at the rocker length c (a kinematic prox… | mech.mechanism | ok |
| `p` | — | Stress, pressure | bar | Pressure | fp.oring-gland | ok |
| `p0` | — | Stress, pressure | bar a | Pre-charge, absolute, at the charging temperature | fp.accumulator | ok |
| `p0_min` | — | Stress, pressure | bar a | Pre-charge, absolute, at the minimum operating temperature | fp.accumulator | ok |
| `p0_set` | — | Stress, pressure | bar a | Pre-charge to set at the charging temperature | fp.accumulator | ok |
| `p_0` | — | Stress, pressure | bar | Gas pre-charge pressure p0, at the maximum operating temperature | fp.accumulator | ok |
| `p_1` | `p_1:lowest-working-pressure` | Stress, pressure | bar | Lowest working pressure p1 | fp.accumulator | RESOLVE (2 meanings) |
| `p_1` | `p_1:pitch-load-direction` | Length | mm | Pitch in the load direction (optional; blank = one row) | struct.connection-ec3 | RESOLVE (2 meanings) |
| `p_2` | `p_2:gauge` | Length | mm | Gauge across the load (optional; blank = one line) | struct.connection-ec3 | RESOLVE (2 meanings) |
| `p_2` | `p_2:highest-working-pressure` | Stress, pressure | bar | Highest working pressure p2 | fp.accumulator | RESOLVE (2 meanings) |
| `p_E` | — | Stress, pressure | MPa | Euler stress | struct.column-buckling | ok |
| `p_atm` | `p_atm:atmospheric-pressure` | Stress, pressure | bar, kPa | Atmospheric pressure (NPSH); Atmospheric pressure (gauge to absolute); Barometric pressure; … (4 descriptions) | bs.duct-sizing, bs.pipe-pump, bs.psychrometrics, fp.accumulator | ok |
| `p_back` | — | Stress, pressure | bar | Back pressure on the discharge side of the standard connection, gauge | fp.cylinder | ok |
| `p_c` | — | Stress, pressure | MPa | Compressive strength | struct.column-buckling | ok |
| `p_coil` | — | Length | mm | Pitch | mech.spring | ok |
| `p_design` | — | Stress, pressure | MPa | Internal design pressure | mech.thin-vessel | ok |
| `p_eff` | — | Stress, pressure | MPa | Gripping interface pressure | mech.press-fit | ok |
| `p_f` | — | Stress, pressure | kPa | Final (maximum working) pressure, gauge | bs.sealed-system | ok |
| `p_fill_max` | — | Percent quantity | % | Fraction outside the limit: gland fill above the upper limit (hot end) | fp.oring-gland | ok |
| `p_fill_min` | — | Percent quantity | % | Fraction outside the limit: gland fill below the lower limit (cold en… | fp.oring-gland | ok |
| `p_gap` | — | Percent quantity | % | Fraction outside the limit: extrusion gap above the permitted gap | fp.oring-gland | ok |
| `p_i` | `p_i:internal-pressure` | Stress, pressure | MPa | Internal pressure | mech.thick-cylinder | RESOLVE (2 meanings) |
| `p_i` | `p_i:pre-charge-pressure` | Stress, pressure | kPa | Pre-charge pressure | bs.sealed-system | RESOLVE (2 meanings) |
| `p_int` | — | Stress, pressure | MPa | Interface pressure | mech.press-fit | ok |
| `p_load` | — | Stress, pressure | bar | Lowest pressure the load still needs, bar gauge, such as the push pre… | fp.accumulator | ok |
| `p_margin` | — | Stress, pressure | kPa | Pre-charge allowance over static head | bs.sealed-system | ok |
| `p_o` | — | Stress, pressure | MPa | External pressure | mech.thick-cylinder | ok |
| `p_op` | — | Stress, pressure | bar | Operating pressure (for the extrusion record, gauge) | fp.oring-gland | ok |
| `p_prv` | — | Stress, pressure | kPa | Safety valve setting, gauge | bs.sealed-system | ok |
| `p_pts` | — | Stress, pressure | Pa | Fan curve: static pressure points | elec.enclosure-heat | ok |
| `p_push` | — | Stress, pressure | bar | Pressure the push load needs at the cylinder | fp.cylinder | ok |
| `p_screw` | — | Length | mm | Screw lead (per revolution of the screw) | elec.motor-sizing | ok |
| `p_sq_max` | — | Percent quantity | % | Fraction outside the limit: squeeze above the upper limit (hot end) | fp.oring-gland | ok |
| `p_sq_min` | — | Percent quantity | % | Fraction outside the limit: squeeze below the lower limit (cold end) | fp.oring-gland | ok |
| `p_stretch` | — | Percent quantity | % | Fraction outside the limit: installed stretch outside its limits | fp.oring-gland | ok |
| `p_sys` | `p_sys:supply-pressure` | Stress, pressure | bar, bar g | Supply pressure; Supply pressure at the cylinder and the pump, gauge | fp.cylinder | RESOLVE (2 meanings) |
| `p_sys` | `p_sys:system-resistance` | Stress, pressure | Pa | System resistance (filters, grilles) at the reference airflow | elec.enclosure-heat | RESOLVE (2 meanings) |
| `p_thread` | — | Length | mm | Thread pitch | mech.bolted-joint | ok |
| `p_v_custom` | — | Stress, pressure | kPa | Vapour pressure of the custom fluid at the fluid temperature, absolut… | bs.pipe-pump | ok |
| `p_w_1` | — | Stress, pressure | kPa | Vapour pressure, state 1 | bs.psychrometrics | ok |
| `p_ws_1` | — | Stress, pressure | kPa | Saturation pressure, state 1 | bs.psychrometrics | ok |
| `p_y` | — | Stress, pressure | MPa | Design strength (yield) | struct.column-buckling | ok |
| `pass` | `pass:all-checks-pass` | Plain number | — | All checks pass | bs.glaser, bs.pipe-expansion | ok |
| `peak_accel` | — | Acceleration | m/s² | Peak acceleration of the output pin | mech.mechanism | ok |
| `pf_link` | — | Plain number | — | Power factor of the linked machine | elec.load-budget | ok |
| `phi` | `phi:area-ratio` | Plain number | — | Area ratio A_b/A_a | fp.cylinder | RESOLVE (2 meanings) |
| `phi` | `phi:shear-plane-angle` | Angle | ° | Shear-plane angle φ | struct.lifting | RESOLVE (2 meanings) |
| `phi_1` | `phi_1:angle-at-point-1` | Angle | ° | Angle at working point 1 (torsion, deflections) | mech.spring | RESOLVE (2 meanings) |
| `phi_1` | `phi_1:relative-humidity-1` | Percent quantity | % | Relative humidity, state 1, as a quantity in % (for linking) | bs.psychrometrics | RESOLVE (2 meanings) |
| `phi_2` | — | Angle | ° | Angle at working point 2 (torsion, deflections) | mech.spring | ok |
| `phi_max` | `phi_max:maximum-pressure-angle` | Angle | ° | Maximum pressure angle allowed (roller follower) | mech.mechanism | RESOLVE (2 meanings) |
| `phi_max` | `phi_max:strain-shape-maximum` | Plain number | — | Strain shape maximum | mech.snap-fit | RESOLVE (2 meanings) |
| `proof_load` | — | Force | kN | Proof-load value | struct.lifting | ok |
| `psat_i` | — | Stress, pressure | Pa | Saturation vapour pressure at the internal temperature | bs.glaser | ok |
| `psi_0` | — | Plain number | — | ψ₀ | struct.load-combinations | ok |
| `psi_1` | — | Plain number | — | ψ₁ | struct.load-combinations | ok |
| `psi_2` | — | Plain number | — | ψ₂ | struct.load-combinations | ok |
| `psi_y` | — | Plain number | — | Ratio of the end moments about y-y, +1 for a uniform moment (optional…; ψ of the M_y diagram | struct.ltb-interaction | ok |
| `psi_z` | — | Plain number | — | Ratio of the end moments about z-z | struct.ltb-interaction | ok |
| `q` | `q:heat-flux` | Heat flux | W/m² | Heat flux | bs.plane-wall | RESOLVE (3 meanings) |
| `q` | `q:notch-sensitivity` | Plain number | — | Notch sensitivity, bending | mech.shaft | RESOLVE (3 meanings) |
| `q` | `q:pressure-on-loaded-area` | Stress, pressure | kPa | Pressure on the loaded area (uniform and patch loads, pressure basis) | struct.plate-bending | RESOLVE (3 meanings) |
| `q_hi_d` | — | Percent quantity | % | Dynamic duty: highest acceptable squeeze | fp.oring-gland | ok |
| `q_hi_s` | — | Percent quantity | % | Static duty: highest acceptable squeeze | fp.oring-gland | ok |
| `q_lo_d` | — | Percent quantity | % | Dynamic duty: lowest acceptable squeeze | fp.oring-gland | ok |
| `q_lo_s` | — | Percent quantity | % | Static duty: lowest acceptable squeeze (from your handbook) | fp.oring-gland | ok |
| `q_max_A` | — | Heat flux | W/m² | Heat-loss limit per square metre of flat surface (optional; blank = n… | bs.insulation | ok |
| `q_max_m` | — | Heat flow per length | W/m | Heat-loss limit per metre of pipe (optional; blank = not a criterion;… | bs.insulation | ok |
| `q_per_m` | — | Heat flow per length | W/m | Heat flow per metre (heat loss) | bs.insulation | ok |
| `q_s` | — | Plain number | — | Notch sensitivity, torsion | mech.shaft | ok |
| `q_v` | — | Volumetric flow | m³/h | Ventilation air flow | bs.room-heat-loss | ok |
| `qty` | — | Plain number | — | Quantity | elec.enclosure-heat | ok |
| `r` | `r:root-radius` | Length | mm | Root or corner radius; Root radius for the shear area (optional; blank = 0); Root radius of an I-section, channel, tee or angle, or outer corner r… | struct.en1993-member, struct.section-properties | RESOLVE (2 meanings) |
| `r` | `r:radius-of-gyration` | Length | mm | Radius of gyration | struct.column-buckling | RESOLVE (2 meanings) |
| `r0` | — | Length | mm | Patch radius (circular plate, central patch) | struct.plate-bending | ok |
| `r_0` | — | Plain number | — | Pre-charge rule: p0 (at T_max) at most this fraction of p1, on absolu… | fp.accumulator | ok |
| `r_1ph` | — | m·kg/(s³·A²) | mV/A/m | Two cables, single-phase AC: resistive component | elec.voltage-drop | ok |
| `r_3ph` | — | m·kg/(s³·A²) | mV/A/m | Three or four cables, three-phase AC: resistive component | elec.voltage-drop | ok |
| `r_bar` | — | Length | mm | Radius of the torsion bar | mech.snap-fit | ok |
| `r_c` | — | Plain number | — | Critical-speed ratio | mech.shaft | ok |
| `r_c_min` | — | Plain number | — | Smallest ratio of the first critical speed to the operating speed | mech.shaft | ok |
| `r_dc` | — | m·kg/(s³·A²) | mV/A/m | Two cables, DC | elec.voltage-drop | ok |
| `r_f` | — | Plain number | — | Surge ratio f_n/f_op | mech.spring | ok |
| `r_g` | — | Length | mm | Groove bottom corner radius | fp.oring-gland | ok |
| `r_g_y` | — | Length | mm | Radius of gyration about the y-y axis | struct.section-properties | ok |
| `r_g_z` | — | Length | mm | Radius of gyration about the z-z axis | struct.section-properties | ok |
| `r_h1` | — | Length | mm | Hook mean radius, bending (extension) | mech.spring | ok |
| `r_h2` | — | Length | mm | Hook side-bend mean radius, torsion (extension) | mech.spring | ok |
| `r_i` | — | Length | mm | Inner corner radius of a hollow rectangle (optional; blank = 1.0 t_w,… | struct.section-properties | ok |
| `r_lug` | — | Length | mm | Lug end radius about the hole centre (the plate is twice that wide at… | struct.lifting | ok |
| `r_p` | — | Plain number | — | Pre-charge rule: p2/p0 (p0 at T_min) at most this ratio, on absolute … | fp.accumulator | ok |
| `r_ring` | — | Length | mm | Ring radius (circular plate, ring load) | struct.plate-bending | ok |
| `red_nom` | — | Percent quantity | % | Cross-section reduction at the mean stretch | fp.oring-gland | ok |
| `res_max` | — | Length | mm | Largest loop-closure residual | mech.mechanism | ok |
| `res_vel_max` | — | Plain number | — | Largest velocity-loop residual, relative to the size of its terms | mech.mechanism | ok |
| `retention_force` | — | Force | N | Separation force | mech.snap-fit | ok |
| `rho` | `rho:material-density` | Density | kg/m³ | Density; Density (for the frequency); Density (shaft self-mass in the critical speed) | mech.shaft, mech.spring, struct.plate-bending | RESOLVE (2 meanings) |
| `rho` | `rho:fluid-density` | Density | kg/m³ | Fluid density | fp.cylinder | RESOLVE (2 meanings) |
| `rho_1` | — | Density | kg/m³ | Moist-air density, state 1 | bs.psychrometrics | ok |
| `rho_20` | `rho_20:resistivity-at-20c` | Resistivity | Ω·mm²/m | Copper resistivity at 20 °C (annealed copper, 1.724e-8 Ω·m); Resistivity at 20 °C (copper 0.01724, aluminium 0.0282 Ω·mm²/m); Resistivity at 20 °C (used for k, and for R₁ + R₂ through k_R); … (4 descriptions) | elec.fault-protection, elec.motor-start, elec.pcb-trace, elec.voltage-drop | ok |
| `rho_air` | — | Density | kg/m³ | Air density | elec.enclosure-heat | ok |
| `rho_custom` | — | Density | kg/m³ | Density (custom fluid) | bs.pipe-pump | ok |
| `rho_shear` | — | Plain number | — | Shear reduction factor ρ | struct.en1993-member | ok |
| `rot` | — | Plain number | — | Rotation (° anticlockwise) | struct.section-properties | ok |
| `round_to` | — | Length | mm | Round sizes up to a multiple of | bs.duct-sizing | ok |
| `s` | `s:fillet-leg` | Length | mm | Fillet leg | mech.weld-group | RESOLVE (2 meanings) |
| `s` | `s:move-distance` | Plain number | — | Move distance (linear: m; rotary: output revolutions) | elec.motor-sizing | RESOLVE (2 meanings) |
| `s_0` | — | Plain number | — | Static safety factor | mech.bearing-life | ok |
| `s_0_req` | — | Plain number | — | Required static safety factor | mech.bearing-life | ok |
| `s_1` | — | Length | mm | Deflection at point 1; Deflection at working point 1 (deflections) | mech.spring | ok |
| `s_2` | — | Length | mm | Deflection at point 2; Deflection at working point 2 (deflections) | mech.spring | ok |
| `s_c` | — | Length | mm | Travel to solid | mech.spring | ok |
| `s_leg` | — | Length | mm | Leg length provided | struct.fillet-weld | ok |
| `s_max` | — | Percent quantity | % | Largest volume swell of the ring in the fluid | fp.oring-gland | ok |
| `s_min` | — | Percent quantity | % | Smallest volume change of the ring in the fluid (negative for shrinka… | fp.oring-gland | ok |
| `s_n` | — | Length | mm | Travel to L_n | mech.spring | ok |
| `s_req` | — | Length | mm | Leg required | mech.weld-group | ok |
| `s_work` | — | Length | mm | Expected working travel s₂ − s₁ (optional; blank = no check). The she… | mech.spring | ok |
| `scr` | — | Angle | mm²/m² | Screening ratio OD·Y/(L − U)² | bs.pipe-expansion | ok |
| `seed` | `seed:monte-carlo-seed` | Plain number | — | Monte Carlo seed | fp.oring-gland, mech.tolerance-stack | ok |
| `shaft_grade` | — | Plain number | — | Shaft IT grade | mech.iso286-fit | ok |
| `shift` | — | Length | mm | Mean shift | mech.tolerance-stack | ok |
| `side` | — | Length | mm | Edge distance on each side of the hole | struct.lifting | ok |
| `sigma` | `sigma:stress` | Stress, pressure | MPa | Applied (equivalent) stress; Axial stress; Bending stress | mech.material-margin, mech.shaft-stress, mech.thermal-expansion | RESOLVE (2 meanings) |
| `sigma` | `sigma:standard-deviation` | Length | mm | Measured standard deviation; Predicted σ; Standard deviation (normal contributors; replaces band/k for the stat… | fp.oring-gland, mech.tolerance-stack | RESOLVE (2 meanings) |
| `sigma_M` | — | Stress, pressure | MPa | Preload stress | mech.bolted-joint | ok |
| `sigma_a` | — | Stress, pressure | MPa | Fatigue amplitude | mech.bolted-joint | ok |
| `sigma_a_limit` | — | Stress, pressure | MPa | Bolt endurance limit (stress amplitude) | mech.bolted-joint | ok |
| `sigma_abs` | — | Stress, pressure | MPa | Stress magnitude | mech.thermal-expansion | ok |
| `sigma_allow` | `sigma_allow:allowable-stress` | Stress, pressure | MPa | Allowable bending stress (optional; blank = no stress check); Allowable stress | mech.thermal-expansion, mech.thick-cylinder, struct.beam-formulas | ok |
| `sigma_b` | — | Stress, pressure | MPa | Pin bending stress | mech.lifting-lug | ok |
| `sigma_br` | — | Stress, pressure | MPa | Bearing stress | mech.lifting-lug | ok |
| `sigma_h` | — | Stress, pressure | MPa | Hoop stress at the corroded thickness | mech.thin-vessel | ok |
| `sigma_leg` | — | Stress, pressure | MPa | Bending stress at the leg root | bs.pipe-expansion | ok |
| `sigma_max` | — | Stress, pressure | MPa | Greatest von Mises stress | struct.plate-bending | ok |
| `sigma_perp` | — | Stress, pressure | MPa | Directional method: normal stress perpendicular to the throat (option… | struct.connection-ec3 | ok |
| `sigma_principal` | — | Stress, pressure | MPa | Greatest principal stress | struct.plate-bending | ok |
| `sigma_r_a` | — | Stress, pressure | MPa | Radial stress, bore | mech.thick-cylinder | ok |
| `sigma_r_b` | — | Stress, pressure | MPa | Radial stress, outer | mech.thick-cylinder | ok |
| `sigma_red_B` | — | Stress, pressure | MPa | Working equivalent stress | mech.bolted-joint | ok |
| `sigma_res` | — | Stress, pressure | MPa | Fully restrained stress | bs.pipe-expansion | ok |
| `sigma_run` | — | Stress, pressure | MPa | Bending stress at the first run root | bs.pipe-expansion | ok |
| `sigma_t` | — | Stress, pressure | MPa | Net-section tension | mech.lifting-lug | ok |
| `sigma_t_a` | — | Stress, pressure | MPa | Hoop stress, bore | mech.thick-cylinder | ok |
| `sigma_t_b` | — | Stress, pressure | MPa | Hoop stress, outer | mech.thick-cylinder | ok |
| `sigma_t_hub` | — | Stress, pressure | MPa | Hub hoop stress at bore | mech.press-fit | ok |
| `sigma_t_shaft` | — | Stress, pressure | MPa | Shaft hoop stress at surface | mech.press-fit | ok |
| `sigma_thin` | — | Stress, pressure | MPa | Thin-wall hoop estimate | mech.thick-cylinder | ok |
| `sigma_tr_a` | — | Stress, pressure | MPa | Tresca, bore | mech.thick-cylinder | ok |
| `sigma_tr_b` | — | Stress, pressure | MPa | Tresca, outer | mech.thick-cylinder | ok |
| `sigma_vm` | — | Stress, pressure | MPa | von Mises stress | mech.shaft-stress | ok |
| `sigma_vm_a` | — | Stress, pressure | MPa | von Mises, bore | mech.thick-cylinder | ok |
| `sigma_vm_b` | — | Stress, pressure | MPa | von Mises, outer | mech.thick-cylinder | ok |
| `sigma_vm_hub` | — | Stress, pressure | MPa | Hub von Mises stress | mech.press-fit | ok |
| `sigma_vm_max` | — | Stress, pressure | MPa | Maximum von Mises | mech.thick-cylinder | ok |
| `sigma_vm_shaft` | — | Stress, pressure | MPa | Shaft von Mises stress at the surface | mech.press-fit | ok |
| `sigma_vm_shaft_bore` | — | Stress, pressure | MPa | Shaft von Mises stress at the shaft bore | mech.press-fit | ok |
| `sigma_vm_shaft_max` | — | Stress, pressure | MPa | Largest shaft von Mises stress (surface or bore) | mech.press-fit | ok |
| `sigma_z` | — | Stress, pressure | MPa | Axial stress | mech.thick-cylinder | ok |
| `sigma_z_max` | — | Stress, pressure | MPa | Maximum bolt tensile stress | mech.bolted-joint | ok |
| `squeeze_max` | — | Percent quantity | % | Largest squeeze | fp.oring-gland | ok |
| `squeeze_min` | — | Percent quantity | % | Smallest squeeze | fp.oring-gland | ok |
| `squeeze_nom` | — | Percent quantity | % | Squeeze at the mean dimensions | fp.oring-gland | ok |
| `start_dip` | — | Percent quantity | % | Voltage dip at the motor terminals | elec.motor-start | ok |
| `steps` | — | Plain number | — | Intervals N of the table (N + 1 rows, a whole number, at most 3600; t… | mech.mechanism | ok |
| `strain_util` | — | Plain number | — | Strain / permissible strain | mech.snap-fit | ok |
| `stretch` | — | Percent quantity | % | Largest installed stretch | fp.oring-gland | ok |
| `stretch_min` | — | Percent quantity | % | Smallest installed stretch | fp.oring-gland | ok |
| `stretch_nom` | — | Percent quantity | % | Installed stretch, mean dimensions | fp.oring-gland | ok |
| `stroke` | — | Length | mm | Stroke | fp.cylinder | ok |
| `sum_R_x` | — | Force | kN | Σ R_x | struct.frame-2d | ok |
| `sum_R_y` | — | Force | kN | Σ R_y | struct.frame-2d | ok |
| `swing` | — | Angle | ° | Rocker swing over the sweep | mech.mechanism | ok |
| `t` | `t:thickness` | Length | mm | Thickness | bs.plane-wall | RESOLVE (2 meanings) |
| `t` | `t:time` | Time | s | Time | elec.fault-protection | RESOLVE (2 meanings) |
| `t_2` | — | Length | mm | Thickness of the other ply, such as the column flange behind an end p… | struct.connection-ec3 | ok |
| `t_auto` | — | Time, rating life | h | Battery autonomy | elec.load-budget | ok |
| `t_c` | — | Length | mm | Thickness, circumferential stress | mech.thin-vessel | ok |
| `t_chosen` | — | Length | mm | Chosen insulation thickness (optional; blank = report at the required… | bs.insulation | ok |
| `t_cycle` | — | Time | s | Cycle time | fp.cylinder | ok |
| `t_d` | — | Time | s | Disconnection time | elec.fault-protection | ok |
| `t_disc` | — | Time | s | Disconnection time of the table column | elec.fault-protection | ok |
| `t_dwell` | `t_dwell:dwell-time` | Time | s | Dwell between moves; Dwell time added to each cycle | elec.motor-sizing, fp.cylinder | ok |
| `t_ext` | — | Time | s | Extend time | fp.cylinder | ok |
| `t_f` | `t_f:flange-thickness` | Length | mm | Flange thickness; Flange thickness (I/H); Flange thickness for the shear area (optional; blank = use A_v); … (4 descriptions) | struct.base-plate, struct.en1993-member, struct.ltb-interaction, struct.section-properties | ok |
| `t_g` | — | Length | mm | Grout thickness (optional; blank = the β_j = 2/3 condition is not che… | struct.base-plate | ok |
| `t_i` | — | Temperature | °C | Room design temperature (blank = the sheet's room temperature T_room) | bs.sealed-system | ok |
| `t_l` | — | Length | mm | Thickness, longitudinal stress | mech.thin-vessel | ok |
| `t_lug` | — | Length | mm | Lug thickness | mech.lifting-lug | ok |
| `t_move` | — | Time | s | Move time | elec.motor-sizing | ok |
| `t_mrss` | — | Length | mm | Modified RSS tolerance (±) | mech.tolerance-stack | ok |
| `t_oz` | — | Length | mil | Copper thickness per oz/ft² (35 µm = 1.378 mil, derived) | elec.pcb-trace | ok |
| `t_p` | `t_p:plate-thickness` | Length | mm | Lug plate thickness; Plate thickness; Shell thickness provided (nominal, before corrosion) | mech.thin-vessel, struct.base-plate, struct.connection-ec3, struct.lifting, struct.plate-bending | ok |
| `t_req` | `t_req:required-thickness` | Length | mm | Plate thickness required (bearing, compression and tension T-stubs); Required nominal thickness | mech.thin-vessel, struct.base-plate | RESOLVE (2 meanings) |
| `t_req` | `t_req:required-disconnection-time` | Time | s | Required disconnection time | elec.fault-protection | RESOLVE (2 meanings) |
| `t_req_N` | — | Length | mm | Plate thickness required for axial bearing alone | struct.base-plate | ok |
| `t_req_q` | — | Length | mm | Required thickness, heat-loss limit | bs.insulation | ok |
| `t_req_se` | — | Length | mm | Required thickness, surface-temperature limit | bs.insulation | ok |
| `t_ret` | — | Time | s | Retract time | fp.cylinder | ok |
| `t_rss` | — | Length | mm | RSS tolerance (±) | mech.tolerance-stack | ok |
| `t_s` | — | Length | mm | Cheek plate thickness (each) | mech.lifting-lug | ok |
| `t_start` | — | Time | s | Run-up time to full speed (continuous duty) | elec.motor-sizing | ok |
| `t_w` | `t_w:web-thickness` | Length | mm | Wall, web or leg thickness; Web thickness (I/H); Web thickness for the shear area (optional; blank = use A_v) | struct.base-plate, struct.en1993-member, struct.section-properties | RESOLVE (2 meanings) |
| `t_w` | `t_w:pipe-wall-thickness` | Length | mm | Pipe wall thickness, with its conductivity (optional; blank = not inc… | bs.insulation | RESOLVE (2 meanings) |
| `t_wall` | `t_wall:wall-thickness` | Length | mm | Pipe wall thickness; Wall thickness (RHS and CHS) | bs.pipe-expansion, struct.base-plate | ok |
| `t_wc` | — | Length | mm | Worst-case tolerance (±) | mech.tolerance-stack | ok |
| `target` | — | Plain number | — | Node (nodal) or element | struct.frame-2d | ok |
| `tau` | `tau:throat-stress` | Stress, pressure | MPa | Throat stress | mech.weld-group | RESOLVE (2 meanings) |
| `tau` | `tau:torsional-shear-stress` | Stress, pressure | MPa | Torsional shear | mech.shaft-stress | RESOLVE (2 meanings) |
| `tau_1` | — | Stress, pressure | MPa | Stress at point 1 | mech.spring | ok |
| `tau_2` | — | Stress, pressure | MPa | Stress at point 2 | mech.spring | ok |
| `tau_B` | — | Stress, pressure | MPa | Nut / internal-thread material shear strength | mech.bolted-joint | ok |
| `tau_c` | — | Stress, pressure | MPa | Stress at solid | mech.spring | ok |
| `tau_max` | — | Stress, pressure | MPa | Maximum shear stress | mech.shaft-stress | ok |
| `tau_p` | — | Stress, pressure | MPa | Pin shear | mech.lifting-lug | ok |
| `tau_par` | — | Stress, pressure | MPa | Directional method: shear parallel to the weld axis (optional; blank … | struct.connection-ec3 | ok |
| `tau_perp` | — | Stress, pressure | MPa | Directional method: shear perpendicular to the weld axis (optional; b… | struct.connection-ec3 | ok |
| `tau_to` | — | Stress, pressure | MPa | Tear-out shear | mech.lifting-lug | ok |
| `tau_w` | — | Stress, pressure | MPa | Throat stress | struct.fillet-weld | ok |
| `theta` | `theta:bend-angle` | Angle | ° | Angle (bend) | elec.containment | RESOLVE (4 meanings) |
| `theta` | `theta:incline-angle` | Angle | ° | Incline from horizontal (+ moving uphill) | elec.motor-sizing | RESOLVE (4 meanings) |
| `theta` | `theta:principal-axis-angle` | Angle | ° | Principal axis angle | struct.section-properties | RESOLVE (4 meanings) |
| `theta` | `theta:sling-angle` | Angle | ° | Sling angle θ from the vertical, measured in the plane of the lug (th… | struct.lifting | RESOLVE (4 meanings) |
| `theta3_0` | — | Angle | ° | Coupler angle at the first input angle | mech.mechanism | ok |
| `theta4_0` | — | Angle | ° | Rocker angle at the first input angle | mech.mechanism | ok |
| `theta4_alt` | — | Angle | ° | Rocker angle on the other circuit at the first input angle | mech.mechanism | ok |
| `theta4_max` | — | Angle | ° | Largest rocker angle | mech.mechanism | ok |
| `theta4_min` | — | Angle | ° | Smallest rocker angle | mech.mechanism | ok |
| `theta_A` | — | Angle (slope) | rad | Slope at A | mech.shaft | ok |
| `theta_B` | — | Angle (slope) | rad | Slope at B | mech.shaft | ok |
| `theta_a` | — | Temperature | °C | Outside temperature of the cold point (Δv constant below it) | bs.glaser | ok |
| `theta_adj` | — | Temperature | °C | Adjacent temperature (blank or θ_e = outside) | bs.room-heat-loss | ok |
| `theta_b` | — | Temperature | °C | Outside temperature of the warm point (Δv constant above it) | bs.glaser | ok |
| `theta_e` | `theta_e:external-design-temperature` | Temperature | °C | External design temperature (location record); Mean outside temperature θe | bs.glaser, bs.room-heat-loss | ok |
| `theta_f` | — | Temperature | °C | Final temperature for the adiabatic check | elec.fault-protection | ok |
| `theta_i` | — | Temperature | °C | Initial temperature for the adiabatic check | elec.fault-protection | ok |
| `theta_max` | — | Angle (slope) | rad | Slope limit at the bearings | mech.shaft | ok |
| `theta_range` | — | Angle | ° | Swept range of the input angle (360° for a full turn) | mech.mechanism | ok |
| `theta_se_max` | — | Temperature | °C | Highest surface temperature, for a hot surface (optional; blank = not… | bs.insulation | ok |
| `theta_start` | — | Angle | ° | Input angle at the first row of the table | mech.mechanism | ok |
| `thickness` | — | Length | mm | Governing required thickness | bs.insulation | ok |
| `trace_Ploss` | — | Power, heat flow | mW | Power loss | elec.pcb-trace | ok |
| `trace_R` | — | Resistance, impedance | mΩ | Trace resistance | elec.pcb-trace | ok |
| `u` | `u:utilisation` | Plain number | — | Stress utilisation; Thickness utilisation; Utilisation | elec.thermal-chain, mech.material-margin, mech.thermal-expansion, mech.thick-cylinder, mech.thin-vessel, mech.weld-group, struct.column-buckling, struct.fillet-weld | RESOLVE (2 meanings) |
| `u` | `u:patch-size-x` | Length | mm | Patch size in x (rectangular plate, central patch) | struct.plate-bending | RESOLVE (2 meanings) |
| `u_2` | — | Plain number | — | Utilisation at point 2 | mech.spring | ok |
| `u_61` | — | Plain number | — | Equation 6.61 | struct.ltb-interaction | ok |
| `u_62` | — | Plain number | — | Equation 6.62 | struct.ltb-interaction | ok |
| `u_C` | — | Plain number | — | Compression-side utilisation | struct.base-plate | ok |
| `u_I2` | — | Plain number | — | I₂ / (1.45 I_z) | elec.conductor-sizing | ok |
| `u_Ib` | — | Plain number | — | I_b / I_n | elec.conductor-sizing | ok |
| `u_In` | — | Plain number | — | I_n / I_z | elec.conductor-sizing | ok |
| `u_LTB` | — | Plain number | — | LTB utilisation | struct.ltb-interaction | ok |
| `u_M_y` | `u_M_y:bending-y-utilisation` | Plain number | — | Bending term, y; Bending y-y utilisation | struct.en1993-member, struct.ltb-interaction | ok |
| `u_M_z` | `u_M_z:bending-z-utilisation` | Plain number | — | Bending term, z; Bending z-z utilisation | struct.en1993-member, struct.ltb-interaction | ok |
| `u_Mc` | — | Plain number | — | Peak condensate against its limit | bs.glaser | ok |
| `u_N` | `u_N:axial-bearing-utilisation` | Plain number | — | Axial bearing utilisation | struct.base-plate | RESOLVE (2 meanings) |
| `u_N` | `u_N:axial-utilisation` | Plain number | — | Axial utilisation | struct.en1993-member | RESOLVE (2 meanings) |
| `u_N_y` | — | Plain number | — | Axial term, y | struct.ltb-interaction | ok |
| `u_N_z` | — | Plain number | — | Axial term, z | struct.ltb-interaction | ok |
| `u_P` | — | Plain number | — | Sidewall pressure utilisation | elec.containment | ok |
| `u_T` | `u_T:cable-tension-utilisation` | Plain number | — | Cable tension utilisation | elec.containment | RESOLVE (2 meanings) |
| `u_T` | `u_T:t-stub-tension-utilisation` | Plain number | — | Tension T-stub utilisation | struct.base-plate | RESOLVE (2 meanings) |
| `u_V` | `u_V:shear-utilisation` | Plain number | — | Shear utilisation | struct.base-plate, struct.en1993-member | ok |
| `u_Zs` | — | Plain number | — | Loop impedance utilisation | elec.fault-protection | ok |
| `u_app` | — | Plain number | — | Approach utilisation (minimum / actual) | bs.heat-exchanger | ok |
| `u_assembly` | — | Plain number | — | Assembly utilisation over the permitted ν | mech.bolted-joint | ok |
| `u_b` | `u_b:bearing-utilisation` | Plain number | — | Bearing utilisation; Utilisation, bearing | struct.connection-ec3, struct.lifting | RESOLVE (3 meanings) |
| `u_b` | `u_b:buckling-deflection-ratio` | Plain number | — | Deflection at point 2 / buckling deflection | mech.spring | RESOLVE (3 meanings) |
| `u_b` | `u_b:pin-bending-utilisation` | Plain number | — | Pin bending utilisation | mech.lifting-lug | RESOLVE (3 meanings) |
| `u_bend` | — | Plain number | — | Bend radius utilisation | elec.containment | ok |
| `u_br` | — | Plain number | — | Bearing utilisation | mech.lifting-lug | ok |
| `u_bt` | — | Plain number | — | Bolt tension utilisation | struct.base-plate | ok |
| `u_bth` | — | Plain number | — | Utilisation, BTH-1 mode | struct.lifting | ok |
| `u_buck` | — | Plain number | — | Rod buckling utilisation (screening) | fp.cylinder | ok |
| `u_buckling` | `u_buckling:buckling-utilisation` | Plain number | — | Buckling utilisation | struct.en1993-member, struct.ltb-interaction | ok |
| `u_c` | — | Plain number | — | Utilisation at solid | mech.spring | ok |
| `u_clamp` | — | Plain number | — | Required over available residual clamp | mech.bolted-joint | ok |
| `u_cpc` | — | Plain number | — | Protective conductor utilisation | elec.fault-protection | ok |
| `u_cpk` | — | Plain number | — | Capability utilisation | mech.tolerance-stack | ok |
| `u_crit` | — | Plain number | — | Utilisation of critical speed against the operating speed | mech.shaft | ok |
| `u_defl` | `u_defl:deflection-utilisation` | Plain number | — | Deflection utilisation | mech.shaft, struct.plate-bending | ok |
| `u_dip` | — | Plain number | — | Dip utilisation at the motor terminals | elec.motor-start | ok |
| `u_dish` | — | Plain number | — | Dishing utilisation | struct.lifting | ok |
| `u_duty` | — | Plain number | — | Duty utilisation | bs.heat-exchanger | ok |
| `u_dv` | — | Plain number | — | Drop utilisation against the limit | elec.voltage-drop | ok |
| `u_emit` | — | Plain number | — | Emitter utilisation | bs.sealed-system | ok |
| `u_f` | `u_f:goodman-utilisation` | Plain number | — | Fatigue utilisation; Goodman utilisation | mech.shaft, mech.spring | RESOLVE (2 meanings) |
| `u_f` | `u_f:surface-factor-utilisation` | Plain number | — | Surface factor against its minimum | bs.glaser | RESOLVE (2 meanings) |
| `u_fat` | — | Plain number | — | Fatigue utilisation against the required factor | mech.spring | ok |
| `u_fatigue` | — | Plain number | — | Fatigue utilisation | mech.bolted-joint | ok |
| `u_fill` | — | Plain number | — | Fill utilisation | elec.containment | ok |
| `u_gap` | — | Plain number | — | Largest gap against the permitted gap | fp.oring-gland | ok |
| `u_interaction` | — | Plain number | — | Interaction utilisation | struct.ltb-interaction | ok |
| `u_k` | `u_k:transformer-impedance-voltage` | Percent quantity | % | Transformer impedance voltage | elec.fault-protection, elec.motor-start | ok |
| `u_leg` | — | Plain number | — | Required leg over available leg | bs.pipe-expansion | ok |
| `u_life` | `u_life:life-utilisation` | Plain number | — | Life utilisation; Required over modified life | mech.bearing-life, mech.bearing-life-basic | ok |
| `u_lin` | — | Plain number | — | Linear interaction utilisation (highest over all bolts) | mech.bolt-group | ok |
| `u_max` | `u_max:governing-utilisation` | Plain number | — | Governing interaction utilisation; Governing utilisation | mech.bolt-group, struct.base-plate | ok |
| `u_member` | `u_member:member-utilisation` | Plain number | — | Member interaction; Member utilisation | struct.en1993-member, struct.ltb-interaction | ok |
| `u_mu` | — | Plain number | — | Required against smallest transmission angle | mech.mechanism | ok |
| `u_n` | `u_n:minimum-length-travel-utilisation` | Plain number | — | Working deflection / travel to L_n | mech.spring | RESOLVE (2 meanings) |
| `u_n` | `u_n:safety-factor-utilisation` | Plain number | — | Utilisation of factor of safety against the required factor | mech.shaft-stress | RESOLVE (2 meanings) |
| `u_p` | — | Plain number | — | Pin shear utilisation | mech.lifting-lug | ok |
| `u_peak` | — | Plain number | — | Peak utilisation | elec.motor-sizing | ok |
| `u_profis` | — | Plain number | — | Governing utilisation reported by the PROFIS anchorage design, from t… | struct.base-plate | ok |
| `u_prs` | — | Plain number | — | Pressure line velocity over its limit | fp.cylinder | ok |
| `u_pull` | — | Plain number | — | Pull load over the retract force | fp.cylinder | ok |
| `u_pump` | — | Plain number | — | Circuit flow over the pump delivery | fp.cylinder | ok |
| `u_push` | — | Plain number | — | Push load over the extend force | fp.cylinder | ok |
| `u_q` | — | Plain number | — | Utilisation, heat-loss limit | bs.insulation | ok |
| `u_r0` | — | Plain number | — | Pre-charge rule utilisation (p0/p1) | fp.accumulator | ok |
| `u_res` | — | Plain number | — | Fully restrained stress over Sa | bs.pipe-expansion | ok |
| `u_rms` | — | Plain number | — | Continuous utilisation | elec.motor-sizing | ok |
| `u_rp` | — | Plain number | — | Pressure ratio utilisation (p2/p0 at the minimum temperature) | fp.accumulator | ok |
| `u_rtn` | — | Plain number | — | Return line velocity over its limit | fp.cylinder | ok |
| `u_run` | — | Plain number | — | First run: required over available | bs.pipe-expansion | ok |
| `u_s` | — | Plain number | — | Working deflection / travel to solid | mech.spring | ok |
| `u_s0` | — | Plain number | — | Static utilisation | mech.bearing-life | ok |
| `u_scr` | — | Plain number | — | Screening ratio over K1 | bs.pipe-expansion | ok |
| `u_se` | — | Plain number | — | Utilisation, surface-temperature limit | bs.insulation | ok |
| `u_section` | — | Plain number | — | Cross-section interaction | struct.en1993-member | ok |
| `u_sigma` | — | Plain number | — | Bending stress over Sa | bs.pipe-expansion | ok |
| `u_slope` | — | Plain number | — | Slope utilisation | mech.shaft | ok |
| `u_sp` | — | Plain number | — | Utilisation, fracture | struct.lifting | ok |
| `u_speed` | — | Plain number | — | Speed utilisation | elec.motor-sizing | ok |
| `u_stress` | — | Plain number | — | Stress utilisation | struct.plate-bending | ok |
| `u_stretch` | — | Plain number | — | Largest stretch against its limit | fp.oring-gland | ok |
| `u_strip` | — | Plain number | — | Engagement utilisation | mech.bolted-joint | ok |
| `u_suc` | — | Plain number | — | Suction line velocity over its limit | fp.cylinder | ok |
| `u_t` | `u_t:tension-utilisation` | Plain number | — | Net-section utilisation; Tension utilisation; Tension utilisation of the most tensioned bolt; … (4 descriptions) | mech.bolt-group, mech.lifting-lug, struct.connection-ec3, struct.lifting | ok |
| `u_target_1` | — | Plain number | — | Cavity wall: U against target | bs.room-heat-loss | ok |
| `u_to` | — | Plain number | — | Tear-out utilisation | mech.lifting-lug | ok |
| `u_v` | `u_v:shear-utilisation` | Plain number | — | Shear utilisation; Shear utilisation of the most sheared bolt; Utilisation, shear | mech.bolt-group, struct.connection-ec3, struct.lifting | ok |
| `u_vt` | — | Plain number | — | Combined utilisation | struct.connection-ec3 | ok |
| `u_w` | — | Plain number | — | Width utilisation | elec.pcb-trace | ok |
| `u_y` | — | Plain number | — | Yield utilisation | mech.shaft | ok |
| `u_yield` | — | Plain number | — | Yield utilisation in the working state | mech.bolted-joint | ok |
| `upper` | — | Length | mm | Upper deviation (or total zone for position/profile) | mech.tolerance-stack | ok |
| `util` | — | Plain number | — | Governing utilisation of the lug and the spreader | struct.lifting | ok |
| `v` | — | Length | mm | Patch size in y (rectangular plate, central patch) | struct.plate-bending | ok |
| `v_1` | — | Specific volume | m³/kg | Specific volume, state 1 | bs.psychrometrics | ok |
| `v_ext` | — | Velocity | mm/s | Extend speed | fp.cylinder | ok |
| `v_limit` | `v_limit:velocity-limit` | Velocity | m/s | Velocity limit (warning and check) | bs.duct-sizing, bs.pipe-pump | ok |
| `v_peak` | `v_peak:peak-velocity` | Velocity | m/s | Highest velocity | bs.duct-sizing, bs.pipe-pump | ok |
| `v_pk` | — | Velocity | m/s | Peak velocity | elec.motor-sizing | ok |
| `v_prs` | — | Velocity | m/s | Pressure line velocity (cap-port line, worst stroke) | fp.cylinder | ok |
| `v_prs_max` | — | Velocity | m/s | Pressure line velocity limit (your record, and the default is a place… | fp.cylinder | ok |
| `v_req` | — | Velocity | mm/s | Required extend speed, which gives the flow and replaces Q, such as t… | fp.cylinder | ok |
| `v_ret` | — | Velocity | mm/s | Retract speed | fp.cylinder | ok |
| `v_ret_max` | — | Velocity | m/s | Return line velocity limit (your record, and the default is a placeho… | fp.cylinder | ok |
| `v_rtn` | — | Velocity | m/s | Return line velocity | fp.cylinder | ok |
| `v_s1` | `v_s1:velocity` | Velocity | m/s | Velocity, oil line; v, main | bs.duct-sizing, bs.pipe-pump | ok |
| `v_suc` | — | Velocity | m/s | Suction line velocity | fp.cylinder | ok |
| `v_suc_max` | — | Velocity | m/s | Suction line velocity limit (your record, and the default is a placeh… | fp.cylinder | ok |
| `v_target` | — | Velocity | m/s | Target velocity (velocity method) | bs.duct-sizing | ok |
| `v_wind` | — | Velocity | m/s | Air speed over the surface (correlation; 0 = still air) | bs.insulation | ok |
| `vdrop` | — | Voltage | mV | Voltage drop | elec.pcb-trace | ok |
| `w` | `w:distributed-load` | Line force, distributed load, stiffness | kN/m | Distributed load, downward (optional; blank = not used by the point-l… | struct.beam-formulas | RESOLVE (3 meanings) |
| `w` | `w:key-width` | Length | mm | Key width | mech.shaft | RESOLVE (3 meanings) |
| `w` | `w:mass-per-metre` | Mass per length | kg/m | Mass per metre of the bundle | elec.containment | RESOLVE (3 meanings) |
| `w1` | — | Line force, distributed load, stiffness | kN/m | w at a (+ towards local y, i.e. left of i→j; gravity rows: + downward) | struct.frame-2d | ok |
| `w2` | — | Line force, distributed load, stiffness | kN/m | w at b (trapezoid) | struct.frame-2d | ok |
| `w_act` | — | Length | mm | Trace width as drawn | elec.pcb-trace | ok |
| `w_c` | — | Plain number | — | Weight-correction factor on the cable weight and, by the same factor,… | elec.containment | ok |
| `w_max` | — | Length | mm | Maximum deflection | struct.plate-bending | ok |
| `w_min` | — | Length | mm | Minimum trace width | elec.pcb-trace | ok |
| `w_over_t` | — | Plain number | — | Deflection over thickness | struct.plate-bending | ok |
| `w_pipe` | — | Mass per length | kg/m | Weight of the pipe per metre with its contents, in kg/m (enter with μ) | bs.pipe-expansion | ok |
| `x` | `x:position` | Length | m, mm | Position; x | mech.bolt-group, mech.shaft, struct.frame-2d, struct.section-properties | ok |
| `x0` | — | Length | mm | Centroid x | struct.section-properties | ok |
| `x1` | — | Length | mm | Start x | mech.weld-group | ok |
| `x2` | — | Length | mm | End x | mech.weld-group | ok |
| `x_1ph` | — | m·kg/(s³·A²) | mV/A/m | Two cables, single-phase AC: reactive component | elec.voltage-drop | ok |
| `x_3ph` | — | m·kg/(s³·A²) | mV/A/m | Three or four cables, three-phase AC: reactive component | elec.voltage-drop | ok |
| `x_A` | — | Length | mm | Bearing A position | mech.shaft | ok |
| `x_B` | — | Length | mm | Bearing B position | mech.shaft | ok |
| `x_M` | — | Length | m | Position of M_max | struct.beam-formulas | ok |
| `x_Mmax` | — | Length | m | x of M max | struct.frame-2d | ok |
| `x_T1` | — | Length | mm | Torque enters at | mech.shaft | ok |
| `x_T2` | — | Length | mm | Torque leaves at (optional; blank = the shaft length, so every sectio… | mech.shaft | ok |
| `x_b` | — | Length | mm | Bolt row distance from the column axis (outside the column face, insi… | struct.base-plate | ok |
| `x_c` | `x_c:centroid-x` | Length | mm | Centroid x | mech.weld-group, struct.section-properties | ok |
| `x_delta` | — | Length | m | Position of δ_max | struct.beam-formulas | ok |
| `x_end` | — | Length | mm | Segment ends at | mech.shaft | ok |
| `x_hi` | — | Percent quantity | % | Installed stretch: highest acceptable (the installation rule is up to… | fp.oring-gland | ok |
| `x_key` | — | Length | mm | Key position | mech.shaft | ok |
| `x_lo` | — | Percent quantity | % | Installed stretch: lowest acceptable (negative allows a ring that is … | fp.oring-gland | ok |
| `x_mean` | — | Length | mm | Mean result | mech.tolerance-stack | ok |
| `x_nom` | — | Length | mm | Nominal result | mech.tolerance-stack | ok |
| `x_wc_max` | — | Length | mm | Worst-case maximum | mech.tolerance-stack | ok |
| `x_wc_min` | — | Length | mm | Worst-case minimum | mech.tolerance-stack | ok |
| `xi` | — | Plain number | — | ξ reduction for 6.10b | struct.load-combinations | ok |
| `y` | `y:deflection` | Length | m, mm | Deflection at the tip (cantilever) or at the end of the lever (torsio…; y | mech.bolt-group, mech.snap-fit, struct.frame-2d, struct.section-properties | ok |
| `y0` | — | Length | mm | Centroid y | struct.section-properties | ok |
| `y1` | — | Length | mm | Start y | mech.weld-group | ok |
| `y2` | — | Length | mm | End y | mech.weld-group | ok |
| `y_c` | `y_c:centroid-y` | Length | mm | Centroid y | mech.weld-group, struct.section-properties | RESOLVE (2 meanings) |
| `y_c` | `y_c:extreme-fibre-distance` | Length | mm | Extreme-fibre distance from the neutral axis (optional; blank = no st… | struct.beam-formulas | RESOLVE (2 meanings) |
| `y_load` | — | Length | mm | Deflection at the loads | mech.shaft | ok |
| `y_max` | — | Length | mm | Deflection limit at the loads | mech.shaft | ok |
| `y_perm` | — | Length | mm | Permissible deflection | mech.snap-fit | ok |
| `y_tb` | — | Heat transfer coefficient, U-value | W/(m²·K) | Thermal-bridge allowance on the area facing outside | bs.room-heat-loss | ok |
| `yield` | — | Percent quantity | % | Yield (predicted) | mech.tolerance-stack | ok |
| `z` | — | Length | mm | Lever arm between the compression resultant and the bolt row | struct.base-plate | ok |
| `z_1ph` | — | m·kg/(s³·A²) | mV/A/m | Two cables, single-phase AC: impedance | elec.voltage-drop | ok |
| `z_3ph` | — | m·kg/(s³·A²) | mV/A/m | Three or four cables, three-phase AC: impedance | elec.voltage-drop | ok |
| `z_C` | — | Length | mm | Distance from the column axis to the compression resultant | struct.base-plate | ok |
| `z_C_in` | — | Length | mm | Compression resultant distance from the column axis (optional; blank … | struct.base-plate | ok |
| `z_g` | — | Length | mm | Load height above the shear centre, positive towards the compression … | struct.ltb-interaction | ok |
| `z_s` | — | Length | m | Static head at the pump suction (+ flooded, − lift) | bs.pipe-pump | ok |
