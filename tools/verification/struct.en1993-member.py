#!/usr/bin/env python3
"""Independent re-derivation of struct.en1993-member (Track P, P1).

Method sources (cited by title, edition, clause; values only):
  BS EN 1993-1-1:2005+A1:2014 (Eurocode 3, steel, general rules)
    cl. 6.2.3 (tension), 6.2.4 (compression), 6.2.5 (bending),
    cl. 6.2.6 (shear, A_v for rolled I), 6.2.8 (bending and shear),
    cl. 6.2.1(7) (linear summation), cl. 6.3.1.2 and Table 6.1/6.2 (curves),
    cl. 6.3.3 (member interaction), Annex A/B (k factors, not used here).
Units inside this script: N, mm, MPa (N/mm^2). Inputs are converted at the edge.
Exit code 0 when every KAT value matches within max(KAT tol, 1e-6).
"""
import math
import sys

ALPHA = {"a0": 0.13, "a": 0.21, "b": 0.34, "c": 0.49, "d": 0.76}  # Table 6.1


def chi_buckling(lam, alpha):
    """Eq. 6.49 with 6.50, capped at 1.0 (cl. 6.3.1.2)."""
    phi = 0.5 * (1.0 + alpha * (lam - 0.2) + lam * lam)
    chi = 1.0 / (phi + math.sqrt(max(phi * phi - lam * lam, 0.0)))
    return min(chi, 1.0), phi


def check(i):
    """i: dict of inputs in the same units the calculator shows (kN, kN*m, MPa, GPa, cm^2, cm^3, cm^4, mm, m)."""
    o = {}
    fy = i["f_y"]
    E = i["E"] * 1e3  # GPa -> MPa
    gM0 = i.get("gamma_M0", 1.0)
    gM1 = i.get("gamma_M1", 1.0)
    cls = int(i.get("section_class", 1))
    A = i["A"] * 1e2
    I_y = i["I_y"] * 1e4
    I_z = i["I_z"] * 1e4
    N = i["N_Ed"] * 1e3
    My = i.get("M_y_Ed", 0.0) * 1e6
    Mz = i.get("M_z_Ed", 0.0) * 1e6
    V = i.get("V_Ed", 0.0) * 1e3
    eta = i.get("eta", 1.0)

    o["N_pl_Rd"] = A * fy / gM0 / 1e3
    Wy = (i["W_pl_y"] if cls <= 2 else i["W_el_y"]) * 1e3
    Wz = (i["W_pl_z"] if cls <= 2 else i["W_el_z"]) * 1e3
    My_c = Wy * fy / gM0
    Mz_c = Wz * fy / gM0
    o["M_y_c_Rd"] = My_c / 1e6
    o["M_z_c_Rd"] = Mz_c / 1e6

    if i.get("A_v") is not None:
        Av = i["A_v"] * 1e2
    else:
        h, b, tw, tf = (i[k] for k in ("h", "b", "t_w", "t_f"))
        r = i.get("r", 0.0)  # blank root radius is taken as 0 by the calculator
        hw = h - 2 * tf
        Av = max(A - 2 * b * tf + (tw + 2 * r) * tf, eta * hw * tw)  # cl. 6.2.6(3)(a)
    o["A_v"] = Av / 1e2
    Vpl = Av * fy / (math.sqrt(3) * gM0)
    o["V_pl_Rd"] = Vpl / 1e3
    o["u_V"] = V / Vpl

    # Flexural buckling (cl. 6.3.1.2)
    for ax, I, Lk, cv, al in (("y", I_y, i["L_cr_y"], i.get("curve_y", "b"), i.get("alpha_y")),
                              ("z", I_z, i["L_cr_z"], i.get("curve_z", "c"), i.get("alpha_z"))):
        alpha = ALPHA[cv] if al is None else al
        Ncr = math.pi ** 2 * E * I / (Lk * 1e3) ** 2
        lam = math.sqrt(A * fy / Ncr)
        chi, phi = chi_buckling(lam, alpha)
        o["N_cr_" + ax] = Ncr / 1e3
        o["lambda_" + ax] = lam
        o["chi_" + ax] = chi
        o["N_b_%s_Rd" % ax] = chi * A * fy / gM1 / 1e3
    o["N_b_Rd"] = min(o["N_b_y_Rd"], o["N_b_z_Rd"])

    # Bending and shear (cl. 6.2.8). rho = (2 V/Vpl - 1)^2 above V = 0.5 Vpl.
    # For an I section with equal flanges and bending about y, the reduced plastic modulus is
    # W_pl,y - rho*Aw^2/(4 tw)  (Eq. 6.30); the elastic/class 3 case is approximated the same way here.
    rho = (2 * V / Vpl - 1) ** 2 if V > 0.5 * Vpl else 0.0
    o["rho"] = rho
    if i.get("A_v") is None:
        Aw = (i["h"] - 2 * i["t_f"]) * i["t_w"]
        My_V = max((Wy - rho * Aw * Aw / (4 * i["t_w"])) * fy / gM0, 0.0)
        My_V = min(My_V, My_c)
    else:
        My_V = My_c  # web dimensions unknown: no EN reduction computed
    o["M_y_V_Rd"] = My_V / 1e6
    # Utilisations (linear, cl. 6.2.1(7)), using the EN reduced major-axis resistance.
    comp = N < 0
    o["u_N"] = abs(N) / (o["N_pl_Rd"] * 1e3)
    o["u_M_y"] = My / My_V if My_V > 0 else (0.0 if My == 0 else float("inf"))
    o["u_M_z"] = Mz / Mz_c
    o["u_section"] = o["u_N"] + o["u_M_y"] + o["u_M_z"]
    if comp:
        k_yy = i.get("k_yy", 1.0)
        k_zz = i.get("k_zz", 1.0)
        o["u_buckling"] = abs(N) / (o["N_b_Rd"] * 1e3)
        # Eq. 6.61/6.62 divide the moments by M_Rk/gamma_M1 (chi_LT = 1 here; no shear reduction in 6.3.3).
        # Review 2026-10-24: the first pass used M_c,Rd with gamma_M0, which copied the product. See finding R2.
        o["u_member"] = o["u_buckling"] + k_yy * My / (Wy * fy / gM1) + k_zz * Mz / (Wz * fy / gM1)
    else:
        o["u_buckling"] = 0.0  # not applicable in tension or at zero axial force
        o["u_member"] = float("nan")  # EN 1993-1-1 has no member buckling check without compression
    return o


