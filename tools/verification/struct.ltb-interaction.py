#!/usr/bin/env python3
"""Independent re-derivation of struct.ltb-interaction (Track P, P1).

Method sources (cited by title, edition, clause or table; values only):
  BS EN 1993-1-1:2005+A1:2014 (Eurocode 3, steel, general rules)
    cl. 6.3.1.2-6.3.1.3 and Tables 6.1, 6.2 (flexural buckling curves)
    cl. 6.3.2.2 and Table 6.4 (LTB, general case)
    cl. 6.3.2.3, Tables 6.5, 6.6 (LTB, rolled and equivalent welded sections, f factor)
    cl. 6.3.3 eq. 6.61, 6.62 (member interaction)
    Annex A, Tables A.1, A.2 (method 1) and Annex B, Tables B.1 to B.3 (method 2)
  NCCI SN003 (elastic critical moment of a doubly symmetric section, general expression)
  ENV 1993-1-1 Annex F style C1 curve fit for end moments (C1 = 1.88 - 1.40 psi + 0.52 psi^2 <= 2.70)
Units inside this script: N, mm, MPa. Inputs are converted at the edge from the units the sheet shows
(kN, kN*m, MPa, GPa, cm^2, cm^3, cm^4, dm^6, mm, m).
Exit code 0 when every KAT value and extra check matches within max(KAT tol, 1e-6).
"""
import math
import sys

PI = math.pi
ALPHA = {"a0": 0.13, "a": 0.21, "b": 0.34, "c": 0.49, "d": 0.76}  # Table 6.1 (recommended values)


# ---------------------------------------------------------------- curve selection
def curve_ltb(general, welded, h_over_b):
    """Table 6.4 (general case) and Table 6.5 (rolled / welded equivalent sections)."""
    deep = h_over_b > 2.0
    if general:
        return ("d" if deep else "c") if welded else ("b" if deep else "a")
    return ("d" if deep else "c") if welded else ("c" if deep else "b")


def curve_flex(welded, h_over_b, tf, axis):
    """Table 6.2, columns S 235 to S 420 only. Returns None when the table has no row."""
    if welded:
        if tf <= 40:
            return "b" if axis == "y" else "c"
        return "c" if axis == "y" else "d"
    if h_over_b > 1.2:
        if tf <= 40:
            return "a" if axis == "y" else "b"
        if tf <= 100:
            return "b" if axis == "y" else "c"
        return None
    if tf <= 100:
        return "b" if axis == "y" else "c"
    return "d"


# ---------------------------------------------------------------- reduction factors
def chi_flex(lam, alpha):
    """Eq. 6.49 with 6.50."""
    phi = 0.5 * (1.0 + alpha * (lam - 0.2) + lam * lam)
    return min(1.0, 1.0 / (phi + math.sqrt(max(phi * phi - lam * lam, 0.0)))), phi


def chi_ltb_general(lam, alpha):
    """Eq. 6.56 with 6.57 (cl. 6.3.2.2)."""
    phi = 0.5 * (1.0 + alpha * (lam - 0.2) + lam * lam)
    return min(1.0, 1.0 / (phi + math.sqrt(max(phi * phi - lam * lam, 0.0)))), phi


def chi_ltb_rolled(lam, alpha, lam0, beta):
    """Cl. 6.3.2.3: chi <= 1 and chi <= 1/lam^2."""
    phi = 0.5 * (1.0 + alpha * (lam - lam0) + beta * lam * lam)
    chi = 1.0 / (phi + math.sqrt(max(phi * phi - beta * lam * lam, 0.0)))
    return min(chi, 1.0, 1.0 / (lam * lam)), phi


# ---------------------------------------------------------------- M_cr
def mcr_general(E, G, Iz, It, Iw, L, k, kw, C1, C2, zg):
    """NCCI SN003 general expression for a doubly symmetric I section (z_j = 0). N, mm."""
    kl = k * L
    a = (k / kw) ** 2 * Iw / Iz
    b = kl * kl * G * It / (PI * PI * E * Iz)
    c2zg = C2 * zg
    return C1 * PI * PI * E * Iz / (kl * kl) * (math.sqrt(a + b + c2zg * c2zg) - c2zg)


def c1_from_psi(psi):
    return min(1.88 - 1.40 * psi + 0.52 * psi * psi, 2.70)


def cm_linear(psi):
    """Annex B Table B.3, linear diagram."""
    return max(0.6 + 0.4 * psi, 0.4)


