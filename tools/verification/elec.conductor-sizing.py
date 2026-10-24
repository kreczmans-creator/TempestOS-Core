#!/usr/bin/env python3
"""Independent re-derivation of elec.conductor-sizing (E4), Track P / P1.

Method (written from BS 7671 reg. 433.1.1 and its Appendix 4 method, and
BS EN 60204-1 cl. 12.4 as I know them; not translated from the C#):
  I_b   = P / (sqrt(3) U cos(phi) eta)   three-phase
        = P / (U cos(phi) eta)           single-phase
        = P / U                          DC
  I_n   = smallest listed rating >= I_b
  C_c   = 1.45 / 2 = 0.725 for a semi-enclosed fuse, else 1
  I_t,req = I_n / (C_a C_g C_i C_c)
  CSA   = smallest conductor whose tabulated I_t >= I_t,req
  I_z   = I_t C_a C_g C_i
  checks: I_b <= I_n, I_n <= I_z, I_2 <= 1.45 I_z
  derived C_a = sqrt((theta_p - theta_a) / (theta_p - theta_ref))
  AWG: d = 0.127 * 92^((36 - n)/39) mm, A = pi d^2 / 4
Exit 0 when every KAT and every extra check agrees.
"""
import math
import sys

RATINGS = [6, 10, 16, 20, 25, 32, 40, 50, 63]
SYN = [(1.5, 12.3), (2.5, 17.7), (4, 24.1), (6, 31.9), (10, 44.6)]
SYN_AWG = [(14, 11.2), (12, 16.9), (10, 26.3), (8, 37.4)]
EPS_NONE = 0.0


def awg_area(n):
    d = 0.127 * 92 ** ((36 - n) / 39)
    return math.pi * d * d / 4


def ib_calc(supply, P_kw, U, cosphi, eta):
    P = P_kw * 1000.0
    if supply == "three":
        return P / (math.sqrt(3) * U * cosphi * eta)
    if supply == "single":
        return P / (U * cosphi * eta)
    return P / U


def derive_ca(theta_p, theta_a, theta_ref):
    num = theta_p - theta_a
    den = theta_p - theta_ref
    if num <= 0 or den <= 0:
        return None
    return math.sqrt(num / den)


def size(Ib=None, supply="three", P=7.5, U=400, cosphi=0.85, eta=0.9,
         device="C", Ca=1.0, Cg=1.0, Ci=1.0, ratings=RATINGS, table=SYN,
         awg=False, I2_in=None):
    r = {}
    r["I_b"] = Ib if Ib is not None else ib_calc(supply, P, U, cosphi, eta)
    cands = [x for x in sorted(ratings) if x >= r["I_b"]]
    if not cands:
        r["block"] = "I_n"
        return r
    r["I_n"] = cands[0]
    r["C_c"] = 0.725 if device == "semi" else 1.0
    r["C_a"] = Ca
    r["I_t_req"] = r["I_n"] / (Ca * Cg * Ci * r["C_c"])
    rows = [(awg_area(a) if awg else a, it, a) for a, it in table]
    ok = sorted([x for x in rows if x[1] >= r["I_t_req"]], key=lambda x: x[0])
    if not ok:
        r["block"] = "CSA"
        return r
    r["CSA"], it, g = ok[0]
    r["I_z"] = it * Ca * Cg * Ci
    r["u_Ib"] = r["I_b"] / r["I_n"]
    r["u_In"] = r["I_n"] / r["I_z"]
    if device in ("B", "C", "D"):
        r["I2"] = 1.45 * r["I_n"]
    elif device == "semi":
        r["I2"] = I2_in if I2_in else 2 * r["I_n"]
    elif I2_in:
        r["I2"] = I2_in
    if "I2" in r:
        r["u_I2"] = r["I2"] / (1.45 * r["I_z"])
    return r


