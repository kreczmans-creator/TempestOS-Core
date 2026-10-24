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
    # Outlets by energy balance on both streams, also for ISO (as a finite-C stream).
    out.update(NTU=ntu, eps=eps, Q=q / 1000.0, T_h_out=tho, T_c_out=tco)
    # Cold-stream ports added in product 1.1.0 (A8 cooling chain): rise, mean temperature, c_p as entered.
    out.update(dT_c=q / cc, T_c_m=(tci + tco) / 2.0, cp_c=cpc)
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
            try:
                out["F"] = f_one_shell(r, p)
            except (ValueError, ZeroDivisionError):
                out["F"] = 0.0  # P at its limit: F tends to 0
        else:  # crossflow: back-calculated
            out["F"] = q / (ua * dlm)
    out["dT_lm"] = dlm
    # Approach = smallest terminal temperature difference.
    ends = (thi - tci, tho - tco) if arr == PARALLEL else (thi - tco, tho - tci)
    out["dT_app"] = min(ends)
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
# Product values below were read from the C# calculator with the probe tool
# (tools/verification/probe, command "run", Release build of release/v0.26.0).
# Q is in kW, temperatures in degC, areas in m2, UA in W/K, dT in K.
EXTRAS = [
    ('E1 counter Cr=0.5 rating, Rf=2e-4', dict(arr=COUNTER, mode='rating', mh=1, cph=4180, thi=80, mc=2, cpc=4180, tci=20, U=1000, A=5, Rf=0.0002),
     {'NTU': 0.996810207, 'eps': 0.563735544, 'Q': 141.3848744, 'T_h_out': 46.1758674, 'T_c_out': 36.9120663, 'dT_app': 26.17586736, 'UA': 4166.666667, 'dT_lm': 33.93236986, 'F': 1.0}),
    ('E2 parallel Cr=0.25 sizing 100 kW', dict(arr=PARALLEL, mode='sizing', mh=1, cph=4180, thi=80, mc=4, cpc=4180, tci=20, U=1000, qreq=100),
     {'NTU': 0.551969972, 'eps': 0.398724083, 'Q': 100.0, 'T_h_out': 56.076555, 'T_c_out': 25.9808612, 'dT_app': 30.09569378, 'UA': 2307.234481, 'A_req': 2.307234481, 'dT_lm': 43.341932, 'F': 1.0}),
    ('E3 crossflow Cr=0.6 rating A=3', dict(arr=CROSS, mode='rating', mh=1, cph=4180, thi=80, mc=1.667, cpc=4180, tci=20, U=1000, A=3),
     {'NTU': 0.717703349, 'eps': 0.436985592, 'Q': 109.5959865, 'T_h_out': 53.7808645, 'T_c_out': 35.7283356, 'dT_app': 33.78086447, 'UA': 3000.0, 'dT_lm': 38.79011544, 'F': 0.941786202}),
    ('E4 crossflow Cr=0.6 sizing 120 kW', dict(arr=CROSS, mode='sizing', mh=1, cph=4180, thi=80, mc=1.667, cpc=4180, tci=20, U=1000, qreq=120),
     {'NTU': 0.835359292, 'eps': 0.478468899, 'Q': 120.0, 'T_h_out': 51.291866, 'T_c_out': 37.2214361, 'dT_app': 31.29186603, 'UA': 3491.801839, 'A_req': 3.491801839, 'dT_lm': 36.7363975, 'F': 0.935481258}),
    ('E5 shell 1-2 Cr=0.5 sizing 150 kW', dict(arr=SHELL, mode='sizing', mh=1, cph=4180, thi=80, mc=2, cpc=4180, tci=20, U=1000, qreq=150),
     {'NTU': 1.257679361, 'eps': 0.598086124, 'Q': 150.0, 'T_h_out': 44.1148325, 'T_c_out': 37.9425837, 'dT_app': 24.11483254, 'UA': 5257.099729, 'A_req': 5.257099729, 'dT_lm': 32.25872951, 'F': 0.884499892}),
    ('E6 shell 1-2 Cr=1 rating A=10', dict(arr=SHELL, mode='rating', mh=1, cph=4180, thi=80, mc=1, cpc=4180, tci=20, U=1000, A=10),
     {'NTU': 2.392344498, 'eps': 0.569221599, 'Q': 142.7607769, 'T_h_out': 45.8467041, 'T_c_out': 54.1532959, 'dT_app': 25.84670408, 'UA': 10000.0, 'dT_lm': 25.84670408, 'F': 0.552336486}),
    ('E7 isothermal, C_h=1e6 W/K, A=2', dict(arr=ISO, mode='rating', mh=10, cph=100000, thi=120, mc=1, cpc=4180, tci=20, U=1000, A=2),
     {'NTU': 0.478468899, 'eps': 0.380268463, 'Q': 158.9522175, 'T_h_out': 119.8410478, 'T_c_out': 58.0268463, 'dT_app': 61.9731537, 'UA': 2000.0, 'dT_lm': 79.40790963, 'F': 1.0}),
    ('E8 near-zero area A=0.01', dict(arr=COUNTER, mode='rating', mh=1, cph=4180, thi=80, mc=1, cpc=4180, tci=20, U=1000, A=0.01),
     {'NTU': 0.002392344, 'eps': 0.002386635, 'Q': 0.598568019, 'T_h_out': 79.8568019, 'T_c_out': 20.1431981, 'dT_app': 59.85680191, 'UA': 10.0, 'dT_lm': 59.85680191, 'F': 1.0}),
    ('E9 counter very large NTU (2392)', dict(arr=COUNTER, mode='rating', mh=1, cph=4180, thi=80, mc=1, cpc=4180, tci=20, U=10000, A=1000),
     {'NTU': 2392.344498, 'eps': 0.999582175, 'Q': 250.6952094, 'T_h_out': 20.0250695, 'T_c_out': 79.9749305, 'dT_app': 0.025069521, 'UA': 10000000.0, 'dT_lm': 0.025069521, 'F': 1.0}),
    ('E10 shell 1-2 very large NTU (2392)', dict(arr=SHELL, mode='rating', mh=1, cph=4180, thi=80, mc=1, cpc=4180, tci=20, U=10000, A=1000),
     {'NTU': 2392.344498, 'eps': 0.585786438, 'Q': 146.9152386, 'T_h_out': 44.8528137, 'T_c_out': 55.1471863, 'dT_app': 24.85281374, 'UA': 10000000.0, 'dT_lm': 24.85281374}),  # product F = 0.0558 is rounding noise at the eps limit; true F -> 0 (Info)
    ('E11 counter fouling Rf=1e-3 sizing 60 kW', dict(arr=COUNTER, mode='sizing', mh=1, cph=4180, thi=80, mc=2, cpc=4180, tci=20, U=1000, Rf=0.001, qreq=60),
     {'NTU': 0.292063111, 'eps': 0.23923445, 'Q': 60.0, 'T_h_out': 65.645933, 'T_c_out': 27.1770335, 'dT_app': 45.64593301, 'UA': 1220.823803, 'A_req': 2.441647606, 'dT_lm': 49.14714134, 'F': 1.0}),
    ('E12 counter sub-zero streams -30/-40 degC', dict(arr=COUNTER, mode='rating', mh=1, cph=4180, thi=-30, mc=1, cpc=4180, tci=-40, U=1000, A=2),
     {'NTU': 0.478468899, 'eps': 0.323624596, 'Q': 13.52750809, 'T_h_out': -33.236246, 'T_c_out': -36.763754, 'dT_app': 6.763754045, 'UA': 2000.0, 'dT_lm': 6.763754045, 'F': 1.0}),
    ('E13 counter Cr=0.9999 rating', dict(arr=COUNTER, mode='rating', mh=1, cph=4180, thi=80, mc=1, cpc=4180.4, tci=20, U=1000, A=4.18),
     {'NTU': 1.0, 'eps': 0.500011961, 'Q': 125.4029997, 'T_h_out': 49.9992824, 'T_c_out': 49.997847, 'dT_app': 29.99928236, 'UA': 4180.0, 'dT_lm': 30.00071764, 'F': 1.0}),
    ('E14 counter Cr=1 rating A=1 (Q_req 200 kW, duty check)', dict(arr=COUNTER, mode='rating', mh=1, cph=4180, thi=80, mc=1, cpc=4180, tci=20, U=1000, A=1),
     {'NTU': 0.23923445, 'eps': 0.193050193, 'Q': 48.41698842, 'T_h_out': 68.4169884, 'T_c_out': 31.5830116, 'dT_app': 48.41698842, 'UA': 1000.0, 'dT_lm': 48.41698842, 'F': 1.0}),
    ('E15 parallel pinch NTU 2392', dict(arr=PARALLEL, mode='rating', mh=1, cph=4180, thi=80, mc=1, cpc=4180, tci=20, U=10000, A=1000),
     {'NTU': 2392.344498, 'eps': 0.5, 'Q': 125.4, 'T_h_out': 50.0, 'T_c_out': 50.0, 'dT_app': 0.0, 'UA': 10000000.0}),
    ('P1 ports 1.1.0: counter Cr=0.5 rating, Rf=2e-4', dict(arr=COUNTER, mode='rating', mh=1, cph=4180, thi=80, mc=2, cpc=4180, tci=20, U=1000, A=5, Rf=0.0002),
     {'dT_c': 16.91206632, 'T_c_m': 28.4560332, 'cp_c': 4180.0}),
    ('P2 ports 1.1.0: parallel sizing 100 kW', dict(arr=PARALLEL, mode='sizing', mh=1, cph=4180, thi=80, mc=4, cpc=4180, tci=20, U=1000, qreq=100),
     {'dT_c': 5.980861244, 'T_c_m': 22.9904306, 'cp_c': 4180.0}),
    ('P3 ports 1.1.0: shell 1-2 sizing 150 kW', dict(arr=SHELL, mode='sizing', mh=1, cph=4180, thi=80, mc=2, cpc=4180, tci=20, U=1000, qreq=150),
     {'dT_c': 17.94258373, 'T_c_m': 28.9712919, 'cp_c': 4180.0}),
    ('P4 ports 1.1.0: crossflow Cr=0.6 rating', dict(arr=CROSS, mode='rating', mh=1, cph=4180, thi=80, mc=1.667, cpc=4180, tci=20, U=1000, A=3),
     {'dT_c': 15.72833565, 'T_c_m': 27.8641678, 'cp_c': 4180.0}),
    ('P5 ports 1.1.0: sub-zero streams', dict(arr=COUNTER, mode='rating', mh=1, cph=4180, thi=-30, mc=1, cpc=4180, tci=-40, U=1000, A=2),
     {'dT_c': 3.236245955, 'T_c_m': -38.381877, 'cp_c': 4180.0}),
    ('P6 ports 1.1.0: isothermal choice, mc=2', dict(arr=ISO, mode='rating', mh=1, cph=4180, thi=80, mc=2, cpc=4180, tci=20, U=1000, A=4.18),
     {'dT_c': 18.96361676, 'T_c_m': 29.4818084, 'cp_c': 4180.0}),
]


