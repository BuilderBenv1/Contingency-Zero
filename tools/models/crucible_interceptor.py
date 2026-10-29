"""Crucible "Wasp" interceptor. Run: python tools/models/crucible_interceptor.py <out_dir>

Built for speed rather than brawling: a long, narrow riveted fuselage with a pointed armoured
nose, small canards, a cranked delta wing with team tops and dark leading edges, one huge
engine glowing orange in a fat tail can, twin outward-canted fins and four AA missiles in a
row under the belly. Longer, slimmer and swept where the Hornet is stubby and straight-winged.
Modelled around z = 0 (belly near 0); the game lifts it to flight altitude.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('crucible')
body = []
secs = [
    A.hexring(1.3, 0.03, 0.02, 0.12, 0.15, 0.17),
    A.hexring(1.05, 0.09, 0.05, 0.07, 0.17, 0.24),
    A.hexring(0.6, 0.15, 0.09, 0.03, 0.18, 0.32),
    A.hexring(-0.6, 0.18, 0.12, 0.02, 0.19, 0.32),
    A.hexring(-0.95, 0.2, 0.14, 0.03, 0.18, 0.3),
]
band = {0: 'hull_dark', 1: 'hull', 5: 'hull', 2: 'hull_dark', 4: 'hull_dark', 3: 'hull'}
fus = A.loft('fus', secs, 'hull', lambda j: band[j], cap0='mech', cap1='mech', bevel=0.012)
inset_panels(fus, 0.02, 0.006, lambda f: f.normal.z > 0.6 and f.calc_area() > 0.06)
body.append(fus)
body.append(cyl('probe', (0, 1.3, 0.15), (0, 1.48, 0.15), 0.012, 'metal', 6, bevel=0))
# Armoured slab canopy, set well forward
body.append(hull_from('canopy', [(0.07, 0.88, 0.25), (-0.07, 0.88, 0.25), (0.08, 0.5, 0.31), (-0.08, 0.5, 0.31),
                                 (0.05, 0.78, 0.33), (-0.05, 0.78, 0.33), (0.06, 0.54, 0.37), (-0.06, 0.54, 0.37)], 'glass', bevel=0.008))
body.append(box('cframe', (0, 0.66, 0.36), (0.14, 0.025, 0.03), 'mech', 0.004, 1))
# Canards
body += A.wing('canard', [(0.08, 0.95), (0.3, 0.84), (0.3, 0.78), (0.08, 0.76)], 0.17, 0.025, 0.015, 'team', 'hull_dark', panels=False)
# Cranked delta: inboard strake, then the main delta to clipped team tips
plan = [(0.14, 0.45), (0.38, 0.0), (0.92, -0.62), (0.92, -0.78), (0.14, -0.78)]
body += A.wing('wing', plan, 0.12, 0.06, 0.025, 'team', 'hull_dark')
for sx in A.SIDES:
    body.append(hull_from(f'lead{sx}', [(sx * 0.14, 0.45, 0.12), (sx * 0.38, 0.0, 0.12), (sx * 0.92, -0.62, 0.12),
                                        (sx * 0.14, 0.4, 0.15), (sx * 0.38, -0.04, 0.15), (sx * 0.9, -0.64, 0.14)], 'hull_dark', 0.004, 1))
    body.append(sphere(f'navlt{sx}', (sx * 0.93, -0.7, 0.12), 0.018, 'glow', 8, 4))
    body += rivets(f'wrv{sx}', (sx * 0.22, -0.2, 0.155), (sx * 0.22, -0.72, 0.155), 4, 'metal', 0.012)
    # Twin canted fins
    fin = prism_x(f'fin{sx}', [(-0.5, 0.3), (-0.82, 0.68), (-0.98, 0.68), (-0.95, 0.3)], -0.022, 0.022, 'team', 0.006, 1)
    body += A.xform([fin], (0, -0.7, 0.3), (0, sx * math.radians(18), 0), (sx * 0.16, 0, 0))
# One huge engine
body += A.thruster('eng', (0, -0.9, 0.17), (0, -1.36, 0.17), 0.2, verts=14)
body.append(cyl('engband', (0, -1.05, 0.17), (0, -1.11, 0.17), 0.2, 'accent', 14, bevel=0))
for sx in A.SIDES:
    body.append(box(f'intake{sx}', (sx * 0.17, 0.25, 0.14), (0.08, 0.5, 0.14), 'hull_dark', 0.012, 1))
    body.append(box(f'mouth{sx}', (sx * 0.17, 0.5, 0.14), (0.06, 0.02, 0.1), 'mech', 0, 1))
# Four belly missiles in a row
for k, y in enumerate((0.3, 0.0, -0.3, -0.6)):
    x = 0.08 if k % 2 else -0.08
    body.append(cyl(f'msl{k}', (x, y - 0.24, -0.02), (x, y + 0.16, -0.02), 0.032, 'accent', 8, bevel=0.003))
    body.append(cyl(f'msln{k}', (x, y + 0.16, -0.02), (x, y + 0.24, -0.02), 0.032, 'metal', 8, r2=0.008, bevel=0))
    body.append(box(f'mrail{k}', (x, y - 0.04, 0.02), (0.02, 0.34, 0.03), 'mech', 0.003, 1))
body += rivets('srv', (0, 0.4, 0.325), (0, -0.85, 0.325), 8, 'metal', 0.014)

A.finish('crucible_interceptor', [('Body', body, None)], 512)
