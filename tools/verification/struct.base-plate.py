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
    # The compression strip runs from c inside the flange (or wall) to the outstand outside it. When that outstand is cut
    # back to the plate edge, the strip centroid moves inward by (c - c_cut)/2 (Addendum A fix G-A-10; the pre-A product
    # and this script's first version used the uncut value).
    cut = max(0.0, c - max(0.0, min(c, (p["L"] - p["h"]) / 2))) if col in ("I/H", "RHS") else 0.0
    if col == "I/H":
        return (p["h"] - p["t_f"]) / 2 - cut / 2
    if col == "RHS":
        return (p["h"] - p["t"]) / 2 - cut / 2
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


def utilisations(p, t_p, zC_fixed=None):
    f_cd, f_jd, c, a, af = bearing(p, t_p)
    N, M = p["N_Ed"], p["M_Ed"]
    NjRd = f_jd * a / 1e3
    uN = max(N, 0) / NjRd
    zC = p["z_C_in"] if p["z_C_in"] else (zC_fixed if zC_fixed is not None else zc_default(p, c))
    reg, C, C2, T, T2 = forces(p, zC)
    CRd = f_jd * af / 1e3
    uC = None
    if M > 0 and reg in (1, 2) and C > 0:
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


def calc(hold_forces=False, **kw):
    """hold_forces=True keeps z_C (hence the forces) at its value for the entered t_p while t is searched.

    The product does this. It differs from the self-consistent search only for a CHS, whose z_C depends on c(t_p).
    """
    p = dict(DEFAULTS)
    p.update(kw)
    r = utilisations(p, p["t_p"])
    zfix = r["z_C"] if hold_forces else None
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
        u = utilisations(p, t, zfix)
        return max(u["u_N"], u["u_C"] or 0, u["u_T"])

    # bolts govern when T_Ed > n_t F_t,Rd (no thickness helps)
    # With z_C moving with t (CHS), the least tension is at t = 100 mm, so test the bolts there.
    ref = r if hold_forces else utilisations(p, 100)
    if max(ref["T_Ed"], ref["T_Ed_2"]) > p["n_t"] * p["F_t_Rd"] * (1 + 1e-9):
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

