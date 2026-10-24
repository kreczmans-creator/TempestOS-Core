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

NARC = 4000


# ---------------------------------------------------------------- geometry
def arc(cx, cy, r, a0, a1, n=None):
    """Points on a circular arc from angle a0 to a1 (degrees), inclusive."""
    n = NARC if n is None else n
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


def sec_I_poly(h, b, tw, tf, r):
    """Doubly symmetric I-section with root fillets in the corners web/flange. Counter-clockwise outline."""
    yw = h / 2 - tf                     # inner face of the flange (positive y)
    poly = [(-b / 2, -h / 2), (b / 2, -h / 2), (b / 2, -yw)]
    if r > 0:
        poly += arc(tw / 2 + r, -yw + r, r, 270, 180)     # lower right fillet: flange top to web face
        poly += arc(tw / 2 + r, yw - r, r, 180, 90)       # upper right fillet: web face to flange underside
    else:
        poly += [(tw / 2, -yw), (tw / 2, yw)]
    poly += [(b / 2, yw), (b / 2, h / 2), (-b / 2, h / 2), (-b / 2, yw)]
    if r > 0:
        poly += arc(-tw / 2 - r, yw - r, r, 90, 0)        # upper left fillet
        poly += arc(-tw / 2 - r, -yw + r, r, 0, -90)      # lower left fillet
    else:
        poly += [(-tw / 2, yw), (-tw / 2, -yw)]
    poly += [(-b / 2, -yw)]
    return poly


def sec_I_poly_coarse(h, b, tw, tf, r):
    """Same outline with 16 chords per fillet, for the finite-element mesh."""
    global NARC
    keep, NARC = NARC, 16
    try:
        return sec_I_poly(h, b, tw, tf, r)
    finally:
        NARC = keep


def sec_I(h, b, tw, tf, r):
    return section([(sec_I_poly(h, b, tw, tf, r), 1)])



def channel_poly(h, b, tw, tf, r):
    """Channel with the web on the left (x = 0 .. tw), heel at the origin, root fillets r."""
    pts = [(0, 0), (b, 0), (b, tf)]
    pts += arc(tw + r, tf + r, r, 270, 180) if r > 0 else [(tw, tf)]
    pts += arc(tw + r, h - tf - r, r, 180, 90) if r > 0 else [(tw, h - tf)]
    pts += [(b, h - tf), (b, h), (0, h)]
    return pts


def tee_poly(h, b, tw, tf, r):
    """Tee with the flange on top (y = h - tf .. h), stem down to y = 0, centred on x = 0."""
    pts = [(-tw / 2, 0), (tw / 2, 0)]
    pts += arc(tw / 2 + r, h - tf - r, r, 180, 90) if r > 0 else [(tw / 2, h - tf)]
    pts += [(b / 2, h - tf), (b / 2, h), (-b / 2, h), (-b / 2, h - tf)]
    pts += arc(-tw / 2 - r, h - tf - r, r, 90, 0) if r > 0 else [(-tw / 2, h - tf)]
    return pts


def angle_poly(h, b, t, r):
    """Angle: vertical leg h along x = 0 .. t, horizontal leg b along y = 0 .. t, heel at the origin, root fillet r."""
    pts = [(0, 0), (b, 0), (b, t)]
    pts += arc(t + r, t + r, r, 270, 180) if r > 0 else [(t, t)]
    pts += [(t, h), (0, h)]
    return pts


def coarse(fn, *a):
    """Build an outline with 16 chords per fillet quadrant, for the finite-element mesh."""
    global NARC
    keep, NARC = NARC, 16
    try:
        return fn(*a)
    finally:
        NARC = keep


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



# ---------------------------------------------------------------- finite-element torsion (simply connected sections)
def _inside(P, loops):
    """Even-odd point test; holes (sign -1) subtract."""
    res = np.zeros(len(P), int)
    for pts, sign in loops:
        v = np.asarray(pts, float)
        x, y = P[:, 0], P[:, 1]
        ins = np.zeros(len(P), bool)
        for i in range(len(v)):
            x1, y1 = v[i]
            x2, y2 = v[(i + 1) % len(v)]
            if y1 == y2:
                continue
            ins ^= ((y1 > y) != (y2 > y)) & (x < (x2 - x1) * (y - y1) / (y2 - y1) + x1)
        res += sign * ins
    return res > 0


