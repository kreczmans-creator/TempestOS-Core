# Accumulator sizing (gas law) (`fp.accumulator@1.7.0`)

**In plain terms.** A hydraulic accumulator is a vessel with a gas cushion that stores fluid under pressure and gives it back when the pump cannot keep up. Gas gets stiffer as it is squeezed, so the volume of fluid you can take from it depends on the pressures at which it is charged and works. The worksheet works out the gas volume and pre-charge pressure needed to deliver the fluid you want between two pressures, picks the next standard size, and flags the bar-litre product against the pressure-equipment rules.

## Scope

Calculates the pre-charge gas volume V0 of a hydraulic accumulator that delivers or absorbs a liquid volume ΔV between two working pressures, with the next listed size and the PSSR bar·litre product. It is not a pressure-vessel code check.

## Method

- Absolute pressures: p_a = p_gauge + p_atm, or as entered in Absolute mode
- Gas volume, one exponent both ways: V0 = ΔV / [(p0/p1)^(1/n_gas) − (p0/p2)^(1/n_gas)], n_gas = 1 isothermal to 1.4 adiabatic
- Gas volume, slow charge then discharge at n_gas: V0 = ΔV·(p2/p0) / [(p2/p1)^(1/n_gas) − 1], from V2 = V0·p0/p2 and V1 = V2·(p2/p1)^(1/n_gas)
- Temperature and gas corrections: V0 × f_rg × the cold-gas factor T_max/T_min, to the power 1/n_gas when one exponent holds both ways, in absolute temperatures
- Pre-charge rules: p0 ≤ r_0·p1 at T_max, and p2/p0(T_min) ≤ r_p with p0(T_min) = p0·T_min/T_max in absolute temperatures
- Pre-charge at the charging temperature: p0(T_c) = p0(T_max)·T_c/T_max in absolute temperatures
- Next size: the smallest listed size not below V0
- PSSR bar-litre product: PS·V of the vessel against 250 bar·L, an indication that needs PS and a volume

### Notes on the method

