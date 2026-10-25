"""Shared helpers for the battlefield's nature and debris models (trees, rocks, wrecks, rubble, props).

Everything is authored in world units, centred on the origin, ground at z = 0, with bases sunk a
little below 0 so the models sit in uneven ground. No team colour: every paint has alpha 1, or 0.5
for the ember / lava glows.

kit's PAINTS are faction colours; `reset()` adds the natural paints below (display-space colours
picked from game/textures/<theme>_*.png so props sit IN the ground) with matching materials.

Shapes: faceted convex-hull lumps (rocks, canopies, snow), tubes along bent paths (trunks,
branches, cacti), star-skirted conifer tiers, crystals, crack strips. Faces are painted by how
they face the light (top faces lighter, undersides darker), so low-poly planes read clearly from
the RTS camera.

`finish` joins, paints, unwraps, bakes (with a temporary ground plane so bases pick up contact
occlusion, and the mesh scaled by `bake_scale` while baking so wear and occlusion read at the
same apparent size as on the units) and exports a single "Body" object.
"""
import math, os, sys, random
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import bpy, bmesh
from mathutils import Vector, Matrix
import kit

TAU = math.tau
FAST = bool(os.environ.get('FAST'))

# Display-space colours (the game's Compatibility renderer uses vertex colours as authored).
NATURE = {
    # temperate
    't_leaf_dk':  (0.12, 0.21, 0.08, 1), 't_leaf':   (0.21, 0.32, 0.11, 1), 't_leaf_lt':  (0.34, 0.44, 0.16, 1),
    't_pine_dk':  (0.07, 0.15, 0.09, 1), 't_pine':   (0.12, 0.23, 0.12, 1), 't_pine_lt':  (0.21, 0.33, 0.16, 1),
    't_grass':    (0.30, 0.33, 0.13, 1), 't_grass_lt': (0.42, 0.44, 0.18, 1), 't_grass_dk': (0.20, 0.23, 0.09, 1),
    't_rock_lt':  (0.46, 0.45, 0.41, 1), 't_rock':   (0.35, 0.34, 0.31, 1), 't_rock_dk':  (0.24, 0.23, 0.21, 1),
    't_moss':     (0.27, 0.31, 0.12, 1), 't_soil':   (0.27, 0.22, 0.16, 1),
    'bark':       (0.29, 0.21, 0.14, 1), 'bark_dk':  (0.18, 0.13, 0.09, 1), 'bark_red':   (0.42, 0.26, 0.16, 1),
    # desert
    'd_sand':     (0.72, 0.57, 0.38, 1), 'd_sand_dk': (0.60, 0.46, 0.30, 1),
    'd_rock_lt':  (0.66, 0.45, 0.30, 1), 'd_rock':   (0.53, 0.34, 0.22, 1), 'd_rock_dk':  (0.38, 0.23, 0.15, 1),
    'd_wood_lt':  (0.62, 0.55, 0.45, 1), 'd_wood':   (0.47, 0.39, 0.30, 1), 'd_wood_dk':  (0.30, 0.24, 0.18, 1),
    'd_cactus_lt': (0.44, 0.50, 0.28, 1), 'd_cactus': (0.32, 0.40, 0.21, 1), 'd_cactus_dk': (0.21, 0.28, 0.14, 1),
    'd_frond_lt': (0.52, 0.52, 0.26, 1), 'd_frond':  (0.38, 0.40, 0.19, 1), 'd_frond_dry': (0.60, 0.50, 0.31, 1),
    'd_scrub':    (0.46, 0.42, 0.26, 1), 'd_scrub_dk': (0.33, 0.29, 0.18, 1), 'd_flower': (0.80, 0.42, 0.30, 1),
    # arctic
    'snow':       (0.90, 0.91, 0.90, 1), 'snow_sh':  (0.72, 0.76, 0.82, 1),
    'a_rock_lt':  (0.56, 0.59, 0.64, 1), 'a_rock':   (0.42, 0.46, 0.52, 1), 'a_rock_dk':  (0.28, 0.31, 0.37, 1),
    'a_pine_dk':  (0.07, 0.15, 0.14, 1), 'a_pine':   (0.12, 0.23, 0.21, 1), 'a_pine_lt':  (0.20, 0.32, 0.29, 1),
    'frost':      (0.66, 0.74, 0.76, 1), 'ice':      (0.56, 0.73, 0.82, 1), 'ice_lt':     (0.80, 0.90, 0.95, 1),
    'a_grass':    (0.52, 0.52, 0.42, 1), 'a_grass_dk': (0.38, 0.38, 0.30, 1), 'a_twig':  (0.30, 0.25, 0.21, 1),
    # lava
    'l_basalt_lt': (0.30, 0.29, 0.28, 1), 'l_basalt': (0.19, 0.18, 0.18, 1), 'l_basalt_dk': (0.11, 0.105, 0.10, 1),
    'l_ash':      (0.36, 0.34, 0.32, 1), 'l_ash_dk':  (0.25, 0.235, 0.22, 1),
    'l_char':     (0.085, 0.075, 0.07, 1), 'l_char_lt': (0.17, 0.15, 0.13, 1),
    'ember':      (1.00, 0.36, 0.05, 0.5), 'ember_dim': (0.80, 0.20, 0.03, 0.5),
    # metal ore crystals (what builders reclaim rocks for)
    'ore_lt':     (0.74, 0.81, 0.90, 1), 'ore':      (0.50, 0.58, 0.69, 1), 'ore_dk':     (0.30, 0.36, 0.45, 1),
    # wrecks and rubble
    'w_scorch':   (0.12, 0.11, 0.10, 1), 'w_soot':   (0.075, 0.07, 0.065, 1),
    'w_steel_lt': (0.46, 0.45, 0.42, 1), 'w_steel':  (0.34, 0.33, 0.31, 1), 'w_steel_dk': (0.21, 0.20, 0.19, 1),
    'w_rust':     (0.42, 0.25, 0.14, 1), 'w_rust_dk': (0.29, 0.17, 0.10, 1),
    'w_tread':    (0.10, 0.09, 0.085, 1), 'w_paint':  (0.30, 0.31, 0.26, 1),
    'c_lt':       (0.52, 0.50, 0.46, 1), 'c_mid':    (0.42, 0.41, 0.38, 1), 'c_dk':       (0.30, 0.29, 0.27, 1),
    'c_dust':     (0.47, 0.44, 0.38, 1),
}

