#!/usr/bin/env python3
"""Independent re-derivation of fp.cylinder (double-acting hydraulic cylinder and its circuit).

Written from first principles and textbook relations (fluid-power texts, Euler/Johnson column theory,
orifice equation), not from the C# code. All internal maths is SI; reporting units match the sheet.

Model
  areas      A_b = pi D^2/4, A_r = pi d^2/4, A_a = A_b - A_r
  force      F_ext = (p A_b - pb A_a) eta_m ;  F_ret = (p A_a - pb A_b) eta_m ; regenerative F_ext = p A_r eta_m
  speed      v = Q eta_vc / A (regenerative extend: A_r) ; t = stroke / v
  pump       Q_p = Vg n eta_vp ; P = p Q_p / eta_tp ; T = P / omega
  motor      T = dp Vg / (2 pi) eta_hm
  lines      v = Q / (pi d^2/4) ; smallest bore d = sqrt(4 Q / (pi v_max))
  orifice    Q = Cd A sqrt(2 dp / rho)
  column     L_eff = K (stroke + L_add), r = d/4, lam = L_eff / r, lam_c = pi sqrt(2E/fy)
             Euler  F_cr = pi^2 E I / L_eff^2  (lam > lam_c)
             Johnson F_cr = A fy (1 - fy lam^2 / (4 pi^2 E))  (lam <= lam_c)
             rod load = max(push load, p A_b)   (stall thrust, no efficiency, no back pressure)

Exit code 0 when every KAT value and every extra check agrees within the tolerance.
"""
import math
import sys

PI = math.pi
fails = 0
nrows = 0


def check(case, sym, expected, indep, tol):
    global fails, nrows
    t = max(tol, 1e-6)
    rel = abs(indep - expected) / abs(expected) if expected != 0 else abs(indep - expected)
    ok = rel <= t
    nrows += 1
    if not ok:
        fails += 1
    print(f"{case:30s} {sym:9s} {expected:16.8g} {indep:16.8g} {rel:10.2e} {tol:8.1e} {'PASS' if ok else 'FAIL'}")