# ---- Known-answer cases (inputs copied from the KAT templates) -------------
UC = dict(N_Ed=-900, M_y_Ed=0, M_z_Ed=0, V_Ed=0, f_y=355, E=210, section_class=1, A=58.7,
          W_pl_y=497, W_el_y=450, W_pl_z=231, W_el_z=152, I_y=4568, I_z=1548,
          h=203.2, b=203.6, t_w=7.2, t_f=11, r=10.2, L_cr_y=4, L_cr_z=4, curve_y="b", curve_z="c")
UB = dict(N_Ed=0, M_y_Ed=120, M_z_Ed=0, V_Ed=100, f_y=275, E=210, section_class=1, A=51.3,
          W_pl_y=623, W_el_y=560, W_pl_z=142, W_el_z=92.6, I_y=8503, I_z=764,
          h=303.4, b=165, t_w=6, t_f=10.2, r=8.9, L_cr_y=6, L_cr_z=1.5, curve_y="a", curve_z="b")
TIE = dict(N_Ed=400, M_y_Ed=0, M_z_Ed=5, V_Ed=0, f_y=355, E=210, section_class=3, A=20,
           W_pl_y=100, W_el_y=80, W_pl_z=40, W_el_z=30, I_y=1000, I_z=200, A_v=8,
           L_cr_y=3, L_cr_z=3, curve_y="b", curve_z="c")

KATS = [
    ("ka-uc-compression", UC, [("N_pl_Rd", 2083.85, 1e-4), ("N_cr_z", 2005.3, 1e-3), ("lambda_z", 1.0194, 1e-3),
                               ("chi_z", 0.5288, 1e-3), ("N_b_z_Rd", 1101.9, 1e-3), ("u_buckling", 0.8168, 1e-3)]),
    ("ka-ub-bending-shear", UB, [("M_y_c_Rd", 171.325, 1e-4), ("A_v", 20.068, 1e-4), ("V_pl_Rd", 318.62, 1e-3),
                                 ("u_M_y", 0.70043, 1e-3), ("u_V", 0.31385, 1e-3)]),
    ("ka-tension-class3", TIE, [("N_pl_Rd", 710, 1e-9), ("M_z_c_Rd", 10.65, 1e-9), ("u_section", 1.03294, 1e-4)]),
]



