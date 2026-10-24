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
    if abs(tf - tr) < 1e-12:
        return tf - ti  # limit of the log mean when flow equals return
    return (tf - tr) / math.log((tf - ti) / (tr - ti))


def sealed(rooms=(), tf=75, tr=65, ti=20, n=1.3, method="am", v_pipe=20, v_plant=10,
           t_fill=10, t_max=85, e_fluid=None, h=5, p_margin=0.3, p_f=2.5, v_vessel=None):
    o = {}
    dt = delta_t(tf, tr, ti, method)
    f = (dt / 50.0) ** n
    o["dT"], o["f_dT"] = dt, f
    # 1.1.0 ports (A7a re-check): loop temperature drop and mean water temperature (kelvin, as the product stores it)
    o["dT_fr"] = tf - tr
    o["t_m_K"] = (tf + tr) / 2.0 + 273.15
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

# Extra checks. Product values were read from the C# calculator with the probe tool
# (tools/verification/probe, command "run", Release build of release/v0.26.0).
# Units: W, K, L, bar, degC.
EXTRAS = [
    ('S1 70/50/21 AMTD, 3 rooms, 12 L vessel', dict(rooms=[('Lounge',2200,2400,3.1),('Kitchen',1100,1000,1.5),('Bed',900,0,2.0)], tf=70, tr=50, ti=21, v_pipe=60, v_plant=15, v_vessel=12),
     {'dT': 39.0, 'f_dT': 0.723974023, 'R1': 3038.783064, 'R2': 1519.391532, 'R3': 1243.138526, 'Phi_tot': 4200.0, 'V_em': 6.6, 'V_s': 81.6, 'e_x': 0.03208619, 'p_i': 0.790185221, 'V_e': 5.35953722, 'u_emit': 1.519391532, 'u_vessel': 0.446628102}),
    ('S2 80/40/20 log mean', dict(rooms=[('A',1000,0,0)], tf=80, tr=40, ti=20, method="lm"),
     {'dT': 36.40956907, 'f_dT': 0.662093302, 'R1': 1510.361147, 'Phi_tot': 1000.0, 'V_em': 0.0, 'V_s': 30.0, 'e_x': 0.03208619, 'p_i': 0.790185221, 'V_e': 1.970418095}),
    ('S3 60/60/20 log mean (equal flow and return)', dict(rooms=[('A',1000,0,0)], tf=60, tr=60, ti=20, method="lm"),
     {'dT': 40.0, 'f_dT': 0.748198758, 'R1': 1336.54325, 'Phi_tot': 1000.0, 'V_em': 0.0, 'V_s': 30.0, 'e_x': 0.03208619, 'p_i': 0.790185221, 'V_e': 1.970418095}),
    ('S4 1100 L, fill 5, max 95, 30 m, 5/6 bar', dict(v_pipe=1000, v_plant=100, t_fill=5, t_max=95, h=30, p_margin=0.5, p_f=5),
     {'dT': 50.0, 'f_dT': 1.0, 'V_em': 0.0, 'V_s': 1100.0, 'e_x': 0.039571055, 'p_i': 3.441888562, 'V_e': 167.6189242}),
    ('S5 glycol record e = 0.05', dict(v_pipe=90, v_plant=10, e_fluid=0.05),
     {'dT': 50.0, 'f_dT': 1.0, 'V_em': 0.0, 'V_s': 100.0, 'e_x': 0.05, 'p_i': 0.790185221, 'V_e': 10.23502675}),
    ('S6 fill 4, max 30', dict(v_pipe=90, v_plant=10, t_fill=4, t_max=30),
     {'dT': 50.0, 'f_dT': 1.0, 'V_em': 0.0, 'V_s': 100.0, 'e_x': 0.004343642, 'p_i': 0.790318768, 'V_e': 0.889215298}),
    ('S7 p_f 2.95 bar, PRV 3 bar', dict(v_pipe=90, v_plant=10, p_f=2.95),
     {'dT': 50.0, 'f_dT': 1.0, 'V_em': 0.0, 'V_s': 100.0, 'e_x': 0.03208619, 'p_i': 0.790185221, 'V_e': 5.868116696}),
    ('S8 10 000 L system', dict(v_pipe=9990, v_plant=10),
     {'dT': 50.0, 'f_dT': 1.0, 'V_em': 0.0, 'V_s': 10000.0, 'e_x': 0.03208619, 'p_i': 0.790185221, 'V_e': 656.8060318}),
    ('S9 n 1.5, 90/90/16 (dT 74 K)', dict(rooms=[('A',1000,1500,0)], tf=90, tr=90, ti=16, n=1.5, t_max=99),
     {'dT': 74.0, 'f_dT': 1.800497709, 'R1': 555.4019841, 'Phi_tot': 1000.0, 'V_em': 0.0, 'V_s': 30.0, 'e_x': 0.042352425, 'p_i': 0.790185221, 'V_e': 2.600869187, 'u_emit': 0.370267989}),
    ('S10 fill 1, max 99', dict(v_pipe=90, v_plant=10, t_fill=1, t_max=99),
     {'dT': 50.0, 'f_dT': 1.0, 'V_em': 0.0, 'V_s': 100.0, 'e_x': 0.042559802, 'p_i': 0.790282744, 'V_e': 8.712511121}),
    ('S15 emitter output entered in kW (Phi 1.5 kW, Phi50 1.6 kW)', dict(rooms=[("Lounge", 1500, 1600, 8)]),
     {'dT': 50.0, 'f_dT': 1.0, 'R1': 1500.0, 'V_em': 8.0, 'V_s': 38.0, 'u_emit': 0.9375, 'V_e': 2.495862921}),
    ('S16 new ports 75/65/20 (1.1.0)', dict(rooms=[('A',1000,0,0)], tf=75, tr=65, ti=20),
     {'dT': 50.0, 'dT_fr': 10.0, 't_m_K': 343.15}),
    ('S17 new ports 90/70/20', dict(rooms=[('A',1000,0,0)], tf=90, tr=70, ti=20),
     {'dT': 60.0, 'dT_fr': 20.0, 't_m_K': 353.15}),
    ('S18 new ports 60/60/20 log mean', dict(rooms=[('A',1000,0,0)], tf=60, tr=60, ti=20, method="lm"),
     {'dT': 40.0, 'dT_fr': 0.0, 't_m_K': 333.15}),
    ('S19 new ports from 167/149/68 degF', dict(rooms=[('A',1000,0,0)], tf=75, tr=65, ti=20),
     {'dT': 50.0, 'dT_fr': 10.0, 't_m_K': 343.15}),
    ('S11 fill 30 = max 30 (e = 0)', dict(v_pipe=90, v_plant=10, t_fill=30, t_max=30, tf=30, tr=30, ti=16),
     {'dT': 14.0, 'f_dT': 0.191119694, 'V_em': 0.0, 'V_s': 100.0, 'e_x': 0.0, 'p_i': 0.78819821, 'V_e': 0.0}),
]


