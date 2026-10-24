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



# ---- Extra checks: product values observed with tools/verification/probe (Release build, v0.26.0) ----
# Each entry: probe name, actions (name, category, F_k), factor overrides, product outputs (all in kN-type units).
# L7 uses a user psi table (wind 0.5/0.2/0, office 0.7/0.5/0.3). L4 uses non-default factors and K_FI = 1.1.
PRODUCT = [
    ('L1_uk_office', [('Slab', 'G permanent', 18), ('Office', 'B office', 10)],
     {'method': '6.10ab', 'xi': 0.925},
     {'E_d_max': 37.4775, 'E_d_min': 18.0, 'E_k_max': 28.0, 'E_k_min': 18.0, 'E_fr_max': 23.0, 'E_fr_min': 18.0, 'E_qp_max': 21.0, 'E_qp_min': 18.0}),
    ('L2_mixed', [('G1', 'G permanent', 12), ('G2 opposing', 'G permanent', -3), ('Imposed', 'B office', 6), ('Wind suction', 'wind', -8), ('Snow', 'snow <= 1000 m', 4)],
     {},
     {'E_d_max': 25.5, 'E_d_min': -4.05, 'E_k_max': 17.2, 'E_k_min': 1.0, 'E_fr_max': 12.0, 'E_fr_min': 7.4, 'E_qp_max': 10.8, 'E_qp_min': 9.0}),
    ('L3_accidental', [('G', 'G permanent', 10), ('Imposed', 'B office', 5), ('Wind', 'wind', 4), ('Impact', 'accidental', 30)],
     {},
     {'E_d_max': 24.75, 'E_d_min': 10.0, 'E_k_max': 17.5, 'E_k_min': 10.0, 'E_fr_max': 12.5, 'E_fr_min': 10.0, 'E_qp_max': 11.5, 'E_qp_min': 10.0, 'E_acc_max': 42.5, 'E_acc_min': 10.0}),
    ('L4_kfi_equ', [('Roof G', 'G permanent', 10), ('Wind uplift', 'wind', -20), ('Roof imposed', 'H roofs', 3)],
     {'gsup': 1.1, 'ginf': 0.9, 'kfi': 1.1},
     {'E_d_max': 15.95, 'E_d_min': -24.0, 'E_k_max': 13.0, 'E_k_min': -10.0, 'E_fr_max': 10.0, 'E_fr_min': 6.0, 'E_qp_max': 10.0, 'E_qp_min': 10.0}),
    ('L5_no_perm', [('Wind', 'wind', 5), ('Thermal', 'thermal', -2)],
     {},
     {'E_d_max': 7.5, 'E_d_min': -3.0, 'E_k_max': 5.0, 'E_k_min': -2.0, 'E_fr_max': 1.0, 'E_fr_min': -1.0, 'E_qp_max': 0.0, 'E_qp_min': 0.0}),
    ('L6_storage_ab', [('G', 'G permanent', 25), ('Storage', 'E storage', 40), ('Snow>1000', 'snow > 1000 m', 6), ('Wind', 'wind', 9)],
     {'method': '6.10ab'},
     {'E_d_max': 108.4875, 'E_d_min': 25.0, 'E_k_max': 78.2, 'E_k_min': 25.0, 'E_fr_max': 62.2, 'E_fr_min': 25.0, 'E_qp_max': 58.2, 'E_qp_min': 25.0}),
    ('L7_psi_user', [('G', 'G permanent', 10), ('Wind', 'wind', 8), ('Imposed', 'B office', 6)],
     {'psi_user': {'wind': (0.5, 0.2, 0.0), 'B office': (0.7, 0.5, 0.3)}},
     {'E_d_max': 31.8, 'E_d_min': 10.0, 'E_k_max': 22.2, 'E_k_min': 10.0, 'E_fr_max': 13.4, 'E_fr_min': 10.0, 'E_qp_max': 11.8, 'E_qp_min': 10.0}),
    ('L9_large', [('G', 'G permanent', 1000000.0), ('Q', 'A domestic', 4000000.0), ('Q2', 'D shopping', -3000000.0), ('Wind', 'wind', 2000000.0)],
     {'method': '6.10ab'},
     {'E_d_max': 8947500.0, 'E_d_min': -3500000.0, 'E_k_max': 6200000.0, 'E_k_min': -2000000.0, 'E_fr_max': 3000000.0, 'E_fr_min': -1100000.0, 'E_qp_max': 2200000.0, 'E_qp_min': -800000.0}),
    ('L10_only_neg_perm', [('G', 'G permanent', -10), ('Imposed', 'B office', 5)],
     {},
     {'E_d_max': -2.5, 'E_d_min': -13.5, 'E_k_max': -5.0, 'E_k_min': -10.0, 'E_fr_max': -7.5, 'E_fr_min': -10.0, 'E_qp_max': -8.5, 'E_qp_min': -10.0}),
    ('L11_acc_only', [('A', 'accidental', 20), ('G', 'G permanent', 10)],
     {},
     {'E_acc_max': 30.0, 'E_acc_min': 10.0}),
    ('L12_acc_two', [('A', 'accidental', -20), ('A2', 'accidental', 15), ('G', 'G permanent', 10), ('Q', 'B office', 5)],
     {},
     {'E_acc_max': 27.5, 'E_acc_min': -10.0}),
]


