#!/usr/bin/env python3
"""Independent re-derivation of struct.connection-ec3 (Track P, P1).

Sources (cited by title, edition and number only):
  EN 1993-1-8:2005 Table 3.1 (bolt strengths), Table 3.3 (hole spacing), cl. 3.6.1 and Table 3.4
  (shear, bearing, tension, punching, combined), cl. 3.10.2 (block tearing), cl. 4.5.3.2 and
  4.5.3.3 with Table 4.1 (fillet welds).  ISO 898-1:2013 cl. 9.1 and ISO 261 (stress area, pitches).
Run: python3 tools/verification/struct.connection-ec3.py
Exit code: 0 when every known-answer case matches within its tolerance, else 1.
Extra checks print PASS or FAIL.  A FAIL naming a finding is a product finding, not a script error,
and does not change the exit code.
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



# ------------------------------------------------------------ full probes ---
def solve(i):
    """Independent solution of one input set (units: mm, kN, MPa, N/mm)."""
    size, grade = i["size"], i["grade"]
    threads = bool(i.get("threads", 1))
    ns = i.get("n_s", 1)
    gm2, gm0 = i.get("gamma_M2", 1.25), i.get("gamma_M0", 1.0)
    t, fu, fy = i["t"], i["f_u"], i["f_y"]
    d0 = i.get("d_0") or default_d0(size)
    o = {}
    o["A_s"] = stress_area(size)
    o["alpha_v"] = alpha_v(grade, threads)
    o["F_v_Rd"] = fv_rd(grade, size, ns, threads, gm2)
    fb, ab, k1 = bearing_conservative(grade, size, t, fu, i["e_1"], i["e_2"], i.get("p_1"), i.get("p_2"), d0, gm2)
    o["F_b_Rd"], o["alpha_b"], o["k_1"] = fb, ab, k1
    o["F_t_Rd"] = ft_rd(grade, size, gm2)
    o["B_p_Rd"] = bp_rd(size, t, fu, gm2)[0]
    fvd, ftd = i.get("F_v_Ed", 0.0), i.get("F_t_Ed", 0.0)
    o["u_v"] = fvd / o["F_v_Rd"]
    o["u_b"] = fvd / fb if fb > 0 else math.inf
    o["u_t"] = ftd / min(o["F_t_Rd"], o["B_p_Rd"])
    o["u_vt"] = combined(fvd, o["F_v_Rd"], ftd, o["F_t_Rd"])
    if "V_Ed" in i:
        o["V_eff_Rd"] = block_tearing_1(fu, fy, i["A_nt"], i["A_nv"], gm2, gm0)
        o["u_block"] = i["V_Ed"] / o["V_eff_Rd"]
    if "a" in i and "F_w_Ed" in i:
        f = fvw_d(fu, i["steel"], gm2)
        o["f_vw_d"], o["F_w_Rd"] = f, f * i["a"]
        o["u_w"] = i["F_w_Ed"] / o["F_w_Rd"]
    if "sigma_perp" in i:
        lhs, lim1, sp, lim2 = weld_directional(fu, i["steel"], i["sigma_perp"], i.get("tau_perp", 0.0), i.get("tau_par", 0.0), gm2)
        o["u_dir"], o["u_perp"] = lhs / lim1, sp / lim2
    return o


PROBES = {
    'P1 M16 4.6 shank, 2 planes, S235': ({'grade': '4.6', 'size': 'M16', 'threads': 0, 'n_s': 2, 'steel': 'S235', 't': 8, 'f_y': 235, 'f_u': 360, 'e_1': 35, 'e_2': 30, 'F_v_Ed': 40},
        {'A_s': 156.66841, 'alpha_v': 0.6, 'F_v_Rd': 77.207781, 'alpha_b': 0.64814815, 'k_1': 2.5, 'F_b_Rd': 59.733333, 'F_t_Rd': 45.120502, 'B_p_Rd': 112.29276, 'u_v': 0.5180825, 'u_b': 0.66964286, 'u_t': 0.0, 'u_vt': 0.5180825}),
    'P2 M24 10.9 threads, S355, inner-bolt alpha_d governs': ({'grade': '10.9', 'size': 'M24', 'steel': 'S355', 't': 16, 'f_y': 355, 'f_u': 490, 'e_1': 60, 'e_2': 45, 'p_1': 65, 'p_2': 80, 'F_v_Ed': 150, 'F_t_Ed': 60},
        {'A_s': 352.50392, 'alpha_v': 0.5, 'F_v_Rd': 141.00157, 'alpha_b': 0.58333333, 'k_1': 2.5, 'F_b_Rd': 219.52, 'F_t_Rd': 253.80283, 'B_p_Rd': 458.52878, 'u_v': 1.0638179, 'u_b': 0.68330904, 'u_t': 0.23640399, 'u_vt': 1.2326779}),
    'P3 M12 8.8 default hole, 3 mm plate (punching governs tension)': ({'grade': '8.8', 'size': 'M12', 'steel': 'S275', 't': 3, 'f_y': 275, 'f_u': 410, 'e_1': 25, 'e_2': 25, 'F_v_Ed': 10, 'F_t_Ed': 30},
        {'A_s': 84.266538, 'alpha_v': 0.6, 'F_v_Rd': 32.358351, 'alpha_b': 0.5952381, 'k_1': 2.5, 'F_b_Rd': 17.571429, 'F_t_Rd': 48.537526, 'B_p_Rd': 35.968775, 'u_v': 0.30903924, 'u_b': 0.56910569, 'u_t': 0.83405675, 'u_vt': 0.75052386}),
    'P4 M36 5.6, hole d+3, S460, large plate': ({'grade': '5.6', 'size': 'M36', 'steel': 'S460', 't': 40, 'f_y': 460, 'f_u': 550, 'e_1': 120, 'e_2': 100, 'p_1': 130, 'p_2': 130, 'F_v_Ed': 300, 'F_t_Ed': 100},
        {'A_s': 816.72256, 'alpha_v': 0.6, 'F_v_Rd': 196.01341, 'alpha_b': 0.86111111, 'k_1': 2.5, 'F_b_Rd': 1364.0, 'F_t_Rd': 294.02012, 'B_p_Rd': 1965.7732, 'u_v': 1.5305075, 'u_b': 0.21994135, 'u_t': 0.34011278, 'u_vt': 1.7734452}),
    'P5 M20 4.6 in S355 (f_ub/f_u limits alpha_b)': ({'grade': '4.6', 'size': 'M20', 'steel': 'S355', 't': 20, 'f_y': 355, 'f_u': 510, 'e_1': 100, 'e_2': 60, 'F_v_Ed': 40},
        {'A_s': 244.79439, 'alpha_v': 0.6, 'F_v_Rd': 47.000523, 'alpha_b': 0.78431373, 'k_1': 2.5, 'F_b_Rd': 320.0, 'F_t_Rd': 70.500785, 'B_p_Rd': 497.12942, 'u_v': 0.85105436, 'u_t': 0.0, 'u_vt': 0.85105436}),
    'P6 M27 8.8, hole 33 entered, k_1 from e2 and p2': ({'grade': '8.8', 'size': 'M27', 'd_0': 33, 'steel': 'S275', 't': 12, 'f_y': 275, 'f_u': 410, 'e_1': 50, 'e_2': 45, 'p_1': 80, 'p_2': 80, 'F_v_Ed': 100},
        {'A_s': 459.40644, 'alpha_v': 0.6, 'F_v_Rd': 176.41207, 'alpha_b': 0.50505051, 'k_1': 1.6939394, 'F_b_Rd': 90.918347, 'F_t_Rd': 264.61811, 'B_p_Rd': 327.71551, 'u_v': 0.56685463, 'u_b': 1.099888, 'u_t': 0.0, 'u_vt': 0.56685463}),
    'P8 weld S355: simplified and directional (sigma_perp < 0)': ({'grade': '8.8', 'size': 'M20', 'steel': 'S355', 't': 10, 'f_y': 355, 'f_u': 490, 'e_1': 40, 'e_2': 40, 'a': 5, 'F_w_Ed': 800, 'sigma_perp': -150, 'tau_perp': 90, 'tau_par': 60},
        {'f_vw_d': 251.46812, 'F_w_Rd': 1257.3406, 'u_w': 0.63626356, 'u_dir': 0.55102041, 'u_perp': 0.42517007}),
    'P9 block tearing, concentric': ({'grade': '8.8', 'size': 'M20', 'steel': 'S275', 't': 10, 'f_y': 275, 'f_u': 410, 'e_1': 40, 'e_2': 40, 'V_Ed': 200, 'A_nt': 300, 'A_nv': 900},
        {'V_eff_Rd': 241.29419, 'u_block': 0.82886371}),
}

# alpha_v by class, threads in the plane / shank in the plane, M20 (product Fv_Rd in kN)
GRADE_TABLE = {"4.6": (47.000523, 60.318579), "4.8": (39.167103, 60.318579), "5.6": (58.750654, 75.398224),
               "5.8": (48.958878, 75.398224), "6.8": (58.750654, 90.477868), "8.8": (94.001046, 120.637158),
               "10.9": (97.917757, 150.796447)}
# M12 ... M36, 8.8, threads: product A_s, F_v_Rd, F_t_Rd, B_p_Rd  (t = 12 mm, S275 f_u 410)
SIZE_TABLE = {"M12": (84.26653836, 32.35835073, 48.5375261, 143.8751013), "M16": (156.6684107, 60.16066971, 90.24100457, 191.8334684),
              "M20": (244.7943917, 94.00104643, 141.0015696, 239.7918355), "M22": (303.399279, 116.5053231, 174.7579847, 271.7640803),
              "M24": (352.5039241, 135.3615069, 203.0422603, 287.7502026), "M27": (459.4064379, 176.4120722, 264.6181082, 327.7155085),
              "M30": (560.5872129, 215.2654898, 322.8982346, 367.6808145), "M36": (816.7225563, 313.6214616, 470.4321924, 439.6183651)}
# weld f_vw_d for f_u = 410, gamma_M2 = 1.25 (product, MPa)
STEEL_TABLE = {"S235": 236.71, "S275": 222.79, "S355": 210.41, "S420": 189.37, "S460": 189.37}


def extras():
    for name, (inp, prod) in PROBES.items():
        o = solve(inp)
        for sym, pv in prod.items():
            iv = o[sym]
            ok = abs(iv - pv) <= max(1e-5 * abs(pv), 1e-9)
            xrows.append((name, sym, pv, iv, abs(iv - pv) / abs(pv) if pv else 0.0, "PASS" if ok else "FAIL", ""))
    for g, (th, sh) in GRADE_TABLE.items():
        for lab, pv, thr in (("threads", th, True), ("shank", sh, False)):
            iv = fv_rd(g, "M20", 1, thr)
            xrows.append((f"M20 class {g}, {lab} in shear plane", "F_v_Rd", pv, iv, abs(iv - pv) / pv, "PASS" if abs(iv - pv) / pv < 2e-6 else "FAIL", ""))
    for sz, (a, v, t_, b) in SIZE_TABLE.items():
        o = {"A_s": stress_area(sz), "F_v_Rd": fv_rd("8.8", sz), "F_t_Rd": ft_rd("8.8", sz), "B_p_Rd": bp_rd(sz, 12, 410)[0]}
        for sym, pv in zip(("A_s", "F_v_Rd", "F_t_Rd", "B_p_Rd"), (a, v, t_, b)):
            xrows.append((f"{sz} 8.8 threads, 12 mm S275", sym, pv, o[sym], abs(o[sym] - pv) / pv, "PASS" if abs(o[sym] - pv) / pv < 1e-6 else "FAIL", ""))
    for st, pv in STEEL_TABLE.items():
        iv = fvw_d(410, st)
        xrows.append((f"weld, steel {st}, f_u 410", "f_vw_d", pv, iv, abs(iv - pv) / pv, "PASS" if abs(iv - pv) / pv < 1e-4 else "FAIL", f"beta_w {BETA_W[st]}"))
    # ---- findings: product value versus what the standard asks for ----
    # F1 oversize hole (d_0 = 24 for M20): Table 3.4 note asks 0.8 x the normal-hole bearing resistance
    normal = bearing_conservative("8.8", "M20", 10, 410, 40, 40)[0]
    xrows.append(("M20 8.8 oversize hole d_0 = 24 mm, e1 = e2 = 40", "F_b_Rd (kN)", 91.111, 0.8 * normal, 91.111 / (0.8 * normal) - 1.0, "FAIL", "P1-struct.connection-ec3-01"))
    # F2 block tearing, eccentric group: V_eff,2
    v2 = block_tearing_2(410, 275, 300, 900)
    xrows.append(("block tearing P9 data, eccentric loading", "V_eff,2,Rd (kN)", 241.29419, v2, 241.29419 / v2 - 1.0, "FAIL", "P1-struct.connection-ec3-02 (product has V_eff,1 only)"))
    # F3 single-lap joint, one bolt row: F_b,Rd <= 1.5 f_u d t / gamma_M2
    fb_free = bearing_conservative("8.8", "M20", 10, 410, 60, 60)[0]
    cap = 1.5 * 410 * 20 * 10 / 1.25 / 1e3
    xrows.append(("M20 8.8, e1 = e2 = 60, single lap one row", "F_b_Rd (kN)", 149.0909, cap, 149.0909 / cap - 1.0, "FAIL", "P1-struct.connection-ec3-03 (cap not applied)"))
    # F4 legacy picker (ADR-0169, revised in 499bfb01): gross area times 0.6 f_ub, the shank-in-plane case.
    # The picker now offers 600 MPa for class 10.9 and 480 MPa for 8.8. At safety factor 1 the legacy module gives
    # 0.6 f_ub pi d^2/4 (188.50 and 150.80 kN for M20). The shank case of this sheet gives the same at gamma_M2 = 1.
    legacy = 600.0 * math.pi * 20 ** 2 / 4 / 1e3
    ec3 = fv_rd("10.9", "M20", 1, threads=False, gm2=1.0)
    xrows.append(("legacy calc.bolt-shear-capacity, M20 class 10.9, SF 1 (shank case)", "capacity (kN)", legacy, ec3, legacy / ec3 - 1.0,
                  "PASS" if abs(legacy / ec3 - 1.0) < 1e-9 else "FAIL", "P1-struct.connection-ec3-04 fixed in 499bfb01"))
    legacy88 = 480.0 * math.pi * 20 ** 2 / 4 / 1e3
    ec388 = fv_rd("8.8", "M20", 1, threads=False, gm2=1.0)
    xrows.append(("legacy calc.bolt-shear-capacity, M20 class 8.8, SF 1 (shank case)", "capacity (kN)", legacy88, ec388, legacy88 / ec388 - 1.0,
                  "PASS" if abs(legacy88 / ec388 - 1.0) < 1e-9 else "FAIL", "same"))
    # R1 (independent review): M12 in a 2 mm clearance hole (the product default d_0 = 14 mm).
    # EN 1993-1-8 cl. 3.6.1(5) asks 0.85 x F_v,Rd for classes 4.8, 5.8, 6.8, 8.8, 10.9 (owner to confirm).
    fv12 = fv_rd("8.8", "M12")
    xrows.append(("M12 8.8 default d_0 = 14 mm (2 mm clearance)", "F_v_Rd (kN)", 32.35835, 0.85 * fv12, 32.35835 / (0.85 * fv12) - 1.0, "FAIL", "P1-struct.connection-ec3-R1 (no 0.85 factor)"))


xrows = []


def report_extras():
    print()
    print("Extra checks (product values hard-coded from the Probe tool; see the report)")
    print(f"{'probe':62s} {'symbol':16s} {'product/ref':>12s} {'independent':>12s} {'rel.diff':>9s}  result")
    for name, sym, pv, iv, rd, tag, note in xrows:
        print(f"{name[:62]:62s} {sym[:16]:16s} {pv:12.6g} {iv:12.6g} {rd:9.2e}  {tag} {note}")
    print()
    print("Note: with e_2 = 12 mm (M20, d_0 = 22) k_1 is negative, so F_b,Rd = 0; the product stops with 'Division by zero'.")


if __name__ == "__main__":
    kats()
    extras()
    ok = report()
    report_extras()
    print("KATs: ALL PASS" if ok else "KATs: FAILURES PRESENT")
    sys.exit(0 if ok else 1)
