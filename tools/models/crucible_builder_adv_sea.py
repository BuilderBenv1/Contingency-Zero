"""Crucible "Salvage Barge" naval engineer. Run: python tools/models/crucible_builder_adv_sea.py <out_dir>

A twin-hulled heavy work catamaran: two riveted pontoon hulls joined by a deck, spanned by a big
team-painted gantry crane carrying a hanging grab claw over the open well between the hulls,
two hydraulic nanolathe arms with orange tips at the bow corners, a deckhouse with a lit window
slot and a hot stack at the stern, coiled cable drums and fenders. Two hulls and a gantry make
it unmistakable next to the single-hulled Dredger. The waterline is z = 0.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import box, hull_from, prism_x, cyl, sphere, rivets, inset_panels
import airsea_parts as A

kit.reset('crucible')
F = 'crucible'
body = []
for sx in A.SIDES:
    h, _ = A.ship_hull(F, 0.17, 0.16, -0.12, bow=1.15, stern=-1.1)
    body += A.xform(h, move=(sx * 0.36, 0, 0))
    body += rivets(f'hr{sx}', (sx * 0.534, 0.9, 0.08), (sx * 0.534, -0.95, 0.08), 9, 'metal', 0.014)
# Cross decks fore and aft; the middle is an open well
for y, l in ((0.86, 0.32), (-0.72, 0.62)):
    body.append(box(f'deck{y}', (0, y, 0.18), (0.88, l, 0.06), 'hull_dark', 0.01, 1))
for sx in A.SIDES:
    body.append(box(f'walk{sx}', (sx * 0.36, 0.05, 0.19), (0.2, 1.3, 0.04), 'hull_dark', 0.006, 1))
# Gantry crane: two team legs per side and a girder across, a trolley and grab claw
for sx in A.SIDES:
    for y in (0.3, -0.12):
        body.append(box(f'gleg{sx}{y}', (sx * 0.36, y, 0.48), (0.06, 0.06, 0.6), 'team', 0.006, 1))
    body.append(box(f'gtop{sx}', (sx * 0.36, 0.09, 0.8), (0.07, 0.5, 0.06), 'team_dark', 0.006, 1))
body.append(box('girder', (0, 0.09, 0.84), (0.82, 0.12, 0.08), 'accent', 0.008, 1))
body += rivets('grv', (-0.36, 0.03, 0.885), (0.36, 0.03, 0.885), 7, 'metal', 0.013)
body.append(box('trolley', (0, 0.09, 0.76), (0.16, 0.16, 0.08), 'hull_dark', 0.008, 1))
body.append(cyl('cable', (0, 0.09, 0.72), (0, 0.09, 0.42), 0.008, 'metal', 5, bevel=0))
body.append(box('claw', (0, 0.09, 0.38), (0.14, 0.14, 0.08), 'hull', 0.01, 1))
for a in range(4):
    ang = a * math.pi / 2
    tip = (math.cos(ang) * 0.1, 0.09 + math.sin(ang) * 0.1, 0.22)
    body.append(cyl(f'tine{a}', (math.cos(ang) * 0.05, 0.09 + math.sin(ang) * 0.05, 0.36), tip, 0.014, 'mech', 5, bevel=0))
# Nanolathe arms at the bow corners
for sx in A.SIDES:
    body.append(cyl(f'abase{sx}', (sx * 0.36, 0.92, 0.21), (sx * 0.36, 0.92, 0.27), 0.06, 'mech', 8, bevel=0))
    body.append(box(f'aup{sx}', (sx * 0.36, 1.0, 0.36), (0.05, 0.05, 0.22), 'team', 0.004, 1))
    body += A.nano_tip(f'atip{sx}', (sx * 0.36, 1.0, 0.46), (sx * 0.3, 1.2, 0.36), 0.03)
# Stern deckhouse, stack, cable drums
body.append(box('house', (0, -0.78, 0.36), (0.44, 0.34, 0.3), 'hull', 0.015, 2))
body.append(box('houseroof', (0, -0.78, 0.52), (0.46, 0.36, 0.02), 'team', 0.006, 1))
body.append(box('hwin', (0, -0.605, 0.42), (0.34, 0.01, 0.04), 'glow_dim', 0, 1))
body.append(cyl('stack', (0.14, -0.86, 0.53), (0.14, -0.86, 0.74), 0.045, 'metal', 10, r2=0.04, bevel=0.004))
body.append(cyl('stglow', (0.14, -0.86, 0.74), (0.14, -0.86, 0.746), 0.035, 'glow', 10, bevel=0))
for sx in A.SIDES:
    body.append(cyl(f'drum{sx}', (sx * 0.28, -0.5, 0.28), (sx * 0.4, -0.5, 0.28), 0.07, 'accent', 12, bevel=0.004))
    for k in range(3):
        body.append(cyl(f'fender{sx}{k}', (sx * 0.54, 0.4 - k * 0.4, 0.06), (sx * 0.56, 0.4 - k * 0.4, 0.06), 0.05, 'tread', 10, bevel=0))

A.finish('crucible_builder_adv_sea', [('Body', body, None)], 512)
