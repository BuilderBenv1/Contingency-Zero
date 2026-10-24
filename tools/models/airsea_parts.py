"""Shared helpers for the air and sea unit models (and the two experimentals).

Builds on kit.py: multi-paint lofts and wings, rings, thrusters, missile pods, nano emitters,
treads, ship hulls, and a finish() that joins, paints, unwraps, bakes and exports the parts.
All coordinates are Blender's: x right, y forward (the unit's nose), z up.
Aircraft are modelled around z = 0 (belly near 0; the game lifts them to flight altitude).
Ships sit with the waterline at z = 0: the hull dips a little below it.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import bpy, bmesh
from mathutils import Vector, Matrix, Euler
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels, add_bevel

SIDES = (-1, 1)


def out_dir():
    return sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'


def _obj_multi(name, bm, paints):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    for p in paints:
        me.materials.append(bpy.data.materials[p])
    return o


def loft(name, rings, paint, edge_paint=None, cap0=None, cap1=None, bevel=0.012, segs=1, caps=True):
    """Lofts closed rings (lists of 3D points, same count, same winding) into one solid.
    edge_paint(j) gives the paint for the band starting at ring point j (None = paint)."""
    paints = [paint]
    def slot(p):
        p = p or paint
        if p not in paints: paints.append(p)
        return paints.index(p)
    bm = bmesh.new()
    vr = [[bm.verts.new(Vector(p)) for p in r] for r in rings]
    n = len(rings[0])
    for i in range(len(rings) - 1):
        for j in range(n):
            k = (j + 1) % n
            a, b, c, d = vr[i][j], vr[i][k], vr[i + 1][k], vr[i + 1][j]
            quad = [a, b, c, d]
            # Collapse degenerate quads (a ring shrunk to a point).
            uniq = []
            for v in quad:
                if all((v.co - u.co).length > 1e-6 for u in uniq): uniq.append(v)
            if len(uniq) < 3: continue
            try:
                f = bm.faces.new(uniq)
            except ValueError:
                continue
            f.material_index = slot(edge_paint(j) if edge_paint else None)
    if caps:
        for ring, cp in ((vr[0], cap0), (vr[-1], cap1)):
            uniq = []
            for v in ring:
                if all((v.co - u.co).length > 1e-6 for u in uniq): uniq.append(v)
            if len(uniq) >= 3:
                try:
                    f = bm.faces.new(uniq); f.material_index = slot(cp)
                except ValueError:
                    pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = _obj_multi(name, bm, paints)
    if bevel: add_bevel(o, bevel, segs)
    return o


def hexring(y, hw, ht, z0, z1, z2, bw=0.8, x=0.0):
    """Parts.Hull section: flat bottom (bw * hw), sides up to z1, sloping in to a ht-wide top at z2.
    Bands: 0 bottom, 1 right side, 2 right shoulder, 3 top, 4 left shoulder, 5 left side."""
    return [(x - hw * bw, y, z0), (x + hw * bw, y, z0), (x + hw, y, z1), (x + ht, y, z2), (x - ht, y, z2), (x - hw, y, z1)]


def octring(y, hb, hw, ht, zb, zm0, zm1, zt, x=0.0):
    """Eight-sided section: bottom hb wide at zb, full width hw between zm0 and zm1, top ht wide at zt.
    Bands: 0 bottom, 1 lower right, 2 right side, 3 upper right, 4 top, 5 upper left, 6 left side, 7 lower left."""
    return [(x - hb, y, zb), (x + hb, y, zb), (x + hw, y, zm0), (x + hw, y, zm1), (x + ht, y, zt),
            (x - ht, y, zt), (x - hw, y, zm1), (x - hw, y, zm0)]


def wing(name, planform, zc, t_root, t_tip, top, side, x_root=None, x_tip=None, dihedral=0.0, mirror=True,
         bevel=0.006, panels=True):
    """A wing slab from a right-hand (x > 0) planform [(x, y), ...]. Thickness tapers from t_root at
    x_root to t_tip at x_tip; dihedral raises z by dihedral per unit of x. Returns [right, left]."""
    xs = [p[0] for p in planform]
    xr = min(xs) if x_root is None else x_root
    xt = max(xs) if x_tip is None else x_tip
    out = []
    for sx in ((1, -1) if mirror else (1,)):
        bm = bmesh.new()
        tv, bv = [], []
        for x, y in planform:
            f = 0 if xt == xr else max(0.0, min(1.0, (abs(x) - xr) / (xt - xr)))
            t = t_root + (t_tip - t_root) * f
            z = zc + dihedral * (abs(x) - xr)
            tv.append(bm.verts.new((sx * x, y, z + t / 2)))
            bv.append(bm.verts.new((sx * x, y, z - t / 2)))
        ft = bm.faces.new(tv); ft.material_index = 0
        fb = bm.faces.new(bv[::-1]); fb.material_index = 1
        n = len(planform)
        for i in range(n):
            j = (i + 1) % n
            f = bm.faces.new((bv[i], bv[j], tv[j], tv[i])); f.material_index = 1
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        o = _obj_multi(f'{name}{sx}', bm, [top, side])
        if panels:
            inset_panels(o, 0.03, 0.006, lambda f: f.normal.z > 0.5 and f.calc_area() > 0.03)
        if bevel: add_bevel(o, bevel, 1)
        out.append(o)
    return out


def ring(name, c, r_out, r_in, h, paint, top=None, verts=20, bevel=0.008):
    """A flat annulus (duct, turret race) centred at c, axis Z, height h."""
    bm = bmesh.new()
    cx, cy, cz = c
    def circ(r, z):
        return [bm.verts.new((cx + math.cos(2 * math.pi * i / verts) * r, cy + math.sin(2 * math.pi * i / verts) * r, z)) for i in range(verts)]
    ob, ot, ib, it = circ(r_out, cz - h / 2), circ(r_out, cz + h / 2), circ(r_in, cz - h / 2), circ(r_in, cz + h / 2)
    for i in range(verts):
        j = (i + 1) % verts
        bm.faces.new((ob[i], ob[j], ot[j], ot[i])).material_index = 0
        bm.faces.new((ib[j], ib[i], it[i], it[j])).material_index = 0
        bm.faces.new((ot[i], ot[j], it[j], it[i])).material_index = 1 if top else 0
        bm.faces.new((ib[i], ib[j], ob[j], ob[i])).material_index = 0
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = _obj_multi(name, bm, [paint, top] if top else [paint])
    if bevel: add_bevel(o, bevel, 1)
    return o


def xform(objs, pivot=(0, 0, 0), rot=(0, 0, 0), move=(0, 0, 0), scale=1.0):
    """Rotates (XYZ Euler, radians), scales about pivot, then moves - baked into the mesh data."""
    p = Vector(pivot)
    M = Matrix.Translation(p + Vector(move)) @ Euler(rot, 'XYZ').to_matrix().to_4x4() @ Matrix.Scale(scale, 4) @ Matrix.Translation(-p)
    for o in objs:
        o.data.transform(M)
    return objs


def mirror_copy(objs):
    """Copies objects mirrored across x = 0 (keeps the modifiers)."""
    out = []
    for o in objs:
        c = o.copy(); c.data = o.data.copy()
        bpy.context.scene.collection.objects.link(c)
        c.data.transform(Matrix.Scale(-1, 4, (1, 0, 0)))
        c.data.flip_normals()
        out.append(c)
    return out


def thruster(name, front, back, r, mech='mech', inner='hull_dark', glow='glow', verts=12):
    """Engine nozzle from front to back: a flared can, a dark liner and a glowing exhaust disc."""
    f, b = Vector(front), Vector(back)
    d = (b - f).normalized()
    parts = [cyl(name + 'can', f, b, r * 0.85, mech, verts, r2=r, bevel=0.006)]
    parts.append(cyl(name + 'lip', b - d * 0.03, b + d * 0.005, r * 1.06, 'metal', verts, bevel=0))
    parts.append(cyl(name + 'glow', b - d * 0.02, b + d * 0.012, r * 0.78, glow, verts, bevel=0))
    return parts


def missile_pod(name, centre, size, pitch_deg, cols, rows, body, tip='accent', yaw=0.0, back=False):
    """A box launcher pitched up, with warhead tips out of its front (+y) face."""
    sx_, sy_, sz_ = size
    parts = [box(name + 'b', (0, 0, 0), size, body, 0.012, 1),
             box(name + 'f', (0, sy_ / 2 + 0.004, 0), (sx_ * 0.9, 0.01, sz_ * 0.88), 'mech', 0, 1)]
    cw, rh = sx_ / cols, sz_ / rows
    rad = min(cw, rh) * 0.3
    for c in range(cols):
        for r in range(rows):
            x = -sx_ / 2 + cw * (c + 0.5); z = -sz_ / 2 + rh * (r + 0.5)
            parts.append(cyl(f'{name}t{c}{r}', (x, sy_ / 2 - 0.01, z), (x, sy_ / 2 + rad * 2.4, z), rad, tip, 8, r2=0.002, bevel=0))
    xform(parts, (0, 0, 0), (math.radians(pitch_deg), 0, yaw + (math.pi if back else 0)), centre)
    return parts


def nano_tip(name, a, b, r, glow='glow'):
    """A nanolathe emitter nozzle from a to b ending in a glowing bead."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    return [cyl(name + 'n', a, b, r, 'metal', 10, r2=r * 0.7, bevel=0.004),
            cyl(name + 'c', b - d * r * 0.6, b + d * r * 0.2, r * 1.25, 'mech', 10, bevel=0),
            sphere(name + 'g', b + d * r * 0.9, r * 0.95, glow, 10, 6)]


