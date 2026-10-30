#!/usr/bin/env python3
"""Independent re-derivation of elec.motor-sizing (Track P, P1).

Method (written before reading the C# Calculate method):
  k [m/rad of motor] : screw p/(2 pi i); belt D/(2 i); rack/pinion D/(2 i).
  Linear load force  : F = m g (sin(theta) + mu cos(theta)) + F_p, g = 9.80665 m/s^2.
  Reflected inertia  : J_refl = m k^2 / eta (linear) or J_L / (i^2 eta) (rotary).
  Load torque        : T_load = F k / eta (linear) or T_L / (i eta) (rotary).
  Trapezoid          : t_acc = f_acc t_move; v_pk = s / (t_move - t_acc); a = v_pk / t_acc.
  S-curve (sine acc.): a(t) = a_pk sin(pi t / t_acc), a_pk = (pi/2) a_trap, same v_pk.
  Torque             : T = J_total * alpha + T_load, J_total = J_refl + J_mech + J_motor.
  RMS                : sqrt(sum(T_i^2 t_i) / (t_move + t_dwell)), dwell at zero torque.
Continuous duty (1.1.0, added at the A7a re-check): a rotary load at steady speed n_L through ratio i.
  omega = 2 pi n_L i / 60; T_load = T_L / (i eta); T_rms = T_load; the whole inertia runs up linearly in
  t_start while carrying the full load: T_pk = J_total omega / t_start + T_load; P_cont = T_load omega.
  The formulas follow from the physics and the 1.1.0 spec paragraph; the known-answer case is the spec's own analytic one.
Rated power (1.4.0, added at the Addendum B-2 re-check): P_rated = T_rated * omega_rated, omega_rated = 2 pi n_rated / 60,
  the candidate motor's nameplate shaft power (continuous duty only; needs n_rated; the running speed is not a substitute).
Sources: Hughes & Drury, Electric Motors and Drives, 5th ed., chapter on drive selection
(inertia referred through a gear ratio, equivalent RMS torque); manufacturer servo-sizing practice.
"""
import math
import sys

G = 9.80665
ROWS = []


def check(case, symbol, expected, value, tol, note=""):
    tol_eff = max(tol, 1e-6)
    rel = abs(value - expected) / abs(expected) if expected != 0 else abs(value - expected)
    ROWS.append((case, symbol, expected, value, rel, tol, rel <= tol_eff, note))


