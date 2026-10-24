"""Shared parts for the walking bots and commanders (group "bots").

Authoring space is Blender's: Z up, the unit faces +Y, ground at z = 0, radius-1 scale.
The procedural models (game/src/View/Meshes/Models.Bots.cs) are written in game axes
(Y up, -Z forward); game (x, y, z) is Blender (x, -z, y), see g().
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere
from mathutils import Vector, Matrix, Euler


def g(x, y, z):
    """Game authoring coords (Y up, -Z forward) to Blender (Z up, +Y forward)."""
    return (x, -z, y)


def lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def add(a, b):
    return tuple(a[i] + b[i] for i in range(3))


def xform(objs, M):
    """Bakes a transform into the mesh data of each object (sub-assemblies built at the origin)."""
    for o in objs:
        o.data.transform(M)
    return objs


def obox(name, centre, size, paint, rot=(0, 0, 0), bevel=0.015, segs=1):
    """A box rotated by Euler degrees (X, Y, Z) about its own centre."""
    o = box(name, (0, 0, 0), size, paint, bevel, segs)
    M = Matrix.Translation(Vector(centre)) @ Euler([math.radians(r) for r in rot]).to_matrix().to_4x4()
    o.data.transform(M)
    return o


def beam(name, a, b, w, d, paint, side=(1, 0, 0), w2=None, d2=None, bevel=0.012, segs=1):
    """A square-section bar from a to b: w across `side`, d across the other axis; tapers to w2/d2 at b."""
    a, b = Vector(a), Vector(b)
    ax = (b - a).normalized()
    s = Vector(side)
    s = (s - ax * s.dot(ax))
    if s.length < 1e-4:
        s = Vector((0, 1, 0)) - ax * ax.y
    s.normalize()
    u = ax.cross(s).normalized()
    pts = []
    for p, ww, dd in ((a, w, d), (b, w if w2 is None else w2, d if d2 is None else d2)):
        for i in (-1, 1):
            for j in (-1, 1):
                pts.append(p + s * i * ww / 2 + u * j * dd / 2)
    return hull_from(name, pts, paint, bevel, segs)


def knee_point(hip, foot, bend, forward=1):
    """Two-bone knee: midpoint pushed along +Y (forward=1) or -Y (forward=-1) by bend."""
    m = lerp(hip, foot, 0.5)
    return (m[0], m[1] + forward * bend, m[2])


# ---------------------------------------------------------------------------------------------
# Legs
# ---------------------------------------------------------------------------------------------

def lattice_leg(p, hip, foot, t, armour='team', glow=True):
    """Slim digitigrade leg: thigh forward-down to the knee, shin back-down to a high reversed
    ankle (the "reverse joint"), then a slim tarsus down to a two-toed foot with a heel spur.
    t is the thickness."""
    h = hip[2] - foot[2]
    sx = 1 if hip[0] >= 0 else -1
    knee = (lerp(hip, foot, 0.4)[0], hip[1] + 0.2 * h, hip[2] - 0.36 * h)
    ank = (lerp(hip, foot, 0.85)[0], foot[1] - 0.2 * h, foot[2] + 0.3 * h)
    fz = foot[2] + t * 0.32
    out = []
    # Hip actuator
    out.append(cyl(p + 'hipj', (hip[0] - sx * t * 0.7, hip[1], hip[2]), (hip[0] + sx * t * 0.7, hip[1], hip[2]), t * 0.62, 'mech', 10))
    out.append(cyl(p + 'hipcap', (hip[0] + sx * t * 0.7, hip[1], hip[2]), (hip[0] + sx * t * 0.82, hip[1], hip[2]), t * 0.42, 'metal', 10, bevel=0))
    # Thigh: dark strut with an angular armour plate on the front/outside
    out.append(beam(p + 'thigh', hip, knee, t * 0.7, t * 0.8, 'mech'))
    tA, tB = lerp(hip, knee, 0.1), lerp(hip, knee, 0.86)
    out.append(beam(p + 'thighplate', add(tA, (sx * t * 0.08, t * 0.12, t * 0.08)), add(tB, (sx * t * 0.06, t * 0.14, 0)),
                    t * 1.15, t * 0.72, armour, w2=t * 0.85, d2=t * 0.5, bevel=0.01))
    # Knee
    out.append(cyl(p + 'knee', (knee[0] - sx * t * 0.55, knee[1], knee[2]), (knee[0] + sx * t * 0.55, knee[1], knee[2]), t * 0.5, 'metal', 10))
    out.append(obox(p + 'kneecap', add(knee, (0, t * 0.35, t * 0.12)), (t * 0.8, t * 0.35, t * 0.7), 'hull', (-20, 0, 0), 0.01))
    # Shin (long, swept back) with a piston alongside
    out.append(beam(p + 'shin', knee, ank, t * 0.62, t * 0.7, 'hull', w2=t * 0.5, d2=t * 0.55))
    out.append(beam(p + 'shinplate', add(lerp(knee, ank, 0.12), (0, -t * 0.25, 0)), add(lerp(knee, ank, 0.7), (0, -t * 0.25, 0)),
                    t * 0.75, t * 0.32, 'hull_dark', w2=t * 0.55, bevel=0.008))
    pa, pb = add(lerp(knee, ank, 0.05), (sx * t * 0.42, 0, 0)), add(lerp(knee, ank, 0.75), (sx * t * 0.42, 0, 0))
    out.append(cyl(p + 'piston', pa, pb, t * 0.13, 'metal', 6, bevel=0))
    out.append(cyl(p + 'pistonsl', pa, lerp(pa, pb, 0.45), t * 0.19, 'mech', 6, bevel=0))
    if glow:
        g0, g1 = add(lerp(knee, ank, 0.25), (sx * t * 0.33, 0, 0)), add(lerp(knee, ank, 0.55), (sx * t * 0.33, 0, 0))
        out.append(beam(p + 'shinglow', g0, g1, t * 0.06, t * 0.16, 'glow', bevel=0))
    # Reverse ankle joint
    out.append(cyl(p + 'ankle', (ank[0] - sx * t * 0.45, ank[1], ank[2]), (ank[0] + sx * t * 0.45, ank[1], ank[2]), t * 0.4, 'mech', 10))
    # Tarsus down to the foot
    out.append(beam(p + 'tarsus', ank, (foot[0], foot[1] - t * 0.1, fz + t * 0.2), t * 0.45, t * 0.5, 'metal', w2=t * 0.55))
    # Foot: a wedge with two forward toes and a heel spur
    fx, fy = foot[0], foot[1]
    out.append(hull_from(p + 'foot', [
        (fx - t * 0.7, fy - t * 0.5, foot[2]), (fx + t * 0.7, fy - t * 0.5, foot[2]),
        (fx - t * 0.7, fy + t * 0.9, foot[2]), (fx + t * 0.7, fy + t * 0.9, foot[2]),
        (fx - t * 0.5, fy - t * 0.3, foot[2] + t * 0.55), (fx + t * 0.5, fy - t * 0.3, foot[2] + t * 0.55),
        (fx - t * 0.4, fy + t * 0.4, foot[2] + t * 0.45), (fx + t * 0.4, fy + t * 0.4, foot[2] + t * 0.45)], 'hull_dark', 0.012, 1))
    for k, ox in enumerate((-0.42, 0.42)):
        out.append(hull_from(p + f'toe{k}', [
            (fx + t * (ox - 0.24), fy + t * 0.6, foot[2]), (fx + t * (ox + 0.24), fy + t * 0.6, foot[2]),
            (fx + t * (ox - 0.16), fy + t * 1.75, foot[2]), (fx + t * (ox + 0.16), fy + t * 1.75, foot[2]),
            (fx + t * (ox - 0.2), fy + t * 0.65, foot[2] + t * 0.36), (fx + t * (ox + 0.2), fy + t * 0.65, foot[2] + t * 0.36),
            (fx + t * (ox - 0.1), fy + t * 1.6, foot[2] + t * 0.12), (fx + t * (ox + 0.1), fy + t * 1.6, foot[2] + t * 0.12)],
            'hull', 0.008, 1))
    out.append(hull_from(p + 'spur', [
        (fx - t * 0.18, fy - t * 0.4, foot[2]), (fx + t * 0.18, fy - t * 0.4, foot[2]),
        (fx - t * 0.1, fy - t * 1.2, foot[2]), (fx + t * 0.1, fy - t * 1.2, foot[2]),
        (fx - t * 0.16, fy - t * 0.4, foot[2] + t * 0.35), (fx + t * 0.16, fy - t * 0.4, foot[2] + t * 0.35)], 'hull', 0.006, 1))
    return out


def crucible_leg(p, hip, foot, t, armour='team', bend=None):
    """Stocky forward-kneed stomper: armoured thigh, rivetted knee guard, thick shin with a
    hydraulic ram, and a broad flat foot with toe plates. t is the thickness."""
    h = hip[2] - foot[2]
    sx = 1 if hip[0] >= 0 else -1
    knee = knee_point(hip, foot, bend if bend is not None else 0.2 * h, 1)
    knee = (knee[0], knee[1], knee[2] + 0.02 * h)
    ank = (foot[0], foot[1] - t * 0.1, foot[2] + t * 0.75)
    out = []
    out.append(cyl(p + 'hipj', (hip[0] - sx * t * 0.6, hip[1], hip[2]), (hip[0] + sx * t * 0.62, hip[1], hip[2]), t * 0.55, 'mech', 8))
    out.append(cyl(p + 'hipcap', (hip[0] + sx * t * 0.62, hip[1], hip[2]), (hip[0] + sx * t * 0.76, hip[1], hip[2]), t * 0.36, 'metal', 8, bevel=0))
    out.append(beam(p + 'thigh', hip, knee, t * 0.8, t * 0.8, 'mech'))
    out.append(beam(p + 'thighplate', add(lerp(hip, knee, 0.12), (sx * t * 0.06, t * 0.1, 0)),
                    add(lerp(hip, knee, 0.88), (sx * t * 0.06, t * 0.12, 0)), t * 1.25, t * 0.9, armour, bevel=0.012))
    out.append(cyl(p + 'knee', (knee[0] - sx * t * 0.62, knee[1], knee[2]), (knee[0] + sx * t * 0.62, knee[1], knee[2]), t * 0.5, 'mech', 8))
    out.append(obox(p + 'kneeguard', add(knee, (0, t * 0.42, t * 0.06)), (t * 1.05, t * 0.32, t * 0.95), 'hull', (10, 0, 0), 0.012))
    out.append(sphere(p + 'kr0', add(knee, (-t * 0.28, t * 0.6, t * 0.3)), t * 0.08, 'metal', 6, 4))
    out.append(sphere(p + 'kr1', add(knee, (t * 0.28, t * 0.6, t * 0.3)), t * 0.08, 'metal', 6, 4))
    out.append(beam(p + 'shin', knee, ank, t * 0.95, t * 0.95, 'hull_dark', w2=t * 0.8, d2=t * 0.75))
    out.append(beam(p + 'shinplate', add(lerp(knee, ank, 0.15), (0, t * 0.45, 0)), add(lerp(knee, ank, 0.8), (0, t * 0.38, 0)),
                    t * 1.0, t * 0.18, 'hull', w2=t * 0.85, bevel=0.008))
    # Hydraulic ram down the back of the leg
    ra, rb = add(lerp(hip, knee, 0.35), (0, -t * 0.55, 0)), add(lerp(knee, ank, 0.65), (0, -t * 0.5, 0))
    out.append(cyl(p + 'ram', ra, rb, t * 0.12, 'metal', 6, bevel=0))
    out.append(cyl(p + 'ramsl', ra, lerp(ra, rb, 0.5), t * 0.2, 'mech', 6, bevel=0))
    out.append(cyl(p + 'ankle', (ank[0] - sx * t * 0.5, ank[1], ank[2]), (ank[0] + sx * t * 0.5, ank[1], ank[2]), t * 0.38, 'mech', 8))
    # Broad stomper foot with two toe plates and a heel block
    fx, fy, fz = foot
    out.append(box(p + 'foot', (fx, fy + t * 0.15, fz + t * 0.28), (t * 1.6, t * 1.9, t * 0.56), 'hull_dark', 0.02, 1))
    for k, ox in enumerate((-0.42, 0.42)):
        out.append(hull_from(p + f'toe{k}', [
            (fx + t * (ox - 0.36), fy + t * 1.0, fz), (fx + t * (ox + 0.36), fy + t * 1.0, fz),
            (fx + t * (ox - 0.34), fy + t * 1.55, fz), (fx + t * (ox + 0.34), fy + t * 1.55, fz),
            (fx + t * (ox - 0.36), fy + t * 1.0, fz + t * 0.46), (fx + t * (ox + 0.36), fy + t * 1.0, fz + t * 0.46),
            (fx + t * (ox - 0.32), fy + t * 1.4, fz + t * 0.22), (fx + t * (ox + 0.32), fy + t * 1.4, fz + t * 0.22)], 'hull', 0.01, 1))
    out.append(box(p + 'heel', (fx, fy - t * 0.85, fz + t * 0.2), (t * 1.1, t * 0.4, t * 0.4), 'hull', 0.012, 1))
    out.append(box(p + 'anklecuff', (ank[0], ank[1], ank[2] - t * 0.15), (t * 1.05, t * 1.05, t * 0.3), 'mech', 0.01, 1))
    return out


# ---------------------------------------------------------------------------------------------
# Weapons and tools
# ---------------------------------------------------------------------------------------------

def emitter(p, a, b, r, faction='lattice', verts=10):
    """Beam emitter: a barrel, a lens collar and a glowing tip at b."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    out = [cyl(p + 'bar', a, b - d * r * 1.3, r, 'metal', verts, r2=r * 0.85)]
    out.append(cyl(p + 'collar', b - d * r * 2.6, b - d * r * 0.4, r * 1.45, 'mech', verts, r2=r * 1.25))
    out.append(cyl(p + 'tip', b - d * r * 0.45, b, r * 0.85, 'glow', verts, bevel=0))
    if faction == 'lattice':
        out.append(cyl(p + 'ring', a.lerp(b, 0.45) - d * r * 0.3, a.lerp(b, 0.45) + d * r * 0.3, r * 1.25, 'glow_dim', verts, bevel=0))
    return out