Absolute pressures `pa = p + p_atm` (Gauge entry) or as entered (Absolute entry), through the shared `GasLaw` kernel (`Tempest.Calc.Engine.Physics.GasLaw`, also behind B6). **Gas process (1.3.0).** The sheet states how the gas is charged to p2 and discharged to p1 (input `process`, output `process`). *Same exponent both ways*, `GasLaw.ChargeVolume`: `V0 = ΔV / [(pa0/pa1)^(1/n_gas) − (pa0/pa2)^(1/n_gas)]`, n = 1 (isothermal) … 1.4 (adiabatic), for a gas charged as fast as it is discharged (a pulsation or a fast cycle). *Slow charge, discharge at n*, `GasLaw.ChargeVolumeSplit`: a held accumulator is charged slowly, so isothermally, to p2 (`V2 = V0·pa0/pa2`) and then discharged at n to p1 (`V1 = V2·(pa2/pa1)^(1/n_gas)`), `ΔV = V1 − V2`, so `V0 = ΔV·(pa2/pa0) / [(pa2/pa1)^(1/n_gas) − 1]`; one exponent from the pre-charge to both pressures understates the volume of a held accumulator (the same hydraulic power unit duty needs 12.7 % more gas volume). *By duty* (the default) is the slow charge in an emergency stroke and the same exponent both ways in energy storage; the shock and pulsation duty always uses one exponent. The volume from either form is multiplied by the real-gas factor `f_rg` (default 1, **at least 1**: nitrogen's compressibility factor exceeds 1 at high pressure, so a factor below 1 would make the accumulator smaller) and times the temperature factor `f_T = (T_max + 273.15)/(T_min + 273.15)`. The pre-charge p0 is defined at T_max and V0 is sized there; at the lowest operating temperature the same gas delivers only `T_min/T_max` of ΔV, so V0 grows by `f_T` (with `T_min = T_max` the factor is 1). Pre-charge at the charging temperature `p0(T_c) = pa0 · (T_c + 273.15)/(T_max + 273.15)` (the addendum writes 273; the 0.15 K changes the result by under 0.01 %); `p0_set` is that pressure on the entered basis (gauge or absolute). Rules (records), both on **absolute** pressures whichever basis the pressures are entered on: `u_r0 = pa0/(r_0·pa1)` with r_0 = 0.9 (p0 at T_max) and `u_rp = (pa2/p0_min)/r_p` with r_p = 4 and `p0_min = pa0/f_T` (p0 at T_min), both PASS/FAIL checks. The `accumulator.rules` record says to note whether the manufacturer states the ratio on gauge or absolute pressures: the Parker numbers read as gauge fail the 0.9·p1 rule by 0.11 %, read as absolute they sit exactly on it. Next size: the smallest listed size not below V0. `bar·L = PS · V` with **PS the vessel's own maximum allowable pressure (bar gauge, entered; blocked below p2 gauge)** and V the vessel volume (`V_vessel`, entered; blocked below V0) or else the selected size; without PS and a volume the `pssr` output reads "indication not available — enter the vessel's PS and volume" and never "below 250".

**Reference diagram.** The worksheet draws a bladder accumulator in section. The gas is bound to `p_0`, the liquid and its dimension to `dV`. `p_1`, `p_2`, `T_max`, `T_min`, `T_c`, `n_gas`, `mode`, `process`, `p_basis`, `r_0`, `r_p`, `p_load` and `sizes` are labels. The drawing is not to scale (ADR-0158, DI-08).

## Inputs

| Symbol | Description | Unit | Default | Usual range | Hard range | Group |
|---|---|---|---|---|---|---|
| `mode` | Duty |  | Energy storage | Energy storage, Shock / pulsation (screening), Emergency stroke |  | Setup |
| `process` | Gas process (By duty: an emergency stroke is charged slowly to p_2 and discharged at n_gas, the other duties use one exponent both ways) |  | By duty | By duty, Same exponent both ways, Slow charge, discharge at n |  | Setup |
| `p_basis` | Pressures entered as |  | Gauge | Gauge, Absolute |  | Setup |
| `dV` | Liquid volume delivered, absorbed or needed for the emergency stroke | L | 10 | 0.01 to 500 | 0.001 to 1000 | Loads |
| `p_0` | Gas pre-charge pressure p0, at the maximum operating temperature | bar | 70 | 1 to 400 | 0 to 700 | Conditions |
| `p_1` | Lowest working pressure p1 | bar | 100 | 1 to 400 | 0 to 700 | Conditions |
| `p_2` | Highest working pressure p2 | bar | 180 | 1 to 400 | 0 to 700 | Conditions |
| `p_atm` | Atmospheric pressure (gauge to absolute) | bar | 1.01325 | 0.7 to 1.05 | 0.5 to 1.1 | Conditions |
| `T_max` | Maximum operating temperature (p0 applies here) | °C | 50 | -20 to 120 | -40 to 150 | Conditions |
| `T_min` | Minimum operating temperature (the gas is coldest and delivers least here) | °C | 20 | -30 to 100 | -40 to 150 | Conditions |
| `T_c` | Charging temperature | °C | 20 | -20 to 60 | -40 to 100 | Conditions |
| `n_gas` (was `n`) | Gas exponent of the discharge, 1 isothermal (slow) to 1.4 adiabatic (fast), also the charge exponent when one exponent holds both ways |  | 1 | up to 1.4 | 1 to 1.4 | Factors |
| `f_rg` | Real-gas factor on V0, the real over the ideal gas volume for this pressure window (1 = ideal gas, at least 1) |  | 1 | 1 to 2.1 | 1 to 3 | Factors |
| `r_0` | Pre-charge rule: p0 (at T_max) at most this fraction of p1, on absolute pressures |  | 0.9 | 0.5 to 0.95 | 0.1 to 1 | Requirements |
| `r_p` | Pre-charge rule: p2/p0 (p0 at T_min) at most this ratio, on absolute pressures |  | 4 | 2 to 10 | 1 to 20 | Requirements |
| `p_load` | Lowest pressure the load still needs, bar gauge, such as the push pressure p_push of the cylinder circuit (optional; blank = not checked) | bar | optional | 1 to 400 | 0 to 700 | Requirements |
| `sizes` | Available accumulator sizes from the manufacturer's range (optional; blank = no size is selected) |  | table |  |  | Size list |
| `sizes.V` | Nominal gas volume (column) | L |  | | | Size list |
| `PS` | Vessel maximum allowable pressure, bar gauge, from the nameplate or relief setting (optional; blank = no PSSR indication) | bar | optional | 10 to 700 | 0 to 1500 | PSSR indication |
| `V_vessel` | Vessel internal volume (optional; blank = the selected size) | L | optional | 0.1 to 1000 | 0.01 to 5000 | PSSR indication |

## Assumptions

1. All gas-law pressures are absolute. Basis: p·Vⁿ = const on a fixed mass of gas.
2. A gauge entry is converted with the atmospheric pressure entered on the sheet. Basis: p·Vⁿ = const on a fixed mass of gas.
3. The exponent n_gas is yours, from 1 for a slow isothermal discharge to 1.4 for a fast adiabatic one. Basis: Gas-law exponents, closed form.
4. With one exponent both ways, a larger n_gas gives a larger volume for a given ΔV. Basis: Gas-law exponents, closed form.
5. A held accumulator is charged slowly and isothermally to p_2, then discharged at n_gas. Basis: Gas-law sizing of a held accumulator: slow charge, fast discharge.
6. An emergency stroke is a held accumulator by default. Basis: Gas-law sizing of a held accumulator: slow charge, fast discharge.
7. Sizing a held accumulator with one exponent understates the volume. Basis: Gas-law sizing of a held accumulator: slow charge, fast discharge.
8. The one-exponent form suits a gas charged as fast as it is discharged. Basis: Gas-law sizing of a held accumulator: slow charge, fast discharge.
9. The shock and pulsation duty always uses one exponent. Basis: Gas-law sizing of a held accumulator: slow charge, fast discharge.
10. The gas is ideal unless a real-gas factor is entered. Basis: Real-gas correction as a user factor.
11. The real-gas factor is at least 1, because real nitrogen needs more volume than ideal gas, never less. Basis: Real-gas correction as a user factor.
12. Above 200 bar the sheet warns that nitrogen departs from the ideal-gas law. Basis: Real-gas correction as a user factor.
13. The pre-charge p0 is defined at the maximum operating temperature T_max, and V0 is sized there. Basis: Manufacturer sizing practice and the gas law at constant mass.
14. At T_min the same gas delivers only the fraction T_min/T_max of ΔV in absolute temperatures. Basis: Manufacturer sizing practice and the gas law at constant mass.
15. A held accumulator multiplies V0 by T_max/T_min. Basis: Manufacturer sizing practice and the gas law at constant mass.
16. One exponent n_gas both ways multiplies V0 by T_max/T_min to the power 1/n_gas. Basis: Manufacturer sizing practice and the gas law at constant mass.
17. The p2/p0 rule is checked with the pre-charge at T_min. Basis: Manufacturer sizing practice and the gas law at constant mass.
18. With T_min equal to T_max the temperature correction is 1. Basis: Manufacturer sizing practice and the gas law at constant mass.
19. The pre-charge rules are records for the accumulator type and make. Basis: Rules are records, not built-in limits.
20. The rules are checked on absolute pressures, whichever basis the pressures are entered on. Basis: Rules are records, not built-in limits.
21. The manufacturer gives the rule values and says whether its ratio is on gauge or absolute pressures. Basis: Rules are records, not built-in limits.
22. The bar·litre product is PS times the vessel volume V. Basis: Regulatory gating: no pressure-vessel code check.
23. PS is the vessel's maximum allowable pressure in bar gauge, never below the highest working pressure p_2. Basis: Regulatory gating: no pressure-vessel code check.
24. The vessel volume is the selected size unless you enter it. Basis: Regulatory gating: no pressure-vessel code check.
25. Without PS and a volume the PSSR indication is not available. Basis: Regulatory gating: no pressure-vessel code check.
26. The indication shows whether the 250 bar·L exception can apply, and it is not a compliance statement. Basis: Regulatory gating: no pressure-vessel code check.
27. The PED, UK PER, PSSR examination and the vessel's design code stay with the manufacturer and the competent person. Basis: Regulatory gating: no pressure-vessel code check.

## Outputs

| Symbol | Description |
|---|---|
| `V0` | Accumulator gas volume required |
| `p0` | Pre-charge, absolute, at the charging temperature |
| `p0_set` | Pre-charge to set at the charging temperature |
| `p0_min` | Pre-charge, absolute, at the minimum operating temperature |
| `f_T` | Gas volume scaling T_max/T_min |
| `u_r0` | Pre-charge rule utilisation (p0/p1) |
| `u_rp` | Pressure ratio utilisation (p2/p0 at the minimum temperature) |
| `pssr` | PSSR Schedule 1 250 bar·L exception (indication only) |
| `process` | Gas process the volume is sized for |
| `V_sel` | Selected size |
| `u_V` | Selected size utilisation |
| `bar_litres` | Pressure-volume product (bar·litres) |

`V0` (L), `V_sel` and `u_V` (when a size list is entered and a size is large enough), `p0` (absolute, at the charging temperature), `p0_set`, `p0_min`, `f_T`, `u_r0`, `u_rp`, `u_load` (with `p_load`), `bar_litres` (only when PS and a volume are given), `pssr` (text), `process` (text, the gas process the volume is sized for). **Ports.** `V0`, `p0`, `bar_litres` (absent until PS and a volume are given) (`acc.V0`, `acc.p0`, `acc.bar_litres`). **Checks.** pre-charge within the p0/p1 rule; pressure ratio within the p2/p0 rule; the lowest working pressure `p_1` carries the load (`u_load = p_load/p_1`, gauge, only with `p_load`); a listed size covers V0 (only when a list is entered and none is large enough). **Warnings.** (1.6.0: `realgas` with `f_rg` left at 1 and no recorded basis fails the check "Real-gas factor against the ideal gas above 100 bar absolute", and `exponent` fails "Gas exponent against a fast discharge" unless a basis for n is recorded.) `realgas` above 200 bar absolute (ideal-gas volume, or your factor in force), and since 1.4.0 above 100 bar absolute while `f_rg` is left at 1; `realgas-window` (1.4.0) for a narrow window (p2/p1 below 1.3) above 100 bar absolute, where the factor on V0 exceeds the compressibility factor at p2; `exponent-real` (1.4.0) for n above 1 from 50 bar absolute (real nitrogen is stiffer than the ideal gas of that exponent); `load-pressure` (1.4.0) for an emergency stroke with no `p_load`; `shock` for the screening duty; `exponent` for n below 1.3 in a shock or emergency duty; `process` when an emergency stroke is sized with one exponent both ways (the held accumulator is then undersized) or when a slow charge is chosen for the shock duty (not applied); `size`; constants not taken from a record.

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as the port's key, so a saved link, a pack binding or an inputs file that uses the symbol or an alias still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `accumulator.volume_gas` | `V0` | L | Accumulator gas pre-charge volume required |
| `accumulator.pressure_precharge` | `p0` | bar | Absolute pre-charge pressure at the charging temperature |
| `accumulator.product_pressure_volume` | `bar_litres` | kJ | Vessel PS (bar gauge) times vessel volume, when PS and a volume are given |

## Checks

Checks the sheet makes:

- Pre-charge against the p0/p1 rule (accumulator.rules record)
- Pressure ratio against the p2/p0 rule (accumulator.rules record)
- Real-gas factor against the ideal gas above 100 bar absolute

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `pressures` | Block | Conservative | Pre-charge, lowest and highest working pressure must be in increasing order (absolute) and above zero. |
| `basis` | Block | Conservative | On the Absolute basis, a pressure below the atmospheric pressure entered here cannot be an absolute pressure: a gauge value was probably typed as absolute. |
| `realgas-ideal` | Block | Conservative | The highest working pressure is above 300 bar absolute and the real-gas factor is left at 1 (ideal gas): the volume would be about half the real nitrogen volume, so enter f_rg. |
| `realgas` | Warning | Unconservative | The highest working pressure is above 200 bar absolute, or above 100 bar absolute with the ideal gas left on (f_rg = 1): nitrogen departs from the ideal-gas law and the volume is too small, so with f_rg left at 1 and no recorded basis for it the real-gas check fails (a block above 300 bar absolute). |
| `realgas-window` | Warning | Unconservative | A narrow pressure window above 100 bar absolute: the real-gas factor on V0 is larger than the compressibility factor at p_2. |
| `exponent-real` | Warning | Unconservative | An exponent above 1 from 50 bar absolute: real nitrogen is stiffer than the ideal gas of that exponent, so an adiabatic discharge needs more gas volume than shown. |
| `load-pressure` | Warning | Unconservative | An emergency stroke without the pressure its load needs: p_1 is not checked against the push load. |
| `shock` | Warning | Conservative | Shock and pulsation sizing is a screening estimate, and the dynamic response of the line is not modelled. |
| `exponent` | Warning | Unconservative | A shock or emergency discharge is fast: an exponent below 1.3 sizes a smaller volume than the usual 1.4, so the exponent check fails unless a basis for n_gas is recorded (a stroke that takes minutes). |
| `process` | Warning | Unconservative | The gas process chosen does not suit the duty: an emergency stroke sized with one exponent both ways understates the volume of a held accumulator, and the shock and pulsation duty uses one exponent whatever process is chosen. |
| `link-basis` | Warning | Conservative | A pressure is linked from another calculation while the pressures are entered as Absolute: linked pressures are gauge, so the atmospheric pressure is added to the linked value. |
| `size` | Warning | Conservative | No listed size is as large as the calculated volume. |
| `temperature` | Block | Conservative | The minimum operating temperature must not be above the maximum. |
| `PS` | Block | Conservative | PS (bar gauge) must not be below the highest working pressure p2 (gauge). |
| `vessel` | Block | Conservative | The vessel volume must not be below the calculated gas volume V0. |

## Limitations

Ideal gas unless the user supplies `f_rg` (no nitrogen real-gas table ships; reference-equation nitrogen at 20 °C needs about 1.13 times the ideal volume for a 170/190/200 bar absolute window, 1.74 for 450/500/700 and 2.01 for 600/650/700, and about 13 to 24 % more than the ideal adiabatic volume at working pressure); single gas pre-charge, no gas-side heat-transfer model (n is the user's), no bladder or piston friction, no oil compressibility or leakage; the shock duty is a gas-volume screening only. The size list and the pre-charge rules are the manufacturer's data and are not shipped. The 250 bar·L indication is PS times the vessel volume as entered (the nominal gas volume of the selected size unless a vessel volume is entered); applicability of PSSR (relevant fluid, definitions) is for the competent person.

## References

- Pressure Systems Safety Regulations 2000 (SI 2000/128), §Schedule 1: 250 bar·litre exception, indication only.
- Pressure Equipment Directive 2014/68/EU and UK PER 2016: Pressure equipment, not checked here.
- Esposito, Fluid Power with Applications, Ch. on accumulators, 7th ed.: Gas-charged accumulator sizing, p·Vⁿ = const.

## Test cases

### Notes on the cases

Analytic, recomputed in python3 (Confirmed): `ka-parker-numbers-absolute` (the Parker HY10-4004/UK numbers p0 90, p1 100, p2 210 bar, ΔV 14 L, isothermal, **read as absolute bar**, T_min = T_max: V0 = 14/(0.9 − 0.428571) = 29.69697 L, p0 81.64475 bar at 20 °C from 50 °C, u_rp 0.58333); `ka-parker-numbers-gauge` (the same numbers as gauge bar: 29.80704 L, and the p0 ≤ 0.9·p1 rule fails by 0.1 %); `ka-adiabatic` (n = 1.4, a gas charged as fast as it is discharged, so entered with *Same exponent both ways*: 36.69279 L; an analytic recompute of the one-exponent relation, not a published example); `ka-charge-temperature` (80 °C to 20 °C: 74.70905 bar abs); `ka-size-and-real-gas` (f_rg 1.05: 31.18182 L, next size 32 L, PS 230 bar gauge on the 32 L: 7360 bar·L); `ka-below-pssr` (1.25 L, PS 110 bar gauge, 2 L vessel: 220 bar·L); `ka-ps-above-working-pressure` (p2 100 bar gauge, 2.4 L vessel: 240 bar·L at the working pressure but 264 bar·L at a PS of 110 bar gauge, so the exception no longer applies); `ka-temperature-range` (20 to 50 °C: f_T 1.102337, V0 32.73606 L, next size 50 L, p0 at T_min 81.64475 bar, u_rp 0.64303). The quoted "about 29.7 to 29.8 L" fits both readings (29.697 as absolute, 29.807 as gauge + 1.01325 bar); the absolute reading puts p0 exactly on the 0.9·p1 limit, which is why it is the one used for the worked example. **Unconfirmed:** `ka-held-slow-charge-fast-discharge` (an emergency stroke, held at p2: isothermal charge and adiabatic discharge, the same numbers: 46.74304 L against 36.69279 L with one exponent, the held gas 20.03273 L at p2 and 34.03273 L at p1; the relation as manufacturers' sizing guides give it, recomputed in python3 and cited to the Parker HY10-4004/UK sizing title only, to be checked against the owner's copy of the manufacturer's sizing guide). **Unconfirmed + Secondary:** `ka-parker-hy10-4004` (V0 about 29.7 L and the catalogue's 32 L selection, as the addendum quotes the Parker example), to be checked against the owner's copy of the catalogue, including whether its pressures are gauge or absolute. **Not pinned (owner check):** the NFPA "How to Size an Accumulator" temperature example (V2 = 1347.77 cm³): its inputs are not stated in the addendum.

- `ka-parker-numbers-absolute`: Parker HY10-4004/UK numbers read as absolute bar: p0 90, p1 100, p2 210, ΔV 14 L, isothermal (analytic) (Confirmed; source: Analytic (p·V = const, V0 = ΔV/[(p0/p1) − (p0/p2)]))
- `ka-parker-numbers-gauge`: The same numbers read as gauge bar with 1.01325 bar atmosphere (analytic) (Confirmed; source: Analytic (absolute = gauge + 1.01325 bar))
- `ka-adiabatic`: Fast charge and fast discharge (one exponent both ways, as in a pulsation cycle), n = 1.4, p0 90, p1 100, p2 210 bar absolute, ΔV 14 L (analytic) (Confirmed; source: Analytic (V0 = ΔV/[(p0/p1)^(1/n) − (p0/p2)^(1/n)], the gas charged as fast as it is discharged))
- `ka-held-slow-charge-fast-discharge`: Emergency stroke, held at p2: charged isothermally, discharged at n = 1.4, p0 90, p1 100, p2 210 bar absolute, ΔV 14 L (analytic) (Unconfirmed; source: Parker Hannifin, HY10-4004/UK, accumulator sizing (mixed isothermal charge and adiabatic discharge relation, as in manufacturers' sizing guides); analytic recompute in python3)
- `ka-charge-temperature`: Pre-charge set at 20 °C for a 80 °C maximum (analytic) (Confirmed; source: Analytic (p0(Tc) = p0(Tmax)·(Tc + 273.15)/(Tmax + 273.15)))
- `ka-size-and-real-gas`: Real-gas factor 1.05 and a size list: next size above the corrected volume (analytic) (Confirmed; source: Analytic (V0 = f_rg·V0_ideal; next listed size; bar·L = PS·V))
- `ka-below-pssr`: A small accumulator below the 250 bar·L indication (analytic) (Confirmed; source: Analytic (V0 = ΔV/[(p0/p1) − (p0/p2)]; bar·L = PS·V of the vessel))
- `ka-ps-above-working-pressure`: PS above the working pressure: the exception that the working pressure alone would allow does not apply (analytic) (Confirmed; source: Analytic (bar·L = PS·V, PS the vessel's maximum allowable pressure in bar gauge))
- `ka-temperature-range`: Operating range 20 to 50 °C: the gas volume grows by T_max/T_min and the pressure ratio is checked with the cold pre-charge (analytic) (Confirmed; source: Analytic (V0 = f_rg·V0_ideal·T_max/T_min; p0(T_min) = p0·T_min/T_max, absolute temperatures))
- `ka-parker-hy10-4004`: Parker HY10-4004/UK accumulator selection example: about 29.7 L, catalogue selection 32 L (Unconfirmed; source: Parker Hannifin, HY10-4004/UK, accumulator sizing example as quoted second-hand)

## Changes

**Changes in 1.7.0 (C-2 consistency, phase C).** Layout and wording; no calculated value changes. The gas exponent `n` is now `n_gas` (alias kept). The inputs sit in the groups Setup (mode, process, p_basis), Loads, Conditions, Factors and Requirements, with the size list and the PSSR inputs after them, and every numeric input has a usual range inside its hard range (the hard limits are unchanged; the valid range shown on the sheet is now the usual range). The three checks read "Pre-charge against the p0/p1 rule", "Pressure ratio against the p2/p0 rule" and "Load pressure against the lowest working pressure" and show their values; the utilisations `u_r0`, `u_rp` and `u_load` are the same numbers as before. The ports are `accumulator.volume_gas`, `accumulator.pressure_precharge` and `accumulator.product_pressure_volume`; the product is declared in kJ on the port (100 J per bar·L), and the sheet output stays in bar·L. The scope is two sentences, the 27 assumptions are one short sentence each, the messages are one or two short sentences, and the Esposito locator names the edition. The two regulations (PSSR, PED and UK PER) keep their form, which the book pattern of the sheet standard does not fit.

**Changes in 1.5.1 (fp.accumulator-03).** The temperature factor `f_T` depends on the gas process. The pre-charge is defined at `T_max`; cooled at constant volume it is `p0' = p0·T_min/T_max`. For a held accumulator (isothermal charge to p2, then discharge at n: the default for an emergency stroke) `V0 ∝ p2/p0'`, so `f_T = T_max/T_min`, the first power, as before. For one exponent both ways (energy storage, shock and pulsation) `V0 = ΔV/[(p0'/p1)^(1/n_gas) − (p0'/p2)^(1/n_gas)] = V0_ideal·(T_max/T_min)^(1/n_gas)`, so `f_T = (T_max/T_min)^(1/n_gas)`; the first power over-sized it (10 L, 90/100/200 bar gauge, n 1.4, 60 °C to −20 °C: `f_T` 1.316 and `V0` 36.512 L before, 1.2167 and 33.757 L now, equal to the direct formula at `p0'`). With n = 1 the two agree. The new step `f_Tabs` is the plain ratio; `p0_min` still follows it.

**Real gas, exponent and load pressure (1.4.0).** The real-gas factor was capped at 1.5 and warned only above 200 bar absolute, while real nitrogen needs 2.0 times the ideal volume for a 600 to 700 bar window, and the factor of a narrow window (170/190/200 bar absolute: 1.134, so 223.5 L became 253.5 L) is larger than the compressibility factor at p2 and went unannounced. `f_rg` now runs to 3 and is defined as the bracket ratio of the real to the ideal gas volume for the sheet's own window, `f_rg = Z_0·(1/p_1 − 1/p_2)/(Z_1/p_1 − Z_2/p_2)` for an isothermal discharge with Z read at p0, p1 and p2 from a gas-property source; the sheet warns (`realgas`) from 100 bar absolute while it is left at 1 and (`realgas-window`) for a narrow window. An exponent above 1 is the ideal gas's: from 50 bar absolute the sheet warns (`exponent-real`) that real nitrogen's effective isentropic exponent (about 1.5 to 2.1 between 60 and 350 bar) needs 13 to 24 % more gas volume, and that an entered `f_rg` is applied on top and must be the discharge path's. The lowest working pressure `p_1` is checked against the pressure the load needs (`p_load`, gauge): in the hydraulic power unit the cylinder's `p_push` is linked there, so a heavier push load fails the accumulator instead of leaving every check green with an accumulator that cannot finish the stroke; an emergency stroke without it warns (`load-pressure`).

**Ideal gas refused above 300 bar (1.5.0, fp.accumulator-01 remainder).** With `f_rg` left at 1 and `p_2` above 300 bar absolute the sheet is blocked (rule `realgas-ideal`) with a message to enter the real-gas factor: reference-EOS nitrogen at 20 °C needs 1.25 to 1.35 times the ideal volume in a narrow window at 300 bar, 1.63 for 400/450/500 bar, 1.75 for 450/500/700 and 2.01 for 600/650/700 bar absolute, so the ideal-gas volume would be about half the real one. The threshold is 300 bar, not the 200 bar the finding first named: the template's own reference case (Parker, 90/100/210 bar absolute, real factor 1.05) lies between, and from 100 to 300 bar absolute the sheet keeps its warning (`realgas`).

**Linked pressures (1.2.0).** A pressure input linked from another calculation (the cylinder's supply pressure `p_sys`, gauge, into `p_2`) is read on this sheet's `p_basis`. In Absolute mode the linked gauge value would be read one atmosphere low, so the sheet warns (`link-basis`) for each of `p_0`, `p_1`, `p_2` that is linked. The default basis is Gauge, which the hydraulic power unit pack keeps.

**Slow charge, fast discharge (1.3.0).** Before 1.3.0 the sheet sized every duty with one exponent from the pre-charge to both working pressures, which is right only when the charge is as fast as the discharge. A held accumulator (emergency stroke) is charged slowly; its volume is larger (the hydraulic power unit F2 step from 26.70 L to 30.08 L, the actuated-linkage F2 step from 1.078 L to 1.310 L). Energy storage keeps the one-exponent default and offers the slow charge as a choice. Sheets issued under 1.2.0 for an emergency stroke should be re-run.

**Pressure basis (1.3.1).** A gauge number typed on the Absolute basis gives a smaller volume with no other sign (4, 5 and 10 bar read as absolute give 2.50 L, against 2.64 L as gauge), and a pre-charge of 0.5 bar absolute is a vacuum. On the Absolute basis a pressure below `p_atm` is now blocked (`basis`) with a message that names the input and says to switch `p_basis` to Gauge. Every pressure on the sheet prints its basis beside the unit: `p_0`, `p_1`, `p_2` follow `p_basis`, `p_atm` is "bar a", `PS` is "bar g", and the working and results show "bar a" or "bar g". The labels name the basis and convert nothing. F1 (`p`, `p_back`) and F3 (`p_op`) print "bar g".

**Changes in 1.6.0 (C-2 fix, F2).** (1) `link-basis`: a pressure linked from another calculation is gauge, and on the Absolute basis it was read as absolute, one atmosphere low (a warning that did not stop a pass). The atmospheric pressure is now added to a linked pressure on the Absolute basis (the warning says so). python3: p_0 90 and p_1 100 bar, p_2 linked as 160 bar gauge, dV 14 L, n = 1: V0 41.0509 L (the old reading, 160 bar absolute, gave 41.4815 L). (2) `exponent`: a shock or emergency discharge with n below 1.3 sizes a smaller gas volume than n = 1.4 (the emergency stroke of `ka-held-slow-charge-fast-discharge`: 29.697 L at n = 1, 46.743 L at n = 1.4). The check "Gas exponent against a fast discharge" fails; a basis recorded for n (a stroke that takes minutes) is the person's, and only the warning stays. (3) `realgas`: with `f_rg` left at 1 above 100 bar absolute the volume is the ideal-gas value, 13 % and more too small; the check "Real-gas factor against the ideal gas above 100 bar absolute" fails unless `f_rg` is entered or a basis for taking the ideal gas is recorded (the clamp-circuit worked example records one). It is a failing check and not a block because the Parker known-answer cases (210 bar absolute, ideal gas) are shipped cases; above 300 bar absolute the block `realgas-ideal` stays. The expected values of every known-answer case are unchanged.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Bladder accumulator for a clamp circuit: 14 L delivered between 100 and 210 bar ([sheet](../../worked-examples/fluid-power/fp.accumulator/clamp-circuit-bladder-accumulator/sheet.txt)).

```text
pa_0 = 90 bar a
pa_1 = 100 bar a
pa_2 = 210 bar a
p2_g = max(210 bar a − 1.0132 bar a, 0 bar) = 208.99 bar g
V0_ideal = 14 L / [(0.9) − (0.42857)] = 29.697 L
f_Tabs = 50 degC / 1 K / (30 degC / 1 K) = 1.066
f_T = 1.066^(1 / 1) = 1.066
V0 = 29.697 L × 1 × 1.066 = 31.656 L
T_ratio = 20 degC / 1 K / (50 degC / 1 K) = 0.90716
p0 = 90 bar a × 0.90716 = 81.645 bar a
p0_min = 90 bar a / 1.066 = 84.43 bar a
p0_set = 81.645 bar a
p0_max = 0.9 × 100 bar a = 90 bar a
u_r0 = 90 bar a / 90 bar a = 1
… 6 more step(s) on the sheet
Governing: PASS  Pre-charge against the p0/p1 rule: 90 bar a vs 90 bar a  utilisation 1.00  « accumulator.rules record
```
<!-- hand-arithmetic:end -->
