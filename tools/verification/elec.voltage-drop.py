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
        dV = mVAm * I * L / 1000.0
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
EXTRA = []  # filled below after the product was probed


def extra(name, symbol, product, independent, tol=1e-6, note=""):
    check("extra:" + name, symbol, product, independent, tol, note)


if __name__ == "__main__":
    pass
