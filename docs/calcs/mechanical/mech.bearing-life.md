# M7 — Rolling bearing rating life (`mech.bearing-life@1.0.0`)

**In plain terms.** A rolling bearing wears out by fatigue of its rolling surfaces, and the load it carries sets how many revolutions that takes: halving the load multiplies the life by eight for a ball bearing. The catalogue's dynamic load rating is the load the bearing survives for a million revolutions with 90 % reliability; dividing it by the actual equivalent load and raising the ratio to the life exponent gives the rated life, which the running speed turns into hours. A reliability factor shortens the life when fewer than one in ten bearings may fail.

**Scope.** Steady radial and axial load and speed. Ported from the legacy `calc.bearing-rating-life` (ADR-0167); also covers `calc.bearing-load-capacity`. Clause numbers only (ISO 281 §6). `C` fills from a released bearing record.

**Method.** `P = X·F_r + Y·F_a`; `L₁₀ = (C/P)^p` million revolutions (p = 3 ball, 10/3 roller); `L₁₀h = L₁₀·10⁶·2π/n`; `L_nm = a₁·L₁₀`, `L_nmh = a₁·L₁₀h`.

**Inputs.** `type`, `C`, `F_r`, `F_a`, `X`, `Y`, `n`, `a_1`, `L_req`. **Outputs.** `P`, `k_P`, `L_10`, `L_10h`, `L_nm`, `L_nmh`. **Checks.** modified life against required (when `L_req` > 0).

**Limitations.** Basic rating life only (no a_ISO lubrication or contamination factor beyond a₁); P > 0.5·C blocks.

**Test cases.** The legacy module's three hand calculations (`ka-legacy-ball-radial`, `ka-legacy-roller`, `ka-legacy-combined-95`).