# ---- Extra checks: product values observed with tools/verification/probe (Release build, v0.26.0) ----
# Each entry: probe name, inputs, product outputs (kN, kN*m, cm^2, dimensionless).
PRODUCT = [
    ('P1_highshear', {'N_Ed': 0, 'M_y_Ed': 120, 'M_z_Ed': 0, 'V_Ed': 250, 'f_y': 275, 'E': 210, 'section_class': 1, 'A': 51.3, 'W_pl_y': 623, 'W_el_y': 560, 'W_pl_z': 142, 'W_el_z': 92.6, 'I_y': 8503, 'I_z': 764, 'h': 303.4, 'b': 165, 't_w': 6, 't_f': 10.2, 'r': 8.9, 'L_cr_y': 6, 'L_cr_z': 1.5, 'curve_y': 'a', 'curve_z': 'b'},
     {'A_v': 20.0676, 'V_pl_Rd': 318.6159422, 'rho': 0.324088203, 'M_y_c_Rd': 115.8005887, 'M_z_c_Rd': 26.39435569, 'N_cr_y': 4895.40603, 'N_cr_z': 7037.685912, 'lambda_y': 0.536822451, 'lambda_z': 0.447723705, 'chi_y': 0.912436565, 'chi_z': 0.906672416, 'N_b_Rd': 1279.088111, 'u_N': 0.0, 'u_M_y': 1.036264162, 'u_M_z': 0.0, 'u_V': 0.784643726, 'u_section': 1.036264162, 'u_buckling': 0.0, 'u_member': 1.036264162}),
    ('P2_V_eq_Vpl', {'N_Ed': 0, 'M_y_Ed': 0, 'M_z_Ed': 0, 'V_Ed': 318.61594422, 'f_y': 275, 'E': 210, 'section_class': 1, 'A': 51.3, 'W_pl_y': 623, 'W_el_y': 560, 'W_pl_z': 142, 'W_el_z': 92.6, 'I_y': 8503, 'I_z': 764, 'h': 303.4, 'b': 165, 't_w': 6, 't_f': 10.2, 'r': 8.9, 'L_cr_y': 6, 'L_cr_z': 1.5, 'curve_y': 'a', 'curve_z': 'b'},
     {'A_v': 20.0676, 'V_pl_Rd': 318.6159422, 'rho': 1.000000025, 'M_y_c_Rd': -4.335e-06, 'M_z_c_Rd': -9.88e-07, 'N_cr_y': 4895.40603, 'N_cr_z': 7037.685912, 'lambda_y': 0.536822451, 'lambda_z': 0.447723705, 'chi_y': 0.912436565, 'chi_z': 0.906672416, 'N_b_Rd': 1279.088111, 'u_N': 0.0, 'u_M_y': -0.0, 'u_M_z': -0.0, 'u_V': 1.000000006, 'u_section': 0.0, 'u_buckling': 0.0, 'u_member': -0.0}),
    ('P3_V_1p2Vpl', {'N_Ed': 0, 'M_y_Ed': 50, 'M_z_Ed': 0, 'V_Ed': 382.339133064, 'f_y': 275, 'E': 210, 'section_class': 1, 'A': 51.3, 'W_pl_y': 623, 'W_el_y': 560, 'W_pl_z': 142, 'W_el_z': 92.6, 'I_y': 8503, 'I_z': 764, 'h': 303.4, 'b': 165, 't_w': 6, 't_f': 10.2, 'r': 8.9, 'L_cr_y': 6, 'L_cr_z': 1.5, 'curve_y': 'a', 'curve_z': 'b'},
     {'A_v': 20.0676, 'V_pl_Rd': 318.6159422, 'rho': 1.960000043, 'M_y_c_Rd': -164.4720073, 'M_z_c_Rd': -37.48800166, 'N_cr_y': 4895.40603, 'N_cr_z': 7037.685912, 'lambda_y': 0.536822451, 'lambda_z': 0.447723705, 'chi_y': 0.912436565, 'chi_z': 0.906672416, 'N_b_Rd': 1279.088111, 'u_N': 0.0, 'u_M_y': -0.304003099, 'u_M_z': -0.0, 'u_V': 1.200000008, 'u_section': -0.304003099, 'u_buckling': 0.0, 'u_member': -0.304003099}),
    ('P4_slender', {'N_Ed': -50, 'M_y_Ed': 0, 'M_z_Ed': 0, 'V_Ed': 0, 'f_y': 355, 'E': 210, 'section_class': 1, 'A': 58.7, 'W_pl_y': 497, 'W_el_y': 450, 'W_pl_z': 231, 'W_el_z': 152, 'I_y': 4568, 'I_z': 1548, 'h': 203.2, 'b': 203.6, 't_w': 7.2, 't_f': 11, 'r': 10.2, 'L_cr_y': 20, 'L_cr_z': 20, 'curve_y': 'b', 'curve_z': 'c'},
     {'A_v': 16.944, 'V_pl_Rd': 347.2831151, 'rho': 0.0, 'M_y_c_Rd': 176.435, 'M_z_c_Rd': 82.005, 'N_cr_y': 236.6928527, 'N_cr_z': 80.21027497, 'lambda_y': 2.967157878, 'lambda_z': 5.097042145, 'chi_y': 0.101510944, 'chi_z': 0.035128701, 'N_b_Rd': 73.20294398, 'u_N': 0.023994049, 'u_M_y': 0.0, 'u_M_z': 0.0, 'u_V': 0.0, 'u_section': 0.023994049, 'u_buckling': 0.683032639, 'u_member': 0.683032639}),
    ('P5_stocky', {'N_Ed': -900, 'M_y_Ed': 0, 'M_z_Ed': 0, 'V_Ed': 0, 'f_y': 355, 'E': 210, 'section_class': 1, 'A': 58.7, 'W_pl_y': 497, 'W_el_y': 450, 'W_pl_z': 231, 'W_el_z': 152, 'I_y': 4568, 'I_z': 1548, 'h': 203.2, 'b': 203.6, 't_w': 7.2, 't_f': 11, 'r': 10.2, 'L_cr_y': 0.5, 'L_cr_z': 0.5, 'curve_y': 'b', 'curve_z': 'c'},
     {'A_v': 16.944, 'V_pl_Rd': 347.2831151, 'rho': 0.0, 'M_y_c_Rd': 176.435, 'M_z_c_Rd': 82.005, 'N_cr_y': 378708.5644, 'N_cr_z': 128336.4399, 'lambda_y': 0.074178947, 'lambda_z': 0.127426054, 'chi_y': 1.0, 'chi_z': 1.0, 'N_b_Rd': 2083.85, 'u_N': 0.431892891, 'u_M_y': 0.0, 'u_M_z': 0.0, 'u_V': 0.0, 'u_section': 0.431892891, 'u_buckling': 0.431892891, 'u_member': 0.431892891}),
    ('P6_alpha', {'N_Ed': -900, 'M_y_Ed': 0, 'M_z_Ed': 0, 'V_Ed': 0, 'f_y': 355, 'E': 210, 'section_class': 1, 'A': 58.7, 'W_pl_y': 497, 'W_el_y': 450, 'W_pl_z': 231, 'W_el_z': 152, 'I_y': 4568, 'I_z': 1548, 'h': 203.2, 'b': 203.6, 't_w': 7.2, 't_f': 11, 'r': 10.2, 'L_cr_y': 4, 'L_cr_z': 4, 'curve_y': 'b', 'curve_z': 'c', 'alpha_z': 0.2, 'alpha_y': 0.2},
     {'A_v': 16.944, 'V_pl_Rd': 347.2831151, 'rho': 0.0, 'M_y_c_Rd': 176.435, 'M_z_c_Rd': 82.005, 'N_cr_y': 5917.321319, 'N_cr_z': 2005.256874, 'lambda_y': 0.593431576, 'lambda_z': 1.019408429, 'chi_y': 0.896852169, 'chi_z': 0.658371079, 'N_b_Rd': 1371.946573, 'u_N': 0.431892891, 'u_M_y': 0.0, 'u_M_z': 0.0, 'u_V': 0.0, 'u_section': 0.431892891, 'u_buckling': 0.656002222, 'u_member': 0.656002222}),
    ('P7_gammas', {'N_Ed': -900, 'M_y_Ed': 40, 'M_z_Ed': 0, 'V_Ed': 0, 'f_y': 355, 'E': 210, 'section_class': 1, 'A': 58.7, 'W_pl_y': 497, 'W_el_y': 450, 'W_pl_z': 231, 'W_el_z': 152, 'I_y': 4568, 'I_z': 1548, 'h': 203.2, 'b': 203.6, 't_w': 7.2, 't_f': 11, 'r': 10.2, 'L_cr_y': 4, 'L_cr_z': 4, 'curve_y': 'b', 'curve_z': 'c', 'gamma_M0': 1.05, 'gamma_M1': 1.1},
     {'A_v': 16.944, 'V_pl_Rd': 330.7458239, 'rho': 0.0, 'M_y_c_Rd': 168.0333333, 'M_z_c_Rd': 78.1, 'N_cr_y': 5917.321319, 'N_cr_z': 2005.256874, 'lambda_y': 0.593431576, 'lambda_z': 1.019408429, 'chi_y': 0.840341654, 'chi_z': 0.528759011, 'N_b_Rd': 1001.685878, 'u_N': 0.453487535, 'u_M_y': 0.238048006, 'u_M_z': 0.0, 'u_V': 0.0, 'u_section': 0.691535541, 'u_buckling': 0.898485264, 'u_member': 1.13653327}),
    ('P8_class2', {'N_Ed': 0, 'M_y_Ed': 100, 'M_z_Ed': 0, 'V_Ed': 100, 'f_y': 275, 'E': 210, 'section_class': '2', 'A': 51.3, 'W_pl_y': 623, 'W_el_y': 560, 'W_pl_z': 142, 'W_el_z': 92.6, 'I_y': 8503, 'I_z': 764, 'h': 303.4, 'b': 165, 't_w': 6, 't_f': 10.2, 'r': 8.9, 'L_cr_y': 6, 'L_cr_z': 1.5, 'curve_y': 'a', 'curve_z': 'b'},
     {'A_v': 20.0676, 'V_pl_Rd': 318.6159422, 'rho': 0.0, 'M_y_c_Rd': 171.325, 'M_z_c_Rd': 39.05, 'N_cr_y': 4895.40603, 'N_cr_z': 7037.685912, 'lambda_y': 0.536822451, 'lambda_z': 0.447723705, 'chi_y': 0.912436565, 'chi_z': 0.906672416, 'N_b_Rd': 1279.088111, 'u_N': 0.0, 'u_M_y': 0.583685977, 'u_M_z': 0.0, 'u_V': 0.31385749, 'u_section': 0.583685977, 'u_buckling': 0.0, 'u_member': 0.583685977}),
    ('P8b_class3', {'N_Ed': 0, 'M_y_Ed': 100, 'M_z_Ed': 0, 'V_Ed': 100, 'f_y': 275, 'E': 210, 'section_class': '3', 'A': 51.3, 'W_pl_y': 623, 'W_el_y': 560, 'W_pl_z': 142, 'W_el_z': 92.6, 'I_y': 8503, 'I_z': 764, 'h': 303.4, 'b': 165, 't_w': 6, 't_f': 10.2, 'r': 8.9, 'L_cr_y': 6, 'L_cr_z': 1.5, 'curve_y': 'a', 'curve_z': 'b'},
     {'A_v': 20.0676, 'V_pl_Rd': 318.6159422, 'rho': 0.0, 'M_y_c_Rd': 154.0, 'M_z_c_Rd': 25.465, 'N_cr_y': 4895.40603, 'N_cr_z': 7037.685912, 'lambda_y': 0.536822451, 'lambda_z': 0.447723705, 'chi_y': 0.912436565, 'chi_z': 0.906672416, 'N_b_Rd': 1279.088111, 'u_N': 0.0, 'u_M_y': 0.649350649, 'u_M_z': 0.0, 'u_V': 0.31385749, 'u_section': 0.649350649, 'u_buckling': 0.0, 'u_member': 0.649350649}),
    ('P9_tension_mom', {'N_Ed': 300, 'M_y_Ed': 50, 'M_z_Ed': 8, 'V_Ed': 0, 'f_y': 355, 'E': 210, 'section_class': 1, 'A': 58.7, 'W_pl_y': 497, 'W_el_y': 450, 'W_pl_z': 231, 'W_el_z': 152, 'I_y': 4568, 'I_z': 1548, 'h': 203.2, 'b': 203.6, 't_w': 7.2, 't_f': 11, 'r': 10.2, 'L_cr_y': 4, 'L_cr_z': 4, 'curve_y': 'b', 'curve_z': 'c'},
     {'A_v': 16.944, 'V_pl_Rd': 347.2831151, 'rho': 0.0, 'M_y_c_Rd': 176.435, 'M_z_c_Rd': 82.005, 'N_cr_y': 5917.321319, 'N_cr_z': 2005.256874, 'lambda_y': 0.593431576, 'lambda_z': 1.019408429, 'chi_y': 0.840341654, 'chi_z': 0.528759011, 'N_b_Rd': 1101.854466, 'u_N': 0.143964297, 'u_M_y': 0.283390484, 'u_M_z': 0.097555027, 'u_V': 0.0, 'u_section': 0.524909808, 'u_buckling': 0.0, 'u_member': 0.380945511}),
    ('P10_comp_mom', {'N_Ed': -500, 'M_y_Ed': 100, 'M_z_Ed': 10, 'V_Ed': 0, 'f_y': 355, 'E': 210, 'section_class': 1, 'A': 58.7, 'W_pl_y': 497, 'W_el_y': 450, 'W_pl_z': 231, 'W_el_z': 152, 'I_y': 4568, 'I_z': 1548, 'h': 203.2, 'b': 203.6, 't_w': 7.2, 't_f': 11, 'r': 10.2, 'L_cr_y': 4, 'L_cr_z': 4, 'curve_y': 'b', 'curve_z': 'c', 'k_yy': 1.1, 'k_zz': 1.2},
     {'A_v': 16.944, 'V_pl_Rd': 347.2831151, 'rho': 0.0, 'M_y_c_Rd': 176.435, 'M_z_c_Rd': 82.005, 'N_cr_y': 5917.321319, 'N_cr_z': 2005.256874, 'lambda_y': 0.593431576, 'lambda_z': 1.019408429, 'chi_y': 0.840341654, 'chi_z': 0.528759011, 'N_b_Rd': 1101.854466, 'u_N': 0.239940495, 'u_M_y': 0.566780967, 'u_M_z': 0.121943784, 'u_V': 0.0, 'u_section': 0.928665246, 'u_buckling': 0.453780436, 'u_member': 1.223572041}),
    ('P11_zero', {'N_Ed': 0, 'M_y_Ed': 0, 'M_z_Ed': 0, 'V_Ed': 0, 'f_y': 355, 'E': 210, 'section_class': 1, 'A': 58.7, 'W_pl_y': 497, 'W_el_y': 450, 'W_pl_z': 231, 'W_el_z': 152, 'I_y': 4568, 'I_z': 1548, 'h': 203.2, 'b': 203.6, 't_w': 7.2, 't_f': 11, 'r': 10.2, 'L_cr_y': 4, 'L_cr_z': 4, 'curve_y': 'b', 'curve_z': 'c'},
     {'A_v': 16.944, 'V_pl_Rd': 347.2831151, 'rho': 0.0, 'M_y_c_Rd': 176.435, 'M_z_c_Rd': 82.005, 'N_cr_y': 5917.321319, 'N_cr_z': 2005.256874, 'lambda_y': 0.593431576, 'lambda_z': 1.019408429, 'chi_y': 0.840341654, 'chi_z': 0.528759011, 'N_b_Rd': 1101.854466, 'u_N': 0.0, 'u_M_y': 0.0, 'u_M_z': 0.0, 'u_V': 0.0, 'u_section': 0.0, 'u_buckling': 0.0, 'u_member': 0.0}),
    ('P12_large', {'N_Ed': -80000.0, 'M_y_Ed': 50000.0, 'M_z_Ed': 20000.0, 'V_Ed': 90000.0, 'f_y': 700, 'E': 210, 'section_class': 1, 'A': 50000.0, 'W_pl_y': 10000000.0, 'W_el_y': 9000000.0, 'W_pl_z': 5000000.0, 'W_el_z': 4000000.0, 'I_y': 1000000000.0, 'I_z': 500000000.0, 'h': 203.2, 'b': 203.6, 't_w': 7.2, 't_f': 11, 'r': 10.2, 'L_cr_y': 50, 'L_cr_z': 50, 'curve_y': 'b', 'curve_z': 'c', 'A_v': 20000.0},
     {'A_v': 20000.0, 'V_pl_Rd': 808290.3769, 'rho': 0.0, 'M_y_c_Rd': 7000000.0, 'M_z_c_Rd': 3500000.0, 'N_cr_y': 8290467.697000001, 'N_cr_z': 4145233.848, 'lambda_y': 0.649747334, 'lambda_z': 0.918881492, 'chi_y': 0.811310753, 'chi_z': 0.588273105, 'N_b_Rd': 2058955.867, 'u_N': 0.022857143, 'u_M_y': 0.007142857, 'u_M_z': 0.005714286, 'u_V': 0.111346123, 'u_section': 0.035714286, 'u_buckling': 0.038854645, 'u_member': 0.051711788}),
    ('P13_helper_eta', {'N_Ed': 0, 'M_y_Ed': 2000, 'M_z_Ed': 0, 'V_Ed': 1500, 'f_y': 355, 'E': 210, 'section_class': 1, 'A': 138, 'W_pl_y': 4381, 'W_el_y': 3900, 'W_pl_z': 231, 'W_el_z': 152, 'I_y': 176400, 'I_z': 1548, 'L_cr_y': 4, 'L_cr_z': 4, 'curve_y': 'b', 'curve_z': 'c', 'h': 1000, 'b': 200, 't_w': 10, 't_f': 10, 'r': 0, 'eta': 1.2},
     {'A_v': 117.6, 'V_pl_Rd': 2410.321904, 'rho': 0.05985217, 'M_y_c_Rd': 1462.169614, 'M_z_c_Rd': 77.09682284, 'N_cr_y': 228506.0159, 'N_cr_z': 2005.256874, 'lambda_y': 0.146421516, 'lambda_z': 1.563035035, 'chi_y': 1.0, 'chi_z': 0.294952814, 'N_b_Rd': 1444.973837, 'u_N': 0.0, 'u_M_y': 1.367830367, 'u_M_z': 0.0, 'u_V': 0.622323515, 'u_section': 1.367830367, 'u_buckling': 0.0, 'u_member': 1.367830367}),
    ('P14_helper_no_r', {'N_Ed': 0, 'M_y_Ed': 0, 'M_z_Ed': 0, 'V_Ed': 100, 'f_y': 355, 'E': 210, 'section_class': 1, 'A': 51.3, 'W_pl_y': 497, 'W_el_y': 450, 'W_pl_z': 231, 'W_el_z': 152, 'I_y': 4568, 'I_z': 1548, 'L_cr_y': 4, 'L_cr_z': 4, 'curve_y': 'b', 'curve_z': 'c', 'h': 303.4, 'b': 165, 't_w': 6, 't_f': 10.2},
     {'A_v': 18.252, 'V_pl_Rd': 374.0917975, 'rho': 0.0, 'M_y_c_Rd': 176.435, 'M_z_c_Rd': 82.005, 'N_cr_y': 5917.321319, 'N_cr_z': 2005.256874, 'lambda_y': 0.55476657, 'lambda_z': 0.952988922, 'chi_y': 0.859122911, 'chi_z': 0.567663347, 'N_b_Rd': 1033.800105, 'u_N': 0.0, 'u_M_y': 0.0, 'u_M_z': 0.0, 'u_V': 0.267314067, 'u_section': 0.0, 'u_buckling': 0.0, 'u_member': 0.0}),
]
# Symbols that depend on the shear-reduced bending resistance. The product uses (1 - rho) x the whole
# resistance; EN 1993-1-1 6.2.8 with Eq. 6.30 reduces only the web part. See finding P1-struct.en1993-member-01 (report).
SHEAR_SENSITIVE = {"M_y_c_Rd": "M_y_V_Rd", "M_z_c_Rd": "M_z_c_Rd", "u_M_y": "u_M_y", "u_M_z": "u_M_z",
                   "u_section": "u_section", "u_member": "u_member"}


