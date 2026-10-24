"""Crucible "Bulwark" heavy assault tank. Run: python tools/models/crucible_heavy_assault.py <out_dir>

A siege brute bigger than the Anvil: wide tracks under thick riveted skirts, a team ram plate
across the nose, twin glowing stacks, and a huge slab-sided turret with twin heavy cannons,
appliqué armour blocks, and a laser emitter housing on the roof.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('crucible')
body = []
body += vp.crucible_tracks(0.54, 0.92, -1.1, 1.06, r_end=0.24, wheels=6, cleats=18, skirt='team', skirt_top=0.56, skirt_rivets=8)

HW = 0.54
body.append(box('tub', (0, -0.02, 0.32), (2 * HW, 2.1, 0.3), 'hull_dark', 0.02))
upper = prism_x('upper', [(1.08, 0.36), (0.72, 0.68), (-0.72, 0.68), (-1.06, 0.62), (-1.12, 0.42), (-1.12, 0.34), (1.08, 0.34)],
                -0.7, 0.7, 'hull', 0.03, 2)
inset_panels(upper, 0.035, 0.012, lambda f: f.normal.z > 0.3 and f.calc_area() > 0.05)
body.append(upper)
body += rivets('glrv', (-0.6, 0.8, 0.665), (0.6, 0.8, 0.665), 8, 'metal', 0.022)
# Ram plate: a wedge of armour across the nose, team on its face.
body.append(prism_x('ram', [(1.26, 0.12), (1.3, 0.2), (1.12, 0.48), (1.02, 0.48), (1.06, 0.12)], -0.62, 0.62, 'team', 0.015, 2))
body.append(box('ramedge', (0, 1.27, 0.1), (1.2, 0.08, 0.05), 'metal', 0.01, 1))
body += rivets('ramrv', (-0.54, 1.2, 0.36), (0.54, 1.2, 0.36), 7, 'metal', 0.02)
for sx in (-1, 1):
    body.append(vp.beam(f'rambrace{sx}', (sx * 0.4, 1.04, 0.3), (sx * 0.4, 0.78, 0.5), 0.08, 0.08, 'mech'))
    body.append(cyl(f'lamp{sx}', (sx * 0.46, 1.0, 0.56), (sx * 0.46, 1.05, 0.56), 0.04, 'glow', 8, bevel=0))
    body.append(cyl(f'lamphood{sx}', (sx * 0.46, 0.97, 0.56), (sx * 0.46, 1.02, 0.56), 0.056, 'mech', 8, bevel=0))
# Glacis team plate.
body.append(prism_x('glacisplate', [(1.0, 0.43), (0.76, 0.67), (0.72, 0.685), (0.96, 0.445)], -0.46, 0.46, 'team', 0.006, 1))
# Engine deck with stacks and grille.
body += vp.grille('deck', (0, -0.84, 0.7), 0.9, 0.42, 8)
for sx in (-1, 1):
    body += vp.stack(f'stack{sx}', (sx * 0.34, -0.9, 0.68), 0.08, 0.36)
# Stowage on the hull sides.
for sx in (-1, 1):
    for k, (y, ln) in enumerate(((0.45, 0.32), (-0.05, 0.4))):
        body.append(box(f'tool{sx}{k}', (sx * 0.64, y, 0.72), (0.12, ln, 0.1), 'accent' if k == 1 else 'hull_dark', 0.012, 1))
body.append(box('rack', (0.3, -1.16, 0.5), (0.34, 0.06, 0.16), 'accent', 0.01, 1))
body.append(cyl('tring', (0, -0.1, 0.68), (0, -0.1, 0.72), 0.42, 'mech', 16, bevel=0.006))

# ---- Turret ----
PIV = (0.0, -0.1, 0.68)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
shell = hull_from('turret', [P(-0.5, -0.46, 0.0), P(0.5, -0.46, 0.0), P(0.5, 0.4, 0.0), P(-0.5, 0.4, 0.0),
                             P(0.36, 0.56, 0.06), P(-0.36, 0.56, 0.06),
                             P(-0.44, -0.5, 0.34), P(0.44, -0.5, 0.34), P(0.44, 0.34, 0.38), P(-0.44, 0.34, 0.38),
                             P(0.28, 0.5, 0.3), P(-0.28, 0.5, 0.3)], 'team', bevel=0.035, segs=2)
inset_panels(shell, 0.04, 0.012, lambda f: f.calc_area() > 0.05)
tur.append(shell)
tur.append(box('roof', P(0, -0.08, 0.39), (0.62, 0.6, 0.03), 'hull', 0.01, 1))
tur += rivets('trv1', P(-0.4, 0.46, 0.31), P(0.4, 0.46, 0.31), 6, 'metal', 0.02)
tur += rivets('trv2', P(-0.4, -0.5, 0.35), P(0.4, -0.5, 0.35), 6, 'metal', 0.02)
# Appliqué blocks on the cheeks.
for sx in (-1, 1):
    for k in range(3):
        tur.append(box(f'appl{sx}{k}', P(sx * 0.53, 0.24 - k * 0.24, 0.18), (0.07, 0.2, 0.22), 'team_dark', 0.012, 1))
        tur.append(sphere(f'applb{sx}{k}', P(sx * 0.57, 0.24 - k * 0.24, 0.18), 0.018, 'metal', 8, 4))
# Twin heavy guns.
tur.append(box('mantlet', P(0, 0.6, 0.17), (0.5, 0.14, 0.24), 'hull_dark', 0.025))
for sx in (-1, 1):
    x = sx * 0.14
    tur.append(cyl(f'sleeve{sx}', P(x, 0.64, 0.17), P(x, 0.86, 0.17), 0.085, 'metal', 12))
    tur.append(cyl(f'barrel{sx}', P(x, 0.86, 0.17), P(x, 1.3, 0.17), 0.062, 'metal', 12))
    tur.append(cyl(f'fume{sx}', P(x, 0.98, 0.17), P(x, 1.1, 0.17), 0.08, 'hull_dark', 12))
    tur.append(box(f'brake{sx}', P(x, 1.34, 0.17), (0.16, 0.12, 0.12), 'mech', 0.02))
    tur.append(box(f'slot{sx}', P(x, 1.34, 0.17), (0.17, 0.05, 0.05), 'tread', 0.004, 1))
# Laser housing on the roof (right), cupola (left), smoke dischargers, antenna.
tur.append(box('lbox', P(0.26, 0.0, 0.48), (0.18, 0.3, 0.16), 'hull_dark', 0.02))
tur.append(cyl('lbarrel', P(0.26, 0.15, 0.48), P(0.26, 0.48, 0.48), 0.04, 'metal', 10))
tur.append(cyl('lhead', P(0.26, 0.44, 0.48), P(0.26, 0.54, 0.48), 0.055, 'mech', 10))
tur.append(cyl('llens', P(0.26, 0.54, 0.48), P(0.26, 0.55, 0.48), 0.042, 'glow', 10, bevel=0))
tur.append(box('lcoil', P(0.26, 0.0, 0.565), (0.12, 0.2, 0.01), 'glow_dim', 0, 1))
tur.append(cyl('cupola', P(-0.2, -0.16, 0.4), P(-0.2, -0.16, 0.5), 0.13, 'hull', 12))
tur.append(cyl('hatch', P(-0.2, -0.16, 0.5), P(-0.2, -0.16, 0.53), 0.11, 'hull_dark', 12))
for k in range(5):
    a = k * 2 * math.pi / 5
    tur.append(box(f'vision{k}', P(-0.2 + math.cos(a) * 0.13, -0.16 + math.sin(a) * 0.13, 0.46), (0.035, 0.035, 0.03), 'glass', 0.004, 1))
for sx in (-1, 1):
    for k in range(3):
        a = P(sx * (0.42 + k * 0.03), 0.34 - k * 0.07, 0.26)
        b = P(sx * (0.5 + k * 0.03), 0.42 - k * 0.07, 0.33)
        tur.append(cyl(f'smoke{sx}{k}', a, b, 0.028, 'mech', 8, bevel=0))
tur.append(box('bustle', P(0, -0.58, 0.18), (0.72, 0.18, 0.24), 'hull_dark', 0.02))
tur.append(box('crate', P(0.2, -0.62, 0.34), (0.16, 0.14, 0.1), 'accent', 0.01, 1))
tur.append(cyl('tarp', P(-0.3, -0.62, 0.34), P(0.04, -0.62, 0.34), 0.06, 'accent', 10))
tur.append(cyl('antenna', P(0.32, -0.5, 0.36), P(0.36, -0.58, 1.0), 0.008, 'mech', 5, bevel=0))

vp.finish('crucible_heavy_assault', body, tur, PIV)
