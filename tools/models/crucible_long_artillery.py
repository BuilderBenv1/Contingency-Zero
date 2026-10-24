"""Crucible "Siegebreaker" long-range artillery. Run: python tools/models/crucible_long_artillery.py <out_dir>

A heavy tracked siege gun: a huge recoil spade dug in behind, a riveted team-painted casemate,
and a massive barrel at 30 degrees with a thick team sleeve, twin recoil cylinders, a fume
extractor and a big slotted muzzle brake. Shell hoist, ammo bins and a stack at the back.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('crucible')
body = []
body += vp.crucible_tracks(0.5, 0.86, -1.08, 1.04, r_end=0.22, wheels=6, cleats=21, skirt='team_dark', skirt_top=0.5, skirt_rivets=9)

HW = 0.5
body.append(box('tub', (0, -0.02, 0.3), (2 * HW, 2.04, 0.28), 'hull_dark', 0.02))
upper = prism_x('upper', [(1.04, 0.34), (0.74, 0.6), (-0.96, 0.6), (-1.06, 0.52), (-1.06, 0.32), (1.04, 0.32)],
                -0.62, 0.62, 'hull', 0.025, 2)
inset_panels(upper, 0.03, 0.01, lambda f: f.normal.z > 0.3 and f.calc_area() > 0.05)
body.append(upper)
body.append(prism_x('glacisplate', [(0.98, 0.39), (0.78, 0.585), (0.74, 0.6), (0.94, 0.405)], -0.44, 0.44, 'team', 0.006, 1))
body += rivets('glrv', (-0.54, 0.84, 0.56), (0.54, 0.84, 0.56), 8, 'metal', 0.02)
for sx in (-1, 1):
    body.append(cyl(f'lamp{sx}', (sx * 0.38, 1.0, 0.44), (sx * 0.38, 1.05, 0.44), 0.04, 'glow', 8, bevel=0))
    body.append(cyl(f'lamphood{sx}', (sx * 0.38, 0.97, 0.44), (sx * 0.38, 1.02, 0.44), 0.055, 'mech', 8, bevel=0))
# Driver's box at the front left.
body.append(box('drv', (-0.3, 0.58, 0.66), (0.26, 0.24, 0.12), 'hull_dark', 0.015, 1))
body.append(box('drvglass', (-0.3, 0.705, 0.68), (0.18, 0.01, 0.04), 'glass', 0, 1))
body += vp.stack('stack', (0.38, 0.62, 0.6), 0.07, 0.34)
# The recoil spade: two heavy arms and a toothed blade dug in behind.
for sx in (-1, 1):
    body.append(vp.beam(f'sparm{sx}', (sx * 0.34, -0.92, 0.46), (sx * 0.3, -1.3, 0.1), 0.1, 0.1, 'hull_dark'))
    body.append(cyl(f'sppivot{sx}', (sx * 0.42, -0.92, 0.46), (sx * 0.26, -0.92, 0.46), 0.06, 'mech', 10))
    body.append(cyl(f'spram{sx}', (sx * 0.2, -0.8, 0.6), (sx * 0.2, -1.22, 0.22), 0.03, 'metal', 8, bevel=0))
body.append(hull_from('spade', [(-0.46, -1.24, 0.26), (0.46, -1.24, 0.26), (-0.46, -1.36, 0.26), (0.46, -1.36, 0.26),
                                (-0.4, -1.32, 0.0), (0.4, -1.32, 0.0), (-0.4, -1.4, 0.02), (0.4, -1.4, 0.02)], 'team', 0.012, 1))
for k in range(5):
    body.append(hull_from(f'spt{k}', [(-0.32 + k * 0.16 - 0.04, -1.32, 0.02), (-0.32 + k * 0.16 + 0.04, -1.32, 0.02),
                                      (-0.32 + k * 0.16, -1.36, -0.0), (-0.32 + k * 0.16, -1.42, 0.0)], 'metal', 0.004, 1))
body += rivets('sprv', (-0.4, -1.31, 0.24), (0.4, -1.31, 0.24), 6, 'metal', 0.018)
body.append(cyl('tring', (0, -0.36, 0.6), (0, -0.36, 0.64), 0.42, 'mech', 16, bevel=0.006))

# ---- Turret: casemate with the big gun ----
PIV = (0.0, -0.36, 0.6)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = []
case = hull_from('case', [P(-0.4, 0.44, 0.0), P(0.4, 0.44, 0.0), P(-0.4, -0.54, 0.0), P(0.4, -0.54, 0.0),
                          P(-0.34, 0.3, 0.42), P(0.34, 0.3, 0.42), P(-0.36, -0.5, 0.42), P(0.36, -0.5, 0.42)], 'team', 0.03, 2)
inset_panels(case, 0.04, 0.012, lambda f: f.calc_area() > 0.06)
tur.append(case)
tur.append(box('caseroof', P(0, -0.12, 0.43), (0.56, 0.6, 0.03), 'hull', 0.01, 1))
tur += rivets('crv1', P(-0.34, 0.31, 0.44), P(0.34, 0.31, 0.44), 6, 'metal', 0.02)
tur += rivets('crv2', P(-0.34, -0.5, 0.44), P(0.34, -0.5, 0.44), 6, 'metal', 0.02)
for sx in (-1, 1):
    tur += rivets(f'csr{sx}', P(sx * 0.41, 0.36, 0.2), P(sx * 0.41, -0.48, 0.2), 6, 'metal', 0.018)
# Gun.
root = P(0, 0.2, 0.34)
E = lambda d, dx=0.0, dz=0.0: vp.elevated((root[0] + dx, root[1], root[2] + dz), 30, d)
tur.append(box('mantlet', P(0, 0.44, 0.3), (0.36, 0.14, 0.3), 'hull_dark', 0.025))
tur.append(cyl('sleeve', E(0.1), E(0.6), 0.16, 'team', 12, r2=0.15))
tur.append(cyl('sleeveband', E(0.56), E(0.62), 0.165, 'mech', 12, bevel=0.004))
tur.append(cyl('tube', E(0.6), E(1.9), 0.11, 'metal', 12, r2=0.1))
tur.append(cyl('fume', E(1.0), E(1.18), 0.135, 'hull_dark', 12))
for sx in (-1, 1):
    tur.append(cyl(f'recoil{sx}', E(0.1, sx * 0.16, -0.08), E(0.82, sx * 0.13, -0.08), 0.045, 'metal', 8))
    tur.append(cyl(f'recoilc{sx}', E(0.1, sx * 0.16, -0.08), E(0.4, sx * 0.15, -0.08), 0.06, 'mech', 8))
tur.append(vp.beam('brake', E(1.88), E(2.14), 0.3, 0.22, 'mech', 0.02))
for sx in (-1, 1):
    for k in range(2):
        tur.append(vp.beam(f'bslot{sx}{k}', E(1.93 + k * 0.1, sx * 0.14), E(1.99 + k * 0.1, sx * 0.14), 0.04, 0.14, 'tread', 0.004))
tur.append(cyl('bore', E(2.14), E(2.15), 0.06, 'tread', 10, bevel=0))
# Shell hoist and ammo bins at the back, a commander's hatch.
tur.append(box('hoist', P(0.24, -0.64, 0.24), (0.18, 0.16, 0.42), 'hull_dark', 0.015, 1))
tur.append(box('hoistglow', P(0.24, -0.725, 0.26), (0.1, 0.008, 0.2), 'glow_dim', 0, 1))
tur.append(box('bin', P(-0.18, -0.64, 0.16), (0.3, 0.16, 0.2), 'accent', 0.012, 1))
for k in range(3):
    tur.append(cyl(f'shell{k}', P(-0.28 + k * 0.1, -0.62, 0.26), P(-0.28 + k * 0.1, -0.62, 0.34), 0.035, 'accent', 8, bevel=0))
    tur.append(sphere(f'tip{k}', P(-0.28 + k * 0.1, -0.62, 0.34), 0.033, 'metal', 8, 4))
tur.append(cyl('hatch', P(-0.18, -0.24, 0.445), P(-0.18, -0.24, 0.475), 0.11, 'hull_dark', 12))
tur.append(cyl('antenna', P(0.3, -0.44, 0.44), P(0.34, -0.52, 1.0), 0.008, 'mech', 5, bevel=0))

vp.finish('crucible_long_artillery', body, tur, PIV)