def barrel(p, a, b, r, brake=True, verts=10, paint='metal'):
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    out = [cyl(p + 'bar', a, b, r, paint, verts, r2=r * 0.9)]
    if brake:
        out.append(cyl(p + 'brake', b - d * r * 2.4, b + d * r * 0.2, r * 1.5, 'mech', verts, r2=r * 1.4))
    return out


def missile_pod(p, centre, size, pitch, cols, rows, paint, tip='accent', yaw=0, face='mech'):
    """A launcher box pitched up by `pitch` degrees, a grid of warheads poking from its front."""
    sx, sy, sz = size
    out = [box(p + 'pod', (0, 0, 0), size, paint, 0.012, 1)]
    out.append(box(p + 'face', (0, sy / 2 + 0.004, 0), (sx * 0.9, 0.012, sz * 0.88), face, 0, 1))
    cw, rh = sx / cols, sz / rows
    rad = min(cw, rh) * 0.33
    for c in range(cols):
        for r in range(rows):
            x = -sx / 2 + cw * (c + 0.5)
            z = -sz / 2 + rh * (r + 0.5)
            out.append(cyl(p + f'm{c}{r}', (x, sy / 2 - 0.01, z), (x, sy / 2 + rad * 2.4, z), rad, tip, 8, r2=0.0, bevel=0))
            out.append(cyl(p + f'mr{c}{r}', (x, sy / 2 - 0.004, z), (x, sy / 2 + 0.012, z), rad * 1.3, 'tread', 8, bevel=0))
    M = Matrix.Translation(Vector(centre)) @ Euler((0, 0, math.radians(yaw))).to_matrix().to_4x4() \
        @ Euler((math.radians(pitch), 0, 0)).to_matrix().to_4x4()
    return xform(out, M)


