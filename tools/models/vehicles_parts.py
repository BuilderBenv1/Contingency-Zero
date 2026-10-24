"""Shared parts for the ground vehicles (tanks, builders, flak, artillery, salvagers) of both factions.

Blender Z-up, unit faces +Y, authored at radius 1. Game procedural models use (x, y_up, z_back);
a game point (x, y, z) is (x, -z, y) here.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
from mathutils import Vector


def out_dir():
    return sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'


def beam(name, a, b, w, h, paint, bevel=0.012, up=(0, 0, 1)):
    """A rectangular bar from a to b, w wide and h tall (h measured along the 'up' side)."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    u = Vector(up)
    if abs(d.dot(u)) > 0.95:
        u = Vector((0, 1, 0)) if abs(d.y) < 0.9 else Vector((1, 0, 0))
    side = d.cross(u).normalized()
    up2 = side.cross(d).normalized()
    pts = []
    for p in (a, b):
        for sx in (-1, 1):
            for sz in (-1, 1):
                pts.append(p + side * (sx * w / 2) + up2 * (sz * h / 2))
    return hull_from(name, [tuple(p) for p in pts], paint, bevel=bevel, segs=1)


def tapered(name, a, b, w0, h0, w1, h1, paint, bevel=0.012, up=(0, 0, 1)):
    """A bar that narrows from (w0, h0) at a to (w1, h1) at b."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    u = Vector(up)
    if abs(d.dot(u)) > 0.95:
        u = Vector((0, 1, 0)) if abs(d.y) < 0.9 else Vector((1, 0, 0))
    side = d.cross(u).normalized()
    up2 = side.cross(d).normalized()
    pts = []
    for p, w, h in ((a, w0, h0), (b, w1, h1)):
        for sx in (-1, 1):
            for sz in (-1, 1):
                pts.append(p + side * (sx * w / 2) + up2 * (sz * h / 2))
    return hull_from(name, [tuple(p) for p in pts], paint, bevel=bevel, segs=1)


def elevated(root, deg, dist):
    """Point dist along a barrel pitched up by deg, pointing +Y."""
    r = math.radians(deg)
    return (root[0], root[1] + math.cos(r) * dist, root[2] + math.sin(r) * dist)


# ---------------------------------------------------------------------------------------------
# Crucible running gear
# ---------------------------------------------------------------------------------------------

def crucible_tracks(tr_in, tr_out, y0, y1, r_end=0.2, wheels=6, cleats=22, skirt='team', skirt_top=0.46,
                    skirt_rivets=9, prefix='trk'):
    """The Anvil's tracks: band, cleats, road wheels with hubs, sprocket and idler, and a skirt.
    Track centre-line height is r_end + 0.01 (so the band touches the ground)."""
    parts = []
    zc = r_end + 0.01
    for sx in (-1, 1):
        xa, xb = sx * tr_in, sx * tr_out
        x0, x1 = min(xa, xb), max(xa, xb)
        xc = (x0 + x1) / 2
        pts = []
        for i in range(10):
            a = -math.pi / 2 + math.pi * i / 9
            pts.append((y1 - r_end + math.cos(a) * r_end, zc + math.sin(a) * r_end))
        for i in range(10):
            a = math.pi / 2 + math.pi * i / 9
            pts.append((y0 + r_end + math.cos(a) * r_end, zc + math.sin(a) * r_end))
        parts.append(prism_x(f'{prefix}band{sx}', pts, x0, x1, 'tread', bevel=0.01, segs=1))
        for i in range(cleats):
            y = y0 + r_end + (y1 - y0 - 2 * r_end) * i / (cleats - 1)
            parts.append(box(f'{prefix}cb{sx}_{i}', (xc, y, 0.005), (x1 - x0 + 0.02, 0.045, 0.03), 'mech', 0.006, 1))
            parts.append(box(f'{prefix}ct{sx}_{i}', (xc, y, zc + r_end - 0.005), (x1 - x0 + 0.02, 0.045, 0.03), 'mech', 0.006, 1))
        wr = r_end * 0.65
        span = (y1 - y0) - 2 * r_end - 0.1
        for i in range(wheels):
            y = y0 + r_end + 0.05 + span * i / (wheels - 1)
            parts.append(cyl(f'{prefix}wh{sx}_{i}', (x0 - 0.01, y, wr + 0.02), (x1 + 0.01, y, wr + 0.02), wr, 'metal', 14, bevel=0.012))
            hx = x1 + 0.02 if sx > 0 else x0 - 0.02
            parts.append(cyl(f'{prefix}hub{sx}_{i}', (hx - sx * 0.03, y, wr + 0.02), (hx, y, wr + 0.02), wr * 0.42, 'mech', 8, bevel=0))
        for y, r in ((y1 - r_end, r_end * 0.85), (y0 + r_end, r_end * 0.75)):
            parts.append(cyl(f'{prefix}end{sx}_{y}', (x0 - 0.015, y, zc), (x1 + 0.015, y, zc), r, 'mech', 12, bevel=0.01))
            if y > 0:
                for k in range(8):
                    a = k * math.pi / 4
                    parts.append(box(f'{prefix}tooth{sx}_{k}', (xc, y + math.cos(a) * r, zc + math.sin(a) * r),
                                     (x1 - x0 + 0.04, 0.04, 0.04), 'metal', 0.004, 1))
        if skirt:
            x = sx * (tr_out + 0.025)
            zt, zb = skirt_top, zc + 0.07
            sk = prism_x(f'{prefix}skirt{sx}', [(y1 + 0.04, zt), (y1 - 0.04, zb), (y0 + 0.06, zb), (y0, zt)],
                         x - 0.022, x + 0.022, skirt, 0.01, 1)
            parts.append(sk)
            if skirt_rivets:
                parts += rivets(f'{prefix}skr{sx}_', (x + sx * 0.025, y1 - 0.09, zt - 0.03), (x + sx * 0.025, y0 + 0.08, zt - 0.03),
                                skirt_rivets, 'metal', 0.018)
    return parts


def crucible_wheel(name, c, r, width, sx, lugs=10):
    """A chunky off-road wheel on an X axle: tyre, lugs, steel rim and hub nut. sx = outward side."""
    x, y, z = c
    parts = []
    xa, xb = x - width / 2, x + width / 2
    parts.append(cyl(name + 'tyre', (xa, y, z), (xb, y, z), r, 'tread', 16, bevel=0.02))
    for k in range(lugs):
        a = k * 2 * math.pi / lugs
        parts.append(box(f'{name}lug{k}', (x, y + math.cos(a) * r, z + math.sin(a) * r), (width * 0.9, 0.06, 0.05), 'tread', 0.008, 1))
    xo = xb if sx > 0 else xa
    parts.append(cyl(name + 'rim', (xo - sx * 0.02, y, z), (xo + sx * 0.012, y, z), r * 0.6, 'metal', 12, bevel=0.008))
    parts.append(cyl(name + 'hub', (xo, y, z), (xo + sx * 0.05, y, z), r * 0.26, 'mech', 8, bevel=0.006))
    for k in range(5):
        a = k * 2 * math.pi / 5
        parts.append(sphere(f'{name}nut{k}', (xo + sx * 0.014, y + math.cos(a) * r * 0.42, z + math.sin(a) * r * 0.42), 0.018, 'mech', 6, 3))
    return parts


def stack(name, base, r, h, band=True):
    """A Crucible smokestack with a glowing mouth."""
    x, y, z = base
    p = [cyl(name, (x, y, z), (x, y, z + h), r, 'metal', 10, r2=r * 0.86)]
    p.append(cyl(name + 'cap', (x, y, z + h), (x, y, z + h + 0.03), r * 0.92, 'glow', 10, bevel=0))
    p.append(cyl(name + 'rim', (x, y, z + h - 0.04), (x, y, z + h + 0.005), r * 1.12, 'mech', 10, bevel=0))
    if band:
        p.append(cyl(name + 'band', (x, y, z + h * 0.45), (x, y, z + h * 0.55), r * 1.15, 'accent', 10, bevel=0))
    return p


def grille(name, c, w, l, n, paint='mech', along='y'):
    """Slatted engine grille block."""
    x, y, z = c
    p = [box(name, (x, y, z), (w, l, 0.06), 'hull_dark', 0.012, 1)]
    for i in range(n):
        t = -0.5 + (i + 0.5) / n
        if along == 'y':
            p.append(box(f'{name}s{i}', (x, y + t * l * 0.9, z + 0.035), (w * 0.85, l * 0.9 / n * 0.5, 0.02), paint, 0.004, 1))
        else:
            p.append(box(f'{name}s{i}', (x + t * w * 0.9, y, z + 0.035), (w * 0.9 / n * 0.5, l * 0.85, 0.02), paint, 0.004, 1))
    return p


# ---------------------------------------------------------------------------------------------
# Lattice running gear
# ---------------------------------------------------------------------------------------------

def lattice_pods(cx, w, y0, y1, h, top='team', prefix='pod', cleats=16, glow=True):
    """Sleek Lattice track pods at x = +/-cx, w wide, from y0 (rear) to y1 (front), h tall.
    A thin tread band runs underneath a faired, chamfered armour shell with a team top plate,
    a glowing strip on the outer face and a bevelled nose. Returns parts."""
    parts = []
    for sx in (-1, 1):
        xc = sx * cx
        x0, x1 = xc - w / 2, xc + w / 2
        # Tread band: low stadium, slightly inset.
        rb = 0.09
        pts = []
        for i in range(8):
            a = -math.pi / 2 + math.pi * i / 7
            pts.append((y1 - 0.12 - rb + math.cos(a) * rb, rb + math.sin(a) * rb))
        for i in range(8):
            a = math.pi / 2 + math.pi * i / 7
            pts.append((y0 + 0.1 + rb + math.cos(a) * rb, rb + math.sin(a) * rb))
        parts.append(prism_x(f'{prefix}band{sx}', pts, x0 + 0.03, x1 - 0.03, 'tread', bevel=0.008, segs=1))
        for i in range(cleats):
            y = y0 + 0.1 + rb + (y1 - y0 - 0.22 - 2 * rb) * i / (cleats - 1)
            parts.append(box(f'{prefix}cl{sx}_{i}', (xc, y, 0.006), (w - 0.05, 0.035, 0.018), 'mech', 0.004, 1))
        # Faired shell over the band.
        z0 = 0.1
        prof = [(y1, z0 + 0.1), (y1 - 0.06, z0 + 0.04 + (h - z0) * 0.55), (y1 - 0.3, h), (y0 + 0.16, h),
                (y0, h - 0.1), (y0, z0 + 0.06), (y0 + 0.14, z0), (y1 - 0.16, z0)]
        shell = prism_x(f'{prefix}shell{sx}', prof, x0, x1, 'hull', bevel=0.02, segs=2)
        inset_panels(shell, 0.025, 0.01, lambda f: abs(f.normal.x) > 0.9 and f.calc_area() > 0.03)
        parts.append(shell)
        # Team top plate, chamfered, plus a dark nose cap.
        tp = [(y1 - 0.3, h + 0.035), (y1 - 0.22, h - 0.005), (y0 + 0.12, h - 0.005), (y0 + 0.2, h + 0.035)]
        parts.append(prism_x(f'{prefix}top{sx}', tp, x0 + 0.015, x1 - 0.015, top, 0.008, 1))
        parts.append(prism_x(f'{prefix}nose{sx}', [(y1 + 0.012, z0 + 0.12), (y1 - 0.05, z0 + 0.04 + (h - z0) * 0.52),
                                                   (y1 - 0.11, z0 + 0.03 + (h - z0) * 0.52), (y1 - 0.05, z0 + 0.12)],
                             x0 + 0.03, x1 - 0.03, 'hull_dark', 0.006, 1))
        xo = x1 if sx > 0 else x0
        # Lower armour strake on the outer face, bolted, and bogie wheels peeking out under it.
        parts.append(prism_x(f'{prefix}strake{sx}', [(y1 - 0.2, z0 + 0.03), (y1 - 0.26, z0 + 0.13), (y0 + 0.22, z0 + 0.13), (y0 + 0.12, z0 + 0.03)],
                             xo - sx * 0.01, xo + sx * 0.022, 'hull_dark', 0.006, 1))
        parts += rivets(f'{prefix}sbolt{sx}', (xo + sx * 0.024, y1 - 0.3, z0 + 0.09), (xo + sx * 0.024, y0 + 0.24, z0 + 0.09),
                        7, 'metal', 0.012)
        nb = 5
        for i in range(nb):
            y = y0 + 0.28 + (y1 - y0 - 0.6) * i / (nb - 1)
            parts.append(cyl(f'{prefix}bogie{sx}{i}', (xo - sx * 0.06, y, 0.075), (xo - sx * 0.025, y, 0.075), 0.06, 'metal', 10, bevel=0.004))
        if glow:
            parts.append(box(f'{prefix}glow{sx}', (xo + sx * 0.006, (y0 + y1) / 2 + 0.05, z0 + (h - z0) * 0.42),
                             (0.012, (y1 - y0) * 0.5, 0.025), 'glow', 0, 1))
        # Small vent louvres at the back of the outer face.
        for k in range(3):
            parts.append(box(f'{prefix}vent{sx}{k}', (xo + sx * 0.004, y0 + 0.18 + k * 0.06, z0 + (h - z0) * 0.62),
                             (0.012, 0.025, 0.07), 'mech', 0, 1))
    return parts


def lattice_eye(name, c, w, paint='glow'):
    """A sensor eye strip in a dark visor housing, facing +Y."""
    x, y, z = c
    return [box(name + 'hood', (x, y - 0.02, z), (w + 0.05, 0.06, 0.07), 'mech', 0.01, 1),
            box(name, (x, y + 0.012, z), (w, 0.012, 0.03), paint, 0, 1)]


# ---------------------------------------------------------------------------------------------
# Nanolathe arm (both factions)
# ---------------------------------------------------------------------------------------------

def nano_arm(prefix, base, size=1.0, yaw_deg=0.0, faction='lattice', reach=1.0):
    """A nanolathe crane: turntable, raised upper arm (team), forearm reaching forward (+Y) and a
    glowing emitter head. About `size` tall. yaw_deg turns it about Z (positive = to the left)."""
    bx, by, bz = base
    yaw = math.radians(yaw_deg)
    cs, sn = math.cos(yaw), math.sin(yaw)
    def P(x, y, z):
        x, y, z = x * size, y * size, z * size
        return (bx + x * cs - y * sn, by + x * sn + y * cs, bz + z)
    t = 1.0 if faction == 'lattice' else 1.3
    lat = faction == 'lattice'
    parts = []
    parts.append(cyl(prefix + 'base', P(0, 0, 0), P(0, 0, 0.1), 0.2 * t * size, 'mech', 12, r2=0.17 * t * size))
    parts.append(cyl(prefix + 'ring', P(0, 0, 0.1), P(0, 0, 0.13), 0.15 * t * size, 'metal', 12, bevel=0))
    parts.append(box(prefix + 'yoke', P(0, 0, 0.18), (0.18 * t * size, 0.16 * size, 0.12 * size), 'hull_dark', 0.01, 1))
    sh, el = P(0, -0.04, 0.2), P(0, -0.2, 0.78)
    wr = P(0, 0.48 * reach, 0.62)
    parts.append(tapered(prefix + 'upper', sh, el, 0.15 * t * size, 0.13 * size, 0.12 * t * size, 0.11 * size, 'team', 0.012))
    parts.append(tapered(prefix + 'fore', el, wr, 0.1 * t * size, 0.1 * size, 0.08 * t * size, 0.08 * size,
                         'hull' if lat else 'accent', 0.01))
    parts.append(sphere(prefix + 'elbow', el, 0.085 * t * size, 'mech', 10, 6))
    if not lat:
        # Hydraulic ram under the upper arm.
        parts.append(cyl(prefix + 'ram', P(0, 0.1, 0.16), P(0, -0.08, 0.6), 0.03 * size, 'metal', 6, bevel=0))
        parts.append(cyl(prefix + 'ramc', P(0, 0.1, 0.16), P(0, 0.02, 0.38), 0.045 * size, 'mech', 6, bevel=0))
    else:
        # Fine conduit along the upper arm with a glowing node.
        parts.append(cyl(prefix + 'cond', P(0.08, -0.02, 0.24), P(0.07, -0.18, 0.72), 0.014 * size, 'glow_dim', 5, bevel=0))
    # Emitter head: a short housing pointing forward-down, lens rings and a glowing tip.
    d = Vector((0, 0.9, -0.45)).normalized()
    hd = Vector(wr)
    tipdir = Vector(P(0, d.y, d.z)) - Vector(P(0, 0, 0))
    tipdir.normalize()
    h1 = hd + tipdir * 0.18 * size
    parts.append(cyl(prefix + 'head', tuple(hd - tipdir * 0.04 * size), tuple(h1), 0.075 * t * size, 'metal', 10, r2=0.06 * t * size))
    for k in (0.06, 0.12):
        pp = hd + tipdir * k * size
        parts.append(cyl(f'{prefix}lens{k}', tuple(pp), tuple(pp + tipdir * 0.02 * size), 0.085 * t * size, 'glow_dim' if lat else 'mech', 10, bevel=0))
    parts.append(sphere(prefix + 'tip', tuple(h1 + tipdir * 0.05 * size), 0.065 * t * size, 'glow', 10, 6))
    if lat:
        # Two slim prongs framing the tip.
        for s in (-1, 1):
            side = Vector((cs, sn, 0)) * s * 0.07 * size
            parts.append(cyl(f'{prefix}prong{s}', tuple(hd + side), tuple(h1 + side + tipdir * 0.08 * size), 0.012 * size, 'hull', 5, bevel=0))
    return parts


# ---------------------------------------------------------------------------------------------
# Finishing
# ---------------------------------------------------------------------------------------------

def finish(uid, body, turret=None, pivot=None, size=512):
    """Joins, paints, unwraps, bakes and exports. Returns the triangle count."""
    out = out_dir()
    os.makedirs(out, exist_ok=True)
    B = kit.join('Body', body)
    objs = [B]
    T = None
    if turret:
        T = kit.join('Turret', turret, parent_origin=pivot)
        objs.append(T)
    if os.environ.get('VP_DRY'):
        # Geometry check only: no bake or export.
        print('tris', {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs})
        return 0
    for o in objs:
        kit.paint_vertex_colours(o)
        kit.unwrap(o)
    kit.bake_detail(B, os.path.join(out, f'{uid}_body.png'), size)
    if T:
        kit.bake_detail(T, os.path.join(out, f'{uid}_turret.png'), size)
    kit.export(os.path.join(out, f'{uid}.glb'), objs)
    tris = sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)
    print('tris', tris, {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs})
    return tris