def torsion_fe_once(loops, h):
    """Saint-Venant J = 2 * integral(phi) with the Prandtl stress function, linear triangles, phi = 0 on the boundary."""
    from scipy.spatial import Delaunay, cKDTree
    from scipy.sparse import coo_matrix
    from scipy.sparse.linalg import spsolve
    bpts = []
    for L, _ in loops:
        L = np.asarray(L, float)
        for i in range(len(L)):
            a, b = L[i], L[(i + 1) % len(L)]
            k = max(1, int(math.ceil(math.hypot(*(b - a)) / h)))
            bpts += [a + (b - a) * j / k for j in range(k)]
    bp = np.array(bpts)
    allp = np.vstack([np.asarray(L, float) for L, _ in loops])
    xs = np.arange(allp[:, 0].min() + h / 2, allp[:, 0].max(), h)
    ys = np.arange(allp[:, 1].min() + h / 2, allp[:, 1].max(), h)
    G = np.array([(x, y) for x in xs for y in ys])
    d, _ = cKDTree(bp).query(G)
    G = G[_inside(G, loops) & (d > 0.45 * h)]
    P = np.vstack([bp, G])
    tri = Delaunay(P).simplices
    tri = tri[_inside(P[tri].mean(axis=1), loops)]
    n, nb = len(P), len(bp)
    x, y = P[tri, 0], P[tri, 1]
    bq = np.stack([y[:, 1] - y[:, 2], y[:, 2] - y[:, 0], y[:, 0] - y[:, 1]], 1)
    cq = np.stack([x[:, 2] - x[:, 1], x[:, 0] - x[:, 2], x[:, 1] - x[:, 0]], 1)
    area = 0.5 * np.abs((bq * x).sum(1))
    good = area > 1e-12
    tri, bq, cq, area = tri[good], bq[good], cq[good], area[good]
    Ke = (bq[:, :, None] * bq[:, None, :] + cq[:, :, None] * cq[:, None, :]) / (4 * area[:, None, None])
    rows = np.repeat(tri, 3, axis=1).ravel()
    cols = np.tile(tri, (1, 3)).ravel()
    K = coo_matrix((Ke.ravel(), (rows, cols)), shape=(n, n)).tocsr()
    f = np.zeros(n)
    np.add.at(f, tri.ravel(), np.repeat(2 * area / 3, 3))
    free = np.arange(nb, n)
    phi = np.zeros(n)
    phi[free] = spsolve(K[free][:, free].tocsc(), f[free])
    return float(phi @ f)


def torsion_fe(loops):
    """Richardson extrapolation (error ~ h^2) of two meshes, h = 0.7 and 0.5 mm."""
    j1, j2 = torsion_fe_once(loops, 0.7), torsion_fe_once(loops, 0.5)
    return j2 + (j2 - j1) * 0.25 / (0.49 - 0.25)


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
    J = torsion_fe([(sec_I_poly_coarse(467.2, 192.8, 11.4, 19.6, 10.2), 1)])
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
    # closed forms for the circle (a polygon of 4000 chords per quarter would carry 4e-5 error)
    D, d = 200.0, 180.0
    c = "ka-tube"
    for sym, kat, ind, tol in [("A", 59.690, math.pi * (D ** 2 - d ** 2) / 4 / CM2, 1e-4),
                               ("I_xx", 2701.1, math.pi * (D ** 4 - d ** 4) / 64 / CM4, 1e-4),
                               ("W_el_x", 270.11, math.pi * (D ** 4 - d ** 4) / 64 / (D / 2) / CM3, 1e-4),
                               ("W_pl_x", 361.33, (D ** 3 - d ** 3) / 6 / CM3, 1e-4),
                               ("J", 5402.2, math.pi * (D ** 4 - d ** 4) / 32 / CM4, 1e-4)]:
        row(c, sym, kat, ind, tol)
    # 5. unequal angle polygon
    s = sec_polygon([(0, 0), (90, 0), (90, 12), (12, 12), (12, 150), (0, 150)])
    c = "ka-angle-polygon"
    for sym, kat, ind, tol in [("A", 27.36, s["A"] / CM2, 1e-6), ("x_c", 21.39474, s["xc"], 1e-5),
                               ("y_c", 51.39474, s["yc"], 1e-5), ("I_xx", 631.80, s["Ixx"] / CM4, 2e-4),
                               ("I_yy", 174.31, s["Iyy"] / CM4, 2e-4), ("I_xy", -191.20, s["Ixy"] / CM4, 2e-4)]:
        row(c, sym, kat, ind, tol)