# ---------------------------------------------------------------- full analysis
def analyse(i):
    """i: inputs in the sheet units. Returns outputs in the sheet units."""
    o = {}
    fy = i.get("f_y", 355.0)
    E = i.get("E", 210.0) * 1e3
    G = i.get("G", 81.0) * 1e3
    gM1 = i.get("gamma_M1", 1.0)
    cls = int(i.get("section_class", 1))
    welded = i.get("section_type", "rolled") == "welded"
    h, b, tf = i["h"], i["b"], i["t_f"]
    A = i["A"] * 1e2
    Iy, Iz, It = i["I_y"] * 1e4, i["I_z"] * 1e4, i["I_t"] * 1e4
    Iw = i["I_w"] * 1e12
    Wply, Wely = i["W_pl_y"] * 1e3, i["W_el_y"] * 1e3
    Wplz, Welz = i["W_pl_z"] * 1e3, i["W_el_z"] * 1e3
    L = i.get("L", 6.0) * 1e3
    k, kw = i.get("k", 1.0), i.get("k_w", 1.0)
    zg = i.get("z_g", 0.0)
    C2 = i.get("C_2", 0.0)
    general = i.get("ltb_method", "rolled") == "general"
    alpha = dict(ALPHA)
    alpha.update(i.get("alphas", {}))
    lam0_ltb = i.get("lambda_LT0", 0.4)
    beta = i.get("beta_LT", 0.75)
    N = -i.get("N_Ed", 0.0) * 1e3  # compression positive
    Lcry, Lcrz = i.get("L_cr_y", 6.0) * 1e3, i.get("L_cr_z", 6.0) * 1e3

    # moment diagram
    if i.get("moment_mode", "psi") == "ends":
        MA, MB = i["M_y_A"], i["M_y_B"]
        # psi = M_small / M_large with the signed ordinates: same sign = single curvature (psi > 0)
        large, small = (MA, MB) if abs(MA) >= abs(MB) else (MB, MA)
        psi_y = small / large if large != 0 else 1.0
        My = abs(large) * 1e6
    else:
        psi_y = i.get("psi_y", 1.0)
        My = i.get("M_y_Ed", 0.0) * 1e6
    psi_z = i.get("psi_z", 1.0)
    Mz = i.get("M_z_Ed", 0.0) * 1e6
    C1_in = i.get("C_1")
    C1 = C1_in if C1_in is not None else c1_from_psi(psi_y)
    o["C_1"] = C1

    # ---- 6.3.2: LTB
    Wy = Wply if cls <= 2 else Wely
    Wz = Wplz if cls <= 2 else Welz
    Mcr = mcr_general(E, G, Iz, It, Iw, L, k, kw, C1, C2, zg)
    lamLT = math.sqrt(Wy * fy / Mcr)
    crv = i.get("ltb_curve", "auto")
    if crv == "auto":
        crv = curve_ltb(general, welded, h / b)
    aLT = alpha[crv]
    if general:
        chiLT, phiLT = chi_ltb_general(lamLT, aLT)
    else:
        chiLT, phiLT = chi_ltb_rolled(lamLT, aLT, lam0_ltb, beta)
    o.update(Mcr=Mcr / 1e6, lambda_LT=lamLT, alpha_LT=aLT, Phi_LT=phiLT, chi_LT_0=chiLT, curve_LT=crv)
    chiLT_use = chiLT
    if i.get("use_f", False) and not general:
        if i.get("k_c") is not None:
            kc = i["k_c"]
        else:
            kc = 1.0 / (1.33 - 0.33 * psi_y)
        f = min(1.0, 1.0 - 0.5 * (1.0 - kc) * (1.0 - 2.0 * (lamLT - 0.8) ** 2))
        chi_mod_nocap = min(1.0, chiLT / f)
        chiLT_use = min(1.0, chiLT / f, 1.0 / lamLT ** 2)
        o.update(k_c=kc, f=f, chi_LT_mod_no_cap2=chi_mod_nocap)
    o["chi_LT"] = chiLT_use
    MbRd = chiLT_use * Wy * fy / gM1
    o["Mb_Rd"] = MbRd / 1e6
    o["u_LTB"] = My / MbRd

    # ---- 6.3.1: flexural buckling
    NRk = A * fy
    Ncry = PI * PI * E * Iy / Lcry ** 2
    Ncrz = PI * PI * E * Iz / Lcrz ** 2
    lamy = math.sqrt(NRk / Ncry)
    lamz = math.sqrt(NRk / Ncrz)
    cy = i.get("curve_y", "auto")
    cz = i.get("curve_z", "auto")
    if cy == "auto":
        cy = curve_flex(welded, h / b, tf, "y")
    if cz == "auto":
        cz = curve_flex(welded, h / b, tf, "z")
    chiy, _ = chi_flex(lamy, alpha[cy])
    chiz, _ = chi_flex(lamz, alpha[cz])
    NbRdy = chiy * NRk / gM1
    NbRdz = chiz * NRk / gM1
    NbRd = min(NbRdy, NbRdz)
    o.update(N_Rk=NRk / 1e3, N_cr_y=Ncry / 1e3, N_cr_z=Ncrz / 1e3, lambda_y=lamy, lambda_z=lamz,
             chi_y=chiy, chi_z=chiz, N_b_y_Rd=NbRdy / 1e3, N_b_z_Rd=NbRdz / 1e3, N_b_Rd=NbRd / 1e3,
             u_buckling=N / NbRd if N > 0 else 0.0, curve_y=cy, curve_z=cz)

    # ---- 6.3.3: interaction
    ny = N / (chiy * NRk / gM1)
    nz = N / (chiz * NRk / gM1)
    Myrk, Mzrk = Wy * fy, Wz * fy
    denLT = chiLT_use * Myrk / gM1
    denz = Mzrk / gM1

    if i.get("annex", "B") == "B":
        Cmy = i.get("C_m_y") if i.get("C_m_y") is not None else cm_linear(psi_y)
        Cmz = i.get("C_m_z") if i.get("C_m_z") is not None else cm_linear(psi_z)
        CmLT = i.get("C_m_LT") if i.get("C_m_LT") is not None else Cmy
        if cls <= 2:
            kyy = min(Cmy * (1 + (lamy - 0.2) * ny), Cmy * (1 + 0.8 * ny))
            kzz = min(Cmz * (1 + (2 * lamz - 0.6) * nz), Cmz * (1 + 1.4 * nz))
        else:
            kyy = min(Cmy * (1 + 0.6 * lamy * ny), Cmy * (1 + 0.6 * ny))
            kzz = min(Cmz * (1 + 0.6 * lamz * nz), Cmz * (1 + 0.6 * nz))
        if i.get("torsion", "susceptible") == "susceptible":
            kyz = 0.6 * kzz if cls <= 2 else kzz
            if cls <= 2:  # Table B.2, plastic column
                t = 0.1 * lamz / (CmLT - 0.25) * nz
                if lamz < 0.4:
                    kzy = min(0.6 + lamz, 1 - t)
                else:
                    kzy = max(1 - t, 1 - 0.1 / (CmLT - 0.25) * nz)
            else:  # Table B.2, elastic column: coefficient 0.05, no lambda_z < 0.4 branch
                kzy = max(1 - 0.05 * lamz / (CmLT - 0.25) * nz, 1 - 0.05 / (CmLT - 0.25) * nz)
        else:
            if cls <= 2:
                kyz, kzy = 0.6 * kzz, 0.6 * kyy
            else:
                kyz, kzy = kzz, 0.8 * kyy
        o.update(C_my=Cmy, C_mz=Cmz, C_mLT=CmLT)
    else:
        # Annex A (method 1), Tables A.1 and A.2
        Ncr_y, Ncr_z = Ncry, Ncrz
        i0sq = (Iy + Iz) / A
        lT = kw * L
        NcrT = (G * It + PI * PI * E * Iw / lT ** 2) / i0sq
        o["N_cr_T"] = NcrT / 1e3
        mu_y = (1 - N / Ncr_y) / (1 - chiy * N / Ncr_y)
        mu_z = (1 - N / Ncr_z) / (1 - chiz * N / Ncr_z)
        wy = min(Wply / Wely, 1.5)
        wz = min(Wplz / Welz, 1.5)
        npl = N / (NRk / gM1)
        aLTa = max(1 - It / Iy, 0.0)
        # lambda_0: slenderness for the uniform moment (C1 = 1, C2 = 0)
        Mcr0 = mcr_general(E, G, Iz, It, Iw, L, k, kw, 1.0, 0.0, 0.0)
        lam0 = math.sqrt(Wy * fy / Mcr0)
        lam_max = max(lamy, lamz)
        Cmy0 = 0.79 + 0.21 * psi_y + 0.36 * (psi_y - 0.33) * N / Ncr_y
        Cmz0 = 0.79 + 0.21 * psi_z + 0.36 * (psi_z - 0.33) * N / Ncr_z
        eps_y = (My / N) * (A / Wely) if N > 0 else 0.0
        # For N -> 0 the ratio eps_y grows without bound and the share tends to 1 (a_LT > 0); N = 0 exactly is the limit.
        share = (math.sqrt(eps_y) * aLTa / (1 + math.sqrt(eps_y) * aLTa)) if N > 0 else (1.0 if aLTa > 0 else 0.0)
        Cmy_a = Cmy0 + (1 - Cmy0) * share
        Cmz_a = Cmz0
        CmLT_a = max(Cmy_a ** 2 * aLTa / math.sqrt((1 - N / Ncr_z) * (1 - N / NcrT)), 1.0)
        thr = 0.2 * math.sqrt(C1) * ((1 - N / Ncr_z) * (1 - N / NcrT)) ** 0.25
        ltb_off = lam0 <= thr
        if ltb_off:
            Cmy_a, Cmz_a, CmLT_a = Cmy0, Cmz0, 1.0
            bLT = cLT = dLT = eLT = 0.0
        else:
            MyR = chiLT_use * Wply * fy / gM1  # chi_LT * M_pl,y,Rd
            MzR = Wplz * fy / gM1
            bLT = 0.5 * aLTa * lam0 ** 2 * (My / MyR) * (Mz / MzR)
            cLT = 10 * aLTa * lam0 ** 2 / (5 + lamz ** 4) * (My / (Cmy_a * MyR))
            dLT = 2 * aLTa * lam0 / (0.1 + lamz ** 4) * (My / (Cmy_a * MyR)) * (Mz / (Cmz_a * MzR))
            eLT = 1.7 * aLTa * lam0 / (0.1 + lamz ** 4) * (My / (Cmy_a * MyR))
        if cls <= 2:
            Cyy = max(1 + (wy - 1) * ((2 - 1.6 / wy * Cmy_a ** 2 * lam_max - 1.6 / wy * Cmy_a ** 2 * lam_max ** 2) * npl - bLT),
                      Wely / Wply)
            Czz = max(1 + (wz - 1) * ((2 - 1.6 / wz * Cmz_a ** 2 * lam_max - 1.6 / wz * Cmz_a ** 2 * lam_max ** 2) * npl - eLT),
                      Welz / Wplz)
            Cyz = max(1 + (wz - 1) * ((2 - 14 * Cmz_a ** 2 * lam_max ** 2 / wz ** 5) * npl - cLT),
                      0.6 * math.sqrt(wz / wy) * Welz / Wplz)
            Czy = max(1 + (wy - 1) * ((2 - 14 * Cmy_a ** 2 * lam_max ** 2 / wy ** 5) * npl - dLT),
                      0.6 * math.sqrt(wy / wz) * Wely / Wply)
            kyy = Cmy_a * CmLT_a * mu_y / (1 - N / Ncr_y) / Cyy
            kyz = Cmz_a * mu_y / (1 - N / Ncr_z) / Cyz * 0.6 * math.sqrt(wz / wy)
            kzy = Cmy_a * CmLT_a * mu_z / (1 - N / Ncr_y) / Czy * 0.6 * math.sqrt(wy / wz)
            kzz = Cmz_a * mu_z / (1 - N / Ncr_z) / Czz
        else:
            kyy = Cmy_a * CmLT_a * mu_y / (1 - N / Ncr_y)
            kyz = Cmz_a * mu_y / (1 - N / Ncr_z)
            kzy = Cmy_a * CmLT_a * mu_z / (1 - N / Ncr_y)
            kzz = Cmz_a * mu_z / (1 - N / Ncr_z)
        o.update(C_my=Cmy_a, C_mz=Cmz_a, C_mLT=CmLT_a, lambda_0=lam0)

    u61 = N / (chiy * NRk / gM1) + kyy * My / denLT + kyz * Mz / denz
    u62 = N / (chiz * NRk / gM1) + kzy * My / denLT + kzz * Mz / denz
    o.update(k_yy=kyy, k_yz=kyz, k_zy=kzy, k_zz=kzz, u_61=u61, u_62=u62, u_interaction=max(u61, u62))
    o["u_member"] = max(o["u_LTB"], o["u_buckling"], u61, u62)
    return o


