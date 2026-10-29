"""Lattice Fleetyard (advanced shipyard). Run: python tools/models/lattice_adv_shipyard.py <out_dir>

The refit of the Lattice Shipyard: the twin piers and arched gantries remain, now with a tall
lattice crane tower on each pier carrying a boom over the basin, a second storey on the dock
block, and a third, higher gantry at the basin mouth carrying the yard's name light.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import t2_base as T
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
import factories_parts as fp
from factories_parts import slab, beam, ring

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'
c = T.capture('lattice_shipyard')
body = T.drop(c.body, 'mast')
F = 'lattice'
PT = 2.4

# ---- Crane towers on the piers ----
for sx in (-1, 1):
    x, y = sx * 9.6, 7.4
    for dx in (-0.6, 0.6):
        for dy in (-0.6, 0.6):
            body.append(beam(f'ct{sx}{dx}{dy}', (x + dx, y + dy, PT), (x + dx * 0.6, y + dy * 0.6, 13.0), 0.22, 0.22, 'hull', 0.02))
    for k in range(5):
        z = PT + 1.6 + k * 2.0
        body.append(box(f'ctb{sx}{k}', (x, y, z), (1.2, 1.2, 0.14), 'mech', 0.01, 1))
    body.append(slab(f'ccab{sx}', (x, y, 13.4), (1.8, 1.8, 1.0), 'hull', 'team', chamfer=0.2, bevel=0.04))
    body.append(beam(f'boom{sx}', (x, y, 13.6), (sx * 3.5, -4.5, 13.0), 0.4, 0.5, 'hull_dark', 0.03))
    body.append(beam(f'boomg{sx}', (x, y - 0.2, 13.3), (sx * 3.6, -4.6, 12.75), 0.08, 0.08, 'glow_dim', 0))
    body.append(cyl(f'cable{sx}', (sx * 3.6, -4.5, 12.9), (sx * 3.6, -4.5, 6.0), 0.05, 'metal', 6, bevel=0))
    body.append(sphere(f'hook{sx}', (sx * 3.6, -4.5, 5.9), 0.3, 'glow', 8, 5))

# ---- Dock second storey ----
st = slab('dstorey', (0, 9.6, 5.3), (10.0, 3.0, 2.2), 'hull', 'team', chamfer=0.4, bevel=0.06, segs=2)
inset_panels(st, 0.18, 0.04, lambda f: f.calc_area() > 4)
body.append(st)
body.append(box('dsg', (0, 8.08, 5.4), (7.0, 0.06, 0.2), 'glow', 0, 1))

# ---- Mouth gantry: taller and further out than the two inner ones ----
gy = -8.6
for sx in (-1, 1):
    for dy in (-0.9, 0.9):
        body.append(beam(f'mleg{sx}{dy}', (sx * 9.6, gy + dy, PT), (sx * 8.2, gy + dy * 0.3, 11.2), 0.5, 0.5, 'hull', 0.04))
body.append(slab('mbridge', (0, gy, 11.6), (18.4, 1.6, 1.2), 'hull', 'team', chamfer=0.3, bevel=0.05))
body.append(box('mbg', (0, gy - 0.82, 11.5), (13.0, 0.06, 0.16), 'glow', 0, 1))
for k in range(4):
    body.append(sphere(f'mbl{k}', (-4.5 + k * 3.0, gy, 10.9), 0.25, 'glow', 8, 5))

fp.finish('lattice_adv_shipyard', OUT, body)
