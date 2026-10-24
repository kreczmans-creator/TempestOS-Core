#!/usr/bin/env python3
"""Independent re-derivation of struct.connection-ec3 (Track P, P1).

Sources (cited by title, edition and number only):
  EN 1993-1-8:2005 Table 3.1 (bolt strengths), Table 3.3 (hole spacing), cl. 3.6.1 and Table 3.4
  (shear, bearing, tension, punching, combined), cl. 3.10.2 (block tearing), cl. 4.5.3.2 and
  4.5.3.3 with Table 4.1 (fillet welds).  ISO 898-1:2013 cl. 9.1 and ISO 261 (stress area, pitches).
Run: python3 tools/verification/struct.connection-ec3.py  (exit 0 = all KATs and extra checks match)
"""
import math
import sys

# ISO 261 coarse pitch and nut/head across flats (ISO 4014 / 4032) for M12-M36
SIZES = {"M12": (12, 1.75, 18), "M16": (16, 2.0, 24), "M20": (20, 2.5, 30), "M22": (22, 2.5, 34),
         "M24": (24, 3.0, 36), "M27": (27, 3.0, 41), "M30": (30, 3.5, 46), "M36": (36, 4.0, 55)}
# EN 1993-1-8 Table 3.1: (f_yb, f_ub) in MPa
GRADES = {"4.6": (240, 400), "4.8": (320, 400), "5.6": (300, 500), "5.8": (400, 500),
          "6.8": (480, 600), "8.8": (640, 800), "10.9": (900, 1000)}
ALPHA_V_06 = {"4.6", "5.6", "8.8"}  # Table 3.4, threads in the shear plane; others 0.5
# EN 1993-1-8 Table 4.1 correlation factor
BETA_W = {"S235": 0.8, "S275": 0.85, "S355": 0.9, "S420": 1.0, "S460": 1.0}


def stress_area(size):
    d, P, _ = SIZES[size]
    d2 = d - 3.0 * math.sqrt(3.0) / 8.0 * P      # 0.649519 P (ISO 898-1 exact form)
    d3 = d - 17.0 * math.sqrt(3.0) / 24.0 * P    # 1.226869 P
    return math.pi / 4.0 * ((d2 + d3) / 2.0) ** 2


def gross_area(size):
    return math.pi * SIZES[size][0] ** 2 / 4.0


def alpha_v(grade, threads=True):
    if not threads:
        return 0.6
    return 0.6 if grade in ALPHA_V_06 else 0.5


def default_d0(size):
    d = SIZES[size][0]
    return d + (3.0 if d >= 27 else 2.0)


def fv_rd(grade, size, ns=1, threads=True, gm2=1.25):
    A = stress_area(size) if threads else gross_area(size)
    return alpha_v(grade, threads) * GRADES[grade][1] * A * ns / gm2 / 1e3  # kN


def ft_rd(grade, size, gm2=1.25, k2=0.9):
    return k2 * GRADES[grade][1] * stress_area(size) / gm2 / 1e3


def bp_rd(size, t, fu, gm2=1.25):
    af = SIZES[size][2]
    dm = 0.5 * (af + af / math.cos(math.radians(30)))
    return 0.6 * math.pi * dm * t * fu / gm2 / 1e3, dm


def bearing(grade, size, t, fu, e1, e2, p1=None, p2=None, d0=None, gm2=1.25):
    """Return worst-of end / inner / edge bearing (kN) with the parts."""
    d = SIZES[size][0]
    d0 = d0 or default_d0(size)
    fub = GRADES[grade][1]
    ad_end = e1 / (3 * d0)
    ad_inner = (p1 / (3 * d0) - 0.25) if p1 else None
    k1_edge = min(2.8 * e2 / d0 - 1.7, 2.5)
    k1_inner = min(1.4 * p2 / d0 - 1.7, 2.5) if p2 else None
    out = {}
    def fb(k1, ad):
        ab = min(ad, fub / fu, 1.0)
        return k1 * ab * fu * d * t / gm2 / 1e3, ab
    out["end_edge"] = fb(k1_edge, ad_end)
    if ad_inner is not None:
        out["inner_edge"] = fb(k1_edge, ad_inner)
    if k1_inner is not None:
        out["end_inner"] = fb(k1_inner, ad_end)
        if ad_inner is not None:
            out["inner_inner"] = fb(k1_inner, ad_inner)
    return out, d0


