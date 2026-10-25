"""War debris: wreck_<0..2>.glb (burnt-out war machines, about 6 across; the game scales them to
3-24) and rubble_<0..2>.glb (broken concrete and rebar about 3 across, left round fallen buildings).
Run: python tools/models/features/debris.py game/models/features [--only wreck_0,...]

wreck_0  a tank: a torn hull tub with its turret blown off beside it, the gun snapped, a track
         run thrown off across the ground, loose road wheels.
wreck_1  a walker: a cracked torso on its side, one leg folded under it, a gun arm and plates.
wreck_2  a wheeled carrier: a bent chassis, a crushed cab, wheels torn off, a snapped launcher.
rubble_0 a heap of slabs and blocks, rebar sticking out.
rubble_1 a broken wall stump standing in its own debris.
rubble_2 a toppled column drum and a buckled girder in a spill of blocks.
No team colour: scorched steel, rust, soot, concrete, embers still glowing in the wreckage.
"""
import math, os, sys, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import nature_parts as N
import bpy, bmesh
import kit
from mathutils import Vector, Matrix, Euler
from nature_parts import TAU, shade, with_top, lump, tube, from_bm, hull_bm, bend_path

OUT = N.out_dir()
ONLY = N.only(sys.argv)


def M(c, rot):
    return Matrix.Translation(Vector(c)) @ Euler(rot).to_matrix().to_4x4()


def chunk(name, rng, c, size, rot, paint, chip=0.18, bevel=0.0):
    """A box-ish piece (hull block, plate, concrete lump) with some corners knocked off."""
    sx, sy, sz = size
    pts = []
    for x in (-1, 1):
        for y in (-1, 1):
            for z in (-1, 1):
                p = Vector((x * sx / 2, y * sy / 2, z * sz / 2))
                if rng.random() < 0.4:
                    # Chip this corner: replace it by three points pulled in along each edge.
                    for ax in range(3):
                        q = p.copy(); q[ax] *= 1 - rng.uniform(0.3, 1.0) * chip * 2 / max(0.3, size[ax])
                        q[ax] = p[ax] - math.copysign(min(abs(p[ax]) * 0.9, rng.uniform(0.05, chip)), p[ax])
                        pts.append(q)
                else:
                    pts.append(p)
    m = M(c, rot)
    bm = hull_bm([m @ p for p in pts], planar=0.01)
    o = from_bm(name, bm, paint)
    if bevel:
        kit.add_bevel(o, bevel, 1)
    return o