def motor(mech, s, t_move, t_dwell, f_acc=1 / 3, profile="trap", i=1.0, eta=0.9, p_mm=None, D_mm=None,
          J_mech=0.0, m=0.0, mu=0.0, theta_deg=0.0, F_p=0.0, J_L=0.0, T_L=0.0, J_motor=0.0,
          T_rated=1.0, T_peak=1.0, n_max=1000.0, SF=1.0):
    th = math.radians(theta_deg)
    o = {}
    if mech in ("screw", "belt", "rack"):
        if mech == "screw":
            k = (p_mm / 1000.0) / (2 * math.pi * i)
        else:
            k = (D_mm / 1000.0) / (2 * i)
        F = m * G * (math.sin(th) + mu * math.cos(th)) + F_p
        J_refl = m * k * k / eta
        T_load = F * k / eta
        theta_m = s / k  # motor angle for the move [rad]
    else:  # rotary
        ratio = 1.0 if mech == "direct" else i
        k = None
        J_refl = J_L / (ratio ** 2 * eta)
        T_load = T_L / (ratio * eta)
        theta_m = s * 2 * math.pi * ratio
    J_total = J_refl + J_mech + J_motor
    t_acc = f_acc * t_move
    t_cv = t_move - 2 * t_acc
    w_pk = theta_m / (t_move - t_acc)  # motor speed at the end of acceleration [rad/s]
    a_trap = w_pk / t_acc
    a_pk = a_trap if profile == "trap" else math.pi / 2 * a_trap
    T_acc = J_total * a_pk
    T_pk = T_acc + T_load
    T_dec = -T_acc + T_load
    # RMS torque: integral of T^2 over the phases
    if profile == "trap":
        int_acc = (T_acc + T_load) ** 2 * t_acc
        int_dec = (-T_acc + T_load) ** 2 * t_acc
    else:
        # T(t) = T_acc sin(pi t/t_acc) + T_load over t_acc
        base = T_acc ** 2 * t_acc / 2 + T_load ** 2 * t_acc
        cross = 2 * T_acc * T_load * (2 * t_acc / math.pi)
        int_acc = base + cross
        int_dec = base - cross
    int_cv = T_load ** 2 * t_cv
    T_rms = math.sqrt((int_acc + int_cv + int_dec) / (t_move + t_dwell))
    n_pk = w_pk * 60 / (2 * math.pi)
    # RMS mechanical power (1.2.0): numerical integral of (T(t) w(t))^2 over the cycle, dwell at zero.
    # Midpoint rule, 40000 steps per phase. Acceleration: T = T_load + T_acc g(u), w = w_pk h(u); deceleration mirrors w
    # and flips the acceleration torque. trap: g = 1, h = u. S-curve: g = sin(pi u), h = (1 - cos(pi u))/2.
    nst = 40000
    acc_int = dec_int = 0.0
    for q in range(nst):
        u = (q + 0.5) / nst
        g, h = (1.0, u) if profile == "trap" else (math.sin(math.pi * u), (1 - math.cos(math.pi * u)) / 2)
        acc_int += ((T_load + T_acc * g) * w_pk * h) ** 2 * t_acc / nst
        dec_int += ((T_load - T_acc * g) * w_pk * (1 - h if profile == "trap" else h)) ** 2 * t_acc / nst
    # deceleration speed: trap w falls linearly (1 - u); S-curve mirrors (1 + cos)/2 = h at 1 - u, torque g is symmetric.
    cv_int = (T_load * w_pk) ** 2 * t_cv
    P_rms = math.sqrt((acc_int + dec_int + cv_int) / (t_move + t_dwell))
    o.update(P_rms=P_rms, k=k, T_load=T_load, J_refl=J_refl, J_total=J_total, w_pk=w_pk, n_pk=n_pk, a_pk=a_pk,
             T_acc=T_acc, T_pk=T_pk, T_dec=T_dec, T_rms=T_rms, J_ratio=(J_refl + J_mech) / J_motor,
             t_acc=t_acc, theta_m=theta_m)
    if mech in ("screw", "belt", "rack"):
        o["v_pk"] = w_pk * k
    return o


def cont(mech, T_L, n_L, t_start, i=1.0, eta=0.9, J_L=0.0, J_mech=0.0, J_motor=2e-4,
         T_rated=1.0, T_peak=1.0, n_max=1000.0, SF=1.0, n_rated=None):
    """Continuous rotary duty: steady torque and speed, linear run-up carrying the full load."""
    ratio = 1.0 if mech == "direct" else i
    w = 2 * math.pi * n_L / 60.0 * ratio            # motor speed [rad/s]
    T_load = T_L / (ratio * eta)
    J_refl = J_L / (ratio ** 2 * eta)
    J_total = J_refl + J_mech + J_motor
    alpha = w / t_start
    T_acc = J_total * alpha
    T_pk = T_acc + T_load
    P_rated = None if n_rated is None else T_rated * 2 * math.pi * n_rated / 60.0   # W, nameplate shaft power
    return dict(P_rated=P_rated, T_load=T_load, J_refl=J_refl, J_total=J_total, alpha_pk=alpha, T_acc=T_acc, T_pk=T_pk,
                T_rms=T_load, n_pk=w, n_pk_rpm=w * 60 / (2 * math.pi), P_cont=T_load * w, P_pk=T_pk * w,
                P_rms=T_load * w, J_ratio=(J_refl + J_mech) / J_motor, duty=T_load / T_rated, u_peak=SF * T_pk / T_peak,
                u_rms=SF * T_load / T_rated, u_speed=(w * 60 / (2 * math.pi)) / n_max)


