#!/usr/bin/env python3
"""Independent re-derivation of elec.containment (Track P, P1).

Method sources (cited by title and number only):
  Fill: geometric area ratio. Cable area n*pi*d^2/4, conduit pi*D^2/4, trunking W*H.
  Bend radius: minimum radius = multiple of the largest cable overall diameter (cable maker's rule; BS 7671 reg. 522.8 for context).
  Pulling: straight horizontal run T2 = T1 + w g mu L (Coulomb friction on the containment floor);
           bend T2 = T1 exp(mu theta) (capstan / belt friction, Beer and Johnston, Vector Mechanics for Engineers: Statics, Ch. 8);
           sidewall pressure SWP = T2 / R (N/m of bend).
  w_c scales the weight on straights and the friction exponent on bends (product convention, spec section Method).
  Jam ratio J = 1.05 D / d for three equal cables in conduit; warn for 2.8 <= J <= 3.2 (cable-pulling guides).
The IET On-Site Guide cable-factor method is NOT implemented by the product (spec scope) and not here.
Exit 0 when every KAT value matches within max(KAT tolerance, 1e-6).
"""
import math
import sys

G = 9.80665


def route_tensions(route, t1, mu, w, wc):
    """Tension after each segment, W/m weight w kg/m. route: list of ('S', L_m) or ('B', theta_deg, R_mm)."""
    t = t1
    ts, ps = [], []
    for seg in route:
        if seg[0] == "S":
            t = t + w * G * wc * mu * seg[1]
            ps.append(None)
        else:
            th = math.radians(seg[1])
            t = t * math.exp(wc * mu * th)
            ps.append(t / (seg[2] / 1000.0))
        ts.append(t)
    return ts, ps


def containment(inp):
    o = {}
    cables = inp["cables"]  # (name, od_mm, count, kg/m, Tmax or None)
    acab = sum(n * math.pi * d * d / 4 for _, d, n, _, _ in cables)
    if inp["type"] == "conduit":
        aint = math.pi * inp["D"] ** 2 / 4
    else:
        aint = inp["W"] * inp["H"]
    fill = acab / aint
    o.update(A_cab=acab, A_int=aint, fill=fill * 100, u_fill=fill / inp["f_max"])
    dmax = max(c[1] for c in cables)
    o["OD_max"] = dmax
    wt = sum(n * m for _, _, n, m, _ in cables)
    o["w"] = wt
    ncab = sum(c[2] for c in cables)
    if inp.get("n_bend"):
        rmin = inp["n_bend"] * dmax
        o["R_min"] = rmin
        bends = [s for s in inp.get("route", []) if s[0] == "B"]
        if bends:
            o["u_bend"] = max(rmin / s[2] for s in bends)
    route = inp.get("route") or []
    if route:
        mu = inp["mu"]
        wc = inp.get("w_c") or 1.0
        t1 = inp.get("T_1", 0.0)
        ta, pa = route_tensions(route, t1, mu, wt, wc)
        rb = list(reversed(route))
        tb, pb = route_tensions(rb, t1, mu, wt, wc)
        o["T_max_a"], o["T_max_b"] = max(ta), max(tb)
        pam = max([p for p in pa if p is not None], default=0.0)
        pbm = max([p for p in pb if p is not None], default=0.0)
        if any(s[0] == "B" for s in route):
            o["P_max_a"], o["P_max_b"] = pam, pbm
        limits = [c[4] for c in cables if c[4]]
        kn = inp.get("k_n") or 1.0
        if ncab <= 3:
            kn = 1.0
        tcab = kn * ncab * min(limits) if limits else None

        def ok(tmax, pmax):
            good = True
            if tcab is not None:
                good &= tmax <= tcab
            if inp.get("T_grip"):
                good &= tmax <= inp["T_grip"]
            if inp.get("P_lim") and any(s[0] == "B" for s in route):
                good &= pmax <= inp["P_lim"]
            return good

        oka, okb = ok(o["T_max_a"], pam), ok(o["T_max_b"], pbm)
        if oka != okb:
            use_a = oka
        elif abs(o["T_max_a"] - o["T_max_b"]) > 1e-9 * max(o["T_max_a"], o["T_max_b"]):
            use_a = o["T_max_a"] < o["T_max_b"]
        else:
            use_a = pam <= pbm
        mt, pm = (o["T_max_a"], pam) if use_a else (o["T_max_b"], pbm)
        o["max_tension"] = mt
        if any(s[0] == "B" for s in route):
            o["P_max"] = pm
        if tcab is not None:
            o["T_cab"] = tcab
            o["u_T"] = mt / tcab
        if inp.get("T_grip"):
            o["u_grip"] = mt / inp["T_grip"]
        if inp.get("P_lim") and any(s[0] == "B" for s in route):
            o["u_P"] = pm / inp["P_lim"]
    return o