def plate(name, rng, c, w, l, t, rot, paint, bend=0.35):
    """A torn armour plate, bent along its middle."""
    m = M(c, rot)
    bm = bmesh.new()
    rows = []
    for i, y in enumerate((-l / 2, 0, l / 2)):
        z = 0 if i != 1 else 0
        ang = (bend if i == 2 else 0)
        yy = y if i < 2 else l / 2 * math.cos(bend)
        zz = 0 if i < 2 else l / 2 * math.sin(bend)
        rows.append([Vector((x * w / 2 * (1 - 0.25 * rng.random() if i == 2 else 1), yy, zz + dz)) for x in (-1, 1) for dz in (0, t)])
    vs = [[bm.verts.new(m @ p) for p in r] for r in rows]
    # Each row: (x-, bottom), (x-, top), (x+, bottom), (x+, top)
    for a, b in zip(vs, vs[1:]):
        bm.faces.new((a[1], a[3], b[3], b[1]))        # top
        bm.faces.new((a[2], a[0], b[0], b[2]))        # bottom
        bm.faces.new((a[0], a[1], b[1], b[0]))        # side -
        bm.faces.new((a[3], a[2], b[2], b[3]))        # side +
    bm.faces.new((vs[0][0], vs[0][2], vs[0][3], vs[0][1]))
    bm.faces.new((vs[-1][1], vs[-1][3], vs[-1][2], vs[-1][0]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return from_bm(name, bm, paint)


def wheel(p, name, c, axis, r, w, rubber='w_tread', hub='w_steel_dk', verts=12):
    a = Vector(c) - Vector(axis).normalized() * w / 2
    b = Vector(c) + Vector(axis).normalized() * w / 2
    p.append(kit.cyl(name, a, b, r, rubber, verts, bevel=0))
    n = Vector(axis).normalized()
    p.append(kit.cyl(name + 'hub', b - n * 0.02, b + n * 0.05, r * 0.55, hub, 8, bevel=0))
    p.append(kit.cyl(name + 'hub2', a + n * 0.02, a - n * 0.05, r * 0.55, hub, 8, bevel=0))


def embers(p, rng, spots, size=0.18):
    for i, (x, y, z) in enumerate(spots):
        p.append(lump(f'ember{i}', rng, (x, y, z), size * rng.uniform(0.8, 1.3), size, size * 0.6, 'ember', n=7, floor=z - size * 0.3))


def scorch(p, rng, rx, ry):
    """The burnt patch the wreck lies in: a low, ragged mound of soot and scorched earth with
    clods thrown round its rim, so it has no clean outline."""
    pal = shade('w_scorch', 'w_soot', 'w_soot', rng)
    p.append(lump('scorch', rng, (0, 0, -0.2), rx * 0.8, ry * 0.8, 0.32, pal, n=30, floor=-0.1, jit=0.3))
    for i in range(9):
        a = i * TAU / 9 + rng.uniform(-0.25, 0.25)
        d = rng.uniform(0.7, 0.95)
        c = (math.cos(a) * rx * d, math.sin(a) * ry * d, -0.08)
        r = rng.uniform(0.35, 0.7)
        p.append(lump(f'clod{i}', rng, c, r, r * 0.7, rng.uniform(0.12, 0.2), shade('w_scorch', 'w_scorch', 'w_soot', rng), n=8, floor=-0.06, rot=a))


def barrel(p, rng, a, b, r, name='barrel'):
    """A snapped gun barrel: a tube with a splintered end and a muzzle-sleeve ring part way along."""
    a, b = Vector(a), Vector(b)
    p.append(tube(name, [a, a.lerp(b, 0.5), b], [r, r * 0.95, r * 0.9], shade('w_steel', 'w_steel_dk', 'w_scorch', rng), sides=8, jag=(rng, r * 2.2)))
    s = a.lerp(b, 0.12)
    p.append(tube(name + 'sleeve', [a, s], [r * 1.5, r * 1.5], 'w_steel_dk', sides=8))


def track_run(p, rng, path, width, link=0.2):
    """A thrown track: links laid along a path over the ground, with guide horns."""
    pts = [Vector(q) for q in path]
    total = sum((b - a).length for a, b in zip(pts, pts[1:]))
    n = int(total / link)
    for i in range(n):
        s = i * link + link / 2
        for a, b in zip(pts, pts[1:]):
            L_ = (b - a).length
            if s <= L_:
                q = a.lerp(b, s / L_); d = (b - a).normalized(); break
            s -= L_
        yaw = math.atan2(d.y, d.x); pitch = -math.asin(max(-1, min(1, d.z)))
        p.append(chunk(f'link{i}', rng, q, (link * 0.85, width, 0.07), (0, pitch, yaw), 'w_tread', chip=0.0))
        if i % 2 == 0:
            p.append(chunk(f'horn{i}', rng, q + Vector((0, 0, 0.06)), (0.08, 0.06, 0.08), (0, pitch, yaw), 'w_steel_dk', chip=0.0))


# ---- wrecks -----------------------------------------------------------------------------------

def wreck_0(rng):
    p = []
    scorch(p, rng, 3.1, 2.6)
    hull_paint = shade('w_steel', 'w_steel_dk', 'w_scorch', rng, spread=0.18)
    # The hull tub, nose down at one corner, its rear torn open.
    rot = (0.08, -0.06, 0.35)
    m = M((0.0, 0.1, 0.35), rot)
    pts = [(-1.4, -0.8, -0.35), (1.4, -0.8, -0.35), (1.4, 0.8, -0.35), (-1.4, 0.8, -0.35),
           (1.65, -0.6, 0.0), (1.65, 0.6, 0.0), (1.3, -0.82, 0.35), (1.3, 0.82, 0.35),
           (-1.2, -0.82, 0.35), (-0.9, 0.82, 0.38), (-1.45, -0.7, 0.1), (-1.45, 0.4, 0.2)]
    hull = from_bm('hull', hull_bm([m @ Vector(q) for q in pts], planar=0.01), hull_paint)
    kit.inset_panels(hull, 0.05, 0.03, lambda f: f.calc_area() > 0.3)
    p.append(hull)
    # Turret ring: a dark hole where the turret was.
    p.append(kit.cyl('ring', m @ Vector((0.1, 0, 0.3)), m @ Vector((0.1, 0, 0.4)), 0.55, 'w_soot', 14, bevel=0))
    p.append(kit.cyl('ringlip', m @ Vector((0.1, 0, 0.36)), m @ Vector((0.1, 0, 0.41)), 0.62, 'w_rust_dk', 14, bevel=0))
    # Skirt plates hanging off / torn, rust showing.
    p.append(plate('skirtL', rng, m @ Vector((0.2, -0.88, 0.1)), 0.05, 1.6, 0.04, (0.2 + rot[0], 0, rot[2] + math.pi / 2), shade('w_rust', 'w_rust_dk', 'w_scorch', rng), bend=0.25))
    p.append(plate('skirtR', rng, (1.0, 1.55, 0.05), 1.3, 1.0, 0.05, (0.05, 0.1, 1.2), shade('w_rust', 'w_rust_dk', 'w_scorch', rng), bend=0.45))
    # Engine deck grille slats at the rear (crushed).
    for i in range(5):
        p.append(chunk(f'slat{i}', rng, m @ Vector((-1.0 + i * 0.12, 0.1, 0.4)), (0.06, 1.0, 0.05), rot, 'w_steel_dk', chip=0.0))
    # The turret, blown off and lying on its side beside the hull, the gun snapped.
    tm = M((-1.5, -1.4, 0.35), (0.5, 0.25, -0.6))
    tp = [(-0.6, -0.55, -0.3), (0.6, -0.55, -0.3), (0.65, 0.5, -0.3), (-0.65, 0.5, -0.3), (0.4, 0.75, -0.15), (-0.4, 0.75, -0.15),
          (-0.5, -0.6, 0.2), (0.5, -0.6, 0.2), (0.45, 0.4, 0.3), (-0.45, 0.4, 0.3)]
    tur = from_bm('turret', hull_bm([tm @ Vector(q) for q in tp], planar=0.01), hull_paint)
    kit.inset_panels(tur, 0.05, 0.025, lambda f: f.calc_area() > 0.2)
    p.append(tur)
    mz = tm @ Vector((0, 0.85, 0.0))
    barrel(p, rng, mz, mz + (tm.to_3x3() @ Vector((0, 1, 0))).normalized() * 1.25 + Vector((0, 0, -0.15)), 0.1)
    # The snapped-off barrel end lies further away.
    barrel(p, rng, (2.2, -1.45, 0.08), (1.3, -1.95, 0.08), 0.085, name='barrelend')
    # The thrown track, draped from the hull side across the ground, and road wheels.
    track_run(p, rng, [(0.9, -1.2, 0.25), (0.2, -1.55, 0.05), (-0.6, -2.0, 0.04), (-0.4, -2.55, 0.03)], 0.45)
    for i, (c, ax) in enumerate((((1.9, 0.6, 0.12), (0.2, 0.1, 1)), ((-2.3, 0.6, 0.25), (1, 0.4, 0.3)), ((m @ Vector((0.6, -0.85, -0.2))), (0, 1, 0)), ((m @ Vector((-0.2, -0.85, -0.2))), (0, 1, 0)))):
        wheel(p, f'wheel{i}', c, ax, 0.28, 0.16)
    # Scattered small plates and blocks.
    for i in range(6):
        a = rng.random() * TAU; d = rng.uniform(1.7, 2.6)
        c = (math.cos(a) * d, math.sin(a) * d * 0.85, 0.04)
        p.append(plate(f'frag{i}', rng, c, rng.uniform(0.25, 0.5), rng.uniform(0.3, 0.6), 0.04, (rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), rng.random() * TAU),
                       shade('w_steel', 'w_rust_dk', 'w_scorch', rng) if i % 2 else shade('w_rust', 'w_rust_dk', 'w_scorch', rng), bend=rng.uniform(0.1, 0.6)))
    embers(p, rng, [m @ Vector((0.1, 0.0, 0.3)), m @ Vector((0.3, 0.2, 0.32)), (-1.1, -0.3, 0.05), (0.9, -0.8, 0.08), (-0.3, 1.2, 0.05)])
    return p


def wreck_1(rng):
    p = []
    scorch(p, rng, 2.9, 2.7)
    pal = shade('w_steel', 'w_steel_dk', 'w_scorch', rng, spread=0.18)
    rust = shade('w_rust', 'w_rust_dk', 'w_scorch', rng)
    # Torso on its side: a wedge-fronted block, its sensor head knocked askew.
    m = M((0.2, 0.2, 0.55), (1.2, 0.15, 0.6))
    tp = [(-0.8, -0.6, -0.5), (0.8, -0.6, -0.5), (0.8, 0.5, -0.5), (-0.8, 0.5, -0.5), (0.6, 0.85, -0.2), (-0.6, 0.85, -0.2),
          (-0.7, -0.65, 0.55), (0.7, -0.65, 0.55), (0.6, 0.6, 0.45), (-0.6, 0.6, 0.45)]
    torso = from_bm('torso', hull_bm([m @ Vector(q) for q in tp], planar=0.01), pal)
    kit.inset_panels(torso, 0.05, 0.03, lambda f: f.calc_area() > 0.25)
    p.append(torso)
    p.append(chunk('head', rng, m @ Vector((0.0, 0.9, 0.35)), (0.55, 0.4, 0.3), (1.0, 0.3, 0.9), pal, chip=0.08))
    p.append(chunk('lens', rng, m @ Vector((0.0, 1.12, 0.35)), (0.22, 0.06, 0.1), (1.0, 0.3, 0.9), 'w_soot', chip=0.0))
    # Leg: thigh and shin folded, the foot splayed on the ground.
    hip = Vector((-0.6, -0.3, 0.55)); knee = Vector((-1.6, -1.2, 0.95)); ankle = Vector((-2.2, -0.4, 0.2))
    p.append(tube('thigh', [hip, knee], [0.24, 0.2], pal, sides=6))
    p.append(tube('shin', [knee, ankle], [0.2, 0.16], pal, sides=6))
    p.append(kit.cyl('kneejoint', knee + Vector((0, 0.25, 0)), knee - Vector((0, 0.25, 0)), 0.27, 'w_steel_dk', 10, bevel=0))
    p.append(chunk('foot', rng, ankle + Vector((-0.2, 0.1, -0.12)), (0.9, 0.55, 0.22), (0.1, 0.05, 0.6), pal, chip=0.1))
    # The other leg, torn off at the hip: a stump of shin lying apart.
    p.append(tube('shin2', [Vector((1.4, -1.6, 0.18)), Vector((2.4, -1.2, 0.22))], [0.2, 0.17], pal, sides=6, jag=(rng, 0.3)))
    p.append(chunk('foot2', rng, (2.55, -1.15, 0.12), (0.5, 0.75, 0.22), (0, 0.1, 0.4), pal, chip=0.1))
    # Gun arm: a box breech and a long snapped barrel pointing out.
    arm = m @ Vector((0.7, 0.2, 0.2))
    p.append(chunk('breech', rng, arm + Vector((0.4, 0.4, 0.0)), (0.5, 0.8, 0.4), (0.2, 0.1, 0.9), pal, chip=0.1))
    barrel(p, rng, arm + Vector((0.6, 0.9, 0.0)), arm + Vector((1.3, 2.0, -0.45)), 0.09)
    # Armour plates shed round it, rust on the undersides.
    for i in range(6):
        a = rng.random() * TAU; d = rng.uniform(1.5, 2.6)
        c = (math.cos(a) * d, math.sin(a) * d * 0.85, 0.05)
        p.append(plate(f'frag{i}', rng, c, rng.uniform(0.3, 0.6), rng.uniform(0.35, 0.7), 0.045, (rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25), rng.random() * TAU),
                       rust if i % 3 == 0 else pal, bend=rng.uniform(0.1, 0.7)))
    # Cables spilling from the torso.
    for i in range(3):
        a = m @ Vector((rng.uniform(-0.4, 0.4), -0.6, rng.uniform(-0.2, 0.3)))
        path = bend_path(rng, a, (rng.uniform(-1, 1), -1, -0.6), 1.2, 3, wander=0.5)
        path = [Vector((q.x, q.y, max(q.z, 0.03))) for q in path]
        p.append(tube(f'cable{i}', path, [0.04] * len(path), 'w_tread', sides=4))
    embers(p, rng, [m @ Vector((0.0, -0.66, 0.0)), (0.9, -0.6, 0.06), (-0.6, 0.9, 0.05), (-1.4, -0.3, 0.06)])
    return p


