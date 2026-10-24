"""Crucible "Dozer" builder vehicle. Run: python tools/models/crucible_builder_vehicle.py <out_dir>

An armoured construction tractor: riveted tub on the Anvil's tracks, a big curved dozer blade in
team paint on push arms and rams, a caged driver's cab at the back with a warning beacon, a
smokestack, and a heavy hydraulic nanolathe crane in the middle of the deck.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import vehicles_parts as vp

kit.reset('crucible')
body = []

# ---- Tracks (shorter at the front to leave room for the blade) ----
body += vp.crucible_tracks(0.46, 0.8, -1.05, 0.9, r_end=0.2, wheels=5, cleats=19, skirt='team', skirt_rivets=8)

# ---- Hull: square tub, upper deck in team paint ----
HW = 0.46
body.append(box('tub', (0, -0.08, 0.28), (2 * HW, 1.9, 0.26), 'hull_dark', 0.02))
upper = prism_x('upper', [(0.92, 0.32), (0.74, 0.58), (-0.94, 0.58), (-1.02, 0.5), (-1.02, 0.3), (0.92, 0.3)],
                -0.6, 0.6, 'hull', bevel=0.025, segs=2)
body.append(upper)
# Team deck plates: a big one round the crane and a sloped one on the glacis.
body.append(box('deckplate', (0, 0.17, 0.59), (1.04, 0.92, 0.025), 'team', 0.008, 1))
body.append(prism_x('glacisplate', [(0.9, 0.36), (0.76, 0.56), (0.72, 0.575), (0.86, 0.38)], -0.5, 0.5, 'team', 0.006, 1))
body += rivets('frontrv', (-0.5, 0.7, 0.595), (0.5, 0.7, 0.595), 7, 'metal', 0.02)
body += rivets('rearrv', (-0.5, -0.98, 0.595), (0.5, -0.98, 0.595), 7, 'metal', 0.02)
for sx in (-1, 1):
    body += rivets(f'siderv{sx}', (sx * 0.57, 0.6, 0.595), (sx * 0.57, -0.85, 0.595), 6, 'metal', 0.018)

# ---- Dozer blade: a curved mouldboard on push arms, rams from the hull ----
BY, BZ = 1.12, 0.03
prof = []
for i in range(9):
    t = i / 8  # concave mouldboard: bottom edge forward, belly back, top lip curling forward
    prof.append((BY - 0.13 * math.sin(math.pi * t) + 0.08 * t * t, BZ + 0.6 * t))
outer = [(y, z) for y, z in prof]
inner = [(y - 0.06, z) for y, z in prof[::-1]]
blade = prism_x('blade', outer + inner, -0.86, 0.86, 'team', bevel=0.012, segs=1)
body.append(blade)
body.append(box('edge', (0, BY - 0.02, BZ + 0.02), (1.74, 0.07, 0.05), 'metal', 0.01, 1))
for sx in (-1, 1):
    body.append(prism_x(f'endplate{sx}', [(BY + 0.02, BZ), (BY - 0.22, BZ), (BY - 0.26, BZ + 0.58), (BY + 0.06, BZ + 0.62)],
                        sx * 0.86 - 0.025, sx * 0.86 + 0.025, 'hull_dark', 0.008, 1))
# Back ribs of the blade.
for k in range(5):
    x = -0.7 + k * 0.35
    body.append(box(f'rib{k}', (x, BY - 0.2, BZ + 0.28), (0.04, 0.08, 0.46), 'hull_dark', 0.008, 1))
body += rivets('bladerv', (-0.78, BY - 0.035, BZ + 0.47), (0.78, BY - 0.035, BZ + 0.47), 9, 'metal', 0.018)
body.append(box('hazard', (0, BY + 0.04, BZ + 0.6), (1.6, 0.05, 0.04), 'accent', 0.006, 1))
for sx in (-1, 1):
    body.append(vp.beam(f'push{sx}', (sx * 0.86, 0.45, 0.3), (sx * 0.72, BY - 0.22, 0.2), 0.08, 0.1, 'hull_dark'))
    body.append(cyl(f'pivot{sx}', (sx * 0.9, 0.45, 0.3), (sx * 0.82, 0.45, 0.3), 0.06, 'mech', 10))
    body.append(cyl(f'ram{sx}', (sx * 0.4, 0.62, 0.58), (sx * 0.4, BY - 0.22, 0.42), 0.035, 'metal', 8, bevel=0))
    body.append(cyl(f'ramc{sx}', (sx * 0.4, 0.62, 0.58), (sx * 0.4, 0.86, 0.52), 0.055, 'mech', 8, bevel=0))
# Headlights under the blade top.
for sx in (-1, 1):
    body.append(box(f'lamphood{sx}', (sx * 0.3, 0.8, 0.62), (0.12, 0.08, 0.08), 'mech', 0.01, 1))
    body.append(box(f'lamp{sx}', (sx * 0.3, 0.845, 0.62), (0.08, 0.01, 0.045), 'glow', 0, 1))

# ---- Cab at the back left: riveted box, glass, roll cage, beacon ----
CX, CY = -0.24, -0.62
body.append(box('cab', (CX, CY, 0.78), (0.44, 0.5, 0.4), 'team', 0.02))
body.append(box('cabroof', (CX, CY, 1.0), (0.48, 0.54, 0.05), 'hull', 0.012, 1))
body.append(box('glassF', (CX, CY + 0.252, 0.85), (0.36, 0.01, 0.14), 'glass', 0, 1))
for sx in (-1, 1):
    body.append(box(f'glassS{sx}', (CX + sx * 0.222, CY + 0.05, 0.85), (0.01, 0.3, 0.12), 'glass', 0, 1))
for x in (CX - 0.22, CX + 0.22):
    for y in (CY - 0.25, CY + 0.25):
        body.append(cyl(f'cage{x}{y}', (x, y, 1.02), (x, y, 1.12), 0.014, 'mech', 6, bevel=0))
for y in (CY - 0.25, CY + 0.25):
    body.append(cyl(f'cagex{y}', (CX - 0.22, y, 1.12), (CX + 0.22, y, 1.12), 0.014, 'mech', 6, bevel=0))
for x in (CX - 0.22, CX + 0.22):
    body.append(cyl(f'cagey{x}', (x, CY - 0.25, 1.12), (x, CY + 0.25, 1.12), 0.014, 'mech', 6, bevel=0))
body.append(cyl('beaconb', (CX + 0.12, CY - 0.12, 1.02), (CX + 0.12, CY - 0.12, 1.06), 0.04, 'mech', 8, bevel=0))
body.append(sphere('beacon', (CX + 0.12, CY - 0.12, 1.08), 0.035, 'glow', 8, 5))
body += rivets('cabrv', (CX - 0.2, CY + 0.255, 0.95), (CX + 0.2, CY + 0.255, 0.95), 5, 'metal', 0.016)
# Smokestack and tool lockers on the right.
body += vp.stack('stack', (0.34, -0.74, 0.58), 0.07, 0.42)
body.append(box('locker', (0.32, -0.36, 0.64), (0.22, 0.26, 0.12), 'accent', 0.012, 1))
body.append(box('lockerlatch', (0.32, -0.228, 0.64), (0.05, 0.01, 0.04), 'metal', 0, 1))
body.append(box('drum', (0.3, -1.06, 0.42), (0.3, 0.07, 0.16), 'accent', 0.01, 1))

# ---- Nanolathe crane in the middle of the deck ----
body.append(box('cranepad', (0.04, 0.1, 0.6), (0.5, 0.5, 0.04), 'hull_dark', 0.01, 1))
body += vp.nano_arm('arm', (0.04, 0.1, 0.62), 0.92, 0, 'crucible')

vp.finish('crucible_builder_vehicle', body)
