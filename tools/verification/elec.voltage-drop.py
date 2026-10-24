#!/usr/bin/env python3
"""Independent re-derivation of elec.voltage-drop (Track P, P1).

Method (written without reading the C# Calculate method):
  rho_T = rho_20 * (1 + alpha * (T_c - 20))            (linear temperature coefficient)
  R = rho_T * L / (A * n)    or   R = R'_km * L / (1000 n)
  X = X'_km * L / (1000 n)
  DC:           dV = 2 I R
  single-phase: dV = 2 I (R cos(phi) + X sin(phi))
  three-phase:  dV = sqrt(3) I (R cos(phi) + X sin(phi))
  mV/A/m route: dV = mVAm * I * L / 1000
  loss: 2 I^2 R (DC and 1-phase), 3 I^2 R (3-phase)
  conductor temperature estimate: T_amb + (T_rated - T_amb) (I/I_z)^2
Reference: BS 7671 (18th ed.) Appendix 4 section 6 (voltage drop, mV/A/m and temperature
correction); Hughes, Electrical and Electronic Technology (resistivity, temperature coefficient).
"""
import math
import sys

ROWS = []


def check(case, symbol, expected, value, tol, note=""):
    tol_eff = max(tol, 1e-6)
    rel = abs(value - expected) / abs(expected) if expected != 0 else abs(value - expected)
    ok = rel <= tol_eff
    ROWS.append((case, symbol, expected, value, rel, tol, ok, note))


def vdrop(system, V_n, I, L, cos_phi=1.0, n_par=1, data="res", rho_20=0.01724, alpha=0.00393,
          A_c=None, T_c=70.0, R_km=None, X_km=0.08, mVAm=None, I_z=None, T_rated=70.0, T_amb=30.0):
    out = {}
    if data == "mvam":
        dV = mVAm * I * L / 1000.0 / n_par  # each of n parallel conductors carries I / n
        out.update(dV=dV, dV_pct=100 * dV / V_n)
        return out
    if data == "res":
        rho_T = rho_20 * (1 + alpha * (T_c - 20.0))
        R = rho_T * L / (A_c * n_par)
    else:
        R = R_km * L / 1000.0 / n_par
    X = X_km * L / 1000.0 / n_par
    sin_phi = math.sqrt(max(0.0, 1 - cos_phi ** 2))
    if system == "dc":
        dV = 2 * I * R
        loss = 2 * I ** 2 * R
    elif system == "1ph":
        dV = 2 * I * (R * cos_phi + X * sin_phi)
        loss = 2 * I ** 2 * R
    else:
        dV = math.sqrt(3) * I * (R * cos_phi + X * sin_phi)
        loss = 3 * I ** 2 * R
    out.update(R=R, X=X, dV=dV, dV_pct=100 * dV / V_n, P_loss=loss)
    if I_z:
        out["T_c_est"] = T_amb + (T_rated - T_amb) * (I / I_z) ** 2
    return out


# ---------------- Known-answer cases (inputs copied from the KAT list) ----------------
r = vdrop("dc", 48, 20, 30, n_par=1, data="res", rho_20=0.01724, alpha=0.00393, A_c=6, T_c=70, X_km=0)
for sym, e, t in [("R", 0.10314, 1e-4), ("dV", 4.1256, 1e-4), ("dV_pct", 8.595, 1e-3), ("P_loss", 82.51, 1e-3)]:
    check("ka-dc", sym, e, r[sym], t)

r = vdrop("3ph", 400, 100, 80, cos_phi=0.85, n_par=1, data="res", rho_20=0.01724, alpha=0.00393,
          A_c=35, T_c=70, X_km=0.08, I_z=119, T_rated=70, T_amb=30)
for sym, e, t in [("R", 0.047150, 1e-4), ("dV", 7.526, 1e-3), ("dV_pct", 1.8815, 1e-3),
                  ("P_loss", 1414.5, 1e-3), ("T_c_est", 58.25, 1e-3)]:
    check("ka-three-phase", sym, e, r[sym], t)

r = vdrop("3ph", 400, 63, 50, data="mvam", mVAm=2.4)
check("ka-mvam", "dV", 7.56, r["dV"], 1e-9)
check("ka-mvam", "dV_pct", 1.89, r["dV_pct"], 1e-9)