def extra_checks():
    fails = findings = 0
    print("\nEXTRA CHECKS (product observed vs independent)")
    print("%-16s %-11s %12s %12s %10s  %s" % ("probe", "symbol", "product", "independent", "rel.diff", "result"))
    for name, inp, prod in PRODUCT:
        ind = check(inp)
        tension = inp["N_Ed"] >= 0
        for sym, pv in prod.items():
            if sym == "u_member" and tension:
                continue  # product adds u_M_y + u_M_z in tension; EN has no member check there (finding 04)
            key = SHEAR_SENSITIVE.get(sym, sym) if ind["rho"] > 0 else sym
            iv = ind[key]
            den = max(abs(iv), 1e-3)
            rd = abs(pv - iv) / den
            if rd <= 1e-6:
                res = "PASS"
            elif sym in SHEAR_SENSITIVE and ind["rho"] > 0:
                res = "FINDING-01"
                findings += 1
            elif sym == "u_member" and inp.get("gamma_M1", 1.0) != inp.get("gamma_M0", 1.0):
                res = "FINDING-R2"  # product divides the member moments by M_c,Rd (gamma_M0), EN uses gamma_M1
                findings += 1
            else:
                res = "FAIL"
                fails += 1
            print("%-16s %-11s %12.6g %12.6g %10.2e  %s" % (name, sym, pv, iv, rd, res))
    return fails, findings


