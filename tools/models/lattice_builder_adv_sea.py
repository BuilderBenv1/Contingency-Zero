"""Lattice "Shipwright" naval engineer. Run: python tools/models/lattice_builder_adv_sea.py <out_dir>

Tier 2 naval constructor on the Tender's hull, beamier, with a team-topped gantry spanning the
deck amidships that carries a forward-reaching boom with three nanolathe emitters, and armoured
sponsons along both sides.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import t2_base as T
import kit
from kit import box, hull_from, cyl, sphere
import airsea_parts as A

c = T.capture('lattice_builder_sea')
body = c.body
T.transform(body, (1.3, 1.05, 1.05))
DECK = 0.22 * 1.05
for sx in (-1, 1):
    body.append(hull_from(f'spon{sx}', [(sx * 0.5, 0.5, 0.0), (sx * 0.5, -0.7, 0.0), (sx * 0.58, 0.4, 0.12), (sx * 0.58, -0.6, 0.12),
                                        (sx * 0.5, 0.45, DECK), (sx * 0.5, -0.65, DECK)], 'hull', 0.01, 1))
    body.append(box(f'spong{sx}', (sx * 0.585, -0.1, 0.1), (0.01, 0.6, 0.02), 'glow_dim', 0, 1))
    body.append(A.beam(f'gleg{sx}', (sx * 0.44, 0.1, DECK), (sx * 0.38, 0.12, 0.95), 0.06, 0.08, 'hull'))
body.append(box('gbar', (0, 0.12, 0.98), (0.86, 0.12, 0.08), 'team', 0.01, 1))
body.append(box('gbarg', (0, 0.055, 0.97), (0.6, 0.01, 0.025), 'glow', 0, 1))
body.append(A.beam('boom', (0, 0.12, 0.96), (0, 0.85, 0.7), 0.08, 0.06, 'hull_dark'))
for k, x in enumerate((-0.12, 0.0, 0.12)):
    body += A.nano_tip(f'nt{k}', (x, 0.82, 0.68), (x, 0.98, 0.56), 0.025)

A.finish('lattice_builder_adv_sea', [('Body', body, None)], 512)