def stack(p, base, r, h, hot=True, band=None, verts=10):
    """Smokestack with a rim; hot stacks glow at the mouth."""
    x, y, z = base
    out = [cyl(p + 'st', (x, y, z), (x, y, z + h), r, 'metal', verts, r2=r * 0.88)]
    out.append(cyl(p + 'rim', (x, y, z + h - r * 0.5), (x, y, z + h + r * 0.1), r * 1.08, 'mech', verts, bevel=0))
    out.append(cyl(p + 'mouth', (x, y, z + h + r * 0.1), (x, y, z + h + r * 0.14), r * 0.8, 'glow' if hot else 'tread', verts, bevel=0))
    if band:
        out.append(cyl(p + 'band', (x, y, z + h * 0.35), (x, y, z + h * 0.52), r * 1.1, band, verts, bevel=0))
    return out


def rivet_row(p, a, b, n, r=0.018, paint='metal'):
    return kit.rivets(p, a, b, n, paint, r)


# ---------------------------------------------------------------------------------------------
# Finishing
# ---------------------------------------------------------------------------------------------

def out_dir():
    return sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'


def finish(uid, body, turret=None, pivot=None, size=512):
    """Joins, paints, unwraps, bakes and exports Body (+ Turret). Prints the triangle count."""
    out = out_dir()
    os.makedirs(out, exist_ok=True)
    size = int(os.environ.get('BOTS_TEX', size))
    B = kit.join('Body', body)
    parts = [B]
    if turret:
        T = kit.join('Turret', turret, parent_origin=pivot)
        parts.append(T)
    if os.environ.get('BOTS_DRY'):
        # Geometry check only: no bake, no export.
        tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in parts}
        print('tris', uid, tris, 'total', sum(tris.values()))
        return parts
    for o in parts:
        kit.paint_vertex_colours(o)
        kit.unwrap(o)
    kit.bake_detail(B, os.path.join(out, f'{uid}_body.png'), size)
    if turret:
        kit.bake_detail(parts[1], os.path.join(out, f'{uid}_turret.png'), size)
    kit.export(os.path.join(out, f'{uid}.glb'), parts)
    tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in parts}
    print('tris', uid, tris, 'total', sum(tris.values()))
    return parts


