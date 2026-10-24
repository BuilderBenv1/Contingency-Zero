"""Shared helpers for the economy / defence / intel structures (both factions).

Adds a few shapes kit.py lacks (oriented beams, rings, n-gon prisms, dishes, trusses, rails,
smokestacks, pipes) and `finish`, which paints, unwraps, bakes and exports a structure.

Baking note: kit.bake_detail's AO distance (0.6), worn-edge radius (0.03) and grime noise are
tuned for mobile units authored at radius 1 (~2.4 across). Structures are authored in world
units (8 to 23 across), so `finish` shrinks every part's mesh by `bake_scale` while baking and
restores it before export. That keeps edge wear, occlusion and grime at the same apparent scale
as the units. Set FAST=1 in the environment to bake tiny maps while iterating.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import bpy, bmesh
from mathutils import Vector, Matrix
import kit
from kit import box, hull_from, cyl, sphere, add_bevel, _obj

FAST = bool(os.environ.get('FAST'))


def out_dir():
    return sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'


def _frame(d):
    """Two unit vectors perpendicular to direction d (side, up)."""
    d = d.normalized()
    ref = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((0, 1, 0))
    side = d.cross(ref).normalized()
    up = side.cross(d).normalized()
    return side, up


def beam(name, a, b, w, h, paint, bevel=0.03, segs=1, w2=None, h2=None):
    """A rectangular girder from a to b (w across, h 'up'); w2/h2 taper it towards b."""
    a, b = Vector(a), Vector(b)
    side, up = _frame(b - a)
    w2 = w if w2 is None else w2
    h2 = h if h2 is None else h2
    pts = []
    for p, ww, hh in ((a, w, h), (b, w2, h2)):
        for sx in (-1, 1):
            for sz in (-1, 1):
                pts.append(p + side * sx * ww / 2 + up * sz * hh / 2)
    return hull_from(name, [tuple(p) for p in pts], paint, bevel, segs)


def ngon(name, c, r0, r1, z0, z1, n, paint, phase=None, bevel=0.03, segs=1):
    """An n-sided prism / frustum about the vertical through c, bottom radius r0 at z0, top r1 at z1."""
    phase = math.pi / n if phase is None else phase
    pts = []
    for r, z in ((r0, z0), (r1, z1)):
        for i in range(n):
            a = phase + i * 2 * math.pi / n
            pts.append((c[0] + math.cos(a) * r, c[1] + math.sin(a) * r, z))
    return hull_from(name, pts, paint, bevel, segs)


def ring(name, c, r_out, r_in, z0, z1, paint, n=24, phase=0.0, bevel=0.0, r_out2=None, r_in2=None):
    """A flat annulus (washer / band) around the vertical through c."""
    r_out2 = r_out if r_out2 is None else r_out2
    r_in2 = r_in if r_in2 is None else r_in2
    bm = bmesh.new()
    rows = []
    for r, z in ((r_out, z0), (r_out2, z1), (r_in2, z1), (r_in, z0)):
        rows.append([bm.verts.new((c[0] + math.cos(phase + i * 2 * math.pi / n) * r,
                                   c[1] + math.sin(phase + i * 2 * math.pi / n) * r, z)) for i in range(n)])
    for k in range(4):
        A, B = rows[k], rows[(k + 1) % 4]
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((A[i], A[j], B[j], B[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = _obj(name, bm, paint)
    if bevel: add_bevel(o, bevel, 1)
    for p in o.data.polygons: p.use_smooth = False
    return o


def oriented(o, origin, direction, roll=0.0):
    """Rotates a mesh authored about the local +Z axis (at the world origin) to point along direction from origin."""
    d = Vector(direction).normalized()
    q = d.to_track_quat('Z', 'Y')
    m = Matrix.Translation(Vector(origin)) @ q.to_matrix().to_4x4() @ Matrix.Rotation(roll, 4, 'Z')
    o.data.transform(m)
    return o


def dish(name, c, direction, r, depth, paint, thick=0.08, rings=5, segs=20, back=None):
    """A parabolic dish (opening along direction), centre of the rim plane at c + direction * depth."""
    bm = bmesh.new()
    centre = bm.verts.new((0, 0, 0))
    prev = None
    for j in range(1, rings + 1):
        rr = r * j / rings
        z = depth * (j / rings) ** 2
        row = [bm.verts.new((math.cos(i * 2 * math.pi / segs) * rr, math.sin(i * 2 * math.pi / segs) * rr, z)) for i in range(segs)]
        for i in range(segs):
            k = (i + 1) % segs
            if prev is None:
                bm.faces.new((centre, row[i], row[k]))
            else:
                bm.faces.new((prev[i], row[i], row[k], prev[k]))
        prev = row
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # Make normals face the opening (+Z).
    for f in bm.faces:
        if f.calc_center_median().z > 0 and f.normal.z < 0:
            f.normal_flip()
    o = _obj(name, bm, paint)
    sol = o.modifiers.new('solid', 'SOLIDIFY'); sol.thickness = thick; sol.offset = -1
    oriented(o, c, direction)
    return o


def disc(name, c, direction, r, paint, segs=16):
    """A flat disc (one polygon) facing along direction: lenses, lit faces."""
    bm = bmesh.new()
    vs = [bm.verts.new((math.cos(i * 2 * math.pi / segs) * r, math.sin(i * 2 * math.pi / segs) * r, 0)) for i in range(segs)]
    bm.faces.new(vs)
    o = _obj(name, bm, paint)
    oriented(o, c, direction)
    return o


def rod(name, a, b, r, paint, verts=8):
    return cyl(name, a, b, r, paint, verts, bevel=0)


def pipe(name, pts, r, paint, verts=8, elbows=True):
    """A pipe through a polyline, with ball elbows at the bends."""
    out = []
    for i in range(len(pts) - 1):
        out.append(cyl(f'{name}{i}', pts[i], pts[i + 1], r, paint, verts, bevel=0))
        if elbows and 0 < i + 1 < len(pts) - 1:
            out.append(sphere(f'{name}e{i}', pts[i + 1], r * 1.15, paint, verts, 4))
    return out


def truss(name, a, b, w, paint, bays=4, rail=0.08, brace=0.045, brace_paint=None, phase_up=None):
    """A square lattice girder from a to b, w wide: four rails with zig-zag bracing on each face."""
    a, b = Vector(a), Vector(b)
    side, up = _frame(b - a)
    if phase_up is not None:
        up = Vector(phase_up); side = (b - a).normalized().cross(up).normalized()
    brace_paint = brace_paint or paint
    corners = [side * sx * w / 2 + up * sz * w / 2 for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    out = []
    for k, cnr in enumerate(corners):
        out.append(beam(f'{name}r{k}', a + cnr, b + cnr, rail, rail, paint, 0.0))
    for k in range(4):
        c0, c1 = corners[k], corners[(k + 1) % 4]
        for i in range(bays):
            t0, t1 = i / bays, (i + 1) / bays
            p0 = a.lerp(b, t0) + (c0 if i % 2 == 0 else c1)
            p1 = a.lerp(b, t1) + (c1 if i % 2 == 0 else c0)
            out.append(beam(f'{name}b{k}_{i}', p0, p1, brace, brace, brace_paint, 0.0))
    return out


def tower_truss(name, base_half, top_half, z0, z1, paint, bays=4, leg=0.22, brace=0.09, brace_paint=None,
                rings=True):
    """A tapering four-legged lattice tower (pylon / derrick): legs at the corners, X bracing on all four faces."""
    brace_paint = brace_paint or paint
    out = []
    def corner(sx, sy, t):
        h = base_half + (top_half - base_half) * t
        return Vector((sx * h, sy * h, z0 + (z1 - z0) * t))
    cs = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    for k, (sx, sy) in enumerate(cs):
        out.append(beam(f'{name}leg{k}', corner(sx, sy, 0), corner(sx, sy, 1), leg, leg, paint, 0.02))
    for k in range(4):
        (ax, ay), (bx, by) = cs[k], cs[(k + 1) % 4]
        for i in range(bays):
            t0, t1 = i / bays, (i + 1) / bays
            out.append(beam(f'{name}x{k}_{i}a', corner(ax, ay, t0), corner(bx, by, t1), brace, brace, brace_paint, 0.0))
            out.append(beam(f'{name}x{k}_{i}b', corner(bx, by, t0), corner(ax, ay, t1), brace, brace, brace_paint, 0.0))
            if rings:
                out.append(beam(f'{name}h{k}_{i}', corner(ax, ay, t1), corner(bx, by, t1), brace * 1.3, brace * 1.3, paint, 0.0))
    return out


def rail(name, pts, h, paint, post_every=1, r=0.035, closed=False):
    """A guard rail along a polyline: posts and a top and mid rail."""
    out = []
    P = [Vector(p) for p in pts] + ([Vector(pts[0])] if closed else [])
    for i in range(len(P) - 1):
        a, b = P[i], P[i + 1]
        out.append(rod(f'{name}t{i}', a + Vector((0, 0, h)), b + Vector((0, 0, h)), r, paint, 5))
        out.append(rod(f'{name}m{i}', a + Vector((0, 0, h * 0.5)), b + Vector((0, 0, h * 0.5)), r * 0.8, paint, 5))
    for i, p in enumerate(P[:-1] if closed else P):
        if i % post_every == 0:
            out.append(rod(f'{name}p{i}', p, p + Vector((0, 0, h)), r, paint, 5))
    return out


def ladder(name, base, top, paint, w=0.4, rung=0.35, r=0.03, normal=(0, -1, 0)):
    """A vertical ladder from base up to top height, standing off a wall facing `normal`."""
    base = Vector(base); n = Vector(normal)
    side = n.cross(Vector((0, 0, 1))).normalized()
    out = []
    for s in (-1, 1):
        out.append(rod(f'{name}s{s}', base + side * s * w / 2, Vector((base.x, base.y, top)) + side * s * w / 2, r * 1.3, paint, 5))
    n_r = max(2, int((top - base.z) / rung))
    for i in range(1, n_r):
        z = base.z + i * (top - base.z) / n_r
        p = Vector((base.x, base.y, z))
        out.append(rod(f'{name}r{i}', p - side * w / 2, p + side * w / 2, r, paint, 4))
    return out


def stack(name, base, r, h, hot=True, band='team', taper=0.85):
    """Crucible smokestack: tapered pipe with a lip, a painted band and a (glowing) mouth."""
    x, y, z = base
    out = [cyl(f'{name}p', (x, y, z), (x, y, z + h), r, 'metal', 12, r2=r * taper, bevel=0.02),
           cyl(f'{name}lip', (x, y, z + h - 0.02), (x, y, z + h + r * 0.25), r * taper * 1.15, 'mech', 12, bevel=0.01),
           cyl(f'{name}mouth', (x, y, z + h + r * 0.25), (x, y, z + h + r * 0.27), r * taper * 0.85, 'glow' if hot else 'tread', 12, bevel=0),
           cyl(f'{name}band', (x, y, z + h * 0.55), (x, y, z + h * 0.55 + r * 0.6), r * 1.08, band, 12, bevel=0.01),
           cyl(f'{name}foot', (x, y, z), (x, y, z + r * 0.5), r * 1.3, 'hull_dark', 12, bevel=0.02)]
    return out


def rivet_ring(name, c, rr, z, n, paint='metal', r=0.06):
    return [sphere(f'{name}{i}', (c[0] + math.cos(i * 2 * math.pi / n) * rr, c[1] + math.sin(i * 2 * math.pi / n) * rr, z),
                   r, paint, 6, 3) for i in range(n)]


def rivet_line(name, a, b, n, paint='metal', r=0.06):
    a, b = Vector(a), Vector(b)
    return [sphere(f'{name}{i}', tuple(a.lerp(b, i / (n - 1) if n > 1 else 0.5)), r, paint, 6, 3) for i in range(n)]


def tris(objs):
    return sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)


def _part_name(o):
    n = o.name.lower()
    return 'body' if n.startswith('body') else 'turret' if n.startswith('turret') else 'rotor'


def finish(uid, parts, size=512, bake_scale=0.5, sizes=None):
    """Paints, unwraps, bakes (at a shrunken scale, see module doc) and exports a structure's parts."""
    out = out_dir()
    os.makedirs(out, exist_ok=True)
    for o in parts:
        kit.paint_vertex_colours(o)
        kit.unwrap(o)
    k = bake_scale
    for o in parts:
        o.data.transform(Matrix.Scale(k, 4))
        o.location = o.location * k
    for o in parts:
        sz = (sizes or {}).get(_part_name(o), size)
        if FAST: sz = min(sz, 128)
        kit.bake_detail(o, os.path.join(out, f'{uid}_{_part_name(o)}.png'), sz, others=[p for p in parts if p is not o])
    for o in parts:
        o.data.transform(Matrix.Scale(1 / k, 4))
        o.location = o.location / k
    kit.export(os.path.join(out, f'{uid}.glb'), parts)
    print(uid, 'tris', tris(parts), {o.name: tris([o]) for o in parts})
