#!/usr/bin/env python3
"""Independent re-derivation of struct.base-plate (S6), EN 1993-1-8:2005 cl. 6.2 (simplified T-stub model).

Written blind from the EN 1993-1-8 method (cl. 6.2.2, 6.2.4 Table 6.2, 6.2.5, 6.2.6.x, 6.2.8.3 Table 6.7) and
EN 1992-1-1 cl. 3.1.6 and 6.7, then compared with the KATs. Units: mm, MPa, kN, kN*m.
Exit 0 when every KAT value matches within max(KAT tol, 1e-6); exit 1 otherwise.
"""
import math
import sys
from scipy.optimize import brentq

DEFAULTS = dict(column="I/H", h=260, b=256, t_f=17, t_w=10, t=10, D=220, B=400, L=400, t_p=20, f_y=275, f_ck=25,
                alpha_cc=0.85, gamma_c=1.5, gamma_M0=1.0, gamma_M2=1.25, C_fd=0.2, alpha=1.5,
                N_Ed=300, M_Ed=100, V_Ed=80, n_t=2, n_b=4, x_b=165, z_C_in=None, m=28, l_eff=200, prying=False,
                e_min=35, F_t_Rd=141, f_ub=800, f_yb=640, A_s=244.79, F_1_vb_Rd=198.8)


def areas(p, c):
    """Effective bearing area and compression-flange area (mm^2) for outstand c.

    Outer outstands are cut back to the plate edge. Outstands that point into the column (web side, hollow side)
    keep the full c, because that ground lies inside the plate.
    """
    B, L = p["B"], p["L"]
    col = p["column"]
    if col == "CHS":
        D, t = p["D"], p["t"]
        co = max(0.0, min(c, (min(B, L) - D) / 2))
        R = D / 2 + co
        r = max(D / 2 - t - c, 0.0)
        a = math.pi * (R * R - r * r)
        a = min(a, B * L)
        return a, a / 2
    h, b = p["h"], p["b"]
    cx = max(0.0, min(c, (B - b) / 2))  # outer outstand across the width B (column width b)
    cy = max(0.0, min(c, (L - h) / 2))  # outer outstand along the length L (column depth h)
    if col == "I/H":
        tf, tw = p["t_f"], p["t_w"]
        web = h - 2 * tf - 2 * c
        if web < 0:
            a = min((b + 2 * cx) * (h + 2 * cy), B * L)
            af = (b + 2 * cx) * (tf + cy + c)
        else:
            flange = (b + 2 * cx) * (tf + cy + c)  # outer outstand cy, inner outstand c
            a = min(2 * flange + web * min(tw + 2 * c, b + 2 * cx), B * L)
            af = flange
    else:
        t = p["t"]
        a = (b + 2 * cx) * (h + 2 * cy) - max(b - 2 * t - 2 * c, 0) * max(h - 2 * t - 2 * c, 0)
        a = min(a, B * L)
        af = (b + 2 * cx) * (t + cy + c)
    return a, min(af, a / 2)


def zc_default(p, c):
    col = p["column"]
    if col == "I/H":
        return (p["h"] - p["t_f"]) / 2
    if col == "RHS":
        return (p["h"] - p["t"]) / 2
    D, t = p["D"], p["t"]
    co = max(0.0, min(c, (min(p["B"], p["L"]) - D) / 2))
    R = D / 2 + co
    r = max(D / 2 - t - c, 0.0)
    return 4 * (R ** 3 - r ** 3) / (3 * math.pi * (R * R - r * r))


def bearing(p, t_p=None):
    t_p = p["t_p"] if t_p is None else t_p
    f_cd = p["alpha_cc"] * p["f_ck"] / p["gamma_c"]
    f_jd = 2.0 / 3.0 * p["alpha"] * f_cd
    c = t_p * math.sqrt(p["f_y"] / (3 * f_jd * p["gamma_M0"]))
    a, af = areas(p, c)
    return f_cd, f_jd, c, a, af