# Light used to pick light / mid / dark per face: mostly from above, a little from the south-west
# (the RTS camera looks north), so ridges split into a lit and a shaded plane.
L = Vector((-0.35, -0.45, 0.82)).normalized()


def out_dir():
    args = sys.argv[1:]
    if '--only' in args:
        i = args.index('--only'); del args[i:i + 2]
    return args[0] if args else '.'


def reset():
    kit.reset()
    kit.PAINTS.update(NATURE)
    for name in NATURE:
        m = bpy.data.materials.new(name)
        m.diffuse_color = NATURE[name][:3] + (1.0,)


# ---- Face painting ---------------------------------------------------------------------------

def shade(lt, mid, dk, rng=None, bias=0.0, spread=0.12, under=None):
    """A face-paint function: lit planes take lt, sides mid, shaded / undersides dk."""
    r = rng or random.Random(1)

    def f(face):
        n = face.normal
        if under and n.z < -0.25:
            return under
        k = n.dot(L) + bias + r.uniform(-spread, spread)
        return lt if k > 0.62 else mid if k > 0.18 else dk
    return f


def with_top(base, top, nz=0.62, zmin=-1e9, rng=None, chance=1.0):
    """Wraps a face-paint function so up-facing faces above zmin take `top` (snow / sand / ash)."""
    r = rng or random.Random(2)

    def f(face):
        c = face.calc_center_median()
        if face.normal.z > nz and c.z > zmin and r.random() < chance:
            return top
        return base(face)
    return f


# ---- Object construction --------------------------------------------------------------------

def from_bm(name, bm, paint):
    """bmesh -> object, painting each face with paint (a name, or a function of the face)."""
    bm.normal_update()
    names, idx = [], {}
    for f in bm.faces:
        p = paint if isinstance(paint, str) else paint(f)
        if p not in idx:
            idx[p] = len(names); names.append(p)
        f.material_index = idx[p]
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    for p in names:
        me.materials.append(bpy.data.materials[p])
    return o


