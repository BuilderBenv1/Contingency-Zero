"""Helpers for building Contingency Zero unit models in Blender (bpy, run headless).

Conventions, matching the game's procedural models (game/src/View/Meshes):
- Blender is Z-up; the unit faces +Y (exported glTF faces -Z, which is the game's forward).
- Mobile units are authored at "radius 1": about 2.4 long, nose at +1.1; the game scales them.
- Every face gets a paint from PAINTS, written to the "Color" attribute as RGBA:
  alpha 1 = plain colour, alpha 0 = team colour (RGB = brightness), alpha 0.5 = glowing light.
- A model is a "Body" object, optionally a "Turret" object whose origin is its pivot (it yaws
  to aim), and optionally a spinning part named "Rotor_X", "Rotor_Y" or "Rotor_Z" whose origin is
  its pivot; the letter is the spin axis in the GAME's axes (Y up, Z towards the back), so a
  wind turbine's blades facing forward spin about Z, a radar dish about Y.
- Structures are authored in world units, centred on the origin, ground at z = 0. A footprint of
  N tiles is N * 8 world units square; keep the model within N * 8 - 1 across.
- Call bake_detail for every part, then export(path, parts).
- Each part gets a baked detail texture: R = ambient occlusion, G = worn edges, B = grime.
"""
import math, os
import bpy, bmesh
from mathutils import Vector, Matrix

# Faction palettes (see game/src/View/Meshes/Style.cs). Paint names are the same for both,
# so a model script picks its faction with reset('lattice') or reset('crucible').
CRUCIBLE = {
    'hull':      (0.33, 0.30, 0.26, 1.0),
    'hull_dark': (0.20, 0.18, 0.16, 1.0),
    'mech':      (0.11, 0.10, 0.09, 1.0),
    'metal':     (0.40, 0.38, 0.35, 1.0),
    'accent':    (0.46, 0.32, 0.20, 1.0),
    'tread':     (0.10, 0.09, 0.08, 1.0),
    'team':      (0.75, 0.75, 0.75, 0.0),
    'team_dark': (0.45, 0.45, 0.45, 0.0),
    'glow':      (1.00, 0.24, 0.03, 0.5),
    'glass':     (0.16, 0.11, 0.08, 1.0),
    'glow_dim':  (0.60, 0.12, 0.02, 0.5),
    'solar':     (0.11, 0.10, 0.12, 1.0),
}
# Lattice: weathered cold steel-grey armour, slim and bevelled, blue glow.
LATTICE = {
    'hull':      (0.52, 0.54, 0.56, 1.0),
    'hull_dark': (0.34, 0.36, 0.39, 1.0),
    'mech':      (0.19, 0.20, 0.22, 1.0),
    'metal':     (0.40, 0.42, 0.44, 1.0),
    'accent':    (0.40, 0.45, 0.50, 1.0),
    'tread':     (0.15, 0.15, 0.16, 1.0),
    'team':      (0.80, 0.80, 0.80, 0.0),
    'team_dark': (0.50, 0.50, 0.50, 0.0),
    'glow':      (0.10, 0.42, 1.00, 0.5),
    'glass':     (0.10, 0.16, 0.22, 1.0),
    'glow_dim':  (0.05, 0.20, 0.55, 0.5),
    'solar':     (0.08, 0.12, 0.20, 1.0),
}
PAINTS = dict(CRUCIBLE)


