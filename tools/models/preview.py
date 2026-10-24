"""Renders a studio preview of a built model: python tools/models/preview.py <dir> <unit_id> <out.png> [team r,g,b]

Imports <dir>/<unit_id>.glb with its baked detail maps and shades it roughly as the game does:
vertex colour (team areas take the team colour), times occlusion, lighter on worn edges,
darker where the grime is. Cycles on the CPU, three-quarter view, a ground plane with shadows.
"""
import sys, os, math
import bpy
from mathutils import Vector

d, uid, out = sys.argv[1:4]
team = tuple(float(x) for x in sys.argv[4].split(',')) if len(sys.argv) > 4 else (0.85, 0.32, 0.08)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.join(d, uid + '.glb'))

def material(img_path):
    m = bpy.data.materials.new('m'); m.use_nodes = True
    nt = m.node_tree; bsdf = nt.nodes['Principled BSDF']
    attr = nt.nodes.new('ShaderNodeVertexColor'); attr.layer_name = 'Color'
    img = nt.nodes.new('ShaderNodeTexImage'); img.image = bpy.data.images.load(img_path)
    img.image.colorspace_settings.name = 'Non-Color'
    sep = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(img.outputs['Color'], sep.inputs[0])
    # team where alpha < 0.25
    lt = nt.nodes.new('ShaderNodeMath'); lt.operation = 'LESS_THAN'; lt.inputs[1].default_value = 0.25
    nt.links.new(attr.outputs['Alpha'], lt.inputs[0])
    teamc = nt.nodes.new('ShaderNodeMix'); teamc.data_type = 'RGBA'; teamc.blend_type = 'MULTIPLY'
    teamc.inputs['Factor'].default_value = 1.0
    nt.links.new(attr.outputs['Color'], teamc.inputs[6]); teamc.inputs[7].default_value = team + (1,)
    pick = nt.nodes.new('ShaderNodeMix'); pick.data_type = 'RGBA'
    nt.links.new(lt.outputs['Value'], pick.inputs['Factor'])
    nt.links.new(attr.outputs['Color'], pick.inputs[6]); nt.links.new(teamc.outputs[2], pick.inputs[7])
    # worn edges to steel
    edge = nt.nodes.new('ShaderNodeMix'); edge.data_type = 'RGBA'
    nt.links.new(sep.outputs['Green'], edge.inputs['Factor'])
    nt.links.new(pick.outputs[2], edge.inputs[6]); edge.inputs[7].default_value = (0.55, 0.53, 0.5, 1)
    grime = nt.nodes.new('ShaderNodeMix'); grime.data_type = 'RGBA'
    gm = nt.nodes.new('ShaderNodeMath'); gm.operation = 'MULTIPLY'; gm.inputs[1].default_value = 0.6
    nt.links.new(sep.outputs['Blue'], gm.inputs[0]); nt.links.new(gm.outputs[0], grime.inputs['Factor'])
    nt.links.new(edge.outputs[2], grime.inputs[6]); grime.inputs[7].default_value = (0.12, 0.09, 0.07, 1)
    ao = nt.nodes.new('ShaderNodeMix'); ao.data_type = 'RGBA'; ao.blend_type = 'MULTIPLY'; ao.inputs['Factor'].default_value = 1
    nt.links.new(grime.outputs[2], ao.inputs[6]); nt.links.new(sep.outputs['Red'], ao.inputs[7])
    nt.links.new(ao.outputs[2], bsdf.inputs['Base Color'])
    bsdf.inputs['Metallic'].default_value = 0.4; bsdf.inputs['Roughness'].default_value = 0.55
    # glow
    gt = nt.nodes.new('ShaderNodeMath'); gt.operation = 'COMPARE'; gt.inputs[1].default_value = 0.5; gt.inputs[2].default_value = 0.1
    nt.links.new(attr.outputs['Alpha'], gt.inputs[0])
    nt.links.new(attr.outputs['Color'], bsdf.inputs['Emission Color'])
    gs = nt.nodes.new('ShaderNodeMath'); gs.operation = 'MULTIPLY'; gs.inputs[1].default_value = 6
    nt.links.new(gt.outputs[0], gs.inputs[0]); nt.links.new(gs.outputs[0], bsdf.inputs['Emission Strength'])
    return m

for o in bpy.context.scene.objects:
    if o.type == 'MESH':
        part = 'body' if o.name.startswith('Body') else 'turret'
        o.data.materials.clear(); o.data.materials.append(material(os.path.join(d, f'{uid}_{part}.png')))

bpy.ops.mesh.primitive_plane_add(size=30)
g = bpy.context.object; gm = bpy.data.materials.new('g'); gm.use_nodes = True
gm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.16, 0.17, 0.12, 1)
g.data.materials.append(gm)
cam = bpy.data.cameras.new('c'); cam.lens = 70
co = bpy.data.objects.new('c', cam); bpy.context.scene.collection.objects.link(co)
co.location = Vector((3.6, 3.9, 3.1)); co.rotation_euler = (Vector((0, 0.1, 0.35)) - co.location).to_track_quat('-Z', 'Y').to_euler()
bpy.context.scene.camera = co
sun = bpy.data.lights.new('s', 'SUN'); sun.energy = 4; sun.angle = 0.1
so = bpy.data.objects.new('s', sun); bpy.context.scene.collection.objects.link(so); so.rotation_euler = (math.radians(50), 0, math.radians(-35))
w = bpy.data.worlds.new('w'); bpy.context.scene.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.35, 0.38, 0.42, 1); w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.6
s = bpy.context.scene; s.render.engine = 'CYCLES'; s.cycles.samples = 64; s.cycles.device = 'CPU'
s.render.resolution_x, s.render.resolution_y = 1200, 800
s.view_settings.view_transform = 'Standard'
s.render.filepath = out
bpy.ops.render.render(write_still=True)