# ---------------- Known-answer cases ----------------
r = motor("screw", 0.5, 0.5, 0.5, 1 / 3, "trap", i=1, eta=0.9, p_mm=10, J_mech=1e-4, m=50, mu=0.1,
          theta_deg=0, F_p=0, J_motor=2e-4)
for sym, e, t in [("v_pk", 1.5, 1e-9), ("n_pk", 9000, 1e-9), ("J_refl", 1.4072e-4, 1e-4),
                  ("T_acc", 2.4923, 1e-4), ("T_load", 0.08670, 1e-3), ("T_pk", 2.579, 1e-3),
                  ("T_rms", 1.4402, 1e-3), ("J_ratio", 1.2036, 1e-3)]:
    check("ka-ball-screw", sym, e, r[sym], t)
check("ka-ball-screw", "P_rms", 0.7841424, r["P_rms"] / 1000.0, 1e-6)

r = motor("geared", 0.25, 0.3, 0.7, 1 / 3, "trap", i=10, eta=0.85, J_mech=0, J_L=0.5, T_L=0, J_motor=5e-4)
for sym, e, t in [("n_pk", 750, 1e-6), ("J_refl", 5.882e-3, 1e-3), ("T_pk", 5.0126, 1e-3),
                  ("T_rms", 2.2417, 1e-3), ("J_ratio", 11.76, 2e-3)]:
    check("ka-geared-index-table", sym, e, r[sym], t)
check("ka-geared-index-table", "P_rms", 0.1016518, r["P_rms"] / 1000.0, 1e-6)

# ka-continuous-pump (spec 1.1.0, analytic): 700 rpm pump through 4:1, 2 s run-up
r = cont("geared", 3.0, 700, 2.0, i=4, eta=0.9, J_L=0.01, J_mech=0.0, J_motor=5e-4, T_rated=2.0, T_peak=6.0, n_max=3500, SF=1.25,
         n_rated=2850)
for sym, e, t in [("n_pk_rpm", 2800, 1e-9), ("T_load", 0.8333333, 1e-6), ("J_refl", 6.944444e-4, 1e-6),
                  ("J_total", 1.194444e-3, 1e-6), ("T_acc", 0.1751147, 1e-6), ("T_pk", 1.008448, 1e-6),
                  ("T_rms", 0.8333333, 1e-6), ("P_cont", 244.3461, 1e-6), ("u_peak", 0.2100933, 1e-6),
                  ("u_rms", 0.5208333, 1e-6), ("u_speed", 0.8, 1e-9), ("J_ratio", 1.388889, 1e-6)]:
    check("ka-continuous-pump", sym, e, r[sym], t)
check("ka-continuous-pump", "P_rms", 0.2443461, r["P_rms"] / 1000.0, 1e-6)
check("ka-continuous-pump", "P_rated", 596.9026042, r["P_rated"], 1e-6)   # 1.4.0: 2.0 N*m x 2 pi x 2850/60