def crane(p, base, size, faction='lattice', reach=1.0):
    """Nanolathe crane on a bot's back: swivel base, raised team upper arm, forearm reaching
    forward and down to a glowing emitter head. Matches Parts.NanoArm (authored ~1 tall at size 1)."""
    bx, by, bz = base
    S = lambda x, y, z: (bx + x * size, by + y * size, bz + z * size)
    lat = faction == 'lattice'
    t = 1.0 if lat else 1.35
    sh, el, wr = S(0, -0.06, 0.16), S(0, -0.22, 0.78), S(0, 0.5 * reach, 0.62)
    out = [cyl(p + 'base', S(0, 0, 0), S(0, 0, 0.18), 0.22 * t * size, 'mech', 12 if lat else 8)]
    out.append(cyl(p + 'baser', S(0, 0, 0.07), S(0, 0, 0.12), 0.24 * t * size, 'hull_dark' if lat else 'accent', 12 if lat else 8, bevel=0))
    out.append(beam(p + 'upper', sh, el, 0.15 * t * size, 0.17 * t * size, 'team', w2=0.12 * t * size, d2=0.13 * t * size))
    out.append(cyl(p + 'elbow', (el[0] - 0.11 * t * size, el[1], el[2]), (el[0] + 0.11 * t * size, el[1], el[2]), 0.1 * t * size, 'mech', 10))
    out.append(beam(p + 'fore', el, wr, 0.1 * t * size, 0.11 * t * size, 'hull' if lat else 'accent', w2=0.08 * t * size, d2=0.09 * t * size))
    if not lat:
        out.append(cyl(p + 'ram', lerp(sh, el, 0.1), lerp(el, wr, 0.35), 0.035 * size, 'metal', 6, bevel=0))
        out.append(cyl(p + 'ramsl', lerp(sh, el, 0.1), lerp(sh, el, 0.6), 0.05 * size, 'mech', 6, bevel=0))
    else:
        out.append(beam(p + 'upglow', add(lerp(sh, el, 0.25), (0.08 * size, 0, 0)), add(lerp(sh, el, 0.75), (0.08 * size, 0, 0)),
                        0.012 * size, 0.04 * size, 'glow', bevel=0))
    d = Vector((0, 1, -0.5)).normalized()
    w = Vector(wr)
    out.append(cyl(p + 'wrist', w, w + d * 0.12 * size, 0.08 * t * size, 'mech', 10))
    if lat:
        # Ring emitter: a collar with three prongs round a glowing orb
        head = w + d * 0.2 * size
        out.append(cyl(p + 'collar', w + d * 0.1 * size, head, 0.09 * size, 'metal', 12, r2=0.11 * size))
        for k in range(3):
            a = math.radians(90 + 120 * k)
            off = Vector((math.cos(a), 0, math.sin(a)))
            off = (off - d * off.dot(d)).normalized() * 0.08 * size
            out.append(beam(p + f'prong{k}', head + off, head + d * 0.16 * size + off * 0.5, 0.025 * size, 0.025 * size, 'hull', bevel=0.004))
        out.append(sphere(p + 'orb', tuple(head + d * 0.1 * size), 0.06 * size, 'glow', 10, 6))
    else:
        head = w + d * 0.14 * size
        out.append(cyl(p + 'nozzle', head, head + d * 0.14 * size, 0.1 * size, 'metal', 8, r2=0.07 * size))
        for k in range(2):
            sxk = -1 if k == 0 else 1
            out.append(beam(p + f'jaw{k}', head + Vector((sxk * 0.09 * size, 0, 0)), head + d * 0.22 * size + Vector((sxk * 0.05 * size, 0, 0)),
                            0.03 * size, 0.06 * size, 'mech', bevel=0.004))
        out.append(sphere(p + 'orb', tuple(head + d * 0.2 * size), 0.07 * size, 'glow', 10, 6))
    return out


