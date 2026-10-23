# M6 — Lifting lug and pin joint (`mech.lifting-lug@1.0.0`)

**In plain terms.** A lifting lug is a plate with a hole and a pin through it, and it can fail five ways: the plate can tear across the narrowest section beside the hole, the pin can crush the hole, the plate can shear out from the hole to its end, the pin can shear through, and the pin can bend between the cheek plates that hold it. Each check is a load over an area, or a moment over a section modulus, compared with what the material allows; the largest ratio governs, and it is often the pin in bending.

**Scope.** One lug between two cheek plates, load along the lug, close-fitting pin (d_p ≥ 0.9·d_h). Ported from the legacy `calc.lifting-lug-pin-joint` (ADR-0167). Clause numbers only (ASME BTH-1 Ch. 3).

**Method.** `σ_t = P/((W − d_h)t)`; `σ_br = P/(d_p t)`; `τ_to = P/(2at)`; `τ_p = P/(2πd_p²/4)`; `M = (P/2)(t/4 + g + t_s/2)`, `σ_b = 32M/(πd_p³)`; each over its allowable.

**Inputs.** `P`, `t`, `W`, `d_h`, `a`, `d_p`, `t_s`, `g`, allowables `f_t`, `f_br`, `f_v`, `f_b_pin`, `f_v_pin`. **Outputs.** the five stresses, `M_p`, the five utilisations. **Checks.** net section, bearing, tear-out, pin shear, pin bending.

**Limitations.** No out-of-plane load, no dished or stiffened lugs, no design factor applied (allowables carry it); a pin looser than 0.9·d_h blocks.

**Test cases.** The legacy module's two hand calculations (`ka-legacy-pass`, `ka-legacy-fail`) to five significant figures.
