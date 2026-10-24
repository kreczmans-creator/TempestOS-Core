#!/usr/bin/env python3
"""Independent re-derivation of struct.beam-formulas (Track P, P1).

Two independent routes per case:
  (a) closed forms derived here from the Euler-Bernoulli equation (own algebra, Roark Table 8.1 as cross-check);
  (b) a Hermite finite-element beam with 600 elements that needs no closed form.
Both must agree before a value is used as the independent answer.
Conventions: left end at x = 0, loads and deflection downward positive, sagging moment positive.
Units inside: kN, m, kN*m, mm for deflection.  Exit 0 when every row passes max(KAT tol, 1e-6).
"""
import math
import sys

import numpy as np

S3 = math.sqrt(3.0)


# ------------------------------------------------------------ route (a): closed forms
def closed(case, L, EI, w=0.0, P=0.0, M0=0.0, a=0.0):
    """Return dict with R_A, R_B, M_max, x_M, M_min, V_max, delta_max, x_delta (kN, m, kN*m, m)."""
    r = {}
    if case == "ss_udl":
        r = dict(R_A=w * L / 2, R_B=w * L / 2, M_max=w * L ** 2 / 8, x_M=L / 2, M_min=0.0, V_max=w * L / 2,
                 delta_max=5 * w * L ** 4 / (384 * EI), x_delta=L / 2)
    elif case == "ss_point":
        b = L - a
        c = min(a, b)
        dl = P * c * (L * L - c * c) ** 1.5 / (9 * S3 * L * EI)
        xd = math.sqrt((L * L - c * c) / 3)
        if a < b:                       # load nearer the left end: mirror
            xd = L - xd
        r = dict(R_A=P * b / L, R_B=P * a / L, M_max=P * a * b / L, x_M=a, M_min=0.0, V_max=max(P * a / L, P * b / L),
                 delta_max=dl, x_delta=xd)
    elif case == "ss_couple_left_cw":   # clockwise couple M0 applied at the left pin
        xd = L * (1 - 1 / S3)
        r = dict(R_A=-M0 / L, R_B=M0 / L, M_max=M0, x_M=0.0, M_min=0.0, V_max=M0 / L,
                 delta_max=M0 * L ** 2 / (9 * S3 * EI), x_delta=xd)
    elif case == "cant_udl":
        r = dict(R_A=w * L, R_B=0.0, M_max=0.0, x_M=L, M_min=-w * L ** 2 / 2, V_max=w * L,
                 delta_max=w * L ** 4 / (8 * EI), x_delta=L)
    elif case == "cant_point":
        r = dict(R_A=P, R_B=0.0, M_max=0.0, x_M=L, M_min=-P * a, V_max=P,
                 delta_max=P * a * a * (3 * L - a) / (6 * EI), x_delta=L)
    elif case == "fixed_udl":
        r = dict(R_A=w * L / 2, R_B=w * L / 2, M_max=w * L ** 2 / 24, x_M=L / 2, M_min=-w * L ** 2 / 12, V_max=w * L / 2,
                 delta_max=w * L ** 4 / (384 * EI), x_delta=L / 2)
    elif case == "fixed_point_centre":
        r = dict(R_A=P / 2, R_B=P / 2, M_max=P * L / 8, x_M=L / 2, M_min=-P * L / 8, V_max=P / 2,
                 delta_max=P * L ** 3 / (192 * EI), x_delta=L / 2)
    elif case == "propped_udl":
        xd = L * (15 - math.sqrt(33)) / 16        # 0.4215 L from the pinned end
        y = lambda x: w * x * x * (L - x) * (3 * L - 2 * x) / (48 * EI)
        r = dict(R_A=5 * w * L / 8, R_B=3 * w * L / 8, M_max=9 * w * L ** 2 / 128, x_M=5 * L / 8,
                 M_min=-w * L ** 2 / 8, V_max=5 * w * L / 8, delta_max=y(xd), x_delta=xd)
    r["delta_max"] *= 1000.0            # m -> mm
    return r


