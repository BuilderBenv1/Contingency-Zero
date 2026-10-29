"""Crucible "Ironside" escort cruiser. Run: python tools/models/crucible_cruiser.py <out_dir>

A broad, riveted hull carrying an armoured citadel with steeply sloped casemate walls (team
roof, glowing gun-port slots), four open flak mounts at the citadel corners with raised quad
barrels, a tall lattice tripod mast with a spotting top, twin squat stacks, and one big twin-gun
turret on the foredeck. Wider and taller amidships than the Hammerhead, with flak bristling at
the corners. The waterline is z = 0.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A
import vehicles_parts as vp

kit.reset('crucible')
F = 'crucible'
HW, DECK, KEEL = 0.42, 0.2, -0.14
body, secs = A.ship_hull(F, HW, DECK, KEEL, bow=1.22, stern=-1.15)
for sx in A.SIDES:
    body += rivets(f'hr{sx}', (sx * (HW + 0.004), 0.9, 0.1), (sx * (HW + 0.004), -1.0, 0.1), 11, 'metal', 0.014)
# Armoured citadel with sloped casemate walls
cit = hull_from('cit', [(-0.38, 0.25, DECK), (0.38, 0.25, DECK), (-0.38, -0.75, DECK), (0.38, -0.75, DECK),
                        (-0.26, 0.18, 0.46), (0.26, 0.18, 0.46), (-0.26, -0.68, 0.46), (0.26, -0.68, 0.46)], 'hull', 0.02, 2)
inset_panels(cit, 0.03, 0.008, lambda f: abs(f.normal.x) > 0.5 and f.calc_area() > 0.05)
body.append(cit)
body.append(box('citroof', (0, -0.25, 0.47), (0.5, 0.84, 0.025), 'team', 0.008, 1))
for sx in A.SIDES:
    for k in range(4):
        y = 0.08 - k * 0.22
        body.append(box(f'port{sx}{k}', (sx * 0.33, y, 0.32), (0.02, 0.08, 0.035), 'glow_dim', 0, 1))
    body += rivets(f'crv{sx}', (sx * 0.31, 0.15, 0.4), (sx * 0.31, -0.65, 0.4), 6, 'metal', 0.013)
# Bridge block and tripod mast
body.append(box('bridge', (0, 0.06, 0.55), (0.3, 0.2, 0.14), 'hull_dark', 0.014, 1))
body.append(box('bwin', (0, 0.165, 0.56), (0.24, 0.01, 0.04), 'glow_dim', 0, 1))
body.append(box('broof', (0, 0.06, 0.63), (0.32, 0.22, 0.02), 'team', 0.006, 1))
for a in (0, 2.1, 4.2):
    body.append(cyl(f'leg{a}', (math.cos(a) * 0.1, -0.08 + math.sin(a) * 0.1, 0.48), (0, -0.08, 1.02), 0.013, 'metal', 5, bevel=0))
body.append(box('top', (0, -0.08, 1.02), (0.14, 0.14, 0.07), 'hull_dark', 0.008, 1))
body.append(box('topg', (0, -0.005, 1.02), (0.1, 0.01, 0.02), 'glow', 0, 1))
for k, y in enumerate((-0.32, -0.5)):
    body.append(cyl(f'stack{k}', (0, y, 0.47), (0, y, 0.66), 0.07, 'metal', 10, r2=0.064, bevel=0.004))
    body.append(cyl(f'stglow{k}', (0, y, 0.66), (0, y, 0.668), 0.058, 'glow', 10, bevel=0))
# Four flak mounts on the citadel corners
for sx in A.SIDES:
    for y in (0.14, -0.62):
        c = (sx * 0.24, y, 0.5)
        body.append(cyl(f'fm{sx}{y}', c, (c[0], c[1], c[2] + 0.05), 0.07, 'accent', 10, bevel=0.004))
        body.append(box(f'fsh{sx}{y}', (c[0], c[1] + 0.05, c[2] + 0.08), (0.12, 0.02, 0.08), 'team', 0.004, 1))
        for bx in (-0.025, 0.025):
            root = (c[0] + bx, c[1], c[2] + 0.07)
            body.append(cyl(f'fb{sx}{y}{bx}', root, vp.elevated(root, 60, 0.2), 0.011, 'metal', 6, bevel=0))
# Foredeck twin-gun turret
PIV = (0, 0.66, DECK)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = [cyl('race', P(0, 0, -0.005), P(0, 0, 0.03), 0.22, 'mech', 12, bevel=0.004)]
tb = hull_from('tbox', [P(-0.18, 0.16, 0.02), P(0.18, 0.16, 0.02), P(-0.2, -0.2, 0.02), P(0.2, -0.2, 0.02),
                        P(-0.15, 0.13, 0.17), P(0.15, 0.13, 0.17), P(-0.18, -0.2, 0.17), P(0.18, -0.2, 0.17)], 'team', 0.02, 2)
tur.append(tb)
tur.append(box('tface', P(0, 0.165, 0.09), (0.28, 0.03, 0.12), 'hull_dark', 0.008, 1))
tur += rivets('trv', P(-0.13, 0.0, 0.175), P(0.13, 0.0, 0.175), 4, 'metal', 0.016)
for sx in A.SIDES:
    tur += A.barrel(f'gun{sx}', P(sx * 0.07, 0.17, 0.09), P(sx * 0.07, 0.7, 0.09), 0.036)
    tur.append(cyl(f'sleeve{sx}', P(sx * 0.07, 0.17, 0.09), P(sx * 0.07, 0.3, 0.09), 0.05, 'hull_dark', 10, bevel=0.004))
A.finish('crucible_cruiser', [('Body', body, None), ('Turret', tur, PIV)], 512)
