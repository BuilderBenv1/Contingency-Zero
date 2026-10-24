"""Crucible "Rampart" heavy cannon bunker. Run: python <this> <out_dir>

A slab-sided riveted bunker with a team band, corner armour blocks and a firing slit, carrying a
huge boxy cast turret (Turret: yaws to aim) with a team roof and cheek plates, a heavy mantlet,
a thick barrel with a fume extractor and slotted muzzle brake, a cupola, an exhaust stack and a
stowage rack. Footprint 2 (16 units): kept inside 15 across. Pivot at 3.4 as the procedural model.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('crucible')
body = []
H = 3.4
bunker = hull_from('bunker', [(sx * 7.2, sy * 7.2, 0) for sx in (-1, 1) for sy in (-1, 1)] + [(sx * 5.6, sy * 5.6, H) for sx in (-1, 1) for sy in (-1, 1)], 'hull', 0.1)
inset_panels(bunker, 0.3, 0.08, lambda f: abs(f.normal.z) < 0.9)
body.append(bunker)
body.append(box('roof', (0, 0, H + 0.06), (10.8, 10.8, 0.14), 'hull_dark', 0.04))
body.append(ep.ring('race', (0, 0), 3.4, 2.9, H, H + 0.25, 'mech', 24))
for s in (-1, 1):
    body += ep.rivet_line(f'rr{s}', (-4.9, s * 5.15, H + 0.14), (4.9, s * 5.15, H + 0.14), 9, 'metal', 0.1)
    body += ep.rivet_line(f'rc{s}', (s * 5.15, -4.2, H + 0.14), (s * 5.15, 4.2, H + 0.14), 7, 'metal', 0.1)
band = hull_from('band', [(-6.3, -6.75, 1.0), (6.3, -6.75, 1.0), (-6.0, -6.45, 2.0), (6.0, -6.45, 2.0),
                          (-6.3, -6.5, 1.0), (6.3, -6.5, 1.0), (-6.0, -6.2, 2.0), (6.0, -6.2, 2.0)], 'team', 0.03)
body.append(band)
body.append(box('slit', (0, -5.95, 2.7), (4.0, 0.6, 0.35), 'tread', 0.03))
body.append(box('slitglow', (0, -6.2, 2.7), (3.4, 0.05, 0.12), 'glow_dim', 0, 1))
for sx in (-1, 1):
    for sy in (-1, 1):
        body.append(box(f'block{sx}{sy}', (sx * 6.55, sy * 6.55, 0.6), (1.6, 1.6, 1.2), 'hull_dark', 0.08))
        body.append(box(f'blockt{sx}{sy}', (sx * 6.55, sy * 6.55, 1.25), (1.3, 1.3, 0.12), 'team_dark', 0.02, 1))
        body += ep.rivet_ring(f'blr{sx}{sy}', (sx * 6.55, sy * 6.55), 0.5, 1.33, 4, 'metal', 0.08)
body.append(box('door', (0, 6.35, 1.2), (2.4, 0.5, 2.2), 'mech', 0.05))
body += ep.stack('bstack', (4.3, 4.3, H + 0.1), 0.3, 1.4, hot=False)

PIV = (0, 0, H)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
tur.append(cyl('tring', P(0, 0, 0), P(0, 0, 0.35), 3.2, 'mech', 24, bevel=0.03))
shell = hull_from('shell', [P(-2.8, -2.6, 0.3), P(2.8, -2.6, 0.3), P(-2.9, 2.0, 0.3), P(2.9, 2.0, 0.3), P(-2.2, 3.1, 0.6), P(2.2, 3.1, 0.6),
                            P(-2.6, -2.7, 2.7), P(2.6, -2.7, 2.7), P(-2.6, 1.6, 2.9), P(2.6, 1.6, 2.9), P(-1.9, 2.7, 2.3), P(1.9, 2.7, 2.3)], 'hull', 0.1)
inset_panels(shell, 0.2, 0.06, lambda f: f.calc_area() > 2.0)
tur.append(shell)
tur.append(box('roof', P(0, -0.3, 2.95), (4.4, 3.9, 0.16), 'team', 0.04))
for sx in (-1, 1):
    tur.append(box(f'cheek{sx}', P(sx * 2.95, 0.0, 1.5), (0.25, 3.8, 1.8), 'team', 0.04))
    tur += ep.rivet_line(f'chr{sx}', P(sx * 3.1, -1.6, 2.25), P(sx * 3.1, 1.6, 2.25), 5, 'metal', 0.08)
tur += ep.rivet_line('roofr', P(-2.0, 1.75, 3.05), P(2.0, 1.75, 3.05), 6, 'metal', 0.08)
# Gun.
tur.append(box('mantlet', P(0, 3.05, 1.35), (2.4, 0.8, 1.7), 'hull_dark', 0.1))
tur.append(cyl('sleeve', P(0, 3.3, 1.35), P(0, 4.4, 1.35), 0.66, 'metal', 14, bevel=0.03))
tur.append(cyl('barrel', P(0, 4.4, 1.35), P(0, 6.8, 1.35), 0.5, 'metal', 14, bevel=0.02))
tur.append(cyl('fume', P(0, 4.9, 1.35), P(0, 5.6, 1.35), 0.66, 'hull_dark', 14, bevel=0.02))
tur.append(cyl('tband', P(0, 3.7, 1.35), P(0, 4.0, 1.35), 0.7, 'team_dark', 14, bevel=0.01))
tur.append(box('brake', P(0, 7.05, 1.35), (1.3, 0.7, 1.0), 'mech', 0.06))
for sx in (-1, 1):
    tur.append(box(f'bslot{sx}', P(sx * 0.55, 7.05, 1.35), (0.28, 0.4, 0.5), 'tread', 0.01, 1))
tur.append(cyl('muzglow', P(0, 7.4, 1.35), P(0, 7.42, 1.35), 0.3, 'glow_dim', 12, bevel=0))
# Cupola, stack, stowage, lamp.
tur.append(cyl('cupola', P(-1.2, -0.9, 3.0), P(-1.2, -0.9, 3.55), 0.8, 'hull', 12, bevel=0.03))
tur.append(cyl('hatch', P(-1.2, -0.9, 3.55), P(-1.2, -0.9, 3.7), 0.62, 'hull_dark', 12, bevel=0.02))
for k in range(5):
    a = k * 2 * math.pi / 5
    tur.append(box(f'vis{k}', P(-1.2 + math.cos(a) * 0.8, -0.9 + math.sin(a) * 0.8, 3.3), (0.2, 0.2, 0.16), 'glass', 0.01, 1))
tur += ep.stack('tstack', P(1.7, -2.0, 3.0), 0.32, 1.5)
tur.append(box('rack', P(0, -2.95, 1.6), (3.6, 0.5, 1.2), 'accent', 0.04))
for i in range(3):
    tur.append(cyl(f'shell{i}', P(-1.1 + i * 1.1, -3.25, 1.2), P(-1.1 + i * 1.1, -3.25, 2.0), 0.2, 'metal', 8, bevel=0))
tur.append(box('sightbox', P(1.3, 1.9, 3.1), (0.8, 0.8, 0.5), 'hull_dark', 0.03))
tur.append(box('sightglass', P(1.3, 2.31, 3.1), (0.6, 0.04, 0.22), 'glow', 0, 1))

B = kit.join('Body', body)
T = kit.join('Turret', tur, parent_origin=PIV)
ep.finish('crucible_heavy_turret', [B, T], 1024, bake_scale=0.3, sizes={'turret': 512})