# ------------------------------------------------------------ route (b): Hermite FE beam
def fe(case, L, EI, w=0.0, P=0.0, M0=0.0, a=0.0, n=600):
    """Beam on n elements.  Returns the same keys as closed() using only equilibrium and compatibility."""
    h = L / n
    nd = n + 1
    K = np.zeros((2 * nd, 2 * nd))
    ke = EI / h ** 3 * np.array([[12, 6 * h, -12, 6 * h], [6 * h, 4 * h * h, -6 * h, 2 * h * h],
                                 [-12, -6 * h, 12, -6 * h], [6 * h, 2 * h * h, -6 * h, 4 * h * h]])
    for e in range(n):
        K[2 * e:2 * e + 4, 2 * e:2 * e + 4] += ke
    F = np.zeros(2 * nd)                # downward positive load => use v positive downward
    udl = w if case in ("ss_udl", "cant_udl", "fixed_udl", "propped_udl") else 0.0
    for e in range(n):
        F[2 * e:2 * e + 4] += udl * h / 2 * np.array([1, h / 6, 1, -h / 6])
    # point load at distance a (nodal when a lands on a node; here a is always a multiple of h in the cases used)
    if case in ("ss_point", "cant_point"):
        k = int(round(a / h))
        F[2 * k] += P
    if case == "fixed_point_centre":
        F[2 * (n // 2)] += P
    # clockwise couple at the left end acts as +rotation (downward-positive v makes clockwise slope positive)
    if case == "ss_couple_left_cw":
        F[1] += M0
    fixed = {"ss_udl": [0, 2 * n], "ss_point": [0, 2 * n], "ss_couple_left_cw": [0, 2 * n],
             "cant_udl": [0, 1], "cant_point": [0, 1], "fixed_udl": [0, 1, 2 * n, 2 * n + 1],
             "fixed_point_centre": [0, 1, 2 * n, 2 * n + 1], "propped_udl": [0, 1, 2 * n]}[case]
    free = [i for i in range(2 * nd) if i not in fixed]
    u = np.zeros(2 * nd)
    u[free] = np.linalg.solve(K[np.ix_(free, free)], F[free])
    react = K @ u - F                   # support force in the dof direction; downward and clockwise positive
    # member end forces -> shear and moment at each node (sagging positive)
    x = np.linspace(0, L, nd)
    M = np.zeros(nd)
    V = np.zeros(nd)
    # internal moment at node k from the left free body: M = sum(R * (x - xr)) - applied loads to the left
    for k in range(nd):
        xs = x[k]
        m = 0.0
        v = 0.0
        for dof in (0, 2 * n):
            xr = 0.0 if dof == 0 else L
            if xr <= xs + 1e-12:
                m += -react[dof] * (xs - xr)      # upward reaction = -react
                v += -react[dof]
        for dof in (1, 2 * n + 1):      # reaction couples (positive = clockwise because rotation sign follows v downward)
            xr = 0.0 if dof == 1 else L
            if xr <= xs + 1e-12:
                m += react[dof]         # a clockwise support couple at the left end sags
        m_load = 0.0
        if case == "ss_couple_left_cw":
            m += M0                 # clockwise couple at the left end sags the beam
        if udl:
            m_load = udl * xs ** 2 / 2
            v -= udl * xs
        if case in ("ss_point", "cant_point") and xs > a + 1e-12:
            m_load += P * (xs - a)
            v -= P
        if case == "fixed_point_centre" and xs > L / 2 + 1e-12:
            m_load += P * (xs - L / 2)
            v -= P
        M[k] = m - m_load
        V[k] = v
    d = u[0::2]
    imax = int(np.argmax(d))
    # parabolic refinement of the maximum deflection
    if 0 < imax < n:
        y0, y1, y2 = d[imax - 1], d[imax], d[imax + 1]
        den = y0 - 2 * y1 + y2
        off = 0.5 * (y0 - y2) / den if den else 0.0
        xd = x[imax] + off * h
        dm = y1 - 0.25 * (y0 - y2) * off
    else:
        xd, dm = x[imax], d[imax]
    return dict(R_A=-react[0], R_B=-react[2 * n], M_max=M.max(), x_M=x[int(np.argmax(M))], M_min=M.min(),
                V_max=np.abs(V).max(), delta_max=dm * 1000.0, x_delta=xd)


# ------------------------------------------------------------ reporting
FAIL = [0]


def row(case, sym, kat, ind, tol, note=""):
    floor = max(tol, 1e-6)
    rel = abs(ind - kat) / abs(kat) if kat != 0 else abs(ind - kat)
    ok = rel <= floor
    FAIL[0] += 0 if ok else 1
    print(f"{case:<26}{sym:<10}{kat:>14.6g}{ind:>14.6g}{rel:>11.2e}{tol:>10.1e}  {'PASS' if ok else 'FAIL'} {note}")


def crosscheck(name, case, **kw):
    """Closed form versus finite element; print a single line and count a failure above 2e-3."""
    c, f = closed(case, **kw), fe(case, **kw)
    worst = 0.0
    for k in c:
        scale = max(abs(c[k]), 1e-9)
        if abs(c[k]) < 1e-9 and abs(f[k]) < 1e-6 * max(abs(c["M_max"]), abs(c["M_min"]), 1):
            continue
        if k in ("x_M", "x_delta") and (abs(c["M_max"]) < 1e-9 if k == "x_M" else False):
            continue
        err = abs(c[k] - f[k]) / scale
        if k == "x_delta":
            err = abs(c[k] - f[k]) / kw["L"] * 5            # position is flat near the maximum
        worst = max(worst, err)
    ok = worst < 2e-3
    FAIL[0] += 0 if ok else 1
    print(f"{'(closed vs FE)':<26}{name:<24} worst rel. diff {worst:.2e}  {'PASS' if ok else 'FAIL'}")


def kats():
    print(f"{'case':<26}{'symbol':<10}{'KAT':>14}{'indep':>14}{'rel.diff':>11}{'tol':>10}  result")
    EI = lambda E_GPa, I_cm4: E_GPa * 1e6 * I_cm4 * 1e-8      # kN*m^2
    r = closed("ss_udl", 6, EI(210, 8000), w=10)
    for s, k, t in [("R_A", 30, 1e-9), ("M_max", 45, 1e-9), ("x_M", 3, 1e-9), ("delta_max", 10.0446, 1e-4)]:
        row("ka-ss-udl", s, k, r[s], t)
    r = closed("ss_point", 6, EI(210, 8000), P=50, a=4)
    for s, k, t in [("R_A", 16.6667, 1e-4), ("R_B", 33.3333, 1e-4), ("M_max", 66.6667, 1e-4),
                    ("x_delta", 3.2660, 1e-4), ("delta_max", 11.5202, 1e-4)]:
        row("ka-ss-point", s, k, r[s], t)
    r = closed("cant_point", 3, EI(200, 2000), P=20, a=3)
    for s, k, t in [("R_A", 20, 1e-9), ("M_min", -60, 1e-9), ("delta_max", 45, 1e-9)]:
        row("ka-cantilever-point", s, k, r[s], t)
    r = closed("fixed_udl", 6, EI(210, 8000), w=10)
    for s, k, t in [("M_min", -30, 1e-9), ("M_max", 15, 1e-9), ("delta_max", 2.00893, 1e-4)]:
        row("ka-fixed-udl", s, k, r[s], t)
    # the finite-element route must agree with the closed forms for every case
    ei = EI(210, 8000)
    crosscheck("ss_udl", "ss_udl", L=6, EI=ei, w=10)
    crosscheck("ss_point a=4", "ss_point", L=6, EI=ei, P=50, a=4)
    crosscheck("ss_point a=1.5", "ss_point", L=6, EI=ei, P=50, a=1.5)
    crosscheck("cant_udl", "cant_udl", L=4, EI=ei, w=12)
    crosscheck("cant_point a=2.4", "cant_point", L=6, EI=ei, P=30, a=2.4)
    crosscheck("fixed_udl", "fixed_udl", L=6, EI=ei, w=10)
    crosscheck("fixed_point_centre", "fixed_point_centre", L=6, EI=ei, P=40)
    crosscheck("propped_udl", "propped_udl", L=8, EI=ei, w=12)
    crosscheck("ss_couple_left_cw", "ss_couple_left_cw", L=6, EI=ei, M0=20)


# ------------------------------------------------------------ extra checks against the C# product
# Product values were read from the probe tool (`Probe run struct.beam-formulas`) on release/v0.26.0 code.
# kN, m, kN*m, mm.  A mismatch prints DIFF and does not change the exit status (KAT cases only).
PRODUCT = {'B1': {'case': 'propped_udl',
            'kw': {'L': 8, 'E': 210, 'I': 20000, 'w': 12},
            'prod': {'R_A': 60.0,
                     'R_B': 36.0,
                     'M_max': 54.0,
                     'x_M': 5.0,
                     'M_min': -96.0,
                     'V_max': 60.0,
                     'delta_max': 6.325868726,
                     'x_delta': 3.372281323}},
     'B2': {'case': 'ss_point',
            'kw': {'L': 6, 'E': 210, 'I': 8000, 'P': 50, 'a': 1.5},
            'prod': {'R_A': 37.5,
                     'R_B': 12.5,
                     'M_max': 56.25,
                     'x_M': 1.5,
                     'M_min': 0.0,
                     'V_max': 37.5,
                     'delta_max': 9.358543433,
                     'x_delta': 2.645898034}},
     'B3': {'case': 'ss_point',
            'kw': {'L': 6, 'E': 210, 'I': 8000, 'P': 50, 'a': 3},
            'prod': {'R_A': 25.0,
                     'R_B': 25.0,
                     'M_max': 75.0,
                     'x_M': 3.0,
                     'M_min': 0.0,
                     'V_max': 25.0,
                     'delta_max': 13.39285714,
                     'x_delta': 3.0}},
     'B4': {'case': 'ss_couple_left_cw',
            'kw': {'L': 6, 'E': 210, 'I': 8000, 'M_0': 20},
            'prod': {'R_A': -3.333333333,
                     'R_B': 3.333333333,
                     'M_max': 20.0,
                     'x_M': 0.0,
                     'M_min': 0.0,
                     'V_max': 3.333333333,
                     'delta_max': 2.749286996,
                     'x_delta': 2.535898385}},
     'B5': {'case': 'cant_udl',
            'kw': {'L': 4, 'E': 200, 'I': 5000, 'w': 12},
            'prod': {'R_A': 48.0,
                     'R_B': 0.0,
                     'M_max': 0.0,
                     'x_M': 0.0,
                     'M_min': -96.0,
                     'V_max': 48.0,
                     'delta_max': 38.4,
                     'x_delta': 4.0}},
     'B6': {'case': 'cant_point',
            'kw': {'L': 6, 'E': 200, 'I': 5000, 'P': 30, 'a': 2.4},
            'prod': {'R_A': 30.0,
                     'R_B': 0.0,
                     'M_max': 0.0,
                     'x_M': 0.0,
                     'M_min': -72.0,
                     'V_max': 30.0,
                     'delta_max': 44.928,
                     'x_delta': 6.0}},
     'B7': {'case': 'fixed_point_centre',
            'kw': {'L': 6, 'E': 210, 'I': 8000, 'P': 40},
            'prod': {'R_A': 20.0,
                     'R_B': 20.0,
                     'M_max': 30.0,
                     'x_M': 3.0,
                     'M_min': -30.0,
                     'V_max': 20.0,
                     'delta_max': 2.678571429,
                     'x_delta': 3.0}},
     'B8': {'case': 'fixed_udl',
            'kw': {'L': 100, 'E': 210, 'I': 100000000.0, 'w': 10000.0},
            'prod': {'R_A': 500000.0,
                     'R_B': 500000.0,
                     'M_max': 4166666.667,
                     'x_M': 50.0,
                     'M_min': -8333333.333,
                     'V_max': 500000.0,
                     'delta_max': 12400.79365,
                     'x_delta': 50.0}},
     'B9': {'case': 'ss_point',
            'kw': {'L': 9, 'E': 70, 'I': 3000, 'P': 70, 'a': 6.3},
            'prod': {'R_A': 21.0,
                     'R_B': 49.0,
                     'M_max': 132.3,
                     'x_M': 6.3,
                     'M_min': 0.0,
                     'V_max': 49.0,
                     'delta_max': 405.963025,
                     'x_delta': 4.956813493}},
     'B10': {'case': 'ss_udl',
             'kw': {'L': 0.1, 'E': 210, 'I': 1, 'w': 0.5},
             'prod': {'R_A': 0.025,
                      'R_B': 0.025,
                      'M_max': 0.000625,
                      'x_M': 0.05,
                      'M_min': 0.0,
                      'V_max': 0.025,
                      'delta_max': 0.0003100198413,
                      'x_delta': 0.05}},
     'B11': {'case': 'ss_point',
             'kw': {'L': 6, 'E': 210, 'I': 8000, 'P': 50, 'a': 6},
             'prod': {'R_A': 0.0,
                      'R_B': 50.0,
                      'M_max': 0.0,
                      'x_M': 6.0,
                      'M_min': 0.0,
                      'V_max': 50.0,
                      'delta_max': 0.0,
                      'x_delta': 3.464101615}},
     'B12': {'case': 'cant_point',
             'kw': {'L': 6, 'E': 210, 'I': 8000, 'P': 10, 'a': 0},
             'prod': {'R_A': 10.0,
                      'R_B': 0.0,
                      'M_max': 0.0,
                      'x_M': 0.0,
                      'M_min': -0.0,
                      'V_max': 10.0,
                      'delta_max': 0.0,
                      'x_delta': 6.0}}}

EXTRA_DIFFS = []
MAP = {"w": "w", "P": "P", "M_0": "M0", "a": "a"}


def xrow(probe, sym, prod, ind, tol, note=""):
    rel = abs(prod - ind) / abs(ind) if ind else abs(prod - ind)
    ok = rel <= max(tol, 1e-6)
    if not ok:
        EXTRA_DIFFS.append((probe, sym))
    print(f"{probe:<26}{sym:<10}{prod:>14.8g}{ind:>14.8g}{rel:>11.2e}{tol:>10.1e}  {'PASS' if ok else 'DIFF'} {note}")


def extras():
    print("--- extra checks: product (hard-coded) against closed form and finite elements ---")
    ei = lambda kw: kw["E"] * 1e6 * kw["I"] * 1e-8
    for tag, d in PRODUCT.items():
        kw, case, prod = d["kw"], d["case"], d["prod"]
        args = {MAP[k]: v for k, v in kw.items() if k in MAP}
        c = closed(case, L=kw["L"], EI=ei(kw), **args)
        name = f"{tag} {case}"[:25]
        # the fixed-end reaction and moment keys
        for sym in ("R_A", "R_B", "M_max", "M_min", "V_max"):
            xrow(name, sym, prod[sym], c[sym], 1e-6)
        # position of the extreme moment; for cantilevers the product reports the hogging extreme at x = 0
        want_xm = 0.0 if case.startswith("cant") else c["x_M"]
        if abs(c["M_max"]) < 1e-12 and not case.startswith("cant"):
            want_xm = prod["x_M"]
        xrow(name, "x_M", prod["x_M"], want_xm, 1e-6, "cantilever: position of M_min" if case.startswith("cant") else "")
        # deflection: closed form, with the finite-element beam as the independent referee
        note = ""
        if case == "propped_udl":
            f = fe(case, L=kw["L"], EI=ei(kw), **args)
            xrow(name, "delta_max", prod["delta_max"], f["delta_max"], 1e-4, "FE referee; product uses w L^4/(185 EI), exact is /184.634 (P1-beam-formulas-02)")
            xrow(name, "x_delta", prod["x_delta"], f["x_delta"], 1e-3, "FE referee; product measures 0.4215 L from the FIXED end (P1-beam-formulas-01)")
        else:
            xrow(name, "delta_max", prod["delta_max"], c["delta_max"], 1e-6)
            if c["delta_max"] > 0:
                xrow(name, "x_delta", prod["x_delta"], c["x_delta"], 1e-6)
    print(f"extra checks with DIFF: {len(EXTRA_DIFFS)} {EXTRA_DIFFS}")


if __name__ == "__main__":
    kats()
    extras()
    print("RESULT:", "ALL PASS" if FAIL[0] == 0 else f"{FAIL[0]} FAIL")
    sys.exit(0 if FAIL[0] == 0 else 1)