# ---------------------------------------------------------------- extra checks against the C# product
# Product values below were read from the probe tool (`Probe run struct.section-properties`) on release/v0.26.0 code.
# Units: mm, mm^2, mm^4, mm^3, mm^6.  A mismatch here is printed as DIFF and does not change the exit status;
# the exit status depends on the known-answer cases only.  The finding number is shown next to each DIFF.
PRODUCT = {'E1': {'A': 5381.7062,
            'xc': 0.0,
            'yc': 0.0,
            'Ixx': 83570141.8,
            'Iyy': 6037835.87,
            'Ixy': 0.0,
            'Wex': 557134.279,
            'Wey': 80504.4783,
            'Wpx': 628423.42,
            'Wpy': 125223.376,
            'J': 199186.918,
            'Iw': 126333398000.0,
            'theta_deg': 0.0},
     'E2': {'A': 2779.96679,
            'xc': 23.6035802,
            'yc': 100.0,
            'Ixx': 18033413.6,
            'Iyy': 1562755.35,
            'Ixy': 0.0,
            'Wex': 180334.136,
            'Wey': 30405.9184,
            'Wpx': 208799.676,
            'Wpy': 55079.018,
            'J': 79366.0,
            'Iw': None,
            'theta_deg': 0.0},
     'E3': {'A': 3600.0,
            'xc': 0.0,
            'yc': 114.375,
            'Ixx': 6838593.75,
            'Iyy': 4230000.0,
            'Ixy': 0.0,
            'Wex': 59790.9836,
            'Wey': 56400.0,
            'Wpx': 106650.0,
            'Wpy': 87750.0,
            'J': 213750.0,
            'Iw': None,
            'theta_deg': 0.0},
     'E4': {'A': 1680.98339,
            'xc': 19.6420846,
            'yc': 31.9116883,
            'Ixx': 1648303.87,
            'Iyy': 792850.433,
            'Ixy': -660643.168,
            'Wex': 24208.3234,
            'Wey': 14322.2596,
            'Wpx': 43821.5044,
            'Wpy': 25953.7551,
            'J': 55000.0,
            'Iw': None,
            'theta_deg': 28.5396856},
     'E5': {'A': 4433.83683,
            'xc': 0.0,
            'yc': 0.0,
            'Ixx': 22000129.7,
            'Iyy': 7323728.53,
            'Ixy': 0.0,
            'Wex': 220001.297,
            'Wey': 146474.571,
            'Wpx': 278217.932,
            'Wpy': 170126.091,
            'J': 17996719.7,
            'Iw': None,
            'theta_deg': 0.0},
     'E6': {'A': 7853.98163,
            'xc': 0.0,
            'yc': 0.0,
            'Ixx': 4908738.52,
            'Iyy': 4908738.52,
            'Ixy': 0.0,
            'Wex': 98174.7704,
            'Wey': 98174.7704,
            'Wpx': 166666.667,
            'Wpy': 166666.667,
            'J': 9817477.04,
            'Iw': None,
            'theta_deg': 0.0},
     'E7a': {'A': 6000.0,
             'xc': 0.0,
             'yc': 130.0,
             'Ixx': 24200000.0,
             'Iyy': 3000000.0,
             'Ixy': 0.0,
             'Wex': 186153.846,
             'Wey': 50000.0,
             'Wpx': 330000.0,
             'Wpy': 90000.0,
             'J': None,
             'Iw': None,
             'theta_deg': 0.0},
     'E7b': {'A': 2000.0,
             'xc': 50.0,
             'yc': 40.0,
             'Ixx': 466666.667,
             'Iyy': 1266666.67,
             'Ixy': 692820.323,
             'Wex': 13864.0269,
             'Wey': 26224.2931,
             'Wpx': 26000.0,
             'Wpy': 43493.7203,
             'J': None,
             'Iw': None,
             'theta_deg': -60.0},
     'E7c': {'A': 18000.0,
             'xc': -3.33333333,
             'yc': -1.11111111,
             'Ixx': 16177777.8,
             'Iyy': 64250000.0,
             'Ixy': -666666.667,
             'Wex': 316521.739,
             'Wey': 621774.194,
             'Wpx': 473333.333,
             'Wpy': 930000.0,
             'J': None,
             'Iw': None,
             'theta_deg': 89.2056245},
     'E7d': {'A': 56859.9402,
             'xc': 0.0,
             'yc': 0.0,
             'Ixx': 130056583.0,
             'Iyy': 130056583.0,
             'Ixy': 0.0,
             'Wex': 1300565.83,
             'Wey': 1300565.83,
             'Wpx': 2305157.78,
             'Wpy': 2305157.78,
             'J': None,
             'Iw': None,
             'theta_deg': 0.0},
     'E7e': {'A': 18030.0722,
             'xc': 0.0,
             'yc': 0.0,
             'Ixx': 13646245.2,
             'Iyy': 13646245.2,
             'Ixy': 0.0,
             'Wex': 272924.904,
             'Wey': 272924.904,
             'Wpx': 428966.495,
             'Wpy': 428966.495,
             'J': None,
             'Iw': None,
             'theta_deg': 0.0},
     'E7f': {'A': 33140.0598,
             'xc': 0.0,
             'yc': 0.0,
             'Ixx': 544943417.0,
             'Iyy': 544943417.0,
             'Ixy': 0.0,
             'Wex': 3632956.11,
             'Wey': 3632956.11,
             'Wpx': 4054636.86,
             'Wpy': 4054636.86,
             'J': None,
             'Iw': None,
             'theta_deg': 0.0},
     'E8': {'A': 9100.0,
            'xc': 51.4835165,
            'yc': 51.4835165,
            'Ixx': 8043305.86,
            'Iyy': 8043305.86,
            'Ixy': -222527.473,
            'Wex': 156230.701,
            'Wey': 156230.701,
            'Wpx': 234475.0,
            'Wpy': 234475.0,
            'J': None,
            'Iw': None,
            'theta_deg': 45.0},
     'E9a': {'A': 2e-06,
             'xc': 0.0,
             'yc': 0.0,
             'Ixx': 6.66666667e-13,
             'Iyy': 1.66666667e-13,
             'Ixy': 0.0,
             'Wex': 6.66666667e-10,
             'Wey': 3.33333333e-10,
             'Wpx': 1e-09,
             'Wpy': 5e-10,
             'J': 4.57363358e-13,
             'Iw': None,
             'theta_deg': 0.0},
     'E9b': {'A': 10000000000.0,
             'xc': 0.0,
             'yc': 0.0,
             'Ixx': 8.33333333e+18,
             'Iyy': 8.33333333e+18,
             'Ixy': 0.0,
             'Wex': 166666667000000.0,
             'Wey': 166666667000000.0,
             'Wpx': 250000000000000.0,
             'Wpy': 250000000000000.0,
             'J': 1.40577019e+19,
             'Iw': None,
             'theta_deg': 0.0},
     'E10a': {'A': 10000.0,
              'xc': 0.0,
              'yc': 0.0,
              'Ixx': 833333333.0,
              'Iyy': 83333.3333,
              'Ixy': 0.0,
              'Wex': 1666666.67,
              'Wey': 16666.6667,
              'Wpx': 2500000.0,
              'Wpy': 25000.0,
              'J': 331232.504,
              'Iw': None,
              'theta_deg': 0.0},
     'E10b': {'A': 10000.0,
              'xc': 0.0,
              'yc': 0.0,
              'Ixx': 8333333.33,
              'Iyy': 8333333.33,
              'Ixy': 0.0,
              'Wex': 166666.667,
              'Wey': 166666.667,
              'Wpx': 250000.0,
              'Wpy': 250000.0,
              'J': 14057701.9,
              'Iw': None,
              'theta_deg': 0.0},
     'E11': {'A': 799.5,
             'xc': 0.0,
             'yc': 0.0,
             'Ixx': 116466817.0,
             'Iyy': 2250010.41,
             'Ixy': 0.0,
             'Wex': 232933.633,
             'Wey': 15000.0694,
             'Wpx': 274675.125,
             'Wpy': 22562.4375,
             'J': 66.625,
             'Iw': 561940240000.0,
             'theta_deg': 0.0},
     'E12': {'A': 1650.0,
             'xc': 19.7727273,
             'yc': 32.2727273,
             'Ixx': 1636477.27,
             'Iyy': 791164.773,
             'Ixy': -664772.727,
             'Wex': 24162.7517,
             'Wey': 14325.6173,
             'Wpx': 43687.5,
             'Wpy': 25818.75,
             'J': 55000.0,
             'Iw': None,
             'theta_deg': 28.7760915}}

