"""Crucible "Thunderhead" strategic bomber. Run: python tools/models/crucible_heavy_bomber.py <out_dir>

A slab-sided flying wing: a wide, thick riveted centre body with a glazed cockpit slot and a
blunt armoured prow, broad team-topped outer wings, six fat jet nacelles across the trailing
edge glowing orange, twin tail booms carrying square fins and a crossbar, and two long bomb bay
doors underneath. Far wider and heavier than the Anvilfall, with no tubular fuselage.
Modelled around z = 0 (belly near 0); the game lifts it to flight altitude.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('crucible')
body = []
centre = hull_from('centre', [(-0.42, 0.95, 0.06), (0.42, 0.95, 0.06), (-0.5, -0.8, 0.0), (0.5, -0.8, 0.0),
                              (-0.36, 1.05, 0.2), (0.36, 1.05, 0.2), (-0.46, 0.6, 0.42), (0.46, 0.6, 0.42),
                              (-0.5, -0.8, 0.36), (0.5, -0.8, 0.36)], 'hull', 0.03, 2)
inset_panels(centre, 0.03, 0.01, lambda f: f.normal.z > 0.6 and f.calc_area() > 0.15)
body.append(centre)
body.append(box('spine', (0, -0.1, 0.42), (0.5, 1.1, 0.04), 'team', 0.01, 1))
body.append(hull_from('prow', [(-0.3, 1.06, 0.06), (0.3, 1.06, 0.06), (-0.3, 1.14, 0.12), (0.3, 1.14, 0.12),
                               (-0.28, 1.06, 0.2), (0.28, 1.06, 0.2)], 'hull_dark', 0.01, 1))
body.append(box('cockpit', (0, 0.84, 0.36), (0.44, 0.08, 0.05), 'glass', 0.004, 1))
body.append(box('cockpitg', (0, 0.87, 0.36), (0.36, 0.02, 0.02), 'glow_dim', 0, 1))
body += rivets('prv', (-0.36, 1.0, 0.3), (0.36, 1.0, 0.3), 7, 'metal', 0.016)
# Outer wings: broad, straight-swept, team tops
plan = [(0.45, 0.55), (1.45, -0.2), (1.45, -0.62), (0.45, -0.75)]
body += A.wing('wing', plan, 0.16, 0.2, 0.07, 'team', 'hull_dark')
for sx in A.SIDES:
    body.append(box(f'tip{sx}', (sx * 1.47, -0.41, 0.16), (0.05, 0.44, 0.1), 'hull_dark', 0.01, 1))
    body.append(sphere(f'navlt{sx}', (sx * 1.5, -0.2, 0.16), 0.022, 'glow', 8, 4))
    body += rivets(f'wrv{sx}', (sx * 0.55, -0.68, 0.27), (sx * 1.35, -0.58, 0.24), 6, 'metal', 0.016)
# Six nacelles across the trailing edge
# Six nacelles riding on top of the trailing edge, glowing out the back
for x in (-1.1, -0.75, -0.36, 0.36, 0.75, 1.1):
    zc = 0.36 if abs(x) > 0.5 else 0.48
    body.append(box(f'pyl{x}', (x, -0.62, zc - 0.1), (0.1, 0.36, 0.12), 'hull_dark', 0.012, 1))
    body += A.thruster(f'eng{x}', (x, -0.38, zc), (x, -0.98, zc), 0.12, verts=12)
    body.append(cyl(f'engband{x}', (x, -0.56, zc), (x, -0.61, zc), 0.118, 'accent', 12, bevel=0))
    body.append(box(f'intk{x}', (x, -0.34, zc), (0.2, 0.04, 0.2), 'mech', 0, 1))
# Twin booms and fins
for sx in A.SIDES:
    body.append(box(f'boom{sx}', (sx * 0.75, -1.1, 0.24), (0.1, 0.9, 0.1), 'hull', 0.012, 1))
    body.append(prism_x(f'fin{sx}', [(-1.25, 0.28), (-1.4, 0.68), (-1.56, 0.68), (-1.56, 0.28)], sx * 0.75 - 0.03, sx * 0.75 + 0.03, 'team', 0.008, 1))
    body.append(sphere(f'finlt{sx}', (sx * 0.75, -1.56, 0.69), 0.02, 'glow', 8, 4))
body.append(box('crossbar', (0, -1.48, 0.3), (1.6, 0.16, 0.05), 'hull_dark', 0.01, 1))
# Bomb bay doors and a ventral gun pod
for sx in A.SIDES:
    body.append(box(f'bay{sx}', (sx * 0.14, 0.0, -0.005), (0.24, 1.0, 0.02), 'mech', 0.004, 1))
body.append(box('baystripe', (0, 0.0, -0.016), (0.03, 1.02, 0.01), 'glow_dim', 0, 1))
body.append(sphere('gunpod', (0, 0.62, -0.02), 0.09, 'hull_dark', 10, 6))
body += A.barrel('gun', (0, 0.66, -0.04), (0, 0.86, -0.06), 0.018, brake=False)

A.finish('crucible_heavy_bomber', [('Body', body, None)], 512)