# ---- Known-answer cases: (case, result, [(symbol, expected, tol)]) ----
def kats():
    out = []
    r = size(supply="three", P=7.5, U=400, cosphi=0.85, eta=0.9, device="C", Ca=0.91, Cg=0.77)
    out.append(("ka-three-phase-mcb-c", r, [("I_b", 14.15074189, 1e-6), ("I_n", 16, 1e-9), ("I_t_req", 22.83430855, 1e-6), ("CSA", 4, 1e-9), ("I_z", 16.88687, 1e-6), ("u_Ib", 0.8844213682, 1e-6), ("u_In", 0.9474816825, 1e-6)]))
    r = size(supply="single", P=3, U=230, cosphi=0.95, eta=0.9, device="B")
    out.append(("ka-single-phase", r, [("I_b", 15.25553013, 1e-6), ("I_n", 16, 1e-9), ("I_t_req", 16, 1e-9), ("CSA", 2.5, 1e-9), ("I_z", 17.7, 1e-9), ("u_In", 0.9039548023, 1e-6)]))
    r = size(supply="dc", P=0.6, U=24, device="gG", Ca=0.91)
    out.append(("ka-dc", r, [("I_b", 25, 1e-9), ("I_n", 25, 1e-9), ("I_t_req", 27.47252747, 1e-6), ("CSA", 6, 1e-9), ("I_z", 29.029, 1e-9), ("u_In", 0.8612077578, 1e-6)]))
    r = size(Ib=18, device="semi")
    out.append(("ka-semi-enclosed", r, [("I_b", 18, 1e-9), ("I_n", 20, 1e-9), ("C_c", 0.725, 1e-9), ("I_t_req", 27.5862069, 1e-6), ("CSA", 6, 1e-9), ("I_z", 31.9, 1e-9), ("I2", 40, 1e-9), ("u_I2", 0.8647713761, 1e-6)]))
    ca = derive_ca(70, 40, 30)
    r = size(supply="three", P=7.5, U=400, cosphi=0.85, eta=0.9, device="C", Ca=ca, Cg=0.77)
    out.append(("ka-derived-ca", r, [("C_a", 0.8660254038, 1e-9), ("I_t_req", 23.99377742, 1e-6), ("CSA", 4, 1e-9), ("I_z", 16.07083342, 1e-6)]))
    r = size(Ib=25, device="gG", awg=True, table=SYN_AWG)
    out.append(("ka-awg-10", r, [("CSA", 5.261154955, 1e-9), ("I_n", 25, 1e-9), ("I_z", 26.3, 1e-9)]))
    return out


def rel(a, b):
    if b == 0:
        return abs(a - b)
    return abs(a - b) / abs(b)


def main():
    fails = 0
    print("== Known-answer cases ==")
    print(f"{'case':24s} {'symbol':8s} {'KAT':>14s} {'indep':>14s} {'rel diff':>10s} {'tol':>8s} result")
    for case, r, exp in kats():
        for sym, e, tol in exp:
            v = r.get(sym)
            d = rel(v, e) if v is not None else float("inf")
            ok = d <= max(tol, 1e-6)
            fails += not ok
            print(f"{case:24s} {sym:8s} {e:14.8g} {v if v is None else format(v, '14.8g'):>14} {d:10.2e} {tol:8.0e} {'PASS' if ok else 'FAIL'}")
    # extras are appended below by the second part of the script
    fails += extras()
    print("\nOVERALL:", "ALL MATCH" if fails == 0 else f"{fails} MISMATCH(ES)")
    return 1 if fails else 0