# ---------------------------------------------------------------- known-answer cases
WELDED = dict(section_type="welded", h=500, b=200, t_f=16, A=110.8, I_y=46036.54933333333, I_z=2137.2333333333333,
              I_t=70.21333333333334, I_w=1.2493653333333332, W_pl_y=2096.36, W_el_y=1841.4619733333332,
              W_pl_z=331.7, W_el_z=213.72333333333333)
ACT = dict(psi_y=0.5, M_y_Ed=200, M_z_Ed=15, N_Ed=-1200, L_cr_y=6, L_cr_z=3, curve_y="b", curve_z="c")

KATS = [
    ("ka-annex-b-class2",
     dict(WELDED, section_class=2, ltb_method="rolled", **ACT),
     {"C_1": (1.31, 1e-12), "Mcr": (521.515295718, 1e-6), "chi_LT": (0.457851025394, 1e-6),
      "k_yy": (0.848513034679, 1e-6), "k_yz": (0.96028367882, 1e-6), "k_zy": (0.91783995565, 1e-6),
      "k_zz": (1.60047279803, 1e-6), "u_61": (0.947749247887, 1e-6), "u_62": (1.24809447033, 1e-6)}),
    ("ka-mcr-uniform-6322",
     dict(WELDED, ltb_method="general", psi_y=1, M_y_Ed=150, N_Ed=0),
     {"C_1": (1.0, 1e-12), "Mcr": (398.103279174, 1e-6), "lambda_LT": (1.3672540904, 1e-6),
      "alpha_LT": (0.76, 1e-12), "chi_LT": (0.315850961286, 1e-6), "Mb_Rd": (235.058749027, 1e-6),
      "u_LTB": (0.638138340398, 1e-6)}),
    ("ka-6323-f-modified",
     dict(WELDED, ltb_method="rolled", psi_y=0, M_y_Ed=250, N_Ed=0, use_f=True),
     {"C_1": (1.88, 1e-12), "Mcr": (748.434164847, 1e-6), "lambda_LT": (0.997172531358, 1e-6),
      "chi_LT_0": (0.561530882199, 1e-6), "k_c": (0.751879699248, 1e-9), "f": (0.885586024324, 1e-6),
      "chi_LT": (0.634078301573, 1e-6), "Mb_Rd": (471.886017841, 1e-6), "u_LTB": (0.529788954425, 1e-6)}),
    ("ka-mcr-k-zg-c2",
     dict(WELDED, ltb_method="rolled", k=0.8, k_w=1.0, C_1=1.35, C_2=0.45, z_g=250, C_m_y=1.0, psi_y=1,
          M_y_Ed=150, N_Ed=0),
     {"Mcr": (440.518293012, 1e-6), "lambda_LT": (1.29976588824, 1e-6), "chi_LT": (0.411641587937, 1e-6),
      "Mb_Rd": (306.346880547, 1e-6)}),
    ("ka-flexural-buckling",
     dict(WELDED, N_Ed=-1200, M_y_Ed=0, L_cr_y=6, L_cr_z=3),
     {"N_Rk": (3933.4, 1e-9), "N_cr_y": (26504.4809114886, 1e-6), "N_cr_z": (4921.851086325, 1e-6),
      "lambda_y": (0.3852338182, 1e-6), "lambda_z": (0.8939635714, 1e-6), "chi_y": (0.9318906542, 1e-6),
      "chi_z": (0.6035449215, 1e-6), "N_b_Rd": (2373.9835943742, 1e-6), "u_buckling": (0.505479483, 1e-6)}),
    ("ka-annex-b-class3-b1",
     dict(WELDED, section_class=3, ltb_method="rolled", torsion="not susceptible", **ACT),
     {"lambda_LT": (1.11959778027, 1e-6), "chi_LT": (0.49453731243, 1e-6), "k_yy": (0.860536013656, 1e-6),
      "k_yz": (1.27112814635, 1e-6), "k_zy": (0.688428810924, 1e-6), "k_zz": (1.27112814635, 1e-6),
      "u_61": (1.11104576816, 1e-6), "u_62": (1.18267539476, 1e-6)}),
    ("ka-annex-a-class2",
     dict(WELDED, section_class=2, ltb_method="rolled", annex="A", **ACT),
     {"k_yy": (1.34899628461, 1e-6), "k_yz": (2.16610447704, 1e-6), "k_zy": (0.749356563725, 1e-6),
      "k_zz": (1.92680651092, 1e-6), "u_61": (1.39511815954, 1e-6), "u_62": (1.19077069504, 1e-6)}),
    ("ka-annex-a-class3",
     dict(WELDED, section_class=3, ltb_method="rolled", annex="A", **ACT),
     {"lambda_LT": (1.11959778027, 1e-6), "chi_LT": (0.49453731243, 1e-6), "k_yy": (1.32784479795, 1e-6),
      "k_yz": (1.39568003974, 1e-6), "k_zy": (1.18115263105, 1e-6), "k_zz": (1.24149385047, 1e-6),
      "u_61": (1.42476704306, 1e-6), "u_62": (1.48163658227, 1e-6)}),
]

# ---------------------------------------------------------------- extra (non-KAT) checks
# Default section: UB 457x191x98 (the sheet default), S355, E = 210 GPa, G = 81 GPa, gamma_M1 = 1.0.
UB = dict(section_type="rolled", h=467.2, b=192.8, t_f=19.6, A=125.27, I_y=45728, I_z=2347.0, I_t=121.29,
          I_w=1.1755, W_pl_y=2232.5, W_el_y=1957.5, W_pl_z=378.9, W_el_z=243.47)

# (label, inputs, {symbol: product value observed with the probe tool})
# --- generated from probe runs (product values hard-coded; the input dicts are what was sent to the probe) ---
def _plate(h_, b_, tf, tw):
    """Welded doubly symmetric I from plates (no fillet welds); I_w = I_z,flanges * h_s^2 / 4."""
    hw = h_ - 2 * tf; hs = h_ - tf
    A = 2 * b_ * tf + hw * tw
    Iy = (b_ * h_ ** 3 - (b_ - tw) * hw ** 3) / 12
    Iz = 2 * tf * b_ ** 3 / 12 + hw * tw ** 3 / 12
    It = (2 * b_ * tf ** 3 + hw * tw ** 3) / 3
    Iw = 2 * (tf * b_ ** 3 / 12) * hs ** 2 / 4
    return dict(section_type='welded', h=h_, b=b_, t_f=tf, A=A / 1e2, I_y=Iy / 1e4, I_z=Iz / 1e4, I_t=It / 1e4, I_w=Iw / 1e12,
                W_pl_y=(b_ * tf * hs + tw * hw ** 2 / 4) / 1e3, W_el_y=Iy / (h_ / 2) / 1e3,
                W_pl_z=(tf * b_ ** 2 / 2 + hw * tw ** 2 / 4) / 1e3, W_el_z=2 * Iz / b_ / 1e3)

UC = {'section_type': 'rolled', 'h': 254.1, 'b': 254.6, 't_f': 14.2, 'A': 93.1, 'I_y': 11407, 'I_z': 3908, 'I_t': 57.3, 'I_w': 0.389, 'W_pl_y': 992, 'W_el_y': 898, 'W_pl_z': 462, 'W_el_z': 307}
WG = _plate(400, 300, 20, 10)  # welded 400 x 300 x 20 / 10, h/b = 1.33

