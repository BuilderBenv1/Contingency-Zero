"""Lattice Synthesis Lab (advanced bot lab). Run: python tools/models/lattice_adv_bot_lab.py <out_dir>

The refit of the Lattice Bot Lab: the same prow-fronted twin towers and round pad, rebuilt
taller with an armoured second storey on each tower, a team-topped synthesis arch spanning the
bay with a glowing assembly halo hung over the pad, and a reactor column rising behind the back
block. Reads as the bot lab, grown up.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import t2_base as T
import kit
from kit import box, hull_from, cyl, sphere, inset_panels
import factories_parts as fp
from factories_parts import slab, beam, ring

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else '.'
c = T.capture('lattice_bot_lab')
body = T.drop(c.body, 'mast', 'orb', 'fin')   # the old antennas make way for the new storey
F = 'lattice'

# ---- Second storey on each tower: armoured, team-topped, glowing windows ----
for sx in (-1, 1):
    st = slab(f'storey{sx}', (sx * 8.5, 2.2, 11.4), (4.4, 14.0, 2.6), 'hull', 'team', chamfer=0.6, bevel=0.08, segs=2)
    inset_panels(st, 0.2, 0.05, lambda f: f.calc_area() > 4)
    body.append(st)
    for i in range(4):
        body.append(box(f'swin{sx}{i}', (sx * 10.72, -2.8 + i * 3.2, 11.3), (0.1, 2.0, 0.5), 'glass', 0.02, 1))
        body.append(box(f'swinl{sx}{i}', (sx * 10.74, -2.8 + i * 3.2, 10.95), (0.08, 2.0, 0.08), 'glow_dim', 0, 1))
    body.append(box(f'sprow{sx}', (sx * 8.5, -4.82, 11.6), (3.0, 0.12, 0.2), 'glow', 0, 1))
    # Heat-sink spires at the back of the storey.
    for k in range(3):
        x = sx * (7.3 + k * 1.2)
        body.append(cyl(f'spire{sx}{k}', (x, 7.6, 12.7), (x, 7.6, 15.0 + k * 0.6), 0.22, 'metal', 8, r2=0.1, bevel=0))
        body.append(sphere(f'spiret{sx}{k}', (x, 7.6, 15.05 + k * 0.6), 0.2, 'glow', 8, 5))

# ---- Synthesis arch across the bay, and the assembly halo hung over the pad ----
arch = slab('arch', (0, -1.6, 12.2), (13.0, 2.0, 1.4), 'hull', 'team', chamfer=0.35, bevel=0.06)
body.append(arch)
for sx in (-1, 1):
    body.append(hull_from(f'archleg{sx}', [(sx * x, -1.6 + dy, z) for dy in (-0.9, 0.9) for x, z in
                                          ((6.0, 11.5), (6.0, 9.6), (4.2, 11.5))], 'hull_dark', bevel=0.04))
    body.append(beam(f'hanger{sx}', (sx * 2.6, -0.8, 11.5), (sx * 3.2, 0.0, 8.0), 0.18, 0.18, 'metal', 0))
body.append(box('archg', (0, -2.62, 12.0), (10.0, 0.06, 0.16), 'glow', 0, 1))
body.append(ring('halo', (0, 0, 7.8), 3.6, 3.0, 0.35, 'team', 24))
body.append(ring('halog', (0, 0, 7.72), 3.2, 3.05, 0.1, 'glow', 24))
for i in range(6):
    a = i * math.pi / 3
    p0 = (math.cos(a) * 3.3, math.sin(a) * 3.3, 7.75)
    body.append(sphere(f'halon{i}', p0, 0.22, 'glow', 8, 5))

# ---- Reactor column behind the back block ----
body.append(cyl('reactor', (0, 9.2, 8.6), (0, 9.2, 13.6), 1.5, 'hull', 16, r2=1.2, bevel=0.06))
for k in range(3):
    z = 9.6 + k * 1.3
    body.append(cyl(f'rband{k}', (0, 9.2, z), (0, 9.2, z + 0.25), 1.56 - k * 0.07, 'glow', 16, bevel=0))
body.append(cyl('rcap', (0, 9.2, 13.6), (0, 9.2, 14.2), 1.25, 'team', 16, r2=0.6, bevel=0.04))
body.append(sphere('rcore', (0, 9.2, 14.35), 0.45, 'glow', 10, 6))

fp.finish('lattice_adv_bot_lab', OUT, body)
