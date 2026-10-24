"""Shared parts for the factory models (bot labs, plants, shipyards, assemblies, gantries).

Structures are authored in world units in Blender axes: x = right, y = north (the back of the
factory), z = up. The door is the south side (-y), where finished units roll out; the sim spawns
each nanoframe at the factory's centre, so build pads are centred on the origin.
"""
import math, os
import bpy, bmesh
from mathutils import Vector, Matrix
import kit
from kit import box, hull_from, prism_x, cyl, sphere


def V(*a):
    return Vector(a)


def two_tone(o, side, top, thresh=0.7):
    """Gives faces facing up (normal z > thresh) the top paint and the rest the side paint."""
    me = o.data
    me.materials.clear()
    me.materials.append(bpy.data.materials[side])
    me.materials.append(bpy.data.materials[top])
    for p in me.polygons:
        p.material_index = 1 if p.normal.z > thresh else 0
    return o


def slab(name, c, size, side, top=None, chamfer=0.0, bevel=0.06, segs=1):
    """A block whose top edges are chamfered in by `chamfer` (Lattice bevelled slab, or a plain
    box when chamfer is 0). The up-facing top takes the top paint."""
    cx, cy, cz = c
    sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
    ch = min(chamfer, sx * 0.9, sy * 0.9, sz * 1.5)
    pts = []
    for x in (-sx, sx):
        for y in (-sy, sy):
            pts.append((cx + x, cy + y, cz - sz))
            if ch > 0:
                pts.append((cx + x, cy + y, cz + sz - ch))
                pts.append((cx + x - math.copysign(ch, x), cy + y - math.copysign(ch, y), cz + sz))
            else:
                pts.append((cx + x, cy + y, cz + sz))
    o = hull_from(name, pts, side, bevel=bevel, segs=segs)
    if top: two_tone(o, side, top)
    return o


