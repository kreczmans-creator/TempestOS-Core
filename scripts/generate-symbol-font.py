"""Generates TempestSymbols-Regular.ttf, the calculation sheet PDF's fallback face.

The body face (the platform default) and the brand faces have no glyph for a few
symbols the calculators print: the perpendicular and parallel subscripts of a
fillet weld (⊥, ∥) and the contour integral of a section's area (∮). They drew
as boxes on the issued sheet. These glyphs are drawn here from plain geometry,
so the face is the project's own work and carries no third-party licence.

Run: python scripts/generate-symbol-font.py
It needs fontTools (MIT), at generation time only; the build only embeds the
resulting file (src/Tempest.Desktop/Documents/Assets/Fonts/).
"""

import math
import os

from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

UPM = 2048  # the body face's (Inter's) units per em, so the glyphs sit at its size
STROKE = 150  # Inter Regular's operator bar weight
CAP = 1490  # Inter's cap height
OPERATOR = 1355  # Inter's advance for + − = ≤


def area(points):
    return sum(x0 * y1 - x1 * y0 for (x0, y0), (x1, y1) in zip(points, points[1:] + points[:1])) / 2


def oriented(points, clockwise=True):
    """TrueType fills clockwise outer contours; an inner (hole) contour runs counter-clockwise."""
    pts = [(round(x), round(y)) for x, y in points]
    if (area(pts) < 0) != clockwise:
        pts.reverse()
    return pts


def rect(x0, y0, x1, y1):
    return oriented([(x0, y0), (x0, y1), (x1, y1), (x1, y0)])


def ellipse(cx, cy, rx, ry, clockwise=True, steps=72):
    return oriented([(cx + rx * math.cos(2 * math.pi * k / steps), cy + ry * math.sin(2 * math.pi * k / steps)) for k in range(steps)], clockwise)


def ring(cx, cy, outer, inner):
    return [ellipse(cx, cy, outer, outer), ellipse(cx, cy, inner, inner, clockwise=False)]


def arc_band(cx, cy, outer, inner, start_deg, end_deg, steps=48):
    """A band between two concentric circles, from one angle to another (degrees, anticlockwise)."""
    angles = [math.radians(start_deg + (end_deg - start_deg) * k / steps) for k in range(steps + 1)]
    out = [(cx + outer * math.cos(a), cy + outer * math.sin(a)) for a in angles]
    inn = [(cx + inner * math.cos(a), cy + inner * math.sin(a)) for a in reversed(angles)]
    return oriented(out + inn)


def thick_line(x0, y0, x1, y1, width=STROKE):
    dx, dy = x1 - x0, y1 - y0
    length = math.hypot(dx, dy)
    nx, ny = -dy / length * width / 2, dx / length * width / 2
    return oriented([(x0 + nx, y0 + ny), (x1 + nx, y1 + ny), (x1 - nx, y1 - ny), (x0 - nx, y0 - ny)])


def stroke(path, width):
    """A polyline drawn at an even width: each point offset along the mean normal of its two segments."""
    left, right = [], []
    for i, (x, y) in enumerate(path):
        (ax, ay), (bx, by) = path[max(i - 1, 0)], path[min(i + 1, len(path) - 1)]
        dx, dy = bx - ax, by - ay
        length = math.hypot(dx, dy)
        nx, ny = -dy / length * width / 2, dx / length * width / 2
        left.append((x + nx, y + ny))
        right.append((x - nx, y - ny))
    return oriented(left + list(reversed(right)))


def integral_path():
    """∫ as an engineer writes it: a hook at the foot, a leaning stem, a hook at the head."""
    path = []
    for k in range(13):  # foot hook: from the left, curling down and up into the stem
        a = math.radians(200 + 160 * k / 12)
        path.append((300 + 150 * math.cos(a), -150 + 150 * math.sin(a)))
    for k in range(1, 12):  # the stem, leaning right
        t = k / 12
        path.append((450 + 100 * t, -150 + 1700 * t))
    for k in range(13):  # head hook: up over the top and down to the right
        a = math.radians(180 - 160 * k / 12)
        path.append((700 + 150 * math.cos(a), 1550 + 150 * math.sin(a)))
    return path


