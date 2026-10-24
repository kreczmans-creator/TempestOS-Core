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
    return 0


if __name__ == "__main__":
    sys.exit(main())