def cyl(mode="Standard", p=160.0, p_back=0.0, Q=36.0, rho=870.0, F_push=80.0, F_pull=40.0,
        D=100.0, d_r=70.0, stroke=500.0, eta_m=0.95, eta_vc=0.98, t_dwell=0.0,
        K_m=1.0, L_add=100.0, E=210.0, f_y=500.0, S_f=3.5,
        V_g=None, n_p=None, eta_vp=0.95, eta_tp=0.88,
        V_gm=None, dp_m=None, eta_hm=0.9,
        d_suc=40.0, d_prs=16.0, d_ret=25.0, v_suc_max=1.2, v_prs_max=5.0, v_ret_max=3.0,
        hoses=None, C_d=0.62, dp_o=None, d_o=None):
    """Inputs in sheet units (bar, L/min, mm, kN, GPa, MPa, cm3/rev, rpm, m/s, kg/m3). Returns dict in sheet units."""
    out = {}
    P = p * 1e5
    Pb = p_back * 1e5
    Dm, dr, st = D / 1e3, d_r / 1e3, stroke / 1e3
    Ab = PI * Dm ** 2 / 4
    Ar = PI * dr ** 2 / 4
    Aa = Ab - Ar
    out.update(A_b=Ab * 1e4, A_r=Ar * 1e4, A_a=Aa * 1e4, phi=Ab / Aa)
    regen = mode.lower().startswith("regen")
    Qm = Q / 6e4                                    # m3/s
    # forces
    F_std = (P * Ab - Pb * Aa) * eta_m
    F_ret = (P * Aa - Pb * Ab) * eta_m
    if regen:
        F_ext = P * Ar * eta_m
        out["F_std"] = F_std / 1e3
    else:
        F_ext = F_std
    out.update(F_ext=F_ext / 1e3, F_ret=F_ret / 1e3)
    # speeds
    A_ext = Ar if regen else Ab
    v_ext = Qm * eta_vc / A_ext
    v_ret = Qm * eta_vc / Aa
    t_ext, t_ret = st / v_ext, st / v_ret
    out.update(v_ext=v_ext * 1e3, v_ret=v_ret * 1e3, t_ext=t_ext, t_ret=t_ret, t_cycle=t_ext + t_ret + t_dwell)
    dVe, dVr = A_ext * st * 1e3, Aa * st * 1e3      # litres
    out.update(dV_ext=dVe, dV_ret=dVr, dV=max(dVe, dVr))
    out["u_push"] = F_push * 1e3 / F_ext if F_ext > 0 else float("inf")
    out["u_pull"] = F_pull * 1e3 / F_ret if F_ret > 0 else float("inf")
    # pump
    have_pump = V_g is not None and n_p is not None
    if have_pump:
        Qgeo = V_g * 1e-6 * n_p / 60                 # m3/s
        Qp = Qgeo * eta_vp
        Pw = P * Qp / eta_tp
        om = 2 * PI * n_p / 60
        Vg_req = Qm / (n_p / 60 * eta_vp) * 1e6      # cm3/rev
        out.update(Q_p=Qp * 6e4, Vg_req=Vg_req, u_pump=Vg_req / V_g,
                   P_pump=Pw / 1e3, T_shaft=Pw / om, n_shaft=n_p)
    else:
        Qp = Qm
        Qgeo = Qm
        out["P_pump"] = P * Qm / eta_tp / 1e3
    if V_gm is not None and dp_m is not None:
        out["T_motor"] = dp_m * 1e5 * V_gm * 1e-6 / (2 * PI) * eta_hm
    # lines
    Qline = max(Qm, Qp)
    Qsuc = max(Qm, Qgeo)
    Qrtn = Qline * Ab / Aa
    Qprs = Qrtn
    if regen:
        Qprs = max(Qrtn, Qline * Ab / Ar)
    out.update(Q_line=Qline * 6e4, Q_suc=Qsuc * 6e4, Q_prs=Qprs * 6e4, Q_rtn=Qrtn * 6e4)
    for tag, Qx, d, vmax in (("suc", Qsuc, d_suc, v_suc_max), ("prs", Qprs, d_prs, v_prs_max),
                             ("rtn", Qrtn, d_ret, v_ret_max)):
        v = Qx / (PI * (d / 1e3) ** 2 / 4)
        out["v_" + tag] = v
        out["u_" + tag] = v / vmax
        dmin = math.sqrt(4 * Qx / (PI * vmax)) * 1e3
        out["d_min_" + tag] = dmin
        if hoses:
            ok = [h for h in sorted(hoses) if h >= dmin]
            out["d_rec_" + tag] = ok[0] if ok else None
    # orifice
    if dp_o is not None:
        jet = C_d * math.sqrt(2 * dp_o * 1e5 / rho)
        out["d_o_req"] = math.sqrt(4 * Qm / (PI * jet)) * 1e3
        if d_o is not None:
            out["Q_o"] = jet * PI * (d_o / 1e3) ** 2 / 4 * 6e4
    # rod column
    Em, fy = E * 1e9, f_y * 1e6
    Leff = K_m * (stroke + L_add) / 1e3
    lam = Leff / (dr / 4)
    lam_c = PI * math.sqrt(2 * Em / fy)
    if lam > lam_c:
        Fcr = PI ** 2 * Em * (PI * dr ** 4 / 64) / Leff ** 2
    else:
        Fcr = Ar * fy * (1 - fy * lam ** 2 / (4 * PI ** 2 * Em))
    Fstall = P * Ab
    Fbuck = max(F_push * 1e3, Fstall)
    out.update(L_eff=Leff * 1e3, lam=lam, lam_c=lam_c, F_cr=Fcr / 1e3, F_allow=Fcr / S_f / 1e3,
               F_stall=Fstall / 1e3, F_buck=Fbuck / 1e3, u_buck=Fbuck * S_f / Fcr)
    return out


HOSES = [8, 10, 12.5, 16, 20, 25, 32, 40, 50]

print(f"{'case':30s} {'symbol':9s} {'KAT expected':>16s} {'independent':>16s} {'rel diff':>10s} {'tol':>8s} result")


def kat(case, inp, exp, tol=1e-9):
    r = cyl(**inp)
    for sym, val in exp.items():
        check(case, sym, val, r[sym], tol)