def wreck_2(rng):
    p = []
    scorch(p, rng, 3.2, 2.5)
    pal = shade('w_steel', 'w_steel_dk', 'w_scorch', rng, spread=0.18)
    rust = shade('w_rust', 'w_rust_dk', 'w_scorch', rng)
    # Chassis: two bent frame rails with cross members, sagging at the back.
    m = M((0.2, 0.0, 0.35), (0.0, 0.07, 0.15))
    for sy in (-0.55, 0.55):
        rail = [m @ Vector((-1.9, sy, -0.15)), m @ Vector((-0.5, sy, 0.0)), m @ Vector((1.0, sy, 0.05)), m @ Vector((1.9, sy + 0.05, -0.05))]
        p.append(tube(f'rail{sy}', rail, [0.11] * 4, 'w_steel_dk', sides=4, phase=math.pi / 4))
    for i, x in enumerate((-1.5, -0.4, 0.7, 1.6)):
        p.append(chunk(f'cross{i}', rng, m @ Vector((x, 0, 0.0)), (0.14, 1.2, 0.1), (0, 0, 0.15), 'w_steel_dk', chip=0.0))
    # Cargo bed plate, half on, curling up at one corner; a box launcher fallen off it.
    p.append(plate('bed', rng, m @ Vector((-0.6, 0.0, 0.15)), 1.4, 2.0, 0.06, (0.0, 0.0, 0.15 + math.pi / 2), rust, bend=0.4))
    lm = M((-1.6, 1.5, 0.35), (0.35, 0.0, 2.2))
    p.append(chunk('launcher', rng, lm @ Vector((0, 0, 0)), (1.3, 0.8, 0.55), (0.35, 0.0, 2.2), pal, chip=0.1))
    for i in range(3):
        for j in range(2):
            c = lm @ Vector((0.66, -0.25 + i * 0.25, -0.12 + j * 0.25))
            p.append(kit.cyl(f'tube{i}{j}', c - lm.to_3x3() @ Vector((0.03, 0, 0)), c + lm.to_3x3() @ Vector((0.03, 0, 0)), 0.09, 'w_soot', 8, bevel=0))
    # Crushed cab at the front.
    cm = M((1.75, 0.05, 0.7), (0.15, 0.25, 0.1))
    cp = [(-0.5, -0.65, -0.4), (0.5, -0.65, -0.4), (0.5, 0.65, -0.4), (-0.5, 0.65, -0.4), (0.6, -0.5, 0.0), (0.6, 0.5, 0.0),
          (-0.45, -0.6, 0.25), (-0.3, 0.6, 0.15), (0.2, -0.4, 0.2), (0.25, 0.35, 0.05)]
    cab = from_bm('cab', hull_bm([cm @ Vector(q) for q in cp], planar=0.01), pal)
    kit.inset_panels(cab, 0.04, 0.025, lambda f: f.calc_area() > 0.15)
    p.append(cab)
    p.append(chunk('window', rng, cm @ Vector((0.45, 0, 0.0)), (0.08, 0.9, 0.22), (0.15, 0.25, 0.1), 'w_soot', chip=0.0))
    # Wheels: two still on their hubs, two torn off and lying flat, one on its edge.
    for i, (c, ax, r) in enumerate((((1.25, 0.95, 0.3), (0.05, 1, 0), 0.38), ((-1.0, -0.95, 0.15), (0.1, 1, 0.5), 0.38),
                                    ((2.6, -1.4, 0.1), (0.1, 0.1, 1), 0.38), ((-2.6, -0.8, 0.1), (0.2, 0.0, 1), 0.38), ((0.3, 1.9, 0.38), (0.4, 1, 0.0), 0.38))):
        wheel(p, f'wheel{i}', c, ax, r, 0.24, verts=14)
    # Debris round it.
    for i in range(6):
        a = rng.random() * TAU; d = rng.uniform(1.8, 2.8)
        c = (math.cos(a) * d, math.sin(a) * d * 0.8, 0.04)
        p.append(plate(f'frag{i}', rng, c, rng.uniform(0.25, 0.5), rng.uniform(0.3, 0.6), 0.04, (rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), rng.random() * TAU),
                       rust if i % 2 else pal, bend=rng.uniform(0.1, 0.6)))
    embers(p, rng, [m @ Vector((0.4, 0.0, 0.0)), m @ Vector((-1.0, 0.2, 0.0)), (1.8, -0.7, 0.06), (-0.5, -1.3, 0.05)])
    return p


