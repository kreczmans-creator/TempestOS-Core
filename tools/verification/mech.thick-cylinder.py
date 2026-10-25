#!/usr/bin/env python3
"""Independent re-derivation of mech.thick-cylinder (Track P, P1).

Method: Lame solution for a long, elastic, isotropic thick-walled cylinder.
Sources: Shigley's Mechanical Engineering Design, 9th ed., section 3-14;
Roark's Formulas for Stress and Strain, thick-walled cylinder cases (Table 13.5 in the cited edition);
Timoshenko, Theory of Elasticity, Lame problem. Units: mm, MPa.

  A = (a^2 p_i - b^2 p_o)/(b^2 - a^2)       B = a^2 b^2 (p_i - p_o)/(b^2 - a^2)
  sigma_r(r) = A - B/r^2     sigma_t(r) = A + B/r^2
  sigma_z = A  (closed ends, pressure end load over the wall area)  or 0 (open ends)
  von Mises from the three principal stresses; Tresca = max - min principal stress
  thin wall: (p_i - p_o)(a + b)/(2 (b - a))
  utilisation u = max(vM at a, vM at b)/sigma_allow
"""
import math
import sys


def lame(a, b, p_i, p_o, closed=True, sigma_allow=None):
    A = (a**2 * p_i - b**2 * p_o) / (b**2 - a**2)
    B = a**2 * b**2 * (p_i - p_o) / (b**2 - a**2)
    out = {"A": A, "B": B}
    sz = A if closed else 0.0
    out["sigma_z"] = sz
    for tag, r in (("a", a), ("b", b)):
        sr = A - B / r**2
        st = A + B / r**2
        out["sigma_r_" + tag] = sr
        out["sigma_t_" + tag] = st
        s = (st, sr, sz)
        out["sigma_vm_" + tag] = math.sqrt(0.5 * ((st - sr)**2 + (sr - sz)**2 + (sz - st)**2))
        out["sigma_tr_" + tag] = max(s) - min(s)
    out["sigma_vm_max"] = max(out["sigma_vm_a"], out["sigma_vm_b"])
    out["sigma_thin"] = (p_i - p_o) * (a + b) / (2 * (b - a))
    if sigma_allow:
        out["u"] = out["sigma_vm_max"] / sigma_allow
    return out


KATS = [
    ("ka-internal-closed", lame(50, 100, 50, 0, True, 200),
     [("sigma_t_a", 83.333, 1e-4), ("sigma_r_a", -50, 1e-6), ("sigma_t_b", 33.333, 1e-4), ("sigma_z", 16.667, 1e-4),
      ("sigma_vm_a", 115.47, 1e-4), ("sigma_tr_a", 133.33, 1e-4), ("u", 0.57735, 1e-4), ("sigma_thin", 75, 1e-6)]),
    ("ka-external-closed", lame(40, 60, 0, 20, True, 100),
     [("sigma_t_a", -72, 1e-6), ("sigma_t_b", -52, 1e-6), ("sigma_z", -36, 1e-6),
      ("sigma_vm_a", 62.354, 1e-4), ("sigma_vm_b", 27.713, 1e-4), ("u", 0.62354, 1e-4)]),
    ("ka-internal-open", lame(50, 100, 50, 0, False, 200),
     [("sigma_vm_a", 116.67, 1e-4), ("sigma_vm_b", 33.333, 1e-4), ("u", 0.58333, 1e-4)]),
]



