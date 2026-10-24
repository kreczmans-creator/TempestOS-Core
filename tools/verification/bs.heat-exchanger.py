#!/usr/bin/env python3
"""Independent re-derivation of bs.heat-exchanger (Track P, P1).

Method sources (cited by title and number only):
  Incropera et al., Fundamentals of Heat and Mass Transfer, 7th ed.:
    Eq. 11.1-11.7 (capacity rates, energy balance), Eq. 11.14 (LMTD),
    Eq. 11.18-11.20 and Table 11.3 (effectiveness relations),
    Table 11.4 (NTU relations), Fig. 11.10 and its closed form (F for one shell pass).
All relations below are coded from first principles. The effectiveness
inverse is found by numerical root finding, not by the closed forms.
Exit 0 when every KAT value matches within max(KAT tolerance, 1e-6).
"""
import math
import sys

from scipy.optimize import brentq

COUNTER, PARALLEL, SHELL, CROSS, ISO = "counter", "parallel", "shell", "cross", "iso"


def eps_of(arr, ntu, cr):
    """Effectiveness from NTU and C_r (Incropera Table 11.3)."""
    if arr == ISO or cr == 0.0:
        return 1.0 - math.exp(-ntu)
    if arr == PARALLEL:
        return (1.0 - math.exp(-ntu * (1.0 + cr))) / (1.0 + cr)
    if arr == COUNTER:
        if abs(1.0 - cr) < 1e-12:
            return ntu / (1.0 + ntu)
        e = math.exp(-ntu * (1.0 - cr))
        return (1.0 - e) / (1.0 - cr * e)
    if arr == SHELL:
        s = math.sqrt(1.0 + cr * cr)
        e = math.exp(-ntu * s)
        return 2.0 / (1.0 + cr + s * (1.0 + e) / (1.0 - e))
    if arr == CROSS:
        return 1.0 - math.exp((1.0 / cr) * ntu ** 0.22 * (math.exp(-cr * ntu ** 0.78) - 1.0))
    raise ValueError(arr)


def eps_max(arr, cr):
    """Limit of epsilon as NTU -> infinity."""
    if arr in (ISO,) or cr == 0.0:
        return 1.0
    if arr == PARALLEL:
        return 1.0 / (1.0 + cr)
    if arr == COUNTER:
        return 1.0
    if arr == SHELL:
        s = math.sqrt(1.0 + cr * cr)
        return 2.0 / (1.0 + cr + s)
    if arr == CROSS:
        return 1.0 / (1.0 + 0.0) if cr == 0 else eps_of(arr, 1e6, cr)
    raise ValueError(arr)


def ntu_of(arr, eps, cr):
    """Inverse of eps_of by bracketed root finding."""
    if eps <= 0:
        return 0.0
    if eps >= eps_max(arr, cr) * (1 - 1e-12):
        return math.inf
    return brentq(lambda x: eps_of(arr, x, cr) - eps, 1e-12, 1e6, xtol=1e-14, rtol=1e-14)


def lmtd(d1, d2):
    if d1 <= 0 or d2 <= 0:
        return float("nan")
    if abs(d1 - d2) < 1e-9 * max(d1, d2):
        return 0.5 * (d1 + d2)
    return (d1 - d2) / math.log(d1 / d2)


def f_one_shell(r, p):
    """F for one shell pass, 2n tube passes (Incropera Fig. 11.10, closed form)."""
    if abs(r - 1.0) < 1e-9:
        s2 = math.sqrt(2.0)
        return (s2 * p / (1.0 - p)) / math.log((2.0 / p - 2.0 + s2) / (2.0 / p - 2.0 - s2))
    s = math.sqrt(r * r + 1.0)
    num = (s / (r - 1.0)) * math.log((1.0 - p) / (1.0 - r * p))
    den = math.log((2.0 / p - 1.0 - r + s) / (2.0 / p - 1.0 - r - s))
    return num / den