def _clean_hull(bm, ret, floor=None, planar=0.02):
    dead = {g for g in ret['geom_interior'] + ret['geom_unused'] if isinstance(g, bmesh.types.BMVert)}
    bmesh.ops.delete(bm, geom=list(dead), context='VERTS')
    if planar:
        bmesh.ops.dissolve_limit(bm, angle_limit=planar, verts=bm.verts, edges=bm.edges)
    if floor is not None:
        dead = [f for f in bm.faces if all(v.co.z <= floor + 1e-4 for v in f.verts)]
        bmesh.ops.delete(bm, geom=dead, context='FACES_ONLY')
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.normal_update()


def hull_bm(points, floor=None, planar=0.02):
    bm = bmesh.new()
    vs = [bm.verts.new(Vector(p)) for p in points]
    ret = bmesh.ops.convex_hull(bm, input=vs)
    _clean_hull(bm, ret, floor, planar)
    return bm


def blob_points(rng, c, rx, ry, rz, n=16, jit=0.18, floor=None, rot=0.0, flat_top=None):
    """Points scattered over an ellipsoid (a Fibonacci sphere, jittered): feed to hull_bm."""
    pts = []
    ga = math.pi * (3 - math.sqrt(5))
    off = rng.random() * TAU
    for i in range(n):
        u = (i + 0.5) / n
        z = 1 - 2 * u
        r = math.sqrt(max(0.0, 1 - z * z))
        a = off + i * ga + rng.uniform(-0.25, 0.25)
        s = 1 + rng.uniform(-jit, jit)
        x, y = math.cos(a) * r * rx * s, math.sin(a) * r * ry * s
        x, y = x * math.cos(rot) - y * math.sin(rot), x * math.sin(rot) + y * math.cos(rot)
        zz = z * rz * s
        if flat_top is not None:
            zz = min(zz, flat_top * rz)
        p = [c[0] + x, c[1] + y, c[2] + zz]
        if floor is not None:
            p[2] = max(p[2], floor)
        pts.append(p)
    return pts


def lump(name, rng, c, rx, ry, rz, paint, n=16, jit=0.18, floor=None, rot=0.0, flat_top=None):
    """A faceted lump (rock, canopy clump, snow drift); floor cuts it flat and drops the base faces."""
    return from_bm(name, hull_bm(blob_points(rng, c, rx, ry, rz, n, jit, floor, rot, flat_top), floor), paint)


def _frames(pts):
    """Parallel-transported (side, up) frames along a polyline."""
    out = []
    side = None
    for i, p in enumerate(pts):
        a = pts[max(0, i - 1)]; b = pts[min(len(pts) - 1, i + 1)]
        t = (b - a).normalized()
        if side is None:
            ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
            side = t.cross(ref).normalized()
        else:
            side = (side - t * side.dot(t)).normalized()
        out.append((t, side, t.cross(side).normalized()))
    return out


def tube_bm(pts, radii, sides=6, star=0.0, tip=None, cap=True, phase=0.0, jag=None, rng=None, bm=None):
    """A tube along pts with radius radii[i]. star > 0 grooves every other vertex (cactus ribs).
    tip: a point to close the end to (a cone); otherwise an end cap. jag: (rng, height) makes a
    snapped, splintered end instead."""
    bm = bm or bmesh.new()
    pts = [Vector(p) for p in pts]
    rings = []
    for i, (p, (t, side, up)) in enumerate(zip(pts, _frames(pts))):
        ring = []
        for k in range(sides):
            a = phase + k * TAU / sides
            rr = radii[i] * (1 - star * (k % 2))
            v = p + (side * math.cos(a) + up * math.sin(a)) * rr
            if jag and i == len(pts) - 1:
                v = v + t * jag[0].uniform(0, jag[1]) * (1.0 if k % 2 == 0 else 0.35)
            ring.append(bm.verts.new(v))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for k in range(sides):
            k2 = (k + 1) % sides
            bm.faces.new((rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]))
    if tip is not None:
        tv = bm.verts.new(Vector(tip))
        for k in range(sides):
            bm.faces.new((rings[-1][k], rings[-1][(k + 1) % sides], tv))
    elif jag:
        cv = bm.verts.new(pts[-1] - _frames(pts)[-1][0] * radii[-1] * 0.4)
        for k in range(sides):
            bm.faces.new((rings[-1][(k + 1) % sides], rings[-1][k], cv))
    elif cap:
        bm.faces.new(rings[-1])
    bm.normal_update()
    return bm


