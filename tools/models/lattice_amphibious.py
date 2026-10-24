"""Lattice "Wader" amphibious walker. Run: python tools/models/lattice_amphibious.py <out_dir>

A sealed, submarine-smooth hull high on four long insect legs: team top canopy, glowing
waterline strips, a ballast vent row, sensor eye and snorkel mast, and a domed laser turret
on top. Matches the procedural model: hips at 0.7, hull z 0.54..1.04, turret pivot
(0, 0.08, 1.04), laser reaching y 0.76.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
from bots_parts import spider_leg, beam, obox, emitter, finish, add, lerp

kit.reset('lattice')
body = []
for sx in (-1, 1):
    for sy in (-1, 1):
        body += spider_leg(f'leg{sx}{sy}', (sx * 0.32, sy * 0.3, 0.72), (sx * 0.68, sy * 0.66, 0.0), 0.11,
                           (sx, sy * 0.3, 1.3), 0.22)
secs = [(0.72, 0.12, 0.08, 0.72, 0.82, 0.88), (0.4, 0.46, 0.32, 0.54, 0.86, 1.04),
        (-0.34, 0.46, 0.32, 0.54, 0.86, 1.04), (-0.62, 0.22, 0.14, 0.64, 0.82, 0.9)]
pts = []
for y, hw, ht, z0, z1, z2 in secs:
    pts += [(-hw * 0.8, y, z0), (hw * 0.8, y, z0), (hw, y, z1), (ht, y, z2), (-ht, y, z2), (-hw, y, z1)]
hull = hull_from('hull', pts, 'hull', 0.035, 2)
inset_panels(hull, 0.025, 0.008, lambda f: abs(f.normal.x) > 0.5 and f.calc_area() > 0.05)
body.append(hull)
# Team top canopy in two halves around the turret ring, sloped flanks in team_dark
body.append(hull_from('topf', [(-0.3, 0.38, 1.04), (0.3, 0.38, 1.04), (-0.07, 0.7, 0.885), (0.07, 0.7, 0.885),
                               (-0.28, 0.36, 1.065), (0.28, 0.36, 1.065), (-0.05, 0.66, 0.91), (0.05, 0.66, 0.91),
                               (-0.3, 0.3, 1.04), (0.3, 0.3, 1.04), (-0.28, 0.3, 1.065), (0.28, 0.3, 1.065)], 'team', 0.01, 1))
body.append(hull_from('topb', [(-0.3, -0.16, 1.04), (0.3, -0.16, 1.04), (-0.3, -0.34, 1.04), (0.3, -0.34, 1.04),
                               (-0.12, -0.62, 0.9), (0.12, -0.62, 0.9),
                               (-0.28, -0.16, 1.065), (0.28, -0.16, 1.065), (-0.1, -0.58, 0.925), (0.1, -0.58, 0.925)], 'team', 0.01, 1))
for sx in (-1, 1):
    body.append(hull_from(f'flank{sx}', [(sx * 0.33, 0.36, 1.03), (sx * 0.33, -0.3, 1.03), (sx * 0.45, 0.38, 0.88), (sx * 0.45, -0.32, 0.88),
                                         (sx * 0.35, 0.36, 1.045), (sx * 0.35, -0.3, 1.045), (sx * 0.47, 0.38, 0.89), (sx * 0.47, -0.32, 0.89)],
                          'team_dark', 0.008, 1))
    body.append(beam(f'wl{sx}', (sx * 0.47, 0.34, 0.8), (sx * 0.47, -0.3, 0.8), 0.02, 0.04, 'glow', bevel=0))
    for k in range(4):
        body.append(box(f'vent{sx}{k}', (sx * 0.4, 0.22 - k * 0.14, 0.6), (0.02, 0.08, 0.04), 'mech', 0.004, 1))
body.append(box('keel', (0, 0, 0.53), (0.4, 1.0, 0.04), 'hull_dark', 0.01, 1))
body.append(hull_from('eye', [(-0.12, 0.68, 0.8), (0.12, 0.68, 0.8), (-0.1, 0.735, 0.82), (0.1, 0.735, 0.82),
                              (-0.11, 0.67, 0.85), (0.11, 0.67, 0.85)], 'glow', 0, 1))
body.append(cyl('snorkel', (-0.18, -0.38, 1.0), (-0.2, -0.44, 1.42), 0.03, 'metal', 8))
body.append(cyl('snorkelc', (-0.2, -0.44, 1.42), (-0.2, -0.44, 1.46), 0.045, 'mech', 8, bevel=0))
body.append(sphere('snorkelg', (-0.2, -0.44, 1.48), 0.03, 'glow', 8, 5))
body.append(cyl('prop', (0, -0.62, 0.75), (0, -0.7, 0.75), 0.1, 'mech', 12))
body.append(cyl('propg', (0, -0.7, 0.75), (0, -0.71, 0.75), 0.075, 'glow_dim', 12, bevel=0))

PIV = (0, 0.08, 1.04)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = [cyl('ring', P(0, 0, 0), P(0, 0, 0.06), 0.25, 'mech', 16)]
dome = hull_from('dome', [P(math.cos(a) * 0.23, math.sin(a) * 0.23, 0.05) for a in [k * math.pi / 6 for k in range(12)]] +
                 [P(math.cos(a) * 0.16, math.sin(a) * 0.16 - 0.02, 0.16) for a in [k * math.pi / 6 for k in range(12)]], 'team', 0.015, 2)
tur.append(dome)
tur.append(box('mantlet', P(0, 0.18, 0.09), (0.16, 0.1, 0.1), 'hull_dark', 0.012, 1))
tur += emitter('las', P(0, 0.2, 0.09), P(0, 0.68, 0.09), 0.04)
tur.append(box('sensor', P(0.1, -0.02, 0.17), (0.06, 0.1, 0.04), 'hull_dark', 0.006, 1))
tur.append(box('sensorg', P(0.1, 0.032, 0.17), (0.04, 0.004, 0.02), 'glow', 0, 1))

finish('lattice_amphibious', body, tur, PIV)
