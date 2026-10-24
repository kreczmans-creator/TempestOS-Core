#!/usr/bin/env python3
"""Independent re-derivation of struct.load-combinations (Track P, P1).

Method sources (cited by title, edition, clause; values only):
  BS EN 1990:2002+A1:2005 (Eurocode, basis of structural design)
    Eq. 6.10, 6.10a, 6.10b (cl. 6.4.3.2); Eq. 6.11b (cl. 6.4.3.3);
    Eq. 6.14b, 6.15b, 6.16b (cl. 6.5.3); Annex A1 Table A1.1 (psi), Table A1.2(B) (gamma, xi).
  UK NA to BS EN 1990: NA.A1.2(B) gives gamma_G,sup 1.35, gamma_G,inf 1.0, gamma_Q 1.5, xi 0.925;
  recommended Annex A1 values are gamma_G,sup 1.35, gamma_G,inf 1.0, gamma_Q 1.5, xi 0.85.
Rule used: a permanent action takes gamma_G,sup (xi in 6.10b) if it adds to the effect sought,
gamma_G,inf if it opposes it. A variable action enters only if it adds. Each variable is tried as leading.
Exit code 0 when every KAT value matches within max(KAT tol, 1e-6).
"""
import sys

# Table A1.1 recommended psi (psi0, psi1, psi2)
PSI = {
    "A domestic": (0.7, 0.5, 0.3), "B office": (0.7, 0.5, 0.3), "C congregation": (0.7, 0.7, 0.6),
    "D shopping": (0.7, 0.7, 0.6), "E storage": (1.0, 0.9, 0.8), "F traffic <= 30 kN": (0.7, 0.7, 0.6),
    "G traffic 30-160 kN": (0.7, 0.5, 0.3), "H roofs": (0.0, 0.0, 0.0),
    "snow <= 1000 m": (0.5, 0.2, 0.0), "snow > 1000 m": (0.7, 0.5, 0.2),
    "wind": (0.6, 0.2, 0.0), "thermal": (0.6, 0.5, 0.0),
}
PERM, ACC = "G permanent", "accidental"


def combos(actions, sign, gsup=1.35, ginf=1.0, gq=1.5, xi=0.85, kfi=1.0, method="6.10", psi=PSI):
    """Return dict of extreme effects. sign=+1 for maximum, -1 for minimum.
    actions: list of (name, category, Fk). Works on s = sign*Fk so 'adds' means s > 0."""
    G = [(n, sign * f) for n, c, f in actions if c == PERM]
    Q = [(n, c, sign * f) for n, c, f in actions if c not in (PERM, ACC)]
    A = [(n, sign * f) for n, c, f in actions if c == ACC]

    def perm(factor_sup, factor_inf):
        return sum((factor_sup if s > 0 else factor_inf) * s for _, s in G)

    def best(f):
        """max over leading choice of f(lead_index); no variables -> f(None)."""
        if not Q:
            return f(None)
        return max(f(k) for k in range(len(Q)))

    def pos(x):
        return x if x > 0 else 0.0

    r = {}
    # ULS
    if method == "6.10":
        r["uls"] = best(lambda k: perm(gsup, ginf) + sum(
            gq * kfi * pos(s) * (1.0 if j == k else psi[c][0]) for j, (n, c, s) in enumerate(Q)))
    else:
        a = perm(gsup, ginf) + sum(gq * kfi * psi[c][0] * pos(s) for n, c, s in Q)
        b = best(lambda k: perm(xi * gsup, ginf) + sum(
            gq * kfi * pos(s) * (1.0 if j == k else psi[c][0]) for j, (n, c, s) in enumerate(Q)))
        r["uls"] = max(a, b)
        r["610a"], r["610b"] = a, b
    g1 = sum(s for _, s in G)
    r["char"] = best(lambda k: g1 + sum(pos(s) * (1.0 if j == k else psi[c][0]) for j, (n, c, s) in enumerate(Q)))
    r["freq"] = best(lambda k: g1 + sum(pos(s) * (psi[c][1] if j == k else psi[c][2]) for j, (n, c, s) in enumerate(Q)))
    r["qp"] = g1 + sum(pos(s) * psi[c][2] for n, c, s in Q)
    if A:
        ad = sum(pos(s) for _, s in A)
        r["acc"] = best(lambda k: g1 + ad + sum(pos(s) * (psi[c][1] if j == k else psi[c][2])
                                                for j, (n, c, s) in enumerate(Q)))
    return {k: sign * v for k, v in r.items()}


def E(actions, **kw):
    mx = combos(actions, +1, **kw)
    mn = combos(actions, -1, **kw)
    out = {"E_d_max": mx["uls"], "E_d_min": mn["uls"], "E_k_max": mx["char"], "E_k_min": mn["char"],
           "E_fr_max": mx["freq"], "E_fr_min": mn["freq"], "E_qp_max": mx["qp"], "E_qp_min": mn["qp"]}
    if "acc" in mx:
        out["E_acc_max"], out["E_acc_min"] = mx["acc"], mn["acc"]
    return out


KATS = [
    ("ka-610-single", [("Dead", PERM, 10), ("Office", "B office", 5)], {},
     [("E_d_max", 21, 1e-9), ("E_d_min", 10, 1e-9), ("E_k_max", 15, 1e-9), ("E_fr_max", 12.5, 1e-9), ("E_qp_max", 11.5, 1e-9)]),
    ("ka-610ab-single", [("Dead", PERM, 10), ("Office", "B office", 5)], {"method": "6.10ab"},
     [("E_d_max", 18.975, 1e-9)]),
    ("ka-610-two-variables", [("Dead", PERM, 10), ("Office", "B office", 5), ("Wind", "wind", 4)], {},
     [("E_d_max", 24.75, 1e-9), ("E_k_max", 17.5, 1e-9)]),
    ("ka-uplift", [("Roof", PERM, 10), ("Uplift", "wind", -20)], {},
     [("E_d_min", -20, 1e-9), ("E_d_max", 13.5, 1e-9)]),
    ("ka-designers-guide", [("Perm", PERM, 20), ("Dom", "A domestic", 8), ("Snow", "snow <= 1000 m", 3)],
     {"method": "6.10ab"}, [("E_d_max", 37.65, 1e-9)]),
]


def main():
    fails = 0
    print("%-22s %-10s %10s %12s %10s %8s  %s" % ("case", "symbol", "KAT", "independent", "rel.diff", "KAT tol", "result"))
    for name, acts, kw, exp in KATS:
        out = E(acts, **kw)
        for sym, val, tol in exp:
            ind = out[sym]
            rd = abs(ind - val) / abs(val) if val else abs(ind)
            ok = rd <= max(tol, 1e-6)
            fails += not ok
            print("%-22s %-10s %10.5g %12.6g %10.2e %8.0e  %s" % (name, sym, val, ind, rd, tol, "PASS" if ok else "FAIL"))
    # EXTRA_CHECKS_BELOW
    print("\nTOTAL FAILS: %d" % fails)
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