def reset(faction='crucible'):
    """Empties the scene and sets the palette for 'lattice' or 'crucible'."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    PAINTS.clear(); PAINTS.update(LATTICE if faction == 'lattice' else CRUCIBLE)
    for name in PAINTS:
        m = bpy.data.materials.new(name)
        m.diffuse_color = PAINTS[name][:3] + (1.0,)


def mat(obj, paint):
    """Gives every face of obj the paint (as a material slot, turned into vertex colours later)."""
    m = bpy.data.materials[paint]
    obj.data.materials.clear()
    obj.data.materials.append(m)
    return obj


def _obj(name, bm, paint, parent=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    mat(o, paint)
    if parent: o.parent = parent
    return o


def box(name, centre, size, paint, bevel=0.02, segs=2):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(centre), verts=bm.verts)
    o = _obj(name, bm, paint)
    if bevel: add_bevel(o, bevel, segs)
    return o


def hull_from(name, points, paint, bevel=0.02, segs=2):
    """Convex hull of a point cloud: chamfered armour blocks."""
    bm = bmesh.new()
    vs = [bm.verts.new(Vector(p)) for p in points]
    bmesh.ops.convex_hull(bm, input=vs)
    bmesh.ops.dissolve_limit(bm, angle_limit=0.01, verts=bm.verts, edges=bm.edges)
    o = _obj(name, bm, paint)
    if bevel: add_bevel(o, bevel, segs)
    return o


def prism_x(name, profile_yz, x0, x1, paint, bevel=0.02, segs=2):
    """Extrudes a (y, z) outline along X from x0 to x1 (hull sides, glacis plates)."""
    bm = bmesh.new()
    a = [bm.verts.new((x0, y, z)) for y, z in profile_yz]
    b = [bm.verts.new((x1, y, z)) for y, z in profile_yz]
    n = len(profile_yz)
    bm.faces.new(a[::-1]); bm.faces.new(b)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = _obj(name, bm, paint)
    if bevel: add_bevel(o, bevel, segs)
    return o


def cyl(name, a, b, r, paint, verts=16, r2=None, bevel=0.01, cap_fill=True):
    """Cylinder (or cone if r2) from point a to point b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap_fill, segments=verts, radius1=r, radius2=r if r2 is None else r2, depth=d.length)
    rot = d.normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation((a + b) / 2) @ rot, verts=bm.verts)
    o = _obj(name, bm, paint)
    if bevel: add_bevel(o, bevel, 1)
    return o


def sphere(name, c, r, paint, segs=10, rings=6):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=r)
    bmesh.ops.translate(bm, vec=Vector(c), verts=bm.verts)
    o = _obj(name, bm, paint)
    for p in o.data.polygons: p.use_smooth = True
    return o


def add_bevel(o, width, segs=2):
    m = o.modifiers.new('bevel', 'BEVEL')
    m.width = width; m.segments = segs; m.limit_method = 'ANGLE'; m.angle_limit = math.radians(35)
    m.harden_normals = True
    return m


def inset_panels(o, thickness=0.025, depth=0.012, face_filter=None):
    """Panel lines: insets selected faces and pushes them in a touch, like bolted-on plates."""
    bm = bmesh.new(); bm.from_mesh(o.data)
    faces = [f for f in bm.faces if face_filter is None or face_filter(f)]
    r = bmesh.ops.inset_individual(bm, faces=faces, thickness=thickness, depth=-depth)
    bm.to_mesh(o.data); bm.free()


def rivets(name, a, b, n, paint, r=0.022, normal=(0, 0, 1)):
    objs = []
    a, b = Vector(a), Vector(b)
    for i in range(n):
        t = i / (n - 1) if n > 1 else 0.5
        objs.append(sphere(f'{name}{i}', a.lerp(b, t), r, paint, 8, 4))
    return objs


def join(name, objs, parent_origin=None):
    """Applies modifiers, joins objects into one, and sets the origin (pivot)."""
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    for o in objs:
        bpy.context.view_layer.objects.active = o
        for m in list(o.modifiers):
            bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name; o.data.name = name
    if parent_origin is not None:
        bpy.context.scene.cursor.location = Vector(parent_origin)
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return o


def paint_vertex_colours(o):
    """Turns material slots into the game's RGBA vertex colour convention, then drops materials."""
    me = o.data
    attr = me.color_attributes.new('Color', 'FLOAT_COLOR', 'CORNER')
    for poly in me.polygons:
        c = PAINTS[me.materials[poly.material_index].name]
        for li in poly.loop_indices:
            attr.data[li].color = c
    me.color_attributes.active_color = attr


