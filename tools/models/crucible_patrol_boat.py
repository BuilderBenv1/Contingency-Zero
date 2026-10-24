"""Crucible "Cutter" patrol boat. Run: python tools/models/crucible_patrol_boat.py <out_dir>

A blunt riveted workboat with teeth: slab sides hung with tyre fenders, a boxy wheelhouse with a
team roof and an orange-lit window slot, a hot smokestack, a three-cell AA rocket box aft, a bow
anchor and searchlight, and a square gun turret with a thick barrel forward (the turret part).
The waterline is z = 0: the hull dips below it.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('crucible')
F = 'crucible'
HW, DECK, KEEL = 0.44, 0.26, -0.16
body, secs = A.ship_hull(F, HW, DECK, KEEL)
for sx in A.SIDES:
    body += rivets(f'drv{sx}', (sx * HW * 0.82, 0.8, DECK + 0.005), (sx * HW * 0.82, -1.0, DECK + 0.005), 9, 'metal', 0.016)
    # Tyre fenders hung on the sides.
    for k, y in enumerate((0.45, 0.05, -0.35, -0.75)):
        body.append(cyl(f'tyre{sx}{k}', (sx * (HW + 0.005), y, 0.12), (sx * (HW + 0.05), y, 0.12), 0.07, 'tread', 10, bevel=0.01))
    body.append(box(f'bitt{sx}', (sx * 0.3, 0.75, DECK + 0.03), (0.06, 0.06, 0.06), 'metal', 0.008, 1))
# Bow: anchor in a hawse, and a searchlight.
body.append(box('anchor', (0.16, 1.14, 0.18), (0.02, 0.08, 0.1), 'mech', 0.006, 1))
body.append(cyl('slight', (0, 0.92, DECK + 0.02), (0, 0.92, DECK + 0.1), 0.04, 'metal', 8, bevel=0))
body.append(cyl('slamp', (0, 0.92, DECK + 0.13), (0, 0.98, DECK + 0.13), 0.05, 'mech', 10, bevel=0.004))
body.append(cyl('slampglow', (0, 0.98, DECK + 0.13), (0, 0.99, DECK + 0.13), 0.04, 'glow', 10, bevel=0))
# Team deck plate aft of the turret.
body.append(box('deckplate', (0, -0.62, DECK + 0.006), (0.56, 0.44, 0.012), 'team_dark', 0.004, 1))

# ---- Wheelhouse ----
wh = box('wheelhouse', (0, -0.14, 0.42), (0.5, 0.52, 0.32), 'hull', 0.02, 2)
inset_panels(wh, 0.03, 0.008, lambda f: abs(f.normal.x) > 0.9)
body.append(wh)
roof = box('roof', (0, -0.14, 0.595), (0.54, 0.56, 0.04), 'team', 0.01, 1)
inset_panels(roof, 0.03, 0.008, lambda f: f.normal.z > 0.9)
body.append(roof)
body.append(box('window', (0, 0.123, 0.5), (0.4, 0.02, 0.06), 'glow_dim', 0, 1))
for x in (-0.1, 0.0, 0.1):
    body.append(box(f'wbar{x}', (x, 0.128, 0.5), (0.02, 0.02, 0.07), 'mech', 0, 1))
body.append(box('visorhood', (0, 0.14, 0.54), (0.46, 0.06, 0.02), 'hull_dark', 0.004, 1))
body += rivets('wr', (-0.24, 0.1, 0.62), (0.24, 0.1, 0.62), 6, 'metal', 0.016)
# Stack and a radio mast.
body.append(cyl('stack', (0.12, -0.32, 0.58), (0.12, -0.32, 0.88), 0.06, 'metal', 10, r2=0.052, bevel=0.004))
body.append(cyl('stackband', (0.12, -0.32, 0.7), (0.12, -0.32, 0.75), 0.066, 'accent', 10, bevel=0))
body.append(cyl('stackrim', (0.12, -0.32, 0.86), (0.12, -0.32, 0.9), 0.064, 'mech', 10, bevel=0))
body.append(cyl('stackglow', (0.12, -0.32, 0.895), (0.12, -0.32, 0.902), 0.05, 'glow', 10, bevel=0))
body.append(cyl('mast', (-0.14, -0.3, 0.6), (-0.14, -0.3, 0.92), 0.012, 'metal', 6, bevel=0))
body.append(box('mastyard', (-0.14, -0.3, 0.84), (0.18, 0.02, 0.02), 'metal', 0, 1))
body.append(sphere('masttip', (-0.14, -0.3, 0.93), 0.022, 'glow', 8, 4))
# ---- AA rocket box aft ----
body.append(box('aamount', (0, -0.8, DECK + 0.04), (0.24, 0.24, 0.08), 'hull_dark', 0.01, 1))
body += A.missile_pod('aa', (0, -0.8, 0.39), (0.36, 0.24, 0.15), 40, 3, 1, 'team', tip='accent', back=True)
# Stern: a towing bitt and exhaust glow.
body.append(cyl('towbitt', (0, -1.0, DECK), (0, -1.0, DECK + 0.08), 0.04, 'metal', 8, bevel=0.004))
for sx in A.SIDES:
    body.append(box(f'exhaust{sx}', (sx * 0.2, -1.155, 0.12), (0.08, 0.02, 0.04), 'glow_dim', 0, 1))

# ---- Square gun turret ----
PIV = (0, 0.58, DECK)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = [cyl('race', P(0, 0, -0.01), P(0, 0, 0.03), 0.2, 'mech', 12, bevel=0.004)]
tb = box('tbox', P(0, -0.02, 0.1), (0.34, 0.36, 0.16), 'team', 0.02, 2)
inset_panels(tb, 0.025, 0.006, lambda f: f.normal.z > 0.9)
tur.append(tb)
tur.append(box('tface', P(0, 0.165, 0.09), (0.3, 0.03, 0.13), 'hull_dark', 0.008, 1))
tur.append(box('tback', P(0, -0.21, 0.09), (0.28, 0.04, 0.1), 'hull_dark', 0.008, 1))
tur += rivets('trv', P(-0.13, 0.12, 0.185), P(0.13, 0.12, 0.185), 4, 'metal', 0.014)
tur.append(cyl('hatch', P(-0.08, -0.06, 0.18), P(-0.08, -0.06, 0.2), 0.05, 'hull_dark', 10, bevel=0))
tur += A.barrel('gun', P(0, 0.17, 0.09), P(0, 0.64, 0.09), 0.042)
tur.append(cyl('sleeve', P(0, 0.17, 0.09), P(0, 0.3, 0.09), 0.055, 'hull_dark', 10, bevel=0.004))

A.finish('crucible_patrol_boat', [('Body', body, None), ('Turret', tur, PIV)], 512)