# Inputs of the six KATs (cable rows: name, od mm, count, mass kg/m, T_max N).
S, B = "S", "B"
INPUTS = {
    "ka-fill-3x10-in-25": dict(cables=[("Control", 10, 3, 0.2, 400)], type="conduit", D=25, f_max=0.5, n_bend=12,
                               route=[(S, 12), (B, 90, 400), (S, 18), (B, 90, 300), (S, 8)], mu=0.35, P_lim=400),
    "ka-pull-tray-two-bends": dict(cables=[("Power", 12, 2, 0.6, 700), ("Control", 8, 1, 0.15, 150)], type="trunking", W=100, H=50,
                                   f_max=0.5, n_bend=8, route=[(S, 20), (B, 90, 500), (S, 30), (B, 90, 600), (S, 10)], T_1=100, mu=0.4, P_lim=2000),
    "ka-direction-passes-every-check": dict(cables=[("Single", 20, 1, 1.0, 5000)], type="conduit", D=100, f_max=0.5, n_bend=6,
                                            route=[(S, 20), (B, 90, 1200), (S, 40), (B, 180, 600), (S, 40)], T_1=50, mu=0.35, P_lim=1500),
    "ka-capstan-180": dict(cables=[("Single", 20, 1, 0.3, 1000)], type="conduit", D=50, f_max=0.5, n_bend=6, route=[(B, 180, 400)], T_1=200, mu=0.35),
    "ka-trunking-fill": dict(cables=[("Data", 8, 12, 0.07, 100)], type="trunking", W=100, H=50, f_max=0.42),
    "ka-bend-too-tight": dict(cables=[("Control", 10, 3, 0.2, 400)], type="conduit", D=32, f_max=0.5, n_bend=12, route=[(S, 5), (B, 90, 100)], mu=0.3),
}

