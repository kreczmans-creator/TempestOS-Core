# Rolling bearing basic rating life, simple form (`mech.bearing-life-basic@1.0.1`)

**In plain terms.** A rolling bearing wears out by fatigue of its rolling surfaces, and the load it carries sets how many revolutions that takes: halving the load multiplies the life by eight for a ball bearing. The catalogue's dynamic load rating is the load the bearing survives for a million revolutions with 90 % reliability; dividing it by the actual equivalent load and raising the ratio to the life exponent gives the rated life, which the running speed turns into hours. A reliability factor shortens the life when fewer than one in ten bearings may fail.

**Scope.** Steady radial and axial load and speed. Ported from the legacy `calc.bearing-rating-life` (ADR-0167); also covers `calc.bearing-load-capacity`. Clause numbers only (ISO 281 §6). `C` fills from a released bearing record.

**Method.** `P = X·F_r + Y·F_a`; `L₁₀ = (C/P)^p` million revolutions (p = 3 ball, 10/3 roller); `L₁₀h = L₁₀·10⁶·2π/n`; `L_nm = a₁·L₁₀`, `L_nmh = a₁·L₁₀h`.

**Inputs.** `type`, `C`, `F_r`, `F_a`, `X`, `Y`, `n`, `a_1`, `L_req`. **Outputs.** `P`, `k_P`, `L_10`, `L_10h`, `L_nm`, `L_nmh`. **Checks.** modified life against required (when `L_req` > 0).

**Why a second bearing template.** `mech.bearing-life` (Addendum A, M5) is the full ISO 281 sheet: it derives the reliability factor from a stated reliability, applies a_ISO, takes duty cycles and checks the static safety factor. The legacy module this template is ported from takes the reliability factor a₁ as an entered number (0.64 at 95 % in its third hand calculation), reports the load ratio `k_P` and blocks at P > 0.5·C and below P = 10⁻⁶·C; M5 cannot reproduce those figures, so the legacy known-answer cases run here and nowhere else (ADR-0167, Amendment 2). Use M5 for a new bearing check.

**Limitations.** Basic rating life only (no a_ISO lubrication or contamination factor beyond a₁); P > 0.5·C blocks.

**Test cases.** The legacy module's three hand calculations (`ka-legacy-ball-radial`, `ka-legacy-roller`, `ka-legacy-combined-95`).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Deep-groove ball bearing under radial load ([sheet](../../worked-examples/mechanical/mech.bearing-life-basic/ball-bearing-radial/sheet.txt)).

```text
p = 3
P = 1 × 4 kN + 0 × 0 kN = 4 kN
k_P = 4 kN / 35.1 kN = 0.11396
L_10 = (35.1 kN / 4 kN)^3 = 675.68
L_10h = 675.68 × 1e6 × 2 × π / 1500 rpm = 7507.6 h
L_nm = 1 × 675.68 = 675.68
L_nmh = 1 × 7507.6 h = 7507.6 h
u_life = 5000 h / 7507.6 h = 0.666
Governing: PASS  Modified life against required life: u_life = 0.666 vs u_life ≤ 1  utilisation 0.666  « ISO 281 §6
```
<!-- hand-arithmetic:end -->