# ---------------------------------------------------------------------------------------------
# Infantry bodies (Models.LatticeBotLegs/Torso, CrucibleBotLegs/Torso)
# ---------------------------------------------------------------------------------------------

def lattice_infantry_legs(t=0.1, hip_z=0.66, stagger=0.05):
    out = []
    for sx in (-1, 1):
        out += lattice_leg(f'leg{sx}', (sx * 0.17, -0.04, hip_z), (sx * 0.25, stagger if sx < 0 else -stagger, 0.0), t)
    out.append(hull_from('pelvis', [(-0.16, -0.1, hip_z - 0.06), (0.16, -0.1, hip_z - 0.06), (-0.12, 0.1, hip_z - 0.05), (0.12, 0.1, hip_z - 0.05),
                                    (-0.2, -0.13, hip_z + 0.07), (0.2, -0.13, hip_z + 0.07), (-0.17, 0.11, hip_z + 0.07), (0.17, 0.11, hip_z + 0.07)],
                         'hull_dark', 0.015, 1))
    out.append(box('pelvisglow', (0, 0.115, hip_z), (0.08, 0.01, 0.02), 'glow', 0, 1))
    return out


def lattice_infantry_torso(P, chest_paint='team'):
    """Chest and head around the turret origin at the waist (P maps local to world)."""
    out = [cyl('waist', P(0, 0, -0.03), P(0, 0, 0.06), 0.1, 'mech', 10)]
    out.append(cyl('waistg', P(0, 0, 0.02), P(0, 0, 0.04), 0.11, 'glow_dim', 10, bevel=0))
    out.append(hull_from('chest', [
        P(-0.14, -0.11, 0.04), P(0.14, -0.11, 0.04), P(-0.13, 0.11, 0.04), P(0.13, 0.11, 0.04),
        P(-0.25, -0.15, 0.26), P(0.25, -0.15, 0.26), P(-0.24, 0.15, 0.24), P(0.24, 0.15, 0.24),
        P(-0.2, -0.14, 0.34), P(0.2, -0.14, 0.34), P(-0.17, 0.09, 0.34), P(0.17, 0.09, 0.34)], 'hull', 0.018, 2))
    out.append(hull_from('chestplate', [
        P(-0.2, -0.13, 0.34), P(0.2, -0.13, 0.34), P(-0.17, 0.09, 0.34), P(0.17, 0.09, 0.34),
        P(-0.23, 0.155, 0.24), P(0.23, 0.155, 0.24), P(-0.12, 0.13, 0.1), P(0.12, 0.13, 0.1),
        P(-0.17, -0.11, 0.365), P(0.17, -0.11, 0.365), P(-0.14, 0.08, 0.365), P(0.14, 0.08, 0.365)], chest_paint, 0.01, 1))
    out.append(box('chestcore', P(0, 0.16, 0.2), (0.05, 0.02, 0.07), 'glow', 0, 1))
    out.append(hull_from('head', [P(-0.07, -0.08, 0.34), P(0.07, -0.08, 0.34), P(-0.06, 0.08, 0.35), P(0.06, 0.08, 0.35),
                                  P(-0.05, 0.12, 0.4), P(0.05, 0.12, 0.4), P(-0.06, -0.08, 0.47), P(0.06, -0.08, 0.47),
                                  P(-0.05, 0.06, 0.48), P(0.05, 0.06, 0.48)], 'hull', 0.012, 1))
    out.append(hull_from('visor', [P(-0.055, 0.085, 0.38), P(0.055, 0.085, 0.38), P(-0.035, 0.125, 0.41), P(0.035, 0.125, 0.41),
                                   P(-0.05, 0.09, 0.425), P(0.05, 0.09, 0.425)], 'glow', 0, 1))
    return out


