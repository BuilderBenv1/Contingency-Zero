"""Lattice "Warden" fortification engineer. Run: python tools/models/lattice_builder_adv_bot.py <out_dir>

Tier 2 walking engineer, built on the Surveyor's sealed pod but broader and armoured: a raised
deflector plate on the nose, heavy team shoulder plates, and in place of the survey antenna a
tall twin-prong projector pylon on its back that throws up turret frames, with glowing coils.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import t2_base as T
import kit
from kit import box, hull_from, cyl, sphere
from bots_parts import beam, finish

c = T.capture('lattice_surveyor')
body = T.drop(c.body, 'ant')
T.transform(body, (1.18, 1.05, 1.0))
# Nose deflector plate.
body.append(hull_from('deflect', [(-0.3, 0.66, 0.5), (0.3, 0.66, 0.5), (-0.34, 0.74, 0.7), (0.34, 0.74, 0.7),
                                  (-0.3, 0.7, 0.86), (0.3, 0.7, 0.86), (-0.28, 0.62, 0.84), (0.28, 0.62, 0.84)], 'hull', 0.015, 1))
body.append(box('deflectg', (0, 0.745, 0.68), (0.4, 0.01, 0.025), 'glow', 0, 1))
body.append(box('deflectt', (0, 0.69, 0.855), (0.5, 0.06, 0.012), 'team', 0.004, 1))
# Shoulder plates.
for sx in (-1, 1):
    body.append(hull_from(f'plate{sx}', [(sx * 0.36, 0.3, 0.8), (sx * 0.36, -0.3, 0.8), (sx * 0.5, 0.26, 0.74), (sx * 0.5, -0.26, 0.74),
                                         (sx * 0.52, 0.22, 0.6), (sx * 0.52, -0.22, 0.6), (sx * 0.38, 0.28, 0.9), (sx * 0.38, -0.28, 0.9)],
                          'team', 0.012, 1))
# Twin-prong projector pylon on the back.
for sx in (-1, 1):
    body.append(beam(f'prong{sx}', (sx * 0.14, -0.46, 0.86), (sx * 0.1, -0.56, 1.62), 0.06, 0.06, 'hull'))
    for k in range(3):
        z = 1.05 + k * 0.17
        t = (z - 0.86) / 0.76
        x, y = sx * (0.14 - 0.04 * t), -0.46 - 0.1 * t
        body.append(cyl(f'pcoil{sx}{k}', (x, y, z), (x, y, z + 0.035), 0.055, 'glow_dim', 10, bevel=0))
    body.append(sphere(f'ptip{sx}', (sx * 0.1, -0.56, 1.65), 0.045, 'glow', 8, 5))
body.append(box('pbridge', (0, -0.52, 1.3), (0.24, 0.05, 0.05), 'mech', 0.006, 1))
body.append(box('pbase', (0, -0.46, 0.9), (0.36, 0.16, 0.08), 'hull_dark', 0.01, 1))

finish('lattice_builder_adv_bot', body)