# ---------------- Extra checks ----------------
# Product values below were read from `Probe run elec.motor-sizing` (10 significant figures).
# Motor data for every extra case: T_rated 2 N*m, T_peak 6 N*m, n_max 10000 rpm, SF 1.25
# (E7 uses T_rated 5000 N*m and T_peak 15000 N*m).
MOT = dict(T_rated=2.0, T_peak=6.0, n_max=10000.0, SF=1.25)
EXTRA_CASES = [
    ('E1 belt, 10 deg incline, F_p, i=3',
     {'mech': 'belt', 'i': 3, 'eta': 0.95, 'D_mm': 100, 'm': 20, 'mu': 0.05, 'theta_deg': 10, 'F_p': 30, 'J_mech': 5e-05, 's': 0.8, 't_move': 1.2, 'f_acc': 0.25, 't_dwell': 0.8, 'J_motor': 0.0001},
     {'T_load': 1.293259701, 'J_refl': 0.005847953216, 'T_acc': 1.066302794, 'T_pk': 2.359562495, 'T_dec': 0.2269569074, 'T_rms': 1.159574443, 'J_ratio': 58.97953216, 'P_pk': 125.8433331, 'u_peak': 0.4915755199, 'u_rms': 0.7247340266, 'u_speed': 0.05092958179, 'n_pk_rpm': 509.2958179}),
    ('E2 rack, 20 deg decline, S-curve',
     {'mech': 'rack', 'i': 5, 'eta': 0.8, 'D_mm': 60, 'm': 200, 'mu': 0.05, 'theta_deg': -20, 'profile': 'scurve', 's': 1.5, 't_move': 2, 'f_acc': 0.3, 't_dwell': 1, 'J_mech': 0.0001, 'J_motor': 0.0002},
     {'T_load': -4.33996501, 'J_refl': 0.009, 'T_acc': 4.347739833, 'T_pk': 0.007774823248, 'T_dec': -8.687704843, 'T_rms': 4.041958996, 'J_ratio': 45.5, 'P_pk': 1.388361294, 'u_peak': 0.001619754843, 'u_rms': 2.526224372, 'u_speed': 0.1705231533, 'n_pk_rpm': 1705.231533}),
    ('E3 direct rotary, triangular, S-curve',
     {'mech': 'direct', 'i': 1, 'eta': 0.9, 'J_L': 0.02, 'T_L': 1.5, 'profile': 'scurve', 's': 2, 't_move': 0.8, 'f_acc': 0.5, 't_dwell': 0.2, 'J_mech': 0, 'J_motor': 0.0002},
     {'T_load': 1.666666667, 'J_refl': 0.02222222222, 'T_acc': 2.766230789, 'T_pk': 4.432897456, 'T_dec': -1.099564122, 'T_rms': 2.298485443, 'J_ratio': 111.1111111, 'P_pk': 139.2635808, 'u_peak': 0.9235203033, 'u_rms': 1.436553402, 'u_speed': 0.03, 'n_pk_rpm': 300.0}),
    ('E4 gearbox rotary i=25, T_L=40',
     {'mech': 'geared', 'i': 25, 'eta': 0.7, 'J_L': 3, 'T_L': 40, 's': 0.125, 't_move': 1.5, 'f_acc': 0.2, 't_dwell': 3, 'J_mech': 0, 'J_motor': 0.0002},
     {'T_load': 2.285714286, 'J_refl': 0.006857142857, 'T_acc': 0.3849074333, 'T_pk': 2.670621719, 'T_dec': 1.900806852, 'T_rms': 1.327121106, 'J_ratio': 34.28571429, 'P_pk': 43.69794569, 'u_peak': 0.5563795248, 'u_rms': 0.8294506912, 'u_speed': 0.015625, 'n_pk_rpm': 156.25}),
    ('E5 screw, 500 kg vertical up, 10 s dwell',
     {'mech': 'screw', 'p_mm': 10, 'i': 1, 'eta': 0.9, 'm': 500, 'mu': 0.1, 'theta_deg': 90, 's': 0.2, 't_move': 1, 't_dwell': 10, 'J_mech': 0.0001, 'J_motor': 0.0002},
     {'T_load': 8.670982348, 'J_refl': 0.001407238662, 'T_acc': 0.9654207188, 'T_pk': 9.636403067, 'T_dec': 7.705561629, 'T_rms': 2.625180392, 'J_ratio': 7.536193308, 'P_pk': 1816.419185, 'u_peak': 2.007583972, 'u_rms': 1.640737745, 'u_speed': 0.18, 'n_pk_rpm': 1800.0}),
    ('E6 near zero: m=0, F_p=5 N, s=0.1 mm',
     {'mech': 'screw', 'p_mm': 10, 'i': 1, 'eta': 0.9, 'm': 0, 'mu': 0.1, 'F_p': 5, 's': 0.0001, 't_move': 0.01, 't_dwell': 0, 'J_mech': 0.0001, 'J_motor': 0.0002},
     {'T_load': 0.008841941283, 'J_refl': 0.0, 'T_acc': 0.8482300165, 'T_pk': 0.8570719578, 'T_dec': -0.8393880752, 'T_rms': 0.6926333473, 'J_ratio': 0.5, 'P_pk': 8.077712898, 'u_peak': 0.1785566579, 'u_rms': 0.4328958421, 'u_speed': 0.009, 'n_pk_rpm': 90.0}),
    ('E7 very large: 1e5 kg, 100 m, 5 deg',
     {'mech': 'screw', 'p_mm': 100, 'i': 1, 'eta': 0.9, 'm': 100000.0, 'mu': 0.1, 'theta_deg': 5, 's': 100, 't_move': 600, 't_dwell': 60, 'J_mech': 1, 'J_motor': 5, 'T_rated': 5000.0, 'T_peak': 15000.0},
     {'T_load': 3239.049142, 'J_refl': 28.14477323, 'T_acc': 2.681724219, 'T_pk': 3241.730867, 'T_dec': 3236.367418, 'T_rms': 3088.312888, 'J_ratio': 5.828954647, 'P_pk': 50920.98938, 'u_peak': 0.2701442389, 'u_rms': 0.7720782219, 'u_speed': 0.015, 'n_pk_rpm': 150.0}),
    ('E8 ball-screw KAT inputs, S-curve',
     {'mech': 'screw', 'p_mm': 10, 'i': 1, 'eta': 0.9, 'm': 50, 'mu': 0.1, 's': 0.5, 't_move': 0.5, 't_dwell': 0.5, 'profile': 'scurve', 'J_mech': 0.0001, 'J_motor': 0.0002},
     {'T_load': 0.08670982348, 'J_refl': 0.0001407238662, 'T_acc': 3.914793188, 'T_pk': 4.001503012, 'T_dec': -3.828083365, 'T_rms': 1.599383292, 'J_ratio': 1.203619331, 'P_pk': 3771.32774, 'u_peak': 0.8336464608, 'u_rms': 0.9996145575, 'u_speed': 0.9, 'n_pk_rpm': 9000.0}),
]
for name, mk, prod in EXTRA_CASES:
    args = {**MOT, **mk}
    r = motor(**args)
    mot = {k: args[k] for k in ("T_rated", "T_peak", "n_max", "SF")}
    r["n_pk_rpm"] = r["n_pk"]
    r["P_pk"] = r["T_pk"] * r["w_pk"]  # the product's definition: T_pk * omega_pk
    r["u_peak"] = mot["SF"] * r["T_pk"] / mot["T_peak"]
    r["u_rms"] = mot["SF"] * r["T_rms"] / mot["T_rated"]
    r["u_speed"] = r["n_pk"] / mot["n_max"]
    for sym, pv in prod.items():
        check("extra " + name.split()[0], sym, pv, r[sym], 1e-8, "product value hard-coded")

