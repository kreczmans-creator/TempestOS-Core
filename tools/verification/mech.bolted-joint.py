#!/usr/bin/env python3
"""Independent re-derivation of mech.bolted-joint (Track P, P1).

Sources, cited by title and number only:
- VDI 2230 Part 1 (2015): stiffness, load factor, embedding, residual clamp, equivalent stress.
- Shigley's Mechanical Engineering Design, 9th ed., eq. 8-17 and 8-20: bolt and frustum stiffness.
- ISO 898-1 (2013) clause 9.1.6: tensile stress area.
Units inside the model: mm, N, MPa. Outputs are reported in kN, kN/m (= N/mm) and MPa.

Exit code: 0 when every KAT row passes (tolerance max(KAT tol, 1e-6)), else 1.
EXTRA rows compare the product values observed with the Probe tool. They do not set the exit code.
"""
import math
import sys


def thread(d, P):
    d2 = d - 0.649519 * P
    d3 = d - 1.226869 * P
    ds = 0.5 * (d2 + d3)
    return d2, d3, ds, math.pi / 4 * ds ** 2, math.pi / 4 * d3 ** 2


def frustum_k(E, d_num, d_h, d_w, t):
    """Stiffness of one hollow 30-degree frustum in N/mm. d_num is the diameter in the numerator."""
    ln = math.log(((1.155 * t + d_w - d_h) * (d_w + d_h)) / ((1.155 * t + d_w + d_h) * (d_w - d_h)))
    return 0.5774 * math.pi * E * d_num / ln


def joint(i, wp="ds", numerator="bolt"):
    """All outputs for inputs i. wp: polar modulus basis (ds = VDI mean, d3 = minor). numerator: bolt or hole."""
    d, P = i["d"], i["P"]
    E_S, E_P = i["E_S"] * 1e3, i["E_P"] * 1e3
    d2, d3, ds, As, Ad3 = thread(d, P)
    AN = math.pi / 4 * d ** 2
    Dkm = 0.5 * (i["d_w"] + i["d_h"])
    thread_lever = 0.16 * P + 0.58 * d2 * i["mu_G"]
    if i["method"] == "nut factor":
        FM = i["M_A"] * 1e3 / (i["K"] * d)
    else:
        FM = i["M_A"] * 1e3 / (thread_lever + 0.5 * Dkm * i["mu_K"])
    FMmin = FM / i["alpha_A"]
    sigM = FM / As
    MG = FM * thread_lever
    Wp = math.pi / 16 * (ds if wp == "ds" else d3) ** 3
    tauM = MG / Wp
    nuM = math.sqrt(sigM ** 2 + 3 * tauM ** 2) / i["R_p02"]
    deltaS = 0.8 * d / (E_S * AN) + i["l_k"] / (E_S * As) + 0.5 * d / (E_S * Ad3)
    kS = 1 / deltaS
    t = i["l_k"] / 2
    kP = 0.5 * frustum_k(E_P, d if numerator == "bolt" else i["d_h"], i["d_h"], i["d_w"], t)
    Phi = i["n"] * kS / (kS + kP)
    FZ = i["f_Z"] * 1e-3 * kS * kP / (kS + kP)
    FA = i["F_A"] * 1e3
    FKR = FMmin - (1 - Phi) * FA - FZ
    FSmax = FM + Phi * FA
    sred = math.sqrt((FSmax / As) ** 2 + 3 * (0.5 * tauM) ** 2)
    return {
        "A_s": As, "F_M": FM / 1e3, "F_M_min": FMmin / 1e3, "sigma_M": sigM, "nu_M": nuM,
        "k_S": kS, "k_P": kP, "Phi": Phi, "F_Z": FZ / 1e3, "F_SA": Phi * FA / 1e3,
        "F_KR": FKR / 1e3, "F_S_max": FSmax / 1e3, "sigma_red_B": sred,
        "sigma_a": Phi * FA / (2 * As),
        "m_min": i["S_strip"] * FSmax / (0.5 * math.pi * d * i["tau_B"]),
    }


BASE = dict(d=12, P=1.75, R_p02=640, E_S=205, sigma_A=50, l_k=40, d_h=13, d_w=18, E_P=205,
            n=0.5, f_Z=11, method="friction", M_A=80, K=0.2, mu_G=0.12, mu_K=0.12,
            alpha_A=1.6, F_A=10, F_KR_req=5, tau_B=400, m_eff=12, S_strip=1.25)


def case(**kw):
    c = dict(BASE)
    c.update(kw)
    return c


rows = []   # (kind, case, symbol, expected, value, rel, tol, ok)


def check(kind, name, sym, expected, value, tol):
    rel = abs(value - expected) / abs(expected) if expected != 0 else abs(value - expected)
    rows.append((kind, name, sym, expected, value, rel, tol, rel <= max(tol, 1e-6)))


# ---------------------------------------------------------------- KATs (exit code)
k1 = joint(case(method="nut factor", M_A=93))
for sym, exp, tol in [("A_s", 84.27, 1e-3), ("F_M", 38.75, 1e-4), ("sigma_M", 459.8, 2e-3)]:
    check("KAT", "ka-m12-88-nut-factor", sym, exp, k1[sym], tol)
