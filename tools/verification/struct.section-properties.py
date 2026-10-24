#!/usr/bin/env python3
"""Independent re-derivation of struct.section-properties (Track P, P1).

Method (own code, not a translation of the C#):
  * Each shape is built as signed polygon loops; arcs use 4000 chords per quarter circle.
  * Area and moments by the shoelace/Green integrals, moved to the centroid by the parallel-axis theorem.
  * Principal values from the Mohr circle.
  * Plastic modulus: bisect the equal-area axis, clip every loop with a half-plane, sum |first moments|.
  * Torsion: exact series (solid rectangle), Bredt + t^3 term (closed thin wall),
    sum b t^3 / 3 plus El Darwish-Johnston fillet term (rolled I), pi d^4/32 (round).
  * Warping: I_w = I_minor (h - t_f)^2 / 4.
Units inside: mm. Exit 0 when every row passes max(KAT tol, 1e-6).
"""
import math
import sys

import numpy as np

NARC = 4000  # chords per quarter circle


# ---------------------------------------------------------------- geometry
def arc(cx, cy, r, a0, a1, n=NARC):
    """Points on a circular arc from angle a0 to a1 (degrees), inclusive."""
    if r <= 0:
        return [(cx, cy)]
    t = np.radians(np.linspace(a0, a1, n + 1))
    return list(zip(cx + r * np.cos(t), cy + r * np.sin(t)))


def loop_moments(pts):
    """Return A, Sx, Sy, Ixx, Iyy, Ixy about the origin for one closed loop (signed by orientation)."""
    p = np.asarray(pts, float)
    x0, y0 = p[:, 0], p[:, 1]
    x1, y1 = np.roll(x0, -1), np.roll(y0, -1)
    c = x0 * y1 - x1 * y0
    A = c.sum() / 2
    Sx = ((y0 + y1) * c).sum() / 6          # integral of y dA
    Sy = ((x0 + x1) * c).sum() / 6          # integral of x dA
    Ixx = ((y0 ** 2 + y0 * y1 + y1 ** 2) * c).sum() / 12
    Iyy = ((x0 ** 2 + x0 * x1 + x1 ** 2) * c).sum() / 12
    Ixy = ((x0 * y1 + 2 * x0 * y0 + 2 * x1 * y1 + x1 * y0) * c).sum() / 24
    return np.array([A, Sx, Sy, Ixx, Iyy, Ixy])


def ccw(pts):
    return loop_moments(pts)[0] > 0


def orient(pts, positive=True):
    return list(pts) if ccw(pts) == positive else list(pts)[::-1]


def section(loops):
    """loops: list of (points, sign). Returns property dict about the centroid."""
    m = np.zeros(6)
    for pts, sign in loops:
        lm = loop_moments(orient(pts, True))
        m += sign * lm
    A, Sx, Sy, Ixx0, Iyy0, Ixy0 = m
    xc, yc = Sy / A, Sx / A
    Ixx = Ixx0 - A * yc ** 2
    Iyy = Iyy0 - A * xc ** 2
    Ixy = Ixy0 - A * xc * yc
    avg, diff = (Ixx + Iyy) / 2, math.hypot((Ixx - Iyy) / 2, Ixy)
    I1, I2 = avg + diff, avg - diff
    theta = 0.5 * math.atan2(-2 * Ixy, Ixx - Iyy)
    allpts = np.vstack([np.asarray(p, float) for p, s in loops if s > 0])
    ymax = max(allpts[:, 1].max() - yc, yc - allpts[:, 1].min())
    xmax = max(allpts[:, 0].max() - xc, xc - allpts[:, 0].min())
    return dict(A=A, xc=xc, yc=yc, Ixx=Ixx, Iyy=Iyy, Ixy=Ixy, I1=I1, I2=I2, theta=theta,
                Wel_x=Ixx / ymax, Wel_y=Iyy / xmax, ix=math.sqrt(Ixx / A), iy=math.sqrt(Iyy / A),
                loops=loops)


def clip(pts, axis, level, keep_above):
    """Sutherland-Hodgman clip of a loop against y>=level (axis 'y') or x>=level ('x')."""
    k = 1 if axis == "y" else 0
    sgn = 1 if keep_above else -1
    out = []
    n = len(pts)
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        da, db = sgn * (a[k] - level), sgn * (b[k] - level)
        if da >= 0:
            out.append(a)
        if (da > 0 > db) or (da < 0 < db):
            t = da / (da - db)
            out.append((a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])))
    return out