def crucible_infantry_legs(hip_z=0.52, t=0.15, stagger=0.05):
    out = []
    for sx in (-1, 1):
        out += crucible_leg(f'leg{sx}', (sx * 0.25, -0.05, hip_z), (sx * 0.32, stagger if sx < 0 else -stagger, 0.0), t)
    out.append(box('pelvis', (0, -0.05, hip_z + 0.02), (0.48, 0.3, 0.15), 'hull_dark', 0.02, 1))
    out.append(box('belt', (0, -0.05, hip_z + 0.07), (0.5, 0.32, 0.04), 'mech', 0.008, 1))
    return out


def crucible_infantry_torso(P, top_paint='team'):
    """Hunched block torso with a low head, around the turret origin at the waist."""
    out = [cyl('waist', P(0, 0, -0.03), P(0, 0, 0.06), 0.16, 'mech', 8)]
    out.append(hull_from('torso', [
        P(-0.26, -0.2, 0.04), P(0.26, -0.2, 0.04), P(-0.26, 0.2, 0.04), P(0.26, 0.2, 0.04),
        P(-0.32, -0.22, 0.3), P(0.32, -0.22, 0.3), P(-0.32, 0.26, 0.28), P(0.32, 0.26, 0.28),
        P(-0.3, -0.2, 0.38), P(0.3, -0.2, 0.38), P(-0.28, 0.2, 0.38), P(0.28, 0.2, 0.38)], 'hull', 0.022, 2))
    out.append(hull_from('top', [P(-0.28, -0.19, 0.38), P(0.28, -0.19, 0.38), P(-0.26, 0.19, 0.38), P(0.26, 0.19, 0.38),
                                 P(-0.26, -0.17, 0.405), P(0.26, -0.17, 0.405), P(-0.24, 0.17, 0.405), P(0.24, 0.17, 0.405)], top_paint, 0.008, 1))
    out.append(hull_from('chestplate', [P(-0.28, 0.21, 0.37), P(0.28, 0.21, 0.37), P(-0.3, 0.265, 0.22), P(0.3, 0.265, 0.22),
                                        P(-0.28, 0.24, 0.37), P(0.28, 0.24, 0.37), P(-0.3, 0.295, 0.22), P(0.3, 0.295, 0.22)], top_paint, 0.008, 1))
    out += kit.rivets('chr', P(-0.24, 0.27, 0.33), P(0.24, 0.27, 0.33), 4, 'metal', 0.016)
    out.append(box('head', P(0, 0.26, 0.17), (0.2, 0.14, 0.12), 'hull_dark', 0.015, 1))
    out.append(box('visor', P(0, 0.335, 0.18), (0.15, 0.01, 0.028), 'glow', 0, 1))
    out.append(box('grate', P(0, 0.27, 0.07), (0.24, 0.06, 0.06), 'mech', 0.008, 1))
    out.append(box('grateg', P(0, 0.302, 0.07), (0.18, 0.006, 0.03), 'glow_dim', 0, 1))
    return out


