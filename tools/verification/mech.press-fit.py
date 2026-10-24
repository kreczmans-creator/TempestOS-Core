#!/usr/bin/env python3
"""Independent re-derivation of mech.press-fit (Track P, P1).

Method (Lame thick-cylinder relations; Shigley's Mechanical Engineering Design,
9th ed., eqs. 3-56 to 3-58 and the pressurised-cylinder relations of section 3-16;
Roark 7th/8th ed., thick-walled cylinder cases). Units here: mm, N, MPa, K.

  delta_r = delta / 2                               (radial interference)
  C_h = R/E_h * ((ro^2 + R^2)/(ro^2 - R^2) + nu_h)  (hub bore growth per unit p)
  C_s = R/E_s * ((R^2 + ri^2)/(R^2 - ri^2) - nu_s)  (shaft surface shrink per unit p)
  p   = delta_r / (C_h + C_s)
  hub bore:      sigma_t =  p (ro^2 + R^2)/(ro^2 - R^2), sigma_r = -p
  shaft surface: sigma_t = -p (R^2 + ri^2)/(R^2 - ri^2), sigma_r = -p
  shaft bore (hollow shaft only): sigma_t = -2 p R^2/(R^2 - ri^2), sigma_r = 0
  von Mises, sigma_z = 0: sqrt(st^2 - st*sr + sr^2)
  F = mu p 2 pi R L ;  T = F R ;  dT = (delta + c_a)/(alpha d)
"""
import math
import sys


def pressfit(d, delta_um, d_o, d_i, L, E_h, nu_h, E_s, nu_s, mu, Sy_h, Sy_s, alpha, c_a_um):
    """d, d_o, d_i, L in mm; delta, c_a in um; E in GPa; Sy in MPa; alpha in 1/K."""
    R = d / 2.0
    ro = d_o / 2.0
    ri = d_i / 2.0
    E_h *= 1000.0
    E_s *= 1000.0
    dr = delta_um / 1000.0 / 2.0
    kh = (ro**2 + R**2) / (ro**2 - R**2)
    ks = (R**2 + ri**2) / (R**2 - ri**2)
    Ch = R / E_h * (kh + nu_h)
    Cs = R / E_s * (ks - nu_s)
    p = dr / (Ch + Cs)
    out = {"p": p}
    out["sigma_t_hub"] = p * kh
    out["sigma_t_shaft"] = -p * ks
    vm = lambda st, sr: math.sqrt(st * st - st * sr + sr * sr)
    out["sigma_vm_hub"] = vm(out["sigma_t_hub"], -p)
    out["sigma_vm_shaft"] = vm(out["sigma_t_shaft"], -p)
    out["sigma_t_shaft_bore"] = -2 * p * R**2 / (R**2 - ri**2)
    out["sigma_vm_shaft_bore"] = abs(out["sigma_t_shaft_bore"])
    out["u_hub"] = out["sigma_vm_hub"] / Sy_h
    out["u_shaft_surface"] = out["sigma_vm_shaft"] / Sy_s
    out["u_shaft_bore"] = out["sigma_vm_shaft_bore"] / Sy_s
    F = mu * p * 2 * math.pi * R * L  # N (MPa * mm^2)
    out["F_a"] = F / 1000.0  # kN
    out["T"] = F * R / 1000.0  # N*m
    out["dT"] = (delta_um + c_a_um) / (alpha * d * 1000.0)
    return out


STD = dict(d=50, delta_um=40, d_o=100, d_i=0, L=60, E_h=207, nu_h=0.292, E_s=207, nu_s=0.292,
           mu=0.15, Sy_h=350, Sy_s=350, alpha=12e-6, c_a_um=20)
HOLLOW = dict(STD, d_i=25)

KATS = [
    ("ka-steel-steel-solid", pressfit(**STD),
     [("p", 62.1, 1e-4), ("sigma_t_hub", 103.5, 1e-4), ("F_a", 87.80, 1e-3), ("T", 2195.0, 1e-3), ("dT", 100, 1e-6)]),
    ("ka-hollow-shaft", pressfit(**HOLLOW),
     [("p", 49.63, 2e-3), ("sigma_t_shaft", -82.7, 2e-3)]),
]