# RMS power extras (1.2.0). Product P_rms (kW) read from `Probe run` at 0772fcf8; the independent value is a numerical integral.
RP_CASES = [
    ("RP1 rack 200 kg, -20 deg, S-curve, template defaults", dict(mech="rack", i=5, eta=0.8, D_mm=60, m=200, mu=0.1, theta_deg=-20, profile="scurve", s=1.5, t_move=2, f_acc=1 / 3, t_dwell=0.5, J_mech=1e-4, J_motor=2e-4), 0.5175774738),
    ("RP2 vertical screw 500 kg, 10 s dwell", EXTRA_CASES[4][1], 0.3682229979),
    ("RP3 ball-screw KAT inputs, S-curve", EXTRA_CASES[7][1], 0.8431893042),
]
for nm, mk, pv in RP_CASES:
    rr = motor(**{**MOT, **mk})
    check("extra " + nm.split()[0], "P_rms", pv, rr["P_rms"] / 1000.0, 1e-6, "product value hard-coded (kW)")

# Continuous-duty extras (elec.motor-sizing 1.1.0). Product values were read from `Probe run elec.motor-sizing`
# (10 significant figures, SI units). Motor data: J_motor 2e-4, T_rated 2, T_peak 6 N*m, n_max 10000 rpm, SF 1.25,
# unless the case says otherwise. CE1 is the wet-heating circulator (pack.wet-heating), T_L from B1.
CONT_CASES = [
    ('CE1 circulator, direct rotary, 2800 rpm, 5 s run-up',
     dict(mech="direct", T_L=0.002037166263, n_L=2800, t_start=5.0, i=1, eta=0.9, J_L=0.002, J_mech=0.0, J_motor=2e-4,
          T_rated=2.0, T_peak=6.0, n_max=10000.0, SF=1.25),
     {'T_load': 0.00226351807, 'J_refl': 0.002222222222, 'J_total': 0.002422222222, 'alpha_pk': 58.64306287, 'T_acc': 0.1420465301, 'T_pk': 0.1443100481, 'T_rms': 0.00226351807, 'n_pk': 293.2153143, 'P_cont': 0.6636981624, 'P_pk': 42.31391612, 'u_peak': 0.03006459336, 'u_rms': 0.001414698794, 'u_speed': 0.28, 'J_ratio': 11.11111111, 'duty': 0.001131759035}),
    ('CE2 gearbox 10:1, eta 0.8, 150 rpm, 3 s run-up',
     dict(mech="geared", T_L=12.0, n_L=150, t_start=3.0, i=10, eta=0.8, J_L=0.5, J_mech=1e-4, J_motor=5e-4,
          T_rated=5.0, T_peak=15.0, n_max=3000.0, SF=1.5),
     {'T_load': 1.5, 'J_refl': 0.00625, 'J_total': 0.00685, 'alpha_pk': 52.35987756, 'T_acc': 0.3586651613, 'T_pk': 1.858665161, 'T_rms': 1.5, 'n_pk': 157.0796327, 'P_cont': 235.619449, 'P_pk': 291.9584408, 'u_peak': 0.1858665161, 'u_rms': 0.45, 'u_speed': 0.5, 'J_ratio': 12.7, 'duty': 0.3}),
    ('CE3 overhauling load, T_L -2 N*m, 2:1',
     dict(mech="geared", T_L=-2.0, n_L=300, t_start=1.0, i=2, eta=0.9, J_L=0.05, J_mech=0.0, J_motor=2e-4,
          T_rated=2.0, T_peak=6.0, n_max=10000.0, SF=1.25),
     {'T_load': -1.111111111, 'J_refl': 0.01388888889, 'J_total': 0.01408888889, 'alpha_pk': 62.83185307, 'T_acc': 0.8852309966, 'T_pk': -0.2258801145, 'T_rms': -1.111111111, 'n_pk': 62.83185307, 'P_cont': -69.81317008, 'P_pk': -14.19246617, 'u_peak': -0.04705835719, 'u_rms': -0.6944444444, 'u_speed': 0.06, 'J_ratio': 69.44444444, 'duty': -0.5555555556}),
]
for name, kw, prod in CONT_CASES:
    r = cont(**kw)
    for sym, pv in prod.items():
        check("extra " + name.split()[0], sym, pv, r[sym], 1e-8, "product value hard-coded")


