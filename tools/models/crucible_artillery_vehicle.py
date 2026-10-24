"""Crucible "Howitzer Cart". Run: python tools/models/crucible_artillery_vehicle.py <out_dir>

A four-wheeled self-propelled gun: a riveted box body on big lugged wheels with a team deck, a
recoil spade dug in at the back, a stack, and an open gun mount with a fat howitzer barrel raised
at 32 degrees on twin recoil cylinders, with a shell rack and a crew shield.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('crucible')
body = []
for sx in (-1, 1):
    for y in (0.66, -0.56):
        body += vp.crucible_wheel(f'w{sx}{y}', (sx * 0.62, y, 0.34), 0.34, 0.24, sx, lugs=12)
for y in (0.66, -0.56):
    body.append(cyl(f'axle{y}', (-0.5, y, 0.34), (0.5, y, 0.34), 0.06, 'mech', 8))
# Body: lower tub, team deck, sloped nose.
body.append(box('tub', (0, 0.0, 0.42), (0.86, 1.86, 0.2), 'hull_dark', 0.02))
deck = prism_x('deck', [(1.0, 0.44), (0.82, 0.66), (-0.92, 0.66), (-0.98, 0.6), (-0.98, 0.44)], -0.44, 0.44, 'team', 0.025, 2)
inset_panels(deck, 0.03, 0.01, lambda f: f.normal.z > 0.3 and f.calc_area() > 0.05)
body.append(deck)
body += rivets('noserv', (-0.38, 0.86, 0.66), (0.38, 0.86, 0.66), 6, 'metal', 0.018)
for sx in (-1, 1):
    body += rivets(f'siderv{sx}', (sx * 0.45, 0.8, 0.6), (sx * 0.45, -0.9, 0.6), 7, 'metal', 0.016)
    # Mudguards over each wheel.
    for y in (0.66, -0.56):
        body.append(prism_x(f'guard{sx}{y}', [(y + 0.4, 0.62), (y + 0.3, 0.72), (y - 0.3, 0.72), (y - 0.4, 0.62)],
                            min(sx * 0.48, sx * 0.8), max(sx * 0.48, sx * 0.8), 'hull_dark', 0.008, 1))
    body.append(box(f'lamp{sx}', (sx * 0.3, 0.97, 0.52), (0.08, 0.02, 0.05), 'glow', 0, 1))
    body.append(box(f'lamphood{sx}', (sx * 0.3, 0.95, 0.55), (0.12, 0.05, 0.03), 'mech', 0.005, 1))
# Driver's hatch with vision block at the front.
body.append(box('drvbox', (-0.2, 0.62, 0.71), (0.26, 0.22, 0.1), 'hull', 0.015, 1))
body.append(box('drvglass', (-0.2, 0.735, 0.72), (0.18, 0.01, 0.04), 'glass', 0, 1))
body += vp.stack('stack', (0.32, 0.68, 0.66), 0.06, 0.32)
# Recoil spade at the back on a pair of arms.
for sx in (-1, 1):
    body.append(vp.beam(f'sparm{sx}', (sx * 0.25, -0.86, 0.48), (sx * 0.2, -1.16, 0.12), 0.08, 0.08, 'metal'))
body.append(hull_from('spade', [(-0.32, -1.12, 0.2), (0.32, -1.12, 0.2), (-0.32, -1.22, 0.2), (0.32, -1.22, 0.2),
                                (-0.26, -1.2, 0.0), (0.26, -1.2, 0.0), (-0.26, -1.26, 0.02), (0.26, -1.26, 0.02)], 'mech', 0.01, 1))
body.append(box('spadeteeth', (0, -1.24, 0.01), (0.52, 0.04, 0.02), 'metal', 0.004, 1))
body.append(cyl('tring', (0, -0.22, 0.66), (0, -0.22, 0.69), 0.36, 'mech', 14, bevel=0.006))

# ---- Turret ----
PIV = (0.0, -0.22, 0.66)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
tur.append(box('mount', P(0, -0.08, 0.1), (0.66, 0.7, 0.2), 'hull', 0.02))
tur += rivets('mountrv', P(-0.28, 0.26, 0.205), P(0.28, 0.26, 0.205), 5, 'metal', 0.016)
# Side walls (team) forming the cradle.
for sx in (-1, 1):
    prof = [(PIV[1] + y, PIV[2] + z) for y, z in ((0.24, 0.2), (0.1, 0.48), (-0.2, 0.48), (-0.36, 0.2))]
    tur.append(prism_x(f'wall{sx}', prof, sx * 0.18 - 0.04, sx * 0.18 + 0.04, 'team', 0.012, 1))
    tur.append(cyl(f'trun{sx}', P(sx * 0.22, -0.02, 0.36), P(sx * 0.26, -0.02, 0.36), 0.08, 'mech', 10))
# Barrel: breech block, sleeve, long tube, muzzle brake.
root = P(0, -0.02, 0.36)
E = lambda d, dx=0.0, dz=0.0: vp.elevated((root[0] + dx, root[1], root[2] + dz), 32, d)
tur.append(vp.beam('breech', E(-0.42), E(-0.1), 0.24, 0.22, 'mech', 0.02))
tur.append(cyl('sleeve', E(-0.12), E(0.5), 0.11, 'hull', 12))
tur.append(cyl('tube', E(0.5), E(1.56), 0.085, 'metal', 12, r2=0.078))
tur.append(cyl('band', E(1.0), E(1.06), 0.1, 'hull_dark', 12, bevel=0.004))
tur.append(vp.beam('brake', E(1.5), E(1.72), 0.22, 0.17, 'mech', 0.02))
for sx in (-1, 1):
    tur.append(vp.beam(f'bslot{sx}', E(1.55, sx * 0.1), E(1.67, sx * 0.1), 0.04, 0.1, 'tread', 0.004))
    tur.append(cyl(f'recoil{sx}', E(-0.18, sx * 0.0, -0.13), E(0.62, 0, -0.13), 0.04, 'metal', 8))
tur.append(cyl('bore', E(1.72), E(1.73), 0.05, 'tread', 10, bevel=0))
# Crew shield at the front of the mount, shell rack at the back, hot ammo glow.
tur.append(hull_from('shield', [P(-0.33, 0.28, 0.18), P(0.33, 0.28, 0.18), P(-0.33, 0.22, 0.18), P(0.33, 0.22, 0.18),
                                P(-0.3, 0.32, 0.36), P(-0.1, 0.32, 0.36), P(-0.3, 0.26, 0.36), P(-0.1, 0.26, 0.36),
                                P(0.3, 0.32, 0.36), P(0.1, 0.32, 0.36), P(0.3, 0.26, 0.36), P(0.1, 0.26, 0.36)], 'team', 0.01, 1))
tur.append(box('rack', P(0, -0.46, 0.16), (0.5, 0.14, 0.16), 'hull_dark', 0.012, 1))
for k in range(5):
    tur.append(cyl(f'shell{k}', P(-0.18 + k * 0.09, -0.46, 0.2), P(-0.18 + k * 0.09, -0.46, 0.3), 0.032, 'accent', 8, bevel=0))
    tur.append(sphere(f'tipsh{k}', P(-0.18 + k * 0.09, -0.46, 0.3), 0.03, 'metal', 8, 4))
tur.append(box('fuse', P(0.22, -0.3, 0.24), (0.1, 0.08, 0.08), 'hull_dark', 0.01, 1))
tur.append(box('fuseg', P(0.22, -0.256, 0.24), (0.06, 0.006, 0.03), 'glow', 0, 1))

vp.finish('crucible_artillery_vehicle', body, tur, PIV)