def tube(name, pts, radii, paint, **kw):
    return from_bm(name, tube_bm(pts, radii, **kw), paint)


def bend_path(rng, a, d, length, n, wander=0.25, up_pull=0.0):
    """A wandering polyline from a, heading d, n segments: gnarled trunks and branches."""
    a = Vector(a); d = Vector(d).normalized()
    pts = [a.copy()]
    seg = length / n
    for i in range(n):
        d = (d + Vector((rng.uniform(-wander, wander), rng.uniform(-wander, wander), rng.uniform(-wander, wander) * 0.5 + up_pull))).normalized()
        a = a + d * seg
        pts.append(a.copy())
    return pts


def conifer_tier(name, rng, c, r, h, paint, n=9, droop=0.35, inner=0.6, mid_r=0.52, mid_h=0.42, tilt=(0, 0), cap=None, cap_r=0.78, cap_lift=0.07):
    """One star-skirted conifer tier: apex, a ridge ring, and a rim of drooping tips and notches.
    Ridges run from each tip up to the apex, so every tip shows a lit and a shaded plane.
    cap: a paint (snow / frost) for a layer lying on the tier's upper surface out to cap_r of the
    way to the tips, so needles fringe it all round."""
    bm = bmesh.new()
    cx, cy, cz = c
    ph = rng.random() * TAU
    apex = bm.verts.new((cx + tilt[0] * h, cy + tilt[1] * h, cz + h))
    mid, rim = [], []
    for i in range(n):
        a = ph + i * TAU / n + rng.uniform(-0.1, 0.1)
        rr = r * mid_r * rng.uniform(0.9, 1.1)
        mid.append(bm.verts.new((cx + math.cos(a) * rr + tilt[0] * h * mid_h, cy + math.sin(a) * rr + tilt[1] * h * mid_h, cz + h * mid_h)))
    for i in range(2 * n):
        a = ph + i * math.pi / n + rng.uniform(-0.06, 0.06)
        if i % 2 == 0:
            rr = r * rng.uniform(0.85, 1.12); z = cz - droop * rng.uniform(0.6, 1.3)
        else:
            rr = r * inner * rng.uniform(0.9, 1.1); z = cz + droop * 0.25
        rim.append(bm.verts.new((cx + math.cos(a) * rr, cy + math.sin(a) * rr, z)))
    under = bm.verts.new((cx, cy, cz + h * 0.18))
    apex_co = apex.co.copy(); mid_co = [v.co.copy() for v in mid]; rim_co = [v.co.copy() for v in rim]
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((apex, mid[i], mid[j]))
        bm.faces.new((mid[i], rim[2 * i], rim[2 * i + 1]))
        bm.faces.new((mid[i], rim[2 * i + 1], mid[j]))
        bm.faces.new((mid[j], rim[2 * i + 1], rim[(2 * i + 2) % (2 * n)]))
    for i in range(2 * n):
        bm.faces.new((rim[(i + 1) % (2 * n)], rim[i], under))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = from_bm(name, bm, paint)
    if not cap:
        return o
    # The cap: the same surface shrunk towards the apex and lifted, with ragged edges.
    cb = bmesh.new()
    up = Vector((0, 0, cap_lift))
    ap = Vector(apex_co) + up * 1.5
    def toward(v, k):
        return ap + (Vector(v) + up - ap) * k
    cm = [cb.verts.new(toward(v, 1.0)) for v in mid_co]
    cr = []
    for i, v in enumerate(rim_co):
        m = Vector(mid_co[i // 2]) if i % 2 == 0 else (Vector(mid_co[i // 2]) + Vector(mid_co[(i // 2 + 1) % n])) / 2
        k = cap_r * rng.uniform(0.8, 1.15) if i % 2 == 0 else min(0.95, cap_r * 1.2) * rng.uniform(0.9, 1.05)
        cr.append(cb.verts.new(m + up + (Vector(v) - m) * k))
    ca = cb.verts.new(ap)
    for i in range(n):
        j = (i + 1) % n
        cb.faces.new((ca, cm[i], cm[j]))
        cb.faces.new((cm[i], cr[2 * i], cr[2 * i + 1]))
        cb.faces.new((cm[i], cr[2 * i + 1], cm[j]))
        cb.faces.new((cm[j], cr[2 * i + 1], cr[(2 * i + 2) % (2 * n)]))
    cb.normal_update()
    for f in cb.faces:
        if f.normal.z < 0: f.normal_flip()
    cb.normal_update()
    return [o, from_bm(name + '_cap', cb, cap)]


def crystal_bm(bm, base, d, length, r, sides=6, rng=None):
    """A pointed hexagonal ore crystal growing from base along d (base buried, no cap)."""
    d = Vector(d).normalized()
    base = Vector(base)
    t, side, up = _frames([base, base + d])[0]
    ph = (rng.random() if rng else 0) * TAU
    r0, r1 = [], []
    for k in range(sides):
        a = ph + k * TAU / sides
        off = (side * math.cos(a) + up * math.sin(a)) * r
        r0.append(bm.verts.new(base + off))
        r1.append(bm.verts.new(base + d * length * 0.72 + off * 0.92))
    tip = bm.verts.new(base + d * length)
    for k in range(sides):
        k2 = (k + 1) % sides
        bm.faces.new((r0[k], r0[k2], r1[k2], r1[k]))
        bm.faces.new((r1[k], r1[k2], tip))
    return bm


def ore_cluster(name, rng, c, n=3, size=1.0, out=None):
    """A few metal-ore crystals poking out of the rock: the cue that builders reclaim it for metal."""
    bm = bmesh.new()
    c = Vector(c)
    outward = Vector(out).normalized() if out is not None else Vector((0, 0, 1))
    for i in range(n):
        d = (outward + Vector((rng.uniform(-0.55, 0.55), rng.uniform(-0.55, 0.55), rng.uniform(0.2, 0.7)))).normalized()
        L0 = size * rng.uniform(0.55, 1.0) * (1.0 if i == 0 else 0.75)
        r = size * rng.uniform(0.11, 0.16) * (1.0 if i == 0 else 0.8)
        b = c + Vector((rng.uniform(-0.15, 0.15), rng.uniform(-0.15, 0.15), -0.15)) * size
        crystal_bm(bm, b, d, L0, r, 6, rng)
    bm.normal_update()
    # Facets alternate bright and dull, like cut metal catching the light.
    return from_bm(name, bm, shade('ore_lt', 'ore', 'ore_dk', rng, bias=0.1, spread=0.2))


def strip_bm(bm, pts, normals, width, lift=0.012):
    """A thin ribbon laid along pts on a surface (normals given per point): glowing cracks."""
    pts = [Vector(p) for p in pts]
    L_, R_ = [], []
    for i, p in enumerate(pts):
        a = pts[max(0, i - 1)]; b = pts[min(len(pts) - 1, i + 1)]
        t = (b - a).normalized()
        n = Vector(normals[i]).normalized()
        s = t.cross(n).normalized() * width * (0.5 if i in (0, len(pts) - 1) else 1.0)
        q = p + n * lift
        L_.append(bm.verts.new(q - s)); R_.append(bm.verts.new(q + s))
    for i in range(len(pts) - 1):
        bm.faces.new((L_[i], R_[i], R_[i + 1], L_[i + 1]))
    bm.normal_update()
    return bm


def surface_cracks(name, rock_obj, rng, count, width=0.06, zmin=0.05, paint='ember', steps=4):
    """Glowing cracks wandering over a rock's surface: random walks along its edges, kept above zmin."""
    me = rock_obj.data
    bm0 = bmesh.new(); bm0.from_mesh(me); bm0.verts.ensure_lookup_table(); bm0.normal_update()
    out = bmesh.new()
    cand = [v for v in bm0.verts if v.co.z > zmin + 0.1 and v.normal.z < 0.9]
    if not cand:
        bm0.free(); return None
    for i in range(count):
        v = rng.choice(cand)
        path = [v]
        prev = None
        for s in range(steps):
            nxt = [e.other_vert(v) for e in v.link_edges if e.other_vert(v) is not prev and e.other_vert(v).co.z > zmin]
            if not nxt: break
            prev, v = v, rng.choice(nxt)
            path.append(v)
        if len(path) < 2: continue
        pts, nrm = [], []
        for a, b in zip(path, path[1:]):
            for t in (0.0, 0.5):
                p = a.co.lerp(b.co, t)
                n = a.normal.lerp(b.normal, t)
                if t == 0.5:
                    sd = (b.co - a.co).cross(n).normalized()
                    p = p + sd * rng.uniform(-0.08, 0.08) * (b.co - a.co).length
                pts.append(p); nrm.append(n)
        pts.append(path[-1].co.copy()); nrm.append(path[-1].normal.copy())
        strip_bm(out, pts, nrm, width)
    bm0.free()
    if not out.faces:
        out.free(); return None
    return from_bm(name, out, paint)


def cut_lines(bm, co, no, zmin=0.0, keep=1.0, rng=None):
    """Bisects bm by a plane (splitting its faces, so the cut can be painted / traced) and returns
    the cut as a list of (a, b, normal) segments above zmin; keep < 1 drops some at random."""
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    r = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=Vector(co), plane_no=Vector(no).normalized())
    bm.normal_update()
    segs = []
    for e in r['geom_cut']:
        if not isinstance(e, bmesh.types.BMEdge) or not e.link_faces:
            continue
        a, b = e.verts[0].co.copy(), e.verts[1].co.copy()
        if min(a.z, b.z) < zmin or (rng and rng.random() > keep):
            continue
        n = sum((f.normal for f in e.link_faces), Vector()).normalized()
        segs.append((a, b, n))
    return segs


def crack_bm(segs, width, rng, lift=0.015, bm=None):
    """Glowing crack ribbons along cut segments: each a thin quad, a little kinked at its middle."""
    bm = bm or bmesh.new()
    for a, b, n in segs:
        m = (a + b) / 2
        side = (b - a).cross(n)
        if side.length < 1e-6:
            continue
        side.normalize()
        m = m + side * rng.uniform(-0.06, 0.06) * (b - a).length
        w = width * rng.uniform(0.6, 1.3)
        pts = [a, m, b]
        strip_bm(bm, pts, [n, n, n], w, lift)
    return bm


def blade_bm(bm, base, d, length, w, bend=0.4, segs=2, rng=None, flat=False):
    """A grass blade / leaf: a thin three-sided spike (never culled away from any side)."""
    base = Vector(base); d = Vector(d).normalized()
    pts = [base]
    dd = d.copy()
    for i in range(segs):
        dd = (dd + Vector((dd.x, dd.y, 0)).normalized() * bend / segs - Vector((0, 0, bend * 0.5 / segs))).normalized() if (dd.x or dd.y) else dd
        pts.append(pts[-1] + dd * length / segs)
    t, side, up = _frames(pts)[0]
    rings = []
    for i, p in enumerate(pts[:-1]):
        f = 1 - i / segs
        tri = []
        for k in range(3):
            a = k * TAU / 3 + math.pi / 2
            sx = math.cos(a) * w * f
            sz = math.sin(a) * w * f * (0.35 if flat else 1.0)
            tri.append(bm.verts.new(p + side * sx + up * sz))
        rings.append(tri)
    tip = bm.verts.new(pts[-1])
    for i in range(len(rings) - 1):
        for k in range(3):
            k2 = (k + 1) % 3
            bm.faces.new((rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]))
    for k in range(3):
        bm.faces.new((rings[-1][k], rings[-1][(k + 1) % 3], tip))
    return bm