# Rated-power extras (elec.motor-sizing 1.4.0, Addendum B-2 re-check at 2f53791c). Product values read from `Probe run`
# (10 significant figures). HPU1 is the E1 step of pack.hydraulic-power-unit (pump torque and speed linked from F1).
# HPU2 and HPU3 are the same sheet with n_rated 100 rpm and 5000 rpm (finding G1: no consistency check on n_rated).
HPU = dict(mech="direct", T_L=76.97311793171667, n_L=1450.0000000000002, t_start=2.0, i=1, eta=0.99, J_L=0.004, J_mech=0.001,
           J_motor=0.1, T_rated=120.0, T_peak=275.0, n_max=3000.0, SF=1.25)
RATED_CASES = [
    ("HPU1 pack E1, n_rated 1460 rpm", dict(HPU, n_rated=1460),
     {'P_rated': 18346.9011, 'T_load': 77.75062417, 'T_pk': 85.72548308, 'P_cont': 11805.93817, 'u_rms': 0.8099023351, 'u_peak': 0.3896612867, 'J_total': 0.105040404}),
    ("HPU2 same, n_rated 100 rpm", dict(HPU, n_rated=100), {'P_rated': 1256.637061, 'P_cont': 11805.93817}),
    ("HPU3 same, n_rated 5000 rpm", dict(HPU, n_rated=5000), {'P_rated': 62831.85307, 'P_cont': 11805.93817}),
]
for name, kw, prod in RATED_CASES:
    rr = cont(**kw)
    for sym, pv in prod.items():
        check("extra " + name.split()[0], sym, pv, rr[sym], 1e-6, "product value hard-coded")