EXTRAS = [
    ('rolled h/b>2, 6.3.2.2 (curve b)',
     dict(UB, ltb_method='general', L=5, psi_y=0, M_y_Ed=300, N_Ed=-800, torsion='not susceptible', L_cr_y=5, L_cr_z=5),
     {'C_1': 1.88, 'Mcr': 1160.108473, 'lambda_LT': 0.8265337861, 'alpha_LT': 0.34, 'chi_LT': 0.7079433871, 'Mb_Rd': 561.0716821, 'u_LTB': 0.5346910378, 'lambda_y': 0.3424972557, 'lambda_z': 1.511790649, 'chi_y': 0.9673474683, 'chi_z': 0.3379324209, 'N_b_Rd': 1502.8142, 'k_yy': 0.6158997321, 'k_yz': 1.047161066, 'k_zy': 0.3695398393, 'k_zz': 1.745268444, 'u_61': 0.5152814278, 'u_62': 0.7299242428, 'u_buckling': 0.5323346026}),
    ('rolled h/b<=2, 6.3.2.2 (curve a)',
     dict(UC, ltb_method='general', L=4, psi_y=0.5, M_y_Ed=150, N_Ed=-900, torsion='not susceptible', L_cr_y=4, L_cr_z=4),
     {'C_1': 1.31, 'Mcr': 917.0512972, 'lambda_LT': 0.6196881592, 'alpha_LT': 0.21, 'chi_LT': 0.88237601, 'Mb_Rd': 310.7375357, 'u_LTB': 0.4827224998, 'lambda_y': 0.4729375176, 'lambda_z': 0.8080015814, 'chi_y': 0.8960107673, 'chi_z': 0.6571298637, 'N_b_Rd': 2171.847056, 'k_yy': 0.8663597087, 'k_yz': 0.8526152596, 'k_zy': 0.5198158252, 'k_zz': 1.421025433, 'u_61': 0.7221257023, 'u_62': 0.6653206155, 'u_buckling': 0.4143938209}),
    ('rolled h/b<=2, 6.3.2.3 (curve b)',
     dict(UC, ltb_method='rolled', L=4, psi_y=0.5, M_y_Ed=150, N_Ed=-900, L_cr_y=4, L_cr_z=4),
     {'C_1': 1.31, 'Mcr': 917.0512972, 'lambda_LT': 0.6196881592, 'alpha_LT': 0.34, 'chi_LT': 0.9081413266, 'Mb_Rd': 319.8110496, 'u_LTB': 0.4690269464, 'lambda_y': 0.4729375176, 'lambda_z': 0.8080015814, 'chi_y': 0.8960107673, 'chi_z': 0.6571298637, 'N_b_Rd': 2171.847056, 'k_yy': 0.8663597087, 'k_yz': 0.8526152596, 'k_zy': 0.9391216613, 'k_zz': 1.421025433, 'u_61': 0.7102604267, 'u_62': 0.854867186, 'u_buckling': 0.4143938209}),
    ('welded h/b<=2, 6.3.2.2 (curve c)',
     dict(WG, ltb_method='general', L=6, psi_y=0.2, M_y_Ed=250, N_Ed=-600, L_cr_y=6, L_cr_z=3),
     {'C_1': 1.6208, 'Mcr': 2108.088724, 'lambda_LT': 0.6622015971, 'alpha_LT': 0.49, 'chi_LT': 0.747936878, 'Mb_Rd': 691.4078088, 'u_LTB': 0.3615811057, 'lambda_y': 0.4512076884, 'lambda_z': 0.5168262757, 'chi_y': 0.9052171561, 'chi_z': 0.833535366, 'N_b_Rd': 4616.118857, 'k_yy': 0.7004450117, 'k_yz': 0.6338195188, 'k_zy': 0.9843775049, 'k_zz': 1.056365865, 'u_61': 0.3729542798, 'u_62': 0.4859116288, 'u_buckling': 0.1299793222}),
    ('welded h/b<=2, 6.3.2.3 (curve c)',
     dict(WG, ltb_method='rolled', L=6, psi_y=0.2, M_y_Ed=250, N_Ed=-600, L_cr_y=6, L_cr_z=3),
     {'C_1': 1.6208, 'Mcr': 2108.088724, 'lambda_LT': 0.6622015971, 'alpha_LT': 0.49, 'chi_LT': 0.848731942, 'Mb_Rd': 784.5847818, 'u_LTB': 0.3186398791, 'lambda_y': 0.4512076884, 'lambda_z': 0.5168262757, 'chi_y': 0.9052171561, 'chi_z': 0.833535366, 'N_b_Rd': 4616.118857, 'k_yy': 0.7004450117, 'k_yz': 0.6338195188, 'k_zy': 0.9843775049, 'k_zz': 1.056365865, 'u_61': 0.3428763118, 'u_62': 0.4436412513, 'u_buckling': 0.1299793222}),
    ('chosen LTB curve a, 6.3.2.3',
     dict(UB, ltb_method='rolled', ltb_curve='a', L=7, psi_y=1, M_y_Ed=200, N_Ed=0),
     {'C_1': 1.0, 'Mcr': 383.2655719, 'lambda_LT': 1.438003707, 'alpha_LT': 0.21, 'chi_LT': 0.4835929807, 'Mb_Rd': 383.2655719, 'u_LTB': 0.521831374}),
    ('chosen LTB curve b, 6.3.2.3',
     dict(UB, ltb_method='rolled', ltb_curve='b', L=7, psi_y=1, M_y_Ed=200, N_Ed=0),
     {'C_1': 1.0, 'Mcr': 383.2655719, 'lambda_LT': 1.438003707, 'alpha_LT': 0.34, 'chi_LT': 0.4548918926, 'Mb_Rd': 360.5188833, 'u_LTB': 0.5547559621}),
    ('chosen LTB curve c, 6.3.2.3',
     dict(UB, ltb_method='rolled', ltb_curve='c', L=7, psi_y=1, M_y_Ed=200, N_Ed=0),
     {'C_1': 1.0, 'Mcr': 383.2655719, 'lambda_LT': 1.438003707, 'alpha_LT': 0.49, 'chi_LT': 0.4135373037, 'Mb_Rd': 327.7438208, 'u_LTB': 0.6102327101}),
    ('chosen LTB curve d, 6.3.2.3',
     dict(UB, ltb_method='rolled', ltb_curve='d', L=7, psi_y=1, M_y_Ed=200, N_Ed=0),
     {'C_1': 1.0, 'Mcr': 383.2655719, 'lambda_LT': 1.438003707, 'alpha_LT': 0.76, 'chi_LT': 0.3594050956, 'Mb_Rd': 284.8420159, 'u_LTB': 0.7021436053}),
    ('chosen LTB curve a, 6.3.2.2',
     dict(UB, ltb_method='general', ltb_curve='a', L=7, psi_y=1, M_y_Ed=200, N_Ed=0),
     {'C_1': 1.0, 'Mcr': 383.2655719, 'lambda_LT': 1.438003707, 'alpha_LT': 0.21, 'chi_LT': 0.3998342605, 'Mb_Rd': 316.8836452, 'u_LTB': 0.6311464887}),
    ('chosen LTB curve b, 6.3.2.2',
     dict(UB, ltb_method='general', ltb_curve='b', L=7, psi_y=1, M_y_Ed=200, N_Ed=0),
     {'C_1': 1.0, 'Mcr': 383.2655719, 'lambda_LT': 1.438003707, 'alpha_LT': 0.34, 'chi_LT': 0.3660554692, 'Mb_Rd': 290.1126864, 'u_LTB': 0.6893872945}),
    ('chosen LTB curve c, 6.3.2.2',
     dict(UB, ltb_method='general', ltb_curve='c', L=7, psi_y=1, M_y_Ed=200, N_Ed=0),
     {'C_1': 1.0, 'Mcr': 383.2655719, 'lambda_LT': 1.438003707, 'alpha_LT': 0.49, 'chi_LT': 0.3354874852, 'Mb_Rd': 265.8864128, 'u_LTB': 0.7522009038}),
    ('chosen LTB curve d, 6.3.2.2',
     dict(UB, ltb_method='general', ltb_curve='d', L=7, psi_y=1, M_y_Ed=200, N_Ed=0),
     {'C_1': 1.0, 'Mcr': 383.2655719, 'lambda_LT': 1.438003707, 'alpha_LT': 0.76, 'chi_LT': 0.2940602519, 'Mb_Rd': 233.0537769, 'u_LTB': 0.858171031}),
    ('6.3.2.2 near 0.2 (target 0.199)',
     dict(UB, ltb_method='general', psi_y=1, M_y_Ed=100, N_Ed=0, L=0.741597),
     {'lambda_LT': 0.1989999144, 'chi_LT': 1.0, 'Mb_Rd': 792.5375}),
    ('6.3.2.2 near 0.2 (target 0.201)',
     dict(UB, ltb_method='general', psi_y=1, M_y_Ed=100, N_Ed=0, L=0.749134),
     {'lambda_LT': 0.2010001215, 'chi_LT': 0.9996457731, 'Mb_Rd': 792.2567619}),
    ('6.3.2.3 near 0.4 (target 0.399)',
     dict(UB, ltb_method='rolled', psi_y=1, M_y_Ed=100, N_Ed=0, L=1.511742),
     {'lambda_LT': 0.3990000662, 'chi_LT': 1.0, 'Mb_Rd': 792.5375}),
    ('6.3.2.3 near 0.4 (target 0.401)',
     dict(UB, ltb_method='rolled', psi_y=1, M_y_Ed=100, N_Ed=0, L=1.519656),
     {'lambda_LT': 0.4010000703, 'chi_LT': 0.9994431151, 'Mb_Rd': 792.0961478}),
    ('6.3.2.3 near 0.4 (target 0.45)',
     dict(UB, ltb_method='rolled', psi_y=1, M_y_Ed=100, N_Ed=0, L=1.715212),
     {'lambda_LT': 0.4499998826, 'chi_LT': 0.9720597285, 'Mb_Rd': 770.3937871}),
    ('f mod psi=-1, lam~1.2 (cap 1/lam^2)',
     dict(UB, ltb_method='rolled', use_f=True, psi_y=-1, M_y_Ed=100, N_Ed=0, L=11.66144, ltb_curve='a'),
     {'C_1': 2.7, 'lambda_LT': 1.199999981, 'chi_LT_0': 0.6442368267, 'k_c': 0.6024096386, 'f': 0.8648192711, 'chi_LT': 0.6944444664, 'Mb_Rd': 550.3732813}),
    ('UDL C1=1.127 C2=0.454 zg=+h/2',
     dict(UB, ltb_method='rolled', C_1=1.127, C_2=0.454, z_g=233.6, C_m_y=0.95, psi_y=1, M_y_Ed=150, N_Ed=0, L=6),
     {'C_1': 1.127, 'Mcr': 396.0305465, 'lambda_LT': 1.414638807, 'alpha_LT': 0.49, 'chi_LT': 0.4232136061, 'Mb_Rd': 335.4126534, 'u_LTB': 0.4472103199}),
    ('UDL C1=1.127 C2=0.454 zg=-h/2',
     dict(UB, ltb_method='rolled', C_1=1.127, C_2=0.454, z_g=-233.6, C_m_y=0.95, psi_y=1, M_y_Ed=150, N_Ed=0, L=6),
     {'C_1': 1.127, 'Mcr': 719.037768, 'lambda_LT': 1.049866454, 'alpha_LT': 0.49, 'chi_LT': 0.6092010315, 'Mb_Rd': 482.8146625, 'u_LTB': 0.3106782201}),
    ('point C1=1.348 C2=0.630 zg=+h/2 L=3',
     dict(UB, ltb_method='general', C_1=1.348, C_2=0.63, z_g=233.6, C_m_y=0.9, psi_y=1, M_y_Ed=150, N_Ed=0, L=3),
     {'C_1': 1.348, 'Mcr': 1112.544335, 'lambda_LT': 0.8440171027, 'alpha_LT': 0.34, 'chi_LT': 0.6969315578, 'Mb_Rd': 552.3443945, 'u_LTB': 0.2715696973}),
    ('extreme zg=+1000 C2=1.5 L=0.5',
     dict(UB, ltb_method='rolled', C_1=1.0, C_2=1.5, z_g=1000, C_m_y=1.0, psi_y=1, M_y_Ed=100, N_Ed=0, L=0.5),
     {'Mcr': 3262.990173, 'lambda_LT': 0.4928355499, 'chi_LT': 0.947882144}),
    ('extreme zg=-1000 C2=1.5 L=0.5',
     dict(UB, ltb_method='rolled', C_1=1.0, C_2=1.5, z_g=-1000, C_m_y=1.0, psi_y=1, M_y_Ed=100, N_Ed=0, L=0.5),
     {'Mcr': 586994.8207, 'lambda_LT': 0.03674453668, 'chi_LT': 1.0}),
    ('k=0.5 kw=0.5 L=8',
     dict(UB, ltb_method='rolled', k=0.5, k_w=0.5, C_1=1.0, C_m_y=1.0, psi_y=1, M_y_Ed=100, N_Ed=0, L=8),
     {'Mcr': 872.7203162, 'lambda_LT': 0.9529549434, 'chi_LT': 0.6679585967}),
    ('k=0.7 kw=1.0 L=8',
     dict(UB, ltb_method='rolled', k=0.7, k_w=1.0, C_1=1.0, C_m_y=1.0, psi_y=1, M_y_Ed=100, N_Ed=0, L=8),
     {'Mcr': 459.8294108, 'lambda_LT': 1.312839205, 'chi_LT': 0.4685403543}),
    ('end moments A=300 B=-150',
     dict(UB, moment_mode='ends', M_y_A=300, M_y_B=-150, N_Ed=-500, L=5, L_cr_y=5, L_cr_z=5),
     {'C_1': 2.7, 'Mcr': 1666.113233, 'chi_LT': 0.8320334083, 'k_yy': 0.4066248884, 'k_zy': 0.7781939156, 'u_LTB': 0.4549468574, 'u_61': 0.3012210656, 'u_62': 0.686746003}),
    ('end moments A=0 B=100',
     dict(UB, moment_mode='ends', M_y_A=0, M_y_B=100, N_Ed=-500, L=5, L_cr_y=5, L_cr_z=5),
     {'C_1': 1.88, 'Mcr': 1160.108473, 'chi_LT': 0.7470447645, 'k_yy': 0.6099373326, 'k_zy': 0.9049402495, 'u_LTB': 0.1689015181, 'u_61': 0.2192476919, 'u_62': 0.4855549085}),
    ('end moments A=200 B=200',
     dict(UB, moment_mode='ends', M_y_A=200, M_y_B=200, N_Ed=-500, L=5, L_cr_y=5, L_cr_z=5),
     {'C_1': 1.0, 'Mcr': 617.0789753, 'chi_LT': 0.5611594964, 'k_yy': 1.016562221, 'k_zy': 0.9556387831, 'u_LTB': 0.4497010051, 'u_61': 0.573377403, 'u_62': 0.7624608479}),
    ('end moments A=100 B=300',
     dict(UB, moment_mode='ends', M_y_A=100, M_y_B=300, N_Ed=-500, L=5, L_cr_y=5, L_cr_z=5),
     {'C_1': 1.471111111, 'Mcr': 907.7917369, 'chi_LT': 0.6794831391, 'k_yy': 0.7454789621, 'k_zy': 0.931163629, 'u_LTB': 0.557086648, 'u_61': 0.5315247266, 'u_62': 0.8514479515}),
    ('end moments A=-250 B=100',
     dict(UB, moment_mode='ends', M_y_A=-250, M_y_B=100, N_Ed=-500, L=5, L_cr_y=5, L_cr_z=5),
     {'C_1': 2.5232, 'Mcr': 1557.01367, 'chi_LT': 0.8174820995, 'k_yy': 0.4472873772, 'k_zy': 0.8248899333, 'u_LTB': 0.3858708186, 'u_61': 0.2888234969, 'u_62': 0.6510100805}),
    ('end moments A=300 B=0',
     dict(UB, moment_mode='ends', M_y_A=300, M_y_B=0, N_Ed=-500, L=5, L_cr_y=5, L_cr_z=5),
     {'C_1': 1.88, 'Mcr': 1160.108473, 'chi_LT': 0.7470447645, 'k_yy': 0.6099373326, 'k_zy': 0.9049402495, 'u_LTB': 0.5067045542, 'u_61': 0.4252863747, 'u_62': 0.7912464723}),
    ('AnnexB class1 B.1',
     dict(UB, torsion='not susceptible', psi_y=0.3, M_y_Ed=250, M_z_Ed=20, N_Ed=-1000, L=5, L_cr_y=5, L_cr_z=5),
     {'C_1': 1.5068, 'Mcr': 929.8145999, 'lambda_LT': 0.9232338721, 'alpha_LT': 0.49, 'chi_LT': 0.6864111945, 'Mb_Rd': 544.0066121, 'u_LTB': 0.4595532379, 'lambda_y': 0.3424972557, 'lambda_z': 1.511790649, 'chi_y': 0.9673474683, 'chi_z': 0.3379324209, 'N_b_Rd': 1502.8142, 'k_yy': 0.7438495982, 'k_yz': 1.158951333, 'k_zy': 0.4463097589, 'k_zz': 1.931585555, 'u_61': 0.7466177916, 'u_62': 1.15772568, 'u_buckling': 0.6654182533}),
    ('AnnexB class2 B.2 lam_z<0.4',
     dict(UB, section_class=2, psi_y=0, M_y_Ed=250, M_z_Ed=20, N_Ed=-1500, L=2, L_cr_y=2, L_cr_z=1.0),
     {'lambda_y': 0.1369989023, 'lambda_z': 0.3023581298, 'chi_y': 1.0, 'chi_z': 0.9632404943, 'N_b_Rd': 4283.612354, 'k_yy': 0.5872498529, 'k_yz': 0.6009909005, 'k_zy': 0.9023581298, 'k_zz': 1.001651501, 'u_61': 0.6119035209, 'u_62': 0.7837477762, 'u_buckling': 0.350171742, 'C_mLT': 0.6}),
    ('AnnexB class3 B.2 (elastic kzy)',
     dict(UB, section_class=3, psi_y=0.5, M_y_Ed=200, M_z_Ed=15, N_Ed=-1000, L=5, L_cr_y=5, L_cr_z=3),
     {'lambda_y': 0.3424972557, 'lambda_z': 0.9070743894, 'chi_y': 0.9673474683, 'chi_z': 0.6566320445, 'N_b_Rd': 2920.098516, 'k_yy': 0.8382155754, 'k_yz': 1.18637886, 'k_zy': 0.9717607787, 'k_zz': 1.18637886, 'u_61': 0.7910652428, 'u_62': 0.9572577304, 'u_buckling': 0.3424541996}),
    ('AnnexB class3 B.2 lam_z<0.4',
     dict(UB, section_class=3, psi_y=0.5, M_y_Ed=200, M_z_Ed=15, N_Ed=-1500, L=2, L_cr_y=2, L_cr_z=1.0),
     {'lambda_y': 0.1369989023, 'lambda_z': 0.3023581298, 'chi_y': 1.0, 'chi_z': 0.9632404943, 'N_b_Rd': 4283.612354, 'k_yy': 0.8221806441, 'k_yz': 1.063526364, 'k_zy': 0.9903747934, 'k_zz': 1.063526364, 'u_61': 0.7618844551, 'u_62': 0.8238562251, 'u_buckling': 0.350171742}),
    ('AnnexB entered Cm values',
     dict(UB, psi_y=0.5, M_y_Ed=200, M_z_Ed=15, N_Ed=-1000, C_m_y=0.9, C_m_z=0.7, C_m_LT=0.6, L=5, L_cr_y=5, L_cr_z=3),
     {'lambda_y': 0.3424972557, 'lambda_z': 0.9070743894, 'chi_y': 0.9673474683, 'chi_z': 0.6566320445, 'N_b_Rd': 2920.098516, 'k_yy': 0.9298119978, 'k_yz': 0.5946319463, 'k_zy': 0.9112481617, 'k_zz': 0.9910532438, 'u_61': 0.6624938037, 'u_62': 0.8094368902, 'u_buckling': 0.3424541996, 'C_my': 0.9, 'C_mz': 0.7, 'C_mLT': 0.6}),
    ('AnnexA class1 UB',
     dict(UB, annex='A', psi_y=0.3, M_y_Ed=250, M_z_Ed=20, N_Ed=-1000, L=5, L_cr_y=5, L_cr_z=3),
     {'k_yy': 1.043536095, 'k_yz': 1.820016484, 'k_zy': 0.5770229779, 'k_zz': 1.850513006, 'u_61': 0.9826323995, 'u_62': 0.8827767634, 'C_my': 0.9348730871, 'C_mz': 1.044625971, 'C_mLT': 1.076904173, 'N_cr_T': 5099.377493, 'lambda_0': 1.133286031}),
    ('AnnexA class2 UB, Mz=0',
     dict(UB, annex='A', section_class=2, psi_y=0, M_y_Ed=250, M_z_Ed=0, N_Ed=-1000, L=5, L_cr_y=5, L_cr_z=5),
     {'k_yy': 1.315108498, 'k_yz': 3.595437108, 'k_zy': 0.5916348298, 'k_zz': 2.116798905, 'u_61': 0.7877662555, 'u_62': 0.9152383055, 'C_my': 0.9057558466, 'C_mz': 1.123961032, 'C_mLT': 1.308944137, 'N_cr_T': 5099.377493, 'lambda_0': 1.133286031}),
    ('AnnexA class2 UC',
     dict(UC, annex='A', section_class=2, psi_y=-0.5, M_y_Ed=100, M_z_Ed=10, N_Ed=-1500, L=4, L_cr_y=4, L_cr_z=4),
     {'k_yy': 0.8674789624, 'k_yz': 1.077224917, 'k_zy': 0.443502474, 'k_zz': 2.002307916, 'u_61': 0.8216100524, 'u_62': 0.9402506157, 'C_my': 0.8110035155, 'C_mz': 1.071468547, 'C_mLT': 1.0, 'N_cr_T': 5884.691971, 'lambda_0': 0.7092655167}),
    ('AnnexA class3 UB',
     dict(UB, annex='A', section_class=3, psi_y=0.5, M_y_Ed=200, M_z_Ed=15, N_Ed=-1000, L=5, L_cr_y=5, L_cr_z=3),
     {'k_yy': 1.088978887, 'k_yz': 1.28064265, 'k_zy': 1.011124168, 'k_zz': 1.189085251, 'u_61': 0.9129440916, 'u_62': 0.9742912854, 'C_my': 0.9514239222, 'C_mz': 1.044625971, 'C_mLT': 1.115372362, 'N_cr_T': 5099.377493, 'lambda_0': 1.061193759}),
    ('AnnexA threshold (lam0 small)',
     dict(UC, annex='A', section_class=2, psi_y=1, M_y_Ed=100, M_z_Ed=10, N_Ed=-1500, L=0.8, L_cr_y=3, L_cr_z=2),
     {'k_yy': 1.015743521, 'k_yz': 0.5538102835, 'k_zy': 0.5344657562, 'k_zz': 0.8311648627, 'u_61': 0.8031384516, 'u_62': 0.7094265512, 'C_my': 1.013772748, 'C_mz': 1.017867137, 'C_mLT': 1.0, 'N_cr_T': 79402.50516, 'lambda_0': 0.1654993247, 'lambda_LT': 0.1654993247}),
    ('AnnexA k=0.8 kw=0.8',
     dict(UB, annex='A', section_class=2, k=0.8, k_w=0.8, psi_y=0.5, M_y_Ed=200, M_z_Ed=15, N_Ed=-1000, L=6, L_cr_y=6, L_cr_z=3),
     {'k_yy': 1.107562528, 'k_yz': 1.431650464, 'k_zy': 0.6091884643, 'k_zz': 1.850513006, 'u_61': 0.817620893, 'u_62': 0.7805064452, 'C_my': 0.9517576596, 'C_mz': 1.044625971, 'C_mLT': 1.110663013, 'N_cr_T': 5315.402011, 'lambda_0': 1.098930972}),
    ('AnnexA tiny N=-1 kN',
     dict(UB, annex='A', section_class=2, psi_y=0.5, M_y_Ed=200, M_z_Ed=15, N_Ed=-1, L=5, L_cr_y=5, L_cr_z=3),
     {'k_yy': 1.001105391, 'k_yz': 1.234625741, 'k_zy': 0.531226962, 'k_zz': 1.556507776, 'u_61': 0.5295280012, 'u_62': 0.3817249839, 'C_my': 0.9971375085, 'C_mz': 1.000044626, 'C_mLT': 1.0, 'N_cr_T': 5099.377493, 'lambda_0': 1.133286031}),
    ('AnnexA N=0',
     dict(UB, annex='A', section_class=2, psi_y=0.5, M_y_Ed=200, M_z_Ed=15, N_Ed=0, L=5, L_cr_y=5, L_cr_z=3),
     {'k_yy': 1.003940506, 'k_yz': 1.231664136, 'k_zy': 0.532676982, 'k_zz': 1.55624923, 'u_61': 0.5300743241, 'u_62': 0.3819209198, 'C_my': 1.0, 'C_mz': 1.0, 'C_mLT': 1.0, 'N_cr_T': 5099.377493, 'lambda_0': 1.133286031}),
    ('AnnexB N=0',
     dict(UB, section_class=2, psi_y=0.5, M_y_Ed=200, M_z_Ed=15, N_Ed=0, L=5, L_cr_y=5, L_cr_z=3),
     {'k_yy': 0.8, 'k_yz': 0.6, 'k_zy': 1.0, 'k_zz': 1.0, 'u_61': 0.3798555778, 'u_62': 0.502698544, 'u_LTB': 0.391182257}),
    ('S460 flexural manual b/c',
     dict(UB, f_y=460, curve_y='b', curve_z='c', N_Ed=-1500, M_y_Ed=100, L_cr_y=5, L_cr_z=3),
     {'lambda_y': 0.3898718109, 'lambda_z': 1.032541806, 'chi_y': 0.9300721029, 'chi_z': 0.5212897779, 'N_b_Rd': 3003.890642, 'u_buckling': 0.4993523996}),
    ('rolled h/b<=1.2 tf>100 (d,d)',
     dict(UC, t_f=110, N_Ed=-1500, M_y_Ed=50, L_cr_y=4, L_cr_z=4),
     {'chi_y': 0.7983856115, 'chi_z': 0.5748233809, 'N_b_Rd': 1899.820015}),
    ('rolled h/b>1.2 tf=60 (b,c)',
     dict(UB, t_f=60, N_Ed=-1500, M_y_Ed=50),
     {'chi_y': 0.9216866467, 'chi_z': 0.2314664088, 'N_b_Rd': 1029.350795}),
    ('rolled tf=40 boundary (a,b)',
     dict(UB, t_f=40, N_Ed=-1500, M_y_Ed=50),
     {'chi_y': 0.9498683613, 'chi_z': 0.2486586793, 'N_b_Rd': 1105.806283}),
    ('welded tf=45 (c,d)',
     dict(WG, t_f=45, N_Ed=-1500, M_y_Ed=50),
     {'chi_y': 0.8698810408, 'chi_z': 0.450260049, 'N_b_Rd': 2493.540151}),
    ('very short member L_cr=0.1 m',
     dict(UB, L_cr_y=0.1, L_cr_z=0.1, N_Ed=-3000, M_y_Ed=10, L=0.1),
     {'lambda_y': 0.006849945113, 'lambda_z': 0.03023581298, 'chi_y': 1.0, 'chi_z': 1.0, 'N_b_Rd': 4447.085, 'Mcr': 1088866.026, 'chi_LT': 1.0}),
    ('very long member 100 m',
     dict(UB, L=100, L_cr_y=100, L_cr_z=100, N_Ed=-10, M_y_Ed=1, psi_y=1),
     {'Mcr': 21.88814243, 'lambda_LT': 6.017352553, 'chi_LT': 0.02761780033, 'lambda_z': 30.23581298, 'chi_z': 0.001081750454, 'N_b_Rd': 4.810636219}),
    ('gamma_M1=1.1, E=200, G=77',
     dict(UB, gamma_M1=1.1, E=200, G=77, psi_y=0.5, M_y_Ed=200, M_z_Ed=15, N_Ed=-1000, L=5, L_cr_y=5, L_cr_z=3),
     {'C_1': 1.31, 'Mcr': 769.5215349, 'lambda_LT': 1.014844544, 'alpha_LT': 0.49, 'chi_LT': 0.6301304776, 'Mb_Rd': 454.0018485, 'u_LTB': 0.4405268407, 'lambda_y': 0.3509552516, 'lambda_z': 0.9294746609, 'chi_y': 0.9652713555, 'chi_z': 0.6422091111, 'N_b_Rd': 2596.325913, 'k_yy': 0.8309461089, 'k_yz': 0.8909378939, 'k_zy': 0.9349097923, 'k_zz': 1.48489649, 'u_61': 0.7315958963, 'u_62': 0.9791616722, 'u_buckling': 0.3851596577}),
    ('custom alphas (c .5, d .8) welded',
     dict(WG, alphas={'c': 0.5, 'd': 0.8, 'b': 0.35}, ltb_method='rolled', psi_y=0.5, M_y_Ed=200, N_Ed=-800, L=6, L_cr_y=6, L_cr_z=3),
     {'C_1': 1.31, 'Mcr': 1703.847624, 'lambda_LT': 0.7365789273, 'alpha_LT': 0.5, 'chi_LT': 0.8002841985, 'Mb_Rd': 739.7987188, 'u_LTB': 0.2703438042, 'lambda_y': 0.4512076884, 'lambda_z': 0.5168262757, 'chi_y': 0.9027546224, 'chi_z': 0.8308432254, 'N_b_Rd': 4601.209782, 'k_yy': 0.8321580887, 'k_yz': 0.6452388034, 'k_zy': 0.983661964, 'k_zz': 1.075398006, 'u_61': 0.3849862221, 'u_62': 0.4397942345, 'u_buckling': 0.1738673171}),
    ('custom lam0=.3 beta=.9',
     dict(UB, lambda_LT0=0.3, beta_LT=0.9, ltb_method='rolled', psi_y=1, M_y_Ed=200, N_Ed=0, L=7),
     {'C_1': 1.0, 'Mcr': 383.2655719, 'lambda_LT': 1.438003707, 'alpha_LT': 0.49, 'chi_LT': 0.3650576329, 'Mb_Rd': 289.3218637, 'u_LTB': 0.6912716427}),
    ('fy=235 class3',
     dict(UB, f_y=235, section_class=3, psi_y=0.5, M_y_Ed=200, M_z_Ed=15, N_Ed=-1000, L=5, L_cr_y=5, L_cr_z=3),
     {'C_1': 1.31, 'Mcr': 808.3734576, 'lambda_LT': 0.7543602467, 'alpha_LT': 0.49, 'chi_LT': 0.7921854906, 'Mb_Rd': 364.415228, 'u_LTB': 0.5488244854, 'lambda_y': 0.278661423, 'lambda_z': 0.7380107021, 'chi_y': 0.9824310205, 'chi_z': 0.7618160309, 'N_b_Rd': 2242.668313, 'k_yy': 0.8462488652, 'k_yz': 1.197446238, 'k_zy': 0.9700839034, 'k_zz': 1.197446238, 'u_61': 1.124139489, 'u_62': 1.29223404, 'u_buckling': 0.4458974134}),
    ('near zero: My=0 Mz=10 N=-1000 (B)',
     dict(UB, psi_y=0.5, M_y_Ed=0, M_z_Ed=10, N_Ed=-1000, L=5, L_cr_y=5, L_cr_z=3),
     {'lambda_y': 0.3424972557, 'lambda_z': 0.9070743894, 'chi_y': 0.9673474683, 'chi_z': 0.6566320445, 'N_b_Rd': 2920.098516, 'k_yy': 0.8264995536, 'k_yz': 0.8494742089, 'k_zy': 0.9435215575, 'k_zz': 1.415790348, 'u_61': 0.2956101741, 'u_62': 0.4477099881, 'u_buckling': 0.3424541996, 'u_LTB': 0.0}),
    ('near zero: My=0 Mz=10 N=-1000 (A)',
     dict(UB, annex='A', section_class=2, psi_y=0.5, M_y_Ed=0, M_z_Ed=10, N_Ed=-1000, L=5, L_cr_y=5, L_cr_z=3),
     {'k_yy': 0.918669153, 'k_yz': 0.848334259, 'k_zy': 0.4903170317, 'k_zz': 1.190898607, 'u_61': 0.2955254255, 'u_62': 0.4309905935, 'C_my': 0.8966143221, 'C_mz': 1.044625971, 'C_mLT': 1.0, 'N_cr_T': 5099.377493, 'lambda_0': 1.133286031}),
    ('near zero: N=-0.01 kN (A)',
     dict(UB, annex='A', section_class=2, psi_y=0.5, M_y_Ed=100, M_z_Ed=5, N_Ed=-0.01, L=5, L_cr_y=5, L_cr_z=3),
     {'k_yy': 1.000240193, 'k_yz': 0.8830350997, 'k_zy': 0.5245221666, 'k_zz': 1.319178435, 'u_61': 0.2284646979, 'u_62': 0.151631934, 'C_my': 0.9995854737, 'C_mz': 1.000000446, 'C_mLT': 1.0, 'N_cr_T': 5099.377493, 'lambda_0': 1.133286031}),
    ('near zero: Iw=1e-5 dm6, It=0.1 cm4',
     dict(UB, I_t=0.1, I_w=1e-05, psi_y=1, M_y_Ed=10, N_Ed=0, L=3),
     {'Mcr': 21.21899891, 'lambda_LT': 6.11149506, 'chi_LT': 0.02677349515}),
    ('large: M_y=1e4 kNm, N=-1e4 kN',
     dict(UB, psi_y=1, M_y_Ed=10000, M_z_Ed=500, N_Ed=-10000, L=5, L_cr_y=5, L_cr_z=5),
     {'lambda_y': 0.3424972557, 'lambda_z': 1.511790649, 'chi_y': 0.9673474683, 'chi_z': 0.3379324209, 'N_b_Rd': 1502.8142, 'k_yy': 1.33124442, 'k_yz': 6.189513328, 'k_zy': 0.1127756623, 'k_zz': 10.31585555, 'u_61': 55.26538283, 'u_62': 47.53614588, 'u_buckling': 6.654182533, 'u_LTB': 22.48505025}),
]