EXTRA_DIFFS = []


def xrow(probe, sym, prod, ind, tol, note=""):
    rel = abs(prod - ind) / abs(ind) if ind else abs(prod - ind)
    ok = rel <= max(tol, 1e-6)
    if not ok:
        EXTRA_DIFFS.append((probe, sym))
    print(f"{probe:<26}{sym:<10}{prod:>14.6g}{ind:>14.6g}{rel:>11.2e}{tol:>10.1e}  {'PASS' if ok else 'DIFF'} {note}")


def compare(probe, prod, s, tol=5e-4):
    ind = dict(A=s["A"], xc=s["xc"], yc=s["yc"], Ixx=s["Ixx"], Iyy=s["Iyy"], Ixy=s["Ixy"], Wex=s["Wel_x"], Wey=s["Wel_y"],
               Wpx=plastic_modulus(s, "y"), Wpy=plastic_modulus(s, "x"))
    scale = s["A"] ** 0.5
    for k, v in ind.items():
        if abs(v) < 1e-6 * scale and abs(prod[k]) < 1e-6 * scale:
            continue                                   # both zero to rounding (symmetry)
        if k == "Ixy" and abs(v) < 1e-9 * s["Ixx"] and abs(prod[k]) < 1e-9 * s["Ixx"]:
            continue
        xrow(probe, k, prod[k], v, tol)