def annex_b_example():
    """Information only: default k_yy = 1.0 against the Annex B formula (finding 02).
    Section: hypothetical square hollow section, class 1, S355, curve a, L = 3 m, uniform moment (C_my = 1).
    Annex B Table B.1, I-type and hollow sections, class 1-2: k_yy = C_my (1 + (lambda_y - 0.2) n_y) <= C_my (1 + 0.8 n_y).
    Eq. 6.61 with chi_LT = 1 and k_yz term zero (M_z = 0)."""
    A, Wpl, I, fy, E, L = 4000.0, 150e3, 1200e4, 355.0, 210e3, 3000.0
    N, M = 700e3, 20e6
    Ncr = math.pi ** 2 * E * I / L ** 2
    lam = math.sqrt(A * fy / Ncr)
    chi, _ = chi_buckling(lam, 0.21)
    n = N / (chi * A * fy)
    kyy = min(1.0 * (1 + (lam - 0.2) * n), 1.0 * (1 + 0.8 * n))
    m = M / (Wpl * fy)
    u_default = n + 1.0 * m
    u_annexb = n + kyy * m
    print("\nINFO Annex B check of the default k_yy = 1.0 (hypothetical SHS, N = 700 kN, M_y = 20 kN*m)")
    print("  lambda = %.4f, chi = %.4f, n_y = %.4f, k_yy(Annex B) = %.4f" % (lam, chi, n, kyy))
    print("  member utilisation with default k_yy = 1.0: %.4f" % u_default)
    print("  member utilisation with Annex B k_yy:       %.4f" % u_annexb)
    # Product values observed with the probe: u_member 0.962652 (PASS) at k_yy = 1.0, and 1.076605 (FAIL) at k_yy = 1.3034.
    for label, prod_u, ind_u in (("k_yy = 1.0", 0.9626521675, u_default), ("k_yy = 1.3034", 1.076605219, u_annexb)):
        print("  product vs independent, %s: %.6f vs %.6f, rel.diff %.1e" % (label, prod_u, ind_u, abs(prod_u - ind_u) / ind_u))
    return u_default, u_annexb


def main():
    fails = 0
    print("%-22s %-11s %12s %12s %10s %8s  %s" % ("case", "symbol", "KAT", "independent", "rel.diff", "KAT tol", "result"))
    for name, inp, exp in KATS:
        out = check(inp)
        for sym, val, tol in exp:
            ind = out[sym]
            rd = abs(ind - val) / abs(val) if val else abs(ind)
            ok = rd <= max(tol, 1e-6)
            fails += not ok
            print("%-22s %-11s %12.5g %12.5g %10.2e %8.0e  %s" % (name, sym, val, ind, rd, tol, "PASS" if ok else "FAIL"))
    ef, nf = extra_checks()
    fails += ef
    ud, ua = annex_b_example()
    print("\nExtra-check findings (expected differences, not counted as failures): %d" % nf)
    print("\nTOTAL FAILS: %d" % fails)
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
