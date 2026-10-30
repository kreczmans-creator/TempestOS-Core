# Duct sizing (`bs.duct-sizing@1.2.1`)

**In plain terms.** Ducts are sized so that air does not lose too much pressure per metre or move fast enough to be noisy. The equal-friction method picks each size to give the same pressure loss per metre; the velocity method picks it to give a target speed. A rectangular duct is compared with the round duct that has the same friction, and the losses along the longest run, straight ducts plus fittings, give the pressure the fan must supply.

## Scope

Calculates duct sizes by the equal-friction or the velocity method, rounded up to standard sizes. It reports the velocity, friction rate and losses of each section and the pressure drop along the index run.

## Method

- Round duct diameter from the design target: D by Colebrook with the air properties at T (equal friction), or D = √(4Q/πv) (velocity), rounded up to the next standard size.
- Rectangular duct of equal friction and capacity: D_e = 1.30 (ab)^0.625/(a + b)^0.25.
- Flat-oval duct of equal friction and capacity: D_e = 1.55 A^0.625/P^0.25, with A = πb²/4 + b(a − b) and P = πb + 2(a − b).
- Velocity method, rectangular or flat-oval duct with side a fixed: b from the free area A = Q/v (rectangle b = A/a; flat oval the smaller root of (1 − π/4)b² − ab + A = 0), rounded up; D_e is kept for the friction only.
- Friction loss per metre, at the velocity v_e of the equivalent round duct: Δp/L = f ρ v_e²/(2D_e).
- Fitting losses, at the actual velocity: Δp = ΣC ρ v²/2.
- Index-run totals, summed over every section of the run: Δp_run = Σ(Δp/L · L + ΣC ρ v²/2).

### Notes on the method

Dry air ρ = p/(287.055 T), Sutherland viscosity; Darcy–Weisbach Δp/L = f ρ v²/(2D_e) with Colebrook (laminar/transition warned); equal friction solves D for the target rate by bisection; D_e,rect = 1.30 (ab)^0.625/(a + b)^0.25, D_e,oval = 1.55 A^0.625/P^0.25 (ASHRAE Ch. 21 relations, cited); loss coefficients C are user inputs (D-19).

## Inputs

| Symbol | Description | Unit | Default | Usual range | Hard range | Group |
|---|---|---|---|---|---|---|
| `method` | Sizing method |  | equal friction | equal friction, velocity |  | Setup |
| `sections` | Duct sections |  | table |  |  | Setup |
| `sections.name` | Section (column) |  | optional | | | Setup |
| `sections.Q` | Airflow (column) | m³/h |  | | | Setup |
| `sections.L` | Length (column) | m |  | | | Setup |
| `sections.shape` | Shape (column) |  | optional | | | Setup |
| `sections.a` | Fixed side a (rectangular/oval; blank = size) (column) | mm | optional | | | Setup |
| `sections.D` | Given diameter or side b (blank = size) (column) | mm | optional | | | Setup |
| `sections.C` | Σ fitting loss coefficients (column) |  | optional | | | Setup |
| `sections.dp_eq` | Equipment / terminal loss (column) | Pa | optional | | | Setup |
| `sections.index` | On the index run (column) |  | optional | | | Setup |
| `round_to` | Round sizes up to a multiple of | mm | 50 | 1 to 100 | 1 to 1000 | Geometry |
| `eps_rough` (was `eps`) | Duct roughness | mm | 0.09 | 0.03 to 3 | 0 to 50 | Material |
| `T_air` (was `T`) | Air temperature | °C | 20 | -20 to 60 | -50 to 200 | Conditions |
| `p_atm` | Barometric pressure | kPa | 101.325 | 80 to 105 | 10 to 1000 | Conditions |
| `dp_target` | Target friction rate (equal friction) | Pa/m | 1 | 0.3 to 5 | 0.01 to 100 | Requirements |
| `v_target` | Target velocity (velocity method) | m/s | 6 | 2 to 20 | 0.1 to 100 | Requirements |
| `v_limit` | Velocity limit (warning and check) | m/s | 8 | 2 to 30 | 0.1 to 100 | Requirements |
| `dp_limit` | Total pressure-drop limit (check) | Pa | 300 | 50 to 2000 | 1 to 100000 | Requirements |

## Assumptions

1. Air is dry at the stated temperature and pressure. Basis: Ideal gas, ρ = p/(287.055 T).
2. The viscosity of air follows Sutherland's law. Basis: Standard air properties.
3. Rectangular and flat-oval ducts are sized through the equivalent diameter for equal friction and capacity. Basis: Huebscher / Heyt–Diaz relations as given in ASHRAE Fundamentals Ch. 21.
4. Fitting loss coefficients C are user-supplied (manufacturer or ASHRAE/CIBSE data, not embedded) and refer to the section's velocity pressure. Basis: Non-negotiable: no standard tables embedded.

## Outputs

