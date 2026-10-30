# Rolling bearing basic rating life (simple form) (`mech.bearing-life-basic@1.1.0`)

**In plain terms.** A rolling bearing wears out by fatigue of its rolling surfaces, and the load it carries sets how many revolutions that takes: halving the load multiplies the life by eight for a ball bearing. The catalogue's dynamic load rating is the load the bearing survives for a million revolutions with 90 % reliability. Dividing it by the actual equivalent load and raising the ratio to the life exponent gives the rated life, which the running speed turns into hours. A reliability factor shortens the life when fewer than one in ten bearings may fail.

## Scope

Calculates the equivalent dynamic load, the basic rating life L₁₀ and the life modified for reliability of a rolling bearing, against a required life. It takes the reliability factor as entered and has no a_ISO factor.

## Method

- Equivalent dynamic load from the radial and axial loads: F_dyn = X·F_r + Y·F_a.
- Basic rating life in millions of revolutions (p = 3 for ball and 10/3 for roller bearings): L₁₀ = (C/F_dyn)^p.
- Rating life in hours, at speed n: L₁₀h = L₁₀·10⁶ revolutions at speed n.
- Life adjusted for the required reliability: L_nm = a₁·L₁₀; L_nmh = a₁·L₁₀h.
- Range of validity of the life equation: F_dyn ≤ 0.5·C.

`L₁₀h = L₁₀·10⁶·2π/n` takes the speed `n` in rad/s. `C` fills from a released bearing record. The legacy module `calc.bearing-rating-life` (ADR-0167) is ported here, and it also covers `calc.bearing-load-capacity`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `type` | Bearing type (selects the life exponent) |  | ball |  | Setup |
| `C` | Basic dynamic load rating | kN | 35.1 | 0.1 to 100000 | Bearing |
| `F_r` | Radial load | kN | 4 | 0 to 100000 | Loads |
| `F_a` | Axial load | kN | 0 | 0 to 100000 | Loads |
| `n` | Speed | rpm | 1500 | 1 to 100000 | Loads |
| `X` | Radial load factor (bearing catalogue) |  | 1 | 0 to 2 | Factors |
| `Y` | Axial load factor (bearing catalogue) |  | 0 | 0 to 5 | Factors |
| `a_1` | Life modification factor for reliability |  | 1 | 0.05 to 1 | Factors |
| `L_req` | Required life (0 for no criterion) | h | 5000 | 0 to 1000000 | Requirements |

## Assumptions

1. Load and speed are steady, and the bearing is correctly fitted, lubricated and free of contamination. Basis: These are the conditions of the basic rating life.
2. X and Y are the catalogue factors for this bearing and load ratio. Basis: Supplied by the user.

## Outputs

| Symbol | Description |
|---|---|
| `F_dyn` | Equivalent dynamic load |
| `k_P` | Load ratio |
| `L_10` | L₁₀ (millions of revolutions) |
| `L_10h` | L₁₀ in hours |
| `L_nm` | Modified life (millions of revolutions) |
| `L_nmh` | Modified life in hours |

## Ports

A port keeps its output symbol; a link, a pack binding or an inputs file written with the symbol or an alias still resolves.

| Public name | Symbol | Unit | Description |
|---|---|---|---|
| `bearing.life_modified` | `L_nmh` | h | Modified rating life in hours |
| `bearing.life_basic` | `L_10h` | h | Basic rating life in hours |
| `bearing.load_dynamic` | `F_dyn` (also `P`) | kN | Equivalent dynamic load |
| `bearing.load_ratio` | `k_P` |  | Load ratio of the equivalent load to the dynamic load rating |

## Checks

- Required life against modified life (`L_req` against `L_nmh`), made only when `L_req` is above 0; the utilisation is `u_life = L_req/L_nmh`.

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `no-load` | Block | Conservative | The equivalent load must be positive. |
| `light-load` | Block | Conservative | P below a millionth of C: the life is effectively unlimited. |
| `heavy-load` | Block | Conservative | P above half the dynamic load rating is outside the basic rating life equation. |

## Limitations

Basic rating life only: there is no a_ISO lubrication or contamination factor beyond `a_1`. A load above half the dynamic load rating blocks, and so does a load below a millionth of it.

## Why a second bearing template

`mech.bearing-life` (Addendum A) is the full ISO 281 sheet: it derives the reliability factor from a stated reliability, applies a_ISO, takes duty cycles and checks the static safety factor. The legacy module this template is ported from takes the reliability factor `a_1` as an entered number (0.64 at 95 % in its third hand calculation), reports the load ratio `k_P`, and blocks at `F_dyn` above 0.5·`C` and below 10⁻⁶·`C`. The full sheet cannot reproduce those figures, so the legacy known-answer cases run here and nowhere else (ADR-0167, Amendment 2). Use `mech.bearing-life` for a new bearing check.

## Changes

1.1.0 (C-2): the equivalent dynamic load `P` is now `F_dyn` (a bare `P` is a power; the port `bearing.load_dynamic` keeps `P` as an alias). The sheet declares the ports `bearing.life_modified`, `bearing.life_basic`, `bearing.load_dynamic` and `bearing.load_ratio`. The inputs are grouped (`Setup`, `Bearing`, `Loads`, `Factors`, `Requirements`). The check names the required life against the modified life. The three block messages are sentences of at most 20 words and name `F_r`. The scope, the assumption and the references follow the standard (ISO 281:2007, Shigley with its authors and edition). No calculated value changes.

## References

- ISO 281:2007, cl. 6: Basic dynamic load rating and basic rating life.
- Budynas and Nisbett, Shigley's Mechanical Engineering Design, Ch. 11, 11th ed.: Rolling-contact bearings.

## Test cases

The legacy module's three hand calculations: `ka-legacy-ball-radial`, `ka-legacy-roller` and `ka-legacy-combined-95`.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Deep-groove ball bearing under radial load ([sheet](../../worked-examples/mechanical/mech.bearing-life-basic/ball-bearing-radial/sheet.txt)).

```text
p = 3
F_dyn = 1 × 4 kN + 0 × 0 kN = 4 kN
k_P = 4 kN / 35.1 kN = 0.11396
L_10 = (35.1 kN / 4 kN)^3 = 675.68
L_10h = 675.68 × 1e6 × 2 × π / 1500 rpm = 7507.6 h
L_nm = 1 × 675.68 = 675.68
L_nmh = 1 × 7507.6 h = 7507.6 h
u_life = 5000 h / 7507.6 h = 0.666
Governing: PASS  Required life against modified life: 5000 h vs 7508 h  utilisation 0.67  « ISO 281:2007 cl. 6
```
<!-- hand-arithmetic:end -->
