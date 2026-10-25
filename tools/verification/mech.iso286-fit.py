#!/usr/bin/env python3
"""Independent re-derivation of mech.iso286-fit (Track P, P1).

Source of the method: ISO 286-1 (2010) tolerance-unit and fundamental-deviation
formulae, written here from my own knowledge of the standard (no tables copied).
Units: D in mm, all deviations and IT values in micrometres.

  main size steps (lower, upper], D = sqrt(lower*upper), first step uses 1 and 3
  i = 0.45 D^(1/3) + 0.001 D   (D <= 500);   I = 0.004 D + 2.1   (D > 500)
  IT5..IT18 = 7,10,16,25,40,64,100,160,250,400,640,1000,1600,2500 times i
  IT01 = 0.3+0.008D, IT0 = 0.5+0.012D, IT1 = 0.8+0.02D; IT2..IT4 geometric between IT1 and IT5
  shaft: d -16D^.44, e -11D^.41, f -5.5D^.41, g -2.5D^.34, h 0, js +-IT/2,
         k +0.6 D^(1/3) (IT4..IT7 only), m IT7-IT6, n 5D^.34, p IT7 (+0..5 in the table),
         s IT7+0.4D (D>50), t IT7+0.63D, u IT7+D, r = sqrt(ei_p * ei_s)
  hole: A..H EI = -es;  K..N ES = -ei + delta (<= IT8);  P..U ES = -ei + delta (<= IT7);
        delta = ITn - IT(n-1)
  fit: c_max = ES - ei, c_min = EI - es, i_max = es - EI, i_min = ei - ES
"""
import math
import sys

STEPS = [3, 6, 10, 18, 30, 50, 80, 120, 180, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150]
MULT = {5: 7, 6: 10, 7: 16, 8: 25, 9: 40, 10: 64, 11: 100, 12: 160, 13: 250, 14: 400, 15: 640, 16: 1000, 17: 1600, 18: 2500}


def step_of(D):
    lo = 0.0
    for hi in STEPS:
        if D <= hi:
            return lo, hi
        lo = hi
    raise ValueError("D above 3150 mm")


def gmean(D):
    lo, hi = step_of(D)
    return math.sqrt(max(lo, 1.0) * hi)


def tol_unit(Dg):
    return 0.45 * Dg ** (1 / 3) + 0.001 * Dg if Dg <= 500 else 0.004 * Dg + 2.1


def IT(n, D):
    Dg = gmean(D)
    if Dg > 500:
        small = {1: 2.0, 2: 2.7, 3: 3.7, 4: 5.0}
        I = tol_unit(Dg)
        if n in small:
            return small[n] * I
    if n >= 5:
        return MULT[n] * tol_unit(Dg)
    it1 = 0.8 + 0.02 * Dg
    if n == 0:
        return 0.5 + 0.012 * Dg
    if n == -1:
        return 0.3 + 0.008 * Dg
    it5 = 7 * tol_unit(Dg)
    return it1 * (it5 / it1) ** ((n - 1) / 4.0)


def shaft_fd(letter, n, D):
    """Return (es, ei) in micrometres."""
    Dg = gmean(D)
    t = IT(n, D)
    if letter == "h":
        return 0.0, -t
    if letter == "js":
        return t / 2, -t / 2
    es_up = {"d": -16 * Dg ** 0.44, "e": -11 * Dg ** 0.41, "f": -5.5 * Dg ** 0.41, "g": -2.5 * Dg ** 0.34}
    if letter in es_up:
        return es_up[letter], es_up[letter] - t
    ei = ei_lower(letter, n, D)
    return ei + t, ei


def ei_lower(letter, n, D):
    Dg = gmean(D)
    if letter == "k":
        return 0.6 * Dg ** (1 / 3) if 4 <= n <= 7 else 0.0
    if letter == "m":
        return IT(7, D) - IT(6, D)
    if letter == "n":
        return 5 * Dg ** 0.34
    if letter == "p":
        return IT(7, D)
    if letter == "s":
        return IT(8, D) + 1 if D <= 50 else IT(7, D) + 0.4 * Dg
    if letter == "r":
        return math.sqrt(ei_lower("p", n, D) * ei_lower("s", n, D))
    if letter == "t":
        return IT(7, D) + 0.63 * Dg
    if letter == "u":
        return IT(7, D) + Dg
    raise KeyError(letter)


def hole_fd(letter, n, D):
    """Return (ES, EI) in micrometres."""
    t = IT(n, D)
    low = letter.lower()
    if letter == "JS":
        return t / 2, -t / 2
    if letter in "ABCDEFGH" and letter != "JS":
        es, _ = shaft_fd(low, n, D)
        return -es + t, -es
    delta = IT(n, D) - IT(n - 1, D)
    if letter in ("K", "M", "N"):
        ei = ei_lower(low, 5, D) if letter != "K" else 0.6 * gmean(D) ** (1 / 3)
        ES = (-ei + delta) if n <= 8 else (0.0 if letter in ("K", "N") else -ei)
    else:
        ei = ei_lower(low, n, D)
        ES = (-ei + delta) if n <= 7 else -ei
    return ES, ES - t


