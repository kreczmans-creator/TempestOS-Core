#!/usr/bin/env python3
"""Independent re-derivation of mech.thick-cylinder (Track P, P1).

Method: Lame solution for a long, elastic, isotropic thick-walled cylinder.
Sources: Shigley's Mechanical Engineering Design, 9th ed., section 3-14;
Roark's Formulas for Stress and Strain, thick-walled cylinder cases (Table 13.5 in the cited edition);
Timoshenko, Theory of Elasticity, Lame problem. Units: mm, MPa.

  A = (a^2 p_i - b^2 p_o)/(b^2 - a^2)       B = a^2 b^2 (p_i - p_o)/(b^2 - a^2)
  sigma_r(r) = A - B/r^2     sigma_t(r) = A + B/r^2
  sigma_z = A  (closed ends, pressure end load over the wall area)  or 0 (open ends)
  von Mises from the three principal stresses; Tresca = max - min principal stress
  thin wall: (p_i - p_o)(a + b)/(2 (b - a))
  utilisation u = max(vM at a, vM at b)/sigma_allow
"""
import math
import sys


def lame(a, b, p_i, p_o, closed=True, sigma_allow=None):
    A = (a**2 * p_i - b**2 * p_o) / (b**2 - a**2)
    B = a**2 * b**2 * (p_i - p_o) / (b**2 - a**2)
    out = {"A": A, "B": B}
    sz = A if closed else 0.0
    out["sigma_z"] = sz
    for tag, r in (("a", a), ("b", b)):
        sr = A - B / r**2
        st = A + B / r**2
        out["sigma_r_" + tag] = sr
        out["sigma_t_" + tag] = st
        s = (st, sr, sz)
        out["sigma_vm_" + tag] = math.sqrt(0.5 * ((st - sr)**2 + (sr - sz)**2 + (sz - st)**2))
        out["sigma_tr_" + tag] = max(s) - min(s)
    out["sigma_vm_max"] = max(out["sigma_vm_a"], out["sigma_vm_b"])
    out["sigma_thin"] = (p_i - p_o) * (a + b) / (2 * (b - a))
    if sigma_allow:
        out["u"] = out["sigma_vm_max"] / sigma_allow
    return out


KATS = [
    ("ka-internal-closed", lame(50, 100, 50, 0, True, 200),
     [("sigma_t_a", 83.333, 1e-4), ("sigma_r_a", -50, 1e-6), ("sigma_t_b", 33.333, 1e-4), ("sigma_z", 16.667, 1e-4),
      ("sigma_vm_a", 115.47, 1e-4), ("sigma_tr_a", 133.33, 1e-4), ("u", 0.57735, 1e-4), ("sigma_thin", 75, 1e-6)]),
    ("ka-external-closed", lame(40, 60, 0, 20, True, 100),
     [("sigma_t_a", -72, 1e-6), ("sigma_t_b", -52, 1e-6), ("sigma_z", -36, 1e-6),
      ("sigma_vm_a", 62.354, 1e-4), ("sigma_vm_b", 27.713, 1e-4), ("u", 0.62354, 1e-4)]),
    ("ka-internal-open", lame(50, 100, 50, 0, False, 200),
     [("sigma_vm_a", 116.67, 1e-4), ("sigma_vm_b", 33.333, 1e-4), ("u", 0.58333, 1e-4)]),
]

EXTRAS = []