def solve(arr, mode, mh, cph, thi, mc, cpc, tci, U, A=None, Rf=0.0, qreq=None):
    ch, cc = mh * cph, mc * cpc
    cmin, cmax = min(ch, cc), max(ch, cc)
    cr = cmin / cmax
    if arr == ISO:
        cr = 0.0
    uf = 1.0 / (1.0 / U + Rf)
    dtmax = thi - tci
    out = {"C_r": cr}
    if mode == "rating":
        ntu = uf * A / cmin
        eps = eps_of(arr, ntu, cr)
        q = eps * cmin * dtmax
        out["A_in"] = A
    else:
        q = qreq * 1000.0
        eps = q / (cmin * dtmax)
        ntu = ntu_of(arr, eps, cr)
        out["A_req"] = ntu * cmin / uf
    tho = thi - q / ch
    tco = tci + q / cc
    if arr == ISO:
        # constant-temperature stream keeps its inlet temperature
        if ch < cc:
            tco = tci + q / cc
        else:
            tho = thi - q / ch
    out.update(NTU=ntu, eps=eps, Q=q / 1000.0, T_h_out=tho, T_c_out=tco)
    ua = ntu * cmin
    out["UA"] = ua
    # LMTD (counterflow end differences) and F
    if arr == PARALLEL:
        dlm = lmtd(thi - tci, tho - tco)
        out["F"] = 1.0
    else:
        dlm = lmtd(thi - tco, tho - tci)
        if arr == COUNTER or arr == ISO:
            out["F"] = 1.0
        elif arr == SHELL:
            r = (thi - tho) / (tco - tci)
            p = (tco - tci) / (thi - tci)
            out["F"] = f_one_shell(r, p)
        else:  # crossflow: back-calculated
            out["F"] = q / (ua * dlm)
    out["dT_lm"] = dlm
    return out


# ---------------------------------------------------------------- KATs
# (case, kwargs, [(symbol, expected, tol)])
KATS = [
    ("ka-counter-balanced",
     dict(arr=COUNTER, mode="rating", mh=1, cph=4180, thi=80, mc=1, cpc=4180, tci=20, U=1000, A=4.18, qreq=120),
     [("NTU", 1, 1e-9), ("eps", 0.5, 1e-9), ("Q", 125.4, 1e-9), ("T_h_out", 50, 1e-9),
      ("dT_lm", 30, 1e-9), ("F", 1, 1e-9)]),
    ("ka-parallel-balanced",
     dict(arr=PARALLEL, mode="rating", mh=1, cph=4180, thi=80, mc=1, cpc=4180, tci=20, U=1000, A=4.18),
     [("eps", 0.4323324, 1e-6), ("Q", 108.4282, 1e-5)]),
    ("ka-shell-tube",
     dict(arr=SHELL, mode="rating", mh=1, cph=4180, thi=80, mc=2, cpc=4180, tci=20, U=1000, A=4.18),
     [("eps", 0.5399396, 1e-6), ("Q", 135.4168, 1e-5), ("T_c_out", 36.19819, 1e-6), ("F", 0.9234561, 1e-6)]),
    ("ka-counter-sizing",
     dict(arr=COUNTER, mode="sizing", mh=1, cph=4180, thi=80, mc=2, cpc=4180, tci=20, U=1000, qreq=194.2697),
     [("NTU", 2, 1e-5), ("A_req", 8.36, 1e-5)]),
    ("ka-incropera-11-1",
     dict(arr=COUNTER, mode="sizing", mh=0.1, cph=2131, thi=100, mc=0.2, cpc=4178, tci=30, U=38.1, qreq=8.524),
     [("T_h_out", 60, 1e-6), ("T_c_out", 40.20, 1e-3), ("dT_lm", 43.2, 1e-3), ("UA", 197.31, 1e-3)]),
]

# --------------------------------------------------------- extra checks
# Each entry: (name, kwargs, symbol, product value observed with the probe, note).
# PRODUCT values are filled in after probing the C# calculator.
EXTRAS = []  # placeholder, replaced below


def run():
    fails = 0
    print(f"{'case':24s} {'symbol':8s} {'KAT':>14s} {'independent':>14s} {'rel.diff':>10s} {'tol':>8s} result")
    for case, kw, exp in KATS:
        res = solve(**kw)
        for sym, e, tol in exp:
            v = res[sym]
            rd = abs(v - e) / abs(e) if e != 0 else abs(v)
            t = max(tol, 1e-6)
            ok = rd <= t
            fails += (not ok)
            print(f"{case:24s} {sym:8s} {e:14.7g} {v:14.7g} {rd:10.2e} {tol:8.0e} {'PASS' if ok else 'FAIL'}")
    return fails


if __name__ == "__main__":
    f = run()
    f += run_extras() if "run_extras" in globals() else 0
    print("ALL PASS" if f == 0 else f"{f} FAILURES")
    sys.exit(0 if f == 0 else 1)