def barrel(name, a, b, r, brake=True, paint='metal', verts=12):
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    p = [cyl(name, a, b, r, paint, verts, r2=r * 0.88, bevel=0.004)]
    if brake:
        p.append(cyl(name + 'mb', b - d * r * 2.6, b + d * r * 0.2, r * 1.45, 'mech', verts, r2=r * 1.35, bevel=0.004))
    return p


def track(prefix, x0, x1, y0, y1, r_end, zc, wheels, cleats, wheel_r=None):
    """Anvil-style track run between x0 and x1: a stadium band, cleats, road wheels, sprocket and idler.
    zc is the height of the end-wheel centres; the band reaches z = zc - r_end at the bottom."""
    parts = []
    xc = (x0 + x1) / 2
    pts = []
    for i in range(10):
        a = -math.pi / 2 + math.pi * i / 9
        pts.append((y1 - r_end + math.cos(a) * r_end, zc + math.sin(a) * r_end))
    for i in range(10):
        a = math.pi / 2 + math.pi * i / 9
        pts.append((y0 + r_end + math.cos(a) * r_end, zc + math.sin(a) * r_end))
    parts.append(prism_x(prefix + 'band', pts, x0, x1, 'tread', bevel=0.008, segs=1))
    zb, zt = zc - r_end, zc + r_end
    for i in range(cleats):
        y = y0 + r_end + (y1 - y0 - 2 * r_end) * i / (cleats - 1)
        parts.append(box(f'{prefix}cb{i}', (xc, y, zb - 0.005), (x1 - x0 + 0.02, 0.045, 0.028), 'mech', 0.005, 1))
        parts.append(box(f'{prefix}ct{i}', (xc, y, zt + 0.005), (x1 - x0 + 0.02, 0.045, 0.028), 'mech', 0.005, 1))
    wr = wheel_r or r_end * 0.68
    for i in range(wheels):
        y = y0 + r_end * 1.4 + (y1 - y0 - r_end * 2.8) * i / max(1, wheels - 1)
        parts.append(cyl(f'{prefix}w{i}', (x0 - 0.01, y, zb + wr + 0.02), (x1 + 0.01, y, zb + wr + 0.02), wr, 'metal', 12, bevel=0.01))
        for xx in (x0 - 0.02, x1 + 0.02):
            parts.append(cyl(f'{prefix}h{i}{xx}', (xx - 0.015, y, zb + wr + 0.02), (xx + 0.015, y, zb + wr + 0.02), wr * 0.42, 'mech', 8, bevel=0))
    for y in (y1 - r_end, y0 + r_end):
        parts.append(cyl(f'{prefix}e{y}', (x0 - 0.015, y, zc), (x1 + 0.015, y, zc), r_end * 0.85, 'mech', 12, bevel=0.008))
    return parts


