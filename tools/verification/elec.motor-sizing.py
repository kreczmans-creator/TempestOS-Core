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
    o.update(k=k, T_load=T_load, J_refl=J_refl, J_total=J_total, w_pk=w_pk, n_pk=n_pk, a_pk=a_pk,
             T_acc=T_acc, T_pk=T_pk, T_dec=T_dec, T_rms=T_rms, J_ratio=(J_refl + J_mech) / J_motor,
             t_acc=t_acc, theta_m=theta_m)
    if mech in ("screw", "belt", "rack"):
        o["v_pk"] = w_pk * k
    return o


# ---------------- Known-answer cases ----------------
r = motor("screw", 0.5, 0.5, 0.5, 1 / 3, "trap", i=1, eta=0.9, p_mm=10, J_mech=1e-4, m=50, mu=0.1,
          theta_deg=0, F_p=0, J_motor=2e-4)
for sym, e, t in [("v_pk", 1.5, 1e-9), ("n_pk", 9000, 1e-9), ("J_refl", 1.4072e-4, 1e-4),
                  ("T_acc", 2.4923, 1e-4), ("T_load", 0.08670, 1e-3), ("T_pk", 2.579, 1e-3),
                  ("T_rms", 1.4402, 1e-3), ("J_ratio", 1.2036, 1e-3)]:
    check("ka-ball-screw", sym, e, r[sym], t)

r = motor("geared", 0.25, 0.3, 0.7, 1 / 3, "trap", i=10, eta=0.85, J_mech=0, J_L=0.5, T_L=0, J_motor=5e-4)
for sym, e, t in [("n_pk", 750, 1e-6), ("J_refl", 5.882e-3, 1e-3), ("T_pk", 5.0126, 1e-3),
                  ("T_rms", 2.2417, 1e-3), ("J_ratio", 11.76, 2e-3)]:
    check("ka-geared-index-table", sym, e, r[sym], t)

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
