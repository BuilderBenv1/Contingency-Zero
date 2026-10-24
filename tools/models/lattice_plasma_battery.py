"""Lattice "Lance Battery" strategic plasma artillery. Run: python <this> <out_dir>

A panelled octagonal emplacement with a team ring; a traverse platform (Turret: yaws to aim)
with team-painted yoke cheeks, capacitor banks, a team counterweight and a very long rail
accelerator raised ~48 degrees: twin magnetic rails, glowing coil rings and a lit muzzle.
Footprint 2 (16 units): kept inside 15 across. Pivot at 1.6 as the procedural model.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, cyl, sphere, inset_panels, Matrix, Vector
import economy_parts as ep

kit.reset('lattice')
body = []
P8 = math.pi / 8
oct_ = lambda r, z: [(math.cos(P8 + k * math.pi / 4) * r, math.sin(P8 + k * math.pi / 4) * r, z) for k in range(8)]
base = hull_from('base', oct_(7.25, 0.0) + oct_(6.3, 1.6), 'hull_dark', 0.08)
inset_panels(base, 0.2, 0.06, lambda f: abs(f.normal.z) < 0.9)
body.append(base)
body.append(ep.ring('team', (0, 0), 6.3, 4.6, 1.6, 1.74, 'team', 8, phase=P8, bevel=0.03))
body.append(ep.ngon('well', (0, 0), 4.6, 4.6, 1.5, 1.65, 8, 'mech'))
for k in range(8):
    a = k * math.pi / 4
    body.append(box(f'light{k}', (0, 0, 0), (1.4, 0.1, 0.14), 'glow', 0, 1))
    body[-1].data.transform(Matrix.Translation((math.cos(a) * 6.65, math.sin(a) * 6.65, 0.9)) @ Matrix.Rotation(a + math.pi / 2, 4, 'Z') @ Matrix.Rotation(-0.53, 4, 'X'))

PIV = (0, 0, 1.6)
P = lambda x, y, z: Vector((PIV[0] + x, PIV[1] + y, PIV[2] + z))
tur = []
plat = hull_from('plat', [tuple(P(math.cos(P8 + k * math.pi / 4) * 4.2, math.sin(P8 + k * math.pi / 4) * 4.2, 0)) for k in range(8)] +
                 [tuple(P(math.cos(P8 + k * math.pi / 4) * 3.8, math.sin(P8 + k * math.pi / 4) * 3.8, 0.9)) for k in range(8)], 'hull', 0.06)
inset_panels(plat, 0.14, 0.04, lambda f: abs(f.normal.z) < 0.5)
tur.append(plat)
tur.append(ep.ring('platring', tuple(P(0, 0, 0))[:2], 3.8, 2.6, PIV[2] + 0.9, PIV[2] + 1.0, 'team', 8, phase=P8, bevel=0.02))
TRUN = P(0, -0.6, 3.0)
for sx in (-1, 1):
    ch = hull_from(f'cheek{sx}', [tuple(P(sx * 1.4 + dx, y, z)) for dx in (-0.32, 0.32) for (y, z) in ((-2.4, 0.9), (2.0, 0.9), (0.3, 3.6), (-1.5, 3.6))], 'team', 0.05)
    tur.append(ch)
    tur.append(cyl(f'trun{sx}', tuple(TRUN + Vector((sx * 1.0, 0, 0))), tuple(TRUN + Vector((sx * 1.85, 0, 0))), 0.55, 'mech', 12, bevel=0.03))
    tur.append(cyl(f'trunglow{sx}', tuple(TRUN + Vector((sx * 1.85, 0, 0))), tuple(TRUN + Vector((sx * 1.9, 0, 0))), 0.3, 'glow', 12, bevel=0))
    # Capacitor bank beside the yoke.
    for j in range(3):
        c = P(sx * 2.75, -1.6 + j * 0.9, 0.9)
        tur.append(cyl(f'cap{sx}{j}', tuple(c), tuple(c + Vector((0, 0, 1.0))), 0.36, 'hull_dark', 8, bevel=0.02))
        tur.append(cyl(f'capg{sx}{j}', tuple(c + Vector((0, 0, 1.0))), tuple(c + Vector((0, 0, 1.1))), 0.24, 'glow', 8, bevel=0))
tur.append(box('counter', tuple(P(0, -2.9, 1.5)), (2.2, 1.0, 1.2), 'team', 0.06))
tur.append(box('counterb', tuple(P(0, -2.9, 0.95)), (2.4, 1.2, 0.2), 'hull_dark', 0.03))
# Barrel: breech, a long tapered tube, twin rails, coil rings and muzzle.
d = Vector((0, math.cos(math.radians(48)), math.sin(math.radians(48))))
side = Vector((1, 0, 0))
up = side.cross(d)
tur.append(ep.beam('breech', tuple(TRUN - d * 1.5), tuple(TRUN + d * 0.9), 1.5, 1.3, 'hull', 0.06))
tur.append(cyl('tube', tuple(TRUN + d * 0.9), tuple(TRUN + d * 9.6), 0.5, 'metal', 14, r2=0.38, bevel=0.02))
for sx in (-1, 1):
    tur.append(ep.beam(f'rail{sx}', tuple(TRUN + d * 1.0 + side * sx * 0.55 + up * 0.1), tuple(TRUN + d * 9.2 + side * sx * 0.45 + up * 0.1), 0.18, 0.5, 'hull_dark', 0.02))
for t in (2.2, 3.8, 5.4, 7.0, 8.4):
    tur.append(cyl(f'coil{t}', tuple(TRUN + d * t), tuple(TRUN + d * (t + 0.35)), 0.75 - t * 0.02, 'glow', 14, bevel=0))
    tur.append(cyl(f'coilb{t}', tuple(TRUN + d * (t - 0.12)), tuple(TRUN + d * t), 0.8 - t * 0.02, 'hull', 14, bevel=0))
tur.append(cyl('muzzle', tuple(TRUN + d * 9.5), tuple(TRUN + d * 10.1), 0.62, 'mech', 14, r2=0.5, bevel=0.02))
tur.append(cyl('muzglow', tuple(TRUN + d * 10.1), tuple(TRUN + d * 10.13), 0.36, 'glow', 14, bevel=0))
tur.append(box('eye', tuple(P(0.0, 1.95, 1.2)), (0.8, 0.1, 0.18), 'glow', 0, 1))

B = kit.join('Body', body)
T = kit.join('Turret', tur, parent_origin=tuple(P(0, 0, 0)))
ep.finish('lattice_plasma_battery', [B, T], 1024, bake_scale=0.3, sizes={'turret': 512})
