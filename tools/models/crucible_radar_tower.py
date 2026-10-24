"""Crucible "Watchtower" radar. Run: python <this> <out_dir>

A riveted footing with a team plate and a generator shed, a tapering four-leg girder tower
with a ladder, a team-floored railed lookout platform with a lit hut and a lamp, and a
spinning bedstead radar array (Rotor_Y: spins flat about the vertical): a team-painted grid
of dipole bars on a girder frame, tilted back, on a slewing post.
Footprint 1 (8 units): kept inside 7 across. Rotor pivot at 7.65 as the procedural model.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('crucible')
body = []
body.append(box('base', (0, 0, 0.3), (5.8, 5.8, 0.6), 'hull_dark', 0.06))
body.append(box('baseplate', (0, 0, 0.64), (4.6, 4.6, 0.1), 'team', 0.02))
for s in (-1, 1):
    body += ep.rivet_line(f'brx{s}', (-2.5, s * 2.65, 0.62), (2.5, s * 2.65, 0.62), 6, 'metal', 0.06)
for sx in (-1, 1):
    for sy in (-1, 1):
        body.append(box(f'shoe{sx}{sy}', (sx * 2.3, sy * 2.3, 0.85), (0.8, 0.8, 0.4), 'hull', 0.04))
body += ep.tower_truss('tw', 2.3, 0.85, 0.7, 7.1, 'hull_dark', bays=4, leg=0.3, brace=0.11, brace_paint='mech')
body += ep.ladder('lad', (0.0, -1.3, 0.7), 7.1, 'mech', w=0.45, normal=(0, -1, 0))
# Generator shed at the base, under the tower.
body.append(box('shed', (1.6, -1.9, 1.15), (1.6, 1.2, 1.0), 'hull', 0.04))
body.append(box('shedroof', (1.6, -1.9, 1.72), (1.75, 1.35, 0.12), 'team', 0.02))
body += ep.stack('gen', (2.1, -1.6, 1.78), 0.15, 0.7)
body += ep.pipe('cable', [(0.9, -1.9, 1.0), (0.4, -0.5, 1.0), (0.3, -0.3, 7.0)], 0.07, 'tread', elbows=False)
# Lookout platform.
body.append(box('plat', (0, 0, 7.3), (3.0, 3.0, 0.4), 'hull', 0.04))
body.append(box('platteam', (0, 0, 7.53), (2.7, 2.7, 0.08), 'team', 0.02))
body += ep.rail('prail', [(-1.45, -1.45, 7.5), (1.45, -1.45, 7.5), (1.45, 1.45, 7.5), (-1.45, 1.45, 7.5)], 0.6, 'accent', r=0.035, closed=True)
body.append(box('hut', (0.85, 0.85, 7.95), (0.9, 0.9, 0.85), 'hull_dark', 0.03))
body.append(box('hutroof', (0.85, 0.85, 8.42), (1.0, 1.0, 0.1), 'team', 0.01))
body.append(box('hutwin', (0.85, 0.39, 8.0), (0.6, 0.03, 0.25), 'glow', 0, 1))
body.append(cyl('lamp', (-1.3, -1.3, 8.1), (-1.3, -1.3, 8.3), 0.1, 'glow', 8, bevel=0))
body.append(cyl('lamppost', (-1.3, -1.3, 7.5), (-1.3, -1.3, 8.1), 0.04, 'mech', 5, bevel=0))
body.append(cyl('slew', (0, 0, 7.5), (0, 0, 7.7), 0.55, 'mech', 12, bevel=0.02))

PIV = (0, 0, 7.65)
P = lambda x, y, z: Vector((PIV[0] + x, PIV[1] + y, PIV[2] + z))
rot = []
rot.append(cyl('post', tuple(P(0, 0, 0)), tuple(P(0, 0, 1.1)), 0.35, 'metal', 10, bevel=0.02))
rot.append(box('gearbox', tuple(P(0, 0, 0.25)), (0.9, 0.9, 0.4), 'hull_dark', 0.03))
arr = []
W, Hh = 4.6, 1.7
arr.append(box('frame_t', (0, 0, Hh / 2), (W, 0.18, 0.18), 'mech', 0.02))
arr.append(box('frame_b', (0, 0, -Hh / 2), (W, 0.18, 0.18), 'mech', 0.02))
for x in (-W / 2, -W / 6, W / 6, W / 2):
    arr.append(box(f'frame_v{x}', (x, 0, 0), (0.16, 0.16, Hh), 'mech', 0.02))
arr.append(box('panel', (0, -0.08, 0), (W - 0.2, 0.12, Hh - 0.25), 'team', 0.03))
for i in range(4):
    z = -Hh / 2 + 0.3 + i * (Hh - 0.6) / 3
    arr.append(box(f'dipole{i}', (0, 0.12, z), (W - 0.1, 0.08, 0.08), 'metal', 0, 1))
arr.append(box('glowstrip', (0, 0.17, 0), (W - 0.6, 0.04, 0.1), 'glow_dim', 0, 1))
arr.append(box('back', (0, -0.3, 0), (1.0, 0.4, 0.8), 'hull', 0.03))
xf = Matrix.Translation(P(0, 0.2, 1.65)) @ Matrix.Rotation(math.radians(15), 4, 'X')
for o in arr:
    o.data.transform(xf)
rot += arr
rot += ep.rivet_line('arrr', tuple(xf @ Vector((-1.8, -0.15, 0.45))), tuple(xf @ Vector((1.8, -0.15, 0.45))), 5, 'metal', 0.05)

B = kit.join('Body', body)
R = kit.join('Rotor_Y', rot, parent_origin=PIV)
ep.finish('crucible_radar_tower', [B, R], 512, bake_scale=0.5)
