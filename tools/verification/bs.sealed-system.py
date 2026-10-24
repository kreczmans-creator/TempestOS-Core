#!/usr/bin/env python3
"""Independent re-derivation of bs.sealed-system (Track P, P1).

Method sources (cited by title and number only):
  BS EN 442-2 (2014), cl. 5: characteristic equation Phi = K * dT^n.
  BS EN 12828 (2012+A1:2014) and BS 7074-1: expansion vessel by Boyle's law
    with acceptance factor; pressures made absolute.
  Kell, G.S., J. Chem. Eng. Data 20 (1975) 97-105: density of air-free water
    at 1 atm, 0-150 degC, polynomial/rational form coded from the paper.
Exit 0 when every KAT value matches within max(KAT tolerance, 1e-6).
"""
import math
import sys

G = 9.80665


def kell_rho(t):
    """Water density, kg/m3, Kell (1975), t in degC (IPTS-68 treated as ITS-90)."""
    num = (999.83952 + 16.945176 * t - 7.9870401e-3 * t ** 2 - 46.170461e-6 * t ** 3
           + 105.56302e-9 * t ** 4 - 280.54253e-12 * t ** 5)
    return num / (1.0 + 16.879850e-3 * t)


def delta_t(tf, tr, ti, method="am"):
    if method == "am":
        return (tf + tr) / 2.0 - ti
    return (tf - tr) / math.log((tf - ti) / (tr - ti))


def sealed(rooms=(), tf=75, tr=65, ti=20, n=1.3, method="am", v_pipe=20, v_plant=10,
           t_fill=10, t_max=85, e_fluid=None, h=5, p_margin=0.3, p_f=2.5, v_vessel=None):
    o = {}
    dt = delta_t(tf, tr, ti, method)
    f = (dt / 50.0) ** n
    o["dT"], o["f_dT"] = dt, f
    v_em = sum(r[3] for r in rooms)
    o["V_em"] = v_em
    o["Phi_tot"] = sum(r[1] for r in rooms)
    for i, r in enumerate(rooms, 1):
        o[f"R{i}"] = r[1] / f
        if r[2] > 0:
            o[f"u{i}"] = r[1] / (r[2] * f)
    us = [o[k] for k in o if k.startswith("u") and k[1:].isdigit()]
    if us:
        o["u_emit"] = max(us)
    vs = v_em + v_pipe + v_plant
    o["V_s"] = vs
    if e_fluid is None:
        ex = kell_rho(t_fill) / kell_rho(t_max) - 1.0
        rho_fill = kell_rho(t_fill)
    else:
        ex = e_fluid
        rho_fill = kell_rho(t_fill)
    o["e_x"] = ex
    pi = rho_fill * G * h / 1e5 + p_margin
    o["p_i"] = pi
    ve = ex * vs / (1.0 - (pi + 1.0) / (p_f + 1.0))
    o["V_e"] = ve
    if v_vessel:
        o["u_vessel"] = ve / v_vessel
    return o


KATS = [
    ("ka-dt50", dict(rooms=[("Lounge", 1500, 1600, 8)], v_pipe=20, v_plant=10),
     [("dT", 50, 1e-9), ("f_dT", 1, 1e-9), ("R1", 1500, 1e-9), ("u_emit", 0.9375, 1e-9), ("V_s", 38, 1e-9)]),
    ("ka-dt30", dict(rooms=[("Lounge", 1500, 0, 0)], tf=55, tr=45),
     [("f_dT", 0.5147503, 1e-6), ("R1", 2914.034, 1e-6)]),
    ("ka-lmtd", dict(rooms=[("Lounge", 1500, 0, 0)], tf=55, tr=45, method="lm"),
     [("dT", 29.72013, 1e-6), ("f_dT", 0.5085164, 1e-6)]),
    ("ka-vessel", dict(v_pipe=90, v_plant=10, v_vessel=8),
     [("e_x", 0.03208619, 1e-6), ("p_i", 0.7901852, 1e-6), ("V_e", 6.568060, 1e-6), ("u_vessel", 0.8210075, 1e-6)]),
    ("ka-manufacturer-dt30", dict(tf=55, tr=45), [("f_dT", 0.51, 0.01)]),
]

EXTRAS = []


def run():
    fails = 0
    print(f"{'case':24s} {'symbol':8s} {'KAT':>14s} {'independent':>14s} {'rel.diff':>10s} {'tol':>8s} result")
    for case, kw, exp in KATS:
        res = sealed(**kw)
        for sym, e, tol in exp:
            v = res[sym]
            rd = abs(v - e) / abs(e) if e != 0 else abs(v)
            ok = rd <= max(tol, 1e-6)
            fails += (not ok)
            print(f"{case:24s} {sym:8s} {e:14.7g} {v:14.7g} {rd:10.2e} {tol:8.0e} {'PASS' if ok else 'FAIL'}")
    return fails


if __name__ == "__main__":
    f = run()
    f += run_extras() if "run_extras" in globals() else 0
    print("ALL PASS" if f == 0 else f"{f} FAILURES")
    sys.exit(0 if f == 0 else 1)