def half_area(loops, axis, level, above):
    a = 0.0
    for pts, s in loops:
        c = clip(orient(pts, True), axis, level, above)
        if len(c) >= 3:
            a += s * loop_moments(c)[0]
    return a


def plastic_modulus(sec, axis):
    loops = sec["loops"]
    allp = np.vstack([np.asarray(p) for p, s in loops])
    k = 1 if axis == "y" else 0
    lo, hi = allp[:, k].min(), allp[:, k].max()
    total = sec["A"]
    for _ in range(80):
        mid = (lo + hi) / 2
        if half_area(loops, axis, mid, True) > total / 2:
            lo = mid
        else:
            hi = mid
    lvl = (lo + hi) / 2
    w = 0.0
    for above in (True, False):
        for pts, s in loops:
            c = clip(orient(pts, True), axis, lvl, above)
            if len(c) >= 3:
                lm = loop_moments(c)
                first = lm[1] if axis == "y" else lm[2]
                a = lm[0]
                cen = first / a if a else 0
                w += s * abs(a * (cen - lvl))
    return w


# ---------------------------------------------------------------- shapes (centred on the bounding box)
def rect_loop(b, h, cx=0.0, cy=0.0):
    return [(cx - b / 2, cy - h / 2), (cx + b / 2, cy - h / 2), (cx + b / 2, cy + h / 2), (cx - b / 2, cy + h / 2)]


def rounded_rect(b, h, r):
    if r <= 0:
        return rect_loop(b, h)
    pts = []
    pts += arc(b / 2 - r, h / 2 - r, r, 0, 90)
    pts += arc(-b / 2 + r, h / 2 - r, r, 90, 180)
    pts += arc(-b / 2 + r, -h / 2 + r, r, 180, 270)
    pts += arc(b / 2 - r, -h / 2 + r, r, 270, 360)
    return pts


def circle_loop(d):
    return arc(0, 0, d / 2, 0, 360, 4 * NARC)[:-1]


def sec_rect(b, h):
    return section([(rect_loop(b, h), 1)])


def sec_circle(d):
    return section([(circle_loop(d), 1)])


def sec_tube(d, t):
    return section([(circle_loop(d), 1), (circle_loop(d - 2 * t), -1)])


def sec_hollow_rect(h, b, t, ro=None, ri=None):
    ro = 1.5 * t if ro is None else ro
    ri = 1.0 * t if ri is None else ri
    return section([(rounded_rect(b, h, ro), 1), (rounded_rect(b - 2 * t, h - 2 * t, ri), -1)])


def sec_I(h, b, tw, tf, r):
    """Doubly symmetric I with quarter-circle root fillets, x-x horizontal."""
    pts = []
    yw = h / 2 - tf
    # right half going up, then mirror (counter-clockwise starting at the bottom centre-right)
    pts += [(tw / 2, -yw + 0.0)]
    # lower right fillet: web face up to lower flange top: centre (tw/2 + r, -yw - r)
    seg_lr = arc(tw / 2 + r, -yw - r, r, 180, 90) if r > 0 else [(tw / 2, -yw)]
    # build polygon explicitly, counter-clockwise
    poly = []
    poly += [(-b / 2, -h / 2), (b / 2, -h / 2), (b / 2, -yw)]
    poly += (arc(tw / 2 + r, -yw - r, r, 90, 180)[::-1] if r > 0 else [(tw / 2, -yw)])  # flange top to web
    # ^ arc from flange-top point (tw/2+r, -yw) at 90 deg to web face (tw/2, -yw-r)? corrected below
    return None