def frond_bm(bm, base, d, length, w, droop=0.6, segs=4, fold=0.35, two_sided=True):
    """A palm frond / fern leaf: a V-folded strip arching out from base and drooping at the tip."""
    base = Vector(base); d = Vector(d)
    hd = Vector((d.x, d.y, 0)).normalized()
    side = hd.cross(Vector((0, 0, 1))).normalized()
    rows = []
    for i in range(segs + 1):
        t = i / segs
        p = base + hd * length * t + Vector((0, 0, length * (d.z * t - droop * t * t)))
        ww = w * math.sin(math.pi * min(1.0, t * 1.1 + 0.08))
        spine = p + Vector((0, 0, ww * fold))
        rows.append((bm.verts.new(p - side * ww), bm.verts.new(spine), bm.verts.new(p + side * ww)))
    faces = []
    for i in range(segs):
        a, b = rows[i], rows[i + 1]
        faces.append(bm.faces.new((a[0], a[1], b[1], b[0])))
        faces.append(bm.faces.new((a[1], a[2], b[2], b[1])))
    if two_sided:
        for f in faces:
            vs = [bm.verts.new(v.co - Vector((0, 0, 0.012))) for v in f.verts]
            bm.faces.new(vs[::-1])
    bm.normal_update()
    return bm