# ---- rubble -----------------------------------------------------------------------------------

def concrete(rng, soot=False):
    return shade('c_lt', 'c_mid', 'c_dk' if not soot else 'w_scorch', rng, spread=0.15)


def rebar(p, rng, a, n=2, name='rebar', length=(0.6, 1.1)):
    for i in range(n):
        d = Vector((rng.uniform(-0.7, 0.7), rng.uniform(-0.7, 0.7), 1))
        path = bend_path(rng, a + Vector((rng.uniform(-0.15, 0.15), rng.uniform(-0.15, 0.15), 0)), d, rng.uniform(*length), 3, wander=0.45)
        p.append(tube(f'{name}{i}', path, [0.035] * len(path), shade('w_rust', 'w_rust_dk', 'w_rust_dk', rng), sides=4, cap=True))


def dust(p, rng, r=1.55):
    p.append(lump('dust', rng, (0, 0, -0.15), r, r * 0.85, 0.35, shade('c_dust', 'c_mid', 'c_dk', rng), n=16, floor=-0.1, jit=0.25))


def blocks(p, rng, n, rmax, zbase=0.15, size=(0.35, 0.8)):
    for i in range(n):
        a = rng.random() * TAU; d = rng.uniform(0.15, rmax)
        s = (rng.uniform(*size), rng.uniform(*size), rng.uniform(size[0] * 0.6, size[1] * 0.7))
        c = (math.cos(a) * d, math.sin(a) * d, zbase + s[2] * 0.3 + (rmax - d) * 0.15)
        p.append(chunk(f'block{i}', rng, c, s, (rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), rng.random() * TAU), concrete(rng, i % 4 == 1), chip=0.12))