# Extra probes. Product values were read from `Probe run mech.thick-cylinder` (MPa; u has no unit).
# Entry: label, inputs, {symbol: product value}.
PROBES = [
    ('r2 both pressures closed', dict(a=60, b=90, p_i=80, p_o=30, closed=True, sigma_allow=250),
     dict(sigma_r_a=-80, sigma_t_a=100, sigma_r_b=-30, sigma_t_b=50, sigma_z=10, sigma_vm_a=155.8845727, sigma_vm_b=69.2820323, sigma_tr_a=180, sigma_tr_b=80, u=0.6235382907, sigma_thin=125)),
    ('r2o both pressures open', dict(a=60, b=90, p_i=80, p_o=30, closed=False, sigma_allow=250),
     dict(sigma_r_a=-80, sigma_t_a=100, sigma_r_b=-30, sigma_t_b=50, sigma_z=0, sigma_vm_a=156.2049935, sigma_vm_b=70, sigma_tr_a=180, sigma_tr_b=80, u=0.6248199741, sigma_thin=125)),
    ('r3 hydrostatic', dict(a=60, b=90, p_i=40, p_o=40, closed=True, sigma_allow=250),
     dict(sigma_r_a=-40, sigma_t_a=-40, sigma_r_b=-40, sigma_t_b=-40, sigma_z=-40, sigma_vm_a=0, sigma_vm_b=0, sigma_tr_a=0, sigma_tr_b=0, u=0, sigma_thin=0)),
    ('r4 thin wall', dict(a=100, b=103, p_i=10, p_o=0, closed=True, sigma_allow=200),
     dict(sigma_r_a=-10, sigma_t_a=338.407225, sigma_r_b=0, sigma_t_b=328.407225, sigma_z=164.2036125, sigma_vm_a=301.7295077, sigma_vm_b=284.4089996, sigma_tr_a=348.407225, sigma_tr_b=328.407225, u=1.508647538, sigma_thin=338.3333333)),
    ('r5 tiny', dict(a=0.01, b=0.02, p_i=1, p_o=0, closed=True, sigma_allow=200),
     dict(sigma_r_a=-1, sigma_t_a=1.666666667, sigma_r_b=-5.820766091e-17, sigma_t_b=0.6666666667, sigma_z=0.3333333333, sigma_vm_a=2.309401077, sigma_vm_b=0.5773502692, sigma_tr_a=2.666666667, sigma_tr_b=0.6666666667, u=0.01154700538, sigma_thin=1.5)),
    ('r6 large', dict(a=5000, b=6000, p_i=1000, p_o=0, closed=True, sigma_allow=3000),
     dict(sigma_r_a=-1000, sigma_t_a=5545.454545, sigma_r_b=-4.768371582e-13, sigma_t_b=4545.454545, sigma_z=2272.727273, sigma_vm_a=5668.529916, sigma_vm_b=3936.479108, sigma_tr_a=6545.454545, sigma_tr_b=4545.454545, u=1.889509972, sigma_thin=5500)),
    ('r7 b>>a closed', dict(a=1, b=1000, p_i=100, p_o=0, closed=True, sigma_allow=200),
     dict(sigma_r_a=-100, sigma_t_a=100.0002, sigma_r_b=0, sigma_t_b=0.0002000002, sigma_z=0.0001000001, sigma_vm_a=173.205254, sigma_vm_b=0.000173205254, sigma_tr_a=200.0002, sigma_tr_b=0.0002000002, u=0.8660262698, sigma_thin=50.1001001)),
    ('r7o b>>a open', dict(a=1, b=1000, p_i=100, p_o=0, closed=False, sigma_allow=200),
     dict(sigma_r_a=-100, sigma_t_a=100.0002, sigma_r_b=0, sigma_t_b=0.0002000002, sigma_z=0, sigma_vm_a=173.205254, sigma_vm_b=0.0002000002, sigma_tr_a=200.0002, sigma_tr_b=0.0002000002, u=0.8660262698, sigma_thin=50.1001001)),
    ('r8 ext buckling', dict(a=95, b=100, p_i=0, p_o=6, closed=True, sigma_allow=200),
     dict(sigma_r_a=0, sigma_t_a=-123.0769231, sigma_r_b=-6, sigma_t_b=-117.0769231, sigma_z=-61.53846154, sigma_vm_a=106.587742, sigma_vm_b=96.19543716, sigma_tr_a=123.0769231, sigma_tr_b=111.0769231, u=0.53293871, sigma_thin=-117)),
    ('r10 over allowable', dict(a=50, b=100, p_i=150, p_o=0, closed=True, sigma_allow=200),
     dict(sigma_r_a=-150, sigma_t_a=250, sigma_r_b=0, sigma_t_b=100, sigma_z=50, sigma_vm_a=346.4101615, sigma_vm_b=86.60254038, sigma_tr_a=400, sigma_tr_b=100, u=1.732050808, sigma_thin=225)),
    ('r11 p_o>p_i both', dict(a=30, b=45, p_i=10, p_o=60, closed=True, sigma_allow=250),
     dict(sigma_r_a=-10, sigma_t_a=-190, sigma_r_b=-60, sigma_t_b=-140, sigma_z=-100, sigma_vm_a=155.8845727, sigma_vm_b=69.2820323, sigma_tr_a=180, sigma_tr_b=80, u=0.6235382907, sigma_thin=-125)),
    ('r12 very thin', dict(a=100, b=100.01, p_i=1, p_o=0, closed=True, sigma_allow=200),
     dict(sigma_r_a=-1, sigma_t_a=10000.50003, sigma_r_b=0, sigma_t_b=9999.500025, sigma_z=4999.750013, sigma_vm_a=8661.553098, sigma_vm_b=8659.821047, sigma_tr_a=10001.50003, sigma_tr_b=9999.500025, u=43.30776549, sigma_thin=10000.5)),
]