def beam(name, a, b, w, h, paint, bevel=0.03, up=(0, 0, 1)):
    """A rectangular beam w wide and h tall from a to b."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    u = Vector(up)
    if abs(d.dot(u)) > 0.95: u = Vector((0, 1, 0)) if abs(d.y) < 0.9 else Vector((1, 0, 0))
    s = d.cross(u).normalized(); u = s.cross(d).normalized()
    pts = [p + s * (i * w / 2) + u * (j * h / 2) for p in (a, b) for i in (-1, 1) for j in (-1, 1)]
    return hull_from(name, pts, paint, bevel=bevel, segs=1)


def ring(name, c, r_out, r_in, h, paint, segs=24, rot=0.0):
    """A flat annulus (glow ring round a build pad)."""
    bm = bmesh.new()
    cx, cy, cz = c
    rings = []
    for z in (cz, cz + h):
        for r in (r_out, r_in):
            rings.append([bm.verts.new((cx + math.cos(rot + 2 * math.pi * i / segs) * r,
                                        cy + math.sin(rot + 2 * math.pi * i / segs) * r, z)) for i in range(segs)])
    bo, bi, to, ti = rings
    for i in range(segs):
        j = (i + 1) % segs
        bm.faces.new((to[i], to[j], ti[j], ti[i]))      # top
        bm.faces.new((bo[j], bo[i], bi[i], bi[j]))      # bottom
        bm.faces.new((bo[i], bo[j], to[j], to[i]))      # outer
        bm.faces.new((bi[j], bi[i], ti[i], ti[j]))      # inner
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return kit._obj(name, bm, paint)


def truss(name, a, b, w, h, paint, n=6, r=0.09, chord='mech', chord_r=None):
    """An open girder: four chords from a to b with zig-zag diagonals between them."""
    a, b = Vector(a), Vector(b)
    d = b - a; dn = d.normalized()
    u = Vector((0, 0, 1))
    if abs(dn.z) > 0.9: u = Vector((0, 1, 0))
    s = dn.cross(u).normalized(); u = s.cross(dn).normalized()
    cr = chord_r or r * 1.6
    objs = []
    corners = [s * (i * w / 2) + u * (j * h / 2) for i, j in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for k, c in enumerate(corners):
        objs.append(cyl(f'{name}_ch{k}', a + c, b + c, cr, chord, 6, bevel=0))
    for i in range(n):
        p0 = a + d * (i / n); p1 = a + d * ((i + 1) / n)
        for f in range(4):
            c0, c1 = corners[f], corners[(f + 1) % 4]
            if i % 2: objs.append(cyl(f'{name}_d{i}_{f}', p0 + c0, p1 + c1, r, paint, 5, bevel=0))
            else: objs.append(cyl(f'{name}_d{i}_{f}', p0 + c1, p1 + c0, r, paint, 5, bevel=0))
    return objs


def studs(name, a, b, n, paint='metal', r=0.12):
    """Rivet studs (low-poly half-spheres) from a to b on an upward face."""
    a, b = Vector(a), Vector(b)
    out = []
    for i in range(n):
        t = i / (n - 1) if n > 1 else 0.5
        out.append(sphere(f'{name}{i}', a.lerp(b, t), r, paint, 6, 3))
    return out


def stack(name, base, r, h, hot=True, band='accent', faction='crucible'):
    """A smokestack: tapered stack, banded, with a rimmed mouth that glows when hot."""
    x, y, z = base
    sides = 10
    o = [cyl(f'{name}_s', (x, y, z), (x, y, z + h), r, 'metal', sides, r2=r * 0.86, bevel=0.03),
         cyl(f'{name}_rim', (x, y, z + h - r * 0.2), (x, y, z + h + r * 0.35), r * 1.1, 'mech', sides, bevel=0.03),
         cyl(f'{name}_mouth', (x, y, z + h + r * 0.35), (x, y, z + h + r * 0.37), r * 0.85, 'glow' if hot else 'tread', sides, bevel=0),
         cyl(f'{name}_band', (x, y, z + h * 0.35), (x, y, z + h * 0.5), r * 1.06, band, sides, bevel=0.02),
         cyl(f'{name}_foot', (x, y, z), (x, y, z + r * 0.6), r * 1.3, 'hull_dark', sides, bevel=0.04)]
    return o


def nano_arm(name, base, target, size, faction, reach=1.0, hang=False):
    """A nanolathe crane: turntable, raised upper arm, forearm reaching to `target` and a
    glowing emitter aimed at it. `hang` mounts it upside down under a gantry."""
    base, target = Vector(base), Vector(target)
    t = 1.0 if faction == 'lattice' else 1.35
    upv = Vector((0, 0, -1 if hang else 1))
    flat = Vector((target.x - base.x, target.y - base.y, 0))
    fwd = flat.normalized() if flat.length > 1e-4 else Vector((0, 1, 0))
    side = fwd.cross(Vector((0, 0, 1))).normalized()
    s = size
    shoulder = base + upv * 0.2 * s
    elbow = base + upv * 0.95 * s - fwd * 0.2 * s
    wrist = base + upv * 0.75 * s + fwd * 0.75 * s * reach
    aim = (target - wrist).normalized()
    tip = wrist + aim * 0.32 * s
    o = []
    o.append(cyl(f'{name}_tt', base, base + upv * 0.18 * s, 0.32 * s * t, 'mech', 10, r2=0.27 * s * t, bevel=0.02))
    o.append(cyl(f'{name}_tt2', base + upv * 0.18 * s, base + upv * 0.24 * s, 0.22 * s * t, 'metal', 10, bevel=0))
    o.append(beam(f'{name}_ua', shoulder, elbow, 0.2 * s * t, 0.24 * s * t, 'team', 0.02))
    o.append(beam(f'{name}_fa', elbow, wrist, 0.14 * s * t, 0.16 * s * t, 'hull' if faction == 'lattice' else 'accent', 0.02))
    o.append(box(f'{name}_el', elbow, (0.26 * s * t,) * 3, 'mech', 0.02, 1))
    o.append(cyl(f'{name}_wr', wrist - side * 0.11 * s * t, wrist + side * 0.11 * s * t, 0.1 * s * t, 'mech', 8, bevel=0))
    o.append(cyl(f'{name}_em', wrist, tip, 0.11 * s * t, 'metal', 8, r2=0.07 * s * t, bevel=0.01))
    o.append(sphere(f'{name}_gl', tip + aim * 0.05 * s, 0.1 * s * t, 'glow', 8, 5))
    o.append(cyl(f'{name}_gr', wrist + aim * 0.12 * s, wrist + aim * 0.17 * s, 0.13 * s * t, 'glow_dim', 8, bevel=0))
    if faction == 'lattice':
        # Slim fins along the upper arm and a sensor eye on the elbow.
        o.append(box(f'{name}_eye', elbow + fwd * 0.135 * s, (0.12 * s, 0.02 * s, 0.05 * s), 'glow', 0, 1))
    else:
        # Hydraulic ram and a counterweight block.
        o.append(cyl(f'{name}_ram', shoulder - fwd * 0.12 * s + upv * 0.02 * s, elbow.lerp(wrist, 0.35), 0.045 * s, 'metal', 6, bevel=0))
        o.append(box(f'{name}_cw', base + upv * 0.42 * s - fwd * 0.42 * s, (0.3 * s, 0.26 * s, 0.26 * s), 'hull_dark', 0.03, 1))
        o.append(beam(f'{name}_cwb', shoulder, base + upv * 0.42 * s - fwd * 0.38 * s, 0.1 * s, 0.1 * s, 'mech', 0.01))
    return o


def pad_round(name, c, r, faction='lattice', segs=24):
    """Lattice build pad: a round plate, segmented glow rim, guide lines and an inner ring."""
    cx, cy, cz = c
    o = [cyl(f'{name}_plate', (cx, cy, cz), (cx, cy, cz + 0.14), r, 'mech', segs, bevel=0.04),
         cyl(f'{name}_inner', (cx, cy, cz + 0.14), (cx, cy, cz + 0.18), r * 0.62, 'hull_dark', segs, bevel=0.02),
         ring(f'{name}_rim', (cx, cy, cz + 0.14), r + 0.3, r - 0.25, 0.1, 'glow', segs),
         ring(f'{name}_mid', (cx, cy, cz + 0.18), r * 0.64, r * 0.58, 0.04, 'glow_dim', segs)]
    for i in range(8):
        a = i * math.pi / 4 + math.pi / 8
        p0 = V(cx + math.cos(a) * r * 0.66, cy + math.sin(a) * r * 0.66, cz + 0.16)
        p1 = V(cx + math.cos(a) * r * 0.92, cy + math.sin(a) * r * 0.92, cz + 0.16)
        o.append(beam(f'{name}_gl{i}', p0, p1, 0.18, 0.05, 'metal', 0))
    return o


def pad_square(name, c, half, glow='glow'):
    """Crucible build pad: a heavy plate with glowing edge channels, hazard border and corner bollards."""
    cx, cy, cz = c
    o = [box(f'{name}_plate', (cx, cy, cz + 0.08), (half * 2, half * 2, 0.16), 'hull_dark', 0.04, 1),
         box(f'{name}_core', (cx, cy, cz + 0.18), (half * 1.3, half * 1.3, 0.06), 'mech', 0.02, 1)]
    for sd in (-1, 1):
        o.append(box(f'{name}_gx{sd}', (cx, cy + sd * half * 0.88, cz + 0.2), (half * 1.7, 0.28, 0.06), glow, 0, 1))
        o.append(box(f'{name}_gy{sd}', (cx + sd * half * 0.88, cy, cz + 0.2), (0.28, half * 1.7, 0.06), glow, 0, 1))
    # Hazard border: alternating accent and dark tiles round the outside.
    n = 9
    for i in range(n):
        p = -half + (i + 0.5) * 2 * half / n
        pnt = 'accent' if i % 2 == 0 else 'tread'
        for sd in (-1, 1):
            o.append(box(f'{name}_hx{sd}_{i}', (cx + p, cy + sd * (half + 0.3), cz + 0.06), (2 * half / n, 0.5, 0.12), pnt, 0, 1))
            o.append(box(f'{name}_hy{sd}_{i}', (cx + sd * (half + 0.3), cy + p, cz + 0.06), (0.5, 2 * half / n, 0.12), pnt, 0, 1))
    for sx in (-1, 1):
        for sy in (-1, 1):
            o.append(cyl(f'{name}_bol{sx}{sy}', (cx + sx * (half + 0.3), cy + sy * (half + 0.3), cz), (cx + sx * (half + 0.3), cy + sy * (half + 0.3), cz + 0.7), 0.25, 'accent', 8, bevel=0.03))
            o.append(cyl(f'{name}_bl{sx}{sy}', (cx + sx * (half + 0.3), cy + sy * (half + 0.3), cz + 0.7), (cx + sx * (half + 0.3), cy + sy * (half + 0.3), cz + 0.8), 0.18, glow, 8, bevel=0))
    return o


def chevrons(name, y, half_w, z, n=6, paint='glow', depth=0.6):
    """A row of glowing arrow chevrons pointing out of the door (-y)."""
    o = []
    for i in range(n):
        x = -half_w + (i + 0.5) * 2 * half_w / n
        for sd in (-1, 1):
            o.append(beam(f'{name}{i}{sd}', (x, y, z), (x + sd * 0.45, y + depth, z), 0.16, 0.05, paint, 0))
    return o


def hazard_strip(name, x0, x1, y, z, w=0.3, h=0.5, n=10, face_y=-1):
    """Alternating accent/dark blocks along a lintel face."""
    o = []
    for i in range(n):
        x = x0 + (i + 0.5) * (x1 - x0) / n
        o.append(box(f'{name}{i}', (x, y, z), ((x1 - x0) / n, w, h), 'accent' if i % 2 == 0 else 'tread', 0, 1))
    return o


def pipe_run(name, pts, r, paint='metal', bracket='mech'):
    """A pipe through a list of points, with collars at the joints."""
    o = []
    pts = [Vector(p) for p in pts]
    for i in range(len(pts) - 1):
        o.append(cyl(f'{name}_p{i}', pts[i], pts[i + 1], r, paint, 8, bevel=0))
    for i, p in enumerate(pts):
        o.append(sphere(f'{name}_j{i}', p, r * 1.3, bracket, 8, 4))
    return o


# ---- Baking: the kit's detail bake is tuned for radius-1 units; structures are 20+ units across,
# so scale its AO distance, edge radius and grime height/noise to the building's size. ----

def _detail_material(img, kind, s):
    m = bpy.data.materials.new('bake_' + kind)
    m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    em = nt.nodes.new('ShaderNodeEmission')
    tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img
    nt.nodes.active = tex
    if kind == 'edge':
        bev = nt.nodes.new('ShaderNodeBevel'); bev.samples = 8; bev.inputs['Radius'].default_value = 0.03 * s
        geo = nt.nodes.new('ShaderNodeNewGeometry')
        dot = nt.nodes.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
        nt.links.new(bev.outputs['Normal'], dot.inputs[0]); nt.links.new(geo.outputs['Normal'], dot.inputs[1])
        ramp = nt.nodes.new('ShaderNodeMapRange')
        ramp.inputs['From Min'].default_value = 0.99; ramp.inputs['From Max'].default_value = 0.88
        nt.links.new(dot.outputs['Value'], ramp.inputs['Value'])
        nt.links.new(ramp.outputs['Result'], em.inputs['Color'])
    else:
        ao = nt.nodes.new('ShaderNodeAmbientOcclusion'); ao.inputs['Distance'].default_value = 0.25 * s
        noise = nt.nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value = 7.0 / s
        noise.inputs['Detail'].default_value = 8.0
        coord = nt.nodes.new('ShaderNodeTexCoord')
        nt.links.new(coord.outputs['Object'], noise.inputs['Vector'])
        sep = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(coord.outputs['Object'], sep.inputs[0])
        low = nt.nodes.new('ShaderNodeMapRange')
        low.inputs['From Min'].default_value = 0.6 * s; low.inputs['From Max'].default_value = 0.0
        nt.links.new(sep.outputs['Z'], low.inputs['Value'])
        inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
        nt.links.new(ao.outputs['AO'], inv.inputs[1])
        a1 = nt.nodes.new('ShaderNodeMath'); a1.operation = 'ADD'
        nt.links.new(inv.outputs['Value'], a1.inputs[0]); nt.links.new(low.outputs['Result'], a1.inputs[1])
        mul = nt.nodes.new('ShaderNodeMath'); mul.operation = 'MULTIPLY'; mul.use_clamp = True
        nt.links.new(a1.outputs['Value'], mul.inputs[0]); nt.links.new(noise.outputs['Fac'], mul.inputs[1])
        mul2 = nt.nodes.new('ShaderNodeMath'); mul2.operation = 'MULTIPLY'; mul2.use_clamp = True
        mul2.inputs[1].default_value = 1.2
        nt.links.new(mul.outputs['Value'], mul2.inputs[0])
        nt.links.new(mul2.outputs['Value'], em.inputs['Color'])
    nt.links.new(em.outputs['Emission'], out.inputs['Surface'])
    return m


def bake(o, path, size=1024, s=4.0, samples=32):
    """kit.bake_detail with AO distance, worn-edge radius and grime scaled by s."""
    import numpy as np
    scn = bpy.context.scene
    scn.render.engine = 'CYCLES'
    scn.cycles.device = 'CPU'
    scn.cycles.samples = samples
    scn.render.bake.margin = 4
    chans = []
    saved = [sl.material for sl in o.material_slots]
    for kind in ('ao', 'edge', 'grime'):
        img = bpy.data.images.new(f'{o.name}_{kind}', size, size, float_buffer=False)
        if kind == 'ao':
            m = bpy.data.materials.new('bake_ao'); m.use_nodes = True
            tex = m.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = img
            m.node_tree.nodes.active = tex
        else:
            m = _detail_material(img, kind, s)
        for sl in o.material_slots: sl.material = m
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True); bpy.context.view_layer.objects.active = o
        if kind == 'ao':
            scn.world = scn.world or bpy.data.worlds.new('w')
            scn.world.light_settings.distance = 0.6 * s
            bpy.ops.object.bake(type='AO')
        else:
            bpy.ops.object.bake(type='EMIT')
        px = np.array(img.pixels[:]).reshape(size, size, 4)
        chans.append(px[:, :, 0])
    for sl, m in zip(o.material_slots, saved): sl.material = m
    out = np.stack(chans + [np.ones((size, size))], axis=-1)
    img = bpy.data.images.new(o.name + '_detail', size, size)
    img.pixels = out.flatten().tolist()
    img.filepath_raw = path; img.file_format = 'PNG'; img.save()


def finish(uid, out_dir, body, rotor=None, rotor_name=None, rotor_pivot=None, s=4.0):
    """Joins, paints, unwraps, bakes and exports. Prints the triangle count."""
    B = kit.join('Body', body)
    parts = [B]
    R = None
    if rotor:
        R = kit.join(rotor_name, rotor, parent_origin=rotor_pivot)
        parts.append(R)
    for o in parts:
        kit.paint_vertex_colours(o)
        kit.unwrap(o)
    os.makedirs(out_dir, exist_ok=True)
    fast = os.environ.get('FAST') == '1'   # quick look while iterating: tiny, noisy bakes
    bake(B, os.path.join(out_dir, f'{uid}_body.png'), 256 if fast else 1024, s, 4 if fast else 32)
    if R: bake(R, os.path.join(out_dir, f'{uid}_rotor.png'), 128 if fast else 512, s * 0.5, 4 if fast else 32)
    kit.export(os.path.join(out_dir, f'{uid}.glb'), parts)
    tris = sum(len(p.vertices) - 2 for o in parts for p in o.data.polygons)
    lo = [min(v.co[i] for o in parts for v in o.data.vertices) for i in range(3)]
    hi = [max(v.co[i] for o in parts for v in o.data.vertices) for i in range(3)]
    print('tris', tris, 'bounds', [round(x, 2) for x in lo], [round(x, 2) for x in hi])
    return parts
