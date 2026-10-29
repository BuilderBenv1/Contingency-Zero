"""Lattice High Skyport (advanced aircraft plant). Run: python tools/models/lattice_adv_aircraft_plant.py <out_dir>

The refit of the Lattice Skyport: the octagonal deck, control tower and radar stay, joined by
a launch gate (two tall team-topped pylons bridged over the deck with a glowing launch rail),
a raised second hangar storey with a spire, and floodlight masts at the deck's front corners.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import t2_base as T
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
import factories_parts as fp
from factories_parts import slab, beam, ring

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'
c = T.capture('lattice_aircraft_plant')
body = T.drop(c.body, 'hmast', 'hmorb')
CY, DZ = -0.8, 3.0

# ---- Launch gate over the deck ----
for sx in (-1, 1):
    x = sx * 8.4
    pyl = hull_from(f'pylon{sx}', [(x + dx, CY - 1.2 + dy, z) for z, r in ((DZ, 1.0), (12.0, 0.55)) for dx, dy in
                                   ((-r, -r), (r, -r), (-r, r), (r, r))], 'hull', bevel=0.08, segs=2)
    body.append(pyl)
    body.append(box(f'pylg{sx}', (x - sx * 0.62, CY - 1.2, 7.5), (0.08, 0.4, 7.0), 'glow_dim', 0, 1))
    body.append(slab(f'pylfoot{sx}', (x, CY - 1.2, DZ + 0.25), (2.6, 2.6, 0.5), 'hull_dark', 'hull', chamfer=0.2, bevel=0.03))
gate = slab('gate', (0, CY - 1.2, 12.4), (18.2, 1.6, 1.2), 'hull', 'team', chamfer=0.3, bevel=0.05)
body.append(gate)
body.append(box('gateg', (0, CY - 2.02, 12.3), (14.0, 0.06, 0.16), 'glow', 0, 1))
for k in range(5):
    x = -6.0 + k * 3.0
    body.append(box(f'gatel{k}', (x, CY - 1.2, 11.72), (0.9, 0.9, 0.12), 'glow', 0, 1))

# ---- Second storey on the hangar, with a spire ----
st = slab('hstorey', (5.6, 9.1, 5.9), (8.4, 3.6, 2.2), 'hull', 'team', chamfer=0.45, bevel=0.06, segs=2)
inset_panels(st, 0.18, 0.04, lambda f: f.calc_area() > 4)
body.append(st)
for k in range(3):
    body.append(box(f'hswin{k}', (3.0 + k * 2.6, 7.28, 6.0), (1.8, 0.08, 0.5), 'glass', 0.02, 1))
body.append(cyl('hspire', (8.6, 9.6, 7.0), (8.6, 9.6, 12.5), 0.24, 'metal', 8, r2=0.1, bevel=0))
body.append(sphere('hspiret', (8.6, 9.6, 12.6), 0.3, 'glow', 8, 5))
for k in range(3):
    body.append(box(f'hsfin{k}', (8.6, 9.6, 8.4 + k * 1.0), (1.4 - k * 0.35, 0.08, 0.08), 'mech', 0, 1))

# ---- Floodlight masts at the front corners ----
for sx in (-1, 1):
    x, y = sx * 9.6, -9.6
    body.append(cyl(f'flood{sx}', (x, y, 0.4), (x, y, 6.5), 0.18, 'metal', 8, r2=0.12, bevel=0))
    body.append(box(f'floodh{sx}', (x, y + 0.1, 6.6), (1.0, 0.5, 0.35), 'mech', 0.03, 1))
    body.append(box(f'floodg{sx}', (x, y + 0.37, 6.6), (0.8, 0.04, 0.22), 'glow', 0, 1))

fp.finish('lattice_adv_aircraft_plant', OUT, body, c.rotor, c.rotor_name, c.rotor_pivot)
