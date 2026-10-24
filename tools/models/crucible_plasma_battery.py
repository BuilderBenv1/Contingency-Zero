"""Crucible "Slag Battery" strategic artillery. Run: python <this> <out_dir>

A low riveted sloped emplacement with a team band; a hex traverse deck (Turret: yaws to aim)
carrying a big riveted gun house with a team roof, twin hot exhaust stacks, recoil cylinders,
an elevating ram, and a thick banded barrel raised 45 degrees with a team sleeve and a glowing
muzzle. Footprint 2 (16 units): kept inside 15 across. Pivot at 2.0 as the procedural model.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('crucible')
body = []
H = 2.0
base = hull_from('base', [(sx * 7.25, sy * 7.25, 0) for sx in (-1, 1) for sy in (-1, 1)] + [(sx * 6.3, sy * 6.3, H) for sx in (-1, 1) for sy in (-1, 1)], 'hull', 0.08)
inset_panels(base, 0.25, 0.07, lambda f: abs(f.normal.z) < 0.9)
body.append(base)
body.append(box('roof', (0, 0, H + 0.05), (12.4, 12.4, 0.12), 'hull_dark', 0.04))
for s in (-1, 1):
    body += ep.rivet_line(f'rr{s}', (-5.8, s * 5.95, H + 0.12), (5.8, s * 5.95, H + 0.12), 10, 'metal', 0.1)
    body += ep.rivet_line(f'rc{s}', (s * 5.95, -5.3, H + 0.12), (s * 5.95, 5.3, H + 0.12), 9, 'metal', 0.1)
for s in (-1, 1):
    body.append(hull_from(f'band{s}', [(-6.4, s * 6.95, 0.55), (6.4, s * 6.95, 0.55), (-6.15, s * 6.7, 1.3), (6.15, s * 6.7, 1.3),
                                      (-6.4, s * 6.75, 0.55), (6.4, s * 6.75, 0.55), (-6.15, s * 6.5, 1.3), (6.15, s * 6.5, 1.3)], 'team', 0.03))
for sx in (-1, 1):
    for sy in (-1, 1):
        body.append(box(f'shells{sx}{sy}', (sx * 5.2, sy * 5.2, H + 0.5), (1.6, 1.6, 0.8), 'accent', 0.05))
        for k in range(3):
            body.append(cyl(f'shell{sx}{sy}{k}', (sx * 5.2 - 0.5 + k * 0.5, sy * 5.2, H + 0.9), (sx * 5.2 - 0.5 + k * 0.5, sy * 5.2, H + 1.5), 0.2, 'metal', 8, r2=0.1, bevel=0))

PIV = (0, 0, H)
P = lambda x, y, z: Vector((PIV[0] + x, PIV[1] + y, PIV[2] + z))
tur = []
deck = hull_from('deck', [tuple(P(math.cos(k * math.pi / 3) * 4.7, math.sin(k * math.pi / 3) * 4.7, 0)) for k in range(6)] +
                 [tuple(P(math.cos(k * math.pi / 3) * 4.5, math.sin(k * math.pi / 3) * 4.5, 0.8)) for k in range(6)], 'mech', 0.05)
tur.append(deck)
tur += [o for k in range(6) for o in ep.rivet_line(f'dr{k}', tuple(P(math.cos(k * math.pi / 3) * 4.0, math.sin(k * math.pi / 3) * 4.0, 0.82)),
                                                    tuple(P(math.cos((k + 1) * math.pi / 3) * 4.0, math.sin((k + 1) * math.pi / 3) * 4.0, 0.82)), 3, 'metal', 0.09)]
house = box('house', tuple(P(0, -1.3, 2.0)), (4.4, 4.6, 2.5), 'hull', 0.1)
inset_panels(house, 0.15, 0.05, lambda f: abs(f.normal.z) < 0.5 and f.calc_area() > 3)
tur.append(house)
tur.append(box('roof', tuple(P(0, -1.3, 3.3)), (4.0, 4.2, 0.16), 'team', 0.04))
for sx in (-1, 1):
    tur.append(box(f'cheek{sx}', tuple(P(sx * 2.3, -0.9, 2.0)), (0.25, 4.0, 2.0), 'team', 0.04))
    tur += ep.rivet_line(f'chr{sx}', tuple(P(sx * 2.45, -2.6, 2.8)), tuple(P(sx * 2.45, 0.8, 2.8)), 5, 'metal', 0.08)
for sx in (-1, 1):
    tur += ep.stack(f'stack{sx}', tuple(P(sx * 1.35, -3.0, 3.38)), 0.42, 2.0)
# Barrel at 45 degrees.
root = P(0, 0.4, 3.0)
d = Vector((0, math.cos(math.radians(45)), math.sin(math.radians(45))))
tur.append(cyl('trunnion', tuple(root + Vector((-1.6, 0, 0))), tuple(root + Vector((1.6, 0, 0))), 0.55, 'metal', 12, bevel=0.03))
tur.append(ep.beam('breech', tuple(root - d * 1.6), tuple(root + d * 0.7), 1.7, 1.7, 'hull_dark', 0.08))
tur.append(cyl('barrel', tuple(root + d * 0.6), tuple(root + d * 9.0), 0.72, 'metal', 14, r2=0.62, bevel=0.03))
tur.append(cyl('sleeve', tuple(root + d * 0.7), tuple(root + d * 2.6), 1.0, 'team', 14, r2=0.95, bevel=0.03))
for t in (3.6, 5.6, 7.4):
    tur.append(cyl(f'hoop{t}', tuple(root + d * t), tuple(root + d * (t + 0.35)), 0.85 - t * 0.012, 'mech', 14, bevel=0.01))
for sx in (-1, 1):
    tur.append(cyl(f'recoil{sx}', tuple(root + d * 0.5 + Vector((sx * 0.75, 0, 0)) - Vector((0, 0.35, -0.35)) * 0),
                   tuple(root + d * 3.4 + Vector((sx * 0.75, 0, 0))), 0.25, 'accent', 10, bevel=0.01))
tur.append(cyl('muzzle', tuple(root + d * 8.8), tuple(root + d * 9.5), 1.1, 'mech', 12, r2=1.0, bevel=0.03))
tur.append(cyl('muzglow', tuple(root + d * 9.5), tuple(root + d * 9.53), 0.6, 'glow', 12, bevel=0))
# Elevating ram from the deck to the barrel underside.
ram_a = P(0, 2.9, 0.8)
ram_b = root + d * 3.0 - Vector((0, -0.45, 0.6)) * 0.0 - Vector((0, 0, 0.75))
tur.append(cyl('ramc', tuple(ram_a), tuple(ram_a.lerp(ram_b, 0.6)), 0.32, 'hull_dark', 10, bevel=0.02))
tur.append(cyl('ramr', tuple(ram_a.lerp(ram_b, 0.55)), tuple(ram_b), 0.18, 'metal', 10, bevel=0))
tur.append(box('ramfoot', tuple(ram_a + Vector((0, 0, 0.1))), (0.8, 0.8, 0.3), 'mech', 0.03))

B = kit.join('Body', body)
T = kit.join('Turret', tur, parent_origin=tuple(P(0, 0, 0)))
ep.finish('crucible_plasma_battery', [B, T], 1024, bake_scale=0.3, sizes={'turret': 512})