def ship_hull(faction, hw, deck, keel, bow=1.2, stern=-1.15, deck_paint='hull_dark', stripe='team'):
    """A ship hull from bow (+y) to stern, deck at `deck`, keel at `keel` (< 0, below the waterline).
    Lattice: a needle bow swept into a sleek hull. Crucible: a blunt, slab-sided riveted hull.
    A team stripe runs along the shoulder; a dark boot-top band marks the waterline."""
    parts = []
    if faction == 'lattice':
        secs = [(bow, 0.015, 0.012, deck * 0.55, deck + 0.05, deck + 0.065),
                (bow - 0.18, hw * 0.34, hw * 0.3, keel * 0.5, deck * 0.62, deck + 0.04),
                (bow - 0.42, hw * 0.7, hw * 0.62, keel, deck * 0.6, deck + 0.02),
                (bow - 0.75, hw * 0.92, hw * 0.83, keel, deck * 0.6, deck + 0.005),
                (bow - 1.05, hw, hw * 0.9, keel, deck * 0.6, deck),
                (stern + 0.25, hw, hw * 0.9, keel, deck * 0.6, deck),
                (stern + 0.04, hw * 0.88, hw * 0.78, keel * 0.45, deck * 0.6, deck),
                (stern, hw * 0.84, hw * 0.74, keel * 0.2, deck * 0.62, deck - 0.005)]
    else:
        secs = [(bow, hw * 0.42, hw * 0.36, keel * 0.3, deck + 0.02, deck + 0.06),
                (bow - 0.12, hw * 0.74, hw * 0.66, keel * 0.75, deck * 0.7, deck + 0.04),
                (bow - 0.32, hw * 0.94, hw * 0.87, keel, deck * 0.66, deck + 0.015),
                (stern + 0.12, hw, hw * 0.92, keel, deck * 0.66, deck),
                (stern, hw, hw * 0.92, keel * 0.6, deck * 0.66, deck)]
    rings = [hexring(y, a, b, z0, z1, z2) for y, a, b, z0, z1, z2 in secs]
    paints = {0: 'mech', 1: 'hull', 5: 'hull', 2: stripe, 4: stripe, 3: deck_paint}
    h = loft('hull', rings, 'hull', lambda j: paints[j], cap0='hull', cap1='hull_dark', bevel=0.01)
    parts.append(h)
    return parts, secs


