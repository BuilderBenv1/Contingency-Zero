"""Crucible "Hammerhead" destroyer. Run: python tools/models/crucible_destroyer.py <out_dir>

A heavy riveted gunboat: a blunt hull with a flared ram plate at the bow (the hammerhead), a boxy
superstructure with a team roof, a dark bridge block with an orange window slot, twin hot
smokestacks, lifeboats, a small aft gun and depth-charge racks, and a big square main turret with
a thick barrel forward (the turret part). The waterline is z = 0: the hull dips below it.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('crucible')
F = 'crucible'
HW, DECK, KEEL = 0.34, 0.2, -0.12
body, secs = A.ship_hull(F, HW, DECK, KEEL)
for sx in A.SIDES:
    body += rivets(f'hr{sx}', (sx * (HW + 0.004), 0.9, 0.1), (sx * (HW + 0.004), -1.0, 0.1), 10, 'metal', 0.014)
# The hammerhead: a flared ram plate across the bow, with bolts.
body.append(hull_from('ram', [(0.2, 1.24, 0.02), (-0.2, 1.24, 0.02), (0.22, 1.2, DECK + 0.05), (-0.22, 1.2, DECK + 0.05),
                              (0.16, 1.1, 0.0), (-0.16, 1.1, 0.0), (0.18, 1.06, DECK + 0.06), (-0.18, 1.06, DECK + 0.06)], 'hull_dark', bevel=0.012))
body += rivets('ramr', (-0.15, 1.235, DECK - 0.02), (0.15, 1.235, DECK - 0.02), 5, 'metal', 0.014, normal=(0, 1, 0))
body.append(box('foredeck', (0, 0.9, DECK + 0.006), (0.3, 0.2, 0.012), 'team_dark', 0.004, 1))
for sx in A.SIDES:
    body.append(box(f'bitt{sx}', (sx * 0.2, 1.0, DECK + 0.03), (0.04, 0.04, 0.05), 'metal', 0.006, 1))

# ---- Superstructure ----
sup = box('sup', (0, -0.2, 0.32), (0.42, 0.62, 0.24), 'hull', 0.02, 2)
inset_panels(sup, 0.03, 0.008, lambda f: abs(f.normal.x) > 0.9)
body.append(sup)
roof = box('roof', (0, -0.2, 0.445), (0.44, 0.64, 0.025), 'team', 0.008, 1)
inset_panels(roof, 0.03, 0.006, lambda f: f.normal.z > 0.9)
body.append(roof)
body.append(box('bridge', (0, -0.02, 0.51), (0.3, 0.22, 0.12), 'hull_dark', 0.014, 1))
body.append(box('broof', (0, -0.02, 0.58), (0.32, 0.24, 0.02), 'team', 0.006, 1))
body.append(box('bwin', (0, 0.092, 0.52), (0.24, 0.01, 0.04), 'glow_dim', 0, 1))
body += rivets('rr', (-0.19, 0.08, 0.46), (0.19, 0.08, 0.46), 6, 'metal', 0.014)
for sx in A.SIDES:
    # Lifeboats in davits.
    body.append(cyl(f'boat{sx}', (sx * 0.25, -0.3, 0.38), (sx * 0.25, -0.52, 0.38), 0.04, 'accent', 8, bevel=0.006))
    for y in (-0.33, -0.49):
        body.append(cyl(f'davit{sx}{y}', (sx * 0.2, y, 0.3), (sx * 0.26, y, 0.44), 0.008, 'metal', 4, bevel=0))
# Twin smokestacks with team bands and glowing mouths.
for k, y in enumerate((-0.3, -0.46)):
    body.append(cyl(f'stack{k}', (0, y, 0.44), (0, y, 0.72), 0.055, 'metal', 10, r2=0.048, bevel=0.004))
    body.append(cyl(f'stband{k}', (0, y, 0.55), (0, y, 0.6), 0.06, 'team_dark', 10, bevel=0))
    body.append(cyl(f'strim{k}', (0, y, 0.7), (0, y, 0.74), 0.058, 'mech', 10, bevel=0))
    body.append(cyl(f'stglow{k}', (0, y, 0.735), (0, y, 0.742), 0.045, 'glow', 10, bevel=0))
body.append(cyl('mast', (0.1, -0.05, 0.6), (0.1, -0.05, 0.86), 0.012, 'metal', 6, bevel=0))
body.append(box('yard', (0.1, -0.05, 0.8), (0.2, 0.02, 0.02), 'metal', 0, 1))
body.append(sphere('masttip', (0.1, -0.05, 0.87), 0.02, 'glow', 8, 4))
# ---- Aft: small fixed gun facing back, depth-charge racks ----
body.append(box('aftgun', (0, -0.82, 0.26), (0.2, 0.2, 0.1), 'team', 0.012, 1))
body += A.barrel('aftbarrel', (0, -0.9, 0.27), (0, -1.14, 0.27), 0.024, brake=False)
for sx in A.SIDES:
    body.append(box(f'rack{sx}', (sx * 0.22, -1.0, DECK + 0.03), (0.06, 0.18, 0.04), 'mech', 0.004, 1))
    for k in range(3):
        body.append(cyl(f'dc{sx}{k}', (sx * 0.19, -0.94 - k * 0.055, DECK + 0.075), (sx * 0.25, -0.94 - k * 0.055, DECK + 0.075), 0.022, 'accent', 8, bevel=0))

# ---- Main turret ----
PIV = (0, 0.56, DECK)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = [cyl('race', P(0, 0, -0.005), P(0, 0, 0.025), 0.19, 'mech', 12, bevel=0.004)]
tb = box('tbox', P(0, -0.02, 0.09), (0.3, 0.34, 0.14), 'team', 0.02, 2)
inset_panels(tb, 0.025, 0.006, lambda f: f.normal.z > 0.9)
tur.append(tb)
tur.append(box('tface', P(0, 0.155, 0.08), (0.26, 0.03, 0.12), 'hull_dark', 0.008, 1))
tur.append(box('tbustle', P(0, -0.21, 0.08), (0.24, 0.06, 0.1), 'hull_dark', 0.008, 1))
tur += rivets('trv', P(-0.11, 0.1, 0.165), P(0.11, 0.1, 0.165), 3, 'metal', 0.016)
tur.append(box('sight', P(0.1, -0.06, 0.18), (0.06, 0.08, 0.04), 'hull_dark', 0.006, 1))
tur += A.barrel('gun', P(0, 0.16, 0.08), P(0, 0.68, 0.08), 0.045)
tur.append(cyl('sleeve', P(0, 0.16, 0.08), P(0, 0.3, 0.08), 0.06, 'hull_dark', 10, bevel=0.004))
tur.append(cyl('fume', P(0, 0.4, 0.08), P(0, 0.47, 0.08), 0.058, 'accent', 10, bevel=0))

A.finish('crucible_destroyer', [('Body', body, None), ('Turret', tur, PIV)], 512)