# Extra probes. Product values were read from `Probe run mech.press-fit` (MPa, kN, N*m, K).
# Entry: label, input overrides on STD, {symbol: product value}, finding flag.
# Finding rows compare the product's shaft check value with the governing (bore) value.
EXTRAS_IN = [
    ("q1 Al hub on steel shaft", dict(E_h=70, nu_h=0.33, alpha=23e-6, Sy_h=250, delta_um=60, d_o=90),
     dict(p=34.1148, sigma_t_hub=64.5744, sigma_vm_hub=86.8136, sigma_vm_shaft=34.1148, F_a=48.2286, T=1205.72, dT=69.5652)),
    ("q2 hollow bore 25, 120 um", dict(d_i=25, delta_um=120),
     dict(p=149.04, sigma_t_hub=248.4, sigma_t_shaft=-248.4, sigma_vm_hub=347.76, sigma_vm_shaft=216.55, F_a=210.7, T=5267.51, dT=233.333)),
    ("q3 thin tube bore 45", dict(d_i=45),
     dict(p=14.795, sigma_t_hub=24.6583, sigma_t_shaft=-140.942, sigma_vm_hub=34.5216, sigma_vm_shaft=134.157, F_a=20.9159, T=522.898)),
    ("q4 thin hub d_o 52", dict(d_o=52, delta_um=20),
     dict(p=3.12337, sigma_t_hub=79.6766, sigma_vm_hub=81.2833, F_a=4.41556, T=110.389, dT=66.6667)),
    ("q5 zero interference", dict(delta_um=0), dict(p=0.0, F_a=0.0, T=0.0, dT=33.3333)),
    ("q6 large 1000 mm hollow", dict(d=1000, d_o=1500, delta_um=800, L=500, d_i=300),
     dict(p=43.6042, sigma_t_hub=113.371, sigma_t_shaft=-52.2292, sigma_vm_hub=140.349, sigma_vm_shaft=48.4954, F_a=10274, T=5.13699e+06, dT=68.3333)),
    ("q8 nu_h 0.5, nu_s 0", dict(nu_h=0.5, nu_s=0), dict(p=52.2947, sigma_vm_hub=122.021, F_a=73.9299)),
    ("q9 Cu hub, steel shaft", dict(E_h=117, nu_h=0.34, delta_um=50, alpha=17e-6, mu=0.2, L=40, d=30, d_o=60),
     dict(p=81.0191, sigma_t_hub=135.032, sigma_vm_hub=189.045, F_a=61.0869, T=916.304, dT=137.255)),
    ("q10 tiny 2 mm shaft", dict(d=2, delta_um=2, d_o=6, L=3, c_a_um=1), dict(p=92.0, sigma_t_hub=115.0, F_a=0.260124, T=0.260124, dT=125.0)),
    ("q11 tube bore 49.9", dict(d_i=49.9, delta_um=10), dict(p=0.0826072, sigma_t_shaft=-41.2623, sigma_vm_shaft=41.2211)),
]
# Finding rows: (label, overrides, product shaft vM at surface, product shaft utilisation)
FINDING_ROWS = [
    ("q2 hollow bore 25, 120 um (finding 01)", dict(d_i=25, delta_um=120), 216.55),
    ("q7 hollow bore 25, 40 um (finding 01)", dict(d_i=25, delta_um=40), 72.1834),
    ("q3 thin tube bore 45 (finding 01)", dict(d_i=45), 134.157),
]


def main():
    fails = 0
    print("== Known-answer cases ==")
    print(f"{'case':22} {'symbol':14} {'KAT exp':>10} {'indep':>12} {'rel diff':>10} {'KAT tol':>8} result")
    for case, res, exp in KATS:
        for sym, e, tol in exp:
            v = res[sym]
            rd = abs(v - e) / abs(e)
            ok = rd <= max(tol, 1e-6)
            fails += not ok
            print(f"{case:22} {sym:14} {e:10.5g} {v:12.6g} {rd:10.2e} {tol:8.0e} {'PASS' if ok else 'FAIL'}")
    print("== Extra probes: product (hard-coded) vs independent ==")
    for label, ov, prod in EXTRAS_IN:
        ind = pressfit(**dict(STD, **ov))
        for sym, pv in prod.items():
            iv = ind[sym]
            rd = abs(pv - iv) / abs(iv) if iv else abs(pv)
            ok = rd <= 1e-5
            fails += not ok
            print(f"{label:28} {sym:14} product={pv:12.6g} indep={iv:12.6g} rd={rd:9.2e} {'PASS' if ok else 'FAIL'}")
    print("== Governing shaft stress for hollow shafts (finding 01, informational) ==")
    for label, ov, prod_vm in FINDING_ROWS:
        ind = pressfit(**dict(STD, **ov))
        ratio = ind["sigma_vm_shaft_bore"] / prod_vm
        print(f"{label:42} product shaft vM={prod_vm:8.2f} MPa  bore vM={ind['sigma_vm_shaft_bore']:8.2f} MPa  "
              f"bore/product={ratio:5.2f}  utilisation bore={ind['u_shaft_bore']:.3f} vs product={prod_vm / 350:.3f}  "
              f"{'FINDING: bore governs and yields' if ind['u_shaft_bore'] > 1 else 'FINDING: product understates'}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
