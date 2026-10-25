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
EXTRAS = []  # filled below from probe runs


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
    print(f"\n{total - bad}/{total} values match; {bad} differ")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