def extras():
    P = PRODUCT
    print("--- extra checks: product (hard-coded) against this script ---")
    # E1 rolled I, 300 x 150, web 7.1, flange 10.7, r = 15
    s = sec_I(300, 150, 7.1, 10.7, 15)
    compare("E1 I 300x150x7.1x10.7", P["E1"], s)
    xrow("E1 I 300x150x7.1x10.7", "I_w", P["E1"]["Iw"], iw_I(s["Iyy"], 300, 10.7), 1e-3)
    Jfe = torsion_fe([(coarse(sec_I_poly, 300, 150, 7.1, 10.7, 15), 1)])
    xrow("E1 I 300x150x7.1x10.7", "J vs FE", P["E1"]["J"], Jfe, 1.5e-2, "finite-element J; regression within 1.5 %")
    # E2 channel 200 x 75, web 6, flange 11, r = 12 (web on the left, heel at the origin)
    s = section([(channel_poly(200, 75, 6, 11, 12), 1)])
    compare("E2 channel 200x75x6x11", P["E2"], s)
    xrow("E2 channel 200x75x6x11", "J sum", P["E2"]["J"], (2 * 75 * 11 ** 3 + (200 - 22) * 6 ** 3) / 3, 1e-9, "documented sum b t^3/3")
    Jfe = torsion_fe([(coarse(channel_poly, 200, 75, 6, 11, 12), 1)])
    print(f"{'(info) E2 channel':<26}{'J vs FE':<10}{P['E2']['J']:>14.6g}{Jfe:>14.6g}{(P['E2']['J'] - Jfe) / Jfe:>+11.2e}  product low: fillet term neglected (P1-struct.section-properties-03)")
    # E3 tee, sharp corners: exact by rectangles
    s = section([(tee_poly(150, 150, 10, 15, 0), 1)])
    compare("E3 tee 150x150x10x15", P["E3"], s, 1e-9)
    # E4 angle with root fillet
    s = section([(angle_poly(100, 75, 10, 12), 1)])
    compare("E4 angle 100x75x10 r12", P["E4"], s)
    Jfe = torsion_fe([(coarse(angle_poly, 100, 75, 10, 12), 1)])
    print(f"{'(info) E4 angle':<26}{'J vs FE':<10}{P['E4']['J']:>14.6g}{Jfe:>14.6g}{(P['E4']['J'] - Jfe) / Jfe:>+11.2e}  product low: fillet term neglected (P1-struct.section-properties-03)")
    s = section([(angle_poly(100, 75, 10, 0), 1)])
    xrow("E12 angle sharp", "theta", P["E12"]["theta_deg"], math.degrees(s["theta"]), 1e-8)
    # E5 hollow rectangle with explicit radii
    s = sec_hollow_rect(200, 100, 8, 12, 4)
    compare("E5 hollow 200x100x8", P["E5"], s, 5e-4)
    rc = (12 + 4) / 2
    Am = 92 * 192 - (4 - math.pi) * rc ** 2
    pm = 2 * (92 + 192) - (8 - 2 * math.pi) * rc
    xrow("E5 hollow 200x100x8", "J Bredt", P["E5"]["J"], 4 * Am ** 2 * 8 / pm + pm * 8 ** 3 / 3, 1e-6, "mid-line radius (r_o + r_i)/2")
    # E6 circle, closed forms
    d = 100.0
    for sym, k, v in [("A", "A", math.pi * d * d / 4), ("I_xx", "Ixx", math.pi * d ** 4 / 64), ("W_pl_x", "Wpx", d ** 3 / 6),
                      ("J", "J", math.pi * d ** 4 / 32)]:
        xrow("E6 circle d=100", sym, P["E6"][k], v, 1e-9)
    # E7 composites
    s = section([(rect_loop(20, 180, 0, 90), 1), (rect_loop(120, 20, 0, 190), 1)])
    compare("E7a composite T", P["E7a"], s, 1e-9)
    th = math.radians(30)
    pts = [(50 + math.cos(th) * x - math.sin(th) * y, 40 + math.sin(th) * x + math.cos(th) * y) for x, y in rect_loop(100, 20)]
    s = section([(pts, 1)])
    compare("E7b rotated 30 deg", P["E7b"], s, 1e-8)
    xrow("E7b rotated 30 deg", "theta", P["E7b"]["theta_deg"], math.degrees(s["theta"]), 1e-8)
    s = section([(rect_loop(200, 100), 1), (rect_loop(50, 40, 30, 10), -1)])
    compare("E7c rect minus rect", P["E7c"], s, 1e-8)
    # defect probes: hollow primitives used as composite parts
    ring = math.pi * (200 ** 2 - 180 ** 2) / 4
    xrow("E7d composite tube add", "A", P["E7d"]["A"], ring, 1e-3, "P1-struct.section-properties-01")
    xrow("E7d composite tube add", "I_xx", P["E7d"]["Ixx"], math.pi * (200 ** 4 - 180 ** 4) / 64, 1e-3, "P1-struct.section-properties-01")
    xrow("E7e composite SHS add", "A", P["E7e"]["A"], sec_hollow_rect(100, 100, 5)["A"], 1e-3, "P1-struct.section-properties-01")
    xrow("E7f 300sq minus tube", "A", P["E7f"]["A"], 300 * 300 - ring, 1e-3, "P1-struct.section-properties-01")
    # E8 polygon with a hole
    s = section([([(0, 0), (100, 0), (100, 100), (0, 100)], 1), ([(20, 20), (20, 50), (50, 50), (50, 20)], -1)])
    compare("E8 polygon with hole", P["E8"], s, 1e-8)
    # E9 extreme sizes
    for tag, b, h in [("E9a", 0.001, 0.002), ("E9b", 1e5, 1e5)]:
        xrow(f"{tag} rect {b:g} x {h:g}"[:25], "I_xx", P[tag]["Ixx"], b * h ** 3 / 12, 1e-8)
        xrow(f"{tag} rect {b:g} x {h:g}"[:25], "J", P[tag]["J"], j_rect_exact(b, h), 1e-6)
    xrow("E9b rect 1e5 x 1e5", "W_pl_x", P["E9b"]["Wpx"], 1e5 ** 3 / 4, 1e-9)
    # E10 slender and square rectangles: exact series
    xrow("E10a rect 10 x 1000", "J", P["E10a"]["J"], j_rect_exact(10, 1000), 1e-6)
    xrow("E10b rect 100 x 100", "J", P["E10b"]["J"], j_rect_exact(100, 100), 1e-6)
    # E11 very thin I-section, sharp corners
    xrow("E11 thin I 1000x300x0.5", "J", P["E11"]["J"], (2 * 300 * 0.5 ** 3 + 999 * 0.5 ** 3) / 3, 1e-9)
    xrow("E11 thin I 1000x300x0.5", "A", P["E11"]["A"], 2 * 300 * 0.5 + 999 * 0.5, 1e-9)
    print(f"extra checks with DIFF: {len(EXTRA_DIFFS)} {EXTRA_DIFFS}")


if __name__ == "__main__":
    kats()
    extras()
    print("RESULT:", "ALL PASS" if FAIL[0] == 0 else f"{FAIL[0]} FAIL")
    sys.exit(0 if FAIL[0] == 0 else 1)
