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
        h, b, tw, tf, r = (i[k] for k in ("h", "b", "t_w", "t_f", "r"))
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

    # Utilisations (linear, cl. 6.2.1(7)); no shear reduction applied in this base form.
    comp = N < 0
    o["u_N"] = abs(N) / (o["N_pl_Rd"] * 1e3)
    o["u_M_y"] = My / My_c
    o["u_M_z"] = Mz / Mz_c
    o["u_section"] = o["u_N"] + o["u_M_y"] + o["u_M_z"]
    if comp:
        k_yy = i.get("k_yy", 1.0)
        k_zz = i.get("k_zz", 1.0)
        o["u_buckling"] = abs(N) / (o["N_b_Rd"] * 1e3)
        o["u_member"] = o["u_buckling"] + k_yy * o["u_M_y"] + k_zz * o["u_M_z"]
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
    # EXTRA_CHECKS_BELOW
    print("\nTOTAL FAILS: %d" % fails)
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