def run_extras():
    fails = 0
    print()
    print("Extra checks (product values read with the probe tool)")
    print(f"{'probe':46s} {'symbol':8s} {'product':>13s} {'independent':>13s} {'rel.diff':>9s} result")
    for label, kw, prod in EXTRAS:
        ind = sealed(**kw)
        for sym, pv in prod.items():
            if sym not in ind:
                print(f"{label[:46]:46s} {sym:8s} not computed by the independent model FAIL")
                fails += 1
                continue
            iv = ind[sym]
            scale = max(abs(pv), abs(iv), 1e-12)
            rd = abs(pv - iv) / scale
            ok = rd <= 1e-6 or abs(pv - iv) < 1e-9
            fails += (not ok)
            print(f"{label[:46]:46s} {sym:8s} {pv:13.7g} {iv:13.7g} {rd:9.2e} {'PASS' if ok else 'FAIL'}")

    # ---- Behaviour that matches the product but is questionable (see the report) ----
    print()
    print("Observations that explain the findings (independent figures)")
    # 01: fill temperature above the maximum temperature gives a negative expansion.
    o = sealed(v_pipe=90, v_plant=10, t_fill=40, t_max=30, tf=30, tr=30, ti=16)
    print(f"P1-bs.sealed-system-01  fill 40, max 30 degC: e_x {o['e_x']:.7f}, V_e {o['V_e']:.5f} L"
          f"  (product: -0.0034465, -0.70399 L, no block; vessel check shows FAIL with u = -0.088)")
    # 02: final pressure 0.05 bar below the safety valve setting is accepted.
    d = sealed(v_pipe=90, v_plant=10, p_f=2.5)["V_e"]
    c = sealed(v_pipe=90, v_plant=10, p_f=2.95)["V_e"]
    print(f"P1-bs.sealed-system-02  V_e at p_f 2.5 bar {d:.4f} L, at p_f 2.95 bar (PRV 3) {c:.4f} L: {100*(c/d-1):+.1f} %")
    # 03: water reserve (seal) volume of 0.5 % of V_s, minimum 3 L, not in the product formula.
    o = sealed(v_pipe=90, v_plant=10)
    vwr = max(0.005 * o["V_s"], 3.0)
    ve_res = (o["e_x"] * o["V_s"] + vwr) / (1.0 - (o["p_i"] + 1.0) / (2.5 + 1.0))
    print(f"P1-bs.sealed-system-03  ka-vessel V_e {o['V_e']:.4f} L; with a {vwr:.1f} L reserve {ve_res:.4f} L (only if the standard requires it)")
    # 06: 1 bar versus 1.01325 bar for atmospheric pressure.
    pa = 1.01325
    v1 = o["e_x"] * o["V_s"] / (1.0 - (o["p_i"] + pa) / (2.5 + pa))
    print(f"P1-bs.sealed-system-06  ka-vessel V_e with 1.01325 bar atmosphere {v1:.4f} L ({100*(v1/o['V_e']-1):+.2f} %)")
    # 04: no range guard on the mean temperature difference.
    o = sealed(rooms=[("A", 1000, 0, 0)], tf=30, tr=30, ti=24)
    print(f"P1-bs.sealed-system-04  30/30/24 degC: dT {o['dT']:.1f} K, f_dT {o['f_dT']:.4f}, 1000 W needs {o['R1']:.0f} W at dT 50 (product: no warning)")
    # 05: with a glycol record the static head still uses the water density at fill.
    #     Illustration: a mixture 4 % denser than water (value chosen for the example only).
    o = sealed(v_pipe=90, v_plant=10, e_fluid=0.05)
    p_gly = 1.04 * kell_rho(10) * G * 5 / 1e5 + 0.3
    ve_gly = 0.05 * o["V_s"] / (1.0 - (p_gly + 1.0) / (2.5 + 1.0))
    print(f"P1-bs.sealed-system-05  e = 0.05: V_e {o['V_e']:.4f} L (water density) vs {ve_gly:.4f} L (4 % denser fluid): {100*(ve_gly/o['V_e']-1):+.2f} %")
    # C1 (A7a re-check): dT and dT_fr are both kelvin differences; linking dT to B1's dT_load is accepted.
    o = sealed(rooms=[("A", 1000, 0, 0)], tf=75, tr=65, ti=20)
    print(f"P1-bs.sealed-system-C1  75/65/20: dT {o['dT']:.0f} K, dT_fr {o['dT_fr']:.0f} K; flow Q = Phi/(rho c dT) is "
          f"{o['dT'] / o['dT_fr']:.1f} times too small if dT is linked instead of dT_fr (product: no notice)")
    # ---- Independent review (2026-10-24): standard-based figures. Values recalled, owner to confirm.
    # BS EN 12828 Annex D (as recalled): p_e <= p_SV - 0.5 bar for p_SV <= 5 bar, else p_e <= 0.9 p_SV;
    # water reserve V_WR >= 0.5 % of V_s, at least 3 L; V_n >= (V_e + V_WR)(p_e + 1)/(p_e - p_0).
    print()
    print("Review: figures against BS EN 12828 Annex D and BS 7074-1 as recalled (owner to confirm)")
    for p_sv in (3.0, 6.0):
        pe_max = p_sv - 0.5 if p_sv <= 5 else 0.9 * p_sv
        print(f"  p_SV {p_sv:.1f} bar: highest final pressure p_e {pe_max:.2f} bar (product accepts any value below {p_sv:.2f} bar)")
    # BS 7074-1 domestic factors as recalled (3 bar valve; charge = initial pressure 0.5, 1.0, 1.5 bar):
    # 0.063, 0.084, 0.126. Back-solve the expansion and final pressure that reproduce all three.
    for pe in (2.5, 2.65):
        es = [fac * (1 - (pc + 1) / (pe + 1)) for fac, pc in ((0.063, 0.5), (0.084, 1.0), (0.126, 1.5))]
        print(f"  BS 7074-1 factors with p_f {pe:.2f} bar imply e = " + ", ".join(f"{e:.4f}" for e in es))
    o = sealed(v_pipe=90, v_plant=10, v_vessel=8)
    vwr = max(0.005 * o["V_s"], 3.0)
    vn = (o["e_x"] * o["V_s"] + vwr) * (2.5 + 1) / (2.5 - o["p_i"])
    print(f"  ka-vessel: product V_e {o['V_e']:.3f} L (8 L vessel passes); with reserve {vn:.3f} L (8 L vessel fails)")
    return fails


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