def fit(D, hl, hg, sl, sg, bore=None):
    """bore = (ES, EI) in µm replaces the hole deviations (rolling-bearing bore, ISO 492; product 1.1.0 hole_kind)."""
    ES, EI = bore if bore else hole_fd(hl, hg, D)
    es, ei = shaft_fd(sl, sg, D)
    out = dict(ES=ES, EI=EI, es=es, ei=ei, IT_hole=IT(hg, D), IT_shaft=IT(sg, D))
    out["c_max"] = ES - ei
    out["c_min"] = EI - es
    out["i_max"] = es - EI
    out["i_min"] = ei - ES
    out["fit"] = "clearance" if out["c_min"] >= 0 else ("interference" if out["c_max"] <= 0 else "transition")
    return out


KATS = [
    ("ka-h7g6-25", fit(25, "H", 7, "g", 6), [("IT_hole", 21, 0.03), ("IT_shaft", 13, 0.03), ("es", -7, 0.06), ("c_min", 7, 0.06), ("c_max", 41, 0.03)]),
    ("ka-h7p6-50", fit(50, "H", 7, "p", 6), [("IT_hole", 25, 0.03), ("ei", 26, 0.06), ("i_max", 42, 0.05), ("i_min", 0, 1.5)]),
    # New in 1.1.0: 35 mm bearing bore (ES 0, EI -9 µm, illustrative) on a k6 seat; tight tolerances, own formula derivation.
    ("ka-bearing-bore-k6", fit(35, "H", 7, "k", 6, bore=(0.0, -9.0)),
     [("ei", 2.030017, 1e-5), ("es", 17.642448, 1e-5), ("c_max", -2.030017, 1e-5), ("c_min", -26.642448, 1e-5), ("i_min", 2.030017, 1e-5)]),
]



# ---------------------------------------------------------------------------
# Extra probes. Product values were read from `Probe run mech.iso286-fit` (µm).
# Each entry: (D, hole letter, hole grade, shaft letter, shaft grade, product dict, finding note or "").
# A finding note means the product differs from the standard for a reason given in the report.
PROBES = [
    ((100, "H", 8, "f", 7), dict(ES=54.313297, EI=0.0, es=-36.035312, ei=-70.795823), ""),
    ((10, "H", 7, "k", 6), dict(ES=14.369873, EI=0.0, es=10.168332, ei=1.187161), ""),
    ((25, "K", 7, "h", 6), dict(ES=6.132068, EI=-14.785935, es=0.0, ei=-13.073752), ""),
    ((25, "N", 7, "h", 6), dict(ES=-6.726332, EI=-27.644336, es=0.0, ei=-13.073752), ""),
    ((25, "P", 7, "h", 6), dict(ES=-13.073752, EI=-33.991756, es=0.0, ei=-13.073752), ""),
    ((25, "JS", 7, "js", 6), dict(ES=10.459002, EI=-10.459002, es=6.536876, ei=-6.536876), ""),
    ((200, "H", 7, "s", 6), dict(ES=46.334693, EI=0.0, es=160.146691, ei=131.187507), ""),
    ((1000, "H", 7, "s", 6), dict(ES=90.84334, EI=0.0, es=505.391304, ei=448.614217), ""),
    ((3150, "H", 7, "g", 6), dict(ES=213.199555, EI=0.0, es=-37.179419, ei=-170.429141), ""),
    ((25, "H", 11, "d", 9), dict(ES=130.737524, EI=0.0, es=-63.862602, ei=-116.157612), ""),
    ((25, "E", 9, "h", 9), dict(ES=92.246539, EI=39.951529, es=0.0, ei=-52.29501), ""),
    ((3, "H", 7, "h", 6), dict(ES=8.674459, EI=0.0, es=0.0, ei=-5.421537), ""),
    ((25, "H", 3, "h", 3), dict(ES=3.402145, EI=0.0, es=0.0, ei=-3.402145), ""),
    ((600, "H", 1, "g", 2), dict(ES=8.689989, EI=0.0, es=-21.510614, ei=-33.242099), ""),
    ((25, "S", 7, "h", 6), dict(ES=-25.84013, EI=-46.758133, es=0.0, ei=-13.073752), ""),
    ((25, "U", 7, "h", 6), dict(ES=-36.311652, EI=-57.229656, es=0.0, ei=-13.073752), ""),
    ((450, "H", 7, "u", 6), dict(ES=62.215581, EI=0.0, es=548.313915, ei=509.429176), ""),
    ((25, "H", 7, "r", 6), dict(ES=20.918004, EI=0.0, es=39.618243, ei=26.544491), ""),
    ((25, "R", 7, "h", 6), dict(ES=-18.70024, EI=-39.618243, es=0.0, ei=-13.073752), ""),
    ((25, "F", 8, "h", 6), dict(ES=52.660145, EI=19.975765, es=0.0, ei=-13.073752), ""),
    # Findings: the product departs from the independent value.
    ((18, "H", 7, "h", 6), dict(ES=20.918004, EI=0.0, es=0.0, ei=-13.073752), "18 mm lands in step (18,30]: float 18.000000000000004 (finding 01)"),
    ((25, "K", 8, "h", 7), dict(ES=11.766377, EI=-20.918004, es=0.0, ei=-20.918004), "K8 drops the k deviation (finding 03)"),
    ((25, "N", 9, "h", 9), dict(ES=-14.570583, EI=-66.865593, es=0.0, ei=-52.29501), "N above IT8 should have ES = 0 (finding 02)"),
    ((28, "H", 7, "t", 6), dict(ES=20.918004, EI=0.0, es=46.758133, ei=33.684381), "t falls back to s when the step mean is <= 24 mm (finding 04)"),
]

