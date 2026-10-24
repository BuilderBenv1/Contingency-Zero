"""Lattice "Swift" air-superiority fighter. Run: python tools/models/lattice_fighter.py <out_dir>

A needle-nosed cranked delta: slim faceted fuselage with a team spine, teardrop canopy, angular
side intakes, canards, twin outward-canted fins, one big blue-glowing engine, and AA missiles on
the wingtip rails. Modelled around z = 0 (belly near 0); the game lifts it to flight altitude.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('lattice')
body = []

# ---- Fuselage: faceted needle, team spine ----
secs = [
    A.octring(1.24, 0.008, 0.012, 0.008, 0.145, 0.15, 0.16, 0.165),
    A.octring(0.95, 0.035, 0.06, 0.03, 0.09, 0.13, 0.16, 0.2),
    A.octring(0.55, 0.07, 0.12, 0.06, 0.04, 0.12, 0.19, 0.27),
    A.octring(0.1, 0.09, 0.17, 0.08, 0.015, 0.11, 0.21, 0.3),
    A.octring(-0.45, 0.1, 0.18, 0.085, 0.015, 0.11, 0.21, 0.3),
    A.octring(-0.78, 0.09, 0.15, 0.08, 0.04, 0.11, 0.21, 0.27),
    A.octring(-0.88, 0.085, 0.14, 0.075, 0.05, 0.11, 0.21, 0.26),
]
band = {0: 'hull_dark', 1: 'hull_dark', 7: 'hull_dark', 2: 'hull', 6: 'hull', 3: 'hull', 5: 'hull', 4: 'team'}
fus = A.loft('fus', secs, 'hull', lambda j: band[j], cap0='metal', cap1='mech', bevel=0.008)
body.append(fus)
# Nose sensor: a glowing IR eye under the needle, and a pitot probe.
body.append(box('irst', (0, 0.88, 0.09), (0.04, 0.05, 0.025), 'glow', 0, 1))
body.append(cyl('pitot', (0, 1.2, 0.155), (0, 1.36, 0.155), 0.007, 'metal', 6, bevel=0))
# Teardrop canopy with a frame bar.
body.append(hull_from('canopy', [(0, 0.86, 0.2), (0.055, 0.68, 0.24), (-0.055, 0.68, 0.24), (0.06, 0.42, 0.27),
                                 (-0.06, 0.42, 0.27), (0.03, 0.62, 0.33), (-0.03, 0.62, 0.33), (0.025, 0.36, 0.34),
                                 (-0.025, 0.36, 0.34), (0, 0.26, 0.31)], 'glass', bevel=0.012, segs=2))
body.append(box('cframe', (0, 0.5, 0.335), (0.07, 0.025, 0.02), 'mech', 0.004, 1))
body.append(box('ckpitglow', (0, 0.55, 0.305), (0.03, 0.06, 0.01), 'glow_dim', 0, 1))
# Angular side intakes with dark mouths and a blue lip light.
for sx in A.SIDES:
    pts = [(0.12, 0.32, 0.08), (0.12, 0.32, 0.2), (0.23, 0.28, 0.08), (0.22, 0.28, 0.19),
           (0.12, -0.42, 0.06), (0.12, -0.42, 0.22), (0.21, -0.42, 0.07), (0.21, -0.42, 0.2)]
    body.append(hull_from(f'intake{sx}', [(sx * x, y, z) for x, y, z in pts], 'hull', bevel=0.01))
    body.append(box(f'mouth{sx}', (sx * 0.175, 0.3, 0.14), (0.09, 0.03, 0.1), 'mech', 0.004, 1))
    body.append(box(f'lip{sx}', (sx * 0.175, 0.315, 0.195), (0.09, 0.012, 0.01), 'glow', 0, 1))

# ---- Wings: cranked delta, team tops with panel lines; canards ----
plan = [(0.14, 0.52), (0.3, 0.16), (0.98, -0.54), (0.98, -0.72), (0.62, -0.7), (0.14, -0.8)]
body += A.wing('wing', plan, 0.135, 0.06, 0.018, 'team', 'hull_dark', dihedral=-0.02)
body += A.wing('canard', [(0.1, 0.9), (0.34, 0.66), (0.34, 0.6), (0.1, 0.62)], 0.17, 0.025, 0.012, 'team', 'hull_dark', panels=False)
for sx in A.SIDES:
    # Grey leading-edge strakes and trailing flaps that break the team planform into plates.
    body.append(box(f'flap{sx}', (sx * 0.42, -0.74, 0.14), (0.42, 0.07, 0.022), 'hull', 0.005, 1))
    body.append(box(f'elevon{sx}', (sx * 0.82, -0.68, 0.125), (0.24, 0.06, 0.018), 'hull_dark', 0.004, 1))
    # Wingtip rail and AA missile, seeker head and fins.
    x = sx * 0.99
    body.append(box(f'rail{sx}', (x, -0.38, 0.12), (0.03, 0.5, 0.03), 'mech', 0.004, 1))
    body.append(cyl(f'msl{sx}', (x, -0.72, 0.1), (x, 0.02, 0.1), 0.03, 'hull', 10, bevel=0.003))
    body.append(cyl(f'mslnose{sx}', (x, 0.02, 0.1), (x, 0.12, 0.1), 0.03, 'accent', 10, r2=0.004, bevel=0))
    for k, (dx, dz) in enumerate(((0.05, 0), (-0.05, 0), (0, 0.05), (0, -0.05))):
        body.append(box(f'mslfin{sx}{k}', (x + dx * 0.6, -0.66, 0.1 + dz * 0.6), (0.004 + abs(dx) * 0.6, 0.08, 0.004 + abs(dz) * 0.6), 'hull_dark', 0, 1))
    body.append(sphere(f'navlt{sx}', (x, -0.74, 0.135), 0.018, 'glow', 8, 4))
    # Underwing missile on a pylon.
    body.append(box(f'pylon{sx}', (sx * 0.56, -0.18, 0.09), (0.02, 0.26, 0.05), 'mech', 0.004, 1))
    body.append(cyl(f'umsl{sx}', (sx * 0.56, -0.48, 0.05), (sx * 0.56, 0.12, 0.05), 0.028, 'hull', 10, bevel=0.003))
    body.append(cyl(f'umslnose{sx}', (sx * 0.56, 0.12, 0.05), (sx * 0.56, 0.2, 0.05), 0.028, 'accent', 10, r2=0.004, bevel=0))

# ---- Twin canted fins (team outer faces) ----
for sx in A.SIDES:
    fin = prism_x(f'fin{sx}', [(-0.42, 0.26), (-0.84, 0.66), (-0.98, 0.66), (-0.95, 0.26)], -0.018, 0.018, 'team', 0.006, 1)
    A.xform([fin], (0, 0, 0.26), (0, sx * 0.32, 0), (sx * 0.14, 0, 0))
    body.append(fin)
    tip = box(f'fintip{sx}', (0, -0.9, 0.665), (0.04, 0.16, 0.02), 'hull', 0.004, 1)
    A.xform([tip], (0, 0, 0.26), (0, sx * 0.32, 0), (sx * 0.14, 0, 0))
    body.append(tip)
    lt = sphere(f'finlt{sx}', (0, -0.98, 0.64), 0.014, 'glow', 6, 4)
    A.xform([lt], (0, 0, 0.26), (0, sx * 0.32, 0), (sx * 0.14, 0, 0))
    body.append(lt)

# ---- Engine: one big blue-glowing nozzle with a ribbed shroud ----
body += A.thruster('eng', (0, -0.8, 0.165), (0, -1.1, 0.165), 0.13, verts=16)
for k in range(3):
    body.append(cyl(f'rib{k}', (0, -0.86 - k * 0.07, 0.165), (0, -0.88 - k * 0.07, 0.165), 0.125 + k * 0.006, 'metal', 16, bevel=0))
# Spine details: an avionics hump and blue data strips either side of the team spine.
body.append(hull_from('hump', [(0.05, 0.12, 0.3), (-0.05, 0.12, 0.3), (0.06, -0.5, 0.3), (-0.06, -0.5, 0.3),
                               (0.03, 0.0, 0.335), (-0.03, 0.0, 0.335), (0.035, -0.42, 0.335), (-0.035, -0.42, 0.335)], 'hull', bevel=0.008))
for sx in A.SIDES:
    body.append(box(f'strip{sx}', (sx * 0.085, -0.2, 0.296), (0.012, 0.5, 0.008), 'glow_dim', 0, 1))
# Belly: a dark ventral strake and an access panel.
body.append(box('ventral', (0, -0.5, 0.005), (0.04, 0.5, 0.03), 'mech', 0.004, 1))

A.finish('lattice_fighter', [('Body', body, None)], 512)
