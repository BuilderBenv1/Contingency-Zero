"""Lattice "Falcon" interceptor. Run: python tools/models/lattice_interceptor.py <out_dir>

Tier 2 air superiority: the Swift's arrowhead stretched long and narrow, with forward canards,
twin canted tail fins, two large engines side by side at the back and missile rails under the
wings. Reads longer and sharper than the Swift from above.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import t2_base as T
import kit
from kit import box, hull_from, cyl, sphere
import airsea_parts as A

c = T.capture('lattice_fighter')
body = c.body
T.transform(body, (0.9, 1.25, 0.95))
for sx in (-1, 1):
    body.append(hull_from(f'canard{sx}', [(sx * 0.12, 1.0, 0.24), (sx * 0.12, 0.7, 0.24), (sx * 0.48, 0.78, 0.25),
                                          (sx * 0.48, 0.72, 0.25), (sx * 0.12, 0.9, 0.27), (sx * 0.12, 0.75, 0.27)], 'team', 0.004, 1))
    body.append(hull_from(f'fin{sx}', [(sx * 0.3, -0.9, 0.32), (sx * 0.3, -1.35, 0.32), (sx * 0.31, -1.0, 0.33),
                                       (sx * 0.5, -1.3, 0.8), (sx * 0.5, -1.45, 0.8), (sx * 0.51, -1.35, 0.8)], 'hull', 0.006, 1))
    body.append(box(f'fing{sx}', (sx * 0.47, -1.32, 0.7), (0.01, 0.08, 0.1), 'glow', 0, 1))
    body += A.thruster(f'eng{sx}', (sx * 0.2, -1.0, 0.24), (sx * 0.2, -1.55, 0.24), 0.17)
    for k in range(2):
        x = sx * (0.55 + k * 0.25)
        body.append(cyl(f'msl{sx}{k}', (x, -0.2, 0.08), (x, 0.4, 0.08), 0.035, 'hull_dark', 8, bevel=0.004))
        body.append(cyl(f'mslt{sx}{k}', (x, 0.4, 0.08), (x, 0.5, 0.08), 0.035, 'accent', 8, r2=0.0, bevel=0))

A.finish('lattice_interceptor', [('Body', body, None)], 512)