def sec_I_poly(h, b, tw, tf, r):
    """Doubly symmetric I-section with root fillets. Counter-clockwise outline."""
    yw = h / 2 - tf                     # inner face of flange (positive y)
    poly = [(-b / 2, -h / 2), (b / 2, -h / 2), (b / 2, -yw)]
    # lower-right fillet, centre (tw/2 + r, -yw - r): runs from (tw/2 + r, -yw) to (tw/2, -yw - r)
    if r > 0:
        poly += arc(tw / 2 + r, -yw - r, r, 90, 180)
    else:
        poly += [(tw / 2, -yw)]
    # web right face up to upper fillet; centre (tw/2 + r, yw + r): from (tw/2, yw + r) to (tw/2 + r, yw)
    if r > 0:
        poly += arc(tw / 2 + r, yw + r, r, 180, 270)
    else:
        poly += [(tw / 2, yw)]
    poly += [(b / 2, yw), (b / 2, h / 2), (-b / 2, h / 2), (-b / 2, yw)]
    if r > 0:
        poly += arc(-tw / 2 - r, yw + r, r, 270, 360)       # from (-tw/2 - r, yw) to (-tw/2, yw + r)
        poly += arc(-tw / 2 - r, -yw - r, r, 0, 90)         # from (-tw/2, -yw - r) to (-tw/2 - r, -yw)
    else:
        poly += [(-tw / 2, yw), (-tw / 2, -yw)]
    return poly


def sec_I(h, b, tw, tf, r):
    return section([(sec_I_poly(h, b, tw, tf, r), 1)])


def sec_polygon(pts):
    return section([(pts, 1)])


# ---------------------------------------------------------------- torsion
def j_rect_exact(b, h):
    """Saint-Venant series, J = (1/3) a b^3 [1 - 192/pi^5 (b/a) sum tanh(n pi a / 2b)/n^5], b = short side."""
    a, bb = max(b, h), min(b, h)
    s = sum(math.tanh(n * math.pi * a / (2 * bb)) / n ** 5 for n in range(1, 200, 2))
    return a * bb ** 3 / 3 * (1 - 192 / math.pi ** 5 * (bb / a) * s)


def j_rect_roark(b, h):
    """Roark 8th ed. Table 10.1 case 4, a and b half-sides (a >= b)."""
    a, bb = max(b, h) / 2, min(b, h) / 2
    return a * bb ** 3 * (16 / 3 - 3.36 * bb / a * (1 - bb ** 4 / (12 * a ** 4)))


def j_bredt_rounded_box(h, b, t, ro):
    """Closed thin wall on the mid line: 4 Am^2 t / pm + pm t^3 / 3 (corner radius ro outer)."""
    rc = max(ro - t / 2, 0.0)
    Am = (b - t) * (h - t) - (4 - math.pi) * rc ** 2
    pm = 2 * ((b - t) + (h - t)) - 8 * rc + 2 * math.pi * rc
    return 4 * Am ** 2 * t / pm + pm * t ** 3 / 3


def j_I_rolled(h, b, tw, tf, r):
    """Sum b t^3 / 3 plus the El Darwish-Johnston fillet term (AISC Design Guide 9 form)."""
    base = (2 * b * tf ** 3 + (h - 2 * tf) * tw ** 3) / 3
    if r <= 0:
        return base
    alpha = (tw / tf) * (0.15 + 0.1 * r / tf)
    D = ((tf + r) ** 2 + tw * (r + tw / 4)) / (2 * r + tf)
    return base + 2 * alpha * D ** 4 - 0.420 * tf ** 4


def iw_I(sec_Iy, h, tf):
    return sec_Iy * (h - tf) ** 2 / 4


# ---------------------------------------------------------------- reporting
ROWS = []
FAIL = [0]


def row(case, sym, kat, ind, tol, note=""):
    floor = max(tol, 1e-6)
    rel = abs(ind - kat) / abs(kat) if kat != 0 else abs(ind - kat)
    ok = rel <= floor
    if not ok:
        FAIL[0] += 1
    print(f"{case:<26}{sym:<10}{kat:>14.6g}{ind:>14.6g}{rel:>11.2e}{tol:>10.1e}  {'PASS' if ok else 'FAIL'} {note}")


CM2, CM3, CM4, CM = 1e2, 1e3, 1e4, 10.0