# Extra probes: inputs not in the KATs (other inputs at the template defaults of the product), and the values the
# C# product returned for them (Probe run, observed 2026-10-24; RHS-clip and the four cut-outstand rows re-observed at 0772fcf8). None means the product does not output that value.
EXTRAS = [
    ('typ-UC254-C30', {'h': 260.3, 'b': 256.3, 't_f': 17.3, 't_w': 10.3, 'B': 450, 'L': 450, 't_p': 30, 'f_ck': 30, 'alpha': 1.8, 'N_Ed': 800, 'M_Ed': 150, 'V_Ed': 100, 'x_b': 185, 'm': 35, 'l_eff': 300},
     [('A_eff', 124361.7798), ('z_C', 121.5), ('C_Ed', 972.2675367), ('T_Ed', 172.2675367), ('F_t_Ed', 86.13376835), ('u_C', 0.8601559792), ('T_Rd', 282.0), ('u_T', 0.6108777897), ('u_V', 0.3170453653), ('V_b_Ed', 0.0), ('t_req_N', 10.0), ('t_req', 26.4363213), ('u_max', 0.8601559792)]),
    ('reg1-2-boundary', {'N_Ed': 600, 'M_Ed': 72.9, 'V_Ed': 0},
     [('A_eff', 98849.98688), ('z_C', 121.5), ('C_Ed', 600.0), ('T_Ed', 0.0), ('F_t_Ed', 0.0), ('u_C', 0.9969496344), ('T_Rd', 282.0), ('u_T', 0.0), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 10.0), ('t_req', 19.9465132), ('u_max', 0.9969496344)]),
    ('reg1-3-boundary', {'N_Ed': -300, 'M_Ed': 49.5, 'V_Ed': 0},
     [('A_eff', 98849.98688), ('z_C', 121.5), ('C_Ed', 0.0), ('T_Ed', 300.0), ('F_t_Ed', 150.0), ('u_C', None), ('T_Rd', 282.0), ('u_T', 1.063829787), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', None), ('t_req', 'bolts govern'), ('u_max', 1.063829787)]),
    ('tension-only-3bolts', {'N_Ed': -1000, 'M_Ed': 0, 'V_Ed': 50, 'n_t': 3, 'n_b': 6, 'F_t_Rd': 200},
     [('A_eff', 98849.98688), ('z_C', 121.5), ('C_Ed', 0.0), ('T_Ed', 500.0), ('F_t_Ed', 166.6666667), ('u_C', None), ('T_Rd', 392.8571429), ('u_T', 1.272727273), ('u_V', 0.2144832723), ('V_b_Ed', 8.333333333), ('t_req_N', None), ('t_req', 22.56304299), ('u_max', 1.272727273)]),
    ('N0-M100', {'N_Ed': 0, 'M_Ed': 100, 'V_Ed': 0},
     [('A_eff', 98849.98688), ('z_C', 121.5), ('C_Ed', 349.0401396), ('T_Ed', 349.0401396), ('F_t_Ed', 174.5200698), ('u_C', 0.579959066), ('T_Rd', 282.0), ('u_T', 1.237730992), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', None), ('t_req', 'bolts govern'), ('u_max', 1.237730992)]),
    ('N0-M0', {'N_Ed': 0, 'M_Ed': 0, 'V_Ed': 0},
     [('A_eff', 98849.98688), ('z_C', 121.5), ('C_Ed', 0.0), ('T_Ed', 0.0), ('F_t_Ed', 0.0), ('u_C', None), ('T_Rd', 282.0), ('u_T', 0.0), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', None), ('t_req', 10.0), ('u_max', 0.0)]),
    ('tiny-M', {'N_Ed': 300, 'M_Ed': 0.001, 'V_Ed': 10},
     [('A_eff', 98849.98688), ('z_C', 121.5), ('C_Ed', 150.0041152), ('T_Ed', 0.0), ('F_t_Ed', 0.0), ('u_C', 0.2492442464), ('T_Rd', 282.0), ('u_T', 0.0), ('u_V', 0.04642260981), ('V_b_Ed', 0.0), ('t_req_N', 10.0), ('t_req', 10.0), ('u_max', 0.2492442464)]),
    ('very-large', {'N_Ed': 10000, 'M_Ed': 2000, 'V_Ed': 2000},
     [('A_eff', 98849.98688), ('z_C', 121.5), ('C_Ed', 12739.9651), ('T_Ed', 2739.965096), ('F_t_Ed', 1369.982548), ('u_C', 21.16850591), ('T_Rd', 282.0), ('u_T', 9.716188284), ('u_V', 0.9278967291), ('V_b_Ed', 0.0), ('t_req_N', '> 100 mm'), ('t_req', 'bolts govern'), ('u_max', 21.16850591)]),
    ('RHS-moment', {'column': 'RHS', 'h': 250, 'b': 150, 't': 8, 'B': 400, 'L': 450, 't_p': 25, 'N_Ed': 250, 'M_Ed': 80, 'V_Ed': 40, 'x_b': 180, 'm': 30},
     [('A_eff', 103823.4277), ('z_C', 121.0), ('C_Ed', 415.282392), ('T_Ed', 165.282392), ('F_t_Ed', 82.64119601), ('u_C', 0.7822926061), ('T_Rd', 282.0), ('u_T', 0.5861077731), ('u_V', 0.1947303293), ('V_b_Ed', 0.0), ('t_req_N', 10.0), ('t_req', 20.9045267), ('u_max', 0.7822926061)]),
    ('RHS-clip', {'column': 'RHS', 'h': 200, 'b': 200, 't': 10, 'B': 230, 'L': 300, 'N_Ed': 300, 'M_Ed': 60, 'V_Ed': 0, 'x_b': 130, 'm': 20, 't_p': 40},
     [('A_eff', 69000.0), ('z_C', 69.12529809), ('C_Ed', 497.174397), ('T_Ed', 197.174397), ('F_t_Ed', 98.58719848), ('u_C', 1.017236618), ('T_Rd', 282.0), ('u_T', 0.6991999892), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 10.0), ('t_req', '> 100 mm'), ('u_max', 1.017236618)]),
    ('RHS-clip-tp20', {'column': 'RHS', 'h': 200, 'b': 200, 't': 10, 'B': 230, 'L': 300, 'N_Ed': 300, 'M_Ed': 60, 'V_Ed': 0, 'x_b': 130, 'm': 20, 't_p': 20},
     [('A_eff', 62876.8442), ('z_C', 94.56264905), ('C_Ed', 440.8569298), ('T_Ed', 140.8569298), ('F_t_Ed', 70.42846489), ('u_C', 1.220308665), ('T_Rd', 282.0), ('u_T', 0.4994926588), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 10.0), ('t_req', 29.60267351), ('u_max', 1.220308665)]),
    ('RHS-clip-at-its-own-t_req', {'column': 'RHS', 'h': 200, 'b': 200, 't': 10, 'B': 230, 'L': 300, 'N_Ed': 300, 'M_Ed': 60, 'V_Ed': 0, 'x_b': 130, 'm': 20, 't_p': 29.60267351},
     [('A_eff', 68135.79987), ('z_C', 82.34932024), ('C_Ed', 466.2129358), ('T_Ed', 166.2129358), ('F_t_Ed', 83.10646789), ('u_C', 1.057515271), ('T_Rd', 282.0), ('u_T', 0.5894075737), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 10.0), ('t_req', 32.66191283), ('u_max', 1.057515271)]),
    ('IH-cutL-tp20', {'B': 360, 'L': 360, 'N_Ed': 500, 'M_Ed': 100, 'V_Ed': 0, 'x_b': 165, 't_p': 20},
     [('A_eff', 98224.13871), ('z_C', 121.062649), ('C_Ed', 637.9721386), ('T_Ed', 137.9721386), ('F_t_Ed', 68.98606932), ('u_C', 1.067909651), ('T_Rd', 282.0), ('u_T', 0.489262903), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 10.0), ('t_req', 22.83751021), ('u_max', 1.067909651)]),
    ('IH-cutL-at-its-own-t_req', {'B': 360, 'L': 360, 'N_Ed': 500, 'M_Ed': 100, 'V_Ed': 0, 'x_b': 165, 't_p': 22.8375},
     [('A_eff', 103923.6403), ('z_C', 117.4537249), ('C_Ed', 646.1235379), ('T_Ed', 146.1235379), ('F_t_Ed', 73.06176893), ('u_C', 1.012777256), ('T_Rd', 282.0), ('u_T', 0.518168574), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 10.0), ('t_req', 23.46584355), ('u_max', 1.012777256)]),
    ('CHS-clip', {'column': 'CHS', 'D': 300, 't': 10, 'B': 340, 'L': 500, 't_p': 40, 'N_Ed': 800, 'M_Ed': 0, 'V_Ed': 0, 'x_b': 200, 'm': 20},
     [('A_eff', 86195.538), ('z_C', 75.13204442), ('C_Ed', 400.0), ('T_Ed', 0.0), ('F_t_Ed', 0.0), ('u_C', None), ('T_Rd', 282.0), ('u_T', 0.0), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 13.94719762), ('t_req', 13.94719762), ('u_max', 0.6551451449)]),
    ('CHS-thick-wall', {'column': 'CHS', 'D': 100, 't': 40, 'B': 400, 'L': 400, 't_p': 30, 'N_Ed': 800, 'M_Ed': 0, 'V_Ed': 0, 'x_b': 100, 'm': 20},
     [('A_eff', 50123.27732), ('z_C', 53.60850023), ('C_Ed', 400.0), ('T_Ed', 0.0), ('F_t_Ed', 0.0), ('u_C', None), ('T_Rd', 282.0), ('u_T', 0.0), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 33.05038458), ('t_req', 33.05038458), ('u_max', 1.126633996)]),
    ('IH-clip-no-overlap-moment', {'h': 300, 'b': 300, 't_f': 20, 't_w': 12, 'B': 340, 'L': 480, 't_p': 25, 'N_Ed': 500, 'M_Ed': 150, 'V_Ed': 0, 'x_b': 200, 'm': 30},
     [('A_eff', 118572.8378), ('z_C', 140.0), ('C_Ed', 735.2941176), ('T_Ed', 235.2941176), ('F_t_Ed', 117.6470588), ('u_C', 1.037160025), ('T_Rd', 282.0), ('u_T', 0.8343763037), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 10.0), ('t_req', 26.07508511), ('u_max', 1.037160025)]),
    ('IH-overlap-unclipped-moment', {'h': 150, 'b': 150, 't_f': 10, 't_w': 6, 'B': 500, 'L': 500, 't_p': 40, 'N_Ed': 1000, 'M_Ed': 100, 'V_Ed': 0, 'x_b': 150, 'm': 30},
     [('A_eff', 124961.407), ('z_C', 70.0), ('C_Ed', 1136.363636), ('T_Ed', 136.3636364), ('F_t_Ed', 68.18181818), ('u_C', 1.283818831), ('T_Rd', 282.0), ('u_T', 0.4835589942), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 23.33637388), ('t_req', 49.24537992), ('u_max', 1.283818831)]),
    ('prying-mode2', {'prying': True, 'e_min': 80, 'm': 28, 't_p': 14, 'N_Ed': 100, 'M_Ed': 100, 'F_t_Rd': 300, 'l_eff': 250, 'L': 500, 'B': 400},
     [('A_eff', 70310.07317), ('z_C', 121.5), ('C_Ed', 406.6317627), ('T_Ed', 306.6317627), ('F_t_Ed', 153.3158813), ('u_C', 0.9942556979), ('T_Rd', 440.2777778), ('u_T', 0.6964506912), ('u_V', 0.4560684246), ('V_b_Ed', 15.0), ('t_req_N', 10.0), ('t_req', 13.92146361), ('u_max', 0.9942556979)]),
    ('prying-mode1', {'prying': True, 'e_min': 35, 'm': 40, 't_p': 10, 'N_Ed': -50, 'M_Ed': 60, 'F_t_Rd': 300, 'l_eff': 150, 'x_b': 170, 'L': 500},
     [('A_eff', 52318.75815), ('z_C', 121.5), ('C_Ed', 176.6723842), ('T_Ed', 226.6723842), ('F_t_Ed', 113.3361921), ('u_C', 0.5987313128), ('T_Rd', 103.125), ('u_T', 2.198035241), ('u_V', 0.5147598534), ('V_b_Ed', 20.0), ('t_req_N', None), ('t_req', 14.82577229), ('u_max', 2.198035241)]),
    ('zCin-150', {'z_C_in': 150},
     [('A_eff', 98849.98688), ('z_C', 150.0), ('C_Ed', 474.6031746), ('T_Ed', 174.6031746), ('F_t_Ed', 87.3015873), ('u_C', 0.7885924357), ('T_Rd', 282.0), ('u_T', 0.6191601936), ('u_V', 0.3713808785), ('V_b_Ed', 5.0), ('t_req_N', 10.0), ('t_req', 16.13574916), ('u_max', 0.7885924357)]),
    ('shear-F1-fyb235', {'f_yb': 235, 'F_1_vb_Rd': 20, 'f_ub': 400, 'V_Ed': 150, 'C_fd': 0.3},
     [('A_eff', 98849.98688), ('z_C', 121.5), ('C_Ed', 521.8150087), ('T_Ed', 221.8150087), ('F_t_Ed', 110.9075044), ('u_C', 0.8670388037), ('T_Rd', 282.0), ('u_T', 0.7865780451), ('u_V', 0.8823529412), ('V_b_Ed', 15.0), ('t_req_N', 10.0), ('t_req', 17.60887161), ('u_max', 0.8823529412)]),
    ('shear-fyb400-gM2-1.5', {'f_yb': 400, 'f_ub': 500, 'A_s': 353, 'gamma_M2': 1.5, 'V_Ed': 300, 'N_Ed': -200, 'M_Ed': 10},
     [('A_eff', 98849.98688), ('z_C', 121.5), ('C_Ed', 0.0), ('T_Ed', 130.3030303), ('F_t_Ed', 65.15151515), ('u_C', None), ('T_Rd', 282.0), ('u_T', 0.4620674833), ('u_V', 1.991855524), ('V_b_Ed', 75.0), ('t_req_N', None), ('t_req', 11.51834087), ('u_max', 1.991855524)]),
    ('factors-extreme-alpha3', {'gamma_M0': 1.1, 'f_y': 355, 'gamma_c': 1.2, 'alpha_cc': 0.8, 'alpha': 3, 'N_Ed': 1500, 'M_Ed': 0, 'V_Ed': 0},
     [('A_eff', 70883.77276), ('z_C', 121.5), ('C_Ed', 750.0), ('T_Ed', 0.0), ('F_t_Ed', 0.0), ('u_C', None), ('T_Rd', 282.0), ('u_T', 0.0), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 11.77778487), ('t_req', 11.77778487), ('u_max', 0.6348420555)]),
    ('alpha1-tp10', {'alpha': 1, 'N_Ed': 800, 'M_Ed': 0, 'V_Ed': 0, 't_p': 10},
     [('A_eff', 62325.45299), ('z_C', 121.5), ('C_Ed', 400.0), ('T_Ed', 0.0), ('F_t_Ed', 0.0), ('u_C', None), ('T_Rd', 98.21428571), ('u_T', 0.0), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 13.94200482), ('t_req', 13.94200482), ('u_max', 1.359089718)]),
    ('thick-plate-100', {'t_p': 100, 'B': 1500, 'L': 1500, 'h': 600, 'b': 300, 't_f': 25, 't_w': 15, 'N_Ed': 3000, 'M_Ed': 400, 'x_b': 500, 'm': 100, 'l_eff': 500},
     [('A_eff', 884938.747), ('z_C', 287.5), ('C_Ed', 2195.652174), ('T_Ed', 0.0), ('F_t_Ed', 0.0), ('u_C', 0.3590440312), ('T_Rd', 282.0), ('u_T', 0.0), ('u_V', 0.1059024358), ('V_b_Ed', 0.0), ('t_req_N', 27.92251), ('t_req', 50.02591482), ('u_max', 0.3590440312)]),
    ('tReq-over-100', {'N_Ed': 10000, 'M_Ed': 0, 'V_Ed': 0},
     [('A_eff', 98849.98688), ('z_C', 121.5), ('C_Ed', 5000.0), ('T_Ed', 0.0), ('F_t_Ed', 0.0), ('u_C', None), ('T_Rd', 282.0), ('u_T', 0.0), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', '> 100 mm'), ('t_req', '> 100 mm'), ('u_max', 7.140945337)]),
    ('N3000-plate700', {'N_Ed': 3000, 'M_Ed': 0, 'V_Ed': 0, 'B': 700, 'L': 700, 'h': 300, 'b': 300, 'x_b': 300, 'm': 30},
     [('A_eff', 113769.9106), ('z_C', 141.5), ('C_Ed', 1500.0), ('T_Ed', 0.0), ('F_t_Ed', 0.0), ('u_C', None), ('T_Rd', 282.0), ('u_T', 0.0), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 36.36848349), ('t_req', 36.36848349), ('u_max', 1.861341938)]),
    ('CHS-thin-plate-moment', {'column': 'CHS', 'D': 219.1, 't': 8, 'B': 380, 'L': 380, 't_p': 16, 'N_Ed': 100, 'M_Ed': 60, 'V_Ed': 0, 'x_b': 150, 'm': 25},
     [('A_eff', 59288.88839), ('z_C', 71.21230217), ('C_Ed', 339.0408185), ('T_Ed', 239.0408185), ('F_t_Ed', 119.5204093), ('u_C', 0.8073112424), ('T_Rd', 281.6), ('u_T', 0.8488665431), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 10.0), ('t_req', 14.7414326), ('u_max', 0.8488665431)]),
    ('CHS-t60-bolts-govern', {'column': 'CHS', 'D': 219.1, 't': 8, 'B': 500, 'L': 500, 'N_Ed': 100, 'M_Ed': 150, 'V_Ed': 0, 'x_b': 200, 'm': 25, 'l_eff': 400, 'F_t_Rd': 250, 't_p': 60},
     [('A_eff', 196349.5408), ('z_C', 106.1032954), ('C_Ed', 555.3680818), ('T_Ed', 455.3680818), ('F_t_Ed', 227.6840409), ('u_C', 0.3993129056), ('T_Rd', 500.0), ('u_T', 0.9107361636), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 10.0), ('t_req', 21.6657503), ('u_max', 0.9107361636)]),
    ('CHS-t25-bolts-govern', {'column': 'CHS', 'D': 219.1, 't': 8, 'B': 500, 'L': 500, 'N_Ed': 100, 'M_Ed': 150, 'V_Ed': 0, 'x_b': 200, 'm': 25, 'l_eff': 400, 'F_t_Rd': 250, 't_p': 25},
     [('A_eff', 89654.53217), ('z_C', 76.38084648), ('C_Ed', 615.093275), ('T_Ed', 515.093275), ('F_t_Ed', 257.5466375), ('u_C', 0.9685700827), ('T_Rd', 500.0), ('u_T', 1.03018655), ('u_V', 0.0), ('V_b_Ed', 0.0), ('t_req_N', 10.0), ('t_req', 'bolts govern'), ('u_max', 1.03018655)]),
]