# ---------------------------------------------------------------- numerical cross-check of M_cr and C1
def _lt_eigen(E, G, Iz, It, Iw, L, m, zload=None, N=30):
    """Smallest load factor lambda of the lateral-torsional eigenproblem for a doubly symmetric beam on fork supports.

    Rayleigh-Ritz with sine series for v and phi on the energy
    0.5*int[E Iz v''^2 + E Iw phi''^2 + G It phi'^2 - 2 lambda m v'' phi] dx  - 0.5 lambda zload * (phi^2 term).
    m(x): moment shape (per unit lambda). zload: matrix of the load-height term per unit lambda, or None.
    This is independent of the C1, C2 closed form and of any table.
    """
    import numpy as np
    from numpy.polynomial.legendre import leggauss
    n = np.arange(1, N + 1)
    kk = n * PI / L
    Kv = np.diag(E * Iz * kk ** 4 * L / 2)
    Kp = np.diag((E * Iw * kk ** 4 + G * It * kk ** 2) * L / 2)
    x, w = leggauss(600)
    x = (x + 1) * L / 2
    w = w * L / 2
    S = np.sin(np.outer(kk, x))
    C = (-(kk[:, None] ** 2) * S * m(x) * w) @ S.T
    B = C.T @ np.linalg.solve(Kv, C)
    Z = zload(kk, L) if zload else np.zeros_like(Kp)

    def f(lam):
        return np.linalg.eigvalsh(Kp - lam * Z - lam * lam * B)[0]

    lo, hi = 0.0, 1.0
    while f(hi) > 0:
        lo, hi = hi, hi * 2
    for _ in range(100):
        mid = (lo + hi) / 2
        if f(mid) > 0:
            lo = mid
        else:
            hi = mid
    return (lo + hi) / 2