# ---------------- Findings evidence (informational, does not change the exit code) ----------------
FIND = []


def finding(tag, text):
    FIND.append((tag, text))


# F01: dwell at zero torque while gravity holds the load (E5: 500 kg vertical axis, no brake assumed).
r = motor(**{**MOT, **EXTRA_CASES[4][1]})
hold = 500 * G * math.sin(math.radians(90)) * r["k"]
ta, tc = r["t_acc"], 1.0 - 2 * r["t_acc"]
rms_hold = math.sqrt((r["T_pk"] ** 2 * ta + r["T_load"] ** 2 * tc + r["T_dec"] ** 2 * ta + hold ** 2 * 10.0) / 11.0)
finding("P1-elec.motor-sizing-01", f"E5 vertical axis: holding torque {hold:.3f} N*m for 10 s. RMS with hold {rms_hold:.3f} N*m; "
        f"product (dwell at zero) {EXTRA_CASES[4][2]['T_rms']:.3f} N*m; ratio {rms_hold / EXTRA_CASES[4][2]['T_rms']:.2f}.")
# F02: peak-torque check uses T_pk only; with a negative load torque the deceleration torque is larger.
r2 = motor(**{**MOT, **EXTRA_CASES[1][1]})
pk_env = max(abs(r2["T_pk"]), abs(r2["T_dec"]))
finding("P1-elec.motor-sizing-02", f"E2 decline: |T_dec| = {abs(r2['T_dec']):.3f} N*m, T_pk = {r2['T_pk']:.4f} N*m. "
        f"SF*envelope/T_peak = {MOT['SF'] * pk_env / MOT['T_peak']:.3f}; product u_peak = {EXTRA_CASES[1][2]['u_peak']:.4f} (passes).")
# F03: S-curve peak power: scan T(t) * w(t) over the acceleration phase.
rs = motor(**{**MOT, **EXTRA_CASES[7][1]})
N = 200001
pmax = 0.0
for n in range(N):
    x = math.pi * n / (N - 1)
    T = rs["T_acc"] * math.sin(x) + rs["T_load"]
    w = rs["w_pk"] * (1 - math.cos(x)) / 2
    pmax = max(pmax, T * w)
finding("P1-elec.motor-sizing-03", f"E8 S-curve: true peak power {pmax:.1f} W; product P_pk {EXTRA_CASES[7][2]['P_pk']:.1f} W; "
        f"ratio {EXTRA_CASES[7][2]['P_pk'] / pmax:.3f}.")


# C1: continuous duty, T_L left blank -> zero load torque, zero utilisation, all checks pass (product: warning "default used" only).
rc = cont("direct", 0.0, 2800, 5.0, i=1, eta=0.9, J_L=0.002, J_mech=0.0, J_motor=2e-4, T_rated=2.0, T_peak=6.0, n_max=10000.0, SF=1.25)
finding("P1-elec.motor-sizing-C1", f"continuous duty, T_L blank (default 0): T_load {rc['T_load']:.3f}, u_rms {rc['u_rms']:.3f}, "
        f"u_peak {rc['u_peak']:.4f}. Product: all three checks pass, only a 'default used' warning.")