def forces(p, zC):
    N, M, xb = p["N_Ed"], p["M_Ed"] * 1000.0, p["x_b"]  # M in kN*mm
    z = zC + xb
    C = (M + N * xb) / z
    T = C - N
    if C >= 0 and T >= 0:
        return 1, C, 0.0, T, 0.0
    if N > 0 and T < 0:  # both flanges in compression
        return 2, N / 2 + M / (2 * zC), N / 2 - M / (2 * zC), 0.0, 0.0
    # N < 0 and C < 0: both rows in tension
    return 3, 0.0, 0.0, -N / 2 + M / (2 * xb), -N / 2 - M / (2 * xb)


def tstub(p, t_p):
    Mpl = 0.25 * p["l_eff"] * t_p ** 2 * p["f_y"] / p["gamma_M0"] / 1e6  # kN*m
    m = p["m"]
    nFt = p["n_t"] * p["F_t_Rd"]
    if p["prying"]:
        n = min(p["e_min"], 1.25 * m)
        m1 = 4 * Mpl * 1e6 / m / 1e3
        m2 = (2 * Mpl * 1e6 + n * nFt * 1e3) / (m + n) / 1e3
        m3 = nFt
        return Mpl, m1, m2, m3, min(m1, m2, m3), n
    m12 = 2 * Mpl * 1e6 / m / 1e3
    return Mpl, m12, None, nFt, min(m12, nFt), None


def utilisations(p, t_p):
    f_cd, f_jd, c, a, af = bearing(p, t_p)
    N, M = p["N_Ed"], p["M_Ed"]
    NjRd = f_jd * a / 1e3
    uN = max(N, 0) / NjRd
    zC = p["z_C_in"] if p["z_C_in"] else zc_default(p, c)
    reg, C, C2, T, T2 = forces(p, zC)
    CRd = f_jd * af / 1e3
    uC = None
    if M > 0 and reg in (1, 2):
        uC = max(C, C2) / CRd
    ts = tstub(p, t_p)
    Tmax = max(T, T2)
    uT = Tmax / ts[4] if Tmax > 0 else 0.0
    return dict(f_cd=f_cd, f_jd=f_jd, c=c, A_eff=a, A_flange=af, N_j_Rd=NjRd, u_N=uN, z_C=zC, z=zC + p["x_b"], regime=reg,
                C_Ed=C, C_Ed_2=C2, T_Ed=T, T_Ed_2=T2, F_t_Ed=Tmax / p["n_t"], C_Rd=CRd, u_C=uC, M_pl_Rd=ts[0], T_Rd_1=ts[1],
                T_Rd_2=ts[2], T_Rd_3=ts[3], T_Rd=ts[4], n_pry=ts[5], u_T=uT, u_bt=Tmax / p["n_t"] / p["F_t_Rd"])


def solve_min_t(fun):
    """Smallest t in [10,100] with fun(t) <= 1, fun decreasing. Returns float or text."""
    if fun(10) <= 1:
        return 10.0
    if fun(100) > 1:
        return "> 100 mm"
    return brentq(lambda t: fun(t) - 1, 10, 100, xtol=1e-12, rtol=1e-14)