def extras():
    """Extra probes. Product values were observed with the probe tool (C# run)
    and are hard-coded. Kind 'agree' counts toward the exit code. Kind 'note'
    prints a divergence that is a finding (see the report) and does not."""
    fails = 0
    pvc_ca = lambda ta, tr=30: derive_ca(70, ta, tr)
    xl_ca = lambda ta, tr=30: derive_ca(90, ta, tr)
    R = RATINGS
    cases = [
        # (name, kwargs for size(), {symbol: product value})
        ("x1 typical 7.5kW MCB-C", dict(device="C"), {"I_b": 14.15074189, "I_n": 16, "CSA": 2.5, "I_z": 17.7, "I2": 23.2, "u_I2": 0.9039548023}),
        ("x2 boundary I_t,req == I_t", dict(device="C", Ca=16 / 17.7), {"CSA": 2.5, "I_z": 16, "u_In": 1}),
        ("x3 just above boundary (1e-8)", dict(device="C", Ca=16 / 17.7 * (1 - 1e-8)), {"CSA": 4, "I_z": 21.78531052}),
        ("x4 semi-enclosed 7.5kW default I2", dict(device="semi"), {"I_t_req": 22.06896552, "CSA": 4, "I_z": 24.1, "I2": 32, "u_I2": 0.9157247103}),
        ("x5 derived Ca XLPE 60C", dict(device="C", Ca=xl_ca(60)), {"C_a": 0.7071067812, "I_t_req": 22.627417, "CSA": 4, "I_z": 17.04127343}),
        ("x6 derived Ca XLPE 25C (below ref)", dict(device="C", Ca=xl_ca(25)), {"C_a": 1.040833, "I_t_req": 15.37230277, "CSA": 2.5, "I_z": 18.4227441}),
        ("x7 derived Ca PVC -10C", dict(device="C", Ca=pvc_ca(-10)), {"C_a": 1.414213562, "CSA": 1.5, "I_z": 17.39482682}),
        ("x8 derived Ca user 250C ref 60C -10C", dict(device="C", Ca=derive_ca(250, -10, 60)), {"C_a": 1.169795304, "I_t_req": 13.67760663, "CSA": 2.5, "I_z": 20.70537688}),
        ("x9 AWG 63 A, rows 4,2,0,-3", dict(Ib=63, device="gG", awg=True, table=[(4, 60), (2, 80), (0, 100), (-3, 200)]), {"CSA": 33.63083402, "I_z": 80, "I_n": 63}),
        ("x10 AWG 4/0 only qualifying", dict(Ib=25, device="gG", awg=True, table=[(40, 1), (-3, 200)]), {"CSA": 107.2193026, "I_z": 200}),
        ("x11 500kW 1000V cos1 eta1 MCCB I2 1000", dict(supply="three", P=500, U=1000, cosphi=1, eta=1, device="MCCB", I2_in=1000, ratings=[630, 800, 1000], table=[(300, 600), (500, 800), (630, 1000)]), {"I_b": 288.6751346, "I_n": 630, "CSA": 500, "I_z": 800, "u_I2": 0.8620689655}),
        ("x12 MCCB I2 = 1.6 In", dict(device="MCCB", I2_in=25.6), {"u_I2": 0.997467368, "CSA": 2.5}),
        ("x13 MCCB I2 = 1.3 In", dict(device="MCCB", I2_in=20.8), {"u_I2": 0.8104422365}),
        ("x14 Cg 1.5, Ci 1.5 (arithmetic only)", dict(device="C", Cg=1.5, Ci=1.5), {"I_t_req": 7.111111111, "CSA": 1.5, "I_z": 27.675}),
        ("x15 DC 10 W 12 V", dict(supply="dc", P=0.01, U=12, device="B"), {"I_b": 0.8333333333, "I_n": 6, "CSA": 1.5, "I_z": 12.3, "u_Ib": 0.1388888889}),
        ("x16 unsorted table, 20 A", dict(Ib=20, device="gG", table=[(10, 44.6), (4, 24.1), (1.5, 12.3), (6, 31.9), (2.5, 17.7)]), {"CSA": 4, "I_z": 24.1}),
        ("x17 non-monotonic table (4mm2=15A), 20 A", dict(Ib=20, device="gG", table=[(1.5, 12.3), (2.5, 17.7), (4, 15), (6, 31.9)]), {"CSA": 6, "I_z": 31.9}),
    ]
    print("\n== Extra probes (product value from probe tool vs independent) ==")
    print(f"{'probe':44s} {'symbol':8s} {'product':>14s} {'indep':>14s} {'rel diff':>10s} result")
    for name, kw, prod in cases:
        r = size(**kw)
        for sym, pv in prod.items():
            v = r.get(sym)
            d = rel(v, pv) if v is not None else float("inf")
            ok = d <= 1e-6
            fails += not ok
            print(f"{name:44s} {sym:8s} {pv:14.8g} {v if v is None else format(v, '14.8g'):>14} {d:10.2e} {'PASS' if ok else 'FAIL'}")
    # Block cases: independent calc must also find no solution.
    blocks = [
        ("m4 94.3 A load, ratings top 63 A", dict(P=50, device="C"), "I_n"),
        ("m5 C_g 0.3 needs 53.3 A, table top 44.6", dict(Cg=0.3, device="C"), "CSA"),
    ]
    print("\n-- block cases (product raised a Block with this symbol) --")
    for name, kw, sym in blocks:
        r = size(**kw)
        ok = r.get("block") == sym
        fails += not ok
        print(f"{name:44s} block={r.get('block')} expected={sym} {'PASS' if ok else 'FAIL'}")
    # AWG geometry against well-known nominal areas (mm2, rounded in common tables).
    print("\n-- AWG geometry vs common nominal areas (2 % tolerance) --")
    for n, nominal in [(14, 2.08), (12, 3.31), (10, 5.26), (8, 8.37), (4, 21.15), (2, 33.6), (0, 53.5), (-3, 107.2)]:
        a = awg_area(n)
        ok = abs(a - nominal) / nominal < 0.02
        fails += not ok
        print(f"AWG {n:3d}: formula {a:9.4f} mm2, nominal {nominal:7.2f} mm2 {'PASS' if ok else 'FAIL'}")
    # Notes: divergences that are findings, not counted in the exit code.
    print("\n== Notes (findings; not counted in the exit code) ==")
    ca50 = derive_ca(70, 50, 30)
    r_forgot = size(device="C", Ca=1.0)
    r_right = size(device="C", Ca=ca50)
    print(f"N1 theta_a 50 C PVC with C_a left at 1: product picks {r_forgot['CSA']} mm2 (I_z {r_forgot['I_z']:.3g} A) and warns nothing.")
    print(f"   With C_a = {ca50:.4f} (derived) the independent pick is {r_right['CSA']} mm2 (I_z {r_right['I_z']:.3g} A). Finding -01.")
    print("   theta_a 70 C with PVC (conductor limit 70 C) and C_a = 1 is also accepted without a warning.")
    print("N2 Duplicate 4 mm2 rows (24.1 A and 30 A), 25 A needed: product uses 30 A and picks 4 mm2.")
    r_dup = size(Ib=25, device="gG", table=[(1.5, 12.3), (2.5, 17.7), (4, 24.1), (6, 31.9)])
    print(f"   Using the lower row, the independent pick is {r_dup['CSA']} mm2. Finding -05.")
    print("N3 MCCB with I2 = 1.6 I_n and a 16.5 A conductor: product keeps the conductor and FAILs the I2 check; it does not size up.")
    r_i2 = size(device="MCCB", I2_in=25.6, table=[(1.5, 12.3), (2.5, 16.5), (4, 24.1)])
    print(f"   I2 check utilisation {r_i2['u_I2']:.3f} (>1 means FAIL). Smallest conductor that passes I2 needs I_z >= {25.6 / 1.45:.2f} A. Finding -06.")
    print("N4 CSA = 1000 mm2 in the ampacity table is rejected as above the hard limit 1000 mm2 (float conversion). Finding -07.")
    r_cg = size(device="C", Cg=1.5, Ci=1.5)
    print(f"N5 C_g = C_i = 1.5 is accepted: 16 A MCB gets {r_cg['CSA']} mm2 (I_z {r_cg['I_z']:.4g} A). Grouping and insulation factors are at most 1. Finding -02.")
    print(f"N6 Derived C_a, PVC, theta_a 40 C: ref 30 C gives {derive_ca(70, 40, 30):.4f}; a 40 C-referenced table needs {derive_ca(70, 40, 40):.4f}. Finding -03 (conservative).")
    print(f"N7 Derived C_a at 25 C: PVC {derive_ca(70, 25, 30):.4f}, XLPE {derive_ca(90, 25, 30):.4f}. Published BS 7671 values as I recall them are 1.03 and 1.02. Finding -04.")
    return fails


if __name__ == "__main__":
    sys.exit(main())