# C2: a negative T_L gives a negative RMS torque and a negative utilisation (an RMS cannot be negative).
rn = cont("geared", -10.0, 300, 1.0, i=2, eta=0.9, J_L=0.05, J_mech=0.0, J_motor=2e-4, T_rated=2.0, T_peak=6.0, n_max=10000.0, SF=1.25)
finding("P1-elec.motor-sizing-C2", f"continuous duty, T_L -10 N*m, 2:1: T_rms {rn['T_rms']:.4f} N*m, u_rms {rn['u_rms']:.4f}, "
        f"u_peak {rn['u_peak']:.4f}; |T_rms| would give u_rms {abs(rn['u_rms']):.4f}. Product: checks FAIL on the negative values.")
# C3: direct coupling still carries the mechanism efficiency (default 0.9).
r1 = cont("direct", 0.002037166263, 2800, 5.0, eta=1.0, J_L=0.002)
r9 = cont("direct", 0.002037166263, 2800, 5.0, eta=0.9, J_L=0.002)
finding("P1-elec.motor-sizing-C3", f"direct coupling: T_load {r9['T_load']:.6f} N*m at eta 0.9 against {r1['T_load']:.6f} at eta 1 "
        f"({100 * (r9['T_load'] / r1['T_load'] - 1):+.1f} %). Since 1.2.0 a warning names the 0.9 for a direct coupling; the default is unchanged.")
# C4: continuous duty prints 'template default used' notices for inputs it ignores (probe, CE1 inputs).
finding("P1-elec.motor-sizing-C4", "continuous duty ignores mu, theta, F_p, profile, s, t_move, f_acc and t_dwell, yet the probe lists "
        "eight 'was not entered; the template default ... is used' warnings for them, plus the fixed 'continuous-ignored' warning.")
# P1-chain.motor-circuit-C1 (pack.motor-circuit): P_pk of a continuous-duty E1 against P_cont, the quantity the supply sees.
rk = cont("geared", 3.0, 700, 2.0, i=4, eta=0.9, J_L=0.01, J_motor=5e-4)
finding("P1-chain.motor-circuit-C1", f"continuous duty (ka-continuous-pump): P_pk {rk['P_pk']:.1f} W against P_cont {rk['P_cont']:.1f} W; "
        f"circulator CE1: P_pk {cont(**CONT_CASES[0][1])['P_pk']:.2f} W against P_cont {cont(**CONT_CASES[0][1])['P_cont']:.3f} W.")

# G1 (new at 2f53791c): P_rated is T_rated x omega_rated with no check against the running power or n_max.
r_lo = cont(**RATED_CASES[1][1])
r_hi = cont(**RATED_CASES[2][1])
finding("P1-elec.motor-sizing-G1", f"pack E1 load P_cont {r_lo['P_cont']:.0f} W. n_rated 100 rpm gives P_rated {r_lo['P_rated']:.0f} W "
        f"({100 * r_lo['P_rated'] / r_lo['P_cont']:.0f} % of the load); n_rated 5000 rpm (above n_max 3000) gives {r_hi['P_rated']:.0f} W. "
        "The product warns in neither case, and E4 is sized on P_rated.")


def main():
    print(f"{'case':28s} {'symbol':8s} {'KAT expected':>14s} {'independent':>14s} {'rel diff':>10s} {'KAT tol':>9s}  result")
    bad = 0
    for case, sym, e, v, rel, tol, ok, note in ROWS:
        if not ok:
            bad += 1
        print(f"{case:28s} {sym:8s} {e:14.6g} {v:14.6g} {rel:10.2e} {tol:9.1e}  {'PASS' if ok else 'FAIL'}  {note}")
    print(f"{len(ROWS) - bad}/{len(ROWS)} rows within tolerance")
    print("\nFindings evidence (informational):")
    for tag, text in FIND:
        print(f"  {tag}: {text}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
