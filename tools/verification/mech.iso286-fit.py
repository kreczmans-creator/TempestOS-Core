#!/usr/bin/env python3
"""Independent re-derivation of mech.iso286-fit (Track P, P1).

Source of the method: ISO 286-1 (2010) tolerance-unit and fundamental-deviation
formulae, written here from my own knowledge of the standard (no tables copied).
Units: D in mm, all deviations and IT values in micrometres.

  main size steps (lower, upper], D = sqrt(lower*upper), first step uses 1 and 3
  i = 0.45 D^(1/3) + 0.001 D   (D <= 500);   I = 0.004 D + 2.1   (D > 500)
  IT5..IT18 = 7,10,16,25,40,64,100,160,250,400,640,1000,1600,2500 times i
  IT01 = 0.3+0.008D, IT0 = 0.5+0.012D, IT1 = 0.8+0.02D; IT2..IT4 geometric between IT1 and IT5
  shaft: d -16D^.44, e -11D^.41, f -5.5D^.41, g -2.5D^.34, h 0, js +-IT/2,
         k +0.6 D^(1/3) (IT4..IT7 only), m IT7-IT6, n 5D^.34, p IT7 (+0..5 in the table),
         s IT7+0.4D (D>50), t IT7+0.63D, u IT7+D, r = sqrt(ei_p * ei_s)
  hole: A..H EI = -es;  K..N ES = -ei + delta (<= IT8);  P..U ES = -ei + delta (<= IT7);
        delta = ITn - IT(n-1)
  fit: c_max = ES - ei, c_min = EI - es, i_max = es - EI, i_min = ei - ES
"""
import math
import sys

STEPS = [3, 6, 10, 18, 30, 50, 80, 120, 180, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150]
MULT = {5: 7, 6: 10, 7: 16, 8: 25, 9: 40, 10: 64, 11: 100, 12: 160, 13: 250, 14: 400, 15: 640, 16: 1000, 17: 1600, 18: 2500}


def step_of(D):
    lo = 0.0
    for hi in STEPS:
        if D <= hi:
            return lo, hi
        lo = hi
    raise ValueError("D above 3150 mm")


def gmean(D):
    lo, hi = step_of(D)
    return math.sqrt(max(lo, 1.0) * hi)


def tol_unit(Dg):
    return 0.45 * Dg ** (1 / 3) + 0.001 * Dg if Dg <= 500 else 0.004 * Dg + 2.1


def IT(n, D):
    Dg = gmean(D)
    if Dg > 500:
        small = {1: 2.0, 2: 2.7, 3: 3.7, 4: 5.0}
        I = tol_unit(Dg)
        if n in small:
            return small[n] * I
    if n >= 5:
        return MULT[n] * tol_unit(Dg)
    it1 = 0.8 + 0.02 * Dg
    if n == 0:
        return 0.5 + 0.012 * Dg
    if n == -1:
        return 0.3 + 0.008 * Dg
    it5 = 7 * tol_unit(Dg)
    return it1 * (it5 / it1) ** ((n - 1) / 4.0)


def shaft_fd(letter, n, D):
    """Return (es, ei) in micrometres."""
    Dg = gmean(D)
    t = IT(n, D)
    if letter == "h":
        return 0.0, -t
    if letter == "js":
        return t / 2, -t / 2
    es_up = {"d": -16 * Dg ** 0.44, "e": -11 * Dg ** 0.41, "f": -5.5 * Dg ** 0.41, "g": -2.5 * Dg ** 0.34}
    if letter in es_up:
        return es_up[letter], es_up[letter] - t
    ei = ei_lower(letter, n, D)
    return ei + t, ei


def ei_lower(letter, n, D):
    Dg = gmean(D)
    if letter == "k":
        return 0.6 * Dg ** (1 / 3) if 4 <= n <= 7 else 0.0
    if letter == "m":
        return IT(7, D) - IT(6, D)
    if letter == "n":
        return 5 * Dg ** 0.34
    if letter == "p":
        return IT(7, D)
    if letter == "s":
        return IT(8, D) + 1 if D <= 50 else IT(7, D) + 0.4 * Dg
    if letter == "r":
        return math.sqrt(ei_lower("p", n, D) * ei_lower("s", n, D))
    if letter == "t":
        return IT(7, D) + 0.63 * Dg
    if letter == "u":
        return IT(7, D) + Dg
    raise KeyError(letter)


def hole_fd(letter, n, D):
    """Return (ES, EI) in micrometres."""
    t = IT(n, D)
    low = letter.lower()
    if letter == "JS":
        return t / 2, -t / 2
    if letter in "ABCDEFGH" and letter != "JS":
        es, _ = shaft_fd(low, n, D)
        return -es + t, -es
    delta = IT(n, D) - IT(n - 1, D)
    if letter in ("K", "M", "N"):
        ei = ei_lower(low, 5, D) if letter != "K" else 0.6 * gmean(D) ** (1 / 3)
        ES = (-ei + delta) if n <= 8 else (0.0 if letter in ("K", "N") else -ei)
    else:
        ei = ei_lower(low, n, D)
        ES = (-ei + delta) if n <= 7 else -ei
    return ES, ES - t


def fit(D, hl, hg, sl, sg):
    ES, EI = hole_fd(hl, hg, D)
    es, ei = shaft_fd(sl, sg, D)
    out = dict(ES=ES, EI=EI, es=es, ei=ei, IT_hole=IT(hg, D), IT_shaft=IT(sg, D))
    out["c_max"] = ES - ei
    out["c_min"] = EI - es
    out["i_max"] = es - EI
    out["i_min"] = ei - ES
    out["fit"] = "clearance" if out["c_min"] >= 0 else ("interference" if out["c_max"] <= 0 else "transition")
    return out


KATS = [
    ("ka-h7g6-25", fit(25, "H", 7, "g", 6), [("IT_hole", 21, 0.03), ("IT_shaft", 13, 0.03), ("es", -7, 0.06), ("c_min", 7, 0.06), ("c_max", 41, 0.03)]),
    ("ka-h7p6-50", fit(50, "H", 7, "p", 6), [("IT_hole", 25, 0.03), ("ei", 26, 0.06), ("i_max", 42, 0.05), ("i_min", 0, 1.5)]),
]

EXTRAS = []  # (label, symbol, product, independent, tolerance, is_finding)