KATS = {
 "ka-fill-3x10-in-25": dict(secondary=False, expected=[
   ('A_cab', 235.619449, 'mm^2', 1e-09),
   ('A_int', 490.8738521, 'mm^2', 1e-09),
   ('fill', 48, '%', 1e-09),
   ('u_fill', 0.96, '', 1e-09),
   ('R_min', 120, 'mm', 1e-09),
   ('u_bend', 0.4, '', 1e-09),
   ('w', 0.6, 'kg/m', 1e-09),
   ('T_max_a', 154.9195277, 'N', 1e-09),
   ('T_max_b', 138.4209876, 'N', 1e-09),
   ('max_tension', 138.4209876, 'N', 1e-09),
   ('P_max_a', 461.4811856, 'N/m', 1e-09),
   ('P_max', 284.2705741, 'N/m', 1e-09),
   ('T_cab', 1200, 'N', 1e-09),
   ('u_T', 0.115350823, '', 1e-09),
   ('u_P', 0.7106764352, '', 1e-09),
 ]),
 "ka-pull-tray-two-bends": dict(secondary=False, expected=[
   ('A_cab', 276.4601535, 'mm^2', 1e-09),
   ('fill', 5.529203071, '%', 1e-09),
   ('w', 1.35, 'kg/m', 1e-09),
   ('T_max_a', 1074.235304, 'N', 1e-09),
   ('T_max_b', 941.1260901, 'N', 1e-09),
   ('max_tension', 941.1260901, 'N', 1e-09),
   ('P_max_a', 1702.132324, 'N/m', 1e-09),
   ('P_max_b', 1670.42854, 'N/m', 1e-09),
   ('T_cab', 450, 'N', 1e-09),
   ('u_T', 2.091391311, '', 1e-09),
   ('R_min', 96, 'mm', 1e-09),
 ]),
 "ka-direction-passes-every-check": dict(secondary=False, expected=[
   ('T_max_a', 1166.942072, 'N', 1e-09),
   ('T_max_b', 1281.141772, 'N', 1e-09),
   ('max_tension', 1281.141772, 'N', 1e-09),
   ('P_max_a', 1716.08162, 'N/m', 1e-09),
   ('P_max', 1010.412685, 'N/m', 1e-09),
   ('T_cab', 5000, 'N', 1e-09),
   ('u_T', 0.2562283544, '', 1e-09),
   ('u_P', 0.6736084567, '', 1e-09),
 ]),
 "ka-capstan-180": dict(secondary=False, expected=[
   ('T_max_a', 600.5673521, 'N', 1e-09),
   ('T_max_b', 600.5673521, 'N', 1e-09),
   ('P_max', 1501.41838, 'N/m', 1e-09),
   ('u_bend', 0.3, '', 1e-09),
 ]),
 "ka-trunking-fill": dict(secondary=False, expected=[
   ('A_cab', 603.1857895, 'mm^2', 1e-09),
   ('A_int', 5000, 'mm^2', 1e-09),
   ('fill', 12.06371579, '%', 1e-09),
   ('u_fill', 0.2872313283, '', 1e-09),
 ]),
 "ka-bend-too-tight": dict(secondary=False, expected=[
   ('R_min', 120, 'mm', 1e-09),
   ('u_bend', 1.2, '', 1e-09),
 ]),
}
# Extra probes: name, model inputs, product values observed with tools/verification/probe (run, 2026-10-25).
# Cable rows: (name, od mm, count, kg/m, T_max N or None). Route: ('S', m) or ('B', deg, R mm). Z3 has a cable with no T_max: the product drops the cable limit, so T_cab and u_T are not listed.
# Z11 is a unit probe: 0.5 in cables, 1.5 in conduit, lb/ft, 100 ft, lbf, 12 in bend, converted to SI here.
EXTRAS = [
    ('Z1 fill only: 4 x 6 mm + 2 x 9 mm in 32 mm conduit, f 0.4',
     {'cables': [('A', 6, 4, 0.05, None), ('B', 9, 2, 0.12, None)], 'type': 'conduit', 'D': 32, 'f_max': 0.4},
     {'A_cab': 240.331838, 'A_int': 804.2477193, 'fill': 29.8828125, 'u_fill': 0.7470703125, 'OD_max': 9.0, 'w': 0.44}),
    ('Z2 five cables, route starts with a bend, T1 0, w_c 1.5, grip 2500, k_n 0.8, P_lim 3000',
     {'cables': [('P', 16, 5, 0.45, 600)], 'type': 'conduit', 'D': 75, 'f_max': 0.4, 'n_bend': 10, 'route': [('B', 90, 600), ('S', 25), ('B', 45, 900), ('S', 40), ('B', 90, 600), ('S', 15)], 'T_1': 0, 'mu': 0.3, 'w_c': 1.5, 'T_grip': 2500, 'k_n': 0.8, 'P_lim': 3000},
     {'A_cab': 1005.309649, 'A_int': 4417.864669, 'fill': 22.75555556, 'u_fill': 0.5688888889, 'OD_max': 16.0, 'w': 2.25, 'R_min': 160.0, 'u_bend': 0.2666666667, 'T_max_a': 1670.93576, 'T_max_b': 2521.927268, 'P_max_a': 2536.662106, 'P_max_b': 4203.212114, 'max_tension': 1670.93576, 'P_max': 2536.662106, 'T_cab': 2400.0, 'u_T': 0.6962232334, 'u_grip': 0.6683743041, 'u_P': 0.8455540352}),
    ('Z3 trunking, one cable without T_max, bends, T1 150',
     {'cables': [('P', 20, 3, 0.8, 1000), ('C', 10, 2, 0.1, None)], 'type': 'trunking', 'W': 150, 'H': 75, 'f_max': 0.45, 'n_bend': 8, 'route': [('S', 30), ('B', 90, 800), ('S', 20)], 'T_1': 150, 'mu': 0.25, 'P_lim': 2500},
     {'A_cab': 1099.557429, 'A_int': 11250.0, 'fill': 9.773843811, 'u_fill': 0.2171965291, 'OD_max': 20.0, 'w': 2.6, 'R_min': 160.0, 'u_bend': 0.2, 'T_max_a': 632.838273, 'T_max_b': 602.1795239, 'P_max_a': 631.6897788, 'P_max_b': 513.6873111, 'max_tension': 602.1795239, 'P_max': 513.6873111, 'u_P': 0.2054749244}),
    ('Z4 near zero: mu 0, T1 75 N, 2 bends',
     {'cables': [('S', 25, 1, 1.2, 2000)], 'type': 'conduit', 'D': 50, 'f_max': 0.4, 'n_bend': 6, 'route': [('S', 50), ('B', 90, 500), ('S', 10), ('B', 90, 500)], 'T_1': 75, 'mu': 0.0, 'P_lim': 1000},
     {'A_cab': 490.8738521, 'A_int': 1963.495408, 'fill': 25.0, 'u_fill': 0.625, 'OD_max': 25.0, 'w': 1.2, 'R_min': 150.0, 'u_bend': 0.3, 'T_max_a': 75.0, 'T_max_b': 75.0, 'P_max_a': 150.0, 'P_max_b': 150.0, 'max_tension': 75.0, 'P_max': 150.0, 'T_cab': 2000.0, 'u_T': 0.0375, 'u_P': 0.15}),
    ('Z6a jam: three 10 mm cables in 30 mm conduit (J = 3.15)',
     {'cables': [('J', 10, 3, 0.2, 300)], 'type': 'conduit', 'D': 30, 'f_max': 0.4},
     {'A_cab': 235.619449, 'A_int': 706.8583471, 'fill': 33.33333333, 'u_fill': 0.8333333333, 'OD_max': 10.0, 'w': 0.6}),
    ('Z8 symmetric route, either direction',
     {'cables': [('S', 15, 1, 0.5, 800)], 'type': 'conduit', 'D': 40, 'f_max': 0.4, 'n_bend': 8, 'route': [('S', 10), ('B', 90, 500), ('S', 20), ('B', 90, 500), ('S', 10)], 'T_1': 0, 'mu': 0.3},
     {'A_cab': 176.7145868, 'A_int': 1256.637061, 'fill': 14.0625, 'u_fill': 0.3515625, 'OD_max': 15.0, 'w': 0.5, 'R_min': 120.0, 'u_bend': 0.24, 'T_max_a': 99.59076278, 'T_max_b': 99.59076278, 'P_max_a': 169.7615756, 'P_max_b': 169.7615756, 'max_tension': 99.59076278, 'P_max': 169.7615756, 'T_cab': 800.0, 'u_T': 0.1244884535}),
    ('Z10 neither direction passes: low limits',
     {'cables': [('S', 15, 2, 0.5, 100)], 'type': 'conduit', 'D': 40, 'f_max': 0.4, 'n_bend': 8, 'route': [('S', 60), ('B', 90, 300), ('S', 5), ('B', 90, 300), ('S', 5)], 'T_1': 20, 'mu': 0.4, 'P_lim': 300},
     {'A_cab': 353.4291735, 'A_int': 1256.637061, 'fill': 28.125, 'u_fill': 0.703125, 'OD_max': 15.0, 'w': 1.0, 'R_min': 120.0, 'u_bend': 0.4, 'T_max_a': 953.6053892, 'T_max_b': 411.308591, 'P_max_a': 3113.306964, 'P_max_b': 586.4966366, 'max_tension': 411.308591, 'P_max': 586.4966366, 'T_cab': 200.0, 'u_T': 2.056542955, 'u_P': 1.954988789}),
    ('Z13 large conduit 300 mm, 40 cables in 80 mm trunking row',
     {'cables': [('L', 12, 40, 0.15, 300)], 'type': 'trunking', 'W': 300, 'H': 100, 'f_max': 0.45, 'n_bend': 6, 'route': [('S', 80), ('B', 90, 1000), ('S', 60)], 'T_1': 0, 'mu': 0.35, 'k_n': 0.7, 'T_grip': 5000},
     {'A_cab': 4523.893421, 'A_int': 30000.0, 'fill': 15.07964474, 'u_fill': 0.3351032164, 'OD_max': 12.0, 'w': 6.0, 'R_min': 72.0, 'u_bend': 0.072, 'T_max_a': 4090.570234, 'T_max_b': 3788.71645, 'P_max_a': 2854.932334, 'P_max_b': 2141.19925, 'max_tension': 3788.71645, 'P_max': 2141.19925, 'T_cab': 8400.0, 'u_T': 0.4510376726, 'u_grip': 0.75774329}),
    ('Z12 very large: 500 m + 720 deg bend R 500 mm + 500 m, mu 0.5, T1 100 N',
     {'cables': [('S', 15, 1, 0.5, 1000000.0)], 'type': 'conduit', 'D': 40, 'f_max': 0.4, 'n_bend': 8, 'route': [('S', 500), ('B', 720, 500), ('S', 500)], 'T_1': 100, 'mu': 0.5},
     {'A_cab': 176.7145868, 'A_int': 1256.637061, 'fill': 14.0625, 'u_fill': 0.3515625, 'OD_max': 15.0, 'w': 0.5, 'R_min': 120.0, 'u_bend': 0.24, 'T_max_a': 711197.4023, 'T_max_b': 711197.4023, 'P_max_a': 1419943.142, 'P_max_b': 1419943.142, 'max_tension': 711197.4023, 'P_max': 1419943.142, 'T_cab': 1000000.0, 'u_T': 0.7111974023}),
    ('Z11 imperial units',
     {'cables': [('A', 12.7, 4, 0.29763278, 444.82216)], 'type': 'conduit', 'D': 38.1, 'f_max': 0.4, 'n_bend': 10, 'route': [('S', 30.48), ('B', 90, 304.8)], 'T_1': 44.482216, 'mu': 0.3},
     {'A_cab': 506.7074791, 'A_int': 1140.091828, 'fill': 44.44444444, 'u_fill': 1.111111111, 'OD_max': 12.7, 'w': 1.190531155, 'R_min': 127.0, 'u_bend': 0.4166666667, 'T_max_a': 242.2823549, 'T_max_b': 178.0168349, 'P_max_a': 794.8896159, 'P_max_b': 233.7910635, 'max_tension': 178.0168349, 'P_max': 233.7910635, 'T_cab': 1779.288646, 'u_T': 0.1000494413}),
]


