"""Lattice "Oracle Mast" radar. Run: python <this> <out_dir>

A hex footing with a team top, a tall slender mast with glowing bands, a sensor pod and three
team-painted stays, topped by a spinning head (Rotor_Y: spins flat about the vertical) with a
team-faced parabolic dish on an arm, a glowing feed horn and a counterweight.
Footprint 1 (8 units): kept inside 7 across. Rotor pivot at 9.2 as the procedural model.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('lattice')
body = []
body.append(ep.ngon('footing', (0, 0), 2.6, 2.0, 0.0, 0.7, 6, 'hull', phase=0, bevel=0.05, segs=2))
body.append(ep.ngon('footteam', (0, 0), 2.0, 1.4, 0.7, 0.85, 6, 'team', phase=0, bevel=0.03))
body.append(ep.ring('footglow', (0, 0), 2.48, 2.25, 0.25, 0.33, 'glow', 6, phase=0))
body.append(box('cab', (0, -1.6, 0.75), (1.4, 0.8, 0.9), 'hull_dark', 0.04))
body.append(box('cablamp', (0, -2.02, 0.95), (0.9, 0.04, 0.08), 'glow', 0, 1))
body.append(ep.ngon('mast', (0, 0), 0.5, 0.24, 0.85, 9.0, 6, 'hull', phase=0, bevel=0.02))
for z in (3.0, 5.6, 7.6):
    r = 0.5 - (z - 0.85) / 8.15 * 0.26 + 0.03
    body.append(ep.ngon(f'band{z}', (0, 0), r, r - 0.01, z, z + 0.22, 6, 'glow', phase=0, bevel=0))
# Sensor pod half way up.
pod = hull_from('pod', [(sx * 0.55, sy * 0.45, z) for sx in (-1, 1) for sy in (-1, 1) for z in (4.1, 4.9)] + [(0, 0.75, 4.5), (0, -0.75, 4.5)], 'hull_dark', 0.03)
body.append(pod)
body.append(box('podeye', (0, -0.62, 4.55), (0.5, 0.05, 0.1), 'glow', 0, 1))
for k in range(3):
    a = k * 2 * math.pi / 3 + math.pi / 2
    ca, sa = math.cos(a), math.sin(a)
    body.append(ep.beam(f'stay{k}', (ca * 2.3, sa * 2.3, 0.7), (ca * 0.32, sa * 0.32, 5.6), 0.3, 0.22, 'team', 0.02, 1, w2=0.18, h2=0.14))
    body.append(box(f'anchor{k}', (ca * 2.3, sa * 2.3, 0.8), (0.4, 0.4, 0.3), 'mech', 0.03))
body.append(ep.ngon('bearing', (0, 0), 0.42, 0.38, 8.8, 9.2, 6, 'mech', phase=0))

PIV = (0, 0, 9.2)
P = lambda x, y, z: Vector((PIV[0] + x, PIV[1] + y, PIV[2] + z))
rot = []
rot.append(ep.ngon('hub', (0, 0), 0.45, 0.35, PIV[2], PIV[2] + 0.45, 6, 'hull_dark', phase=0))
rot.append(ep.beam('arm', tuple(P(0, -0.7, 0.3)), tuple(P(0, 0.75, 0.7)), 0.32, 0.26, 'hull', 0.02))
facing = Vector((0, 1, 0.45)).normalized()
dc = P(0, 0.8, 0.9)
rot.append(ep.dish('dish', tuple(dc), tuple(facing), 2.0, 0.55, 'team', 0.08, 5, 20))
rot.append(ep.dish('dishback', tuple(dc - facing * 0.09), tuple(facing), 1.98, 0.55, 'hull', 0.04, 4, 20))
rot.append(ep.ring('dishrim', (0, 0), 2.08, 1.92, -0.05, 0.08, 'hull_dark', 20))
rot[-1].data.transform(Matrix.Translation(dc + facing * 0.55) @ facing.to_track_quat('Z', 'Y').to_matrix().to_4x4())
# Feed horn on three struts.
horn = dc + facing * 1.5
for k in range(3):
    a = k * 2 * math.pi / 3
    q = facing.to_track_quat('Z', 'Y').to_matrix()
    rim = dc + facing * 0.5 + q @ Vector((math.cos(a) * 1.8, math.sin(a) * 1.8, 0))
    rot.append(ep.rod(f'feedstrut{k}', tuple(rim), tuple(horn), 0.035, 'mech', 5))
rot.append(sphere('horn', tuple(horn), 0.16, 'glow', 8, 5))
rot.append(box('counter', tuple(P(0, -0.85, 0.35)), (0.7, 0.6, 0.6), 'hull_dark', 0.04))
rot.append(box('countert', tuple(P(0, -0.85, 0.68)), (0.6, 0.5, 0.06), 'team', 0.01, 1))

B = kit.join('Body', body)
R = kit.join('Rotor_Y', rot, parent_origin=PIV)
ep.finish('lattice_radar_tower', [B, R], 512, bake_scale=0.5)