k2 = joint(case())
for sym, exp, tol in [("k_P", 1.9078e6, 2e-3), ("k_S", 3.2119e5, 2e-3), ("Phi", 0.07205, 3e-3), ("F_M", 40.69, 2e-3)]:
    check("KAT", "ka-shigley-stiffness", sym, exp, k2[sym], tol)
n_kat = len(rows)

# ---------------------------------------------------------------- extra checks
# Product values below come from the Probe tool (C# calculator). Inputs are overrides of BASE.
EXTRAS = [
    ('p1-nutfactor-M16', {'method': 'nut factor', 'd': 16, 'P': 2, 'd_h': 17.5, 'd_w': 24, 'l_k': 50, 'M_A': 150, 'F_A': 20},
     {'A_s': 156.668144, 'F_M': 46.875, 'F_M_min': 29.296875, 'sigma_M': 299.199305, 'nu_M': 0.583473424, 'k_S': 467697.903, 'k_P': 2551233.18, 'Phi': 0.0774608447, 'F_Z': 4.34765489, 'F_SA': 1.54921689, 'F_KR': 6.498437, 'F_S_max': 48.4242169, 'sigma_red_B': 328.658506, 'sigma_a': 4.9442626, 'm_min': 6.02105741}),
    ('p2-n-is-1', {'n': 1.0},
     {'A_s': 84.2663675, 'F_M': 40.6898786, 'F_M_min': 25.4311741, 'sigma_M': 482.872109, 'nu_M': 0.968656979, 'k_S': 321185.752, 'k_P': 1907819.25, 'Phi': 0.144093778, 'F_Z': 3.02395372, 'F_SA': 1.44093778, 'F_KR': 13.8481582, 'F_S_max': 42.1308164, 'sigma_red_B': 536.434603, 'sigma_a': 8.54989852, 'm_min': 6.98471633}),
    ('p3-n-is-0', {'n': 0.0},
     {'A_s': 84.2663675, 'F_M': 40.6898786, 'F_M_min': 25.4311741, 'sigma_M': 482.872109, 'nu_M': 0.968656979, 'k_S': 321185.752, 'k_P': 1907819.25, 'Phi': 0.0, 'F_Z': 3.02395372, 'F_SA': 0.0, 'F_KR': 12.4072204, 'F_S_max': 40.6898786, 'sigma_red_B': 520.534005, 'sigma_a': 0.0, 'm_min': 6.74582845}),
    ('p4-separation-FA100', {'F_A': 100},
     {'A_s': 84.2663675, 'F_M': 40.6898786, 'F_M_min': 25.4311741, 'sigma_M': 482.872109, 'nu_M': 0.968656979, 'k_S': 321185.752, 'k_P': 1907819.25, 'Phi': 0.0720468891, 'F_Z': 3.02395372, 'F_SA': 7.20468891, 'F_KR': -70.3880907, 'F_S_max': 47.8945675, 'sigma_red_B': 600.696161, 'sigma_a': 42.7494926, 'm_min': 7.94026788}),
    ('p5-alpha1-fz0', {'alpha_A': 1.0, 'f_Z': 0},
     {'A_s': 84.2663675, 'F_M': 40.6898786, 'F_M_min': 40.6898786, 'sigma_M': 482.872109, 'nu_M': 0.968656979, 'k_S': 321185.752, 'k_P': 1907819.25, 'Phi': 0.0720468891, 'F_Z': 0.0, 'F_SA': 0.720468891, 'F_KR': 31.4103475, 'F_S_max': 41.4103475, 'sigma_red_B': 528.474944, 'sigma_a': 4.27494926, 'm_min': 6.86527239}),
    ('p6-M64-large', {'d': 64, 'P': 6, 'd_h': 66, 'd_w': 100, 'l_k': 300, 'M_A': 8000, 'F_A': 500, 'R_p02': 900, 'm_eff': 64},
     {'A_s': 2675.96958, 'F_M': 790.266384, 'F_M_min': 493.91649, 'sigma_M': 295.319644, 'nu_M': 0.394645654, 'k_S': 1456738.67, 'k_P': 10888888.3, 'Phi': 0.058998165, 'F_Z': 14.1333374, 'F_SA': 29.4990825, 'F_KR': 9.28223495, 'F_S_max': 819.765466, 'sigma_red_B': 321.840367, 'sigma_a': 5.51184937, 'm_min': 25.4823684}),
    ('p7-FA0-M0.1', {'F_A': 0, 'M_A': 0.1},
     {'A_s': 84.2663675, 'F_M': 0.0508623482, 'F_M_min': 0.0317889676, 'sigma_M': 0.603590136, 'nu_M': 0.00121082122, 'k_S': 321185.752, 'k_P': 1907819.25, 'Phi': 0.0720468891, 'F_Z': 3.02395372, 'F_SA': 0.0, 'F_KR': -2.99216475, 'F_S_max': 0.0508623482, 'sigma_red_B': 0.650667506, 'sigma_a': 0.0, 'm_min': 0.00843228556}),
    ('p8-dh-12.01', {'d_h': 12.01},
     {'A_s': 84.2663675, 'F_M': 41.3139723, 'F_M_min': 25.8212327, 'sigma_M': 490.27831, 'nu_M': 0.983514058, 'k_S': 321185.752, 'k_P': 2210237.78, 'Phi': 0.0634397499, 'F_Z': 3.08477251, 'F_SA': 0.634397499, 'F_KR': 13.3708577, 'F_S_max': 41.9483698, 'sigma_red_B': 535.509013, 'sigma_a': 3.76423903, 'm_min': 6.95446917}),
    ('p9-lk1-dw200', {'l_k': 1, 'd_w': 200},
     {'A_s': 84.2663675, 'F_M': 10.772828, 'F_M_min': 6.7330175, 'sigma_M': 127.842558, 'nu_M': 0.256456283, 'k_S': 1168478.36, 'k_P': 5935880590.0, 'Phi': 9.84056514e-05, 'F_Z': 12.8507323, 'F_SA': 0.000984056514, 'F_KR': -16.1167308, 'F_S_max': 10.7738121, 'sigma_red_B': 137.824547, 'sigma_a': 0.00583896365, 'm_min': 1.78615151}),
    ('p10-soft-members', {'E_P': 70, 'l_k': 25},
     {'A_s': 84.2663675, 'F_M': 40.6898786, 'F_M_min': 25.4311741, 'sigma_M': 482.872109, 'nu_M': 0.968656979, 'k_S': 445407.216, 'k_P': 781063.223, 'Phi': 0.181580902, 'F_Z': 3.12017561, 'F_SA': 1.81580902, 'F_KR': 14.1268075, 'F_S_max': 42.5056876, 'sigma_red_B': 540.583269, 'sigma_a': 10.7742215, 'm_min': 7.04686489}),
    ('p11-loose-hole', {'d_h': 15, 'd_w': 18.5},
     {'A_s': 84.2663675, 'F_M': 39.1947267, 'F_M_min': 24.4967042, 'sigma_M': 465.128946, 'nu_M': 0.933063624, 'k_S': 321185.752, 'k_P': 1483798.69, 'Phi': 0.0889718894, 'F_Z': 2.9043602, 'F_SA': 0.889718894, 'F_KR': 12.4820629, 'F_S_max': 40.0844456, 'sigma_red_B': 511.216645, 'sigma_a': 5.27920522, 'm_min': 6.64545589}),
]


