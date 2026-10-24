#!/usr/bin/env python3
"""Independent re-derivation of mech.bolted-joint (Track P, P1).

Sources (cited by title and number only):
- VDI 2230 Part 1 (2015), steps R3, R5, R7, R9 and R10 for the model structure.
- Shigley's Mechanical Engineering Design, 9th ed., eq. 8-17 and 8-20, for stiffness.
- ISO 898-1 (2013) clause 9.1.6 for the tensile stress area.
Units: mm, N, MPa inside the model. Reported in kN and kN/m where the KATs use them.
"""
import math
import sys

# ----------------------------------------------------------------- model

def thread(d, P):
    d2 = d - 0.649519 * P
    d3 = d - 1.226869 * P
    ds = 0.5 * (d2 + d3)
    As = math.pi / 4 * ds ** 2
    Ad3 = math.pi / 4 * d3 ** 2
    return d2, d3, ds, As, Ad3


def frustum_k(E, d_num, d_h, d_w, t):
    """Stiffness of one hollow 30-degree frustum, N/mm. d_num is the diameter in the numerator."""
    ln = math.log(((1.155 * t + d_w - d_h) * (d_w + d_h)) / ((1.155 * t + d_w + d_h) * (d_w - d_h)))
    return 0.5774 * math.pi * E * d_num / ln


def joint(i, numerator="bolt"):
    """Return a dict of every output for inputs i (mm, N, MPa, N*mm units)."""
    d, P = i["d"], i["P"]
    E_S, E_P = i["E_S"] * 1e3, i["E_P"] * 1e3          # GPa -> MPa
    d2, d3, ds, As, Ad3 = thread(d, P)
    AN = math.pi / 4 * d ** 2
    o = {"A_s": As}
    Dkm = 0.5 * (i["d_w"] + i["d_h"])
    if i["method"] == "nut factor":
        FM = i["M_A"] * 1e3 / (i["K"] * d)
        MG = FM * (0.16 * P + 0.58 * d2 * i["mu_G"])
    else:
        lever = 0.16 * P + 0.58 * d2 * i["mu_G"] + 0.5 * Dkm * i["mu_K"]
        FM = i["M_A"] * 1e3 / lever
        MG = FM * (0.16 * P + 0.58 * d2 * i["mu_G"])
    FMmin = FM / i["alpha_A"]
    sigM = FM / As
    deltaS = 0.8 * d / (E_S * AN) + i["l_k"] / (E_S * As) + 0.5 * d / (E_S * Ad3)
    kS = 1 / deltaS
    t = i["l_k"] / 2
    dnum = d if numerator == "bolt" else i["d_h"]
    kf = frustum_k(E_P, dnum, i["d_h"], i["d_w"], t)
    kP = kf / 2
    Phi = i["n"] * kS / (kS + kP)
    FZ = i["f_Z"] * 1e-3 * kS * kP / (kS + kP)          # um -> mm, result N
    FA = i["F_A"] * 1e3
    FKR = FMmin - (1 - Phi) * FA - FZ
    FSmax = FM + Phi * FA
    sz = FSmax / As
    Wp = math.pi / 16 * ds ** 3
    tauM = MG / Wp
    sred = math.sqrt(sz ** 2 + 3 * (0.5 * tauM) ** 2)
    sa = Phi * FA / (2 * As)
    mmin = i["S_strip"] * FSmax / (0.5 * math.pi * d * i["tau_B"])
    o.update({
        "F_M": FM / 1e3, "F_M_min": FMmin / 1e3, "sigma_M": sigM, "nu_M": sigM / i["R_p02"],
        "k_S": kS, "k_P": kP * 1e3 / 1e3 * 1.0,  # N/mm == kN/m
        "Phi": Phi, "F_Z": FZ / 1e3, "F_SA": Phi * FA / 1e3, "F_KR": FKR / 1e3,
        "F_S_max": FSmax / 1e3, "sigma_red_B": sred, "sigma_a": sa, "m_min": mmin,
        "tau_M": tauM,
    })
    return o

BASE = dict(d=12, P=1.75, R_p02=640, E_S=205, sigma_A=50, l_k=40, d_h=13, d_w=18, E_P=205,
            n=0.5, f_Z=11, method="friction", M_A=80, K=0.2, mu_G=0.12, mu_K=0.12,
            alpha_A=1.6, F_A=10, F_KR_req=5, tau_B=400, m_eff=12, S_strip=1.25)

def case(**kw):
    c = dict(BASE); c.update(kw); return c

# ----------------------------------------------------------------- KATs

rows = []

def check(case_name, sym, expected, value, tol, kind="KAT"):
    tol_eff = max(tol, 1e-6)
    rel = abs(value - expected) / abs(expected) if expected != 0 else abs(value - expected)
    ok = rel <= tol_eff
    rows.append((kind, case_name, sym, expected, value, rel, tol, ok))

k1 = joint(case(method="nut factor", M_A=93))
check("ka-m12-88-nut-factor", "A_s", 84.27, k1["A_s"], 1e-3)
check("ka-m12-88-nut-factor", "F_M", 38.75, k1["F_M"], 1e-4)
check("ka-m12-88-nut-factor", "sigma_M", 459.8, k1["sigma_M"], 2e-3)
k2 = joint(case())
check("ka-shigley-stiffness", "k_P", 1.9078e6, k2["k_P"], 2e-3)
check("ka-shigley-stiffness", "k_S", 3.2119e5, k2["k_S"], 2e-3)
check("ka-shigley-stiffness", "Phi", 0.07205, k2["Phi"], 3e-3)
check("ka-shigley-stiffness", "F_M", 40.69, k2["F_M"], 2e-3)

# ----------------------------------------------------------------- extra checks
# Product values below were read from the Probe tool (C# calculator) and are hard-coded.
# EXTRAS: (name, inputs, {symbol: product value})
EXTRAS = []   # filled in after probing

for name, inp, prod in EXTRAS:
    ind = joint(inp)
    for sym, pv in prod.items():
        check(name, sym, pv, ind[sym], 1e-3, kind="EXTRA")

# ----------------------------------------------------------------- report

print(f"{'kind':5} {'case':26} {'symbol':12} {'expected':>13} {'independent':>13} {'rel.diff':>10} {'tol':>8}  result")
bad = 0
for kind, c, s, e, v, r, t, ok in rows:
    bad += (not ok)
    print(f"{kind:5} {c:26} {s:12} {e:13.6g} {v:13.6g} {r:10.2e} {t:8.0e}  {'PASS' if ok else 'FAIL'}")
print(f"{len(rows) - bad}/{len(rows)} rows pass")
sys.exit(1 if bad else 0)
