"""Lattice "Overseer" flying engineer. Run: python tools/models/lattice_builder_adv_air.py <out_dir>

Tier 2 air constructor: the Kite's body widened, with a pair of outrigger engine nacelles on
short pylons, three nanolathe emitters under the nose, and a dorsal sensor dome with a team
collar. Builds anything, slowly.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import t2_base as T
import kit
from kit import box, hull_from, cyl, sphere
import airsea_parts as A

c = T.capture('lattice_builder_air')
body = c.body
T.transform(body, (1.2, 1.08, 1.05))
for sx in (-1, 1):
    x = sx * 1.12
    body.append(A.beam(f'pylon{sx}', (sx * 0.7, -0.1, 0.12), (x, -0.15, 0.14), 0.18, 0.05, 'hull'))
    body.append(hull_from(f'nac{sx}', [(x + dx, y, z) for y, r in ((0.45, 0.07), (-0.55, 0.14)) for dx, z in
                                       ((-r, 0.14), (r, 0.14), (0, 0.14 + r), (0, 0.14 - r))], 'hull', 0.01, 1))
    body.append(box(f'nact{sx}', (x, -0.05, 0.26), (0.08, 0.6, 0.012), 'team', 0.003, 1))
    body += A.thruster(f'thr{sx}', (x, -0.5, 0.14), (x, -0.68, 0.14), 0.12)
for k, x in enumerate((-0.22, 0.0, 0.22)):
    body += A.nano_tip(f'nt{k}', (x, 0.5, -0.06), (x, 0.72, -0.16), 0.03)
body.append(cyl('domec', (0, -0.1, 0.42), (0, -0.1, 0.48), 0.2, 'team', 14, bevel=0.005))
body.append(sphere('dome', (0, -0.1, 0.5), 0.15, 'glass', 12, 6))
body.append(sphere('domeg', (0, -0.1, 0.6), 0.04, 'glow', 8, 5))

A.finish('lattice_builder_adv_air', [('Body', body, None)], 512)