# Cases where the product blocks the run. Each entry: label, kwargs, expectation of
# the independent model ("blocked": eps_required above eps_max; "attainable": it is not).
BLOCKS = [
    ("B1 parallel sizing Cr=1, 150 kW", dict(arr=PARALLEL, cr=1.0, q=150, cmin=4180, dt=60), "blocked", "blocked", ""),
    ("B2 shell 1-2 sizing Cr=1, 147 kW", dict(arr=SHELL, cr=1.0, q=147, cmin=4180, dt=60), "blocked", "blocked", ""),
    ("B3 counter sizing Cr=1, 250.7 kW", dict(arr=COUNTER, cr=1.0, q=250.7, cmin=4180, dt=60), "attainable", "blocked",
     "FINDING P1-bs.heat-exchanger-02: product limit eps<0.999 (NTU 1000) blocks a duty that NTU 2392 reaches"),
]


def run_extras():
    fails = 0
    print()
    print("Extra checks (product values read with the probe tool)")
    print(f"{'probe':50s} {'symbol':7s} {'product':>13s} {'independent':>13s} {'rel.diff':>9s} result")
    for label, kw, prod in EXTRAS:
        ind = solve(**kw)
        for sym, pv in prod.items():
            if sym not in ind:
                continue
            iv = ind[sym]
            scale = max(abs(pv), abs(iv), 1e-12)
            # Temperatures near zero degC: use an absolute scale of 1 K for the relative figure.
            if sym in ("T_h_out", "T_c_out"):
                scale = max(scale, 1.0)
            rd = abs(pv - iv) / scale
            ok = rd <= 1e-6
            fails += (not ok)
            print(f"{label[:50]:50s} {sym:7s} {pv:13.7g} {iv:13.7g} {rd:9.2e} {'PASS' if ok else 'FAIL'}")
    print()
    for label, kw, expect, product, note in BLOCKS:
        emax = eps_max(kw["arr"], kw["cr"])
        eps_req = kw["q"] * 1000 / (kw["cmin"] * kw["dt"])
        got = "blocked" if eps_req >= emax else "attainable"
        flag = "PASS" if got == expect else "FAIL"
        fails += (flag == "FAIL")
        line = f"{label:40s} eps_req {eps_req:.5f} eps_max {emax:.5f} independent: {got}; product: {product}"
        print(line, flag, ("  " + note) if got != product else "")
    # Isothermal idealisation (finding 01): product treats C_r as 0 whatever the stream ratio.
    iso = solve(arr=ISO, mode="rating", mh=1, cph=4180, thi=80, mc=2, cpc=4180, tci=20, U=1000, A=4.18)
    cf = solve(arr=COUNTER, mode="rating", mh=1, cph=4180, thi=80, mc=2, cpc=4180, tci=20, U=1000, A=4.18)
    print(f"Isothermal choice with C_r(true)=0.5: Q {iso['Q']:.3f} kW (product 158.536) vs counterflow {cf['Q']:.3f} kW"
          f" (product 141.635): FINDING P1-bs.heat-exchanger-01 (no guard on the stream ratio)")
    return fails


