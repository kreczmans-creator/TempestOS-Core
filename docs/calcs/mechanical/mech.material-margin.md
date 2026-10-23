# M12 — Material strength margin (`mech.material-margin@1.0.0`)

**In plain terms.** A part is safe in static loading while the stress in it stays below what the material can take, with a margin for what is not known. The factor of safety on yield is the yield strength over the working stress; on ultimate strength it is the tensile strength over the stress, which is usually required to be larger because fracture is worse than yielding. Filling the strengths from a released material record makes the margin cite where the strengths came from.

**Scope.** Static stress at a critical point against yield and, when given, ultimate strength. Ported from the legacy `calc.material-selection-margin` (ADR-0167). `f_y` and `f_u` fill from a released material record.

**Method.** `n_y = f_y/σ`; `n_u = f_u/σ`; `n_gov = min(n_y, n_u/k_u)`; `u = n_req/n_gov`.

**Inputs.** `sigma`, `f_y`, optional `f_u`, `k_u`, `n_req`. **Outputs.** `n_y`, `n_u`, `n_gov`, `u`. **Checks.** governing factor against required.

**Limitations.** Static only; the applied stress must already be the equivalent stress at the critical point.

**Test cases.** A hand calculation (`ka-margin-hand`); the legacy module had property tests only.