for name, over, prod in EXTRAS:
    ind = joint(case(**over))
    ind_d3 = joint(case(**over), wp="d3")
    for sym, pv in prod.items():
        # Product value is the "expected" column; independent value is compared against it.
        ref = ind_d3 if sym in ("nu_M", "sigma_red_B") else ind
        check("EXTRA", name, sym, pv, ref[sym], 1e-3)
    # Show the effect of the polar-modulus basis separately (finding P1-mech.bolted-joint-01).
    for sym in ("nu_M", "sigma_red_B"):
        check("EXTRA", name, sym + "[ds]", prod[sym], ind[sym], 1e-3)

# Physical lower bound once the members separate: the bolt then carries the whole working load.
# Columns: expected = the bound (F_A = 100 kN), independent column = the product F_S_max.
sep = [e for e in EXTRAS if e[0] == "p4-separation-FA100"][0]
rows.append(("BOUND", sep[0], "F_S_max>=F_A", 100.0, sep[2]["F_S_max"], 0.0, 0.0, sep[2]["F_S_max"] >= 100.0))

# Sensitivity: frustum numerator uses the hole diameter in the exact integration.
for name, over in [("default", {}), ("loose-hole", dict(d_h=15, d_w=18.5))]:
    a = joint(case(**over))
    b = joint(case(**over), numerator="hole")
    print(f"note  {name}: k_P bolt-d {a['k_P']:.5g}, hole-d {b['k_P']:.5g} kN/m; "
          f"F_KR {a['F_KR']:.4f} -> {b['F_KR']:.4f} kN; Phi {a['Phi']:.5f} -> {b['Phi']:.5f}")

# ---------------------------------------------------------------- report
print(f"{'kind':6} {'case':22} {'symbol':14} {'expected':>13} {'independent':>13} {'rel.diff':>10} {'tol':>7}  result")
for kind, c, s, e, v, r, t, ok in rows:
    print(f"{kind:6} {c:22} {s:14} {e:13.6g} {v:13.6g} {r:10.2e} {t:7.0e}  {'PASS' if ok else 'FAIL'}")
kat_bad = sum(1 for r in rows[:n_kat] if not r[7])
ex_bad = sum(1 for r in rows[n_kat:] if not r[7])
print(f"KAT rows pass: {n_kat - kat_bad}/{n_kat}; extra rows pass: {len(rows) - n_kat - ex_bad}/{len(rows) - n_kat}")
sys.exit(1 if kat_bad else 0)