def crossflow_exact(ntu, cr, n=300):
    """Both-unmixed crossflow effectiveness by marching the two energy equations on an n x n grid.

    Used only to size the error of the published correlation (Info finding 05). The scheme is
    second order (Crank-Nicolson per cell); the error at n = 300 is below 0.01 in eps percent.
    """
    nh, nc = ntu, ntu * cr
    dx = 1.0 / n
    a, b = nh * dx / 2.0, nc * dx / 2.0
    tc = [0.0] * n
    hot_out = 0.0
    for _ in range(n):
        h = 1.0
        for i in range(n):
            d0 = h - tc[i]
            d1 = d0 * (1.0 - a - b) / (1.0 + a + b)
            d = d0 + d1
            h -= a * d
            tc[i] += b * d
        hot_out += h
    return 1.0 - hot_out / n


def crossflow_report():
    print()
    print("Crossflow correlation against a numerical solution of the two energy equations (Info)")
    for ntu, cr in [(0.5, 1.0), (1.0, 1.0), (3.0, 0.5)]:
        ex = crossflow_exact(ntu, cr)
        co = eps_of(CROSS, ntu, cr)
        print(f"  NTU {ntu:3.1f}, C_r {cr:3.1f}: numerical {ex:.5f}, correlation {co:.5f}, difference {100 * (co / ex - 1):+.2f} %")


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
    crossflow_report()
    print("ALL PASS" if f == 0 else f"{f} FAILURES")
    sys.exit(0 if f == 0 else 1)
