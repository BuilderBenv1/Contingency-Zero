"""Lattice "Tidewarden" cruiser. Run: python tools/models/lattice_cruiser.py <out_dir>

Tier 2 fleet escort on the Reef's needle hull, beamier and taller: a bigger forward gun
turret (the turret part), twin flak mounts on the superstructure roof, a tall lattice tower
amidships, and team armour belts along the waterline.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import t2_base as T
import kit
from kit import box, hull_from, cyl, sphere
import airsea_parts as A

c = T.capture('lattice_destroyer')
body, tur = c.body, c.turret
T.transform(body, (1.35, 1.0, 1.12))
PIV = (c.pivot[0], c.pivot[1], c.pivot[2] * 1.12)
T.transform(tur, (1.3, 1.25, 1.25), move=(0, 0, PIV[2] - c.pivot[2]), pivot=c.pivot)
for sx in (-1, 1):
    body.append(box(f'belt{sx}', (sx * 0.43, -0.1, 0.1), (0.02, 1.5, 0.06), 'team', 0.004, 1))
    # Twin flak mounts on the tier 1 roof.
    x, y, z = sx * 0.16, -0.42, 0.44
    body.append(cyl(f'fbase{sx}', (x, y, z), (x, y, z + 0.05), 0.07, 'mech', 12, bevel=0.004))
    body.append(box(f'fhouse{sx}', (x, y, z + 0.09), (0.1, 0.1, 0.07), 'hull', 0.008, 1))
    for dx in (-0.025, 0.025):
        body += A.barrel(f'fb{sx}{dx}', (x + dx, y + 0.04, z + 0.1), (x + dx, y + 0.2, z + 0.2), 0.012, brake=False)
# Lattice tower amidships.
for dx in (-0.06, 0.06):
    for dy in (-0.06, 0.06):
        body.append(A.beam(f'tw{dx}{dy}', (dx, -0.2 + dy, 0.5), (dx * 0.4, -0.2 + dy * 0.4, 1.12), 0.018, 0.018, 'metal'))
for k in range(3):
    body.append(box(f'twb{k}', (0, -0.2, 0.62 + k * 0.16), (0.1 - k * 0.02, 0.1 - k * 0.02, 0.012), 'mech', 0, 1))
body.append(box('twtop', (0, -0.2, 1.14), (0.12, 0.05, 0.03), 'team', 0.004, 1))
body.append(sphere('twg', (0, -0.2, 1.19), 0.03, 'glow', 8, 5))

A.finish('lattice_cruiser', [('Body', body, None), ('Turret', tur, PIV)], 512)