def rel(a, b):
    return abs(a - b) / max(abs(b), 1e-300) if b != 0 else abs(a - b)


def rifenburg_bend(t_in, mu, theta_deg, w_n_per_m, r_m):
    """Horizontal-plane bend with the cable weight on the duct floor (Rifenburg, 1950; cable-pulling guides, e.g. IEEE 1185 Annex).
    T_out = T_in cosh(mu th) + sqrt(T_in^2 + (w R)^2) sinh(mu th). Informational only: the product uses the capstan form."""
    x = mu * math.radians(theta_deg)
    return t_in * math.cosh(x) + math.sqrt(t_in ** 2 + (w_n_per_m * r_m) ** 2) * math.sinh(x)


def info_section():
    print("\nInformational comparison (not part of the exit status): capstan bend against a bend with the weight term")
    cases = [
        ("Z14 heavy MV cable, 10 kg/m, 90 deg bend R 3 m, T1 100 N, mu 0.3", 100.0, 0.3, 90, 10.0, 3.0, 160.1977651),
        ("Z15 same, T1 0 N (product keeps 0 N)", 0.0, 0.3, 90, 10.0, 3.0, 0.0),
        ("Z16 3-cable bundle 1.35 kg/m, 90 deg R 0.5 m, T1 100 N, mu 0.4", 100.0, 0.4, 90, 1.35, 0.5, 187.4456088),
    ]
    for name, t1, mu, th, w, r, prod in cases:
        cap = t1 * math.exp(mu * math.radians(th))
        rif = rifenburg_bend(t1, mu, th, w * G, r)
        print("%-70s capstan %10.4f  product %s  with weight term %10.4f  (%+.1f %%)" % (
            name, cap, "%10.4f" % prod if prod is not None else "     n/a  ", rif, (rif / cap - 1) * 100 if cap else float("inf")))