def calc(**kw):
    p = dict(DEFAULTS)
    p.update(kw)
    r = utilisations(p, p["t_p"])
    N = p["N_Ed"]
    # shear
    a_bc = 0.44 - 0.0003 * p["f_yb"]
    F2 = a_bc * p["f_ub"] * p["A_s"] / p["gamma_M2"] / 1e3
    Fvb = min(p["F_1_vb_Rd"], F2)
    Ff = p["C_fd"] * max(N, 0)
    VRd = Ff + p["n_b"] * Fvb
    r.update(alpha_bc=a_bc, F_2_vb_Rd=F2, F_vb_Rd=Fvb, F_f_Rd=Ff, V_Rd=VRd, u_V=p["V_Ed"] / VRd,
             V_b_Ed=max(p["V_Ed"] - Ff, 0) / p["n_b"])
    # thickness
    if N <= 0:
        r["t_req_N"] = None
    else:
        r["t_req_N"] = solve_min_t(lambda t: utilisations(p, t)["u_N"])

    def worst(t):
        u = utilisations(p, t)
        return max(u["u_N"], u["u_C"] or 0, u["u_T"])

    # bolts govern when T_Ed > n_t F_t,Rd (no thickness helps)
    if max(r["T_Ed"], r["T_Ed_2"]) > p["n_t"] * p["F_t_Rd"] + 1e-9:
        r["t_req"] = "bolts govern"
    else:
        r["t_req"] = solve_min_t(worst)
    r["u_max"] = max(r["u_N"], r["u_C"] or 0, r["u_T"], r["u_V"], r["u_bt"])
    return r


IH_BASE = dict(column="I/H")

KATS = {
    "ka-ih-moment-bolts": (dict(), [("z_C", 121.5, 1e-9), ("z", 286.5, 1e-9), ("C_Ed", 521.8150087, 1e-6), ("T_Ed", 221.8150087, 1e-6),
        ("F_t_Ed", 110.9075044, 1e-6), ("C_Rd", 601.8358193, 1e-6), ("u_C", 0.8670388037, 1e-6), ("M_pl_Rd", 5.5, 1e-9),
        ("T_Rd_1", 392.8571429, 1e-6), ("T_Rd_3", 282, 1e-9), ("T_Rd", 282, 1e-9), ("u_T", 0.7865780451, 1e-6),
        ("alpha_bc", 0.248, 1e-9), ("F_2_vb_Rd", 38.8530688, 1e-6), ("F_vb_Rd", 38.8530688, 1e-6), ("F_f_Rd", 60, 1e-9),
        ("V_Rd", 215.4122752, 1e-6), ("u_V", 0.3713808785, 1e-6), ("V_b_Ed", 5, 1e-9), ("t_req", 17.60887161, 1e-6),
        ("u_max", 0.8670388037, 1e-6)]),
    "ka-ih-moment-prying": (dict(prying=True, e_min=35), [("T_Rd_1", 785.7142857, 1e-6), ("n_pry", 35, 1e-9),
        ("T_Rd_2", 331.2698413, 1e-6), ("T_Rd_3", 282, 1e-9), ("T_Rd", 282, 1e-9), ("u_T", 0.7865780451, 1e-6)]),
    "ka-ih-both-compression": (dict(N_Ed=600, M_Ed=40, V_Ed=0), [("C_Ed", 464.6090535, 1e-6), ("C_Ed_2", 135.3909465, 1e-6),
        ("T_Ed", 0, 1e-9), ("F_t_Ed", 0, 1e-9), ("u_C", 0.7719863767, 1e-6)]),
    "ka-ih-both-tension": (dict(N_Ed=-300, M_Ed=20, V_Ed=0), [("T_Ed", 210.6060606, 1e-6), ("T_Ed_2", 89.39393939, 1e-6),
        ("F_t_Ed", 105.3030303, 1e-6), ("C_Ed", 0, 1e-9), ("F_f_Rd", 0, 1e-9)]),
    "ka-ih-axial": (dict(N_Ed=1200, M_Ed=0, V_Ed=0), [("f_cd", 14.16666667, 1e-6), ("f_jd", 14.16666667, 1e-6),
        ("c", 50.87470191, 1e-6), ("A_eff", 98849.98688, 1e-6), ("N_j_Rd", 1400.374814, 1e-6), ("u_N", 0.8569134405, 1e-6),
        ("t_req_N", 17.0753989, 1e-6), ("t_req", 17.0753989, 1e-6), ("F_t_Ed", 0, 1e-9)]),
    "ka-rhs-axial": (dict(column="RHS", h=200, b=200, t=10, B=350, L=350, N_Ed=900, M_Ed=0, V_Ed=0),
        [("A_eff", 84929.5469, 1e-6), ("N_j_Rd", 1203.168581, 1e-6), ("u_N", 0.7480248522, 1e-6), ("t_req", 14.4652113, 1e-6)]),
    "ka-chs-axial": (dict(column="CHS", D=220, t=10, B=350, L=350, N_Ed=800, M_Ed=0, V_Ed=0),
        [("A_eff", 73724.93227, 1e-6), ("N_j_Rd", 1044.436541, 1e-6), ("u_N", 0.7659632433, 1e-6), ("t_req", 14.85923906, 1e-6)]),
    "ka-chs-moment": (dict(column="CHS", D=220, t=10, B=350, L=350, N_Ed=200, M_Ed=60, V_Ed=0, x_b=140, m=25),
        [("z_C", 73.15464965, 1e-6), ("z", 213.1546497, 1e-6), ("C_Ed", 412.8457913, 1e-6), ("T_Ed", 212.8457913, 1e-6),
         ("F_t_Ed", 106.4228956, 1e-6), ("C_Rd", 522.2182703, 1e-6), ("u_C", 0.7905617532, 1e-6), ("t_req", 15.39956042, 1e-6)]),
    "ka-ih-overlap-clip": (dict(h=150, b=150, t_f=10, t_w=6, B=300, L=400, t_p=40, N_Ed=1000, M_Ed=0, V_Ed=0),
        [("c", 101.7494038, 1e-6), ("A_eff", 106049.6423, 1e-6), ("N_j_Rd", 1502.369932, 1e-6)]),
}
# The KAT cases that use IhBase also take the template defaults for every input they do not list
# (alpha_cc 0.85, gamma_c 1.5, gamma_M0 1, gamma_M2 1.25, alpha 1.5, C_fd 0.2, prying off, e_min 35).
# The RHS, CHS and clip cases leave x_b, m, l_eff, n_t, n_b, F_t_Rd and bolt data at the template defaults.

