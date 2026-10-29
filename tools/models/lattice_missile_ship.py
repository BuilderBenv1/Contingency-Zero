"""Lattice "Starfall" missile ship. Run: python tools/models/lattice_missile_ship.py <out_dir>

Tier 2 cruise-missile ship on a widened Reef hull: the forward gun is replaced by a grid of
vertical launch cells with glowing hatches, and the stern carries a trainable launcher (the
turret part) with two long cruise missiles on raised rails.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import t2_base as T
import kit
from kit import box, hull_from, cyl, sphere
import airsea_parts as A
import bpy

c = T.capture('lattice_destroyer')
for o in c.turret:
    bpy.data.objects.remove(o, do_unlink=True)
body = c.body
T.transform(body, (1.3, 1.0, 1.08))
DECK = 0.18 * 1.08
# Forward launch cells.
body.append(box('vls', (0, 0.66, DECK + 0.04), (0.3, 0.42, 0.08), 'hull_dark', 0.008, 1))
for i in range(3):
    for j in range(4):
        x, y = -0.09 + i * 0.09, 0.5 + j * 0.1
        body.append(box(f'cell{i}{j}', (x, y, DECK + 0.085), (0.07, 0.08, 0.012), 'hull', 0.004, 1))
        body.append(box(f'cellg{i}{j}', (x, y, DECK + 0.093), (0.03, 0.04, 0.004), 'glow_dim', 0, 1))
body.append(box('vlsband', (0, 0.88, DECK + 0.05), (0.3, 0.02, 0.06), 'team', 0.004, 1))
# Stern launcher.
PIV = (0, -0.78, DECK)
P = lambda x, y, z: (PIV[0] + x, PIV[1] + y, PIV[2] + z)
tur = [cyl('lring', P(0, 0, 0), P(0, 0, 0.04), 0.16, 'mech', 14, bevel=0.004),
       box('lbase', P(0, 0, 0.08), (0.22, 0.26, 0.08), 'hull', 0.01, 1)]
for sx in (-1, 1):
    x = sx * 0.07
    tur.append(A.beam(f'rail{sx}', P(x, -0.16, 0.12), P(x, 0.22, 0.24), 0.05, 0.03, 'hull_dark'))
    tur.append(cyl(f'msl{sx}', P(x, -0.18, 0.16), P(x, 0.26, 0.29), 0.032, 'hull', 10, bevel=0.004))
    tur.append(cyl(f'mslt{sx}', P(x, 0.26, 0.29), P(x, 0.34, 0.315), 0.032, 'accent', 10, r2=0.0, bevel=0))
    tur.append(box(f'mslb{sx}', P(x, 0.02, 0.23), (0.07, 0.06, 0.01), 'team', 0.002, 1))
    tur += A.thruster(f'msle{sx}', P(x, -0.15, 0.17), P(x, -0.2, 0.155), 0.03)

A.finish('lattice_missile_ship', [('Body', body, None), ('Turret', tur, PIV)], 512)
