"""Crucible "Brander" light laser bunker. Run: python <this> <out_dir>

A squat riveted, sloped-sided bunker with a team band, sandbag-style armour blocks and a
vision slit, carrying a boxy riveted housing (Turret: yaws to aim) with a team lid, a heavy
mantlet and twin emitter barrels with glowing muzzles, plus a small exhaust stack.
Footprint 1 (8 units): kept inside 7 across. Turret pivot at 3.0 as the procedural model.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('crucible')
body = []
H = 3.0
bunker = hull_from('bunker', [(sx * 3.35, sy * 3.35, 0) for sx in (-1, 1) for sy in (-1, 1)] +
                   [(sx * 2.55, sy * 2.55, H) for sx in (-1, 1) for sy in (-1, 1)], 'hull', 0.08)
inset_panels(bunker, 0.18, 0.05, lambda f: abs(f.normal.z) < 0.9)
body.append(bunker)
body.append(box('roof', (0, 0, H + 0.05), (4.9, 4.9, 0.12), 'hull_dark', 0.03))
body.append(ep.ring('race', (0, 0), 1.75, 1.35, H, H + 0.2, 'mech', 16))
for sy in (-1, 1):
    body += ep.rivet_line(f'rr{sy}', (-2.2, sy * 2.35, H + 0.12), (2.2, sy * 2.35, H + 0.12), 6, 'metal', 0.07)
for sx in (-1, 1):
    body += ep.rivet_line(f'rc{sx}', (sx * 2.35, -1.9, H + 0.12), (sx * 2.35, 1.9, H + 0.12), 5, 'metal', 0.07)
# Team band and a vision slit on the front face.
band = hull_from('band', [(-2.9, -3.12, 1.05), (2.9, -3.12, 1.05), (-2.75, -3.0, 1.75), (2.75, -3.0, 1.75),
                          (-2.9, -2.9, 1.05), (2.9, -2.9, 1.05), (-2.75, -2.75, 1.75), (2.75, -2.75, 1.75)], 'team', 0.02)
body.append(band)
body.append(box('slit', (0, -2.72, 2.25), (1.8, 0.4, 0.22), 'tread', 0.02))
body.append(box('slitglow', (0, -2.86, 2.25), (1.4, 0.05, 0.08), 'glow_dim', 0, 1))
# Armour blocks stacked round the base corners.
for sx in (-1, 1):
    for sy in (-1, 1):
        body.append(box(f'block{sx}{sy}', (sx * 3.0, sy * 3.0, 0.35), (0.9, 0.9, 0.7), 'hull_dark', 0.06))
        body.append(box(f'blockt{sx}{sy}', (sx * 3.0, sy * 3.0, 0.74), (0.7, 0.7, 0.08), 'team_dark', 0.02, 1))
body.append(box('door', (0, 2.95, 0.9), (1.2, 0.3, 1.6), 'mech', 0.03))

PIV = (0, 0, H)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
tur.append(cyl('ring', P(0, 0, 0), P(0, 0, 0.25), 1.5, 'mech', 16, bevel=0.02))
tur.append(box('house', P(0, -0.1, 0.85), (2.7, 2.9, 1.3), 'hull', 0.07))
tur.append(box('lid', P(0, -0.2, 1.56), (2.4, 2.4, 0.14), 'team', 0.03))
for sx in (-1, 1):
    tur.append(box(f'side{sx}', P(sx * 1.4, -0.1, 0.85), (0.14, 2.6, 1.0), 'team_dark', 0.02))
    tur += ep.rivet_line(f'sr{sx}', P(sx * 1.48, -1.1, 1.25), P(sx * 1.48, 0.9, 1.25), 4, 'metal', 0.06)
tur += ep.rivet_line('fr', P(-1.05, 1.36, 1.38), P(1.05, 1.36, 1.38), 5, 'metal', 0.06)
tur.append(box('mantlet', P(0, 1.45, 0.8), (1.9, 0.45, 1.0), 'hull_dark', 0.06))
for sx in (-1, 1):
    x = sx * 0.48
    tur.append(cyl(f'sleeve{sx}', P(x, 1.6, 0.8), P(x, 2.2, 0.8), 0.26, 'metal', 10, bevel=0.02))
    tur.append(cyl(f'barrel{sx}', P(x, 2.2, 0.8), P(x, 3.1, 0.8), 0.17, 'metal', 10, bevel=0.01))
    tur.append(cyl(f'coil{sx}', P(x, 2.35, 0.8), P(x, 2.65, 0.8), 0.24, 'accent', 10, bevel=0))
    tur.append(cyl(f'muzzle{sx}', P(x, 3.0, 0.8), P(x, 3.3, 0.8), 0.24, 'mech', 10, bevel=0.01))
    tur.append(cyl(f'muzglow{sx}', P(x, 3.3, 0.8), P(x, 3.33, 0.8), 0.16, 'glow', 10, bevel=0))
tur += ep.stack('exh', P(0.85, -1.15, 1.5), 0.17, 0.6)
tur.append(box('sight', P(-0.75, 0.6, 1.72), (0.4, 0.5, 0.25), 'hull_dark', 0.02))
tur.append(box('sightglass', P(-0.75, 0.86, 1.72), (0.3, 0.03, 0.12), 'glow', 0, 1))

B = kit.join('Body', body)
T = kit.join('Turret', tur, parent_origin=PIV)
ep.finish('crucible_light_turret', [B, T], 512, bake_scale=0.5)
