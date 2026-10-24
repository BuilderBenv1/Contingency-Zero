"""Lattice Mortar Walker. Run: python tools/models/lattice_mortar_bot.py <out_dir>

A low, stable four-legged walker: insect legs with team femur plates, a sleek sealed hull with
a team top and sensor eye, and on top a turntable carrying a short fat mortar tube between
two team cheek plates, pitched steeply up, with a glowing range-finder.
Matches the procedural model: hips at 0.52, hull z 0.42..0.7, turret pivot (0, -0.06, 0.68),
tube at 60 degrees.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
from bots_parts import spider_leg, beam, obox, finish, add, lerp
from mathutils import Vector

kit.reset('lattice')
body = []
for sx in (-1, 1):
    for sy in (-1, 1):
        body += spider_leg(f'leg{sx}{sy}', (sx * 0.26, sy * 0.24, 0.52), (sx * 0.54, sy * 0.48, 0.0), 0.09,
                           (sx * 0.7, 0, 1), 0.16)
secs = [(0.5, 0.2, 0.12, 0.44, 0.56, 0.62), (0.24, 0.36, 0.26, 0.4, 0.6, 0.7),
        (-0.3, 0.36, 0.26, 0.4, 0.6, 0.7), (-0.48, 0.24, 0.16, 0.44, 0.56, 0.62)]
pts = []
for y, hw, ht, z0, z1, z2 in secs:
    pts += [(-hw * 0.8, y, z0), (hw * 0.8, y, z0), (hw, y, z1), (ht, y, z2), (-ht, y, z2), (-hw, y, z1)]
hull = hull_from('hull', pts, 'hull', 0.025, 2)
inset_panels(hull, 0.02, 0.006, lambda f: abs(f.normal.x) > 0.5 and f.calc_area() > 0.03)
body.append(hull)
body.append(hull_from('deck', [(-0.25, 0.23, 0.7), (0.25, 0.23, 0.7), (-0.25, -0.29, 0.7), (0.25, -0.29, 0.7),
                               (-0.24, 0.22, 0.715), (0.24, 0.22, 0.715), (-0.24, -0.28, 0.715), (0.24, -0.28, 0.715)], 'team', 0.006, 1))
body.append(hull_from('glacis', [(-0.25, 0.25, 0.705), (0.25, 0.25, 0.705), (-0.15, 0.49, 0.632), (0.15, 0.49, 0.632),
                                 (-0.25, 0.24, 0.69), (0.25, 0.24, 0.69), (-0.15, 0.48, 0.615), (0.15, 0.48, 0.615)], 'team_dark', 0.006, 1))
body.append(hull_from('stern', [(-0.25, -0.31, 0.705), (0.25, -0.31, 0.705), (-0.15, -0.47, 0.632), (0.15, -0.47, 0.632),
                                (-0.25, -0.3, 0.69), (0.25, -0.3, 0.69), (-0.15, -0.46, 0.615), (0.15, -0.46, 0.615)], 'team_dark', 0.006, 1))
for sx in (-1, 1):
    body.append(beam(f'sideg{sx}', (sx * 0.365, 0.2, 0.6), (sx * 0.365, -0.28, 0.6), 0.01, 0.02, 'glow_dim', bevel=0))
body.append(hull_from('eye', [(-0.1, 0.48, 0.5), (0.1, 0.48, 0.5), (-0.08, 0.52, 0.53), (0.08, 0.52, 0.53),
                              (-0.09, 0.47, 0.56), (0.09, 0.47, 0.56)], 'glow', 0, 1))
body.append(box('ammo', (0, -0.5, 0.52), (0.26, 0.08, 0.12), 'hull_dark', 0.01, 1))
body.append(box('ammog', (0, -0.545, 0.52), (0.16, 0.006, 0.03), 'glow', 0, 1))

PIV = (0, -0.06, 0.68)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = [cyl('ring', P(0, 0, 0), P(0, 0, 0.08), 0.24, 'mech', 16, r2=0.21)]
tur.append(cyl('ringg', P(0, 0, 0.05), P(0, 0, 0.07), 0.235, 'glow_dim', 16, bevel=0))
for sx in (-1, 1):
    tur.append(hull_from(f'cheek{sx}', [P(sx * 0.13, -0.2, 0.06), P(sx * 0.19, -0.2, 0.06), P(sx * 0.13, 0.18, 0.06), P(sx * 0.19, 0.18, 0.06),
                                        P(sx * 0.13, -0.12, 0.34), P(sx * 0.19, -0.12, 0.34), P(sx * 0.13, 0.04, 0.32), P(sx * 0.19, 0.04, 0.32)],
                          'team', 0.012, 1))
    tur.append(cyl(f'trun{sx}', P(sx * 0.09, -0.04, 0.16), P(sx * 0.21, -0.04, 0.16), 0.05, 'metal', 10))
# The tube: short, fat and steep, with a team band and a reinforced muzzle
d = Vector((0, math.cos(math.radians(60)), math.sin(math.radians(60))))
root = Vector(P(0, -0.12, 0.14))
tur.append(cyl('tube', tuple(root - d * 0.12), tuple(root + d * 0.52), 0.105, 'metal', 14, r2=0.1))
tur.append(cyl('band', tuple(root + d * 0.06), tuple(root + d * 0.22), 0.125, 'team', 14))
tur.append(cyl('muzzle', tuple(root + d * 0.46), tuple(root + d * 0.6), 0.13, 'mech', 14, r2=0.12))
tur.append(cyl('bore', tuple(root + d * 0.6), tuple(root + d * 0.605), 0.075, 'tread', 14, bevel=0))
tur.append(cyl('breech', tuple(root - d * 0.2), tuple(root - d * 0.1), 0.08, 'hull_dark', 10))
# Range-finder on the left cheek
tur.append(box('rf', P(-0.24, 0.02, 0.22), (0.06, 0.14, 0.06), 'hull_dark', 0.008, 1))
tur.append(box('rfg', P(-0.24, 0.092, 0.22), (0.04, 0.004, 0.03), 'glow', 0, 1))

finish('lattice_mortar_bot', body, tur, PIV)