| Symbol | Description |
|---|---|
| `D_s1` | D, main |
| `v_s1` | v, main |
| `Re_s1` | Re, main |
| `f_s1` | f, main |
| `dpL_s1` | Δp/L, main |
| `dp_f_s1` | Friction loss, main |
| `dp_fit_s1` | Fitting loss, main |
| `dp_s1` | Section loss, main |
| `dp_index` | Index-run Δp |
| `dp_all` | Σ all sections |
| `v_peak` | Highest velocity |
| `a_s1` | a, branch |
| `b_s1` | b, branch |
| `De_s1` | D_e, branch |

## Ports

A port is listed under its public name, `<area>.<quantity>`. The output symbol stays as the port's key, so a saved link, a pack binding or an inputs file that uses the symbol or an alias still resolves.

| Public name | Output symbol | Unit | Description |
|---|---|---|---|
| `duct.pressure_drop_index` | `dp_index` | Pa | Pressure drop along the index run |
| `duct.velocity_peak` | `v_peak` | m/s | Highest velocity of any section |

## Checks

Checks the sheet makes:

- Index-run pressure drop against limit
- Highest velocity against limit

Applicability rules:

| Rule | Severity | Effect on the result | Condition |
|---|---|---|---|
| `sections` | Block | Conservative | At least one section is required. |
| `geometry` | Block | Conservative | A given size must be positive, and a fixed side needs the rectangular or flat-oval shape. |
| `velocity` | Warning | Conservative | A section exceeds the velocity limit. |
| `laminar` | Warning | Conservative | A section is laminar or transitional (Re < 4000), which is unusual for ductwork. |
| `summary-only` | Warning | Conservative | More than 50 sections: per-section lines are omitted. |

## Limitations



## References

- ASHRAE Handbook — Fundamentals, Ch. 21: Duct design (friction chart basis, equivalent diameters, loss coefficients).
- CIBSE Guide C, §4: Flow of fluids in pipes and ducts.
- BS EN 1505:1998: Ventilation for buildings: sheet metal air ducts and fittings with rectangular cross-section, dimensions.
- BS EN 1506:2007: Ventilation for buildings: sheet metal air ducts and fittings with circular cross-section, dimensions.

## Test cases

### Notes on the cases

400 mm round at 1 m³/s against the ASHRAE friction-chart point (Colebrook, within 0.5 %); 600 × 300 rectangular equivalent diameter (ASHRAE relation); velocity-method rounding (analytic).

- `ka-round-given`: Round duct of given size, friction rate (ASHRAE friction-chart point) (Confirmed; source: ASHRAE Handbook — Fundamentals, 2017)
- `ka-rectangular-equivalent`: Rectangular 600 × 300 equivalent diameter (ASHRAE Ch. 21 relation) (Confirmed; source: ASHRAE Handbook — Fundamentals, 2017)
- `ka-velocity-method`: Velocity method sizes to the next standard round size (analytic) (Confirmed; source: Analytic)

## Changes

**1.2.1.** Declares the unit change of 1.2.0 (ADR-0177 review): the sections column `Q` was in m³/s before 1.2.0, so a bare stored cell from an earlier version is read in m³/s, never m³/h. No calculated value changes.

**Changes in 1.2.0 (C-2 consistency, phase C).** Layout and wording; no calculated value changes. The air temperature `T` is now `T_air` and the duct roughness `eps` is now `eps_rough`; the old symbols stay as aliases, so a saved calculation, an inputs file, a CSV and a pack binding that use the old symbol still load. The airflow column `Q` of the duct table now reads m³/h by default, with hard limits 0.0036 to 3.6·10⁶ m³/h; the value in SI does not change, and a cell saved with its unit keeps its value. The inputs sit in the groups Setup, Geometry, Material, Conditions and Requirements, and every numeric input has a usual range inside its hard range. The checks read "Index-run pressure drop against limit" and "Highest velocity against limit" and show the two values. The ports are `duct.pressure_drop_index` and `duct.velocity_peak`. The scope is two sentences, each assumption is one short sentence, and the two standards carry their year (BS EN 1505:1998, BS EN 1506:2007).

**Changes in 1.1.0.** Velocity method, rectangular or flat-oval duct with side `a` fixed (or chosen): `b` is sized from the free area `A = Q/v` (rectangle `b = A/a`; flat oval the smaller root of `(1 − π/4) b² − a b + A = 0`), then rounded up, and `D_e` is used for the friction only. Sizing `b` from the `D_e` of the round duct at the target velocity gave a duct about 5 to 12 % too large with a velocity below target (`Q` 0.5 m³/s, `a` 500 mm, 6 m/s, 10 mm steps: rectangle `b` 190 → 170 mm, `v` 5.26 → 5.88 m/s; flat oval `b` 200 → 190 mm, `v` 5.47 → 5.73 m/s). The equal-friction method is unchanged.

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
Governing: PASS  Highest velocity against limit: 7.074 m/s vs 8 m/s  utilisation 0.88
```
<!-- hand-arithmetic:end -->