def rubble_0(rng):
    p = []
    dust(p, rng)
    # A broken floor slab tilted up out of the heap, rebar from its torn edge.
    sm = M((0.1, 0.05, 0.45), (0.4, 0.1, 0.5))
    p.append(chunk('slab', rng, sm @ Vector((0, 0, 0)), (1.7, 1.2, 0.22), (0.4, 0.1, 0.5), concrete(rng), chip=0.25))
    rebar(p, rng, sm @ Vector((0.0, 0.55, 0.0)), 3)
    p.append(chunk('slab2', rng, (-0.85, -0.6, 0.15), (1.1, 0.8, 0.18), (-0.15, 0.1, 2.0), concrete(rng, True), chip=0.2))
    blocks(p, rng, 7, 1.25)
    return p


def rubble_1(rng):
    p = []
    dust(p, rng)
    # A stump of wall with a jagged top, standing in its debris; window sill notch.
    wm = M((0.0, 0.2, 0.0), (0, 0, 0.3))
    pts = [(-1.2, -0.17, -0.1), (1.2, -0.17, -0.1), (1.2, 0.17, -0.1), (-1.2, 0.17, -0.1)]
    tops = [(-1.2, 1.3), (-0.7, 1.55), (-0.3, 1.0), (0.1, 0.8), (0.5, 0.55), (0.9, 0.7), (1.2, 0.4)]
    for i, ((x0, z0), (x1, z1)) in enumerate(zip(tops, tops[1:])):
        q = [(x0, -0.17, -0.1), (x1, -0.17, -0.1), (x0, 0.17, -0.1), (x1, 0.17, -0.1),
             (x0, -0.17, z0), (x1, -0.17, z1), (x0, 0.17, z0 - 0.05), (x1, 0.17, z1 - 0.05)]
        p.append(from_bm(f'wall{i}', hull_bm([wm @ Vector(v) for v in q], planar=0.01), concrete(rng, i == 2)))
    for x in (-0.8, -0.1, 0.7):
        z = 1.0 if x < 0 else 0.6
        rebar(p, rng, wm @ Vector((x, 0, z)), 1, name=f'rb{x:.1f}', length=(0.4, 0.7))
    blocks(p, rng, 7, 1.3)
    return p