def kats():
    print(f"{'case':<26}{'symbol':<10}{'KAT':>14}{'indep':>14}{'rel.diff':>11}{'tol':>10}  result")
    # 1. UB 457x191x98
    s = sec_I(467.2, 192.8, 11.4, 19.6, 10.2)
    Wy = plastic_modulus(s, "y")
    Wx = plastic_modulus(s, "x")
    c = "ka-ub-457x191x98"
    J = j_I_rolled(467.2, 192.8, 11.4, 19.6, 10.2)
    Iw = iw_I(s["Iyy"], 467.2, 19.6) / 1e12
    for sym, kat, ind, tol in [("A", 125, s["A"] / CM2, 0.005), ("I_xx", 45730, s["Ixx"] / CM4, 0.005),
                               ("I_yy", 2347, s["Iyy"] / CM4, 0.005), ("i_x", 19.1, s["ix"] / CM, 0.005),
                               ("i_y", 4.33, s["iy"] / CM, 0.005), ("W_el_x", 1957, s["Wel_x"] / CM3, 0.005),
                               ("W_el_y", 243, s["Wel_y"] / CM3, 0.005), ("W_pl_x", 2232, Wy / CM3, 0.005),
                               ("W_pl_y", 379, Wx / CM3, 0.005), ("J", 121, J / CM4, 0.01), ("I_w", 1.18, Iw, 0.01)]:
        row(c, sym, kat, ind, tol)
    # 2. SHS 100x100x5
    s = sec_hollow_rect(100, 100, 5)
    c = "ka-shs-100x100x5"
    for sym, kat, ind, tol in [("A", 18.7, s["A"] / CM2, 0.005), ("I_xx", 279, s["Ixx"] / CM4, 0.005),
                               ("W_pl_x", 66.4, plastic_modulus(s, "y") / CM3, 0.005),
                               ("J", 439, j_bredt_rounded_box(100, 100, 5, 7.5) / CM4, 0.01)]:
        row(c, sym, kat, ind, tol)
    # 3. rectangle 100 x 200 (b = 100, h = 200)
    s = sec_rect(100, 200)
    c = "ka-rectangle"
    for sym, kat, ind, tol in [("A", 200, s["A"] / CM2, 1e-9), ("I_xx", 6666.67, s["Ixx"] / CM4, 1e-5),
                               ("I_yy", 1666.67, s["Iyy"] / CM4, 1e-5), ("W_el_x", 666.667, s["Wel_x"] / CM3, 1e-5),
                               ("W_pl_x", 1000, plastic_modulus(s, "y") / CM3, 1e-6),
                               ("W_pl_y", 500, plastic_modulus(s, "x") / CM3, 1e-6),
                               ("i_x", 5.7735, s["ix"] / CM, 1e-4), ("J", 4580, j_rect_exact(100, 200) / CM4, 0.002)]:
        row(c, sym, kat, ind, tol)
    # 4. tube 200 x 10
    s = sec_tube(200, 10)
    c = "ka-tube"
    Jt = math.pi * (200 ** 4 - 180 ** 4) / 32
    for sym, kat, ind, tol in [("A", 59.690, s["A"] / CM2, 1e-4), ("I_xx", 2701.1, s["Ixx"] / CM4, 1e-4),
                               ("W_el_x", 270.11, s["Wel_x"] / CM3, 1e-4),
                               ("W_pl_x", 361.33, plastic_modulus(s, "y") / CM3, 1e-4), ("J", 5402.2, Jt / CM4, 1e-4)]:
        row(c, sym, kat, ind, tol)
    # 5. unequal angle polygon
    s = sec_polygon([(0, 0), (90, 0), (90, 12), (12, 12), (12, 150), (0, 150)])
    c = "ka-angle-polygon"
    for sym, kat, ind, tol in [("A", 27.36, s["A"] / CM2, 1e-6), ("x_c", 21.39474, s["xc"], 1e-5),
                               ("y_c", 51.39474, s["yc"], 1e-5), ("I_xx", 631.80, s["Ixx"] / CM4, 2e-4),
                               ("I_yy", 174.31, s["Iyy"] / CM4, 2e-4), ("I_xy", -191.20, s["Ixy"] / CM4, 2e-4)]:
        row(c, sym, kat, ind, tol)


# ---------------------------------------------------------------- extra checks (filled after probing)
def extras():
    pass


if __name__ == "__main__":
    kats()
    extras()
    print("RESULT:", "ALL PASS" if FAIL[0] == 0 else f"{FAIL[0]} FAIL")
    sys.exit(0 if FAIL[0] == 0 else 1)