def half_width_at(secs, y):
    """Hull half-width (at the shoulder) at station y, from ship_hull's sections."""
    s = sorted(secs, key=lambda t: t[0])
    if y <= s[0][0]: return s[0][1]
    for a, b in zip(s, s[1:]):
        if a[0] <= y <= b[0]:
            t = (y - a[0]) / (b[0] - a[0])
            return a[1] + (b[1] - a[1]) * t
    return s[-1][1]


def finish(uid, parts, size=512):
    """parts: list of (name, objs, pivot or None). Joins, paints, unwraps, bakes and exports."""
    out = out_dir()
    os.makedirs(out, exist_ok=True)
    joined = []
    for name, objs, pivot in parts:
        joined.append(kit.join(name, objs, parent_origin=pivot))
    for o in joined:
        kit.paint_vertex_colours(o)
        kit.unwrap(o)
    for o in joined:
        suffix = 'body' if o.name == 'Body' else 'turret' if o.name == 'Turret' else 'rotor'
        kit.bake_detail(o, os.path.join(out, f'{uid}_{suffix}.png'), size)
    kit.export(os.path.join(out, f'{uid}.glb'), joined)
    tris = 0
    for o in joined:
        n = sum(len(p.vertices) - 2 for p in o.data.polygons)
        print(f'part {o.name}: {n} tris')
        tris += n
    print('tris', tris)


def beam(name, a, b, w, h, paint, bevel=0.012, taper=1.0, up=(0, 0, 1)):
    """A rectangular strut from a to b, w wide (sideways) and h deep; taper scales the b end."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    side = d.cross(Vector(up))
    if side.length < 1e-4: side = Vector((1, 0, 0))
    side.normalize()
    u = side.cross(d).normalized()
    pts = []
    for p, s in ((a, 1.0), (b, taper)):
        for i in (-1, 1):
            for j in (-1, 1):
                pts.append(tuple(p + side * (i * w * s / 2) + u * (j * h * s / 2)))
    return hull_from(name, pts, paint, bevel=bevel, segs=1)