def main():
    fails = 0
    n = 0
    print("%-26s %-12s %-16s %-16s %-9s %-8s %s" % ("case", "symbol", "KAT expected", "independent", "rel.diff", "KAT tol", "result"))
    for name, k in KATS.items():
        o = containment(INPUTS[name])
        for sym, exp, unit, tol in k["expected"]:
            got = o.get(sym)
            if got is None:
                print("%-26s %-12s %-16s %-16s %-9s %-8s %s" % (name, sym, exp, "n/a", "-", tol, "FAIL (no independent value)"))
                fails += 1
                n += 1
                continue
            r = rel(got, exp)
            ok = r <= max(tol, 1e-6)
            n += 1
            fails += 0 if ok else 1
            print("%-26s %-12s %-16.10g %-16.10g %-9.2e %-8.0e %s" % (name, sym, exp, got, r, tol, "PASS" if ok else "FAIL"))
    print("\nExtra checks (product values hard-coded from the probe tool, elec.containment 1.1.0)")
    for name, inp, prod in EXTRAS:
        o = containment(inp)
        for sym, pv in prod.items():
            got = o.get(sym)
            r = rel(got, pv) if got is not None else float("inf")
            ok = r <= 1e-6
            n += 1
            fails += 0 if ok else 1
            print("%-26s %-12s %-16.10g %-16.10g %-9.2e %-8s %s" % (name.split()[0], sym, pv, got if got is not None else float("nan"), r, "1e-6", "PASS" if ok else "FAIL"))
    info_section()
    print("\n%d values compared, %d failed" % (n, fails))
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