def glyphs():
    up_tack = [rect(160, 0, OPERATOR - 160, STROKE), rect(OPERATOR / 2 - STROKE / 2, 0, OPERATOR / 2 + STROKE / 2, CAP)]
    parallel = [rect(300, -250, 300 + STROKE, 1650), rect(700, -250, 700 + STROKE, 1650)]
    element = [
        arc_band(760, 620, 560, 560 - STROKE, 90, 270),
        rect(760, 1180 - STROKE, 1190, 1180),
        rect(760, 60, 1190, 60 + STROKE),
        rect(200, 620 - STROKE / 2, 1190, 620 + STROKE / 2),
    ]
    centre = 545
    minus_plus = [
        rect(160, 1050, OPERATOR - 160, 1050 + STROKE),
        rect(160, centre - 200 - STROKE / 2, OPERATOR - 160, centre - 200 + STROKE / 2),
        rect(OPERATOR / 2 - STROKE / 2, 0, OPERATOR / 2 + STROKE / 2, 860),
    ]
    proportional = [
        ellipse(520, 560, 360, 330), ellipse(520, 560, 360 - STROKE, 330 - STROKE, clockwise=False),
        arc_band(1240, 560, 400, 400 - STROKE, 125, 235),
    ]
    angle = [rect(160, 0, OPERATOR - 160, STROKE), thick_line(160 + STROKE * 0.4, STROKE / 2, OPERATOR - 260, CAP - 150)]
    contour = [stroke(integral_path(), 140), *ring(500, 700, 300, 300 - 130)]
    superscript_t = [rect(100, 1500, 800, 1620), rect(390, 900, 510, 1500)]
    not_parallel = [*parallel, thick_line(220, 150, 930, 1250)]

    return {
        "uni22A5": (0x22A5, OPERATOR, up_tack),  # ⊥ up tack (perpendicular)
        "uni27C2": (0x27C2, OPERATOR, up_tack),  # ⟂ perpendicular
        "uni2225": (0x2225, 1150, parallel),  # ∥ parallel to
        "uni2016": (0x2016, 1150, parallel),  # ‖ double vertical line
        "uni2226": (0x2226, 1150, not_parallel),  # ∦ not parallel to
        "uni222E": (0x222E, 1000, contour),  # ∮ contour integral
        "uni2208": (0x2208, OPERATOR, element),  # ∈ element of
        "uni2213": (0x2213, OPERATOR, minus_plus),  # ∓ minus-or-plus
        "uni221D": (0x221D, OPERATOR, proportional),  # ∝ proportional to
        "uni2220": (0x2220, OPERATOR, angle),  # ∠ angle
        "uni1D40": (0x1D40, 900, superscript_t),  # ᵀ modifier letter capital T (a transpose)
    }


def draw(contours):
    pen = TTGlyphPen(None)
    for contour in contours:
        pen.moveTo(contour[0])
        for point in contour[1:]:
            pen.lineTo(point)
        pen.closePath()
    return pen.glyph()


def main():
    shapes = glyphs()
    order = [".notdef", "space", *shapes]
    builder = FontBuilder(UPM, isTTF=True)
    builder.setupGlyphOrder(order)
    builder.setupCharacterMap({0x20: "space", **{code: name for name, (code, _, _) in shapes.items()}})

    notdef = draw([rect(100, 0, 900, 1400), oriented([(220, 120), (780, 120), (780, 1280), (220, 1280)], clockwise=False)])
    builder.setupGlyf({".notdef": notdef, "space": TTGlyphPen(None).glyph(), **{name: draw(contours) for name, (_, _, contours) in shapes.items()}})

    metrics = {".notdef": (1000, 100), "space": (520, 0)}
    glyf = builder.font["glyf"]
    for name, (_, advance, _) in shapes.items():
        glyph = glyf[name]
        glyph.recalcBounds(glyf)
        metrics[name] = (advance, glyph.xMin)
    builder.setupHorizontalMetrics(metrics)
    builder.setupHorizontalHeader(ascent=1984, descent=-494)
    builder.setupNameTable({
        "familyName": "Tempest Symbols",
        "styleName": "Regular",
        "uniqueFontIdentifier": "TempestSymbols-Regular-1.0",
        "fullName": "Tempest Symbols Regular",
        "psName": "TempestSymbols-Regular",
        "version": "Version 1.000",
        "copyright": "Copyright 2026 Steven Kreczman. Drawn for TempestOS.",
    })
    builder.setupOS2(sTypoAscender=1984, sTypoDescender=-494, usWinAscent=1984, usWinDescent=494, sCapHeight=CAP, sxHeight=1118, fsType=0)
    builder.setupPost()
    builder.setupHead(unitsPerEm=UPM, fontRevision=1.0)

    out = os.path.join(os.path.dirname(__file__), "..", "src", "Tempest.Desktop", "Documents", "Assets", "Fonts", "TempestSymbols-Regular.ttf")
    builder.save(out)
    print(f"wrote {os.path.normpath(out)}: {len(shapes)} glyphs")


if __name__ == "__main__":
    main()