def numeric_section():
    """Prints C1 (end moments) and UDL / point-load M_cr from the eigenproblem. Returns the number of failed assertions."""
    import numpy as np
    bad = 0
    E, G = 210e3, 81e3
    Iz, It, Iw = UB["I_z"] * 1e4, UB["I_t"] * 1e4, UB["I_w"] * 1e12
    L = 6000.0
    Mu = mcr_general(E, G, Iz, It, Iw, L, 1, 1, 1.0, 0.0, 0.0)
    lam = _lt_eigen(E, G, Iz, It, Iw, L, lambda x: np.ones_like(x))
    ok = abs(lam - Mu) / Mu < 1e-6
    bad += 0 if ok else 1
    print(f"uniform moment: eigen {lam / 1e6:.4f} kNm, closed form {Mu / 1e6:.4f} kNm  {'PASS' if ok else 'FAIL'}")
    print("\nC1 for end moments, UB 457x191x98 on fork supports (info only; the product uses 1.88 - 1.40 psi + 0.52 psi^2 <= 2.70):")
    print(f"{'L (m)':>6} {'psi':>6} {'C1 eigen':>9} {'C1 formula':>11} {'formula/eigen':>14}")
    for Lm in (6.0, 12.0, 20.0):
        Ls = Lm * 1e3
        Mus = mcr_general(E, G, Iz, It, Iw, Ls, 1, 1, 1.0, 0.0, 0.0)
        for psi in (0.5, 0.0, -0.5, -1.0):
            c1n = _lt_eigen(E, G, Iz, It, Iw, Ls, lambda x, psi=psi, Ls=Ls: 1 - (1 - psi) * x / Ls) / Mus
            c1f = c1_from_psi(psi)
            print(f"{Lm:>6.1f} {psi:>6.2f} {c1n:>9.4f} {c1f:>11.4f} {c1f / c1n:>14.4f}")
    print("\nTransverse load at height z_g (UDL, table C1 = 1.127, C2 = 0.454; load on the compression flange side is z_g > 0):")
    for zg in (0.0, 233.6, -233.6, 500.0):
        lam = _lt_eigen(E, G, Iz, It, Iw, L, lambda x: x * (L - x) / 2,
                        zload=lambda kk, L, zg=zg: zg * (L / 2) * np.eye(len(kk)))
        Mn = lam * L * L / 8
        Mf = mcr_general(E, G, Iz, It, Iw, L, 1, 1, 1.127, 0.454, zg)
        ok = abs(Mf - Mn) / Mn < 0.01
        bad += 0 if ok else 1
        print(f"  z_g {zg:7.1f} mm: eigen {Mn / 1e6:8.3f} kNm  formula {Mf / 1e6:8.3f} kNm  ratio {Mf / Mn:6.4f}  {'PASS' if ok else 'FAIL (>1 %)'}")
    return bad