# Extra probes: (name, inputs, [(symbol, product value observed with the C# probe)]). Filled from probe runs.
EXTRAS = []


def rel(a, b):
    return abs(a - b) / max(abs(b), 1e-12) if b != 0 else abs(a)


def main():
    bad = 0
    total = 0
    print(f"{'case':26s} {'symbol':10s} {'KAT':>14s} {'indep':>14s} {'rel diff':>10s} {'tol':>8s} result")
    for case, (kw, exp) in KATS.items():
        r = calc(**kw)
        for sym, e, tol in exp:
            v = r[sym]
            total += 1
            if isinstance(v, str) or v is None:
                ok, d = False, float("nan")
            else:
                d = abs(v - e) / abs(e) if e != 0 else abs(v - e)
                ok = d <= max(tol, 1e-6)
            bad += (not ok)
            vs = f"{v:14.8g}" if not isinstance(v, str) and v is not None else f"{str(v):>14s}"
            print(f"{case:26s} {sym:10s} {e:14.8g} {vs} {d:10.2e} {tol:8.0e} {'PASS' if ok else 'FAIL'}")
    print()
    print("Extra checks (product values observed with the C# probe)")
    for name, kw, obs in EXTRAS:
        r = calc(**kw)
        for sym, pv in obs:
            v = r[sym]
            total += 1
            if isinstance(pv, str) or isinstance(v, str) or v is None or pv is None:
                ok = (str(pv) == str(v)) or (pv is None and v is None)
                d = float("nan")
            else:
                d = abs(v - pv) / abs(pv) if pv != 0 else abs(v - pv)
                ok = d <= 1e-6
            bad += (not ok)
            print(f"{name:26s} {sym:10s} {str(pv):>14s} {str(v if isinstance(v, str) or v is None else round(v, 8)):>14s} {d:10.2e} {'PASS' if ok else 'FAIL'}")
    print(f"\n{total - bad}/{total} values match")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