# ---------------- Extra checks (product values from the probe tool, hard-coded) ----------------
# Product values below were read from `Probe run elec.voltage-drop` (10 significant figures).
EXTRA_CASES = [
    ('V1 single-phase 230 V, 32 A, 25 m, 4 mm2, cos 0.9',
     {'system': '1ph', 'V_n': 230, 'I': 32, 'L': 25, 'cos_phi': 0.9, 'A_c': 4, 'T_c': 70, 'X_km': 0.08},
     {'R': 0.128922875, 'X': 0.002, 'dV': 7.481751506, 'dV_pct': 3.252935438, 'P_loss': 264.034048}),
    ('V2 three-phase R and X per km, 2 parallel',
     {'system': '3ph', 'V_n': 400, 'I': 63, 'L': 50, 'cos_phi': 0.8, 'n_par': 2, 'data': 'perkm', 'R_km': 0.63, 'X_km': 0.095},
     {'R': 0.01575, 'X': 0.002375, 'dV': 1.530396792, 'dV_pct': 0.3825991981, 'P_loss': 187.53525}),
    ('V3 single-phase mV/A/m 1.5, 2 parallel',
     {'system': '1ph', 'V_n': 230, 'I': 60, 'L': 40, 'n_par': 2, 'data': 'mvam', 'mVAm': 1.5},
     {'dV': 1.8, 'dV_pct': 0.7826086957}),
    ('V4 tiny: 1 uA, 1 mm, 1e5 mm2',
     {'system': '3ph', 'V_n': 0.1, 'I': 1e-06, 'L': 0.001, 'cos_phi': 1, 'A_c': 100000.0, 'T_c': 70, 'X_km': 0},
     {'R': 2.062766e-10, 'X': 0.0, 'dV': 3.572815516e-16, 'dV_pct': 3.572815516e-13, 'P_loss': 6.188298e-22}),
    ('V5 huge: 1e6 A, 1e6 m, 20 parallel',
     {'system': '3ph', 'V_n': 1000000.0, 'I': 1000000.0, 'L': 1000000.0, 'cos_phi': 0.5, 'n_par': 20, 'A_c': 1000, 'T_c': 70, 'X_km': 0.1},
     {'R': 1.031383, 'X': 5.0, 'dV': 8393203.879, 'dV_pct': 839.3203879, 'P_loss': 3094149000000.0}),
    ('V6 DC 24 V, 2.5 mm2, T_c -40 degC',
     {'system': 'dc', 'V_n': 24, 'I': 10, 'L': 12, 'A_c': 2.5, 'T_c': -40, 'X_km': 0},
     {'R': 0.0632390784, 'X': 0.0, 'dV': 1.264781568, 'dV_pct': 5.2699232, 'P_loss': 12.64781568}),
    ('V7 power factor 0.01',
     {'system': '3ph', 'V_n': 400, 'I': 100, 'L': 80, 'cos_phi': 0.01, 'A_c': 35, 'T_c': 70, 'X_km': 0.08},
     {'R': 0.04714893714, 'X': 0.0064, 'dV': 1.190121444, 'dV_pct': 0.2975303611, 'P_loss': 1414.468114}),
    ('V8 overload 150 A on I_z 100 A, T_amb 40',
     {'system': '3ph', 'V_n': 400, 'I': 150, 'L': 80, 'cos_phi': 0.85, 'A_c': 35, 'T_c': 70, 'X_km': 0.08, 'I_z': 100, 'T_rated': 70, 'T_amb': 40},
     {'R': 0.04714893714, 'X': 0.0064, 'dV': 11.28812302, 'dV_pct': 2.822030756, 'P_loss': 3182.553257, 'T_c_est': 107.5}),
    ('V9 aluminium 95 mm2, 90 degC',
     {'system': '3ph', 'V_n': 400, 'I': 200, 'L': 120, 'cos_phi': 0.9, 'rho_20': 0.0282, 'alpha': 0.00403, 'A_c': 95, 'T_c': 90, 'X_km': 0.08, 'I_z': 215, 'T_rated': 90, 'T_amb': 30},
     {'R': 0.04566975158, 'X': 0.0096, 'dV': 15.68798763, 'dV_pct': 3.921996908, 'P_loss': 5480.370189, 'T_c_est': 81.9199567}),
    ('V12 single-phase R and X per km with I_z',
     {'system': '1ph', 'V_n': 230, 'I': 20, 'L': 30, 'cos_phi': 0.95, 'data': 'perkm', 'R_km': 4.61, 'X_km': 0.1, 'I_z': 32},
     {'R': 0.1383, 'X': 0.003, 'dV': 5.292869988, 'dV_pct': 2.301247821, 'P_loss': 110.64, 'T_c_est': 45.625}),
]
for name, kw, prod in EXTRA_CASES:
    r = vdrop(**kw)
    for sym, pv in prod.items():
        check("extra " + name.split()[0], sym, pv, r[sym], 1e-8, "product value hard-coded")

# ---------------- Findings evidence (informational, does not change the exit code) ----------------
FIND = []


def finding(tag, text):
    FIND.append((tag, text))


# F01: default copper resistivity against BS EN 60228 class 2 maximum resistance at 20 degC
# (values from memory, ohm/km: 1.5 -> 12.1, 2.5 -> 7.41, 6 -> 3.08, 16 -> 1.15, 35 -> 0.524, 50 -> 0.387, 95 -> 0.193;
# check against the owner's copy of the standard).
R60228 = {1.5: 12.1, 2.5: 7.41, 6: 3.08, 16: 1.15, 35: 0.524, 50: 0.387, 95: 0.193}
txt = ", ".join(f"{a} mm2: {100 * (0.01724 * 1000 / a / r - 1):+.1f} %" for a, r in R60228.items())
finding("P1-elec.voltage-drop-01", "Product R (rho 0.01724) against class 2 maximum R at 20 degC: " + txt)
t = vdrop("3ph", 400, 1, 1000, 1.0, data="res", A_c=35, T_c=70, X_km=0)
finding("P1-elec.voltage-drop-01", f"35 mm2 three-phase at 70 degC, cos phi 1, X 0: {t['dV'] * 1000:.4f} mV/A/m (product 1.0208) "
        "against about 1.1 mV/A/m in the BS 7671 Appendix 4 table for 35 mm2 copper (value from memory).")
# F02: blank A_c and rho_20 are replaced by 35 mm2 and copper without a notice (probe: run without A_c).
finding("P1-elec.voltage-drop-02", "Resistivity route with A_c and rho_20 left blank gives the KAT three-phase answer (R 0.04715 ohm) with no notice.")
# F03: mV/A/m route ignores I_z.
finding("P1-elec.voltage-drop-03", "V3 variant with I = 60 A, I_z = 50 A on the mV/A/m route: no overload check, no T_c_est, no warning.")


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