def rel(a, b):
    return abs(a - b) / max(abs(b), 1e-12) if b != 0 else abs(a)


def consistency_note():
    """Informational only (not counted in the exit code): t_req for a CHS when z_C moves with t, against the product."""
    print("\nSelf-consistent t_req for a CHS (z_C re-evaluated at every t), against the product (z_C held at the entered t_p)")
    cases = [("ka-chs-moment (KAT)", KATS["ka-chs-moment"][0], 15.39956042)]
    for name, kw, obs in EXTRAS:
        # Any column whose z_C moves with c: a CHS always, an I/H or RHS once the outstand is cut at the plate edge
        # (Addendum A fix G-A-10 made the strip centroid depend on c, so the finding now reaches those columns too).
        if kw.get("M_Ed", 0) > 0 and any(o[0] == "t_req" for o in obs) and name not in ("RHS-clip", ) and \
                isinstance(dict(obs)["t_req"], float):
            t_hold = calc(hold_forces=True, **kw)["t_req"]
            t_self = calc(**kw)["t_req"]
            if name.startswith(("RHS-clip-", "IH-cutL")) or (isinstance(t_self, float) and isinstance(t_hold, float) and abs(t_self - t_hold) > 1e-4):
                cases.append((name, kw, dict(obs)["t_req"]))
        elif name.startswith("CHS-") and kw.get("M_Ed", 0) > 0 and any(o[0] == "t_req" for o in obs):
            cases.append((name, kw, dict(obs)["t_req"]))
    for name, kw, prod_t in cases:
        c_ = calc(**kw)
        t_ = c_["t_req"]
        p = dict(DEFAULTS)
        p.update(kw)
        at_prod = utilisations(p, prod_t) if not isinstance(prod_t, str) else None
        extra = ""
        if isinstance(t_, str):
            extra = " (the search is not monotone once z_C moves with c, so no single self-consistent value exists)"
        if at_prod is not None:
            extra = f"; at the product t_req the self-consistent u_C = {at_prod['u_C'] or 0:.4f}, u_T = {at_prod['u_T']:.4f}"
        print(f"  {name:26s} product t_req = {prod_t}   self-consistent t_req = {t_ if isinstance(t_, str) else round(t_, 4)}{extra}")


def main():
    bad = 0
    total = 0
    print(f"{'case':26s} {'symbol':10s} {'KAT':>14s} {'indep':>14s} {'rel diff':>10s} {'tol':>8s} result")
    for case, (kw, exp) in KATS.items():
        r = calc(hold_forces=True, **kw)
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
        r = calc(hold_forces=True, **kw)
        for sym, pv in obs:
            v = r[sym]
            total += 1
            if isinstance(pv, str) or isinstance(v, str) or v is None or pv is None:
                ok = (pv is None and v is None) or (isinstance(pv, str) and isinstance(v, str) and v.startswith(pv))
                d = float("nan")
            else:
                d = abs(v - pv) / abs(pv) if pv != 0 else abs(v - pv)
                ok = d <= 1e-6
            bad += (not ok)
            print(f"{name:26s} {sym:10s} {str(pv):>14s} {str(v if isinstance(v, str) or v is None else round(v, 8)):>14s} {d:10.2e} {'PASS' if ok else 'FAIL'}")
    print(f"\n{total - bad}/{total} values match")
    consistency_note()
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