# Informational: long-tube elastic collapse under external pressure (Roark, chapter on elastic stability;
# Timoshenko, Theory of Elastic Stability): p_cr = E/(4(1 - nu^2)) (t/r_m)^3, E 207 GPa, nu 0.3.
# The product reports u = 0.666 and a pass at p_o = 7.5 MPa for a = 95 mm, b = 100 mm.
COLLAPSE_CASE = dict(a=95.0, b=100.0, p_o=7.5, E=207000.0, nu=0.3, product_u=0.6661733875)


def main():
    fails = 0
    print("== Known-answer cases ==")
    print(f"{'case':20} {'symbol':12} {'KAT exp':>10} {'indep':>12} {'rel diff':>10} {'KAT tol':>8} result")
    for case, res, exp in KATS:
        for sym, e, tol in exp:
            v = res[sym]
            rd = abs(v - e) / abs(e)
            ok = rd <= max(tol, 1e-6)
            fails += not ok
            print(f"{case:20} {sym:12} {e:10.5g} {v:12.6g} {rd:10.2e} {tol:8.0e} {'PASS' if ok else 'FAIL'}")
    print("== Extra probes: product (hard-coded) vs independent ==")
    for label, inp, prod in PROBES:
        ind = lame(inp["a"], inp["b"], inp["p_i"], inp["p_o"], inp["closed"], inp["sigma_allow"])
        scale = max(abs(ind["sigma_t_a"]), abs(ind["sigma_vm_max"]), 1e-12)
        worst = 0.0
        bad = []
        for sym, pv in prod.items():
            iv = ind[sym]
            rd = abs(pv - iv) / max(abs(iv), 1e-9 * scale)  # near-zero values: compare to 1e-9 of the stress scale
            worst = max(worst, rd)
            if rd > 1e-6 and abs(pv - iv) > 1e-9 * scale:
                bad.append((sym, pv, iv))
        ok = not bad
        fails += not ok
        print(f"{label:28} {len(prod)} symbols, worst rel diff {worst:9.2e} (floor 1e-9 of scale) {'PASS' if ok else 'FAIL ' + str(bad)}")
    c = COLLAPSE_CASE
    t = c["b"] - c["a"]
    rm = (c["a"] + c["b"]) / 2
    pcr = c["E"] / (4 * (1 - c["nu"]**2)) * (t / rm)**3
    print("== Informational: external-pressure collapse (finding 01) ==")
    print(f"a={c['a']}, b={c['b']}, p_o={c['p_o']} MPa: product u={c['product_u']:.3f} (pass); long-tube p_cr={pcr:.2f} MPa; p_o/p_cr={c['p_o'] / pcr:.2f}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