def rubble_2(rng):
    p = []
    dust(p, rng)
    # A toppled column drum (octagonal, chipped) and a buckled I-girder across the heap.
    a, b = Vector((-1.1, -0.5, 0.35)), Vector((0.2, 0.4, 0.4))
    p.append(tube('drum', [a, b], [0.42, 0.42], concrete(rng), sides=8, phase=0.3))
    p.append(kit.cyl('drumend', a - (b - a).normalized() * 0.02, a, 0.36, 'c_dk', 8, bevel=0))
    g0, g1 = Vector((-0.3, 1.0, 0.25)), Vector((1.3, -0.4, 0.85))
    gm = (g0 + g1) / 2 + Vector((0, 0, 0.2))
    rs = shade('w_rust', 'w_rust_dk', 'w_scorch', rng)
    for o, (w, h) in (('web', (0.05, 0.3)), ('flA', (0.3, 0.05)), ('flB', (0.3, 0.05))):
        off = Vector((0, 0, 0.13 if o == 'flA' else -0.13 if o == 'flB' else 0))
        for k, (s, e) in enumerate(((g0, gm), (gm, g1))):
            d = (e - s); yaw = math.atan2(d.y, d.x); pitch = -math.asin(d.z / d.length)
            p.append(chunk(f'{o}{k}', rng, (s + e) / 2 + off, (d.length + 0.05, w, h), (0, pitch, yaw), rs, chip=0.0))
    rebar(p, rng, Vector((0.5, -0.5, 0.4)), 2)
    blocks(p, rng, 6, 1.3)
    return p


BUILDS = [('wreck_0', wreck_0), ('wreck_1', wreck_1), ('wreck_2', wreck_2), ('rubble_0', rubble_0), ('rubble_1', rubble_1), ('rubble_2', rubble_2)]

if __name__ == '__main__':
    for i, (name, fn) in enumerate(BUILDS):
        if ONLY and name not in ONLY:
            continue
        N.reset()
        parts = fn(random.Random(3000 + i * 71))
        N.finish(name, parts, OUT, 512, bake_scale=0.5 if name.startswith('wreck') else 0.8)