def unwrap(o):
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(50), island_margin=0.0, area_weight=0.0)
    # Same texel density everywhere, then pack tightly.
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')


def _bake_material(o, img, kind):
    """A temporary material on every slot whose emission is the thing being baked."""
    m = bpy.data.materials.new('bake_' + kind)
    m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    em = nt.nodes.new('ShaderNodeEmission')
    tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img
    nt.nodes.active = tex
    if kind == 'edge':
        # Worn edges: where a rounded (bevel) normal differs from the true one.
        bev = nt.nodes.new('ShaderNodeBevel'); bev.samples = 8; bev.inputs['Radius'].default_value = 0.03
        geo = nt.nodes.new('ShaderNodeNewGeometry')
        dot = nt.nodes.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
        nt.links.new(bev.outputs['Normal'], dot.inputs[0]); nt.links.new(geo.outputs['Normal'], dot.inputs[1])
        ramp = nt.nodes.new('ShaderNodeMapRange')
        ramp.inputs['From Min'].default_value = 0.99; ramp.inputs['From Max'].default_value = 0.88
        nt.links.new(dot.outputs['Value'], ramp.inputs['Value'])
        nt.links.new(ramp.outputs['Result'], em.inputs['Color'])
    elif kind == 'grime':
        # Dirt gathers low down and in creases: noise, weighted by height and by AO.
        ao = nt.nodes.new('ShaderNodeAmbientOcclusion'); ao.inputs['Distance'].default_value = 0.25
        noise = nt.nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value = 7.0
        noise.inputs['Detail'].default_value = 8.0
        coord = nt.nodes.new('ShaderNodeTexCoord')
        nt.links.new(coord.outputs['Object'], noise.inputs['Vector'])
        sep = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(coord.outputs['Object'], sep.inputs[0])
        low = nt.nodes.new('ShaderNodeMapRange')
        low.inputs['From Min'].default_value = 0.6; low.inputs['From Max'].default_value = 0.0
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


def bake_detail(o, path, size=512, others=()):
    """Bakes R = AO, G = worn edges, B = grime for o into one PNG at path."""
    import numpy as np
    scn = bpy.context.scene
    scn.render.engine = 'CYCLES'
    scn.cycles.device = 'CPU'
    scn.cycles.samples = 48
    scn.render.bake.margin = 4
    chans = []
    saved = [s.material for s in o.material_slots]
    for kind in ('ao', 'edge', 'grime'):
        img = bpy.data.images.new(f'{o.name}_{kind}', size, size, float_buffer=False)
        if kind == 'ao':
            m = bpy.data.materials.new('bake_ao'); m.use_nodes = True
            tex = m.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = img
            m.node_tree.nodes.active = tex
        else:
            m = _bake_material(o, img, kind)
        for s in o.material_slots: s.material = m
        # Only o is baked; the rest of the scene still occludes it.
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True); bpy.context.view_layer.objects.active = o
        if kind == 'ao':
            scn.world = scn.world or bpy.data.worlds.new('w')
            scn.world.light_settings.distance = 0.6
            bpy.ops.object.bake(type='AO')
        else:
            bpy.ops.object.bake(type='EMIT')
        px = np.array(img.pixels[:]).reshape(size, size, 4)
        chans.append(px[:, :, 0])
    for s, m in zip(o.material_slots, saved): s.material = m
    out = np.stack(chans + [np.ones((size, size))], axis=-1)
    img = bpy.data.images.new(o.name + '_detail', size, size)
    img.pixels = out.flatten().tolist()
    img.filepath_raw = path; img.file_format = 'PNG'; img.save()


def export(path, objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, use_selection=True, export_format='GLB',
                              export_materials='NONE', export_vertex_color='ACTIVE',
                              export_apply=True, export_yup=True, export_texcoords=True, export_normals=True)