kat("ka-circuit-standard",
    dict(mode="Standard", p=160, p_back=0, Q=36, rho=870, F_push=80, F_pull=40, D=100, d_r=70, stroke=500,
         eta_m=0.95, eta_vc=0.98, t_dwell=0, K_m=1, L_add=100, E=210, f_y=500, S_f=3.5, V_g=28, n_p=1450,
         eta_vp=0.95, eta_tp=0.88, V_gm=16, dp_m=160, eta_hm=0.9, d_suc=40, d_prs=16, d_ret=25,
         v_suc_max=1.2, v_prs_max=5, v_ret_max=3, C_d=0.62, dp_o=10, d_o=3, hoses=HOSES),
    dict(A_b=78.53981633974483, A_r=38.48451000647497, A_a=40.05530633326986, phi=1.9607843137254901,
         F_ext=119.38052083641215, F_ret=60.88406562657019, v_ext=74.86648523042757, v_ret=146.79702986358348,
         t_ext=6.6785558112027905, t_ret=3.4060634637134233, t_cycle=10.084619274916214,
         dV_ext=3.926990816987242, dV_ret=2.0027653166634933, dV=3.926990816987242,
         Q_p=38.56999999999999, Vg_req=26.134301270417424, u_pump=0.9333679025149082,
         P_pump=11.687878787878786, T_shaft=76.97311793171664, n_shaft=1450, T_motor=36.66929888837269,
         Q_line=38.56999999999999, Q_suc=40.6, Q_prs=75.62745098039215, Q_rtn=75.62745098039215,
         v_suc=0.5384742241275793, v_prs=6.269001383838239, v_rtn=2.5677829668201424,
         u_suc=0.4487285201063161, u_prs=1.2538002767676477, u_rtn=0.8559276556067141,
         d_min_suc=26.794880708264138, d_min_prs=17.91571575049453, d_min_rtn=23.129089579017077,
         d_rec_suc=32, d_rec_prs=20, d_rec_rtn=25, d_o_req=5.0694069278021825, Q_o=12.607550087575712,
         L_eff=600, lam=34.285714285714285, lam_c=91.0520054524614, F_cr=1787.8069776735522,
         F_allow=510.8019936210149, F_stall=125.66370614359174, F_buck=125.66370614359174,
         u_buck=0.2460125600778818, u_push=0.6701260761764014, u_pull=0.6569863491925504))

kat("ka-regenerative",
    dict(mode="Regenerative extend", p=160, Q=36, F_push=40, F_pull=40, D=100, d_r=70, stroke=500,
         V_g=28, n_p=1450, d_prs=25, d_ret=25),
    dict(F_std=119.38052083641215, F_ext=58.496455209841955, v_ext=152.78874536821954,
         t_ext=3.2724923474893677, t_cycle=6.678555811202791, dV_ext=1.9242255003237485,
         dV_ret=2.0027653166634933, dV=2.0027653166634933, Q_prs=78.7142857142857,
         v_prs=2.672590434853617, u_prs=0.5345180869707234, d_min_prs=18.27768596832493,
         u_push=0.6838021185473483, F_buck=125.66370614359174, u_buck=0.2460125600778818))

kat("ka-euler-slender",
    dict(p=160, Q=36, F_push=15, F_pull=40, D=80, d_r=40, stroke=2000),
    dict(L_eff=2100, lam=210, lam_c=91.0520054524614, F_cr=59.05957462914251, F_allow=16.874164179755002,
         F_stall=80.4247719318987, F_buck=80.4247719318987, u_buck=4.766148478535569,
         F_ret=57.30265000147782, F_ext=76.40353333530378))

kat("ka-back-pressure-fixed-free",
    dict(p=160, p_back=10, Q=36, F_push=80, F_pull=40, D=100, d_r=70, stroke=300, K_m=2),
    dict(F_ext=115.57526673475151, F_ret=53.42278307429443, L_eff=800, lam=45.714285714285715,
         F_cr=1681.7036822789553, F_allow=480.4867663654158, F_buck=125.66370614359174,
         u_buck=0.26153416689112935, t_ext=4.007133486721674, t_ret=2.043638078228054, dV=2.356194490192345))

kat("ka-orifice",
    dict(p=160, Q=22.619467105846514, rho=800, D=100, d_r=70, stroke=500, C_d=0.6, dp_o=10, d_o=4),
    dict(d_o_req=4, Q_o=22.61946710584651))

kat("ka-lossless",
    dict(p=100, Q=47.1238898038469, D=100, d_r=56, stroke=400, eta_m=1, eta_vc=1),
    dict(A_a=53.90972993560086, F_ext=78.53981633974483, F_ret=53.90972993560086, v_ext=99.99999999999999,
         t_ext=4.000000000000001, v_ret=145.68764568764567, t_ret=2.7456000000000005, dV=3.1415926535897936))


# ---------------------------------------------------------------------------------------------
# Extra checks (not in the product KAT list). The product values were observed with the Probe tool
# on release/v0.26.0 @ 2f53791c (inputs: /tmp/claude-0/probe-inputs/cyl_x*.json) and are hard-coded here
# so that this script runs alone. Tolerance 1e-6 (the probe prints ten significant figures).
# ---------------------------------------------------------------------------------------------
print()
print("extra probes: product value (hard-coded from the Probe run) against the independent value")


def extra(name, inp, product, tol=1e-6):
    r = cyl(**inp)
    for sym, val in product.items():
        check(name[:30], sym, val, r[sym], tol)