def spider_leg(p, hip, foot, t, kdir, bend, armour='team', faction='lattice'):
    """An insect-style leg for four-legged walkers: the knee lifts up and out along kdir, the
    femur carries an armour plate, the tibia tapers down to a small foot pad."""
    H, F = Vector(hip), Vector(foot)
    K = (H + F) / 2 + Vector(kdir).normalized() * bend
    out = [sphere(p + 'hip', tuple(H), t * 0.75, 'mech', 10, 6)]
    side = Vector((0, 0, 1))
    out.append(beam(p + 'fem', H, K, t * 0.75, t * 0.8, 'mech', side=side))
    out.append(beam(p + 'femplate', H.lerp(K, 0.12) + Vector((0, 0, t * 0.25)), H.lerp(K, 0.9) + Vector((0, 0, t * 0.25)),
                    t * 1.3, t * 0.55, armour, side=(-(K - H).y, (K - H).x, 0), w2=t * 1.0, bevel=0.01))
    out.append(cyl(p + 'piston', tuple(H.lerp(K, 0.2) - Vector((0, 0, t * 0.45))), tuple(K.lerp(F, 0.25) + Vector((0, 0, t * 0.1))),
                   t * 0.14, 'metal', 6, bevel=0))
    out.append(sphere(p + 'knee', tuple(K), t * 0.62, 'metal' if faction == 'lattice' else 'mech', 10, 6))
    pad = F + Vector((0, 0, t * 0.3))
    out.append(beam(p + 'tib', K, pad, t * 0.75, t * 0.8, 'hull', side=side, w2=t * 0.45, d2=t * 0.5))
    out.append(beam(p + 'tibplate', K.lerp(pad, 0.08), K.lerp(pad, 0.55), t * 0.95, t * 0.95, 'hull_dark', side=side, w2=t * 0.7, d2=t * 0.7, bevel=0.008))
    if faction == 'lattice':
        out.append(beam(p + 'tibglow', K.lerp(pad, 0.15) + Vector((0, 0, 0)), K.lerp(pad, 0.45), t * 0.98, t * 0.2, 'glow_dim', side=side, bevel=0))
    out.append(cyl(p + 'pad', tuple(F), tuple(F + Vector((0, 0, t * 0.35))), t * 0.6, 'hull_dark', 8, r2=t * 0.4))
    return out