# Bearing-bore mode (product 1.1.0), values from the Probe tool (µm): (D, shaft letter, grade, ES, EI, product dict).
BORE_PROBES = [
    (35, "p", 6, 0.0, -9.0, dict(ES=0.0, EI=-9.0, es=40.59231799, ei=24.97988799)),
    (25, "g", 6, 0.0, -8.0, dict(ES=0.0, EI=-8.0, es=-7.285291741, ei=-20.35904412)),
]

# Tabulated ISO 286-2 values as I know them (µm, values only). The standard rounds and uses finer
# sub-steps for r..zc above 50 mm, and p = IT7 + 0..5. Allowance: 1.5 µm.
# (label, D, quantity, table value, product value)
TABLE = [
    ("25 H7/g6 es", 25, -7, -7.285292),
    ("25 K7 ES", 25, 6, 6.132068),
    ("25 N7 ES", 25, -7, -6.726332),
    ("25 P7 ES", 25, -14, -13.073752),
    ("50 p6 ei", 50, 26, 24.979888),
    ("100 p6 ei (80-120)", 100, 37, 34.76051),
    ("200 p6 ei (180-250)", 200, 50, 46.334693),
    ("450 p6 ei (400-500)", 450, 68, 62.215581),
    ("565 p6 ei (500-630)", 565, 78, 69.519911),
    ("90 s6 ei (80-100)", 90, 71, 73.952346),
    ("110 s6 ei (100-120)", 110, 79, 73.952346),
    ("20 u6 ei (18-24)", 20, 41, 44.155904),
    ("28 u6 ei (24-30)", 28, 48, 44.155904),
    ("565 k6 ei (500-630)", 565, 0, 4.949215),
]
TABLE_ALLOW = 1.5


def main():
    fails = 0
    print("== Known-answer cases ==")
    print(f"{'case':14} {'symbol':8} {'KAT exp':>9} {'indep':>10} {'rel diff':>10} {'KAT tol':>8} result")
    for case, res, exp in KATS:
        for sym, e, tol in exp:
            v = res[sym]
            rd = abs(v - e) / abs(e) if e else abs(v)  # zero expected: absolute difference in µm
            ok = rd <= max(tol, 1e-6)
            fails += not ok
            print(f"{case:14} {sym:8} {e:9.4g} {v:10.5g} {rd:10.2e} {tol:8.2g} {'PASS' if ok else 'FAIL'}")
    print("== Extra probes: product (hard-coded) vs independent, µm ==")
    for (D, hl, hg, sl, sg), prod, note in PROBES:
        ind = fit(D, hl, hg, sl, sg)
        for sym in ("ES", "EI", "es", "ei"):
            a, b = prod[sym], ind[sym]
            diff = abs(a - b)
            ok = diff <= 1e-3
            tag = "PASS" if ok else ("FINDING" if note else "FAIL")
            if tag == "FAIL":
                fails += 1
            if tag != "PASS" or sym == "ES":
                print(f"{D:7g} {hl}{hg}/{sl}{sg} {sym:3} product={a:10.4f} indep={b:10.4f} diff={diff:8.4f} {tag} {note if tag == 'FINDING' else ''}")
    print("== Bearing-bore mode: product (hard-coded) vs independent, µm ==")
    for D, sl, sg, ES, EI, prod in BORE_PROBES:
        ind = fit(D, "H", 7, sl, sg, bore=(ES, EI))
        for sym in ("ES", "EI", "es", "ei"):
            diff = abs(prod[sym] - ind[sym])
            ok = diff <= 1e-3
            fails += not ok
            print(f"{D:7g} bore/{sl}{sg} {sym:3} product={prod[sym]:10.4f} indep={ind[sym]:10.4f} diff={diff:8.4f} {'PASS' if ok else 'FAIL'}")
    print("== Product vs tabulated values (allowance %.1f um); informational ==" % TABLE_ALLOW)
    for label, D, tab, prod in TABLE:
        d = prod - tab
        print(f"{label:24} table={tab:7.1f} product={prod:9.3f} diff={d:7.3f} {'ok' if abs(d) <= TABLE_ALLOW else 'FINDING (sub-step / p-increment / k)'}")
    print("== Step-boundary check: IT7 at the upper limit of each step ==")
    for D in STEPS:
        print(f"D={D:5d} IT7={IT(7, D):9.4f} (step {step_of(D)})") if D in (18, 30) else None
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