extra("x1 regen typical 63/36",
      dict(mode='Regenerative extend', p=120, Q=60, D=63, d_r=36, stroke=800, F_push=25, F_pull=20, V_g=25, n_p=1500, d_prs=20, d_ret=20, K_m=0.7, hoses=HOSES),
      dict(F_ext=11.60378663, F_std=35.53659654, v_ext=962.7891619, t_cycle=2.544690049, dV=1.679495433, Q_prs=183.75, v_prs=9.748240264, d_rec_prs=32.0, P_pump=8.096590909, T_shaft=51.54449861, Vg_req=42.10526315937748, F_cr=358.53658870000004, u_buck=0.3651630187))
extra("x2 back 40 bar, K=0.7, motor",
      dict(p=250, p_back=40, Q=90, D=125, d_r=90, stroke=1200, K_m=0.7, F_push=200, F_pull=100, V_gm=32, dp_m=200),
      dict(F_ext=268.9978892, F_ret=93.73236206, u_pull=1.066867385, T_motor=91.67324722, P_pump=42.61363636, v_prs=15.49083878, lam=40.44444444, F_cr=2867.062066, u_buck=0.3745250458))
extra("x3 near zero",
      dict(p=1, Q=0.1, D=10, d_r=4, stroke=1, F_push=0, F_pull=0, L_add=0),
      dict(F_ext=0.007461282552, F_ret=0.006267477344, v_ext=20.7962459, t_cycle=0.08847750739, d_min_prs=0.7108120620999999, lam=1.0, F_cr=6.282806367, u_buck=0.004375263873))
extra("x4 very large",
      dict(p=700, Q=2000, D=500, d_r=450, stroke=5000, K_m=2.5, L_add=5000, F_push=15000, F_pull=100, V_g=1000, n_p=2000, d_suc=200, d_prs=200, d_ret=200),
      dict(F_ext=13057.244470000001, v_ext=166.3699672, dV=981.7477042, P_pump=2518.939394, T_shaft=12027.04968, u_pump=1.052631579, Q_prs=10526.315789999999, F_cr=6675.1184490000005, u_buck=7.865028973))
extra("x5 lambda just below lam_c (Johnson)",
      dict(d_r=40, D=80, stroke=910.0, L_add=0, F_push=10),
      dict(lam=91.0, F_cr=314.5180346, u_buck=0.8949779371))
extra("x6 lambda just above lam_c (Euler)",
      dict(d_r=40, D=80, stroke=911.0, L_add=0, F_push=10),
      dict(lam=91.1, F_cr=313.8283332, u_buck=0.8969448325))
extra("x7 regen with back pressure (ignored)",
      dict(mode='Regenerative extend', p_back=30, V_g=28, n_p=1450),
      dict(F_std=107.9647585, F_ext=58.49645521, F_ret=38.50021797, u_pull=1.038955157))
extra("x8 oversize pump 100 cm3 at 1500 rpm, 210 bar",
      dict(V_g=100, n_p=1500, p=210),
      dict(Q_p=142.5, P_pump=56.67613636, T_shaft=360.8114903, u_pump=0.2526315789, Q_suc=150.0, v_suc=1.989436789, Q_prs=279.41176470000005, d_min_prs=34.43632327))
extra("x9 fixed-fixed K=0.5 Euler",
      dict(D=63, d_r=28, stroke=1500, K_m=0.5, p=100, F_push=20, S_f=3.5),
      dict(lam=114.2857143, F_cr=97.71046728, F_stall=31.17245311, u_buck=1.116600799))
extra("x10 low E and f_y",
      dict(E=50, f_y=100, D=63, d_r=45, stroke=1000),
      dict(lam_c=99.34588266, lam=97.77777778, F_cr=82.01213563, u_buck=3.414128871))
extra("x11 thin annulus d_r=99.5",
      dict(d_r=99.5),
      dict(A_a=0.783434668, F_ret=1.190820695, v_ret=7505.412053, Q_prs=3609.0225564, v_prs=299.1634269))
extra("x12 dwell 12.5 s",
      dict(t_dwell=12.5),
      dict(t_cycle=22.58461927))

# Notes from the Probe run, not asserted numerically:
#  * lambda = 910.5/10 = 91.05 sits on lam_c = 91.052: the Johnson value (stroke 910) and the Euler value (stroke 911)
#    differ by 0.2 percent in u_buck (0.89498 and 0.89694), so the two branches meet.
#  * Blocks seen: d_r >= D; p_back = 51 bar at p = 100 bar (retract force <= 0); V_g without n_p; eta_tp > eta_vp;
#    dp_o > p; d_o without dp_o.  Warning seen: hoses list smaller than the minimum bore ("size").

print()
print(f"{nrows} values compared, {fails} failed")
sys.exit(1 if fails else 0)