def table_row(case, sym, exp, ind, tol, ok):
    rel = abs(ind - exp) / abs(exp) if exp != 0 else abs(ind - exp)
    print(f"{case:<26} {sym:<11} {exp:>16.9g} {ind:>16.9g} {rel:>10.2e} {tol:>8.0e}  {'PASS' if ok else 'FAIL'}")


def main():
    bad = 0
    total = 0
    print("== Known-answer cases ==")
    print(f"{'case':<26} {'symbol':<11} {'KAT expected':>16} {'independent':>16} {'rel diff':>10} {'tol':>8}  result")
    for case, inp, exp in KATS:
        out = analyse(inp)
        for sym, (val, tol) in exp.items():
            total += 1
            ind = out[sym]
            rel = abs(ind - val) / abs(val) if val != 0 else abs(ind - val)
            ok = rel <= max(tol, 1e-6)
            bad += 0 if ok else 1
            table_row(case, sym, val, ind, tol, ok)
    print("\n== Extra checks (product values observed with the probe tool) ==")
    print(f"{'probe':<26} {'symbol':<11} {'product':>16} {'independent':>16} {'rel diff':>10} {'tol':>8}  result")
    for label, inp, prod in EXTRAS:
        out = analyse(inp)
        for sym, val in prod.items():
            total += 1
            ind = out[sym]
            rel = abs(ind - val) / abs(val) if val != 0 else abs(ind - val)
            ok = rel <= 1e-5
            bad += 0 if ok else 1
            table_row(label, sym, val, ind, 1e-5, ok)
    print("\n== Numerical cross-check of M_cr (eigenproblem, no tables) ==")
    nbad = numeric_section()
    bad += nbad
    total += 1 + 4
    print(f"\n{total - bad}/{total} values match; {bad} differ")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
