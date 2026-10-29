"""Lattice "Eclipse" heavy bomber. Run: python tools/models/lattice_heavy_bomber.py <out_dir>

Tier 2 strategic bomber: the Lancet's flying wing enlarged into a broad, deep crescent with a
raised dorsal spine, four engine nacelles slung along the trailing edge, twin tail fins, and
a long glowing bomb bay down the belly.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import t2_base as T
import kit
from kit import box, hull_from, cyl, sphere
import airsea_parts as A

c = T.capture('lattice_bomber')
body = c.body
T.transform(body, (1.3, 1.15, 1.6))
body.append(hull_from('spine', [(-0.14, 0.9, 0.5), (0.14, 0.9, 0.5), (-0.2, -0.9, 0.5), (0.2, -0.9, 0.5),
                                (-0.08, 0.7, 0.72), (0.08, 0.7, 0.72), (-0.12, -0.8, 0.74), (0.12, -0.8, 0.74)], 'hull', 0.012, 1))
body.append(box('spinet', (0, -0.05, 0.745), (0.14, 1.4, 0.012), 'team', 0.003, 1))
body.append(box('canopy', (0, 0.75, 0.66), (0.1, 0.2, 0.06), 'glass', 0.01, 1))
for sx in (-1, 1):
    for k, x in enumerate((0.45, 0.95)):
        X = sx * x
        body.append(cyl(f'nac{sx}{k}', (X, 0.2, 0.1), (X, -0.86, 0.1), 0.06, 'hull_dark', 12, r2=0.11, bevel=0.006))
        body.append(box(f'nacp{sx}{k}', (X, -0.3, 0.2), (0.04, 0.5, 0.12), 'hull', 0.006, 1))
        body += A.thruster(f'eng{sx}{k}', (X, -0.86, 0.1), (X, -1.02, 0.1), 0.11)
    body.append(hull_from(f'fin{sx}', [(sx * 0.3, -0.6, 0.6), (sx * 0.3, -1.0, 0.6), (sx * 0.31, -0.7, 0.62),
                                       (sx * 0.42, -0.95, 1.05), (sx * 0.42, -1.1, 1.05), (sx * 0.43, -1.0, 1.05)], 'hull', 0.006, 1))
    body.append(box(f'finband{sx}', (sx * 0.38, -0.9, 0.86), (0.012, 0.16, 0.08), 'team', 0, 1))
body.append(box('bay', (0, -0.05, -0.012), (0.3, 1.2, 0.02), 'mech', 0.004, 1))
body.append(box('bayg', (0, -0.05, -0.024), (0.04, 1.1, 0.008), 'glow', 0, 1))

A.finish('lattice_heavy_bomber', [('Body', body, None)], 512)