# ---- Finishing -------------------------------------------------------------------------------

def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def _tone(path, r=(0.0, 1.0), g=1.0, b=1.0):
    """Remaps the baked channels: R (occlusion) to r[0] + r[1] * R, G and B scaled."""
    import numpy as np
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.array(img.pixels[:]).reshape(h, w, 4)
    px[:, :, 0] = np.clip(r[0] + r[1] * px[:, :, 0], 0, 1)
    px[:, :, 1] = np.clip(px[:, :, 1] * g, 0, 1)
    px[:, :, 2] = np.clip(px[:, :, 2] * b, 0, 1)
    img.pixels = px.flatten().tolist()
    img.filepath_raw = path; img.file_format = 'PNG'; img.save()
    bpy.data.images.remove(img)


def finish(name, parts, out, size=512, bake_scale=1.0, ao=(0.0, 1.0), edge=1.0, grime=1.0):
    """Joins parts into "Body", paints, unwraps, bakes <name>_body.png and exports <name>.glb.
    bake_scale shrinks / grows the mesh while baking so wear and occlusion read at unit scale;
    ao / edge / grime tone the baked channels (e.g. less grime on snow, softer AO on tiny props)."""
    parts = [p for p in parts if p is not None]
    B = kit.join('Body', parts)
    kit.paint_vertex_colours(B)
    # No team colour anywhere: alpha must be 1 or 0.5.
    attr = B.data.color_attributes['Color']
    bad = [d.color[3] for d in attr.data if abs(d.color[3] - 1) > 1e-3 and abs(d.color[3] - 0.5) > 1e-3]
    assert not bad, f'{name}: stray alpha {bad[:3]}'
    kit.unwrap(B)
    os.makedirs(out, exist_ok=True)
    png = os.path.join(out, f'{name}_body.png')
    me = B.data
    if bake_scale != 1.0:
        me.transform(Matrix.Scale(bake_scale, 4))
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=60)
    gme = bpy.data.meshes.new('bakeground'); bm.to_mesh(gme); bm.free()
    g = bpy.data.objects.new('bakeground', gme); bpy.context.scene.collection.objects.link(g)
    kit.bake_detail(B, png, 64 if FAST else size)
    bpy.data.objects.remove(g)
    if bake_scale != 1.0:
        me.transform(Matrix.Scale(1.0 / bake_scale, 4))
    _tone(png, ao, edge, grime)
    kit.export(os.path.join(out, f'{name}.glb'), [B])
    xs = [v.co for v in me.vertices]
    bb = (min(v.x for v in xs), max(v.x for v in xs), min(v.y for v in xs), max(v.y for v in xs), min(v.z for v in xs), max(v.z for v in xs))
    n = tris(B)
    print(f'MODEL {name} tris {n} x {bb[0]:.2f}..{bb[1]:.2f} y {bb[2]:.2f}..{bb[3]:.2f} z {bb[4]:.2f}..{bb[5]:.2f}', flush=True)
    return n


def only(argv_names):
    """Names given after --only on the command line (to rebuild a few while iterating)."""
    if '--only' in sys.argv:
        i = sys.argv.index('--only')
        return set(sys.argv[i + 1].split(','))
    return None