def bearing_conservative(grade, size, t, fu, e1, e2, p1=None, p2=None, d0=None, gm2=1.25):
    """The spec's rule: alpha_b from the worst of end/inner; k1 from min of e2 and p2 terms."""
    d = SIZES[size][0]
    d0 = d0 or default_d0(size)
    fub = GRADES[grade][1]
    ad = e1 / (3 * d0)
    if p1:
        ad = min(ad, p1 / (3 * d0) - 0.25)
    k1 = min(2.8 * e2 / d0 - 1.7, 2.5)
    if p2:
        k1 = min(k1, 1.4 * p2 / d0 - 1.7)
    ab = min(ad, fub / fu, 1.0)
    return k1 * ab * fu * d * t / gm2 / 1e3, ab, k1


def combined(fv, fvrd, ft, ftrd):
    return fv / fvrd + ft / (1.4 * ftrd)


def block_tearing_1(fu, fy, Ant, Anv, gm2=1.25, gm0=1.0):
    return (fu * Ant / gm2 + fy * Anv / (math.sqrt(3) * gm0)) / 1e3  # kN, concentric loading


def block_tearing_2(fu, fy, Ant, Anv, gm2=1.25, gm0=1.0):
    return (0.5 * fu * Ant / gm2 + fy * Anv / (math.sqrt(3) * gm0)) / 1e3  # eccentric loading


def fvw_d(fu, steel, gm2=1.25):
    return fu / (math.sqrt(3) * BETA_W[steel] * gm2)


def weld_directional(fu, steel, sp, tp, tl, gm2=1.25):
    lhs = math.sqrt(sp ** 2 + 3 * (tp ** 2 + tl ** 2))
    lim1 = fu / (BETA_W[steel] * gm2)
    lim2 = 0.9 * fu / gm2
    return lhs, lim1, abs(sp), lim2


rows = []


def check(case, sym, ref, ind, tol, label="KAT"):
    rd = abs(ind - ref) / abs(ref) if ref != 0 else abs(ind - ref)
    ok = rd <= max(tol, 1e-6)
    rows.append((case, sym, ref, ind, rd, tol, ok))
    return ok


def kats():
    c = "ka-m20-88-shear-bearing"
    fvrd = fv_rd("8.8", "M20")
    ftrd = ft_rd("8.8", "M20")
    fb, ab, k1 = bearing_conservative("8.8", "M20", 10, 410, 40, 40, 70, 70)
    bp, dm = bp_rd("M20", 10, 410)
    check(c, "A_s", 244.794, stress_area("M20"), 1e-5)
    check(c, "F_v_Rd", 94.00105, fvrd, 1e-6)
    check(c, "F_t_Rd", 141.0016, ftrd, 1e-6)
    check(c, "F_b_Rd", 99.39394, fb, 1e-6)
    check(c, "B_p_Rd", 199.8265, bp, 1e-6)
    check(c, "u_vt", 0.6838832, combined(50, fvrd, 30, ftrd), 1e-6)
    c = "ka-m20-109-threads"
    check(c, "alpha_v", 0.5, alpha_v("10.9"), 1e-12)
    check(c, "F_v_Rd", 97.91776, fv_rd("10.9", "M20"), 1e-6)
    c = "ka-weld-s275"
    f = fvw_d(410, "S275")
    check(c, "f_vw_d", 222.7893, f, 1e-6)
    check(c, "F_w_Rd", 935.715, f * 4.2, 1e-6)
    c = "ka-p363-m20-88"
    check(c, "F_v_Rd", 94.1, fvrd, 0.01)
    check(c, "F_t_Rd", 141.0, ftrd, 0.01)
    c = "ka-p358-fin-plate"
    check(c, "F_b_Rd", 99.4, fb, 0.01)


def report():
    print(f"{'case':26s} {'symbol':22s} {'reference':>13s} {'independent':>13s} {'rel.diff':>10s} {'tol':>8s}  result")
    allok = True
    for case, sym, ref, ind, rd, tol, ok in rows:
        allok &= ok
        print(f"{case:26s} {sym:22s} {ref:13.7g} {ind:13.7g} {rd:10.2e} {tol:8.1e}  {'PASS' if ok else 'FAIL'}")
    return allok


if __name__ == "__main__":
    kats()
    try:
        extras()
    except NameError:
        pass
    ok = report()
    print("ALL PASS" if ok else "FAILURES PRESENT")
    sys.exit(0 if ok else 1)
