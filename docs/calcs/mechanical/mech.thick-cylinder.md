# M2c — Thick-walled cylinder (`mech.thick-cylinder@2.0.0`)

**Scope.** Lamé stresses at bore and outer surface under internal and external pressure, open or closed ends; von Mises and Tresca; utilisation; thin-wall comparison. Migrated from `calc.thick-walled-cylinder` (v0.16) onto the worksheet engine; the old calculator and its vectors stay and this sheet reproduces them.

**Method.** `A = (a² p_i − b² p_o)/(b² − a²)`, `B = a² b² (p_i − p_o)/(b² − a²)`, `σ_r = A − B/r²`, `σ_θ = A + B/r²`, `σ_z = A` (closed) or 0. von Mises from the three principals; Tresca = max − min. Thin wall: `(p_i − p_o)(a + b)/(2(b − a))`.

**Inputs.** `a`, `b`, `p_i`, `p_o`, `closed`, `sigma_allow`. **Outputs.** stresses at a and b, `sigma_z`, `sigma_vm_*`, `sigma_tr_*`, `sigma_vm_max`, `u`, `sigma_thin`, `k_wall`. **Check.** von Mises against allowable.

**Test cases.** three v0.16 vectors (internal/closed, external/closed, internal/open) — analytic, Confirmed.