def extra_checks():
    fails = 0
    print("\nEXTRA CHECKS (product observed vs independent)")
    print("%-16s %-10s %12s %12s %10s  %s" % ("probe", "symbol", "product", "independent", "rel.diff", "result"))
    for name, acts, kw, prod in PRODUCT:
        kw = dict(kw)
        user = kw.pop("psi_user", None)
        if user:
            kw["psi"] = user
        ind = E(acts, **kw)
        for sym, pv in prod.items():
            iv = ind[sym]
            rd = abs(pv - iv) / max(abs(iv), 1e-9)
            ok = rd <= 1e-6
            fails += not ok
            print("%-16s %-10s %12.6g %12.6g %10.2e  %s" % (name, sym, pv, iv, rd, "PASS" if ok else "FAIL"))
    return fails


def uk_na_comparison():
    """Information only: recommended defaults against the UK NA values (see findings 01 and 02).
    UK NA to BS EN 1990 NA.A1.2(B): xi = 0.925 (recommended 0.85). Worked example: G 18, Q (office) 10 kN/m, 6.10b."""
    acts = [("Slab", PERM, 18), ("Office", "B office", 10)]
    rec = E(acts, method="6.10ab")["E_d_max"]
    uk = E(acts, method="6.10ab", xi=0.925)["E_d_max"]
    print("\nINFO default xi = 0.85 against UK NA xi = 0.925 (G 18, office 10 kN/m, 6.10a/b)")
    print("  E_d_max with the template default xi: %.4f kN/m; with UK NA xi: %.4f kN/m; default is %.1f %% lower"
          % (rec, uk, 100 * (uk - rec) / uk))
    # Roof imposed (category H) as the accompanying action under a wind-leading case.
    acts2 = [("Roof G", PERM, 10), ("Wind", "wind", 6), ("Roof imposed", "H roofs", 4)]
    rec_h = E(acts2)["E_d_max"]
    uk_psi = dict(PSI)
    uk_psi["H roofs"] = (0.7, 0.0, 0.0)  # UK NA Table NA.A1.1 value for category H, as I recall it; owner to confirm
    uk_psi["wind"] = (0.5, 0.2, 0.0)     # UK NA Table NA.A1.1 wind value, as I recall it; owner to confirm
    uk_h = E(acts2, psi=uk_psi)["E_d_max"]
    print("INFO recommended psi_0 (roof H 0, wind 0.6) against UK NA psi_0 (roof H 0.7, wind 0.5), as I recall the NA")
    print("  G 10, wind 6, roof imposed 4 kN: recommended %.4f kN; UK NA %.4f kN" % (rec_h, uk_h))


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
    fails += extra_checks()
    uk_na_comparison()
    print("\nTOTAL FAILS: %d" % fails)
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
